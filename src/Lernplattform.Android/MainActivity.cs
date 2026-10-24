using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace Lernplattform.Android;

[Activity(
#if PROBE
    Label = "Lernplattform Probe",
#else
    Label = "Fachinformatiker Lernplattform",
#endif
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
