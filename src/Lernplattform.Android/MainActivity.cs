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
        SystemmessungEinrichten();
        Lernplattform.Spiel.Vollbild.Geraet = an => RunOnUiThread(() =>
        {
            _messung?.Setzen(an);
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

    private Systemmessung? _messung;

    /// <summary>
    /// Weisung P7-16: Werte von Android fuer langsame Bilder im Spiel (Speicherbereinigung von Java,
    /// Waermezustand, ausgelassene Takte, Beruehrungen), nur gemessen, solange das Spiel offen ist.
    /// </summary>
    private void SystemmessungEinrichten()
    {
        var power = GetSystemService(PowerService) as global::Android.OS.PowerManager;
        double hertz = 0;
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && Display is { } anzeige)
        {
            hertz = anzeige.RefreshRate;
        }
        int Waerme()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29) && power is not null)
            {
                return (int)power.CurrentThermalStatus;
            }
            return -1;
        }
        _messung = new Systemmessung(Waerme, hertz);
        var messung = _messung;
        // global:: nicht in der Interpolation: der Doppelpunkt waere dort der Beginn einer Formatangabe
        var version = global::Android.OS.Build.VERSION.Release;
        var api = (int)global::Android.OS.Build.VERSION.SdkInt;
        Lernplattform.Spiel.Systemwerte.Kopf = () => string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"Android {version} (API {api}), Bildwiederholrate {messung.Hertz:0} Hz");
        Lernplattform.Spiel.Systemwerte.Seit = messung.Seit;
    }

    public override bool DispatchTouchEvent(global::Android.Views.MotionEvent? e)
    {
        if (_messung is { } messung && e?.ActionMasked == global::Android.Views.MotionEventActions.Down)
        {
            messung.Beruehrungen++;
        }
        return base.DispatchTouchEvent(e);
    }
}
