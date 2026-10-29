# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge neu Abschnitt 12: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 NVDA und Gesamttest, danach Beta). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); Phase 6 (Verteilung) abgenommen (Weisung P6-21); Phase 7 (Spiel in 3D) abgenommen; **Phase 8: Plan `Plan_Phase8_CSharp.md` freigegeben (Weisung P8-2)**.
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | Auf `main` **A 0.23.0** (W12 Vorladen und Nicos Optik-Nachtrag, PR #67, 91be933). Release macht Chat nach Nicos Merge-OK, CC startet `release.yml` nie |
| Phase | 0–7 abgenommen. **Phase 8** (Plan `Plan_Phase8_CSharp.md`, Weisung P8-2): W1 bis W12 gemergt (zuletzt #67 A 0.23.0) |
| Jetzt | Doku: `CLAUDE.md` aufgeräumt und Block „Routinen“ (Weisung P8-57, Doku-PR). Danach **Bericht Phase 8** (nach Weisung) |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`, Vorladen und Gesamtansicht mit allen Orten `AutoMessung.P8W12Bau.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G51 bestehen, CI erzeugt alle neu. Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P8-52** (CLAUDE.md aufgeräumt) / P8-53 |
| Sammelliste | nächste freie Nummer **164** (163 Vollbild-Satz mit Esc offen); offen für Nico: 24 (Rechnername im Gerätenamen); 84/85 in Phase 9 |
| Stoppgrenzen | siehe `CLAUDE.md` Abschnitt „Stoppgrenzen“ (Regel A und Spiel) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` (Block „Routinen“ zuerst) und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
