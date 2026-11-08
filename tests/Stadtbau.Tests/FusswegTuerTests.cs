using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Patch S 0.23.1 / A 0.52.1 (Weisung P12-98, Sammelliste 244, Fund Nico): Fussweg zur Eingangstuer ziehen. Vorher fing
/// „Fußweg ziehen“ nur Fusswegenden und die Gehwegkante; ein Tipp an der Tuer landete auf dem Raster 0,5 im Haus und die
/// Vorschau war rot („führt durch das Haus“). Jeder Fall mit Gegenprobe (alter Fangweg, alte Regel 3).
/// </summary>
public class FusswegTuerTests
{
    private static Spieldaten D => Daten.Spiel;

    private static PyObjekt Stadt1() => Aussen.Aus(D.Fassung);

    private static List<StadtbauFund> Funde(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D.Inhalt, D.Grafik);

    private static StadtSzene Szene(PyObjekt a)
    {
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        return StadtSzene.Bauen(f.StadtFuer(f.MitKarte(D.Inhalt)), null, D.Grafik);
    }

    private static List<StadtbauFund> FusswegRot(PyObjekt a) => [.. Funde(a).Where(f => f.Rot && f.Text.Contains("Fußweg", StringComparison.Ordinal))];

    private static Fussweg Weg(PyObjekt a, int nr = 0) => Fusswege.Alle(Objekt(a["stadtdaten"]))[nr];

    private static void FusswegeSetzen(PyObjekt a, params float[][] punkte)
    {
        var liste = new List<object?>();
        foreach (var p in punkte)
        {
            var l = new List<object?>();
            for (var i = 0; i + 1 < p.Length; i += 2)
            {
                l.Add(new List<object?> { (double)p[i], (double)p[i + 1] });
            }
            var w = new PyObjekt();
            w.Add("punkte", l);
            liste.Add(w);
        }
        Py.Setzen(Objekt(a["stadtdaten"]), Fusswege.Abschnitt, liste);
    }

    // Bitweiche (Stadt 1): Tuer der Daten (14,9; 19,8) auf der Aussenkante, echte Wand bei y 19,66, Tuer zeigt nach +y
    private static readonly Vector2 BitweicheTuer = new(14.9f, 19.8f);

    [Fact]
    public void Fangen_AnDerTuer_LageUndRichtung_GegenprobeAlterFangweg()
    {
        var a = Stadt1();
        var p = AussenBearbeitung.Fangen(a, ZiehArt.Fussweg, 15.0, 19.2, true);
        Assert.Equal(BitweicheTuer, p.Lage);
        Assert.NotNull(p.Weg);
        Assert.True(Vector2.Distance(p.Weg!.Value, new Vector2(0, 1)) < 1e-3f, $"Richtung {p.Weg}");
        // die Tuer ist im Spiel der Anfang des Tuerwegs
        Assert.Contains(Szene(a).Tueren, t => t.Ort == "bitweiche" && Vector2.Distance(t.WegAnfang, p.Lage) < 1e-3f);
        AussenBearbeitung.TuerFangenAltFuerTests = true;
        try
        {
            var alt = AussenBearbeitung.Fangen(a, ZiehArt.Fussweg, 15.0, 19.2, true);
            Assert.Equal(new Vector2(15f, 19f), alt.Lage);   // frei auf dem Raster, im Haus
        }
        finally
        {
            AussenBearbeitung.TuerFangenAltFuerTests = false;
        }
    }

    [Fact]
    public void Fangen_EinrastenAus_BleibtFrei()
    {
        var p = AussenBearbeitung.Fangen(Stadt1(), ZiehArt.Fussweg, 15.13, 19.52, false);
        Assert.Equal(new Vector2(15.1f, 19.5f), p.Lage);
        Assert.Null(p.Weg);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ziehen_TuerZumGehweg_UndGehwegZurTuer_Gruen_GegenprobeRot(bool vonDerTuer)
    {
        var (ax, ay, bx, by) = vonDerTuer ? (15.0, 19.2, 14.9, 20.7) : (14.9, 20.7, 15.0, 19.2);
        var a = Stadt1();
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Fussweg, ax, ay, bx, by, true, out var grund);
        Assert.Null(grund);
        Assert.NotNull(s);
        var punkte = Weg(a).Punkte;
        Assert.Equal(BitweicheTuer, vonDerTuer ? punkte[0] : punkte[^1]);
        Assert.Empty(FusswegRot(a));
        // Vorschau = Schritt: der Plan endet ebenso an der Tuer
        var (pa, pb, plan) = AussenBearbeitung.ZiehPlan(Stadt1(), ZiehArt.Fussweg, ax, ay, bx, by, true);
        Assert.True(plan.Gefunden);
        Assert.Equal(BitweicheTuer, vonDerTuer ? pa.Lage : pb.Lage);

        // Gegenprobe: der alte Fangweg setzt den Weg ins Haus, die Pruefung ist rot
        var alt = Stadt1();
        AussenBearbeitung.TuerFangenAltFuerTests = true;
        try
        {
            AussenBearbeitung.Ziehen(alt, ZiehArt.Fussweg, ax, ay, bx, by, true, out _);
        }
        finally
        {
            AussenBearbeitung.TuerFangenAltFuerTests = false;
        }
        Assert.Contains(FusswegRot(alt), f => f.Text.Contains("durch das Haus", StringComparison.Ordinal));
    }

    [Fact]
    public void Ziehen_AnDieTuerEinesGebaeudesOhneOrt()
    {
        var a = Stadt1();
        var stellwerk = AussenBearbeitung.Alle(a).First(x => x.Stelle.Art == AussenArt.Gebaeude).Stelle;
        var z = Blueprint.ZierwegVon(a, stellwerk)!;
        var t = (List<object?>)z["tuer"]!;
        var tuer = new Vector2((float)Py.F(t[0]), (float)Py.F(t[1]));
        var p = AussenBearbeitung.Fangen(a, ZiehArt.Fussweg, tuer.X + 0.3, tuer.Y - 0.3, true);
        Assert.Equal(tuer, p.Lage);
        Assert.Contains(Szene(a).Tueren, d => d.Ort == "stellwerk" && Vector2.Distance(d.WegAnfang, tuer) < 1e-3f);
    }

    [Fact]
    public void Fangen_GedrehtesHaus_TuerWieImSpiel()
    {
        var a = Stadt1();
        Assert.True(AussenBearbeitung.Drehen(a, new AussenStelle(AussenArt.Haus, -1, 0, "zuhause")));
        var t = Szene(a).Tueren.Single(x => x.Ort == "zuhause");
        var p = AussenBearbeitung.Fangen(a, ZiehArt.Fussweg, t.WegAnfang.X + 0.2, t.WegAnfang.Y + 0.2, true);
        Assert.True(Vector2.Distance(p.Lage, t.WegAnfang) < 0.02f, $"Fang {p.Lage}, Tuer im Spiel {t.WegAnfang}");
        Assert.True(Vector2.Distance(p.Weg!.Value, t.Aussen) < 0.02f, $"Richtung {p.Weg}, Tuer zeigt {t.Aussen}");
    }

    [Fact]
    public void Regel3_BisAnDieWandDerTuer_Gruen_NebenDerTuerUndQuerDurchsHaus_Rot()
    {
        var mitte = Szene(Stadt1()).Tueren.Single(t => t.Ort == "bitweiche").Mitte;
        Assert.True(mitte.Y < BitweicheTuer.Y - 0.05f, "die echte Wand liegt im Block");
        // bis an die echte Wand an der Tuer: gruen
        var a = Stadt1();
        FusswegeSetzen(a, [mitte.X, mitte.Y, mitte.X, 20.8f]);
        Assert.Empty(FusswegRot(a));
        // Gegenprobe: alte Regel 3 ist rot
        StadtbauPruefung.FusswegTuerAltFuerTests = true;
        try
        {
            Assert.Contains(FusswegRot(a), f => f.Text.Contains("durch das Haus", StringComparison.Ordinal));
        }
        finally
        {
            StadtbauPruefung.FusswegTuerAltFuerTests = false;
        }
        // durch die Wand neben der Tuer: rot
        var neben = Stadt1();
        FusswegeSetzen(neben, [mitte.X - 0.6f, mitte.Y, mitte.X - 0.6f, 20.8f]);
        Assert.Contains(FusswegRot(neben), f => f.Text.Contains("durch das Haus", StringComparison.Ordinal));
        // durch die Tuer quer durchs Haus: rot
        var quer = Stadt1();
        FusswegeSetzen(quer, [mitte.X, 20.8f, mitte.X, 18.2f]);
        Assert.Contains(FusswegRot(quer), f => f.Text.Contains("durch das Haus", StringComparison.Ordinal));
    }

    [Fact]
    public void Spiel_FusswegBisZurTuer_Begehbar_FigurKommtInDenOrt()
    {
        var a = Stadt1();
        AussenBearbeitung.Ziehen(a, ZiehArt.Fussweg, 15.0, 19.2, 14.9, 20.7, true, out var grund);
        Assert.Null(grund);
        var szene = Szene(a);
        foreach (var q in Weg(a).Glatt)
        {
            Assert.True(szene.Laufraster.KannStehen(q.X, q.Y), $"nicht begehbar bei {q}");
        }
        // der Standplatz zum Betreten des Orts (vor der Tuer) liegt auf dem Fussweg: die Figur kommt vom Fussweg in den Ort
        var ort = szene.Ort("bitweiche")!;
        Assert.True(szene.Laufraster.KannStehen(ort.StehX, ort.StehZ), $"Steh {ort.StehX};{ort.StehZ} Weg {string.Join(" ", Weg(a).Glatt.Take(3))}");
        Assert.True(Kurve.Abstand(new Vector2(ort.StehX, ort.StehZ), [.. Weg(a).Glatt]) < 0.02f, $"Standplatz {ort.StehX};{ort.StehZ} liegt nicht auf dem Fussweg");
        // der vorhandene Zugangsweg bleibt, kein Doppelfund
        Assert.NotNull(AussenBearbeitung.Zugang(a, "bitweiche"));
        Assert.Empty(FusswegRot(a));
    }
}
