using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Stadtbau.Android;

/// <summary>
/// Phase 13 Welle 2: Start des Werkzeugs am Tablet. Projekte liegen im privaten Ordner der App (Werkzeugordner.Standard);
/// auf einem Handy zeigt die Ansicht nur den Hinweis "Das Werkzeug ist für Tablet und PC gedacht".
/// </summary>
[Activity(
    Label = "Fachinformatiker Stadtbau",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Patch S 0.5.1 (Weisung P12-35): "Werkzeug beenden" schliesst die Aufgabe (auch aus der Liste der letzten Apps)
        // und beendet danach den Prozess, wie "Programm beenden" der Lernplattform
        StadtbauApp.GeraetBeenden = () => RunOnUiThread(() =>
        {
            FinishAndRemoveTask();
            new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!).PostDelayed(
                () => global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid()), 500);
        });
        // Patch S 0.5.1 (Weisung P12-37 Punkt 2): Vollbild = Systemleisten aus (beim Wischen vom Rand kurz sichtbar);
        // den Wunsch bekommt auch Avalonia (WerkzeugVollbild.Leisten), sonst zeigt es sie beim naechsten Sichtbarwerden
        // wieder (Lehre aus Sammelliste 218 der Lernplattform)
        global::Stadtbau.Oberflaeche.WerkzeugVollbild.Geraet = an => RunOnUiThread(() =>
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30) && Window?.InsetsController is { } steuerung)
            {
                if (an)
                {
                    // 2 = BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
                    steuerung.SystemBarsBehavior = 2;
                    steuerung.Hide(global::Android.Views.WindowInsets.Type.SystemBars());
                }
                else
                {
                    steuerung.Show(global::Android.Views.WindowInsets.Type.SystemBars());
                }
            }
        });
        global::Stadtbau.Oberflaeche.WerkzeugVollbild.Anwenden();
    }
}
