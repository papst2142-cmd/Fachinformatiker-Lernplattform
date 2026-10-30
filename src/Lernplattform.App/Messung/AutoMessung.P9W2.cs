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
/// Sicht AE, streng 400 ms). Dazu die Zahlen der Sicht AE (gemeinsame Inhalte nach einteilung_bisher.json und ab W3
/// die eigenen AE-Inhalte), ab W3 (Weisung P9-8) das erste Oeffnen von Karteikarten und Pruefungstrainer bei AE (streng
/// 400 ms) und die Layoutpruefung der Seiten, die AE anders zeigt (Klausurauswahl, AP2-Szenarien, Testprojekte ohne
/// Aufgaben, Optionen "Lerninhalte"). Nichts wird gespeichert: die Sicht wird nur angezeigt, am Ende wieder SI.
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

        // 2. bis 5. je Fachrichtung mit eigenen Inhalten: AE (W3), DPA (W4, Weisung P9-10)
        async Task<TrainerSeite?> SichtPruefen(string fr, string name)
        {
            // 2. Fachrichtung wechseln (Sicht si -> fr), zurueck = Sicht si (nicht gemessen)
            var erste = await Wechsel(fr);
            var reihe = await Messwiederholung.MessenAsync(erste, Geraetegrenze, () => Wechsel(fr),
                async () => { await Wechsel("si"); }, Task.Delay);
            funde += Ergebnis("Fachrichtung wechseln SI -> " + name + " (Inhalte, Dashboard, Bild)", reihe);
            if (Zustand.Sicht != fr || await Zustand.Inhalte is not { } ae)
            {
                Fund("Fachrichtung wechseln: Sicht " + name + " nicht gesetzt");
                return null;
            }

            // 3. Zahlen der Sicht AE: die gemeinsamen bisherigen Inhalte (einteilung_bisher.json) und die eigenen
            //    AE-Inhalte (W3, Weisung P9-8), keine Testprojekte
            var einteilung = ae.Einteilung;
            var eigene = (Karten: ae.Karteikarten.Count(e => e.Zugehoerigkeit == fr), Quiz: ae.Quizfragen.Count(e => e.Zugehoerigkeit == fr),
                Ap2: ae.Ap2Szenarien.Count(e => e.Zugehoerigkeit == fr));
            var soll = (Karten: einteilung.Anzahl(Einteilung.Karteikarten, Fachrichtungen.Gemeinsam) + eigene.Karten,
                Quiz: einteilung.Anzahl(Einteilung.Quizfragen, Fachrichtungen.Gemeinsam) + einteilung.Anzahl(Einteilung.QuizErzeugt, Fachrichtungen.Gemeinsam) + eigene.Quiz,
                Ap1: einteilung.Anzahl(Einteilung.Ap1, Fachrichtungen.Gemeinsam));
            Messwerte.Notiere(string.Create(inv,
                $"Sicht {name}: {ae.Karteikarten.Count} Karteikarten (Soll {soll.Karten}, davon {name} {eigene.Karten}), {ae.Quizfragen.Count} Pruefungsfragen (Soll {soll.Quiz}, davon {name} {eigene.Quiz}), {ae.Ap1Szenarien.Count} AP1-Szenarien (Soll {soll.Ap1}), {ae.Ap2Szenarien.Count} AP2-Szenarien (davon {name} {eigene.Ap2}), {ae.Testprojekte.Count} Testprojekte, {ae.ThemenReihenfolge.Count} Themen"));
            if (ae.Karteikarten.Count != soll.Karten || ae.Quizfragen.Count != soll.Quiz || ae.Ap1Szenarien.Count != soll.Ap1
                || ae.Ap2Szenarien.Count != eigene.Ap2 || ae.Testprojekte.Count != 0 || soll.Karten == 0
                || eigene.Karten == 0 || eigene.Quiz == 0 || eigene.Ap2 == 0)
            {
                Fund("Sicht " + name + ": Zahlen weichen von einteilung_bisher.json und den eigenen Inhalten ab");
            }

            // 3a. Karteikarten und Pruefungstrainer der Fachrichtung: erstes Oeffnen in der neuen Sicht bis zum Bild (streng)
            foreach (var (id, seite) in new[] { (Seiten.Karteikarten, "Karteikarten"), (Seiten.Trainer, "Pruefungstrainer") })
            {
                if (ansicht.GebauteSeiten.ContainsKey(id))
                {
                    Fund(seite + " " + name + ": Seite war vor dem ersten Oeffnen schon gebaut");
                    continue;
                }
                var uhr = Stopwatch.StartNew();
                await ansicht.ZeigeAsync(id);
                switch (ansicht.GebauteSeiten[id])
                {
                    case KarteikartenSeite karten:
                        await karten.Geladen;
                        break;
                    case TrainerSeite neu:
                        await neu.Geladen;
                        break;
                }
                await ansicht.BildFertigAsync();
                funde += Ergebnis(seite + " " + name + " (erstes Oeffnen, bis zum Bild)", Messwiederholung.Einmal(uhr.Elapsed.TotalMilliseconds, Geraetegrenze));
            }

            // 4. Klausurauswahl: Pruefungstrainer neu gebaut (Sicht der Fachrichtung), Wechsel zu "Prüfung" bis zum Bild (streng)
            await ansicht.ZeigeAsync(Seiten.Trainer);
            if (ansicht.GebauteSeiten[Seiten.Trainer] is not TrainerSeite trainer)
            {
                Fund("Klausurauswahl " + name + ": Pruefungstrainer nicht gebaut");
                return null;
            }
            await trainer.Geladen;
            await ansicht.BildFertigAsync();
            funde += Ergebnis("Klausurauswahl " + name + " (Wechsel zu \"Prüfung\", bis zum Bild)",
                await BildMessenAsync(ansicht, () => trainer.ArtWaehlen(TrainerTexte.Pruefung), grenze: Geraetegrenze));
            // W3/W4/W5: AE, DPA und DV haben eigene Aufgaben, der Hinweis darf nicht mehr zu sehen sein
            if (Textbaum(ansicht).Contains(FachrichtungTexte.KlausurOhneAufgaben))
            {
                Fund("Klausurauswahl " + name + ": Hinweis \"" + FachrichtungTexte.KlausurOhneAufgaben + "\" noch zu sehen");
            }

            // 5. Layout der Ansichten, die die Fachrichtung anders zeigt, in jeder Schriftstufe schmal und am PC
            var fenster = TopLevel.GetTopLevel(ansicht) as Window;
            var (breiteVorher, hoeheVorher) = fenster is null ? (0d, 0d) : (fenster.Width, fenster.Height);
            var layout = new List<string>();
            foreach (var stufe in Einstellungen.Schriftgroessen)
            {
                Schrift.Anwenden(Zustand.SchriftFaktor(stufe));
                foreach (var (groesse, breite, hoehe) in fenster is null ? [("wie gestartet", 0d, 0d)] : new[] { ("schmal", SchmalBreite, SchmalHoehe), ("pc", PcBreite, PcHoehe) })
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
                        layout.AddRange(Layoutpruefung.Pruefe(ansicht).Select(f => $"{groesse} {stufe} {name} {id}: {f}"));
                    }
                }
            }
            Zustand.DesignAnwenden();
            if (fenster is not null)
            {
                fenster.Width = breiteVorher;
                fenster.Height = hoeheVorher;
            }
            Messwerte.Notiere(string.Create(inv, $"Sicht {name} Layout (Klausurauswahl, AP2-Szenarien, Testprojekte, Optionen; {Einstellungen.Schriftgroessen.Count} Schriftstufen): {layout.Count} Funde"));
            foreach (var fund in layout.Distinct().Take(20))
            {
                Fund("Layout " + fund);
            }
            return trainer;
        }

        TrainerSeite? trainer = null;
        foreach (var (fr, name) in new[] { ("ae", "AE"), ("dpa", "DPA"), ("dv", "DV") })
        {
            if (await SichtPruefen(fr, name) is not { } t)
            {
                return funde;
            }
            trainer = t;
            t.ArtWaehlen(TrainerTexte.Uebung);
        }

        // 6. zurueck auf SI (wie gespeichert) und die vorher gebauten Seiten wieder bauen
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
        // 7. W4 (Weisung P9-10 Teil B): Optionen > Spiel aufklappen bis zum Bild (wiederholbar, 400 ms), die Zeile zum
        //    Platz und Schwierigkeitsgrad wird im Hintergrund gelesen; Auswahl Einmalzahlung/Miete und Erklaertext zu sehen
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (ansicht.GebauteSeiten[Seiten.Optionen] is OptionenSeite optionen && optionen.Klappe(Optionen.Spiel) is { } spielKarte)
        {
            async Task<double?> Aufklappen()
            {
                var uhr = Stopwatch.StartNew();
                spielKarte.IsExpanded = true;
                await ansicht.BildFertigAsync();
                return uhr.Elapsed.TotalMilliseconds;
            }
            spielKarte.IsExpanded = false;
            await ansicht.BildFertigAsync();
            var offen = await Aufklappen();
            funde += Ergebnis("Optionen > Spiel aufklappen (bis zum Bild)", await Messwiederholung.MessenAsync(offen, Geraetegrenze, Aufklappen,
                async () => { spielKarte.IsExpanded = false; await ansicht.BildFertigAsync(); }, Task.Delay));
            spielKarte.IsExpanded = true;
            var lesen = Stopwatch.StartNew();
            await optionen.SpielZeileGelesen;
            await ansicht.BildFertigAsync();
            var zeile = optionen.SpielZeile ?? "";
            Messwerte.Notiere(string.Create(inv, $"Optionen > Spiel: Zeile \"{zeile}\" (gelesen nach {lesen.Elapsed.TotalMilliseconds:F0} ms im Hintergrund)"));
            var text = Textbaum(spielKarte);
            if (!(zeile.StartsWith("Schwierigkeitsgrad: ", StringComparison.Ordinal) || zeile.StartsWith("Platz ", StringComparison.Ordinal))
                || !text.Contains(Beschriftungen.Gross(Core.Spiel.Wohnung.MieteTitel), StringComparison.Ordinal)
                || Core.Spiel.Wohnung.MieteWahl.Any(w => !text.Contains(w.Text, StringComparison.Ordinal))
                || !text.Contains("Gilt für den nächsten Umzug im Spiel.", StringComparison.Ordinal)
                || text.Contains(Beschriftungen.Platzhalter, StringComparison.Ordinal))
            {
                Fund("Optionen > Spiel: Zeile, WOHNUNGEN, Auswahl oder Erklaertext fehlen (oder Platzhalter zu sehen)");
            }
            spielKarte.IsExpanded = false;
        }
        else
        {
            Fund("Optionen > Spiel: Karte nicht gefunden");
        }

        await ansicht.ZeigeAsync(vorher);
        return funde;
    }

    /// <summary>Alle Texte der sichtbaren TextBlocks (fuer die Pruefung, ob ein Hinweis zu sehen ist).</summary>
    private static string Textbaum(Control wurzel) => string.Join("\n",
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(wurzel).OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
}
