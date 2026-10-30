using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lernplattform.Core.Inhalte;

/// <summary>
/// Phase 9 W2 (Plan Phase 9 Abschnitte 4, 5 und 12): Lerninhalte je Fachrichtung. Zu den Dateien
/// aus "inhalte" (Python v0.63.2, unveraendert) kommen die Inhalte der C#-Linie aus
/// inhalte_csharp/lernen, nur angehaengt: neue Eintraege stehen hinter den bisherigen, ihre
/// <see cref="Lerninhalt.Nummer"/> zaehlt weiter (der Lernstand der Szenarien und Testprojekte
/// speichert Nummer - 1). Die bisherigen Inhalte teilt einteilung_bisher.json in "si" und
/// "gemeinsam" ein (ohne Eintrag si). <see cref="Fuer"/> liefert die Sicht einer Fachrichtung:
/// gemeinsame Inhalte plus die eigenen, in der Reihenfolge der Gesamtliste. SI sieht damit genau
/// die bisherigen Inhalte (alle G-Werte bleiben). Das Spiel liest nur <see cref="Bisher"/>.
/// </summary>
public sealed partial class Lerninhalte
{
    /// <summary>Listen in inhalte_csharp/lernen (Name ohne ".json"), jede darf fehlen.</summary>
    public static readonly IReadOnlyList<(InhaltArt Art, string Name)> LernenListen =
    [
        (InhaltArt.Karteikarte, "karteikarten"),
        (InhaltArt.Quizfrage, "quizfragen"),
        (InhaltArt.Ap2Szenario, "szenarien_ap2"),
        (InhaltArt.Testprojekt, "projektarbeiten"),
    ];

    public const string EinteilungDatei = "einteilung_bisher";
    public const string ThemenDatei = "themen";
    public const string ZusatzDatei = "rahmenplan_zusatz";

    private Lerninhalte? _alle;
    private Lerninhalte? _bisher;
    private readonly ConcurrentDictionary<string, IReadOnlyList<Lerninhalt>> _ap2Je = new(StringComparer.Ordinal);

    /// <summary>Fachrichtung dieser Sicht (si, ae, dpa, dv); null = alle Inhalte ohne Auswahl.</summary>
    public string? Sicht { get; private init; }

    /// <summary>Alle Inhalte aller Fachrichtungen (bisherige plus neue), Grundlage jeder Sicht.</summary>
    public Lerninhalte Alle => _alle ?? this;

    /// <summary>Nur die Dateien aus "inhalte" wie Python v0.63.2 (das Spiel liest nur diese, Nico 5c).</summary>
    public Lerninhalte Bisher => _bisher ?? this;

    /// <summary>Einteilung der bisherigen Inhalte (leer, wenn ohne inhalte_csharp/lernen geladen).</summary>
    public Einteilung Einteilung { get; private init; } = Einteilung.Leer;

    /// <summary>inhalte_csharp/lernen/rahmenplan_zusatz.json (leer, wenn ohne geladen).</summary>
    public JsonObject RahmenplanZusatz { get; private init; } = [];

    /// <summary>Fachrichtung der Themen aus inhalte_csharp/lernen/themen.json (Themen-Kennung -> Wert des Feldes).</summary>
    public IReadOnlyDictionary<string, string?> NeueThemen { get; private init; } = new Dictionary<string, string?>();

    /// <summary>Neue Eintraege je Art in Dateireihenfolge (fuer Pruefung und nur-anhaengen-Test).</summary>
    public IReadOnlyDictionary<InhaltArt, IReadOnlyList<Lerninhalt>> Neue { get; private init; } =
        new Dictionary<InhaltArt, IReadOnlyList<Lerninhalt>>();

    /// <summary>
    /// Bisherige Inhalte plus inhalte_csharp/lernen. <paramref name="lernen"/> liefert eine Datei
    /// (Name ohne ".json") oder null, wenn es sie nicht gibt. Die Zugehoerigkeit der bisherigen
    /// Eintraege wird nach der Einteilung gesetzt (die 54 erzeugten Subnetz-Aufgaben unter quizfragen_erzeugt).
    /// </summary>
    public static Lerninhalte MitLernen(Lerninhalte bisher, Func<string, byte[]?> lernen) => MitLernen(bisher, lernen, null);

    /// <summary>
    /// Phase 10 W1 (Weisung P9-13, Nico 5): wie <see cref="MitLernen(Lerninhalte, Func{string, byte[]?})"/>, von den
    /// neuen Eintraegen aber nur die der <paramref name="fachrichtungen"/> (null = alle). Die bisherigen Inhalte sind
    /// immer ganz da. Jeder Eintrag behaelt die Nummer, die er beim Laden aller Fachrichtungen haette, und jede Sicht
    /// einer geladenen Fachrichtung ist gleich der Sicht beim Laden aller. Eine weitere Fachrichtung kommt mit
    /// <see cref="MitFachrichtung"/> dazu.
    /// </summary>
    public static Lerninhalte MitLernen(Lerninhalte bisher, Func<string, byte[]?> lernen, IReadOnlyCollection<string>? fachrichtungen) =>
        MitLernen(bisher, new LernenOrdner(lernen), fachrichtungen, null);

    /// <summary>Wie oben mit einem <see cref="LernenOrdner"/>, dessen Listen schon im Hintergrund gelesen werden.</summary>
    public static Lerninhalte MitLernen(Lerninhalte bisher, LernenOrdner ordner, IReadOnlyCollection<string>? fachrichtungen) =>
        MitLernen(bisher, ordner, fachrichtungen, null);

    private static Lerninhalte MitLernen(Lerninhalte bisher, LernenOrdner ordner, IReadOnlyCollection<string>? fachrichtungen,
        Lerninhalte? vorher)
    {
        var lernen = ordner.Lesen;
        var geladen = fachrichtungen?.ToHashSet(StringComparer.Ordinal);
        var einteilung = vorher?.Einteilung ?? (lernen(EinteilungDatei) is { } e ? Einteilung.Laden(e) : Einteilung.Leer);
        if (vorher is null)
        {
            Einteilen(bisher, einteilung);
        }

        var neue = new Dictionary<InhaltArt, IReadOnlyList<Lerninhalt>>();
        foreach (var (art, name) in LernenListen)
        {
            var vorherAnzahl = art switch
            {
                InhaltArt.Karteikarte => bisher.Karteikarten.Count,
                InhaltArt.Quizfrage => bisher.Quizfragen.Count,
                InhaltArt.Ap2Szenario => bisher.Ap2Szenarien.Count,
                _ => bisher.Testprojekte.Count,
            };
            List<(int Index, JsonObject Daten)> daten = geladen is null
                ? lernen(name) is { } dateiInhalt ? LadeListe(_ => dateiInhalt, name).Select((d, i) => (i, d)).ToList() : []
                : ordner.Dokument(name) is { } dokument ? LadeListeFuer(dokument, name, geladen) : [];
            neue[art] = daten.Select(d => new Lerninhalt(art, vorherAnzahl + d.Index + 1, d.Daten)
            {
                Zugehoerigkeit = d.Daten[Fachrichtungen.Feld] is JsonValue v && v.GetValueKind() == JsonValueKind.String
                    && Fachrichtungen.Werte.Contains(v.GetValue<string>()) ? v.GetValue<string>() : Ungueltig,
            }).ToList();
        }

        var (themen, neueThemen) = vorher is not null ? (vorher.Themen.ToDictionary(p => p.Key, p => p.Value), vorher.NeueThemen.ToDictionary(p => p.Key, p => p.Value))
            : ThemenMitLernen(bisher, lernen(ThemenDatei));
        return new Lerninhalte
        {
            Ordner = bisher.Ordner,
            Karteikarten = [.. bisher.Karteikarten, .. neue[InhaltArt.Karteikarte]],
            Quizfragen = [.. bisher.Quizfragen, .. neue[InhaltArt.Quizfrage]],
            AnzahlQuizfragenDatei = bisher.AnzahlQuizfragenDatei,
            Ap2Szenarien = [.. bisher.Ap2Szenarien, .. neue[InhaltArt.Ap2Szenario]],
            Ap1Szenarien = bisher.Ap1Szenarien,
            Testprojekte = [.. bisher.Testprojekte, .. neue[InhaltArt.Testprojekt]],
            Themen = themen,
            ThemenReihenfolge = Reihenfolge(themen),
            ThemaNach = ThemenNachId(themen),
            Rahmenplan = bisher.Rahmenplan,
            RahmenplanZuordnung = bisher.RahmenplanZuordnung,
            _bisher = bisher,
            Einteilung = einteilung,
            RahmenplanZusatz = vorher?.RahmenplanZusatz ?? (lernen(ZusatzDatei) is { } z ? LadeObjekt(_ => z, ZusatzDatei) : []),
            NeueThemen = neueThemen,
            Neue = neue,
            Geladen = geladen,
            _lernen = ordner,
        };
    }

    /// <summary>Phase 10 W1: die geladenen Fachrichtungen der neuen Inhalte (null = alle).</summary>
    public IReadOnlySet<string>? Geladen { get; private init; }

    private LernenOrdner? _lernen;
    private readonly ConcurrentDictionary<string, Lazy<Lerninhalte>> _mit = new(StringComparer.Ordinal);

    /// <summary>Ist <paramref name="fachrichtung"/> in diesen Inhalten (alle Fachrichtungen geladen oder diese)?</summary>
    public bool HatFachrichtung(string fachrichtung) => Alle.Geladen is not { } g || g.Contains(fachrichtung);

    /// <summary>
    /// Phase 10 W1 (Weisung P9-13, Nico 5): alle Inhalte plus die neuen Eintraege von <paramref name="fachrichtung"/>
    /// (nachladen beim Wechsel). Schon geladen: dieselben Inhalte. Je Fachrichtung wird nur einmal nachgeladen,
    /// auch wenn mehrere gleichzeitig fragen; die bisherigen Inhalte werden nicht neu gelesen.
    /// </summary>
    public Lerninhalte MitFachrichtung(string fachrichtung)
    {
        var alle = Alle;
        if (alle.HatFachrichtung(fachrichtung) || alle._lernen is not { } lernen)
        {
            return alle;
        }
        return alle._mit.GetOrAdd(fachrichtung, fr => new Lazy<Lerninhalte>(
            () => MitLernen(alle.Bisher, lernen, [.. alle.Geladen!, fr], alle))).Value;
    }

    /// <summary>Zugehoerigkeit der bisherigen Eintraege nach der Einteilung (einmal je Laden der bisherigen Inhalte).</summary>
    private static void Einteilen(Lerninhalte bisher, Einteilung einteilung)
    {
        foreach (var karte in bisher.Karteikarten)
        {
            karte.Zugehoerigkeit = einteilung.Wert(Einteilung.Karteikarten, karte.Frage);
        }
        for (var i = 0; i < bisher.Quizfragen.Count; i++)
        {
            bisher.Quizfragen[i].Zugehoerigkeit = einteilung.Wert(i < bisher.AnzahlQuizfragenDatei
                ? Einteilung.Quizfragen : Einteilung.QuizErzeugt, bisher.Quizfragen[i].Frage);
        }
        foreach (var (liste, art) in new[] { (bisher.Ap1Szenarien, Einteilung.Ap1), (bisher.Ap2Szenarien, Einteilung.Ap2),
            (bisher.Testprojekte, Einteilung.Projekte) })
        {
            foreach (var eintrag in liste)
            {
                eintrag.Zugehoerigkeit = einteilung.Wert(art, eintrag.Titel);
            }
        }
    }

    /// <summary>
    /// Wie <see cref="MitLernen"/>, aber eine nicht lesbare Datei in inhalte_csharp/lernen nimmt nie die bisherigen
    /// Inhalte mit: dann gelten nur die bisherigen (alles si, AE/DPA/DV sehen nichts), der Fehler geht an
    /// <paramref name="fehler"/>. Ein Fehler in inhalte/ selbst wirft wie bisher.
    /// </summary>
    public static Lerninhalte MitLernenSicher(Func<Lerninhalte> bisher, Func<string, byte[]?> lernen, Action<Exception> fehler,
        IReadOnlyCollection<string>? fachrichtungen = null) => MitLernenSicher(bisher, new LernenOrdner(lernen), fehler, fachrichtungen);

    /// <summary>Wie oben mit einem <see cref="LernenOrdner"/> (Phase 10 W1: Listen schon im Hintergrund gelesen).</summary>
    public static Lerninhalte MitLernenSicher(Func<Lerninhalte> bisher, LernenOrdner lernen, Action<Exception> fehler,
        IReadOnlyCollection<string>? fachrichtungen)
    {
        try
        {
            return MitLernen(bisher(), lernen, fachrichtungen);
        }
        catch (Exception ausnahme) when (ausnahme is not OutOfMemoryException)
        {
            fehler(ausnahme);
            return MitLernen(bisher(), _ => null);
        }
    }

    /// <summary>Zugehoerigkeit eines neuen Eintrags ohne gueltiges Feld "fachrichtung": niemand sieht ihn, die Pruefung meldet ihn.</summary>
    public const string Ungueltig = "";

    /// <summary>
    /// Sicht einer Fachrichtung (unbekannt = si): gemeinsame und eigene Inhalte in der Reihenfolge der
    /// Gesamtliste, Themen mit sichtbaren Inhalten (SI: alle bisherigen) plus die eigenen neuen Themen,
    /// Rahmenplan bei AE, DPA und DV mit dem Zusatz der Fachrichtung.
    /// </summary>
    public Lerninhalte Fuer(string? fachrichtung)
    {
        var alle = Alle;
        var fr = fachrichtung is not null && Fachrichtungen.Werte.Contains(fachrichtung) ? fachrichtung : Fachrichtungen.Vorgabe;
        bool Sichtbar(Lerninhalt e) => e.SichtbarFuer(fr);
        var karten = alle.Karteikarten.Where(Sichtbar).ToList();
        var quiz = alle.Quizfragen.Where(Sichtbar).ToList();
        var ap2 = alle.Ap2Szenarien.Where(Sichtbar).ToList();
        var ap1 = alle.Ap1Szenarien.Where(Sichtbar).ToList();
        var projekte = alle.Testprojekte.Where(Sichtbar).ToList();
        var benutzt = karten.Concat(quiz).Concat(ap2).Concat(ap1).Concat(projekte)
            .Select(e => e.Thema).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var themen = new Dictionary<string, IReadOnlyList<Thema>>();
        foreach (var (fachbereich, liste) in alle.Themen)
        {
            themen[fachbereich] = liste.Where(t => alle.NeueThemen.TryGetValue(t.Id, out var eigene)
                ? eigene == fr
                : fr == Fachrichtungen.Systemintegration || benutzt.Contains(t.Id)).ToList();
        }
        return new Lerninhalte
        {
            Ordner = alle.Ordner,
            Karteikarten = karten,
            Quizfragen = quiz,
            AnzahlQuizfragenDatei = alle.Quizfragen.Take(alle.AnzahlQuizfragenDatei).Count(Sichtbar),
            Ap2Szenarien = ap2,
            Ap1Szenarien = ap1,
            Testprojekte = projekte,
            Themen = themen,
            ThemenReihenfolge = Reihenfolge(themen),
            ThemaNach = alle.ThemaNach,
            Rahmenplan = fr == Fachrichtungen.Systemintegration ? alle.Rahmenplan : RahmenplanFuer(alle.Rahmenplan, alle.RahmenplanZusatz, fr),
            RahmenplanZuordnung = alle.RahmenplanZuordnung,
            _alle = alle,
            _bisher = alle.Bisher,
            Sicht = fr,
            Einteilung = alle.Einteilung,
            RahmenplanZusatz = alle.RahmenplanZusatz,
            NeueThemen = alle.NeueThemen,
            Neue = alle.Neue,
        };
    }

    /// <summary>
    /// AP2-Szenarien genau einer Fachrichtung (Quelle der Klausur: "ap2" = si, "ap2_&lt;fr&gt;"), nur angehaengt.
    /// Phase 10 W1: ist die Fachrichtung noch nicht geladen (Pruefung von vor einem Wechsel), wird sie hier nachgeladen.
    /// </summary>
    public IReadOnlyList<Lerninhalt> Ap2SzenarienFuer(string fachrichtung) =>
        Alle._ap2Je.GetOrAdd(fachrichtung, fr => MitFachrichtung(fr).Ap2Szenarien.Where(e => e.Zugehoerigkeit == fr).ToList());

    /// <summary>
    /// Rahmenplan der Sicht AE, DPA oder DV: Kopie des bisherigen Plans, dazu die Lernfelder der
    /// Fachrichtung aus dem Zusatz (Feld "fachrichtung") und die AP2-Zuordnung ihrer Punkte.
    /// </summary>
    public static JsonObject RahmenplanFuer(JsonObject rahmenplan, JsonObject zusatz, string fachrichtung)
    {
        var plan = (JsonObject)rahmenplan.DeepClone();
        if (zusatz["lernfelder"] is JsonArray felder && plan["lernfelder"] is JsonArray ziel)
        {
            foreach (var feld in felder.OfType<JsonObject>().Where(f => (string?)f[Fachrichtungen.Feld] == fachrichtung))
            {
                var kopie = (JsonObject)feld.DeepClone();
                kopie.Remove(Fachrichtungen.Feld);
                kopie.Remove("beleg");
                ziel.Add(kopie);
            }
        }
        if (zusatz["ap2"] is JsonObject zuordnung && zuordnung[fachrichtung] is JsonObject punkte && plan["punkte"] is JsonArray alle)
        {
            foreach (var punkt in alle.OfType<JsonObject>())
            {
                if ((string?)punkt["id"] is { } id && punkte[id] is JsonObject eintrag && eintrag["bereiche"] is JsonArray bereiche)
                {
                    punkt["ap2"] = bereiche.DeepClone();
                }
            }
        }
        return plan;
    }

    private static (Dictionary<string, IReadOnlyList<Thema>>, Dictionary<string, string?>) ThemenMitLernen(Lerninhalte bisher, byte[]? datei)
    {
        var themen = bisher.Themen.ToDictionary(p => p.Key, p => p.Value);
        var neue = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (datei is null || LadeObjekt(_ => datei, ThemenDatei)["themen"] is not JsonObject daten)
        {
            return (themen, neue);
        }
        foreach (var (kurz, eintraege) in daten)
        {
            if (!Fachbereiche.Schluessel.TryGetValue(kurz, out var voll))
            {
                throw new InvalidDataException("lernen/themen.json: unbekannter Fachbereich " + kurz);
            }
            var liste = themen.TryGetValue(voll, out var alt) ? alt.ToList() : [];
            foreach (var e in eintraege!.AsArray().OfType<JsonObject>())
            {
                var thema = new Thema((string)e["id"]!, (string)e["name"]!, (string)e["kurz"]!, voll);
                liste.Add(thema);
                neue[thema.Id] = e[Fachrichtungen.Feld] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;
            }
            themen[voll] = liste;
        }
        return (themen, neue);
    }

    private static List<string> Reihenfolge(Dictionary<string, IReadOnlyList<Thema>> themen) =>
        Fachbereiche.Alle.SelectMany(f => themen.TryGetValue(f, out var liste) ? liste : []).Select(t => t.Id).ToList();
}

/// <summary>
/// Phase 9 W2 (Plan 12.1): inhalte_csharp/lernen/einteilung_bisher.json, erzeugt von
/// werkzeuge/inhalte/einteilen.py. Je Inhaltsart Schluessel (Fragetext, Titel oder Kennung) -> "si"
/// oder "gemeinsam"; ohne Eintrag gilt si.
/// </summary>
public sealed class Einteilung
{
    public const string Karteikarten = "karteikarten";
    public const string Quizfragen = "quizfragen";
    public const string Ap1 = "szenarien_ap1";
    public const string Ap2 = "szenarien_ap2";
    public const string Projekte = "projektarbeiten";
    public const string Fachgespraech = "fachgespraech";

    /// <summary>Die im Programm erzeugten Subnetz-Aufgaben (hinter quizfragen.json), Weisung P9-6: gemeinsam.</summary>
    public const string QuizErzeugt = "quizfragen_erzeugt";

    public static readonly IReadOnlyList<string> Arten = [Karteikarten, Quizfragen, QuizErzeugt, Ap1, Ap2, Projekte, Fachgespraech];

    public static readonly Einteilung Leer = new([]);

    private readonly Dictionary<string, Dictionary<string, string>> _werte;

    private Einteilung(Dictionary<string, Dictionary<string, string>> werte) => _werte = werte;

    /// <summary>Datei lesen; nur "si" und "gemeinsam" sind gueltige Werte (sonst Abbruch).</summary>
    public static Einteilung Laden(byte[] datei)
    {
        var wurzel = JsonNode.Parse(datei) as JsonObject
            ?? throw new InvalidDataException(EinteilungName + " enthaelt kein Objekt");
        var werte = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var art in Arten)
        {
            var teil = new Dictionary<string, string>(StringComparer.Ordinal);
            if (wurzel[art] is JsonObject eintraege)
            {
                foreach (var (schluessel, wert) in eintraege)
                {
                    var text = wert is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;
                    if (text is not (Fachrichtungen.Systemintegration or Fachrichtungen.Gemeinsam))
                    {
                        throw new InvalidDataException(EinteilungName + ": unbekannter Wert bei " + art + " '" + schluessel + "'");
                    }
                    teil[schluessel] = text;
                }
            }
            werte[art] = teil;
        }
        return new Einteilung(werte);
    }

    private const string EinteilungName = "lernen/einteilung_bisher.json";

    /// <summary>Wert eines Schluessels; ohne Eintrag si.</summary>
    public string Wert(string art, string? schluessel) =>
        schluessel is not null && _werte.TryGetValue(art, out var teil) && teil.TryGetValue(schluessel, out var wert)
            ? wert : Fachrichtungen.Vorgabe;

    /// <summary>Alle Schluessel einer Art in Dateireihenfolge.</summary>
    public IReadOnlyCollection<string> Schluessel(string art) => _werte.TryGetValue(art, out var teil) ? teil.Keys : [];

    /// <summary>Anzahl der Eintraege mit einem Wert je Art.</summary>
    public int Anzahl(string art, string wert) => _werte.TryGetValue(art, out var teil) ? teil.Values.Count(w => w == wert) : 0;
}

/// <summary>
/// Phase 10 W1 (Weisung P9-13): Dateien aus inhalte_csharp/lernen (Name ohne ".json" -> Inhalt, null = fehlt). Die
/// Listen werden je Datei einmal gelesen und als JSON-Dokument gemerkt, damit das Nachladen einer Fachrichtung sie nicht
/// neu zerlegt; <see cref="Vorab"/> beginnt damit im Hintergrund, waehrend die bisherigen Inhalte laden.
/// </summary>
public sealed class LernenOrdner(Func<string, byte[]?> lesen)
{
    private readonly ConcurrentDictionary<string, Lazy<JsonDocument?>> _dokumente = new(StringComparer.Ordinal);

    public Func<string, byte[]?> Lesen { get; } = lesen;

    /// <summary>Liste <paramref name="name"/> als Dokument (null = Datei fehlt); ein Lesefehler kommt hier an.</summary>
    public JsonDocument? Dokument(string name) =>
        _dokumente.GetOrAdd(name, n => new Lazy<JsonDocument?>(() => Lesen(n) is { } datei ? JsonDocument.Parse(datei) : null)).Value;

    /// <summary>Alle Listen schon im Hintergrund lesen (je Datei eine Aufgabe); Fehler zeigen sich erst bei <see cref="Dokument"/>.</summary>
    public void Vorab()
    {
        foreach (var (_, name) in Lerninhalte.LernenListen)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    Dokument(name);
                }
                catch (Exception)
                {
                    // Lazy merkt sich den Fehler, MitLernen meldet ihn
                }
            });
        }
    }
}
