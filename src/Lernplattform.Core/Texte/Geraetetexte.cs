using Lernplattform.Core.Dashboard;

namespace Lernplattform.Core.Texte;

/// <summary>Eine erlaubte Ausnahme von "PC = Handy" (Wortliste Sammelliste 13, Weisung P2-14).</summary>
public sealed record GeraeteAusnahme(string Nr, string Stelle, string Grund);

/// <summary>
/// Phase 2, L11 (Sammelliste 13, Weisung P2-14 Frage 1 a): einzige Stelle, an der die
/// Oberflaeche einen Anzeigetext nach Geraet (pc = kein Android) oder Aufbau (pc = breiter
/// Aufbau mit Seitenleiste) waehlt. Die Daten bleiben wie Python (DashboardWerte, Optionen,
/// Vergleichswerte G9/G13); hier wird nur gewaehlt, was angezeigt wird. Unterschiede sind nur
/// die Eintraege von <see cref="Ausnahmen"/>; Abstaende und Umbrueche zaehlen nicht (B6, B7).
/// </summary>
public static class Geraetetexte
{
    /// <summary>Erlaubte Unterschiede (B2, B6, B7 brauchen keinen Eintrag, siehe Kommentar).</summary>
    // B2 (Suchfeld/Suchknopf) entfaellt in C#: Suchfeld und Seitentitel sind auf beiden Geraeten gleich.
    // B6/B7 und N5 sind nur Abstand, Umbruch und Umbruchschutz: der Vergleich vereinheitlicht sie.
    public static readonly IReadOnlyList<GeraeteAusnahme> Ausnahmen =
    [
        new("B1", "Navigation", "Menuepunkt 1 \"Dashboard\" (Seitenleiste) / \"Start\" (untere Leiste), Nico 20.10."),
        new("B3", "Tagesziel.Hinweis", "Erinnerung verhaelt sich verschieden (in C# noch nicht angezeigt, Beleg mit dem Bau der Erinnerung)"),
        new("B4", "Optionen.Bereiche", "\"Datenbank\" gibt es nur am PC"),
        new("B4", "Optionen.Abschnitte", "\"Hänger\" gibt es nur am PC (N3)"),
        new("B4", "Optionen.Suchwoerter", "Suchwoerter von \"Hänger\" nur am PC (N3)"),
        new("B5", "Navigation", "Seitenleiste mit 14 Punkten / untere Leiste mit 6 Punkten und Sammelseite \"Lernen\""),
        new("B5", "Kopfzeilen", "Sammelseite \"Lernen\" nur im schmalen Aufbau; Spiel-Seiten fehlen dort noch (N7)"),
        new("B8", "Dashboard.Ring", "gleiches Wort; nur bei kaputten Daten (ueber 100 %) zeigt der PC die gekappte Zahl wie Python"),
        new("B9", "Diagnose.Knoepfe", "\"Ordner öffnen\" gibt es nur am PC"),
        new("B10", "Uebernahme.Dateifilter", "Android kennt den Dateityp .fisisicherung nicht"),
        new("B11", "Abgleich.Geraet", "\"PC <Rechnername>\" / \"Handy\" (Sammelliste 24)"),
    ];

    // -- Aufbau (pc = breiter Aufbau) -----------------------------------------

    /// <summary>Menuepunkte (B1, B5).</summary>
    public static IReadOnlyList<NavEintrag> Navigation(bool pc) => pc ? Texte.Navigation.Pc : Texte.Navigation.Handy;

    /// <summary>Kopfzeilen (B5).</summary>
    public static IReadOnlyList<Kopftitel> Kopfzeilen(bool pc) => pc ? Texte.Navigation.KopfPc : Texte.Navigation.KopfHandy;

    // -- Geraet (pc = kein Android) -------------------------------------------

    /// <summary>Text im Banner (B6: nur Abstand und Umbruchschutz).</summary>
    public static string Banner(DashboardBanner banner, bool pc) => pc ? banner.TextPc : banner.TextHandy;

    /// <summary>Erste Zeile einer Aktivitaet: beide ungekuerzt (T2, Nico 21.10.).</summary>
    public static string AktivitaetZeile1(DashboardAktivitaet aktivitaet, bool pc) => aktivitaet.Handy1;

    /// <summary>Datumszeile einer Aktivitaet: beide deutsch "21.10.2026 05:57" (T1, Nico 21.10.).</summary>
    public static string AktivitaetZeile2(DashboardAktivitaet aktivitaet, bool pc) => aktivitaet.Handy2;

    /// <summary>Leertext der Aktivitaeten (B7: nur Umbruch).</summary>
    public static string AktivitaetenLeer(bool pc) => pc ? DashboardTexte.AktivitaetenLeerPc : DashboardTexte.AktivitaetenLeerHandy;

    /// <summary>Text im Ring eines Fachbereichs (B8).</summary>
    public static string Ring(DashboardFachbereich fachbereich, bool pc) => pc ? fachbereich.RingText : fachbereich.RingTextHandy;

    /// <summary>Bereiche der Optionen (B4).</summary>
    public static IReadOnlyList<OptionenBereich> OptionenBereiche(bool pc) => Optionen.BereicheFuer(pc);

    /// <summary>Untertitel eines Bereichs: beide mit dem PC-Wortlaut (T3, Nico 21.10.).</summary>
    public static string? OptionenUntertitel(OptionenBereich bereich, bool pc) => bereich.UntertitelPc;

    /// <summary>Zwischenueberschriften eines Bereichs (B4).</summary>
    public static IReadOnlyList<OptionenAbschnitt> OptionenAbschnitte(OptionenBereich bereich, bool pc) => bereich.AbschnitteFuer(pc);

    /// <summary>Suchwoerter eines Bereichs in der Suche (B4).</summary>
    public static IReadOnlyList<string> OptionenSuchwoerter(OptionenBereich bereich, bool pc) => bereich.StichwoerterFuer(pc);

    /// <summary>Hinweis zum Tagesziel (B3).</summary>
    public static string TageszielHinweis(bool pc) => TageszielTexte.Hinweis(pc);

    /// <summary>Knoepfe der Diagnose (B9).</summary>
    public static IReadOnlyList<string> DiagnoseKnoepfe(bool pc) => DiagnoseTexte.Knoepfe(pc);

    /// <summary>Dateifilter beim Uebernehmen (Name, Muster; B10). Am Handy keiner.</summary>
    public static IReadOnlyList<(string Name, string Muster)> UebernahmeDateifilter(bool pc) => pc
        ? [(UebernahmeTexte.FilterSicherung, UebernahmeTexte.FilterSicherungMuster), (UebernahmeTexte.FilterAlle, "*")]
        : [];

    /// <summary>Dateiname der Kopie im Ergebnis (B6: am Handy Umbruchpunkt nach "_", P2-6 A3).</summary>
    public static string KopieName(string dateiname, bool pc) => UebernahmeTexte.KopieName(dateiname, !pc);

    /// <summary>Geraet in Commit-Nachricht und Sicherung (B11).</summary>
    public static string Geraet(bool pc, string rechnername) => pc ? ("PC " + rechnername.Trim()).Trim() : "Handy";
}
