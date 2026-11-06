using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lernplattform.Core.Update;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-16 Abschnitt 3, Weisung P12-17): zwei Programme teilen sich die Releases des Repos.
/// Die Lernplattform nimmt nur Tags ihrer Linie (<c>vA…</c>, <c>vB…</c>, ab 1.0 <c>v1…</c>), das Stadtbau-Werkzeug nur
/// <c>vS…</c>. Ein Werkzeug-Release wird nie „latest“ (stadtbau_release.yml); trotzdem filtert jede Update-Quelle selbst
/// nach ihrer Linie (Velopack-Quelle beider Programme ueber <see cref="Filtern"/>, Handy-Suche der Lernplattform ueber
/// <see cref="IstLernplattform"/>), damit ein falsch markiertes Release nie das andere Programm ersetzt.
/// </summary>
public static partial class ReleaseLinie
{
    /// <summary>Linie der Lernplattform: vA0.42.2, vB0.1.0, ab 1.0 v1.0.0 (Hauptnummer ab 1).</summary>
    [GeneratedRegex(@"^v(?:[AB](?:0|[1-9][0-9]*)|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex LernplattformMuster();

    /// <summary>Linie des Stadtbau-Werkzeugs: vS0.1.0.</summary>
    [GeneratedRegex(@"^vS(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex StadtbauMuster();

    public static bool IstLernplattform(string? tag) => tag is not null && LernplattformMuster().IsMatch(tag);

    public static bool IstStadtbau(string? tag) => tag is not null && StadtbauMuster().IsMatch(tag);

    /// <summary>Welche Linie ein Programm nimmt.</summary>
    public enum Linie
    {
        Lernplattform,
        Stadtbau,
    }

    public static bool Gehoert(string? tag, Linie linie) => linie == Linie.Stadtbau ? IstStadtbau(tag) : IstLernplattform(tag);

    /// <summary>
    /// Antwort von <c>GET /repos/…/releases</c> (JSON-Liste): nur die Releases der Linie, Reihenfolge wie von GitHub.
    /// Eintraege ohne Text-Tag oder ungewohnter Form fallen weg. Keine Liste: leeres Ergebnis.
    /// </summary>
    public static List<JsonObject> Filtern(string antwort, Linie linie)
    {
        JsonNode? wurzel;
        try
        {
            wurzel = JsonNode.Parse(antwort);
        }
        catch (JsonException)
        {
            return [];
        }
        var ergebnis = new List<JsonObject>();
        if (wurzel is not JsonArray liste)
        {
            return ergebnis;
        }
        foreach (var eintrag in liste)
        {
            if (eintrag is JsonObject release && release["tag_name"] is JsonValue t && t.GetValueKind() == JsonValueKind.String
                && Gehoert(t.GetValue<string>(), linie))
            {
                ergebnis.Add(release);
            }
        }
        return ergebnis;
    }
}
