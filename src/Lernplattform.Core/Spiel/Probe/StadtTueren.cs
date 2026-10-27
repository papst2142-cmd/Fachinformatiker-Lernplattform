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
    public static void Tuer(List<Teil> teile, float x, float z, Vector2 n, float hoehe)
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
        Q(-(b / 2) - 0.05f, (b / 2) + 0.05f, 0f, h + 0.05f, -0.02f, 0.03f, Rahmen);
        Q(-(b / 2) + 0.03f, -0.015f, 0.02f, h - 0.03f, 0.03f, 0.045f, Glas);
        Q(0.015f, (b / 2) - 0.03f, 0.02f, h - 0.03f, 0.03f, 0.045f, Glas);
        Q(-(b / 2) + 0.06f, -0.04f, h * 0.55f, h - 0.06f, 0.045f, 0.05f, GlasHell);
        Q(0.04f, (b / 2) - 0.06f, h * 0.55f, h - 0.06f, 0.045f, 0.05f, GlasHell);
        Q(-(b / 2) - 0.08f, (b / 2) + 0.08f, 0f, 0.035f, 0.0f, 0.16f, Stufe);
        Q(-(b / 2) - 0.12f, (b / 2) + 0.12f, h + 0.05f, h + 0.1f, 0f, 0.3f, Vordach);
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
