# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md`. Phasenpläne in `Fi-Austausch/weisungen/`: `Plan_Phase0_CSharp.md`, `Plan_Phase1_CSharp.md`, `Plan_Phase2_CSharp.md` (in Arbeit).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | **A 0.2.0** auf `main` (f97beeb), Paket 1.0.0-a.0.2.0, Android 1000200; kein Tag, kein öffentliches Release |
| Phase | 0 und 1 abgenommen und gemergt (Phase 1: PR #2, CI auf main 8/8 grün, Pakete von Nico auf Windows-VM, Ubuntu-VM, S24 getestet). Zweig `phase1-kern` existiert nicht mehr |
| Jetzt | **Phase 2 (Lernkern) in Planung**, Weisung P2-1_Plan (Fi-Austausch d6be822): nur Plan + Meldung P2-1, **kein Bau** bis „Plan freigegeben“ |
| Ziel Phase 2 | A 0.3.0 (1000300): Karteikarten + Wiederholung, Prüfungstrainer, Fortschritt/Auswertung, Suche, Lernstand-Import aus der Sicherungsdatei (Pflicht), Abgleich PC↔Handy. Pakete heißen L1, L2, … |
| Vergleichswerte | G1, G2, G6, G6b, G9–G14 bestehen (Erzeuger `werkzeuge/vergleich/`, CI erzeugt neu); Phase 2 setzt mit G15 ff. fort |
| Letzte/nächste Meldung | P1-6 / **P2-1** (Plan Phase 2) |
| Stoppgrenzen | unverändert, siehe `CLAUDE.md` „Aktueller Stand“ (Regel A) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
