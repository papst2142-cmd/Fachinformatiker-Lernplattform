using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Ein Haus ohne Ort mit Gehweg, Tuer und Namensschild (nicht betretbar, Phase 8 W8b: Stellwerk).</summary>
public sealed record StadtZierhaus(string Name, Vector3 Schild, Vector3 KastenMin, Vector3 KastenMax, float GehX, float GehZ);

/// <summary>
/// Phase 8 W8b (Weisung P8-20, Nico 4a): Tueren und Gehwege der Stadt, nur Darstellung. Jeder Fussweg endet
/// mittig an einer Tuer auf der Wand: hat das CC0-Modell selbst eine Tuer zum Weg, wird es in x so verschoben,
/// dass sie auf dem Weg sitzt (<c>darstellung.modell_tuer</c> in stadt.json), sonst bekommt das Haus eine Tuer
/// aus einfachen Kaesten (Rahmen, zwei Glasfluegel, Stufe, Vordach).
/// </summary>
public static class StadtTueren
{
    public static readonly Vector3 Rahmen = Netz.Farbe("#3A3550");
    public static readonly Vector3 Glas = Netz.Farbe("#7FB8D6");
    public static readonly Vector3 GlasHell = Netz.Farbe("#A5E9F5");
    public static readonly Vector3 Stufe = Netz.Farbe("#8E8AA3");
    public static readonly Vector3 Vordach = Netz.Farbe("#F4B942");

    /// <summary>Abschnitt <c>darstellung</c> aus stadt.json (leer, wenn er fehlt).</summary>
    public static PyObjekt Darstellung(Stadt stadt) => Get(stadt.Daten, "darstellung") as PyObjekt ?? [];

    /// <summary>Versatz des Modells in x, wenn es seine eigene Tuer zum Weg hat; sonst null.</summary>
    public static float? ModellVersatz(Stadt stadt, string ort) =>
        Get(Get(Darstellung(stadt), "modell_tuer") as PyObjekt ?? [], ort) is { } v ? (float)Py.F(v) : null;

    /// <summary>Gehwege mit Tuer an Haeusern ohne Ort (zierwege).</summary>
    public static IEnumerable<PyObjekt> Zierwege(Stadt stadt) =>
        (Get(Darstellung(stadt), "zierwege") as List<object?> ?? []).Select(Objekt);

    /// <summary>
    /// Tuer aus Kaesten: Mitte (x, z) auf der Wand, Richtung n zeigt nach aussen. Rahmen, zwei Glasfluegel
    /// mit hellem Oberlicht, Stufe davor, Vordach darueber.
    /// </summary>
    public static void Tuer(List<Teil> teile, float x, float z, Vector2 n, float hoehe, float hinten = 0f)
    {
        var b = StadtSzene.TuerBreite;
        var quer = new Vector2(-n.Y, n.X);
        void Q(float a1, float a2, float y1, float y2, float d1, float d2, Vector3 f)
        {
            // a = entlang der Wand, d = nach aussen
            var p1 = new Vector2(x, z) + (quer * a1) + (n * d1);
            var p2 = new Vector2(x, z) + (quer * a2) + (n * d2);
            teile.Add(new Teil(TeilArt.Quader, MathF.Min(p1.X, p2.X), y1, MathF.Min(p1.Y, p2.Y), MathF.Max(p1.X, p2.X), y2, MathF.Max(p1.Y, p2.Y), f));
        }
        var h = hoehe;
        // P8-28: der Rahmen reicht nach hinten bis an die tiefste Stelle der Wand dahinter (kein Spalt)
        Q(-(b / 2) - 0.05f, (b / 2) + 0.05f, 0f, h + 0.05f, -0.02f - hinten, 0.03f, Rahmen);
        Q(-(b / 2) + 0.03f, -0.015f, 0.02f, h - 0.03f, 0.03f, 0.045f, Glas);
        Q(0.015f, (b / 2) - 0.03f, 0.02f, h - 0.03f, 0.03f, 0.045f, Glas);
        Q(-(b / 2) + 0.06f, -0.04f, h * 0.55f, h - 0.06f, 0.045f, 0.05f, GlasHell);
        Q(0.04f, (b / 2) - 0.06f, h * 0.55f, h - 0.06f, 0.045f, 0.05f, GlasHell);
        Q(-(b / 2) - 0.08f, (b / 2) + 0.08f, 0f, 0.035f, 0.0f, 0.16f, Stufe);
        Q(-(b / 2) - 0.12f, (b / 2) + 0.12f, h + 0.05f, h + 0.1f, 0f, 0.3f, Vordach);
    }

    /// <summary>Rand um die Tuer (Rahmen, Vordach), in dem kein Fenster liegen darf (Weisung P8-28).</summary>
    public const float Rand = 0.05f;

    /// <summary>Halbe Breite der Tuer mit Rahmen und Rand, quer zur Wand.</summary>
    public static float HalbeFlaeche => (StadtSzene.TuerBreite / 2) + 0.05f + Rand;

    /// <summary>Glas eines CC0-Modells: deutlich blau (Fensterscheiben der Pakete, z. B. #BCE2FF, #87AEE5).</summary>
    public static bool IstGlas(Vector3 f) => f.Z >= 0.85f && f.Z - f.X >= 0.2f;

    /// <summary>
    /// Weisung P8-28 (Nico: Tueren sitzen nicht am Haus): die CC0-Modelle fuellen ihren Kasten nicht ganz, ihre
    /// Wand liegt oft hinter der Kastenwand (Vorbau, Vordach, Saeulen). Gemessen mit Strahlen von aussen auf die
    /// Tuerflaeche (9 Spalten ueber die Glasbreite, 4 Hoehen): Tiefe der Tuer hinter der Kastenwand und Tiefe der
    /// hintersten getroffenen Stelle. Steht in der Mitte ein Pfeiler vor der Wand, sitzt die Tuer vor dem Pfeiler
    /// (sonst stuende er mitten in der Tuer), sonst auf der Wand (Mittel der Spalten). Null, wenn das Modell dort fehlt.
    /// </summary>
    public static (float Tiefe, float Hinten)? Wandtiefe(IReadOnlyList<ModellStueck> stuecke, GrafikBibliothek grafik, Vector2 wand, Vector2 aussen, float hoehe)
    {
        var quer = new Vector2(-aussen.Y, aussen.X);
        var halb = (StadtSzene.TuerBreite / 2) - 0.03f;
        var spalten = new List<(int I, float T)>();
        var hinten = 0f;
        for (var i = -4; i <= 4; i++)
        {
            var treffer = new List<float>();
            for (var j = 1; j <= 4; j++)
            {
                var p = wand + (quer * (i * halb / 4)) + (aussen * 0.5f);
                if (Treffer(stuecke, grafik, new Vector3(p.X, hoehe * j / 5f, p.Y), new Vector3(-aussen.X, 0, -aussen.Y), 1.4f) is { } t && t.T - 0.5f >= -0.02f)
                {
                    treffer.Add(t.T - 0.5f);
                }
            }
            if (treffer.Count >= 2)
            {
                spalten.Add((i, Mitte(treffer)));
                hinten = MathF.Max(hinten, treffer.Max());
            }
        }
        if (spalten.Count < 5)
        {
            return null;
        }
        var mittel = Mitte([.. spalten.Select(s => s.T)]);
        var mitteSpalte = spalten.Where(s => s.I == 0).Select(s => (float?)s.T).FirstOrDefault();
        var tiefe = mitteSpalte is { } m && m < mittel - 0.03f ? m : mittel;
        return (MathF.Max(0f, tiefe), MathF.Max(0f, hinten - MathF.Max(0f, tiefe)));
    }

    private static float Mitte(List<float> werte)
    {
        werte.Sort();
        return werte.Count % 2 == 1 ? werte[werte.Count / 2] : (werte[(werte.Count / 2) - 1] + werte[werte.Count / 2]) / 2;
    }

    /// <summary>
    /// Liegt ein Dreieck (a, b, c) auf der Flaeche einer Tuer? Quer zur Wand innerhalb von <see cref="HalbeFlaeche"/>,
    /// unten bis ueber das Vordach (Hoehe + 0,1 + Rand), in der Tiefe zwischen Rand vor der Tuer und Rand hinter der
    /// hintersten Stelle der Wand (mitte = Tuer auf der Wand, aussen = Richtung nach draussen).
    /// </summary>
    public static bool AufTuerFlaeche(Vector3 a, Vector3 b, Vector3 c, Vector2 mitte, Vector2 aussen, float hoehe, float hinten)
    {
        var quer = new Vector2(-aussen.Y, aussen.X);
        float qmin = float.MaxValue, qmax = float.MinValue, dmin = float.MaxValue, dmax = float.MinValue;
        foreach (var p in (ReadOnlySpan<Vector3>)[a, b, c])
        {
            var r = new Vector2(p.X, p.Z) - mitte;
            var q = Vector2.Dot(r, quer);
            var d = -Vector2.Dot(r, aussen);
            (qmin, qmax, dmin, dmax) = (MathF.Min(qmin, q), MathF.Max(qmax, q), MathF.Min(dmin, d), MathF.Max(dmax, d));
        }
        var unten = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
        return qmax > -HalbeFlaeche && qmin < HalbeFlaeche && unten < hoehe + 0.1f + Rand && dmin < hinten + Rand && dmax > -Rand;
    }

    /// <summary>
    /// Weisung P8-28 (Nico: "Bitte dann die Fenster entfernen, wenn dort eine Tuer ist."): Glas-Dreiecke der
    /// CC0-Stuecke [von, bis), die auf der Tuerflaeche liegen, bekommen die haeufigste Wandfarbe dieses Stuecks
    /// an der Tuer. Das Stueck behaelt Namen und Lage; die umgefaerbte Fassung steht in <paramref name="umgefaerbt"/>.
    /// </summary>
    public static void FensterWeg(List<ModellStueck> stuecke, int von, int bis, GrafikBibliothek grafik, Dictionary<int, GrafikModell> umgefaerbt,
        Vector2 mitte, Vector2 aussen, float hoehe, float hinten)
    {
        for (var i = von; i < bis; i++)
        {
            var gm = umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name);
            if (gm is null)
            {
                continue;
            }
            var weg = new List<int>();
            var wand = new Dictionary<Vector3, int>();
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                var (a, b, c) = Ecken(gm, d, stuecke[i].Lage);
                var f = new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f;
                if (!AufTuerFlaeche(a, b, c, mitte, aussen, hoehe, hinten))
                {
                    continue;
                }
                if (IstGlas(f))
                {
                    weg.Add(d);
                }
                else
                {
                    wand[f] = wand.GetValueOrDefault(f) + 1;
                }
            }
            if (weg.Count == 0)
            {
                continue;
            }
            // haeufigste Farbe ohne Glas (bei Gleichstand die zuerst gezaehlte), ohne Wand dort Grau
            var farbe = wand.Count > 0 ? wand.MaxBy(w => w.Value).Key : new Vector3(0.56f, 0.57f, 0.6f);
            var farben = (byte[])gm.Farben.Clone();
            foreach (var d in weg)
            {
                farben[d * 3] = (byte)MathF.Round(farbe.X * 255);
                farben[(d * 3) + 1] = (byte)MathF.Round(farbe.Y * 255);
                farben[(d * 3) + 2] = (byte)MathF.Round(farbe.Z * 255);
            }
            umgefaerbt[i] = new GrafikModell(gm.Name, gm.Ecken, farben);
        }
    }

    /// <summary>Die drei Ecken eines Dreiecks in der Welt.</summary>
    public static (Vector3 A, Vector3 B, Vector3 C) Ecken(GrafikModell gm, int d, Matrix4x4 lage)
    {
        var e = gm.Ecken;
        return (Vector3.Transform(new Vector3(e[d * 9], e[(d * 9) + 1], e[(d * 9) + 2]), lage),
            Vector3.Transform(new Vector3(e[(d * 9) + 3], e[(d * 9) + 4], e[(d * 9) + 5]), lage),
            Vector3.Transform(new Vector3(e[(d * 9) + 6], e[(d * 9) + 7], e[(d * 9) + 8]), lage));
    }

    /// <summary>
    /// Erster Treffer eines Strahls (Start, Richtung mit Laenge 1) auf die Dreiecke der CC0-Stuecke, beidseitig,
    /// hoechstens <paramref name="weit"/> weit: Entfernung und Farbe des getroffenen Dreiecks, sonst null.
    /// </summary>
    public static (float T, Vector3 Farbe)? Treffer(IEnumerable<ModellStueck> stuecke, GrafikBibliothek grafik, Vector3 start, Vector3 richtung, float weit)
    {
        (float T, Vector3 Farbe)? best = null;
        foreach (var st in stuecke)
        {
            if (grafik.Modell(st.Name) is not { } gm)
            {
                continue;
            }
            var e = gm.Ecken;
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                var a = Vector3.Transform(new Vector3(e[d * 9], e[(d * 9) + 1], e[(d * 9) + 2]), st.Lage);
                var b = Vector3.Transform(new Vector3(e[(d * 9) + 3], e[(d * 9) + 4], e[(d * 9) + 5]), st.Lage);
                var c = Vector3.Transform(new Vector3(e[(d * 9) + 6], e[(d * 9) + 7], e[(d * 9) + 8]), st.Lage);
                // Moeller-Trumbore
                var k1 = b - a;
                var k2 = c - a;
                var pv = Vector3.Cross(richtung, k2);
                var det = Vector3.Dot(k1, pv);
                if (MathF.Abs(det) < 1e-9f)
                {
                    continue;
                }
                var tv = start - a;
                var u = Vector3.Dot(tv, pv) / det;
                if (u < 0 || u > 1)
                {
                    continue;
                }
                var qv = Vector3.Cross(tv, k1);
                var v = Vector3.Dot(richtung, qv) / det;
                if (v < 0 || u + v > 1)
                {
                    continue;
                }
                var t = Vector3.Dot(k2, qv) / det;
                if (t >= 0 && t <= weit && (best is null || t < best.Value.T))
                {
                    best = (t, new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f);
                }
            }
        }
        return best;
    }

    /// <summary>Naechste Wand eines Kastens zum Punkt p: Punkt auf der Wand und Richtung nach aussen.</summary>
    public static (Vector2 Wand, Vector2 Aussen) Wand(Vector3 min, Vector3 max, Vector2 p)
    {
        var kandidaten = new (float D, Vector2 W, Vector2 N)[]
        {
            (MathF.Abs(p.Y - max.Z), new(Math.Clamp(p.X, min.X, max.X), max.Z), new(0, 1)),
            (MathF.Abs(p.Y - min.Z), new(Math.Clamp(p.X, min.X, max.X), min.Z), new(0, -1)),
            (MathF.Abs(p.X - max.X), new(max.X, Math.Clamp(p.Y, min.Z, max.Z)), new(1, 0)),
            (MathF.Abs(p.X - min.X), new(min.X, Math.Clamp(p.Y, min.Z, max.Z)), new(-1, 0)),
        };
        // nur Waende, deren Spanne den Punkt enthaelt
        var passend = kandidaten.Where(k => k.N.X == 0 ? p.X >= min.X - 0.01f && p.X <= max.X + 0.01f : p.Y >= min.Z - 0.01f && p.Y <= max.Z + 0.01f).ToList();
        var best = (passend.Count > 0 ? passend : [.. kandidaten]).MinBy(k => k.D);
        return (best.W, best.N);
    }
}
