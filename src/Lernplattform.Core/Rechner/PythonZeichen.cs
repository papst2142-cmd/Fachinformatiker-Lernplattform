using System.Globalization;
using System.Numerics;
using System.Text;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3, R1: Textregeln von Python 3.12, die die Rechner benutzen (Plan Phase 3, 2.2).
/// Laengen und Ausschnitte zaehlen Codepunkte wie len() und s[a:b] (nie string.Length,
/// ein Emoji ist ein Zeichen); Leerraum wie str.isspace() (auch \x1c-\x1f), Ziffern wie
/// str.isdigit() (auch "²"), casefold() und int() wie Python. Tabellen aus
/// PythonZeichen.Tabelle.cs (werkzeuge/tabellen/erzeuge_python_zeichen.py), Vergleichswerte G24.
/// </summary>
internal static partial class PythonZeichen
{
    // Eigene Klasse: die Tabellen (andere Datei der partiellen Klasse) sind dann sicher schon da
    private static class Mengen
    {
        public static readonly HashSet<int> Leer = [.. Leerraum];
        public static readonly HashSet<int> Ende = [.. Zeilenende];
        public static readonly Dictionary<int, string> Falt = BaueFalt();
    }

    private static Dictionary<int, string> BaueFalt()
    {
        var tabelle = new Dictionary<int, string>();
        for (var i = 0; i < FaltLaeufe.Length; i += 4)
        {
            for (var n = 0; n < FaltLaeufe[i + 1]; n++)
            {
                var zeichen = FaltLaeufe[i] + n * FaltLaeufe[i + 2];
                tabelle[zeichen] = char.ConvertFromUtf32(zeichen + FaltLaeufe[i + 3]);
            }
        }
        foreach (var (zeichen, gefaltet) in FaltMehrfach)
        {
            tabelle[zeichen] = gefaltet;
        }
        return tabelle;
    }

    private static bool InBereichen(int[] bereiche, int schritt, int zeichen)
    {
        for (var i = 0; i < bereiche.Length; i += schritt)
        {
            if (zeichen < bereiche[i])
            {
                return false;
            }
            if (zeichen <= bereiche[i + 1])
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>str.isspace() fuer ein Zeichen.</summary>
    public static bool IstLeer(int zeichen) => Mengen.Leer.Contains(zeichen);

    /// <summary>str.isdigit() fuer ein Zeichen.</summary>
    public static bool IstZiffer(int zeichen) => InBereichen(Ziffern, 2, zeichen);

    /// <summary>Wert einer Dezimalziffer (str.isdecimal()), sonst -1.</summary>
    public static int Dezimalwert(int zeichen)
    {
        for (var i = 0; i < Dezimal.Length; i += 3)
        {
            if (zeichen < Dezimal[i])
            {
                return -1;
            }
            if (zeichen <= Dezimal[i + 1])
            {
                return Dezimal[i + 2] + zeichen - Dezimal[i];
            }
        }
        return -1;
    }

    /// <summary>Zeichen, an dem str.splitlines() trennt.</summary>
    public static bool IstZeilenende(int zeichen) => Mengen.Ende.Contains(zeichen);

    /// <summary>Codepunkte eines Textes (Ersatzzeichen einzeln wie in Python nicht moeglich: Paare zaehlen als eins).</summary>
    public static int[] Codepunkte(string text)
    {
        var ergebnis = new List<int>(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            ergebnis.Add(rune.Value);
        }
        return [.. ergebnis];
    }

    /// <summary>len(text).</summary>
    public static int Laenge(string text)
    {
        var laenge = text.Length;
        foreach (var c in text)
        {
            if (char.IsLowSurrogate(c))
            {
                laenge--;
            }
        }
        return laenge;
    }

    /// <summary>text[von:bis] mit Codepunkten (von, bis werden wie in Python auf 0..len begrenzt; negativ zaehlt vom Ende).</summary>
    public static string Teil(string text, int von, int? bis = null)
    {
        var laenge = Laenge(text);
        if (laenge == text.Length)
        {
            var (a, b) = Grenzen(laenge, von, bis);
            return b > a ? text[a..b] : "";
        }
        var punkte = Codepunkte(text);
        var (x, y) = Grenzen(punkte.Length, von, bis);
        var teil = new StringBuilder();
        for (var i = x; i < y; i++)
        {
            teil.Append(char.ConvertFromUtf32(punkte[i]));
        }
        return teil.ToString();
    }

    private static (int, int) Grenzen(int laenge, int von, int? bis)
    {
        var a = von < 0 ? Math.Max(0, laenge + von) : Math.Min(von, laenge);
        var e = bis ?? laenge;
        var b = e < 0 ? Math.Max(0, laenge + e) : Math.Min(e, laenge);
        return (a, b);
    }

    /// <summary>str.strip() ohne Argument.</summary>
    public static string Strip(string text) => RStrip(LStrip(text));

    /// <summary>str.lstrip() ohne Argument.</summary>
    public static string LStrip(string text)
    {
        var i = 0;
        while (i < text.Length && !char.IsSurrogate(text[i]) && IstLeer(text[i]))
        {
            i++;
        }
        return text[i..];
    }

    /// <summary>str.rstrip() ohne Argument.</summary>
    public static string RStrip(string text)
    {
        var e = text.Length;
        while (e > 0 && !char.IsSurrogate(text[e - 1]) && IstLeer(text[e - 1]))
        {
            e--;
        }
        return text[..e];
    }

    /// <summary>str.strip(zeichen) / lstrip / rstrip mit einer Zeichenmenge (nur BMP-Zeichen).</summary>
    public static string Strip(string text, string zeichen, bool links = true, bool rechts = true)
    {
        var a = 0;
        var e = text.Length;
        while (links && a < e && zeichen.Contains(text[a], StringComparison.Ordinal))
        {
            a++;
        }
        while (rechts && e > a && zeichen.Contains(text[e - 1], StringComparison.Ordinal))
        {
            e--;
        }
        return text[a..e];
    }

    /// <summary>str.split() ohne Trenner: an Leerraum, ohne leere Teile.</summary>
    public static List<string> Split(string text)
    {
        var teile = new List<string>();
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var leer = !char.IsSurrogate(text[i]) && IstLeer(text[i]);
            if (leer && start >= 0)
            {
                teile.Add(text[start..i]);
                start = -1;
            }
            else if (!leer && start < 0)
            {
                start = i;
            }
        }
        if (start >= 0)
        {
            teile.Add(text[start..]);
        }
        return teile;
    }

    /// <summary>str.splitlines() (ohne Zeilenenden).</summary>
    public static List<string> SplitLines(string text)
    {
        var zeilen = new List<string>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            if (!char.IsSurrogate(text[i]) && IstZeilenende(text[i]))
            {
                zeilen.Add(text[start..i]);
                i += text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                start = i;
            }
            else
            {
                i++;
            }
        }
        if (start < text.Length)
        {
            zeilen.Add(text[start..]);
        }
        return zeilen;
    }

    /// <summary>str.isdigit(): nicht leer und jedes Zeichen eine Ziffer.</summary>
    public static bool IstZiffernText(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }
        foreach (var rune in text.EnumerateRunes())
        {
            if (!IstZiffer(rune.Value))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>str.casefold().</summary>
    public static string Falten(string text)
    {
        var ergebnis = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (Mengen.Falt.TryGetValue(rune.Value, out var gefaltet))
            {
                ergebnis.Append(gefaltet);
            }
            else
            {
                ergebnis.Append(rune.ToString());
            }
        }
        return ergebnis.ToString();
    }

    /// <summary>str.ljust(breite) (Breite in Codepunkten).</summary>
    public static string Links(string text, int breite)
    {
        var fehlt = breite - Laenge(text);
        return fehlt > 0 ? text + new string(' ', fehlt) : text;
    }

    /// <summary>
    /// int(text) fuer einen Text: Leerraum aussen, ein Vorzeichen, Dezimalziffern jeder Schrift,
    /// einzelne "_" zwischen Ziffern. Sonst false (Python: ValueError).
    /// </summary>
    public static bool Ganzzahl(string text, out BigInteger wert)
    {
        wert = BigInteger.Zero;
        var rest = Strip(text);
        var negativ = false;
        if (rest.Length > 0 && (rest[0] == '+' || rest[0] == '-'))
        {
            negativ = rest[0] == '-';
            rest = rest[1..];
        }
        if (rest.Length == 0)
        {
            return false;
        }
        var ziffern = new StringBuilder();
        var vorher = '_';
        foreach (var rune in rest.EnumerateRunes())
        {
            if (rune.Value == '_')
            {
                if (vorher == '_')
                {
                    return false;
                }
                vorher = '_';
                continue;
            }
            var ziffer = Dezimalwert(rune.Value);
            if (ziffer < 0)
            {
                return false;
            }
            ziffern.Append((char)('0' + ziffer));
            vorher = '0';
        }
        if (vorher == '_')
        {
            return false;
        }
        wert = BigInteger.Parse(ziffern.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
        if (negativ)
        {
            wert = -wert;
        }
        return true;
    }
}
