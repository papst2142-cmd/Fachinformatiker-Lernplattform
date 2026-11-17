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
| Version | Auf `main` **A 0.87.0** / **S 0.56.1** (Inhaltspakete W1, Sammelliste 371: PR #208, main 9ff3030, vA0.87.0 veröffentlicht). |
| Phase | **0–13 abgenommen** (Phase 13 Stadtbau-Werkzeug: Nico 14.11.2026, Bericht `Fi-Austausch/anlagen/P13-Bericht_Phase13.md`). Stadt 1 von Nico ist im Spiel (seit A 0.75.2 Fassung 2). Gemergt seitdem: Prüfung im echten Format (A 0.76–0.79, AE ab A 0.84, DPA ab A 0.85, DV ab A 0.86), Kamera und Karte (A 0.80), Handy-X, Sitzen/Liegen (A 0.81), Markierung (A 0.81.1), Tausenderpunkt (A 0.81.2), Nachladen (A 0.82/S 0.54), Werkzeug-Nachladen und Stadtteil-Namen (A 0.83/S 0.55), Plätze und Bündel 361–365 (A 0.84.1/S 0.56.0), Inhaltspakete W1 (A 0.87). |
| Jetzt | Welle **A 0.88.0 / S 0.56.2** (Weisung P12-296 mit P12-295/P12-295b) auf Zweig `claude/project-thread-3z3t09`: Paket-Städte im Spiel (Fahrplan am Bahnhof, Zugfahrt als Ereignis `zug_gefahren`, Vorsatz `<kennung>:` der Orte, Entfernen gesperrt, zweites Gerät ohne Paket), Sammelliste 373 (Werkzeug: heutige Stadt mit Vorschau und „Übernehmen und speichern“ auf einen Platz setzen) und 374 (Paketgröße, „Neu laden“). Danach (je nur nach eigener Weisung): W3 A 0.89.0 / S 0.57.0 Werkzeug und echtes Laden, Kurzplan „Lern-Pakete je Fachrichtung“ (Nico 3b), Haltungen 352 mit 358, Werkstatt 355–357, Beta 1. |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`, Vorladen und Gesamtansicht mit allen Orten `AutoMessung.P8W12Bau.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Sollwerte | G1–G52, Testdaten, `inhalte/`, fünf Zeichen-Tabellen und `einteilung_bisher.json` fest, dazu seit Phase 13 die Stadt-Fassung 0 (92 Dateien mit Prüfsumme); die CI erzeugt nichts mehr aus Python, nur die drei Live-Proben mit Python 0.63.2 (Sicherungen für die alte App) bleiben bis 1.0. G52 entsteht bei Inhalte-Wellen mit `pruefe_lerninhalte.py` ohne Python-Linie. Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P12-176** (A 0.87.0) / nächste Meldung **P12-177** (A 0.88.0), Weisung P12-297, Sammelliste 375, Sollwert-Änderung Nr. 22 |
| Sammelliste | nächste freie Nummer **375** (offene Punkte siehe `CLAUDE.md` „Aktueller Stand“ und `Fi-Austausch/SAMMELLISTE_NEU.md`) |
| Stoppgrenzen | siehe `CLAUDE.md` Abschnitt „Stoppgrenzen“ (Regel A und Spiel) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` (Block „Routinen“ zuerst) und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
