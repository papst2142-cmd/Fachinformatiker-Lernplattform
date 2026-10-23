namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 6 W2 (VE5): Texte der Linux-Verknuepfung wie fisi_verknuepfung.py in Python 0.63.2, in der
/// Fassung der Wortliste W2 Spalte "C# a" (Anlage P6-5_Wortliste_W2_Linux_Verknuepfung.md, angenommen
/// mit Weisung P6-7). Nur unter Linux im installierten AppImage zu sehen. "W2-n" = Nummer der Wortliste.
/// </summary>
public static class VerknuepfungTexte
{
    /// <summary>W2-1: Knopf unter Optionen › Updates.</summary>
    public const string Anlegen = "Verknüpfung anlegen";

    /// <summary>W2-2: Zeile neben dem Knopf, keine Verknuepfung im Anwendungsmenue.</summary>
    public const string Keine = "Noch keine Verknüpfung angelegt.";

    /// <summary>W2-3: Zeile neben dem Knopf, Verknuepfung im Anwendungsmenue vorhanden.</summary>
    public const string Vorhanden = "Im Anwendungsmenü liegt eine Verknüpfung.";

    /// <summary>W2-4: Titel der Rueckfrage beim Start.</summary>
    public const string FrageTitel = "Verknüpfung";

    /// <summary>W2-5: Text der Rueckfrage beim Start.</summary>
    public const string FrageText = "Soll eine Verknüpfung zur Fachinformatiker Lernplattform im Anwendungsmenü und auf dem Desktop "
        + "angelegt werden? Du kannst sie später auch in den Optionen unter „Updates“ anlegen.";

    /// <summary>W2-6: Knoepfe der Rueckfrage (wie Python).</summary>
    public const string Ja = "Ja";
    public const string Nein = "Nein";
    public const string NieFragen = "Nicht mehr fragen";

    /// <summary>W2-7: Ergebnis neu, je Ort eine Zeile mit dem Pfad.</summary>
    public const string Angelegt = "Verknüpfung angelegt: %s";

    /// <summary>W2-8: Ergebnis erneuert, je Ort eine Zeile mit dem Pfad.</summary>
    public const string Erneuert = "Verknüpfung erneuert: %s";

    /// <summary>W2-9: kein Desktop-Ordner, nur der Eintrag im Anwendungsmenue.</summary>
    public const string KeinDesktop = "Es wurde kein Desktop-Ordner gefunden. Die Verknüpfung liegt nur im Anwendungsmenü.";

    /// <summary>W2-10: Fehler; %s ist die Fehlerart (Name der Ausnahme), nie der Systemtext (wie W3-1).</summary>
    public const string Fehler = "Die Verknüpfung konnte nicht angelegt werden: %s";

    /// <summary>W2-11: Zusatz, wenn GNOME die Datei auf dem Desktop nicht als vertrauenswuerdig markieren liess.</summary>
    public const string Unsicher = "\nFalls sie sich nicht per Doppelklick starten lässt: Rechtsklick und „Start erlauben“ wählen.";

    /// <summary>Fehlerart, wenn das Programm nicht als AppImage laeuft (Variable APPIMAGE leer).</summary>
    public const string ProgrammUnbekannt = "Programmdatei unbekannt";
}
