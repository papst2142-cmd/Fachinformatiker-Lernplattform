namespace Lernplattform.Core.Design;

/// <summary>
/// Welle A 0.99.0 (Sammelliste 400, Weisung P12-320, Nico 3b): Farbe eines Symbols auf einer leuchtenden Grundfarbe (runder
/// Haltung-Knopf am rechten Daumen). Schwarz oder Weiss, je nachdem, was mehr Kontrast hat; die Grenze
/// <see cref="Farbwelt.MindestKontrast"/> pruefen <c>SpielHaltungGehenTests</c> in allen 72 Farbwelten (nie lockern).
/// </summary>
public static class Symbolfarbe
{
    /// <summary>Dunkle Symbolfarbe.</summary>
    public const string Dunkel = "#111111";

    /// <summary>Helle Symbolfarbe.</summary>
    public const string Hell = "#FFFFFF";

    /// <summary>Symbolfarbe auf der Fuellung <paramref name="fuellung"/> (Hex "#RRGGBB").</summary>
    public static string Auf(string fuellung) =>
        Farbwelt.Kontrast(Dunkel, fuellung) >= Farbwelt.Kontrast(Hell, fuellung) ? Dunkel : Hell;
}
