using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Nachbesserung S 0.5.1 (Weisung P12-42 Punkt 1): "Nach Updates suchen" antwortet immer; beim Start kommt ein Fenster
/// nur bei einer neuen Version. Die Suche selbst nimmt nur Releases der Linie vS (LinienQuelle, Tests in der
/// Lernplattform seit Phase 13 W2).
/// </summary>
public class UpdateSucheTests
{
    [Fact]
    public async Task JedesErgebnisBekommtEineAntwort()
    {
        var neu = await UpdateSuche.SuchenAsync(() => Task.FromResult(new UpdateErgebnis(UpdateArt.Neu, "0.5.2", () => Task.CompletedTask)));
        Assert.Equal((Texte.UpdateNeuTitel, Texte.UpdateNeuText("0.5.2")), UpdateSuche.Antwort(neu));
        Assert.Contains("S 0.5.2", UpdateSuche.Antwort(neu).Text, StringComparison.Ordinal);
        var aktuell = await UpdateSuche.SuchenAsync(() => Task.FromResult(new UpdateErgebnis(UpdateArt.Aktuell)));
        Assert.Contains(Werkzeug.Anzeige, UpdateSuche.Antwort(aktuell).Text, StringComparison.Ordinal);
        var nicht = await UpdateSuche.SuchenAsync(() => Task.FromResult(new UpdateErgebnis(UpdateArt.NichtInstalliert)));
        Assert.Equal(Texte.UpdateNichtInstalliert, UpdateSuche.Antwort(nicht).Text);
        foreach (var e in new[] { neu, aktuell, nicht })
        {
            Assert.False(string.IsNullOrWhiteSpace(UpdateSuche.Antwort(e).Titel));
        }
    }

    [Fact]
    public async Task FehlerDerSuche_WirdZurAntwortInEinfachenWorten()
    {
        var e = await UpdateSuche.SuchenAsync(() => throw new HttpRequestException("kein Netz"));
        Assert.Equal(UpdateArt.Fehler, e.Art);
        Assert.Contains("kein Netz", e.Grund, StringComparison.Ordinal);
        Assert.Equal(Texte.UpdateFehler, UpdateSuche.Antwort(e).Text);
        // Gegenprobe: ein anderer Fehler (Programmfehler) wird nicht verschluckt
        await Assert.ThrowsAsync<ArgumentException>(() => UpdateSuche.SuchenAsync(() => throw new ArgumentException("Fehler im Programm")));
    }

    [Fact]
    public void BeimStart_NurBeiNeuerVersionEinFenster()
    {
        Assert.True(UpdateSuche.BeimStartZeigen(new UpdateErgebnis(UpdateArt.Neu)));
        Assert.False(UpdateSuche.BeimStartZeigen(new UpdateErgebnis(UpdateArt.Aktuell)));
        Assert.False(UpdateSuche.BeimStartZeigen(new UpdateErgebnis(UpdateArt.NichtInstalliert)));
        Assert.False(UpdateSuche.BeimStartZeigen(new UpdateErgebnis(UpdateArt.Fehler)));
    }
}
