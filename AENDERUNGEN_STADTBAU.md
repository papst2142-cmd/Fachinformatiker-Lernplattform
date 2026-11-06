# Änderungen – Fachinformatiker Stadtbau

Das Stadtbau-Werkzeug ist ein eigenes Programm neben der Lernplattform (Windows-PC und Android-Tablet). Damit werden
Häuser und Innenräume der Spielstadt gebaut; die fertige Datei wird geprüft und in die Lernplattform eingebaut.

## S 0.1.0

Erste Fassung des Werkzeugs: Innenräume umbauen.

- Neues Projekt anlegen („Stadt 1 überarbeiten“ oder „Innenraum überarbeiten“), Projekte öffnen und speichern. Jede Änderung wird sofort gespeichert; zusätzlich liegen die letzten 20 Stände als Sicherung bereit.
- Rückgängig und Wiederholen ohne Grenze, solange das Projekt offen ist; die letzten 200 Schritte bleiben auch nach dem Schließen erhalten.
- Draufsicht der Stadt: ein Haus mit Innenraum antippen öffnet ihn (die Stadt außen zu bauen kommt mit der nächsten Version).
- Innenräume von Möbelhaus, Elektromarkt, Bank, Amt und Kaufhaus: Wände und Türen, Böden, Möbel aus dem Katalog setzen, verschieben (Raster 0,5, fein 0,1), drehen, kopieren und löschen, Waren zuordnen.
- Prüfungen in einfachen Worten: Überschneidungen, ob jede Ware und jede Person zu Fuß erreichbar ist, ob man hinter den Tresen kommt, ob das Sortiment vollständig ist, und die Grenzen der Grafik (grün, gelb ab 80 %, rot darüber). Antippen eines Funds springt an seine Stelle.
- „Datei speichern“ legt die geprüfte Stadt-Datei (.fistadt) ab, „An CC senden“ schickt sie zum Einbau. Solange etwas rot ist, geht beides nicht.
- Auf einem Handy zeigt das Werkzeug nur einen Hinweis: es ist für Tablet und PC gedacht.
