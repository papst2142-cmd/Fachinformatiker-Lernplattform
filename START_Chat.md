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
| Version | **A 0.20.0** auf Zweig `claude/project-thread-298vse` (Phase 8 W9 Jahreszeiten-Färbung und Hörprobe, PR #63 Entwurf, Weisung P8-32). Auf `main` **A 0.19.0** (c34a9f5, PR #62, vA0.19.0 veröffentlicht). Release macht Chat nach Nicos Merge-OK, CC startet `release.yml` nie |
| Phase | 0–7 abgenommen. **Phase 8** (Plan `Plan_Phase8_CSharp.md`, Weisung P8-2): W1 bis W8 (#51–#61) und W8b Stadt-Feinschliff A 0.19.0 (#62) gemergt |
| Jetzt | **W9 (A 0.20.0, Nico 1a 2a 3c 4b 5b):** Stadt-Farben je Jahreszeit (nur Darstellung, Sommer bitgleich, Schnee im dunklen Design hell), Hörprobe an den Ton-Reglern, Sammelliste 147. PR #63 voll 8/8 (Lauf 40681570356), Meldung P8-32; wartet auf Nicos S24-Probe und Merge-OK. **Nächste Weisung: P8-33 Teilplan W10 (A 0.21.0, kein Bau) im neuen Thread** |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit (`AutoMessung.P8W6.cs`) und je Jahreszeit (`AutoMessung.P8W9.cs`, Bauzeit nur Info). Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G51 bestehen, CI erzeugt alle neu. Fingerabdrücke f2b59ce8, Stadt 03d0a012, 185dee1b, a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P8-32** (W9 Bau) / P8-33 |
| Sammelliste | nächste freie Nummer **152** (148–151 Nicos Wünsche für W10); offen für Nico: 24 (Rechnername im Gerätenamen); 84/85 in Phase 9 |
| Stoppgrenzen | Regel A wie bisher, neu für das Spiel siehe `CLAUDE.md` „Aktueller Stand“, Punkt Phase 7 |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
