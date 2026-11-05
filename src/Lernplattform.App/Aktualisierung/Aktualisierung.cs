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

    /// <summary>
    /// Phase 5 BE9: Quelle des Updates am PC (Velopack), vom Desktop-Start gesetzt. Seit A 0.39.0 setzt auch der
    /// Android-Start sie (<see cref="Core.Update.HandyQuelle"/>); nur die Probe-APK laesst sie leer, dort sucht
    /// <see cref="Core.Update.GithubSuche"/> und die APK kommt ueber den Browser.
    /// </summary>
    public static Func<Core.Update.IUpdateQuelle>? Quelle { get; set; }

    /// <summary>A 0.39.0: Geraetezugriffe fuer das Update am Handy (Erlaubnis, WLAN, Installieren), sonst null.</summary>
    public static Core.Update.IHandyGeraet? Handy { get; set; }

    private static string Metadaten(string schluessel) => typeof(Aktualisierung).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == schluessel)?.Value ?? "";
}
