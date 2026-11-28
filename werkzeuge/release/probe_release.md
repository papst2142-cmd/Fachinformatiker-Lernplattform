In der Prüfung im echten Format gibt es jetzt den Schreibtischtest als Tabelle zum Ausfüllen.

- Bei einem Schreibtischtest gehst du ein kleines Programm Schritt für Schritt durch und trägst ein, welchen Wert jede Variable nach jedem Schritt hat. Bisher war das ein freies Textfeld. Jetzt siehst du eine Tabelle mit einer Zeile je Schritt und einer Spalte je Variable. Was schon vorgegeben ist, steht grau in der Tabelle; du füllst nur die leeren Kästchen aus.
- Tippst du in ein Kästchen, wird die passende Zeile im Programmcode daneben (am PC) oder darüber (am Handy) farbig markiert. Mit der Eingabetaste springst du zum nächsten leeren Kästchen.
- Am Handy lässt sich die Tabelle seitlich wischen. Die Spalte mit der Zeilennummer bleibt dabei immer stehen.
- Bei der Auswertung siehst du je Kästchen, ob es richtig war, und daneben die Musterlösung. Bewertet werden nur die Kästchen, die du ausfüllen solltest.
- Umgestellt sind drei Aufgaben: in AP1 die Sätze „Bootswerft Elbkrümmung GmbH“ (Aufgabe 4b) und „Backstube Mehlschwalbe GmbH“ (Aufgabe 4a), in AP2 Bereich 2 für FIAE der Satz „Bergbahnen Hohenfirst GmbH“ (Aufgabe 2a). Hattest du eine dieser Prüfungen gerade offen, wird deine bisherige Antwort zu genau dieser Teilaufgabe beiseitegelegt, weil sie nicht mehr in die Tabelle passt. Alle anderen Antworten bleiben.
- Das Programm kann jetzt auch die Ausgabe eines Programms und einen Datenauszug (Tabelle aus einer CSV-Datei oder ein JSON-Text) als Anlage zeigen. Beide lassen sich mit „Groß öffnen“ auf die ganze Fläche vergrößern, den CSV-Auszug kannst du zwischen Tabelle und Originaltext umschalten. Noch nutzt keine Aufgabe diese Anlagen; sie sind für kommende Prüfungssätze vorbereitet.
- Für Anwendungsentwicklung (FIAE) gibt es passend zu dieser Version ein neues Fachrichtungs-Paket. Ältere FIAE-Pakete passen nicht mehr zu ihr.
- Dein Lernstand, deine fertigen Prüfungen und deine Spielstände bleiben erhalten.

### Herunterladen

**Fachinformatiker Lernplattform A 1.20.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-1.20.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-1.20.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-1.20.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-1.20.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
