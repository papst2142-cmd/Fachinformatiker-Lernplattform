# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge neu Abschnitt 12: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 NVDA und Gesamttest, danach Beta). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); **Phase 6 (Verteilung): Plan `Plan_Phase6_CSharp.md` freigegeben (Weisung P6-2), W1–W3 gemergt, A 0.12.0 veröffentlicht (vA0.12.0); A 0.12.1 gemergt (Update-Test durch Nico), A 0.12.2 Optik im Bau, danach Bericht Phase 6**.
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | **A 0.12.2** auf `main` (c79c53f, PR #31), veröffentlicht als vA0.12.2 (24.10.2026 20:41). Davor vA0.12.0 (d30b1a3) und vA0.12.1 (e031933). Nächste Version A 0.12.3 bzw. nach Weisung. CC startet `release.yml` nie |
| Phase | 0–5 abgenommen und gemergt (Kern, Lernkern, Rechner, Prüfungsbereiche, Betrieb: Sicherung, Diagnose, Update); **Phase 6 fertig** bis auf den Update-Test am Windows-PC (Nico): W1 Release-Ablauf, W2 Plattformen, W3 erstes Release, A 0.12.1 Update-Test, A 0.12.2 Optik wie Python 0.63.2. Update-Test S24 A 0.12.0 → A 0.12.2 bestanden |
| Jetzt | Bericht Phase 6 (VE9) liegt auf Fi-Austausch `anlagen/P6-Bericht_Phase6.md` (Meldung P6-16), wartet auf Abnahme; danach Phase 7 (Spiel) nur nach Plan und Nicos Freigabe |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Farbregler im Start ohne Bedienung: erster Schritt 150 ms, ab Schritt 2 100 ms. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G41 bestehen, CI erzeugt alle neu |
| Letzte/nächste Meldung | **P6-16** (Bericht Phase 6) / P6-17 bzw. P7-1 |
| Sammelliste | nächste freie Nummer **79**; offen für Nico: 24 (Rechnername im Gerätenamen); für Phase 6: 46; 74 (Lizenztexte in der APK), 75 (Kommentar PythonPow.cs), 76/77 (Linux-Verknüpfung), 78 (Start-Test-Probe) |
| Stoppgrenzen | unverändert, siehe `CLAUDE.md` „Bleibende Regeln“ (Regel A) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
