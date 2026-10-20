using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Liest den Lernstand der Python-Linie: die Datenbank fisi_lernplattform.db
/// und die Sicherungsdatei lernstand.json.gz (Formatversion 1, Abgleich-Format 2).
/// Nur lesen, nie schreiben. Grundlage fuer die Pflicht-Uebernahme (E4) in Phase 2.
/// </summary>
public static class AlterLernstand
{
    public const int SicherungFormatversion = 1;
    public const int AbgleichFormat = 2;

    public sealed record Tabelle(string Name, IReadOnlyList<string> Spalten, IReadOnlyList<object?[]> Zeilen);

    /// <summary>Alle Tabellen der alten Datenbank, nur lesend geoeffnet.</summary>
    public static List<Tabelle> LeseDatenbank(string pfad)
    {
        var verbindung = new SqliteConnectionStringBuilder
        {
            DataSource = pfad,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        };
        using var db = new SqliteConnection(verbindung.ToString());
        db.Open();
        var namen = new List<string>();
        using (var befehl = db.CreateCommand())
        {
            befehl.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var leser = befehl.ExecuteReader();
            while (leser.Read())
            {
                namen.Add(leser.GetString(0));
            }
        }
        var ergebnis = new List<Tabelle>();
        foreach (var name in namen)
        {
            using var befehl = db.CreateCommand();
            // Tabellennamen stammen aus sqlite_master, nicht von aussen
            befehl.CommandText = $"SELECT * FROM \"{name.Replace("\"", "\"\"")}\" ORDER BY rowid";
            using var leser = befehl.ExecuteReader();
            var spalten = Enumerable.Range(0, leser.FieldCount).Select(leser.GetName).ToList();
            var zeilen = new List<object?[]>();
            while (leser.Read())
            {
                var zeile = new object?[leser.FieldCount];
                for (int i = 0; i < leser.FieldCount; i++)
                {
                    zeile[i] = leser.IsDBNull(i) ? null : leser.GetValue(i);
                }
                zeilen.Add(zeile);
            }
            ergebnis.Add(new Tabelle(name, spalten, zeilen));
        }
        return ergebnis;
    }

    /// <summary>Sicherungsdatei (gzip-JSON) lesen; prueft Art und Formatversion.</summary>
    public static JsonDocument LeseSicherung(Stream gzipDaten)
    {
        using var entpackt = new GZipStream(gzipDaten, CompressionMode.Decompress);
        var dokument = JsonDocument.Parse(entpackt);
        var wurzel = dokument.RootElement;
        int version = wurzel.GetProperty("formatversion").GetInt32();
        int abgleich = wurzel.GetProperty("daten").GetProperty("format").GetInt32();
        if (version > SicherungFormatversion || abgleich > AbgleichFormat)
        {
            dokument.Dispose();
            throw new InvalidDataException($"Sicherung zu neu: Formatversion {version}, Abgleich-Format {abgleich}");
        }
        return dokument;
    }
}
