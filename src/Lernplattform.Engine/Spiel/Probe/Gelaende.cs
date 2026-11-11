using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Ein Huegel (Kuppe): Mitte, Radius, Hoehe, rund oder laenglich (Laenge, Drehung in 15-Grad-Schritten).</summary>
public sealed record Huegel(int Nr, float X, float Z, float Radius, float Hoehe, bool Laenglich, float Laenge, int Dreh)
{
    /// <summary>Richtung der Laenge (Drehung 0 = laengs der x-Achse).</summary>
    public Vector2 Richtung => new(MathF.Cos(Dreh * MathF.PI / 180f), MathF.Sin(Dreh * MathF.PI / 180f));

    /// <summary>Halbe Laenge des Rueckens (Abstand der beiden Kuppenmitten von der Mitte), rund = 0.</summary>
    public float Ruecken => Laenglich ? MathF.Max(0f, (Laenge / 2) - Radius) : 0f;

    /// <summary>Abstand eines Punkts zum Ruecken (rund: zur Mitte).</summary>
    public float Abstand(Vector2 p)
    {
        var r = p - new Vector2(X, Z);
        if (Ruecken <= 0f)
        {
            return r.Length();
        }
        var t = Math.Clamp(Vector2.Dot(r, Richtung), -Ruecken, Ruecken);
        return (r - (Richtung * t)).Length();
    }

    /// <summary>Hoehe dieses Huegels an <paramref name="p"/> (Kosinus-Kuppe: oben flach, am Rand ohne Kante).</summary>
    public float HoeheAn(Vector2 p)
    {
        var d = Abstand(p) / Radius;
        return d >= 1f ? 0f : Hoehe * (1f + MathF.Cos(MathF.PI * d)) / 2f;
    }

    /// <summary>Umriss am Boden (fuer Draufsicht, Schatten und Pruefung): Kreis bzw. zwei Halbkreise mit geraden Seiten.</summary>
    public List<Vector2> Umriss(int schritte = 24)
    {
        var m = new Vector2(X, Z);
        var u = new List<Vector2>();
        var a = Richtung;
        var q = new Vector2(-a.Y, a.X);
        for (var seite = 0; seite < 2; seite++)
        {
            var mitte = m + (a * (seite == 0 ? Ruecken : -Ruecken));
            for (var i = 0; i <= schritte / 2; i++)
            {
                var w = (MathF.PI * i / (schritte / 2)) - (MathF.PI / 2) + (seite * MathF.PI);
                u.Add(mitte + (a * (MathF.Cos(w) * Radius)) + (q * (MathF.Sin(w) * Radius)));
            }
        }
        return u;
    }
}

/// <summary>
/// Welle A 0.66.0 / S 0.42.0 (Weisung P12-192, Nico 1c 2b 3c 4a 5a; Hinweise P12-191): Gelaende Stufe 1. Daten
/// <c>stadtdaten.gelaende</c> = <c>{"huegel": [{x, y, radius, hoehe, form: rund|laenglich, laenge?, dreh?}], "gemalt": [[i, j, h], …]}</c>.
/// Huegel sind Kuppen (Radius 1–3, Hoehe 0,2–1,5, laenglich bis Laenge 6, Drehung in 15 Grad); gemalt sind Hoehen in
/// Hundertsteln auf dem Raster <see cref="Raster"/> 0,5 (Punkt i, j = Lage i · 0,5, j · 0,5), dazwischen linear. Die Hoehe
/// ist das Groesste aus Huegeln und Gemaltem, hoechstens <see cref="HoeheMax"/>. Nur Darstellung: Flaeche im Stadtnetz (kein
/// neuer Zeichenaufruf), Stuecke, Baeume und Kleingruen stehen auf der Hoehe ihrer Mitte, die Figur geht darueber; Wege,
/// Fingerabdruecke und Stadt-Uhr bleiben gleich. Ohne den Abschnitt (Fassung 0) gibt es kein Gelaende und nichts aendert sich.
/// </summary>
public sealed class Gelaende
{
    public const string Abschnitt = "gelaende";

    /// <summary>Raster der gemalten Hoehen und des Netzes in der Naehe.</summary>
    public const float Raster = 0.5f;

    /// <summary>Raster des Netzes aus der Ferne (ab <see cref="StadtSzene.FernAb"/>).</summary>
    public const float RasterFern = 1.0f;

    public const float HoeheMax = 1.5f;
    public const float HoeheMin = 0.2f;
    public const float RadiusMin = 1f;
    public const float RadiusMax = 3f;
    public const float LaengeMax = 6f;

    /// <summary>Freier Saum: so weit bleibt jeder Huegel von Haus, Strasse, Weg, Gleis, Wasser, Platz, Haltestelle, Zebra und Ampel.</summary>
    public const float Saum = 1.5f;

    public const int HuegelHoechstens = 16;
    public const int DreieckeHoechstens = 6000;

    /// <summary>Steiler als 45 Grad (Steigung 1) ist ein Hang fuer die Figur gesperrt (E1).</summary>
    public const float SteilAb = 1f;

    /// <summary>
    /// Das Netz liegt so weit ueber der Hoehe (wie der Park), damit es am flachen Rand nicht mit der Wiese um dasselbe
    /// Bildpixel kaempft; Stuecke und Figur stehen auf <see cref="Boden"/>.
    /// </summary>
    public const float Ueber = 0.012f;

    /// <summary>Farbe der Flaeche: die Wiese (Boden der Stadt), damit der Huegel ohne Kante aus der Wiese waechst (E1).</summary>
    public static readonly Vector3 Farbe = Netz.Farbe("#9CC48A");

    private readonly Dictionary<(int I, int J), float> _gemalt;

    public IReadOnlyList<Huegel> Huegel { get; }

    /// <summary>Gemalte Hoehen je Rasterpunkt (i, j) in Feldern (nicht Hundertsteln).</summary>
    public IReadOnlyDictionary<(int I, int J), float> Gemalt => _gemalt;

    public Gelaende(IReadOnlyList<Huegel> huegel, Dictionary<(int I, int J), float> gemalt)
    {
        Huegel = huegel;
        _gemalt = gemalt;
    }

    /// <summary>
    /// Das Gelaende einer Stadt (null ohne Abschnitt). Falsch gespeicherte Eintraege fehlen hier; die Pruefung
    /// (<see cref="Lesen"/>) meldet sie rot.
    /// </summary>
    public static Gelaende? Aus(PyObjekt? stadtDaten) =>
        WeglassenWert.Value || stadtDaten is null || !Py.Hat(stadtDaten, Abschnitt) ? null : Lesen(stadtDaten, []);

    /// <summary>
    /// Szene ohne Gelaende bauen (je Ablauf): die Pruefung „unerreichbar nur wegen des Hangs“ vergleicht damit, Tests nutzen
    /// es als Gegenprobe (Stadt wie vor A 0.66.0).
    /// </summary>
    public static bool Weglassen { get => WeglassenWert.Value; set => WeglassenWert.Value = value; }
    private static readonly AsyncLocal<bool> WeglassenWert = new();

    /// <summary>Gegenprobe: Kleingruen ohne die Regel „Fuss flach“ (nur Tests, je Ablauf).</summary>
    public static bool FlachAltFuerTests { get => FlachAlt.Value; set => FlachAlt.Value = value; }
    private static readonly AsyncLocal<bool> FlachAlt = new();

    /// <summary>Liest das Gelaende und sammelt Texte zu falsch gespeicherten Eintraegen (leer = in Ordnung).</summary>
    public static Gelaende? Lesen(PyObjekt stadtDaten, List<(string Text, Vector2? Stelle)> fehler)
    {
        if (!Py.Hat(stadtDaten, Abschnitt))
        {
            return null;
        }
        var huegel = new List<Huegel>();
        var gemalt = new Dictionary<(int, int), float>();
        if (stadtDaten[Abschnitt] is not PyObjekt g)
        {
            fehler.Add(("Das Gelände ist falsch gespeichert.", null));
            return new Gelaende(huegel, gemalt);
        }
        foreach (var (k, _) in g)
        {
            if (k is not ("huegel" or "gemalt"))
            {
                fehler.Add(($"Das Gelände hat ein unbekanntes Feld „{k}“.", null));
            }
        }
        if (Py.Hat(g, "huegel"))
        {
            if (g["huegel"] is not List<object?> liste)
            {
                fehler.Add(("Die Hügel sind falsch gespeichert.", null));
            }
            else
            {
                for (var i = 0; i < liste.Count; i++)
                {
                    if (HuegelLesen(liste[i], i + 1, out var text) is { } h)
                    {
                        huegel.Add(h);
                    }
                    else
                    {
                        fehler.Add((text, Mitte(liste[i])));
                    }
                }
            }
        }
        if (Py.Hat(g, "gemalt"))
        {
            if (g["gemalt"] is not List<object?> punkte)
            {
                fehler.Add(("Die gemalten Höhen sind falsch gespeichert.", null));
            }
            else
            {
                foreach (var p in punkte)
                {
                    if (p is List<object?> { Count: 3 } q && q[0] is long i && q[1] is long j && q[2] is long h && i >= 0 && j >= 0
                        && h >= 1 && h <= (long)MathF.Round(HoeheMax * 100) && i < 100000 && j < 100000 && !gemalt.ContainsKey(((int)i, (int)j)))
                    {
                        gemalt[((int)i, (int)j)] = h / 100f;
                    }
                    else
                    {
                        fehler.Add(("Eine gemalte Höhe ist falsch gespeichert.", null));
                        break;
                    }
                }
            }
        }
        return new Gelaende(huegel, gemalt);
    }

    private static Vector2? Mitte(object? o) =>
        o is PyObjekt d && d["x"] is long or double && d["y"] is long or double ? new Vector2((float)Py.F(d["x"]), (float)Py.F(d["y"])) : null;

    /// <summary>Ein gespeicherter Huegel (null = falsch gespeichert).</summary>
    public static Huegel? HuegelAus(object? o, int nr) => HuegelLesen(o, nr, out _);

    private static Huegel? HuegelLesen(object? o, int nr, out string text)
    {
        text = $"Hügel Nr. {nr} ist falsch gespeichert.";
        if (o is not PyObjekt d)
        {
            return null;
        }
        bool Zahl(string k) => Py.Hat(d, k) && d[k] is long or double;
        foreach (var (k, _) in d)
        {
            if (k is not ("x" or "y" or "radius" or "hoehe" or "form" or "laenge" or "dreh"))
            {
                return null;
            }
        }
        if (!Zahl("x") || !Zahl("y") || !Zahl("radius") || !Zahl("hoehe") || d["form"] is not ("rund" or "laenglich"))
        {
            return null;
        }
        var laenglich = Py.Gleich(d["form"], "laenglich");
        var r = (float)Py.F(d["radius"]);
        var h = (float)Py.F(d["hoehe"]);
        if (r < RadiusMin - 0.001f || r > RadiusMax + 0.001f)
        {
            text = $"Hügel Nr. {nr}: Radius muss zwischen 1 und 3 liegen.";
            return null;
        }
        if (h < HoeheMin - 0.001f || h > HoeheMax + 0.001f)
        {
            text = $"Hügel Nr. {nr}: Höhe muss zwischen 0,2 und 1,5 liegen.";
            return null;
        }
        var laenge = 0f;
        var dreh = 0;
        if (laenglich)
        {
            if (!Zahl("laenge"))
            {
                return null;
            }
            laenge = (float)Py.F(d["laenge"]);
            if (laenge < (2 * r) - 0.001f || laenge > LaengeMax + 0.001f)
            {
                text = $"Hügel Nr. {nr}: ein länglicher Hügel ist mindestens so lang wie breit und höchstens 6 lang.";
                return null;
            }
        }
        else if (Py.Hat(d, "laenge"))
        {
            return null;
        }
        if (Py.Hat(d, "dreh"))
        {
            if (d["dreh"] is not long dl || dl < 0 || dl >= 360 || dl % 15 != 0)
            {
                text = $"Hügel Nr. {nr}: Drehung nur in Schritten von 15 Grad.";
                return null;
            }
            dreh = (int)dl;
        }
        return new Huegel(nr, (float)Py.F(d["x"]), (float)Py.F(d["y"]), r, h, laenglich, laenge, dreh);
    }

    /// <summary>Hoehe des Gelaendes an (x, z): Groesstes aus Huegeln und Gemaltem, hoechstens <see cref="HoeheMax"/>.</summary>
    public float Hoehe(float x, float z)
    {
        var p = new Vector2(x, z);
        var h = 0f;
        foreach (var k in Huegel)
        {
            h = MathF.Max(h, k.HoeheAn(p));
        }
        if (_gemalt.Count > 0)
        {
            h = MathF.Max(h, GemaltAn(x, z));
        }
        return MathF.Min(h, HoeheMax);
    }

    /// <summary>Gemalte Hoehe an (x, z): linear zwischen den vier Rasterpunkten (fehlender Punkt = 0).</summary>
    public float GemaltAn(float x, float z)
    {
        var fx = x / Raster;
        var fz = z / Raster;
        var i = (int)MathF.Floor(fx);
        var j = (int)MathF.Floor(fz);
        var tx = fx - i;
        var tz = fz - j;
        float W(int a, int b) => _gemalt.GetValueOrDefault((a, b));
        return (W(i, j) * (1 - tx) * (1 - tz)) + (W(i + 1, j) * tx * (1 - tz)) + (W(i, j + 1) * (1 - tx) * tz) + (W(i + 1, j + 1) * tx * tz);
    }

    /// <summary>Hoehe, auf der Stuecke, Baeume, Kleingruen und die Figur stehen (auf dem Netz, ausserhalb 0).</summary>
    public float Boden(float x, float z)
    {
        var h = Hoehe(x, z);
        return h > 0f ? h + Ueber : 0f;
    }

    /// <summary>So weit darf der Fuss eines Baums oder Kleingruens am Rand von der Hoehe der Mitte abweichen.</summary>
    public const float FussSpiel = 0.02f;

    /// <summary>Steht ein runder Fuss (Mitte x, z, Radius r) eben genug (8 Randpunkte bis <see cref="FussSpiel"/>)?</summary>
    public bool Flach(float x, float z, float r)
    {
        if (FlachAlt.Value)
        {
            return true;
        }
        var mitte = Boden(x, z);
        for (var k = 0; k < 8; k++)
        {
            var w = k * MathF.PI / 4;
            if (MathF.Abs(Boden(x + (r * MathF.Cos(w)), z + (r * MathF.Sin(w))) - mitte) > FussSpiel)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Steigung (Betrag des Gefaelles) an (x, z).</summary>
    public float Steigung(float x, float z)
    {
        const float d = 0.05f;
        var gx = (Hoehe(x + d, z) - Hoehe(x - d, z)) / (2 * d);
        var gz = (Hoehe(x, z + d) - Hoehe(x, z - d)) / (2 * d);
        return MathF.Sqrt((gx * gx) + (gz * gz));
    }

    /// <summary>Steiler als 45 Grad: fuer die Figur gesperrt.</summary>
    public bool ZuSteil(float x, float z) => Steigung(x, z) > SteilAb;

    /// <summary>Normale der Flaeche an (x, z).</summary>
    public Vector3 Normale(float x, float z)
    {
        const float d = 0.05f;
        var gx = (Hoehe(x + d, z) - Hoehe(x - d, z)) / (2 * d);
        var gz = (Hoehe(x, z + d) - Hoehe(x, z - d)) / (2 * d);
        return Vector3.Normalize(new Vector3(-gx, 1f, -gz));
    }

    /// <summary>Rechteck, ausserhalb dessen die Hoehe 0 ist (leer = kein Gelaende).</summary>
    public (float X1, float Z1, float X2, float Z2)? Bereich()
    {
        float x1 = float.MaxValue, z1 = float.MaxValue, x2 = float.MinValue, z2 = float.MinValue;
        foreach (var k in Huegel)
        {
            foreach (var p in k.Umriss(8))
            {
                (x1, z1, x2, z2) = (MathF.Min(x1, p.X), MathF.Min(z1, p.Y), MathF.Max(x2, p.X), MathF.Max(z2, p.Y));
            }
            var a = k.Ruecken + k.Radius;
            (x1, z1, x2, z2) = (MathF.Min(x1, k.X - a), MathF.Min(z1, k.Z - a), MathF.Max(x2, k.X + a), MathF.Max(z2, k.Z + a));
        }
        foreach (var ((i, j), _) in _gemalt)
        {
            (x1, z1, x2, z2) = (MathF.Min(x1, (i - 1) * Raster), MathF.Min(z1, (j - 1) * Raster), MathF.Max(x2, (i + 1) * Raster), MathF.Max(z2, (j + 1) * Raster));
        }
        return x1 > x2 ? null : (x1, z1, x2, z2);
    }

    /// <summary>Die Zellen des Netzes (Raster <paramref name="raster"/>) mit Hoehe an wenigstens einer Ecke.</summary>
    public IEnumerable<(int S, int Z)> Zellen(float raster, int spaltenMax, int zeilenMax)
    {
        if (Bereich() is not { } b)
        {
            yield break;
        }
        var s0 = Math.Max(0, (int)MathF.Floor(b.X1 / raster));
        var s1 = Math.Min(spaltenMax - 1, (int)MathF.Ceiling(b.X2 / raster));
        var z0 = Math.Max(0, (int)MathF.Floor(b.Z1 / raster));
        var z1 = Math.Min(zeilenMax - 1, (int)MathF.Ceiling(b.Z2 / raster));
        for (var s = s0; s <= s1; s++)
        {
            for (var z = z0; z <= z1; z++)
            {
                if (Hoehe(s * raster, z * raster) > 0f || Hoehe((s + 1) * raster, z * raster) > 0f
                    || Hoehe(s * raster, (z + 1) * raster) > 0f || Hoehe((s + 1) * raster, (z + 1) * raster) > 0f)
                {
                    yield return (s, z);
                }
            }
        }
    }

    /// <summary>Zahl der Dreiecke des Netzes in der Naehe (Grenze <see cref="DreieckeHoechstens"/>).</summary>
    public int Dreiecke(float breite, float tiefe) => 2 * Zellen(Raster, (int)MathF.Ceiling(breite / Raster), (int)MathF.Ceiling(tiefe / Raster)).Count();

    /// <summary>
    /// Das Netz des Gelaendes in <paramref name="netz"/> (nah Raster 0,5, fern 1,0): je Zelle zwei Dreiecke, Hoehe +
    /// <see cref="Ueber"/>, Normale aus dem Gefaelle, Faerbung des Netzes je Ecke (Winter: Schnee wie die Wiese).
    /// </summary>
    public void Hinzu(Netz netz, float breite, float tiefe, bool fern)
    {
        var raster = fern ? RasterFern : Raster;
        var spalten = (int)MathF.Ceiling(breite / raster);
        var zeilen = (int)MathF.Ceiling(tiefe / raster);
        var ecken = new Dictionary<(int, int), uint>();
        var punkte = new List<Vector3>();
        var normalen = new List<Vector3>();
        var indizes = new List<uint>();
        uint Ecke(int s, int z)
        {
            if (!ecken.TryGetValue((s, z), out var e))
            {
                var x = Math.Min(s * raster, breite);
                var y = Math.Min(z * raster, tiefe);
                e = (uint)punkte.Count;
                ecken[(s, z)] = e;
                punkte.Add(new Vector3(x, Hoehe(x, y) + Ueber, y));
                normalen.Add(Normale(x, y));
            }
            return e;
        }
        foreach (var (s, z) in Zellen(raster, spalten, zeilen))
        {
            var a = Ecke(s, z);
            var b = Ecke(s + 1, z);
            var c = Ecke(s, z + 1);
            var d = Ecke(s + 1, z + 1);
            indizes.AddRange([a, c, b, b, c, d]);
        }
        netz.HinzuGitter(punkte, normalen, Farbe, indizes);
    }

    /// <summary>
    /// Hoehenlinien (Draufsicht des Werkzeugs) auf den Stufen <paramref name="stufe"/>, 2·stufe, … als Strecken; Raster
    /// 0,25, Verfahren „Marching Squares“.
    /// </summary>
    public List<(float Stufe, Vector2 A, Vector2 B)> Hoehenlinien(float stufe, float breite, float tiefe)
    {
        var linien = new List<(float, Vector2, Vector2)>();
        if (Bereich() is not { } b)
        {
            return linien;
        }
        const float r = 0.25f;
        var s0 = Math.Max(0, (int)MathF.Floor(b.X1 / r));
        var s1 = Math.Min((int)(breite / r), (int)MathF.Ceiling(b.X2 / r));
        var z0 = Math.Max(0, (int)MathF.Floor(b.Z1 / r));
        var z1 = Math.Min((int)(tiefe / r), (int)MathF.Ceiling(b.Z2 / r));
        var h = new float[s1 - s0 + 1, z1 - z0 + 1];
        for (var s = s0; s <= s1; s++)
        {
            for (var z = z0; z <= z1; z++)
            {
                h[s - s0, z - z0] = Hoehe(s * r, z * r);
            }
        }
        for (var stufeH = stufe; stufeH <= HoeheMax + 0.001f; stufeH += stufe)
        {
            for (var s = s0; s < s1; s++)
            {
                for (var z = z0; z < z1; z++)
                {
                    var p = new[] { new Vector2(s * r, z * r), new Vector2((s + 1) * r, z * r), new Vector2((s + 1) * r, (z + 1) * r), new Vector2(s * r, (z + 1) * r) };
                    var w = new[] { h[s - s0, z - z0], h[s + 1 - s0, z - z0], h[s + 1 - s0, z + 1 - z0], h[s - s0, z + 1 - z0] };
                    var schnitte = new List<Vector2>();
                    for (var k = 0; k < 4; k++)
                    {
                        var (wa, wb) = (w[k], w[(k + 1) % 4]);
                        if ((wa < stufeH) != (wb < stufeH))
                        {
                            schnitte.Add(Vector2.Lerp(p[k], p[(k + 1) % 4], (stufeH - wa) / (wb - wa)));
                        }
                    }
                    for (var k = 0; k + 1 < schnitte.Count; k += 2)
                    {
                        linien.Add((stufeH, schnitte[k], schnitte[k + 1]));
                    }
                }
            }
        }
        return linien;
    }

    /// <summary>Daten eines Huegels zum Speichern (Zahlen auf 0,01, Drehung ganz).</summary>
    public static PyObjekt HuegelDaten(float x, float z, float radius, float hoehe, bool laenglich, float laenge, int dreh)
    {
        var d = new PyObjekt
        {
            { "x", Math.Round(x, 2) },
            { "y", Math.Round(z, 2) },
            { "radius", Math.Round(radius, 2) },
            { "hoehe", Math.Round(hoehe, 2) },
            { "form", laenglich ? "laenglich" : "rund" },
        };
        if (laenglich)
        {
            d.Add("laenge", Math.Round(laenge, 2));
            d.Add("dreh", (long)dreh);
        }
        return d;
    }

    /// <summary>Kurzer Text zu einem Huegel (Werkzeug).</summary>
    public static string Beschreibung(Huegel h) => string.Create(CultureInfo.GetCultureInfo("de-DE"),
        $"Hügel Nr. {h.Nr}: Radius {h.Radius:0.##}, Höhe {h.Hoehe:0.##}{(h.Laenglich ? $", länglich {h.Laenge:0.##}, {h.Dreh} Grad" : "")}");
}
