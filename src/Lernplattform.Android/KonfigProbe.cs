#if UPDATEPROBE
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Lernplattform.Android;

/// <summary>
/// Welle Fehlerpruefung Teil 2 (Weisung P12-345, Sammelliste 426): Probe der Konfigurationswechsel im Emulator, NUR in der
/// Probe-APK der CI (<c>-p:UpdateProbe=true</c>, nie ausgeliefert). Start mit <c>--ez konfig_probe true</c>. Jede Zeile beginnt
/// mit „KONFIGPROBE:“: <c>ONCREATE n</c> (n = wie oft die Activity in diesem Prozess angelegt wurde), <c>GEAENDERT</c> mit
/// Dichte und Groesse, danach <c>ANSICHT</c> mit der Groesse und Skalierung, die Avalonia nach dem Wechsel zeichnet.
/// <c>werkzeuge/android/konfig_probe.sh</c> aendert Dichte und Groesse (<c>wm density</c>, <c>wm size</c>) und liest die Zeilen.
/// Welle A 1.3.2 (Weisung P12-350, Sammelliste 434/435): eine Datei fuer Lernplattform und Werkzeug; <c>ANSICHT</c> nennt auch
/// die Skalierung, die zur Dichte gehoert (<c>soll</c> = Dichte / 160), und mit <c>--ez dichte_gegenprobe true</c> verhaelt sich
/// die App wie vor 434 (<see cref="Dichte.AusFuerGegenprobe"/>), damit die Probe zeigt, dass sie den Fehler sieht.
/// </summary>
internal static class KonfigProbe
{
    public const string Schalter = "konfig_probe";
    public const string Gegenprobe = "dichte_gegenprobe";

    private static int _angelegt;

    /// <summary>Die Probe laeuft in diesem Start (die Update-Probe sucht dann nicht).</summary>
    public static bool An { get; private set; }

    /// <summary>Die Ansicht des Programms (Lernplattform: <c>MainView.Zuletzt</c>, Werkzeug: die Hauptansicht aus <c>NachStart</c>).</summary>
    public static Func<Control?>? Ansicht { get; set; }

    private static void Log(string text) => global::Android.Util.Log.Info("KONFIGPROBE", "KONFIGPROBE: " + text);

    public static void OnCreate(global::Android.App.Activity activity, global::Android.Content.Intent? intent)
    {
        _angelegt++;
        An = An || intent?.GetBooleanExtra(Schalter, false) == true;
        Dichte.AusFuerGegenprobe = Dichte.AusFuerGegenprobe || intent?.GetBooleanExtra(Gegenprobe, false) == true;
        if (An)
        {
            var c = activity.Resources?.Configuration;
            Log("ONCREATE " + _angelegt + " dichte " + (c?.DensityDpi ?? 0) + (Dichte.AusFuerGegenprobe ? " gegenprobe" : ""));
        }
    }

    public static void Geaendert(global::Android.Content.Res.Configuration c)
    {
        if (!An)
        {
            return;
        }
        var dichte = c.DensityDpi;
        Log($"GEAENDERT dichte {dichte} breite {c.ScreenWidthDp} hoehe {c.ScreenHeightDp} kleinste {c.SmallestScreenWidthDp}");
        // nach dem Wechsel: welche Groesse und Skalierung hat die Ansicht von Avalonia, und laeuft der Oberflaechen-Faden weiter?
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var ansicht = Ansicht?.Invoke();
                var oben = ansicht is null ? null : TopLevel.GetTopLevel(ansicht);
                var g = oben?.ClientSize ?? default;
                var soll = dichte / 160.0;
                Log(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"ANSICHT breite {g.Width:F0} hoehe {g.Height:F0} skalierung {(oben?.RenderScaling ?? 0):F2} soll {soll:F2} aufgebaut {(ansicht?.IsLoaded == true ? "ja" : "nein")}"));
            });
        });
    }
}
#endif
