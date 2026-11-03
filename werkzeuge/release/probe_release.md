Für Macs mit Intel-Prozessor gibt es jetzt ein eigenes Installationspaket.

- Neben dem Paket für Macs mit Apple-Chip (M1 bis M4) liegt auf der Release-Seite jetzt auch ein Paket für Macs mit Intel-Prozessor (macOS 14 oder neuer). Der Dateiname endet auf „osx-x64.pkg“, das Paket für Apple-Chips weiter auf „osx-arm64.pkg“.
- Beide Mac-Pakete sind nicht signiert: Beim ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ auf „Dennoch öffnen“ klicken. Das Update im Programm gibt es auf macOS noch nicht.
- Im Dashboard wird der Name eines Prüfungsthemas (zum Beispiel „Netzwerkdesign“) bei großer Schrift am PC nicht mehr mitten im Wort getrennt. Passt das Wort nicht in seine Spalte, wird nur dieser Name etwas kleiner geschrieben.
- Die Liste der Fremdbestandteile („Lizenzen“ in den Optionen) nennt jetzt auch die Teile des Intel-Pakets.

### Herunterladen

**Fachinformatiker Lernplattform A 0.32.11**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.32.11-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.32.11-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.32.11-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.32.11-osx-x64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
