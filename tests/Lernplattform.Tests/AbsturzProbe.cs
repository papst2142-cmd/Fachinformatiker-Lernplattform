using System.Diagnostics;
using System.Runtime.CompilerServices;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pruefung;

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
    public const string ArtTrainer = "trainer";
    public const string ArtProjektfeld = "projektfeld";
    public const string ArtUebernahme = "uebernahme";
    public const string ArtVorschau = "vorschau";
    public const string ArtAbschluss = "abschluss";
    public const string ArtVorLoeschen = "vor_loeschen";
    public const string ArtAusbildung = "ausbildung";
    public const string ArtProjektLoeschen = "projekt_loeschen";
    public const string ArtProjektfelderViele = "projektfelder_viele";
    public const string ArtKlausurFesthalten = "klausur_festhalten";
    public const string ArtEinrichten = "einrichten";
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
                case ArtTrainer:
                    var nr = "";
                    var trainer = new LernstandSchreiber(new Lernstand(args[0]), null, () => "probe-" + nr);
                    while (true)
                    {
                        nr = Guid.NewGuid().ToString("N");
                        // Lange Art, damit ein Schreibvorgang mehrere Seiten der Datei beruehrt
                        if (trainer.TrainerSpeichern("art " + nr + new string('.', 3000), 3, true))
                        {
                            Console.Out.WriteLine("gespeichert probe-" + nr);
                            Console.Out.Flush();
                        }
                    }
                case ArtProjektfeld:
                    var nf = "";
                    var feld = new LernstandSchreiber(new Lernstand(args[0]), null, () => "probe-" + nf);
                    for (var runde = 0; ; runde++)
                    {
                        nf = Guid.NewGuid().ToString("N");
                        // Lange Werte, damit INSERT und Aufraeumen mehrere Seiten der Datei beruehren
                        if (feld.ProjektFeldSpeichern("probe-projekt", "feld " + (runde % 5), "wert " + nf + new string('.', 3000)))
                        {
                            Console.Out.WriteLine("gespeichert probe-" + nf);
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
                case ArtAbschluss:
                    var na = "";
                    var abschluss = new LernstandSchreiber(new Lernstand(args[0]), null, () => "probe-" + na + "-" + Guid.NewGuid().ToString("N"));
                    while (true)
                    {
                        na = Guid.NewGuid().ToString("N");
                        // 30 Antworten mit langen Fragen, damit die eine Buchung viele Seiten der Datei beruehrt
                        var zeilen = Enumerable.Range(0, 30)
                            .Select(i => (Abschlusszeile)new QuizAntwort("WiSo", "Frage " + na + " " + i + new string('.', 3000), i % 2 == 0)).ToList();
                        if (abschluss.PruefungAbschliessen(na, "wiso", 50, "4 (Ausreichend)", 60, "{}", zeilen))
                        {
                            Console.Out.WriteLine("gespeichert " + na);
                            Console.Out.Flush();
                        }
                    }
                case ArtVorLoeschen:
                    // Abschluss bis vor clear_running: das Entfernen der laufenden Pruefung haelt an
                    var ablage = KlausurAbsturzTests.DateiAblage(args[1], neu =>
                    {
                        if (!neu.ContainsKey(LaufendePruefung.Schluessel))
                        {
                            Console.Out.WriteLine(Halt);
                            Console.Out.Flush();
                            Thread.Sleep(Timeout.Infinite);
                        }
                    });
                    var ablauf = KlausurAbsturzTests.Ablauf(args[0], ablage);
                    ablauf.Zeigen();
                    if (ablauf.ImLauf)
                    {
                        ablauf.Abgeben(auto: false);    // WiSo: Abgabe gespeichert, dann gleich der Abschluss
                    }
                    else
                    {
                        ablauf.Abschliessen();
                    }
                    Console.Out.WriteLine(Fertig);
                    Console.Out.Flush();
                    break;
                case ArtKlausurFesthalten:
                    // W4 (Sammelliste 51): offene Klausur-Antworten immer wieder festhalten wie bei Zustand.EingabenSpeichern
                    var probe = KlausurAblaufTests.EchteAblage(args[1]);
                    var klausur = KlausurAbsturzTests.Ablauf(args[0], probe);
                    klausur.Starten(Klausur.Konzeption);
                    while (true)
                    {
                        var nk = Guid.NewGuid().ToString("N");
                        if (klausur.Festhalten([(Klausur.Schluessel(0, 0), "Antwort " + nk + new string('.', 3000))]))
                        {
                            Console.Out.WriteLine("gespeichert " + nk);
                            Console.Out.Flush();
                        }
                    }
                case ArtEinrichten:
                    // W4: Rundgang "Fertig" (Name, Termin, Merker) immer wieder in einem Schreibvorgang
                    var einrichten = new Einstellungen(args[0]);
                    while (true)
                    {
                        var ne = "N" + Guid.NewGuid().ToString("N");
                        var (_, aenderung) = Lernplattform.Core.Hilfe.Einrichten.Speichern(ne, new Dictionary<string, bool> { ["B"] = true },
                            "1.2.2027", "", new DateOnly(2026, 10, 22));
                        var werte = einrichten.Laden();
                        aenderung!(werte);
                        werte["probe"] = ne;
                        if (einrichten.Speichern(werte))
                        {
                            Console.Out.WriteLine("gespeichert " + ne);
                            Console.Out.Flush();
                        }
                    }
                case ArtAusbildung:
                    var ausbildung = new LernstandSchreiber(new Lernstand(args[0]));
                    for (var runde = 0; ; runde++)
                    {
                        var wert = Ausbildung.Werte[runde % Ausbildung.Werte.Length];
                        if (Ausbildung.Waehlen(ausbildung, wert))
                        {
                            Console.Out.WriteLine("gespeichert " + runde);
                            Console.Out.Flush();
                        }
                    }
                case ArtProjektfelderViele:
                    var nv = "";
                    var viele = new LernstandSchreiber(new Lernstand(args[0]), null, () => "probe-" + nv);
                    for (var runde = 0; ; runde++)
                    {
                        nv = Guid.NewGuid().ToString("N");
                        // 6 Projekte x 8 Felder, lange Werte: INSERT und Aufraeumen beruehren viele Seiten
                        if (viele.ProjektFeldSpeichern("probe-" + (runde % 6), "feld " + (runde / 6 % 8), "wert " + nv + new string('.', 3000)))
                        {
                            Console.Out.WriteLine("gespeichert probe-" + nv);
                            Console.Out.Flush();
                        }
                    }
                case ArtProjektLoeschen:
                    var loeschen = new LernstandSchreiber(new Lernstand(args[0]));
                    for (var runde = 0; ; runde++)
                    {
                        // je Runde ein Projekt mit 5 langen Feldern, dann in einer Buchung loeschen (Diff A)
                        var projekt = "probe-" + runde;
                        for (var f = 0; f < 5; f++)
                        {
                            loeschen.ProjektFeldSpeichern(projekt, "feld " + f, "wert " + runde + new string('.', 3000));
                        }
                        if (loeschen.ProjektLoeschen(projekt))
                        {
                            Console.Out.WriteLine("gespeichert " + projekt);
                            Console.Out.Flush();
                        }
                    }
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
    /// Kind starten und hart beenden: bei ArtSpeichern/ArtTrainer nach mindestens Meldungen, bei ArtUebernahme
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
                if (art != ArtSpeichern && art != ArtTrainer && art != ArtProjektfeld && art != ArtAbschluss && art != ArtAusbildung && art != ArtKlausurFesthalten && art != ArtEinrichten
                    && art != ArtProjektLoeschen && art != ArtProjektfelderViele
                    && (zeile.Result == Halt || zeile.Result == Fertig))
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
