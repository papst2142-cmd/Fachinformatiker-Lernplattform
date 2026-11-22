#if UPDATEPROBE
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Lernplattform.Android;

/// <summary>
/// Welle Fehlerpruefung Teil 2 (Weisung P12-345, Sammelliste 426): Probe der Konfigurationswechsel im Emulator, NUR in der
/// Probe-APK der CI (<c>-p:UpdateProbe=true</c>, nie ausgeliefert). Start mit <c>--ez konfig_probe true</c>. Jede Zeile beginnt
/// mit „KONFIGPROBE:“: <c>ONCREATE n</c> (n = wie oft die Activity in diesem Prozess angelegt wurde), <c>GEAENDERT</c> mit
/// Dichte und Groesse, danach <c>ANSICHT</c> mit der Groesse, die Avalonia nach dem Wechsel zeichnet.
/// <c>werkzeuge/android/konfig_probe.sh</c> aendert Dichte und Groesse (<c>wm density</c>, <c>wm size</c>) und liest die Zeilen.
/// </summary>
internal static class KonfigProbe
{
    public const string Schalter = "konfig_probe";

    private static int _angelegt;

    /// <summary>Die Probe laeuft in diesem Start (die Update-Probe sucht dann nicht).</summary>
    public static bool An { get; private set; }

    private static void Log(string text) => global::Android.Util.Log.Info("KONFIGPROBE", "KONFIGPROBE: " + text);

    public static void OnCreate(global::Android.Content.Intent? intent)
    {
        _angelegt++;
        An = An || intent?.GetBooleanExtra(Schalter, false) == true;
        if (An)
        {
            Log("ONCREATE " + _angelegt);
        }
    }

    public static void Geaendert(global::Android.Content.Res.Configuration c)
    {
        if (!An)
        {
            return;
        }
        Log($"GEAENDERT dichte {c.DensityDpi} breite {c.ScreenWidthDp} hoehe {c.ScreenHeightDp} kleinste {c.SmallestScreenWidthDp}");
        // nach dem Wechsel: welche Groesse hat die Ansicht von Avalonia (in dp), und laeuft der Oberflaechen-Faden weiter?
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var ansicht = (global::Avalonia.Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime)?.MainView;
                var oben = ansicht is null ? null : TopLevel.GetTopLevel(ansicht);
                var g = oben?.ClientSize ?? default;
                Log($"ANSICHT breite {g.Width:F0} hoehe {g.Height:F0} skalierung {(oben?.RenderScaling ?? 0):F2} aufgebaut {(ansicht?.IsLoaded == true ? "ja" : "nein")}");
            });
        });
    }
}
#endif
