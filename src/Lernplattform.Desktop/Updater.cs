using System;
using System.IO;
using System.Threading.Tasks;
using Lernplattform.Core.Daten;
using Velopack;
using Velopack.Sources;
using Aktuell = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Desktop;

/// <summary>
/// Phase 0, Spike D: Update ueber Velopack aus einem Ordner (Windows, Linux-AppImage,
/// macOS). Spaeter kommt die Quelle GitHub-Releases dazu; dafuer braucht es Releases,
/// die erst nach Freigabe entstehen.
/// </summary>
internal static class Updater
{
    public static void Verbinden()
    {
        Aktuell.Zustand = () =>
        {
            try
            {
                var mgr = new UpdateManager(new SimpleFileSource(new DirectoryInfo(Path.GetTempPath())));
                return mgr.IsInstalled
                    ? "installiert, Paketversion " + mgr.CurrentVersion
                    : "nicht installiert (Ordner-Programm), Paketversion " + Aktuell.PaketVersion;
            }
            catch (Exception fehler)
            {
                return "Updater-Fehler " + fehler.GetType().Name;
            }
        };
        Aktuell.AusOrdner = async ordner =>
        {
            var mgr = new UpdateManager(new SimpleFileSource(new DirectoryInfo(ordner)));
            if (!mgr.IsInstalled)
            {
                return "Update geht nur im installierten Programm (Setup bzw. AppImage).";
            }
            var neu = await mgr.CheckForUpdatesAsync();
            if (neu is null)
            {
                return "Kein neueres Paket im Ordner (installiert: " + mgr.CurrentVersion + ").";
            }
            await mgr.DownloadUpdatesAsync(neu);
            mgr.ApplyUpdatesAndRestart(neu.TargetFullRelease);
            return "Update wird eingespielt, Programm startet neu.";
        };
    }

    /// <summary>
    /// Ablauf ohne Oberflaeche fuer die CI (LERNPLATTFORM_UPDATE_AUTO):
    /// "suchen" spielt das Paket aus LERNPLATTFORM_UPDATE_ORDNER ein und beendet,
    /// "bericht" schreibt Version und Datenordner-Probe. Ausgabe in LERNPLATTFORM_UPDATE_BERICHT.
    /// </summary>
    public static int Auto(string modus)
    {
        var bericht = Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_BERICHT") is { Length: > 0 } b ? b : "update_bericht.txt";
        try
        {
            var ordner = Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_ORDNER") ?? Path.GetTempPath();
            var mgr = new UpdateManager(new SimpleFileSource(new DirectoryInfo(ordner)));
            var version = mgr.IsInstalled ? mgr.CurrentVersion?.ToString() ?? "?" : "nicht installiert";
            var probe = Ablage.UpdateProbe(version);
            File.AppendAllText(bericht, $"{modus}: Version {version}; Probe: {probe}{Environment.NewLine}");
            if (modus != "suchen" || !mgr.IsInstalled)
            {
                return mgr.IsInstalled ? 0 : 3;
            }
            var neu = Task.Run(() => mgr.CheckForUpdatesAsync()).GetAwaiter().GetResult();
            if (neu is null)
            {
                File.AppendAllText(bericht, "suchen: kein neueres Paket" + Environment.NewLine);
                return 4;
            }
            Task.Run(() => mgr.DownloadUpdatesAsync(neu)).GetAwaiter().GetResult();
            File.AppendAllText(bericht, $"suchen: spiele {neu.TargetFullRelease.Version} ein{Environment.NewLine}");
            mgr.ApplyUpdatesAndExit(neu.TargetFullRelease);
            return 0;
        }
        catch (Exception fehler)
        {
            File.AppendAllText(bericht, $"{modus}: FEHLER {fehler.GetType().Name}: {fehler.Message}{Environment.NewLine}");
            return 5;
        }
    }
}
