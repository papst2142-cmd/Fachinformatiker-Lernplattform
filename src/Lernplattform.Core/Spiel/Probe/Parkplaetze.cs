using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Ein gelegter Parkplatz: Flaeche hinter dem Gehweg einer geraden Strasse mit Gehweg, Einfahrt quer ueber den Gehweg.
/// <see cref="Vorn"/> = Mitte der Vorderkante (an der Gehwegkante), <see cref="Laengs"/> = Richtung der Strasse,
/// <see cref="Aussen"/> = von der Strasse weg; <see cref="Breite"/> laengs, <see cref="Tiefe"/> nach aussen.
/// </summary>
public sealed record Parkplatz(int Nr, Vector2 Vorn, Vector2 Laengs, Vector2 Aussen, int Plaetze, int Reihen, float Halb)
{
    /// <summary>Stellplaetze je Reihe.</summary>
    public int JeReihe => (Plaetze + Reihen - 1) / Reihen;

    /// <summary>Breite laengs der Strasse.</summary>
    public float Breite => Reihen == 1 ? JeReihe * Parkplaetze.PlatzBreit : (2 * Parkplaetze.PlatzTief) + Parkplaetze.Gasse;

    /// <summary>Tiefe von der Gehwegkante nach aussen.</summary>
    public float Tiefe => Reihen == 1 ? Parkplaetze.Gasse + Parkplaetze.PlatzTief : (JeReihe * Parkplaetze.PlatzBreit) + 0.1f;

    /// <summary>Punkt im Raum des Parkplatzes (u laengs ab der Mitte, v nach aussen ab der Vorderkante).</summary>
    public Vector2 Punkt(float u, float v) => Vorn + (Laengs * u) + (Aussen * v);

    /// <summary>Die vier Ecken der Flaeche.</summary>
    public Vector2[] Ecken() => [Punkt(-Breite / 2, 0), Punkt(Breite / 2, 0), Punkt(Breite / 2, Tiefe), Punkt(-Breite / 2, Tiefe)];

    /// <summary>Die Einfahrt (Anfang an der Bordsteinkante, Ende an der Flaeche) in der Mitte der Vorderkante.</summary>
    public (Vector2 A, Vector2 B) Einfahrt => (Vorn - (Aussen * (Halb - (StadtSzene.StrassenBreite / 2))), Vorn);

    /// <summary>Die Stellplaetze: Mitte und Blick (Richtung, in die die Front des geparkten Autos zeigt).</summary>
    public IEnumerable<(Vector2 Mitte, Vector2 Blick)> Stellplaetze()
    {
        if (Reihen == 1)
        {
            for (var i = 0; i < Plaetze; i++)
            {
                var u = (-Breite / 2) + ((i + 0.5f) * Parkplaetze.PlatzBreit);
                yield return (Punkt(u, Parkplaetze.Gasse + (Parkplaetze.PlatzTief / 2)), Aussen);
            }
            yield break;
        }
        for (var i = 0; i < Plaetze; i++)
        {
            var seite = i % 2 == 0 ? -1f : 1f;
            var v = 0.05f + ((i / 2) + 0.5f) * Parkplaetze.PlatzBreit;
            var u = seite * ((Parkplaetze.Gasse / 2) + (Parkplaetze.PlatzTief / 2));
            yield return (Punkt(u, v), Laengs * seite);
        }
    }

    /// <summary>Die weissen Trennlinien (Anfang, Ende).</summary>
    public IEnumerable<(Vector2 A, Vector2 B)> Linien()
    {
        if (Reihen == 1)
        {
            for (var i = 0; i <= JeReihe; i++)
            {
                var u = (-Breite / 2) + (i * Parkplaetze.PlatzBreit);
                yield return (Punkt(u, Parkplaetze.Gasse + 0.03f), Punkt(u, Tiefe - 0.03f));
            }
            yield break;
        }
        foreach (var seite in (float[])[-1f, 1f])
        {
            var u1 = seite * (Parkplaetze.Gasse / 2);
            var u2 = seite * ((Parkplaetze.Gasse / 2) + Parkplaetze.PlatzTief - 0.03f);
            for (var i = 0; i <= JeReihe; i++)
            {
                var v = 0.05f + (i * Parkplaetze.PlatzBreit);
                yield return (Punkt(u1, v), Punkt(u2, v));
            }
        }
    }
}

/// <summary>
/// Welle A 0.61.0 / S 0.36.0 (Weisung P12-171, Nico 3a): Parkplaetze als eigene Flaeche neben einer geraden Strasse mit
/// Gehweg. Daten <c>stadtdaten.parkplaetze</c> = <c>[{x, y, plaetze, reihen}, …]</c>: Punkt auf der Seite des Parkplatzes
/// (wie eine Haltestelle auf die naechste gerade Strasse mit Gehweg gelegt, <see cref="Legen"/>), 2 bis 12 Plaetze, 1 oder 2
/// Reihen. Geparkte Autos stehen nur (kein Ein- und Ausparken): je Spieltag aus Tag, Parkplatz und Platz gezogen, ob ein
/// Platz belegt ist und welches der sechs Automodelle dort steht, gleich auf allen Geraeten (<see cref="Belegung"/>); im
/// Werkzeug sind alle Plaetze belegt (<see cref="Stadt.ParkplaetzeVoll"/>), damit Grenzen, Live-Leiste und „Stadt zu teuer“
/// den vollen Parkplatz zaehlen (Weisung P12-170 Hinweis 4). Die Autos sind Stuecke der Stadt (mit dem Instancing aus D2 ab
/// 8 gleichen). Nur Darstellung: kein Spielstand, keine Ereignisart. Ohne den Abschnitt (Fassung 0) aendert sich nichts.
/// </summary>
public static class Parkplaetze
{
    public const string Abschnitt = "parkplaetze";

    /// <summary>Breite und Tiefe eines Stellplatzes, Breite der Fahrgasse.</summary>
    public const float PlatzBreit = 0.36f, PlatzTief = 0.72f, Gasse = 0.6f;

    /// <summary>Kleinste und groesste Zahl der Plaetze.</summary>
    public const int Mindestens = 2, Hoechstens = 12;

    /// <summary>Etwa so viele Plaetze sind an einem Spieltag belegt (zwei von drei).</summary>
    public const int BelegtVonDrei = 2;

    /// <summary>Flaeche und Linien.</summary>
    public static readonly Vector3 Flaeche = new(0.42f, 0.43f, 0.46f), Linie = new(0.94f, 0.94f, 0.92f);

    /// <summary>Hoehe der Flaeche und der Linien (wie der Gehweg bzw. knapp darueber).</summary>
    public const float FlaecheHoehe = StadtSzene.Hoehe.Gehweg, LinieHoehe = StadtSzene.Hoehe.Gehweg + 0.004f;

    /// <summary>Ein Eintrag der Daten: Punkt, Plaetze, Reihen (null = falsch gespeichert).</summary>
    public static (Vector2 P, int Plaetze, int Reihen)? Lesen(object? eintrag) =>
        Zebrastreifen.Lesen(eintrag) is { } p && eintrag is PyObjekt o && Get(o, "plaetze") is long n && Get(o, "reihen") is long r
            ? (p, (int)Math.Clamp(n, -1000, 1000), (int)Math.Clamp(r, -1000, 1000))
            : null;

    /// <summary>Gespeicherte Eintraege (null = kein Abschnitt; ein falsch gespeicherter Eintrag ist null in der Liste).</summary>
    public static List<(Vector2 P, int Plaetze, int Reihen)?>? Eintraege(PyObjekt? stadtDaten)
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

    /// <summary>Gueltige Zahl der Plaetze und Reihen?</summary>
    public static bool Gueltig(int plaetze, int reihen) => plaetze is >= Mindestens and <= Hoechstens && reihen is 1 or 2;

    /// <summary>Den Parkplatz an die naechste gerade Strasse mit Gehweg legen (wie <see cref="Haltestellen.Legen"/>).</summary>
    public static Parkplatz? Legen(int nr, Vector2 p, int plaetze, int reihen, IReadOnlyList<(List<Vector2> Linie, float Halb)> gehwege,
        float fang = Haltestellen.Fang + 1.5f)
    {
        if (!Gueltig(plaetze, reihen) || Haltestellen.Legen(nr, p, gehwege, fang) is not { } h)
        {
            return null;
        }
        // Vorderkante an der Gehwegkante, laengs an der Stelle des Punkts
        return new Parkplatz(nr, h.Mitte + (h.Aussen * h.Halb), h.Richtung, h.Aussen, plaetze, reihen, h.Halb);
    }

    /// <summary>Alle Parkplaetze der Stadtdaten, die an einer geraden Strasse mit Gehweg liegen (Reihenfolge der Daten).</summary>
    public static List<Parkplatz> Alle(PyObjekt? stadtDaten, IReadOnlyList<(List<Vector2> Linie, float Halb)> gehwege)
    {
        var liste = new List<Parkplatz>();
        var eintraege = Eintraege(stadtDaten) ?? [];
        for (var i = 0; i < eintraege.Count; i++)
        {
            if (eintraege[i] is var (p, n, r) && Legen(i + 1, p, n, r, gehwege) is { } pp)
            {
                liste.Add(pp);
            }
        }
        return liste;
    }

    /// <summary>
    /// Welches Auto steht am Spieltag <paramref name="tag"/> auf Platz <paramref name="platz"/> von Parkplatz
    /// <paramref name="nr"/>? Modellname oder null (frei). <paramref name="voll"/> = jeder Platz belegt (Werkzeug).
    /// </summary>
    public static string? Belegung(int nr, int platz, long tag, bool voll)
    {
        var zufall = new Autostrecken.Zufall(1000 + (nr * 16) + platz, tag);
        var belegt = zufall.Bis(3) < BelegtVonDrei;
        var modell = Verkehr.Strecken[zufall.Bis(Verkehr.Strecken.Length)].Modell;
        return voll || belegt ? modell : null;
    }
}
