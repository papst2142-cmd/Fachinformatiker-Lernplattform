using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 9 W2 (Weisung P9-5 Punkt 5, Plan Phase 9 Abschnitt 12.4 Antwort 3a): Messpunkte "Inhalte laden" (alle
/// Ordner, kalt, einmalig und streng 400 ms), "Fachrichtung wechseln" (si -> ae bis die Inhalte, das Dashboard und
/// das Bild fertig sind; wiederholbar, 400 ms) und "Klausurauswahl AE" (erste Auswahl der Klausursimulation in der
/// Sicht AE, streng 400 ms). Dazu die Zahlen der Sicht AE (gemeinsame Inhalte nach einteilung_bisher.json) und die
/// Layoutpruefung der Seiten, die AE anders zeigt (Klausurauswahl, AP2-Szenarien und Testprojekte ohne Aufgaben,
/// Optionen "Lerninhalte"). Nichts wird gespeichert: die Sicht wird nur angezeigt, am Ende wieder SI.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P9W2MesspunkteAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: " + text);
            funde++;
        }

        // 1. Inhalte laden (beim Start im Hintergrund gemessen, Zustand.InhalteLaden)
        funde += Ergebnis("Inhalte laden (alle Ordner, kalt, einmalig)", Messwiederholung.Einmal(Zustand.InhalteLadenMs, Geraetegrenze));

        var gebaut = ansicht.GebauteSeiten.Keys.ToList();
        var vorher = ansicht.AktuelleSeite;
        await ansicht.ZeigeAsync(Seiten.Optionen);

        async Task<double?> Wechsel(string fachrichtung)
        {
            Zustand.SichtenVergessen();     // jeder Wechsel baut die Sicht neu (keine gemerkte Sicht)
            var uhr = Stopwatch.StartNew();
            ansicht.SichtAnwenden(fachrichtung);
            await Zustand.Inhalte;
            await Zustand.DashboardInhalte;
            await Zustand.Gewicht;
            await ansicht.BildFertigAsync();
            return uhr.Elapsed.TotalMilliseconds;
        }

        // 2. Fachrichtung wechseln (Sicht si -> ae), zurueck = Sicht si (nicht gemessen)
        var erste = await Wechsel("ae");
        var reihe = await Messwiederholung.MessenAsync(erste, Geraetegrenze, () => Wechsel("ae"),
            async () => { await Wechsel("si"); }, Task.Delay);
        funde += Ergebnis("Fachrichtung wechseln SI -> AE (Inhalte, Dashboard, Bild)", reihe);
        if (Zustand.Sicht != "ae" || await Zustand.Inhalte is not { } ae)
        {
            Fund("Fachrichtung wechseln: Sicht AE nicht gesetzt");
            return funde;
        }

        // 3. Zahlen der Sicht AE: genau die gemeinsamen bisherigen Inhalte, keine AP2-Szenarien und Testprojekte
        var einteilung = ae.Einteilung;
        var soll = (Karten: einteilung.Anzahl(Einteilung.Karteikarten, Fachrichtungen.Gemeinsam),
            Quiz: einteilung.Anzahl(Einteilung.Quizfragen, Fachrichtungen.Gemeinsam) + einteilung.Anzahl(Einteilung.QuizErzeugt, Fachrichtungen.Gemeinsam),
            Ap1: einteilung.Anzahl(Einteilung.Ap1, Fachrichtungen.Gemeinsam));
        Messwerte.Notiere(string.Create(inv,
            $"Sicht AE: {ae.Karteikarten.Count} Karteikarten (Soll {soll.Karten}), {ae.Quizfragen.Count} Pruefungsfragen (Soll {soll.Quiz}), {ae.Ap1Szenarien.Count} AP1-Szenarien (Soll {soll.Ap1}), {ae.Ap2Szenarien.Count} AP2-Szenarien, {ae.Testprojekte.Count} Testprojekte, {ae.ThemenReihenfolge.Count} Themen"));
        if (ae.Karteikarten.Count != soll.Karten || ae.Quizfragen.Count != soll.Quiz || ae.Ap1Szenarien.Count != soll.Ap1
            || ae.Ap2Szenarien.Count != 0 || ae.Testprojekte.Count != 0 || soll.Karten == 0)
        {
            Fund("Sicht AE: Zahlen weichen von einteilung_bisher.json ab");
        }

        // 4. Klausurauswahl AE: Pruefungstrainer neu gebaut (Sicht AE), Wechsel zu "Prüfung" bis zum Bild (streng)
        await ansicht.ZeigeAsync(Seiten.Trainer);
        if (ansicht.GebauteSeiten[Seiten.Trainer] is not TrainerSeite trainer)
        {
            Fund("Klausurauswahl AE: Pruefungstrainer nicht gebaut");
            return funde;
        }
        await trainer.Geladen;
        await ansicht.BildFertigAsync();
        funde += Ergebnis("Klausurauswahl AE (Wechsel zu \"Prüfung\", bis zum Bild)",
            await BildMessenAsync(ansicht, () => trainer.ArtWaehlen(TrainerTexte.Pruefung), grenze: Geraetegrenze));
        if (!Textbaum(ansicht).Contains(FachrichtungTexte.KlausurOhneAufgaben))
        {
            Fund("Klausurauswahl AE: Hinweis \"" + FachrichtungTexte.KlausurOhneAufgaben + "\" fehlt");
        }

        // 5. Layout der Ansichten, die AE anders zeigt, in jeder Schriftstufe schmal und am PC
        var fenster = TopLevel.GetTopLevel(ansicht) as Window;
        var (breiteVorher, hoeheVorher) = fenster is null ? (0d, 0d) : (fenster.Width, fenster.Height);
        var layout = new List<string>();
        foreach (var stufe in Einstellungen.Schriftgroessen)
        {
            Schrift.Anwenden(Zustand.SchriftFaktor(stufe));
            foreach (var (name, breite, hoehe) in fenster is null ? [("wie gestartet", 0d, 0d)] : new[] { ("schmal", SchmalBreite, SchmalHoehe), ("pc", PcBreite, PcHoehe) })
            {
                if (fenster is not null)
                {
                    var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
                    fenster.Width = Math.Min(breite, hoechstB);
                    fenster.Height = Math.Min(hoehe, hoechstH);
                }
                foreach (var id in new[] { Seiten.Trainer, Seiten.Ap2Szenarien, Seiten.Testprojekt, Seiten.Optionen })
                {
                    await ansicht.ZeigeAsync(id);
                    if (ansicht.GebauteSeiten[id] is SzenarioSeite szenarien)
                    {
                        await szenarien.Geladen;
                    }
                    if (ansicht.GebauteSeiten[id] is IAufklappbar klappbar)
                    {
                        klappbar.AllesAufklappen();
                    }
                    await ansicht.BildFertigAsync();
                    await ansicht.BildFertigAsync();
                    layout.AddRange(Layoutpruefung.Pruefe(ansicht).Select(f => $"{name} {stufe} AE {id}: {f}"));
                }
            }
        }
        Zustand.DesignAnwenden();
        if (fenster is not null)
        {
            fenster.Width = breiteVorher;
            fenster.Height = hoeheVorher;
        }
        Messwerte.Notiere(string.Create(inv, $"Sicht AE Layout (Klausurauswahl, AP2-Szenarien, Testprojekte, Optionen; {Einstellungen.Schriftgroessen.Count} Schriftstufen): {layout.Count} Funde"));
        foreach (var fund in layout.Distinct().Take(20))
        {
            Fund("Layout " + fund);
        }

        // 6. zurueck auf SI (wie gespeichert) und die vorher gebauten Seiten wieder bauen
        trainer.ArtWaehlen(TrainerTexte.Uebung);
        await Wechsel("si");
        foreach (var id in gebaut.Where(id => !ansicht.GebauteSeiten.ContainsKey(id)))
        {
            await ansicht.ZeigeAsync(id);
            switch (ansicht.GebauteSeiten[id])
            {
                case KarteikartenSeite karten:
                    await karten.Geladen;
                    break;
                case TrainerSeite neu:
                    await neu.Geladen;
                    break;
                case SzenarioSeite szenarien:
                    await szenarien.Geladen;
                    break;
            }
        }
        await ansicht.ZeigeAsync(vorher);
        return funde;
    }

    /// <summary>Alle Texte der sichtbaren TextBlocks (fuer die Pruefung, ob ein Hinweis zu sehen ist).</summary>
    private static string Textbaum(Control wurzel) => string.Join("\n",
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(wurzel).OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
}
