using System;
using System.IO;
using System.Linq;
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
        // Mac-Welle (Sammelliste 200): nur die echte Quelle (GitHub) fragt am Mac ohne Feed die Release-Seite
        Aktuell.Quelle = () => new VelopackQuelle(Quelle(), seiteOhneFeed: OperatingSystem.IsMacOS()
            && Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_QUELLE") is not { Length: > 0 });
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
        return Echt();
    }

    /// <summary>
    /// Phase 13 W2 (Weisung P12-17): nur Releases der Lernplattform (vA, vB, v1), nie die des Stadtbau-Werkzeugs (vS).
    /// A 0.53.2 (Sammelliste 256): stoerungsfest wie das Werkzeug seit S 0.25.1 (nur der Feed des neuesten Releases,
    /// jede Abfrage bei Stoerung bis zu 4 Mal, Zeilen WIEDERHOLUNG in update.log). Kanal = der, nach dem Velopack fragt.
    /// </summary>
    internal static StoerungsfesteQuelle Echt() =>
        new(Repo, Core.Update.ReleaseLinie.Linie.Lernplattform, null, text => Updateprotokoll.Schreiben(text), AbrufZeit);

    /// <summary>Zeitgrenze je Abruf von Release-Liste und Feed (GitHub antwortet mit 504 nach rund 11 s); vorher 30 Minuten.</summary>
    public static readonly TimeSpan AbrufZeit = TimeSpan.FromSeconds(30);

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
            if (modus == "feed")
            {
                return Feed(bericht);
            }
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

    /// <summary>
    /// A 0.53.2 (Sammelliste 256), nur lesend fuer die CI: fragt das ECHTE GitHub ueber dieselbe Quelle wie die Suche im
    /// Programm (<see cref="Echt"/>) nach dem Feed des Kanals LERNPLATTFORM_UPDATE_KANAL und schreibt
    /// "feed: neueste &lt;Paketversion&gt;" (die Probe vergleicht mit der hoechsten vA laut GitHub-API); jede Wiederholung
    /// steht als Zeile "feed: WIEDERHOLUNG ..." im Bericht. Installiert und laedt nichts.
    /// </summary>
    private static int Feed(string bericht)
    {
        var kanal = Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_KANAL") is { Length: > 0 } k ? k : "win";
        var quelle = Echt();
        quelle.Wiederholt += text => File.AppendAllText(bericht, "feed: " + text + Environment.NewLine);
        var feed = quelle.GetReleaseFeed(Velopack.Logging.NullVelopackLogger.Instance, null, kanal).GetAwaiter().GetResult();
        File.AppendAllText(bericht, $"feed: neueste {feed.Assets.Select(a => a.Version).Max()?.ToString() ?? "keine"} (Kanal {kanal}){Environment.NewLine}");
        return 0;
    }
}

/// <summary>Velopack als <see cref="IUpdateQuelle"/>; Fehler werden zu deutschen Texten wie in fisi_update.py.</summary>
internal sealed class VelopackQuelle(IUpdateSource quelle, bool neustart = true, bool seiteOhneFeed = false) : IUpdateQuelle
{
    /// <summary>Hoechstens so lange ohne Fortschritt (ein Prozent des Pakets), dann DownloadZeit.</summary>
    public static readonly TimeSpan Stillstand = TimeSpan.FromSeconds(60);

    /// <summary>
    /// A 0.53.2: so lange ohne Ergebnis und ohne Wiederholung, dann gilt die Suche als haengend (KeineVerbindung).
    /// Ein Abruf dauert hoechstens 2 × <see cref="Updater.AbrufZeit"/> (Velopack fragt bei einem Fehler noch einmal
    /// klein geschrieben), danach meldet die Quelle die Wiederholung.
    /// </summary>
    public static readonly TimeSpan SucheStillstand = 2 * Updater.AbrufZeit + TimeSpan.FromSeconds(10);

    private static readonly TimeSpan LaengstePause = Stoerung.Pausen.Max();

    public UpdateManager Manager { get; } = new(quelle);

    public bool Installiert => Manager.IsInstalled;

    public async Task<UpdateAngebot?> SuchenAsync(CancellationToken abbruch)
    {
        if (!Manager.IsInstalled)
        {
            // Ordner-Programm (aus dem Quellcode oder entpackt): nur Hinweis und Download-Seite (asset_url fehlt)
            return await SeiteSuchenAsync(abbruch).ConfigureAwait(false);
        }
        // Mac-Welle (Sammelliste 200): ohne Mac-Feed im Release zusaetzlich die GitHub-Seite fragen
        return await OhneFeed.SuchenAsync(() => VelopackSuchenAsync(abbruch), () => SeiteSuchenAsync(abbruch), seiteOhneFeed).ConfigureAwait(false);
    }

    private static async Task<UpdateAngebot?> SeiteSuchenAsync(CancellationToken abbruch)
    {
        using var http = new HttpClient();
        return await GithubSuche.SuchenAsync(http, Aktuell.Anzeige, "pc", abbruch: abbruch, melden: text => Updateprotokoll.Schreiben(text)).ConfigureAwait(false);
    }

    private async Task<UpdateAngebot?> VelopackSuchenAsync(CancellationToken abbruch)
    {
        UpdateInfo? info;
        // A 0.53.2 (Sammelliste 256): bis A 0.53.1 hoechstens GithubSuche.Wartezeit (10 s) fuer die ganze Suche, darin
        // passte keine Wiederholung. Jetzt gilt die Suche als haengend, wenn SucheStillstand lang nichts kommt; jede
        // Wiederholung der Quelle verlaengert um ihre Pause.
        using var stillstand = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
        stillstand.CancelAfter(SucheStillstand);
        void Weiter(string _) => stillstand.CancelAfter(LaengstePause + SucheStillstand);
        var stoerungsfest = quelle as StoerungsfesteQuelle;
        if (stoerungsfest is not null)
        {
            stoerungsfest.Wiederholt += Weiter;
        }
        try
        {
            info = await Manager.CheckForUpdatesAsync().WaitAsync(stillstand.Token).ConfigureAwait(false);
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
        finally
        {
            if (stoerungsfest is not null)
            {
                stoerungsfest.Wiederholt -= Weiter;
            }
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
        // A 0.53.2: eine Wiederholung des Pakets (Stoerung bei GitHub) verlaengert um ihre Pause
        void Weiter(string _) => stillstand.CancelAfter(LaengstePause + Stillstand);
        var stoerungsfest = quelle as StoerungsfesteQuelle;
        if (stoerungsfest is not null)
        {
            stoerungsfest.Wiederholt += Weiter;
        }
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
        finally
        {
            if (stoerungsfest is not null)
            {
                stoerungsfest.Wiederholt -= Weiter;
            }
        }
        return Path.Combine(VelopackLocator.Current.PackagesDir!, info.TargetFullRelease.FileName);
    }

    /// <summary>
    /// Patch A 0.37.0 (Sammelliste 203, Antwort 1c): am Mac ein Bundle, das der Benutzer nicht aendern darf
    /// (Installation "fuer alle Benutzer" in /Applications): der Updater laeuft ohne --silent und fragt nach dem
    /// Passwort, statt still zu scheitern. Sonst, und unter Windows und Linux, still wie bisher.
    /// </summary>
    public string? Hinweis => MitPasswort() ? UpdateTexte.MacPasswort : null;

    private static bool MitPasswort() =>
        OperatingSystem.IsMacOS() && MacBundle.Von(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) is { } bundle
            && !MacBundle.Beschreibbar(bundle);

    public void Installieren(UpdateAngebot angebot)
    {
        // Gegenprobe der Update-Probe in der CI (pakete_und_update.sh): erzwingt den alten stillen Weg, der bei einem
        // nicht beschreibbaren Bundle scheitern muss
        var still = !MitPasswort() || Environment.GetEnvironmentVariable("LERNPLATTFORM_UPDATE_GEGENPROBE_STILL") == "1";
        // wartet, bis das Programm beendet ist, ersetzt dann und startet die neue Version
        Manager.WaitExitThenApplyUpdates(((UpdateInfo)angebot.Intern!).TargetFullRelease, silent: still, restart: neustart, Array.Empty<string>());
    }
}
