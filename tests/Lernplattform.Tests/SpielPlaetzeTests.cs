using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G44 Teil "Plaetze" (<c>spiel_plaetze.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_plaetze.py</c>): Aus denselben Tabellen liest C# dieselben
/// Platz-Zeilen, dieselbe Belegung, dieselben Ereignisse je Durchgang, denselben aktiven Durchgang und
/// dieselbe Uebersicht der Spielstaende wie Python 0.63.2.
/// </summary>
public class SpielPlaetzeTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_plaetze.json");

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    private static void Gleich(JsonElement soll, object? ist, string wo)
    {
        var s = T(W(soll));
        Assert.True(s == T(ist), wo + "\n  Python: " + s + "\n  C#:     " + T(ist));
    }

    /// <summary>Die festgehaltenen Tabellen in eine neue Datenbank mit dem Aufbau von <see cref="Lernstand"/>.</summary>
    private static void Einfuegen(SqliteConnection v, JsonElement tabellen)
    {
        var spalten = new Dictionary<string, string[]>
        {
            ["spiel_ereignisse"] = ["id", "timestamp", "utc_offset", "typ", "daten", "geraet", "uid", "lauf"],
            ["spiel_plaetze"] = ["id", "timestamp", "platz", "lauf", "aktion", "daten", "geraet", "uid"],
            ["sync_meta"] = ["key", "value"],
        };
        foreach (var (tabelle, namen) in spalten)
        {
            foreach (var zeile in tabellen.GetProperty(tabelle).EnumerateArray())
            {
                using var befehl = v.CreateCommand();
                befehl.CommandText = "INSERT INTO " + tabelle + " (" + string.Join(", ", namen) + ") VALUES ("
                    + string.Join(", ", namen.Select((_, i) => "$p" + i)) + ")";
                for (var i = 0; i < namen.Length; i++)
                {
                    var wert = zeile[i];
                    befehl.Parameters.AddWithValue("$p" + i, wert.ValueKind switch
                    {
                        JsonValueKind.Null => DBNull.Value,
                        JsonValueKind.Number => wert.GetInt64(),
                        _ => wert.GetString()!,
                    });
                }
                befehl.ExecuteNonQuery();
            }
        }
    }

    private static List<object?> Paare<T1, T2>(IEnumerable<(T1, T2)> paare) =>
        paare.Select(p => (object?)new List<object?> { p.Item1, p.Item2 }).ToList();

    [Fact]
    public void PlaetzeWiePython()
    {
        var anzahl = 0;
        foreach (var stufe in Soll.GetProperty("stufen").EnumerateArray())
        {
            var wo = stufe.GetProperty("name").GetString()!;
            using var ordner = new TempOrdner();
            var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
            using var v = lernstand.Verbindung();
            Einfuegen(v, stufe.GetProperty("tabellen"));

            var zeilen = Spielstaende.Zeilen(v);
            Gleich(stufe.GetProperty("zeilen"), zeilen.Select(z => (object?)new List<object?> { z.Zeit, z.Uid, z.Platz, z.Lauf, z.Aktion, z.Daten }).ToList(), wo + " slot_rows");
            var alte = Spielstaende.HatAlteEreignisse(v);
            Assert.True(stufe.GetProperty("alte").GetBoolean() == alte, wo + " has_legacy_events");
            var belegung = Spielstaende.Belegung(zeilen, alte);
            Gleich(stufe.GetProperty("belegung"), new PyObjekt
            {
                { "plaetze", Paare(belegung.Plaetze) }, { "namen", Paare(belegung.Namen) },
                { "extra", belegung.Extra.Cast<object?>().ToList() },
                { "geloescht", belegung.Geloescht.Order(StringComparer.Ordinal).Cast<object?>().ToList() },
            }, wo + " slot_layout");
            Gleich(stufe.GetProperty("anzahl"), Spielstaende.AnzahlJeLauf(v).OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => (object?)new List<object?> { p.Key, p.Value }).ToList(), wo + " event_counts");
            foreach (var lauf in stufe.GetProperty("ereignisse").EnumerateObject())
            {
                Gleich(lauf.Value, Spielstaende.LaufEreignisse(v, lauf.Name).Select(e => (object?)new List<object?> { e.Zeit, e.Art, e.Daten }).ToList(), wo + " game_events " + lauf.Name);
            }
            var gespeichert = Spielstaende.GespeicherterLauf(v);
            Gleich(stufe.GetProperty("gespeichert"), gespeichert, wo + " get_meta");
            var aktiv = Spielstaende.AktiverLauf(gespeichert, belegung);
            Gleich(stufe.GetProperty("aktiv"), aktiv, wo + " _resolve_run");
            var plaetze = Spielstaende.Uebersicht(v, aktiv, SpielInhaltTests.Inhalt);
            Gleich(stufe.GetProperty("plaetze"), plaetze.Cast<object?>().ToList(), wo + " slots");
            Gleich(stufe.GetProperty("zeilen_text"), plaetze.Select(p => (object?)Spielstaende.Zeile(p)).ToList(), wo + " slot_line");
            anzahl++;
        }
        Assert.True(anzahl >= 10, anzahl + " Stufen");
    }
}
