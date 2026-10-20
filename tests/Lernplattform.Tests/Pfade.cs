namespace Lernplattform.Tests;

/// <summary>Pfade zu Vergleichswerten und Inhalten im Testausgabeordner.</summary>
internal static class Pfade
{
    public static string Vergleich(string datei) => Path.Combine(AppContext.BaseDirectory, "Vergleich", datei);

    public static string Inhalte => Path.Combine(AppContext.BaseDirectory, "inhalte");
}
