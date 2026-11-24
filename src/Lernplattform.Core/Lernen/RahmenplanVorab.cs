using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Lernplattform.Core.Lernen;

/// <summary>
/// Weisung P12-369 (Nico 24.11.2026, "jetzt schon beheben"): fertige Rahmenplan-Zuordnung (Punkte je Inhalt) aus einer
/// beim Bau eingebetteten Tabelle. Das Suchen der 144 Stichwort-Muster in jedem Inhalt kostete beim Start kalt
/// 300-650 ms und hielt "Karteikarten bis erste Karte" auf (Lauf 1208 ubuntu: 847 ms Warten auf die Gewichte).
/// Die Tabelle gilt nur, wenn die Regeln gleich sind (Pruefsumme von rahmenplan_zuordnung), und je Inhalt nur, wenn
/// Quelle, Thema und Text gleich sind (Pruefsumme als Schluessel); alles andere (neue Inhalte, Pakete, geaenderte Texte)
/// wird wie bisher gerechnet. Das Ergebnis ist gleich (Test RahmenplanVorabTests mit Gegenprobe). Neu schreiben mit
/// LERNPLATTFORM_VORAB_SCHREIBEN=1 und dem Test RahmenplanVorabTests.Tabelle_GleichDerRechnung.
/// </summary>
public static class RahmenplanVorab
{
    public const string Datei = "rahmenplan_vorab.json";
    private const string Ressource = "Lernplattform.Core.Lernen.rahmenplan_vorab.json";

    private static readonly Lazy<(string Regeln, Dictionary<string, string[]> Punkte)?> Tabelle = new(Laden);

    /// <summary>Punkte je Schluessel, wenn die Tabelle zu diesen Regeln gehoert, sonst null.</summary>
    public static IReadOnlyDictionary<string, string[]>? Fuer(JsonNode zuordnung) =>
        Tabelle.Value is { } t && t.Regeln == Regeln(zuordnung) ? t.Punkte : null;

    /// <summary>Pruefsumme der Regeln (rahmenplan_zuordnung als kompakter Text).</summary>
    public static string Regeln(JsonNode zuordnung) => Summe(zuordnung.ToJsonString());

    /// <summary>Schluessel eines Inhalts: Quelle, Thema und die durchsuchten Texte (vor der Kleinschreibung).</summary>
    public static string Schluessel(string quelle, string? thema, string text) =>
        Summe(quelle + "\u001f" + (thema is null ? "\u0000" : thema) + "\u001f" + text);

    private static string Summe(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 12);

    private static (string, Dictionary<string, string[]>)? Laden()
    {
        try
        {
            using var strom = typeof(RahmenplanVorab).Assembly.GetManifestResourceStream(Ressource);
            if (strom is null)
            {
                return null;
            }
            var json = JsonNode.Parse(strom)!.AsObject();
            var punkte = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var (schluessel, liste) in json["punkte"]!.AsObject())
            {
                punkte[schluessel] = liste!.AsArray().Select(p => (string)p!).ToArray();
            }
            return ((string)json["regeln"]!, punkte);
        }
        catch (Exception fehler)
        {
            Diagnose.Fehlerprotokoll.Schreiben(fehler);
            return null;
        }
    }

    /// <summary>Tabelle als Text (sortiert, eine Zeile je Inhalt) aus den gerechneten Punkten; nur fuer Test und Werkzeug.</summary>
    public static string Schreiben(JsonNode zuordnung, IEnumerable<(string Schluessel, IReadOnlyList<string> Punkte)> eintraege)
    {
        var text = new StringBuilder();
        text.Append("{\"regeln\": \"").Append(Regeln(zuordnung)).Append("\", \"punkte\": {\n");
        var zeilen = eintraege.GroupBy(e => e.Schluessel, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => "\"" + g.Key + "\": [" + string.Join(", ", g.First().Punkte.Select(p => JsonValue.Create(p).ToJsonString())) + "]");
        text.Append(string.Join(",\n", zeilen)).Append("\n}}\n");
        return text.ToString();
    }
}
