using System.Net;
using System.Text;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;
using Lernplattform.Desktop;
using Velopack.Logging;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.53.2 (Sammelliste 256, Weisung P12-113): die Update-Suche der Lernplattform haelt Stoerungen von GitHub aus
/// (am 09.11.2026 rund jede dritte Antwort 504). Am PC (Velopack) wie das Werkzeug seit S 0.25.1: nur der Feed des
/// neuesten Releases der Linie, jede Abfrage bei Stoerung bis zu 4 Mal. Am Handy und im Mac-Rueckfall (eine Abfrage
/// "latest") dieselbe Wiederholung. Gegenproben: die alte Quelle (alle Feeds, ein Versuch) scheitert am selben 504,
/// 404 wird nie wiederholt, ein Abbruch durch den Nutzer auch nicht.
/// </summary>
[Collection("Update")]
public class UpdateStoerungTests
{
    // Lernplattform-Linie mit Lockvogel vS9.9.9 (Werkzeug) vorne, Kanal "win" wie am Windows-PC
    private static readonly string[] Tags = ["vS9.9.9", "vA0.53.1", "vA0.53.0", "vA0.52.1"];

    private static GithubTestserver Server(Dictionary<string, int> stoerungen, HttpStatusCode stoerung = HttpStatusCode.GatewayTimeout) =>
        new(stoerungen, stoerung, Tags, "win", "Fachinformatiker-Lernplattform");

    private static string Repo(GithubTestserver s) => s.Adresse + "papst2142-cmd/Fachinformatiker-Lernplattform";

    // wie die Lernplattform am PC: kein fester Kanal, Velopack nennt ihn ("win")
    private static string[] Versionen(Velopack.Sources.IUpdateSource quelle) =>
        [.. quelle.GetReleaseFeed(NullVelopackLogger.Instance, null, "win").GetAwaiter().GetResult().Assets.Select(a => a.Version.ToString())];

    private static void OhnePausen(Action tun)
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        try
        {
            tun();
        }
        finally
        {
            Stoerung.OhnePausenFuerTests.Value = false;
        }
    }

    [Fact]
    public void Pc_NurNeuesterFeed_504WirdWiederholt() => OhnePausen(() =>
    {
        // Liste: ein Versuch scheitert, Feed: zwei Versuche (Velopack fragt je Versuch zweimal, einmal klein geschrieben)
        using var s = Server(new() { ["releases"] = 2, ["feed_vA0.53.1.json"] = 4 });
        var zeilen = new List<string>();
        var gemeldet = new List<string>();
        var quelle = new StoerungsfesteQuelle(Repo(s), ReleaseLinie.Linie.Lernplattform, null, zeilen.Add, TimeSpan.FromSeconds(30));
        quelle.Wiederholt += gemeldet.Add;
        Assert.Equal(["1.0.0-a.0.53.1"], Versionen(quelle));
        Assert.Equal(3, s.Anzahl("/releases"));
        Assert.Equal(5, s.Anzahl("feed_vA0.53.1"));
        Assert.Equal(0, s.Anzahl("feed_vA0.53.0"));
        Assert.Equal(0, s.Anzahl("vS9.9.9"));
        Assert.Equal(3, zeilen.Count);
        Assert.All(zeilen, z => Assert.StartsWith(Stoerung.Zeile, z, StringComparison.Ordinal));
        Assert.Equal(zeilen, gemeldet);
    });

    [Fact]
    public void Pc_Gegenprobe_AlleFeedsWieBisher_ScheitertAmStoerungDesAltenFeeds() => OhnePausen(() =>
    {
        // wie die Quelle bis A 0.53.1 (Velopacks GithubSource ueber LinienQuelle): jeder Feed der Linie wird geladen;
        // bleibt GitHub beim Feed eines alten Releases gestoert, scheitert die ganze Suche
        using var s = Server(new() { ["feed_vA0.52.1.json"] = 99 });
        StoerungsfesteQuelle.AlleFeedsFuerTests.Value = true;
        try
        {
            var x = Assert.ThrowsAny<HttpRequestException>(() => Versionen(new StoerungsfesteQuelle(Repo(s), ReleaseLinie.Linie.Lernplattform, null, _ => { })));
            Assert.Equal(HttpStatusCode.GatewayTimeout, x.StatusCode);
            Assert.Equal(1, s.Anzahl("feed_vA0.53.0"));
        }
        finally
        {
            StoerungsfesteQuelle.AlleFeedsFuerTests.Value = false;
        }
        // dieselbe Stoerung trifft die neue Quelle nie (sie laedt den alten Feed nicht)
        var vorher = s.Anzahl("feed_vA0.52.1");
        Assert.Equal(["1.0.0-a.0.53.1"], Versionen(new StoerungsfesteQuelle(Repo(s), ReleaseLinie.Linie.Lernplattform, null, _ => { })));
        Assert.Equal(vorher, s.Anzahl("feed_vA0.52.1"));
    });

    [Fact]
    public void Pc_KanalVonVelopack_FremderKanalOhneFeed() => OhnePausen(() =>
    {
        using var s = Server([]);
        var quelle = new StoerungsfesteQuelle(Repo(s), ReleaseLinie.Linie.Lernplattform, null, _ => { });
        Assert.Empty(quelle.GetReleaseFeed(NullVelopackLogger.Instance, null, "linux").GetAwaiter().GetResult().Assets);
        Assert.Equal(0, s.Anzahl("feed_"));
    });

    [Fact]
    public void Pc_404_NieWiederholt() => OhnePausen(() =>
    {
        using var s = Server(new() { ["feed_vA0.53.1.json"] = 5 }, HttpStatusCode.NotFound);
        var zeilen = new List<string>();
        Assert.ThrowsAny<HttpRequestException>(() => Versionen(new StoerungsfesteQuelle(Repo(s), ReleaseLinie.Linie.Lernplattform, null, zeilen.Add)));
        Assert.Equal(2, s.Anzahl("feed_vA0.53.1"));
        Assert.Empty(zeilen);
    });

    [Fact]
    public void Pc_DauerhaftNachVierVersuchen_FehlerBleibt() => OhnePausen(() =>
    {
        using var s = Server(new() { ["feed_vA0.53.1.json"] = 9 });
        var zeilen = new List<string>();
        var x = Assert.ThrowsAny<HttpRequestException>(() => Versionen(new StoerungsfesteQuelle(Repo(s), ReleaseLinie.Linie.Lernplattform, null, zeilen.Add)));
        Assert.Equal(HttpStatusCode.GatewayTimeout, x.StatusCode);
        Assert.Equal(8, s.Anzahl("feed_vA0.53.1"));
        Assert.Equal(3, zeilen.Count);
    });

    [Fact]
    public void Pausen_WieImWerkzeug() =>
        Assert.Equal([TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)], Stoerung.Pausen);

    // ---- Seite "latest" (Handy, Mac-Rueckfall, Ordner-Programm) ------------------------------------------------

    private const string Antwort = "{\"tag_name\": \"vA0.53.2\", \"assets\": [{\"name\": \"a.apk\", \"browser_download_url\": \"u\", \"size\": 3}]}";

    private static async Task<(UpdateAngebot? Angebot, string? Fehler, int Anfragen, List<string> Zeilen)> Latest(Func<int, HttpResponseMessage> antwort,
        CancellationToken abbruch = default)
    {
        var anfragen = 0;
        var zeilen = new List<string>();
        using var client = new HttpClient(new Attrappe(_ => antwort(++anfragen)));
        try
        {
            return (await GithubSuche.SuchenAsync(client, "A 0.53.1", "android", "http://127.0.0.1:9/releases/latest", abbruch, zeilen.Add), null, anfragen, zeilen);
        }
        catch (UpdateFehler e)
        {
            return (null, e.Message, anfragen, zeilen);
        }
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(Antwort)) };

    [Fact]
    public async Task Latest_504ZweiMal_DannAngebot()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        var r = await Latest(n => n <= 2 ? new HttpResponseMessage(HttpStatusCode.GatewayTimeout) : Ok(), TestContext.Current.CancellationToken);
        Assert.Equal("A 0.53.2", r.Angebot?.Version);
        Assert.Equal(3, r.Anfragen);
        Assert.Equal(2, r.Zeilen.Count);
        Assert.All(r.Zeilen, z => Assert.StartsWith("WIEDERHOLUNG Suche latest Versuch ", z, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Latest_KeineAntwort_DannAngebot()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        var r = await Latest(n => n == 1 ? throw new HttpRequestException("kein Netz") : Ok(), TestContext.Current.CancellationToken);
        Assert.Equal("A 0.53.2", r.Angebot?.Version);
        Assert.Equal(2, r.Anfragen);
    }

    [Fact]
    public async Task Latest_DauerhaftGestoert_GleicherTextWieVorher()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        var r = await Latest(_ => new HttpResponseMessage(HttpStatusCode.GatewayTimeout), TestContext.Current.CancellationToken);
        Assert.Equal(UpdateTexte.Abgelehnt.Replace("%d", "504", StringComparison.Ordinal), r.Fehler);
        Assert.Equal(4, r.Anfragen);
        Assert.Equal(3, r.Zeilen.Count);
        var netz = await Latest(_ => throw new HttpRequestException("kein Netz"), TestContext.Current.CancellationToken);
        Assert.Equal(UpdateTexte.KeineVerbindung, netz.Fehler);
        Assert.Equal(4, netz.Anfragen);
    }

    [Fact]
    public async Task Latest_Gegenprobe_404_403_NieWiederholt()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        foreach (var code in new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden })
        {
            var r = await Latest(_ => new HttpResponseMessage(code), TestContext.Current.CancellationToken);
            Assert.Equal(UpdateTexte.Abgelehnt.Replace("%d", ((int)code).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), r.Fehler);
            Assert.Equal(1, r.Anfragen);
            Assert.Empty(r.Zeilen);
        }
    }

    [Fact]
    public async Task Latest_AbbruchDurchNutzer_NieWiederholt()
    {
        using var abbruch = new CancellationTokenSource();
        var r = await Latest(_ =>
        {
            abbruch.Cancel();
            throw new TaskCanceledException();
        }, abbruch.Token);
        Assert.Equal(UpdateTexte.KeineVerbindung, r.Fehler);
        Assert.Equal(1, r.Anfragen);
        Assert.Empty(r.Zeilen);
    }

    private sealed class Attrappe(Func<HttpRequestMessage, HttpResponseMessage> antwort) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(antwort(request));
    }
}
