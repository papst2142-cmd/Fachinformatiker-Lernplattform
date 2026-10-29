using Lernplattform.Core.Daten;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Dashboard;

/// <summary>
/// Phase 8 W2b (Sammelliste 114): Kasten LERNSTATUS der Seitenleiste am PC wie Python 0.63.2
/// (Sidebar.update_status: distinct_cards_learned + distinct_quiz_questions von content_totals).
/// Dieselben Abfragen wie <see cref="DashboardWerte"/> (Bearbeitet, InhalteGesamt), nur lesend;
/// fehlt die Datei oder scheitert eine Abfrage, zaehlt der Teil 0 (wie im Dashboard).
/// </summary>
public static class Lernstatus
{
    public static (long Bearbeitet, int Gesamt) Lesen(string dbPfad, DashboardInhalte inhalte)
    {
        if (!File.Exists(dbPfad))
        {
            return (0, inhalte.Summe);
        }
        try
        {
            using var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPfad,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = Lernstand.Wartezeit,
            }.ToString());
            verbindung.Open();
            // Phase 9 W2: bei AE, DPA und DV nur Fragen der Sicht (wie die Kachel im Dashboard), SI wie Python
            return (Anzahl(verbindung, "card_events", inhalte.SichtKarten)
                + Anzahl(verbindung, "quiz_answers", inhalte.SichtFragen), inhalte.Summe);
        }
        catch (SqliteException)
        {
            return (0, inhalte.Summe);
        }
    }

    private static long Anzahl(SqliteConnection verbindung, string tabelle, HashSet<string>? sichtbar)
    {
        try
        {
            using var befehl = verbindung.CreateCommand();
            if (sichtbar is null)
            {
                befehl.CommandText = $"SELECT COUNT(DISTINCT question) FROM {tabelle}";
                return befehl.ExecuteScalar() is long n ? n : 0;
            }
            befehl.CommandText = $"SELECT DISTINCT question FROM {tabelle}";
            using var leser = befehl.ExecuteReader();
            long anzahl = 0;
            while (leser.Read())
            {
                if (!leser.IsDBNull(0) && leser.GetValue(0) is string frage && sichtbar.Contains(frage))
                {
                    anzahl++;
                }
            }
            return anzahl;
        }
        catch (SqliteException)
        {
            return 0;
        }
    }
}
