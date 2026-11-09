using System.Net;
using System.Text;

namespace Lernplattform.Tests;

/// <summary>
/// Kleiner GitHub-Ersatz auf 127.0.0.1 fuer die stoerungsfeste Update-Quelle (S 0.25.1, seit A 0.53.2 auch fuer die
/// Lernplattform; Datei in Stadtbau.Tests verlinkt). Velopack fragt bei fremden Adressen &lt;adresse&gt;/api/v3/repos/...
/// Je Dateiname eine Zahl von Stoerungen (Antwort <c>stoerung</c>, Vorgabe 504), danach die echte Antwort.
/// </summary>
public sealed class GithubTestserver : IDisposable
{
    private readonly HttpListener _hoerer = new();
    private readonly Dictionary<string, int> _stoerungen;
    public List<string> Abrufe { get; } = [];
    public string Adresse { get; }

    private readonly string[] _tags;
    private readonly string _kanal;
    private readonly string _paket;

    /// <param name="tags">Releases der Liste, neueste zuerst (Vorgabe: Lockvogel vA9.9.9 und drei vS).</param>
    public GithubTestserver(Dictionary<string, int> stoerungen, HttpStatusCode stoerung = HttpStatusCode.GatewayTimeout,
        string[]? tags = null, string kanal = "stadtbau-win", string paket = "Fachinformatiker-Stadtbau")
    {
        _tags = tags ?? ["vA9.9.9", "vS0.25.0", "vS0.24.0", "vS0.23.1"];
        _kanal = kanal;
        _paket = paket;
        _stoerungen = new(stoerungen, StringComparer.OrdinalIgnoreCase);
        var port = FreierPort();
        Adresse = "http://127.0.0.1:" + port + "/";
        _hoerer.Prefixes.Add(Adresse);
        _hoerer.Start();
        _ = Task.Run(async () =>
        {
            while (_hoerer.IsListening)
            {
                HttpListenerContext k;
                try
                {
                    k = await _hoerer.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }
                var pfad = k.Request.Url!.AbsolutePath;
                lock (Abrufe)
                {
                    Abrufe.Add(pfad);
                }
                var datei = pfad[(pfad.LastIndexOf('/') + 1)..];
                string? text = null;
                if (_stoerungen.TryGetValue(datei, out var rest) && rest > 0)
                {
                    _stoerungen[datei] = rest - 1;
                    k.Response.StatusCode = (int)stoerung;
                }
                else if (pfad.EndsWith("/releases", StringComparison.Ordinal))
                {
                    text = Liste();
                }
                else if (datei.StartsWith("feed_", StringComparison.Ordinal))
                {
                    text = Feed(datei["feed_".Length..^".json".Length]);
                }
                else
                {
                    k.Response.StatusCode = 404;
                }
                if (text is not null)
                {
                    var b = Encoding.UTF8.GetBytes(text);
                    k.Response.ContentType = "application/json";
                    await k.Response.OutputStream.WriteAsync(b);
                }
                k.Response.Close();
            }
        });
    }

    private string Liste()
    {
        var r = new StringBuilder("[");
        var tags = _tags;
        for (var i = 0; i < tags.Length; i++)
        {
            var feed = Adresse + "dl/feed_" + tags[i] + ".json";
            r.Append(i == 0 ? "" : ",").Append("{\"tag_name\":\"").Append(tags[i]).Append("\",\"name\":\"").Append(tags[i])
                .Append("\",\"draft\":false,\"prerelease\":false,\"published_at\":\"2026-11-0").Append(9 - i)
                .Append("T06:00:00Z\",\"assets\":[{\"name\":\"releases.").Append(_kanal).Append(".json\",\"url\":\"").Append(feed)
                .Append("\",\"browser_download_url\":\"").Append(feed).Append("\"}]}");
        }
        return r.Append(']').ToString();
    }

    // Paketversion wie beim Release: vS0.25.0 = 0.25.0, vA0.53.1 = 1.0.0-a.0.53.1
    private string Feed(string tag)
    {
        var version = tag.StartsWith("vS", StringComparison.Ordinal) ? tag[2..] : "1.0.0-" + char.ToLowerInvariant(tag[1]) + "." + tag[2..];
        return "{\"Assets\":[{\"PackageId\":\"" + _paket + "\",\"Version\":\"" + version + "\",\"Type\":\"Full\","
            + "\"FileName\":\"" + _paket + "-" + version + "-" + _kanal + "-full.nupkg\",\"SHA1\":\"00\",\"SHA256\":\"00\",\"Size\":1}]}";
    }

    private static int FreierPort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    public int Anzahl(string teil)
    {
        lock (Abrufe)
        {
            return Abrufe.Count(a => a.Contains(teil, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Dispose() => _hoerer.Close();
}
