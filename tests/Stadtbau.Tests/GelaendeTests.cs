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
/// Welle A 0.66.0 / S 0.42.0 (Weisung P12-192, Nico 1c 2b 3c 4a 5a): Gelaende Stufe 1. Werkzeug (Huegel setzen, schieben,
/// drehen, kopieren, loeschen, Werte; Pinsel heben, senken, glaetten), Spiel (Hoehe = groesster Wert aus Kuppen und Gemaltem,
/// Stuecke auf der Hoehe ihrer Mitte, Kleingruen nur flach, Laufraster bei sanften Huegeln gleich, steil gesperrt, Fassung
/// ohne Gelaende unveraendert) und die Pruefregeln je rot und gruen, jeweils mit Gegenprobe.
/// Grundlage: leere Stadt 30 × 20, Strasse (2; 3)–(28; 3) mit Gehweg 0,35; Huegel um (15; 13).
/// </summary>
public class GelaendeTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Grundlage(bool huegel = true)
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_gelaende", 30, 20));
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(2, 3), P(28, 3) } }, { "gehweg", 0.35 } });
        if (huegel)
        {
            Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Huegel, 15, 13));
        }
        return a;
    }

    private static StadtFassung F(PyObjekt a) => Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", "");

    private static List<StadtbauFund> Pruefen(PyObjekt a) => StadtbauPruefung.Aussen(F(a), D.Inhalt, D.Grafik);

    private static string Text(List<StadtbauFund> f) => string.Join(" | ", f.Select(x => (x.Rot ? "rot: " : "gelb: ") + x.Text));

    private static void Gruen(PyObjekt a, string teil)
    {
        var f = Pruefen(a);
        Assert.True(!f.Any(x => x.Rot && x.Text.Contains(teil, StringComparison.Ordinal)), "unerwartet rot „" + teil + "“: " + Text(f));
    }

    private static void Rot(PyObjekt a, string teil)
    {
        var f = Pruefen(a);
        Assert.True(f.Any(x => x.Rot && x.Text.Contains(teil, StringComparison.Ordinal)), "erwartet rot „" + teil + "“, gefunden: " + Text(f));
    }

    private static StadtSzene Szene(PyObjekt a) => Ansicht3D.Bauen(F(a), D, Ansicht3D.Einstellung.Standard).Szene;

    private static PyObjekt Huegel(PyObjekt a, int i) => Objekt(AussenBearbeitung.HuegelListe(a)[i]);

    private static AussenStelle H(int i) => new(AussenArt.Huegel, i);

    /// <summary>Gelaende-Abschnitt direkt schreiben (fuer Regeln, die das Werkzeug nicht zulaesst).</summary>
    private static void Abschnitt(PyObjekt a, PyObjekt g) => Py.Setzen(Aussen.Stadtdaten(a), Gelaende.Abschnitt, g);

    private static PyObjekt HuegelDaten(double x, double y, double radius, double hoehe) =>
        new() { { "x", x }, { "y", y }, { "radius", radius }, { "hoehe", hoehe }, { "form", "rund" } };

    // ---- Werkzeug ------------------------------------------------------------------------------------

    [Fact]
    public void Setzen_Schieben_Kopieren_Loeschen_LetzterNimmtDenAbschnittMit()
    {
        var a = Grundlage();
        Assert.Single(AussenBearbeitung.HuegelListe(a));
        Assert.Equal(15.0, Py.F(Huegel(a, 0)["x"]));
        Assert.Equal(AussenBearbeitung.HuegelRadiusNeu, Py.F(Huegel(a, 0)["radius"]));
        AussenBearbeitung.Verschieben(a, H(0), 1, -0.5);
        Assert.Equal(16.0, Py.F(Huegel(a, 0)["x"]));
        Assert.Equal(12.5, Py.F(Huegel(a, 0)["y"]));
        var kopie = AussenBearbeitung.Kopieren(a, H(0));
        Assert.NotNull(kopie);
        Assert.Equal(2, AussenBearbeitung.HuegelListe(a).Count);
        AussenBearbeitung.Loeschen(a, H(1));
        AussenBearbeitung.Loeschen(a, H(0));
        Assert.False(Py.Hat(Aussen.Stadtdaten(a), Gelaende.Abschnitt));
    }

    [Fact]
    public void Werte_WerdenBegrenzt_UndRundDrehtNicht_LaenglichIn15Grad()
    {
        var a = Grundlage();
        AussenBearbeitung.HuegelWerteSetzen(a, H(0), 9, 9, false, 0);
        var (r, h, lang, _, _) = AussenBearbeitung.HuegelWerte(a, H(0));
        Assert.Equal(Gelaende.RadiusMax, r);
        Assert.Equal(Gelaende.HoeheMax, h, 3);
        Assert.False(lang);
        // Gegenprobe: ein runder Huegel dreht nicht, ein laenglicher in 15-Grad-Schritten
        Assert.False(AussenBearbeitung.Drehen(a, H(0), 15));
        AussenBearbeitung.HuegelWerteSetzen(a, H(0), 2, 1, true, 5);
                Assert.True(AussenBearbeitung.Drehen(a, H(0), 15));
        Assert.Equal(15, AussenBearbeitung.HuegelWerte(a, H(0)).Dreh);
        Assert.Equal(5.0, AussenBearbeitung.HuegelWerte(a, H(0)).Laenge);
    }

    [Fact]
    public void Pinsel_HebtSenktGlaettet_JederZugEinSchritt()
    {
        var a = Grundlage(huegel: false);
        var zug = new List<Vector2> { new(10, 12), new(12, 12) };
        Assert.True(AussenBearbeitung.Pinselzug(a, PinselArt.Heben, zug, 1) > 0);
        var g = AussenBearbeitung.GelaendeVon(a)!;
        var mitte = g.Hoehe(11, 12);
        Assert.True(mitte > 0.05f, $"Hoehe nach Heben {mitte}");
        // jede Stelle nur einmal je Zug: ein Zug ueber dieselbe Stelle hin und zurueck hebt nicht doppelt
        var b = Grundlage(huegel: false);
        AussenBearbeitung.Pinselzug(b, PinselArt.Heben, [new(11, 12), new(11.5f, 12), new(11, 12)], 1);
        Assert.Equal(AussenBearbeitung.PinselStaerke, AussenBearbeitung.GelaendeVon(b)!.Hoehe(11, 12), 3);
        // Senken bis 0 nimmt den Abschnitt wieder weg
        for (var i = 0; i < 20; i++)
        {
            AussenBearbeitung.Pinselzug(a, PinselArt.Senken, zug, 2);
        }
        Assert.False(Py.Hat(Aussen.Stadtdaten(a), Gelaende.Abschnitt));
        // Glaetten nimmt die Spitze
        var c = Grundlage(huegel: false);
        for (var i = 0; i < 8; i++)
        {
            AussenBearbeitung.Pinselzug(c, PinselArt.Heben, [new(11, 12)], 0.5f);
        }
        var vorher = AussenBearbeitung.GelaendeVon(c)!.Hoehe(11, 12);
        Assert.True(AussenBearbeitung.Pinselzug(c, PinselArt.Glaetten, [new(11, 12)], 1.5f) > 0);
        Assert.True(AussenBearbeitung.GelaendeVon(c)!.Hoehe(11, 12) < vorher);
        // nie hoeher als 1,5
        for (var i = 0; i < 30; i++)
        {
            AussenBearbeitung.Pinselzug(c, PinselArt.Heben, [new(11, 12)], 0.5f);
        }
        Assert.True(AussenBearbeitung.GelaendeVon(c)!.Hoehe(11, 12) <= Gelaende.HoeheMax + 1e-4f);
    }

    [Fact]
    public void Projekt_PinselzugIstEinSchritt_Rueckgaengig()
    {
        var p = Projekt.Neu("Gelaende", D, Daten.NeueAblage(), null, (30, 20));
        Assert.True(p.AussenAendern("Pinsel", a => AussenBearbeitung.Pinselzug(a, PinselArt.Heben, [new(10, 10), new(12, 10)], 1) > 0));
        Assert.True(Py.Hat(Aussen.Stadtdaten(p.AussenStand), Gelaende.Abschnitt));
        Assert.True(p.Rueckgaengig());
        Assert.False(Py.Hat(Aussen.Stadtdaten(p.AussenStand), Gelaende.Abschnitt));
    }

    // ---- Spiel ---------------------------------------------------------------------------------------

    [Fact]
    public void Hoehe_IstDerGroessteWert_AusKuppeUndGemaltem_HoechstensEinsFuenf()
    {
        var a = Grundlage();
        var kuppe = AussenBearbeitung.GelaendeVon(a)!.Hoehe(15, 13);
        Assert.Equal(AussenBearbeitung.HuegelHoeheNeu, kuppe, 3);
        var g = Objekt(Aussen.Stadtdaten(a)[Gelaende.Abschnitt]);
        g.Add("gemalt", new List<object?> { new List<object?> { 30L, 26L, 40L }, new List<object?> { 30L, 20L, 150L } });
        var gel = AussenBearbeitung.GelaendeVon(a)!;
        Assert.Equal(AussenBearbeitung.HuegelHoeheNeu, gel.Hoehe(15, 13), 3); // 0,4 gemalt < 0,8 Kuppe
        Assert.Equal(1.5f, gel.Hoehe(15, 10), 3);
        Assert.True(gel.Hoehe(15, 10) <= Gelaende.HoeheMax);
    }

    [Fact]
    public void OhneGelaende_SzeneGleich_Gegenprobe()
    {
        var ohne = Grundlage(huegel: false);
        var mit = Grundlage();
        Assert.Equal(StadtSzeneHash.Stadt(Szene(ohne)), StadtSzeneHash.Stadt(SzeneOhneGelaende(mit)));
        // Gegenprobe: mit Huegel aendert sich das Netz der Stadt
        Assert.NotEqual(StadtSzeneHash.Stadt(Szene(ohne)), StadtSzeneHash.Stadt(Szene(mit)));
    }

    private static StadtSzene SzeneOhneGelaende(PyObjekt a)
    {
        Gelaende.Weglassen = true;
        try
        {
            return Szene(a);
        }
        finally
        {
            Gelaende.Weglassen = false;
        }
    }

    [Fact]
    public void Fassung0_OhneGelaende_StadtUnveraendert()
    {
        var a = Aussen.Aus(D.Fassung);
        Assert.Null(AussenBearbeitung.GelaendeVon(a));
        Assert.Equal(StadtSzeneHash.Stadt(SzeneOhneGelaende(a)), StadtSzeneHash.Stadt(Szene(a)));
    }

    [Fact]
    public void SanfterHuegel_LaufrasterOrteUndWegeGleich_SteilerGesperrt_Gegenprobe()
    {
        var a = Grundlage();
        AussenBearbeitung.HuegelWerteSetzen(a, H(0), 3, 0.5, false, 0);
        var mit = Szene(a);
        var ohne = SzeneOhneGelaende(a);
        Assert.Equal(Raster(ohne), Raster(mit));
        Assert.Equal(ohne.Orte.Select(o => (o.Id, o.StehX, o.StehZ, o.TuerX, o.TuerZ)), mit.Orte.Select(o => (o.Id, o.StehX, o.StehZ, o.TuerX, o.TuerZ)));
        // Gegenprobe: ein steiler Huegel (Radius 1, Hoehe 1,5) sperrt Zellen am Hang
        AussenBearbeitung.HuegelWerteSetzen(a, H(0), 1, 1.5, false, 0);
        var steil = Szene(a);
        Assert.NotEqual(Raster(ohne), Raster(steil));
        Assert.False(steil.Laufraster.KannStehen(15.5, 13));
        Assert.True(steil.Gelaende!.ZuSteil(15.5f, 13));
    }

    private static string Raster(StadtSzene s)
    {
        var t = new System.Text.StringBuilder();
        for (var x = 0.125; x < 30; x += 0.25)
        {
            for (var z = 0.125; z < 20; z += 0.25)
            {
                t.Append(s.Laufraster.KannStehen(x, z) ? '1' : '0');
            }
        }
        return t.ToString();
    }

    [Fact]
    public void Kleingruen_FussFlach_UndAufDerHoeheDerMitte_Gegenprobe()
    {
        // Hochebene 0,5 von (8; 8) bis (20; 16) mit Hang 0,5 breit: oben flach, am Rand schief
        var a = Grundlage(huegel: false);
        var gemalt = new List<object?>();
        for (var i = 16; i <= 40; i++)
        {
            for (var j = 16; j <= 32; j++)
            {
                gemalt.Add(new List<object?> { (long)i, (long)j, 50L });
            }
        }
        Abschnitt(a, new PyObjekt { { "gemalt", gemalt } });
        // die leere Stadt des Werkzeugs ist ganz frei (kleingruen = false); hier mit Kleingruen
        Py.Setzen(Objekt(Aussen.Stadtdaten(a)["darstellung"]), "kleingruen", true);
        var szene = Szene(a);
        var g = szene.Gelaende!;
        var aufHuegel = szene.GruenKlein!.Stuecke.Where(s => g.Hoehe(s.Lage.M41, s.Lage.M43) > 0).ToList();
        Assert.NotEmpty(aufHuegel);
        Assert.All(aufHuegel, s =>
        {
            Assert.True(g.Flach(s.Lage.M41, s.Lage.M43, 0.1f), $"{s.Name} bei {s.Lage.M41}; {s.Lage.M43} steht schief");
            Assert.True(s.Lage.M42 >= g.Boden(s.Lage.M41, s.Lage.M43) - 1e-4f, $"{s.Name} steckt im Hang");
        });
        // Gegenprobe: ohne die Regel „Fuss flach“ steht Kleingruen auch am Hang (Fuss mehr als 0,02 daneben)
        Gelaende.FlachAltFuerTests = true;
        StadtSzene alt;
        try
        {
            alt = Szene(a);
        }
        finally
        {
            Gelaende.FlachAltFuerTests = false;
        }
        Assert.Contains(alt.GruenKlein!.Stuecke, s => g.Hoehe(s.Lage.M41, s.Lage.M43) > 0 && !g.Flach(s.Lage.M41, s.Lage.M43, 0.1f));
    }

    [Fact]
    public void Baum_UndKatalogStueck_StehenAufDerHoeheIhrerMitte()
    {
        var a = Grundlage();
        AussenBearbeitung.HuegelWerteSetzen(a, H(0), 3, 1.0, false, 0);
        AussenBearbeitung.Setzen(a, AussenArt.Baum, 15, 13);
        var szene = Szene(a);
        var boden = szene.Gelaende!.Boden(15, 13);
        Assert.True(boden > 0.9f);
        Assert.Contains(szene.Stuecke, s => MathF.Abs(s.Lage.M41 - 15) < 0.01f && MathF.Abs(s.Lage.M43 - 13) < 0.01f && MathF.Abs(s.Lage.M42 - boden) < 0.05f);
        // Gegenprobe: ohne Gelaende steht derselbe Baum am Boden
        Assert.Contains(SzeneOhneGelaende(a).Stuecke, s => MathF.Abs(s.Lage.M41 - 15) < 0.01f && MathF.Abs(s.Lage.M43 - 13) < 0.01f && s.Lage.M42 < 0.05f);
    }

    // ---- Pruefregeln ---------------------------------------------------------------------------------

    [Fact]
    public void Grundlage_Gruen()
    {
        var f = Pruefen(Grundlage());
        Assert.True(!f.Any(x => x.Rot && (x.Text.Contains("Hügel", StringComparison.Ordinal) || x.Text.Contains("Gelände", StringComparison.Ordinal))), Text(f));
    }

    [Fact]
    public void Regel1_Daten_Rot()
    {
        var a = Grundlage(huegel: false);
        Abschnitt(a, new PyObjekt { { "huegel", new List<object?> { HuegelDaten(15, 13, 5, 1) } } });
        Rot(a, "Hügel");
        var b = Grundlage(huegel: false);
        Abschnitt(b, new PyObjekt { { "huegel", new List<object?> { HuegelDaten(15, 13, 2, 1) } } });
        Gruen(b, "Hügel");
    }

    [Fact]
    public void Regel2_UeberDenRand_Rot()
    {
        var a = Grundlage(huegel: false);
        Abschnitt(a, new PyObjekt { { "huegel", new List<object?> { HuegelDaten(29, 13, 2, 1) } } });
        Rot(a, "über den Rand");
        Gruen(Grundlage(), "über den Rand");
    }

    [Fact]
    public void Regel3_MehrAlsSechzehn_Rot()
    {
        var a = Grundlage(huegel: false);
        var liste = new List<object?>();
        for (var i = 0; i < Gelaende.HuegelHoechstens + 1; i++)
        {
            liste.Add(HuegelDaten(4 + (i % 9 * 2.5), 9 + (i / 9 * 5), 1, 0.3));
        }
        Abschnitt(a, new PyObjekt { { "huegel", liste } });
        Rot(a, "höchstens " + Gelaende.HuegelHoechstens);
        liste.RemoveAt(liste.Count - 1);
        Gruen(a, "höchstens " + Gelaende.HuegelHoechstens);
    }

    [Fact]
    public void Regel4_SaumAnDerStrasse_Rot_UndWeitGenug_Gruen()
    {
        var a = Grundlage();
        AussenBearbeitung.Verschieben(a, H(0), 0, -6.5); // Mitte 6,5: Rand bei 4,5, Strasse mit Gehweg bis 3,71
        Rot(a, "einer Straße");
        AussenBearbeitung.Verschieben(a, H(0), 0, 2);
        Gruen(a, "einer Straße");
    }

    [Fact]
    public void Regel5_LaterneAufDemHuegel_Rot()
    {
        var a = Grundlage();
        Aussen.Laternen(a).Add(new List<object?> { 15.0, 13.0, false });
        Rot(a, "Laterne steht auf einem Hügel");
        Gruen(Grundlage(), "Laterne steht auf einem Hügel");
    }

    [Fact]
    public void Regel6_BaumAmSteilenHang_Rot_AufDerKuppe_Gruen()
    {
        var a = Grundlage();
        AussenBearbeitung.HuegelWerteSetzen(a, H(0), 2, 1.5, false, 0);
        AussenBearbeitung.Setzen(a, AussenArt.Baum, 16, 13);
        Rot(a, "Baum steht zu schief");
        var b = Grundlage();
        AussenBearbeitung.HuegelWerteSetzen(b, H(0), 3, 0.5, false, 0);
        AussenBearbeitung.Setzen(b, AussenArt.Baum, 15, 13);
        Gruen(b, "Baum steht zu schief");
    }

    [Fact]
    public void Regel7_OrtNurWegenSteilemHangUnerreichbar_Rot_Gegenprobe()
    {
        var a = Aussen.Aus(D.Fassung);
        var szene = Szene(a);
        var ziel = szene.Orte.First(o => o.Id == "bitweiche");
        // Ring aus gemalten Hoehen 1,5 um die Stelle vor der Tuer: innen flach, aussen herum steil
        var gemalt = new List<object?>();
        for (var i = (int)((ziel.StehX - 2) / Gelaende.Raster); i <= (int)((ziel.StehX + 2) / Gelaende.Raster); i++)
        {
            for (var j = (int)((ziel.StehZ - 2) / Gelaende.Raster); j <= (int)((ziel.StehZ + 2) / Gelaende.Raster); j++)
            {
                var d = Vector2.Distance(new Vector2(i * Gelaende.Raster, j * Gelaende.Raster), new Vector2(ziel.StehX, ziel.StehZ));
                if (d is > 0.9f and < 1.6f)
                {
                    gemalt.Add(new List<object?> { (long)i, (long)j, 150L });
                }
            }
        }
        Abschnitt(a, new PyObjekt { { "gemalt", gemalt } });
        Rot(a, "wegen eines zu steilen Hangs zu Fuß nicht erreichbar");
        // Gegenprobe: ohne den Ring ist der Ort erreichbar (kein solcher Fund)
        Gruen(Aussen.Aus(D.Fassung), "wegen eines zu steilen Hangs");
    }

    [Fact]
    public void Nahansicht_MitSechzehnHuegeln_UnterDerGrenze_GelaendeZaehltMit()
    {
        var a = Aussen.Aus(D.Fassung);
        var liste = new List<object?>();
        for (var i = 0; i < Gelaende.HuegelHoechstens; i++)
        {
            liste.Add(HuegelDaten(5 + (i % 4 * 7), 5 + (i / 4 * 7), Gelaende.RadiusMax, Gelaende.HoeheMax));
        }
        Abschnitt(a, new PyObjekt { { "huegel", liste } });
        var mit = GrenzenLive.Fuer(F(a), D);
        Assert.False(mit.Ueber, mit.ToString());
        Assert.True(AussenBearbeitung.GelaendeVon(a)!.Dreiecke(40, 30) <= Gelaende.DreieckeHoechstens);
        // Gegenprobe: ohne Gelaende zaehlt die Gesamtansicht weniger Dreiecke (das Gelaende ist mitgezaehlt)
        var ohne = GrenzenLive.Fuer(D.Fassung, D);
        Assert.True(mit.Gesamt.Dreiecke > ohne.Gesamt.Dreiecke, mit + " gegen " + ohne);
        // kein neuer Zeichenaufruf: das Gelaende liegt im Netz der Stadt
        Assert.True(mit.Gesamt.Aufrufe <= ohne.Gesamt.Aufrufe && mit.Nah.Aufrufe <= ohne.Nah.Aufrufe, mit + " gegen " + ohne);
    }

    [Fact]
    public void Fassung0_FreieWiese_MitRandEinsFuenf()
    {
        var szene = Szene(Aussen.Aus(D.Fassung));
        var (frei, groesster, stelle) = StadtbauPruefung.GelaendePlatz(szene, D.Fassung);
        // gemessen mit A 0.66.0: 23 Felder, groesster Radius 2,875 bei (21,625; 2,875)
        Assert.True(frei > 0 && groesster >= Gelaende.RadiusMin, $"frei {frei}, groesster {groesster} bei {stelle}");
        // Gegenprobe: ein Huegel mit dem groessten Radius an dieser Stelle ist frei vom Rand-Fund, einer mit 0,5 mehr nicht
        var a = Aussen.Aus(D.Fassung);
        Abschnitt(a, new PyObjekt { { "huegel", new List<object?> { HuegelDaten(stelle.X, stelle.Y, Math.Floor(groesster * 10) / 10, 0.5) } } });
        Gruen(a, "liegt näher als");
        if (groesster + 0.5 <= Gelaende.RadiusMax)
        {
            Abschnitt(a, new PyObjekt { { "huegel", new List<object?> { HuegelDaten(stelle.X, stelle.Y, Math.Floor(groesster * 10) / 10 + 0.5, 0.5) } } });
            Rot(a, "liegt näher als");
        }
    }

    [Fact]
    public void Tippen_TrifftDenHang_NichtDenBodenDarunter_Gegenprobe()
    {
        var a = Grundlage();
        AussenBearbeitung.HuegelWerteSetzen(a, H(0), 3, 1.5, false, 0);
        var g = AussenBearbeitung.GelaendeVon(a)!;
        var kamera = new Kamera(15, 13, 10) { Neigung = 40f };
        // Bildpunkt eines Punkts auf dem Hang zurueck in die Welt: mit Gelaende genau dort, ohne (flacher Boden) weiter hinten
        var ziel = new Vector3(15, g.Boden(15, 14), 14);
        var (bx, by) = kamera.Bildpunkt(ziel, 1600, 900)!.Value;
        var mit = kamera.Boden(bx, by, 1600, 900, g.Boden)!.Value;
        Assert.True(Vector2.Distance(mit, new Vector2(15, 14)) < 0.05f, $"getroffen {mit}");
        var ohne = kamera.Boden(bx, by, 1600, 900)!.Value;
        Assert.True(Vector2.Distance(ohne, new Vector2(15, 14)) > 0.5f, $"ohne Gelaende {ohne}");
    }

    [Fact]
    public void Ladepruefung_KaputteDaten_Fund_GuteDaten_KeinFund()
    {
        var a = Grundlage(huegel: false);
        Abschnitt(a, new PyObjekt { { "huegel", new List<object?> { HuegelDaten(15, 13, 5, 1) } } });
        Assert.Contains(StadtbauPruefung.Verbaut(F(a), D.Grafik), f => f.StartsWith("Gelände: ", StringComparison.Ordinal));
        Assert.DoesNotContain(StadtbauPruefung.Verbaut(F(Grundlage()), D.Grafik), f => f.StartsWith("Gelände: ", StringComparison.Ordinal));
    }
}
