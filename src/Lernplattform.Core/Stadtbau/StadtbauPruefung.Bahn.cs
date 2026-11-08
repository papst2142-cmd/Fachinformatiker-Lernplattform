using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle K2 (A 0.49.0 / S 0.15.0, Weisung P12-72, Plan Kurven P12-36 Abschnitte 2 und 5, Nico 2 = a: Bahn setzbar mit
/// Radius 3 oder 4,5, ohne Weiche): Regeln fuer die Bahn, alle rot. Die Regeln 1 und 2 gelten fuer jede Bahn, die Regeln 3
/// bis 5 nur fuer eine Bahn aus dem Werkzeug (sie hat immer das Feld <c>radien</c>); das Gleis der Stadt 1 (Fassung 0, ohne
/// <c>radien</c>) hat kurze Stuecke mit engeren Boegen und bleibt so, wie es seit Phase 7 im Spiel ist.
/// <list type="number">
/// <item>Daten: weniger als zwei Punkte, ein Punkt nicht auf der Karte.</item>
/// <item>Zwei Gleise (oder ein Gleis mit sich selbst) kreuzen sich oder liegen naeher als der doppelte Lichtraum
/// (<see cref="GleisAbstand"/>): Weichen gibt es nicht. Erlaubt ist nur Ende an Ende in gleicher Richtung (bis
/// <see cref="StadtSzene.AnschlussGrad"/> Grad Knick), auch ein geschlossener Ring.</item>
/// <item>Ein Bogen ist enger gezeichnet als <see cref="Strassenkurve.BahnMindestens"/> (die Stuecke an der Ecke sind zu kurz
/// fuer den gewuenschten Radius; die Wagen ragten ueber die Schienen).</item>
/// <item>Die Bahn fuehrt durch Wasser (Fluss oder Teich samt Ufer): eine Bruecke fuer die Bahn gibt es nicht.</item>
/// <item>Eine gerade Strasse quert das Gleis flacher als <see cref="UebergangMindestGrad"/> Grad: der Bahnuebergang waere
/// lang und schief, die Warnkreuze stuenden im Weg.</item>
/// </list>
/// Fassung 0 hat hier keinen Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>So weit liegen zwei Gleise mindestens auseinander (Mitte zu Mitte): zweimal der Lichtraum.</summary>
    public const float GleisAbstand = 2 * Bahnuebergang.LichtraumHalb;

    /// <summary>So steil muss eine Strasse das Gleis mindestens queren (Grad).</summary>
    public const double UebergangMindestGrad = 45;

    /// <summary>Gleisenden, die naeher als dies beieinander liegen, gelten als verbunden.</summary>
    private const float GleisStoss = 0.05f;

    /// <summary>Regel 1 (in <see cref="DatenPruefen"/>): Punkte einer Bahn.</summary>
    private static void BahnDaten(PyObjekt l, int nr, double breite, double hoehe, List<StadtbauFund> funde)
    {
        var punkte = ListeOderLeer(l, "punkte").OfType<List<object?>>().ToList();
        if (punkte.Count < 2)
        {
            funde.Add(new StadtbauFund($"Bahn Nr. {nr} hat weniger als zwei Punkte.", "Bahn löschen und neu setzen.", true));
            return;
        }
        foreach (var p in punkte)
        {
            var (x, y) = (Py.F(p[0]), Py.F(p[1]));
            if (x < 0 || y < 0 || x > breite || y > hoehe)
            {
                funde.Add(new StadtbauFund($"Bahn Nr. {nr} reicht über den Rand der Karte ({Zf(x)}; {Zf(y)}).", "Bahn zurück auf die Karte schieben.", true, x, y));
                return;
            }
        }
    }

    /// <summary>Regeln 2 bis 5 auf der gebauten Szene.</summary>
    private static void BahnRegeln(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        var welt = Objekt(fassung.Karte["welt"]);
        var land = ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().ToList();
        var bahnen = land.Where(l => Py.Gleich(l["typ"], "bahn")).Select((l, i) => (Nr: i + 1, Daten: l, Ecken: Linie(l["punkte"]), Glatt: StadtSzene.Linienzug(l)))
            .Where(b => b.Ecken.Count >= 2).ToList();
        if (bahnen.Count == 0)
        {
            return;
        }

        // 2: Gleise kreuzen sich oder liegen zu nah (andere Bahn)
        for (var i = 0; i < bahnen.Count; i++)
        {
            for (var j = i + 1; j < bahnen.Count; j++)
            {
                var (a, b) = (bahnen[i], bahnen[j]);
                var stoesse = new List<Vector2>();
                foreach (var (ea, ha) in Enden(a.Ecken))
                {
                    foreach (var (eb, hb) in Enden(b.Ecken))
                    {
                        if (Vector2.Distance(ea, eb) > GleisStoss)
                        {
                            continue;
                        }
                        stoesse.Add(ea);
                        var knick = MathF.Acos(Math.Clamp(Vector2.Dot(ha, -hb), -1f, 1f)) * 180 / MathF.PI;
                        if (knick > StadtSzene.AnschlussGrad)
                        {
                            funde.Add(new StadtbauFund($"Bahn Nr. {a.Nr} und Bahn Nr. {b.Nr} stoßen mit einem Knick von {Zf(Math.Round(knick))}° aneinander.",
                                "Ein Ende antippen und in die Richtung des anderen Gleises legen (mit „Einrasten“ geht das von selbst).", true, ea.X, ea.Y));
                        }
                    }
                }
                var zuNah = Abgetastet(a.Glatt).FirstOrDefault(p => stoesse.All(s => Vector2.Distance(p, s) > 2 * GleisAbstand) && Kurve.Abstand(p, b.Glatt) < GleisAbstand - Toleranz);
                if (zuNah != default)
                {
                    funde.Add(new StadtbauFund($"Bahn Nr. {a.Nr} und Bahn Nr. {b.Nr} kreuzen sich oder liegen zu nah beieinander (weniger als {Zf(GleisAbstand)}). Weichen gibt es nicht.",
                        "Ein Gleis verschieben oder beide Ende an Ende verbinden.", true, zuNah.X, zuNah.Y));
                }
            }
        }

        foreach (var b in bahnen)
        {
            // 2: ein Gleis kreuzt sich selbst (ein geschlossener Ring ist erlaubt)
            if (SelbstKreuzung(b.Glatt, Vector2.Distance(b.Ecken[0], b.Ecken[^1]) <= GleisStoss) is { } selbst)
            {
                funde.Add(new StadtbauFund($"Bahn Nr. {b.Nr} kreuzt sich selbst oder kommt sich zu nah (weniger als {Zf(GleisAbstand)}). Weichen gibt es nicht.",
                    "Einen Eckpunkt verschieben, bis das Gleis frei läuft.", true, selbst.X, selbst.Y));
            }
            if (StadtSzene.Radien(b.Daten) is null)
            {
                continue;   // das Gleis der Stadt 1 (Fassung 0): nur die Regeln 1 und 2
            }

            // 3: Bogen enger gezeichnet als 3
            foreach (var (ecke, r) in Kurve.Boegen(b.Ecken, StadtSzene.Radien(b.Daten)))
            {
                if (r < Strassenkurve.BahnMindestens - 1e-3f)
                {
                    funde.Add(new StadtbauFund($"Der Bogen von Bahn Nr. {b.Nr} an der Ecke ({Zf(ecke.X)}; {Zf(ecke.Y)}) ist enger als {Zf(Strassenkurve.BahnMindestens)} gezeichnet (Radius {Zf(Math.Round(r, 2))}): die Stücke daneben sind zu kurz.",
                        "Die Ecke flacher ziehen oder die Stücke daneben länger machen.", true, ecke.X, ecke.Y));
                }
            }

            // 4: durch Wasser
            var nass = Abgetastet(b.Glatt).FirstOrDefault(p => szene.Fluesse.Any(f => Kurve.Abstand(p, f) < FlussUferHalb + (StadtSzene.GleisBreite / 2) - Toleranz)
                || szene.Teiche.Any(t => Wasser.Drin(t.MitUfer, p) || Kurve.Abstand(p, [.. t.MitUfer, t.MitUfer[0]]) < (StadtSzene.GleisBreite / 2) - Toleranz));
            if (nass != default)
            {
                funde.Add(new StadtbauFund($"Bahn Nr. {b.Nr} führt durch Wasser. Eine Brücke für die Bahn gibt es nicht.", "Gleis um das Wasser herum legen.", true, nass.X, nass.Y));
            }

            // 5: Strasse quert zu flach
            var nr = 0;
            foreach (var s in land.Where(l => Py.Gleich(l["typ"], "strasse")))
            {
                nr++;
                if (Strassenkurve.IstKurve(s) || Kreisverkehr.IstRing(s))
                {
                    continue;   // Kurven und Ringe pruefen die Regeln der Kurven und des Kreisverkehrs
                }
                foreach (var (mitte, _, sinus) in StadtSzene.Kreuzungen(b.Glatt, Linie(s["punkte"])))
                {
                    var grad = Math.Asin(Math.Clamp(sinus, 0, 1)) * 180 / Math.PI;
                    if (grad < UebergangMindestGrad - 1e-3)
                    {
                        funde.Add(new StadtbauFund($"Straße Nr. {nr} quert Bahn Nr. {b.Nr} zu flach ({Zf(Math.Round(grad))}°, mindestens {Zf(UebergangMindestGrad)}°).",
                            "Straße oder Gleis drehen, bis sie sich fast rechtwinklig kreuzen.", true, mitte.X, mitte.Y));
                    }
                }
            }
        }
    }

    /// <summary>Beide Enden eines Linienzugs mit der Richtung hinaus.</summary>
    private static IEnumerable<(Vector2 Ende, Vector2 Hinaus)> Enden(List<Vector2> l)
    {
        var a = l.FindIndex(p => Vector2.DistanceSquared(p, l[0]) > 1e-8f);
        var b = l.FindLastIndex(p => Vector2.DistanceSquared(p, l[^1]) > 1e-8f);
        if (a < 0 || b < 0)
        {
            yield break;
        }
        yield return (l[0], Vector2.Normalize(l[0] - l[a]));
        yield return (l[^1], Vector2.Normalize(l[^1] - l[b]));
    }

    /// <summary>
    /// Erster Punkt, an dem das Gleis sich selbst naeher als <see cref="GleisAbstand"/> kommt, gemessen nur zwischen Stellen,
    /// die laengs mindestens <c>3</c> auseinander liegen (im engsten Bogen, Radius 3, ist die Sehne dann groesser als der
    /// Abstand); bei einem Ring zaehlt der kuerzere Weg ueber den Stoss.
    /// </summary>
    private static Vector2? SelbstKreuzung(List<Vector2> glatt, bool ring)
    {
        const float Schritt = 0.2f;
        const float Laengs = 3f;
        var proben = new List<(Vector2 P, float S)>();
        var s = 0f;
        for (var i = 0; i + 1 < glatt.Count; i++)
        {
            var l = Vector2.Distance(glatt[i], glatt[i + 1]);
            var n = Math.Max(1, (int)MathF.Ceiling(l / Schritt));
            for (var k = 0; k < n; k++)
            {
                proben.Add((Vector2.Lerp(glatt[i], glatt[i + 1], k / (float)n), s + (l * k / n)));
            }
            s += l;
        }
        proben.Add((glatt[^1], s));
        for (var i = 0; i < proben.Count; i++)
        {
            for (var j = i + 1; j < proben.Count; j++)
            {
                var laengs = proben[j].S - proben[i].S;
                if (ring)
                {
                    laengs = MathF.Min(laengs, s - laengs);
                }
                if (laengs >= Laengs && Vector2.Distance(proben[i].P, proben[j].P) < GleisAbstand - Toleranz)
                {
                    return proben[i].P;
                }
            }
        }
        return null;
    }
}
