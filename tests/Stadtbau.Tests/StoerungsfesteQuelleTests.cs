using System.Net;
using System.Text;
using Lernplattform.Core.Update;
using Lernplattform.Desktop;
using Stadtbau.Desktop;
using Lernplattform.Tests;
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
    private static string[] Versionen(GithubTestserver s, List<string> zeilen)
    {
        var quelle = new StoerungsfesteQuelle(s.Adresse + "papst2142-cmd/Fachinformatiker-Lernplattform",
            ReleaseLinie.Linie.Stadtbau, Updatequelle.Kanal, zeilen.Add);
        var feed = quelle.GetReleaseFeed(NullVelopackLogger.Instance, null, Updatequelle.Kanal).GetAwaiter().GetResult();
        return [.. feed.Assets.Select(a => a.Version.ToString())];
    }

    [Fact]
    public void NurNeuesterFeed_Und504WirdWiederholt()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        try
        {
            // Liste: ein Versuch scheitert (2 Abrufe), Feed: zwei Versuche scheitern (4 Abrufe)
            using var s = new GithubTestserver(new() { ["releases"] = 2, ["feed_vS0.25.0.json"] = 4 });
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
            Stoerung.OhnePausenFuerTests.Value = false;
        }
    }

    [Fact]
    public void Gegenprobe_AlleFeeds_WieVelopack_LaedtJedenFeed()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        StoerungsfesteQuelle.AlleFeedsFuerTests.Value = true;
        try
        {
            using var s = new GithubTestserver([]);
            Assert.Equal(["0.25.0", "0.24.0", "0.23.1"], Versionen(s, []));
            Assert.Equal(3, s.Anzahl("feed_vS"));
        }
        finally
        {
            StoerungsfesteQuelle.AlleFeedsFuerTests.Value = false;
            Stoerung.OhnePausenFuerTests.Value = false;
        }
    }

    [Fact]
    public void Gegenprobe_Stoerung404_NieWiederholt()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        try
        {
            using var s = new GithubTestserver(new() { ["feed_vS0.25.0.json"] = 5 }, HttpStatusCode.NotFound);
            var zeilen = new List<string>();
            Assert.ThrowsAny<HttpRequestException>(() => Versionen(s, zeilen));
            Assert.Equal(2, s.Anzahl("feed_vS0.25.0"));
            Assert.Empty(zeilen);
        }
        finally
        {
            Stoerung.OhnePausenFuerTests.Value = false;
        }
    }

    [Fact]
    public void Stoerung_DauerhaftNachVierVersuchen_FehlerBleibt()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        try
        {
            using var s = new GithubTestserver(new() { ["feed_vS0.25.0.json"] = 9 });
            var zeilen = new List<string>();
            var x = Assert.ThrowsAny<HttpRequestException>(() => Versionen(s, zeilen));
            Assert.Equal(HttpStatusCode.GatewayTimeout, x.StatusCode);
            Assert.Equal(8, s.Anzahl("feed_vS0.25.0"));
            Assert.Equal(3, zeilen.Count);
        }
        finally
        {
            Stoerung.OhnePausenFuerTests.Value = false;
        }
    }
}
