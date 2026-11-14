using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// A 0.76.0, Welle 1 "Prüfung im echten Format" (Weisung P12-262): Messpunkte (Grenze 400 ms) fuer Laden der Saetze,
/// Lauf, Aufgabe wechseln, Anlagen umschalten, Bewertung und Ergebnis, dazu eine Bedienprobe mit Maus und Finger:
/// Reiter, Umschalter und ein Haekchen der Bewertung werden wie von Hand angetippt (Zeigerereignisse), ohne zu speichern.
/// Die Ansichten selbst prueft die Layoutpruefung ueber <see cref="KlausurTeil.ProbeNamen"/>.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> EchtFormatMesspunkteAsync(MainView ansicht)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format: " + text);
            funde++;
        }
        // Laden und Ladepruefung der eingebauten Datei (einmalig, streng): keine Meldung erlaubt
        var uhr = Stopwatch.StartNew();
        var bestand = EchtSaetze.Laden(Zustand.LernenDateiFuerMessung(EchtSaetze.DateiName));
        funde += Ergebnis("Pruefungssaetze im echten Format laden und pruefen", Messwiederholung.Einmal(uhr.Elapsed.TotalMilliseconds, Abbruch));
        Messwerte.Notiere("Pruefung im echten Format: " + bestand.Saetze.Count + " Saetze, " + bestand.Fehler.Count + " Meldungen der Ladepruefung");
        foreach (var fehler in bestand.Fehler)
        {
            Fund("Ladepruefung " + fehler.Kennung + ": " + fehler.Meldung);
        }
        if (bestand.Saetze.Count == 0)
        {
            Fund("kein Satz geladen");
            return funde;
        }
        if (!ansicht.GebauteSeiten.TryGetValue(Seiten.Trainer, out var gebaut) || gebaut is not TrainerSeite trainer)
        {
            Fund("Pruefungstrainer nicht gebaut");
            return funde;
        }
        await trainer.Geladen;
        await ansicht.ZeigeAsync(Seiten.Trainer);
        trainer.ArtWaehlen(TrainerTexte.Pruefung);
        await ansicht.BildFertigAsync();
        var k = trainer.Klausur!;
        var e = k.Echt;
        var ab = KlausurTeil.EchtAb + 1;
        async Task Messpunkt(string titel, Action schritt, Action? zurueck = null, bool wiederholbar = false) =>
            funde += Ergebnis(titel, await BildMessenAsync(ansicht, schritt, zurueck, wiederholbar));
        await Messpunkt("Pruefung im echten Format Auswahl mit Meldungen (bis zum Bild)", () => k.Probe(KlausurTeil.EchtAb), wiederholbar: true);
        await Messpunkt("Pruefung im echten Format Lauf zeigen, Aufgabe 1 (bis zum Bild)", () => k.Probe(ab + 1), wiederholbar: true);
        await Messpunkt("Pruefung im echten Format Aufgabe wechseln (bis zum Bild)", () => e.ProbeReiter("2", false), () => e.ProbeReiter("1", false));
        await Messpunkt("Pruefung im echten Format Anlagen umschalten (bis zum Bild)", () => e.ProbeReiter("1", true), () => e.ProbeReiter("1", false));
        await Messpunkt("Pruefung im echten Format Bewertung zeigen (bis zum Bild)", () => k.Probe(ab + 7), wiederholbar: true);
        await Messpunkt("Pruefung im echten Format Bewertung Aufgabe wechseln (bis zum Bild)", () => e.ProbeReiter("2", false), () => e.ProbeReiter("1", false));
        await Messpunkt("Pruefung im echten Format Ergebnis zeigen (bis zum Bild)", () => k.Probe(ab + 9), wiederholbar: true);

        // Bedienprobe: mit Maus und mit Finger je Reiter, Umschalter und Haekchen antippen
        foreach (var art in new[] { PointerType.Mouse, PointerType.Touch })
        {
            var name = art == PointerType.Mouse ? "Maus" : "Finger";
            k.Probe(ab + 1);
            await ansicht.BildFertigAsync();
            if (!await TippenAsync(ansicht, e.ProbeReiterPillen, "3 (24)", art) || e.ProbeStand.Reiter != "3")
            {
                Fund(name + ": Reiter \"Aufgabe 3\" antippen wechselt nicht (Reiter " + e.ProbeStand.Reiter + ")");
            }
            if (!await TippenAsync(ansicht, e.ProbeUmschalter, Beschriftungen.Einsetzen(EchtTexte.Anlagen, bestand.Saetze[0].Anlagen.Count), art)
                || !e.ProbeStand.Anlagen)
            {
                Fund(name + ": Umschalter \"Anlagen\" antippen zeigt die Anlagen nicht");
            }
            k.Probe(ab + 7);
            await ansicht.BildFertigAsync();
            var vorher = e.ProbeStand.Summe;
            var haken = e.ProbeInhalt?.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(c => c.IsChecked == false);
            if (haken is null || !await TippenAsync(ansicht, haken, art) || e.ProbeStand.Summe == vorher)
            {
                Fund(name + ": Haekchen der Bewertung antippen aendert den Zwischenstand nicht (" + vorher + " / " + e.ProbeStand.Summe + ")");
            }
            Messwerte.Notiere("Pruefung im echten Format Bedienprobe " + name + ": Reiter, Anlagen, Haekchen (" + vorher + " -> " + e.ProbeStand.Summe + ")");
        }
        k.Probe(-1);
        trainer.ArtWaehlen(TrainerTexte.Uebung);
        await ansicht.BildFertigAsync();
        return funde;
    }

    /// <summary>Knopf mit dem Namen in einem Bereich antippen (Druck und Loslassen in der Mitte).</summary>
    private static async Task<bool> TippenAsync(MainView ansicht, Control? bereich, string name, PointerType art)
    {
        var ziel = bereich?.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == name);
        return ziel is not null && await TippenAsync(ansicht, ziel, art);
    }

    private static async Task<bool> TippenAsync(MainView ansicht, Control ziel, PointerType art)
    {
        ziel.BringIntoView();
        await ansicht.BildFertigAsync();
        if (TopLevel.GetTopLevel(ziel) is not { } wurzel)
        {
            return false;
        }
        var mitte = new Point(ziel.Bounds.Width / 2, ziel.Bounds.Height / 2);
        var punkt = ziel.TranslatePoint(mitte, wurzel) ?? mitte;
        var zeiger = new Pointer(Pointer.GetNextFreeId(), art, true);
        var druck = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var los = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        ziel.RaiseEvent(new PointerPressedEventArgs(ziel, zeiger, wurzel, punkt, 0, druck, KeyModifiers.None));
        ziel.RaiseEvent(new PointerReleasedEventArgs(ziel, zeiger, wurzel, punkt, 0, los, KeyModifiers.None, MouseButton.Left));
        await ansicht.BildFertigAsync();
        return true;
    }
}
