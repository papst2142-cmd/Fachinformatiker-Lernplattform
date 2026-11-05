namespace Stadtbau.Core;

/// <summary>
/// Datenordner des Werkzeugs (getrennt von der Lernplattform): Windows %LOCALAPPDATA%\Fachinformatiker-Stadtbau, Linux
/// ~/.local/share/fachinformatiker-stadtbau, macOS ~/Library/Application Support/Fachinformatiker-Stadtbau, Android der
/// Ordner der App. STADTBAU_DATEN setzt ihn fuer Tests und den Start-Test. Darin: projekte/, sicherungen/, exporte/.
/// </summary>
public sealed class Werkzeugordner(string wurzel)
{
    public const string Umgebung = "STADTBAU_DATEN";

    public string Wurzel { get; } = wurzel;

    public string Projekte => Path.Combine(Wurzel, "projekte");

    public string Sicherungen => Path.Combine(Wurzel, "sicherungen");

    public string Exporte => Path.Combine(Wurzel, "exporte");

    public string FehlerLog => Path.Combine(Wurzel, "fehler.log");

    public static Werkzeugordner Standard()
    {
        if (Environment.GetEnvironmentVariable(Umgebung) is { Length: > 0 } eigen)
        {
            return new Werkzeugordner(eigen);
        }
        if (OperatingSystem.IsWindows())
        {
            return new Werkzeugordner(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Werkzeug.Kennung));
        }
        if (OperatingSystem.IsMacOS())
        {
            return new Werkzeugordner(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", Werkzeug.Kennung));
        }
        if (OperatingSystem.IsAndroid())
        {
            return new Werkzeugordner(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Werkzeug.Kennung));
        }
        var daten = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x
            ? x
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return new Werkzeugordner(Path.Combine(daten, Werkzeug.Kennung.ToLowerInvariant()));
    }

    /// <summary>Ordner anlegen (alle drei).</summary>
    public Werkzeugordner Anlegen()
    {
        Directory.CreateDirectory(Projekte);
        Directory.CreateDirectory(Sicherungen);
        Directory.CreateDirectory(Exporte);
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
}
