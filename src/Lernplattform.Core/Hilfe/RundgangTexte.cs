namespace Lernplattform.Core.Hilfe;

/// <summary>
/// Phase 4, W4: Beschriftungen von Rundgang, Einrichten, Hilfe-Seite und Optionen "Rundgang und Hilfe"
/// (fisi_hilfe 150-227, Wortliste W4 A) und der Ausbildungsfrage mit Warnhinweis (N3, N5, N6, N8).
/// Getrennt von <see cref="HilfeTexte"/>, damit der Begriffe-Test die Beschriftungen findet (Nachtrag P4-11).
/// PC = Handy; nur die Tastenzeile steht in HilfeTexte.TourTasten (B13).
/// </summary>
public static class RundgangTexte
{
    public const string Titel = "Rundgang";                                // TOUR_TITLE
    public const string Schritt = "Schritt %d von %d";                     // TOUR_STEP
    public const string Ueberspringen = "Überspringen";                    // TOUR_SKIP
    public const string Zurueck = "Zurück";                                // TOUR_BACK
    public const string Weiter = "Weiter";                                 // TOUR_NEXT
    public const string Fertig = "Speichern und loslegen";                 // TOUR_FINISH

    public const string Name = "Dein Name";                                // SETUP_NAME
    public const string NameZusatz = "freiwillig";                         // SETUP_NAME_HINT
    public const string Plan = "Rahmenplan";                               // SETUP_PLAN
    public const string PlanHinweis = "Die Abschnitte B, D und E gehören zu anderen Fachrichtungen. "
        + "Für Systemintegration kannst du sie ausgeschaltet lassen.";      // SETUP_PLAN_HINT
    public const string Termine = "Prüfungstermine";                       // SETUP_DATES

    public const string OptionenTitel = "Rundgang und Hilfe";              // OPTIONS_TITLE
    public const string OptionenUntertitel = "Einführung in 5 Schritten";  // OPTIONS_SUBTITLE
    public const string OptionenText = "Der Rundgang zeigt die wichtigsten Bereiche und hilft beim Einrichten "
        + "(Name, Rahmenplan, Prüfungstermine). Die Hilfe erklärt alles in kurzen Texten.";   // OPTIONS_TEXT
    public const string RundgangStarten = "Rundgang starten";              // BTN_TOUR
    public const string HilfeOeffnen = "Hilfe öffnen";                     // BTN_HELP

    public const string HilfeTitel = "Hilfe";                              // HELP_TITLE
    public const string HilfeUntertitel = "Kurz erklärt";                  // HELP_SUBTITLE

    /// <summary>Art der Hilfe-Treffer in der Suche (SEARCH_KIND).</summary>
    public const string SucheArt = "Hilfe";

    /// <summary>UPDATE_PROTECTION_HINT (Update-Bereich, Phase 6; hier nur fuer G35).</summary>
    public const string UpdateSchutzHinweis = "Meldet sich dein Schutzprogramm, ist das bei neuen Versionen normal. "
        + "Mehr in der Hilfe unter Update und Schutzprogramm.";

    // -- NEU: Ausbildungsfrage und Warnhinweis (Wortliste W4, N3-N8) --------------------------------

    public const string AusbildungTitel = "Deine Ausbildung";              // N3
    public const string AusbildungSpeichern = "Speichern";                 // N5
    public const string AusbildungUeberspringen = Ueberspringen;           // N5 (wie TOUR_SKIP)
    public const string WarnungTitel = "Rahmenplan anpassen?";             // N6
    public const string Ja = "Ja";                                         // N8
    public const string Nein = "Nein";                                     // N8

    /// <summary>TOUR_STEP mit Werten.</summary>
    public static string SchrittText(int seite, int anzahl) =>
        Schritt.Replace("%d von %d", seite.ToString(System.Globalization.CultureInfo.InvariantCulture) + " von "
            + anzahl.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
