using Lernplattform.Core.Diagnose;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P2-17 A1/A2 (Nico 21.10.2026): Farbregler im Start ohne Bedienung, Schritt 1 gegen
/// 150 ms, ab Schritt 2 gegen 100 ms. Kontrollfehler: Schritt 1 mit 160 ms rot, Schritt 2 mit
/// 120 ms rot. Die Regel wirkt nur auf diese Messung; der Ansichtswechsel bleibt bei 400 ms.
/// </summary>
public class FarbreglerregelTests
{
    private static double[] Reihe(double erster, double zweiter) => [erster, zweiter, .. Enumerable.Repeat(16.0, 358)];

    [Fact]
    public void GrenzenAlsKonstanten()
    {
        Assert.Equal(150, Farbreglerregel.GrenzeErsterSchritt);
        Assert.Equal(100, Farbreglerregel.GrenzeWeitereSchritte);
    }

    [Fact]
    public void ErsterSchrittMit140Gruen()
    {
        Assert.Empty(Farbreglerregel.Funde(Reihe(140, 16)));
        Assert.Empty(Farbreglerregel.Funde(Reihe(150, 100)));
    }

    [Fact]
    public void Kontrollfehler_ErsterSchrittMit160Rot()
    {
        var funde = Farbreglerregel.Funde(Reihe(160, 16));
        Assert.Equal(["Schritt 1: 160.0 ms ueber 150 ms"], funde);
    }

    [Fact]
    public void Kontrollfehler_ZweiterSchrittMit120Rot()
    {
        var funde = Farbreglerregel.Funde(Reihe(140, 120));
        Assert.Equal(["Schritt 2: 120.0 ms ueber 100 ms"], funde);
        // auch ein spaeter Schritt bekommt keine 150 ms
        double[] spaet = [.. Reihe(16, 16)[..359], 120];
        Assert.Equal(["Schritt 360: 120.0 ms ueber 100 ms"], Farbreglerregel.Funde(spaet));
    }

    [Fact]
    public void RegelWirktNurAufDenFarbregler()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "global.json")))
        {
            ordner = ordner.Parent;
        }
        Assert.NotNull(ordner);
        var text = File.ReadAllText(Path.Combine(ordner.FullName, "src", "Lernplattform.App", "Messung", "AutoMessung.cs")).Replace("\r\n", "\n");
        // Ansichtswechsel und erstes Oeffnen bleiben bei 400 ms
        Assert.Contains("public const double Geraetegrenze = 400;", text, StringComparison.Ordinal);
        // Farbreglerregel steht nur im Rumpf von FarbreglerAsync
        var start = text.IndexOf("private static async Task<int> FarbreglerAsync(", StringComparison.Ordinal);
        var ende = text.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        Assert.True(start > 0 && ende > start);
        var aussen = text[..start] + text[ende..];
        Assert.DoesNotContain("Farbreglerregel.", aussen, StringComparison.Ordinal);
        Assert.Contains("Farbreglerregel.Funde(zeiten)", text[start..ende], StringComparison.Ordinal);
        // keine feste 100 mehr in der Farbregler-Pruefung
        Assert.DoesNotContain("> 100", text[start..ende], StringComparison.Ordinal);
    }
}
