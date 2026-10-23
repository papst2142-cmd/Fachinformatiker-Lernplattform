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
        // Gegenpruefung W2 Fund 2: dazu Leser ohne Sperre (Oberflaeche, Abgleich), die dauernd laden
        var fertig = false;
        var leser = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            var einstellungen = new Einstellungen(ordner.Pfad);
            while (!Volatile.Read(ref fertig))
            {
                einstellungen.Laden();
            }
        })).ToArray();
        await Task.WhenAll(faeden);
        Volatile.Write(ref fertig, true);
        await Task.WhenAll(leser);
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
