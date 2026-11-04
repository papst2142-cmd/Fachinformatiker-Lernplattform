Nach dem Test auf allen Geräten: Das Update am Mac klappt jetzt auch im Ordner „Programme“ für alle Benutzer, am PC lässt sich der Bildschirm für das Vollbild wählen, und am Handy gibt es „Programm beenden“.

- Mac: Lag das Programm im Ordner „Programme“ für alle Benutzer, scheiterte das Update im Programm still, und die alte Version startete wieder. Jetzt fragt macOS in diesem Fall nach dem Passwort eines Administrators; vorher erklärt ein Hinweis im Update-Fenster, warum. Ohne Passwort bleibt die bisherige Version.
- Mac: Das Installationspaket installiert nur noch für den eigenen Benutzer (in „Benutzer“ → „Programme“). Dort klappt das Update ohne Passwort. Eine schon vorhandene Version im Ordner „Programme“ für alle Benutzer kann sich ab dieser Version selbst aktualisieren; ältere Versionen dort bitte einmal neu installieren.
- Mac: Die Diagnose meldete Hänger, während das Programm verdeckt im Hintergrund lag. macOS bremst dann die Zeitgeber eines Programms, das war kein echter Hänger. Solche Fälle stehen jetzt nur noch mit dem Zusatz „Programm verdeckt“ in „haenger.log“ und nicht mehr im Fehlerprotokoll. Hängt das Programm, während es zu sehen ist, wird das wie bisher gemeldet.
- PC: In „Optionen“ → „Vollbild“ gibt es bei mehr als einem Bildschirm die Auswahl „Bildschirm für Vollbild“. Vorgabe ist wie bisher der Bildschirm, auf dem das Fenster liegt. Wählst du einen festen Bildschirm, geht das Vollbild immer dorthin; ist er nicht angeschlossen, bleibt es beim Bildschirm des Fensters.
- Handy und Tablet: „Programm beenden“ gibt es jetzt auch hier, auch im Vollbild. Am Handy (und am Tablet im Hochformat) beendet das X oben rechts neben der Lupe das Programm; dieselbe Möglichkeit steht in „Optionen“ → „Programm beenden“. Am Tablet im Querformat steht der Knopf unten in der Seitenleiste wie am PC. Vorher werden alle Eingaben gespeichert und, wenn eingerichtet, ein letzter Abgleich gemacht.

### Herunterladen

**Fachinformatiker Lernplattform A 0.37.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.37.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.37.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.37.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.37.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
