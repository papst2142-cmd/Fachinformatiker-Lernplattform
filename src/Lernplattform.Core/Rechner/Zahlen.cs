using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Rechner;

/// <summary>Ungueltige Eingabe in einem Rechner (Python InputError); der Text ist fuer die Anzeige.</summary>
public sealed class RechnerFehler(string meldung) : Exception(meldung);

/// <summary>
/// Phase 3, R1: Zahlen lesen und schreiben genau wie Python v0.63.2 (fisi_core 2568-2679,
/// 4072-4098, plural 2526). Leseregel: Komma = Dezimalzeichen, Punkt vor Dreiergruppen =
/// Tausenderpunkt, sonst Dezimalpunkt; nie double.Parse mit Laendereinstellung. Zahltext:
/// kaufmaennisch gerundet ueber den kuerzesten Dezimaltext der Gleitkommazahl (Python
/// Decimal(repr(x))), nie Math.Round. Vergleichswerte G24.
/// </summary>
public static partial class Zahlen
{
    [GeneratedRegex("^[1-9][0-9]{0,2}(\\.[0-9]{3})+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Gruppen();

    [GeneratedRegex("^[1-9][0-9]{0,2}([ \u00a0\u202f][0-9]{3})+(,[0-9]+)?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex GruppenLeer();

    [GeneratedRegex("[ \u00a0\u202f]", RegexOptions.CultureInvariant)]
    private static partial Regex LeerZeichen();

    [GeneratedRegex("^[0-9.,]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex NurZahlzeichen();

    [GeneratedRegex("^0[0-9]{1,2}(\\.[0-9]{3})+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex NullGruppen();

    /// <summary>_NUMBER_GROUPS.fullmatch (Python re: [0-9] ist nur ASCII).</summary>
    internal static bool PasstGruppen(string text) => Gruppen().IsMatch(text);

    private static bool Voll(Regex muster, string text) => muster.IsMatch(text);

    /// <summary>
    /// _number_value: liest nach der Leseregel oder liefert den Grund ("leer", "keine Zahl",
    /// "Nachkommastellen").
    /// </summary>
    internal static bool Wert(string? text, out double wert, out string grund)
    {
        wert = 0;
        grund = "";
        var roh = PythonZeichen.Strip(text ?? "");
        if (roh.Length == 0)
        {
            grund = "leer";
            return false;
        }
        if (PythonZeichen.Laenge(roh) > RechnerTexte.NUMBER_MAX_CHARS)
        {
            grund = "keine Zahl";
            return false;
        }
        var negativ = roh.StartsWith('-');
        if (negativ)
        {
            roh = roh[1..];
        }
        if (Voll(GruppenLeer(), roh))
        {
            roh = LeerZeichen().Replace(roh, ".");
        }
        if (!Voll(NurZahlzeichen(), roh) || roh.Count(c => c == ',') > 1)
        {
            grund = "keine Zahl";
            return false;
        }
        var komma = roh.IndexOf(',', StringComparison.Ordinal);
        var ganz = komma < 0 ? roh : roh[..komma];
        var rest = komma < 0 ? "" : roh[(komma + 1)..];
        var dezimal = komma >= 0;
        if (komma >= 0)
        {
            if (ganz.Contains('.', StringComparison.Ordinal))
            {
                if (!PasstGruppen(ganz))
                {
                    grund = "keine Zahl";
                    return false;
                }
                ganz = ganz.Replace(".", "", StringComparison.Ordinal);
            }
        }
        else if (ganz.Contains('.', StringComparison.Ordinal))
        {
            if (Voll(NullGruppen(), ganz))
            {
                grund = "keine Zahl";
                return false;
            }
            if (PasstGruppen(ganz))
            {
                ganz = ganz.Replace(".", "", StringComparison.Ordinal);
            }
            else if (ganz.Count(c => c == '.') == 1)
            {
                var punkt = ganz.IndexOf('.', StringComparison.Ordinal);
                rest = ganz[(punkt + 1)..];
                ganz = ganz[..punkt];
                dezimal = true;
            }
            else
            {
                grund = "keine Zahl";
                return false;
            }
        }
        if (dezimal)
        {
            if (!PythonZeichen.IstZiffernText(rest))
            {
                grund = "keine Zahl";
                return false;
            }
            if (rest.Length > RechnerTexte.NUMBER_MAX_DECIMALS)
            {
                grund = "Nachkommastellen";
                return false;
            }
        }
        if (ganz.Length == 0)
        {
            ganz = "0";
        }
        if (!PythonZeichen.IstZiffernText(ganz))
        {
            grund = "keine Zahl";
            return false;
        }
        var zahl = double.Parse(ganz + (rest.Length > 0 ? "." + rest : ""), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
        wert = zahl == 0 ? 0.0 : (negativ ? -zahl : zahl);
        return true;
    }

    /// <summary>True, wenn der Text nach der Leseregel eine Zahl ist (_host_is_number).</summary>
    internal static bool IstZahl(string text) => Wert(text, out _, out _);

    /// <summary>read_number(text, caption): Kommazahl oder RechnerFehler mit der Feldbeschriftung.</summary>
    public static double Lesen(string? text, string beschriftung) => Lies(text, beschriftung, false);

    /// <summary>read_number(text, caption, ganz=True): ganze Zahl oder RechnerFehler.</summary>
    public static BigInteger LesenGanz(string? text, string beschriftung)
    {
        var wert = Lies(text, beschriftung, true);
        if (wert != Math.Truncate(wert))
        {
            throw new RechnerFehler($"Bitte eine ganze Zahl eingeben: {beschriftung}.");
        }
        return new BigInteger(wert);
    }

    private static double Lies(string? text, string beschriftung, bool ganz)
    {
        if (Wert(text, out var wert, out var grund))
        {
            return wert;
        }
        if (grund == "leer")
        {
            throw new RechnerFehler($"Bitte einen Wert eingeben: {beschriftung}.");
        }
        throw new RechnerFehler($"Bitte eine Zahl eingeben: {beschriftung}. Zum Beispiel {(ganz ? "1.000" : "1.000 oder 1,5")}.");
    }

    /// <summary>_thousands_point_lines: ["Punkt als Tausender gelesen."] oder leer.</summary>
    public static List<string> TausenderHinweis(params string?[] texte)
    {
        foreach (var text in texte)
        {
            var ganz = PythonZeichen.Strip(text ?? "");
            if (ganz.StartsWith('-'))
            {
                ganz = ganz[1..];
            }
            var komma = ganz.IndexOf(',', StringComparison.Ordinal);
            if (komma >= 0)
            {
                ganz = ganz[..komma];
            }
            if (ganz.Contains('.', StringComparison.Ordinal) && PasstGruppen(ganz))
            {
                return ["Punkt als Tausender gelesen."];
            }
        }
        return [];
    }

    /// <summary>de_number(x, digits): deutsche Schreibweise, kaufmaennisch gerundet (HALF_UP auf repr(x)).</summary>
    public static string DeZahl(double wert, int stellen = 2)
    {
        if (double.IsNaN(wert))
        {
            return "nan";
        }
        if (double.IsInfinity(wert))
        {
            return wert > 0 ? "inf" : "-inf";
        }
        var (negativ, ziffern, exponent) = Kuerzest(wert);
        var schieben = exponent + stellen;
        BigInteger zahl;
        if (schieben >= 0)
        {
            zahl = ziffern * BigInteger.Pow(10, schieben);
        }
        else
        {
            var teiler = BigInteger.Pow(10, -schieben);
            zahl = BigInteger.DivRem(ziffern, teiler, out var rest);
            if (rest * 2 >= teiler)
            {
                zahl += 1;
            }
        }
        var text = zahl.ToString(CultureInfo.InvariantCulture);
        if (stellen > 0)
        {
            text = text.PadLeft(stellen + 1, '0');
        }
        var ganz = stellen > 0 ? text[..^stellen] : text;
        var bruch = stellen > 0 ? "," + text[^stellen..] : "";
        return (negativ ? "-" : "") + Gruppiert(ganz) + bruch;
    }

    /// <summary>Kuerzester Dezimaltext (wie Python repr): Vorzeichen, Ziffern, Zehnerexponent.</summary>
    internal static (bool Negativ, BigInteger Ziffern, int Exponent) Kuerzest(double wert)
    {
        var r = wert.ToString("R", CultureInfo.InvariantCulture);
        var negativ = r.StartsWith('-');
        if (negativ)
        {
            r = r[1..];
        }
        var exponent = 0;
        var e = r.IndexOfAny(['E', 'e']);
        if (e >= 0)
        {
            exponent = int.Parse(r[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            r = r[..e];
        }
        var punkt = r.IndexOf('.', StringComparison.Ordinal);
        if (punkt >= 0)
        {
            exponent -= r.Length - punkt - 1;
            r = r.Remove(punkt, 1);
        }
        return (negativ, BigInteger.Parse(r, NumberStyles.None, CultureInfo.InvariantCulture), exponent);
    }

    private static string Gruppiert(string ziffern)
    {
        var text = new StringBuilder();
        for (var i = 0; i < ziffern.Length; i++)
        {
            if (i > 0 && (ziffern.Length - i) % 3 == 0)
            {
                text.Append('.');
            }
            text.Append(ziffern[i]);
        }
        return text.ToString();
    }

    /// <summary>de_int(x): ganze Zahl mit Tausenderpunkten, auch sehr gross (IPv6).</summary>
    public static string DeGanz(BigInteger wert) =>
        (wert.Sign < 0 ? "-" : "") + Gruppiert(BigInteger.Abs(wert).ToString(CultureInfo.InvariantCulture));

    /// <summary>de_int(x) fuer eine Kommazahl: int(x) schneidet ab.</summary>
    public static string DeGanz(double wert) => DeGanz(new BigInteger(Math.Truncate(wert)));

    /// <summary>_de_short(x, digits): wie DeZahl, ohne ueberfluessige Nullen (0,9 statt 0,90).</summary>
    public static string DeKurz(double wert, int stellen = 2)
    {
        var text = DeZahl(wert, stellen);
        if (text.Contains(',', StringComparison.Ordinal))
        {
            text = text.TrimEnd('0').TrimEnd(',');
        }
        return text;
    }

    /// <summary>plural(n, einzahl, mehrzahl): "1 Tag", "2 Tage", "1.000 Tage".</summary>
    public static string Mehrzahl(BigInteger anzahl, string einzahl, string mehrzahl) =>
        DeGanz(anzahl) + " " + (anzahl == 1 ? einzahl : mehrzahl);
}
