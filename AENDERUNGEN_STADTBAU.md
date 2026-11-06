# Änderungen – Fachinformatiker Stadtbau

Das Stadtbau-Werkzeug ist ein eigenes Programm neben der Lernplattform (Windows-PC und Android-Tablet). Damit werden
Häuser und Innenräume der Spielstadt gebaut; die fertige Datei wird geprüft und in die Lernplattform eingebaut.

## S 0.5.0

Ein fertig gestaltetes Gebäude lässt sich jetzt als Blueprint (Vorlage) speichern und in jedes Projekt setzen, auch in eine leere Stadt.

- Gebäude in der Draufsicht antippen, dann „Als Blueprint speichern“ und einen Namen eingeben (Umlaute erlaubt). Gespeichert werden Modell, Tür, Fußweg, Schild und der Innenraum mit der ganzen Einrichtung und den Stücken aus dem Katalog.
- Links in der Draufsicht steht die Gruppe „Meine Blueprints“: antippen und dann die Stelle auf der Karte wählen. Das Gebäude wird dort gesetzt, der Fußweg reicht bis zur Straße. Es ist ein ganz normales Gebäude: verschieben, löschen und Rückgängig gehen wie bei jedem anderen, und dieselben Prüfungen gelten. Eine belegte Stelle ist rot.
- Blueprints lassen sich umbenennen und löschen (mit Rückfrage). Jede Änderung legt vorher eine Sicherung an. Eine kaputte Datei oder eine Datei aus einer neueren Werkzeug-Version bleibt mit einem Hinweis in der Liste stehen; nichts verschwindet still.
- Der Innenraum eines gesetzten Gebäudes steht im Reiter „Innenräume“ unter „Neue Gebäude“ und lässt sich dort wie jeder Innenraum einrichten und prüfen. Ins Spiel kommt er erst mit den neuen Städten; ein gelber Hinweis sagt das. Projekt und Blueprint behalten ihn.
- Dasselbe Gebäude mit Tür kann vorerst nur einmal je Stadt stehen (ein zweites Mal ist rot), weil das Spiel Aussehen und Tür über den Namen des Modells findet.
- Projekte aus S 0.4.0 lassen sich weiter öffnen. Ein Projekt, das mit S 0.5.0 gespeichert wurde, öffnet S 0.4.0 nur noch zum Lesen.

## S 0.4.0

Was du baust, siehst du jetzt in der 3D-Ansicht genau so, wie es später im Spiel aussieht.

- Gehwege an Straßen, Stücke aus dem vollen Katalog und Effekte erscheinen in der 3D-Ansicht, bei Tag und Nacht; ein Effekt leuchtet nachts. Die Grenzen zählen dabei genau das, was gezeichnet wird.
- Neue Prüfungen draußen: ein Stück aus dem Katalog darf nicht in einem Haus, auf einem Baum, an einer Laterne, auf der Fahrbahn oder auf dem Gleis stehen. Ein Effekt darf nicht in einem Haus stehen.
- Die Laternen werden jetzt auch für einen späten Spielstand geprüft, in dem alle Orte zu sehen sind. In der heutigen Stadt 1 ist dabei die Laterne Nr. 20 rot: sie muss an eine freie Stelle geschoben werden, bevor die Stadt exportiert werden kann.

## S 0.3.0

Jetzt steht der ganze Vorrat an freien Grafiken (CC0) zum Bauen bereit: rund 1 500 Stücke aus 23 Paketen.

- „Voller Katalog“ links in der Draufsicht und bei den Innenräumen: Gruppen wie Möbel, Küche und Restaurant, Bäume und Natur, Häuser und Bauteile, Straßen und Schienen, Fahrzeuge, Technik und Freizeit, Feste, Tiere, Figuren und Effekte, jeweils mit kleinem Vorschaubild in gleich großen Kacheln. Die Suche findet Stücke mit deutschen und englischen Wörtern (zum Beispiel „Stuhl“ oder „chair“).
- Ein Stück antippen und dann die Stelle im Grundriss oder auf der Karte: es wird gesetzt und lässt sich wie alle anderen Stücke verschieben, drehen, kopieren und löschen. Draußen lassen sich Breite und Tiefe ändern.
- Effekte (Funken, Rauch, Feuer und Ähnliches) sind nur Aussehen: sie versperren keinen Weg und erscheinen im Plan als leuchtender Stern, klar anders als eine Laterne.
- Die Prüfungen kennen die neuen Stücke: Überschneidungen und Wege werden wie bei den anderen Möbeln geprüft, und die Grenzen der Grafik zählen jedes Stück mit seiner echten Größe.
- In der 3D-Ansicht erscheinen die neuen Stücke erst, wenn sie ins Spiel eingebaut werden; ihre Größe zählt aber schon jetzt in den Grenzen.
- Ortsnamen in der Draufsicht bleiben immer ganz auf der Karte (vorher ragte „TALHEIM“ am Tablet über den Rand). Gekürzte Hausnamen zeigen ihren vollen Namen, wenn du mit der Maus darüber fährst oder das Haus antippst.

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
