Die Spielstadt zeigt auf manchen Handys und Tablets keine Bildfehler mehr, die Spielfigur geht nicht mehr über die Straße, und die Ortsliste lässt sich immer schließen.

- Auf Geräten mit einem Grafikchip von Mali (zum Beispiel dem Galaxy Tab A8) ragten in der Stadt lange, spitze Dreiecke aus Straßen, Gehwegen, Kreisverkehren und dem Teich über die Wiese. Straßen sahen stellenweise zerrissen oder dunkel aus.
- Die Ursache: Das Spiel ließ die Grafik die Lage der Ecken mit zu wenigen Stellen rechnen. Viele Geräte rechnen trotzdem genau, Mali-Chips nicht. Jetzt rechnet das Spiel die Lage überall genau.
- Auf allen anderen Geräten und am PC sieht die Stadt genau so aus wie vorher.
- Ein Problembericht nennt jetzt zusätzlich, wie genau die Grafik des Geräts rechnet. So lassen sich solche Fehler künftig schneller finden.
- Die Spielfigur geht nicht mehr über die Fahrbahn oder über die Schienen. Sie bleibt auf dem Gehweg und quert eine Straße nur auf einem Zebrastreifen oder an einer Ampel, und zwar gerade über die Mitte. Über die Schienen geht sie nur auf einem Bahnübergang. Liegt dein Ziel auf der anderen Straßenseite, führt der Weg über den nächsten Übergang. Das gilt beim Gehen von Ort zu Ort und nach einem Tipp in die Stadt.
- Die Ortsliste schließt sich jetzt, sobald die Figur einen Laden oder ein anderes Haus betritt. Vorher blieb sie drinnen offen, und es gab keinen Knopf, um sie zu schließen. Draußen öffnet der Knopf „Orte“ sie wie gewohnt.
- Dein Spielstand, dein Geld, deine Aufgaben, das Lernen und dein Lernstand bleiben, wie sie sind. Ein Weg von Ort zu Ort dauert im Spiel so lange wie vorher.

### Herunterladen

**Fachinformatiker Lernplattform A 0.75.1**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.75.1-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.75.1-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.75.1-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.75.1-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
