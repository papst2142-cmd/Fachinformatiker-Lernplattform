using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lernplattform.Core.Update;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Lernplattform.Desktop;

/// <summary>
/// Fund Lauf 709/712 (Nico 09.11.2026: viele 504 bei GitHub), im Werkzeug behoben mit S 0.25.1, in der Lernplattform
/// mit A 0.53.2 (Sammelliste 256): Velopacks GithubSource laedt in GetReleaseFeed die Feed-Datei JEDES Releases
/// nacheinander (heute rund 20 Releases je Linie, je rund 11 s bei github.com) und bricht beim ersten Fehler ganz ab;
/// GitHub antwortete dabei auf rund jede dritte Datei mit 504. Diese Quelle nimmt (1) nur das neueste Release der Linie,
/// das eine Feed-Datei fuer den Kanal hat (es nennt das volle Paket der neuesten Version), und (2) wiederholt jede
/// einzelne Abfrage (Release-Liste, Feed, Paket) bei einer Stoerung (<see cref="Stoerung.Voruebergehend"/>) nach
/// <see cref="Stoerung.Pausen"/>. Gleicher Filter auf die Linie wie <see cref="LinienQuelle"/> (nur gelesen,
/// unveraendert). Die Datei wird in Lernplattform.Desktop und Stadtbau.Desktop gleich gebaut (verlinkt).
/// </summary>
public sealed class StoerungsfesteQuelle : GithubSource
{
    private readonly ReleaseLinie.Linie _linie;
    private readonly string? _kanal;
    private readonly Melder _melder;
    private string? _feedKanal;

    /// <param name="repo">Volle Adresse des Repos (https://github.com/...; in Tests http://127.0.0.1:port/...).</param>
    /// <param name="linie">Nur Releases dieser Linie (vA/vB/v1 oder vS).</param>
    /// <param name="kanal">Velopack-Kanal; null = der Kanal, nach dem Velopack fragt (Lernplattform: win, linux, osx, osx-x64).</param>
    /// <param name="melden">bekommt jede Zeile <see cref="Stoerung.Zeile"/> (update.log).</param>
    /// <param name="abrufZeit">Zeitgrenze je Abruf von Liste und Feed (null = Velopack, 30 Minuten); nie fuer das Paket.</param>
    public StoerungsfesteQuelle(string repo, ReleaseLinie.Linie linie, string? kanal, Action<string> melden, TimeSpan? abrufZeit = null)
        : this(repo, linie, kanal, new Melder(melden), abrufZeit)
    {
    }

    private StoerungsfesteQuelle(string repo, ReleaseLinie.Linie linie, string? kanal, Melder melder, TimeSpan? abrufZeit)
        : base(repo, null, false, new StoerungsfestLaden(melder.Melden, abrufZeit))
    {
        _linie = linie;
        _kanal = kanal;
        _melder = melder;
    }

    /// <summary>Nur fuer die Gegenprobe der Tests: die Quelle laedt die Feeds aller Releases der Linie (wie GithubSource).</summary>
    internal static readonly AsyncLocal<bool> AlleFeedsFuerTests = new();

    /// <summary>Jede Wiederholung (nach <c>melden</c>); die Lernplattform verlaengert damit die Zeitgrenze von Suche und Laden.</summary>
    public event Action<string>? Wiederholt
    {
        add => _melder.Weiter += value;
        remove => _melder.Weiter -= value;
    }

    public override Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null,
        VelopackAsset? latestLocalRelease = null)
    {
        _feedKanal = channel;
        return base.GetReleaseFeed(logger, appId, channel, stagingId, latestLocalRelease);
    }

    protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
    {
        var adresse = new Uri(GetApiBaseUrl(RepoUri), $"repos{RepoUri.AbsolutePath}/releases?per_page=30&page=1");
        var antwort = await Downloader.DownloadString(adresse.ToString(), GetRequestHeaders("application/vnd.github.v3+json"), 30).ConfigureAwait(false);
        return NeuestesMitFeed(LinienQuelle.Waehlen(antwort, _linie, includePrereleases), _kanal ?? _feedKanal ?? "");
    }

    /// <summary>Wie Velopack, aber mit Port (fuer den Testserver der Tests auf 127.0.0.1); github.com unveraendert.</summary>
    protected override Uri GetApiBaseUrl(Uri repoUrl) =>
        repoUrl.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase)
            ? base.GetApiBaseUrl(repoUrl)
            : new Uri($"{repoUrl.Scheme}{Uri.SchemeDelimiter}{repoUrl.Authority}/api/v3/");

    /// <summary>Neuestes Release (Liste neueste zuerst) mit der Feed-Datei des Kanals; ohne eines leer.</summary>
    internal static GithubRelease[] NeuestesMitFeed(GithubRelease[] releases, string kanal)
    {
        var datei = "releases." + kanal + ".json";
        var mit = releases.Where(r => r.Assets.Any(a => string.Equals(a.Name, datei, StringComparison.OrdinalIgnoreCase)));
        return AlleFeedsFuerTests.Value ? [.. mit] : [.. mit.Take(1)];
    }

    private sealed class Melder(Action<string> melden)
    {
        public event Action<string>? Weiter;

        public void Melden(string text)
        {
            melden(text);
            Weiter?.Invoke(text);
        }
    }
}

/// <summary>Laden wie Velopack, jede Abfrage bei Stoerung bis zu 4 Mal (<see cref="Stoerung.WiederholenAsync{T}"/>).</summary>
public sealed class StoerungsfestLaden(Action<string> melden, TimeSpan? abrufZeit = null) : HttpClientFileDownloader
{
    // Velopack gibt die Zeitgrenze in Minuten an
    private double Zeit(double timeout) => abrufZeit is { } z ? z.TotalMinutes : timeout;

    public override Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers, double timeout, CancellationToken cancelToken = default) =>
        Stoerung.WiederholenAsync("Laden " + Kurz(url), async () => { await base.DownloadFile(url, targetFile, progress, headers, timeout, cancelToken).ConfigureAwait(false); return true; },
            melden, abbruch: cancelToken);

    public override Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers, double timeout) =>
        Stoerung.WiederholenAsync("Laden " + Kurz(url), () => base.DownloadBytes(url, headers, Zeit(timeout)), melden);

    public override Task<string> DownloadString(string url, IDictionary<string, string>? headers, double timeout) =>
        Stoerung.WiederholenAsync("Laden " + Kurz(url), () => base.DownloadString(url, headers, Zeit(timeout)), melden);

    private static string Kurz(string url) => url[(url.LastIndexOf('/') + 1)..];
}
