using System.Globalization;
using System.Numerics;
using System.Text;

namespace Lernplattform.Core.Dashboard;

// Weisung P12-179 (Engine-Projekt): unveraendert aus DashboardWerte.cs hierher verschoben, damit die Grundlagen
// (PythonRunden.Runde1) ohne die Werte des Dashboards auskommen.
/// <summary>Nachbildung der Python-Umwandlungen fuer Werte aus SQLite (str(), bool(), Slicing nach Zeichen, round(x, 1)).</summary>
internal static class PyText
{
    /// <summary>Wie Pythons bool(x) fuer SQLite-Werte.</summary>
    public static bool Wahr(object? wert) => wert switch
    {
        null => false,
        long n => n != 0,
        double d => d != 0,
        string s => s.Length > 0,
        byte[] b => b.Length > 0,
        _ => true,
    };

    /// <summary>Zahlwert einer Summe (None/Text -> 0 wie "x or 0").</summary>
    public static double Zahl(object? wert) => wert switch
    {
        long n => n,
        double d => d,
        _ => 0,
    };

    /// <summary>"%d" einer Zahl: ganzzahliger Teil (Python int()).</summary>
    public static string D(double wert) => Math.Truncate(wert).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Wie Pythons str(x) fuer SQLite-Werte.</summary>
    public static string Str(object? wert) => wert switch
    {
        null => "None",
        string s => s,
        long n => n.ToString(CultureInfo.InvariantCulture),
        double d => Float(d),
        byte[] b => Bytes(b),
        _ => Convert.ToString(wert, CultureInfo.InvariantCulture) ?? "",
    };

    /// <summary>Anzahl Zeichen wie Pythons len() (Unicode-Zeichen, nicht UTF-16-Einheiten).</summary>
    public static int Laenge(string text) => text.EnumerateRunes().Count();

    /// <summary>text[von:bis] wie in Python, gezaehlt in Unicode-Zeichen.</summary>
    public static string Teil(string text, int von, int bis)
    {
        var zeichen = text.EnumerateRunes().ToList();
        von = Math.Clamp(von, 0, zeichen.Count);
        bis = Math.Clamp(bis, von, zeichen.Count);
        var ergebnis = new StringBuilder();
        for (var i = von; i < bis; i++)
        {
            ergebnis.Append(zeichen[i].ToString());
        }
        return ergebnis.ToString();
    }

    /// <summary>Wie Pythons str.split() ohne Argument (Leerraum nach Python).</summary>
    public static List<string> Woerter(string text)
    {
        var woerter = new List<string>();
        var aktuell = new StringBuilder();
        foreach (var zeichen in text.EnumerateRunes())
        {
            if (Leerraum(zeichen.Value))
            {
                if (aktuell.Length > 0)
                {
                    woerter.Add(aktuell.ToString());
                    aktuell.Clear();
                }
            }
            else
            {
                aktuell.Append(zeichen.ToString());
            }
        }
        if (aktuell.Length > 0)
        {
            woerter.Add(aktuell.ToString());
        }
        return woerter;
    }

    /// <summary>Pythons str.isspace fuer ein Zeichen.</summary>
    private static bool Leerraum(int c) =>
        c is >= 0x09 and <= 0x0D or >= 0x1C and <= 0x20 or 0x85 or 0xA0 or 0x1680 or >= 0x2000 and <= 0x200A
            or 0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

    /// <summary>Erstes Zeichen ist Buchstabe oder Ziffer (Pythons isalnum).</summary>
    public static bool BeginntAlnum(string wort)
    {
        var zeichen = Rune.GetRuneAt(wort, 0);
        return Rune.IsLetter(zeichen) || Rune.IsNumber(zeichen);
    }

    /// <summary>Genau JJJJ-MM-TT (date.fromisoformat fuer die Form, die die Datenbank schreibt).</summary>
    public static bool IsoTag(string text, out DateOnly tag) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out tag);

    /// <summary>Pythons round(x, 1): exakter Wert der Gleitkommazahl, bei Gleichstand zur geraden Ziffer.</summary>
    public static double Runde1(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x == 0)
        {
            return x;
        }
        var bits = BitConverter.DoubleToInt64Bits(x);
        var negativ = bits < 0;
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantisse = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0)
        {
            exponent = 1;
        }
        else
        {
            mantisse |= 1L << 52;
        }
        exponent -= 1075;   // x = mantisse * 2^exponent
        var zaehler = new BigInteger(mantisse) * 10;
        var nenner = BigInteger.One;
        if (exponent > 0)
        {
            zaehler <<= exponent;
        }
        else
        {
            nenner <<= -exponent;
        }
        var ganz = BigInteger.DivRem(zaehler, nenner, out var rest);
        var doppelt = rest * 2;
        if (doppelt > nenner || (doppelt == nenner && !ganz.IsEven))
        {
            ganz += 1;
        }
        var ergebnis = (double)ganz / 10.0;
        return negativ ? -ergebnis : ergebnis;
    }

    /// <summary>Pythons repr(float).</summary>
    private static string Float(double d)
    {
        if (double.IsNaN(d))
        {
            return "nan";
        }
        if (double.IsInfinity(d))
        {
            return d > 0 ? "inf" : "-inf";
        }
        // kuerzeste eindeutige Ziffernfolge und Zehnerexponent
        var kurz = d.ToString("R", CultureInfo.InvariantCulture);
        var vorzeichen = kurz.StartsWith('-') ? "-" : "";
        kurz = kurz.TrimStart('-');
        var e = kurz.IndexOfAny(['E', 'e']);
        var exp = e >= 0 ? int.Parse(kurz[(e + 1)..], CultureInfo.InvariantCulture) : 0;
        var mantisse = e >= 0 ? kurz[..e] : kurz;
        var punkt = mantisse.IndexOf('.', StringComparison.Ordinal);
        var ziffern = mantisse.Replace(".", "", StringComparison.Ordinal);
        var stellen = (punkt >= 0 ? punkt : mantisse.Length) + exp;   // Stellen vor dem Komma
        var fuehrend = ziffern.Length - ziffern.TrimStart('0').Length;
        ziffern = ziffern.Trim('0');
        if (ziffern.Length == 0)
        {
            return vorzeichen + "0.0";
        }
        stellen -= fuehrend;
        var exp10 = stellen - 1;
        if (exp10 is < -4 or >= 16)
        {
            var rest = ziffern.Length > 1 ? "." + ziffern[1..] : "";
            return vorzeichen + ziffern[0] + rest + "e" + (exp10 < 0 ? "-" : "+") + Math.Abs(exp10).ToString("00", CultureInfo.InvariantCulture);
        }
        if (stellen <= 0)
        {
            return vorzeichen + "0." + new string('0', -stellen) + ziffern;
        }
        if (stellen >= ziffern.Length)
        {
            return vorzeichen + ziffern + new string('0', stellen - ziffern.Length) + ".0";
        }
        return vorzeichen + ziffern[..stellen] + "." + ziffern[stellen..];
    }

    /// <summary>Pythons repr(bytes).</summary>
    private static string Bytes(byte[] daten)
    {
        var anfuehrung = daten.Contains((byte)'\'') && !daten.Contains((byte)'"') ? '"' : '\'';
        var text = new StringBuilder("b").Append(anfuehrung);
        foreach (var b in daten)
        {
            text.Append(b switch
            {
                (byte)'\\' => "\\\\",
                (byte)'\t' => "\\t",
                (byte)'\n' => "\\n",
                (byte)'\r' => "\\r",
                _ when b == anfuehrung => "\\" + (char)b,
                >= 0x20 and < 0x7F => ((char)b).ToString(),
                _ => "\\x" + b.ToString("x2", CultureInfo.InvariantCulture),
            });
        }
        return text.Append(anfuehrung).ToString();
    }
}
