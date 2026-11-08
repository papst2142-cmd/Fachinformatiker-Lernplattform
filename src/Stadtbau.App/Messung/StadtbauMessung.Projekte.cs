using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Lernplattform.Messung;
using Lernplattform.Views;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.23.0 „Projekte löschen“ (Weisung P12-96, Sammelliste 242): im Fenster „Projekt öffnen“ steht jedes Projekt in einem
/// Kasten mit „Löschen“ (Tipp, Vorlesename), auch eine kaputte Datei. Layout und Namen je Groesse hell und dunkel, ebenso die
/// Rueckfrage (Knoepfe „Löschen“, „Abbrechen“, Fokus auf „Abbrechen“). Abbrechen laesst die Datei stehen, Löschen nimmt den
/// Eintrag weg und legt die Kopie in die Sicherungen. Taste Entf auf einem Kasten fragt, auf dem Fenster selbst nicht. Das
/// offene Projekt: Rueckfrage bei nicht gespeicherter Aenderung, Abbrechen haelt es offen, danach Startseite.
/// Gegenprobe: Liste wie bis S 0.22.1 (ein Knopf je Projekt) muss auffallen.
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Funde an den Kaesten des Fensters „Projekt öffnen“ (erwartet: <paramref name="dateien"/> Eintraege).</summary>
    private static List<string> ProjektKaestenPruefen(Hauptansicht a, IReadOnlyList<string> dateien)
    {
        var funde = new List<string>();
        if (a.ProjektKaesten.Count != dateien.Count)
        {
            funde.Add("Liste zeigt " + a.ProjektKaesten.Count.ToString(Inv) + " Projekte statt " + dateien.Count.ToString(Inv));
        }
        foreach (var (kasten, knoepfe, datei) in a.ProjektKaesten)
        {
            var name = Projekt.AnzeigeName(datei);
            if (TopLevel.GetTopLevel(kasten) is null || !kasten.IsEffectivelyVisible)
            {
                funde.Add("\"" + name + "\" steht in keinem Kasten");
                continue;
            }
            var loeschen = knoepfe.FirstOrDefault(k => k.Content is TextBlock { Text: var t } && t == Texte.Loeschen);
            if (loeschen is null)
            {
                funde.Add("\"" + name + "\" hat keinen Knopf \"" + Texte.Loeschen + "\"");
            }
            else
            {
                if (Tipp.Von(loeschen) != Texte.ProjektLoeschenTipp)
                {
                    funde.Add("\"" + name + "\": Tipp fehlt am Knopf \"" + Texte.Loeschen + "\"");
                }
                if (Avalonia.Automation.AutomationProperties.GetName(loeschen) is not { } v || !v.Contains(name, StringComparison.Ordinal))
                {
                    funde.Add("\"" + name + "\": Vorlesename des Knopfs nennt das Projekt nicht");
                }
            }
            var innen = new Rect(kasten.Bounds.Size);
            foreach (var k in knoepfe)
            {
                if (k.TranslatePoint(new Point(0, 0), kasten) is not { } p || !k.IsEffectivelyVisible
                    || !innen.Contains(new Rect(p, k.Bounds.Size).Deflate(0.5)))
                {
                    funde.Add("\"" + name + "\": Knopf liegt nicht in seinem Kasten");
                }
            }
        }
        return funde;
    }

    private static List<string> OffeneKnoepfe() => Dialog.Offen is { } d
        ? [.. d.GetLogicalDescendants().OfType<Button>().Where(k => k.Parent is WrapPanel).Select(k => k.Content is TextBlock t ? t.Text ?? "" : "")]
        : [];

    private static void OffenWaehlen(string text)
    {
        if (Dialog.Offen is { } d
            && d.GetLogicalDescendants().OfType<Button>().FirstOrDefault(k => k.Parent is WrapPanel && k.Content is TextBlock { } t && t.Text == text) is { } k)
        {
            k.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        else
        {
            Fund("Projekte loeschen: Knopf \"" + text + "\" im offenen Fenster fehlt");
        }
    }

    /// <summary>Bis zu 5 s auf eine Bedingung warten (Bild fuer Bild).</summary>
    private static async Task<bool> BisAsync(Hauptansicht a, Func<bool> bedingung)
    {
        for (var i = 0; i < 100 && !bedingung(); i++)
        {
            await BildAsync(a);
            await Task.Delay(50);
        }
        return bedingung();
    }

    private static bool KnoepfeSind(params string[] soll) => OffeneKnoepfe().SequenceEqual(soll);

    private static Button? LoeschKnopf(Hauptansicht a, string datei) =>
        a.ProjektKaesten.FirstOrDefault(e => e.Datei == datei).Knoepfe?.FirstOrDefault(k => k.Content is TextBlock { Text: var t } && t == Texte.Loeschen);

    private static async Task ProjekteLoeschenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Löschen A", null);
        var dateiA = a.Projekt!.Datei;
        a.NeuAnlegen("Starttest Löschen B", null);
        var dateiB = a.Projekt!.Datei;
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        var kaputt = Path.Combine(a.Ablage.Projekte, "starttest_kaputt" + Projekt.Endung);
        File.WriteAllText(kaputt, "kein JSON {");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Gegenprobe: Liste wie bis S 0.22.1 (ein Knopf je Projekt, kein Kasten, kein Löschen) muss auffallen
        Hauptansicht.ProjektListeAltFuerTests = true;
        var alt = a.OeffnenAsync();
        await BildAsync(a);
        var gegenprobe = ProjektKaestenPruefen(a, Projekt.Liste(a.Ablage)).Count;
        OffenWaehlen(Texte.Abbrechen);
        await alt;
        Hauptansicht.ProjektListeAltFuerTests = false;
        Messwerte.Notiere("Projekte loeschen Gegenprobe (Liste wie S 0.22.1): " + gegenprobe.ToString(Inv) + " Funde");
        if (gegenprobe == 0)
        {
            Fund("Projekte loeschen: Gegenprobe (Liste ohne Kaesten und Löschen) ohne Fund (Probe taugt nicht)");
        }

        var fenster = a.OeffnenAsync();
        await BildAsync(a);
        foreach (var x in ProjektKaestenPruefen(a, Projekt.Liste(a.Ablage)))
        {
            Fund("Projekt oeffnen: " + x);
        }
        if (!a.ProjektKaesten.Any(e => e.Datei == kaputt))
        {
            Fund("Projekt oeffnen: kaputte Datei steht nicht in der Liste");
        }
        await AlleGroessenAsync(a, f, "Projekt oeffnen");

        // Abbrechen: Rueckfrage mit „Abbrechen“ als Vorgabe, die Datei bleibt
        LoeschKnopf(a, dateiA)?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!await BisAsync(a, () => KnoepfeSind(Texte.Loeschen, Texte.Abbrechen)))
        {
            Fund("Projekte loeschen: Rueckfrage zeigt \"" + string.Join(" | ", OffeneKnoepfe()) + "\"");
        }
        await BildAsync(a);
        if (TopLevel.GetTopLevel(a)?.FocusManager?.GetFocusedElement() is not Button { Content: TextBlock { Text: var fokus } } || fokus != Texte.Abbrechen)
        {
            Fund("Projekte loeschen: Vorgabe der Rueckfrage ist nicht \"" + Texte.Abbrechen + "\"");
        }
        foreach (var (name, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            Layout(a, "Rueckfrage Projekt loeschen " + name);
        }
        OffenWaehlen(Texte.Abbrechen);
        await BisAsync(a, () => KnoepfeSind(Texte.OeffnenDatei, Texte.Abbrechen));
        if (!File.Exists(dateiA))
        {
            Fund("Projekte loeschen: Abbrechen hat das Projekt geloescht");
        }

        // Löschen: Eintrag weg, Kopie in den Sicherungen
        LoeschKnopf(a, dateiA)?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BisAsync(a, () => KnoepfeSind(Texte.Loeschen, Texte.Abbrechen));
        OffenWaehlen(Texte.Loeschen);
        await BisAsync(a, () => !File.Exists(dateiA) && KnoepfeSind(Texte.OeffnenDatei, Texte.Abbrechen));
        await BildAsync(a);
        var kopien = Directory.Exists(new Sicherungen(a.Ablage, "Starttest Löschen A").Ordner)
            ? Directory.GetFiles(new Sicherungen(a.Ablage, "Starttest Löschen A").Ordner, "geloescht_*" + Projekt.Endung) : [];
        if (File.Exists(dateiA) || a.ProjektKaesten.Any(e => e.Datei == dateiA) || kopien.Length != 1)
        {
            Fund("Projekte loeschen: nach \"" + Texte.Loeschen + "\" Datei da " + File.Exists(dateiA) + ", in der Liste "
                + a.ProjektKaesten.Any(e => e.Datei == dateiA) + ", Kopien " + kopien.Length.ToString(Inv));
        }
        foreach (var x in ProjektKaestenPruefen(a, Projekt.Liste(a.Ablage)))
        {
            Fund("Projekt oeffnen nach dem Loeschen: " + x);
        }

        // Taste Entf: auf dem Fenster selbst nichts, auf dem Kasten der kaputten Datei die Rueckfrage
        Dialog.Offen?.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Delete });
        await BildAsync(a);
        if (!KnoepfeSind(Texte.OeffnenDatei, Texte.Abbrechen))
        {
            Fund("Projekte loeschen: Entf ohne Kasten mit Fokus fragt trotzdem");
        }
        var kaputtKnopf = a.ProjektKaesten.FirstOrDefault(e => e.Datei == kaputt).Knoepfe?.FirstOrDefault();
        kaputtKnopf?.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Delete, Source = kaputtKnopf });
        if (!await BisAsync(a, () => KnoepfeSind(Texte.Loeschen, Texte.Abbrechen)))
        {
            Fund("Projekte loeschen: Entf auf dem Kasten fragt nicht");
        }
        OffenWaehlen(Texte.Loeschen);
        if (!await BisAsync(a, () => !File.Exists(kaputt)))
        {
            Fund("Projekte loeschen: kaputte Datei laesst sich nicht loeschen");
        }

        // das offene Projekt mit nicht gespeicherter Aenderung: Abbrechen haelt es offen, ohne Speichern loeschen schliesst es
        await BisAsync(a, () => KnoepfeSind(Texte.OeffnenDatei, Texte.Abbrechen));
        Hauptansicht.UngespeichertFuerTests = true;
        try
        {
            foreach (var (wahl, weg) in new[] { (Texte.Abbrechen, false), (Texte.OhneSpeichernLoeschen, true) })
            {
                LoeschKnopf(a, dateiB)?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await BisAsync(a, () => KnoepfeSind(Texte.Loeschen, Texte.Abbrechen));
                OffenWaehlen(Texte.Loeschen);
                if (!await BisAsync(a, () => KnoepfeSind(Texte.SpeichernUndLoeschen, Texte.OhneSpeichernLoeschen, Texte.Abbrechen)))
                {
                    Fund("Projekte loeschen: offenes Projekt, Rueckfrage zeigt \"" + string.Join(" | ", OffeneKnoepfe()) + "\"");
                    break;
                }
                if (wahl == Texte.Abbrechen)
                {
                    foreach (var (name, b, h) in Groessen)
                    {
                        await GroesseAsync(a, f, b, h);
                        Layout(a, "Rueckfrage offenes Projekt loeschen " + name);
                    }
                }
                OffenWaehlen(wahl);
                await BisAsync(a, () => KnoepfeSind(Texte.OeffnenDatei, Texte.Abbrechen));
                if (File.Exists(dateiB) == weg || (a.Projekt is null) != weg)
                {
                    Fund("Projekte loeschen: offenes Projekt nach \"" + wahl + "\": Datei da " + File.Exists(dateiB) + ", Projekt offen " + (a.Projekt is not null));
                }
            }
        }
        finally
        {
            Hauptansicht.UngespeichertFuerTests = false;
        }
        OffenWaehlen(Texte.Abbrechen);
        await fenster;
        await BildAsync(a);
        var start = a.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == Texte.Willkommen && t.IsEffectivelyVisible);
        if (a.Projekt is not null || !start)
        {
            Fund("Projekte loeschen: nach dem Loeschen des offenen Projekts keine Startseite");
        }
        await AlleGroessenAsync(a, f, "Start nach Projekt loeschen");
        Messwerte.Notiere("Projekte loeschen: Kaesten, Rueckfrage mit Vorgabe Abbrechen, Kopie in den Sicherungen, Entf, offenes Projekt -> Startseite");
    }
}
