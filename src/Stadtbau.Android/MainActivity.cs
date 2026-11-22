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
    // S 0.61.1 (Weisung P12-350, Sammelliste 435): dieselben Konfigurationswechsel wie die Lernplattform seit A 1.3.1 (426) selbst
    // behandeln (Bildschirm-Aufbau, kleinste Breite, Tastatur, Dichte). Sonst baut Android die Activity neu, etwa beim Wechsel in
    // den Mehrfenster-Modus, beim Anstecken einer Tastatur oder bei anderer Anzeigegroesse.
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
#if UPDATEPROBE
        // S 0.61.1 (Sammelliste 435): die Konfig-Probe misst die Hauptansicht des Werkzeugs (kommt beim Anlegen der Activity)
        var vorher = StadtbauApp.NachStart;
        StadtbauApp.NachStart = (ansicht, fenster) =>
        {
            Lernplattform.Android.KonfigProbe.Ansicht = () => ansicht;
            vorher?.Invoke(ansicht, fenster);
        };
#endif
        base.OnCreate(savedInstanceState);
        _dichte = Resources?.Configuration?.DensityDpi ?? 0;
#if UPDATEPROBE
        Lernplattform.Android.KonfigProbe.OnCreate(this, Intent);
#endif
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
        UpdateEinrichten();
    }

    private static readonly Lazy<System.Net.Http.HttpClient> Netz = new(() => new System.Net.Http.HttpClient());
    private Lernplattform.Android.HandyInstaller? _installer;
    private Lernplattform.Android.HandyInstaller.Empfaenger? _empfaenger;

    /// <summary>Aktion des Broadcasts mit dem Ergebnis der Installation (eigene des Werkzeugs, nur an dieses Paket).</summary>
    public const string UpdateAktion = "io.github.papst2142cmd.stadtbau.UPDATE_ERGEBNIS";

    /// <summary>
    /// S 0.6.0 (Weisung P12-42 Punkt 2): das Werkzeug aktualisiert sich am Tablet selbst, nur aus Releases der Linie vS
    /// (HandyQuelle mit Linie Stadtbau), gleiche Pruefungen wie die Lernplattform seit A 0.39.0 (TabletUpdate).
    /// </summary>
    private void UpdateEinrichten()
    {
        var ordner = StadtbauApp.Ordner;
        _installer = new Lernplattform.Android.HandyInstaller(this, UpdateAktion, "stadtbau.apk", global::Stadtbau.Oberflaeche.TabletUpdate.Rueckmeldung,
            e => ordner.Fehler("Update: " + e.GetType().Name + ": " + e.Message));
        var installer = _installer;
        var url = Lernplattform.Core.Update.GithubSuche.ListeUrl;
        var fingerabdruck = Lernplattform.Core.Update.HandyPruefung.Fingerabdruck;
#if UPDATEPROBE
        (url, fingerabdruck) = UpdateProbe.Ziel();
#endif
        global::Stadtbau.Oberflaeche.TabletUpdate.Geraet = installer;
        global::Stadtbau.Oberflaeche.TabletUpdate.Quelle = () => global::Stadtbau.Core.TabletAktualisierung.Quelle(Netz.Value, installer,
            ordner.Wurzel, url, fingerabdruck);
#if !UPDATEPROBE
        // die Probe sucht selbst (ohne Fenster), sonst kaeme das Fenster "Neue Version" dazwischen
        global::Stadtbau.Oberflaeche.Aktualisierung.Pruefen = global::Stadtbau.Oberflaeche.TabletUpdate.SuchenAsync;
#endif
        _empfaenger = new Lernplattform.Android.HandyInstaller.Empfaenger(this, installer);
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
        if (!Lernplattform.Android.KonfigProbe.An)
        {
            UpdateProbe.Starten(installer);
        }
#endif
    }

    private int _dichte;

    /// <summary>
    /// S 0.61.1 (Weisung P12-350, Sammelliste 435): nach einer reinen Dichte-Aenderung die neue Skalierung sofort an Avalonia
    /// geben, nicht erst beim naechsten Groessenwechsel (gleiche Behandlung wie die Lernplattform, <c>Dichte.cs</c>).
    /// </summary>
    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        _dichte = Lernplattform.Android.Dichte.Nachziehen(this, newConfig, _dichte);
#if UPDATEPROBE
        Lernplattform.Android.KonfigProbe.Geaendert(newConfig);
#endif
    }

    protected override void OnResume()
    {
        base.OnResume();
        // Rueckkehr aus der Einstellung "Unbekannte Apps installieren"
        _installer?.Zurueck();
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
}
