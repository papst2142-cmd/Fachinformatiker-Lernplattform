using System.Globalization;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>Kennwerte eines RAID (raid_values): brutto, netto, Verlust, Effizienz in Prozent, Toleranz.</summary>
public sealed record RaidWerte(double Brutto, double Netto, double Verlust, double Effizienz, int Toleranz);

/// <summary>
/// Phase 3, R3: RAID-Rechner genau wie Python v0.63.2 (fisi_core 2683-2693, 3833-3917).
/// Vergleichswerte G26.
/// </summary>
public static class Raid
{
    /// <summary>RAID_RULES: Mindestanzahl Platten und Formel (Nettokapazitaet, Ausfalltoleranz).</summary>
    public static readonly IReadOnlyDictionary<string, (int Minimum, Func<int, double, (double Netto, int Toleranz)> Formel)> Regeln =
        new Dictionary<string, (int, Func<int, double, (double, int)>)>(StringComparer.Ordinal)
        {
            ["RAID 0"] = (1, (n, s) => (n * s, 0)),
            ["RAID 1"] = (2, (n, s) => (s, n - 1)),
            ["RAID 5"] = (3, (n, s) => ((n - 1) * s, 1)),
            ["RAID 6"] = (4, (n, s) => ((n - 2) * s, 2)),
            ["RAID 10"] = (4, (n, s) => (n / 2.0 * s, 1)),
        };

    /// <summary>RAID_INPUT_MIN: Mindestanzahl nur fuer neue Eingaben (RAID 0 mit 2 Platten).</summary>
    public static readonly IReadOnlyDictionary<string, int> EingabeMinimum =
        new Dictionary<string, int>(StringComparer.Ordinal) { ["RAID 0"] = 2 };

    private static int Min(string level) => EingabeMinimum.TryGetValue(level, out var m) ? m : 0;

    /// <summary>raid_values(level, disks, size) oder null, wenn die Plattenzahl nicht passt.</summary>
    public static RaidWerte? Werte(string level, int platten, double groesse)
    {
        var (minimum, formel) = Regeln[level];
        if (platten < minimum || (level == "RAID 10" && platten % 2 != 0))
        {
            return null;
        }
        var (netto, toleranz) = formel(platten, groesse);
        var brutto = platten * groesse;
        return new RaidWerte(brutto, netto, brutto - netto, brutto != 0 ? netto / brutto * 100 : 0, toleranz);
    }

    /// <summary>raid_min_disks(level): Mindestanzahl fuer eine neue Eingabe.</summary>
    public static int MindestPlatten(string level) => Math.Max(Regeln[level].Minimum, Min(level));

    /// <summary>raid_input_problem(level, disks): Meldung oder leerer Text.</summary>
    public static string EingabeProblem(string level, int platten)
    {
        var minimum = Min(level);
        return platten < minimum
            ? string.Create(CultureInfo.InvariantCulture, $"{level} braucht mindestens {minimum} Platten.")
            : "";
    }

    /// <summary>raid_input_values: wie Werte, mit den Mindestwerten fuer neue Eingaben.</summary>
    public static RaidWerte? EingabeWerte(string level, int platten, double groesse) =>
        EingabeProblem(level, platten).Length > 0 ? null : Werte(level, platten, groesse);

    /// <summary>raid_tolerance_lines: Ausfalltoleranz als Zeilen (RAID 10 dreizeilig).</summary>
    public static List<string> ToleranzZeilen(string level, int platten, int toleranz)
    {
        var text = Zahlen.Mehrzahl(toleranz, "Festplatte", "Festplatten");
        if (level != "RAID 10")
        {
            return [text];
        }
        return [text + " sicher", string.Create(CultureInfo.InvariantCulture, $"(bis zu {platten / 2}, wenn je Spiegelpaar"),
            "nur eine ausfällt)"];
    }

    /// <summary>raid_report(level, disks_text, size_text): Nutzkapazitaet, Verlust und Effizienz.</summary>
    public static string Bericht(string level, string? plattenText, string? groesseText)
    {
        var platten = Zahlen.LesenGanz(plattenText, RAID_FIELD_DISKS);
        var groesse = Zahlen.Lesen(groesseText, RAID_FIELD_SIZE);
        if (platten <= 0 || groesse <= 0)
        {
            throw new RechnerFehler("Anzahl und Kapazität müssen größer als 0 sein.");
        }
        if (platten > 1000)
        {
            throw new RechnerFehler("Die Anzahl der Festplatten ist zu groß (höchstens 1.000).");
        }
        if (groesse > 1000000)
        {
            throw new RechnerFehler("Die Kapazität je Festplatte ist zu groß (höchstens 1.000.000 GB).");
        }
        if (groesse < 0.01)
        {
            throw new RechnerFehler("Die Kapazität je Festplatte ist zu klein (mindestens 0,01 GB).");
        }
        var n = (int)platten;
        var werte = EingabeWerte(level, n, groesse);
        if (werte is null)
        {
            var extra = level == "RAID 10" ? " und eine gerade Anzahl" : "";
            return string.Create(CultureInfo.InvariantCulture,
                $"Ungültige Konfiguration für {level}.\n\nBenötigt werden mindestens {MindestPlatten(level)} Festplatten{extra}.");
        }
        var toleranz = ToleranzZeilen(level, n, werte.Toleranz);
        var zeilen = new List<string>
        {
            "RAID-Level            : " + level,
            string.Create(CultureInfo.InvariantCulture, $"Festplatten           : {n} x {Zahlen.DeKurz(groesse)} GB"),
            "Bruttokapazität       : " + Zahlen.DeZahl(werte.Brutto) + " GB",
            "Nutzkapazität         : " + Zahlen.DeZahl(werte.Netto) + " GB",
            "Redundanz / Verlust   : " + Zahlen.DeZahl(werte.Verlust) + " GB",
            "Speichereffizienz     : " + Zahlen.DeZahl(werte.Effizienz, 1) + " %",
            "Ausfalltoleranz       : " + toleranz[0],
        };
        zeilen.AddRange(toleranz.Skip(1).Select(zeile => new string(' ', 24) + zeile));
        zeilen.AddRange(Zahlen.TausenderHinweis(plattenText, groesseText));
        return string.Join("\n", zeilen);
    }
}
