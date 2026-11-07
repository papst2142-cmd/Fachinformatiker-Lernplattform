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
    public const string NichtsGewaehltDraussen = "Tippe ein Stück auf der Karte an, um es zu bearbeiten.";
    public const string Drehen = "Drehen";

    /// <summary>Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 4): Drehen leicht finden, auch beim Setzen.</summary>
    public const string DrehenTipp = "Dreht das Stück um 90 Grad, am PC auch mit der Taste R.";

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
    public const string RasterAus = "Raster aus (feiner schieben)";
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
    public const string HausDrehenNicht = "Häuser schauen im Spiel immer mit der Vorderseite nach Süden. Drehen kann das Spiel noch nicht.";
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
        [Rueckgaengig] = "Nimmt die letzte Änderung zurück.",
        [Wiederholen] = "Macht die zuletzt zurückgenommene Änderung wieder.",
        [DateiSpeichern] = "Prüft die Stadt und speichert sie als Datei, die CC ins Spiel einbauen kann.",
        [AnCcSenden] = "Prüft die Stadt und schickt die Datei für das Spiel direkt an CC.",
        [Dunkel] = "Schaltet zwischen hellen und dunklen Farben um.",
        [Vollbild] = "Zeigt das Werkzeug über den ganzen Bildschirm, am PC auch mit der Taste F11.",
        [WerkzeugBeenden] = "Schließt das Werkzeug, deine Arbeit ist dabei gespeichert.",
    };

    /// <summary>Tipp je Gruppe der Liste links (Stadt von oben und Innenräume).</summary>
    public const string TippAussenKatalog = "Einfache Stücke für die Stadt: antippen, dann die Stelle auf der Karte antippen.";
    public const string TippVollerKatalog = "Öffnet alle rund 1 500 freien Grafiken (CC0) zum Suchen und Setzen.";
    public const string TippHausOhneOrt = "Setzt ein Gebäude ohne Innenraum, zum Beispiel ein Wohnhaus als Kulisse.";
    public const string TippMeineBlueprints = "Deine gespeicherten Gebäude-Vorlagen: antippen und auf die Karte setzen.";
    public const string TippOrt = "Wähle das Haus, dessen Innenraum du einrichten willst.";
    public const string TippKatalog = "Möbel und Regale für den Innenraum: antippen, dann die Stelle im Grundriss antippen.";

    public static string TippGruppe(string gruppe) => $"Stücke der Gruppe „{gruppe}“ für den Innenraum.";
}
