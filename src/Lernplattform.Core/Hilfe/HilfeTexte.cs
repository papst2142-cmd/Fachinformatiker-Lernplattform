namespace Lernplattform.Core.Hilfe;

/// <summary>Ein Hilfe-Abschnitt (fisi_hilfe.HELP_SECTIONS): Kennung, Titel, genannte Hauptpunkte (PC-Namen), Text.</summary>
public sealed record HilfeAbschnitt(string Id, string Titel, IReadOnlyList<string> Reiter, string Text);

/// <summary>Eine Seite des Rundgangs (fisi_hilfe.TOUR_PAGES).</summary>
public sealed record RundgangSeite(string Id, string Titel, string Text);

/// <summary>
/// Phase 4, W4: die Texte der Hilfe und des Rundgangs mit „…“-Begriffen (Wortliste W4 mit Nachtrag P4-11).
/// Genau die Python-Texte von fisi_hilfe 0.63.2 mit eingesetzten Paaren (<see cref="Paare"/>), dazu N4 und N7.
/// Nur diese Datei ist vom Begriffe-Test ausgenommen; Beschriftungen stehen in <see cref="RundgangTexte"/>
/// und Texte.RahmenplanTexte. Geprueft gegen tests/Vergleich/hilfe.json (G35).
/// </summary>
public static partial class HilfeTexte
{
    /// <summary>TOUR_KEYS, nur am PC (Geraeteausnahme B13, Diff B).</summary>
    public const string TourTasten = "Tab wechselt, Eingabe bestätigt, Esc überspringt";

    /// <summary>HELP_INTRO.</summary>
    public const string Einleitung = "Kurze Antworten zu den wichtigsten Bereichen. Wähle ein Thema, um es aufzuklappen. Begriffe in "
        + "„Anführungszeichen“ stehen genau so in der App.";

    /// <summary>SETUP_DATES_HINT (Rundgang „Jetzt einrichten“).</summary>
    public const string TermineHinweis = "Format TT.MM.JJJJ, leer lassen geht auch. Mit Termin kommen in den sechs Monaten davor neue "
        + "Aufgaben zu prüfungsrelevanten Themen etwas früher dran, solange „Übungsauswahl nach Rahmenplan "
        + "gewichten“ an ist.";

    /// <summary>NAME_OPTION_HINT (Optionen „Rundgang und Hilfe“).</summary>
    public const string NameHinweis = "Nur auf diesem Gerät gespeichert, nicht im Abgleich und nicht in der Sicherung. Mit Namen steht "
        + "oben im „Dashboard“ (am Handy: „Start“) „Hallo“ und dein Name.";

    /// <summary>N4: Text der Ausbildungsfrage (nur C#, Weisung P4-10).</summary>
    public const string AusbildungFrage = "Welche Fachrichtung lernst du? Davon hängen die Projektstunden im „Abschlussprojekt“, die "
        + "Prüfungsbereiche und die gezeigten Lerninhalte ab. Die Wahl gilt auf allen Geräten, die du abgleichst. Ändern kannst du sie "
        + "jederzeit in den „Optionen“ unter „Ausbildung“.";

    /// <summary>N7: Warnhinweis Rahmenplan; %s = Fachrichtung (PruefungTexte.Fachrichtungen), %s = SECTION_OPTION_TEXT.</summary>
    public const string Warnung = "Du hast „%s“ gewählt. Soll der Rahmenplan dazu passen? Bei „Ja“ wird „%s“ eingeschaltet. Die "
        + "„Rahmenplan-Abdeckung“ im „Fortschritt“ zeigt dann auch diesen Abschnitt, und neue Aufgaben dazu "
        + "kommen etwas früher dran, solange „Übungsauswahl nach Rahmenplan gewichten“ an ist. Es wird nichts "
        + "ausgeschaltet: Abschnitte, die schon an sind, bleiben an. Bei „Nein“ bleibt alles, wie es ist. "
        + "Ändern kannst du es jederzeit in den „Optionen“ unter „Lerninhalte“ bei „Rahmenplan“. Die "
        + "Einstellung gilt für dieses Gerät. Deine anderen Geräte fragen nach dem nächsten Abgleich "
        + "ebenfalls, wenn der Abschnitt dort aus ist.";

    /// <summary>TOUR_PAGES in Reihenfolge.</summary>
    public static readonly IReadOnlyList<RundgangSeite> Seiten =
    [
        new("willkommen", "Willkommen",
            // Phase 9 W2: Paar P9a (HilfePaare), statt "(Schwerpunkt Systemintegration)"
            "Die Fachinformatiker Lernplattform hilft dir bei der Vorbereitung auf die Prüfungen AP1 und AP2. "
            + "Deine Fachrichtung wählst du in den „Optionen“ unter „Ausbildung“.\n"
            + "\nLernen kannst du mit „Karteikarten“, im „Prüfungstrainer“ und mit den „AP1-Szenarien“ und „AP2-Szena"
            + "rien“, die wie Prüfungsaufgaben aufgebaut sind. Am PC findest du alles links in der Seitenleiste, "
            + "am Handy unter „Lernen“."),
        new("fortschritt", "Fortschritt und Rahmenplan",
            "Unter „Fortschritt“ siehst du, was du schon kannst: deine Ergebnisse und die „Rahmenplan-Abdeckung“ "
            + "für jeden Punkt des Ausbildungsrahmenplans. Deine Lernserie steht oben im Banner des „Dashboard“ "
            + "(am Handy: „Start“).\n"
            + "\n„Jetzt wiederholen“ im „Dashboard“ (am Handy: „Start“) holt fällige Karten und Fragen zurück, damit "
            + "du nichts vergisst."),
        new("spiel", "Spiel",
            "Im „Spiel“ arbeitest du in einer IT-Firma und löst Tickets aus dem Berufsalltag. Dein Wissensstand "
            + "im Spiel kommt aus dem, was du mit Karteikarten und im Prüfungstrainer lernst.\n"
            + "\nSpäter kannst du eine eigene Firma gründen."),
        new("sicherung", "Sicherung und Abgleich",
            "Dein Lernstand wird auf diesem Gerät gespeichert. In den „Optionen“ sicherst du ihn mit „Sicherung "
            + "erstellen“ als Datei oder hältst PC und Handy mit „Abgleich PC und Handy“ auf demselben Stand.\n"
            + "\nFragen beantwortet die „Hilfe“. Diesen Rundgang kannst du in den „Optionen“ jederzeit wieder starten"
            + "."),
        new("einrichten", "Jetzt einrichten",
            "Alle Angaben sind freiwillig und gelten nur für dieses Gerät. Rahmenplan und Termine kannst du "
            + "später in den „Optionen“ unter „Lerninhalte“ bei „Rahmenplan“ ändern."),
    ];

    /// <summary>HELP_SECTIONS in Reihenfolge, Texte mit eingesetzten Paaren.</summary>
    public static readonly IReadOnlyList<HilfeAbschnitt> Abschnitte =
    [
        new("lernen", "Lernen mit Karteikarten", ["Karteikarten", "Dashboard", "Notizblock"],
            "Unter „Karteikarten“ (am Handy unter „Lernen“) übst du Fachbegriffe und Grundwissen. Oben wählst du "
            + "den Fachbereich, zum Beispiel „Alle“ oder „Netzwerk“, und den Lernmodus:\n"
            + "• „Freitext“: eigene Antwort schreiben, dann „Antwort prüfen“\n"
            + "• „Multiple Choice“: eine Antwort auswählen\n"
            + "• „Aufdecken“: erst überlegen, dann „Lösung aufdecken“ und mit „Gewusst“ oder „Nicht gewusst“ "
            + "bewerten\n"
            + "\nWas du schon bearbeitet hast, kommt nach einiger Zeit zur Wiederholung. „Jetzt wiederholen“ im „Dash"
            + "board“ (am Handy: „Start“) holt die fälligen Karten und Fragen. Im „Notizblock“ siehst du, was du "
            + "noch üben musst."),
        new("pruefung", "Prüfung (AP1 und AP2)", ["Prüfungstrainer", "AP1-Szenarien", "AP2-Szenarien", "Testprojekt", "Abschlussprojekt"],
            "Im „Prüfungstrainer“ übst du Prüfungsfragen mit Erklärung. Schalte oben auf „Prüfung“, um eine "
            + "Prüfung nach IHK-Vorbild zu schreiben: AP1 oder einen der drei Teile der AP2 (Konzeption, "
            + "Netzwerke, WiSo). Sie ist aufgebaut wie die echte Prüfung: Es läuft eine feste Zeit. Offene "
            + "Aufgaben bewertest du nach der Abgabe selbst anhand der Musterlösung, WiSo wird automatisch "
            + "ausgewertet. Danach siehst du Punkte und die Note nach IHK-Schlüssel (Orientierung, keine amtliche "
            + "Note).\n"
            + "\n„AP1-Szenarien“ und „AP2-Szenarien“ sind längere Aufgaben wie in der Prüfung, mit Musterlösung. Im „"
            + "Testprojekt“ übst du Kundenaufträge, im „Abschlussprojekt“ planst du dein eigenes IHK-Projekt vom "
            + "Antrag bis zum Fachgespräch. Am Handy findest du alles unter „Lernen“.\n"
            + "\nWenn du für dein Abschlussprojekt KI-Hilfsmittel nutzt (zum Beispiel für Texte, Code oder Bilder), v"
            + "erlangen manche Kammern eine Angabe dazu in der Projektdokumentation. Frag bei deiner IHK nach, was "
            + "für dich gilt."),
        new("rechner", "Rechner (Subnetting, RAID, Bildschirm, USV)", ["Rechner"],
            "Unter „Rechner“ gibt es vier Rechner: „Subnetting“ (IPv4 und IPv6), „RAID-Kapazität“, "
            + "„Bildschirm-Datenvolumen“ und ganz unten „USV-Kapazität“. Werte eintragen und „Berechnen“ wählen. "
            + "„Rechenweg anzeigen“ erklärt, wie man es von Hand rechnet.\n"
            + "\nDer USV-Rechner rechnet mit „Akku berechnen“, wie groß der Akku für eine gewünschte Laufzeit sein mu"
            + "ss, und mit „Laufzeit berechnen“, wie lange ein vorhandener Akku reicht. „Empfehlung“ sagt, ob die "
            + "USV passt oder zu klein ist, und zeigt die Regel dazu. Ein Bild zeigt Last, USV, Akku und Laufzeit. "
            + "Unter „Übungsaufgaben“ gibt es Aufgaben mit „Lösung anzeigen“ und „Nächste Aufgabe“.\n"
            + "\nOben unter „Trainer“ rechnest du selbst: „IPv4-Subnetz“, „VLSM“, „Binär/Hex/Dezimal“, „IPv6-Schreibw"
            + "eise“, „Netz aufteilen“ und „Nach Hostbedarf“, jeweils „Leicht“, „Mittel“ oder „Schwer“. "
            + "Trainer-Aufgaben zählen zum Tagesziel."),
        new("spiel", "Firma-Spiel", ["Spiel"],
            "Im „Spiel“ arbeitest du bei der Bitweiche IT-Service GmbH. Jeder Arbeitstag bringt Tickets, zum "
            + "Beispiel PCs zusammenbauen, Fehler suchen oder Netze planen. Dein Wissensstand im Spiel kommt aus "
            + "dem, was du mit Karteikarten und im Prüfungstrainer lernst.\n"
            + "\nMit genug Erspartem und gutem Ansehen kannst du eine eigene Firma gründen, Leute einstellen und Auft"
            + "räge annehmen. Es gibt drei Spielstände für getrennte Durchgänge. Der Lernfortschritt gilt für alle "
            // Phase 8 W5: Paar H2 (Spielstand im Abgleich)
            + "gemeinsam. Mit eingerichtetem Abgleich spielst du denselben Spielstand auf PC und Handy weiter.\n"
            // Phase 7 W6: Paar S1 (Satz der Stadt, StadtTexte.HilfeSatz)
            + "\n" + Texte.StadtTexte.HilfeSatz
            // Phase 8 W5: Paar H1 (Stadt in 3D, Kaufhaus, Bett)
            + "\n\n" + Texte.StadtTexte.HilfeStadt),
        new("fortschritt", "Fortschritt und Rahmenplan", ["Fortschritt", "Optionen"],
            "Unter „Fortschritt“ siehst du die Ergebnisse deiner Prüfungen und wie viel du an den letzten Tagen "
            + "gelernt hast. Deine Lernserie steht oben im Banner des „Dashboard“ (am Handy: „Start“). Die "
            + "„Rahmenplan-Abdeckung“ zeigt für jeden Punkt des Ausbildungsrahmenplans, wie gut du ihn schon "
            + "kannst. Mit „Jetzt üben“ übst du gezielt einen Punkt.\n"
            + "\nIn den „Optionen“ unter „Lerninhalte“ bei „Rahmenplan“ stellst du ein, welche Abschnitte angezeigt w"
            + "erden, dein aktuelles Lernfeld und die Termine für AP1 und AP2. Ist „Übungsauswahl nach Rahmenplan "
            + "gewichten“ an, kommen in den sechs Monaten vor einem Termin neue Aufgaben zu prüfungsrelevanten "
            + "Themen etwas früher dran."),
        new("sicherung", "Sicherung und Abgleich", ["Optionen"],
            "Dein Lernstand wird auf diesem Gerät gespeichert. In den „Optionen“ unter „Sicherung“ speicherst du "
            + "ihn mit „Sicherung erstellen“ als Datei, zum Beispiel vor einem Gerätewechsel. Mit „Sicherung "
            + "einspielen“ holst du ihn zurück, auch auf einem anderen Gerät. Dabei wählst du „Zusammenführen“ "
            + "(nur Fehlendes ergänzen) oder „Alles ersetzen“. In der Sicherungsdatei steht auch der Name des "
            + "Geräts, am PC mit dem Rechnernamen.\n"
            + "\nMit „Abgleich PC und Handy“ haben PC und Handy denselben Lernstand. Wie du ihn einrichtest, steht im"
            + " nächsten Thema."),
        new("abgleich", "Abgleich einrichten", ["Optionen"],
            "Für den Abgleich brauchst du ein kostenloses Konto bei github.com:\n"
            + "1. Lege dort ein neues, privates Repository an, zum Beispiel mit dem Namen Fi-Lernstand.\n"
            + "2. Erzeuge einen Zugangsschlüssel: Profilbild > Settings > Developer settings > Personal access "
            + "tokens > Fine-grained tokens > Generate new token. Wähle nur dieses Repository und bei Contents das "
            + "Recht Read and write. Kopiere den Schlüssel (beginnt mit github_pat_), GitHub zeigt ihn nur einmal.\n"
            + "3. In den „Optionen“ unter „Abgleich PC und Handy“ trägst du bei „Repository (Benutzer/Name)“ zum "
            + "Beispiel deinname/Fi-Lernstand und bei „Zugangsschlüssel (Token)“ den Schlüssel ein und wählst "
            + "„Speichern und abgleichen“. Auf dem anderen Gerät genauso.\n"
            + "\nÜbertragen werden: was du wann gelernt hast, die Ergebnisse deiner Tests und Prüfungen (Punkte und N"
            + "ote, nicht deine Antworten), die Texte deines Abschlussprojekts, deine Spielstände und die "
            + "Bestenliste. In jeder Änderung im Repository steht außerdem der Name des Geräts, am PC mit dem "
            + "Rechnernamen.\n\nAchtung: Auch Löschen wird abgeglichen."),
        new("update", "Update", ["Optionen"],
            "In den „Optionen“ unter „Updates“ prüft „Nach Updates suchen“, ob es eine neue Version gibt. Ist "
            + "„Beim Start automatisch nach Updates suchen“ an, sucht die App beim Start von selbst.\n"
            + "\nAm PC führt „Jetzt aktualisieren“ durch Download und Installation, danach startet das Programm neu. "
            + "Am Handy wird die neue Version im Browser heruntergeladen. Danach die Datei öffnen und "
            + "„Installieren“ tippen. Dein Lernstand bleibt erhalten.\n"
            + "\nUnter Linux legt „Verknüpfung anlegen“ eine Verknüpfung im Anwendungsmenü und auf dem Desktop an "
            + "oder erneuert sie, zum Beispiel nachdem du das AppImage verschoben hast."),
        new("farben", "Eigene Farben", ["Optionen"],
            "In den „Optionen“ unter „Optische Anpassungen“ bei „Farben“ liegen die „Vorlagen“ mit „Grundfarbe“ "
            + "und „Hintergrund“ (zum Aufklappen). In der hellen Darstellung heißen die Hintergründe nach ihrer "
            + "hellen Fläche: „Flieder“, „Hellblau“, „Mintgrün“, „Rosé“, „Hellgrau“ und „Weiß“.\n"
            + "\nDarunter steht der Bereich „Eigene Farben“. Mit „Farbton“, „Sättigung“ und „Helligkeit“ stellst du „"
            + "Akzent 1“ und „Akzent 2“ (den Farbverlauf der Knöpfe) und den Hintergrund ein. Die Vorschau zeigt "
            + "sofort, wie es aussieht. Die Regler starten bei deiner Farbwelt. Sobald du einen Regler bewegst, "
            + "werden alle Farben passend zu deinen Einstellungen neu berechnet.\n"
            + "\nEin Warnhinweis bedeutet: Etwas ist schlecht lesbar oder die Fachbereichsfarben sind schwer zu unter"
            + "scheiden. Er nennt den gemessenen Wert. Speichern geht trotzdem.\n"
            + "\n„Speichern“ übernimmt die Farben. „Auf Farbwelt zurücksetzen“ stellt die gewählte Farbwelt wieder he"
            + "r. Die eigenen Farben gelten nur auf diesem Gerät und nur für die gewählte Darstellung (Hell oder "
            + "Dunkel)."),
        new("problem", "Problem melden", ["Optionen"],
            "Wenn etwas nicht klappt: In den „Optionen“ unter „Diagnose und Werkzeuge“ steht bei „Problem "
            + "melden“ ein Bericht mit Programmversion, Gerät, Datenordner und Fehlerprotokoll. Zugangsschlüssel, "
            + "Passwörter und dein Benutzername in Pfaden werden entfernt. Andere Angaben aus Fehlermeldungen "
            + "können darin stehen. Sieh dir den Bericht vor dem Weitergeben an.\n"
            + "\nSpeichere den Bericht mit „Als Datei speichern“ (oder „Kopieren“) und gib ihn an die Person weiter, "
            + "von der du das Programm hast. Schreib kurz dazu: Was hast du gemacht, was ist passiert, was hast du "
            + "erwartet?"),
        new("leistung", "Leistungsmessung", ["Optionen"],
            "In den „Optionen“ unter „Diagnose und Werkzeuge“ schaltet bei „Leistungsmessung“ der Schalter "
            + "„Messung aufzeichnen“ die Messung ein und aus. Sie ist zu Beginn aus. Eingeschaltet schreibt das "
            + "Programm in eine Messdatei, wie lange Seitenwechsel, Darstellungswechsel und Änderungen der "
            + "Fenstergröße dauern und wie viel Arbeitsspeicher es dabei braucht.\n"
            + "\nDie Messdatei „leistungsmessung.csv“ liegt im Datenordner auf diesem Gerät. „Messdatei zeigen“ öffne"
            + "t am PC den Datenordner, am Handy kannst du die Datei speichern oder teilen. „Messdatei löschen“ "
            + "entfernt sie. Es wird nichts gesendet. Die Datei enthält keine Lerninhalte und keinen Namen. Die "
            + "Einstellung gilt nur für dieses Gerät und kommt nicht in den Abgleich."),
        new("haenger", "Hänger-Diagnose (PC)", ["Optionen"],
            "Bleibt das Programm am PC hängen, schreibt es nach 30 Sekunden automatisch in die Datei "
            + "haenger.log, an welcher Stelle es steht. Blockaden ab 5 Sekunden stehen kurz in fehler.log, bei "
            + "bekannten langen Vorgängen (Seitenaufbau, Darstellungswechsel, Schriftwechsel) erst ab 30 "
            + "Sekunden. Der Abgleich läuft im Hintergrund und blockiert nicht. Beide Dateien liegen im "
            + "Datenordner und stehen im Bericht bei „Problem melden“. Es wird nichts gesendet.\n"
            + "\nHängt unter Linux das Tippen, startest du das Programm mit dem Startschalter\n--ohne-eingabemethode\nD"
            + "ann nutzt es keine Eingabemethode, zum Beispiel IBus."),
        new("tastatur", "Bedienung mit der Tastatur (PC)", [],
            "Am PC lassen sich die Lernbereiche und die Optionen ohne Maus bedienen.\n"
            + "• Tab springt zum nächsten Element, Umschalt+Tab zurück. Das gewählte Element hat einen deutlichen "
            + "Rahmen.\n"
            + "• Eingabe oder Leertaste drückt Knöpfe, schaltet Schalter um und klappt Bereiche auf und zu.\n"
            + "• Esc überspringt den Rundgang.\n"
            + "• Strg+F springt in die Suche oben rechts.\n"
            + "• Strg+Eingabe prüft im Antwortfeld einer Karteikarte deine Antwort.\n"
            + "\nDie Schrift vergrößerst du in den „Optionen“ unter „Optische Anpassungen“ bei „Schriftgröße“ mit „Gr"
            + "oß“, „Sehr groß“, „Extra groß“ oder „Maximal“."),
        new("schutzprogramm", "Warnung vom Schutzprogramm", [],
            "Manche Schutzprogramme und Browser warnen beim Herunterladen oder Installieren der Fachinformatiker "
            + "Lernplattform. Der Grund: Das Programm ist neu, wird selten heruntergeladen und ist nicht digital "
            + "signiert.\n"
            + "\nLade die App deshalb nur von der Release-Seite herunter: github.com/papst2142-cmd/Fachinformatiker-L"
            + "ernplattform/releases. Dort steht zu jeder Datei eine Prüfsumme (SHA-256), mit der du die Datei "
            + "vergleichen kannst. Schalte den Schutz nicht dauerhaft ab. Warnt dein Schutzprogramm auch bei der "
            + "Datei von der Release-Seite, kannst du den Fehlalarm beim Hersteller des Schutzprogramms melden."),
        new("update_schutz", "Update und Schutzprogramm", ["Optionen"],
            "Bei einer neuen Version kann sich dein Schutzprogramm melden. Das ist bei neuen, noch wenig "
            + "bekannten Versionen normal. So kann es aussehen:\n"
            + "• Es fragt, ob die Installation erlaubt ist. Erlaube sie.\n"
            + "• Beim ersten Start meldet es, dass das Programm unter Vorbehalt gestartet wurde, nach etwa einer "
            + "Minute vielleicht noch einmal. Mit Trotzdem ausführen startet das Programm normal.\n"
            + "• Eine von Hand heruntergeladene Installationsdatei kann in der Quarantäne landen, auch wenn sie in "
            + "Ordnung ist (Fehlalarm).\n"
            + "• Am Handy fragt Android vor der Installation nach. Erlaube sie.\n"
            + "\nDas Update in den „Optionen“ unter „Updates“ lädt die Datei direkt von der Release-Seite. Lädst du s"
            + "elbst, dann nur von github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases, und "
            + "vergleiche die Prüfsumme (SHA-256). Abschalten musst du dafür nichts. Bist du unsicher, frag nach, "
            + "bevor du etwas erlaubst."),
        new("rechtliches", "Rechtliches", ["Optionen"],
            "Die Fachinformatiker Lernplattform ist ein privates Lernprogramm. Sie ist kein Angebot und kein "
            + "Produkt einer Industrie- und Handelskammer (IHK) oder einer anderen Prüfungsstelle und wird von "
            + "diesen weder herausgegeben noch geprüft. Die Aufgaben, Szenarien und Testprojekte sind eigene "
            + "Übungsaufgaben. Sie enthalten keine offiziellen Prüfungsaufgaben. Bezeichnungen wie „AP1“, „AP2“ "
            + "oder „Note nach IHK-Schlüssel“ beschreiben nur, wofür geübt wird und nach welchem Notenschlüssel "
            + "die Übung ausgewertet wird.\n"
            + "\nCopyright (c) 2026 Nico H – Alle Rechte vorbehalten. Erstellt mit Hilfe von Claude (Anthropic).\n\nIn "
            // Phase 8 W5: Paar L1 (Knopf "Lizenzen")
            + "den „Optionen“ unter „Über das Programm“ findest du „Lizenz“, „Hinweise zu Fremdbestandteilen“ "
            + "(Bestandteile anderer Hersteller und ihre Lizenzen) und „Lizenzen“ (die vollständigen Lizenztexte "
            + "dieser Bestandteile)."),
    ];
}
