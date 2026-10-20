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
| Version | A 0.1.0 auf `main`; A 0.2.0 kommt mit Welle 2 von Phase 1 (kein öffentliches Release, Pakete aus der CI) |
| Phase | 1 freigegeben (Weisung P1-1_Plan_Freigabe, 125a7f6). Zweig `phase1-kern`: Welle 1 (K1 Lerninhalte, K2 Lernstand-DB, K3 Einstellungen, K4 Fehlerprotokoll) gebaut |
| Vergleichswerte neu | G10 `fehlerprotokoll.json`, G11 `datenbank.json`, G12 `inhalte_pruefung.json` (Erzeuger in `werkzeuge/vergleich/`) |
| Letzte Meldung | P1-1 Plan Phase 1; nächste P1-2 Bericht Welle 1 |
| Offen bei Chat | Lesung Meldung P1-2, danach Freigabe Welle 2 (K5–K9) |
| Nächster Schritt | Nach Lesung: Welle 2 auf `phase1-kern`, dabei Version A 0.2.0 |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`, Schlusszeile `Meldung <Nr> liegt auf Fi-Austausch, Commit <sha>`.
4. Nach dem Patch: Version +1 (außer eine Weisung sagt anderes, z. B. Welle 1 von Phase 1), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren.
