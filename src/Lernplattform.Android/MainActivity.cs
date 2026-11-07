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
#if UPDATEPROBE
        // Patch A 0.41.1 (Sammelliste 218): Gegenprobe der Emulator-Probe, Start mit "--ez vollbild_gegenprobe true"
        // verhaelt sich wie vor dem Fix (Avalonia erfaehrt nichts vom Wunsch "Systemleisten aus"); vor base.OnCreate,
        // weil MainView dort schon in die Ansicht kommt
        if (Intent?.GetBooleanExtra(UpdateProbe.Gegenprobe, false) == true)
        {
            Lernplattform.Spiel.Vollbild.LeistenUeberAvalonia = false;
        }
#endif
        base.OnCreate(savedInstanceState);
        SystemmessungEinrichten();
#if PROBE
        // Weisung P8-16 a: Ton-Probe mit SoundFlow (nur Probe-APK)
        Lernplattform.Spiel.TonProbe.Geraet = _tonprobe;
        // Weisung P8-52: Leistungsprobe (nur Probe-APK)
        Lernplattform.Spiel.Leistungsprobe.An = true;
        Lernplattform.Spiel.Leistungsprobe.BildschirmAn = an => RunOnUiThread(() =>
        {
            if (an)
            {
                Window?.AddFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn);
            }
            else
            {
                Window?.ClearFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn);
            }
        });
#endif
        // Patch A 0.37.0 (Sammelliste 206): "Programm beenden" schliesst die Aufgabe (auch aus der Liste der letzten Apps)
        // und beendet danach den Prozess, damit der naechste Start ein Kaltstart ist wie am PC
        Lernplattform.Programm.Programmende.Geraet = () => RunOnUiThread(() =>
        {
            FinishAndRemoveTask();
            new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!).PostDelayed(
                () => global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid()), 500);
        });
        // Phase 8 W8 (Nico 3a): "leisten" = Systemleisten aus, im Spiel immer, sonst mit dem Schalter "Vollbild"
        Lernplattform.Spiel.Vollbild.Geraet = (an, leisten) => RunOnUiThread(() =>
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
            if (OperatingSystem.IsAndroidVersionAtLeast(30) && Window?.InsetsController is { } steuerung)
            {
                if (leisten)
                {
                    // 2 = BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE (Leisten erscheinen beim Wischen vom Rand kurz)
                    steuerung.SystemBarsBehavior = 2;
                    steuerung.Hide(global::Android.Views.WindowInsets.Type.SystemBars());
                }
                else
                {
                    steuerung.Show(global::Android.Views.WindowInsets.Type.SystemBars());
                }
            }
        });
        // Phase 8 W8: Schalter "Vollbild" schon beim Start an
        Lernplattform.Spiel.Vollbild.GeraetAnwenden();
#if !PROBE
        UpdateEinrichten();
#endif
    }

#if !PROBE
    private static readonly Lazy<System.Net.Http.HttpClient> Netz = new(() => new System.Net.Http.HttpClient());
    private HandyInstaller? _installer;
    private HandyInstaller.Empfaenger? _empfaenger;

    /// <summary>Aktion des Broadcasts mit dem Ergebnis der Installation (seit A 0.39.0 unveraendert).</summary>
    public const string UpdateAktion = "io.github.papst2142cmd.lernplattform.UPDATE_ERGEBNIS";

    /// <summary>
    /// A 0.39.0 (Weisung P11-40): Update im Programm wie am PC. Die Probe-APK bleibt beim Weg ueber den Browser
    /// (eigene Kennung, die Pruefung des Paketnamens liesse ihr Update ohnehin nie zu).
    /// </summary>
    private void UpdateEinrichten()
    {
        _installer = new HandyInstaller(this, UpdateAktion, "lernplattform.apk", Lernplattform.Programm.UpdateDienst.HandyRueckmeldung,
            e => Lernplattform.Core.Diagnose.Fehlerprotokoll.Schreiben(e));
        var installer = _installer;
        var url = Lernplattform.Core.Update.GithubSuche.NeuesteUrl;
        var fingerabdruck = Lernplattform.Core.Update.HandyPruefung.Fingerabdruck;
#if UPDATEPROBE
        (url, fingerabdruck) = UpdateProbe.Ziel();
#endif
        Lernplattform.Aktualisierung.Aktualisierung.Handy = installer;
        Lernplattform.Aktualisierung.Aktualisierung.Quelle = () => new Lernplattform.Core.Update.HandyQuelle(Netz.Value, installer,
            Lernplattform.Aktualisierung.Aktualisierung.Anzeige, url, fingerabdruck);
        _empfaenger = new HandyInstaller.Empfaenger(this, installer);
        var filter = new global::Android.Content.IntentFilter(installer.Aktion);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            RegisterReceiver(_empfaenger, filter, global::Android.Content.ReceiverFlags.NotExported);
        }
        else
        {
            RegisterReceiver(_empfaenger, filter);
        }
#if UPDATEPROBE
        UpdateProbe.Starten(installer);
#endif
    }

    protected override void OnDestroy()
    {
        if (_empfaenger is not null)
        {
            UnregisterReceiver(_empfaenger);
            _empfaenger = null;
        }
        base.OnDestroy();
    }
#endif

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
        var art = e?.ActionMasked;
        if (_messung is { } messung && art == global::Android.Views.MotionEventActions.Down)
        {
            messung.Beruehrungen++;
        }
        if (art is global::Android.Views.MotionEventActions.Down or global::Android.Views.MotionEventActions.Up)
        {
            // Weisung P7-17: Zeitpunkt fuer die Eingabe vor einem langsamen Bild
            Lernplattform.Spiel.Systemwerte.Finger = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        return base.DispatchTouchEvent(e);
    }

    /// <summary>
    /// Weisung P7-17: vor dem Wechsel in den Hintergrund alle Spielschritte fertig schreiben.
    /// Sammelliste 51 (_lifecycle main.py 5970): offene Eingaben in Klausur und Abschlussprojekt
    /// speichern, auch wenn Avalonia den Wechsel nicht als Deactivated meldet.
    /// </summary>
#if PROBE
    private readonly Tonprobe _tonprobe = new();
#endif

    protected override void OnPause()
    {
        Lernplattform.Core.Spiel.Spielschlange.AlleLeeren();
        Lernplattform.Programm.Zustand.EingabenSpeichern();
        // Phase 8 W8: im Hintergrund still
        Lernplattform.Spiel.TonDienst.Hintergrund(true);
#if PROBE
        _tonprobe.Aufraeumen();
#endif
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        Lernplattform.Spiel.TonDienst.Hintergrund(false);
#if !PROBE
        // A 0.39.0: Rueckkehr aus der Einstellung "Unbekannte Apps installieren"
        _installer?.Zurueck();
#endif
#if UPDATEPROBE
        UpdateProbe.LeistenMelden(this);
#endif
    }

    protected override void OnStop()
    {
        Lernplattform.Core.Spiel.Spielschlange.AlleLeeren();
        base.OnStop();
    }
}
