using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Daten;

/// <summary>Warum keine Ruecksicherung entstand (Texte in Texte.RuecksicherungTexte, Wortlaut P2-11).</summary>
public enum RuecksicherungFehler
{
    /// <summary>T3: Der Lernstand kann nicht gelesen werden (beschaedigt, nicht erreichbar, Lesefehler).</summary>
    NichtLesbar,

    /// <summary>T4 „Kein Speicherplatz mehr“.</summary>
    KeinPlatz,

    /// <summary>T4 „Keine Berechtigung für diesen Ordner“.</summary>
    KeineBerechtigung,

    /// <summary>T4 „Unbekannter Fehler“ (auch: Pruefung der geschriebenen Datei gescheitert).</summary>
    Unbekannt,
}

public sealed class RuecksicherungAusnahme(RuecksicherungFehler fehler)
    : Exception("Ruecksicherung: " + fehler.ToString())
{
    public RuecksicherungFehler Fehler { get; } = fehler;
}

/// <summary>
/// Phase 2, L9: „Sicherung für die alte App erstellen“. Schreibt eine .fisisicherung genau wie
/// fisi_sicherung.create_backup der Python-Linie v0.63.2: gzip-JSON mit Huelle (art,
/// formatversion 1, app_version, erstellt, geraet, daten, einstellungen, zusammenfassung,
/// pruefsumme), daten = fisi_sync.export_local (Austauschformat 2), einstellungen nur das
/// Tagesziel. Python fuehrt sie mit „Sicherung zusammenführen“ zusammen (CI-Beleg R16).
/// Der Lernstand wird nur gelesen, in einer Lesetransaktion (alle Tabellen derselbe Stand,
/// Weisung P2-11 A1). AlterLernstand.cs und SicherungsPruefung.cs bleiben unberuehrt.
/// </summary>
public static class Ruecksicherung
{
    public const string Endung = ".fisisicherung";         // BACKUP_EXT
    public const string Art = "FISI-Sicherung";            // KIND
    public const int Formatversion = 1;                    // FORMAT_VERSION
    public const int Austauschformat = 2;                  // fisi_sync.FORMAT
    public const string SchreiberWert = "0.62.4";          // fisi_sync.WRITER (fest, waechst nicht mit, P2-11 A3)
    public const string GekapptFeld = "gekappt";           // CLAMP_KEY
    public const string GekapptPraefix = "gekappt:";       // CLAMP_PREFIX
    public const string AltDurchgang = "alt";              // LEGACY_RUN

    /// <summary>Nur fuer Tests: wird beim Lesen nach jeder Tabelle aufgerufen (Kontrollfehler „Schreiben waehrend des Exports“).</summary>
    internal static Action<string>? NachTabelle { get; set; }

    /// <summary>Tagesziel-Schluessel in Python-Reihenfolge (fisi_lernen.GOAL_SETTINGS).</summary>
    private static readonly string[] Zielschluessel = ["ziel_an", "ziel_anzahl", "serie_an", "erinnerung_an", "erinnerung_zeit"];

    private static readonly string[] Ereignistabellen =
        ["test_results", "card_events", "quiz_answers", "scenario_events", "project_events", "ap1_events", "trainer_aufgaben", "pruefungen"];

    /// <summary>default_name: Lernplattform-Sicherung_2026-10-01_1423.fisisicherung (Ortszeit).</summary>
    public static string DateiName(DateTime ortszeit) =>
        "Lernplattform-Sicherung_" + ortszeit.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture) + Endung;

    /// <summary>
    /// export_local: alle Eintraege im Austauschformat, in der offenen Lesetransaktion.
    /// Schluessel und Reihenfolge wie Python: format, schreiber, gekappt, columns, markers,
    /// tables, dann mischbetrieb und aufgehoben nur, wenn vorhanden.
    /// </summary>
    internal static PyObjekt Austausch(SqliteConnection db, SqliteTransaction? tx)
    {
        var tabellen = new PyObjekt();
        foreach (var (tabelle, spalten) in Uebernahme.Tabellen)
        {
            using var befehl = db.CreateCommand();
            befehl.Transaction = tx;
            befehl.CommandText = $"SELECT uid, {string.Join(", ", spalten)} FROM {tabelle} ORDER BY timestamp, id";
            using var leser = befehl.ExecuteReader();
            var zeilen = new List<object?>();
            while (leser.Read())
            {
                var zeile = new object?[leser.FieldCount];
                for (var i = 0; i < leser.FieldCount; i++)
                {
                    zeile[i] = leser.IsDBNull(i) ? null : leser.GetValue(i);
                }
                zeilen.Add(zeile);
            }
            tabellen.Add(tabelle, zeilen);
            NachTabelle?.Invoke(tabelle);
        }
        // dict(SELECT key, value FROM sync_meta): Reihenfolge der Zeilen, spaeterer Schluessel gewinnt
        var meta = new PyObjekt();
        using (var befehl = db.CreateCommand())
        {
            befehl.Transaction = tx;
            befehl.CommandText = "SELECT key, value FROM sync_meta";
            using var leser = befehl.ExecuteReader();
            while (leser.Read())
            {
                var schluessel = leser.IsDBNull(0) ? null : leser.GetValue(0);
                var wert = leser.IsDBNull(1) ? null : leser.GetValue(1);
                if (schluessel is not string text)
                {
                    continue;     // Python: Zahlen als Schluessel passen nie zu den Markennamen
                }
                var stelle = meta.FindIndex(p => p.Key == text);
                if (stelle >= 0)
                {
                    meta[stelle] = new KeyValuePair<string, object?>(text, wert);
                }
                else
                {
                    meta.Add(text, wert);
                }
            }
        }
        var marken = new PyObjekt();
        foreach (var (schluessel, wert) in meta)
        {
            if (Loeschmarken.Schluessel.Contains(schluessel) && Loeschmarken.Wahr(wert))
            {
                marken.Add(schluessel, wert);
            }
        }
        var gekappt = new PyObjekt();
        foreach (var k in Loeschmarken.Schluessel)
        {
            if (Kappung(meta[GekapptPraefix + k]) is { } paar)
            {
                gekappt.Add(k, paar);
            }
        }
        var spalten2 = new PyObjekt();
        foreach (var (tabelle, spalten) in Uebernahme.Tabellen)
        {
            spalten2.Add(tabelle, spalten.ToList());
        }
        var ergebnis = new PyObjekt
        {
            { "format", (long)Austauschformat },
            { Uebernahme.Schreiber, SchreiberWert },
            { GekapptFeld, gekappt },
            { "columns", spalten2 },
            { "markers", marken },
            { "tables", tabellen },
        };
        var misch = meta[Loeschmarken.MischbetriebSchluessel];
        if (Loeschmarken.Wahr(misch))
        {
            ergebnis.Add(Loeschmarken.MischbetriebSchluessel, misch);
        }
        var aufhebungen = Loeschmarken.AufhebungenZusammen(
            Loeschmarken.Schluessel.ToDictionary(k => k, k => meta[Loeschmarken.AufhebungPraefix + k]), null, true, null);
        if (aufhebungen.Count > 0)
        {
            var feld = new PyObjekt();
            foreach (var k in Loeschmarken.Schluessel)
            {
                if (aufhebungen.TryGetValue(k, out var paar))
                {
                    feld.Add(k, paar.ToList());
                }
            }
            ergebnis.Add(Loeschmarken.AufhebungFeld, feld);
        }
        return ergebnis;
    }

    /// <summary>_clamp_pair: [alt, neu] als JSON-Text oder Liste, beide gueltige Marken; sonst null.</summary>
    internal static List<object?>? Kappung(object? wert)
    {
        if (wert is string text)
        {
            try
            {
                wert = JsonNode.Parse(text);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
        if (wert is JsonArray liste && liste.Count == 2
            && liste.All(e => e is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String
                && Loeschmarken.Lesen((string)v!) is not null))
        {
            return [(string)liste[0]!, (string)liste[1]!];
        }
        return null;
    }

    /// <summary>Lernstand in einer Lesetransaktion lesen (export_local). Nicht lesbar: NichtLesbar.</summary>
    public static PyObjekt Lesen(Lernstand lernstand)
    {
        if (lernstand.NurLesenGrund is "kaputt" or "nicht_erreichbar" || !File.Exists(lernstand.Pfad))
        {
            throw new RuecksicherungAusnahme(RuecksicherungFehler.NichtLesbar);
        }
        using var sperre = Vorgaenge.Sperren();
        try
        {
            using var db = lernstand.Verbindung();
            using var tx = db.BeginTransaction(deferred: true);
            var daten = Austausch(db, tx);
            tx.Commit();
            return daten;
        }
        catch (SqliteException e)
        {
            Uebernahme.Protokoll(lernstand.Pfad, "Ruecksicherung: Lernstand nicht lesbar, SQLite-Fehler "
                + e.SqliteErrorCode.ToString(CultureInfo.InvariantCulture));
            throw new RuecksicherungAusnahme(RuecksicherungFehler.NichtLesbar);
        }
    }

    /// <summary>_goal_values: alle Tagesziel-Schluessel mit Vorgaben und gueltigen Werten (goal_settings).</summary>
    public static PyObjekt Tagesziel(JsonObject einstellungen)
    {
        var ziel = Einstellungen.Ziel(einstellungen);
        return new PyObjekt
        {
            { Zielschluessel[0], ziel.ZielAn },
            { Zielschluessel[1], (long)ziel.ZielAnzahl },
            { Zielschluessel[2], ziel.SerieAn },
            { Zielschluessel[3], ziel.ErinnerungAn },
            { Zielschluessel[4], ziel.ErinnerungZeit },
        };
    }

    /// <summary>_checksum: SHA-256 ueber {"daten", "einstellungen"} mit sort_keys, kompakt, UTF-8.</summary>
    public static string Pruefsumme(PyObjekt daten, PyObjekt einstellungen)
    {
        var text = PythonJson.Text(new PyObjekt { { "daten", daten }, { "einstellungen", einstellungen } }, sortiert: true);
        return Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(text)));
    }

    /// <summary>_summary_counts: Lerneintraege, Spielstaende, Abzeichen, Projekte.</summary>
    public static PyObjekt Zusammenfassung(PyObjekt daten)
    {
        var tabellen = (PyObjekt)daten["tables"]!;
        long Anzahl(string t) => tabellen[t] is List<object?> z ? z.Count : 0;
        var ereignisse = Ereignistabellen.Sum(Anzahl);
        var plaetze = Datensaetze(daten, "spiel_plaetze");
        var geloescht = plaetze.Where(r => Equals(r["aktion"], "geloescht")).Select(r => Schluessel(r["lauf"])).ToHashSet();
        var laeufe = plaetze.Where(r => Equals(r["aktion"], "angelegt") && Loeschmarken.Wahr(r["lauf"])).Select(r => Schluessel(r["lauf"])).ToHashSet();
        laeufe.UnionWith(Datensaetze(daten, "spiel_ereignisse").Select(r => Schluessel(Loeschmarken.Wahr(r["lauf"]) ? r["lauf"] : AltDurchgang)));
        laeufe.ExceptWith(geloescht);
        var abzeichen = Datensaetze(daten, "spiel_bestenliste").Count(r => Equals(r["art"], "erfolg"));
        var marken = new Dictionary<object, ((string, string) Wann, object? Wert)>();
        var projekte = new HashSet<object>();
        foreach (var r in Datensaetze(daten, "abschlussprojekt"))
        {
            if (!Loeschmarken.Wahr(r["projekt"]))
            {
                continue;
            }
            var projekt = Schluessel(r["projekt"]);
            projekte.Add(projekt);
            if (Equals(r["feld"], "_geloescht"))
            {
                var wann = (Dashboard.PyText.Str(r["timestamp"]), Dashboard.PyText.Str(r["uid"]));
                if (!marken.TryGetValue(projekt, out var alt) || Vergleich(wann, alt.Wann) > 0)
                {
                    marken[projekt] = (wann, r["wert"]);
                }
            }
        }
        projekte.ExceptWith(marken.Where(m => Dashboard.PyText.Str(m.Value.Wert) == "1").Select(m => m.Key));
        return new PyObjekt
        {
            { "lerneintraege", ereignisse },
            { "spielstaende", (long)laeufe.Count },
            { "abzeichen", (long)abzeichen },
            { "projekte", (long)projekte.Count },
        };
    }

    private static int Vergleich((string A, string B) x, (string A, string B) y)
    {
        var erst = Loeschmarken.Vergleich(x.A, y.A);
        return erst != 0 ? erst : Loeschmarken.Vergleich(x.B, y.B);
    }

    /// <summary>Gleichheit wie Python fuer SQLite-Werte (1 == 1.0, Text und Zahl verschieden).</summary>
    private static object Schluessel(object? wert) => wert switch
    {
        null => "\0null",
        double d when d == Math.Floor(d) && Math.Abs(d) < 9e18 => (long)d,
        _ => wert,
    };

    /// <summary>_records fuer die eigenen Daten (Spalten wie SYNC_TABLES, alle vorhanden).</summary>
    private static List<Dictionary<string, object?>> Datensaetze(PyObjekt daten, string tabelle)
    {
        var spalten = Uebernahme.Tabellen.First(t => t.Tabelle == tabelle).Spalten;
        var zeilen = ((PyObjekt)daten["tables"]!)[tabelle] as List<object?> ?? [];
        var ergebnis = new List<Dictionary<string, object?>>();
        foreach (object?[] zeile in zeilen.Cast<object?[]>())
        {
            var satz = new Dictionary<string, object?> { ["uid"] = zeile[0] };
            for (var i = 0; i < spalten.Length; i++)
            {
                satz[spalten[i]] = zeile[i + 1];
            }
            if (Loeschmarken.Wahr(satz["uid"]))
            {
                ergebnis.Add(satz);
            }
        }
        return ergebnis;
    }

    /// <summary>create_backup: die ganze Huelle als Python-Objekt (Reihenfolge wie Python).</summary>
    public static PyObjekt Huelle(PyObjekt daten, PyObjekt einstellungen, string appVersion, DateTime erstellt, string geraet) => new()
    {
        { "art", Art },
        { "formatversion", (long)Formatversion },
        { "app_version", appVersion },
        { "erstellt", erstellt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) },
        { "geraet", geraet },
        { "daten", daten },
        { "einstellungen", einstellungen },
        { "zusammenfassung", Zusammenfassung(daten) },
        { "pruefsumme", Pruefsumme(daten, einstellungen) },
    };

    /// <summary>gzip.compress(json.dumps(huelle, ensure_ascii=False, separators=(",", ":")).encode("utf-8")).</summary>
    public static byte[] Packen(PyObjekt wert)
    {
        byte[] text;
        try
        {
            text = PythonJson.Utf8(PythonJson.Text(wert));
        }
        catch (Exception e) when (e is FormatException or System.Text.EncoderFallbackException)
        {
            throw new RuecksicherungAusnahme(RuecksicherungFehler.Unbekannt);
        }
        using var ausgabe = new MemoryStream();
        using (var gz = new GZipStream(ausgabe, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gz.Write(text);
        }
        return ausgabe.ToArray();
    }

    /// <summary>
    /// Die fertige Sicherungsdatei als Bytes: Lernstand lesen (NichtLesbar), Huelle bilden, packen
    /// und zur Probe wieder einlesen (gleiche Pruefung wie „Lernstand übernehmen“).
    /// </summary>
    public static byte[] Erstellen(Lernstand lernstand, JsonObject einstellungen, string appVersion, string geraet, DateTime erstellt)
    {
        var daten = Lesen(lernstand);
        var bytes = Packen(Huelle(daten, Tagesziel(einstellungen), appVersion, erstellt, geraet));
        Pruefen(bytes);
        return bytes;
    }

    /// <summary>Zur Probe einlesen wie read_backup (Pruefsumme nachgerechnet); sonst Unbekannt.</summary>
    public static void Pruefen(byte[] bytes)
    {
        try
        {
            Uebernahme.LeseSicherung(bytes);
        }
        catch (UebernahmeAusnahme)
        {
            throw new RuecksicherungAusnahme(RuecksicherungFehler.Unbekannt);
        }
    }

    /// <summary>
    /// save_file: in &lt;pfad&gt;.tmp im selben Ordner schreiben, auf die Platte bringen (fsync),
    /// zur Probe zurueck lesen und pruefen, dann an ihren Platz umbenennen. Scheitert etwas,
    /// wird die Hilfsdatei entfernt und eine vorhandene Datei bleibt unveraendert.
    /// </summary>
    public static void Speichern(string pfad, byte[] bytes)
    {
        var temp = pfad + ".tmp";
        try
        {
            using (var datei = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                datei.Write(bytes);
                datei.Flush(flushToDisk: true);
            }
            if (!File.ReadAllBytes(temp).AsSpan().SequenceEqual(bytes))
            {
                throw new IOException("Probe der geschriebenen Datei gescheitert");
            }
            Pruefen(File.ReadAllBytes(temp));
            File.Move(temp, pfad, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or RuecksicherungAusnahme or NotSupportedException or ArgumentException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception f) when (f is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
            throw e as RuecksicherungAusnahme ?? new RuecksicherungAusnahme(Fehlerart(e));
        }
    }

    /// <summary>
    /// Android (Weisung P2-11 A2): zuerst atomar als &lt;arbeitsdatei&gt; im App-Ordner schreiben und
    /// pruefen (<see cref="Speichern"/>), dann an den gewaehlten Ort kopieren (Strom aus dem
    /// Speichern-Dialog). Scheitert ein Schritt: RuecksicherungAusnahme mit Grund fuer T4. Die
    /// Arbeitsdatei wird danach immer entfernt; sie ist nur eine Zwischenstufe.
    /// </summary>
    public static async Task SpeichernUeberAsync(string arbeitsdatei, byte[] bytes, Func<Task<Stream>> oeffnen, CancellationToken abbruch = default)
    {
        try
        {
            Speichern(arbeitsdatei, bytes);
            await using var ziel = await oeffnen();
            await using (var quelle = File.OpenRead(arbeitsdatei))
            {
                await quelle.CopyToAsync(ziel, abbruch);
            }
            await ziel.FlushAsync(abbruch);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            throw new RuecksicherungAusnahme(Fehlerart(e));
        }
        finally
        {
            try
            {
                File.Delete(arbeitsdatei);
            }
            catch (Exception f) when (f is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        }
    }

    /// <summary>Kurzer Grund fuer T4 (nie ein Pfad): Platz, Berechtigung oder unbekannt.</summary>
    public static RuecksicherungFehler Fehlerart(Exception e)
    {
        if (e is UnauthorizedAccessException)
        {
            return RuecksicherungFehler.KeineBerechtigung;
        }
        if (e is IOException)
        {
            var code = e.HResult & 0xFFFF;
            // Windows: ERROR_DISK_FULL 112, ERROR_HANDLE_DISK_FULL 39; Unix: ENOSPC 28, EDQUOT 122 (Linux) bzw. 69 (macOS)
            if ((OperatingSystem.IsWindows() && code is 112 or 39) || (!OperatingSystem.IsWindows() && e.HResult is 28 or 122 or 69))
            {
                return RuecksicherungFehler.KeinPlatz;
            }
            // Unix: EACCES 13, EPERM 1, EROFS 30; Windows: ERROR_ACCESS_DENIED 5, ERROR_WRITE_PROTECT 19
            if ((OperatingSystem.IsWindows() && code is 5 or 19) || (!OperatingSystem.IsWindows() && e.HResult is 13 or 1 or 30))
            {
                return RuecksicherungFehler.KeineBerechtigung;
            }
        }
        return RuecksicherungFehler.Unbekannt;
    }
}
