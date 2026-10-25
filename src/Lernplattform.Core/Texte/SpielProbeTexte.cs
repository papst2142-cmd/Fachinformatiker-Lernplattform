namespace Lernplattform.Core.Texte;

/// <summary>
/// Texte der 3D-Stichprobe (Phase 7 W1). Nur fuer die Probe; die Texte des Spiels kommen mit
/// dem Spiel-Kern (W2) und der Stadt (W4) und gehen vorher zu Chat (Verhaltenszusage).
/// </summary>
public static class SpielProbeTexte
{
    public const string Titel = "Spiel (Probe 3D)";
    public const string Hinweis = "Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Ziehen verschiebt die Ansicht, zwei Finger zoomen und drehen. Am PC: Mausrad zoomt, Pfeiltasten verschieben, Q und E drehen.";
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
    public const string NichtErreichbar = "Dort kommst du nicht hin.";
    public const string ImFreien = "Tippe auf ein Haus, einen Namen oder einen freien Platz, um hinzugehen.";
    public const string ImBuero = "Büro der Bitweiche. Tippe auf den Boden, um hinzugehen.";
    public const string Spielzeit = "Spielzeit {0} Uhr";
    public const string Bilder = "{0} Bilder/s";
    public const string OhneGrafik = "3D ist auf diesem Gerät nicht verfügbar (keine Grafikbeschleunigung). Die Orte erreichst du über die Liste.";
    public const string GrafikStartet = "3D startet …";

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
