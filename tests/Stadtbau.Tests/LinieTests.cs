using Lernplattform.Core.Update;
using Lernplattform.Desktop;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-16 Abschnitt 3): das Werkzeug nimmt nur Releases der Linie vS, die Lernplattform nur
/// vA/vB/v1. Gegenprobe jeweils mit dem Release der anderen Linie (neuer und als latest markiert).
/// </summary>
public class LinieTests
{
    private const string Antwort = """
        [
          {"tag_name": "vS0.2.0", "name": "Fachinformatiker Stadtbau S 0.2.0", "prerelease": false, "published_at": "2026-11-08T10:00:00Z", "assets": []},
          {"tag_name": "vA0.43.0", "name": "Fachinformatiker Lernplattform A 0.43.0", "prerelease": false, "published_at": "2026-11-07T10:00:00Z", "assets": []},
          {"tag_name": "vS0.1.0", "name": "Fachinformatiker Stadtbau S 0.1.0", "prerelease": false, "published_at": "2026-11-06T10:00:00Z", "assets": []},
          {"tag_name": "vA0.42.1", "name": "Fachinformatiker Lernplattform A 0.42.1", "prerelease": false, "published_at": "2026-11-05T10:00:00Z", "assets": []},
          {"tag_name": "vS0.3.0-test", "name": "kaputt", "prerelease": false, "published_at": "2026-11-09T10:00:00Z", "assets": []},
          {"tag_name": 5, "name": "ohne Text", "prerelease": false, "published_at": "2026-11-09T10:00:00Z", "assets": []}
        ]
        """;

    [Fact]
    public void Werkzeug_NurLinieS()
    {
        var r = LinienQuelle.Waehlen(Antwort, ReleaseLinie.Linie.Stadtbau, vorab: false);
        Assert.Equal(["Fachinformatiker Stadtbau S 0.2.0", "Fachinformatiker Stadtbau S 0.1.0"], r.Select(x => x.Name));
    }

    [Fact]
    public void Lernplattform_NurLinieA_GegenprobeWerkzeugIstNeuer()
    {
        var r = LinienQuelle.Waehlen(Antwort, ReleaseLinie.Linie.Lernplattform, vorab: false);
        Assert.Equal(["Fachinformatiker Lernplattform A 0.43.0", "Fachinformatiker Lernplattform A 0.42.1"], r.Select(x => x.Name));
        // Gegenprobe: ohne Filter waere das neueste Release das des Werkzeugs
        Assert.Equal("vS0.3.0-test", System.Text.Json.Nodes.JsonNode.Parse(Antwort)!.AsArray().OrderByDescending(x => (string)x!["published_at"]!).First()!["tag_name"]!.ToString());
    }

    [Theory]
    [InlineData("vA0.42.1", true, false)]
    [InlineData("vB0.1.0", true, false)]
    [InlineData("v1.0.0", true, false)]
    [InlineData("v12.3.4", true, false)]
    [InlineData("vS0.1.0", false, true)]
    [InlineData("vS10.20.30", false, true)]
    [InlineData("v0.64.0", false, false)]
    [InlineData("vA0.42", false, false)]
    [InlineData("vS0.1.0-rc", false, false)]
    [InlineData("vA01.2.3", false, false)]
    [InlineData("S0.1.0", false, false)]
    [InlineData("", false, false)]
    public void Linien(string tag, bool lernplattform, bool stadtbau)
    {
        Assert.Equal(lernplattform, ReleaseLinie.IstLernplattform(tag));
        Assert.Equal(stadtbau, ReleaseLinie.IstStadtbau(tag));
    }

    [Fact]
    public void Filtern_KeineListe_Leer()
    {
        Assert.Empty(ReleaseLinie.Filtern("{}", ReleaseLinie.Linie.Stadtbau));
        Assert.Empty(ReleaseLinie.Filtern("kein json", ReleaseLinie.Linie.Lernplattform));
    }
}
