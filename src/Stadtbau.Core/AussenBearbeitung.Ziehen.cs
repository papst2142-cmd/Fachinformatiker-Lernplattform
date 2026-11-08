using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>Was beim Ziehen von Punkt zu Punkt entsteht (Kennung als <see cref="AussenArt.Ziehen"/>-Modell).</summary>
public enum ZiehArt
{
    Strasse,
    Fussweg,
    Fluss,

    /// <summary>Welle K2 (Weisung P12-72): Bahn mit Eckpunkten und Radius 4,5 oder 3 je Ecke.</summary>
    Bahn,
}

/// <summary>
/// Ein eingerasteter Punkt beim Ziehen: Lage, Richtung vom Bezug weg in die neue Verbindung hinein (null = frei) und das
/// Stueck, an dessen Ende er liegt (Nummer in der Landschaft bzw. in <c>stadtdaten.fusswege</c>, welches Ende), oder null.
/// </summary>
public sealed record Ziehpunkt(Vector2 Lage, Vector2? Weg, int? Nummer = null, bool AmAnfang = false);

/// <summary>
/// Welle Z (S 0.14.0 / A 0.48.0, Weisung P12-71, Kurzplan P12-40 Abschnitt 3, Nico P12-67 Fragen 3 und 4 = a): Strassen,
/// Fusswege und Fluesse von Punkt zu Punkt ziehen. Erster Tipp = Anfang, zweiter = Ende; beide rasten ein
/// (<see cref="Fangen"/>), das Werkzeug legt die Verbindung selbst (<see cref="Verbindung"/>) und setzt sie in EINEM Schritt
/// (<see cref="Ziehen"/>). Die Vorschau in 2D und 3D rechnet denselben Schritt auf einer Kopie (Vorschau = Ergebnis).
/// <list type="bullet">
/// <item>Strasse: gerade Stuecke und Boegen (Kurven mit Feld <c>kurve</c>, 15 bis 90 Grad in 7,5-Grad-Schritten, Radius
/// 3, 2 oder 1); faengt an freien Strassenenden (Lage und Richtung) und quer an geraden Strassen (Einmuendung). Ein
/// gerades Stueck in Verlaengerung einer geraden Strasse verlaengert diese (sonst fehlte am Stoss die Mittellinie).</item>
/// <item>Fussweg: ein Linienzug mit runden Ecken in <c>stadtdaten.fusswege</c>; faengt an Fusswegenden und an der
/// Aussenkante von Gehweg bzw. Strasse (quer). Am Ende eines Fusswegs wird dieser verlaengert.</item>
/// <item>Fluss: Eckpunkte mit Radius je Ecke (5, 3, 2); faengt an Flussenden und am Kartenrand (rechtwinklig). Am Ende
/// eines Flusses wird dieser verlaengert.</item>
/// <item>Bahn (Welle K2): wie der Fluss mit Radius 4,5 oder 3 je Ecke; faengt an Gleisenden und am Kartenrand; die Bahn
/// behaelt das Feld <c>radien</c> immer.</item>
/// </list>
/// </summary>
public static partial class AussenBearbeitung
{
    public const string ZiehKennungStrasse = "strasse";
    public const string ZiehKennungFussweg = "fussweg";
    public const string ZiehKennungFluss = "fluss";
    public const string ZiehKennungBahn = "bahn";

    /// <summary>Kennung fuer das Setzen (Modell der Art <see cref="AussenArt.Ziehen"/>).</summary>
    public static string ZiehKennung(ZiehArt art) => art switch
    {
        ZiehArt.Fussweg => ZiehKennungFussweg,
        ZiehArt.Fluss => ZiehKennungFluss,
        ZiehArt.Bahn => ZiehKennungBahn,
        _ => ZiehKennungStrasse,
    };

    /// <summary>Kennung lesen (null = keine).</summary>
    public static ZiehArt? ZiehArtVon(string? kennung) => kennung switch
    {
        ZiehKennungStrasse => ZiehArt.Strasse,
        ZiehKennungFussweg => ZiehArt.Fussweg,
        ZiehKennungFluss => ZiehArt.Fluss,
        ZiehKennungBahn => ZiehArt.Bahn,
        _ => null,
    };

    /// <summary>Radien, die beim Ziehen versucht werden (groesster zuerst).</summary>
    public static IReadOnlyList<double> ZiehRadien(ZiehArt art) => art switch
    {
        ZiehArt.Fluss => [.. Strassenkurve.FlussRadien.OrderByDescending(r => r)],
        ZiehArt.Bahn => [.. Strassenkurve.BahnRadien.OrderByDescending(r => r)],
        ZiehArt.Fussweg => [.. Fusswege.Radien.OrderByDescending(r => r)],
        _ => [.. Strassenkurve.ZugRadien.OrderByDescending(r => r)],
    };

    /// <summary>Name fuer Meldungen.</summary>
    public static string ZiehName(ZiehArt art) => art switch
    {
        ZiehArt.Fussweg => Texte.Fussweg,
        ZiehArt.Fluss => Texte.Fluss,
        ZiehArt.Bahn => Texte.Bahn,
        _ => Texte.Strasse,
    };

    /// <summary>Halbe Breite der Verbindung (Vorschau): Strasse samt neuem Gehweg, Fussweg, Fluss samt Ufer.</summary>
    public static float ZiehHalb(ZiehArt art) => art switch
    {
        ZiehArt.Fussweg => Fusswege.Breite / 2,
        ZiehArt.Fluss => LinienHalb(AussenArt.Fluss),
        ZiehArt.Bahn => LinienHalb(AussenArt.Bahn),
        _ => (StadtSzene.StrassenBreite / 2) + (float)NeuerGehweg,
    };

    // ---- Einrasten von Anfang und Ende ----------------------------------------------------------------

    /// <summary>
    /// Den Punkt (x, y) einrasten: an ein freies Ende (bis <see cref="EndeFangweite"/>) mit Lage und Richtung, sonst an eine
    /// Kante (Strasse: gerade Strasse quer, Fussweg: Gehwegkante, Fluss: Kartenrand), sonst frei auf das Raster
    /// (0,5 bzw. mit „Einrasten“ aus 0,1, dann auch ohne Fangen).
    /// </summary>
    public static Ziehpunkt Fangen(PyObjekt aussen, ZiehArt art, double x, double y, bool raster)
    {
        var p = new Vector2((float)x, (float)y);
        var frei = new Ziehpunkt(new Vector2((float)Bearbeitung.AufRaster(x, raster ? Bearbeitung.Raster : Bearbeitung.RasterFein),
            (float)Bearbeitung.AufRaster(y, raster ? Bearbeitung.Raster : Bearbeitung.RasterFein)), null);
        if (!raster)
        {
            return frei;
        }
        var ende = ZiehEnden(aussen, art).Select(e => (e, D: Vector2.Distance(e.Lage, p))).Where(e => e.D <= EndeFangweite)
            .OrderBy(e => e.D).Select(e => e.e).FirstOrDefault();
        if (ende is not null)
        {
            return ende;
        }
        return art switch
        {
            ZiehArt.Strasse => Einmuendung(aussen, p) ?? frei,
            ZiehArt.Fussweg => Gehwegkante(aussen, p) ?? frei,
            _ => Kartenrand(aussen, p) ?? frei,
        };
    }

    /// <summary>Freie Enden der gleichen Art mit der Richtung aus dem Stueck hinaus.</summary>
    private static IEnumerable<Ziehpunkt> ZiehEnden(PyObjekt aussen, ZiehArt art)
    {
        if (art == ZiehArt.Fussweg)
        {
            var liste = Aussen.Fusswege(aussen);
            for (var i = 0; i < liste.Count; i++)
            {
                if (Fusswege.Lesen(liste[i]) is not { } f || f.Punkte.Count < 2)
                {
                    continue;
                }
                var p = f.Punkte;
                yield return new Ziehpunkt(p[0], Vector2.Normalize(p[0] - p[1]), i, true);
                yield return new Ziehpunkt(p[^1], Vector2.Normalize(p[^1] - p[^2]), i, false);
            }
            yield break;
        }
        var land = Aussen.Landschaft(aussen);
        var typ = art switch { ZiehArt.Fluss => "fluss", ZiehArt.Bahn => "bahn", _ => "strasse" };
        var strassen = art == ZiehArt.Strasse
            ? land.OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "strasse")).Select(t => Linie(t["punkte"])).ToList()
            : [];
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is not PyObjekt l || !Py.Gleich(l["typ"], typ) || Kreisverkehr.IstRing(l) || Linie(l["punkte"]) is not { Count: >= 2 } linie
                || Vector2.Distance(linie[0], linie[^1]) < 1e-3f)
            {
                continue;
            }
            foreach (var anfang in new[] { true, false })
            {
                var e = anfang ? linie[0] : linie[^1];
                // ein Strassenende ist frei, wenn keine andere Strasse dort anschliesst oder einmuendet
                if (art == ZiehArt.Strasse && strassen.Any(s => !(s.Count == linie.Count && s.SequenceEqual(linie)) && s.Count >= 2 && Kurve.Abstand(e, s) <= 0.05f))
                {
                    continue;
                }
                var weg = art == ZiehArt.Strasse ? EndRichtung(linie, anfang) : Vector2.Normalize(anfang ? linie[0] - linie[1] : linie[^1] - linie[^2]);
                yield return new Ziehpunkt(e, weg, i, anfang);
            }
        }
    }

    /// <summary>Strasse: quer an eine gerade Strasse (Fusspunkt auf der Mittellinie, nicht naeher als eine Strassenbreite am Ende).</summary>
    private static Ziehpunkt? Einmuendung(PyObjekt aussen, Vector2 p)
    {
        Ziehpunkt? best = null;
        var bestD = (float)EndeFangweite;
        foreach (var l in Aussen.Landschaft(aussen).OfType<PyObjekt>())
        {
            if (!Py.Gleich(l["typ"], "strasse") || Strassenkurve.IstKurve(l) || Kreisverkehr.IstRing(l) || Linie(l["punkte"]) is not { Count: 2 } s)
            {
                continue;
            }
            var d = s[1] - s[0];
            var laenge = d.Length();
            if (laenge < 1e-3f)
            {
                continue;
            }
            var u = d / laenge;
            var t = Vector2.Dot(p - s[0], u);
            if (t < StadtSzene.StrassenBreite || t > laenge - StadtSzene.StrassenBreite)
            {
                continue;
            }
            var fuss = s[0] + (u * t);
            var abstand = Vector2.Distance(p, fuss);
            if (abstand > bestD)
            {
                continue;
            }
            var quer = new Vector2(-u.Y, u.X);
            var seite = Vector2.Dot(p - fuss, quer) >= 0 ? 1 : -1;
            best = new Ziehpunkt(Rund(fuss), quer * seite);
            bestD = abstand;
        }
        return best;
    }

    /// <summary>Fussweg: an die Aussenkante des Gehwegs (bzw. der Fahrbahn ohne Gehweg), quer von der Strasse weg.</summary>
    private static Ziehpunkt? Gehwegkante(PyObjekt aussen, Vector2 p)
    {
        Ziehpunkt? best = null;
        var bestD = (float)EndeFangweite;
        foreach (var l in Aussen.Landschaft(aussen).OfType<PyObjekt>())
        {
            if (!Py.Gleich(l["typ"], "strasse") || Linie(l["punkte"]) is not { Count: >= 2 } s)
            {
                continue;
            }
            var halb = (StadtSzene.StrassenBreite / 2) + StadtSzene.GehwegBreite(l);
            for (var i = 0; i + 1 < s.Count; i++)
            {
                var d = s[i + 1] - s[i];
                var laenge = d.Length();
                if (laenge < 1e-3f)
                {
                    continue;
                }
                var u = d / laenge;
                var t = Vector2.Dot(p - s[i], u);
                if (t < 0 || t > laenge)
                {
                    continue;
                }
                var fuss = s[i] + (u * t);
                var quer = new Vector2(-u.Y, u.X);
                var seite = Vector2.Dot(p - fuss, quer) >= 0 ? 1 : -1;
                var kante = fuss + (quer * seite * halb);
                var abstand = Vector2.Distance(p, kante);
                if (abstand <= bestD)
                {
                    best = new Ziehpunkt(Rund(kante), quer * seite);
                    bestD = abstand;
                }
            }
        }
        return best;
    }

    /// <summary>Fluss: an den Kartenrand (Lage auf dem Raster 0,5 laengs des Rands), rechtwinklig in die Karte hinein.</summary>
    private static Ziehpunkt? Kartenrand(PyObjekt aussen, Vector2 p)
    {
        var (b, h) = Aussen.Groesse(aussen);
        var raender = new (float D, Vector2 Lage, Vector2 Weg)[]
        {
            (p.X, new Vector2(0, (float)Bearbeitung.AufRaster(p.Y, Bearbeitung.Raster)), Vector2.UnitX),
            ((float)b - p.X, new Vector2((float)b, (float)Bearbeitung.AufRaster(p.Y, Bearbeitung.Raster)), -Vector2.UnitX),
            (p.Y, new Vector2((float)Bearbeitung.AufRaster(p.X, Bearbeitung.Raster), 0), Vector2.UnitY),
            ((float)h - p.Y, new Vector2((float)Bearbeitung.AufRaster(p.X, Bearbeitung.Raster), (float)h), -Vector2.UnitY),
        };
        var r = raender.MinBy(r => r.D);
        return r.D <= EndeFangweite ? new Ziehpunkt(r.Lage, r.Weg) : null;
    }

    private static Vector2 Rund(Vector2 p) => new((float)KurvenZahl(p.X), (float)KurvenZahl(p.Y));

    // ---- Planen und Setzen ----------------------------------------------------------------------------

    /// <summary>Anfang und Ende einrasten und die Verbindung rechnen (Vorschau und Schritt).</summary>
    public static (Ziehpunkt Anfang, Ziehpunkt Ende, Verbindung.Plan Plan) ZiehPlan(PyObjekt aussen, ZiehArt art, double ax, double ay, double bx, double by, bool raster)
    {
        var a = Fangen(aussen, art, ax, ay, raster);
        var b = Fangen(aussen, art, bx, by, raster);
        if (a.Nummer is { } na && b.Nummer == na && b.AmAnfang == a.AmAnfang)
        {
            // dasselbe Ende zweimal: das Ende frei lassen (sonst waere die Verbindung leer)
            b = new Ziehpunkt(new Vector2((float)bx, (float)by), null);
        }
        var plan = Verbindung.Rechnen(a.Lage, a.Weg, b.Lage, b.Weg, ZiehRadien(art), art == ZiehArt.Strasse);
        if (plan.Gefunden && !AufKarte(aussen, plan))
        {
            plan = plan with { Grund = Texte.ZiehenAusserhalb };
        }
        return (a, b, plan);
    }

    private static bool AufKarte(PyObjekt aussen, Verbindung.Plan plan)
    {
        var (b, h) = Aussen.Groesse(aussen);
        return plan.Eckzug.All(p => p.X >= -1e-3f && p.Y >= -1e-3f && p.X <= b + 1e-3f && p.Y <= h + 1e-3f);
    }

    /// <summary>
    /// Von (ax, ay) nach (bx, by) ziehen: einrasten, Verbindung rechnen und setzen, alles ein Schritt. Rueckgabe: das
    /// zuletzt gesetzte oder verlaengerte Stueck, null mit <paramref name="grund"/>, wenn keine Verbindung passt (dann
    /// aendert sich nichts).
    /// </summary>
    public static AussenStelle? Ziehen(PyObjekt aussen, ZiehArt art, double ax, double ay, double bx, double by, bool raster, out string? grund)
    {
        var (a, b, plan) = ZiehPlan(aussen, art, ax, ay, bx, by, raster);
        grund = plan.Grund;
        if (!plan.Gefunden)
        {
            return null;
        }
        return art switch
        {
            ZiehArt.Strasse => StrasseZiehen(aussen, a, b, plan),
            ZiehArt.Fussweg => LinieZiehen(aussen, a, b, plan, fussweg: true),
            _ => LinieZiehen(aussen, a, b, plan, fussweg: false, bahn: art == ZiehArt.Bahn),
        };
    }

    /// <summary>Strasse: gerade Stuecke und Bogenstuecke anlegen (gerade in Verlaengerung einer geraden Strasse = verlaengern).</summary>
    private static AussenStelle StrasseZiehen(PyObjekt aussen, Ziehpunkt a, Ziehpunkt b, Verbindung.Plan plan)
    {
        var land = Aussen.Landschaft(aussen);
        var stuecke = Verbindung.Stuecke(plan);
        bool Gerade(int? nr) => nr is { } n && land[n] is PyObjekt l && !Strassenkurve.IstKurve(l) && Liste(l["punkte"]).Count == 2;
        var gehweg = a.Nummer is { } an && Get(Objekt(land[an]), "gehweg") is { } g ? g
            : b.Nummer is { } bn && Get(Objekt(land[bn]), "gehweg") is { } g2 ? g2 : Bearbeitung.Zahl(NeuerGehweg);
        AussenStelle? letzte = null;
        int? weg = null;
        if (stuecke.Count > 0 && stuecke[0].Grad == 0 && Gerade(a.Nummer))
        {
            // Anfang an einer geraden Strasse in derselben Richtung: sie verlaengern
            var punkte = Liste(Objekt(land[a.Nummer!.Value])["punkte"]);
            punkte[a.AmAnfang ? 0 : 1] = KurvenPunkt(stuecke[0].Punkte[^1]);
            stuecke.RemoveAt(0);
            letzte = new AussenStelle(AussenArt.Strasse, a.Nummer.Value);
            if (stuecke.Count == 0 && Gerade(b.Nummer) && b.Nummer != a.Nummer)
            {
                // nur eine Gerade zwischen zwei geraden Strassen: zu einer Strasse zusammenfassen
                var andere = Liste(Objekt(land[b.Nummer!.Value])["punkte"]);
                punkte[a.AmAnfang ? 0 : 1] = andere[b.AmAnfang ? 1 : 0];
                weg = b.Nummer;
            }
        }
        if (weg is null && stuecke.Count > 0 && stuecke[^1].Grad == 0 && Gerade(b.Nummer) && b.Nummer != a.Nummer)
        {
            var punkte = Liste(Objekt(land[b.Nummer!.Value])["punkte"]);
            punkte[b.AmAnfang ? 0 : 1] = KurvenPunkt(stuecke[^1].Punkte[0]);
            stuecke.RemoveAt(stuecke.Count - 1);
            letzte ??= new AussenStelle(AussenArt.Strasse, b.Nummer.Value);
        }
        foreach (var (punkte, grad, radius) in stuecke)
        {
            var l = new PyObjekt
            {
                { "typ", "strasse" },
                { "punkte", new List<object?>(punkte.Select(q => (object?)KurvenPunkt(q))) },
                { "gehweg", gehweg },
            };
            if (grad > 0)
            {
                // ganze Zahl als long wie die festen Kurven (json.dumps: 45 statt 45.0); der Cast auf object vor dem ?: ist noetig,
                // sonst macht C# aus beiden Zweigen double
                var winkel = Math.Abs(grad - Math.Round(grad)) < 1e-9 ? (object)(long)Math.Round(grad) : grad;
                l.Add(Strassenkurve.Feld, new PyObjekt { { "form", Strassenkurve.Bogen }, { "winkel", winkel }, { "radius", Bearbeitung.Zahl(radius) } });
            }
            land.Add(l);
            letzte = new AussenStelle(AussenArt.Strasse, land.Count - 1);
        }
        if (weg is { } w)
        {
            land.RemoveAt(w);
            if (letzte is { } s && s.Nummer > w)
            {
                letzte = s with { Nummer = s.Nummer - 1 };
            }
        }
        return letzte!;
    }

    /// <summary>Fussweg, Fluss oder Bahn: Eckpunkte mit Radius je Ecke; am Ende eines gleichen Stuecks wird dieses verlaengert.</summary>
    private static AussenStelle LinieZiehen(PyObjekt aussen, Ziehpunkt a, Ziehpunkt b, Verbindung.Plan plan, bool fussweg, bool bahn = false)
    {
        var liste = fussweg ? Aussen.FusswegeAnlegen(aussen) : Aussen.Landschaft(aussen);
        var ende = fussweg ? Fusswege.Radius : Kurve.Radius;
        var punkte = plan.Eckzug;
        var radien = new List<double> { ende };
        radien.AddRange(plan.Ecken.Select(e => e.Radius));
        radien.Add(ende);
        (List<Vector2> P, List<double> R) Vorhanden(int nr, bool amAnfang)
        {
            var o = Objekt(liste[nr]);
            var p = Linie(o["punkte"]);
            var r = o[Strassenkurve.RadienFeld] is List<object?> rr && rr.Count == p.Count ? rr.Select(Py.F).ToList() : [.. p.Select(_ => (double)ende)];
            if (amAnfang)
            {
                p.Reverse();
                r.Reverse();
            }
            return (p, r);
        }
        int? ziel = null;
        var stoesse = new List<int>();
        if (a.Nummer is { } na)
        {
            // an das Ende des Stuecks anhaengen (der Stoss ist ein Eckpunkt in gerader Linie)
            var (p, r) = Vorhanden(na, a.AmAnfang);
            stoesse.Add(p.Count - 1);
            p.AddRange(punkte.Skip(1));
            r[^1] = ende;
            r.AddRange(radien.Skip(1));
            (punkte, radien) = (p, r);
            ziel = na;
        }
        if (b.Nummer is { } nb && nb != a.Nummer)
        {
            var (p, r) = Vorhanden(nb, !b.AmAnfang);
            stoesse.Add(punkte.Count - 1);
            punkte.AddRange(p.Skip(1));
            radien[^1] = ende;
            radien.AddRange(r.Skip(1));
        }
        // ein Stoss in gerader Linie ist kein Eckpunkt (er faellt weg, sonst bliebe ein unnoetiger Punkt im Stueck)
        foreach (var i in stoesse.OrderByDescending(i => i))
        {
            if (i > 0 && i < punkte.Count - 1 && Math.Abs(Verbindung.Grad(punkte[i] - punkte[i - 1], punkte[i + 1] - punkte[i])) < Verbindung.GleichGrad)
            {
                punkte.RemoveAt(i);
                radien.RemoveAt(i);
            }
        }
        var neu = new PyObjekt();
        if (!fussweg)
        {
            neu.Add("typ", bahn ? "bahn" : "fluss");
        }
        neu.Add("punkte", new List<object?>(punkte.Select(q => (object?)KurvenPunkt(q))));
        neu.Add(Strassenkurve.RadienFeld, new List<object?>(radien.Select(Bearbeitung.Zahl)));
        if (!fussweg && !bahn)
        {
            // der Fluss ohne eigene Radien wie bisher ohne Feld; die Bahn behaelt es immer
            RadienAufraeumen(neu);
        }
        // andere Felder des verlaengerten Stuecks bleiben (gleiche Stelle in der Liste)
        if (ziel is { } z)
        {
            var alt = Objekt(liste[z]);
            foreach (var (k, v) in alt.Where(e => e.Key is not ("typ" or "punkte" or Strassenkurve.RadienFeld)).ToList())
            {
                neu.Add(k, v);
            }
            liste[z] = neu;
        }
        else
        {
            liste.Add(neu);
            z = liste.Count - 1;
        }
        if (b.Nummer is { } weg && weg != a.Nummer)
        {
            liste.RemoveAt(weg);
            z -= weg < z ? 1 : 0;
        }
        return new AussenStelle(fussweg ? AussenArt.Fussweg : bahn ? AussenArt.Bahn : AussenArt.Fluss, z, fussweg ? 0 : -1);
    }

    /// <summary>Die gezeichnete Mittellinie der Verbindung (Vorschau): Strasse aus den Stuecken, Fussweg und Fluss mit runden
    /// Ecken; ohne passende Verbindung die gerade Linie von Anfang zu Ende.</summary>
    public static List<Vector2> ZiehLinie(ZiehArt art, Verbindung.Plan plan)
    {
        if (!plan.Gefunden)
        {
            return [plan.Anfang, plan.Ende];
        }
        if (art == ZiehArt.Strasse)
        {
            var linie = new List<Vector2>();
            foreach (var (punkte, _, _) in Verbindung.Stuecke(plan))
            {
                linie.AddRange(linie.Count == 0 ? punkte : punkte.Skip(1));
            }
            return linie;
        }
        var radien = new List<float> { 1 };
        radien.AddRange(plan.Ecken.Select(e => (float)e.Radius));
        radien.Add(1);
        return Kurve.Glatt(plan.Eckzug, radien);
    }

    // ---- Fusswege als Stuecke -------------------------------------------------------------------------

    /// <summary>Der gezeichnete Linienzug eines Fusswegs (wie im Spiel).</summary>
    public static List<Vector2> FusswegGlatt(PyObjekt aussen, AussenStelle s) =>
        Fusswege.Lesen(Aussen.Fusswege(aussen).ElementAtOrDefault(s.Nummer)) is { } f ? f.Glatt : [];

    /// <summary>Laenge eines Fusswegs (gezeichnet).</summary>
    public static double FusswegLaenge(PyObjekt aussen, AussenStelle s)
    {
        var g = FusswegGlatt(aussen, s);
        return R(Enumerable.Range(0, Math.Max(0, g.Count - 1)).Sum(i => Vector2.Distance(g[i], g[i + 1])));
    }

    private static IEnumerable<(AussenStelle Stelle, double X, double Y)> AlleFusswege(PyObjekt aussen)
    {
        var liste = Aussen.Fusswege(aussen);
        for (var i = 0; i < liste.Count; i++)
        {
            if (Fusswege.Lesen(liste[i]) is { } f)
            {
                var m = f.Glatt[f.Glatt.Count / 2];
                if (f.Glatt.Count == 2)
                {
                    m = (f.Glatt[0] + f.Glatt[1]) / 2;
                }
                yield return (new AussenStelle(AussenArt.Fussweg, i), m.X, m.Y);
            }
        }
    }

    private static AussenStelle? FusswegBei(PyObjekt aussen, Vector2 p, double greifen)
    {
        var liste = Aussen.Fusswege(aussen);
        AussenStelle? best = null;
        var bestD = (Fusswege.Breite / 2) + Math.Max(0.05, greifen / 3);
        for (var i = liste.Count - 1; i >= 0; i--)
        {
            if (Fusswege.Lesen(liste[i]) is { } f && Kurve.Abstand(p, f.Glatt) is var d && d <= bestD)
            {
                best = new AussenStelle(AussenArt.Fussweg, i);
                bestD = d;
            }
        }
        return best;
    }

    private static void FusswegVerschieben(PyObjekt aussen, AussenStelle s, double dx, double dy)
    {
        foreach (var p in Liste(Objekt(Aussen.Fusswege(aussen)[s.Nummer])["punkte"]))
        {
            var q = Liste(p);
            q[0] = KurvenZahl(Py.F(q[0]) + dx);
            q[1] = KurvenZahl(Py.F(q[1]) + dy);
        }
    }

    private static AussenStelle FusswegKopieren(PyObjekt aussen, AussenStelle s)
    {
        var liste = Aussen.Fusswege(aussen);
        liste.Add(Py.Kopie(liste[s.Nummer]));
        var neu = new AussenStelle(AussenArt.Fussweg, liste.Count - 1);
        FusswegVerschieben(aussen, neu, 0, 1);
        return neu;
    }

    private static string FusswegBeschreibung(PyObjekt aussen, AussenStelle s, string lage) =>
        string.Create(CultureInfo.GetCultureInfo("de-DE"), $"{Texte.Fussweg}, {FusswegLaenge(aussen, s):0.##} lang") + lage;
}
