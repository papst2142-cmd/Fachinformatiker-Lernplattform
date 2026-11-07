using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle K3 (S 0.11.0, Weisungen P12-58/P12-59, Kurzplan P12-37 Abschnitt 5, Nico 1 = a, 2 = a): Kreisverkehr setzen,
/// schieben, drehen und kopieren. Gesetzt wird er als <see cref="AussenArt.Kreisverkehr"/> mit der Kennung
/// <see cref="Kennung"/>; danach ist der Ring eine <see cref="AussenArt.Strasse"/> mit dem Feld <c>kreisverkehr</c>
/// (<see cref="Kreisverkehr"/>). Beim Setzen kommen vier gerade Zufahrten (0°, 90°, 180°, 270°, je
/// <see cref="ZufahrtLaenge"/> ueber den Gehweg hinaus) und vier Laternen am Aussenrand zwischen den Zufahrten dazu.
/// Schieben, Drehen und Kopieren nehmen Zufahrten (ganze Strassen mit einem Ende auf dem Ring) und die Laternen am Ring
/// mit; Loeschen nimmt nur den Ring (die Zufahrten bleiben als Strassen stehen, Rueckgaengig holt ihn zurueck).
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>So weit reicht eine neue Zufahrt ueber den Gehweg des Rings hinaus.</summary>
    public const double ZufahrtLaenge = 1.5;

    /// <summary>Kennung fuer das Setzen: „kreisverkehr:2“ oder „kreisverkehr:3“.</summary>
    public static string Kennung(double radius) => string.Create(CultureInfo.InvariantCulture, $"kreisverkehr:{radius}");

    /// <summary>Radius aus der Kennung (null = keine gueltige Kennung).</summary>
    public static double? KreisverkehrRadius(string? kennung)
    {
        var teile = (kennung ?? "").Split(':');
        return teile.Length == 2 && teile[0] == "kreisverkehr" && double.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
            && Kreisverkehr.Radien.Any(e => Math.Abs(e - r) < 1e-9) ? r : null;
    }

    /// <summary>Ist das Stueck ein Kreisverkehr (Strasse mit Feld <c>kreisverkehr</c>)?</summary>
    public static bool IstKreisverkehr(PyObjekt aussen, AussenStelle s) =>
        s.Art == AussenArt.Strasse && Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is PyObjekt l && Kreisverkehr.IstRing(l);

    /// <summary>Radius des gewaehlten Kreisverkehrs, sonst null.</summary>
    public static double? KreisverkehrVon(PyObjekt aussen, AussenStelle s) =>
        IstKreisverkehr(aussen, s) ? Kreisverkehr.Radius(Objekt(Aussen.Landschaft(aussen)[s.Nummer])) : null;

    /// <summary>Aussenmass (Ring samt Gehweg) um die Mitte.</summary>
    private static double KreisAussen(double radius) => radius + (StadtSzene.StrassenBreite / 2) + NeuerGehweg;

    /// <summary>Neuer Kreisverkehr mit der Mitte bei (x, y) auf dem Raster 0,5, ganz auf der Karte (sonst null).</summary>
    private static AussenStelle? KreisverkehrSetzen(PyObjekt aussen, double x, double y, string? kennung)
    {
        if (KreisverkehrRadius(kennung) is not { } radius)
        {
            return null;
        }
        var (breite, hoehe) = Aussen.Groesse(aussen);
        var weit = KreisAussen(radius) + ZufahrtLaenge;
        if (2 * KreisAussen(radius) > Math.Min(breite, hoehe))
        {
            return null;
        }
        // ganz auf die Karte: der Ring samt Gehweg; Zufahrten werden am Rand gekuerzt
        var aussenMass = KreisAussen(radius);
        var mx = Math.Clamp(Bearbeitung.AufRaster(x, Bearbeitung.Raster), Math.Ceiling(aussenMass / Bearbeitung.Raster) * Bearbeitung.Raster,
            breite - (Math.Ceiling(aussenMass / Bearbeitung.Raster) * Bearbeitung.Raster));
        var my = Math.Clamp(Bearbeitung.AufRaster(y, Bearbeitung.Raster), Math.Ceiling(aussenMass / Bearbeitung.Raster) * Bearbeitung.Raster,
            hoehe - (Math.Ceiling(aussenMass / Bearbeitung.Raster) * Bearbeitung.Raster));
        var m = new Vector2((float)mx, (float)my);
        var land = Aussen.Landschaft(aussen);
        land.Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?>(Kreisverkehr.Ring(m, radius).Select(p => (object?)PunktObjekt(p))) },
            { "gehweg", Bearbeitung.Zahl(NeuerGehweg) },
            { Kreisverkehr.Feld, new PyObjekt { { "radius", Bearbeitung.Zahl(radius) } } },
        });
        var ring = new AussenStelle(AussenArt.Strasse, land.Count - 1);
        foreach (var grad in new[] { 0.0, 90, 180, 270 })
        {
            var d = new Vector2((float)Math.Cos(grad * Math.PI / 180), (float)Math.Sin(grad * Math.PI / 180));
            var innen = m + (d * (float)radius);
            // am Kartenrand gekuerzt (mindestens bis an den Gehweg)
            var laenge = (float)weit;
            var aussen1 = m + (d * laenge);
            aussen1 = new Vector2(Math.Clamp(aussen1.X, 0, (float)breite), Math.Clamp(aussen1.Y, 0, (float)hoehe));
            land.Add(new PyObjekt
            {
                { "typ", "strasse" },
                { "punkte", new List<object?> { PunktObjekt(aussen1), PunktObjekt(innen) } },
                { "gehweg", Bearbeitung.Zahl(NeuerGehweg) },
            });
        }
        // Laternen am Aussenrand zwischen den Zufahrten, Nachtlicht.Rand neben der Mitte eines Ringstuecks (nicht vor einem
        // Eckpunkt: dort faellt das Lot auf kein Stueck des Rings, und das Spiel suchte die Strassenseite gegenueber)
        var halbSchritt = Kreisverkehr.SchrittGrad / 2;
        foreach (var grad in new[] { 45 + halbSchritt, 135 + halbSchritt, 225 + halbSchritt, 315 + halbSchritt })
        {
            var d = new Vector2((float)Math.Cos(grad * Math.PI / 180), (float)Math.Sin(grad * Math.PI / 180));
            var sehne = radius * Math.Cos(halbSchritt * Math.PI / 180);
            var p = m + (d * (float)(sehne + Nachtlicht.Rand));
            Aussen.Laternen(aussen).Add(new List<object?> { Bearbeitung.Zahl(p.X), Bearbeitung.Zahl(p.Y), false });
        }
        return ring;
    }

    /// <summary>Was zum Kreisverkehr gehoert: die Zufahrten (Stellen in der Landschaft) und die Laternen am Ring.</summary>
    private static (List<int> Zufahrten, List<int> Laternen) KreisverkehrTeile(PyObjekt aussen, AussenStelle s)
    {
        var land = Aussen.Landschaft(aussen);
        var ringDaten = Objekt(land[s.Nummer]);
        var ring = Linie(ringDaten["punkte"]);
        var m = Kreisverkehr.Mitte(ring);
        var radius = Kreisverkehr.Radius(ringDaten) ?? Vector2.Distance(ring[0], m);
        var zufahrten = new List<int>();
        for (var i = 0; i < land.Count; i++)
        {
            if (i == s.Nummer || land[i] is not PyObjekt l || !Py.Gleich(l["typ"], "strasse"))
            {
                continue;
            }
            var p = Linie(l["punkte"]);
            if (p.Count >= 2 && (Kurve.Abstand(p[0], ring) <= Kreisverkehr.AmRing || Kurve.Abstand(p[^1], ring) <= Kreisverkehr.AmRing))
            {
                zufahrten.Add(i);
            }
        }
        var laternen = new List<int>();
        var lat = Aussen.Laternen(aussen);
        for (var i = 0; i < lat.Count; i++)
        {
            var q = Liste(lat[i]);
            var d = Vector2.Distance(new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])), m);
            if (d <= radius + Nachtlicht.Rand + 0.3)
            {
                laternen.Add(i);
            }
        }
        return (zufahrten, laternen);
    }

    /// <summary>Zufahrten und Laternen eines Kreisverkehrs mit verschieben (der Ring selbst ist schon verschoben).</summary>
    private static void KreisverkehrMitnehmen(PyObjekt aussen, (List<int> Zufahrten, List<int> Laternen) teile, Func<Vector2, Vector2> abbilden)
    {
        var land = Aussen.Landschaft(aussen);
        foreach (var i in teile.Zufahrten)
        {
            var punkte = Liste(Objekt(land[i])["punkte"]);
            var linie = Linie(punkte);
            for (var k = 0; k < punkte.Count; k++)
            {
                punkte[k] = PunktObjekt(abbilden(linie[k]));
            }
        }
        foreach (var i in teile.Laternen)
        {
            var q = Liste(Aussen.Laternen(aussen)[i]);
            var p = abbilden(new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])));
            q[0] = Bearbeitung.Zahl(p.X);
            q[1] = Bearbeitung.Zahl(p.Y);
        }
    }

    /// <summary>Kopie des Kreisverkehrs mit Zufahrten und Laternen rechts daneben.</summary>
    private static AussenStelle KreisverkehrKopieren(PyObjekt aussen, AussenStelle s)
    {
        var land = Aussen.Landschaft(aussen);
        var teile = KreisverkehrTeile(aussen, s);
        var radius = KreisverkehrVon(aussen, s) ?? 2;
        var versatz = new Vector2((float)(2 * (KreisAussen(radius) + ZufahrtLaenge)), 0);
        var kopie = (PyObjekt)Py.Kopie(land[s.Nummer])!;
        var punkte = Liste(kopie["punkte"]);
        var linie = Linie(punkte);
        for (var k = 0; k < punkte.Count; k++)
        {
            punkte[k] = PunktObjekt(linie[k] + versatz);
        }
        land.Add(kopie);
        var neu = new AussenStelle(AussenArt.Strasse, land.Count - 1);
        foreach (var i in teile.Zufahrten)
        {
            var z = (PyObjekt)Py.Kopie(land[i])!;
            var zp = Liste(z["punkte"]);
            var zl = Linie(zp);
            for (var k = 0; k < zp.Count; k++)
            {
                zp[k] = PunktObjekt(zl[k] + versatz);
            }
            land.Add(z);
        }
        foreach (var i in teile.Laternen)
        {
            var q = Liste(Aussen.Laternen(aussen)[i]);
            Aussen.Laternen(aussen).Add(new List<object?> { Bearbeitung.Zahl(Py.F(q[0]) + versatz.X), Bearbeitung.Zahl(Py.F(q[1]) + versatz.Y), q[2] });
        }
        return neu;
    }
}
