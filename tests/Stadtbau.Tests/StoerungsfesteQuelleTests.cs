using System.Net;
using System.Text;
using Lernplattform.Core.Update;
using Stadtbau.Desktop;
using Velopack.Logging;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.25.1 (Lauf 709/712, Nico 09.11.2026: viele 504 bei GitHub): die Update-Quelle des Werkzeugs laedt nur den Feed des
/// neuesten Releases der Linie und wiederholt jede einzelne Abfrage bei einer Stoerung. Nachgestellt mit einem kleinen
/// Server auf 127.0.0.1 (Velopack fragt bei fremden Adressen &lt;adresse&gt;/api/v3/repos/...): erst 504, dann Erfolg.
/// Gegenproben: alle Feeds (wie Velopacks GithubSource) und 404 werden nicht wiederholt. Velopack fragt jede Adresse
/// bei einem Fehler selbst noch einmal klein geschrieben; ein Versuch sind darum zwei Abrufe.
/// </summary>
public class StoerungsfesteQuelleTests
{
    private sealed class Server : IDisposable
    {
        private readonly HttpListener _hoerer = new();
        private readonly Dictionary<string, int> _stoerungen;
        public List<string> Abrufe { get; } = [];
        public string Adresse { get; }

        public Server(Dictionary<string, int> stoerungen, HttpStatusCode stoerung = HttpStatusCode.GatewayTimeout)
        {
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
            string[] tags = ["vA9.9.9", "vS0.25.0", "vS0.24.0", "vS0.23.1"];
            for (var i = 0; i < tags.Length; i++)
            {
                var feed = Adresse + "dl/feed_" + tags[i] + ".json";
                r.Append(i == 0 ? "" : ",").Append("{\"tag_name\":\"").Append(tags[i]).Append("\",\"name\":\"").Append(tags[i])
                    .Append("\",\"draft\":false,\"prerelease\":false,\"published_at\":\"2026-11-0").Append(9 - i)
                    .Append("T06:00:00Z\",\"assets\":[{\"name\":\"releases.stadtbau-win.json\",\"url\":\"").Append(feed)
                    .Append("\",\"browser_download_url\":\"").Append(feed).Append("\"}]}");
            }
            return r.Append(']').ToString();
        }

        private static string Feed(string tag) =>
            "{\"Assets\":[{\"PackageId\":\"Fachinformatiker-Stadtbau\",\"Version\":\"" + tag[2..] + "\",\"Type\":\"Full\","
            + "\"FileName\":\"Fachinformatiker-Stadtbau-" + tag[2..] + "-stadtbau-win-full.nupkg\",\"SHA1\":\"00\",\"SHA256\":\"00\",\"Size\":1}]}";

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

    private static string[] Versionen(Server s, List<string> zeilen)
    {
        var quelle = new Updatequelle.StoerungsfesteQuelle(s.Adresse + "papst2142-cmd/Fachinformatiker-Lernplattform",
            ReleaseLinie.Linie.Stadtbau, Updatequelle.Kanal, zeilen.Add);
        var feed = quelle.GetReleaseFeed(NullVelopackLogger.Instance, null, Updatequelle.Kanal).GetAwaiter().GetResult();
        return [.. feed.Assets.Select(a => a.Version.ToString())];
    }

    [Fact]
    public void NurNeuesterFeed_Und504WirdWiederholt()
    {
        Updatequelle.OhnePausenFuerTests.Value = true;
        try
        {
            // Liste: ein Versuch scheitert (2 Abrufe), Feed: zwei Versuche scheitern (4 Abrufe)
            using var s = new Server(new() { ["releases"] = 2, ["feed_vS0.25.0.json"] = 4 });
            var zeilen = new List<string>();
            Assert.Equal(["0.25.0"], Versionen(s, zeilen));
            Assert.Equal(3, s.Anzahl("/releases"));
            Assert.Equal(5, s.Anzahl("feed_vS0.25.0"));
            Assert.Equal(0, s.Anzahl("feed_vS0.24.0"));
            Assert.Equal(0, s.Anzahl("vA9.9.9"));
            Assert.Equal(3, zeilen.Count);
            Assert.All(zeilen, z => Assert.Contains("504", z, StringComparison.Ordinal));
        }
        finally
        {
            Updatequelle.OhnePausenFuerTests.Value = false;
        }
    }

    [Fact]
    public void Gegenprobe_AlleFeeds_WieVelopack_LaedtJedenFeed()
    {
        Updatequelle.OhnePausenFuerTests.Value = true;
        Updatequelle.AlleFeedsFuerTests.Value = true;
        try
        {
            using var s = new Server([]);
            Assert.Equal(["0.25.0", "0.24.0", "0.23.1"], Versionen(s, []));
            Assert.Equal(3, s.Anzahl("feed_vS"));
        }
        finally
        {
            Updatequelle.AlleFeedsFuerTests.Value = false;
            Updatequelle.OhnePausenFuerTests.Value = false;
        }
    }

    [Fact]
    public void Gegenprobe_Stoerung404_NieWiederholt()
    {
        Updatequelle.OhnePausenFuerTests.Value = true;
        try
        {
            using var s = new Server(new() { ["feed_vS0.25.0.json"] = 5 }, HttpStatusCode.NotFound);
            var zeilen = new List<string>();
            Assert.ThrowsAny<HttpRequestException>(() => Versionen(s, zeilen));
            Assert.Equal(2, s.Anzahl("feed_vS0.25.0"));
            Assert.Empty(zeilen);
        }
        finally
        {
            Updatequelle.OhnePausenFuerTests.Value = false;
        }
    }

    [Fact]
    public void Stoerung_DauerhaftNachVierVersuchen_FehlerBleibt()
    {
        Updatequelle.OhnePausenFuerTests.Value = true;
        try
        {
            using var s = new Server(new() { ["feed_vS0.25.0.json"] = 9 });
            var zeilen = new List<string>();
            var x = Assert.ThrowsAny<HttpRequestException>(() => Versionen(s, zeilen));
            Assert.Equal(HttpStatusCode.GatewayTimeout, x.StatusCode);
            Assert.Equal(8, s.Anzahl("feed_vS0.25.0"));
            Assert.Equal(3, zeilen.Count);
        }
        finally
        {
            Updatequelle.OhnePausenFuerTests.Value = false;
        }
    }
}
