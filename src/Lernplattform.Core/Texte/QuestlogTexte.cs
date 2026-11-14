namespace Lernplattform.Core.Texte;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 301): Texte des Questlogs im Handy (PC und Handy gleich).
/// </summary>
public static class QuestlogTexte
{
    /// <summary>Titel des Fensters.</summary>
    public const string Titel = "Aufträge";

    /// <summary>Reiter mit Zahl.</summary>
    public const string Offen = "Offen ({0})";

    public const string Erledigt = "Erledigt ({0})";

    /// <summary>Knopf je Eintrag: die Figur geht zum Ort (ohne 3D: kommt sofort an).</summary>
    public const string Hingehen = "Hingehen";

    public const string Schliessen = "Schließen";

    /// <summary>Zeile Questgeber.</summary>
    public const string Von = "Von: {0}";

    /// <summary>Zeile Ort.</summary>
    public const string Wo = "Wo: {0}";

    public const string MitEtage = "{0}, {1}. Etage";

    /// <summary>Welle E4: Raum am Arbeitsort („Büro, Raum Technik“).</summary>
    public const string MitRaum = "{0}, Raum {1}";

    /// <summary>Welle E4: der Questgeber wartet am Arbeitsort (nur dann; bei „wartet nicht“ fehlt der Satz).</summary>
    public const string WartetDort = "{0} wartet dort auf dich.";

    public const string Geoeffnet = " (geöffnet {0} bis {1} Uhr)";

    public const string Draussen = "draußen vor {0}";

    public const string DraussenOhneOrt = "draußen in der Stadt";

    public const string NeuHeute = "neu heute";

    /// <summary>Kette: {0} Stelle, {1} Laenge, {2} Schritte mit Zeichen.</summary>
    public const string Kette = "Kette {0} von {1}: {2}";

    public const string KeineOffenen = "Heute ist kein Auftrag mehr offen.";

    public const string KeineErledigten = "Du hast noch keinen Auftrag erledigt.";

    /// <summary>Mit eigener Firma gibt es keine Tickets mehr (wie das Spiel).</summary>
    public const string MitFirma = "Mit deiner eigenen Firma bekommst du keine Aufträge mehr von anderen.";
}
