# Änderungen – Fachinformatiker Stadtbau

Das Stadtbau-Werkzeug ist ein eigenes Programm neben der Lernplattform (Windows-PC und Android-Tablet). Damit werden
Häuser und Innenräume der Spielstadt gebaut; die fertige Datei wird geprüft und in die Lernplattform eingebaut.

## S 0.7.0

Du kannst jetzt direkt in der 3D-Ansicht bauen, so wie in „Stadt von oben“.

- Links steht dieselbe Liste „Stücke für draußen“, rechts dasselbe „Ausgewähltes Stück“. Darunter findest du wie bisher Tag und Nacht, Jahreszeit, Fest, „Probe laufen“ und die Grenzen.
- Stück links wählen und in die Stadt tippen setzt es. Schon vorher zeigt ein Schatten am Boden, wo es hinkommt: grün heißt „passt hier“, rot heißt „geht hier nicht“, mit dem Grund darunter. Geprüft wird genauso wie in der Liste „Prüfungen draußen“.
- Ein Stück antippen wählt es, auch ein Haus an seiner Wand. Ziehen verschiebt es, der Schatten zeigt dabei den neuen Platz. Drehen, Kopieren und Löschen gehen rechts mit den Knöpfen oder mit den Tasten R, Strg+D und Entf. Ein Doppelklick auf ein Haus öffnet seinen Innenraum.
- Jede Änderung ist ein Schritt, den du mit „Rückgängig“ zurücknehmen kannst, egal ob du sie von oben oder in 3D gemacht hast.
- Die Kamera bewegst du, indem du auf einer freien Stelle ziehst. Mit der rechten Maustaste nach links und rechts drehst du sie, nach oben und unten kippst du sie. Am Tablet zoomst du mit zwei Fingern, zwei Finger gemeinsam hoch oder runter kippen, zwei Finger drehen die Kamera. Unter dem Bild gibt es „+“, „−“, „Ganze Stadt zeigen“, „Steiler“ und „Flacher“; die Tasten Bild hoch und Bild runter kippen auch.
- „Probe laufen“ hat jetzt einen Schalter „Figur laufen lassen“. Ist er an, läuft die Figur dorthin, wo du tippst, und es wird nichts gebaut.

## S 0.6.0

Das Werkzeug aktualisiert sich jetzt auch am Android-Tablet selbst, so wie die Lernplattform am Handy.

- Nach dem Start fragt das Werkzeug bei GitHub nach, ob es eine neuere Version gibt. Gibt es eine, erscheint das Fenster „Neue Version“ mit „Jetzt aktualisieren“ und „Später“. Mit „Nach Updates suchen“ auf der Startseite kannst du jederzeit selbst nachsehen, jetzt auch am Tablet.
- „Jetzt aktualisieren“ lädt die neue Version, prüft sie und übergibt sie an Android. Android schließt das Werkzeug dafür kurz; danach öffnest du es einfach wieder. Deine Projekte bleiben erhalten.
- Geprüft wird alles, bevor etwas installiert wird: die Prüfsumme der Datei (sie muss bei GitHub und in der Liste im Release-Text gleich sein), der Name der App, die Signatur und dass die Version wirklich neuer ist. Stimmt etwas nicht, wird die Datei gelöscht und nichts installiert.
- Beim ersten Update braucht das Werkzeug einmalig die Erlaubnis „Unbekannte Apps installieren“. Ein eigenes Fenster erklärt, wo du sie einschaltest.
- Geht etwas schief (kein Internet, zu wenig Speicher, Android bricht ab), sagt ein Fenster, was los ist, und bietet „Zur Download-Seite“ an. Dort kannst du die neue Version auch von Hand laden.
- Das Werkzeug nimmt nur eigene Versionen, nie eine Fassung der Lernplattform.
- Diese Version (S 0.6.0) installierst du am Tablet noch einmal von Hand. Ab dann kommen neue Versionen von selbst.

## S 0.5.1

Das Werkzeug lässt sich jetzt richtig beenden, und alle Knöpfe sagen genauer, was sie tun.

- Oben rechts gibt es den Knopf „Werkzeug beenden“, am PC und am Tablet. Deine Arbeit ist dabei gespeichert, denn jede Änderung wird sofort gespeichert. Läuft gerade noch eine Prüfung oder die 3D-Ansicht wird neu gebaut, wartet das Werkzeug kurz darauf. Konnte eine Änderung nicht gespeichert werden, fragt es nach: „Speichern und beenden“, „Ohne Speichern beenden“ oder „Abbrechen“. Am PC macht das Fensterkreuz dasselbe. Am Tablet wird die App wirklich geschlossen.
- Klarere Namen für die Knöpfe oben: „Neues Projekt“, „Projekt öffnen“ und „Projekt speichern“ statt „Neu“, „Öffnen“ und „Speichern“. Aus „Datei speichern“ wird „Stadt-Datei für das Spiel speichern“, aus „An CC senden“ wird „Stadt-Datei an CC senden“, aus „Draufsicht“ wird „Stadt von oben“, und der Farbknopf heißt „Dunkle Farben“ bzw. „Helle Farben“.
- Gebäude, die aus einem Blueprint gesetzt wurden, heißen jetzt „Gebäude aus Blueprint“ (vorher „Neues Gebäude“). Außerdem: „Gebäude setzen“ statt „Haus setzen“, „Kopieren“ statt „Kopie“, „Projekt anlegen“ statt „Anlegen“ und „Leere Stadt neu bauen“ statt „Leere Stadt“.
- Jeder Knopf oben und jede Gruppe in der Liste links hat einen kurzen Tipp, was sie tut. Am PC erscheint er, wenn die Maus darauf zeigt. Am Tablet erscheint er, wenn du den Finger etwas länger darauf hältst. Der Knopf wird dabei nicht ausgelöst.
- Auf der Startseite erklärt ein kurzer Text, was ein Projekt ist und was du darin bauen kannst.
- Eine neue leere Stadt ist jetzt ganz frei: nur der Boden, ohne Gras, Büsche und Steine, auch in der 3D-Ansicht. Parks, die du selbst setzt, haben weiter Bäume und Büsche. Leere Städte, die du schon vorher angelegt hast, bleiben, wie sie waren (mit Grün); sie werden nicht still umgestellt.
- Neuer Knopf „Vollbild“ oben: das Werkzeug füllt den ganzen Bildschirm, am PC geht das auch mit der Taste F11, am Tablet verschwinden die Leisten von Android. Beim nächsten Start ist das Vollbild wieder so, wie du es zuletzt hattest.
- Zoomen mit zwei Fingern klappt jetzt immer, in der Ansicht „Stadt von oben“ und im Grundriss, auch wenn der zweite Finger etwas später kommt oder der erste auf einem Stück liegt. Ein Finger auf einer freien Stelle schiebt die Ansicht. Mit zwei Fingern wird nie ein Stück verschoben.
- Die Ansicht „Stadt von oben“ und der Grundriss lassen sich frei bewegen: auf einer freien Stelle ziehen, mit der rechten oder mittleren Maustaste oder mit gehaltener Leertaste auch über Häusern und Bäumen, am Tablet mit zwei Fingern. Zoomen geht mit dem Mausrad, mit zwei Fingern und mit „+“ und „−“. Die Knöpfe „Ganze Stadt zeigen“ und „Ganzen Raum zeigen“ holen alles wieder ins Bild.
- Drehen ist leicht zu finden: Beim gewählten Stück steht „Drehen“ vorn, am PC geht auch die Taste R. Schon beim Setzen einer Straße, eines Parks oder eines Stücks aus dem Katalog gibt es „Drehen“, so lassen sich Straßen auch senkrecht bauen. Häuser und Gebäude aus Blueprints kann das Spiel noch nicht drehen; dort ist der Knopf grau, und ein Satz sagt, warum. Laternen richten sich selbst zur Straße aus, Bäume und Beete sehen von allen Seiten gleich aus.
- Liegen zwei Straßen aufeinander oder so dicht nebeneinander, dass sich die Fahrbahnen überdecken, ist das jetzt ein roter Fund in der Prüfung. Dort hätte im Spiel die Mittellinie gefehlt. Erlaubt sind Kreuzungen und Einmündungen.
- „Rückgängig“ und „Wiederholen“ stehen nach jedem Schritt sofort richtig. Konnte eine Änderung gerade nicht gespeichert werden (zum Beispiel weil ein anderes Programm die Datei kurz festhielt), bleibt sie trotzdem da und kann zurückgenommen werden; unten steht ein Hinweis, und der nächste Schritt speichert wieder.
- Am PC gibt es auf der Startseite den Knopf „Nach Updates suchen“. Er sagt immer, was los ist: neue Version gefunden, du hast die neueste Version, oder warum die Suche nicht geklappt hat.

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
