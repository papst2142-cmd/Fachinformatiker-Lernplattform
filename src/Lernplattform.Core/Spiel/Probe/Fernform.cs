using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle D1 „Dreiecke sparen“ (Weisung P12-143 mit P12-118 Punkt 2, Nico 2a; Teile B und C): vereinfachte Form eines
/// CC0-Modells fuer die Ferne. Die Ecken werden in ein Gitter von 1/<see cref="Teile"/> der groessten Seite des Modells
/// zusammengelegt (Plan P12-79 Teil D: „kaum Unterschied“), ohne Zufall und in fester Reihenfolge, also auf jedem Geraet
/// gleich:
/// <list type="bullet">
/// <item>Jede Gitterzelle bekommt eine Ecke: den Mittelwert ihrer Ecken; liegt eine Ecke der Zelle auf dem Rand des Modells
/// (kleinstes oder groesstes x, y, z), bleibt die Ecke auf diesem Rand (der Umriss und der Fuss auf dem Boden bleiben).</item>
/// <item>Ein Dreieck, dessen Ecken in weniger als drei Zellen liegen, faellt weg; von Dreiecken mit denselben drei Zellen
/// und derselben groben Farbe (je Kanal 8 Stufen) bleibt das erste.</item>
/// <item>Farbe des Dreiecks und Farben der Ecken bleiben die des uebrig gebliebenen Dreiecks; der Name bleibt (Faerbung
/// nach Jahreszeit, Licht und Glas erkennen dieselben Flaechen).</item>
/// </list>
/// Gezeichnet wird die Fernform erst ab dem Kameraabstand, ab dem auch Autos und Laternen ihre Fernform zeigen
/// (<c>StadtFlaeche.AutoFern</c>); nah bleiben die Originale.
/// </summary>
public static class Fernform
{
    /// <summary>Gitter: 1/32 der groessten Seite des Modells (Nico 2a, Plan P12-79 Teil D Massnahme B).</summary>
    public const int Teile = 32;

    /// <summary>Modelle mit hoechstens so vielen Dreiecken bleiben, wie sie sind (dort gibt es nichts zu sparen).</summary>
    public const int Mindestens = 12;

    private static readonly ConditionalWeakTable<GrafikModell, GrafikModell> Vorrat = new();

    /// <summary>Die Fernform von <paramref name="modell"/> (einmal je Modell gebaut und behalten).</summary>
    public static GrafikModell Von(GrafikModell modell) => Vorrat.GetValue(modell, Bauen);

    /// <summary>Die Fernform neu bauen (ohne Vorrat; Tests).</summary>
    public static GrafikModell Bauen(GrafikModell m)
    {
        var n = m.Dreiecke;
        var groesse = MathF.Max(m.Max.X - m.Min.X, MathF.Max(m.Max.Y - m.Min.Y, m.Max.Z - m.Min.Z));
        if (n <= Mindestens || !(groesse > 0))
        {
            return m;
        }
        var zelle = groesse / Teile;
        var e = m.Ecken;
        var zellen = new int[n * 3];
        var summe = new Dictionary<int, (Vector3 Summe, int Zahl, int Rand)>();
        for (var k = 0; k < n * 3; k++)
        {
            var p = new Vector3(e[k * 3], e[(k * 3) + 1], e[(k * 3) + 2]);
            var z = Zelle(p, m.Min, zelle);
            zellen[k] = z;
            var (s, zahl, rand) = summe.GetValueOrDefault(z);
            summe[z] = (s + p, zahl + 1, rand | Rand(p, m.Min, m.Max));
        }
        var ecke = new Dictionary<int, Vector3>(summe.Count);
        foreach (var (z, (s, zahl, rand)) in summe)
        {
            var p = s / zahl;
            p.X = (rand & 1) != 0 ? m.Min.X : (rand & 2) != 0 ? m.Max.X : p.X;
            p.Y = (rand & 4) != 0 ? m.Min.Y : (rand & 8) != 0 ? m.Max.Y : p.Y;
            p.Z = (rand & 16) != 0 ? m.Min.Z : (rand & 32) != 0 ? m.Max.Z : p.Z;
            ecke[z] = p;
        }
        var gesehen = new HashSet<long>();
        var ecken = new List<float>(n * 9);
        var farben = new List<byte>(n * 3);
        var eckFarben = m.EckFarben is null ? null : new List<byte>(n * 9);
        for (var d = 0; d < n; d++)
        {
            int a = zellen[d * 3], b = zellen[(d * 3) + 1], c = zellen[(d * 3) + 2];
            if (a == b || b == c || a == c)
            {
                continue;
            }
            // Schluessel: die drei Zellen sortiert (je 15 Bit) und die grobe Farbe (9 Bit)
            Span<int> s = [a, b, c];
            s.Sort();
            var farbe = ((m.Farben[d * 3] >> 5) << 6) | ((m.Farben[(d * 3) + 1] >> 5) << 3) | (m.Farben[(d * 3) + 2] >> 5);
            var schluessel = ((long)s[0] << 39) | ((long)s[1] << 24) | ((long)s[2] << 9) | (long)farbe;
            if (!gesehen.Add(schluessel))
            {
                continue;
            }
            foreach (var z in (ReadOnlySpan<int>)[a, b, c])
            {
                var p = ecke[z];
                ecken.Add(p.X);
                ecken.Add(p.Y);
                ecken.Add(p.Z);
            }
            farben.Add(m.Farben[d * 3]);
            farben.Add(m.Farben[(d * 3) + 1]);
            farben.Add(m.Farben[(d * 3) + 2]);
            if (eckFarben is not null)
            {
                for (var i = 0; i < 9; i++)
                {
                    eckFarben.Add(m.EckFarben![(d * 9) + i]);
                }
            }
        }
        return new GrafikModell(m.Name, [.. ecken], [.. farben], eckFarben is null ? null : [.. eckFarben]);
    }

    /// <summary>Nummer der Gitterzelle (je Achse 0..Teile-1, 5 Bit).</summary>
    private static int Zelle(Vector3 p, Vector3 min, float zelle)
    {
        static int Achse(float wert, float von, float zelle) => Math.Clamp((int)MathF.Floor((wert - von) / zelle), 0, Teile - 1);
        return (Achse(p.X, min.X, zelle) << 10) | (Achse(p.Y, min.Y, zelle) << 5) | Achse(p.Z, min.Z, zelle);
    }

    /// <summary>Liegt die Ecke auf dem Rand des Modells? Bit 0/1 x klein/gross, 2/3 y, 4/5 z.</summary>
    private static int Rand(Vector3 p, Vector3 min, Vector3 max) =>
        (p.X == min.X ? 1 : 0) | (p.X == max.X ? 2 : 0) | (p.Y == min.Y ? 4 : 0) | (p.Y == max.Y ? 8 : 0)
        | (p.Z == min.Z ? 16 : 0) | (p.Z == max.Z ? 32 : 0);
}
