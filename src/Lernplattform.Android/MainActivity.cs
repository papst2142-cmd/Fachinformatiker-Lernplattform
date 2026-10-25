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
    /// <summary>
    /// Phase 7 W4 (Weisung P7-9 Punkt 2, Wunsch Nico): im Spiel Querformat und Systemleisten aus
    /// (Wischen vom Rand zeigt sie kurz), Bild bis in die Kamera-Aussparung; beim Verlassen wieder die Einstellung des Geraets
    /// (Hochformat, wenn das Handy hochgehalten wird oder das automatische Drehen aus ist).
    /// </summary>
    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Lernplattform.Spiel.Vollbild.Geraet = an => RunOnUiThread(() =>
        {
            RequestedOrientation = an ? ScreenOrientation.SensorLandscape : ScreenOrientation.Unspecified;
            if (OperatingSystem.IsAndroidVersionAtLeast(28) && Window is { } fenster && fenster.Attributes is { } lage)
            {
                // P7-10 Nachtrag 1: im Spiel bis an den Rand zeichnen, auch neben der Kamera-Aussparung
                // (sonst bleibt im Querformat ein schwarzer Streifen); die Knoepfe haelt die Spielseite
                // im sicheren Bereich (SafeAreaPadding)
                lage.LayoutInDisplayCutoutMode = an
                    ? global::Android.Views.LayoutInDisplayCutoutMode.ShortEdges
                    : global::Android.Views.LayoutInDisplayCutoutMode.Default;
                fenster.Attributes = lage;
            }
            if (OperatingSystem.IsAndroidVersionAtLeast(30) && Window?.InsetsController is { } leisten)
            {
                if (an)
                {
                    // 2 = BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE (Leisten erscheinen beim Wischen vom Rand kurz)
                    leisten.SystemBarsBehavior = 2;
                    leisten.Hide(global::Android.Views.WindowInsets.Type.SystemBars());
                }
                else
                {
                    leisten.Show(global::Android.Views.WindowInsets.Type.SystemBars());
                }
            }
        });
    }
}
