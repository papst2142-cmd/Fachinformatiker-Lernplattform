using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.66.0 / S 0.42.0 (Weisung P12-192): die Regeln fuer das Gelaende (<c>stadtdaten.gelaende</c>, <see cref="Gelaende"/>),
/// alle rot:
/// <list type="number">
/// <item>Daten: falsch gespeichert (Feld, Zahl, Radius 1–3, Hoehe 0,2–1,5, Laenge, Drehung, gemalte Hoehe);</item>
/// <item>Karte: ein Huegel oder eine gemalte Hoehe reicht ueber den Rand der Karte;</item>
/// <item>Grenzen: mehr als <see cref="Gelaende.HuegelHoechstens"/> Huegel oder mehr als <see cref="Gelaende.DreieckeHoechstens"/> Dreiecke;</item>
/// <item>Saum: naeher als <see cref="Gelaende.Saum"/> an Haus, Strasse, Fussweg, Gleis, Wasser, Platz, Haltestelle, Zebrastreifen oder Ampel;</item>
/// <item>eine Laterne oder ein Questgeber steht auf einem Huegel;</item>
/// <item>ein Katalog-Stueck steht zu schief (schwebt an einer Ecke mehr als <see cref="SchiefSchwebt"/> oder sinkt mehr als <see cref="SchiefSinkt"/> ein);</item>
/// <item>ein Baum steht zu schief (Fuss am Stamm mehr als <see cref="Gelaende.FussSpiel"/> neben dem Hang);</item>
/// <item>unerreichbar: ein Ort, ein Questgeber oder eine Haltestelle ist nur wegen eines zu steilen Hangs nicht zu Fuss erreichbar.</item>
/// </list>
/// Fassung 0 (ohne <c>gelaende</c>) hat hier keinen Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>So weit darf ein Katalog-Stueck an einer Ecke ueber dem Hang schweben.</summary>
    public const float SchiefSchwebt = 0.05f;

    /// <summary>So weit darf ein Katalog-Stueck an einer Ecke im Hang einsinken.</summary>
    public const float SchiefSinkt = 0.25f;

    /// <summary>Halbe Breite des Stamms, an deren Rand der Fuss eines Baums gemessen wird (E1).</summary>
    public const float BaumStamm = 0.08f;

    private static string Zg(double w) => w.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>Regeln 1 bis 3 auf den Daten.</summary>
    private static void GelaendeDaten(StadtFassung fassung, double breite, double hoehe, List<StadtbauFund> funde)
    {
        var fehler = new List<(string Text, Vector2? Stelle)>();
        if (Gelaende.Lesen(fassung.Stadtdaten, fehler) is not { } g)
        {
            return;
        }
        foreach (var (text, stelle) in fehler)
        {
            funde.Add(new StadtbauFund(text, "Hügel löschen und neu setzen.", true, stelle?.X, stelle?.Y));
        }
        foreach (var h in g.Huegel)
        {
            if (h.Umriss().Any(p => p.X < 0 || p.Y < 0 || p.X > breite || p.Y > hoehe))
            {
                funde.Add(new StadtbauFund($"Hügel Nr. {h.Nr} reicht über den Rand der Karte.", "Hügel ganz auf die Karte schieben oder kleiner machen.", true, h.X, h.Z));
            }
        }
        if (g.Gemalt.Keys.Any(k => (k.I + 1) * Gelaende.Raster > breite || (k.J + 1) * Gelaende.Raster > hoehe))
        {
            funde.Add(new StadtbauFund("Gemaltes Gelände reicht über den Rand der Karte.", "Am Rand mit dem Pinsel „Senken“ wegmalen.", true));
        }
        if (g.Huegel.Count > Gelaende.HuegelHoechstens)
        {
            funde.Add(new StadtbauFund($"Die Stadt hat {g.Huegel.Count} Hügel, erlaubt sind höchstens {Gelaende.HuegelHoechstens}.", "Hügel löschen.", true));
        }
        var dreiecke = g.Dreiecke((float)breite, (float)hoehe);
        if (dreiecke > Gelaende.DreieckeHoechstens)
        {
            funde.Add(new StadtbauFund($"Das Gelände braucht {dreiecke.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} Dreiecke, erlaubt sind höchstens {Gelaende.DreieckeHoechstens.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))}.",
                "Hügel kleiner machen oder löschen.", true));
        }
    }

    /// <summary>Ein Kern des Gelaendes: Strecke (A, B) mit Radius; Huegel = Ruecken, gemalte Hoehe = Punkt mit Radius Raster.</summary>
    private sealed record GelaendeKern(string Name, Vector2 A, Vector2 B, float R);

    private static List<GelaendeKern> Kerne(Gelaende g)
    {
        var kerne = g.Huegel.Select(h =>
        {
            var m = new Vector2(h.X, h.Z);
            return new GelaendeKern($"Hügel Nr. {h.Nr}", m - (h.Richtung * h.Ruecken), m + (h.Richtung * h.Ruecken), h.Radius);
        }).ToList();
        foreach (var ((i, j), _) in g.Gemalt)
        {
            var p = new Vector2(i * Gelaende.Raster, j * Gelaende.Raster);
            kerne.Add(new GelaendeKern("Gemaltes Gelände", p, p, Gelaende.Raster));
        }
        return kerne;
    }

    /// <summary>Kleinster Abstand des Kerns zu einem Punkt (minus Radius = Abstand des Gelaende-Rands).</summary>
    private static float KernAbstand(GelaendeKern k, Func<Vector2, float> abstand)
    {
        var laenge = Vector2.Distance(k.A, k.B);
        var schritte = Math.Max(1, (int)MathF.Ceiling(laenge / 0.1f));
        var best = float.MaxValue;
        for (var i = 0; i <= schritte; i++)
        {
            best = MathF.Min(best, abstand(Vector2.Lerp(k.A, k.B, i / (float)schritte)));
        }
        return best - k.R;
    }

    private static float RechteckAbstand(Vector2 p, float x1, float z1, float x2, float z2)
    {
        var dx = MathF.Max(MathF.Max(x1 - p.X, 0), p.X - x2);
        var dz = MathF.Max(MathF.Max(z1 - p.Y, 0), p.Y - z2);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    private static float VieleckAbstand(Vector2 p, IReadOnlyList<Vector2> v) =>
        Wasser.Drin(v, p) ? 0f : Kurve.Abstand(p, [.. v, v[0]]);

    /// <summary>Alles, wovon das Gelaende <see cref="Gelaende.Saum"/> Abstand haelt (Art fuer den Fund, Abstand eines Punkts).</summary>
    private static List<(string Art, Func<Vector2, float> Abstand)> GelaendeHindernisse(StadtSzene szene, StadtFassung fassung)
    {
        var hindernisse = new List<(string Art, Func<Vector2, float> Abstand)>();
        foreach (var h in szene.Haeuser)
        {
            hindernisse.Add(("einem Haus", p => RechteckAbstand(p, h.Min.X, h.Min.Z, h.Max.X, h.Max.Z)));
        }
        var welt = Objekt(fassung.Karte["welt"]);
        foreach (var l in ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "strasse")))
        {
            var linie = ListeOderLeer(l, "punkte").OfType<List<object?>>().Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1]))).ToList();
            if (linie.Count >= 2)
            {
                var halb = (StadtSzene.StrassenBreite / 2) + StadtSzene.GehwegBreite(l);
                hindernisse.Add(("einer Straße", p => Kurve.Abstand(p, linie) - halb));
            }
        }
        foreach (var (linie, halb) in szene.BaumSperren)
        {
            // die Sperren der Baeume: Strassen (oben mit Gehweg), Zugangswege, Gleise, Fluesse
            var art = halb == StadtSzene.WegBreite / 2 ? "einem Fußweg" : halb == (StadtSzene.GleisBreite / 2) + 0.1f ? "einem Gleis"
                : halb == (StadtSzene.FlussBreite + 0.3f) / 2 ? "dem Wasser" : null;
            if (art is not null)
            {
                var l = linie;
                var hb = art == "einem Gleis" ? StadtSzene.GleisBreite / 2 : art == "dem Wasser" ? StadtSzene.FlussBreite / 2 : halb;
                hindernisse.Add((art, p => Kurve.Abstand(p, l) - hb));
            }
        }
        foreach (var w in Spiel.Probe.Fusswege.Alle(fassung.Stadtdaten).Where(w => w.Punkte.Count >= 2))
        {
            hindernisse.Add(("einem Fußweg", p => Kurve.Abstand(p, w.Punkte) - (StadtSzene.WegBreite / 2)));
        }
        foreach (var t in szene.Teiche)
        {
            hindernisse.Add(("dem Wasser", p => VieleckAbstand(p, t.MitUfer)));
        }
        foreach (var (x1, z1, x2, z2) in szene.Parks)
        {
            hindernisse.Add(("einem Platz", p => RechteckAbstand(p, x1, z1, x2, z2)));
        }
        foreach (var pp in szene.Parkplaetze)
        {
            var ecken = pp.Ecken();
            hindernisse.Add(("einem Parkplatz", p => VieleckAbstand(p, ecken)));
        }
        foreach (var h in szene.Haltestellen)
        {
            hindernisse.Add(("einer Haltestelle", p => Vector2.Distance(p, h.Mitte) - 0.5f));
        }
        foreach (var z in szene.Zebras)
        {
            hindernisse.Add(("einem Zebrastreifen", p => Vector2.Distance(p, z.Mitte) - (StadtSzene.StrassenBreite / 2)));
        }
        foreach (var a in szene.Ampeln)
        {
            hindernisse.Add(("einer Ampel", p => Vector2.Distance(p, a.Mitte) - (StadtSzene.StrassenBreite / 2)));
        }
        return hindernisse;
    }

    /// <summary>
    /// Weisung P12-254 (Nico Frage 2 = a, „nur auf der Wiese malen“): darf der Pinsel den Rasterpunkt <paramref name="p"/>
    /// aendern? Genau die Regel 4 fuer eine gemalte Hoehe (Kern = Punkt mit Radius <see cref="Gelaende.Raster"/>, Rand
    /// <see cref="Gelaende.Saum"/> zu Haus, Strasse, Fussweg, Gleis, Wasser, Platz, Parkplatz, Haltestelle, Zebrastreifen,
    /// Ampel); was der Pinsel malt, meldet die Pruefung darum nie rot. Das Ergebnis haelt die Hindernisse fest (fuer viele Punkte).
    /// </summary>
    public static Func<Vector2, bool> GelaendeMalbar(StadtSzene szene, StadtFassung fassung)
    {
        var hindernisse = GelaendeHindernisse(szene, fassung);
        return p =>
        {
            var k = new GelaendeKern("Gemaltes Gelände", p, p, Gelaende.Raster);
            return hindernisse.All(h => KernAbstand(k, h.Abstand) >= Gelaende.Saum - 0.001f);
        };
    }

    /// <summary>
    /// Messung fuer die Meldung (Weisung P12-192 Punkt 3c): wie viel freie Wiese bleibt mit dem festen Rand <see cref="Gelaende.Saum"/>
    /// (Flaeche in Feldern, auf der die Mitte eines Huegels mit Radius <see cref="Gelaende.RadiusMin"/> stehen kann; Raster 0,25) und
    /// der groesste moegliche runde Huegel (Radius bis <see cref="Gelaende.RadiusMax"/>, ganz auf der Karte).
    /// </summary>
    public static (double FreieFelder, double GroessterRadius, Vector2 Stelle) GelaendePlatz(StadtSzene szene, StadtFassung fassung)
    {
        var hindernisse = GelaendeHindernisse(szene, fassung);
        const float r = 0.25f;
        var (frei, groesster, stelle) = (0, 0f, Vector2.Zero);
        for (var x = r / 2; x < szene.Breite; x += r)
        {
            for (var z = r / 2; z < szene.Tiefe; z += r)
            {
                var p = new Vector2(x, z);
                var rand = MathF.Min(MathF.Min(x, z), MathF.Min(szene.Breite - x, szene.Tiefe - z));
                var weg = hindernisse.Count == 0 ? float.MaxValue : hindernisse.Min(h => h.Abstand(p)) - Gelaende.Saum;
                var moeglich = MathF.Min(MathF.Min(rand, weg), Gelaende.RadiusMax);
                if (moeglich >= Gelaende.RadiusMin)
                {
                    frei++;
                    if (moeglich > groesster)
                    {
                        (groesster, stelle) = (moeglich, p);
                    }
                }
            }
        }
        return (frei * r * r, groesster, stelle);
    }

    /// <summary>Regeln 4 bis 8 an der gebauten Szene.</summary>
    private static void GelaendeRegeln(IStadtSicht stadt, ISpielSicht stand, StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde,
        Func<StadtSzene>? ohneGelaende)
    {
        if (szene.Gelaende is not { } g || (g.Huegel.Count == 0 && g.Gemalt.Count == 0))
        {
            return;
        }
        var hindernisse = GelaendeHindernisse(szene, fassung);
        // Regel 4: Saum (je Huegel bzw. gemalter Flaeche nur der naechste Fund)
        var gemaltGemeldet = false;
        foreach (var k in Kerne(g))
        {
            if (k.Name == "Gemaltes Gelände" && gemaltGemeldet)
            {
                continue;
            }
            var art = "";
            var abstand = float.MaxValue;
            foreach (var h in hindernisse)
            {
                var a = KernAbstand(k, h.Abstand);
                if (a < abstand)
                {
                    (art, abstand) = (h.Art, a);
                }
            }
            if (abstand < Gelaende.Saum - 0.001f)
            {
                gemaltGemeldet |= k.Name == "Gemaltes Gelände";
                funde.Add(new StadtbauFund($"{k.Name} liegt näher als {Zg(Gelaende.Saum)} an {art} ({Zg(Math.Max(0, abstand))}).",
                    "Hügel weiter weg schieben oder kleiner machen; um jeden Hügel bleibt ein Rand von 1,5 frei.", true, (k.A.X + k.B.X) / 2, (k.A.Y + k.B.Y) / 2));
            }
        }
        // Regel 5: Laterne oder Questgeber auf einem Huegel
        foreach (var (x, z, _) in fassung.Laternen)
        {
            if (g.Hoehe(x, z) > 0f)
            {
                funde.Add(new StadtbauFund("Eine Laterne steht auf einem Hügel.", "Laterne vom Hügel nehmen oder den Hügel verschieben.", true, x, z));
            }
        }
        foreach (var q in szene.Questgeber)
        {
            if (g.Hoehe((float)q.X, (float)q.Z) > 0f)
            {
                funde.Add(new StadtbauFund($"Questgeber „{q.Name}“ steht auf einem Hügel.", "Questgeber neben den Hügel stellen.", true, q.X, q.Z));
            }
        }
        // Regel 6: Katalog-Stueck zu schief
        foreach (var k in Katalogstueck.AusStadtdaten(fassung.Stadtdaten))
        {
            var mitte = g.Boden(k.X, k.Z);
            var (x1, z1, x2, z2) = k.Rechteck;
            var ecken = new[] { (x1, z1), (x2, z1), (x1, z2), (x2, z2) }.Select(e => g.Boden(e.Item1, e.Item2)).ToList();
            if (ecken.Any(e => mitte - e > SchiefSchwebt) || ecken.Any(e => e - mitte > SchiefSinkt))
            {
                funde.Add(new StadtbauFund("Ein Katalog-Stück steht zu schief am Hang.", "Stück auf flachen Boden schieben oder den Hügel flacher machen.", true, k.X, k.Z));
            }
        }
        // Regel 7: Baum zu schief (gesetzte Baeume; Kleingruen meidet schiefe Stellen von selbst)
        foreach (var (mitte, _) in szene.Baeume)
        {
            if (g.Hoehe(mitte.X, mitte.Y) > 0f && !g.Flach(mitte.X, mitte.Y, BaumStamm))
            {
                funde.Add(new StadtbauFund("Ein Baum steht zu schief am Hang.", "Baum auf flacheren Boden schieben oder den Hügel flacher machen.", true, mitte.X, mitte.Y));
            }
        }
        // Regel 8: unerreichbar nur wegen eines steilen Hangs
        var startOrt = szene.Ort(stadt.StartOrt) ?? (szene.Orte.Count > 0 ? szene.Orte[0] : null);
        if (startOrt is null)
        {
            return;
        }
        var start = (startOrt.StehX, startOrt.StehZ);
        var ziele = szene.Orte.Where(o => o.Id != startOrt.Id).Select(o => (Name: $"„{o.Name}“", X: (double)o.StehX, Z: (double)o.StehZ))
            .Concat(szene.Questgeber.Select(q => (Name: $"Questgeber „{q.Name}“", q.X, q.Z)))
            .Concat(szene.Haltestellen.Select(h => (Name: $"Haltestelle Nr. {h.Nr}", X: (double)(h.Mitte.X + (h.Aussen.X * 0.6f)), Z: (double)(h.Mitte.Y + (h.Aussen.Y * 0.6f)))))
            .ToList();
        bool Erreicht(StadtSzene s, (double X, double Z) ziel) =>
            s.Laufraster.Weg(start, ziel).Count > 0 || s.Laufraster.NaechsteFreie(start.StehX, start.StehZ) == s.Laufraster.NaechsteFreie(ziel.X, ziel.Z);
        var nicht = ziele.Where(z => !Erreicht(szene, (z.X, z.Z))).ToList();
        if (nicht.Count == 0 || ohneGelaende?.Invoke() is not { } ohne)
        {
            return;
        }
        foreach (var z in nicht.Where(z => Erreicht(ohne, (z.X, z.Z))))
        {
            funde.Add(new StadtbauFund($"{z.Name} ist wegen eines zu steilen Hangs zu Fuß nicht erreichbar.",
                "Hügel flacher oder kleiner machen: steiler als 45 Grad kommt die Figur nicht hinauf.", true, z.X, z.Z));
        }
    }
}
