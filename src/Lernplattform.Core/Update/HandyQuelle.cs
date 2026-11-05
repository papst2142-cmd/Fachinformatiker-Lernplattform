using System.Net.Http.Headers;
using System.Security.Cryptography;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Update;

/// <summary>
/// A 0.39.0: was die Update-Quelle am Handy vom Geraet braucht (Android-Teil in Lernplattform.Android,
/// in Tests eine Attrappe). Nur Geraetezugriffe, keine Oberflaeche.
/// </summary>
public interface IHandyGeraet
{
    /// <summary>Ordner im App-Speicher fuer die geladene APK (keine Speicher-Erlaubnis noetig).</summary>
    string LadeOrdner { get; }

    /// <summary>Freie Bytes im Ladeordner.</summary>
    long FreierSpeicher { get; }

    /// <summary>true im WLAN (oder per Kabel), false ueber mobile Daten, null unbekannt.</summary>
    bool? ImWlan { get; }

    /// <summary>Erlaubnis "Unbekannte Apps installieren" fuer diese App (vor Android 8 immer true).</summary>
    bool DarfInstallieren { get; }

    /// <summary>Oeffnet die Einstellung der Erlaubnis und liefert nach der Rueckkehr den neuen Zustand.</summary>
    Task<bool> ErlaubnisHolenAsync();

    string EigenesPaket { get; }

    long EigenerVersionscode { get; }

    /// <summary>Liest Paketname, Versionscode und Zertifikate einer APK; null, wenn Android sie nicht lesen kann.</summary>
    ApkAngaben? ApkLesen(string pfad);

    /// <summary>
    /// Uebergibt die APK an Android (PackageInstaller-Session). Wirft bei einem Fehler beim Uebergeben; das Ergebnis
    /// der Installation kommt spaeter ueber <see cref="HandyQuelle.Rueckmeldung"/> (bei Erfolg beendet Android die App).
    /// </summary>
    void Installieren(string pfad);
}

/// <summary>
/// A 0.39.0 (Weisung P11-39/P11-40): Update im Programm am Handy wie am PC. Suche ueber GitHub (wie bisher),
/// Laden in den App-Speicher mit Fortschritt, Pruefung (<see cref="HandyPruefung"/>), dann Uebergabe an Android.
/// Kopie vor_update_*, Merker und Sperre macht wie am PC <see cref="UpdateAblauf.Installieren"/>.
/// </summary>
public sealed class HandyQuelle(HttpClient http, IHandyGeraet geraet, string aktuell, string url = GithubSuche.NeuesteUrl,
    string fingerabdruck = HandyPruefung.Fingerabdruck, string? datenordner = null) : IUpdateQuelle
{
    private string? _datei;

    public bool Installiert => true;

    public string Name => "Android";

    public IHandyGeraet Geraet => geraet;

    public Task<UpdateAngebot?> SuchenAsync(CancellationToken abbruch) => GithubSuche.SuchenAsync(http, aktuell, "android", url, abbruch);

    public async Task<string> LadenAsync(UpdateAngebot angebot, Action<long, long> fortschritt, CancellationToken abbruch)
    {
        if (angebot.DateiName is null || angebot.DateiUrl is null)
        {
            throw new UpdateFehler(UpdateTexte.KeinInstaller);
        }
        var soll = HandyPruefung.Erwartet(angebot);
        var ordner = geraet.LadeOrdner;
        Directory.CreateDirectory(ordner);
        // alte Reste eines frueheren Versuchs zaehlen nicht zum freien Platz
        AlteEntfernen(ordner);
        if (geraet.FreierSpeicher < HandyPruefung.Noetig(angebot.Groesse))
        {
            throw new UpdateFehler(UpdateTexte.KeinPlatz.Replace("%s", HandyPruefung.Mb(HandyPruefung.Noetig(angebot.Groesse)), StringComparison.Ordinal));
        }
        var ziel = Path.Combine(ordner, Path.GetFileName(angebot.DateiName));
        var teil = ziel + ".part";
        using (var anfrage = new HttpRequestMessage(HttpMethod.Get, angebot.DateiUrl))
        {
            anfrage.Headers.UserAgent.Add(new ProductInfoHeaderValue(GithubSuche.Kennung, null));
            using var antwort = await http.SendAsync(anfrage, HttpCompletionOption.ResponseHeadersRead, abbruch).ConfigureAwait(false);
            antwort.EnsureSuccessStatusCode();
            var gesamt = antwort.Content.Headers.ContentLength ?? angebot.Groesse;
            await using var strom = await antwort.Content.ReadAsStreamAsync(abbruch).ConfigureAwait(false);
            await using (var datei = new FileStream(teil, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var puffer = new byte[81920];
                long geladen = 0;
                int n;
                while ((n = await strom.ReadAsync(puffer, abbruch).ConfigureAwait(false)) > 0)
                {
                    await datei.WriteAsync(puffer.AsMemory(0, n), abbruch).ConfigureAwait(false);
                    geladen += n;
                    fortschritt(geladen, gesamt);
                }
            }
        }
        File.Move(teil, ziel, true);
        // Groesse und Pruefsumme prueft danach UpdateAblauf.Pruefen gegen den digest; hier zusaetzlich gegen die Tabelle
        string ist;
        await using (var lesen = File.OpenRead(ziel))
        {
            ist = Convert.ToHexStringLower(await SHA256.HashDataAsync(lesen, abbruch).ConfigureAwait(false));
        }
        if (ist != soll)
        {
            Entfernen(ziel);
            throw new UpdateFehler(UpdateTexte.DownloadPruefsumme);
        }
        if (HandyPruefung.ApkPruefen(geraet.ApkLesen(ziel), geraet.EigenesPaket, geraet.EigenerVersionscode, fingerabdruck) is { } fehler)
        {
            Updateprotokoll.Schreiben("APK abgelehnt: " + fehler, datenordner);
            Entfernen(ziel);
            throw new UpdateFehler(fehler);
        }
        _datei = ziel;
        return ziel;
    }

    public void Installieren(UpdateAngebot angebot)
    {
        if (_datei is null || !File.Exists(_datei))
        {
            throw new FileNotFoundException("APK");
        }
        geraet.Installieren(_datei);
    }

    /// <summary>
    /// Rueckmeldung von Android (aus dem Android-Teil gerufen): Zeile in update.log; bei einem Fehler Merker weg,
    /// gehaltene Sperre frei, Datei weg. Liefert den Text fuer das Fenster oder null (Erfolg, Rueckfrage).
    /// </summary>
    public static string? Rueckmeldung(int status, string? meldung, long groesse, Daten.Einstellungen einstellungen, string ordner, string? datenordner = null)
    {
        Updateprotokoll.Schreiben("Android meldet: " + HandyPruefung.StatusName(status)
            + (string.IsNullOrEmpty(meldung) ? "" : ", " + Kuerzen(meldung)), datenordner);
        var text = HandyPruefung.StatusText(status, groesse);
        if (text is null)
        {
            return null;
        }
        UpdateMerker.Vergessen(einstellungen);
        UpdateAblauf.SperreFreigeben();
        AlteEntfernen(ordner);
        return text;
    }

    // Android-Meldungen koennen Pfade nennen: nur die erste Zeile, hoechstens 120 Zeichen
    private static string Kuerzen(string meldung)
    {
        var zeile = meldung.Split('\n')[0].Trim();
        return zeile.Length > 120 ? zeile[..120] + "…" : zeile;
    }

    private static void AlteEntfernen(string ordner)
    {
        try
        {
            if (Directory.Exists(ordner))
            {
                foreach (var datei in Directory.GetFiles(ordner))
                {
                    Entfernen(datei);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void Entfernen(string pfad)
    {
        try
        {
            File.Delete(pfad);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
