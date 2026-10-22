namespace Lernplattform.Tests;

/// <summary>Pfade zu Vergleichswerten und Inhalten im Testausgabeordner.</summary>
internal static class Pfade
{
    public static string Vergleich(string datei) => Path.Combine(AppContext.BaseDirectory, "Vergleich", datei);

    public static string Inhalte => Path.Combine(AppContext.BaseDirectory, "inhalte");

    /// <summary>Wurzel des Repos (Ordner mit Directory.Build.props).</summary>
    public static string Wurzel
    {
        get
        {
            var ordner = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(ordner.FullName, "Directory.Build.props")))
            {
                ordner = ordner.Parent!;
            }
            return ordner.FullName;
        }
    }
}
