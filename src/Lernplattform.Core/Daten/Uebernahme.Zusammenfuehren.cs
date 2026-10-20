using System.Globalization;
using Lernplattform.Core.Dashboard;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Daten;

public static partial class Uebernahme
{
    /// <summary>Nur fuer Tests: wird nach jeder Tabelle mitten in der Transaktion aufgerufen (Absturzprobe).</summary>
    internal static Action<string>? Haltepunkt { get; set; }

    private static readonly IReadOnlyDictionary<string, string> Gruppe = new Dictionary<string, string>
    {
        ["spiel_plaetze"] = "platz",
        ["abschlussprojekt"] = "projekt",
        ["spiel_ereignisse"] = "spiel",
        ["spiel_bestenliste"] = "bestenliste",
        ["test_results"] = "verlauf",
        ["pruefungen"] = "verlauf",
    };

    private static void Pruefen(Lernstand ziel, Austausch daten)
    {
        if (ziel.NurLesenGrund is not null)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.NurLesen);
        }
        if (daten.QuellPfad is { } quelle && GleicherPfad(quelle, ziel.Pfad))
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.GleicheDatei);
        }
    }

    private static bool GleicherPfad(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
        OperatingSystem.IsLinux() || OperatingSystem.IsAndroid() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Vorschau: dieselbe Rechnung auf einer Wegwerf-Kopie der Datenbank (im Temp-Ordner).
    /// Die eigene Datenbank wird nur gelesen.
    /// </summary>
    public static UebernahmeBericht Vorschau(Lernstand ziel, Austausch daten,
        IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen, Uhr? uhr = null)
    {
        Pruefen(ziel, daten);
        var kopie = Path.Combine(Path.GetTempPath(), "lernplattform_vorschau_" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var quelle = ziel.Verbindung())
            using (var ziel2 = Oeffnen(kopie))
            {
                quelle.BackupDatabase(ziel2);
            }
            using var db = Oeffnen(kopie);
            using var tx = db.BeginTransaction();
            var bericht = Zusammenfuehren(db, tx, daten, umbenennungen, uhr ?? Uhr.System, true);
            tx.Rollback();
            return bericht;
        }
        catch (SqliteException e)
        {
            Protokoll(ziel.Pfad, "Vorschau der Uebernahme gescheitert: SQLite-Fehler " + e.SqliteErrorCode.ToString(CultureInfo.InvariantCulture));
            throw new UebernahmeAusnahme(UebernahmeFehler.SchreibenGescheitert);
        }
        catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException or OverflowException)
        {
            throw new UebernahmeAusnahme(UebernahmeFehler.SchreibenGescheitert);
        }
        finally
        {
            foreach (var datei in new[] { kopie, kopie + "-journal" })
            {
                try
                {
                    File.Delete(datei);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }

    /// <summary>
    /// Uebernehmen: zuerst die automatische Sicherung der eigenen Datenbank (scheitert sie,
    /// passiert nichts), dann Zusammenfuehren in einer Transaktion.
    /// </summary>
    /// <param name="sicherungsordner">Vorgabe: Ordner "sicherungen" neben der Datenbank.</param>
    public static UebernahmeBericht Uebernehmen(Lernstand ziel, Austausch daten,
        IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen, Uhr? uhr = null, string? sicherungsordner = null)
    {
        uhr ??= Uhr.System;
        Pruefen(ziel, daten);
        var sicherung = SicherungAnlegen(ziel, sicherungsordner ?? Path.Combine(Path.GetDirectoryName(ziel.Pfad)!, SicherungsOrdner), uhr);
        Pruefen(ziel, daten);   // direkt vor dem Schreiben noch einmal (wie Python G-B)
        try
        {
            using var db = ziel.Verbindung();
            using var tx = db.BeginTransaction();
            var bericht = Zusammenfuehren(db, tx, daten, umbenennungen, uhr, false);
            tx.Commit();
            return bericht with { Sicherungsdatei = sicherung };
        }
        catch (SqliteException e)
        {
            Protokoll(ziel.Pfad, "Uebernahme gescheitert, nichts geaendert: SQLite-Fehler " + e.SqliteErrorCode.ToString(CultureInfo.InvariantCulture));
            throw new UebernahmeAusnahme(UebernahmeFehler.SchreibenGescheitert);
        }
        catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException or OverflowException)
        {
            Protokoll(ziel.Pfad, "Uebernahme gescheitert, nichts geaendert: unerwarteter Wert in der Datei");
            throw new UebernahmeAusnahme(UebernahmeFehler.SchreibenGescheitert);
        }
    }

    private static SqliteConnection Oeffnen(string pfad)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = pfad,
            Pooling = false,
            DefaultTimeout = Lernstand.Wartezeit,
        }.ToString());
        db.Open();
        return db;
    }

    /// <summary>Kopie der eigenen Datenbank (SQLite-Sicherung), danach quick_check. Pfad oder SicherungGescheitert.</summary>
    private static string SicherungAnlegen(Lernstand ziel, string ordner, Uhr uhr)
    {
        string? datei = null;
        try
        {
            Directory.CreateDirectory(ordner);
            var name = SicherungsPraefix + uhr.Jetzt().ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            datei = Path.Combine(ordner, name + ".db");
            for (var n = 2; File.Exists(datei); n++)
            {
                datei = Path.Combine(ordner, name + "_" + n.ToString(CultureInfo.InvariantCulture) + ".db");
            }
            using (var quelle = ziel.Verbindung())
            using (var kopie = Oeffnen(datei))
            {
                quelle.BackupDatabase(kopie);
            }
            using (var probe = Oeffnen(datei))
            {
                if (Abfrage(probe, null, "PRAGMA quick_check") is not [[string ok]] || ok != "ok")
                {
                    throw new IOException("quick_check");
                }
            }
            return datei;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SqliteException or NotSupportedException or ArgumentException)
        {
            Protokoll(ziel.Pfad, "Sicherung vor der Uebernahme gescheitert, nichts uebernommen: " + e.GetType().Name);
            if (datei is not null)
            {
                try
                {
                    File.Delete(datei);     // nur die eben angefangene, unvollstaendige Kopie
                }
                catch (Exception f) when (f is IOException or UnauthorizedAccessException)
                {
                }
            }
            throw new UebernahmeAusnahme(UebernahmeFehler.SicherungGescheitert);
        }
    }

    private sealed class Zaehler
    {
        public int Vorher, Nachher, Neu, Vorhanden, EntferntDurchMarke, Uebersprungen, Abgelehnt;
    }

    /// <summary>merge_into_local(check_future=True) und die Zaehlung von merge_backup, in der offenen Transaktion.</summary>
    private static UebernahmeBericht Zusammenfuehren(SqliteConnection db, SqliteTransaction tx, Austausch daten,
        IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen, Uhr uhr, bool vorschau)
    {
        // Lokaler Stand wie export_local (Marken, Aufhebungen) und _mixed_recent
        var meta = Abfrage(db, tx, "SELECT key, value FROM sync_meta").Where(z => z[0] is string)
            .GroupBy(z => (string)z[0]!).ToDictionary(g => g.Key, g => g.Last()[1]);
        var lokal = Loeschmarken.Schluessel.Where(k => Loeschmarken.Wahr(meta.GetValueOrDefault(k)))
            .ToDictionary(k => k, k => meta[k]);
        var lokalAufhebungen = Loeschmarken.AufhebungenZusammen(
            Loeschmarken.Schluessel.ToDictionary(k => k, k => meta.GetValueOrDefault(Loeschmarken.AufhebungPraefix + k)), null, true, null)
            .ToDictionary(p => p.Key, p => (object?)p.Value);
        var jetzt = uhr.Jetzt();
        var gesehen = Loeschmarken.IsoUtc(meta.GetValueOrDefault(Loeschmarken.MischbetriebSchluessel));
        var mischbetrieb = gesehen is { } g2 && (jetzt.UtcDateTime - g2.UtcDateTime).TotalSeconds <= Loeschmarken.MischbetriebSekunden;
        var zone = daten.MitSchreiber && !mischbetrieb;

        var fremd = new Dictionary<string, object?>(daten.Marken);
        var ungueltig = 0;
        foreach (var seite in new[] { lokal, fremd })
        {
            foreach (var k in Loeschmarken.Ungueltige(seite, zone))
            {
                seite.Remove(k);
                ungueltig++;
            }
        }
        // check_future: Marken der Datei mehr als 300 s nach der Geraeteuhr gelten nicht
        var zukunft = 0;
        var grenze = DateTime.SpecifyKind(jetzt.UtcDateTime.AddSeconds(Loeschmarken.UhrToleranz), DateTimeKind.Unspecified);
        foreach (var k in Loeschmarken.Schluessel)
        {
            if (Loeschmarken.Lesen(fremd.GetValueOrDefault(k)) is { Utc: { } utc } && utc > grenze)
            {
                fremd.Remove(k);
                zukunft++;
            }
        }
        var aufhebungen = Loeschmarken.AufhebungenZusammen(lokalAufhebungen, daten.Aufhebungen, zone, jetzt);
        Loeschmarken.AufhebungenAnwenden(lokal, aufhebungen, zone);
        Loeschmarken.AufhebungenAnwenden(fremd, aufhebungen, zone);
        var marken = Loeschmarken.Zusammen(lokal, fremd, zone, fremdZuerst: false);
        var reset = marken.GetValueOrDefault("reset_at");
        var geleert = Loeschmarken.Spaeter(reset, marken.GetValueOrDefault("history_cleared_at"), zone);

        var vorher = new HashSet<(string, object?)>();
        var zaehler = new Dictionary<string, Zaehler>();
        foreach (var (tabelle, _) in Tabellen)
        {
            var uids = Abfrage(db, tx, $"SELECT uid FROM {tabelle}");
            zaehler[tabelle] = new Zaehler { Vorher = uids.Count };
            vorher.UnionWith(uids.Select(z => (tabelle, z[0])));
        }

        Ausfuehren(db, tx, "DELETE FROM sync_meta WHERE key IN ($0, $1, $2, $3)", [.. Loeschmarken.Schluessel]);
        foreach (var (k, wert) in marken)
        {
            Ausfuehren(db, tx, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES ($0, $1)", k, wert);
        }
        foreach (var (k, paar) in aufhebungen)
        {
            Ausfuehren(db, tx, "INSERT OR REPLACE INTO sync_meta (key, value) VALUES ($0, $1)",
                Loeschmarken.AufhebungPraefix + k, Loeschmarken.PaarText(paar));
        }

        var weg = new HashSet<(string, object?)>();
        var erhalten = new List<(string Tabelle, object? Uid)>();
        foreach (var (tabelle, spalten) in Tabellen)
        {
            var z = zaehler[tabelle];
            var schnittText = Gruppe.GetValueOrDefault(tabelle) switch
            {
                "platz" or "projekt" => null,
                "spiel" => marken.GetValueOrDefault("spiel_reset_at"),
                "bestenliste" => marken.GetValueOrDefault("bestenliste_reset_at"),
                "verlauf" => geleert,
                _ => reset,
            };
            (string Text, DateTime? Utc)? schnitt = null;
            if (!string.IsNullOrEmpty(schnittText))
            {
                schnitt = zone ? Loeschmarken.Lesen(schnittText) : (schnittText, null);
            }
            if (schnitt is { } s)
            {
                var (uids, anzahl) = LoeschenBis(db, tx, tabelle, spalten, s);
                weg.UnionWith(uids.Select(u => (tabelle, u)));
                z.EntferntDurchMarke = anzahl;
            }
            var sql = $"INSERT OR IGNORE INTO {tabelle} (uid, {string.Join(", ", spalten)}) VALUES ("
                + string.Join(", ", Enumerable.Range(0, spalten.Length + 1).Select(i => "$" + i.ToString(CultureInfo.InvariantCulture))) + ")";
            foreach (var zeile in daten.Tabellen.GetValueOrDefault(tabelle, []))
            {
                if (Satz(daten, tabelle, zeile) is not { } satz)
                {
                    z.Uebersprungen++;
                    continue;
                }
                var uid = satz["uid"];
                if (schnitt is { } s2 && FaelltUnter(satz, s2))
                {
                    if (Abfrage(db, tx, $"SELECT 1 FROM {tabelle} WHERE uid = $0", uid).Count == 0)
                    {
                        weg.Add((tabelle, uid));
                    }
                    continue;
                }
                if (Ausfuehren(db, tx, sql, [uid, .. spalten.Select(c => satz[c])]) > 0)
                {
                    z.Neu++;
                    erhalten.Add((tabelle, uid));
                }
                else if (Abfrage(db, tx, $"SELECT 1 FROM {tabelle} WHERE uid = $0", uid).Count > 0)
                {
                    z.Vorhanden++;
                }
                else
                {
                    z.Abgelehnt++;
                }
            }
            Haltepunkt?.Invoke("nach:" + tabelle);
        }

        // purge_deleted_runs: Ereignisse geloeschter Spieldurchgaenge verschwinden
        foreach (var lauf in Abfrage(db, tx, "SELECT DISTINCT lauf FROM spiel_plaetze WHERE aktion = 'geloescht'").Select(r => r[0]))
        {
            if (lauf is "alt")
            {
                Ausfuehren(db, tx, "DELETE FROM spiel_ereignisse WHERE lauf IS NULL OR lauf = ''");
            }
            else
            {
                Ausfuehren(db, tx, "DELETE FROM spiel_ereignisse WHERE lauf = $0", lauf);
            }
        }
        var uebernommen = erhalten.Count(e => e.Tabelle != "spiel_ereignisse"
            || Abfrage(db, tx, "SELECT 1 FROM spiel_ereignisse WHERE uid = $0", e.Uid).Count > 0);
        // purge_superseded_project_rows (Python v0.63.2 woertlich)
        Ausfuehren(db, tx,
            "DELETE FROM abschlussprojekt WHERE feld != '_geloescht' AND wert != ''"
            + " AND EXISTS ("
            + " SELECT 1 FROM abschlussprojekt AS loesch"
            + " WHERE loesch.projekt = abschlussprojekt.projekt"
            + " AND loesch.feld = '_geloescht' AND loesch.wert = '1'"
            + " AND loesch.timestamp < abschlussprojekt.timestamp)");
        Ausfuehren(db, tx,
            "DELETE FROM abschlussprojekt WHERE EXISTS ("
            + " SELECT 1 FROM abschlussprojekt AS neu"
            + " WHERE neu.projekt = abschlussprojekt.projekt"
            + " AND neu.feld = abschlussprojekt.feld"
            + " AND (neu.timestamp > abschlussprojekt.timestamp"
            + "  OR (neu.timestamp = abschlussprojekt.timestamp"
            + "      AND neu.uid > abschlussprojekt.uid)))");
        // apply_question_renames: alte Fragetexte auf die neuen (uid und Zeit bleiben)
        var umbenannt = 0;
        foreach (var (tabelle, alt, neu) in umbenennungen)
        {
            umbenannt += Ausfuehren(db, tx, $"UPDATE {tabelle} SET question = $0 WHERE question = $1", neu, alt);
        }

        var nachher = new HashSet<(string, object?)>();
        foreach (var (tabelle, _) in Tabellen)
        {
            var uids = Abfrage(db, tx, $"SELECT uid FROM {tabelle}");
            zaehler[tabelle].Nachher = uids.Count;
            nachher.UnionWith(uids.Select(z => (tabelle, z[0])));
        }
        var markenNachher = Abfrage(db, tx, "SELECT key, value FROM sync_meta WHERE key IN ($0, $1, $2, $3)", [.. Loeschmarken.Schluessel])
            .ToDictionary(z => (string)z[0]!, z => PyText.Str(z[1]));
        var tabellenZahlen = Tabellen.Select(t =>
        {
            var z = zaehler[t.Tabelle];
            return new TabellenZahlen(t.Tabelle, z.Vorher, z.Nachher, z.Neu, z.Vorhanden, z.EntferntDurchMarke,
                weg.Count(w => w.Item1 == t.Tabelle && !vorher.Contains(w)), z.Uebersprungen, z.Abgelehnt);
        }).ToList();
        return new UebernahmeBericht(
            daten.Quelle, vorschau,
            Neu: nachher.Except(vorher).Count(e => e.Item1 != "spiel_plaetze"),
            Uebernommen: uebernommen,
            SchonVorhanden: tabellenZahlen.Sum(t => t.Vorhanden),
            EntferntDurchLoeschmarke: tabellenZahlen.Sum(t => t.EntferntDurchMarke),
            EntferntSonst: tabellenZahlen.Sum(t => t.Vorher + t.Neu - t.EntferntDurchMarke - t.Nachher),
            NichtUebernommen: weg.Count(w => !vorher.Contains(w)),
            Uebersprungen: tabellenZahlen.Sum(t => t.Uebersprungen),
            Abgelehnt: tabellenZahlen.Sum(t => t.Abgelehnt),
            UngueltigeMarken: ungueltig,
            ZukunftsMarken: zukunft,
            Umbenannt: umbenannt,
            MarkenNachher: markenNachher,
            Tabellen: tabellenZahlen,
            Sicherungsdatei: null);
    }

    /// <summary>_delete_before: loescht alles, was nicht nach der Marke liegt. (uids, Anzahl Zeilen).</summary>
    private static (List<object?> Uids, int Anzahl) LoeschenBis(SqliteConnection db, SqliteTransaction tx, string tabelle, string[] spalten,
        (string Text, DateTime? Utc) marke)
    {
        string bedingung;
        object?[] werte;
        if (marke.Utc is null || !spalten.Contains("utc_offset"))
        {
            (bedingung, werte) = ("timestamp <= $0", [marke.Text]);
        }
        else
        {
            bedingung = "(utc_offset IS NULL AND timestamp <= $0) OR (utc_offset IS NOT NULL AND "
                + "datetime(substr(timestamp, 1, 19), printf('%d minutes', -utc_offset)) <= $1)";
            werte = [marke.Text, marke.Utc.Value.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)];
        }
        var uids = Abfrage(db, tx, $"SELECT uid FROM {tabelle} WHERE {bedingung}", werte).Select(z => z[0]).Distinct().ToList();
        var anzahl = Ausfuehren(db, tx, $"DELETE FROM {tabelle} WHERE {bedingung}", werte);
        return (uids, anzahl);
    }

    /// <summary>_falls_under: liegt ein Eintrag zeitlich nicht nach der Marke? Mit Zone und utc_offset in UTC, sonst als Text.</summary>
    internal static bool FaelltUnter(IReadOnlyDictionary<string, object?> satz, (string Text, DateTime? Utc) marke)
    {
        var zeit = PyText.Str(satz.GetValueOrDefault("timestamp"));
        var abstand = satz.GetValueOrDefault("utc_offset");
        if (marke.Utc is { } utc && abstand is not null)
        {
            var moment = Loeschmarken.Ortszeit(PyText.Teil(zeit, 0, 19));
            long? minuten = abstand switch
            {
                long n => n,
                double d when double.IsNaN(d) => null,
                double d => double.IsFinite(d) ? (long)Math.Truncate(d) : throw new OverflowException("utc_offset"),
                string t => Einstellungen.PythonIntText(t),
                _ => null,
            };
            if (moment is { } m && minuten is { } min)
            {
                // Python: timedelta ausserhalb des Kalenders bricht ab (OverflowError) - hier ebenso
                return m.AddMinutes(-min) <= utc;
            }
        }
        return Loeschmarken.Vergleich(zeit, marke.Text) <= 0;
    }
}
