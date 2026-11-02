Tabellen in Aufgaben stehen jetzt sauber untereinander, der Kalender zählt den Rechen-Trainer mit, und der Abgleich nennt deinen Rechner nicht mehr beim Namen.

- Tabellen in Aufgaben und Musterlösungen (zum Beispiel Nutzwertanalysen oder Regelwerke) werden jetzt in einer Schrift mit fester Zeichenbreite gezeigt. Dadurch stehen die Spalten genau untereinander. Der Text selbst ist unverändert.
- Der Kalender „Lerntage“ auf dem Dashboard markiert jetzt auch Tage, an denen du nur im Rechen-Trainer geübt hast. Die Lernserie hat diese Tage schon bisher gezählt.
- Beim Abgleich zwischen PC und Handy und in einer Sicherungsdatei steht als Gerät jetzt nur noch „PC“ oder „Handy“. Der Name deines Rechners, der oft ein Personenname ist, wird nicht mehr verwendet.
- Linux: Die Verknüpfung im Anwendungsmenü startet jetzt auch dann, wenn das Programm in einem Ordner liegt, dessen Name ein „%“ oder einen umgekehrten Schrägstrich enthält.
- Eine Erklärung im Prüfungstrainer (Fachrichtung Digitale Vernetzung, geschirmte Kabel) ist genauer formuliert: Der Schirm wird im Schrank an der Potenzialausgleichsschiene aufgelegt.

### Herunterladen

**Fachinformatiker Lernplattform A 0.32.5**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.32.5-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.32.5-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.32.5-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
