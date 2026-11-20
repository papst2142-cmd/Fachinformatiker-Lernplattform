#if UPDATEPROBE
using Avalonia.Threading;

namespace Lernplattform.Android;

/// <summary>
/// Welle A 0.98.0 (Weisung P12-315, Sammelliste 391): Touch-Probe im Emulator, NUR in der Probe-APK der CI
/// (<c>-p:UpdateProbe=true</c>, nie ausgeliefert). Start mit <c>--ez touch_probe true</c> (dazu <c>--ez touch_umkehren true</c>
/// fuer den zweiten Start mit „Umsehen links/rechts umkehren“). Das Programm oeffnet das Spiel (<see cref="Lernplattform.Messung.AutoMessung.TouchProbeAsync"/>)
/// und schreibt jede Zeile mit „TOUCHPROBE:“ ins Android-Protokoll; <c>werkzeuge/android/touch_probe.sh</c> setzt die Finger und liest sie.
/// </summary>
internal static class TouchProbe
{
    public const string Schalter = "touch_probe";

    public const string Umkehren = "touch_umkehren";

    /// <summary>Die Probe laeuft in diesem Start (die Update-Probe sucht dann nicht).</summary>
    public static bool An { get; private set; }

    private static void Log(string text) => global::Android.Util.Log.Info("TOUCHPROBE", "TOUCHPROBE: " + text);

    public static void Starten(global::Android.Content.Intent? intent)
    {
        if (intent is null || !intent.GetBooleanExtra(Schalter, false))
        {
            return;
        }
        An = true;
        var umkehren = intent.GetBooleanExtra(Umkehren, false);
        _ = Task.Run(async () =>
        {
            // auf die Ansicht warten (sie entsteht in base.OnCreate, Inhalte laden im Hintergrund)
            for (var i = 0; i < 120 && Lernplattform.Views.MainView.Zuletzt is null; i++)
            {
                await Task.Delay(500);
            }
            await Task.Delay(3000);
            if (Lernplattform.Views.MainView.Zuletzt is not { } ansicht)
            {
                Log("FEHLER keine Ansicht");
                return;
            }
            await Dispatcher.UIThread.InvokeAsync(() => Lernplattform.Messung.AutoMessung.TouchProbeAsync(ansicht, Log, umkehren));
        });
    }
}
#endif
