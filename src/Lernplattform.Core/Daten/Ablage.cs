namespace Lernplattform.Core.Daten;

/// <summary>
/// Datenordner der C#-Linie (Plan 4.2): Windows %APPDATA%\Fachinformatiker-Lernplattform,
/// Linux ~/.local/share/fachinformatiker-lernplattform, macOS und Android im
/// App-Datenordner. Getrennt vom alten Ordner FISI-Lernplattform der Python-Linie.
/// Bleibt bei jedem Update gleich (durchgehende Update-Linie A, B, 1.0).
/// </summary>
public static class Ablage
{
    public const string Name = "Fachinformatiker-Lernplattform";

    public static string Datenordner()
    {
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_DATEN") is { Length: > 0 } eigener)
        {
            return eigener;
        }
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
        }
        var basis = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basis, OperatingSystem.IsLinux() ? Name.ToLowerInvariant() : Name);
    }

    /// <summary>
    /// Phase 0, Spike D: Probe-Datei im Datenordner. Wird einmal angelegt und nie
    /// ueberschrieben; nach einem Update zeigt sie, dass der Datenordner erhalten blieb.
    /// </summary>
    public static string UpdateProbe(string version)
    {
        var ordner = Datenordner();
        Directory.CreateDirectory(ordner);
        var datei = Path.Combine(ordner, "update_probe.txt");
        if (!File.Exists(datei))
        {
            File.WriteAllText(datei, $"angelegt von {version} am {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        }
        return File.ReadAllText(datei);
    }
}
