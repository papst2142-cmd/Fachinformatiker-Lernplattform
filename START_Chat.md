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
| Version | **A 0.23.0** auf Zweig `claude/project-thread-vruvcy` (W12 Vorladen, Weisung P8-55). Auf `main` **A 0.22.1** (39e4e56, PR #66). Release macht Chat nach Nicos Merge-OK, CC startet `release.yml` nie |
| Phase | 0–7 abgenommen. **Phase 8** (Plan `Plan_Phase8_CSharp.md`, Weisung P8-2): W1 bis W11 gemergt (zuletzt W10 #64 A 0.21.0, W11 #65 A 0.22.0, Fix #66 A 0.22.1) |
| Jetzt | **W12 Vorladen A 0.23.0 (Weisung P8-55):** Spielseite lädt Stadt, Räume, Festgestalten und Figurbilder beim Öffnen vor (Ladeanzeige „Stadt wird geladen …“ ab 0,3 s), Räume per Inhalts-Signatur, Stadt für neues Fest schon im Haus gebaut, Android nach Hintergrund nur neu hochladen; Grenzen Gesamtansicht 160 000 / 40; Sammelliste 162 behoben, 145/147 waren schon behoben. Wartet auf vollen Lauf, Nicos Probe am S24/Tab und Merge-OK. Danach Sammelliste 144 und Bericht Phase 8 |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`, Vorladen und Gesamtansicht mit allen Orten `AutoMessung.P8W12Bau.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G51 bestehen, CI erzeugt alle neu. Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P8-50** (W12 Vorladen) / P8-51 |
| Sammelliste | nächste freie Nummer **163**; offen für Nico: 24 (Rechnername im Gerätenamen); 84/85 in Phase 9 |
| Stoppgrenzen | Regel A wie bisher, neu für das Spiel siehe `CLAUDE.md` „Aktueller Stand“, Punkt Phase 7 |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
