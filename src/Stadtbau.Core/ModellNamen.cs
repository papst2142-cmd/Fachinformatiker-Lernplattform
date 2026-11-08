using Lernplattform.Core.Daten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.24.0 (Weisung P12-100, Nico 4a): deutsche Namen der vorgegebenen Gebaeude-Modelle fuer die Liste „Modell“ (die
/// Kennung steht im Tipp). Namen wie im Spiel (Orte der Stadt 1, neue Orte), fuer Modelle ohne Ort ein fester Name.
/// Eine Kopie aus „Tür dazu“ heisst wie ihr Grundmodell mit Nummer („Wohnung 2“). Unbekannte Modelle (z. B. eigene
/// Gebaeude aus Blueprints) behalten die Kennung.
/// </summary>
public static class ModellNamen
{
    /// <summary>Fester Name je Kennung (Kennung → Name).</summary>
    public static readonly IReadOnlyDictionary<string, string> Namen = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["amt"] = "Amt",
        ["bahnhof"] = "Bahnhof Talheim",
        ["bahnhof_neu"] = "Talheim-Nord",
        ["bank"] = "Bank",
        ["bitweiche"] = "Bitweiche",
        ["elektromarkt"] = "Elektromarkt",
        ["filiale"] = "Filiale",
        ["gewerbehof"] = "Gewerbehof",
        ["haltepunkt"] = "Haltepunkt Birkenhain",
        ["kaufhaus"] = "Kaufhaus",
        ["lokschuppen"] = "Lokschuppen",
        ["moebelhaus"] = "Möbelhaus",
        ["stellwerk"] = "Stellwerk",
        ["verwaltung"] = "Talbahn-Verwaltung",
        ["werkstatt"] = "Betriebswerkstatt",
        ["wohnung"] = "Wohnung",
    };

    /// <summary>Bahnhoefe (gelber Hinweis fern vom Gleis).</summary>
    public static bool IstBahnhof(string modell) => modell is "bahnhof" or "bahnhof_neu" or "haltepunkt";

    /// <summary>Anzeigename eines Modells der Stadt (Kopie mit Nummer).</summary>
    public static string Anzeige(PyObjekt aussen, string modell)
    {
        var (grund, nummer) = GebaeudeTuer.Teile(aussen, modell);
        var name = Namen.TryGetValue(grund, out var n) ? n : grund;
        return nummer is { } k ? name + " " + k.ToString(System.Globalization.CultureInfo.InvariantCulture) : name;
    }
}
