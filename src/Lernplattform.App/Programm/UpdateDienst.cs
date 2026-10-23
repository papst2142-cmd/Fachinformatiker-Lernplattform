using System.Net.Http;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;
using Lernplattform.Views;
using Aktuell = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Programm;

/// <summary>
/// Phase 5 BE10: Update im Programm wie UpdateController (app_gui 8675) und check_updates/auto_check am
/// Handy (main.py 6077). PC: nach 1 s die Pruefung des letzten Updates (Fenster "Update nicht
/// abgeschlossen"), nach 3 s die Suche (wenn "Beim Start automatisch ..." an). Handy: Suche 3 s nach
/// dem Start und beim Zurueckholen hoechstens alle 30 Minuten, nach einem Netzfehler ein zweiter Versuch
/// nach 30 s; "Später" gilt dort bis zum naechsten Start, nur die Suche von Hand zeigt dieselbe Version
/// wieder. Automatische Suchen melden sich nur, wenn es ein Update gibt. Im Start ohne Bedienung nie ins Netz.
/// </summary>
public static class UpdateDienst
{
    public static readonly bool Pc = !OperatingSystem.IsAndroid();
    public static readonly TimeSpan PruefungNach = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan SucheNach = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan Abstand = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Nochmal = TimeSpan.FromSeconds(30);

    private static readonly Lazy<HttpClient> Netz = new(() => new HttpClient());
    private static bool _busy;
    private static DateTime? _letzteAuto;
    private static string? _abgelehnt;

    /// <summary>Gerade offenes Update-Fenster (hoechstens eins).</summary>
    public static UpdateFenster? Fenster { get; internal set; }

    /// <summary>update_exit: das Programm schliesst fuer das Update (ohne letzten Abgleich, wie final_sync=False).</summary>
    public static bool FuerUpdateBeendet { get; private set; }

    private static bool Aus => MainView.AutoModus;

    private static int _gestartet;

    /// <summary>Beim Start (MainView, erstes Bild); am Handy kann die Ansicht neu entstehen, gestartet wird einmal.</summary>
    public static void Starten()
    {
        if (Aus || Interlocked.Exchange(ref _gestartet, 1) == 1)
        {
            return;
        }
        if (Pc)
        {
            DispatcherTimer.RunOnce(LetztesUpdatePruefen, PruefungNach);
            DispatcherTimer.RunOnce(() => Automatisch(), SucheNach);
        }
        else
        {
            Automatisch(SucheNach);
        }
    }

    /// <summary>Handy: App kommt aus dem Hintergrund zurueck (nur aus dem Android-Lebenszyklus gerufen).</summary>
    public static void Zurueck()
    {
        if (!Aus)
        {
            Automatisch();
        }
    }

    /// <summary>check_finished_update: lief vor diesem Start ein Update, das nicht angekommen ist?</summary>
    private static void LetztesUpdatePruefen()
    {
        try
        {
            if (UpdateMerker.Abschliessen(Zustand.Einstellungen, Aktuell.Anzeige, Fehlerprotokoll.System()) is { Ok: false } fehler)
            {
                _ = Dialog.Zeigen(UpdateTexte.FehlschlagTitel, fehler.Text, Dialog.Ok);
            }
            Zustand.EinstellungenNeuLesen();
        }
        catch (Exception e)
        {
            Fehlerprotokoll.Schreiben(e);
        }
    }

    /// <summary>auto_check: nur bei eingeschaltetem Schalter; am Handy hoechstens alle 30 Minuten.</summary>
    private static void Automatisch(TimeSpan? verzoegerung = null)
    {
        if (!(Zustand.Einstellungen.Laden()[Einstellungen.AutoCheckSchluessel]?.GetValueKind() is System.Text.Json.JsonValueKind.True))
        {
            return;
        }
        if (!Pc)
        {
            var jetzt = DateTime.UtcNow;
            if (_letzteAuto is { } letzte && jetzt - letzte < Abstand)
            {
                return;
            }
            _letzteAuto = jetzt;
        }
        _ = SuchenAsync(false, verzoegerung, nochmal: !Pc);
    }

    /// <summary>"Nach Updates suchen" (manual): Ergebnis fuer die Zeile im Bereich "Updates".</summary>
    public static Task<(UpdateAngebot? Angebot, string? Fehler)?> VonHandAsync() => SuchenAsync(true, null, false);

    private static async Task<(UpdateAngebot? Angebot, string? Fehler)?> SuchenAsync(bool vonHand, TimeSpan? verzoegerung, bool nochmal)
    {
        if (verzoegerung is { } warten)
        {
            await Task.Delay(warten);
        }
        if (_busy)
        {
            return null;
        }
        _busy = true;
        UpdateAngebot? angebot = null;
        string? fehler = null;
        try
        {
            // PC: Velopack (Desktop setzt die Quelle), Handy: GitHub mit APK
            angebot = await Task.Run(() => Aktuell.Quelle is { } quelle ? UpdateAblauf.SuchenAsync(quelle()) : HandySuche());
        }
        catch (UpdateFehler e)
        {
            fehler = e.Message;
        }
        catch (Exception e)
        {
            fehler = UpdateAblauf.Unerwartet(e);
        }
        finally
        {
            _busy = false;
        }
        if (!vonHand && !Pc)
        {
            if (fehler is not null)
            {
                // erfolglos: beim naechsten Zurueckholen erneut, nach dem Start einmal nach 30 s
                _letzteAuto = null;
                if (nochmal)
                {
                    _ = SuchenAsync(false, Nochmal, false);
                }
            }
            else
            {
                _letzteAuto = DateTime.UtcNow;
            }
        }
        if (angebot is not null && Fenster is null && (vonHand || angebot.Version != _abgelehnt))
        {
            Fenster = new UpdateFenster(angebot, Aktuell.Quelle is not null && angebot.DateiName is not null && Installierbar());
            _ = Fenster.ZeigenAsync();
        }
        return (angebot, fehler);
    }

    private static Task<UpdateAngebot?> HandySuche() => UpdateAblauf.SuchenAsync(new GithubQuelle(Netz.Value));

    private static bool Installierbar()
    {
        try
        {
            return Aktuell.Quelle?.Invoke().Installiert ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Handy: "Später" bzw. "Herunterladen" schliesst; dieselbe Version kommt erst nach einem Neustart wieder.</summary>
    internal static void Abgelehnt(string version) => _abgelehnt = version;

    /// <summary>
    /// "Jetzt aktualisieren": laden, pruefen, installieren (im Hintergrund). Danach schliesst das
    /// Programm nach 1,5 s (der Updater wartet darauf und startet die neue Version).
    /// </summary>
    internal static async Task<string> AktualisierenAsync(UpdateAngebot angebot, Action<long, long> fortschritt, Action installiere)
    {
        if (Vorgaenge.Belegt)
        {
            // D8: Abgleich, Uebernahme oder Sicherung laeuft; die Oberflaeche wartet nie
            throw new UpdateFehler(AbgleichTexte.Belegt);
        }
        var quelle = Aktuell.Quelle!();
        await Task.Run(() => UpdateAblauf.LadenAsync(quelle, angebot, fortschritt));
        installiere();
        return await Task.Run(() => UpdateAblauf.Installieren(quelle, angebot, Zustand.Einstellungen, sperreHalten: true));
    }

    /// <summary>Nach dem Start des Updaters: Programm schliessen (ohne letzten Abgleich).</summary>
    internal static void Schliessen()
    {
        FuerUpdateBeendet = true;
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>Handy: Suche ueber GitHub (APK); laden und installieren macht der Browser bzw. Android.</summary>
    private sealed class GithubQuelle(HttpClient http) : IUpdateQuelle
    {
        public bool Installiert => false;

        public Task<UpdateAngebot?> SuchenAsync(CancellationToken abbruch) => GithubSuche.SuchenAsync(http, Aktuell.Anzeige, "android", abbruch: abbruch);

        public Task<string> LadenAsync(UpdateAngebot angebot, Action<long, long> fortschritt, CancellationToken abbruch) =>
            throw new UpdateFehler(UpdateTexte.KeinInstaller);

        public void Installieren(UpdateAngebot angebot) => throw new UpdateFehler(UpdateTexte.NurInstalliert);
    }
}
