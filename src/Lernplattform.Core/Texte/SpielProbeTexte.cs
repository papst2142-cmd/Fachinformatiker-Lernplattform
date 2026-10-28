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
    public const string Hingehen = "Hingehen";
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
    public const string ImFreien = "Tippe auf ein Haus, einen Namen oder einen freien Platz, um hinzugehen.";
    // Phase 8 W10 (Sammelliste 148, Nico 1b): Klappknopf des Hinweisfelds (Bildschirmleser)
    public const string HinweisEinklappen = "Hinweis einklappen";
    public const string HinweisAusklappen = "Hinweis ausklappen";
    public const string ImBuero = "Büro der Bitweiche. Tippe auf den Boden, um hinzugehen.";
    public const string Spielzeit = "Spielzeit {0} Uhr";
    public const string Bilder = "{0} Bilder/s";
    public const string OhneGrafik = "3D ist auf diesem Gerät nicht verfügbar (keine Grafikbeschleunigung). Die Orte erreichst du über die Liste.";
    public const string GrafikStartet = "3D startet …";

    // Weisung P7-17/P7-18 (Text von Chat angenommen): ein Spielschritt wurde im Hintergrund nicht gespeichert
    public const string SchrittNichtGespeichert = "Ein Spielschritt konnte nicht gespeichert werden. Das Spiel lädt den letzten gespeicherten Stand.";

    // W5: Einrichten der Wohnung, Beschriftungen wie Python (mobile/src/spiel.py _home_editor, fisi_game_gui HomeView)
    public const string Einrichten = "Einrichten";
    public const string Fertig = "Fertig";
    public const string Drehen = "Drehen";
    public const string InDenKarton = "In den Karton";
    public const string Aufstellen = "Aufstellen";
    public const string Ausgewaehlt = "Ausgewählt";
    public const string ImKarton = "Im Karton";
    public const string AllesAusgepackt = "Alles ist ausgepackt.";
    public const string EinrichtenAufstellen = "Tippe auf den Boden, wo es hin soll. Ein zweites Tippen auf die Vorschau stellt es auf.";
    public const string EinrichtenVerschieben = "Tippe auf den Boden, um es dorthin zu stellen, oder drehe es.";
}
