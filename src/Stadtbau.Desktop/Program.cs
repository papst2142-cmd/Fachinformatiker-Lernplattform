using Avalonia;
using Lernplattform.Core.Update;
using Lernplattform.Desktop;
using Stadtbau.Core;
using Velopack;

namespace Stadtbau.Desktop;

/// <summary>
/// Phase 13 Welle 2: Start des Werkzeugs. Velopack zuerst (Installation, Update), dann das Fenster. Updates nur aus
/// Releases der Linie vS (<see cref="LinienQuelle"/>), eigener Kanal <see cref="Kanal"/>, nie von allein beim Start.
/// Start-Test: LERNPLATTFORM_MESSUNG_AUTO=1 und LERNPLATTFORM_MESSUNG_TEIL=stadtbau.
/// </summary>
internal static class Program
{
    /// <summary>Velopack-Kanal des Werkzeugs (die Lernplattform nutzt die Vorgabe "win").</summary>
    public const string Kanal = "stadtbau-win";

    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        var auto = Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_AUTO") == "1"
            && Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_TEIL") == "stadtbau";
        if (auto)
        {
            Messung.StadtbauMessung.Einrichten();
        }
        else if (OperatingSystem.IsWindows())
        {
            Oberflaeche.Aktualisierung.Pruefen = Pruefen;
        }
        return AppBuilder.Configure<StadtbauApp>().UsePlatformDetect().WithInterFont().LogToTrace().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Neue Version der Linie vS suchen und laden; Rueckgabe: Anzeige der neuen Version oder null.</summary>
    private static async Task<Func<Task>?> Pruefen()
    {
        var mgr = new UpdateManager(new LinienQuelle(Werkzeug.Repository, ReleaseLinie.Linie.Stadtbau), new UpdateOptions { ExplicitChannel = Kanal });
        if (!mgr.IsInstalled)
        {
            return null;
        }
        var neu = await mgr.CheckForUpdatesAsync();
        if (neu is null)
        {
            return null;
        }
        return async () =>
        {
            await mgr.DownloadUpdatesAsync(neu);
            mgr.ApplyUpdatesAndRestart(neu);
        };
    }
}
