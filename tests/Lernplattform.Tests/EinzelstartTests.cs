using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Lernplattform.Core.Diagnose;

namespace Lernplattform.Tests;

/// <summary>
/// Kindprozess fuer die Mehrfachstart-Tests: das Testprogramm startet sich selbst ein zweites Mal
/// (Umgebungsvariable LERNPLATTFORM_EINZELSTART). Art "beanspruchen": Beanspruchen aufrufen, Ergebnis,
/// Sperre und fehler.log-Zeilen auf der Standardausgabe melden, beenden. Art "halten": die Sperre
/// holen, "HAELT" melden und laufen, bis der Test den Prozess hart beendet.
/// </summary>
internal static class EinzelstartProbe
{
    private const string Variable = "LERNPLATTFORM_EINZELSTART";
    private const string Argumente = "LERNPLATTFORM_EINZELSTART_ARGUMENTE";
    public const string ArtBeanspruchen = "beanspruchen";
    public const string ArtHalten = "halten";
    public const string Haelt = "HAELT";

#pragma warning disable CA2255 // Nur im Testprogramm: der Kindprozess faengt hier an, bevor xUnit startet
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Kind()
    {
        var art = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrEmpty(art))
        {
            return;
        }
        var args = (Environment.GetEnvironmentVariable(Argumente) ?? "").Split('\n');
        try
        {
            Einzelstart.FuerTests(text =>
            {
                Console.Out.WriteLine("LOG " + text.Replace('\n', ' '));
                Console.Out.Flush();
            });
            var ergebnis = Einzelstart.Beanspruchen(args[0], double.Parse(args[1], CultureInfo.InvariantCulture),
                double.Parse(args[2], CultureInfo.InvariantCulture), "A Probe");
            if (art == ArtHalten)
            {
                Console.Out.WriteLine(Haelt);
                Console.Out.Flush();
                Thread.Sleep(Timeout.Infinite);
            }
            Console.Out.WriteLine("ERGEBNIS " + (ergebnis ? "start" : "beenden") + " " + (Einzelstart.Gesperrt ? "gesperrt" : "ohne"));
            Console.Out.Flush();
        }
        finally
        {
            Environment.Exit(0);
        }
    }

    private static ProcessStartInfo Info(string art, string[] argumente)
    {
        var programm = Environment.ProcessPath!;
        var start = new ProcessStartInfo(programm)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (Path.GetFileNameWithoutExtension(programm).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(typeof(EinzelstartProbe).Assembly.Location);
        }
        start.Environment[Variable] = art;
        start.Environment[Argumente] = string.Join('\n', argumente);
        return start;
    }

    /// <summary>Zweiter Start: laeuft bis zum Ende, liefert alle gemeldeten Zeilen.</summary>
    public static Task<List<string>> Beanspruchen(string ordner, double antwort, double sperre) => Task.Run(() =>
    {
        using var kind = Process.Start(Info(ArtBeanspruchen, [ordner, Zahl(antwort), Zahl(sperre)]))!;
        _ = kind.StandardError.ReadToEndAsync();
        var ausgabe = kind.StandardOutput.ReadToEndAsync();
        if (!kind.WaitForExit(TimeSpan.FromSeconds(90)))
        {
            kind.Kill(true);
            throw new InvalidOperationException("Einzelstart-Probe: Kind haengt");
        }
        return ausgabe.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(z => z.TrimEnd('\r')).ToList();
    });

    /// <summary>Halter in eigenem Prozess: kehrt zurueck, sobald er die Sperre hat; der Aufrufer beendet ihn hart.</summary>
    public static Process Halten(string ordner)
    {
        var kind = Process.Start(Info(ArtHalten, [ordner, "3", "25"]))!;
        _ = kind.StandardError.ReadToEndAsync();
        var zeile = kind.StandardOutput.ReadLineAsync();
        if (!zeile.Wait(TimeSpan.FromSeconds(60)) || zeile.Result != Haelt)
        {
            kind.Kill(true);
            throw new InvalidOperationException("Einzelstart-Probe: Halter ohne Sperre (" + zeile.Result + ")");
        }
        return kind;
    }

    private static string Zahl(double wert) => wert.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Phase 5 BE8 Sperre gegen Mehrfachstart wie fisi_einzelstart, mit echtem zweitem Prozess.
/// Kontrollfehler: zweiter Start beendet sich ohne "da"; Sperre bleibt nach hartem Ende des Halters
/// belegt; ein zweiter Start verhindert den Start (Grundsatz: nie).
/// </summary>
[Collection("Einzelstart")]
public sealed class EinzelstartTests : IDisposable
{
    private readonly TempOrdner _ordner = new();
    private readonly List<string> _fehlerlog = [];

    public EinzelstartTests() => Einzelstart.FuerTests(text => _fehlerlog.Add(text));

    public void Dispose()
    {
        Einzelstart.FuerTests();
        _ordner.Dispose();
    }

    private static string Ergebnis(List<string> zeilen) => zeilen.Single(z => z.StartsWith("ERGEBNIS ", StringComparison.Ordinal));

    private string[] Wartende() => Directory.GetFiles(_ordner.Pfad, Einzelstart.Praefix + "*");

    [Fact]
    public void NurLinuxUndWindows()
    {
        Assert.True(Einzelstart.Unterstuetzt("linux"));
        Assert.True(Einzelstart.Unterstuetzt("win32"));
        Assert.False(Einzelstart.Unterstuetzt("darwin"));
        Assert.False(Einzelstart.Unterstuetzt("android"));
    }

    [Fact]
    public void ErsterStartHaeltSperreUndSchreibtInfo()
    {
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad, version: "A 0.10.0"));
        Assert.True(Einzelstart.Gesperrt);
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad));   // zweimal im selben Prozess: weiter starten
        var info = Einzelstart.InfoLesen(_ordner.Pfad);
        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), info["pid"]);
        Assert.Equal("A 0.10.0", info["version"]);
        Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d$", info["start"]);
        Assert.Equal(_ordner.Pfad, Einzelstart.Uebernehmen());
        Assert.Equal(_ordner.Pfad, Einzelstart.Ordner);
        Assert.Empty(_fehlerlog);
    }

    /// <summary>Laufendes Programm antwortet "da": der zweite Start holt es nach vorn und beendet sich still.</summary>
    [Fact]
    public async Task ZweiterStartBeendetSichNachDa()
    {
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad));
        var zeilen = await EinzelstartProbe.Beanspruchen(_ordner.Pfad, 3, 25);
        Assert.Equal("ERGEBNIS beenden ohne", Ergebnis(zeilen));
        Assert.DoesNotContain(zeilen, z => z.StartsWith("LOG ", StringComparison.Ordinal));
        Assert.Empty(Wartende());   // Signal und Antwort aufgeraeumt
    }

    /// <summary>Die Oberflaeche hat uebernommen und meldet "endet": der zweite Start wartet auf die Sperre und startet.</summary>
    [Fact]
    public async Task EndetDannSperreFrei()
    {
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad));
        Einzelstart.Uebernehmen();
        var zweiter = EinzelstartProbe.Beanspruchen(_ordner.Pfad, 10, 25);
        var uhr = Stopwatch.StartNew();
        // sobald das Signal liegt: als schliessendes Programm beantworten, dann die Sperre freigeben
        while (Wartende().Length == 0 && uhr.Elapsed < TimeSpan.FromSeconds(60))
        {
            Thread.Sleep(20);
        }
        Assert.False(Einzelstart.SignaleBeantworten(_ordner.Pfad, true));
        Thread.Sleep(300);
        Einzelstart.FuerTests(text => _fehlerlog.Add(text));
        var zeilen = await zweiter;
        Assert.Equal("ERGEBNIS start gesperrt", Ergebnis(zeilen));
        Assert.DoesNotContain(zeilen, z => z.StartsWith("LOG ", StringComparison.Ordinal));
        Assert.Equal("A Probe", Einzelstart.InfoLesen(_ordner.Pfad)["version"]);
    }

    /// <summary>Beim Schliessen antwortet ein Hintergrund-Faden "endet" (auch wenn die Oberflaeche wartet); der zweite Start bekommt die Sperre danach.</summary>
    [Fact]
    public async Task SchliessenAntwortetEndet()
    {
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad));
        Einzelstart.Uebernehmen();
        Einzelstart.Schliessen();
        Assert.Null(Einzelstart.Ordner);
        var zweiter = EinzelstartProbe.Beanspruchen(_ordner.Pfad, 10, 25);
        await Task.Delay(4000, TestContext.Current.CancellationToken);
        // beantwortet: Signal und Antwort sind weg (ein unbeantwortetes Signal bliebe liegen), der zweite Start wartet auf die Sperre
        Assert.Empty(Wartende());
        Assert.False(zweiter.IsCompleted);
        Einzelstart.FuerTests(text => _fehlerlog.Add(text));   // Prozessende: Sperre frei
        var uhr = Stopwatch.StartNew();
        var zeilen = await zweiter;
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(4), "nach endet gleich auf die Sperre gewartet, nicht erst nach der Antwortzeit");
        Assert.Equal("ERGEBNIS start gesperrt", Ergebnis(zeilen));
        Assert.DoesNotContain(zeilen, z => z.StartsWith("LOG ", StringComparison.Ordinal));
    }

    /// <summary>Keine Antwort, Sperre bleibt belegt: Start ohne Sperre mit einem Eintrag in fehler.log, der den Halter nennt.</summary>
    [Fact]
    public async Task OhneAntwortStartOhneSperre()
    {
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad, version: "A 0.10.0"));
        Einzelstart.Uebernehmen();   // niemand beantwortet Signale
        var zeilen = await EinzelstartProbe.Beanspruchen(_ordner.Pfad, 0.5, 1);
        Assert.Equal("ERGEBNIS start ohne", Ergebnis(zeilen));
        var log = Assert.Single(zeilen, z => z.StartsWith("LOG ", StringComparison.Ordinal));
        Assert.StartsWith("LOG Mehrfachstart: Das laufende Programm hat nicht geantwortet (Antwort: keine) und die Sperre war nach 1 s noch belegt. Start ohne Sperre. Halter: pid "
            + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ", gestartet ", log, StringComparison.Ordinal);
        Assert.Contains(", Version A 0.10.0", log, StringComparison.Ordinal);
        Assert.Empty(Wartende());   // das liegen gelassene Signal ist am Ende entfernt
    }

    /// <summary>E2: kommt "da" erst nach der Antwortzeit, beendet sich der zweite Start trotzdem still.</summary>
    [Fact]
    public async Task SpaeteAntwortDa()
    {
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad));
        Einzelstart.Uebernehmen();
        var zweiter = EinzelstartProbe.Beanspruchen(_ordner.Pfad, 0.5, 30);
        var uhr = Stopwatch.StartNew();
        while (Wartende().Length == 0 && uhr.Elapsed < TimeSpan.FromSeconds(60))
        {
            Thread.Sleep(20);
        }
        Thread.Sleep(1500);   // Antwortzeit (0,5 s) sicher vorbei, das Signal liegt noch
        Assert.True(Einzelstart.SignaleBeantworten(_ordner.Pfad, false));
        var zeilen = await zweiter;
        Assert.Equal("ERGEBNIS beenden ohne", Ergebnis(zeilen));
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(25));
    }

    /// <summary>Kontrollfehler "Sperre nach Absturz belegt": wird der Halter hart beendet, ist die Sperre sofort frei.</summary>
    [Fact]
    public void HartBeendeterHalterGibtSperreFrei()
    {
        using (var halter = EinzelstartProbe.Halten(_ordner.Pfad))
        {
            Assert.NotEqual(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), Einzelstart.InfoLesen(_ordner.Pfad)["pid"]);
            halter.Kill(true);
            halter.WaitForExit();
        }
        var uhr = Stopwatch.StartNew();
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad, 0.5, 1));
        Assert.True(Einzelstart.Gesperrt);
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(1), "ohne Warten auf eine Antwort");
        Assert.Empty(_fehlerlog);
    }

    /// <summary>Haelt ein anderer Prozess die Sperre und antwortet "da", beendet sich dieser Start (der einzige Fall).</summary>
    [Fact]
    public void AndererHalterAntwortetDa()
    {
        using var halter = EinzelstartProbe.Halten(_ordner.Pfad);
        try
        {
            Assert.False(Einzelstart.Beanspruchen(_ordner.Pfad, 3, 25));
            Assert.False(Einzelstart.Gesperrt);
        }
        finally
        {
            halter.Kill(true);
            halter.WaitForExit();
        }
    }

    [Fact]
    public void AlteSignaleWerdenAufgeraeumt()
    {
        var alt = Path.Combine(_ordner.Pfad, Einzelstart.Praefix + "1-1" + Einzelstart.SignalEndung);
        var altAntwort = Path.Combine(_ordner.Pfad, Einzelstart.Praefix + "1-1" + Einzelstart.AntwortEndung);
        var frisch = Path.Combine(_ordner.Pfad, Einzelstart.Praefix + "2-2" + Einzelstart.AntwortEndung);
        foreach (var pfad in new[] { alt, altAntwort, frisch })
        {
            File.WriteAllText(pfad, "");
        }
        File.SetLastWriteTimeUtc(alt, DateTime.UtcNow.AddSeconds(-61));
        File.SetLastWriteTimeUtc(altAntwort, DateTime.UtcNow.AddMinutes(-10));
        Assert.True(Einzelstart.Beanspruchen(_ordner.Pfad));
        Einzelstart.Uebernehmen();
        Assert.False(File.Exists(alt));
        Assert.False(File.Exists(altAntwort));
        Assert.True(File.Exists(frisch));
    }

    [Fact]
    public void HalterText()
    {
        Assert.Equal("Halter: unbekannt (laeuft.info fehlt oder ist nicht lesbar).", Einzelstart.HalterText(_ordner.Pfad));
        var info = Path.Combine(_ordner.Pfad, Einzelstart.InfoDatei);
        File.WriteAllText(info, "pid=\n");
        Assert.Equal("Halter: unbekannt (laeuft.info fehlt oder ist nicht lesbar).", Einzelstart.HalterText(_ordner.Pfad));
        Assert.True(Einzelstart.InfoSchreiben(_ordner.Pfad, "A 0.10.0", new DateTime(2026, 10, 20, 10, 0, 0)));
        var pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var text = Einzelstart.HalterText(_ordner.Pfad);
        Assert.StartsWith("Halter: pid " + pid + ", gestartet 2026-10-20 10:00:00, Version A 0.10.0", text, StringComparison.Ordinal);
        Assert.EndsWith(".", text, StringComparison.Ordinal);
        if (!OperatingSystem.IsLinux())
        {
            Assert.Equal("Halter: pid " + pid + ", gestartet 2026-10-20 10:00:00, Version A 0.10.0.", text);
            return;
        }
        Assert.Matches(@"; Linux: Zustand [A-Za-z], wchan \S+\.$", text);
        // andere Startzeit: Prozessnummer neu vergeben
        File.WriteAllText(info, File.ReadAllText(info).Replace("startticks=", "startticks=9", StringComparison.Ordinal));
        Assert.EndsWith("; Linux: Prozessnummer neu vergeben (Angaben veraltet).", Einzelstart.HalterText(_ordner.Pfad), StringComparison.Ordinal);
        File.WriteAllText(info, "pid=999999999\nstart=x\n");
        Assert.Equal("Halter: pid 999999999, gestartet x, Version ?; Linux: Prozess läuft nicht mehr (Angaben veraltet).", Einzelstart.HalterText(_ordner.Pfad));
    }
}
