using System.Globalization;
using System.Text;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Fehlerprotokoll "fehler.log" im Datenordner (neben der Datenbank), wie in
/// der Python-Linie (fisi_core.write_error_log, fisi_diagnose.read_log):
/// Kopfzeile mit Zeitpunkt, Version und System; Zugangsschluessel und
/// Benutzerpfade werden vor dem Schreiben entfernt; ueber 256 KB wird die
/// aeltere Haelfte verworfen. Schreiben darf selbst nie scheitern.
/// </summary>
public static class Fehlerprotokoll
{
    public const string DateiName = "fehler.log";
    public const int Hoechstgroesse = 256 * 1024;

    public const string KeinProtokoll = "Keine Fehler aufgezeichnet – die Datei fehler.log ist leer oder noch nicht "
        + "vorhanden. Das ist ein gutes Zeichen.";
    public const string Gekuerzt = "… (ältere Einträge gekürzt, gezeigt werden die letzten {0} KB)";

    private static readonly object Sperre = new();
    private static string _version = "";
    private static int _eingerichtet;

    // Phase 2, L10 (Weisung P2-11 A7): bekannte Geheimnisse (der eingetragene Zugangsschluessel)
    private static IReadOnlyList<string> _geheimnisse = [];

    public static string Pfad() => Path.Combine(Ablage.Datenordner(), DateiName);

    /// <summary>
    /// Phase 2, L10: Werte, die vor jedem Eintrag entfernt werden, bevor die Muster greifen
    /// (wie fisi_diagnose.scrub mit secrets). Aufruf mit den Einstellungen nach jedem Laden und
    /// Speichern; uebernommen werden die Werte geheimer Schluessel (Datenschutz.Geheimnisse).
    /// </summary>
    public static void GeheimnisseSetzen(IEnumerable<KeyValuePair<string, object?>> einstellungen) =>
        _geheimnisse = Datenschutz.Geheimnisse(einstellungen);

    /// <summary>System wie Pythons sys.platform (win32, linux, darwin; Android: android).</summary>
    public static string System()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win32";
        }
        if (OperatingSystem.IsAndroid())
        {
            return "android";
        }
        return OperatingSystem.IsMacOS() ? "darwin" : "linux";
    }

    /// <summary>Haengt einen Eintrag an. pfad nur fuer eine Datenbank an anderem Ort (fehler.log daneben) und fuer Tests.</summary>
    public static void Schreiben(string text, string? pfad = null)
    {
        try
        {
            if (pfad is null)
            {
                // wie Python: der eigene Datenordner wird angelegt, ein fremder Ort nie
                pfad = Pfad();
                Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            }
            lock (Sperre)
            {
                if (File.Exists(pfad) && new FileInfo(pfad).Length > Hoechstgroesse)
                {
                    var alt = Lesen(File.ReadAllBytes(pfad));
                    var rest = alt.Length > Hoechstgroesse / 2 ? alt[^(Hoechstgroesse / 2)..] : alt;
                    File.WriteAllText(pfad, rest, new UTF8Encoding(false));
                }
                var zeit = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                var inhalt = Datenschutz.PfadeAnonymisieren(Datenschutz.Schwaerzen(text.TrimEnd(), _geheimnisse));
                var eintrag = $"=== {zeit} | Version {(_version.Length > 0 ? _version : "?")} | {System()} ===\n{inhalt}\n";
                // Zeilenenden wie Python im Textmodus (Windows \r\n, sonst \n)
                eintrag = eintrag.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", Environment.NewLine, StringComparison.Ordinal);
                File.AppendAllText(pfad, eintrag, new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // Darf nie scheitern (kein Schreibrecht, volles Laufwerk ...)
        }
    }

    /// <summary>Schreibt eine Ausnahme mit allen Einzelheiten.</summary>
    public static void Schreiben(Exception fehler, string? pfad = null) => Schreiben(fehler.ToString(), pfad);

    /// <summary>
    /// Leitet unbehandelte Fehler (Programm, Hintergrundaufgaben) zusaetzlich
    /// nach fehler.log. Oberflaeche und Android melden ihre Fehler ueber
    /// <see cref="Schreiben(Exception, string?)"/>. Mehrfacher Aufruf schadet nicht.
    /// </summary>
    public static void Einrichten(string version)
    {
        _version = version;
        if (Interlocked.Exchange(ref _eingerichtet, 1) == 1)
        {
            return;
        }
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception fehler)
            {
                Schreiben(fehler);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) => Schreiben(e.Exception);
    }

    /// <summary>
    /// Inhalt von fehler.log, hoechstens die letzten grenze Bytes (bei
    /// gekuerztem Anfang ab der naechsten ganzen Zeile, mit Hinweis). Fehlt
    /// die Datei oder ist sie leer: "".
    /// </summary>
    public static string Anzeigen(string? pfad = null, int grenze = Hoechstgroesse)
    {
        pfad ??= Pfad();
        byte[] roh;
        long groesse;
        try
        {
            // haenger.log haelt die Haenger-Diagnose zum Schreiben offen: Mitbenutzung erlauben (Windows, Gegenpruefung W2 Fund 1)
            using var datei = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            groesse = datei.Length;
            datei.Seek(Math.Max(0, groesse - grenze), SeekOrigin.Begin);
            using var puffer = new MemoryStream();
            datei.CopyTo(puffer);
            roh = puffer.ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
        var text = Lesen(roh);
        if (groesse > grenze)
        {
            var schnitt = text.IndexOf('\n');
            text = string.Format(CultureInfo.InvariantCulture, Gekuerzt, grenze / 1024) + "\n"
                + (schnitt >= 0 ? text[(schnitt + 1)..] : "");
        }
        return text.Trim();
    }

    /// <summary>UTF-8 lesen, kaputte Bytes werden ersetzt (wie errors="replace").</summary>
    private static string Lesen(byte[] roh) => new UTF8Encoding(false, false).GetString(roh);
}
