Kleine Verbesserungen, damit nichts verloren geht und alles flüssig bleibt.

- Was du in einer laufenden Prüfung oder im Abschlussprojekt gerade tippst, wird jetzt auch vor einem Update sofort gespeichert und am Handy zuverlässig, sobald du die App verlässt. Vor einer Sicherung und beim Übernehmen eines Lernstands war das schon so.
- Neuer Knopf „Lizenzen“ unter „Optionen“ → „Über das Programm“: Er zeigt die vollständigen Lizenztexte aller mitgelieferten Bausteine, jetzt auch am Handy. Jeder Baustein steht in einer eigenen Karte zum Aufklappen.
- Die Fortschrittsringe auf der Startseite zeigen wieder einen sauberen Farbverlauf von einer Farbe zur anderen. Vorher sah man bei einem Teil-Fortschritt eine Farbe und ein Dreieck in einer anderen Farbe.
- Unter Linux bleibt das Fenster bedienbar, während „Verknüpfung anlegen“ arbeitet.
- Die Hilfe zur Hänger-Diagnose nennt das „Vorladen“ nicht mehr, weil es das in dieser Version nicht gibt.

### Herunterladen

**Fachinformatiker Lernplattform A 0.13.1**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.13.1-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.13.1-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.13.1-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
