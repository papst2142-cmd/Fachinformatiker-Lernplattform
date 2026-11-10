using System.Globalization;
using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Art eines Szenenteils der 3D-Stichprobe (Phase 7 W1).</summary>
public enum TeilArt
{
    /// <summary>Quader von (X1, Y1, Z1) bis (X2, Y2, Z2).</summary>
    Quader,

    /// <summary>Satteldach ueber der Grundflaeche X1..X2 / Z1..Z2 von Hoehe Y1 bis First Y2, First laengs der laengeren Seite.</summary>
    Satteldach,

    /// <summary>
    /// Flaches Band am Boden (Strasse, Fluss, Gleis) von (X1, Z1) nach (X2, Z2) auf Hoehe Y1 mit
    /// Breite Y2 (Phase 7 W4).
    /// </summary>
    Band,
}

/// <summary>
/// Faerbung eines CC0-Modells je Ecke (Phase 8 W9, je Ecke seit Weisung P12-50): Nummer des Dreiecks, Grundfarbe der Ecke,
/// Betrag der Normale nach oben, mittlere Hoehe des Dreiecks -&gt; Farbe der Ecke.
/// </summary>
public delegate Vector3 Modellfaerbung(int dreieck, Vector3 grund, float oben, float hoehe);

/// <summary>Ein Teil der Szene in Weltkoordinaten (X nach rechts, Y nach oben, Z zum Betrachter).</summary>
public readonly record struct Teil(TeilArt Art, float X1, float Y1, float Z1, float X2, float Y2, float Z2, Vector3 Farbe);

/// <summary>
/// Dreiecksnetz fuer die Darstellung: je Ecke Ort (3), Normale (3), Farbe (3) = 9 Gleitkommazahlen.
/// Flache Schattierung (jede Flaeche eigene Ecken), wie bei Low-Poly-Grafik ueblich.
/// </summary>
public sealed class Netz
{
    public const int Zahlen = 9;

    private readonly List<float> _ecken = [];
    private readonly List<uint> _indizes = [];

    public IReadOnlyList<float> Ecken => _ecken;

    public IReadOnlyList<uint> Indizes => _indizes;

    /// <summary>Weisung P7-16: die Ecken ohne Kopie (zum Hochladen in die Grafikkarte).</summary>
    public ReadOnlySpan<float> EckenFeld => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_ecken);

    /// <summary>Weisung P7-16: die Indizes ohne Kopie.</summary>
    public ReadOnlySpan<uint> IndizesFeld => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_indizes);

    public int AnzahlEcken => _ecken.Count / Zahlen;

    /// <summary>
    /// Phase 8 W9: Faerbung der folgenden eigenen Teile und Streifen je Flaeche (Farbe, Betrag der Normale nach
    /// oben, mittlere Hoehe) -> neue Farbe; null = Farbe wie angegeben (Sommer, bitgleich wie vorher).
    /// </summary>
    public Func<Vector3, float, float, Vector3>? Faerbung { get; set; }

    /// <summary>
    /// Welle D1 (Weisung P12-143, Teil D): Unterseiten am Boden weglassen. Ist ein Wert gesetzt, kommt ein Dreieck eines
    /// CC0-Modells nicht ins Netz, wenn es nach unten zeigt (Normale nach unten steiler als <see cref="UnterseiteNeigung"/>)
    /// und alle drei Ecken hoechstens auf dieser Hoehe liegen. Die Kamera ist nie so tief (kleinster Abstand 2, flachste
    /// Neigung 25 Grad: Auge ueber 0,8), also sieht man diese Flaechen nie. null = alle Dreiecke (bitgleich wie vorher).
    /// </summary>
    public float? UnterseiteBis { get; set; }

    /// <summary>Welle D1: ab diesem Anteil der Normale nach unten zaehlt ein Dreieck als Unterseite.</summary>
    public const float UnterseiteNeigung = 0.9f;

    /// <summary>Welle D1: so viele Unterseiten am Boden wurden in dieses Netz nicht aufgenommen (Tests, Messung).</summary>
    public int UnterseitenWeg { get; private set; }

    public static Vector3 Farbe(string hex)
    {
        var h = hex.TrimStart('#');
        var wert = int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Vector3(((wert >> 16) & 255) / 255f, ((wert >> 8) & 255) / 255f, (wert & 255) / 255f);
    }

    public static Netz Aus(IEnumerable<Teil> teile)
    {
        var netz = new Netz();
        if (teile.TryGetNonEnumeratedCount(out var anzahl))
        {
            // Weisung P7-16: Platz fuer Quader (24 Ecken, 36 Indizes) vorab, statt die Listen oft zu vergroessern
            netz._ecken.Capacity = anzahl * 24 * Zahlen;
            netz._indizes.Capacity = anzahl * 36;
        }
        foreach (var teil in teile)
        {
            netz.Hinzu(teil);
        }
        return netz;
    }

    public void Hinzu(Teil t)
    {
        if (t.Art == TeilArt.Satteldach)
        {
            Satteldach(t);
            return;
        }
        if (t.Art == TeilArt.Band)
        {
            Band(t);
            return;
        }
        Vector3 a = new(t.X1, t.Y1, t.Z1), b = new(t.X2, t.Y2, t.Z2);
        // sechs Seiten, Ecken gegen den Uhrzeigersinn von aussen gesehen
        Viereck(new(a.X, b.Y, a.Z), new(a.X, b.Y, b.Z), new(b.X, b.Y, b.Z), new(b.X, b.Y, a.Z), t.Farbe);   // oben
        Viereck(new(a.X, a.Y, a.Z), new(b.X, a.Y, a.Z), new(b.X, a.Y, b.Z), new(a.X, a.Y, b.Z), t.Farbe);   // unten
        Viereck(new(a.X, a.Y, b.Z), new(b.X, a.Y, b.Z), new(b.X, b.Y, b.Z), new(a.X, b.Y, b.Z), t.Farbe);   // vorne (+Z)
        Viereck(new(b.X, a.Y, a.Z), new(a.X, a.Y, a.Z), new(a.X, b.Y, a.Z), new(b.X, b.Y, a.Z), t.Farbe);   // hinten
        Viereck(new(a.X, a.Y, a.Z), new(a.X, a.Y, b.Z), new(a.X, b.Y, b.Z), new(a.X, b.Y, a.Z), t.Farbe);   // links
        Viereck(new(b.X, a.Y, b.Z), new(b.X, a.Y, a.Z), new(b.X, b.Y, a.Z), new(b.X, b.Y, b.Z), t.Farbe);   // rechts
    }

    /// <summary>Ein CC0-Modell an seiner Lage einfuegen (flache Normale je Dreieck, Farbe je Ecke, Weisung P12-50).</summary>
    public void Hinzu(GrafikModell modell, Matrix4x4 lage) => Hinzu(modell, lage, null);

    /// <summary>
    /// Phase 8 W9: wie <see cref="Hinzu(GrafikModell, Matrix4x4)"/>, jede Ecke in der Farbe aus <paramref name="faerbung"/>
    /// (Nummer des Dreiecks im Modell, Farbe der Ecke, Betrag der Normale nach oben, mittlere Hoehe des Dreiecks). Seit
    /// A 0.43.6 (Weisung P12-50) wird die Faerbung je Ecke mit der Eckfarbe gerufen; alles, was sie entscheidet (Schnee,
    /// Licht, Pflanze), haengt nur am Dreieck, damit bleibt jedes Dreieck in einer Art gefaerbt.
    /// <paramref name="dreiecksfarben"/> (wenn angegeben) bekommt je eingefuegtem Dreieck die Farbe des Dreiecks nach der
    /// Faerbung (zum Erkennen von Glas wie vor dem Farbverlauf, <see cref="Nachtlicht.GlasMarkieren"/>).
    /// </summary>
    public void Hinzu(GrafikModell modell, Matrix4x4 lage, Modellfaerbung? faerbung, List<Vector3>? dreiecksfarben = null)
    {
        var e = modell.Ecken;
        for (var d = 0; d < modell.Dreiecke; d++)
        {
            var a = Vector3.Transform(new Vector3(e[d * 9], e[(d * 9) + 1], e[(d * 9) + 2]), lage);
            var b = Vector3.Transform(new Vector3(e[(d * 9) + 3], e[(d * 9) + 4], e[(d * 9) + 5]), lage);
            var c = Vector3.Transform(new Vector3(e[(d * 9) + 6], e[(d * 9) + 7], e[(d * 9) + 8]), lage);
            var kreuz = Vector3.Cross(b - a, c - a);
            if (kreuz.LengthSquared() < 1e-14f)
            {
                continue;
            }
            if (UnterseiteBis is { } bis && MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) <= bis
                && kreuz.Y < -UnterseiteNeigung * MathF.Sqrt(kreuz.LengthSquared()))
            {
                UnterseitenWeg++;
                continue;
            }
            var grund = modell.Farbe(d);
            float oben = 0, hoehe = 0;
            if (faerbung is not null)
            {
                oben = MathF.Abs(kreuz.Y) / MathF.Sqrt(kreuz.LengthSquared());
                hoehe = (a.Y + b.Y + c.Y) / 3;
            }
            Vector3 Farbe(Vector3 f) => faerbung is null ? f : faerbung(d, f, oben, hoehe);
            dreiecksfarben?.Add(Farbe(grund));
            if (modell.EckFarben is null)
            {
                Dreieck(a, b, c, Farbe(grund));
            }
            else
            {
                Dreieck(a, b, c, Farbe(modell.EckFarbe(d, 0)), Farbe(modell.EckFarbe(d, 1)), Farbe(modell.EckFarbe(d, 2)));
            }
        }
    }

    /// <summary>Ein Streifen: Oberseite (und bei Hoehe Seiten und Enden) entlang des Linienzugs, Ecken auf Gehrung.</summary>
    public void Hinzu(Streifen s)
    {
        if (s.Punkte.Count < 2)
        {
            return;
        }
        var n = Kurve.Normalen(s.Punkte);
        var halb = s.Breite / 2;
        var links = new List<Vector2>();
        var rechts = new List<Vector2>();
        for (var i = 0; i < s.Punkte.Count; i++)
        {
            var q = n[i].Quer * (halb * n[i].Faktor);
            links.Add(s.Punkte[i] + q);
            rechts.Add(s.Punkte[i] - q);
        }
        var hoch = s.Oben - s.Unten > 1e-5f;
        Vector3 O(Vector2 p) => new(p.X, s.Oben, p.Y);
        Vector3 U(Vector2 p) => new(p.X, s.Unten, p.Y);
        for (var i = 0; i + 1 < s.Punkte.Count; i++)
        {
            Flaeche(O(rechts[i]), O(links[i]), O(links[i + 1]), O(rechts[i + 1]), s.Farbe, Vector3.UnitY);
            if (hoch)
            {
                var seite = s.Farbe * 0.85f;
                Flaeche(U(links[i]), U(links[i + 1]), O(links[i + 1]), O(links[i]), seite, null);
                Flaeche(U(rechts[i + 1]), U(rechts[i]), O(rechts[i]), O(rechts[i + 1]), seite, null);
            }
        }
        if (hoch)
        {
            Flaeche(U(rechts[0]), U(links[0]), O(links[0]), O(rechts[0]), s.Farbe * 0.85f, null);
            Flaeche(U(links[^1]), U(rechts[^1]), O(rechts[^1]), O(links[^1]), s.Farbe * 0.85f, null);
        }
    }

    /// <summary>
    /// S 0.8.0 (Weisung P12-47): eine flache Flaeche am Boden (Teich, Ufer) in Dreiecken (<see cref="Wasser.Dreiecke"/>),
    /// Normale nach oben, Faerbung wie bei den Streifen (Winter, Nacht wie der Boden).
    /// </summary>
    public void Hinzu(Bodenflaeche f)
    {
        var p = f.Punkte;
        if (p.Count < 3)
        {
            return;
        }
        var farbe = Faerbung is { } faerbung ? faerbung(f.Farbe, 1f, f.Hoehe) : f.Farbe;
        foreach (var (ia, ib, ic) in Wasser.Dreiecke(p))
        {
            // gegen den Uhrzeigersinn in (x, z) zeigt das Kreuzprodukt nach unten: b und c tauschen (Normale nach oben)
            var basis = (uint)AnzahlEcken;
            Ecke(new Vector3(p[ia].X, f.Hoehe, p[ia].Y), Vector3.UnitY, farbe);
            Ecke(new Vector3(p[ic].X, f.Hoehe, p[ic].Y), Vector3.UnitY, farbe);
            Ecke(new Vector3(p[ib].X, f.Hoehe, p[ib].Y), Vector3.UnitY, farbe);
            _indizes.AddRange([basis, basis + 1, basis + 2]);
        }
    }

    /// <summary>Viereck mit fester Normale (oben) oder aus den Ecken; ohne Flaeche (entartet) nichts.</summary>
    private void Flaeche(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 farbe, Vector3? normale)
    {
        var kreuz = Vector3.Cross(b - a, c - a);
        if (kreuz.LengthSquared() < 1e-14f)
        {
            kreuz = Vector3.Cross(c - a, d - a);
            if (kreuz.LengthSquared() < 1e-14f)
            {
                return;
            }
        }
        var n = normale ?? Vector3.Normalize(kreuz);
        if (Faerbung is { } f)
        {
            farbe = f(farbe, MathF.Abs(n.Y), (a.Y + b.Y + c.Y + d.Y) / 4);
        }
        var basis = (uint)AnzahlEcken;
        foreach (var p in (Vector3[])[a, b, c, d])
        {
            Ecke(p, n, farbe);
        }
        _indizes.AddRange([basis, basis + 1, basis + 2, basis, basis + 2, basis + 3]);
    }

    private void Satteldach(Teil t)
    {
        var laengsX = t.X2 - t.X1 >= t.Z2 - t.Z1;
        var f = t.Farbe;
        if (laengsX)
        {
            var mz = (t.Z1 + t.Z2) / 2;
            Vector3 l1 = new(t.X1, t.Y1, t.Z1), l2 = new(t.X1, t.Y1, t.Z2), r1 = new(t.X2, t.Y1, t.Z1), r2 = new(t.X2, t.Y1, t.Z2);
            Vector3 fl = new(t.X1, t.Y2, mz), fr = new(t.X2, t.Y2, mz);
            Viereck(l2, r2, fr, fl, f);       // vordere Dachflaeche
            Viereck(r1, l1, fl, fr, f);       // hintere Dachflaeche
            Dreieck(l1, l2, fl, f * 0.9f);    // Giebel links
            Dreieck(r2, r1, fr, f * 0.9f);    // Giebel rechts
        }
        else
        {
            var mx = (t.X1 + t.X2) / 2;
            Vector3 v1 = new(t.X1, t.Y1, t.Z2), v2 = new(t.X2, t.Y1, t.Z2), h1 = new(t.X1, t.Y1, t.Z1), h2 = new(t.X2, t.Y1, t.Z1);
            Vector3 fv = new(mx, t.Y2, t.Z2), fh = new(mx, t.Y2, t.Z1);
            Viereck(h1, v1, fv, fh, f);       // linke Dachflaeche
            Viereck(v2, h2, fh, fv, f);       // rechte Dachflaeche
            Dreieck(v1, v2, fv, f * 0.9f);    // Giebel vorne
            Dreieck(h2, h1, fh, f * 0.9f);    // Giebel hinten
        }
    }

    private void Band(Teil t)
    {
        var richtung = new Vector2(t.X2 - t.X1, t.Z2 - t.Z1);
        if (richtung.LengthSquared() < 1e-12f)
        {
            return;
        }
        var quer = Vector2.Normalize(new Vector2(-richtung.Y, richtung.X)) * (t.Y2 / 2);
        Vector3 Ecke(float x, float z, Vector2 q) => new(x + q.X, t.Y1, z + q.Y);
        var a = Ecke(t.X1, t.Z1, -quer);
        var b = Ecke(t.X1, t.Z1, quer);
        var c = Ecke(t.X2, t.Z2, quer);
        var d = Ecke(t.X2, t.Z2, -quer);
        // Normale nach oben: Reihenfolge so waehlen, dass das Kreuzprodukt +Y zeigt
        if (Vector3.Cross(b - a, c - a).Y < 0)
        {
            (b, d) = (d, b);
        }
        Viereck(a, b, c, d, t.Farbe);
    }

    private void Viereck(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 farbe)
    {
        var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        if (Faerbung is { } f)
        {
            farbe = f(farbe, MathF.Abs(n.Y), (a.Y + b.Y + c.Y + d.Y) / 4);
        }
        var basis = (uint)AnzahlEcken;
        foreach (var p in (Vector3[])[a, b, c, d])
        {
            Ecke(p, n, farbe);
        }
        _indizes.AddRange([basis, basis + 1, basis + 2, basis, basis + 2, basis + 3]);
    }

    /// <summary>Ein fertiges Netz an seiner Lage einfuegen (Schaufensterpuppe mit Kleidung).</summary>
    public void Hinzu(Netz andere, Matrix4x4 lage)
    {
        var basis = (uint)AnzahlEcken;
        var e = andere._ecken;
        for (var i = 0; i < e.Count; i += Zahlen)
        {
            var p = Vector3.Transform(new Vector3(e[i], e[i + 1], e[i + 2]), lage);
            var n = Vector3.Normalize(Vector3.TransformNormal(new Vector3(e[i + 3], e[i + 4], e[i + 5]), lage));
            Ecke(p, n, new Vector3(e[i + 6], e[i + 7], e[i + 8]));
        }
        foreach (var j in andere._indizes)
        {
            _indizes.Add(basis + j);
        }
    }

    private void Dreieck(Vector3 a, Vector3 b, Vector3 c, Vector3 farbe) => Dreieck(a, b, c, farbe, farbe, farbe);

    /// <summary>Weisung P12-50: ein Dreieck mit einer Farbe je Ecke (die Grafikkarte verlaeuft dazwischen).</summary>
    private void Dreieck(Vector3 a, Vector3 b, Vector3 c, Vector3 fa, Vector3 fb, Vector3 fc)
    {
        var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        var basis = (uint)AnzahlEcken;
        Ecke(a, n, fa);
        Ecke(b, n, fb);
        Ecke(c, n, fc);
        _indizes.AddRange([basis, basis + 1, basis + 2]);
    }

    private void Ecke(Vector3 p, Vector3 n, Vector3 farbe)
    {
        _ecken.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, farbe.X, farbe.Y, farbe.Z]);
    }

    /// <summary>
    /// Phase 8 W10 (Nico 3b): Marke "leuchtet nachts" auf die Farbe der Ecken von <paramref name="von"/> bis vor
    /// <paramref name="bis"/> (alle drei Anteile + <see cref="Nachtlicht.Marke"/>). Der Shader zieht sie ab; am Tag
    /// sieht die Flaeche aus wie vorher, nachts mischt er sie zum Lampenlicht. Ecken mit Schnee bleiben ohne Marke.
    /// </summary>
    public void Leuchten(int von, int bis)
    {
        for (var e = von; e < bis; e++)
        {
            var i = (e * Zahlen) + 6;
            if (_ecken[i] >= Jahreszeitfarben.SchneeMarke - 0.5f)
            {
                continue;   // Schnee auf dem Fensterbrett leuchtet nicht
            }
            _ecken[i] += Nachtlicht.Marke;
            _ecken[i + 1] += Nachtlicht.Marke;
            _ecken[i + 2] += Nachtlicht.Marke;
        }
    }

    /// <summary>Ort der Ecke Nummer <paramref name="e"/>.</summary>
    public Vector3 Ort(int e) => new(_ecken[e * Zahlen], _ecken[(e * Zahlen) + 1], _ecken[(e * Zahlen) + 2]);

    /// <summary>Normale der Ecke Nummer <paramref name="e"/>.</summary>
    /// <summary>
    /// Welle D1 (Teil A Sichtkegel): Kugel um alle Ecken (Mitte des umschliessenden Kastens, halbe Diagonale); leeres Netz =
    /// Halbmesser -1 (nie im Bild).
    /// </summary>
    public (Vector3 Mitte, float Halb) Kugel()
    {
        if (AnzahlEcken == 0)
        {
            return (Vector3.Zero, -1f);
        }
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var e = 0; e < AnzahlEcken; e++)
        {
            var p = new Vector3(_ecken[e * Zahlen], _ecken[(e * Zahlen) + 1], _ecken[(e * Zahlen) + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return ((min + max) / 2, (max - min).Length() / 2);
    }

    public Vector3 Normale(int e) => new(_ecken[(e * Zahlen) + 3], _ecken[(e * Zahlen) + 4], _ecken[(e * Zahlen) + 5]);

    /// <summary>Farbe der Ecke Nummer <paramref name="e"/> (mit Marken).</summary>
    public Vector3 FarbeAn(int e) => new(_ecken[(e * Zahlen) + 6], _ecken[(e * Zahlen) + 7], _ecken[(e * Zahlen) + 8]);
}
