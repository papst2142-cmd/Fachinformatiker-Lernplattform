using System.Text;
using System.Text.Json.Nodes;

namespace Stadtbau.Core;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-37 Punkt 2): Einstellungen des Werkzeugs in <c>einstellungen.json</c> im Werkzeugordner
/// ("vollbild", seit S 0.9.1 "raster_zeigen" im Gebaeude-Editor, Weisung P12-52, seit S 0.17.0 "gruppe_links"). Unbekannte Schluessel bleiben beim Schreiben erhalten. Fehlt die Datei oder ist sie kaputt,
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
    public static void VollbildSetzen(Werkzeugordner o, bool an) => Setzen(o, "vollbild", an);

    /// <summary>Weisung P12-52 (Wunsch Nico): Raster im Gebaeude-Editor zeigen? Vorgabe an; nur der Wert false blendet aus.</summary>
    public static bool RasterZeigen(Werkzeugordner o) =>
        !(Lesen(o, out _) is { } obj && obj["raster_zeigen"] is JsonValue v && v.TryGetValue<bool>(out var an) && !an);

    /// <summary>Raster zeigen merken (Fehler wie <see cref="VollbildSetzen"/>).</summary>
    public static void RasterZeigenSetzen(Werkzeugordner o, bool an) => Setzen(o, "raster_zeigen", an);

    /// <summary>Schluessel der offenen Gruppe links (Welle Q).</summary>
    public const string GruppeSchluessel = "gruppe_links";

    /// <summary>
    /// Welle Q (Weisung P12-76 Punkt 6): welche Gruppe links in „Stadt von oben“ offen ist (Kennung der Gruppe). Fehlt der
    /// Wert, ist er kein Text oder kennt das Werkzeug die Gruppe nicht: <paramref name="vorgabe"/>. Leerer Text = alle zu.
    /// </summary>
    public static string OffeneGruppe(Werkzeugordner o, IReadOnlyCollection<string> bekannt, string vorgabe) =>
        Lesen(o, out _) is { } obj && obj[GruppeSchluessel] is JsonValue v && v.TryGetValue<string>(out var g) && (g.Length == 0 || bekannt.Contains(g)) ? g : vorgabe;

    /// <summary>Offene Gruppe merken (Fehler wie <see cref="VollbildSetzen"/>).</summary>
    public static void OffeneGruppeSetzen(Werkzeugordner o, string gruppe) => Setzen(o, GruppeSchluessel, JsonValue.Create(gruppe));

    private static void Setzen(Werkzeugordner o, string schluessel, bool an) => Setzen(o, schluessel, JsonValue.Create(an));

    private static void Setzen(Werkzeugordner o, string schluessel, JsonNode wert)
    {
        var obj = Lesen(o, out var kaputt);
        if (kaputt)
        {
            throw new IOException("einstellungen.json ist nicht lesbar");
        }
        obj ??= [];
        obj[schluessel] = wert;
        Werkzeugordner.SicherSchreiben(Datei(o), Encoding.UTF8.GetBytes(obj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n"));
    }
}
