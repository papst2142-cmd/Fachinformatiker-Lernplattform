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

    [GeneratedRegex(@"^v?S ?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex StadtbauNummerMuster();

    /// <summary>
    /// S 0.6.0: Nummer einer Werkzeug-Version aus Tag ("vS0.6.0") oder Anzeige ("S 0.6.0") fuer den Vergleich; sonst null
    /// (auch jede Zahl, die nicht in int passt).
    /// </summary>
    public static (int Haupt, int Neben, int Fix)? StadtbauNummer(string? text)
    {
        if (text is null || StadtbauNummerMuster().Match(text) is not { Success: true } m)
        {
            return null;
        }
        var z = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (!int.TryParse(m.Groups[i + 1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out z[i]))
            {
                return null;
            }
        }
        return (z[0], z[1], z[2]);
    }

    /// <summary>Anzeige "S 0.6.0" zu einer Nummer.</summary>
    public static string StadtbauAnzeige((int Haupt, int Neben, int Fix) nummer) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"S {nummer.Haupt}.{nummer.Neben}.{nummer.Fix}");

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
