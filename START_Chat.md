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
| Version | **A 0.19.0** auf `main` (c34a9f5, PR #62, W8b Stadt-Feinschliff, 28.10.2026). Release macht Chat nach Nicos Merge-OK, CC startet `release.yml` nie |
| Phase | 0–7 abgenommen. **Phase 8** (Plan `Plan_Phase8_CSharp.md`, Weisung P8-2): W1 Prüfstand (#51), W2 Eingaben A 0.13.1 (#52), W2b A 0.13.2 (#53), W3 Spiel-Abgleich A 0.14.0 (#54), W4 Spiel-Tempo A 0.14.1 (#55), Fix A 0.14.2 (#56), W5 Kaufhaus A 0.15.0 (#57), W6 Festzeiten A 0.16.0 (#58), W7 Schritte/Autos/Grün A 0.17.0 (#59), W8 Ton/Vollbild/Tasten A 0.18.0 (#61), W8b Stadt-Feinschliff A 0.19.0 (#62) gemergt |
| Jetzt | **W8b gemergt** (Türen am Wegende und mittig, nur eine Tür je Haus, Fenster ringsum, Stellwerk mit Schild, Bahnhof innen als Wartehalle, Stadt-Fingerabdruck 03d0a012; Prüfung je Haus mit Nahaufnahmen aus 4 Blickwinkeln, Bilder `Fi-Austausch/bilder/A0.19.0_W8b_Tueren2/`). Nächstes: W9 (u. a. Hörprobe an den Ton-Reglern, Weisung P8-29) nach Weisung in einem neuen CC-Thread |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`). Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G51 bestehen, CI erzeugt alle neu. Fingerabdrücke f2b59ce8, 86ac8f19, 185dee1b, a3ec3cdf unverändert (Tests ohne Festzeit) |
| Letzte/nächste Meldung | **P8-30** (W8b Türen Feinschliff, Fi-Austausch 55ce136) / nach nächster Weisung |
| Sammelliste | nächste freie Nummer **148**; offen für Nico: 24 (Rechnername im Gerätenamen); 84/85 in Phase 9 |
| Stoppgrenzen | Regel A wie bisher, neu für das Spiel siehe `CLAUDE.md` „Aktueller Stand“, Punkt Phase 7 |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
