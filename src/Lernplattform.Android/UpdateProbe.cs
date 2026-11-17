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
                    await PaketeAsync();
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

    /// <summary>
    /// Welle A 0.89.0 (Inhaltspakete W3, Weisung P12-297 W3-4): echtes Laden eines Inhaltspakets am Handy. Der Testserver von
    /// update_probe.sh liefert die Release-Liste im Format von GitHub (<c>releases.json</c>, erzeugt von
    /// <c>paket_release.py github-probe</c>) und die Paketdateien. Die Verwaltung ist dieselbe wie in der App (Quelle ueber
    /// <see cref="Lernplattform.Core.Pakete.PaketQuelle.Fuer"/>, Ablage im Datenordner, Pruefung gegen den digest); nur die Adresse
    /// der Liste ist die des Testservers. Zeilen "PAKET ..." liest update_probe.sh.
    /// </summary>
    private static async Task PaketeAsync()
    {
        try
        {
            var (url, _) = Ziel();
            var liste = url[..(url.LastIndexOf('/') + 1)] + "releases.json";
            Environment.SetEnvironmentVariable(Lernplattform.Core.Pakete.PaketQuelle.Umgebung, liste);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var verwaltung = new Lernplattform.Core.Pakete.PaketVerwaltung(new Lernplattform.Core.Pakete.PaketAblage(Lernplattform.Core.Daten.Ablage.Datenordner()),
                () => Lernplattform.Core.Pakete.PaketQuelle.Fuer(http, z => Log("PAKET STOERUNG " + z)), Aktuell.Anzeige);
            Log("PAKET QUELLE " + liste);
            await verwaltung.SuchenAsync();
            Log("PAKET LISTE " + string.Join(",", verwaltung.Angebote.Select(a => a.Kennung)) + (verwaltung.SuchFehler is { } f ? " FEHLER " + f : ""));
            if (verwaltung.Angebote.FirstOrDefault(a => a.Kennung == "probe_kaputt") is { } kaputt)
            {
                Log("PAKET KAPUTT " + (await verwaltung.LadenAsync(kaputt, null, CancellationToken.None) ?? "ANGENOMMEN"));
            }
            if (verwaltung.Angebote.FirstOrDefault(a => a.Kennung == "probe_quests") is { } quests)
            {
                var grund = await verwaltung.LadenAsync(quests, null, CancellationToken.None);
                var staende = verwaltung.Staedte().Where(s => s.Kopf.Kennung == "probe_quests").Select(s => s.Kopf.Stand).ToList();
                Log(grund is null && staende.Count == 1 ? "PAKET GELADEN probe_quests " + staende[0] + " digest " + quests.Sha256 : "PAKET NICHT GELADEN " + grund);
                verwaltung.Entfernen("probe_quests");
            }
        }
        catch (Exception e)
        {
            Log("PAKET FEHLER " + e.GetType().Name + ": " + e.Message);
        }
        Log("PAKET ENDE");
    }
}
#endif
