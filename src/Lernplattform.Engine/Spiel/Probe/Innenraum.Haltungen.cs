using System.Numerics;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.93.0 (Weisung P12-303, Sammelliste 352): wo die Spielfigur drinnen ducken und kriechen darf. Die Haltung nimmt mehr
/// Platz als das Stehen (geduckt ragen die Arme nach vorn, kriechend liegt der Koerper hinter dem Kopf); sie gilt nur, wenn die
/// Figur dabei nichts beruehrt (Einrichtung, Fest-Deko, Personen; 0,02 Spiel wie beim Sitzen), ganz im Raum bleibt und nichts ueber
/// die Mitte einer Wand reicht (Regel 347). Nur Darstellung: kein Ereignis, keine Spielzeit.
/// </summary>
public sealed partial class Innenraum
{
    /// <summary>
    /// Quader um die Figur der Hoehe 1 in der Haltung (x halb, y von bis, z von bis; Blickrichtung +z), mit Reserve fuer Muetze und
    /// Spitzhut. Der Kopf steht wie im Stehen ueber der Stelle (Figur.Ducken, Figur.Kriechen); <c>SpielHaltungenTests</c> prueft, dass
    /// jede Ecke der Figur in ihnen liegt.
    /// </summary>
    public static IReadOnlyList<(float Halb, float Y1, float Y2, float Z1, float Z2)> KoerperQuader(Koerperhaltung haltung) => haltung switch
    {
        // gemessen (alle Kleider, mit Muetze und Spitzhut): geduckt bis 0,85 hoch und 0,29 nach vorn (Handtasche), kriechend 0,477 nach hinten
        Koerperhaltung.Ducken => [(0.19f, 0f, 0.87f, -0.215f, 0.30f)],
        Koerperhaltung.Kriechen => [(0.19f, 0f, 0.87f, -0.485f, 0.22f)],
        _ => [(0.19f, 0f, 1.08f, -0.215f, 0.22f)],
    };

    /// <summary>
    /// Welle A 0.93.0: Hoehe des Sprungs (Anteil der Figurhoehe) am hoechsten Punkt. Der Sprung ist nur Darstellung: die Figur
    /// bleibt auf ihrem Weg; ueber dem Kopf muss so viel frei sein (<see cref="SprungFrei"/>).
    /// </summary>
    public const float SprungHoch = 0.22f;

    /// <summary>Welle A 0.93.0: ist ueber der Figur an <paramref name="ort"/> Platz fuer den Sprung (der stehende Quader um <see cref="SprungHoch"/> angehoben beruehrt nichts und bleibt unter der Decke)?</summary>
    public bool SprungFrei(Vector2 ort, float blick)
    {
        var h = FigurHoehe;
        IReadOnlyList<(float Halb, float Y1, float Y2, float Z1, float Z2)> quader = [.. KoerperQuader(Koerperhaltung.Stehen).Select(q => (q.Halb, q.Y1 + SprungHoch, q.Y2 + SprungHoch, q.Z1, q.Z2))];
        if ((quader.Max(q => q.Y2) * h) + 0.02f > IchWandHoehe)
        {
            return false;
        }
        return !Hindernisse().Any(k => QuaderBeruehrt(new Vector3(ort.X, 0, ort.Y), blick, quader, h, k.Lo, k.Hi));
    }

    /// <summary>Welle A 0.93.0: Grundflaeche der Haltung (Anteile der Figurhoehe ab ihrer Stelle: nach hinten, nach vorn, zur Seite).</summary>
    public static (float Hinten, float Vorn, float Halb) KoerperGrundflaeche(Koerperhaltung haltung)
    {
        var q = KoerperQuader(haltung);
        return (-q.Min(k => k.Z1), q.Max(k => k.Z2), q.Max(k => k.Halb));
    }

    private List<(Vector3 Lo, Vector3 Hi)>? _hindernisse;

    /// <summary>Welle A 0.93.0: alle Stuecke des Raums als achsgleiche Kaesten (Modelle, eigene Quader der Einrichtung ohne begehbare Deko, Fest-Deko), ohne flache Teile am Boden.</summary>
    private List<(Vector3 Lo, Vector3 Hi)> Hindernisse()
    {
        if (_hindernisse is { } h)
        {
            return h;
        }
        h = [
            .. _stuecke.Select(s => Huelle(s.Name, s.Lage)).OfType<(Vector3, Vector3)>(),
            // begehbare Deko (Teppich, Markierung am Boden; Bild, Whiteboard an der Wand) steht der Figur auch im Stehen nicht im Weg
            .. _dekoUmrisse.Where(u => !ProbeWelt.BegehbareTypen.Contains(Py.Str(u.Deko["typ"])))
                .SelectMany(u => u.Kaesten.Where(k => k.Art == TeilArt.Quader).Select(Huelle)),
            .. FestKaesten.Where(k => k.Art == TeilArt.Quader).Select(Huelle),
            .. FestModelle.Select(m => Huelle(m.Name, m.Lage)).OfType<(Vector3, Vector3)>(),
        ];
        h.RemoveAll(k => k.Hi.Y <= 0.05f);
        _hindernisse = h;
        return h;
    }

    /// <summary>
    /// Welle A 0.93.0: darf die Figur an <paramref name="ort"/> mit Blick <paramref name="blick"/> (Bogenmass) die Haltung
    /// einnehmen? Ganz im Raum, nichts ueber die Mitte einer Wand, keine Beruehrung mit Einrichtung, Fest-Deko oder Personen.
    /// </summary>
    public bool KoerperFrei(Vector2 ort, float blick, Koerperhaltung haltung)
    {
        var f = new Vector2(MathF.Sin(blick), MathF.Cos(blick));
        var q = new Vector2(f.Y, -f.X);
        var (hinten, vorn, halb) = KoerperGrundflaeche(haltung);
        const float spiel = 0.02f;
        float h = FigurHoehe, v = (vorn * h) + spiel, r = (hinten * h) + spiel, s = (halb * h) + spiel;
        Vector2[] ecken = [ort + (f * v) + (q * s), ort + (f * v) - (q * s), ort - (f * r) - (q * s), ort - (f * r) + (q * s)];
        if (ecken.Any(e => e.X < 0 || e.Y < 0 || e.X > Breite || e.Y > Tiefe))
        {
            return false;
        }
        if (UeberWand(ort, f, (hinten, vorn, halb), h, ort))
        {
            return false;
        }
        var quader = KoerperQuader(haltung);
        foreach (var (lo, hi) in Hindernisse())
        {
            if (QuaderBeruehrt(new Vector3(ort.X, 0, ort.Y), blick, quader, h, lo, hi))
            {
                return false;
            }
        }
        foreach (var p in Personen)
        {
            var (px, pz) = Sitz(p) is { } sitz ? (sitz.X, sitz.Z) : (p.X, p.Z);
            if (QuaderBeruehrt(new Vector3(ort.X, 0, ort.Y), blick, quader, h, new Vector3(px - 0.3f, 0, pz - 0.3f), new Vector3(px + 0.3f, h, pz + 0.3f)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Welle A 0.93.0: ein Quader der Figur (Stelle <paramref name="ort"/>, 0,02 Spiel) schneidet den achsgleichen Kasten lo..hi.</summary>
    public static bool QuaderBeruehrt(Vector3 ort, float blick, IReadOnlyList<(float Halb, float Y1, float Y2, float Z1, float Z2)> quader, float hoehe, Vector3 lo, Vector3 hi)
    {
        const float spiel = 0.02f;
        var f = new Vector2(MathF.Sin(blick), MathF.Cos(blick));
        var q = new Vector2(f.Y, -f.X);
        var o = new Vector2(ort.X, ort.Z);
        foreach (var (halb, y1, y2, z1, z2) in quader)
        {
            if (ort.Y + (y1 * hoehe) - spiel >= hi.Y || ort.Y + (y2 * hoehe) + spiel <= lo.Y)
            {
                continue;
            }
            float vorn = (z2 * hoehe) + spiel, hinten = (z1 * hoehe) - spiel, seite = (halb * hoehe) + spiel;
            Vector2[] ecken = [o + (f * vorn) + (q * seite), o + (f * vorn) - (q * seite), o + (f * hinten) - (q * seite), o + (f * hinten) + (q * seite)];
            if (Schneidet(ecken, new Vector2(lo.X, lo.Z), new Vector2(hi.X, hi.Z)))
            {
                return true;
            }
        }
        return false;
    }
}
