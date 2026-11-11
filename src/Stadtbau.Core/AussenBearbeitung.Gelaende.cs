using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>Was der Pinsel des Gelaendes tut.</summary>
public enum PinselArt
{
    Heben,
    Senken,
    Glaetten,
}

/// <summary>
/// Welle S 0.42.0 / A 0.66.0 (Weisung P12-192, Nico 1c 2b 3c 4a 5a): Gelaende im Werkzeug. Huegel setzen (Knopf „Hügel“ in
/// „Natur und Licht“), schieben, kopieren, loeschen, Groesse/Hoehe/Form/Laenge aendern, laengliche in 15-Grad-Schritten drehen;
/// Pinsel (heben, senken, glaetten) auf dem Raster 0,5, jeder Zug ein Schritt. Daten in <c>stadtdaten.gelaende</c>
/// (<see cref="Gelaende"/>); der letzte Huegel und die letzte gemalte Hoehe weg = der Abschnitt faellt weg.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Vorgaben eines neuen Huegels.</summary>
    public const double HuegelRadiusNeu = 2.0;

    /// <inheritdoc cref="HuegelRadiusNeu"/>
    public const double HuegelHoeheNeu = 0.8;

    /// <summary>Pinsel: so viel hebt oder senkt ein Tupfer in der Mitte (am Rand weniger).</summary>
    public const float PinselStaerke = 0.1f;

    /// <summary>Pinsel: Groessen (Radius in Feldern).</summary>
    public static readonly IReadOnlyList<float> PinselGroessen = [0.5f, 1f, 1.5f, 2f];

    /// <summary>Abschnitt „gelaende“ der Stadt (null ohne Gelaende), z. B. als Schluessel fuer zwischengespeicherte Hoehenlinien.</summary>
    public static PyObjekt? GelaendeAbschnitt(PyObjekt aussen) => GelaendeDaten(aussen);

    private static PyObjekt? GelaendeDaten(PyObjekt aussen) => Get(Aussen.Stadtdaten(aussen), Gelaende.Abschnitt) as PyObjekt;

    private static PyObjekt GelaendeAnlegen(PyObjekt aussen)
    {
        var sd = Aussen.Stadtdaten(aussen);
        if (Get(sd, Gelaende.Abschnitt) is not PyObjekt g)
        {
            g = [];
            Py.Setzen(sd, Gelaende.Abschnitt, g);
        }
        return g;
    }

    /// <summary>Liste der Huegel (leer, wenn es keine gibt).</summary>
    public static List<object?> HuegelListe(PyObjekt aussen) => GelaendeDaten(aussen) is { } g ? Get(g, "huegel") as List<object?> ?? [] : [];

    private static List<object?> HuegelListeAnlegen(PyObjekt aussen)
    {
        var g = GelaendeAnlegen(aussen);
        if (Get(g, "huegel") is not List<object?> l)
        {
            l = [];
            Py.Setzen(g, "huegel", l);
        }
        return l;
    }

    /// <summary>Das gelesene Gelaende (null ohne Abschnitt).</summary>
    public static Gelaende? GelaendeVon(PyObjekt aussen) => Gelaende.Aus(Aussen.Stadtdaten(aussen));

    /// <summary>Der Huegel an der Stelle (gelesen, null = falsch gespeichert).</summary>
    public static Huegel? HuegelVon(PyObjekt aussen, AussenStelle s)
    {
        var liste = HuegelListe(aussen);
        if (s.Nummer >= liste.Count)
        {
            return null;
        }
        return Gelaende.HuegelAus(liste[s.Nummer], s.Nummer + 1);
    }

    private static IEnumerable<(AussenStelle Stelle, double X, double Y)> AlleHuegel(PyObjekt aussen)
    {
        var liste = HuegelListe(aussen);
        for (var i = 0; i < liste.Count; i++)
        {
            if (liste[i] is PyObjekt h && Py.Hat(h, "x") && Py.Hat(h, "y"))
            {
                yield return (new AussenStelle(AussenArt.Huegel, i), Py.F(h["x"]), Py.F(h["y"]));
            }
        }
    }

    /// <summary>Der oberste Huegel unter dem Punkt (Umriss), sonst null.</summary>
    private static AussenStelle? HuegelBei(PyObjekt aussen, Vector2 p, double greifen)
    {
        var liste = HuegelListe(aussen);
        for (var i = liste.Count - 1; i >= 0; i--)
        {
            if (HuegelVon(aussen, new AussenStelle(AussenArt.Huegel, i)) is { } h && h.Abstand(p) <= h.Radius + (float)Math.Max(0, greifen - 0.3))
            {
                return new AussenStelle(AussenArt.Huegel, i);
            }
        }
        return null;
    }

    private static AussenStelle HuegelSetzen(PyObjekt aussen, double x, double y, bool raster)
    {
        var r = raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        var liste = HuegelListeAnlegen(aussen);
        liste.Add(Gelaende.HuegelDaten((float)Bearbeitung.AufRaster(x, r), (float)Bearbeitung.AufRaster(y, r), (float)HuegelRadiusNeu, (float)HuegelHoeheNeu, false, 0, 0));
        return new AussenStelle(AussenArt.Huegel, liste.Count - 1);
    }

    private static void HuegelVerschieben(PyObjekt aussen, AussenStelle s, double dx, double dy)
    {
        var h = Objekt(HuegelListe(aussen)[s.Nummer]);
        Py.Setzen(h, "x", Bearbeitung.Zahl(Py.F(h["x"]) + dx));
        Py.Setzen(h, "y", Bearbeitung.Zahl(Py.F(h["y"]) + dy));
    }

    private static AussenStelle HuegelKopieren(PyObjekt aussen, AussenStelle s)
    {
        var liste = HuegelListe(aussen);
        var kopie = (PyObjekt)Py.Kopie(liste[s.Nummer])!;
        var versatz = (2 * Py.F(kopie["radius"])) + (Py.Hat(kopie, "laenge") ? Py.F(kopie["laenge"]) - (2 * Py.F(kopie["radius"])) : 0) + 0.5;
        Py.Setzen(kopie, "x", Bearbeitung.Zahl(Py.F(kopie["x"]) + versatz));
        liste.Add(kopie);
        return new AussenStelle(AussenArt.Huegel, liste.Count - 1);
    }

    private static void HuegelLoeschen(PyObjekt aussen, int nummer)
    {
        var liste = HuegelListe(aussen);
        liste.RemoveAt(nummer);
        if (liste.Count == 0)
        {
            var g = GelaendeAnlegen(aussen);
            Weg(g, "huegel");
        }
        GelaendeAufraeumen(aussen);
    }

    /// <summary>Faellt der Abschnitt <c>gelaende</c> weg, wenn er leer ist (wie vor dem ersten Huegel).</summary>
    private static void GelaendeAufraeumen(PyObjekt aussen)
    {
        if (GelaendeDaten(aussen) is { Count: 0 })
        {
            Weg(Aussen.Stadtdaten(aussen), Gelaende.Abschnitt);
        }
    }

    private static void Weg(PyObjekt o, string schluessel)
    {
        var rest = o.Where(e => e.Key != schluessel).ToList();
        o.Clear();
        o.AddRange(rest);
    }

    /// <summary>Laengliche Huegel drehen in 15-Grad-Schritten (runde sehen von allen Seiten gleich aus).</summary>
    private static bool HuegelDrehen(PyObjekt aussen, AussenStelle s, int grad)
    {
        var h = Objekt(HuegelListe(aussen)[s.Nummer]);
        if (!Py.Gleich(h["form"], "laenglich"))
        {
            return false;
        }
        var alt = Py.Hat(h, "dreh") ? Py.Int(h["dreh"]) : 0;
        Py.Setzen(h, "dreh", (long)((((alt + grad) % 360) + 360) % 360));
        return true;
    }

    /// <summary>Werte eines Huegels: Radius, Hoehe, laenglich, Laenge (rund = 0), Drehung.</summary>
    public static (double Radius, double Hoehe, bool Laenglich, double Laenge, int Dreh) HuegelWerte(PyObjekt aussen, AussenStelle s)
    {
        var h = Objekt(HuegelListe(aussen)[s.Nummer]);
        var laenglich = Py.Gleich(h["form"], "laenglich");
        return (Py.F(h["radius"]), Py.F(h["hoehe"]), laenglich, laenglich && Py.Hat(h, "laenge") ? Py.F(h["laenge"]) : 0,
            Py.Hat(h, "dreh") ? (int)Py.Int(h["dreh"]) : 0);
    }

    /// <summary>
    /// Werte eines Huegels setzen (begrenzt auf die erlaubten Bereiche: Radius 1–3, Hoehe 0,2–1,5, Laenge 2·Radius–6; ein
    /// zu langer laenglicher Huegel wird kuerzer, ein zu breiter rund). Mitte bleibt.
    /// </summary>
    public static void HuegelWerteSetzen(PyObjekt aussen, AussenStelle s, double radius, double hoehe, bool laenglich, double laenge)
    {
        var h = Objekt(HuegelListe(aussen)[s.Nummer]);
        var dreh = Py.Hat(h, "dreh") ? (int)Py.Int(h["dreh"]) : 0;
        radius = Math.Clamp(Math.Round(radius * 4) / 4, Gelaende.RadiusMin, Gelaende.RadiusMax);
        hoehe = Math.Clamp(Math.Round(hoehe * 20) / 20, Gelaende.HoeheMin, Gelaende.HoeheMax);
        if (laenglich && 2 * radius > Gelaende.LaengeMax)
        {
            laenglich = false;
        }
        laenge = laenglich ? Math.Clamp(Math.Round(laenge * 2) / 2, 2 * radius, Gelaende.LaengeMax) : 0;
        var neu = Gelaende.HuegelDaten((float)Py.F(h["x"]), (float)Py.F(h["y"]), (float)radius, (float)hoehe, laenglich, (float)laenge, dreh);
        h.Clear();
        h.AddRange(neu);
    }

    // ---- Pinsel ---------------------------------------------------------------------------------------

    /// <summary>
    /// Ein Pinselzug: an jedem Punkt des Zugs (Abstand hoechstens ein halbes Raster, sonst wird nachgefuellt) die Rasterpunkte
    /// bis <paramref name="groesse"/> heben, senken oder glaetten (am Rand weniger). Hoehen bleiben zwischen 0 und 1,5 auf
    /// 0,01; ausserhalb der Karte nichts. Ergebnis: Zahl der geaenderten Rasterpunkte (0 = nichts geaendert).
    /// </summary>
    public static int Pinselzug(PyObjekt aussen, PinselArt art, IReadOnlyList<Vector2> zug, float groesse)
    {
        if (zug.Count == 0)
        {
            return 0;
        }
        var (breite, hoehe) = Aussen.Groesse(aussen);
        var gemalt = new Dictionary<(int I, int J), int>();
        foreach (var p in GemaltListe(aussen))
        {
            if (p is List<object?> { Count: 3 } q && q[0] is long i && q[1] is long j && q[2] is long h)
            {
                gemalt[((int)i, (int)j)] = (int)h;
            }
        }
        var vorher = new Dictionary<(int, int), int>(gemalt);
        var punkte = new List<Vector2> { zug[0] };
        for (var k = 1; k < zug.Count; k++)
        {
            var d = Vector2.Distance(zug[k - 1], zug[k]);
            var n = Math.Max(1, (int)MathF.Ceiling(d / (Gelaende.Raster / 2)));
            for (var t = 1; t <= n; t++)
            {
                punkte.Add(Vector2.Lerp(zug[k - 1], zug[k], t / (float)n));
            }
        }
        var imax = (int)Math.Floor(breite / Gelaende.Raster) - 1;
        var jmax = (int)Math.Floor(hoehe / Gelaende.Raster) - 1;
        // jeder Rasterpunkt wird je Zug hoechstens einmal getroffen (sonst haengt die Wirkung vom Tempo des Fingers ab)
        var getroffen = new Dictionary<(int, int), float>();
        foreach (var p in punkte)
        {
            var r = (int)MathF.Ceiling(groesse / Gelaende.Raster);
            var (ci, cj) = ((int)MathF.Round(p.X / Gelaende.Raster), (int)MathF.Round(p.Y / Gelaende.Raster));
            for (var i = ci - r; i <= ci + r; i++)
            {
                for (var j = cj - r; j <= cj + r; j++)
                {
                    if (i < 1 || j < 1 || i > imax || j > jmax)
                    {
                        continue;
                    }
                    var d = Vector2.Distance(p, new Vector2(i * Gelaende.Raster, j * Gelaende.Raster));
                    if (d > groesse)
                    {
                        continue;
                    }
                    var w = (1f + MathF.Cos(MathF.PI * d / groesse)) / 2f;
                    getroffen[(i, j)] = MathF.Max(getroffen.GetValueOrDefault((i, j)), w);
                }
            }
        }
        var max = (int)MathF.Round(Gelaende.HoeheMax * 100);
        foreach (var ((i, j), w) in getroffen)
        {
            var alt = vorher.GetValueOrDefault((i, j));
            int neu;
            if (art == PinselArt.Glaetten)
            {
                var summe = 0;
                for (var a = -1; a <= 1; a++)
                {
                    for (var b = -1; b <= 1; b++)
                    {
                        summe += vorher.GetValueOrDefault((i + a, j + b));
                    }
                }
                neu = (int)MathF.Round(alt + ((summe / 9f) - alt) * w);
            }
            else
            {
                neu = alt + (int)MathF.Round((art == PinselArt.Heben ? 1 : -1) * PinselStaerke * 100 * w);
            }
            neu = Math.Clamp(neu, 0, max);
            if (neu == 0)
            {
                gemalt.Remove((i, j));
            }
            else
            {
                gemalt[(i, j)] = neu;
            }
        }
        var geaendert = gemalt.Count(e => vorher.GetValueOrDefault(e.Key) != e.Value) + vorher.Keys.Count(k => !gemalt.ContainsKey(k));
        if (geaendert == 0)
        {
            return 0;
        }
        var liste = gemalt.OrderBy(e => e.Key.I).ThenBy(e => e.Key.J)
            .Select(e => (object?)new List<object?> { (long)e.Key.I, (long)e.Key.J, (long)e.Value }).ToList();
        var g = GelaendeAnlegen(aussen);
        if (liste.Count == 0)
        {
            Weg(g, "gemalt");
            GelaendeAufraeumen(aussen);
        }
        else
        {
            Py.Setzen(g, "gemalt", liste);
        }
        return geaendert;
    }

    /// <summary>Gemalte Hoehen ([i, j, Hundertstel], leer ohne).</summary>
    public static List<object?> GemaltListe(PyObjekt aussen) => GelaendeDaten(aussen) is { } g ? Get(g, "gemalt") as List<object?> ?? [] : [];

    private static string HuegelBeschreibung(PyObjekt aussen, AussenStelle s, string lage)
    {
        var (radius, hoehe, laenglich, laenge, dreh) = HuegelWerte(aussen, s);
        return string.Create(De, $"{Texte.Huegel} Nr. {s.Nummer + 1}: {(laenglich ? Texte.HuegelLaenglich : Texte.HuegelRund)}, Radius {radius:0.##}, Höhe {hoehe:0.##}")
            + (laenglich ? string.Create(De, $", Länge {laenge:0.##}, {dreh} Grad") : "") + lage;
    }
}
