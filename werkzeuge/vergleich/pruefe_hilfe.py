#!/usr/bin/env python3
"""Unabhaengige Gegenkontrolle G35 (hilfe.json).

Geschrieben nur aus P4-10_Spezifikation_G35.md, P4-10_Wortliste_W4.md und
P4-11_Wortliste_W4_Nachtrag.md (Abschnitte 3 und 5). Liest weder die
Python-Linie noch den C#-Code. Aufruf:
    python3 -I pruefe_hilfe.py <ausgabe.json>
Nur Standardbibliothek, Python 3.12.
"""
import atexit
import json
import os
import re
import shutil
import sqlite3
import sys
import tempfile

# ---------------------------------------------------------------------------
# Texte (Wortliste P4-10, Abschnitte A-D, Spalte "Text"; Python-Werte ohne N1/N2)
# Per Skript aus der Markdown-Tabelle gelesen, hier als Konstanten abgelegt.
# ---------------------------------------------------------------------------
KONSTANTEN = {'TOUR_TITLE': 'Rundgang',
 'TOUR_STEP': 'Schritt %d von %d',
 'TOUR_SKIP': 'Überspringen',
 'TOUR_BACK': 'Zurück',
 'TOUR_NEXT': 'Weiter',
 'TOUR_FINISH': 'Speichern und loslegen',
 'TOUR_KEYS': 'Tab wechselt, Eingabe bestätigt, Esc überspringt',
 'SETUP_NAME': 'Dein Name',
 'SETUP_NAME_HINT': 'freiwillig',
 'SETUP_PLAN': 'Rahmenplan',
 'SETUP_PLAN_HINT': 'Die Abschnitte B, D und E gehören zu anderen Fachrichtungen. Für Systemintegration '
                    'kannst du sie ausgeschaltet lassen.',
 'SETUP_DATES': 'Prüfungstermine',
 'SETUP_DATES_HINT': 'Format TT.MM.JJJJ, leer lassen geht auch. Mit Termin kommen in den sechs Monaten davor '
                     'neue Aufgaben zu prüfungsrelevanten Themen etwas früher dran, solange „Übungsauswahl '
                     'nach Rahmenplan gewichten“ an ist.',
 'GREETING': 'Hallo %s',
 'NAME_OPTION_HINT': 'Nur auf diesem Gerät gespeichert, nicht im Abgleich und nicht in der Sicherung. Mit '
                     'Namen steht oben im „Dashboard“ (am Handy: „Start“) „Hallo“ und dein Name.',
 'OPTIONS_TITLE': 'Rundgang und Hilfe',
 'OPTIONS_SUBTITLE': 'Einführung in 5 Schritten',
 'OPTIONS_TEXT': 'Der Rundgang zeigt die wichtigsten Bereiche und hilft beim Einrichten (Name, Rahmenplan, '
                 'Prüfungstermine). Die Hilfe erklärt alles in kurzen Texten.',
 'BTN_TOUR': 'Rundgang starten',
 'BTN_HELP': 'Hilfe öffnen',
 'HELP_TITLE': 'Hilfe',
 'HELP_SUBTITLE': 'Kurz erklärt',
 'HELP_INTRO': 'Kurze Antworten zu den wichtigsten Bereichen. Wähle ein Thema, um es aufzuklappen. Begriffe '
               'in „Anführungszeichen“ stehen genau so in der App.',
 'SEARCH_KIND': 'Hilfe',
 'UPDATE_PROTECTION_HINT': 'Meldet sich dein Schutzprogramm, ist das bei neuen Versionen normal. Mehr in der '
                           'Hilfe unter Update und Schutzprogramm.'}
RAHMENPLAN = {'OPTIONS_TITLE': 'Rahmenplan',
 'OPTIONS_SUBTITLE': 'Abdeckung im Fortschritt und Reihenfolge neuer Aufgaben',
 'WEIGHT_OPTION_TEXT': 'Übungsauswahl nach Rahmenplan gewichten',
 'WEIGHT_OPTION_HINT': 'Neue Aufgaben zu Punkten mit viel Ausbildungszeit kommen etwas früher dran, vor '
                       'einem Prüfungstermin die prüfungsrelevanten. Wiederholungen bleiben unverändert.',
 'FIELD_OPTION_TEXT': 'Aktuelles Lernfeld',
 'FIELD_OPTION_OFF': 'Aus',
 'AP1_DATE_TEXT': 'Termin AP1',
 'AP2_DATE_TEXT': 'Termin AP2',
 'DATE_HINT': 'TT.MM.JJJJ',
 'DATE_INVALID': 'Bitte ein Datum im Format TT.MM.JJJJ eingeben.',
 'NUR_DIESES_GERAET': 'nur für dieses Gerät',
 'SECTION_OPTION_TEXT': {'B': 'Abschnitt B anzeigen (Anwendungsentwicklung)',
                         'D': 'Abschnitt D anzeigen (Daten- und Prozessanalyse)',
                         'E': 'Abschnitt E anzeigen (Digitale Vernetzung)'}}
SEITEN = [{'id': 'willkommen',
  'titel': 'Willkommen',
  'text': 'Die Fachinformatiker Lernplattform hilft dir bei der Vorbereitung auf die Prüfungen AP1 und AP2 '
          '(Schwerpunkt Systemintegration).\n'
          '\n'
          'Lernen kannst du mit „Karteikarten“, im „Prüfungstrainer“ und mit den „AP1-Szenarien“ und '
          '„AP2-Szenarien“, die wie Prüfungsaufgaben aufgebaut sind. Am PC findest du alles links in der '
          'Seitenleiste, am Handy unter „Lernen“.'},
 {'id': 'fortschritt',
  'titel': 'Fortschritt und Rahmenplan',
  'text': 'Unter „Fortschritt“ siehst du, was du schon kannst: deine Ergebnisse und die '
          '„Rahmenplan-Abdeckung“ für jeden Punkt des Ausbildungsrahmenplans. Deine Lernserie steht oben im '
          'Banner des „Dashboard“ (am Handy: „Start“).\n'
          '\n'
          '„Jetzt wiederholen“ im „Dashboard“ (am Handy: „Start“) holt fällige Karten und Fragen zurück, '
          'damit du nichts vergisst.'},
 {'id': 'spiel',
  'titel': 'Spiel',
  'text': 'Im „Spiel“ arbeitest du in einer IT-Firma und löst Tickets aus dem Berufsalltag. Dein '
          'Wissensstand im Spiel kommt aus dem, was du mit Karteikarten und im Prüfungstrainer lernst.\n'
          '\n'
          'Später kannst du eine eigene Firma gründen.'},
 {'id': 'sicherung',
  'titel': 'Sicherung und Abgleich',
  'text': 'Dein Lernstand wird auf diesem Gerät gespeichert. In den „Optionen“ sicherst du ihn mit '
          '„Sicherung erstellen“ als Datei oder hältst PC und Handy mit „Abgleich PC und Handy“ auf '
          'demselben Stand.\n'
          '\n'
          'Fragen beantwortet die „Hilfe“. Diesen Rundgang kannst du in den „Optionen“ jederzeit wieder '
          'starten.'},
 {'id': 'einrichten',
  'titel': 'Jetzt einrichten',
  'text': 'Alle Angaben sind freiwillig und gelten nur für dieses Gerät. Rahmenplan und Termine kannst du '
          'später in den „Optionen“ unter „Lerninhalte“ bei „Rahmenplan“ ändern.'}]
ABSCHNITTE = [{'id': 'lernen',
  'titel': 'Lernen mit Karteikarten',
  'reiter': ['Karteikarten', 'Dashboard', 'Notizblock'],
  'text': 'Unter „Karteikarten“ (am Handy unter „Lernen“) übst du Fachbegriffe und Grundwissen. Oben wählst '
          'du den Fachbereich, zum Beispiel „Alle“ oder „Netzwerk“, und den Lernmodus:\n'
          '• „Freitext“: eigene Antwort schreiben, dann „Antwort prüfen“\n'
          '• „Multiple Choice“: eine Antwort auswählen\n'
          '• „Aufdecken“: erst überlegen, dann „Lösung aufdecken“ und mit „Gewusst“ oder „Nicht gewusst“ '
          'bewerten\n'
          '\n'
          'Was du schon bearbeitet hast, kommt nach einiger Zeit zur Wiederholung. „Jetzt wiederholen“ im '
          '„Dashboard“ (am Handy: „Start“) holt die fälligen Karten und Fragen. Im „Notizblock“ siehst du, '
          'was du noch üben musst.'},
 {'id': 'pruefung',
  'titel': 'Prüfung (AP1 und AP2)',
  'reiter': ['Prüfungstrainer', 'AP1-Szenarien', 'AP2-Szenarien', 'Testprojekt', 'Abschlussprojekt'],
  'text': 'Im „Prüfungstrainer“ übst du Prüfungsfragen mit Erklärung. Schalte oben auf „Prüfung“, um eine '
          'Prüfung nach IHK-Vorbild zu schreiben: AP1 oder einen der drei Teile der AP2 (Konzeption, '
          'Netzwerke, WiSo). Sie ist aufgebaut wie die echte Prüfung: Es läuft eine feste Zeit. Offene '
          'Aufgaben bewertest du nach der Abgabe selbst anhand der Musterlösung, WiSo wird automatisch '
          'ausgewertet. Danach siehst du Punkte und die Note nach IHK-Schlüssel (Orientierung, keine '
          'amtliche Note).\n'
          '\n'
          '„AP1-Szenarien“ und „AP2-Szenarien“ sind längere Aufgaben wie in der Prüfung, mit Musterlösung. '
          'Im „Testprojekt“ übst du Kundenaufträge, im „Abschlussprojekt“ planst du dein eigenes IHK-Projekt '
          'vom Antrag bis zum Fachgespräch. Am Handy findest du alles unter „Lernen“.\n'
          '\n'
          'Wenn du für dein Abschlussprojekt KI-Hilfsmittel nutzt (zum Beispiel für Texte, Code oder '
          'Bilder), verlangen manche Kammern eine Angabe dazu in der Projektdokumentation. Frag bei deiner '
          'IHK nach, was für dich gilt.'},
 {'id': 'rechner',
  'titel': 'Rechner (Subnetting, RAID, Bildschirm, USV)',
  'reiter': ['Rechner'],
  'text': 'Unter „Rechner“ gibt es vier Rechner: „Subnetting“ (IPv4 und IPv6), „RAID-Kapazität“, '
          '„Bildschirm-Datenvolumen“ und ganz unten „USV-Kapazität“. Werte eintragen und „Berechnen“ wählen. '
          '„Rechenweg anzeigen“ erklärt, wie man es von Hand rechnet.\n'
          '\n'
          'Der USV-Rechner rechnet mit „Akku berechnen“, wie groß der Akku für eine gewünschte Laufzeit sein '
          'muss, und mit „Laufzeit berechnen“, wie lange ein vorhandener Akku reicht. „Empfehlung“ sagt, ob '
          'die USV passt oder zu klein ist, und zeigt die Regel dazu. Ein Bild zeigt Last, USV, Akku und '
          'Laufzeit. Unter „Übungsaufgaben“ gibt es Aufgaben mit „Lösung anzeigen“ und „Nächste Aufgabe“.\n'
          '\n'
          'Oben unter „Trainer“ rechnest du selbst: „IPv4-Subnetz“, „VLSM“, „Binär/Hex/Dezimal“, '
          '„IPv6-Schreibweise“, „Netz aufteilen“ und „Nach Hostbedarf“, jeweils „Leicht“, „Mittel“ oder '
          '„Schwer“. Trainer-Aufgaben zählen zum Tagesziel.'},
 {'id': 'spiel',
  'titel': 'Firma-Spiel',
  'reiter': ['Spiel'],
  'text': 'Im „Spiel“ arbeitest du bei der Bitweiche IT-Service GmbH. Jeder Arbeitstag bringt Tickets, zum '
          'Beispiel PCs zusammenbauen, Fehler suchen oder Netze planen. Dein Wissensstand im Spiel kommt aus '
          'dem, was du mit Karteikarten und im Prüfungstrainer lernst.\n'
          '\n'
          'Mit genug Erspartem und gutem Ansehen kannst du eine eigene Firma gründen, Leute einstellen und '
          'Aufträge annehmen. Es gibt drei Spielstände für getrennte Durchgänge. Der Lernfortschritt gilt '
          'für alle gemeinsam.'},
 {'id': 'fortschritt',
  'titel': 'Fortschritt und Rahmenplan',
  'reiter': ['Fortschritt', 'Optionen'],
  'text': 'Unter „Fortschritt“ siehst du die Ergebnisse deiner Prüfungen und wie viel du an den letzten '
          'Tagen gelernt hast. Deine Lernserie steht oben im Banner des „Dashboard“ (am Handy: „Start“). Die '
          '„Rahmenplan-Abdeckung“ zeigt für jeden Punkt des Ausbildungsrahmenplans, wie gut du ihn schon '
          'kannst. Mit „Jetzt üben“ übst du gezielt einen Punkt.\n'
          '\n'
          'In den „Optionen“ unter „Lerninhalte“ bei „Rahmenplan“ stellst du ein, welche Abschnitte '
          'angezeigt werden, dein aktuelles Lernfeld und die Termine für AP1 und AP2. Ist „Übungsauswahl '
          'nach Rahmenplan gewichten“ an, kommen in den sechs Monaten vor einem Termin neue Aufgaben zu '
          'prüfungsrelevanten Themen etwas früher dran.'},
 {'id': 'sicherung',
  'titel': 'Sicherung und Abgleich',
  'reiter': ['Optionen'],
  'text': 'Dein Lernstand wird auf diesem Gerät gespeichert. In den „Optionen“ unter „Sicherung“ speicherst '
          'du ihn mit „Sicherung erstellen“ als Datei, zum Beispiel vor einem Gerätewechsel. Mit „Sicherung '
          'einspielen“ holst du ihn zurück, auch auf einem anderen Gerät. Dabei wählst du „Zusammenführen“ '
          '(nur Fehlendes ergänzen) oder „Alles ersetzen“. In der Sicherungsdatei steht auch der Name des '
          'Geräts, am PC mit dem Rechnernamen.\n'
          '\n'
          'Mit „Abgleich PC und Handy“ haben PC und Handy denselben Lernstand. Wie du ihn einrichtest, steht '
          'im nächsten Thema.'},
 {'id': 'abgleich',
  'titel': 'Abgleich einrichten',
  'reiter': ['Optionen'],
  'text': 'Für den Abgleich brauchst du ein kostenloses Konto bei github.com:\n'
          '1. Lege dort ein neues, privates Repository an, zum Beispiel mit dem Namen fisi-lernstand.\n'
          '2. Erzeuge einen Zugangsschlüssel: Profilbild > Settings > Developer settings > Personal access '
          'tokens > Fine-grained tokens > Generate new token. Wähle nur dieses Repository und bei Contents '
          'das Recht Read and write. Kopiere den Schlüssel (beginnt mit github_pat_), GitHub zeigt ihn nur '
          'einmal.\n'
          '3. In den „Optionen“ unter „Abgleich PC und Handy“ trägst du bei „Repository (Benutzer/Name)“ zum '
          'Beispiel deinname/fisi-lernstand und bei „Zugangsschlüssel (Token)“ den Schlüssel ein und wählst '
          '„Speichern und abgleichen“. Auf dem anderen Gerät genauso.\n'
          '\n'
          'Übertragen werden: was du wann gelernt hast, die Ergebnisse deiner Tests und Prüfungen (Punkte '
          'und Note, nicht deine Antworten), die Texte deines Abschlussprojekts, deine Spielstände und die '
          'Bestenliste. In jeder Änderung im Repository steht außerdem der Name des Geräts, am PC mit dem '
          'Rechnernamen.\n'
          '\n'
          'Achtung: Auch Löschen wird abgeglichen.'},
 {'id': 'update',
  'titel': 'Update',
  'reiter': ['Optionen'],
  'text': 'In den „Optionen“ unter „Updates“ prüft „Nach Updates suchen“, ob es eine neue Version gibt. Ist '
          '„Beim Start automatisch nach Updates suchen“ an, sucht die App beim Start von selbst.\n'
          '\n'
          'Am PC führt „Jetzt aktualisieren“ durch Download und Installation, danach startet das Programm '
          'neu. Am Handy wird die neue Version im Browser heruntergeladen. Danach die Datei öffnen und '
          '„Installieren“ tippen. Dein Lernstand bleibt erhalten.\n'
          '\n'
          'Unter Linux legt „Desktop-Verknüpfung anlegen“ eine Verknüpfung auf dem Desktop an oder erneuert '
          'sie, zum Beispiel nachdem du das AppImage verschoben hast.'},
 {'id': 'farben',
  'titel': 'Eigene Farben',
  'reiter': ['Optionen'],
  'text': 'In den „Optionen“ unter „Optische Anpassungen“ bei „Farben“ liegen die „Vorlagen“ mit '
          '„Grundfarbe“ und „Hintergrund“ (zum Aufklappen). In der hellen Darstellung heißen die '
          'Hintergründe nach ihrer hellen Fläche: „Flieder“, „Hellblau“, „Mintgrün“, „Rosé“, „Hellgrau“ und '
          '„Weiß“.\n'
          '\n'
          'Darunter steht der Bereich „Eigene Farben“. Mit „Farbton“, „Sättigung“ und „Helligkeit“ stellst '
          'du „Akzent 1“ und „Akzent 2“ (den Farbverlauf der Knöpfe) und den Hintergrund ein. Die Vorschau '
          'zeigt sofort, wie es aussieht. Die Regler starten bei deiner Farbwelt. Sobald du einen Regler '
          'bewegst, werden alle Farben passend zu deinen Einstellungen neu berechnet.\n'
          '\n'
          'Ein Warnhinweis bedeutet: Etwas ist schlecht lesbar oder die Fachbereichsfarben sind schwer zu '
          'unterscheiden. Er nennt den gemessenen Wert. Speichern geht trotzdem.\n'
          '\n'
          '„Speichern“ übernimmt die Farben. „Auf Farbwelt zurücksetzen“ stellt die gewählte Farbwelt wieder '
          'her. Die eigenen Farben gelten nur auf diesem Gerät und nur für die gewählte Darstellung (Hell '
          'oder Dunkel).'},
 {'id': 'problem',
  'titel': 'Problem melden',
  'reiter': ['Optionen'],
  'text': 'Wenn etwas nicht klappt: In den „Optionen“ unter „Diagnose und Werkzeuge“ steht bei „Problem '
          'melden“ ein Bericht mit Programmversion, Gerät, Datenordner und Fehlerprotokoll. '
          'Zugangsschlüssel, Passwörter und dein Benutzername in Pfaden werden entfernt. Andere Angaben aus '
          'Fehlermeldungen können darin stehen. Sieh dir den Bericht vor dem Weitergeben an.\n'
          '\n'
          'Speichere den Bericht mit „Als Datei speichern“ (oder „Kopieren“) und gib ihn an die Person '
          'weiter, von der du das Programm hast. Schreib kurz dazu: Was hast du gemacht, was ist passiert, '
          'was hast du erwartet?'},
 {'id': 'leistung',
  'titel': 'Leistungsmessung',
  'reiter': ['Optionen'],
  'text': 'In den „Optionen“ unter „Diagnose und Werkzeuge“ schaltet bei „Leistungsmessung“ der Schalter '
          '„Messung aufzeichnen“ die Messung ein und aus. Sie ist zu Beginn aus. Eingeschaltet schreibt das '
          'Programm in eine Messdatei, wie lange Seitenwechsel, Darstellungswechsel und Änderungen der '
          'Fenstergröße dauern und wie viel Arbeitsspeicher es dabei braucht.\n'
          '\n'
          'Die Messdatei „leistungsmessung.csv“ liegt im Datenordner auf diesem Gerät. „Messdatei zeigen“ '
          'öffnet am PC den Datenordner, am Handy kannst du die Datei speichern oder teilen. „Messdatei '
          'löschen“ entfernt sie. Es wird nichts gesendet. Die Datei enthält keine Lerninhalte und keinen '
          'Namen. Die Einstellung gilt nur für dieses Gerät und kommt nicht in den Abgleich.'},
 {'id': 'haenger',
  'titel': 'Hänger-Diagnose (PC)',
  'reiter': ['Optionen'],
  'text': 'Bleibt das Programm am PC hängen, schreibt es nach 30 Sekunden automatisch in die Datei '
          'haenger.log, an welcher Stelle es steht. Blockaden ab 5 Sekunden stehen kurz in fehler.log, bei '
          'bekannten langen Vorgängen (Seitenaufbau, Darstellungswechsel, Schriftwechsel, Vorladen) erst ab '
          '30 Sekunden. Der Abgleich läuft im Hintergrund und blockiert nicht. Beide Dateien liegen im '
          'Datenordner und stehen im Bericht bei „Problem melden“. Es wird nichts gesendet.\n'
          '\n'
          'Ab Version 0.59.2 unter Linux: Hängt das Programm, öffne ein Terminal und gib diesen Befehl ein:\n'
          'kill -USR1 $(pgrep -i -x -o fisi-lernplattf)\n'
          'Das Programm schreibt dann in die Datei haenger.log, an welcher Stelle es gerade steht. Es wird '
          'dabei nicht beendet. Achtung: In Version 0.59.1 und älter beendet derselbe Befehl das Programm. '
          'In den „Optionen“ unter „Diagnose und Werkzeuge“ kopiert „Befehl kopieren“ ihn für dich.\n'
          '\n'
          'Unter Linux mit IBus schaltet das Programm für sich die IBus-Eingabe ab (X11 ab 0.60, bisher nur '
          'im Test geprüft), sonst kann es einfrieren. Umlaute, ß und AltGr gehen weiter. Wer IBus braucht, '
          'startet es mit\n'
          'FISI_XIM=1 fisi-lernplattform\n'
          'Mit FISI_XIM=1 kann das Programm wieder langsamer sein oder hängen.'},
 {'id': 'tastatur',
  'titel': 'Bedienung mit der Tastatur (PC)',
  'reiter': [],
  'text': 'Am PC lassen sich die Lernbereiche und die Optionen ohne Maus bedienen. Die Spielkarte und die '
          'Grundrisse im Spiel brauchen noch die Maus.\n'
          '• Tab springt zum nächsten Element, Umschalt+Tab zurück. Das gewählte Element hat einen '
          'deutlichen Rahmen.\n'
          '• Eingabe oder Leertaste drückt Knöpfe, schaltet Schalter um und klappt Bereiche auf und zu.\n'
          '• Esc überspringt den Rundgang.\n'
          '• Strg+F springt in die Suche oben rechts.\n'
          '• Strg+Eingabe prüft im Antwortfeld einer Karteikarte deine Antwort.\n'
          '\n'
          'Die Schrift vergrößerst du in den „Optionen“ unter „Optische Anpassungen“ bei „Schriftgröße“ mit '
          '„Groß“, „Sehr groß“, „Extra groß“ oder „Maximal“.'},
 {'id': 'schutzprogramm',
  'titel': 'Warnung vom Schutzprogramm',
  'reiter': [],
  'text': 'Manche Schutzprogramme und Browser warnen beim Herunterladen oder Installieren der '
          'Fachinformatiker Lernplattform. Der Grund: Das Programm ist neu, wird selten heruntergeladen und '
          'ist nicht digital signiert.\n'
          '\n'
          'Lade die App deshalb nur von der Release-Seite herunter: '
          'github.com/papst2142-cmd/Fisi-Lernplattform/releases. Dort steht zu jeder Datei eine Prüfsumme '
          '(SHA-256), mit der du die Datei vergleichen kannst. Schalte den Schutz nicht dauerhaft ab. Warnt '
          'dein Schutzprogramm auch bei der Datei von der Release-Seite, kannst du den Fehlalarm beim '
          'Hersteller des Schutzprogramms melden.'},
 {'id': 'update_schutz',
  'titel': 'Update und Schutzprogramm',
  'reiter': ['Optionen'],
  'text': 'Bei einer neuen Version kann sich dein Schutzprogramm melden. Das ist bei neuen, noch wenig '
          'bekannten Versionen normal. So kann es aussehen:\n'
          '• Es fragt, ob die Installation erlaubt ist. Erlaube sie.\n'
          '• Beim ersten Start meldet es, dass das Programm unter Vorbehalt gestartet wurde, nach etwa einer '
          'Minute vielleicht noch einmal. Mit Trotzdem ausführen startet das Programm normal.\n'
          '• Eine von Hand heruntergeladene Installationsdatei kann in der Quarantäne landen, auch wenn sie '
          'in Ordnung ist (Fehlalarm).\n'
          '• Am Handy fragt Android vor der Installation nach. Erlaube sie.\n'
          '\n'
          'Das Update in den „Optionen“ unter „Updates“ lädt die Datei direkt von der Release-Seite. Lädst '
          'du selbst, dann nur von github.com/papst2142-cmd/Fisi-Lernplattform/releases, und vergleiche die '
          'Prüfsumme (SHA-256). Abschalten musst du dafür nichts. Bist du unsicher, frag nach, bevor du '
          'etwas erlaubst.'},
 {'id': 'rechtliches',
  'titel': 'Rechtliches',
  'reiter': ['Optionen'],
  'text': 'Die Fachinformatiker Lernplattform ist ein privates Lernprogramm. Sie ist kein Angebot und kein '
          'Produkt einer Industrie- und Handelskammer (IHK) oder einer anderen Prüfungsstelle und wird von '
          'diesen weder herausgegeben noch geprüft. Die Aufgaben, Szenarien und Testprojekte sind eigene '
          'Übungsaufgaben. Sie enthalten keine offiziellen Prüfungsaufgaben. Bezeichnungen wie „AP1“, „AP2“ '
          'oder „Note nach IHK-Schlüssel“ beschreiben nur, wofür geübt wird und nach welchem Notenschlüssel '
          'die Übung ausgewertet wird.\n'
          '\n'
          'Copyright (c) 2026 Nico H – Alle Rechte vorbehalten. Erstellt mit Hilfe von Claude (Anthropic).\n'
          '\n'
          'In den „Optionen“ unter „Über das Programm“ findest du „Lizenz“ und „Hinweise zu '
          'Fremdbestandteilen“ (Bestandteile anderer Hersteller und ihre Lizenzen).'}]

OPTIONS_SUBTITLE = "Einführung in 5 Schritten"
RELEASE_PAGE_TEXT = "github.com/papst2142-cmd/Fisi-Lernplattform/releases"
DATE_INVALID = RAHMENPLAN["DATE_INVALID"]
GREETING = KONSTANTEN["GREETING"]

# ---------------------------------------------------------------------------
# Feste Tabelle (Nachtrag P4-11, Abschnitt 5, maßgeblich)
# ---------------------------------------------------------------------------
PAARE = [
    {"nr": "N1", "thema": "abgleich",
     "alt": "Namen fisi-lernstand.", "neu": "Namen Fi-Lernstand."},
    {"nr": "N1", "thema": "abgleich",
     "alt": "deinname/fisi-lernstand", "neu": "deinname/Fi-Lernstand"},
    {"nr": "N2", "thema": "schutzprogramm",
     "alt": "github.com/papst2142-cmd/Fisi-Lernplattform/releases",
     "neu": "github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"},
    {"nr": "N2", "thema": "update_schutz",
     "alt": "github.com/papst2142-cmd/Fisi-Lernplattform/releases",
     "neu": "github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"},
    {"nr": "W1", "thema": "update",
     "alt": "\n\nUnter Linux legt „Desktop-Verknüpfung anlegen“ eine Verknüpfung auf dem Desktop an oder erneuert sie, zum Beispiel nachdem du das AppImage verschoben hast.",
     # Phase 6 W2: neu laut Weisung P6-7 (Nachtrag Nico 23.10.2026 20:07), Wortliste W2-12 Spalte "C# a"
     "neu": "\n\nUnter Linux legt „Verknüpfung anlegen“ eine Verknüpfung im Anwendungsmenü und auf dem Desktop an oder erneuert sie, zum Beispiel nachdem du das AppImage verschoben hast."},
    {"nr": "W2", "thema": "haenger",
     "alt": "\n\nAb Version 0.59.2 unter Linux: Hängt das Programm, öffne ein Terminal und gib diesen Befehl ein:\nkill -USR1 $(pgrep -i -x -o fisi-lernplattf)\nDas Programm schreibt dann in die Datei haenger.log, an welcher Stelle es gerade steht. Es wird dabei nicht beendet. Achtung: In Version 0.59.1 und älter beendet derselbe Befehl das Programm. In den „Optionen“ unter „Diagnose und Werkzeuge“ kopiert „Befehl kopieren“ ihn für dich.",
     "neu": ""},
    {"nr": "W3", "thema": "haenger",
     "alt": "Unter Linux mit IBus schaltet das Programm für sich die IBus-Eingabe ab (X11 ab 0.60, bisher nur im Test geprüft), sonst kann es einfrieren. Umlaute, ß und AltGr gehen weiter. Wer IBus braucht, startet es mit\nFISI_XIM=1 fisi-lernplattform\nMit FISI_XIM=1 kann das Programm wieder langsamer sein oder hängen.",
     "neu": "Hängt unter Linux das Tippen, startest du das Programm mit dem Startschalter\n--ohne-eingabemethode\nDann nutzt es keine Eingabemethode, zum Beispiel IBus."},
    {"nr": "W4", "thema": "tastatur",
     "alt": " Die Spielkarte und die Grundrisse im Spiel brauchen noch die Maus.",
     "neu": ""},
    # Phase 7 W6 (Weisung P7-11, Hilfe-Abschnitt Spiel): Satz der Stadt, freigegeben mit Weisung P7-9
    {"nr": "S1", "thema": "spiel",
     "alt": "Der Lernfortschritt gilt für alle gemeinsam.",
     "neu": "Der Lernfortschritt gilt für alle gemeinsam.\n\nIm Spiel vergeht nur Spielzeit: Jeder Schritt kostet Minuten, die echte Uhr spielt keine Rolle."},
    # UNKLAR: Nachtrag Abschnitt 3 nennt fuer RELEASE_PAGE_TEXT nur thema; "nr" setze ich
    # auf "N2" (Tabelle Abschnitt 3: "RELEASE_PAGE_TEXT | NEU: N2"), Platz am Ende der Liste.
    {"nr": "N2", "thema": "RELEASE_PAGE_TEXT",
     "alt": "github.com/papst2142-cmd/Fisi-Lernplattform/releases",
     "neu": "github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"},
]


def abschnitte_mit_paaren():
    """16 Themen mit eingesetzten Paaren (jedes alt genau einmal)."""
    erg = []
    for a in ABSCHNITTE:
        text = a["text"]
        for p in PAARE:
            if p["thema"] == a["id"]:
                if text.count(p["alt"]) != 1:
                    raise SystemExit("Paar %s/%s kommt nicht genau einmal vor" % (p["nr"], p["thema"]))
                text = text.replace(p["alt"], p["neu"])
        erg.append(dict(a, text=text))
    if RELEASE_PAGE_TEXT.count(PAARE[-1]["alt"]) != 1:
        raise SystemExit("RELEASE_PAGE_TEXT passt nicht")
    return erg


# ---------------------------------------------------------------------------
# Datum (Spezifikation Abschnitt 2)
# ---------------------------------------------------------------------------
# \d in einem str-Muster von re ist jede Unicode-Dezimalziffer (Kategorie Nd,
# gleich str.isdecimal); die uebrigen Ziffern im Muster sind ASCII-Literale.
_D = r"3[0-1]|[1-2]\d|0[1-9]|[1-9]| [1-9]"
_M = r"1[0-2]|0[1-9]|[1-9]"
_Y = r"\d\d\d\d"
_MUSTER_A = re.compile(r"(%s)\.(%s)\.(%s)" % (_D, _M, _Y))
_MUSTER_B = re.compile(r"(%s)-(%s)-(%s)" % (_Y, _M, _D))


def _schaltjahr(j):
    return j % 4 == 0 and (j % 100 != 0 or j % 400 == 0)


def _gueltig(j, m, t):
    if not (1 <= j <= 9999) or not (1 <= m <= 12):
        return False
    tage = [31, 29 if _schaltjahr(j) else 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][m - 1]
    return 1 <= t <= tage


def parse_date(text):
    """Gibt (jahr, monat, tag) oder None."""
    t = str(text or "").strip()
    # UNKLAR: "^...$" der Spezifikation lese ich als Treffer ueber den ganzen Text
    # (fullmatch). Ein "$" vor einem letzten "\n" kann nach strip() nicht auftreten.
    m = _MUSTER_A.fullmatch(t)
    if m:
        tag, monat, jahr = int(m.group(1)), int(m.group(2)), int(m.group(3))
        if _gueltig(jahr, monat, tag):
            return (jahr, monat, tag)
    m = _MUSTER_B.fullmatch(t)
    if m:
        jahr, monat, tag = int(m.group(1)), int(m.group(2)), int(m.group(3))
        if _gueltig(jahr, monat, tag):
            return (jahr, monat, tag)
    return None


def iso_text(d):
    return "%04d-%02d-%02d" % d


def date_text(text):
    d = parse_date(text)
    if d is None:
        return ""
    return "%02d.%02d.%s" % (d[2], d[1], str(d[0]))


def check_date(text):
    if not str(text or "").strip():
        return ["", None]
    d = parse_date(text)
    if d is None:
        return [None, DATE_INVALID]
    return [iso_text(d), None]


DATUM_FAELLE = [
    "", " ", "1.2.2027", " 1.2.2027 ", "01.02.2027", "2027-02-01", "2027-2-1", "2027-02- 1", " 1.02.2027",
    "01. 2.2027", "31.02.2027", "29.02.2027", "29.02.2028", "01.13.2027", "01.01.0000", "01.01.0001", "1.2.0999",
    "31.12.9999", "1.1.99", "01.02.20271", "1.2.2027.", "2027-02-01T00", "1.2.２０２７",
    "١.٢.٢٠٢٧",
    "1.2.2027 ", "1.2.2027\x1c", "﻿1.2.2027", "1.2.2027​", "15.6.2027",
]


def teil_datum():
    erg = {}
    for f in DATUM_FAELLE:
        d = parse_date(f)
        erg[f] = [iso_text(d) if d else None, date_text(f), check_date(f)]
    return erg


# ---------------------------------------------------------------------------
# Suche (Spezifikation Abschnitt 3, Nachtrag Abschnitt 3: ueber Texte MIT Paaren)
# ---------------------------------------------------------------------------
def search_help(q, abschnitte):
    nadel = str(q or "").strip().lower()
    if not nadel:
        return []
    erg = []
    for a in abschnitte:
        text = a["text"].replace("\n", " ")
        if nadel in a["titel"].lower() or nadel in text.lower():
            # UNKLAR: Ergebnis-Text = ersetzter Text MIT eingesetzten Paaren
            # (Nachtrag: "erst die Paare einsetzen, dann suchen wie Abschnitt 3").
            erg.append([a["id"], a["titel"], text])
    return erg


SUCHE_FAELLE = [
    "", "   ", "rundgang", "RUNDGANG", "Abgleich", "github_pat_", "ß", "GRÖSSE", "größe", "İ", "Prüfung (AP1",
    "„Optionen“", "kill -USR1", "Fisi-Lernplattform", "\tHilfe\n", "\x1chilfe", "Bitweiche", "zzz", "SHA-256",
    "a b", "Akku  berechnen", "Optionen\nSicherung", "Fine-grained", "ki-hilfsmittel", "Esc", "e",
    # Nachtrag P4-11 Abschnitt 3: neue Anfragen am Ende, in dieser Reihenfolge
    "fisi", "eingabemethode", "Vorladen", "Desktop", "Fachinformatiker-Lernplattform/releases", "Spielkarte",
]


def teil_suche():
    abschnitte = abschnitte_mit_paaren()
    return {f: search_help(f, abschnitte) for f in SUCHE_FAELLE}


# ---------------------------------------------------------------------------
# Name (Spezifikation Abschnitt 4)
# ---------------------------------------------------------------------------
def clean_name(text):
    return " ".join(str(text or "").split())[:40]


def greeting(name):
    n = clean_name(name)
    return GREETING % n if n else ""


NAME_FAELLE = [
    None, "", "  Alex   Muster ", "a\u001cb\u001fc", "y" * 80, "\U0001F600" * 41, " Kim Lee​",
    123, 0, True, False, 1.0, [1], {"a": 1}, [], "Ünal Şahin",
]


def teil_name():
    erg = []
    for x in NAME_FAELLE:
        s = str(x or "")
        erg.append([x, clean_name(s), greeting(s)])
    return erg


# ---------------------------------------------------------------------------
# Einstellungen (Spezifikation Abschnitt 5)
# ---------------------------------------------------------------------------
LERNFELDER = ["LF %d" % i for i in range(1, 10)] + [
    "LF 10b", "LF 11b", "LF 12b", "LF 10a", "LF 11a", "LF 12a"]

RP_VORGABEN = {
    "rp_abschnitt_b": False,
    "rp_abschnitt_d": False,
    "rp_abschnitt_e": False,
    "rp_gewichtung": True,
    "rp_lernfeld": "",
    "rp_termin_ap1": "2027-09-29",
    "rp_termin_ap2": "2028-04-26",
}


class Einstellungen:
    def __init__(self, pfad):
        self.pfad = pfad

    def load_settings(self):
        s = {"auto_check": True}
        if os.path.exists(self.pfad):
            with open(self.pfad, encoding="utf-8") as f:
                d = json.load(f)
            if isinstance(d, dict):
                s.update(d)
        return s

    def save_settings(self, s):
        with open(self.pfad, "w", encoding="utf-8") as f:
            json.dump(s, f, ensure_ascii=False)

    def datei(self):
        if not os.path.exists(self.pfad):
            return None
        with open(self.pfad, encoding="utf-8") as f:
            return json.load(f)

    def setzen(self, start):
        if start is None:
            if os.path.exists(self.pfad):
                os.remove(self.pfad)
        else:
            self.save_settings(start)

    def save_rp_settings(self, **kw):
        s = self.load_settings()
        for k, v in kw.items():
            if k in RP_VORGABEN:
                s[k] = v
        self.save_settings(s)

    def setup_values(self):
        s = self.load_settings()
        rp = rp_settings(s)
        return {
            "name": str(s.get("nutzer_name") or ""),
            "abschnitte": {x: rp["rp_abschnitt_" + x.lower()] for x in ("B", "D", "E")},
            "rp_termin_ap1": date_text(rp["rp_termin_ap1"]),
            "rp_termin_ap2": date_text(rp["rp_termin_ap2"]),
        }

    def save_setup(self, name, abschnitte, ap1, ap2):
        iso1, fehler1 = check_date(ap1)
        iso2, fehler2 = check_date(ap2)
        if fehler1 or fehler2:
            return DATE_INVALID
        s = self.load_settings()
        s["nutzer_name"] = clean_name(name)
        self.save_settings(s)
        kw = {}
        for x in ("B", "D", "E"):
            if x in abschnitte:
                kw["rp_abschnitt_" + x.lower()] = bool(abschnitte[x])
        kw["rp_termin_ap1"] = iso1 or ""
        kw["rp_termin_ap2"] = iso2 or ""
        self.save_rp_settings(**kw)
        return None

    def mark_tour_seen(self, heute):
        s = self.load_settings()
        s["rundgang_gesehen"] = heute
        self.save_settings(s)


def rp_settings(s):
    r = dict(RP_VORGABEN)
    # UNKLAR: "vorhandene Schluessel aus s uebernehmen" lese ich als: nur die sieben
    # Vorgabe-Schluessel; fremde Schluessel kommen nicht in das Ergebnis.
    for k in RP_VORGABEN:
        if k in s:
            r[k] = s[k]
    for k in ("rp_abschnitt_b", "rp_abschnitt_d", "rp_abschnitt_e", "rp_gewichtung"):
        r[k] = bool(r[k])
    lf = r["rp_lernfeld"]
    r["rp_lernfeld"] = lf if isinstance(lf, str) and lf in LERNFELDER else ""
    for k in ("rp_termin_ap1", "rp_termin_ap2"):
        d = parse_date(str(r[k] or ""))
        r[k] = iso_text(d) if d else ""
    return r


RP_FAELLE = [
    {},
    {"rp_abschnitt_b": "ja", "rp_gewichtung": 0, "rp_lernfeld": "LF 10a", "rp_termin_ap1": 20270101,
     "rp_termin_ap2": "1.2.2028"},
    {"rp_abschnitt_e": [], "rp_lernfeld": 5, "rp_termin_ap2": "2028-02-30"},
    {"rp_termin_ap1": "1.2.2027\u001c", "rp_termin_ap2": "1.2.２０２７"},
]

SETUP_FAELLE = [
    (None, ("  Alex  ", {"B": True, "D": False, "E": True}, "29.09.2027", "26.04.2028")),
    ({"darstellung": "hell", "rp_abschnitt_d": True, "x": [1]}, ("Kim", {"B": True}, "", " ")),
    ({"darstellung": "hell"}, ("Kim", {"B": True}, "31.02.2027", "1.1.2028")),
    ({"rp_abschnitt_b": "ja", "rp_termin_ap1": "kaputt", "rp_lernfeld": "LF 99"},
     ("", {}, "1.2.2027", "2028-4-26")),
    (None, ("x", {"B": True}, "", "29.02.2027")),
]

HEUTE = "2026-10-22"


def teil_einstellungen(ordner):
    e = Einstellungen(os.path.join(ordner, "einstellungen.json"))
    faelle = []
    for start, args in SETUP_FAELLE:
        e.setzen(start)
        fehler = e.save_setup(*args)
        faelle.append([fehler, e.datei(), e.setup_values()])
    e.setzen({"a": 1})
    e.save_rp_settings(rp_abschnitt_b=True, foo=1, rp_lernfeld="LF 3")
    rp_speichern = e.datei()
    e.mark_tour_seen(HEUTE)
    merker = e.datei()
    return {
        "rp_settings": [rp_settings(f) for f in RP_FAELLE],
        "faelle": faelle,
        "rp_speichern": rp_speichern,
        "rundgang_merker": merker,
    }


# ---------------------------------------------------------------------------
# Rundgang (Spezifikation Abschnitt 6)
# ---------------------------------------------------------------------------
def tour_seen(s):
    return bool(s.get("rundgang_gesehen"))


def db_has_data(pfad):
    try:
        con = sqlite3.connect(pfad)
    except Exception:  # UNKLAR: Fehler schon beim Oeffnen zaehle ich wie eine Ausnahme = true
        return True
    try:
        try:
            namen = [r[0] for r in con.execute(
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'")]
        except Exception:
            return True
        for n in namen:
            try:
                zeile = con.execute('SELECT 1 FROM "%s" LIMIT 1' % n.replace('"', '""')).fetchone()
            except Exception:
                return True
            if zeile is not None:
                return True
        return False
    finally:
        con.close()


def tour_due(db, s):
    return not tour_seen(s) and not db_has_data(db)


MERKER = [{}, {"rundgang_gesehen": "2026-10-22"}, {"rundgang_gesehen": ""}, {"rundgang_gesehen": 0},
          {"rundgang_gesehen": True}, {"rundgang_gesehen": []}, {"rundgang_gesehen": "0"}]


def _db_bauen(pfad, befehle):
    con = sqlite3.connect(pfad)
    for b in befehle:
        con.execute(b)
    con.commit()
    con.close()


def teil_rundgang(ordner):
    dbs = {}
    p = os.path.join(ordner, "leer.db")
    _db_bauen(p, [])
    dbs["leer"] = p
    p = os.path.join(ordner, "tabellen.db")
    _db_bauen(p, ["CREATE TABLE t1 (a)", "CREATE TABLE t2 (a)"])
    dbs["tabellen"] = p
    p = os.path.join(ordner, "zweite.db")
    _db_bauen(p, ["CREATE TABLE t1 (a)", "CREATE TABLE t2 (a)", "INSERT INTO t2 VALUES (1)"])
    dbs["zweite"] = p
    p = os.path.join(ordner, "anfuehrung.db")
    _db_bauen(p, ['CREATE TABLE "a""b" (x)', 'INSERT INTO "a""b" VALUES (1)'])
    dbs["anfuehrung"] = p
    p = os.path.join(ordner, "kaputt.db")
    with open(p, "wb") as f:
        f.write(b"x" * 100)
    dbs["kaputt"] = p
    return {
        "daten": {k: db_has_data(v) for k, v in dbs.items()},
        "gesehen": [tour_seen(m) for m in MERKER],
        "faellig": {k: [tour_due(v, m) for m in MERKER] for k, v in dbs.items()},
    }


# ---------------------------------------------------------------------------
# Oberflaeche (Spezifikation Abschnitt 7)
# ---------------------------------------------------------------------------
_SOT = ["r:SECTION_OPTION_TEXT.B", "r:SECTION_OPTION_TEXT.D", "r:SECTION_OPTION_TEXT.E"]
ANSICHTEN = {
    "rundgang_kopf": ["k:TOUR_TITLE", "k:TOUR_STEP"],
    # Menge, fuer PC und Handy gleich; Reihenfolge wie in der Spezifikation
    "rundgang_knoepfe": ["k:TOUR_SKIP", "k:TOUR_BACK", "k:TOUR_NEXT", "k:TOUR_FINISH"],
    "einrichten": ["k:SETUP_NAME", "k:SETUP_NAME_HINT", "k:SETUP_PLAN"] + _SOT + [
        "k:SETUP_PLAN_HINT", "k:SETUP_DATES", "r:AP1_DATE_TEXT", "r:AP2_DATE_TEXT", "k:SETUP_DATES_HINT",
        "r:DATE_INVALID"],
    "hilfe_kopf": ["k:HELP_TITLE", "k:HELP_SUBTITLE", "k:HELP_INTRO", "k:BTN_TOUR"],
    "optionen_rundgang": ["k:OPTIONS_TITLE", "k:OPTIONS_SUBTITLE", "k:OPTIONS_TEXT", "k:BTN_TOUR", "k:BTN_HELP",
                          "k:SETUP_NAME", "k:SETUP_NAME_HINT", "k:NAME_OPTION_HINT"],
    "optionen_rahmenplan": ["r:OPTIONS_TITLE", "r:NUR_DIESES_GERAET", "r:OPTIONS_SUBTITLE"] + _SOT + [
        "r:WEIGHT_OPTION_TEXT", "r:WEIGHT_OPTION_HINT", "r:FIELD_OPTION_TEXT", "r:FIELD_OPTION_OFF",
        "r:AP1_DATE_TEXT", "r:AP2_DATE_TEXT", "r:DATE_INVALID"],
}


def teil_oberflaeche():
    erg = {k: {"pc": list(v), "handy": list(v)} for k, v in ANSICHTEN.items()}
    erg["rundgang_tasten"] = {"pc": ["k:TOUR_KEYS"], "handy": []}
    # Selbstpruefung: jeder Name muss in Teil texte existieren
    for ans in erg.values():
        for n in ans["pc"]:
            art, name = n.split(":", 1)
            if art == "k":
                assert name in KONSTANTEN, n
            else:
                teile = name.split(".")
                assert teile[0] in RAHMENPLAN, n
                if len(teile) == 2:
                    assert teile[1] in RAHMENPLAN[teile[0]], n
    return erg


# ---------------------------------------------------------------------------
def teil_texte():
    return {
        "konstanten": dict(KONSTANTEN),
        "rahmenplan": json.loads(json.dumps(RAHMENPLAN)),
        "seiten": [dict(s) for s in SEITEN],
        "abschnitte": [dict(a, reiter=list(a["reiter"])) for a in ABSCHNITTE],
        "options_subtitle": OPTIONS_SUBTITLE,
        "release_page_text": RELEASE_PAGE_TEXT,
    }


def main(argv):
    if len(argv) != 2:
        print("Aufruf: python3 -I pruefe_hilfe.py <ausgabe.json>", file=sys.stderr)
        return 2
    assert len(KONSTANTEN) == 25 and len(SEITEN) == 5 and len(ABSCHNITTE) == 16
    assert KONSTANTEN["OPTIONS_SUBTITLE"] == OPTIONS_SUBTITLE
    ordner = tempfile.mkdtemp()
    atexit.register(shutil.rmtree, ordner, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    erg = {
        "texte": teil_texte(),
        "datum": teil_datum(),
        "suche": teil_suche(),
        "name": teil_name(),
        "einstellungen": teil_einstellungen(ordner),
        "rundgang": teil_rundgang(ordner),
        "oberflaeche": teil_oberflaeche(),
        "paare": [dict(p) for p in PAARE],
    }
    with open(argv[1], "w", encoding="utf-8", newline="\n") as f:
        json.dump(erg, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
