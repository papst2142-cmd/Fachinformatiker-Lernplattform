using Avalonia;
using Lernplattform.Core.Update;
using Lernplattform.Desktop;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
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
    public const string Kanal = Updatequelle.Kanal;

    [STAThread]
    public static int Main(string[] args)
    {
        var auto = Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_AUTO") == "1"
            && Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_TEIL") == "stadtbau";
        // S 0.25.1 (Sammelliste 249/250): Datenordner anlegen, damit das Velopack-Protokoll in update.log ankommt
        var ordner = StadtbauApp.Ordner;
        try
        {
            ordner.Anlegen();
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
        }
        VelopackApp.Build().SetAutoApplyOnStartup(false).SetLogger(new Updatequelle.Protokoll(ordner.Wurzel)).Run();
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable(Werkzeugordner.Umgebung) is not { Length: > 0 })
        {
            // Sammelliste 250: Daten aus dem alten Ordner (= Installationsordner) einmal herueberkopieren
            Datenumzug.Windows(ordner);
        }
        if (auto)
        {
            Messung.StadtbauMessung.Einrichten();
        }
        else if (OperatingSystem.IsWindows())
        {
            Oberflaeche.Aktualisierung.Pruefen = Pruefen;
        }
        if (Environment.GetEnvironmentVariable(UpdateProbe.Umgebung) is { Length: > 0 } probe)
        {
            // nur CI (Weisung P12-107 Punkt 3): Suche gegen das echte GitHub ohne Fenster
            return UpdateProbe.Ausfuehren(probe, Pruefen);
        }
        // Phase 13 Welle 3: wie in der Lernplattform erlaubt nur der Start-Test (Xvfb der CI) den Mesa-Softwaretreiber
        // llvmpipe, damit die 3D-Ansicht dort OpenGL bekommt. Ohne Schalter gilt die Vorgabe von Avalonia.
        var x11 = new X11PlatformOptions();
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_GL_LLVMPIPE") == "1")
        {
            x11.GlxRendererBlacklist = [];
        }
        return AppBuilder.Configure<StadtbauApp>().UsePlatformDetect().With(x11).WithInterFont().LogToTrace().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Neue Version der Linie vS suchen (Ergebnis fuer den Start und fuer "Nach Updates suchen"). Jeder Schritt steht
    /// seit S 0.25.1 in update.log des Datenordners (Sammelliste 249).
    /// </summary>
    internal static async Task<UpdateErgebnis> Pruefen()
    {
        var log = StadtbauApp.Ordner.Wurzel;
        var mgr = Updatequelle.Manager();
        if (!mgr.IsInstalled)
        {
            return new UpdateErgebnis(UpdateArt.NichtInstalliert);
        }
        Updateprotokoll.Schreiben("Suche nach Updates (Linie vS, Kanal " + Kanal + "), installiert: " + mgr.CurrentVersion, log);
        UpdateInfo? neu;
        try
        {
            neu = await mgr.CheckForUpdatesAsync();
        }
        catch (Exception x) when (x is not OutOfMemoryException)
        {
            Updateprotokoll.Schreiben("Suche gescheitert: " + x.GetType().Name + ": " + x.Message, log);
            throw;
        }
        if (neu is null)
        {
            Updateprotokoll.Schreiben("Keine neuere Version gefunden.", log);
            return new UpdateErgebnis(UpdateArt.Aktuell);
        }
        var version = neu.TargetFullRelease.Version.ToString();
        Updateprotokoll.Schreiben("Neue Version gefunden: " + version, log);
        return new UpdateErgebnis(UpdateArt.Neu, version, async () =>
        {
            try
            {
                await mgr.DownloadUpdatesAsync(neu);
                Updateprotokoll.Schreiben("Version " + version + " geladen, wird eingespielt, das Werkzeug startet neu.", log);
                mgr.ApplyUpdatesAndRestart(neu);
            }
            catch (Exception x) when (x is not OutOfMemoryException)
            {
                Updateprotokoll.Schreiben("Laden oder Einspielen gescheitert: " + x.GetType().Name + ": " + x.Message, log);
                throw;
            }
        });
    }
}
