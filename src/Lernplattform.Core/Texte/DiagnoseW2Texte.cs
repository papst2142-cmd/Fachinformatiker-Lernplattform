namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 5 BE7: "Leistungsmessung" im Bereich "Diagnose und Werkzeuge" (fisi_leistung, PC und Handy gleich).
/// </summary>
public static class LeistungTexte
{
    public const string Titel = "Leistungsmessung";
    public const string Untertitel = "nur für dieses Gerät";
    public const string Hilfe = "Zeichnet auf, wie lange Seitenwechsel, Darstellungswechsel und Änderungen "
        + "der Fenstergröße dauern und wie viel Arbeitsspeicher das Programm braucht. "
        + "Die Messdatei bleibt auf diesem Gerät, es wird nichts gesendet. Sie enthält "
        + "keine Lerninhalte und keinen Namen. Ist die Messung aus, wird nichts "
        + "aufgezeichnet.";
    public const string Schalter = "Messung aufzeichnen";
    public const string Zeigen = "Messdatei zeigen";
    public const string Loeschen = "Messdatei löschen";
    public const string KeineDatei = "Noch keine Messdatei vorhanden.";
    /// <summary>%(zeilen)d = Eintraege, %(kb)s = Groesse in KB mit Komma.</summary>
    public const string Datei = "Messdatei: %(zeilen)d Einträge, %(kb)s KB";
    public const string Geloescht = "Messdatei gelöscht";
    /// <summary>%s = Grund (Wortliste W2: nie ein Systemtext).</summary>
    public const string ZeigenFehler = "Die Messdatei ließ sich nicht öffnen: %s";
}

/// <summary>
/// Phase 5 BE8: "Hänger-Diagnose" (fisi_haenger, nur PC). Der Linux-Befehl nennt den Programmnamen
/// "Lernplattform", der Hinweis dazu ohne die Versionssaetze der Python-Linie (Wortliste W2).
/// </summary>
public static class HaengerTexte
{
    public const string Titel = "Hänger-Diagnose";
    /// <summary>HELP ohne "Vorladen" (die C#-Linie laedt nicht vor; Sammelliste 71, Wortliste W3).</summary>
    public const string Hilfe = "Bleibt das Programm hängen, schreibt es nach 30 Sekunden automatisch in die "
        + "Datei haenger.log, an welcher Stelle es steht. Blockaden ab 5 Sekunden stehen "
        + "kurz in fehler.log, bei bekannten langen Vorgängen (Seitenaufbau, "
        + "Darstellungswechsel, Schriftwechsel) erst ab 30 Sekunden. Der "
        + "Abgleich läuft im Hintergrund und blockiert nicht. Beide Dateien liegen im "
        + "Datenordner und stehen im Problembericht. Es wird nichts gesendet.";
    /// <summary>SIGNAL_HELP ohne "Ab Version 0.59.2" und ohne den Satz zu 0.59.1 (Wortliste W2).</summary>
    public const string SignalHilfe = "Unter Linux: Hängt das Programm, öffne ein Terminal "
        + "und gib diesen Befehl ein. Das Programm schreibt dann in die Datei "
        + "haenger.log, an welcher Stelle es gerade steht, und wird dabei nicht "
        + "beendet.";
    /// <summary>KILL_COMMAND mit dem Programmnamen der C#-Linie (Wortliste W2).</summary>
    public const string Befehl = "kill -USR1 $(pgrep -i -x -o lernplattform)";
    public const string BefehlKopieren = "Befehl kopieren";
    public const string BefehlKopiert = "Befehl in die Zwischenablage kopiert";
    public const string Keine = "Keine Hänger aufgezeichnet.";
    /// <summary>%(kb)s = Groesse mit Komma, %(zeit)s = "TT.MM.JJJJ HH:MM".</summary>
    public const string Datei = "haenger.log: %(kb)s KB, zuletzt geändert %(zeit)s";
}

/// <summary>
/// Phase 5 W2: Ersatz fuer den Systemtext, den Python in Fehlerfenstern zeigt ("%s" = str(error)).
/// Die Einzelheiten gehen nach fehler.log (Wortliste W2).
/// </summary>
public static class DiagnoseW2Texte
{
    public const string GrundImProtokoll = "Einzelheiten stehen in fehler.log.";
}
