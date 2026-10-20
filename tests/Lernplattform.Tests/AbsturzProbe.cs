using System.Diagnostics;
using System.Runtime.CompilerServices;
using Lernplattform.Core.Daten;

namespace Lernplattform.Tests;

/// <summary>
/// Absturzprobe fuer L2 und L3: Der Test startet das Testprogramm ein zweites Mal als
/// Kindprozess (Umgebungsvariable LERNPLATTFORM_ABSTURZPROBE). Das Kind schreibt in die
/// Datenbank und meldet jeden Schritt auf der Standardausgabe; der Elternprozess beendet
/// es hart (wie Stromausfall oder Taskmanager) und prueft danach die Datei.
/// </summary>
internal static class AbsturzProbe
{
    public const string ArtSpeichern = "speichern";
    public const string ArtUebernahme = "uebernahme";
    public const string ArtVorschau = "vorschau";
    public const string Fertig = "FERTIG";
    private const string Variable = "LERNPLATTFORM_ABSTURZPROBE";
    private const string Argumente = "LERNPLATTFORM_ABSTURZPROBE_ARGUMENTE";
    public const string Halt = "HALT";

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
            switch (art)
            {
                case ArtSpeichern:
                    var n = "";
                    var schreiber = new LernstandSchreiber(new Lernstand(args[0]), null, () => "probe-" + n);
                    while (true)
                    {
                        n = Guid.NewGuid().ToString("N");
                        // Lange Texte, damit ein Schreibvorgang mehrere Seiten der Datei beruehrt
                        if (schreiber.KarteSpeichern("Fachbereich " + n, "Frage " + n + new string('.', 3000), "lernen", true))
                        {
                            Console.Out.WriteLine("gespeichert probe-" + n);
                            Console.Out.Flush();
                        }
                    }
                case ArtUebernahme:
                    Uebernahme.Haltepunkt = punkt =>
                    {
                        if (punkt == args[2])
                        {
                            Console.Out.WriteLine(Halt);
                            Console.Out.Flush();
                            Thread.Sleep(Timeout.Infinite);
                        }
                    };
                    var daten = Uebernahme.Lesen(UebernahmeQuelle.AbgleichDatei, args[1]);
                    Uebernahme.Uebernehmen(new Lernstand(args[0]), daten, Umbenennungen.Laden(Pfade.Inhalte));
                    Console.Out.WriteLine(Fertig);
                    Console.Out.Flush();
                    break;
                case ArtVorschau:
                    Uebernahme.Haltepunkt = punkt =>
                    {
                        if (punkt == args[2])
                        {
                            Console.Out.WriteLine(Halt);
                            Console.Out.Flush();
                            Thread.Sleep(Timeout.Infinite);
                        }
                    };
                    Uebernahme.Vorschau(new Lernstand(args[0]), Uebernahme.Lesen(UebernahmeQuelle.AbgleichDatei, args[1]),
                        Umbenennungen.Laden(Pfade.Inhalte));
                    Console.Out.WriteLine(Fertig);
                    Console.Out.Flush();
                    break;
            }
        }
        finally
        {
            Environment.Exit(5);
        }
    }

    /// <summary>
    /// Kind starten und hart beenden: bei ArtSpeichern nach mindestens Meldungen, bei ArtUebernahme
    /// sobald es HALT meldet. Rueckgabe: die gemeldeten Kennungen (ArtSpeichern).
    /// </summary>
    public static List<string> Starten(string art, string[] argumente, int mindestens = 0, IReadOnlyDictionary<string, string>? umgebung = null)
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
            start.ArgumentList.Add(typeof(AbsturzProbe).Assembly.Location);
        }
        start.Environment[Variable] = art;
        start.Environment[Argumente] = string.Join('\n', argumente);
        foreach (var (name, wert) in umgebung ?? new Dictionary<string, string>())
        {
            start.Environment[name] = wert;
        }
        using var kind = Process.Start(start)!;
        _ = kind.StandardError.ReadToEndAsync();
        var gemeldet = new List<string>();
        var uhr = Stopwatch.StartNew();
        try
        {
            while (true)
            {
                var zeile = kind.StandardOutput.ReadLineAsync();
                if (!zeile.Wait(TimeSpan.FromSeconds(60)) || zeile.Result is null)
                {
                    throw new InvalidOperationException("Absturzprobe: Kind ohne erwartete Meldung beendet");
                }
                if (art != ArtSpeichern && (zeile.Result == Halt || zeile.Result == Fertig))
                {
                    break;
                }
                if (zeile.Result.StartsWith("gespeichert ", StringComparison.Ordinal))
                {
                    gemeldet.Add(zeile.Result["gespeichert ".Length..]);
                    if (gemeldet.Count >= mindestens)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            kind.Kill(true);
            kind.WaitForExit();
        }
        return gemeldet;
    }
}
