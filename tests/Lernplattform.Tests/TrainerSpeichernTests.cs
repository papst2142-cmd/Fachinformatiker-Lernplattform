using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Fortschritt;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3, R5: geprueften Aufgaben des Rechen-Trainers speichern wie Python log_trainer und
/// zaehlen wie trainer_stats (Vergleichswerte G27 "speichern"). Pflichttests wie Phase 2:
/// nach einem Lesefehler nichts schreiben, schreibgeschuetzte Datei, Absturz mitten im
/// Schreiben; Dashboard und Fortschritt zaehlen die neue Zeile mit.
/// </summary>
public class TrainerSpeichernTests
{
    private static readonly JsonElement G27 = VergleichsHilfe.Lade("rechentrainer.json").GetProperty("speichern");

    private static string Statistik(IReadOnlyDictionary<string, (long Geloest, double Richtig)> s) =>
        string.Join("‖", s.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}:{p.Value.Geloest}:{p.Value.Richtig.ToString("R", CultureInfo.InvariantCulture)}"));

    private static string Statistik(JsonElement e) =>
        string.Join("‖", e.EnumerateArray().Select(z => $"{z[0].GetString()}:{z[1].GetInt64()}:{z[2].GetDouble().ToString("R", CultureInfo.InvariantCulture)}"));

    private static Dictionary<string, (long, double)> AlsStatistik(JsonElement e) =>
        e.EnumerateArray().ToDictionary(z => z[0].GetString()!, z => (z[1].GetInt64(), z[2].GetDouble()));

    [Fact]
    public void ZeilenUndStatistikWiePython()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var jetzt = DateTimeOffset.MinValue;
        var nummer = 0;
        var schreiber = new LernstandSchreiber(new Lernstand(pfad), SpeichernTests.FesteUhr(() => jetzt),
            () => "rechentrainer-uid-" + (++nummer).ToString("0000", CultureInfo.InvariantCulture));
        Assert.Equal(Statistik(G27.GetProperty("vorher")), Statistik(schreiber.TrainerStatistik()));
        foreach (var aufruf in G27.GetProperty("aufrufe").EnumerateArray())
        {
            jetzt = DateTimeOffset.Parse(aufruf.GetProperty("utc").GetString()!, CultureInfo.InvariantCulture);
            Assert.Equal(aufruf.GetProperty("ergebnis").GetBoolean(), schreiber.TrainerSpeichern(aufruf.GetProperty("art").GetString()!,
                aufruf.GetProperty("stufe").GetInt32(), aufruf.GetProperty("richtig").GetBoolean()));
        }
        var spalten = G27.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()!).ToList();
        using (var db = VergleichsHilfe.Oeffnen(pfad))
        {
            Assert.Equal(G27.GetProperty("zeilen").EnumerateArray().Select(VergleichsHilfe.Zeile).ToList(),
                VergleichsHilfe.Abfrage(db, $"SELECT {string.Join(", ", spalten)} FROM trainer_aufgaben ORDER BY id").Select(VergleichsHilfe.Zeile).ToList());
            Assert.Equal(G27.GetProperty("typen").EnumerateArray().Select(VergleichsHilfe.Zeile).ToList(),
                VergleichsHilfe.Abfrage(db, $"SELECT {string.Join(", ", spalten.Select(s => $"typeof({s})"))} FROM trainer_aufgaben ORDER BY id")
                    .Select(VergleichsHilfe.Zeile).ToList());
        }
        var statistik = schreiber.TrainerStatistik();
        Assert.Equal(Statistik(G27.GetProperty("statistik")), Statistik(statistik));
        Assert.Equal(G27.GetProperty("zeile").GetString(), RechenTrainer.StatistikZeile(statistik));
    }

    [Fact]
    public void StatistikZeileWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G27.GetProperty("zeilen_fest").EnumerateArray())
        {
            a.Gleich(f[1].GetString(), RechenTrainer.StatistikZeile(AlsStatistik(f[0])), "show_stats " + Statistik(f[0]));
        }
        a.Pruefen(8);
    }

    /// <summary>Kommazahl in correct (fremde Daten): "%d" schneidet erst die Summe ueber alle Arten ab (Gegenpruefung P3-11 F1).</summary>
    [Fact]
    public void StatistikMitFremdenWerten()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var lernstand = new Lernstand(pfad);
        Assert.True(lernstand.Schreiben("INSERT INTO trainer_aufgaben (timestamp, art, stufe, correct) VALUES ('2026-10-20 10:00:00', 'ipv4', 1, 0.5)"));
        Assert.True(lernstand.Schreiben("INSERT INTO trainer_aufgaben (timestamp, art, stufe, correct) VALUES ('2026-10-20 10:00:00', 'vlsm', 1, 0.5)"));
        Assert.True(lernstand.Schreiben("INSERT INTO trainer_aufgaben (timestamp, art, stufe, correct) VALUES ('2026-10-20 10:00:00', 'zahlen', 1, 0)"));
        var statistik = new LernstandSchreiber(lernstand).TrainerStatistik();
        Assert.Equal("ipv4:1:0.5\u2016vlsm:1:0.5\u2016zahlen:1:0", Statistik(statistik));
        Assert.Equal("bisher 3 Aufgaben, 1 richtig", RechenTrainer.StatistikZeile(statistik));
    }

    [Fact]
    public void NachLesefehlerWirdNichtsGeschrieben()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var lernstand = new Lernstand(pfad);
        Assert.Equal("kaputt", lernstand.NurLesenGrund);
        var schreiber = new LernstandSchreiber(lernstand);
        Assert.False(schreiber.TrainerSpeichern("ipv4", 1, true));
        Assert.Equal(1, lernstand.AbgelehnteSchreibzugriffe);
        Assert.Empty(schreiber.TrainerStatistik());
        Assert.Equal("", RechenTrainer.StatistikZeile(schreiber.TrainerStatistik()));
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
        Assert.Equal([Fehlerprotokoll.DateiName, Lernstand.DateiName],
            Directory.EnumerateFileSystemEntries(ordner.Pfad).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Schreibgeschuetzte Datei: nichts gespeichert, Datei bytegleich (gelingt nur ohne Root, CI laeuft ohne).</summary>
    [Fact]
    public void SchreibgeschuetzteDateiSpeichertKeineAufgabe()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        Assert.True(new LernstandSchreiber(new Lernstand(pfad)).TrainerSpeichern("zahlen", 2, false));
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(pfad, FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(pfad, UnixFileMode.UserRead);
        }
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var lernstand = new Lernstand(pfad);
        Assert.Equal("schreibschutz", lernstand.NurLesenGrund);
        var schreiber = new LernstandSchreiber(lernstand);
        Assert.False(schreiber.TrainerSpeichern("zahlen", 2, true));
        Assert.Equal("zahlen:1:0", Statistik(schreiber.TrainerStatistik()));
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
    }

    /// <summary>Absturz mitten im Schreiben (Kindprozess, hart beendet): Datenbank in Ordnung, jede gemeldete Zeile ganz da.</summary>
    [Fact]
    public void AbsturzMittenImSchreiben()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        for (var runde = 0; runde < 3; runde++)
        {
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtTrainer, [pfad], 40 + runde * 37);
            using var db = VergleichsHilfe.Oeffnen(pfad);
            Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
            var zeilen = VergleichsHilfe.Abfrage(db, "SELECT art, uid, stufe, correct, timestamp, utc_offset FROM trainer_aufgaben WHERE uid LIKE 'probe-%'");
            foreach (var z in zeilen)
            {
                var n = ((string)z[0]!)["art ".Length..].TrimEnd('.');
                Assert.Equal("art " + n + new string('.', 3000), z[0]);
                Assert.Equal("probe-" + n, z[1]);
                Assert.Equal(3L, z[2]);
                Assert.Equal(1L, z[3]);
                Assert.NotNull(z[4]);
                Assert.NotNull(z[5]);
            }
            var vorhanden = zeilen.Select(z => (string)z[1]!).ToHashSet();
            Assert.All(gemeldet, uid => Assert.Contains(uid, vorhanden));
            Assert.True(gemeldet.Count >= 40 + runde * 37);
        }
    }

    /// <summary>
    /// Dashboard (Kachel "Heute", Lernserie, Zustand) und Fortschritt (Aufgaben je Tag) zaehlen die
    /// neue Zeile mit; der Kalender "Lerntage" nicht (Python month_activity ohne trainer_aufgaben).
    /// </summary>
    [Fact]
    public void DashboardUndFortschrittZaehlenMit()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var heute = new DateOnly(2026, 10, 21);
        var jetzt = DateTimeOffset.Parse("2026-10-21T08:00:00+00:00", CultureInfo.InvariantCulture);
        var schreiber = new LernstandSchreiber(new Lernstand(pfad), SpeichernTests.FesteUhr(() => jetzt));
        DashboardWerte Dashboard() => DashboardWerte.Lesen(pfad, heute, Inhalte, Einstellungen.Ziel(new JsonObject()), null,
            Path.Combine(ordner.Pfad, "fehler.log"));
        Assert.Equal(DashboardZustand.Leer, Dashboard().Zustand);
        Assert.Equal(0, Auswertung.Lesen(pfad).Reihe(7, heute, null).Werte[^1]);
        Assert.True(schreiber.TrainerSpeichern("ipv4", 1, true));
        Assert.True(schreiber.TrainerSpeichern("vlsm", 2, false));
        var werte = Dashboard();
        Assert.Equal(DashboardZustand.Daten, werte.Zustand);
        Assert.Equal(2, werte.Tagesziel.Erledigt);
        Assert.Equal(1, werte.Lernserie);
        Assert.DoesNotContain(21, DashboardWerte.Monat(pfad, 2026, 10).Lerntage);
        Assert.Equal(2, Auswertung.Lesen(pfad).Reihe(7, heute, null).Werte[^1]);
    }

    private static readonly DashboardInhalte Inhalte = new(Lerninhalte.Laden(Pfade.Inhalte));
}
