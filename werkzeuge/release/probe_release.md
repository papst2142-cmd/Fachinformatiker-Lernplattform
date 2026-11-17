In den Optionen gibt es eine neue Karte „Inhaltspakete“. Damit lassen sich später zusätzliche Inhalte, zum Beispiel neue Städte, laden, ohne die App neu zu installieren. Heute ist sie die Grundlage; spielbare Pakete kommen mit einer der nächsten Versionen.

- Die Karte steht in der Gruppe „Programm“ unter „Updates“. „Nach Paketen suchen“ fragt im Internet nach, welche Pakete es gibt. Jedes Paket zeigt seinen Namen, eine kurze Beschreibung, seine Größe und ob es geladen ist, ob es einen neueren Stand gibt oder ob es fehlerhaft ist.
- Geladen wird nur, wenn du auf „Laden“ tippst; nichts lädt von allein. Ein Balken zeigt den Fortschritt, „Abbrechen“ hört jederzeit auf. Bricht das Laden ab, bleibt alles, wie es vorher war.
- Am Handy ohne WLAN fragt die App vor dem Laden nach und nennt die Größe des Pakets.
- Jedes Paket wird vor dem Einsetzen ganz geprüft. Ist es beschädigt, unvollständig oder für eine neuere App gedacht, steht der Grund in Rot dabei, und nichts wird halb geladen. „Entfernen“ löscht ein Paket nach einer Rückfrage wieder ganz.
- Ohne Internet geht alles, was schon geladen ist, weiter; die Karte sagt dann „Keine Verbindung“.
- Im Spiel verschwindet jetzt auch der Knopf „Spiel verlassen“, wenn du die untere Leiste mit dem Dreieck zuklappst. Aufgeklappt ist er wie bisher da.
- Dein Lernstand, die Lerninhalte, die Prüfungen und die Stadt bleiben, wie sie sind.

### Herunterladen

**Fachinformatiker Lernplattform A 0.87.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.87.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.87.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.87.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.87.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
