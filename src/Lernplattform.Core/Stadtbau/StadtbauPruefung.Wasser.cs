using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// S 0.8.0 / A 0.43.4 (Weisung P12-47, Plan P12-26b Abschnitt 10): die Regeln fuer Wasser (Fluss aus der Karte, Teiche
/// und Stege aus <c>stadtdaten.wasser</c>), gerechnet auf derselben Szene wie im Spiel:
/// <list type="bullet">
/// <item>Daten: <c>wasser</c> ist ein Objekt, <c>eis</c> nur true/false, jeder Teich hat mindestens drei Punkte auf der
/// Karte, jeder Steg x, y, w, t (groesser 0) und dreh 0 bis 3;</item>
/// <item>ein Teich schneidet sich nicht selbst und ist mindestens <see cref="Wasser.Mindestens"/> breit und tief;</item>
/// <item>kein Haus, Gebaeude, Blueprint und kein Katalog-Stueck (ausser Effekten, die schweben) im Wasser oder auf dem Ufer;</item>
/// <item>keine Strasse im Teich (samt Ufer), keine Strasse laengs im Fluss (quer = Bruecke, bis <see cref="FlussQuerHoechstens"/>);</item>
/// <item>keine Laterne, kein Baum, Beet oder Park im Wasser;</item>
/// <item>ein Steg beruehrt mit mindestens einem Ende das Ufer;</item>
/// <item>jeder Ort ist zu Fuss erreichbar (Weg ueber das Laufraster vom Startpunkt eines neuen Spielstands bis vor seine Tuer).</item>
/// </list>
/// Fassung 0 (ohne <c>wasser</c>) hat hier keinen Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>
    /// Laengste Strecke einer Strasse im Band des Flusses samt Ufer (0,95 breit): eine Bruecke im rechten Winkel liegt
    /// 0,95 darin, schraeg mehr; ab hier (drei Bandbreiten, eine Kreuzung flacher als etwa 20 Grad) liegt die Strasse laengs im Fluss.
    /// </summary>
    public const float FlussQuerHoechstens = 3 * (StadtSzene.FlussBreite + 0.3f);

    /// <summary>So weit darf ein Stegende hinter dem Ufer an Land liegen (eine Zelle des Laufrasters).</summary>
    public const float StegLandHoechstens = (float)StadtSzene.LaufSchritt;

    /// <summary>Halbe Breite des Flusses mit Ufer (wie das Ufer-Band im Spiel).</summary>
    private const float FlussUferHalb = (StadtSzene.FlussBreite + 0.3f) / 2;

    private const float FlussWasserHalb = StadtSzene.FlussBreite / 2;

    private static void WasserRegeln(Stadt stadt, Spielstand stand, StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        WasserDaten(fassung, funde);
        var teiche = szene.Teiche;
        var fluesse = szene.Fluesse;
        string HausName(StadtHaus h) => szene.Ort(h.Id)?.Name ?? h.Id.Split('#')[0];

        // Teiche: Form
        var nr = 0;
        foreach (var t in teiche)
        {
            nr++;
            var p = t.Punkte;
            float bx = p.Max(q => q.X) - p.Min(q => q.X), bz = p.Max(q => q.Y) - p.Min(q => q.Y);
            var mitte = Mitte(p);
            if (bx < Wasser.Mindestens - 1e-4f || bz < Wasser.Mindestens - 1e-4f)
            {
                funde.Add(new StadtbauFund($"Teich Nr. {nr} ist kleiner als {Zf(Wasser.Mindestens)} × {Zf(Wasser.Mindestens)}.",
                    "Eckpunkte weiter auseinander ziehen.", true, mitte.X, mitte.Y));
            }
            if (Wasser.SchneidetSich(p))
            {
                funde.Add(new StadtbauFund($"Der Rand von Teich Nr. {nr} kreuzt sich selbst.", "Eckpunkte so ziehen, dass sich der Rand nicht kreuzt.", true, mitte.X, mitte.Y));
            }
        }

        // Haeuser (Orte, Gebaeude, Blueprints) nicht im Wasser oder auf dem Ufer
        foreach (var h in szene.Haeuser)
        {
            var r = (h.Min.X + Toleranz, h.Min.Z + Toleranz, h.Max.X - Toleranz, h.Max.Z - Toleranz);
            if (teiche.Any(t => RechteckImVieleck(r, t.MitUfer)) || fluesse.Any(f => RechteckLinie(r, f) < FlussUferHalb))
            {
                funde.Add(new StadtbauFund($"„{HausName(h)}“ steht im Wasser oder auf dem Ufer.", "Haus neben das Wasser schieben.", true,
                    (h.Min.X + h.Max.X) / 2, (h.Min.Z + h.Max.Z) / 2));
            }
        }
        List<Katalogstueck.Draussen> stuecke;
        try
        {
            stuecke = Katalogstueck.AusStadtdaten(fassung.Stadtdaten);
        }
        catch (InvalidDataException)
        {
            stuecke = [];   // schon ein Fund der Katalog-Regel
        }
        foreach (var k in stuecke.Where(k => !k.Effekt))
        {
            var (x1, z1, x2, z2) = k.Rechteck;
            var r = (x1 + Toleranz, z1 + Toleranz, x2 - Toleranz, z2 - Toleranz);
            if (teiche.Any(t => RechteckImVieleck(r, t.MitUfer)) || fluesse.Any(f => RechteckLinie(r, f) < FlussUferHalb))
            {
                funde.Add(new StadtbauFund($"„{k.Typ}“ steht im Wasser oder auf dem Ufer.", "Stück neben das Ufer schieben.", true, k.X, k.Z));
            }
        }

        // Strassen: nicht im Teich, nicht laengs im Fluss
        var snr = 0;
        foreach (var s in szene.Strassen)
        {
            snr++;
            nr = 0;
            foreach (var t in teiche)
            {
                nr++;
                if (LinieImVieleck(s, t.MitUfer, StadtSzene.StrassenBreite / 2) is { } p)
                {
                    funde.Add(new StadtbauFund($"Straße Nr. {snr} führt durch Teich Nr. {nr}.", "Straße um den Teich herum legen (über einen Teich baut das Spiel keine Brücke).", true, p.X, p.Y));
                }
            }
            foreach (var f in fluesse)
            {
                var (laenge, mitte) = LaengsIm(s, f, FlussUferHalb);
                if (laenge > FlussQuerHoechstens)
                {
                    funde.Add(new StadtbauFund($"Straße Nr. {snr} läuft {Zf(laenge)} lang im Fluss (quer als Brücke höchstens {Zf(FlussQuerHoechstens)}).",
                        "Straße quer über den Fluss legen oder neben den Fluss schieben.", true, mitte.X, mitte.Y));
                }
            }
        }

        // Laternen, Baeume, Beete, Parks nicht im Wasser
        bool ImWasser(Vector2 p, float r = 0f) =>
            teiche.Any(t => Wasser.Drin(t.Rund, p) || Wasser.RandAbstand(t.Rund, p) < r) || fluesse.Any(f => Kurve.Abstand(p, f) < FlussWasserHalb + r);
        nr = 0;
        foreach (var (x, z, _) in fassung.Laternen)
        {
            nr++;
            if (ImWasser(new Vector2(x, z)))
            {
                funde.Add(new StadtbauFund($"Laterne Nr. {nr} steht im Wasser.", "Laterne an die Straße setzen.", true, x, z));
            }
        }
        // Baeume wie gesetzt (das Spiel schiebt einen Baum im Fluss zur Seite, gemeint war er trotzdem im Wasser) und wie gezeichnet
        var welt = Objekt(fassung.Karte["welt"]);
        var gesetzt = ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "baum"))
            .SelectMany(t => Linie(t["punkte"]));
        foreach (var m in gesetzt.Concat(szene.Baeume.Select(b => b.Mitte)).Distinct())
        {
            if (ImWasser(m))
            {
                funde.Add(new StadtbauFund("Ein Baum steht im Wasser.", "Baum ans Ufer oder auf die Wiese setzen.", true, m.X, m.Y));
            }
        }
        nr = 0;
        foreach (var b in fassung.Beete)
        {
            nr++;
            if (ImWasser(b))
            {
                funde.Add(new StadtbauFund($"Beet Nr. {nr} liegt im Wasser.", "Beet auf die Wiese setzen.", true, b.X, b.Y));
            }
        }
        foreach (var (x1, z1, x2, z2) in szene.Parks)
        {
            var r = (x1 + Toleranz, z1 + Toleranz, x2 - Toleranz, z2 - Toleranz);
            if (teiche.Any(t => RechteckImVieleck(r, t.Rund)) || fluesse.Any(f => RechteckLinie(r, f) < FlussWasserHalb))
            {
                funde.Add(new StadtbauFund("Ein Park liegt im Wasser.", "Park neben das Wasser schieben oder kleiner machen.", true, (x1 + x2) / 2, (z1 + z2) / 2));
            }
        }

        // Stege: mindestens ein Ende am Ufer
        nr = 0;
        foreach (var s in szene.Stege)
        {
            nr++;
            if (!AmUfer(s.Enden.A, teiche, fluesse) && !AmUfer(s.Enden.B, teiche, fluesse))
            {
                funde.Add(new StadtbauFund($"Steg Nr. {nr} berührt mit keinem Ende das Ufer.", "Steg so schieben, dass ein Ende auf dem Ufer liegt.", true, s.X, s.Z));
            }
        }

        // jeder Ort zu Fuss erreichbar
        Erreichbar(stadt, stand, szene, funde);
    }

    /// <summary>Werte in <c>stadtdaten.wasser</c> (falsch gespeicherte Eintraege zeichnet das Spiel nicht, deshalb rot).</summary>
    private static void WasserDaten(StadtFassung fassung, List<StadtbauFund> funde)
    {
        if (!Py.Hat(fassung.Stadtdaten, Wasser.Abschnitt))
        {
            return;
        }
        if (fassung.Stadtdaten[Wasser.Abschnitt] is not PyObjekt w)
        {
            funde.Add(new StadtbauFund("Der Teil „wasser“ der Stadt ist falsch gespeichert.", "Teiche und Stege löschen und neu setzen.", true));
            return;
        }
        if (Py.Hat(w, "eis") && w["eis"] is not bool)
        {
            funde.Add(new StadtbauFund("Der Schalter „eis“ hat den Wert " + PythonJson.Text(w["eis"]) + " statt true oder false.",
                "Den Schalter „Eis im Winter“ einmal aus- und wieder einschalten.", true));
        }
        var welt = Objekt(fassung.Karte["welt"]);
        var (breite, hoehe) = (Py.F(welt["breite"]), Py.F(welt["hoehe"]));
        if (Py.Hat(w, "teiche"))
        {
            if (w["teiche"] is not List<object?> teiche)
            {
                funde.Add(new StadtbauFund("Die Teiche sind falsch gespeichert.", "Teiche löschen und neu setzen.", true));
            }
            else
            {
                var nr = 0;
                foreach (var t in teiche)
                {
                    nr++;
                    if (Wasser.Punkte(t) is not { Count: >= 3 } p)
                    {
                        funde.Add(new StadtbauFund($"Teich Nr. {nr} ist falsch gespeichert (weniger als drei Eckpunkte).", "Teich löschen und neu setzen.", true));
                        continue;
                    }
                    var aus = p.FindIndex(q => q.X < 0 || q.Y < 0 || q.X > breite || q.Y > hoehe);
                    if (aus >= 0)
                    {
                        funde.Add(new StadtbauFund($"Teich Nr. {nr} reicht über den Rand der Karte.", "Teich zurück auf die Karte schieben.", true, p[aus].X, p[aus].Y));
                    }
                }
            }
        }
        if (Py.Hat(w, "stege"))
        {
            if (w["stege"] is not List<object?> stege)
            {
                funde.Add(new StadtbauFund("Die Stege sind falsch gespeichert.", "Stege löschen und neu setzen.", true));
            }
            else
            {
                var nr = 0;
                foreach (var s in stege)
                {
                    nr++;
                    if (Wasser.Steg(s) is not { } steg)
                    {
                        funde.Add(new StadtbauFund($"Steg Nr. {nr} ist falsch gespeichert.", "Steg löschen und neu setzen.", true));
                        continue;
                    }
                    var (x1, z1, x2, z2) = steg.Rechteck;
                    if (x1 < 0 || z1 < 0 || x2 > breite || z2 > hoehe)
                    {
                        funde.Add(new StadtbauFund($"Steg Nr. {nr} reicht über den Rand der Karte.", "Steg zurück auf die Karte schieben.", true, steg.X, steg.Z));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Jeder sichtbare Ort ist vom Startpunkt eines neuen Spielstands aus zu Fuss erreichbar (gleiche Wegsuche wie die
    /// Figur im Spiel, <see cref="StadtSpiel.GeheZu"/>).
    /// </summary>
    private static void Erreichbar(Stadt stadt, Spielstand stand, StadtSzene szene, List<StadtbauFund> funde)
    {
        if (szene.Orte.Count < 2)
        {
            return;
        }
        var startOrt = szene.Ort(stadt.Stand(new Spielablauf(stadt.Inhalt).Stand).Ort) ?? szene.Orte[0];
        var start = (startOrt.StehX, startOrt.StehZ);
        foreach (var o in szene.Orte.Where(o => o.Id != startOrt.Id))
        {
            var gleich = Math.Abs(o.StehX - startOrt.StehX) < 0.01 && Math.Abs(o.StehZ - startOrt.StehZ) < 0.01;
            if (!gleich && szene.Laufraster.Weg(start, (o.StehX, o.StehZ)).Count == 0
                && szene.Laufraster.NaechsteFreie(start.StehX, start.StehZ) != szene.Laufraster.NaechsteFreie(o.StehX, o.StehZ))
            {
                funde.Add(new StadtbauFund($"„{o.Name}“ ist zu Fuß nicht erreichbar (von „{startOrt.Name}“ aus).",
                    szene.MitGehweg
                        ? "Einen Weg frei machen: Wasser, Gleis oder Häuser versperren ihn (über Wasser führen nur Brücken und Stege). Über eine Straße mit Gehweg führt nur ein Zebrastreifen: Zebrastreifen setzen."
                        : "Einen Weg frei machen: Wasser, Gleis oder Häuser versperren ihn (über Wasser führen nur Brücken und Stege).", true, o.TuerX, o.TuerZ));
            }
        }
    }

    // ---- Hilfen --------------------------------------------------------------------------------------

    private static Vector2 Mitte(IReadOnlyList<Vector2> p) => new(p.Average(q => q.X), p.Average(q => q.Y));

    /// <summary>Ueberdecken sich Rechteck (x1, z1, x2, z2) und Vieleck (Ecke drin, Kante kreuzt)?</summary>
    public static bool RechteckImVieleck((float X1, float Z1, float X2, float Z2) r, IReadOnlyList<Vector2> p)
    {
        if (r.X2 <= r.X1 || r.Z2 <= r.Z1 || p.Count < 3)
        {
            return false;
        }
        Vector2[] ecken = [new(r.X1, r.Z1), new(r.X2, r.Z1), new(r.X2, r.Z2), new(r.X1, r.Z2)];
        if (ecken.Any(e => Wasser.Drin(p, e)) || p.Any(q => q.X > r.X1 && q.X < r.X2 && q.Y > r.Z1 && q.Y < r.Z2))
        {
            return true;
        }
        for (var i = 0; i < p.Count; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                if (Wasser.StreckenSchneiden(p[i], p[(i + 1) % p.Count], ecken[j], ecken[(j + 1) % 4]))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Kleinster Abstand eines Rechtecks zu einer Linie (0 = beruehrt oder schneidet).</summary>
    private static float RechteckLinie((float X1, float Z1, float X2, float Z2) r, IReadOnlyList<Vector2> linie)
    {
        if (r.X2 <= r.X1 || r.Z2 <= r.Z1)
        {
            return float.MaxValue;
        }
        Vector2[] ecken = [new(r.X1, r.Z1), new(r.X2, r.Z1), new(r.X2, r.Z2), new(r.X1, r.Z2)];
        var best = float.MaxValue;
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var (a, b) = (linie[i], linie[i + 1]);
            if ((a.X > r.X1 && a.X < r.X2 && a.Y > r.Z1 && a.Y < r.Z2) || Enumerable.Range(0, 4).Any(j => Wasser.StreckenSchneiden(a, b, ecken[j], ecken[(j + 1) % 4])))
            {
                return 0;
            }
            foreach (var e in ecken)
            {
                best = MathF.Min(best, Kurve.Abstand(e, [a, b]));
            }
            foreach (var q in (Vector2[])[a, b])
            {
                var dx = MathF.Max(MathF.Max(r.X1 - q.X, 0), q.X - r.X2);
                var dz = MathF.Max(MathF.Max(r.Z1 - q.Y, 0), q.Y - r.Z2);
                best = MathF.Min(best, MathF.Sqrt((dx * dx) + (dz * dz)));
            }
        }
        return best;
    }

    /// <summary>Erste Stelle, an der die Linie (mit halber Breite) das Vieleck beruehrt, sonst null (Schritte 0,02).</summary>
    private static Vector2? LinieImVieleck(IReadOnlyList<Vector2> linie, IReadOnlyList<Vector2> p, float halb)
    {
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var (a, b) = (linie[i], linie[i + 1]);
            var schritte = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b) / 0.02f));
            for (var k = 0; k <= schritte; k++)
            {
                var q = Vector2.Lerp(a, b, (float)k / schritte);
                if (Wasser.Drin(p, q) || Wasser.RandAbstand(p, q) < halb - Toleranz)
                {
                    return q;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Laengstes zusammenhaengendes Stueck der Linie <paramref name="s"/> naeher als <paramref name="halb"/> an der Linie
    /// <paramref name="f"/> (Schritte 0,02), mit seiner Mitte.
    /// </summary>
    public static (float Laenge, Vector2 Mitte) LaengsIm(IReadOnlyList<Vector2> s, IReadOnlyList<Vector2> f, float halb)
    {
        var (best, bestMitte) = (0f, Vector2.Zero);
        var lauf = 0f;
        Vector2 anfang = default, vorher = default;
        var drinVorher = false;
        for (var i = 0; i + 1 < s.Count; i++)
        {
            var (a, b) = (s[i], s[i + 1]);
            var schritte = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b) / 0.02f));
            for (var k = i == 0 ? 0 : 1; k <= schritte; k++)
            {
                var q = Vector2.Lerp(a, b, (float)k / schritte);
                var drin = Kurve.Abstand(q, f) < halb;
                if (drin)
                {
                    if (drinVorher)
                    {
                        lauf += Vector2.Distance(vorher, q);
                    }
                    else
                    {
                        (lauf, anfang) = (0f, q);
                    }
                    if (lauf > best)
                    {
                        (best, bestMitte) = (lauf, (anfang + q) / 2);
                    }
                }
                (drinVorher, vorher) = (drin, q);
            }
        }
        return (best, bestMitte);
    }

    /// <summary>
    /// Liegt der Punkt auf dem Ufer eines Teichs oder des Flusses (nicht weiter im Wasser als <see cref="Toleranz"/>,
    /// hoechstens <see cref="StegLandHoechstens"/> hinter dem Ufer an Land)?
    /// </summary>
    private static bool AmUfer(Vector2 e, IReadOnlyList<Teich> teiche, IReadOnlyList<IReadOnlyList<Vector2>> fluesse)
    {
        foreach (var t in teiche)
        {
            var imWasser = Wasser.Drin(t.Rund, e) && Wasser.RandAbstand(t.Rund, e) > Toleranz;
            var nahUfer = Wasser.Drin(t.MitUfer, e) || Wasser.RandAbstand(t.MitUfer, e) <= StegLandHoechstens;
            if (!imWasser && nahUfer)
            {
                return true;
            }
        }
        foreach (var f in fluesse)
        {
            var d = Kurve.Abstand(e, f);
            if (d >= FlussWasserHalb - Toleranz && d <= FlussUferHalb + StegLandHoechstens)
            {
                return true;
            }
        }
        return false;
    }
}
