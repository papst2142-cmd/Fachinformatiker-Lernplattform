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
/// Welle A 0.70.0 / S 0.46.0 (Weisung P12-218 Punkt 2, Nico 2 = a mit Aenderung, 4 = a): Haltepunkt mit freiem Bahnsteig
/// (0,8 tief, nichts darauf), Wartehaeuschen dahinter und blauer Zugzielanzeiger NEBEN dem Haeuschen. Das Modell bringt nur
/// das Werkzeug mit; beim Setzen kommt es in die Stadt (Stadt 1 bleibt bitgleich). Je Fall eine Gegenprobe.
/// </summary>
public class HaltepunktFreiTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly Lazy<byte[]> Datei = new(() =>
        File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "teststadt_S0.43.0.fistadt")));

    private static Projekt Nico() => Projekt.AusStadtDatei(Datei.Value, D, Daten.NeueAblage(), "Nico Teststadt");

    private static List<string> RoteFunde(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text)];

    /// <summary>Haltepunkt bei (x, y) setzen, „Tür dazu“ (ungedreht, wie in der Oberflaeche), dann ans Gleis legen.</summary>
    private static (AussenStelle S, string? Hinweis) SetzenMitTuer(Projekt p, double x = 19, double y = 30.5, bool anGleis = true)
    {
        AussenStelle? s = null;
        p.AussenAendern(Texte.TuerDazu, a => { s = AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, x, y, raster: false, modell: Zusatzmodelle.HaltepunktFrei); });
        Assert.NotNull(s);
        var platz = GebaeudeTuer.DazuPlatz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s!, einrasten: true);
        string? h = null;
        p.AussenAendern(Texte.TuerDazu, a => h = GebaeudeTuer.DazuAn(a, s!, true, platz));
        var gelegt = false;
        p.AussenAendern(Texte.TuerDazu, a => { gelegt = AussenBearbeitung.BahnhofAnGleis(a, s!); });
        Assert.Equal(anGleis, gelegt);
        return (s!, h);
    }

    private static StadtHaus Haus(StadtSzene sz) => sz.Haeuser.Single(h => h.Id.Contains(Zusatzmodelle.HaltepunktFrei, StringComparison.Ordinal));

    [Fact]
    public void Modell_NurImWerkzeug_KommtBeimSetzenInDieStadt_GegenprobeOhneZusatz()
    {
        var a = Aussen.Aus(D.Fassung);
        Assert.Null(Get(AussenBearbeitung.ModelleDerStadt(a), Zusatzmodelle.HaltepunktFrei));
        Assert.NotNull(Get(AussenBearbeitung.Modelle(a), Zusatzmodelle.HaltepunktFrei));
        Assert.True(ModellNamen.IstBahnhof(Zusatzmodelle.HaltepunktFrei));
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 10, 10, raster: false, modell: Zusatzmodelle.HaltepunktFrei));
        Assert.NotNull(Get((PyObjekt)Get(Aussen.Karte(a), "modelle")!, Zusatzmodelle.HaltepunktFrei));
        // Stadt 1 ohne Setzen bleibt wie sie ist (Nico 4 = a): das Modell steht nicht in der Fassung
        Assert.Null(Get((PyObjekt)Get(Aussen.Karte(Aussen.Aus(D.Fassung)), "modelle")!, Zusatzmodelle.HaltepunktFrei));
        AussenBearbeitung.ZusatzAltFuerTests.Value = true;
        try
        {
            Assert.Null(Get(AussenBearbeitung.Modelle(Aussen.Aus(D.Fassung)), Zusatzmodelle.HaltepunktFrei));
        }
        finally
        {
            AussenBearbeitung.ZusatzAltFuerTests.Value = false;
        }
    }

    [Fact]
    public void Modell_BahnsteigFrei_AnzeigeNebenDemHaeuschen()
    {
        var teile = ((List<object?>)Zusatzmodelle.Alle()[Zusatzmodelle.HaltepunktFrei]!).OfType<PyObjekt>().ToList();
        var steig = teile.Single(Bahnsteig.IstFlaeche);
        Assert.Equal(0.8, Py.F(steig["h"]), 6);
        double X(PyObjekt t) => Py.F(t["x"]);
        double Y(PyObjekt t) => Py.F(t["y"]);
        var (s1, s2) = ((X(steig), Y(steig)), (X(steig) + Py.F(steig["w"]), Y(steig) + Py.F(steig["h"])));
        var haeuschen = teile.Single(t => Py.Gleich(t["art"], "block"));
        var anzeige = teile.Single(t => Py.Gleich(t["art"], "mast"));
        Assert.True(Py.Hat(anzeige, "w"));
        // nichts steht auf dem Bahnsteig: Haeuschen dahinter, Anzeige (Breite w, Tiefe 0,12) neben dem Haeuschen
        Assert.True(Y(haeuschen) + Py.F(haeuschen["t"]) <= s1.Item2 + 1e-9);
        Assert.True(Y(anzeige) + 0.06 <= s1.Item2 + 1e-9);
        Assert.True(X(anzeige) - (Py.F(anzeige["w"]) / 2) >= X(haeuschen) + Py.F(haeuschen["w"]));
        Assert.True(X(anzeige) + (Py.F(anzeige["w"]) / 2) <= s2.Item1);
    }

    [Fact]
    public void Teststadt_AmSchraegenGleis_TuerAmHaeuschen_BahnsteigBleibtDran_OhneFund_GegenprobeTeilen()
    {
        var p = Nico();
        static int Abgetrennt(Projekt p) =>
            Aussen.Landschaft(p.AussenStand).OfType<PyObjekt>().Count(t => Py.Str(t["modell"]).StartsWith("bahnsteig_", StringComparison.Ordinal));
        var vorher = Abgetrennt(p);
        var (s, hinweis) = SetzenMitTuer(p);
        Assert.Null(hinweis);
        Assert.Empty(RoteFunde(p));
        var g = Objekt(Aussen.Landschaft(p.AussenStand)[s.Nummer]);
        // Haus und Bahnsteig bleiben ein Modell (kein abgetrennter Bahnsteig), die Tuer sitzt auf der Wand des Haeuschens
        Assert.Equal(Zusatzmodelle.HaltepunktFrei, Py.Str(g["modell"]));
        Assert.Equal(vorher, Abgetrennt(p));
        Assert.NotEqual(0, AussenBearbeitung.HausGrad(p.AussenStand, s));
        var sz = p.SzeneOhneLaternen(p.AussenStand, true)!;
        var haus = Haus(sz);
        // die Tuer liegt auf der Wand des Haeuschens (Kasten des Hauses = Bloecke), nicht auf der Kante des Bahnsteigs
        var tuer = sz.Tueren.OrderBy(t => haus.Abstand(t.Mitte)).First();
        Assert.True(haus.Abstand(tuer.Mitte) < 0.02f, tuer.Mitte.ToString());
        Assert.True(haus.LokalMax.X - haus.LokalMin.X < 1.4f, "Kasten des Hauses umfasst den Bahnsteig");
        // Gegenprobe: wie bis S 0.45.0 trennt „Tür dazu“ den Bahnsteig ab
        GebaeudeTuer.BahnsteigTeilenAltFuerTests = true;
        try
        {
            var q = Nico();
            // abgetrennt hat das Haus keinen Bahnsteig mehr und legt sich nicht mehr ans Gleis
            SetzenMitTuer(q, anGleis: false);
            Assert.Equal(vorher + 1, Abgetrennt(q));
        }
        finally
        {
            GebaeudeTuer.BahnsteigTeilenAltFuerTests = false;
        }
    }

    /// <summary>
    /// Ungedreht umfasst der Kasten des Hauses den Bahnsteig; die Tuer des Zierwegs liegt dann innen im Kasten und sitzt im Spiel
    /// auf der Wand des Haeuschens zum Bahnsteig (nicht auf der Rueckwand). Gegenprobe: wie bis A 0.69.0 auf dem Kasten.
    /// </summary>
    [Fact]
    public void Ungedreht_SpielZeichnetTuerAmHaeuschenZumBahnsteig_GegenprobeKasten()
    {
        foreach (var dreh in new[] { 0, 1, 2, 3 })
        {
            var p = Projekt.Neu("Haltepunkt", D, Daten.NeueAblage(), null, (40, 40));
            var s = p.AussenAendern("Haus", a =>
            {
                var st = AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 20, 20, raster: true, modell: Zusatzmodelle.HaltepunktFrei)!;
                for (var i = 0; i < dreh; i++)
                {
                    AussenBearbeitung.Drehen(a, st);
                }
                return st;
            });
            p.AussenAendern("Tuer", a => GebaeudeTuer.Dazu(a, s, einrasten: false));
            var t = (List<object?>)Blueprint.ZierwegVon(p.AussenStand, s)!["tuer"]!;
            var daten = new Vector2((float)Py.F(t[0]), (float)Py.F(t[1]));
            StadtTuer Tuer() => p.SzeneOhneLaternen(p.AussenStand, true)!.Tueren.Single();
            Assert.True(Vector2.Distance(Tuer().Mitte, daten) < 0.02f, $"dreh {dreh}: {Tuer().Mitte} {daten}");
            // Tuer schaut zum Bahnsteig: die Bahnsteigflaeche liegt vor ihr
            var steig = Haus(p.SzeneOhneLaternen(p.AussenStand, true)!);
            Assert.True(steig.Drin(Tuer().Mitte + (Tuer().Aussen * 0.4f)), $"dreh {dreh}");
            StadtSzene.ZierKastenAltFuerTests = true;
            try
            {
                Assert.True(Vector2.Distance(Tuer().Mitte, daten) > 0.5f, $"Gegenprobe dreh {dreh}");
            }
            finally
            {
                StadtSzene.ZierKastenAltFuerTests = false;
            }
        }
    }

    [Fact]
    public void Zugzielanzeiger_BlauUndRechteckig_SchriftLeuchtetNachts_GegenprobeMastOhneBreite()
    {
        var teile = StadtSzene.Zugzielanzeiger(0, 0, 0.5f, 0.75f);
        Assert.Contains(teile, t => t.Farbe == StadtSzene.AnzeigeBlau);
        var p = Nico();
        SetzenMitTuer(p);
        var sz = p.SzeneOhneLaternen(p.AussenStand, true)!;
        var netz = sz.HausNetz(Haus(sz));
        int Blau() => Enumerable.Range(0, netz.AnzahlEcken).Count(e => Vector3.Distance(netz.FarbeAn(e), StadtSzene.AnzeigeBlau) < 0.02f);
        int Licht() => Enumerable.Range(0, netz.AnzahlEcken).Count(e => netz.FarbeAn(e).X >= Nachtlicht.Marke - 0.5f);
        Assert.True(Blau() > 0);
        Assert.True(Licht() > 0);
        // querliegend: breiter als hoch
        var b = teile.Where(t => t.Farbe == StadtSzene.AnzeigeBlau).ToList();
        Assert.All(b, t => Assert.True(t.X2 - t.X1 > t.Y2 - t.Y1));
        // Gegenprobe: derselbe Mast ohne Breite (wie jeder Mast der Stadt 1) zeichnet keine Anzeige
        var ohne = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var m = (List<object?>)Get((PyObjekt)Get(Aussen.Karte(ohne), "modelle")!, Zusatzmodelle.HaltepunktFrei)!;
        m.OfType<PyObjekt>().Single(t => Py.Gleich(t["art"], "mast")).RemoveAll(e => e.Key == "w");
        var sz2 = p.SzeneOhneLaternen(ohne, true)!;
        netz = sz2.HausNetz(Haus(sz2));
        Assert.Equal(0, Blau());
    }
}
