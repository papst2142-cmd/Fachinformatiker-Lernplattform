using Lernplattform.Core.Daten;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 5 W2 (Weisung P5-6 Punkt 4, Risiko 5, Sammelliste 55): Schreiben in einstellungen.json
/// ueber eine gemeinsame Sperre. Kontrollfehler "ohne Sperre": gleichzeitiges Lesen-Aendern-Schreiben
/// verliert Aenderungen, dann fehlen am Ende Schluessel oder Zaehler sind zu klein.
/// </summary>
public class EinstellungenSperreTests
{
    private const int Runden = 150;

    [Fact]
    public async Task GleichzeitigesSchreibenVerliertNichts()
    {
        using var ordner = new TempOrdner();
        // Gegenpruefung W2 Fund 2: dazu Leser ohne Sperre (Oberflaeche, Abgleich), die dauernd laden. Weisung P12-310: die Leser
        // starten zuerst, die Schreiber erst, wenn jeder Leser nachweislich einmal geladen hat (sonst konnten die Schreiber fertig
        // sein, bevor ein Leser anfing, und die Gegenpruefung lief nie); sie laden bis nach dem Ende der Schreiber.
        var fertig = false;
        using var leserLaufen = new CountdownEvent(2);
        var leser = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            var einstellungen = new Einstellungen(ordner.Pfad);
            var n = 0;
            do
            {
                einstellungen.Laden();
                if (n++ == 0)
                {
                    leserLaufen.Signal();
                }
            }
            while (!Volatile.Read(ref fertig));
            return n;
        })).ToArray();
        Assert.True(leserLaufen.Wait(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken), "Leser starteten nicht");
        // je Faden eine eigene Instanz wie Oberflaeche (Zustand) und Abgleich im Hintergrund
        var faeden = Enumerable.Range(0, 4).Select(nr => Task.Run(() =>
        {
            var einstellungen = new Einstellungen(ordner.Pfad);
            for (var i = 1; i <= Runden; i++)
            {
                if (nr % 2 == 0)
                {
                    Assert.True(einstellungen.Aendern(werte =>
                    {
                        werte["zaehler_" + nr] = i;
                        werte["summe"] = (werte["summe"]?.GetValue<int>() ?? 0) + 1;
                    }));
                }
                else
                {
                    Assert.True(einstellungen.Setze("zaehler_" + nr, i));
                }
            }
        })).ToArray();
        await Task.WhenAll(faeden);
        Volatile.Write(ref fertig, true);
        Assert.All(await Task.WhenAll(leser), n => Assert.True(n > 0));
        var ende = new Einstellungen(ordner.Pfad).Laden();
        for (var nr = 0; nr < 4; nr++)
        {
            Assert.Equal(Runden, ende["zaehler_" + nr]!.GetValue<int>());
        }
        Assert.Equal(2 * Runden, ende["summe"]!.GetValue<int>());
    }

    [Fact]
    public void AendernSetztUndEntferntSchluessel()
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        Assert.True(einstellungen.Aendern(werte => werte["a"] = 1));
        Assert.Equal(1, einstellungen.Laden()["a"]!.GetValue<int>());
        Assert.True(einstellungen.Aendern(werte => werte.Remove("a")));
        Assert.Null(einstellungen.Laden()["a"]);
        Assert.True(einstellungen.Laden()[Einstellungen.AutoCheckSchluessel]!.GetValue<bool>());
    }
}
