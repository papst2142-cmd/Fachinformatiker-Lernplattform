using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.39.0 (Weisung P11-39/P11-40, Plan P11-30): Update im Programm am Handy. Pruefungen vor der Uebergabe an
/// Android (Groesse, zwei Pruefsummen, Paketname, Versionscode, Zertifikat), Laden in den App-Speicher, Rueckmeldung
/// von Android. Jede Pruefung mit Gegenprobe (eine Angabe falsch: nichts installiert, Datei weg).
/// </summary>
[Collection("Update")]
public class HandyUpdateTests
{
    private const string Paket = "io.github.papst2142cmd.lernplattform";
    private const string Name = "Fachinformatiker-Lernplattform-A-0.39.0-android-arm64.apk";
    private static readonly byte[] Inhalt = Encoding.ASCII.GetBytes("keine echte APK, nur Testinhalt");
    private static readonly string Summe = Convert.ToHexStringLower(SHA256.HashData(Inhalt));

    [Fact]
    public void FingerabdruckGleichDerDateiImRepo()
    {
        var datei = File.ReadAllLines(Path.Combine(Wurzel(), "werkzeuge", "release", "android_fingerabdruck.txt"))
            .Where(z => !z.StartsWith('#')).Select(z => z.Trim()).Single(z => z.Length > 0);
        var programm = HandyPruefung.Fingerabdruck;
        Assert.Equal(datei, programm);
    }

    [Fact]
    public void PruefsummeAusDemReleaseText()
    {
        var text = "## Prüfsummen (SHA-256)\n\n| Datei | Größe (Bytes) | SHA-256 |\n|---|---:|---|\n"
            + "| `Andere.pkg` | 12 | `" + new string('a', 64) + "` |\n"
            + "| `" + Name + "` | 44073473 | `" + Summe.ToUpperInvariant() + "` |\r\n";
        Assert.Equal(Summe, HandyPruefung.AusReleaseText(text, Name));
        Assert.Null(HandyPruefung.AusReleaseText(text, "fehlt.apk"));
        Assert.Null(HandyPruefung.AusReleaseText(null, Name));
        // zwei Zeilen fuer denselben Namen: keine Angabe (nicht raten)
        Assert.Null(HandyPruefung.AusReleaseText(text + "| `" + Name + "` | 1 | `" + new string('b', 64) + "` |\n", Name));
        // 63 Zeichen sind keine Pruefsumme
        Assert.Null(HandyPruefung.AusReleaseText("| `" + Name + "` | 1 | `" + new string('c', 63) + "` |\n", Name));
    }

    [Fact]
    public void SucheLiestDigestUndTabelle()
    {
        var antwort = "{\"tag_name\": \"vA0.39.0\", \"body\": \"| `" + Name + "` | 31 | `" + Summe + "` |\", \"assets\": [{\"name\": \""
            + Name + "\", \"browser_download_url\": \"u\", \"size\": 31, \"digest\": \"sha256:" + Summe + "\"}]}";
        var angebot = GithubSuche.Auswerten(antwort, "A 0.38.0", "android")!;
        Assert.Equal("sha256:" + Summe, angebot.Pruefsumme);
        Assert.Equal(Summe, angebot.TabellenPruefsumme);
        Assert.Equal(Summe, HandyPruefung.Erwartet(angebot));
    }

    [Fact]
    public void BeidePruefsummenNoetigUndGleich()
    {
        var gut = Angebot();
        Assert.Equal(Summe, HandyPruefung.Erwartet(gut));
        Assert.Equal(UpdateTexte.KeinePruefsumme, Assert.Throws<UpdateFehler>(() => HandyPruefung.Erwartet(gut with { Pruefsumme = null })).Message);
        Assert.Equal(UpdateTexte.KeinePruefsumme, Assert.Throws<UpdateFehler>(() => HandyPruefung.Erwartet(gut with { TabellenPruefsumme = null })).Message);
        Assert.Equal(UpdateTexte.DownloadPruefsumme,
            Assert.Throws<UpdateFehler>(() => HandyPruefung.Erwartet(gut with { TabellenPruefsumme = new string('a', 64) })).Message);
    }

    [Fact]
    public void ApkPruefungMitGegenproben()
    {
        var fp = HandyPruefung.Fingerabdruck;
        Assert.Null(HandyPruefung.ApkPruefen(new ApkAngaben(Paket, 1003900, [fp]), Paket, 1003800));
        Assert.Null(HandyPruefung.ApkPruefen(new ApkAngaben(Paket, 1003900, [fp.ToUpperInvariant()]), Paket, 1003800));
        Assert.Equal(UpdateTexte.FremdeDatei, HandyPruefung.ApkPruefen(null, Paket, 1003800));
        // die Probe-APK ersetzt nie die echte App und umgekehrt
        Assert.Equal(UpdateTexte.FremdeDatei, HandyPruefung.ApkPruefen(new ApkAngaben(Paket + ".probe", 1003900, [fp]), Paket, 1003800));
        Assert.Equal(UpdateTexte.FremdeDatei, HandyPruefung.ApkPruefen(new ApkAngaben(Paket, 1003900, [fp]), Paket + ".probe", 1003800));
        Assert.Equal(UpdateTexte.FremdeDatei, HandyPruefung.ApkPruefen(new ApkAngaben(Paket, 1003900, [new string('a', 64)]), Paket, 1003800));
        Assert.Equal(UpdateTexte.FremdeDatei, HandyPruefung.ApkPruefen(new ApkAngaben(Paket, 1003900, []), Paket, 1003800));
        Assert.Equal(UpdateTexte.FremdeDatei, HandyPruefung.ApkPruefen(new ApkAngaben(Paket, 1003900, [fp, fp]), Paket, 1003800));
        Assert.Equal(UpdateTexte.NichtNeuer, HandyPruefung.ApkPruefen(new ApkAngaben(Paket, 1003800, [fp]), Paket, 1003800));
        Assert.Equal(UpdateTexte.NichtNeuer, HandyPruefung.ApkPruefen(new ApkAngaben(Paket, 1003799, [fp]), Paket, 1003800));
    }

    [Fact]
    public void RueckmeldungenVonAndroid()
    {
        Assert.Null(HandyPruefung.StatusText(HandyPruefung.StatusFertig, 1));
        Assert.Null(HandyPruefung.StatusText(HandyPruefung.StatusRueckfrage, 1));
        Assert.Equal(UpdateTexte.HandyAbgebrochen, HandyPruefung.StatusText(HandyPruefung.StatusAbgebrochen, 1));
        Assert.Equal(UpdateTexte.AndroidGesperrt, HandyPruefung.StatusText(HandyPruefung.StatusGesperrt, 1));
        Assert.Equal(UpdateTexte.FremdeDatei, HandyPruefung.StatusText(HandyPruefung.StatusKonflikt, 1));
        Assert.Equal(UpdateTexte.FremdeDatei, HandyPruefung.StatusText(HandyPruefung.StatusUnvertraeglich, 1));
        Assert.Equal(UpdateTexte.StartFehler, HandyPruefung.StatusText(HandyPruefung.StatusFehler, 1));
        Assert.Equal(UpdateTexte.StartFehler, HandyPruefung.StatusText(HandyPruefung.StatusUngueltig, 1));
        // 44 073 473 Bytes: doppelt plus 20 MB = 104,1 MB
        Assert.Equal("Auf dem Gerät ist nicht genug Speicher frei (nötig etwa 104,1 MB). Bitte Platz schaffen und erneut versuchen.",
            HandyPruefung.StatusText(HandyPruefung.StatusSpeicher, 44073473));
    }

    [Fact]
    public async Task LadenPrueftUndUebergibt()
    {
        using var temp = new TempOrdner();
        var geraet = new Geraet(temp.Pfad);
        var quelle = Quelle(geraet, temp.Pfad);
        var angebot = Angebot();
        var datei = await UpdateAblauf.LadenAsync(quelle, angebot, ordner: temp.Pfad, abbruch: TestContext.Current.CancellationToken);
        Assert.Equal(Path.Combine(geraet.LadeOrdner, Name), datei);
        Assert.Equal(Inhalt, await File.ReadAllBytesAsync(datei, TestContext.Current.CancellationToken));
        Assert.Equal([datei], geraet.Gelesen);
        // Installieren uebergibt genau diese Datei; update.log nennt Android
        UpdateAblauf.Installieren(quelle, angebot, new Einstellungen(temp.Pfad), temp.Pfad);
        Assert.Equal([datei], geraet.Installiert);
        Assert.Contains("Installer wird gestartet: Android, Version A 0.39.0, " + Name, Log(temp.Pfad), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OhnePlatzKeinDownload()
    {
        using var temp = new TempOrdner();
        var geraet = new Geraet(temp.Pfad) { Frei = HandyPruefung.Noetig(Inhalt.Length) - 1 };
        var anfragen = 0;
        var quelle = Quelle(geraet, temp.Pfad, () => anfragen++);
        var e = await Assert.ThrowsAsync<UpdateFehler>(() => UpdateAblauf.LadenAsync(quelle, Angebot(), ordner: temp.Pfad,
            abbruch: TestContext.Current.CancellationToken));
        Assert.StartsWith("Auf dem Gerät ist nicht genug Speicher frei (nötig etwa 20,0 MB)", e.Message, StringComparison.Ordinal);
        Assert.Equal(0, anfragen);
        // Gegenprobe: genau genug Platz laedt
        geraet.Frei = HandyPruefung.Noetig(Inhalt.Length);
        await UpdateAblauf.LadenAsync(quelle, Angebot(), ordner: temp.Pfad, abbruch: TestContext.Current.CancellationToken);
        Assert.Equal(1, anfragen);
    }

    [Fact]
    public async Task FalscheApkWirdGeloeschtUndNieUebergeben()
    {
        using var temp = new TempOrdner();
        foreach (var (apk, text) in new (ApkAngaben?, string)[]
        {
            (null, UpdateTexte.FremdeDatei),
            (new ApkAngaben(Paket + ".probe", 1003900, [HandyPruefung.Fingerabdruck]), UpdateTexte.FremdeDatei),
            (new ApkAngaben(Paket, 1003900, [new string('a', 64)]), UpdateTexte.FremdeDatei),
            (new ApkAngaben(Paket, 1003800, [HandyPruefung.Fingerabdruck]), UpdateTexte.NichtNeuer),
        })
        {
            var geraet = new Geraet(temp.Pfad) { Apk = apk };
            var quelle = Quelle(geraet, temp.Pfad);
            var e = await Assert.ThrowsAsync<UpdateFehler>(() => UpdateAblauf.LadenAsync(quelle, Angebot(), ordner: temp.Pfad,
                abbruch: TestContext.Current.CancellationToken));
            Assert.Equal(text, e.Message);
            Assert.Empty(Directory.GetFiles(geraet.LadeOrdner));
            Assert.Throws<FileNotFoundException>(() => quelle.Installieren(Angebot()));
            Assert.Empty(geraet.Installiert);
        }
    }

    [Fact]
    public async Task PruefsummeDerTabelleFalsch()
    {
        // digest und Tabelle gleich, aber die geladene Datei passt zu keinem: beschaedigt, Datei weg
        using var temp = new TempOrdner();
        var geraet = new Geraet(temp.Pfad);
        var anders = new string('d', 64);
        var e = await Assert.ThrowsAsync<UpdateFehler>(() => UpdateAblauf.LadenAsync(Quelle(geraet, temp.Pfad),
            Angebot() with { Pruefsumme = "sha256:" + anders, TabellenPruefsumme = anders }, ordner: temp.Pfad,
            abbruch: TestContext.Current.CancellationToken));
        Assert.Equal(UpdateTexte.DownloadPruefsumme, e.Message);
        Assert.Empty(Directory.GetFiles(geraet.LadeOrdner));
        Assert.Empty(geraet.Gelesen);
    }

    [Fact]
    public async Task FehlerVonAndroidGibtSperreUndMerkerFrei()
    {
        using var temp = new TempOrdner();
        var einstellungen = new Einstellungen(temp.Pfad);
        var geraet = new Geraet(temp.Pfad);
        Directory.CreateDirectory(geraet.LadeOrdner);
        File.WriteAllBytes(Path.Combine(geraet.LadeOrdner, Name), Inhalt);
        UpdateMerker.Merken(einstellungen, "A 0.39.0");
        UpdateAblauf.GehalteneSperre = Vorgaenge.Sperren();
        try
        {
            // Rueckfrage: nichts aendert sich (Android zeigt sein Fenster)
            Assert.Null(HandyQuelle.Rueckmeldung(HandyPruefung.StatusRueckfrage, null, Inhalt.Length, einstellungen, geraet.LadeOrdner, temp.Pfad));
            Assert.True(Vorgaenge.Belegt);
            Assert.NotNull(einstellungen.Laden()[UpdateMerker.Schluessel]);
            // abgebrochen: Text, Merker weg, Sperre frei, Datei weg, Zeile in update.log
            Assert.Equal(UpdateTexte.HandyAbgebrochen, HandyQuelle.Rueckmeldung(HandyPruefung.StatusAbgebrochen, "INSTALL_FAILED_ABORTED: User rejected\n/data/x",
                Inhalt.Length, einstellungen, geraet.LadeOrdner, temp.Pfad));
            // Vorgaenge gilt prozessweit: parallel laufende Tests anderer Sammlungen duerfen die Sperre kurz halten
            // (Lauf 501 sah dort ein Belegt). Beleg der Freigabe: die Sperre ist wieder zu bekommen; ohne
            // SperreFreigeben bliebe sie bis zum finally gehalten und Sperren() kaeme nie zurueck.
            Assert.Null(UpdateAblauf.GehalteneSperre);
            var wieder = Task.Run(Vorgaenge.Sperren, TestContext.Current.CancellationToken);
            (await wieder.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).Dispose();
            Assert.Null(einstellungen.Laden()[UpdateMerker.Schluessel]);
            Assert.Empty(Directory.GetFiles(geraet.LadeOrdner));
            var log = Log(temp.Pfad);
            Assert.Contains("Android meldet: abgebrochen (Status 3), INSTALL_FAILED_ABORTED: User rejected", log, StringComparison.Ordinal);
            Assert.DoesNotContain("/data/x", log, StringComparison.Ordinal);
        }
        finally
        {
            UpdateAblauf.SperreFreigeben();
        }
    }

    [Fact]
    public void NichtAbgeschlossenAmHandyMitEigenemTipp()
    {
        Assert.Contains(UpdateTexte.TippAndroid, UpdateMerker.Fehlschlag("A 0.39.0", "A 0.38.0", "android", "/x"), StringComparison.Ordinal);
        Assert.DoesNotContain(UpdateTexte.TippAndroid, UpdateMerker.Fehlschlag("A 0.39.0", "A 0.38.0", "win32", "/x"), StringComparison.Ordinal);
        Assert.Contains(UpdateTexte.TippWindows, UpdateMerker.Fehlschlag("A 0.39.0", "A 0.38.0", "win32", "/x"), StringComparison.Ordinal);
    }

    // ---- Hilfen -------------------------------------------------------------------------------

    private static UpdateAngebot Angebot() => new("A 0.39.0", "", "https://example.invalid/seite", Name, "https://example.invalid/app.apk",
        Inhalt.Length, "sha256:" + Summe, TabellenPruefsumme: Summe);

    private static HandyQuelle Quelle(Geraet geraet, string ordner, Action? anfrage = null) =>
        new(new HttpClient(new Attrappe(_ =>
        {
            anfrage?.Invoke();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Inhalt) };
        })), geraet, "A 0.38.0", datenordner: ordner);

    private static string Log(string ordner) => File.ReadAllText(Updateprotokoll.Pfad(ordner), Encoding.UTF8);

    private static string Wurzel()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "Directory.Build.props")))
        {
            ordner = ordner.Parent;
        }
        return ordner!.FullName;
    }

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
        public long EigenerVersionscode => 1003800;
        public ApkAngaben? Apk { get; init; } = new(Paket, 1003900, [HandyPruefung.Fingerabdruck]);
        public List<string> Gelesen { get; } = [];
        public List<string> Installiert { get; } = [];

        public ApkAngaben? ApkLesen(string pfad)
        {
            Gelesen.Add(pfad);
            return Apk;
        }

        public void Installieren(string pfad) => Installiert.Add(pfad);
    }
}
