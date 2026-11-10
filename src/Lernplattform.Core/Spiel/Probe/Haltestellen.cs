using System.Numerics;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Eine gelegte Haltestelle: an einer geraden Strasse mit Gehweg, auf einer Seite. <see cref="Mitte"/> = Lotfuss auf der
/// Strassenmitte, <see cref="Richtung"/> = Richtung der Strasse (Laenge 1), <see cref="Aussen"/> = von der Mitte zur Seite
/// der Haltestelle (Laenge 1), <see cref="Halb"/> = halbe Fahrbahn plus Gehweg dieser Strasse.
/// </summary>
public sealed record Haltestelle(int Nr, Vector2 Mitte, Vector2 Richtung, Vector2 Aussen, float Halb)
{
    /// <summary>Fahrtrichtung des Busses, der hier haelt (die Haltestelle liegt rechts, Rechtsverkehr).</summary>
    public Vector2 Fahrt => Vector2.Dot(Bahnuebergang.Rechts(Richtung), Aussen) > 0 ? Richtung : -Richtung;

    /// <summary>Stelle in der Fahrspur, an der die Front des Busses haelt (auf Hoehe des Schilds).</summary>
    public Vector2 Halt => Mitte + (Fahrt * Haltestellen.SchildVor) + (Aussen * Verkehr.Spur);

    /// <summary>Fuss des Masts mit dem Schild (an der Bordsteinkante, auf dem Gehweg).</summary>
    public Vector2 Schild => Mitte + (Fahrt * Haltestellen.SchildVor) + (Aussen * ((StadtSzene.StrassenBreite / 2) + 0.06f));

    /// <summary>Mitte des Wartehaeuschens (hinter dem Gehweg).</summary>
    public Vector2 Haeuschen => Mitte + (Aussen * (Halb + Haltestellen.HaeuschenAbstand + (Haltestellen.HaeuschenTief / 2)));

    /// <summary>Platz der wartenden Figur (im Haeuschen vor der Bank, Blick zur Strasse).</summary>
    public Vector2 Wartend => Haeuschen - (Fahrt * 0.08f) - (Aussen * 0.01f);

    /// <summary>Die vier Ecken des Haeuschens (fuer Laufraster, Pruefung und Werkzeug).</summary>
    public Vector2[] HaeuschenEcken()
    {
        var m = Haeuschen;
        var l = Fahrt * (Haltestellen.HaeuschenLang / 2);
        var t = Aussen * (Haltestellen.HaeuschenTief / 2);
        return [m - l - t, m + l - t, m + l + t, m - l + t];
    }
}

/// <summary>
/// Welle A 0.61.0 / S 0.36.0 (Weisung P12-171, Kurzplan Meldung P12-102, Nico 1a 2a 3a 4b 5b): Bushaltestellen, die Nico im
/// Werkzeug an eine gerade Strasse mit Gehweg setzt. Daten <c>stadtdaten.haltestellen</c> = <c>[{x, y}, …]</c> (ein Punkt
/// auf der Seite der Haltestelle; das Spiel legt ihn wie einen Zebrastreifen auf die naechste gerade Strasse mit Gehweg,
/// <see cref="Legen"/>). Gezeichnet: Wartehaeuschen hinter dem Gehweg (Dach, Glasrueckwand, Seitenwaende, Bank) und Mast mit
/// H-Schild im Paket-Netz, gelbe Zickzack-Linie auf der Fahrspur im Stadtnetz: kein neuer Zeichenaufruf. Der Bus
/// (<see cref="Buslinie"/>) faehrt nur, wenn es Haltestellen gibt; je Haltestelle wartet eine Figur (<see cref="Verkehr.Wartende"/>).
/// Nur Darstellung: keine Ereignisart, kein Feld im Spielstand. Ohne den Abschnitt (Fassung 0) aendert sich nichts.
/// </summary>
public static class Haltestellen
{
    public const string Abschnitt = "haltestellen";

    /// <summary>So weit darf der gesetzte Punkt von der Strassenmitte weg sein (Werkzeug: Einrasten).</summary>
    public const float Fang = 1.4f;

    /// <summary>So weit liegt der Mast (und die Front des haltenden Busses) vor der Mitte der Haltestelle.</summary>
    public const float SchildVor = 0.35f;

    /// <summary>Abstand des Haeuschens hinter der Gehwegkante.</summary>
    public const float HaeuschenAbstand = 0.04f;

    /// <summary>Laenge (laengs der Strasse), Tiefe und Hoehe des Haeuschens.</summary>
    public const float HaeuschenLang = 0.62f, HaeuschenTief = 0.24f, HaeuschenHoch = 0.44f;

    /// <summary>Laenge der Zickzack-Linie auf der Fahrspur (etwas laenger als der Bus, endet am Schild).</summary>
    public const float ZickzackLang = 1.4f;

    /// <summary>Hoechstens so viele Haltestellen je Stadt (je Haltestelle eine wartende Figur im Aufruf der Autos).</summary>
    public const int Hoechstens = 8;

    private static readonly Vector3 Gelb = new(0.96f, 0.80f, 0.10f);
    private static readonly Vector3 Gruen = new(0.12f, 0.50f, 0.30f);
    private static readonly Vector3 Grau = new(0.48f, 0.50f, 0.53f);
    private static readonly Vector3 Dunkel = new(0.18f, 0.19f, 0.21f);
    private static readonly Vector3 Glas = new(0.62f, 0.78f, 0.86f);
    private static readonly Vector3 Holz = new(0.55f, 0.38f, 0.22f);
    private static readonly Vector3 Weiss = new(0.96f, 0.96f, 0.94f);

    /// <summary>Gespeicherte Punkte (null = kein Abschnitt; ein falsch gespeicherter Eintrag ist null in der Liste).</summary>
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
        return [.. liste.Select(Zebrastreifen.Lesen)];
    }

    /// <summary>
    /// Den Punkt auf die naechste gerade Strasse mit Gehweg legen (Lotfuss innerhalb des Stuecks, bis <paramref name="fang"/>);
    /// die Seite ist die des Punkts. null = keine passende Strasse. <paramref name="gehwege"/> = Strassen mit Gehweg (Linie,
    /// halbe Fahrbahn plus Gehweg).
    /// </summary>
    public static Haltestelle? Legen(int nr, Vector2 p, IReadOnlyList<(List<Vector2> Linie, float Halb)> gehwege, float fang = Fang)
    {
        Haltestelle? beste = null;
        var abstand = float.MaxValue;
        foreach (var (s, halb) in gehwege)
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
            if (a > fang || a >= abstand || a < 1e-4f)
            {
                continue;
            }
            var quer = Bahnuebergang.Rechts(d);
            var aussen = Vector2.Dot(p - fuss, quer) >= 0 ? quer : -quer;
            (beste, abstand) = (new Haltestelle(nr, fuss, d, aussen, halb), a);
        }
        return beste;
    }

    /// <summary>Die geraden und gebogenen Strassen mit Gehweg einer Landschaft (Linie, halbe Fahrbahn plus Gehweg) wie in <see cref="StadtSzene"/>.</summary>
    public static List<(List<Vector2> Linie, float Halb)> Gehwege(IEnumerable<PyObjekt> landschaft) =>
        [.. landschaft.Where(t => Py.Gleich(t["typ"], "strasse") && StadtSzene.GehwegBreite(t) > 0)
            .Select(t => (Linie: StadtSzene.Vektoren(t["punkte"]), Halb: (StadtSzene.StrassenBreite / 2) + StadtSzene.GehwegBreite(t)))];

    /// <summary>Alle Haltestellen der Stadtdaten, die an einer geraden Strasse mit Gehweg liegen (Reihenfolge der Daten).</summary>
    public static List<Haltestelle> Alle(PyObjekt? stadtDaten, IReadOnlyList<(List<Vector2> Linie, float Halb)> gehwege)
    {
        var liste = new List<Haltestelle>();
        var punkte = Punkte(stadtDaten) ?? [];
        for (var i = 0; i < punkte.Count; i++)
        {
            if (punkte[i] is { } p && Legen(i + 1, p, gehwege) is { } h)
            {
                liste.Add(h);
            }
        }
        return liste;
    }

    /// <summary>Lage im Raum der Haltestelle: x = laengs (Fahrtrichtung), z = nach aussen, y oben.</summary>
    public static Matrix4x4 Lage(Vector2 fuss, Vector2 laengs, Vector2 aussen) =>
        new(laengs.X, 0, laengs.Y, 0, 0, 1, 0, 0, aussen.X, 0, aussen.Y, 0, fuss.X, 0, fuss.Y, 1);

    private static Teil Q(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 f) => new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, f);

    /// <summary>Wartehaeuschen im eigenen Raum (Mitte am Boden, x laengs, z nach aussen = Rueckwand).</summary>
    private static readonly Lazy<Netz> HaeuschenMuster = new(() =>
    {
        const float l = HaeuschenLang / 2, t = HaeuschenTief / 2, h = HaeuschenHoch, w = 0.012f;
        var s = new Netz();
        s.Hinzu(Q(-l, 0f, -t, l, 0.012f, t, Grau));                       // Boden
        s.Hinzu(Q(-l - 0.02f, h, -t - 0.04f, l + 0.02f, h + 0.03f, t + 0.01f, Gruen));   // Dach
        s.Hinzu(Q(-l, 0.012f, t - w, l, h, t, Glas));                     // Rueckwand
        s.Hinzu(Q(-l, 0.012f, -t + 0.03f, -l + w, h, t, Glas));          // Seitenwaende
        s.Hinzu(Q(l - w, 0.012f, -t + 0.03f, l, h, t, Glas));
        foreach (var x in (float[])[-l, l - w])
        {
            s.Hinzu(Q(x, 0.012f, t - 0.025f, x + w, h, t, Gruen));          // Pfosten hinten
            s.Hinzu(Q(x, 0.012f, -t + 0.03f, x + w, h, -t + 0.03f + w, Gruen)); // Pfosten vorn
        }
        s.Hinzu(Q(-l + 0.06f, 0.10f, t - 0.10f, l - 0.06f, 0.12f, t - 0.02f, Holz)); // Bank
        s.Hinzu(Q(-l + 0.08f, 0.012f, t - 0.06f, -l + 0.10f, 0.10f, t - 0.04f, Dunkel));
        s.Hinzu(Q(l - 0.10f, 0.012f, t - 0.06f, l - 0.08f, 0.10f, t - 0.04f, Dunkel));
        return s;
    });

    /// <summary>Mast mit H-Schild im eigenen Raum (Fuss am Boden, z = zur Strasse hin quer, x laengs).</summary>
    private static readonly Lazy<Netz> MastMuster = new(() =>
    {
        var s = new Netz();
        s.Hinzu(Q(-0.015f, 0f, -0.015f, 0.015f, 0.62f, 0.015f, Grau));
        s.Hinzu(Q(-0.008f, 0.50f, -0.06f, 0.008f, 0.66f, 0.06f, Gelb));      // Schild (gelber Rand)
        s.Hinzu(Q(-0.010f, 0.515f, -0.045f, 0.010f, 0.645f, 0.045f, Gruen)); // Schild (gruen)
        // "H" in Weiss auf beiden Seiten des Schilds
        foreach (var x in (float[])[-0.0115f, 0.0105f])
        {
            s.Hinzu(Q(x, 0.535f, -0.028f, x + 0.001f, 0.625f, -0.016f, Weiss));
            s.Hinzu(Q(x, 0.535f, 0.016f, x + 0.001f, 0.625f, 0.028f, Weiss));
            s.Hinzu(Q(x, 0.574f, -0.016f, x + 0.001f, 0.586f, 0.016f, Weiss));
        }
        return s;
    });

    /// <summary>Haeuschen und Mast aller Haltestellen in ein Netz der Stadt (Paket-Netz, nah und fern gleich).</summary>
    public static void Hinzu(Netz ziel, IEnumerable<Haltestelle> haltestellen)
    {
        foreach (var h in haltestellen)
        {
            ziel.Hinzu(HaeuschenMuster.Value, Lage(h.Haeuschen, h.Fahrt, h.Aussen));
            ziel.Hinzu(MastMuster.Value, Lage(h.Schild, h.Fahrt, h.Aussen));
        }
    }

    /// <summary>Breite der Zickzack-Linie.</summary>
    public const float ZickzackBreite = 0.03f;

    /// <summary>Die Striche der gelben Zickzack-Linie (Anfang, Ende) auf der Fahrspur der Haltestelle.</summary>
    public static IEnumerable<(Vector2 A, Vector2 B)> Zickzack(Haltestelle h)
    {
        const int zacken = 7;
        var ende = h.Mitte + (h.Fahrt * SchildVor);
        var anfang = ende - (h.Fahrt * ZickzackLang);
        var innen = 0.06f;
        var rand = (StadtSzene.StrassenBreite / 2) - 0.04f;
        var vorher = anfang + (h.Aussen * rand);
        for (var i = 1; i <= zacken; i++)
        {
            var p = anfang + (h.Fahrt * (ZickzackLang * i / zacken)) + (h.Aussen * (i % 2 == 1 ? innen : rand));
            yield return (vorher, p);
            vorher = p;
        }
    }
}
