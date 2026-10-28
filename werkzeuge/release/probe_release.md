Neu: In der Stadt wird es abends dunkel, du kannst länger unterwegs sein, das Hinweisfeld lässt sich zuklappen, und am breiten Bildschirm stehen die Karten des Dashboards nebeneinander.

- Die Stadt folgt jetzt der Uhrzeit im Spiel. Morgens ist das Licht etwas wärmer und dunkler, tagsüber sieht alles aus wie bisher. Gegen Abend kommt die Dämmerung mit Abendrot, danach wird es blau und ab 20 Uhr Nacht. Im Winter beginnt die Dämmerung schon um 16 Uhr, im Frühling und Herbst um 17:30 Uhr und im Sommer um 18:30 Uhr. Springt die Uhr, wechselt das Licht sanft innerhalb von zwei Sekunden.
- Nachts brennt in etwa jedem zweiten Fenster Licht, und 30 Straßenlaternen am Straßenrand leuchten. Die Räume innen bleiben immer hell. Es ist nur die Darstellung: dein Spielstand ändert sich dadurch nicht.
- Abends musst du nicht mehr sofort nach Hause: Nach 20 Uhr kannst du weiter überall hingehen, die Häuser haben ihre normalen Öffnungszeiten (die meisten sind dann zu). Die Uhr läuft bis 23 Uhr weiter und bleibt dann stehen, bis du zu Hause schlafen gehst. Der neue Tag beginnt wie bisher um 7 Uhr.
- Das Hinweisfeld unten in der Stadt hat links einen kleinen Pfeil. Ein Tipp darauf klappt das ganze Feld zu einem Pfeil zusammen, ein weiterer Tipp klappt es wieder auf. Das Programm merkt sich deine Wahl. Ist das Feld zugeklappt, erscheint eine neue kurze Meldung (zum Beispiel „Geschlossen“ oder „Es ist 23 Uhr“) für einige Sekunden neben dem Pfeil.
- Am breiten Bildschirm (ab etwa 900 Punkten Inhaltsbreite bei normaler Schrift) zeigt das Dashboard seine Karten nebeneinander wie die alte Fassung am PC: die vier Ringe und die Erfolgsquote in einer Reihe, der Lernverlauf neben der Abdeckung, die fünf Fachbereiche in einer Reihe, die Aktivitäten neben den Lerntagen und die AP1- neben den AP2-Prüfungsthemen, dort mit fünf Ringen in einer Reihe und dem Namen darunter. Am Handy, in schmalen Fenstern und bei sehr großer Schrift bleibt alles wie bisher.

### Herunterladen

**Fachinformatiker Lernplattform A 0.21.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.21.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.21.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.21.0-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
