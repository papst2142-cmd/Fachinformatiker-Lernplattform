using System.Text.Json;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Laedt die Lerninhalte (JSON-Dateien aus der Python-Linie, unveraendert).
/// Phase 0: nur Laden und Zaehlen (Spike C). Das Datenmodell folgt in Phase 1.
/// </summary>
public static class Inhalte
{
    private static readonly JsonDocumentOptions Optionen = new() { AllowTrailingCommas = false };

    /// <summary>Alle *.json unter dem Ordner, Schluessel = relativer Pfad mit "/".</summary>
    public static Dictionary<string, JsonDocument> LadeAlle(string ordner)
    {
        var ergebnis = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        foreach (var pfad in Directory.EnumerateFiles(ordner, "*.json", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(ordner, pfad).Replace(Path.DirectorySeparatorChar, '/');
            ergebnis[name] = JsonDocument.Parse(File.ReadAllBytes(pfad), Optionen);
        }
        return ergebnis;
    }

    /// <summary>Zahl aller Texte und deren UTF-8-Laenge (Vergleich mit Python).</summary>
    public static (long Texte, long Bytes) ZaehleTexte(JsonElement wert)
    {
        long texte = 0, bytes = 0;
        void Gehe(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    texte++;
                    bytes += System.Text.Encoding.UTF8.GetByteCount(e.GetString()!);
                    break;
                case JsonValueKind.Array:
                    foreach (var x in e.EnumerateArray())
                    {
                        Gehe(x);
                    }
                    break;
                case JsonValueKind.Object:
                    foreach (var x in e.EnumerateObject())
                    {
                        Gehe(x.Value);
                    }
                    break;
            }
        }
        Gehe(wert);
        return (texte, bytes);
    }

    /// <summary>Zahl der Eintraege wie Pythons len(): Liste = Elemente, Objekt = Schluessel.</summary>
    public static int Eintraege(JsonElement wert) => wert.ValueKind switch
    {
        JsonValueKind.Array => wert.GetArrayLength(),
        JsonValueKind.Object => wert.EnumerateObject().Count(),
        _ => 0,
    };
}
