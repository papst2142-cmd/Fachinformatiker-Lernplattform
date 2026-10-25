using System.Globalization;
using System.Numerics;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Textbausteine wie Python 0.63.2 fuer die Rueckmeldungen des Spiels: der
/// %-Operator (<c>"%d von %s" % (...)</c>), <c>fisi_game._num</c>, <c>_euro</c>,
/// <c>fisi_core.plural</c> und float(text). Nachkommastellen werden wie Python auf dem exakten
/// Wert halb gerade gerundet (<see cref="PythonRunden"/>), nie mit ToString("F2").
/// </summary>
public static class PyFormat
{
    /// <summary>fmt % (werte...) fuer %s, %d, %i, %f mit Breite, Genauigkeit, Flags "-", "0", "+", " " und %%.</summary>
    public static string Prozent(string fmt, params object?[] werte)
    {
        var b = new StringBuilder();
        var n = 0;
        for (var i = 0; i < fmt.Length; i++)
        {
            if (fmt[i] != '%')
            {
                b.Append(fmt[i]);
                continue;
            }
            i++;
            if (i >= fmt.Length)
            {
                throw new FormatException("incomplete format");
            }
            if (fmt[i] == '%')
            {
                b.Append('%');
                continue;
            }
            var flags = "";
            while ("-0+ #".Contains(fmt[i], StringComparison.Ordinal))
            {
                flags += fmt[i++];
            }
            var breite = 0;
            while (char.IsAsciiDigit(fmt[i]))
            {
                breite = breite * 10 + (fmt[i++] - '0');
            }
            int? genau = null;
            if (fmt[i] == '.')
            {
                i++;
                var g = 0;
                while (char.IsAsciiDigit(fmt[i]))
                {
                    g = g * 10 + (fmt[i++] - '0');
                }
                genau = g;
            }
            if (n >= werte.Length)
            {
                throw new FormatException("not enough arguments for format string");
            }
            var wert = werte[n++];
            var text = fmt[i] switch
            {
                's' => genau is int g2 ? Rechner.PythonZeichen.Teil(Py.Str(wert), 0, g2) : Py.Str(wert),
                'd' or 'i' => Vorzeichen(Ganz(wert).ToString(CultureInfo.InvariantCulture), flags),
                'f' => Vorzeichen(Fest(Py.F(wert), genau ?? 6), flags),
                _ => throw new FormatException("Format %" + fmt[i] + " nicht nachgebaut"),
            };
            // Breite in Codepunkten wie Python (ein Emoji zaehlt 1, in UTF-16 aber 2)
            var lang = Rechner.PythonZeichen.Laenge(text);
            if (lang < breite)
            {
                text = flags.Contains('-', StringComparison.Ordinal) ? text.PadRight(text.Length + breite - lang)
                    : flags.Contains('0', StringComparison.Ordinal) && fmt[i] != 's' ? Nullen(text, breite)
                    : text.PadLeft(text.Length + breite - lang);
            }
            b.Append(text);
        }
        if (n != werte.Length)
        {
            throw new FormatException("not all arguments converted during string formatting");
        }
        return b.ToString();
    }

    private static BigInteger Ganz(object? wert) => wert switch
    {
        double d when double.IsFinite(d) => new BigInteger(Math.Truncate(d)),
        double => throw new OverflowException("cannot convert float infinity to integer"),
        string => throw new FormatException("%d format: a real number is required, not str"),
        null => throw new FormatException("%d format: a real number is required, not NoneType"),
        _ => Py.Int(wert),
    };

    private static string Vorzeichen(string text, string flags) =>
        text.StartsWith('-') ? text
        : flags.Contains('+', StringComparison.Ordinal) ? "+" + text
        : flags.Contains(' ', StringComparison.Ordinal) ? " " + text
        : text;

    private static string Nullen(string text, int breite)
    {
        var zeichen = text.Length > 0 && text[0] is '-' or '+' or ' ' ? text[..1] : "";
        return zeichen + text[zeichen.Length..].PadLeft(breite - zeichen.Length, '0');
    }

    /// <summary>"%.{stellen}f" % x: exakter Wert halb gerade gerundet; -0.0 und kleine negative Werte behalten "-".</summary>
    public static string Fest(double x, int stellen)
    {
        if (double.IsNaN(x))
        {
            return "nan";
        }
        if (double.IsInfinity(x))
        {
            return x > 0 ? "inf" : "-inf";
        }
        var ganz = BigInteger.Abs(PythonRunden.Gerundet(x, stellen)).ToString(CultureInfo.InvariantCulture);
        if (stellen > 0)
        {
            ganz = ganz.PadLeft(stellen + 1, '0');
            ganz = ganz[..^stellen] + "." + ganz[^stellen..];
        }
        var negativ = x < 0 || (x == 0 && double.IsNegative(x));
        return (negativ ? "-" : "") + ganz;
    }

    /// <summary>"{:,.0f}".format(wert).replace(",", ".") (metric_text).</summary>
    public static string GanzPunkt(object? wert)
    {
        var text = Fest(Py.F(wert), 0);
        var zeichen = text.StartsWith('-') ? "-" : "";
        return zeichen + Tausender(text.TrimStart('-')).Replace(',', '.');
    }

    /// <summary>"{:,}" bzw. "{:,.2f}" ohne Vorzeichen: Tausender mit Komma.</summary>
    private static string Tausender(string ziffern)
    {
        var teile = new List<string>();
        while (ziffern.Length > 3)
        {
            teile.Insert(0, ziffern[^3..]);
            ziffern = ziffern[..^3];
        }
        teile.Insert(0, ziffern);
        return string.Join(",", teile);
    }

    /// <summary>fisi_game._num: deutsch 1.234,5 ohne ueberfluessige Nachkommastellen; nan/inf "–", -0 wird 0.</summary>
    public static string Num(object? wert)
    {
        string text;
        if (wert is double d && !double.IsFinite(d))
        {
            return "–";
        }
        if (wert is double k && k != Math.Floor(k))
        {
            text = Fest(k, 2).TrimEnd('0').TrimEnd('.');
        }
        else
        {
            text = (wert is double g ? PythonRunden.Runde(g) : Py.Int(wert)).ToString(CultureInfo.InvariantCulture);
        }
        if (text == "-0")
        {
            text = "0";
        }
        var punkt = text.IndexOf('.', StringComparison.Ordinal);
        var ganz = punkt < 0 ? text : text[..punkt];
        var rest = punkt < 0 ? "" : text[(punkt + 1)..];
        var zeichen = ganz.StartsWith('-') ? "-" : "";
        ganz = ganz.TrimStart('-');
        return zeichen + Tausender(ganz).Replace(',', '.') + (rest.Length > 0 ? "," + rest : "");
    }

    /// <summary>fisi_game._euro: "{:,.2f}" mit deutschen Zeichen und " €".</summary>
    public static string Euro(object? wert)
    {
        var text = wert is double d ? Fest(d, 2) : Py.Int(wert).ToString(CultureInfo.InvariantCulture) + ".00";
        var zeichen = text.StartsWith('-') ? "-" : "";
        text = text.TrimStart('-');
        var punkt = text.IndexOf('.', StringComparison.Ordinal);
        var ganz = punkt < 0 ? text : text[..punkt];
        var rest = punkt < 0 ? "" : text[punkt..];
        var englisch = zeichen + Tausender(ganz) + rest;
        return englisch.Replace(",", "X", StringComparison.Ordinal).Replace(".", ",", StringComparison.Ordinal)
            .Replace("X", ".", StringComparison.Ordinal) + " €";
    }

    /// <summary>fisi_core.plural ohne Zahlwort fuer ganze Zahlen: "1 Tag", "2 Tage", "1.000 Tage".</summary>
    public static string Plural(object? anzahl, string einzahl, string mehrzahl)
    {
        if (!Py.IstInt(anzahl))
        {
            throw new NotSupportedException("plural() nur fuer ganze Zahlen nachgebaut");
        }
        var n = Py.Int(anzahl);
        var zahl = Tausender(BigInteger.Abs(n).ToString(CultureInfo.InvariantCulture)).Replace(',', '.');
        return (n < 0 ? "-" : "") + zahl + " " + (n == 1 ? einzahl : mehrzahl);
    }

    /// <summary>float(text) wie Python: Leerraum, Unterstriche nur zwischen Ziffern, inf/nan, Unicode-Ziffern; sonst null.</summary>
    public static double? Float(string text)
    {
        var t = Rechner.PythonZeichen.Strip(text);
        var b = new StringBuilder();
        foreach (var c in t)
        {
            b.Append(char.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber ? (char)('0' + (int)char.GetNumericValue(c)) : c);
        }
        t = b.ToString();
        var klein = Suche.PythonKlein.Klein(t);
        var ohne = klein.TrimStart('+', '-');
        if (ohne.Length == klein.Length - 1 || ohne.Length == klein.Length)
        {
            var minus = klein.StartsWith('-');
            if (ohne is "inf" or "infinity")
            {
                return minus ? double.NegativeInfinity : double.PositiveInfinity;
            }
            if (ohne == "nan")
            {
                return double.NaN;
            }
        }
        const string ziffern = @"[0-9](?:_?[0-9])*";
        if (!System.Text.RegularExpressions.Regex.IsMatch(t, @"\A[+-]?(?:" + ziffern + @"(?:\.(?:" + ziffern + @")?)?|\." + ziffern + @")(?:[eE][+-]?" + ziffern + @")?\z"))
        {
            return null;
        }
        return double.Parse(t.Replace("_", "", StringComparison.Ordinal), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
