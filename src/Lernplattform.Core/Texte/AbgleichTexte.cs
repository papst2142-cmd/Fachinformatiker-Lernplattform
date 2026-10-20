namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 2, L10: die von Chat freigegebenen Abgleich-Texte T5 bis T7 (Fi-Austausch
/// anlagen/P2-11_Wortlaut_W4.md). Alle übrigen Abgleich-Texte (Python-Wortlaut) folgen erst
/// nach Chat-Prüfung (Weisung P2-11 Punkt 4) und fehlen hier bewusst.
/// </summary>
public static class AbgleichTexte
{
    /// <summary>T5: Sicherheitskopie vor dem Abgleich gescheitert (AbgleichFehler.SicherungGescheitert).</summary>
    public const string SicherungGescheitert = "Der Abgleich wurde nicht durchgeführt, weil vorher keine Sicherheitskopie erstellt werden konnte. Dein Lernstand ist unverändert. Prüfe den freien Speicherplatz und versuche es später noch einmal.";

    /// <summary>T6: letzter Satz des Abgleich-Hinweises (ersetzt den Python-Satz zur Hilfe).</summary>
    public const string Zugangsschluessel = "Der Zugangsschlüssel wird nur in der Einstellungsdatei dieses Geräts gespeichert, ohne Verschlüsselung, und nicht in Sicherungen oder in die Fehlerdatei übernommen.";

    /// <summary>T7: Beispiel im Repository-Hinweis.</summary>
    public const string RepositoryBeispiel = "Das Repository bitte als „Benutzer/Name“ angeben, z. B. deinname/Fi-Lernstand.";
}
