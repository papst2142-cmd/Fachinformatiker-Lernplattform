using Avalonia.Media;

namespace Lernplattform.Views;

/// <summary>
/// Eigene, einfache Symbole (selbst gezeichnet, keine fremde Symbolschrift): Lupe fuer
/// die Suche, Fragezeichen im Kreis fuer die Hilfe.
/// </summary>
public static class Symbole
{
    public static readonly Geometry Suche = Geometry.Parse(
        "M10,2 A8,8 0 1 0 15.2,16.6 L21,22.4 L22.4,21 L16.6,15.2 A8,8 0 0 0 10,2 Z M10,4 A6,6 0 1 1 10,16 A6,6 0 1 1 10,4 Z");

    public static readonly Geometry Hilfe = Geometry.Parse(
        "M12,1 A11,11 0 1 0 12,23 A11,11 0 1 0 12,1 Z M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 Z "
        + "M12,5.5 C9.8,5.5 8.2,7 8.2,9.2 L10.2,9.2 C10.2,8.1 11,7.4 12,7.4 C13,7.4 13.8,8.1 13.8,9 C13.8,10.9 11,10.7 11,13.8 L13,13.8 "
        + "C13,11.7 15.8,11.4 15.8,9 C15.8,7 14.2,5.5 12,5.5 Z M11,15.5 L13,15.5 L13,17.5 L11,17.5 Z");
}
