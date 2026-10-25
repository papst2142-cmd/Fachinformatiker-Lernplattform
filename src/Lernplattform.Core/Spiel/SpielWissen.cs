using System.Globalization;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Mathe;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W6: der Wissensstand des Spiels aus dem Lernstand (Python 0.63.2 fisi_game.knowledge,
/// topic_knowledge, knowledge_from_answers, game_items und Game.learned_projects mit den Abfragen aus
/// fisi_core: category_coverage, topic_answers, topic_coverage, completed_projects). Wird nie
/// gespeichert, sondern bei Bedarf neu berechnet; die Datenbank wird nur lesend geoeffnet. Fehlt sie
/// oder scheitert eine Abfrage, zaehlt das wie Pythons Vorgabewert (keine Zeilen).
/// </summary>
public sealed class SpielWissen(string datenbank, Lerninhalte lernen, PyObjekt balancing)
{
    private readonly Dictionary<string, string?> kartenThema = Thema(lernen.Karteikarten);
    private readonly Dictionary<string, string?> fragenThema = Thema(lernen.Quizfragen);

    /// <summary>knowledge(db, balancing["wissen"]): Wissensstand je Fachbereich (Kurzname).</summary>
    public PyObjekt Fachbereiche()
    {
        var werte = Parameter(false);
        using var db = Leser.Oeffnen(datenbank);
        var antworten = new Dictionary<string, List<bool>>(StringComparer.Ordinal);
        foreach (var (kurz, name) in Inhalte.Fachbereiche.Schluessel)
        {
            antworten[kurz] = [.. db.Abfrage(
                "SELECT correct FROM ("
                + "  SELECT timestamp, id, correct, 0 AS src FROM quiz_answers WHERE category = ?1"
                + "  UNION ALL"
                + "  SELECT timestamp, id, correct, 1 AS src FROM card_events"
                + "  WHERE category = ?1 AND correct IS NOT NULL"
                + ") ORDER BY timestamp DESC, id DESC LIMIT ?2", name, (long)werte.Fenster)
                .Select(z => PyText.Wahr(z[0]))];
        }

        // category_coverage(game_content_totals(db))
        var summen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var eintrag in SpielInhalte(db))
        {
            if (eintrag.Fachbereich is { } fach)
            {
                summen[fach] = summen.GetValueOrDefault(fach) + 1;
            }
        }
        var gesehen = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT category, COUNT(DISTINCT question) FROM ("
            + "  SELECT category, question FROM quiz_answers"
            + "  UNION"
            + "  SELECT category, question FROM card_events"
            + ") GROUP BY category"))
        {
            if (zeile[0] is string fach)
            {
                gesehen[fach] = Convert.ToInt64(zeile[1], CultureInfo.InvariantCulture);
            }
        }
        var abdeckung = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (kurz, name) in Inhalte.Fachbereiche.Schluessel)
        {
            var summe = Math.Max(1, summen.TryGetValue(name, out var s) ? s : 1);
            abdeckung[kurz] = Math.Min(100.0, gesehen.GetValueOrDefault(name) / (double)summe * 100.0);
        }
        return AusAntworten(antworten, abdeckung, werte, Inhalte.Fachbereiche.Schluessel.Keys);
    }

    /// <summary>topic_knowledge(db, topic_params(balancing)): Wissensstand je Thema.</summary>
    public PyObjekt Themen()
    {
        var werte = Parameter(true);
        using var db = Leser.Oeffnen(datenbank);

        // topic_answers (neueste zuerst, hoechstens Fenster je Thema)
        var antworten = new Dictionary<string, List<bool>>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT src, question, correct FROM ("
            + "  SELECT timestamp, id, 0 AS src, question, correct FROM quiz_answers"
            + "  UNION ALL"
            + "  SELECT timestamp, id, 1 AS src, question, correct FROM card_events"
            + "  WHERE correct IS NOT NULL"
            + ") ORDER BY timestamp DESC, id DESC"))
        {
            if (ThemaVon(zeile[0], zeile[1]) is not { } thema)
            {
                continue;
            }
            if (!antworten.TryGetValue(thema, out var eimer))
            {
                antworten[thema] = eimer = [];
            }
            if (eimer.Count < werte.Fenster)
            {
                eimer.Add(PyText.Wahr(zeile[2]));
            }
        }

        // topic_coverage(game_topic_totals(db))
        var summen = lernen.ThemenReihenfolge.ToDictionary(t => t, _ => 0, StringComparer.Ordinal);
        foreach (var eintrag in SpielInhalte(db))
        {
            if (eintrag.Thema is { } thema && summen.ContainsKey(thema))
            {
                summen[thema]++;
            }
        }
        var gesehen = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT DISTINCT 0, question FROM quiz_answers"
            + " UNION SELECT DISTINCT 1, question FROM card_events"))
        {
            if (ThemaVon(zeile[0], zeile[1]) is { } thema)
            {
                gesehen[thema] = gesehen.GetValueOrDefault(thema) + 1;
            }
        }
        var abdeckung = lernen.ThemenReihenfolge.ToDictionary(t => t,
            t => Math.Min(100.0, gesehen.GetValueOrDefault(t) / (double)Math.Max(1, summen.GetValueOrDefault(t, 1)) * 100.0),
            StringComparer.Ordinal);
        return AusAntworten(antworten, abdeckung, werte, lernen.ThemenReihenfolge);
    }

    /// <summary>Game.learned_projects: Nummern der im Lernbereich bearbeiteten Testprojekte.</summary>
    public PySet Projekte()
    {
        using var db = Leser.Oeffnen(datenbank);
        return new PySet(db.Abfrage("SELECT DISTINCT project_index FROM project_events").Select(z => z[0]));
    }

    /// <summary>knowledge_from_answers: Erfolgsquote (neuere Antworten zaehlen staerker) mal Abdeckung hoch Exponent.</summary>
    private static PyObjekt AusAntworten(Dictionary<string, List<bool>> antworten, Dictionary<string, double> abdeckung,
        (int Fenster, int Mindest, double Voll, double Exponent) werte, IEnumerable<string> schluessel)
    {
        var fenster = Math.Max(1, werte.Fenster);
        var mindest = Math.Max(1, werte.Mindest);
        var voll = Math.Max(1.0, werte.Voll);
        var ergebnis = new PyObjekt();
        foreach (var name in schluessel)
        {
            var juengste = antworten.TryGetValue(name, out var liste) ? liste.Take(fenster).ToList() : [];
            var quote = 0.0;
            if (juengste.Count > 0)
            {
                long richtig = 0, summe = 0;
                for (var i = 0; i < juengste.Count; i++)
                {
                    var gewicht = juengste.Count - i;
                    summe += gewicht;
                    if (juengste[i])
                    {
                        richtig += gewicht;
                    }
                }
                quote = richtig / (double)summe;
                quote *= Math.Min(1.0, juengste.Count / (double)mindest);
            }
            var anteil = Math.Min(1.0, Math.Max(0.0, abdeckung.GetValueOrDefault(name)) / voll);
            ergebnis.Add(name, PyText.Runde1(100.0 * quote * PythonPow.Pow(anteil, werte.Exponent)));
        }
        return ergebnis;
    }

    /// <summary>balancing["wissen"], fuer Themen mit den Abweichungen aus "wissen_thema" (topic_params).</summary>
    private (int Fenster, int Mindest, double Voll, double Exponent) Parameter(bool themen)
    {
        var werte = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, wert) in (PyObjekt)balancing["wissen"]!)
        {
            werte[name] = wert;
        }
        if (themen && balancing["wissen_thema"] is PyObjekt abweichung)
        {
            foreach (var (name, wert) in abweichung)
            {
                if (!name.StartsWith('_'))
                {
                    werte[name] = wert;
                }
            }
        }
        double Zahl(string name) => Convert.ToDouble(werte[name], CultureInfo.InvariantCulture);
        return ((int)Zahl("letzte_antworten"), (int)Zahl("mindest_antworten"), Zahl("volle_abdeckung_prozent"), Zahl("abdeckung_exponent"));
    }

    /// <summary>game_items: neue Inhalte (Feld neu_ab) zaehlen erst, wenn sie schon bearbeitet wurden.</summary>
    private IEnumerable<Lerninhalt> SpielInhalte(Leser db)
    {
        HashSet<string> Bearbeitet(string tabelle, IReadOnlyList<Lerninhalt> eintraege) => eintraege.Any(Neu)
            ? db.Abfrage($"SELECT DISTINCT question FROM {tabelle}").Select(z => z[0]).OfType<string>().ToHashSet(StringComparer.Ordinal)
            : [];
        var karten = Bearbeitet("card_events", lernen.Karteikarten);
        var fragen = Bearbeitet("quiz_answers", lernen.Quizfragen);
        return lernen.Karteikarten.Where(k => !Neu(k) || karten.Contains(k.Frage!))
            .Concat(lernen.Quizfragen.Where(f => !Neu(f) || fragen.Contains(f.Frage!)));
    }

    private static bool Neu(Lerninhalt eintrag) => Einstellungen.PythonBool(eintrag.Daten["neu_ab"]);

    /// <summary>event_topic: das Thema einer Antwort ergibt sich aus der Frage (1 = Karteikarte, 0 = Quiz).</summary>
    private string? ThemaVon(object? quelle, object? frage) =>
        frage is string text && (PyText.Wahr(quelle) ? kartenThema : fragenThema).TryGetValue(text, out var thema) ? thema : null;

    private static Dictionary<string, string?> Thema(IReadOnlyList<Lerninhalt> eintraege)
    {
        var ergebnis = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var eintrag in eintraege)
        {
            ergebnis[eintrag.Frage!] = eintrag.Thema;
        }
        return ergebnis;
    }

    /// <summary>Nur lesende Verbindung; eine fehlende Datei oder eine gescheiterte Abfrage liefert keine Zeilen.</summary>
    private sealed class Leser(SqliteConnection? verbindung) : IDisposable
    {
        public static Leser Oeffnen(string pfad)
        {
            if (!File.Exists(pfad))
            {
                return new Leser(null);
            }
            var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = pfad,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = Lernstand.Wartezeit,
            }.ToString());
            try
            {
                verbindung.Open();
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = "PRAGMA query_only = ON";
                befehl.ExecuteNonQuery();
                return new Leser(verbindung);
            }
            catch (SqliteException)
            {
                verbindung.Dispose();
                return new Leser(null);
            }
        }

        public List<object?[]> Abfrage(string sql, params object[] werte)
        {
            if (verbindung is null)
            {
                return [];
            }
            try
            {
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = sql;
                for (var i = 0; i < werte.Length; i++)
                {
                    befehl.Parameters.AddWithValue("?" + (i + 1).ToString(CultureInfo.InvariantCulture), werte[i]);
                }
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
            catch (SqliteException)
            {
                return [];
            }
        }

        public void Dispose() => verbindung?.Dispose();
    }
}
