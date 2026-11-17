using System.Net;
using System.Security.Cryptography;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Update;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.89.0 (Inhaltspakete W3, Weisung P12-297 W3-4): echtes Laden ueber die GitHub-Quelle mit der Release-Liste, die
/// <c>werkzeuge/pakete/paket_release.py github-probe</c> erzeugt (Text und Pruefsummen-Tabelle wie <c>paket_release.yml</c>, Datei
/// gebaut mit <c>paket_bauen.py</c>). Dieselbe Liste nutzen der Start-Test (PC) und die Emulator-Probe (Handy). Geprueft: nur
/// Pakete ohne Entwurf und Vorab, kein Programm-Release, Laden gegen den <c>digest</c> von GitHub, falsche und fehlende
/// Pruefsumme rot, nichts halb im Datenordner.
/// </summary>
public sealed class PaketGithubTests : IDisposable
{
    private const string App = "A 0.89.0";
    private const string Adresse = "http://paketprobe.test/releases";
    private static readonly string Echt = Path.Combine(Pfade.Wurzel, "tests/Lernplattform.Tests/Testdaten/pakete_echt");
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "paket_github_" + Guid.NewGuid().ToString("N"));

    public PaketGithubTests() => Directory.CreateDirectory(_tmp);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tmp, true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Die Probe-Liste mit der Adresse des Servers (wie der Start-Test und update_probe.sh sie ausliefern).</summary>
    internal static string Liste(string host) =>
        File.ReadAllText(Path.Combine(Echt, "github_releases.json")).Replace("http://PAKETPROBE", host, StringComparison.Ordinal);

    private sealed class Github(string? liste = null) : HttpMessageHandler
    {
        public List<string> Abfragen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pfad = request.RequestUri!.AbsolutePath;
            Abfragen.Add(pfad);
            if (pfad == "/releases")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(liste ?? Liste("http://paketprobe.test")) });
            }
            var datei = Path.Combine(Echt, Path.GetFileName(pfad));
            return Task.FromResult(File.Exists(datei) ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(datei)) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static string Abdruck(string ordner) => !Directory.Exists(ordner) ? "" : string.Join("\n",
        Directory.GetFileSystemEntries(ordner, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(p => Path.GetRelativePath(ordner, p)));

    [Fact]
    public async Task W3_4_ProbeListe_NurPakete_LadenGegenDigest_FalschRot()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        var github = new Github();
        var daten = Path.Combine(_tmp, "daten");
        var v = new PaketVerwaltung(new PaketAblage(daten), () => new GithubQuelle(new HttpClient(github), _ => { }, Adresse), App);
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        Assert.Null(v.SuchFehler);
        // Entwurf, Vorab-Release und Programm-Release stehen nie in der Liste
        Assert.Equal(["probe_kaputt", "probe_quests"], v.Angebote.Select(a => a.Kennung));
        var quests = v.Angebote.Single(a => a.Kennung == "probe_quests");
        var datei = File.ReadAllBytes(Path.Combine(Echt, "probe_quests_1.fipaket"));
        Assert.Equal(("Probe: Stadt mit Aufträgen", Paket.ArtStadt, (long)datei.Length, Convert.ToHexStringLower(SHA256.HashData(datei))),
            (quests.Name, quests.Art, quests.Groesse, quests.Sha256));
        Assert.Equal("Probe-Stadt für Tests: Stadt 1 (Fassung 2) mit zwei Aufträgen von Greta Sommer.", quests.Text);
        Assert.Null(await v.LadenAsync(quests, null, TestContext.Current.CancellationToken));
        Assert.Contains("/probe_quests_1.fipaket", github.Abfragen);
        var zeile = v.Zeilen().Single(z => z.Kennung == "probe_quests");
        Assert.Equal(PaketZustand.Geladen, zeile.Zustand);
        Assert.Single(v.Staedte(), s => s.Kopf.Kennung == "probe_quests");
        var vorher = Abdruck(daten);
        // falsche Pruefsumme: roter Grund, nichts geladen, keine Reste
        var kaputt = v.Angebote.Single(a => a.Kennung == "probe_kaputt");
        Assert.Equal(PaketTexte.Format(PaketTexte.Pruefsumme, "probe_kaputt_1.fipaket"), await v.LadenAsync(kaputt, null, TestContext.Current.CancellationToken));
        Assert.Equal(PaketZustand.Fehlerhaft, v.Zeilen().Single(z => z.Kennung == "probe_kaputt").Zustand);
        Assert.Equal(vorher, Abdruck(daten));
        // ohne digest nimmt die GitHub-Quelle nie an (Gegenprobe zur alten Regel „ohne digest ohne Pruefung“)
        Assert.Equal(PaketTexte.Format(PaketTexte.Pruefsumme, "probe_quests_1.fipaket"),
            await v.LadenAsync(quests with { Sha256 = null }, null, TestContext.Current.CancellationToken));
        Assert.Equal(vorher, Abdruck(daten));
    }

    [Fact]
    public void W3_4_Quelle_AdresseOderOrdner()
    {
        Assert.True(PaketQuelle.IstAdresse("http://127.0.0.1:8765/releases.json"));
        Assert.True(PaketQuelle.IstAdresse("https://api.github.com/repos/x/y/releases"));
        Assert.False(PaketQuelle.IstAdresse("/tmp/pakete"));
        Assert.False(PaketQuelle.IstAdresse("C:\\pakete"));
        Assert.False(PaketQuelle.IstAdresse("httpordner"));
    }

    /// <summary>Die Liste der Probe passt zum gebauten Paket (Groesse und Pruefsumme der echten Testdatei).</summary>
    [Fact]
    public void W3_4_ProbeListe_DigestGleichDerEchtenDatei()
    {
        var angebote = PaketQuelle.Auswerten(Liste("http://x"));
        foreach (var a in angebote.Where(a => a.Kennung != "probe_kaputt"))
        {
            var datei = File.ReadAllBytes(Path.Combine(Echt, Paket.Dateiname(a.Kennung, a.Stand)));
            Assert.Equal((Convert.ToHexStringLower(SHA256.HashData(datei)), (long)datei.Length), (a.Sha256, a.Groesse));
        }
    }
}
