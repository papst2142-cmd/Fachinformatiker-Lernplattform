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
| Phase | 1, Welle 2 (K5 Design, K6 Navigation, K7 Dashboard, K8 Optionen, K9 Sicherung in Optionen) gebaut; gepusht ohne K9; K9 liegt als Patch `/mnt/project-files/phase1_csharp/K9_Sicherung.patch` (gleich Anlage `P1-3_K9_*.diff`) und wird erst nach Chats OK gepusht |
| Vergleichswerte neu | G6b `eigene_farben.json`, G9 `beschriftungen.json`, G13 `dashboard.json`, G14 `potenz.json` (Erzeuger in `werkzeuge/vergleich/`, CI erzeugt sie neu) |
| Letzte Meldung | P1-3 (Welle 2, K9 zur Lesung, 5 Fragen); nächste P1-4 |
| Offen bei Chat | K9-Diff lesen, Fragen 2–5 (Hinweistexte Schrift/Tagesziel, Über-Text, defekt.json) |
| Nächster Schritt | Nach Chats Antwort: K9 pushen, Antworten umsetzen, dann K10 (Gegenprüfung, Fotos PC/Handy für Nico, ein ganzer GitHub-Lauf, Bericht). Übergabe: `/mnt/project-files/phase1_csharp/HANDOFF_Welle2_K10.md` |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`, Schlusszeile `Meldung <Nr> liegt auf Fi-Austausch, Commit <sha>`.
4. Nach dem Patch: Version +1 (außer eine Weisung sagt anderes, z. B. Welle 1 von Phase 1), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren.
