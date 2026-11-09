using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Ein gelegter Zebrastreifen: Mitte auf der Strassenmitte, Richtung laengs der Strasse (Laenge 1).</summary>
public sealed record Zebra(int Nr, Vector2 Mitte, Vector2 Richtung)
{
    /// <summary>Quer zur Strasse (Laenge 1).</summary>
    public Vector2 Quer => new(-Richtung.Y, Richtung.X);

    /// <summary>
    /// Liegt <paramref name="q"/> auf dem Zebrastreifen? <paramref name="laengs"/> und <paramref name="quer"/> verbreitern
    /// ihn (laengs der Strasse bzw. ueber den Fahrbahnrand hinaus).
    /// </summary>
    public bool Drauf(Vector2 q, float laengs = 0f, float quer = 0f)
    {
        var r = q - Mitte;
        return MathF.Abs(Vector2.Dot(r, Richtung)) <= (Zebrastreifen.Breite / 2) + laengs
            && MathF.Abs(Vector2.Dot(r, Quer)) <= (StadtSzene.StrassenBreite / 2) + quer;
    }
}

/// <summary>
/// Welle A 0.54.0 / S 0.26.0 (Weisung P12-115, Meldung P12-75 Abschnitt 4, Sammelliste 255): Zebrastreifen, die Nico im
/// Werkzeug setzt. Daten <c>stadtdaten.zebrastreifen</c> = <c>[{x, y}, …]</c> (Mitte auf der Strassenmitte); die Richtung
/// kommt aus der Strasse (naechste gerade Strasse, <see cref="Legen"/>). Nur Darstellung und Laufraster: weisse Balken als
/// Flaeche im Stadtnetz (kein neuer Zeichenaufruf), die Fahrbahn einer Strasse mit Gehweg ist dort frei zum Gehen, Autos
/// halten davor, solange die Figur darauf steht. Keine Ereignisart, kein Feld im Spielstand. Ohne den Abschnitt (Fassung 0)
/// aendert sich nichts.
/// </summary>
public static class Zebrastreifen
{
    public const string Abschnitt = "zebrastreifen";

    /// <summary>Breite laengs der Strasse.</summary>
    public const float Breite = 0.6f;

    /// <summary>So weit darf ein gesetzter Punkt von der Strassenmitte weg sein (Werkzeug: Einrasten).</summary>
    public const float Fang = 0.95f;

    /// <summary>Zahl der weissen Balken quer ueber die Fahrbahn.</summary>
    public const int Balken = 5;

    /// <summary>Breite eines Balkens (quer zur Strasse).</summary>
    public const float BalkenBreite = 0.09f;

    /// <summary>
    /// Gespeicherte Punkte (null = kein Abschnitt). Ein falsch gespeicherter Eintrag (kein Objekt mit Zahlen x und y) ist
    /// null in der Liste; die Pruefung des Werkzeugs meldet ihn rot, die Szene laesst ihn weg.
    /// </summary>
    public static List<Vector2?>? Punkte(PyObjekt? stadtDaten)
    {
        if (stadtDaten is null || !Py.Hat(stadtDaten, Abschnitt))
        {
            return null;
        }
        if (stadtDaten[Abschnitt] is not List<object?> liste)
        {
            return [null];
        }
        return [.. liste.Select(Lesen)];
    }

    /// <summary>Einen Eintrag lesen (null = falsch gespeichert).</summary>
    public static Vector2? Lesen(object? eintrag) =>
        eintrag is PyObjekt o && Get(o, "x") is long or double && Get(o, "y") is long or double
            ? new Vector2((float)Py.F(o["x"]), (float)Py.F(o["y"]))
            : null;

    /// <summary>
    /// Den Punkt quer auf die naechste gerade Strasse legen (Lotfuss auf ihrer Mitte, nur innerhalb des Stuecks, bis
    /// <paramref name="fang"/>); null = keine gerade Strasse in der Naehe. <paramref name="strassen"/> = alle Strassen.
    /// </summary>
    public static Zebra? Legen(int nr, Vector2 p, IReadOnlyList<IReadOnlyList<Vector2>> strassen, float fang = Fang)
    {
        Zebra? beste = null;
        var abstand = float.MaxValue;
        foreach (var s in strassen)
        {
            if (s.Count != 2)
            {
                continue;
            }
            var d = s[1] - s[0];
            var l = d.Length();
            if (l < 1e-4f)
            {
                continue;
            }
            d /= l;
            var t = Vector2.Dot(p - s[0], d);
            if (t < 0 || t > l)
            {
                continue;
            }
            var fuss = s[0] + (d * t);
            var a = Vector2.Distance(p, fuss);
            if (a <= fang && a < abstand)
            {
                (beste, abstand) = (new Zebra(nr, fuss, d), a);
            }
        }
        return beste;
    }

    /// <summary>Alle Zebrastreifen der Stadtdaten, die auf einer geraden Strasse liegen (in der Reihenfolge der Daten).</summary>
    public static List<Zebra> Alle(PyObjekt? stadtDaten, IReadOnlyList<IReadOnlyList<Vector2>> strassen)
    {
        var liste = new List<Zebra>();
        var punkte = Punkte(stadtDaten) ?? [];
        for (var i = 0; i < punkte.Count; i++)
        {
            if (punkte[i] is { } p && Legen(i + 1, p, strassen) is { } z)
            {
                liste.Add(z);
            }
        }
        return liste;
    }

    /// <summary>Die weissen Balken (Anfang, Ende laengs der Strasse) quer verteilt ueber die Fahrbahn.</summary>
    public static IEnumerable<(Vector2 A, Vector2 B)> BalkenVon(Zebra z)
    {
        var halb = StadtSzene.StrassenBreite / 2;
        var luecke = ((2 * halb) - (Balken * BalkenBreite)) / Balken;
        for (var i = 0; i < Balken; i++)
        {
            var quer = -halb + (luecke / 2) + (BalkenBreite / 2) + (i * (BalkenBreite + luecke));
            var m = z.Mitte + (z.Quer * quer);
            yield return (m - (z.Richtung * (Breite / 2)), m + (z.Richtung * (Breite / 2)));
        }
    }
}
