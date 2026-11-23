using System.Numerics;
using System.Runtime.InteropServices;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Sammelliste 437 (Weisung P12-354): das Kleingruen der Paket-Stadt hing am Rechner. Ein Grasbueschel bekam im Winter je nach
/// Drehung eine um wenige Millionstel andere Schneefarbe (Betrag der Normale nach oben aus den gedrehten Weltkoordinaten), an der
/// Grenze einer Farbstufe fiel es dadurch aus seiner Instancing-Gruppe, und das je Rechner verschieden. Seit A 1.4.1 kommen die
/// Entscheidungen je Dreieck einer nur um die Hochachse gedrehten Lage aus dem Raum des Modells: dasselbe Modell gibt in jeder
/// Drehung und an jedem Ort Bit fuer Bit dieselben Ecken im Raum des Modells. Gegenprobe: der alte Weg
/// (<see cref="Netz.ObenAusWeltFuerTests"/>) gibt verschiedene.
/// </summary>
public sealed class KleingruenRechnerfestTests
{
    private static byte[] LokalesNetz(GrafikModell m, float winkel, Vector3 ort, float groesse)
    {
        var lage = StadtSzene.Lage(m, Vector3.Zero, new Vector3(groesse), ort, new Vector2(MathF.Sin(winkel), MathF.Cos(winkel)));
        var faerbung = Jahreszeitfarben.FuerModell(Jahreszeit.Winter, Faerbeart.Klein, m, 12345, Jahreszeitfarben.Nadel(m));
        var n = new Netz();
        n.HinzuLokal(m, lage, faerbung);
        return [.. MemoryMarshal.AsBytes(n.EckenFeld), .. MemoryMarshal.AsBytes(n.IndizesFeld)];
    }

    private static int Verschiedene(string modell)
    {
        var m = SpielGrafikTests.Bibliothek.Value.Modell(modell)!;
        var netze = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 720; i++)
        {
            var winkel = i * MathF.Tau / 720f;
            var ort = new Vector3(3.7f + (i * 0.173f), 0.012f, 41.3f - (i * 0.057f));
            netze.Add(Convert.ToHexString(LokalesNetz(m, winkel, ort, 0.48589748f)));
        }
        return netze.Count;
    }

    [Theory]
    [InlineData("wd-grass_1_b")]
    [InlineData("wd-grass_2_b")]
    [InlineData("wd-bush_4_c")]
    public void Winter_JedeDrehungUndJederOrt_GleichesNetzImModell(string modell)
    {
        Assert.Equal(1, Verschiedene(modell));
    }

    [Fact]
    public void Gegenprobe_AlterWegAusWeltkoordinaten_GibtVerschiedeneNetze()
    {
        Netz.ObenAusWeltFuerTests = true;
        try
        {
            Assert.True(Verschiedene("wd-grass_1_b") > 1, "der alte Weg gab in jeder Drehung dasselbe Netz, die Gegenprobe sieht nichts");
        }
        finally
        {
            Netz.ObenAusWeltFuerTests = false;
        }
    }
}
