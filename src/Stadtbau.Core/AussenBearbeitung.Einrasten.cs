using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Patch S 0.12.1 (Weisungen P12-62, P12-66, Sammelliste 233, Fund Nico): Strassenenden rasten mit Lage UND Richtung ein.
/// Vorher rastete eine Gerade mit dem Fusspunkt auf jede Linie ein, auch mitten in den Bogen einer Kurve (sie ragte hinein,
/// die Mittellinie brach ab), eine Kurve fing nur bis 0,5 und drehte sich nie (sie konnte quer anschliessen).
/// <list type="bullet">
/// <item>Ein Ende rastet bis <see cref="EndeFangweite"/> (eine Strassenbreite) nur an ein freies Ende einer anderen Strasse
/// (nie an den Ring eines Kreisverkehrs, nie an ein Ende, an dem schon eine dritte Strasse anschliesst).</item>
/// <item>Eine Kurve dreht sich dabei um das eingerastete Ende auf die Richtung des Gegenstuecks, wenn die Abweichung
/// hoechstens <see cref="KurveDrehtBis"/> Grad betraegt; eine Gerade legt sich in die Richtung des Gegenstuecks (gleiche
/// Laenge), wenn die Abweichung hoechstens <see cref="GeradeDrehtBis"/> Grad betraegt. Sonst rastet nichts an diesem Ende.</item>
/// <item>Die Richtung einer Kurve an ihrem Ende ist die Tangente des Bogens (die erste Sehne weicht um die Haelfte des
/// Schritts von 7,5 Grad ab), darum liegt die Kurve danach genau in Richtung der Geraden.</item>
/// <item>Findet eine Gerade kein passendes Ende, rastet sie wie bisher mit dem Fusspunkt ein (Einmuendung), aber nur auf
/// Strassen ohne Bogen (Kreuzungen und Einmuendungen gehen nur auf geraden Stuecken, Regel 2 der Kurven).</item>
/// </list>
/// Die Vorschau beim Ziehen (2D und 3D) rechnet denselben Schritt auf einer Kopie, sie zeigt also genau das Ergebnis.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>So nah muss ein Strassenende an einem freien Ende liegen, damit es dort einrastet (eine Strassenbreite).</summary>
    public const double EndeFangweite = 0.95;

    /// <summary>Hoechstens so viele Grad dreht sich eine Kurve beim Einrasten.</summary>
    public const double KurveDrehtBis = 45;

    /// <summary>Hoechstens so viele Grad dreht sich eine Gerade beim Einrasten.</summary>
    public const double GeradeDrehtBis = 15;

    /// <summary>Unter so vielen Grad Abweichung gilt die Richtung als gleich (Rundung der Punkte auf 0,01).</summary>
    public const float RundungGrad = 0.5f;

    /// <summary>Nur fuer die Gegenprobe in Tests: Einrasten wie vor dem Patch (Fusspunkt auf jeder Linie, Kurve 0,5 ohne Drehen).</summary>
    internal static bool EinrastenAltFuerTests { get => AltFuerTests.Value; set => AltFuerTests.Value = value; }

    // je Ablauf (Test), damit parallel laufende Tests den Schalter nicht sehen
    private static readonly AsyncLocal<bool> AltFuerTests = new();

    /// <summary>
    /// Richtung, in die die Strasse an ihrem Ende hinaus zeigt (Einheitsvektor). Gerade: die Strasse selbst. Bogen (Punkte
    /// alle <see cref="Strassenkurve.SchrittGrad"/> Grad): die Tangente, gerechnet aus der Sehne zum k-ten Punkt (k bis 6, noch im
    /// ersten Bogen auch der S-Kurve), die um k halbe Schritte von der Tangente abweicht. Die lange Sehne haelt den
    /// Rundungsfehler der Punkte (0,01) unter 0,3 Grad; mit der ersten kurzen Sehne allein waeren es ueber 1 Grad.
    /// </summary>
    public static Vector2 EndRichtung(IReadOnlyList<Vector2> l, bool anfang)
    {
        var p = anfang ? l : [.. l.Reverse()];
        var vorwaerts = p[1] - p[0];
        if (p.Count >= 3)
        {
            var k = Math.Min(p.Count - 1, 6);
            var seite = MathF.Sign(Winkel(p[1] - p[0], p[2] - p[1]));
            vorwaerts = Gedreht(p[k] - p[0], -seite * k * (float)(Strassenkurve.SchrittGrad / 2 * Math.PI / 180));
        }
        return -Vector2.Normalize(vorwaerts);
    }

    /// <summary>Vorzeichenbehafteter Winkel von <paramref name="a"/> nach <paramref name="b"/> im Bogenmass.</summary>
    private static float Winkel(Vector2 a, Vector2 b) => MathF.Atan2((a.X * b.Y) - (a.Y * b.X), Vector2.Dot(a, b));

    private static Vector2 Gedreht(Vector2 v, float w)
    {
        var (s, c) = MathF.SinCos(w);
        return new Vector2((v.X * c) - (v.Y * s), (v.X * s) + (v.Y * c));
    }

    /// <summary>Freie Enden aller anderen Strassen: Lage und Richtung nach aussen.</summary>
    private static List<(Vector2 Ende, Vector2 Hinaus)> FreieEnden(PyObjekt aussen, PyObjekt eigene)
    {
        var strassen = Aussen.Landschaft(aussen).OfType<PyObjekt>()
            .Where(t => !ReferenceEquals(t, eigene) && Py.Gleich(t["typ"], "strasse") && !Kreisverkehr.IstRing(t))
            .Select(t => Linie(t["punkte"])).Where(t => t.Count >= 2 && Vector2.Distance(t[0], t[^1]) > 1e-3f).ToList();
        var enden = new List<(Vector2, Vector2)>();
        for (var i = 0; i < strassen.Count; i++)
        {
            foreach (var anfang in new[] { true, false })
            {
                var e = anfang ? strassen[i][0] : strassen[i][^1];
                // schon belegt: an diesem Ende schliesst eine weitere Strasse an
                var belegt = strassen.Where((_, k) => k != i).Any(t => Vector2.Distance(t[0], e) <= 0.02f || Vector2.Distance(t[^1], e) <= 0.02f);
                if (!belegt)
                {
                    enden.Add((e, EndRichtung(strassen[i], anfang)));
                }
            }
        }
        return enden;
    }

    /// <summary>
    /// Bestes Gegenstueck fuer die Enden einer Linie: naechstes freies Ende bis <see cref="EndeFangweite"/>, dessen Richtung
    /// hoechstens <paramref name="bisGrad"/> abweicht. Drehung = Winkel, um den die Linie um ihr Ende gedreht werden muss.
    /// </summary>
    private static (bool Anfang, Vector2 Ende, Vector2 Ziel, float Drehung, float Abstand)? Gegenstueck(PyObjekt aussen, PyObjekt eigene,
        List<Vector2> linie, double bisGrad)
    {
        (bool, Vector2, Vector2, float, float)? bester = null;
        foreach (var (ziel, hinaus) in FreieEnden(aussen, eigene))
        {
            foreach (var anfang in new[] { true, false })
            {
                var ende = anfang ? linie[0] : linie[^1];
                var d = Vector2.Distance(ende, ziel);
                // eigene Richtung hinaus soll der Richtung des Gegenstuecks entgegen laufen
                var drehung = Winkel(EndRichtung(linie, anfang), -hinaus);
                // kleine Abweichung (Rundung der Punkte) nicht drehen, sonst wandert die Kurve bei jedem Einrasten
                drehung = Math.Abs(drehung) < RundungGrad * MathF.PI / 180 ? 0 : drehung;
                if (d <= EndeFangweite + 1e-4 && Math.Abs(drehung) <= (bisGrad * Math.PI / 180) + 1e-4
                    && (bester is not { } b || d < b.Item5 - 1e-4f || (Math.Abs(d - b.Item5) <= 1e-4f && Math.Abs(drehung) < Math.Abs(b.Item4))))
                {
                    bester = (anfang, ende, ziel, drehung, d);
                }
            }
        }
        return bester;
    }

    /// <summary>
    /// Kurve an ein freies Strassenende einrasten: verschieben und um das Ende drehen (bis <see cref="KurveDrehtBis"/> Grad).
    /// true = eingerastet (auch: lag schon genau dort).
    /// </summary>
    public static bool KurveEinrasten(PyObjekt aussen, AussenStelle s)
    {
        if (EinrastenAltFuerTests)
        {
            return KurveEinrastenAlt(aussen, s);
        }
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        var punkte = Liste(l["punkte"]);
        var linie = Linie(punkte);
        if (linie.Count < 3 || Gegenstueck(aussen, l, linie, KurveDrehtBis) is not { } g)
        {
            return false;
        }
        // die Form genau aus dem Feld "kurve" neu rechnen (Punkte vor S 0.12.1 waren auf 0,01 gerundet: die erste Sehne
        // weicht dann bis 0,65 Grad ab, zwei Kurven gleicher Drehrichtung stiessen mit 8,8 statt 7,5 Grad aneinander und
        // galten im Spiel nicht als Anschluss), dann mit dem eingerasteten Ende auf das Ziel legen
        var form = Genau(l, linie) ?? linie;
        var ende = g.Anfang ? form[0] : form[^1];
        var drehung = Winkel(EndRichtung(form, g.Anfang), Gedreht(EndRichtung(linie, g.Anfang), g.Drehung));
        for (var i = 0; i < punkte.Count; i++)
        {
            punkte[i] = KurvenPunkt(g.Ziel + Gedreht(form[i] - ende, drehung));
        }
        return true;
    }

    /// <summary>Die genaue Mittellinie der Kurve (Feld <c>kurve</c>) mit gleicher Drehrichtung am Anfang, oder null.</summary>
    private static List<Vector2>? Genau(PyObjekt l, List<Vector2> linie)
    {
        if (Objekt(l[Strassenkurve.Feld]) is not PyObjekt k || k["form"] is not string f || k["winkel"] is not (long or double)
            || k["radius"] is not (long or double))
        {
            return null;
        }
        var seite = MathF.Sign(Winkel(linie[1] - linie[0], linie[2] - linie[1]));
        foreach (var gespiegelt in new[] { false, true })
        {
            var m = Strassenkurve.Mittellinie(f, (int)Py.F(k["winkel"]), Py.F(k["radius"]), gespiegelt);
            if (m.Count == linie.Count && MathF.Sign(Winkel(m[1] - m[0], m[2] - m[1])) == seite)
            {
                return m;
            }
        }
        return null;
    }

    /// <summary>Punkt einer Kurve auf 0,0001 genau (gerade Strassen bleiben bei 0,01, <see cref="Bearbeitung.Runden"/>).</summary>
    internal static List<object?> KurvenPunkt(Vector2 p) => [KurvenZahl(p.X), KurvenZahl(p.Y)];

    /// <summary>Zahl einer Kurve auf 0,0001 genau.</summary>
    internal static double KurvenZahl(double wert) => Math.Round(wert, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Gerade (2 Punkte) einrasten: an ein freies Ende in dessen Richtung (bis <see cref="GeradeDrehtBis"/> Grad, Laenge
    /// bleibt), sonst jedes Ende mit dem Fusspunkt auf eine Strasse ohne Bogen (bis <see cref="EinrastenAb"/>).
    /// </summary>
    private static void GeradeEinrasten(PyObjekt aussen, PyObjekt l)
    {
        var punkte = Liste(l["punkte"]);
        var linie = Linie(punkte);
        if (EinrastenAltFuerTests)
        {
            var alle = Strassen(aussen).Where(x => !(x.Count == linie.Count && x.SequenceEqual(linie))).ToList();
            punkte[0] = PunktObjekt(Einrasten(linie[0], alle));
            punkte[^1] = PunktObjekt(Einrasten(linie[^1], alle));
            return;
        }
        if (linie.Count == 2 && Gegenstueck(aussen, l, linie, GeradeDrehtBis) is { } g)
        {
            var laenge = Vector2.Distance(linie[0], linie[1]);
            // die Gerade laeuft vom Ziel weg in die Richtung, in die das Gegenstueck hinaus zeigt
            var weg = Gedreht(-EndRichtung(linie, g.Anfang), g.Drehung) * laenge;
            var anderes = g.Ziel + weg;
            punkte[g.Anfang ? 0 : 1] = PunktObjekt(g.Ziel);
            punkte[g.Anfang ? 1 : 0] = PunktObjekt(anderes);
            return;
        }
        var gerade = GeradeLinien(aussen, l);
        punkte[0] = PunktObjekt(Einrasten(linie[0], gerade));
        punkte[^1] = PunktObjekt(Einrasten(linie[^1], gerade));
    }

    /// <summary>Mittellinien aller anderen Strassen ohne Bogen (fuer das Einrasten mit dem Fusspunkt).</summary>
    private static List<List<Vector2>> GeradeLinien(PyObjekt aussen, PyObjekt eigene) =>
        [.. Aussen.Landschaft(aussen).OfType<PyObjekt>()
            .Where(t => !ReferenceEquals(t, eigene) && Py.Gleich(t["typ"], "strasse") && !Strassenkurve.IstKurve(t))
            .Select(t => Linie(t["punkte"]))];

    /// <summary>Stand vor dem Patch (S 0.12.0): nur fuer die Gegenprobe.</summary>
    private static bool KurveEinrastenAlt(PyObjekt aussen, AussenStelle s)
    {
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        var punkte = Liste(l["punkte"]);
        var linie = Linie(punkte);
        if (linie.Count < 2)
        {
            return false;
        }
        var andere = Aussen.Landschaft(aussen).OfType<PyObjekt>()
            .Where(t => !ReferenceEquals(t, l) && Py.Gleich(t["typ"], "strasse")).Select(t => Linie(t["punkte"])).Where(t => t.Count >= 2).ToList();
        var bester = (Weg: Vector2.Zero, Abstand: float.MaxValue);
        foreach (var ende in new[] { linie[0], linie[^1] })
        {
            foreach (var a in andere)
            {
                foreach (var e in new[] { a[0], a[^1] })
                {
                    var d = Vector2.Distance(ende, e);
                    if (d <= 0.5 && d < bester.Abstand)
                    {
                        bester = (e - ende, d);
                    }
                }
            }
        }
        if (bester.Abstand == float.MaxValue || bester.Abstand < 1e-4f)
        {
            return bester.Abstand < 1e-4f;
        }
        for (var i = 0; i < punkte.Count; i++)
        {
            punkte[i] = PunktObjekt(linie[i] + bester.Weg);
        }
        return true;
    }
}
