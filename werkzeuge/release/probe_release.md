Die Suche nach Updates hält Störungen bei GitHub jetzt aus. Bisher meldete sie in solchen Zeiten oft „Keine Verbindung zu GitHub“ oder „GitHub hat die Anfrage abgelehnt“, obwohl es eine neue Version gab.

- GitHub, wo die neuen Versionen liegen, antwortet manchmal für kurze Zeit mit einer Fehlermeldung. Die Suche fragt dann bis zu vier Mal nach, mit kurzen Pausen dazwischen (15, 30 und 60 Sekunden). Kommt eine Antwort, geht es normal weiter.
- Am PC fragt die Suche nur noch nach der neuesten Version und nicht mehr nach jeder älteren. Das geht schneller und ist weniger anfällig.
- Das gilt für die Suche, für das Herunterladen der neuen Version und am Handy auch für das Laden der neuen App.
- Bleibt GitHub gestört oder hast du kein Internet, kommt nach den Versuchen dieselbe Meldung wie bisher. Die Suche dauert in diesem Fall länger als bisher (ohne Internet etwa zwei Minuten, wenn GitHub gar nicht antwortet einige Minuten).
- Jeder neue Versuch steht im Update-Protokoll (Datei update.log im Datenordner), damit man später sieht, was passiert ist.
- Am Lernen, am Spiel und an deinem Lernstand ändert sich nichts.

### Herunterladen

**Fachinformatiker Lernplattform A 0.53.2**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.53.2-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.53.2-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.53.2-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.53.2-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
