namespace Lernplattform.Core.Inhalte;

/// <summary>
/// Fachbereiche, Pruefungsthemen und Fachrichtung, wie in der Python-Linie
/// (fisi_core CATEGORIES, CATEGORY_KEYS, AP1_THEMES, AP2_THEMES, THEME_BLOCK).
/// </summary>
public static class Fachbereiche
{
    public const string Netzwerk = "Netzwerk & Protokolle";
    public const string Sicherheit = "IT-Sicherheit & Datenschutz";
    public const string Systeme = "Systeme, RAID & Hardware";
    public const string Wirtschaft = "Wirtschaft & Prozesse";
    public const string Datenbanken = "Datenbanken & SQL";

    /// <summary>Die fuenf Fachbereiche in fester Reihenfolge.</summary>
    public static readonly IReadOnlyList<string> Alle = [Netzwerk, Sicherheit, Systeme, Wirtschaft, Datenbanken];

    /// <summary>Kurzname in den JSON-Dateien -> voller Name.</summary>
    public static readonly IReadOnlyDictionary<string, string> Schluessel = new Dictionary<string, string>
    {
        ["netzwerk"] = Netzwerk,
        ["sicherheit"] = Sicherheit,
        ["systeme"] = Systeme,
        ["wirtschaft"] = Wirtschaft,
        ["datenbanken"] = Datenbanken,
    };

    public static readonly IReadOnlyDictionary<string, string> Kurzname = new Dictionary<string, string>
    {
        [Netzwerk] = "Netzwerk",
        [Sicherheit] = "Sicherheit",
        [Systeme] = "Systeme",
        [Wirtschaft] = "Wirtschaft",
        [Datenbanken] = "Datenbanken",
    };

    /// <summary>Die fuenf Themenbloecke der AP2.</summary>
    public static readonly IReadOnlyList<string> Ap2Themen =
        ["Subnetting & Routing", "IT-Sicherheit", "Storage & RAID", "Netzwerkdesign", "Wirtschaft & Beratung"];

    /// <summary>Die fuenf Themenbloecke der AP1.</summary>
    public static readonly IReadOnlyList<string> Ap1Themen =
    [
        "Rechnernetze Grundlagen", "Datenschutz & Sicherheit", "Rechnertechnik & Zahlensysteme",
        "Projektplanung", "Wirtschafts- und Sozialkunde",
    ];

    /// <summary>Szenario-Themen ohne eigenen Block zaehlen zum fachlich passenden Block.</summary>
    public static readonly IReadOnlyDictionary<string, string> ThemenBlock = new Dictionary<string, string>
    {
        ["Virtualisierung"] = "Storage & RAID",
        ["Projektmanagement"] = "Wirtschaft & Beratung",
        ["Systemadministration"] = "Storage & RAID",
        ["Automatisierung & Skripte"] = "Storage & RAID",
        ["Arbeitsplatz einrichten: Bedarf, Beschaffung, Übergabe"] = "Projektplanung",
    };

    /// <summary>Themenblock, zu dem ein Szenario-Thema zaehlt.</summary>
    public static string? Block(string? thema) => thema is null ? null : ThemenBlock.GetValueOrDefault(thema, thema);
}

/// <summary>
/// Fachrichtung eines Lerninhalts (Gesamtplan Abschnitt 8, Phase 9 W2). Die Dateien in
/// "inhalte" bleiben unveraendert: ohne Feld "fachrichtung" gilt die Vorgabe Systemintegration.
/// Neue Inhalte in inhalte_csharp/lernen tragen das Feld als Pflicht (Kennungen wie
/// <see cref="Pruefung.Ausbildung.Werte"/>).
/// </summary>
public static class Fachrichtungen
{
    public const string Feld = "fachrichtung";
    public const string Systemintegration = "si";
    public const string Vorgabe = Systemintegration;

    /// <summary>Einteilung der bisherigen Inhalte: fuer alle Fachrichtungen.</summary>
    public const string Gemeinsam = "gemeinsam";

    /// <summary>Erlaubte Werte des Feldes "fachrichtung" in inhalte_csharp/lernen.</summary>
    public static readonly IReadOnlyList<string> Werte = ["si", "ae", "dpa", "dv"];
}
