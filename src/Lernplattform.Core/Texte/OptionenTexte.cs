using Lernplattform.Core.Design;

namespace Lernplattform.Core.Texte;

/// <summary>Darstellung Dunkel/Hell (fisi_theme.MODES). Am PC steht die Beschriftung in Grossbuchstaben.</summary>
public static class DarstellungTexte
{
    public static readonly IReadOnlyList<(string Id, string Name)> Modi = [(Farbwelt.Dunkel, "Dunkel"), (Farbwelt.Hell, "Hell")];

    /// <summary>Beschriftung ueber der Auswahl (PC "DARSTELLUNG" = Gross, Handy ui.label).</summary>
    public const string Beschriftung = "Darstellung";
}

/// <summary>
/// Texte zu den Farben (fisi_optionen TEMPLATES_*, fisi_theme BUSY_*). Die Namen
/// der Grundfarben und Hintergruende stehen in <see cref="Farbwelt"/> (Grundfarbe.Name,
/// Hintergrund.NameDunkel/NameHell; in der hellen Darstellung gilt NameHell).
/// </summary>
public static class FarbenTexte
{
    public const string Grundfarbe = "Grundfarbe";
    public const string Hintergrund = "Hintergrund";

    public const string VorlagenId = "vorlagen";
    public const string VorlagenTitel = "Vorlagen";
    public const string VorlagenHinweis = "Ein Klick auf eine Vorlage stellt die Farben ein. Sind eigene Farben "
        + "gespeichert oder Regler bewegt, setzt er nur die Regler.";
    public const string VorlagenText = "Die Grundfarbe ändert Buttons, Ringe, Balken und Banner, der "
        + "Hintergrund die Flächen und Karten. Fachbereichsfarben und Erfolg, "
        + "Fehler, Warnung bleiben gleich (in der hellen Darstellung etwas "
        + "dunkler, damit sie gut lesbar sind).";

    public const string BeschaeftigtTitel = "Farben werden angewendet";
    public const string BeschaeftigtText = "Bitte warten, die Oberfläche wird mit den neuen Farben aufgebaut …";
    /// <summary>Kurze Ladeanzeige beim Oeffnen einer Ansicht (LOADING_TEXT).</summary>
    public const string Laden = "Wird geladen …";

    /// <summary>Anzeigename eines Hintergrunds in der Darstellung (im Hellen z. B. "Weiß" statt "Schwarz").</summary>
    public static string HintergrundName(Farbwelt.Hintergrund hintergrund, string darstellung) =>
        darstellung == Farbwelt.Hell ? hintergrund.NameHell : hintergrund.NameDunkel;
}

/// <summary>Eine Schriftstufe (fisi_theme.FONT_SIZES): Kennung, Name, Vergroesserung (1.0 = Normal).</summary>
public sealed record Schriftstufe(string Id, string Name, double Faktor);

/// <summary>Schriftgroesse (fisi_theme FONT_*, PC und Handy gleich). Ids = Einstellungen.Schriftgroessen.</summary>
public static class SchriftTexte
{
    public static readonly IReadOnlyList<Schriftstufe> Stufen =
    [
        new("normal", "Normal", 1.0),
        new("gross", "Groß", 1.15),
        new("sehr_gross", "Sehr groß", 1.3),
        new("extra_gross", "Extra groß", 1.6),
        new("riesig", "Maximal", 2.0),
    ];

    public const string Titel = "Schriftgröße";
    public const string Untertitel = "nur für dieses Gerät";
    public const string Hinweis = "Gilt sofort und nur für dieses Gerät. Am PC wächst die ganze Oberfläche mit "
        + "(Schrift, Knöpfe, Abstände); passt sie nicht mehr ins Fenster, lässt sie sich "
        + "seitlich verschieben. Am Handy kommt die Schriftgröße aus den "
        + "Systemeinstellungen noch hinzu.";
    /// <summary>
    /// Angezeigter Hinweis in C# (Weisung P1-3, Frage 2): nur der erste Satz von
    /// <see cref="Hinweis"/>; der Rest (seitlich verschieben, Systemschrift) stimmt fuer C# nicht.
    /// </summary>
    public const string HinweisKurz = "Gilt sofort und nur für dieses Gerät.";
    public const string BeschaeftigtTitel = "Schriftgröße wird angewendet";
    public const string BeschaeftigtText = "Bitte warten, die Oberfläche wird in der neuen Größe aufgebaut …";

    /// <summary>Stufe zur Kennung, unbekannt -> Normal (wie font_size).</summary>
    public static Schriftstufe Stufe(string? id) => Stufen.FirstOrDefault(s => s.Id == id) ?? Stufen[0];
}

/// <summary>
/// Tagesziel, Lernserie, Erinnerung (Texte wie PC und Handy sie zeigen). Grenzwerte
/// und Vorgaben stehen in Daten.Einstellungen (ZielVorgabe, ZielMin, ZielMax,
/// ErinnerungVorgabe); hier nur die Schrittweite des Zahlenfelds.
/// </summary>
public static class TageszielTexte
{
    public const int Schritt = 5;

    /// <summary>Schalter (Einstellungs-Schluessel, Beschriftung), PC und Handy gleich.</summary>
    public static readonly IReadOnlyList<(string Schluessel, string Text)> Schalter =
    [
        ("ziel_an", "Tagesziel anzeigen"),
        ("serie_an", "Lernserie anzeigen"),
        ("erinnerung_an", "An das Tagesziel erinnern"),
    ];

    public const string AufgabenProTag = "Aufgaben pro Tag";
    public const string ErinnerungUm = "Erinnerung um";
    /// <summary>Platzhalter im Feld fuer die Uhrzeit (am Handy).</summary>
    public const string ErinnerungPlatzhalter = "18:00";

    private const string HinweisAnfang = "Gezählt werden bewertete Karteikarten, Prüfungstrainer-Fragen, "
        + "Szenarien, Testprojekte und Trainer-Aufgaben. Die Erinnerung ";
    /// <summary>Hinweis am PC (Erinnerung, solange das Programm offen ist).</summary>
    public const string HinweisPc = HinweisAnfang + "erscheint als Hinweis, solange das Programm geöffnet ist.";
    /// <summary>Hinweis am Handy (Erinnerung beim Oeffnen oder Zurueckholen der App).</summary>
    public const string HinweisHandy = HinweisAnfang + "erscheint als Hinweis, wenn du die App öffnest oder zurückholst.";

    public static string Hinweis(bool pc) => pc ? HinweisPc : HinweisHandy;

    /// <summary>
    /// Angezeigter Hinweis in C# (Weisung P1-3, Frage 3): nur der erste Satz, ohne die
    /// Zusage zur Erinnerung (gibt es noch nicht). Gezaehlt wird wie DashboardWerte
    /// (activity_days): Karteikarten, Quiz, AP1/AP2-Szenarien, Testprojekte, Trainer.
    /// </summary>
    public const string HinweisGezaehlt = "Gezählt werden bewertete Karteikarten, Prüfungstrainer-Fragen, "
        + "Szenarien, Testprojekte und Trainer-Aufgaben.";
}

/// <summary>
/// "Problem melden" im Bereich "Diagnose und Werkzeuge" (fisi_diagnose, PC und Handy
/// gleich; "Datenordner öffnen" nur am PC). Fehlerprotokoll-Texte (leer, gekuerzt)
/// stehen in Diagnose.Fehlerprotokoll.
/// </summary>
public static class DiagnoseTexte
{
    public const string Titel = "Problem melden";
    public const string Untertitel = "Angaben für die Fehlersuche";
    public const string Hilfe = "Zeigt Programmversion, Gerät, Datenordner und das Fehlerprotokoll. "
        + "Zugangsschlüssel, Passwörter und dein Benutzername in Pfaden werden "
        + "entfernt. Andere Angaben aus Fehlermeldungen, zum Beispiel der Name "
        + "deines Rechners, können darin stehen. Sieh dir den Bericht vor dem "
        + "Weitergeben an. Speichere ihn als Datei (oder kopiere ihn) und gib ihn "
        + "mit deiner Beschreibung des Problems an die Person weiter, von der du "
        + "das Programm hast.";
    public const string Kopieren = "Kopieren";
    public const string Speichern = "Als Datei speichern";
    /// <summary>Nur am PC.</summary>
    public const string OrdnerOeffnen = "Datenordner öffnen";
    public const string Kopiert = "Bericht in die Zwischenablage kopiert";
    /// <summary>%s = Fehlermeldung.</summary>
    public const string OrdnerFehler = "Der Datenordner ließ sich nicht öffnen: %s";
    public const string Dateityp = "Textdatei";

    /// <summary>Knoepfe in der angezeigten Reihenfolge.</summary>
    public static IReadOnlyList<string> Knoepfe(bool pc) => pc ? [Kopieren, Speichern, OrdnerOeffnen] : [Kopieren, Speichern];
}

/// <summary>"Über das Programm" (fisi_rechtliches.about_text, PC und Handy gleich bis auf den Plattform-Absatz).</summary>
public static class UeberTexte
{
    public const string Programmname = "Fachinformatiker Lernplattform";
    public const string Copyright = "Copyright (c) 2026 Nico H – Alle Rechte vorbehalten";
    public const string Beschreibung = "Lernprogramm für die Prüfungsvorbereitung zum Fachinformatiker "
        + "(Schwerpunkt Systemintegration) mit Karteikarten, Prüfungstrainer, "
        + "AP1-/AP2-Szenarien, Testprojekten und Praxis-Rechnern.";
    public const string Hinweis = "Die Fachinformatiker Lernplattform ist ein privates Lernprogramm. Sie ist "
        + "kein Angebot und kein Produkt einer Industrie- und Handelskammer (IHK) oder "
        + "einer anderen Prüfungsstelle und wird von diesen weder herausgegeben noch "
        + "geprüft. Die Aufgaben, Szenarien und Testprojekte sind eigene "
        + "Übungsaufgaben. Sie enthalten keine offiziellen Prüfungsaufgaben. "
        + "Bezeichnungen wie „AP1“, „AP2“ oder „Note nach IHK-Schlüssel“ beschreiben nur, "
        + "wofür geübt wird und nach welchem Notenschlüssel die Übung ausgewertet wird.";
    public const string ClaudeHinweis = "Erstellt mit Hilfe von Claude (Anthropic).";

    /// <summary>Plattform-Absatz der Python-Linie am PC (fuer C# noch nicht festgelegt).</summary>
    public const string PlattformPython = "Umgesetzt mit Python und CustomTkinter. Die Installer für "
        + "Windows, Linux und macOS bringen alles Nötige mit – es muss "
        + "nichts zusätzlich installiert werden. Die Handy-App für "
        + "Android nutzt dieselben Lerninhalte.";
    /// <summary>Plattform-Absatz der Python-Linie am Handy (fuer C# noch nicht festgelegt).</summary>
    public const string PlattformPythonHandy = "Die Handy-App nutzt dieselben Lerninhalte wie die "
        + "PC-Version und ist mit Python und Flet umgesetzt.";

    public const string KnopfLizenz = "Lizenz";
    public const string KnopfFremd = "Hinweise zu Fremdbestandteilen";
    public const string TitelLizenz = "Lizenz";
    public const string TitelFremd = "Hinweise zu Fremdbestandteilen";
    /// <summary>Phase 8 W2 (Weisung P8-3, Nico 5a): Knopf, der die vollen Lizenztexte zeigt (nur C#-Linie).</summary>
    public const string KnopfLizenzen = "Lizenzen";

    /// <summary>Ganzer Text der Seite (wie about_text); version = Anzeigeversion, z. B. "A 0.1.0".</summary>
    public static string Text(string version, string plattform) =>
        Programmname + " Version " + version + "\n" + Copyright + "\n\n" + Beschreibung + "\n\n"
        + plattform + "\n\n" + Hinweis + "\n\n" + ClaudeHinweis;
}
