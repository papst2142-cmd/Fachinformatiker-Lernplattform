# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 Fachrichtungen, 10 Inhalte-Runden 2–4 je Fachrichtung (A 0.28 bis 0.36), 11 NVDA und Gesamttest, 12 Spielmechanik Charakter, 13 Stadtbau-Werkzeug (eigenes Programm außerhalb der Lernplattform, nur Windows-PC und Android-Tablet: Nico baut Städte, Häuser und Innenräume selbst, Anzeige der Grenzen Dreiecke/Aufrufe, Export-Datei, die CC ins Spiel einbaut und prüft; die heutige Stadt bleibt geschützt; Plan kommt mit eigener Weisung), danach Beta 1 mit drei neuen Städten aus dem Werkzeug; Nico 30.10.2026, Plan Phase 9 Abschnitt 12.6, Weisung P9-11). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); Phase 6 (Verteilung) abgenommen (Weisung P6-21); Phase 7 (Spiel in 3D) und Phase 8 (Spiel-Abgleich und Kleinigkeiten) abgenommen; Phase 9 und Phase 10 abgenommen; Phase 11 (Barrierefreiheit, Fehlerwellen, Gesamttest) abgenommen (Nico 05.11.2026 08:41 MEZ); **Phase 12 (Spielmechanik Charakter) abgenommen** (Nico 05.11.2026 18:37 MEZ, Plan `Fi-Austausch/anlagen/P12-4_Plan_Phase12.md`, Bericht `Fi-Austausch/anlagen/P12-Bericht_Phase12.md`).
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | Im Bau **A 0.43.6 / S 0.9.1** (Farbverlauf je Ecke: Wände ohne Flecken in X-Form, im Spiel und im Werkzeug; Grafik-Format FLGM Version 2). Auf `main` **A 0.43.5** / **S 0.9.0** (PR #120, main 879619e). Releases macht Chat; CC startet `release.yml` und `stadtbau_release.yml` nie |
| Phase | 0–12 abgenommen (Phase 12: Nico 05.11.2026 18:37 MEZ, Welle 1 Kleiderschrank A 0.40.0, Welle 2 Menüs in der Spielwelt A 0.41.0; Phase 11: Nico 05.11.2026 08:41 MEZ). **Phase 11**: W1, FW1–FW10, W2, W3, Mac-Welle, Fehlerberichte, Patch-Welle, Seitenleiste, CI schneller und Handy-Update gemergt (A 0.32.0 bis 0.39.0, PR #84 bis #103); Gerätetest auf allen 5 Geräten (04.11.2026), NVDA und TalkBack in Ordnung; 0 offene F |
| Jetzt | **Welle A 0.43.6 / S 0.9.1 Farbverlauf** (Weisung P12-50, Nico Frage 1 = c, Sammelliste 230), Meldung P12-35. Danach: Nico baut Stadt 1 → W4b A 0.44.0 (neuer Zweig ab `main`) → W5 Etagen → Bericht Phase 13 |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`, Vorladen und Gesamtansicht mit allen Orten `AutoMessung.P8W12Bau.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Sollwerte | G1–G52, Testdaten, `inhalte/`, fünf Zeichen-Tabellen und `einteilung_bisher.json` fest, dazu seit Phase 13 die Stadt-Fassung 0 (92 Dateien mit Prüfsumme); die CI erzeugt nichts mehr aus Python, nur die drei Live-Proben mit Python 0.63.2 (Sicherungen für die alte App) bleiben bis 1.0. G52 entsteht bei Inhalte-Wellen mit `pruefe_lerninhalte.py` ohne Python-Linie. Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P12-35** (Welle A 0.43.6 Farbverlauf) / P12-36 |
| Sammelliste | nächste freie Nummer **231** (230 Flecken an Wänden, erledigt mit A 0.43.6; 229 Funde nennen Gebäude ohne Ort mit dem Modellnamen „bau:…“; 228 gewählter Setzen-Knopf links nicht markiert, später Optik Werkzeug; 227 Intel-Mac Lernteil zweimal vom Wachhund beendet, beobachten; 226 Wachhund `pakete (osx-x64)` in W4b; 225 in W4c erledigt; 224 Artefaktname `stadtbau-android-S-…` in `ci.yml` fest; 223 in W4a erledigt; 222 Katalog-Namen deutsch; 221 Emulator-Probe Update langsam, beobachten; 220 Laterne 20 im späten Spielstand, Nico verschiebt sie in W4b; 215 Möbelhaus größer/sortenrein, 216 Elektromarkt obere Reihe nicht erreichbar, beide in W4b mit Nicos Datei); 214 (Ladezeit S24 und Tab A9+) unter „Offen bis 1.0“, 122/123 K |
| Stoppgrenzen | siehe `CLAUDE.md` Abschnitt „Stoppgrenzen“ (Regel A und Spiel) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` (Block „Routinen“ zuerst) und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
