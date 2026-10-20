using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Gemeinsame Hilfen fuer die Vergleichswerte G15-G20: JSON-Werte in SQLite-Werte
/// (ganze Zahl, Kommazahl, Text, NULL) und ein Text je Wert, der ganze Zahl und
/// Kommazahl unterscheidet (100 ist nicht 100.0).
/// </summary>
internal static class VergleichsHilfe
{
    public static JsonElement Lade(string datei)
    {
        using var f = File.OpenRead(Pfade.Vergleich(datei));
        return JsonDocument.Parse(f, new JsonDocumentOptions { MaxDepth = 256 }).RootElement.Clone();
    }

    /// <summary>JSON-Wert wie Python ihn geschrieben hat: Zahl ohne Punkt/Exponent = ganze Zahl.</summary>
    public static object? Wert(JsonElement wert) => wert.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => wert.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when wert.GetRawText().IndexOfAny(['.', 'e', 'E']) < 0 => wert.GetInt64(),
        JsonValueKind.Number => wert.GetDouble(),
        _ => throw new InvalidDataException(wert.GetRawText()),
    };

    /// <summary>Text eines Wertes mit Typ: i:5, f:5.0 (kuerzeste Darstellung), s:..., null.</summary>
    public static string Text(object? wert) => wert switch
    {
        null or DBNull => "null",
        long n => "i:" + n.ToString(CultureInfo.InvariantCulture),
        int n => "i:" + n.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "b:1" : "b:0",
        double d => "f:" + d.ToString("R", CultureInfo.InvariantCulture),
        string s => "s:" + s,
        byte[] b => "x:" + Convert.ToHexString(b),
        _ => throw new InvalidDataException(wert.GetType().Name),
    };

    public static string Text(JsonElement wert) => Text(Wert(wert));

    public static string Zeile(IEnumerable<object?> werte) => string.Join(" | ", werte.Select(Text));

    public static string Zeile(JsonElement werte) => string.Join(" | ", werte.EnumerateArray().Select(Text));

    public static SqliteConnection Oeffnen(string pfad)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = pfad, Pooling = false }.ToString());
        db.Open();
        return db;
    }

    public static List<object?[]> Abfrage(SqliteConnection db, string sql)
    {
        using var befehl = db.CreateCommand();
        befehl.CommandText = sql;
        using var leser = befehl.ExecuteReader();
        var zeilen = new List<object?[]>();
        while (leser.Read())
        {
            var zeile = new object?[leser.FieldCount];
            for (var i = 0; i < zeile.Length; i++)
            {
                zeile[i] = leser.IsDBNull(i) ? null : leser.GetValue(i);
            }
            zeilen.Add(zeile);
        }
        return zeilen;
    }

    public static void Ausfuehren(SqliteConnection db, string sql, params object?[] werte)
    {
        using var befehl = db.CreateCommand();
        befehl.CommandText = sql;
        for (var i = 0; i < werte.Length; i++)
        {
            befehl.Parameters.AddWithValue("$p" + i.ToString(CultureInfo.InvariantCulture), werte[i] ?? DBNull.Value);
        }
        befehl.ExecuteNonQuery();
    }

    /// <summary>Zeilen {tabelle: {spalten, zeilen}} in die Datenbank (Spaltennamen wie im Dump, auch id).</summary>
    public static void FuelleTabellen(SqliteConnection db, JsonElement tabellen)
    {
        foreach (var tabelle in tabellen.EnumerateObject())
        {
            var spalten = tabelle.Value.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()!).ToList();
            var sql = $"INSERT INTO {tabelle.Name} ({string.Join(", ", spalten)}) VALUES ("
                + string.Join(", ", spalten.Select((_, i) => "$p" + i.ToString(CultureInfo.InvariantCulture))) + ")";
            foreach (var zeile in tabelle.Value.GetProperty("zeilen").EnumerateArray())
            {
                Ausfuehren(db, sql, [.. zeile.EnumerateArray().Select(Wert)]);
            }
        }
    }

    /// <summary>Stand einer Datenbank wie stand_von() im Erzeuger G20, als Textzeilen (Spalten wie in Python).</summary>
    public static List<string> Stand(string pfad, JsonElement erwartet)
    {
        using var db = Oeffnen(pfad);
        var zeilen = new List<string>();
        foreach (var tabelle in erwartet.GetProperty("tabellen").EnumerateObject())
        {
            var spalten = tabelle.Value.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()!).ToList();
            zeilen.Add("# " + tabelle.Name + ": " + string.Join(", ", spalten));
            zeilen.AddRange(Abfrage(db, $"SELECT {string.Join(", ", spalten)} FROM {tabelle.Name} ORDER BY id")
                .Select(z => tabelle.Name + ": " + Zeile(z)));
        }
        zeilen.Add("# sync_meta");
        zeilen.AddRange(Abfrage(db, "SELECT key, value FROM sync_meta ORDER BY key").Select(z => "meta: " + Zeile(z)));
        zeilen.Add("# sqlite_sequence");
        zeilen.AddRange(Abfrage(db, "SELECT name, seq FROM sqlite_sequence ORDER BY name").Select(z => "seq: " + Zeile(z)));
        return zeilen;
    }

    /// <summary>Erwarteter Stand aus G20 in derselben Textform.</summary>
    public static List<string> Stand(JsonElement erwartet)
    {
        var zeilen = new List<string>();
        foreach (var tabelle in erwartet.GetProperty("tabellen").EnumerateObject())
        {
            var spalten = tabelle.Value.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()!).ToList();
            zeilen.Add("# " + tabelle.Name + ": " + string.Join(", ", spalten));
            zeilen.AddRange(tabelle.Value.GetProperty("zeilen").EnumerateArray().Select(z => tabelle.Name + ": " + Zeile(z)));
        }
        zeilen.Add("# sync_meta");
        zeilen.AddRange(erwartet.GetProperty("sync_meta").EnumerateArray().Select(z => "meta: " + Zeile(z)));
        zeilen.Add("# sqlite_sequence");
        zeilen.AddRange(erwartet.GetProperty("sqlite_sequence").EnumerateArray().Select(z => "seq: " + Zeile(z)));
        return zeilen;
    }

    /// <summary>Unterschied zweier Zeilenlisten lesbar (erste Abweichung mit Umgebung).</summary>
    public static string Unterschied(IReadOnlyList<string> erwartet, IReadOnlyList<string> ist)
    {
        var text = new StringBuilder();
        for (var i = 0; i < Math.Max(erwartet.Count, ist.Count); i++)
        {
            var e = i < erwartet.Count ? erwartet[i] : "<fehlt>";
            var c = i < ist.Count ? ist[i] : "<fehlt>";
            if (e != c)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"Zeile {i}:\n  Python: {e}\n  C#:     {c}");
                if (text.Length > 4000)
                {
                    break;
                }
            }
        }
        return text.ToString();
    }

    /// <summary>Python-Sortierung von Texten (Codepunkte).</summary>
    public static int Codepunkte(string a, string b)
    {
        var x = a.EnumerateRunes().GetEnumerator();
        var y = b.EnumerateRunes().GetEnumerator();
        while (true)
        {
            var mx = x.MoveNext();
            var my = y.MoveNext();
            if (!mx || !my)
            {
                return mx.CompareTo(my);
            }
            var d = x.Current.Value.CompareTo(y.Current.Value);
            if (d != 0)
            {
                return d;
            }
        }
    }
}
