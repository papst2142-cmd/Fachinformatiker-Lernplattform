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
}
