using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 1, K2 (R3): Lernstand-Datenbank. Aufbau exakt wie Python (G11),
/// Speicherpruefung, Fehlerarten und Verhalten bei kaputter, gesperrter oder
/// schreibgeschuetzter Datei. Nur Testdaten in Wegwerf-Ordnern.
/// </summary>
public class LernstandTests
{
    private static readonly JsonElement G11 = Lade();

    private static JsonElement Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("datenbank.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    /// <summary>Aufbau der Datenbank als JSON, gleiche Rechnung wie schema() in erzeuge_datenbank.py.</summary>
    internal static JsonObject Aufbau(string pfad)
    {
        using var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False;Mode=ReadOnly");
        verbindung.Open();
        List<object?[]> Zeilen(string sql)
        {
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = sql;
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
        static string? Normal(object? sql) => sql is string s ? string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) : null;
        static JsonNode? Wert(object? w) => w switch
        {
            null => null,
            long l => JsonValue.Create(l),
            string s => JsonValue.Create(s),
            _ => JsonValue.Create(w.ToString()),
        };

        var tabellen = new JsonArray();
        foreach (var t in Zeilen("SELECT name, sql FROM sqlite_master WHERE type = 'table' ORDER BY name"))
        {
            var name = (string)t[0]!;
            var spalten = new JsonArray(Zeilen($"PRAGMA table_info({name})")
                .Select(z => (JsonNode)new JsonArray(z.Select(Wert).ToArray())).ToArray());
            var indizes = Zeilen($"PRAGMA index_list({name})")
                .Select(z => new JsonObject
                {
                    ["name"] = (string)z[1]!,
                    ["unique"] = (long)z[2]!,
                    ["origin"] = (string)z[3]!,
                    ["partial"] = (long)z[4]!,
                    ["spalten"] = new JsonArray(Zeilen($"PRAGMA index_info({z[1]})").Select(r => Wert(r[2])).ToArray()),
                })
                .OrderBy(i => (string)i["name"]!, StringComparer.Ordinal)
                .Select(i => (JsonNode)i).ToArray();
            tabellen.Add(new JsonObject
            {
                ["name"] = name,
                ["sql"] = Normal(t[1]),
                ["spalten"] = spalten,
                ["indizes"] = new JsonArray(indizes),
            });
        }
        var indexSql = new JsonArray(Zeilen("SELECT name, tbl_name, sql FROM sqlite_master WHERE type = 'index' ORDER BY name")
            .Select(z => (JsonNode)new JsonArray(Wert(z[0]), Wert(z[1]), Normal(z[2]) is { } s ? JsonValue.Create(s) : null)).ToArray());
        var version = (long)Zeilen("PRAGMA user_version")[0][0]!;
        return new JsonObject { ["tabellen"] = tabellen, ["index_sql"] = indexSql, ["user_version"] = version };
    }

    [Fact]
    public void AufbauGleichWiePython()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        Assert.Null(lernstand.NurLesenGrund);
        var ist = Aufbau(lernstand.Pfad);
        var soll = JsonNode.Parse(G11.GetProperty("schema").GetRawText())!.AsObject();
        // Einzige gewollte Abweichung: Version des Aufbaus (Python 0, C# 1)
        Assert.Equal(0L, (long)soll["user_version"]!);
        Assert.Equal((long)Lernstand.AufbauVersion, (long)ist["user_version"]!);
        soll.Remove("user_version");
        ist.Remove("user_version");
        Assert.Equal(13, soll["tabellen"]!.AsArray().Count(t => !((string)t!["name"]!).StartsWith("sqlite_", StringComparison.Ordinal)));
        Assert.Equal(soll.ToJsonString(), ist.ToJsonString());
    }

    /// <summary>Kontrollfehler: Fehlt eine Spalte, muss der Vergleich das finden.</summary>
    [Fact]
    public void KontrollfehlerFehlendeSpalteFaellt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        using (var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False"))
        {
            verbindung.Open();
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = "ALTER TABLE card_events DROP COLUMN utc_offset";
            befehl.ExecuteNonQuery();
        }
        var ist = Aufbau(pfad);
        var soll = JsonNode.Parse(G11.GetProperty("schema").GetRawText())!.AsObject();
        soll.Remove("user_version");
        ist.Remove("user_version");
        Assert.NotEqual(soll.ToJsonString(), ist.ToJsonString());
    }

    [Fact]
    public void ZweitesOeffnenAendertNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var erster = new Lernstand(pfad);
        Assert.True(erster.Schreiben("INSERT INTO card_events (timestamp, category, question, mode, correct, uid) VALUES ($t, $c, $q, $m, $k, $u)",
            ("$t", "2026-10-20 10:00:00"), ("$c", "Netzwerk & Protokolle"), ("$q", "Frage?"), ("$m", "karte"), ("$k", 1), ("$u", "u1")));
        var vorher = Aufbau(pfad).ToJsonString();
        var zweiter = new Lernstand(pfad);
        Assert.Null(zweiter.NurLesenGrund);
        Assert.Equal(vorher, Aufbau(pfad).ToJsonString());
        Assert.Single(zweiter.Abfragen("SELECT question FROM card_events"));
    }

    [Fact]
    public void UidBleibtEindeutig()
    {
        using var ordner = new TempOrdner();
        var meldungen = new List<string>();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName), meldungen.Add);
        const string sql = "INSERT INTO quiz_answers (timestamp, category, question, correct, uid) VALUES ('t', 'c', 'q', 1, 'gleich')";
        Assert.True(lernstand.Schreiben(sql));
        Assert.False(lernstand.Schreiben(sql));
        Assert.Equal(1, lernstand.Fehlerzaehler["sonstige"]);
        Assert.Null(lernstand.NurLesenGrund);
        Assert.Equal([Lernstand.Fehlertexte["sonstige"]], meldungen);
        Assert.Contains("Datenbankfehler (sonstige)", File.ReadAllText(Path.Combine(ordner.Pfad, "fehler.log")));
    }

    [Fact]
    public void FehlertexteUndArtenWiePython()
    {
        foreach (var art in G11.GetProperty("fehlertexte").EnumerateObject())
        {
            var soll = art.Value.GetString()!.Replace("fisi_lernplattform.db", Lernstand.DateiName, StringComparison.Ordinal);
            Assert.Equal(soll, Lernstand.Fehlertexte[art.Name]);
        }
        Assert.Equal(G11.GetProperty("fehlertexte").EnumerateObject().Count(), Lernstand.Fehlertexte.Count);
        foreach (var zeile in G11.GetProperty("nur_lesen_zeilen").EnumerateObject())
        {
            Assert.Equal(zeile.Value.GetString(), Lernstand.NurLesenZeilen[zeile.Name]);
        }
        Assert.Equal(G11.GetProperty("nur_lesen_zeilen").EnumerateObject().Count(), Lernstand.NurLesenZeilen.Count);
        foreach (var fall in G11.GetProperty("fehlerarten").EnumerateArray())
        {
            Assert.Equal(fall[1].GetString(), Lernstand.Fehlerart(fall[0].GetString()!));
        }
    }

    [Fact]
    public void KaputteDateiBleibtUnveraendert()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "Das ist keine Datenbank, sondern Text."u8, .. new byte[200]]);
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var meldungen = new List<string>();
        var lernstand = new Lernstand(pfad, meldungen.Add);
        Assert.Equal("kaputt", lernstand.NurLesenGrund);
        Assert.False(lernstand.Schreiben("CREATE TABLE x (a)"));
        Assert.Equal(1, lernstand.AbgelehnteSchreibzugriffe);
        Assert.Empty(lernstand.Abfragen("SELECT 1 FROM card_events"));
        Assert.Equal([Lernstand.Fehlertexte["kaputt"]], meldungen);
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
    }

    [Fact]
    public void BeschaedigteSeiteWirdErkannt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var lernstand = new Lernstand(pfad);
        for (var i = 0; i < 200; i++)
        {
            lernstand.Schreiben("INSERT INTO card_events (timestamp, category, question, mode, uid) VALUES ('t', 'c', $q, 'm', $u)",
                ("$q", new string('x', 500) + i), ("$u", "u" + i));
        }
        // Mitte der Datei ueberschreiben (Seiten der Tabelle, nicht der Kopf)
        var bytes = File.ReadAllBytes(pfad);
        Array.Fill(bytes, (byte)0xFF, bytes.Length / 2, 4096);
        File.WriteAllBytes(pfad, bytes);
        var vorher = SHA256.HashData(bytes);
        var zweiter = new Lernstand(pfad);
        Assert.Equal("kaputt", zweiter.NurLesenGrund);
        // Zweite Sicherung: auch wer an Schreiben() vorbei die Verbindung nutzt, kann nichts aendern
        using (var verbindung = zweiter.Verbindung())
        using (var befehl = verbindung.CreateCommand())
        {
            befehl.CommandText = "PRAGMA query_only";
            Assert.Equal(1L, (long)befehl.ExecuteScalar()!);
            befehl.CommandText = "INSERT INTO sync_meta (key, value) VALUES ('a', 'b')";
            var fehler = Assert.Throws<SqliteException>(() => befehl.ExecuteNonQuery());
            Assert.Equal("schreibschutz", Lernstand.Fehlerart(fehler.Message));
        }
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
    }

    [Fact]
    public void LeereDateiWirdAngelegt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, []);
        var lernstand = new Lernstand(pfad);
        Assert.Null(lernstand.NurLesenGrund);
        Assert.Equal((long)Lernstand.AufbauVersion, (long)Aufbau(pfad)["user_version"]!);
    }

    [Fact]
    public void FehlenderOrdnerIstNichtErreichbar()
    {
        using var ordner = new TempOrdner();
        var meldungen = new List<string>();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, "fehlt", "noch_tiefer", Lernstand.DateiName), meldungen.Add);
        Assert.Equal("nicht_erreichbar", lernstand.NurLesenGrund);
        Assert.Equal([Lernstand.Fehlertexte["nicht_erreichbar"]], meldungen);
        Assert.False(Directory.Exists(Path.Combine(ordner.Pfad, "fehlt")));
    }

    [Fact]
    public void SchreibgeschuetzteDateiSpeichertNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(pfad, FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(pfad, UnixFileMode.UserRead);
        }
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var meldungen = new List<string>();
        var lernstand = new Lernstand(pfad, meldungen.Add);
        Assert.Equal("schreibschutz", lernstand.NurLesenGrund);
        Assert.False(lernstand.Schreiben("INSERT INTO sync_meta (key, value) VALUES ('a', 'b')"));
        Assert.Equal([Lernstand.Fehlertexte["schreibschutz"]], meldungen);
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
    }

    [Fact]
    public void SchreibgeschuetzterOrdnerSpeichertNichts()
    {
        using var ordner = new TempOrdner();
        var unter = Directory.CreateDirectory(Path.Combine(ordner.Pfad, "daten")).FullName;
        if (OperatingSystem.IsWindows())
        {
            // Unter Windows verhindert das Ordner-Attribut kein Anlegen; dort zaehlt der Schutz der Datei (Test oben).
            var datei = Path.Combine(unter, Lernstand.DateiName);
            _ = new Lernstand(datei);
            File.SetAttributes(datei, FileAttributes.ReadOnly);
            Assert.Equal("schreibschutz", new Lernstand(datei).NurLesenGrund);
            return;
        }
        File.SetUnixFileMode(unter, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var lernstand = new Lernstand(Path.Combine(unter, Lernstand.DateiName));
        Assert.Equal("schreibschutz", lernstand.NurLesenGrund);
        Assert.Empty(Directory.EnumerateFileSystemEntries(unter));
    }

    [Fact]
    public void GesperrteDatenbankMeldetGesperrt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var meldungen = new List<string>();
        var lernstand = new Lernstand(pfad, meldungen.Add);
        using var sperre = new SqliteConnection($"Data Source={pfad};Pooling=False;Default Timeout=1");
        sperre.Open();
        using (var befehl = sperre.CreateCommand())
        {
            befehl.CommandText = "BEGIN EXCLUSIVE";
            befehl.ExecuteNonQuery();
        }
        Assert.False(lernstand.Schreiben("INSERT INTO sync_meta (key, value) VALUES ('a', 'b')"));
        Assert.Null(lernstand.NurLesenGrund);
        Assert.Equal(1, lernstand.Fehlerzaehler["gesperrt"]);
        Assert.Equal([Lernstand.Fehlertexte["gesperrt"]], meldungen);
    }

    /// <summary>Mischbetrieb: Die neue App benutzt nie den Ordner oder die Datei der Python-App.</summary>
    [Fact]
    public void MischbetriebGetrennteDaten()
    {
        var ordner = Ablage.Datenordner();
        Assert.DoesNotContain("FISI-Lernplattform", ordner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fisi-lernplattform", ordner, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("fisi_lernplattform.db", Lernstand.DateiName);
        Assert.Equal("lernplattform.db", Lernstand.DateiName);
    }
}
