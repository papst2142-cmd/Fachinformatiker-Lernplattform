Die Seitenleiste am PC und die Optionen sind aufgeräumt: Alles steht zugeklappt unter seiner Überschrift und klappt erst auf, wenn du darauf klickst.

- Die Seitenleiste ist jetzt gegliedert wie die Leiste unten am Handy: Start, Lernen, Rechner, Spiel, Fortschritt, Optionen. Darunter stehen wie bisher Hilfe und Programm beenden. Das gilt am PC und am Tablet im Querformat.
- Unter „Lernen“ stehen Karteikarten (mit den Fachbereichen), Prüfungstrainer, AP1-Szenarien, AP2-Szenarien, Testprojekt, Abschlussprojekt und Notizblock. Ein Klick auf „Lernen“ klappt die Liste auf und zeigt die Übersicht mit allen Lernbereichen. „Start“ öffnet das Dashboard.
- Beim Programmstart ist alles zugeklappt. Es ist immer nur eine Gruppe offen: Öffnest du eine andere, klappt die offene zu. Wechselst du auf eine Seite ohne Gruppe (zum Beispiel den Rechner), klappt alles zu. Kommst du über die Suche oder eine Kachel des Dashboards auf eine Seite, öffnet sich ihre Gruppe von selbst.
- Optionen (PC, Tablet und Handy): Die Gruppen Lernen, Aussehen, Spiel, Daten und Programm sind beim Öffnen zugeklappt. Unter jedem Gruppennamen steht, welche Bereiche darin sind. Auch hier ist immer nur eine Gruppe offen. Die Bereiche darin und ihre Einstellungen sind unverändert.

### Herunterladen

**Fachinformatiker Lernplattform A 0.38.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.38.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.38.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.38.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.38.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
