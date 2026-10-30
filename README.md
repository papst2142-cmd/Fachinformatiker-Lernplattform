# Fachinformatiker Lernplattform

Lernprogramm für die Prüfungsvorbereitung zum Fachinformatiker (alle vier Fachrichtungen wählbar).
Neubau in C# (.NET 10, Avalonia) der bisherigen Python-Version. Stand: **A 0.24.0, Phase 9 (Fachrichtungen)**.

Copyright (c) 2026 Nico H – Alle Rechte vorbehalten (siehe [LICENSE.txt](LICENSE.txt)).
Bestandteile Dritter: siehe [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).

## Hinweis

Die Fachinformatiker Lernplattform ist ein privates Lernprogramm. Sie ist kein Angebot und kein Produkt einer Industrie- und Handelskammer (IHK) oder einer anderen Prüfungsstelle und wird von diesen weder herausgegeben noch geprüft. Die Aufgaben, Szenarien und Testprojekte sind eigene Übungsaufgaben. Sie enthalten keine offiziellen Prüfungsaufgaben. Bezeichnungen wie „AP1“, „AP2“ oder „Note nach IHK-Schlüssel“ beschreiben nur, wofür geübt wird und nach welchem Notenschlüssel die Übung ausgewertet wird.

Erstellt mit Hilfe von Claude (Anthropic).

## Aufbau

| Ordner | Inhalt |
|---|---|
| `src/Lernplattform.Core` | Logik ohne Oberfläche (Lernen, Rechner, Prüfung, Spiel-Kern, Daten) |
| `src/Lernplattform.App` | gemeinsame Oberfläche für PC und Handy (Avalonia) |
| `src/Lernplattform.Desktop` | Start für Windows, Linux, macOS |
| `src/Lernplattform.Android` | Start für Android |
| `tests/Lernplattform.Tests` | Tests (xUnit), Vergleichswerte aus der Python-Version in `tests/Vergleich` |
| `inhalte/` | Lerninhalte (JSON, aus der Python-Version übernommen) |
| `werkzeuge/vergleich/` | Python-Skripte, die die Vergleichswerte erzeugen |

## Linux: wenn etwas nicht stimmt

- **Bildfehler im Fenster** (beobachtet nur über eine VNC-Konsole einer virtuellen Maschine, über RDP nicht): das Programm mit dem Schalter `--software` starten, z. B. `./Fachinformatiker-Lernplattform.AppImage --software`. Es zeichnet dann ohne Grafikkarte. Gleiche Wirkung: Umgebungsvariable `LERNPLATTFORM_SOFTWARE=1`. Standard bleibt das normale Zeichnen.
- **Tippen hängt** (Eingabemethode IBus): mit `--ohne-eingabemethode` starten (oder `LERNPLATTFORM_IME=0`).
- **AppImage startet nicht per Doppelklick:** Nach dem Herunterladen fehlt oft das Recht „Als Programm ausführen“. Rechtsklick → Eigenschaften → „Als Programm ausführen“ einschalten, oder im Terminal `chmod +x Fachinformatiker-Lernplattform.AppImage`. In Phase 0 liegt das AppImage deshalb zusätzlich als `.tar.gz` bei; beim Entpacken bleibt das Recht erhalten.

## Bauen und testen

```
dotnet build src/Lernplattform.Desktop
dotnet test --project tests/Lernplattform.Tests
```
