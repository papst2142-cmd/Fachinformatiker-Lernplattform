using System.Text.RegularExpressions;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 11 FW7 (Sammelliste 41, 43): Umbruch der Ergebniskaesten fuer die ANZEIGE. Feste,
/// benannte Ausnahme von Python fit_lines: <see cref="Kasten(string, int, bool)"/> bleibt genau
/// wie Python (Vergleichswerte G24), die Kaesten der Oberflaeche nutzen
/// <see cref="KastenAnzeige"/> mit vier Zusatzregeln. Der Wortlaut bleibt gleich, nur die
/// Zeilenwechsel liegen anders:
/// <list type="number">
/// <item>Eine grosse Zahl mit Tausenderpunkten ("8.796.093.022.208,00") wird nie zerteilt; passt
/// sie nicht, ist der Kasten seitlich wischbar ("→ wischen").</item>
/// <item>Eine Einheit nach einer Zahl ("29,90 €", "100 %", "16 Mbit/s", "20,00 Sek.") bleibt bei der Zahl,
/// auch im Fliesstext; eine kurze Klammer ("(im Hundert)") bleibt zusammen.</item>
/// <item>Rechenzeichen ("x", "/", "+", "-") stehen nie allein am Zeilenende, sie gehen mit in die
/// neue Zeile (wie Python "=").</item>
/// <item>Ein Satz, der im Rechenweg auf zwei Zeilen verteilt ist (Folgezeile gleich eingerueckt,
/// beginnt klein), wird zusammen umbrochen, wenn seine erste Zeile ohnehin nicht passt.</item>
/// </list>
/// </summary>
public static partial class Zeilen
{
    /// <summary>Rechenzeichen, die zusaetzlich zu Python "=" in die neue Zeile gehen (Regel 3).</summary>
    public static readonly IReadOnlyList<string> AnzeigeRechenzeichen = ["x", "/", "+", "-"];

    /// <summary>Laengste Klammer, die zusammen bleibt (Regel 2), in Zeichen.</summary>
    public const int AnzeigeKlammerHoechstens = 16;

    // als Eigenschaft: die Reihenfolge statischer Felder ueber Teildateien ist nicht festgelegt
    private static string[] AnzeigeBindewoerter => field ??= [.. Bindewoerter, .. AnzeigeRechenzeichen];

    [GeneratedRegex("^€[,;.)]?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Euro();

    // abgekuerzte Einheit mit Punkt ("Sek.", "Min.", "Std.")
    [GeneratedRegex("^[A-Za-z\u00c4\u00d6\u00dc\u00e4\u00f6\u00fc]{1,4}\\.[,;)]?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Kuerzel();

    /// <summary>fit_lines fuer die Anzeige (Regeln 1 bis 4 oben), sonst genau wie <see cref="Kasten(string, int, bool)"/>.</summary>
    public static string KastenAnzeige(string text, int zeichen, bool einheitlich = false) => Kasten(text, zeichen, einheitlich, true);

    /// <summary>Regel 2: gehoert wort an das vorige Wort (Einheit nach Zahl, kurze Klammer)?</summary>
    private static bool AnzeigeKleben(string vorher, string wort)
    {
        if (EndetMitZiffer(vorher) && (Einheit().IsMatch(wort) || Euro().IsMatch(wort) || Kuerzel().IsMatch(wort)))
        {
            return true;
        }
        return vorher.StartsWith('(') && !vorher.Contains(')', StringComparison.Ordinal)
            && L(vorher) + 1 + L(wort) <= AnzeigeKlammerHoechstens;
    }

    /// <summary>Regel 4: Folgezeilen eines Satzes an die zu lange erste Zeile haengen.</summary>
    private static string[] AbsaetzeVerbinden(string[] zeilen, int zeichen)
    {
        var aus = new List<string>();
        foreach (var zeile in zeilen)
        {
            if (aus.Count > 0 && Fortsetzung(aus[^1], zeile, zeichen))
            {
                aus[^1] = aus[^1] + " " + zeile.TrimStart(' ');
            }
            else
            {
                aus.Add(zeile);
            }
        }
        return [.. aus];
    }

    private static bool Fortsetzung(string vorher, string zeile, int zeichen)
    {
        var koerper = zeile.TrimStart(' ');
        if (L(vorher) <= zeichen || koerper.Length == 0 || Einzug(vorher) != Einzug(zeile) || Einzug(zeile).Length == 0)
        {
            return false;
        }
        if (!char.IsLower(koerper[0]) || Zerlegen(vorher) is not null || Zerlegen(zeile) is not null)
        {
            return false;
        }
        return !Spalten(vorher) && !Spalten(zeile);
    }

    private static bool Spalten(string zeile) =>
        zeile.TrimStart(' ').TrimEnd(' ').Contains("  ", StringComparison.Ordinal);
}
