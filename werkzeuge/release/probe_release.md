In der Prüfung im echten Format zeichnest du Struktogramme jetzt selbst im Baukasten.

- In der Anwendungsentwicklung (Prüfungsbereich 2) gibt es eine Aufgabe, in der du ein Struktogramm zeichnest. Bisher war das eine Aufgabe auf Papier, jetzt zeichnest du es direkt im Programm.
- Du hast sieben Bausteine: Anweisung, Verzweigung, Fallauswahl, Zählschleife, Schleife mit Prüfung am Anfang (solange), Schleife mit Prüfung am Ende (wiederhole) und Aufruf. Tippe einen Baustein an, und er kommt an die Stelle, die unter „Neuer Block kommt“ steht: nach oder vor dem gewählten Block oder in einen Zweig.
- Du tippst einen Block im Bild oder in der Liste darunter an und schreibst seinen Text. Mit „Nach oben“, „Nach unten“ und „Heraus“ verschiebst du ihn, mit „Entfernen“ nimmst du ihn weg; hat er Inhalt, fragt das Programm vorher nach. Bei der Fallauswahl fügst du Fälle hinzu oder nimmst sie weg, „sonst“ steht immer am Ende.
- Das Bild folgt der üblichen Norm: Ein leerer Zweig zeigt „∅“, ein Aufruf hat doppelte Seitenlinien. Dein Text steht genau so da, wie du ihn eingibst.
- Nach dem Abgeben siehst du deine Zeichnung neben dem Musterbild, dazu eine Zählung der Bausteine, damit du selbst vergleichen kannst.
- Mit der Tastatur wählst du Blöcke mit den Pfeiltasten und öffnest das Textfeld mit der Eingabetaste; der Screenreader liest jeden Block vor.
- Die Aufgabe hat dafür eine neue Fassung bekommen. Dein Lernstand, die anderen Lerninhalte und Prüfungssätze und deine Spielstände bleiben genau gleich.

### Herunterladen

**Fachinformatiker Lernplattform A 1.12.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-1.12.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-1.12.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-1.12.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-1.12.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
