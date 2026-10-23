# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge neu Abschnitt 12: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 NVDA und Gesamttest, danach Beta). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); **Phase 6 (Verteilung): Plan `Plan_Phase6_CSharp.md` freigegeben (Weisung P6-2), W1 gemergt**.
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | **A 0.11.0** auf `main` (7f10955, PR #26 = Phase 6 W1 ohne Version; A 0.11.0 aus PR #24; Paket 1.0.0-a.0.11.0, Android 1001100, Update-APK 1001101); kein Tag, kein öffentliches Release |
| Phase | 0–5 abgenommen und gemergt (Kern, Lernkern, Rechner, Prüfungsbereiche, Betrieb: Sicherung, Diagnose, Update); Phase 6 W1 Release-Ablauf gemergt |
| Jetzt | **Phase 6 (Verteilung)**: nach W1 kommt per Weisung der Android-Schlüssel (2a), dann ein Dispatch-Lauf 8/8 auf main, dann startet Nico `release.yml` (CC nie); danach W2 Plattformen, W3 erstes Release A 0.12.0/0.12.1 |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Farbregler im Start ohne Bedienung: erster Schritt 150 ms, ab Schritt 2 100 ms. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G41 bestehen, CI erzeugt alle neu |
| Letzte/nächste Meldung | **P6-2** (W1 Release-Ablauf) / P6-3 |
| Sammelliste | nächste freie Nummer **76**; offen für Nico: 24 (Rechnername im Gerätenamen); für Phase 6: 46; neu 74 (Lizenztexte in der APK), 75 (Kommentar PythonPow.cs) |
| Stoppgrenzen | unverändert, siehe `CLAUDE.md` „Bleibende Regeln“ (Regel A) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
