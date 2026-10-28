using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 8 W10 (Sammelliste 150, Nico 3b): Licht der Stadt nach der Tagesuhr der Stadt (Spielzeit, nie die echte
/// Uhr) und der Jahreszeit (W9). Nur Darstellung: Helligkeit, Farbton, Himmel, Anteil des Nachtlichts (Fenster,
/// Laternen, <see cref="Nachtlicht"/>). Keine Dreiecke, keine Aufrufe dazu. Am Tag genau wie vorher (Hell 1, Ton 1).
/// Morgen 07:00 bis 08:00 (Winter 08:30), Daemmerung ab <see cref="DaemmerungAb"/> bis 20:00, Nacht ab 20:00 (die
/// Uhr laeuft bis 23:00 weiter, <see cref="Stadt.UhrSteht"/>). Innenraeume bleiben immer hell.
/// </summary>
public static class Tageslicht
{
    /// <summary>Licht zu einer Uhrzeit: Hell = Faktor auf alle Farben, Ton = Farbfilter, Nacht 0..1 (Fenster leuchten).</summary>
    public readonly record struct Licht(float Hell, Vector3 Ton, float Nacht, Vector3 Himmel);

    /// <summary>Feste Uhr nur fuer Start-Test und Fotos (Ansichten um 07:00, 12:00, 19:00, 20:00); null = Uhr der Stadt.</summary>
    public static long? FesteUhr { get; set; }

    /// <summary>So lange blendet das Licht ueber, wenn die Uhr springt (ein Weg schreibt seine Minuten beim Losgehen).</summary>
    public static readonly TimeSpan Ueberblenden = TimeSpan.FromSeconds(2);

    /// <summary>Ab hier Nacht (20:00).</summary>
    public const long NachtAb = 1200;

    private static readonly Vector3 Neutral = new(1f, 1f, 1f);
    private static readonly Vector3 Morgen = new(1f, 0.92f, 0.82f);
    private static readonly Vector3 Abendrot = new(1f, 0.8f, 0.66f);
    private static readonly Vector3 Mond = new(0.62f, 0.7f, 1f);
    private static readonly Vector3 HimmelTag = new(0.73f, 0.85f, 0.95f);
    private static readonly Vector3 HimmelMorgen = new(0.96f, 0.82f, 0.68f);
    private static readonly Vector3 HimmelAbend = new(0.93f, 0.55f, 0.42f);
    private static readonly Vector3 HimmelNacht = new(0.06f, 0.08f, 0.2f);

    /// <summary>Licht am Tag (und immer in den Innenraeumen).</summary>
    public static readonly Licht Tag = new(1f, Neutral, 0f, HimmelTag);

    /// <summary>Beginn der Daemmerung je Jahreszeit (Spielminuten): Winter frueh, Sommer spaet.</summary>
    public static long DaemmerungAb(Jahreszeit j) => j switch
    {
        Jahreszeit.Winter => 960,     // 16:00
        Jahreszeit.Sommer => 1110,    // 18:30
        _ => 1050,                    // 17:30
    };

    /// <summary>Ende des Morgens (ab hier voller Tag): Winter 08:30, sonst 08:00.</summary>
    public static long MorgenBis(Jahreszeit j) => j == Jahreszeit.Winter ? 510 : 480;

    public static Licht Fuer(long uhr, Jahreszeit j)
    {
        const long nacht = NachtAb;
        var dAb = DaemmerungAb(j);
        if (uhr >= nacht)
        {
            return new(0.42f, Mond, 1f, HimmelNacht);
        }
        if (uhr >= dAb)
        {
            // Daemmerung: erst Abendrot, dann blau in die Nacht
            var t = (uhr - dAb) / (float)(nacht - dAb);
            var hell = 1f - (0.58f * t);
            var ton = t < 0.5f ? Vector3.Lerp(Neutral, Abendrot, t * 2) : Vector3.Lerp(Abendrot, Mond, (t - 0.5f) * 2);
            var himmel = t < 0.5f ? Vector3.Lerp(HimmelTag, HimmelAbend, t * 2) : Vector3.Lerp(HimmelAbend, HimmelNacht, (t - 0.5f) * 2);
            return new(hell, ton, Math.Clamp((t - 0.45f) / 0.55f, 0f, 1f), himmel);
        }
        var mBis = MorgenBis(j);
        if (uhr < mBis)
        {
            var t = Math.Clamp((uhr - 420) / (float)(mBis - 420), 0f, 1f);
            var start = j == Jahreszeit.Winter ? 0.6f : 0.8f;
            return new(start + ((1f - start) * t), Vector3.Lerp(Morgen, Neutral, t), j == Jahreszeit.Winter ? (1f - t) * 0.6f : 0f, Vector3.Lerp(HimmelMorgen, HimmelTag, t));
        }
        return Tag;
    }

    /// <summary>Zwischen zwei Lichtern ueberblenden (<paramref name="t"/> 0 = a, 1 = b).</summary>
    public static Licht Mischen(Licht a, Licht b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        if (t >= 1f)
        {
            return b;
        }
        return new(a.Hell + ((b.Hell - a.Hell) * t), Vector3.Lerp(a.Ton, b.Ton, t), a.Nacht + ((b.Nacht - a.Nacht) * t), Vector3.Lerp(a.Himmel, b.Himmel, t));
    }
}
