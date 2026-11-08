using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle Z (A 0.48.0 / S 0.14.0, Weisung P12-71, Nico P12-67 Frage 3 = a und 4 = a): Ziehen von Punkt zu Punkt fuer
/// Strasse, Fussweg und Fluss. Bögen in Schritten von 7,5 Grad mit den heutigen Radien, Vorschau gleich Ergebnis, ein
/// Schritt im Verlauf; passt nichts, aendert sich nichts. Je Fall mit Gegenprobe.
/// </summary>
public class ZiehenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static PyObjekt Leer() => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_ziehen", 32, 24));

    private static List<StadtbauFund> Funde(PyObjekt a) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D.Inhalt, D.Grafik);

    private static List<Vector2> Linie(PyObjekt a, int nr) =>
        [.. Liste(Objekt(Aussen.Landschaft(a)[nr])["punkte"]).Select(p => new Vector2((float)Py.F(Liste(p)[0]), (float)Py.F(Liste(p)[1])))];

    private static List<PyObjekt> Strassen(PyObjekt a) => [.. Aussen.Landschaft(a).OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "strasse"))];

    private static StadtSzene Szene(PyObjekt a)
    {
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        return StadtSzene.Bauen(f.StadtFuer(f.MitKarte(D.Inhalt)), null, D.Grafik);
    }

    private static readonly double[] Radien = [3, 2, 1];

    // ---- Verbindung (ohne Oberflaeche) ------------------------------------------------------------------

    [Fact]
    public void Verbindung_BeideFrei_IstEineGerade()
    {
        var plan = Verbindung.Rechnen(new Vector2(2, 2), null, new Vector2(9, 5), null, Radien, true);
        Assert.True(plan.Gefunden);
        Assert.Empty(plan.Ecken);
        Assert.Equal(Texte.ZiehenZuKurz, Verbindung.Rechnen(new Vector2(2, 2), null, new Vector2(2.1f, 2), null, Radien, true).Grund);
    }

    [Theory]
    [InlineData(10, 4)]
    [InlineData(10, -4)]
    [InlineData(6, 6)]
    [InlineData(3, 8)]
    [InlineData(12, 1.5)]
    public void Verbindung_EinEnde_BoegenIn7_5GradAb15_HeutigeRadien(double x, double y)
    {
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2((float)x, (float)y);
        var plan = Verbindung.Rechnen(p0, Vector2.UnitX, p1, null, Radien, true);
        Assert.True(plan.Gefunden, plan.Grund);
        Assert.NotEmpty(plan.Ecken);
        foreach (var e in plan.Ecken)
        {
            Assert.Equal(0, Math.Abs(e.Grad) % Strassenkurve.SchrittGrad, 6);
            Assert.True(Math.Abs(e.Grad) >= Strassenkurve.ZugMindestens - 1e-6, $"Bogen {e.Grad} Grad");
            Assert.Contains(e.Radius, Radien);
        }
        var stuecke = Verbindung.Stuecke(plan);
        var alle = stuecke.SelectMany(s => s.Punkte).ToList();
        Assert.Equal(p0, alle[0]);
        Assert.True(Vector2.Distance(p1, alle[^1]) < 0.01f, $"Ende {alle[^1]} statt {p1}");
        // jedes Bogenstueck ist eine erlaubte Kurve des Spiels (Feld kurve ist gueltig)
        foreach (var (_, grad, radius) in stuecke.Where(s => s.Grad != 0))
        {
            Assert.True(Strassenkurve.ZugBogen(Strassenkurve.Bogen, Math.Abs(grad), radius), $"{grad} Grad, Radius {radius}");
        }
        // Gegenprobe: ohne Stufen (Fussweg/Fluss) ist die Drehung nicht an 7,5 Grad gebunden
        var frei = Verbindung.Rechnen(p0, Vector2.UnitX, p1, null, Radien, false);
        Assert.True(frei.Gefunden);
    }

    [Fact]
    public void Verbindung_ZweiEnden_SKurve_UndRichtungPasstNicht()
    {
        // Anfang nach rechts, Ende kommt von links herein (w1 zeigt aus der Verbindung heraus nach rechts)
        var plan = Verbindung.Rechnen(new Vector2(0, 0), Vector2.UnitX, new Vector2(12, 4), -Vector2.UnitX, Radien, true);
        Assert.True(plan.Gefunden, plan.Grund);
        Assert.Equal(2, plan.Ecken.Count);
        Assert.True(Math.Sign(plan.Ecken[0].Grad) == -Math.Sign(plan.Ecken[1].Grad), "S-Kurve: gegenlaeufig");
        // 10 Grad zwischen den Richtungen passen nicht auf 7,5-Grad-Stufen (Strasse), frei schon
        var schief = Verbindung.Gedreht(-Vector2.UnitX, 10);
        Assert.False(Verbindung.Rechnen(new Vector2(0, 0), Vector2.UnitX, new Vector2(12, 4), schief, Radien, true).Gefunden);
        Assert.True(Verbindung.Rechnen(new Vector2(0, 0), Vector2.UnitX, new Vector2(12, 4), schief, Radien, false).Gefunden);
    }

    [Fact]
    public void Verbindung_EndeHinterDemAnfang_KeinWeg()
    {
        var plan = Verbindung.Rechnen(new Vector2(10, 10), Vector2.UnitX, new Vector2(9.5f, 10), null, Radien, true);
        Assert.False(plan.Gefunden);
        Assert.NotNull(plan.Grund);
    }

    // ---- Strasse ziehen ---------------------------------------------------------------------------------

    [Fact]
    public void Strasse_Frei_EineGeradeMitGehweg()
    {
        var a = Leer();
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 4, 4, 14, 4, true, out var grund);
        Assert.Null(grund);
        Assert.NotNull(s);
        var strassen = Strassen(a);
        Assert.Single(strassen);
        Assert.Equal([new Vector2(4, 4), new Vector2(14, 4)], Linie(a, s!.Nummer));
        Assert.Equal(AussenBearbeitung.NeuerGehweg, Py.F(strassen[0]["gehweg"]), 6);
    }

    [Fact]
    public void Strasse_VomEnde_AbbiegenMitBoegen_SchliesstImSpielAn()
    {
        var a = Leer();
        AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 4, 4, 12, 4, true, out _);
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 12.3, 4.2, 20, 12, true, out var grund);
        Assert.Null(grund);
        Assert.NotNull(s);
        var strassen = Strassen(a);
        Assert.Contains(strassen, Strassenkurve.IstKurve);
        foreach (var k in strassen.Where(Strassenkurve.IstKurve))
        {
            var kurve = Objekt(k[Strassenkurve.Feld]);
            Assert.True(Strassenkurve.ZugBogen((string)kurve["form"]!, Py.F(kurve["winkel"]), Py.F(kurve["radius"])));
            // ganze Winkel als ganze Zahl wie bei den festen Kurven (json.dumps 45, nicht 45.0), halbe als Kommazahl
            var w = Py.F(kurve["winkel"]);
            Assert.True(w % 1 == 0 ? kurve["winkel"] is long : kurve["winkel"] is double, $"winkel {kurve["winkel"]?.GetType().Name} {w}");
        }
        // alle Stuecke liegen in EINER Kette der Mittellinie (Anschluss wie im Spiel), keine roten Funde
        var linien = AussenBearbeitung.Strassen(a);
        Assert.Contains(StadtSzene.MittellinienZuege(linien), z => z.Strassen.Count == linien.Count);
        Assert.DoesNotContain(Funde(a), f => f.Rot);
        // Gegenprobe: mit „Einrasten“ aus rastet der Anfang nicht am Ende ein, die Bögen fehlen
        var b = Leer();
        AussenBearbeitung.Ziehen(b, ZiehArt.Strasse, 4, 4, 12, 4, true, out _);
        AussenBearbeitung.Ziehen(b, ZiehArt.Strasse, 12.3, 4.2, 20, 12, false, out _);
        Assert.DoesNotContain(Strassen(b), Strassenkurve.IstKurve);
    }

    [Fact]
    public void Strasse_GeradeVerlaengerung_BleibtEinStueck()
    {
        var a = Leer();
        AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 4, 4, 10, 4, true, out _);
        AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 10, 4, 16, 4, true, out _);
        var strassen = Strassen(a);
        Assert.Single(strassen);
        Assert.Equal([new Vector2(4, 4), new Vector2(16, 4)], Linie(a, 0));
    }

    [Fact]
    public void Strasse_KeinWeg_AendertNichts()
    {
        var a = Leer();
        AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 4, 4, 12, 4, true, out _);
        var vorher = PythonJson.Standard(a);
        // vom Ende nach rechts ein Ziel knapp hinter dem Anfang: so eng dreht kein Bogen
        Assert.Null(AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 12, 4, 11.5, 4.5, true, out var grund));
        Assert.NotNull(grund);
        Assert.Equal(vorher, PythonJson.Standard(a));
    }

    // ---- Verlauf und Vorschau ---------------------------------------------------------------------------

    [Theory]
    [InlineData(ZiehArt.Strasse)]
    [InlineData(ZiehArt.Fussweg)]
    [InlineData(ZiehArt.Fluss)]
    [InlineData(ZiehArt.Bahn)]
    public void EinSchrittImVerlauf_UndVorschauGleichErgebnis(ZiehArt art)
    {
        var p = Projekt.Neu("Ziehen " + art, D, Daten.NeueAblage(), leer: (32, 24));
        p.AussenAendern("Straße", a => AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 4, 4, 12, 4, true, out _));
        var (ax, ay, bx, by) = art switch
        {
            ZiehArt.Strasse => (12.0, 4.0, 22.0, 14.0),
            ZiehArt.Fussweg => (8.0, 4.6, 14.0, 12.0),
            _ => (0.2, 18.0, 20.0, 23.8),
        };
        var vorschau = Bauen3D.Ziehen(p.AussenStand, art, ax, ay, bx, by, true);
        Assert.NotNull(vorschau.Stelle);
        Assert.NotEmpty(vorschau.Umriss);
        var schritte = p.SchritteZurueck;
        p.AussenAendern(AussenBearbeitung.ZiehName(art) + " " + Texte.Gezogen, a => AussenBearbeitung.Ziehen(a, art, ax, ay, bx, by, true, out _));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal(PythonJson.Standard(vorschau.Stand), PythonJson.Standard(p.AussenStand));
        // ein Schritt zurueck nimmt alles mit (auch zusammengefasste Stuecke)
        p.Rueckgaengig();
        Assert.Equal(schritte, p.SchritteZurueck);
        Assert.NotEqual(PythonJson.Standard(vorschau.Stand), PythonJson.Standard(p.AussenStand));
    }

    // ---- Fussweg ----------------------------------------------------------------------------------------

    [Fact]
    public void Fussweg_AnDerGehwegkante_BegehbarOhneAutosUndOhneMittellinie()
    {
        var a = Leer();
        AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 2, 4, 30, 4, true, out _);
        var vorher = Szene(a);
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Fussweg, 10, 4.7, 18, 16, true, out var grund);
        Assert.Null(grund);
        Assert.Equal(AussenArt.Fussweg, s!.Art);
        var weg = Fusswege.Alle(Objekt(a["stadtdaten"]));
        Assert.Single(weg);
        // der Anfang liegt genau auf der Gehwegkante, der Weg verlaesst sie quer
        var kante = 4 + (StadtSzene.StrassenBreite / 2) + (float)AussenBearbeitung.NeuerGehweg;
        Assert.Equal(kante, weg[0].Punkte[0].Y, 3);
        Assert.Equal(weg[0].Punkte[0].X, weg[0].Punkte[1].X, 3);
        Assert.Empty(Strassen(a).Skip(1));
        var nachher = Szene(a);
        // begehbar entlang des ganzen Wegs
        foreach (var q in weg[0].Glatt)
        {
            Assert.True(nachher.Laufraster.KannStehen(q.X, q.Y), $"nicht begehbar bei {q}");
        }
        // keine Autos und keine Mittellinie: der Fussweg ist keine Strasse (Autostrecken und Striche lesen nur szene.Strassen)
        Assert.Equal(vorher.Strassen.Count, nachher.Strassen.Count);
        Assert.Equal(StadtSzene.MittellinienStriche([.. vorher.Strassen.Select(l => l.ToList())]), StadtSzene.MittellinienStriche([.. nachher.Strassen.Select(l => l.ToList())]));
        Assert.DoesNotContain(nachher.Strassen, l => l.Any(q => Kurve.Abstand(q, weg[0].Glatt) < 0.05f && q.Y > 5));
        Assert.DoesNotContain(Funde(a), f => f.Rot);
    }

    [Fact]
    public void Fussweg_Verlaengern_UndRadienJeEcke()
    {
        var a = Leer();
        AussenBearbeitung.Ziehen(a, ZiehArt.Fussweg, 4, 10, 10, 10, true, out _);
        AussenBearbeitung.Ziehen(a, ZiehArt.Fussweg, 10, 10, 18, 16, true, out var grund);
        Assert.Null(grund);
        var weg = Fusswege.Alle(Objekt(a["stadtdaten"]));
        Assert.Single(weg);
        Assert.Equal(new Vector2(4, 10), weg[0].Punkte[0]);
        Assert.True(weg[0].Punkte.Count >= 3);
        Assert.NotNull(weg[0].Radien);
        Assert.All(weg[0].Radien!, r => Assert.Contains((double)r, Fusswege.Radien));
    }

    [Fact]
    public void Fussweg_Loeschen_LetzterNimmtDenAbschnittMit()
    {
        var a = Leer();
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Fussweg, 4, 10, 10, 10, true, out _)!;
        Assert.True(Py.Hat(Objekt(a["stadtdaten"]), Fusswege.Abschnitt));
        Assert.True(AussenBearbeitung.Loeschen(a, s));
        Assert.False(Py.Hat(Objekt(a["stadtdaten"]), Fusswege.Abschnitt));
        Assert.NotNull(AussenBearbeitung.DrehenNicht(s));
    }

    // ---- Fluss ------------------------------------------------------------------------------------------

    [Fact]
    public void Fluss_VomKartenrand_RechtwinkligUndMitRadien()
    {
        var a = Leer();
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Fluss, 0.3, 18, 16, 23.7, true, out var grund);
        Assert.Null(grund);
        Assert.Equal(AussenArt.Fluss, s!.Art);
        var linie = Linie(a, s.Nummer);
        Assert.Equal(0, linie[0].X, 3);
        Assert.Equal(linie[0].Y, linie[1].Y, 3);
        Assert.Equal(24, linie[^1].Y, 3);
        Assert.DoesNotContain(Funde(a), f => f.Rot);
        // Gegenprobe: ohne Einrasten bleibt der Anfang frei (nicht am Rand)
        var b = Leer();
        var t = AussenBearbeitung.Ziehen(b, ZiehArt.Fluss, 0.3, 18, 16, 23.7, false, out _);
        Assert.NotEqual(0, Linie(b, t!.Nummer)[0].X, 3);
    }

    [Fact]
    public void EinEnde_BogenBis90Grad_VorStaerkeremMitGroesseremRadius()
    {
        // Fund im Foto: vom Fussweg-Ende nach unten zu (15; 13) gewann Radius 3 mit 120 Grad (Ecke weit unter dem Ziel,
        // ausserhalb der Karte); jetzt zuerst Boegen bis 90 Grad (hier 90 Grad mit Radius 2)
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_ziehen", 24, 16));
        AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 3, 6, 21, 6, true, out _);
        AussenBearbeitung.Ziehen(a, ZiehArt.Fussweg, 9, 6.8, 9, 11, true, out _);
        var (_, _, plan) = AussenBearbeitung.ZiehPlan(a, ZiehArt.Fussweg, 9, 11, 15, 13, true);
        Assert.True(plan.Gefunden, plan.Grund);
        Assert.All(plan.Ecken, e => Assert.True(Math.Abs(e.Grad) <= 90, $"{e.Grad} Grad"));
        Assert.All(plan.Eckzug, q => Assert.True(q.Y <= 16, $"Ecke {q} ausserhalb"));
        // ist das Ende hinter dem Anfang, gibt es weiter staerkere Boegen
        var hinten = Verbindung.Rechnen(new Vector2(10, 10), Vector2.UnitX, new Vector2(8, 16), null, Radien, true);
        Assert.True(hinten.Gefunden, hinten.Grund);
        Assert.Contains(hinten.Ecken, e => Math.Abs(e.Grad) > 90);
    }

    [Fact]
    public void Fluss_Verlaengern_StossInGeraderLinieKeinEckpunkt()
    {
        var a = Leer();
        AussenBearbeitung.Ziehen(a, ZiehArt.Fluss, 0.3, 5, 9, 5, true, out _);
        var s = AussenBearbeitung.Ziehen(a, ZiehArt.Fluss, 9, 5, 15, 12, true, out _)!;
        var linie = Linie(a, s.Nummer);
        Assert.DoesNotContain(new Vector2(9, 5), linie);
        Assert.Equal(3, linie.Count);
    }
}
