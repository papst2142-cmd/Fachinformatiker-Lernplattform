using Lernplattform.Core.Fortschritt;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L7: Rechenteil der Diagramme wie Python v0.63.2. Die Sollwerte wurden mit
/// Python 3.12 aus fisi_widgets._nice_step/_axis_text und fisi_theme.label_stride/
/// shown_labels/curve_controls der eingefrorenen Python-Linie berechnet.
/// </summary>
public class DiagrammTests
{
    [Fact]
    public void GlatterSchrittWiePython()
    {
        double[] roh = [0.5, 1, 1.25, 2.6, 7, 11, 13, 26, 99, 101, 1250, 3001];
        int[] soll = [1, 1, 2, 3, 8, 12, 15, 30, 100, 120, 1500, 4000];
        Assert.Equal(soll, roh.Select(Diagramm.GlatterSchritt));
    }

    [Fact]
    public void AchsenTextWiePython()
    {
        double[] werte = [0, 25, 12500, 1000000, 7.5, 2.25];
        string[] soll = ["0", "25", "12.500", "1.000.000", "7,5", "2,25"];
        Assert.Equal(soll, werte.Select(Diagramm.AchsenText));
    }

    [Fact]
    public void BeschriftungsabstandWiePython()
    {
        double[] platz = [5, 10, 20, 30, 80];
        int[] soll = [15, 10, 5, 3, 1];
        Assert.Equal(soll, platz.Select(p => Diagramm.Schrittweite(30, p, 40)));
        Assert.Equal(1, Diagramm.Schrittweite(1, 5, 40));
        Assert.Equal(30, Diagramm.Schrittweite(30, 1, 1000));
        Assert.Equal([1, 8, 15, 22, 29], Diagramm.Gezeigt(30, 7));
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], Diagramm.Gezeigt(7, 1));
    }

    [Fact]
    public void KurveWiePython()
    {
        var k = Diagramm.Kontrollpunkte([(0, 100), (10, 50), (20, 80), (30, 80), (40, 0)]);
        (double, double)[] soll =
        [
            (3.3333333333333335, 83.33333333333333), (6.666666666666666, 50.0),
            (13.333333333333334, 50.0), (16.666666666666668, 80.0),
            (23.333333333333332, 80.0), (26.666666666666668, 80.0),
            (33.333333333333336, 80.0), (36.666666666666664, 26.666666666666668),
        ];
        Assert.Equal(soll, k.SelectMany(p => new[] { p.C1, p.C2 }));
    }

    [Fact]
    public void AchseMitZielUndFest()
    {
        Assert.Equal((100.0, 25.0), Diagramm.Achse([10, 20], 100, null));
        // max(5, 3, 20 * 1,1) = 22, 22 / 4 = 5,5 -> Schritt 6, Spitze 24; ohne Werte 5 / 4 -> Schritt 2
        Assert.Equal((24.0, 6.0), Diagramm.Achse([3], null, 20));
        Assert.Equal((8.0, 2.0), Diagramm.Achse([0, 0], null, null));
    }
}
