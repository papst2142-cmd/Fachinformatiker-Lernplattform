Das Dashboard zeigt bei Anwendungsentwicklung, Daten- und Prozessanalyse und Digitaler Vernetzung nur noch deine Fachrichtung, alte Klausur-Einträge werden berichtigt, und eine Frage zu Entwurfsmustern ist neu gefasst.

- Hast du Anwendungsentwicklung, Daten- und Prozessanalyse oder Digitale Vernetzung gewählt, zählen auf dem Dashboard die Erfolgsquote, der Lernverlauf und die Antworten je Fachbereich jetzt nur die Karten, Fragen, Szenarien und Testprojekte deiner Fachrichtung. Bisher flossen dort auch Antworten aus anderen Fachrichtungen ein. Bei Systemintegration bleibt alles wie bisher.
- Vor Version A 0.32.0 hat der Abschluss einer Klausur in Anwendungsentwicklung, Daten- und Prozessanalyse oder Digitaler Vernetzung die Aufgaben beim falschen Szenario eingetragen (bei einem Szenario der Systemintegration). Beim ersten Start nach diesem Update berichtigt das Programm solche Einträge einmal von selbst. Vorher legt es eine Kopie deines Lernstands im Ordner „sicherungen“ an (Name beginnt mit „vor_bereinigung_“). Ohne Kopie ändert es nichts. Andere Einträge bleiben unverändert.
- Im Prüfungstrainer (Anwendungsentwicklung) ist die Frage zum Importprogramm mit verschiedenen Lesern neu gefasst: Sie beschreibt jetzt wirklich eine Fabrikmethode. Die alte Fassung wird nicht mehr gestellt.
- Die Hilfe zu Sicherung und Abgleich sagt jetzt richtig, dass als Gerät nur „PC“ oder „Handy“ gespeichert wird.

### Herunterladen

**Fachinformatiker Lernplattform A 0.32.6**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.32.6-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.32.6-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.32.6-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
