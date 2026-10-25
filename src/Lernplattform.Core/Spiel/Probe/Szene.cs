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

    public int AnzahlEcken => _ecken.Count / Zahlen;

    public static Vector3 Farbe(string hex)
    {
        var h = hex.TrimStart('#');
        var wert = int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Vector3(((wert >> 16) & 255) / 255f, ((wert >> 8) & 255) / 255f, (wert & 255) / 255f);
    }

    public static Netz Aus(IEnumerable<Teil> teile)
    {
        var netz = new Netz();
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

    /// <summary>Ein CC0-Modell an seiner Lage einfuegen (flache Normale je Dreieck, Farbe des Dreiecks).</summary>
    public void Hinzu(GrafikModell modell, Matrix4x4 lage)
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
            var farbe = new Vector3(modell.Farben[d * 3], modell.Farben[(d * 3) + 1], modell.Farben[(d * 3) + 2]) / 255f;
            Dreieck(a, b, c, farbe);
        }
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
        var basis = (uint)AnzahlEcken;
        foreach (var p in (Vector3[])[a, b, c, d])
        {
            Ecke(p, n, farbe);
        }
        _indizes.AddRange([basis, basis + 1, basis + 2, basis, basis + 2, basis + 3]);
    }

    private void Dreieck(Vector3 a, Vector3 b, Vector3 c, Vector3 farbe)
    {
        var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        var basis = (uint)AnzahlEcken;
        Ecke(a, n, farbe);
        Ecke(b, n, farbe);
        Ecke(c, n, farbe);
        _indizes.AddRange([basis, basis + 1, basis + 2]);
    }

    private void Ecke(Vector3 p, Vector3 n, Vector3 farbe)
    {
        _ecken.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, farbe.X, farbe.Y, farbe.Z]);
    }
}
