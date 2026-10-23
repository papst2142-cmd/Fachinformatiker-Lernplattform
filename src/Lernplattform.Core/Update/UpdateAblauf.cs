using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Update;

/// <summary>
/// Ein verfuegbares Update (UpdateInfo in fisi_update.py). Version als Anzeige ("A 0.11.1"), Notizen
/// roh (Markdown), Seite = Release-Seite; Datei nur, wenn es fuer dieses System eine gibt.
/// Intern: Gegenstueck des Updaters (PC: Velopack-UpdateInfo), sonst null.
/// </summary>
public sealed record UpdateAngebot(string Version, string Notizen, string Seite, string? DateiName = null, string? DateiUrl = null,
    long Groesse = 0, string? Pruefsumme = null, object? Intern = null);

/// <summary>Fehler beim Suchen, Laden oder Installieren; der Text ist fuer die Anzeige (UpdateError).</summary>
public sealed class UpdateFehler(string text) : Exception(text);

/// <summary>
/// Die Quelle eines Updates. PC: Velopack (GitHub-Releases, in Tests Ordner oder lokaler Testserver).
/// Laden liefert den Pfad der geladenen Datei, Installieren uebergibt sie dem Updater, der nach dem
/// Ende des Programms ersetzt und neu startet.
/// </summary>
public interface IUpdateQuelle
{
    /// <summary>install_kind() is not None: das Programm laeuft als installierte Anwendung.</summary>
    bool Installiert { get; }

    Task<UpdateAngebot?> SuchenAsync(CancellationToken abbruch);

    Task<string> LadenAsync(UpdateAngebot angebot, Action<long, long> fortschritt, CancellationToken abbruch);

    void Installieren(UpdateAngebot angebot);
}

/// <summary>
/// Phase 5 BE9: Ablauf des Updates wie check_for_update / download / install (fisi_update.py) ueber
/// eine <see cref="IUpdateQuelle"/>: deutsche Fehlertexte, Schritte in update.log, Pruefung von Groesse
/// und SHA-256, vor dem Start eine Kopie des Lernstands (Nico, Frage 3), Merker "Update unterwegs"
/// und Ausschluss gegen Abgleich, Uebernahme und Sicherung (D8, <see cref="Vorgaenge"/>).
/// </summary>
public static class UpdateAblauf
{
    public const string KopiePraefix = "vor_update_";

    /// <summary>Suche; jeder Fehler wird zu einer UpdateFehler mit deutschem Text (nie ein Systemtext).</summary>
    public static async Task<UpdateAngebot?> SuchenAsync(IUpdateQuelle quelle, CancellationToken abbruch = default)
    {
        try
        {
            return await quelle.SuchenAsync(abbruch).ConfigureAwait(false);
        }
        catch (UpdateFehler)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new UpdateFehler(Unerwartet(e));
        }
    }

    public static string Unerwartet(Exception e) => UpdateTexte.UnerwarteterFehler.Replace("%s", e.GetType().Name, StringComparison.Ordinal);

    /// <summary>download: laedt, prueft Groesse und Pruefsumme, liefert den Pfad. fortschritt(geladen, gesamt).</summary>
    public static async Task<string> LadenAsync(IUpdateQuelle quelle, UpdateAngebot angebot, Action<long, long>? fortschritt = null,
        string? ordner = null, CancellationToken abbruch = default)
    {
        if (angebot.DateiName is null)
        {
            throw new UpdateFehler(UpdateTexte.KeinInstaller);
        }
        Updateprotokoll.Schreiben("Download startet: Version " + angebot.Version + ", " + angebot.DateiName, ordner);
        string datei;
        try
        {
            datei = await quelle.LadenAsync(angebot, fortschritt ?? ((_, _) => { }), abbruch).ConfigureAwait(false);
        }
        catch (UpdateFehler e)
        {
            Updateprotokoll.Schreiben("Download fehlgeschlagen: " + e.Message, ordner);
            throw;
        }
        catch (Exception e)
        {
            Updateprotokoll.Schreiben("Download fehlgeschlagen: " + e.GetType().Name, ordner);
            throw new UpdateFehler(DownloadText(e));
        }
        return Pruefen(datei, angebot, ordner);
    }

    /// <summary>Fehlertext je Fall wie download (HTTP, Zeit, Verbindung, unvollstaendig, Speichern).</summary>
    public static string DownloadText(Exception e) => e switch
    {
        HttpRequestException { StatusCode: { } code } => UpdateTexte.DownloadHttp.Replace("%d", ((int)code).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
        TaskCanceledException or TimeoutException => UpdateTexte.DownloadZeit,
        HttpRequestException { HttpRequestError: HttpRequestError.ResponseEnded or HttpRequestError.InvalidResponse } => UpdateTexte.DownloadUnvollstaendig,
        HttpRequestException or WebException => UpdateTexte.DownloadOffline,
        IOException or UnauthorizedAccessException => UpdateTexte.DownloadSpeichern,
        _ => Unerwartet(e),
    };

    /// <summary>
    /// Groesse und SHA-256 der geladenen Datei wie in download: falsche Groesse = unvollstaendig, falsche
    /// Pruefsumme = beschaedigt (die Datei wird dann entfernt). Ohne Pruefsumme nur die Groesse und eine Zeile.
    /// </summary>
    public static string Pruefen(string datei, UpdateAngebot angebot, string? ordner = null)
    {
        long groesse;
        string ist;
        try
        {
            groesse = new FileInfo(datei).Length;
            using var strom = File.OpenRead(datei);
            ist = Convert.ToHexStringLower(SHA256.HashData(strom));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Updateprotokoll.Schreiben("Download fehlgeschlagen: " + e.GetType().Name, ordner);
            throw new UpdateFehler(UpdateTexte.DownloadSpeichern);
        }
        if (angebot.Groesse > 0 && groesse != angebot.Groesse)
        {
            Updateprotokoll.Schreiben(string.Create(CultureInfo.InvariantCulture, $"Download unvollständig: {groesse} von {angebot.Groesse} Bytes"), ordner);
            Entfernen(datei);
            throw new UpdateFehler(UpdateTexte.DownloadUnvollstaendig);
        }
        var soll = ErwartetSha256(angebot.Pruefsumme);
        if (soll is not null && ist != soll)
        {
            Updateprotokoll.Schreiben("Prüfsumme falsch: erwartet " + soll + ", erhalten " + ist, ordner);
            Entfernen(datei);
            throw new UpdateFehler(UpdateTexte.DownloadPruefsumme);
        }
        if (soll is null)
        {
            Updateprotokoll.Schreiben("GitHub nennt keine Prüfsumme für " + angebot.DateiName + ", nur die Größe wird geprüft.", ordner);
        }
        Updateprotokoll.Schreiben(string.Create(CultureInfo.InvariantCulture,
            $"Download fertig: {angebot.DateiName} ({groesse} Bytes, {(soll is not null ? "Größe und Prüfsumme geprüft" : "Größe geprüft")})"), ordner);
        return datei;
    }

    /// <summary>_expected_sha256: Hex-Pruefsumme aus "sha256:&lt;hex&gt;" (wie die GitHub-Antwort), sonst null.</summary>
    public static string? ErwartetSha256(string? angabe)
    {
        if (angabe is null || !angabe.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var wert = Suche.PythonKlein.Klein(Rechner.PythonZeichen.Strip(angabe[7..]));
        return wert.Length == 64 && wert.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')) ? wert : null;
    }

    /// <summary>
    /// install: Kopie des Lernstands nach sicherungen/vor_update_*.db, Merker setzen, Updater starten.
    /// Liefert den Hinweistext; danach muss sich das Programm beenden (der Updater wartet darauf).
    /// Wartet wie Abgleich und Uebernahme auf <see cref="Vorgaenge"/> (nur im Hintergrund aufrufen);
    /// die Oberflaeche fragt vorher <see cref="Vorgaenge.Belegt"/> und lehnt dann mit AbgleichTexte.Belegt ab.
    /// </summary>
    public static string Installieren(IUpdateQuelle quelle, UpdateAngebot angebot, Einstellungen einstellungen, string? ordner = null, Uhr? uhr = null,
        bool sperreHalten = false)
    {
        if (!quelle.Installiert)
        {
            throw new UpdateFehler(UpdateTexte.NurInstalliert);
        }
        var sperre = Vorgaenge.Sperren();   // D8: nicht neben Abgleich, Uebernahme oder Sicherung (die Oberflaeche lehnt vorher ab)
        try
        {
            var text = InstallierenGesperrt(quelle, angebot, einstellungen, ordner, uhr);
            if (sperreHalten)
            {
                // bis das Programm endet (der Updater wartet darauf): kein Abgleich mehr dazwischen (Gegenpruefung W3 Fund 4)
                GehalteneSperre = sperre;
                sperre = null;
            }
            return text;
        }
        finally
        {
            sperre?.Dispose();
        }
    }

    /// <summary>Nach einem gestarteten Update gehaltene Sperre (gibt das Ende des Programms frei).</summary>
    internal static IDisposable? GehalteneSperre { get; set; }

    private static string InstallierenGesperrt(IUpdateQuelle quelle, UpdateAngebot angebot, Einstellungen einstellungen, string? ordner, Uhr? uhr)
    {
        var daten = ordner ?? Ablage.Datenordner();
        string? kopie;
        try
        {
            kopie = KopieAnlegen(Path.Combine(daten, Lernstand.DateiName), Path.Combine(daten, Uebernahme.SicherungsOrdner), uhr ?? Uhr.System);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SqliteException or NotSupportedException or ArgumentException)
        {
            Updateprotokoll.Schreiben("Kopie des Lernstands vor dem Update gescheitert: " + e.GetType().Name + ". Das Update wurde nicht gestartet.", ordner);
            throw new UpdateFehler(UpdateTexte.KopieFehler);
        }
        Updateprotokoll.Schreiben(kopie is null ? "Kein Lernstand vorhanden, keine Kopie nötig."
            : "Kopie des Lernstands: " + Uebernahme.SicherungsOrdner + "/" + Path.GetFileName(kopie), ordner);
        if (!UpdateMerker.Merken(einstellungen, angebot.Version))
        {
            Updateprotokoll.Schreiben("Der Merker für das Update ließ sich nicht speichern.", ordner);
        }
        Updateprotokoll.Schreiben("Installer wird gestartet: Velopack, Version " + angebot.Version + ", " + angebot.DateiName, ordner);
        try
        {
            quelle.Installieren(angebot);
        }
        catch (Exception e)
        {
            UpdateMerker.Vergessen(einstellungen);
            Updateprotokoll.Schreiben("Installer ließ sich nicht starten: " + e.GetType().Name, ordner);
            throw new UpdateFehler(UpdateTexte.StartFehler);
        }
        Updateprotokoll.Schreiben("Installer läuft. Das Programm wird jetzt beendet, der Installer wartet darauf.", ordner);
        return UpdateTexte.Installiert;
    }

    /// <summary>
    /// Kopie der Datenbank nach ordner/vor_update_JJJJ-MM-TT_HHMMSS.db (nie ueberschreiben, sonst Zaehler),
    /// danach quick_check; ohne Datenbank null. Wird nie von allein geloescht.
    /// </summary>
    public static string? KopieAnlegen(string datenbank, string ordner, Uhr uhr)
    {
        if (!File.Exists(datenbank))
        {
            return null;
        }
        Directory.CreateDirectory(ordner);
        var name = KopiePraefix + uhr.Jetzt().ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        var datei = Path.Combine(ordner, name + ".db");
        for (var n = 2; File.Exists(datei); n++)
        {
            datei = Path.Combine(ordner, name + "_" + n.ToString(CultureInfo.InvariantCulture) + ".db");
        }
        try
        {
            using (var quelle = Oeffnen(datenbank, SqliteOpenMode.ReadWrite))
            using (var kopie = Oeffnen(datei, SqliteOpenMode.ReadWriteCreate))
            {
                quelle.BackupDatabase(kopie);
            }
            using (var probe = Oeffnen(datei, SqliteOpenMode.ReadOnly))
            using (var befehl = probe.CreateCommand())
            {
                befehl.CommandText = "PRAGMA quick_check";
                if (befehl.ExecuteScalar() is not "ok")
                {
                    throw new IOException("quick_check");
                }
            }
            return datei;
        }
        catch
        {
            Entfernen(datei);       // nur die eben angefangene, unvollstaendige Kopie
            Entfernen(datei + "-journal");
            throw;
        }
    }

    private static SqliteConnection Oeffnen(string pfad, SqliteOpenMode art)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = pfad,
            Mode = art,
            Pooling = false,
            DefaultTimeout = Lernstand.Wartezeit,
        }.ToString());
        db.Open();
        return db;
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
