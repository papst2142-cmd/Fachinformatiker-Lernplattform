using System.Diagnostics;
using System.Text;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 5 BE8 Haenger-Diagnose wie fisi_haenger (ohne Python-Sollwert, Zeiten haengen an der Uhr):
/// feste Uhr ueber die Parameter von Herzschlag und Pruefen, nachgestellter Herzschlag.
/// Kontrollfehler: Waechter schreibt bei laufendem Herzschlag.
/// </summary>
[Collection("Haenger")]
public sealed class HaengerTests : IDisposable
{
    private readonly TempOrdner _ordner = new();
    private readonly List<string> _fehlerlog = [];

    public HaengerTests()
    {
        Haenger.FuerTests(() => "1 lp T S wartet", text => _fehlerlog.Add(text));
        Assert.True(Haenger.Einrichten("A 0.10.0", _ordner.Pfad, plattform: "linux", zusatz: "x11", waechter: false));
    }

    public void Dispose()
    {
        Haenger.FuerTests();
        _ordner.Dispose();
    }

    private string Log() => File.ReadAllText(Haenger.Pfad(_ordner.Pfad), Encoding.UTF8);

    [Fact]
    public void StartzeileUndOhneHerzschlagNichts()
    {
        Assert.Matches(@"^=== \d{4}-\d\d-\d\d \d\d:\d\d:\d\d \| Start \| Version A 0\.10\.0 \| linux \| pid \d+ \| x11 ===\n$", Log());
        Assert.Null(Haenger.Pruefen(1000));
    }

    /// <summary>Kontrollfehler: bei laufendem Herzschlag schreibt der Waechter nie, auch nicht ueber Stunden.</summary>
    [Fact]
    public void LaufenderHerzschlagSchreibtNichts()
    {
        var vorher = Log();
        for (var t = 0.0; t < 3600; t += 0.5)
        {
            Assert.Equal(0.0, Haenger.Herzschlag(t), 9);
            if (t % 1 == 0)
            {
                Assert.Null(Haenger.Pruefen(t + 0.4));
            }
        }
        Assert.Equal(vorher, Log());
        Assert.Empty(_fehlerlog);
    }

    [Fact]
    public void BlockadeAbFuenfSekundenUndWiederFrei()
    {
        Haenger.Seite("dashboard");
        Haenger.Herzschlag(100);
        Assert.Null(Haenger.Pruefen(101));
        Assert.Null(Haenger.Pruefen(104.9));
        Assert.Equal("blockiert", Haenger.Pruefen(105));
        Assert.Null(Haenger.Pruefen(106));
        Assert.Equal(7.0, Haenger.Herzschlag(107.5), 9);   // Verspaetung gegenueber dem Takt (7,5 s - 0,5 s) fuer die Leistungsmessung
        Assert.Equal("frei", Haenger.Pruefen(108));
        Assert.Equal(
        [
            "Hänger-Diagnose: Hauptfaden seit 5,0 s ohne Rückruf (Seite: dashboard, Vorgang: bereit)\nWartepunkte: 1 lp T S wartet",
            "Hänger-Diagnose: Hauptfaden wieder frei nach 8,0 s (Seite: dashboard, Vorgang: bereit)\nWartepunkte: 1 lp T S wartet",
        ], _fehlerlog);
        Assert.Contains("| Hänger-Diagnose: Hauptfaden seit 5,0 s ohne Rückruf (Seite: dashboard, Vorgang: bereit)\nWartepunkte: 1 lp T S wartet ===\n", Log(), StringComparison.Ordinal);
        Assert.StartsWith("haenger.log: ", Haenger.ZustandText(Haenger.Pfad(_ordner.Pfad)), StringComparison.Ordinal);
    }

    [Fact]
    public void BekannterVorgangErstAbDreissigSekunden()
    {
        var vorher = Haenger.Vorgang("seite:optionen", 0);
        Assert.Equal(Haenger.Bereit, vorher);
        Haenger.Herzschlag(0);
        Assert.Null(Haenger.Pruefen(5));
        Assert.Null(Haenger.Pruefen(29.9));
        Assert.Equal("blockiert", Haenger.Pruefen(30));
        Assert.Contains("Vorgang: seite:optionen", _fehlerlog.Single(), StringComparison.Ordinal);
    }

    /// <summary>recent: endete ein bekannter Vorgang kurz vor dem letzten Herzschlag, gilt er fuer die Blockade.</summary>
    [Fact]
    public void KuerzlichBeendeterVorgangZaehlt()
    {
        Haenger.Vorgang("darstellung", 9);
        Haenger.Vorgang(Haenger.Bereit, 10);
        Assert.Equal("darstellung", Haenger.Kuerzlich(10));
        Assert.Equal(Haenger.Bereit, Haenger.Kuerzlich(10.1));
        Haenger.Herzschlag(10);
        Assert.Null(Haenger.Pruefen(15));   // bekannt: erst ab 30 s
        Assert.Equal("blockiert", Haenger.Pruefen(40));
    }

    [Fact]
    public void HoechstensZehnZeilenJeSitzung()
    {
        var t = 0.0;
        for (var i = 0; i < 12; i++)
        {
            Haenger.Herzschlag(t);
            Assert.Equal("blockiert", Haenger.Pruefen(t + 6));
            t += 7;
            Haenger.Herzschlag(t);
            Assert.Equal("frei", Haenger.Pruefen(t + 0.5));
            t += 1;
        }
        // 10 Blockaden mit "wieder frei" (20 Zeilen), dann ein Hinweis; danach nur noch haenger.log
        Assert.Equal(21, _fehlerlog.Count);
        Assert.Equal("Hänger-Diagnose: Weitere Blockaden in dieser Sitzung nicht einzeln notiert.", _fehlerlog[^1]);
        Assert.Equal(12, Log().Split("ohne Rückruf").Length - 1);
    }

    /// <summary>D6: statt des Stapels nach 30 s und weiteren 90 s die Stelle, je Sitzung hoechstens 3 Mal.</summary>
    [Fact]
    public void StelleNachDreissigUndNeunzigSekunden()
    {
        Haenger.Seite("karteikarten");
        Haenger.Herzschlag(0);
        Assert.Equal("blockiert", Haenger.Pruefen(5));
        Assert.Null(Haenger.Pruefen(29));
        Assert.Equal("stelle", Haenger.Pruefen(30));
        Assert.Null(Haenger.Pruefen(119));
        Assert.Equal("stelle", Haenger.Pruefen(120));
        Assert.Null(Haenger.Pruefen(500));
        Assert.Contains("| Stelle 1 (Hauptfaden seit 30 s still, Seite: karteikarten, Vorgang: bereit)\nWartepunkte: 1 lp T S wartet ===", Log(), StringComparison.Ordinal);
        Assert.Contains("| Stelle 2 (Hauptfaden seit 120 s still,", Log(), StringComparison.Ordinal);
        Haenger.Herzschlag(501);
        Assert.Equal("frei", Haenger.Pruefen(501.5));
        // dritte Stelle in einer neuen Blockade, eine vierte gibt es nicht
        Haenger.Herzschlag(600);
        Assert.Equal("blockiert", Haenger.Pruefen(605));
        Assert.Equal("stelle", Haenger.Pruefen(630));
        Assert.Null(Haenger.Pruefen(720));
        Assert.Null(Haenger.Pruefen(2000));
        Assert.Equal(3, Log().Split("| Stelle ").Length - 1);
    }

    [Fact]
    public void BeimSchliessenSchweigtDerWaechter()
    {
        var schliesst = false;
        Haenger.Schliessend(() => schliesst);
        Haenger.Herzschlag(0);
        schliesst = true;
        Assert.Null(Haenger.Pruefen(50));
        Haenger.Schliessend(() => throw new InvalidOperationException("darf nie stoeren"));
        Assert.Equal("blockiert", Haenger.Pruefen(51));
    }

    [Fact]
    public void ZustandTextOhneHaenger()
    {
        Assert.Equal(HaengerTexte.Keine, Haenger.ZustandText(Haenger.Pfad(_ordner.Pfad)));
        Assert.Equal(HaengerTexte.Keine, Haenger.ZustandText(Path.Combine(_ordner.Pfad, "fehlt.log")));
    }

    [Fact]
    public void GrosseDateiWirdBeimStartGekuerzt()
    {
        Haenger.FuerTests(() => "");
        var pfad = Haenger.Pfad(_ordner.Pfad);
        var zeile = "=== 2026-10-20 10:00:00 | alte Zeile mit etwas Text ===\n";
        File.WriteAllText(pfad, string.Concat(Enumerable.Repeat(zeile, Haenger.LogMax / zeile.Length + 100)));
        Assert.True(Haenger.Einrichten("A", _ordner.Pfad, plattform: "linux", waechter: false));
        var text = Log();
        Assert.True(Encoding.UTF8.GetByteCount(text) <= Haenger.LogBehalten + 200);
        Assert.StartsWith("=== 2026-10-20 10:00:00 | alte Zeile", text, StringComparison.Ordinal);
        Assert.Contains("| Start | Version A | linux | pid ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FaedenTextUnterLinux()
    {
        var text = Haenger.FaedenText();
        if (!OperatingSystem.IsLinux())
        {
            Assert.Equal("", text);
            return;
        }
        Assert.Matches(@"^\d+ \S.* [A-Za-z] \S+(; \d+ .*)*$", text);
        Assert.Contains(Environment.ProcessId + " ", text, StringComparison.Ordinal);
    }

    /// <summary>kill -USR1 schreibt die Angaben, das Programm laeuft weiter (Linux und macOS).</summary>
    [Fact]
    public void SignalSchreibtAbruf()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }
        Haenger.Seite("optionen");
        using (var kill = Process.Start(new ProcessStartInfo("kill", "-USR1 " + Environment.ProcessId) { UseShellExecute = false })!)
        {
            kill.WaitForExit();
            Assert.Equal(0, kill.ExitCode);
        }
        var uhr = Stopwatch.StartNew();
        while (!Log().Contains("Abruf (Signal USR1)", StringComparison.Ordinal) && uhr.Elapsed < TimeSpan.FromSeconds(10))
        {
            Thread.Sleep(50);
        }
        Assert.Contains("| Abruf (Signal USR1): Seite: optionen, Vorgang: bereit\nWartepunkte: 1 lp T S wartet ===", Log(), StringComparison.Ordinal);
    }
}
