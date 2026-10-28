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
| Version | **A 0.22.0** auf Zweig `claude/project-thread-dmsek9` (Phase 8 W11 Festzeiten-Verkehr und Stadtfehler, PR als Entwurf, Weisung P8-45). Auf `main` **A 0.21.0** (ae19579, PR #64). Release macht Chat nach Nicos Merge-OK, CC startet `release.yml` nie |
| Phase | 0–7 abgenommen. **Phase 8** (Plan `Plan_Phase8_CSharp.md`, Weisung P8-2): W1 bis W10 gemergt (zuletzt W8 #61 A 0.18.0, W8b #62 A 0.19.0, W9 #63 A 0.20.0, W10 #64 A 0.21.0) |
| Jetzt | **W11 (A 0.22.0, Weisung P8-45):** Gestalten statt Autos zu Halloween/Weihnachten (ab Dämmerung) und Ostern (tagsüber) (`FestVerkehr`), 21 Laternen nach Nicos Skizze (16 einfach, 5 doppelt) im eigenen Paket-Netz mit Tannen und Kürbissen (Graveyard Kit), Bahnhof ohne Turm mit Uhr c2, Stellwerk ohne Rückseitentür, Straßenenden bündig, Altbau-Anbau. Wartet auf Nicos Merge-OK. Danach Sammelliste 144 und Bericht Phase 8 |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G51 bestehen, CI erzeugt alle neu. Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P8-42** (W11 Bau) / P8-43 |
| Sammelliste | nächste freie Nummer **162**; offen für Nico: 24 (Rechnername im Gerätenamen); 84/85 in Phase 9 |
| Stoppgrenzen | Regel A wie bisher, neu für das Spiel siehe `CLAUDE.md` „Aktueller Stand“, Punkt Phase 7 |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
