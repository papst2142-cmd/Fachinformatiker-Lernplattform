using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle Grafik G1 (Weisung P12-182): „Grenzen live“ prueft weiter gegen Mittel, die Info-Zeile nennt den Anteil an den
/// Grenzen von Niedrig und Hoch. Die Zaehlung mehrerer Stufen auf einmal ist je Stufe gleich der Zaehlung einer Stufe, und
/// Mittel ist gleich der Zaehlung ohne Angabe (wie bis A 0.63.0).
/// </summary>
public class GrenzenStufenTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public void BildJe_GleichJederStufeEinzeln_MittelWieVorher()
    {
        var spiel = Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Grenzen);
        var szene = spiel.Szene;
        var z = new StadtbauGrenzen.Draussen(szene, szene.Vorbereiten());
        var stufen = new[] { Grafikstufen.Mittel, Grafikstufen.Niedrig, Grafikstufen.Hoch };
        var verschieden = false;
        foreach (var abstand in new[] { 4f, 10f, 16f, 20f, 30f, Kamera.AbstandMax })
        {
            var k = new Kamera(szene.Breite / 3, szene.Tiefe / 2, abstand) { Neigung = Bauen3D.NeigungSpiel };
            var sp = k.Sicht * k.Projektion(GrenzenLive.BildBreite, GrenzenLive.BildHoehe);
            var je = z.BildJe(sp, abstand, stufen);
            for (var i = 0; i < stufen.Length; i++)
            {
                Assert.Equal(z.Bild(sp, abstand, werte: stufen[i]), je[i]);
            }
            Assert.Equal(z.Bild(sp, abstand), je[0]);
            Assert.True(je[1].Dreiecke <= je[0].Dreiecke && je[0].Dreiecke <= je[2].Dreiecke, abstand + ": " + string.Join(", ", je));
            verschieden |= je[1] != je[0] || je[2] != je[0];
        }
        // Gegenprobe: die Stufen zaehlen wirklich verschieden
        Assert.True(verschieden);
    }

    [Fact]
    public void InfoZeile_NiedrigUndHoch_GeprueftGegenMittel()
    {
        var g = GrenzenLive.Fuer(D.Fassung, D);
        Assert.True(g.AnteilNiedrig > 0 && g.AnteilHoch > 0);
        // Niedrig spart, hat aber auch die kleinere Grenze; Hoch zeichnet mehr bei groesserer Grenze
        Assert.True(g.AnteilHoch < g.AnteilNiedrig + 1);
        Assert.Contains("Niedrig ", g.StufenInfo, StringComparison.Ordinal);
        Assert.Contains("Hoch ", g.StufenInfo, StringComparison.Ordinal);
        Assert.EndsWith("(geprüft wird gegen Mittel)", g.StufenInfo, StringComparison.Ordinal);
        // geprueft wird gegen Mittel: Fund nur ueber der Grenze von Mittel
        Assert.Equal(g.Anteil > 1, g.Fund() is not null);
    }
}
