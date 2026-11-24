namespace Lernplattform.Core.Spiel;

// Welle A 1.6.0 (Sammelliste 444): Typen des Spiel-Kerns (Lernplattform.Core), darum nicht im Namensraum der Engine.

/// <summary>Symbol einer Handlung (Knopf ueber dem linken Stick, Anzeige am PC).</summary>
public enum HandlungArt
{
    /// <summary>Tuer: Hineingehen, Zur Stadt, Raumtuer oeffnen oder schliessen, Warten vor der Tuer.</summary>
    Tuer,

    /// <summary>Bus und Zug: Einsteigen, Aussteigen, Haelt gleich, Zug fahren.</summary>
    Bus,

    /// <summary>Alles andere an der Stelle: Schrank, Regal, Werkbank, PC, Bett, Person, Treppe ...</summary>
    Hand,
}

/// <summary>Eine Handlung an der Stelle der Figur: Aktion, Text und Symbol.</summary>
public readonly record struct Handlung(string Aktion, string Name, HandlungArt Art);
