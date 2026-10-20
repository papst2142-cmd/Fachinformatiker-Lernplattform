namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 2, L8: Texte der Suche zeichengleich aus Python v0.63.2 (PC-Wortlaut,
/// app_gui.SearchView und TopBar, fisi_optionen.search_options). Vergleichswerte G19.
/// </summary>
public static class SucheTexte
{
    public const string Titel = "Suchergebnisse";                                         // app_gui 8262
    public const string Info = "%d Treffer für „%s“";                                     // 8303
    public const string Keine = "Keine Treffer. Versuche einen anderen Suchbegriff.";     // 8306
    public const string Weitere = "… weitere %d Treffer nicht angezeigt.";                // 8339
    public const string Feld = "Suchen …  (Strg+F)";                                      // 1031 (Suchfeld der Kopfleiste)
    public const string Kopf = "%s · %s";                                                 // 8360
    public const string Auslassung = "…";                                                 // 8369
    public const string OptionenDetail = "Bereich in den Optionen: ";                     // fisi_optionen 214

    // Art der Treffer (fisi_core.search_content, KIND_QUIZ 2271)
    public const string ArtKarte = "Karteikarte";
    public const string ArtQuiz = "Prüfungsfrage";
    public const string ArtAp2 = "AP2-Szenario";
    public const string ArtAp1 = "AP1-Szenario";
}
