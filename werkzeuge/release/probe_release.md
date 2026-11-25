In der Prüfung im echten Format zeichnest du jetzt auch UML-Klassendiagramme und Use-Case-Diagramme selbst im Baukasten.

- Im Klassendiagramm setzt du Klassen, gibst jeder einen Namen und fügst mit „Attribut hinzufügen“ und „Methode hinzufügen“ Zeilen an. Klassen verbindest du mit „Verbinden …“: Assoziation, Vererbung, Aggregation oder Komposition. An Assoziation, Aggregation und Komposition wählst du an beiden Enden die Multiplizität (1, 0..1, *, 0..* oder 1..*). Bei Vererbung, Aggregation und Komposition zeigt die Spitze bzw. Raute in die richtige Richtung; mit „Richtung umkehren“ drehst du sie.
- Im Use-Case-Diagramm ist die Systemgrenze vorgegeben. Akteure stehen außerhalb, Anwendungsfälle innerhalb. Du verbindest sie mit Assoziation, «include», «extend» oder Generalisierung; die Pfeile zeigen in die richtige Richtung.
- Nach dem Abgeben vergleicht das Programm deine Zeichnung mit dem Musterbild und zeigt grün, was passt, und rot, was fehlt oder anders ist. Die Punkte vergibst du wie bisher selbst mit den Häkchen. Der Kopf der Bewertung nennt bei einem AE-Satz den AE-Bereich.
- Fünf Prüfungssätze der Fachrichtung Anwendungsentwicklung (AE) haben eine neue Fassung: Drei Teilaufgaben mit Klassendiagramm und zwei mit Use-Case-Diagramm werden jetzt im Baukasten gezeichnet statt auf Papier. Alle anderen Teilaufgaben und alle Themen der Sätze bleiben.
- Hast du AE gewählt, braucht die App dafür den neuen Stand des AE-Pakets. Sie zeigt nach dem Update wie beim ersten Mal die Karte „Für AE fehlt das Fachrichtungs-Paket“ mit dem Knopf „Laden“.
- Hast du einen dieser fünf Sätze gerade angefangen, läuft er mit der neuen Fassung weiter. Deine Antworten zu den geänderten Teilaufgaben bleiben gespeichert, werden aber nicht mehr gezeigt; das Programm sagt dir das einmal. Alle anderen Antworten bleiben, wie sie waren.
- Dein Lernstand, die übrigen Lerninhalte, die Prüfungen und deine Spielstände bleiben genau gleich.

### Herunterladen

**Fachinformatiker Lernplattform A 1.8.0**: privates Lernprogramm für die Ausbildung Fachinformatiker/in (Alphaversion „A“). Copyright Nico H, alle Rechte vorbehalten (siehe `LICENSE.txt`).

## Welche Datei für welches Gerät?

- **Windows:** `Fachinformatiker-Lernplattform-win-Setup.exe` herunterladen und starten. Das Programm ist nicht digital signiert. Erscheint „Der Computer wurde durch Windows geschützt“, auf „Weitere Informationen“ und dann „Trotzdem ausführen“ klicken.
- **Linux:** `Fachinformatiker-Lernplattform-A-1.8.0-AppImage.tar.gz` herunterladen und entpacken, dann `Fachinformatiker-Lernplattform.AppImage` doppelklicken. Die einzelne Datei `Fachinformatiker-Lernplattform.AppImage` braucht das Update im Programm; lädst du sie direkt, fehlt ihr das Recht „Als Programm ausführen“.
- **Android** (64-Bit, arm64-v8a): `Fachinformatiker-Lernplattform-A-1.8.0-android-arm64.apk` am Handy herunterladen und öffnen. Android fragt, ob der Browser Apps installieren darf.
- **macOS mit Apple-Chip (M1 bis M4):** `Fachinformatiker-Lernplattform-A-1.8.0-osx-arm64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- **macOS mit Intel-Prozessor (macOS 14 oder neuer):** `Fachinformatiker-Lernplattform-A-1.8.0-osx-x64.pkg`. Nicht signiert: nach dem ersten Öffnen unter „Systemeinstellungen“ → „Datenschutz & Sicherheit“ „Dennoch öffnen“ wählen. Neue Versionen kommen danach über das Update im Programm („Optionen“ → „Updates“).
- Die übrigen Dateien (`.nupkg`, `RELEASES…`, `releases.*.json`) braucht das Update im Programm. Lizenzen der Fremdbestandteile: `THIRD_PARTY_NOTICES.txt` (Übersicht) und `THIRD_PARTY_LICENSES.txt` (volle Texte).

## Prüfsummen (SHA-256)

Vergleiche die Prüfsumme der heruntergeladenen Datei mit dieser Liste (Windows: `certutil -hashfile <Datei> SHA256`, Linux und macOS: `sha256sum <Datei>` bzw. `shasum -a 256 <Datei>`).

| Datei | Größe (Bytes) | SHA-256 |
|---|---:|---|
| `Probe-1.txt` | 8 | `84a1cacf88394c5158e1be0a32905a9700d1aae224d14302773d7e4d0d0e61e0` |
| `Probe-2.txt` | 8 | `9b282b9417c402d06fba3dafcfb2203a2d5d78452315544a6c9dd756f2db27ec` |

Release-Seite: https://github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases
