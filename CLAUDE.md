# CLAUDE.md – Fachinformatiker Lernplattform (C#-Linie)

## Wer entscheidet
- Besitzer **Nico** entscheidet alles (höchste Instanz); Fachaufsicht **Claude Chat**. Weisungen kommen über das Repo `papst2142-cmd/Fi-Austausch` (`weisungen/`), Meldungen gehen dorthin (`meldungen/Meldung_P0-<n>_<Thema>.md` in Phase 0).
- Immer Deutsch, ausführlich und verständlich. Nico ist kein Entwickler. Bei Unklarheit fragen (Optionen + Empfehlung).
- Keine Zeitangaben oder Terminprognosen.

## Projekt
- Lernprogramm für die Fachinformatiker-Ausbildung (Systemintegration). Prioritäten: 1 Lernen, 2 Komfort/Tempo, 3 Spiel, 4 Verteilung.
- Neubau in C# der Python-Linie `papst2142-cmd/Fisi-Lernplattform`. Diese ist seit 0.63.2 (main fbc657d, Tag v0.63.2) **eingefroren**: nur lesen, nie löschen, Referenz für Vergleichswerte.
- Repo: `papst2142-cmd/Fachinformatiker-Lernplattform`, Zweig `main` (Ruleset: kein Löschen, kein Force-Push). Arbeit auf Zweigen, Merge per Squash nach Freigabe.
- Phasen 0–7 laut `Fi-Austausch/weisungen/Plan_Umstieg_CSharp_2026-10-18.md`; Phase 0: `Plan_Phase0_CSharp.md`. Jede Phase: Plan → Nicos Freigabe → Bau → Bericht → Abnahme.

## Technik
- .NET 10 (LTS, SDK in `global.json`), Avalonia 12.1.x, SQLite (Microsoft.Data.Sqlite), xUnit v3 mit Microsoft.Testing.Platform (`dotnet test --project tests/Lernplattform.Tests`).
- Paketversionen nur in `Directory.Packages.props`; gemeinsame Einstellungen und **Version** nur in `Directory.Build.props`.
- Avalonia-Bau-Statistik ist abgeschaltet (`Directory.Build.targets`, in der CI zusätzlich `AVALONIA_TELEMETRY_OPTOUT=1`). Nicht entfernen.
- Aufbau: `src/Lernplattform.Core` (Logik, KEINE Oberfläche, auch Spiel-Kern), `src/Lernplattform.App` (Avalonia, Namensraum `Lernplattform`), `src/Lernplattform.Desktop`, `src/Lernplattform.Android`, `tests/Lernplattform.Tests`.
- Lerninhalte in `inhalte/` sind JSON wie in Python (Stand v0.63.2). Regel: Inhalte nur anhängen, nie umschreiben oder löschen ohne Nico.
- Vergleichswerte in `tests/Vergleich/`, erzeugt von `werkzeuge/vergleich/*.py` aus der Python-Linie (nie von Hand ändern). C#-Ergebnisse müssen exakt gleich sein, sonst Meldung.
  - `zufall.json` (G2): `Core/Zufall/PythonZufall.cs` (Mersenne Twister wie Python, Lernteil) und `Wuerfel.cs` (SHA-256-Würfel des Spiels).
  - `inhalte.json` (G1), `testdaten.db/.json`, `sicherung_test.json.gz`: `Core/Daten`. Nur Testdaten; Nicos echter Lernstand nie im Repo (Auflage A3).
- Ein Bildschirm-Aufbau für PC und Handy. Beschriftungen PC = Handy. Farbwelten (72 = 6 × 6 × 2) werden im Core berechnet und müssen exakt den Python-Hexwerten entsprechen (A2).

## Namen (endgültig, Nico 19.10.2026)
- Anzeige „Fachinformatiker Lernplattform“; Android `io.github.papst2142cmd.lernplattform`; Datenordner `Fachinformatiker-Lernplattform` (Linux `fachinformatiker-lernplattform`); Datenbank `lernplattform.db`; Installationskennung (Velopack) `Fachinformatiker-Lernplattform`; Programmdatei `Lernplattform` auf allen Systemen (Windows `Lernplattform.exe`, Linux/macOS `Lernplattform`, Nico 20.10.2026, ersetzt die frühere Kleinschreibung). Kennung und Programmname nach der ersten Auslieferung nie ändern.
- **Durchgehende Update-Linie:** A, B und 1.0 haben dieselbe Kennung und denselben Datenordner; der Lernstand bleibt bei jedem Update. Kennung und Datenordner nie ändern. Eigene Installation nur gegenüber der Python-Linie (einmalige Übernahme aus `lernstand.json.gz`).
- Linux nur AppImage mit Velopack-Update; Android-Update über neue APK (gleicher Schlüssel); nur arm64-v8a.

## Version
- Aktuell intern `0.2.0`, Anzeige „A 0.2.0“, Paketversion `1.0.0-a.0.2.0`, Android-Versionscode 1000200 (Stand main f97beeb). Noch kein Tag, kein öffentliches Release. Reihenfolge A < B < 1.0.
- Schema x.y.z (Nico 20.10.2026): x = 0 in Alpha/Beta, y = neue Funktionen, z = Fixes. **Bei jedem Update Versionsnummer +1** (nur in `Directory.Build.props`; Android-Versionscode wird daraus berechnet), aber erst wenn die Weisung es sagt. Jede Version bekommt einen Abschnitt in `AENDERUNGEN.md`. Reine Dokument-PRs ohne Programmänderung erhöhen die Version nicht.
- Android-Versionscode: Stufe × 1 000 000 + Haupt × 10 000 + Neben × 100 + Fix (A=1, B=2, ab 1.0=3); A 0.1.0 = 1000100, A 0.2.0 = 1000200. Phase 2 (Nico P2-2): W1 ohne Versionssprung, W2 = A 0.3.0 (1000300), W3 = A 0.4.0, W4 = A 0.5.0, W5 = A 0.6.0 (Ende Phase 2); Fixes dazwischen z+1.

## Regeln (fest)
- Commits Deutsch, ASCII, Identität `papst2142-cmd <333448595+papst2142-cmd@users.noreply.github.com>`. Nie private E-Mail, nie Zugangsdaten, keine Schlüssel im Repo.
- Kein Force-Push. `main`, Release, Tag nur nach Freigabe. Repo-/Kontoeinstellungen (auch Actions-Rechte) nur Nico.
- Nichts löschen ohne Nicos OK. Privates Repo `fisi-lernstand` nicht anfassen.
- Copyright „Nico H“, `LICENSE.txt` „Alle Rechte vorbehalten“. Neue Fremdbibliothek nur mit Eintrag in `THIRD_PARTY_NOTICES.txt` und einzeln belegter Lizenz (A5).
- Tests nie kürzen, lockern oder überspringen. Zeitgrenze für Ansichtswechsel und erstes Öffnen 400 ms (Abbruch ab 1 000 ms).
- Meldungen ≤ 8 KB, ≤ 5 Fragen, Zeile 1 `AN: …`, Zeile 2 `ENTSCHEIDUNG NÖTIG: …`, Schluss genau eine Zeile `Meldung <Nr> liegt auf Fi-Austausch, Commit <sha>`. Neue Funde außerhalb des Auftrags → `SAMMELLISTE_NEU.md`. VERBRAUCHSLOG Phase 0: `/mnt/project-files/phase0_csharp/VERBRAUCHSLOG_P0.md`.
- Nach jedem Patch: Handoff für einen neuen Chat (`START_Chat.md` aktualisieren) und diese Datei aktuell halten.

## Aktueller Stand
- **Phase 0 und Phase 1 abgenommen und gemergt** (Phase 0: main 2f9a971, A 0.1.0; Phase 1: PR #2, Squash **f97beeb**, A 0.2.0, CI auf main 8/8 grün). Nico hat A 0.2.0 auf Windows-VM, Ubuntu-VM und S24 getestet: nichts Nennenswertes. Der Zweig `phase1-kern` existiert nicht mehr; Arbeit immer auf einem neuen Zweig ab `main`.
- **Phase 2 (Lernkern) im Bau** (Weisung P2-2_Plan_Freigabe, Fi-Austausch d5da1a1; Plan `Fi-Austausch/weisungen/Plan_Phase2_CSharp.md`). Pakete L1, L2, … (nicht K). Wellen: W1 L1–L3, W2 L4–L6, W3 L7–L8, W4 L9–L10, W5 L11–L13; je Welle Zweig ab `main`, Meldung, Merge erst nach Abnahme + Nicos OK. Kein Release, kein Tag in Phase 2. Nico nutzt bis Ende des Umbaus keins der Programme.
  - **W1 (Zweig `phase2-kern`, ohne Oberfläche, ohne neue Nutzertexte):** L1 `Core/Lernen/` (Status.cs, Wiederholung.cs, Reihenfolge.cs, Trainer.cs), L2 `Daten/Uhr.cs` + `Daten/LernstandSchreiber.cs` (Lernstand.cs unverändert), L3 `Daten/Uebernahme*.cs` + `Daten/Loeschmarken.cs` (Variante b: zusammenführen wie Python, Vorschau, automatische Sicherung `sicherungen/vor_uebernahme_*.db`, wiederholbar; Quellen .fisisicherung, lernstand.json.gz, alte Python-DB nur lesend). Vergleichswerte G15 wiederholung, G16 reihenfolge, G17 trainer, G20 uebernahme (+ Erzeuger pruefsumme_sonderfall, Sammelliste 15). Absturzprobe per Kindprozess `tests/.../AbsturzProbe.cs`.
  - Uhrabweichung (`uhr_abweichung`) rechnet erst L10 (Abgleich) ein; die Übernahme schreibt sie nicht.
  - Weitere Entscheidungen P2-2: Abgleich über neues privates Repo nur für C# (Nico legt es an, wenn W4 ansteht; CC meldet vorher; Sammelliste 16 = Weg ohne Repo). PC-Wortlaut überall, Ausnahme „Dashboard“ (PC) / „Start“ (Handy); Wortlisten liefert Chat vor W2/W4/W5. Farbregler in W5 (L12). Falscher Dateiname in MainView/AlterLernstand.cs Z. 12 erst in W2/L5 als Diff zur Lesung.
- Phase 1 im Rückblick (Nico 20.10.): 1a DB wie Python, 2a spätere Bereiche sichtbar mit „Kommt in einer spaeteren Ausbaustufe“, 4a „Alte Sicherung pruefen“ unter Optionen > Sicherung. Hinweiskarte bei `DashboardZustand.Kaputt`: „Der Lernstand konnte nicht vollständig gelesen werden. Er wurde nicht verändert.“
  - Core: K1 `Inhalte` (G12), K2 `Daten/Lernstand.cs` (G11, `PRAGMA user_version` 1), K3 `Daten/Einstellungen.cs` (G11; nach Lesefehler `NurAnzeige`, nichts schreiben), K4 `Diagnose` (G10), `Design/EigeneFarben.cs` (G6b), `Texte/*` (G9), `Dashboard/DashboardWerte.cs` (G13, nur lesend), `Mathe/PythonPow.cs` (G14).
  - **Potenz immer mit `PythonPow.Pow`**, nie `Math.Pow`, wo Python `**` rechnet: Math.Pow weicht unter Windows/macOS im letzten Bit ab (Nachbau glibc-pow, ARM Optimized Routines, MIT). Ausnahme Farbwelt.cs (Phase 0, abgenommen, G6 überall grün).
  - App: Schriftstufen über Ressourcen `Schrift.*` (`Design/Schrift.cs`), Farben `Pinsel.*`. Seitenleiste ab `MainView.BreitGrenze` = 720 × Schriftfaktor, darunter Handy-Aufbau. Start ohne Bedienung prüft je Stufe 384×832, Grenze und 1360×880 (Layoutprüfung: Wort im Wort, Knopf aus Karte, Rand), rot ab 1000 ms oder Fund (Rückgabe 3).
- Stoppgrenze (Regel A, gilt in Phase 2 unverändert): `SicherungsPruefung.cs`, `AlterLernstand.cs`, das Verhalten von `Einstellungen.cs` und G1/G2/G6 nicht ändern ohne Diff zur Lesung durch Chat; Erweiterungen von `DashboardWerte.cs`, `EigeneFarben.cs`, `PythonPow.cs` liest Chat als R3-Diff vollständig; schreibende Änderung an `Lernstand.cs` und neue Nutzertexte mit Verhaltenszusage zuerst zu Chat.
- Tests mit Schreibschutz (2 in `LernstandTests`, 1 in `EinstellungenTests`, 1 in `SpeichernTests`) gelingen nur ohne Root; im Container als Benutzer `claude` in einer Kopie prüfen (`runuser -u claude`, Kopie unter `/tmp/kopie_claude`; CI läuft ohne Root).
- Sicherungsprüfung (A3): Ausgabe nur Zählwerte/Zusammenfassung/Prüfsumme ja-nein/SHA-256, Fehler nur Code; Markertest `SicherungsPruefungTests` darf nie gelockert werden.
- Schalter: `LERNPLATTFORM_MESSUNG_AUTO=1`, `LERNPLATTFORM_IME=0` / `--ohne-eingabemethode`, `LERNPLATTFORM_SOFTWARE=1` / `--software`, `LERNPLATTFORM_UPDATE_AUTO`, `LERNPLATTFORM_DATEN`.
- Offen (Sammelliste Nr. 9–12): NVDA, echte Sicherung, B-Sprung nur CI, Windows > 100 % Skalierung, echte Linux-Hardware, Ladezeit S24, macOS ohne Start, Lizenzen Inter/AndroidX.SplashScreen/Velopack-Teile vor erster Veröffentlichung, AppImage-Ausführungsrecht.
- Für Phase 2 vorgemerkt: Sammelliste 13 (Texte, s. o.), 14 (Farbregler, W5), 7 (Karteikarten-Ansicht: Zeilenanpassung beim Neubau prüfen). Nächste freie Sammelliste-Nummer: 17.
- Dashboard rechnet bei jedem erneuten Zeigen neu (`DashboardSeite.Neu`, `Geladen`); der Start ohne Bedienung wartet darauf vor der Layoutprüfung. Fotos lokal: Patch `/mnt/project-files/phase1_csharp/fotos_k10/nur_lokal_fotos_autolauf.diff` (nie einchecken).
- Hinweiszeile in Meldungsdateien: `Meldung <Nr> liegt auf Fi-Austausch, Commit siehe origin/main` (Regel ab Weisung P1-4); echte sha nur in der Zeile an Nico.
- **W2 (Zweig `phase2-lernen`, Weisung P2-3 5307044):** Schritt A gemeldet (P2-3). Diffs D1 (`.fisisicherung` statt `lernstand.json.gz` in MainView + Kommentar AlterLernstand.cs Z. 12) und D2 (Vorschau in `:memory:` statt Temp-Datei) erst nach Chat-Freigabe pushen. L4/L6 nur mit Python-Texten zeichengleich vorbauen; L5-Texte warten auf Wortlaut (P2-4). Neuer Vergleichswert G22 `lerntexte.json` (G21 = Abgleich). Spielstände zurückholen = Plan E5, nicht Phase 2.
- Letzte Meldung P2-3 (W2 Schritt A), nächste P2-4. Log `/mnt/project-files/phase2_csharp/VERBRAUCHSLOG_P2.md`.
