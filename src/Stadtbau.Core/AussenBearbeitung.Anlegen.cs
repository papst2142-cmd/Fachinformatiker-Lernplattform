using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle E (S 0.13.0, Weisung P12-63, Kurzplan P12-40 Abschnitt 2, Nico 1a 2a): Einrasten fuer Fluesse, Haeuser und
/// Katalog-Stuecke. Alles nur bei eingeschaltetem „Einrasten“ (Taste E); ausgeschaltet setzt und schiebt das Werkzeug frei
/// im feinen Raster 0,1. Die Vorschau (2D und 3D) rechnet dieselben Schritte auf einer Kopie.
/// <list type="bullet">
/// <item>Haus, Gebaeude ohne Ort und Blueprint: die Seite mit der Tuer (ohne Tuer die Vorderseite) legt sich bis
/// <see cref="EndeFangweite"/> buendig an die Gehwegkante einer geraden Strasse, die parallel davor liegt. Ohne Gehweg
/// steht die Wand wie bei den Laeden der Stadt 1 <see cref="HausAbstandOhneGehweg"/> von der Strassenmitte. Tuer, Fussweg
/// und Schild gehen mit, der Fussweg laeuft gerade zur Strasse.</item>
/// <item>Katalog-Stueck: steht mit der Rueckseite an der aeusseren Gehwegkante, die Vorderseite zur Strasse (nur Strassen
/// mit Gehweg; nicht an Hauswaende). Effekte rasten nicht ein.</item>
/// <item>Fluss: ein Ende rastet bis <see cref="EndeFangweite"/> an das freie Ende eines anderen Flusses (Lage und Richtung,
/// bis <see cref="GeradeDrehtBis"/> Grad), sonst an den Kartenrand (letztes Stueck rechtwinklig zum Rand). Ein gezogener
/// Eckpunkt legt ein Stueck, das eine gerade Strasse fast quer kreuzt (bis <see cref="BrueckeFangGrad"/> Grad), genau quer:
/// die Bruecke ist dann so kurz wie moeglich.</item>
/// </list>
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Abstand der Hauswand von der Strassenmitte ohne Gehweg (die Laeden der Stadt 1: Tuer 0,6 vor der Strassenmitte).</summary>
    public const double HausAbstandOhneGehweg = 0.6;

    /// <summary>So weit (in Grad) darf ein Flussstueck vom rechten Winkel zur Strasse abweichen, damit es quer einrastet.</summary>
    public const double BrueckeFangGrad = 20;

    private const double ParallelGrad = 1;

    /// <summary>Gerade Strassen (2 Punkte, ohne Bogen, kein Ring) mit Mittellinie und halber Breite bis zur Gehwegkante.</summary>
    private static List<(Vector2 A, Vector2 B, float Gehweg)> GeradeStrassen(PyObjekt aussen) =>
        [.. Aussen.Landschaft(aussen).OfType<PyObjekt>()
            .Where(t => Py.Gleich(t["typ"], "strasse") && !Strassenkurve.IstKurve(t) && !Kreisverkehr.IstRing(t))
            .Select(t => (L: Linie(t["punkte"]), G: (float)Py.F(Get(t, "gehweg", 0L))))
            .Where(t => t.L.Count == 2 && Vector2.Distance(t.L[0], t.L[1]) > 1e-3f)
            .Select(t => (t.L[0], t.L[1], t.G))];

    /// <summary>
    /// Wie weit muss eine Kante (Mitte <paramref name="kante"/>, halbe Laenge <paramref name="halb"/>, Richtung hinaus
    /// <paramref name="n"/>) nach vorn, damit sie im Abstand <paramref name="ziel"/>(Gehweg) vor der Mitte einer parallelen
    /// geraden Strasse steht? Null, wenn keine Strasse bis <see cref="EndeFangweite"/> passt.
    /// </summary>
    private static double? Anlegen(PyObjekt aussen, Vector2 kante, double halb, Vector2 n, Func<float, double?> ziel)
    {
        double? bester = null;
        var sinGrenze = Math.Sin(ParallelGrad * Math.PI / 180);
        foreach (var (a, b, gehweg) in GeradeStrassen(aussen))
        {
            var u = Vector2.Normalize(b - a);
            if (Math.Abs(Vector2.Dot(u, n)) > sinGrenze || ziel(gehweg) is not { } soll)
            {
                continue;
            }
            var d = Vector2.Dot(a - kante, n);
            if (d <= 0)
            {
                continue;
            }
            // die Kante muss neben der Strasse liegen (Ueberlappung laengs)
            var t0 = Vector2.Dot(kante - a, u);
            var laenge = Vector2.Distance(a, b);
            if (t0 + halb <= 0.01 || t0 - halb >= laenge - 0.01)
            {
                continue;
            }
            var weg = d - soll;
            if (Math.Abs(weg) <= EndeFangweite + 1e-4 && (bester is null || Math.Abs(weg) < Math.Abs(bester.Value)))
            {
                bester = weg;
            }
        }
        return bester;
    }

    /// <summary>Halbe Breite der Strasse bis zur Gehwegkante (ohne Gehweg die Fahrbahn).</summary>
    private static double Kante(float gehweg) => (StadtSzene.StrassenBreite / 2) + gehweg;

    /// <summary>Seite eines Hauses, an der die Tuer liegt (ohne Tuer die gedrehte Vorderseite): Richtung hinaus.</summary>
    public static Vector2 HausTuerSeite(PyObjekt aussen, AussenStelle s)
    {
        PyObjekt? zugang = s.Art == AussenArt.Haus ? Zugang(aussen, s.Ort)
            : Zierwege(aussen, Py.Str(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["modell"])).FirstOrDefault();
        if (zugang?["tuer"] is not List<object?> t)
        {
            return HausFront(aussen, s);
        }
        var (x, y, w, h) = HausFlaeche(aussen, s);
        var p = new Vector2((float)Py.F(t[0]), (float)Py.F(t[1]));
        var seiten = new (float D, Vector2 N)[]
        {
            (MathF.Abs(p.Y - (float)(y + h)), new(0, 1)), (MathF.Abs(p.Y - (float)y), new(0, -1)),
            (MathF.Abs(p.X - (float)(x + w)), new(1, 0)), (MathF.Abs(p.X - (float)x), new(-1, 0)),
        };
        return seiten.MinBy(k => k.D).N;
    }

    /// <summary>Haus mit der Tuerseite an die Gehwegkante legen; true = verschoben.</summary>
    public static bool HausAnGehweg(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art is not (AussenArt.Haus or AussenArt.Gebaeude))
        {
            return false;
        }
        var (x, y, w, h) = HausFlaeche(aussen, s);
        var n = HausTuerSeite(aussen, s);
        var mitte = new Vector2((float)(x + (w / 2)), (float)(y + (h / 2)));
        var tief = n.X != 0 ? w / 2 : h / 2;
        var halb = n.X != 0 ? h / 2 : w / 2;
        if (Anlegen(aussen, mitte + (n * (float)tief), halb, n, g => g > 0 ? Kante(g) : HausAbstandOhneGehweg) is not { } weg || Math.Abs(weg) < 0.005)
        {
            return false;
        }
        VerschiebenGenau(aussen, s, R(n.X * weg), R(n.Y * weg));
        return true;
    }

    /// <summary>Katalog-Stueck mit der Rueckseite an die aeussere Gehwegkante (Vorderseite zur Strasse); true = verschoben.</summary>
    public static bool KatalogAnGehweg(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art != AussenArt.Katalog)
        {
            return false;
        }
        var k = Objekt(Aussen.KatalogStuecke(aussen)[s.Nummer]);
        if (Katalogstueck.IstEffekt(Py.Str(k["id"])))
        {
            return false;
        }
        var n = Hausdrehung.Drehen(new Vector2(0, 1), Vector2.Zero, (int)Py.Int(Get(k, "dreh", 0L)));
        double w = Py.F(k["w"]), t = Py.F(k["t"]);
        var mitte = new Vector2((float)Py.F(k["x"]), (float)Py.F(k["y"]));
        var tief = n.X != 0 ? w / 2 : t / 2;
        var halb = n.X != 0 ? t / 2 : w / 2;
        // Rueckseite = Kante gegen n; sie soll an der aeusseren Gehwegkante stehen (nur Strassen mit Gehweg)
        if (Anlegen(aussen, mitte - (n * (float)tief), halb, n, g => g > 0 ? Kante(g) : null) is not { } weg || Math.Abs(weg) < 0.005)
        {
            return false;
        }
        Py.Setzen(k, "x", Bearbeitung.Zahl(Py.F(k["x"]) + (n.X * weg)));
        Py.Setzen(k, "y", Bearbeitung.Zahl(Py.F(k["y"]) + (n.Y * weg)));
        return true;
    }

    // ---- Fluss --------------------------------------------------------------------------------------

    /// <summary>Freie Enden anderer Fluesse mit Richtung hinaus.</summary>
    private static List<(Vector2 Ende, Vector2 Hinaus)> FreieFlussEnden(PyObjekt aussen, PyObjekt eigener)
    {
        var enden = new List<(Vector2, Vector2)>();
        foreach (var t in Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => !ReferenceEquals(t, eigener) && Py.Gleich(t["typ"], "fluss")))
        {
            var l = Linie(t["punkte"]);
            if (l.Count >= 2)
            {
                enden.Add((l[0], Vector2.Normalize(l[0] - l[1])));
                enden.Add((l[^1], Vector2.Normalize(l[^1] - l[^2])));
            }
        }
        return enden;
    }

    /// <summary>
    /// Fluss einrasten (nach Setzen oder Verschieben des ganzen Flusses oder eines Endpunkts): Ende an Ende eines anderen
    /// Flusses (Lage und Richtung), sonst Ende an den Kartenrand (rechtwinklig). true = etwas geaendert.
    /// </summary>
    public static bool FlussEinrasten(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art != AussenArt.Fluss)
        {
            return false;
        }
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        var punkte = Liste(l["punkte"]);
        var linie = Linie(punkte);
        if (linie.Count < 2)
        {
            return false;
        }
        // nur das gezogene Ende (Eckpunkt) oder beide Enden (ganzer Fluss)
        var anfaenge = s.Punkt < 0 ? new[] { true, false } : s.Punkt == 0 ? [true] : s.Punkt == linie.Count - 1 ? [false] : [];
        (bool Anfang, Vector2 Ziel, float Drehung, float D)? bester = null;
        foreach (var (ziel, hinaus) in FreieFlussEnden(aussen, l))
        {
            foreach (var anfang in anfaenge)
            {
                var ende = anfang ? linie[0] : linie[^1];
                var eigenHinaus = Vector2.Normalize(anfang ? linie[0] - linie[1] : linie[^1] - linie[^2]);
                var d = Vector2.Distance(ende, ziel);
                var drehung = Winkel(eigenHinaus, -hinaus);
                drehung = Math.Abs(drehung) < RundungGrad * MathF.PI / 180 ? 0 : drehung;
                if (d <= EndeFangweite + 1e-4 && Math.Abs(drehung) <= (GeradeDrehtBis * Math.PI / 180) + 1e-4 && (bester is not { } b || d < b.D))
                {
                    bester = (anfang, ziel, drehung, d);
                }
            }
        }
        if (bester is { } g)
        {
            if (g.D < 1e-3f && g.Drehung == 0)
            {
                return false;
            }
            var ende = g.Anfang ? linie[0] : linie[^1];
            if (s.Punkt >= 0)
            {
                // ein gezogener Endpunkt legt sich nur auf das Ende (die Nachbarpunkte bleiben)
                punkte[s.Punkt] = PunktObjekt(g.Ziel);
                return true;
            }
            for (var i = 0; i < punkte.Count; i++)
            {
                punkte[i] = PunktObjekt(g.Ziel + Gedreht(linie[i] - ende, g.Drehung));
            }
            return true;
        }
        return FlussAnRand(aussen, punkte, linie, anfaenge);
    }

    /// <summary>Flussende bis <see cref="EndeFangweite"/> am Kartenrand: auf den Rand, letztes Stueck rechtwinklig dazu.</summary>
    private static bool FlussAnRand(PyObjekt aussen, List<object?> punkte, List<Vector2> linie, bool[] anfaenge)
    {
        var (breite, hoehe) = Aussen.Groesse(aussen);
        var geaendert = false;
        foreach (var anfang in anfaenge)
        {
            var i = anfang ? 0 : linie.Count - 1;
            var e = linie[i];
            var v = anfang ? linie[1] : linie[^2];
            var raender = new (double D, Vector2 Neu)[]
            {
                (e.X, new(0, v.Y)), (breite - e.X, new((float)breite, v.Y)), (e.Y, new(v.X, 0)), (hoehe - e.Y, new(v.X, (float)hoehe)),
            };
            var rand = raender.MinBy(r => Math.Abs(r.D));
            if (Math.Abs(rand.D) > EndeFangweite + 1e-4 || Vector2.Distance(rand.Neu, e) < 1e-3f)
            {
                continue;
            }
            // nur, wenn das letzte Stueck schon ungefaehr zum Rand laeuft (bis 45 Grad)
            var zumRand = Vector2.Normalize(rand.Neu - v);
            if (Vector2.Distance(rand.Neu, v) < 0.3f || Vector2.Dot(Vector2.Normalize(e - v), zumRand) < MathF.Cos(MathF.PI / 4))
            {
                continue;
            }
            punkte[i] = PunktObjekt(rand.Neu);
            geaendert = true;
        }
        return geaendert;
    }

    /// <summary>
    /// Nach dem Ziehen eines Eckpunkts: kreuzt ein Nachbarstueck eine gerade Strasse fast quer (bis <see cref="BrueckeFangGrad"/>),
    /// legt sich der Eckpunkt so, dass es genau quer kreuzt (Laenge bleibt). true = geaendert.
    /// </summary>
    public static bool BrueckeQuer(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art != AussenArt.Fluss || s.Punkt < 0)
        {
            return false;
        }
        var punkte = Eckpunkte(aussen, s);
        var linie = Linie(punkte);
        var k = s.Punkt;
        foreach (var nachbar in new[] { k - 1, k + 1 }.Where(i => i >= 0 && i < linie.Count))
        {
            var (fest, frei) = (linie[nachbar], linie[k]);
            foreach (var (a, b, _) in GeradeStrassen(aussen))
            {
                if (Schnitt(fest, Vector2.Normalize(frei - fest), a, b) is not { } t || t <= 0.01f || t >= Vector2.Distance(fest, frei) - 0.01f)
                {
                    continue;
                }
                var u = Vector2.Normalize(b - a);
                var quer = new Vector2(-u.Y, u.X);
                var richtung = Vector2.Normalize(frei - fest);
                if (Vector2.Dot(quer, richtung) < 0)
                {
                    quer = -quer;
                }
                var abweichung = Math.Acos(Math.Clamp(Vector2.Dot(quer, richtung), -1, 1)) * 180 / Math.PI;
                if (abweichung < RundungGrad || abweichung > BrueckeFangGrad)
                {
                    continue;
                }
                punkte[k] = PunktObjekt(fest + (quer * Vector2.Distance(fest, frei)));
                return true;
            }
        }
        return false;
    }
}
