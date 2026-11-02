Die Erinnerung ans Tagesziel ist da, Spielstand und Bestenliste lassen sich in den Optionen löschen, und einige Anzeigen für Anwendungsentwicklung, Daten- und Prozessanalyse und Digitale Vernetzung sind richtiggestellt.

- In den „Optionen“ unter „Tagesziel“ gibt es jetzt den Schalter „An das Tagesziel erinnern“ und das Feld „Erinnerung um“ (Vorgabe 18:00 Uhr). Ist das Tagesziel ab dieser Uhrzeit noch nicht erreicht, erscheint einmal am Tag ein Hinweis, wie viele Aufgaben noch fehlen. Am PC wird alle 5 Minuten nachgesehen, solange das Programm geöffnet ist, am Handy beim Öffnen und beim Zurückholen der App.
- In den „Optionen“ unter „Löschen und zurücksetzen“ lassen sich jetzt der Spielstand des aktiven Platzes zurücksetzen und die Bestenliste des Spiels löschen. Beides fragt vorher nach. Beim Zurücksetzen bleiben die anderen Plätze, die Bestenliste und der Lernfortschritt erhalten.
- Im „Fortschritt“ steht eine abgeschlossene Klausur aus Anwendungsentwicklung, Daten- und Prozessanalyse oder Digitaler Vernetzung jetzt mit ihrem Namen da (zum Beispiel „AP2 · Planen eines Softwareproduktes“) statt mit einem internen Kürzel.
- Im „Notizblock“ tragen Szenarien und Testprojekte jetzt dieselbe Nummer wie in ihrer Liste.
- Wer eine Klausur dieser drei Fachrichtungen abgeschlossen hat, sieht das Ergebnis der einzelnen Aufgaben jetzt beim richtigen Szenario. Bisher landete es bei einem Szenario der Systemintegration. Was bisher so gespeichert wurde, bleibt unverändert stehen.
- Für Systemintegration ändert sich an den Lerninhalten und Anzeigen nichts.

### Herunterladen

**Fachinformatiker Lernplattform A 0.32.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.32.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.32.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS** (nur Apple-Chips): `Fachinformatiker-Lernplattform-A-0.32.0-osx-arm64.pkg`. Nur gebaut, nicht auf einem Mac geprüft, nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Das Update im Programm gibt es auf macOS noch nicht.
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
