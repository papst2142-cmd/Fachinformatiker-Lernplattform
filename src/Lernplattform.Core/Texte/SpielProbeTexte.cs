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
    /// „Ziehen verschiebt die Ansicht … Pfeiltasten verschieben …“).
    /// </summary>
    public const string Hinweis = "Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Zwei Finger drehen und neigen die Kamera und zoomen ein wenig. Am PC: rechte Maustaste dreht und neigt, Mausrad zoomt ein wenig, Q und E drehen, R und F neigen. Die ganze Stadt zeigt die Karte im Handy.";

    /// <summary>Hinweis mit den gewaehlten Tasten (Tastenbelegung); mit Standard genau <see cref="Hinweis"/>.</summary>
    public static string HinweisFuer(Tasten tasten)
    {
        string T(string aufgabe) => tasten.AnzeigeFuer(aufgabe);
        return "Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Zwei Finger drehen und neigen die Kamera und zoomen ein wenig. "
            + $"Am PC: rechte Maustaste dreht und neigt, Mausrad zoomt ein wenig, {T(Tasten.DrehLinks)} und {T(Tasten.DrehRechts)} drehen"
            // ohne Taste fuers Neigen (Zusammenstoss mit einer eigenen Wahl aus frueherer Fassung) nennt der Hinweis keine
            + (tasten.Tasten(Tasten.Steiler).Contains(Tasten.OhneTaste) || tasten.Tasten(Tasten.Flacher).Contains(Tasten.OhneTaste)
                ? "" : $", {T(Tasten.Steiler)} und {T(Tasten.Flacher)} neigen")
            + ". Die ganze Stadt zeigt die Karte im Handy.";
    }

    public const string Orte = "Orte";

    /// <summary>Weisung P8-52 (Text von Chat angenommen): Knopf der Leistungsprobe, nur in der Probe-APK.</summary>
    public const string Leistungsprobe = "Leistungsprobe";
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

    /// <summary>Erste Zeile im Sitzen.</summary>
    public const string DuSitzt = "Du sitzt. Zum Aufstehen tippe woanders hin oder auf „Aufstehen“.";

    /// <summary>Erste Zeile im Liegen.</summary>
    public const string DuLiegst = "Du liegst im Bett. Zum Aufstehen tippe woanders hin oder auf „Aufstehen“.";

    /// <summary>Am Schreibtisch, solange die Figur steht (die PC-Knoepfe gibt es erst im Sitzen).</summary>
    public const string ErstHinsetzen = "Setz dich auf den Stuhl (antippen oder „Hinsetzen“), dann geht der PC an.";

    /// <summary>Am Bett, solange die Figur steht („Schlafen“ gibt es erst im Liegen).</summary>
    public const string ErstHinlegen = "Leg dich ins Bett (antippen oder „Hinlegen“), dann kannst du schlafen.";

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
    /// <summary>
    /// Weisung P12-74: Hinweis in einem Geschaeft auf einer Etage ohne Verkaufsperson (Obergeschoss); im Erdgeschoss bleibt
    /// „Tippe auf eine Ware oder sprich mit dem Verkauf.“
    /// </summary>
    public const string EtageOhneVerkauf = Spiel.Probe.Innenraum.EtageOhneVerkauf;

    public const string ImFreien = "Tippe auf ein Haus, einen Namen oder einen freien Platz, um hinzugehen.";
    // Phase 8 W10 (Sammelliste 148, Nico 1b): Klappknopf des Hinweisfelds (Bildschirmleser)
    public const string HinweisEinklappen = "Hinweis einklappen";
    public const string HinweisAusklappen = "Hinweis ausklappen";
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
}
