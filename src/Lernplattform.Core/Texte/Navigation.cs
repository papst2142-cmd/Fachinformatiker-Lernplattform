using Lernplattform.Core.Inhalte;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Ein Punkt der Navigation. Symbol: Name des Material-Symbols (PC NAV_SYMBOLS bzw.
/// CATEGORY_NAV_SYMBOL, Handy der Name aus ft.Icons, z. B. "DASHBOARD_OUTLINED").
/// SymbolAktiv: Handy-Symbol des gewaehlten Reiters. Zeichen: eigenes Zeichen der
/// PC-Seitenleiste (NAV_ITEMS bzw. CATEGORY_NAV_ICON).
/// </summary>
public sealed record NavEintrag(string Id, string Symbol, string Titel, IReadOnlyList<NavEintrag> Unterpunkte,
    string? Zeichen = null, string? SymbolAktiv = null);

/// <summary>Brotkrumen der Kopfleiste einer Seite (VIEW_TITLES bzw. crumbs).</summary>
public sealed record Kopftitel(string Id, string Bereich, string Seite);

/// <summary>Eintrag der Handy-Sammelseite "Lernen". Farben: Verlauf-Name oder zwei Hexwerte.</summary>
public sealed record LernenEintrag(string Id, string Symbol, string Titel, string Muster, IReadOnlyList<string> Farben);

/// <summary>
/// Navigation, Kopfleiste und Sammelseite "Lernen" wie in der Python-Linie
/// (app_gui NAV_ITEMS/NAV_SYMBOLS/VIEW_TITLES, mobile/src/main NAV/LearnScreen;
/// Vergleichswerte G9).
/// </summary>
public static class Navigation
{
    /// <summary>Seitenleiste am PC (Reihenfolge wie NAV_ITEMS). Karteikarten: ein Unterpunkt je Fachbereich (Id = voller Name).</summary>
    public static readonly IReadOnlyList<NavEintrag> Pc =
    [
        new("dashboard", "dashboard", "Dashboard", [], "grid"),
        new("cards", "style", "Karteikarten",
        [
            new(Fachbereiche.Netzwerk, "lan", Fachbereiche.Kurzname[Fachbereiche.Netzwerk], [], "node"),
            new(Fachbereiche.Sicherheit, "security", Fachbereiche.Kurzname[Fachbereiche.Sicherheit], [], "shield"),
            new(Fachbereiche.Systeme, "dns", Fachbereiche.Kurzname[Fachbereiche.Systeme], [], "server"),
            new(Fachbereiche.Wirtschaft, "euro", Fachbereiche.Kurzname[Fachbereiche.Wirtschaft], [], "case"),
            new(Fachbereiche.Datenbanken, "storage", Fachbereiche.Kurzname[Fachbereiche.Datenbanken], [], "database"),
        ], "cards"),
        new("quiz", "track_changes", "Prüfungstrainer", [], "target"),
        new("ap1scenarios", "layers", "AP1-Szenarien", [], "layers"),
        new("scenarios", "diamond", "AP2-Szenarien", [], "diamond"),
        new("testproject", "flag", "Testprojekt", [], "flag"),
        new("abschluss", "workspace_premium", "Abschlussprojekt", [], "case"),
        new("notebook", "edit_note", "Notizblock", [], "notebook"),
        new("calc", "calculate", "Rechner", [], "calc"),
        new("game", "sports_esports", "Spiel",
        [
            new("buero", "business", "Büro", [], "office"),
            new("kunde", "storefront", "Kunde", [], "station"),
            new("zuhause", "home", "Zuhause", [], "home"),
            new("firma", "work", "Firma", [], "case"),
            new("reise", "route", "Reise", [], "journey"),
        ], "game"),
        new("progress", "insights", "Fortschritt", [], "chart"),
        new("help", "help_outline", Hilfe, [], "help"),
        new("settings", "settings", "Optionen", [], "gear"),
    ];

    /// <summary>Untere Leiste am Handy (NAV).</summary>
    public static readonly IReadOnlyList<NavEintrag> Handy =
    [
        new("dashboard", "DASHBOARD_OUTLINED", "Start", [], SymbolAktiv: "DASHBOARD_ROUNDED"),
        new("learn", "SCHOOL_OUTLINED", "Lernen", [], SymbolAktiv: "SCHOOL_ROUNDED"),
        new("calc", "CALCULATE_OUTLINED", "Rechner", [], SymbolAktiv: "CALCULATE_ROUNDED"),
        new("game", "SPORTS_ESPORTS_OUTLINED", "Spiel", [], SymbolAktiv: "SPORTS_ESPORTS_ROUNDED"),
        new("progress", "INSIGHTS_OUTLINED", "Fortschritt", [], SymbolAktiv: "INSIGHTS_ROUNDED"),
        new("settings", "SETTINGS_OUTLINED", "Optionen", [], SymbolAktiv: "SETTINGS_ROUNDED"),
    ];

    /// <summary>Sammelseite "Lernen" am Handy (LearnScreen.ENTRIES); Untertitel ueber <see cref="LernenUntertitel"/>.</summary>
    public static readonly IReadOnlyList<LernenEintrag> Lernen =
    [
        new("cards", "STYLE_ROUNDED", "Karteikarten", "%s Karten in %s Fachbereichen", ["accent"]),
        new("quiz", "TRACK_CHANGES_ROUNDED", "Prüfungstrainer", "%s Aufgaben, mit %s", ["primary"]),
        new("ap1scenarios", "LAYERS_ROUNDED", "AP1-Szenarien", "%d Aufgaben der Grundlagenprüfung", ["#2563EB", "#22D3EE"]),
        new("scenarios", "DIAMOND_ROUNDED", "AP2-Szenarien", "%d Aufgaben der Abschlussprüfung", ["#DB2777", "#FB923C"]),
        new("testproject", "FLAG_ROUNDED", "Testprojekt", "%d Kundenaufträge zum Üben", ["success"]),
        new("abschluss", "WORKSPACE_PREMIUM_ROUNDED", "Abschlussprojekt", "Dein IHK-Projekt vom Antrag bis zum Fachgespräch",
            ["#7C3AED", "#22D3EE"]),
        new("notebook", "EDIT_NOTE_ROUNDED", "Notizblock", "Was du noch üben musst, aus allen Bereichen", ["#DC2626", "#FBBF24"]),
    ];

    public static readonly Kopftitel LernenKopf = new("learn", "LERNEN", "ÜBERSICHT");

    /// <summary>fisi_rechtliches.NOTE_KURZ (Untertitel Pruefungstrainer).</summary>
    public const string NoteKurz = "Note (Orientierung)";

    /// <summary>Untertitel eines Lernen-Eintrags mit den Anzahlen der Lerninhalte (wie LearnScreen.ENTRIES).</summary>
    public static string LernenUntertitel(LernenEintrag eintrag, Lerninhalte inhalte) => eintrag.Id switch
    {
        "cards" => Beschriftungen.Einsetzen(eintrag.Muster, Beschriftungen.MitPunkten(inhalte.Karteikarten.Count),
            Beschriftungen.Zahlwort(Fachbereiche.Alle.Count)),
        "quiz" => Beschriftungen.Einsetzen(eintrag.Muster, Beschriftungen.MitPunkten(inhalte.Quizfragen.Count), NoteKurz),
        "ap1scenarios" => Beschriftungen.Einsetzen(eintrag.Muster, inhalte.Ap1Szenarien.Count),
        "scenarios" => Beschriftungen.Einsetzen(eintrag.Muster, inhalte.Ap2Szenarien.Count),
        "testproject" => Beschriftungen.Einsetzen(eintrag.Muster, inhalte.Testprojekte.Count),
        _ => eintrag.Muster,
    };

    // Kopfleiste
    public const string Hilfe = "Hilfe";
    /// <summary>Platzhalter im Suchfeld am PC.</summary>
    public const string SuchePc = "Suchen …  (Strg+F)";
    /// <summary>Knopf Suche in der Kopfleiste am Handy (Tooltip und Name fuer Bildschirmleser).</summary>
    public const string SucheHandy = "Suchen";

    /// <summary>Knoepfe rechts in der Kopfleiste am Handy: Hilfe, Suche.</summary>
    public static readonly IReadOnlyList<NavEintrag> KopfKnoepfeHandy =
    [
        new("help", "HELP_OUTLINE_ROUNDED", Hilfe, []),
        new("search", "SEARCH_ROUNDED", SucheHandy, []),
    ];

    // Seitenleiste am PC
    public const string Logo = "FI";
    public const string LogoZusatz = "Lernplattform";
    /// <summary>Fusszeile der Seitenleiste; %s = Anzeigeversion.</summary>
    public const string Fusszeile = "Version %s";
    public const string Menue = "MENÜ";
    public const string Lernstatus = "LERNSTATUS";

    /// <summary>Brotkrumen am PC (VIEW_TITLES, Reihenfolge wie dort).</summary>
    public static readonly IReadOnlyList<Kopftitel> KopfPc =
    [
        new("dashboard", "DASHBOARD", "HOME"),
        new("cards", "LERNEN", "KARTEIKARTEN"),
        new("quiz", "LERNEN", "PRÜFUNGSTRAINER"),
        new("ap1scenarios", "LERNEN", "AP1-SZENARIEN"),
        new("scenarios", "LERNEN", "AP2-SZENARIEN"),
        new("testproject", "LERNEN", "TESTPROJEKT"),
        new("abschluss", "LERNEN", "ABSCHLUSSPROJEKT"),
        new("notebook", "LERNEN", "NOTIZBLOCK"),
        new("calc", "WERKZEUGE", "RECHNER"),
        new("game", "PRAXIS", "SPIEL"),
        new("buero", "SPIEL", "BÜRO"),
        new("kunde", "SPIEL", "KUNDE"),
        new("zuhause", "SPIEL", "ZUHAUSE"),
        new("firma", "SPIEL", "FIRMA"),
        new("filiale", "SPIEL", "FILIALE"),
        new("serverfarm", "SPIEL", "SERVERFARM"),
        new("reise", "SPIEL", "REISE"),
        new("progress", "AUSWERTUNG", "FORTSCHRITT"),
        new("settings", "SYSTEM", "OPTIONEN"),
        new("help", "SYSTEM", "HILFE"),
        new("search", "SUCHE", "ERGEBNISSE"),
    ];

    /// <summary>Brotkrumen am Handy (crumbs der Seiten in der Reihenfolge von SCREEN_CLASSES; ohne Spiel, das ein eigenes Modul hat).</summary>
    public static readonly IReadOnlyList<Kopftitel> KopfHandy =
    [
        new("dashboard", "DASHBOARD", "HOME"),
        LernenKopf,
        new("cards", "LERNEN", "KARTEIKARTEN"),
        new("quiz", "LERNEN", "PRÜFUNGSTRAINER"),
        new("ap1scenarios", "LERNEN", "AP1-SZENARIEN"),
        new("scenarios", "LERNEN", "AP2-SZENARIEN"),
        new("testproject", "LERNEN", "TESTPROJEKT"),
        new("abschluss", "LERNEN", "ABSCHLUSSPROJEKT"),
        new("notebook", "LERNEN", "NOTIZBLOCK"),
        new("calc", "WERKZEUGE", "RECHNER"),
        new("progress", "AUSWERTUNG", "FORTSCHRITT"),
        new("settings", "SYSTEM", "OPTIONEN"),
        new("search", "SUCHE", "ERGEBNISSE"),
        new("help", "SYSTEM", "HILFE"),
    ];
}
