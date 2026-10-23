using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Daten;

/// <summary>Warum "Alles ersetzen" nicht stattfand (der Lernstand ist dann unveraendert).</summary>
public enum WiederherstellungFehler
{
    /// <summary>MSG_READ_ONLY: der Lernstand wird nicht gespeichert (Lesefehler, Schreibschutz).</summary>
    NurLesen,

    /// <summary>"Die Sicherheitskopie ließ sich nicht speichern" (auch: Probe der Kopie gescheitert).</summary>
    KopieGescheitert,

    /// <summary>"Die Sicherung ließ sich nicht einspielen" (SQLite oder Hilfsdatei), alles zurueckgenommen.</summary>
    EinspielenGescheitert,
}

public sealed class WiederherstellungAusnahme(WiederherstellungFehler fehler, RuecksicherungFehler grund = RuecksicherungFehler.Unbekannt)
    : Exception("Wiederherstellung: " + fehler.ToString())
{
    public WiederherstellungFehler Fehler { get; } = fehler;

    /// <summary>Bei KopieGescheitert: Platz, Berechtigung oder unbekannt (nie ein Pfad).</summary>
    public RuecksicherungFehler Grund { get; } = grund;
}

/// <summary>
/// Eine gepruefte Sicherungsdatei (.fisisicherung) mit allem, was Vorschau und "Alles ersetzen"
/// brauchen: der Lernstand (wie Uebernahme.LeseSicherung), aus der Huelle das Tagesziel und
/// die Datei selbst (fuer die Zeilen der Vorschau).
/// </summary>
public sealed record Sicherungsinhalt(Austausch Daten, JsonObject Einstellungen, byte[] Roh)
{
    /// <summary>backup_summary["zeilen"]: Zeilen der Vorschau (UebernahmeAblauf.SicherungsZeilen, G22).</summary>
    public IReadOnlyList<string> Zeilen => UebernahmeAblauf.SicherungsZeilen(Roh);

    /// <summary>backup_summary["erstellt"]: "TT.MM.JJJJ, HH:MM Uhr" (aus der ersten Zeile "Erstellt: …").</summary>
    public string ErstelltText => Zeilen[0]["Erstellt: ".Length..];

    /// <summary>read_backup: wirft UebernahmeAusnahme wie "Lernstand übernehmen" (gleiche Pruefung).</summary>
    public static Sicherungsinhalt Lesen(byte[] roh)
    {
        var daten = Uebernahme.LeseSicherung(roh);
        // Die Huelle ist geprueft (gzip, JSON, art, Pruefsumme, einstellungen ein Objekt)
        using var eingabe = new MemoryStream(roh);
        using var gz = new GZipStream(eingabe, CompressionMode.Decompress);
        var huelle = (JsonObject)JsonNode.Parse(gz)!;
        return new Sicherungsinhalt(daten, (JsonObject)huelle["einstellungen"]!.DeepClone(), roh);
    }
}

/// <summary>
/// Phase 5 BE2: "Alles ersetzen" wie fisi_sicherung.replace_all (Python v0.63.2). Der Lernstand
/// des Geraets wird durch die Sicherung ersetzt: dieselben Eintraege mit derselben uid, dieselben
/// Loeschmarken, dazu das Tagesziel. Vorher kommt der jetzige Stand als Sicherheitskopie
/// (.fisisicherung wie create_backup) in den Ordner "sicherungen" (D3, Python: neben die
/// Datenbank). Geraetebezogenes (Farben, Zugangsschluessel) bleibt. Alle Tabellen werden in
/// einer Buchung geleert und gefuellt; die Nummern (id) zaehlen weiter (AUTOINCREMENT).
/// </summary>
public static class Wiederherstellung
{
    public const string Praefix = "vor_wiederherstellung_";   // SAFETY_PREFIX

    /// <summary>EVENT_TABLES (Lern-Eintraege) und HISTORY_TABLES (Historie) wie fisi_core.</summary>
    public static readonly IReadOnlyList<string> Ereignistabellen =
        ["test_results", "card_events", "quiz_answers", "scenario_events", "project_events", "ap1_events", "trainer_aufgaben", "pruefungen"];

    public static readonly IReadOnlyList<string> Historientabellen = ["test_results", "pruefungen"];

    /// <summary>
    /// Nur fuer Tests (Absturzprobe, Kontrollfehler): "kopie" nach dem Speichern der Kopie, "vor-buchung",
    /// je Tabelle mitten in der Buchung, "vor-commit" nach dem Schreiben der Hilfsdatei.
    /// </summary>
    internal static Action<string>? Haltepunkt { get; set; }

    /// <summary>
    /// replace_all. Rueckgabe: Pfad der Sicherheitskopie. Ablauf: Pruefung "nur lesen", Sperre (wie
    /// Abgleich und Uebernahme), Sicherheitskopie (geschrieben, zurueckgelesen, geprueft), noch einmal
    /// "nur lesen", dann eine Buchung: Tabellen leeren und fuellen, Marken und Aufhebungen
    /// (replace_lifts), Tagesziel in die Hilfsdatei; commit; Hilfsdatei an ihren Platz.
    /// Scheitert die Kopie oder die Buchung, bleibt der Lernstand unveraendert.
    /// </summary>
    /// <param name="ordner">Vorgabe: Ordner "sicherungen" neben der Datenbank.</param>
    public static string Ersetzen(Lernstand ziel, Sicherungsinhalt sicherung, Einstellungen einstellungen, string appVersion, string geraet,
        string? ordner = null, Uhr? uhr = null)
    {
        uhr ??= Uhr.System;
        NurLesenPruefen(ziel);
        using var sperre = Vorgaenge.Sperren();
        ordner ??= Path.Combine(Path.GetDirectoryName(ziel.Pfad)!, Uebernahme.SicherungsOrdner);
        var kopie = KopieAnlegen(ziel, einstellungen, appVersion, geraet, ordner, uhr);
        Haltepunkt?.Invoke("vor-buchung");

        var daten = sicherung.Daten;
        var marken = daten.Marken.Where(m => Loeschmarken.Schluessel.Contains(m.Key) && Loeschmarken.Wahr(m.Value))
            .ToDictionary(m => m.Key, m => m.Value);
        var werte = einstellungen.Laden();
        var ziele = Ziele(sicherung.Einstellungen);
        foreach (var (schluessel, wert) in ziele)
        {
            werte[schluessel] = wert;
        }
        NurLesenPruefen(ziel);   // nach der Kopie und direkt vor der Buchung noch einmal (Python G-B)
        var hilfsdatei = false;
        try
        {
            using var db = ziel.Verbindung();
            using var tx = db.BeginTransaction();
            foreach (var (tabelle, spalten) in Uebernahme.Tabellen)
            {
                Ausfuehren(db, tx, "DELETE FROM " + tabelle);
                var sql = $"INSERT OR IGNORE INTO {tabelle} (uid, {string.Join(", ", spalten)}) VALUES ("
                    + string.Join(", ", Enumerable.Range(0, spalten.Length + 1).Select(i => "$" + i.ToString(CultureInfo.InvariantCulture))) + ")";
                foreach (var satz in Uebernahme.Datensaetze(daten, tabelle))
                {
                    Ausfuehren(db, tx, sql, [satz["uid"], .. spalten.Select(s => satz[s])]);
                }
                Haltepunkt?.Invoke(tabelle);
            }
            // Loeschungen, die die Sicherung nicht kennt, sind aufgehoben (auch auf den anderen Geraeten)
            var meta = Abfrage(db, tx, "SELECT key, value FROM sync_meta").Where(z => z[0] is string)
                .GroupBy(z => (string)z[0]!).ToDictionary(g => g.Key, g => g.Last()[1]);
            var aufhebungen = Loeschmarken.ErsetzenAufhebungen(
                Loeschmarken.Schluessel.ToDictionary(k => k, k => meta.GetValueOrDefault(k)),
                Loeschmarken.Schluessel.ToDictionary(k => k, k => meta.GetValueOrDefault(Loeschmarken.AufhebungPraefix + k)), marken);
            Ausfuehren(db, tx, "DELETE FROM sync_meta WHERE key IN ($0, $1, $2, $3)", [.. Loeschmarken.Schluessel]);
            foreach (var (schluessel, wert) in marken)
            {
                Ausfuehren(db, tx, "INSERT INTO sync_meta (key, value) VALUES ($0, $1)", schluessel, wert);
            }
            foreach (var (schluessel, paar) in aufhebungen)
            {
                Ausfuehren(db, tx, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES ($0, $1)",
                    Loeschmarken.AufhebungPraefix + schluessel, Loeschmarken.PaarText(paar));
            }
            // Tagesziel schon vor dem commit in die Hilfsdatei, direkt danach an ihren Platz: ein Absturz
            // dazwischen liesse sonst den neuen Lernstand mit dem alten Tagesziel zurueck
            hilfsdatei = einstellungen.HilfsdateiSchreiben(werte);
            Haltepunkt?.Invoke("vor-commit");
            tx.Commit();
        }
        catch (Exception e)
        {
            // Die Hilfsdatei (mit Zugangsschluessel) bleibt bei keinem Fehler liegen; die Buchung ist zurueckgenommen
            einstellungen.HilfsdateiEntfernen();
            if (e is not (SqliteException or IOException or UnauthorizedAccessException))
            {
                throw;
            }
            Uebernahme.Protokoll(ziel.Pfad, "Alles ersetzen gescheitert, nichts geaendert: " + (e is SqliteException s
                ? "SQLite-Fehler " + s.SqliteErrorCode.ToString(CultureInfo.InvariantCulture) : e.GetType().Name));
            throw new WiederherstellungAusnahme(WiederherstellungFehler.EinspielenGescheitert);
        }
        // Sehr selten scheitert das: Lernstand neu, Tagesziel bleibt alt (wie Python)
        if (hilfsdatei)
        {
            einstellungen.HilfsdateiEinsetzen();
        }
        return kopie;
    }

    private static void NurLesenPruefen(Lernstand ziel)
    {
        if (ziel.NurLesenGrund is not null)
        {
            throw new WiederherstellungAusnahme(WiederherstellungFehler.NurLesen);
        }
    }

    /// <summary>_goal_values der Sicherung als Einstellungswerte (Vorgaben und Grenzen wie goal_settings).</summary>
    internal static List<(string Schluessel, JsonNode Wert)> Ziele(JsonObject einstellungen)
    {
        var ziel = Einstellungen.Ziel(einstellungen);
        return
        [
            ("ziel_an", JsonValue.Create(ziel.ZielAn)),
            ("ziel_anzahl", JsonValue.Create(ziel.ZielAnzahl)),
            ("serie_an", JsonValue.Create(ziel.SerieAn)),
            ("erinnerung_an", JsonValue.Create(ziel.ErinnerungAn)),
            ("erinnerung_zeit", JsonValue.Create(ziel.ErinnerungZeit)),
        ];
    }

    /// <summary>_safety_path: &lt;ordner&gt;/vor_wiederherstellung_JJJJ-MM-TT_HHMM.fisisicherung, sonst _2, _3 …</summary>
    public static string KopiePfad(string ordner, DateTimeOffset jetzt)
    {
        var name = Praefix + jetzt.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);
        var pfad = Path.Combine(ordner, name + Ruecksicherung.Endung);
        for (var n = 2; File.Exists(pfad); n++)   // zweimal in einer Minute: nichts ueberschreiben
        {
            pfad = Path.Combine(ordner, name + "_" + n.ToString(CultureInfo.InvariantCulture) + Ruecksicherung.Endung);
        }
        return pfad;
    }

    /// <summary>
    /// create_backup des jetzigen Stands (in der Sperre gelesen) und save_file mit Probe; scheitert
    /// das, KopieGescheitert, und es bleibt keine halbe Datei liegen.
    /// </summary>
    private static string KopieAnlegen(Lernstand ziel, Einstellungen einstellungen, string appVersion, string geraet, string ordner, Uhr uhr)
    {
        string? geschrieben = null;
        try
        {
            Directory.CreateDirectory(ordner);
            var jetzt = uhr.Jetzt();
            var pfad = KopiePfad(ordner, jetzt);
            PyObjekt daten;
            using (var db = ziel.Verbindung())
            using (var tx = db.BeginTransaction(deferred: true))
            {
                daten = Ruecksicherung.Austausch(db, tx);
                tx.Commit();
            }
            var bytes = Ruecksicherung.Packen(Ruecksicherung.Huelle(daten, Ruecksicherung.Tagesziel(einstellungen.Laden()), appVersion,
                jetzt.DateTime, geraet));
            Ruecksicherung.Speichern(pfad, bytes);
            geschrieben = pfad;
            // Erst wenn die Kopie an ihrem Platz wieder lesbar ist (gleiche Pruefung wie beim Einspielen), wird ersetzt
            Haltepunkt?.Invoke("kopie");
            if (!File.ReadAllBytes(pfad).AsSpan().SequenceEqual(bytes))
            {
                throw new IOException("Probe der Sicherheitskopie gescheitert");
            }
            Ruecksicherung.Pruefen(File.ReadAllBytes(pfad));
            return pfad;
        }
        catch (RuecksicherungAusnahme e)
        {
            Entfernen(geschrieben);
            Uebernahme.Protokoll(ziel.Pfad, "Sicherheitskopie vor Alles ersetzen gescheitert, nichts geaendert: " + e.Fehler);
            throw new WiederherstellungAusnahme(WiederherstellungFehler.KopieGescheitert, e.Fehler);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SqliteException or NotSupportedException or ArgumentException)
        {
            Entfernen(geschrieben);
            Uebernahme.Protokoll(ziel.Pfad, "Sicherheitskopie vor Alles ersetzen gescheitert, nichts geaendert: " + e.GetType().Name);
            throw new WiederherstellungAusnahme(WiederherstellungFehler.KopieGescheitert, Ruecksicherung.Fehlerart(e));
        }
    }

    /// <summary>Nur die eben geschriebene Kopie, deren Probe gescheitert ist (nie eine fremde Datei).</summary>
    private static void Entfernen(string? datei)
    {
        if (datei is null)
        {
            return;
        }
        try
        {
            File.Delete(datei);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // -- Kleine SQL-Helfer ---------------------------------------------------

    private static SqliteCommand Befehl(SqliteConnection db, SqliteTransaction tx, string sql, object?[] werte)
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

    private static void Ausfuehren(SqliteConnection db, SqliteTransaction tx, string sql, params object?[] werte)
    {
        using var befehl = Befehl(db, tx, sql, werte);
        befehl.ExecuteNonQuery();
    }

    private static List<object?[]> Abfrage(SqliteConnection db, SqliteTransaction tx, string sql)
    {
        using var befehl = Befehl(db, tx, sql, []);
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
}
