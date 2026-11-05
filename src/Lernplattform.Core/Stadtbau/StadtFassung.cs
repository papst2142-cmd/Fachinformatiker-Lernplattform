using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 1 (Weisung P12-16, Plan <c>Fi-Austausch/anlagen/P12-15_Plan_Phase13.md</c> Abschnitte 5 und 6): eine
/// Fassung einer Stadt als Datei <c>.fistadt</c> (JSON, UTF-8). Dasselbe Format schreibt spaeter das Stadtbau-Werkzeug
/// (Nico 1a: gemeinsamer Teil von Spiel und Werkzeug); das Spiel zeichnet die Stadt aus der Fassung, die
/// <see cref="AktiveFassung"/> nennt.
/// Inhalt: <c>karte</c> (Aufbau wie <c>inhalte/spiel/orte.json</c>: Welt, Orte, Gebaeudemodelle), <c>stadtdaten</c> (wie
/// <c>inhalte_csharp/spiel/stadt.json</c>: Raster, Uhr, Zugaenge, neue Orte, Darstellung), <c>innenraeume</c> (wie
/// <c>inhalte_csharp/spiel/innenraeume.json</c>), <c>laternen</c> (je Laterne Wunschplatz x, z und doppelt, vorher fest im
/// Programm als <c>Nachtlicht.Skizze</c>), <c>beete</c> (fest gesetzte Beete, vorher <c>Nachtlicht.Beete</c>). Die Regeln
/// (Kleingruen streuen, Verkehr, Fest-Deko, Bahnuebergang, Laternen nach <c>Nachtlicht.Regeln</c> an den Strassenrand)
/// bleiben im Programm und gelten fuer jede Fassung.
/// Geprueft wird jede Fassung mit <see cref="StadtbauPruefung"/>; Fassung 0 (die Stadt bis A 0.41.1) ist ein geschuetzter
/// Sollwert (<c>tests/Vergleich/SOLLWERTE.sha256</c>, Sollwert-Aenderung Nr. 13).
/// </summary>
public sealed class StadtFassung
{
    /// <summary>Wert des Felds "format".</summary>
    public const string Format = "fistadt";

    /// <summary>Hoechste Formatnummer, die dieses Programm liest (eine neuere Datei wird abgelehnt, nie umgeschrieben).</summary>
    public const long FormatVersion = 1;

    /// <summary>Dateiendung der Fassungen und Exporte.</summary>
    public const string Endung = ".fistadt";

    /// <summary>Die Felder der Datei in ihrer festen Reihenfolge (jedes andere Feld ist ein Fehler).</summary>
    public static readonly string[] Felder =
        ["format", "formatversion", "stadt", "werkzeug", "erstellt", "nachricht", "karte", "stadtdaten", "innenraeume", "laternen", "beete", "pruefsumme"];

    /// <summary>Felder des Abschnitts "stadt".</summary>
    public static readonly string[] StadtFelder = ["id", "fassung", "basis"];

    public required string StadtId { get; init; }

    /// <summary>Nummer der Fassung (0 = die Stadt bis A 0.41.1).</summary>
    public required long Nummer { get; init; }

    /// <summary>Fassung, auf der diese aufbaut (null bei Fassung 0 und bei einer leeren Stadt).</summary>
    public long? Basis { get; init; }

    public required string Werkzeug { get; init; }

    /// <summary>Zeitpunkt als Text (ISO 8601 mit Zeitzone, z. B. "2026-11-05T22:30:00+01:00").</summary>
    public required string Erstellt { get; init; }

    public string Nachricht { get; init; } = "";

    public required PyObjekt Karte { get; init; }

    public required PyObjekt Stadtdaten { get; init; }

    public required PyObjekt Innenraeume { get; init; }

    public required IReadOnlyList<(float X, float Z, bool Doppelt)> Laternen { get; init; }

    public required IReadOnlyList<Vector2> Beete { get; init; }

    /// <summary>Dateiname einer Fassung im Ordner <c>inhalte_csharp/spiel/stadtbau/</c>.</summary>
    public static string Dateiname(string stadtId, long nummer) =>
        string.Create(CultureInfo.InvariantCulture, $"{stadtId}_fassung{nummer}{Endung}");

    /// <summary>
    /// Die Spielinhalte mit der Karte dieser Fassung statt <c>orte.json</c> (eine Kopie; alle anderen Abschnitte bleiben
    /// dieselben Objekte).
    /// </summary>
    public PyObjekt MitKarte(PyObjekt inhalt)
    {
        var neu = new PyObjekt();
        foreach (var (k, v) in inhalt)
        {
            neu.Add(k, k == "orte" ? Karte : v);
        }
        return neu;
    }

    /// <summary>Die Stadt dieser Fassung zu Spielinhalten, deren Abschnitt "orte" schon die <see cref="Karte"/> ist.</summary>
    public Stadt StadtFuer(PyObjekt inhalt) => new(inhalt, Stadtdaten) { Fassung = this };

    /// <summary>Die Fassung als Objekt ohne Pruefsumme (Reihenfolge der Felder fest).</summary>
    private PyObjekt OhnePruefsumme() => new()
    {
        { "format", Format },
        { "formatversion", FormatVersion },
        { "stadt", new PyObjekt { { "id", StadtId }, { "fassung", Nummer }, { "basis", Basis } } },
        { "werkzeug", Werkzeug },
        { "erstellt", Erstellt },
        { "nachricht", Nachricht },
        { "karte", Karte },
        { "stadtdaten", Stadtdaten },
        { "innenraeume", Innenraeume },
        { "laternen", Laternen.Select(l => (object?)new List<object?> { Zahl(l.X), Zahl(l.Z), l.Doppelt }).ToList() },
        { "beete", Beete.Select(b => (object?)new List<object?> { Zahl(b.X), Zahl(b.Y) }).ToList() },
    };

    /// <summary>float als kuerzeste Dezimalzahl (0.65f wird 0.65, nicht 0.6499999761581421), ganze Werte als x.0 wie Python.</summary>
    private static double Zahl(float f) => double.Parse(f.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>
    /// Pruefsumme "sha256:&lt;hex&gt;" ueber den Inhalt ohne das Feld "pruefsumme" (json.dumps kompakt mit sortierten
    /// Schluesseln, UTF-8).
    /// </summary>
    public static string Pruefsumme(PyObjekt ohnePruefsumme) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(PythonJson.Text(ohnePruefsumme, sortiert: true))));

    /// <summary>
    /// Die Datei: je Feld eine Zeile (lesbar, und eine Aenderung zeigt im Vergleich das betroffene Feld), Werte wie Pythons
    /// json.dumps mit Standard-Trennzeichen, am Ende ein Zeilenumbruch.
    /// </summary>
    public byte[] Schreiben()
    {
        var objekt = OhnePruefsumme();
        objekt.Add("pruefsumme", Pruefsumme(objekt));
        var text = new StringBuilder("{\n");
        for (var i = 0; i < objekt.Count; i++)
        {
            text.Append(' ').Append(PythonJson.Standard(objekt[i].Key)).Append(": ").Append(PythonJson.Standard(objekt[i].Value));
            text.Append(i + 1 < objekt.Count ? ",\n" : "\n");
        }
        text.Append("}\n");
        return PythonJson.Utf8(text.ToString());
    }

    /// <summary>Liest eine Datei; jeder Fund der Formatpruefung ist ein Fehler (<see cref="StadtbauFehler"/> mit allen Funden).</summary>
    public static StadtFassung Lesen(ReadOnlySpan<byte> datei)
    {
        object? roh;
        try
        {
            roh = Py.Lesen(datei);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new StadtbauFehler(["Die Datei ist kein gültiges JSON."]);
        }
        var funde = StadtbauPruefung.Format(roh);
        if (funde.Count > 0)
        {
            throw new StadtbauFehler(funde);
        }
        var d = (PyObjekt)roh!;
        var stadt = (PyObjekt)d["stadt"]!;
        return new StadtFassung
        {
            StadtId = (string)stadt["id"]!,
            Nummer = Py.Int(stadt["fassung"]),
            Basis = stadt["basis"] is null ? null : Py.Int(stadt["basis"]),
            Werkzeug = (string)d["werkzeug"]!,
            Erstellt = (string)d["erstellt"]!,
            Nachricht = (string)d["nachricht"]!,
            Karte = (PyObjekt)d["karte"]!,
            Stadtdaten = (PyObjekt)d["stadtdaten"]!,
            Innenraeume = (PyObjekt)d["innenraeume"]!,
            Laternen = [.. ((List<object?>)d["laternen"]!).Select(l => (List<object?>)l!).Select(l => ((float)Py.F(l[0]), (float)Py.F(l[1]), (bool)l[2]!))],
            Beete = [.. ((List<object?>)d["beete"]!).Select(b => (List<object?>)b!).Select(b => new Vector2((float)Py.F(b[0]), (float)Py.F(b[1])))],
        };
    }
}

/// <summary>Eine Fassung ist nicht lesbar oder verletzt eine Regel; <see cref="Funde"/> nennt jeden Grund in einfachen Worten.</summary>
public sealed class StadtbauFehler(IReadOnlyList<string> funde) : IOException("Stadt-Fassung ungültig: " + string.Join(" | ", funde))
{
    public IReadOnlyList<string> Funde { get; } = funde;
}
