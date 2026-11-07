using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Ein Fussweg aus <c>stadtdaten.fusswege</c>: Eckpunkte wie gesetzt und der Wunschradius je Eckpunkt.</summary>
public sealed record Fussweg(IReadOnlyList<Vector2> Punkte, IReadOnlyList<float>? Radien)
{
    /// <summary>Der gezeichnete Linienzug (Ecken als Kreisboegen wie beim Fluss).</summary>
    public List<Vector2> Glatt { get; } = Kurve.Glatt(Punkte, Radien, Fusswege.Radius);
}

/// <summary>
/// Welle Z (A 0.48.0 / S 0.14.0, Weisung P12-71, Nico P12-67 Frage 4 = a): eigene Fusswege ohne Strasse, z. B. durch einen
/// Park oder zu einem Platz. Sie liegen in <c>stadtdaten.fusswege</c> (<c>[{punkte: [[x, y], …], radien: [r, …]}]</c>),
/// nicht in <c>karte.landschaft</c>: die Ladepruefung des Spiels (<c>SpielPruefung</c>, wie Python) kennt dort nur die
/// alten Arten, und Teiche, Stege und Katalog-Stuecke liegen aus demselben Grund in den Stadtdaten. Ein Fussweg ist nur
/// Darstellung und Laufraster: hell wie die Wege zu den Tueren, <see cref="Breite"/> breit, frei zum Gehen, Baeume und
/// Kleingruen halten Abstand; keine Autos, keine Mittellinie, keine Ereignisart, kein Feld im Spielstand. Ohne
/// <c>fusswege</c> (Fassung 0) aendert sich nichts.
/// </summary>
public static class Fusswege
{
    public const string Abschnitt = "fusswege";

    /// <summary>Breite eines Fusswegs (wie der Weg von der Tuer zur Strasse).</summary>
    public const float Breite = StadtSzene.WegBreite;

    /// <summary>Wunschradius einer Ecke ohne Feld <c>radien</c>.</summary>
    public const float Radius = 1f;

    /// <summary>Erlaubte Radien je Eckpunkt (wie die Kurven der Strassen).</summary>
    public static readonly IReadOnlyList<double> Radien = [1, 2, 3];

    /// <summary>
    /// Alle Fusswege der Stadtdaten. Nur gueltige Eintraege (die Pruefung des Werkzeugs meldet jeden anderen als roten
    /// Fund und sperrt damit den Export; ins Spiel kommt nur eine Fassung ohne Rot).
    /// </summary>
    public static List<Fussweg> Alle(PyObjekt? stadtDaten)
    {
        var liste = new List<Fussweg>();
        if (stadtDaten is null || Get(stadtDaten, Abschnitt) is not List<object?> eintraege)
        {
            return liste;
        }
        foreach (var e in eintraege)
        {
            if (Lesen(e) is { } f)
            {
                liste.Add(f);
            }
        }
        return liste;
    }

    /// <summary>Einen Eintrag lesen (null = falsch gespeichert: kein Objekt, weniger als zwei Punkte, keine Zahlen,
    /// <c>radien</c> nicht je Punkt eine Zahl).</summary>
    public static Fussweg? Lesen(object? eintrag)
    {
        if (eintrag is not PyObjekt o || Get(o, "punkte") is not List<object?> punkte || punkte.Count < 2)
        {
            return null;
        }
        var p = new List<Vector2>();
        foreach (var q in punkte)
        {
            if (q is not List<object?> { Count: 2 } xy || xy[0] is not (long or double) || xy[1] is not (long or double))
            {
                return null;
            }
            p.Add(new Vector2((float)Py.F(xy[0]), (float)Py.F(xy[1])));
        }
        List<float>? radien = null;
        if (Py.Hat(o, "radien"))
        {
            if (o["radien"] is not List<object?> r || r.Count != p.Count || r.Any(w => w is not (long or double)))
            {
                return null;
            }
            radien = [.. r.Select(w => (float)Py.F(w))];
        }
        return new Fussweg(p, radien);
    }
}
