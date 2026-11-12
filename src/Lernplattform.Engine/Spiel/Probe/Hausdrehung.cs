using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle E (Weisungen P12-63, P12-67 Frage 1 = a): Haeuser in 90-Grad-Schritten gedreht. Gemeinsamer Teil von Spiel
/// (<see cref="StadtSzene"/>) und Stadtbau-Werkzeug.
/// <list type="bullet">
/// <item>Haus ohne Ort (Gebaeude der Landschaft): Feld <see cref="Feld"/> am Eintrag, 1 bis 3 Vierteldrehungen.</item>
/// <item>Haus mit Ort: <c>stadtdaten.darstellung.</c><see cref="Abschnitt"/> = {Ort-Kennung: 1 bis 3}. Die Orte selbst
/// (orte.json, neue_orte) bekommen kein neues Feld, ihre Ladepruefung und ihre Felder bleiben unveraendert.</item>
/// <item>Gedreht wird im Uhrzeigersinn von oben (Front Sued, dann West, Nord, Ost) um die Mitte aller Bloecke des Modells
/// (<see cref="Mitte"/>). Eine Vierteldrehung bildet (x, y) auf (m.x - (y - m.y), m.y + (x - m.x)) ab; die Grundflaeche bleibt
/// ein achsenparalleles Rechteck (Breite und Tiefe getauscht).</item>
/// <item>Ohne Feld (Fassung 0) ist nichts gedreht: das Spiel baut genau wie vorher (Szene, Fingerabdruecke bytegleich).</item>
/// </list>
/// Tuer, Fussweg und Schild stehen in den Daten an ihrer echten Stelle (das Werkzeug dreht sie beim Drehen mit); das Spiel
/// findet die Tuer wie bisher auf der naechsten Wand.
/// </summary>
public static class Hausdrehung
{
    /// <summary>Feld der Drehung an einem Gebaeude der Landschaft.</summary>
    public const string Feld = "dreh";

    /// <summary>Abschnitt in stadtdaten.darstellung mit der Drehung je Ort.</summary>
    public const string Abschnitt = "hausdreh";

    /// <summary>Vierteldrehungen eines Gebaeudes der Landschaft (0 bis 3; ein ungueltiger Wert zaehlt 0, die Pruefung meldet ihn).</summary>
    public static int Von(PyObjekt gebaeude) => Gueltig(Get(gebaeude, Feld));

    /// <summary>Vierteldrehungen eines Orts aus stadtdaten.darstellung.hausdreh (0 = nicht gedreht).</summary>
    public static int VonOrt(PyObjekt? stadtdaten, string id) =>
        stadtdaten is not null && Get(stadtdaten, "darstellung") is PyObjekt d && Get(d, Abschnitt) is PyObjekt h ? Gueltig(Get(h, id)) : 0;

    /// <summary>Ist der Wert eine erlaubte Drehung (ganze Zahl 0 bis 3)?</summary>
    public static bool Erlaubt(object? wert) => wert is long l && l is >= 0 and <= 3;

    private static int Gueltig(object? wert) => Erlaubt(wert) ? (int)(long)wert! : 0;

    /// <summary>Mitte aller Bloecke eines Modells (relativ zu x, y des Hauses); ohne Block die Mitte von 1 × 1.</summary>
    public static Vector2 Mitte(object? modellTeile)
    {
        var bloecke = (modellTeile as List<object?> ?? []).OfType<PyObjekt>().Where(t => Py.Gleich(t["art"], "block")).ToList();
        if (bloecke.Count == 0)
        {
            return new Vector2(0.5f, 0.5f);
        }
        var x1 = bloecke.Min(b => Py.F(b["x"]));
        var y1 = bloecke.Min(b => Py.F(b["y"]));
        var x2 = bloecke.Max(b => Py.F(b["x"]) + Py.F(b["w"]));
        var y2 = bloecke.Max(b => Py.F(b["y"]) + Py.F(b["t"]));
        return new Vector2((float)((x1 + x2) / 2), (float)((y1 + y2) / 2));
    }

    /// <summary>Punkt um <paramref name="mitte"/> um <paramref name="dreh"/> Vierteldrehungen im Uhrzeigersinn drehen (genau, ohne Winkelfunktion).</summary>
    public static Vector2 Drehen(Vector2 p, Vector2 mitte, int dreh)
    {
        var d = p - mitte;
        for (var i = 0; i < ((dreh % 4) + 4) % 4; i++)
        {
            d = new Vector2(-d.Y, d.X);
        }
        return mitte + d;
    }

    /// <summary>Punkt (double) drehen wie <see cref="Drehen(Vector2, Vector2, int)"/>.</summary>
    public static (double X, double Y) Drehen(double x, double y, double mx, double my, int dreh)
    {
        double dx = x - mx, dy = y - my;
        for (var i = 0; i < ((dreh % 4) + 4) % 4; i++)
        {
            (dx, dy) = (-dy, dx);
        }
        return (mx + dx, my + dy);
    }

    /// <summary>Lage (Modell → Welt) um die Hochachse durch <paramref name="mitte"/> gedreht (genaue Matrix, nur 0/±1).</summary>
    public static Matrix4x4 Matrix(Vector2 mitte, int dreh)
    {
        var (c, s) = (((dreh % 4) + 4) % 4) switch { 1 => (0f, 1f), 2 => (-1f, 0f), 3 => (0f, -1f), _ => (1f, 0f) };
        // Zeilenvektor: x' = c·x - s·z, z' = s·x + c·z (eine Vierteldrehung: x' = -z, z' = x)
        var r = new Matrix4x4(c, 0, s, 0, 0, 1, 0, 0, -s, 0, c, 0, 0, 0, 0, 1);
        return Matrix4x4.CreateTranslation(-mitte.X, 0, -mitte.Y) * r * Matrix4x4.CreateTranslation(mitte.X, 0, mitte.Y);
    }

    // ---- Welle A 0.70.0 (Sammelliste 314, Weisung P12-212 Punkt 2 = a): feine Drehung in 15-Grad-Schritten --------------

    /// <summary>Feld der feinen Drehung an einem Gebaeude der Landschaft (Grad nach den Vierteln, 15 bis 75).</summary>
    public const string GradFeld = "grad";

    /// <summary>Abschnitt in stadtdaten.darstellung mit der feinen Drehung je Ort (Grad nach den Vierteln, 15 bis 75).</summary>
    public const string GradAbschnitt = "hausgrad";

    /// <summary>Schritt der feinen Drehung eines Hauses in Grad.</summary>
    public const int GradSchritt = 15;

    /// <summary>Ist der Wert eine erlaubte feine Drehung (ganze Zahl 15, 30, 45, 60 oder 75)?</summary>
    public static bool GradErlaubt(object? wert) => wert is long l && l is > 0 and < 90 && l % GradSchritt == 0;

    private static int GradGueltig(object? wert) => GradErlaubt(wert) ? (int)(long)wert! : 0;

    /// <summary>Feine Drehung eines Gebaeudes der Landschaft (0 = keine; ein ungueltiger Wert zaehlt 0, die Pruefung meldet ihn).</summary>
    public static int GradVon(PyObjekt gebaeude) => GradGueltig(Get(gebaeude, GradFeld));

    /// <summary>Feine Drehung eines Orts aus stadtdaten.darstellung.hausgrad (0 = keine).</summary>
    public static int GradVonOrt(PyObjekt? stadtdaten, string id) =>
        stadtdaten is not null && Get(stadtdaten, "darstellung") is PyObjekt d && Get(d, GradAbschnitt) is PyObjekt h ? GradGueltig(Get(h, id)) : 0;

    /// <summary>Ganzer Winkel in Grad (0 bis 345) aus Vierteln und feiner Drehung.</summary>
    public static int Winkel(int dreh, int grad) => ((((dreh * 90) + grad) % 360) + 360) % 360;

    /// <summary>Winkel (Grad, beliebig) in Viertel und feine Drehung zerlegen (auf 15 Grad gerundet).</summary>
    public static (int Dreh, int Grad) Zerlegen(int winkel)
    {
        var w = (((int)Math.Round(winkel / (double)GradSchritt, MidpointRounding.AwayFromZero) * GradSchritt % 360) + 360) % 360;
        return (w / 90, w % 90);
    }

    /// <summary>
    /// Feine Drehung eines Hauses um die Hochachse durch <see cref="Mitte"/> (nach den Vierteln). Das Haus wird im
    /// viertelgedrehten Rahmen gebaut (Tuer, Fenster, Kasten wie bisher) und danach als Ganzes mit dieser Lage gedreht.
    /// </summary>
    public readonly record struct Feinlage(Vector2 Mitte, int Grad)
    {
        /// <summary>Ist das Haus fein gedreht?</summary>
        public bool Aktiv => Grad != 0;

        private (double Sin, double Cos) SinCos => Math.SinCos(Grad * Math.PI / 180);

        /// <summary>Punkt aus dem Rahmen des Hauses in die Welt.</summary>
        public Vector2 Punkt(Vector2 p)
        {
            if (!Aktiv)
            {
                return p;
            }
            var (sin, cos) = SinCos;
            double dx = p.X - Mitte.X, dy = p.Y - Mitte.Y;
            return new Vector2((float)(Mitte.X + (dx * cos) - (dy * sin)), (float)(Mitte.Y + (dx * sin) + (dy * cos)));
        }

        /// <summary>Punkt aus der Welt in den Rahmen des Hauses.</summary>
        public Vector2 Zurueck(Vector2 p) => Aktiv ? (this with { Grad = -Grad }).Punkt(p) : p;

        /// <summary>Richtung aus dem Rahmen des Hauses in die Welt.</summary>
        public Vector2 Richtung(Vector2 v) => Punkt(Mitte + v) - Mitte;

        /// <summary>Richtung aus der Welt in den Rahmen des Hauses.</summary>
        public Vector2 RichtungZurueck(Vector2 v) => Zurueck(Mitte + v) - Mitte;

        /// <summary>Lage (Modell im Rahmen des Hauses → Welt) als Matrix (Zeilenvektor wie <see cref="Hausdrehung.Matrix"/>).</summary>
        public Matrix4x4 Matrix
        {
            get
            {
                var (sin, cos) = SinCos;
                var (c, s) = ((float)cos, (float)sin);
                var r = new Matrix4x4(c, 0, s, 0, 0, 1, 0, 0, -s, 0, c, 0, 0, 0, 0, 1);
                return Matrix4x4.CreateTranslation(-Mitte.X, 0, -Mitte.Y) * r * Matrix4x4.CreateTranslation(Mitte.X, 0, Mitte.Y);
            }
        }

        /// <summary>Teil der Szene in die Welt: Band an beiden Enden gedreht, Quader und Satteldach behalten ihre Ecken und zeichnen sich gedreht (<see cref="Teil.Dreh"/>).</summary>
        public Teil Teil(Teil t)
        {
            if (!Aktiv)
            {
                return t;
            }
            if (t.Art == TeilArt.Band)
            {
                var (a, b) = (Punkt(new Vector2(t.X1, t.Z1)), Punkt(new Vector2(t.X2, t.Z2)));
                return t with { X1 = a.X, Z1 = a.Y, X2 = b.X, Z2 = b.Y };
            }
            return t with { Dreh = new Vector3(Mitte.X, Mitte.Y, Grad) };
        }

        /// <summary>Die vier Ecken eines Rechtecks (im Rahmen des Hauses) in der Welt, im Umlauf.</summary>
        public Vector2[] Ecken(double x1, double z1, double x2, double z2) =>
            [Punkt(new((float)x1, (float)z1)), Punkt(new((float)x2, (float)z1)), Punkt(new((float)x2, (float)z2)), Punkt(new((float)x1, (float)z2))];

        /// <summary>Achsenparalleler Kasten um einen gedrehten Kasten (Hoehe bleibt).</summary>
        public (Vector3 Min, Vector3 Max) Kasten(Vector3 min, Vector3 max)
        {
            var e = Ecken(min.X, min.Z, max.X, max.Z);
            return (new Vector3(e.Min(p => p.X), min.Y, e.Min(p => p.Y)), new Vector3(e.Max(p => p.X), max.Y, e.Max(p => p.Y)));
        }

        /// <summary>Liegt der Weltpunkt (mit Rand) im Kasten, der im Rahmen des Hauses von min bis max reicht?</summary>
        public bool Drin(Vector3 min, Vector3 max, Vector2 welt, float rand = 0f)
        {
            var p = Zurueck(welt);
            return p.X >= min.X - rand && p.X <= max.X + rand && p.Y >= min.Z - rand && p.Y <= max.Z + rand;
        }
    }

    /// <summary>
    /// Welle A 0.70.0: wie tief zwei konvexe Vielecke (Grundflaechen) ineinander liegen: die kleinste Ueberdeckung entlang aller
    /// Kantenrichtungen (Trennachsen). 0 oder weniger = sie beruehren sich hoechstens. Fuer zwei achsenparallele Rechtecke ist das
    /// das Kleinere der Ueberdeckungen in x und in z.
    /// </summary>
    public static float Ueberlappung(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
    {
        var tief = float.MaxValue;
        foreach (var vieleck in (IReadOnlyList<Vector2>[])[a, b])
        {
            for (var i = 0; i < vieleck.Count; i++)
            {
                var kante = vieleck[(i + 1) % vieleck.Count] - vieleck[i];
                if (kante.LengthSquared() < 1e-12f)
                {
                    continue;
                }
                var achse = Vector2.Normalize(new Vector2(-kante.Y, kante.X));
                float aMin = a.Min(p => Vector2.Dot(p, achse)), aMax = a.Max(p => Vector2.Dot(p, achse));
                float bMin = b.Min(p => Vector2.Dot(p, achse)), bMax = b.Max(p => Vector2.Dot(p, achse));
                tief = MathF.Min(tief, MathF.Min(aMax, bMax) - MathF.Max(aMin, bMin));
            }
        }
        return tief;
    }

    /// <summary>Ecken eines achsenparallelen Rechtecks im Umlauf.</summary>
    public static Vector2[] Rechteck(float x1, float z1, float x2, float z2) => [new(x1, z1), new(x2, z1), new(x2, z2), new(x1, z2)];

    /// <summary>Breite der achsenparallelen Streifen, mit denen ein gedrehtes Rechteck das freie Gehen sperrt.</summary>
    public const float StreifenBreite = 0.05f;

    /// <summary>
    /// Welle A 0.70.0: ein gedrehtes Rechteck (vier Ecken, konvex) als schmale achsenparallele Streifen fuer Laufraster und
    /// Hindernisse. Jeder Streifen deckt das Vieleck in seinem Abschnitt ganz ab und ragt hoechstens um eine Streifenbreite
    /// darueber hinaus (Abschnitte quer zur x-Achse).
    /// </summary>
    public static List<(double, double, double, double)> Streifen(IReadOnlyList<Vector2> ecken, float breite = StreifenBreite)
    {
        var p = ecken;
        var (a0, a1) = (p.Min(v => v.X), p.Max(v => v.X));
        var anzahl = Math.Max(1, (int)MathF.Ceiling((a1 - a0) / breite));
        var schritt = (a1 - a0) / anzahl;
        var ergebnis = new List<(double, double, double, double)>(anzahl);
        for (var i = 0; i < anzahl; i++)
        {
            var (s0, s1) = (a0 + (i * schritt), i == anzahl - 1 ? a1 : a0 + ((i + 1) * schritt));
            float lo = float.MaxValue, hi = float.MinValue;
            for (var k = 0; k < p.Count; k++)
            {
                var (u, v) = (p[k], p[(k + 1) % p.Count]);
                // Kante auf den Abschnitt [s0, s1] zuschneiden
                var (ux, vx) = (u.X, v.X);
                var (kmin, kmax) = (MathF.Min(ux, vx), MathF.Max(ux, vx));
                if (kmax < s0 || kmin > s1)
                {
                    continue;
                }
                if (vx - ux is > -1e-9f and < 1e-9f)
                {
                    // senkrechte Kante im Abschnitt: ganz
                    (lo, hi) = (MathF.Min(lo, MathF.Min(u.Y, v.Y)), MathF.Max(hi, MathF.Max(u.Y, v.Y)));
                    continue;
                }
                foreach (var x in (float[])[MathF.Max(kmin, s0), MathF.Min(kmax, s1)])
                {
                    var y = u.Y + ((v.Y - u.Y) * (x - ux) / (vx - ux));
                    (lo, hi) = (MathF.Min(lo, y), MathF.Max(hi, y));
                }
            }
            if (lo > hi)
            {
                continue;
            }
            ergebnis.Add((s0, lo, s1, hi));
        }
        return ergebnis;
    }

    /// <summary>Teil der Szene drehen: Quader und Satteldach als neuer achsenparalleler Kasten, Band an beiden Enden.</summary>
    public static Teil Drehen(Teil t, Vector2 mitte, int dreh)
    {
        var a = Drehen(new Vector2(t.X1, t.Z1), mitte, dreh);
        var b = Drehen(new Vector2(t.X2, t.Z2), mitte, dreh);
        return t.Art == TeilArt.Band
            ? t with { X1 = a.X, Z1 = a.Y, X2 = b.X, Z2 = b.Y }
            : t with { X1 = MathF.Min(a.X, b.X), Z1 = MathF.Min(a.Y, b.Y), X2 = MathF.Max(a.X, b.X), Z2 = MathF.Max(a.Y, b.Y) };
    }
}
