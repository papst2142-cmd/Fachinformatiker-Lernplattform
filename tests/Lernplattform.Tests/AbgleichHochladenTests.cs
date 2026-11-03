using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Daten;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// FW8 (Sammelliste 193, Weisung P11-15): allein, nicht neben anderen Tests. Die beiden Tests messen, wie lange
/// ein Lernschreiben waehrend des Hochladens wartet (Grenze 2 s, unveraendert); neben vielen parallelen Tests
/// misst das auf einem langsamen Rechner auch die Last mit (Lauf 430, Windows: 3,89 s).
/// </summary>
[CollectionDefinition("Abgleich allein", DisableParallelization = true)]
public sealed class AbgleichAlleinSammlung;

/// <summary>
/// P2-12 A1 (aus <see cref="AbgleichTests"/>): keine Schreibsperre waehrend des Netzes. FW8 (Sammelliste 193):
/// die Tests laufen allein, und ein rotes Ergebnis nennt die Ursache: ob die Datenbank in diesem Augenblick
/// gesperrt war (Probe mit BEGIN EXCLUSIVE ohne Wartezeit, wie ein Commit sie braucht) oder frei, wie lange
/// das Hochladen da schon lief und wie voll der Thread-Pool war. Aussage und Grenze bleiben gleich.
/// </summary>
[Collection("Abgleich allein")]
public sealed class AbgleichHochladenTests
{
    /// <summary>
    /// Kann eine neue Verbindung jetzt sofort schreiben (BEGIN EXCLUSIVE ohne Wartezeit, danach zurueck)?
    /// "frei" oder "gesperrt (SQLite-Fehler n)" (5 = belegt); die Probe schreibt nichts.
    /// </summary>
    internal static string Sperre(string pfad)
    {
        // roh ueber SQLite (ohne die Warteschleife von Microsoft.Data.Sqlite, die bei Zeitlimit 0 endlos wartet)
        using var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = pfad, Pooling = false }.ToString());
        verbindung.Open();
        SQLitePCL.raw.sqlite3_busy_timeout(verbindung.Handle, 0);
        var code = SQLitePCL.raw.sqlite3_exec(verbindung.Handle, "BEGIN EXCLUSIVE");
        if (code != SQLitePCL.raw.SQLITE_OK)
        {
            return "gesperrt (SQLite-Fehler " + code.ToString(CultureInfo.InvariantCulture) + ")";
        }
        SQLitePCL.raw.sqlite3_exec(verbindung.Handle, "ROLLBACK");
        return "frei";
    }

    private static string Last()
    {
        ThreadPool.GetAvailableThreads(out var frei, out _);
        ThreadPool.GetMaxThreads(out var hoechstens, out _);
        return "Thread-Pool: " + ThreadPool.PendingWorkItemCount.ToString(CultureInfo.InvariantCulture) + " wartende Aufgaben, "
            + (hoechstens - frei).ToString(CultureInfo.InvariantCulture) + " belegte Faeden, "
            + Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture) + " Prozessoren";
    }

    /// <summary>
    /// Waehrend das Hochladen mehrere Sekunden dauert, speichert ein zweiter Schreiber (die Lernseite)
    /// eine Antwort. Sie gelingt sofort und ist nach dem Abgleich in der Datenbank.
    /// </summary>
    [Fact]
    public async Task LernenWaehrendDesHochladens()
    {
        using var attrappe = AbgleichTests.Attrappe();
        using var pc = new AbgleichTests.Geraet("PC Test", AbgleichTests.Zeit);
        pc.Schreiber.KarteSpeichern("Netzwerk", "Vorher", "karte", true);
        attrappe.PutVerzoegerung = TimeSpan.FromSeconds(4);
        var abbruch = TestContext.Current.CancellationToken;

        var abgleich = Task.Run(() => pc.Abgleichen(attrappe, warten: TimeSpan.FromSeconds(30)), abbruch);
        Assert.True(attrappe.PutBegonnen.Wait(TimeSpan.FromSeconds(20), abbruch));
        var seitPut = Stopwatch.StartNew();
        var lernen = new LernstandSchreiber(new Lernstand(pc.Lernstand.Pfad), pc.Uhr, null, pc.Abweichung);
        var sperreVorher = Sperre(pc.Lernstand.Pfad);
        var bisSchreiben = seitPut.ElapsedMilliseconds;
        var uhr = Stopwatch.StartNew();
        Assert.True(lernen.KarteSpeichern("Netzwerk", "Waehrend", "karte", true));
        uhr.Stop();
        var sperreDanach = Sperre(pc.Lernstand.Pfad);
        Assert.False(abgleich.IsCompleted);                    // das Hochladen laeuft noch
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(2), "Schreiben wartete " + uhr.Elapsed
            + "; Datenbank vor dem Schreiben " + sperreVorher + ", danach " + sperreDanach
            + "; Schreiben begann " + bisSchreiben.ToString(CultureInfo.InvariantCulture) + " ms nach dem Beginn des Hochladens; " + Last());
        await abgleich;
        Assert.Equal(["Vorher", "Waehrend"], AbgleichTests.Fragen(pc));
    }

    /// <summary>
    /// Kontrollfehler A1: haelt jemand die Schreibsperre ueber das Hochladen, scheitert das Lernschreiben (der Test oben
    /// wird rot). FW8: die Probe meldet genau diesen Fall als "gesperrt".
    /// </summary>
    [Fact]
    public async Task KontrollfehlerSperreUeberDasHochladen()
    {
        using var attrappe = AbgleichTests.Attrappe();
        using var pc = new AbgleichTests.Geraet("PC Test", AbgleichTests.Zeit);
        pc.Schreiber.KarteSpeichern("Netzwerk", "Vorher", "karte", true);
        attrappe.PutVerzoegerung = TimeSpan.FromSeconds(8);
        var abbruch = TestContext.Current.CancellationToken;

        var abgleich = Task.Run(() => pc.Abgleichen(attrappe, warten: TimeSpan.FromSeconds(30)), abbruch);
        Assert.True(attrappe.PutBegonnen.Wait(TimeSpan.FromSeconds(20), abbruch));
        Assert.Equal("frei", Sperre(pc.Lernstand.Pfad));
        // Fehler nachgestellt: Schreibsperre (BEGIN IMMEDIATE) waehrend des Hochladens
        using (var gesperrt = pc.Lernstand.Verbindung())
        using (var tx = gesperrt.BeginTransaction(deferred: false))
        {
            Assert.StartsWith("gesperrt (SQLite-Fehler 5)", Sperre(pc.Lernstand.Pfad), StringComparison.Ordinal);
            var lernen = new LernstandSchreiber(new Lernstand(pc.Lernstand.Pfad), pc.Uhr, null, pc.Abweichung);
            Assert.False(lernen.KarteSpeichern("Netzwerk", "Waehrend", "karte", true));
            tx.Rollback();
        }
        Assert.Equal("frei", Sperre(pc.Lernstand.Pfad));
        await abgleich;
        Assert.Equal(["Vorher"], AbgleichTests.Fragen(pc));
    }
}
