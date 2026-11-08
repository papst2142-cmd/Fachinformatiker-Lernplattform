using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle K2 (A 0.49.0 / S 0.15.0, Weisung P12-72, Plan P12-36, Nico 2 = a): die Bahn im Werkzeug. Setzen (drei Punkte wie
/// der Fluss, Feld <c>radien</c> immer dabei), Eckpunkte, Radius 3 oder 4,5 je Ecke, Einrasten Ende an Ende und am
/// Kartenrand, fein drehen, kopieren, loeschen, „Bahn ziehen“; das Spiel zeichnet das Gleis mit Bogen, Bahnuebergang und Zug.
/// Je Fall mit Gegenprobe.
/// </summary>
public class BahnTests
{
    private static Spieldaten D => Daten.Spiel;

    private static PyObjekt Leer() => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_bahn", 32, 20));

    private static PyObjekt Eintrag(PyObjekt a, AussenStelle s) => Objekt(Aussen.Landschaft(a)[s.Nummer]);

    private static List<double> RadienVon(PyObjekt a, AussenStelle s) => [.. Liste(Eintrag(a, s)["radien"]).Select(Py.F)];

    private static List<StadtbauFund> Funde(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D.Inhalt, D.Grafik);

    private static StadtSzene Szene(PyObjekt a)
    {
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        return StadtSzene.Bauen(f.StadtFuer(f.MitKarte(D.Inhalt)), null, D.Grafik);
    }

    [Fact]
    public void Setzen_DreiPunkteMitRadien_WaehlbarUndOhneFund()
    {
        var a = Leer();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Bahn, 16, 10)!;
        Assert.Equal(AussenArt.Bahn, s.Art);
        Assert.Equal(-1, s.Punkt);
        Assert.Equal("bahn", Eintrag(a, s)["typ"]);
        Assert.Equal(3, AussenBearbeitung.EckpunkteWerte(a, s).Count);
        Assert.Equal([3.0, 3.0, 3.0], RadienVon(a, s));
        Assert.Equal(AussenBearbeitung.NeueBahn, Vector2.Distance(AussenBearbeitung.EckpunkteWerte(a, s)[0], AussenBearbeitung.EckpunkteWerte(a, s)[^1]), 3);
        // auf dem Gleis getippt = die Bahn, am Eckpunkt = der Eckpunkt
        Assert.Equal(s, AussenBearbeitung.StueckBei(a, 14.7, 10.05));
        Assert.Equal(s with { Punkt = 2 }, AussenBearbeitung.StueckBei(a, 20, 10));
        Assert.Contains("Bahn", AussenBearbeitung.Beschreibung(a, s, n => n), StringComparison.Ordinal);
        Assert.DoesNotContain(Funde(a), f => f.Rot);
        // Gegenprobe: der Fluss an derselben Stelle bekommt kein Feld radien und ist ein Fluss
        var b = Leer();
        var f = AussenBearbeitung.Setzen(b, AussenArt.Fluss, 16, 10)!;
        Assert.Equal(AussenArt.Fluss, f.Art);
        Assert.False(Py.Hat(Eintrag(b, f), "radien"));
    }

    [Fact]
    public void Radius_NurDreiOderVierEinhalb_FeldBleibt()
    {
        var a = Leer();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Bahn, 16, 10)!;
        var mitte = s with { Punkt = 1 };
        AussenBearbeitung.Verschieben(a, mitte, 0, 4);
        Assert.True(AussenBearbeitung.RadiusWaehlbar(a, mitte));
        Assert.False(AussenBearbeitung.RadiusWaehlbar(a, s with { Punkt = 0 }));
        Assert.Equal(Strassenkurve.BahnRadien, AussenBearbeitung.Radien(AussenArt.Bahn));
        Assert.True(AussenBearbeitung.FlussRadiusSetzen(a, mitte, 4.5));
        Assert.Equal(4.5, AussenBearbeitung.FlussRadius(a, mitte));
        Assert.False(AussenBearbeitung.FlussRadiusSetzen(a, mitte, 2));
        Assert.False(AussenBearbeitung.FlussRadiusSetzen(a, mitte, 5));
        // zurueck auf 3: die Bahn behaelt das Feld (der Fluss wuerde es abgeben)
        Assert.True(AussenBearbeitung.FlussRadiusSetzen(a, mitte, 3));
        Assert.Equal([3.0, 3.0, 3.0], RadienVon(a, s));
        // Eckpunkt dazu und weg: radien laeuft mit
        var neu = AussenBearbeitung.EckpunktDazu(a, mitte)!;
        Assert.Equal(4, RadienVon(a, s).Count);
        Assert.True(AussenBearbeitung.EckpunktWeg(a, neu));
        Assert.Equal(3, RadienVon(a, s).Count);
    }

    [Fact]
    public void Einrasten_EndeAnEndeEinerBahn_NichtAmFluss_UndAmKartenrand()
    {
        var a = Leer();
        var erste = AussenBearbeitung.Setzen(a, AussenArt.Bahn, 8, 10)!;   // (4; 10) bis (12; 10)
        var zweite = AussenBearbeitung.Setzen(a, AussenArt.Bahn, 17, 10.5, raster: false)!;
        AussenBearbeitung.Verschieben(a, zweite, -0.5, -0.5);
        var p = AussenBearbeitung.EckpunkteWerte(a, zweite);
        Assert.Equal(new Vector2(12, 10), p[0]);
        Assert.Equal(10, p[^1].Y, 3);
        Assert.DoesNotContain(Funde(a), f => f.Rot);
        // Gegenprobe: ein Fluss-Ende faengt die Bahn nicht
        var b = Leer();
        AussenBearbeitung.Setzen(b, AussenArt.Fluss, 8, 10);
        var bahn = AussenBearbeitung.Setzen(b, AussenArt.Bahn, 17, 10.5, raster: false)!;
        AussenBearbeitung.Verschieben(b, bahn, -0.5, -0.5);
        Assert.NotEqual(new Vector2(12, 10), AussenBearbeitung.EckpunkteWerte(b, bahn)[0]);
        // Kartenrand: das Ende legt sich auf den Rand
        var c = Leer();
        var r = AussenBearbeitung.Setzen(c, AussenArt.Bahn, 4.5, 6)!;   // (0,5; 6) bis (8,5; 6)
        AussenBearbeitung.Verschieben(c, r with { Punkt = 0 }, 0, 0.5);
        Assert.Equal(0, AussenBearbeitung.EckpunkteWerte(c, r)[0].X, 3);
        _ = erste;
    }

    [Fact]
    public void Drehen_Fein15Grad_KopierenLoeschen()
    {
        var a = Leer();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Bahn, 16, 10)!;
        Assert.True(AussenBearbeitung.FeinDrehbar(AussenArt.Bahn));
        Assert.Equal(0, AussenBearbeitung.Winkel(a, s));
        Assert.True(AussenBearbeitung.DrehenSchritt(a, s));
        Assert.Equal(15, AussenBearbeitung.Winkel(a, s));
        var k = AussenBearbeitung.Kopieren(a, s)!;
        Assert.Equal(AussenArt.Bahn, k.Art);
        Assert.Equal(PythonJson.Standard(Eintrag(a, s)["radien"]), PythonJson.Standard(Eintrag(a, k)["radien"]));
        Assert.True(AussenBearbeitung.Loeschen(a, k));
        Assert.Single(Aussen.Landschaft(a).OfType<PyObjekt>(), l => Py.Gleich(l["typ"], "bahn"));
    }

    [Fact]
    public void Ziehen_VomKartenrand_MitBoegen4_5Und3_ImmerMitRadien()
    {
        var a = Leer();
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Bahn, 0.3, 6, 22, 19.7, true, out var grund)!;
        Assert.Null(grund);
        Assert.Equal(AussenArt.Bahn, s.Art);
        var p = AussenBearbeitung.EckpunkteWerte(a, s);
        Assert.Equal(0, p[0].X, 3);
        Assert.Equal(p[0].Y, p[1].Y, 3);
        Assert.Equal(20, p[^1].Y, 3);
        var radien = RadienVon(a, s);
        Assert.Equal(p.Count, radien.Count);
        Assert.All(radien.Skip(1).SkipLast(1), r => Assert.Contains(r, Strassenkurve.BahnRadien));
        Assert.Contains(4.5, radien);
        Assert.DoesNotContain(Funde(a), f => f.Rot);
        // gerade gezogen: alle 3, das Feld bleibt trotzdem (Gegenprobe Fluss: ohne Feld)
        var b = Leer();
        var g = AussenBearbeitung.Ziehen(b, ZiehArt.Bahn, 2, 4, 12, 4, true, out _)!;
        Assert.True(Py.Hat(Eintrag(b, g), "radien"));
        var c = Leer();
        var fl = AussenBearbeitung.Ziehen(c, ZiehArt.Fluss, 2, 4, 12, 4, true, out _)!;
        Assert.False(Py.Hat(Eintrag(c, fl), "radien"));
    }

    [Fact]
    public void Ziehen_VomGleisende_Verlaengert_EinStueck()
    {
        var a = Leer();
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Bahn, 0.3, 6, 10, 6, true, out _)!;
        var t = AussenBearbeitung.Ziehen(a, ZiehArt.Bahn, 10, 6, 24, 16, true, out var grund)!;
        Assert.Null(grund);
        Assert.Equal(s.Nummer, t.Nummer);
        Assert.Single(Aussen.Landschaft(a).OfType<PyObjekt>(), l => Py.Gleich(l["typ"], "bahn"));
        Assert.DoesNotContain(new Vector2(10, 6), AussenBearbeitung.EckpunkteWerte(a, t));
        Assert.DoesNotContain(Funde(a), f => f.Rot);
        // Gegenprobe: ohne Einrasten entsteht ein zweites Gleis (der Anfang faengt nicht am Ende)
        var b = Leer();
        AussenBearbeitung.Ziehen(b, ZiehArt.Bahn, 0.3, 6, 10, 6, true, out _);
        AussenBearbeitung.Ziehen(b, ZiehArt.Bahn, 10.3, 6.2, 24, 16, false, out _);
        Assert.Equal(2, Aussen.Landschaft(b).OfType<PyObjekt>().Count(l => Py.Gleich(l["typ"], "bahn")));
    }

    [Fact]
    public void Spiel_BogenMitBahnuebergangUndZug_GleisGesperrt()
    {
        var a = Leer();
        AussenBearbeitung.Ziehen(a, ZiehArt.Bahn, 0.3, 6, 22, 19.7, true, out _);
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 6.0, 0.0 }, new List<object?> { 6.0, 12.0 } } } });
        Assert.DoesNotContain(Funde(a), f => f.Rot);
        var szene = Szene(a);
        Assert.Single(szene.Gleise);
        Assert.Contains(szene.Uebergaenge, u => u.Strasse);
        Assert.NotEmpty(szene.Andreaskreuze);
        // das Gleis ist im Bogen gesperrt, auf dem Bahnuebergang frei
        var glatt = szene.Gleise[0];
        var bogen = glatt[glatt.Count / 2];
        Assert.False(szene.Laufraster.KannStehen(bogen.X, bogen.Y));
        Assert.True(szene.Laufraster.KannStehen(6, 6));
        // Gegenprobe ohne Gleis: dort frei
        var b = Leer();
        Assert.True(Szene(b).Laufraster.KannStehen(bogen.X, bogen.Y));
    }
}
