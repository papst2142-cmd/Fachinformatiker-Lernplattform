# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 Fachrichtungen, 10 Inhalte-Runden 2–4 je Fachrichtung (A 0.28 bis 0.36), 11 NVDA und Gesamttest, 12 Stadtbau-Werkzeug (eigenes Programm außerhalb der Lernplattform, nur Windows-PC und Android-Tablet: Nico baut Städte, Häuser und Innenräume selbst, Anzeige der Grenzen Dreiecke/Aufrufe, Export-Datei, die CC ins Spiel einbaut und prüft; die heutige Stadt bleibt geschützt; Plan kommt mit eigener Weisung), danach Beta 1 mit drei neuen Städten aus dem Werkzeug; Nico 30.10.2026, Plan Phase 9 Abschnitt 12.6, Weisung P9-11). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); Phase 6 (Verteilung) abgenommen (Weisung P6-21); Phase 7 (Spiel in 3D) und Phase 8 (Spiel-Abgleich und Kleinigkeiten) abgenommen; **Phase 9 Fachrichtungen abgenommen (Nico 30.10.2026 15:27 MEZ); Phase 10 Inhalte-Runden 2–4 läuft nach `Plan_Phase10_CSharp.md`**.
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | Auf `main` **A 0.28.0** (Phase 10 W1, PR #76, main a16da63). Zweig `claude/project-thread-1xchax` **A 0.29.0** (Phase 10 W2 AE Runden 2–4). Release nur nach Nicos Merge-OK durch Chat, CC startet `release.yml` nie |
| Phase | 0–8 abgenommen. **Phase 9** fertig: W1 Prüfstand A 0.23.1, W2 Gerüst A 0.24.0 (Sicht je Fachrichtung, Lader `inhalte_csharp/lernen/`, Einteilung per `einteilen.py`, Klausur b1/b2), W3 bis W5 Runde 1 AE, DPA, DV (je 1 000 Inhalte, A 0.25.0 bis 0.27.0), dazu Optionen > Spiel und Umziehen. Abgenommen (Nico 30.10.2026 15:27 MEZ) |
| Jetzt | Phase 10 (Weisung P9-13: je Fachrichtung eine Welle mit Runden 2–4). W1 A 0.28.0 gemergt (nur gewählte Fachrichtung laden, Nachladen mit Ladeanzeige). **W2 AE A 0.29.0 gebaut** (Weisung P10-1): 1 200 Karten, 1 750 Fragen, 50 Testprojekte, 18 Fachgespräch-Fragen (`inhalte_csharp/lernen/fachgespraech.json`), Sicht AE 2 584 / 3 735 / 50 / 60; Gegenprüfung aller Inhalte, Werkzeuge `/mnt/project-files/phase10_csharp/w2/werkzeuge/`. Wartet auf Nicos Stichproben und Merge-OK. Danach W3 DPA (A 0.30.0), W4 DV (A 0.31.0), Bericht Phase 10 |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`, Vorladen und Gesamtansicht mit allen Orten `AutoMessung.P8W12Bau.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G52 bestehen, CI erzeugt alle neu (G52 `lerninhalte_pruefung.json`: Ladeprüfung mit Fachgespräch und Sichten AE/DPA/DV). Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P10-1** (W2 AE) / P10-2 |
| Sammelliste | nächste freie Nummer **182** (181 Simple Factory in einer Bestand-Quizfrage, P10-1; 164 Spiel je Fachrichtung in der Beta; 169–176 Funde und Einzelfälle der Einteilung aus W2; 177 Tabellen in Szenarien mit Proportionalschrift; 178 Optionen > Spiel und 179 Umziehen, in W4/W5 gebaut; 180 Platzhalter Löschen/Tagesziel); offen für Nico: 24 (Rechnername im Gerätenamen) |
| Stoppgrenzen | siehe `CLAUDE.md` Abschnitt „Stoppgrenzen“ (Regel A und Spiel) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` (Block „Routinen“ zuerst) und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
