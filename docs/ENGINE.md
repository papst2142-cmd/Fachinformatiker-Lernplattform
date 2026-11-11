# Die eigene Engine (Spiel und Stadtbau-Werkzeug)

Stand A 0.62.1 / S 0.37.1 (Weisung P12-175 „Engine festigen“ Stufe 1, Weisung P12-176 Stufe 2 Teil 1). Dieses Blatt erklärt, woraus die 3D-Stadt besteht,
wie Daten vom Werkzeug ins Spiel kommen und welche Regeln für jede Änderung gelten. Einzelheiten stehen im Code
(Kommentare am Anfang jeder Datei) und in `CLAUDE.md`; hier stehen nur Überblick und Verweise.

## 1. Was zur Engine gehört

Die Engine ist der Teil, den **Spiel und Werkzeug gemeinsam** nutzen. Das Werkzeug ist ihr Editor.

| Ort | Inhalt |
|---|---|
| `src/Lernplattform.Core/Spiel/Probe/` | Szene, Netze, Modelle, Stadt, Innenräume, Verkehr, Licht (Namensraum `Lernplattform.Core.Spiel.Probe`) |
| `src/Lernplattform.Core/Stadtbau/` | Stadt-Datei und Fassungen, Prüfregeln, Grenzen, Bauteile, Etagen (Namensraum `Lernplattform.Core.Stadtbau`) |
| `src/Lernplattform.App/Spiel/StadtFlaeche*.cs` | Zeichnen mit OpenGL (Hochladen, Sichtkegel, Aufrufe) |

Nicht zur Engine gehören der Spiel-Kern (`Core/Spiel/*.cs`: Regeln, Spielstand, Ereignisse; die Engine liest ihn nur), der
Lernteil und das Werkzeug selbst (`src/Stadtbau.*`). Die Trennlinie prüft `tests/Lernplattform.Tests/EngineGrenzenTests.cs`:
die Engine nutzt keinen Namensraum des Lernteils und nichts aus `Stadtbau.*`, und die Lernplattform enthält vom Werkzeug
nur diesen gemeinsamen Teil.

## 2. Bestandteile

- **Szene und Netze** (`Szene.cs`, `StadtSzene.cs`): `StadtSzene.Bauen` legt aus Karte, Stadtdaten, Spielstand, Jahreszeit
  und Fest alle Stücke an; `Vorbereiten` baut daraus die Netze (`Netz`: Ecken, Farben je Ecke, Indizes): Stadtnetz, je Haus
  ein Netz, Kleingrün, Paket-Netz (Laternen, Bahnübergang, Masten, Haltestellen), Ampel-Lichter. Das Werkzeug baut nach einer
  Änderung nur Teile neu (`StadtSzene.Teilbau.cs`, gleiches Ergebnis wie der volle Bau).
- **Zeichnen und Aufrufe** (`StadtFlaeche.cs`, `StadtFlaeche.Instanzen.cs`): je Netz ein Aufruf, je Haus im Bild einer, je
  Satz des Instancing einer. Die Zählung ohne Grafikkarte (`StadtbauGrenzen.Draussen.cs`) rechnet nach denselben Regeln; der
  Start-Test vergleicht sie mit dem gezeichneten Bild.
- **Sparen** (Welle D1/D2): Sichtkegel je Haus und je Stück (`FestUebergang.ImSichtkegel`, `Netz.Kugel`), Fernform ab
  Kameraabstand `StadtSzene.FernAb` 25 (`Fernform.cs`, Gitter 1/32), Unterseiten am Boden weg (`StadtSzene.UnterseiteBis`),
  Instancing für Modelle mit mindestens 8 gleichen Lagen (`Instanzen.cs`), gesparte Fahrzeugmodelle (`Fahrzeugmodelle.cs`).
- **Laufraster** (`StadtSzene.LaufrasterBauen`): wo die Figur gehen darf (Häuser, Wasser, Fahrbahn mit Gehweg gesperrt;
  Zebrastreifen, Furten, Stege, Fußwege frei).
- **Verkehr** (`Verkehr.cs`, `FestVerkehr.cs`, `Autostrecken.cs`, `Buslinie.cs`, `Ampeln.cs`, `Zebrastreifen.cs`,
  `Haltestellen.cs`, `Parkplaetze.cs`): nur Darstellung, kein Spielstand. Feste Strecken (Fassung 0) oder automatische
  Strecken aus dem Straßennetz; Zufall je Spieltag aus Stadt, Tag und Auto, auf allen Geräten gleich.
- **Wasser** (`Wasser.cs`): Fluss, Teiche, Stege, Eis im Winter, als Flächen im Stadtnetz.
- **Innenräume und Etagen** (`Innenraum.cs`, `StadtSpiel.Etagen.cs`, `Core/Stadtbau/Etagen.cs`, `InnenGrundriss.cs`).
- **Licht und Zeit** (`Tageslicht.cs`, `Nachtlicht.cs`, `Jahreszeitfarben.cs`, `FestDeko.cs`): Tag/Nacht, 22 Laternen,
  Festzeiten.
- **Modelle** (`Grafik.cs`: `GrafikBibliothek`, `GrafikModell`): alle Grafik im Format FLGM (Abschnitt 4), nur CC0.
- **Katalog** (`Katalogstueck.cs`; Katalog selbst nur im Werkzeug `src/Stadtbau.Core/Katalog.cs`): Stücke `cc0:paket/name`;
  ins Spiel kommen nur verbaute Stücke (`katalog_verbaut.bin`).
- **Eigene Gebäude** (`Core/Stadtbau/Bauteile.cs`): Teile aus dem Gebäude-Editor werden zu einem Modell zusammengesetzt.
- **Prüfregeln** (`Core/Stadtbau/StadtbauPruefung*.cs`): jede Regel ein klarer roter (oder gelber) Fund in einfachen Worten.
  Das Werkzeug zeigt sie während des Bauens, das Spiel prüft beim Laden (`StadtbauPruefung.Verbaut`, `SpielPruefung`).

## 3. Datenfluss Werkzeug → Spiel

1. Nico baut im Werkzeug ein **Projekt** (`.fisbau`, Datenordner des Werkzeugs). Es hält nur die Änderungen gegenüber der
   Ausgangsfassung, den Verlauf und Teile nur für das Werkzeug (Notizen, automatische Laternen, Innenräume eigener Gebäude).
2. **Export** (nur ohne roten Fund): das Werkzeug schreibt eine **Stadt-Datei** (`.fistadt`) und sendet sie über „An CC
   senden“.
3. CC legt sie als neue **Fassung** `inhalte_csharp/spiel/stadtbau/<stadt>_fassung<n>.fistadt` ab, trägt die Nummer in
   `aktiv.json` ein und erzeugt `inhalte_csharp/grafik/katalog_verbaut.bin/.json` mit `werkzeuge/stadtbau/verbaut.py` neu.
   Fassung 0 und die Dateien davor bleiben unverändert.
4. Das **Spiel** (`SpielStart`) liest die geltende Fassung (`AktiveFassung.Laden`), prüft sie (`StadtFassung.Lesen`,
   Ladeprüfung) und baut daraus Stadt und Innenräume.

## 4. Formate

| Format | Nummer heute | ältere, noch lesbar (echte Testdatei) | schreibt | liest |
|---|---|---|---|---|
| Stadt-Datei `.fistadt` | 1 | – | Werkzeug (Export), `StadtFassung` | `StadtFassung.Lesen`, `StadtbauPruefung.Format` |
| `aktiv.json` | 1 (Feld `format`, seit A 0.62.1) | ohne Feld = 1 (`aktiv_A0.62.0.json`) | CC von Hand | `AktiveFassung.FormatVon`, `Laden` |
| Projekt `.fisbau` | 3 | 1 (`projekt_S0.1.0.fisbau`), 2 (`projekt_S0.4.0.fisbau`) | Werkzeug | `Projekt.Oeffnen` (neuer = nur ansehen) |
| Blueprint `.fisblueprint` | 3 | 1 (`blueprint_S0.8.0`), 2 (`blueprint_S0.18.0`) | Werkzeug | `Blueprint.Lesen`, `Blueprint.Alle` |
| Grafik FLGM (`modelle.bin`, `katalog_verbaut.bin`, Katalog-Pakete) | 2 | 1 (`katalog_verbaut_S0.4.0.bin`) | `wandle_modelle.py`, `wandle_katalog.py`, `verbaut.py` | `GrafikBibliothek.Lesen` |
| `katalog_verbaut.json` | 1 (Feld `format`, seit A 0.62.1) | ohne Feld = 1 (`katalog_verbaut_S0.4.0.json`) | `verbaut.py` | `GrafikBibliothek.KatalogFormat`, `KatalogLaden` |
| `katalog.json`, `namen_de.json` (nur Werkzeug) | 1 | – | `wandle_katalog.py`, von Hand | `Katalog.Laden` |

Echte Testdateien liegen in `tests/Stadtbau.Tests/Testdaten/`. Für jedes Format prüft `tests/Stadtbau.Tests/EngineFormateTests.cs`:
eine neuere Nummer ist ein klarer Fehler (beim Projekt: nur ansehen, mit Hinweis), jede ältere Fassung liest sich.

## 5. Wohin gehört ein neues Merkmal (Regeln)

- **Daten nur angehängt**: neue Felder kommen als neuer Abschnitt in `stadtdaten` (oder ein neues Feld an einer Straße); ohne
  den Abschnitt verhält sich alles wie vorher. Keine neue Formatnummer, solange ältere Programme die Datei nicht falsch lesen.
- **Fassung 0 bitgleich**: jede Änderung muss für Fassung 0 dieselben Netze, Türen, Orte, Laternen und dasselbe Laufraster
  ergeben (Sollwerte unten). Ist das nicht möglich, braucht es eine freigegebene Sollwert-Änderung.
- **Grafik nur angehängt** an `modelle.bin`; eine gesparte Fassung ist ein neues Modell mit Zusatz im Namen, gewählt über eine
  Tabelle (`Fahrzeugmodelle`).
- **Prüfregel je Merkmal**: im gemeinsamen Teil `Core/Stadtbau/StadtbauPruefung.<Thema>.cs`, je Regel ein roter und ein
  grüner Test; Regeln nur für das Werkzeug liegen in `src/Stadtbau.Core`.
- **Gegenprobe**: jeder Fix und jede neue Regel hat einen Test, der mit dem alten Verhalten rot wird (Schalter
  `…AltFuerTests`, `AsyncLocal`).
- **Kein neuer Zeichenaufruf** ohne Grund; neue Stücke kommen in vorhandene Netze. Jeder neue Aufruf muss in
  `StadtbauGrenzen.Draussen` mitgezählt werden.
- **Darstellung ≠ Spielstand**: Autos, Gestalten, Laternen, Ampeln, Tag/Nacht ändern nie einen Spielstand oder Fingerabdruck.

## 6. Grenzen und feste Sollwerte

- Grenzen je Bild (Gesamtansicht und Nahansicht): **160 000 Dreiecke / 40 Aufrufe**, nie lockern. Gemessen im Start-Test
  und live im Werkzeug (`GrenzenLive`, „Stadt zu teuer“ ist rot).
- Fingerabdrücke des Spiels: G43 `f2b59ce8`, Stadt `03d0a012`, Einkauf `185dee1b`, Kaufhaus `a3ec3cdf`.
- `StadtSzeneHash` und die Rechnerfest-Werte in `tests/Lernplattform.Tests/StadtbauFassungTests.cs` (je Stand eine Spalte).
- Fassung 0 (`stadt1_fassung0.fistadt`) und `aktiv.json` sind Sollwerte in `tests/Vergleich/SOLLWERTE.sha256` (eine neue Fassung braucht eine freigegebene Sollwert-Änderung).
- `modelle.bin`: alte Modelle bytegleich (`ModelleNurAngehaengt`).

## 7. Messpunkte der Stadt (seit A 0.62.1)

`Core/Stadtbau/StadtLast.cs` misst, was eine Stadt beim Bauen kostet: „Stadt bauen“ = `StadtSzene.Bauen` plus `Vorbereiten`
(alle Stücke, alle Netze) und „Netzdaten“ = Byte aller Netze, die in die Grafikkarte kommen (9 float je Ecke, ein uint je
Index; Stadt, Häuser, Kleingrün, Paket-Netz, Fernformen, Sätze des Instancing, Ampel-Lichter, jedes Netz einmal). Der
Start-Test des Spiels (`AutoMessung.Engine.cs`, alle Orte, Winter, Weihnachten) und des Werkzeugs
(`StadtbauMessung.StadtLast.cs`, Stadt 1, Nacht, Winter, Weihnachten) bauen die Szene dafür frisch. „Stadt bauen“ ist
einmalig und streng (`StadtLast.BauenGrenzeMs`), „Netzdaten“ hat die Grenze `StadtLast.NetzdatenGrenzeMb`. Beide Grenzen sind
Vorschläge bis zu Nicos Antwort (Meldung P12-107); Grenzen nie lockern.

## 8. Plan: eigenes Projekt `Lernplattform.Engine` (nur Plan, Bau erst nach Stadt 1)

Heute liegt die Engine in `Lernplattform.Core` (Namensräume `Spiel.Probe`, `Stadtbau`) und in `App/Spiel/StadtFlaeche*.cs`.
Ein eigenes Projekt ginge nicht ohne Umbau, weil 39 von 71 Engine-Dateien den Spiel-Kern lesen (`Stadt`, `Spielstand`,
`Spielablauf`, `Py`, `PyObjekt`, `Festzeit`, `Jahreszeit`). Schritte, jeder für sich prüfbar:

1. **Gemeinsame Grundlagen herauslösen**: `Py`, `PyObjekt`, `PythonJson` und die Zeit-Typen (`Festzeit`, `Jahreszeit`) in ein
   kleines Projekt `Lernplattform.Grundlagen`, das Core und Engine beide nutzen. Keine Änderung am Verhalten.
2. **Schmale Schnittstelle zum Spiel-Kern**: was die Engine vom Spielstand braucht (sichtbare Orte, eigene Firma, Zuhause,
   Uhr), als Schnittstelle `ISpielSicht` im Engine-Projekt; der Spiel-Kern liefert sie. Die Engine liest nie mehr direkt
   `Spielstand`.
3. **Projekt `Lernplattform.Engine`** mit `Spiel/Probe` und `Stadtbau`; die Trennlinie (`EngineGrenzenTests`) prüft dann den
   Verweis auf Projektebene (die Engine kennt `Lernplattform.Core` nicht mehr).
4. **Zeichnen** (`StadtFlaeche*.cs`) bleibt in der App, nutzt nur die Engine.

Prüfung je Schritt: alle Tests, Fingerabdrücke, `StadtSzeneHash`, Fassung 0 bitgleich, Start-Test. Risiko: großer Diff ohne
sichtbaren Gewinn für Nico; darum erst nach Stadt 1 und nur mit eigener Weisung.
