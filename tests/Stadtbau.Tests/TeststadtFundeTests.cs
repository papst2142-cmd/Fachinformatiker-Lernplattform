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
/// Funde 292 bis 296 aus Nicos Teststadt S 0.40.0 (Meldung P12-116, Weisung P12-196 Nico 1b 2a 3b 4a). Die Teststadt liegt als
/// Stadt-Datei in <c>Testdaten/nico/</c> und wird wie bei Nico über „Stadt-Datei als neues Projekt öffnen“ geladen. Jeder Fund
/// hat eine Gegenprobe mit dem Verhalten vor dem Fix.
/// </summary>
public class TeststadtFundeTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly Lazy<byte[]> Datei = new(() =>
        File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "teststadt_S0.40.0.fistadt")));

    private static Projekt Nico(string name = "Nico Teststadt") => Projekt.AusStadtDatei(Datei.Value, D, Daten.NeueAblage(), name);

    private static List<(int Nr, string Modell)> Gebaeude(PyObjekt a) =>
        [.. Aussen.Landschaft(a).Select((t, i) => (t, i)).Where(x => x.t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude")).Select(x => (x.i, Py.Str(((PyObjekt)x.t!)["modell"])))];

    private static HashSet<string> RoteFunde(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text)];

    // ---- 292: die benutzte Modelltuer bleibt sichtbar --------------------------------------------------------------

    /// <summary>Tueren auf einer Modelltuer, deren dunkle Tuer im fertigen Bild noch da ist (nicht in Wandfarbe zugemalt).</summary>
    private static (int Gesamt, int Sichtbar) ModelltuerenSichtbar(StadtSzene sz)
    {
        var (gesamt, sichtbar) = (0, 0);
        foreach (var t in sz.Tueren.Where(t => t.Modell))
        {
            var haus = sz.Haeuser.Where(h => t.Mitte.X >= h.Min.X - 0.1f && t.Mitte.X <= h.Max.X + 0.1f && t.Mitte.Y >= h.Min.Z - 0.1f && t.Mitte.Y <= h.Max.Z + 0.1f)
                .MinBy(h => (h.Max.X - h.Min.X) * (h.Max.Z - h.Min.Z));
            // nur Tueren, unter denen das Modell selbst (ungefaerbt) eine dunkle Tuer hat (das Rolltor der Werkstatt ist hell)
            if (haus is null || StadtTueren.ModellTueren(sz.Stuecke, haus.StueckeVon, haus.StueckeBis, i => sz.Grafik?.Modell(sz.Stuecke[i].Name), t.Mitte, t.Aussen).Count == 0)
            {
                continue;
            }
            gesamt++;
            if (StadtTueren.ModellTueren(sz.Stuecke, haus.StueckeVon, haus.StueckeBis, sz.ModellAn, t.Mitte, t.Aussen).Count > 0)
            {
                sichtbar++;
            }
        }
        return (gesamt, sichtbar);
    }

    [Fact]
    public void Fund292_BenutzteModelltuer_BleibtSichtbar_Gegenprobe()
    {
        var p = Nico();
        var (gesamt, sichtbar) = ModelltuerenSichtbar(p.SzeneOhneLaternen(p.AussenStand, true)!);
        Assert.True(gesamt >= 3, "Tueren auf einer Modelltuer: " + gesamt);
        Assert.Equal(gesamt, sichtbar);
        // Stadt 1 (Fassung 0): ebenso
        var s1 = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        var (g1, s1Sicht) = ModelltuerenSichtbar(s1.SzeneOhneLaternen(s1.AussenStand, true)!);
        Assert.Equal(g1, s1Sicht);
        // Gegenprobe: die alte Suche bis zur Vorderwand malt Nicos Tueren zu
        StadtSzene.RueckseiteAltFuerTests = true;
        try
        {
            var q = Nico("Alt");
            var (ga, sa) = ModelltuerenSichtbar(q.SzeneOhneLaternen(q.AussenStand, true)!);
            Assert.True(sa < ga, $"alt {sa} von {ga}");
        }
        finally
        {
            StadtSzene.RueckseiteAltFuerTests = false;
        }
    }

    /// <summary>Dreiecke in der Tuer-Farbe „schwarz“ an <paramref name="modell"/> und wie viele davon an einer gezeichneten Modelltuer liegen (Mitte bis zum halben Rahmen).</summary>
    private static (int Gefaerbt, int InDerTuer) Schwarz(Projekt p, string modell)
    {
        var sz = p.SzeneOhneLaternen(p.AussenStand, true)!;
        var haus = sz.Haeuser.First(h => h.Id.StartsWith(modell + "#", StringComparison.Ordinal));
        var tueren = sz.Tueren.Where(t => t.Modell).Select(t => t.Mitte).ToList();
        var (gefaerbt, drin) = (0, 0);
        for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
        {
            var gm = sz.ModellAn(i)!;
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                if (gm.Farben[d * 3] != 0x1D || gm.Farben[(d * 3) + 1] != 0x1D || gm.Farben[(d * 3) + 2] != 0x21)
                {
                    continue;
                }
                gefaerbt++;
                var e = gm.Ecken;
                var m = Vector3.Transform((new Vector3(e[d * 9], e[(d * 9) + 1], e[(d * 9) + 2]) + new Vector3(e[(d * 9) + 3], e[(d * 9) + 4], e[(d * 9) + 5])
                    + new Vector3(e[(d * 9) + 6], e[(d * 9) + 7], e[(d * 9) + 8])) / 3, sz.Stuecke[i].Lage);
                drin += tueren.Any(t => Vector2.Distance(t, new Vector2(m.X, m.Z)) <= StadtTueren.HalbRahmen + 0.1f) ? 1 : 0;
            }
        }
        return (gefaerbt, drin);
    }

    /// <summary>Weisung P12-196 Frage 4 = a: ohne Wahl die Tuer wie im Modell, eine gewaehlte Tuer-Farbe faerbt nur das Tuerblatt der Modelltuer.</summary>
    [Fact]
    public void Fund292_TuerFarbe_FaerbtNurDasTuerblattDerModelltuer()
    {
        var p = Nico();
        var (nr, modell) = Gebaeude(p.AussenStand).First(g => g.Modell == "bitweiche");
        Assert.Equal((0, 0), Schwarz(p, modell));
        p.AussenAendern("Farbe", a => AussenBearbeitung.FarbeSetzen(a, modell, Gebaeudefarben.TuerSchluessel, "schwarz"));
        var (gefaerbt, drin) = Schwarz(p, modell);
        Assert.True(gefaerbt > 0);
        Assert.Equal(gefaerbt, drin);
        Assert.True(nr > 0);
    }

    // ---- 293: „Tür dazu“ ohne roten Tuerfund -------------------------------------------------------------------------

    /// <summary>Je Gebaeude ohne Ort: Tuer weg, „Tür dazu“ wie der Knopf (Fassaden-Rechnung), neue rote Tuerfunde zaehlen.</summary>
    private static int NeueRoteNachTuerDazu(Projekt vorlage)
    {
        var neu = 0;
        foreach (var (nr, _) in Gebaeude(vorlage.AussenStand))
        {
            var s = new AussenStelle(AussenArt.Gebaeude, nr);
            var p = Projekt.AusStadtDatei(vorlage.Stand().Schreiben(), D, Daten.NeueAblage(), "T" + nr);
            p.AussenAendern("weg", a => GebaeudeTuer.Entfernen(a, s));
            if (GebaeudeTuer.DazuNicht(p.AussenStand, s) is not null)
            {
                continue;
            }
            var vorher = RoteFunde(p);
            var platz = GebaeudeTuer.DazuPlatz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s, einrasten: true);
            p.AussenAendern(Texte.TuerDazu, a => GebaeudeTuer.DazuAn(a, s, true, platz));
            neu += RoteFunde(p).Count(f => !vorher.Contains(f) && f.Contains("Tür", StringComparison.Ordinal));
        }
        return neu;
    }

    [Fact]
    public void Fund293_TuerDazu_KeinRoterTuerfund_Gegenprobe()
    {
        Assert.Equal(0, NeueRoteNachTuerDazu(Nico()));
        GebaeudeTuer.MitteAltFuerTests = true;
        try
        {
            Assert.True(NeueRoteNachTuerDazu(Nico()) > 0);
        }
        finally
        {
            GebaeudeTuer.MitteAltFuerTests = false;
        }
    }

    // ---- 294: Kopie behaelt die Grafik ihres Grundmodells -------------------------------------------------------------

    private static List<string> Stuecke(StadtSzene sz, string modell)
    {
        var h = sz.Haeuser.First(h => h.Id.StartsWith(modell + "#", StringComparison.Ordinal));
        return [.. Enumerable.Range(h.StueckeVon, h.StueckeBis - h.StueckeVon).Select(i => sz.Stuecke[i].Name + " " + Vector3.Transform(Vector3.Zero, sz.Stuecke[i].Lage).ToString("F3", null))];
    }

    [Fact]
    public void Fund294_KopieBahnhof_GleicheGrafikWieDasOriginal_Gegenprobe()
    {
        static (List<string> Vorher, List<string> Nachher, string Kopie) Probe()
        {
            var p = Nico();
            var (nr, _) = Gebaeude(p.AussenStand).First(g => g.Modell == "bahnhof");
            var vorher = Stuecke(p.SzeneOhneLaternen(p.AussenStand, true)!, "bahnhof");
            var s = new AussenStelle(AussenArt.Gebaeude, nr);
            p.AussenAendern(Texte.TuerDazu, a => GebaeudeTuer.Dazu(a, s, true));
            var kopie = Py.Str(Objekt(Aussen.Landschaft(p.AussenStand)[nr])["modell"]);
            return (vorher, Stuecke(p.SzeneOhneLaternen(p.AussenStand, true)!, kopie), kopie);
        }

        var (vorher, nachher, kopie) = Probe();
        Assert.NotEqual("bahnhof", kopie);
        Assert.True(vorher.Count > 0);
        Assert.Equal(vorher, nachher);
        StadtSzene.KopieAltFuerTests = true;
        try
        {
            var (v2, n2, _) = Probe();
            Assert.NotEqual(v2, n2);
        }
        finally
        {
            StadtSzene.KopieAltFuerTests = false;
        }
    }

    [Fact]
    public void Fund294_ZweiteWohnungMitTuer_GleicheGrafik()
    {
        Assert.True(ZweiteWohnung());
        StadtSzene.KopieAltFuerTests = true;
        try
        {
            Assert.False(ZweiteWohnung());
        }
        finally
        {
            StadtSzene.KopieAltFuerTests = false;
        }
    }

    private static bool ZweiteWohnung()
    {
        // wie in Meldung P12-116: in Nicos Teststadt eine zweite Wohnung setzen und „Tür dazu“ (Nicos Wohnung hat schon eine Tuer)
        var p = Nico();
        var s = p.AussenAendern("Haus", a => AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 30, 20, modell: "wohnung"))!;
        p.AussenAendern(Texte.TuerDazu, a => GebaeudeTuer.Dazu(a, s, false));
        var kopie = Py.Str(Objekt(Aussen.Landschaft(p.AussenStand)[s.Nummer])["modell"]);
        Assert.NotEqual("wohnung", kopie);
        var sz = p.SzeneOhneLaternen(p.AussenStand, true)!;
        var original = sz.Haeuser.First(h => h.Id.StartsWith("wohnung#", StringComparison.Ordinal));
        var neu = sz.Haeuser.First(h => h.Id.StartsWith(kopie + "#", StringComparison.Ordinal));
        // Baeume waehlt das Spiel je Stelle (Teil „baum“), darum zaehlen nur die Modelle des Hauses
        List<string> Haus(StadtHaus h) =>
            [.. Enumerable.Range(h.StueckeVon, h.StueckeBis - h.StueckeVon).Select(i => sz.Stuecke[i].Name).Where(n => !n.Contains("tree", StringComparison.Ordinal) && !n.Contains("pine", StringComparison.Ordinal))];
        Assert.NotEmpty(Haus(original));
        return Haus(original).SequenceEqual(Haus(neu));
    }

    // ---- 295: Bahnhof rastet ans Gleis, Regel fuer Bahnsteige im Modell -------------------------------------------------

    [Fact]
    public void Fund295_NicosBahnhofAufDemGleis_Rot_Gegenprobe()
    {
        var p = Nico();
        Assert.Contains(RoteFunde(p), f => f.Contains("liegt auf dem Gleis", StringComparison.Ordinal));
        StadtbauPruefung.BahnsteigNurAltFuerTests = true;
        try
        {
            Assert.DoesNotContain(RoteFunde(Nico("Alt")), f => f.Contains("liegt auf dem Gleis", StringComparison.Ordinal));
        }
        finally
        {
            StadtbauPruefung.BahnsteigNurAltFuerTests = false;
        }
        // Stadt 1 (Fassung 0): alle Bahnsteige frei
        var s1 = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        Assert.DoesNotContain(RoteFunde(s1), f => f.Contains("liegt auf dem Gleis", StringComparison.Ordinal));
    }

    [Fact]
    public void Fund295_NicosBahnhof_GeschobenRastetAnsGleis_Gegenprobe()
    {
        var p = Nico();
        var (nr, _) = Gebaeude(p.AussenStand).First(g => g.Modell == "bahnhof");
        var s = new AussenStelle(AussenArt.Gebaeude, nr);
        p.AussenAendern("schieben", a => AussenBearbeitung.Verschieben(a, s, 0.5, 0.5));
        Assert.DoesNotContain(RoteFunde(p), f => f.Contains("liegt auf dem Gleis", StringComparison.Ordinal));
        // eingerastet: noch einmal einrasten aendert nichts
        Assert.False(AussenBearbeitung.BahnhofAnGleis((PyObjekt)Py.Kopie(p.AussenStand)!, s));
        AussenBearbeitung.BahnhofAltFuerTests = true;
        try
        {
            var q = Nico("Alt");
            var g = Objekt(Aussen.Landschaft(q.AussenStand)[nr]);
            var (x, y) = (Py.F(g["x"]), Py.F(g["y"]));
            q.AussenAendern("schieben", a => AussenBearbeitung.Verschieben(a, s, 0.5, 0.5));
            Assert.True(AussenBearbeitung.BahnhofAltFuerTests);
            AussenBearbeitung.BahnhofAltFuerTests = false;
            // ohne Einrasten ans Gleis haette jetzt das Einrasten etwas zu tun
            Assert.True(AussenBearbeitung.BahnhofAnGleis((PyObjekt)Py.Kopie(q.AussenStand)!, s), $"alt bei {x}/{y}");
        }
        finally
        {
            AussenBearbeitung.BahnhofAltFuerTests = false;
        }
    }

    /// <summary>Je Bahnhof der Stadt 1 (Orte mit Bahnsteig im Modell) und Drehung: geschoben rastet er ein, Bahnsteig frei vom Gleis.</summary>
    [Fact]
    public void Fund295_JederBahnhofDerStadt1_JeDrehung_RastetEin()
    {
        var vorlage = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        var bahnhoefe = AussenBearbeitung.Orte(vorlage.AussenStand)
            .Where(o => Bahnsteig.Hat(Get(AussenBearbeitung.Modelle(vorlage.AussenStand), Py.Str(o["modell"])))).Select(o => Py.Str(o["id"])).ToList();
        Assert.True(bahnhoefe.Count >= 2, string.Join(",", bahnhoefe));
        foreach (var id in bahnhoefe)
        {
            foreach (var dreh in new[] { 0, 1 })
            {
                var p = Projekt.Neu("B", D, Daten.NeueAblage());
                var s = new AussenStelle(AussenArt.Haus, -1, 0, id);
                p.AussenAendern("drehen", a =>
                {
                    for (var i = 0; i < dreh; i++)
                    {
                        AussenBearbeitung.Drehen(a, s);
                    }
                    AussenBearbeitung.Verschieben(a, s, 0, 0.5);
                });
                Assert.False(AussenBearbeitung.BahnhofAnGleis((PyObjekt)Py.Kopie(p.AussenStand)!, s), $"{id} Drehung {dreh}");
                Assert.DoesNotContain(RoteFunde(p), f => f.Contains("liegt auf dem Gleis", StringComparison.Ordinal));
            }
        }
    }

    // ---- 296: Laternen neu verteilen nach Stadt-Datei -------------------------------------------------------------------

    private static List<string> Laternen(Projekt p) =>
        [.. Aussen.LaternenWerte(p.AussenStand).Select(l => $"{l.X:F2}/{l.Z:F2}/{l.Doppelt}").Order(StringComparer.Ordinal)];

    [Fact]
    public void Fund296_LaternenNeuVerteilen_NachStadtDatei_WieFrisch_Gegenprobe()
    {
        var p = Nico();
        var rotVorher = RoteFunde(p);
        p.LaternenVerteilen();
        var einmal = Laternen(p);
        Assert.DoesNotContain(RoteFunde(p), f => !rotVorher.Contains(f));
        // zweiter Druck aendert nichts
        var (dazu, weg) = p.LaternenVerteilen();
        Assert.Equal((0, 0), (dazu, weg));
        Assert.Equal(einmal, Laternen(p));
        // ueber die Stadt-Datei wieder geoeffnet: der Knopf gibt dieselben Laternen (wie frisch verteilt)
        var wieder = Projekt.AusStadtDatei(p.Stand().Schreiben(), D, Daten.NeueAblage(), "Wieder");
        wieder.LaternenVerteilen();
        Assert.Equal(einmal, Laternen(wieder));
        // Gegenprobe: alter Knopf (nur automatische) stellt nach der Stadt-Datei dazwischen neue auf
        LaternenAutomatik.NurAutomatischeAltFuerTests = true;
        try
        {
            var q = Nico("Alt");
            var zahl = Aussen.LaternenWerte(q.AussenStand).Count;
            q.LaternenVerteilen();
            Assert.True(Aussen.LaternenWerte(q.AussenStand).Count > zahl + 20);
        }
        finally
        {
            LaternenAutomatik.NurAutomatischeAltFuerTests = false;
        }
    }

    [Fact]
    public void Fund296_VorherSagen_NenntZahlDerVerschwindenden()
    {
        var p = Nico();
        var (verschwinden, neu) = p.LaternenVorherSagen();
        var zahl = Aussen.LaternenWerte(p.AussenStand).Count;
        p.LaternenVerteilen();
        Assert.Equal(zahl - verschwinden + neu, Aussen.LaternenWerte(p.AussenStand).Count);
        Assert.True(verschwinden > 0);
    }
}
