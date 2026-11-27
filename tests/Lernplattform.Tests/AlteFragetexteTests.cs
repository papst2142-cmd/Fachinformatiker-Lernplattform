using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pruefung;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.18.0 (L0, Weisung P12-424 Nico 2b, Plan P12-269 Abschnitt 3): alte Fragetexte im Lernstand werden beim Start einmal
/// auf die heutigen umgeschrieben (Kopie vorher), danach gibt es keine Zuordnungen alt -&gt; neu mehr. Gegenproben: gleiches
/// Ergebnis wie der bisherige Weg (UPDATE je Zuordnung), nichts verloren, zweiter Start aendert nichts, ohne Kopie nichts.
/// Nur Testdaten (A3).
/// </summary>
public class AlteFragetexteTests
{
    private static readonly Uhr Feste = new(() => new DateTimeOffset(2026, 11, 27, 15, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    /// <summary>
    /// SHA-256 der Zuordnungen (Zeilen "tabelle\talt\tneu\n"): die 55 aus inhalte/umbenennungen.json (Python v0.63.2, Stand vor
    /// L0) in derselben Reihenfolge plus die FIAE-Quizfrage Fabrikmethode (ausschluss.json Stelle 231 -&gt; 6360). Am 27.11.2026
    /// gegen die Datei geprueft; die Zuordnungen aendern sich nie mehr.
    /// </summary>
    private const string ZuordnungenSha = "9768a697a0eced5666be50bef8e35b240b58c29f1f48608af83552b3d8692b2f";

    private static string Sha(IEnumerable<(string Tabelle, string Alt, string Neu)> liste) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(liste.Select(z => $"{z.Tabelle}\t{z.Alt}\t{z.Neu}\n")))));

    [Fact]
    public void Zuordnungen_GenauDie56_FesteReihenfolge()
    {
        var z = AlteFragetexte.Zuordnungen;
        Assert.Equal(22, z.Count(x => x.Tabelle == "card_events"));
        Assert.Equal(34, z.Count(x => x.Tabelle == "quiz_answers"));
        Assert.Equal(ZuordnungenSha, Sha(z));
        // keine Ketten, kein alter Text doppelt, kein heutiger Text gleich einem alten
        Assert.Equal(z.Count, z.Select(x => (x.Tabelle, x.Alt)).Distinct().Count());
        Assert.DoesNotContain(z, x => z.Any(y => y.Tabelle == x.Tabelle && y.Alt == x.Neu));
    }

    private static void Ausfuehren(string pfad, string sql, params (string, object?)[] werte)
    {
        using var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False");
        verbindung.Open();
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        foreach (var (name, wert) in werte)
        {
            befehl.Parameters.AddWithValue(name, wert ?? DBNull.Value);
        }
        befehl.ExecuteNonQuery();
    }

    /// <summary>Alle Zeilen der drei Tabellen als Text (Tabelle, uid, Zeit, Spalten), sortiert.</summary>
    private static List<string> Zeilen(string pfad)
    {
        using var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False;Mode=ReadOnly");
        verbindung.Open();
        var zeilen = new List<string>();
        foreach (var sql in new[]
        {
            "SELECT 'card_events', uid, timestamp, category, question, mode, correct FROM card_events",
            "SELECT 'quiz_answers', uid, timestamp, category, question, '', correct FROM quiz_answers",
            "SELECT 'pruefungen', uid, timestamp, art, daten, punkte, note FROM pruefungen",
        })
        {
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = sql;
            using var leser = befehl.ExecuteReader();
            while (leser.Read())
            {
                zeilen.Add(string.Join("|", Enumerable.Range(0, 7).Select(i => leser.IsDBNull(i) ? "null" : Convert.ToString(leser.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
            }
        }
        zeilen.Sort(StringComparer.Ordinal);
        return zeilen;
    }

    private const string Heute = "Heutige Frage, die bleibt?";

    /// <summary>
    /// Lernstand mit jedem alten Text (Karte und Quiz) zweimal, heutigen Texten, einer WiSo-Pruefung mit alten Texten (als Liste
    /// und als Schluessel der Antworten) und einer Pruefung ohne alte Texte. Rueckgabe: Zahl der Zeilen mit altem Text.
    /// </summary>
    private static int Alt(string pfad)
    {
        _ = new Lernstand(pfad);
        var n = 0;
        foreach (var (tabelle, alt, neu) in AlteFragetexte.Zuordnungen)
        {
            foreach (var richtig in new[] { 0, 1 })
            {
                n++;
                Ausfuehren(pfad, tabelle == "card_events"
                    ? "INSERT INTO card_events (timestamp, utc_offset, category, question, mode, correct, uid) VALUES ($t, 60, 'k', $q, 'karte', $r, $u)"
                    : "INSERT INTO quiz_answers (timestamp, utc_offset, category, question, correct, uid) VALUES ($t, 60, 'k', $q, $r, $u)",
                    ("$t", $"2026-10-01 10:{n % 60:00}:00"), ("$q", alt), ("$r", richtig), ("$u", "u" + n));
            }
            // dieselbe Frage schon mit heutigem Text: beide Zeilen bleiben (wie bisher, nichts zusammengelegt)
            Ausfuehren(pfad, tabelle == "card_events"
                ? "INSERT INTO card_events (timestamp, utc_offset, category, question, mode, correct, uid) VALUES ('2026-10-02 10:00:00', 60, 'k', $q, 'karte', 1, $u)"
                : "INSERT INTO quiz_answers (timestamp, utc_offset, category, question, correct, uid) VALUES ('2026-10-02 10:00:00', 60, 'k', $q, 1, $u)",
                ("$q", neu), ("$u", "h" + n));
        }
        Ausfuehren(pfad, "INSERT INTO quiz_answers (timestamp, utc_offset, category, question, correct, uid) VALUES ('2026-10-03 10:00:00', 60, 'k', $q, 1, 'x1')",
            ("$q", Heute));
        var quiz = AlteFragetexte.Zuordnungen.Where(z => z.Tabelle == "quiz_answers").ToList();
        var daten = PythonJson.Text(new PyObjekt
        {
            { "fragen", new List<object?> { quiz[0].Alt, Heute, new PyObjekt { { "q", quiz[1].Alt } } } },
            { "antworten", new PyObjekt { { quiz[0].Alt, "A" }, { Heute, "B" } } },
            { "hinweis", "Text mit \"" + quiz[2].Alt + "\" mitten drin bleibt" },
        });
        Ausfuehren(pfad, "INSERT INTO pruefungen (timestamp, utc_offset, art, punkte, note, dauer, daten, uid) VALUES ('2026-10-04 10:00:00', 60, 'wiso', 50.0, '3', 60, $d, 'p1')",
            ("$d", daten));
        Ausfuehren(pfad, "INSERT INTO pruefungen (timestamp, utc_offset, art, punkte, note, dauer, daten, uid) VALUES ('2026-10-05 10:00:00', 60, 'wiso', 50.0, '3', 60, $d, 'p2')",
            ("$d", PythonJson.Text(new PyObjekt { { "fragen", new List<object?> { Heute } } })));
        return n + 1;
    }

    /// <summary>Erwartet: jede Zeile wie der bisherige Weg (je Zuordnung UPDATE ... WHERE question = alt), alles andere gleich.</summary>
    private static List<string> Erwartet(List<string> vorher)
    {
        var ergebnis = new List<string>();
        foreach (var zeile in vorher)
        {
            var teile = zeile.Split('|');
            if (teile[0] is "card_events" or "quiz_answers")
            {
                var tabelle = teile[0];
                teile[4] = AlteFragetexte.Zuordnungen.Where(z => z.Tabelle == tabelle && z.Alt == teile[4]).Select(z => z.Neu).FirstOrDefault() ?? teile[4];
            }
            ergebnis.Add(string.Join("|", teile));
        }
        ergebnis.Sort(StringComparer.Ordinal);
        return ergebnis;
    }

    [Fact]
    public void Umschreiben_WieBisher_NichtsVerloren_KopieVorher_ZweiterStartNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var alte = Alt(pfad);
        var vorher = Zeilen(pfad);

        var fund = AlteFragetexte.Suchen(new Lernstand(pfad));
        Assert.Equal((44, 68, 1), (fund.Karten, fund.Fragen, fund.Pruefungen.Count));
        Assert.Equal(alte, fund.Zeilen);
        var ergebnis = AlteFragetexte.Ausfuehren(new Lernstand(pfad), Feste);
        Assert.Equal((alte, alte), (ergebnis.Gefunden, ergebnis.Umgeschrieben));

        var nachher = Zeilen(pfad);
        // Gegenprobe: ohne Umschreiben waere das Ergebnis anders (die Testdaten enthalten alte Texte)
        Assert.NotEqual(vorher, nachher);
        // nichts verloren, nichts dazu: gleiche Zahl je Tabelle, gleiche uid und Zeit
        Assert.Equal(vorher.Count, nachher.Count);
        Assert.Equal(vorher.Select(z => string.Join("|", z.Split('|')[..3])), nachher.Select(z => string.Join("|", z.Split('|')[..3])).Order(StringComparer.Ordinal));
        // Karten und Quiz genau wie der bisherige Weg
        Assert.Equal(Erwartet(vorher).Where(z => !z.StartsWith("pruefungen", StringComparison.Ordinal)),
            nachher.Where(z => !z.StartsWith("pruefungen", StringComparison.Ordinal)));
        // WiSo-Pruefung: alte Texte heute, ein Text mitten in einem anderen bleibt; die Pruefung ohne alte Texte bleibt gleich
        var quiz = AlteFragetexte.Zuordnungen.Where(z => z.Tabelle == "quiz_answers").ToList();
        var p1 = JsonNode.Parse(nachher.Single(z => z.Contains("|p1|", StringComparison.Ordinal)).Split('|')[4])!;
        Assert.Equal(quiz[0].Neu, p1["fragen"]![0]!.GetValue<string>());
        Assert.Equal(quiz[1].Neu, p1["fragen"]![2]!["q"]!.GetValue<string>());
        Assert.Equal("A", p1["antworten"]![quiz[0].Neu]!.GetValue<string>());
        Assert.Contains(quiz[2].Alt, p1["hinweis"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(vorher.Single(z => z.Contains("|p2|", StringComparison.Ordinal)), nachher.Single(z => z.Contains("|p2|", StringComparison.Ordinal)));

        // Kopie vorher mit dem alten Stand, Zeile im Update-Protokoll
        Assert.Equal(Path.Combine(ordner.Pfad, "sicherungen", "vor_umschreiben_2026-11-27_150000.db"), ergebnis.Kopie);
        Assert.Equal(vorher, Zeilen(ergebnis.Kopie!));
        Assert.Contains(AlteFragetexte.Texte.Umgeschrieben(alte, "sicherungen/vor_umschreiben_2026-11-27_150000.db"),
            File.ReadAllText(Path.Combine(ordner.Pfad, "update.log")), StringComparison.Ordinal);

        // zweiter Start: nichts mehr zu tun, keine neue Kopie, Datei unveraendert
        var bytes = File.ReadAllBytes(pfad);
        var zweiter = AlteFragetexte.Ausfuehren(new Lernstand(pfad), Feste);
        Assert.Equal((0, 0, null), (zweiter.Gefunden, zweiter.Umgeschrieben, zweiter.Kopie));
        Assert.Single(Directory.GetFiles(Path.Combine(ordner.Pfad, "sicherungen")));
        Assert.Equal(bytes, File.ReadAllBytes(pfad));
    }

    [Fact]
    public void Umschreiben_OhneKopie_KeineAenderung()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var alte = Alt(pfad);
        var vorher = Zeilen(pfad);
        File.WriteAllText(Path.Combine(ordner.Pfad, "sicherungen"), "kein Ordner");   // Kopie scheitert

        var ergebnis = AlteFragetexte.Ausfuehren(new Lernstand(pfad), Feste);

        Assert.Equal((alte, 0, null), (ergebnis.Gefunden, ergebnis.Umgeschrieben, ergebnis.Kopie));
        Assert.Equal(vorher, Zeilen(pfad));
        Assert.Equal(AlteFragetexte.Texte.KopieFehlt(alte), ergebnis.Protokoll);
    }

    [Fact]
    public void Umschreiben_HeutigerLernstand_NichtsZuTun_DateiUnveraendert()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        Ausfuehren(pfad, "INSERT INTO quiz_answers (timestamp, utc_offset, category, question, correct, uid) VALUES ('2026-10-03 10:00:00', 60, 'k', $q, 1, 'x1')",
            ("$q", Heute));
        var bytes = File.ReadAllBytes(pfad);

        var ergebnis = AlteFragetexte.Ausfuehren(new Lernstand(pfad), Feste);

        Assert.Equal((0, 0, null), (ergebnis.Gefunden, ergebnis.Umgeschrieben, ergebnis.Kopie));
        Assert.Equal(bytes, File.ReadAllBytes(pfad));
        Assert.False(Directory.Exists(Path.Combine(ordner.Pfad, "sicherungen")));
    }

    [Fact]
    public void LaufendeWiso_AlteTexteHeute_AntwortenMitgenommen_AndereArtenBleiben()
    {
        var quiz = AlteFragetexte.Zuordnungen.Where(z => z.Tabelle == "quiz_answers").ToList();
        var werte = new JsonObject
        {
            [LaufendePruefung.Schluessel] = new JsonObject
            {
                ["art"] = Klausur.Wiso,
                ["fragen"] = new JsonArray(quiz[0].Alt, Heute, quiz[1].Alt),
                ["antworten"] = new JsonObject { [quiz[0].Alt] = "A", [quiz[1].Alt] = "X", [quiz[1].Neu] = "schon da" },
                ["optionen"] = new JsonObject { [quiz[0].Alt] = new JsonArray("a", "b") },
            },
        };
        Assert.True(AlteFragetexte.LaufendeUmschreiben(werte));
        var z = werte[LaufendePruefung.Schluessel]!;
        Assert.Equal([quiz[0].Neu, Heute, quiz[1].Neu], z["fragen"]!.AsArray().Select(k => k!.GetValue<string>()));
        Assert.Equal("A", z["antworten"]![quiz[0].Neu]!.GetValue<string>());
        Assert.Equal("schon da", z["antworten"]![quiz[1].Neu]!.GetValue<string>());   // vorhandener neuer Schluessel bleibt (wie migrate_running)
        Assert.NotNull(z["optionen"]![quiz[0].Neu]);
        // zweites Mal: nichts mehr
        Assert.False(AlteFragetexte.LaufendeUmschreiben(werte));
        // andere Art bleibt unberuehrt
        var andere = new JsonObject { [LaufendePruefung.Schluessel] = new JsonObject { ["art"] = "konzeption", ["fragen"] = new JsonArray(quiz[0].Alt) } };
        Assert.False(AlteFragetexte.LaufendeUmschreiben(andere));
        Assert.Equal(quiz[0].Alt, andere[LaufendePruefung.Schluessel]!["fragen"]![0]!.GetValue<string>());
    }
}
