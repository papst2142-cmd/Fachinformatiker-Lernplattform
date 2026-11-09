using Lernplattform.Core.Update;
using Lernplattform.Desktop;
using Stadtbau.Core;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Stadtbau.Desktop;

/// <summary>
/// Sammelliste 249 (Weisung P12-107): Update-Quelle des Werkzeugs am PC. Bis S 0.25.0 bekam <see cref="LinienQuelle"/>
/// nur "papst2142-cmd/Fachinformatiker-Lernplattform" (ohne https://github.com/); Velopacks GithubSource macht daraus
/// eine Uri und warf <see cref="UriFormatException"/>, die die Suche nicht fing, also kam nie ein Angebot. Die Datei
/// ist auch in Stadtbau.Tests verlinkt (Test mit Gegenprobe).
/// </summary>
internal static class Updatequelle
{
    /// <summary>Velopack-Kanal des Werkzeugs (die Lernplattform nutzt die Vorgabe "win").</summary>
    public const string Kanal = "stadtbau-win";

    /// <summary>Volle Adresse des Repos fuer Velopack (wie Lernplattform.Desktop.Updater.Repo).</summary>
    public static string RepositoryUrl => "https://github.com/" + Werkzeug.Repository;

    /// <summary>Nur fuer die Gegenprobe: die alte Angabe ohne https://github.com/.</summary>
    internal static readonly AsyncLocal<bool> AltFuerTests = new();

    /// <summary>
    /// Quelle mit Filter auf die Linie vS, nur das neueste Release mit Feed, jede Datei bei Stoerung bis zu 4 Mal
    /// (<see cref="StoerungsfesteQuelle"/>). Die Gegenprobe (<see cref="AltFuerTests"/>) baut die Quelle bis S 0.25.0.
    /// </summary>
    public static IUpdateSource Quelle(Action<string>? melden = null) =>
        AltFuerTests.Value
            ? new LinienQuelle(Werkzeug.Repository, ReleaseLinie.Linie.Stadtbau)
            : new StoerungsfesteQuelle(RepositoryUrl, ReleaseLinie.Linie.Stadtbau, Kanal, melden ?? (_ => { }));

    /// <summary>UpdateManager fuer das Werkzeug (Kanal stadtbau-win); Wiederholungen meldet <paramref name="melden"/>.</summary>
    public static UpdateManager Manager(Action<string>? melden = null) =>
        new(Quelle(melden), new UpdateOptions { ExplicitChannel = Kanal });

    /// <summary>Nur fuer die Gegenprobe der Tests: die Quelle laedt die Feeds aller Releases der Linie (wie GithubSource).</summary>
    internal static readonly AsyncLocal<bool> AlleFeedsFuerTests = new();

    /// <summary>
    /// Fund Lauf 709/712 (Nico 09.11.2026: viele 504 bei GitHub): Velopacks GithubSource laedt in GetReleaseFeed die
    /// Feed-Datei JEDES Releases nacheinander (heute 21 Releases vS, je rund 11 s bei github.com) und bricht beim ersten
    /// Fehler ganz ab; GitHub antwortete dabei auf rund jede dritte Datei mit 504. Diese Quelle nimmt (1) nur das neueste
    /// Release der Linie, das eine Feed-Datei fuer den Kanal hat (es nennt das volle Paket der neuesten Version), und
    /// (2) wiederholt jede einzelne Abfrage (Release-Liste, Feed, Paket) bei einer Stoerung (<see cref="Voruebergehend"/>)
    /// nach <see cref="Pausen"/>. Gleicher Filter auf die Linie wie <see cref="LinienQuelle"/> (nur gelesen, unveraendert).
    /// </summary>
    internal sealed class StoerungsfesteQuelle(string repo, ReleaseLinie.Linie linie, string kanal, Action<string> melden)
        : GithubSource(repo, null, false, new Laden(melden))
    {
        protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
        {
            var adresse = new Uri(GetApiBaseUrl(RepoUri), $"repos{RepoUri.AbsolutePath}/releases?per_page=30&page=1");
            var antwort = await Downloader.DownloadString(adresse.ToString(), GetRequestHeaders("application/vnd.github.v3+json"), 30).ConfigureAwait(false);
            return NeuestesMitFeed(LinienQuelle.Waehlen(antwort, linie, includePrereleases), kanal);
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
    }

    /// <summary>Laden wie Velopack, jede Abfrage bei Stoerung bis zu 4 Mal (<see cref="WiederholenAsync{T}"/>).</summary>
    internal sealed class Laden(Action<string> melden) : HttpClientFileDownloader
    {
        public override Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers, double timeout, CancellationToken cancelToken = default) =>
            WiederholenAsync("Laden " + Kurz(url), async () => { await base.DownloadFile(url, targetFile, progress, headers, timeout, cancelToken).ConfigureAwait(false); return true; }, melden);

        public override Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers, double timeout) =>
            WiederholenAsync("Laden " + Kurz(url), () => base.DownloadBytes(url, headers, timeout), melden);

        public override Task<string> DownloadString(string url, IDictionary<string, string>? headers, double timeout) =>
            WiederholenAsync("Laden " + Kurz(url), () => base.DownloadString(url, headers, timeout), melden);

        private static string Kurz(string url) => url[(url.LastIndexOf('/') + 1)..];
    }

    /// <summary>Wartezeiten zwischen den Versuchen bei Stoerungen von GitHub (insgesamt 4 Versuche).</summary>
    public static readonly TimeSpan[] Pausen = [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    /// <summary>Nur fuer Tests: Pausen fuer <see cref="WiederholenAsync{T}"/> ohne eigenes Warten (Laden ueber Velopack).</summary>
    internal static readonly AsyncLocal<bool> OhnePausenFuerTests = new();

    /// <summary>
    /// Stoerung bei GitHub statt Fehler im Programm (Nico 09.11.2026, viele 504 bei GitHub): Antwort 5xx (z. B. 504
    /// Gateway Time-out), keine Antwort (Netz, ohne Statuscode) oder Zeitueberschreitung, auch als innere Ausnahme.
    /// 4xx (z. B. 404, 403) und alle anderen Fehler sind keine Stoerung und werden nie wiederholt.
    /// </summary>
    public static bool Voruebergehend(Exception? x)
    {
        for (; x is not null; x = x.InnerException)
        {
            switch (x)
            {
                case HttpRequestException h when h.StatusCode is null || (int)h.StatusCode >= 500:
                case TaskCanceledException:
                case TimeoutException:
                    return true;
                case HttpRequestException:
                    return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Fuehrt einen Netz-Schritt aus und wiederholt ihn bei einer Stoerung von GitHub nach <see cref="Pausen"/>; jede
    /// Wiederholung meldet <paramref name="melden"/>. Nach dem letzten Versuch oder bei einem anderen Fehler geht die
    /// Ausnahme unveraendert weiter.
    /// </summary>
    public static T Wiederholen<T>(string schritt, Func<T> tun, Action<string> melden, Action<TimeSpan>? warten = null)
    {
        warten ??= Thread.Sleep;
        return WiederholenAsync(schritt, () => Task.FromResult(tun()), melden, p => { warten(p); return Task.CompletedTask; })
            .GetAwaiter().GetResult();
    }

    /// <summary>Wie <see cref="Wiederholen{T}"/>, fuer asynchrone Schritte (jede einzelne Abfrage beim Laden).</summary>
    public static async Task<T> WiederholenAsync<T>(string schritt, Func<Task<T>> tun, Action<string> melden, Func<TimeSpan, Task>? warten = null)
    {
        warten ??= p => OhnePausenFuerTests.Value ? Task.CompletedTask : Task.Delay(p);
        for (var versuch = 1; ; versuch++)
        {
            try
            {
                return await tun().ConfigureAwait(false);
            }
            catch (Exception x) when (versuch <= Pausen.Length && Voruebergehend(x))
            {
                var pause = Pausen[versuch - 1];
                melden("WIEDERHOLUNG " + schritt + " Versuch " + (versuch + 1) + " nach " + (int)pause.TotalSeconds
                    + " s (" + x.GetType().Name + ": " + x.Message + ")");
                await warten(pause).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Velopack-Protokoll in update.log des Datenordners (Info und hoeher, Ausnahmen mit Art und Text). Velopacks
    /// eigene Datei lag bis S 0.25.0 im Installationsordner und ging mit jeder Neuinstallation verloren.
    /// </summary>
    public sealed class Protokoll(string ordner) : IVelopackLogger
    {
        public void Log(VelopackLogLevel logLevel, string? message, Exception? exception)
        {
            if (logLevel < VelopackLogLevel.Information)
            {
                return;
            }
            var text = "Velopack " + logLevel + ": " + message
                + (exception is null ? "" : " (" + exception.GetType().Name + ": " + exception.Message + ")");
            Updateprotokoll.Schreiben(text, ordner);
        }
    }
}
