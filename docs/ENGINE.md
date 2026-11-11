# Die eigene Engine (Spiel und Stadtbau-Werkzeug)

Stand A 0.62.1 / S 0.37.1 (Weisung P12-175 „Engine festigen“ Stufe 1, Weisung P12-176 Stufe 2 Teil 1). Dieses Blatt erklärt, woraus die 3D-Stadt besteht,
wie Daten vom Werkzeug ins Spiel kommen und welche Regeln für jede Änderung gelten. Einzelheiten stehen im Code
(Kommentare am Anfang jeder Datei) und in `CLAUDE.md`; hier stehen nur Überblick und Verweise.

## 1. Was zur Engine gehört

Die Engine ist der Teil, den **Spiel und Werkzeug gemeinsam** nutzen. Das Werkzeug ist ihr Editor. Seit A 0.63.0 /
S 0.38.0 ist sie ein eigenes Projekt (Abschnitt 8).

| Ort | Inhalt |
|---|---|
| `src/Lernplattform.Engine/Spiel/Probe/` | Szene, Netze, Modelle, Stadt, Innenräume, Verkehr, Licht (Namensraum `Lernplattform.Core.Spiel.Probe`) |
| `src/Lernplattform.Engine/Stadtbau/` | Stadt-Datei und Fassungen, Prüfregeln, Grenzen, Bauteile, Etagen (Namensraum `Lernplattform.Core.Stadtbau`) |
| `src/Lernplattform.App/Spiel/StadtFlaeche*.cs` | Zeichnen mit OpenGL (Hochladen, Sichtkegel, Aufrufe) |
| `src/Lernplattform.Grundlagen` | `Py`, `PyObjekt`, `PythonJson`, Python-Rechenregeln, Zeit-Typen, `ISpielSicht`; Engine und Spiel-Kern nutzen sie |

Die Namensräume blieben beim Umzug gleich (E1): so musste keine Datei außerhalb der Engine geändert werden, und die
Sollwerte, Fingerabdrücke und Formate bleiben unberührt.

Nicht zur Engine gehören der Spiel-Kern (`Core/Spiel/*.cs`: Regeln, Spielstand, Ereignisse), der Lernteil und das
Werkzeug selbst (`src/Stadtbau.*`). Die Engine kennt den Spiel-Kern nicht: was sie von ihm braucht, holt sie über
`SpielKern.Regeln` (Schnittstelle `ISpielRegeln`), die Sichten `ISpielSicht` (Spielstand) und `IStadtSicht` (Stadt) und
`ISortiment` (Geschäfte). Der Spiel-Kern meldet seine Regeln beim Laden von `Lernplattform.Core` an
(`SpielKernRegeln.Anmelden`, Modul-Start; `Stadtbau.Core` ruft sie beim eigenen Modul-Start ebenfalls). Die Trennlinie
prüft `tests/Lernplattform.Tests/EngineGrenzenTests.cs` auf Projektebene (Verweise der `.csproj`, Verweise der DLL, jeder
Typ im richtigen Projekt, Gegenprobe Engine→Core rot) und im IL (kein Namensraum des Lernteils, nichts aus `Stadtbau.*`).

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
- **Gelände** (`Gelaende.cs`, seit A 0.66.0): `stadtdaten.gelaende` mit Hügeln (`huegel`: Mitte, Radius 1–3, Höhe 0,2–1,5,
  rund oder länglich bis 6, Drehung in 15 Grad) und gemalten Höhen (`gemalt`: `[i, j, Hundertstel]` im Raster 0,5). Höhe =
  größter Wert aus Kuppen und Gemaltem, höchstens 1,5. Ein Gitter im Stadtnetz (Fernform Raster 1,0 ab `FernAb`), kein eigener
  Aufruf. Stücke außerhalb der Häuser stehen auf der Höhe ihrer Mitte und kippen nicht, Kleingrün nur, wo der Fuß bis 0,02 eben
  ist; Figur und Kamera folgen der Höhe (`StadtSzene.BodenHoehe`). Laufraster gleich, nur Hänge steiler als 45 Grad gesperrt.
  Ohne Abschnitt (Fassung 0) bitgleich. Regeln `StadtbauPruefung.Gelaende.cs` (Rand `Gelaende.Saum` 1,5, höchstens 16 Hügel
  und 6 000 Dreiecke).
- **Innenräume und Etagen** (`Innenraum.cs`, `StadtSpiel.Etagen.cs`, `Engine/Stadtbau/Etagen.cs`, `InnenGrundriss.cs`).
- **Licht und Zeit** (`Tageslicht.cs`, `Nachtlicht.cs`, `Jahreszeitfarben.cs`, `FestDeko.cs`): Tag/Nacht, 22 Laternen,
  Festzeiten.
- **Modelle** (`Grafik.cs`: `GrafikBibliothek`, `GrafikModell`): alle Grafik im Format FLGM (Abschnitt 4), nur CC0.
- **Katalog** (`Katalogstueck.cs`; Katalog selbst nur im Werkzeug `src/Stadtbau.Core/Katalog.cs`): Stücke `cc0:paket/name`;
  ins Spiel kommen nur verbaute Stücke (`katalog_verbaut.bin`).
- **Eigene Gebäude** (`Engine/Stadtbau/Bauteile.cs`): Teile aus dem Gebäude-Editor werden zu einem Modell zusammengesetzt.
- **Prüfregeln** (`Engine/Stadtbau/StadtbauPruefung*.cs`): jede Regel ein klarer roter (oder gelber) Fund in einfachen Worten.
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
5. **Quests und Questgeber** (seit A 0.65.0, Welle Q2): `stadtdaten.quests` und `stadtdaten.questgeber` legt
   `Stadtbau/Questebene.cs` beim Bau der Stadt (`StadtbauSpiel.StadtFuer`) über den Spielinhalt: bewegte Personen an
   derselben Stelle der Liste, neue Personen und neue Quests hinten angehängt (Reihenfolge der alten bleibt, Spielstände
   rechnen gleich), umgehängte Quests nur mit neuem Auftraggeber/Ort. Ohne die Abschnitte bleibt der Inhalt dasselbe Objekt.
   Ein roter Fund wirft `StadtbauFehler` (nie still weg); nur Werkzeug-Ansichten bauen `nachsichtig`. Questgeber draußen
   zeichnet `StadtSzene` im Netz des Hauses ihres Orts (`QuestgeberJeHaus`: Ort mit dem nächsten Standplatz; Sichtkegel
   und Fernform wie das Haus, kein neuer Aufruf; ohne Haus im Paket-Netz), drinnen stehen sie in `Innenraum` neben den
   Verkäufern.

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
- **Prüfregel je Merkmal**: im gemeinsamen Teil `Engine/Stadtbau/StadtbauPruefung.<Thema>.cs`, je Regel ein roter und ein
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

`Engine/Stadtbau/StadtLast.cs` misst, was eine Stadt beim Bauen kostet: „Stadt bauen“ = `StadtSzene.Bauen` plus `Vorbereiten`
(alle Stücke, alle Netze) und „Netzdaten“ = Byte aller Netze, die in die Grafikkarte kommen (9 float je Ecke, ein uint je
Index; Stadt, Häuser, Kleingrün, Paket-Netz, Fernformen, Sätze des Instancing, Ampel-Lichter, jedes Netz einmal). Der
Start-Test des Spiels (`AutoMessung.Engine.cs`, alle Orte, Winter, Weihnachten) und des Werkzeugs
(`StadtbauMessung.StadtLast.cs`, Stadt 1, Nacht, Winter, Weihnachten) bauen die Szene dafür frisch. „Stadt bauen“ ist
einmalig und streng (`StadtLast.BauenGrenzeMs`), „Netzdaten“ hat die Grenze `StadtLast.NetzdatenGrenzeMb`. Beide Grenzen sind
Vorschläge bis zu Nicos Antwort (Meldung P12-107); Grenzen nie lockern.

## 8. Eigenes Projekt `Lernplattform.Engine` (gebaut mit A 0.63.0 / S 0.38.0)

Aufbau der Verweise (keiner rückwärts):

```
Lernplattform.Grundlagen  ←  Lernplattform.Engine  ←  Lernplattform.Core  ←  Lernplattform.App / Stadtbau.Core
```

1. **Grundlagen**: die Dateien bleiben an ihrem Ort in `src/Lernplattform.Core/` (Stoppgrenzen und Sollwert-Pfade bleiben
   gleich); `src/Lernplattform.Core/Grundlagen.props` nennt sie, `Lernplattform.Grundlagen.csproj` übersetzt sie,
   `Lernplattform.Core.csproj` nimmt sie heraus. Eine neue Grundlage nur dort eintragen und in `EngineGrenzenTests.Grundlagen`.
2. **Schnittstelle**: `ISpielRegeln` (in `SpielKern.cs`) mit den Regeln, die die Engine vom Spiel-Kern braucht; Feste Namen
   und Zahlen (Läden, Schalter, Möbel-Art, Reichweiten) stehen in `SpielKern`, der Spiel-Kern leitet seine gleichnamigen
   Werte dorthin weiter (gleiche Werte). Eine neue Regel = neues Mitglied in `ISpielRegeln`, Umsetzung in
   `Core/Spiel/SpielKernRegeln.cs` (nur weiterreichen).
3. **Projekt** `src/Lernplattform.Engine` (`Spiel/Probe`, `Stadtbau`). Was die Engine an das Spiel bindet, blieb im
   Spiel-Kern: `StadtSpiel`, `HinweisWahl`, `Tastenbelegung`, `Stadtbau/StadtbauSpiel.cs` (Probestand, Stadt aus Fassung,
   bewegte Teile der Grenzen).
4. **Zeichnen** (`StadtFlaeche*.cs`) bleibt in der App.

Nachweis: Fingerabdrücke, `StadtSzeneHash` und Rechnerfest-Werte, `SOLLWERTE.sha256`, Fassung 0 und Testdateien bitgleich,
Spiel- und Werkzeug-Fotos pixelgleich vorher/nachher.

## 9. Entscheidung: kein Streaming (Nico 11.11.2026 06:35 MEZ)

Die Stadt wird ganz geladen, nicht in Zellen nachgeladen. Gemessen (A 0.62.1): Netzdaten rund **19 MB** (Grenze 48 MB),
„Stadt bauen“ im Spiel rund 0,15 s, im Werkzeug bis rund **0,8 s** (Windows; Grenze 2 s). Beides liegt weit unter den
Grenzen. **Erst bei einer großen durchgehenden Welt (mehrere Städte ohne Zug dazwischen) neu fragen.**
