using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Nachtrag 1 zu P7-13 (Wunsch 4): Zeitmessung je 3D-Bild. Je 10 s eine Zeile mit Bildern je Sekunde,
/// Abstaenden und Teilzeiten, die langsamsten Bilder mit allen Teilen, hoechstens 16 KB, im Problembericht.
/// Dazu: die Signatur ohne Geometrie ist gleich der gebauten Stadt (sonst bliebe ein neuer Ort unsichtbar).
/// </summary>
public sealed class SpielBildZeitenTests
{
    private static BildZeiten.Bild Bild(double zeit, double abstand, double zeichnen = 3, int gc0 = 0, long bytes = 512, bool innen = false) =>
        new(zeit, abstand, 0.2, 0.5, zeichnen, 1.0, gc0, 0, 0, bytes, innen);

    [Fact]
    public void ZeilenJeZehnSekundenUndLangsameBilder()
    {
        var z = new BildZeiten { Kopf = "Grafik: Test" };
        var zeit = 0.0;
        // 25 s mit 120 Bildern/s, bei 12 s ein Bild mit 41,7 ms (24/s) und einer Speicherbereinigung
        for (var i = 0; i < 25 * 120; i++)
        {
            var langsam = i == 12 * 120;
            var abstand = langsam ? 41.7 : 1000.0 / 120;
            zeit += abstand / 1000;
            z.Aufnehmen(Bild(zeit, abstand, langsam ? 30 : 3, langsam ? 1 : 0));
        }
        var text = z.Text();
        Assert.StartsWith("Bildzeiten im Spiel", text, StringComparison.Ordinal);
        Assert.Contains("Grafik: Test", text, StringComparison.Ordinal);
        Assert.Contains("Bilder gesamt: 3000", text, StringComparison.Ordinal);
        var zeilen = text.Split('\n');
        // zwei volle Fenster (0-10 s, 10-20 s) und das laufende
        Assert.Equal(3, zeilen.Count(l => l.Contains(" Stadt: ", StringComparison.Ordinal) && l.Contains("120 je s im Lauf", StringComparison.Ordinal)));
        Assert.Single(zeilen, l => l.EndsWith("(laufend)", StringComparison.Ordinal));
        Assert.Contains(zeilen, l => l.Contains("GC 1/0/0", StringComparison.Ordinal) && l.Contains("1 langsam", StringComparison.Ordinal));
        Assert.Contains("Langsamste Bilder (über 20,0 ms):", text, StringComparison.Ordinal);
        Assert.Contains("41,7 ms (Logik 0,2, Vorbereitung 0,5, Zeichnen 30,0, Danach 1,0, GC 1/0/0, 0 KB)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PausenSindKeineLangsamenBilderUndDerTextBleibtKlein()
    {
        var z = new BildZeiten();
        var zeit = 0.0;
        for (var i = 0; i < 40000; i++)
        {
            // alle 50 Bilder eine Pause von 2 s (nichts zu zeichnen), dazwischen 60 Bilder/s, drinnen
            var abstand = i % 50 == 0 ? 2000 : 1000.0 / 60;
            zeit += abstand / 1000;
            z.Aufnehmen(Bild(zeit, abstand, innen: true));
        }
        var text = z.Text();
        Assert.True(Encoding.UTF8.GetByteCount(text) <= BildZeiten.DateiMax);
        Assert.DoesNotContain("Langsamste Bilder", text, StringComparison.Ordinal);
        Assert.Contains(" innen: ", text, StringComparison.Ordinal);
        Assert.Contains("60 je s im Lauf", text, StringComparison.Ordinal);
        Assert.Contains(", 0 langsam", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BildzeitenImProblembericht()
    {
        using var temp = new TempOrdner();
        var ohne = Problembericht.Bauen(null, "A 0.12.2", "Handy", [], DateTime.Now, temp.Pfad, Path.Combine(temp.Pfad, "update.log"));
        Assert.DoesNotContain(BildZeiten.DateiName, ohne, StringComparison.Ordinal);
        var z = new BildZeiten();
        z.Aufnehmen(Bild(1, 0));
        z.Aufnehmen(Bild(1.02, 20));
        File.WriteAllText(Path.Combine(temp.Pfad, BildZeiten.DateiName), z.Text());
        var mit = Problembericht.Bauen(null, "A 0.12.2", "Handy", new JsonObject(), DateTime.Now, temp.Pfad, Path.Combine(temp.Pfad, "update.log"));
        Assert.Contains("--- spiel_bilder.log ---\nBildzeiten im Spiel", mit, StringComparison.Ordinal);
    }

    [Fact]
    public void SignaturOhneGeometrieGleichDerGebautenStadt()
    {
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        Assert.Equal(StadtSzene.Bauen(stadt, null).Signatur, StadtSzene.SignaturFuer(stadt, null));
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var mit = StadtSzene.SignaturFuer(stadt, stand);
        Assert.Equal(StadtSzene.Bauen(stadt, stand).Signatur, mit);
        // Kontrollfehler: anderer Stand, andere Signatur (sonst baute die Ankunft nie neu)
        Assert.NotEqual(StadtSzene.SignaturFuer(stadt, null), mit);
    }
}
