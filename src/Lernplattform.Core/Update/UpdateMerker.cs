using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Update;

/// <summary>Ergebnis der Pruefung beim Start: Ok mit der laufenden Version, sonst Fehler mit dem Meldungstext.</summary>
public sealed record UpdateAbschluss(bool Ok, string Text);

/// <summary>
/// Phase 5 BE9: Merker "Update unterwegs" (update_ausstehend) in einstellungen.json wie
/// remember_pending_update / forget_pending_update / finish_pending_update (fisi_update.py).
/// Geschrieben wird nur ueber <see cref="Einstellungen.Aendern"/> (Sperre aus W2, D8). Die Pruefung
/// beim Start entfernt den Merker immer, die Meldung kommt also genau einmal.
/// </summary>
public static class UpdateMerker
{
    public const string Schluessel = "update_ausstehend";

    /// <summary>Vor dem Start des Installers: welche Version kommen soll (Anzeige, z. B. "A 0.11.1").</summary>
    public static bool Merken(Einstellungen einstellungen, string version, DateTime? jetzt = null) =>
        einstellungen.Aendern(werte => werte[Schluessel] = new JsonObject
        {
            ["version"] = version,
            ["zeit"] = (jetzt ?? DateTime.Now).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        });

    public static bool Vergessen(Einstellungen einstellungen) => einstellungen.Aendern(werte => werte.Remove(Schluessel));

    /// <summary>
    /// finish_pending_update: null (kein Merker), Ok (neue Version laeuft oder Merker ohne Ziel) oder Fehler
    /// mit dem Text fuer das Fenster "Update nicht abgeschlossen". system wie Fehlerprotokoll.System()
    /// waehlt den Tipp (Linux: AppImage, sonst Installer; Wortliste W3). ordner: Datenordner (update.log).
    /// </summary>
    public static UpdateAbschluss? Abschliessen(Einstellungen einstellungen, string aktuell, string system, string? ordner = null)
    {
        if (einstellungen.Laden()[Schluessel] is not JsonObject merker)
        {
            return null;
        }
        var ziel = Text(merker["version"]);
        Vergessen(einstellungen);
        if (ziel.Length == 0 || !UpdateVersion.IstNeuer(ziel, aktuell))
        {
            Updateprotokoll.Schreiben("Update auf " + (ziel.Length > 0 ? ziel : "?") + " abgeschlossen, Programm läuft als " + aktuell + ".", ordner);
            return new UpdateAbschluss(true, aktuell);
        }
        Updateprotokoll.Schreiben("Update auf " + ziel + " NICHT abgeschlossen, installiert ist weiterhin " + aktuell + ".", ordner);
        return new UpdateAbschluss(false, Fehlschlag(ziel, aktuell, system, ordner));
    }

    /// <summary>FAILED_TEXT mit Ziel, laufender Version, Grund (immer REASON_UNKNOWN), Pfad von update.log und Tipp je System.</summary>
    public static string Fehlschlag(string ziel, string aktuell, string system, string? ordner = null) => UpdateTexte.FehlschlagText
        .Replace("%(neu)s", ziel, StringComparison.Ordinal)
        .Replace("%(alt)s", aktuell, StringComparison.Ordinal)
        .Replace("%(grund)s", UpdateTexte.GrundUnbekannt, StringComparison.Ordinal)
        .Replace("%(pfad)s", Updateprotokoll.Pfad(ordner), StringComparison.Ordinal)
        .Replace("%(tipp)s", system == "linux" ? UpdateTexte.TippAppImage : UpdateTexte.TippWindows, StringComparison.Ordinal);

    // str(pending.get("version") or ""): leer bei fehlendem, leerem oder falschem Wert
    private static string Text(JsonNode? wert) => wert switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "True" : "",
        JsonValue v when v.TryGetValue<double>(out var d) => d == 0 ? "" : v.ToJsonString(),
        null => "",
        _ => wert.ToJsonString(),
    };
}
