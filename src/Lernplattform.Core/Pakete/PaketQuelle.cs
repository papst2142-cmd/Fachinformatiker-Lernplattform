using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;

namespace Lernplattform.Core.Pakete;

/// <summary>Ein Paket, das die Quelle anbietet (Name und Text nur zur Anzeige; gilt erst nach der Pruefung).</summary>
public sealed record PaketAngebot(string Kennung, long Stand, string Name, string Text, long Groesse, string Adresse, string? Sha256);

/// <summary>Fehler einer Paket-Quelle (kein Netz, Stoerung, Antwort unlesbar); Text fuer die Anzeige.</summary>
public sealed class PaketFehler(string text, bool keineVerbindung) : Exception(text)
{
    public bool KeineVerbindung { get; } = keineVerbindung;
}

/// <summary>Woher Pakete kommen (Plan P12-175 Abschnitt 2, Nico 2a).</summary>
public interface IPaketQuelle
{
    Task<IReadOnlyList<PaketAngebot>> ListeAsync(CancellationToken abbruch);

    /// <summary>Laedt die Datei eines Angebots nach <paramref name="ziel"/>; <paramref name="fortschritt"/> = geladene Bytes.</summary>
    Task LadenAsync(PaketAngebot angebot, string ziel, IProgress<long>? fortschritt, CancellationToken abbruch);
}

/// <summary>
/// Welle A 0.87.0: Auswahl der Quelle. Ohne Schalter GitHub (eigene Releases <c>vP&lt;kennung&gt;.&lt;stand&gt;</c>, nie „latest“,
/// Release macht Chat); <c>LERNPLATTFORM_PAKET_QUELLE</c> = Ordner mit <c>.fipaket</c>-Dateien (Tests, Start-Test, Probe-APK).
/// </summary>
public static partial class PaketQuelle
{
    public const string Umgebung = "LERNPLATTFORM_PAKET_QUELLE";

    /// <summary>Liste der Releases (oeffentlich, kein Token); Pakete filtert <see cref="TagMuster"/>.</summary>
    public const string ListeUrl = "https://api.github.com/repos/" + GithubSuche.Repository + "/releases?per_page=100";

    /// <summary>Tag eines Paket-Releases: vP&lt;kennung&gt;.&lt;stand&gt; (z. B. vPstadt2.1); keine Programm-Linie nimmt ihn (<see cref="ReleaseLinie"/>).</summary>
    [GeneratedRegex(@"^vP([a-z][a-z0-9_]{1,31})\.([1-9][0-9]{0,8})\z", RegexOptions.CultureInvariant)]
    public static partial Regex TagMuster();

    /// <summary>Probe-APK: Ordner, den der Android-Start setzt (dort liegt das eingebettete Probe-Paket).</summary>
    public static string? ProbeOrdner { get; set; }

    public static IPaketQuelle Fuer(HttpClient http, Action<string> melden) =>
        Environment.GetEnvironmentVariable(Umgebung) is { Length: > 0 } ordner ? new OrdnerQuelle(ordner)
        : ProbeOrdner is { } probe ? new OrdnerQuelle(probe)
        : new GithubQuelle(http, melden);

    /// <summary>Je Kennung nur das Angebot mit dem hoechsten Stand, nach Kennung geordnet.</summary>
    internal static IReadOnlyList<PaketAngebot> Neueste(IEnumerable<PaketAngebot> alle) =>
        [.. alle.GroupBy(a => a.Kennung).Select(g => g.MaxBy(a => a.Stand)!).OrderBy(a => a.Kennung, StringComparer.Ordinal)];

    /// <summary>Wertet die Release-Liste von GitHub aus (nur Pakete, keine Entwuerfe und Vorab-Releases, Datei mit passendem Namen).</summary>
    public static IReadOnlyList<PaketAngebot> Auswerten(string json)
    {
        JsonArray liste;
        try
        {
            liste = JsonNode.Parse(json) as JsonArray ?? throw new JsonException();
        }
        catch (Exception x) when (x is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new PaketFehler(PaketTexte.Format(PaketTexte.Netz, "Antwort von GitHub nicht lesbar"), false);
        }
        var angebote = new List<PaketAngebot>();
        foreach (var release in liste.OfType<JsonObject>())
        {
            if (Wahr(release["draft"]) || Wahr(release["prerelease"]) || Zeichen(release["tag_name"]) is not { } tag
                || TagMuster().Match(tag) is not { Success: true } m)
            {
                continue;
            }
            var kennung = m.Groups[1].Value;
            var stand = long.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var name = Paket.Dateiname(kennung, stand);
            if ((release["assets"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(a => Zeichen(a["name"]) == name) is not { } datei
                || Zeichen(datei["browser_download_url"]) is not { } adresse)
            {
                continue;
            }
            var groesse = datei["size"] is JsonValue g && g.TryGetValue<long>(out var s) ? s : 0;
            var sha = Zeichen(datei["digest"]) is { } d && d.StartsWith("sha256:", StringComparison.Ordinal) ? d[7..] : null;
            var titel = Zeichen(release["name"]) is { Length: > 0 } t ? t : kennung;
            var text = (Zeichen(release["body"]) ?? "").Replace("\r", "").Split('\n').FirstOrDefault(z => z.Trim().Length > 0)?.Trim() ?? "";
            angebote.Add(new PaketAngebot(kennung, stand, titel, text, groesse, adresse, sha));
        }
        return Neueste(angebote);
    }

    private static string? Zeichen(JsonNode? wert) => wert is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    private static bool Wahr(JsonNode? wert) => wert is JsonValue v && v.GetValueKind() == JsonValueKind.True;

    /// <summary>Kopiert mit Fortschritt (Bloecke von 80 KB).</summary>
    internal static async Task KopierenAsync(Stream von, Stream nach, IProgress<long>? fortschritt, CancellationToken abbruch)
    {
        var block = new byte[81920];
        long summe = 0;
        int n;
        while ((n = await von.ReadAsync(block, abbruch).ConfigureAwait(false)) > 0)
        {
            await nach.WriteAsync(block.AsMemory(0, n), abbruch).ConfigureAwait(false);
            summe += n;
            if (summe > Paket.HoechstGroesse)
            {
                throw new PaketFehler(PaketTexte.ZuGross, false);
            }
            fortschritt?.Report(summe);
        }
    }
}

/// <summary>Ordner mit Paketdateien (Testquelle). Kennung und Stand aus dem Dateinamen; Name und Text aus dem Kopf, wenn lesbar.</summary>
public sealed class OrdnerQuelle(string ordner) : IPaketQuelle
{
    /// <summary>Nur Tests und Start-Test: Pause je Block beim Laden (Balken sichtbar, Abbruch mitten im Laden).</summary>
    public static readonly AsyncLocal<TimeSpan> BremseFuerTests = new();

    /// <summary>Start-Test: Pause je Block fuer alle Ablaeufe (der Laden-Knopf startet auf dem Oberflaechen-Faden).</summary>
    public static TimeSpan Bremse { get; set; }

    public Task<IReadOnlyList<PaketAngebot>> ListeAsync(CancellationToken abbruch)
    {
        if (!Directory.Exists(ordner))
        {
            throw new PaketFehler(PaketTexte.QuelleFehlt, true);
        }
        var angebote = new List<PaketAngebot>();
        foreach (var datei in Directory.GetFiles(ordner, "*" + Paket.Endung).OrderBy(d => d, StringComparer.Ordinal))
        {
            if (Paket.AusDateiname(Path.GetFileName(datei)) is not { } kennungStand)
            {
                continue;
            }
            var (name, text) = (kennungStand.Kennung, "");
            try
            {
                using var strom = File.OpenRead(datei);
                using var zip = new System.IO.Compression.ZipArchive(strom);
                if (zip.GetEntry(Paket.KopfDatei) is { } eintrag)
                {
                    using var kopf = eintrag.Open();
                    if (JsonNode.Parse(kopf) is JsonObject o)
                    {
                        name = o["name"] is JsonValue n && n.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : name;
                        text = o["text"] is JsonValue t && t.GetValueKind() == JsonValueKind.String ? t.GetValue<string>() : "";
                    }
                }
            }
            catch (Exception x) when (x is IOException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException)
            {
                // unlesbar: bleibt im Angebot, die Pruefung beim Laden nennt den Grund
            }
            angebote.Add(new PaketAngebot(kennungStand.Kennung, kennungStand.Stand, name, text, new FileInfo(datei).Length, datei, null));
        }
        return Task.FromResult(PaketQuelle.Neueste(angebote));
    }

    public async Task LadenAsync(PaketAngebot angebot, string ziel, IProgress<long>? fortschritt, CancellationToken abbruch)
    {
        await using var von = File.OpenRead(angebot.Adresse);
        await using var nach = File.Create(ziel);
        var bremse = BremseFuerTests.Value > TimeSpan.Zero ? BremseFuerTests.Value : Bremse;
        var block = new byte[bremse > TimeSpan.Zero ? 16384 : 81920];
        long summe = 0;
        int n;
        while ((n = await von.ReadAsync(block, abbruch).ConfigureAwait(false)) > 0)
        {
            await nach.WriteAsync(block.AsMemory(0, n), abbruch).ConfigureAwait(false);
            summe += n;
            fortschritt?.Report(summe);
            if (bremse > TimeSpan.Zero)
            {
                await Task.Delay(bremse, abbruch).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// GitHub: Release-Liste und Datei, jede Abfrage nur ueber <see cref="Stoerung"/> (bis 4 Versuche bei 5xx, ohne Antwort,
/// Zeitueberschreitung); die Datei wird gegen den <c>digest</c> (SHA-256) von GitHub geprueft.
/// </summary>
public sealed class GithubQuelle(HttpClient http, Action<string> melden, string url = PaketQuelle.ListeUrl) : IPaketQuelle
{
    public async Task<IReadOnlyList<PaketAngebot>> ListeAsync(CancellationToken abbruch)
    {
        try
        {
            var json = await Stoerung.WiederholenAsync("Paketliste", async () =>
            {
                using var anfrage = new HttpRequestMessage(HttpMethod.Get, url);
                anfrage.Headers.UserAgent.ParseAdd(GithubSuche.Kennung);
                anfrage.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var zeit = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
                zeit.CancelAfter(GithubSuche.Wartezeit);
                using var antwort = await http.SendAsync(anfrage, zeit.Token).ConfigureAwait(false);
                antwort.EnsureSuccessStatusCode();
                return await antwort.Content.ReadAsStringAsync(zeit.Token).ConfigureAwait(false);
            }, melden, abbruch: abbruch).ConfigureAwait(false);
            return PaketQuelle.Auswerten(json);
        }
        catch (Exception x) when (!abbruch.IsCancellationRequested && x is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new PaketFehler(PaketTexte.KeineVerbindung, true);
        }
    }

    public async Task LadenAsync(PaketAngebot angebot, string ziel, IProgress<long>? fortschritt, CancellationToken abbruch)
    {
        try
        {
            await Stoerung.WiederholenAsync("Paket " + angebot.Kennung, async () =>
            {
                using var antwort = await http.GetAsync(angebot.Adresse, HttpCompletionOption.ResponseHeadersRead, abbruch).ConfigureAwait(false);
                antwort.EnsureSuccessStatusCode();
                await using var von = await antwort.Content.ReadAsStreamAsync(abbruch).ConfigureAwait(false);
                await using var nach = File.Create(ziel);
                await PaketQuelle.KopierenAsync(von, nach, fortschritt, abbruch).ConfigureAwait(false);
                return true;
            }, melden, abbruch: abbruch).ConfigureAwait(false);
        }
        catch (Exception x) when (!abbruch.IsCancellationRequested && x is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new PaketFehler(PaketTexte.Format(PaketTexte.Netz, x.Message), true);
        }
        if (angebot.Sha256 is { } soll)
        {
            await using var datei = File.OpenRead(ziel);
            var ist = Convert.ToHexStringLower(await SHA256.HashDataAsync(datei, abbruch).ConfigureAwait(false));
            if (!string.Equals(ist, soll, StringComparison.OrdinalIgnoreCase))
            {
                throw new PaketFehler(PaketTexte.Format(PaketTexte.Pruefsumme, Paket.Dateiname(angebot.Kennung, angebot.Stand)), false);
            }
        }
    }
}
