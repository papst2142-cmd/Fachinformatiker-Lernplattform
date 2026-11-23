Die Lerninhalte der Fachrichtungen Anwendungsentwicklung (AE), Daten- und Prozessanalyse (DPA) und Digitale Vernetzung (DV) kommen jetzt als eigenes Fachrichtungs-Paket. Du lädst nur das Paket deiner Fachrichtung; neue Inhalte für eine Fachrichtung kommen künftig als neuer Stand ihres Pakets, ohne App-Update.

- Die App selbst bringt nur noch die Inhalte für Systemintegration (SI) und die gemeinsamen Inhalte aller Fachrichtungen mit. Dadurch ist sie kleiner. Wer SI lernt, lädt nichts und merkt keinen Unterschied.
- Hast du AE, DPA oder DV gewählt, fragt die App beim ersten Start nach diesem Update einmal: „Für AE fehlt das Fachrichtungs-Paket (1,2 MB). Jetzt laden?“ Ein Tipp auf „Laden“ holt das Paket, und du lernst genau dieselben Inhalte wie vorher. Am Handy ohne WLAN fragt die App vorher, ob sie mobile Daten nutzen darf. Nichts lädt von allein.
- Tippst du „Später“, siehst du die gemeinsamen Inhalte, und auf der Startseite steht die Karte „Für AE fehlt das Fachrichtungs-Paket“ mit dem Knopf „Laden“, bis das Paket da ist. Laden geht auch unter Optionen › Inhaltspakete.
- Dein Lernstand bleibt vollständig erhalten. Was du zu Inhalten deiner Fachrichtung schon bearbeitet hast, ruht nur, solange das Paket fehlt, und ist nach dem Laden genau wieder da. Auch ein angefangener Prüfungssatz bleibt gespeichert.
- Ohne Internet zeigt die App einen klaren Hinweis; SI und die gemeinsamen Inhalte gehen weiter vollständig.
- Aussehen, Bedienung, die Prüfungen und deine Spielstände bleiben, wie sie sind.

### Herunterladen

**Fachinformatiker Lernplattform A 1.5.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-1.5.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-1.5.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-1.5.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-1.5.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
