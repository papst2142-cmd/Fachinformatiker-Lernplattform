using System.Text.RegularExpressions;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.3.2 / S 0.61.1 (Weisung P12-350, Sammelliste 434 und 435): Lernplattform und Werkzeug behandeln am Handy dieselben
/// Konfigurationswechsel selbst (426) und geben eine reine Dichte-Aenderung sofort an Avalonia weiter (<c>Dichte.cs</c>, eine
/// Datei fuer beide Programme). Die Wirkung am Geraet belegt die Konfig-Probe im Emulator (Job android); dieser Test haelt fest,
/// dass beide Activities gleich bleiben, auch wenn spaeter nur eine geaendert wird.
/// </summary>
public class AndroidKonfigurationTests
{
    private static string Lesen(params string[] teile) => File.ReadAllText(Path.Combine([Pfade.Wurzel, .. teile]));

    private static string[] Wechsel(string quelle)
    {
        var m = Regex.Match(quelle, @"ConfigurationChanges\s*=\s*(?<w>[^)\]]*)\)\]");
        Assert.True(m.Success, "ConfigurationChanges nicht gefunden");
        return Regex.Matches(m.Groups["w"].Value, @"ConfigChanges\.(\w+)").Select(t => t.Groups[1].Value).Order(StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void BeideActivities_GleicheKonfigurationswechsel_MitDichte()
    {
        var lp = Wechsel(Lesen("src", "Lernplattform.Android", "MainActivity.cs"));
        var wz = Wechsel(Lesen("src", "Stadtbau.Android", "MainActivity.cs"));
        Assert.Equal(["Density", "Keyboard", "KeyboardHidden", "Orientation", "ScreenLayout", "ScreenSize", "SmallestScreenSize", "UiMode"], lp);
        Assert.Equal(lp, wz);
    }

    [Fact]
    public void BeideActivities_ZiehenDieDichteNach()
    {
        foreach (var datei in new[] { Lesen("src", "Lernplattform.Android", "MainActivity.cs"), Lesen("src", "Stadtbau.Android", "MainActivity.cs") })
        {
            var m = Regex.Match(datei, @"public override void OnConfigurationChanged\([^)]*\)\s*\{(?<k>[^}]*)\}");
            Assert.True(m.Success, "OnConfigurationChanged fehlt");
            var koerper = m.Groups["k"].Value;
            // die Rueckmeldung an Avalonia gilt in jeder Fassung, nicht nur in der Probe-APK
            var vorProbe = koerper.Split("#if")[0];
            Assert.Contains("base.OnConfigurationChanged(newConfig);", vorProbe);
            Assert.Contains("Dichte.Nachziehen(this, newConfig, _dichte)", vorProbe);
            Assert.Matches(@"base\.OnCreate\(savedInstanceState\);\s*_dichte = Resources\?\.Configuration\?\.DensityDpi \?\? 0;", datei);
        }
        Assert.Contains(@"<Compile Include=""..\Lernplattform.Android\Dichte.cs"" Link=""Dichte.cs"" />", Lesen("src", "Stadtbau.Android", "Stadtbau.Android.csproj"));
    }
}
