#if UPDATEPROBE
using System.Reflection;
using Avalonia.Threading;
using Lernplattform.Core.Update;
using Aktuell = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Android;

/// <summary>
/// A 0.39.0 (Weisung P11-40 Frage 5a): Emulator-Probe der CI, NUR mit -p:UpdateProbe=true gebaut, nie ausgeliefert.
/// Beim Start: update.log ins Protokoll (zeigt, ob das letzte Update abgeschlossen ist), dann ueber den Testserver
/// suchen und mit dem echten Ablauf (UpdateDienst.AktualisierenAsync: Laden, Pruefen, Kopie, Merker, Session)
/// aktualisieren. Jede Zeile beginnt mit "UPDATEPROBE:" im Android-Protokoll (logcat); werkzeuge/android/update_probe.sh liest sie.
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

    /// <summary>Intent-Zusatz fuer die Gegenprobe des Vollbilds (MainActivity.OnCreate).</summary>
    public const string Gegenprobe = "vollbild_gegenprobe";

    /// <summary>
    /// Patch A 0.41.1 (Sammelliste 218): nach jedem OnResume (Start und Rueckkehr aus dem Hintergrund) 5 s warten, dann
    /// melden, ob Android die Statusleiste zeigt, und was das Programm will (Merker "vollbild", Gegenprobe).
    /// </summary>
    public static void LeistenMelden(global::Android.App.Activity activity)
    {
        new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!).PostDelayed(() =>
        {
            var insets = activity.Window?.DecorView.RootWindowInsets;
            var sichtbar = !OperatingSystem.IsAndroidVersionAtLeast(30) || insets is null ? "unbekannt"
                : insets.IsVisible(global::Android.Views.WindowInsets.Type.StatusBars()) ? "ja" : "nein";
            Log("LEISTEN sichtbar " + sichtbar + ", vollbild " + (Lernplattform.Spiel.Vollbild.Dauerhaft ? "an" : "aus")
                + ", gegenprobe " + (Lernplattform.Spiel.Vollbild.LeistenUeberAvalonia ? "nein" : "ja"));
        }, 5000);
    }

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
                Log("START Version " + Aktuell.Anzeige + ", Versionscode " + installer.EigenerVersionscode + ", darf installieren " + installer.DarfInstallieren
                    + ", WLAN " + (installer.ImWlan?.ToString() ?? "unbekannt"));
                var log = Updateprotokoll.Pfad();
                if (File.Exists(log))
                {
                    foreach (var zeile in await File.ReadAllLinesAsync(log))
                    {
                        Log("LOG " + zeile);
                    }
                }
                var ordner = Path.Combine(Lernplattform.Core.Daten.Ablage.Datenordner(), Lernplattform.Core.Daten.Uebernahme.SicherungsOrdner);
                Log("KOPIEN " + (Directory.Exists(ordner) ? string.Join(",", Directory.GetFiles(ordner).Select(Path.GetFileName)) : "-"));
                var angebot = await UpdateAblauf.SuchenAsync(Aktuell.Quelle!());
                if (angebot is null)
                {
                    Log("KEIN ANGEBOT");
                    // Patch A 0.41.1 (Sammelliste 218): Merker "vollbild" wie nach dem Schalter in den Optionen speichern,
                    // ohne ihn jetzt anzuwenden; update_probe.sh startet danach neu und liest LEISTEN (LeistenMelden)
                    var gemerkt = await Dispatcher.UIThread.InvokeAsync(() =>
                        Lernplattform.Programm.Zustand.Aendere(w => Lernplattform.Core.Spiel.Ton.VollbildWahl.Wert(w, true)));
                    Log("VOLLBILD MERKER " + (gemerkt ? "an" : "FEHLER"));
                    return;
                }
                Log("ANGEBOT " + angebot.Version + " " + angebot.DateiName + " " + angebot.Groesse);
                var text = await Dispatcher.UIThread.InvokeAsync(() => Lernplattform.Programm.UpdateDienst.AktualisierenAsync(angebot, (_, _) => { }, () => { }));
                Log("UEBERGEBEN " + text);
            }
            catch (Exception e)
            {
                Log("FEHLER " + e.GetType().Name + ": " + e.Message);
            }
        });
    }
}
#endif
