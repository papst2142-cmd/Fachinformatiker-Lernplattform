using System.Security.Cryptography;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-51 (Nico 07.11.2026 09:40 MEZ): das Werkzeug hat ein eigenes Symbol, das der Lernplattform bleibt unverändert.
/// </summary>
public class SymbolTests
{
    // Lernplattform (unverändert seit Phase 0)
    private const string LernplattformPng = "17ce3bdbeb1537d088a55c936cfb40abb18c19fe5c97e77fef481fa65aed0924";
    private const string LernplattformIco = "4cef83aa154e754f37e3b92c14a4abdeb2c59abcf98d0d630041e37864e51080";
    // Werkzeug: freigegebenes Bild (Fi-Austausch anlagen/stadtbau_symbol/Stadtbau_Symbol_512.png) und icon.ico daraus
    // (werkzeuge/stadtbau/erzeuge_symbol_ico.py)
    private const string StadtbauPng = "f61f5d7cd8f2f4a33931187b8e7eaa7e754f2ede150ab3bcd6fdad812a61e422";
    private const string StadtbauIco = "70ea09fe9adb1dd515abdf92f7b58607f19a8c97feef94f734b080c98e910461";

    private static string Summe(string pfad) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Daten.Wurzel, pfad))));

    /// <summary>Rot, wenn beide Symbole gleich sind (auch als Gegenprobe genutzt).</summary>
    internal static void Verschieden(string werkzeug, string lernplattform) =>
        Assert.True(werkzeug != lernplattform, "Werkzeug und Lernplattform haben dasselbe Symbol: " + werkzeug);

    [Fact]
    public void Lernplattform_SymbolUnveraendert()
    {
        Assert.Equal(LernplattformPng, Summe("src/Lernplattform.App/Assets/icon.png"));
        Assert.Equal(LernplattformPng, Summe("src/Lernplattform.Android/Icon.png"));
        Assert.Equal(LernplattformIco, Summe("src/Lernplattform.App/Assets/icon.ico"));
    }

    [Fact]
    public void Werkzeug_EigenesSymbol_AufTabletUndPc()
    {
        Assert.Equal(StadtbauPng, Summe("src/Stadtbau.App/Assets/icon.png"));
        Assert.Equal(StadtbauPng, Summe("src/Stadtbau.Android/Icon.png"));
        Assert.Equal(StadtbauIco, Summe("src/Stadtbau.App/Assets/icon.ico"));
        Verschieden(Summe("src/Stadtbau.App/Assets/icon.png"), Summe("src/Lernplattform.App/Assets/icon.png"));
        Verschieden(Summe("src/Stadtbau.Android/Icon.png"), Summe("src/Lernplattform.Android/Icon.png"));
        Verschieden(Summe("src/Stadtbau.App/Assets/icon.ico"), Summe("src/Lernplattform.App/Assets/icon.ico"));
    }

    [Fact]
    public void Gegenprobe_GleicheSymboleSindRot()
    {
        var lp = Summe("src/Lernplattform.App/Assets/icon.png");
        Assert.ThrowsAny<Exception>(() => Verschieden(lp, lp));
    }

    [Fact]
    public void ProgrammUndPaket_NehmenDasWerkzeugSymbol()
    {
        var projekt = File.ReadAllText(Path.Combine(Daten.Wurzel, "src/Stadtbau.Desktop/Stadtbau.Desktop.csproj"));
        Assert.Contains(@"<ApplicationIcon>..\Stadtbau.App\Assets\icon.ico</ApplicationIcon>", projekt);
        Assert.DoesNotContain("Lernplattform.App\\Assets\\icon", projekt);
        var bau = File.ReadAllText(Path.Combine(Daten.Wurzel, "werkzeuge/stadtbau/baue_stadtbau.sh"));
        Assert.Contains("-i \"$WURZEL/src/Stadtbau.App/Assets/icon.ico\"", bau);
        Assert.DoesNotContain("Lernplattform.App/Assets/icon", bau);
        var app = File.ReadAllText(Path.Combine(Daten.Wurzel, "src/Stadtbau.App/StadtbauApp.axaml.cs"));
        Assert.Contains("avares://Stadtbau.App/Assets/icon.ico", app);
        Assert.Contains("Icon = Symbol(),", app);
    }
}
