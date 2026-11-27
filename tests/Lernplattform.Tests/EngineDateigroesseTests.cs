using System.Text.RegularExpressions;

namespace Lernplattform.Tests;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395 Nico 5a, Weisung P12-399 Antwort 3a): keine Quelldatei der Engine
/// (<c>src/Lernplattform.Engine</c>, alle Unterordner) und keine Zeichenflaeche <c>src/Lernplattform.App/Spiel/StadtFlaeche*.cs</c>
/// hat mehr als <see cref="HoechstensZeilen"/> Zeilen. Grosse Klassen stehen in Teil-Dateien (<c>partial</c>) nach Themen;
/// so bleibt jede Aenderung leicht zu pruefen. Die Gegenprobe zeigt, dass eine zu lange Datei gefunden wird.
/// </summary>
public class EngineDateigroesseTests
{
    /// <summary>Hoechstzahl der Zeilen je Datei.</summary>
    public const int HoechstensZeilen = 1500;

    /// <summary>Fund fuer eine Datei mit <paramref name="zeilen"/> Zeilen (null = in Ordnung).</summary>
    public static string? Fund(string name, int zeilen) => zeilen <= HoechstensZeilen ? null
        : $"{name} hat {zeilen} Zeilen, erlaubt sind hoechstens {HoechstensZeilen}. Die Klasse nach Themen in Teil-Dateien "
          + "(partial) teilen, reiner Umzug ohne Aenderung (docs/ENGINE.md, Abschnitt Teil-Dateien).";

    /// <summary>Zeilen wie ein Editor sie zaehlt: Zeilenumbrueche, plus eine Zeile, wenn die Datei nicht mit einem endet.</summary>
    public static int Zeilen(string text) => text.Length == 0 ? 0 : text.Count(c => c == '\n') + (text.EndsWith('\n') ? 0 : 1);

    /// <summary>Alle geprueften Dateien (ohne bin/obj).</summary>
    public static List<string> Dateien()
    {
        var engine = Path.Combine(Pfade.Wurzel, "src", "Lernplattform.Engine");
        var sep = Path.DirectorySeparatorChar;
        var liste = Directory.GetFiles(engine, "*.cs", SearchOption.AllDirectories)
            .Where(d => !d.Contains(sep + "bin" + sep, StringComparison.Ordinal) && !d.Contains(sep + "obj" + sep, StringComparison.Ordinal))
            .ToList();
        liste.AddRange(Directory.GetFiles(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Spiel"), "StadtFlaeche*.cs"));
        liste.Sort(StringComparer.Ordinal);
        return liste;
    }

    [Fact]
    public void Engine_JedeDatei_HoechstensFuenfzehnhundertZeilen()
    {
        var dateien = Dateien();
        // die Engine hat ueber hundert Dateien, die Zeichenflaeche mehrere Teile: eine leere Liste waere ein falsches Gruen
        Assert.True(dateien.Count > 100, "zu wenige Dateien gefunden: " + dateien.Count);
        Assert.Contains(dateien, d => d.EndsWith("StadtSzene.cs", StringComparison.Ordinal));
        Assert.Contains(dateien, d => d.EndsWith("Innenraum.cs", StringComparison.Ordinal));
        Assert.Contains(dateien, d => d.EndsWith("StadtFlaeche.cs", StringComparison.Ordinal));
        var funde = dateien.Select(d => Fund(Path.GetRelativePath(Pfade.Wurzel, d), Zeilen(File.ReadAllText(d)))).OfType<string>().ToList();
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    [Fact]
    public void Gegenprobe_ZuLangeDatei_WirdGefunden()
    {
        var lang = string.Concat(Enumerable.Repeat("x\n", HoechstensZeilen + 1));
        Assert.Equal(HoechstensZeilen + 1, Zeilen(lang));
        Assert.NotNull(Fund("Lang.cs", Zeilen(lang)));
        var genau = string.Concat(Enumerable.Repeat("x\n", HoechstensZeilen - 1)) + "x";
        Assert.Equal(HoechstensZeilen, Zeilen(genau));
        Assert.Null(Fund("Genau.cs", Zeilen(genau)));
        // die alte StadtSzene.cs (3 944 Zeilen vor A 1.16.0) waere rot
        Assert.Contains("3944 Zeilen", Fund("StadtSzene.cs", 3944), StringComparison.Ordinal);
    }

    /// <summary>
    /// Alle Dateinamen <c>Name.cs</c>, die <c>docs/ENGINE.md</c> in Code-Zeichen (Backticks) nennt und die es im Repo nicht gibt
    /// (Vergleich ueber den Dateinamen, ohne bin/obj). So bleibt das Blatt nach Umzuegen auf Stand (P12-395 Punkt 5).
    /// </summary>
    public static List<string> FehlendeDateien(string blatt, ISet<string> vorhanden)
    {
        var fehlt = new SortedSet<string>(StringComparer.Ordinal);
        // Codebloecke mit drei Backticks zuerst entfernen, sonst verschieben sie die Paare im Rest des Blatts
        var ohneBloecke = Regex.Replace(blatt, "```[\\s\\S]*?```", "");
        foreach (Match code in Regex.Matches(ohneBloecke, "`([^`\\n]+)`"))
        {
            foreach (Match name in Regex.Matches(code.Groups[1].Value, @"[\w./]+\.cs\b"))
            {
                if (!vorhanden.Contains(Path.GetFileName(name.Value)))
                {
                    fehlt.Add(name.Value);
                }
            }
        }
        return [.. fehlt];
    }

    private static HashSet<string> DateinamenImRepo()
    {
        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(Pfade.Wurzel, "*.cs", SearchOption.AllDirectories)
            .Where(d => !d.Contains(sep + "bin" + sep, StringComparison.Ordinal) && !d.Contains(sep + "obj" + sep, StringComparison.Ordinal))
            .Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void EngineBlatt_JedeGenannteDatei_GibtEs()
    {
        var blatt = File.ReadAllText(Path.Combine(Pfade.Wurzel, "docs", "ENGINE.md"));
        Assert.Contains("Teil-Dateien", blatt, StringComparison.Ordinal);
        var vorhanden = DateinamenImRepo();
        // die Teil-Dateien muessen genannt und vorhanden sein (sonst prueft der Test nichts Neues)
        Assert.Contains("StadtSzene.Netze.cs", vorhanden);
        var fehlt = FehlendeDateien(blatt, vorhanden);
        Assert.True(fehlt.Count == 0, "docs/ENGINE.md nennt Dateien, die es nicht gibt: " + string.Join(", ", fehlt));
        // Gegenprobe: ein erfundener Name im Blatt wird gefunden
        Assert.Equal(["Gibtsnicht.cs"], FehlendeDateien(blatt + "\n- `Gibtsnicht.cs`\n", vorhanden));
    }
}
