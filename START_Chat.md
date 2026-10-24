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
| Version | **A 0.12.1** auf `main` (e031933, PR #30); veröffentlicht vA0.12.0 (Nico 24.10.2026 16:19), vA0.12.1 durch Nico (Weisung P6-13). Zweig `claude/project-thread-n4dahs` (ab e031933) hebt auf **A 0.12.2** (Paket 1.0.0-a.0.12.2, Tag vA0.12.2, Android 1001202, Update-APK 1001203, Update-Probe A 0.12.2 → A 0.12.3 → B 0.1.0; Weisung P6-14) |
| Phase | 0–5 abgenommen und gemergt (Kern, Lernkern, Rechner, Prüfungsbereiche, Betrieb: Sicherung, Diagnose, Update); Phase 6 W1 Release-Ablauf, W2 Plattformen (Linux-Verknüpfung) und W3 erstes Release gemergt |
| Jetzt | **A 0.12.2 Optik wie Python 0.63.2** (Weisung P6-14 bis P6-17): alle Seiten wie die Handy-App, Schrift Roboto, Symbole Material Icons (`fisi_symbole.otf`), Kalender mit Blättern; Fix HaengerTests (P6-15). Merge erst nach Nicos Bild-Abnahme (`bilder/A0.12.2_Optik/`). Bericht Phase 6 (VE9) vorbereitet, Push erst nach Weisung mit Ergebnis des Update-Tests A 0.12.1 |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Farbregler im Start ohne Bedienung: erster Schritt 150 ms, ab Schritt 2 100 ms. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G41 bestehen, CI erzeugt alle neu |
| Letzte/nächste Meldung | **P6-14** (A 0.12.2 Optik) / P6-15 |
| Sammelliste | nächste freie Nummer **79**; offen für Nico: 24 (Rechnername im Gerätenamen); für Phase 6: 46; 74 (Lizenztexte in der APK), 75 (Kommentar PythonPow.cs), 76/77 (Linux-Verknüpfung), 78 (Start-Test-Probe) |
| Stoppgrenzen | unverändert, siehe `CLAUDE.md` „Bleibende Regeln“ (Regel A) |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
