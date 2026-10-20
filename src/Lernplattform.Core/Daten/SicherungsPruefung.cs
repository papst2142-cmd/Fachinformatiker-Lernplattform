using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Phase 0, Spike C mit echtem Lernstand (Auflage A3, Weisung CC P0-3 Abschnitt 2).
/// Prueft eine Sicherungsdatei der Python-Linie nur im Speicher und gibt nur aus:
/// Zaehlwerte je bekannter Tabelle, die Zusammenfassung, "Pruefsumme stimmt ja/nein"
/// und SHA-256 der Datei. Keine Pfade, keine Dateinamen, keine Inhalte, keine
/// Fehlertexte (bei Fehler nur ein fester Code). Kein Schreiben, keine Temp-Kopie,
/// kein Log, kein Netz. Namen aus der Datei werden nie ausgegeben: Tabellen und
/// Zusammenfassung nur ueber die festen Listen unten.
/// </summary>
public static class SicherungsPruefung
{
    /// <summary>Die 12 Tabellen der Sicherung (fisi_core.SYNC_TABLES, v0.63.2).</summary>
    public static readonly IReadOnlyList<string> BekannteTabellen =
    [
        "abschlussprojekt", "ap1_events", "card_events", "project_events", "pruefungen", "quiz_answers",
        "scenario_events", "spiel_bestenliste", "spiel_ereignisse", "spiel_plaetze", "test_results", "trainer_aufgaben",
    ];

    /// <summary>Schluessel der Zusammenfassung (fisi_sicherung._summary_counts, v0.63.2).</summary>
    public static readonly IReadOnlyList<string> BekannteZusammenfassung = ["lerneintraege", "spielstaende", "abzeichen", "projekte"];

    /// <summary>Feste Fehlercodes; nie ein Fehlertext.</summary>
    public const string FehlerKeinGzip = "C1";
    public const string FehlerKeinJson = "C2";
    public const string FehlerZuNeu = "C3";
    public const string FehlerAufbau = "C4";
    public const string FehlerSonst = "C9";

    public static IReadOnlyList<string> Pruefe(byte[] daten)
    {
        var inv = CultureInfo.InvariantCulture;
        var zeilen = new List<string>();
        try
        {
            using var doc = AlterLernstand.LeseSicherung(new MemoryStream(daten, writable: false));
            var w = doc.RootElement;
            zeilen.Add("Pruefsumme stimmt: " + (AlterLernstand.PruefsummeStimmt(doc) ? "ja" : "nein"));
            var tabellen = w.GetProperty("daten").GetProperty("tables");
            var weitere = 0;
            foreach (var t in tabellen.EnumerateObject())
            {
                if (!BekannteTabellen.Contains(t.Name, StringComparer.Ordinal))
                {
                    weitere++;
                }
            }
            foreach (var name in BekannteTabellen)
            {
                zeilen.Add(tabellen.TryGetProperty(name, out var t) && t.ValueKind == JsonValueKind.Array
                    ? string.Create(inv, $"Tabelle {name}: {t.GetArrayLength()}")
                    : $"Tabelle {name}: fehlt");
            }
            zeilen.Add(string.Create(inv, $"Weitere Tabellen (ohne Namen): {weitere}"));
            var zusammen = w.TryGetProperty("zusammenfassung", out var z) ? z : default;
            foreach (var name in BekannteZusammenfassung)
            {
                zeilen.Add(zusammen.ValueKind == JsonValueKind.Object && zusammen.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number
                    ? string.Create(inv, $"Zusammenfassung {name}: {e.GetInt64()}")
                    : $"Zusammenfassung {name}: fehlt");
            }
        }
        catch (Exception fehler)
        {
            zeilen.Clear();
            zeilen.Add("FEHLER " + Code(fehler));
        }
        zeilen.Add("SHA-256 Datei: " + Convert.ToHexStringLower(SHA256.HashData(daten)));
        return zeilen;
    }

    private static string Code(Exception fehler) => fehler switch
    {
        InvalidDataException when fehler.Message.StartsWith("Sicherung zu neu", StringComparison.Ordinal) => FehlerZuNeu,
        InvalidDataException => FehlerKeinGzip,
        JsonException => FehlerKeinJson,
        KeyNotFoundException or InvalidOperationException or FormatException => FehlerAufbau,
        _ => FehlerSonst,
    };
}
