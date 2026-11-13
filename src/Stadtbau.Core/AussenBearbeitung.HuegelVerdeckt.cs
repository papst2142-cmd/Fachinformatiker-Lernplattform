using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-242 Punkt 3, Sammelliste 336): Nachgestellt ging „Hügel setzen“ an jeder Stelle (am Rand und
/// nah an Strasse, Haus oder Gleis mit rotem Fund und Grund, bei 16 Huegeln grauer Knopf mit Grund). Stumm blieb nur ein
/// Fall: auf gemaltem Gelaende, das dort schon so hoch ist wie der neue Huegel (oder in einem groesseren Huegel), steht der
/// Huegel in den Daten, man sieht ihn aber nicht (die Hoehe ist das Groesste aus Huegeln und Gemaltem). Das Werkzeug sagt
/// das jetzt beim Setzen (<see cref="Texte.HuegelVerdeckt"/>) statt nur „Hügel gesetzt“.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>So viel muss ein Huegel irgendwo ueber das uebrige Gelaende hinausragen, damit man ihn sieht.</summary>
    public const float HuegelSichtbarAb = 0.05f;

    /// <summary>Gegenprobe (nur Tests, je Ablauf): wie bis S 0.51.0, kein Hinweis auf einen verdeckten Huegel.</summary>
    public static bool HuegelVerdecktAltFuerTests { get => HuegelVerdecktAlt.Value; set => HuegelVerdecktAlt.Value = value; }
    private static readonly AsyncLocal<bool> HuegelVerdecktAlt = new();

    /// <summary>
    /// Liegt der Huegel ganz unter dem uebrigen Gelaende (gemalte Hoehen und andere Huegel), so dass man ihn nicht sieht?
    /// Geprueft im Raster 0,25 ueber seinem Umriss.
    /// </summary>
    public static bool HuegelVerdeckt(PyObjekt aussen, AussenStelle s)
    {
        if (HuegelVerdecktAlt.Value || s.Art != AussenArt.Huegel || HuegelVon(aussen, s) is not { } h
            || Gelaende.Aus(Aussen.Stadtdaten(aussen)) is not { } g)
        {
            return false;
        }
        var umriss = h.Umriss();
        var (x1, z1) = (umriss.Min(p => p.X), umriss.Min(p => p.Y));
        var (x2, z2) = (umriss.Max(p => p.X), umriss.Max(p => p.Y));
        const float schritt = 0.25f;
        for (var x = x1; x <= x2; x += schritt)
        {
            for (var z = z1; z <= z2; z += schritt)
            {
                var p = new Vector2(x, z);
                var eigen = MathF.Min(h.HoeheAn(p), Gelaende.HoeheMax);
                if (eigen <= 0)
                {
                    continue;
                }
                var rest = g.Gemalt.Count > 0 ? g.GemaltAn(x, z) : 0f;
                foreach (var k in g.Huegel)
                {
                    if (k.Nr != h.Nr)
                    {
                        rest = MathF.Max(rest, k.HoeheAn(p));
                    }
                }
                if (eigen > MathF.Min(rest, Gelaende.HoeheMax) + HuegelSichtbarAb)
                {
                    return false;
                }
            }
        }
        return true;
    }
}
