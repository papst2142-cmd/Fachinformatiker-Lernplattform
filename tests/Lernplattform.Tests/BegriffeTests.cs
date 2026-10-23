using System.Text;
using System.Text.RegularExpressions;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W4 (Nachtrag Wortliste P4-11 Abschnitt 2, Sammelliste 57): jeder Begriff in „…“ der
/// geprueften Hilfe-Texte steht als Zeichenkette im Programm (wie test_rundgang_hilfe
/// test_genannte_begriffe_stehen_so_in_der_oberflaeche). Ausgenommen ist genau HilfeTexte.cs.
/// Liste A nennt die Begriffe noch nicht gebauter Bereiche; sie muss schrumpfen.
/// </summary>
public class BegriffeTests
{
    /// <summary>Genau eine ausgenommene Datei (relativ zu src/).</summary>
    public const string Ausgenommen = "Lernplattform.Core/Hilfe/HilfeTexte.cs";

    /// <summary>Liste A: Begriff -> Phase, in der er gebaut wird. Rot, sobald er als Text auftaucht.</summary>
    public static readonly IReadOnlyDictionary<string, int> NochNichtGebaut = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        // Phase 5 W1: "Sicherung erstellen", "Sicherung einspielen", "Alles ersetzen" gebaut (gestrichen)
        ["Jetzt aktualisieren"] = 5,
        ["Installieren"] = 5,
        ["Messung aufzeichnen"] = 5,
        ["leistungsmessung.csv"] = 5,
        ["Messdatei zeigen"] = 5,
        ["Messdatei löschen"] = 5,
    };

    /// <summary>Einziger Begriff, der keine Beschriftung ist (NOT_A_LABEL).</summary>
    public static readonly IReadOnlySet<string> KeineBeschriftung = new HashSet<string>(StringComparer.Ordinal) { "Anführungszeichen" };

    /// <summary>Die geprueften Texte: 16 Themen, 5 Seiten, SETUP_DATES_HINT, HELP_INTRO, NAME_OPTION_HINT, N4, N7 je Fachrichtung.</summary>
    public static IReadOnlyList<string> GepruefteTexte()
    {
        var texte = new List<string>();
        texte.AddRange(HilfeTexte.Abschnitte.Select(a => a.Text));
        texte.AddRange(HilfeTexte.Seiten.Select(s => s.Text));
        texte.Add(HilfeTexte.TermineHinweis);
        texte.Add(HilfeTexte.Einleitung);
        texte.Add(HilfeTexte.NameHinweis);
        texte.Add(HilfeTexte.AusbildungFrage);
        foreach (var (wert, _) in PruefungTexte.Fachrichtungen)
        {
            if (AusbildungFrage.Abschnitt(wert) is not null)
            {
                texte.Add(AusbildungFrage.WarnungText(wert));
            }
        }
        return texte;
    }

    /// <summary>_quoted: alle Begriffe in „…“.</summary>
    public static IEnumerable<string> Begriffe(string text) =>
        Regex.Matches(text, "„([^“]+)“").Select(m => m.Groups[1].Value);

    [Fact]
    public void GenannteBegriffeGibtEs()
    {
        var texte = GepruefteTexte();
        Assert.Equal(16 + 5 + 4 + 3, texte.Count);
        var begriffe = texte.SelectMany(Begriffe).ToHashSet(StringComparer.Ordinal);
        Assert.True(begriffe.Count > 40, begriffe.Count.ToString());
        var zeichenketten = Zeichenketten();
        var fehlen = new List<string>();
        var gebaut = new List<string>();
        foreach (var begriff in begriffe.Order(StringComparer.Ordinal))
        {
            if (KeineBeschriftung.Contains(begriff))
            {
                continue;
            }
            var vorhanden = zeichenketten.Any(z => z.Contains(begriff, StringComparison.Ordinal));
            if (NochNichtGebaut.ContainsKey(begriff))
            {
                if (vorhanden)
                {
                    gebaut.Add(begriff);
                }
            }
            else if (!vorhanden)
            {
                fehlen.Add(begriff);
            }
        }
        Assert.True(fehlen.Count == 0, "Begriff ohne Text im Programm: " + string.Join(" | ", fehlen));
        Assert.True(gebaut.Count == 0, "Liste A: Begriff steht jetzt als Text, Eintrag streichen: " + string.Join(" | ", gebaut));
        // Liste A darf nur Begriffe nennen, die noch in der Hilfe stehen
        var veraltet = NochNichtGebaut.Keys.Where(b => !begriffe.Contains(b)).ToList();
        Assert.True(veraltet.Count == 0, "Liste A: Begriff steht nicht mehr in der Hilfe: " + string.Join(" | ", veraltet));
        Assert.Equal(6, NochNichtGebaut.Count);                  // Phase 5 W1: 9 -> 6 (drei Begriffe gebaut)
        Assert.All(NochNichtGebaut.Values, phase => Assert.Equal(5, phase));   // Weisung P5-3 Punkt 4: Phase 6 -> 5
    }

    /// <summary>Genau eine Datei ist ausgenommen, und sie enthaelt alle geprueften Texte (findet sich nicht selbst).</summary>
    [Fact]
    public void GenauEineDateiAusgenommen()
    {
        var src = Path.Combine(Pfade.Wurzel, "src");
        var alle = Dateien(src, mitAusnahme: true).ToList();
        var ohne = Dateien(src, mitAusnahme: false).ToList();
        Assert.Equal(alle.Count - 1, ohne.Count);
        Assert.Contains(Path.Combine(src, Ausgenommen.Replace('/', Path.DirectorySeparatorChar)), alle);
        // Begriffe, die nur in einer Kopie eines geprueften Textes stehen (wie Python: fisi_rechtliches), zaehlen nicht als Fund
        var zeichenketten = Zeichenketten();
        var anfaenge = GepruefteTexte().SelectMany(t => t.Split('\n')).Where(z => z.Length > 60).Select(z => z[..60]).ToList();
        var echt = zeichenketten.Where(z => !anfaenge.Any(a => z.Contains(a, StringComparison.Ordinal))).ToList();
        var nurKopie = GepruefteTexte().SelectMany(Begriffe).Distinct()
            .Where(b => !KeineBeschriftung.Contains(b) && !NochNichtGebaut.ContainsKey(b))
            .Where(b => !echt.Any(z => z.Contains(b, StringComparison.Ordinal))).Order(StringComparer.Ordinal).ToList();
        Assert.True(nurKopie.Count == 0, "Begriff nur in einer Textkopie: " + string.Join(" | ", nurKopie));
    }

    /// <summary>Der Zerleger: Kommentare zaehlen nicht, mit + verbundene Zeichenketten gelten als eine.</summary>
    [Fact]
    public void ZerlegerWieRegel()
    {
        var quelle = "// \"Kommentar eins\"\n/* \"Kommentar zwei\" */ var a = \"Sicherung \" + // Zwischenruf\n  \"erstellen\"; var b = 'x';"
            + " var c = @\"wört\"\"lich\"; var d = $\"Wert {x} Ende\\u00e4\"; var e = \"\"\"\n    roh \"Text\"\n    \"\"\";";
        var z = Zerlegen(quelle);
        Assert.Equal(["Sicherung erstellen", "wört\"lich", "Wert {x} Endeä", "roh \"Text\""], z);
        Assert.DoesNotContain(z, s => s.Contains("Kommentar", StringComparison.Ordinal));
    }

    private static IEnumerable<string> Dateien(string src, bool mitAusnahme)
    {
        var ausnahme = Path.Combine(src, Ausgenommen.Replace('/', Path.DirectorySeparatorChar));
        foreach (var datei in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var teile = Path.GetRelativePath(src, datei).Split(Path.DirectorySeparatorChar);
            if (teile.Contains("obj") || teile.Contains("bin"))
            {
                continue;
            }
            if (!mitAusnahme && datei == ausnahme)
            {
                continue;
            }
            yield return datei;
        }
    }

    private static List<string> Zeichenketten() =>
        Dateien(Path.Combine(Pfade.Wurzel, "src"), mitAusnahme: false).SelectMany(d => Zerlegen(File.ReadAllText(d))).ToList();

    /// <summary>
    /// Zerlegt C#-Quelltext: liefert alle Zeichenketten ("…", @"…", $"…", """…"""); Kommentare
    /// und Zeichen ('x') werden uebersprungen; Zeichenketten nur mit + und Leerraum/Kommentar dazwischen
    /// werden zusammengefuegt.
    /// </summary>
    internal static List<string> Zerlegen(string q)
    {
        var ergebnis = new List<string>();
        StringBuilder? laufend = null;   // zuletzt gelesene Zeichenkette, solange nur + und Leerraum folgen
        var plus = false;
        var i = 0;
        void Abschliessen()
        {
            if (laufend is not null)
            {
                ergebnis.Add(laufend.ToString());
            }
            laufend = null;
            plus = false;
        }
        void Anfuegen(string text)
        {
            if (laufend is not null && plus)
            {
                laufend.Append(text);
            }
            else
            {
                Abschliessen();
                laufend = new StringBuilder(text);
            }
            plus = false;
        }
        while (i < q.Length)
        {
            var c = q[i];
            if (c == '/' && i + 1 < q.Length && q[i + 1] == '/')
            {
                while (i < q.Length && q[i] != '\n')
                {
                    i++;
                }
                continue;
            }
            if (c == '/' && i + 1 < q.Length && q[i + 1] == '*')
            {
                var ende = q.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = ende < 0 ? q.Length : ende + 2;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            if (c == '+' && laufend is not null && !plus)
            {
                plus = true;
                i++;
                continue;
            }
            if (c == '\'')
            {
                Abschliessen();
                i++;
                while (i < q.Length && q[i] != '\'')
                {
                    i += q[i] == '\\' ? 2 : 1;
                }
                i++;
                continue;
            }
            // Vorsilben $ und @ (auch $@, @$, $$)
            var j = i;
            var wortlich = false;
            while (j < q.Length && (q[j] == '$' || q[j] == '@'))
            {
                wortlich |= q[j] == '@';
                j++;
            }
            if (j < q.Length && q[j] == '"' && (j > i || c == '"'))
            {
                if (j + 2 < q.Length && q[j + 1] == '"' && q[j + 2] == '"')
                {
                    // rohe Zeichenkette
                    var n = 0;
                    while (j + n < q.Length && q[j + n] == '"')
                    {
                        n++;
                    }
                    var zaun = new string('"', n);
                    var start = j + n;
                    var ende = q.IndexOf(zaun, start, StringComparison.Ordinal);
                    Anfuegen(RohText(q[start..ende]));
                    i = ende + n;
                    continue;
                }
                var text = new StringBuilder();
                j++;
                while (j < q.Length)
                {
                    if (wortlich)
                    {
                        if (q[j] == '"' && j + 1 < q.Length && q[j + 1] == '"')
                        {
                            text.Append('"');
                            j += 2;
                            continue;
                        }
                        if (q[j] == '"')
                        {
                            break;
                        }
                        text.Append(q[j++]);
                        continue;
                    }
                    if (q[j] == '\\')
                    {
                        j = Flucht(q, j, text);
                        continue;
                    }
                    if (q[j] == '"' || q[j] == '\n')
                    {
                        break;
                    }
                    text.Append(q[j++]);
                }
                Anfuegen(text.ToString());
                i = j + 1;
                continue;
            }
            Abschliessen();
            i++;
        }
        Abschliessen();
        return ergebnis;
    }

    private static int Flucht(string q, int j, StringBuilder text)
    {
        var z = q[j + 1];
        switch (z)
        {
            case 'n': text.Append('\n'); return j + 2;
            case 't': text.Append('\t'); return j + 2;
            case 'r': text.Append('\r'); return j + 2;
            case '0': text.Append('\0'); return j + 2;
            case 'u':
                text.Append((char)Convert.ToInt32(q.Substring(j + 2, 4), 16));
                return j + 6;
            case 'U':
                text.Append(char.ConvertFromUtf32(Convert.ToInt32(q.Substring(j + 2, 8), 16)));
                return j + 10;
            default:
                text.Append(z);
                return j + 2;
        }
    }

    /// <summary>Rohe Zeichenkette ueber mehrere Zeilen: Einrueckung der Schlusszeile entfernen.</summary>
    private static string RohText(string innen)
    {
        if (!innen.Contains('\n'))
        {
            return innen;
        }
        var zeilen = innen.Replace("\r", "").Split('\n').ToList();
        var einzug = zeilen[^1].Length;
        zeilen.RemoveAt(0);
        zeilen.RemoveAt(zeilen.Count - 1);
        return string.Join("\n", zeilen.Select(z => z.Length >= einzug ? z[einzug..] : z.TrimStart()));
    }
}
