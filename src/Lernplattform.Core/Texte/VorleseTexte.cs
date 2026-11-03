namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 11 W2 (Weisung P11-20): Namen, die nur der Screenreader vorliest (NVDA am PC, TalkBack am Handy), fuer
/// Bedienelemente ohne eigene sichtbare Beschriftung. Nur C#-Linie, kein Vergleich mit Python; nicht sichtbar, die
/// Optik bleibt gleich. PC und Handy lesen dasselbe.
/// </summary>
public static class VorleseTexte
{
    /// <summary>Spielseite mit der Stadt in 3D (die Stadt selbst liest der Screenreader nicht vor, Antwort 2a).</summary>
    public const string Stadt = "Spiel, Stadt";

    /// <summary>Abschlussprojekt, Karte "Testprojekt als Vorlage": Auswahl des Fachbereichs.</summary>
    public const string VorlageFachbereich = "Fachbereich des Testprojekts";

    /// <summary>Abschlussprojekt, Karte "Testprojekt als Vorlage": Auswahl des Testprojekts.</summary>
    public const string VorlageTestprojekt = "Testprojekt";

    /// <summary>Optionen, "Über das Programm": Feld mit dem vollen Lizenztext.</summary>
    public const string Lizenztext = "Lizenztext";

    /// <summary>Optionen, Sicherung: Feld mit dem Ergebnis von "Alte Sicherung pruefen".</summary>
    public const string SicherungErgebnis = "Ergebnis der Sicherungsprüfung";

    /// <summary>Spiel, Rack: Auswahl des Geraets.</summary>
    public const string RackGeraet = "Gerät wählen";
}
