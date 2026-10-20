using System.Reflection;

namespace Lernplattform.Aktualisierung;

/// <summary>
/// Verbindung zum Updater. Der PC-Start (Desktop, Velopack) setzt die Funktionen;
/// auf Android bleiben sie leer, dort laeuft das Update ueber eine neue APK.
/// </summary>
public static class Aktualisierung
{
    /// <summary>Paketversion fuer den Updater, z. B. 1.0.0-a.0.1.0 (A &lt; B &lt; 1.0).</summary>
    public static string PaketVersion { get; } = Metadaten("PaketVersion");

    /// <summary>Anzeige wie "A 0.1.0"; ab 1.0 ohne Stufe.</summary>
    public static string Anzeige { get; } = (Metadaten("Stufe") + " " + (typeof(Aktualisierung).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?")).Trim();

    /// <summary>Kurzer Zustand des Updaters (installiert, Version), oder null ohne Updater.</summary>
    public static Func<string>? Zustand { get; set; }

    /// <summary>Sucht im Ordner ein neueres Paket, laedt es und startet neu. Liefert sonst den Grund.</summary>
    public static Func<string, Task<string>>? AusOrdner { get; set; }

    private static string Metadaten(string schluessel) => typeof(Aktualisierung).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == schluessel)?.Value ?? "";
}
