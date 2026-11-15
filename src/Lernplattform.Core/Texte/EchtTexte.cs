namespace Lernplattform.Core.Texte;

/// <summary>
/// Welle 1 "Prüfung im echten Format" (Weisung P12-262): Texte des neuen Modus. PC und Handy gleich.
/// Die heutige Klausur behält ihre Texte (<see cref="PruefungTexte"/>).
/// </summary>
public static class EchtTexte
{
    public const string KarteTitel = "Prüfung im echten Format";
    public const string KarteUntertitel = "Nach dem Aufbau der IHK-Abschlussprüfung";
    public const string Einleitung = "Eine erfundene Firma für alle vier Aufgaben, Teilaufgaben mit Arbeitsanweisung und Punkten, "
        + "Anlagen wie Tabellen und Konfigurationen. 90 Minuten ohne Pause, 100 Punkte. Zahlen, Lückenfelder und Tabellen "
        + "bewertet das Programm, den Rest bewertest du selbst mit der Musterlösung. Das Ergebnis zählt nicht ins Gesamtergebnis.";
    public const string Angabe = "%d Minuten · 4 Aufgaben · 100 Punkte · Sätze: %d, davon bearbeitet: %d";
    public const string KeinSatz = "Für diesen Bereich gibt es noch keine Prüfung im echten Format.";
    public const string FehlerHinweis = "Ein Prüfungssatz konnte nicht geladen werden: %s";
    public const string Starten = "%s starten";
    public const string StartFrage = "%s\n\nIm echten Format: %s.\n\nDie Zeit (%d Minuten) läuft sofort und lässt sich nicht anhalten. Starten?";
    public const string AndereLaeuft = "Es läuft gerade eine Prüfung im anderen Modus. Gib sie zuerst ab oder brich sie ab.";
    public const string Ergebnisse = "Ergebnisse im echten Format";
    public const string KeinErgebnis = "Noch keine Prüfung im echten Format abgelegt.";
    public const string NameZusatz = " (echtes Format)";
    public const string KurzZusatz = " echt";
    public const string SatzFehlt = "Der Prüfungssatz der gespeicherten Prüfung im echten Format fehlt in diesem Programm. "
        + "Sie bleibt gespeichert, bis das Programm den Satz wieder kennt.";

    public const string Ausgangssituation = "Ausgangssituation";
    public const string AufgabeKurz = "%d (%d)";
    public const string Zeitfrage = "Zeitfrage";
    public const string ZeitfrageUntertitel = "Wird nicht bewertet";
    public const string AufgabeTitel = "Aufgabe %d: %s";
    public const string Punkte = "%d Punkte";
    public const string EinPunkt = "1 Punkt";
    public const string Teil = "%s)";
    public const string Anlagen = "Anlagen (%d)";
    public const string AufgabeUmschalter = "Aufgabe";
    public const string Anlage = "Anlage %s: %s";
    public const string NurErste = "Es zählen nur die ersten %s Angaben.";
    public const string AngabeFeld = "%d. Angabe";
    public const string Rechenweg = "Rechenweg";
    public const string ErgebnisFeld = "Ergebnis";
    public const string Antwort = "Antwort";
    public const string Mehrfach = "Mehrere Antworten können richtig sein.";
    public const string Englisch = "Englischer Text: Antworten auf Deutsch.";
    public const string Regeln = "Stichworte genügen, außer ganze Sätze sind verlangt. Mit gerundeten Ergebnissen weiterrechnen. "
        + "Hilfsmittel: ein einfacher Taschenrechner.";

    public const string Zwischenstand = "Zwischenstand: %d von 100 Punkten";
    public const string Programm = "Bewertet das Programm";
    public const string Richtig = "richtig";
    public const string Falsch = "falsch";
    public const string ProgrammPunkte = "Vom Programm: %d von %d Punkten";
    public const string SelbstPunkte = "Selbst bewertet: %d von %d Punkten";
    public const string AngabeRichtig = "%d. Angabe richtig (%s)";
    public const string HinweisPunkte = "%s (%s)";
    public const string RechenwegRichtig = "Rechenweg";
    public const string BewertungAnleitung = "Grün oder rot hat das Programm bewertet. Vergleiche den Rest mit der Musterlösung "
        + "und hake ab, was du richtig hattest.";
    public const string Loesung = "MUSTERLÖSUNG";
    public const string ErwarteteWerte = "Erwartet: %s";
    public const string Leer = "(leer)";

    // -- Welle 2 (A 0.77.0): gezeichnete Anlagen und Zeichenaufgaben --------------------------------
    public const string UmlName = "UML-Aktivitätsdiagramm";
    public const string ErName = "ER-Modell in Chen-Notation";
    public const string VerbundenMit = "verbunden mit";
    public const string WeiterZu = "weiter zu";
    public const string Attribute = "Attribute";
    public const string SchluesselZusatz = "(Schlüssel)";
    public const string Zwischen = "zwischen";
    public const string Und = "und";
    public const string Hoechstwert = "Höchster Wert %s um %s.";
    public const string AlsText = "Als Text zeigen";
    public const string BildZeigen = "Text ausblenden";
    public const string BildName = "%s, Bild. Den Inhalt zeigt der Knopf „Als Text zeigen“ als Text.";
    public const string Leitungen = "Beschriftung der Leitungen";
    public const string ZeichnenHinweis = "Zeichne auf Papier. Nach dem Abgeben vergleichst du deine Zeichnung mit dem Musterbild "
        + "und hakst ab, was stimmt.";
    public const string Vorgabe = "Vorgegebener Anfang";
    public const string Gezeichnet = "Auf Papier gezeichnet";
    public const string NichtGezeichnet = "Nicht als gezeichnet markiert";
    public const string Musterbild = "MUSTERBILD";

    public const string ErgebnisAufgaben = "Nach Aufgaben";
    public const string ErgebnisAufgabenUntertitel = "Unter 50 Prozent rot";
    public const string AufgabePunkte = "%d von %d";

    /// <summary>"1 Punkt", sonst "%d Punkte".</summary>
    public static string PunkteText(int punkte) => punkte == 1 ? EinPunkt : Beschriftungen.Einsetzen(Punkte, punkte);
}
