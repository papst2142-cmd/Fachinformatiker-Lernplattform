using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.24.0 (Weisung P12-100, Nico 1b „Fußweg zur nächsten Straße“): liegt keine Strasse gerade vor der Tuer (Fund
/// beim Foto von Nicos Teststadt: alle 9 Haeuser stehen mit der Rueckseite oder der Seite zur Strasse), fuehrt der Weg vom
/// Punkt vor der Tuer auf kuerzestem Weg zur naechsten Strasse, wenn noetig um die Ecken des Hauses herum (nie durch das
/// Haus). Endpunkt auf der Strassenmitte wie bei den Orten (Pruefung „Fußweg erreicht keine Straße“).
/// </summary>
public static partial class GebaeudeTuer
{
    /// <summary>Abstand des Wegs um eine Hausecke (wie der Weg vor der Tuer).</summary>
    public const float UmDieEcke = 0.5f;

    /// <summary>Laengster Weg zur Strasse; weiter weg steht das Haus nicht an einer Strasse (gelber Hinweis).</summary>
    public const float WegHoechstens = 12f;

    /// <summary>Gegenprobe: alter Weg nur geradeaus (Fund Teststadt).</summary>
    internal static readonly AsyncLocal<bool> NurGeradeausFuerTests = new();

    /// <summary>
    /// Weg <paramref name="weg"/> (Tuer, Punkt davor) bis zur naechsten Strasse fuehren, ohne das Haus (x, y, w, h) zu
    /// schneiden. true = Strasse erreicht.
    /// </summary>
    internal static bool WegZurStrasse(List<object?> weg, (double X, double Y, double W, double H) haus, IReadOnlyList<List<Vector2>> strassen, IReadOnlyList<Vector2>? laternen = null)
    {
        laternen ??= [];
        if (NurGeradeausFuerTests.Value || weg.Count < 2 || strassen.All(s => s.Count < 2))
        {
            return false;
        }
        var p = Lesen(weg[^1]);
        var min = new Vector2((float)haus.X, (float)haus.Y);
        var max = new Vector2((float)(haus.X + haus.W), (float)(haus.Y + haus.H));
        var e = new Vector2(UmDieEcke);
        var ecken = new[] { new Vector2(min.X, min.Y) - e, new Vector2(max.X + e.X, min.Y - e.Y), max + e, new Vector2(min.X - e.X, max.Y + e.Y) };
        List<Vector2>? bester = null;
        var besteLaenge = float.MaxValue;
        void Versuch(List<Vector2> zug)
        {
            var ziel = Naechster(zug[^1], strassen, laternen);
            var ganz = new List<Vector2>(zug) { ziel };
            for (var i = 0; i + 1 < ganz.Count; i++)
            {
                if (SchneidetHaus(ganz[i], ganz[i + 1], min, max))
                {
                    return;
                }
            }
            var laenge = 0f;
            for (var i = 0; i + 1 < ganz.Count; i++)
            {
                laenge += Vector2.Distance(ganz[i], ganz[i + 1]);
            }
            if (laenge <= WegHoechstens && laenge < besteLaenge - 1e-4f)
            {
                (bester, besteLaenge) = (ganz, laenge);
            }
        }
        Versuch([p]);
        // um eine oder zwei Ecken (die Ecken liegen um das Haus herum im Kreis)
        for (var i = 0; i < 4; i++)
        {
            Versuch([p, ecken[i]]);
            Versuch([p, ecken[i], ecken[(i + 1) % 4]]);
            Versuch([p, ecken[i], ecken[(i + 3) % 4]]);
        }
        if (bester is null)
        {
            return false;
        }
        weg.RemoveAt(weg.Count - 1);
        foreach (var q in bester)
        {
            weg.Add(Punkt(q));
        }
        return true;
    }

    private static Vector2 Lesen(object? punkt) => punkt is List<object?> l ? new Vector2((float)Py.F(l[0]), (float)Py.F(l[1])) : Vector2.Zero;

    /// <summary>
    /// Freiraum um den Wunschplatz einer Laterne, den das Wegende meidet (wie das Kleingruen, Fund Teststadt: der Weg der
    /// Bitweiche endete an der Laterne am Kreisverkehr, die dann nicht mehr frei stand).
    /// </summary>
    public const float LaterneFrei = 0.8f;

    /// <summary>Gegenprobe: Wegende ohne Ruecksicht auf Laternen.</summary>
    internal static readonly AsyncLocal<bool> LaternenEgalFuerTests = new();

    /// <summary>Naechster Punkt auf der Mitte aller Strassen, nicht naeher als <see cref="LaterneFrei"/> an einer Laterne.</summary>
    private static Vector2 Naechster(Vector2 p, IReadOnlyList<List<Vector2>> strassen, IReadOnlyList<Vector2> laternen)
    {
        var best = p;
        var abstand = float.MaxValue;
        bool Frei(Vector2 q) => LaternenEgalFuerTests.Value || laternen.All(l => Vector2.Distance(l, q) > LaterneFrei);
        foreach (var s in strassen)
        {
            for (var i = 0; i + 1 < s.Count; i++)
            {
                var ab = s[i + 1] - s[i];
                var laenge = ab.Length();
                if (laenge < 1e-6f)
                {
                    continue;
                }
                var t = Math.Clamp(Vector2.Dot(p - s[i], ab) / (laenge * laenge), 0, 1);
                // der Fusspunkt, sonst Punkte daneben auf der Strecke in Schritten von 0,25
                for (var k = 0; k <= 40; k++)
                {
                    foreach (var vz in k == 0 ? new[] { 0f } : new[] { 1f, -1f })
                    {
                        var tt = t + (vz * k * 0.25f / laenge);
                        if (tt < 0 || tt > 1)
                        {
                            continue;
                        }
                        var q = s[i] + (ab * tt);
                        var d = Vector2.Distance(p, q);
                        if (d < abstand && Frei(q))
                        {
                            (best, abstand) = (q, d);
                        }
                    }
                }
            }
        }
        return best;
    }

    /// <summary>Schneidet die Strecke a-b das Innere des Hauses (Rand 0,01 nach innen)?</summary>
    internal static bool SchneidetHaus(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
    {
        const float rand = 0.01f;
        var lo = min + new Vector2(rand);
        var hi = max - new Vector2(rand);
        // Liang-Barsky
        float t0 = 0, t1 = 1;
        var d = b - a;
        foreach (var (pp, q) in new[] { (-d.X, a.X - lo.X), (d.X, hi.X - a.X), (-d.Y, a.Y - lo.Y), (d.Y, hi.Y - a.Y) })
        {
            if (Math.Abs(pp) < 1e-9f)
            {
                if (q < 0)
                {
                    return false;
                }
                continue;
            }
            var r = q / pp;
            if (pp < 0)
            {
                t0 = Math.Max(t0, r);
            }
            else
            {
                t1 = Math.Min(t1, r);
            }
            if (t0 > t1)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Gegenprobe: der rote Fund des gemeinsamen Teils bleibt auch an Gebaeuden ohne Innenraum.</summary>
    internal static readonly AsyncLocal<bool> RotBleibtFuerTests = new();

    /// <summary>
    /// Nico 1b: erreicht der Weg einer Tuer an einem Gebaeude ohne Innenraum keine Strasse, ist das nur ein gelber Hinweis
    /// (<see cref="Pruefen"/>). Der gemeinsame Teil meldet jeden Zierweg ohne Strasse rot; fuer diese Tueren faellt der rote
    /// Fund im Werkzeug weg (Orte und Gebaeude mit Innenraum bleiben rot). Das Spiel prueft Zierwege beim Laden nicht.
    /// </summary>
    public static bool NurGelb(PyObjekt aussen, StadtbauFund f)
    {
        if (RotBleibtFuerTests.Value || !f.Rot || Zierwege(aussen, anlegen: false) is not { } zw)
        {
            return false;
        }
        var bauten = Aussen.Bauten(aussen);
        return zw.OfType<PyObjekt>().Any(z => z["modell"] is string m && bauten[m] is not PyObjekt
            && Aussen.Landschaft(aussen).OfType<PyObjekt>().Any(g => Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], m))
            && f.Text == $"Der Fußweg von „{(z["name"] as string ?? m)}“ erreicht keine Straße.");
    }
}
