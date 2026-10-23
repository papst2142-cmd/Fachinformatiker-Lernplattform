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
        CancellationToken abbruch = default)
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
        return Auswerten(text, aktuell, art);
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
        if (!UpdateVersion.IstNeuer(tag, aktuell))
        {
            return null;
        }
        var datei = DateiWaehlen(dateien, art);
        var groesse = datei?["size"] is JsonValue g && g.GetValueKind() == JsonValueKind.Number && g.TryGetValue<long>(out var l) ? l : 0;
        return new UpdateAngebot(
            UpdateVersion.Lesen(tag)!.Anzeige,
            Zeichen(release["body"]) is { } notiz ? Rechner.PythonZeichen.Strip(notiz) : "",
            Wahr(release["html_url"]) ? Zeichen(release["html_url"]) ?? ReleaseSeite : ReleaseSeite,
            datei is null ? null : Zeichen(datei["name"]),
            datei is null ? null : Zeichen(datei["browser_download_url"]),
            groesse,
            datei is null ? null : Zeichen(datei["digest"]));
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
