using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace Lernplattform.Core.Daten;

/// <summary>Eine Anfrage an GitHub wurde mit einem HTTP-Code abgelehnt (wie Pythons urllib HTTPError).</summary>
internal sealed class GitHubAbgelehnt(int code) : Exception("HTTP " + code.ToString(CultureInfo.InvariantCulture))
{
    public int Code { get; } = code;
}

/// <summary>
/// Phase 2, L10: Zugriff auf die GitHub-Schnittstelle wie fisi_sync._request (Python v0.63.2):
/// Kopfzeilen Authorization (Bearer), Accept, X-GitHub-Api-Version 2022-11-28, User-Agent;
/// 15 s Wartezeit je Schritt (Verbindung, Antwortkopf, jedes Stueck der Antwort); die Uhrzeit
/// von GitHub aus dem Date-Kopf jeder erfolgreichen Antwort. Fehler wie Python: HTTP-Code als
/// <see cref="GitHubAbgelehnt"/>, keine Verbindung (auch Zeitueberschreitung) und abgebrochene
/// Antwort als <see cref="AbgleichAusnahme"/>. Der Zugangsschluessel steht nur im Kopf der
/// Anfrage, nie in einem Fehlertext (Weisung P2-11 A7).
/// Weiterleitungen nur bei GET und nur auf denselben Server (Python folgt bei GET jeder;
/// ein fremder Server bekaeme sonst den Zugangsschluessel) - sonst gilt der HTTP-Code.
/// Die Adresse der Schnittstelle ist fest; nur Tests geben die Attrappe vor (A9).
/// </summary>
public sealed class GitHubZugang : IDisposable
{
    public const string Datei = "lernstand.json.gz";       // REMOTE_FILE
    public const int Wartezeit = 15;                       // TIMEOUT (Sekunden)
    private const string Kennung = "Fachinformatiker-Lernplattform";
    private const int HoechstWeiterleitungen = 10;         // urllib max_redirections

    private readonly HttpClient _client;
    private readonly TimeSpan _warten;

    public GitHubZugang() : this(new Uri("https://api.github.com"), TimeSpan.FromSeconds(Wartezeit))
    {
    }

    /// <summary>Nur fuer Tests: Attrappe auf 127.0.0.1 und kuerzere Wartezeit.</summary>
    internal GitHubZugang(Uri basis, TimeSpan warten)
    {
        Basis = basis;
        _warten = warten;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = warten,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            // Die Attrappe liegt auf diesem Rechner: nie ueber einen Proxy
            UseProxy = !basis.IsLoopback,
        };
        _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public Uri Basis { get; }

    /// <summary>Antwort einer erfolgreichen Anfrage: Inhalt und Uhrzeit von GitHub (Date-Kopf, sonst null).</summary>
    internal sealed record Antwort(byte[] Inhalt, DateTimeOffset? Datum);

    /// <summary>
    /// _request: Anfrage senden, Antwort ganz lesen. HTTP-Fehler: GitHubAbgelehnt. Keine Verbindung,
    /// Zeitueberschreitung: KeineVerbindung. Antwort brach ab: Unvollstaendig.
    /// </summary>
    internal Antwort Anfrage(HttpMethod methode, string pfad, string token, byte[]? koerper = null,
        string accept = "application/vnd.github+json") =>
        AnfrageAsync(methode, pfad, token, koerper, accept).GetAwaiter().GetResult();

    private async Task<Antwort> AnfrageAsync(HttpMethod methode, string pfad, string token, byte[]? koerper, string accept)
    {
        var ziel = new Uri(Basis, pfad);
        for (var weiter = 0; ; weiter++)
        {
            using var anfrage = new HttpRequestMessage(methode, ziel);
            anfrage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            anfrage.Headers.TryAddWithoutValidation("Accept", accept);
            anfrage.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            anfrage.Headers.TryAddWithoutValidation("User-Agent", Kennung);
            if (koerper is not null)
            {
                anfrage.Content = new ByteArrayContent(koerper);
                anfrage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            using var frist = new CancellationTokenSource(_warten);
            HttpResponseMessage antwort;
            try
            {
                antwort = await _client.SendAsync(anfrage, HttpCompletionOption.ResponseHeadersRead, frist.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
            {
                throw new AbgleichAusnahme(AbgleichFehler.KeineVerbindung);
            }
            using (antwort)
            {
                var code = (int)antwort.StatusCode;
                if (code is 301 or 302 or 303 or 307 or 308 && (methode == HttpMethod.Get || methode == HttpMethod.Head)
                    && weiter < HoechstWeiterleitungen && antwort.Headers.Location is { } ort)
                {
                    var neu = ort.IsAbsoluteUri ? ort : new Uri(ziel, ort);
                    if (neu.Scheme == Basis.Scheme && string.Equals(neu.Authority, Basis.Authority, StringComparison.OrdinalIgnoreCase))
                    {
                        ziel = neu;
                        continue;
                    }
                }
                if (code < 200 || code >= 300)
                {
                    throw new GitHubAbgelehnt(code);
                }
                var inhalt = await Lesen(antwort, frist).ConfigureAwait(false);
                return new Antwort(inhalt, antwort.Headers.Date);
            }
        }
    }

    /// <summary>Antwort ganz lesen; jedes Stueck hat die volle Wartezeit (wie der Socket-Timeout in Python).</summary>
    private async Task<byte[]> Lesen(HttpResponseMessage antwort, CancellationTokenSource frist)
    {
        try
        {
            await using var strom = await antwort.Content.ReadAsStreamAsync(frist.Token).ConfigureAwait(false);
            using var ziel = new MemoryStream();
            var puffer = new byte[64 * 1024];
            while (true)
            {
                frist.CancelAfter(_warten);
                var n = await strom.ReadAsync(puffer, frist.Token).ConfigureAwait(false);
                if (n == 0)
                {
                    return ziel.ToArray();
                }
                ziel.Write(puffer, 0, n);
            }
        }
        catch (HttpIOException e) when (e.HttpRequestError == HttpRequestError.ResponseEnded)
        {
            // IncompleteRead: Verbindung brach mitten in der Antwort ab
            throw new AbgleichAusnahme(AbgleichFehler.Unvollstaendig);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
        {
            throw new AbgleichAusnahme(AbgleichFehler.KeineVerbindung);
        }
    }

    public void Dispose() => _client.Dispose();
}
