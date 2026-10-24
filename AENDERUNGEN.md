# Änderungen

## A 0.12.0
Die erste Version zum Herunterladen. Das Programm ist noch in der Alphaphase (A): Es wird gerade aufgebaut und ist zum eigenen Ausprobieren gedacht. Dein Lernstand bleibt bei jedem späteren Update erhalten.

**Installieren**
- Für jedes Gerät gibt es eine eigene Datei: Windows-Installation, Linux-Programm (AppImage, auch gepackt als `.tar.gz`), Android-App für 64-Bit-Handys und ein Paket für Macs mit Apple-Chip (nur gebaut, noch nicht auf einem Mac ausprobiert). Welche Datei für welches Gerät ist, steht auf der Release-Seite.
- Zu jeder Datei nennt die Release-Seite eine Prüfsumme (SHA-256). Damit kannst du nachprüfen, dass die Datei vollständig und unverändert angekommen ist.
- Die Android-App ist immer mit demselben Schlüssel unterschrieben. Darum lässt sich jede spätere Version einfach über die alte installieren, ohne dass der Lernstand verloren geht.
- Linux: Unter „Optionen“ → „Updates“ legt „Verknüpfung anlegen“ das Programm ins Anwendungsmenü und, wenn es einen Desktop-Ordner gibt, auf den Desktop. Beim ersten Start fragt das Programm einmal danach („Ja“, „Nein“ fragt beim nächsten Start wieder, „Nicht mehr fragen“).
- „Über“ → Fremdbestandteile listet alle mitgelieferten Bausteine anderer Hersteller mit ihren Lizenzen. Die vollen Lizenztexte liegen als `THIRD_PARTY_LICENSES.txt` am PC neben dem Programm und auf der Release-Seite zum Herunterladen.

**Updates**
- Unter „Optionen“ → „Updates“ sucht „Nach Updates suchen“ nach einer neueren Version; auf Wunsch geschieht das bei jedem Start von selbst („Beim Start automatisch nach Updates suchen“).
- Gibt es eine neuere Version, zeigt das Fenster „Update verfügbar“, was neu ist. Am PC lädt „Jetzt aktualisieren“ das Update, prüft Größe und Prüfsumme, legt vorher eine Kopie deines Lernstands im Ordner `sicherungen` an und startet das Programm neu. Am Handy öffnet „Herunterladen“ die neue App-Datei im Browser.
- Kam ein Update nicht an, sagt der nächste Start einmal „Update nicht abgeschlossen“, mit Grund und einem Tipp. Jeder Schritt steht in der Datei `update.log`.
- Gesucht wird nur nach veröffentlichten Versionen. Die Reihenfolge ist Alpha (A) vor Beta (B) vor 1.0.

**Lernstand sichern**
- Unter „Optionen“ → „Sicherung“ erstellst du mit „Sicherung erstellen“ eine Datei mit deinem ganzen Lernstand, zum Beispiel für einen USB-Stick. „Sicherung einspielen“ zeigt zuerst, was in der Datei steckt; dann wählst du, ob sie mit dem jetzigen Stand zusammengeführt wird oder ihn ersetzt („Alles ersetzen“ fragt zweimal nach).
- Vor jedem Ersetzen legt das Programm selbst eine Kopie im Ordner `sicherungen` an. Diese Kopien löscht es nie von allein.
- „Löschen und zurücksetzen“ löscht auf Wunsch Lerndaten, Historie, Spielstand oder Bestenliste; am PC zeigt „Datenbank“, wo die Datei mit deinem Lernstand liegt.
- Update, Sicherung einspielen und der Abgleich zwischen PC und Handy laufen nie gleichzeitig, damit sie sich nicht gegenseitig stören.

**Wenn etwas nicht klappt**
- Unter „Optionen“ → „Diagnose und Werkzeuge“ erstellt „Problem melden“ einen Bericht zum Kopieren oder Speichern. Er enthält keine persönlichen Ordnernamen und keine Zugangsdaten.
- Dort gibt es auch eine Leistungsmessung (schreibt Messwerte in eine Datei) und am PC die Hänger-Diagnose für den Fall, dass das Programm nicht mehr reagiert.
- Am PC startet das Programm nur einmal: Ein zweiter Start holt das schon offene Fenster nach vorn, statt ein zweites Programm zu öffnen.

## Phase 6, Welle 2: Plattformen (ohne neue Version, Weisung P6-6/P6-7)
Die Version bleibt A 0.11.0 (die neue Version kommt mit dem ersten Release in Welle 3).
- Linux (installiertes AppImage): Optionen „Updates“ mit „Verknüpfung anlegen“. Die Verknüpfung kommt ins Anwendungsmenü und, wenn es einen Desktop-Ordner gibt, auf den Desktop; ein erneuter Klick erneuert sie (zum Beispiel nachdem das AppImage verschoben wurde). Beim ersten Start fragt das Programm einmal danach („Ja“, „Nein“ fragt beim nächsten Start wieder, „Nicht mehr fragen“). Der Hilfe-Artikel „Update“ erklärt den Knopf.
- Windows: die Update-Probe deinstalliert zum Schluss und prüft, dass Datenordner und Lernstand dabei unverändert bleiben (T4); sie zählt, wie oft die Velopack-Sperre auftritt (T5, Sammelliste 46).
- Start ohne Bedienung zeigt die neuen Ansichten in allen Schriftstufen, Größen und 72 Farbwelten.

## Phase 6, Welle 1: Release-Ablauf (ohne neue Version, Weisung P6-2)
Keine neue Programmfunktion, die Version bleibt A 0.11.0 (Plan Phase 6, D5).
- Release-Bau als Skript (`werkzeuge/release/baue_release.sh`), im vollen CI-Lauf ohne Hochladen geprüft; `release.yml` startet nur von Hand, nur von `main` und nur mit vollem Lauf 8/8 und legt höchstens einen Entwurf an.
- Tag-Regel `vA…`/`vB…`/`v1…` mit Test (Sammelliste 73), Release-Text mit SHA-256-Tabelle (Sammelliste 56), Android-Release-Schlüssel nur über Geheimnisse mit Fingerabdruck-Prüfung.
- „Über“ → Fremdbestandteile zeigt jetzt die beim Bau erzeugte Liste: jede ausgelieferte Datei gehört zu einem genannten Bestandteil; die vollen Lizenztexte liegen als `THIRD_PARTY_LICENSES.txt` am PC neben dem Programm und im Release, in der Android-App noch nicht (Sammelliste 12).

## A 0.11.0 (in Arbeit, Phase 5: Betrieb)
Version A 0.11.0, Paket 1.0.0-a.0.11.0, Android 1001100 (Weisung P5-8). Ein Update von A 0.10.0 behält den Lernstand.
- Welle 1, Sicherung: Optionen „Sicherung“ mit „Sicherung erstellen“ und „Sicherung einspielen“ (Vorschau, Zusammenführen oder „Alles ersetzen“ mit zwei Rückfragen), „Löschen und zurücksetzen“ und am PC „Datenbank“. Vor jedem Ersetzen entsteht eine Kopie unter `sicherungen/`.
- Welle 2, Diagnose: Optionen „Diagnose und Werkzeuge“ mit „Problem melden“ (Bericht ohne Benutzerpfade und ohne Zugangsdaten, kopieren oder speichern), Leistungsmessung mit Messdatei, am PC die Hänger-Diagnose und eine Sperre gegen einen zweiten gleichzeitigen Programmstart.
- Welle 3, Update im Programm: Optionen „Updates“ mit „Nach Updates suchen“ und dem Schalter „Beim Start automatisch nach Updates suchen“. Gibt es eine neuere Version, zeigt das Fenster „Update verfügbar“ die Neuerungen. Am PC lädt „Jetzt aktualisieren“ das Update (Größe und SHA-256-Prüfsumme werden geprüft), legt vorher eine Kopie des Lernstands unter `sicherungen/vor_update_*.db` an und startet das Programm danach neu; läuft das Programm nicht als installierte Anwendung, führt „Zur Download-Seite“ zum Release. Am Handy öffnet „Herunterladen“ die neue App-Datei im Browser.
- Kam ein Update nicht an, meldet der nächste Start einmal „Update nicht abgeschlossen“ mit Grund, Tipp und dem Pfad von `update.log`; jeder Schritt des Updates steht in `update.log` (auch im Problembericht).
- Update, Sicherung einspielen, Übernahme und Abgleich laufen nie gleichzeitig. Gesucht wird nur nach veröffentlichten Versionen (keine Vorab-Versionen); die Reihenfolge ist A vor B vor 1.0.
- Der Hilfetext der Hänger-Diagnose nennt „Vorladen“ nicht mehr (gibt es in dieser Linie nicht, Sammelliste 71).
- Start ohne Bedienung zeigt die neuen Ansichten in allen Schriftstufen, Größen und 72 Farbwelten.

## A 0.10.0 (in Arbeit, Phase 4: Prüfungsbereiche)
Version A 0.10.0, Paket 1.0.0-a.0.10.0, Android 1001000 (Weisung P4-16). Ein Update von A 0.9.0 behält den Lernstand.
- Neue Lernseiten „AP1-Szenarien“, „AP2-Szenarien“ und „Testprojekt“ (bisher „Kommt in einer späteren Ausbaustufe“): Liste mit Status-Punkt, Suche, Themen- und Statusfilter, Blättern, Aufgabe mit eigenem Lösungsfeld, Musterlösung aufdecken und „Gewusst“/„Nicht gewusst“. Jede Bewertung wird genau einmal gespeichert, wie in Python 0.63.2.
- Neue Seite „Notizblock“: Lernstand je Bereich, Filter, Suche und Einträge mit Musterantwort zum Aufklappen.
- Klausursimulation im Prüfungstrainer: Prüfung wählen, laufende Prüfung mit Countdown, WiSo-Teil, Abbrechen und Abgeben mit Rückfrage, Bewertung mit Kriterien und Punktvorschlag, Ergebnis mit schwachen Themen und „Gesamtergebnis“. Eine laufende Prüfung bleibt nach dem Beenden erhalten; der Abschluss wird in einem Schritt gespeichert (alles oder nichts).
- Neue Seite „Abschlussprojekt“ mit sechs Reitern: Projekt anlegen, wählen und löschen, Vorlage aus dem Testprojekt, automatisches Speichern nach dem Tippen, Export als Text und als PDF.
- Neue Seite „Hilfe“ (16 Themen und 5 Seiten, auch in der Suche) und ein Rundgang beim ersten Start mit „Jetzt einrichten“ (Name, Rahmenplan, Prüfungstermine). Die Ausbildungsfrage erscheint beim Start, solange keine Ausbildung gewählt ist; ein Wechsel der Ausbildung zeigt einen Hinweis, auch auf anderen Geräten nach dem Abgleich.
- Optionen: neue Bereiche „Rundgang und Hilfe“ und „Lerninhalte > Rahmenplan“.
- Start ohne Bedienung zeigt alle neuen Ansichten in allen Schriftstufen, Größen und 72 Farbwelten. Ein Messwert über der Grenze wird bis zu 3 Mal gemessen; ist der Start-Test rot, folgt ein 2. Lauf (rot nur, wenn derselbe Punkt in beiden Läufen rot ist).

## A 0.9.0 (in Arbeit, Phase 3: Rechner, Welle 4)
Version A 0.9.0, Paket 1.0.0-a.0.9.0, Android 1000900 (Weisung P3-24):
- Neue Rechner auf der Seite „Rechner“, je mit „Rechenweg anzeigen“ und eigener Aufgabenart im Trainer, gerechnet wie in Python 0.63.2 (Vergleichswerte G28 bis G31):
  - „Datenmengen“: Datenmengen umrechnen (Trainer „Datenmengen“).
  - „Übertragung“: Übertragungsdauer aus Datenmenge und Geschwindigkeit (Trainer „Übertragungsdauer“).
  - „Bild und Video“: Speicherbedarf von Bildern und Videos, Ergebnis lässt sich in „Übertragung“ übernehmen (Trainer „Bild und Video“).
  - „WiSo“: sechs Rechner (Bezug, Verkauf, Deckungsbeitrag, Abschreibung, Zinsen, Amortisation), Geldbeträge genau auf den Cent (Trainer-Bereich „WiSo“).
- In allen Prozentfeldern des WiSo-Rechners sind höchstens 2 Nachkommastellen erlaubt; sonst erscheint der rote Hinweis „Bitte höchstens 2 Nachkommastellen eingeben: …“ (Weisung P3-24). Der Trainer bleibt unverändert.
- Start ohne Bedienung zeigt die neuen Rechner und Trainer-Arten in allen Schriftstufen, Größen und 72 Farbwelten und misst Berechnen, Rechenweg und Übernehmen.

## A 0.8.0 (in Arbeit, Phase 3: Rechner, Welle 3)
Version A 0.8.0, Paket 1.0.0-a.0.8.0, Android 1000800 (Weisung P3-12):
- Der Reiter „Trainer“ der Rechner-Seite (bisher „Kommt in einer späteren Ausbaustufe“) ist gebaut: der Rechen-Trainer wie in Python 0.63.2 mit sechs Aufgabenarten (IPv4-Subnetz, VLSM, Binär/Hex/Dezimal, IPv6-Schreibweise, Netz aufteilen, Nach Hostbedarf), drei Schwierigkeiten und Runden zu 10 Aufgaben. Aufgaben, Lösungen, Rechenwege und die Prüfung der Antworten sind dieselben wie in Python (Vergleichswerte G27).
- Jede geprüfte Aufgabe wird genau einmal gespeichert, auch mit leeren Feldern (dann als falsch). „Nächste Aufgabe“, „Auswertung“ und „Runde starten“ speichern nichts. Unter „Runde starten“ steht „bisher … Aufgaben, … richtig“ über alle Arten; die Zeile wird beim ersten Öffnen des Reiters und am Ende einer Runde neu gelesen. Gelöste Aufgaben zählen wie in Python im Dashboard und im Fortschritt mit.
- Nach jedem Prüfen wird auf PC und Handy der Abgleich angestoßen (Python stößt ihn nur am Handy an).
- Bewusste Abweichung von Python: Eine Eingabe mit mehr als 4 300 Ziffern gilt als falsch und wird als falsch gespeichert (Python bricht dort mit einem Fehler ab und speichert nichts).
- Ohne laufende Runde ist der Knopf „Prüfen“ auf PC und Handy gesperrt (Python blendet ihn am Handy aus). Am schmalen Aufbau (Handy) erscheint der Rechenweg in der schmalen Fassung; wird das Fenster mitten in einer Runde schmaler oder breiter, bleibt die Runde und die nächste Prüfung zeigt den passenden Rechenweg.
- Passt in einem Ergebniskasten eine Zeile auch nach dem Umbruch nicht, steht darunter „→ wischen“ (wie am Handy in Python, jetzt auf PC und Handy).
- Start ohne Bedienung zeigt den Trainer in neun weiteren Ansichten (ohne zu speichern) in allen Schriftstufen, Größen und 72 Farbwelten und misst Reiter, „Runde starten“, „Prüfen“ und „Nächste Aufgabe“.

## A 0.7.0 (in Arbeit, Phase 3: Rechner, Welle 2)
Version A 0.7.0, Paket 1.0.0-a.0.7.0, Android 1000700 (Weisung P3-4):
- Welle 1 (ohne Oberfläche, ohne Versionssprung): Rechenkern wie in Python 0.63.2 für Subnetting IPv4 und IPv6 (Netzdaten, Netz aufteilen mit Seiten, Hostbedarf), RAID, Bildschirm-Datenmenge und USV-Kapazität mit Übungsaufgaben und Rechenwegen (Vergleichswerte G24 bis G26).
- Die Seite „Rechner“ (bisher Platzhalter) ist gebaut: Subnetting mit IPv4/IPv6 und den Ansichten Netzdaten, Netz aufteilen (Blättern, „Zur Seite“) und Hostbedarf, RAID-Kapazität, Bildschirm-Datenmenge und USV-Kapazität mit Bild (Last, USV mit Auslastung, Akku, Laufzeitbalken, Empfehlung). Jede Karte hat „Rechenweg anzeigen“. Der Reiter „Trainer“ zeigt „Kommt in einer späteren Ausbaustufe“ bis Welle 3.
- Ergebnisse stehen in der Schrift JetBrains Mono 2.304 (im Programm enthalten, auf allen Systemen gleich, Lizenz SIL OFL 1.1 unter „Hinweise zu Fremdbestandteilen“). Zeilen werden wie in Python an die Breite des Kastens angepasst; am Handy gelten die schmalen Fassungen der Berichte und Rechenwege.
- Eine falsche Eingabe zeigt auf PC und Handy einen roten Hinweis mit dem Text der Meldung unter dem Knopf; der Ergebniskasten wird geleert. Es öffnet sich kein Fenster (Python zeigt am PC ein Fenster „Ungültige Eingabe“).
- Die Texte der Rechner-Seite stehen als eine gemeinsame Liste für PC und Handy im Programm; ein Test vergleicht sie mit Python.
- Start ohne Bedienung misst den Wechsel IPv4/IPv6, das Aufteilen in 2^32 und 2^64 Subnetze (erste und letzte Seite), den Hostbedarf mit 12 Zeilen und den Rechenweg, prüft die Schrift (i, W und 0 gleich breit) und die Rechner-Ansichten in allen Schriftstufen, Größen und 72 Farbwelten.

## A 0.6.0 (in Arbeit, Phase 2: Lernkern, Welle 5)
Version A 0.6.0, Paket 1.0.0-a.0.6.0, Android 1000600 (Weisung P2-14):
- PC und Handy zeigen dieselben Wörter (Sammelliste 13, Wortwahl Nico 21.10.): Die Aktivitäten im Dashboard nennen das Datum auf beiden Geräten deutsch („21.10.2026 05:57“) und zeigen den Text ungekürzt; der Untertitel des Abgleichs lautet überall „über ein privates GitHub-Repository“. Unterschiede gibt es nur noch, wo eine Funktion nur auf einem Gerät existiert oder der Aufbau nach Breite wechselt (feste Liste, ein Test wacht darüber).
- Optionen > Eigene Farben: Die Regler zeigen wieder eine farbige Spur wie in Python 0.63.2. Die Spur zeigt, welche Farbe beim Schieben entsteht; der Knopf ist hell oder dunkel, je nachdem, was sich an seiner Stelle besser abhebt (Vergleichswerte G23).
- Neu gegenüber A 0.5.0 (wie Python): Mit der Tastatur läuft der Farbton im Kreis (von 359° weiter auf 0°, von 0° zurück auf 359°). Gespeichert werden weiter nur Werte von 0 bis 359.
- Start ohne Bedienung misst das Neuzeichnen beim Ziehen eines Farbreglers (Grenze 150 ms für den ersten Schritt, 100 ms ab dem zweiten).

## A 0.5.0 (in Arbeit, Phase 2: Lernkern, Welle 4)
Version A 0.5.0, Paket 1.0.0-a.0.5.0, Android 1000500 (Weisung P2-11):
- Optionen > Sicherung: „Sicherung für die alte App erstellen“. Die Datei (`.fisisicherung`) liest die alte Python-Version 0.63.2 über „Sicherung zusammenführen“ ein; der Lernstand hier wird nur gelesen, in einem Zug, sodass alle Teile zum selben Stand gehören. Gespeichert wird über eine Hilfsdatei und eine Probe; am Handy erst im App-Ordner, dann an den gewählten Ort. Wortlaut nach Weisung P2-11 (Vergleichswerte G21, Beleg mit Python in der CI).
- Kern des Abgleichs PC ↔ Handy über ein privates GitHub-Repository wie in Python (Zusammenführen, Löschmarken, Uhrabweichung, bis zu drei Versuche bei gleichzeitigem Schreiben). Vor jeder Änderung an vorhandenen Einträgen und vor dem ersten Abgleich entsteht eine Kopie `sicherungen/vor_abgleich_<Datum_Uhrzeit>.db`; gelingt sie nicht, wird nichts verändert. Der Zugangsschlüssel erscheint nie in Fehlerdatei, Sicherung oder Meldung.
- Optionen > „Abgleich PC und Handy“ (Weisung P2-12): Repository und Zugangsschlüssel eintragen, „Speichern und abgleichen“ mit Ergebnis oder Fehler in einer Zeile darunter, Schalter „Automatisch abgleichen (beim Start, nach dem Lernen und beim Verlassen der App)“. Automatisch wird am PC 1,5 s nach dem Start, 60 s nach dem Lernen und beim Beenden abgeglichen, am Handy beim Start, 30 s nach dem Lernen und beim Verlassen der App. Texte wie in Python 0.63.2; ein unerwarteter Fehler zeigt nur „Unerwarteter Fehler beim Abgleich.“
- Während des Hochladens ist der Lernstand nicht gesperrt: Antworten, die man während eines Abgleichs gibt, werden sofort gespeichert.
- Läuft gerade ein Abgleich oder eine Übernahme, lehnen „Lernstand übernehmen“ und „Sicherung für die alte App erstellen“ mit einem Hinweis ab, statt zu warten.
- Übernahme, Abgleich und Sicherung für die alte App laufen nie gleichzeitig.

## A 0.4.0 (in Arbeit, Phase 2: Lernkern, Welle 3)
Version A 0.4.0, Paket 1.0.0-a.0.4.0, Android 1000400 (Weisung P2-7):
- Fortschritt (nur lesend) wie in Python: Kacheln „Test-Sessions“, „Durchschnitt“, „Bestes Ergebnis“; „Aufgaben pro Tag“ mit Tagesziel als Linie und „Richtig und falsch pro Tag“ für 7 oder 30 Tage; „Ergebnisse im Prüfungstrainer“; „Historie der Prüfungssessions“ und „Prüfungen (Klausursimulation)“ (aus Python übernommene Klausuren nur zur Anzeige); schwache Themen der letzten Prüfung mit „Jetzt üben“. Gerundet wird überall wie Python (Vergleichswerte G18).
- Rahmenplan-Abdeckung mit Ansicht „Rahmenplan“ (Filter Alle, AP1, Konzeption, Netzwerke, Projekt, WiSo) und „Lernfeld“, Zeilen zum Aufklappen und „Jetzt üben“ je Punkt bzw. Lernthema. „Jetzt üben“ übt erst die Prüfungsfragen in fester Reihenfolge und fragt danach „Weiter üben“ für die Karteikarten zum selben Thema.
- Suche wie in Python: Optionen, Karteikarten, Prüfungsfragen, AP2- und AP1-Szenarien in dieser Reihenfolge, höchstens 60 Zeilen, Groß-/Kleinschreibung wie Python (Vergleichswerte G19). Der Suchtext wird einmal je Programmstart im Hintergrund vorbereitet; eine neue Suche bricht die alte ab. Ein Treffer in den Optionen klappt den passenden Bereich auf, ein Treffer bei Karteikarten oder Prüfungsfragen springt dorthin. Strg+F öffnet die Suche.
- Bewusste Abweichung von Python (Weisung P2-8, Sammelliste 22): Erfolgsquote und jede Anzeige „% richtig“ werden bei 100 % gekappt, und „X von Y richtig“ zeigt nie mehr richtige als gestellte Antworten. Python zeigt bei kaputten oder fremden Daten Werte über 100 %; echte Daten sind davon nicht betroffen.
- Am Handy wird in der Rahmenplan-Abdeckung ein sehr langes Wort bei großer Schrift etwas kleiner gesetzt, damit es nicht mitten im Wort umbricht.
- Start ohne Bedienung prüft die neuen Seiten in allen Schriftstufen, Fenstergrößen und 72 Farbwelten und misst die Suche „Subnetz“ und die Fälligkeit bei 100 000 Antworten; das Lesen der Antworten für die Fälligkeit ist dafür schneller geworden (gleiches Ergebnis).

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
