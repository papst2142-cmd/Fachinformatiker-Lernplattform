using Lernplattform.Core.Design;

namespace Lernplattform.Core.Texte;

/// <summary>Zwischenueberschrift in einem zusammengelegten Bereich (SECTIONS, DIAGNOSE_SECTIONS).</summary>
public sealed record OptionenAbschnitt(string Id, string Titel, bool NurPc = false);

/// <summary>
/// Ein Bereich der Optionen (fisi_optionen.AREAS). Offen: kein Klappbereich (nur
/// "Updates"). Aufgeklappt: beim Oeffnen der Optionen aufgeklappt (unfolded_at_start).
/// Untertitel wie PC und Handy sie beim Aufbau setzen (null = keiner; "%s" = Version).
/// Stichwoerter fuer die Suche, StichwoerterPc nur am PC zusaetzlich.
/// </summary>
public sealed record OptionenBereich(
    string Id,
    string Titel,
    string? UntertitelPc,
    string? UntertitelHandy,
    bool NurPc,
    bool Offen,
    bool Aufgeklappt,
    IReadOnlyList<OptionenAbschnitt> Abschnitte,
    IReadOnlyList<string> Stichwoerter,
    IReadOnlyList<string> StichwoerterPc)
{
    public string? Untertitel(bool pc) => pc ? UntertitelPc : UntertitelHandy;

    /// <summary>Zwischenueberschriften am PC bzw. am Handy (ohne "nur PC").</summary>
    public IReadOnlyList<OptionenAbschnitt> AbschnitteFuer(bool pc) => [.. Abschnitte.Where(a => pc || !a.NurPc)];

    public IReadOnlyList<string> StichwoerterFuer(bool pc) => pc ? [.. Stichwoerter, .. StichwoerterPc] : Stichwoerter;
}

/// <summary>
/// Aufbau der Optionen wie in der Python-Linie (fisi_optionen; Vergleichswerte G9):
/// Reihenfolge, Titel, Untertitel, "nur PC", Zwischenueberschriften.
/// </summary>
public static class Optionen
{
    public const string Updates = "updates";
    public const string Rundgang = "rundgang";
    public const string Optik = "optik";
    public const string Tagesziel = "tagesziel";
    public const string Abgleich = "abgleich";
    public const string Sicherung = "sicherung";
    public const string Datenbank = "datenbank";
    public const string Lerninhalte = "lerninhalte";
    public const string Spiel = "spiel";
    public const string Loeschen = "loeschen";
    public const string Diagnose = "diagnose";
    public const string Ueber = "ueber";

    // Zwischenueberschriften
    public const string AbschnittSchrift = "schrift";
    public const string AbschnittFarben = "farben";
    public const string AbschnittRahmenplan = "rahmenplan";
    public const string AbschnittProblem = "problem";
    public const string AbschnittLeistung = "leistung";
    public const string AbschnittHaenger = "haenger";

    public const string OptikTitel = "Optische Anpassungen";
    public const string FarbenTitel = "Farben";
    public const string DiagnoseTitel = "Diagnose und Werkzeuge";
    public const string DiagnoseUntertitel = "Fehler melden, messen, Hänger finden";
    public const string HaengerTitel = "Hänger-Diagnose";
    public const string UeberTitel = "Über das Programm";
    public const string NurDiesesGeraet = "nur für dieses Gerät";

    /// <summary>Art der Treffer aus den Optionen in der Suche (SEARCH_KIND).</summary>
    public const string SucheArt = "Optionen";

    // Ab 0.62.5 (B-G-04): einstellungen.json liess sich nicht speichern (PC und Handy gleich)
    public const string SpeichernFehlgeschlagenTitel = "Speichern fehlgeschlagen";
    public const string SpeichernFehlgeschlagen = "Die Einstellung ließ sich nicht speichern. Ist der Datenordner "
        + "schreibgeschützt oder die Platte voll? Die bisherige Einstellung gilt weiter.";

    // Bereich "Updates" (PC und Handy gleich)
    public const string UpdatesUntertitel = "installierte Version %s";
    public const string UpdatesSuchen = "Nach Updates suchen";
    public const string UpdatesAutomatisch = "Beim Start automatisch nach Updates suchen";

    /// <summary>Alle Bereiche in der angezeigten Reihenfolge (am Handy ohne "nur PC", siehe <see cref="BereicheFuer"/>).</summary>
    public static readonly IReadOnlyList<OptionenBereich> Bereiche =
    [
        new(Updates, "Updates", UpdatesUntertitel, UpdatesUntertitel, false, true, false, [],
            ["Update", "Version", "aktualisieren", "Desktop-Verknüpfung"], []),
        new(Rundgang, "Rundgang und Hilfe", "Einführung in 5 Schritten", "Einführung in 5 Schritten", false, false, false, [],
            ["Rundgang", "Hilfe", "Name", "Einführung"], []),
        new(Optik, OptikTitel, SchriftTexte.Untertitel, SchriftTexte.Untertitel, false, false, false,
            [new(AbschnittSchrift, SchriftTexte.Titel), new(AbschnittFarben, FarbenTitel)],
            [
                "Schrift", "Schriftgröße", "groß", "Sehr groß", "Extra groß", "Maximal",
                "Farbe", "Darstellung", "Dunkel", "Hell", FarbenTexte.VorlagenTitel,
                "Grundfarbe", "Hintergrund", "Eigene Farben", "Regler",
                "Farbton", "Sättigung", "Helligkeit", "Akzent",
                .. Farbwelt.Grundfarben.Select(g => g.Name),
                .. Farbwelt.Hintergruende.Select(h => h.NameDunkel),
                .. Farbwelt.Hintergruende.Select(h => h.NameHell),
            ], []),
        new(Tagesziel, "Tagesziel", NurDiesesGeraet, NurDiesesGeraet, false, false, false, [],
            ["Ziel", "Lernserie", "Erinnerung", "Aufgaben pro Tag"], []),
        new(Abgleich, "Abgleich PC und Handy", "über ein privates GitHub-Repository", "privates GitHub-Repository",
            false, false, false, [],
            ["Abgleich", "Repository", "Token", "Zugangsschlüssel", "GitHub"], []),
        new(Sicherung, "Sicherung", "Lernstand als Datei", "Lernstand als Datei", false, false, false, [],
            ["Sicherung", "Backup", "wiederherstellen"], []),
        new(Datenbank, "Datenbank", null, null, true, false, false, [],
            ["Datenbank", "Speicherort", "FISI_DB_PATH"], []),
        new(Lerninhalte, "Lerninhalte", null, null, false, false, false,
            [new(AbschnittRahmenplan, "Rahmenplan")],
            ["Lerninhalte", "Anzahl", "Karteikarten gesamt", "Rahmenplan", "Lernfeld", "Prüfungstermin", "Gewichtung", "AP1", "AP2"],
            []),
        new(Spiel, "Spiel", null, null, false, false, false, [],
            ["Spiel", "Wohnungen", "Miete", "Schwierigkeitsgrad"], []),
        new(Loeschen, "Löschen und zurücksetzen", "Lerndaten, Historie, Spielstand, Bestenliste",
            "Lerndaten, Historie, Spielstand, Bestenliste", false, false, false, [],
            ["Löschen", "zurücksetzen", "Lerndaten", "Historie", "Spielstand", "Bestenliste"], []),
        new(Diagnose, DiagnoseTitel, DiagnoseUntertitel, DiagnoseUntertitel, false, false, false,
            [
                new(AbschnittProblem, DiagnoseTexte.Titel),
                new(AbschnittLeistung, "Leistungsmessung"),
                new(AbschnittHaenger, HaengerTitel, NurPc: true),
            ],
            [
                "Diagnose", "Werkzeuge", DiagnoseTexte.Titel, "Problem", "Fehler",
                "fehler.log", "Bericht", "melden", "Leistungsmessung", "Leistung",
                "Messung", "Messdatei",
            ],
            [HaengerTitel, "Hänger", "haenger.log", "hängt"]),
        new(Ueber, UeberTitel, null, null, false, false, true, [],
            ["Über", "Programm", "Version"], []),
    ];

    /// <summary>Bereiche am PC bzw. am Handy (ohne "nur PC"), wie fisi_optionen.areas(pc).</summary>
    public static IReadOnlyList<OptionenBereich> BereicheFuer(bool pc) => [.. Bereiche.Where(b => pc || !b.NurPc)];

    /// <summary>Bereich zur Kennung (KeyNotFoundException, wenn es ihn nicht gibt).</summary>
    public static OptionenBereich Bereich(string id) =>
        Bereiche.Append(AusbildungBereich).FirstOrDefault(b => b.Id == id) ?? throw new KeyNotFoundException(id);

    // -- Phase 4, W2: Bereich "Ausbildung" (nur C#, Wortliste W2 Abschnitt 7) -----------------

    public const string Ausbildung = "ausbildung";

    /// <summary>Neu in C# (Python hat ihn nicht); <see cref="Bereiche"/> bleibt wie G9.</summary>
    public static readonly OptionenBereich AusbildungBereich = new(Ausbildung, PruefungTexte.AusbildungReiter, null, null,
        false, false, false, [], PruefungTexte.AusbildungSuchwoerter, []);

    /// <summary>Angezeigte Bereiche: wie <see cref="BereicheFuer"/>, "Ausbildung" vor "Lerninhalte".</summary>
    public static IReadOnlyList<OptionenBereich> AlleFuer(bool pc)
    {
        var liste = BereicheFuer(pc).ToList();
        liste.Insert(liste.FindIndex(b => b.Id == Lerninhalte), AusbildungBereich);
        return liste;
    }

    // -- Phase 8 W2b (Sammelliste 116, Nico 26.10.2026): Gruppen, alle Karten zugeklappt ----------

    public const string GruppeLernen = "LERNEN";
    public const string GruppeAussehen = "AUSSEHEN";
    public const string GruppeSpiel = "SPIEL";
    public const string GruppeDaten = "DATEN";
    public const string GruppeProgramm = "PROGRAMM";

    /// <summary>Gruppen der Optionen-Seite mit den Bereichen in Anzeigereihenfolge ("Datenbank" nur am PC).</summary>
    public static readonly IReadOnlyList<(string Titel, IReadOnlyList<string> Ids)> Gruppen =
    [
        (GruppeLernen, [Ausbildung, Tagesziel, Lerninhalte]),
        (GruppeAussehen, [Optik]),
        (GruppeSpiel, [Spiel]),
        (GruppeDaten, [Abgleich, Sicherung, Datenbank, Loeschen]),
        (GruppeProgramm, [Updates, Rundgang, Diagnose, Ueber]),
    ];

    /// <summary>
    /// Weisung P8-5b (Sammelliste 121, Nico 26.10.2026 19:43): jede Gruppe eine Farbe (Schluessel der Farbwelt);
    /// Ueberschrift, Pfeil, Strich und Titel jeder Karte der Gruppe nehmen sie an. Ausnahme: "Loeschen und
    /// zuruecksetzen" bleibt rot als Warnung (<see cref="Farbe"/>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> GruppenFarben = new Dictionary<string, string>
    {
        [GruppeLernen] = "blue", [GruppeAussehen] = "cyan", [GruppeSpiel] = "pink", [GruppeDaten] = "purple", [GruppeProgramm] = "orange",
    };

    /// <summary>Farbe (Schluessel der Farbwelt) der Karte eines Bereichs: die seiner Gruppe, "Loeschen" rot.</summary>
    public static string Farbe(string id) =>
        id == Loeschen ? "red" : GruppenFarben[Gruppen.First(g => g.Ids.Contains(id)).Titel];

    /// <summary>Die angezeigten Bereiche (<see cref="AlleFuer"/>) nach <see cref="Gruppen"/> geordnet; fehlende ("nur PC") entfallen.</summary>
    public static IReadOnlyList<(string Titel, IReadOnlyList<OptionenBereich> Bereiche)> Gruppieren(IReadOnlyList<OptionenBereich> angezeigt) =>
        [.. Gruppen.Select(g => (g.Titel, (IReadOnlyList<OptionenBereich>)[.. g.Ids.Select(id => angezeigt.FirstOrDefault(b => b.Id == id)).OfType<OptionenBereich>()]))];

    // Kurzzeilen fuer Bereiche ohne Untertitel (jede Karte zeigt zugeklappt Titel und Kurzzeile)
    public const string LerninhalteKurz = "Anzahl der Inhalte und Rahmenplan";
    public const string DatenbankKurz = "Speicherort des Lernstands";
    public const string UeberKurz = "Version und Lizenzen";

    /// <summary>Phase 8 W5 (Sammelliste 117): Kurzzeile "Spiel", ersetzt den Platzhalter (Vorschlag CC, Chat liest).</summary>
    public const string SpielKurz = "Spielstände und Schwierigkeitsgrad wählst du im Spiel";

    /// <summary>
    /// Kurzzeile einer zugeklappten Karte: der Untertitel (<see cref="OptionenBereich.UntertitelPc"/>, "%s" = Version),
    /// sonst eine eigene Zeile; <see cref="Bereiche"/> bleibt wie G9.
    /// </summary>
    public static string Kurzzeile(OptionenBereich bereich) => bereich.UntertitelPc ?? bereich.Id switch
    {
        Ausbildung => PruefungTexte.AusbildungTitel,
        Lerninhalte => LerninhalteKurz,
        Datenbank => DatenbankKurz,
        Spiel => SpielKurz,
        Ueber => UeberKurz,
        _ => throw new KeyNotFoundException(bereich.Id),
    };
}
