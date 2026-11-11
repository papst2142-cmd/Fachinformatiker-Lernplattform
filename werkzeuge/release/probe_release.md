Neue Einstellung „Grafik“: du kannst wählen, wie aufwendig die Spielstadt gezeichnet wird, und siehst, wie stark dein Gerät dabei arbeitet.

- In den Optionen gibt es in der Gruppe „Aussehen“ die Karte „Grafik“ mit vier Stufen: Niedrig, Mittel, Hoch und Eigene. Jede Stufe hat einen kurzen Satz, was sie tut.
- Niedrig spart am meisten: Häuser in der Ferne werden schon früher einfach gezeichnet, kein Gras und keine Büsche, halb so viele Autos und Gestalten, die Stadt in 75 % Auflösung und höchstens 30 Bilder je Sekunde. Gut für ältere Handys und Tablets und für einen kühleren Akku.
- Mittel ist die Vorgabe und sieht genau so aus wie bisher.
- Hoch ist für starke Geräte: volle Formen bis in die Ferne und Gras weiter sichtbar.
- Bei „Eigene“ wählst du jeden dieser Werte selbst.
- Beim ersten Start nach dem Update wählt das Programm einmal selbst eine Stufe passend zum Gerät (zum Beispiel Hoch an einem PC mit starker Grafikkarte, Niedrig ohne Grafikkarte). Danach ändert es die Stufe nie mehr von allein; du kannst sie jederzeit umstellen.
- Unter der Stufe steht die Belastung des Geräts vom letzten Besuch in der Stadt: Bilder je Sekunde, Rechenzeit je Bild, Arbeitsspeicher und Prozessor des Programms und, wo das Gerät es meldet, die Grafikkarte. Was ein Gerät nicht meldet, steht als „nicht verfügbar“ da.
- Der Schalter für die Leistungsanzeige im Spiel steht jetzt auch in dieser Karte. Ist er an, zeigt die Zeile oben links im Spiel zusätzlich Arbeitsspeicher, Prozessor und Grafikkarte. Es wird nichts aufgezeichnet.
- Die Stufe ändert nur, wie die Stadt gezeichnet wird. Spielstand, Häuser, Türen, Wege und deine Figur bleiben in jeder Stufe gleich. Am Lernen und an deinem Lernstand ändert sich nichts.

### Herunterladen

**Fachinformatiker Lernplattform A 0.64.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-0.64.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-0.64.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-0.64.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-0.64.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
