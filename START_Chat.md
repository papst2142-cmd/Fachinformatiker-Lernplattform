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
| Version | A 0.1.0 auf `main`; **A 0.2.0** auf Zweig `phase1-kern` (kein öffentliches Release, Pakete aus der CI) |
| Phase | 1, Welle 2 (K5–K10) abgenommen, Nachbesserung N1/N2 gepusht (Code a49948c), Lauf 39414992742 grün 8/8 |
| Vergleichswerte neu | G6b `eigene_farben.json`, G9 `beschriftungen.json`, G13 `dashboard.json`, G14 `potenz.json` (Erzeuger in `werkzeuge/vergleich/`, CI erzeugt sie neu) |
| Letzte Meldung | P1-6 (N1/N2 gepusht, Lauf); nächste P1-7 |
| Offen bei Chat | K9-Diff lesen, Fragen 2–5 (Hinweistexte Schrift/Tagesziel, Über-Text, defekt.json) |
| Nächster Schritt | Chat prüft Kopf und Lauf, dann Nicos Freigabe (Pakete auf Windows-VM, Ubuntu-VM, S24); danach Merge-Frage und Welle 3 laut Plan. Übergabe: `/mnt/project-files/phase1_csharp/HANDOFF_Welle2_Abnahme.md` |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`, Schlusszeile `Meldung <Nr> liegt auf Fi-Austausch, Commit <sha>`.
4. Nach dem Patch: Version +1 (außer eine Weisung sagt anderes, z. B. Welle 1 von Phase 1), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren.
