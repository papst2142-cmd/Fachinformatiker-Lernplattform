using System.Text;
using System.Text.Json.Nodes;

namespace Stadtbau.Core;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-37 Punkt 2): Einstellungen des Werkzeugs in <c>einstellungen.json</c> im Werkzeugordner
/// (heute nur "vollbild"). Unbekannte Schluessel bleiben beim Schreiben erhalten. Fehlt die Datei oder ist sie kaputt,
/// gilt die Vorgabe (kein Vollbild); eine kaputte Datei wird nie still ueberschrieben.
/// </summary>
public static class WerkzeugEinstellungen
{
    public static string Datei(Werkzeugordner o) => Path.Combine(o.Wurzel, "einstellungen.json");

    private static JsonObject? Lesen(Werkzeugordner o, out bool kaputt)
    {
        kaputt = false;
        try
        {
            if (!File.Exists(Datei(o)))
            {
                return null;
            }
            if (JsonNode.Parse(File.ReadAllText(Datei(o), Encoding.UTF8)) is JsonObject obj)
            {
                return obj;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
        }
        kaputt = true;
        return null;
    }

    /// <summary>Vollbild beim Start? (nur der Wert true zaehlt)</summary>
    public static bool Vollbild(Werkzeugordner o) =>
        Lesen(o, out _) is { } obj && obj["vollbild"] is JsonValue v && v.TryGetValue<bool>(out var an) && an;

    /// <summary>Vollbild merken. Wirft IOException, wenn die Datei kaputt ist oder nicht geschrieben werden kann.</summary>
    public static void VollbildSetzen(Werkzeugordner o, bool an)
    {
        var obj = Lesen(o, out var kaputt);
        if (kaputt)
        {
            throw new IOException("einstellungen.json ist nicht lesbar");
        }
        obj ??= [];
        obj["vollbild"] = an;
        Werkzeugordner.SicherSchreiben(Datei(o), Encoding.UTF8.GetBytes(obj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n"));
    }
}
