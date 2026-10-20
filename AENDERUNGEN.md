# Änderungen

## A 0.1.0 (in Arbeit, Phase 0: Fundament)
- Neues Projekt in C# (.NET 10, Avalonia 12.1) mit fünf Teilen: Core, App, Desktop, Android, Tests.
- Zufall exakt wie in der Python-Version (Mersenne Twister mit Text-Startwerten und Spiel-Würfel), geprüft gegen Vergleichswerte.
- Lerninhalte, alte Lernstand-Datenbank und alte Sicherungsdatei werden gelesen, geprüft gegen Vergleichswerte aus der Python-Version.
- Die 72 Farbwelten (6 Grundfarben x 6 Hintergründe x Hell/Dunkel) werden wie in Python berechnet, geprüft gegen Vergleichswerte.
- Messseite für Spike A (nur Phase 0): misst Start, erstes Öffnen einer Seite, Wechsel Hell/Dunkel, alle 72 Farbwelten, Seitenwechsel und Arbeitsspeicher; Ergebnis per Knopf kopierbar.
- Messseite: Knopf „Alte Sicherung prüfen“ liest eine Sicherung oder Datenbank der alten App nur lesend und zeigt nur Zählwerte und Prüfsummen; die Prüfsumme der Sicherung wird wie in Python nachgerechnet.
