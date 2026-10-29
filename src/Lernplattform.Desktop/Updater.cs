using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;
using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;
using Aktuell = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Desktop;

/// <summary>
/// Phase 5 BE9: Update am PC ueber Velopack. Quelle sind die GitHub-Releases des Repos (oeffentlich,
/// ohne Token, nie Vorab-Releases); LERNPLATTFORM_UPDATE_QUELLE ersetzt sie fuer Tests durch einen Ordner
/// oder einen lokalen Testserver (http://127.0.0.1:...). Laeuft das Programm nicht als installierte
/// Anwendung, sucht es wie Python trotzdem und bietet nur die Download-Seite an.
/// Ohne Oberflaeche (LERNPLATTFORM_UPDATE_AUTO) prueft die CI denselben Ablauf (Paketskript).
/// </summary>
internal static class Updater
{
    public const string Repo = "https://github.com/" + GithubSuche.Repository;

    public static void Verbinden()
    {
        Aktuell.Quelle = () => new VelopackQuelle(Quelle());
        Aktuell.Zustand = () =>
        {
            try
            {
                var mgr = new UpdateManager(new SimpleFileSource(new DirectoryInfo(Path.GetTempPath())));
                return mgr.IsInstalled
                    ? "installiert, Paketversion " + mgr.CurrentVersion
                    : "nicht installiert (Ordner-Programm), Paketversion " + Aktuell.PaketVersion;
            }
            catch (Exception fehler)
            {
                return "Updater-Fehler " + fehler.GetType().Name;
            }
        };
    }

    /// <summary>GitHub, oder fuer Tests LERNPLATTFORM_UPDATE_QUELLE (Ordner oder http-Adresse).</summary>
    internal static IUpdateSource Quelle(string? angabe = null)
    {
        angabe ??= Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_QUELLE");
        if (angabe is { Length: > 0 })
        {
            return angabe.StartsWith("http://", StringComparison.Ordinal) || angabe.StartsWith("https://", StringComparison.Ordinal)
                ? new SimpleWebSource(angabe)
                : new SimpleFileSource(new DirectoryInfo(angabe));
        }
        return new GithubSource(Repo, null, false);
    }

    /// <summary>
    /// Ablauf ohne Oberflaeche fuer die CI (LERNPLATTFORM_UPDATE_AUTO):
    /// "suchen" sucht in LERNPLATTFORM_UPDATE_ORDNER, laedt, prueft und installiert wie der Knopf
    /// "Jetzt aktualisieren" (Kopie, Merker, Updater) und beendet; "version" schreibt nur Version und
    /// Datenordner-Probe (zum Warten, laesst den Merker stehen); "bericht" schreibt dazu das Ergebnis der
    /// Pruefung beim Start (wie das erste Bild am PC) und die Zahl der Kopien vor dem Update.
    /// Ausgabe in LERNPLATTFORM_UPDATE_BERICHT.
    /// </summary>
    public static int Auto(string modus)
    {
        var bericht = Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_BERICHT") is { Length: > 0 } b ? b : "update_bericht.txt";
        try
        {
            var ordner = Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_ORDNER") ?? Path.GetTempPath();
            var quelle = new VelopackQuelle(new SimpleFileSource(new DirectoryInfo(ordner)), neustart: false);
            var version = quelle.Manager.IsInstalled ? quelle.Manager.CurrentVersion?.ToString() ?? "?" : "nicht installiert";
            var probe = Ablage.UpdateProbe(version);
            File.AppendAllText(bericht, $"{modus}: Version {version}; Probe: {probe}{Environment.NewLine}");
            if (!quelle.Manager.IsInstalled)
            {
                return 3;
            }
            if (modus is not ("suchen" or "bericht"))
            {
                return 0;
            }
            // wie der Programmstart: Lernstand anlegen (die Kopie vor dem Update braucht ihn), Merker pruefen
            _ = new Lernstand();
            var einstellungen = new Einstellungen();
            if (modus == "bericht")
            {
                var abschluss = UpdateMerker.Abschliessen(einstellungen, Aktuell.Anzeige, Fehlerprotokoll.System());
                var kopien = Directory.Exists(Path.Combine(Ablage.Datenordner(), Uebernahme.SicherungsOrdner))
                    ? Directory.GetFiles(Path.Combine(Ablage.Datenordner(), Uebernahme.SicherungsOrdner), UpdateAblauf.KopiePraefix + "*.db").Length
                    : 0;
                File.AppendAllText(bericht, $"{modus}: Abschluss {(abschluss is null ? "keiner" : abschluss.Ok ? "ok " + abschluss.Text : "FEHLER")}; Kopien vor dem Update: {kopien}{Environment.NewLine}");
                return 0;
            }
            var neu = Task.Run(() => UpdateAblauf.SuchenAsync(quelle)).GetAwaiter().GetResult();
            if (neu is null)
            {
                File.AppendAllText(bericht, "suchen: kein neueres Paket" + Environment.NewLine);
                return 4;
            }
            // Weisung P8-53: Notizen des angebotenen Pakets so, wie das Update-Fenster sie zeigt (Pruefsumme, die Probe vergleicht)
            var notizen = UpdateNotizen.Lesbar(neu.Notizen);
            File.AppendAllText(bericht, $"suchen: Notizen {notizen.Length} Zeichen; sha256 {Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(notizen)))}{Environment.NewLine}");
            Task.Run(() => UpdateAblauf.LadenAsync(quelle, neu)).GetAwaiter().GetResult();
            File.AppendAllText(bericht, $"suchen: spiele {neu.Version} ein{Environment.NewLine}");
            UpdateAblauf.Installieren(quelle, neu, einstellungen);
            return 0;
        }
        catch (Exception fehler)
        {
            File.AppendAllText(bericht, $"{modus}: FEHLER {fehler.GetType().Name}: {fehler.Message}{Environment.NewLine}");
            return 5;
        }
    }
}

/// <summary>Velopack als <see cref="IUpdateQuelle"/>; Fehler werden zu deutschen Texten wie in fisi_update.py.</summary>
internal sealed class VelopackQuelle(IUpdateSource quelle, bool neustart = true) : IUpdateQuelle
{
    /// <summary>Hoechstens so lange ohne Fortschritt (ein Prozent des Pakets), dann DownloadZeit.</summary>
    public static readonly TimeSpan Stillstand = TimeSpan.FromSeconds(60);

    public UpdateManager Manager { get; } = new(quelle);

    public bool Installiert => Manager.IsInstalled;

    public async Task<UpdateAngebot?> SuchenAsync(CancellationToken abbruch)
    {
        if (!Manager.IsInstalled)
        {
            // Ordner-Programm (aus dem Quellcode oder entpackt): nur Hinweis und Download-Seite (asset_url fehlt)
            using var http = new HttpClient();
            return await GithubSuche.SuchenAsync(http, Aktuell.Anzeige, "pc", abbruch: abbruch).ConfigureAwait(false);
        }
        UpdateInfo? info;
        try
        {
            info = await Manager.CheckForUpdatesAsync().WaitAsync(GithubSuche.Wartezeit, abbruch).ConfigureAwait(false);
        }
        catch (HttpRequestException e) when (e.StatusCode is { } code)
        {
            throw new UpdateFehler(UpdateTexte.Abgelehnt.Replace("%d", ((int)code).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TimeoutException or OperationCanceledException)
        {
            throw new UpdateFehler(UpdateTexte.KeineVerbindung);
        }
        catch (JsonException)
        {
            throw new UpdateFehler(UpdateTexte.Unerwartet);
        }
        if (info is null)
        {
            return null;
        }
        var ziel = info.TargetFullRelease;
        return new UpdateAngebot(UpdateVersion.Lesen(ziel.Version.ToString())?.Anzeige ?? ziel.Version.ToString(), ziel.NotesMarkdown ?? "",
            GithubSuche.ReleaseSeite, ziel.FileName, null, ziel.Size, string.IsNullOrEmpty(ziel.SHA256) ? null : "sha256:" + ziel.SHA256, info);
    }

    public async Task<string> LadenAsync(UpdateAngebot angebot, Action<long, long> fortschritt, CancellationToken abbruch)
    {
        var info = (UpdateInfo)angebot.Intern!;
        var gesamt = Math.Max(1, angebot.Groesse);
        // wie Python (30 s je Lesevorgang): kommt Stillstand lang kein Fortschritt, gilt der Download als zu langsam
        using var stillstand = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
        stillstand.CancelAfter(Stillstand);
        try
        {
            await Manager.DownloadUpdatesAsync(info, prozent =>
            {
                stillstand.CancelAfter(Stillstand);
                fortschritt(gesamt * prozent / 100, gesamt);
            }, stillstand.Token).ConfigureAwait(false);
        }
        catch (ChecksumFailedException)
        {
            throw new UpdateFehler(UpdateTexte.DownloadPruefsumme);
        }
        catch (OperationCanceledException) when (!abbruch.IsCancellationRequested)
        {
            throw new UpdateFehler(UpdateTexte.DownloadZeit);
        }
        return Path.Combine(VelopackLocator.Current.PackagesDir!, info.TargetFullRelease.FileName);
    }

    public void Installieren(UpdateAngebot angebot) =>
        // wartet, bis das Programm beendet ist, ersetzt dann und startet die neue Version
        Manager.WaitExitThenApplyUpdates(((UpdateInfo)angebot.Intern!).TargetFullRelease, silent: true, restart: neustart, Array.Empty<string>());
}
