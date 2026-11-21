using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.87.0 (Weisung P12-294, Plan P12-175, Nico 1a 2a 3b 4a 5a): Inhaltspakete Grundlage. Abnahmesaetze A1 (laden, pruefen,
/// anzeigen, entfernen, Datenordner danach wie vorher), A2 (kaputt, abgeschnitten, Datei fehlt, fremd, zu neu, unbekannte Art =
/// Grund, nichts halb; Abbruch laesst den alten Stand; Reste raeumt der naechste Start weg), A3 (ohne Netz geht Geladenes,
/// „Keine Verbindung“), A4 (kein Programm nimmt einen vP-Tag, Netz nur ueber Stoerung, nur Datenordner). Echte Testdateien in
/// <c>Testdaten/pakete_echt/</c> (gebaut mit <c>werkzeuge/pakete/paket_bauen.py</c>), Varianten werden hier daraus gebaut.
/// </summary>
public sealed class PaketTests : IDisposable
{
    private const string App = "A 0.87.0";
    private static readonly string Echt = Path.Combine(Pfade.Wurzel, "tests/Lernplattform.Tests/Testdaten/pakete_echt");
    private static readonly string Probe = Path.Combine(Pfade.Wurzel, "tests/Lernplattform.Tests/Testdaten/pakete");
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "paket_tests_" + Guid.NewGuid().ToString("N"));

    public PaketTests() => Directory.CreateDirectory(_tmp);

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

    private static byte[] Klein => File.ReadAllBytes(Path.Combine(Echt, "probe_klein_1.fipaket"));

    private static Dictionary<string, byte[]> Dateien(byte[] zip)
    {
        using var strom = new MemoryStream(zip);
        return Paket.Entpacken(strom).Dateien!;
    }

    private static byte[] Zip(IEnumerable<(string Name, byte[] Inhalt)> eintraege)
    {
        using var puffer = new MemoryStream();
        using (var zip = new ZipArchive(puffer, ZipArchiveMode.Create, true))
        {
            foreach (var (name, inhalt) in eintraege)
            {
                using var s = zip.CreateEntry(name).Open();
                s.Write(inhalt);
            }
        }
        return puffer.ToArray();
    }

    /// <summary>Baut das kleine Paket mit geaendertem Kopf neu (Pruefsummen bleiben, wenn die Datei gleich bleibt).</summary>
    private static byte[] MitKopf(Action<JsonObject> aendern, Func<byte[], byte[]>? stadt = null)
    {
        var d = Dateien(Klein);
        var kopf = (JsonObject)JsonNode.Parse(d[Paket.KopfDatei])!;
        aendern(kopf);
        var s = stadt is null ? d[Paket.StadtDatei] : stadt(d[Paket.StadtDatei]);
        return Zip([(Paket.KopfDatei, Encoding.UTF8.GetBytes(kopf.ToJsonString())), (Paket.StadtDatei, s)]);
    }

    private static string? Grund(byte[] zip, string app = App) => Paket.PruefenArchiv(new MemoryStream(zip), app).Grund;

    [Fact]
    public void A1_EchtesKleinesPaket_IstGut()
    {
        var e = Paket.PruefenArchiv(new MemoryStream(Klein), App);
        Assert.True(e.Gut, e.Grund);
        Assert.Equal(("stadt", "probe_klein", 1L, 1L), (e.Kopf!.Art, e.Kopf.Kennung, e.Kopf.Stand, e.Kopf.Format));
        Assert.Equal("Probe: kleine Stadt", e.Kopf.Name);
    }

    [Fact]
    public void A1_ProbePaketTestkarte_IstGut()
    {
        var e = Paket.PruefenArchiv(File.OpenRead(Path.Combine(Probe, "probe_testkarte_1.fipaket")), App);
        Assert.True(e.Gut, e.Grund);
        Assert.Equal("probe_testkarte", e.Kopf!.Kennung);
    }

    [Fact]
    public void A2_UnbekannteArt_EchteTestdatei_KlarerGrund_Gegenprobe()
    {
        var echt = File.ReadAllBytes(Path.Combine(Echt, "probe_grafik_1.fipaket"));
        Assert.Equal(PaketTexte.ArtUnbekannt, Grund(echt));
        Assert.Equal("Diese Art von Paket kennt die App noch nicht. Bitte die App aktualisieren.", PaketTexte.ArtUnbekannt);
        // Gegenprobe: dieselbe Datei mit art „stadt“ ist gut (der Grund kommt wirklich von der Art)
        var d = Dateien(echt);
        var kopf = (JsonObject)JsonNode.Parse(d[Paket.KopfDatei])!;
        kopf["art"] = "stadt";
        Assert.Null(Grund(Zip([(Paket.KopfDatei, Encoding.UTF8.GetBytes(kopf.ToJsonString())), (Paket.StadtDatei, d[Paket.StadtDatei])])));
        // ohne Feld art: Pflichtfeld
        Assert.Equal(PaketTexte.Format(PaketTexte.FeldFehlt, "art"), Grund(MitKopf(k => k.Remove("art"))));
    }

    /// <summary>
    /// Fehlerpruefung P12-341 Fund 2: zwei Dateinamen, die sich nur in Gross-/Kleinschreibung unterscheiden, sind unter
    /// Windows und macOS EINE Datei. Ein solches Paket ist fehlerhaft (wie ein doppelter Name), auch wenn der Kopf beide
    /// mit richtiger Pruefsumme nennt. Gegenprobe: bis A 1.2.1 galt es als gut (Grund leer).
    /// </summary>
    [Fact]
    public void A2_NamenNurGrossKleinAnders_Fehlerhaft()
    {
        var d = Dateien(Klein);
        var kopf = JsonNode.Parse(d[Paket.KopfDatei])!.AsObject();
        var gross = Paket.StadtDatei.ToUpperInvariant();
        kopf["dateien"]![gross] = kopf["dateien"]![Paket.StadtDatei]!.DeepClone();
        var zip = Zip([(Paket.KopfDatei, Encoding.UTF8.GetBytes(kopf.ToJsonString())), (Paket.StadtDatei, d[Paket.StadtDatei]), (gross, d[Paket.StadtDatei])]);
        Assert.Equal(PaketTexte.Format(PaketTexte.DateiFremd, gross), Grund(zip));
        // dasselbe Paket mit einem wirklich anderen Namen bleibt gut (die Regel trifft nur Gross-/Kleinschreibung)
        var andersKopf = JsonNode.Parse(d[Paket.KopfDatei])!.AsObject();
        andersKopf["dateien"]!["stadt2.fistadt"] = andersKopf["dateien"]![Paket.StadtDatei]!.DeepClone();
        Assert.Null(Grund(Zip([(Paket.KopfDatei, Encoding.UTF8.GetBytes(andersKopf.ToJsonString())), (Paket.StadtDatei, d[Paket.StadtDatei]), ("stadt2.fistadt", d[Paket.StadtDatei])])));
    }

    [Fact]
    public void A2_Kaputt_Abgeschnitten_Fehlt_Fremd_ZuNeu()
    {
        Assert.Equal(PaketTexte.Format(PaketTexte.Pruefsumme, Paket.StadtDatei), Grund(MitKopf(_ => { }, s => { var k = (byte[])s.Clone(); k[100] ^= 1; return k; })));
        Assert.Equal(PaketTexte.KeinArchiv, Grund(Klein[..(Klein.Length / 2)]));
        Assert.Equal(PaketTexte.KeinArchiv, Grund(Encoding.UTF8.GetBytes("kein Archiv")));
        var d = Dateien(Klein);
        Assert.Equal(PaketTexte.Format(PaketTexte.DateiFehlt, Paket.StadtDatei), Grund(Zip([(Paket.KopfDatei, d[Paket.KopfDatei])])));
        Assert.Equal(PaketTexte.KopfFehlt, Grund(Zip([("bild.png", [1, 2, 3])])));
        Assert.Equal(PaketTexte.Format(PaketTexte.DateiFremd, "extra.txt"),
            Grund(Zip([(Paket.KopfDatei, d[Paket.KopfDatei]), (Paket.StadtDatei, d[Paket.StadtDatei]), ("extra.txt", [1])])));
        Assert.Equal(PaketTexte.Format(PaketTexte.DateiFremd, "unter/x"), Grund(Zip([("unter/x", [1])])));
        Assert.Equal(PaketTexte.KopfKaputt, Grund(Zip([(Paket.KopfDatei, Encoding.UTF8.GetBytes("{kaputt")), (Paket.StadtDatei, d[Paket.StadtDatei])])));
        Assert.Equal(PaketTexte.Format(PaketTexte.FormatNeu, 2, 1), Grund(MitKopf(k => k["format"] = 2)));
        Assert.Equal(PaketTexte.Format(PaketTexte.FeldFehlt, "format"), Grund(MitKopf(k => k["format"] = 1.5)));
        Assert.Equal(PaketTexte.Format(PaketTexte.AppAlt, "A 0.99.0"), Grund(MitKopf(k => k["braucht_app"] = "A 0.99.0")));
        Assert.Equal(PaketTexte.Format(PaketTexte.AppAlt, "A 0.87.0"), Grund(Klein, "A 0.86.0"));
        Assert.Null(Grund(Klein, "B 0.1.0"));
        Assert.Equal(PaketTexte.Format(PaketTexte.KennungReserviert, "stadt1"), Grund(MitKopf(k => k["kennung"] = "stadt1")));
        // Welle A 0.89.0 (Weisung P12-297 B): falsch gelieferte Ware traegt den Vorsatz falschlieferung: (kein Paket)
        Assert.Equal(PaketTexte.Format(PaketTexte.KennungReserviert, "falschlieferung"), Grund(MitKopf(k => k["kennung"] = "falschlieferung")));
        Assert.Equal(PaketTexte.Format(PaketTexte.FeldFehlt, "kennung"), Grund(MitKopf(k => k["kennung"] = "Gross")));
        Assert.Equal(PaketTexte.Format(PaketTexte.FeldFehlt, "stand"), Grund(MitKopf(k => k["stand"] = 0)));
        Assert.StartsWith(PaketTexte.Format(PaketTexte.StadtFehler, ""), Grund(MitKopf(k =>
        {
            var falsch = Encoding.UTF8.GetBytes("{}");
            k["dateien"]![Paket.StadtDatei]!["sha256"] = Convert.ToHexStringLower(SHA256.HashData(falsch));
            k["dateien"]![Paket.StadtDatei]!["groesse"] = falsch.Length;
        }, _ => Encoding.UTF8.GetBytes("{}")))!);
    }

    private PaketVerwaltung Verwaltung(string quelle, string? daten = null) =>
        new(new PaketAblage(daten ?? Path.Combine(_tmp, "daten")), () => new OrdnerQuelle(quelle), App);

    private static string Abdruck(string ordner) => !Directory.Exists(ordner) ? "" : string.Join("\n",
        Directory.GetFileSystemEntries(ordner, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(p => Path.GetRelativePath(ordner, p) + (File.Exists(p) ? " " + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p))) : "/")));

    [Fact]
    public async Task A1_Laden_Anzeigen_Entfernen_DatenordnerWieVorher()
    {
        var daten = Path.Combine(_tmp, "daten");
        Directory.CreateDirectory(daten);
        File.WriteAllText(Path.Combine(daten, "einstellungen.json"), "{}");
        var vorher = Abdruck(daten);
        var quelle = Path.Combine(_tmp, "quelle");
        Directory.CreateDirectory(quelle);
        File.Copy(Path.Combine(Echt, "probe_klein_1.fipaket"), Path.Combine(quelle, "probe_klein_1.fipaket"));
        var v = Verwaltung(quelle, daten);
        Assert.Empty(v.Zeilen());
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        var zeile = Assert.Single(v.Zeilen());
        Assert.Equal((PaketZustand.NichtGeladen, "Probe: kleine Stadt"), (zeile.Zustand, zeile.Name));
        Assert.Equal([PaketKnopf.Laden], zeile.Knoepfe);
        var stufen = new List<long>();
        Assert.Null(await v.LadenAsync(zeile.Angebot!, new SofortFortschritt(stufen), TestContext.Current.CancellationToken));
        Assert.NotEmpty(stufen);
        zeile = Assert.Single(v.Zeilen());
        Assert.Equal((PaketZustand.Geladen, PaketTexte.Format(PaketTexte.ZustandGeladen, 1)), (zeile.Zustand, zeile.ZustandText));
        Assert.True(File.Exists(Path.Combine(daten, "pakete", "probe_klein", "1", Paket.StadtDatei)));
        Assert.False(Directory.Exists(Path.Combine(daten, "pakete", "_laden")));
        // ein neuer Verwalter (Neustart, ohne Netz) zeigt es weiter als geladen
        var ohneNetz = Verwaltung(Path.Combine(_tmp, "gibt_es_nicht"), daten);
        await ohneNetz.SuchenAsync(TestContext.Current.CancellationToken);
        Assert.True(ohneNetz.OhneVerbindung);
        Assert.Equal(PaketZustand.Geladen, Assert.Single(ohneNetz.Zeilen()).Zustand);
        Assert.True(v.Entfernen("probe_klein"));
        Assert.Equal(vorher, Abdruck(daten));
        Assert.Equal(PaketZustand.NichtGeladen, Assert.Single(v.Zeilen()).Zustand);
    }

    private sealed class SofortFortschritt(List<long> liste) : IProgress<long>
    {
        public void Report(long value) => liste.Add(value);
    }

    [Fact]
    public async Task A2_NeuerStand_Aktualisieren_Abbruch_LaesstAltenStand_ResteWeg()
    {
        var daten = Path.Combine(_tmp, "daten");
        var quelle = Path.Combine(_tmp, "quelle");
        Directory.CreateDirectory(quelle);
        File.Copy(Path.Combine(Echt, "probe_klein_1.fipaket"), Path.Combine(quelle, "probe_klein_1.fipaket"));
        var v = Verwaltung(quelle, daten);
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        Assert.Null(await v.LadenAsync(v.Angebote[0], null, TestContext.Current.CancellationToken));
        var stand1 = Abdruck(daten);
        // Stand 2 wird angeboten
        File.WriteAllBytes(Path.Combine(quelle, "probe_klein_2.fipaket"), MitKopf(k => k["stand"] = 2));
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        var zeile = Assert.Single(v.Zeilen());
        Assert.Equal((PaketZustand.NeuerStand, PaketTexte.Format(PaketTexte.ZustandNeu, 1, 2)), (zeile.Zustand, zeile.ZustandText));
        Assert.Equal([PaketKnopf.Aktualisieren, PaketKnopf.Entfernen], zeile.Knoepfe);
        // Abbruch mitten im Laden (Knopf): der alte Stand bleibt, keine Reste
        OrdnerQuelle.BremseFuerTests.Value = TimeSpan.FromMilliseconds(20);
        using var abbruch = new CancellationTokenSource();
        var fortschritt = new AbbrechenNach(abbruch);
        Assert.Equal(PaketTexte.Abgebrochen, await v.LadenAsync(zeile.Angebot!, fortschritt, abbruch.Token));
        OrdnerQuelle.BremseFuerTests.Value = TimeSpan.Zero;
        Assert.Equal(stand1, Abdruck(daten));
        Assert.Equal(PaketZustand.NeuerStand, Assert.Single(v.Zeilen()).Zustand);
        // Programmende mitten im Laden: Reste in _laden raeumt der naechste Start weg
        var laden = Path.Combine(daten, "pakete", "_laden");
        Directory.CreateDirectory(Path.Combine(laden, "probe_klein_2"));
        File.WriteAllBytes(Path.Combine(laden, "probe_klein_2.fipaket"), [1, 2, 3]);
        _ = Verwaltung(quelle, daten);
        Assert.Equal(stand1, Abdruck(daten));
        // Aktualisieren: Stand 2 ersetzt Stand 1
        Assert.Null(await v.LadenAsync(zeile.Angebot!, null, TestContext.Current.CancellationToken));
        Assert.Equal(PaketTexte.Format(PaketTexte.ZustandGeladen, 2), Assert.Single(v.Zeilen()).ZustandText);
        Assert.False(Directory.Exists(Path.Combine(daten, "pakete", "probe_klein", "1")));
    }

    private sealed class AbbrechenNach(CancellationTokenSource quelle) : IProgress<long>
    {
        public void Report(long value)
        {
            if (value > 0)
            {
                quelle.Cancel();
            }
        }
    }

    [Fact]
    public async Task A2_KaputtesAngebot_RoterGrund_NichtsHalb_Gegenprobe()
    {
        var daten = Path.Combine(_tmp, "daten");
        var quelle = Path.Combine(_tmp, "quelle");
        Directory.CreateDirectory(quelle);
        File.WriteAllBytes(Path.Combine(quelle, "probe_klein_1.fipaket"), Klein[..(Klein.Length - 40)]);
        File.Copy(Path.Combine(Echt, "probe_grafik_1.fipaket"), Path.Combine(quelle, "probe_grafik_1.fipaket"));
        var v = Verwaltung(quelle, daten);
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        foreach (var a in v.Angebote)
        {
            Assert.NotNull(await v.LadenAsync(a, null, TestContext.Current.CancellationToken));
        }
        var zeilen = v.Zeilen().ToDictionary(z => z.Kennung);
        Assert.Equal(PaketTexte.Format(PaketTexte.ZustandFehler, PaketTexte.KeinArchiv), zeilen["probe_klein"].ZustandText);
        Assert.Equal(PaketTexte.Format(PaketTexte.ZustandFehler, PaketTexte.ArtUnbekannt), zeilen["probe_grafik"].ZustandText);
        Assert.All(zeilen.Values, z => Assert.Equal(PaketZustand.Fehlerhaft, z.Zustand));
        // Sammelliste 374: ein abgeschnittenes Archiv kann ein neues Laden loesen, eine unbekannte Art nur ein App-Update
        Assert.Equal([PaketKnopf.NeuLaden], zeilen["probe_klein"].Knoepfe);
        Assert.Empty(zeilen["probe_grafik"].Knoepfe);
        PaketTexte.NeuLadenAltFuerTests = true;
        try
        {
            Assert.Equal([PaketKnopf.NeuLaden], v.Zeilen().First(z => z.Kennung == "probe_grafik").Knoepfe);
        }
        finally
        {
            PaketTexte.NeuLadenAltFuerTests = false;
        }
        Assert.False(Directory.Exists(Path.Combine(daten, "pakete")));
        // Gegenprobe: die gute Datei laedt
        File.Copy(Path.Combine(Echt, "probe_klein_1.fipaket"), Path.Combine(quelle, "probe_klein_1.fipaket"), true);
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        Assert.Null(await v.LadenAsync(v.Angebote.First(a => a.Kennung == "probe_klein"), null, TestContext.Current.CancellationToken));
        Assert.Equal(PaketZustand.Geladen, v.Zeilen().First(z => z.Kennung == "probe_klein").Zustand);
    }

    [Fact]
    public async Task A2_GeladenesPaketSpaeterKaputt_RoterGrund_NeuLadenRepariert()
    {
        var daten = Path.Combine(_tmp, "daten");
        var quelle = Path.Combine(_tmp, "quelle");
        Directory.CreateDirectory(quelle);
        File.Copy(Path.Combine(Echt, "probe_klein_1.fipaket"), Path.Combine(quelle, "probe_klein_1.fipaket"));
        var v = Verwaltung(quelle, daten);
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        Assert.Null(await v.LadenAsync(v.Angebote[0], null, TestContext.Current.CancellationToken));
        var stadt = Path.Combine(daten, "pakete", "probe_klein", "1", Paket.StadtDatei);
        File.WriteAllBytes(stadt, File.ReadAllBytes(stadt)[..100]);
        var zeile = Assert.Single(v.Zeilen());
        Assert.Equal((PaketZustand.Fehlerhaft, PaketTexte.Format(PaketTexte.ZustandFehler, PaketTexte.Format(PaketTexte.Pruefsumme, Paket.StadtDatei))),
            (zeile.Zustand, zeile.ZustandText));
        Assert.Equal([PaketKnopf.NeuLaden, PaketKnopf.Entfernen], zeile.Knoepfe);
        Assert.Null(await v.LadenAsync(zeile.Angebot!, null, TestContext.Current.CancellationToken));
        Assert.Equal(PaketZustand.Geladen, Assert.Single(v.Zeilen()).Zustand);
    }

    [Fact]
    public void S374_Groesse_UnterEinemMb_ZweiNachkommastellen_Gegenprobe()
    {
        Assert.Equal("0,05", PaketTexte.Mb(51_200));
        Assert.Equal("0,00", PaketTexte.Mb(0));
        Assert.Equal("0,94", PaketTexte.Mb(985_000));
        Assert.Equal("1,0", PaketTexte.Mb(1_047_527));
        Assert.Equal("1,0", PaketTexte.Mb(1_048_576));
        Assert.Equal("12,1", PaketTexte.Mb(12_687_769));
        // das echte kleine Probe-Paket (10 396 Bytes) zeigte „0,0 MB“
        Assert.Equal("0,01", PaketTexte.Mb(new FileInfo(Path.Combine(Echt, "probe_klein_1.fipaket")).Length));
        PaketTexte.MbAltFuerTests = true;
        try
        {
            Assert.Equal("0,0", PaketTexte.Mb(51_200));
            Assert.Equal("12,1", PaketTexte.Mb(12_687_769));
        }
        finally
        {
            PaketTexte.MbAltFuerTests = false;
        }
    }

    [Fact]
    public void S374_NeuLaden_NurBeiGruendenDieLadenLoest_Gegenprobe()
    {
        string[] nein =
        [
            PaketTexte.ArtUnbekannt, PaketTexte.Format(PaketTexte.FormatNeu, 2, 1), PaketTexte.Format(PaketTexte.AppAlt, "A 0.99.0"),
            PaketTexte.Format(PaketTexte.KennungReserviert, "stadt1"),
        ];
        string[] ja =
        [
            PaketTexte.KeinArchiv, PaketTexte.KopfFehlt, PaketTexte.KopfKaputt, PaketTexte.Format(PaketTexte.Pruefsumme, Paket.StadtDatei),
            PaketTexte.Format(PaketTexte.DateiFehlt, Paket.StadtDatei), PaketTexte.Format(PaketTexte.Netz, "Zeit"), PaketTexte.Abgebrochen,
            PaketTexte.Format(PaketTexte.Speichern, "voll"),
        ];
        Assert.All(nein, g => Assert.False(PaketTexte.NeuLadenHilft(g), g));
        Assert.All(ja, g => Assert.True(PaketTexte.NeuLadenHilft(g), g));
        // jeder Grund ohne „Neu laden“ nennt das App-Update (ausser der Kennung der App, die kein Update loest)
        Assert.All(nein[..3], g => Assert.Contains("App aktualisieren", g, StringComparison.Ordinal));
        PaketTexte.NeuLadenAltFuerTests = true;
        try
        {
            Assert.All(nein, g => Assert.True(PaketTexte.NeuLadenHilft(g), g));
        }
        finally
        {
            PaketTexte.NeuLadenAltFuerTests = false;
        }
    }

    [Fact]
    public void A4_KeinProgrammNimmtPaketTags_Gegenprobe()
    {
        foreach (var tag in new[] { "vPstadt2.1", "vPprobe_testkarte.12" })
        {
            Assert.False(ReleaseLinie.IstLernplattform(tag), tag);
            Assert.False(ReleaseLinie.IstStadtbau(tag), tag);
            Assert.Matches(PaketQuelle.TagMuster(), tag);
        }
        Assert.True(ReleaseLinie.IstLernplattform("vA0.87.0"));
        Assert.True(ReleaseLinie.IstStadtbau("vS0.56.1"));
        Assert.DoesNotMatch(PaketQuelle.TagMuster(), "vA0.87.0");
        Assert.DoesNotMatch(PaketQuelle.TagMuster(), "vS0.56.1");
    }

    private static string Release(string tag, string datei, bool draft = false, bool vorab = false) =>
        $$"""{"tag_name":"{{tag}}","name":"Stadt {{tag}}","body":"\nErste Zeile\nzweite","draft":{{(draft ? "true" : "false")}},"prerelease":{{(vorab ? "true" : "false")}},"assets":[{"name":"{{datei}}","size":123,"browser_download_url":"https://x/{{datei}}","digest":"sha256:abc"}]}""";

    [Fact]
    public void A4_GithubListe_NurPaketeDerLinie_NeuesterStand()
    {
        var json = "[" + string.Join(",",
            Release("vA0.87.0", "Fachinformatiker-Lernplattform.apk"),
            Release("vPstadt2.1", "stadt2_1.fipaket"),
            Release("vPstadt2.3", "stadt2_3.fipaket"),
            Release("vPstadt2.4", "stadt2_4.fipaket", draft: true),
            Release("vPstadt2.5", "stadt2_5.fipaket", vorab: true),
            Release("vPstadt3.1", "falscher_name.fipaket"),
            Release("vPhafen.2", "hafen_2.fipaket")) + "]";
        var liste = PaketQuelle.Auswerten(json);
        Assert.Equal(["hafen 2", "stadt2 3"], liste.Select(a => a.Kennung + " " + a.Stand));
        Assert.Equal(("Stadt vPstadt2.3", "Erste Zeile", 123L, "abc"), (liste[1].Name, liste[1].Text, liste[1].Groesse, liste[1].Sha256));
    }

    private sealed class Antworten(Func<int, HttpResponseMessage> antwort) : HttpMessageHandler
    {
        public int Anzahl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(antwort(++Anzahl));
    }

    [Fact]
    public async Task A3_A4_Github_NurUeberStoerung_OhneNetz_KeineVerbindung()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        var zeilen = new List<string>();
        // erst 504, dann die Liste: genau eine Wiederholung ueber Stoerung
        var handler = new Antworten(n => n == 1 ? new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" + Release("vPstadt2.1", "stadt2_1.fipaket") + "]") });
        var liste = await new GithubQuelle(new HttpClient(handler), zeilen.Add).ListeAsync(TestContext.Current.CancellationToken);
        Assert.Single(liste);
        Assert.Equal(2, handler.Anzahl);
        Assert.Single(zeilen, z => z.StartsWith(Stoerung.Zeile, StringComparison.Ordinal));
        // kein Netz: alle 4 Versuche, dann „Keine Verbindung“
        var ohne = new Antworten(_ => throw new HttpRequestException("kein Netz"));
        var f = await Assert.ThrowsAsync<PaketFehler>(() => new GithubQuelle(new HttpClient(ohne), _ => { }).ListeAsync(TestContext.Current.CancellationToken));
        Assert.True(f.KeineVerbindung);
        Assert.Equal(PaketTexte.KeineVerbindung, f.Message);
        Assert.Equal(4, ohne.Anzahl);
        // Verwaltung: Suche ohne Netz, Geladenes bleibt
        var v = new PaketVerwaltung(new PaketAblage(Path.Combine(_tmp, "d")), () => new GithubQuelle(new HttpClient(ohne), _ => { }), App);
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        Assert.Equal((true, PaketTexte.KeineVerbindung), (v.OhneVerbindung, v.SuchFehler));
    }

    [Fact]
    public async Task A4_Github_PruefsummeVonGithub_Falsch_Grund()
    {
        Stoerung.OhnePausenFuerTests.Value = true;
        var handler = new Antworten(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Klein) });
        var ziel = Path.Combine(_tmp, "x.fipaket");
        var angebot = new PaketAngebot("probe_klein", 1, "x", "", Klein.Length, "https://x/probe_klein_1.fipaket", new string('0', 64));
        var f = await Assert.ThrowsAsync<PaketFehler>(() => new GithubQuelle(new HttpClient(handler), _ => { }).LadenAsync(angebot, ziel, null, TestContext.Current.CancellationToken));
        Assert.Equal(PaketTexte.Format(PaketTexte.Pruefsumme, "probe_klein_1.fipaket"), f.Message);
        // Gegenprobe: richtige Pruefsumme
        await new GithubQuelle(new HttpClient(handler), _ => { }).LadenAsync(angebot with { Sha256 = Convert.ToHexStringLower(SHA256.HashData(Klein)) }, ziel, null, TestContext.Current.CancellationToken);
        Assert.Equal(Klein, File.ReadAllBytes(ziel));
    }

    [Fact]
    public void A4_AblageNurImDatenordner()
    {
        var daten = Path.Combine(_tmp, "datenordner");
        var ablage = new PaketAblage(daten);
        Assert.StartsWith(daten, ablage.Ordner, StringComparison.Ordinal);
        Assert.StartsWith(daten, ablage.Laden, StringComparison.Ordinal);
        Assert.False(ablage.Ordner.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal));
    }

    [Fact]
    public void ProbePaket_GleichDemWerkzeug_UndDerTestkarte()
    {
        // das Probe-Paket enthaelt genau die Testkarte (gleiche Bytes) und den Kopf aus paket_bauen.py
        var d = Dateien(File.ReadAllBytes(Path.Combine(Probe, "probe_testkarte_1.fipaket")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, "tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt")), d[Paket.StadtDatei]);
        Assert.Equal(["paket.json", "stadt.fistadt"], d.Keys.Order(StringComparer.Ordinal));
    }

    // ---- Sammelliste 376 (A 0.89.0, Weisung P12-297 D): im Fahrplan nur Pakete der Art stadt -------------------------------

    [Fact]
    public async Task S376_Fahrplan_NurStaedte_ArtAusDemAngebot_Gegenprobe()
    {
        var quelle = Path.Combine(_tmp, "quelle376");
        Directory.CreateDirectory(quelle);
        File.Copy(Path.Combine(Echt, "probe_klein_1.fipaket"), Path.Combine(quelle, "probe_klein_1.fipaket"));
        File.Copy(Path.Combine(Echt, "probe_grafik_1.fipaket"), Path.Combine(quelle, "probe_grafik_1.fipaket"));
        var v = Verwaltung(quelle);
        await v.SuchenAsync(TestContext.Current.CancellationToken);
        // vor dem Laden: die Art steht im Angebot (Kopf paket.json)
        Assert.Equal(["probe_grafik grafik", "probe_klein stadt"], v.Angebote.Select(a => a.Kennung + " " + a.Art));
        var zeilen = v.Zeilen().ToDictionary(z => z.Kennung);
        Assert.True(zeilen["probe_klein"].ImFahrplan);
        Assert.False(zeilen["probe_grafik"].ImFahrplan);
        // Angebot ohne Art (Release-Text nennt keine): nicht im Fahrplan, aber in den Optionen
        var ohneArt = zeilen["probe_klein"] with { Art = null };
        Assert.False(ohneArt.ImFahrplan);
        // geladen und als unbekannte Art erkannt: weiter nicht im Fahrplan
        Assert.Equal(PaketTexte.ArtUnbekannt, await v.LadenAsync(v.Angebote.Single(a => a.Kennung == "probe_grafik"), null, TestContext.Current.CancellationToken));
        Assert.False(v.Zeilen().Single(z => z.Kennung == "probe_grafik").ImFahrplan);
        Assert.Null(await v.LadenAsync(v.Angebote.Single(a => a.Kennung == "probe_klein"), null, TestContext.Current.CancellationToken));
        Assert.True(v.Zeilen().Single(z => z.Kennung == "probe_klein").ImFahrplan);
        // Gegenprobe Stand A 0.88.0: das Paket unbekannter Art stand vor dem Laden im Fahrplan
        PaketZeile.ArtAltFuerTests = true;
        try
        {
            Assert.True(zeilen["probe_grafik"].ImFahrplan);
            Assert.True(ohneArt.ImFahrplan);
        }
        finally
        {
            PaketZeile.ArtAltFuerTests = false;
        }
    }

    [Fact]
    public void S376_Github_ArtAusDemReleaseText()
    {
        static string Mit(string tag, string datei, string body) =>
            $$"""{"tag_name":"{{tag}}","name":"N","body":{{System.Text.Json.JsonSerializer.Serialize(body)}},"draft":false,"prerelease":false,"assets":[{"name":"{{datei}}","size":1,"browser_download_url":"https://x/{{datei}}","digest":"sha256:abc"}]}""";
        var liste = PaketQuelle.Auswerten("[" + string.Join(",",
            Mit("vPhafen.1", "hafen_1.fipaket", "Art: stadt\r\nEine Stadt am Meer.\nzweite"),
            Mit("vPlernsi.1", "lernsi_1.fipaket", "Lernkarten SI\nArt: lernen"),
            Mit("vPohne.1", "ohne_1.fipaket", "Kein Hinweis auf die Art"),
            Mit("vPfalsch.1", "falsch_1.fipaket", "Art: Stadt")) + "]");
        Assert.Equal(["falsch -", "hafen stadt", "lernsi lernen", "ohne -"], liste.Select(a => a.Kennung + " " + (a.Art ?? "-")));
        // die Zeile mit der Art zaehlt nicht als Text
        Assert.Equal("Eine Stadt am Meer.", liste[1].Text);
        Assert.Equal("Lernkarten SI", liste[2].Text);
        Assert.Equal("stadt", PaketQuelle.ArtAusText("x\nArt: stadt\n"));
        Assert.Null(PaketQuelle.ArtAusText("Art:stadt"));
    }
}
