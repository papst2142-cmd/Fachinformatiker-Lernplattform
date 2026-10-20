# Änderungen

## A 0.3.0 (in Arbeit, Phase 2: Lernkern)
Welle 1 (Kern ohne Oberfläche, ohne Versionssprung, Weisung P2-2):
- Lernlogik wie in Python: Lernstatus je Karte und Frage, Wiederholungsplan, bevorzugte Reihenfolge nach Rahmenplan und Prüfungstrainer (Vergleichswerte G15, G16, G17).
- Antworten werden wie in Python gespeichert, mit Uhrzeit und Zeitzone; ein Schreibfehler ändert den Lernstand nicht.
- Übernahme eines alten Lernstands (Sicherung `.fisisicherung`, Abgleich-Datei `lernstand.json.gz`, alte Datenbank der Python-Version, nur lesend): zusammenführen wie Python, vorher automatische Kopie im Ordner `sicherungen`, beliebig oft wiederholbar (Vergleichswerte G20).

Welle 2 (Oberfläche, Version A 0.3.0, Paket 1.0.0-a.0.3.0, Android 1000300):
- Karteikarten: Fachbereich, Thema, Status und drei Modi (Freitext, Multiple Choice, Aufdecken) wie in Python; Texte zeichengleich (Vergleichswerte G22), lange Themennamen brechen auch am Handy um.
- Prüfungstrainer, Art „Übung“: Fragenanzahl, Themen, Zeit, Auswertung mit Hinweis unter dem Ergebnis; Reihenfolge der Antworten wie in Python. Die Art „Prüfung“ kommt in einer späteren Welle.
- Optionen > Sicherung: „Lernstand übernehmen“ mit Vorschau (rechnet nur auf einer Kopie im Arbeitsspeicher und ändert nichts), Hinweis auf die automatische Kopie im Ordner `sicherungen` und Ergebnis; Wortlaut nach Weisung P2-4.
- Dashboard: „Jetzt wiederholen“ auf PC und Handy wie in Python. Am PC steht der Knopf immer da und ist ohne fällige Aufgaben gesperrt, am Handy erscheint er nur mit fälligen Aufgaben. Ein Klick übt zuerst die fälligen Karteikarten, dann die fälligen Prüfungstrainer-Fragen; ist nichts fällig, erscheint „Heute ist nichts mehr fällig.“
- Der Dateiname der Kopie im Ergebnis bricht nur am Handy nach „_“ um; am PC bleibt er unverändert und kopierbar.
- Die Rahmenplan-Zuordnung wird nach dem Start im Hintergrund vorbereitet, damit die erste Karte schnell erscheint.
- Start ohne Bedienung prüft zusätzlich die neuen Ansichten (Karteikarten in drei Modi, Prüfungstrainer mit Frage und Ergebnis, Übernahme mit Vorschau, Ergebnis und Fehler, Rückfrage beim Abbrechen) in allen Schriftstufen und Fenstergrößen sowie Layout und Kontrast in allen 72 Farbwelten.

## A 0.2.0 (in Arbeit, Phase 1: Kern und Oberfläche)
Welle 1 (Kern ohne Oberfläche; die Versionsnummer wird erst mit Welle 2 erhöht, Weisung P1-1):
- Lerninhalte als Datenmodell: Karteikarten, Quizfragen (mit den 54 automatisch erzeugten Aufgaben), AP2-/AP1-Szenarien, Testprojekte, Themen und Rahmenplan werden wie in Python geladen und geprüft; jeder Inhalt kennt seine Fachrichtung (ohne Angabe: Systemintegration). Geprüft gegen Vergleichswerte G12, ohne den Spielteil.
- Lernstand-Datenbank `lernplattform.db` mit denselben Tabellen und Spalten wie Python; Aufbau-Version als `PRAGMA user_version = 1`. Beschädigte, schreibgeschützte oder nicht erreichbare Datenbank: nichts wird geändert, eine klare Meldung erscheint. Geprüft gegen Vergleichswerte G11.
- Einstellungen `einstellungen.json` wie in Python: unbekannte Einträge bleiben erhalten, Speichern atomar, unter Linux/macOS nur für das eigene Konto lesbar (0600), beschädigte Datei wird als `einstellungen.defekt.json` aufgehoben und einmal gemeldet.
- Fehlerprotokoll `fehler.log` im Datenordner (höchstens 256 KB) ohne Zugangsschlüssel, Passwörter und Benutzernamen in Pfaden; PC und Handy schreiben unbehandelte Fehler hinein. Geprüft gegen Vergleichswerte G10.

Welle 2 (Oberfläche, Version A 0.2.0, Paket 1.0.0-a.0.2.0, Android 1000200):
- Einstellungen: Fehlt die Datei, gelten die Grundwerte und Speichern legt sie an. Lässt sich eine vorhandene Datei nicht lesen (gesperrt, keine Leserechte), gelten die Grundwerte nur zur Anzeige; es wird nichts geschrieben und „Speichern fehlgeschlagen“ erscheint (Auflage 3.1, bewusste Abweichung von Python).
- Design: 72 Farbwelten, Hell/Dunkel und eigene Farben (Regler, Vorschau, Kontrastwarnung, geprüft gegen Vergleichswerte G6b); fünf Schriftstufen wirken sofort ohne Neuaufbau, Überschriften wachsen nur bis zu einer Obergrenze.
- Navigation: ein Aufbau für PC und Handy. Breites Fenster mit Seitenleiste, schmales Fenster mit unterer Leiste (6 Einträge) und Sammelseite „Lernen“; die Grenze wächst mit der Schriftstufe (720 Punkte × Schriftfaktor), damit auch bei großer Schrift kein Wort mitten im Wort umbricht; Suche und Hilfe oben. Noch nicht gebaute Bereiche zeigen „Kommt in einer späteren Ausbaustufe“ (mit Umlaut). Alle Beschriftungen wie in Python (Vergleichswerte G9).
- Dashboard mit allen Karten; die Werte werden nur lesend aus `lernplattform.db` berechnet, im Hintergrund (Vergleichswerte G13), und bei jedem Öffnen neu, damit Änderungen am Tagesziel und ein neuer Tag sofort sichtbar sind. Fehlt die Datenbank, wird sie nicht angelegt. Der Untertitel „Auswahl zeigt die Themen“ fehlt, bis die Themen-Auswahl gebaut ist. Ist die Datenbank vorhanden, aber nicht lesbar, steht oben „Der Lernstand konnte nicht vollständig gelesen werden. Er wurde nicht verändert.“; ein Programmfehler dabei kommt mit seiner Art (ohne Lerndaten) ins Fehlerprotokoll
- Eigene Farben: der Satz zu den Vorlagen-Kacheln („setzt nur die Regler …“) steht wie in Python nur da, wenn eigene Farben gespeichert sind.
- Optionen: Optik, Tagesziel, Diagnose (Fehlerprotokoll ansehen und kopieren), Über das Programm (mit Lizenz und Fremdbibliotheken); übrige Bereiche mit Platzhalter.
- Optionen: unter der Schriftgröße steht „Gilt sofort und nur für dieses Gerät.“, unter dem Tagesziel „Gezählt werden bewertete Karteikarten, Prüfungstrainer-Fragen, Szenarien, Testprojekte und Trainer-Aufgaben.“ (Weisung P1-3: nur was C# heute schon hält).
- „Alte Sicherung prüfen“ und „Ergebnis kopieren“ stehen jetzt unter Optionen > Sicherung; die Prüfung selbst ist unverändert.
- Rechnen wie Python auf allen Systemen: Potenzen (`x ** y`, z. B. für Farbabstand und Kontrast der eigenen Farben und die Wissensanzeige im Dashboard) werden mit einem eigenen Nachbau der Linux-Funktion gerechnet, damit Windows, macOS, Linux und Android jedes Bit gleich wie die Python-Version treffen (Vergleichswerte G14).
- Lerninhalte sind ins Programm eingebettet und werden im Hintergrund geladen; die erste Ansicht wartet nicht darauf.
- Start ohne Bedienung (CI): öffnet jede Seite, wechselt Hell/Dunkel und Schriftstufen und prüft bei allen fünf Schriftstufen in drei festen Fenstergrößen (Handy 384 × 832, Grenze zum breiten Aufbau, PC 1360 × 880), dass kein Wort mitten im Wort umbricht und kein Knopf aus seiner Karte ragt; rot ab 1000 ms.

## A 0.1.0 (in Arbeit, Phase 0: Fundament)
- Neues Projekt in C# (.NET 10, Avalonia 12.1) mit fünf Teilen: Core, App, Desktop, Android, Tests.
- Zufall exakt wie in der Python-Version (Mersenne Twister mit Text-Startwerten und Spiel-Würfel), geprüft gegen Vergleichswerte.
- Lerninhalte, alte Lernstand-Datenbank und alte Sicherungsdatei werden gelesen, geprüft gegen Vergleichswerte aus der Python-Version.
- Die 72 Farbwelten (6 Grundfarben x 6 Hintergründe x Hell/Dunkel) werden wie in Python berechnet, geprüft gegen Vergleichswerte.
- Messseite für Spike A (nur Phase 0): misst Start, erstes Öffnen einer Seite, Wechsel Hell/Dunkel, alle 72 Farbwelten, Seitenwechsel und Arbeitsspeicher; Ergebnis per Knopf kopierbar.
- Messseite: Knopf „Alte Sicherung prüfen“ liest eine Kopie der Sicherung der alten App nur im Speicher und zeigt nur Zählwerte je Tabelle, Zusammenfassung, „Prüfsumme stimmt ja/nein“ und SHA-256; keine Namen, Pfade, Inhalte oder Fehlertexte (Auflagen Weisung CC P0-3).
- Updates am PC mit Velopack (Windows-Setup, Linux-AppImage, macOS). Die Paketversion ordnet A < B < 1.0 (A 0.1.0 = 1.0.0-a.0.1.0). Ein Rückschritt wird abgelehnt, der Datenordner bleibt bei jedem Update erhalten.
- Android-Versionscode und Anzeige werden aus Stufe und Version berechnet (A 0.1.0 = 1000100); die CI baut zusätzlich A 0.1.1 mit demselben Testschlüssel für die Update-Probe auf dem Handy.
- Screenreader-Probe (nur Phase 0): Seite mit Knopf, Textfeld, Liste, Kontrollkästchen und Reitern, alle mit Namen für NVDA, Orca und TalkBack.
- Linux: Startschalter `--software` (Zeichnen ohne Grafikkarte) und `--ohne-eingabemethode` als Ausweich, beschrieben im README und auf der Messseite; Standard bleibt das normale Zeichnen. AppImage zusätzlich als .tar.gz, damit das Ausführungsrecht erhalten bleibt.
- Messseite: Speicher-Dauertest (20 Durchgänge der ganzen Messung, Speicher nach jedem Durchgang).
- Nach der Abnahme von Phase 0: Messseite, Screenreader-Probe, Dauertest und Knopf „Update aus Ordner“ entfernt. „Alte Sicherung prüfen“ und „Ergebnis kopieren“ stehen jetzt auf der Startseite, die Prüfung selbst ist unverändert. Die CI startet das Programm weiter einmal ohne Bedienung und prüft das Update ohne Oberfläche.
