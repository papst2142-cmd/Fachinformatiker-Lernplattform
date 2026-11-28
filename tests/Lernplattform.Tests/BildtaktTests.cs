using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 1.20.1 (Sammelliste 473, Weisung P12-449 Punkt 1): Nachbildung des Tab A8 (Bildschirm 60 Hz, ein Bild erscheint zum
/// naechsten Takt nach seiner Anforderung): die Bildbremse fordert im Takt an. Bei 30 und 60 Bildern je Sekunde kommen die Bilder
/// gleichmaessig (95 % der Abstaende hoechstens 36 bzw. 20 ms), nie mehr als gewaehlt. Gegenprobe mit der Bremse bis A 1.20.0:
/// abwechselnd ein Takt mehr (wie am Tab A8 gemessen: 30 FPS Mitte 34,6 ms, 95 % 50 ms). Grenzen der Nachbildung: Beginn des Bilds
/// 0 bis 1,5 ms nach dem Takt, das Warten endet 0 bis <c>spaet</c> ms zu spaet, der Bildschirm nimmt eine Anforderung bis
/// <c>vorlauf</c> ms vor dem Takt noch an (am Geraet nicht gemessen, darum mehrere Werte).
/// </summary>
public sealed class BildtaktTests
{
    private const double Hz = 60;

    private static (double Hoch95, double Mitte, int JeSekunde) Nachbilden(int bilder, bool alt, double spaetMs, double vorlaufMs, int seed)
    {
        var zufall = new Random(seed);
        var takt = 1.0 / Hz;
        var vsync = 0.0;
        var abstaende = new List<double>();
        var beginne = new List<double>();
        var beginn = 0.0;
        for (var i = 0; i < 600; i++)
        {
            beginn = vsync + (zufall.NextDouble() * 0.0015);
            beginne.Add(beginn);
            var ende = beginn + 0.003;
            // Bremse: warten bis zur Anforderung, das Warten endet etwas zu spaet
            var warten = alt ? beginn + (1.0 / bilder) - ende : Bildtakt.Warten(ende - beginn, bilder, Hz);
            var anforderung = ende + Math.Max(0, warten) + (zufall.NextDouble() * spaetMs / 1000);
            // naechster Takt nach der Anforderung (eine Anforderung kurz vor dem Takt zaehlt noch fuer ihn)
            var naechster = Math.Ceiling((anforderung - (vorlaufMs / 1000) - 1e-9) / takt) * takt;
            if (naechster <= vsync)
            {
                naechster = vsync + takt;
            }
            abstaende.Add((naechster - vsync) * 1000);
            vsync = naechster;
        }
        abstaende.Sort();
        var jeSekunde = beginne.GroupBy(b => (int)Math.Floor(b)).Max(g => g.Count());
        return (abstaende[(int)(abstaende.Count * 0.95)], abstaende[abstaende.Count / 2], jeSekunde);
    }

    [Theory]
    [InlineData(3.0, 0.0)]
    [InlineData(3.0, 1.0)]
    [InlineData(6.0, 2.0)]
    [InlineData(1.5, 0.5)]
    public void Bremse_30_Gleichmaessig_GegenprobeAlt(double spaetMs, double vorlaufMs)
    {
        var neu = Nachbilden(30, false, spaetMs, vorlaufMs, 449);
        var alt = Nachbilden(30, true, spaetMs, vorlaufMs, 449);
        Assert.True(neu.Hoch95 <= 36, $"30 FPS: 95 % {neu.Hoch95:F1} ms (Grenze 36)");
        Assert.True(neu.JeSekunde <= 30, $"30 FPS: {neu.JeSekunde} Bilder in einer Sekunde");
        Assert.True(alt.Hoch95 > 36, $"Gegenprobe bis A 1.20.0: 95 % {alt.Hoch95:F1} ms");
    }

    [Theory]
    [InlineData(3.0, 0.0)]
    [InlineData(3.0, 1.0)]
    [InlineData(6.0, 2.0)]
    public void Bremse_60_Gleichmaessig_GegenprobeAlt(double spaetMs, double vorlaufMs)
    {
        var neu = Nachbilden(60, false, spaetMs, vorlaufMs, 449);
        var alt = Nachbilden(60, true, spaetMs, vorlaufMs, 449);
        Assert.True(neu.Hoch95 <= 20, $"60 FPS: 95 % {neu.Hoch95:F1} ms (Grenze 20)");
        Assert.True(neu.JeSekunde <= 60, $"60 FPS: {neu.JeSekunde} Bilder in einer Sekunde");
        Assert.True(alt.Hoch95 > 20, $"Gegenprobe bis A 1.20.0: 95 % {alt.Hoch95:F1} ms");
    }

    [Fact]
    public void OhneTakt_WieBisher()
    {
        // am PC (Takt unbekannt) genau die Rechnung bis A 1.20.0
        Assert.Equal((1.0 / 30) - 0.01, Bildtakt.Warten(0.01, 30, 0), 12);
        Assert.Equal(0.0345, Bildtakt.Schritt(0.0345, 0), 12);
    }

    [Fact]
    public void Schritt_GanzeTakte()
    {
        Assert.Equal(2 / Hz, Bildtakt.Schritt(0.0346, Hz), 12);
        Assert.Equal(3 / Hz, Bildtakt.Schritt(0.0497, Hz), 12);
        Assert.Equal(1 / Hz, Bildtakt.Schritt(0.004, Hz), 12);
        Assert.Equal(0.0, Bildtakt.Schritt(0, Hz), 12);
    }
}
