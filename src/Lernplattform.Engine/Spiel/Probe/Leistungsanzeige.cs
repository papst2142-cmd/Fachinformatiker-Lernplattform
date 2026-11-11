using System.Globalization;
using System.Text.Json.Nodes;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle D2 (Weisung P12-157 Frage 5 = a): Leistungsanzeige im Spiel, nur C#, PC und Handy gleich, Vorgabe aus.
/// Schalter in den Optionen (Diagnose, Abschnitt Leistungsmessung), Merker <see cref="Schluessel"/> in
/// einstellungen.json (nur ueber <c>Einstellungen.Aendern</c>). Ist er an, steht oben links in der Kopfkarte der Stadt
/// eine kleine Zeile mit Bildern je Sekunde, Dreiecken und Aufrufen des letzten Bilds. Nur Anzeige: kein Spielstand,
/// kein Ereignis.
/// </summary>
public static class Leistungsanzeige
{
    public const string Schluessel = "leistungsanzeige";

    public const string Schalter = "Leistungsanzeige im Spiel";

    public const string Hilfe = "Zeigt im Spiel oben links, wie viele Bilder je Sekunde gezeichnet werden und wie viele "
        + "Dreiecke und Zeichenaufrufe das letzte Bild hatte. Nur zur Anzeige, es wird nichts aufgezeichnet.";

    /// <summary>Vorgabe aus: nur ein echtes true schaltet ein.</summary>
    public static bool Lesen(JsonObject werte) => werte[Schluessel] is JsonValue v && v.TryGetValue<bool>(out var an) && an;

    public static void Wert(JsonObject werte, bool an) => werte[Schluessel] = an;

    /// <summary>Text der Zeile, z. B. „60 Bilder/s · 52.957 Dreiecke · 17 Aufrufe“ (Tausender mit Punkt).</summary>
    public static string Zeile(int bilderJeSekunde, long dreiecke, int aufrufe) =>
        string.Create(CultureInfo.InvariantCulture, $"{bilderJeSekunde} Bilder/s · {Zahl(dreiecke)} Dreiecke · {aufrufe} Aufrufe");

    private static string Zahl(long n) => n.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
}
