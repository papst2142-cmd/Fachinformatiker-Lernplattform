using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.0.0 (Sammelliste 407, Nico: „im Buero der Chefin steht der Sessel verkehrt herum“). Nicos Ziel: jeder Sessel und
/// jeder Stuhl blickt richtig, Sessel in den Raum (nicht zur Wand), Stuehle zum Tisch. Feste Tabelle der Blickrichtung aller
/// Sessel in allen Innenraeumen; Stuehle muessen in die Richtung ihres Sitzplatzes blicken. Gegenprobe ueber
/// <c>Innenraum.SesselAltFuerTests</c> (wie bis A 0.99.0: jeder Sessel blickt nach Sueden).
/// </summary>
public sealed class SpielSesselTests
{
    /// <summary>Modelle, auf denen man in einem Sessel sitzt.</summary>
    private static readonly HashSet<string> Sessel = ["loungeChair", "kkArmchair"];

    /// <summary>Modelle von Stuehlen an Tischen.</summary>
    private static readonly HashSet<string> Stuehle = ["chairDesk", "chair", "chairCushion", "kkChairA"];

    /// <summary>Vorderseite eines Modells auf der Karte (das Modell blickt ungedreht nach +Z = Sueden) als n, s, o, w.</summary>
    internal static string Blick(ModellStueck m)
    {
        var v = Vector3.TransformNormal(Vector3.UnitZ, m.Lage);
        return MathF.Abs(v.X) > MathF.Abs(v.Z) ? (v.X > 0 ? "o" : "w") : (v.Z > 0 ? "s" : "n");
    }

    private static Vector2 Mitte(ModellStueck m)
    {
        var gm = SpielGrafikTests.Bibliothek.Value.Modell(m.Name)!;
        var p = Vector3.Transform((gm.Min + gm.Max) / 2, m.Lage);
        return new Vector2(p.X, p.Z);
    }

    private static Vector2 Richtung(string blick) => blick switch
    {
        "n" => new(0, -1),
        "s" => new(0, 1),
        "o" => new(1, 0),
        _ => new(-1, 0),
    };

    /// <summary>Alle Sessel als „Stand Ansicht x/z Blick“ und die Funde (Blick zur Wand, Stuhl nicht zum Tisch).</summary>
    private static (List<string> Tabelle, List<string> Funde) Erheben()
    {
        var tabelle = new List<string>();
        var funde = new List<string>();
        foreach (var (stand, innen) in SpielSitzenTests.AlleRaeume())
        {
            foreach (var m in innen.Stuecke)
            {
                if (Sessel.Contains(m.Name))
                {
                    var (mitte, blick) = (Mitte(m), Blick(m));
                    var zeile = string.Create(CultureInfo.InvariantCulture, $"{stand} {innen.Ansicht} {m.Name} {mitte.X:F2}/{mitte.Y:F2} {blick}");
                    tabelle.Add(zeile);
                    // vor dem Sessel (1,1 von der Mitte, gut eine halbe Sesseltiefe vor der Vorderkante) ist begehbarer Boden, nicht die Wand
                    var vorn = mitte + (Richtung(blick) * 1.1f);
                    if (!Wege.KannStehen(vorn.X, vorn.Y, innen.Lauf))
                    {
                        funde.Add("Sessel blickt zur Wand oder in ein Moebel: " + zeile);
                    }
                }
                else if (Stuehle.Contains(m.Name) && innen.Stuehle.Count > 0)
                {
                    var mitte = Mitte(m);
                    var platz = innen.Stuehle.MinBy(s => Vector2.Distance(s.Stuhl, mitte))!;
                    if (Vector2.Distance(platz.Stuhl, mitte) > 0.3f)
                    {
                        continue;   // Stuhl ohne Sitzplatz (z. B. Wartestuehle): keine Tischrichtung
                    }
                    var soll = new Vector2(MathF.Sin(platz.Blick), MathF.Cos(platz.Blick));
                    if (Vector2.Dot(soll, Richtung(Blick(m))) < 0.9f)
                    {
                        funde.Add(string.Create(CultureInfo.InvariantCulture, $"Stuhl blickt nicht zum Tisch: {stand} {innen.Ansicht} {m.Name} {mitte.X:F2}/{mitte.Y:F2} {Blick(m)}"));
                    }
                }
            }
        }
        return (tabelle, funde);
    }

    // Chefbuero der Bitweiche (buero): Rueckseite zur Suedwand, Blick nach Norden zum Schreibtisch (bis A 0.99.0 Blick zur Wand)
    private static readonly string[] Soll =
    [
        "neu buero loungeChair 0.82/4.78 n",
        "neu geschaeft:moebelhaus loungeChair 24.45/0.85 s",
        "neu kunde:lindenau loungeChair 19.08/5.07 w",
        "tag200 buero loungeChair 0.82/4.78 n",
        "tag200 geschaeft:moebelhaus loungeChair 24.45/0.85 s",
        "tag200 kunde:lindenau loungeChair 19.08/5.07 w",
        "gespielt geschaeft:moebelhaus loungeChair 24.45/0.85 s",
        "gespielt kunde:lindenau loungeChair 19.08/5.07 w",
    ];

    [Fact]
    public void AlleSesselUndStuehle_BlickWieTabelle_NieZurWand_Gegenprobe()
    {
        var (tabelle, funde) = Erheben();
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.True(tabelle.SequenceEqual(Soll), "Ist:\n" + string.Join("\n", tabelle.Select(z => $"        \"{z}\",")));
        Assert.Contains(tabelle, z => z.StartsWith("neu buero loungeChair", StringComparison.Ordinal));
        Innenraum.SesselAltFuerTests.Value = true;
        try
        {
            var (alt, altFunde) = Erheben();
            Assert.False(alt.SequenceEqual(Soll), "Gegenprobe: wie bis A 0.99.0 blickt der Sessel der Chefin zur Wand");
            Assert.Contains(altFunde, f => f.Contains("neu buero loungeChair", StringComparison.Ordinal));
        }
        finally
        {
            Innenraum.SesselAltFuerTests.Value = false;
        }
    }
}
