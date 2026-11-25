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

    /// <summary>A 0.94.0 (Weisung P12-304): gespeicherte Pruefung eines ersetzten Satzes (ausschluss.json) geht weiter.</summary>
    public const string SatzErsetzt = "Dieser Prüfungssatz wurde durch neue Sätze ersetzt. Du kannst die gespeicherte Prüfung "
        + "noch zu Ende bearbeiten und abschließen, dein Stand bleibt erhalten. Danach wird der Satz nicht mehr angeboten.";

    /// <summary>A 1.8.0 (Weisung P12-372): gespeicherte Pruefung eines Satzes, von dem es eine neue Fassung gibt.</summary>
    public const string TeilNeuGefasst = "Zu diesem Prüfungssatz gibt es eine neue Fassung: einzelne Teilaufgaben werden jetzt "
        + "im Baukasten gezeichnet. Deine Antworten zu diesen Teilaufgaben bleiben gespeichert, werden aber nicht mehr gezeigt; "
        + "alle anderen Antworten bleiben wie sie waren.";

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
    // A 1.8.0 (Weisung P12-371): Klassen- und Use-Case-Diagramm
    public const string KlasseName = "UML-Klassendiagramm";
    public const string UseCaseName = "UML-Use-Case-Diagramm";
    public const string Methoden = "Methoden";
    public const string Vorgegeben = "(vorgegeben)";
    public const string NameFeld = "Name";
    public const string Attribut = "Attribut";
    public const string Methode = "Methode";
    public const string AttributNr = "Attribut %d";
    public const string MethodeNr = "Methode %d";
    public const string AttributNeu = "Attribut hinzufügen";
    public const string MethodeNeu = "Methode hinzufügen";
    public const string Entfernen = "Entfernen";
    public const string ZeileEntfernen = "%s entfernen";
    public const string VerbindenLeiste = "Verbinden …";
    public const string ArtDerBeziehung = "Art der Beziehung";
    public const string ArtZu = "Art der Beziehung zu %s";
    public const string Umkehren = "Richtung umkehren (zu %s)";
    public const string MultAn = "Multiplizität an %s";
    public const string KompositionGanzes = "Bei einer Komposition hat das Ganze die Multiplizität 1 oder 0..1.";
    public const string RichtungKlasse = "Arten: Assoziation, Vererbung, Aggregation, Komposition. Vererbung zeigt vom gewählten Baustein zur Oberklasse, "
        + "Aggregation und Komposition vom Teil zum Ganzen; „Richtung umkehren“ dreht den Pfeil.";
    public const string RichtungUseCase = "Arten: Assoziation (Akteur–Fall), «include», «extend», Generalisierung. «include» zeigt vom gewählten Fall "
        + "zum eingebundenen Fall, «extend» vom erweiternden Fall zum Basisfall, Generalisierung zum Allgemeinen.";
    public const string Keine = "keine";
    public const string GrenzeZeile = "Systemgrenze „%s“ (vorgegeben): Spalte %d bis %d, Zeile %d bis %d; Anwendungsfälle stehen darin, Akteure außerhalb.";
    public const string AssoziationMit = "Assoziation mit %s";
    public const string ErbtVon = "erbt von %s (Vererbung)";
    public const string OberklasseVon = "Oberklasse von %s (Vererbung)";
    public const string TeilVonAggregation = "Teil von %s (Aggregation)";
    public const string GanzesAggregation = "Ganzes zu %s (Aggregation)";
    public const string TeilVonKomposition = "Teil von %s (Komposition)";
    public const string GanzesKomposition = "Ganzes zu %s (Komposition)";
    public const string BindetEin = "«include» bindet %s ein";
    public const string EingebundenVon = "wird von %s eingebunden («include»)";
    public const string Erweitert = "«extend» erweitert %s";
    public const string ErweitertDurch = "wird durch %s erweitert («extend»)";
    public const string Spezialisiert = "spezialisiert %s (Generalisierung)";
    public const string Verallgemeinert = "verallgemeinert %s (Generalisierung)";
    public const string MultHierDort = "(Multiplizität hier %s, dort %s)";
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
    public const string ZeichnenHinweis = "Zeichne auf Papier oder im Baukasten. Nach dem Abgeben vergleichst du deine Zeichnung "
        + "mit dem Musterbild und hakst ab, was stimmt.";
    public const string Vorgabe = "Vorgegebener Anfang";
    public const string Gezeichnet = "Auf Papier gezeichnet";
    public const string NichtGezeichnet = "Nicht als gezeichnet markiert";
    public const string Musterbild = "MUSTERBILD";

    // -- A 0.78.0: Zeichen-Baukasten -----------------------------------------------------------------
    public const string BaukastenOeffnen = "Im Programm zeichnen";
    public const string BaukastenSchliessen = "Baukasten ausblenden";
    public const string Baukasten = "BAUKASTEN";
    public const string BaukastenAnleitung = "Wähle einen Baustein: er erscheint an der nächsten freien Stelle. Tippe auf einen "
        + "Baustein, um ihn zu wählen, und auf ein freies Feld, um ihn dorthin zu setzen. Ohne Maus geht alles über die Liste "
        + "„Deine Zeichnung“ und die Knöpfe beim gewählten Baustein.";
    public const string BausteinSetzen = "%s setzen";
    public const string Zeichenflaeche = "Zeichenfläche";
    public const string Gewaehlt = "Gewählt: %s";
    public const string NichtsGewaehlt = "Kein Baustein gewählt. Tippe auf einen Baustein oder wähle ihn in der Liste.";
    public const string Stelle = "Spalte %d, Zeile %d";
    public const string Fest = "Vorgegeben, bleibt stehen.";
    public const string BausteinText = "Text des Bausteins";
    public const string SchluesselAttribut = "Schlüsselattribut (unterstrichen)";
    public const string NachLinks = "Nach links";
    public const string NachRechts = "Nach rechts";
    public const string NachOben = "Nach oben";
    public const string NachUnten = "Nach unten";
    public const string VerbindenTippen = "Verbinden durch Antippen";
    public const string VerbindenHinweis = "Tippe auf den Baustein, mit dem %s verbunden werden soll.";
    public const string VerbindenAbbrechen = "Verbinden abbrechen";
    public const string VerbindenMit = "Verbinden mit";
    public const string Verbinden = "Verbinden";
    public const string Verbindungen = "Verbindungen";
    public const string PfeilZu = "Pfeil zu %s";
    public const string PfeilVon = "Pfeil von %s";
    public const string LinieZu = "Linie zu %s";
    public const string Bedingung = "Bedingung am Pfeil zu %s, z. B. [ja]";
    public const string Kardinalitaet = "Kardinalität an %s";
    public const string OhneKardinalitaet = "–";
    public const string VerbindungEntfernen = "Verbindung zu %s entfernen";
    public const string BausteinEntfernen = "Baustein entfernen";
    public const string DeineZeichnung = "DEINE ZEICHNUNG ALS LISTE";
    public const string ZeichnungLeer = "Noch kein Baustein gesetzt.";
    public const string KommtVon = "kommt von";
    public const string OhneVerbindung = "noch ohne Verbindung";
    public const string RasterVoll = "Kein freies Feld mehr.";
    public const string FeldBelegt = "Das Feld ist schon belegt.";
    public const string NichtMitSich = "Ein Baustein wird nicht mit sich selbst verbunden.";
    public const string SchonVerbunden = "Die beiden Bausteine sind schon verbunden.";
    public const string NichtStartEnde = "Ein Pfeil führt nie in den Start und nie aus dem Ende.";
    public const string ErNichtErlaubt = "Erlaubt sind Attribut zu Entität oder Beziehung und Beziehung zu Entität.";
    public const string ArtNichtErlaubt = "Diese Art der Beziehung gibt es in diesem Diagramm nicht.";
    public const string KeinKreis = "Vererbung und Generalisierung dürfen keinen Kreis bilden.";
    public const string NurAkteurFall = "Eine Assoziation verbindet einen Akteur mit einem Anwendungsfall.";
    public const string NurFallFall = "«include» und «extend» verbinden nur Anwendungsfälle.";
    public const string NurGleiche = "Eine Generalisierung verbindet nur gleiche Bausteine (Akteur mit Akteur, Anwendungsfall mit Anwendungsfall).";
    public const string AttributBelegt = "Ein Attribut hängt an genau einer Entität oder Beziehung.";
    public const string ZeichnungUnlesbar = "Deine gespeicherte Zeichnung ließ sich nicht lesen. Der Baukasten beginnt wieder mit der Vorgabe.";
    public const string ImBaukasten = "Im Baukasten gezeichnet";
    public const string EigeneZeichnung = "Deine Zeichnung";
    public const string EigeneZeichnungTitel = "DEINE ZEICHNUNG";
    public const string ProgrammZaehlt = "VOM PROGRAMM GEZÄHLT";
    public const string ZaehlHinweis = "Das Programm zählt nur. Ob Texte und Ablauf stimmen, entscheidest du mit den Häkchen.";
    public const string ZaehlAnzahl = "%s: %d, im Musterbild %d";
    public const string ZaehlVerbindungen = "Verbindungen: %d, im Musterbild %d";
    public const string ZaehlKard = "Beziehungen %s: %d, im Musterbild %d";
    public const string ZaehlSchluessel = "Schlüsselattribute: %d, im Musterbild %d";
    public const string ZaehlRegelnOk = "Regeln des Diagramms eingehalten";
    public const string ZaehlRegel = "Regel verletzt: %s";
    public const string ZaehlWeitere = "und %d weitere Regelverstöße";
    // A 1.8.0: Zaehlung mit Namen (Klasse, Use-Case)
    public const string ZaehlVon = "%s: %d von %d";
    public const string ZaehlNamenOk = "(Namen stimmen)";
    public const string ZaehlFehlt = "fehlt: %s";
    public const string ZaehlZuViel = "nicht im Musterbild: %s";
    public const string ZaehlAttributeMethoden = "Attribute: %d von %d, Methoden: %d von %d";
    public const string ZaehlBeziehungFehlt = "Fehlt: %s";
    public const string ZaehlBeziehungZuViel = "Nicht im Musterbild: %s";
    public const string ZaehlArtFalsch = "%s: %s, erwartet %s";
    public const string ZaehlRichtungFalsch = "%s %s: Richtung falsch, erwartet %s";
    public const string ZaehlMultFalsch = "%s: Multiplizität %s – %s, erwartet %s – %s";
    public const string ZaehlMitMult = "mit %s und %s";
    public const string OhneName = "(ohne Namen)";
    public const string Klassen = "Klassen";
    public const string Akteure = "Akteure";
    public const string Anwendungsfaelle = "Anwendungsfälle";
    public const string ZaehlOhneText = "Ohne Text: %s";

    public const string ErgebnisAufgaben = "Nach Aufgaben";
    public const string ErgebnisAufgabenUntertitel = "Unter 50 Prozent rot";
    public const string AufgabePunkte = "%d von %d";

    /// <summary>"1 Punkt", sonst "%d Punkte".</summary>
    public static string PunkteText(int punkte) => punkte == 1 ? EinPunkt : Beschriftungen.Einsetzen(Punkte, punkte);
}
