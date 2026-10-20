# Änderungen

## A 0.1.0 (in Arbeit, Phase 0: Fundament)
- Neues Projekt in C# (.NET 10, Avalonia 12.1) mit fünf Teilen: Core, App, Desktop, Android, Tests.
- Zufall exakt wie in der Python-Version (Mersenne Twister mit Text-Startwerten und Spiel-Würfel), geprüft gegen Vergleichswerte.
- Lerninhalte, alte Lernstand-Datenbank und alte Sicherungsdatei werden gelesen, geprüft gegen Vergleichswerte aus der Python-Version.
- Die 72 Farbwelten (6 Grundfarben x 6 Hintergründe x Hell/Dunkel) werden wie in Python berechnet, geprüft gegen Vergleichswerte.
- Messseite für Spike A (nur Phase 0): misst Start, erstes Öffnen einer Seite, Wechsel Hell/Dunkel, alle 72 Farbwelten, Seitenwechsel und Arbeitsspeicher; Ergebnis per Knopf kopierbar.
- Messseite: Knopf „Alte Sicherung prüfen“ liest eine Kopie der Sicherung der alten App nur im Speicher und zeigt nur Zählwerte je Tabelle, Zusammenfassung, „Prüfsumme stimmt ja/nein“ und SHA-256; keine Namen, Pfade, Inhalte oder Fehlertexte (Auflagen Weisung CC P0-3).
- Updates am PC mit Velopack (Windows-Setup, Linux-AppImage, macOS). Die Paketversion ordnet A < B < 1.0 (A 0.1.0 = 1.0.0-a.0.1.0). Ein Rückschritt wird abgelehnt, der Datenordner bleibt bei jedem Update erhalten.
- Android-Versionscode und Anzeige werden aus Stufe und Version berechnet (A 0.1.0 = 1000100); die CI baut zusätzlich A 0.1.1 mit demselben Testschlüssel für die Update-Probe auf dem Handy.
