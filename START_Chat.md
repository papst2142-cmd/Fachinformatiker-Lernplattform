# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 Fachrichtungen, 10 Inhalte-Runden 2–4 je Fachrichtung (A 0.28 bis 0.36), 11 NVDA und Gesamttest, 12 Stadtbau-Werkzeug (eigenes Programm außerhalb der Lernplattform, nur Windows-PC und Android-Tablet: Nico baut Städte, Häuser und Innenräume selbst, Anzeige der Grenzen Dreiecke/Aufrufe, Export-Datei, die CC ins Spiel einbaut und prüft; die heutige Stadt bleibt geschützt; Plan kommt mit eigener Weisung), danach Beta 1 mit drei neuen Städten aus dem Werkzeug; Nico 30.10.2026, Plan Phase 9 Abschnitt 12.6, Weisung P9-11). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); Phase 6 (Verteilung) abgenommen (Weisung P6-21); Phase 7 (Spiel in 3D) und Phase 8 (Spiel-Abgleich und Kleinigkeiten) abgenommen; Phase 9 und Phase 10 abgenommen; **Phase 11 (Barrierefreiheit, Fehlerwellen, Gesamttest) fertig gebaut, Bericht wartet auf Abnahme**.
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | Auf `main` **A 0.39.0** (Handy-Update, PR #103, main 9eec1ef, gemergt 05.11.2026 08:30 MEZ). Release vA0.39.0 macht Chat; CC startet `release.yml` nie |
| Phase | 0–10 abgenommen (Phase 10: Nico 02.11.2026 06:58 MEZ). **Phase 11** fertig gebaut: W1, FW1–FW10, W2, W3, Mac-Welle, Fehlerberichte, Patch-Welle, Seitenleiste, CI schneller und Handy-Update gemergt (A 0.32.0 bis 0.39.0, PR #84 bis #103); Gerätetest auf allen 5 Geräten (04.11.2026), NVDA und TalkBack in Ordnung; 0 offene F |
| Jetzt | Bericht Phase 11 `Fi-Austausch/anlagen/P11-Bericht_Phase11.md` mit Meldung P11-33 und Doku-PR (keine Version); wartet auf Nicos Abnahme und Antworten. Offen bis 1.0 siehe `CLAUDE.md` „Aktueller Stand“. Danach Phase 12 (Spielmechanik Charakter) erst mit eigenem Plan und Nicos Freigabe |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Start-Test in zwei Teilen (`LERNPLATTFORM_MESSUNG_TEIL=lernen`/`spiel`), im Spielteil je Festzeit Stadt, Park, Kaufhaus und Gesamtansicht (`AutoMessung.P8W6.cs`), Festzeiten-Gestalten `AutoMessung.P8W11.cs`, Vorladen und Gesamtansicht mit allen Orten `AutoMessung.P8W12Bau.cs`. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G52 bestehen, CI erzeugt alle neu (G52 `lerninhalte_pruefung.json`: Ladeprüfung mit Fachgespräch und Sichten AE/DPA/DV). Fingerabdrücke f2b59ce8, Stadt 03d0a012, Einkauf 185dee1b, Kaufhaus a3ec3cdf unverändert |
| Letzte/nächste Meldung | **P11-33** (Bericht Phase 11) / P11-34 |
| Sammelliste | nächste freie Nummer **215**; offen nur 214 (Ladezeit Tab A9+, Frage an Nico) und Vorschlag K für 122/123 |
| Stoppgrenzen | siehe `CLAUDE.md` Abschnitt „Stoppgrenzen“ (Regel A und Spiel) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` (Block „Routinen“ zuerst) und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
