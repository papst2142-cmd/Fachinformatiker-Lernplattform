using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-218 Punkt 3 (Nico b: „aber mittig jeweils vor das Tor sollte man es setzen koennen, ohne dass eine Fehlermeldung
/// kommen kann“): ein Abstellgleis zweigt mit einer Weiche vom Hauptgleis ab und endet genau in der Mitte eines Tors, ohne
/// roten oder gelben Fund; geprueft an jedem Tor von Lokschuppen und Betriebswerkstatt (Modelle aus Nicos Teststadt). Ein
/// frei endendes Abstellgleis bekommt einen Prellbock, im Schuppen keiner. Kein Zug faehrt hinein.
/// </summary>
public class AbstellgleisTests
{
    private static Spieldaten D => Daten.Spiel;

    private const float Haupt = 4f;

    /// <summary>Leere Stadt 40 × 30 mit geradem Hauptgleis bei y = 4, Lokschuppen und Werkstatt mit den Toren zum Gleis.</summary>
    private static Projekt Bahnhofsfeld()
    {
        var nico = TorEingangTests.Nico();
        var modelle = (PyObjekt)Aussen.Karte(nico.AussenStand)["modelle"]!;
        var p = Projekt.Neu("Abstellgleis", D, Daten.NeueAblage(), null, (40, 30));
        p.AussenAendern("Feld", a =>
        {
            var karte = Aussen.Karte(a);
            if (Get(karte, "modelle") is not PyObjekt m)
            {
                m = [];
                Py.Setzen(karte, "modelle", m);
            }
            Py.Setzen(m, "lokschuppen", Py.Kopie(modelle["lokschuppen"]));
            Py.Setzen(m, "werkstatt", Py.Kopie(modelle["werkstatt"]));
            var land = Aussen.Landschaft(a);
            land.Add(new PyObjekt { { "typ", "bahn" }, { "punkte", new List<object?> { P(0, Haupt), P(40, Haupt) } }, { "radien", new List<object?> { 3.0, 3.0 } } });
            land.Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", "lokschuppen" }, { "x", 10L }, { "y", 17L }, { "dreh", 2L } });
            land.Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", "werkstatt" }, { "x", 26L }, { "y", 17L }, { "dreh", 2L } });
        });
        return p;
    }

    private static List<object?> P(double x, double y) => [x, y];

    private static List<Vector2> Linie(object? o) => [.. ((List<object?>)o!).OfType<List<object?>>().Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])))];

    private static List<string> Funde(Projekt p) => [.. p.PruefenAussen().Select(f => (f.Rot ? "rot: " : "gelb: ") + f.Text).Order()];

    private static List<Ziehpunkt> Tore(Projekt p, string modell)
    {
        var s = TorEingangTests.Gebaeude(p, modell);
        // nur die Tore dieses Gebaeudes (die Punkte liegen auf seiner Grundflaeche)
        return [.. AussenBearbeitung.Torpunkte(p.AussenStand).Where(t => AussenBearbeitung.HausDrin(p.AussenStand, s, t.Lage, 0.01)).OrderBy(t => t.Lage.X)];
    }

    private static List<Vector2> Gleis(Projekt p, int nr) => Linie(Objekt(Aussen.Landschaft(p.AussenStand)[nr])["punkte"]);

    /// <summary>Abstellgleis von der Weiche (Tipp auf dem Hauptgleis bei <paramref name="weicheX"/>) bis zum Tor.</summary>
    private static (AussenStelle? Stelle, string? Grund) Ziehen(Projekt p, float weicheX, Vector2 tor)
    {
        AussenStelle? s = null;
        string? grund = null;
        p.AussenAendern("Bahn", a => s = AussenBearbeitung.Ziehen(a, ZiehArt.Bahn, weicheX, Haupt + 0.2, tor.X + 0.3, tor.Y - 0.2, true, out grund));
        return (s, grund);
    }

    private static int Prellboecke(Projekt p) => p.SzeneOhneLaternen(p.AussenStand, true)!.Teile.Count(t => t.Farbe == StadtSzene.PrellbockRot);

    public static TheoryData<string, int> JedesTor => new() { { "lokschuppen", 0 }, { "lokschuppen", 1 }, { "lokschuppen", 2 }, { "werkstatt", 0 }, { "werkstatt", 1 }, { "werkstatt", 2 } };

    [Theory]
    [MemberData(nameof(JedesTor))]
    public void JedesTor_AbstellgleisMittigVorsTor_OhneFund_GegenprobenAlteRegelUndOhneFangen(string modell, int nr)
    {
        var p = Bahnhofsfeld();
        var vorher = Funde(p);
        var tore = Tore(p, modell);
        Assert.Equal(3, tore.Count);
        var tor = tore[nr];
        // das Tor zeigt zum Hauptgleis (das Gebaeude ist um 180 Grad gedreht)
        Assert.True(Vector2.Distance(tor.Weg!.Value, -Vector2.UnitY) < 1e-3f, $"{tor.Weg}");
        var (s, grund) = Ziehen(p, tor.Lage.X - 7, tor.Lage);
        Assert.Null(grund);
        var g = Gleis(p, s!.Nummer);
        // ein Ende genau in der Tormitte, das andere auf dem Hauptgleis (Weiche), in dessen Richtung
        Assert.True(Vector2.Distance(g[^1], tor.Lage) < 1e-3f, $"{g[^1]} {tor.Lage}");
        Assert.True(MathF.Abs(g[0].Y - Haupt) < StadtbauPruefung.WeicheFang, $"{g[0]}");
        Assert.True(MathF.Abs(g[1].Y - Haupt) < StadtbauPruefung.WeicheFang, $"{g[1]}");
        // keine neuen Funde (rot oder gelb), kein Prellbock (Weiche und Tor)
        Assert.Equal(vorher, Funde(p));
        Assert.Equal(0, Prellboecke(p));

        // Gegenprobe 1: ohne Weichen-Regel (wie bis A 0.69.0) ist dasselbe Gleis rot „zu nah“
        StadtbauPruefung.WeicheAltFuerTests = true;
        try
        {
            Assert.Contains(Funde(p), f => f.StartsWith("rot: ", StringComparison.Ordinal) && f.Contains("zu nah", StringComparison.Ordinal));
        }
        finally
        {
            StadtbauPruefung.WeicheAltFuerTests = false;
        }

        // Gegenprobe 2: ohne Fangen von Tor und Weiche (wie bis S 0.45.0) liegt das Ende nicht mittig im Tor
        var q = Bahnhofsfeld();
        AussenBearbeitung.WeicheTorAltFuerTests = true;
        try
        {
            var (s2, _) = Ziehen(q, tor.Lage.X - 7, tor.Lage);
            Assert.True(s2 is null || Vector2.Distance(Gleis(q, s2.Nummer)[^1], tor.Lage) > 0.05f);
        }
        finally
        {
            AussenBearbeitung.WeicheTorAltFuerTests = false;
        }
    }

    /// <summary>Alle drei Tore des Lokschuppens zugleich (zwei Weichen von links, eine von rechts): ohne Fund.</summary>
    [Fact]
    public void Lokschuppen_AlleDreiTore_Zugleich_OhneFund()
    {
        var p = Bahnhofsfeld();
        var vorher = Funde(p);
        var tore = Tore(p, "lokschuppen");
        Assert.Null(Ziehen(p, tore[0].Lage.X - 7, tore[0].Lage).Grund);
        Assert.Null(Ziehen(p, tore[1].Lage.X - 9.5f, tore[1].Lage).Grund);
        Assert.Null(Ziehen(p, tore[2].Lage.X + 7, tore[2].Lage).Grund);
        var bahnen = Aussen.Landschaft(p.AussenStand).OfType<PyObjekt>().Count(t => Py.Gleich(t["typ"], "bahn"));
        Assert.Equal(4, bahnen);
        foreach (var t in tore)
        {
            Assert.Contains(Aussen.Landschaft(p.AussenStand).OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "bahn")), l => Vector2.Distance(Linie(l["punkte"])[^1], t.Lage) < 1e-3f);
        }
        Assert.Equal(vorher, Funde(p));
        Assert.Equal(0, Prellboecke(p));
    }

    /// <summary>Ein frei endendes Abstellgleis hat einen Prellbock am freien Ende; Gegenprobe ohne Prellbock; Stadt 1 hat keinen.</summary>
    [Fact]
    public void FreiesEnde_HatPrellbock_GegenprobeAlt_Stadt1Keiner()
    {
        var p = Bahnhofsfeld();
        var vorher = Funde(p);
        AussenStelle? s = null;
        string? grund = null;
        p.AussenAendern("Bahn", a => s = AussenBearbeitung.Ziehen(a, ZiehArt.Bahn, 30, Haupt + 0.2, 36, 12, true, out grund));
        Assert.Null(grund);
        Assert.Equal(vorher, Funde(p));
        Assert.Equal(2, Prellboecke(p));   // Pufferbohle: rote Teile links und rechts der weissen Mitte
        StadtSzene.PrellbockAltFuerTests = true;
        try
        {
            Assert.Equal(0, Prellboecke(p));
        }
        finally
        {
            StadtSzene.PrellbockAltFuerTests = false;
        }
        var stadt1 = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        Assert.Equal(0, Prellboecke(stadt1));
    }

    /// <summary>Ein Gleis, das auf dem Hauptgleis beginnt und daneben bleibt, ist keine Weiche: weiter rot.</summary>
    [Fact]
    public void GleisNebenDemHauptgleis_OhneAuseinanderlaufen_BleibtRot()
    {
        var p = Bahnhofsfeld();
        p.AussenAendern("Bahn", a => Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", "bahn" }, { "punkte", new List<object?> { P(5, Haupt), P(12, Haupt + 0.5) } }, { "radien", new List<object?> { 3.0, 3.0 } },
        }));
        Assert.Contains(Funde(p), f => f.StartsWith("rot: ", StringComparison.Ordinal) && f.Contains("zu nah", StringComparison.Ordinal));
    }
}
