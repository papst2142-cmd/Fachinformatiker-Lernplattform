using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Sammelliste 280 (A 0.59.1, Weisung P12-164, Nico: „Warum hat der Gehweg keine Kurven wie alle anderen Straßen, Flüsse,
/// Schienen?“): der Weg von einer Tuer zur Strasse (Zugang <c>zugaenge_neu</c> bzw. Zierweg) mit dem Feld <c>radien</c> (je Punkt
/// ein Radius, wie bei Fluss, Bahn und Fussweg) wird als EIN Band mit Gehrung und runden Ecken gezeichnet (<see cref="Streifen"/>,
/// wie die Fusswege), statt Stueck fuer Stueck als Rechtecke, die sich an jedem Knick ueberlagern. Ohne Feld (Fassung 0, Wege
/// aelterer Projekte) bleibt alles wie bisher: Wahl E1 „neues Merkmal“, damit Fassung 0, Fingerabdruecke, Laufraster und Tueren
/// bitgleich bleiben (das Laufraster nutzt weiter die Eckpunkte des Wegs). Nur Darstellung.
/// </summary>
public static class StadtTuerweg
{
    /// <summary>Wunschradius einer Ecke (das Werkzeug schreibt ihn; das Spiel nimmt an jeder Ecke, was Platz hat).</summary>
    public const float Radius = 0.5f;

    /// <summary>Gegenprobe: Wege mit <c>radien</c> wie bis A 0.59.0 Stueck fuer Stueck als Rechtecke (nur Tests, je Ablauf).</summary>
    public static bool AltFuerTests { get => Alt.Value; set => Alt.Value = value; }
    private static readonly AsyncLocal<bool> Alt = new();

    /// <summary>
    /// Der gezeichnete Linienzug des Wegs (runde Ecken, Enden um die halbe Breite verlaengert wie bisher), oder null ohne
    /// gueltiges Feld <c>radien</c> (dann zeichnet das Spiel wie bisher). Eine Ecke, an der der Bogen nicht mindestens die halbe
    /// Breite als Radius bekommt (kurzes Nachbarstueck), bleibt eine Gehrung ohne Bogen: sonst kreuzte sich die Innenkante.
    /// </summary>
    public static List<Vector2>? Gezeichnet(object? zugang, float breite)
    {
        if (Alt.Value || zugang is not PyObjekt z || Get(z, "radien") is not List<object?> r || Get(z, "weg") is not List<object?> w
            || r.Count != w.Count || w.Count < 2 || r.Any(x => x is not (long or double)) || w.Any(q => q is not List<object?> { Count: 2 } xy || xy[0] is not (long or double) || xy[1] is not (long or double)))
        {
            return null;
        }
        var p = w.Select(q => (List<object?>)q!).Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1]))).ToList();
        var radien = r.Select(x => (float)Py.F(x)).ToList();
        // erst nach Abzug doppelter Punkte zaehlen (wie Kurve.Glatt)
        var boegen = Kurve.Boegen(p, radien, Radius);
        var halb = breite / 2;
        for (var i = 1; i + 1 < p.Count; i++)
        {
            if (boegen.FirstOrDefault(b => Vector2.DistanceSquared(b.Ecke, p[i]) < 1e-10f) is { } bogen && bogen.Radius < halb - 1e-4f)
            {
                radien[i] = 0;
            }
        }
        var glatt = Kurve.Glatt(p, radien, Radius);
        if (glatt.Count < 2)
        {
            return null;
        }
        // Enden wie bisher um die halbe Breite verlaengert (in das Haus und in die Strasse hinein)
        var a = Vector2.Normalize(glatt[1] - glatt[0]);
        var e = Vector2.Normalize(glatt[^1] - glatt[^2]);
        glatt[0] -= a * halb;
        glatt[^1] += e * halb;
        return glatt;
    }
}
