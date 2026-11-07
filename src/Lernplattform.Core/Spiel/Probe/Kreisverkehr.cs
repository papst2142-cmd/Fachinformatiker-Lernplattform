using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle K3 (A 0.45.0 / S 0.11.0, Weisungen P12-58/P12-59, Kurzplan Meldung P12-37 Abschnitt 5, Nico 1 = a, 2 = a): ein
/// Kreisverkehr ist der Ring als Strasse in <c>karte.welt.landschaft</c> (ein geschlossener Linienzug, ein Punkt alle
/// <see cref="SchrittGrad"/> Grad, der letzte gleich dem ersten) mit dem Feld <c>kreisverkehr</c> (<c>{"radius": 2 | 3}</c>,
/// Mitte der Fahrbahn). Als Strasse kennen ihn Laufraster, Laternen, Kreuzungsregel und Mittellinie wie jede andere
/// Strasse; dazu zeichnet das Spiel die Mittelinsel (Rasen mit hellem Rand und Baum, nicht begehbar). Zufahrten sind
/// gerade Strassen, deren Ende auf dem Ring liegt und die genau auf die Mitte zielen. Keine neue Art, keine neue
/// Formatnummer, keine neue Ereignisart; Fassung 0 hat keinen Kreisverkehr.
/// </summary>
public static class Kreisverkehr
{
    /// <summary>Feld der Ring-Strasse.</summary>
    public const string Feld = "kreisverkehr";

    /// <summary>Ein Punkt des Rings alle so viel Grad (wie die Kurven).</summary>
    public const double SchrittGrad = 7.5;

    /// <summary>Erlaubte Radien (Mitte der Fahrbahn), Nico Frage 1 = a.</summary>
    public static readonly IReadOnlyList<double> Radien = [2, 3];

    /// <summary>Breite des hellen Rands der Mittelinsel (Bordstein).</summary>
    public const float RandBreite = 0.08f;

    /// <summary>Hoehe des Rands und des Rasens der Insel (ueber dem Gehweg, unter der Fahrbahn).</summary>
    public const float RandHoehe = 0.055f;

    public const float RasenHoehe = 0.056f;

    /// <summary>Krone des Baums auf der Insel (wie ein Baum der Landschaft ohne Angabe).</summary>
    public const float BaumRadius = 0.45f;

    /// <summary>Zufahrten je Kreisverkehr.</summary>
    public const int ZufahrtenMindestens = 3;

    public const int ZufahrtenHoechstens = 4;

    /// <summary>Kleinster Winkel zwischen zwei Zufahrten (Grad).</summary>
    public const double ZufahrtenAbstandGrad = 60;

    /// <summary>So weit darf eine Zufahrt von der Richtung zur Mitte abweichen (Grad).</summary>
    public const double ZielGrad = 2;

    /// <summary>So nah am Ring (Mittellinie) zaehlt ein Strassenende als Zufahrt.</summary>
    public const float AmRing = 0.05f;

    /// <summary>Hat die Strasse das Feld <see cref="Feld"/>?</summary>
    public static bool IstRing(PyObjekt strasse) => Py.Gleich(strasse["typ"], "strasse") && Py.Hat(strasse, Feld);

    /// <summary>Radius aus dem Feld (null = kein gueltiger Radius aus <see cref="Radien"/>).</summary>
    public static double? Radius(PyObjekt strasse) =>
        IstRing(strasse) && strasse[Feld] is PyObjekt k && k["radius"] is long or double && Radien.Any(r => Math.Abs(r - Py.F(k["radius"])) < 1e-9)
            ? Py.F(k["radius"]) : null;

    /// <summary>Die Punkte des Rings um <paramref name="mitte"/>: 49 Punkte, der erste rechts der Mitte, der letzte gleich dem ersten.</summary>
    public static List<Vector2> Ring(Vector2 mitte, double radius)
    {
        var n = (int)Math.Round(360 / SchrittGrad);
        var p = new List<Vector2>();
        for (var k = 0; k < n; k++)
        {
            var t = k * SchrittGrad * Math.PI / 180;
            p.Add(mitte + new Vector2((float)(radius * Math.Cos(t)), (float)(radius * Math.Sin(t))));
        }
        p.Add(p[0]);
        return p;
    }

    /// <summary>Geschlossener Linienzug (mindestens 4 Punkte, letzter = erster)?</summary>
    public static bool Geschlossen(IReadOnlyList<Vector2> p) => p.Count >= 4 && Vector2.Distance(p[0], p[^1]) < 1e-4f;

    /// <summary>Mitte des Rings (Schwerpunkt der Punkte ohne den doppelten letzten).</summary>
    public static Vector2 Mitte(IReadOnlyList<Vector2> ring)
    {
        var n = Geschlossen(ring) ? ring.Count - 1 : ring.Count;
        var s = Vector2.Zero;
        for (var i = 0; i < n; i++)
        {
            s += ring[i];
        }
        return s / n;
    }

    /// <summary>Stimmen die Punkte mit dem Radius (ein Kreis wie <see cref="Ring"/>, auf 0,02 genau)?</summary>
    public static bool Rund(IReadOnlyList<Vector2> ring, double radius)
    {
        if (!Geschlossen(ring) || ring.Count != (int)Math.Round(360 / SchrittGrad) + 1)
        {
            return false;
        }
        var m = Mitte(ring);
        return ring.All(q => Math.Abs(Vector2.Distance(q, m) - radius) <= 0.02);
    }

    /// <summary>Halbmesser der Mittelinsel (bis zum inneren Rand der Fahrbahn).</summary>
    public static float InselRadius(double radius) => (float)radius - (StadtSzene.StrassenBreite / 2);

    /// <summary>Kreis als Vieleck (gegen den Uhrzeigersinn, 48 Ecken) fuer Insel und Rand.</summary>
    public static List<Vector2> Kreis(Vector2 mitte, float radius) => Ring(mitte, radius).Take((int)Math.Round(360 / SchrittGrad)).ToList();

    /// <summary>
    /// Die Zufahrten eines Rings: Strassen (Stelle in <paramref name="strassen"/>), deren Anfang oder Ende bis
    /// <see cref="AmRing"/> an der Mittellinie des Rings liegt, mit der Richtung von der Mitte zum Ende (Grad, 0 bis 360).
    /// </summary>
    public static List<(int Strasse, bool AmAnfang, double Grad)> Zufahrten(IReadOnlyList<Vector2> ring, IReadOnlyList<List<Vector2>> strassen, int eigene)
    {
        var m = Mitte(ring);
        var liste = new List<(int, bool, double)>();
        for (var k = 0; k < strassen.Count; k++)
        {
            var s = strassen[k];
            if (k == eigene || s.Count < 2)
            {
                continue;
            }
            foreach (var (ende, anfang) in new[] { (s[0], true), (s[^1], false) })
            {
                if (Kurve.Abstand(ende, ring) <= AmRing)
                {
                    var d = ende - m;
                    var grad = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
                    // auf 0,001 Grad gerundet, damit eine Zufahrt genau rechts 0 heisst und nicht 359,99…
                    liste.Add((k, anfang, Math.Round(grad < 0 ? grad + 360 : grad, 3) % 360));
                }
            }
        }
        return liste;
    }

    /// <summary>Weicht die Zufahrt (gerade, Ende auf dem Ring) um mehr als <see cref="ZielGrad"/> von der Richtung zur Mitte ab?</summary>
    public static bool ZieltAufMitte(IReadOnlyList<Vector2> zufahrt, bool amAnfang, Vector2 mitte)
    {
        if (zufahrt.Count != 2)
        {
            return false;
        }
        var (ende, anderes) = amAnfang ? (zufahrt[0], zufahrt[1]) : (zufahrt[1], zufahrt[0]);
        var hin = ende - anderes;
        var zur = mitte - ende;
        if (hin.LengthSquared() < 1e-10f || zur.LengthSquared() < 1e-10f)
        {
            return false;
        }
        return Vector2.Dot(Vector2.Normalize(hin), Vector2.Normalize(zur)) >= Math.Cos(ZielGrad * Math.PI / 180);
    }

    /// <summary>Punkte einer Strasse als Linienzug (Zahlen wie das Spiel als float).</summary>
    public static List<Vector2> Linie(object? punkte) =>
        [.. Liste(punkte).Select(p => Liste(p)).Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])))];

    /// <summary>Alle Ringe einer Landschaft: (Linienzug, Radius) mit gueltigem Radius und runden Punkten.</summary>
    public static List<(List<Vector2> Ring, double Radius)> Alle(IEnumerable<object?> landschaft)
    {
        var liste = new List<(List<Vector2>, double)>();
        foreach (var l in landschaft.OfType<PyObjekt>())
        {
            if (Radius(l) is { } r && Linie(l["punkte"]) is var p && Rund(p, r))
            {
                liste.Add((p, r));
            }
        }
        return liste;
    }
}
