using System.Text.RegularExpressions;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3, R1: Ergebnis- und Rechenweg-Texte auf eine Breite in Zeichen bringen, genau wie
/// Python v0.63.2 (fisi_core 3435-3832: _fit_line, _fit_lines, fit_lines und Helfer,
/// _wrap_para, _join_items, _pc_table, _handy_address). Alle Laengen zaehlen Codepunkte wie
/// Python len() (PythonZeichen.Laenge), nie string.Length. Vergleichswerte G24.
/// </summary>
public static partial class Zeilen
{
    /// <summary>Breite der Rechenwege und Ergebnisse am PC (CALC_EXPLAIN_PC_WIDTH).</summary>
    public const int PcBreite = RechnerTexte.CALC_EXPLAIN_PC_WIDTH;

    /// <summary>Breite am Handy (CALC_EXPLAIN_HANDY_WIDTH).</summary>
    public const int HandyBreite = RechnerTexte.CALC_EXPLAIN_HANDY_WIDTH;

    private static readonly string[] Bindewoerter = [.. RechnerTexte._FIT_BIND];

    [GeneratedRegex("^[0-9]{1,3}(\\.[0-9]{3})+[,;)]?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ZahlMitPunkten();

    [GeneratedRegex("^(?:%|[A-Za-z\u00c4\u00d6\u00dc\u00e4\u00f6\u00fc/]{1,4})[,;)]?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Einheit();

    [GeneratedRegex("^/[0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex PraefixAnfang();

    private static int L(string text) => PythonZeichen.Laenge(text);

    private static string Einzug(string zeile) => zeile[..(zeile.Length - zeile.TrimStart(' ').Length)];

    /// <summary>Erstes Zeichen ist eine Ziffer (word[:1].isdigit()).</summary>
    private static bool BeginntMitZiffer(string wort) =>
        wort.Length > 0 && PythonZeichen.IstZiffer(wort.EnumerateRunes().First().Value);

    /// <summary>Letztes Zeichen ist eine Ziffer (word[-1:].isdigit()).</summary>
    private static bool EndetMitZiffer(string wort)
    {
        if (wort.Length == 0)
        {
            return false;
        }
        var i = wort.Length - 1;
        var zeichen = char.IsLowSurrogate(wort[i]) && i > 0 && char.IsHighSurrogate(wort[i - 1])
            ? char.ConvertToUtf32(wort[i - 1], wort[i]) : wort[i];
        return PythonZeichen.IstZiffer(zeichen);
    }

    // ---------------------------------------------------------------- _fit_lines (Ergebnisse)

    /// <summary>_fit_pieces: Bruchstellen eines zu langen Wortes fuer _fit_line.</summary>
    internal static List<string> Stuecke(string wort)
    {
        if (ZahlMitPunkten().IsMatch(wort))
        {
            return VorPunkten(wort);
        }
        if (wort.Contains(':', StringComparison.Ordinal))
        {
            var stuecke = new List<string>();
            foreach (var stueck in BisZeichen(wort, ':'))
            {
                if (stuecke.Count > 0 && (stueck == ":" || stueck.StartsWith('/')))
                {
                    stuecke[^1] += stueck;
                }
                else
                {
                    stuecke.Add(stueck);
                }
            }
            return stuecke;
        }
        if (wort.Trim('-').Contains('-', StringComparison.Ordinal))
        {
            return BisZeichen(wort, '-');
        }
        return [wort];
    }

    /// <summary>re.split(r"(?=\.)", wort): vor jedem Punkt trennen (leerer Anfang bleibt).</summary>
    private static List<string> VorPunkten(string wort)
    {
        var teile = new List<string>();
        var start = 0;
        for (var i = 0; i < wort.Length; i++)
        {
            if (wort[i] == '.')
            {
                teile.Add(wort[start..i]);
                start = i;
            }
        }
        teile.Add(wort[start..]);
        return teile;
    }

    /// <summary>re.findall(r"[^x]*x|[^x]+", wort).</summary>
    private static List<string> BisZeichen(string wort, char trenner)
    {
        var teile = new List<string>();
        var start = 0;
        while (start < wort.Length)
        {
            var i = wort.IndexOf(trenner, start);
            if (i < 0)
            {
                teile.Add(wort[start..]);
                break;
            }
            teile.Add(wort[start..(i + 1)]);
            start = i + 1;
        }
        return teile;
    }

    /// <summary>_fit_line: eine zu lange Zeile umbrechen; Folgezeilen beginnen mit mehr.</summary>
    internal static List<string> ZeileUmbrechen(string zeile, int breite, string mehr)
    {
        var lead = Einzug(zeile);
        var teile = Regex.Split(PythonZeichen.Strip(zeile), "( +)");
        var aus = new List<string>();
        var aktuell = lead;
        var frisch = true;
        for (var n = 0; n < teile.Length; n += 2)
        {
            var wort = teile[n];
            var luecke = n == 0 ? "" : teile[n - 1];
            var kleber = frisch ? "" : luecke;
            if (L(aktuell + kleber + wort) <= breite)
            {
                aktuell += kleber + wort;
                frisch = false;
                continue;
            }
            if (L(mehr + wort) <= breite)
            {
                if (!frisch)
                {
                    aus.Add(aktuell);
                }
                aktuell = mehr + wort;
                frisch = false;
                continue;
            }
            var stuecke = Stuecke(wort);
            for (var nummer = 0; nummer < stuecke.Count; nummer++)
            {
                var stueck = stuecke[nummer];
                kleber = nummer == 0 ? (frisch ? "" : luecke) : "";
                if (L(aktuell + kleber + stueck) <= breite)
                {
                    aktuell += kleber + stueck;
                    frisch = false;
                    continue;
                }
                if (!frisch)
                {
                    aus.Add(aktuell);
                }
                aktuell = mehr + stueck;
                frisch = false;
                while (L(aktuell) > breite)
                {
                    aus.Add(PythonZeichen.Teil(aktuell, 0, breite));
                    aktuell = mehr + PythonZeichen.Teil(aktuell, breite);
                }
            }
        }
        aus.Add(aktuell);
        return aus;
    }

    /// <summary>_fit_lines: jede Zeile hoechstens breite Zeichen; kuerzere bleiben unveraendert.</summary>
    public static List<string> Einpassen(IEnumerable<string> zeilen, int breite)
    {
        var aus = new List<string>();
        foreach (var zeile in zeilen)
        {
            if (L(zeile) <= breite)
            {
                aus.Add(zeile);
            }
            else
            {
                aus.AddRange(ZeileUmbrechen(zeile, breite, Einzug(zeile) + " "));
            }
        }
        return aus;
    }

    /// <summary>_handy_fit.</summary>
    internal static List<string> HandyEinpassen(IEnumerable<string> zeilen) => Einpassen(zeilen, HandyBreite);

    // ---------------------------------------------------------------- fit_lines (Kasten)

    private sealed record Teile(string Einzug, string Beschriftung, string Wert);

    /// <summary>_fit_parts: "Beschriftung : Wert" zerlegen, sonst null.</summary>
    private static Teile? Zerlegen(string zeile)
    {
        var pos = zeile.IndexOf(": ", StringComparison.Ordinal);
        if (pos < 0)
        {
            return null;
        }
        var wert = PythonZeichen.Strip(zeile[(pos + 2)..]);
        if (wert.Length == 0)
        {
            return null;
        }
        var kopf = zeile[..pos];
        var beschriftung = PythonZeichen.Strip(kopf);
        var einzug = beschriftung.Length > 0 ? Einzug(kopf) : "";
        return new Teile(einzug, beschriftung, wert);
    }

    /// <summary>_fit_sentence: Satz an Leerzeichen umbrechen; Spalten und einzelne Woerter bleiben.</summary>
    internal static List<string> Satz(string zeile, int zeichen)
    {
        var koerper = zeile.TrimStart(' ');
        var einzug = zeile[..(zeile.Length - koerper.Length)];
        if (PythonZeichen.RStrip(koerper).Contains("  ", StringComparison.Ordinal)
            || !PythonZeichen.Strip(koerper).Contains(' ', StringComparison.Ordinal))
        {
            return [zeile];
        }
        var zeilen = new List<string>();
        var aktuell = "";
        var woerter = new List<string>();
        foreach (var wort in PythonZeichen.Split(koerper))
        {
            if (woerter.Count > 0 && woerter[^1].EndsWith("RFC", StringComparison.Ordinal) && BeginntMitZiffer(wort))
            {
                woerter[^1] += " " + wort;
            }
            else
            {
                woerter.Add(wort);
            }
        }
        foreach (var w in woerter)
        {
            var wort = w;
            if (aktuell.Length > 0 && L(einzug) + L(aktuell) + 1 + L(wort) > zeichen)
            {
                (aktuell, wort) = Loesen(aktuell, wort, zeichen - L(einzug));
                zeilen.Add(einzug + aktuell);
                aktuell = wort;
            }
            else
            {
                aktuell = aktuell.Length > 0 ? aktuell + " " + wort : wort;
            }
        }
        zeilen.Add(einzug + aktuell);
        return zeilen;
    }

    /// <summary>_fit_unbind: Bindewoerter ("=", "->", "→", "hex") am Zeilenende mit in die neue Zeile.</summary>
    private static (string, string) Loesen(string aktuell, string wort, int platz)
    {
        var kopf = aktuell;
        var schwanz = new List<string>();
        while (kopf.Contains(' ', StringComparison.Ordinal) && Bindewoerter.Contains(kopf[(kopf.LastIndexOf(' ') + 1)..]))
        {
            var i = kopf.LastIndexOf(' ');
            schwanz.Insert(0, kopf[(i + 1)..]);
            kopf = kopf[..i];
        }
        var neu = string.Join(" ", schwanz.Append(wort));
        if (schwanz.Count == 0 || L(neu) > platz)
        {
            return (aktuell, wort);
        }
        return (kopf, neu);
    }

    /// <summary>_fit_prose: Satz mit "(Nibble): /44 ..." bricht wie ein Satz um, sonst null.</summary>
    private static List<string>? Prosa(string zeile, int zeichen)
    {
        var pos = zeile.IndexOf(": ", StringComparison.Ordinal);
        if (pos <= 0 || zeile[pos - 1] == ' ' || PythonZeichen.Strip(zeile).Contains("  ", StringComparison.Ordinal))
        {
            return null;
        }
        if (PythonZeichen.Split(zeile[..pos]).Count < 4)
        {
            return null;
        }
        if (!zeile[..pos].EndsWith("(Nibble)", StringComparison.Ordinal) || !PraefixAnfang().IsMatch(zeile[(pos + 2)..]))
        {
            return null;
        }
        if (!PythonZeichen.Strip(zeile[(pos + 2)..]).Contains(' ', StringComparison.Ordinal))
        {
            return null;
        }
        var zeilen = Satz(zeile, zeichen);
        if (zeilen.Count < 2 || !zeilen[0].StartsWith(zeile[..(pos + 2)], StringComparison.Ordinal))
        {
            return null;
        }
        return zeilen;
    }

    /// <summary>_FIT_NUMBER: ^\d{1,3}(?:\.\d{3})+(?:,\d+)?$ mit Pythons \d (Unicode-Dezimalziffern).</summary>
    private static bool GrosseZahl(string wort)
    {
        var p = PythonZeichen.Codepunkte(wort);
        bool Ziffer(int i) => i < p.Length && PythonZeichen.Dezimalwert(p[i]) >= 0;
        var i = 0;
        while (i < 3 && Ziffer(i))
        {
            i++;
        }
        if (i == 0)
        {
            return false;
        }
        var gruppen = 0;
        while (i + 3 < p.Length && p[i] == '.' && Ziffer(i + 1) && Ziffer(i + 2) && Ziffer(i + 3))
        {
            i += 4;
            gruppen++;
        }
        if (gruppen == 0)
        {
            return false;
        }
        if (i < p.Length && p[i] == ',' && Ziffer(i + 1))
        {
            i++;
            while (Ziffer(i))
            {
                i++;
            }
        }
        return i == p.Length || (i == p.Length - 1 && p[i] == '\n');
    }

    /// <summary>_fit_value_pieces: IPv6 nach einem Doppelpunkt, grosse Zahl vor einem Tausenderpunkt.</summary>
    internal static List<string> WertStuecke(string wort)
    {
        if (wort.Contains(':', StringComparison.Ordinal))
        {
            // re.split(r"(?<=:)(?=[^:])", wort), leere Teile weg
            var teile = new List<string>();
            var start = 0;
            for (var i = 1; i < wort.Length; i++)
            {
                if (wort[i - 1] == ':' && wort[i] != ':')
                {
                    teile.Add(wort[start..i]);
                    start = i;
                }
            }
            teile.Add(wort[start..]);
            return [.. teile.Where(t => t.Length > 0)];
        }
        var gruppen = wort.Split('.');
        if (GrosseZahl(wort) && !(gruppen.Length == 4 && gruppen.All(g => Bis255(g))))
        {
            return [.. VorPunkten(wort).Where(t => t.Length > 0)];
        }
        return [wort];
    }

    /// <summary>int(gruppe) &lt;= 255; wie in Python bricht int() bei "890,5" ab (ValueError).</summary>
    private static bool Bis255(string gruppe)
    {
        if (!PythonZeichen.Ganzzahl(gruppe, out var wert))
        {
            throw new FormatException("invalid literal for int() with base 10: " + gruppe);
        }
        return wert <= 255;
    }

    /// <summary>_fit_balanced: Stuecke auf moeglichst wenige, moeglichst gleich lange Zeilen.</summary>
    private static List<string>? Ausgewogen(List<string> stuecke, int platz)
    {
        for (var anzahl = 1; anzahl <= stuecke.Count; anzahl++)
        {
            List<string>? beste = null;
            var laengste = int.MaxValue;
            foreach (var schnitte in Kombinationen(stuecke.Count - 1, anzahl - 1))
            {
                var grenzen = new List<int> { 0 };
                grenzen.AddRange(schnitte.Select(s => s + 1));
                grenzen.Add(stuecke.Count);
                var zeilen = new List<string>();
                for (var g = 0; g + 1 < grenzen.Count; g++)
                {
                    zeilen.Add(string.Concat(stuecke.Skip(grenzen[g]).Take(grenzen[g + 1] - grenzen[g])));
                }
                var lang = zeilen.Max(L);
                if (lang <= platz && (beste is null || lang < laengste))
                {
                    beste = zeilen;
                    laengste = lang;
                }
            }
            if (beste is not null)
            {
                return beste;
            }
        }
        return null;
    }

    /// <summary>itertools.combinations(range(n), k) in Pythons Reihenfolge.</summary>
    private static IEnumerable<int[]> Kombinationen(int n, int k)
    {
        if (k > n)
        {
            yield break;
        }
        var auswahl = Enumerable.Range(0, k).ToArray();
        while (true)
        {
            yield return [.. auswahl];
            var i = k - 1;
            while (i >= 0 && auswahl[i] == i + n - k)
            {
                i--;
            }
            if (i < 0)
            {
                yield break;
            }
            auswahl[i]++;
            for (var j = i + 1; j < k; j++)
            {
                auswahl[j] = auswahl[j - 1] + 1;
            }
        }
    }

    /// <summary>_fit_value: Wert auf Zeilen zu hoechstens platz Zeichen verteilen.</summary>
    internal static List<string> Wert(string wert, int platz)
    {
        if (platz <= 0 || L(wert) <= platz || wert.Contains("  ", StringComparison.Ordinal))
        {
            return [wert];
        }
        var woerter = new List<string>();
        foreach (var wort in wert.Split(' '))
        {
            if (woerter.Count > 0 && EndetMitZiffer(woerter[^1]) && Einheit().IsMatch(wort))
            {
                woerter[^1] += " " + wort;
            }
            else if (woerter.Count > 0 && woerter[^1].EndsWith("RFC", StringComparison.Ordinal) && BeginntMitZiffer(wort))
            {
                woerter[^1] += " " + wort;
            }
            else
            {
                woerter.Add(wort);
            }
        }
        if (woerter.Count == 1 && wert.Contains(':', StringComparison.Ordinal) && !wert.Contains(' ', StringComparison.Ordinal))
        {
            var ausgewogen = Ausgewogen(WertStuecke(wert), platz);
            if (ausgewogen is not null)
            {
                return ausgewogen;
            }
        }
        var zeilen = new List<string>();
        var aktuell = "";
        foreach (var wort in woerter)
        {
            var stuecke = L(wort) > platz ? WertStuecke(wort) : [wort];
            for (var nummer = 0; nummer < stuecke.Count; nummer++)
            {
                var stueck = stuecke[nummer];
                var kleber = nummer == 0 && aktuell.Length > 0 ? " " : "";
                if (aktuell.Length > 0 && L(aktuell) + kleber.Length + L(stueck) > platz)
                {
                    if (kleber.Length > 0)
                    {
                        (aktuell, stueck) = Loesen(aktuell, stueck, platz);
                    }
                    zeilen.Add(aktuell);
                    aktuell = stueck;
                }
                else
                {
                    aktuell += kleber + stueck;
                }
            }
        }
        zeilen.Add(aktuell);
        return zeilen;
    }

    /// <summary>
    /// fit_lines(text, zeichen, einheitlich): Text fuer einen Kasten mit zeichen Zeichen Breite.
    /// einheitlich (Handy): muss in einem Abschnitt eine Zeile umbrechen, stehen alle zweizeilig.
    /// </summary>
    public static string Kasten(string text, int zeichen, bool einheitlich = false)
    {
        if (string.IsNullOrEmpty(text) || zeichen <= 0)
        {
            return text;
        }
        var zeilen = text.Split('\n');
        var erzwungen = new HashSet<int>();
        if (einheitlich)
        {
            var abschnitte = new List<List<(int Start, bool Bricht)>>();
            int? posJetzt = null;
            var index = 0;
            while (index < zeilen.Length)
            {
                var teile = Zerlegen(zeilen[index]);
                if (teile is null || teile.Beschriftung.Length == 0)
                {
                    posJetzt = null;
                    index++;
                    continue;
                }
                var start = index;
                var block = new List<string> { zeilen[index] };
                index++;
                while (index < zeilen.Length)
                {
                    var folge = Zerlegen(zeilen[index]);
                    if (folge is null || folge.Beschriftung.Length > 0)
                    {
                        break;
                    }
                    block.Add(zeilen[index]);
                    index++;
                }
                var pos = zeilen[start].IndexOf(": ", StringComparison.Ordinal);
                if (pos != posJetzt)
                {
                    abschnitte.Add([]);
                    posJetzt = pos;
                }
                var bricht = !block.All(z => L(z) <= zeichen) && !(block.Count == 1 && Prosa(zeilen[start], zeichen) is not null);
                abschnitte[^1].Add((start, bricht));
            }
            foreach (var abschnitt in abschnitte)
            {
                if (abschnitt.Count > 1 && abschnitt.Any(a => a.Bricht))
                {
                    erzwungen.UnionWith(abschnitt.Select(a => a.Start));
                }
            }
        }
        var aus = new List<string>();
        var i = 0;
        while (i < zeilen.Length)
        {
            var zeile = zeilen[i];
            var teile = Zerlegen(zeile);
            if (teile is null || teile.Beschriftung.Length == 0)
            {
                if (teile is not null && L(zeile) > zeichen)
                {
                    aus.AddRange(Wert(teile.Wert, zeichen - 2).Select(z => "  " + z));
                }
                else if (teile is null && L(zeile) > zeichen)
                {
                    aus.AddRange(Satz(zeile, zeichen));
                }
                else
                {
                    aus.Add(zeile);
                }
                i++;
                continue;
            }
            var start = i;
            var block = new List<(string Zeile, Teile Teile)> { (zeile, teile) };
            i++;
            while (i < zeilen.Length)
            {
                var folge = Zerlegen(zeilen[i]);
                if (folge is null || folge.Beschriftung.Length > 0)
                {
                    break;
                }
                block.Add((zeilen[i], folge));
                i++;
            }
            if (!erzwungen.Contains(start) && block.All(b => L(b.Zeile) <= zeichen))
            {
                aus.AddRange(block.Select(b => b.Zeile));
                continue;
            }
            var prosa = block.Count == 1 ? Prosa(zeile, zeichen) : null;
            if (prosa is not null)
            {
                aus.AddRange(prosa);
                continue;
            }
            aus.Add(teile.Einzug + teile.Beschriftung + ":");
            foreach (var (_, t) in block)
            {
                aus.AddRange(Wert(t.Wert, zeichen - L(teile.Einzug) - 2).Select(z => teile.Einzug + "  " + z));
            }
        }
        return string.Join("\n", aus);
    }

    // ---------------------------------------------------------------- Helfer der Rechenwege

    /// <summary>_wrap_para: Fliesstext mit gleichem Einzug in jeder Zeile.</summary>
    internal static List<string> Absatz(string text, string lead, int breite) =>
        L(lead + text) > breite ? ZeileUmbrechen(lead + text, breite, lead) : [lead + text];

    /// <summary>_join_items: Aufzaehlung auf Zeilen verteilen (", " am Ende, " + " am Anfang).</summary>
    internal static List<string> Aufzaehlen(string erster, IReadOnlyList<string> eintraege, string kleber, string mehr, int breite)
    {
        var zeilen = new List<string>();
        var aktuell = erster + eintraege[0];
        for (var nummer = 2; nummer <= eintraege.Count; nummer++)
        {
            var eintrag = eintraege[nummer - 1];
            var schwanz = nummer < eintraege.Count ? PythonZeichen.Strip(kleber, " +", links: false) : "";
            if (L(aktuell + kleber + eintrag + schwanz) <= breite)
            {
                aktuell += kleber + eintrag;
            }
            else if (kleber.StartsWith(' '))
            {
                zeilen.Add(aktuell);
                aktuell = mehr + PythonZeichen.LStrip(kleber) + eintrag;
            }
            else
            {
                zeilen.Add(aktuell + PythonZeichen.RStrip(kleber));
                aktuell = mehr + eintrag;
            }
        }
        zeilen.Add(aktuell);
        return zeilen;
    }

    /// <summary>_pc_table: Tabelle am PC; breiter als 75 Zeichen, dann nur so breit wie noetig.</summary>
    internal static List<string> PcTabelle(IReadOnlyList<string> koepfe, IReadOnlyList<IReadOnlyList<string>> zeilen, IReadOnlyList<int> breiten)
    {
        var alle = new List<IReadOnlyList<string>> { koepfe };
        alle.AddRange(zeilen);
        List<string> Bauen(IReadOnlyList<int> groessen) =>
        [
            .. alle.Select(zellen => PythonZeichen.RStrip("  " + string.Join(" ",
                zellen.Zip(groessen).Select(p => PythonZeichen.Links(p.First, p.Second)).Append(zellen[^1])))),
        ];
        var ergebnis = Bauen(breiten);
        if (ergebnis.Max(L) > PcBreite)
        {
            ergebnis = Bauen([.. Enumerable.Range(0, breiten.Count).Select(i => alle.Max(z => L(z[i])))]);
        }
        return ergebnis;
    }

    /// <summary>_handy_address: Beschriftung und Adresse am Handy, lange IPv6 in der Mitte geteilt.</summary>
    internal static List<string> HandyAdresse(string beschriftung, string adresse, string einzug = "  ")
    {
        if (L(beschriftung + adresse) <= HandyBreite)
        {
            return [beschriftung + adresse];
        }
        var kopf = PythonZeichen.Strip(beschriftung).Length > 0 ? new List<string> { PythonZeichen.RStrip(beschriftung) } : [];
        var lead = PythonZeichen.Strip(beschriftung).Length > 0 ? einzug : beschriftung;
        if (L(lead + adresse) <= HandyBreite)
        {
            return [.. kopf, lead + adresse];
        }
        var laenge = L(adresse);
        var schnitt = Finde(adresse, ":", laenge / 2 - 3) + 1;
        if (schnitt == 0)
        {
            schnitt = laenge / 2;
        }
        return [.. kopf, .. HandyEinpassen([lead + PythonZeichen.Teil(adresse, 0, schnitt), einzug + PythonZeichen.Teil(adresse, schnitt)])];
    }

    /// <summary>str.find(teil, start) in Codepunkten (negativer Start zaehlt vom Ende).</summary>
    internal static int Finde(string text, string teil, int start)
    {
        var laenge = L(text);
        if (start < 0)
        {
            start = Math.Max(0, start + laenge);
        }
        if (start > laenge)
        {
            return -1;
        }
        var rest = PythonZeichen.Teil(text, start);
        var i = rest.IndexOf(teil, StringComparison.Ordinal);
        return i < 0 ? -1 : start + L(rest[..i]);
    }

    /// <summary>Zeilen zu einem Text (Python "\n".join).</summary>
    internal static string Text(IEnumerable<string> zeilen) => string.Join("\n", zeilen);

    /// <summary>Python "%-22s" bzw. ljust mit Codepunkten.</summary>
    internal static string Spalte(string text, int breite) => PythonZeichen.Links(text, breite);
}
