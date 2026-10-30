# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 Fachrichtungen, 10 Inhalte-Runden 2–4 je Fachrichtung (A 0.28 bis 0.36), 11 NVDA und Gesamttest, 12 Stadtbau-Werkzeug (eigenes Programm außerhalb der Lernplattform, nur Windows-PC und Android-Tablet: Nico baut Städte, Häuser und Innenräume selbst, Anzeige der Grenzen Dreiecke/Aufrufe, Export-Datei, die CC ins Spiel einbaut und prüft; die heutige Stadt bleibt geschützt; Plan kommt mit eigener Weisung), danach Beta 1 mit drei neuen Städten aus dem Werkzeug; Nico 30.10.2026, Plan Phase 9 Abschnitt 12.6, Weisung P9-11). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); Phase 6 (Verteilung) abgenommen (Weisung P6-21); Phase 7 (Spiel in 3D) und Phase 8 (Spiel-Abgleich und Kleinigkeiten) abgenommen; **Phase 9 Fachrichtungen: Plan `Plan_Phase9_CSharp.md` freigegeben (Weisung P9-2), Nachtrag Abschnitt 12; W1 bis W4 gemergt, W5 Runde 1 DV + Umziehen (A 0.27.0) gebaut**.
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | Auf `main` **A 0.26.0** (W4 gemergt, PR #73, main 58cb01d). Zweig `claude/project-thread-z6dnku` **A 0.27.0** (W5). Release vA0.26.0 macht Chat; Release macht Chat nach Nicos Merge-OK, CC startet `release.yml` nie |
| Phase | 0–8 abgenommen. **Phase 9** (Plan `Plan_Phase9_CSharp.md`, Weisung P9-2): W1 Prüfstand A 0.23.1 und W2 Gerüst A 0.24.0 gemergt (Sicht je Fachrichtung, Lader `inhalte_csharp/lernen/`, Einteilung der bisherigen Inhalte per `einteilen.py`, Klausur b1/b2 AE/DPA/DV). W3 Runde 1 AE A 0.25.0 und W4 Runde 1 DPA + Optionen > Spiel A 0.26.0 gemergt. W5 Runde 1 DV A 0.27.0, danach Bericht Phase 9; Runden 2–4 = Phase 10 |
| Jetzt | W5 (Weisung P9-11): Runde 1 DV (300 Karteikarten, 370 Quizfragen, 330 AP2-Szenarien, 11 Themen `dv_*`, Gegenprüfung aller 1 000) und Umziehen im Spiel wie Python (Karte „Wohnung“ beim Einrichten, Rückfrage, Kauf oder Miete nach Optionen > Spiel; `StadtSpiel.Umziehen` über `Spielablauf.Umziehen`). Meldung P9-11 mit 20 Stichproben, wartet auf Nicos Freigabe und Merge-OK. Danach Bericht Phase 9 |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`, Vorladen und Gesamtansicht mit allen Orten `AutoMessung.P8W12Bau.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G52 bestehen, CI erzeugt alle neu (G52 `lerninhalte_pruefung.json`: Ladeprüfung und Sichten AE/DPA/DV). Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P9-11** / P9-12 |
| Sammelliste | nächste freie Nummer **181** (164 Spiel je Fachrichtung in der Beta; 169–176 Funde und Einzelfälle der Einteilung aus W2; 177 Tabellen in Szenarien mit Proportionalschrift; 178 Optionen > Spiel und 179 Umziehen, in W4/W5 gebaut; 180 Platzhalter Löschen/Tagesziel); offen für Nico: 24 (Rechnername im Gerätenamen) |
| Stoppgrenzen | siehe `CLAUDE.md` Abschnitt „Stoppgrenzen“ (Regel A und Spiel) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` (Block „Routinen“ zuerst) und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
