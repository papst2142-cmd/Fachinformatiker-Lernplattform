using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Lernplattform.Tests;

/// <summary>
/// Welle Sammelliste 217 (Weisung P12-14, Nico 1a 2a 3a 4a 5a): die Vergleichswerte G1-G52 und Testdaten in
/// tests/Vergleich/, alle Dateien unter inhalte/ (byte-gleich mit Python v0.63.2), die fuenf erzeugten Zeichen-Tabellen
/// und einteilung_bisher.json sind feste Sollwerte "Stand C#". Die CI erzeugt sie nicht mehr aus der Python-Linie;
/// dieser Test prueft ihre SHA-256 gegen tests/Vergleich/SOLLWERTE.sha256 (gleiche Regeln wie
/// werkzeuge/vergleich/sollwerte.py im Job "sollwerte"). Eine Aenderung nur mit Nicos Freigabe und einem neuen Abschnitt
/// "## Nr. n" in AENDERUNGEN_SOLLWERTE.md; die Kopfzeile "# Aenderung: n" muss gleich der hoechsten Nummer sein.
/// Gegenproben: ein Byte geaendert, eine Zeile geloescht, Nummer ohne Eintrag erhoeht, neue Datei ohne Zeile: rot.
/// </summary>
public class SollwerteTests
{
    private const string Liste = "tests/Vergleich/SOLLWERTE.sha256";
    private const string Aenderungen = "tests/Vergleich/AENDERUNGEN_SOLLWERTE.md";
    private static readonly string[] Ordner = ["tests/Vergleich", "inhalte", "inhalte_csharp/spiel/stadtbau"];

    internal static readonly string[] Einzeln =
    [
        "inhalte_csharp/lernen/einteilung_bisher.json",
        "src/Lernplattform.Core/Projekt/PdfZeichen.Tabelle.cs",
        "src/Lernplattform.Core/Rechner/PythonZeichen.Tabelle.cs",
        "src/Lernplattform.Core/Spiel/PyTitel.Tabelle.cs",
        "src/Lernplattform.Core/Suche/PythonKlein.Tabelle.cs",
        "src/Lernplattform.Core/Texte/RechnerTexte.Python.cs",
    ];

    [Fact]
    public void AlleUnveraendert()
    {
        var fehler = Pruefen(Wurzel());
        Assert.True(fehler.Count == 0, string.Join("\n", fehler));
    }

    [Fact]
    public void ListeNenntGenauDieGeschuetztenDateien()
    {
        var wurzel = Wurzel();
        var zeilen = Zeilen(wurzel).Keys.Order(StringComparer.Ordinal).ToList();
        Assert.Equal(Geschuetzt(wurzel), zeilen);
        // alle G-Dateien und Testdaten (59) plus quiz_python.json, 24 Dateien unter inhalte/, 6 einzelne,
        // seit Sollwert-Aenderung Nr. 13 die 2 Dateien der Stadt-Fassung 0 (inhalte_csharp/spiel/stadtbau/)
        Assert.Equal(92, zeilen.Count);
    }

    [Fact]
    public void WerkzeugSchuetztDieselbenDateien()
    {
        var text = File.ReadAllText(Path.Combine(Wurzel(), "werkzeuge", "vergleich", "sollwerte.py"));
        var block = Regex.Match(text, @"EINZELN = \((.*?)\n\)", RegexOptions.Singleline).Groups[1].Value;
        var python = Regex.Matches(block, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(Einzeln, python);
        Assert.Contains("ORDNER = (\"tests/Vergleich\", \"inhalte\", \"inhalte_csharp/spiel/stadtbau\")", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Gegenprobe_EinByteGeaendert_Rot()
    {
        using var kopie = Kopie();
        var pfad = Path.Combine(kopie.Pfad, "tests", "Vergleich", "spiel_fingerabdruck.json");
        var bytes = File.ReadAllBytes(pfad);
        bytes[^2] ^= 1;
        File.WriteAllBytes(pfad, bytes);
        var fehler = Pruefen(kopie.Pfad);
        Assert.Single(fehler);
        Assert.StartsWith("abweichend: tests/Vergleich/spiel_fingerabdruck.json", fehler[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Gegenprobe_ZeileGeloescht_Rot()
    {
        using var kopie = Kopie();
        var pfad = Path.Combine(kopie.Pfad, Liste);
        File.WriteAllLines(pfad, File.ReadAllLines(pfad).Where(z => !z.EndsWith("  inhalte/quizfragen.json", StringComparison.Ordinal)));
        Assert.Equal(["ohne Zeile in " + Liste + ": inhalte/quizfragen.json"], Pruefen(kopie.Pfad));
    }

    [Fact]
    public void Gegenprobe_NummerOhneEintragErhoeht_Rot()
    {
        using var kopie = Kopie();
        var pfad = Path.Combine(kopie.Pfad, Liste);
        var hoechste = Nummern(kopie.Pfad).Max();
        var text = File.ReadAllText(pfad);
        File.WriteAllText(pfad, text.Replace("# Aenderung: " + hoechste + "\n", "# Aenderung: " + (hoechste + 1) + "\n", StringComparison.Ordinal));
        var fehler = Pruefen(kopie.Pfad);
        Assert.Single(fehler);
        Assert.Contains("hoechste Nummer", fehler[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Gegenprobe_NeueDateiOhneZeileUndLueckeInNummern_Rot()
    {
        using var kopie = Kopie();
        File.WriteAllText(Path.Combine(kopie.Pfad, "tests", "Vergleich", "neu.json"), "{}\n");
        var md = Path.Combine(kopie.Pfad, Aenderungen);
        File.WriteAllText(md, File.ReadAllText(md).Replace("## Nr. 2\n", "## Nr. 3\n", StringComparison.Ordinal));
        var fehler = Pruefen(kopie.Pfad);
        Assert.Contains("ohne Zeile in " + Liste + ": tests/Vergleich/neu.json", fehler);
        Assert.Contains(fehler, f => f.StartsWith("Nummern in ", StringComparison.Ordinal));
    }

    /// <summary>Alle Abweichungen als Text; leer = gruen.</summary>
    internal static List<string> Pruefen(string wurzel)
    {
        var fehler = new List<string>();
        var zeilen = File.ReadAllText(Path.Combine(wurzel, Liste)).Replace("\r\n", "\n").Split('\n');
        var eintraege = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var z in zeilen.Where(z => z.Length > 0 && !z.StartsWith('#')))
        {
            var m = Regex.Match(z, "^([0-9a-f]{64})  (\\S.*)$");
            if (!m.Success)
            {
                fehler.Add("Zeile ohne Form '<sha256>  <pfad>': " + z);
                continue;
            }
            if (!eintraege.TryAdd(m.Groups[2].Value, m.Groups[1].Value))
            {
                fehler.Add("doppelt: " + m.Groups[2].Value);
            }
        }
        var nummern = Nummern(wurzel);
        if (!nummern.SequenceEqual(Enumerable.Range(1, nummern.Count)))
        {
            fehler.Add("Nummern in " + Aenderungen + " nicht fortlaufend ab 1: " + string.Join(", ", nummern));
        }
        var kopf = zeilen.Where(z => z.StartsWith("# Aenderung: ", StringComparison.Ordinal)).ToList();
        var hoechste = nummern.Count == 0 ? 0 : nummern.Max();
        if (kopf.Count != 1)
        {
            fehler.Add("genau eine Zeile '# Aenderung: <n>' noetig, gefunden " + kopf.Count);
        }
        else if (kopf[0] != "# Aenderung: " + hoechste)
        {
            fehler.Add(Liste + " nennt " + kopf[0][2..] + ", hoechste Nummer in " + Aenderungen + " ist " + hoechste);
        }
        var soll = Geschuetzt(wurzel);
        fehler.AddRange(soll.Where(p => !eintraege.ContainsKey(p)).Select(p => "ohne Zeile in " + Liste + ": " + p));
        foreach (var (pfad, wert) in eintraege.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var voll = Path.Combine(wurzel, pfad);
            if (!File.Exists(voll))
            {
                fehler.Add("fehlt: " + pfad);
            }
            else if (!soll.Contains(pfad))
            {
                fehler.Add("nicht geschuetzt, aber gelistet: " + pfad);
            }
            else if (Summe(voll) is var ist && ist != wert)
            {
                fehler.Add("abweichend: " + pfad + " (Soll " + wert + ", Ist " + ist + ")");
            }
        }
        return fehler;
    }

    private static List<string> Geschuetzt(string wurzel)
    {
        var pfade = new HashSet<string>(Einzeln, StringComparer.Ordinal);
        foreach (var ordner in Ordner)
        {
            foreach (var datei in Directory.EnumerateFiles(Path.Combine(wurzel, ordner), "*", SearchOption.AllDirectories))
            {
                var pfad = Path.GetRelativePath(wurzel, datei).Replace('\\', '/');
                if (pfad != Liste && pfad != Aenderungen)
                {
                    pfade.Add(pfad);
                }
            }
        }
        return pfade.Order(StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, string> Zeilen(string wurzel) =>
        File.ReadAllLines(Path.Combine(wurzel, Liste)).Where(z => z.Length > 0 && !z.StartsWith('#'))
            .ToDictionary(z => z[66..], z => z[..64], StringComparer.Ordinal);

    private static List<int> Nummern(string wurzel) =>
        Regex.Matches(File.ReadAllText(Path.Combine(wurzel, Aenderungen)).Replace("\r\n", "\n"), @"^## Nr\. (\d+)[ \t]*$", RegexOptions.Multiline)
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();

    private static string Summe(string pfad) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(pfad)));

    /// <summary>Kopie aller geschuetzten Dateien und der beiden Listen in einen leeren Ordner (fuer die Gegenproben).</summary>
    private static TempOrdner Kopie()
    {
        var wurzel = Wurzel();
        var kopie = new TempOrdner();
        foreach (var pfad in Geschuetzt(wurzel).Append(Liste).Append(Aenderungen))
        {
            var ziel = Path.Combine(kopie.Pfad, pfad);
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.Copy(Path.Combine(wurzel, pfad), ziel);
        }
        Assert.Empty(Pruefen(kopie.Pfad));
        return kopie;
    }

    private static string Wurzel()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(ordner.FullName, "Directory.Build.props")))
        {
            ordner = ordner.Parent!;
        }
        return ordner.FullName;
    }
}
