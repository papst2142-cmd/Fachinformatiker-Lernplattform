using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle A 0.54.0 / S 0.26.0 (Weisung P12-115, Meldung P12-75 Abschnitte 3 und 4, Sammelliste 254/255): Zebrastreifen im
/// Werkzeug (setzen, schieben nur laengs, mit der Strasse schieben und loeschen), im Spiel (Balken, Laufraster: Fahrbahn
/// einer Strasse mit Gehweg nur am Zebrastreifen begehbar, Strassen ohne Gehweg wie bisher), Autos (halten nur bei der Figur
/// auf der Fahrbahn oder auf dem Zebrastreifen) und die Pruefregeln je rot und gruen.
/// Grundlage: leere Stadt 24 × 16, gerade Strasse (2; 8)–(22; 8) mit Gehweg 0,35, Zebrastreifen bei (12; 8).
/// </summary>
public class ZebrastreifenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    private static PyObjekt Grundlage(bool zebra = true, double gehweg = 0.35)
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_zebra", 24, 16));
        var s = new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(2, 8), P(22, 8) } } };
        if (gehweg > 0)
        {
            s.Add("gehweg", gehweg);
        }
        Aussen.Landschaft(a).Add(s);
        if (zebra)
        {
            Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 12, 8.6));
        }
        return a;
    }

    /// <summary>Wo der Weg die Strassenmitte (y 8) kreuzt (x), null = gar nicht.</summary>
    private static double? Kreuzt(IReadOnlyList<(double X, double Y)> weg, (double X, double Y) start)
    {
        var a = start;
        foreach (var b in weg)
        {
            if ((a.Y - 8) * (b.Y - 8) <= 0 && a.Y != b.Y)
            {
                return a.X + ((b.X - a.X) * (8 - a.Y) / (b.Y - a.Y));
            }
            a = b;
        }
        return null;
    }

    private static List<object?> Zebras(PyObjekt a) => AussenBearbeitung.ZebraListe(a);

    private static (double X, double Y) Zebra(PyObjekt a, int i) => (Py.F(Objekt(Zebras(a)[i])["x"]), Py.F(Objekt(Zebras(a)[i])["y"]));

    private static List<StadtbauFund> Pruefen(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D.Inhalt, D.Grafik);

    private static string Text(List<StadtbauFund> f) => string.Join(" | ", f.Select(x => (x.Rot ? "rot: " : "gelb: ") + x.Text));

    private static void Gruen(PyObjekt a)
    {
        var f = Pruefen(a);
        Assert.True(!f.Any(x => x.Text.Contains("Zebrastreifen", StringComparison.Ordinal)), Text(f));
    }

    private static void Rot(PyObjekt a, string teil)
    {
        var f = Pruefen(a);
        Assert.True(f.Any(x => x.Rot && x.Text.Contains(teil, StringComparison.Ordinal)), "erwartet rot „" + teil + "“, gefunden: " + Text(f));
    }

    private static StadtSzene Szene(PyObjekt a) =>
        Ansicht3D.Bauen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D, Ansicht3D.Einstellung.Standard).Szene;

    // ---- Werkzeug ------------------------------------------------------------------------------------

    [Fact]
    public void Setzen_RastetQuerAufDieStrassenmitteEin()
    {
        var a = Grundlage();
        Assert.Equal((12d, 8d), Zebra(a, 0));
        // zu weit weg (ueber 0,95): nichts gesetzt
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 12, 9.2));
        Assert.Single(Zebras(a));
        // auf einer Strasse mit drei Punkten (Kurve) nicht
        var b = Grundlage(zebra: false);
        Aussen.Landschaft(b).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(2, 2), P(12, 4), P(22, 2) } }, { "gehweg", 0.35 } });
        Assert.Null(AussenBearbeitung.Setzen(b, AussenArt.Zebrastreifen, 12, 4));
    }

    [Fact]
    public void Schieben_NurLaengsDerStrasse_UndMitDerStrasse()
    {
        var a = Grundlage();
        var z = new AussenStelle(AussenArt.Zebrastreifen, 0);
        AussenBearbeitung.Verschieben(a, z, 2, 1.5);
        Assert.Equal((14d, 8d), Zebra(a, 0));
        // die Strasse verschoben: der Zebrastreifen wandert mit
        AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Strasse, 0), 0, 2);
        Assert.Equal((14d, 10d), Zebra(a, 0));
        // kopieren: laengs daneben
        var k = AussenBearbeitung.Kopieren(a, z)!;
        Assert.Equal((16.5d, 10d), Zebra(a, k.Nummer));
        Assert.NotNull(AussenBearbeitung.DrehenNicht(z));
        // die Strasse geloescht: ihre Zebrastreifen gehen mit, der Abschnitt faellt weg
        Assert.True(AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Strasse, 0)));
        Assert.False(Py.Hat(Aussen.Stadtdaten(a), Zebrastreifen.Abschnitt));
    }

    [Fact]
    public void StueckBei_FindetDenZebrastreifen()
    {
        var a = Grundlage();
        Assert.Equal(AussenArt.Zebrastreifen, AussenBearbeitung.StueckBei(a, 12.1, 8.3)!.Art);
        Assert.Equal(AussenArt.Strasse, AussenBearbeitung.StueckBei(a, 16, 8.1)!.Art);
    }

    // ---- Spiel ---------------------------------------------------------------------------------------

    [Fact]
    public void Spiel_Balken_OhneNeuenAufruf_UndKeinStrichDarauf()
    {
        var mit = Szene(Grundlage());
        var ohne = Szene(Grundlage(zebra: false));
        Assert.Single(mit.Zebras);
        // fuenf weisse Balken (je ein Band), ein Strich der Mittellinie auf dem Zebrastreifen faellt weg
        var weiss = mit.Teile.Count(t => t.Art == TeilArt.Band && t.Farbe == Vector3.One);
        Assert.Equal(Zebrastreifen.Balken, weiss);
        Assert.True(mit.Teile.Count - ohne.Teile.Count is >= 3 and <= Zebrastreifen.Balken);
    }

    [Fact]
    public void Laufraster_FahrbahnMitGehweg_NurAmZebrastreifen_Gegenprobe()
    {
        var szene = Szene(Grundlage());
        Assert.False(szene.Laufraster.KannStehen(6, 8), "Fahrbahn mit Gehweg ist begehbar");
        Assert.True(szene.Laufraster.KannStehen(6, 8.5), "Gehweg ist nicht begehbar");
        Assert.True(szene.Laufraster.KannStehen(12, 8), "Zebrastreifen ist nicht begehbar");
        // ueber die Strasse nur am Zebrastreifen: der Weg von Seite zu Seite fuehrt ueber ihn
        var weg = szene.Laufraster.Weg((10, 7.4), (10, 8.6));
        Assert.NotEmpty(weg);
        Assert.True(Kreuzt(weg, (10, 7.4)) is { } x && Math.Abs(x - 12) <= 0.3, "Weg kreuzt nicht am Zebrastreifen: " + string.Join(" ", weg));
        // ohne Zebrastreifen: nur um das Ende der Strasse herum (bei x 2), nie ueber die Fahrbahn
        var ohne = Szene(Grundlage(zebra: false)).Laufraster.Weg((10, 7.4), (10, 8.6));
        Assert.NotEmpty(ohne);
        Assert.True(ohne.Min(p => p.X) < 2, "Weg fuehrt ueber die Fahrbahn");
        // Strasse ohne Gehweg: Fahrbahn begehbar wie bisher
        Assert.True(Szene(Grundlage(zebra: false, gehweg: 0)).Laufraster.KannStehen(6, 8));
        // Gegenprobe: Laufraster wie bis A 0.53.2
        StadtSzene.FahrbahnFreiAltFuerTests = true;
        try
        {
            Assert.True(Szene(Grundlage(zebra: false)).Laufraster.KannStehen(6, 8));
        }
        finally
        {
            StadtSzene.FahrbahnFreiAltFuerTests = false;
        }
    }

    [Fact]
    public void Fassung0_LaufrasterGleichWieBisher()
    {
        var a = Aussen.Aus(D.Fassung);
        var neu = Szene(a).Laufraster;
        StadtSzene.FahrbahnFreiAltFuerTests = true;
        try
        {
            var alt = Szene(a).Laufraster;
            for (var x = 0.125; x < 34; x += 0.25)
            {
                for (var z = 0.125; z < 24; z += 0.25)
                {
                    Assert.Equal(alt.KannStehen(x, z), neu.KannStehen(x, z));
                }
            }
        }
        finally
        {
            StadtSzene.FahrbahnFreiAltFuerTests = false;
        }
    }

    // ---- Autos ---------------------------------------------------------------------------------------

    private static Verkehr EinAuto(StadtSzene szene, float startX)
    {
        var strassen = szene.Strassen;
        var v = new Verkehr { FuerStrassen = strassen };
        v.Zebras.AddRange(szene.Zebras);
        // Auto faehrt nach Osten auf der rechten Spur (y 8 + Spur nach Sueden ist die rechte Seite bei Blick nach +x)
        var spur = Kurve.Versetzt([new Vector2(0, 8), new Vector2(24, 8)], Verkehr.Spur);
        v.Autos.Add(new Verkehr.Auto("car-taxi", spur, startX));
        return v;
    }

    [Fact]
    public void Auto_HaeltNurBeiFigurAufDerFahrbahn_Gegenprobe()
    {
        var szene = Szene(Grundlage(zebra: false));
        var v = EinAuto(szene, 4);
        var (ort, richtung) = v.Autos[0].Lage(4 + 0.3f);
        var quer = new Vector2(-richtung.Y, richtung.X);
        var mitteQuer = Vector2.Dot(new Vector2(0, 8) - ort, quer);
        Vector2 Figur(float vonMitte) => ort + (richtung * 0.5f) + (quer * (mitteQuer + vonMitte * MathF.Sign(-mitteQuer)));
        // Figur auf dem Gehweg der eigenen Seite (0,40 und 0,50 von der Mitte): das Auto faehrt
        foreach (var q in (float[])[0.4f, 0.5f])
        {
            var f = Figur(q);
            v.Weiter(0.1, f.X, f.Y);
            Assert.False(v.Autos[0].Haelt, $"haelt bei Figur {q} von der Mitte");
        }
        // auf der Fahrbahn (0,30): es haelt
        var g = Figur(0.3f);
        v.Weiter(0.1, g.X, g.Y);
        Assert.True(v.Autos[0].Haelt);
        // Gegenprobe: Regel bis A 0.53.2 haelt auch bei der Figur auf dem Gehweg
        Verkehr.AnhaltenAltFuerTests = true;
        try
        {
            var f = Figur(0.4f);
            v.Weiter(0.1, f.X, f.Y);
            Assert.True(v.Autos[0].Haelt);
        }
        finally
        {
            Verkehr.AnhaltenAltFuerTests = false;
        }
    }

    [Fact]
    public void Auto_HaeltVorDemZebrastreifen_SolangeDieFigurDraufSteht()
    {
        var szene = Szene(Grundlage());
        var v = EinAuto(szene, 11.4f);
        // Figur auf dem Zebrastreifen (auf beiden Seiten der Fahrbahn, 0,3 von der Mitte): das Auto 0,6 davor haelt
        var weit = new Vector2(12, 7.7f);
        var gegen = new Vector2(12, 8.3f);
        var halt = false;
        foreach (var f in (Vector2[])[weit, gegen])
        {
            var vv = EinAuto(szene, 11.4f);
            vv.Weiter(0.1, f.X, f.Y);
            halt |= vv.Autos[0].Haelt;
            Assert.True(vv.Autos[0].Haelt, $"faehrt bei Figur auf dem Zebrastreifen {f}");
        }
        Assert.True(halt);
        // Figur neben dem Zebrastreifen auf dem Gehweg: das Auto faehrt
        v.Weiter(0.1, 12, 8.55f);
        Assert.False(v.Autos[0].Haelt);
        // Gegenprobe: ohne Zebrastreifen-Regel (alte Regel) haelt es fuer die Figur auf der Gegenspur nicht
        var w = new Verkehr { FuerStrassen = szene.Strassen };
        var spur = Kurve.Versetzt([new Vector2(0, 8), new Vector2(24, 8)], Verkehr.Spur);
        w.Autos.Add(new Verkehr.Auto("car-taxi", spur, 11.4f));
        var aussenSeite = spur[0].Y > 8 ? new Vector2(12, 7.7f) : new Vector2(12, 8.3f);
        w.Weiter(0.1, aussenSeite.X, aussenSeite.Y);
        Assert.False(w.Autos[0].Haelt);
    }

    // ---- Pruefregeln ---------------------------------------------------------------------------------

    [Fact]
    public void Pruefung_Grundlage_Gruen() => Gruen(Grundlage());

    [Fact]
    public void Regel1_FalschGespeichert_Rot()
    {
        var a = Grundlage();
        Py.Setzen(Objekt(Zebras(a)[0]), "x", "zwölf");
        Rot(a, "Zebrastreifen Nr. 1 ist falsch gespeichert");
    }

    [Fact]
    public void Regel2_AusserhalbDerKarte_Rot()
    {
        var a = Grundlage();
        Zebras(a).Add(new PyObjekt { { "x", 30.0 }, { "y", 8.0 } });
        Rot(a, "Zebrastreifen Nr. 2 liegt außerhalb der Karte");
    }

    [Fact]
    public void Regel3_NichtAufStrasse_UndInKurve_Rot()
    {
        var a = Grundlage();
        Zebras(a).Add(new PyObjekt { { "x", 12.0 }, { "y", 11.0 } });
        Rot(a, "Zebrastreifen Nr. 2 liegt nicht quer auf einer geraden Straße");
        var b = Grundlage();
        Aussen.Landschaft(b).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(2, 2), P(12, 4), P(22, 2) } }, { "gehweg", 0.35 } });
        Zebras(b).Add(new PyObjekt { { "x", 12.0 }, { "y", 4.0 } });
        Rot(b, "Zebrastreifen Nr. 2 liegt in einer Kurve");
    }

    [Fact]
    public void Regel4_StrasseOhneGehweg_Rot()
    {
        var a = Grundlage(zebra: false, gehweg: 0);
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 12, 8));
        Rot(a, "liegt auf einer Straße ohne Gehweg");
    }

    [Fact]
    public void Regel5_NahAmKreisverkehr_Rot_UndWeitGenug_Gruen()
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_zebra", 24, 16));
        AussenBearbeitung.Setzen(a, AussenArt.Kreisverkehr, 12, 8, modell: AussenBearbeitung.Kennung(2));
        // Zufahrt nach Osten: Ring bei x 14, Fahrbahnrand 14,36
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 15, 8));
        Rot(a, "liegt zu nah am Kreisverkehr");
        Zebras(a).Clear();
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 16, 8));
        Gruen(a);
    }

    [Fact]
    public void Regel6_NahAnKreuzung_Rot()
    {
        var a = Grundlage(zebra: false);
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { P(12, 2), P(12, 8) } }, { "gehweg", 0.35 } });
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 12.8, 8));
        Rot(a, "liegt zu nah an einer Kreuzung");
        Zebras(a).Clear();
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 13.2, 8));
        Gruen(a);
    }

    [Fact]
    public void Regel7_NahAmBahnuebergang_Rot()
    {
        var a = Grundlage(zebra: false);
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "bahn" }, { "punkte", new List<object?> { P(15, 0), P(15, 16) } } });
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 13.5, 8));
        Rot(a, "liegt zu nah am Bahnübergang");
        Zebras(a).Clear();
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 12.5, 8));
        Gruen(a);
    }

    [Fact]
    public void Regel8_ZweiZuNah_Rot()
    {
        var a = Grundlage();
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 13.5, 8));
        Rot(a, "Zebrastreifen Nr. 2 liegt zu nah an Zebrastreifen Nr. 1");
        Zebras(a).RemoveAt(1);
        Assert.NotNull(AussenBearbeitung.Setzen(a, AussenArt.Zebrastreifen, 14.5, 8));
        Gruen(a);
    }

    [Fact]
    public void Regel9_LaterneOderStueckDarauf_Rot()
    {
        var a = Grundlage();
        Aussen.Laternen(a).Add(new List<object?> { 12.1, 8.5, false });
        Rot(a, "Auf Zebrastreifen Nr. 1 steht Laterne Nr. 1");
        var b = Grundlage();
        Py.Setzen(Aussen.Stadtdaten(b), "katalog", new List<object?>
        {
            new PyObjekt { { "id", "city-kit-roads/bench" }, { "x", 12.0 }, { "y", 8.6 }, { "w", 0.3 }, { "t", 0.2 }, { "dreh", 0L } },
        });
        Rot(b, "Auf Zebrastreifen Nr. 1 steht");
        // Laterne neben dem Zebrastreifen: gruen
        var c = Grundlage();
        Aussen.Laternen(c).Add(new List<object?> { 13.0, 8.5, false });
        Gruen(c);
    }

    [Fact]
    public void Erreichbar_MitGehweg_NenntZebrastreifen()
    {
        // Fassung 0 hat keine Strasse mit Gehweg: der Hinweis bleibt wie bisher
        var a = Aussen.Aus(D.Fassung);
        Assert.False(Szene(a).MitGehweg);
        Assert.True(Szene(Grundlage()).MitGehweg);
    }
}
