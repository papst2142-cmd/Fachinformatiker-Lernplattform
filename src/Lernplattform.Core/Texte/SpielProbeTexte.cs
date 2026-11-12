using Tasten = Lernplattform.Core.Spiel.Probe.Tastenbelegung;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Texte der 3D-Stichprobe (Phase 7 W1). Nur fuer die Probe; die Texte des Spiels kommen mit
/// dem Spiel-Kern (W2) und der Stadt (W4) und gehen vorher zu Chat (Verhaltenszusage).
/// </summary>
public static class SpielProbeTexte
{
    public const string Titel = "Spiel (Probe 3D)";
    /// <summary>Hinweis mit der Standardbelegung (Phase 8 W8: rechte Maustaste dreht, Text von Chat angenommen, P8-16 E2 Punkt 5).</summary>
    public const string Hinweis = "Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Ziehen verschiebt die Ansicht, zwei Finger zoomen und drehen. Am PC: Mausrad zoomt, Pfeiltasten verschieben, rechte Maustaste oder Q und E drehen.";

    /// <summary>Hinweis mit den gewaehlten Tasten (Tastenbelegung); mit Standard genau <see cref="Hinweis"/>.</summary>
    public static string HinweisFuer(Tasten tasten)
    {
        string T(string aufgabe) => tasten.AnzeigeFuer(aufgabe);
        var pfeile = tasten.Eigen.Keys.Any(a => a is Tasten.Links or Tasten.Rechts
            or Tasten.Hoch or Tasten.Runter)
            ? $"{T(Tasten.Links)}, {T(Tasten.Rechts)}, {T(Tasten.Hoch)} und {T(Tasten.Runter)}"
            : "Pfeiltasten";
        return "Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Ziehen verschiebt die Ansicht, zwei Finger zoomen und drehen. "
            + $"Am PC: Mausrad zoomt, {pfeile} verschieben, rechte Maustaste oder {T(Tasten.DrehLinks)} und {T(Tasten.DrehRechts)} drehen.";
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
