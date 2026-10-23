using Lernplattform.Core.Diagnose;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P4-12 B6 (Nico 22.10.2026): Messwiederholung im Start ohne Bedienung. Ein einmaliger
/// Ausreisser wird gruen, eine dauerhaft langsame Handlung bleibt nach 3 Messungen rot, unter der
/// Grenze wird nie wiederholt, vor jeder Wiederholung kommen Zustand davor und Pause. Grenzen
/// bleiben unveraendert.
/// </summary>
public class MesswiederholungTests
{
    private sealed class Ablauf(params double?[] folgende)
    {
        private int _naechste;
        public List<string> Schritte { get; } = [];

        public Task<double?> Nochmal()
        {
            Schritte.Add("messen");
            return Task.FromResult(folgende[_naechste++]);
        }

        public Task Zurueck()
        {
            Schritte.Add("zurueck");
            return Task.CompletedTask;
        }

        public Task Warten(TimeSpan dauer)
        {
            Schritte.Add("pause " + dauer.TotalMilliseconds);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void FesteWerte()
    {
        Assert.Equal(3, Messwiederholung.Hoechstens);
        Assert.Equal(200, Messwiederholung.Pause.TotalMilliseconds);
    }

    [Fact]
    public async Task EinmaligerAusreisserWirdGruen()
    {
        var a = new Ablauf(412);
        var reihe = await Messwiederholung.MessenAsync(1119, 1000, a.Nochmal, a.Zurueck, a.Warten);
        Assert.False(reihe.Rot);
        Assert.Equal(new double?[] { 1119, 412 }, reihe.Werte);
        Assert.Equal(412, reihe.Bester);
        Assert.Equal(["zurueck", "pause 200", "messen"], a.Schritte);
    }

    [Fact]
    public async Task Kontrollfehler_DauerhaftLangsamBleibtRot()
    {
        var a = new Ablauf(1200, 1300, 999);
        var reihe = await Messwiederholung.MessenAsync(1100, 1000, a.Nochmal, a.Zurueck, a.Warten);
        Assert.True(reihe.Rot);
        // hoechstens 3 Messungen, die vierte (999) wird nie gemessen
        Assert.Equal(new double?[] { 1100, 1200, 1300 }, reihe.Werte);
        Assert.Equal(1100, reihe.Bester);
        Assert.Equal(["zurueck", "pause 200", "messen", "zurueck", "pause 200", "messen"], a.Schritte);
        Assert.Equal("Wechsel: 1100 ms ueber Grenze 1000 ms; Einzelmessungen 1100 ms / 1200 ms / 1300 ms (3 Messungen)",
            reihe.Fehlertext("Wechsel"));
    }

    [Fact]
    public async Task UnterDerGrenzeKeineWiederholung()
    {
        var a = new Ablauf(5);
        var reihe = await Messwiederholung.MessenAsync(1000, 1000, a.Nochmal, a.Zurueck, a.Warten);
        Assert.False(reihe.Rot);
        Assert.False(reihe.Wiederholt);
        Assert.Empty(a.Schritte);
    }

    [Fact]
    public async Task OhneZurueckNurPauseUndMessen()
    {
        var a = new Ablauf(150, 90);
        var reihe = await Messwiederholung.MessenAsync(120.5, 100, a.Nochmal, null, a.Warten);
        Assert.False(reihe.Rot);
        Assert.Equal(new double?[] { 120.5, 150, 90 }, reihe.Werte);
        Assert.Equal(["pause 200", "messen", "pause 200", "messen"], a.Schritte);
        Assert.Equal("120.5 ms / 150.0 ms / 90.0 ms", reihe.Einzelwerte("F1"));
    }

    [Fact]
    public async Task NichtFertigZaehltAlsUeberDerGrenze()
    {
        var a = new Ablauf(null, null);
        var reihe = await Messwiederholung.MessenAsync(null, 1000, a.Nochmal, a.Zurueck, a.Warten);
        Assert.True(reihe.Rot);
        Assert.Null(reihe.Bester);
        Assert.Equal("Suche: nicht fertig ueber Grenze 1000 ms; Einzelmessungen nicht fertig / nicht fertig / nicht fertig (3 Messungen)",
            reihe.Fehlertext("Suche"));
        var b = new Ablauf(30);
        Assert.False((await Messwiederholung.MessenAsync(null, 1000, b.Nochmal, b.Zurueck, b.Warten)).Rot);
    }

    [Fact]
    public void EinmaligerMesspunktBleibtStreng()
    {
        var rot = Messwiederholung.Einmal(1119, 1000);
        Assert.True(rot.Rot);
        Assert.False(rot.Wiederholbar);
        Assert.Equal("Karteikarten bis erste Karte: 1119 ms ueber Grenze 1000 ms; Einzelmessungen 1119 ms (einmaliger Messpunkt, nicht wiederholt)",
            rot.Fehlertext("Karteikarten bis erste Karte"));
        Assert.False(Messwiederholung.Einmal(1000, 1000).Rot);
        Assert.True(Messwiederholung.Einmal(null, 1000).Rot);
    }

    [Fact]
    public void GrenzenUnveraendert()
    {
        Assert.Equal(150, Farbreglerregel.GrenzeErsterSchritt);
        Assert.Equal(100, Farbreglerregel.GrenzeWeitereSchritte);
        var text = File.ReadAllText(Path.Combine(Wurzel(), "src", "Lernplattform.App", "Messung", "AutoMessung.cs")).Replace("\r\n", "\n");
        Assert.Contains("public const double Abbruch = 1000;", text, StringComparison.Ordinal);
        Assert.Contains("public const double Geraetegrenze = 400;", text, StringComparison.Ordinal);
        // Schritt 1 des Farbreglers bleibt streng: die Wiederholung beginnt erst ab Schritt 2
        Assert.Contains("Messwiederholung.MessenAsync(erste, Farbreglerregel.GrenzeWeitereSchritte,", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Messwiederholung.MessenAsync(erste, Farbreglerregel.GrenzeErsterSchritt", text, StringComparison.Ordinal);
    }

    private static string Wurzel()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "global.json")))
        {
            ordner = ordner.Parent;
        }
        Assert.NotNull(ordner);
        return ordner.FullName;
    }
}
