using System.Numerics;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268, Nico Antwort 5, P12-258): Karte der Stadt im Spiel-Handy, nur aus den Daten der Szene
/// (Strassen, Gleise, Wasser, Parks, Haeuser, Orte, Haltestellen). Jeder Ort bekommt eine Nummer in der Reihenfolge von
/// <see cref="StadtSzene.Orte"/> (wie die Liste unter der Karte); Antippen einer Nummer oder eines Orts-Hauses waehlt den Ort,
/// die Figur geht sofort los. Nur Darstellung und Bedienung: nichts davon geht in den Spielstand.
/// </summary>
public sealed class StadtKarte
{
    /// <summary>Ein Ort auf der Karte: Nummer (ab 1), Mitte und Kasten am Boden (x, z), ob dort ein offener Auftrag wartet.</summary>
    public sealed record Ort(int Nr, string Id, string Name, Vector2 Mitte, Vector2 Min, Vector2 Max, bool Aufgabe);

    /// <summary>Halbmesser der Nummernmarke in Bildpunkten (bei Schriftfaktor 1).</summary>
    public const float MarkeRadius = 11f;

    /// <summary>Antippen trifft eine Marke bis zu diesem Abstand in Bildpunkten (Finger).</summary>
    public const float TreffRadius = 22f;

    public StadtKarte(StadtSzene szene, IReadOnlySet<string> aufgaben)
    {
        Szene = szene;
        Orte = szene.Orte.Select((o, i) => new Ort(i + 1, o.Id, o.Name,
            new Vector2((o.KastenMin.X + o.KastenMax.X) / 2, (o.KastenMin.Z + o.KastenMax.Z) / 2),
            new Vector2(o.KastenMin.X, o.KastenMin.Z), new Vector2(o.KastenMax.X, o.KastenMax.Z), aufgaben.Contains(o.Id))).ToList();
    }

    public StadtSzene Szene { get; }

    public IReadOnlyList<Ort> Orte { get; }

    /// <summary>Massstab (Bildpunkte je Feld), damit die ganze Stadt in <paramref name="breite"/> x <paramref name="hoehe"/> passt.</summary>
    public float Massstab(float breite, float hoehe) =>
        MathF.Max(0.01f, MathF.Min(breite / MathF.Max(1, Szene.Breite), hoehe / MathF.Max(1, Szene.Tiefe)));

    /// <summary>Bildpunkt eines Punkts am Boden (x, z): Norden oben wie die Draufsicht des Werkzeugs, Ursprung oben links.</summary>
    public static Vector2 Bild(Vector2 welt, float massstab) => welt * massstab;

    /// <summary>
    /// Welcher Ort liegt unter dem Bildpunkt? Zuerst die naechste Nummernmarke bis <see cref="TreffRadius"/> (× Schriftfaktor),
    /// sonst das Haus des Orts unter dem Punkt; sonst null (freie Stelle: nichts).
    /// </summary>
    public Ort? Treffer(Vector2 bild, float massstab, float schrift = 1f)
    {
        var naechster = Orte.Select(o => (o, d: Vector2.Distance(Bild(o.Mitte, massstab), bild))).OrderBy(t => t.d).ThenBy(t => t.o.Nr).FirstOrDefault();
        if (naechster.o is not null && naechster.d <= TreffRadius * schrift)
        {
            return naechster.o;
        }
        var welt = bild / massstab;
        return Orte.Where(o => welt.X >= o.Min.X && welt.X <= o.Max.X && welt.Y >= o.Min.Y && welt.Y <= o.Max.Y)
            .OrderBy(o => Vector2.Distance(o.Mitte, welt)).FirstOrDefault();
    }

    /// <summary>Wo die Figur auf der Karte steht: draussen ihre Stelle, an oder in einem Ort dessen Stehplatz.</summary>
    public static Vector2 Figur(StadtSpiel spiel) =>
        !spiel.ImInnenraum && spiel.ImFreien ? new Vector2(spiel.FigurX, spiel.FigurZ)
        : spiel.Szene.Ort(spiel.Jetzt.Ort) is { } ort ? new Vector2(ort.StehX, ort.StehZ)
        : new Vector2(spiel.FigurX, spiel.FigurZ);
}
