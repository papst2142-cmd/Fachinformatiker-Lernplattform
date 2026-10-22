using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Core.Pruefung;

/// <summary>Ein Pruefungsbereich der Klausursimulation (fisi_pruefung EXAMS, nur SI).</summary>
public sealed record KlausurArt(string Art, string Kurz, int Minuten, string Quelle, string Name,
    IReadOnlyList<IReadOnlyList<string>>? Themen, IReadOnlyList<string>? Pool, IReadOnlyList<string>? QuizThemen);

/// <summary>Ein Eintrag des Verlaufs (DBManager.exams, neueste zuerst).</summary>
public sealed record KlausurEintrag(string Zeit, string? Art, double? Punkte, string? Note, JsonNode? Daten, string? Kennung);

/// <summary>
/// Lerninhalte fuer die Pruefung (QUIZ_QUESTIONS, AP1_SZENARIEN, SZENARIEN, QUESTION_RENAMES);
/// die aufbereiteten Aufgaben werden je Szenario einmal gerechnet und behalten.
/// </summary>
public sealed class KlausurInhalte
{
    private readonly Dictionary<(string, int), Aufgabe?> _teile = [];
    private readonly Dictionary<string, Lerninhalt> _frage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _umbenennung = new(StringComparer.Ordinal);
    private readonly Lock _sperre = new();

    public KlausurInhalte(Lerninhalte inhalte, IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen)
    {
        Inhalte = inhalte;
        foreach (var frage in inhalte.Quizfragen)
        {
            _frage[frage.Frage ?? ""] = frage;      // {q["q"]: q} - die letzte gewinnt
        }
        foreach (var (tabelle, alt, neu) in umbenennungen)
        {
            if (tabelle == "quiz_answers")
            {
                _umbenennung[alt] = neu;
            }
        }
    }

    public Lerninhalte Inhalte { get; }

    /// <summary>source_data: AP1-Szenarien fuer "ap1", sonst AP2.</summary>
    public IReadOnlyList<Lerninhalt> Quelle(string? quelle) => quelle == "ap1" ? Inhalte.Ap1Szenarien : Inhalte.Ap2Szenarien;

    /// <summary>scenario_parts(source_data(quelle)[index]); IndexOutOfRange wie Python bei falschem Index.</summary>
    public Aufgabe? Teile(string? quelle, int index)
    {
        var liste = Quelle(quelle);
        if (index < 0 || index >= liste.Count)
        {
            throw new IndexOutOfRangeException("Aufgabe " + index.ToString(CultureInfo.InvariantCulture) + " fehlt");
        }
        var schluessel = (quelle == "ap1" ? "ap1" : "ap2", index);
        lock (_sperre)
        {
            if (_teile.TryGetValue(schluessel, out var fertig))
            {
                return fertig;
            }
        }
        var teile = Kriterien.Teile(liste[index]);
        lock (_sperre)
        {
            _teile[schluessel] = teile;
        }
        return teile;
    }

    /// <summary>usable_indices: Aufgaben eines Themas, die sich fuer die Pruefung eignen.</summary>
    public List<int> Nutzbar(string quelle, string thema)
    {
        var liste = Quelle(quelle);
        var ergebnis = new List<int>();
        for (var i = 0; i < liste.Count; i++)
        {
            if (liste[i].SzenarioThema == thema && Teile(quelle, i) is not null)
            {
                ergebnis.Add(i);
            }
        }
        return ergebnis;
    }

    /// <summary>by_question[frage] oder null.</summary>
    public Lerninhalt? Quizfrage(string? frage) => frage is not null && _frage.TryGetValue(frage, out var q) ? q : null;

    /// <summary>current_question: heutiger Fragetext (auch ueber mehrere Umbenennungen).</summary>
    public string? AktuelleFrage(string? frage)
    {
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        while (frage is not null && _umbenennung.TryGetValue(frage, out var neu) && gesehen.Add(frage))
        {
            frage = neu;
        }
        return frage;
    }

    /// <summary>wiso_pool: Fragetexte mit Thema aus quiz_themen, Reihenfolge der Inhalte.</summary>
    public List<string> WisoPool(KlausurArt art) =>
        Inhalte.Quizfragen.Where(q => q.Thema is { } t && art.QuizThemen!.Contains(t)).Select(q => q.Frage ?? "").ToList();
}

/// <summary>
/// Phase 4, W2 (PB5): Klausursimulation wie fisi_pruefung v0.63.2 - Pruefung zusammenstellen
/// (new_exam, used_items, _pick) und Zeit (start_utc, elapsed_seconds, seconds_left, submit,
/// time_text). Vergleichswerte G32 (neu, zeit). Die Zeit laeuft nach der Uhr weiter, auch wenn
/// das Programm zu ist (keine Pause, Nicos Wunsch 0.51). Bewusst anders als Python: der Start
/// nimmt den echten Augenblick (Python: naive Ortszeit, in der doppelten Stunde der Zeitumstellung
/// eine Stunde daneben); G32 "momente" belegt beide Wege.
/// </summary>
public static class Klausur
{
    public const string Ap1 = "ap1";
    public const string Konzeption = "konzeption";
    public const string Netzwerke = "netzwerke";
    public const string Wiso = "wiso";
    public const int WisoAnzahl = 30;
    public const int WisoSeite = 5;
    public const string Laeuft = "laeuft";
    public const string Bewertung = "bewertung";
    public const string ZeitFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>EXAMS (Reihenfolge der Auswahl).</summary>
    public static readonly IReadOnlyList<KlausurArt> Arten =
    [
        new(Ap1, "AP1", 90, "ap1", "AP1 · Einrichten eines IT-gestützten Arbeitsplatzes",
            [["Rechnertechnik & Zahlensysteme"], ["Rechnernetze Grundlagen"], ["Datenschutz & Sicherheit"],
             ["Projektplanung", "Arbeitsplatz einrichten: Bedarf, Beschaffung, Übergabe"]], null, null),
        new(Konzeption, "Konzeption", 90, "ap2", "AP2 · Konzeption und Administration von IT-Systemen", null,
            ["Storage & RAID", "Virtualisierung", "IT-Sicherheit", "Projektmanagement", "Wirtschaft & Beratung",
             "Systemadministration", "Automatisierung & Skripte"], null),
        new(Netzwerke, "Netzwerke", 90, "ap2", "AP2 · Analyse und Entwicklung von Netzwerken",
            [["Subnetting & Routing"], ["Subnetting & Routing"], ["Netzwerkdesign"], ["IT-Sicherheit"]], null, null),
        new(Wiso, "WiSo", 60, "quiz", "AP2 · Wirtschafts- und Sozialkunde", null, null, ["recht", "arbeitswelt"]),
    ];

    /// <summary>EXAM[art] oder null.</summary>
    public static KlausurArt? Art(string? art) => Arten.FirstOrDefault(a => a.Art == art);

    /// <summary>random.Random(None): Startwert aus dem Zufallsgenerator des Systems (os.urandom).</summary>
    public static PythonZufall Zufall() => new(new BigInteger(RandomNumberGenerator.GetBytes(32), isUnsigned: true));

    // -- Bereits gestellt ---------------------------------------------------------------------

    /// <summary>used_items: gestellte Aufgaben (Index) und Fragen (heutiger Text) dieses Bereichs.</summary>
    public sealed class Benutzt
    {
        internal readonly HashSet<double> Indizes = [];
        internal readonly HashSet<string> Fragen = new(StringComparer.Ordinal);
        internal readonly List<string> Texte = [];

        /// <summary>sorted(map(str, used)) fuer den Vergleich mit G32.</summary>
        public List<string> Sortiert() => Texte.Distinct().Order(StringComparer.Ordinal).ToList();
    }

    public static Benutzt Gestellt(KlausurInhalte inhalte, IEnumerable<KlausurEintrag> verlauf, string art)
    {
        var benutzt = new Benutzt();
        foreach (var eintrag in verlauf)
        {
            if (eintrag.Art != art || eintrag.Daten is not JsonObject daten)
            {
                continue;
            }
            if (daten["aufgaben"] is JsonArray aufgaben)
            {
                foreach (var aufgabe in aufgaben)
                {
                    var index = aufgabe is JsonObject o ? o["index"] : null;
                    if (Json.Zahl(index) is { } zahl)
                    {
                        benutzt.Indizes.Add(zahl);
                    }
                    benutzt.Texte.Add(Json.PyStr(index));
                }
            }
            if (daten["fragen"] is JsonArray fragen)
            {
                foreach (var frage in fragen)
                {
                    var text = frage is JsonObject o ? Json.Text(o["q"]) : Json.Text(frage);
                    var heute = inhalte.AktuelleFrage(text);
                    if (heute is not null)
                    {
                        benutzt.Fragen.Add(heute);
                    }
                    benutzt.Texte.Add(heute ?? "None");
                }
            }
        }
        return benutzt;
    }

    // -- Neue Pruefung ------------------------------------------------------------------------

    /// <summary>new_exam mit dem echten Augenblick jetzt (start_utc = ganze Sekunden, Ortszeit in zone).</summary>
    public static JsonObject Neu(KlausurInhalte inhalte, string art, IEnumerable<KlausurEintrag> verlauf, PythonZufall zufall,
        DateTimeOffset jetzt, TimeZoneInfo zone)
    {
        var sekunden = (long)Math.Floor(jetzt.ToUnixTimeMilliseconds() / 1000.0);
        var ort = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UnixEpoch.AddSeconds(sekunden), zone);
        return Neu(inhalte, art, verlauf, zufall, ort.ToString(ZeitFormat, CultureInfo.InvariantCulture), sekunden);
    }

    /// <summary>new_exam wie Python mit naiver Ortszeit (datetime.now()): start_utc wie timestamp() mit fold=0.</summary>
    public static JsonObject NeuOrtszeit(KlausurInhalte inhalte, string art, IEnumerable<KlausurEintrag> verlauf, PythonZufall zufall,
        DateTime ortszeit, TimeZoneInfo zone)
    {
        var ganz = new DateTime(ortszeit.Ticks - ortszeit.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
        return Neu(inhalte, art, verlauf, zufall, ganz.ToString(ZeitFormat, CultureInfo.InvariantCulture), OrtszeitSekunden(ganz, zone));
    }

    private static JsonObject Neu(KlausurInhalte inhalte, string art, IEnumerable<KlausurEintrag> verlauf, PythonZufall zufall,
        string start, long startUtc)
    {
        var bereich = Art(art) ?? throw new ArgumentException("Unbekannter Pruefungsbereich " + art, nameof(art));
        var benutzt = Gestellt(inhalte, verlauf, art);
        var zustand = new JsonObject
        {
            ["art"] = art,
            ["start"] = start,
            ["start_utc"] = startUtc,
            ["minuten"] = bereich.Minuten,
            ["phase"] = Laeuft,
            ["antworten"] = new JsonObject(),
            ["punkte"] = new JsonObject(),
            ["kriterien"] = new JsonObject(),
        };
        if (art == Wiso)
        {
            var pool = inhalte.WisoPool(bereich);
            var frisch = pool.Where(q => !benutzt.Fragen.Contains(q)).ToList();
            var fragen = zufall.Sample(frisch, Math.Min(frisch.Count, WisoAnzahl));
            if (fragen.Count < WisoAnzahl)
            {
                var rest = pool.Where(q => !fragen.Contains(q)).ToList();
                fragen.AddRange(zufall.Sample(rest, Math.Min(rest.Count, WisoAnzahl - fragen.Count)));
            }
            zufall.Shuffle(fragen);
            zustand["fragen"] = new JsonArray([.. fragen.Select(q => (JsonNode?)q)]);
            var optionen = new JsonObject();
            foreach (var frage in fragen)
            {
                var liste = inhalte.Quizfrage(frage)!.Optionen.ToList();
                zufall.Shuffle(liste);
                optionen[frage] = new JsonArray([.. liste.Select(o => (JsonNode?)o)]);
            }
            zustand["optionen"] = optionen;
            return zustand;
        }
        var themen = bereich.Themen ?? zufall.Sample(bereich.Pool!, 4).Select(t => (IReadOnlyList<string>)new List<string> { t }).ToList();
        var aufgaben = new JsonArray();
        var genommen = new HashSet<int>();
        foreach (var wahl in themen)
        {
            var thema = zufall.Choice(wahl);
            var kandidaten = inhalte.Nutzbar(bereich.Quelle, thema);
            var frei = kandidaten.Where(i => !benutzt.Indizes.Contains(i) && !genommen.Contains(i)).ToList();
            if (frei.Count == 0)
            {
                frei = kandidaten.Where(i => !genommen.Contains(i)).ToList();
            }
            if (frei.Count == 0)
            {
                continue;
            }
            var index = zufall.Choice(frei);
            genommen.Add(index);
            aufgaben.Add(new JsonObject { ["quelle"] = bereich.Quelle, ["index"] = index });
        }
        zustand["aufgaben"] = aufgaben;
        return zustand;
    }

    /// <summary>task_details(state, number).</summary>
    public static Aufgabe? Details(KlausurInhalte inhalte, JsonObject zustand, int nummer)
    {
        var aufgabe = (JsonObject)zustand["aufgaben"]![nummer]!;
        return inhalte.Teile(Json.Text(aufgabe["quelle"]), (int)(Json.Zahl(aufgabe["index"])
            ?? throw new InvalidDataException("Aufgabe ohne Index")));
    }

    /// <summary>answer_key: "%d-%d".</summary>
    public static string Schluessel(int nummer, int teil) =>
        nummer.ToString(CultureInfo.InvariantCulture) + "-" + teil.ToString(CultureInfo.InvariantCulture);

    // -- Zeit ---------------------------------------------------------------------------------

    /// <summary>Jetzt als Sekunden seit 1970 (time.time()).</summary>
    public static double Sekunden(DateTimeOffset jetzt) => (jetzt.UtcTicks - DateTime.UnixEpoch.Ticks) / (double)TimeSpan.TicksPerSecond;

    /// <summary>
    /// Naive Ortszeit als Sekunden seit 1970 wie Pythons datetime.timestamp() (fold=0, _mktime):
    /// in der Luecke der Zeitumstellung und in der doppelten Stunde gilt der Abstand von vorher.
    /// </summary>
    public static long OrtszeitSekunden(DateTime ortszeit, TimeZoneInfo zone)
    {
        var t = (long)Math.Floor((ortszeit.Ticks - DateTime.UnixEpoch.Ticks) / (double)TimeSpan.TicksPerSecond);
        long Lokal(long u) => (long)(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UnixEpoch.AddSeconds(u), zone).Ticks
            - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond;
        var a = Lokal(t) - t;
        var u1 = t - a;
        var t1 = Lokal(u1);
        long b;
        if (t1 == t)
        {
            var u0 = u1 - 24 * 3600;
            b = Lokal(u0) - u0;
            if (a == b)
            {
                return u1;
            }
        }
        else
        {
            b = t1 - u1;
        }
        var u2 = t - b;
        if (Lokal(u2) == t)
        {
            return u2;
        }
        if (t1 == t)
        {
            return u1;
        }
        return Math.Max(u1, u2);
    }

    private static DateTime Start(JsonObject zustand) =>
        DateTime.ParseExact(Json.Text(zustand["start"]) ?? "", ZeitFormat, CultureInfo.InvariantCulture);

    /// <summary>deadline: Start (Ortszeit) plus Minuten.</summary>
    public static DateTime Frist(JsonObject zustand) => Start(zustand).AddMinutes(Json.Zahl(zustand["minuten"]) ?? 0);

    /// <summary>elapsed_seconds: ueber start_utc, sonst "start" als Ortszeit; nie negativ.</summary>
    public static double Vergangen(JsonObject zustand, double jetzt, TimeZoneInfo zone)
    {
        var start = Json.Zahl(zustand["start_utc"]) ?? OrtszeitSekunden(Start(zustand), zone);
        return Math.Max(0.0, jetzt - start);
    }

    private static long Gesamt(JsonObject zustand) => (long)(Json.Zahl(zustand["minuten"]) ?? 0) * 60;

    /// <summary>seconds_left.</summary>
    public static long Rest(JsonObject zustand, double jetzt, TimeZoneInfo zone)
    {
        var gesamt = Gesamt(zustand);
        return Math.Max(0, Math.Min(gesamt, (long)Math.Truncate(gesamt - Vergangen(zustand, jetzt, zone))));
    }

    /// <summary>time_text: "%02d:%02d", ab einer Stunde "%d:%02d:%02d".</summary>
    public static string Zeittext(long sekunden) => sekunden >= 3600
        ? string.Create(CultureInfo.InvariantCulture, $"{sekunden / 3600}:{sekunden % 3600 / 60:00}:{sekunden % 60:00}")
        : string.Create(CultureInfo.InvariantCulture, $"{sekunden / 60:00}:{sekunden % 60:00}");

    /// <summary>submit: Dauer festhalten, Phase "bewertung".</summary>
    public static void Abgeben(JsonObject zustand, double jetzt, TimeZoneInfo zone)
    {
        zustand["dauer"] = Math.Min(Gesamt(zustand), (long)Math.Truncate(Vergangen(zustand, jetzt, zone)));
        zustand["phase"] = Bewertung;
    }

    /// <summary>suggested_points: Punktevorschlag aus abgehakten Kriterien.</summary>
    public static int Vorschlag(int maximum, IReadOnlyList<bool> haken) => haken.Count == 0
        ? 0 : (int)Mathe.PythonRunden.Runde(maximum * haken.Count(h => h) / (double)haken.Count);
}

/// <summary>Zugriff auf JSON-Werte mit Pythons Bedeutung (True ist 1, ganze Zahl bleibt ganz).</summary>
public static class Json
{
    /// <summary>Zahl (auch true/false als 1/0) oder null.</summary>
    public static double? Zahl(JsonNode? knoten)
    {
        if (knoten is not JsonValue wert)
        {
            return null;
        }
        switch (wert.GetValueKind())
        {
            case JsonValueKind.True:
                return 1;
            case JsonValueKind.False:
                return 0;
            case JsonValueKind.Number:
                if (wert.TryGetValue<JsonElement>(out var e))
                {
                    return e.GetDouble();
                }
                if (wert.TryGetValue<long>(out var l))
                {
                    return l;
                }
                if (wert.TryGetValue<int>(out var i))
                {
                    return i;
                }
                if (wert.TryGetValue<double>(out var d))
                {
                    return d;
                }
                return double.Parse(wert.ToJsonString(), CultureInfo.InvariantCulture);
            default:
                return null;
        }
    }

    /// <summary>Text oder null.</summary>
    public static string? Text(JsonNode? knoten) =>
        knoten is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    /// <summary>Ganze Zahl im JSON (ohne Punkt und Exponent)?</summary>
    public static bool Ganz(JsonNode? knoten) => knoten is JsonValue v && v.GetValueKind() == JsonValueKind.Number
        && v.ToJsonString().IndexOfAny(['.', 'e', 'E']) < 0;

    /// <summary>str(x) von Python fuer einfache Werte.</summary>
    public static string PyStr(JsonNode? knoten) => knoten switch
    {
        null => "None",
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        JsonValue v when v.GetValueKind() == JsonValueKind.True => "True",
        JsonValue v when v.GetValueKind() == JsonValueKind.False => "False",
        JsonValue v when Ganz(v) => v.ToJsonString(),
        JsonValue v => Daten.PythonJson.Zahl(Zahl(v) ?? 0),
        _ => knoten.ToJsonString(),
    };

    /// <summary>JSON-Wert als Python-Wert fuer PythonDump (PyObjekt, Liste, string, bool, long, double, null).</summary>
    public static object? Wert(JsonNode? knoten) => knoten switch
    {
        null => null,
        JsonObject o => Objekt(o),
        JsonArray a => a.Select(Wert).ToList(),
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => v.GetValue<string>(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when Ganz(v) && long.TryParse(v.ToJsonString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l) => l,
            JsonValueKind.Number => Zahl(v),
            _ => null,
        },
        _ => null,
    };

    private static Daten.PyObjekt Objekt(JsonObject o)
    {
        var p = new Daten.PyObjekt();
        foreach (var (k, v) in o)
        {
            p.Add(k, Wert(v));
        }
        return p;
    }
}
