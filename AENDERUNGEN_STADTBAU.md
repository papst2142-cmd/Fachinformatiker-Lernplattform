# Änderungen – Fachinformatiker Stadtbau

Das Stadtbau-Werkzeug ist ein eigenes Programm neben der Lernplattform (Windows-PC und Android-Tablet). Damit werden
Häuser und Innenräume der Spielstadt gebaut; die fertige Datei wird geprüft und in die Lernplattform eingebaut.

## S 0.44.0

Neuer begehbarer Kleiderschrank und neue Kleidung im gemeinsamen Teil.

- Der begehbare Kleiderschrank sieht in der 3D-Ansicht und im Grundriss eines Innenraums jetzt so aus wie im Spiel: eine Schrankwand mit Spiegel und Kommode. Er ist genauso groß wie vorher; Prüfung und Wege ändern sich nicht.
- Personen in Innenräumen zeigen ihre Frisur jetzt auch in 3D.
- Im Werkzeug ändert sich sonst nichts an der Bedienung.

## S 0.43.0

Ansichten robuster, sauberes Fehlerprotokoll und Busfahrten im gemeinsamen Teil.

- Alle Ansichten des Werkzeugs (3D-Ansicht, Grenzen, Quests, Laternen und Türen) zeigen eine halb fertige Stadt jetzt mit einer gemeinsamen Einstellung „nachsichtig“: ein fehlerhafter Auftrag oder eine Laterne ohne freien Platz lässt nur diese Stelle aus. Die Prüfung meldet beides weiter rot, und das Spiel lädt so eine Stadt nicht.
- Ein Fehler der 3D-Ansicht steht nur noch einmal je Ursache in der Datei fehler.log (vorher bei jeder Änderung der Stadt erneut). So bleibt die Datei klein und „An CC senden“ zeigt das Wichtige.
- Der gemeinsame Teil mit dem Spiel kann jetzt Busfahrten berechnen (Weg zur Haltestelle, Warten, Fahrt). Im Werkzeug ändert sich dadurch nichts an der Bedienung.

## S 0.42.1

Fehler aus der Gesamtprüfung behoben.

- Ein runder Hügel sieht von allen Seiten gleich aus: der Knopf „Drehen“ ist bei ihm jetzt grau (mit Grund als Tipp), und der Drehgriff erscheint nicht mehr. Ein länglicher Hügel dreht wie bisher.
- Werden mehrere Stücke zusammen gedreht, dreht ein länglicher Hügel jetzt auch seine Richtung mit (vorher wanderte nur seine Mitte).
- Findet eine Straßenlaterne an ihrer Straße keinen freien Platz, zeigt die 3D-Ansicht die Stadt trotzdem (ohne diese Laterne) statt beim alten Bild mit „Fehler“ stehen zu bleiben. Die Prüfung meldet die Laterne weiter rot.

## S 0.42.0

Gelände: Hügel setzen und Gelände malen, dazu Verbesserungen aus der Teststadt.

- In „Natur und Licht“ gibt es den Knopf „Hügel“: ein runder Hügel wird auf die Wiese gesetzt. Unter „Ausgewähltes Stück“ lassen sich Radius (1 bis 3), Höhe (0,2 bis 1,5) und Form (rund oder länglich bis 6 lang) einstellen; ein länglicher Hügel dreht in 15-Grad-Schritten. Jede Änderung ist ein Schritt und lässt sich rückgängig machen.
- „Gelände malen“ mit drei Pinseln: Heben, Senken und Glätten, in vier Größen. Jeder Zug mit dem Finger oder der Maus ist ein Schritt.
- Die Ansicht von oben zeigt Hügel mit Höhenlinien und Höhenzahlen; die Ebene „Gelände“ blendet sie aus. Die 3D-Ansicht zeigt das Gelände wie das Spiel.
- Neue Prüfungen (rot): um jeden Hügel bleibt ein Rand von 1,5 frei zu Häusern, Straßen, Wegen, Gleis, Wasser und Plätzen; keine Laterne und kein Questgeber auf einem Hügel; Bäume und Stücke nicht zu schief am Hang; kein Ort, kein Questgeber und keine Haltestelle darf wegen eines zu steilen Hangs unerreichbar sein; höchstens 16 Hügel und 6 000 Dreiecke für das Gelände.
- Die Rückseite eines Gebäudes mit Weg malt die benutzte Tür des Modells nicht mehr zu. „Tür dazu“ setzt die Tür auf eine passende Stelle der Fassade.
- Kopien von Bahnhof und Wohnung behalten ihre Grafik. Ein Bahnhof mit Bahnsteig rastet am Gleis ein; ein Bahnsteig im Modell, der zu nah am Gleis liegt, ist rot.
- „Laternen neu verteilen“ verteilt auch nach dem Öffnen einer Stadt-Datei alle automatischen Laternen neu und zählt nur, was sich wirklich ändert.
- Ein gelber Hinweis erscheint, wenn der Name eines Questgebers mit einer Anrede beginnt (zum Beispiel „Frau“).

## S 0.41.0

Quests und Questgeber kommen ins Spiel.

- Die Lernplattform liest jetzt die Quests und Questgeber aus der Stadt-Datei. Der gelbe Hinweis „erscheint erst mit dem Einbau“ fällt darum weg.
- Die Prüfung nutzt zusätzlich die Ladeprüfung des Spiels: was das Spiel so nicht laden würde, steht rot in der Liste und sperrt Speichern und Senden.
- Personen draußen zeichnet die 3D-Ansicht jetzt selbst wie das Spiel; „Grenzen live“ zählt sie dort genau einmal mit.

## S 0.40.0

Quests und Questgeber im Werkzeug.

- Neuer Reiter „Quests“: alle Quests des Spiels in einer Liste mit Suche und Filtern (Typ, Questgeber, Ort). Vorhandene Quests lassen sich ansehen, einer anderen Person geben („umhängen“) und als neue Quest kopieren; ihre Texte bleiben unverändert. Neue Quests sind vom Typ „Auswahl“ (eine von mehreren Antworten ist richtig) oder „Zuordnung“ (Begriffe einander zuordnen) und haben Titel, Ticket, Frage, Antworten bzw. Paare, Hilfe, Erklärung, Thema, Geld, Ruf, „Frei ab Tag“ und „Erst nach“.
- Neuer Reiter „Questgeber“: die Personen, die Quests vergeben. Eigene Personen mit Name, Rolle und Aussehen anlegen; jede Person drinnen in einen Innenraum oder draußen auf die Karte stellen (höchstens 12 draußen). Die Personen aus dem Spiel lassen sich nur verschieben und wieder an ihren alten Platz stellen.
- In der Spielgeschichte zeigen die Zusammenhänge die Aufgaben wahlweise „je Ort“ oder „je Questgeber“. Einen Pfeil von einer Quest auf eine neue Quest ziehen setzt „Erst nach“.
- Eine Prüfung zeigt rot, was so nicht ins Spiel kann (zum Beispiel Quest ohne Questgeber, leere Antwort, Kreis bei „Erst nach“, Person auf einem Haus oder nicht erreichbar). Personen draußen zählen in „Grenzen live“ mit.
- Quests und Questgeber stehen schon in der Stadt-Datei. Im Spiel erscheinen sie erst mit dem Einbau; ein gelber Hinweis sagt das.

## S 0.39.0

Versionsanzeige und Hinweis auf die neuen Grafikstufen der Lernplattform.

- Die Version des Werkzeugs (zum Beispiel „S 0.39.0“) steht jetzt immer klein rechts oben in der Titelzeile, auf der Startseite und beim Bauen. Vorher stand sie nur im Menü „Einstellungen“.
- Die Leiste „Grenzen live“ prüft die Stadt weiter gegen die Grenzen der Stufe Mittel (Vorgabe im Spiel). Neu steht darunter eine Zeile, wie viel die Stadt von den Grenzen der Stufen Niedrig und Hoch ausschöpft, zum Beispiel „Niedrig 81 % · Hoch 70 %“. Ein roter Fund „Stadt zu teuer“ entsteht wie bisher nur über der Grenze von Mittel.

## S 0.38.0

Umbau im Inneren, an der Bedienung ändert sich nichts.

- Der gemeinsame Teil mit dem Spiel (Stadt bauen, prüfen, zeichnen) ist jetzt ein eigener Baustein, die „Engine“. Das Werkzeug ist ihr Editor. Städte, Prüfungen und die 3D-Ansicht bleiben genau wie bisher.

## S 0.37.1

Kleine Absicherung im gemeinsamen Teil mit dem Spiel.

- Die Liste der mitgelieferten Baukasten-Stücke und die Datei, welche Fassung einer Stadt gilt, tragen jetzt eine Nummer für ihren Aufbau. Ein zu neuer Aufbau wird klar gemeldet statt still gelesen. An der Bedienung ändert sich nichts.

## S 0.37.0

Sparsamere Autos.

- Die Autos in der 3D-Ansicht und auf den Parkplätzen bestehen aus rund einem Viertel bis einem Drittel weniger Dreiecken (Räder mit 12 statt 24 Ecken, unsichtbare Flächen unter dem Wagenboden weg). Sie sehen aus wie bisher.
- „Grenzen live“ zählt dadurch weniger: ein voller Parkplatz mit 12 Plätzen kostet rund 7 700 Dreiecke weniger.

## S 0.36.0

Bushaltestellen, Linienbus und Parkplätze.

- Neu in der Gruppe „Straßen und Wege“: die Knöpfe „Haltestelle“ und „Parkplatz“. Tippe neben eine gerade Straße mit Gehweg, auf die Seite, auf der sie stehen sollen. Häuschen, Schild und gelbe Linie bzw. Parkfläche mit Einfahrt legen sich von allein an die Straße.
- Beide lassen sich nur längs der Straße schieben und wandern mit der Straße mit. Wird die Straße gelöscht, fallen sie weg. Kopieren legt eine Kopie längs daneben.
- Beim gewählten Parkplatz stellst du rechts die Zahl der Plätze (2 bis 12) und eine oder zwei Reihen ein. Im Werkzeug sind immer alle Plätze belegt, damit die Grenzen den vollen Parkplatz zählen.
- Der Bus fährt in der 3D-Ansicht von allein alle Haltestellen an, mit der Haltestelle rechts, und hält dort etwa vier Sekunden. Ab sieben Haltestellen fahren zwei Busse.
- Neue Prüfregeln (alle rot): nicht an einer geraden Straße mit Gehweg, zu nah an Kreuzung, Kreisverkehr, Zebrastreifen, Ampel oder Bahnübergang, zwei Haltestellen zu nah auf derselben Seite, mehr als acht Haltestellen, Platz des Häuschens oder des Parkplatzes nicht frei, Einfahrt zu nah an Kreuzung oder Haltestelle, der Bus erreicht eine Haltestelle nicht oder bleibt stecken. Die automatischen Laternen halten Abstand zu Schild und Einfahrt.

## S 0.35.0

Ampeln an Kreuzungen.

- Neu in der Gruppe „Straßen und Wege“: der Knopf „Ampel“. Tippe in die Nähe einer Kreuzung oder Einmündung, die Ampel springt genau auf ihre Mitte. Masten, Haltelinien und Furten kommen von allein an jede Straße der Kreuzung. An einem Kreisverkehr gibt es keine Ampel, und an jeder Kreuzung höchstens eine.
- Schiebst du eine Straße der Kreuzung, rückt die Ampel mit. Löschst du sie und es bleibt keine Kreuzung, fällt die Ampel weg. Eine Ampel selbst lässt sich nur an eine andere freie Kreuzung schieben; kopieren und drehen gibt es nicht, sie richtet sich selbst aus.
- Neue Prüfregeln (alle rot): Ampel nicht an einer Kreuzung, am Kreisverkehr, zwei an derselben Kreuzung, eine Straße der Kreuzung zu kurz zum Halten, zu nah an einem Bahnübergang oder Zebrastreifen, Laterne, Baum, Stück oder Haus auf dem Platz eines Masts. Die automatischen Laternen halten Abstand zu den Masten.
- In der 3D-Ansicht schalten die Ampeln wie im Spiel, und die Autos halten bei Rot.
- Der gelbe Hinweis „Selbst gebautes Gebäude …“ an einem Gebäude aus „Als Gebäude übernehmen“ nennt jetzt seinen Namen statt der Kennung „bau:…“.

## S 0.34.1

Türen und Wege genauer, Tippen ohne Warten.

- Tippst du mit „Tür setzen“ an ein Haus, kommt die Tür sofort. Rechnet das Werkzeug die Fassade noch, steht kurz „Fassade wird berechnet …“ und die Tür kommt danach an die getippte Stelle. Vorher konnte das Werkzeug dabei einen Moment hängen.
- Liegt dein Finger auf einer Tür des Hausmodells, landen Tür und Marke immer auf ihrer Mitte, auch wenn du etwas links oder rechts daneben tippst. Vorher rutschte die Tür bei manchen Häusern auf den Rahmen neben der Glastür.
- Der Weg von der Tür zur Straße läuft jetzt gerade aus der Tür, wenn nötig um die Ecke des Hauses, und trifft die Straße zum Schluss quer. Er hat runde Ecken wie ein Fußweg.
- Ein Fund an einer Tür nennt den Namen des Hauses („Kaufhaus“) statt der Kennung („kaufhaus“).
- Ist die Stadt zu teuer, steht dieser Fund in der Prüfliste ganz oben.

## S 0.34.0

Die Grenzen der Stadt live sehen.

- Unten links steht die Leiste „Grenzen live“. Sie zeigt nach jeder Änderung, wie nah die Stadt an den Grenzen des Spiels liegt (Dreiecke und Zeichenaufrufe), für die Gesamtansicht und für die teuerste Stelle aus der Nähe. Die Zahl vorne sagt, welcher Wert den Balken füllt, in Klammern steht der andere. Der Balken ist grün, ab drei Vierteln gelb und über der Grenze rot.
- Liegt die Stadt über der Grenze, zeigt die Prüfliste den roten Fund „Stadt zu teuer“ mit der Stelle. Solange er da ist, sind „Stadt-Datei speichern“ und „An CC senden“ gesperrt.
- Neu: die Wärmekarte (Knopf oben über der Karte oder Taste W). Sie färbt die Stadt von oben je Feld nach der Zahl der Dreiecke, von Blau bis Rot. Nur zum Ansehen, sie ändert nichts.
- Die 3D-Ansicht zeichnet oft vorkommende Dinge (Büsche, Steine, Geschenke) wie das Spiel in einem Zug.

## S 0.33.0

Türen frei setzen und Funde in der Prüfliste antippen.

- „Tür setzen“ legt die Tür jetzt auf die Wand, die du siehst, auch wenn das Haus vorne Schaufenster, Nischen oder Vorsprünge hat. Tippst du vor ein Schaufenster und „Einrasten“ ist an, rückt die Tür auf die nächste glatte Stelle derselben Wand oder auf eine Tür des Hausmodells; ein gelber Hinweis sagt, dass sie gerückt ist. Gibt es an der Wand keine glatte Stelle, sagt das der Hinweis, und du wählst eine andere Wand.
- Die Tür steht immer ganz auf einer Wand, mindestens 0,3 Felder von der Ecke. Ziehst du sie über das Ende der Wand hinaus, bleibt sie an dieser Wand, bis dein Finger wirklich vor der Nachbarwand steht.
- An selbst gebauten Häusern wird beim Tippen auf ein Fenster im Erdgeschoss das Fenster zur Tür (die alte Tür wird zum Fenster). Das ist ein Schritt, „Rückgängig“ stellt beides wieder her.
- Jeder Fund in der Prüfliste mit einer Stelle zeigt rechts einen Pfeil. Antippen springt hin: in der Draufsicht steht die Stelle mittig und das Stück dort ist gewählt, in der 3D-Ansicht fährt die Kamera hin (die Neigung bleibt), im Innenraum steht die Stelle mittig, im Gebäude-Editor wird die Ebene des Teils gezeigt und das Teil gewählt. Funde ohne Stelle haben keinen Pfeil und lassen sich nicht antippen.
- Fehler behoben: Ein Fund an einer Tür nannte statt des Orts manchmal den Namen der Firma aus dem Probestand („Probe“). Jetzt steht dort der Name des Orts aus der Karte.

## S 0.32.0

Die 3D-Ansicht zeichnet die Stadt so sparsam wie das Spiel.

- Häuser außerhalb des Bilds werden nicht gezeichnet. Weit herausgezoomt zeigen Häuser und Festschmuck eine vereinfachte Form, von nah bleibt alles wie bisher.
- „Grenzen messen“ zeigt dadurch kleinere Zahlen: die heutige Stadt braucht in der Gesamtansicht (Nacht, Winter, Weihnachten) rund 40 Prozent weniger Dreiecke.

## S 0.31.0

Häuser färben, richtige Türen, Fabrikhallen bauen und große Katalog-Stücke als Gebäude übernehmen.

- Neuer Knopf „Farbe“ unter „Ausgewähltes Stück“ an jedem Haus: Fassade, Dach und Fensterrahmen bekommen eine von 12 festen Farben oder eine eigene Farbe (drei Regler mit Vorschau). Die Tür gibt es in Braun, Schwarz, Weiß und Cremefarben. „Wie vorher“ nimmt die Farbe wieder weg. Jede Wahl ist ein Schritt, die Ansicht in 2D und 3D folgt sofort. Auch im Gebäude-Editor gibt es die Farbwahl; ein Blueprint nimmt seine Farben mit.
- Die Tür eines selbst gebauten Hauses sieht wie eine Tür aus: farbiges Türblatt mit Klinke, bündig in der Wand.
- Im Gebäude-Editor gibt es die Gruppe „Industrie (Halle)“: Hallenwände, Fenster, Tor, Ecken, Boden und Dach der Fabrik auf einem feineren Raster (ein Drittel Feld). Ein Gebäude ist entweder ein Wohnhaus oder eine Halle; beides gemischt ist rot. Neue Gruppe „Dachaufsätze“: Schornsteine, Tanks und Solarplatten für beide Arten. Die Grenze von 8 000 Dreiecken bleibt.
- Neuer Knopf „Als Gebäude übernehmen“ an einem Stück aus dem Katalog: das Stück wird ein Gebäude mit Tür, Weg zur Straße und Innenraum. Danach gehen „Tür setzen“, der Innenraum und „Als Blueprint speichern“ wie bei jedem eigenen Gebäude. Ein Effekt kann kein Gebäude werden.
- „Tür setzen“ geht jetzt auch an eigenen Gebäuden mit Innenraum.
- Beim Setzen einer Reihe zeigt die Zahl neben − und + immer den gewählten Abstand.
- Fehler behoben: Stand eine Laterne ohne Straße in der Nähe, blieb die 3D-Ansicht leer.

## S 0.30.0

Mehrere Stücke auf einmal bearbeiten und Stücke als Reihe setzen.

- Neuer Knopf „Mehrere wählen“ oben links (Taste U), in der Karte von oben und in der 3D-Ansicht. Danach nimmt jedes Antippen ein Stück zur Auswahl dazu oder wieder heraus. Ein Finger über freie Fläche zieht einen Rahmen und wählt alles darin.
- Mit der Maus geht das auch ohne den Knopf: Umschalt+Klick nimmt ein Stück dazu, Umschalt+Ziehen über freie Fläche zieht einen Rahmen. Strg+A wählt alle Stücke der Stadt.
- Die gewählten Stücke sind farbig umrandet, ein Schild zeigt, wie viele es sind. Wer eins davon zieht, schiebt alle zusammen (ohne Einrasten). Rechts gibt es „Alle drehen“, „Alle kopieren“, „Alle löschen“ und „Auswahl aufheben“; die Tasten R, Strg+D, Entf und die Pfeiltasten wirken auf alle.
- Jede dieser Aktionen ist ein einziger Schritt: einmal „Rückgängig“ stellt alles wieder her.
- Beim Drehen drehen sich alle Stücke um ihre gemeinsame Mitte um ein Viertel. Ein Kreisverkehr nimmt seine Zufahrten und Laternen mit, Zebrastreifen bleiben auf ihrer Straße. Häuser mit Ort und der Paketplatz lassen sich wie bisher nicht löschen; sie bleiben dann gewählt.
- Bäume und Stücke aus dem Katalog lassen sich als Reihe setzen: beim Setzen den Schalter „Als Reihe setzen“ einschalten, dann Anfang antippen und Ende antippen (oder mit dem Finger in einem Zug ziehen). Abstand mit − und + wählbar, dazu „Beide Seiten“ und „Zufällig drehen“.
- Die Vorschau zeigt jedes Stück der Reihe grün oder rot und rechts die Zahl der Stücke und „+n Dreiecke“. Rote Stellen (auf der Fahrbahn, im Haus, im Wasser, auf dem Gleis, auf einem Fußweg, an einem Baum oder einer Laterne) werden ausgelassen und gezählt. Die ganze Reihe ist ein Schritt; danach sind ihre Stücke gewählt.

## S 0.29.0

Drei neue Pakete im Katalog: eine Fabrik, viel Natur und ein Lagerplatz. Der Katalog hat jetzt 2 073 Stücke aus 26 Paketen.

- Fabrik (143 Stücke, Kenney Factory Kit): Hallenwände, Hallentore, Laufstege mit Treppen, Fließbänder, Rohre aus Stahl und Glas, Maschinen, Kran, Greifarme, Trichter, Kolben, Zahnräder, Schaltpulte, Bodenmarkierungen und Warnleuchten.
- Natur (329 Stücke, Kenney Nature Kit): viele Bäume (auch im Herbst und dunkel), Fichten, Palmen, Sträucher, Blumen, Pilze, Felsen und Steine, Klippen, Wasserfälle, Brücken aus Holz und Stein, Wegplatten, Weidezäune, Äcker mit Mais und Weizen, Zelte, Lagerfeuer und ein Kanu.
- Lagerplatz (80 Stücke, Kenney Survival Kit): Fässer, Kisten, Truhe, Holzzäune mit Tor, Blechplatten, Gerüste aus Holz und Metall, Werkbänke, Werkzeug, Baumstämme, Holz- und Steinhaufen und Wegweiser.
- Alle neuen Stücke haben deutsche Namen und sind über die Suche zu finden, auf Deutsch und auf Englisch.
- Die neuen Stücke stehen in den bekannten Gruppen, zum Beispiel Bäume in „Bäume und Natur“, Hallenwände in „Häuser und Bauteile“ und Maschinen in „Technik und Freizeit“.

## S 0.28.0

Türen selbst setzen, und die leeren Schilder an den Häusern sind weg.

- Unter „Ausgewähltes Stück“ gibt es bei Häusern und Gebäuden den Knopf „Tür setzen“. Danach tippst du eine Wand an, in der Karte von oben oder in der 3D-Ansicht. Die Tür kommt an diese Stelle, mindestens 0,3 Längen von einer Ecke entfernt. Der Weg führt von der Tür gerade hinaus und dann zur nächsten Straße.
- Die Tür lässt sich an der Wand entlang ziehen, auch um die Ecke auf eine andere Wand. Ist das Haus ausgewählt, kannst du die Tür in der Karte von oben auch direkt greifen und ziehen.
- Liegt eine Tür des Hausmodells in der Nähe, rastet die Tür auf ihre Mitte ein (nur mit „Einrasten“ an).
- Setzen und Ziehen sind je ein Schritt und lassen sich rückgängig machen. „Tür setzen beenden“, Escape oder ein Rechtsklick beenden das Setzen.
- Das geht auch bei Orten mit Innenraum, nach „Aussehen tauschen“ und beim Zuhause. Im Spiel kommt die Figur dann durch die neue Tür ins Haus.
- „Tür dazu“ setzt eine neue Tür jetzt auf die Wand, die zur nächsten Straße zeigt (bisher immer auf die Vorderseite).
- Türen sitzen jetzt sauber in der Wand: Rastet eine Tür auf die Tür eines Bauteils oder Hausmodells ein, wird keine zweite Tür mehr davor gezeichnet. Die Prüfung meldet rot, wenn eine Tür nicht sauber in der Wand sitzt, zum Beispiel auf einem Fenster oder an einem Vorbau. Dann die Tür auf eine glatte Wand oder auf die Tür des Modells setzen.
- Die farbigen Platten ohne Schrift an acht Häusern werden nicht mehr gezeichnet, wie im Spiel.

## S 0.27.0

Drei kleine Werkzeuge für die Karte von oben: ein Maßband, Ebenen zum Ein- und Ausblenden und Notizen.

- Oben links über der Karte stehen jetzt die Knöpfe „Wählen“, „Maßband“, „Ebenen ▾“ und „Notiz“.
- Maßband (Taste L): erstes Antippen setzt den Anfang, zweites das Ende, ein drittes beginnt eine neue Messung. Die Länge steht in Feldern mit einer Nachkommastelle (z. B. „3,5 Felder“) über der Linie und rechts in der Spalte. Die Punkte rasten an Ecken und Mitten von Häusern, Straßen und anderen Stücken ein. Das Maßband geht auch in der 3D-Ansicht am Boden. Es ist nur zum Ansehen: Es ändert nichts an der Stadt und ist kein Schritt zum Rückgängigmachen. Escape oder „Maßband wegnehmen“ löscht die Messung.
- Ebenen: In der Liste „Ebenen ▾“ blendest du Bäume und Grün, Laternen, Katalog-Stücke, Beschriftungen, Wasser, Wege, Notizen, Funde und die Zahl der Aufgaben in der Karte von oben aus und wieder ein. Ausgeblendete Stücke lassen sich nicht anwählen. Neben dem Knopf steht, wie viele Ebenen aus sind. Die Wahl bleibt auch nach einem Neustart. Setzt du ein Stück einer ausgeblendeten Ebene, wird sie wieder eingeblendet. Die 3D-Ansicht zeigt immer alles.
- Notizen (Taste N): kleine gelbe Zettel in der Karte von oben, zum Beispiel „hier kommt der Bahnhof hin“. Setzen, Text ändern, verschieben, kopieren und löschen ist je ein Schritt und lässt sich rückgängig machen. Notizen stehen nur in deinem Projekt, nie in der Stadt-Datei und nie im Spiel.

## S 0.26.0

Laternen stellen sich jetzt von selbst an die Straßen, und es gibt Zebrastreifen.

- Legst du eine Straße an oder änderst sie, stellt das Werkzeug an ihr Laternen auf: alle 3,5 Längen eine, abwechselnd links und rechts, in Kurven außen. An Kreuzungen, vor Türen und Wegen, am Bahnübergang, auf Zebrastreifen und am Kreisverkehr bleibt Platz. Steht etwas im Weg, rückt die Laterne ein Stück zur Seite oder fällt weg.
- Selbst gesetzte Laternen bleiben immer stehen; die automatischen halten Abstand zu ihnen. Löschst du eine automatische Laterne, merkt sich das Projekt das, und sie kommt an dieser Stelle nicht wieder. Verschiebst du eine, gilt sie danach als selbst gesetzt. In der Auswahl steht bei einer automatischen Laterne „(automatisch)“.
- Der Knopf „Laternen neu verteilen“ (Gruppe „Natur und Licht“) stellt alle automatischen Laternen an allen Straßen neu auf. Die heutige Stadt 1 behält ihre Laternen, solange du keine Straße änderst.
- Neuer Knopf „Zebrastreifen“ (Gruppe „Straßen und Wege“), in der Karte von oben und in der 3D-Ansicht. Der Zebrastreifen legt sich quer genau auf die Mitte einer geraden Straße und lässt sich nur längs der Straße schieben. Schiebst du die Straße, wandert er mit; löschst du sie, fällt er weg.
- Im Spiel geht die Figur über eine Straße mit Gehweg nur an einem Zebrastreifen. Die Prüfung meldet rot, wenn ein Zebrastreifen in einer Kurve, im Kreisverkehr, auf einer Straße ohne Gehweg, zu nah an Kreuzung, Kreisverkehr, Bahnübergang oder einem anderen Zebrastreifen liegt oder wenn eine Laterne oder ein Stück darauf steht. Kommt die Figur nicht mehr zu einem Ort, schlägt die Prüfung vor, einen Zebrastreifen zu setzen.
- Öffnest du eine leere Stadt über „Projekt öffnen“, schrieb danach jede Änderung draußen eine Fehlerzeile in `fehler.log`, und die Prüfung im Innenraum lief nicht neu. Das ist behoben; es ging dabei nichts verloren.

## S 0.25.1

Deine Projekte liegen am PC jetzt an einem sicheren Ort, und das Werkzeug aktualisiert sich am PC wieder selbst.

- Am Windows-PC lagen Projekte, Blueprints, Sicherungen und Exporte bisher im selben Ordner wie das Programm. Eine neue Installation (Setup) oder das Deinstallieren hat diesen Ordner geleert, die Projekte waren dann weg. Jetzt liegen alle Daten im Ordner `AppData\Roaming\Fachinformatiker-Stadtbau` (wie bei der Lernplattform). Beim ersten Start werden Projekte, Blueprints, Sicherungen, Exporte und Einstellungen aus dem alten Ordner dorthin kopiert. Dabei wird nichts gelöscht und nichts überschrieben.
- „Nach Updates suchen“ fand am PC nie eine neue Version: die Adresse von GitHub war unvollständig. Das ist behoben. Jeder Schritt des Updates steht jetzt in der Datei `update.log` im Datenordner, damit ein Fehler später nachzulesen ist. Ein Fehler bei der Suche zeigt jetzt immer ein Fenster.
- Die Update-Suche hält Störungen bei GitHub aus: Sie lädt nur noch die Angaben zur neuesten Version (vorher die aller früheren Versionen), und antwortet GitHub mit einer Störung, versucht sie es bis zu viermal mit kurzer Pause.
- Im Fenster „Projekt öffnen“ gibt es den Knopf „Stadt-Datei als neues Projekt öffnen“. Er macht aus einer Stadt-Datei (die mit „Stadt-Datei speichern“ oder „An CC senden“ entstand) ein neues Projekt. Ein vorhandenes Projekt wird nie überschrieben; gibt es den Namen schon, bekommt das neue Projekt eine Nummer dazu.
- Mit zwei oder mehr Bildschirmen am PC steht im Menü „Einstellungen“ der Eintrag „Bildschirm für Vollbild“. Dort wählst du, auf welchem Bildschirm das Vollbild erscheint; die Wahl gilt auch beim nächsten Start.

## S 0.25.0

Die Prüfung erkennt jetzt, wenn ein Stück den Eingang eines Hauses versperrt, und die Karte von oben zeigt deutsche Namen.

- Steht ein Stück aus dem Katalog direkt vor einer Tür (auf der Tür oder auf dem ersten Stück des Fußwegs), meldet die Prüfung das rot: im Spiel käme die Figur nicht ins Haus. Das gilt für die Türen aller Orte und Gebäude. Effekte wie der Stern versperren nichts und bleiben erlaubt.
- Gebäude ohne Ort tragen in der Karte von oben ihren deutschen Namen wie in der Liste „Modell“ (zum Beispiel „Stellwerk“ statt „stellwerk“), ein Bahnsteig heißt „Bahnsteig“.

## S 0.24.0

Vorgegebene Gebäude bekommen auf Wunsch eine Tür, und die Bahnhöfe stehen wieder in der Liste.

- Unter „Ausgewähltes Stück“ steht bei jedem Gebäude ohne Innenraum der Knopf „Tür dazu“. Er setzt eine Tür mitten auf die Vorderseite und legt einen Fußweg zur nächsten Straße, wenn nötig um das Haus herum. Steht das Haus nah vor einer Straße, rückt es an den Gehweg wie die Läden. Ein Knopfdruck ist ein Schritt, „Rückgängig“ nimmt ihn zurück.
- „Tür entfernen“ nimmt Tür und Fußweg wieder weg. Das geht auch in Projekten, die schon vorher angelegt wurden.
- Findet der Weg keine Straße, kommt die Tür trotzdem, mit einem gelben Hinweis.
- Steht dasselbe Gebäude zweimal in der Stadt, bekommt das zweite eine eigene Kopie („Wohnung 2“), damit jedes seine Tür hat. Wird das Haus gelöscht, verschwindet die Kopie mit.
- „Bahnhof Talheim“ und „Talheim-Nord“ stehen wieder in der Liste „Modell“. Mit Tür bleibt der Bahnsteig als eigenes Stück stehen. Steht ein Bahnhof weiter als 1,5 vom Gleis, kommt ein gelber Hinweis.
- Die Liste „Modell“ zeigt deutsche Namen wie im Spiel; die Kennung steht im Tipp.
- Rot ist eine Tür, die direkt auf ein anderes Haus oder ins Wasser zeigt.

## S 0.23.1

Ein Fußweg lässt sich jetzt bis an die Eingangstür eines Hauses ziehen.

- Mit „Fußweg ziehen“ rastet der Anfang oder das Ende an der Tür jedes Hauses ein, auch bei gedrehten Häusern, beim Stellwerk und bei eigenen Gebäuden. Der Weg geht dann gerade von der Tür weg. Das gilt in beide Richtungen (von der Tür zum Gehweg und vom Gehweg zur Tür), in der Draufsicht und in der 3D-Ansicht, mit Maus und Finger.
- Bisher landete ein Tipp an der Tür im Raster mitten im Haus, und die Vorschau war rot („führt durch das Haus“).
- Liegen Tür und Gehwegkante nahe beieinander, rastet der Punkt an der näheren von beiden ein. Mit „Einrasten“ aus bleibt der Punkt frei wie bisher.
- Ein Weg durch die Wand neben der Tür oder quer durch das Haus bleibt rot.

## S 0.23.0

Angefangene Projekte lassen sich jetzt löschen.

- Im Fenster „Projekt öffnen“ steht jedes Projekt in einem eigenen Kasten. Den Namen antippen öffnet das Projekt wie bisher, darunter steht „Löschen“.
- Vor dem Löschen fragt das Werkzeug nach; „Abbrechen“ ist vorgewählt. Das Projekt verschwindet erst nach „Löschen“.
- Zur Sicherheit legt das Werkzeug vorher eine Kopie der Projektdatei in den Ordner „sicherungen“ des Projekts (Name beginnt mit „geloescht_“). Klappt die Kopie nicht, bleibt das Projekt und das Werkzeug sagt, warum. Alle anderen Projekte, Sicherungen, Blueprints und gespeicherten Stadt-Dateien bleiben unberührt.
- Ist das Projekt gerade offen, wartet das Werkzeug laufende Arbeit ab. Gibt es eine nicht gespeicherte Änderung, fragt es, ob vorher gespeichert werden soll. Danach zeigt es die Startseite.
- Auch eine beschädigte Projektdatei lässt sich so löschen. Mit der Tastatur löscht die Taste Entf das Projekt, dessen Kasten gerade gewählt ist.

## S 0.22.1

Selbst entworfene Gebäude hindern das Speichern und Senden der Stadt nicht mehr.

- Steht ein Gebäude aus „Gebäude entwerfen“ in der Stadt, frei oder als neues Aussehen eines Orts, lässt sich die Stadt-Datei jetzt speichern und an CC senden. Vorher war das gesperrt.
- Ein gelber Hinweis erinnert in beiden Fällen daran, dass das Spiel diese Gebäude erst zeigt, wenn CC die Stadt einbaut. Rote Funde sperren weiter wie bisher.

## S 0.22.0

Du siehst jetzt, welche Aufgaben der Spielgeschichte an welchem Ort der Stadt spielen. Das Werkzeug zeigt das nur an; an der Spielgeschichte ändert sich nichts.

- In der Draufsicht steht an jedem Haus mit Aufgaben eine Zahl: so viele Aufgaben der Spielgeschichte spielen dort. Wiederkehrende Aufträge und Zwischenfälle zählen nicht mit.
- Wählst du einen solchen Ort, zeigt eine gelbe Zeile, wie viele Aufgaben dort spielen. Verschieben und „Aussehen tauschen“ ändern daran nichts. Rechts steht die Karte „Spielgeschichte an diesem Ort“ mit allen Aufgaben, wiederkehrenden Aufträgen und Zwischenfällen und dem Tag, ab dem sie kommen.
- Neuer Reiter „Spielgeschichte“: Er zeigt die „Zusammenhänge“, je Ort einen Kasten, von links nach rechts nach dem Tag, an dem der Ort ins Spiel kommt. Die Linien zeigen, welche Aufgabe erst nach welcher kommt; sie kreuzen sich nie. „Alle zeigen“ klappt die Aufgaben eines Orts auf, „Alle aufklappen“ alle Orte. Einen Kasten antippen zeigt rechts seine Spielgeschichte. Zoomen und Schieben gehen wie in der Draufsicht, „Alles zeigen“ passt alles ins Fenster.
- „Aufgabe → Ort“ zeigt alle Aufgaben als Liste mit Tag, Ort, Auftraggeber und Vorgänger, mit Suche und Filtern nach Ort, Tag und Person. „Alle in der Spielgeschichte“ auf der Karte eines Orts öffnet die Liste ab dem Tag, an dem der Ort ins Spiel kommt, und hebt seine Aufgaben hervor.

## S 0.21.0

Ein Ort kann jetzt ein anderes Aussehen bekommen.

- Wähle in der Draufsicht einen Ort und tippe rechts auf „Aussehen tauschen“. Die Liste zeigt deine Blueprints mit Tür und die Gebäude der Stadt mit Tür, zum Beispiel das Stellwerk. Tippst du eine Vorlage an, erscheint das neue Haus als Schatten: grün, wenn es passt, rot mit dem Grund, wenn nicht. „Übernehmen“ tauscht das Haus in einem Schritt; Rückgängig geht.
- Name, Aufgaben, Kunde, Innenraum und Spielstände des Orts bleiben. Die Mitte des Hauses bleibt, die Tür zeigt zur selben Seite wie vorher, der Fußweg folgt dem alten Weg.
- Steht das gewählte Gebäude noch als eigenes Haus in der Stadt (z. B. das Stellwerk), sagt das Werkzeug, dass du es zuerst löschen musst. Danach ist es weiter in der Liste.
- Hatte das alte Haus einen Bahnsteig, bleibt er als eigenes Stück am Gleis stehen. Du kannst ihn verschieben oder löschen; ein Ort ohne Bahnsteig ist erlaubt und erscheint nur gelb. Ein Bahnsteig auf dem Gleis ist rot.
- „Altes Aussehen zurück“ bringt das Haus aus der Ausgangsstadt wieder, mit seinem Bahnsteig.
- Das Zuhause lässt sich noch nicht tauschen.
- Neu unter „Ausgewähltes Stück“: das Feld „Name des Orts“. Neuen Namen eingeben und „Namen übernehmen“ antippen; das ist ein Schritt, Rückgängig geht. Ein leerer, doppelter oder zu langer Name (mehr als 28 Zeichen) geht nicht. Das Spiel zeigt den neuen Namen auf dem Schild und in der Ortsliste; Aufgaben, Kunde und Innenraum bleiben.
- Die Prüfung erkennt gedrehte Häuser jetzt richtig und lässt keine Stadt-Datei mehr durch, die das Spiel nicht laden könnte.

## S 0.20.0

Die Autos in der 3D-Ansicht fahren abwechslungsreicher.

- In einer Stadt mit Kurven oder einem Kreisverkehr biegen die Autos an jeder Kreuzung zufällig ab, statt alle dieselbe feste Runde zu drehen. Nach mindestens acht Abzweigungen fahren sie auf dem kürzesten Weg zurück zu ihrem Start und beginnen von vorn.
- Im Spiel wechseln die Wege jeden Spieltag. Das Werkzeug zeigt immer die Wege des ersten Spieltags, damit du nach jeder Änderung dasselbe Bild siehst.
- Die Prüfung „Hier bleibt ein Auto stecken“ prüft die Wege des ersten Spieltags wie bisher.

## S 0.19.0

Selbst entworfene Gebäude haben jetzt begehbare Obergeschosse.

- Speicherst du im Editor „Gebäude entwerfen“ ein Gebäude mit mehreren Stockwerken als Blueprint, bekommt sein Innenraum für jedes Obergeschoss mit Wänden eine Etage. Jede Etage ist so groß wie die Grundfläche ihres Stockwerks, ein Raum über die ganze Fläche. Das Dach wird keine Etage.
- Ein Treppenhaus kommt gleich mit: Treppen an derselben Stelle auf jeder Etage, im Erdgeschoss hinten im Flur. Damit ist jede Etage sofort begehbar. Du kannst die Treppen danach wie gewohnt verschieben, löschen oder durch einen Fahrstuhl ersetzen.
- Änderst du später den Entwurf, bleiben eingerichtete Etagen, wie sie sind. Passt eine Etage nicht mehr zu ihrem Stockwerk, erscheint ein gelber Hinweis mit dem Knopf „Vorschlag neu“; er legt die Obergeschosse und das Treppenhaus neu an, das Erdgeschoss bleibt. Rückgängig holt die alten Etagen zurück.
- Eine Etage darf nie über die Grundfläche ihres Stockwerks hinausragen; das zeigt die Prüfung rot.
- Die 3D-Ansicht zeigt jetzt jeden Ort, den die Draufsicht zeigt. Bisher fehlte das „Rechenzentrum Lokschuppen“, weil die Serverfarm in der Vorschau nicht freigeschaltet war; die Prüfung prüft es jetzt auch.
- Blueprints und Projekte aus älteren Versionen bleiben lesbar. Ein Gebäude ohne Etagen bekommt sie beim nächsten Speichern oder mit „Vorschlag neu“.

## S 0.18.0

Der volle Katalog spricht jetzt Deutsch.

- Alle 1 521 Stücke im Katalog haben einen deutschen Namen, zum Beispiel „Einbaukühlschrank“ statt „Kitchen fridge built in“. Die Namen stehen auf den Kacheln, bei „Ausgewähltes Stück“ und in den Prüf-Funden.
- Die Suche findet ein Stück weiter mit dem deutschen und mit dem englischen Namen. Der englische Name steht im Tipp der Kachel.
- Ist ein Wort zu lang für die Kachel (zum Beispiel „Badezimmerschrank“), steht dieser Name etwas kleiner da, damit kein Wort mitten drin getrennt wird.
- Deine Projekte, Blueprints und Stadt-Dateien bleiben, wie sie sind; nur die Anzeige ist neu.

## S 0.17.0

Die Knöpfe sind neu geordnet: weniger auf einmal, das Wichtige immer zur Hand.

- Oben links steht dein Projekt mit „gespeichert ✓“. Rechts gibt es zwei Menüs: „Projekt“ (neues Projekt, öffnen, speichern, Stadt-Datei für das Spiel speichern oder an CC senden) und „Einstellungen“ (helle oder dunkle Farben, Vollbild, nach Updates suchen, Tastenkürzel, Version). „Werkzeug beenden“ bleibt ganz rechts.
- Die Ansichten „Stadt von oben“, „Innenräume“, „3D-Ansicht“ und „Gebäude entwerfen“ stehen als eine Leiste darunter, die gewählte ist farbig. Daneben nehmen zwei Pfeile die letzte Änderung zurück oder machen sie wieder.
- Links in „Stadt von oben“ gibt es ein Suchfeld und vier Gruppen zum Aufklappen: „Straßen und Wege“ (mit Kurven, Kreisverkehr, Ziehen und Bahn), „Natur und Licht“, „Wasser“ und „Gebäude“ (mit deinen Blueprints). Es ist immer nur eine Gruppe offen, das Werkzeug merkt sich welche. „Voller Katalog“ steht immer unten.
- Rechts steht „Ausgewähltes Stück“ immer oben. „Prüfen“ zeigt im Kopf, wie viele rote Funde es gibt. „Innenräume öffnen“ ist zugeklappt. In der 3D-Ansicht kommen „Vorschau“ (Tag/Nacht, Jahreszeit, Fest, Figur laufen lassen) und die Grenzen dazu; die Erklärung der 3D-Ansicht steht hinter dem Knopf „?“.
- Die Knöpfe für die Kamera (näher, weiter, alles zeigen, kippen) und der Schalter „Einrasten“ liegen in einer kleinen Karte unten rechts in der Ansicht. „Ganze Stadt zeigen“ lässt darunter Platz, damit die Karte keinen Hausnamen verdeckt.
- Im Reiter „Innenräume“ stehen die Etagen jetzt rechts unter „Ausgewähltes Stück“.
- Das Fenster „Tastenkürzel“ (Taste F1 oder ?, oder im Menü „Einstellungen“) zeigt alle Tasten.
- Funde an einem eigenen Gebäude nennen jetzt seinen Namen statt „bau:…“.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.16.0

Häuser bekommen Etagen: Obergeschosse mit Treppe oder Fahrstuhl.

- Im Reiter „Innenräume“ steht links die Etagenliste: Erdgeschoss, dann die Obergeschosse. Ein Knopf zeigt die Etage, „Etage hinzufügen“ legt eine neue oben an (bis zum 4. Obergeschoss). Eine neue Etage bekommt die Außenwände des Erdgeschosses als Vorschlag.
- Etagen lassen sich umbenennen und löschen, aber nicht das Erdgeschoss und keine Etage, zu der eine Treppe oder ein Fahrstuhl führt.
- Im Katalog gibt es die Gruppe „Treppe und Fahrstuhl“. Wählst du eine gesetzte Treppe, stellst du rechts bei „Ziel“ die Etage darüber oder darunter ein; das Werkzeug setzt das Gegenstück an dieselbe Stelle der anderen Etage. Beim Fahrstuhl hakst du alle Etagen an, auf denen er hält.
- Ein blauer Pfeil zeigt, wo die Figur nach dem Wechsel ankommt und wohin sie schaut. Den Pfeil kannst du ziehen; „Blick drehen“ dreht ihn. Verschiebst du die Treppe, wandert der Pfeil mit.
- Die Prüfung meldet rot: Etage größer als das Erdgeschoss, mehr als vier Obergeschosse, Etage nicht erreichbar, Treppe ohne Ziel, Ankunft nicht frei, oben ist eine Ware zu Fuß nicht erreichbar, eine Etage hat eine Eingangstür.
- Der Export nimmt die Etagen mit; ein Blueprint eines Hauses mit Etagen auch.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.15.0

Die Bahn lässt sich jetzt selbst bauen: setzen, ziehen und mit Bögen versehen.

- Links gibt es die neue Gruppe „Bahn“ mit dem Knopf „Bahn“: er setzt ein gerades Gleis mit drei Eckpunkten. Ziehe den mittleren Punkt zur Seite, dann biegt das Gleis in einem Bogen. Mit „Eckpunkt dazu“ und „Eckpunkt weg“ formst du es weiter.
- Neben „Fluss ziehen“ steht „Bahn ziehen“: Anfang antippen, Ende antippen, und das Werkzeug legt das Gleis mit Geraden und Bögen (Radius 4,5, wenn es passt, sonst 3). Die Vorschau ist grün oder rot mit dem Grund.
- Tippst du einen Eckpunkt der Bahn an, wählst du rechts den Radius: 3 oder 4,5. Enger geht es nicht, sonst ragten die Wagen über die Schienen.
- Ein Gleisende rastet am Ende eines anderen Gleises ein (gleiche Richtung) oder rechtwinklig am Kartenrand. Ziehst du einen Eckpunkt so, dass das Gleis eine gerade Straße fast quer kreuzt, legt es sich genau quer: der Bahnübergang wird gerade.
- Gleise drehen wie Flüsse in Schritten von 15 Grad, lassen sich kopieren und löschen; das Gleis von Talheim kannst du jetzt auch wählen und bearbeiten.
- Die Prüfung meldet rot: Gleise kreuzen sich oder liegen zu nah (Weichen gibt es nicht), ein Bogen ist zu eng gezeichnet, ein Gleis führt durch Wasser, eine Straße quert es flacher als 45 Grad oder in einem Bogen.
- In der Draufsicht sieht das Gleis jetzt aus wie im Spiel: ein glattes Schotterbett mit Schwellen statt einer gestrichelten Linie.
- Beim Ziehen steht der Grund einer roten Vorschau jetzt sofort im Hinweis links, nicht erst nach dem zweiten Tipp.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.14.0

Straßen, Fußwege und Flüsse lassen sich jetzt von Punkt zu Punkt ziehen: Anfang antippen, Ende antippen, fertig.

- Links gibt es drei neue Knöpfe: „Straße ziehen“, „Fußweg ziehen“ und „Fluss ziehen“. Tippe den Anfang an und dann das Ende. Am Tablet kannst du auch mit einem Finger vom Anfang bis zum Ende ziehen. Bis zum zweiten Tipp zeigt eine Vorschau genau, was entsteht: grün, wenn es passt, rot mit dem Grund, wenn nicht.
- Das Werkzeug verbindet Anfang und Ende selbst mit Geraden und Bögen und nimmt den größten Radius, der passt. Straßenbögen drehen in Schritten von 7,5 Grad und mindestens 15 Grad, damit sie im Spiel ohne Lücke anschließen.
- Der Anfang und das Ende rasten ein: eine Straße an ein freies Straßenende oder quer an eine gerade Straße, ein Fußweg an die Gehwegkante oder an das Ende eines anderen Fußwegs, ein Fluss an ein Flussende oder rechtwinklig an den Kartenrand. Mit dem Schalter „Einrasten“ aus liegt alles frei.
- Jedes Ziehen ist ein Schritt: „Rückgängig“ nimmt alles auf einmal zurück. Passt keine Verbindung, ändert sich nichts.
- Abbrechen: Escape, Rechtsklick oder „Setzen beenden“; am Tablet zwei Finger, dann bewegt sich nur die Ansicht. Das geht in der Draufsicht und in der 3D-Ansicht.
- Neu ist die Art „Fußweg“: ein Weg nur zum Gehen, ohne Autos und ohne Mittellinie. Du kannst ihn wählen, schieben, kopieren und löschen. Die Prüfung meldet rot, wenn er durch ein Haus, durch Wasser oder über das Gleis führt, wenn ein Baum, eine Laterne oder ein Katalog-Stück darauf steht oder wenn eine Ecke zu eng ist.
- Die festen Knöpfe für Kurven und Kreisverkehre bleiben wie bisher.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.13.0

Häuser lassen sich jetzt drehen, Straßen und Flüsse fein drehen, und vieles rastet von selbst an der richtigen Stelle ein.

- Häuser, Gebäude ohne Ort und Blueprints drehen in Vierteldrehungen (Taste R oder Knopf „Drehen“, auch schon beim Setzen). Tür, Fußweg und Schild drehen mit, der Fußweg läuft danach wieder zur nächsten Straße. Im Spiel steht das Haus genauso gedreht.
- Straßen, Kurven, Kreisverkehre, Flüsse und Teiche drehen in Schritten von 15 Grad (R vorwärts, Umschalt+R rückwärts). Straßen, Kurven und Flüsse haben in der Auswahl rechts ein Feld „Winkel (Grad)“: dort stellst du den Winkel auf 1 Grad genau ein.
- Am Tablet (und mit der Maus) erscheint über dem gewählten Stück ein runder Drehgriff, in der Draufsicht und in der 3D-Ansicht. Ziehst du ihn mit einem Finger um das Stück, dreht es sich in Rasten (Linien 15 Grad, alles andere 90 Grad). Zwei Finger bewegen wie bisher die Ansicht.
- Katalog-Stücke, Stege und Parks drehen weiter in Vierteldrehungen.
- Ein Haus legt sich beim Setzen und Schieben mit der Türseite genau an die Gehwegkante einer Straße davor (ohne Gehweg im Abstand wie die Läden in Stadt 1). Ein Katalog-Stück stellt sich mit der Rückseite an die äußere Gehwegkante.
- Ein Fluss rastet Ende an Ende an einen anderen Fluss ein oder rechtwinklig an den Kartenrand. Ziehst du einen Eckpunkt so, dass der Fluss eine Straße fast quer kreuzt, legt er sich genau quer: die Brücke wird so kurz wie möglich.
- Der Schalter „Einrasten“ (Taste E) schaltet all das aus: dann setzt und schiebst du frei in feinen Schritten von 0,1. Er ersetzt den bisherigen Schalter „Raster aus (feiner schieben)“.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.12.1

Kurven und Straßen rasten jetzt sauber aneinander ein, auch wenn du sie mit dem Finger nur ungefähr an das Ende ziehst.

- Ziehst oder setzt du eine Kurve oder eine gerade Straße bis knapp an das Ende einer anderen Straße (bis knapp ein Feld daneben), springt sie genau an dieses Ende und dreht sich in dieselbe Richtung. Eine Kurve dreht sich dabei um höchstens 45 Grad, eine gerade Straße um höchstens 15 Grad.
- Eine gerade Straße rastet nicht mehr mitten in einen Bogen ein, sondern nur an das Ende der Kurve oder auf eine andere gerade Straße.
- Kurve an Kurve schließt jetzt ohne Lücke an: die Mittellinie läuft weiter.
- Stößt eine Kurve trotzdem quer an das Ende einer Straße, meldet die Prüfung das rot und sagt dir, was zu tun ist.
- Der Schatten einer Kurve in der 3D-Ansicht ist jetzt ein glatter Streifen ohne Zacken.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.12.0

In der 3D-Ansicht fahren jetzt Autos auf deinen Straßen. Ihre Wege legt das Werkzeug von selbst aus deinem Straßennetz an.

- Die Autos fahren auf geraden Straßen, durch Kurven, an Einmündungen und Kreuzungen und durch den Kreisverkehr. Sie fahren rechts, im Kreisverkehr gegen den Uhrzeigersinn, und wer im Kreisverkehr fährt, hat Vorfahrt.
- Am Ende einer Sackgasse, auch am Rand der Karte, wendet ein Auto und fährt zurück.
- Wie viele Autos fahren, richtet sich nach der Länge deiner Straßen (ein Auto je 10 Felder Straße), höchstens sechs wie in der heutigen Stadt.
- Die Prüfung meldet rot, wenn eine Sackgasse zu kurz zum Wenden ist oder wenn ein Auto stecken bleiben würde.
- Stadt 1 behält ihre festen Autowege, solange sie keine Kurve und keinen Kreisverkehr hat.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.11.0

Du kannst jetzt Kreisverkehre bauen, und die Mittellinie läuft über jeden Kurvenanschluss ohne Lücke.

- Links unter den Kurven gibt es die Knöpfe „Kreisverkehr klein“ (Radius 2) und „Kreisverkehr groß“ (Radius 3). Ein Kreisverkehr kommt mit Ring, Gehweg, Insel mit Rasen und Baum, vier geraden Zufahrten und vier Laternen am Rand.
- Schieben und Drehen nehmen die Zufahrten und die Laternen mit. Eine Zufahrt darf weg, mit drei Zufahrten bleibt der Kreisverkehr in Ordnung. Löschen nimmt nur den Ring, die Zufahrten bleiben als Straßen stehen.
- Die Prüfung meldet rot, wenn ein Kreisverkehr weniger als drei oder mehr als vier Zufahrten hat, eine Zufahrt nicht gerade auf die Mitte zielt, zwei Zufahrten näher als 60 Grad liegen, eine Straße den Ring kreuzt, ein Haus, Baum, Fluss, Teich oder die Bahn auf dem Kreisverkehr liegt, eine Laterne auf der Insel, auf dem Ring oder in einer Zufahrt steht oder der Kreisverkehr über den Rand der Karte ragt.
- Wo eine Kurve an eine gerade Straße oder an eine andere Kurve anschließt, läuft die Mittellinie jetzt im gleichen Takt weiter, ohne fehlenden oder doppelten Strich.
- Alles geht in der Draufsicht und in der 3D-Ansicht, mit der Maus und mit dem Finger.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.10.0

Straßen können jetzt Kurven haben, und am Fluss wählst du den Bogen an jeder Biegung selbst.

- Links unter „Straße“ gibt es die Knöpfe „Kurve 90°“, „Kurve 45°“ und „S-Kurve“. Bei der Kurve 90° wählst du daneben den Radius 1, 2 oder 3; die Kurve 45° und die S-Kurve haben den Radius 2.
- Eine Kurve ist ein ganzes Stück: Schieben, Drehen und Kopieren nehmen sie immer ganz mit. Ihre Länge ist fest.
- Mit „Spiegeln“ (Taste M) biegt die Kurve zur anderen Seite ab, beim Setzen und bei einer gewählten Kurve. Drehen geht wie bisher mit „Drehen“ (Taste R).
- Liegt ein Ende der Kurve höchstens einen halben Schritt neben dem Ende einer anderen Straße, rastet die Kurve dort ein. So schließt sie ohne Lücke an.
- Kreuzungen und Einmündungen gehen nur auf geraden Stücken. Die Prüfung meldet rot, wenn eine Straße in den Bogen einer Kurve mündet, eine Kurve über den Fluss oder die Bahn führt oder ein Baum oder eine Laterne auf der Kurve steht. Gelb meldet sie ein Kurvenende, das knapp neben einer Straße endet, ohne anzuschließen.
- Am Fluss wählst du einen Eckpunkt und rechts den Bogen dort: 2 (eng), 3 (wie bisher) oder 5 (weit). Ist der Bogen zu eng für das Ufer, meldet die Prüfung das.
- Alles geht in der Draufsicht und in der 3D-Ansicht, mit der Maus und mit dem Finger.
- Deine Projekte und Blueprints bleiben, wie sie sind.

## S 0.9.1

Die Häuser und Bauteile sehen jetzt sauberer aus: Viele Wände zeigten bisher schräge Flecken in Form eines X, weil jede Fläche nur eine einzige Farbe bekam. Jetzt zeigen sie den sanften Farbverlauf, den die Grafik eigentlich hat.

- Das gilt in der 3D-Ansicht der Stadt, im Gebäude-Editor und für alle Stücke aus dem Katalog.
- Die Bilder im Katalog bleiben, wie sie waren.
- Deine Projekte und Blueprints bleiben, wie sie sind.
- Im Gebäude-Editor sitzen die Wände „Fenster mit Markisen“, „Fenster mit Balkon“, „Breites Fenster mit Sims“ und „Zwei Rundfenster mit Sims“ jetzt bündig mit den Wänden darüber und darunter. Vorher standen sie ein Stück zu weit hinten, dadurch wirkten Dach und Tür versetzt.
- Ein schon gespeichertes Gebäude mit diesen Wänden bleibt lesbar und lässt sich weiterbauen. Speichere es einmal neu, dann passt auch sein Umriss in der Stadt genau.
- Im Gebäude-Editor gibt es den Schalter „Raster zeigen“ (Taste G). Ohne Raster siehst du das Gebäude ungestört; Setzen und Wählen gehen trotzdem. Das Werkzeug merkt sich deine Wahl.
- Ein gewähltes Stück zum Setzen (zum Beispiel Straße, Fluss oder Teich) lässt sich wieder abwählen: Tippe den Knopf links ein zweites Mal an oder tippe auf „Setzen beenden“. Dann wird nichts gesetzt. Der gewählte Knopf ist farbig markiert.
- Während du ein Stück setzen willst, bewegen zwei Finger, die rechte oder mittlere Maustaste und die Leertaste die Ansicht, ohne etwas zu setzen.
- Das Werkzeug hat jetzt ein eigenes Symbol.

## S 0.9.0

Du kannst jetzt eigene Gebäude entwerfen: aus Wänden, Ecken, Türen, Dächern und Fenstern, Stockwerk für Stockwerk.

- Oben gibt es den neuen Knopf „Gebäude entwerfen“. Du baust ein neues Gebäude oder baust einen deiner eigenen Blueprints weiter.
- Links stehen die Teile in Gruppen (Wände, Ecken, Türen, Dächer, Fenster und Details), jedes mit Bild und deutschem Namen. Teil antippen, dann das Feld im Raster antippen setzt es. Ein Schatten zeigt vorher, ob es dort passt: grün heißt „passt“, rot heißt „geht hier nicht“, mit dem Grund darunter.
- Oben wählst du die Ebene: Erdgeschoss, 1. bis 4. Obergeschoss und eine Ebene nur für das Dach. Die Ebenen darüber werden ausgeblendet, damit du gut hineinsiehst.
- Ein Teil antippen wählt es. Rechts drehst, kopierst und löschst du es, stellst es eine Ebene höher oder tiefer und schiebst es mit den Pfeilen um ein Feld. Eine Tür im Erdgeschoss legst du dort als Eingang fest.
- Die Kamera bewegst du wie in der 3D-Ansicht der Stadt: am Tablet mit zwei Fingern, am PC mit der rechten Maustaste und dem Mausrad, dazu die Knöpfe unter dem Bild. Mit dem Schalter „Nacht“ siehst du die Fenster leuchten.
- Rechts zeigt ein Balken, wie viele Dreiecke das Gebäude hat (höchstens 8 000). Die Prüfung meldet rot, was nicht geht (zum Beispiel ein Teil in der Luft, ein Dach ohne Wand, kein Eingang, vor dem Eingang steht etwas), und gelb, was du noch ansehen solltest (ein Feld oben offen, ein Fenster zeigt in ein Nachbarteil).
- „Als Blueprint speichern“ legt das Gebäude in „Meine Blueprints“ ab. Für das Erdgeschoss bekommt es gleich einen Innenraum mit Flur und Raum als Vorschlag, den du wie jeden Innenraum umbauen kannst.
- Ein eigenes Gebäude kannst du in die Stadt setzen und in der 3D-Ansicht ansehen. Ins Spiel kommt es erst mit dem Einbau; bis dahin lässt sich eine Stadt mit eigenem Gebäude noch nicht als Datei für das Spiel speichern.
- Jede Änderung ist ein Schritt, den du mit „Rückgängig“ zurücknehmen kannst.

## S 0.8.0

Du kannst jetzt Wasser bauen: Teiche, Stege und den Fluss.

- Links gibt es die neue Gruppe „Wasser“ mit „Fluss“, „Teich“, „Runder Teich“ und „Steg“. Setzen, Verschieben, Drehen, Kopieren und Löschen gehen wie bei jedem anderen Stück, von oben und in der 3D-Ansicht.
- Ein Teich hat eine freie Form mit runden Ecken. Wählst du ihn, siehst du an jeder Ecke einen kleinen Kreis. Den ziehst du mit der Maus oder mit einem Finger an eine neue Stelle. Mit „Eckpunkt dazu“ und „Eckpunkt weg“ machst du die Form feiner oder einfacher; ein Teich behält mindestens drei Ecken. Beim Fluss gehen die Eckpunkte genauso.
- Ein Steg ist ein Holzsteg am Ufer. Länge und Breite stellst du rechts ein.
- Der Schalter „Eis im Winter“ lässt alles Wasser der Stadt im Winter zufrieren. Ist er aus, bleibt das Wasser auch im Winter blau.
- Die Prüfung achtet jetzt auch auf das Wasser: kein Haus, kein Baum, keine Laterne und kein Stück im Wasser oder auf dem Ufer, keine Straße durch einen Teich und nicht längs durch den Fluss (quer als Brücke ist erlaubt), jeder Steg berührt das Ufer, und jeder Ort bleibt zu Fuß erreichbar. Wo etwas nicht geht, zeigt der Schatten in der 3D-Ansicht rot.
- Alles ist ein Schritt, den du mit „Rückgängig“ zurücknehmen kannst.

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
