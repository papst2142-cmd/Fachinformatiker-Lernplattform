using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Pruefung;

/// <summary>
/// Fachrichtung der Ausbildung (Nico P4-5: gilt auf allen Geraeten, Vorgabe si). Gespeichert als
/// Feld einer reservierten Zeile in abschlussprojekt, der einzigen abgeglichenen Tabelle mit
/// "neueste gewinnt" und ohne Loeschmarken-Schnitt (kein Schemawechsel, Format 2 unveraendert).
/// Schreiben ueber ProjektFeldSpeichern: feiner Stempel, aeltere Fassungen in derselben Buchung
/// entfernt. Die Projektliste (ab W2 fuer die Kachel) und die Zaehlung der Projekte lassen sie aus.
/// </summary>
public static class Ausbildung
{
    /// <summary>Reservierte Projekt-Kennung; Python-Kennungen sind 12 Hex-Zeichen, kein Treffer moeglich.</summary>
    public const string Zeile = "zz-einstellungen";

    public const string Feld = "fachrichtung";

    public static readonly string[] Werte = ["si", "ae", "dpa", "dv"];

    /// <summary>Gibt es die Zeile schon (auch mit einem unbekannten Wert einer spaeteren Version)? Dann fragt W4 nicht.</summary>
    public static bool Gewaehlt(LernstandSchreiber schreiber) =>
        schreiber.ProjektFelder().TryGetValue(Zeile, out var felder) && felder.ContainsKey(Feld);

    /// <summary>Gespeicherter Wert oder null (nie gewaehlt, unbekannt oder Lesefehler).</summary>
    public static string? Gespeichert(LernstandSchreiber schreiber) =>
        schreiber.ProjektFelder().TryGetValue(Zeile, out var felder)
            && felder.TryGetValue(Feld, out var wert) && Array.IndexOf(Werte, wert) >= 0 ? wert : null;

    /// <summary>Wirksame Fachrichtung: gespeicherter Wert, sonst si.</summary>
    public static string Aktuell(LernstandSchreiber schreiber) => Gespeichert(schreiber) ?? "si";

    /// <summary>Waehlen (Optionen, ab W4 Rundgang). false: nichts gespeichert, Anzeige bleibt beim alten Wert.</summary>
    public static bool Waehlen(LernstandSchreiber schreiber, string wert) =>
        Array.IndexOf(Werte, wert) >= 0 && schreiber.ProjektFeldSpeichern(Zeile, Feld, wert);
}
