Die erste Version zum Herunterladen. Das Programm ist noch in der Alphaphase (A): Es wird gerade aufgebaut und ist zum eigenen Ausprobieren gedacht. Dein Lernstand bleibt bei jedem späteren Update erhalten.

**Installieren**
- Für jedes Gerät gibt es eine eigene Datei: Windows-Installation, Linux-Programm (AppImage, auch gepackt als `.tar.gz`), Android-App für 64-Bit-Handys und ein Paket für Macs mit Apple-Chip (nur gebaut, noch nicht auf einem Mac ausprobiert).
- Zu jeder Datei gibt es eine Prüfsumme (SHA-256). Damit kannst du nachprüfen, dass die Datei vollständig und unverändert angekommen ist.
- Die Android-App ist immer mit demselben Schlüssel unterschrieben. Darum lässt sich jede spätere Version einfach über die alte installieren, ohne dass der Lernstand verloren geht.
- Linux: Unter „Optionen“ → „Updates“ legt „Verknüpfung anlegen“ das Programm ins Anwendungsmenü und, wenn es einen Desktop-Ordner gibt, auf den Desktop. Beim Start des installierten AppImage fragt das Programm danach, bis du „Ja“ oder „Nicht mehr fragen“ wählst („Nein“ fragt beim nächsten Start wieder).
- Unter „Optionen“ → „Über das Programm“ zeigt „Hinweise zu Fremdbestandteilen“ alle mitgelieferten Bausteine anderer Hersteller mit ihren Lizenzen. Die vollen Lizenztexte liegen als `THIRD_PARTY_LICENSES.txt` am PC neben dem Programm und auf der Release-Seite zum Herunterladen.

**Updates**
- Unter „Optionen“ → „Updates“ sucht „Nach Updates suchen“ nach einer neueren Version; auf Wunsch geschieht das bei jedem Start von selbst („Beim Start automatisch nach Updates suchen“).
- Gibt es eine neuere Version, zeigt das Fenster „Update verfügbar“, was neu ist. Unter Windows und Linux lädt „Jetzt aktualisieren“ das Update, prüft Größe und Prüfsumme, legt vorher eine Kopie deines Lernstands im Ordner `sicherungen` an und startet das Programm neu. Am Handy öffnet „Herunterladen“ die neue App-Datei im Browser.
- Kam ein Update nicht an, sagt der nächste Start einmal „Update nicht abgeschlossen“, mit Grund und einem Tipp. Jeder Schritt steht in der Datei `update.log`.
- Gesucht wird nur nach veröffentlichten Versionen. Die Reihenfolge ist Alpha (A) vor Beta (B) vor 1.0.

**Lernstand sichern**
- Unter „Optionen“ → „Sicherung“ erstellst du mit „Sicherung erstellen“ eine Datei mit deinem ganzen Lernstand, zum Beispiel für einen USB-Stick. „Sicherung einspielen“ zeigt zuerst, was in der Datei steckt; dann wählst du, ob sie mit dem jetzigen Stand zusammengeführt wird oder ihn ersetzt („Alles ersetzen“ fragt zweimal nach).
- Vor jedem Einspielen, Update und Abgleich legt das Programm selbst eine Kopie deines Lernstands im Ordner `sicherungen` an. Diese Kopien löscht es nie von allein.
- „Löschen und zurücksetzen“ löscht auf Wunsch Lerndaten, Historie, Spielstand oder Bestenliste; am PC zeigt „Datenbank“, wo die Datei mit deinem Lernstand liegt.
- Update, Sicherung einspielen und der Abgleich zwischen PC und Handy laufen nie gleichzeitig, damit sie sich nicht gegenseitig stören.

**Wenn etwas nicht klappt**
- Unter „Optionen“ → „Diagnose und Werkzeuge“ erstellt „Problem melden“ einen Bericht zum Kopieren oder Speichern. Zugangsschlüssel, Passwörter und dein Benutzername in Pfaden werden daraus entfernt; andere Angaben, zum Beispiel der Name deines Rechners, können darin stehen. Sieh dir den Bericht vor dem Weitergeben an.
- Dort gibt es auch eine Leistungsmessung (schreibt Messwerte in eine Datei) und am PC die Hänger-Diagnose für den Fall, dass das Programm nicht mehr reagiert.
- Am PC startet das Programm nur einmal: Ein zweiter Start holt das schon offene Fenster nach vorn, statt ein zweites Programm zu öffnen.

### Herunterladen

**Fachinformatiker Lernplattform A 0.12.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.12.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.12.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.12.0-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
