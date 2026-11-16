using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 3): grosse Karten rechnet das Werkzeug mit Nachladen wie das Spiel. Geprueft wird
/// Nicos Ziel: die Testkarte ist nicht mehr „Stadt zu teuer“ (jeder Stadtteil und das gleichzeitig Geladene unter den
/// Grenzen), ein zu teurer Stadtteil ist ein roter Fund mit seinem Namen (Gegenprobe mit vielen Stuecken), kleine Karten
/// (Stadt 1) rechnen wie bisher.
/// </summary>
[Collection("Testkarte")]
public class GrenzenLiveNachladenTests
{
    private static Spieldaten D => Daten.Spiel;

    internal static StadtFassung TestkarteStand(Action<PyObjekt>? aendern = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt"));
        var p = Projekt.AusStadtDatei(bytes, D, Daten.NeueAblage(), "Testkarte");
        if (aendern is not null)
        {
            p.AussenAendern("Test", a =>
            {
                aendern(a);
                return 0;
            });
        }
        return p.Stand();
    }

    private static readonly Lazy<GrenzenLive> Testkarte = new(() => GrenzenLive.Fuer(TestkarteStand(), D));

    [Fact]
    public void Testkarte_JederStadtteilUnterDerGrenze_KeinFund()
    {
        var g = Testkarte.Value;
        Assert.NotNull(g.Kacheln);
        Assert.Equal(256, g.Kacheln!.Count);
        Assert.Equal(256, g.Kacheln.Select(k => k.Kachel).Distinct().Count());
        Assert.All(g.Kacheln, k => Assert.True(k.Dreiecke > 0 && !k.Ueber, k.Kachel.Name + " " + k.Dreiecke + "/" + k.Aufrufe));
        Assert.False(g.Ueber);
        Assert.Null(g.Fund());
        Assert.True(g.GeladenBytes / 1048576.0 <= StadtLast.NetzdatenGrenzeMb);
        Assert.Equal(256 * GrenzenLive.NachladenDrehungen.Length, g.Bilder);
        Assert.Equal((0L, 0), g.Gesamt);
        Assert.Equal(g.Teuerste!.Dreiecke, g.Nah.Dreiecke);
    }

    [Fact]
    public void Stadt1_RechnetWieBisher_OhneStadtteile()
    {
        Assert.False(GrenzenLive.MitNachladenFuer(64, 64));
        Assert.True(GrenzenLive.MitNachladenFuer(64.5f, 64));
        var g = GrenzenLive.Fuer(D.Fassung, D);
        Assert.Null(g.Kacheln);
        Assert.True(g.Gesamt.Dreiecke > 0);
    }

    [Fact]
    public void Gegenprobe_OhneSichtkegel_StadtteilTeurer()
    {
        GrenzenLive.OhneSparenFuerTests = true;
        GrenzenLive ohne;
        try
        {
            ohne = GrenzenLive.Fuer(TestkarteStand(), D);
        }
        finally
        {
            GrenzenLive.OhneSparenFuerTests = false;
        }
        Assert.True(ohne.Nah.Dreiecke > Testkarte.Value.Nah.Dreiecke, ohne + " gegen " + Testkarte.Value);
    }

    [Fact]
    public void ZuTeuererStadtteil_RoterFundMitNamenUndStelle()
    {
        var g = Testkarte.Value;
        var kk = g.Kacheln!.ToList();
        var c3 = kk.FindIndex(k => k.Kachel.Name == "C3");
        var d4 = kk.FindIndex(k => k.Kachel.Name == "D4");
        kk[c3] = kk[c3] with { Dreiecke = StadtbauGrenzen.Dreiecke + 500 };
        kk[d4] = kk[d4] with { Aufrufe = StadtbauGrenzen.Aufrufe + 1 };
        var teuer = g with { Kacheln = kk, Nah = (StadtbauGrenzen.Dreiecke + 500, 10) };
        Assert.True(teuer.Ueber);
        var f = teuer.Fund()!;
        Assert.True(f.Rot);
        Assert.StartsWith("Stadt zu teuer: im Stadtteil", f.Text, StringComparison.Ordinal);
        Assert.Contains("Auch zu teuer:", f.Text, StringComparison.Ordinal);
        Assert.Contains("C3", f.Text, StringComparison.Ordinal);
        Assert.Contains("D4", f.Text, StringComparison.Ordinal);
        // Gegenprobe: genau auf der Grenze kein Fund
        kk = [.. g.Kacheln!];
        kk[c3] = kk[c3] with { Dreiecke = StadtbauGrenzen.Dreiecke, Aufrufe = StadtbauGrenzen.Aufrufe };
        Assert.Null((g with { Kacheln = kk }).Fund());
        // zu viel gleichzeitig geladen: roter Fund mit dem Stadtteil
        var mb = g with { GeladenBytes = (long)((StadtLast.NetzdatenGrenzeMb + 1) * 1048576) };
        Assert.True(mb.Ueber);
        Assert.Contains("MB Netzdaten gleichzeitig geladen", mb.Fund()!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void VieleStueckeInEinemStadtteil_RoterFundMitSeinemNamen()
    {
        var gross = Katalog.Aktiv!.Eintraege.Where(e => e.Art == KatalogArt.Modell).MaxBy(e => e.Dreiecke)!;
        var zahl = (int)Math.Min(400, (StadtbauGrenzen.Dreiecke / Math.Max(1, gross.Dreiecke)) + 20);
        var stand = TestkarteStand(a =>
        {
            for (var i = 0; i < zahl; i++)
            {
                AussenBearbeitung.Setzen(a, AussenArt.Katalog, 38 + (i % 20 * 0.1), 38 + (i / 20 * 0.1), raster: false, modell: gross.Id);
            }
        });
        var g = GrenzenLive.Fuer(stand, D);
        var f = g.Fund();
        Assert.NotNull(f);
        Assert.Contains("Stadtteil " + Stadtteile.Von(40, 40, 256, 256).Name, f!.Text, StringComparison.Ordinal);
        Assert.True(g.Kacheln!.Single(k => k.Kachel == Stadtteile.Von(40, 40, 256, 256)).Ueber);
    }
}
