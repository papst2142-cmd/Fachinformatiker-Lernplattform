using Lernplattform.Core.Update;
using Lernplattform.Desktop;
using Stadtbau.Core;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Stadtbau.Desktop;

/// <summary>
/// Sammelliste 249 (Weisung P12-107 Punkt 3): Probe des Updates am PC gegen das ECHTE GitHub, nur in der CI
/// (werkzeuge/stadtbau/update_probe_pc.sh, Teil von baue_stadtbau.sh win-x64). Ohne Fenster; Ergebnis als Zeilen in die
/// Datei aus <see cref="BerichtUmgebung"/>. Modi:
///   feed    = lesend: neueste Version im Feed der Linie vS (Kanal stadtbau-win) und der Lernplattform (vA, Kanal win)
///   update  = das installierte Werkzeug sucht wie der Knopf "Nach Updates suchen", laedt und spielt ein (ohne Neustart,
///             Velopack ersetzt nach dem Ende des Prozesses), Zeilen ANGEBOT, GELADEN, EINSPIELEN.
/// Rueckgabe 0 = ok, 1 = Fehler (Zeile FEHLER mit Art und Text).
/// </summary>
internal static class UpdateProbe
{
    public const string Umgebung = "STADTBAU_UPDATE_PROBE";
    public const string BerichtUmgebung = "STADTBAU_UPDATE_PROBE_BERICHT";

    public static int Ausfuehren(string modus, Func<Task<UpdateErgebnis>> _)
    {
        var bericht = Environment.GetEnvironmentVariable(BerichtUmgebung) is { Length: > 0 } b ? b : "update_probe.txt";
        void Zeile(string text) => File.AppendAllText(bericht, text + Environment.NewLine);
        try
        {
            switch (modus)
            {
                case "feed":
                    Zeile("FEED werkzeug " + Neueste(Updatequelle.Quelle(), Updatequelle.Kanal));
                    Zeile("FEED lernplattform " + Neueste(new LinienQuelle("https://github.com/" + GithubSuche.Repository, ReleaseLinie.Linie.Lernplattform), "win"));
                    return 0;
                case "update":
                    var mgr = Updatequelle.Manager();
                    Zeile("INSTALLIERT " + (mgr.IsInstalled ? mgr.CurrentVersion?.ToString() : "nein"));
                    if (!mgr.IsInstalled)
                    {
                        return 1;
                    }
                    var neu = mgr.CheckForUpdatesAsync().GetAwaiter().GetResult();
                    if (neu is null)
                    {
                        Zeile("KEIN ANGEBOT");
                        return 1;
                    }
                    Zeile("ANGEBOT " + neu.TargetFullRelease.Version);
                    mgr.DownloadUpdatesAsync(neu).GetAwaiter().GetResult();
                    Zeile("GELADEN " + neu.TargetFullRelease.FileName);
                    Updateprotokoll.Schreiben("Probe: Version " + neu.TargetFullRelease.Version + " geladen, wird nach dem Ende eingespielt.", StadtbauApp.Ordner.Wurzel);
                    mgr.WaitExitThenApplyUpdates(neu, silent: true, restart: false);
                    Zeile("EINSPIELEN gestartet");
                    return 0;
                default:
                    Zeile("FEHLER unbekannter Modus " + modus);
                    return 1;
            }
        }
        catch (Exception x) when (x is not OutOfMemoryException)
        {
            Zeile("FEHLER " + x.GetType().Name + ": " + x.Message);
            return 1;
        }
    }

    /// <summary>Hoechste Version im Feed eines Kanals (lesend, nichts geladen).</summary>
    private static string Neueste(IUpdateSource quelle, string kanal)
    {
        var feed = quelle.GetReleaseFeed(NullVelopackLogger.Instance, null, kanal).GetAwaiter().GetResult();
        return feed.Assets.Select(a => a.Version).Max()?.ToString() ?? "keine";
    }
}
