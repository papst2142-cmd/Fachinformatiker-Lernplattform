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
| Version | **A 0.14.0** auf Zweig `claude/project-thread-8mg42a` (Phase 8 W3 Spiel-Abgleich, PR als Entwurf, Weisung P8-5). Auf `main` **A 0.13.2** (a9f9448, PR #53). Zuletzt veröffentlicht vA0.13.1 (4684608). Release macht Chat nach Nicos Merge-OK, CC startet `release.yml` nie |
| Phase | 0–7 abgenommen (Phase 7 Spiel in 3D, Bericht `Fi-Austausch/anlagen/P7-Bericht_Phase7.md`). **Phase 8** (Plan `Plan_Phase8_CSharp.md`, Weisung P8-2): W1 Prüfstand (#51), W2 Eingaben A 0.13.1 (#52), W2b Seitenleiste/Optionen A 0.13.2 (#53) gemergt |
| Jetzt | **W3 Spiel-Abgleich (A 0.14.0):** Spielseite frischt nach Abgleich auf (nicht bei offenem Ticket, danach nachgeholt), gelöschter Platz wird neu gewählt, Tagesuhr der Stadt nimmt das Gerät mit dem weitesten Stand (`SpielGeraete.cs`), Spielsperre erst vor dem ersten Schreiben (`Spielablauf.Sichern`); Nachtrag P8-5b: Optionen je Gruppe eine Farbe (Löschen rot). Danach W4 Spiel-Tempo, W5 Kaufhaus + Hilfe + Abschluss |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Farbregler erster Schritt 150 ms, ab Schritt 2 100 ms. Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`). Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G51 bestehen, CI erzeugt alle neu. Spiel: `tests/Vergleich/spiel_*.json`; neu G51 `spiel_abgleich.json` (Erzeuger `werkzeuge/vergleich/erzeuge_spiel_abgleich.py`, drei Geräte, Abgleich wie `fisi_sync`). Fingerabdrücke f2b59ce8, 86ac8f19, 185dee1b unverändert |
| Letzte/nächste Meldung | **P8-5b** (Gruppenfarben, nach P8-5 W3) / P8-6 |
| Sammelliste | nächste freie Nummer **122** (118–120 aus P8-5, 121 Gruppenfarben der Optionen in P8-5b); offen für Nico: 24 (Rechnername im Gerätenamen); 84/85 in Phase 9 |
| Stoppgrenzen | Regel A wie bisher, neu für das Spiel siehe `CLAUDE.md` „Aktueller Stand“, Punkt Phase 7 |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
