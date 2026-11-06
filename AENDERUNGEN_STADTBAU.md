# Änderungen – Fachinformatiker Stadtbau

Das Stadtbau-Werkzeug ist ein eigenes Programm neben der Lernplattform (Windows-PC und Android-Tablet). Damit werden
Häuser und Innenräume der Spielstadt gebaut; die fertige Datei wird geprüft und in die Lernplattform eingebaut.

## S 0.2.0

Jetzt lässt sich auch die Stadt draußen bauen, und du siehst sie in 3D.

- Draufsicht: Straßen mit Gehweg, Häuser, Bäume, Parks, Beete, Laternen und den Platz für die Pakete setzen, verschieben (Raster 0,5, fein 0,1), drehen, kopieren und löschen. Ein Doppeltipp auf ein Haus mit Innenraum öffnet ihn.
- Neues Projekt „Leere Stadt“: eine eigene Stadt von Grund auf, Größe 8 bis 64 je Seite.
- Prüfungen draußen in einfachen Worten, wie im Spiel: hat jedes Haus einen Weg zur Straße, steht die Tür auf der Wand, stehen Laternen am Straßenrand, ist am Bahnübergang Platz für den Zug, liegt nichts übereinander. Antippen eines Funds springt an seine Stelle.
- 3D-Ansicht: die Stadt so, wie sie im Spiel aussieht, bei Tag und Nacht, in jeder Jahreszeit und zu jedem Fest. Nach jeder Änderung wird nur der geänderte Teil neu gebaut, alles andere bleibt stehen; so ist das neue Bild schnell da. Mit „Grenzen messen“ siehst du, ob die Stadt für die Grafik nicht zu groß wird.
- Probe laufen: in der 3D-Ansicht mit der Figur durch die Stadt gehen.
- Raumnamen stehen jetzt über der Wand, Waren heißen wie im Spiel, und neue Projekte bekommen einen Namensvorschlag.

## S 0.1.0

Erste Fassung des Werkzeugs: Innenräume umbauen.

- Neues Projekt anlegen („Stadt 1 überarbeiten“ oder „Innenraum überarbeiten“), Projekte öffnen und speichern. Jede Änderung wird sofort gespeichert; zusätzlich liegen die letzten 20 Stände als Sicherung bereit.
- Rückgängig und Wiederholen ohne Grenze, solange das Projekt offen ist; die letzten 200 Schritte bleiben auch nach dem Schließen erhalten.
- Draufsicht der Stadt: ein Haus mit Innenraum antippen öffnet ihn (die Stadt außen zu bauen kommt mit der nächsten Version).
- Innenräume von Möbelhaus, Elektromarkt, Bank, Amt und Kaufhaus: Wände und Türen, Böden, Möbel aus dem Katalog setzen, verschieben (Raster 0,5, fein 0,1), drehen, kopieren und löschen, Waren zuordnen.
- Prüfungen in einfachen Worten: Überschneidungen, ob jede Ware und jede Person zu Fuß erreichbar ist, ob man hinter den Tresen kommt, ob das Sortiment vollständig ist, und die Grenzen der Grafik (grün, gelb ab 80 %, rot darüber). Antippen eines Funds springt an seine Stelle.
- „Datei speichern“ legt die geprüfte Stadt-Datei (.fistadt) ab, „An CC senden“ schickt sie zum Einbau. Solange etwas rot ist, geht beides nicht.
- Auf einem Handy zeigt das Werkzeug nur einen Hinweis: es ist für Tablet und PC gedacht.
