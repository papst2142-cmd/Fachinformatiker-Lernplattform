using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle K1 (A 0.44.0 / S 0.10.0, Weisung P12-55, Plan P12-36 Abschnitt 5): die Regeln 1 bis 9 fuer Kurven, gerechnet auf
/// den Daten der Fassung und der Szene wie im Spiel:
/// <list type="number">
/// <item>Radius nicht in der Liste (Kurve <see cref="Strassenkurve.Erlaubt"/> oder gezogener Bogen
/// <see cref="Strassenkurve.ZugBogen"/> (Welle Z: 15 bis 90 Grad in 7,5-Grad-Schritten, Radius 1, 2, 3), Fluss <see cref="Strassenkurve.FlussRadien"/>,
/// Bahn <see cref="Strassenkurve.BahnRadien"/>; Feld <c>radien</c> mit so vielen Zahlen wie Punkten);</item>
/// <item>Kreuzung oder Einmuendung in einer Kurve (nur an ihren Enden darf eine Strasse anschliessen);</item>
/// <item>Bahnuebergang in einer Kurve: eine Kurve kreuzt das Gleis, oder eine Strasse kreuzt einen Bogen, dessen Radius im
/// Werkzeug gesetzt wurde (Feld <c>radien</c>; die Talbahn der Stadt 1 kreuzt ihre Strassen seit Phase 7 in flachen
/// Boegen mit Radius 3, das bleibt erlaubt);</item>
/// <item>Bahnradius unter <see cref="Strassenkurve.BahnMindestens"/>;</item>
/// <item>das Ufer eines Flusses ueberlappt sich (gezeichneter Radius einer Ecke kleiner als das halbe Ufer);</item>
/// <item>Kurve ueber Haus, Baum, Laterne oder Katalog-Stueck (Haus und Katalog-Stueck pruefen die Regeln fuer jede
/// Strasse schon, hier kommen Baum und Laterne dazu);</item>
/// <item>zwei Strassen aufeinander, auch entlang des Bogens (<see cref="StrassenNaehe"/> misst Linienzuege);</item>
/// <item>Bruecke nur, wo eine gerade Strasse den Fluss quert;</item>
/// <item>gelb: ein offenes Kurvenende liegt neben einer Strasse, schliesst aber nicht an;</item>
/// <item>(10, Patch A 0.46.1) ein Kurvenende liegt genau auf dem Ende einer anderen Strasse, aber quer (kein Anschluss nach
/// <see cref="StadtSzene.Anschluss"/>).</item>
/// </list>
/// Fassung 0 hat keine Kurve und kein Feld <c>radien</c>: hier kein Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>So nah (Mittellinie an Mittellinie) zaehlt eine Strasse als angeschlossen.</summary>
    public const float AnschlussGrenze = 0.05f;

    /// <summary>Bis so weit neben einer Strasse zeigt ein offenes Kurvenende gelb „schließt nicht an“.</summary>
    public const float OffenBis = 1.0f;

    /// <summary>
    /// Regel 1 und 4 auf den Daten, vor dem Bau der Szene. false = das Feld <c>radien</c> ist so kaputt, dass die Szene
    /// sich nicht bauen laesst (keine Liste, falsche Zahl von Werten, keine Zahl).
    /// </summary>
    private static bool KurvenDaten(PyObjekt welt, List<StadtbauFund> funde)
    {
        var heil = true;
        var nr = 0;
        var flussNr = 0;
        var bahnNr = 0;
        foreach (var l in ListeOderLeer(welt, "landschaft").OfType<PyObjekt>())
        {
            var typ = l["typ"] as string;
            if (typ == "strasse")
            {
                nr++;
                if (Py.Hat(l, Strassenkurve.Feld) && !Strassenkurve.Gueltig(l[Strassenkurve.Feld]))
                {
                    var p = Linie(l["punkte"]).FirstOrDefault();
                    funde.Add(new StadtbauFund($"Die Kurve (Straße Nr. {nr}) hat eine Form oder einen Radius, die es nicht gibt. Erlaubt: Kurve 90° mit Radius 1, 2 oder 3, Kurve 45° und S-Kurve mit Radius 2, gezogene Bögen von 15 bis 90° in Schritten von 7,5° mit Radius 1, 2 oder 3.",
                        "Kurve löschen und aus der Liste links neu setzen.", true, p.X, p.Y));
                }
            }
            if (typ is not ("fluss" or "bahn"))
            {
                continue;
            }
            flussNr += typ == "fluss" ? 1 : 0;
            bahnNr += typ == "bahn" ? 1 : 0;
            var name = typ == "fluss" ? $"Der Fluss Nr. {flussNr}" : $"Bahn Nr. {bahnNr}";
            if (!Py.Hat(l, Strassenkurve.RadienFeld))
            {
                continue;
            }
            var punkte = Linie(l["punkte"]);
            if (l[Strassenkurve.RadienFeld] is not List<object?> radien || radien.Count != punkte.Count || radien.Any(r => r is not (long or double)))
            {
                funde.Add(new StadtbauFund($"{name} hat ein falsch gespeichertes Feld „radien“ (je Eckpunkt eine Zahl).", "Letzte Änderung rückgängig machen.", true));
                heil = false;
                continue;
            }
            var erlaubt = typ == "fluss" ? Strassenkurve.FlussRadien : Strassenkurve.BahnRadien;
            for (var i = 1; i + 1 < punkte.Count; i++)
            {
                var r = Py.F(radien[i]);
                if (typ == "bahn" && r < Strassenkurve.BahnMindestens)
                {
                    funde.Add(new StadtbauFund($"Bahn Nr. {bahnNr} hat an Eckpunkt {i + 1} den Radius {Zf(r)}, kleiner als {Zf(Strassenkurve.BahnMindestens)}: die Wagen ragen dort über die Schienen.",
                        "Radius 3 oder 4,5 wählen.", true, punkte[i].X, punkte[i].Y));
                }
                else if (!erlaubt.Any(e => Math.Abs(e - r) < 1e-9))
                {
                    funde.Add(new StadtbauFund($"{name} hat an Eckpunkt {i + 1} den Radius {Zf(r)}, erlaubt sind {string.Join(", ", erlaubt.Select(Zf))}.",
                        "Eckpunkt antippen und einen erlaubten Radius wählen.", true, punkte[i].X, punkte[i].Y));
                }
            }
        }
        return heil;
    }

    /// <summary>Regeln 2, 3, 5, 6 (Baum, Laterne), 8 und 9 auf der gebauten Szene.</summary>
    private static void KurvenRegeln(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        var welt = Objekt(fassung.Karte["welt"]);
        var land = ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().ToList();
        var strassen = land.Where(l => Py.Gleich(l["typ"], "strasse")).Select(l => (Daten: l, Linie: Linie(l["punkte"]))).ToList();

        // 5: Ufer ueberlappt sich (gezeichneter Radius kleiner als das halbe Ufer)
        var flussNr = 0;
        foreach (var f in land.Where(l => Py.Gleich(l["typ"], "fluss")))
        {
            flussNr++;
            foreach (var (ecke, r) in Kurve.Boegen(Linie(f["punkte"]), StadtSzene.Radien(f)))
            {
                if (r < FlussUferHalb - 1e-4f)
                {
                    funde.Add(new StadtbauFund($"Das Ufer von Fluss Nr. {flussNr} überlappt sich an der Ecke ({Zf(ecke.X)}; {Zf(ecke.Y)}): der Bogen ist dort zu eng.",
                        "Ecke flacher ziehen, die Stücke daneben länger machen oder einen größeren Radius wählen.", true, ecke.X, ecke.Y));
                }
            }
        }

        // 3: Bahnuebergang in einer Bahnkurve, deren Radius im Werkzeug gesetzt wurde
        foreach (var b in land.Where(l => Py.Gleich(l["typ"], "bahn") && StadtSzene.Radien(l) is not null))
        {
            var glatt = StadtSzene.Linienzug(b);
            var gerade = Linie(b["punkte"]);
            foreach (var (s, nr) in strassen.Select((s, i) => (s, i + 1)))
            {
                foreach (var q in Schnittpunkte(s.Linie, glatt))
                {
                    if (!AufGeradem(q, glatt, gerade))
                    {
                        funde.Add(new StadtbauFund($"Straße Nr. {nr} kreuzt die Bahn in einer Kurve. Ein Bahnübergang geht nur auf einem geraden Stück.",
                            "Straße auf ein gerades Stück des Gleises schieben.", true, q.X, q.Y));
                    }
                }
            }
        }

        for (var i = 0; i < strassen.Count; i++)
        {
            if (!Strassenkurve.IstKurve(strassen[i].Daten) || strassen[i].Linie.Count < 2)
            {
                continue;
            }
            var kurve = strassen[i].Linie;
            var nr = i + 1;
            var halb = (StadtSzene.StrassenBreite / 2) + StadtSzene.GehwegBreite(strassen[i].Daten);

            // 2: Kreuzung oder Einmuendung im Bogen (eine andere Strasse beruehrt die Mittellinie fern der beiden Enden)
            for (var j = 0; j < strassen.Count; j++)
            {
                if (j == i || strassen[j].Linie.Count < 2)
                {
                    continue;
                }
                if (Beruehrungen(strassen[j].Linie, kurve)
                    .FirstOrDefault(q => Vector2.Distance(q, kurve[0]) > AnschlussGrenze && Vector2.Distance(q, kurve[^1]) > AnschlussGrenze) is var q
                    && q != default)
                {
                    funde.Add(new StadtbauFund($"Straße Nr. {j + 1} kreuzt die Kurve (Straße Nr. {nr}) oder mündet in ihren Bogen. Kreuzungen und Einmündungen gehen nur auf geraden Stücken.",
                        "Die Straße an ein Ende der Kurve oder an ein gerades Stück legen.", true, q.X, q.Y));
                }
            }

            // 3: die Kurve kreuzt ein Gleis
            foreach (var g in szene.Gleise)
            {
                foreach (var q in Schnittpunkte(kurve, g).Take(1))
                {
                    funde.Add(new StadtbauFund($"Die Kurve (Straße Nr. {nr}) kreuzt die Bahn. Ein Bahnübergang geht nur, wo eine gerade Straße das Gleis quert.",
                        "Kurve vom Gleis weg schieben, dort eine gerade Straße bauen.", true, q.X, q.Y));
                }
            }

            // 8: die Kurve kreuzt den Fluss (Bruecken nur an geraden Strassen)
            foreach (var f in szene.Fluesse)
            {
                foreach (var q in Schnittpunkte(kurve, f).Take(1))
                {
                    funde.Add(new StadtbauFund($"Die Kurve (Straße Nr. {nr}) führt über den Fluss. Eine Brücke gibt es nur, wo eine gerade Straße den Fluss quert.",
                        "Kurve vom Fluss weg schieben, über den Fluss eine gerade Straße bauen.", true, q.X, q.Y));
                }
            }

            // 6: Baum oder Laterne auf der Kurve (Fahrbahn samt Gehweg bzw. Fahrbahn)
            foreach (var baum in land.Where(l => Py.Gleich(l["typ"], "baum")))
            {
                foreach (var p in Linie(baum["punkte"]).Where(p => Kurve.Abstand(p, kurve) < halb).Take(1))
                {
                    funde.Add(new StadtbauFund($"Ein Baum steht auf der Kurve (Straße Nr. {nr}).", "Baum neben die Kurve setzen.", true, p.X, p.Y));
                }
            }
            var lnr = 0;
            foreach (var (x, z, _) in fassung.Laternen)
            {
                lnr++;
                if (Kurve.Abstand(new Vector2(x, z), kurve) < StadtSzene.StrassenBreite / 2)
                {
                    funde.Add(new StadtbauFund($"Laterne Nr. {lnr} steht auf der Fahrbahn der Kurve (Straße Nr. {nr}).", "Laterne an den Rand der Kurve setzen.", true, x, z));
                }
            }

            // 10 (Patch A 0.46.1, Sammelliste 233): ein Ende liegt genau auf dem Ende einer anderen Strasse, aber quer (mehr
            // als StadtSzene.AnschlussGrad): kein Anschluss, die Mittellinie fehlt, die Gehwege ueberlappen
            var alleLinien = strassen.Select(s => s.Linie).ToList();
            foreach (var anfang in new[] { true, false })
            {
                var ende = anfang ? kurve[0] : kurve[^1];
                var hinaus = Vector2.Normalize(anfang ? kurve[0] - kurve[1] : kurve[^1] - kurve[^2]);
                if (StadtSzene.Anschluss(ende, hinaus, kurve, alleLinien) is not null)
                {
                    continue;
                }
                foreach (var (s, j) in strassen.Select((s, j) => (s, j)).Where(x => x.j != i && x.s.Linie.Count >= 2 && !Kreisverkehr.IstRing(x.s.Daten)))
                {
                    var amAnfang = Vector2.Distance(s.Linie[0], ende) <= 0.02f;
                    if (!amAnfang && Vector2.Distance(s.Linie[^1], ende) > 0.02f)
                    {
                        continue;
                    }
                    var hinein = Vector2.Normalize(amAnfang ? s.Linie[1] - s.Linie[0] : s.Linie[^2] - s.Linie[^1]);
                    var grad = Math.Round(Math.Acos(Math.Clamp(Vector2.Dot(hinaus, hinein), -1, 1)) * 180 / Math.PI);
                    funde.Add(new StadtbauFund($"Die Kurve (Straße Nr. {nr}) stößt quer an das Ende von Straße Nr. {j + 1} (die Richtungen weichen um {grad:0} Grad ab): dort fehlt die Mittellinie und die Gehwege überlappen.",
                        "Kurve drehen oder neu an das Ende ziehen, sie rastet dann in Richtung ein.", true, ende.X, ende.Y));
                    break;
                }
            }

            // 9 (gelb): offenes Ende neben einer Strasse
            foreach (var ende in new[] { kurve[0], kurve[^1] })
            {
                var andere = strassen.Select((s, k) => (s.Linie, Nr: k + 1)).Where(s => s.Nr != nr && s.Linie.Count >= 2)
                    .Select(s => (s.Nr, D: Kurve.Abstand(ende, s.Linie))).OrderBy(s => s.D).FirstOrDefault();
                if (andere.Nr > 0 && andere.D > AnschlussGrenze && andere.D <= OffenBis)
                {
                    funde.Add(new StadtbauFund($"Ein Ende der Kurve (Straße Nr. {nr}) schließt nicht an Straße Nr. {andere.Nr} an ({Zf(andere.D)} daneben).",
                        "Kurve an das Ende der Straße schieben, sie rastet dort ein.", false, ende.X, ende.Y));
                }
            }
        }
    }

    /// <summary>Stellen auf <paramref name="b"/>, an denen <paramref name="a"/> die Mittellinie kreuzt oder beruehrt (abgetastet alle 0,02).</summary>
    private static IEnumerable<Vector2> Beruehrungen(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
    {
        foreach (var q in Schnittpunkte(a, b))
        {
            yield return q;
        }
        for (var i = 0; i + 1 < a.Count; i++)
        {
            var laenge = Vector2.Distance(a[i], a[i + 1]);
            var schritte = Math.Max(1, (int)MathF.Ceiling(laenge / 0.02f));
            for (var k = 0; k <= schritte; k++)
            {
                var q = Vector2.Lerp(a[i], a[i + 1], k / (float)schritte);
                if (Kurve.Abstand(q, b) <= AnschlussGrenze)
                {
                    yield return Naechster(q, b);
                }
            }
        }
    }

    private static Vector2 Naechster(Vector2 q, IReadOnlyList<Vector2> p)
    {
        var best = p[0];
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var d = p[i + 1] - p[i];
            var l2 = d.LengthSquared();
            var t = l2 < 1e-12f ? 0 : Math.Clamp(Vector2.Dot(q - p[i], d) / l2, 0f, 1f);
            var f = p[i] + (d * t);
            if (Vector2.Distance(q, f) < Vector2.Distance(q, best))
            {
                best = f;
            }
        }
        return best;
    }

    /// <summary>Schnittpunkte zweier Linienzuege (echte Kreuzungen der Stuecke).</summary>
    private static IEnumerable<Vector2> Schnittpunkte(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
    {
        for (var i = 0; i + 1 < a.Count; i++)
        {
            for (var j = 0; j + 1 < b.Count; j++)
            {
                var (p, r) = (a[i], a[i + 1] - a[i]);
                var (q, s) = (b[j], b[j + 1] - b[j]);
                var nenner = (r.X * s.Y) - (r.Y * s.X);
                if (MathF.Abs(nenner) < 1e-9f)
                {
                    continue;
                }
                var t = (((q.X - p.X) * s.Y) - ((q.Y - p.Y) * s.X)) / nenner;
                var u = (((q.X - p.X) * r.Y) - ((q.Y - p.Y) * r.X)) / nenner;
                if (t is >= 0 and <= 1 && u is >= 0 and <= 1)
                {
                    yield return p + (r * t);
                }
            }
        }
    }

    /// <summary>Liegt der Punkt des geglaetteten Gleises auf einem geraden Stueck (nicht in einem Bogen)?</summary>
    private static bool AufGeradem(Vector2 q, IReadOnlyList<Vector2> glatt, IReadOnlyList<Vector2> gerade)
    {
        // auf einem geraden Stueck liegt der Punkt auf der Strecke zweier Eckpunkte des ungeglaetteten Linienzugs
        // und weit genug von beiden Bogenanfaengen (der Bogen weicht dort von der Strecke ab)
        for (var i = 0; i + 1 < gerade.Count; i++)
        {
            var d = gerade[i + 1] - gerade[i];
            var l2 = d.LengthSquared();
            if (l2 < 1e-10f)
            {
                continue;
            }
            var t = Math.Clamp(Vector2.Dot(q - gerade[i], d) / l2, 0f, 1f);
            if (Vector2.Distance(q, gerade[i] + (d * t)) > 0.005f)
            {
                continue;
            }
            // Abstand zum naechsten Bogen im geglaetteten Linienzug: jeder Punkt des Bogens weicht von der Strecke ab
            var abseits = glatt.Where(g => Kurve.Abstand(g, [gerade[i], gerade[i + 1]]) > 0.005f);
            var frei = (StadtSzene.StrassenBreite / 2) + 0.3f;
            return abseits.All(g => Vector2.Distance(g, q) > frei);
        }
        return false;
    }
}
