using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W5 (Weisung P7-10): Wegpruefung und Vorschau beim Aufstellen wie <c>fisi_game.py</c>
/// (Python 0.63.2, Z. 13550-13700: _reachable, _without_rect, _path_targets, _unreachable,
/// path_check, path_warning, preview_text, placement_preview). Eine Sperrung wird nur gemeldet,
/// nie verboten. Vergleichswerte G49 (<c>spiel_orte.json</c>, Teil "wohnung").
/// </summary>
public static partial class Wohnung
{
    private static readonly (int, int)[] Nachbarn = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>_reachable: alle Zellen, die die Figur von start aus erreicht (wie walk_path).</summary>
    public static HashSet<(long, long)> Erreichbar(PyMenge frei, (double X, double Y) start)
    {
        if (Wege.NaechsteFrei(start.X, start.Y, frei) is not { } anfang)
        {
            return [];
        }
        var gesehen = new HashSet<(long, long)> { anfang };
        var schlange = new List<(long, long)> { anfang };
        for (var i = 0; i < schlange.Count; i++)
        {
            var (s, z) = schlange[i];
            foreach (var (ds, dz) in Nachbarn)
            {
                var naechste = (s + ds, z + dz);
                if (!frei.Contains(naechste) || gesehen.Contains(naechste))
                {
                    continue;
                }
                if (ds != 0 && dz != 0 && (!frei.Contains((s + ds, z)) || !frei.Contains((s, z + dz))))
                {
                    continue;
                }
                gesehen.Add(naechste);
                schlange.Add(naechste);
            }
        }
        return gesehen;
    }

    /// <summary>_without_rect: Laufraster, wenn zusaetzlich das Rechteck (x, y, w, h) im Weg steht.</summary>
    public static PyMenge OhneRechteck(PyMenge frei, double x, double y, double w, double h)
    {
        var gesperrt = new HashSet<(long, long)>();
        var reichweite = (long)(Wege.Koerper / Wege.Schritt) + 1;
        var (s0, z0) = Wege.Zelle(x, y);
        var (s1, z1) = Wege.Zelle(x + w, y + h);
        for (var s = s0 - reichweite; s <= s1 + reichweite; s++)
        {
            for (var z = z0 - reichweite; z <= z1 + reichweite; z++)
            {
                var (cx, cy) = Wege.Mitte((s, z));
                var dx = Math.Max(Math.Max(x - cx, 0), cx - x - w);
                var dy = Math.Max(Math.Max(y - cy, 0), cy - y - h);
                if (dx * dx + dy * dy < Wege.Koerper * Wege.Koerper)
                {
                    gesperrt.Add((s, z));
                }
            }
        }
        return frei.Ohne(gesperrt);
    }

    /// <summary>_path_targets: jeder Raum und jedes benutzbare Moebel (mit "aktion").</summary>
    public static List<PyObjekt> WegZiele(Spielstand stand, PyObjekt einrichtung)
    {
        var gebaeude = Grundriss(stand);
        var ziele = Liste(gebaeude["raeume"]).Select(Objekt).Select(r => new PyObjekt
        {
            { "art", "raum" }, { "name", r["name"] }, { "x", r["x"] }, { "y", r["y"] }, { "w", r["w"] }, { "h", r["h"] },
        }).ToList();
        foreach (var m in AufgestellteMoebel(stand, einrichtung))
        {
            var info = MoebelStueck(m.Moebel, stand.Inhalt);
            if (info is not null && Py.Wahr(Get(info, "aktion")))
            {
                ziele.Add(new PyObjekt
                {
                    { "art", "moebel" }, { "name", info["name"] }, { "stueck", m.Stueck }, { "x", m.X }, { "y", m.Y }, { "w", m.W }, { "h", m.H },
                });
            }
        }
        return ziele;
    }

    /// <summary>_unreachable: Ziele, die von start aus keine erreichbare Zelle trifft.</summary>
    public static List<PyObjekt> Unerreichbar(PyMenge frei, (double X, double Y) start, List<PyObjekt> ziele)
    {
        var zellen = Erreichbar(frei, start).Select(Wege.Mitte).ToList();
        var ergebnis = new List<PyObjekt>();
        foreach (var ziel in ziele)
        {
            double x = Py.F(ziel["x"]), y = Py.F(ziel["y"]), w = Py.F(ziel["w"]), h = Py.F(ziel["h"]);
            var raum = Py.Gleich(ziel["art"], "raum");
            var ok = zellen.Any(c => raum
                ? x <= c.Item1 && c.Item1 < x + w && y <= c.Item2 && c.Item2 < y + h
                : double.Hypot(Math.Max(Math.Max(x - c.Item1, 0), c.Item1 - x - w), Math.Max(Math.Max(y - c.Item2, 0), c.Item2 - y - h)) <= MoebelReichweite);
            if (!ok)
            {
                ergebnis.Add(ziel);
            }
        }
        return ergebnis;
    }

    private static (double, double) Startplatz(PyObjekt gebaeude)
    {
        var p = Liste(Objekt(gebaeude["flur"])["spieler"]);
        return (Py.F(p[0]), Py.F(p[1]));
    }

    /// <summary>path_check: Ziele, die die Figur in der aktuellen Einrichtung nicht erreicht (leer = alles erreichbar).</summary>
    public static List<PyObjekt> Wegpruefung(Spielstand stand)
    {
        var gebaeude = SpielOrte.WohnungsGrundriss(stand);
        return Unerreichbar(Wege.BasisRaster(gebaeude), Startplatz(gebaeude), WegZiele(stand, Einrichtung(stand)));
    }

    /// <summary>path_warning: Warnzeile zur Wegpruefung (leer = alles frei).</summary>
    public static string WegWarnung(List<PyObjekt> gesperrt)
    {
        if (gesperrt.Count == 0)
        {
            return "";
        }
        var namen = new List<string>();
        foreach (var ziel in gesperrt)
        {
            var name = Py.Str(ziel["name"]);
            if (!namen.Contains(name))
            {
                namen.Add(name);
            }
        }
        return "Achtung, Weg versperrt! Die Figur erreicht nicht mehr: "
            + (namen.Count > 1 ? string.Join(", ", namen.Take(namen.Count - 1)) + " und " + namen[^1] : namen[0]) + ".";
    }

    /// <summary>preview_text: (Text, Farbe) zur Vorschau: "gruen" passt, "gelb" versperrt einen Weg, "rot" geht nicht.</summary>
    public static (string Text, string Farbe) VorschauText(PyObjekt vorschau)
    {
        if (Py.Wahr(vorschau["problem"]))
        {
            return (Py.Str(vorschau["problem"]), "rot");
        }
        if (Py.Wahr(vorschau["gesperrt"]))
        {
            return (WegWarnung([.. Liste(vorschau["gesperrt"]).Select(Objekt)]) + " Aufstellen geht trotzdem.", "gelb");
        }
        return ("Passt. Klicken oder loslassen zum Aufstellen.", "gruen");
    }

    /// <summary>
    /// placement_preview: Vorschau beim Aufstellen {"x", "y", "w", "h", "dreh", "gesperrt", "problem"}.
    /// Speichert nichts.
    /// </summary>
    public static PyObjekt Vorschau(Spielstand stand, string stueck, double cx, double cy, long dreh)
    {
        var kennung = stand.Moebel.Get(stueck);
        var moebel = MoebelStueck(kennung, stand.Inhalt)!;
        var (w, h) = Groesse(moebel, dreh);
        var (x, y) = Andocken(stand, kennung, cx, cy, dreh);
        var ergebnis = new PyObjekt
        {
            { "x", x }, { "y", y }, { "w", w }, { "h", h }, { "dreh", ((dreh % 4) + 4) % 4 }, { "gesperrt", new List<object?>() },
            { "problem", PlatzProblem(stand, stueck, kennung, x, y, dreh) },
        };
        if (Py.Wahr(ergebnis["problem"]))
        {
            return ergebnis;
        }
        var einrichtung = Einrichtung(stand);
        var andere = new PyObjekt();
        foreach (var (k, v) in Objekt(einrichtung["moebel"]))
        {
            if (k != stueck)
            {
                andere.Add(k, v);
            }
        }
        var rest = new PyObjekt { { "moebel", andere }, { "boeden", einrichtung["boeden"] } };
        var karton = stand.Moebel.Keys.Count(k => !Py.Hat(andere, Py.Str(k)) && Py.Str(k) != stueck);
        var gebaeude = SpielOrte.WohnungsGrundriss(stand, rest, karton);
        var frei = Wege.BasisRaster(gebaeude);
        if (!Begehbar.Contains(Py.Str(moebel["typ"])))
        {
            frei = OhneRechteck(frei, Py.F(x), Py.F(y), Py.F(w), Py.F(h));
        }
        var mit = new PyObjekt();
        foreach (var (k, v) in andere)
        {
            mit.Add(k, v);
        }
        mit.Add(stueck, new List<object?> { x, y, ((dreh % 4) + 4) % 4 });
        Py.Setzen(rest, "moebel", mit);
        Py.Setzen(ergebnis, "gesperrt", Unerreichbar(frei, Startplatz(gebaeude), WegZiele(stand, rest)).Cast<object?>().ToList());
        return ergebnis;
    }
}
