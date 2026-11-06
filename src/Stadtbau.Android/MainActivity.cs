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
    }
}
