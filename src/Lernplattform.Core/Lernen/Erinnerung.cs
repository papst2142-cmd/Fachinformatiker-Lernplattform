using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Lernen;

/// <summary>
/// Phase 11 W1 (Sammelliste 180): Erinnerung ans Tagesziel wie Python v0.63.2 (fisi_lernen.reminder_due,
/// reminder_text; app_gui.check_reminder, mobile/src/main.py check_reminder). Einmal am Tag
/// (einstellungen.json "erinnerung_gezeigt" = Datum der letzten Erinnerung), ab der eingestellten Uhrzeit
/// und nur, solange das Tagesziel offen ist. Gezeigt wird ein Hinweis fuer 9 Sekunden (REMINDER_TOAST_MS).
/// </summary>
public static class Erinnerung
{
    /// <summary>Schluessel in einstellungen.json mit dem Tag der letzten Erinnerung (ISO-Datum).</summary>
    public const string GezeigtSchluessel = "erinnerung_gezeigt";

    /// <summary>REMINDER_TOAST_MS (fisi_core 2550).</summary>
    public const int Dauer = 9000;

    /// <summary>Am PC alle 5 Minuten, solange das Programm laeuft (app_gui 11093).</summary>
    public static readonly TimeSpan PcAbstand = TimeSpan.FromMinutes(5);

    /// <summary>Erste Pruefung nach dem Start (PC root.after(8000), Handy asyncio.sleep(8)).</summary>
    public static readonly TimeSpan StartNach = TimeSpan.FromSeconds(8);

    /// <summary>settings.get("erinnerung_gezeigt", ""): nur ein Text zaehlt (Python vergleicht mit ==).</summary>
    public static string GezeigtAm(JsonObject werte) =>
        werte[GezeigtSchluessel] is JsonValue v && v.TryGetValue<string>(out var tag) ? tag : "";

    /// <summary>
    /// reminder_due: Soll jetzt erinnert werden? Nur mit Tagesziel und Erinnerung an, nur wenn das Ziel
    /// (erledigt &lt; Ziel) noch offen ist, hoechstens einmal am Tag und ab der eingestellten Uhrzeit
    /// (Vergleich der Texte "HH:MM" wie Python).
    /// </summary>
    public static bool Faellig(Einstellungen.Tagesziel ziel, long erledigt, DateTime jetzt, string gezeigtAm)
    {
        if (!(ziel.ZielAn && ziel.ErinnerungAn) || erledigt >= ziel.ZielAnzahl)
        {
            return false;
        }
        if (gezeigtAm == Tag(jetzt))
        {
            return false;
        }
        return string.CompareOrdinal(jetzt.ToString("HH:mm", CultureInfo.InvariantCulture), ziel.ErinnerungZeit) >= 0;
    }

    /// <summary>date.today().isoformat()</summary>
    public static string Tag(DateTime jetzt) => jetzt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>reminder_text</summary>
    public static string Text(long erledigt, int ziel) => string.Create(CultureInfo.InvariantCulture,
        $"Dein Tagesziel ist noch offen: {erledigt} von {ziel} Aufgaben, es fehlen noch {Math.Max(0, ziel - erledigt)}.");
}
