using System.Globalization;
using System.Text;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Python-dict mit fester Reihenfolge der Schluessel (Einfuegereihenfolge wie Python ab 3.7).
/// Fuer <see cref="PythonJson"/>: Werte sind null, bool, long, int, double, string,
/// PyObjekt oder eine Liste (IEnumerable&lt;object?&gt;).
/// </summary>
public sealed class PyObjekt : List<KeyValuePair<string, object?>>
{
    public void Add(string schluessel, object? wert) => Add(new KeyValuePair<string, object?>(schluessel, wert));

    public object? this[string schluessel] => this.FirstOrDefault(p => p.Key == schluessel).Value;
}

/// <summary>
/// Phase 2, L9: Text wie Pythons json.dumps(wert, ensure_ascii=False, separators=(",", ":")),
/// wahlweise mit sort_keys=True (Pruefsumme der Sicherung). Kommazahlen wie Pythons repr
/// (kuerzeste Darstellung, "1e-05", "1e+16", "2.0"), Schluessel sortiert nach Codepunkten
/// wie Python, Steuerzeichen als \u00XX in Kleinbuchstaben, alles andere unveraendert.
/// Vergleichswerte G21 (Zahlentext, export_local, Pruefsumme).
/// </summary>
public static class PythonJson
{
    /// <summary>json.dumps mit kompakten Trennzeichen und ohne \u-Umschreibung.</summary>
    public static string Text(object? wert, bool sortiert = false)
    {
        var text = new StringBuilder();
        Schreiben(text, wert, sortiert);
        return text.ToString();
    }

    /// <summary>Wie Python text.encode("utf-8"): ein einzelnes Ersatzzeichen (Surrogat) ist ein Fehler.</summary>
    public static byte[] Utf8(string text) => new UTF8Encoding(false, true).GetBytes(text);

    private static void Schreiben(StringBuilder text, object? wert, bool sortiert)
    {
        switch (wert)
        {
            case null:
                text.Append("null");
                break;
            case bool b:
                text.Append(b ? "true" : "false");
                break;
            case string s:
                Zeichenkette(text, s);
                break;
            case long n:
                text.Append(n.ToString(CultureInfo.InvariantCulture));
                break;
            case int n:
                text.Append(n.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                text.Append(Zahl(d));
                break;
            case byte[]:
                // Python: "Object of type bytes is not JSON serializable" (BLOB in der Datenbank)
                throw new FormatException("Wert nicht als JSON darstellbar: bytes");
            case PyObjekt objekt:
                text.Append('{');
                var paare = sortiert ? objekt.OrderBy(p => p.Key, Codepunkte.Vergleich).ToList() : (IEnumerable<KeyValuePair<string, object?>>)objekt;
                var erstes = true;
                foreach (var (schluessel, inhalt) in paare)
                {
                    if (!erstes)
                    {
                        text.Append(',');
                    }
                    erstes = false;
                    Zeichenkette(text, schluessel);
                    text.Append(':');
                    Schreiben(text, inhalt, sortiert);
                }
                text.Append('}');
                break;
            case System.Collections.IEnumerable liste:
                text.Append('[');
                var erster = true;
                foreach (var inhalt in liste)
                {
                    if (!erster)
                    {
                        text.Append(',');
                    }
                    erster = false;
                    Schreiben(text, inhalt, sortiert);
                }
                text.Append(']');
                break;
            default:
                // Python: "Object of type bytes is not JSON serializable"
                throw new FormatException("Wert nicht als JSON darstellbar: " + wert.GetType().Name);
        }
    }

    /// <summary>py_encode_basestring: " \ und Steuerzeichen umschreiben, sonst Zeichen unveraendert.</summary>
    private static void Zeichenkette(StringBuilder text, string s)
    {
        text.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                case '\b':
                    text.Append("\\b");
                    break;
                case '\f':
                    text.Append("\\f");
                    break;
                default:
                    if (c < 0x20)
                    {
                        text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(c);
                    }
                    break;
            }
        }
        text.Append('"');
    }

    /// <summary>
    /// float.__repr__ (und json.dumps): kuerzeste Ziffernfolge, die genau diesen Wert ergibt;
    /// Exponentschreibweise, wenn der Dezimalpunkt vor der vierten Nachkommastelle oder nach
    /// der 16. Stelle laege; NaN und Unendlich wie json.dumps (allow_nan).
    /// </summary>
    public static string Zahl(double d)
    {
        if (double.IsNaN(d))
        {
            return "NaN";
        }
        if (double.IsInfinity(d))
        {
            return d > 0 ? "Infinity" : "-Infinity";
        }
        if (d == 0)
        {
            return double.IsNegative(d) ? "-0.0" : "0.0";
        }
        // .NET liefert mit "R" ebenfalls die kuerzeste Ziffernfolge; nur die Schreibweise unterscheidet sich
        var roh = Math.Abs(d).ToString("R", CultureInfo.InvariantCulture);
        var e = roh.IndexOfAny(['E', 'e']);
        var mantisse = e < 0 ? roh : roh[..e];
        var exponent = e < 0 ? 0 : int.Parse(roh[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var punkt = mantisse.IndexOf('.');
        var ziffern = mantisse.Replace(".", "", StringComparison.Ordinal);
        var dezimalpunkt = (punkt < 0 ? mantisse.Length : punkt) + exponent;
        var fuehrend = 0;
        while (fuehrend < ziffern.Length - 1 && ziffern[fuehrend] == '0')
        {
            fuehrend++;
        }
        ziffern = ziffern[fuehrend..];
        dezimalpunkt -= fuehrend;
        ziffern = ziffern.TrimEnd('0');
        if (ziffern.Length == 0)
        {
            ziffern = "0";
        }
        var vorzeichen = d < 0 ? "-" : "";
        if (dezimalpunkt <= -4 || dezimalpunkt > 16)
        {
            var exp = dezimalpunkt - 1;
            return vorzeichen + ziffern[0] + (ziffern.Length > 1 ? "." + ziffern[1..] : "")
                + "e" + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture);
        }
        if (dezimalpunkt <= 0)
        {
            return vorzeichen + "0." + new string('0', -dezimalpunkt) + ziffern;
        }
        if (dezimalpunkt >= ziffern.Length)
        {
            return vorzeichen + ziffern + new string('0', dezimalpunkt - ziffern.Length) + ".0";
        }
        return vorzeichen + ziffern[..dezimalpunkt] + "." + ziffern[dezimalpunkt..];
    }

    /// <summary>Python vergleicht Texte nach Codepunkten (nicht nach UTF-16-Einheiten).</summary>
    private sealed class Codepunkte : IComparer<string>
    {
        public static readonly Codepunkte Vergleich = new();

        public int Compare(string? a, string? b)
        {
            var x = (a ?? "").EnumerateRunes().GetEnumerator();
            var y = (b ?? "").EnumerateRunes().GetEnumerator();
            while (true)
            {
                var weiterX = x.MoveNext();
                var weiterY = y.MoveNext();
                if (!weiterX || !weiterY)
                {
                    return weiterX ? 1 : weiterY ? -1 : 0;
                }
                if (x.Current.Value != y.Current.Value)
                {
                    return x.Current.Value.CompareTo(y.Current.Value);
                }
            }
        }
    }
}
