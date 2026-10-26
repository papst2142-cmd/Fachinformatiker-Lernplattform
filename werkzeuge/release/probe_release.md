Aufgeräumte Seitenleiste am PC und übersichtlichere Optionen.

- Die Seitenleiste am PC zeigt wieder das Logo und vor jedem Eintrag ein Symbol, wie am Handy. Die Einträge stehen in Gruppen mit kleinen Überschriften: oben das Dashboard, dann „Lernen“ (Karteikarten, Prüfungstrainer, AP1- und AP2-Szenarien, Testprojekt, Abschlussprojekt, Fortschritt), „Spiel“ und „Werkzeuge“ (Notizblock, Rechner). Hilfe und Optionen stehen immer sichtbar unten.
- Karteikarten und Spiel haben einen kleinen Pfeil: Ein Klick klappt die Unterpunkte auf oder zu. Der gewählte Eintrag ist hervorgehoben.
- Unten in der Seitenleiste zeigt der Kasten „Lernstatus“, wie viele Karten und Fragen du schon bearbeitet hast, mit Balken, darunter die Versionsnummer.
- Die Optionen sind in Gruppen geordnet: Lernen, Aussehen, Spiel, Daten und Programm. Jede Karte ist zuerst zugeklappt und zeigt nur ihren Titel und eine kurze Zeile; ein Tippen öffnet sie. Gibt es ein Update, steht das schon in der kurzen Zeile der Karte „Updates“.
- Die Suche findet weiterhin jeden Bereich der Optionen und klappt ihn auf.
- Am Handy bleibt die untere Leiste, wie sie war.

### Herunterladen

**Fachinformatiker Lernplattform A 0.13.2**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.13.2-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.13.2-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.13.2-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
