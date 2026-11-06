using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using Lernplattform.Core.Update;

namespace Lernplattform.Android;

/// <summary>
/// A 0.39.0 (Weisung P11-39/P11-40, Plan P11-30): Update im Programm am Handy. Erlaubnis "Unbekannte Apps
/// installieren", WLAN, freier Speicher, APK auslesen (Paketname, Versionscode, Zertifikat) und die Uebergabe an
/// Android ueber eine PackageInstaller-Session. Ab Android 12 ohne Rueckfrage, wenn Android es erlaubt
/// (Selbst-Update, UPDATE_PACKAGES_WITHOUT_USER_ACTION). Das Ergebnis kommt als Broadcast an <see cref="Empfaenger"/>.
/// Nicht in der Probe-APK (dort bleibt der Weg ueber den Browser).
/// S 0.6.0 (Weisung P12-42 Punkt 2): dieselbe Datei baut auch das Stadtbau-Werkzeug (verlinkt in Stadtbau.Android).
/// Was je Programm verschieden ist, kommt als Wert: Aktion des Broadcasts, Name der Datei in der Session, wohin die
/// Rueckmeldung geht und wohin ein Fehler geschrieben wird. Die Lernplattform uebergibt genau ihre Werte von A 0.39.0.
/// </summary>
public sealed class HandyInstaller(Activity activity, string aktion, string sitzungsDatei, Action<int, string?> rueckmeldung,
    Action<Exception> fehler) : IHandyGeraet
{
    /// <summary>Aktion des Broadcasts mit dem Ergebnis der Session (nur an dieses Paket).</summary>
    public string Aktion => aktion;

    /// <summary>Rueckmeldung von Android zur Installation (auf dem UI-Faden).</summary>
    public Action<int, string?> Rueckmeldung => rueckmeldung;

    // PackageInstaller.EXTRA_STATUS / EXTRA_STATUS_MESSAGE, Intent.EXTRA_INTENT
    private const string ExtraStatus = "android.content.pm.extra.STATUS";
    private const string ExtraMeldung = "android.content.pm.extra.STATUS_MESSAGE";
    private const string ExtraIntent = "android.intent.extra.INTENT";
    // PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED (API 31)
    private const int OhneRueckfrage = 2;

    private TaskCompletionSource<bool>? _erlaubnis;

    /// <summary>Wird aus <see cref="MainActivity.OnResume"/> gerufen (Rueckkehr aus der Einstellung).</summary>
    public void Zurueck() => _erlaubnis?.TrySetResult(DarfInstallieren);

    public string LadeOrdner => Path.Combine(activity.CacheDir!.AbsolutePath, "update");

    public long FreierSpeicher
    {
        get
        {
            Directory.CreateDirectory(LadeOrdner);
            return new StatFs(LadeOrdner).AvailableBytes;
        }
    }

    public bool? ImWlan
    {
        get
        {
            if (activity.GetSystemService(Context.ConnectivityService) is not ConnectivityManager netz
                || netz.ActiveNetwork is not { } aktiv || netz.GetNetworkCapabilities(aktiv) is not { } art)
            {
                return null;
            }
            return art.HasTransport(TransportType.Wifi) || art.HasTransport(TransportType.Ethernet);
        }
    }

    public bool DarfInstallieren =>
        !OperatingSystem.IsAndroidVersionAtLeast(26) || activity.PackageManager!.CanRequestPackageInstalls();

    public Task<bool> ErlaubnisHolenAsync()
    {
        if (DarfInstallieren || !OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return Task.FromResult(true);
        }
        _erlaubnis = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oeffnen = new Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources,
            global::Android.Net.Uri.Parse("package:" + activity.PackageName));
        activity.StartActivity(oeffnen);
        return _erlaubnis.Task;
    }

    public string EigenesPaket => activity.PackageName!;

    public long EigenerVersionscode
    {
        get
        {
            var info = activity.PackageManager!.GetPackageInfo(activity.PackageName!, 0)!;
            return OperatingSystem.IsAndroidVersionAtLeast(28) ? info.LongVersionCode : Alt(info);
        }
    }

#pragma warning disable CA1422, CS0618 // VersionCode und GET_SIGNATURES nur fuer Android vor 9
    private static long Alt(PackageInfo info) => info.VersionCode;
#pragma warning restore CA1422, CS0618

    public ApkAngaben? ApkLesen(string pfad)
    {
        try
        {
            var pm = activity.PackageManager!;
            if (!OperatingSystem.IsAndroidVersionAtLeast(28))
            {
                return null;   // A 0.39.0 nur ab Android 9 (S24 und Tab A9+ haben 14/15); aelter: Download-Seite
            }
#pragma warning disable CA1422, CS0618 // die Variante mit PackageInfoFlags.Of gibt es erst ab API 33
            var info = OperatingSystem.IsAndroidVersionAtLeast(33)
                ? pm.GetPackageArchiveInfo(pfad, PackageManager.PackageInfoFlags.Of((long)PackageInfoFlags.SigningCertificates))
                : pm.GetPackageArchiveInfo(pfad, PackageInfoFlags.SigningCertificates);
#pragma warning restore CA1422, CS0618
            if (info?.PackageName is not { } paket || info.SigningInfo is not { } signatur)
            {
                return null;
            }
            var zertifikate = (signatur.HasMultipleSigners ? signatur.GetApkContentsSigners() : signatur.GetSigningCertificateHistory())
                ?? [];
            // ohne Rotation ist die Geschichte genau ein Zertifikat; mit Rotation zaehlt nur das aktuelle (letzte)
            var liste = signatur.HasMultipleSigners
                ? zertifikate.Select(Sha256).ToList()
                : zertifikate.Length > 0 ? [Sha256(zertifikate[^1])] : [];
            return new ApkAngaben(paket, info.LongVersionCode, liste);
        }
        catch (Exception e)
        {
            fehler(e);
            return null;
        }
    }

    private static string Sha256(Signature zertifikat) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(zertifikat.ToByteArray()!));

    public void Installieren(string pfad)
    {
        var installer = activity.PackageManager!.PackageInstaller;
        var angaben = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
        angaben.SetAppPackageName(activity.PackageName);
        var laenge = new FileInfo(pfad).Length;
        angaben.SetSize(laenge);
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            angaben.SetRequireUserAction(OhneRueckfrage);
        }
        var nummer = installer.CreateSession(angaben);
        using var sitzung = installer.OpenSession(nummer);
        try
        {
            using (var ziel = sitzung.OpenWrite(sitzungsDatei, 0, laenge))
            using (var quelle = File.OpenRead(pfad))
            {
                quelle.CopyTo(ziel);
                sitzung.Fsync(ziel);
            }
            var antwort = new Intent(aktion).SetPackage(activity.PackageName);
            var art = PendingIntentFlags.UpdateCurrent | (OperatingSystem.IsAndroidVersionAtLeast(31) ? PendingIntentFlags.Mutable : 0);
            var rueckweg = PendingIntent.GetBroadcast(activity, nummer, antwort, art)!;
            sitzung.Commit(rueckweg.IntentSender!);
        }
        catch
        {
            sitzung.Abandon();
            throw;
        }
    }

    /// <summary>Empfaengt das Ergebnis der Session (dynamisch registriert, nicht exportiert).</summary>
    public sealed class Empfaenger(Activity activity, HandyInstaller installer) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is null)
            {
                return;
            }
            var status = intent.GetIntExtra(ExtraStatus, Lernplattform.Core.Update.HandyPruefung.StatusFehler);
            var meldung = intent.GetStringExtra(ExtraMeldung);
            if (status == Lernplattform.Core.Update.HandyPruefung.StatusRueckfrage)
            {
                // Android fragt selbst nach ("Aktualisieren?"): sein Fenster zeigen
#pragma warning disable CA1422, CS0618 // GetParcelableExtra(string) vor API 33
                var bestaetigen = OperatingSystem.IsAndroidVersionAtLeast(33)
                    ? (Intent?)intent.GetParcelableExtra(ExtraIntent, Java.Lang.Class.FromType(typeof(Intent)))
                    : (Intent?)intent.GetParcelableExtra(ExtraIntent);
#pragma warning restore CA1422, CS0618
                if (bestaetigen is not null)
                {
                    bestaetigen.AddFlags(ActivityFlags.NewTask);
                    activity.StartActivity(bestaetigen);
                }
            }
            Melden?.Invoke(status, meldung);
            activity.RunOnUiThread(() => installer.Rueckmeldung(status, meldung));
        }

        /// <summary>Nur fuer die Emulator-Probe (UPDATEPROBE): jede Rueckmeldung mitlesen.</summary>
        public static Action<int, string?>? Melden { get; set; }
    }
}
