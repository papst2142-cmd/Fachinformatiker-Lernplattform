# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md`. Phasenpläne in `Fi-Austausch/weisungen/`: `Plan_Phase0_CSharp.md`, `Plan_Phase1_CSharp.md`, `Plan_Phase2_CSharp.md` (freigegeben, P2-2).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | **A 0.2.0** auf `main` (f97beeb), Paket 1.0.0-a.0.2.0, Android 1000200; kein Tag, kein öffentliches Release |
| Phase | 0 und 1 abgenommen und gemergt (Phase 1: PR #2, CI auf main 8/8 grün, Pakete von Nico auf Windows-VM, Ubuntu-VM, S24 getestet). Zweig `phase1-kern` existiert nicht mehr |
| Jetzt | **Phase 2 (Lernkern) im Bau**, Weisung P2-2_Plan_Freigabe (Fi-Austausch d5da1a1). Welle 1 (L1 Lernlogik, L2 Antworten speichern, L3 Übernahme) auf Zweig `phase2-kern`, Bericht P2-2; Merge erst nach Abnahme + Nicos OK |
| Ziel Phase 2 | 5 Wellen: W1 L1–L3 (ohne Versionssprung), W2 L4–L6 = A 0.3.0 (1000300), W3 L7–L8 = A 0.4.0, W4 L9–L10 = A 0.5.0, W5 L11–L13 = A 0.6.0. Karteikarten + Wiederholung, Prüfungstrainer, Fortschritt, Suche, Übernahme (Variante b), Abgleich PC↔Handy (neues privates Repo vor W4) |
| Vergleichswerte | G1, G2, G6, G6b, G9–G14 bestehen; W1 neu: G15 wiederholung, G16 reihenfolge, G17 trainer, G20 uebernahme, Erzeuger pruefsumme_sonderfall (CI erzeugt alle neu) |
| Letzte/nächste Meldung | P2-2 (Bericht W1) / **P2-3** |
| Stoppgrenzen | unverändert, siehe `CLAUDE.md` „Aktueller Stand“ (Regel A) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
