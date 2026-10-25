# START_Chat.md – Einstieg für einen neuen Chat zur C#-Linie

Diese Datei wird nach jedem Patch erneuert. Sie reicht, um in einem frischen Chat sauber weiterzuarbeiten. Führend ist `CLAUDE.md`; bei Widerspruch gilt `CLAUDE.md`.

## 1. Worum es geht
Fachinformatiker Lernplattform, Neubau in C# (.NET 10, Avalonia 12.1). Besitzer und Entscheider: Nico. Fachaufsicht: Claude Chat. Umsetzung: Claude Code.

## 2. Wo alles liegt
- Code: `papst2142-cmd/Fachinformatiker-Lernplattform` (`main`, geschützt: Änderungen nur per PR, Squash nach Freigabe). Regeln: `CLAUDE.md` im Repo.
- Austausch: `papst2142-cmd/Fi-Austausch` (`weisungen/`, `meldungen/`, `anlagen/`, `SAMMELLISTE_NEU.md`).
- Referenz (nur lesen): `papst2142-cmd/Fisi-Lernplattform` (Python, eingefroren seit v0.63.2, main fbc657d).
- Gesamtplan: `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md` (Phasenfolge neu Abschnitt 12: 5 Betrieb, 6 Verteilung, 7 Spiel, 8 Spiel-Abgleich und Kleinigkeiten, 9 NVDA und Gesamttest, danach Beta). Pläne in `Fi-Austausch/weisungen/`: Phasen 0–5 umgesetzt (ältere Pläne in der Git-Historie); Phase 6 (Verteilung) abgenommen (Weisung P6-21); **Phase 7 (Spiel in echtem 3D): Plan `Plan_Phase7_CSharp.md` freigegeben (Weisung P7-2)**.
- Zusammenarbeit: `Fi-Austausch/weisungen/Regel_Automatische_Uebergabe.md` (bindend, Abschnitte 1–11, Startzeilen für neue Threads).

## 3. Aktueller Stand
| Feld | Wert |
|---|---|
| Version | **A 0.12.2** auf `main` (c79c53f, PR #31), veröffentlicht als vA0.12.2 (24.10.2026 20:41). Davor vA0.12.0 (d30b1a3) und vA0.12.1 (e031933). Nächste Version A 0.12.3 bzw. nach Weisung. Release macht Chat nach Nicos Merge-OK (Nico 24.10.2026 18:19), CC nie. CC startet `release.yml` nie |
| Phase | 0–6 abgenommen; 0–5 (Kern, Lernkern, Rechner, Prüfungsbereiche, Betrieb: Sicherung, Diagnose, Update); **Phase 6 fertig** bis auf den Update-Test am Windows-PC (Nico): W1 Release-Ablauf, W2 Plattformen, W3 erstes Release, A 0.12.1 Update-Test, A 0.12.2 Optik wie Python 0.63.2. Update-Test S24 A 0.12.0 → A 0.12.2 bestanden |
| Jetzt | **Phase 7:** W1 Stichprobe 3D PR #35 (nicht mergen); W2, W3, W8, P7-6 und W4 gemergt (main 6f9e478). Stapel als Entwurf, nur zusammen und der Reihe nach mergen: #42 W5 Innenräume (831201d) ← #43 W6 Ansichten (c2f0f42) ← #44 W7 Geschäfte + Nachtrag 1 Nicos S24-Wünsche (57deb75, voll 40232273792 8/8) ← #45 W7b Bank und Amt + Text 5 (af2bd1f, voll 40234988703 8/8, Probe-APK 12615140460). Wartet auf Nicos S24-Probe und Merge-OK (Meldung P7-13a). Einzelheiten `CLAUDE.md` Punkt Phase 7 |
| Messregeln | Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms). Farbregler im Start ohne Bedienung: erster Schritt 150 ms, ab Schritt 2 100 ms. Messwiederholung und Neustart-Regel siehe `CLAUDE.md` Abschnitt CI |
| Vergleichswerte | G1–G41 bestehen, CI erzeugt alle neu; Spiel (W2, main 40affd5): `spiel_*.json` für G42, G44–G46, G48, G49; W3 (main 3807754): G43 `spiel_fingerabdruck.json` (f2b59ce8…6656), G47 `spiel_firma.json`; W8 (PR #38): G44 Übernahme `spiel_uebernahme.json`; P7-6 (PR #40): G44 Verwaltung `spiel_verwaltung.json`, G48 Stadt `spiel_stadt.json`, zweiter Fingerabdruck 86ac8f19…59ac |
| Letzte/nächste Meldung | **P7-13a** (Fi-Austausch 47797ac) / P7-14; Sammelliste nächste 106 |
| Sammelliste | nächste freie Nummer **106** (105: .NET 10.0.14 in der Lizenzliste, kommt mit #44 nach main); offen für Nico: 24 (Rechnername im Gerätenamen); für Phase 6: 46; 74 (Lizenztexte in der APK), 75 (Kommentar PythonPow.cs), 76/77 (Linux-Verknüpfung), 78 (Start-Test-Probe) |
| Stoppgrenzen | Regel A wie bisher, neu für das Spiel siehe `CLAUDE.md` „Aktueller Stand“, Punkt Phase 7 |

## 4. So arbeitet der neue Chat
1. `CLAUDE.md` und diese Datei lesen, dann die jüngste Weisung auf `Fi-Austausch` (bei Widerspruch gilt die jüngere).
2. Nur den aktuellen Auftrag erledigen; Neues → `SAMMELLISTE_NEU.md`.
3. Ergebnis als Meldung auf `Fi-Austausch`; letzte Zeile der Meldungsdatei `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main`, die echte sha nur in der Zeile an Nico.
4. Nach einem Programm-Patch: Version +1 nach Schema x.y.z (nur wenn die Weisung es sagt), `AENDERUNGEN.md`, diese Datei und `CLAUDE.md` aktualisieren. Reine Dokument-PRs erhöhen die Version nicht.
