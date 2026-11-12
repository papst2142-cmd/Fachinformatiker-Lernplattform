using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

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
        [Zusatzmodelle.HaltepunktFrei] = "Haltepunkt mit freiem Bahnsteig",
        ["kaufhaus"] = "Kaufhaus",
        ["lokschuppen"] = "Lokschuppen",
        ["moebelhaus"] = "Möbelhaus",
        ["stellwerk"] = "Stellwerk",
        ["verwaltung"] = "Talbahn-Verwaltung",
        ["werkstatt"] = "Betriebswerkstatt",
        ["wohnung"] = "Wohnung",
    };

    /// <summary>Bahnhoefe (gelber Hinweis fern vom Gleis).</summary>
    /// <remarks>Weisung P12-214: das Modell von Talheim-Nord (<c>bahnhof_neu</c>) zaehlt nicht mehr als Bahnhof; Talheim-Nord wird das
    /// Stellwerk und braucht kein Gleis (vorher gelber Fund „steht nicht an einem Gleis“, Gegenprobe <see cref="TalheimAltFuerTests"/>).</remarks>
    public static bool IstBahnhof(string modell) => modell is "bahnhof" or "haltepunkt" or Zusatzmodelle.HaltepunktFrei || (TalheimAlt.Value && modell == "bahnhof_neu");

    /// <summary>Gegenprobe P12-214: Talheim-Nord-Modell zaehlt als Bahnhof (wie bis S 0.44.1, nur Tests, je Ablauf).</summary>
    public static bool TalheimAltFuerTests { get => TalheimAlt.Value; set => TalheimAlt.Value = value; }
    private static readonly AsyncLocal<bool> TalheimAlt = new();

    /// <summary>Anzeigename eines Modells der Stadt (Kopie mit Nummer).</summary>
    public static string Anzeige(PyObjekt aussen, string modell)
    {
        var (grund, nummer) = GebaeudeTuer.Teile(aussen, modell);
        var name = Namen.TryGetValue(grund, out var n) ? n : grund;
        return nummer is { } k ? name + " " + k.ToString(System.Globalization.CultureInfo.InvariantCulture) : name;
    }

    /// <summary>Gegenprobe: Beschriftung bis S 0.24.0 (Kennung des Modells), nur in Tests, je Ablauf.</summary>
    internal static readonly AsyncLocal<bool> BeschriftungAltFuerTests = new();

    /// <summary>
    /// Welle S 0.25.0 (Sammelliste 247, Weisung P12-104): Beschriftung eines Gebaeudes ohne Ort in der Draufsicht wie in der
    /// Liste „Modell“: eigenes Gebaeude mit seinem Namen, Bahnsteig als „Bahnsteig“, sonst <see cref="Anzeige"/>.
    /// </summary>
    public static string Beschriftung(PyObjekt aussen, AussenStelle s)
    {
        if (AussenBearbeitung.BauName(aussen, s) is { } bau)
        {
            return bau;
        }
        var modell = Py.Str(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["modell"]);
        if (BeschriftungAltFuerTests.Value)
        {
            return modell;
        }
        return modell.StartsWith(Bahnsteig.ModellPraefix, StringComparison.Ordinal) ? Texte.Bahnsteig : Anzeige(aussen, modell);
    }
}
