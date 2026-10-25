using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Ein Linienzug als Band mit Breite und Hoehe (Fluss, Ufer, Gleisbett, Schiene, Schwelle), Ecken
/// auf Gehrung: die Kanten bleiben ueberall gleich weit von der Mittellinie (Phase 7, Weisung P7-10
/// Nachtrag 1). <see cref="Unten"/> = <see cref="Oben"/>: nur die flache Oberseite.
/// </summary>
public sealed record Streifen(IReadOnlyList<Vector2> Punkte, float Breite, float Unten, float Oben, Vector3 Farbe);

/// <summary>
/// Phase 7, Weisung P7-10 Nachtrag 1 (Wunsch Nico nach der S24-Probe): glatte Kurven fuer Schienen
/// und Fluss statt der Knicke des Python-Linienzugs. Jede Ecke wird durch einen Kreisbogen ersetzt
/// (Radius <see cref="Radius"/>, wo die Nachbarstuecke lang genug sind, sonst so gross, wie es geht);
/// reicht ein Stueck nicht fuer beide Boegen, treffen sie sich in seiner Mitte. Die Richtung aendert
/// sich so nirgends sprunghaft (keine Zacken, kein Versatz an den Uebergaengen). Nur Darstellung und
/// Laufraster, nichts davon geht in den Spielstand.
/// </summary>
public static class Kurve
{
    /// <summary>Wunschradius der Boegen in Karteneinheiten.</summary>
    public const float Radius = 3f;

    /// <summary>Hoechstens so viel Grad Richtungsaenderung je Bogenstueck.</summary>
    public const float GradJeStueck = 4f;

    /// <summary>Linienzug mit Kreisboegen an den Ecken (Anfang und Ende bleiben, gerade Stuecke als zwei Punkte).</summary>
    public static List<Vector2> Glatt(IReadOnlyList<Vector2> p, float radius = Radius)
    {
        var punkte = new List<Vector2>();
        foreach (var q in p)
        {
            if (punkte.Count == 0 || Vector2.DistanceSquared(punkte[^1], q) > 1e-10f)
            {
                punkte.Add(q);
            }
        }
        if (punkte.Count < 3)
        {
            return punkte;
        }
        var ergebnis = new List<Vector2> { punkte[0] };
        for (var i = 1; i + 1 < punkte.Count; i++)
        {
            var a = punkte[i - 1];
            var b = punkte[i];
            var c = punkte[i + 1];
            var rein = Vector2.Normalize(b - a);
            var raus = Vector2.Normalize(c - b);
            var cos = Math.Clamp(Vector2.Dot(rein, raus), -1f, 1f);
            var winkel = MathF.Acos(cos);
            if (winkel < 1e-3f)
            {
                continue;   // gerade weiter: kein Bogen
            }
            // Laenge vom Eckpunkt bis zum Bogenanfang; hoechstens das halbe Nachbarstueck
            var t = MathF.Min(radius * MathF.Tan(winkel / 2), MathF.Min((b - a).Length(), (c - b).Length()) / 2);
            var r = t / MathF.Tan(winkel / 2);
            var anfang = b - (rein * t);
            var ende = b + (raus * t);
            // Mittelpunkt: vom Bogenanfang senkrecht zur Fahrtrichtung, auf der Innenseite
            var links = new Vector2(-rein.Y, rein.X);
            var seite = Kreuz(rein, raus) > 0 ? 1f : -1f;
            var mitte = anfang + (links * (r * seite));
            var stuecke = Math.Max(1, (int)MathF.Ceiling(winkel * 180f / MathF.PI / GradJeStueck));
            var start = anfang - mitte;
            for (var k = 0; k <= stuecke; k++)
            {
                var w = seite * winkel * k / stuecke;
                var (sin, cosW) = MathF.SinCos(w);
                var punkt = mitte + new Vector2((start.X * cosW) - (start.Y * sin), (start.X * sin) + (start.Y * cosW));
                if (Vector2.DistanceSquared(ergebnis[^1], punkt) > 1e-10f)
                {
                    ergebnis.Add(punkt);
                }
            }
            if (Vector2.DistanceSquared(ergebnis[^1], ende) > 1e-6f)
            {
                ergebnis.Add(ende);
            }
            else
            {
                ergebnis[^1] = ende;
            }
        }
        ergebnis.Add(punkte[^1]);
        return ergebnis;
    }

    private static float Kreuz(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>Richtung senkrecht zur Linie an jedem Punkt (Gehrung) und der Faktor, um den die Kante dort weiter aussen liegt.</summary>
    public static List<(Vector2 Quer, float Faktor)> Normalen(IReadOnlyList<Vector2> p)
    {
        var liste = new List<(Vector2, float)>();
        for (var i = 0; i < p.Count; i++)
        {
            var vor = i > 0 ? Vector2.Normalize(p[i] - p[i - 1]) : Vector2.Normalize(p[1] - p[0]);
            var nach = i + 1 < p.Count ? Vector2.Normalize(p[i + 1] - p[i]) : vor;
            var n1 = new Vector2(-vor.Y, vor.X);
            var n2 = new Vector2(-nach.Y, nach.X);
            var summe = n1 + n2;
            var quer = summe.LengthSquared() < 1e-8f ? n1 : Vector2.Normalize(summe);
            var faktor = 1f / MathF.Max(0.5f, Vector2.Dot(quer, n1));
            liste.Add((quer, faktor));
        }
        return liste;
    }

    /// <summary>Parallele Linie im Abstand <paramref name="abstand"/> (links positiv).</summary>
    public static List<Vector2> Versetzt(IReadOnlyList<Vector2> p, float abstand)
    {
        var n = Normalen(p);
        return [.. p.Select((q, i) => q + (n[i].Quer * (abstand * n[i].Faktor)))];
    }

    /// <summary>Punkte im Abstand <paramref name="schritt"/> entlang der Linie mit ihrer Richtung (Schwellen).</summary>
    public static List<(Vector2 Ort, Vector2 Richtung)> Entlang(IReadOnlyList<Vector2> p, float schritt, float beginn = 0f)
    {
        var liste = new List<(Vector2, Vector2)>();
        var pos = beginn;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var d = p[i + 1] - p[i];
            var laenge = d.Length();
            if (laenge <= 0)
            {
                continue;
            }
            var e = d / laenge;
            while (pos <= laenge)
            {
                liste.Add((p[i] + (e * pos), e));
                pos += schritt;
            }
            pos -= laenge;
        }
        return liste;
    }

    /// <summary>Laengstes gerades Stueck (zwei aufeinanderfolgende Punkte) der Linie.</summary>
    public static (Vector2 A, Vector2 B) LaengstesStueck(IReadOnlyList<Vector2> p)
    {
        (Vector2 A, Vector2 B) best = default;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            if (Vector2.Distance(p[i], p[i + 1]) > Vector2.Distance(best.A, best.B))
            {
                best = (p[i], p[i + 1]);
            }
        }
        return best;
    }

    /// <summary>Kleinster Abstand des Punkts zur Linie.</summary>
    public static float Abstand(Vector2 q, IReadOnlyList<Vector2> p)
    {
        if (p.Count == 1)
        {
            return Vector2.Distance(q, p[0]);
        }
        var best = float.MaxValue;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var d = p[i + 1] - p[i];
            var l2 = d.LengthSquared();
            var t = l2 < 1e-12f ? 0 : Math.Clamp(Vector2.Dot(q - p[i], d) / l2, 0f, 1f);
            best = MathF.Min(best, Vector2.Distance(q, p[i] + (d * t)));
        }
        return best;
    }
}
