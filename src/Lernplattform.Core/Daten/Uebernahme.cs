using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Diagnose;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Daten;

/// <summary>Woher der uebernommene Lernstand kommt.</summary>
public enum UebernahmeQuelle
{
    /// <summary>Sicherungsdatei .fisisicherung (Huelle mit Pruefsumme, darin das Austauschformat 2).</summary>
    Sicherung,

    /// <summary>Abgleich-Datei lernstand.json.gz (nur das Austauschformat 2, ohne Huelle und Pruefsumme).</summary>
    AbgleichDatei,

    /// <summary>Datenbank fisi_lernplattform.db der Python-Linie (nur lesen).</summary>
    AlteDatenbank,
}

/// <summary>Warum eine Uebernahme nicht stattfindet (Code, keine Nutzertexte in Welle 1).</summary>
public enum UebernahmeFehler
{
    /// <summary>MSG_NOT_BACKUP: keine Sicherung (kein gzip/JSON, falsche Art, falsche Typen, Datei abgeschnitten).</summary>
    KeineSicherung,

    /// <summary>MSG_NEWER: Formatversion oder Austauschformat neuer als bekannt.</summary>
    ZuNeu,

    /// <summary>MSG_DAMAGED: Pruefsumme stimmt nicht.</summary>
    PruefsummeFalsch,

    /// <summary>MSG_BROKEN: Inhalt hat ein unerwartetes Format.</summary>
    InhaltKaputt,

    /// <summary>Abgleich-Datei beschaedigt (wie Python beim Abgleich).</summary>
    AbgleichDateiKaputt,

    /// <summary>Alte Datenbank nicht lesbar.</summary>
    DatenbankNichtLesbar,

    /// <summary>Quelle ist die eigene Datenbank.</summary>
    GleicheDatei,

    /// <summary>Lernstand wird nicht gespeichert (Lesefehler, Schreibschutz): nichts uebernehmen.</summary>
    NurLesen,

    /// <summary>Die automatische Sicherung davor ist gescheitert: nichts uebernommen.</summary>
    SicherungGescheitert,

    /// <summary>Fehler beim Schreiben: alles zurueckgenommen, Lernstand unveraendert.</summary>
    SchreibenGescheitert,
}

public sealed class UebernahmeAusnahme(UebernahmeFehler fehler, string? version = null)
    : Exception("Uebernahme: " + fehler.ToString())
{
    public UebernahmeFehler Fehler { get; } = fehler;

    /// <summary>Bei ZuNeu: app_version der Sicherung (oder "?").</summary>
    public string? Version { get; } = version;
}

/// <summary>Gelesener Lernstand im Austauschformat 2 (fisi_sync.export_local), noch nichts geschrieben.</summary>
public sealed class Austausch
{
    public required UebernahmeQuelle Quelle { get; init; }

    /// <summary>Feld "schreiber" vorhanden (ab 0.62.4: Marken mit Zone vergleichen).</summary>
    public required bool MitSchreiber { get; init; }

    public required IReadOnlyDictionary<string, object?> Marken { get; init; }

    /// <summary>Feld "aufgehoben" (nur wenn es ein Objekt ist).</summary>
    public IReadOnlyDictionary<string, object?>? Aufhebungen { get; init; }

    /// <summary>Spaltennamen je Tabelle aus der Datei (nur nicht leere Listen).</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Spalten { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<object?[]>> Tabellen { get; init; }

    /// <summary>Sicherung: app_version und erstellt (nur Anzeige).</summary>
    public string? AppVersion { get; init; }

    public string? Erstellt { get; init; }

    internal Austausch MitPfad(string pfad) => new()
    {
        Quelle = Quelle, MitSchreiber = MitSchreiber, Marken = Marken, Aufhebungen = Aufhebungen, Spalten = Spalten,
        Tabellen = Tabellen, AppVersion = AppVersion, Erstellt = Erstellt, QuellPfad = pfad,
    };

    /// <summary>Gelesene Datei (fuer die Pruefung "nicht die eigene Datenbank").</summary>
    public string? QuellPfad { get; init; }

    public int Zeilen => Tabellen.Values.Sum(t => t.Count);
}

/// <summary>Zaehlwerte einer Tabelle (Vorschau oder Ergebnis).</summary>
/// <param name="Neu">Zeilen eingefuegt (vor dem Aufraeumen der Spielstaende).</param>
/// <param name="Vorhanden">Kennung (uid) gab es schon: nicht doppelt eingefuegt.</param>
/// <param name="EntferntDurchMarke">Vorhandene Zeilen, die eine Loeschmarke entfernt.</param>
/// <param name="NichtUebernommen">Zeilen der Datei unter einer Loeschmarke (bleiben geloescht).</param>
/// <param name="Uebersprungen">Zeilen ohne Kennung oder mit fehlenden Spalten.</param>
/// <param name="Abgelehnt">Von der Datenbank abgelehnt (z. B. leerer Pflichtwert).</param>
public sealed record TabellenZahlen(string Tabelle, int Vorher, int Nachher, int Neu, int Vorhanden, int EntferntDurchMarke,
    int NichtUebernommen, int Uebersprungen, int Abgelehnt);

/// <summary>Ergebnis einer Uebernahme oder ihrer Vorschau.</summary>
/// <param name="Neu">Neue Eintraege wie Pythons merge_backup (ohne Platz-Zeilen des Spiels).</param>
/// <param name="Uebernommen">Wie merge_into_local (netto, ohne geloeschte Spieldurchgaenge).</param>
/// <param name="EntferntDurchLoeschmarke">Vorhandene Eintraege, die eine Loeschmarke entfernt.</param>
/// <param name="EntferntSonst">Vorhandene oder neue Zeilen, die das Aufraeumen entfernt (geloeschte Spieldurchgaenge, aeltere Fassungen im Abschlussprojekt).</param>
/// <param name="NichtUebernommen">Eintraege der Datei, die eine Loeschung hier nicht hereinlaesst (Pythons "kept").</param>
/// <param name="UngueltigeMarken">Ungueltige Marken beider Seiten (wie Python stats.invalid).</param>
/// <param name="UngueltigInDatei">Davon in der Datei (fuer den Hinweis im Dialog, L5).</param>
public sealed record UebernahmeBericht(UebernahmeQuelle Quelle, bool Vorschau, int Neu, int Uebernommen, int SchonVorhanden,
    int EntferntDurchLoeschmarke, int EntferntSonst, int NichtUebernommen, int Uebersprungen, int Abgelehnt,
    int UngueltigeMarken, int ZukunftsMarken, int Umbenannt, IReadOnlyDictionary<string, string> MarkenNachher,
    IReadOnlyList<TabellenZahlen> Tabellen, string? Sicherungsdatei, int UngueltigInDatei = 0);

/// <summary>Korrigierte Fragetexte (inhalte/umbenennungen.json, fisi_core.load_question_renames).</summary>
public static class Umbenennungen
{
    /// <summary>(Tabelle, alter Text, neuer Text) in der Reihenfolge der Datei; bei Lesefehler leer.</summary>
    public static IReadOnlyList<(string Tabelle, string Alt, string Neu)> Laden(Func<string, byte[]> lesen)
    {
        try
        {
            if (JsonNode.Parse(lesen("umbenennungen")) is not JsonObject daten)
            {
                return [];
            }
            var liste = new List<(string, string, string)>();
            foreach (var (quelle, tabelle) in new[] { ("karte", "card_events"), ("quiz", "quiz_answers") })
            {
                if (daten[quelle] is JsonObject paare)
                {
                    foreach (var (alt, neu) in paare)
                    {
                        if (neu is JsonValue v && v.TryGetValue<string>(out var text))
                        {
                            liste.Add((tabelle, alt, text));
                        }
                    }
                }
            }
            return liste;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<(string Tabelle, string Alt, string Neu)> Laden(string inhalteOrdner) =>
        Laden(name => File.ReadAllBytes(Path.Combine(inhalteOrdner, name + ".json")));
}

/// <summary>
/// Uebernahme des Lernstands der Python-Linie (Paket L3) wie "Sicherung zusammenfuehren"
/// (fisi_sicherung.read_backup, merge_backup, fisi_sync.merge_into_local mit check_future):
/// neue Eintraege kommen dazu, vorhandene bleiben (Kennung uid), Loeschmarken gelten wie
/// in Python, Fragetexte werden nach umbenennungen.json korrigiert. Vor jeder Uebernahme
/// entsteht automatisch eine Sicherung der C#-Datenbank; scheitert sie, wird nichts
/// uebernommen. Alles in einer Transaktion: ganz oder gar nicht. Die Quelle wird nur gelesen.
/// AlterLernstand.cs und SicherungsPruefung.cs bleiben unveraendert (nur die Pruefsumme wird genutzt).
/// </summary>
public static partial class Uebernahme
{
    public const string SicherungArt = "FISI-Sicherung";   // KIND
    public const int SicherungFormatversion = 1;           // FORMAT_VERSION
    public const int AustauschFormat = 2;                  // fisi_sync.FORMAT
    public const string Schreiber = "schreiber";           // WRITER_KEY
    public const string SicherungsOrdner = "sicherungen";
    public const string SicherungsPraefix = "vor_uebernahme_";

    /// <summary>SYNC_TABLES: Tabellen und Datenspalten (ohne id und uid) in Python-Reihenfolge.</summary>
    public static readonly IReadOnlyList<(string Tabelle, string[] Spalten)> Tabellen =
    [
        ("test_results", ["timestamp", "utc_offset", "score", "total", "percentage", "note", "duration_seconds"]),
        ("card_events", ["timestamp", "utc_offset", "category", "question", "mode", "correct"]),
        ("quiz_answers", ["timestamp", "utc_offset", "category", "question", "correct"]),
        ("scenario_events", ["timestamp", "utc_offset", "scenario_index", "title", "theme", "correct"]),
        ("project_events", ["timestamp", "utc_offset", "project_index", "title", "category", "correct"]),
        ("ap1_events", ["timestamp", "utc_offset", "scenario_index", "title", "theme", "correct"]),
        ("trainer_aufgaben", ["timestamp", "utc_offset", "art", "stufe", "correct"]),
        ("pruefungen", ["timestamp", "utc_offset", "art", "punkte", "note", "dauer", "daten"]),
        ("spiel_ereignisse", ["timestamp", "utc_offset", "typ", "daten", "geraet", "lauf"]),
        ("spiel_plaetze", ["timestamp", "platz", "lauf", "aktion", "daten", "geraet"]),
        ("spiel_bestenliste", ["timestamp", "utc_offset", "durchgang", "art", "schluessel", "wert", "daten"]),
        ("abschlussprojekt", ["timestamp", "projekt", "feld", "wert"]),
    ];

    /// <summary>OPTIONAL_COLUMNS: fehlen sie in einer Zeile, gelten sie als leer.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Optional = new Dictionary<string, string[]>
    {
        ["test_results"] = ["utc_offset"],
        ["card_events"] = ["utc_offset"],
        ["quiz_answers"] = ["utc_offset"],
        ["scenario_events"] = ["correct", "utc_offset"],
        ["project_events"] = ["correct", "utc_offset"],
        ["ap1_events"] = ["correct", "utc_offset"],
        ["trainer_aufgaben"] = ["utc_offset"],
        ["pruefungen"] = ["utc_offset"],
        ["spiel_ereignisse"] = ["lauf", "utc_offset"],
        ["spiel_bestenliste"] = ["utc_offset"],
    };

    private static readonly string[] Ereignistabellen =
        ["test_results", "card_events", "quiz_answers", "scenario_events", "project_events", "ap1_events", "trainer_aufgaben", "pruefungen"];

    private static string[] SpaltenVon(string tabelle) => Tabellen.First(t => t.Tabelle == tabelle).Spalten;

    // -- Lesen ---------------------------------------------------------------

    /// <summary>Datei lesen und pruefen (Sicherung oder Abgleich-Datei) bzw. alte Datenbank lesen. Schreibt nichts.</summary>
    public static Austausch Lesen(UebernahmeQuelle quelle, string pfad) => (quelle switch
    {
        UebernahmeQuelle.Sicherung => LeseSicherung(LeseDatei(pfad, UebernahmeFehler.KeineSicherung)),
        UebernahmeQuelle.AbgleichDatei => LeseAbgleichDatei(LeseDatei(pfad, UebernahmeFehler.AbgleichDateiKaputt)),
        _ => LeseAlteDatenbank(pfad),
    }).MitPfad(pfad);

    private static byte[] LeseDatei(string pfad, UebernahmeFehler fehler)
    {
        try
        {
            return File.ReadAllBytes(pfad);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new UebernahmeAusnahme(fehler);
        }
    }

    /// <summary>gzip entpacken, streng als UTF-8 lesen (BOM wie Python ein Fehler), JSON lesen; null bei Fehler.</summary>
    private static JsonDocument? Entpacken(byte[] roh)
    {
        try
        {
            using var eingabe = new MemoryStream(roh);
            using var gz = new GZipStream(eingabe, CompressionMode.Decompress);
            using var ausgabe = new MemoryStream();
            gz.CopyTo(ausgabe);
            var bytes = ausgabe.ToArray();
            var text = new UTF8Encoding(false, true).GetString(bytes);
            if (text.StartsWith('﻿'))
            {
                return null;
            }
            return JsonDocument.Parse(bytes);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or DecoderFallbackException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>int(x) wie Python fuer einen JSON-Wert; null = TypeError/ValueError.</summary>
    private static long? PyInt(JsonElement wert)
    {
        switch (wert.ValueKind)
        {
            case JsonValueKind.True:
                return 1;
            case JsonValueKind.False:
                return 0;
            case JsonValueKind.String:
                return Einstellungen.PythonIntText(wert.GetString()!);
            case JsonValueKind.Number:
                var roh = wert.GetRawText();
                if (roh.IndexOfAny(['.', 'e', 'E']) < 0)
                {
                    return long.TryParse(roh, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ganz)
                        ? ganz : roh.StartsWith('-') ? long.MinValue : long.MaxValue;
                }
                var zahl = double.Parse(roh, NumberStyles.Float, CultureInfo.InvariantCulture);
                return double.IsFinite(zahl) ? (long)Math.Clamp(Math.Truncate(zahl), long.MinValue, long.MaxValue) : null;
            default:
                return null;
        }
    }

    /// <summary>Zellwert wie Pythons json.loads: Text, ganze Zahl, Kommazahl, true/false als 1/0, null.</summary>
    private static object? Zelle(JsonElement wert)
    {
        switch (wert.ValueKind)
        {
            case JsonValueKind.String:
                return wert.GetString();
            case JsonValueKind.True:
                return 1L;
            case JsonValueKind.False:
                return 0L;
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Number:
                var roh = wert.GetRawText();
                if (roh.IndexOfAny(['.', 'e', 'E']) >= 0)
                {
                    return double.Parse(roh, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                // Python kennt beliebig grosse ganze Zahlen, SQLite nicht: dann bricht Python beim Schreiben ab
                return long.TryParse(roh, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ganz)
                    ? ganz : throw new FormatException("Zahl zu gross");
            default:
                throw new FormatException("kein Zellwert");
        }
    }

    private static bool IstZelle(JsonElement wert) => wert.ValueKind is JsonValueKind.String or JsonValueKind.Number
        or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;

    /// <summary>Typpruefung wie der erste Teil von _well_formed (Spalten, Marken, Zeilen der bekannten Tabellen).</summary>
    private static bool TypenStimmen(JsonElement daten)
    {
        if (daten.TryGetProperty("columns", out var spalten) && spalten.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (daten.TryGetProperty("markers", out var marken) && marken.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (spalten.ValueKind == JsonValueKind.Object && !spalten.EnumerateObject().All(s =>
                s.Value.ValueKind == JsonValueKind.Array && s.Value.EnumerateArray().All(n => n.ValueKind == JsonValueKind.String)))
        {
            return false;
        }
        if (marken.ValueKind == JsonValueKind.Object && !marken.EnumerateObject().All(m =>
                m.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null))
        {
            return false;
        }
        if (!daten.TryGetProperty("tables", out var tabellen) || tabellen.ValueKind != JsonValueKind.Object)
        {
            return true;
        }
        foreach (var (tabelle, _) in Tabellen)
        {
            if (!tabellen.TryGetProperty(tabelle, out var zeilen))
            {
                continue;
            }
            if (zeilen.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            foreach (var zeile in zeilen.EnumerateArray())
            {
                if (zeile.ValueKind != JsonValueKind.Array || !zeile.EnumerateArray().All(IstZelle))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Austauschformat (geprueftes JSON-Objekt) in das Modell uebertragen.</summary>
    private static Austausch Modell(JsonElement daten, UebernahmeQuelle quelle, string? appVersion = null, string? erstellt = null)
    {
        var marken = new Dictionary<string, object?>();
        if (daten.TryGetProperty("markers", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in m.EnumerateObject())
            {
                marken[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
            }
        }
        Dictionary<string, object?>? aufhebungen = null;
        if (daten.TryGetProperty(Loeschmarken.AufhebungFeld, out var a) && a.ValueKind == JsonValueKind.Object)
        {
            aufhebungen = [];
            foreach (var p in a.EnumerateObject())
            {
                // _lift_pair nimmt nur Text (JSON) oder Liste, alles andere ist keine Aufhebung
                aufhebungen[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Array => JsonNode.Parse(p.Value.GetRawText()),
                    _ => null,
                };
            }
        }
        var spalten = new Dictionary<string, IReadOnlyList<string>>();
        if (daten.TryGetProperty("columns", out var s) && s.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in s.EnumerateObject())
            {
                var namen = p.Value.EnumerateArray().Select(n => n.GetString()!).ToList();
                if (namen.Count > 0)
                {
                    spalten[p.Name] = namen;
                }
            }
        }
        var tabellen = new Dictionary<string, IReadOnlyList<object?[]>>();
        if (daten.TryGetProperty("tables", out var t) && t.ValueKind == JsonValueKind.Object)
        {
            foreach (var (tabelle, _) in Tabellen)
            {
                if (t.TryGetProperty(tabelle, out var zeilen) && zeilen.ValueKind == JsonValueKind.Array)
                {
                    tabellen[tabelle] = zeilen.EnumerateArray().Select(z => z.EnumerateArray().Select(Zelle).ToArray()).ToList();
                }
            }
        }
        return new Austausch
        {
            Quelle = quelle,
            MitSchreiber = daten.TryGetProperty(Schreiber, out var w) && w.ValueKind != JsonValueKind.Null,
            Marken = marken,
            Aufhebungen = aufhebungen,
            Spalten = spalten,
            Tabellen = tabellen,
            AppVersion = appVersion,
            Erstellt = erstellt,
        };
    }

    /// <summary>
    /// read_backup: Reihenfolge wie Python (Art, Version vor Pruefsumme, dann Typen, dann Probelauf).
    /// Die Pruefsumme rechnet AlterLernstand.PruefsummeStimmt (unveraendert).
    /// </summary>
    public static Austausch LeseSicherung(byte[] roh)
    {
        using var dokument = Entpacken(roh) ?? throw new UebernahmeAusnahme(UebernahmeFehler.KeineSicherung);
        var wurzel = dokument.RootElement;
        if (wurzel.ValueKind != JsonValueKind.Object || !wurzel.TryGetProperty("art", out var art)
            || art.ValueKind != JsonValueKind.String || art.GetString() != SicherungArt)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.KeineSicherung);
        }
        var hatDaten = wurzel.TryGetProperty("daten", out var daten);
        long? version = wurzel.TryGetProperty("formatversion", out var fv) ? PyInt(fv) : 0;
        long? format = hatDaten && daten.ValueKind == JsonValueKind.Object
            ? daten.TryGetProperty("format", out var f) ? PyInt(f) : 0
            : 0;
        if (version is null || format is null)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.KeineSicherung);
        }
        // Erst die Version: eine neuere App koennte die Pruefsumme anders bilden
        if (version > SicherungFormatversion || format > AustauschFormat)
        {
            var app = wurzel.TryGetProperty("app_version", out var av) && av.ValueKind == JsonValueKind.String && av.GetString()!.Length > 0
                ? av.GetString() : "?";
            throw new UebernahmeAusnahme(UebernahmeFehler.ZuNeu, app);
        }
        bool stimmt;
        try
        {
            stimmt = AlterLernstand.PruefsummeStimmt(dokument);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException)
        {
            stimmt = false;    // daten/einstellungen fehlen oder pruefsumme ist kein Text
        }
        if (!stimmt)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.PruefsummeFalsch);
        }
        if (daten.ValueKind != JsonValueKind.Object || !daten.TryGetProperty("tables", out var tabellen)
            || tabellen.ValueKind != JsonValueKind.Object
            || !wurzel.TryGetProperty("einstellungen", out var einstellungen) || einstellungen.ValueKind != JsonValueKind.Object)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.KeineSicherung);
        }
        if (!TypenStimmen(daten))
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.InhaltKaputt);
        }
        Austausch modell;
        try
        {
            modell = Modell(daten, UebernahmeQuelle.Sicherung,
                wurzel.TryGetProperty("app_version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null,
                wurzel.TryGetProperty("erstellt", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null);
        }
        catch (FormatException)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.InhaltKaputt);
        }
        // Probelauf wie _well_formed: _slot_state rechnet int(platz or 0) fuer jede Platz-Zeile
        foreach (var zeile in Datensaetze(modell, "spiel_plaetze"))
        {
            if (PlatzNummer(zeile["platz"]) is null)
            {
                throw new UebernahmeAusnahme(UebernahmeFehler.InhaltKaputt);
            }
        }
        return modell;
    }

    /// <summary>int(platz or 0) wie Python; null = ValueError.</summary>
    internal static long? PlatzNummer(object? wert) => !Loeschmarken.Wahr(wert) ? 0 : wert switch
    {
        long n => n,
        double d => double.IsFinite(d) ? (long)Math.Truncate(d) : null,
        string s => Einstellungen.PythonIntText(s),
        _ => null,
    };

    /// <summary>Abgleich-Datei lernstand.json.gz: Austauschformat 2 ohne Huelle und Pruefsumme.</summary>
    public static Austausch LeseAbgleichDatei(byte[] roh)
    {
        using var dokument = Entpacken(roh) ?? throw new UebernahmeAusnahme(UebernahmeFehler.AbgleichDateiKaputt);
        var daten = dokument.RootElement;
        // Python: dict und format eine ganze Zahl (true/false zaehlen in Python als Zahl)
        if (daten.ValueKind != JsonValueKind.Object)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.AbgleichDateiKaputt);
        }
        long format = 0;
        if (daten.TryGetProperty("format", out var f))
        {
            if (f.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                format = f.ValueKind == JsonValueKind.True ? 1 : 0;
            }
            else if (f.ValueKind != JsonValueKind.Number || f.GetRawText().IndexOfAny(['.', 'e', 'E']) >= 0)
            {
                throw new UebernahmeAusnahme(UebernahmeFehler.AbgleichDateiKaputt);
            }
            else
            {
                format = PyInt(f)!.Value;
            }
        }
        if (format > AustauschFormat)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.ZuNeu);
        }
        // Strenger als Python (dort bricht der Abgleich spaeter mit einem Programmfehler ab):
        // gleiche Typpruefung wie bei Sicherungen, bevor etwas geschrieben wird
        if ((daten.TryGetProperty("tables", out var t) && t.ValueKind != JsonValueKind.Object) || !TypenStimmen(daten))
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.AbgleichDateiKaputt);
        }
        try
        {
            return Modell(daten, UebernahmeQuelle.AbgleichDatei);
        }
        catch (FormatException)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.AbgleichDateiKaputt);
        }
    }

    /// <summary>
    /// Alte Python-Datenbank wie export_local lesen: nur lesend geoeffnet (query_only), je Tabelle
    /// SELECT uid, Spalten ORDER BY timestamp, id; Marken und Aufhebungen aus sync_meta.
    /// Spalten, die Python beim Oeffnen nachtragen wuerde (optional), gelten als leer; Zeilen
    /// ohne Kennung (uid) werden nicht uebernommen (Python 0.63.2 hat sie immer).
    /// </summary>
    public static Austausch LeseAlteDatenbank(string pfad)
    {
        if (!File.Exists(pfad))
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.DatenbankNichtLesbar);
        }
        try
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = pfad,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = Lernstand.Wartezeit,
            }.ToString());
            db.Open();
            Ausfuehren(db, null, "PRAGMA query_only = ON");
            using var lesen = db.BeginTransaction(deferred: true);   // ein Lesestand fuer alles
            var vorhanden = Abfrage(db, lesen, "SELECT name FROM sqlite_master WHERE type = 'table'").Select(z => (string)z[0]!).ToHashSet();
            var tabellen = new Dictionary<string, IReadOnlyList<object?[]>>();
            foreach (var (tabelle, spalten) in Tabellen)
            {
                if (!vorhanden.Contains(tabelle))
                {
                    tabellen[tabelle] = [];
                    continue;
                }
                var da = Abfrage(db, lesen, $"PRAGMA table_info({tabelle})").Select(z => (string)z[1]!).ToHashSet();
                var optional = Optional.GetValueOrDefault(tabelle, []);
                if (spalten.Any(s => !da.Contains(s) && !optional.Contains(s)) || !da.Contains("id") || !da.Contains("timestamp") || !da.Contains("uid"))
                {
                    throw new UebernahmeAusnahme(UebernahmeFehler.DatenbankNichtLesbar);
                }
                var auswahl = string.Join(", ", new[] { "uid" }.Concat(spalten).Select(s => da.Contains(s) ? s : "NULL"));
                tabellen[tabelle] = Abfrage(db, lesen, $"SELECT {auswahl} FROM {tabelle} ORDER BY timestamp, id");
            }
            var meta = vorhanden.Contains("sync_meta")
                ? Abfrage(db, lesen, "SELECT key, value FROM sync_meta").Where(z => z[0] is string)
                    .GroupBy(z => (string)z[0]!).ToDictionary(g => g.Key, g => g.Last()[1])
                : [];
            var marken = Loeschmarken.Schluessel.Where(k => Loeschmarken.Wahr(meta.GetValueOrDefault(k)))
                .ToDictionary(k => k, k => meta[k]);
            var aufhebungen = Loeschmarken.AufhebungenZusammen(
                Loeschmarken.Schluessel.ToDictionary(k => k, k => meta.GetValueOrDefault(Loeschmarken.AufhebungPraefix + k)), null, true, null);
            return new Austausch
            {
                Quelle = UebernahmeQuelle.AlteDatenbank,
                MitSchreiber = true,    // export_local schreibt immer "schreiber"
                Marken = marken,
                Aufhebungen = aufhebungen.Count > 0 ? aufhebungen.ToDictionary(p => p.Key, p => (object?)p.Value) : null,
                Spalten = Tabellen.ToDictionary(t => t.Tabelle, t => (IReadOnlyList<string>)t.Spalten),   // wie export_local
                Tabellen = tabellen,
            };
        }
        catch (SqliteException)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.DatenbankNichtLesbar);
        }
    }

    /// <summary>_records: Zeilen einer Tabelle als Datensaetze (mit uid), unvollstaendige fallen weg.</summary>
    internal static List<Dictionary<string, object?>> Datensaetze(Austausch daten, string tabelle)
    {
        var spalten = SpaltenVon(tabelle);
        var ergebnis = new List<Dictionary<string, object?>>();
        foreach (var zeile in daten.Tabellen.GetValueOrDefault(tabelle, []))
        {
            var satz = Satz(daten, tabelle, zeile);
            if (satz is not null)
            {
                ergebnis.Add(satz);
            }
        }
        return ergebnis;
    }

    /// <summary>dict(zip(namen, zeile)) mit leeren optionalen Spalten; null = ohne uid oder unvollstaendig.</summary>
    private static Dictionary<string, object?>? Satz(Austausch daten, string tabelle, object?[] zeile)
    {
        var spalten = SpaltenVon(tabelle);
        var namen = new[] { "uid" }.Concat(daten.Spalten.GetValueOrDefault(tabelle) ?? spalten).ToList();
        var satz = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < Math.Min(namen.Count, zeile.Length); i++)
        {
            satz[namen[i]] = zeile[i];
        }
        foreach (var spalte in Optional.GetValueOrDefault(tabelle, []))
        {
            satz.TryAdd(spalte, null);
        }
        return Loeschmarken.Wahr(satz.GetValueOrDefault("uid")) && spalten.All(satz.ContainsKey) ? satz : null;
    }

    // -- Kleine SQL-Helfer ---------------------------------------------------

    private static SqliteCommand Befehl(SqliteConnection db, SqliteTransaction? tx, string sql, object?[] werte)
    {
        var befehl = db.CreateCommand();
        befehl.Transaction = tx;
        befehl.CommandText = sql;
        for (var i = 0; i < werte.Length; i++)
        {
            befehl.Parameters.AddWithValue("$" + i.ToString(CultureInfo.InvariantCulture), werte[i] ?? DBNull.Value);
        }
        return befehl;
    }

    private static int Ausfuehren(SqliteConnection db, SqliteTransaction? tx, string sql, params object?[] werte)
    {
        using var befehl = Befehl(db, tx, sql, werte);
        return befehl.ExecuteNonQuery();
    }

    private static List<object?[]> Abfrage(SqliteConnection db, SqliteTransaction? tx, string sql, params object?[] werte)
    {
        using var befehl = Befehl(db, tx, sql, werte);
        using var leser = befehl.ExecuteReader();
        var zeilen = new List<object?[]>();
        while (leser.Read())
        {
            var zeile = new object?[leser.FieldCount];
            for (var i = 0; i < leser.FieldCount; i++)
            {
                zeile[i] = leser.IsDBNull(i) ? null : leser.GetValue(i);
            }
            zeilen.Add(zeile);
        }
        return zeilen;
    }

    internal static void Protokoll(string pfad, string text)
    {
        try
        {
            Fehlerprotokoll.Schreiben(text, Path.Combine(Path.GetDirectoryName(pfad)!, Fehlerprotokoll.DateiName));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
