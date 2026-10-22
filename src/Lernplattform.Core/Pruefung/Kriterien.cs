using System.Globalization;
using System.Text;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Core.Pruefung;

/// <summary>Eine Teilaufgabe der Pruefung: Aufgabentext, Punkte, Musterloesung, Kriterien.</summary>
public sealed record Teilaufgabe(string Text, int Punkte, string Loesung, IReadOnlyList<string> Kriterien);

/// <summary>Aufbereitete Aufgabe (scenario_parts).</summary>
public sealed record Aufgabe(string Titel, string Thema, string Einleitung, IReadOnlyList<Teilaufgabe> Teile);

/// <summary>
/// Phase 4, W2: Aufgaben fuer die Pruefung aufbereiten wie fisi_pruefung v0.63.2 (split_numbered,
/// subtask_points, criteria, scenario_parts), Vergleichswerte G32 (kriterien) und G36. Die
/// regulaeren Ausdruecke von Python sind von Hand nachgebaut, mit Pythons Zeichenklassen
/// (Leerraum wie str.isspace, \d nur Dezimalziffern, Buchstaben fuer [^\W\d_]); .NET-\w und
/// .NET-\s weichen davon ab (Kontrollfehler "Satztrennung mit .NET-\w").
/// </summary>
public static class Kriterien
{
    public const int Aufgabenpunkte = 25;

    /// <summary>_ABBREVIATIONS in dieser Reihenfolge (ersetzt ohne Wortgrenzen, wie str.replace).</summary>
    public static readonly IReadOnlyList<string> Abkuerzungen =
    [
        "z.B.", "z. B.", "d.h.", "d. h.", "ca.", "bzw.", "u.a.", "usw.", "ggf.", "inkl.", "Nr.", "evtl.", "vgl.",
        "bspw.", "max.", "min.", "o.ä.", "u.U.", "Abs.", "Std.", "Min.", "Mio.", "Tsd.", "etc.", "z.T.",
    ];

    /// <summary>split_numbered: (Einleitung, Teile) aus Zeilen mit "1. ...", "2. ...".</summary>
    public static (string Einleitung, List<string> Teile) Aufteilen(string zeilen)
    {
        var einleitung = new List<string>();
        var teile = new List<List<string>>();
        foreach (var zeile in zeilen.Split('\n'))
        {
            var ende = NummerEnde(zeile);
            if (ende >= 0)
            {
                teile.Add([zeile[ende..]]);
            }
            else if (teile.Count > 0)
            {
                teile[^1].Add(zeile);
            }
            else
            {
                einleitung.Add(zeile);
            }
        }
        return (PythonZeichen.Strip(string.Join("\n", einleitung)),
            teile.Select(t => PythonZeichen.Strip(string.Join("\n", t))).ToList());
    }

    /// <summary>_SUBTASK = ^\s*(\d+)\.\s* : Ende des Treffers (UTF-16-Stelle) oder -1.</summary>
    private static int NummerEnde(string zeile)
    {
        var i = UeberspringeLeer(zeile, 0);
        var ziffern = 0;
        while (i < zeile.Length && Rune.TryGetRuneAt(zeile, i, out var r) && PythonZeichen.Dezimalwert(r.Value) >= 0)
        {
            i += r.Utf16SequenceLength;
            ziffern++;
        }
        if (ziffern == 0 || i >= zeile.Length || zeile[i] != '.')
        {
            return -1;
        }
        return UeberspringeLeer(zeile, i + 1);
    }

    private static int UeberspringeLeer(string text, int i)
    {
        while (i < text.Length && Rune.TryGetRuneAt(text, i, out var r) && PythonZeichen.IstLeer(r.Value))
        {
            i += r.Utf16SequenceLength;
        }
        return i;
    }

    /// <summary>subtask_points: Punkte je Teilaufgabe, Summe gesamt (3 Teile: 9/8/8).</summary>
    public static List<int> Teilpunkte(int anzahl, int gesamt = Aufgabenpunkte)
    {
        var (basis, rest) = (gesamt / anzahl, gesamt % anzahl);
        return Enumerable.Range(0, anzahl).Select(i => basis + (i < rest ? 1 : 0)).ToList();
    }

    /// <summary>criteria: jeder Satz bzw. jede Aufzaehlungszeile einer Musterloesung ein Kriterium.</summary>
    public static List<string> Aus(string text)
    {
        var liste = new List<string>();
        foreach (var roh in text.Split('\n'))
        {
            var zeile = PythonZeichen.Strip(OhnePunktzeichen(PythonZeichen.Strip(roh)));
            if (zeile.Length == 0)
            {
                continue;
            }
            var geschuetzt = GrossesBeispiel(zeile);
            for (var n = 0; n < Abkuerzungen.Count; n++)
            {
                geschuetzt = geschuetzt.Replace(Abkuerzungen[n], Marke(n), StringComparison.Ordinal);
            }
            geschuetzt = Dezimalpunkte(geschuetzt);
            foreach (var stueck in Saetze(geschuetzt))
            {
                var satz = stueck;
                for (var n = 0; n < Abkuerzungen.Count; n++)
                {
                    satz = satz.Replace(Marke(n), Abkuerzungen[n], StringComparison.Ordinal);
                }
                satz = PythonZeichen.Strip(satz.Replace('\x01', '.').Replace('\x02', '.'));
                if (PythonZeichen.Laenge(satz) > 2)
                {
                    liste.Add(satz);
                }
            }
        }
        return liste.Count > 0 ? liste : [PythonZeichen.Strip(text)];
    }

    private static string Marke(int n) => "\0" + n.ToString(CultureInfo.InvariantCulture) + "\0";

    /// <summary>_BULLET = ^[-•*]+(?=\s|$): Aufzaehlungszeichen nur vor Leerraum oder Zeilenende.</summary>
    private static string OhnePunktzeichen(string zeile)
    {
        var i = 0;
        while (i < zeile.Length && zeile[i] is '-' or '•' or '*')
        {
            i++;
        }
        if (i == 0)
        {
            return zeile;
        }
        if (i == zeile.Length || (Rune.TryGetRuneAt(zeile, i, out var r) && PythonZeichen.IstLeer(r.Value)))
        {
            return zeile[i..];
        }
        return zeile;
    }

    /// <summary>[^\W\d_] in Python: Buchstabe (L*) oder Zahlzeichen ohne Dezimalziffer (Nl, No).</summary>
    private static bool IstBuchstabe(Rune r) => Rune.GetUnicodeCategory(r) is UnicodeCategory.UppercaseLetter
        or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
        or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber;

    /// <summary>_CAPITAL_EXAMPLE = (?&lt;![^\W\d_])Z\.( ?)B\. wird Z\x02 + Gruppe 1 + B\x02.</summary>
    private static string GrossesBeispiel(string zeile)
    {
        var text = new StringBuilder(zeile.Length);
        var i = 0;
        while (i < zeile.Length)
        {
            if (zeile[i] == 'Z' && i + 1 < zeile.Length && zeile[i + 1] == '.' && (i == 0 || !VorherBuchstabe(zeile, i)))
            {
                var j = i + 2;
                var leer = j < zeile.Length && zeile[j] == ' ';
                if (leer)
                {
                    j++;
                }
                if (j + 1 < zeile.Length && zeile[j] == 'B' && zeile[j + 1] == '.')
                {
                    text.Append("Z\x02").Append(leer ? " " : "").Append("B\x02");
                    i = j + 2;
                    continue;
                }
            }
            text.Append(zeile[i]);
            i++;
        }
        return text.ToString();
    }

    private static bool VorherBuchstabe(string text, int i)
    {
        var start = char.IsLowSurrogate(text[i - 1]) && i >= 2 ? i - 2 : i - 1;
        return Rune.TryGetRuneAt(text, start, out var r) && IstBuchstabe(r);
    }

    /// <summary>re.sub(r"(\d)\.(\d)", "\1\x01\2"): Treffer ueberlappen nicht.</summary>
    private static string Dezimalpunkte(string zeile)
    {
        var punkte = PythonZeichen.Codepunkte(zeile);
        var text = new StringBuilder(zeile.Length);
        var i = 0;
        while (i < punkte.Length)
        {
            if (i + 2 < punkte.Length && PythonZeichen.Dezimalwert(punkte[i]) >= 0 && punkte[i + 1] == '.'
                && PythonZeichen.Dezimalwert(punkte[i + 2]) >= 0)
            {
                text.Append(char.ConvertFromUtf32(punkte[i])).Append('\x01').Append(char.ConvertFromUtf32(punkte[i + 2]));
                i += 3;
                continue;
            }
            text.Append(char.ConvertFromUtf32(punkte[i]));
            i++;
        }
        return text.ToString();
    }

    /// <summary>re.split(r"(?&lt;=[.!?])\s+(?=[A-ZÄÖÜ0-9„\"(])"): nur ganze Leerraum-Folgen nach Satzzeichen.</summary>
    private static List<string> Saetze(string zeile)
    {
        var stuecke = new List<string>();
        var anfang = 0;
        var i = 0;
        while (i < zeile.Length)
        {
            if (i > 0 && zeile[i - 1] is '.' or '!' or '?' && Rune.TryGetRuneAt(zeile, i, out var r) && PythonZeichen.IstLeer(r.Value))
            {
                var j = UeberspringeLeer(zeile, i);
                if (j < zeile.Length && zeile[j] is (>= 'A' and <= 'Z') or 'Ä' or 'Ö' or 'Ü' or (>= '0' and <= '9') or '„' or '"' or '(')
                {
                    stuecke.Add(zeile[anfang..i]);
                    anfang = j;
                    i = j;
                    continue;
                }
                i = j;
                continue;
            }
            i++;
        }
        stuecke.Add(zeile[anfang..]);
        return stuecke;
    }

    /// <summary>scenario_parts: null, wenn Aufgaben und Loesungen nicht zusammenpassen.</summary>
    public static Aufgabe? Teile(Lerninhalt szenario)
    {
        var (einleitung, aufgaben) = Aufteilen(szenario.Text("text") ?? "");
        var (_, loesungen) = Aufteilen(szenario.Text("solution") ?? "");
        if (aufgaben.Count == 0 || aufgaben.Count != loesungen.Count)
        {
            return null;
        }
        var punkte = Teilpunkte(aufgaben.Count);
        return new Aufgabe(szenario.Titel ?? "", szenario.SzenarioThema ?? "", einleitung,
            aufgaben.Select((a, i) => new Teilaufgabe(a, punkte[i], loesungen[i], Aus(loesungen[i]))).ToList());
    }
}
