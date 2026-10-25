using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>Was die Gebaeudemodelle vom Spielstand wissen muessen (_map_context).</summary>
public sealed record ModellKontext(object? Stufe, bool Gegruendet, object? Filiale, PySet Raeume, object? Wohnung, PySet Geloest, bool FarmFertig);

/// <summary>
/// Phase 7, W4 (Weisung P7-9, Frage 1b aus P7-3): der Teil von building_shapes/world_shapes, den die
/// 3D-Stadt braucht, wie Python 0.63.2 (fisi_game.py Z. 14365-14710): welche Teile eines
/// Gebaeudemodells zu sehen sind (<c>_map_context</c>, <c>_part_visible</c>, <c>model_parts</c>) und
/// der Umriss je Teil und je Modell (<c>_part_box</c>, Umriss aus <c>model_shapes</c>, Feld "box" von
/// <c>map_places</c>). Vergleichswerte G48 Teil "Stadt" (Feld "boxen" in <c>spiel_stadt.json</c>).
/// Die 2D-Zeichenbefehle selbst (Farben, Fenster, Schatten) baut die C#-Linie nicht nach, die
/// Darstellung ist 3D (Nico 25.10.2026, Weisung P7-9).
/// Karteneinheiten wie orte.json: x nach rechts, y nach unten (in 3D: z zum Betrachter).
/// </summary>
public static class StadtModelle
{
    /// <summary>_map_context: ohne Spielstand Stufe 0, keine Firma, Startwohnung.</summary>
    public static ModellKontext Kontext(Spielstand? stand, PyObjekt inhalt)
    {
        if (stand is null)
        {
            return new(0L, false, 0L, [], Objekt(inhalt["wohnungen"])["start"], [], false);
        }
        var raeume = new PySet();
        foreach (var (schluessel, _) in stand.Raeume)
        {
            raeume.Add(schluessel);
        }
        return new(
            Py.Wahr(stand.Firma) ? Objekt(stand.Firma)["stufe"] : 0L,
            Py.Wahr(stand.Firma),
            Py.Wahr(stand.Zweigstelle) ? Objekt(stand.Zweigstelle)["stufe"] : 0L,
            raeume,
            stand.Wohnung,
            stand.Geloest,
            stand.Farm is PyObjekt farm && Py.Wahr(farm["fertig"]));
    }

    /// <summary>_part_visible: Bedingungen eines Teils (ab_stufe, wohnung, erledigt, gegruendet ...).</summary>
    public static bool Sichtbar(PyObjekt teil, ModellKontext k)
    {
        if (Py.Hat(teil, "ab_stufe") && Py.Kleiner(k.Stufe, teil["ab_stufe"]))
        {
            return false;
        }
        if (Py.Hat(teil, "bis_stufe") && Py.Kleiner(teil["bis_stufe"], k.Stufe))
        {
            return false;
        }
        if (Py.Hat(teil, "ab_filiale") && Py.Kleiner(k.Filiale, teil["ab_filiale"]))
        {
            return false;
        }
        if (Py.Hat(teil, "bis_filiale") && Py.Kleiner(teil["bis_filiale"], k.Filiale))
        {
            return false;
        }
        if (Py.Hat(teil, "raum") && !k.Raeume.Contains(teil["raum"]))
        {
            return false;
        }
        if (Py.Hat(teil, "wohnung") && !Liste(teil["wohnung"]).Any(w => Py.Gleich(w, k.Wohnung)))
        {
            return false;
        }
        if (Py.Hat(teil, "erledigt") && !k.Geloest.Contains(teil["erledigt"]))
        {
            return false;
        }
        if (Py.Hat(teil, "offen") && k.Geloest.Contains(teil["offen"]))
        {
            return false;
        }
        if (Py.Hat(teil, "gegruendet") && Py.Wahr(teil["gegruendet"]) != k.Gegruendet)
        {
            return false;
        }
        if (Py.Hat(teil, "serverfarm_fertig") && Py.Wahr(teil["serverfarm_fertig"]) != k.FarmFertig)
        {
            return false;
        }
        return true;
    }

    /// <summary>Alle Modelle: die aus orte.json, dazu die neuen aus stadt.json (neue_modelle).</summary>
    public static PyObjekt Modelle(PyObjekt inhalt, PyObjekt? stadt)
    {
        var alle = new PyObjekt();
        foreach (var (id, teile) in Objekt(Objekt(inhalt["orte"])["modelle"]))
        {
            Py.Setzen(alle, id, teile);
        }
        if (stadt is not null && Py.Hat(stadt, "neue_modelle"))
        {
            foreach (var (id, teile) in Objekt(stadt["neue_modelle"]))
            {
                Py.Setzen(alle, id, teile);
            }
        }
        return alle;
    }

    /// <summary>model_parts: die sichtbaren Teile mit ihrer Stelle im Modell (Python zaehlt sie im Vergleich mit).</summary>
    public static List<(int Nr, PyObjekt Teil)> Teile(PyObjekt modelle, string modell, ModellKontext k)
    {
        var liste = Py.Hat(modelle, modell) ? Liste(modelle[modell]) : [];
        var sichtbar = new List<(int, PyObjekt)>();
        for (var i = 0; i < liste.Count; i++)
        {
            var teil = Objekt(liste[i]);
            if (Sichtbar(teil, k))
            {
                sichtbar.Add((i, teil));
            }
        }
        return sichtbar;
    }

    private static double Zahl(PyObjekt teil, string name, double vorgabe = 0) => Py.Hat(teil, name) ? Py.F(teil[name]) : vorgabe;

    /// <summary>_part_box: Umriss (x1, y1, x2, y2) eines Teils an (ox, oy); Rechenfolge wie Python.</summary>
    public static (double X1, double Y1, double X2, double Y2) TeilBox(PyObjekt teil, double ox, double oy)
    {
        var x = ox + Zahl(teil, "x");
        var y = oy + Zahl(teil, "y");
        var art = Py.Str(teil["art"]);
        switch (art)
        {
            case "block":
            {
                var h = Py.F(teil["h"]);
                return (x, y - h, x + Py.F(teil["w"]) + (h * 0.35), y + Py.F(teil["t"]));
            }
            case "kreis" or "baum":
            {
                var r = Zahl(teil, "r", 0.4);
                return (x - r, y - r, x + r, y + r);
            }
            case "mast":
                return (x - 0.12, y - Py.F(teil["h"]) - 0.12, x + 0.12, y);
            case "schild":
                return (x, y, x + Py.F(teil["w"]), y + 0.28);
            default:
                return (x, y, x + Py.F(teil["w"]), y + Py.F(teil["h"]));
        }
    }

    /// <summary>Umriss aus model_shapes: alle sichtbaren Teile ausser Baeumen; ohne Teil (ox, oy, ox + 1, oy + 1).</summary>
    public static (double X1, double Y1, double X2, double Y2) Box(IEnumerable<(int Nr, PyObjekt Teil)> teile, double ox, double oy)
    {
        (double X1, double Y1, double X2, double Y2)? box = null;
        foreach (var (_, teil) in teile)
        {
            if (Py.Gleich(teil["art"], "baum"))
            {
                continue;
            }
            var b = TeilBox(teil, ox, oy);
            box = box is not { } v ? b : (Math.Min(v.X1, b.X1), Math.Min(v.Y1, b.Y1), Math.Max(v.X2, b.X2), Math.Max(v.Y2, b.Y2));
        }
        return box ?? (ox, oy, ox + 1, oy + 1);
    }
}
