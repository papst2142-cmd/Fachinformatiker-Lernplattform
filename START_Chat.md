# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md`. Phase 0: `Plan_Phase0_CSharp.md`.

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | A 0.1.0 (in Arbeit, noch nicht veröffentlicht), Paketversion 1.0.0-a.0.1.0 |
| Phase | 0, im Bau seit 20.10.2026; Zweig `phase0-grundlagen`, PR #1 |
| Fertig | Grundgerüst, Spikes B, C (mit Auflagen A3), D (Pakete + Update-Probe CI), Farbwelten G6, Probeseiten A und E |
| Letzte Meldung | P0-4 (Fi-Austausch) |
| Offen bei Nico | Messungen Spike A, Installation/Update Spike D (Windows-VM, Ubuntu-VM, S24), NVDA Spike E, Sicherungsprüfung nach Chat-Lesung |
| Offen bei Chat | Lesung Sicherungsprüfung (Auflage 4), Paketversion-Schema (Frage 1 P0-4) |
| Nächster Schritt | Ergebnisse von Nico einarbeiten, dann Bericht Phase 0 mit Gegenprüfung |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`, Schlusszeile `Meldung <Nr> liegt auf Fi-Austausch, Commit <sha>`.
4. Nach dem Patch: Version +1, `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren.
