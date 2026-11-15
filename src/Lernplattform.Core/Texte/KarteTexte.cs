namespace Lernplattform.Core.Texte;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268, Nico Antwort 5 und P12-258): Texte der Karte im Spiel-Handy (PC und Handy gleich).
/// </summary>
public static class KarteTexte
{
    /// <summary>Titel des Handy-Fensters (Auftraege und Karte).</summary>
    public const string HandyTitel = "Handy";

    /// <summary>Welle A 0.81.0 (Sammelliste 348): Name des X oben rechts im Spiel-Handy (Screenreader, Tipp).</summary>
    public const string HandySchliessen = "Handy schließen";

    /// <summary>Reiter im Handy und Knopf draussen (ersetzt mit 3D den Knopf „Orte“).</summary>
    public const string Karte = "Karte";

    /// <summary>Satz ueber der Karte.</summary>
    public const string Antippen = "Tippe auf einen Ort, dann geht deine Figur sofort los.";

    public const string Du = "Du";

    public const string Haltestelle = "Haltestelle";

    public const string Aufgabe = "Aufgabe";

    /// <summary>Eintrag der Liste: Nummer auf der Karte und Name.</summary>
    public const string Eintrag = "{0}  {1}";

    /// <summary>Zusatz am Eintrag, wenn dort ein offener Auftrag wartet.</summary>
    public const string AufgabeWartet = "Aufgabe wartet";

    /// <summary>Name des Eintrags fuer den Screenreader.</summary>
    public const string EintragName = "Hingehen zu {0}";

    public const string EintragNameAufgabe = "Hingehen zu {0}, dort wartet eine Aufgabe";

    /// <summary>Name der gezeichneten Karte fuer den Screenreader (die Orte stehen als Knoepfe darunter).</summary>
    public const string BildName = "Karte der Stadt mit {0} Orten. Jeder Ort steht als Knopf in der Liste darunter.";

    /// <summary>Hier stehst du (Ort, an dem die Figur gerade ist).</summary>
    public const string HierBistDu = "Du bist hier";
}
