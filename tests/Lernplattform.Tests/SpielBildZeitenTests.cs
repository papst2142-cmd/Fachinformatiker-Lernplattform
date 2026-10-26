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

    /// <summary>
    /// Weisung P7-15: die Zeit eines langsamen Bildes bleibt bei diesem Bild (Luecke + eigene Teile), nicht
    /// beim naechsten; eine lange Luecke ohne gemessene Arbeit heisst "ausserhalb", eine Pause zaehlt nicht.
    /// </summary>
    [Fact]
    public void HaengerGehoertDemBildDasIhnVerursacht()
    {
        var z = new BildZeiten();
        // Bild 1: 120 ms Vorbereitung (z. B. Laden beim Betreten); Bild 2 danach: Abstand 125 ms, eigene Teile fast 0
        var langsam = new BildZeiten.Bild(1.0, 8.3, 0.1, 120, 3, 0.1, 0, 0, 0, 1024, true, 5, 0, 0, 0, 512);
        var danach = new BildZeiten.Bild(1.125, 125, 0.1, 0.2, 2, 0.1, 0, 0, 0, 1024, true, 5, 0, 0, 0, 512);
        var draussen = new BildZeiten.Bild(2.0, 90, 0.1, 0.2, 2, 0.1, 0, 0, 0, 1024, true, 88, 1, 0.5, 1.0, 512);
        var pause = new BildZeiten.Bild(5.0, 3000, 0.1, 0.2, 2, 0.1, 0, 0, 0, 1024, true, 2990, 0, 0, 0, 512);
        Assert.Equal(128.2, BildZeiten.Dauer(langsam), 6);
        Assert.Equal("Vorbereitung", BildZeiten.Teil(langsam));
        Assert.True(BildZeiten.Dauer(danach) < BildZeiten.LangsamAbMs);
        Assert.Equal("außerhalb", BildZeiten.Teil(draussen));
        Assert.Equal(86.5, BildZeiten.AusserhalbMs(draussen), 6);
        Assert.Equal(0, BildZeiten.AusserhalbMs(pause));
        Assert.True(BildZeiten.Dauer(pause) < BildZeiten.LangsamAbMs);
        foreach (var b in new[] { langsam, danach, draussen, pause })
        {
            z.Aufnehmen(b);
        }
        var text = z.Text();
        Assert.Contains("128,2 ms (Logik 0,1, Vorbereitung 120,0", text, StringComparison.Ordinal);
        Assert.Contains("Teil: Vorbereitung", text, StringComparison.Ordinal);
        Assert.Contains("Teil: außerhalb", text, StringComparison.Ordinal);
        Assert.DoesNotContain("125,0 ms (", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Weisung P7-16: je langsamem Bild die Teile der Auftraege (Wegsuche, Ereignis) und die Werte des
    /// Systems (Android), wenn die Plattform sie liefert; ohne sie bleibt die Zeile wie bisher.
    /// </summary>
    [Fact]
    public void LangsamesBildMitTeilenUndSystemwerten()
    {
        var z = new BildZeiten();
        z.Aufnehmen(new BildZeiten.Bild(1.0, 8.3, 0.1, 0.2, 2, 0.1, 0, 0, 0, 1024, false, 30, 1, 0.5, 26, 512, 0.4, 20.1));
        z.Aufnehmen(new BildZeiten.Bild(2.0, 120, 0.1, 0.2, 2, 0.1, 0, 0, 0, 1024, true, 118, 0, 0, 0, 512,
            System: "Android (seit 160 ms): GC 1 in 3 ms, blockierend 0 in 0 ms, ausgelassene Takte 13, Berührungen 0, Wärme keine"));
        z.Aufnehmen(new BildZeiten.Bild(3.0, 40, 0.1, 0.2, 2, 0.1, 0, 0, 0, 1024, true, 38, 0, 0, 0, 512));
        var text = z.Text();
        Assert.Contains("Aufträge 26,0 (davon Wegsuche 0,4, Ereignis 20,1), außerhalb", text, StringComparison.Ordinal);
        Assert.Contains("Teil: außerhalb; Android (seit 160 ms): GC 1 in 3 ms", text, StringComparison.Ordinal);
        var ohne = text.Split('\n').Single(l => l.Contains("00:00:03.00", StringComparison.Ordinal));
        Assert.EndsWith("Teil: außerhalb", ohne, StringComparison.Ordinal);
        Assert.DoesNotContain("davon", ohne, StringComparison.Ordinal);
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
