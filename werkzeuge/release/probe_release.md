Auf dem Mac gibt es jetzt das Update im Programm, und das Spiel zeigt die Stadt dort von allein in 3D.

- Mac: Unter „Optionen“ → „Updates“ sucht das Programm jetzt auch auf dem Mac nach einer neuen Version und spielt sie mit „Jetzt aktualisieren“ ein, wie unter Windows und Linux. Vorher legt es eine Kopie deines Lernstands im Datenordner an (Ordner „sicherungen“, Name beginnt mit „vor_update_“). Macs mit Apple-Chip und Macs mit Intel-Prozessor bekommen jeweils ihr eigenes Paket.
- Mac: Bisher meldete die Suche „Du hast die neueste Version“, obwohl es eine neuere gab. Findet das Programm auf dem Mac kein Paket zum Einspielen, schaut es jetzt zusätzlich auf der Release-Seite nach und bietet dort die neue Version zum Herunterladen an.
- Mac: Das Spiel zeigt die Stadt jetzt ohne zusätzliche Einstellung in 3D. Kann der Mac keine 3D-Grafik zeigen, erscheinen wie bisher ein Hinweis und die Liste der Orte.
- Interne Prüfungen des Programms installieren das fertige Mac-Paket jetzt auf Macs mit Apple-Chip und mit Intel-Prozessor, starten es und spielen ein Update ein; an der Bedienung ändert sich dadurch nichts.

### Herunterladen

**Fachinformatiker Lernplattform A 0.35.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.35.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.35.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.35.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.35.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
