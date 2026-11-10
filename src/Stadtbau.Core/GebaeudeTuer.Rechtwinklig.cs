using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Stadtbau.Core;

/// <summary>
/// Sammelliste 278 (Weisung P12-164, Nico Frage 1 = a „rechtwinklig“): der Weg von der Tuer zur Strasse laeuft gerade aus der
/// Tuer, wenn noetig um eine oder zwei Ecken des Hauses (Abstand <see cref="UmDieEcke"/>), jedes Stueck parallel oder
/// rechtwinklig zur Hauswand; das letzte Stueck trifft die Strasse quer (rechtwinklig zur Strassenmitte). Von allen solchen
/// Wegen gilt der kuerzeste (hoechstens <see cref="WegHoechstens"/>), das Ende liegt auf der Strassenmitte und nicht naeher als
/// <see cref="LaterneFrei"/> an einer Laterne, kein Stueck schneidet das Haus.
/// Sammelliste 280: der Weg bekommt das Feld <c>radien</c> (je Punkt ein Radius, Anfang und Ende 0, Ecken
/// <see cref="StadtTuerweg.Radius"/>); das Spiel zeichnet ihn damit mit runden Ecken wie einen Fussweg. Wege ohne das Feld
/// (Fassung 0, aeltere Projekte) bleiben, wie sie sind.
/// </summary>
public static partial class GebaeudeTuer
{
    /// <summary>Gegenprobe Sammelliste 278/280: Weg wie bis S 0.34.0 (gerade hinaus, sonst schraeg zum naechsten Punkt), ohne Radien.</summary>
    public static bool WegAltFuerTests { get => WegAlt.Value; set => WegAlt.Value = value; }
    private static readonly AsyncLocal<bool> WegAlt = new();

    /// <summary>Schrittweite, in der das letzte gerade Stueck vor dem Abbiegen auf die Strasse verlaengert wird.</summary>
    public const float WegRaster = 0.05f;

    /// <summary>Das letzte (quere) Stueck ist mindestens so lang: ein winziges Stueck verdreht sich beim Runden auf 0,001.</summary>
    public const float QuerMindestens = 0.3f;

    /// <summary>
    /// Den Weg des Zugangs bzw. Zierwegs <paramref name="zugang"/> (Feld <c>weg</c>: Tuer, Punkt davor) rechtwinklig bis zur
    /// Strasse fuehren und <c>radien</c> setzen. true = Strasse erreicht (sonst bleibt der Weg, wie er war, ohne Radien).
    /// </summary>
    internal static bool WegRechtwinklig(PyObjekt zugang, (double X, double Y, double W, double H) haus, IReadOnlyList<List<Vector2>> strassen, IReadOnlyList<Vector2>? laternen = null)
    {
        var weg = (List<object?>)zugang["weg"]!;
        if (WegAlt.Value)
        {
            zugang.RemoveAll(e => e.Key == "radien");
            return AussenBearbeitung.WegAnschliessen(weg, strassen) || WegZurStrasse(weg, haus, strassen, laternen);
        }
        laternen ??= [];
        if (weg.Count < 2 || strassen.All(s => s.Count < 2))
        {
            return false;
        }
        var tuer = Lesen(weg[0]);
        var vor = Lesen(weg[1]);
        var min = new Vector2((float)haus.X, (float)haus.Y);
        var max = new Vector2((float)(haus.X + haus.W), (float)(haus.Y + haus.H));
        var bester = Planen(tuer, vor, min, max, strassen, laternen);
        if (bester is null)
        {
            return false;
        }
        weg.Clear();
        foreach (var q in bester)
        {
            weg.Add(Punkt3(q));
        }
        Py.Setzen(zugang, "radien", Radien(weg.Count));
        return true;
    }

    /// <summary>Radien je Punkt: Anfang und Ende 0, jede Ecke <see cref="StadtTuerweg.Radius"/>.</summary>
    internal static List<object?> Radien(int punkte) =>
        [.. Enumerable.Range(0, punkte).Select(i => (object?)(i == 0 || i == punkte - 1 ? 0.0 : (double)StadtTuerweg.Radius))];

    /// <summary>Der kuerzeste rechtwinklige Weg (Tuer, ..., Punkt auf der Strassenmitte), null = keiner.</summary>
    internal static List<Vector2>? Planen(Vector2 tuer, Vector2 vor, Vector2 min, Vector2 max, IReadOnlyList<List<Vector2>> strassen, IReadOnlyList<Vector2> laternen)
    {
        var e = UmDieEcke;
        var n = vor - tuer;
        n = MathF.Abs(n.X) >= MathF.Abs(n.Y) ? new Vector2(MathF.Sign(n.X), 0) : new Vector2(0, MathF.Sign(n.Y));
        // Haus mit Nische oder Hof (Gewerbehof): liegt die Tuer im Rechteck der Daten, zaehlt nur der Teil hinter der Tuer als Haus
        // (vor der Tuer ist frei, sonst kaeme der Weg nie hinaus)
        if (n.Y > 0)
        {
            max.Y = MathF.Min(max.Y, tuer.Y);
        }
        if (n.Y < 0)
        {
            min.Y = MathF.Max(min.Y, tuer.Y);
        }
        if (n.X > 0)
        {
            max.X = MathF.Min(max.X, tuer.X);
        }
        if (n.X < 0)
        {
            min.X = MathF.Max(min.X, tuer.X);
        }
        List<Vector2>? bester = null;
        var besteLaenge = float.MaxValue;
        bool Frei(Vector2 q) => LaternenEgalFuerTests.Value || laternen.All(l => Vector2.Distance(l, q) > LaterneFrei);
        void Versuch(List<Vector2> zug)
        {
            for (var i = 0; i + 1 < zug.Count; i++)
            {
                if (!Achsrecht(zug[i], zug[i + 1]) || SchneidetHaus(zug[i], zug[i + 1], min, max))
                {
                    return;
                }
            }
            var w = zug[^1];
            var richtung = zug.Count >= 2 && Vector2.Distance(zug[^2], w) > 1e-4f ? Vector2.Normalize(w - zug[^2]) : n;
            var laengeBis = Laenge(zug);
            // am letzten Punkt gerade weiter oder rechtwinklig abbiegen, ein Stueck (a >= 0) gehen, dann quer auf die Strasse
            // Gegenprobe der Tests: nur gerade aus der Tuer (kein Abbiegen, keine Ecke)
            var richtungen = NurGeradeausFuerTests.Value ? new[] { richtung } : [richtung, new Vector2(-richtung.Y, richtung.X), new Vector2(richtung.Y, -richtung.X)];
            foreach (var u in richtungen)
            {
            for (var k = 0; k * WegRaster <= WegHoechstens; k++)
            {
                var a = k * WegRaster;
                var q = w + (u * a);
                if (laengeBis + a > besteLaenge || (a > 0 && SchneidetHaus(w, q, min, max)))
                {
                    break;
                }
                foreach (var s in strassen)
                {
                    for (var i = 0; i + 1 < s.Count; i++)
                    {
                        var ab = s[i + 1] - s[i];
                        var l = ab.Length();
                        if (l < 1e-6f)
                        {
                            continue;
                        }
                        var d = ab / l;
                        var t = Vector2.Dot(q - s[i], d);
                        if (t < 0 || t > l)
                        {
                            continue;
                        }
                        var fuss = s[i] + (d * t);
                        var quer = Vector2.Distance(q, fuss);
                        var ganz = laengeBis + a + quer;
                        // ein schraeges letztes Stueck (schraege Strasse) zaehlt dreifach: lieber am Haus entlang und kurz quer
                        // hinueber als lang schraeg ueber die Wiese (Nico Frage 1 = a)
                        var kosten = ganz + (Achsrecht(q, fuss) ? 0 : 2 * quer);
                        if (quer < QuerMindestens || ganz > WegHoechstens || kosten >= besteLaenge - 1e-4f || !Frei(fuss) || SchneidetHaus(q, fuss, min, max))
                        {
                            continue;
                        }
                        var neu = new List<Vector2>(zug);
                        if (a > 0)
                        {
                            neu.Add(q);
                        }
                        neu.Add(fuss);
                        (bester, besteLaenge) = (Gerade(neu), kosten);
                    }
                }
            }
            }
        }
        Versuch([tuer, vor]);
        if (NurGeradeausFuerTests.Value)
        {
            return bester;
        }
        // um eine oder zwei Ecken: vor der Tuer an der Wand entlang bis neben das Haus, dann an der Seite entlang nach hinten
        var seiten = MathF.Abs(n.X) < 0.5f
            ? new[] { new Vector2(min.X - e, vor.Y), new Vector2(max.X + e, vor.Y) }
            : [new Vector2(vor.X, min.Y - e), new Vector2(vor.X, max.Y + e)];
        foreach (var c in seiten)
        {
            Versuch([tuer, vor, c]);
            var hinten = MathF.Abs(n.X) < 0.5f ? new Vector2(c.X, n.Y > 0 ? min.Y - e : max.Y + e) : new Vector2(n.X > 0 ? min.X - e : max.X + e, c.Y);
            Versuch([tuer, vor, c, hinten]);
        }
        return bester;
    }

    private static bool Achsrecht(Vector2 a, Vector2 b) => MathF.Abs(a.X - b.X) < 1e-4f || MathF.Abs(a.Y - b.Y) < 1e-4f;

    private static float Laenge(List<Vector2> p)
    {
        var l = 0f;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            l += Vector2.Distance(p[i], p[i + 1]);
        }
        return l;
    }

    /// <summary>Punkte ohne Knick weglassen (gerade weiter) und doppelte Punkte.</summary>
    private static List<Vector2> Gerade(List<Vector2> p)
    {
        var r = new List<Vector2>();
        foreach (var q in p)
        {
            if (r.Count > 0 && Vector2.Distance(r[^1], q) < 1e-4f)
            {
                continue;
            }
            if (r.Count >= 2)
            {
                var d1 = Vector2.Normalize(r[^1] - r[^2]);
                var d2 = Vector2.Normalize(q - r[^1]);
                if (Vector2.Dot(d1, d2) > 0.99995f)
                {
                    r[^1] = q;
                    continue;
                }
            }
            r.Add(q);
        }
        return r;
    }

    /// <summary>Auf 0,001 (das letzte Stueck soll auch kurz noch quer auf die Strasse treffen, ±1 Grad).</summary>
    private static List<object?> Punkt3(Vector2 p) => [Bearbeitung.Zahl(Math.Round(p.X, 3)), Bearbeitung.Zahl(Math.Round(p.Y, 3))];
}
