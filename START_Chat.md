# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md`. Phasenpläne in `Fi-Austausch/weisungen/`: `Plan_Phase0_CSharp.md`, `Plan_Phase1_CSharp.md`, `Plan_Phase2_CSharp.md` (alle umgesetzt). Plan Phase 3 (Rechner) entsteht als Nächstes.

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | **A 0.6.0** auf `main` (4ecf7c7, W5 gemergt, PR #9; Paket 1.0.0-a.0.6.0, Android 1000600, Update-APK 1000601); kein Tag, kein öffentliches Release |
| Phase | 0, 1 und **2 (Lernkern) abgenommen und gemergt**. Phase 2: W1 (PR #4), W2 (PR #5), W3 (PR #6), W4 (PR #7), W5 (PR #9, Weisungen P2-18/P2-19): Karteikarten, Wiederholung, Prüfungstrainer, Fortschritt, Suche, Übernahme, Rücksicherung, Abgleich PC↔Handy, Texte PC = Handy (Geraetetexte), Farbregler mit Spur |
| Jetzt | **Phase 3 (Rechner): Plan als Entwurf vorlegen** (Weisung P2-19 A3; Aufbau wie Plan_Phase2: Inventar Python, Vergleichswerte, Pakete, Wellen, Entscheidungen für Nico). Kein Bau vor Nicos Freigabe. Eigener Thread |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Farbregler im Start ohne Bedienung: erster Schritt 150 ms, ab Schritt 2 100 ms (Nico, P2-17, `Farbreglerregel.cs`) |
| Vergleichswerte | G1, G2, G6, G6b, G9–G14 bestehen; Phase 2: G15–G17, G20 (W1), G22 lerntexte (W2), G18 auswertung, G19 suche (W3), G21 abgleich (W4), G23 reglerspur (W5, `erzeuge_reglerspur.py`). CI erzeugt alle neu |
| Letzte/nächste Meldung | P2-13 (Doku nach Merge W5) / **P3-1** (Plan Phase 3) |
| Sammelliste | nächste freie Nummer **29**. Offen für Nico: 24 (Rechnername im Gerätenamen); Nico-Tests am S24: 18, 21, 23, 26; 28 = CI-Einzelwerte über 400 ms beobachten |
| Stoppgrenzen | unverändert, siehe `CLAUDE.md` „Aktueller Stand“ (Regel A) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
