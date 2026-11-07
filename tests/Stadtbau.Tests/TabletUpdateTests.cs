using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.6.0 (Weisung P12-42 Punkt 2): das Werkzeug aktualisiert sich am Tablet selbst, mit denselben Pruefungen wie die
/// Lernplattform am Handy (A 0.39.0): SHA-256 aus zwei Quellen, Paketname, genau ein Zertifikat gleich
/// android_fingerabdruck.txt, hoeherer Versionscode, Platz. Nur Releases der Linie vS, nie ein Release der Lernplattform
/// (Gegenprobe: das neuere vA-Release in derselben Liste wird nie angeboten). Jede Pruefung mit Gegenprobe.
/// </summary>
public class TabletUpdateTests
{
    private const string Paket = "io.github.papst2142cmd.stadtbau";
    private const string Name = "Fachinformatiker-Stadtbau-S-0.6.0-android-arm64.apk";
    /// <summary>Installierte Version in den Tests (fest, damit die Tests nicht von der laufenden Version abhaengen).</summary>
    private const string Vorher = "S 0.5.1";
    private const string NameA = "Fachinformatiker-Lernplattform-A-0.99.0-android-arm64.apk";
    private static readonly byte[] Inhalt = Encoding.ASCII.GetBytes("keine echte APK, nur Testinhalt des Werkzeugs");
    private static readonly string Summe = Convert.ToHexStringLower(SHA256.HashData(Inhalt));

    private static string Release(string tag, string datei, bool entwurf = false, bool vorab = false, string? tabelle = null) =>
        "{\"tag_name\": \"" + tag + "\", \"draft\": " + (entwurf ? "true" : "false") + ", \"prerelease\": " + (vorab ? "true" : "false")
        + ", \"html_url\": \"https://github.com/x/releases/tag/" + tag + "\", \"body\": \"## Prüfsummen (SHA-256)\\n\\n| `" + datei + "` | "
        + Inhalt.Length + " | `" + (tabelle ?? Summe) + "` |\\n\", \"assets\": [{\"name\": \"" + datei + "\", \"browser_download_url\": \"https://dl/"
        + datei + "\", \"size\": " + Inhalt.Length + ", \"digest\": \"sha256:" + Summe + "\"}]}";

    /// <summary>Liste wie GET /releases: zuerst ein neueres Release der Lernplattform, dann Entwurf und Vorab-Release vS.</summary>
    private static string Liste(string? tabelle = null) => "[" + string.Join(", ",
        Release("vA0.99.0", NameA),
        Release("vS0.9.0", "Entwurf.apk", entwurf: true),
        Release("vS0.8.0", "Vorab.apk", vorab: true),
        Release("vS0.5.9", "Fachinformatiker-Stadtbau-S-0.5.9-android-arm64.apk"),
        Release("vS0.6.0", Name, tabelle: tabelle),
        Release("vS0.6.0-rc", "Kaputt.apk")) + "]";

    [Fact]
    public void Liste_NimmtHoechsteVersionDerLinieS_NieLernplattform()
    {
        var angebot = GithubSuche.AuswertenLinie(Liste(), "S 0.5.1", ReleaseLinie.Linie.Stadtbau, "android")!;
        Assert.Equal("S 0.6.0", angebot.Version);
        Assert.Equal(Name, angebot.DateiName);
        Assert.Equal("sha256:" + Summe, angebot.Pruefsumme);
        Assert.Equal(Summe, angebot.TabellenPruefsumme);
        Assert.Equal("https://github.com/x/releases/tag/vS0.6.0", angebot.Seite);
        // Gegenprobe: dieselbe Antwort als "neuestes Release" der Lernplattform waere das vA-Release
        Assert.Equal("A 0.99.0", GithubSuche.Auswerten(Release("vA0.99.0", NameA), "A 0.43.2", "android")!.Version);
        // nur Lernplattform-Releases in der Liste: nie ein Angebot fuer das Werkzeug
        Assert.Null(GithubSuche.AuswertenLinie("[" + Release("vA0.99.0", NameA) + ", " + Release("vB0.1.0", NameA) + ", " + Release("v1.0.0", NameA) + "]",
            "S 0.5.1", ReleaseLinie.Linie.Stadtbau, "android"));
        // und umgekehrt: die Lernplattform bekommt ein Werkzeug-Release nie (auch nicht als "latest")
        Assert.Null(GithubSuche.Auswerten(Release("vS9.0.0", Name), "A 0.43.2", "android"));
    }

    [Fact]
    public void Liste_GleicheOderAeltereVersion_KeinAngebot()
    {
        Assert.Null(GithubSuche.AuswertenLinie(Liste(), "S 0.6.0", ReleaseLinie.Linie.Stadtbau, "android"));
        Assert.Null(GithubSuche.AuswertenLinie(Liste(), "S 0.10.0", ReleaseLinie.Linie.Stadtbau, "android"));
        // Gegenprobe: eine Nummer kleiner bekommt S 0.6.0 (Vergleich nach Zahlen, nicht nach Text)
        Assert.Equal("S 0.6.0", GithubSuche.AuswertenLinie(Liste(), "S 0.5.10", ReleaseLinie.Linie.Stadtbau, "android")!.Version);
        // Entwurf und Vorab-Release zaehlen nie, auch wenn sie hoeher sind
        Assert.Null(GithubSuche.AuswertenLinie("[" + Release("vS0.9.0", Name, entwurf: true) + ", " + Release("vS0.8.0", Name, vorab: true) + "]",
            "S 0.5.1", ReleaseLinie.Linie.Stadtbau, "android"));
    }

    [Fact]
    public void Liste_FalscheAntwort_Fehler()
    {
        Assert.Equal(UpdateTexte.KeineVerbindung, Assert.Throws<UpdateFehler>(() =>
            GithubSuche.AuswertenLinie("kein json", "S 0.5.1", ReleaseLinie.Linie.Stadtbau, "android")).Message);
        Assert.Equal(UpdateTexte.Unerwartet, Assert.Throws<UpdateFehler>(() =>
            GithubSuche.AuswertenLinie(Release("vS0.6.0", Name), "S 0.5.1", ReleaseLinie.Linie.Stadtbau, "android")).Message);
        // die Lernplattform sucht nie ueber die Liste
        Assert.Throws<ArgumentException>(() => GithubSuche.AuswertenLinie(Liste(), "A 0.43.2", ReleaseLinie.Linie.Lernplattform, "android"));
    }

    [Theory]
    [InlineData("vS0.6.0", 0, 6, 0)]
    [InlineData("S 0.6.0", 0, 6, 0)]
    [InlineData("S0.10.2", 0, 10, 2)]
    public void Nummer(string text, int haupt, int neben, int fix) =>
        Assert.Equal((haupt, neben, fix), ReleaseLinie.StadtbauNummer(text));

    [Theory]
    [InlineData("vA0.6.0")]
    [InlineData("A 0.6.0")]
    [InlineData("vS0.6")]
    [InlineData("vS0.6.0-rc")]
    [InlineData("vS01.6.0")]
    [InlineData("vS0.6.99999999999")]
    [InlineData("")]
    public void Nummer_Ungueltig(string text) => Assert.Null(ReleaseLinie.StadtbauNummer(text));

    [Fact]
    public async Task Quelle_FragtDieListe_LernplattformWeiterLatest()
    {
        var gefragt = new List<string>();
        using var http = new HttpClient(new Attrappe(r =>
        {
            gefragt.Add(r.RequestUri!.ToString());
            return Antwort(r.RequestUri!.AbsolutePath.EndsWith("/latest", StringComparison.Ordinal) ? Release("vA0.99.0", NameA) : Liste());
        }));
        using var temp = new Temp();
        var werkzeug = TabletAktualisierung.Quelle(http, new Geraet(temp.Pfad), temp.Pfad, aktuell: Vorher);
        Assert.Equal(ReleaseLinie.Linie.Stadtbau, werkzeug.Linie);
        var angebot = await UpdateAblauf.SuchenAsync(werkzeug, TestContext.Current.CancellationToken);
        Assert.Equal("S 0.6.0", angebot!.Version);
        Assert.Equal([GithubSuche.ListeUrl], gefragt);
        Assert.DoesNotContain("latest", GithubSuche.ListeUrl, StringComparison.Ordinal);
        // Gegenprobe: die Quelle der Lernplattform (ohne Linie, wie in ihrer MainActivity) fragt wie bisher "latest"
        gefragt.Clear();
        var lernplattform = new HandyQuelle(http, new Geraet(temp.Pfad), "A 0.43.2");
        Assert.Equal(ReleaseLinie.Linie.Lernplattform, lernplattform.Linie);
        Assert.Equal("A 0.99.0", (await UpdateAblauf.SuchenAsync(lernplattform, TestContext.Current.CancellationToken))!.Version);
        Assert.Equal([GithubSuche.NeuesteUrl], gefragt);
    }

    [Fact]
    public async Task Ergebnis_FuerDieOberflaeche()
    {
        using var http = new HttpClient(new Attrappe(_ => Antwort(Liste())));
        using var temp = new Temp();
        var e = await UpdateSuche.SuchenAsync(async () => UpdateSuche.AusAngebot(await UpdateAblauf.SuchenAsync(TabletAktualisierung.Quelle(http, new Geraet(temp.Pfad), temp.Pfad, aktuell: Vorher))));
        Assert.Equal(UpdateArt.Neu, e.Art);
        Assert.Equal(Texte.UpdateNeuText("0.6.0"), UpdateSuche.Antwort(e).Text);
        Assert.Contains("(S 0.6.0)", UpdateSuche.Antwort(e).Text, StringComparison.Ordinal);
        Assert.Equal(UpdateArt.Aktuell, UpdateSuche.AusAngebot(null).Art);
        // Netzfehler des gemeinsamen Teils werden zum Ergebnis "Fehler" (das Fenster sagt es in einfachen Worten)
        using var kaputt = new HttpClient(new Attrappe(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var f = await UpdateSuche.SuchenAsync(async () => UpdateSuche.AusAngebot(await UpdateAblauf.SuchenAsync(TabletAktualisierung.Quelle(kaputt, new Geraet(temp.Pfad), temp.Pfad, aktuell: Vorher))));
        Assert.Equal(UpdateArt.Fehler, f.Art);
        Assert.Equal((Texte.UpdateSuchen, Texte.UpdateFehler), UpdateSuche.Antwort(f));
    }

    [Fact]
    public async Task Uebergeben_LaedtPrueftUndInstalliert()
    {
        using var temp = new Temp();
        var geraet = new Geraet(temp.Pfad);
        var (quelle, angebot) = await AngebotAsync(geraet, temp.Pfad);
        var fehler = new List<string>();
        Assert.Null(await TabletAktualisierung.UebergebenAsync(quelle, angebot, temp.Pfad, fehler.Add));
        var datei = Path.Combine(geraet.LadeOrdner, Name);
        Assert.Equal([datei], geraet.Gelesen);
        Assert.Equal([datei], geraet.Installiert);
        Assert.Empty(fehler);
        var log = await File.ReadAllTextAsync(Updateprotokoll.Pfad(temp.Pfad), TestContext.Current.CancellationToken);
        Assert.Contains("Download fertig: " + Name, log, StringComparison.Ordinal);
        Assert.Contains("Installer wird gestartet: Android, Version S 0.6.0, " + Name, log, StringComparison.Ordinal);
    }

    /// <summary>
    /// Selbst gefunden (Emulator-Probe Lauf 587): am frischen Tablet fehlt der Werkzeugordner bis zum ersten Projekt;
    /// update.log muss trotzdem entstehen (vorher schrieb das Protokoll still nichts).
    /// </summary>
    [Fact]
    public async Task Uebergeben_OhneWerkzeugordner_UpdateLogEntsteht()
    {
        using var temp = new Temp();
        var geraet = new Geraet(temp.Pfad);
        var (quelle, angebot) = await AngebotAsync(geraet, temp.Pfad);
        var frisch = Path.Combine(temp.Pfad, "frisch", "Fachinformatiker-Stadtbau");
        Assert.False(Directory.Exists(frisch));
        Assert.Null(await TabletAktualisierung.UebergebenAsync(quelle, angebot, frisch, _ => { }));
        var log = await File.ReadAllTextAsync(Updateprotokoll.Pfad(frisch), TestContext.Current.CancellationToken);
        Assert.Contains("Download fertig: " + Name, log, StringComparison.Ordinal);
        Assert.Contains("Installer läuft", log, StringComparison.Ordinal);
        var frisch2 = Path.Combine(temp.Pfad, "frisch2");
        Assert.Null(TabletAktualisierung.Rueckmeldung(HandyPruefung.StatusRueckfrage, null, Inhalt.Length, geraet.LadeOrdner, frisch2));
        Assert.True(File.Exists(Updateprotokoll.Pfad(frisch2)));
    }

    [Fact]
    public async Task Uebergeben_ApkDerLernplattform_NieInstalliert()
    {
        // Gegenprobe Paketname: gleiche Signatur, hoeherer Versionscode, aber die App der Lernplattform
        using var temp = new Temp();
        var geraet = new Geraet(temp.Pfad) { Apk = new ApkAngaben("io.github.papst2142cmd.lernplattform", 1009900, [HandyPruefung.Fingerabdruck]) };
        var (quelle, angebot) = await AngebotAsync(geraet, temp.Pfad);
        Assert.Equal(Texte.TabletFremdeDatei, await TabletAktualisierung.UebergebenAsync(quelle, angebot, temp.Pfad, _ => { }));
        Assert.Empty(geraet.Installiert);
        Assert.False(File.Exists(Path.Combine(geraet.LadeOrdner, Name)));
    }

    [Theory]
    [InlineData("fremd")]
    [InlineData("zwei")]
    [InlineData("alt")]
    [InlineData("gleich")]
    public async Task Uebergeben_ZertifikatOderVersionFalsch_NieInstalliert(string fall)
    {
        using var temp = new Temp();
        var fp = HandyPruefung.Fingerabdruck;
        var apk = fall switch
        {
            "fremd" => new ApkAngaben(Paket, 1000600, [new string('a', 64)]),
            "zwei" => new ApkAngaben(Paket, 1000600, [fp, fp]),
            "alt" => new ApkAngaben(Paket, 1000500, [fp]),
            _ => new ApkAngaben(Paket, 1000501, [fp]),
        };
        var geraet = new Geraet(temp.Pfad) { Apk = apk };
        var (quelle, angebot) = await AngebotAsync(geraet, temp.Pfad);
        var soll = fall is "fremd" or "zwei" ? Texte.TabletFremdeDatei : Texte.TabletNichtNeuer;
        Assert.Equal(soll, await TabletAktualisierung.UebergebenAsync(quelle, angebot, temp.Pfad, _ => { }));
        Assert.Empty(geraet.Installiert);
    }

    [Fact]
    public async Task Uebergeben_PruefsummeDerTabelleFehltOderFalsch()
    {
        using var temp = new Temp();
        var geraet = new Geraet(temp.Pfad);
        var (quelle, angebot) = await AngebotAsync(geraet, temp.Pfad);
        Assert.Equal(UpdateTexte.KeinePruefsumme, await TabletAktualisierung.UebergebenAsync(quelle, angebot with { TabellenPruefsumme = null }, temp.Pfad, _ => { }));
        Assert.Equal(UpdateTexte.KeinePruefsumme, await TabletAktualisierung.UebergebenAsync(quelle, angebot with { Pruefsumme = null }, temp.Pfad, _ => { }));
        var (quelle2, falsch) = await AngebotAsync(geraet, temp.Pfad, tabelle: new string('b', 64));
        Assert.Equal(UpdateTexte.DownloadPruefsumme, await TabletAktualisierung.UebergebenAsync(quelle2, falsch, temp.Pfad, _ => { }));
        Assert.Empty(geraet.Installiert);
        // Gegenprobe: mit beiden richtigen Pruefsummen geht es
        Assert.Null(await TabletAktualisierung.UebergebenAsync(quelle, angebot, temp.Pfad, _ => { }));
        Assert.Single(geraet.Installiert);
    }

    [Fact]
    public async Task Uebergeben_OhnePlatz_KeinDownload()
    {
        using var temp = new Temp();
        var geraet = new Geraet(temp.Pfad) { Frei = HandyPruefung.Noetig(Inhalt.Length) - 1 };
        var (quelle, angebot) = await AngebotAsync(geraet, temp.Pfad);
        var text = await TabletAktualisierung.UebergebenAsync(quelle, angebot, temp.Pfad, _ => { });
        Assert.StartsWith("Auf dem Gerät ist nicht genug Speicher frei", text, StringComparison.Ordinal);
        Assert.Empty(geraet.Gelesen);
        geraet.Frei = HandyPruefung.Noetig(Inhalt.Length);
        Assert.Null(await TabletAktualisierung.UebergebenAsync(quelle, angebot, temp.Pfad, _ => { }));
    }

    [Fact]
    public async Task Rueckmeldung_FehlerTextDesWerkzeugs_DateiWeg()
    {
        using var temp = new Temp();
        var geraet = new Geraet(temp.Pfad);
        var (quelle, angebot) = await AngebotAsync(geraet, temp.Pfad);
        Assert.Null(await TabletAktualisierung.UebergebenAsync(quelle, angebot, temp.Pfad, _ => { }));
        Assert.Null(TabletAktualisierung.Rueckmeldung(HandyPruefung.StatusRueckfrage, null, Inhalt.Length, geraet.LadeOrdner, temp.Pfad));
        Assert.True(File.Exists(Path.Combine(geraet.LadeOrdner, Name)));
        Assert.Equal(Texte.TabletAbgebrochen, TabletAktualisierung.Rueckmeldung(HandyPruefung.StatusAbgebrochen, "abgebrochen", Inhalt.Length, geraet.LadeOrdner, temp.Pfad));
        Assert.False(File.Exists(Path.Combine(geraet.LadeOrdner, Name)));
        Assert.Equal(Texte.TabletGesperrt, TabletAktualisierung.Rueckmeldung(HandyPruefung.StatusGesperrt, null, 1, geraet.LadeOrdner, temp.Pfad));
        Assert.Equal(Texte.TabletFremdeDatei, TabletAktualisierung.Rueckmeldung(HandyPruefung.StatusKonflikt, null, 1, geraet.LadeOrdner, temp.Pfad));
        Assert.Equal(UpdateTexte.StartFehler, TabletAktualisierung.Rueckmeldung(HandyPruefung.StatusFehler, null, 1, geraet.LadeOrdner, temp.Pfad));
        Assert.Contains("Android meldet: abgebrochen (Status 3), abgebrochen",
            await File.ReadAllTextAsync(Updateprotokoll.Pfad(temp.Pfad), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>Selbst gefunden (Foto des Angebots): jeder Satz steht im Fenster "Neue Version" nur einmal.</summary>
    [Fact]
    public void Angebot_JederSatzNurEinmal()
    {
        var text = Texte.UpdateNeuText("0.6.1") + " " + Texte.TabletJetztHinweis("80,1") + " " + Texte.TabletMobileDaten("80,1");
        var saetze = text.Split(". ", StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().TrimEnd('.')).ToList();
        Assert.Equal(saetze.Count, saetze.Distinct().Count());
        Assert.Single(saetze, t => t == "Deine Projekte bleiben erhalten");
    }

    [Fact]
    public void Texte_NieVonDerLernplattform()
    {
        // jeder Text, den der gemeinsame Teil am Tablet liefern kann (Suche, Laden, Pruefen, Rueckmeldung von Android)
        var moeglich = new List<string>
        {
            UpdateTexte.KeineVerbindung, UpdateTexte.Unerwartet, UpdateTexte.KeinInstaller, UpdateTexte.KeinePruefsumme,
            UpdateTexte.DownloadPruefsumme, UpdateTexte.DownloadUnvollstaendig, UpdateTexte.DownloadSpeichern, UpdateTexte.DownloadZeit,
            UpdateTexte.DownloadOffline, UpdateTexte.FremdeDatei, UpdateTexte.NichtNeuer, UpdateTexte.KeineErlaubnis,
            UpdateAblauf.Unerwartet(new InvalidOperationException()),
        };
        for (var s = -1; s <= 8; s++)
        {
            if (HandyPruefung.StatusText(s, 1) is { } t)
            {
                moeglich.Add(t);
            }
        }
        foreach (var t in moeglich)
        {
            var w = Texte.TabletText(t);
            Assert.DoesNotContain("Lernplattform", w, StringComparison.Ordinal);
            Assert.DoesNotContain("Lernstand", w, StringComparison.Ordinal);
        }
        // Gegenprobe: ohne Umsetzung stuenden dort Lernplattform und Lernstand
        Assert.Contains(moeglich, t => t.Contains("Lernplattform", StringComparison.Ordinal));
        Assert.Contains(moeglich, t => t.Contains("Lernstand", StringComparison.Ordinal));
        // die eigenen Texte des Werkzeugs auch nicht
        foreach (var feld in typeof(Texte).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.Name.StartsWith("Tablet", StringComparison.Ordinal)))
        {
            var t = (string)feld.GetValue(null)!;
            Assert.DoesNotContain("Lernplattform", t, StringComparison.Ordinal);
            Assert.DoesNotContain("Lernstand", t, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Fingerabdruck_WieDieLernplattform()
    {
        // die Werkzeug-APK traegt Nicos Release-Schluessel (baue_stadtbau.sh mit STADTBAU_RELEASE_STRENG=1)
        var wurzel = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(wurzel.FullName, "Directory.Build.props")))
        {
            wurzel = wurzel.Parent!;
        }
        var datei = File.ReadAllLines(Path.Combine(wurzel.FullName, "werkzeuge", "release", "android_fingerabdruck.txt"))
            .Where(z => !z.StartsWith('#')).Select(z => z.Trim()).Single(z => z.Length > 0);
        var programm = HandyPruefung.Fingerabdruck;
        Assert.Equal(datei, programm);
        Assert.Contains("STADTBAU_RELEASE_STRENG", File.ReadAllText(Path.Combine(wurzel.FullName, "werkzeuge", "stadtbau", "baue_stadtbau.sh")), StringComparison.Ordinal);
    }

    private static async Task<(HandyQuelle Quelle, UpdateAngebot Angebot)> AngebotAsync(Geraet geraet, string ordner, string? tabelle = null)
    {
        var http = new HttpClient(new Attrappe(r => r.RequestUri!.Host == "dl"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Inhalt) }
            : Antwort(Liste(tabelle))));
        var quelle = TabletAktualisierung.Quelle(http, geraet, ordner, aktuell: Vorher);
        return (quelle, (await UpdateAblauf.SuchenAsync(quelle))!);
    }

    private static HttpResponseMessage Antwort(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };

    private sealed class Attrappe(Func<HttpRequestMessage, HttpResponseMessage> antwort) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(antwort(request));
    }

    private sealed class Geraet(string ordner) : IHandyGeraet
    {
        public string LadeOrdner => Path.Combine(ordner, "cache", "update");
        public long Frei { get; set; } = long.MaxValue;
        public long FreierSpeicher => Frei;
        public bool? ImWlan => true;
        public bool DarfInstallieren => true;
        public Task<bool> ErlaubnisHolenAsync() => Task.FromResult(true);
        public string EigenesPaket => Paket;
        public long EigenerVersionscode => 1000501;
        public ApkAngaben? Apk { get; init; } = new(Paket, 1000600, [HandyPruefung.Fingerabdruck]);
        public List<string> Gelesen { get; } = [];
        public List<string> Installiert { get; } = [];

        public ApkAngaben? ApkLesen(string pfad)
        {
            Gelesen.Add(pfad);
            return Apk;
        }

        public void Installieren(string pfad) => Installiert.Add(pfad);
    }

    private sealed class Temp : IDisposable
    {
        public string Pfad { get; } = Directory.CreateTempSubdirectory("stadtbau-tablet-").FullName;

        public void Dispose()
        {
            try
            {
                Directory.Delete(Pfad, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
