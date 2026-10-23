using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Mathe;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 5 BE4: Texte der Bereiche „Löschen und zurücksetzen“ und „Datenbank“. Mit (Py) markierte
/// Teile zeichengleich aus Python v0.63.2 (fisi_optionen, fisi_lernen, app_gui; Vergleichswerte
/// G40). Rückfragen und Rückmeldungen wie am PC (PC-Wortlaut überall, Nico P2-2). (W9) weicht ab
/// und steht in der Wortliste W1. Spielstand und Bestenliste bleiben bis zum Spiel Platzhalter.
/// </summary>
public static class LoeschenTexte
{
    public const string Lerndaten = "LERNDATEN";                                              // (Py)
    public const string LerndatenText = "Setzt sämtliche Lernfortschritte zurück: Testergebnisse, "
        + "Klausursimulationen, Karteikarten-Verlauf, Antworten im "
        + "Prüfungstrainer, Aufgaben im Rechen-Trainer und bearbeitete "
        + "Szenarien und Testprojekte. Erhalten bleiben das Abschlussprojekt, "
        + "deine Einstellungen (z. B. das Tagesziel) und der Spielstand des "
        + "Lernspiels; das Wissen im Spiel beginnt aber wieder bei 0. Mit "
        + "eingerichtetem Abgleich gilt das Zurücksetzen auf allen abgeglichenen "
        + "Geräten. Dieser Schritt lässt sich nicht rückgängig machen.";                      // (Py) RESET_ALL_TEXT
    public const string LerndatenKnopf = "Alle Lerndaten löschen";                            // (Py)
    public const string LerndatenFrageTitel = "Alles zurücksetzen";                           // (Py)
    public const string LerndatenFrage = "Wirklich ALLE Lerndaten unwiderruflich löschen?";    // (Py)
    public const string LerndatenErfolgTitel = "Zurückgesetzt";                               // (Py)
    public const string LerndatenErfolg = "Alle Lerndaten wurden gelöscht.";                  // (Py)

    public const string Historie = "HISTORIE";                                                // (Py) HISTORY_LABEL.upper()
    public const string HistorieText = "Löscht alle gespeicherten Testergebnisse und Prüfungen, die im "
        + "„Fortschritt“ stehen. Die übrigen Lerndaten (Tagesziel, Lernserie, "
        + "Aufgaben pro Tag) bleiben erhalten. Dieser Schritt lässt sich nicht "
        + "rückgängig machen.";                                                               // (Py) HISTORY_TEXT
    public const string HistorieKnopf = "Historie löschen";                                   // (Py) HISTORY_BUTTON
    public const string HistorieFrageTitel = "Historie löschen";                              // (Py)
    public const string HistorieFrage = "Wirklich alle gespeicherten Testergebnisse und Prüfungen löschen?\n\n"
        + "Die Lern-Ereignisse für das Dashboard bleiben erhalten.";                          // (Py) PC
    public const string HistorieErfolgTitel = "Gelöscht";                                     // (Py)
    public const string HistorieErfolg = "Die Historie wurde gelöscht.";                      // (Py) DELETE_HISTORY_DONE
    public const string NichtsGeloescht = "Es wurde nichts gelöscht.";                        // (Py) DELETE_NOTHING

    public const string Spielstand = "SPIELSTAND";                                            // (Py)
    public const string SpielstandKnopf = "Spielstand zurücksetzen";                          // (Py)
    public const string Bestenliste = "BESTENLISTE";                                          // (Py)
    public const string BestenlisteKnopf = "Bestenliste löschen";                             // (Py)

    public const string DatenbankTitel = "Speicherort der Lernfortschritte:";                 // (Py)

    /// <summary>(W9) LERNPLATTFORM_DATEN ist ein Ordner (Python: FISI_DB_PATH, eine Datei).</summary>
    public const string DatenbankHinweis = "Der Ordner lässt sich über die Umgebungsvariable LERNPLATTFORM_DATEN "
        + "überschreiben, zum Beispiel um die Datenbank auf einem Netzlaufwerk abzulegen. Dort liegen dann "
        + "auch die Einstellungen und der Ordner „sicherungen“.";

    /// <summary>_with_clock_fix: Rückmeldung, bei korrigierter Marke mit Leerzeile und Hinweis.</summary>
    public static string MitUhrHinweis(string text, double korrektur) =>
        Uhrtexte.Korrektur(korrektur) is { Length: > 0 } hinweis ? text + "\n\n" + hinweis : text;

    /// <summary>_delete_checked: „Es wurde nichts gelöscht.“ und die gesammelten Datenbanktexte, je mit Leerzeile.</summary>
    public static string NichtsGeloeschtMit(IEnumerable<string> meldungen) => string.Join("\n\n", meldungen.Prepend(NichtsGeloescht));
}

/// <summary>fisi_sync.clock_fix_text: Hinweis, wenn eine Löschung mit der Uhrzeit von GitHub gespeichert wurde.</summary>
public static class Uhrtexte
{
    /// <summary>sekunden = Geräteuhr minus GitHub; 0 = kein Hinweis (leerer Text).</summary>
    public static string Korrektur(double sekunden)
    {
        if (sekunden == 0)
        {
            return "";
        }
        var groesse = Math.Abs(sekunden);
        var menge = groesse >= 86400 ? DashboardTexte.Plural(PythonRunden.Runde(groesse / 86400.0), "Tag", "Tage")
            : groesse >= 3600 ? DashboardTexte.Plural(PythonRunden.Runde(groesse / 3600.0), "Stunde", "Stunden")
            : DashboardTexte.Plural(Math.Max(1, PythonRunden.Runde(groesse / 60.0)), "Minute", "Minuten");
        return "Die Uhr dieses Geräts geht um " + menge + " " + (sekunden > 0 ? "vor" : "nach")
            + ". Die Löschung wurde mit der Uhrzeit von GitHub gespeichert.";
    }
}
