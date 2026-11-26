using Tasten = Lernplattform.Core.Spiel.Probe.Tastenbelegung;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Texte der 3D-Stichprobe (Phase 7 W1). Nur fuer die Probe; die Texte des Spiels kommen mit
/// dem Spiel-Kern (W2) und der Stadt (W4) und gehen vorher zu Chat (Verhaltenszusage).
/// </summary>
public static class SpielProbeTexte
{
    public const string Titel = "Spiel (Probe 3D)";
    /// <summary>
    /// Hinweis draussen mit der Standardbelegung. Welle A 0.80.0 (Weisung P12-268): feste Kamera, kein Verschieben mehr;
    /// zwei Finger bzw. rechte Maustaste drehen und neigen, Rest-Zoom 7 bis 9, Ueberblick in der Karte im Handy (bis A 0.79.0:
    /// „Ziehen verschiebt die Ansicht … Pfeiltasten verschieben …“). Seit A 0.95.0 nennt er die Gehtasten W, A, S und D.
    /// </summary>
    public const string Hinweis = "Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Zwei Finger drehen und neigen die Kamera und zoomen ein wenig. Am PC: W, A, S und D gehen, rechte Maustaste dreht und neigt, Mausrad zoomt ein wenig, Q und E drehen, R und G neigen, F für die Handlung hier (Tür, Bus, Gegenstand). Ganz heranzoomen oder I führt in die Ich-Sicht. Die ganze Stadt zeigt die Karte im Handy.";

    /// <summary>Hinweis mit den gewaehlten Tasten (Tastenbelegung); mit Standard genau <see cref="Hinweis"/>.</summary>
    public static string HinweisFuer(Tasten tasten)
    {
        string T(string aufgabe) => tasten.AnzeigeFuer(aufgabe);
        return "Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Zwei Finger drehen und neigen die Kamera und zoomen ein wenig. "
            + "Am PC: "
            // Welle A 0.95.0 (Sammelliste 385): die vier Gehtasten (fehlt einer eine Taste, nennt der Hinweis keine)
            + (Tasten.Gehen.Any(g => tasten.Tasten(g).Contains(Tasten.OhneTaste)) ? ""
                : $"{T(Tasten.GehVor)}, {T(Tasten.GehLinks)}, {T(Tasten.GehZurueck)} und {T(Tasten.GehRechts)} gehen, ")
            + $"rechte Maustaste dreht und neigt, Mausrad zoomt ein wenig, {T(Tasten.DrehLinks)} und {T(Tasten.DrehRechts)} drehen"
            // ohne Taste fuers Neigen (Zusammenstoss mit einer eigenen Wahl aus frueherer Fassung) nennt der Hinweis keine
            + (tasten.Tasten(Tasten.Steiler).Contains(Tasten.OhneTaste) || tasten.Tasten(Tasten.Flacher).Contains(Tasten.OhneTaste)
                ? "" : $", {T(Tasten.Steiler)} und {T(Tasten.Flacher)} neigen")
            // Welle A 1.6.0 (Sammelliste 444): die Taste fuer die Handlung an der Stelle
            + (tasten.Tasten(Tasten.Handeln).Contains(Tasten.OhneTaste) ? "" : $", {T(Tasten.Handeln)} für die Handlung hier (Tür, Bus, Gegenstand)")
            + ". Ganz heranzoomen"
            + (tasten.Tasten(Tasten.IchSicht).Contains(Tasten.OhneTaste) ? "" : $" oder {T(Tasten.IchSicht)}")
            + " führt in die Ich-Sicht. Die ganze Stadt zeigt die Karte im Handy.";
    }

    /// <summary>Welle A 0.92.0 (Weisung P12-301 Punkt 1): Eintrag im aufgeklappten Menue unten, aus der normalen Kamera.</summary>
    public const string IchSichtKnopf = "Ich-Sicht";

    /// <summary>Welle A 0.92.0: derselbe Eintrag in der Ich-Sicht.</summary>
    public const string IchSichtBeenden = "Ich-Sicht beenden";

    /// <summary>Welle A 0.92.0: Ansage beim Einschalten (Screenreader).</summary>
    public const string IchSichtAn = "Ich-Sicht an.";

    /// <summary>Welle A 0.92.0: Ansage beim Ausschalten (Screenreader).</summary>
    public const string IchSichtAus = "Ich-Sicht aus.";

    /// <summary>Welle A 0.92.0: Blickrichtung in der Ich-Sicht (Screenreader).</summary>
    public static string IchBlick(string richtung) => "Blick nach " + richtung + ".";

    /// <summary>Welle A 0.92.0: was vorn liegt (Screenreader), z. B. „Vor dir: Kaufhaus, 12 m.“</summary>
    public static string IchVorDir(string name, int meter) =>
        meter < 2 ? "Vor dir: " + name + "." : "Vor dir: " + name + ", " + meter.ToString(System.Globalization.CultureInfo.InvariantCulture) + " m.";

    public const string Orte = "Orte";

    /// <summary>Weisung P8-52 (Text von Chat angenommen): Knopf der Leistungsprobe, nur in der Probe-APK.</summary>
    public const string Leistungsprobe = "Leistungsprobe";

    /// <summary>Welle Nachladen A 0.82.0 (nur Probe-APK): grosse Testkarte laden und den Gang messen.</summary>
    public const string Testkarte = "Testkarte";
    public const string Hingehen = "Hingehen";

    // ---- Welle A 0.67.0 (Weisung P12-204, Sammelliste 302): Bus fuer den Spieler ----

    /// <summary>Knopf in der Ortsliste (auch ohne 3D).</summary>
    public const string MitDemBus = "Mit dem Bus";

    /// <summary>Zeile unter dem Namen eines Orts in der Ortsliste, wenn der Bus sich lohnt: {0} zu Fuss, {1} mit dem Bus (Minuten).</summary>
    public const string BusZeiten = "Zu Fuß {0} Min., mit dem Bus {1} Min.";

    /// <summary>Unterwegs mit dem Bus: {0} Ort, {1} Minuten zu Fuss, {2} warten, {3} fahren, {4} zusammen.</summary>
    public const string BusUnterwegs = "Mit dem Bus zu {0}: {1} Min. zu Fuß, {2} Min. warten, {3} Min. Fahrt, zusammen {4} Min. Tippen überspringt die Fahrt.";

    /// <summary>Der Bus lohnt nicht (zu Fuss gleich schnell oder schneller) oder es gibt keine Fahrt.</summary>
    public const string BusLohntNicht = "Mit dem Bus bist du nicht schneller. Geh lieber zu Fuß.";

    /// <summary>Waehrend die Figur noch geht.</summary>
    public const string BusErstAnkommen = "Komm erst an, dann kannst du in den Bus steigen.";

    /// <summary>Name des Knopfs fuer den Screenreader: {0} Ort, {1} Minuten mit dem Bus.</summary>
    public const string MitDemBusName = "Mit dem Bus zu {0}, {1} Minuten";

    // ---- Welle A 0.81.0 (Weisung P12-272, Sammelliste 349): hinsetzen und hinlegen ----

    /// <summary>Knopf vor einem freien Sitzmoebel (auch am PC-Stuhl und an der Haltestelle).</summary>
    public const string KnopfHinsetzen = "Hinsetzen";

    /// <summary>Knopf vor einem freien Bett.</summary>
    public const string KnopfHinlegen = "Hinlegen";

    /// <summary>Knopf im Sitzen und im Liegen.</summary>
    public const string KnopfAufstehen = "Aufstehen";

    /// <summary>Welle A 1.6.0 (Sammelliste 443, Weisung P12-360 Antwort 1c): Raumtuer, zu bis man sie oeffnet.</summary>
    public const string KnopfTuerOeffnen = "Tür öffnen";

    public const string KnopfTuerSchliessen = "Tür schließen";

    public const string TuerIstZu = "Die Tür ist zu. Öffnen kannst du sie mit F oder dem Knopf über dem linken Stick.";

    /// <summary>Welle A 0.99.0 (Weisung P12-317 F2): Glastuer am Serverschrank (nur Darstellung).</summary>
    public const string KnopfGlastuerOeffnen = "Glastür öffnen";

    public const string KnopfGlastuerSchliessen = "Glastür schließen";

    public const string GlastuerKeinPlatz = "Hier geht die Glastür nicht auf. Tritt einen Schritt zur Seite.";

    /// <summary>Erste Zeile im Sitzen.</summary>
    public const string DuSitzt = "Du sitzt. Zum Aufstehen tippe woanders hin oder auf „Aufstehen“.";

    /// <summary>Erste Zeile im Liegen.</summary>
    public const string DuLiegst = "Du liegst im Bett. Zum Aufstehen tippe woanders hin oder auf „Aufstehen“.";

    /// <summary>Am Schreibtisch, solange die Figur steht (die PC-Knoepfe gibt es erst im Sitzen).</summary>
    public const string ErstHinsetzen = "Setz dich auf den Stuhl (antippen oder „Hinsetzen“), dann geht der PC an.";

    /// <summary>Am Bett, solange die Figur steht („Schlafen“ gibt es erst im Liegen).</summary>
    public const string ErstHinlegen = "Leg dich ins Bett (antippen oder „Hinlegen“), dann kannst du schlafen.";

    // ---- Welle A 0.93.0 (Weisung P12-303, Sammelliste 352): Haltungen ----

    /// <summary>Knopf „Springen“ (Leertaste): kurzer Sprung an der Stelle oder im Gehen.</summary>
    public const string KnopfSpringen = "Springen";

    /// <summary>Knopf „Ducken“ (seit A 0.99.0 geht die Figur geduckt weiter, bis sie aufsteht).</summary>
    public const string KnopfDucken = "Ducken";

    /// <summary>Knopf „Kriechen“ (seit A 0.99.0 kriecht die Figur weiter, bis sie aufsteht).</summary>
    public const string KnopfKriechen = "Kriechen";

    /// <summary>
    /// Welle A 1.7.0 (Sammelliste 447): kleiner Knopf am rechten Daumen (am PC Symbol mit Taste) zum Hinknien (geduckt; gehalten oder
    /// nach unten gewischt: hinlegen). Der Knopf zum Hinlegen heisst wie am Bett <see cref="KnopfHinlegen"/>.
    /// </summary>
    public const string KnopfHinknien = "Hinknien";

    /// <summary>Knopf im Sitzen auf Sofa, Sessel oder Liegestuhl.</summary>
    public const string KnopfZuruecklehnen = "Zurücklehnen";

    /// <summary>Knopf im zurueckgelehnten Sitzen.</summary>
    public const string KnopfAufrichten = "Aufrichten";

    /// <summary>Erste Zeile im zurueckgelehnten Sitzen.</summary>
    public const string DuSitztZurueck = "Du sitzt zurückgelehnt. Mit „Aufrichten“ setzt du dich wieder gerade hin.";

    /// <summary>Ansage und Zeile beim Ducken.</summary>
    public const string DuDuckst = "Du duckst dich und kannst so auch gehen. Zum Aufstehen tippe auf „Aufstehen“.";

    /// <summary>Ansage und Zeile beim Kriechen.</summary>
    public const string DuKriechst = "Du legst dich flach auf den Bauch und kannst so auch kriechen. Zum Aufstehen tippe auf „Aufstehen“.";

    /// <summary>Ansage beim Aufstehen aus Ducken oder Kriechen.</summary>
    public const string DuStehst = "Du stehst wieder.";

    /// <summary>Ansage beim Springen.</summary>
    public const string DuSpringst = "Du springst.";

    /// <summary>Welle A 0.99.0: beim Gehen in der Haltung ist vorn kein Platz mehr, die Figur steht auf.</summary>
    public const string HaltungKeinPlatzMehr = "Hier ist für deine Haltung kein Platz. Du stehst auf.";

    /// <summary>Ducken geht hier nicht (die Figur wuerde etwas beruehren oder ueber eine Wand ragen).</summary>
    public const string KeinPlatzDucken = "Hier ist zum Ducken kein Platz.";

    /// <summary>Kriechen geht hier nicht (die Figur wuerde etwas beruehren oder ueber eine Wand ragen).</summary>
    public const string KeinPlatzKriechen = "Hier ist zum Kriechen kein Platz.";

    /// <summary>Springen geht hier nicht (ueber dem Kopf ist etwas im Weg).</summary>
    public const string KeinPlatzSpringen = "Hier ist zum Springen kein Platz.";

    /// <summary>Ducken und Kriechen gehen nur an der Stelle.</summary>
    public const string ErstAnkommenHaltung = "Ducken und Kriechen gehen nur im Stehen. Warte, bis du angekommen bist.";

    /// <summary>Im Sitzen, Liegen, im Bus und beim Einrichten gibt es keine dieser Haltungen.</summary>
    public const string ErstAufstehen = "Steh erst auf.";

    // ---- Welle A 0.73.0 (Weisung P12-239, Sammelliste 335): selbst mit dem Bus fahren ----

    /// <summary>Knopf unten, wenn der Bus an der Haltestelle der Figur haelt.</summary>
    public const string Einsteigen = "Einsteigen";

    /// <summary>Knopf unten waehrend der Fahrt.</summary>
    public const string Aussteigen = "Aussteigen";

    /// <summary>Knopf unten nach dem Druecken von „Aussteigen“ (Tippen aendert nichts).</summary>
    public const string HaeltGleich = "Hält an der nächsten Haltestelle";

    /// <summary>Zeile, solange die Figur an einer Haltestelle steht und kein Bus haelt.</summary>
    public const string AnDerHaltestelle = "Haltestelle: Hier wartest du auf den Bus.";

    /// <summary>Zeile, wenn der Bus an der Haltestelle der Figur haelt.</summary>
    public const string BusIstDa = "Der Bus ist da. Tippe unten auf „Einsteigen“.";

    /// <summary>Zeile waehrend der Fahrt.</summary>
    public const string ImBusUnterwegs = "Du fährst mit dem Bus. Mit „Aussteigen“ hält er an der nächsten Haltestelle.";

    /// <summary>Zeile nach „Aussteigen“, bis der Bus haelt.</summary>
    public const string BusHaeltGleich = "Der Bus hält an der nächsten Haltestelle, dort steigst du aus.";

    /// <summary>Zeile nach dem Aussteigen: {0} Minuten Fahrt.</summary>
    public const string Ausgestiegen = "Ausgestiegen. Die Fahrt hat {0} Min. gedauert.";

    /// <summary>Waehrend der Fahrt getippt oder einen Ort gewaehlt.</summary>
    public const string ErstAussteigen = "Du sitzt im Bus. Steig erst aus.";
    public const string Betreten = "Hineingehen";
    public const string ZurStadt = "Zur Stadt";
    public const string Warten = "Eine Stunde warten";
    public const string Offen = "{0}: geöffnet bis {1} Uhr.";
    public const string ImmerOffen = "{0}: immer offen.";
    public const string Geschlossen = "{0}: geschlossen, öffnet um {1} Uhr.";
    public const string KeinInnenraum = "Innenraum kommt später.";
    public const string Unterwegs = "Unterwegs zu {0} …";
    public const string Angekommen = "Angekommen.";

    // Nachtrag 1 zu P7-12 (Wunsch 5): ein neues Ziel waehrend des Gehens gilt ab der Ankunft
    public const string DanachZu = "Danach geht es weiter zu {0}.";
    public const string DanachDorthin = "Danach geht es dorthin weiter.";
    public const string NichtErreichbar = "Dort kommst du nicht hin.";

    /// <summary>Welle A 0.95.0 (Sammelliste 385): beim freien Gehen gegen eine Wand, ein Haus oder die Fahrbahn gelaufen (Ansage, einmal je Halt).</summary>
    public const string NichtWeiter = "Hier geht es nicht weiter.";

    /// <summary>Welle A 0.95.0: Ansage beim Halt nach freiem Gehen draussen, wenn ein Ort nah ist ({0} = sein Name).</summary>
    public const string VorDir = "Vor dir: {0}.";

    /// <summary>
    /// Welle A 0.95.0 (Weisung P12-306, Nico 1c): Schalter in den Optionen; seit A 0.96.0 (Weisung P12-308/P12-309) oben in der
    /// Karte „Steuerung“ (vorher Abschnitt „Gehen“ der Karte „Spiel“).
    /// </summary>

    public const string AntippenGehen = "Antippen zum Gehen";

    /// <summary>Erklaerung unter dem Schalter (mit dem Hinweis fuer den Screenreader, E1 Chat).</summary>
    public const string AntippenGehenSatz = "An: Ein Tipp oder Klick in die 3D-Ansicht lässt die Figur dorthin gehen. Aus: Die Figur geht nur mit den Gehtasten (Standard W, A, S, D, unten in dieser Karte änderbar) und am Handy mit dem Stick. Die Liste der Orte, die Karte und die Knöpfe gehen immer. Mit Screenreader am besten anlassen.";

    /// <summary>
    /// Welle A 0.96.0 (Sammelliste 385 Welle 2, Weisung P12-309): Satz zu den Sticks in der Karte „Steuerung“ (PC und Handy gleich)
    /// und ihre Namen fuer den Screenreader.
    /// </summary>
    public const string SticksSatz = "Am Handy und Tablet: Setzt du einen Daumen links unten auf und ziehst ihn ein Stück, erscheint dort ein Stick zum Gehen. Rechts unten erscheint so ein Stick zum Drehen der Ansicht (in der Ich-Sicht zum Umsehen). Beide gehen zugleich und verschwinden beim Loslassen. Ein kurzer Tipp ohne Ziehen bleibt Antippen.";

    /// <summary>
    /// Welle A 0.97.0 (Sammelliste 394, Weisung P12-314, Wortlaut E1): zwei Schalter in der Karte „Steuerung“ (PC und Handy
    /// gleich), Standard aus, und der Satz darunter.
    /// </summary>
    public const string UmsehenLinksRechts = "Umsehen links/rechts umkehren";

    public const string UmsehenObenUnten = "Umsehen oben/unten umkehren";

    public const string UmsehenSatz = "Gilt für das Umsehen mit dem rechten Stick, der Maus und den Tasten zum Drehen. An: Diese Richtung ist umgekehrt. Gehen bleibt immer gleich.";

    /// <summary>
    /// Welle A 1.1.0 (Sammelliste 409, Weisung P12-328 Punkt 2): Regler „Blickfeld Ich-Sicht“ in der Karte „Steuerung“ (PC und
    /// Handy gleich), Anzeige des Werts und der Satz darunter.
    /// </summary>
    public const string Blickfeld = "Blickfeld Ich-Sicht";

    public const string BlickfeldGrad = "{0}°";

    public const string BlickfeldSatz = "Wie weit du in der Ich-Sicht zur Seite siehst, von 60 bis 110 Grad (Vorgabe 90). Größer wirkt weiter und Räume geräumiger, kleiner wirkt näher. Die normale Kamera bleibt gleich.";

    // ---- Welle A 1.6.0 (Sammelliste 444, Weisungen P12-359/P12-360): Handlung statt Dreieck-Menue ----

    /// <summary>Name des runden Handy-Symbols am Daumen bzw. unten links (Screenreader, mit der Zahl der Auftraege).</summary>
    public const string KnopfTelefon = "Handy";

    /// <summary>Ueberschrift der kleinen Auswahl aller Handlungen (F bzw. Knopf gehalten).</summary>
    public const string HandlungAuswahl = "Was möchtest du tun?";

    /// <summary>Auswahl der Handlungen schliessen.</summary>
    public const string HandlungAuswahlSchliessen = "Schließen";

    /// <summary>Anzeige am PC: Taste und Haupthandlung, z. B. „F: Tür öffnen“.</summary>
    public const string HandlungTaste = "{0}: {1}";

    /// <summary>
    /// Welle A 1.6.0 (Sammelliste 445/446, Weisung P12-360 Punkte 6 und 7): Regler „Empfindlichkeit“ in der Karte „Steuerung“
    /// (PC und Handy gleich), Anzeige des Werts und der Satz darunter.
    /// </summary>
    public const string EmpfindlichkeitMaus = "Empfindlichkeit Maus";

    public const string EmpfindlichkeitStickLinks = "Empfindlichkeit linker Stick (Gehen)";

    public const string EmpfindlichkeitStickRechts = "Empfindlichkeit rechter Stick (Umsehen)";

    public const string EmpfindlichkeitProzent = "{0} %";

    public const string EmpfindlichkeitSatz = "Von 25 bis 200 Prozent (Vorgabe 100). Die Maus und der rechte Stick drehen den Blick schneller oder langsamer. Beim linken Stick reicht mehr oder weniger Ausschlag für dasselbe Tempo, schneller als volles Tempo geht die Figur nie.";

    public const string StickGehen = "Stick zum Gehen";

    public const string StickDrehen = "Stick zum Drehen";

    /// <summary>
    /// Weisung P12-307 Fund 2 (Wortlaut Chat, E2): steht in der Tafel statt eines Satzes „Tippe …, um hinzugehen.“, solange
    /// „Antippen zum Gehen“ aus ist (dann geht Antippen in der 3D-Ansicht nicht; ohne 3D bleibt die Tafel wie sie ist).
    /// </summary>
    public const string GehtastenStattTippen = "Gehe mit den Gehtasten (Standard W, A, S, D). Antippen zum Gehen ist in den Optionen ausgeschaltet.";

    /// <summary>Ein Satz „Tippe …, um hinzugehen.“ (ohne Punkt dazwischen) in einer Tafelzeile.</summary>
    public static readonly System.Text.RegularExpressions.Regex TippeHinzugehen =
        new(@"Tippe [^.]*?, um hinzugehen\.", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Tafelzeile wie gezeigt: ist Antippen an (oder gibt es keine 3D-Ansicht), wortgleich; sonst wird jeder Satz
    /// „Tippe …, um hinzugehen.“ durch <see cref="GehtastenStattTippen"/> ersetzt, der Rest der Zeile bleibt.
    /// </summary>
    public static string Tafelzeile(string? zeile, bool antippenGilt) =>
        zeile is null ? "" : antippenGilt ? zeile : TippeHinzugehen.Replace(zeile, GehtastenStattTippen);
    /// <summary>
    /// Weisung P12-74: Hinweis in einem Geschaeft auf einer Etage ohne Verkaufsperson (Obergeschoss); im Erdgeschoss bleibt
    /// „Tippe auf eine Ware oder sprich mit dem Verkauf.“
    /// </summary>
    public const string EtageOhneVerkauf = Spiel.Probe.Innenraum.EtageOhneVerkauf;

    public const string ImFreien = "Tippe auf ein Haus, einen Namen oder einen freien Platz, um hinzugehen.";
    // Phase 8 W10 (Sammelliste 148, Nico 1b): Klappknopf des Hinweisfelds (Bildschirmleser)
    public const string HinweisEinklappen = "Hinweis einklappen";
    public const string HinweisAusklappen = "Hinweis ausklappen";
    // Welle A 1.13.0 (Sammelliste 464): Zeile oben mit dem Hinweis (Bildschirmleser); das Dreieck mit den zwei Texten oben gibt es
    // seitdem nicht mehr (die Texte bleiben fuer die Hilfe-Begriffe unveraendert stehen)
    public const string HinweisZeile = "Hinweis: {0}. Antippen zeigt den ganzen Text.";
    public const string HinweisGanz = "Hinweis: {0}. Antippen schließt den ganzen Text.";
    public const string ImBuero = "Büro der Bitweiche. Tippe auf den Boden, um hinzugehen.";
    public const string Spielzeit = "Spielzeit {0} Uhr";
    public const string Bilder = "{0} Bilder/s";
    public const string OhneGrafik = "3D ist auf diesem Gerät nicht verfügbar (keine Grafikbeschleunigung). Die Orte erreichst du über die Liste.";
    public const string GrafikStartet = "3D startet …";

    /// <summary>Weisung P8-55 (Text von Chat angenommen, P8-52): Ladeanzeige beim Oeffnen der Spielseite (erst ab 0,3 s).</summary>
    public const string StadtLaedt = "Stadt wird geladen …";

    // Weisung P7-17/P7-18 (Text von Chat angenommen): ein Spielschritt wurde im Hintergrund nicht gespeichert
    public const string SchrittNichtGespeichert = "Ein Spielschritt konnte nicht gespeichert werden. Das Spiel lädt den letzten gespeicherten Stand.";

    // W5: Einrichten der Wohnung, Beschriftungen wie Python (mobile/src/spiel.py _home_editor, fisi_game_gui HomeView)
    public const string Einrichten = "Einrichten";
    public const string Fertig = "Fertig";
    public const string Drehen = "Drehen";
    public const string InDenKarton = "In den Karton";

    /// <summary>Sammelliste 382 (Weisung P12-298, Nico): Platz des eben aufgestellten Moebels bestaetigen, im Einrichten bleiben.</summary>
    public const string Uebernehmen = "Übernehmen";

    /// <summary>Sammelliste 382: Zeile nach „Übernehmen“, %s = Name des Moebels.</summary>
    public const string Uebernommen = "„%s“ steht. Tippe das nächste Möbelstück an oder auf „Fertig“.";

    /// <summary>Phase 12 Welle 1 (Nico 3b): Knopf beim Einrichten, %s = Rueckkaufpreis („Zurückverkaufen (170 €)“).</summary>
    public const string Zurueckverkaufen = "Zurückverkaufen (%s)";

    public const string VerkaufenTitel = "Zurückverkaufen";

    /// <summary>Rueckfrage: %s = Name, %s = Preis.</summary>
    public const string VerkaufenFrage = "„%s“ für %s zurückverkaufen? Das Möbelstück ist danach weg.";

    public const string VerkaufenJa = "Verkaufen";

    /// <summary>Zeile nach dem Verkauf, %s = Name.</summary>
    public const string Verkauft = "Verkauft: %s.";
    public const string Aufstellen = "Aufstellen";
    public const string Ausgewaehlt = "Ausgewählt";
    public const string ImKarton = "Im Karton";
    public const string AllesAusgepackt = "Alles ist ausgepackt.";
    public const string EinrichtenAufstellen = "Tippe auf den Boden, wo es hin soll. Ein zweites Tippen auf die Vorschau stellt es auf.";
    public const string EinrichtenVerschieben = "Tippe auf den Boden, um es dorthin zu stellen, oder drehe es.";

    // ---- Welle A 1.9.0 (Sammelliste 450, Weisungen P12-375 bis P12-377): Knoepfe anordnen ----

    /// <summary>Knopf in der Karte „Steuerung“ (nur Handy und Tablet) und Titel der Leiste im Bearbeiten-Modus.</summary>
    public const string KnoepfeAnordnen = "Knöpfe anordnen";

    public const string KnoepfeAnordnenSatz = "Verschiebe die runden Knöpfe im Spiel und stelle für jeden die Größe ein. Hoch und quer haben je eine eigene Anordnung.";

    /// <summary>Leiste ohne gewaehlten Knopf.</summary>
    public const string AnordnenTippen = "Knopf antippen";

    /// <summary>Name des Reglers (Screenreader), {0} = Knopf.</summary>
    public const string AnordnenGroesse = "Größe {0}";

    public const string AnordnenRaster = "Raster";

    public const string AnordnenStandard = "Standard";

    public const string AnordnenAbbrechen = "Abbrechen";

    public const string AnordnenFertig = "Fertig";

    public const string AnordnenHinweis = "Knopf antippen und ziehen";

    public const string AnordnenHinweisRaster = "Knopf antippen und ziehen · er rastet ein";

    /// <summary>Name eines Knopfs im Bearbeiten-Modus (Screenreader), {0} = Knopf.</summary>
    public const string AnordnenVerschieben = "{0}, verschieben";

    /// <summary>Pfeile fuer den gewaehlten Knopf (Screenreader), {0} = Knopf.</summary>
    public const string AnordnenLinks = "{0} nach links";

    public const string AnordnenRechts = "{0} nach rechts";

    public const string AnordnenOben = "{0} nach oben";

    public const string AnordnenUnten = "{0} nach unten";

    public const string AnordnenStandardTitel = "Standard wiederherstellen?";

    /// <summary>{0} = „Querformat“ oder „Hochformat“ (diese Ausrichtung), {1} = die andere.</summary>
    public const string AnordnenStandardFrage = "Alle Knöpfe im {0} kommen an ihren Standard-Platz und auf 100 % Größe zurück. Das {1} bleibt, wie es ist.";

    public const string Querformat = "Querformat";

    public const string Hochformat = "Hochformat";

    public const string AnordnenZuruecksetzen = "Zurücksetzen";

    public const string AnordnenAussen = "Hier nicht: zu nah am Rand oder an der Kopfzeile";

    public const string AnordnenLeiste = "Hier nicht: auf der Leiste";

    public const string AnordnenUeberlappt = "Hier nicht: zu nah an einem anderen Knopf";

    public const string AnordnenStickPlatz = "Hier nicht: dann bleibt kein Platz für einen Stick";

    public const string AnordnenZurueck = " · springt beim Loslassen zurück";

    /// <summary>Hinweis im Spiel, einmal je Sitzung, wenn die eigene Anordnung auf diesem Bildschirm nicht passt.</summary>
    public const string AnordnenPasstNicht = "Deine Knöpfe passen hier nicht hin. Es gilt der Standard.";

    /// <summary>Name des Handlungsknopfs im Bearbeiten-Modus (dort gibt es keine Handlung).</summary>
    public const string KnopfHandlung = "Handlung";

    public const string PlatzFuerStick = "Platz für\nden Stick";
}
