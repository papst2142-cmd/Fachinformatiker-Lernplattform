using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle K1 (A 0.44.0 / S 0.10.0, Weisung P12-55): Kurven setzen, einrasten, drehen, spiegeln, schieben; Radius je
/// Eckpunkt am Fluss; im Spiel das Band ohne Spalt (mit Gegenprobe: gerade Baender je Stueck ohne Gehrung lassen Spalte),
/// stumpfer Anschluss an eine gerade Strasse und begehbar fuer die Figur.
/// </summary>
public class KurvenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static PyObjekt Leer(double breite = 24, double hoehe = 16) => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_kurven", breite, hoehe));

    private static List<Vector2> Linie(PyObjekt a, AussenStelle s) =>
        [.. Liste(Objekt(Aussen.Landschaft(a)[s.Nummer])["punkte"]).Select(p => new Vector2((float)Py.F(Liste(p)[0]), (float)Py.F(Liste(p)[1])))];

    private static void Strasse(PyObjekt a, double x1, double y1, double x2, double y2) =>
        Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { x1, y1 }, new List<object?> { x2, y2 } } } });

    [Theory]
    [InlineData("bogen", 90, 1.0)]
    [InlineData("bogen", 90, 2.0)]
    [InlineData("bogen", 90, 3.0)]
    [InlineData("bogen", 45, 2.0)]
    [InlineData("s", 45, 2.0)]
    public void Mittellinie_AlleSiebenKommaFuenfGrad_AufDemKreis(string form, int winkel, double radius)
    {
        var p = Strassenkurve.Mittellinie(form, winkel, radius);
        var schritte = winkel / 7.5;
        Assert.Equal((form == "s" ? 2 * schritte : schritte) + 1, p.Count);
        Assert.Equal(Vector2.Zero, p[0]);
        // jeder Punkt des ersten Bogens liegt auf dem Kreis um (0, r)
        for (var i = 0; i <= schritte; i++)
        {
            Assert.Equal(radius, Vector2.Distance(p[i], new Vector2(0, (float)radius)), 4);
        }
        // jedes Stueck gleich lang (Sehne von 7,5 Grad)
        var sehne = 2 * radius * Math.Sin(7.5 / 2 * Math.PI / 180);
        for (var i = 0; i + 1 < p.Count; i++)
        {
            Assert.Equal(sehne, Vector2.Distance(p[i], p[i + 1]), 4);
        }
        var ende = Vector2.Normalize(p[^1] - p[^2]);
        if (form == "s")
        {
            // S-Kurve: am Ende wieder in Richtung +x, seitlich versetzt
            Assert.True(Vector2.Dot(ende, Vector2.Normalize(p[1] - p[0])) > 0.9999f);
            Assert.True(p[^1].Y > 0.5f);
        }
        else
        {
            var soll = new Vector2((float)Math.Cos(winkel * Math.PI / 180), (float)Math.Sin(winkel * Math.PI / 180));
            Assert.True(Vector2.Dot(ende, soll) > 0.995f, $"Ende zeigt {ende}");
        }
        // gespiegelt: gleiche Punkte an der x-Achse
        var m = Strassenkurve.Mittellinie(form, winkel, radius, gespiegelt: true);
        Assert.Equal(p.Select(q => new Vector2(q.X, -q.Y)), m);
    }

    [Fact]
    public void Kennung_NurErlaubteKurven()
    {
        Assert.Equal(("bogen", 90, 2.0, false), Strassenkurve.Lesen("bogen:90:2"));
        Assert.Equal(("s", 45, 2.0, true), Strassenkurve.Lesen(Strassenkurve.Kennung("s", 45, 2, true)));
        Assert.Null(Strassenkurve.Lesen("bogen:90:1.5"));
        Assert.Null(Strassenkurve.Lesen("bogen:45:1"));
        Assert.Null(Strassenkurve.Lesen("bogen:90:2:x"));
        Assert.Null(Strassenkurve.Lesen(null));
        Assert.Null(AussenBearbeitung.Setzen(Leer(), AussenArt.Kurve, 5, 5, modell: "bogen:90:1.5"));
    }

    [Fact]
    public void Setzen_SchreibtStrasseMitFeldKurveUndGehweg()
    {
        var a = Leer();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 10, 8, modell: "bogen:90:2")!;
        Assert.Equal(AussenArt.Strasse, s.Art);
        var l = Objekt(Aussen.Landschaft(a)[s.Nummer]);
        Assert.Equal("strasse", l["typ"]);
        Assert.Equal(AussenBearbeitung.NeuerGehweg, Py.F(l["gehweg"]), 6);
        var k = Objekt(l["kurve"]);
        Assert.Equal("bogen", k["form"]);
        Assert.Equal(90L, k["winkel"]);
        Assert.Equal(2.0, Py.F(k["radius"]));
        Assert.True(AussenBearbeitung.IstKurve(a, s));
        Assert.Equal(13, Linie(a, s).Count);
        // Mitte der Kurve (Umriss) auf (10, 8)
        var p = Linie(a, s);
        Assert.Equal(10, (p.Min(q => q.X) + p.Max(q => q.X)) / 2, 1);
        Assert.Equal(8, (p.Min(q => q.Y) + p.Max(q => q.Y)) / 2, 1);
        Assert.StartsWith("Kurve 90°, Radius 2", AussenBearbeitung.Beschreibung(a, s, x => x), StringComparison.Ordinal);
        // am Rand: ganz auf die Karte geschoben
        var rand = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 0.5, 0.5, modell: "bogen:90:3")!;
        Assert.All(Linie(a, rand), q => Assert.True(q.X >= -1e-4 && q.Y >= -1e-4));
    }

    [Fact]
    public void Setzen_EndeNebenStrassenende_RastetEin_GegenprobeZuWeit()
    {
        var a = Leer();
        Strasse(a, 2, 5, 6, 5);
        // Umriss 2 × 2, Anfang links oben: Mitte (7, 6) legt den Anfang auf (6, 5) + (0,2; 0,2)
        var s = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 7.2, 6.2, modell: "bogen:90:2")!;
        var p = Linie(a, s);
        Assert.True(Vector2.Distance(p[0], new Vector2(6, 5)) < 0.011f, $"Anfang {p[0]}");
        // Gegenprobe: zu weit weg (1,0, Fangweite seit S 0.12.1 eine Strassenbreite 0,95), kein Einrasten
        var b = Leer();
        Strasse(b, 2, 5, 6, 5);
        var t = AussenBearbeitung.Setzen(b, AussenArt.Kurve, 8, 6, modell: "bogen:90:2")!;
        Assert.True(Vector2.Distance(Linie(b, t)[0], new Vector2(6, 5)) > 0.4f);
    }

    [Fact]
    public void DrehenUndSpiegeln_BehaltenDieForm()
    {
        var a = Leer();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 10, 8, modell: "s:45:2")!;
        var vorher = Linie(a, s);
        Assert.True(AussenBearbeitung.Drehen(a, s));
        var gedreht = Linie(a, s);
        // gleiche Stuecklaengen (starr gedreht), Richtung am Anfang um 90 Grad weiter
        for (var i = 0; i + 1 < vorher.Count; i++)
        {
            Assert.Equal(Vector2.Distance(vorher[i], vorher[i + 1]), Vector2.Distance(gedreht[i], gedreht[i + 1]), 1);
        }
        Assert.True(MathF.Abs(Vector2.Dot(Vector2.Normalize(vorher[1] - vorher[0]), Vector2.Normalize(gedreht[1] - gedreht[0]))) < 0.05f);
        for (var i = 0; i < 3; i++)
        {
            AussenBearbeitung.Drehen(a, s);
        }
        Assert.All(vorher.Zip(Linie(a, s)), z => Assert.True(Vector2.Distance(z.First, z.Second) < 0.03f));

        // Spiegeln: der Anfang bleibt, der Bogen zeigt zur anderen Seite; zweimal = wie vorher
        var b = Leer();
        var k = AussenBearbeitung.Setzen(b, AussenArt.Kurve, 10, 8, modell: "bogen:90:2")!;
        var vor = Linie(b, k);
        Assert.Null(AussenBearbeitung.SpiegelnNicht(b, k));
        Assert.True(AussenBearbeitung.Spiegeln(b, k));
        var gesp = Linie(b, k);
        Assert.Equal(vor[0], gesp[0]);
        Assert.True((vor[^1].Y - vor[0].Y) * (gesp[^1].Y - gesp[0].Y) < 0, "biegt nicht zur anderen Seite");
        AussenBearbeitung.Spiegeln(b, k);
        Assert.All(vor.Zip(Linie(b, k)), z => Assert.True(Vector2.Distance(z.First, z.Second) < 0.02f));
        // eine gerade Strasse laesst sich nicht spiegeln
        var g = AussenBearbeitung.Setzen(b, AussenArt.Strasse, 4, 3)!;
        Assert.Equal(Texte.SpiegelnNurKurve, AussenBearbeitung.SpiegelnNicht(b, g));
        Assert.False(AussenBearbeitung.Spiegeln(b, g));
    }

    [Fact]
    public void Verschieben_KurveBleibtGanz_EndenRastenZusammenEin()
    {
        var a = Leer();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Kurve, 14, 8, modell: "bogen:90:1")!;
        var vorher = Linie(a, s);
        AussenBearbeitung.Verschieben(a, s, 1, 0.5);
        var nachher = Linie(a, s);
        Assert.All(vorher.Zip(nachher), z => Assert.True(Vector2.Distance(z.First + new Vector2(1, 0.5f), z.Second) < 0.011f));
        // eine Strasse endet 0,3 neben dem Kurvenende: nach dem Schieben liegt das Ende genau darauf, die Form bleibt
        Strasse(a, nachher[^1].X + 0.3, nachher[^1].Y + 4, nachher[^1].X + 0.3, nachher[^1].Y + 0.2);
        AussenBearbeitung.Verschieben(a, s, 0, 0);
        AussenBearbeitung.Verschieben(a, s, 0.5, 0);
        AussenBearbeitung.Verschieben(a, s, -0.5, 0);
        var eingerastet = Linie(a, s);
        Assert.True(Vector2.Distance(eingerastet[^1], new Vector2(nachher[^1].X + 0.3f, nachher[^1].Y + 0.2f)) < 0.011f, $"Ende {eingerastet[^1]}");
        for (var i = 0; i + 1 < nachher.Count; i++)
        {
            Assert.Equal(Vector2.Distance(nachher[i], nachher[i + 1]), Vector2.Distance(eingerastet[i], eingerastet[i + 1]), 1);
        }
    }

    [Fact]
    public void FlussRadius_JeEckpunkt_FeldBleibtImGleichschritt()
    {
        var a = Leer();
        var f = AussenBearbeitung.Setzen(a, AussenArt.Fluss, 12, 8)!;
        var mitte = f with { Punkt = 1 };
        Assert.False(AussenBearbeitung.RadiusWaehlbar(a, f with { Punkt = 0 }));
        Assert.True(AussenBearbeitung.RadiusWaehlbar(a, mitte));
        Assert.Equal(3, AussenBearbeitung.FlussRadius(a, mitte));
        Assert.False(AussenBearbeitung.FlussRadiusSetzen(a, mitte, 4));
        Assert.True(AussenBearbeitung.FlussRadiusSetzen(a, mitte, 5));
        var l = Objekt(Aussen.Landschaft(a)[f.Nummer]);
        Assert.Equal(new List<object?> { 3.0, 5.0, 3.0 }, Liste(l["radien"]));
        // Eckpunkt dazu: neuer Punkt mit 3, der Radius bleibt am alten Punkt
        var neu = AussenBearbeitung.EckpunktDazu(a, mitte)!;
        Assert.Equal(2, neu.Punkt);
        Assert.Equal(new List<object?> { 3.0, 5.0, 3.0, 3.0 }, Liste(l["radien"]));
        Assert.Equal(5, AussenBearbeitung.FlussRadius(a, mitte));
        // Eckpunkt weg, dann Radius zurueck auf 3: das Feld faellt weg (wie ohne)
        Assert.True(AussenBearbeitung.EckpunktWeg(a, neu));
        Assert.Equal(new List<object?> { 3.0, 5.0, 3.0 }, Liste(l["radien"]));
        Assert.True(AussenBearbeitung.FlussRadiusSetzen(a, mitte, 3));
        Assert.False(Py.Hat(l, "radien"));
    }

    [Fact]
    public void FlussRadius_ZeichnetEngerOderWeiter()
    {
        var p = new List<Vector2> { new(0, 0), new(12, 0), new(12, 12) };
        Assert.Equal(Kurve.Glatt(p), Kurve.Glatt(p, null));
        Assert.Equal(Kurve.Glatt(p), Kurve.Glatt(p, [3f, 3f, 3f]));
        foreach (var r in new[] { 2f, 5f })
        {
            var b = Kurve.Boegen(p, [3f, r, 3f]);
            Assert.Single(b);
            Assert.Equal(r, b[0].Radius, 3);
            // die Mitte des Bogens liegt r * (Wurzel 2 - 1) von der Ecke entfernt
            var glatt = Kurve.Glatt(p, [3f, r, 3f]);
            Assert.Equal(r * (MathF.Sqrt(2) - 1), Kurve.Abstand(new Vector2(12, 0), glatt), 1);
        }
    }

    // ---- Spiel: Zeichnung und Laufraster -------------------------------------------------------------

    /// <summary>Stadt mit gerader Strasse, Kurve 90° (Radius 1) direkt angesetzt und gerader Strasse danach.</summary>
    private static (PyObjekt A, List<Vector2> Kurve) MitKurve(double radius = 1)
    {
        var a = Leer();
        Strasse(a, 2, 5, 8, 5);
        var kurve = Strassenkurve.Mittellinie("bogen", 90, radius).Select(q => q + new Vector2(8, 5)).ToList();
        Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?>(kurve.Select(q => (object?)new List<object?> { (double)q.X, (double)q.Y })) },
            { "gehweg", 0.35 },
            { "kurve", new PyObjekt { { "form", "bogen" }, { "winkel", 90L }, { "radius", radius } } },
        });
        Strasse(a, kurve[^1].X, kurve[^1].Y, kurve[^1].X, 14);
        return (a, kurve);
    }

    private static StadtSzene Szene(PyObjekt a) =>
        Ansicht3D.Bauen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D, Ansicht3D.Einstellung.Standard).Szene;

    private static bool InViereck(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float K(Vector2 u, Vector2 v, Vector2 w) => ((v.X - u.X) * (w.Y - u.Y)) - ((v.Y - u.Y) * (w.X - u.X));
        var k = new[] { K(a, b, p), K(b, c, p), K(c, d, p), K(d, a, p) };
        return k.All(x => x >= -1e-5f) || k.All(x => x <= 1e-5f);
    }

    /// <summary>Oberseiten eines Streifens wie <c>Netz.Hinzu(Streifen)</c> (Gehrung ueber <see cref="Kurve.Normalen"/>).</summary>
    private static List<Vector2[]> Vierecke(Streifen s)
    {
        var n = Kurve.Normalen(s.Punkte);
        var halb = s.Breite / 2;
        var l = s.Punkte.Select((q, i) => q + (n[i].Quer * (halb * n[i].Faktor))).ToList();
        var r = s.Punkte.Select((q, i) => q - (n[i].Quer * (halb * n[i].Faktor))).ToList();
        return [.. Enumerable.Range(0, s.Punkte.Count - 1).Select(i => new[] { r[i], l[i], l[i + 1], r[i + 1] })];
    }

    /// <summary>Gegenprobe: je Stueck ein gerades Band ohne Gehrung und ohne Verlaengerung (so lagen die Spalte in den Entwurfsbildern).</summary>
    private static List<Vector2[]> OhneGehrung(IReadOnlyList<Vector2> p, float breite) =>
        [.. Enumerable.Range(0, p.Count - 1).Select(i =>
        {
            var q = new Vector2(-(p[i + 1] - p[i]).Y, (p[i + 1] - p[i]).X) / Vector2.Distance(p[i], p[i + 1]) * (breite / 2);
            return new[] { p[i] - q, p[i] + q, p[i + 1] + q, p[i + 1] - q };
        })];

    /// <summary>Punkte am Rand der Fahrbahn (0,01 innen), alle 0,005 entlang des Bogens: wie viele liegen in keinem Viereck?</summary>
    private static int Spalte(IReadOnlyList<Vector2> kurve, float radius, List<Vector2[]> vierecke, float breite)
    {
        var mitte = kurve[0] + new Vector2(0, radius);
        var fehlt = 0;
        foreach (var abstand in new[] { radius - (breite / 2) + 0.01f, radius + (breite / 2) - 0.01f })
        {
            for (var w = 0.02f; w < MathF.PI / 2 - 0.02f; w += 0.005f / abstand)
            {
                var q = mitte + new Vector2(MathF.Sin(w), -MathF.Cos(w)) * abstand;
                fehlt += vierecke.Any(v => InViereck(q, v[0], v[1], v[2], v[3])) ? 0 : 1;
            }
        }
        return fehlt;
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void Spiel_KurveAlsBand_OhneSpalt_GegenprobeOhneGehrungMitSpalt(double radius)
    {
        var (a, kurve) = MitKurve(radius);
        var szene = Szene(a);
        var strasse = szene.Streifen.Where(s => s.Breite == StadtSzene.StrassenBreite && Math.Abs(s.Oben - StadtSzene.Hoehe.Strasse) < 1e-6).ToList();
        var gehweg = szene.Streifen.Where(s => Math.Abs(s.Breite - (StadtSzene.StrassenBreite + 0.7f)) < 1e-5f).ToList();
        Assert.Single(strasse);
        Assert.Single(gehweg);
        // angesetzt an beide geraden Strassen: das Band beginnt und endet an den Kurvenenden (stumpf, mit einer kurzen
        // Zugabe in Richtung der geraden Strasse, damit die Kante quer zu ihr liegt)
        Assert.Equal(kurve, strasse[0].Punkte.Skip(1).Take(kurve.Count));
        Assert.Equal(kurve.Count + 2, strasse[0].Punkte.Count);
        Assert.Equal(kurve[0] - new Vector2(StadtSzene.StumpfZugabe, 0), strasse[0].Punkte[0]);
        Assert.Equal(kurve[^1] + new Vector2(0, StadtSzene.StumpfZugabe), strasse[0].Punkte[^1]);
        var r = (float)radius;
        Assert.Equal(0, Spalte(kurve, r, Vierecke(strasse[0]), StadtSzene.StrassenBreite));
        Assert.Equal(0, Spalte(kurve, r, Vierecke(gehweg[0]), StadtSzene.StrassenBreite + 0.7f));
        Assert.True(Spalte(kurve, r, OhneGehrung(kurve, StadtSzene.StrassenBreite), StadtSzene.StrassenBreite) > 0, "Gegenprobe ohne Spalt");
    }

    [Fact]
    public void Spiel_AnschlussAnGeradeStrasse_Stumpf_OhneEckeUeberDemBogen()
    {
        var (a, kurve) = MitKurve(1);
        var szene = Szene(a);
        // die gerade Strasse davor endet genau am Kurvenanfang (8, 5): ihr Band reicht nicht in den Bogen
        var band = szene.Teile.Where(t => t.Art == TeilArt.Band && Math.Abs(t.Y1 - StadtSzene.Hoehe.Strasse) < 1e-6 && Math.Abs(t.Z1 - 5) < 1e-4 && Math.Abs(t.Z2 - 5) < 1e-4).ToList();
        Assert.Single(band);
        Assert.Equal(8f, MathF.Max(band[0].X1, band[0].X2), 4);
        // die freie Seite (links am Kartenrand nicht, bei x = 2 frei) endet wie bisher an der Gehwegkante
        Assert.Equal(2f - (StadtSzene.WegBreite / 2), MathF.Min(band[0].X1, band[0].X2), 4);
        // kein Spalt an der Stossstelle: jeder Punkt der Fahrbahn 0,1 vor bis 0,1 hinter dem Kurvenanfang liegt im Band
        // der geraden Strasse oder in einem Viereck der Kurve
        var t = band[0];
        var halb = t.Y2 / 2;
        var gerade = new[] { new Vector2(t.X1, t.Z1 - halb), new Vector2(t.X1, t.Z1 + halb), new Vector2(t.X2, t.Z2 + halb), new Vector2(t.X2, t.Z2 - halb) };
        var vierecke = Vierecke(szene.Streifen.Single(st => st.Breite == StadtSzene.StrassenBreite && Math.Abs(st.Oben - StadtSzene.Hoehe.Strasse) < 1e-6));
        // kein Viereck ueberschlaegt sich (die Kanten links und rechts kreuzen sich nicht)
        Assert.All(vierecke, v => Assert.True(Vector2.Dot(v[3] - v[0], v[2] - v[1]) > 0, "Viereck ueberschlaegt sich"));
        vierecke.Add(gerade);
        for (var x = 7.9f; x <= 8.1f; x += 0.002f)
        {
            for (var y = 5 - halb + 0.005f; y <= 5 + halb - 0.005f; y += 0.01f)
            {
                var q = new Vector2(x, y);
                // hinter dem Anfang nur, was zur Fahrbahn des Bogens gehoert
                if (x > 8 && Kurve.Abstand(q, kurve) > halb - 0.005f)
                {
                    continue;
                }
                Assert.True(vierecke.Any(v => InViereck(q, v[0], v[1], v[2], v[3])), $"Spalt bei {q}");
            }
        }
        // Gegenprobe: ohne Kurve (zwei gerade Strassen im Knick) verlaengern sich beide um die halbe Breite
        var b = Leer();
        Strasse(b, 2, 5, 8, 5);
        Strasse(b, 8, 5, 8, 14);
        var knick = Szene(b).Teile.Where(t => t.Art == TeilArt.Band && Math.Abs(t.Y1 - StadtSzene.Hoehe.Strasse) < 1e-6 && Math.Abs(t.Z1 - 5) < 1e-4 && Math.Abs(t.Z2 - 5) < 1e-4).ToList();
        Assert.Equal(8f + (StadtSzene.StrassenBreite / 2), MathF.Max(knick[0].X1, knick[0].X2), 4);
        Assert.False(StadtSzene.Fortgesetzt(new Vector2(8, 5), Vector2.UnitX, [new(2, 5), new(8, 5)], [[new(8, 5), new(8, 14)]]));
    }

    [Fact]
    public void Spiel_FigurLaeuftDurchDieKurve()
    {
        var (a, kurve) = MitKurve(2);
        var szene = Szene(a);
        foreach (var q in kurve)
        {
            Assert.True(szene.Laufraster.KannStehen(q.X, q.Y), $"nicht begehbar bei {q}");
        }
        // Gegenprobe: 1,5 neben dem Bogen (Wiese ohne Weg) ist nicht immer frei? Die Wiese ist frei, darum: der Bogen
        // selbst liegt auf der Strasse (Mittellinie gezeichnet), Striche entlang des Bogens
        var striche = szene.Teile.Count(t => t.Art == TeilArt.Band && Math.Abs(t.Y1 - StadtSzene.Hoehe.Mittellinie) < 1e-6
            && Kurve.Abstand(new Vector2((t.X1 + t.X2) / 2, (t.Z1 + t.Z2) / 2), kurve) < 0.02f);
        Assert.True(striche >= 5, $"nur {striche} Striche im Bogen");
    }
}
