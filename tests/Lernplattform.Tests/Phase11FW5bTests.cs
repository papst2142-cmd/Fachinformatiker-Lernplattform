using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 11 FW5b (Weisung P11-11): 186 alte Fehlbuchungen von AE/DPA/DV-Klausuren bereinigen (Kopie vorher, nur
/// belegbare Zeilen, SI-eigene Zeilen bleiben). Nur Testdaten (A3).
/// </summary>
public class Phase11FW5bTests
{
    private static readonly string Lernen = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");

    private static Lerninhalte Echt() => Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte),
        name => Path.Combine(Lernen, name + ".json") is var pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null);

    private static readonly Uhr Feste = new(() => new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    // -- 186 ------------------------------------------------------------------------------------

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

    private static List<(string Uid, long Index)> Ereignisse(string pfad)
    {
        using var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False;Mode=ReadOnly");
        verbindung.Open();
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT uid, scenario_index FROM scenario_events ORDER BY uid";
        using var leser = befehl.ExecuteReader();
        var zeilen = new List<(string, long)>();
        while (leser.Read())
        {
            zeilen.Add((leser.GetString(0), leser.GetInt64(1)));
        }
        return zeilen;
    }

    private static void Ereignis(string pfad, string uid, string zeit, long? abstand, long index, Lerninhalt szenario) =>
        Ausfuehren(pfad, "INSERT INTO scenario_events (timestamp, utc_offset, scenario_index, title, theme, correct, uid)"
            + " VALUES ($t, $o, $i, $ti, $th, 1, $u)", ("$t", zeit), ("$o", abstand), ("$i", index), ("$ti", szenario.Titel),
            ("$th", szenario.SzenarioThema), ("$u", uid));

    private static void Pruefung(string pfad, string zeit, string art, string aufgaben) =>
        Ausfuehren(pfad, "INSERT INTO pruefungen (timestamp, utc_offset, art, punkte, note, dauer, daten, uid)"
            + " VALUES ($t, 60, $a, 50.0, 'x', 60, $d, $u)", ("$t", zeit), ("$a", art),
            ("$d", "{\"aufgaben\": [" + aufgaben + "], \"themen\": {}}"), ("$u", "p" + zeit));

    /// <summary>
    /// Lernstand wie vor A 0.32.0: eine AE- und eine DPA-Klausur mit falscher Stelle, dazu SI-eigene Zeilen zur
    /// selben und zu anderer Zeit. Rueckgabe: richtige Stellen der drei falschen Zeilen.
    /// </summary>
    private static (long Ae0, long Ae1, long Dpa0) Alt(string pfad, Lerninhalte alle)
    {
        var si = alle.Ap2SzenarienFuer("si");
        var ae = alle.Ap2SzenarienFuer("ae");
        var dpa = alle.Ap2SzenarienFuer("dpa");
        _ = new Lernstand(pfad);
        Pruefung(pfad, "2026-10-31 10:00:00", "softwareprodukt",
            "{\"quelle\": \"ap2_ae\", \"index\": 0, \"punkte\": 20, \"max\": 25}, {\"quelle\": \"ap2_ae\", \"index\": 1, \"punkte\": 5, \"max\": 25}");
        Pruefung(pfad, "2026-10-31 11:00:00", "konzeption", "{\"quelle\": \"ap2\", \"index\": 0, \"punkte\": 20, \"max\": 25}");
        Pruefung(pfad, "2026-10-31 12:00:00", Fachrichtung.Nach("dpa").Bereiche.First(b => b.Rolle == "b1").Art,
            "{\"quelle\": \"ap2_dpa\", \"index\": 0, \"punkte\": 20, \"max\": 25}");
        Ereignis(pfad, "e1", "2026-10-31 10:00:00", 60, 0, ae[0]);    // falsch: AE-Aufgabe auf SI-Stelle 0
        Ereignis(pfad, "e2", "2026-10-31 10:00:00", 60, 1, ae[1]);    // falsch
        Ereignis(pfad, "e3", "2026-10-31 10:00:00", 60, 0, si[0]);    // SI-eigene Zeile zur selben Zeit: bleibt
        Ereignis(pfad, "e4", "2026-10-31 09:00:00", 60, 0, ae[0]);    // andere Zeit: nicht belegbar, bleibt
        Ereignis(pfad, "e5", "2026-10-31 10:00:00", null, 0, ae[0]);  // anderer Abstand: bleibt
        Ereignis(pfad, "e6", "2026-10-31 11:00:00", 60, 0, si[0]);    // SI-Klausur: bleibt
        Ereignis(pfad, "e7", "2026-10-31 12:00:00", 60, 0, dpa[0]);   // falsch (DPA)
        Ereignis(pfad, "e8", "2026-10-30 08:00:00", 60, 3, si[3]);    // SI-Uebung: bleibt
        return (ae[0].Nummer - 1, ae[1].Nummer - 1, dpa[0].Nummer - 1);
    }

    [Fact]
    public void Bereinigung_NurBelegbareZeilen_KopieVorher_SiBleibt()
    {
        var alle = Echt();
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var (ae0, ae1, dpa0) = Alt(pfad, alle);
        var vorher = Ereignisse(pfad);
        Assert.True(ae0 >= alle.Ap2SzenarienFuer("si").Count && dpa0 >= alle.Ap2SzenarienFuer("si").Count);

        var lernstand = new Lernstand(pfad);
        Assert.Equal(3, Bereinigung.Suchen(lernstand, alle).Count);
        var ergebnis = Bereinigung.Ausfuehren(lernstand, alle, Feste);

        Assert.Equal((3, 3), (ergebnis.Gefunden, ergebnis.Berichtigt));
        // nur die drei belegbaren Zeilen, Kennungen (uid) bleiben
        Assert.Equal([("e1", ae0), ("e2", ae1), ("e3", 0L), ("e4", 0L), ("e5", 0L), ("e6", 0L), ("e7", dpa0), ("e8", 3L)],
            Ereignisse(pfad));
        // Kopie vorher mit dem alten Stand
        Assert.NotNull(ergebnis.Kopie);
        Assert.Equal(Path.Combine(ordner.Pfad, "sicherungen", "vor_bereinigung_2026-11-02_140000.db"), ergebnis.Kopie);
        Assert.Equal(vorher, Ereignisse(ergebnis.Kopie!));
        var protokoll = File.ReadAllText(Path.Combine(ordner.Pfad, "update.log"));
        Assert.Contains(Bereinigung.Texte.Berichtigt(3, "sicherungen/vor_bereinigung_2026-11-02_140000.db"), protokoll, StringComparison.Ordinal);

        // zweiter Start: nichts mehr zu tun, keine neue Kopie
        var zweiter = Bereinigung.Ausfuehren(new Lernstand(pfad), alle, Feste);
        Assert.Equal((0, 0, null), (zweiter.Gefunden, zweiter.Berichtigt, zweiter.Kopie));
        Assert.Single(Directory.GetFiles(Path.Combine(ordner.Pfad, "sicherungen")));
    }

    [Fact]
    public void Bereinigung_OhneKopie_KeineAenderung()
    {
        var alle = Echt();
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = Alt(pfad, alle);
        var vorher = Ereignisse(pfad);
        File.WriteAllText(Path.Combine(ordner.Pfad, "sicherungen"), "kein Ordner");   // Kopie scheitert

        var ergebnis = Bereinigung.Ausfuehren(new Lernstand(pfad), alle, Feste);

        Assert.Equal((3, 0, null), (ergebnis.Gefunden, ergebnis.Berichtigt, ergebnis.Kopie));
        Assert.Equal(vorher, Ereignisse(pfad));
        Assert.Equal(Bereinigung.Texte.KopieFehlt(3), ergebnis.Protokoll);
    }

    [Fact]
    public void Bereinigung_LernstandOhneFachrichtungsKlausur_NichtsZuTun()
    {
        var alle = Echt();
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        Pruefung(pfad, "2026-10-31 11:00:00", "konzeption", "{\"quelle\": \"ap2\", \"index\": 0, \"punkte\": 20, \"max\": 25}");
        Ereignis(pfad, "e6", "2026-10-31 11:00:00", 60, 0, alle.Ap2SzenarienFuer("si")[0]);

        var ergebnis = Bereinigung.Ausfuehren(new Lernstand(pfad), alle, Feste);

        Assert.Equal((0, 0, null), (ergebnis.Gefunden, ergebnis.Berichtigt, ergebnis.Kopie));
        Assert.False(Directory.Exists(Path.Combine(ordner.Pfad, "sicherungen")));
        Assert.False(File.Exists(Path.Combine(ordner.Pfad, "update.log")));
    }
}
