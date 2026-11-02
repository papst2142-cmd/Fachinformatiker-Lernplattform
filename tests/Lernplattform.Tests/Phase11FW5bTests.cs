using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;
using System.Text.Json.Nodes;
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

    // -- 169 ------------------------------------------------------------------------------------

    [Fact]
    public void Dashboard_AeZaehltQuoteVerlaufUndFachbereicheNurInDerSicht_SiAlles()
    {
        var alle = Echt();
        var si = alle.Fuer("si");
        var ae = alle.Fuer("ae");
        var fragenAe = ae.Quizfragen.Select(q => q.Frage).ToHashSet();
        var kartenAe = ae.Karteikarten.Select(k => k.Frage).ToHashSet();
        var frageAe = ae.Quizfragen[0];
        var frageSi = si.Quizfragen.First(q => !fragenAe.Contains(q.Frage));
        var karteAe = ae.Karteikarten[0];
        var karteSi = si.Karteikarten.First(k => !kartenAe.Contains(k.Frage));
        var szenarioAe = ae.Ap2Szenarien.First(s => s.Zugehoerigkeit == "ae");
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        const string Zeit = "2026-11-02 09:00:00";
        void Frage(Lerninhalt f, int richtig) => Ausfuehren(pfad, "INSERT INTO quiz_answers (timestamp, category, question, correct, uid)"
            + " VALUES ($t, $c, $q, $r, lower(hex(randomblob(8))))", ("$t", Zeit), ("$c", f.Fachbereich), ("$q", f.Frage), ("$r", richtig));
        void Karte(Lerninhalt k, int richtig) => Ausfuehren(pfad, "INSERT INTO card_events (timestamp, category, question, mode, correct, uid)"
            + " VALUES ($t, $c, $q, 'lernen', $r, lower(hex(randomblob(8))))", ("$t", Zeit), ("$c", k.Fachbereich), ("$q", k.Frage), ("$r", richtig));
        Frage(frageAe, 1);
        Frage(frageSi, 0);
        Frage(frageSi, 0);
        Karte(karteAe, 1);
        Karte(karteSi, 0);
        Ereignis(pfad, "s1", Zeit, 60, szenarioAe.Nummer - 1, szenarioAe);
        Ereignis(pfad, "s2", Zeit, 60, 0, si.Ap2Szenarien[0]);
        Ausfuehren(pfad, "INSERT INTO trainer_aufgaben (timestamp, art, stufe, correct, uid) VALUES ($t, 'subnetz', 1, 1, 't1')", ("$t", Zeit));

        var heute = new DateOnly(2026, 11, 2);
        var ziel = Einstellungen.Ziel(new JsonObject());
        DashboardWerte Lies(Lerninhalte sicht) => DashboardWerte.Lesen(pfad, heute, new DashboardInhalte(sicht), ziel, null,
            Path.Combine(ordner.Pfad, "fehler.log"));
        long Fach(DashboardWerte w) => w.Fachbereiche.Sum(f => f.Antworten);
        long Erwartet(params Lerninhalt[] beantwortet) => beantwortet.Count(e => Fachbereiche.Alle.Contains(e.Fachbereich!));

        var wAe = Lies(ae);
        Assert.Equal((1L, 1.0, 100.0), (wAe.Quote.Antworten, wAe.Quote.Richtig, wAe.Quote.Wert));
        Assert.Equal(4, wAe.Verlauf[^1].Anzahl);        // Frage, Karte, Szenario der Sicht und die Rechner-Aufgabe
        Assert.Equal(Erwartet(frageAe, karteAe), Fach(wAe));
        Assert.Contains("100", wAe.Banner.TextPc, StringComparison.Ordinal);

        // SI wie Python: alle Zeilen
        var wSi = Lies(si);
        Assert.Equal((3L, 1.0), (wSi.Quote.Antworten, wSi.Quote.Richtig));
        Assert.Equal(8, wSi.Verlauf[^1].Anzahl);
        Assert.Equal(Erwartet(frageAe, frageSi, frageSi, karteAe, karteSi), Fach(wSi));
    }

    // -- 181 ------------------------------------------------------------------------------------

    private const string Alt231 = "Ein Importprogramm soll je nach Dateiendung (.csv, .json, .xml) das passende Leseobjekt liefern. "
        + "Der restliche Code soll nur das Interface ILeser kennen. Welches Muster passt?";

    [Fact]
    public void Quizfrage231_AlteNichtMehrGezogen_NeueAngehaengt_NummernBleiben()
    {
        var datei = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Lernen, "quizfragen.json")))!.AsArray();
        // die Datei bleibt nur angehaengt: die alte Fassung steht weiter an Stelle 231, die neue am Ende (6360)
        Assert.Equal(Alt231, datei[231]!["q"]!.GetValue<string>());
        Assert.Equal(6361, datei.Count);
        var neu = datei[6360]!.AsObject();
        Assert.Equal(("ae", "ae_oop", "Fabrikmethode"), (neu["fachrichtung"]!.GetValue<string>(), neu["thema"]!.GetValue<string>(), neu["a"]!.GetValue<string>()));
        Assert.Contains("überschriebenen Methode erzeugeLeser()", neu["q"]!.GetValue<string>(), StringComparison.Ordinal);

        var alle = Echt();
        // fester Ausschluss: genau dieser eine Eintrag, Text gleich der Datei
        Assert.Equal([("quizfragen", 231, Alt231)], alle.Ausschluss);
        Assert.All(alle.Ausschluss, a => Assert.Equal(a.Frage, JsonNode.Parse(File.ReadAllBytes(Path.Combine(Lernen, a.Datei + ".json")))![a.Stelle]!["q"]!.GetValue<string>()));
        Assert.DoesNotContain(alle.Quizfragen, q => q.Frage == Alt231);
        Assert.Single(alle.Quizfragen, q => q.Frage == neu["q"]!.GetValue<string>());
        foreach (var fr in new[] { "si", "ae", "dpa", "dv" })
        {
            Assert.DoesNotContain(alle.Fuer(fr).Quizfragen, q => q.Frage == Alt231);
            Assert.Equal(fr == "ae", alle.Fuer(fr).Quizfragen.Any(q => q.Frage == neu["q"]!.GetValue<string>()));
        }
        // die anderen Eintraege behalten ihre Nummer (Stelle in der Datei)
        var basis = Lerninhalte.Laden(Pfade.Inhalte).Quizfragen.Count;
        Assert.All(alle.Quizfragen.Where(q => q.Nummer > basis), q =>
            Assert.Equal(datei[q.Nummer - basis - 1]!["q"]!.GetValue<string>(), q.Frage));
        Assert.Equal(basis + 6361, alle.Quizfragen.Single(q => q.Frage == neu["q"]!.GetValue<string>()).Nummer);

        // Gegenprobe: ohne ausschluss.json waere die alte Fassung wieder dabei
        var ohne = Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte),
            name => name != Lerninhalte.AusschlussDatei && Path.Combine(Lernen, name + ".json") is var pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null);
        Assert.Single(ohne.Quizfragen, q => q.Frage == Alt231);
        Assert.Equal(alle.Quizfragen.Count + 1, ohne.Quizfragen.Count);
        // steht an der Stelle ein anderer Text, wirkt der Ausschluss nicht
        var anders = new JsonObject { ["q"] = Alt231 + " " };
        Assert.False(Lerninhalte.Ausgeschlossen(alle.Ausschluss, "quizfragen", 231, anders));
        Assert.False(Lerninhalte.Ausgeschlossen(alle.Ausschluss, "quizfragen", 230, datei[231]!.AsObject()));
        Assert.True(Lerninhalte.Ausgeschlossen(alle.Ausschluss, "quizfragen", 231, datei[231]!.AsObject()));
    }
}
