using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Update;

/// <summary>
/// Phase 5 BE9: check_for_update (fisi_update.py) fuer das Handy: fragt das neueste Release des Repos
/// ab (oeffentlich, kein Token) und waehlt die APK-Datei wie pick_asset(kind="android"). Vorab-Releases
/// und Entwuerfe werden nie angeboten. Der PC sucht ueber Velopack (Desktop/Updater.cs).
/// Tags der C#-Linie: "vA0.11.0", "vB0.1.0", ab 1.0 "v1.0.0" (<see cref="UpdateVersion"/>).
/// </summary>
public static class GithubSuche
{
    public const string Repository = "papst2142-cmd/Fachinformatiker-Lernplattform";
    public const string NeuesteUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
    public const string ReleaseSeite = "https://github.com/" + Repository + "/releases/latest";
    public const string Kennung = "Fachinformatiker-Lernplattform-Updater";
    public static readonly TimeSpan Wartezeit = TimeSpan.FromSeconds(10);

    /// <summary>Neueres Release oder null; UpdateFehler bei Netzproblemen oder unerwarteter Antwort. url nur fuer Tests.</summary>
    public static async Task<UpdateAngebot?> SuchenAsync(HttpClient http, string aktuell, string art = "android", string url = NeuesteUrl,
        CancellationToken abbruch = default) =>
        Auswerten(await AbrufenAsync(http, url, abbruch).ConfigureAwait(false), aktuell, art);

    /// <summary>
    /// S 0.6.0 (Weisung P12-42 Punkt 2): Suche einer Linie ohne "latest" (das Stadtbau-Werkzeug wird nie "latest").
    /// Fragt die Liste der Releases ab (<see cref="ListeUrl"/>) und nimmt nur Releases der Linie (<see cref="AuswertenLinie"/>).
    /// </summary>
    public static async Task<UpdateAngebot?> SuchenLinieAsync(HttpClient http, string aktuell, ReleaseLinie.Linie linie, string url = ListeUrl,
        CancellationToken abbruch = default) =>
        AuswertenLinie(await AbrufenAsync(http, url, abbruch).ConfigureAwait(false), aktuell, linie, "android");

    /// <summary>Liste der Releases (neueste zuerst, 30 je Seite wie die Velopack-Quelle LinienQuelle am PC).</summary>
    public const string ListeUrl = "https://api.github.com/repos/" + Repository + "/releases?per_page=30";

    private static async Task<string> AbrufenAsync(HttpClient http, string url, CancellationToken abbruch)
    {
        string text;
        try
        {
            using var zeit = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
            zeit.CancelAfter(Wartezeit);
            using var anfrage = new HttpRequestMessage(HttpMethod.Get, url);
            anfrage.Headers.UserAgent.Add(new ProductInfoHeaderValue(Kennung, null));
            anfrage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var antwort = await http.SendAsync(anfrage, zeit.Token).ConfigureAwait(false);
            if (!antwort.IsSuccessStatusCode)
            {
                throw new UpdateFehler(UpdateTexte.Abgelehnt.Replace("%d", ((int)antwort.StatusCode).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
            }
            text = new UTF8Encoding(false, true).GetString(await antwort.Content.ReadAsByteArrayAsync(zeit.Token).ConfigureAwait(false));
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or DecoderFallbackException)
        {
            throw new UpdateFehler(UpdateTexte.KeineVerbindung);
        }
        return text;
    }

    /// <summary>Auswertung der Antwort (ohne Netz, fuer Tests und G41).</summary>
    public static UpdateAngebot? Auswerten(string antwort, string aktuell, string art)
    {
        JsonNode? wurzel;
        try
        {
            wurzel = JsonNode.Parse(antwort);
        }
        catch (JsonException)
        {
            throw new UpdateFehler(UpdateTexte.KeineVerbindung);    // ValueError wie in Python
        }
        if (wurzel is not JsonObject release)
        {
            throw new UpdateFehler(UpdateTexte.Unerwartet);
        }
        if (Wahr(release["prerelease"]) || Wahr(release["draft"]))
        {
            return null;
        }
        var tag = release.ContainsKey("tag_name") ? Zeichen(release["tag_name"]) : "";
        var dateien = release.ContainsKey("assets") ? release["assets"] as JsonArray : [];
        if (tag is null || dateien is null)
        {
            throw new UpdateFehler(UpdateTexte.Unerwartet);
        }
        // Phase 13 W2 (Weisung P12-17): ein Release des Stadtbau-Werkzeugs (vS...) ist nie ein Update der Lernplattform
        if (ReleaseLinie.IstStadtbau(tag) || !UpdateVersion.IstNeuer(tag, aktuell))
        {
            return null;
        }
        return Bauen(release, UpdateVersion.Lesen(tag)!.Anzeige, dateien, art);
    }

    /// <summary>
    /// S 0.6.0 (Weisung P12-42 Punkt 2): Antwort von <c>GET /repos/…/releases</c> fuer eine Linie ohne "latest". Nimmt nur
    /// Releases der Linie (<see cref="ReleaseLinie.Filtern"/>, nie ein Release der anderen Linie), ohne Entwuerfe und
    /// Vorab-Releases, davon die hoechste Version; ist sie neuer als <paramref name="aktuell"/>, das Angebot, sonst null.
    /// Heute nur fuer das Stadtbau-Werkzeug (Anzeige "S 0.6.0"); die Lernplattform sucht weiter ueber "latest".
    /// </summary>
    public static UpdateAngebot? AuswertenLinie(string antwort, string aktuell, ReleaseLinie.Linie linie, string art)
    {
        if (linie != ReleaseLinie.Linie.Stadtbau)
        {
            throw new ArgumentException("Die Lernplattform sucht ueber releases/latest (SuchenAsync).", nameof(linie));
        }
        JsonNode? wurzel;
        try
        {
            wurzel = JsonNode.Parse(antwort);
        }
        catch (JsonException)
        {
            throw new UpdateFehler(UpdateTexte.KeineVerbindung);
        }
        if (wurzel is not JsonArray)
        {
            throw new UpdateFehler(UpdateTexte.Unerwartet);
        }
        var eigene = ReleaseLinie.StadtbauNummer(aktuell) ?? throw new ArgumentException("Version " + aktuell, nameof(aktuell));
        JsonObject? bestes = null;
        (int, int, int) beste = default;
        foreach (var release in ReleaseLinie.Filtern(antwort, linie))
        {
            if (Wahr(release["prerelease"]) || Wahr(release["draft"]) || ReleaseLinie.StadtbauNummer(Zeichen(release["tag_name"])) is not { } nummer)
            {
                continue;
            }
            if (bestes is null || nummer.CompareTo(beste) > 0)
            {
                bestes = release;
                beste = nummer;
            }
        }
        if (bestes is null || beste.CompareTo(eigene) <= 0)
        {
            return null;
        }
        var dateien = bestes.ContainsKey("assets") ? bestes["assets"] as JsonArray : [];
        if (dateien is null)
        {
            throw new UpdateFehler(UpdateTexte.Unerwartet);
        }
        return Bauen(bestes, ReleaseLinie.StadtbauAnzeige(beste), dateien, art);
    }

    private static UpdateAngebot Bauen(JsonObject release, string anzeige, JsonArray dateien, string art)
    {
        var datei = DateiWaehlen(dateien, art);
        var groesse = datei?["size"] is JsonValue g && g.GetValueKind() == JsonValueKind.Number && g.TryGetValue<long>(out var l) ? l : 0;
        return new UpdateAngebot(
            anzeige,
            Zeichen(release["body"]) is { } notiz ? Rechner.PythonZeichen.Strip(notiz) : "",
            Wahr(release["html_url"]) ? Zeichen(release["html_url"]) ?? ReleaseSeite : ReleaseSeite,
            datei is null ? null : Zeichen(datei["name"]),
            datei is null ? null : Zeichen(datei["browser_download_url"]),
            groesse,
            datei is null ? null : Zeichen(datei["digest"]),
            TabellenPruefsumme: datei is null || Zeichen(datei["name"]) is not { } name ? null : HandyPruefung.AusReleaseText(Zeichen(release["body"]), name));
    }

    /// <summary>pick_asset fuer art "android" (Endung .apk); Eintraege ungewohnter Form werden uebersprungen.</summary>
    public static JsonObject? DateiWaehlen(JsonArray dateien, string art)
    {
        if (art != "android")
        {
            return null;
        }
        foreach (var eintrag in dateien)
        {
            if (eintrag is JsonObject datei && PythonStr(datei["name"]).EndsWith(".apk", StringComparison.Ordinal))
            {
                return datei;
            }
        }
        return null;
    }

    private static string? Zeichen(JsonNode? wert) => wert is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    // str(asset.get("name") or ""): ein Name, der kein Text ist, endet nie auf ".apk"
    private static string PythonStr(JsonNode? wert) => Zeichen(wert) ?? "";

    /// <summary>Wahrheitswert wie in Python (leer, 0, false, null = falsch).</summary>
    private static bool Wahr(JsonNode? wert) => wert switch
    {
        null => false,
        JsonObject o => o.Count > 0,
        JsonArray a => a.Count > 0,
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => v.GetValue<string>().Length > 0,
            JsonValueKind.Number => v.GetValue<double>() != 0,
            _ => false,
        },
        _ => false,
    };
}
