Eine Aufräum-Version: alte, längst ersetzte Lerninhalte sind jetzt ganz aus dem Programm entfernt.

- Gelöscht sind 47 Karteikarten und 81 Quizfragen in einer älteren Fassung, die schon seit früheren Versionen durch eine verbesserte Fassung ersetzt und nicht mehr zu sehen waren, dazu die 10 alten Prüfungssätze für Anwendungsentwicklung (FIAE) und die 6 älteren Fassungen einzelner FIAE-Sätze. Was du beim Lernen siehst, bleibt gleich: dieselben Karten, Fragen und Prüfungssätze in derselben Reihenfolge.
- Dein Lernstand bleibt erhalten. Steht darin noch ein Eintrag mit dem alten Text einer Frage, schreibt das Programm ihn beim ersten Start einmal auf den heutigen Text um. Vorher legt es eine Kopie deines Lernstands an. Diese Prüfung läuft bei jedem Start mit; sie fällt erst in einer späteren Version weg, wenn alle Geräte mindestens diese Version haben.
- Gleichst du deinen Lernstand zwischen mehreren Geräten ab, aktualisiere bitte alle Geräte auf diese Version, bevor du wieder abgleichst.
- Hattest du eine Prüfung im echten Format mit einem der gelöschten Sätze offen, wird sie beim Öffnen beendet und nicht gewertet. Ein Hinweis sagt dir das. Deine fertigen Prüfungen bleiben im Verlauf.
- „Lernstand übernehmen“ nimmt jetzt eine Sicherung der Lernplattform oder eine Abgleich-Datei. Die Datenbank der früheren App lässt sich dort nicht mehr wählen.
- Für FIAE, FIDP und FIDV gibt es passend zu dieser Version neue Fachrichtungs-Pakete. Ältere Pakete passen nicht mehr zu ihr.

### Herunterladen

**Fachinformatiker Lernplattform A 1.18.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-1.18.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-1.18.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-1.18.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-1.18.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
