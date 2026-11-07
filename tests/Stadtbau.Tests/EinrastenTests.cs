using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Patch A 0.46.1 / S 0.12.1 (Weisungen P12-62, P12-66, Sammelliste 233, Fund Nico am Tablet): Strassenenden rasten mit Lage
/// UND Richtung ein, so dass das Spiel sie als Anschluss erkennt (<see cref="StadtSzene.Anschluss"/>: Mittellinie durchgehend,
/// keine Endkappe, kein Ueberlappen). Je Fall die Gegenprobe mit dem Einrasten vor dem Patch
/// (<see cref="AussenBearbeitung.EinrastenAltFuerTests"/>): dort fehlt der Anschluss.
/// </summary>
public class EinrastenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static PyObjekt Leer() => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_einrasten", 32, 20));

    private static List<List<Vector2>> Linien(PyObjekt a) => AussenBearbeitung.Strassen(a);

    private static List<Vector2> Linie(PyObjekt a, AussenStelle s) =>
        [.. Liste(Objekt(Aussen.Landschaft(a)[s.Nummer])["punkte"]).Select(p => new Vector2((float)Py.F(Liste(p)[0]), (float)Py.F(Liste(p)[1])))];

    /// <summary>Stelle einer Strasse unter allen Strassen (fuer die Ketten der Mittellinie).</summary>
    private static int Nr(PyObjekt a, AussenStelle s) =>
        Aussen.Landschaft(a).Take(s.Nummer).OfType<PyObjekt>().Count(t => Py.Gleich(t["typ"], "strasse"));

    /// <summary>Liegen die beiden Strassen in derselben Kette der Mittellinie (= Anschluss wie im Spiel)?</summary>
    private static bool Angeschlossen(PyObjekt a, AussenStelle x, AussenStelle y)
    {
        var (i, j) = (Nr(a, x), Nr(a, y));
        return StadtSzene.MittellinienZuege(Linien(a)).Any(z => z.Strassen.Contains(i) && z.Strassen.Contains(j));
    }

    private static List<StadtbauFund> Funde(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D.Inhalt, D.Grafik);

    /// <summary>Fuehrt den Fall mit dem neuen Einrasten und als Gegenprobe mit dem alten aus.</summary>
    private static void MitGegenprobe(Func<(PyObjekt A, AussenStelle X, AussenStelle Y)> fall)
    {
        var neu = fall();
        Assert.True(Angeschlossen(neu.A, neu.X, neu.Y), "kein Anschluss: " + string.Join(" | ", Linien(neu.A).Select(l => $"{l[0]}-{l[^1]}")));
        Assert.DoesNotContain(Funde(neu.A), f => f.Rot);
        AussenBearbeitung.EinrastenAltFuerTests = true;
        try
        {
            var alt = fall();
            Assert.False(Angeschlossen(alt.A, alt.X, alt.Y), "Gegenprobe: auch vor dem Patch angeschlossen");
        }
        finally
        {
            AussenBearbeitung.EinrastenAltFuerTests = false;
        }
    }

    [Fact]
    public void Setzen_GeradeAnKurve_Nicos_Fund_RagtNichtInDenBogen()
    {
        // Nicos Foto (N1): Kurve 90 Grad Radius 2, dann eine Gerade, deren Ende 0,5 in den Bogen ragt
        MitGegenprobe(() =>
        {
            var a = Leer();
            var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 18, 13, modell: "bogen:90:2")!;
            var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 15.5, 12)!;
            return (a, g, k);
        });
        var b = Leer();
        AussenBearbeitung.Setzen(b, AussenArt.Kurve, 18, 13, modell: "bogen:90:2");
        var gerade = Linie(b, AussenBearbeitung.Setzen(b, AussenArt.Strasse, 15.5, 12)!);
        Assert.Equal(new Vector2(17, 12), gerade[1]);
        Assert.Equal(4, Vector2.Distance(gerade[0], gerade[1]), 2);
    }

    [Fact]
    public void Ziehen_GeradeAnKurve_InRasterschritten()
    {
        MitGegenprobe(() =>
        {
            var a = Leer();
            var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 18, 13, modell: "bogen:90:2")!;
            var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 12, 12)!;
            // wie mit dem Finger: 3,5 nach rechts, Ende auf 17,5 = 0,5 im Bogen
            AussenBearbeitung.Verschieben(a, g, 3.5, 0);
            return (a, g, k);
        });
    }

    [Fact]
    public void Setzen_KurveSchraegNebenDemEnde_Fangweite095()
    {
        // N2: ein Rasterfeld schraeg daneben (0,71): rastet jetzt ein, vorher nicht (0,5)
        MitGegenprobe(() =>
        {
            var a = Leer();
            var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 15, 12)!;
            var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 18.5, 13.5, modell: "bogen:90:2")!;
            return (a, g, k);
        });
        // weiter als eine Strassenbreite: kein Einrasten
        var b = Leer();
        AussenBearbeitung.Setzen(b, AussenArt.Strasse, 15, 12);
        var k2 = AussenBearbeitung.Setzen(b, AussenArt.Kurve, 19, 13, modell: "bogen:90:2")!;
        Assert.Equal(new Vector2(18, 12), Linie(b, k2)[0]);
    }

    [Fact]
    public void Ziehen_KurveSchraegAnDieGerade_DrehtSichInRichtung()
    {
        // Kurve um 30 Grad gedreht neben dem Ende: sie dreht sich beim Einrasten auf die Richtung der Geraden
        MitGegenprobe(() =>
        {
            var a = Leer();
            var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 15, 12)!;
            var land = Aussen.Landschaft(a);
            var linie = Strassenkurve.Mittellinie(Strassenkurve.Bogen, 90, 2);
            var (s, c) = MathF.SinCos(30 * MathF.PI / 180);
            land.Add(new PyObjekt
            {
                { "typ", "strasse" },
                { "punkte", linie.Select(p => (object?)new List<object?> { Math.Round(17.3 + (p.X * c) - (p.Y * s), 2), Math.Round(12.2 + (p.X * s) + (p.Y * c), 2) }).ToList() },
                { "gehweg", 0.35 },
                { Strassenkurve.Feld, new PyObjekt { { "form", "bogen" }, { "winkel", 90L }, { "radius", 2.0 } } },
            });
            var k = new AussenStelle(AussenArt.Strasse, land.Count - 1);
            AussenBearbeitung.Verschieben(a, k, 0.5, 0);
            AussenBearbeitung.Verschieben(a, k, -0.5, 0);
            return (a, g, k);
        });
    }

    [Fact]
    public void Drehen_KurveRastetNurInRichtungEin_QuerBleibtFrei()
    {
        var a = Leer();
        var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 15, 12)!;
        var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 18, 13, modell: "bogen:90:2")!;
        Assert.True(Angeschlossen(a, g, k));
        // zweimal drehen: das andere Ende stuende quer am Strassenende; es rastet dort nicht ein und es gibt keinen Anschluss
        AussenBearbeitung.Drehen(a, k);
        AussenBearbeitung.Drehen(a, k);
        Assert.False(Angeschlossen(a, g, k));
        // viermal: wieder genau angeschlossen, ohne dass die Kurve wandert
        AussenBearbeitung.Drehen(a, k);
        AussenBearbeitung.Drehen(a, k);
        Assert.True(Angeschlossen(a, g, k));
        Assert.Equal(new Vector2(19, 14), Linie(a, k)[^1]);
    }

    [Fact]
    public void Spiegeln_KurveBleibtAngeschlossen()
    {
        MitGegenprobe(() =>
        {
            var a = Leer();
            var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 15, 12)!;
            // Kurve in Gegenrichtung gesetzt (Ende am Strassenende), dann gespiegelt
            var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 18.5, 11.5, modell: "bogen:90:2:m")!;
            AussenBearbeitung.Spiegeln(a, k);
            AussenBearbeitung.Verschieben(a, k, 0.5, 0.5);
            return (a, g, k);
        });
    }

    [Fact]
    public void KurveAnKurve_RastetMitRichtungEin()
    {
        MitGegenprobe(() =>
        {
            var a = Leer();
            var k1 = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 10, 9, modell: "bogen:90:2")!;
            // Ende der ersten Kurve (11; 10), Richtung nach unten; die zweite beginnt nach unten, 0,57 daneben (vorher: Fangweite 0,5)
            var land = Aussen.Landschaft(a);
            land.Add(new PyObjekt
            {
                { "typ", "strasse" },
                { "punkte", Strassenkurve.Mittellinie(Strassenkurve.Bogen, 90, 2).Select(p => (object?)new List<object?> { Math.Round(11.4 - p.Y, 2), Math.Round(10.4 + p.X, 2) }).ToList() },
                { "gehweg", 0.35 },
                { Strassenkurve.Feld, new PyObjekt { { "form", "bogen" }, { "winkel", 90L }, { "radius", 2.0 } } },
            });
            var k2 = new AussenStelle(AussenArt.Strasse, land.Count - 1);
            AussenBearbeitung.Verschieben(a, k2, 0.5, 0);
            AussenBearbeitung.Verschieben(a, k2, -0.5, 0);
            return (a, k1, k2);
        });
    }

    [Fact]
    public void GeradeAnKurvenende_LegtSichInRichtungDerKurve()
    {
        MitGegenprobe(() =>
        {
            var a = Leer();
            var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 10, 9, modell: "bogen:90:2")!;
            // Ende (11; 10) zeigt nach unten: eine waagrechte Gerade daneben dreht sich nicht (90 Grad), eine senkrechte rastet ein
            var land = Aussen.Landschaft(a);
            land.Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 11.5, 10.5 }, new List<object?> { 11.5, 14.5 } } }, { "gehweg", 0.35 } });
            var g = new AussenStelle(AussenArt.Strasse, land.Count - 1);
            AussenBearbeitung.Verschieben(a, g, -0.5, 0);
            AussenBearbeitung.Verschieben(a, g, 0.5, 0);
            AussenBearbeitung.Verschieben(a, g, -0.5, 0);
            return (a, k, g);
        });
    }

    [Fact]
    public void KurveAnZufahrtDesKreisverkehrs()
    {
        MitGegenprobe(() =>
        {
            var a = Leer();
            Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Kreisverkehr, 12, 10, modell: AussenBearbeitung.Kennung(3)));
            // Zufahrt Ost endet bei (17,21; 10); die Kurve beginnt 0,94 schraeg daneben (vorher: Fangweite 0,5)
            var land = Aussen.Landschaft(a);
            var zufahrt = Enumerable.Range(0, land.Count).Select(i => new AussenStelle(AussenArt.Strasse, i))
                .First(s => Py.Gleich(Objekt(land[s.Nummer])["typ"], "strasse") && Linie(a, s).Any(p => p.X > 17));
            var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 19, 11.5, modell: "bogen:90:2")!;
            return (a, zufahrt, k);
        });
    }

    [Fact]
    public void Einmuendung_NurAufGeradenStrassen_NieInDenBogen()
    {
        // Gerade quer auf die Mitte eines Bogens: vorher rastete sie auf die Kurvenlinie (Regel 2 rot), jetzt bleibt sie frei
        var a = Leer();
        var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 14, 10, modell: "bogen:90:3")!;
        var mitte = Linie(a, k)[6];
        var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, mitte.X - 2.3, mitte.Y)!;
        Assert.True(Kurve.Abstand(Linie(a, g)[1], Linie(a, k)) > 0.05f);
        // an einer geraden Strasse rastet die Einmuendung wie bisher mit dem Fusspunkt ein
        var b = Leer();
        AussenBearbeitung.Setzen(b, AussenArt.Strasse, 15, 10);
        var land = Aussen.Landschaft(b);
        land.Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 14.0, 6.0 }, new List<object?> { 14.0, 9.5 } } }, { "gehweg", 0.35 } });
        AussenBearbeitung.Verschieben(b, new AussenStelle(AussenArt.Strasse, land.Count - 1), 0, 0.5);
        Assert.Equal(new Vector2(14, 10), Linie(b, new AussenStelle(AussenArt.Strasse, land.Count - 1))[1]);
    }

    [Fact]
    public void Pruefung_KurveQuerAmEnde_Rot_InRichtungGruen()
    {
        var a = Leer();
        var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 15, 12)!;
        var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 18, 13, modell: "bogen:90:2")!;
        Assert.DoesNotContain(Funde(a), f => f.Text.Contains("stößt quer"));
        // Kurve so legen, dass ihr ENDE genau auf dem Strassenende liegt, aber quer (Nachstellung N3 vor dem Patch)
        var punkte = Liste(Objekt(Aussen.Landschaft(a)[k.Nummer])["punkte"]);
        var linie = Linie(a, k);
        var m = new Vector2(18, 13);
        for (var i = 0; i < punkte.Count; i++)
        {
            var d = linie[i] - m;
            var q = m - d;
            punkte[i] = new List<object?> { Math.Round(q.X, 2), Math.Round(q.Y, 2) };
        }
        Assert.Equal(new Vector2(17, 12), Linie(a, k)[^1]);
        var quer = Funde(a).Where(f => f.Text.Contains("stößt quer")).ToList();
        Assert.Single(quer);
        Assert.True(quer[0].Rot);
        Assert.False(Angeschlossen(a, g, k));
    }

    [Fact]
    public void Umriss_KurveIstEinBand_GegenprobeJeStueck()
    {
        var a = Leer();
        var k = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 18, 13, modell: "bogen:90:2")!;
        var band = Bauen3D.Umriss(a, k);
        Assert.Single(band);
        Assert.Equal(26, band[0].Length);
        // das Band liegt ueberall eine halbe Strassenbreite plus Gehweg neben der Mittellinie
        var halb = (StadtSzene.StrassenBreite / 2) + 0.35f;
        Assert.All(band[0], p => Assert.InRange(Kurve.Abstand(p, Linie(a, k)), halb - 0.03f, halb + 0.03f));
        Bauen3D.UmrissJeStueckFuerTests = true;
        try
        {
            Assert.Equal(12, Bauen3D.Umriss(a, k).Count);
        }
        finally
        {
            Bauen3D.UmrissJeStueckFuerTests = false;
        }
        // gerade Strasse: wie bisher ein Rechteck
        var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 10, 4)!;
        Assert.Single(Bauen3D.Umriss(a, g));
        Assert.Equal(4, Bauen3D.Umriss(a, g)[0].Length);
    }

    [Fact]
    public void Vorschau3D_GleichDemErgebnis()
    {
        var a = Leer();
        AussenBearbeitung.Setzen(a, AussenArt.Kurve, 18, 13, modell: "bogen:90:2");
        var g = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 12, 12)!;
        var vorschau = Bauen3D.Verschieben(a, g, 3.5, 0, raster: true);
        AussenBearbeitung.Verschieben(a, g, 3.5, 0);
        Assert.Equal(Linie(a, g), Linie(vorschau.Stand, g));
        Assert.Equal(new Vector2(17, 12), Linie(a, g)[1]);
    }
}
