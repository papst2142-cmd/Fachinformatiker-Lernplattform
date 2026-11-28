Gehen im Spiel ist auf schwächeren Tablets gleichmäßiger, und die Kamera springt beim Gehen nicht mehr zurück.

- Bei 30 oder 60 Bildern je Sekunde kamen die Bilder bisher ungleichmäßig: Das Programm wartete genau eine dreißigstel (bzw. sechzigstel) Sekunde und bat erst dann um das nächste Bild. Weil der Bildschirm nur in festen Takten ein neues Bild zeigt, kam die Bitte oft knapp zu spät, und das Bild erschien einen Takt später. Jetzt bittet das Programm etwas früher um das Bild, so dass es genau im richtigen Takt erscheint. Es werden dabei nie mehr Bilder als eingestellt.
- Die Figur und die Kamera bewegen sich jetzt je gezeigtem Bild um genau gleich viel weiter. Vorher wirkte die Bewegung dadurch auch bei 60 Bildern je Sekunde unruhig.
- Jedes Mal, wenn du beim freien Gehen stehen bleibst, schreibt das Spiel die gegangene Zeit in den Spielstand. Dabei wurde bisher der ganze Spielverlauf kopiert; mit einem langen Spielstand dauerte das am Tablet bis zu einer Zehntelsekunde, und das Bild stand kurz still. Jetzt wird nur noch kopiert, was sich ändern kann. Am PC braucht ein Halt dadurch nur noch rund ein Drittel des Speichers, und das wächst nicht mehr mit der Länge des Spielstands. Auch die Zahl der offenen Aufträge am Handy-Knopf wird nach einem bloßen Weg nicht mehr ganz neu gerechnet, weil ein Weg sie nie ändert. Ein Halt dauert am PC damit nur noch rund ein Drittel der Zeit.
- Bliebst du kurz vor einer Tür stehen, lief die Figur bisher von selbst das letzte Stück bis genau vor die Tür, oft ein kleines Stück zurück, und die Kamera ging mit. Jetzt bleibt die Figur dort stehen, wo du sie angehalten hast. „Hineingehen“ geht von dort wie bisher.
- Was im Spiel gezählt wird (Spielzeit, Wege, Aufgaben), bleibt genau gleich. Dein Spielstand und dein Lernstand bleiben erhalten.

### Herunterladen

**Fachinformatiker Lernplattform A 1.20.1**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-1.20.1-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-1.20.1-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-1.20.1-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-1.20.1-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
