using System.Text;

namespace Lernplattform.Tests;

/// <summary>
/// Groessenwaechter (Weisung P12-298 Teil C, Nico 17.11.2026 22:09 MEZ, Antwort a): CLAUDE.md darf hoechstens 45 KB
/// (46 080 Byte) gross sein. Bleibende Regeln einer Welle kommen nach Themen in docs/REGELN.md, nie in CLAUDE.md. Die
/// Meldung nennt die Groesse und was zu tun ist; die Gegenprobe zeigt, dass die Pruefung eine zu grosse Datei findet.
/// </summary>
public class DokuGroesseTests
{
    /// <summary>Hoechstgroesse von CLAUDE.md in Byte (45 KB).</summary>
    public const long Hoechstens = 46_080;

    private static string Wurzel()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "Directory.Build.props")))
        {
            ordner = ordner.Parent;
        }
        return ordner!.FullName;
    }

    /// <summary>Fund fuer eine Datei der Groesse <paramref name="bytes"/> (null = in Ordnung).</summary>
    public static string? Fund(string name, long bytes) => bytes <= Hoechstens ? null
        : $"{name} ist {bytes} Byte gross, erlaubt sind hoechstens {Hoechstens} Byte (45 KB). Neue bleibende Regeln gehoeren nach "
          + "docs/REGELN.md (passender Abschnitt, angehaengt), in CLAUDE.md nur eine Verweiszeile; \"Aktueller Stand\" ersetzen, nicht anhaengen.";

    [Fact]
    public void ClaudeMd_HoechstensFuenfundvierzigKilobyte()
    {
        var datei = Path.Combine(Wurzel(), "CLAUDE.md");
        Assert.True(File.Exists(datei), "CLAUDE.md fehlt: " + datei);
        var fund = Fund("CLAUDE.md", new FileInfo(datei).Length);
        Assert.True(fund is null, fund);
    }

    [Fact]
    public void RegelnMd_DaUndVonClaudeMdGenannt()
    {
        var wurzel = Wurzel();
        Assert.True(File.Exists(Path.Combine(wurzel, "docs", "REGELN.md")), "docs/REGELN.md fehlt");
        Assert.Contains("docs/REGELN.md", File.ReadAllText(Path.Combine(wurzel, "CLAUDE.md"), Encoding.UTF8), StringComparison.Ordinal);
    }

    /// <summary>Gegenprobe: ein Byte ueber der Grenze ist ein Fund mit Groesse und Hinweis, genau die Grenze nicht.</summary>
    [Fact]
    public void Gegenprobe_EinByteZuViel_IstFund()
    {
        Assert.Null(Fund("CLAUDE.md", Hoechstens));
        var fund = Fund("CLAUDE.md", Hoechstens + 1);
        Assert.NotNull(fund);
        Assert.Contains("46081 Byte", fund, StringComparison.Ordinal);
        Assert.Contains("docs/REGELN.md", fund, StringComparison.Ordinal);
        // die heutige Fassung vor der Kuerzung (82 584 Byte) waere rot
        Assert.NotNull(Fund("CLAUDE.md", 82_584));
    }
}
