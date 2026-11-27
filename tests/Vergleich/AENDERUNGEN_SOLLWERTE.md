# Änderungen der Sollwerte (tests/Vergleich)

Seit der Welle Sammelliste 217 (Weisung P12-14, Nico 05.11.2026 20:52 MEZ, Antworten 1a 2a 3a 4a 5a) sind die Dateien in `tests/Vergleich/` feste Sollwerte „Stand C# A 0.41.1“ (main 71de14c). Die CI erzeugt sie nicht mehr aus der Python-Linie v0.63.2, sondern prüft sie über `SOLLWERTE.sha256` (Test `SollwerteTests`, Job `sollwerte`).

Regeln:
- Ein Sollwert ändert sich nur mit Nicos Freigabe (Frage in einer Meldung mit vorher/nachher und Grund, Antwort als Weisung).
- Jede Änderung bekommt hier einen Abschnitt `## Nr. <n>` (fortlaufend, ohne Lücke); die Zeile `# Aenderung: <n>` in `SOLLWERTE.sha256` ist gleich der höchsten Nummer hier, sonst ist `SollwerteTests` rot.
- `SOLLWERTE.sha256` wird nur mit `python3 -I werkzeuge/vergleich/sollwerte.py schreiben <n>` geschrieben, nie von Hand.
- Die Nummern 1 bis 11 sind die früheren „festen Ausnahmen von Python“. Ihre Vergleichsdateien zeigen weiter das Verhalten von Python v0.63.2; das C#-Verhalten prüft jeweils der genannte Test (Nico 2a). Sie sind erledigt.

## Nr. 1
- Was: Gerätename nur „PC“ oder „Handy“, kein Rechnername (Sammelliste 24b).
- Freigabe: Weisung P11-10 (FW5a). Test: `GeraetetexteTests.Geraet_OhneRechnername_AusnahmeVonPython`, `Phase11FW5bTests.Hilfe_GeraetOhneRechnernamen_FesteAusnahmeUeberPaare`.

## Nr. 2
- Was: Kalender „Lerntage“ zählt Tage mit Rechen-Trainer mit (Sammelliste 33a).
- Freigabe: Weisung P11-10 (FW5a). Test: `TrainerSpeichernTests.DashboardUndFortschrittZaehlenMit`.

## Nr. 3
- Was: Exec-Zeile der Linux-Verknüpfung maskiert `\` und `%` nach der Desktop-Entry-Spezifikation (Sammelliste 76a).
- Freigabe: Weisung P11-10 (FW5a). Test: `VerknuepfungTests.ExecZeileMaskiert_AusnahmeVonPython`.

## Nr. 4
- Was: Sicherheitskopie bei gesperrter `einstellungen.json` ohne erfundenes Tagesziel (Sammelliste 67).
- Freigabe: Weisung P11-8 (FW3). Test: `WiederherstellungTests.KopieOhneErfundenesTagesziel`.

## Nr. 5
- Was: Sicherung ohne Tagesziel lässt beim Ersetzen das eigene Tagesziel stehen (Sammelliste 189).
- Freigabe: Weisung P11-9 (FW4). Test: `WiederherstellungTests.SicherungOhneTageszielBehaeltEigenes`.

## Nr. 6
- Was: Gleichnamige Geräte zählen in der Stadt-Uhr als ein Gerät (Sammelliste 118b).
- Freigabe: Weisungen P11-15/P11-16 (FW8). Test: `SpielAbgleichTests.TagesuhrGleichnamigeGeraeteSindEinGeraet_Ausnahme`.

## Nr. 7
- Was: Ergebniskasten des Rechners nur in der Anzeige: große Zahl nie zerteilt, Einheit bei der Zahl (FW7, Sammelliste 41/43); `Zeilen.Kasten` bleibt wie Python (G24).
- Freigabe: Weisungen P11-13/P11-14 (FW7). Test: `ZeilenAnzeigeTests` (Tests `…_AusnahmeVonPython`).

## Nr. 8
- Was: Dashboard zählt bei AE/DPA/DV nur die Sicht der Fachrichtung, SI wie Python (Sammelliste 169b).
- Freigabe: Weisung P11-11 (FW5b). Test: `Phase11FW5bTests`.

## Nr. 9
- Was: Bereinigung alter Fehlbuchungen aus AE/DPA/DV-Klausuren mit Kopie `vor_bereinigung_*` (Sammelliste 186b).
- Freigabe: Weisung P11-11 (FW5b). Test: `Phase11FW5bTests`.

## Nr. 10
- Was: Firmen-Tafel ohne Reiter „kredite“, Kredite nur am Bankschalter (Phase 12, P12-3); G47 und `Betrieb.Reiter` bleiben wie Python.
- Freigabe: Plan Phase 12 (Weisung P12-5, Nico 1a). Test: `SpielSpielweltTests.Firma_ReiterOhneKredite_AusnahmeVonPython`.

## Nr. 11
- Was: Der alte Kleiderschrank (340 €) steht in keinem Laden mehr, dafür die vier neuen Schränke im Möbelhaus (Phase 12 Welle 1, Nico 4a).
- Freigabe: Plan Phase 12 (Weisung P12-5). Test: `SpielGeschaefteTests.InhaltOhneFehler`.

## Nr. 12
- Was: Start der festen Sollwerte (Welle Sammelliste 217). Neu `quiz_python.json` (Fragetexte von `fisi_core.QUIZ_QUESTIONS` v0.63.2, einmalig mit `werkzeuge/vergleich/erzeuge_quiz_python.py` erzeugt); `pruefe_lerninhalte.py` (G52) liest sie statt der Python-Linie und erzeugt `lerninhalte_pruefung.json` bytegleich. Kein Sollwert hat sich im Inhalt geändert.
- Freigabe: Weisung P12-14 (Nico 05.11.2026 20:52 MEZ, 1a 2a 3a 4a 5a). Test: `SollwerteTests`.

## Nr. 13
- Was: Neu geschützt die Stadt-Fassungen in `inhalte_csharp/spiel/stadtbau/` (Phase 13 Welle 1 „Stadt aus Daten“): `stadt1_fassung0.fistadt` (Stadt 1 bis A 0.41.1: Karte aus `inhalte/spiel/orte.json`, `stadt.json`, `innenraeume.json` unverändert, dazu die 22 Laternenplätze und das Beet, die vorher fest im Programm standen) und `aktiv.json` (Stadt 1 → Fassung 0). Vorher: keine Datei, die Szene kam aus den drei Dateien und den festen Werten. Nachher: das Spiel zeichnet aus der Fassung; Szene-Prüfsumme in allen Jahres- und Festzeiten, alle Innenräume und alle Fingerabdrücke unverändert. Kein bisheriger Sollwert hat sich geändert.
- Freigabe: Plan Phase 13 (Weisung P12-16, Nico 05.11.2026 22:22 MEZ, 1a 2a 3a 4a 5a). Test: `StadtbauFassungTests`.

## Nr. 14
- Was: G52 `lerninhalte_pruefung.json` nach der SI-Inhalte-Welle A 0.53.0 (1 019 Karteikarten und 1 561 Prüfungsfragen für Systemintegration, angehängt in `inhalte_csharp/lernen/`). Vorher: Sicht SI 1 565 Karteikarten und 2 174 Prüfungsfragen. Nachher: 2 584 und 3 735 (gleich AE, DPA und DV). Sonst keine Zeile geändert; `inhalte/` unverändert.
- Freigabe: Weisung P12-97 (Nico: „Erweitere bitte den Aufgaben Pool für Fisis so das sie genau so viele Aufgaben haben wie die anderen Fachinformatiker“, gilt als Freigabe der Sollwert-Änderung Nr. 14). Test: `SollwerteTests`, `LerninhalteTests`.

## Nr. 15
- Was: `inhalte_csharp/spiel/stadtbau/aktiv.json` bekommt eine Formatnummer (Engine festigen Stufe 2 Teil 1, Sammelliste 286). Vorher: `{"_hinweis": [...], "stadt1": 0}` ohne Formatnummer; ein späteres Programm hätte eine neuere Fassung dieser Datei nicht als neuer erkannt. Nachher: zusätzlich die Zeile `"format": 1` vor `"stadt1"`; Stadt 1 gilt weiter mit Fassung 0. Das Spiel liest eine Datei ohne Feld als Format 1 (Dateien bis A 0.62.0 bleiben lesbar, echte Testdatei `tests/Stadtbau.Tests/Testdaten/aktiv_A0.62.0.json`), eine größere Nummer ist ein klarer Fehler. Fassung 0, Szene, Fingerabdrücke und alle anderen Sollwerte unverändert. Gleich dazu (kein Sollwert) bekommt `katalog_verbaut.json` die Nummer über `werkzeuge/stadtbau/verbaut.py`.
- Freigabe: Weisung P12-176 (Chat Frage 2 = a aus Meldung P12-106; bauen frei, gemergt erst nach Nicos OK, Frage in Meldung P12-107). Test: `SollwerteTests`, `EngineFormateTests.AktivJson_NeuereNummerFehler_EchteDateiOhneNummerLesbar`, `StadtbauFassungTests.AktivNenntFassung0`.

## Nr. 16
- Was: Stadt 1 von Nico wird im Spiel eingebaut (W4b). `inhalte_csharp/spiel/stadtbau/aktiv.json`: vorher `"stadt1": 0`, nachher `"stadt1": 1`. Neu dazu die Datei `inhalte_csharp/spiel/stadtbau/stadt1_fassung1.fistadt` = Nicos Stadt-Datei (`fi-fehlerberichte` main 2c9be3e, `stadtbau/2026-11-13_064738_PC_S0.47.0.fistadt`, Kopie `tests/Stadtbau.Tests/Testdaten/nico/stadt1_S0.47.0.fistadt`) mit dem Kopf Stadt `stadt1`, Fassung 1, Grundlage 0 und der Uhr 1 Minute je Schritt (vorher 2, Nico Frage 2 = a); alles andere bytegleich zu Nicos Datei. Fassung 0 (`stadt1_fassung0.fistadt`) bleibt unverändert liegen und ist weiter die Grundlage des Werkzeugs; Fingerabdrücke, Szene-Sollwerte der Fassung 0 und alte Spielstände unverändert (jede Weg-Zeile trägt ihre Minuten).
- Freigabe: Weisung P12-228 (Nico 13.11.2026 06:53 MEZ, Antworten 1 und 2 = a); gemergt erst nach Nicos OK (Frage in Meldung P12-130). Test: `SollwerteTests`, `StadtbauFassungTests.AktivNenntFassung1_Fassung0BleibtDaneben`, `StadtbauFassung1Tests`.

## Nr. 17
- Was: Stadt 1 von Nico in Fassung 2 (Patch A 0.75.2). `inhalte_csharp/spiel/stadtbau/aktiv.json`: vorher `"stadt1": 1`, nachher `"stadt1": 2`. Neu dazu die Datei `inhalte_csharp/spiel/stadtbau/stadt1_fassung2.fistadt` = Nicos Stadt-Datei (`fi-fehlerberichte` main c08f44d, `stadtbau/2026-11-14_182607_PC_S0.53.1.fistadt`, Kopie `tests/Stadtbau.Tests/Testdaten/nico/stadt1_S0.53.1.fistadt`) mit dem Kopf Stadt `stadt1`, Fassung 2, Grundlage 0 (Nicos Datei trug Fassung 1, Grundlage 0; Grundlage bleibt 0 wie bei Fassung 1, sonst lehnt das Werkzeug die Datei als fremd ab) und neu gerechneter Prüfsumme; alles andere bytegleich zu Nicos Datei (Uhr schon 1 Minute je Schritt). Fassung 0 und Fassung 1 bleiben unverändert liegen; Fingerabdrücke und alte Spielstände unverändert.
- Freigabe: Weisung P12-263 (Nico 14.11.2026, „Das ist die Stadt 1. Version 2.“); gemergt erst nach Nicos OK (Meldung P12-153). Test: `SollwerteTests`, `StadtbauFassungTests`, `StadtbauFassung2Tests`.

## Nr. 18
- Was: G52 `tests/Vergleich/lerninhalte_pruefung.json` (Welle W3 Prüfung im echten Format, A 0.79.0). Vorher gab es in G52 keinen Schlüssel für die Prüfungssätze; nachher steht dort der neue Schlüssel `pruefungssaetze` (Format, Fehlerliste, je Satz Fachrichtung, Bereich, Firma, Punkte und Teile je Aufgabe, Arten der Anlagen, Antwortformen, Operatoren, Punkte selbst/Programm; dazu je Fachrichtung die Sicht AP1/Bereich 1/Bereich 2), unabhängig in Python gerechnet von `werkzeuge/vergleich/pruefe_lerninhalte.py`. Inhalt: in `inhalte_csharp/lernen/pruefungssaetze.json` 15 neue Sätze nur angehängt (AP1 `alle_ap1_02` bis `06`, Konzeption `si_b1_02` bis `06`, Netzwerke `si_b2_01` bis `05`), die beiden Probesätze unverändert. Alle anderen Schlüssel von G52 unverändert.
- Freigabe: gehört zur Inhalte-Freigabe der 15 Sätze durch Nico (Weisung P12-267, Meldung P12-157); gemergt erst nach Nicos OK. Test: `SollwerteTests`, `EchtInhalteW3Tests` (G52 gleich der C#-Ladeprüfung, Gegenprobe mit verdorbenem Satz).

## Nr. 19
- Was: G52 `tests/Vergleich/lerninhalte_pruefung.json` (Prüfung im echten Format für AE, A 0.84.0). Im Schlüssel `pruefungssaetze` kommen unter `saetze` die 10 neuen Sätze hinzu (`ae_b1_01` bis `05` Planen eines Softwareproduktes, `ae_b2_01` bis `05` Entwicklung und Umsetzung von Algorithmen, nur angehängt in `inhalte_csharp/lernen/pruefungssaetze.json`), und die Sicht der Fachrichtung `ae` nennt sie unter `b1` und `b2` (vorher leer = Platzhalter). Vorher/nachher: die 17 bisherigen Sätze, die Sichten `si`, `dpa`, `dv`, die AP1-Sicht von `ae`, die Fehlerliste (leer) und alle anderen Schlüssel von G52 sind gleich, unabhängig in Python gerechnet von `werkzeuge/vergleich/pruefe_lerninhalte.py`.
- Freigabe: gehört zur Inhalte-Freigabe der 10 Sätze durch Nico (Weisung P12-285, Meldung P12-170); gemergt erst nach Nicos OK. Test: `SollwerteTests`, `EchtInhalteW3Tests` (G52 gleich der C#-Ladeprüfung, Gegenprobe mit verdorbenem Satz), `EchtInhalteAeTests`.

## Nr. 20
- Was: G52 `tests/Vergleich/lerninhalte_pruefung.json` (Prüfung im echten Format für DPA, A 0.85.0). Im Schlüssel `pruefungssaetze` kommen unter `saetze` die 10 neuen Sätze hinzu (`dpa_b1_01` bis `05` Durchführen einer Prozessanalyse, `dpa_b2_01` bis `05` Sicherstellen der Datenqualität, nur angehängt in `inhalte_csharp/lernen/pruefungssaetze.json`), und die Sicht der Fachrichtung `dpa` nennt sie unter `b1` und `b2` (vorher leer = Platzhalter). Vorher/nachher: die 27 bisherigen Sätze, die Sichten `si`, `ae`, `dv`, die AP1-Sicht von `dpa`, die Fehlerliste (leer) und alle anderen Schlüssel von G52 sind gleich, unabhängig in Python gerechnet von `werkzeuge/vergleich/pruefe_lerninhalte.py`.
- Freigabe: gehört zur Inhalte-Freigabe der 10 Sätze durch Nico (Weisung P12-291, Meldung P12-173); gemergt erst nach Nicos OK. Test: `SollwerteTests`, `EchtInhalteW3Tests` (G52 gleich der C#-Ladeprüfung, Gegenprobe mit verdorbenem Satz), `EchtInhalteDpaTests`.

## Nr. 21
- Was: G52 `tests/Vergleich/lerninhalte_pruefung.json` (Prüfung im echten Format für DV, A 0.86.0). Im Schlüssel `pruefungssaetze` kommen unter `saetze` die 10 neuen Sätze hinzu (`dv_b1_01` bis `05` Diagnose und Störungsbeseitigung in vernetzten Systemen, `dv_b2_01` bis `05` Betrieb und Erweiterung von vernetzten Systemen, nur angehängt in `inhalte_csharp/lernen/pruefungssaetze.json`), und die Sicht der Fachrichtung `dv` nennt sie unter `b1` und `b2` (vorher leer = Platzhalter). Vorher/nachher: die 37 bisherigen Sätze, die Sichten `si`, `ae`, `dpa`, die AP1-Sicht von `dv`, die Fehlerliste (leer) und alle anderen Schlüssel von G52 sind gleich, unabhängig in Python gerechnet von `werkzeuge/vergleich/pruefe_lerninhalte.py`. Danach zeigt keine Fachrichtung mehr den Platzhalter.
- Freigabe: gehört zur Inhalte-Freigabe der 10 Sätze durch Nico (Weisung P12-292, Meldung P12-174); gemergt erst nach Nicos OK. Test: `SollwerteTests`, `EchtInhalteW3Tests` (G52 gleich der C#-Ladeprüfung, Gegenprobe mit verdorbenem Satz), `EchtInhalteDvTests`.

## Nr. 22
- Was: G52 `tests/Vergleich/lerninhalte_pruefung.json` (AE-Satzpaare, A 0.94.0). Im Schlüssel `pruefungssaetze` steht `format` 3 statt 2; unter `saetze` kommen die 10 neuen Sätze hinzu (`ae_b1_06` bis `10` Planen eines Softwareproduktes, `ae_b2_06` bis `10` Entwicklung und Umsetzung von Algorithmen, je Satz mit `aufbau` „paar“ und `gebiete`, nur angehängt in `inhalte_csharp/lernen/pruefungssaetze.json`); neuer Schlüssel `ausgeschlossen` nennt die 10 alten AE-Sätze `ae_b1_01` bis `ae_b2_05` (Einträge in `inhalte_csharp/lernen/ausschluss.json` mit Stelle und Kennung), und die Sicht der Fachrichtung `ae` nennt unter `b1` und `b2` nur noch die neuen Sätze (vorher `ae_b1_01` bis `05` und `ae_b2_01` bis `05`). Vorher/nachher: die 47 bisherigen Sätze, die Sichten `si`, `dpa`, `dv`, die AP1-Sicht von `ae`, die Fehlerliste (leer) und alle anderen Schlüssel von G52 sind gleich, unabhängig in Python gerechnet von `werkzeuge/vergleich/pruefe_lerninhalte.py`.
- Freigabe: gehört zur Inhalte-Freigabe der 10 Sätze durch Nico (Weisung P12-304, Meldung P12-186); gemergt erst nach Nicos OK. Test: `SollwerteTests`, `EchtInhalteW3Tests` (G52 gleich der C#-Ladeprüfung, Gegenprobe mit verdorbenem Satzpaar), `EchtInhalteAePaareTests`.

## Nr. 23
- Was: Sieben C#-Sollwerte der Spiel-Erzeuger auf den breiten Grundrissen (Welle A 1.2.0, Flure wirklich breiter: Diele 1,32 m, Flure mindestens 1,80 m, Türen mindestens 0,90 m; Daten `inhalte_csharp/spiel/grundrisse_breit.json`). Nachgerechnet mit den eingefrorenen Erzeugern `werkzeuge/vergleich/erzeuge_spiel_*.py` auf einer Kopie der Python-Linie v0.63.2, in die `werkzeuge/vergleich/breite_grundrisse_python.py` die breiten Grundrisse einsetzt (Probe vorher: dieselben Erzeuger ohne Einsetzen geben alle 17 Spiel-Dateien byte-gleich wieder). Alle übrigen Spiel-Dateien und der Fingerabdruck (G43 f2b59ce8) bleiben byte-gleich.
  - `spiel_inhalte.json` (G42): 33 Stellen; die Prüfsummen von `gebaeude`, `kollegen`, `kunden`, `wohnungen`, `firma` und `aufgaben` sowie von 27 Aufträgen mit einer Stelle im Raum (z. B. Auftrag `diagnose-automat-ssd`: `stelle` [4.9, 10.5] → [4.9, 11.1]).
  - `spiel_pruefung.json` (G42 Teil Prüfung): 1 Stelle; Fall „Start-Einrichtung Tuer“: vorher „Matratze passt hier nicht hin: Es muss ganz in einem Raum stehen.“, nachher „Matratze würde eine Tür versperren.“
  - `spiel_aktionen.json` (G44 Aktionen): 22 518 Stellen; die gespielten Läufe stellen Möbel im breiten Grundriss (Lauf 1: 615 → 609 Ereignisse, z. B. Start-Matratze y 0,25 → 0,5).
  - `spiel_firma.json` (G47): 989 Stellen; Grundrisse der Firmen- und Filialstufen (z. B. Filiale, Flur-Deko y 5,35 → 5,85).
  - `spiel_neuaufbau.json` (G44): 3 Stellen; Protokoll „lang“, Prüfsummen der vorbereiteten Aufträge in drei Ständen (Stellen im breiten Büro).
  - `spiel_orte.json` (G49): 37 246 Stellen; Grundrisse, Personen und Texte der Orte (z. B. Wohnung, Fenster y 10 → 10,7).
  - `spiel_wege.json` (G48 Wege): 123 260 Stellen; Raster, Punkte und Wege (z. B. Büro, freie Rasterpunkte 3 310 → 3 781).
- Grund: Nico wählt Weg B „Neue Grundrisse als Daten“ (21.11.2026 11:28 MEZ, Weisung P12-334) und „Ja, mit Sollwert Nr. 23“ (21.11.2026 15:29 MEZ, Weisung P12-335): die C#-Sollwerte prüfen die Inhalte, wie Spiel und Werkzeug sie wirklich lesen.
- Freigabe: Weisungen P12-334 und P12-335 (Nico), Sprachregel C#-Sollwerte P12-336; gemergt erst nach Nicos OK (Meldung P12-208). Test: `SollwerteTests`, `SpielInhaltTests`, `SpielPruefungTests`, `SpielAktionenTests`, `SpielFirmaTests`, `SpielNeuaufbauTests`, `SpielOrteTests`, `SpielWegeTests` (auf `SpielInhaltTests.Inhalt` mit breiten Grundrissen), Gegenprobe `SollwerteNr23Tests` (mit den alten, schmalen Grundrissen rot).

## Nr. 24
- Was: G52 `tests/Vergleich/lerninhalte_pruefung.json` (Prüfung echt W1, A 1.8.0). Im Schlüssel `pruefungssaetze` steht `format` 4 statt 3; fünf AE-Sätze haben eine neue Fassung (`"fassung": 2`, ganzer Satz noch einmal hinten angehängt in `inhalte_csharp/lernen/pruefungssaetze.json`, die alte Fassung bleibt bytegleich), je Satz neu der Schlüssel `fassung` 2 und geänderte Zählwerte: `ae_b1_06` (4a Use-Case-Diagramm statt Stakeholder-Tabelle: `formen` zeichnen 1 → 2, zeilen 12 → 11, `operatoren` ergaenzen 4 → 3, neu zeichnen 1, `gebiete.diagramm` 25 → 31), `ae_b1_07` (2c Klassendiagramm im Baukasten: zeichnen 1 → 2, zeilen 11 → 10), `ae_b1_08` (3a: zeichnen 1 → 2, zeilen 13 → 12), `ae_b1_09` (2c: zeichnen 1 → 2, zeilen 11 → 10), `ae_b1_10` (1b Use-Case-Diagramm statt Zuordnungstabelle: tabelle 2 → 1, neu zeichnen 1, ergaenzen 3 → 2, zeichnen 1 → 2, `gebiete.diagramm` 24 → 30, `punkte_selbst` 85 → 91, `punkte_programm` 15 → 9). Vorher/nachher: alle anderen Sätze, die Sichten, `ausgeschlossen`, die Fehlerliste (leer) und alle anderen Schlüssel von G52 sind gleich, unabhängig in Python gerechnet von `werkzeuge/vergleich/pruefe_lerninhalte.py` (dort neu die Regel der Fassung: ab Format 4, Kennung doppelt nur mit höherer Fassung, die höhere ersetzt die vorige an ihrer Stelle).
- Grund: Weisung P12-371 (Nico 24.11.2026, Plan P12-232 Antworten 1a bis 5a: Klassen- und Use-Case-Diagramm im Baukasten, fünf neue AE-Fassungen) und Weisung P12-372 (Chat: Frage 1 = a mit Bedingung, E1 neue Fassung mit `"fassung": 2`, Format 4).
- Freigabe: gehört zur Inhalte-Freigabe der fünf Fassungen durch Chat (Dauerregel Nico 24.11.2026 23:06, Anlage `anlagen/P12-239_W1_AE_Fassungen.md`); gemergt erst nach Freigabe. Test: `SollwerteTests`, `EchtInhalteW3Tests` (G52 gleich der C#-Ladeprüfung, Gegenproben mit verdorbenem Satz und verdorbener neuer Fassung), `EchtW1BaukastenTests`.

## Nr. 25
- Was: G52 `tests/Vergleich/lerninhalte_pruefung.json` (Prüfung echt W2 Struktogramm, A 1.12.0). Im Schlüssel `pruefungssaetze` steht `format` 5 statt 4; ein AE-Satz hat eine neue Fassung (`"fassung": 2`, ganzer Satz noch einmal hinten angehängt in `inhalte_csharp/lernen/pruefungssaetze.json`, die alte Fassung bleibt bytegleich), neu der Schlüssel `fassung` 2 und geänderte Zählwerte: `ae_b2_08` (2b Struktogramm im Baukasten: neu zeichnen 1, zeilen 13 → 12). Punkte, Operatoren, Gebiete und `punkte_selbst` bleiben gleich (zeilen und zeichnen bewertet man beide selbst). Vorher/nachher: alle anderen Sätze (auch `ae_b2_02`), die Sichten, `ausgeschlossen`, die Fehlerliste (leer) und alle anderen Schlüssel von G52 sind gleich, unabhängig in Python gerechnet von `werkzeuge/vergleich/pruefe_lerninhalte.py` (unverändert).
- Grund: Weisung P12-390 (Chat: Kurzplan P12-250 Prüfung echt W2, Antworten alle a, Auflagen A–H; Struktogramm als Baukasten mit sieben Bausteinen; E1 neue Fassung mit `"fassung": 2` wie in W1, Format 5). Weisung P12-393 (Nico 26.11.2026, Frage 1 = b): die zuerst mit angehängte Fassung von `ae_b2_02` 2a kam wieder heraus, weil `ae_b2_02` seit A 0.94.0 über `ausschluss.json` ausgeschlossen ist und in der App nie zu sehen war.
- Freigabe: Bild-OK Nico 26.11.2026 06:37 MEZ (Weisung P12-393, Merge und Release nach neuem grünen Lauf freigegeben). Test: `SollwerteTests`, `EchtInhalteW3Tests` (G52 gleich der C#-Ladeprüfung), `EchtStruktogrammTests`.

## Nr. 26
- Was: G9 `tests/Vergleich/beschriftungen.json` (Prüfungsbereich W1 Variante B, A 1.15.0), nur die Abschnitte `seitenleiste_pc`, `navigation_handy` und `lernen`; `navigation_pc` und alle anderen Abschnitte bleiben gleich (Seitenkatalog unverändert).
  - `seitenleiste_pc`: `logo` und `texte` gleich; neu `zeilen` je Fachrichtung (si, ae, dpa, dv) wie das Deckblatt: Start, Gruppe „ABSCHLUSSPRÜFUNG <Kürzel>“, Teil 1 · AP1, Teil 2 · AP2 mit den drei Bereichen der Fachrichtung (FISI Konzeption und Administration, Analyse und Entwicklung von Netzwerken; FIAE, FIDP, FIDV ihre Bereiche) und Abschlussprojekt, Gruppe LERNEN (Karteikarten, Notizblock, Rechner), Gruppe SONSTIGES (Spiel, Fortschritt); neu `unten` (Hilfe, Optionen) und `gruppen` (Muster der drei Gruppenköpfe). Vorher gab es diese Liste nicht im Sollwert (Leiste aus `navigation_pc`).
  - `navigation_handy`: vorher Start, Lernen, Rechner, Spiel, Fortschritt, Optionen; nachher Start, Teil 1, Teil 2, Lernen, Spiel, Mehr (Symbole ZIFFER_1, ZIFFER_2, EDIT_NOTE_ROUNDED, MEHR).
  - `lernen`: `kopf` gleich; `eintraege` vorher Karteikarten, Prüfungstrainer, AP1-Szenarien, AP2-Szenarien, Testprojekte, Mein Projekt, Notizblock; nachher Karteikarten, Notizblock, Rechner (Prüfungstrainer, Szenarien und Projekt erreicht man jetzt über Teil 1 und Teil 2); neu `mehr` (Kopf „MEHR · ÜBERSICHT“, Fortschritt, Optionen, Hilfe und die ganze Liste AP2-Szenarien, damit sie am Handy in zwei Klicks erreichbar bleibt) für den Knopf „Mehr“ am Handy.
- Grund: Weisung P12-406 (Nico, Antworten 1a bis 5a zum Kurzplan P12-261: Seitenleiste wie das Deckblatt plus LERNEN und SONSTIGES, Leiste am Handy Start, Teil 1, Teil 2, Lernen, Spiel, Mehr) und Weisung P12-407 (Chat: Sollwert-Änderung Nr. 26 nur für diese Abschnitte erlaubt).
- Freigabe: Weisung P12-406 (Nico) mit P12-407; gemergt erst nach Nicos Bild-OK (Meldung P12-262). Test: `SollwerteTests`, `BeschriftungenTests` (Leiste, Handy-Leiste, Lernen und Mehr gleich dem Programm), `SeitenleisteOptionenTests`, `PruefungsteileTests` (Deckblatt je Fachrichtung, jede bisherige Seite in höchstens zwei Klicks, Gegenprobe).

## Nr. 27
- Was: G9 `tests/Vergleich/beschriftungen.json` (Reiter B und Symbole Satz 3, A 1.17.0), nur die Abschnitte `navigation_handy`, `seitenleiste_pc`, `kopfleiste_handy` und `lernen`; alle anderen Abschnitte bleiben gleich.
  - `navigation_handy`: vorher Start, Teil 1, Teil 2, Lernen, Spiel, Mehr; nachher Start, Prüfung, Lernen, Spiel, Mehr (neue Kennung `pruefung`; Symbole aus Satz 3: START, PRUEFUNG, LERNEN, SPIEL, MEHR).
  - `seitenleiste_pc`: `zeilen` je Fachrichtung vorher Start, Gruppe „ABSCHLUSSPRÜFUNG <Kürzel>“, Teil 1 · AP1, Teil 2 · AP2 mit Bereichen und Abschlussprojekt, Gruppe LERNEN, Gruppe SONSTIGES; nachher Start, Gruppe „ABSCHLUSSPRÜFUNG <Kürzel>“, Hauptpunkt Prüfung mit den Unterpunkten AP1, AP2, den drei Bereichen der Fachrichtung und Abschlussprojekt, Hauptpunkt Lernen mit Karteikarten, Notizblock, Rechner, dann Spiel und Fortschritt. `gruppen` nur noch „ABSCHLUSSPRÜFUNG %s“ (LERNEN und SONSTIGES sind jetzt Hauptpunkte). Neu `symbole` (Symbol je Hauptpunkt: dashboard START, pruefung PRUEFUNG, learn LERNEN, game SPIEL, progress FORTSCHRITT, help HILFE, settings OPTIONEN, mehr MEHR, beenden BEENDEN). `logo`, `texte` und `unten` gleich.
  - `kopfleiste_handy`: Symbol des Knopfs Hilfe vorher HELP_OUTLINE_ROUNDED, nachher HILFE (Wegweiser aus Satz 3); Titel und Lupe gleich.
  - `lernen`: in `mehr` der Satz zur Liste AP2-Szenarien „… aus allen Bereichen von Teil 2“ → „… von AP2“; dazu (Weisung P12-432, Nico 2b) hinten angehängt die Einträge „Testprojekte“ (`testproject`) und „Mein Projekt“ (`abschluss`), damit beide am Handy wieder in zwei Klicks erreichbar sind; alles andere gleich.
  - Startseite, Feld „Deine Abschlussprüfung“ (Weisung P12-432, Nico Frage 3 „jetzt mit beheben“): „TEIL 1 · AP1 · 20 %“ → „AP1 · 20 %“, „TEIL 2 · AP2 · 80 %“ → „AP2 · 80 %“. Diese Texte stehen in keiner Sollwert-Datei (nur `Pruefungsteile.StartTeil1/2`), darum ändert sich dafür keine Prüfsumme.
- Grund: Weisung P12-430 (Nico: Reiter B aus P12-268 und Symbole Satz 3 aus P12-271/272, Start = Sonnenaufgang; Sollwert-Änderung Nr. 27 für diese Beschriftungen freigegeben) und Weisung P12-432 (Nico 27.11.2026 11:21 MEZ: 2b, 3 jetzt).
- Freigabe: Weisung P12-430 (Nico); gemergt erst nach Nicos Bild-OK (Meldung P12-273). Test: `SollwerteTests`, `BeschriftungenTests` (Leiste, Handy-Leiste, Symbole und Mehr gleich dem Programm), `SeitenleisteOptionenTests.UntereLeisteWieNico`, `PruefungsteileTests` (Klickwege, Segment AP1/AP2), `SymbolSatzTests`.

## Nr. 28
- Was: L0 Aufräumen (A 1.18.0): Altbestand gelöscht, Python-Zuordnungen weg. Zwei Dateien ändern sich, drei freigegebene bleiben gleich.
  - G1 `tests/Vergleich/inhalte.json`: der Eintrag `umbenennungen.json` (56 alte Fragetexte, 3 Einträge) fällt weg, weil die Datei `inhalte/umbenennungen.json` gelöscht ist (Nico 2b). Die alten Fragetexte stehen nur noch in `src/Lernplattform.Core/Daten/alte_fragetexte.json` (gleicher Aufbau, nur für das einmalige Umschreiben des Lernstands beim Start, Weisung P12-437 1a). Alle anderen Einträge gleich.
  - G52 `tests/Vergleich/lerninhalte_pruefung.json`: (1) Fall `quiz_alter_text`: Meldung „… steht als alter Text in umbenennungen.json“ → „… in alte_fragetexte.json“. (2) `pruefungssaetze.ausgeschlossen`: vorher die 10 alten FIAE-Sätze `ae_b1_01`…`ae_b1_05`, `ae_b2_01`…`ae_b2_05`, nachher leer (`ausschluss.json` gibt es nicht mehr). (3) `pruefungssaetze.saetze`: diese 10 Sätze fallen weg; die Reihenfolge der übrigen folgt der Datei (FIAE: `ae_b2_06`, `ae_b2_07`, `ae_b2_09`, `ae_b2_10`, `ae_b1_06`…`ae_b1_10`, `ae_b2_08`, weil die älteren Fassungen gelöscht sind und die neuen allein hinten stehen); Inhalt je Satz gleich. (4) `sichten` (Karten, Fragen, Sätze je Fachrichtung) unverändert; die Sätze einer Sicht stehen jetzt nach Kennung geordnet, das ist genau die Reihenfolge davor.
  - `tests/Vergleich/uebernahme.json`, `tests/Vergleich/spiel_uebernahme.json`, `tests/Vergleich/rahmenplan_vorab.json`: freigegeben, aber unverändert (die Fälle der alten Python-Datenbank prüft C# nicht mehr; die Datei bleibt als Sollwert stehen).
- Grund: Weisung P12-424 (Nico 1b 2b 3a 4a 5a, Plan Meldung P12-269) und P12-436/P12-437: neue Fassung ersetzt die alte sofort (Löschliste `inhalte_csharp/lernen/geloescht.json`), `umbenennungen.json` und die Übernahme aus der alten Python-Datenbank entfallen.
- Freigabe: Weisung P12-424 (Nico, Sollwert-Änderung Nr. 28 für G1, G52, uebernahme.json, spiel_uebernahme.json, rahmenplan_vorab.json). Test: `SollwerteTests`, `DatenTests.InhalteGleichWiePython`, `EchtInhalteW3Tests.G52_Pruefungssaetze_GleichDerUnabhaengigenPruefung`, `LerninhalteTests` (Löschliste L1, Zahlen L2, L4), `EchtL0Tests`.

## Nr. 29
- Was: G9 `tests/Vergleich/beschriftungen.json` (Prüfungsbereich einheitlich, A 1.19.0), nur die Abschnitte `seitenleiste_pc` und `lernen`; alle anderen Abschnitte bleiben gleich (auch `kopfleiste_pc`: die Kopfzeilen der Seiten Prüfungstrainer, AP1-Szenarien und AP2-Szenarien bleiben im Katalog, die Seiten öffnen sich nur noch eingebettet in AP1 bzw. AP2).
  - `seitenleiste_pc`: `zeilen` je Fachrichtung (si, ae, dpa, dv) vorher unter „Prüfung“ AP1, AP2, die drei Bereiche und „Abschlussprojekt“ (`abschluss`), danach Lernen, Spiel, Fortschritt; nachher unter „Prüfung“ nur AP1, AP2 und die drei Bereiche (sie öffnen AP2 › Üben bei diesem Bereich), nach Fortschritt neu der Hauptpunkt „Mehr“ (`mehr`) mit den Unterpunkten „Testprojekte“ (`testproject`) und „Mein Projekt“ (`abschluss`). `logo`, `texte`, `unten`, `gruppen` und `symbole` gleich (das Symbol für `mehr` stand schon in `symbole`).
  - `lernen`: in `mehr` fällt der Eintrag „AP2-Szenarien“ (`scenarios`) weg; die Szenarien stehen jetzt unter AP2 › Üben je Bereich. Fortschritt, Optionen, Hilfe, Testprojekte und Mein Projekt gleich.
- Grund: Weisung P12-440 (Nico 27.11.2026 19:08 MEZ, Antworten 1a 2a 3a 4a 5a zum Entwurf P12-278: Prüfungsbereich einheitlich, PC-Hauptpunkt „Mehr“ mit Testprojekte und Mein Projekt, Bereich-Unterpunkte bleiben, AP2-Szenarien nur noch je Bereich).
- Freigabe: Weisung P12-440 (Nico 5a, Sollwert-Änderung Nr. 29 für `beschriftungen.json`); gemergt erst nach Nicos Bild-OK (Meldung P12-280). Test: `SollwerteTests`, `BeschriftungenTests` (Leiste und Mehr gleich dem Programm), `SeitenleisteOptionenTests.UntereLeisteWieNico`, `PruefungsteileTests` (Klickwege, Mehr).
