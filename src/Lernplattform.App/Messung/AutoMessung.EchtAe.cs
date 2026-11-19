using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// A 0.84.0 (Weisung P12-285 Punkt 1 und 6): die Fachrichtung AE hat in beiden AP2-Bereichen eigene Saetze. Der Start-Test
/// prueft die Auswahl (AE zeigt je Bereich eine Startzeile mit 5 Saetzen), misst Lauf und
/// Bewertung eines AE-Satzes je Bereich (Grenze 400 ms) und bedient sie mit Maus und Finger: Reiter, Anlagen, Haekchen.
/// Die Ansichten selbst prueft die Layoutpruefung in jeder Groesse ueber <see cref="EchtFormatTeil.ProbeNamen"/> ab
/// <see cref="EchtFormatTeil.AeAb"/>. A 0.85.0 (Weisung P12-291): dieselbe Pruefung fuer DPA (Ansichten ab
/// <see cref="EchtFormatTeil.DpaAb"/>, Auswahl ueber <see cref="KlausurTeil.ProbeAuswahl"/>). A 0.86.0 (Weisung P12-292): dasselbe
/// fuer DV (Ansichten ab <see cref="EchtFormatTeil.DvAb"/>); danach zeigt keine Fachrichtung mehr den Platzhalter. Gegenprobe:
/// die Ansicht <see cref="KlausurTeil.EchtAb"/> nimmt die DV-Saetze heraus und zeigt weiter Platzhalter und Meldung ohne Startknopf.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Fachrichtungen, die heute eigene AP2-Saetze haben (ausser SI); alle anderen zeigen den Platzhalter.</summary>
    private static readonly string[] EchtMitSaetzen = ["ae", "dpa", "dv"];

    private static Task<int> EchtAeAsync(MainView ansicht, KlausurTeil k, EchtFormatTeil e, EchtBestand bestand,
        Func<string, Action, Action?, bool, Task> messpunkt) =>
        EchtFachrichtungAsync(ansicht, k, e, bestand, messpunkt, "ae", EchtFormatTeil.AeAb);

    private static Task<int> EchtDpaAsync(MainView ansicht, KlausurTeil k, EchtFormatTeil e, EchtBestand bestand,
        Func<string, Action, Action?, bool, Task> messpunkt) =>
        EchtFachrichtungAsync(ansicht, k, e, bestand, messpunkt, "dpa", EchtFormatTeil.DpaAb);

    private static Task<int> EchtDvAsync(MainView ansicht, KlausurTeil k, EchtFormatTeil e, EchtBestand bestand,
        Func<string, Action, Action?, bool, Task> messpunkt) =>
        EchtFachrichtungAsync(ansicht, k, e, bestand, messpunkt, "dv", EchtFormatTeil.DvAb);

    private static async Task<int> EchtFachrichtungAsync(MainView ansicht, KlausurTeil k, EchtFormatTeil e, EchtBestand bestand,
        Func<string, Action, Action?, bool, Task> messpunkt, string frId, int ansichtAb)
    {
        var funde = 0;
        var kurz = frId.ToUpperInvariant();
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format " + kurz + ": " + text);
            funde++;
        }
        var ae = Fachrichtung.Nach(frId);
        foreach (var rolle in new[] { EchtSaetze.B1, EchtSaetze.B2 })
        {
            var zahl = bestand.Fuer(rolle, ae.Id).Count;
            if (zahl != 5)
            {
                Fund("Bereich " + ae.Bereich(rolle).Name + ": " + zahl + " Saetze statt 5");
            }
        }
        foreach (var fr in Fachrichtung.Alle.Select(f => f.Id).Where(f => f != "si" && !EchtMitSaetzen.Contains(f)))
        {
            if (bestand.Fuer(EchtSaetze.B1, fr).Count + bestand.Fuer(EchtSaetze.B2, fr).Count != 0)
            {
                Fund(fr + " hat schon Saetze (soll weiter den Platzhalter zeigen)");
            }
        }

        // Auswahl: die Fachrichtung mit Startknopf je Bereich, DV in der Probe mit Meldungen (ohne DV-Saetze, Gegenprobe
        // Platzhalter) ohne Startknopf fuer b1/b2. Die heutige Klausur hat
        // Knoepfe mit derselben Aufschrift ("Softwareprodukt starten"); gezaehlt wird darum nur ein Knopf, der neben der
        // Angabe des echten Formats steht ("... · Sätze: n, davon bearbeitet: m").
        var angabe = EchtTexte.Angabe.Split("%d")[1];
        bool Echt(Button b) => b.GetVisualAncestors().OfType<NebenOderUnter>().FirstOrDefault() is { } zeile
            && zeile.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains(angabe, StringComparison.Ordinal) == true);
        int Startknoepfe(Fachrichtung f) => ansicht.GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible
            && b.Content is TextBlock { Text: { } aufschrift }
            && (aufschrift == Beschriftungen.Einsetzen(EchtTexte.Starten, f.Bereich(EchtSaetze.B1).Kurz)
                || aufschrift == Beschriftungen.Einsetzen(EchtTexte.Starten, f.Bereich(EchtSaetze.B2).Kurz))
            && Echt(b));
        if (frId == "ae")
        {
            k.Probe(1);
        }
        else
        {
            k.ProbeAuswahl(frId);
        }
        await ansicht.BildFertigAsync();
        var aeKnoepfe = Startknoepfe(ae);
        k.Probe(KlausurTeil.EchtAb);
        await ansicht.BildFertigAsync();
        var dvKnoepfe = Startknoepfe(Fachrichtung.Nach("dv"));
        if (aeKnoepfe != 2 || dvKnoepfe != 0)
        {
            Fund("Auswahl: " + kurz + " zeigt " + aeKnoepfe + " Startknoepfe fuer b1/b2 (soll 2), DV ohne Saetze (Probe) " + dvKnoepfe + " (soll 0)");
        }
        Messwerte.Notiere("Pruefung im echten Format " + kurz + ": Auswahl mit " + aeKnoepfe + " Startknoepfen b1/b2, DV ohne Saetze (Probe) " + dvKnoepfe);

        var ab = KlausurTeil.EchtAb + 1 + ansichtAb;
        await messpunkt("Pruefung im echten Format " + kurz + " Bereich 1 Lauf, Aufgabe 1 (bis zum Bild)", () => k.Probe(ab), null, true);
        await messpunkt("Pruefung im echten Format " + kurz + " Bereich 1 Bewertung (bis zum Bild)", () => k.Probe(ab + 2), null, true);
        await messpunkt("Pruefung im echten Format " + kurz + " Bereich 2 Lauf, Aufgabe 1 (bis zum Bild)", () => k.Probe(ab + 3), null, true);
        await messpunkt("Pruefung im echten Format " + kurz + " Bereich 2 Bewertung (bis zum Bild)", () => k.Probe(ab + 5), null, true);

        // Bedienprobe je Bereich mit Maus und Finger: Reiter "Aufgabe 2", Umschalter "Anlagen", ein Haekchen der Bewertung
        foreach (var (rolle, start) in new[] { (EchtSaetze.B1, ab), (EchtSaetze.B2, ab + 3) })
        {
            var satz = bestand.Fuer(rolle, ae.Id).FirstOrDefault();
            if (satz is null)
            {
                continue;
            }
            foreach (var art in new[] { PointerType.Mouse, PointerType.Touch })
            {
                var name = satz.Kennung + " " + (art == PointerType.Mouse ? "Maus" : "Finger");
                k.Probe(start);
                await ansicht.BildFertigAsync();
                if (!await TippenAsync(ansicht, e.ProbeReiterPillen, Beschriftungen.Einsetzen(EchtTexte.AufgabeKurz, 2, satz.Aufgaben[1].Punkte), art)
                    || e.ProbeStand.Reiter != "2")
                {
                    Fund(name + ": Reiter \"Aufgabe 2\" antippen wechselt nicht (Reiter " + e.ProbeStand.Reiter + ")");
                }
                // Breites Fenster (zwei Spalten) und Aufgabe 2 mit Anlagen: die Anlagen stehen schon daneben, der Umschalter ist
                // dann gewollt verborgen (EchtFormatTeil, wie in EchtWelle2 geprueft); sonst muss Antippen die Anlagen zeigen.
                var daneben = e.ProbeZweiSpalten.Gezeigt && e.ProbeUmschalter?.IsVisible == false;
                if (daneben)
                {
                    if (e.ProbeInhalt?.Children.OfType<Grid>().Any() != true)
                    {
                        Fund(name + ": zwei Spalten ohne Anlagen neben der Aufgabe");
                    }
                }
                else if (!await TippenAsync(ansicht, e.ProbeUmschalter, EchtFormatTeil.UmschalterText(satz, satz.Aufgaben[1]), art)
                    || !e.ProbeStand.Anlagen)
                {
                    Fund(name + ": Umschalter \"Anlagen\" antippen zeigt die Anlagen nicht");
                }
                k.Probe(start + 2);
                await ansicht.BildFertigAsync();
                var vorher = e.ProbeStand.Summe;
                // A 0.94.0: hat Aufgabe 1 der Probe nur gesetzte Haekchen (ae_b2_06), wird ein gesetztes abgewaehlt; die Summe
                // muss sich in beiden Faellen aendern
                var boxen = e.ProbeInhalt?.GetVisualDescendants().OfType<CheckBox>().ToList() ?? [];
                var haken = boxen.FirstOrDefault(c => c.IsChecked == false) ?? boxen.FirstOrDefault(c => c.IsChecked == true);
                if (haken is null || !await TippenAsync(ansicht, haken, art) || e.ProbeStand.Summe == vorher)
                {
                    Fund(name + ": Haekchen der Bewertung antippen aendert den Zwischenstand nicht (" + vorher + " / " + e.ProbeStand.Summe + ")");
                }
                Messwerte.Notiere("Pruefung im echten Format " + kurz + " Bedienprobe " + name + ": Reiter, Anlagen" + (daneben ? " daneben (zwei Spalten)" : "")
                    + ", Haekchen (" + vorher + " -> "
                    + e.ProbeStand.Summe + ")");
            }
            k.Probe(start);
            await ansicht.BildFertigAsync();
            funde += await AnlagenZahl363Async(ansicht, e, kurz + " " + rolle);
        }
        return funde;
    }
}
