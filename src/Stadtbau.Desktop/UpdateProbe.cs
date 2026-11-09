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
/// Jeder Netz-Schritt wird bei einer Stoerung von GitHub (5xx wie 504, Zeitueberschreitung) bis zu 4 Mal versucht
/// (<see cref="Stoerung.Wiederholen{T}"/>), jede Wiederholung steht als Zeile WIEDERHOLUNG im Bericht.
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
        // Sammelliste 258 (Lauf 745: 403 rate limit): die Probe fragt GitHub angemeldet, wenn die CI GITHUB_TOKEN setzt
        var zugang = Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { Length: > 0 } t ? t : null;
        Zeile("ANGEMELDET " + (zugang is null ? "nein" : "ja"));
        try
        {
            switch (modus)
            {
                case "feed":
                    Zeile("FEED werkzeug " + Stoerung.Wiederholen("Feed werkzeug", () => Neueste(Updatequelle.Quelle(Zeile, zugang), Updatequelle.Kanal), Zeile));
                    // Linie vA ueber dieselbe stoerungsfeste Abfrage (die LinienQuelle der Lernplattform laedt alle Feeds
                    // und scheitert bei den heutigen 504 fast immer, Sammelliste 256)
                    Zeile("FEED lernplattform " + Stoerung.Wiederholen("Feed lernplattform",
                        () => Neueste(new StoerungsfesteQuelle("https://github.com/" + GithubSuche.Repository, ReleaseLinie.Linie.Lernplattform, "win", Zeile, zugang: zugang), "win"), Zeile));
                    return 0;
                case "update":
                    var mgr = Updatequelle.Manager(Zeile, zugang);
                    Zeile("INSTALLIERT " + (mgr.IsInstalled ? mgr.CurrentVersion?.ToString() : "nein"));
                    if (!mgr.IsInstalled)
                    {
                        return 1;
                    }
                    var neu = Stoerung.Wiederholen("Suche", () => mgr.CheckForUpdatesAsync().GetAwaiter().GetResult(), Zeile);
                    if (neu is null)
                    {
                        Zeile("KEIN ANGEBOT");
                        return 1;
                    }
                    Zeile("ANGEBOT " + neu.TargetFullRelease.Version);
                    Stoerung.Wiederholen("Laden", () => { mgr.DownloadUpdatesAsync(neu).GetAwaiter().GetResult(); return true; }, Zeile);
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
