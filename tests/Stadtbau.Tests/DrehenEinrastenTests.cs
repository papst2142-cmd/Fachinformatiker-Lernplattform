using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Tests;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle E A 0.47.0 / S 0.13.0 (Weisungen P12-63, P12-67 Fragen 1 = a und 2 = a): Haeuser drehen in Vierteln (auch im
/// Spiel), Linien fein (15 Grad, Winkelfeld 1 Grad), Drehgriff, Einrasten von Haeusern, Katalog-Stuecken und Fluessen. Je
/// Fall eine Gegenprobe (ohne Drehung bzw. mit „Einrasten“ aus).
/// </summary>
public class DrehenEinrastenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static PyObjekt Stadt1() => Aussen.Aus(D.Fassung);

    private static PyObjekt Leer() => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_drehen", 32, 20));

    private static readonly AussenStelle Zuhause = new(AussenArt.Haus, -1, 0, "zuhause");

    private static StadtSzene Szene(PyObjekt a)
    {
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        return StadtSzene.Bauen(f.StadtFuer(f.MitKarte(D.Inhalt)), null, D.Grafik);
    }

    private static List<StadtbauFund> Funde(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", ""), D.Inhalt, D.Grafik);

    private static List<Vector2> Linie(PyObjekt a, int nummer) =>
        [.. Liste(Objekt(Aussen.Landschaft(a)[nummer])["punkte"]).Select(p => new Vector2((float)Py.F(Liste(p)[0]), (float)Py.F(Liste(p)[1])))];

    // ---- Haeuser in Vierteln ------------------------------------------------------------------------

    [Fact]
    public void Hausdrehung_VierViertel_SindWiederDerAnfang()
    {
        var m = new Vector2(1.5f, 2.25f);
        var p = new Vector2(4, -1);
        Assert.Equal(p, Hausdrehung.Drehen(p, m, 4));
        // eine Vierteldrehung im Uhrzeigersinn von oben: Front Sued (0, 1) zeigt danach nach Westen (-1, 0)
        Assert.Equal(new Vector2(-1, 0), Hausdrehung.Drehen(new Vector2(0, 1), Vector2.Zero, 1));
        // die Matrix der Szene dreht genau wie die Punkte (x = x, z = y)
        foreach (var dreh in new[] { 1, 2, 3 })
        {
            var w = Vector3.Transform(new Vector3(p.X, 0.7f, p.Y), Hausdrehung.Matrix(m, dreh));
            var q = Hausdrehung.Drehen(p, m, dreh);
            Assert.Equal(q.X, w.X, 4);
            Assert.Equal(q.Y, w.Z, 4);
            Assert.Equal(0.7f, w.Y, 4);
        }
        Assert.True(Hausdrehung.Erlaubt(3L));
        Assert.False(Hausdrehung.Erlaubt(4L));
        Assert.False(Hausdrehung.Erlaubt(1.0));
    }

    [Fact]
    public void Haus_VierMalGedreht_DatenUndSzeneWieVorher()
    {
        var vorher = Stadt1();
        var a = Stadt1();
        for (var i = 0; i < 4; i++)
        {
            Assert.True(AussenBearbeitung.Drehen(a, Zuhause));
        }
        Assert.Equal(0, AussenBearbeitung.HausDreh(a, Zuhause));
        // nach vier Vierteln faellt der Abschnitt hausdreh wieder weg: die Stadt ist bytegleich (Fassung 0 bleibt geschuetzt)
        Assert.Equal(PythonJson.Standard(vorher), PythonJson.Standard(a));
        Assert.Equal(StadtSzeneHash.Stadt(Szene(vorher)), StadtSzeneHash.Stadt(Szene(a)));
    }

    [Fact]
    public void Haus_Gedreht_SpielZeichntTuerUndGrundflaecheGedreht()
    {
        var a = Stadt1();
        var vorher = Szene(a);
        var tuer0 = vorher.Tueren.Single(t => t.Ort == "zuhause");
        var haus0 = vorher.Haeuser.Single(h => h.Id == "zuhause");
        Assert.True(AussenBearbeitung.Drehen(a, Zuhause));
        Assert.Equal(1, Hausdrehung.VonOrt(Aussen.Stadtdaten(a), "zuhause"));
        var nachher = Szene(a);
        var tuer1 = nachher.Tueren.Single(t => t.Ort == "zuhause");
        var haus1 = nachher.Haeuser.Single(h => h.Id == "zuhause");
        // die Tuer zeigt eine Vierteldrehung weiter (Gegenprobe: ungedreht zeigt sie anders)
        var soll = Hausdrehung.Drehen(tuer0.Aussen, Vector2.Zero, 1);
        Assert.True(Vector2.Distance(soll, tuer1.Aussen) < 0.01f, $"Tuer zeigt {tuer1.Aussen}, soll {soll}");
        Assert.True(Vector2.Distance(tuer0.Aussen, tuer1.Aussen) > 1f, "Gegenprobe: Tuer zeigt wie vorher");
        Assert.Equal(tuer0.Modell, tuer1.Modell);
        // die Grundflaeche tauscht Breite und Tiefe
        var g0 = haus0.Max - haus0.Min;
        var g1 = haus1.Max - haus1.Min;
        Assert.Equal(g0.X, g1.Z, 2);
        Assert.Equal(g0.Z, g1.X, 2);
        Assert.Equal(g0.Y, g1.Y, 2);
        // die Tuer der Daten liegt auf der Seite, zu der das Spiel die Tuer zeichnet
        Assert.True(Vector2.Dot(AussenBearbeitung.HausTuerSeite(a, Zuhause), tuer1.Aussen) > 0.9f);
    }

    [Theory]
    [InlineData("elektromarkt", 1)]
    [InlineData("elektromarkt", 2)]
    [InlineData("moebelhaus", 3)]
    public void Haus_MitModelltuer_Gedreht_TuerBleibtAmFussweg(string ort, int mal)
    {
        var a = Stadt1();
        var stelle = new AussenStelle(AussenArt.Haus, -1, 0, ort);
        var vorher = Szene(a).Tueren.Single(t => t.Ort == ort);
        for (var i = 0; i < mal; i++)
        {
            Assert.True(AussenBearbeitung.Drehen(a, stelle));
        }
        var t = Szene(a).Tueren.Single(t => t.Ort == ort);
        Assert.True(vorher.Modell && t.Modell, "die eigene Tuer des Modells");
        Assert.True(Vector2.Distance(Hausdrehung.Drehen(vorher.Aussen, Vector2.Zero, mal), t.Aussen) < 0.01f, $"Richtung {t.Aussen}");
        // die Tuer des Modells sitzt quer zur Wand genau auf dem Anfang des Fusswegs (wie ungedreht)
        var quer = new Vector2(-t.Aussen.Y, t.Aussen.X);
        var querVorher = new Vector2(-vorher.Aussen.Y, vorher.Aussen.X);
        Assert.Equal(Vector2.Dot(vorher.WegAnfang - vorher.Mitte, querVorher), Vector2.Dot(t.WegAnfang - t.Mitte, quer), 2);
    }

    [Fact]
    public void Gebaeude_OhneOrt_Gedreht_UndPruefung()
    {
        var a = Stadt1();
        var stellwerk = new AussenStelle(AussenArt.Gebaeude, 13);
        Assert.True(AussenBearbeitung.Drehen(a, stellwerk));
        Assert.Equal(1L, Objekt(Aussen.Landschaft(a)[13])[Hausdrehung.Feld]);
        var gruen = Funde(a).Where(f => f.Rot).Select(f => f.Text).ToHashSet();
        // ungueltige Werte sind rot (Gegenprobe: gueltiger Wert ohne diesen Fund)
        Py.Setzen(Objekt(Aussen.Landschaft(a)[13]), Hausdrehung.Feld, 5L);
        Assert.Contains(Funde(a), f => f.Rot && !gruen.Contains(f.Text));
        var b = Stadt1();
        var d = Objekt(Aussen.Stadtdaten(b)["darstellung"]);
        Py.Setzen(d, Hausdrehung.Abschnitt, new PyObjekt { { "gibt_es_nicht", 1L } });
        Assert.Contains(Funde(b), f => f.Rot && f.Text.Contains("gibt_es_nicht", StringComparison.Ordinal));
        Py.Setzen(d, Hausdrehung.Abschnitt, new PyObjekt { { "zuhause", 0L } });
        Assert.Contains(Funde(b), f => f.Rot && f.Text.Contains("zuhause", StringComparison.OrdinalIgnoreCase));
        Py.Setzen(d, Hausdrehung.Abschnitt, new PyObjekt { { "zuhause", 2L } });
        Assert.DoesNotContain(Funde(b), f => f.Rot && f.Text.Contains(Hausdrehung.Abschnitt, StringComparison.Ordinal));
    }

    [Fact]
    public void Gebaeude_BeimSetzenGedreht()
    {
        var a = Leer();
        var modell = AussenBearbeitung.HausModelle(a)[0];
        var s = AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Gebaeude, 16, 10, true, modell, 3)!;
        Assert.Equal(3, AussenBearbeitung.HausDreh(a, s));
        var g = AussenBearbeitung.Grundflaeche(a, modell);
        var f = AussenBearbeitung.HausFlaeche(a, s);
        Assert.Equal((g.H, g.W), (f.W, f.H));
    }

    // ---- feines Drehen --------------------------------------------------------------------------------

    [Fact]
    public void Strasse_Fein_15Grad_UndWinkelfeld()
    {
        var a = Leer();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 16, 10)!;
        Assert.Equal(0, AussenBearbeitung.Winkel(a, s));
        var laenge = Vector2.Distance(Linie(a, s.Nummer)[0], Linie(a, s.Nummer)[1]);
        Assert.True(AussenBearbeitung.DrehenSchritt(a, s));
        Assert.Equal(15, AussenBearbeitung.Winkel(a, s));
        Assert.True(AussenBearbeitung.DrehenSchritt(a, s, rueckwaerts: true));
        Assert.True(AussenBearbeitung.DrehenSchritt(a, s, rueckwaerts: true));
        Assert.Equal(345, AussenBearbeitung.Winkel(a, s));
        Assert.True(AussenBearbeitung.WinkelSetzen(a, s, 37));
        Assert.Equal(37, AussenBearbeitung.Winkel(a, s));
        Assert.False(AussenBearbeitung.WinkelSetzen(a, s, 37));
        var l = Linie(a, s.Nummer);
        Assert.Equal(laenge, Vector2.Distance(l[0], l[1]), 1);
        // die Mitte bleibt
        Assert.True(Vector2.Distance((l[0] + l[1]) / 2, new Vector2(16, 10)) < 0.02f);
    }

    [Fact]
    public void Kurve_Fein_BehaeltIhreForm()
    {
        var a = Leer();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 16, 10, modell: "bogen:90:2")!;
        var vorher = Linie(a, s.Nummer);
        Assert.True(AussenBearbeitung.Drehen(a, s, 15));
        var nachher = Linie(a, s.Nummer);
        Assert.True(Strassenkurve.IstKurve(Objekt(Aussen.Landschaft(a)[s.Nummer])));
        Assert.Equal(vorher.Count, nachher.Count);
        Assert.Equal(Vector2.Distance(vorher[0], vorher[^1]), Vector2.Distance(nachher[0], nachher[^1]), 3);
        var d0 = vorher[^1] - vorher[0];
        var d1 = nachher[^1] - nachher[0];
        var grad = (Math.Atan2(d1.Y, d1.X) - Math.Atan2(d0.Y, d0.X)) * 180 / Math.PI;
        Assert.Equal(15, ((grad % 360) + 360) % 360, 1);
    }

    [Fact]
    public void Viertel_BleibenFuerKatalogStegPark_HaeuserSeitA0700In15Grad()
    {
        Assert.Equal(15, AussenBearbeitung.DrehSchritt(AussenArt.Strasse));
        Assert.Equal(15, AussenBearbeitung.DrehSchritt(AussenArt.Fluss));
        foreach (var art in new[] { AussenArt.Katalog, AussenArt.Steg, AussenArt.Park })
        {
            Assert.Equal(90, AussenBearbeitung.DrehSchritt(art));
        }
        // Welle A 0.70.0 (Sammelliste 314): Haeuser drehen in Schritten von 15 Grad (HausFeinDrehenTests)
        Assert.Equal(15, AussenBearbeitung.DrehSchritt(AussenArt.Haus));
        Assert.Equal(15, AussenBearbeitung.DrehSchritt(AussenArt.Gebaeude));
        var a = Stadt1();
        Assert.True(AussenBearbeitung.Drehen(a, Zuhause, -90));
        Assert.Equal(3, AussenBearbeitung.HausDreh(a, Zuhause));
        // Gegenprobe: wie bis S 0.45.0 nur Viertel
        AussenBearbeitung.HausViertelAltFuerTests = true;
        try
        {
            Assert.Equal(90, AussenBearbeitung.DrehSchritt(AussenArt.Haus));
            Assert.False(AussenBearbeitung.Drehen(a, Zuhause, 15));
            Assert.Equal((3, 0), (AussenBearbeitung.HausDreh(a, Zuhause), AussenBearbeitung.HausGrad(a, Zuhause)));
        }
        finally
        {
            AussenBearbeitung.HausViertelAltFuerTests = false;
        }
    }

    // ---- Drehgriff ------------------------------------------------------------------------------------

    [Fact]
    public void Drehgriff_RastetJeArt()
    {
        var (mx, my) = (200.0, 300.0);
        var (gx, gy) = Drehgriff.Lage(mx, my);
        Assert.True(Drehgriff.Getroffen(mx, my, gx + 10, gy + 10));
        Assert.False(Drehgriff.Getroffen(mx, my, gx + 30, gy));
        // Finger rechts neben der Mitte: eine Vierteldrehung im Uhrzeigersinn
        Assert.Equal(90, Drehgriff.Grad(mx, my, mx + 50, my, 15));
        Assert.Equal(-90, Drehgriff.Grad(mx, my, mx - 50, my, 90));
        // 20 Grad: Linien rasten auf 15, Haeuser bleiben bei 0
        var w = 20 * Math.PI / 180;
        var (x, y) = (mx + (50 * Math.Sin(w)), my - (50 * Math.Cos(w)));
        Assert.Equal(15, Drehgriff.Grad(mx, my, x, y, 15));
        Assert.Equal(0, Drehgriff.Grad(mx, my, x, y, 90));
    }

    // ---- Einrasten ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Gebaeude_LegtSichAnDieGehwegkante(bool einrasten)
    {
        var a = Leer();
        AussenBearbeitung.Setzen(a, AussenArt.Strasse, 16, 10);
        var modell = AussenBearbeitung.HausModelle(a)[0];
        var g = AussenBearbeitung.Grundflaeche(a, modell);
        var kante = 10 - ((StadtSzene.StrassenBreite / 2) + AussenBearbeitung.NeuerGehweg);
        // Vorderseite (Sued) 0,4 vor der Gehwegkante
        var s = AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 16, kante - (g.H / 2) - 0.4, einrasten, modell)!;
        var f = AussenBearbeitung.HausFlaeche(a, s);
        if (einrasten)
        {
            Assert.Equal(kante, f.Y + f.H, 2);
        }
        else
        {
            Assert.True(Math.Abs(kante - (f.Y + f.H)) > 0.2, "Gegenprobe: liegt auch ohne Einrasten an");
        }
    }

    [Fact]
    public void Gebaeude_OhneGehweg_WieDieLaedenDerStadt1()
    {
        var a = Leer();
        var st = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 16, 10)!;
        AussenBearbeitung.GehwegSetzen(a, st, 0);
        var modell = AussenBearbeitung.HausModelle(a)[0];
        var g = AussenBearbeitung.Grundflaeche(a, modell);
        var s = AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 16, 10 - AussenBearbeitung.HausAbstandOhneGehweg - (g.H / 2) - 0.7, true, modell)!;
        var f = AussenBearbeitung.HausFlaeche(a, s);
        Assert.Equal(10 - AussenBearbeitung.HausAbstandOhneGehweg, f.Y + f.H, 2);
    }

    [Fact]
    public void Gebaeude_Verschieben_RastetNurMitEinrasten()
    {
        var a = Leer();
        AussenBearbeitung.Setzen(a, AussenArt.Strasse, 16, 10);
        var modell = AussenBearbeitung.HausModelle(a)[0];
        var g = AussenBearbeitung.Grundflaeche(a, modell);
        var kante = 10 - ((StadtSzene.StrassenBreite / 2) + AussenBearbeitung.NeuerGehweg);
        var s = AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 16, kante - (g.H / 2) - 3, false, modell)!;
        var b = (PyObjekt)Py.Kopie(a)!;
        AussenBearbeitung.Verschieben(a, s, 0, 2.5);
        var f = AussenBearbeitung.HausFlaeche(a, s);
        Assert.Equal(kante, f.Y + f.H, 2);
        AussenBearbeitung.Verschieben(b, s, 0, 2.5, raster: false);
        var fb = AussenBearbeitung.HausFlaeche(b, s);
        Assert.True(Math.Abs(kante - (fb.Y + fb.H)) > 0.2, "Gegenprobe: ohne Einrasten angelegt");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Katalog_RueckseiteAnDerGehwegkante(bool einrasten)
    {
        var e = Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Modell && k.Aussen[1] <= 0.3 && k.Aussen[0] <= 1.5);
        var a = Leer();
        AussenBearbeitung.Setzen(a, AussenArt.Strasse, 16, 10);
        var kante = 10 - ((StadtSzene.StrassenBreite / 2) + AussenBearbeitung.NeuerGehweg);
        var s = AussenBearbeitung.Setzen(a, AussenArt.Katalog, 16, kante - 0.6, einrasten, e.Id)!;
        var k = Objekt(Aussen.KatalogStuecke(a)[s.Nummer]);
        var hinten = Py.F(k["y"]) - (Py.F(k["t"]) / 2);
        if (einrasten)
        {
            Assert.Equal(kante, hinten, 2);
        }
        else
        {
            Assert.True(Math.Abs(kante - hinten) > 0.2, "Gegenprobe: liegt auch ohne Einrasten an");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Fluss_EndeAnEnde(bool einrasten)
    {
        var a = Leer();
        var f1 = AussenBearbeitung.Setzen(a, AussenArt.Fluss, 10, 10)!;
        var ende = Linie(a, f1.Nummer)[^1];
        var f2 = AussenBearbeitung.Setzen(a, AussenArt.Fluss, ende.X + 3.5, 10.4, einrasten)!;
        var anfang = Linie(a, f2.Nummer)[0];
        if (einrasten)
        {
            Assert.True(Vector2.Distance(anfang, ende) < 0.01f, $"Anfang {anfang}, Ende {ende}");
        }
        else
        {
            Assert.True(Vector2.Distance(anfang, ende) > 0.3f, "Gegenprobe: auch ohne Einrasten angelegt");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Fluss_AnDenKartenrand(bool einrasten)
    {
        var a = Leer();
        var f = AussenBearbeitung.Setzen(a, AussenArt.Fluss, 3.5, 10, einrasten)!;
        var l = Linie(a, f.Nummer);
        Assert.Equal(einrasten ? 0 : 0.5f, l[0].X, 3);
        Assert.Equal(l[1].Y, l[0].Y, 3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Fluss_EckpunktLegtBrueckeQuer(bool einrasten)
    {
        var a = Leer();
        AussenBearbeitung.Setzen(a, AussenArt.Strasse, 16, 10);
        var f = AussenBearbeitung.Setzen(a, AussenArt.Fluss, 16, 6)!;
        var l = Linie(a, f.Nummer);
        Assert.Equal(new Vector2(16, 6), l[1]);
        // das Ende (19, 6) nach (17, 13): das Stueck kreuzt die Strasse um 8 Grad schief
        AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Fluss, f.Nummer, 2), -2, 7, einrasten);
        var neu = Linie(a, f.Nummer)[2];
        if (einrasten)
        {
            Assert.Equal(16, neu.X, 2);
            Assert.True(neu.Y > 12.9f);
        }
        else
        {
            Assert.Equal(17, neu.X, 2);
        }
    }
}
