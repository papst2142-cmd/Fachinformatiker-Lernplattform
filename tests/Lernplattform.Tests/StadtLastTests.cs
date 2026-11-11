using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Tests;

/// <summary>
/// Engine Stufe 2 Teil 1 (Weisung P12-176): <see cref="StadtLast"/> zaehlt jedes hochgeladene Netz genau einmal und
/// rechnet die Byte wie die Grafikkarte (9 float je Ecke, ein uint je Index). Stadt 1 liegt unter der vorgeschlagenen Grenze
/// der Netzdaten; die Zeit misst nur der Start-Test (Rechner-abhaengig).
/// </summary>
public sealed class StadtLastTests
{
    [Fact]
    public void Stadt1_NetzeEinmal_BytesWieDieGrafikkarte_UnterDerGrenze()
    {
        var e = StadtLast.Messen(() => SpielInstanzenTests.Szene(Jahreszeit.Winter, Festzeit.Weihnachten));
        var szene = SpielInstanzenTests.Szene(Jahreszeit.Winter, Festzeit.Weihnachten);
        var f = szene.Vorbereiten();
        var netze = StadtLast.Netze(f).ToList();
        Assert.Equal(netze.Count, netze.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Contains(f.Stadt, netze);
        Assert.All(f.Haeuser.Values, h => Assert.Contains(h, netze));
        Assert.All(f.Instanzen?.Saetze ?? [], s => Assert.Contains(s.Nah, netze));
        Assert.NotNull(f.StadtFern);
        Assert.Contains(f.StadtFern, netze);
        Assert.Equal((long)f.Stadt.AnzahlEcken * Netz.Zahlen * 4 + (long)f.Stadt.Indizes.Count * 4, StadtLast.Bytes(f.Stadt));
        // gleiche Szene, gleiche Netzdaten (Messung baut frisch, aendert nichts)
        Assert.Equal(netze.Sum(StadtLast.Bytes), e.Bytes);
        Assert.Equal(netze.Count, e.Netze);
        Assert.InRange(e.Mb, 1, StadtLast.NetzdatenGrenzeMb);
        Assert.True(e.BauenMs > 0 && e.VorbereitenMs > 0);
    }

    /// <summary>Gegenprobe: ein Netz, das doppelt in der Liste stuende (Haus nah = fern), zaehlt nur einmal.</summary>
    [Fact]
    public void Gegenprobe_GleichesNetzZweimal_ZaehltEinmal()
    {
        var f = SpielInstanzenTests.Szene(Jahreszeit.Sommer, null).Vorbereiten();
        var doppelt = f with { HaeuserFern = f.Haeuser, StadtFern = f.Stadt };
        Assert.Equal(StadtLast.Netze(f).Count() - (f.StadtFern is null ? 0 : 1) - (f.HaeuserFern?.Count ?? 0),
            StadtLast.Netze(doppelt).Count());
    }
}
