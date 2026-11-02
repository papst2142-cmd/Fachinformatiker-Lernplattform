using System.Text.RegularExpressions;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Suche;

namespace Lernplattform.Tests;

/// <summary>Phase 11 FW1 (Weisung P11-4): Sammelliste 17 und 78; 34 in RechenTrainer*Tests, 175 ueber G16.</summary>
public sealed partial class Phase11FW1Tests
{
    /// <summary>
    /// Sammelliste 17: der Schnellweg von RahmenplanGewicht.Klein (ToLowerInvariant mit "İ") gilt nur unter
    /// U+0180; dort ist er fuer jedes Zeichen gleich Pythons lower() (PythonKlein, geprueft gegen G19).
    /// </summary>
    [Fact]
    public void KleinSchnellwegGleichPython()
    {
        var abweichend = new List<int>();
        for (var c = 0; c < RahmenplanGewicht.SchnellGrenze; c++)
        {
            var text = "A" + (char)c + "Z";
            Assert.True(RahmenplanGewicht.SchnellGleich(text));
            if (RahmenplanGewicht.Klein(text) != PythonKlein.Klein(text))
            {
                abweichend.Add(c);
            }
        }
        Assert.Empty(abweichend);
        Assert.False(RahmenplanGewicht.SchnellGleich("ΟΔΟΣ"));
        Assert.Equal("οδος", RahmenplanGewicht.Klein("ΟΔΟΣ"));
        Assert.Equal("i̇stanbul", RahmenplanGewicht.Klein("İstanbul"));
    }

    [GeneratedRegex(@"""[AB] \d+\.\d+\.\d+""|-[ab]\.\d+\.\d+\.\d+")]
    private static partial Regex FesteVersion();

    /// <summary>
    /// Sammelliste 78: die Proben im Start ohne Bedienung nennen keine feste Programmversion mehr (vorher
    /// "A 0.11.1 verfügbar" und "installiert A 0.11.0"). Einzige Ausnahme: die Kurzzeilen-Probe "A 9.9.9"
    /// (P8W2b) prueft nur den Satzbau, nicht die installierte Version.
    /// </summary>
    [Fact]
    public void StartTestProbenOhneFesteVersion()
    {
        var ordner = Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Messung");
        var funde = Directory.EnumerateFiles(ordner, "*.cs")
            .SelectMany(datei => File.ReadAllLines(datei).Select((zeile, nr) => (Datei: Path.GetFileName(datei), Nr: nr + 1, Zeile: zeile)))
            .Where(z => !z.Zeile.TrimStart().StartsWith("//", StringComparison.Ordinal) && FesteVersion().IsMatch(z.Zeile))
            .Where(z => !(z.Datei == "AutoMessung.P8W2b.cs" && z.Zeile.Contains("\"A 9.9.9\"", StringComparison.Ordinal)))
            .Select(z => $"{z.Datei}:{z.Nr}: {z.Zeile.Trim()}")
            .ToList();
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }
}
