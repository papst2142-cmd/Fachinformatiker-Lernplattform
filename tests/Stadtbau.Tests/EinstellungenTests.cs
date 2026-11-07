using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// einstellungen.json des Werkzeugs: Vollbild (S 0.5.1) und Raster zeigen im Gebaeude-Editor (S 0.9.1, Weisung P12-52,
/// Vorgabe an, gemerkt). Unbekannte Schluessel bleiben, eine kaputte Datei wird nie still ueberschrieben.
/// </summary>
public class EinstellungenTests
{
    [Fact]
    public void RasterZeigen_VorgabeAn_GemerktNebenVollbild()
    {
        var o = Daten.NeueAblage();
        Assert.True(WerkzeugEinstellungen.RasterZeigen(o));
        WerkzeugEinstellungen.VollbildSetzen(o, true);
        WerkzeugEinstellungen.RasterZeigenSetzen(o, false);
        Assert.False(WerkzeugEinstellungen.RasterZeigen(o));
        Assert.True(WerkzeugEinstellungen.Vollbild(o));
        WerkzeugEinstellungen.RasterZeigenSetzen(o, true);
        Assert.True(WerkzeugEinstellungen.RasterZeigen(o));
        Assert.True(WerkzeugEinstellungen.Vollbild(o));
    }

    [Fact]
    public void RasterZeigen_FremderWertOderKaputteDatei_BleibtAn_KaputteDateiNichtUeberschrieben()
    {
        var o = Daten.NeueAblage();
        File.WriteAllText(WerkzeugEinstellungen.Datei(o), "{\"raster_zeigen\": \"nein\", \"anders\": 1}");
        Assert.True(WerkzeugEinstellungen.RasterZeigen(o));
        WerkzeugEinstellungen.RasterZeigenSetzen(o, false);
        Assert.Contains("\"anders\": 1", File.ReadAllText(WerkzeugEinstellungen.Datei(o)), StringComparison.Ordinal);
        File.WriteAllText(WerkzeugEinstellungen.Datei(o), "{kaputt");
        Assert.True(WerkzeugEinstellungen.RasterZeigen(o));
        Assert.Throws<IOException>(() => WerkzeugEinstellungen.RasterZeigenSetzen(o, false));
        Assert.Equal("{kaputt", File.ReadAllText(WerkzeugEinstellungen.Datei(o)));
    }
}
