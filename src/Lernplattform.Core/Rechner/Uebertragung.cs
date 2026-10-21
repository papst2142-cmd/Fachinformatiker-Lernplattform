using System.Globalization;
using System.Numerics;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4a, D2: Uebertragungsdauer (Plan P3-15 Abschnitt 4.2, Weisung P3-16). Kein
/// Python-Vorbild; Vergleichswerte G28 aus dem unabhaengigen Skript. Feste Reihenfolge:
/// bit = (groesse * F) * 8, rate = datenrate * 10^k * prozent / 100, t = bit / rate.
/// Aufgeteilt werden die kaufmaennisch auf Hundertstel gerundeten Sekunden als ganze Zahl,
/// so kann nie "60,00" Sekunden entstehen. Nur Rechnung und Zahltext, keine Saetze.
/// </summary>
public static class Uebertragung
{
    /// <summary>Groesse-Einheiten (wie Datenmengen, ohne Bit).</summary>
    public static readonly IReadOnlyList<string> Einheiten = ["Byte", "kB", "MB", "GB", "TB", "KiB", "MiB", "GiB", "TiB"];

    /// <summary>Raten-Einheiten; Datenraten sind immer dezimal.</summary>
    public static readonly IReadOnlyList<string> Raten = ["kbit/s", "Mbit/s", "Gbit/s"];

    /// <summary>Vorgabe der Raten-Pille (Weisung P3-14: Mbit/s).</summary>
    public const string VorgabeRate = "Mbit/s";

    /// <summary>Hoechste Datenrate in der gewaehlten Einheit (vorlaeufig, Wortliste W4a).</summary>
    public const double HoechstRate = 1e6;

    /// <summary>Faktor einer Raten-Einheit in bit/s.</summary>
    public static double RatenFaktor(string einheit) => einheit switch
    {
        "kbit/s" => 1e3,
        "Mbit/s" => 1e6,
        "Gbit/s" => 1e9,
        _ => throw new ArgumentException("unbekannte Rate: " + einheit, nameof(einheit)),
    };

    /// <summary>Rechnet die Dauer oder liefert das erste ungueltige Feld (Groesse, Rate, Prozent).</summary>
    public static UebertragungErgebnis Rechnen(double groesse, string einheit, double datenrate, string rateEinheit, double prozent)
    {
        if (!Einheiten.Contains(einheit))
        {
            throw new ArgumentException("unbekannte Einheit: " + einheit, nameof(einheit));
        }
        var faktor = RatenFaktor(rateEinheit);
        if (!Datenmenge.ImBereich(groesse))
        {
            return new UebertragungErgebnis(UebertragungFehler.Groesse, 0, 0, 0);
        }
        if (!(datenrate > 0 && datenrate <= HoechstRate))
        {
            return new UebertragungErgebnis(UebertragungFehler.Rate, 0, 0, 0);
        }
        if (!(prozent > 0 && prozent <= 100))
        {
            return new UebertragungErgebnis(UebertragungFehler.Prozent, 0, 0, 0);
        }
        var bit = groesse * Datenmenge.Faktor(einheit) * 8;
        var rate = datenrate * faktor * prozent / 100;
        return new UebertragungErgebnis(UebertragungFehler.Keiner, bit, rate, bit / rate);
    }
}

/// <summary>Erstes ungueltiges Feld der Uebertragungsdauer.</summary>
public enum UebertragungFehler
{
    Keiner,
    Groesse,
    Rate,
    Prozent,
}

/// <summary>Ergebnis: Datenmenge in Bit, wirksame Rate in bit/s, Dauer in Sekunden.</summary>
public sealed record UebertragungErgebnis(UebertragungFehler Fehler, double Bit, double Rate, double Sekunden)
{
    /// <summary>Hundertstelsekunden, kaufmaennisch gerundet ueber den kuerzesten Dezimaltext.</summary>
    public BigInteger Hundertstel
    {
        get
        {
            var (_, ziffern, exponent) = Zahlen.Kuerzest(Sekunden);
            var schieben = exponent + 2;
            if (schieben >= 0)
            {
                return ziffern * BigInteger.Pow(10, schieben);
            }
            var teiler = BigInteger.Pow(10, -schieben);
            var zahl = BigInteger.DivRem(ziffern, teiler, out var rest);
            return rest * 2 >= teiler ? zahl + 1 : zahl;
        }
    }

    /// <summary>Stunden, Minuten und Sekundentext ("10,00") aus den gerundeten Hundertsteln.</summary>
    public (BigInteger Stunden, int Minuten, string Sekunden) Aufgeteilt
    {
        get
        {
            var c = Hundertstel;
            var stunden = BigInteger.DivRem(c, 360000, out var rest);
            var minuten = (int)(rest / 6000);
            var sek = (int)(rest % 6000);
            return (stunden, minuten, (sek / 100).ToString(CultureInfo.InvariantCulture) + "," + (sek % 100).ToString("00", CultureInfo.InvariantCulture));
        }
    }
}
