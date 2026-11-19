using System.Text.RegularExpressions;
using Lernplattform.Core.Update;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P12-306b (Nico 19.11.2026 06:52 MEZ): nach A 0.99.0 kommt A 1.0.0 (danach A 1.1.0 …), sinngemaess auch fuer B; die
/// Stufe A/B trennt von der echten 1.0.0 ohne Buchstaben. Feste Reihe A 0.99.0 &lt; A 0.99.1 &lt; A 1.0.0 &lt; A 1.1.0 &lt; B 0.1.0
/// &lt; 1.0.0 in Paket-, Tag- und Anzeigeform, Android-Versionscodes aus der Regel in Directory.Build.props und die Tag-Regel.
/// Gegenproben: eine Rechnung ohne Hauptnummer (wie sie bei „x = 0 in Alpha“ gereicht haette) bringt die Reihe durcheinander.
/// </summary>
public sealed class VersionRegelA100Tests
{
    /// <summary>Die Reihe in Anzeigeform, aufsteigend.</summary>
    private static readonly string[] Reihe = ["A 0.99.0", "A 0.99.1", "A 1.0.0", "A 1.1.0", "B 0.1.0", "1.0.0"];

    private static readonly string[] Pakete = ["1.0.0-a.0.99.0", "1.0.0-a.0.99.1", "1.0.0-a.1.0.0", "1.0.0-a.1.1.0", "1.0.0-b.0.1.0", "1.0.0"];

    private static readonly string[] Tags = ["vA0.99.0", "vA0.99.1", "vA1.0.0", "vA1.1.0", "vB0.1.0", "v1.0.0"];

    [Fact]
    public void Reihe_InAllenFormen_Aufsteigend()
    {
        foreach (var form in new[] { Reihe, Pakete, Tags })
        {
            for (var i = 0; i < form.Length; i++)
            {
                for (var j = 0; j < form.Length; j++)
                {
                    Assert.True(UpdateVersion.IstNeuer(form[j], form[i]) == (j > i), $"{form[j]} neuer als {form[i]}: erwartet {j > i}");
                }
            }
        }
        // gemischte Formen: Paket gegen Tag gegen Anzeige
        for (var i = 0; i + 1 < Reihe.Length; i++)
        {
            Assert.True(UpdateVersion.IstNeuer(Tags[i + 1], Pakete[i]));
            Assert.True(UpdateVersion.IstNeuer(Pakete[i + 1], Reihe[i]));
            Assert.False(UpdateVersion.IstNeuer(Reihe[i], Tags[i + 1]));
        }
    }

    [Fact]
    public void Formen_GehenIneinanderUeber()
    {
        for (var i = 0; i < Reihe.Length; i++)
        {
            var v = UpdateVersion.Lesen(Reihe[i])!;
            Assert.Equal(Pakete[i], v.Paket);
            Assert.Equal(Reihe[i], UpdateVersion.Lesen(Pakete[i])!.Anzeige);
            Assert.Equal(Reihe[i], UpdateVersion.Lesen(Tags[i])!.Anzeige);
            var stufe = Reihe[i].Contains(' ', StringComparison.Ordinal) ? Reihe[i][..1] : "";
            var zahlen = Reihe[i].Contains(' ', StringComparison.Ordinal) ? Reihe[i][2..] : Reihe[i];
            Assert.Equal(Tags[i], UpdateVersion.ReleaseTag(stufe, zahlen));
            Assert.True(ReleaseLinie.IstLernplattform(Tags[i]), Tags[i]);
            Assert.False(ReleaseLinie.IstStadtbau(Tags[i]), Tags[i]);
        }
        Assert.Equal("vB1.0.0", UpdateVersion.ReleaseTag("B", "1.0.0"));
        Assert.Equal("1.0.0-b.1.0.0", UpdateVersion.Lesen("B 1.0.0")!.Paket);
        Assert.True(UpdateVersion.IstNeuer("B 1.0.0", "B 0.99.0"));
        Assert.True(UpdateVersion.IstNeuer("1.0.0", "B 1.0.0"));
        // Gegenprobe: die echte 1.0.0 ohne Buchstaben ist nie gleich A 1.0.0
        Assert.NotEqual(UpdateVersion.Lesen("A 1.0.0"), UpdateVersion.Lesen("1.0.0"));
    }

    /// <summary>Android-Versionscode nach der Regel in Directory.Build.props (Stufe × 1 000 000 + Haupt × 10 000 + Neben × 100 + Fix).</summary>
    private static long Code(UpdateVersion v, long hauptFaktor) => (v.Stufe * 1_000_000L) + (v.Haupt * hauptFaktor) + (v.Neben * 100L) + v.Fix;

    [Fact]
    public void AndroidCodes_Aufsteigend_UndRegelInDirectoryBuildProps_Gegenprobe()
    {
        var props = File.ReadAllText(Path.Combine(Wurzel(), "Directory.Build.props"));
        Assert.Matches(new Regex(@"Multiply\(\$\(StufeCode\),1000000\)"), props);
        Assert.Matches(new Regex(@"Multiply\(\$\(VersionHaupt\),10000\)"), props);
        Assert.Matches(new Regex(@"Multiply\(\$\(VersionNeben\),100\)"), props);
        var codes = Reihe.Select(r => Code(UpdateVersion.Lesen(r)!, 10_000)).ToArray();
        Assert.Equal([1_009_900L, 1_009_901L, 1_010_000L, 1_010_100L, 2_000_100L, 3_010_000L], codes);
        Assert.True(codes.Zip(codes.Skip(1)).All(p => p.First < p.Second));
        // Gegenprobe: ohne Hauptnummer im Code waere A 1.0.0 (1 000 000) aelter als A 0.99.0 (1 009 900)
        var ohneHaupt = Reihe.Select(r => Code(UpdateVersion.Lesen(r)!, 0)).ToArray();
        Assert.False(ohneHaupt.Zip(ohneHaupt.Skip(1)).All(p => p.First < p.Second));
    }

    [Fact]
    public void Velopack_OrdnetPaketversionenAufsteigend_Gegenprobe()
    {
        var v = Pakete.Select(Velopack.SemanticVersion.Parse).ToArray();
        for (var i = 0; i + 1 < v.Length; i++)
        {
            Assert.True(v[i].CompareTo(v[i + 1]) < 0, $"{Pakete[i]} < {Pakete[i + 1]}");
        }
        // Gegenprobe: als Text sortiert stuende 1.0.0-a.0.99.0 hinter 1.0.0-a.1.0.0 nicht davor
        Assert.True(string.CompareOrdinal("1.0.0-a.0.99.0", "1.0.0-a.1.0.0") < 0);
        Assert.True(string.CompareOrdinal("1.0.0-a.0.99.0", "1.0.0-a.0.100.0") > 0);
        Assert.True(Velopack.SemanticVersion.Parse("1.0.0-a.0.99.0").CompareTo(Velopack.SemanticVersion.Parse("1.0.0-a.0.100.0")) < 0);
    }

    [Fact]
    public void TagFaelle_EnthaltenA100()
    {
        var text = File.ReadAllText(Path.Combine(Wurzel(), "werkzeuge", "release", "tag_faelle.json"));
        Assert.Contains("\"vA1.0.0\"", text, StringComparison.Ordinal);
        Assert.Contains("\"vA0.99.0\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PaketBrauchtApp_A100()
    {
        // Mindestversion eines Inhaltspakets ("braucht_app"): A 1.0.0 ist neuer als A 0.99.0, aelter als B 0.1.0
        var braucht = UpdateVersion.Lesen("A 1.0.0")!;
        Assert.True(braucht.CompareTo(UpdateVersion.Lesen("A 0.99.0")) > 0);
        Assert.True(braucht.CompareTo(UpdateVersion.Lesen("1.0.0-a.1.0.0")) == 0);
        Assert.True(braucht.CompareTo(UpdateVersion.Lesen("B 0.1.0")) < 0);
    }

    private static string Wurzel()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Directory.Build.props")))
        {
            d = d.Parent;
        }
        return d?.FullName ?? throw new InvalidOperationException("Wurzel nicht gefunden");
    }
}
