using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// FW8 (Sammelliste 101, Weisung P11-15): die Tests mit dem gemeinsamen Zustand der Haenger-Diagnose (Seite, Vorgang,
/// Wartepunkte, offene haenger.log, Signal USR1 im ganzen Prozess) laufen allein, nicht neben anderen Tests. Unter der
/// Last paralleler Tests stellt macOS das Signal sonst spaet zu oder der Faden des Abrufs kommt spaet dran (Lauf
/// 40172014126; Lauf 423 mit Sammelliste 191). Fristen und Wortlaut bleiben gleich.
/// </summary>
[CollectionDefinition("Haenger", DisableParallelization = true)]
public sealed class HaengerSammlung;

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

    // geteilt lesen wie Fehlerprotokoll.Anzeigen: die Diagnose haelt haenger.log offen (unter Windows sonst gesperrt)
    private string Log()
    {
        using var datei = new FileStream(Haenger.Pfad(_ordner.Pfad), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var leser = new StreamReader(datei, Encoding.UTF8);
        return leser.ReadToEnd();
    }

    /// <summary>
    /// A 0.29.2 (Weisung P10-4, Lauf 377 auf macOS einmal rot): eigener, leerer Datenordner, die Datei wird gleich nach
    /// dem Pruefen geschlossen und erst dann gelesen. Ein auf macOS spaet verarbeitetes Signal aus
    /// <see cref="SignalSchreibtAbruf"/> schreibt sonst eine Abruf-Zeile in die noch offene haenger.log dieses Tests (Vermutung wie bei
    /// <see cref="ZustandTextOhneHaenger"/>, A 0.12.2, nicht belegt). Aussage unveraendert: genau die Startzeile, ohne Herzschlag nichts.
    /// </summary>
    [Fact]
    public void StartzeileUndOhneHerzschlagNichts()
    {
        using var eigener = new TempOrdner();
        Haenger.FuerTests(() => "1 lp T S wartet", text => _fehlerlog.Add(text));
        Assert.True(Haenger.Einrichten("A 0.10.0", eigener.Pfad, plattform: "linux", zusatz: "x11", waechter: false));
        Assert.Null(Haenger.Pruefen(1000));
        Haenger.FuerTests(() => "1 lp T S wartet", text => _fehlerlog.Add(text));
        var log = File.ReadAllText(Haenger.Pfad(eigener.Pfad));
        // bei Rot den ganzen Log zeigen (Assert.Matches kuerzt den Wert)
        Assert.True(Regex.IsMatch(log, @"^=== \d{4}-\d\d-\d\d \d\d:\d\d:\d\d \| Start \| Version A 0\.10\.0 \| linux \| pid \d+ \| x11 ===\n$"),
            "voller Log:\n" + log);
        Assert.Empty(_fehlerlog);
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

    /// <summary>
    /// Patch A 0.37.0 (Sammelliste 204, Weisung P11-34 Antwort 3a): am Mac drosselt macOS die Zeitgeber eines verdeckten
    /// Programms (App Nap). Gegenprobe zu <see cref="BlockadeAbFuenfSekundenUndWiederFrei"/>: dieselbe Blockade, nur
    /// verdeckt, steht allein in haenger.log (mit Zusatz), nie in fehler.log.
    /// </summary>
    [Fact]
    public void VerdecktNurInHaengerLog()
    {
        Haenger.Sichtbarkeit(() => false);
        Haenger.Seite("settings");
        Haenger.Herzschlag(100);
        Assert.Null(Haenger.Pruefen(104.9));
        Assert.Equal("verdeckt", Haenger.Pruefen(105));
        Assert.Equal("stelle", Haenger.Pruefen(240));   // Stelle wie bisher nur in haenger.log
        Haenger.Herzschlag(241);
        Assert.Equal("frei", Haenger.Pruefen(241.5));
        Assert.Empty(_fehlerlog);
        var log = Log();
        Assert.Contains("| Hänger-Diagnose: Hauptfaden seit 5,0 s ohne Rückruf (Seite: settings, Vorgang: bereit)" + Haenger.VerdecktZusatz
            + "\nWartepunkte: 1 lp T S wartet ===\n", log, StringComparison.Ordinal);
        Assert.Contains("| Hänger-Diagnose: Hauptfaden wieder frei nach 141,5 s (Seite: settings, Vorgang: bereit)" + Haenger.VerdecktZusatz,
            log, StringComparison.Ordinal);
        // verdeckte Blockaden zaehlen nicht zu den zehn Zeilen je Sitzung: die naechste sichtbare kommt in fehler.log
        Haenger.Sichtbarkeit(() => true);
        Haenger.Herzschlag(300);
        Assert.Equal("blockiert", Haenger.Pruefen(305));
        Assert.Single(_fehlerlog);
    }

    /// <summary>Sichtbar (oder unbekannt) bleibt alles wie bisher: Gegenprobe, dass die Sichtbarkeit nur bei "verdeckt" wirkt.</summary>
    [Fact]
    public void SichtbarWieBisher()
    {
        Haenger.Sichtbarkeit(() => true);
        Haenger.Seite("settings");
        Haenger.Herzschlag(100);
        Assert.Equal("blockiert", Haenger.Pruefen(105));
        Haenger.Herzschlag(107.5);
        Assert.Equal("frei", Haenger.Pruefen(108));
        Assert.Equal(2, _fehlerlog.Count);
        Assert.DoesNotContain(Haenger.VerdecktZusatz, Log(), StringComparison.Ordinal);
        Haenger.Sichtbarkeit(() => throw new InvalidOperationException("Abfrage kaputt"));
        Haenger.Herzschlag(200);
        Assert.Equal("blockiert", Haenger.Pruefen(205));
        Assert.Equal(3, _fehlerlog.Count);
    }

    /// <summary>
    /// Ein echter Haenger wird nie verschluckt: verdeckt begonnen, dann sichtbar und der Herzschlag steht weiter so lange
    /// wie die Grenze (5 s seit dem Sichtbarwerden) -> Meldung in fehler.log; vorher nicht (macOS braucht einen Augenblick,
    /// bis der gedrosselte Zeitgeber wieder laeuft).
    /// </summary>
    [Fact]
    public void VerdecktDannSichtbarUndWeiterBlockiertMeldet()
    {
        var sichtbar = false;
        Haenger.Sichtbarkeit(() => sichtbar);
        Haenger.Seite("settings");
        Haenger.Herzschlag(100);
        Assert.Equal("verdeckt", Haenger.Pruefen(105));
        Assert.Equal("stelle", Haenger.Pruefen(150));
        sichtbar = true;
        Assert.Null(Haenger.Pruefen(151));
        Assert.Null(Haenger.Pruefen(155.9));
        Assert.Empty(_fehlerlog);
        Assert.Equal("blockiert", Haenger.Pruefen(156));
        Assert.Null(Haenger.Pruefen(157));
        Haenger.Herzschlag(160);
        Assert.Equal("frei", Haenger.Pruefen(160.5));
        Assert.Equal(
        [
            "Hänger-Diagnose: Hauptfaden seit 56,0 s ohne Rückruf (Seite: settings, Vorgang: bereit)\nWartepunkte: 1 lp T S wartet",
            "Hänger-Diagnose: Hauptfaden wieder frei nach 60,5 s (Seite: settings, Vorgang: bereit)\nWartepunkte: 1 lp T S wartet",
        ], _fehlerlog);
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

    /// <summary>
    /// A 0.12.2 (Weisung P6-15): eigener, leerer Datenordner. Die Diagnose schreibt nur bei der Einrichtung
    /// (Startzeile) dorthin und wird gleich wieder geschlossen; ein spaet verarbeitetes Signal aus
    /// <see cref="SignalSchreibtAbruf"/> (macOS) kann diese Datei damit nicht mehr erreichen.
    /// </summary>
    [Fact]
    public void ZustandTextOhneHaenger()
    {
        using var eigener = new TempOrdner();
        Haenger.FuerTests(() => "1 lp T S wartet", text => _fehlerlog.Add(text));
        Assert.True(Haenger.Einrichten("A 0.10.0", eigener.Pfad, plattform: "linux", zusatz: "x11", waechter: false));
        Haenger.FuerTests(() => "1 lp T S wartet", text => _fehlerlog.Add(text));
        var pfad = Haenger.Pfad(eigener.Pfad);
        // A 0.29.2: bei Rot den ganzen Log zeigen (Assert.Matches kuerzt den Wert; Lauf 383 auf macOS einmal rot)
        var log = File.ReadAllText(pfad);
        Assert.True(Regex.IsMatch(log, @"^=== \d{4}-\d\d-\d\d \d\d:\d\d:\d\d \| Start \| Version A 0\.10\.0 \| linux \| pid \d+ \| x11 ===\n$"),
            "voller Log:\n" + log);
        Assert.Equal(HaengerTexte.Keine, Haenger.ZustandText(pfad));
        Assert.Equal(HaengerTexte.Keine, Haenger.ZustandText(Path.Combine(eigener.Pfad, "fehlt.log")));
    }

    /// <summary>Gegenpruefung W2 Fund 4: nach einem Abruf (kill -USR1) nennen die Optionen die Datei, nicht "Keine Hänger".</summary>
    [Fact]
    public void ZustandTextNachAbruf()
    {
        Haenger.Abruf();
        Assert.Contains("| Abruf (Signal USR1): Seite: -, Vorgang: bereit", Log(), StringComparison.Ordinal);
        Assert.StartsWith("haenger.log: ", Haenger.ZustandText(Haenger.Pfad(_ordner.Pfad)), StringComparison.Ordinal);
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

    /// <summary>
    /// FW6 (Sammelliste 191, Weisung P11-12): Lauf 423 (macOS) war <see cref="LaufenderHerzschlagSchreibtNichts"/> rot, weil im Log
    /// dieses Tests eine weitere Zeile "=== 2026-11-02 14:34:34 | …" stand, in derselben Sekunde wie seine Startzeile. Ausser dem Waechter
    /// schreibt nur der Abruf (Signal USR1) in eine fremde, gerade offene haenger.log; ein Signal, das macOS erst nach
    /// <see cref="SignalSchreibtAbruf"/> zustellt, landet so im Log des naechsten Tests (wie Sammelliste 101 und 107). Deshalb nimmt der
    /// Signal-Abruf in Tests nur noch <see cref="SignalSchreibtAbruf"/> an (Haenger.SignalAbrufFuerTests); im Programm gilt er immer.
    /// Gegenprobe: ein Signal ohne Freigabe schreibt nichts in den Log eines anderen Tests (ohne den Schalter rot).
    /// </summary>
    [Fact]
    public void SpaetesSignalSchreibtNichtInFremdenLog()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }
        var vorher = Log();
        using (var kill = Process.Start(new ProcessStartInfo("kill", "-USR1 " + Environment.ProcessId) { UseShellExecute = false })!)
        {
            kill.WaitForExit();
            Assert.Equal(0, kill.ExitCode);
        }
        // so lange wie SignalSchreibtAbruf hoechstens auf seine Zeile wartet, hier aber auf keine
        var uhr = Stopwatch.StartNew();
        while (Log() == vorher && uhr.Elapsed < TimeSpan.FromSeconds(2))
        {
            Thread.Sleep(50);
        }
        var log = Log();
        Assert.True(log == vorher, "voller Log:\n" + log);
    }

    /// <summary>
    /// FW8 (Sammelliste 101), Gegenprobe mit festem Ablauf: ein Signal hat die Freigabe schon gesehen, da schliesst der
    /// naechste Test die Datei (FuerTests) und oeffnet seine eigene haenger.log. Das Signal schreibt nie in diese neue
    /// Datei: FuerTests wartet, bis der Abruf fertig ist. Ohne die gemeinsame Sperre stuende die Abruf-Zeile im Log des
    /// naechsten Tests (rot). Ohne echtes Signal, darum auf jedem System.
    /// </summary>
    [Fact]
    public async Task AbrufNachFreigabeSchreibtNichtInDieNaechsteDatei()
    {
        using var naechster = new TempOrdner();
        Haenger.Seite("optionen");
        Haenger.SignalAbrufFuerTests(true);
        using var gesehen = new ManualResetEventSlim();
        using var weiter = new ManualResetEventSlim();
        Haenger.NachFreigabeFuerTests = () =>
        {
            gesehen.Set();
            weiter.Wait(TimeSpan.FromSeconds(1));   // mit der Sperre kommt der naechste Test hier nicht weiter
        };
        var signal = Task.Run(Haenger.SignalEmpfangen, TestContext.Current.CancellationToken);
        Assert.True(gesehen.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        var umstieg = Task.Run(() =>
        {
            Haenger.FuerTests(() => "2 lp T S wartet", _fehlerlog.Add);
            Assert.True(Haenger.Einrichten("A 0.10.0", naechster.Pfad, plattform: "linux", zusatz: "x11", waechter: false));
            weiter.Set();
        }, TestContext.Current.CancellationToken);
        await umstieg.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await signal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Haenger.FuerTests();   // Datei des "naechsten Tests" schliessen, bevor sein Ordner geloescht wird (Windows)
        string neu;
        using (var datei = new FileStream(Haenger.Pfad(naechster.Pfad), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var leser = new StreamReader(datei, Encoding.UTF8))
        {
            neu = leser.ReadToEnd();
        }
        Assert.DoesNotContain("Abruf (Signal", neu, StringComparison.Ordinal);
        Assert.Contains("| Abruf (Signal USR1): Seite: optionen, Vorgang: bereit\nWartepunkte: 1 lp T S wartet ===", Log(), StringComparison.Ordinal);
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
        Haenger.SignalAbrufFuerTests(true);
        using (var kill = Process.Start(new ProcessStartInfo("kill", "-USR1 " + Environment.ProcessId) { UseShellExecute = false })!)
        {
            kill.WaitForExit();
            Assert.Equal(0, kill.ExitCode);
        }
        // Sammelliste 107 (macOS einmal rot "nach 0 ms" mit einer Zeile "Seite: -"): auf genau die erwartete
        // Zeile warten statt auf die erste Abruf-Zeile; Wortlaut und Frist (10 s) bleiben wie vorher
        const string Erwartet = "| Abruf (Signal USR1): Seite: optionen, Vorgang: bereit\nWartepunkte: 1 lp T S wartet ===";
        var uhr = Stopwatch.StartNew();
        while (!Log().Contains(Erwartet, StringComparison.Ordinal) && uhr.Elapsed < TimeSpan.FromSeconds(10))
        {
            Thread.Sleep(50);
        }
        // Sammelliste 101 (macOS einmal rot): bei Rot den ganzen Log und die Wartezeit zeigen, nicht nur den Anfang
        var log = Log();
        Assert.True(log.Contains(Erwartet, StringComparison.Ordinal),
            "nach " + uhr.ElapsedMilliseconds + " ms, Seite/Vorgang jetzt " + Haenger.Aktuell() + ", voller Log:\n" + log);
    }
}
