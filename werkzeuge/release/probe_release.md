Am Handy und am Tablet aktualisiert sich die App jetzt selbst, wie am PC: ein Tipp auf „Jetzt aktualisieren“ genügt, der Umweg über den Browser fällt weg.

- Gibt es eine neue Version, zeigt das Update-Fenster am Handy den Knopf „Jetzt aktualisieren“ und die ungefähre Größe des Downloads. Die App lädt die neue Version selbst, prüft sie und gibt sie zur Installation an Android weiter. Dein Lernstand bleibt erhalten, vorher legt die App wie am PC eine Sicherheitskopie an.
- Beim ersten Update fragt die App einmal nach der Erlaubnis „Unbekannte Apps installieren“. Ein Tipp auf „Zur Einstellung“ öffnet die passende Einstellung; danach geht das Update von allein weiter. Ohne Erlaubnis bleibt alles, wie es ist, und die neue Version gibt es weiter über „Zur Download-Seite“.
- Bist du nicht im WLAN, weist das Fenster darauf hin, dass das Update über mobile Daten lädt, und nennt die Größe. Du entscheidest selbst, ob du jetzt oder später aktualisierst.
- Vor der Installation prüft die App die geladene Datei: Prüfsumme aus zwei Quellen, Name der App, Signatur des Herausgebers und dass die Version neuer ist. Passt etwas nicht, wird die Datei gelöscht und nichts installiert.
- Android schließt die App während der Installation kurz. Das Fenster sagt das vorher; öffne die App danach einfach wieder. Beim nächsten Start steht im Update-Protokoll, ob das Update angekommen ist.
- Klappt die Installation nicht (zu wenig Speicher, abgebrochen, von Android gesperrt), nennt ein Fenster den Grund und bietet „Zur Download-Seite“ an. Dein Lernstand bleibt dabei unverändert.
- Die Hilfe unter „Update“ beschreibt den neuen Ablauf am Handy, auch was eine Warnung von Play Protect bedeutet.

### Herunterladen

**Fachinformatiker Lernplattform A 0.39.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.39.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.39.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.39.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.39.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
