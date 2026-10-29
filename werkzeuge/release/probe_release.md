Kleine Verbesserungen: ein genauerer Hinweis zum Vollbild, eine strengere Prüfung der Spielinhalte und aufgeräumte Räume im Spiel.

- In den Optionen steht beim Vollbild jetzt auch, dass am PC die Taste F11 das Vollbild ein- und ausschaltet und die Taste Esc es verlässt.
- Beim Start prüft das Programm die Inhalte des Spiels (Aufträge, Texte, Zahlen) jetzt genauso gründlich wie die frühere Python-Fassung, auch die Platzhalter in Texten wie „{kunde}“. Ein fehlerhaft gebauter Inhalt fällt damit schon beim Laden auf und nicht erst mitten im Spiel.
- Im Spiel sitzen Personen an ihrem Schreibtisch jetzt richtig auf dem Bürostuhl und schauen zum Tisch, statt im Stuhl zu stehen.
- In der Wartehalle des Bahnhofs Talheim stehen keine Bänke mehr mitten im Raum. In der Bank und im Amt steht die Wartebank jetzt an der Wand, Wege und Türen bleiben frei.
- In der Verwaltung der Bitweiche stehen die beiden Schreibtische an den Wänden, dazwischen ist ein Gang frei.
- Am Lernstand, am Spielstand und an der Bedienung ändert sich nichts.

### Herunterladen

**Fachinformatiker Lernplattform A 0.23.1**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.23.1-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.23.1-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.23.1-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
