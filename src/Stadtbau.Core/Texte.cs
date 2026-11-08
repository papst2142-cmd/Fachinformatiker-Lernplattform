using System.Globalization;
namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 2: alle sichtbaren Texte des Stadtbau-Werkzeugs an einer Stelle (Deutsch, einfache Worte fuer Nico).
/// Die Oberflaeche nimmt Texte nur von hier, die Tests pruefen Wortlaut und Namen fuer den Screenreader.
/// </summary>
public static class Texte
{
    // ---- Kopfleiste ----
    // Patch S 0.5.1 (Weisung P12-35, Nico: "Die Beschreibungen sind zu ungenau"): jeder Knopf sagt, womit er arbeitet
    public const string Neu = "Neues Projekt";
    public const string Oeffnen = "Projekt öffnen";
    public const string Speichern = "Projekt speichern";
    public const string Rueckgaengig = "Rückgängig";
    public const string Wiederholen = "Wiederholen";
    public const string DateiSpeichern = "Stadt-Datei für das Spiel speichern";
    public const string AnCcSenden = "Stadt-Datei an CC senden";
    public const string Hell = "Helle Farben";
    public const string Dunkel = "Dunkle Farben";
    public const string Draufsicht = "Stadt von oben";
    public const string Innenraeume = "Innenräume";

    /// <summary>Patch S 0.5.1 (Weisung P12-35 Punkt 1): Kopfleiste ganz rechts, am PC wie das Fensterkreuz.</summary>
    public const string WerkzeugBeenden = "Werkzeug beenden";

    /// <summary>Patch S 0.5.1 (Weisung P12-37 Punkt 2): ganzer Bildschirm, am PC auch F11, nach einem Neustart gemerkt.</summary>
    public const string Vollbild = "Vollbild";
    public const string VollbildBeenden = "Vollbild beenden";
    public const string VollbildFehler = "Die Einstellung „Vollbild“ konnte nicht gespeichert werden; beim nächsten Start gilt wieder die alte.";

    // ---- Start ----
    public const string Willkommen = "Willkommen im Stadtbau-Werkzeug";
    public const string WillkommenText =
        "Hier baust du die Stadt des Lernspiels und ihre Innenräume um. Lege ein neues Projekt an oder öffne ein vorhandenes. Jede Änderung wird sofort gespeichert, die letzten 20 Stände liegen zusätzlich als Sicherung bereit.";

    /// <summary>Patch S 0.5.1 (Weisung P12-35): was ein Projekt ist und was man darin bauen kann.</summary>
    public const string WasIstEinProjekt = "Was ist ein Projekt?";

    public const string WasIstEinProjektText =
        "Ein Projekt ist deine Baustelle: eine eigene Kopie einer Stadt. Was du darin änderst, ändert das Spiel erst, wenn du die Stadt-Datei an CC schickst und sie eingebaut ist. In einem Projekt kannst du die Stadt von oben umbauen (Straßen, Häuser, Bäume, Laternen, Stücke aus dem Katalog), die Innenräume der Häuser einrichten, Gebäude als Blueprint (Vorlage) speichern und wieder setzen und alles in 3D ansehen.";
    public const string LetzteProjekte = "Zuletzt bearbeitet";
    public const string KeineProjekte = "Noch kein Projekt vorhanden.";
    public const string NurHandy = "Das Werkzeug ist für Tablet und PC gedacht";
    public const string NurHandyText = "Auf einem Handy ist der Bildschirm zu klein zum Bauen. Bitte öffne das Werkzeug auf dem Tablet oder am PC.";

    // ---- Neu ----
    public const string NeuTitel = "Neues Projekt";
    public const string NeuName = "Name des Projekts";
    public const string NeuNameHinweis = "Buchstaben, Ziffern, Leerzeichen, - und _ (höchstens 40 Zeichen).";
    public const string NeuArt = "Was möchtest du bauen?";
    public const string NeuStadt = "Stadt 1 überarbeiten";
    public const string NeuInnenraum = "Innenraum überarbeiten";
    public const string NeuLeer = "Leere Stadt neu bauen";
    public const string NeuOrt = "Welcher Innenraum?";
    public const string Anlegen = "Projekt anlegen";
    public const string Abbrechen = "Abbrechen";
    public const string VorschlagLeer = "Neue Stadt";

    /// <summary>
    /// Vorgegebener Projektname im Fenster "Neues Projekt" (Sammelliste 219 Punkt 3, mit Umlauten; die Datei heisst in ASCII,
    /// <see cref="Projekt.DateiName"/>): Stadt 1 = "Stadt 1 überarbeiten", Innenraum = "&lt;Ort&gt; umbauen", leer = "Neue Stadt".
    /// </summary>
    public static string Vorschlag(string art, string? ortName) => art switch
    {
        NeuInnenraum => (ortName ?? "Innenraum") + " umbauen",
        NeuLeer => VorschlagLeer,
        _ => NeuStadt,
    };

    public const string NameUngueltig = "Der Name passt nicht. Erlaubt sind Buchstaben, Ziffern, Leerzeichen, - und _ (1 bis 40 Zeichen).";

    // ---- Oeffnen ----
    public const string OeffnenTitel = "Projekt öffnen";
    public const string OeffnenDatei = "Projekt-Datei von woanders holen";
    public const string NurLesen = "Diese Datei kommt von einer neueren Version des Werkzeugs. Du kannst sie ansehen, aber nicht ändern. Bitte aktualisiere das Werkzeug.";

    // ---- Innenraeume ----
    public const string Ort = "Innenraum";
    public const string Katalog = "Katalog";
    public const string KatalogHinweis = "Tippe ein Stück an und dann auf die Stelle im Grundriss.";
    public const string SetzenAktiv = "Tippe auf die Stelle im Grundriss, an die das Stück soll.";
    public const string Auswahl = "Ausgewähltes Stück";
    public const string NichtsGewaehlt = "Tippe ein Stück im Grundriss an, um es zu bearbeiten.";

    /// <summary>Draufsicht draussen ohne Auswahl (Weisung P12-25: draussen heisst es "auf der Karte").</summary>
    /// <summary>Tipp einer Katalog-Kachel (Sammelliste 222): deutscher Name, englischer Name des Pakets (fuer die Suche), Art.</summary>
    public static string KatalogTipp(string deutsch, string englisch, string art) => deutsch + " (englisch: " + englisch + "), " + art;

    public const string NichtsGewaehltDraussen = "Tippe ein Stück auf der Karte an, um es zu bearbeiten.";
    public const string Drehen = "Drehen";

    /// <summary>Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 4): Drehen leicht finden, auch beim Setzen.</summary>
    public const string DrehenTipp = "Dreht das Stück: Straßen, Kurven, Flüsse und Teiche um 15 Grad, alles andere um 90 Grad. Am PC auch mit der Taste R, mit Umschalt+R zurück.";

    public const string SetzenWaagerecht = "Die Straße wird waagerecht gesetzt. „Drehen“ (oder Taste R) setzt sie senkrecht.";
    public const string SetzenSenkrecht = "Die Straße wird senkrecht gesetzt. „Drehen“ (oder Taste R) setzt sie waagerecht.";

    public static string SetzenGedreht(int vierteldrehungen) =>
        vierteldrehungen == 0 ? "Das Stück wird nicht gedreht gesetzt. „Drehen“ (oder Taste R) dreht es um 90 Grad."
        : "Das Stück wird um " + (vierteldrehungen * 90).ToString(System.Globalization.CultureInfo.InvariantCulture) + " Grad gedreht gesetzt. „Drehen“ (oder Taste R) dreht weiter.";
    public const string Kopie = "Kopieren";
    public const string Loeschen = "Löschen";
    public const string Ware = "Ware";
    public const string KeineWare = "keine Ware";
    public const string Breite = "Breite";
    public const string Tiefe = "Tiefe";
    public const string X = "Links (x)";
    public const string Y = "Oben (y)";
    public const string Uebernehmen = "Übernehmen";
    /// <summary>Welle E (S 0.13.0): Schalter "Einrasten" (ersetzt "Raster aus (feiner schieben)"), Taste E.</summary>
    public const string Einrasten = "Einrasten";

    /// <summary>Welle E: Feld "Winkel" (Grad) an Strassen, Kurven und Fluessen.</summary>
    public const string Winkel = "Winkel (Grad)";

    public const string WinkelTipp = "Richtung vom Anfang zum Ende in Grad: 0 = nach rechts, 90 = nach unten. Ganze Grad, dann „Übernehmen“.";

    public const string DrehgriffHinweis = "Am Kreis über dem Stück ziehen dreht es (Straßen, Kurven, Flüsse und Teiche in 15-Grad-Schritten, alles andere in 90-Grad-Schritten).";

    public const string EinrastenTipp = "An: Stücke rasten im Raster 0,5 ein, Straßen an Straßenenden, Flüsse an Flüsse und den Kartenrand, Häuser und Bänke an den Gehweg. Aus: frei setzen und schieben im feinen Raster 0,1. Taste E.";

    public const string EinrastenInnenTipp = "An: Einrichtung rastet im Raster 0,5 ein. Aus: feiner schieben im Raster 0,1. Taste E.";
    public const string Raum = "Raum";
    public const string RaumAendern = "Raum ändern";
    public const string Tuer = "Tür von / bis";
    public const string Boden = "Boden";
    public const string Grenzen = "Grenzen";
    public const string Pruefungen = "Prüfungen";
    public const string AllesGruen = "Alles in Ordnung.";
    public const string Vergroessern = "Vergrößern";
    public const string Verkleinern = "Verkleinern";
    public const string Einpassen = "Ganz zeigen";

    // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 2): die Ansicht frei bewegen
    public const string GanzeStadtZeigen = "Ganze Stadt zeigen";
    public const string GanzenRaumZeigen = "Ganzen Raum zeigen";
    public const string GanzeStadtZeigenTipp = "Zeigt die ganze Stadt. Verschieben: auf freier Fläche ziehen, mit der rechten oder mittleren Maustaste oder mit gehaltener Leertaste überall; am Tablet mit einem Finger auf freier Fläche oder mit zwei Fingern.";
    public const string GanzenRaumZeigenTipp = "Zeigt den ganzen Raum. Verschieben: auf freier Fläche ziehen, mit der rechten oder mittleren Maustaste oder mit gehaltener Leertaste überall; am Tablet mit einem Finger auf freier Fläche oder mit zwei Fingern.";
    public const string ZoomTipp = "Vergrößert oder verkleinert die Ansicht; dasselbe geht mit dem Mausrad und am Tablet mit zwei Fingern.";
    public const string Grundriss = "Grundriss";
    public const string DraufsichtHinweis = "Tippe ein Stück der Stadt an, um es zu bearbeiten, oder wähle links ein neues Stück. Ein Doppeltipp auf ein Haus mit Innenraum öffnet den Innenraum.";

    // ---- Stadt aussen (Welle 3) ----
    public const string Strasse = "Straße";
    public const string Haus = "Haus";
    public const string HausOhneOrt = "Gebäude ohne Innenraum";

    /// <summary>Gebaeude aus einem Blueprint mit Innenraum (nur im Werkzeug, Meldung P12-23 Frage 1 a).</summary>
    public const string NeuesGebaeude = "Gebäude aus Blueprint";
    public const string Laterne = "Laterne";
    public const string LaterneDoppelt = "Doppellaterne";
    public const string Beet = "Beet";
    public const string Baum = "Baum";
    public const string Park = "Park";
    public const string Paketplatz = "Paketplatz";

    // S 0.8.0 (Weisung P12-47, Plan P12-26b Abschnitt 10): Wasser
    public const string Fluss = "Fluss";
    public const string Teich = "Teich";
    public const string TeichRund = "Runder Teich";
    public const string Steg = "Steg";
    public const string Eckpunkt = "Eckpunkt";
    public const string Eckpunkte = "Eckpunkte";
    public const string EckpunktDazu = "Eckpunkt dazu";
    public const string EckpunktWeg = "Eckpunkt weg";
    public const string EckpunktWaehlen = "Tippe zuerst einen Eckpunkt an (die runden Griffe am Rand).";
    public const string EckpunktWenigstens = "Ein Teich braucht mindestens drei Eckpunkte, ein Fluss und eine Bahn zwei.";
    public const string EisImWinter = "Eis im Winter (Fluss und Teiche frieren zu)";
    public const string WasserGruppe = "Wasser";
    // ---- Welle K1 (S 0.10.0, Weisung P12-55): Kurven und Radien -------------------------------------------
    public const string Kurve90 = "Kurve 90°";

    public const string Kurve45 = "Kurve 45°";

    public const string SKurve = "S-Kurve";

    public const string Kurve90Tipp = "Setzt eine Kurve um 90 Grad mit dem gewählten Radius. Liegt ein Ende bis 0,5 neben einem Straßenende, rastet die Kurve dort ein.";

    public const string Kurve45Tipp = "Setzt eine Kurve um 45 Grad (Radius 2). Liegt ein Ende bis 0,5 neben einem Straßenende, rastet die Kurve dort ein.";

    public const string SKurveTipp = "Setzt eine S-Kurve aus zwei Bögen von 45 Grad (Radius 2): die Straße versetzt sich seitlich und läuft gerade weiter.";

    public const string KurvenRadius = "Radius der Kurve 90°";

    public const string KurvenRadiusTipp = "Radius in der Mitte der Fahrbahn: 1 ist eng, 3 ist weit. Gilt für die nächste Kurve 90°.";

    public const string Spiegeln = "Spiegeln";

    public const string SpiegelnTipp = "Tauscht links und rechts: die Kurve biegt in die andere Richtung, am PC auch mit der Taste M.";

    public const string KurveGespiegelt = "Kurve gespiegelt";

    public const string SpiegelnNurKurve = "Nur Kurven lassen sich spiegeln.";

    public const string KurveLaengeFest = "Eine Kurve hat eine feste Länge: Radius und Winkel bestimmen sie.";

    public static string SetzenKurveLage(int vierteldrehungen, bool gespiegelt) =>
        SetzenGedreht(vierteldrehungen) + (gespiegelt ? " Gespiegelt." : "");

    // ---- Welle K3 (S 0.11.0, Weisung P12-59): Kreisverkehr ---------------------------------------------------
    public static string KreisverkehrName(double radius) => string.Create(CultureInfo.GetCultureInfo("de-DE"), $"Kreisverkehr, Radius {radius:0.##}");

    public const string KreisverkehrKlein = "Kreisverkehr klein";

    public const string KreisverkehrGross = "Kreisverkehr groß";

    public const string KreisverkehrKleinTipp = "Setzt einen Kreisverkehr mit Radius 2: Ring mit Gehweg, Insel mit Rasen und Baum, vier gerade Zufahrten und vier Laternen. Eine Zufahrt darf weg (drei bleiben).";

    public const string KreisverkehrGrossTipp = "Setzt einen Kreisverkehr mit Radius 3: Ring mit Gehweg, Insel mit Rasen und Baum, vier gerade Zufahrten und vier Laternen. Eine Zufahrt darf weg (drei bleiben).";

    public const string KreisverkehrLaengeFest = "Ein Kreisverkehr hat eine feste Größe: der Radius bestimmt sie. Schieben und Drehen nehmen Zufahrten und Laternen mit.";

    public const string RadiusHier = "Radius an diesem Eckpunkt";

    public const string RadiusHierTipp = "So eng biegt der Fluss an diesem Eckpunkt: 2 ist eng, 3 wie bisher, 5 ist weit.";

    public const string FlussTipp = "Setzt einen Fluss. Ziehe seine Punkte wie bei einer Straße, er bleibt gleich breit. Wo eine Straße ihn quert, baut das Spiel eine Brücke.";
    public const string TeichTipp = "Setzt einen Teich mit Ufer. Ziehe die runden Griffe an den Ecken; die Ecken werden rund gezeichnet.";
    public const string TeichRundTipp = "Setzt einen runden Teich (acht Eckpunkte). Die Griffe lassen sich danach frei ziehen.";
    public const string StegTipp = "Setzt einen Holzsteg. Ein Ende muss auf dem Ufer liegen; über den Fluss gelegt ist er eine Fußgängerbrücke.";
    public const string EckpunktDazuTipp = "Setzt einen neuen Eckpunkt neben den gewählten (sonst in die Mitte der längsten Kante).";
    public const string EckpunktWegTipp = "Nimmt den gewählten Eckpunkt weg.";
    public const string EisTipp = "Nur im Winter: Fluss und Teiche frieren zu, Brücken und Stege bleiben. Ohne Haken sieht das Wasser im Winter aus wie bisher.";
    public const string StegLaenge = "Länge";
    public const string TippWasser = "Fluss, Teich und Steg. Wasser sperrt den Weg; über einen Fluss führen nur Brücken (Straße quer darüber) und Stege.";
    public const string StegBreite = "Breite";

    // Phase 13 Welle 3b (Weisung P12-19): voller CC0-Katalog
    public const string VollerKatalog = "Voller Katalog (CC0)";
    public const string VollerKatalogOeffnen = "Voller Katalog";
    public const string VollerKatalogHinweis = "Alle Modelle aller CC0-Pakete. Wähle eine Gruppe oder suche nach einem Namen (deutsch oder englisch), dann tippe ein Stück an.";
    public const string KatalogSuche = "Suche im Katalog";
    public const string KatalogSucheHinweis = "z. B. Stuhl, Baum, Auto, Kürbis";
    public const string KatalogSchliessen = "Katalog schließen";
    public const string KatalogGruppen = "Gruppen";
    public const string KatalogKeinTreffer = "Kein Stück gefunden. Versuche ein anderes Wort.";
    public const string KatalogMehr = "Weitere anzeigen";
    public const string KatalogLaedt = "Katalog wird geladen …";
    public const string KatalogFehlt = "Der volle Katalog ist in diesem Paket nicht enthalten.";
    public const string Effekt = "Effekt";
    public const string EffektHinweis = "Nur Aussehen: versperrt keinen Weg. Im Plan und in der 3D-Ansicht ein leuchtender Stern.";

    public static string KatalogTreffer(int zahl) => zahl == 1 ? "1 Stück" : zahl.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " Stücke";

    public static string KatalogDreiecke(int dreiecke) => dreiecke.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " Dreiecke";
    public const string Gehweg = "Gehweg";
    public const string GehwegBreite = "Gehweg je Seite (0 bis 0,6)";
    public const string Laenge = "Länge";
    public const string Modell = "Modell";
    public const string PaketVor = "Abstand vor der Tür";
    public const string PaketSeitlich = "Abstand zur Seite";
    public const string Doppelt = "Doppellaterne (zwei Lampen)";
    public const string AussenKatalog = "Stücke für draußen";
    public const string AussenSetzenAktiv = "Tippe auf die Stelle in der Karte, an die das Stück soll.";

    // S 0.9.1 (Weisung P12-53 Teil A): Setzen abwaehlen
    public const string SetzenBeenden = "Setzen beenden";
    public const string SetzenBeendenTipp = "Beendet das Setzen, ohne etwas auf die Karte zu stellen. Danach kannst du wieder Stücke antippen und die Karte frei bewegen. Am PC geht das auch mit der Taste Escape.";
    public const string SetzenBeendet = "Setzen beendet, nichts gesetzt.";
    public const string SetzenKnopfTipp = "Antippen wählt das Stück zum Setzen, nochmal antippen wählt es wieder ab.";
    public const string SetzenGewaehlt = "gewählt";
    public const string LaterneDrehenNicht = "Laternen richten sich im Spiel selbst zur Straße aus.";
    public const string DrehenNichtNoetig = "Dieses Stück sieht von allen Seiten gleich aus.";
    public const string OrtLoeschenNicht = "Ein Haus mit Innenraum gehört zum Spiel und bleibt. Du kannst es verschieben.";
    public const string KopieNicht = "Ein Haus mit Innenraum und den Paketplatz gibt es nur einmal.";
    public const string PaketLoeschenNicht = "Den Paketplatz gibt es genau einmal. Du kannst ihn verschieben.";
    public const string Ansicht3D = "3D-Ansicht";
    public const string Tag = "Tag";
    public const string Nacht = "Nacht";
    public const string Jahreszeit = "Jahreszeit";
    public const string Fest = "Fest";
    public const string KeinFest = "kein Fest";
    public const string ProbeLaufen = "Probe laufen";
    public const string ProbeLaufenHinweis = "Schalte „Figur laufen lassen“ ein und tippe in der 3D-Ansicht auf eine Stelle: die Figur läuft dorthin wie im Spiel. Solange der Schalter an ist, baust du nichts.";
    public const string FigurLaufen = "Figur laufen lassen";
    public const string Ansicht3DBaut = "Die 3D-Ansicht wird neu gebaut …";
    public const string Ansicht3DFehler = "Die 3D-Ansicht lässt sich gerade nicht bauen. Schau bei den Prüfungen nach.";
    public const string Ansicht3DOhneGrafik = "Auf diesem Gerät gibt es keine 3D-Grafik.";
    public const string GrenzenGesamt = "Grenzen in der Gesamtansicht (Nacht, Winter, Weihnachten)";
    public const string GrenzenMessen = "Grenzen messen";
    public const string GrenzenNochNicht = "Noch nicht gemessen. Tippe auf „Grenzen messen“.";
    public const string Ansicht3DHinweis = "Die 3D-Ansicht zeigt die Stadt so, wie das Spiel sie baut. Du baust hier wie in „Stadt von oben“: Stück links wählen und tippen setzt es, ein Stück antippen wählt es, ziehen verschiebt es. Auf freier Fläche ziehen bewegt die Kamera. Mit der rechten Maustaste drehst du sie (links und rechts) und kippst sie (hoch und runter); am Tablet drehst du sie mit zwei Fingern.";

    // S 0.7.0 (Weisung P12-45, Plan P12-26b Abschnitt 9): Bauen in der 3D-Ansicht
    public const string Bauen3DGehtNicht = "Hier geht das nicht.";
    public const string Bauen3DPasst = "Passt hier.";
    public const string Bauen3DPrueft = "Wird geprüft …";
    public const string Steiler = "Steiler";
    public const string Flacher = "Flacher";
    public const string KippenTipp = "Kippt die Kamera: steiler schaut mehr von oben, flacher mehr von der Seite. Dasselbe geht mit der rechten Maustaste hoch und runter und am Tablet mit zwei Fingern gemeinsam hoch und runter.";
    public const string GanzeStadt3DTipp = "Zeigt die ganze Stadt von oben. Bewegen: auf freier Fläche ziehen; Mausrad oder zwei Finger zoomen.";
    public static string Schatten(bool? passt, string? grund) => passt switch
    {
        true => Bauen3DPasst,
        false => "Geht hier nicht: " + grund,
        null => Bauen3DPrueft,
    };
    public const string PruefungenDraussen = "Prüfungen draußen";
    public const string PruefungLaeuft = "Prüfung läuft …";
    public const string HausSetzen = "Gebäude setzen";
    public const string KeineInnenraeume = "Diese Stadt hat noch keine Häuser mit Innenraum.";
    public const string LeerBreite = "Breite der Stadt (8 bis 64)";
    public const string LeerHoehe = "Tiefe der Stadt (8 bis 64)";
    public const string LeerGroesseUngueltig = "Die Größe passt nicht. Erlaubt sind 8 bis 64 in Schritten von 0,5.";

    // ---- Export und Senden ----
    public const string ExportGesperrt = "Datei kann noch nicht gespeichert werden";
    public const string ExportOk = "Datei gespeichert";
    public const string SendenFrage = "Die Datei geht an CC, der sie ins Spiel einbaut und prüft. Möchtest du eine Nachricht dazuschreiben? (freiwillig)";
    public const string SendenTitel = AnCcSenden;
    public const string Senden = "Senden";
    public const string Gesendet = "Gesendet";
    public const string SendenFehler = "Senden hat nicht geklappt";
    public const string SendenNichtVerfuegbar = "Senden ist in dieser Fassung nicht verfügbar. Nimm „" + DateiSpeichern + "“ und gib die Datei an CC.";
    public const string Ok = "OK";
    public const string Gespeichert = "Gespeichert";

    // ---- Blueprints (Phase 13 Welle 4c, Weisung P12-32) ----
    public const string MeineBlueprints = "Meine Blueprints";
    public const string BlueprintsHinweis = "Ein Blueprint ist ein Gebäude, das du gestaltet hast, als Vorlage. Antippen, dann auf die Karte tippen: dort steht es als neues Gebäude.";
    public const string KeineBlueprints = "Noch kein Blueprint. Wähle ein Haus und tippe auf „Als Blueprint speichern“.";
    public const string AlsBlueprintSpeichern = "Als Blueprint speichern";
    public const string BlueprintTitel = "Als Blueprint speichern";
    public const string BlueprintName = "Name des Blueprints";
    public const string BlueprintNameHinweis = "Buchstaben (auch ä, ö, ü, ß), Ziffern, Leerzeichen, - und _ (höchstens 40 Zeichen).";
    public const string BlueprintGespeichert = "Blueprint gespeichert";
    public const string BlueprintSetzen = "Blueprint setzen";
    public const string BlueprintGesetzt = "Blueprint gesetzt";
    public const string Umbenennen = "Umbenennen";
    public const string UmbenennenTitel = "Blueprint umbenennen";
    public const string BlueprintLoeschenTitel = "Blueprint löschen?";
    public const string BlueprintGeloescht = "Blueprint gelöscht";
    public const string BlueprintGibtEs = "Einen Blueprint mit diesem Namen gibt es schon.";
    public const string BlueprintKaputt = "Die Datei ist beschädigt und lässt sich nicht lesen.";
    public const string BlueprintKeiner = "Das ist keine Blueprint-Datei des Stadtbau-Werkzeugs.";
    public const string BlueprintNeuer = "Die Datei stammt aus einer neueren Version des Werkzeugs. Bitte das Werkzeug aktualisieren.";
    public const string BlueprintPruefsumme = "Die Datei wurde verändert oder ist beschädigt (Prüfsumme passt nicht).";
    public const string BlueprintNichtLesbar = "Die Datei lässt sich gerade nicht öffnen.";
    public const string BlueprintAusserhalb = "Die Stelle liegt außerhalb der Karte.";
    public const string BlueprintNurHaus = "Als Blueprint lassen sich Häuser und Gebäude speichern.";
    public const string BlueprintSchonDa = "Dieses Gebäude steht schon in der Stadt.";
    public const string BlueprintSchonDaTipp = "Das Spiel zeichnet die Tür nur am ersten Gebäude dieses Modells. Eines der beiden löschen.";

    public const string NeueGebaeude = "Gebäude aus Blueprints";
    public const string BauInnenNochNichtTipp = "Nichts geht verloren: der Innenraum bleibt im Projekt und im Blueprint.";

    public static string BauInnenNochNicht(string name) =>
        $"Der Innenraum von „{name}“ kommt erst mit den neuen Städten ins Spiel.";

    public static string BauName(string name) => name + " (neu)";

    public static string BlueprintModellAnders(string modell) =>
        $"In dieser Stadt gibt es schon ein anderes Modell mit dem Namen „{modell}“. Der Blueprint lässt sich hier nicht setzen.";

    public static string BlueprintLoeschenFrage(string name) =>
        $"Soll der Blueprint „{name}“ gelöscht werden? Die Datei kommt vorher in die Sicherungen.";

    public static string Grenzzeile(long dreiecke, int aufrufe, long grenzeDreiecke, int grenzeAufrufe) =>
        string.Create(System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
            $"{dreiecke:N0} von {grenzeDreiecke:N0} Dreiecken, {aufrufe} von {grenzeAufrufe} Zeichenaufrufen");

    // ---- Updates (Nachbesserung S 0.5.1, Weisung P12-42 Punkt 1) ----
    public const string UpdateSuchen = "Nach Updates suchen";
    public const string UpdateSuchenTipp = "Fragt bei GitHub nach, ob es eine neuere Version des Werkzeugs gibt. Deine Projekte bleiben beim Update erhalten.";
    public const string UpdateSucht = "Suche nach einer neuen Version …";
    public const string UpdateNeuTitel = "Neue Version";

    public static string UpdateNeuText(string? version) =>
        "Es gibt eine neue Version des Stadtbau-Werkzeugs" + (version is { Length: > 0 } v ? " (S " + v + ")" : "") + ". Deine Projekte bleiben erhalten.";

    public const string UpdateJetzt = "Jetzt aktualisieren";
    public const string UpdateSpaeter = "Später";
    public const string UpdateLaedt = "Neue Version wird geladen …";

    public static string UpdateAktuell(string anzeige) => "Du hast die neueste Version (" + anzeige + ").";

    public const string UpdateNichtInstalliert = "Dieses Werkzeug wurde nicht mit dem Installationsprogramm eingerichtet. Updates kommen nur in die installierte Fassung; lade die neue Version von der Download-Seite.";
    public const string UpdateFehler = "Die Suche hat nicht geklappt, vielleicht fehlt gerade das Internet. Versuche es später noch einmal.";

    // ---- Update am Tablet (S 0.6.0, Weisung P12-42 Punkt 2; Wortlaut nach dem Update der Lernplattform am Handy, A 0.39.0) ----
    public static string TabletJetztHinweis(string mb) =>
        "Das Werkzeug lädt die neue Version selbst (etwa " + mb + " MB), prüft sie und installiert sie.";

    public static string TabletMobileDaten(string mb) => "Du bist gerade nicht im WLAN. Das Update lädt etwa " + mb + " MB über mobile Daten.";

    public const string TabletLaeuft = "Die neue Version ist geprüft und wird installiert. Android schließt das Werkzeug dafür kurz; öffne es danach einfach wieder. Deine Projekte bleiben erhalten.";
    public const string TabletErlaubnisTitel = "Erlaubnis für Updates";
    public const string TabletErlaubnisText = "Damit sich das Werkzeug selbst aktualisieren kann, braucht es einmalig die Erlaubnis „Unbekannte Apps installieren“. "
        + "Tippe auf „Zur Einstellung“, schalte dort „Von dieser Quelle zulassen“ ein und tippe dann auf „Zurück“. Das Update geht danach von allein weiter.";
    public const string TabletZurEinstellung = "Zur Einstellung";
    public const string TabletAbbrechen = "Abbrechen";
    public const string TabletKeineErlaubnis = "Ohne die Erlaubnis „Unbekannte Apps installieren“ kann sich das Werkzeug nicht selbst aktualisieren. Die neue Version gibt es auch über „Zur Download-Seite“.";
    public const string TabletFehlerTitel = "Update hat nicht geklappt";
    public const string TabletZurSeite = "Zur Download-Seite";
    public const string TabletAbgebrochen = "Die Installation wurde abgebrochen. Deine Projekte sind unverändert. Du kannst es später erneut versuchen.";
    public const string TabletFremdeDatei = "Die geladene Datei stammt nicht vom Stadtbau-Werkzeug (Signatur stimmt nicht). Sie wurde gelöscht und nicht installiert. Deine Projekte sind unverändert.";
    public const string TabletNichtNeuer = "Die geladene Datei ist nicht neuer als die installierte Version. Sie wurde gelöscht und nicht installiert. Deine Projekte sind unverändert.";
    public const string TabletGesperrt = "Android hat die Installation gesperrt, zum Beispiel durch die „Automatische Sperre“ in den Samsung-Einstellungen. Deine Projekte sind unverändert.";

    /// <summary>
    /// Texte des gemeinsamen Update-Teils (Lernplattform.Core.Update, Wortlaut der Lernplattform) fuer das Werkzeug:
    /// wo die Lernplattform von sich oder vom Lernstand spricht, steht hier der Text des Werkzeugs; alle anderen bleiben.
    /// </summary>
    public static string TabletText(string text) => text switch
    {
        Lernplattform.Core.Texte.UpdateTexte.HandyAbgebrochen => TabletAbgebrochen,
        Lernplattform.Core.Texte.UpdateTexte.FremdeDatei => TabletFremdeDatei,
        Lernplattform.Core.Texte.UpdateTexte.NichtNeuer => TabletNichtNeuer,
        Lernplattform.Core.Texte.UpdateTexte.AndroidGesperrt => TabletGesperrt,
        Lernplattform.Core.Texte.UpdateTexte.KeineErlaubnis => TabletKeineErlaubnis,
        _ => text,
    };

    // ---- Beenden (Patch S 0.5.1, Weisung P12-35 Punkt 1) ----
    public const string BeendenTitel = "Werkzeug beenden?";
    public const string BeendenFrage = "Die letzte Änderung ist noch nicht gespeichert. Soll sie vor dem Beenden gespeichert werden?";
    public const string SpeichernUndBeenden = "Speichern und beenden";
    public const string OhneSpeichernBeenden = "Ohne Speichern beenden";
    public const string BeendenWartet = "Einen Moment, die Arbeit im Hintergrund wird fertig …";
    /// <summary>Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 5): automatisches Speichern gescheitert, der Schritt bleibt.</summary>
    public const string AutoSpeichernFehler = "Speichern hat gerade nicht geklappt, deine Änderung ist aber da und kann zurückgenommen werden. Der nächste Schritt versucht es wieder. Grund: ";
    public const string BeendenSpeichernFehler = "Speichern hat nicht geklappt, das Werkzeug bleibt offen: ";

    // ---- Tipps (Patch S 0.5.1, Weisung P12-35 Punkt 2): ein Satz je Knopf der Kopfleiste und je Gruppe links ----
    // Am PC beim Zeigen mit der Maus, am Tablet bei langem Druck; der Screenreader liest sie als Hilfetext.

    /// <summary>Tipp je Knopf der Kopfleiste (Schluessel = Beschriftung; Test: jeder Knopf hat einen).</summary>
    public static readonly IReadOnlyDictionary<string, string> KopfTipps = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Draufsicht] = "Zeigt die ganze Stadt von oben: Straßen, Häuser, Bäume und Laternen setzen, verschieben und löschen.",
        [Innenraeume] = "Zeigt den Grundriss eines Hauses von innen: Regale, Möbel und Waren einrichten.",
        [Ansicht3D] = "Zeigt die Stadt so, wie das Spiel sie zeichnet, bei Tag oder Nacht und zu jeder Jahreszeit.",
        [Neu] = "Legt ein neues Projekt an: Stadt 1 überarbeiten, einen Innenraum umbauen oder eine leere Stadt neu bauen.",
        [Oeffnen] = "Öffnet ein Projekt, an dem du schon gearbeitet hast.",
        [Speichern] = "Speichert das Projekt und legt zusätzlich eine Sicherung an (jede Änderung wird ohnehin sofort gespeichert).",
        [Rueckgaengig] = "Nimmt die letzte Änderung zurück (Strg+Z).",
        [Wiederholen] = "Macht die zuletzt zurückgenommene Änderung wieder (Strg+Y).",
        [DateiSpeichern] = "Prüft die Stadt und speichert sie als Datei, die CC ins Spiel einbauen kann.",
        [AnCcSenden] = "Prüft die Stadt und schickt die Datei für das Spiel direkt an CC.",
        [Dunkel] = "Schaltet zwischen hellen und dunklen Farben um.",
        [Vollbild] = "Zeigt das Werkzeug über den ganzen Bildschirm, am PC auch mit der Taste F11.",
        [WerkzeugBeenden] = "Schließt das Werkzeug, deine Arbeit ist dabei gespeichert.",
        [GebaeudeEntwerfen] = "Baut ein eigenes Gebäude aus Wänden, Dächern und Fenstern und speichert es als Blueprint.",
        [MenueProjekt] = TippMenueProjekt,
        [MenueEinstellungen] = TippMenueEinstellungen,
        [Tastenkuerzel] = TippTastenkuerzel,
        [UpdateSuchen] = "Fragt bei GitHub nach, ob es eine neuere Version des Werkzeugs gibt, deine Projekte bleiben dabei erhalten.",
    };

    /// <summary>Tipp je Gruppe der Liste links (Stadt von oben und Innenräume).</summary>
    public const string TippAussenKatalog = "Einfache Stücke für die Stadt: antippen, dann die Stelle auf der Karte antippen.";
    public const string TippVollerKatalog = "Öffnet alle rund 1 500 freien Grafiken (CC0) zum Suchen und Setzen.";
    public const string TippHausOhneOrt = "Setzt ein Gebäude ohne Innenraum, zum Beispiel ein Wohnhaus als Kulisse.";
    public const string TippMeineBlueprints = "Deine gespeicherten Gebäude-Vorlagen: antippen und auf die Karte setzen.";
    public const string TippOrt = "Wähle das Haus, dessen Innenraum du einrichten willst.";
    public const string TippKatalog = "Möbel und Regale für den Innenraum: antippen, dann die Stelle im Grundriss antippen.";

    public static string TippGruppe(string gruppe) => $"Stücke der Gruppe „{gruppe}“ für den Innenraum.";

    // ---- Gebaeude entwerfen (S 0.9.0, Weisung P12-48, Plan P12-26b Abschnitte 1 bis 5) ----
    public const string GebaeudeEntwerfen = "Gebäude entwerfen";
    public const string EntwurfFrage = "Ein neues Gebäude bauen oder einen eigenen Blueprint weiterbauen?";
    public const string EntwurfNeu = "Neues Gebäude";
    public const string EntwurfWeiter = "Weiterbauen";
    public const string EntwurfKeineEigenen = "Noch kein eigenes Gebäude als Blueprint gespeichert.";
    public const string EntwurfZurueck = "Zurück zur Stadt";
    public const string EntwurfZurueckFrage = "Das Gebäude ist noch nicht als Blueprint gespeichert. Trotzdem zurück zur Stadt? Was du gebaut hast, geht dann verloren.";
    public const string EntwurfVerwerfen = "Ja, verwerfen";
    public const string EntwurfTitelNeu = "Neues Gebäude";
    public const string EntwurfHinweis = "Teil links antippen, dann das Feld im Raster antippen. Ein Finger setzt oder wählt, zwei Finger bewegen die Kamera.";
    public const string EntwurfEbene = "Ebene";
    public const string EbeneDach = "5. Ebene (nur Dach)";
    public const string EntwurfTeile = "Bauteile";
    public const string EntwurfGruppeWaende = "Wände";
    public const string EntwurfGruppeEcken = "Ecken";
    public const string EntwurfGruppeTueren = "Türen";
    public const string EntwurfGruppeDaecher = "Dächer";
    public const string EntwurfGruppeDetails = "Fenster, Türen und Kleines";
    public const string EntwurfAuswahl = "Ausgewähltes Teil";
    public const string EntwurfNichtsGewaehlt = "Kein Teil gewählt. Tippe auf ein Teil im Gebäude.";
    public static string EntwurfGewaehlt(string name) => "Gewählt: " + name;
    public const string EntwurfNichtImRaster = "außerhalb des Bauplatzes";
    public const string EntwurfDrehen = "Drehen 90°";
    public const string EntwurfKopieren = "Kopieren";
    public const string EntwurfLoeschen = "Löschen";
    public const string EntwurfHoeher = "Ebene ▲";
    public const string EntwurfTiefer = "Ebene ▼";
    public const string EntwurfNord = "↑";
    public const string EntwurfSued = "↓";
    public const string EntwurfWest = "←";
    public const string EntwurfOst = "→";
    public const string EntwurfNachNord = "Ein Feld nach oben schieben";
    public const string EntwurfNachSued = "Ein Feld nach unten schieben";
    public const string EntwurfNachWest = "Ein Feld nach links schieben";
    public const string EntwurfNachOst = "Ein Feld nach rechts schieben";
    public const string EntwurfAlsEingang = "Als Eingang festlegen";
    public const string EntwurfIstEingang = "Das ist der Eingang.";
    public const string EntwurfGrenzen = "Grenzen dieses Gebäudes";
    public const string EntwurfPruefungen = "Prüfungen";
    public const string EntwurfAllesGut = "Alles in Ordnung.";
    public const string EntwurfNacht = "Nacht zeigen";
    public const string EntwurfSpeichern = "Als Blueprint speichern";
    public const string EntwurfGespeichert = "Gebäude als Blueprint gespeichert";
    public const string EntwurfGrundrissNeu = "Der Innenraum im Erdgeschoss wurde aus der Grundfläche neu vorgeschlagen.";
    public const string EntwurfRotSpeichern = "Mit roten Prüfungen lässt sich das Gebäude noch nicht speichern:";
    public const string EntwurfGanzZeigen = "Ganz zeigen";
    public const string EntwurfKameraDrehen = "Kamera drehen";

    public const string TippEntwurfWaende = "Wände mit Fenstern, Balkonen und Markisen: die Fassade zeigt nach außen.";
    public const string TippEntwurfEcken = "Ecken haben zwei Fassaden: mit „Drehen 90°“ an jede Ecke des Hauses anpassen.";
    public const string TippEntwurfTueren = "Wände mit Tür. Eine Tür im Erdgeschoss wird mit „Als Eingang festlegen“ der Eingang.";
    public const string TippEntwurfDaecher = "Dächer kommen auf die Ebene über den Wänden.";
    public const string TippEntwurfDetails = "Fensterrahmen, Haustüren und Klimageräte an eine Wand oder auf ein Flachdach setzen.";
    public const string TippEntwurfEbene = "Wählt die Ebene, auf der du baust; die Ebenen darüber sind ausgeblendet.";
    public const string TippEntwurfDrehen = "Dreht das Teil um 90 Grad im Uhrzeigersinn (Taste R).";
    public const string TippEntwurfKopieren = "Legt eine Kopie ins nächste freie Feld daneben.";
    public const string TippEntwurfLoeschen = "Nimmt das Teil weg (Taste Entf).";
    public const string TippEntwurfEbeneWechseln = "Schiebt das Teil eine Ebene höher oder tiefer.";
    public const string TippEntwurfPfeile = "Schiebt das Teil ein Feld weiter.";
    public const string TippEntwurfEingang = "Diese Tür wird der Eingang: hier kommen Tür und Fußweg in der Stadt hin.";
    public const string TippEntwurfSpeichern = "Prüft das Gebäude und speichert es unter einem Namen als Blueprint.";
    public const string TippEntwurfZurueck = "Schließt den Editor und zeigt wieder die Stadt.";
    public const string TippEntwurfNacht = "Zeigt das Gebäude bei Nacht: die Fenster leuchten wie im Spiel.";

    /// <summary>Weisung P12-52 (Wunsch Nico): Schalter im Gebaeude-Editor, Vorgabe an, gemerkt.</summary>
    public const string EntwurfRaster = "Raster zeigen";

    public const string TippEntwurfRaster = "Blendet die Linien des Bauraster ein oder aus (Taste G), damit du das Gebäude ungestört ansehen kannst. Setzen und Wählen gehen auch ohne Raster.";

    public const string RasterFehler = "Die Einstellung „Raster zeigen“ konnte nicht gespeichert werden. Sie gilt bis zum Beenden.";
    public const string TippEntwurfKamera = "Dreht die Kamera um das Gebäude.";

    /// <summary>Zahl mit Tausenderpunkt wie im Deutschen (1.528), unabhaengig von der Sprache des Geraets.</summary>
    public static string Tsd(int n) => n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));

    public static string EntwurfTitel(string name) => $"Gebäude entwerfen: {name}";

    public static string EntwurfStelle(int ebene, int x, int y) => $"{Entwurf.EbeneName(ebene)}, Feld {x + 1} · {y + 1}: ";

    public static string EntwurfTeilStelle(int ebene, int x, int y, string seite) => $"{Entwurf.EbeneName(ebene)}, Feld {x + 1} · {y + 1}, Fassade {seite}";

    public static string EntwurfGrenzText(int dreiecke) => $"{Tsd(dreiecke)} von {Tsd(Lernplattform.Core.Stadtbau.Bauteile.GrenzeDreiecke)} Dreiecken. Im Spiel ein Haus wie jedes andere, kein Zeichenaufruf je Teil.";

    public static string SeiteName(Lernplattform.Core.Stadtbau.Bauteile.Seiten s) => s switch
    {
        Lernplattform.Core.Stadtbau.Bauteile.Seiten.Nord => "oben (Norden)",
        Lernplattform.Core.Stadtbau.Bauteile.Seiten.Ost => "rechts (Osten)",
        Lernplattform.Core.Stadtbau.Bauteile.Seiten.Sued => "unten (Süden)",
        Lernplattform.Core.Stadtbau.Bauteile.Seiten.West => "links (Westen)",
        Lernplattform.Core.Stadtbau.Bauteile.Seiten.Keine => "keine",
        _ => string.Join(" und ", Lernplattform.Core.Stadtbau.Bauteile.Alle.Where(a => (s & a) != 0).Select(SeiteName)),
    };

    // Pruefungen im Editor (Plan Abschnitt 4)
    public const string EntwurfLeer = "Im Erdgeschoss steht noch kein Teil.";
    public const string EntwurfLeerTipp = "Eine Wand im Erdgeschoss setzen.";
    public const string EntwurfDoppelt = "Hier stehen zwei Teile im selben Feld.";
    public const string EntwurfDoppeltTipp = "Eines der beiden Teile löschen oder verschieben.";
    public const string EntwurfInDerLuft = "Das Teil hängt in der Luft.";
    public const string EntwurfInDerLuftTipp = "Unter einer Wand muss auf der Ebene darunter eine Wand stehen.";
    public const string EntwurfDetailTipp = "Fensterrahmen und Haustüren brauchen eine Wand im selben Feld, Klimageräte auf dem Dach ein Dach.";
    public const string EntwurfDachOhneWand = "Unter dem Dach steht keine Wand.";
    public const string EntwurfDachOhneWandTipp = "Ein Dach kommt auf die Ebene über einer Wand.";
    public const string EntwurfZuHoch = "Wände gehen höchstens bis zum 4. OG.";
    public const string EntwurfZuHochTipp = "Auf der 5. Ebene stehen nur Dächer.";
    public const string EntwurfKeinEingang = "Das Gebäude hat noch keinen Eingang.";
    public const string EntwurfKeinEingangTipp = "Eine Tür im Erdgeschoss wählen und „Als Eingang festlegen“ antippen.";
    public const string EntwurfMehrEingaenge = "Das Gebäude hat mehr als einen Eingang.";
    public const string EntwurfMehrEingaengeTipp = "Ein Gebäude hat genau einen Eingang; eine Tür neu als Eingang festlegen.";
    public const string EntwurfEingangOhneTuer = "Am Eingang ist keine Tür.";
    public const string EntwurfEingangOhneTuerTipp = "Eine Tür an diese Stelle setzen oder eine andere Tür als Eingang festlegen.";
    public const string EntwurfVorEingangBelegt = "Vor dem Eingang steht ein Teil.";
    public const string EntwurfVorEingangBelegtTipp = "Das Feld vor der Tür muss frei bleiben, dort beginnt der Fußweg.";
    public const string EntwurfZuVieleDreieckeTipp = "Weniger oder einfachere Teile verwenden (eine glatte Wand hat weniger Dreiecke als eine mit Fenstern).";
    public const string EntwurfZerfaellt = "Die Grundfläche zerfällt in zwei Teile.";
    public const string EntwurfZerfaelltTipp = "Alle Felder im Erdgeschoss müssen über eine Seite zusammenhängen.";
    public const string EntwurfUnbekanntTipp = "Das Teil gibt es in dieser Werkzeug-Version nicht; bitte löschen.";
    public const string EntwurfObenOffenTipp = "Auf die Ebene darüber ein Dach oder eine weitere Wand setzen.";
    public const string EntwurfFensterZuNachbar = "Ein Fenster zeigt in ein Nachbarteil.";
    public const string EntwurfFensterZuNachbarTipp = "Das Teil drehen, bis das Fenster nach außen zeigt, oder eine Wand ohne Fenster nehmen.";

    public static string EntwurfZuVieleDreiecke(int n) => $"Das Gebäude hat {Tsd(n)} Dreiecke, erlaubt sind höchstens {Tsd(Lernplattform.Core.Stadtbau.Bauteile.GrenzeDreiecke)}.";

    public static string EntwurfUnbekannt(string name) => $"Unbekanntes Teil „{name}“.";

    public static string EntwurfObenOffen(int n) => n == 1 ? "Ein Feld ist oben offen (kein Dach)." : $"{n} Felder sind oben offen (kein Dach).";

    // in der Stadt (bis zum Einbau W4b)
    public const string EigenesGebaeudeTipp = "Das Spiel zeichnet selbst gebaute Gebäude erst mit dem Einbau; bis dahin lässt sich die Stadt mit ihnen nicht als Datei speichern. Nichts geht verloren.";

    public static string EigenesGebaeudeNochNicht(string name) => $"Selbst gebautes Gebäude „{name}“: kommt mit dem Einbau ins Spiel.";

    // Welle Z (Weisung P12-71): Ziehen von Punkt zu Punkt und Fusswege
    public const string StrasseZiehen = "Straße ziehen";
    public const string StrasseZiehenTipp = "Anfang antippen, dann das Ende: das Werkzeug verbindet beide mit Geraden und Bögen und rastet an Straßenenden ein.";
    public const string FusswegZiehen = "Fußweg ziehen";
    public const string FusswegZiehenTipp = "Ein Weg nur zum Gehen (ohne Autos): Anfang antippen, dann das Ende; er rastet an Gehwegkanten und anderen Fußwegen ein.";
    public const string FlussZiehen = "Fluss ziehen";
    public const string FlussZiehenTipp = "Anfang antippen, dann das Ende: der Fluss biegt mit runden Ecken und rastet an Flussenden und am Kartenrand ein.";
    public const string Fussweg = "Fußweg";
    public const string Gezogen = "gezogen";
    public const string ZiehenAnfang = "Anfang antippen (oder mit dem Finger vom Anfang zum Ende ziehen). Abbrechen: Escape, Rechtsklick oder „Setzen beenden“.";
    public const string ZiehenEnde = "Ende antippen. Abbrechen: Escape, Rechtsklick oder „Setzen beenden“.";
    public const string ZiehenAbgebrochen = "Anfang verworfen, neuen Anfang antippen.";
    public const string ZiehenZuKurz = "Anfang und Ende liegen zu nah beieinander.";
    public const string ZiehenHinten = "Das Ende liegt hinter dem Anfang: so eng kann der Bogen nicht drehen.";
    public const string ZiehenZuEng = "Keine Verbindung mit den erlaubten Radien: zu eng. Das Ende weiter weg oder anders ansetzen.";
    public const string ZiehenAusserhalb = "Die Verbindung läge nicht ganz auf der Karte.";
    public const string FusswegDrehenNicht = "Einen Fußweg zieht man neu, statt ihn zu drehen.";

    public static string ZiehenRichtung(double grad) => string.Create(CultureInfo.GetCultureInfo("de-DE"),
        $"Die beiden Enden passen nicht zu Bögen in Schritten von 7,5 Grad (es fehlen {grad:0.#} Grad). Ein Ende auf ein Vielfaches von 7,5 Grad drehen.");

    // Welle K2 (Weisung P12-72): Bahn setzen und ziehen
    public const string Bahn = "Bahn";
    public const string BahnGruppe = "Bahn";
    public const string BahnTipp = "Setzt ein gerades Gleis (drei Eckpunkte). Ziehe den mittleren Punkt zur Seite für einen Bogen; wo eine Straße es quert, baut das Spiel einen Bahnübergang.";
    public const string TippBahn = "Gleise für den Zug. Bögen mit Radius 3 oder 4,5; Weichen gibt es nicht, zwei Gleise dürfen sich nicht kreuzen. Ein Bahnübergang geht nur, wo eine gerade Straße ein gerades Stück quert.";
    public const string BahnZiehen = "Bahn ziehen";
    public const string BahnZiehenTipp = "Anfang antippen, dann das Ende: das Gleis biegt mit Radius 4,5 oder 3 und rastet an Gleisenden und am Kartenrand ein.";
    public const string BahnRadiusTipp = "So eng biegt das Gleis an diesem Eckpunkt: 3 ist das engste, 4,5 ist weit.";

    public static string ZiehenGesetzt(string art, int stuecke) => stuecke <= 1 ? art + " gezogen" : $"{art} gezogen ({stuecke} Stücke)";

    // Welle W5 (Weisung P12-73, Plan P12-23b): Etagen, Treppe und Fahrstuhl
    public const string Etagen = "Etagen";
    public const string TippEtagen = "Die Etagen dieses Hauses. Antippen zeigt ihren Grundriss. Treppe und Fahrstuhl findest du im Katalog unter „Treppe und Fahrstuhl“.";
    public const string EtageHinzufuegen = "Etage hinzufügen";
    public const string EtageHinzufuegenTipp = "Legt oben eine neue Etage an. Vorschlag: die Außenwände und der Flur des Erdgeschosses, ohne Einrichtung.";
    public const string EtageHoechstens = "Mehr als vier Etagen über dem Erdgeschoss gehen nicht. Das Haus hat schon das Erdgeschoss und vier Obergeschosse.";
    public const string EtageUmbenennen = "Umbenennen";
    public const string EtageUmbenennenTipp = "Gibt der gezeigten Etage einen eigenen Namen (leer = „1. Obergeschoss“ usw.).";
    public const string EtageName = "Name der Etage";
    public const string EtageLoeschen = "Etage löschen";
    public const string EtageLoeschenTipp = "Löscht die gezeigte Etage. Das geht nur, wenn keine Treppe und kein Fahrstuhl dort steht.";
    public const string EtageLoeschenNicht = "Auf dieser Etage steht noch eine Treppe oder ein Fahrstuhl. Erst diese Stücke löschen, dann die Etage.";
    public const string EtageLoeschenTitel = "Etage löschen";
    public static string EtageLoeschenFrage(string name) => $"„{name}“ mit ihrer ganzen Einrichtung löschen?";
    public const string EgNichtLoeschen = "Das Erdgeschoss lässt sich nicht löschen.";
    public static string EtageAngelegt(string name) => name + " angelegt";
    public static string EtageGezeigt(string name) => name + " gezeigt";
    public const string Ziel = "Ziel";
    public const string ZielTipp = "Wohin führt dieses Stück? Das Werkzeug setzt das Gegenstück an dieselbe Stelle auf der gewählten Etage.";
    public const string ZielKeins = "noch nirgends hin";
    public const string ZielTreppeNurNachbar = "Eine Treppe führt nur eine Etage hoch oder runter. Lege zuerst mit „Etage hinzufügen“ eine Etage an.";
    public const string FahrstuhlZiele = "Hält auf";
    public const string FahrstuhlZieleTipp = "Haken setzen: auf dieser Etage hält der Fahrstuhl (das Werkzeug setzt dort einen Fahrstuhl an dieselbe Stelle).";
    public const string BlickDrehen = "Blick drehen";
    public const string BlickDrehenTipp = "Dreht die Blickrichtung der Figur bei der Ankunft um eine Vierteldrehung.";

    // S 0.19.0 (Weisung P12-80, Plan P12-51): Etagen aus dem Entwurf
    public static string EtageFehltImInnenraum(string ebene) => $"Die Ebene {ebene} hat im Entwurf Wände, aber keine Etage: noch nicht begehbar.";
    public static string EtageRagtHinaus(string etage, string ebene) => $"Das {etage} ragt über die Grundfläche der Ebene {ebene} im Entwurf hinaus.";
    public const string EtageRagtHinausTipp = "Eine Etage liegt ganz in den Wänden ihrer Ebene. „Vorschlag neu“ passt die Etagen an den Entwurf an, oder Breite und Tiefe verkleinern.";
    public static string EtagePasstNichtMehr(string etage, string ebene) => $"Das {etage} passt nicht mehr zur Ebene {ebene} im Entwurf (kleiner als ihre Grundfläche).";
    public const string EtagenVorschlagNeuTipp = "„Vorschlag neu“ legt die Etagen mit Treppenhaus neu aus dem Entwurf an. Die Einrichtung der Obergeschosse geht dabei verloren, das Erdgeschoss bleibt.";
    public const string EtagenVorschlagNeu = "Vorschlag neu";
    public const string EntwurfVorschlagNeuTipp = "Beim Speichern werden die Obergeschosse und das Treppenhaus neu aus dem Entwurf angelegt. Ihre Einrichtung geht dabei verloren, das Erdgeschoss bleibt.";
    public const string EtagenVorschlagNeuKnopfTipp = "Legt die Obergeschosse und das Treppenhaus neu aus dem Entwurf an. Rückgängig holt die alten Etagen zurück.";
    public const string EtagenVorschlagNeuTitel = "Etagen neu vorschlagen";
    public const string EtagenVorschlagNeuFrage = "Die Obergeschosse mit ihrer Einrichtung und das Treppenhaus werden neu aus dem Entwurf angelegt. Das Erdgeschoss bleibt. Weiter?";
    public static string EtagenVorschlagGemacht(int anzahl) => anzahl == 0 ? "Keine Etagen: der Entwurf hat keine Wände über dem Erdgeschoss." : anzahl == 1 ? "1 Etage mit Treppe neu vorgeschlagen" : $"{anzahl} Etagen mit Treppenhaus neu vorgeschlagen";
    public static string EntwurfEtagenAngelegt(int anzahl) => anzahl == 1 ? "Das 1. Obergeschoss wurde mit einer Treppe angelegt." : $"{anzahl} Obergeschosse wurden mit Treppenhaus angelegt.";
    public const string AnkunftHinweis = "Der Pfeil vor dem Stück ist die Ankunft: dort steht die Figur nach dem Wechsel und schaut in Pfeilrichtung. Ziehe ihn, um ihn zu verschieben.";
    public const string AnkunftVerschoben = "Ankunft verschoben";
    public const string ZielGesetzt = "Ziel gesetzt";

    // Welle Q (Weisung P12-76, Kurzplan Meldung P12-42): neue Ordnung der Knoepfe
    public const string MenueProjekt = "Projekt";
    public const string MenueEinstellungen = "Einstellungen";
    public const string Tastenkuerzel = "Tastenkürzel anzeigen";
    public const string TastenTitel = "Tastenkürzel";
    public const string Schliessen = "Schließen";
    public const string SpeicherStand = "gespeichert ✓";
    public const string NichtGespeichert = "nicht gespeichert";
    public static string ProjektTitel(string name) => "Projekt „" + name + "“";
    public const string GruppeStrassen = "Straßen und Wege";
    public const string GruppeNatur = "Natur und Licht";
    public const string GruppeGebaeude = "Gebäude";
    public const string TippGruppeStrassen = "Straßen, Kurven, Kreisverkehr, Fußwege und die Bahn; Straßen, Fußwege und Gleise lassen sich auch von Punkt zu Punkt ziehen.";
    public const string TippGruppeNatur = "Bäume, Parks, Beete und Laternen.";
    public const string TippGruppeGebaeude = "Gebäude ohne Innenraum und deine gespeicherten Blueprints.";
    public const string StueckSuchen = "Stück suchen";
    public const string StueckSuchenPlatzhalter = "Stück suchen …";
    public const string StueckSuchenTipp = "Zeigt nur die Knöpfe, deren Name den Text enthält, zum Beispiel „Kurve“; ein leeres Feld zeigt wieder alle Gruppen.";
    public const string SucheKeinTreffer = "Nichts gefunden. Im vollen Katalog gibt es noch viel mehr Stücke.";
    public const string Pruefen = "Prüfen";
    public const string PruefenGruen = "Prüfen – alles in Ordnung";
    public static string PruefenRot(int n) => "Prüfen – " + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + " rot";
    public const string Vorschau = "Vorschau";
    public const string InnenraeumeOeffnen = "Innenräume öffnen";
    public const string Hilfe3D = "So bedienst du die 3D-Ansicht";
    public const string KameraKarte = "Ansicht";
    public const string GrenzenKurz = "Grenzen (Nacht, Winter, Weihnachten)";

    public const string Hilfe3DKnopf = "Hilfe zur 3D-Ansicht";
    public const string Hilfe3DTipp = "Erklärt, wie du in der 3D-Ansicht baust und die Kamera bewegst.";
    public const string ReiterName = "Ansicht wählen";
    public const string TastenTablet = "Am Tablet gibt es keine Tasten: Dort zeigt ein langer Druck auf einen Knopf, was er tut, und zwei Finger bewegen die Ansicht.";
    public const string Version = "Version";

    /// <summary>Tipps der Kopfleiste fuer die Knoepfe, die Welle Q dazubringt (die anderen stehen in <see cref="KopfTipps"/>).</summary>
    public const string TippMenueProjekt = "Neues Projekt, Projekt öffnen und speichern, die Stadt-Datei für das Spiel speichern oder an CC senden.";
    public const string TippMenueEinstellungen = "Helle oder dunkle Farben, Vollbild, nach Updates suchen, Tastenkürzel und die Version.";
    public const string TippTastenkuerzel = "Zeigt alle Tasten, mit denen du schneller arbeitest, am PC auch mit der Taste F1 oder dem Fragezeichen.";

    /// <summary>
    /// Welle Q: alle Tastenkuerzel des Werkzeugs (Fenster „Tastenkürzel“). Spalte 3 nennt die Tasten des Programms
    /// (Avalonia-Namen), damit ein Test pruefen kann, dass jede abgefragte Taste hier steht.
    /// </summary>
    public static readonly IReadOnlyList<(string Taste, string Wirkung, string[] Tasten)> TastenListe =
    [
        ("Strg+Z / Strg+Y", "Rückgängig / Wiederholen", ["Z", "Y"]),
        ("Strg+S", "Projekt speichern", ["S"]),
        ("Strg+D", "Ausgewähltes Stück kopieren", ["D"]),
        ("Entf / Rücktaste", "Ausgewähltes Stück löschen", ["Delete", "Back"]),
        ("R / Umschalt+R", "Drehen (auch beim Setzen), Umschalt dreht andersherum", ["R"]),
        ("M", "Kurve spiegeln", ["M"]),
        ("E", "Einrasten an oder aus", ["E"]),
        ("Esc", "Setzen beenden, Auswahl aufheben", ["Escape"]),
        ("Pfeiltasten", "Ausgewähltes Stück verschieben (3D ohne Auswahl: Ansicht schieben)", ["Left", "Right", "Up", "Down"]),
        ("Leertaste + ziehen", "Ansicht schieben", ["Space"]),
        ("Bild hoch / runter", "3D-Ansicht steiler / flacher", ["PageUp", "PageDown"]),
        ("Q", "3D-Ansicht drehen", ["Q"]),
        ("+ / −", "3D-Ansicht näher / weiter weg", ["OemPlus", "Add", "OemMinus", "Subtract"]),
        ("G", "Raster zeigen (Gebäude entwerfen)", ["G"]),
        ("F11", "Vollbild", ["F11"]),
        ("F1 / ?", "Diese Übersicht", ["F1"]),
    ];
}
