using System.Globalization;
using System.Text.RegularExpressions;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Update;

/// <summary>Angaben aus einer geladenen APK (Android liest sie aus, ohne zu installieren).</summary>
/// <param name="Paket">Paketname (applicationId).</param>
/// <param name="Versionscode">Android-Versionscode.</param>
/// <param name="Zertifikate">SHA-256 der Signatur-Zertifikate, klein geschrieben (64 Hexzeichen).</param>
public sealed record ApkAngaben(string Paket, long Versionscode, IReadOnlyList<string> Zertifikate);

/// <summary>
/// A 0.39.0 (Weisung P11-39/P11-40, Plan P11-30 Abschnitt 1): Pruefungen einer geladenen APK, bevor sie an
/// Android geht. Alle muessen stimmen, sonst wird die Datei geloescht und nichts installiert:
/// Groesse = GitHub, SHA-256 = digest von GitHub UND = Tabelle "Prüfsummen (SHA-256)" im Release-Text,
/// Paketname = eigener Paketname (die Probe-APK ersetzt nie die echte App und umgekehrt), Versionscode groesser,
/// genau ein Zertifikat mit dem Fingerabdruck des Release-Schluessels.
/// </summary>
public static partial class HandyPruefung
{
    /// <summary>SHA-256 des Release-Zertifikats, gleich werkzeuge/release/android_fingerabdruck.txt (Test HandyUpdateTests).</summary>
    public const string Fingerabdruck = "328aae64ab7ed35e37b0a84cdd046289deb59e940b45a29120ec1ee42b2bf012";

    /// <summary>Platz, der neben der doppelten APK-Groesse frei bleiben muss (Datei im App-Speicher plus Kopie in der Session).</summary>
    public const long Rand = 20L * 1024 * 1024;

    /// <summary>Bytes, die vor dem Laden frei sein muessen.</summary>
    public static long Noetig(long groesse) => checked(Math.Max(0, groesse) * 2 + Rand);

    /// <summary>Zahl in MB mit einer Nachkommastelle wie die Fortschrittszeile ("42,0").</summary>
    public static string Mb(long bytes) => Rechner.Zahlen.DeZahl(bytes / 1048576.0, 1);

    [GeneratedRegex(@"^\|\s*`([^`|]+)`\s*\|\s*\d+\s*\|\s*`([0-9A-Fa-f]{64})`\s*\|\s*$", RegexOptions.Multiline)]
    private static partial Regex TabellenZeile();

    /// <summary>
    /// Pruefsumme der Datei aus der Tabelle im Release-Text (release.py: "| `name` | groesse | `sha256` |"),
    /// klein geschrieben; null ohne Text, ohne Zeile oder bei mehr als einer Zeile fuer denselben Namen.
    /// </summary>
    public static string? AusReleaseText(string? text, string dateiName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        string? gefunden = null;
        foreach (Match zeile in TabellenZeile().Matches(text.Replace("\r\n", "\n", StringComparison.Ordinal)))
        {
            if (zeile.Groups[1].Value != dateiName)
            {
                continue;
            }
            if (gefunden is not null)
            {
                return null;
            }
            gefunden = Suche.PythonKlein.Klein(zeile.Groups[2].Value);
        }
        return gefunden;
    }

    /// <summary>
    /// Vor dem Laden: beide Pruefsummen muessen da sein (sonst KeinePruefsumme) und gleich sein (sonst
    /// DownloadPruefsumme, denn eine der beiden Angaben stimmt dann nicht). Liefert die erwartete Pruefsumme.
    /// </summary>
    public static string Erwartet(UpdateAngebot angebot)
    {
        var digest = UpdateAblauf.ErwartetSha256(angebot.Pruefsumme);
        var tabelle = angebot.TabellenPruefsumme;
        if (digest is null || tabelle is null)
        {
            throw new UpdateFehler(UpdateTexte.KeinePruefsumme);
        }
        if (digest != tabelle)
        {
            throw new UpdateFehler(UpdateTexte.DownloadPruefsumme);
        }
        return digest;
    }

    /// <summary>
    /// Nach dem Laden: Angaben der APK gegen die laufende App. null = in Ordnung, sonst der Fehlertext.
    /// apk null = Android konnte die Datei nicht lesen (zaehlt wie eine fremde Datei).
    /// </summary>
    public static string? ApkPruefen(ApkAngaben? apk, string eigenesPaket, long eigenerVersionscode, string fingerabdruck = Fingerabdruck)
    {
        if (apk is null || apk.Paket != eigenesPaket)
        {
            return UpdateTexte.FremdeDatei;
        }
        if (apk.Zertifikate.Count != 1 || !string.Equals(apk.Zertifikate[0], fingerabdruck, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateTexte.FremdeDatei;
        }
        if (apk.Versionscode <= eigenerVersionscode)
        {
            return UpdateTexte.NichtNeuer;
        }
        return null;
    }

    // Rueckmeldung der Installation (PackageInstaller.STATUS_*)
    public const int StatusRueckfrage = -1;
    public const int StatusFertig = 0;
    public const int StatusFehler = 1;
    public const int StatusGesperrt = 2;
    public const int StatusAbgebrochen = 3;
    public const int StatusUngueltig = 4;
    public const int StatusKonflikt = 5;
    public const int StatusSpeicher = 6;
    public const int StatusUnvertraeglich = 7;

    /// <summary>Text fuer eine Rueckmeldung von Android; null bei Erfolg oder Rueckfrage (dann zeigt Android selbst etwas).</summary>
    public static string? StatusText(int status, long groesse) => status switch
    {
        StatusRueckfrage or StatusFertig => null,
        StatusAbgebrochen => UpdateTexte.HandyAbgebrochen,
        StatusSpeicher => UpdateTexte.KeinPlatz.Replace("%s", Mb(Noetig(groesse)), StringComparison.Ordinal),
        StatusGesperrt => UpdateTexte.AndroidGesperrt,
        StatusKonflikt or StatusUnvertraeglich => UpdateTexte.FremdeDatei,
        _ => UpdateTexte.StartFehler,
    };

    /// <summary>Kurzname einer Rueckmeldung fuer update.log.</summary>
    public static string StatusName(int status) => status switch
    {
        StatusRueckfrage => "Rückfrage von Android",
        StatusFertig => "fertig",
        StatusFehler => "Fehler",
        StatusGesperrt => "gesperrt",
        StatusAbgebrochen => "abgebrochen",
        StatusUngueltig => "ungültige Datei",
        StatusKonflikt => "Konflikt",
        StatusSpeicher => "kein Speicher",
        StatusUnvertraeglich => "unverträglich",
        _ => "unbekannt",
    } + " (Status " + status.ToString(CultureInfo.InvariantCulture) + ")";
}
