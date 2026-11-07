using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Ein Teich aus <c>stadtdaten.wasser.teiche</c>: die Eckpunkte, wie sie gesetzt wurden (Ecken gerundet gezeichnet).</summary>
public sealed record Teich(IReadOnlyList<Vector2> Punkte)
{
    /// <summary>Wasserflaeche mit gerundeten Ecken (gegen den Uhrzeigersinn, <see cref="Wasser.Rund"/>).</summary>
    public IReadOnlyList<Vector2> Rund { get; } = Wasser.Rund(Punkte);

    /// <summary>Wasser samt Ufer (<see cref="Wasser.UferBreite"/> rundum).</summary>
    public IReadOnlyList<Vector2> MitUfer { get; } = Wasser.Erweitert(Wasser.Rund(Punkte), Wasser.UferBreite);
}

/// <summary>
/// Ein Steg aus <c>stadtdaten.wasser.stege</c>: Mitte (x, y der Karte), Laenge <see cref="W"/> und Breite <see cref="T"/>
/// vor dem Drehen, <see cref="Dreh"/> = Vierteldrehungen (0 bis 3; 1 und 3 liegen laengs der Kartentiefe).
/// </summary>
public sealed record Steg(float X, float Z, float W, float T, int Dreh)
{
    /// <summary>Liegt der Steg laengs der Kartentiefe (gedreht)?</summary>
    public bool Quer => Dreh % 2 == 1;

    /// <summary>Rechteck auf der Karte (x1, z1, x2, z2).</summary>
    public (float X1, float Z1, float X2, float Z2) Rechteck
    {
        get
        {
            var (hw, ht) = Quer ? (T / 2, W / 2) : (W / 2, T / 2);
            return (X - hw, Z - ht, X + hw, Z + ht);
        }
    }

    /// <summary>Die beiden Enden (Mitte der schmalen Seiten).</summary>
    public (Vector2 A, Vector2 B) Enden => Quer
        ? (new Vector2(X, Z - (W / 2)), new Vector2(X, Z + (W / 2)))
        : (new Vector2(X - (W / 2), Z), new Vector2(X + (W / 2), Z));
}

/// <summary>
/// S 0.8.0 / A 0.43.4 (Weisung P12-47, Plan P12-26b Abschnitt 10): Teiche, Stege und Eis aus <c>stadtdaten.wasser</c>
/// (<c>teiche: [{punkte}]</c>, <c>stege: [{x, y, w, t, dreh}]</c>, <c>eis: true/false</c>). Nur Darstellung und Laufraster;
/// ohne <c>wasser</c> (Fassung 0) aendert sich nichts. Der Fluss bleibt wie bisher in <c>karte.landschaft</c>.
/// Gemeinsam fuer Spiel, Pruefung und Werkzeug: Ecken runden, Ufer, Dreiecke, Punkt im Wasser, Selbstschnitt.
/// </summary>
public static class Wasser
{
    public const string Abschnitt = "wasser";

    /// <summary>Ufer rundum um einen Teich (Plan Abschnitt 10).</summary>
    public const float UferBreite = 0.15f;

    /// <summary>Wunschradius der gerundeten Ecken; kleiner, wo die Kanten kurz sind.</summary>
    public const float EckRadius = 0.5f;

    /// <summary>Stuecke je gerundeter Ecke.</summary>
    public const int EckStuecke = 4;

    /// <summary>Kleinster Teich (Breite und Tiefe der Eckpunkte), Pruefung.</summary>
    public const float Mindestens = 1f;

    /// <summary>Hoehe der Stegflaeche (ueber Wasser 0,034 und Strasse 0,058) und Dicke der Bretter.</summary>
    public const float StegOben = 0.1f;
    public const float StegDicke = 0.03f;

    /// <summary>Eis im Winter (Schalter <c>eis</c>, Frage 6 = a): helles Eisblau; der Rand ist das Ufer (im Winter weiss).</summary>
    public static readonly Vector3 Eis = Netz.Farbe("#D6ECF7");

    /// <summary>Holz der Stege (Bretter) und der Pfosten.</summary>
    public static readonly Vector3 StegHolz = Netz.Farbe("#9A6B3F");
    public static readonly Vector3 StegPfosten = Netz.Farbe("#6E4A2A");

    /// <summary>Der Abschnitt <c>wasser</c> der Stadtdaten (oder null).</summary>
    public static PyObjekt? Daten(PyObjekt? stadtDaten) => stadtDaten is not null && Get(stadtDaten, Abschnitt) is PyObjekt w ? w : null;

    /// <summary>Eis im Winter an? Nur bei <c>eis = true</c> (fehlt oder anderer Wert = aus; die Pruefung meldet einen falschen Wert).</summary>
    public static bool EisAn(PyObjekt? stadtDaten) => Daten(stadtDaten) is { } w && Get(w, "eis") is true;

    /// <summary>Die Teiche (ein Teich mit weniger als drei lesbaren Punkten fehlt; die Pruefung meldet ihn).</summary>
    public static List<Teich> Teiche(PyObjekt? stadtDaten)
    {
        var liste = new List<Teich>();
        if (Daten(stadtDaten) is not { } w || Get(w, "teiche") is not List<object?> teiche)
        {
            return liste;
        }
        foreach (var t in teiche)
        {
            if (Punkte(t) is { Count: >= 3 } p)
            {
                liste.Add(new Teich(p));
            }
        }
        return liste;
    }

    /// <summary>Die Eckpunkte eines Teich-Eintrags (null = nicht lesbar).</summary>
    public static List<Vector2>? Punkte(object? teich)
    {
        if (teich is not PyObjekt o || Get(o, "punkte") is not List<object?> punkte)
        {
            return null;
        }
        var liste = new List<Vector2>();
        foreach (var p in punkte)
        {
            if (p is not List<object?> { Count: >= 2 } q || !Zahl(q[0]) || !Zahl(q[1]))
            {
                return null;
            }
            liste.Add(new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])));
        }
        return liste;
    }

    /// <summary>Die Stege (ein Eintrag mit fehlenden oder falschen Werten fehlt; die Pruefung meldet ihn).</summary>
    public static List<Steg> Stege(PyObjekt? stadtDaten)
    {
        var liste = new List<Steg>();
        if (Daten(stadtDaten) is not { } w || Get(w, "stege") is not List<object?> stege)
        {
            return liste;
        }
        foreach (var s in stege)
        {
            if (Steg(s) is { } steg)
            {
                liste.Add(steg);
            }
        }
        return liste;
    }

    /// <summary>Ein Steg-Eintrag (null = nicht lesbar: x, y, w, t Zahlen, w und t groesser 0, dreh 0 bis 3).</summary>
    public static Steg? Steg(object? eintrag)
    {
        if (eintrag is not PyObjekt o || !Zahl(Get(o, "x")) || !Zahl(Get(o, "y")) || !Zahl(Get(o, "w")) || !Zahl(Get(o, "t")))
        {
            return null;
        }
        var dreh = Get(o, "dreh", 0L);
        if (dreh is not long d || d is < 0 or > 3 || Py.F(o["w"]) <= 0 || Py.F(o["t"]) <= 0)
        {
            return null;
        }
        return new Steg((float)Py.F(o["x"]), (float)Py.F(o["y"]), (float)Py.F(o["w"]), (float)Py.F(o["t"]), (int)d);
    }

    private static bool Zahl(object? w) => w is long or double;

    // ---- Formen --------------------------------------------------------------------------------------

    /// <summary>Feste Formen als Hilfe zum Erzeugen der Eckpunkte (Werkzeug; bei Frage 7 = b die einzige Art zu setzen).</summary>
    public enum Form
    {
        Eckig,
        Rund,
    }

    /// <summary>Eckpunkte einer festen Form um die Mitte (cx, cz) mit Breite und Tiefe (Rund: 8 Punkte, gerundet wirkt er oval).</summary>
    public static List<Vector2> FormPunkte(Form form, float cx, float cz, float breite, float tiefe)
    {
        if (form == Form.Eckig)
        {
            return
            [
                new(cx - (breite / 2), cz - (tiefe / 2)), new(cx + (breite / 2), cz - (tiefe / 2)),
                new(cx + (breite / 2), cz + (tiefe / 2)), new(cx - (breite / 2), cz + (tiefe / 2)),
            ];
        }
        var liste = new List<Vector2>();
        for (var i = 0; i < 8; i++)
        {
            var w = i * MathF.PI / 4;
            liste.Add(new Vector2(cx + (MathF.Cos(w) * breite / 2), cz + (MathF.Sin(w) * tiefe / 2)));
        }
        return liste;
    }

    // ---- Geometrie -----------------------------------------------------------------------------------

    /// <summary>Doppelte Flaeche mit Vorzeichen (Schnuerband); groesser 0 = gegen den Uhrzeigersinn in (x, z).</summary>
    public static float Flaeche2(IReadOnlyList<Vector2> p)
    {
        var s = 0f;
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            s += (a.X * b.Y) - (b.X * a.Y);
        }
        return s;
    }

    /// <summary>Gleiche aufeinanderfolgende Punkte weg, Umlauf gegen den Uhrzeigersinn.</summary>
    public static List<Vector2> Geordnet(IReadOnlyList<Vector2> p)
    {
        var liste = new List<Vector2>();
        foreach (var q in p)
        {
            if (liste.Count == 0 || Vector2.DistanceSquared(liste[^1], q) > 1e-8f)
            {
                liste.Add(q);
            }
        }
        if (liste.Count > 1 && Vector2.DistanceSquared(liste[0], liste[^1]) <= 1e-8f)
        {
            liste.RemoveAt(liste.Count - 1);
        }
        if (Flaeche2(liste) < 0)
        {
            liste.Reverse();
        }
        return liste;
    }

    /// <summary>
    /// Ecken gerundet (Frage 7 = a): an jeder Ecke ein Bogen (quadratische Kurve mit der Ecke als Stuetzpunkt) von
    /// hoechstens <see cref="EckRadius"/> und hoechstens 45 Prozent der kuerzeren Nachbarkante, damit sich zwei Boegen
    /// nie ueberlappen. Ergebnis gegen den Uhrzeigersinn.
    /// </summary>
    public static List<Vector2> Rund(IReadOnlyList<Vector2> eckpunkte)
    {
        var p = Geordnet(eckpunkte);
        if (p.Count < 3)
        {
            return p;
        }
        var ergebnis = new List<Vector2>();
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[(i + p.Count - 1) % p.Count];
            var b = p[i];
            var c = p[(i + 1) % p.Count];
            float d1 = Vector2.Distance(a, b), d2 = Vector2.Distance(b, c);
            var r = MathF.Min(EckRadius, 0.45f * MathF.Min(d1, d2));
            var p1 = b + ((a - b) / d1 * r);
            var p2 = b + ((c - b) / d2 * r);
            for (var k = 0; k <= EckStuecke; k++)
            {
                var t = k / (float)EckStuecke;
                var q = ((1 - t) * (1 - t) * p1) + (2 * (1 - t) * t * b) + (t * t * p2);
                if (ergebnis.Count == 0 || Vector2.DistanceSquared(ergebnis[^1], q) > 1e-8f)
                {
                    ergebnis.Add(q);
                }
            }
        }
        if (ergebnis.Count > 1 && Vector2.DistanceSquared(ergebnis[0], ergebnis[^1]) <= 1e-8f)
        {
            ergebnis.RemoveAt(ergebnis.Count - 1);
        }
        return ergebnis;
    }

    /// <summary>Vieleck (gegen den Uhrzeigersinn) um <paramref name="d"/> nach aussen erweitert (Gehrung, an spitzen Ecken begrenzt).</summary>
    public static List<Vector2> Erweitert(IReadOnlyList<Vector2> p, float d)
    {
        var liste = new List<Vector2>(p.Count);
        if (p.Count < 3)
        {
            return [.. p];
        }
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[(i + p.Count - 1) % p.Count];
            var b = p[i];
            var c = p[(i + 1) % p.Count];
            var n1 = Aussen(b - a);
            var n2 = Aussen(c - b);
            var m = n1 + n2;
            if (m.LengthSquared() < 1e-8f)
            {
                liste.Add(b + (n1 * d));
                continue;
            }
            m = Vector2.Normalize(m);
            var cos = MathF.Max(Vector2.Dot(m, n1), 0.35f);
            liste.Add(b + (m * (d / cos)));
        }
        return liste;
    }

    /// <summary>Normale nach aussen einer Kante eines Vielecks gegen den Uhrzeigersinn.</summary>
    private static Vector2 Aussen(Vector2 kante)
    {
        var l = kante.Length();
        return l < 1e-6f ? Vector2.Zero : new Vector2(kante.Y, -kante.X) / l;
    }

    /// <summary>Liegt der Punkt im Vieleck (Strahlverfahren)?</summary>
    public static bool Drin(IReadOnlyList<Vector2> p, Vector2 q)
    {
        var drin = false;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
        {
            if ((p[i].Y > q.Y) != (p[j].Y > q.Y) && q.X < ((p[j].X - p[i].X) * (q.Y - p[i].Y) / (p[j].Y - p[i].Y)) + p[i].X)
            {
                drin = !drin;
            }
        }
        return drin;
    }

    /// <summary>Kleinster Abstand des Punkts zum Rand des Vielecks.</summary>
    public static float RandAbstand(IReadOnlyList<Vector2> p, Vector2 q)
    {
        var best = float.MaxValue;
        for (var i = 0; i < p.Count; i++)
        {
            best = MathF.Min(best, Kurve.Abstand(q, [p[i], p[(i + 1) % p.Count]]));
        }
        return best;
    }

    /// <summary>Schneiden sich zwei nicht benachbarte Kanten des Vielecks (Eckpunkte, wie gesetzt)?</summary>
    public static bool SchneidetSich(IReadOnlyList<Vector2> eckpunkte)
    {
        var p = eckpunkte;
        var n = p.Count;
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                if (j == i + 1 || (i == 0 && j == n - 1))
                {
                    continue;
                }
                if (StreckenSchneiden(p[i], p[(i + 1) % n], p[j], p[(j + 1) % n]))
                {
                    return true;
                }
            }
        }
        // zwei gleiche Eckpunkte zaehlen auch (der Teich haette dort eine Spitze ohne Flaeche)
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                if (Vector2.DistanceSquared(p[i], p[j]) < 1e-6f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Schneiden sich die Strecken a-b und c-d (auch Beruehren)?</summary>
    public static bool StreckenSchneiden(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float K(Vector2 p, Vector2 q, Vector2 r) => ((q.X - p.X) * (r.Y - p.Y)) - ((q.Y - p.Y) * (r.X - p.X));
        static bool Auf(Vector2 p, Vector2 q, Vector2 r) =>
            MathF.Min(p.X, q.X) - 1e-6f <= r.X && r.X <= MathF.Max(p.X, q.X) + 1e-6f && MathF.Min(p.Y, q.Y) - 1e-6f <= r.Y && r.Y <= MathF.Max(p.Y, q.Y) + 1e-6f;
        float k1 = K(a, b, c), k2 = K(a, b, d), k3 = K(c, d, a), k4 = K(c, d, b);
        if (((k1 > 1e-7f && k2 < -1e-7f) || (k1 < -1e-7f && k2 > 1e-7f)) && ((k3 > 1e-7f && k4 < -1e-7f) || (k3 < -1e-7f && k4 > 1e-7f)))
        {
            return true;
        }
        return (MathF.Abs(k1) <= 1e-7f && Auf(a, b, c)) || (MathF.Abs(k2) <= 1e-7f && Auf(a, b, d))
            || (MathF.Abs(k3) <= 1e-7f && Auf(c, d, a)) || (MathF.Abs(k4) <= 1e-7f && Auf(c, d, b));
    }

    /// <summary>
    /// Dreiecke eines einfachen Vielecks gegen den Uhrzeigersinn (Ohren abschneiden): je Dreieck drei Nummern der Ecken.
    /// Findet sich kein Ohr mehr (Vieleck schneidet sich), endet es mit dem, was es hat.
    /// </summary>
    public static List<(int A, int B, int C)> Dreiecke(IReadOnlyList<Vector2> p)
    {
        var ergebnis = new List<(int, int, int)>();
        var rest = Enumerable.Range(0, p.Count).ToList();
        var schutz = p.Count * p.Count;
        while (rest.Count > 3 && schutz-- > 0)
        {
            var gefunden = false;
            for (var i = 0; i < rest.Count; i++)
            {
                int ia = rest[(i + rest.Count - 1) % rest.Count], ib = rest[i], ic = rest[(i + 1) % rest.Count];
                Vector2 a = p[ia], b = p[ib], c = p[ic];
                if (((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X)) <= 1e-9f)
                {
                    continue;   // keine konvexe Ecke
                }
                var leer = true;
                foreach (var k in rest)
                {
                    if (k != ia && k != ib && k != ic && ImDreieck(p[k], a, b, c))
                    {
                        leer = false;
                        break;
                    }
                }
                if (!leer)
                {
                    continue;
                }
                ergebnis.Add((ia, ib, ic));
                rest.RemoveAt(i);
                gefunden = true;
                break;
            }
            if (!gefunden)
            {
                break;
            }
        }
        if (rest.Count == 3)
        {
            ergebnis.Add((rest[0], rest[1], rest[2]));
        }
        return ergebnis;
    }

    private static bool ImDreieck(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        static float K(Vector2 p, Vector2 q, Vector2 r) => ((q.X - p.X) * (r.Y - p.Y)) - ((q.Y - p.Y) * (r.X - p.X));
        return K(a, b, p) >= -1e-9f && K(b, c, p) >= -1e-9f && K(c, a, p) >= -1e-9f;
    }

    // ---- Szene ---------------------------------------------------------------------------------------

    /// <summary>
    /// Teile eines Stegs (Bretter als eine flache Platte, Pfosten bis zum Grund an den Seiten, etwa alle 0,6), als
    /// Quader in das vorhandene Netz der Stadt (kein eigener Zeichenaufruf).
    /// </summary>
    public static IEnumerable<Teil> StegTeile(Steg s)
    {
        var (x1, z1, x2, z2) = s.Rechteck;
        yield return new Teil(TeilArt.Quader, x1, StegOben - StegDicke, z1, x2, StegOben, z2, StegHolz);
        var laenge = s.W;
        var anzahl = Math.Max(2, (int)MathF.Ceiling(laenge / 0.6f) + 1);
        const float halb = 0.025f;
        for (var i = 0; i < anzahl; i++)
        {
            var t = anzahl == 1 ? 0.5f : i / (float)(anzahl - 1);
            foreach (var seite in (float[])[0f, 1f])
            {
                float px, pz;
                if (s.Quer)
                {
                    pz = z1 + halb + ((z2 - z1 - (2 * halb)) * t);
                    px = seite == 0 ? x1 + halb : x2 - halb;
                }
                else
                {
                    px = x1 + halb + ((x2 - x1 - (2 * halb)) * t);
                    pz = seite == 0 ? z1 + halb : z2 - halb;
                }
                yield return new Teil(TeilArt.Quader, px - halb, 0f, pz - halb, px + halb, StegOben - StegDicke, pz + halb, StegPfosten);
            }
        }
    }
}

/// <summary>S 0.8.0: eine flache Flaeche am Boden (Teich, Ufer), als Vieleck gegen den Uhrzeigersinn in (x, z).</summary>
public sealed record Bodenflaeche(IReadOnlyList<Vector2> Punkte, float Hoehe, Vector3 Farbe);
