using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Sammelliste 383 (Weisung P12-298, Nico): ein Moebel ist nur aus seinem Raum benutzbar. Nur C# (kein Nachbau von
/// Python): die Python-Regeln furniture_near und home_message bleiben unveraendert und bekommen in der Darstellung
/// (<c>StadtSpiel</c>) einen Filter „nicht hinter einer Wand“.
/// </summary>
public static partial class Wohnung
{
    /// <summary>Abstand der Stelle vor einem Moebel (Mitte der Figur) zu seiner Kante.</summary>
    public const double VorDemMoebel = 0.45;

    /// <summary>
    /// Liegt zwischen der Figur (<paramref name="x"/>, <paramref name="y"/>) und dem naechsten Punkt des Rechtecks
    /// (<paramref name="rx"/>, <paramref name="ry"/>, <paramref name="rw"/>, <paramref name="rh"/>) ein Wandstueck des
    /// Grundrisses (Tueren sind Luecken)? Dann ist das Moebel von dort nicht benutzbar.
    /// </summary>
    public static bool HinterWand(PyObjekt gebaeude, double rx, double ry, double rw, double rh, double x, double y)
    {
        var nx = Math.Clamp(x, rx, rx + rw);
        var ny = Math.Clamp(y, ry, ry + rh);
        const double rand = 1e-6;
        foreach (var (x1, y1, x2, y2) in Wege.Wandstuecke(gebaeude))
        {
            if (StreckeSchneidet(x, y, nx, ny, x1 + rand, y1 + rand, x2 - rand, y2 - rand))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Wie oben fuer ein aufgestelltes Moebel.</summary>
    public static bool HinterWand(PyObjekt gebaeude, Aufgestellt m, double x, double y) =>
        HinterWand(gebaeude, m.X, m.Y, Py.F(m.W), Py.F(m.H), x, y);

    /// <summary>Strecke (ax, ay)–(bx, by) schneidet das achsgleiche Rechteck (Liang-Barsky).</summary>
    private static bool StreckeSchneidet(double ax, double ay, double bx, double by, double x1, double y1, double x2, double y2)
    {
        if (x1 >= x2 || y1 >= y2)
        {
            return false;
        }
        double t0 = 0, t1 = 1, dx = bx - ax, dy = by - ay;
        foreach (var (p, q) in new[] { (-dx, ax - x1), (dx, x2 - ax), (-dy, ay - y1), (dy, y2 - ay) })
        {
            if (p == 0)
            {
                if (q < 0)
                {
                    return false;
                }
                continue;
            }
            var r = q / p;
            if (p < 0)
            {
                t0 = Math.Max(t0, r);
            }
            else
            {
                t1 = Math.Min(t1, r);
            }
            if (t0 > t1)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Richtung der Vorderseite eines Moebels (dreh 0 = nach +y, Vierteldrehungen im Uhrzeigersinn wie Rahmen).</summary>
    public static (double X, double Y) Vorn(long dreh) => (((dreh % 4) + 4) % 4) switch
    {
        1 => (-1, 0),
        2 => (0, -1),
        3 => (1, 0),
        _ => (0, 1),
    };

    /// <summary>
    /// Stellen, von denen aus die Figur ein Rechteck benutzt, je Seite eine Liste: zuerst die Vorderseite
    /// (<paramref name="vorn"/>, falls bekannt), dann die anderen Seiten. Je Seite der Reihe nach: Abstand
    /// <see cref="VorDemMoebel"/>, 0,3, 0,6, 0,75 von der Kante (alle in <see cref="MoebelReichweite"/>), je Abstand von der
    /// Seitenmitte aus in Schritten von 0,25 zu den Ecken. Ein begehbares Stueck (Teppich) hat davor eine Liste mit Stellen
    /// darauf. Nur Stellen im Haus, die nicht hinter einer Wand liegen.
    /// </summary>
    public static List<List<(double X, double Y)>> Standplaetze(PyObjekt gebaeude, double rx, double ry, double rw, double rh,
        (double X, double Y)? vorn, bool begehbar = false)
    {
        var (mx, my) = (rx + (rw / 2), ry + (rh / 2));
        var seiten = new List<(double X, double Y)> { (0, 1), (-1, 0), (0, -1), (1, 0) };
        if (vorn is { } v)
        {
            seiten.Remove(v);
            seiten.Insert(0, v);
        }
        var (breite, hoehe) = (Py.F(gebaeude["breite"]), Py.F(gebaeude["hoehe"]));
        bool Gut(double px, double py) => px > 0 && py > 0 && px < breite && py < hoehe && !HinterWand(gebaeude, rx, ry, rw, rh, px, py);
        static IEnumerable<double> Versatz(double halb)
        {
            yield return 0;
            for (var t = 0.25; t <= halb + 1e-9; t += 0.25)
            {
                yield return -t;
                yield return t;
            }
        }
        var alle = new List<List<(double X, double Y)>>();
        if (begehbar)
        {
            alle.Add([.. Versatz(rw / 2).SelectMany(dx => Versatz(rh / 2).Select(dy => (mx + dx, my + dy))).Where(p => Gut(p.Item1, p.Item2))]);
        }
        foreach (var (sx, sy) in seiten)
        {
            var stellen = new List<(double X, double Y)>();
            foreach (var abstand in new[] { VorDemMoebel, 0.3, 0.6, 0.75 })
            {
                foreach (var t in Versatz(sx != 0 ? rh / 2 : rw / 2))
                {
                    var (px, py) = sx != 0 ? (mx + (sx * ((rw / 2) + abstand)), my + t) : (mx + t, my + (sy * ((rh / 2) + abstand)));
                    if (Gut(px, py))
                    {
                        stellen.Add((px, py));
                    }
                }
            }
            alle.Add(stellen);
        }
        return alle;
    }

    /// <summary>Wie oben fuer ein aufgestelltes Moebel (Vorderseite aus seiner Drehung).</summary>
    public static List<List<(double X, double Y)>> Standplaetze(PyObjekt gebaeude, Aufgestellt m) =>
        Standplaetze(gebaeude, m.X, m.Y, Py.F(m.W), Py.F(m.H), Vorn(m.Dreh), Begehbar.Contains(m.Typ));

    /// <summary>Grundriss der eigenen Wohnung (Raeume, Flur) des Spielstands (Sammelliste 383).</summary>
    public static PyObjekt GrundrissVon(Spielstand stand) => Grundriss(stand);
}
