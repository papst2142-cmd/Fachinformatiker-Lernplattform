using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 1.20.1 (Sammelliste 477, Weisung P12-449 Punkt 3): beim Vorwaertsgehen (Stick bzw. Taste in eine Richtung, mit kurzen
/// Pausen, einem kuenstlichen Haenger von 150 ms und dem Spielereignis jedes Halts) geht die Figur nie entgegen der Laufrichtung,
/// und die Kamera, die ihr folgt, geht nie zurueck. An jedem Ort der Probestadt in alle vier Richtungen. Gegenprobe mit dem Ablauf
/// bis A 1.20.0: blieb die Figur bis 0,5 vor einer Tuer stehen, ging sie auf den Stehplatz zurueck (die Kamera mit).
/// </summary>
public sealed class SpielVorwaertsTests
{
    private const double Bild = 1.0 / 30;

    private static (int Figur, int Kamera, int Halte, List<string> Beispiele) Gehen(bool alt)
    {
        var spiel = SpielBusfahrtTests.Spiel();
        var zufall = new Random(477);
        var (figur, kamera, halte) = (0, 0, 0);
        var beispiele = new List<string>();
        StadtSpiel.HaltVorTuerAltFuerTests = alt;
        try
        {
            foreach (var ort in spiel.Szene.Orte)
            {
                foreach (var (rx, rz) in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0) })
                {
                    spiel.StelleFuerTests(ort.StehX, ort.StehZ);
                    var k = new Kamera(spiel.FigurX, spiel.FigurZ, 8f);
                    for (var stueck = 0; stueck < 6; stueck++)
                    {
                        var halten = 0.4 + (zufall.NextDouble() * 1.5);
                        var pause = StadtSpiel.HaltNach + 0.01 + (zufall.NextDouble() * 0.5);
                        var haenger = zufall.NextDouble() * halten;
                        for (var t = 0.0; t < halten + pause; t += Bild)
                        {
                            var an = t < halten;
                            // ein Haenger von 150 ms (der Zeitschritt ist wie in der Stadtflaeche auf 0,1 s begrenzt)
                            var dt = Math.Abs(t - haenger) < Bild / 2 ? Math.Min(0.15, 0.1) : Bild;
                            var (x0, z0) = (spiel.FigurX, spiel.FigurZ);
                            var (kx0, kz0) = (k.ZielX, k.ZielZ);
                            var zahl = spiel.Ablauf.Stand.Verlauf.Count;
                            spiel.FreiGehen(an ? rx : 0, an ? rz : 0, dt);
                            spiel.Fortschreiten(dt);
                            k.Folgen(spiel.FigurX, spiel.FigurZ, dt);
                            halte += spiel.Ablauf.Stand.Verlauf.Count - zahl;
                            var vor = ((spiel.FigurX - x0) * rx) + ((spiel.FigurZ - z0) * rz);
                            var kvor = ((k.ZielX - kx0) * rx) + ((k.ZielZ - kz0) * rz);
                            if (vor < -1e-4)
                            {
                                figur++;
                                if (beispiele.Count < 5)
                                {
                                    beispiele.Add($"{ort.Id} ({rx},{rz}) Figur {vor:F3}");
                                }
                            }
                            if (kvor < -1e-4)
                            {
                                kamera++;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            StadtSpiel.HaltVorTuerAltFuerTests = false;
        }
        return (figur, kamera, halte, beispiele);
    }

    [Fact]
    public void Vorwaerts_NieZurueck_GegenprobeAlt()
    {
        var neu = Gehen(alt: false);
        Assert.True(neu.Halte >= 15, $"nur {neu.Halte} Spielereignisse beim Gehen");
        Assert.True(neu.Figur == 0, $"Figur ging {neu.Figur}x zurueck: {string.Join("; ", neu.Beispiele)}");
        Assert.Equal(0, neu.Kamera);
        var alt = Gehen(alt: true);
        Assert.True(alt.Figur > 0, "Gegenprobe bis A 1.20.0: die Figur ging nie zurueck");
        Assert.True(alt.Kamera > 0, "Gegenprobe bis A 1.20.0: die Kamera ging nie zurueck");
    }
}
