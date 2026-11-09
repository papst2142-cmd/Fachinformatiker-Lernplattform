using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle Q (Weisung P12-76, Kurzplan Meldung P12-42): neue Ordnung der Knoepfe. Geprueft werden: (a) kein Knopf von
/// S 0.16.0 ist verloren, jeder ist mit hoechstens einem Tippen zu sehen, die haeufigen ohne Tippen (Gegenprobe: ein
/// versteckter Knopf faellt auf); (b) beide Menues in jeder Groesse hell und dunkel (Eintraege mindestens 44 Bildpunkte
/// hoch, mit Namen, ganz im Bild, Layoutpruefung), Escape und ein Eintrag schliessen das Menue; (c) Fenster „Tastenkürzel“
/// ueber F1, ? und das Menue (Gegenprobe: ? im Suchfeld oeffnet nichts); (d) Suche und Gruppen links (nur eine offen,
/// gemerkt); (e) die Kamera-Karte (seit S 0.27.0 auch die Werkzeugleiste oben) verdeckt nach dem Einpassen keinen Hausnamen und nichts von der Stadt (Gegenprobe:
/// Einpassen wie bis S 0.16.0); (f) „Prüfen – n rot“ zaehlt die roten Funde; (g) „?“ der 3D-Ansicht.
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Bedienelemente von S 0.16.0 draussen und im Kopf (Name, haeufig = ohne Tippen zu sehen).</summary>
    private static readonly (string Name, bool Haeufig)[] FruehereKnoepfe =
    [
        (Texte.Draufsicht, true), (Texte.Innenraeume, true), (Texte.Ansicht3D, true), (Texte.GebaeudeEntwerfen, true),
        (Texte.Rueckgaengig, true), (Texte.Wiederholen, true), (Texte.WerkzeugBeenden, true),
        (Texte.Neu, false), (Texte.Oeffnen, false), (Texte.Speichern, false), (Texte.DateiSpeichern, false), (Texte.AnCcSenden, false),
        (Texte.Vollbild, false), (Texte.Dunkel, false),
        (Texte.Strasse, false), (Texte.Baum, false), (Texte.Park, false), (Texte.Beet, false), (Texte.Laterne, false),
        (Texte.StrasseZiehen, false), (Texte.FusswegZiehen, false), (Texte.FlussZiehen, false), (Texte.BahnZiehen, false),
        (Texte.Kurve90, false), (Texte.Kurve45, false), (Texte.SKurve, false), (Texte.KurvenRadius, false),
        (Texte.KreisverkehrKlein, false), (Texte.KreisverkehrGross, false),
        (Texte.Fluss, false), (Texte.Teich, false), (Texte.TeichRund, false), (Texte.Steg, false), (Texte.EisImWinter, false),
        (Texte.Bahn, false), (Texte.Modell, false), (Texte.HausSetzen, false),
        (Texte.VollerKatalogOeffnen, true), (Texte.Vergroessern, true), (Texte.Verkleinern, true), (Texte.GanzeStadtZeigen, true), (Texte.Einrasten, true),
    ];

    private static IEnumerable<Control> Kandidaten(Hauptansicht a, string name)
    {
        foreach (var (text, knopf) in a.KopfKnoepfe)
        {
            if (text == name)
            {
                yield return knopf;
            }
        }
        foreach (var c in a.GetLogicalDescendants().OfType<Control>())
        {
            var beschriftung = c switch
            {
                Button { Content: TextBlock t } => t.Text,
                _ => null,
            };
            if (c is Button or CheckBox or ComboBox && (beschriftung == name || AutomationProperties.GetName(c) == name
                || AutomationProperties.GetLabeledBy(c) is TextBlock { Text: { } l } && l == name))
            {
                yield return c;
            }
        }
    }

    /// <summary>Funde zu (a): fehlt ein frueherer Knopf, braucht er mehr als ein Tippen oder ein haeufiger ueberhaupt eins.</summary>
    private static List<string> ErreichbarkeitPruefen(Hauptansicht a)
    {
        var funde = new List<string>();
        foreach (var (name, haeufig) in FruehereKnoepfe)
        {
            var klicks = Kandidaten(a, name).Select(a.KlicksBis).Where(k => k >= 0).DefaultIfEmpty(-1).Min();
            if (klicks < 0)
            {
                funde.Add("\"" + name + "\" fehlt oder ist nicht zu erreichen");
            }
            else if (klicks > (haeufig ? 0 : 1))
            {
                funde.Add("\"" + name + "\" braucht " + klicks.ToString(Inv) + " Tippen (erlaubt " + (haeufig ? "0" : "1") + ")");
            }
        }
        return funde;
    }

    private static TopLevel Oben(Hauptansicht a) => TopLevel.GetTopLevel(a)!;

    private static void Taste(Hauptansicht a, Key taste) =>
        Oben(a).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = taste, Source = Oben(a) });

    private static bool DialogMitTitel(string titel) =>
        Lernplattform.Views.Dialog.Offen is { } d && d.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == titel);

    private static void DialogSchliessen(string knopf)
    {
        if (Lernplattform.Views.Dialog.Offen is { } d && d.GetLogicalDescendants().OfType<Button>().FirstOrDefault(k => k.Content is TextBlock { } t && t.Text == knopf) is { } k)
        {
            k.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }

    private static async Task OrdnungAsync(Hauptansicht a, Window? f)
    {
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.NeuAnlegen("Starttest Ordnung", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await BildAsync(a);

        // (a) kein Knopf verloren, Gegenprobe: ein versteckter Knopf faellt auf
        foreach (var x in ErreichbarkeitPruefen(a))
        {
            Fund("Ordnung: " + x);
        }
        a.KurvenKnoepfe[0].IsVisible = false;
        var versteckt = ErreichbarkeitPruefen(a).Count;
        a.KurvenKnoepfe[0].IsVisible = true;
        Messwerte.Notiere("Ordnung: " + FruehereKnoepfe.Length.ToString(Inv) + " fruehere Knoepfe erreichbar; Gegenprobe versteckter Knopf: " + versteckt.ToString(Inv) + " Funde");
        if (versteckt == 0)
        {
            Fund("Ordnung Gegenprobe: ein versteckter Knopf faellt nicht auf (Probe taugt nicht)");
        }

        // (b) Menues in jeder Groesse hell und dunkel
        foreach (var dunkel in new[] { false, true })
        {
            if (a.Dunkel != dunkel)
            {
                a.FarbeWechseln();
            }
            foreach (var (name, b, h) in Groessen)
            {
                await GroesseAsync(a, f, b, h);
                foreach (var (menue, oeffnen) in new (string, Action)[] { (Texte.MenueProjekt, a.ProjektMenueFuerFotos), (Texte.MenueEinstellungen, a.EinstellungenMenueFuerFotos) })
                {
                    oeffnen();
                    await BildAsync(a);
                    var wo = "Menue " + menue + " " + name + " " + (dunkel ? "dunkel" : "hell");
                    if (a.MenueOffen != menue)
                    {
                        Fund(wo + ": geht nicht auf");
                    }
                    foreach (var e in a.MenueEintraege[menue].Where(e => e.IsVisible))
                    {
                        var text = e.Content is TextBlock t ? t.Text : "?";
                        if (e.Bounds.Height < Hauptansicht.EintragHoehe - 0.5)
                        {
                            Fund(string.Create(Inv, $"{wo}: Eintrag \"{text}\" nur {e.Bounds.Height:F0} Bildpunkte hoch (mindestens {Hauptansicht.EintragHoehe:F0})"));
                        }
                        if (AutomationProperties.GetName(e) is not { Length: > 0 })
                        {
                            Fund(wo + ": Eintrag \"" + text + "\" ohne Namen fuer den Screenreader");
                        }
                    }
                    var karte = a.MenueKarte;
                    if (karte.TranslatePoint(new Point(0, 0), a) is not { } p || p.X < 0 || p.Y < 0 || p.X + karte.Bounds.Width > a.Bounds.Width + 0.5 || p.Y + karte.Bounds.Height > a.Bounds.Height + 0.5)
                    {
                        Fund(wo + ": Menue liegt nicht ganz im Bild");
                    }
                    Layout(a, wo);
                    a.MenueZuFuerFotos();
                }
            }
        }
        if (a.Dunkel)
        {
            a.FarbeWechseln();
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        // Escape schliesst, ein Eintrag schliesst und fuehrt aus (Tastenkuerzel)
        a.ProjektMenueFuerFotos();
        Taste(a, Key.Escape);
        if (a.MenueOffen is not null)
        {
            Fund("Menue: Escape schliesst das Menue nicht");
            a.MenueZuFuerFotos();
        }
        a.EinstellungenMenueFuerFotos();
        a.MenueEintraege[Texte.MenueEinstellungen].First(e => e.Content is TextBlock { Text: Texte.Tastenkuerzel }).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (a.MenueOffen is not null || !DialogMitTitel(Texte.TastenTitel))
        {
            Fund("Menue: \"" + Texte.Tastenkuerzel + "\" schliesst das Menue nicht oder zeigt kein Fenster");
        }

        // (c) Tastenkuerzel: Layout in jeder Groesse, F1 und ?, Gegenprobe ? im Suchfeld
        foreach (var (name, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            Layout(a, "Tastenkuerzel " + name);
        }
        DialogSchliessen(Texte.Schliessen);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        Taste(a, Key.F1);
        await BildAsync(a);
        var mitF1 = DialogMitTitel(Texte.TastenTitel);
        DialogSchliessen(Texte.Schliessen);
        await BildAsync(a);
        a.Karte.Focus();
        Oben(a).RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "?", Source = a.Karte });
        await BildAsync(a);
        var mitFrage = DialogMitTitel(Texte.TastenTitel);
        DialogSchliessen(Texte.Schliessen);
        await BildAsync(a);
        a.SucheFeld.Focus();
        Oben(a).RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "?", Source = a.SucheFeld });
        await BildAsync(a);
        var imFeld = DialogMitTitel(Texte.TastenTitel);
        DialogSchliessen(Texte.Schliessen);
        a.SucheFuerFotos("");
        a.Karte.Focus();
        Messwerte.Notiere("Tastenkuerzel: F1 " + (mitF1 ? "ja" : "nein") + ", ? " + (mitFrage ? "ja" : "nein") + ", ? im Suchfeld " + (imFeld ? "ja" : "nein"));
        if (!mitF1 || !mitFrage || imFeld)
        {
            Fund("Tastenkuerzel: F1/? oeffnen das Fenster nicht oder ? im Suchfeld oeffnet es");
        }

        // (d) Gruppen: nur eine offen und gemerkt; Suche
        var wasser = a.GruppenLinks.First(g => g.Kennung == "wasser");
        if (!wasser.Offen)
        {
            wasser.Kopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        var offen = a.GruppenLinks.Where(g => g.Offen).Select(g => g.Kennung).ToList();
        var gemerkt = WerkzeugEinstellungen.OffeneGruppe(StadtbauApp.Ordner, [.. a.GruppenLinks.Select(g => g.Kennung)], "?");
        if (offen.Count != 1 || offen[0] != "wasser" || gemerkt != "wasser")
        {
            Fund("Gruppen links: offen " + string.Join(", ", offen) + ", gemerkt " + gemerkt + " (erwartet nur wasser)");
        }
        a.SucheFuerFotos("Kurve");
        await BildAsync(a);
        var sichtbar = a.GruppenLinks.Where(g => g.IsVisible).Select(g => g.Kennung).ToList();
        var baum = Kandidaten(a, Texte.Baum).Any(c => c.IsEffectivelyVisible);
        if (sichtbar.Count != 1 || sichtbar[0] != "strassen" || a.KurvenKnoepfe.Any(k => !k.IsEffectivelyVisible) || baum)
        {
            Fund("Suche \"Kurve\": Gruppen " + string.Join(", ", sichtbar) + ", Kurven sichtbar " + a.KurvenKnoepfe.Count(k => k.IsEffectivelyVisible).ToString(Inv) + ", Baum sichtbar " + baum);
        }
        Layout(a, "Suche Kurve");
        a.SucheFuerFotos("xyzxyz");
        await BildAsync(a);
        if (!a.KeinTrefferSichtbar || a.GruppenLinks.Any(g => g.IsVisible))
        {
            Fund("Suche ohne Treffer: kein Hinweis oder Gruppen sichtbar");
        }
        a.SucheFuerFotos("");
        await BildAsync(a);
        if (a.GruppenLinks.Any(g => !g.IsVisible) || a.GruppenLinks.Count(g => g.Offen) != 1 || !wasser.Offen)
        {
            Fund("Suche geleert: Gruppen nicht wie vorher");
        }
        a.GruppenLinks[0].Kopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // (e) Kamera-Karte verdeckt nach dem Einpassen nichts; Gegenprobe Einpassen wie bis S 0.16.0
        var verdeckt = await KameraKartePruefenAsync(a, f, melden: true);
        KartenFlaeche.UntenFreiAltFuerTests = true;
        int alt;
        try
        {
            alt = await KameraKartePruefenAsync(a, f, melden: false);
        }
        finally
        {
            KartenFlaeche.UntenFreiAltFuerTests = false;
        }
        a.Karte.Einpassen();
        Messwerte.Notiere("Kamera-Karte: " + verdeckt.ToString(Inv) + " verdeckt; Gegenprobe ohne Platz unten: " + alt.ToString(Inv));
        if (alt == 0)
        {
            Fund("Kamera-Karte Gegenprobe: ohne Platz unten verdeckt sie nichts (Probe taugt nicht)");
        }

        // (f) „Prüfen – n rot“
        var rot = a.AussenFunde.Count(x => x.Rot);
        var soll = rot > 0 ? Texte.PruefenRot(rot) : Texte.PruefenGruen;
        if (a.PruefenDraussen?.Titel != soll)
        {
            Fund("Pruefen: Kopf \"" + a.PruefenDraussen?.Titel + "\" statt \"" + soll + "\"");
        }

        // (g) „?“ der 3D-Ansicht und Kamera-Karte mit den Knoepfen der 3D-Ansicht
        a.DreiZeigen();
        await BildAsync(a);
        if (a.Hilfe3DKnopf is not { IsEffectivelyVisible: true } hilfe)
        {
            Fund("3D-Ansicht: Knopf \"?\" fehlt in der Kamera-Karte");
        }
        else
        {
            hilfe.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BildAsync(a);
            if (!DialogMitTitel(Texte.Hilfe3D))
            {
                Fund("3D-Ansicht: \"?\" zeigt die Erklaerung nicht");
            }
            DialogSchliessen(Texte.Ok);
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    /// <summary>Je Groesse nach dem Einpassen: Hausnamen und Stadt, die unter der Kamera-Karte liegen (Anzahl).</summary>
    private static async Task<int> KameraKartePruefenAsync(Hauptansicht a, Window? f, bool melden)
    {
        var verdeckt = 0;
        foreach (var (name, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            a.Karte.Einpassen();
            await BildAsync(a);
            // S 0.27.0: ebenso die Werkzeugleiste oben links (Waehlen, Massband, Ebenen, Notiz)
            foreach (var (wer, karte) in new (string, Control)[] { ("Kamera-Karte", a.KameraDraussen), ("Werkzeugleiste", a.WerkzeugLeiste) })
            {
                if (karte.TranslatePoint(new Point(0, 0), a.Karte) is not { } p)
                {
                    continue;
                }
                var r = new Rect(p, karte.Bounds.Size);
                foreach (var (text, flaeche) in a.Karte.Beschriftungen.Where(x => x.Flaeche.Intersects(r)))
                {
                    verdeckt++;
                    if (melden)
                    {
                        Fund(wer + " " + name + ": verdeckt den Namen \"" + text + "\"");
                    }
                }
                if (a.Karte.WeltFlaeche.Intersects(r))
                {
                    verdeckt++;
                    if (melden)
                    {
                        Fund(wer + " " + name + ": liegt ueber der Stadt");
                    }
                }
            }
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        return verdeckt;
    }
}
