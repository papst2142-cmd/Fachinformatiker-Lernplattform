using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 1 (Plan Abschnitt 4, Weisung P1-2 3.2/3.3): Start ohne Bedienung misst erstes
/// Oeffnen jeder Seite, Wechsel Hell/Dunkel und Wechsel der Schriftstufe und prueft
/// jede Seite in jeder Schriftstufe breit (Fenster wie gestartet) und schmal
/// (384 x 832 Punkte wie das S24 hochkant) mit der Layoutpruefung.
/// Nichts wird gespeichert. Rueckgabe: Anzahl Funde (ueber 1 000 ms oder Layout).
/// </summary>
public static class AutoMessung
{
    public const double Abbruch = 1000;
    public const double Geraetegrenze = 400;
    public const double SchmalBreite = 384;
    public const double SchmalHoehe = 832;

    public static async Task<int> LaufAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        await Zustand.Inhalte;
        // Erstes Oeffnen jeder Seite (Seitenleiste, untere Leiste, Kopfleiste)
        var ids = Texte.NavigationPc.SelectMany(p => p.Unter.Prepend(p)).Select(p => p.Id)
            .Concat(Texte.NavigationHandy.Select(p => p.Id))
            .Concat([Seiten.Lernen, Seiten.Hilfe, Seiten.Suche])
            .Distinct().ToList();
        foreach (var id in ids)
        {
            await ansicht.ZeigeAsync(id);
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        if (ansicht.GebauteSeiten[Seiten.Dashboard] is DashboardSeite dashboard)
        {
            await dashboard.Fertig.Task;
            await ansicht.BildFertigAsync();
            Messwerte.Notiere(string.Create(inv, $"Dashboard mit Werten fertig: {Messwerte.SeitStart.Elapsed.TotalMilliseconds:F0} ms nach Start"));
        }

        // Wechsel Hell/Dunkel und Schriftstufe (je 3 Durchgaenge)
        var werte = Zustand.Werte;
        for (var runde = 0; runde < 3; runde++)
        {
            foreach (var darstellung in (string[])[Farbwelt.Hell, Farbwelt.Dunkel])
            {
                var uhr = Stopwatch.StartNew();
                FarbenAnwenden.Anwenden(Farbwelt.Berechne(darstellung, Einstellungen.Grundfarbe(werte), Einstellungen.Hintergrund(werte)));
                await ansicht.BildFertigAsync();
                Messwerte.Reihe("Wechsel Hell/Dunkel", uhr.Elapsed.TotalMilliseconds);
            }
            foreach (var stufe in Einstellungen.Schriftgroessen.Append(Einstellungen.SchriftNormal))
            {
                var uhr = Stopwatch.StartNew();
                Schrift.Anwenden(Zustand.SchriftFaktor(stufe));
                await ansicht.BildFertigAsync();
                Messwerte.Reihe("Wechsel Schriftstufe", uhr.Elapsed.TotalMilliseconds);
            }
        }

        // Layoutpruefung je Seite und Schriftstufe, breit und schmal
        var fenster = TopLevel.GetTopLevel(ansicht) as Window;
        var groessen = new List<(string Name, double? Breite, double? Hoehe)> { ("breit", null, null) };
        if (fenster is not null)
        {
            groessen.Add(("schmal", SchmalBreite, SchmalHoehe));
        }
        var layoutFunde = new List<string>();
        foreach (var (name, breite, hoehe) in groessen)
        {
            if (fenster is not null && breite is double b && hoehe is double h)
            {
                fenster.Width = b;
                fenster.Height = h;
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
            }
            foreach (var stufe in Einstellungen.Schriftgroessen)
            {
                Schrift.Anwenden(Zustand.SchriftFaktor(stufe));
                foreach (var id in ansicht.GebauteSeiten.Keys.ToList())
                {
                    await ansicht.ZeigeAsync(id);
                    if (ansicht.GebauteSeiten[id] is IAufklappbar klappbar)
                    {
                        klappbar.AllesAufklappen();
                        await ansicht.BildFertigAsync();
                    }
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{name} {stufe} {id}: {fund}");
                    }
                }
            }
        }
        Zustand.DesignAnwenden();

        Messwerte.Notiere(string.Create(inv, $"Layoutpruefung (Woerter, Knoepfe, Rand), {Einstellungen.Schriftgroessen.Count} Schriftstufen x {ansicht.GebauteSeiten.Count} Seiten x {groessen.Count} Groessen: {layoutFunde.Count} Funde"));
        foreach (var fund in layoutFunde.Distinct().Take(60))
        {
            Messwerte.Notiere("  " + fund);
        }
        funde += layoutFunde.Count;
        var zuLang = Messwerte.ErstesOeffnen.Where(e => e.Value > Abbruch).Select(e => e.Key).ToList();
        foreach (var (reihe, ms) in Messwerte.Hoechstwerte().Where(r => r.Value > Abbruch))
        {
            zuLang.Add(reihe);
        }
        Messwerte.Notiere(zuLang.Count == 0
            ? "Zeitgrenze CI: alle Werte unter 1000 ms"
            : "Zeitgrenze CI ueberschritten (ueber 1000 ms): " + string.Join(", ", zuLang));
        funde += zuLang.Count;
        Messwerte.Notiere(funde == 0 ? "URTEIL: gruen" : string.Create(inv, $"URTEIL: rot ({funde} Funde)"));
        return funde;
    }
}

/// <summary>Seite mit Klappbereichen; fuer die Layoutpruefung alles aufklappen.</summary>
public interface IAufklappbar
{
    void AllesAufklappen();
}
