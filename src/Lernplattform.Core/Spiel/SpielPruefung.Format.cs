using System.Globalization;
using System.Text;

namespace Lernplattform.Core.Spiel;

/// <summary>Ausgang von <c>text.format(**werte)</c> in der Inhaltspruefung (Sammelliste 84).</summary>
public enum FormatUrteil
{
    /// <summary>Python formatiert ohne Fehler.</summary>
    Geht,

    /// <summary>KeyError, IndexError oder ValueError: Python faengt ihn und meldet ein Problem.</summary>
    Fehler,

    /// <summary>Eine andere Ausnahme (TypeError, AttributeError): die Python-Pruefung bricht ab.</summary>
    Abbruch,

    /// <summary>Nicht nachgebildet (Ketten ueber Methoden, __doc__ und __class__ hinaus, riesige Breiten): wie Abbruch, strenger als Python.</summary>
    NichtNachgebildet,
}

/// <summary>
/// Phase 9 W1 (Sammelliste 84): <c>str.format(**werte)</c> wie Python 3.12 (Objects/stringlib/unicode_format.h,
/// Python/formatter_unicode.c) fuer Werte aus Text und ganzen Zahlen: Feldnamen mit Index und Attribut,
/// Umwandlung !r/!s/!a, Format-Angabe nach der Mini-Sprache, verschachtelte Felder in der Format-Angabe
/// (hoechstens zwei Stufen). Gegenproben in G42 (<c>spiel_pruefung.json</c>, Teil "format").
/// Gerechnet wird in Codepunkten wie Python.
/// </summary>
public static partial class SpielPruefung
{
    /// <summary>dir(str) in Python 3.12 (Gegenprobe in G42 "format.attribute").</summary>
    public static readonly IReadOnlySet<string> StrAttribute = new HashSet<string>(StringComparer.Ordinal)
    {
        "__add__", "__class__", "__contains__", "__delattr__", "__dir__", "__doc__", "__eq__", "__format__", "__ge__",
        "__getattribute__", "__getitem__", "__getnewargs__", "__getstate__", "__gt__", "__hash__", "__init__",
        "__init_subclass__", "__iter__", "__le__", "__len__", "__lt__", "__mod__", "__mul__", "__ne__", "__new__",
        "__reduce__", "__reduce_ex__", "__repr__", "__rmod__", "__rmul__", "__setattr__", "__sizeof__", "__str__",
        "__subclasshook__", "capitalize", "casefold", "center", "count", "encode", "endswith", "expandtabs", "find",
        "format", "format_map", "index", "isalnum", "isalpha", "isascii", "isdecimal", "isdigit", "isidentifier",
        "islower", "isnumeric", "isprintable", "isspace", "istitle", "isupper", "join", "ljust", "lower", "lstrip",
        "maketrans", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rjust", "rpartition",
        "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "translate", "upper",
        "zfill",
    };

    /// <summary>dir(int) in Python 3.12 (Gegenprobe in G42 "format.attribute").</summary>
    public static readonly IReadOnlySet<string> IntAttribute = new HashSet<string>(StringComparer.Ordinal)
    {
        "__abs__", "__add__", "__and__", "__bool__", "__ceil__", "__class__", "__delattr__", "__dir__", "__divmod__",
        "__doc__", "__eq__", "__float__", "__floor__", "__floordiv__", "__format__", "__ge__", "__getattribute__",
        "__getnewargs__", "__getstate__", "__gt__", "__hash__", "__index__", "__init__", "__init_subclass__",
        "__int__", "__invert__", "__le__", "__lshift__", "__lt__", "__mod__", "__mul__", "__ne__", "__neg__",
        "__new__", "__or__", "__pos__", "__pow__", "__radd__", "__rand__", "__rdivmod__", "__reduce__",
        "__reduce_ex__", "__repr__", "__rfloordiv__", "__rlshift__", "__rmod__", "__rmul__", "__ror__", "__round__",
        "__rpow__", "__rrshift__", "__rshift__", "__rsub__", "__rtruediv__", "__rxor__", "__setattr__", "__sizeof__",
        "__str__", "__sub__", "__subclasshook__", "__truediv__", "__trunc__", "__xor__", "as_integer_ratio",
        "bit_count", "bit_length", "conjugate", "denominator", "from_bytes", "imag", "is_integer", "numerator",
        "real", "to_bytes",
    };

    /// <summary>Breite oder Genauigkeit darueber: nicht nachgebildet (Python legt sehr lange Texte an oder scheitert am Speicher).</summary>
    private const long GrosseBreite = 10_000_000;

    /// <summary>In verschachtelten Feldern wird der Text wirklich gebaut; darueber nicht nachgebildet.</summary>
    private const long GrosseBreiteInnen = 10_000;

    /// <summary>text.format(**werte): Werte sind Text (string) oder ganze Zahlen (long).</summary>
    public static FormatUrteil FormatPruefen(string text, IReadOnlyDictionary<string, object> werte)
    {
        try
        {
            new Formatierer(werte).Bauen(Runen(text), 2);
            return FormatUrteil.Geht;
        }
        catch (FormatFehler)
        {
            return FormatUrteil.Fehler;
        }
        catch (FormatAbbruch)
        {
            return FormatUrteil.Abbruch;
        }
        catch (FormatNichtNachgebildet)
        {
            return FormatUrteil.NichtNachgebildet;
        }
    }

    /// <summary>
    /// try: text.format(...) except (KeyError, IndexError, ValueError): Problem. false = Problem melden;
    /// eine andere Ausnahme laesst die Python-Pruefung abbrechen, hier ebenso. Kein Text (Zahl, None): AttributeError.
    /// </summary>
    private static bool Format(object? text, params (string Name, object Wert)[] werte)
    {
        var urteil = FormatPruefen(text as string ?? throw new InvalidDataException("Text erwartet (Python: AttributeError)"),
            werte.ToDictionary(w => w.Name, w => w.Wert, StringComparer.Ordinal));
        return urteil switch
        {
            FormatUrteil.Geht => true,
            FormatUrteil.Fehler => false,
            _ => throw new InvalidDataException("Platzhalter laesst die Pruefung abbrechen (" + urteil + "): " + Anfang(text)),
        };
    }

    private static int[] Runen(string text) => text.EnumerateRunes().Select(r => r.Value).ToArray();

    private static string RunenText(int[] runen, int von, int bis)
    {
        var b = new StringBuilder();
        for (var i = von; i < bis; i++)
        {
            b.Append(new Rune(runen[i]).ToString());
        }
        return b.ToString();
    }

    private static int Ziffer(int rune) => CharUnicodeInfo.GetDecimalDigitValue(new Rune(rune).ToString(), 0);

    private sealed class FormatFehler : Exception;

    private sealed class FormatAbbruch : Exception;

    private sealed class FormatNichtNachgebildet : Exception;

    private enum Art { Text, Zahl, Objekt }

    /// <summary>Ein Wert beim Formatieren; Text null = Inhalt nicht nachgebildet (__doc__).</summary>
    private readonly record struct Wert(Art Art, string? Text, long Zahl);

    private sealed record Angabe(int? Fuellung, int? Ausrichtung, int? Vorzeichen, bool Alternativ, bool Null, long Breite,
        int? Tausend, long? Genau, int Typ, bool Gleitkomma);

    private sealed class Formatierer(IReadOnlyDictionary<string, object> werte)
    {
        /// <summary>build_string: Text mit Feldern; Rueckgabe der fertige Text (null, wenn nicht nachgebildet).</summary>
        public List<int>? Bauen(int[] t, int tiefe)
        {
            if (tiefe <= 0)
            {
                throw new FormatFehler();     // Max string recursion exceeded
            }
            var aus = new List<int>();
            var bekannt = true;
            var i = 0;
            var n = t.Length;
            while (i < n)
            {
                var c = t[i];
                if (c == '}')
                {
                    if (i + 1 < n && t[i + 1] == '}')
                    {
                        aus.Add('}');
                        i += 2;
                        continue;
                    }
                    throw new FormatFehler();     // Single '}' encountered in format string
                }
                if (c != '{')
                {
                    aus.Add(c);
                    i++;
                    continue;
                }
                if (i + 1 < n && t[i + 1] == '{')
                {
                    aus.Add('{');
                    i += 2;
                    continue;
                }
                if (i + 1 >= n)
                {
                    throw new FormatFehler();     // Single '{' encountered in format string
                }
                i++;
                var start = i;
                int? ende = null;
                while (i < n)
                {
                    c = t[i++];
                    if (c == '[')
                    {
                        while (i < n && t[i] != ']')
                        {
                            i++;
                        }
                        if (i < n)
                        {
                            i++;
                        }
                        continue;
                    }
                    if (c == '{')
                    {
                        throw new FormatFehler();     // unexpected '{' in field name
                    }
                    if (c is '}' or ':' or '!')
                    {
                        ende = c;
                        break;
                    }
                }
                if (ende is null)
                {
                    throw new FormatFehler();     // expected '}' before end of string
                }
                var name = t[start..(i - 1)];
                int? umwandlung = null;
                var angabe = Array.Empty<int>();
                var aufbauen = false;
                if (ende == '!')
                {
                    if (i >= n)
                    {
                        throw new FormatFehler();     // end of string while looking for conversion specifier
                    }
                    umwandlung = t[i++];
                    if (i >= n)
                    {
                        throw new FormatFehler();     // unmatched '{' in format spec
                    }
                    c = t[i++];
                    if (c == ':')
                    {
                        ende = ':';
                    }
                    else if (c != '}')
                    {
                        throw new FormatFehler();     // expected ':' after conversion specifier
                    }
                }
                if (ende == ':')
                {
                    var stufe = 1;
                    var s0 = i;
                    while (i < n)
                    {
                        c = t[i++];
                        if (c == '{')
                        {
                            stufe++;
                            aufbauen = true;
                        }
                        else if (c == '}' && --stufe == 0)
                        {
                            break;
                        }
                    }
                    if (stufe != 0)
                    {
                        throw new FormatFehler();     // unmatched '{' in format spec
                    }
                    angabe = t[s0..(i - 1)];
                }
                var wert = FeldObjekt(name);
                if (umwandlung is { } u)
                {
                    wert = Umwandeln(wert, u);
                }
                if (aufbauen)
                {
                    angabe = Bauen(angabe, tiefe - 1)?.ToArray() ?? throw new FormatNichtNachgebildet();
                }
                var teile = AngabePruefen(wert.Art, angabe);
                if (bekannt && Darstellen(wert, teile) is { } stueck)
                {
                    aus.AddRange(stueck);
                }
                else
                {
                    bekannt = false;
                }
            }
            return bekannt ? aus : null;
        }

        /// <summary>get_integer: nur Dezimalziffern (auch Unicode), sonst null; Ueberlauf ist ValueError.</summary>
        private static long? GanzeZahl(int[] t, int von, int bis)
        {
            if (bis <= von)
            {
                return null;
            }
            long w = 0;
            for (var i = von; i < bis; i++)
            {
                var z = Ziffer(t[i]);
                if (z < 0)
                {
                    return null;
                }
                if (w > (long.MaxValue - z) / 10)
                {
                    throw new FormatFehler();     // Too many decimal digits in format string
                }
                w = w * 10 + z;
            }
            return w;
        }

        /// <summary>get_field_object: erster Teil aus den Werten, dann .attribut und [schluessel] der Reihe nach.</summary>
        private Wert FeldObjekt(int[] name)
        {
            var n = name.Length;
            var i = 0;
            while (i < n && name[i] != '.' && name[i] != '[')
            {
                i++;
            }
            if (i == 0 || GanzeZahl(name, 0, i) is not null)
            {
                throw new FormatFehler();     // IndexError: keine Platz-Argumente
            }
            if (!werte.TryGetValue(RunenText(name, 0, i), out var roh))
            {
                throw new FormatFehler();     // KeyError
            }
            var wert = roh is string s ? new Wert(Art.Text, s, 0) : new Wert(Art.Zahl, null, Convert.ToInt64(roh, CultureInfo.InvariantCulture));
            while (i < n)
            {
                if (name[i] == '.')
                {
                    var j = i + 1;
                    while (j < n && name[j] != '.' && name[j] != '[')
                    {
                        j++;
                    }
                    if (j == i + 1)
                    {
                        throw new FormatFehler();     // Empty attribute in format string
                    }
                    wert = Attribut(wert, RunenText(name, i + 1, j));
                    i = j;
                    continue;
                }
                var k = i + 1;
                while (k < n && name[k] != ']')
                {
                    k++;
                }
                if (k >= n)
                {
                    throw new FormatFehler();     // Missing ']' in format string
                }
                if (k == i + 1)
                {
                    throw new FormatFehler();     // Empty attribute in format string
                }
                wert = Index(wert, name, i + 1, k);
                i = k + 1;
                if (i < n && name[i] != '.' && name[i] != '[')
                {
                    throw new FormatFehler();     // Only '.' or '[' may follow ']' in format field specifier
                }
            }
            return wert;
        }

        private static Wert Attribut(Wert wert, string name) => wert.Art switch
        {
            Art.Text when !StrAttribute.Contains(name) => throw new FormatAbbruch(),     // AttributeError
            Art.Text => name == "__doc__" ? new Wert(Art.Text, null, 0) : new Wert(Art.Objekt, null, 0),
            Art.Zahl when !IntAttribute.Contains(name) => throw new FormatAbbruch(),     // AttributeError
            Art.Zahl => name switch
            {
                "__doc__" => new Wert(Art.Text, null, 0),
                "real" or "numerator" => wert,
                "imag" => new Wert(Art.Zahl, null, 0),
                "denominator" => new Wert(Art.Zahl, null, 1),
                _ => new Wert(Art.Objekt, null, 0),
            },
            _ => throw new FormatNichtNachgebildet(),
        };

        /// <summary>wert[schluessel]: nur Text mit ganzer Zahl; sonst TypeError.</summary>
        private static Wert Index(Wert wert, int[] name, int von, int bis)
        {
            if (wert.Art != Art.Text)
            {
                if (wert.Art == Art.Zahl)
                {
                    throw new FormatAbbruch();     // 'int' object is not subscriptable
                }
                throw new FormatNichtNachgebildet();
            }
            var k = GanzeZahl(name, von, bis) ?? throw new FormatAbbruch();     // string indices must be integers
            if (wert.Text is null)
            {
                throw new FormatNichtNachgebildet();
            }
            var runen = Runen(wert.Text);
            return k < runen.Length ? new Wert(Art.Text, new Rune(runen[k]).ToString(), 0) : throw new FormatFehler();
        }

        private static Wert Umwandeln(Wert wert, int art)
        {
            if (art is not ('r' or 's' or 'a'))
            {
                throw new FormatFehler();     // Unknown conversion specifier
            }
            if (wert.Art == Art.Objekt || (wert.Art == Art.Text && wert.Text is null))
            {
                return new Wert(Art.Text, null, 0);
            }
            if (wert.Art == Art.Zahl)
            {
                return new Wert(Art.Text, wert.Zahl.ToString(CultureInfo.InvariantCulture), 0);
            }
            return art == 's' ? wert : new Wert(Art.Text, ReprOderNichts(wert.Text!), 0);
        }

        /// <summary>repr()/ascii() nur fuer einfache Texte (druckbares ASCII ohne Anfuehrungszeichen und \); sonst nicht nachgebildet.</summary>
        private static string? ReprOderNichts(string text) =>
            text.All(c => c is >= ' ' and <= '~' && c != '\'' && c != '\\') ? "'" + text + "'" : null;

        private static Angabe? AngabePruefen(Art art, int[] s)
        {
            if (s.Length == 0)
            {
                return null;
            }
            if (art == Art.Objekt)
            {
                throw new FormatAbbruch();     // TypeError: unsupported format string passed to ...__format__
            }
            var n = s.Length;
            var i = 0;
            int? fuellung = null;
            int? ausrichtung = null;
            if (n >= 2 && s[1] is '<' or '>' or '=' or '^')
            {
                fuellung = s[0];
                ausrichtung = s[1];
                i = 2;
            }
            else if (s[0] is '<' or '>' or '=' or '^')
            {
                ausrichtung = s[0];
                i = 1;
            }
            int? vorzeichen = null;
            if (i < n && s[i] is '+' or '-' or ' ')
            {
                vorzeichen = s[i++];
            }
            var nullMinus = false;
            if (i < n && s[i] == 'z')
            {
                nullMinus = true;
                i++;
            }
            var alternativ = false;
            if (i < n && s[i] == '#')
            {
                alternativ = true;
                i++;
            }
            var nullFlagge = false;
            if (fuellung is null && i < n && s[i] == '0')
            {
                nullFlagge = true;
                if (ausrichtung is null && art == Art.Zahl)
                {
                    ausrichtung = '=';
                }
                i++;
            }
            var j = i;
            while (j < n && Ziffer(s[j]) >= 0)
            {
                j++;
            }
            var breite = GanzeZahl(s, i, j) ?? 0;
            i = j;
            int? tausend = null;
            if (i < n && s[i] == ',')
            {
                tausend = ',';
                i++;
            }
            if (i < n && s[i] == '_')
            {
                if (tausend is not null)
                {
                    throw new FormatFehler();     // Cannot specify both ',' and '_'.
                }
                tausend = '_';
                i++;
            }
            if (i < n && s[i] == ',')
            {
                throw new FormatFehler();     // Cannot specify both ',' and '_'. / Cannot specify ',' with ','.
            }
            long? genau = null;
            if (i < n && s[i] == '.')
            {
                i++;
                j = i;
                while (j < n && Ziffer(s[j]) >= 0)
                {
                    j++;
                }
                if (j == i)
                {
                    throw new FormatFehler();     // Format specifier missing precision
                }
                genau = GanzeZahl(s, i, j);
                i = j;
            }
            if (n - i > 1)
            {
                throw new FormatFehler();     // Invalid format specifier
            }
            var typ = n - i == 1 ? s[i] : art == Art.Text ? 's' : 'd';
            if (breite > GrosseBreite || (genau ?? 0) > GrosseBreite)
            {
                throw new FormatNichtNachgebildet();
            }
            if (tausend is not null && !(typ is 'd' or 'e' or 'f' or 'g' or 'E' or 'G' or '%' or 'F'
                || (typ is 'b' or 'o' or 'x' or 'X' && tausend == '_')))
            {
                throw new FormatFehler();     // Cannot specify ',' with 'x'.
            }
            var teile = new Angabe(fuellung, ausrichtung, vorzeichen, alternativ, nullFlagge, breite, tausend, genau, typ, false);
            if (art == Art.Text)
            {
                if (typ != 's' || vorzeichen is not null || nullMinus || alternativ || ausrichtung == '=')
                {
                    throw new FormatFehler();     // Unknown format code / ... not allowed in string format specifier
                }
                return teile;
            }
            if (typ is 'b' or 'c' or 'd' or 'o' or 'x' or 'X' or 'n')
            {
                if (genau is not null || nullMinus || (typ == 'c' && (vorzeichen is not null || alternativ)))
                {
                    throw new FormatFehler();     // ... not allowed in integer format specifier
                }
                return teile;
            }
            return typ is 'e' or 'E' or 'f' or 'F' or 'g' or 'G' or '%' ? teile with { Gleitkomma = true } : throw new FormatFehler();
        }

        /// <summary>Der formatierte Text eines Felds (nur noetig in verschachtelten Feldern); null = nicht nachgebildet.</summary>
        private static List<int>? Darstellen(Wert wert, Angabe? a)
        {
            if (wert.Art == Art.Objekt || (wert.Art == Art.Text && wert.Text is null))
            {
                return null;
            }
            if (a is null)
            {
                return [.. Runen(wert.Art == Art.Text ? wert.Text! : wert.Zahl.ToString(CultureInfo.InvariantCulture))];
            }
            if (a.Gleitkomma || a.Breite > GrosseBreiteInnen || (a.Null && a.Tausend is not null))
            {
                return null;
            }
            var fuellung = a.Fuellung ?? (a.Null ? '0' : ' ');
            int ausrichtung;
            List<int> text;
            var vorn = new List<int>();
            if (wert.Art == Art.Text)
            {
                var runen = Runen(wert.Text!);
                text = [.. a.Genau is { } g && g < runen.Length ? runen[..(int)g] : runen];
                ausrichtung = a.Ausrichtung ?? '<';
            }
            else
            {
                var v = wert.Zahl;
                if (a.Typ == 'c')
                {
                    text = [(int)v];
                }
                else
                {
                    var basis = a.Typ switch { 'b' => 2, 'o' => 8, 'x' or 'X' => 16, _ => 10 };
                    var ziffern = basis == 10 ? Math.Abs(v).ToString(CultureInfo.InvariantCulture) : Convert.ToString(Math.Abs(v), basis);
                    if (a.Typ == 'X')
                    {
                        ziffern = ziffern.ToUpperInvariant();
                    }
                    if (a.Tausend is { } trenner)
                    {
                        var gruppe = basis == 10 ? 3 : 4;
                        var stuecke = new List<string>();
                        while (ziffern.Length > gruppe)
                        {
                            stuecke.Insert(0, ziffern[^gruppe..]);
                            ziffern = ziffern[..^gruppe];
                        }
                        ziffern = string.Join((char)trenner, stuecke.Prepend(ziffern));
                    }
                    text = [.. Runen(ziffern)];
                }
                if (v < 0)
                {
                    vorn.Add('-');
                }
                else if (a.Vorzeichen is '+' or ' ')
                {
                    vorn.Add(a.Vorzeichen.Value);
                }
                if (a.Alternativ && a.Typ is 'b' or 'o' or 'x' or 'X')
                {
                    vorn.AddRange(['0', a.Typ == 'X' ? 'X' : a.Typ]);
                }
                ausrichtung = a.Ausrichtung ?? (a.Null ? '=' : '>');
            }
            var rest = (int)a.Breite - vorn.Count - text.Count;
            if (rest <= 0)
            {
                return [.. vorn, .. text];
            }
            var links = ausrichtung switch { '<' => 0, '^' => rest / 2, _ => rest };
            var fuell = Enumerable.Repeat(fuellung, rest).ToList();
            return ausrichtung == '='
                ? [.. vorn, .. fuell, .. text]
                : [.. fuell.Take(links), .. vorn, .. text, .. fuell.Skip(links)];
        }
    }
}
