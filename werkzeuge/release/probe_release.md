Spielst du die Stadt auf zwei Geräten, zeigt die Uhr nach dem Abgleich jetzt in Sonderfällen die richtige Zeit, und ein seltener Speicherfehler im Spiel wird noch sicherer abgefangen.

- Hast du am selben Spieltag auf PC und Handy ohne Abgleich gespielt und standen beide Figuren am selben Ort, lief die Uhr der Stadt nach dem Abgleich manchmal zu weit vor. Jetzt rechnet jeder Schritt ab dem Stand des Geräts, auf dem er gemacht wurde. Gehst du nach dem Abgleich von dem gezeigten Stand des anderen Geräts aus weiter, merkt sich das Spiel diesen Stand, damit beide Geräte dieselbe Uhrzeit zeigen.
- Haben zwei Geräte genau in derselben Sekunde einen Schritt in der Stadt gemacht, ordnet das Spiel jeden Schritt jetzt dem richtigen Gerät zu. Bisher konnten die Minuten beider Schritte zusammengezählt werden.
- Zwei Geräte gleicher Art (zum Beispiel zwei PCs) gelten für die Uhr der Stadt weiterhin als ein Gerät, weil das Programm nur „PC“ oder „Handy“ speichert. Spielst du auf zwei PCs am selben Tag ohne Abgleich, kann die Uhr dort weiter vorlaufen.
- Konnte ein Spielschritt einmal nicht gespeichert werden, nimmt das Spiel bis zum Neuladen des Spielstands sicher keine weiteren Schritte dieses Spielstands mehr an, die auf dem fehlenden aufbauen.
- Interne Prüfungen des Programms laufen zuverlässiger; an der Bedienung ändert sich dadurch nichts.

### Herunterladen

**Fachinformatiker Lernplattform A 0.32.9**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.32.9-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.32.9-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.32.9-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
