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
