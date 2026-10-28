using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W7 (Weisung P8-14, Sammelliste 129, Nico 4c, E2 Chat): Baeume und Gruen nur zur Darstellung. Ohne
/// Wuerfelkronen; nichts auf Strasse, Fussweg, Gleis, Wasser, Stehplatz, Haus oder einem Platz der Fest-Deko
/// irgendeiner Festzeit; an jedem Tag gleich; Bueschel, Steine, Beete und Parkbaeume sperren das freie Gehen,
/// jede Tuer bleibt erreichbar.
/// </summary>
public sealed class SpielGruenTests
{
    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static Stadt NeueStadt() => new(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);

    private static StadtSzene Bauen(string? fest, Spielstand? stand = null) => StadtSzene.Bauen(NeueStadt(), stand, Grafik, fest, FestDekoTests.Daten.Value);

    public static TheoryData<string> Zustaende => ["ohne", .. Festzeit.Alle];

    private static string? Fest(string zustand) => zustand == "ohne" ? null : zustand;

    [Fact]
    public void BaumartenOhneWuerfelkrone()
    {
        Assert.Equal(["wd-tree_1_a", "wd-tree_3_a", "wd-tree_4_a", "tree_large", "tree_small"], Gruen.Baeume.Select(b => b.Name));
        Assert.All(Gruen.Modelle, m => Assert.NotNull(Grafik.Modell(m)));
        Assert.DoesNotContain(Gruen.Modelle, m => m.StartsWith("wd-tree_2", StringComparison.Ordinal));
        // in der Stadt kommen alle Arten vor
        var szene = Bauen(null);
        var genutzt = szene.Stuecke.Select(s => s.Name).Concat(szene.GruenKlein!.Baeume.Select(s => s.Name)).ToHashSet();
        Assert.All(Gruen.Baeume, b => Assert.Contains(b.Name, genutzt));
    }

    /// <summary>Gleiche Verteilung bei jedem Aufbau und an jedem Tag (mit und ohne Festzeit), feste Anzahl je Art.</summary>
    [Fact]
    public void JedesMalUndJedenTagGleich()
    {
        var soll = Bauen(null).GruenKlein!;
        // W8b (Weisung P8-20): ein Busch weniger (75 -> 74): die Tueren sitzen auf der Wand (die Fest-Plaetze an den Tueren wandern mit) und der Gehweg am Stellwerk sperrt Platz
        // W11 (Weisung P8-45): zwei Grasbuschel mehr (122 -> 124), weil Platz frei wird (Bahnhof ohne Turm, Strassenenden buendig)
        Assert.Equal(new Dictionary<string, int> { ["baum"] = 5, ["busch"] = 74, ["gras"] = 124, ["stein"] = 20, ["beet"] = 22 }.OrderBy(p => p.Key),
            soll.Anzahl.OrderBy(p => p.Key));
        foreach (var fest in Festzeit.Alle.Prepend(null))
        {
            var ist = Bauen(fest).GruenKlein!;
            Assert.Equal(soll.Plaetze, ist.Plaetze);
            Assert.Equal(soll.Teile, ist.Teile);
            Assert.Equal(soll.Stuecke.Select(s => (s.Name, s.Lage)), ist.Stuecke.Select(s => (s.Name, s.Lage)));
            Assert.Equal(soll.Baeume.Select(s => (s.Name, s.Lage)), ist.Baeume.Select(s => (s.Name, s.Lage)));
        }
        // auch mit einem gespielten Stand (weitere Orte sichtbar) steht nichts auf den neuen Wegen
        Assert.NotEmpty(Bauen(null, SpielFingerabdruckTests.Gespielt.Value.Stand).GruenKlein!.Plaetze);
    }

    [Theory]
    [MemberData(nameof(Zustaende))]
    public void NieAufWegenHaeusernOderFestplaetzen(string zustand)
    {
        var fest = Fest(zustand);
        foreach (var stand in new[] { null, SpielFingerabdruckTests.Gespielt.Value.Stand })
        {
            var szene = Bauen(fest, stand);
            var festPlaetze = Festzeit.Alle.SelectMany(f => Bauen(f, stand).FestGesetzt).ToList();
            foreach (var (art, x, z, r) in szene.GruenKlein!.Plaetze)
            {
                var p = new Vector2(x, z);
                var wo = art + " bei " + x + "/" + z;
                foreach (var (linie, halb) in szene.BaumSperren)
                {
                    Assert.True(Kurve.Abstand(p, linie) >= halb + r + 0.02f - 1e-4f, wo + " auf einem Weg");
                }
                foreach (var o in szene.Orte)
                {
                    Assert.True(Vector2.Distance(p, new Vector2(o.StehX, o.StehZ)) >= 0.45f + r - 1e-4f, wo + " auf dem Stehplatz von " + o.Id);
                }
                foreach (var h in szene.Haeuser)
                {
                    Assert.False(x + r > h.Min.X && x - r < h.Max.X && z + r > h.Min.Z && z - r < h.Max.Z, wo + " im Haus");
                }
                foreach (var g in festPlaetze)
                {
                    Assert.True(Vector2.Distance(p, new Vector2(g.X, g.Z)) >= Math.Max(g.R, 0.12f) + r + 0.05f - 1e-4f, wo + " auf dem Platz von " + (g.Name ?? g.Art));
                }
            }
        }
    }

    /// <summary>Die Figur geht nie durch Bueschel, Steine, Beete und Parkbaeume, ueber Gras schon; jede Tuer bleibt erreichbar.</summary>
    [Theory]
    [MemberData(nameof(Zustaende))]
    public void HindernisseUndJedeTuerErreichbar(string zustand)
    {
        var fest = Fest(zustand);
        foreach (var stand in new[] { null, SpielFingerabdruckTests.Gespielt.Value.Stand })
        {
            var szene = Bauen(fest, stand);
            var raster = szene.Laufraster;
            var hindernisse = szene.GruenKlein!.Hindernisse.ToList();
            Assert.True(hindernisse.Count > 100);
            foreach (var (x, z, r) in hindernisse)
            {
                var (s0, z0) = raster.Zelle(x - r - 0.5, z - r - 0.5);
                var (s1, z1) = raster.Zelle(x + r + 0.5, z + r + 0.5);
                for (var sp = s0; sp <= s1; sp++)
                {
                    for (var ze = z0; ze <= z1; ze++)
                    {
                        var (mx, mz) = raster.Mitte((sp, ze));
                        Assert.False(raster.IstFrei((sp, ze)) && Math.Abs(mx - x) < r && Math.Abs(mz - z) < r, "Figur geht durch Gruen bei " + x + "/" + z);
                    }
                }
            }
            var start = szene.Orte[0];
            foreach (var o in szene.Orte.Skip(1))
            {
                var weg = raster.Weg((start.StehX, start.StehZ), (o.StehX, o.StehZ));
                Assert.True(weg.Count > 0, (fest ?? "ohne") + ": " + o.Id + " nicht erreichbar");
                var (lx, lz) = weg[^1];
                Assert.True(Math.Sqrt(Math.Pow(lx - o.StehX, 2) + Math.Pow(lz - o.StehZ, 2)) < 0.5, (fest ?? "ohne") + ": " + o.Id + " nur in der Naehe");
            }
        }
    }

    /// <summary>Die Parkbaeume kommen ins Netz der Stadt (immer gezeichnet), das Kleingruen in ein eigenes Netz.</summary>
    [Fact]
    public void NetzeDerStadt()
    {
        var szene = Bauen(null);
        var klein = szene.GruenNetzBauen()!;
        var erwartet = szene.GruenKlein!.Stuecke.Sum(s => Grafik.Modell(s.Name)!.Dreiecke) + (szene.GruenKlein.Teile.Count * 12);
        Assert.Equal(erwartet, klein.Indizes.Count / 3);
        var ohneParkbaeume = szene.NetzBauen().Indizes.Count / 3;
        Assert.True(ohneParkbaeume > szene.GruenKlein.Baeume.Sum(b => Grafik.Modell(b.Name)!.Dreiecke));
        Assert.Null(StadtSzene.Bauen(NeueStadt(), null).GruenNetzBauen());
    }
}
