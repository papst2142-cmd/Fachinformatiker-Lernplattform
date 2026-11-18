Die Inhalte der Fachrichtungen Anwendungsentwicklung (AE), Daten- und Prozessanalyse (DPA) und Digitale Vernetzung (DV) lassen sich jetzt auch als Inhaltspaket nachladen. Heute bringt die App diese Inhalte weiter selbst mit; das Paket ist die Grundlage dafür, dass neue Fragen später ohne App-Update kommen können.

- Die Karte „Inhaltspakete“ in den Optionen zeigt drei neue Zeilen: „Fachrichtungs-Paket AE“, „Fachrichtungs-Paket DPA“ und „Fachrichtungs-Paket DV“. Heute steht dort „In der App enthalten“. Gibt es einen neueren Stand, steht er dabei, und „Laden“ holt ihn.
- Findet die Suche nach Paketen einen neueren Stand für deine Fachrichtung, zeigt die Lernansicht oben einmal einen Hinweis mit der Größe. Geladen wird nur, wenn du auf „Laden“ tippst; am Handy ohne WLAN fragt die App vorher nach.
- Ein Fachrichtungs-Paket darf nur Neues hinten anhängen. Jede Frage behält ihren Platz, nichts wird umgeschrieben oder verschoben. Ein Paket, das das versucht, wird abgelehnt und nicht eingesetzt.
- Solange du einen Prüfungssatz einer Fachrichtung schreibst, lässt sich ihr Paket nicht entfernen. Nach dem Entfernen gelten wieder die Inhalte, die die App mitbringt.
- Fehlen auf einem Gerät Inhalte, die du auf einem anderen schon bearbeitet hast, weist die App nach dem Abgleich einmal auf das Paket hin. Dein Fortschritt dazu bleibt und geht nach dem Laden genau dort weiter.
- Dein Lernstand, die Lerninhalte, die Prüfungen und deine Spielstände bleiben, wie sie sind.

### Herunterladen

**Fachinformatiker Lernplattform A 0.91.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.91.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.91.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.91.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.91.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
