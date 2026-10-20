using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>
    /// Prueft die Pruefsumme der Sicherung wie fisi_sicherung._checksum:
    /// SHA-256 ueber json.dumps({"daten", "einstellungen"}, sort_keys=True,
    /// separators=(",", ":"), ensure_ascii=False). Zahlen werden im Originaltext
    /// uebernommen (die Datei stammt aus Pythons json.dumps).
    /// </summary>
    public static bool PruefsummeStimmt(JsonDocument sicherung)
    {
        var wurzel = sicherung.RootElement;
        var sb = new StringBuilder();
        sb.Append("{\"daten\":");
        SchreibePythonJson(wurzel.GetProperty("daten"), sb);
        sb.Append(",\"einstellungen\":");
        SchreibePythonJson(wurzel.GetProperty("einstellungen"), sb);
        sb.Append('}');
        var summe = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        return wurzel.TryGetProperty("pruefsumme", out var soll) && soll.GetString() == summe;
    }

    private static void SchreibePythonJson(JsonElement e, StringBuilder sb)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                var erstes = true;
                foreach (var p in e.EnumerateObject().OrderBy(p => p.Name, CodepunktVergleich.Instanz))
                {
                    if (!erstes)
                    {
                        sb.Append(',');
                    }
                    erstes = false;
                    SchreibeText(p.Name, sb);
                    sb.Append(':');
                    SchreibePythonJson(p.Value, sb);
                }
                sb.Append('}');
                break;
            case JsonValueKind.Array:
                sb.Append('[');
                var i = 0;
                foreach (var x in e.EnumerateArray())
                {
                    if (i++ > 0)
                    {
                        sb.Append(',');
                    }
                    SchreibePythonJson(x, sb);
                }
                sb.Append(']');
                break;
            case JsonValueKind.String:
                SchreibeText(e.GetString()!, sb);
                break;
            default:
                sb.Append(e.GetRawText());
                break;
        }
    }

    /// <summary>Text wie Pythons json.dumps mit ensure_ascii=False.</summary>
    private static void SchreibeText(string text, StringBuilder sb)
    {
        sb.Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        sb.Append('"');
    }

    /// <summary>Sortierung wie Python (Unicode-Codepunkte, nicht UTF-16-Einheiten).</summary>
    private sealed class CodepunktVergleich : IComparer<string>
    {
        public static readonly CodepunktVergleich Instanz = new();

        public int Compare(string? a, string? b)
        {
            var ea = a!.EnumerateRunes();
            var eb = b!.EnumerateRunes();
            while (true)
            {
                bool ha = ea.MoveNext(), hb = eb.MoveNext();
                if (!ha || !hb)
                {
                    return ha.CompareTo(hb);
                }
                var d = ea.Current.Value.CompareTo(eb.Current.Value);
                if (d != 0)
                {
                    return d;
                }
            }
        }
    }
}
