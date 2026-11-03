Eingabefelder bleiben bei großer Schrift am Handy jetzt immer in ihrer Karte und sind dadurch auch im dunklen Modus klar umrandet zu sehen.

- Im Rechen-Trainer ragten die Felder für die Antwort (zum Beispiel Stunden, Minuten und Sekunden) bei der Schrift „Extra groß“ und „Maximal“ am Handy links und rechts über die Karte hinaus; ihr Rahmen war dadurch nur oben und unten zu sehen, und im dunklen Modus wirkten sie blass. Jetzt passen sie sich der Breite der Karte an und haben rundum einen Rahmen.
- Dasselbe gilt für das Feld „Dein Name“ unter „Optionen“ → „Rundgang und Hilfe“ und für das Feld einer weiteren Phase im Zeitplan des Abschlussprojekts.
- Im Spiel liegt die Bildlaufleiste der Ortsliste und des Hinweistexts unten (auch beim Einrichten der Wohnung) jetzt neben dem Text und verdeckt die letzten Buchstaben nicht mehr.
- Interne Prüfungen des Programms prüfen jetzt auf allen Seiten, dass nichts unter der Bildlaufleiste liegt und kein Eingabefeld aus seiner Karte ragt; an der Bedienung ändert sich dadurch nichts.

### Herunterladen

**Fachinformatiker Lernplattform A 0.32.10**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.32.10-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.32.10-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.32.10-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
