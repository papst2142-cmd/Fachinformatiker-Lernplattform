using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lernplattform.Core.Inhalte;

/// <summary>Art eines Lerninhalts (Python: KARTEIKARTEN, QUIZ_QUESTIONS, SZENARIEN, AP1_SZENARIEN, PROJEKTARBEITEN).</summary>
public enum InhaltArt
{
    Karteikarte,
    Quizfrage,
    Ap2Szenario,
    Ap1Szenario,
    Testprojekt,
}

/// <summary>Ein Thema eines Fachbereichs (inhalte/themen.json).</summary>
public sealed record Thema(string Id, string Name, string Kurz, string Fachbereich);

/// <summary>
/// Ein Lerninhalt nach dem Laden wie in Python (load_content): Fachbereich als
/// voller Name, Zeilenlisten der Textfelder zu einem Text verbunden. Alle Felder
/// der JSON-Datei bleiben in <see cref="Daten"/> erhalten (auch spaeter
/// hinzugekommene), die haeufigen Felder gibt es als Eigenschaften.
/// </summary>
public sealed class Lerninhalt
{
    internal Lerninhalt(InhaltArt art, int nummer, JsonObject daten)
    {
        Art = art;
        Nummer = nummer;
        Daten = daten;
    }

    public InhaltArt Art { get; }

    /// <summary>Position in der Gesamtliste ab 1 (wie "Nr." in den Pruefmeldungen; der Lernstand speichert Index = Nummer - 1).</summary>
    public int Nummer { get; }

    /// <summary>Alle Felder des Eintrags nach dem Umwandeln.</summary>
    public JsonObject Daten { get; }

    public string? Fachbereich => Text("cat");

    public string? Thema => Text("thema");

    /// <summary>Fragetext (Karteikarte, Quizfrage).</summary>
    public string? Frage => Text("q");

    /// <summary>Titel (Szenario, Testprojekt).</summary>
    public string? Titel => Text("title");

    /// <summary>Richtige Antwort (Karteikarte, Quizfrage).</summary>
    public string? Antwort => Text("a");

    /// <summary>Szenario-Thema (Feld "theme").</summary>
    public string? SzenarioThema => Text("theme");

    public IReadOnlyList<string> Optionen => Liste("options");

    /// <summary>Fachrichtung; ohne Feld "fachrichtung" gilt Systemintegration.</summary>
    public string Fachrichtung => Text(Fachrichtungen.Feld) ?? Fachrichtungen.Vorgabe;

    /// <summary>
    /// Phase 9 W2: wer den Inhalt sieht, "gemeinsam" (alle Fachrichtungen) oder eine Fachrichtung
    /// (si, ae, dpa, dv). Bisherige Inhalte nach einteilung_bisher.json (ohne Eintrag si), neue
    /// Inhalte aus inhalte_csharp/lernen nach ihrem Feld "fachrichtung".
    /// </summary>
    public string Zugehoerigkeit { get; internal set; } = Fachrichtungen.Vorgabe;

    /// <summary>Sieht die Fachrichtung <paramref name="fachrichtung"/> diesen Inhalt?</summary>
    public bool SichtbarFuer(string fachrichtung) =>
        Zugehoerigkeit == Fachrichtungen.Gemeinsam || Zugehoerigkeit == fachrichtung;

    /// <summary>Textwert eines Feldes oder null, wenn es fehlt oder kein Text ist.</summary>
    public string? Text(string feld) =>
        Daten[feld] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    /// <summary>Texte einer Liste (fehlendes Feld = leere Liste).</summary>
    public IReadOnlyList<string> Liste(string feld) =>
        Daten[feld] is JsonArray a
            ? a.Select(x => x is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : x?.ToJsonString() ?? "")
                .ToList()
            : [];
}

/// <summary>
/// Datenmodell der Lerninhalte (Phase 1, K1): laedt die JSON-Dateien aus
/// "inhalte" genau wie die Python-Linie (fisi_core.load_content,
/// build_quiz_database, _load_topics, fisi_rahmenplan._load). Die Dateien
/// werden nur gelesen, nie veraendert.
/// </summary>
public sealed partial class Lerninhalte
{
    /// <summary>Felder, die in den JSON-Dateien auch als Liste von Zeilen stehen duerfen (TEXT_FIELDS).</summary>
    public static readonly IReadOnlyList<string> TextFelder = ["text", "solution", "ausgangssituation", "auftrag", "a_full", "exp"];

    private Lerninhalte()
    {
    }

    public string Ordner { get; private init; } = "";

    public IReadOnlyList<Lerninhalt> Karteikarten { get; private init; } = [];

    /// <summary>Feste Quizfragen plus erzeugte Aufgaben (build_quiz_database).</summary>
    public IReadOnlyList<Lerninhalt> Quizfragen { get; private init; } = [];

    /// <summary>Anzahl der festen Quizfragen aus quizfragen.json (dahinter folgen die erzeugten).</summary>
    public int AnzahlQuizfragenDatei { get; private init; }

    public IReadOnlyList<Lerninhalt> Ap2Szenarien { get; private init; } = [];

    public IReadOnlyList<Lerninhalt> Ap1Szenarien { get; private init; } = [];

    public IReadOnlyList<Lerninhalt> Testprojekte { get; private init; } = [];

    /// <summary>Fachbereich (voller Name) -> Themen in fester Reihenfolge (TOPICS).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Thema>> Themen { get; private init; } =
        new Dictionary<string, IReadOnlyList<Thema>>();

    /// <summary>Alle Themen-Kennungen in Fachbereich-Reihenfolge (TOPIC_ORDER).</summary>
    public IReadOnlyList<string> ThemenReihenfolge { get; private init; } = [];

    /// <summary>Themen-Kennung -> Thema (TOPIC_NAME, TOPIC_SHORT, TOPIC_CAT).</summary>
    public IReadOnlyDictionary<string, Thema> ThemaNach { get; private init; } = new Dictionary<string, Thema>();

    /// <summary>inhalte/rahmenplan.json (Ausbildungsrahmenplan, ab Python 0.55).</summary>
    public JsonObject Rahmenplan { get; private init; } = [];

    /// <summary>inhalte/rahmenplan_zuordnung.json.</summary>
    public JsonObject RahmenplanZuordnung { get; private init; } = [];

    /// <summary>Alle Inhalte laden. Ein unbekannter Fachbereich-Kurzname bricht ab (wie Python).</summary>
    public static Lerninhalte Laden(string ordner) =>
        Laden(name => File.ReadAllBytes(Path.Combine(ordner, name + ".json")), ordner);

    /// <summary>
    /// Wie <see cref="Laden(string)"/>, die Dateien liefert aber <paramref name="lesen"/>
    /// (Name ohne ".json" -> Inhalt). So kann die App die Inhalte aus dem Programm selbst
    /// lesen (PC und Handy gleich), ohne einen Ordner auf dem Geraet.
    /// </summary>
    public static Lerninhalte Laden(Func<string, byte[]> lesen, string ordner = "")
    {
        var quiz = LadeListe(lesen, "quizfragen");
        var anzahlDatei = quiz.Count;
        var themen = LadeThemen(lesen);
        return new Lerninhalte
        {
            Ordner = ordner,
            Karteikarten = Verpacke(InhaltArt.Karteikarte, LadeListe(lesen, "karteikarten")),
            Quizfragen = Verpacke(InhaltArt.Quizfrage, Fragenkatalog.Aufbauen(quiz)),
            AnzahlQuizfragenDatei = anzahlDatei,
            Ap2Szenarien = Verpacke(InhaltArt.Ap2Szenario, LadeListe(lesen, "szenarien_ap2")),
            Ap1Szenarien = Verpacke(InhaltArt.Ap1Szenario, LadeListe(lesen, "szenarien_ap1")),
            Testprojekte = Verpacke(InhaltArt.Testprojekt, LadeListe(lesen, "projektarbeiten")),
            Themen = themen,
            ThemenReihenfolge = Fachbereiche.Alle
                .SelectMany(f => themen.TryGetValue(f, out var liste) ? liste : [])
                .Select(t => t.Id)
                .ToList(),
            ThemaNach = ThemenNachId(themen),
            Rahmenplan = LadeObjekt(lesen, "rahmenplan"),
            RahmenplanZuordnung = LadeObjekt(lesen, "rahmenplan_zuordnung"),
        };
    }

    /// <summary>inhalte/&lt;name&gt;.json laden und umwandeln wie load_content.</summary>
    public static List<JsonObject> LadeListe(string ordner, string name) =>
        LadeListe(n => File.ReadAllBytes(Path.Combine(ordner, n + ".json")), name);

    private static List<JsonObject> LadeListe(Func<string, byte[]> lesen, string name)
    {
        var liste = JsonNode.Parse(lesen(name)) as JsonArray
            ?? throw new InvalidDataException(name + ".json enthaelt keine Liste");
        var ergebnis = new List<JsonObject>(liste.Count);
        foreach (var knoten in liste.ToList())
        {
            liste.Remove(knoten);
            ergebnis.Add(Umwandeln(knoten as JsonObject ?? throw new InvalidDataException(name + ".json: Eintrag ist kein Objekt"), name));
        }
        return ergebnis;
    }

    /// <summary>Ein Eintrag wie load_content: Fachbereich-Kurzname -> Name, Zeilenlisten -> Text.</summary>
    private static JsonObject Umwandeln(JsonObject eintrag, string name)
    {
        var kurz = eintrag["cat"] is JsonValue c && c.GetValueKind() == JsonValueKind.String ? c.GetValue<string>() : null;
        if (kurz is null || !Fachbereiche.Schluessel.TryGetValue(kurz, out var voll))
        {
            throw new InvalidDataException(name + ".json: unbekannter Fachbereich " + (kurz ?? eintrag["cat"]?.ToJsonString() ?? "None"));
        }
        eintrag["cat"] = voll;
        foreach (var feld in TextFelder)
        {
            if (eintrag[feld] is JsonArray zeilen)
            {
                eintrag[feld] = string.Join("\n", zeilen.Select(z => z!.GetValue<string>()));
            }
        }
        return eintrag;
    }

    /// <summary>
    /// Phase 10 W1 (Weisung P9-13, Nico 5): nur die Eintraege einer Liste aus inhalte_csharp/lernen, deren Feld
    /// "fachrichtung" in <paramref name="fachrichtungen"/> steht, mit ihrer Position in der ganzen Datei. Die
    /// uebrigen werden nur gelesen, nicht umgewandelt (das spart beim Start die Zeit der anderen Fachrichtungen).
    /// Ein Eintrag ohne gueltiges Feld wird uebersprungen (er waere fuer niemanden sichtbar, die Pruefung meldet ihn).
    /// </summary>
    private static List<(int Index, JsonObject Daten)> LadeListeFuer(JsonDocument dokument, string name, IReadOnlySet<string> fachrichtungen)
    {
        if (dokument.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(name + ".json enthaelt keine Liste");
        }
        var ergebnis = new List<(int, JsonObject)>();
        var index = 0;
        foreach (var element in dokument.RootElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(Fachrichtungen.Feld, out var wert)
                && wert.ValueKind == JsonValueKind.String && wert.GetString() is { } fr && fachrichtungen.Contains(fr))
            {
                ergebnis.Add((index, Umwandeln(JsonObject.Create(element)!, name)));
            }
            index++;
        }
        return ergebnis;
    }

    private static JsonObject LadeObjekt(Func<string, byte[]> lesen, string name) =>
        JsonNode.Parse(lesen(name)) as JsonObject
        ?? throw new InvalidDataException(name + ".json enthaelt kein Objekt");

    private static List<Lerninhalt> Verpacke(InhaltArt art, List<JsonObject> liste) =>
        liste.Select((daten, i) => new Lerninhalt(art, i + 1, daten)).ToList();

    private static Dictionary<string, IReadOnlyList<Thema>> LadeThemen(Func<string, byte[]> lesen)
    {
        var daten = LadeObjekt(lesen, "themen")["themen"] as JsonObject
            ?? throw new InvalidDataException("themen.json: Feld 'themen' fehlt");
        var ergebnis = new Dictionary<string, IReadOnlyList<Thema>>();
        foreach (var (kurz, eintraege) in daten)
        {
            if (!Fachbereiche.Schluessel.TryGetValue(kurz, out var voll))
            {
                throw new InvalidDataException("themen.json: unbekannter Fachbereich " + kurz);
            }
            ergebnis[voll] = eintraege!.AsArray()
                .Select(e => new Thema((string)e!["id"]!, (string)e["name"]!, (string)e["kurz"]!, voll))
                .ToList();
        }
        return ergebnis;
    }

    private static Dictionary<string, Thema> ThemenNachId(Dictionary<string, IReadOnlyList<Thema>> themen)
    {
        var ergebnis = new Dictionary<string, Thema>();
        foreach (var thema in themen.Values.SelectMany(t => t))
        {
            ergebnis[thema.Id] = thema;
        }
        return ergebnis;
    }

    // -- Kennzahlen (fisi_core content_totals, topic_totals, theme_totals, ap1_theme_totals)

    /// <summary>Anzahl Karten + Quizfragen je Fachbereich (content_totals).</summary>
    public OrderedDictionary<string, int> InhalteJeFachbereich()
    {
        var summen = new OrderedDictionary<string, int>();
        foreach (var fachbereich in Fachbereiche.Alle)
        {
            summen[fachbereich] = 0;
        }
        foreach (var eintrag in Karteikarten.Concat(Quizfragen))
        {
            var fachbereich = eintrag.Fachbereich!;
            summen[fachbereich] = summen.GetValueOrDefault(fachbereich) + 1;
        }
        return summen;
    }

    /// <summary>Anzahl Karten + Quizfragen je Thema (topic_totals).</summary>
    public OrderedDictionary<string, int> InhalteJeThema()
    {
        var summen = new OrderedDictionary<string, int>();
        foreach (var thema in ThemenReihenfolge)
        {
            summen[thema] = 0;
        }
        foreach (var eintrag in Karteikarten.Concat(Quizfragen))
        {
            if (eintrag.Thema is { } thema && summen.ContainsKey(thema))
            {
                summen[thema]++;
            }
        }
        return summen;
    }

    /// <summary>Anzahl AP2-Szenarien je Themenblock (theme_totals).</summary>
    public OrderedDictionary<string, int> Ap2SzenarienJeBlock() => JeBlock(Ap2Szenarien);

    /// <summary>Anzahl AP1-Szenarien je Themenblock (ap1_theme_totals).</summary>
    public OrderedDictionary<string, int> Ap1SzenarienJeBlock() => JeBlock(Ap1Szenarien);

    private static OrderedDictionary<string, int> JeBlock(IReadOnlyList<Lerninhalt> szenarien)
    {
        var summen = new OrderedDictionary<string, int>();
        foreach (var szenario in szenarien)
        {
            var block = Fachbereiche.Block(szenario.SzenarioThema
                ?? throw new InvalidDataException("Szenario Nr. " + szenario.Nummer + " ohne Thema"))!;
            summen[block] = summen.GetValueOrDefault(block) + 1;
        }
        return summen;
    }
}
