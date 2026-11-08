using System.Numerics;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle Z (S 0.14.0, Weisung P12-71, Kurzplan P12-40 Abschnitt 3, Nico P12-67 Frage 3 = a): die Verbindung zwischen
/// Anfang und Ende beim Ziehen von Punkt zu Punkt, ohne Oberflaeche. Sie besteht aus geraden Stuecken und Boegen:
/// ein Eckzug P0 → X1 (→ X2) → P1, jede Ecke mit Radius und Winkel, der Bogen beruehrt beide Nachbarstuecke
/// (Tangentenlaenge r · tan(θ/2), wie <see cref="Lernplattform.Core.Spiel.Probe.Kurve.Glatt(IReadOnlyList{Vector2}, IReadOnlyList{float}?, float)"/>).
/// <list type="bullet">
/// <item>Ein Ende ohne Richtung (frei auf der Karte) liegt genau dort; ein Ende mit Richtung (an einem Strassenende, einer
/// Gehwegkante, einem Flussende, dem Kartenrand) wird in dieser Richtung verlassen bzw. erreicht.</item>
/// <item>Die Richtung der Stuecke zwischen den Ecken aendert sich in Schritten von <see cref="Strassenkurve.SchrittGrad"/>
/// Grad gegen den Anfang; bei Strassen (<see cref="Stufig"/>) ist jeder Bogen ein Vielfaches davon und mindestens
/// <see cref="Strassenkurve.ZugMindestens"/> Grad (sonst passt er nicht zu den Kurvenstuecken des Spiels).</item>
/// <item>Gesucht wird zuerst eine Ecke (Gerade–Bogen–Gerade), dann zwei Ecken (S oder zweimal gleich herum); der groesste
/// Radius, der passt, gewinnt, bei gleichem Radius die kleinste Drehung.</item>
/// <item>Passt nichts, gibt es keine Verbindung und einen Grund in einfachen Worten (die Vorschau ist dann rot, nichts
/// wird gesetzt).</item>
/// </list>
/// </summary>
public static class Verbindung
{
    /// <summary>Eine Ecke: Lage, Radius, Winkel in Grad (positiv = Richtung nimmt zu, auf der Karte rechts herum).</summary>
    public readonly record struct Ecke(Vector2 Lage, double Radius, double Grad);

    /// <summary>Ergebnis: der Eckzug von Anfang bis Ende (Anfang, Ecken, Ende) oder ein Grund.</summary>
    public sealed record Plan(Vector2 Anfang, IReadOnlyList<Ecke> Ecken, Vector2 Ende, string? Grund)
    {
        public bool Gefunden => Grund is null;

        /// <summary>Alle Punkte des Eckzugs.</summary>
        public List<Vector2> Eckzug => [Anfang, .. Ecken.Select(e => e.Lage), Ende];
    }

    /// <summary>Kuerzestes gerades Stueck zwischen zwei Boegen (darunter faellt es weg).</summary>
    public const float GeradeMindestens = 0.005f;

    /// <summary>Unter so viel Grad Abweichung zaehlt eine Richtung als gleich (Rundung der Punkte).</summary>
    public const double GleichGrad = 0.5;

    /// <summary>
    /// Den Eckzug rechnen. <paramref name="w0"/> = Richtung, in der die Verbindung den Anfang verlaesst; <paramref name="w1"/> =
    /// Richtung vom Ende in die Verbindung hinein (also entgegen der Fahrt), je null = frei. <paramref name="radien"/> in der
    /// Reihenfolge der Wahl (groesster zuerst). <paramref name="stufig"/>: jeder Bogen ein Vielfaches von 7,5 Grad ab 15.
    /// </summary>
    public static Plan Rechnen(Vector2 p0, Vector2? w0, Vector2 p1, Vector2? w1, IReadOnlyList<double> radien, bool stufig)
    {
        if (Vector2.Distance(p0, p1) < 0.25f)
        {
            return new Plan(p0, [], p1, Texte.ZiehenZuKurz);
        }
        if (w0 is null && w1 is null)
        {
            return new Plan(p0, [], p1, null);
        }
        if (w0 is null)
        {
            // umgekehrt rechnen: vom Ende mit Richtung zum freien Anfang, dann den Eckzug umdrehen
            var r = EinEnde(p1, w1!.Value, p0, radien, stufig);
            return r.Grund is not null ? r with { Anfang = p0, Ende = p1 }
                : new Plan(r.Ende, [.. r.Ecken.Reverse().Select(e => e with { Grad = -e.Grad })], r.Anfang, null);
        }
        return w1 is null ? EinEnde(p0, w0.Value, p1, radien, stufig) : ZweiEnden(p0, w0.Value, p1, w1.Value, radien, stufig);
    }

    private static double Mindestens(bool stufig) => stufig ? Strassenkurve.ZugMindestens : Strassenkurve.SchrittGrad;

    private static double Tangente(double r, double grad) => r * Math.Tan(Math.Abs(grad) * Math.PI / 360);

    /// <summary>Anfang mit Richtung, Ende frei: Gerade bis zur Ecke, Bogen, Gerade bis genau zum Ende.</summary>
    private static Plan EinEnde(Vector2 p0, Vector2 w0, Vector2 p1, IReadOnlyList<double> radien, bool stufig)
    {
        var d0 = Vector2.Normalize(w0);
        var zu = p1 - p0;
        var peil = Grad(d0, zu);
        if (Math.Abs(peil) < GleichGrad && Vector2.Dot(zu, d0) > 0)
        {
            // geradeaus: das Ende auf die Richtung legen (Vorschau und Ergebnis zeigen dieselbe Lage)
            return new Plan(p0, [], p0 + (d0 * Vector2.Dot(zu, d0)), null);
        }
        // zuerst nur Boegen bis 90 Grad (sonst gewaenne ein grosser Radius mit einem Bogen, der ueber das Ende hinaus
        // ausholt und zurueckkommt), erst wenn keiner passt auch staerkere
        foreach (var grenze in new[] { 90.0, 180.0 })
        {
            if (EinEnde(p0, d0, p1, zu, radien, stufig, grenze) is { } plan)
            {
                return plan;
            }
        }
        return new Plan(p0, [], p1, Math.Abs(peil) > 90 ? Texte.ZiehenHinten : Texte.ZiehenZuEng);
    }

    private static Plan? EinEnde(Vector2 p0, Vector2 d0, Vector2 p1, Vector2 zu, IReadOnlyList<double> radien, bool stufig, double grenze)
    {
        Plan? best = null;
        var bestGrad = double.MaxValue;
        foreach (var r in radien)
        {
            for (var k = 1; k * Strassenkurve.SchrittGrad < 180 - 1e-9; k++)
            {
                foreach (var seite in new[] { 1, -1 })
                {
                    var grad = seite * k * Strassenkurve.SchrittGrad;
                    if (Math.Abs(grad) < Mindestens(stufig) - 1e-9 || Math.Abs(grad) >= bestGrad || Math.Abs(grad) > grenze + 1e-9)
                    {
                        continue;
                    }
                    var dm = Gedreht(d0, grad);
                    if (Loesen(zu, d0, dm) is not var (a, b))
                    {
                        continue;
                    }
                    var t = Tangente(r, grad);
                    if (a >= t - 1e-4 && b >= t - 1e-4)
                    {
                        best = new Plan(p0, [new Ecke(p0 + (d0 * (float)a), r, grad)], p1, null);
                        bestGrad = Math.Abs(grad);
                    }
                }
            }
            if (best is not null)
            {
                return best;
            }
        }
        return null;
    }

    /// <summary>Beide Enden mit Richtung: eine Ecke, sonst zwei.</summary>
    private static Plan ZweiEnden(Vector2 p0, Vector2 w0, Vector2 p1, Vector2 w1, IReadOnlyList<double> radien, bool stufig)
    {
        var d0 = Vector2.Normalize(w0);
        var d1 = -Vector2.Normalize(w1);   // Fahrtrichtung am Ende
        var delta = Grad(d0, d1);
        var zu = p1 - p0;
        if (stufig)
        {
            var stufe = Math.Round(delta / Strassenkurve.SchrittGrad) * Strassenkurve.SchrittGrad;
            if (Math.Abs(delta - stufe) > GleichGrad)
            {
                return new Plan(p0, [], p1, Texte.ZiehenRichtung(Math.Abs(delta - stufe)));
            }
            delta = stufe;
        }
        if (Math.Abs(delta) < GleichGrad)
        {
            // gleiche Richtung: gerade, wenn das Ende auf der Linie liegt
            var quer = Math.Abs(Kreuz(d0, zu));
            if (quer < 0.01 && Vector2.Dot(zu, d0) > 0)
            {
                return new Plan(p0, [], p1, null);
            }
            delta = 0;
        }
        foreach (var r in radien)
        {
            // eine Ecke
            if (Math.Abs(delta) >= Mindestens(stufig) - 1e-9 && Math.Abs(delta) < 180 - 1e-9 && Loesen(zu, d0, d1) is var (la, lc))
            {
                var t = Tangente(r, delta);
                if (la >= t - 1e-4 && lc >= t - 1e-4)
                {
                    return new Plan(p0, [new Ecke(p0 + (d0 * (float)la), r, delta)], p1, null);
                }
            }
            // zwei Ecken: Richtung dazwischen in Schritten von 7,5 Grad gegen den Anfang
            Plan? best = null;
            var bestGrad = double.MaxValue;
            for (var k = -23; k <= 23; k++)
            {
                var g1 = k * Strassenkurve.SchrittGrad;
                var g2 = delta - g1;
                if (Math.Abs(g1) < Mindestens(stufig) - 1e-9 || Math.Abs(g2) < Mindestens(stufig) - 1e-9 || Math.Abs(g2) >= 180 - 1e-9
                    || Math.Abs(g1) + Math.Abs(g2) >= bestGrad)
                {
                    continue;
                }
                var dm = Gedreht(d0, g1);
                var (t1, t2) = (Tangente(r, g1), Tangente(r, g2));
                foreach (var (a, b, c) in ZweiLoesungen(zu, d0, dm, d1, t1, t2))
                {
                    if (a >= t1 - 1e-4 && b >= t1 + t2 - 1e-4 && c >= t2 - 1e-4)
                    {
                        var x1 = p0 + (d0 * (float)a);
                        best = new Plan(p0, [new Ecke(x1, r, g1), new Ecke(x1 + (dm * (float)b), r, g2)], p1, null);
                        bestGrad = Math.Abs(g1) + Math.Abs(g2);
                        break;
                    }
                }
            }
            if (best is not null)
            {
                return best;
            }
        }
        return new Plan(p0, [], p1, Texte.ZiehenZuEng);
    }

    /// <summary>Drei Laengen a, b, c mit zu = a·d0 + b·dm + c·d1: gleich lange Enden, kuerzester Anfang, kuerzestes Ende.</summary>
    private static IEnumerable<(double A, double B, double C)> ZweiLoesungen(Vector2 zu, Vector2 d0, Vector2 dm, Vector2 d1, double t1, double t2)
    {
        if (Loesen(zu, d0 + d1, dm) is var (ac, b1))
        {
            yield return (ac, b1, ac);
        }
        if (Loesen(zu - (d0 * (float)t1), dm, d1) is var (b2, c2))
        {
            yield return (t1, b2, c2);
        }
        if (Loesen(zu - (d1 * (float)t2), d0, dm) is var (a3, b3))
        {
            yield return (a3, b3, t2);
        }
    }

    /// <summary>zu = a·u + b·v loesen (null, wenn u und v fast parallel sind).</summary>
    private static (double A, double B)? Loesen(Vector2 zu, Vector2 u, Vector2 v)
    {
        double det = Kreuz(u, v);
        if (Math.Abs(det) < 1e-6)
        {
            return null;
        }
        return (Kreuz(zu, v) / det, Kreuz(u, zu) / det);
    }

    private static double Kreuz(Vector2 a, Vector2 b) => ((double)a.X * b.Y) - ((double)a.Y * b.X);

    /// <summary>Vorzeichenbehafteter Winkel von a nach b in Grad.</summary>
    public static double Grad(Vector2 a, Vector2 b) => Math.Atan2(Kreuz(a, b), Vector2.Dot(a, b)) * 180 / Math.PI;

    /// <summary>Vektor um so viel Grad gedreht (positiv = Richtung nimmt zu).</summary>
    public static Vector2 Gedreht(Vector2 v, double grad)
    {
        var (s, c) = Math.SinCos(grad * Math.PI / 180);
        return new Vector2((float)((v.X * c) - (v.Y * s)), (float)((v.X * s) + (v.Y * c)));
    }

    /// <summary>
    /// Strassen: der Eckzug als Folge von Stuecken. Gerade = zwei Punkte; Bogen = Punkte alle 7,5 Grad
    /// (<see cref="Strassenkurve.Mittellinie"/>) mit Winkel und Radius; ein Bogen ueber 90 Grad wird in gleich grosse
    /// Teile von hoechstens 90 Grad geteilt. Gerade Stuecke kuerzer als <see cref="GeradeMindestens"/> fallen weg.
    /// </summary>
    public static List<(List<Vector2> Punkte, double Grad, double Radius)> Stuecke(Plan plan)
    {
        var liste = new List<(List<Vector2>, double, double)>();
        var cur = plan.Anfang;
        var zug = plan.Eckzug;
        for (var i = 0; i < plan.Ecken.Count; i++)
        {
            var e = plan.Ecken[i];
            var rein = Vector2.Normalize(zug[i + 1] - zug[i]);
            var t = (float)Tangente(e.Radius, e.Grad);
            var anfang = e.Lage - (rein * t);
            if (Vector2.Distance(cur, anfang) >= GeradeMindestens)
            {
                liste.Add(([cur, anfang], 0, 0));
            }
            var schritte = (int)Math.Round(Math.Abs(e.Grad) / Strassenkurve.SchrittGrad);
            var teile = (int)Math.Ceiling(Math.Abs(e.Grad) / Strassenkurve.ZugHoechstens - 1e-9);
            var richtung = rein;
            var lage = anfang;
            var rest = schritte;
            for (var n = 0; n < teile; n++)
            {
                var jetzt = (int)Math.Round(rest / (double)(teile - n));
                var grad = jetzt * Strassenkurve.SchrittGrad;
                var form = Strassenkurve.Mittellinie(Strassenkurve.Bogen, grad, e.Radius, e.Grad < 0);
                var w = Math.Atan2(richtung.Y, richtung.X);
                var (s, c) = MathF.SinCos((float)w);
                var punkte = form.Select(q => lage + new Vector2((q.X * c) - (q.Y * s), (q.X * s) + (q.Y * c))).ToList();
                liste.Add((punkte, grad, e.Radius));
                lage = punkte[^1];
                richtung = Gedreht(richtung, Math.Sign(e.Grad) * grad);
                rest -= jetzt;
            }
            cur = lage;
        }
        if (Vector2.Distance(cur, plan.Ende) >= GeradeMindestens)
        {
            liste.Add(([cur, plan.Ende], 0, 0));
        }
        else if (liste.Count > 0 && liste[^1].Item2 == 0)
        {
            liste[^1].Item1[^1] = plan.Ende;
        }
        return liste;
    }
}
