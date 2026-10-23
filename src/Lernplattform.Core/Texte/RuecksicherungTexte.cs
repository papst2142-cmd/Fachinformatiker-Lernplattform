using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 2, L9: Texte zu „Sicherung für die alte App erstellen“ nach dem verbindlichen Wortlaut
/// (Fi-Austausch anlagen/P2-11_Wortlaut_W4.md, Weisung P2-11 F2). Mit (Py) markierte Teile
/// zeichengleich aus Python v0.63.2 (app_gui, fisi_sicherung).
/// </summary>
public static class RuecksicherungTexte
{
    /// <summary>T1 Knopf.</summary>
    public const string Knopf = "Sicherung erstellen";                                   // (Py) BTN_CREATE (Frage 2: ersetzt „Sicherung für die alte App erstellen“)

    // T2 (Erklärung unter dem Knopf) entfällt mit Frage 2: dort steht jetzt SicherungTexte.Hilfe und (W1) HilfeAlteApp.

    /// <summary>Titel des Speichern-Dialogs (T1) und Dateityp im Filter.</summary>
    public const string DateiTyp = "Lernstand-Sicherung";                                // (Py) FILE_TYPE

    /// <summary>Wartehinweis, solange die Sicherung entsteht (R4).</summary>
    public const string Warten = "Bitte warten – die Sicherung wird erstellt.";      // (Py) WAIT_BACKUP_TEXT

    /// <summary>T3 Lernstand nicht lesbar.</summary>
    public const string NichtErstelltTitel = "Sicherung nicht erstellt";
    public const string NichtErstelltText = "Der Lernstand kann nicht gelesen werden, deshalb wurde keine Sicherung erstellt. Es wurde nichts verändert.";

    /// <summary>T4 Schreiben gescheitert.</summary>
    public const string SpeichernTitel = "Speichern fehlgeschlagen";                     // (Py) SAVE_ERROR_TITLE
    public const string SpeichernText = "Die Sicherung konnte nicht gespeichert werden: %s. Wähle einen anderen Ordner oder schaffe Platz und versuche es noch einmal.";
    public const string GrundPlatz = "Kein Speicherplatz mehr";
    public const string GrundBerechtigung = "Keine Berechtigung für diesen Ordner";
    public const string GrundUnbekannt = "Unbekannter Fehler";

    /// <summary>Erfolg (R12): „Gespeichert: &lt;Dateiname&gt;“.</summary>
    public const string Gespeichert = "Gespeichert: %s";                                 // (Py) app_gui 8067

    public static string Grund(RuecksicherungFehler fehler) => fehler switch
    {
        RuecksicherungFehler.KeinPlatz => GrundPlatz,
        RuecksicherungFehler.KeineBerechtigung => GrundBerechtigung,
        _ => GrundUnbekannt,
    };

    /// <summary>Titel und Text der Meldung zu einem Fehler (T3 bzw. T4 mit Grund).</summary>
    public static (string Titel, string Text) Fehler(RuecksicherungFehler fehler) => fehler == RuecksicherungFehler.NichtLesbar
        ? (NichtErstelltTitel, NichtErstelltText)
        : (SpeichernTitel, SpeichernText.Replace("%s", Grund(fehler), StringComparison.Ordinal));

    public static string Erfolg(string dateiname) => Gespeichert.Replace("%s", dateiname, StringComparison.Ordinal);
}
