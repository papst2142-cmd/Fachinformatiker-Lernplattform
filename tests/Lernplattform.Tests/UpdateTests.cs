using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 5 BE9 (Weisung P5-8): Update im Programm gegen G41 (tests/Vergleich/update.json, erzeugt aus
/// fisi_update.py v0.63.2 von werkzeuge/vergleich/erzeuge_update.py) und eigene Tabellen fuer die
/// C#-Linie (Versionen A &lt; B &lt; 1.0, Kopie vor dem Update, Merker, Ausschluss ueber Vorgaenge).
/// Abweichungen von Python stehen in der Wortliste W3: Release-Seite des neuen Repos statt der
/// Fisi-Seite, Grund immer REASON_UNKNOWN (Velopack hat kein Installer-Protokoll und kein .alt),
/// Tipp nach System statt nach .alt-Datei.
/// </summary>
[Collection("Update")]
public class UpdateTests
{
    private static readonly JsonElement G41 = VergleichsHilfe.Lade("update.json");

    private static readonly Regex Zeitstempel = new(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d \|", RegexOptions.Multiline);

    private static string Zeit => G41.GetProperty("zeit").GetString()!;

    private static string Text(string schluessel) => G41.GetProperty("texte").GetProperty(schluessel).GetString()!;

    // ---- G41: Texte ---------------------------------------------------------------------------

    [Fact]
    public void TexteWiePython()
    {
        Assert.Equal(UpdateTexte.Unerwartet, Text("UNEXPECTED_ANSWER"));
        Assert.Equal(UpdateTexte.KeineDatei, Text("NO_ASSET_TEXT"));
        Assert.Equal(UpdateTexte.DownloadHttp, Text("DOWNLOAD_HTTP"));
        Assert.Equal(UpdateTexte.DownloadZeit, Text("DOWNLOAD_TIMEOUT"));
        Assert.Equal(UpdateTexte.DownloadOffline, Text("DOWNLOAD_OFFLINE"));
        Assert.Equal(UpdateTexte.DownloadUnvollstaendig, Text("DOWNLOAD_INCOMPLETE"));
        Assert.Equal(UpdateTexte.DownloadSpeichern, Text("DOWNLOAD_SAVE"));
        Assert.Equal(UpdateTexte.DownloadPruefsumme, Text("DOWNLOAD_CHECKSUM"));
        Assert.Equal(UpdateTexte.FehlschlagTitel, Text("FAILED_TITLE"));
        Assert.Equal(UpdateTexte.FehlschlagText, Text("FAILED_TEXT"));
        Assert.Equal(UpdateTexte.TippWindows, Text("FAILED_TIP_WINDOWS"));
        Assert.Equal(UpdateTexte.TippAppImage, Text("FAILED_TIP_APPIMAGE"));
        Assert.Equal(UpdateTexte.GrundUnbekannt, Text("REASON_UNKNOWN"));
        Assert.Equal(UpdateNotizen.Ende, Text("NOTES_END"));
        Assert.Equal(UpdateMerker.Schluessel, Text("PENDING_KEY"));
        Assert.Equal(Problembericht.UpdateName, Text("UPDATE_LOG_NAME"));
        Assert.Equal(Updateprotokoll.Hoechstgroesse, G41.GetProperty("texte").GetProperty("UPDATE_LOG_MAX").GetInt32());
    }

    [Fact]
    public void NotizenWiePython()
    {
        var faelle = G41.GetProperty("notizen").EnumerateArray().ToList();
        Assert.True(faelle.Count >= 7);
        foreach (var fall in faelle)
        {
            Assert.Equal(fall[1].GetString(), UpdateNotizen.Lesbar(fall[0].GetString()));
        }
    }

    // ---- G41: Suche (Handy, GitHub-Antwort) ---------------------------------------------------

    [Fact]
    public async Task SucheWiePython()
    {
        var faelle = G41.GetProperty("suche").EnumerateArray().ToList();
        Assert.Equal(28, faelle.Count);
        var nummer = 0;
        foreach (var fall in faelle)
        {
            nummer++;
            var roh = fall.GetProperty("antwort");
            var antwort = roh.ValueKind == JsonValueKind.String ? roh.GetString()! : JsonNode.Parse(roh.GetRawText())!.ToJsonString();
            var aktuell = fall.GetProperty("aktuell").GetString()!;
            var soll = fall.GetProperty("ergebnis");
            UpdateAngebot? ist = null;
            string? fehler = null;
            try
            {
                ist = await SuchenMitAntwort(antwort, aktuell);
            }
            catch (UpdateFehler e)
            {
                fehler = e.Message;
            }
            if (soll.ValueKind == JsonValueKind.Null)
            {
                Assert.True(ist is null && fehler is null, $"Fall {nummer}: {antwort}");
                continue;
            }
            if (soll.TryGetProperty("fehler", out var sollFehler))
            {
                Assert.True(sollFehler.GetString() == fehler, $"Fall {nummer}: erwartet {sollFehler}, erhalten {fehler ?? ist?.ToString()}");
                continue;
            }
            Assert.True(fehler is null && ist is not null, $"Fall {nummer}: {fehler}");
            Assert.Equal(soll.GetProperty("version").GetString(), ist!.Version);
            Assert.Equal(soll.GetProperty("notes").GetString(), ist.Notizen);
            // Wortliste W3: Ersatz-Seite ist die Release-Seite der C#-Linie, nicht die der Python-Linie
            var seite = soll.GetProperty("page_url").GetString()!;
            Assert.Equal(seite.Contains("/Fisi-Lernplattform/", StringComparison.Ordinal) ? GithubSuche.ReleaseSeite : seite, ist.Seite);
            Assert.Equal(soll.GetProperty("asset_name").GetString(), ist.DateiName);
            Assert.Equal(soll.GetProperty("asset_url").GetString(), ist.DateiUrl);
            Assert.Equal(soll.GetProperty("asset_size").GetInt64(), ist.Groesse);
            Assert.Equal(soll.GetProperty("asset_digest").GetString(), ist.Pruefsumme);
        }
    }

    // Python-Fall {"http": 403} / {"netz": true} (Objekt statt Text) = Antwort der Attrappe, sonst der Text mit 200
    private static async Task<UpdateAngebot?> SuchenMitAntwort(string antwort, string aktuell)
    {
        var http = Regex.Match(antwort, @"^\{""http"":(\d+)\}$");
        using var client = new HttpClient(new Attrappe(_ =>
        {
            if (http.Success)
            {
                return new HttpResponseMessage((HttpStatusCode)int.Parse(http.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            if (Regex.IsMatch(antwort, @"^\{""netz"":true\}$"))
            {
                throw new HttpRequestException("keine Verbindung");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(antwort)) };
        }));
        return await GithubSuche.SuchenAsync(client, aktuell, "android", "http://127.0.0.1:9/releases/latest", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SucheSchicktKennungUndWartetHoechstensZehnSekunden()
    {
        HttpRequestMessage? gesehen = null;
        using var client = new HttpClient(new Attrappe(anfrage =>
        {
            gesehen = anfrage;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"tag_name\": \"vA0.1.0\", \"assets\": []}") };
        }));
        Assert.Null(await GithubSuche.SuchenAsync(client, "A 0.11.0", abbruch: TestContext.Current.CancellationToken));
        Assert.Equal(GithubSuche.NeuesteUrl, gesehen!.RequestUri!.ToString());
        Assert.Equal("api.github.com", gesehen.RequestUri.Host);
        Assert.Contains(GithubSuche.Kennung, gesehen.Headers.UserAgent.ToString(), StringComparison.Ordinal);
        Assert.Null(gesehen.Headers.Authorization);     // nie ein Token
        Assert.Equal(TimeSpan.FromSeconds(10), GithubSuche.Wartezeit);
    }

    [Fact]
    public void SucheCSharpTags()
    {
        // Tags der C#-Linie: vA0.11.0, vB0.1.0, ab 1.0 v1.0.0; Anzeige wie im Programm
        static string? Version(string tag, string aktuell) =>
            GithubSuche.Auswerten("{\"tag_name\": \"" + tag + "\", \"assets\": [{\"name\": \"a.apk\", \"browser_download_url\": \"u\", \"size\": 3}]}", aktuell, "android")?.Version;
        Assert.Equal("A 0.11.1", Version("vA0.11.1", "A 0.11.0"));
        Assert.Equal("B 0.1.0", Version("vB0.1.0", "A 0.11.0"));
        Assert.Equal("1.0.0", Version("v1.0.0", "B 0.9.9"));
        Assert.Null(Version("vA0.12.0", "B 0.1.0"));
        Assert.Null(Version("vB0.9.0", "1.0.0"));
        Assert.Null(Version("vA0.11.0", "A 0.11.0"));
        // PC: es gibt keine passende Datei (Velopack sucht selbst), nur die Seite
        Assert.Null(GithubSuche.Auswerten("{\"tag_name\": \"vA0.11.1\", \"assets\": [{\"name\": \"a.apk\"}]}", "A 0.11.0", "pc")!.DateiName);
    }

    // ---- eigene Tabelle: Versionen ------------------------------------------------------------

    [Theory]
    [InlineData("A 0.11.1", "A 0.11.0", true)]
    [InlineData("A 0.11.0", "A 0.11.0", false)]
    [InlineData("A 0.10.9", "A 0.11.0", false)]
    [InlineData("A 0.11.10", "A 0.11.9", true)]
    [InlineData("A 1.0.0", "A 0.99.99", true)]
    [InlineData("B 0.1.0", "A 0.99.0", true)]
    [InlineData("A 0.99.0", "B 0.1.0", false)]      // Kontrollfehler: B gilt nie als aelter als A
    [InlineData("B 0.0.0", "A 9.9.9", true)]
    [InlineData("1.0.0", "B 0.99.0", true)]
    [InlineData("B 0.99.0", "1.0.0", false)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0-a.0.11.1", "1.0.0-a.0.11.0", true)]
    [InlineData("1.0.0-b.0.1.0", "1.0.0-a.0.11.0", true)]
    [InlineData("1.0.0-a.0.11.0", "1.0.0-b.0.1.0", false)]
    [InlineData("1.0.0", "1.0.0-b.0.1.0", true)]
    [InlineData("vA0.11.1", "A 0.11.0", true)]
    [InlineData("vB0.1.0", "1.0.0-a.0.11.0", true)]
    [InlineData("A 0.11", "A 0.10.5", true)]
    [InlineData("A 0.11", "A 0.11.0", false)]
    [InlineData("kaputt", "A 0.11.0", false)]
    [InlineData("A 0.11.1", "kaputt", false)]
    [InlineData("", "A 0.11.0", false)]
    [InlineData("A ٠.١١.١", "A 0.11.0", false)]     // nur Ziffern 0-9
    [InlineData("C 0.11.1", "A 0.11.0", false)]
    [InlineData("A 0.11.1.1", "A 0.11.0", false)]
    public void VersionsreiheAvorBvorEins(string kandidat, string aktuell, bool neuer) =>
        Assert.Equal(neuer, UpdateVersion.IstNeuer(kandidat, aktuell));

    [Fact]
    public void VersionAnzeigeUndPaket()
    {
        Assert.Equal("A 0.11.0", UpdateVersion.Lesen("1.0.0-a.0.11.0")!.Anzeige);
        Assert.Equal("1.0.0-a.0.11.0", UpdateVersion.Lesen("A 0.11.0")!.Paket);
        Assert.Equal("1.0.0-b.0.2.3", UpdateVersion.Lesen("vB0.2.3")!.Paket);
        Assert.Equal("1.0.0", UpdateVersion.Lesen("v1.0.0")!.Paket);
        Assert.Equal("B 0.2.0", UpdateVersion.Lesen(" B 0.2 ")!.Anzeige);
    }

    // ---- G41: update.log schreiben und lesen ----------------------------------------------------

    private static string Baustein(string angabe)
    {
        var lang = string.Concat(Enumerable.Range(0, 3000).Select(i => $"Zeile {i:00000} mit Text äöü\n"));
        return angabe switch
        {
            "{lang}" => lang,
            "{emoji}" => lang.Replace("Zeile 01500", "Zeile \U0001F600\U0001F600", StringComparison.Ordinal),
            "{x70k}" => new string('x', 70 * 1024),
            "{crlf}" => string.Concat(Enumerable.Repeat("a\r\nb\rc\n", 10000)),
            _ => angabe,
        };
    }

    // Inhalt so, wie Python ihn im Textmodus sieht (unter Windows schreiben beide \r\n)
    private static string LogLesen(string ordner)
    {
        var pfad = Updateprotokoll.Pfad(ordner);
        return File.Exists(pfad) ? File.ReadAllText(pfad, Encoding.UTF8).Replace(Environment.NewLine, "\n", StringComparison.Ordinal) : "";
    }

    private static void Vergleichen(JsonElement soll, string ist, string fall)
    {
        if (soll.ValueKind == JsonValueKind.String)
        {
            Assert.True(soll.GetString() == ist, $"{fall}: erwartet {soll.GetString()}, erhalten {ist}");
            return;
        }
        var punkte = ist.EnumerateRunes().Count();
        Assert.True(soll.GetProperty("zeichen").GetInt32() == punkte, $"{fall}: {punkte} Zeichen");
        Assert.EndsWith(soll.GetProperty("ende").GetString()!, ist, StringComparison.Ordinal);
        Assert.Equal(soll.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ist))));
    }

    [Fact]
    public void ProtokollWiePython()
    {
        var jetzt = DateTime.Parse(Zeit, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var fall in G41.GetProperty("protokoll").EnumerateArray())
        {
            using var temp = new TempOrdner();
            if (fall.GetProperty("vorher").GetString() is { } vorher)
            {
                File.WriteAllBytes(Updateprotokoll.Pfad(temp.Pfad), Encoding.UTF8.GetBytes(Baustein(vorher)));
            }
            foreach (var zeile in fall.GetProperty("zeilen").EnumerateArray())
            {
                Updateprotokoll.Schreiben(zeile.GetString()!, temp.Pfad, jetzt);
            }
            Vergleichen(fall.GetProperty("nachher"), LogLesen(temp.Pfad), fall.GetProperty("name").GetString()!);
        }
    }

    [Fact]
    public void ProtokollLesenWiePython()
    {
        foreach (var fall in G41.GetProperty("lesen").EnumerateArray())
        {
            using var temp = new TempOrdner();
            var pfad = Path.Combine(temp.Pfad, "update.log");
            if (fall[0].GetString() is { } inhalt)
            {
                File.WriteAllBytes(pfad, Encoding.UTF8.GetBytes(Baustein(inhalt)));
            }
            Vergleichen(fall[2], Problembericht.UpdateLogLesen(pfad, fall[1].GetInt32()), fall[0].ToString());
        }
    }

    [Fact]
    public void ProtokollOhneBenutzerpfadeUndNieEinFehler()
    {
        using var temp = new TempOrdner();
        var heim = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Updateprotokoll.Schreiben("Datei " + Path.Combine(heim, "geheim", "x.db"), temp.Pfad);
        Assert.DoesNotContain(heim, LogLesen(temp.Pfad), StringComparison.Ordinal);
        // Ordner gibt es nicht: still nichts
        Updateprotokoll.Schreiben("x", Path.Combine(temp.Pfad, "fehlt", "tiefer"));
        Assert.False(Directory.Exists(Path.Combine(temp.Pfad, "fehlt")));
    }

    // ---- G41: Download pruefen ----------------------------------------------------------------

    private static string Zeitlos(string log) => Zeitstempel.Replace(log, "ZEIT |");

    [Fact]
    public async Task DownloadWiePython()
    {
        var faelle = G41.GetProperty("download").EnumerateArray().ToList();
        Assert.Equal(7, faelle.Count);
        foreach (var fall in faelle)
        {
            using var temp = new TempOrdner();
            var name = fall.GetProperty("name").GetString()!;
            var quelle = new Quelle { Inhalt = Encoding.UTF8.GetBytes(fall.GetProperty("inhalt").GetString()!), Ordner = temp.Pfad };
            var angebot = new UpdateAngebot("0.11.1", "", GithubSuche.ReleaseSeite, "Paket.nupkg", "Paket.nupkg",
                fall.GetProperty("groesse").GetInt64(), fall.GetProperty("digest").GetString());
            string? fehler = null;
            string? datei = null;
            try
            {
                datei = await UpdateAblauf.LadenAsync(quelle, angebot, ordner: temp.Pfad, abbruch: TestContext.Current.CancellationToken);
            }
            catch (UpdateFehler e)
            {
                fehler = e.Message;
            }
            Assert.True(fall.GetProperty("fehler").GetString() == fehler, $"{name}: {fehler}");
            Assert.Equal(Zeitlos(fall.GetProperty("log").GetString()!), Zeitlos(LogLesen(temp.Pfad)));
            // beschaedigte oder unvollstaendige Datei bleibt nicht liegen
            Assert.Equal(fehler is null, File.Exists(quelle.Datei));
            Assert.Equal(fehler is null ? quelle.Datei : null, datei);
        }
    }

    [Fact]
    public async Task DownloadOhnePruefsummeNurMitZeileImProtokoll()
    {
        // Kontrollfehler "Update ohne Pruefsumme": ohne sha256 wird geladen, aber das Protokoll sagt es;
        // eine falsche Pruefsumme laesst die Datei nie durch.
        using var temp = new TempOrdner();
        var quelle = new Quelle { Inhalt = [1, 2, 3], Ordner = temp.Pfad };
        await UpdateAblauf.LadenAsync(quelle, new UpdateAngebot("A 0.11.1", "", "s", "p.nupkg", "p", 3, null), ordner: temp.Pfad, abbruch: TestContext.Current.CancellationToken);
        Assert.Contains("GitHub nennt keine Prüfsumme für p.nupkg", LogLesen(temp.Pfad), StringComparison.Ordinal);
        var falsch = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 4 }));
        var e = await Assert.ThrowsAsync<UpdateFehler>(() => UpdateAblauf.LadenAsync(quelle, new UpdateAngebot("A 0.11.1", "", "s", "p.nupkg", "p", 3, falsch), ordner: temp.Pfad, abbruch: TestContext.Current.CancellationToken));
        Assert.Equal(UpdateTexte.DownloadPruefsumme, e.Message);
        Assert.False(File.Exists(quelle.Datei));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 3 })),
            UpdateAblauf.ErwartetSha256("SHA256:  " + Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })) + " \n"));
        Assert.Null(UpdateAblauf.ErwartetSha256(Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 3 }))));
        Assert.Null(UpdateAblauf.ErwartetSha256("sha256:" + new string('İ', 64)));
    }

    [Fact]
    public async Task DownloadFehlerWerdenDeutscheTexte()
    {
        using var temp = new TempOrdner();
        foreach (var (fehler, text) in new (Exception, string)[]
        {
            (new HttpRequestException("x", null, HttpStatusCode.NotFound), UpdateTexte.DownloadHttp.Replace("%d", "404", StringComparison.Ordinal)),
            (new TaskCanceledException(), UpdateTexte.DownloadZeit),
            (new HttpRequestException(HttpRequestError.ResponseEnded, "x"), UpdateTexte.DownloadUnvollstaendig),
            (new HttpRequestException("x"), UpdateTexte.DownloadOffline),
            (new IOException("Platte voll /home/nico"), UpdateTexte.DownloadSpeichern),
            (new InvalidOperationException("intern"), "Unerwarteter Fehler: InvalidOperationException"),
        })
        {
            var quelle = new Quelle { Fehler = fehler, Ordner = temp.Pfad };
            var e = await Assert.ThrowsAsync<UpdateFehler>(() => UpdateAblauf.LadenAsync(quelle, new UpdateAngebot("A 0.11.1", "", "s", "p", "p"), ordner: temp.Pfad, abbruch: TestContext.Current.CancellationToken));
            Assert.Equal(text, e.Message);
        }
        Assert.DoesNotContain("/home/nico", LogLesen(temp.Pfad), StringComparison.Ordinal);
        var ohne = await Assert.ThrowsAsync<UpdateFehler>(() => UpdateAblauf.LadenAsync(new Quelle(), new UpdateAngebot("A 0.11.1", "", "s"), ordner: temp.Pfad, abbruch: TestContext.Current.CancellationToken));
        Assert.Equal(UpdateTexte.KeinInstaller, ohne.Message);
        var suche = await Assert.ThrowsAsync<UpdateFehler>(() => UpdateAblauf.SuchenAsync(new Quelle { Fehler = new FormatException("x") }, TestContext.Current.CancellationToken));
        Assert.Equal("Unerwarteter Fehler: FormatException", suche.Message);
    }

    // ---- G41: Merker und Pruefung beim Start ----------------------------------------------------

    [Fact]
    public void MerkerWiePython()
    {
        using var temp = new TempOrdner();
        File.WriteAllText(Path.Combine(temp.Pfad, Einstellungen.DateiName), "{\"auto_check\": true, \"tagesziel\": 20}");
        Assert.True(UpdateMerker.Merken(new Einstellungen(temp.Pfad), "0.11.1", DateTime.Parse(Zeit, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(G41.GetProperty("merker").GetRawText()),
            JsonNode.Parse(File.ReadAllText(Path.Combine(temp.Pfad, Einstellungen.DateiName)))));
    }

    [Fact]
    public void AbschlussWiePython()
    {
        var faelle = G41.GetProperty("abschluss").EnumerateArray().ToList();
        Assert.Equal(10, faelle.Count);
        foreach (var fall in faelle)
        {
            using var temp = new TempOrdner();
            var name = fall.GetProperty("name").GetString()!;
            var werte = new JsonObject { ["auto_check"] = true };
            if (fall.GetProperty("merker").ValueKind != JsonValueKind.Null)
            {
                werte[UpdateMerker.Schluessel] = JsonNode.Parse(fall.GetProperty("merker").GetRawText());
            }
            File.WriteAllText(Path.Combine(temp.Pfad, Einstellungen.DateiName), werte.ToJsonString());
            // Wortliste W3: der Tipp richtet sich nach dem System (Linux = AppImage), der Grund ist immer REASON_UNKNOWN
            var appImage = fall.GetProperty("merker") is { ValueKind: JsonValueKind.Object } m && m.TryGetProperty("alt", out _);
            var ergebnis = UpdateMerker.Abschliessen(new Einstellungen(temp.Pfad), fall.GetProperty("aktuell").GetString()!, appImage ? "linux" : "windows", temp.Pfad);
            var soll = fall.GetProperty("ergebnis");
            if (soll.ValueKind == JsonValueKind.Null)
            {
                Assert.True(ergebnis is null, name);
            }
            else
            {
                Assert.True(ergebnis is not null, name);
                Assert.Equal(soll[0].GetString() == "ok", ergebnis!.Ok);
                var text = soll[1].GetString()!
                    .Replace(Text("REASON_ROLLBACK"), Text("REASON_UNKNOWN"), StringComparison.Ordinal)
                    .Replace(Text("REASON_APPIMAGE"), Text("REASON_UNKNOWN"), StringComparison.Ordinal)
                    .Replace("{PFAD}", Updateprotokoll.Pfad(temp.Pfad), StringComparison.Ordinal);
                Assert.Equal(text, ergebnis.Text);
            }
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fall.GetProperty("einstellungen").GetRawText()),
                JsonNode.Parse(File.ReadAllText(Path.Combine(temp.Pfad, Einstellungen.DateiName)))), name);
            var log = fall.GetProperty("log").GetString() ?? "";
            Assert.Equal(Zeitlos(log), Zeitlos(LogLesen(temp.Pfad)));
        }
    }

    [Fact]
    public void FehlschlagKommtGenauEinmal()
    {
        // Kontrollfehler "zweite Meldung nach Neustart" und "Fehlschlag nicht gemeldet"
        using var temp = new TempOrdner();
        var einstellungen = new Einstellungen(temp.Pfad);
        Assert.True(UpdateMerker.Merken(einstellungen, "A 0.11.1"));
        var erster = UpdateMerker.Abschliessen(new Einstellungen(temp.Pfad), "A 0.11.0", "windows", temp.Pfad);
        Assert.NotNull(erster);
        Assert.False(erster.Ok);
        Assert.StartsWith("Das Update auf Version A 0.11.1 wurde nicht installiert. Installiert ist weiterhin Version A 0.11.0.", erster.Text, StringComparison.Ordinal);
        Assert.Null(UpdateMerker.Abschliessen(new Einstellungen(temp.Pfad), "A 0.11.0", "windows", temp.Pfad));
        Assert.True(UpdateMerker.Merken(einstellungen, "A 0.11.1"));
        Assert.Equal(new UpdateAbschluss(true, "A 0.11.1"), UpdateMerker.Abschliessen(new Einstellungen(temp.Pfad), "A 0.11.1", "windows", temp.Pfad));
        Assert.Null(UpdateMerker.Abschliessen(new Einstellungen(temp.Pfad), "A 0.11.1", "windows", temp.Pfad));
        // B nach A: angekommen; A-Ziel bei laufender B: gilt als angekommen (nie Rueckschritt melden)
        Assert.True(UpdateMerker.Merken(einstellungen, "B 0.1.0"));
        Assert.False(UpdateMerker.Abschliessen(new Einstellungen(temp.Pfad), "A 0.99.0", "windows", temp.Pfad)!.Ok);
        Assert.True(UpdateMerker.Merken(einstellungen, "A 0.99.0"));
        Assert.True(UpdateMerker.Abschliessen(new Einstellungen(temp.Pfad), "B 0.1.0", "windows", temp.Pfad)!.Ok);
    }

    // ---- Installieren: Kopie, Merker, Ausschluss ------------------------------------------------

    private static readonly Uhr FesteUhr = new(() => new DateTimeOffset(2026, 10, 23, 10, 34, 56, TimeSpan.Zero), TimeZoneInfo.Utc);

    private static string LernstandAnlegen(string ordner)
    {
        var pfad = Path.Combine(ordner, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        using var db = VergleichsHilfe.Oeffnen(pfad);
        VergleichsHilfe.Ausfuehren(db, "CREATE TABLE probe (v TEXT)");
        VergleichsHilfe.Ausfuehren(db, "INSERT INTO probe VALUES ('vor dem Update')");
        return pfad;
    }

    [Fact]
    public void InstallierenLegtVorherEineKopieAnUndSetztDenMerker()
    {
        using var temp = new TempOrdner();
        LernstandAnlegen(temp.Pfad);
        var einstellungen = new Einstellungen(temp.Pfad);
        var quelle = new Quelle { Ordner = temp.Pfad };
        quelle.BeimInstallieren = () =>
        {
            // im Augenblick des Starts: Kopie fertig und lesbar, Merker gespeichert
            var kopien = Directory.GetFiles(Path.Combine(temp.Pfad, Uebernahme.SicherungsOrdner), "vor_update_*.db");
            Assert.Equal([Path.Combine(temp.Pfad, Uebernahme.SicherungsOrdner, "vor_update_2026-10-23_103456.db")], kopien);
            using var kopie = VergleichsHilfe.Oeffnen(kopien[0]);
            Assert.Equal("vor dem Update", VergleichsHilfe.Abfrage(kopie, "SELECT v FROM probe")[0][0]);
            Assert.Equal("A 0.11.1", new Einstellungen(temp.Pfad).Laden()[UpdateMerker.Schluessel]!["version"]!.GetValue<string>());
        };
        var angebot = new UpdateAngebot("A 0.11.1", "", "s", "p.nupkg");
        Assert.Equal(UpdateTexte.Installiert, UpdateAblauf.Installieren(quelle, angebot, einstellungen, temp.Pfad, FesteUhr));
        Assert.Equal(1, quelle.Installationen);
        // zweite Kopie in derselben Sekunde ueberschreibt nie
        Assert.Equal(UpdateTexte.Installiert, UpdateAblauf.Installieren(new Quelle(), angebot, einstellungen, temp.Pfad, FesteUhr));
        Assert.True(File.Exists(Path.Combine(temp.Pfad, Uebernahme.SicherungsOrdner, "vor_update_2026-10-23_103456_2.db")));
        var log = LogLesen(temp.Pfad);
        Assert.Contains("| Programm | Kopie des Lernstands: sicherungen/vor_update_2026-10-23_103456.db\n", log, StringComparison.Ordinal);
        Assert.Contains("| Programm | Installer wird gestartet: Velopack, Version A 0.11.1, p.nupkg\n", log, StringComparison.Ordinal);
    }

    [Fact]
    public void OhneKopieKeinUpdate()
    {
        // Kontrollfehler "keine Kopie vor dem Update": scheitert die Kopie, startet der Updater nie
        using var temp = new TempOrdner();
        LernstandAnlegen(temp.Pfad);
        File.WriteAllText(Path.Combine(temp.Pfad, Uebernahme.SicherungsOrdner), "eine Datei, kein Ordner");
        var einstellungen = new Einstellungen(temp.Pfad);
        var quelle = new Quelle();
        var e = Assert.Throws<UpdateFehler>(() => UpdateAblauf.Installieren(quelle, new UpdateAngebot("A 0.11.1", "", "s", "p"), einstellungen, temp.Pfad, FesteUhr));
        Assert.Equal(UpdateTexte.KopieFehler, e.Message);
        Assert.Equal(0, quelle.Installationen);
        Assert.Null(einstellungen.Laden()[UpdateMerker.Schluessel]);
        Assert.Contains("Kopie des Lernstands vor dem Update gescheitert: IOException. Das Update wurde nicht gestartet.", LogLesen(temp.Pfad), StringComparison.Ordinal);
    }

    [Fact]
    public void OhneLernstandKeineKopieNoetig()
    {
        using var temp = new TempOrdner();
        var quelle = new Quelle();
        Assert.Equal(UpdateTexte.Installiert, UpdateAblauf.Installieren(quelle, new UpdateAngebot("A 0.11.1", "", "s", "p"), new Einstellungen(temp.Pfad), temp.Pfad, FesteUhr));
        Assert.Equal(1, quelle.Installationen);
        Assert.Contains("Kein Lernstand vorhanden, keine Kopie nötig.", LogLesen(temp.Pfad), StringComparison.Ordinal);
    }

    [Fact]
    public void StartfehlerVergisstDenMerker()
    {
        using var temp = new TempOrdner();
        var einstellungen = new Einstellungen(temp.Pfad);
        var quelle = new Quelle { BeimInstallieren = () => throw new InvalidOperationException("Update.exe fehlt") };
        var e = Assert.Throws<UpdateFehler>(() => UpdateAblauf.Installieren(quelle, new UpdateAngebot("A 0.11.1", "", "s", "p"), einstellungen, temp.Pfad, FesteUhr));
        Assert.Equal(UpdateTexte.StartFehler, e.Message);
        Assert.Null(einstellungen.Laden()[UpdateMerker.Schluessel]);
        Assert.Contains("Installer ließ sich nicht starten: InvalidOperationException", LogLesen(temp.Pfad), StringComparison.Ordinal);
        // nicht installiert: nichts passiert
        var ordner = new Quelle { Installiert = false };
        Assert.Equal(UpdateTexte.NurInstalliert, Assert.Throws<UpdateFehler>(() => UpdateAblauf.Installieren(ordner, new UpdateAngebot("A 0.11.1", "", "s", "p"), einstellungen, temp.Pfad, FesteUhr)).Message);
        Assert.Equal(0, ordner.Installationen);
    }

    [Fact]
    public async Task InstallierenWartetAufAbgleichUebernahmeUndSicherung()
    {
        // D8: Update und Abgleich/Uebernahme/Sicherung schliessen sich ueber Vorgaenge aus
        using var temp = new TempOrdner();
        var quelle = new Quelle();
        Task<string> lauf;
        using (Vorgaenge.Sperren())
        {
            lauf = Task.Run(() => UpdateAblauf.Installieren(quelle, new UpdateAngebot("A 0.11.1", "", "s", "p"), new Einstellungen(temp.Pfad), temp.Pfad, FesteUhr));
            await Task.Delay(300, TestContext.Current.CancellationToken);
            Assert.False(lauf.IsCompleted);
            Assert.Equal(0, quelle.Installationen);
        }
        Assert.Equal(UpdateTexte.Installiert, await lauf);
        Assert.Equal(1, quelle.Installationen);
        Assert.False(quelle.SperreFrei);            // waehrend des Starts gehalten
    }

    // ---- Hilfen -------------------------------------------------------------------------------

    private sealed class Attrappe(Func<HttpRequestMessage, HttpResponseMessage> antwort) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(antwort(request));
    }

    private sealed class Quelle : IUpdateQuelle
    {
        public bool Installiert { get; set; } = true;
        public byte[] Inhalt { get; init; } = [];
        public Exception? Fehler { get; init; }
        public string Ordner { get; init; } = Path.GetTempPath();
        public string Datei => Path.Combine(Ordner, "Paket.nupkg");
        public Action? BeimInstallieren { get; set; }
        public int Installationen { get; private set; }
        public bool SperreFrei { get; private set; } = true;

        public Task<UpdateAngebot?> SuchenAsync(CancellationToken abbruch) =>
            Fehler is not null ? throw Fehler : Task.FromResult<UpdateAngebot?>(null);

        public async Task<string> LadenAsync(UpdateAngebot angebot, Action<long, long> fortschritt, CancellationToken abbruch)
        {
            await Task.Yield();
            if (Fehler is not null)
            {
                throw Fehler;
            }
            await File.WriteAllBytesAsync(Datei, Inhalt, abbruch);
            fortschritt(Inhalt.Length, Inhalt.Length);
            return Datei;
        }

        public void Installieren(UpdateAngebot angebot)
        {
            SperreFrei = !Vorgaenge.Belegt;
            BeimInstallieren?.Invoke();
            Installationen++;
        }
    }
}
