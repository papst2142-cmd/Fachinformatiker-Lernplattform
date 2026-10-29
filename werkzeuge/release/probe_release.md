Neu im Spiel: Die Stadt wird beim Öffnen einmal ganz vorbereitet, danach geht das Betreten der Häuser ohne Wartezeit.

- Öffnest du das Spiel, bereitet das Programm die ganze Stadt, alle Räume, die Gestalten der Festzeiten und die Bilder der Figur einmal vor. Dauert das länger als drei Zehntelsekunden, steht in der Mitte ein Ring mit „Stadt wird geladen …“, der zeigt, wie weit es ist. Danach öffnet sich jedes Haus sofort, auch beim ersten Betreten.
- Ein Raum wird nur dann neu aufgebaut, wenn sich in ihm wirklich etwas geändert hat (zum Beispiel neue Möbel oder eine neue Festzeit). Bisher wurde er oft unnötig neu gebaut.
- Beginnt eine Festzeit oder eine neue Jahreszeit, während du in einem Haus bist, baut das Programm die Stadt schon im Hintergrund um. Beim Hinausgehen steht sie sofort bereit.
- Am Handy: Kehrst du aus einer anderen App zurück, wird nur das Bild der Stadt neu an die Grafik übergeben, mit derselben Ladeanzeige. Die Stadt wird dafür nicht noch einmal berechnet.
- Behoben: In einem sehr schmalen Fenster konnte das Namensschild eines Hauses (zum Beispiel „Zuhause“) kurz über den Rand ragen, nachdem das Fenster verkleinert worden war.
- Dein Spielstand und das Spiel selbst ändern sich dadurch nicht.

### Herunterladen

**Fachinformatiker Lernplattform A 0.23.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.23.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.23.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.23.0-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
