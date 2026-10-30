using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;

namespace Lernplattform.Core.Projekt;

/// <summary>Eine Frage fuers Fachgespraech; Kategorie = voller Fachbereich oder null (allgemein).</summary>
public sealed record ProjektFrage(string Id, string? Kategorie, string Frage, string Hinweis);

/// <summary>
/// Phase 4, W3: Fragen fuers Fachgespraech wie fisi_projekt._load_questions v0.63.2 aus
/// inhalte/fachgespraech.json (Liste "fragen"). "cat" wird ueber CATEGORY_KEYS zum vollen Namen,
/// leer bleibt null, ein unbekannter Kurzname wird null (wie Python dict.get).
/// </summary>
public static class Fachgespraech
{
    public static List<ProjektFrage> Laden(byte[] datei)
    {
        var daten = JsonNode.Parse(datei) as JsonObject ?? throw new InvalidDataException("fachgespraech.json enthaelt kein Objekt");
        var fragen = daten["fragen"] as JsonArray ?? throw new InvalidDataException("fachgespraech.json: Feld 'fragen' fehlt");
        return fragen.Select(f => Frage(f as JsonObject ?? throw new InvalidDataException("fachgespraech.json: Eintrag ist kein Objekt"))).ToList();
    }

    /// <summary>Ein Eintrag der Liste "fragen" (auch inhalte_csharp/lernen/fachgespraech.json, Phase 10 W2).</summary>
    public static ProjektFrage Frage(JsonObject eintrag)
    {
        var kurz = eintrag["cat"]?.GetValue<string>();
        var kategorie = string.IsNullOrEmpty(kurz) ? null : Fachbereiche.Schluessel.GetValueOrDefault(kurz);
        return new ProjektFrage(eintrag["id"]!.GetValue<string>(), kategorie, eintrag["q"]!.GetValue<string>(),
            eintrag["hinweis"]!.GetValue<string>());
    }

    public static List<ProjektFrage> Laden(string inhalteOrdner) => Laden(File.ReadAllBytes(Path.Combine(inhalteOrdner, "fachgespraech.json")));
}
