Die Stadt im Spiel sieht genauso aus wie bisher. Diese Version bereitet das Spiel auf Häuser mit mehreren Stockwerken aus dem Stadtbau-Werkzeug vor.

- Ein Haus in einer neuen Stadt kann bis zu vier Etagen über dem Erdgeschoss haben, verbunden durch eine Treppe oder einen Fahrstuhl.
- Im obersten Stock sieht man die Treppe als Öffnung im Boden mit Stufen nach unten und einem Geländer; darüber läuft man nicht.
- Stehst du vor einer Treppe, erscheint ein Knopf wie „Treppe hoch zum 1. Obergeschoss“. Am Fahrstuhl öffnet der Knopf ein Fenster, in dem du die Etage wählst. Ohne 3D-Ansicht stehen dieselben Knöpfe unter der Ortsliste.
- Das Haus verlässt du nur im Erdgeschoss durch die Eingangstür; oben sagt dir das Spiel, dass du zuerst nach unten musst.
- Das Spiel merkt sich, auf welcher Etage du bist. Beendest du das Programm oben, stehst du beim nächsten Start wieder dort. Gibt es die Etage in einer neuen Stadt nicht mehr, stehst du vor dem Haus.
- Die Prüfung für neue Städte meldet rot, wenn eine Etage größer ist als das Erdgeschoss, wenn eine Etage nicht erreichbar ist, wenn eine Treppe nirgends hinführt oder wenn man oben nicht von der Treppe zu jeder Ware gehen kann.
- An deinem Spielstand und an deinem Lernstand ändert sich nichts; der Abgleich zwischen PC und Handy nimmt die Etage mit.

### Herunterladen

**Fachinformatiker Lernplattform A 0.50.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.50.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.50.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.50.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.50.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
