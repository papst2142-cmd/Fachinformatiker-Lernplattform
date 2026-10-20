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
| Version | A 0.1.0 (in Arbeit, noch nicht veröffentlicht) |
| Phase | 0, im Bau seit Startzeile Nico 20.10.2026 |
| Fertig | Grundgerüst (5 Projekte), Spike B Zufall, Spike C mit Testdaten |
| Offen bei Nico | Ort der Sicherungsdatei (Spike C), Messungen Spike A, Starts/Updates Spike D, NVDA-Probe Spike E, Actions-Rechte nur falls nötig |
| Nächster Schritt | Spike A (Messfenster mit Farbwelten), Spike D (Pakete und Updates aus der CI) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`, Schlusszeile `Meldung <Nr> liegt auf Fi-Austausch, Commit <sha>`.
4. Nach dem Patch: Version +1, `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren.
