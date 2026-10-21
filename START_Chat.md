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
| Version | **A 0.5.0** auf `main` (35b5863, W4 gemergt, PR #7; Paket 1.0.0-a.0.5.0, Android 1000500, Update-APK 1000501); kein Tag, kein öffentliches Release |
| Phase | 0 und 1 abgenommen und gemergt (Phase 1: PR #2, CI auf main 8/8 grün, Pakete von Nico auf Windows-VM, Ubuntu-VM, S24 getestet). Zweig `phase1-kern` existiert nicht mehr |
| Jetzt | **Phase 2 (Lernkern) im Bau.** W1 (PR #4), W2 (PR #5), W3 (PR #6) und **W4** (L9 Rücksicherung, L10 Abgleich PC↔Handy; abgenommen mit Weisung P2-13, PR #7, main 35b5863) gemergt. **Als Nächstes W5** (L11–L13, Ziel A 0.6.0, 1000600): erst nach Weisung; vorher Plan L11–L13 gegenlesen und Verhaltenstabelle vorlegen (Tabelle → Wortlaut Chat → Beleg → Bau). Wortliste zu Sammelliste 13 liefert Chat vor W5 |
| Ziel Phase 2 | 5 Wellen: W1 L1–L3 (ohne Versionssprung), W2 L4–L6 = A 0.3.0 (1000300), W3 L7–L8 = A 0.4.0, W4 L9–L10 = A 0.5.0, W5 L11–L13 = A 0.6.0. Karteikarten + Wiederholung, Prüfungstrainer, Fortschritt, Suche, Übernahme (Variante b), Abgleich PC↔Handy (neues privates Repo vor W4) |
| Vergleichswerte | G1, G2, G6, G6b, G9–G14 bestehen; W1: G15–G17, G20; W2: G22 lerntexte; W3: G18 auswertung (`erzeuge_auswertung.py`), G19 suche (`erzeuge_suche.py`, Kleinschreibung-Tabelle `erzeuge_python_klein.py`); W4: G21 abgleich (`erzeuge_abgleich.py`, mit Stempel und Texten). CI erzeugt alle neu |
| Letzte/nächste Meldung | P2-10 (Bericht W4) / **P2-11** |
| Sammelliste | nächste freie Nummer **27** (24 Rechnername im Gerätenamen, 25 clock_fix_text, 26 Android-Kopie nachprüfen) |
| Stoppgrenzen | unverändert, siehe `CLAUDE.md` „Aktueller Stand“ (Regel A) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
