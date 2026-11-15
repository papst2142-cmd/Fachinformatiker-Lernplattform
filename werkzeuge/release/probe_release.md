Zeichenaufgaben kannst du jetzt auch im Programm lösen: mit dem Zeichen-Baukasten.

- Unter jeder Zeichenaufgabe öffnet der Knopf „Im Programm zeichnen“ den Baukasten. Er kann UML-Aktivitätsdiagramme (Start, Aktion, Verzweigung, Zusammenführung, Ende) und ER-Modelle in Chen-Notation (Entität, Beziehung, Attribut).
- Du wählst einen Baustein, und er erscheint an der nächsten freien Stelle. Mit Maus oder Finger tippst du auf einen Baustein, um ihn zu wählen, und auf ein freies Feld, um ihn dorthin zu setzen. Verbindungen ziehst du mit „Verbinden durch Antippen“. Bedingungen an Pfeilen, Kardinalitäten (1, n, m) und Schlüsselattribute stellst du beim gewählten Baustein ein.
- Ein vorgegebener Anfang steht fest in der Zeichenfläche und lässt sich nicht verschieben oder löschen.
- Alles geht auch ohne Maus: Die Liste „Deine Zeichnung“ nennt jeden Baustein mit Stelle und „verbunden mit …“. Beim gewählten Baustein gibt es Knöpfe wie „Nach rechts“ oder „Verbinden mit“. So kann man auch mit Tastatur und Screenreader zeichnen.
- Nach dem Abgeben siehst du deine Zeichnung neben dem Musterbild. Das Programm zählt, was sich zählen lässt: Bausteine je Art, Verbindungen, Kardinalitäten, Schlüsselattribute und die Regeln des Diagramms. Es zeigt das grün oder rot. Die Punkte vergibst du weiter selbst mit Häkchen.
- Deine Zeichnung wird wie jede Antwort gespeichert und bleibt auch erhalten, wenn das Programm unerwartet endet. Zeichnen auf Papier geht weiter wie bisher.

### Herunterladen

**Fachinformatiker Lernplattform A 0.78.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.78.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.78.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.78.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.78.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
