using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.83.0 (Weisung P12-283 Fund 1, Bild P12-168 02_Karte_im_Handy): die Nummernmarken der Orte auf der Karte im
/// Spiel-Handy liegen ganz innerhalb der Karte (samt weissem Rand und Marke „!“) und ueberlappen sich nicht, in Stadt 1
/// (Fassung 0, 1, 2) und in der Testkarte, bei jeder Groesse und Schriftstufe. Gegenprobe: mit den Marken wie bis A 0.82.0
/// (genau in der Mitte des Orts) zeigt die Testkarte genau Nicos Fund (Ueberlappung und Rand oben).
/// </summary>
[Collection("Haenger")]
public sealed class SpielKarteMarkenTests
{
    public static TheoryData<int, float, float, float> Lagen()
    {
        var d = new TheoryData<int, float, float, float>();
        foreach (var fassung in new[] { 0, 1, 2, -1 })
        {
            foreach (var (b, h) in new[] { (320f, 320f), (400f, 400f), (600f, 420f), (900f, 600f) })
            {
                foreach (var schrift in new[] { 1f, 1.3f, 1.6f, 2f })
                {
                    d.Add(fassung, b, h, schrift);
                }
            }
        }
        return d;
    }

    /// <summary>Szene einer Fassung von Stadt 1 (0, 1, 2) oder der Testkarte (-1).</summary>
    internal static StadtSzene Szene(int fassung)
    {
        if (fassung < 0)
        {
            return SpielTestkarteTests.Testkarte.Value.Szene;
        }
        var f = AktiveFassung.LadenNummer(p => File.Exists(Path.Combine(Pfade.Wurzel, p)) ? File.ReadAllBytes(Path.Combine(Pfade.Wurzel, p)) : null, fassung);
        var inhalt = f.MitKarte(SpielInhaltTests.Inhalt);
        return new StadtSpiel(f.StadtFuer(inhalt), SpielQuestTests.AllesErledigt(inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value).Szene;
    }

    [Theory]
    [MemberData(nameof(Lagen))]
    public void JedeMarke_GanzInDerKarte_KeineUeberlappt(int fassung, float breite, float hoehe, float schrift)
    {
        var szene = Szene(fassung);
        // schlimmster Fall: an jedem Ort wartet ein Auftrag (Marke „!“ oben rechts), dazu ganz ohne Auftrag
        foreach (var aufgaben in new[] { szene.Orte.Select(o => o.Id).ToHashSet(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal) })
        {
            var karte = new StadtKarte(szene, aufgaben);
            var s = karte.Massstab(breite, hoehe);
            var marken = karte.Marken(s, schrift);
            Assert.Equal(karte.Orte.Count, marken.Count);
            Assert.Empty(karte.MarkenFund(marken, s, schrift));
            // jede Marke trifft beim Antippen genau ihren Ort
            for (var i = 0; i < karte.Orte.Count; i++)
            {
                Assert.Equal(karte.Orte[i].Nr, karte.Treffer(marken[i], s, schrift)?.Nr);
            }
        }
    }

    [Fact]
    public void Gegenprobe_MarkenWieBisher_TestkarteZeigtNicosFund()
    {
        var szene = Szene(-1);
        var karte = new StadtKarte(szene, szene.Orte.Select(o => o.Id).ToHashSet(StringComparer.Ordinal));
        var s = karte.Massstab(400, 400);
        StadtKarte.MarkenAltFuerTests = true;
        List<string> funde;
        try
        {
            funde = karte.MarkenFund(karte.Marken(s), s);
        }
        finally
        {
            StadtKarte.MarkenAltFuerTests = false;
        }
        Assert.Contains(funde, f => f.Contains("überlappen sich", StringComparison.Ordinal));
        Assert.Contains(funde, f => f.Contains("ragt über den Rand", StringComparison.Ordinal));
        // neu: dieselbe Karte ohne Fund
        Assert.Empty(karte.MarkenFund(karte.Marken(s), s));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Stadt1_MarkenRueckenHoechstensWenig(int fassung)
    {
        // Nicos Stadt soll sich kaum sichtbar aendern: jede Marke bleibt nah an der Mitte ihres Orts (Messwert fuer P12-169)
        var szene = Szene(fassung);
        var karte = new StadtKarte(szene, new HashSet<string>(StringComparer.Ordinal));
        foreach (var (b, schrift) in new[] { (320f, 1f), (400f, 1f), (600f, 1f), (320f, 1.6f), (400f, 2f) })
        {
            var s = karte.Massstab(b, b);
            var neu = karte.Marken(s, schrift);
            var weiteste = karte.Orte.Select((o, i) => Vector2.Distance(StadtKarte.Bild(o.Mitte, s), neu[i])).Max();
            var bewegt = karte.Orte.Where((o, i) => Vector2.Distance(StadtKarte.Bild(o.Mitte, s), neu[i]) > 0.5f).Count();
            TestContext.Current.TestOutputHelper?.WriteLine($"Fassung {fassung}, Breite {b}, Schrift {schrift}: {bewegt} Marken bewegt, weiteste {weiteste:0.0} Bildpunkte");
            Assert.True(weiteste <= 4 * StadtKarte.MarkeRadius * schrift, $"{weiteste}");
        }
    }
}
