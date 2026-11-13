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

    /// <summary>Gegenprobe S 0.49.0: Weg eines schraeg gedrehten Hauses achsrecht in der Stadt statt im Rahmen des Hauses (nur Tests, je Ablauf).</summary>
    public static bool SchraegWeltAltFuerTests { get => SchraegWeltAlt.Value; set => SchraegWeltAlt.Value = value; }
    private static readonly AsyncLocal<bool> SchraegWeltAlt = new();

    /// <summary>Schrittweite, in der das letzte gerade Stueck vor dem Abbiegen auf die Strasse verlaengert wird.</summary>
    public const float WegRaster = 0.05f;

    /// <summary>Das letzte (quere) Stueck ist mindestens so lang: ein winziges Stueck verdreht sich beim Runden auf 0,001.</summary>
    public const float QuerMindestens = 0.3f;

    /// <summary>
    /// Den Weg des Zugangs bzw. Zierwegs <paramref name="zugang"/> (Feld <c>weg</c>: Tuer, Punkt davor) rechtwinklig bis zur
    /// Strasse fuehren und <c>radien</c> setzen. true = Strasse erreicht (sonst bleibt der Weg, wie er war, ohne Radien).
    /// </summary>
    /// <param name="lage">S 0.49.0 (Weisung P12-231 Antwort 4c): feine Drehung des Hauses; <paramref name="haus"/> ist dann seine
    /// Flaeche im Rahmen des Hauses, und der Weg wird dort geplant (gerade aus der Tuer, Knicke parallel und rechtwinklig zu den
    /// Waenden, runde Ecken, kuerzester Weg zur Strasse) und danach in die Stadt gedreht.</param>
    internal static bool WegRechtwinklig(PyObjekt zugang, (double X, double Y, double W, double H) haus, IReadOnlyList<List<Vector2>> strassen,
        IReadOnlyList<Vector2>? laternen = null, Hausdrehung.Feinlage lage = default)
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
        if (SchraegWeltAlt.Value && lage.Aktiv)
        {
            // Gegenprobe: in der Stadt achsrecht um den Kasten des gedrehten Hauses (Rahmen der Stadt statt des Hauses)
            var ecken = lage.Ecken(haus.X, haus.Y, haus.X + haus.W, haus.Y + haus.H);
            var (ex, ey) = (ecken.Min(e => e.X), ecken.Min(e => e.Y));
            (haus, lage) = ((ex, ey, ecken.Max(e => e.X) - ex, ecken.Max(e => e.Y) - ey), default);
        }
        var tuer = lage.Zurueck(Lesen(weg[0]));
        var vor = lage.Zurueck(Lesen(weg[1]));
        var min = new Vector2((float)haus.X, (float)haus.Y);
        var max = new Vector2((float)(haus.X + haus.W), (float)(haus.Y + haus.H));
        if (lage.Aktiv)
        {
            // Tuer und erster Schritt genau auf die Achse des Hauses (Runden auf 0,001 dreht sie sonst um einen Hauch)
            var d = vor - tuer;
            vor = tuer + (MathF.Abs(d.X) >= MathF.Abs(d.Y) ? new Vector2(d.X, 0) : new Vector2(0, d.Y));
            strassen = [.. strassen.Select(l => l.Select(lage.Zurueck).ToList())];
            laternen = [.. laternen.Select(lage.Zurueck)];
        }
        // Patch S 0.49.1 (Sammelliste 331, Nico 4c „immer kuerzester Weg zur Strasse“): am schraegen Haus nur das kurze Stueck
        // gerade aus der Tuer (wie am geraden Haus), wenn noetig um die Ecken des Hauses, dann auf kuerzestem Weg zur Strasse
        var bester = lage.Aktiv && !SchraegLangAlt.Value ? PlanenKurz(tuer, vor, min, max, strassen, laternen) : Planen(tuer, vor, min, max, strassen, laternen);
        if (bester is not null && lage.Aktiv)
        {
            bester = [.. bester.Select(lage.Punkt)];
        }
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
                // Patch S 0.46.1 (selbst gefunden an Nicos Werkstatt): das gerade Stueck geht nie ueber die Mitte einer Strasse
                // hinaus (an einer schraegen Strasse lief es vorher drueber und kam mit dem queren Stueck zurueck)
                if (a > 0 && !UeberStrasseAlt.Value && UeberStrasse(w, q, strassen))
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

    /// <summary>Gegenprobe Patch S 0.49.1 (Sammelliste 331): Weg am schraegen Haus wie in S 0.49.0 (lange rechtwinklig zur Wand, erst vor der Strasse abbiegen; nur Tests, je Ablauf).</summary>
    public static bool SchraegLangAltFuerTests { get => SchraegLangAlt.Value; set => SchraegLangAlt.Value = value; }
    private static readonly AsyncLocal<bool> SchraegLangAlt = new();

    /// <summary>
    /// Patch S 0.49.1 (Sammelliste 331): Weg eines schraeg gedrehten Hauses im Rahmen des Hauses. Anfang wie bisher: Tuer und das
    /// kurze Stueck gerade hinaus (<paramref name="vor"/>), wenn noetig an der Wand entlang um eine oder zwei Ecken (diese
    /// Stuecke parallel zu den Waenden); von dort geradewegs zum naechsten Punkt einer Strassenmitte (in der Stadt meist
    /// schraeg zum Haus, an der Ecke rund). Gilt der kuerzeste Weg: kein Stueck schneidet das Haus, das letzte kreuzt keine
    /// andere Strassenmitte, ist mindestens <see cref="QuerMindestens"/> lang, das Ende liegt nicht naeher als
    /// <see cref="LaterneFrei"/> an einer Laterne, alles hoechstens <see cref="WegHoechstens"/>. Null = keiner.
    /// </summary>
    /// <summary>Abstand der Wegmitte zum Haus am schraegen Haus: halbe Wegbreite und ein wenig Luft.</summary>
    public const float KurzAbstand = (StadtSzene.WegBreite / 2) + 0.05f;

    private static readonly AsyncLocal<bool> KurzOhneAbstand = new();

    /// <summary>Nur Gegenprobe: nur die Mittellinie gegen das Haus pruefen (erster Stand von S 0.49.1, das Band streifte die Ecke).</summary>
    internal static bool KurzOhneAbstandFuerTests { get => KurzOhneAbstand.Value; set => KurzOhneAbstand.Value = value; }

    internal static List<Vector2>? PlanenKurz(Vector2 tuer, Vector2 vor, Vector2 min, Vector2 max, IReadOnlyList<List<Vector2>> strassen, IReadOnlyList<Vector2> laternen)
    {
        var e = UmDieEcke;
        var n = vor - tuer;
        n = MathF.Abs(n.X) >= MathF.Abs(n.Y) ? new Vector2(MathF.Sign(n.X), 0) : new Vector2(0, MathF.Sign(n.Y));
        // wie in Planen: bei Nische oder Hof zaehlt nur der Teil hinter der Tuer als Haus
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
        // Zielpunkte auf den Strassenmitten: alle WegRaster, dazu je Anfang die Fusspunkte (genau quer, Fusspunkte)
        var ziele = new List<Vector2>();
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
                for (var k = 0; k * WegRaster <= l; k++)
                {
                    ziele.Add(s[i] + (ab * (k * WegRaster / l)));
                }
                ziele.Add(s[i + 1]);
            }
        }
        var anfaenge = new List<List<Vector2>> { new() { tuer, vor } };
        var seiten = MathF.Abs(n.X) < 0.5f
            ? new[] { new Vector2(min.X - e, vor.Y), new Vector2(max.X + e, vor.Y) }
            : [new Vector2(vor.X, min.Y - e), new Vector2(vor.X, max.Y + e)];
        foreach (var c in seiten)
        {
            anfaenge.Add([tuer, vor, c]);
            var hinten = MathF.Abs(n.X) < 0.5f ? new Vector2(c.X, n.Y > 0 ? min.Y - e : max.Y + e) : new Vector2(n.X > 0 ? min.X - e : max.X + e, c.Y);
            anfaenge.Add([tuer, vor, c, hinten]);
        }
        // jedes Stueck ausser dem ersten (aus der Tuer) haelt mit dem ganzen Band Abstand zum Haus: es darf keine Hausecke streifen
        var band = KurzOhneAbstand.Value ? Vector2.Zero : new Vector2(KurzAbstand);
        var (aMin, aMax) = (min - band, max + band);
        List<Vector2>? bester = null;
        var besteLaenge = float.MaxValue;
        foreach (var zug in anfaenge)
        {
            var frei = true;
            for (var i = 0; i + 1 < zug.Count && frei; i++)
            {
                frei = !SchneidetHaus(zug[i], zug[i + 1], i == 0 ? min : aMin, i == 0 ? max : aMax);
            }
            var laengeBis = Laenge(zug);
            if (!frei || laengeBis >= besteLaenge)
            {
                continue;
            }
            var w = zug[^1];
            foreach (var ziel in ziele.Concat(Fusspunkte(w, strassen)))
            {
                var quer = Vector2.Distance(w, ziel);
                var ganz = laengeBis + quer;
                if (quer < QuerMindestens || ganz > WegHoechstens || ganz >= besteLaenge - 1e-4f)
                {
                    continue;
                }
                if ((!LaternenEgalFuerTests.Value && laternen.Any(q => Vector2.Distance(q, ziel) <= LaterneFrei))
                    || SchneidetHaus(w, ziel, aMin, aMax) || UeberStrasse(w, ziel, strassen))
                {
                    continue;
                }
                (bester, besteLaenge) = (Gerade([.. zug, ziel]), ganz);
            }
        }
        return bester;
    }

    /// <summary>Fusspunkte von <paramref name="w"/> auf jedem Stueck einer Strassenmitte (nur innerhalb des Stuecks).</summary>
    private static IEnumerable<Vector2> Fusspunkte(Vector2 w, IReadOnlyList<List<Vector2>> strassen)
    {
        foreach (var s in strassen)
        {
            for (var i = 0; i + 1 < s.Count; i++)
            {
                var ab = s[i + 1] - s[i];
                var l2 = ab.LengthSquared();
                if (l2 < 1e-12f)
                {
                    continue;
                }
                var t = Vector2.Dot(w - s[i], ab) / l2;
                if (t is >= 0 and <= 1)
                {
                    yield return s[i] + (ab * t);
                }
            }
        }
    }

    /// <summary>Gegenprobe Patch S 0.46.1: das gerade Stueck darf ueber die Strassenmitte hinaus (wie bis S 0.46.0, nur Tests, je Ablauf).</summary>
    public static bool UeberStrasseAltFuerTests { get => UeberStrasseAlt.Value; set => UeberStrasseAlt.Value = value; }
    private static readonly AsyncLocal<bool> UeberStrasseAlt = new();

    /// <summary>Kreuzt die Strecke p-q die Mitte einer Strasse (echt, nicht nur beruehrt)?</summary>
    private static bool UeberStrasse(Vector2 p, Vector2 q, IReadOnlyList<List<Vector2>> strassen)
    {
        static float Kreuz(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
        var r = q - p;
        foreach (var s in strassen)
        {
            for (var i = 0; i + 1 < s.Count; i++)
            {
                var e = s[i + 1] - s[i];
                var nenner = Kreuz(r, e);
                if (MathF.Abs(nenner) < 1e-9f)
                {
                    continue;
                }
                var t = Kreuz(s[i] - p, e) / nenner;
                var u = Kreuz(s[i] - p, r) / nenner;
                if (t > 1e-3f && t < 1 - 1e-3f && u >= 0 && u <= 1)
                {
                    return true;
                }
            }
        }
        return false;
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
