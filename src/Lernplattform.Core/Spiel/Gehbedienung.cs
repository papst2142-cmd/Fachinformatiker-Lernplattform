using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.95.0 (Weisung P12-306, Nico Antwort 1c): Schalter „Antippen zum Gehen“ in den Optionen (Karte „Spiel“), am PC
/// und am Handy gleich. Standard: an (Antippen und die Gehtasten gehen beide). Aus: ein Tipp oder Klick in die 3D-Ansicht
/// laesst die Figur nicht mehr gehen; Gehen geht dann nur mit den Tasten (Handy: mit dem Stick, Welle 2). Ortsliste, Karte,
/// „Hineingehen“ per Knopf und die Bedienung ohne 3D bleiben unberuehrt. Gespeichert wird nur „aus“ (Standard = kein Eintrag)
/// ueber <see cref="Einstellungen.Aendern"/>; ein kaputter Wert gilt als Standard (nie still „aus“).
/// </summary>
public static class Gehbedienung
{
    public const string Schluessel = "antippen_gehen";

    /// <summary>Gilt gerade (setzt die Oberflaeche beim Start und nach jeder Aenderung).</summary>
    public static bool AntippenAn { get; set; } = true;

    /// <summary>Wert aus einstellungen.json: nur ein echtes <c>false</c> schaltet aus.</summary>
    public static bool Lesen(JsonObject werte) =>
        !(werte[Schluessel] is JsonValue v && v.TryGetValue<bool>(out var an) && !an);

    /// <summary>In einer Aenderung setzen (an = Standard: Eintrag entfernen).</summary>
    public static void Wert(JsonObject werte, bool an)
    {
        if (an)
        {
            werte.Remove(Schluessel);
        }
        else
        {
            werte[Schluessel] = false;
        }
    }
}
