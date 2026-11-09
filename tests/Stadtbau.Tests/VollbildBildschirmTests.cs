using Lernplattform.Core.Spiel.Ton;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.25.1 (Weisung P12-109, Sammelliste 252): Bildschirm fuer das Vollbild im Werkzeug wie in der Lernplattform. Wahl nur
/// am PC ab zwei Bildschirmen, gemerkt in einstellungen.json des Werkzeugordners (Lage und Groesse, sonst Name), andere
/// Schluessel bleiben. Gegenproben: ohne Wahl, fehlender Bildschirm und „schon dort“ bewegen das Fenster nicht.
/// </summary>
public class VollbildBildschirmTests
{
    private static readonly Bildschirm Links = new(0, 0, 1920, 1080, true, "DELL");
    private static readonly Bildschirm Rechts = new(1920, 0, 2560, 1440, false, "LG");

    [Fact]
    public void Wahl_NurAmPc_AbZweiBildschirmen()
    {
        Assert.True(VollbildZielWahl.Zeigen(pc: true, 2));
        Assert.True(VollbildZielWahl.Zeigen(pc: true, 3));
        Assert.False(VollbildZielWahl.Zeigen(pc: true, 1));
        Assert.False(VollbildZielWahl.Zeigen(pc: false, 2));
    }

    [Fact]
    public void Ziel_GemerkterBildschirm_AuchNachNeustart_UndNachName()
    {
        Bildschirm[] alle = [Links, Rechts];
        Assert.Equal(1, VollbildZielWahl.Ziel(alle, Rechts, jetzt: 0, beimStart: false));
        // beim Start immer setzen (das Fenster hat noch keine Lage), auch wenn es dort zu stehen scheint
        Assert.Equal(1, VollbildZielWahl.Ziel(alle, Rechts, jetzt: 1, beimStart: true));
        // Bildschirm umgesteckt: gleiche Groesse an anderer Stelle -> ueber den Namen
        Bildschirm[] umgesteckt = [Rechts with { X = -2560 }, Links];
        Assert.Equal(0, VollbildZielWahl.Ziel(umgesteckt, Rechts, jetzt: 1, beimStart: false));
    }

    [Fact]
    public void Ziel_Gegenproben_BleibtWoEsIst()
    {
        Bildschirm[] alle = [Links, Rechts];
        Assert.Null(VollbildZielWahl.Ziel(alle, null, jetzt: 0, beimStart: true));
        Assert.Null(VollbildZielWahl.Ziel(alle, new Bildschirm(5000, 0, 800, 600, false, "weg"), jetzt: 0, beimStart: false));
        Assert.Null(VollbildZielWahl.Ziel(alle, Rechts, jetzt: 1, beimStart: false));
    }

    [Fact]
    public void Einstellungen_MerkenLesenLoeschen_AndereSchluesselBleiben()
    {
        var o = Daten.NeueAblage();
        Assert.Null(WerkzeugEinstellungen.VollbildBildschirm(o));
        WerkzeugEinstellungen.VollbildSetzen(o, true);
        WerkzeugEinstellungen.VollbildBildschirmSetzen(o, Rechts);
        var gelesen = WerkzeugEinstellungen.VollbildBildschirm(o);
        Assert.Equal(Rechts with { Haupt = false }, gelesen);
        Assert.True(WerkzeugEinstellungen.Vollbild(o));
        Assert.Contains("\"vollbild_bildschirm\"", File.ReadAllText(WerkzeugEinstellungen.Datei(o)), StringComparison.Ordinal);
        WerkzeugEinstellungen.VollbildBildschirmSetzen(o, null);
        Assert.Null(WerkzeugEinstellungen.VollbildBildschirm(o));
        Assert.DoesNotContain("vollbild_bildschirm", File.ReadAllText(WerkzeugEinstellungen.Datei(o)), StringComparison.Ordinal);
        Assert.True(WerkzeugEinstellungen.Vollbild(o));
    }

    [Fact]
    public void Einstellungen_KaputteDatei_WirdNieUeberschrieben()
    {
        var o = Daten.NeueAblage();
        File.WriteAllText(WerkzeugEinstellungen.Datei(o), "kaputt {");
        Assert.Throws<IOException>(() => WerkzeugEinstellungen.VollbildBildschirmSetzen(o, Rechts));
        Assert.Equal("kaputt {", File.ReadAllText(WerkzeugEinstellungen.Datei(o)));
        Assert.Null(WerkzeugEinstellungen.VollbildBildschirm(o));
    }
}
