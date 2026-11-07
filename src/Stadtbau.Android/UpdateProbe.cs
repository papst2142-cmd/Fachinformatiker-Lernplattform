#if UPDATEPROBE
using System.Reflection;
using Lernplattform.Android;
using Lernplattform.Core.Update;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Android;

/// <summary>
/// S 0.6.0 (Weisung P12-42 Punkt 2): Emulator-Probe der CI fuer das Update des Werkzeugs, NUR mit -p:UpdateProbe=true
/// gebaut, nie ausgeliefert. Beim Start: update.log ins Protokoll, dann ueber den Testserver suchen (echte Suche
/// HandyQuelle mit Linie Stadtbau ueber die Liste der Releases, in der auch ein neueres Release der Lernplattform steht)
/// und mit dem echten Ablauf ohne Fenster aktualisieren (<see cref="TabletUpdate.UebergebenAsync"/>: Laden, Pruefen,
/// Session). Jede Zeile beginnt mit "UPDATEPROBE:" im Android-Protokoll; werkzeuge/stadtbau/update_probe.sh liest sie.
/// </summary>
internal static class UpdateProbe
{
    private static int _gestartet;

    public static (string Url, string Fingerabdruck) Ziel()
    {
        string Wert(string schluessel) => typeof(UpdateProbe).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == schluessel)?.Value ?? "";
        return (Wert("UpdateProbeUrl"), Wert("UpdateProbeFingerabdruck"));
    }

    private static void Log(string text) => global::Android.Util.Log.Info("UPDATEPROBE", "UPDATEPROBE: " + text);

    public static void Starten(HandyInstaller installer)
    {
        if (Interlocked.Exchange(ref _gestartet, 1) == 1)
        {
            return;
        }
        HandyInstaller.Empfaenger.Melden = (status, meldung) => Log("STATUS " + status + " " + HandyPruefung.StatusName(status) + " " + (meldung ?? ""));
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(12));
                Log("START Version " + Werkzeug.Anzeige + ", Versionscode " + installer.EigenerVersionscode + ", Paket " + installer.EigenesPaket
                    + ", darf installieren " + installer.DarfInstallieren + ", WLAN " + (installer.ImWlan?.ToString() ?? "unbekannt"));
                var log = Updateprotokoll.Pfad(StadtbauApp.Ordner.Wurzel);
                if (File.Exists(log))
                {
                    foreach (var zeile in await File.ReadAllLinesAsync(log))
                    {
                        Log("LOG " + zeile);
                    }
                }
                var ergebnis = await UpdateSuche.SuchenAsync(TabletUpdate.SuchenAsync);
                if (ergebnis.Art != UpdateArt.Neu || ergebnis.Angebot is not { } angebot)
                {
                    Log(ergebnis.Art == UpdateArt.Fehler ? "FEHLER Suche: " + ergebnis.Grund : "KEIN ANGEBOT");
                    return;
                }
                Log("ANGEBOT " + angebot.Version + " " + angebot.DateiName + " " + angebot.Groesse);
                var problem = await TabletUpdate.UebergebenAsync(TabletUpdate.Quelle!(), angebot, text => Log("FEHLER " + text));
                Log(problem is null ? "UEBERGEBEN" : "FEHLER " + problem);
            }
            catch (Exception e)
            {
                Log("FEHLER " + e.GetType().Name + ": " + e.Message);
            }
        });
    }
}
#endif
