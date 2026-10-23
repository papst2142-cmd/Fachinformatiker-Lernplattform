namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 4, W4: Optionen "Lerninhalte" &gt; "Rahmenplan" (fisi_rahmenplan 604-620, Wortliste W4 B).
/// Auch im Rundgang "Jetzt einrichten" (Schalter B/D/E, Termine). Kein Platzhalter im Datumsfeld (N12).
/// </summary>
public static class RahmenplanTexte
{
    public const string Titel = "Rahmenplan";                                                  // OPTIONS_TITLE
    public const string Untertitel = "Abdeckung im Fortschritt und Reihenfolge neuer Aufgaben"; // OPTIONS_SUBTITLE
    public const string NurDiesesGeraet = Optionen.NurDiesesGeraet;

    /// <summary>SECTION_OPTION_TEXT, Reihenfolge B, D, E (OPTIONAL_SECTIONS).</summary>
    public static readonly IReadOnlyList<(string Abschnitt, string Text)> AbschnittTexte =
    [
        ("B", "Abschnitt B anzeigen (Anwendungsentwicklung)"),
        ("D", "Abschnitt D anzeigen (Daten- und Prozessanalyse)"),
        ("E", "Abschnitt E anzeigen (Digitale Vernetzung)"),
    ];

    public const string Gewichtung = "Übungsauswahl nach Rahmenplan gewichten";               // WEIGHT_OPTION_TEXT
    public const string GewichtungHinweis = "Neue Aufgaben zu Punkten mit viel Ausbildungszeit kommen etwas "
        + "früher dran, vor einem Prüfungstermin die prüfungsrelevanten. Wiederholungen bleiben unverändert.";   // WEIGHT_OPTION_HINT
    public const string Lernfeld = "Aktuelles Lernfeld";                                       // FIELD_OPTION_TEXT
    public const string LernfeldAus = "Aus";                                                   // FIELD_OPTION_OFF
    public const string TerminAp1 = "Termin AP1";                                              // AP1_DATE_TEXT
    public const string TerminAp2 = "Termin AP2";                                              // AP2_DATE_TEXT
    public const string DatumFormat = "TT.MM.JJJJ";                                            // DATE_HINT (nur G35, N12)
    public const string DatumUngueltig = "Bitte ein Datum im Format TT.MM.JJJJ eingeben.";     // DATE_INVALID

    /// <summary>SECTION_OPTION_TEXT[abschnitt].</summary>
    public static string AbschnittText(string abschnitt) => AbschnittTexte.First(a => a.Abschnitt == abschnitt).Text;
}
