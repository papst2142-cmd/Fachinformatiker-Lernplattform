using Lernplattform.Core.Update;

namespace Lernplattform.Tests;

/// <summary>
/// Mac-Welle (Weisung P11-28, Sammelliste 200 = Fund M4): Findet Velopack am Mac kein Paket (Release ohne
/// Mac-Feed), meldete das Programm "neueste Version", obwohl die GitHub-Seite eine neuere Version hatte.
/// Gegenprobe: mit dem alten Weg (nur Velopack, entspricht mac: false) bleibt das Angebot aus.
/// </summary>
public class MacUpdateTests
{
    private static readonly UpdateAngebot Seite = new("A 9.9.9", "Neu", GithubSuche.ReleaseSeite, "datei.pkg",
        "https://example.invalid/datei.pkg", 123, "sha256:00", new object());

    private static readonly UpdateAngebot Velopack = new("A 9.9.8", "Neu", GithubSuche.ReleaseSeite, "paket.nupkg", null, 5, null, new object());

    [Fact]
    public async Task MacOhneFeed_FragtDieReleaseSeite_NurMitDownloadSeite()
    {
        var angebot = await OhneFeed.SuchenAsync(() => Task.FromResult<UpdateAngebot?>(null), () => Task.FromResult<UpdateAngebot?>(Seite), mac: true);
        Assert.NotNull(angebot);
        Assert.Equal("A 9.9.9", angebot.Version);
        Assert.Equal(GithubSuche.ReleaseSeite, angebot.Seite);
        // nichts zum Einspielen: das Update-Fenster bietet nur die Seite an (UpdateDienst prueft DateiName)
        Assert.Null(angebot.DateiName);
        Assert.Null(angebot.DateiUrl);
        Assert.Null(angebot.Intern);
    }

    [Fact]
    public async Task Gegenprobe_OhneMacRueckfall_BliebEsBeiNeuesterVersion()
    {
        Assert.Null(await OhneFeed.SuchenAsync(() => Task.FromResult<UpdateAngebot?>(null), () => Task.FromResult<UpdateAngebot?>(Seite), mac: false));
    }

    [Fact]
    public async Task VelopackAngebot_GehtVor_SeiteWirdNichtGefragt()
    {
        var gefragt = false;
        var angebot = await OhneFeed.SuchenAsync(() => Task.FromResult<UpdateAngebot?>(Velopack),
            () => { gefragt = true; return Task.FromResult<UpdateAngebot?>(Seite); }, mac: true);
        Assert.Same(Velopack, angebot);
        Assert.False(gefragt);
    }

    [Fact]
    public async Task MacOhneFeed_SeiteOhneNeuere_BleibtNeuesteVersion()
    {
        Assert.Null(await OhneFeed.SuchenAsync(() => Task.FromResult<UpdateAngebot?>(null), () => Task.FromResult<UpdateAngebot?>(null), mac: true));
    }
}
