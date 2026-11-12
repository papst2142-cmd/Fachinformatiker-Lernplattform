namespace Stadtbau.Core;

/// <summary>
/// Datenordner des Werkzeugs (getrennt von der Lernplattform): Windows %APPDATA%\Fachinformatiker-Stadtbau (seit S 0.25.1,
/// Sammelliste 250; vorher %LOCALAPPDATA%\Fachinformatiker-Stadtbau = Installationsordner von Velopack, siehe
/// <see cref="Datenumzug"/>), Linux
/// ~/.local/share/fachinformatiker-stadtbau, macOS ~/Library/Application Support/Fachinformatiker-Stadtbau, Android der
/// Ordner der App. STADTBAU_DATEN setzt ihn fuer Tests und den Start-Test. Darin: projekte/, sicherungen/, exporte/ und seit Welle 4c blueprints/.
/// </summary>
public sealed class Werkzeugordner(string wurzel)
{
    public const string Umgebung = "STADTBAU_DATEN";

    public string Wurzel { get; } = wurzel;

    public string Projekte => Path.Combine(Wurzel, "projekte");

    public string Sicherungen => Path.Combine(Wurzel, "sicherungen");

    public string Exporte => Path.Combine(Wurzel, "exporte");

    /// <summary>Phase 13 Welle 4c (Weisung P12-32): je Blueprint eine Datei (<see cref="Blueprint"/>).</summary>
    public string Blueprints => Path.Combine(Wurzel, "blueprints");

    public string FehlerLog => Path.Combine(Wurzel, "fehler.log");

    /// <summary>
    /// Nur fuer die Gegenprobe (Sammelliste 250): Windows wieder mit dem alten Ordner %LOCALAPPDATA%\Fachinformatiker-Stadtbau,
    /// in den Velopack das Werkzeug installiert (Setup und Deinstallation leeren ihn).
    /// </summary>
    internal static readonly AsyncLocal<bool> StandardAltFuerTests = new();

    public static Werkzeugordner Standard() => new(StandardPfad(Aktuell(), Environment.GetFolderPath, Environment.GetEnvironmentVariable));

    /// <summary>Betriebssystem fuer <see cref="StandardPfad"/> (Tests geben es vor).</summary>
    internal enum Betriebssystem
    {
        Windows,
        Mac,
        Android,
        Linux,
    }

    internal static Betriebssystem Aktuell() =>
        OperatingSystem.IsWindows() ? Betriebssystem.Windows
        : OperatingSystem.IsMacOS() ? Betriebssystem.Mac
        : OperatingSystem.IsAndroid() ? Betriebssystem.Android
        : Betriebssystem.Linux;

    /// <summary>Installationsordner von Velopack unter Windows (%LOCALAPPDATA%\Kennung) = Datenordner bis S 0.25.0.</summary>
    internal static string InstallationWindows(Func<Environment.SpecialFolder, string> ordner) =>
        Path.Combine(ordner(Environment.SpecialFolder.LocalApplicationData), Werkzeug.Kennung);

    /// <summary>Datenordner je System ohne Zugriff auf die Platte (testbar).</summary>
    internal static string StandardPfad(Betriebssystem system, Func<Environment.SpecialFolder, string> ordner, Func<string, string?> umgebung)
    {
        if (umgebung(Umgebung) is { Length: > 0 } eigen)
        {
            return eigen;
        }
        switch (system)
        {
            case Betriebssystem.Windows:
                // Velopack installiert nach %LOCALAPPDATA%\<Kennung> und leert diesen Ordner bei Setup und Deinstallation;
                // die Daten liegen deshalb wie bei der Lernplattform unter %APPDATA% (Sammelliste 250).
                return StandardAltFuerTests.Value
                    ? InstallationWindows(ordner)
                    : Path.Combine(ordner(Environment.SpecialFolder.ApplicationData), Werkzeug.Kennung);
            case Betriebssystem.Mac:
                // das Programm liegt als Bundle in ~/Applications oder /Applications, nie hier
                return Path.Combine(ordner(Environment.SpecialFolder.UserProfile), "Library", "Application Support", Werkzeug.Kennung);
            case Betriebssystem.Android:
                // privater Ordner der App; ein Update ueber eine neue APK laesst ihn stehen
                return Path.Combine(ordner(Environment.SpecialFolder.LocalApplicationData), Werkzeug.Kennung);
            default:
                var daten = umgebung("XDG_DATA_HOME") is { Length: > 0 } x
                    ? x
                    : Path.Combine(ordner(Environment.SpecialFolder.UserProfile), ".local", "share");
                return Path.Combine(daten, Werkzeug.Kennung.ToLowerInvariant());
        }
    }

    /// <summary>Ordner anlegen (alle vier).</summary>
    public Werkzeugordner Anlegen()
    {
        Directory.CreateDirectory(Projekte);
        Directory.CreateDirectory(Sicherungen);
        Directory.CreateDirectory(Exporte);
        Directory.CreateDirectory(Blueprints);
        return this;
    }

    /// <summary>Datei sicher schreiben: erst eine Kopie daneben, dann ersetzen (nie halb geschrieben).</summary>
    public static void SicherSchreiben(string pfad, byte[] daten)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pfad))!);
        var neu = pfad + ".neu";
        File.WriteAllBytes(neu, daten);
        File.Move(neu, pfad, overwrite: true);
    }

    /// <summary>Eine Zeile in fehler.log (nur Art und Text, nie Schluessel).</summary>
    public void Fehler(string text)
    {
        try
        {
            Directory.CreateDirectory(Wurzel);
            File.AppendAllText(FehlerLog, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " | " + text + Environment.NewLine);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private readonly HashSet<string> _ursachen = new(StringComparer.Ordinal);

    /// <summary>
    /// Welle A 0.67.0 (Weisung P12-204, E3): einen Fehler der 3D-Ansicht nur einmal je Ursache in fehler.log schreiben (vorher
    /// stand bei jeder Aenderung der Stadt die ganze Meldung erneut darin). Ursache = Bereich, Art, Text und erste Zeile des
    /// Aufrufs; gleiche Ursache spaeter = nichts geschrieben (false). Gegenprobe <see cref="JedesMalAltFuerTests"/>.
    /// </summary>
    public bool FehlerEinmal(string bereich, Exception? fehler)
    {
        var innen = fehler is AggregateException { InnerException: { } i } ? i : fehler;
        var ursache = Ursache(bereich, innen);
        lock (_ursachen)
        {
            if (!_ursachen.Add(ursache) && !JedesMalAltFuerTests)
            {
                return false;
            }
        }
        Fehler(bereich + ": " + innen + Environment.NewLine + "(gleiche Ursache wird in dieser Sitzung nicht noch einmal eingetragen)");
        return true;
    }

    /// <summary>Kennzeichen einer Ursache: Bereich, Art, Text und erste Zeile des Aufrufs.</summary>
    public static string Ursache(string bereich, Exception? fehler) => string.Join("|", bereich, fehler?.GetType().FullName ?? "",
        fehler?.Message ?? "", fehler?.StackTrace?.Split('\n', 2)[0].Trim() ?? "");

    /// <summary>Gegenprobe E3: jeder Fehler wird geschrieben (Stand A 0.66.1).</summary>
    public static bool JedesMalAltFuerTests { get => JedesMalAlt.Value; set => JedesMalAlt.Value = value; }

    private static readonly AsyncLocal<bool> JedesMalAlt = new();
}
