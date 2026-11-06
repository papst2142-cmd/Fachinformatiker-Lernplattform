using System.Reflection;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Namen Nico 1a, Weisung P12-16): feste Namen des Stadtbau-Werkzeugs (nach der ersten
/// Auslieferung nie aendern) und die Version der eigenen Linie "S 0.x" (aus Directory.Build.props, StadtbauVersion).
/// </summary>
public static class Werkzeug
{
    public const string Name = "Fachinformatiker Stadtbau";

    /// <summary>Datenordner und Velopack-Kennung (Linux klein geschrieben).</summary>
    public const string Kennung = "Fachinformatiker-Stadtbau";

    public const string Repository = "papst2142-cmd/Fachinformatiker-Lernplattform";

    /// <summary>Version "0.1.0" aus der Baugruppe (Directory.Build.props, StadtbauVersion).</summary>
    public static string Version { get; } = typeof(Werkzeug).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Anzeige "S 0.5.0".</summary>
    public static string Anzeige => "S " + Version;

    /// <summary>Release-Tag "vS0.1.0" (Linie des Werkzeugs, Lernplattform.Core.Update.ReleaseLinie).</summary>
    public static string Tag => "vS" + Version;

    /// <summary>Text fuer das Feld "werkzeug" einer Export-Datei.</summary>
    public static string Kennzeile => Name + " " + Anzeige;

    /// <summary>Stufe aus der Baugruppe ("S"), Pruefung im Test.</summary>
    public static string? Stufe => typeof(Werkzeug).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "Stufe")?.Value;
}
