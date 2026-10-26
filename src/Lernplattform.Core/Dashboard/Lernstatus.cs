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
            return (Anzahl(verbindung, "SELECT COUNT(DISTINCT question) FROM card_events")
                + Anzahl(verbindung, "SELECT COUNT(DISTINCT question) FROM quiz_answers"), inhalte.Summe);
        }
        catch (SqliteException)
        {
            return (0, inhalte.Summe);
        }
    }

    private static long Anzahl(SqliteConnection verbindung, string sql)
    {
        try
        {
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = sql;
            return befehl.ExecuteScalar() is long n ? n : 0;
        }
        catch (SqliteException)
        {
            return 0;
        }
    }
}
