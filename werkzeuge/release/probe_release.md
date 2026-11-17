Inhaltspakete lassen sich jetzt wirklich aus dem Internet laden, und Städte aus Paketen können eigene Aufträge haben.

- „Nach Paketen suchen“ fragt jetzt bei GitHub nach, wo die Pakete der Lernplattform liegen. Gezeigt werden nur fertige Pakete; Entwürfe, Vorabversionen und Programm-Updates erscheinen dort nie.
- Jede geladene Datei wird mit der Prüfsumme verglichen, die GitHub zu ihr nennt. Passt sie nicht oder fehlt sie, steht der Grund in Rot dabei und nichts wird geladen. Ein Update der App lädt nie ein Paket, und ein Paket ersetzt nie die App.
- Eine Stadt aus einem Paket kann jetzt Aufträge haben. Personen vor den Geschäften bieten sie mit „Auftrag annehmen“ an, gelöst werden sie wie die Tickets zu Hause. Sie zählen nicht zu den Tickets des Tages und warten, bis du sie löst.
- Fehlt das Paket auf einem Gerät, ruhen seine Aufträge; nichts geht verloren, und nach dem Laden geht es genau dort weiter. Gelöste Aufträge zählen auf jedem Gerät gleich.
- Der Fahrplan am Bahnhof nennt nur noch Städte. Pakete anderer Art stehen nur in den Optionen.
- Dein Lernstand, die Lerninhalte, die Prüfungen und deine Heimatstadt bleiben, wie sie sind.

### Herunterladen

**Fachinformatiker Lernplattform A 0.89.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.89.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.89.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.89.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.89.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
