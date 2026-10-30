using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 9 W4 (Weisung P9-10 Teil B, Sammelliste 178): Schalter "Miete" der Optionen wie <c>fisi_game.py</c>
/// (Python 0.63.2, Z. 13887-13911: RENT_SETTING, RENT_CHOICES, HOME_SUBTITLE, RENT_HELP, rent_mode,
/// set_rent_mode). Er liegt lokal je Geraet in einstellungen.json und wirkt nur auf den naechsten Umzug;
/// der Mietvertrag selbst steht im Ereignis move_home (PC und Handy rechnen denselben Kontostand).
/// </summary>
public static partial class Wohnung
{
    /// <summary>RENT_SETTING: Schluessel in einstellungen.json (true = Miete).</summary>
    public const string MieteSchluessel = "spiel_miete";

    /// <summary>RENT_CHOICES: Auswahl in den Optionen (Schluessel, Anzeige).</summary>
    public static readonly IReadOnlyList<(string Id, string Text)> MieteWahl = [("einmal", "Einmalzahlung"), ("miete", "Miete")];

    /// <summary>Ueberschrift der Auswahl in der Karte "Spiel" (in Grossbuchstaben gezeigt wie PC "WOHNUNGEN" und Handy ui.label).</summary>
    public const string MieteTitel = "Wohnungen";

    /// <summary>HOME_SUBTITLE: Untertitel der Karte "Wohnung" (false = Einmalzahlung).</summary>
    public static string WohnungUntertitel(bool miete) => miete ? "Größer wohnen: Kaution und Miete je Arbeitstag" : "Größer wohnen kostet einmalig";

    /// <summary>RENT_HELP (Vorlage mit %d fuer den Anteil der Kaution in Prozent).</summary>
    public const string MieteHilfeVorlage = "Gilt für den nächsten Umzug im Spiel. Einmalzahlung: Die Wohnung wird "
        + "gekauft. Miete: nur eine Kaution (%d %% des Kaufpreises), dafür geht "
        + "jeden Arbeitstag die Miete vom Spielgeld ab. Ein laufender Mietvertrag "
        + "gilt bis zum nächsten Umzug. Die Einstellung gilt nur für dieses Gerät.";

    /// <summary>RENT_HELP % round(balancing["miete"]["kaution_anteil"] * 100) wie in den Optionen (PC und Handy gleich).</summary>
    public static string MieteHilfe(PyObjekt balancing) =>
        Prozent(MieteHilfeVorlage, PythonRunden.Runde(Py.F(Objekt(balancing["miete"])["kaution_anteil"]) * 100));

    /// <summary>rent_mode: ist in den Optionen dieses Geraets "Miete" gewaehlt? (bool() des gespeicherten Werts wie Python).</summary>
    public static bool MieteGewaehlt(JsonObject einstellungen) => einstellungen[MieteSchluessel] switch
    {
        null => false,
        JsonValue w when w.TryGetValue(out bool b) => b,
        JsonValue w when w.TryGetValue(out double d) => d != 0,
        JsonValue w when w.TryGetValue(out string? s) => s.Length > 0,
        JsonArray a => a.Count > 0,
        JsonObject o => o.Count > 0,
        _ => false,
    };

    // Phase 9 W5 (Weisung P9-11 Teil B, Sammelliste 179): Karte "Wohnung" beim Einrichten wie Python
    // _build_below (fisi_game_gui.py Z. 4956-4978) und _home_editor (mobile/src/spiel.py Z. 4586-4611), Ablauf _move.

    /// <summary>Titel der Karte "Wohnung".</summary>
    public const string WohnungTitel = "Wohnung";

    /// <summary>"Du wohnst in: %s"</summary>
    public static string WohntIn(PyObjekt bleibe) => Prozent("Du wohnst in: %s", bleibe["name"]);

    /// <summary>Knopf je Wohnung und Titel der Rueckfrage.</summary>
    public const string UmziehenKnopf = "Umziehen";

    /// <summary>Keine groessere Wohnung mehr.</summary>
    public const string GroessteWohnung = "Du wohnst schon in der größten Wohnung.";

    /// <summary>set_rent_mode: der Wert, den die Optionen speichern (settings[RENT_SETTING] = bool(flag)).</summary>
    public static void MieteSetzen(JsonObject einstellungen, bool miete) => einstellungen[MieteSchluessel] = miete;
}
