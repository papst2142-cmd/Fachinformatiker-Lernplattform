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
