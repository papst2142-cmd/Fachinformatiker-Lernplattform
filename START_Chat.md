# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 Fachrichtungen, 10 Inhalte-Runden 2–4 je Fachrichtung (A 0.28 bis 0.36), 11 NVDA und Gesamttest, danach Beta; Nico 30.10.2026, Plan Phase 9 Abschnitt 12.6). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); Phase 6 (Verteilung) abgenommen (Weisung P6-21); Phase 7 (Spiel in 3D) und Phase 8 (Spiel-Abgleich und Kleinigkeiten) abgenommen; **Phase 9 Fachrichtungen: Plan `Plan_Phase9_CSharp.md` freigegeben (Weisung P9-2), Nachtrag Abschnitt 12; W1 bis W3 gemergt, W4 Runde 1 DPA (A 0.26.0) gebaut**.
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | Auf `main` **A 0.25.0** (W3 gemergt, PR #72, main 8368ba0). Zweig `claude/project-thread-989wup` **A 0.26.0** (W4). Release vA0.25.0 macht Chat; Release macht Chat nach Nicos Merge-OK, CC startet `release.yml` nie |
| Phase | 0–8 abgenommen. **Phase 9** (Plan `Plan_Phase9_CSharp.md`, Weisung P9-2): W1 Prüfstand A 0.23.1 und W2 Gerüst A 0.24.0 gemergt (Sicht je Fachrichtung, Lader `inhalte_csharp/lernen/`, Einteilung der bisherigen Inhalte per `einteilen.py`, Klausur b1/b2 AE/DPA/DV). W3 Runde 1 AE A 0.25.0 gemergt. Danach Runde 1 DV (A 0.27.0); Runden 2–4 = Phase 10 |
| Jetzt | W4 (Weisung P9-10): Runde 1 DPA (300 Karteikarten, 370 Quizfragen, 330 AP2-Szenarien, 11 Themen `dpa_*`, Gegenprüfung aller 1 000) und Optionen-Karte Spiel wie Python (Platz/Schwierigkeitsgrad, Wohnungen Einmalzahlung/Miete, Schlüssel `spiel_miete`). Meldung P9-10 mit 20 Stichproben, wartet auf Nicos Freigabe und Merge-OK. Danach Runde 1 DV (A 0.27.0) nach Weisung |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`, Vorladen und Gesamtansicht mit allen Orten `AutoMessung.P8W12Bau.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G52 bestehen, CI erzeugt alle neu (G52 `lerninhalte_pruefung.json`: Ladeprüfung und Sichten AE/DPA/DV). Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P9-10** / P9-11 |
| Sammelliste | nächste freie Nummer **179** (164 Spiel je Fachrichtung in der Beta; 169–176 Funde und Einzelfälle der Einteilung aus W2; 177 Tabellen in Szenarien mit Proportionalschrift; 178 Optionen > Spiel, in W4 gebaut); offen für Nico: 24 (Rechnername im Gerätenamen) |
| Stoppgrenzen | siehe `CLAUDE.md` Abschnitt „Stoppgrenzen“ (Regel A und Spiel) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` (Block „Routinen“ zuerst) und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
