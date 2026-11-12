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
/// Funde 312, 313, 315 und 316 aus Nicos Teststadt S 0.43.0 (Weisungen P12-207/P12-211). Die Teststadt liegt als Stadt-Datei in
/// <c>Testdaten/nico/</c> und wird wie bei Nico über „Stadt-Datei als neues Projekt öffnen“ geladen. Jeder Fund hat eine
/// Gegenprobe mit dem Verhalten vor dem Fix.
/// </summary>
public class TeststadtS0430Tests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly Lazy<byte[]> Datei = new(() =>
        File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "teststadt_S0.43.0.fistadt")));

    private static Projekt Nico(string name = "Nico Teststadt") => Projekt.AusStadtDatei(Datei.Value, D, Daten.NeueAblage(), name);

    private static AussenStelle Gebaeude(Projekt p, string modell) =>
        new(AussenArt.Gebaeude, Aussen.Landschaft(p.AussenStand).FindIndex(t => t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], modell)));

    private static List<string> RoteFunde(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text)];

    /// <summary>„Tür dazu“ wie in der Oberfläche: Platz auf der Fassade rechnen, dann ein Schritt.</summary>
    private static string? TuerDazu(Projekt p, AussenStelle s)
    {
        var platz = GebaeudeTuer.DazuPlatz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s, einrasten: true);
        string? hinweis = null;
        p.AussenAendern(Texte.TuerDazu, a => hinweis = GebaeudeTuer.DazuAn(a, s, true, platz));
        return hinweis;
    }

    // ---- 312: Betriebswerkstatt ----------------------------------------------------------------------------------

    [Fact]
    public void Fund312_Betriebswerkstatt_TuerDazu_AufDasRolltor_OhneFund()
    {
        var p = Nico();
        var s = Gebaeude(p, "werkstatt");
        Assert.Empty(RoteFunde(p));
        Assert.Null(TuerDazu(p, s));
        Assert.Empty(RoteFunde(p));
        var z = Blueprint.ZierwegVon(p.AussenStand, s)!;
        Assert.True(Py.Wahr(z["modell_tuer"]));
        // im Spiel: die Tuer ist das Rolltor des Modells (keine zweite Tuer davor), mitten auf ihm wie in Stadt 1
        var sz = p.SzeneOhneLaternen(p.AussenStand, true)!;
        var t = sz.Tueren.Single(x => x.Ort == "werkstatt");
        Assert.True(t.Modell);
        var haus = sz.Haeuser.First(h => h.Id.StartsWith("werkstatt#", StringComparison.Ordinal));
        Assert.True(Vector2.Distance(t.Mitte - new Vector2(haus.Min.X, haus.Min.Z), new Vector2(2.7f, 2.16f)) < 0.01f, t.Mitte.ToString());
    }

    [Fact]
    public void Fund312_Betriebswerkstatt_TuerSetzenVonHand_RastetAufsRolltor()
    {
        var p = Nico();
        var s = Gebaeude(p, "werkstatt");
        TuerDazu(p, s);
        var f = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s)!;
        // ein Tipp 0,15 neben der Tormitte ohne Einrasten und 0,4 daneben mit Einrasten: die Tuer kommt auf die Tormitte
        var wand = Enumerable.Range(0, f.Waende.Count).Single(i => f.Waende[i].Aussen == new Vector2(0, 1));
        var w = f.Waende[wand];
        var mitte = w.Bei(w.Laenge / 2);
        var platz = f.Finden(w.Bei((w.Laenge / 2) + 0.15f) + new Vector2(0, 0.1f), null, einrasten: false);
        Assert.True(platz.Eigene);
        Assert.Equal(TuerFassade.Stelle.Modelltuer, platz.Art);
        Assert.True(Vector2.Distance(platz.Punkt, mitte) < 0.01f, platz.Punkt.ToString());
        var weiter = f.Finden(w.Bei((w.Laenge / 2) + 0.4f) + new Vector2(0, 0.1f), null, einrasten: true);
        Assert.True(weiter.Eigene);
        Assert.True(Vector2.Distance(weiter.Punkt, mitte) < 0.01f, weiter.Punkt.ToString());
        // ohne Einrasten neben dem Tor: keine glatte Stelle, kein Tor
        Assert.False(f.Finden(w.Bei((w.Laenge / 2) + 1.2f) + new Vector2(0, 0.1f), null, einrasten: false).Eigene);
        p.AussenAendern(Texte.TuerSetzenSchritt, a => TuerSetzen.SetzenAn(a, s, platz));
        Assert.Empty(RoteFunde(p));
        // eine glatte Stelle gibt es an dieser Fassade sonst nicht (Erdgeschoss ist zurueckgesetzt)
        Assert.DoesNotContain(f.ProbenVon(wand), q => q.Art == TuerFassade.Stelle.Glatt);
    }

    [Fact]
    public void Fund312_Gegenprobe_OhneTorAlsZiel_Rot()
    {
        TuerBuendig.ModellTuerAltFuerTests = true;
        try
        {
            var p = Nico("Alt");
            TuerDazu(p, Gebaeude(p, "werkstatt"));
            Assert.Contains(RoteFunde(p), x => x.Contains("sitzt nicht sauber", StringComparison.Ordinal));
        }
        finally
        {
            TuerBuendig.ModellTuerAltFuerTests = false;
        }
    }

    [Fact]
    public void Fund312_Gegenprobe_SpielOhneFeldModellTuer_ZeichnetZweiteTuer_Rot()
    {
        var p = Nico();
        TuerDazu(p, Gebaeude(p, "werkstatt"));
        StadtSzene.ZierModellTuerAltFuerTests = true;
        try
        {
            var sz = p.SzeneOhneLaternen((PyObjekt)Py.Kopie(p.AussenStand)!, true)!;
            Assert.False(sz.Tueren.Single(x => x.Ort == "werkstatt").Modell);
            Assert.Contains(RoteFunde(p), x => x.Contains("sitzt nicht sauber", StringComparison.Ordinal));
        }
        finally
        {
            StadtSzene.ZierModellTuerAltFuerTests = false;
        }
    }

    [Fact]
    public void Fund312_Stadt1_Betriebswerkstatt_TuerSetzen_BehaeltDasRolltor()
    {
        var p = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        var s = new AussenStelle(AussenArt.Haus, 0, Ort: "betriebswerkstatt");
        var f = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s)!;
        var tuer = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        var platz = f.Finden(tuer + new Vector2(0.3f, 0.1f), null, einrasten: true);
        Assert.True(platz.Eigene);
        var vorher = RoteFunde(p);
        p.AussenAendern(Texte.TuerSetzenSchritt, a => TuerSetzen.SetzenAn(a, s, platz));
        // Stadt 1 hat nur den bekannten Fund Laterne 20 (Sammelliste 220), die Tuer bringt keinen dazu
        Assert.Equal(vorher, RoteFunde(p));
        Assert.DoesNotContain(RoteFunde(p), x => x.Contains("Tür", StringComparison.Ordinal));
        var mt = Objekt(Get(Objekt(Get(Aussen.Stadtdaten(p.AussenStand), "darstellung")), "modell_tuer"));
        Assert.Equal(0L, mt["betriebswerkstatt"]);
    }

    // ---- 316: Lokschuppen (Gebaeude ohne Grafik des Katalogs) ----------------------------------------------------

    [Fact]
    public void Fund316_Lokschuppen_TuerDazu_MittenAufDemAnbau_OhneFund()
    {
        var p = Nico();
        var s = Gebaeude(p, "lokschuppen");
        Assert.Null(TuerDazu(p, s));
        Assert.Empty(RoteFunde(p));
        // Anbau nach der halben Drehung: x 18,5, z 34,2 bis 35,2; die Tuer (mit Rahmen) ganz auf seiner Wand, mittig
        var t = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        Assert.Equal(18.5f, t.X, 0.001f);
        Assert.InRange(t.Y, 34.7f - 0.05f, 34.7f + 0.05f);
        var halb = StadtTueren.HalbRahmen;
        Assert.InRange(t.Y, 34.2f + halb, 35.2f - halb);
    }

    [Fact]
    public void Fund316_Lokschuppen_Fassade_GlattNurAmAnbau()
    {
        var p = Nico();
        var s = Gebaeude(p, "lokschuppen");
        TuerDazu(p, s);
        var f = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s)!;
        Assert.Equal(4, f.Waende.Count);
        for (var w = 0; w < 4; w++)
        {
            var glatt = f.ProbenVon(w).Where(q => q.Art == TuerFassade.Stelle.Glatt).ToList();
            if (f.Waende[w].Aussen == new Vector2(-1, 0))
            {
                Assert.NotEmpty(glatt);
                Assert.All(glatt, q => Assert.InRange(f.Waende[w].Bei(q.T).Y, 34.2f + StadtTueren.HalbRahmen - 0.01f, 35.2f - StadtTueren.HalbRahmen + 0.01f));
            }
            else
            {
                // Tore, Fenster: dort waere die Tuer nicht buendig
                Assert.Empty(glatt);
            }
        }
    }

    [Fact]
    public void Fund316_Gegenprobe_OhneFassade_TuerNebenDemAnbau()
    {
        TuerFassade.ProzeduralAltFuerTests = true;
        try
        {
            var p = Nico("Alt");
            var s = Gebaeude(p, "lokschuppen");
            Assert.Null(TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s));
            TuerDazu(p, s);
            var t = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
            // in der Mitte der Kastenwand: der Rahmen ragt ueber den Anbau hinaus (halb in der Luft)
            Assert.True(t.Y + StadtTueren.HalbRahmen > 35.2f + 0.01f || t.Y - StadtTueren.HalbRahmen < 34.2f - 0.01f, t.ToString());
        }
        finally
        {
            TuerFassade.ProzeduralAltFuerTests = false;
        }
    }

    // ---- 315: schwebende Platten -----------------------------------------------------------------------------------

    /// <summary>Platten eines Hauses ganz ueber der Traufe der Bloecke (Quader ueber der Wand, kein Dach): sie schweben.</summary>
    private static int Schwebend(StadtSzene sz, StadtHaus h)
    {
        var teile = Enumerable.Range(h.TeileVon, h.TeileBis - h.TeileVon).Select(i => sz.Teile[i]).ToList();
        var traufe = teile.Where(t => t.Art == TeilArt.Quader && t.Y1 <= 0.001f).Max(t => t.Y2);
        return teile.Count(t => t.Art == TeilArt.Quader && t.Y1 >= traufe - 0.001f);
    }

    [Fact]
    public void Fund315_Lokschuppen_KeineSchwebendenPlatten_Stadt1Gleich_Gegenprobe()
    {
        var p = Nico();
        var sz = p.SzeneOhneLaternen(p.AussenStand, true)!;
        Assert.Equal(0, Schwebend(sz, sz.Haeuser.First(h => h.Id.StartsWith("lokschuppen#", StringComparison.Ordinal))));
        var s1 = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        var ort = AussenBearbeitung.Orte(s1.AussenStand).First(o => Py.Gleich(o["modell"], "lokschuppen"));
        var sz1 = s1.SzeneOhneLaternen(s1.AussenStand, true)!;
        var teile1 = sz1.Haeuser.First(h => h.Id == Py.Str(ort["id"]));
        Assert.Equal(0, Schwebend(sz1, teile1));
        StadtSzene.KreisAltFuerTests = true;
        try
        {
            // Stadt 1: der Lokschuppen als Ort hatte keine schwebenden Platten, er bleibt gleich (Fassung 0 bitgleich)
            var alt = s1.SzeneOhneLaternen((PyObjekt)Py.Kopie(s1.AussenStand)!, true)!;
            var altHaus = alt.Haeuser.First(h => h.Id == Py.Str(ort["id"]));
            Assert.Equal(teile1.TeileBis - teile1.TeileVon, altHaus.TeileBis - altHaus.TeileVon);
            var altNico = p.SzeneOhneLaternen((PyObjekt)Py.Kopie(p.AussenStand)!, true)!;
            Assert.Equal(3, Schwebend(altNico, altNico.Haeuser.First(h => h.Id.StartsWith("lokschuppen#", StringComparison.Ordinal))));
        }
        finally
        {
            StadtSzene.KreisAltFuerTests = false;
        }
    }

    // ---- 313: Haltepunkt mit Bahnsteig ----------------------------------------------------------------------------

    [Fact]
    public void Fund313_Haltepunkt_TuerDazu_BleibtGanz_OhneFund_Gegenprobe()
    {
        var p = Nico();
        var s = Gebaeude(p, "haltepunkt");
        Assert.Null(TuerDazu(p, s));
        Assert.Empty(RoteFunde(p));
        // kein abgetrennter Bahnsteig, die Tuer auf der Wand des Wartehauses (Seite weg vom Bahnsteig)
        Assert.DoesNotContain(Aussen.Landschaft(p.AussenStand), t => t is PyObjekt g && Py.Str(g["modell"]).StartsWith("bahnsteig_haltepunkt", StringComparison.Ordinal));
        var t = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        Assert.Equal(36.3f, t.Y, 0.001f);
        Assert.InRange(t.X, 6.5f + StadtTueren.HalbRahmen, 7.8f - StadtTueren.HalbRahmen);
        GebaeudeTuer.BahnsteigTeilenAltFuerTests = true;
        try
        {
            var q = Nico("Alt");
            TuerDazu(q, Gebaeude(q, "haltepunkt"));
            Assert.Contains(RoteFunde(q), x => x.Contains("stehen ineinander", StringComparison.Ordinal));
        }
        finally
        {
            GebaeudeTuer.BahnsteigTeilenAltFuerTests = false;
        }
    }

    [Fact]
    public void Fund313_Haltepunkt_AmGeradenGleis_RastetEin_GebaeudeHinterDemBahnsteig()
    {
        // Nicos Gleis ist nur von x 0 bis 4,78 gerade (y 40), danach schraeg: dort setzen wir den Haltepunkt suedlich des Gleises
        var p = Nico();
        var s = Gebaeude(p, "haltepunkt");
        var a = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var g = Objekt(Aussen.Landschaft(a)[s.Nummer]);
        Py.Setzen(g, "x", 1.0);
        Py.Setzen(g, "y", 40.5);
        Assert.True(AussenBearbeitung.BahnhofAnGleis(a, s));
        var teile = Get(AussenBearbeitung.Modelle(a), "haltepunkt");
        var (x, y, dreh) = (Py.F(g["x"]), Py.F(g["y"]), AussenBearbeitung.HausDreh(a, s));
        var r = Bahnsteig.Rechtecke(teile, x, y, dreh).Single();
        Assert.Equal(40f + AussenBearbeitung.BahnsteigKante, r.Min.Y, 0.011f);
        // das Wartehaus steht ganz hinter der Gleiskante des Bahnsteigs (nicht naeher am Gleis)
        var mitte = new Vector2((float)x, (float)y) + Hausdrehung.Mitte(teile);
        var block = Objekt(((List<object?>)teile!).OfType<PyObjekt>().First(t => Py.Gleich(t["art"], "block")));
        var b1 = Hausdrehung.Drehen(new Vector2((float)(x + Py.F(block["x"])), (float)(y + Py.F(block["y"]))), mitte, dreh);
        var b2 = Hausdrehung.Drehen(new Vector2((float)(x + Py.F(block["x"]) + Py.F(block["w"])), (float)(y + Py.F(block["y"]) + Py.F(block["t"]))), mitte, dreh);
        Assert.True(MathF.Min(b1.Y, b2.Y) >= r.Min.Y - 0.001f, $"Block {b1} {b2}, Bahnsteig {r}");
    }

    [Fact]
    public void Fund313_Haltepunkt_AmSchraegenGleis_RastetNicht()
    {
        // wie bei Nico: neben dem schraegen Stueck gibt es kein gerades Gleis in Nord-Süd- oder Ost-West-Richtung (Kurzplan 314)
        var p = Nico();
        var a = (PyObjekt)Py.Kopie(p.AussenStand)!;
        Assert.False(AussenBearbeitung.BahnhofAnGleis(a, Gebaeude(p, "haltepunkt")));
    }

    [Fact]
    public void Fund313_SelbstGefunden_KoerperMitTiefeStattHoehe_Gegenprobe()
    {
        // flaches, tiefes Wartehaus vor dem Bahnsteig (Gleisseite): nur die Drehung um eine halbe Runde stellt es hinter den Bahnsteig;
        // mit der alten Regel (Tiefe aus der Hoehe 0,1) galt das Wartehaus auf dem Gleis als frei
        static (int Dreh, bool Gerastet) Lauf()
        {
            var p = Nico("Koerper");
            var a = (PyObjekt)Py.Kopie(p.AussenStand)!;
            var s = Gebaeude(p, "haltepunkt");
            Py.Setzen(Objekt(Get(Aussen.Karte(a), "modelle")), "haltepunkt", new List<object?>
            {
                new PyObjekt { { "art", "flaeche" }, { "x", 0L }, { "y", 0L }, { "w", 2.4 }, { "h", 0.5 }, { "farbe", "bahnsteig" } },
                new PyObjekt { { "art", "block" }, { "x", 0.5 }, { "y", -0.7 }, { "w", 1.3 }, { "t", 0.7 }, { "h", 0.1 } },
            });
            var g = Objekt(Aussen.Landschaft(a)[s.Nummer]);
            Py.Setzen(g, "x", 1.0);
            Py.Setzen(g, "y", 41.3);
            var gerastet = AussenBearbeitung.BahnhofAnGleis(a, s);
            return (AussenBearbeitung.HausDreh(a, s), gerastet);
        }
        var neu = Lauf();
        Assert.True(neu.Gerastet);
        Assert.Equal(2, neu.Dreh);
        AussenBearbeitung.KoerperHoeheAltFuerTests = true;
        try
        {
            Assert.Equal(0, Lauf().Dreh);
        }
        finally
        {
            AussenBearbeitung.KoerperHoeheAltFuerTests = false;
        }
    }
}
