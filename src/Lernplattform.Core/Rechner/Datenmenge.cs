namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4a, D1: Datenmengen umrechnen (Plan P3-15 Abschnitt 4.1, Weisung P3-16). Kein
/// Python-Vorbild; die Rechnung steht gleich im unabhaengigen Skript
/// werkzeuge/vergleich/erzeuge_w4_datenmengen.py (Vergleichswerte G28). Feste Reihenfolge:
/// byte = wert * F (Bit: wert / 8), bit = byte * 8, Zielwert = byte / F. Nur Rechnung und
/// Zahltext, keine Saetze (die kommen nach der Lesung der Wortliste).
/// </summary>
public static class Datenmenge
{
    /// <summary>Hoechster erlaubter Wert in der gewaehlten Einheit (0 &lt; Wert &lt;= 10^12).</summary>
    public const double Hoechstwert = 1e12;

    /// <summary>Einheiten der Pillen in Anzeige-Reihenfolge.</summary>
    public static readonly IReadOnlyList<string> Einheiten = ["Bit", "Byte", "kB", "MB", "GB", "TB", "KiB", "MiB", "GiB", "TiB"];

    /// <summary>Dezimale Zieleinheiten (Faktor 1000).</summary>
    public static readonly IReadOnlyList<string> Dezimal = ["kB", "MB", "GB", "TB"];

    /// <summary>Binaere Zieleinheiten (Faktor 1024).</summary>
    public static readonly IReadOnlyList<string> Binaer = ["KiB", "MiB", "GiB", "TiB"];

    /// <summary>Faktor in Byte je Einheit (exakte Zweier- und Zehnerpotenzen); Bit hat keinen.</summary>
    public static double Faktor(string einheit) => einheit switch
    {
        "Byte" => 1,
        "kB" => 1e3,
        "MB" => 1e6,
        "GB" => 1e9,
        "TB" => 1e12,
        "KiB" => 1024,
        "MiB" => 1048576,
        "GiB" => 1073741824,
        "TiB" => 1099511627776,
        _ => throw new ArgumentException("unbekannte Einheit: " + einheit, nameof(einheit)),
    };

    /// <summary>Wert in Byte (Bit: geteilt durch 8).</summary>
    public static double NachByte(double wert, string einheit) => einheit == "Bit" ? wert / 8 : wert * Faktor(einheit);

    /// <summary>Gueltiger Bereich 0 &lt; wert &lt;= 10^12.</summary>
    public static bool ImBereich(double wert) => wert > 0 && wert <= Hoechstwert;

    /// <summary>Rechnet alle Einheiten; null, wenn der Wert ausserhalb des Bereichs liegt.</summary>
    public static DatenmengeErgebnis? Rechnen(double wert, string einheit)
    {
        if (!Einheiten.Contains(einheit))
        {
            throw new ArgumentException("unbekannte Einheit: " + einheit, nameof(einheit));
        }
        if (!ImBereich(wert))
        {
            return null;
        }
        var bytes = NachByte(wert, einheit);
        var werte = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var ziel in Dezimal.Concat(Binaer))
        {
            werte[ziel] = bytes / Faktor(ziel);
        }
        return new DatenmengeErgebnis(bytes, bytes * 8, werte);
    }

    /// <summary>
    /// Zahltext fuer Bit, Byte und Raten in bit/s: ganze Zahl ohne Komma (kuerzester Dezimaltext,
    /// nie DeGanz(double)), sonst bis zu 3 Nachkommastellen ohne Nullen am Ende.
    /// </summary>
    public static string GanzOderKurz(double wert) =>
        wert == Math.Truncate(wert) ? Zahlen.DeZahl(wert, 0) : Zahlen.DeKurz(wert, 3);

    /// <summary>Zielwert unter 0,01, aber nicht 0 (Anzeige-Merker, Plan 4.1).</summary>
    public static bool Klein(double wert) => wert > 0 && wert < 0.01;
}

/// <summary>Ergebnis von Datenmenge.Rechnen: Byte, Bit und die acht Zielwerte.</summary>
public sealed record DatenmengeErgebnis(double Byte, double Bit, IReadOnlyDictionary<string, double> Werte)
{
    /// <summary>Zahltext eines Zielwerts (2 Nachkommastellen, kaufmaennisch).</summary>
    public string Text(string einheit) => einheit switch
    {
        "Bit" => Datenmenge.GanzOderKurz(Bit),
        "Byte" => Datenmenge.GanzOderKurz(Byte),
        _ => Zahlen.DeZahl(Werte[einheit]),
    };
}
