using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.22.0 (Weisung P12-91 Punkt 2): Symbol der Aufgabenart statt eines Hakens (es gibt keinen Status). Einfache
/// Linien im Feld 24 × 24, gezeichnet in der Akzentfarbe auf einem runden Grund; „liste“ = Kopf eines Orts.
/// </summary>
internal static class AufgabenSymbol
{
    private static readonly Dictionary<string, string> Pfade = new(StringComparer.Ordinal)
    {
        // Kopf eines Orts: Liste
        ["liste"] = "M 6,7 L 18,7 M 6,12 L 18,12 M 6,17 L 14,17",
        // Auswahl: Kreis mit Haken-Punkt (Option gewaehlt) und zwei Zeilen
        ["auswahl"] = "M 4,8 A 2.5,2.5 0 1 1 9,8 A 2.5,2.5 0 1 1 4,8 M 12,8 L 20,8 M 4,16 A 2.5,2.5 0 1 1 9,16 A 2.5,2.5 0 1 1 4,16 M 12,16 L 20,16",
        // Terminal: Eingabezeichen und Strich
        ["terminal"] = "M 5,7 L 10,12 L 5,17 M 12,17 L 19,17",
        // Zuordnung: zwei Pfeile gegeneinander
        ["zuordnung"] = "M 4,9 L 19,9 M 15,5 L 19,9 L 15,13 M 20,15 L 5,15 M 9,11 L 5,15 L 9,19",
        // Formular: Blatt mit Zeilen
        ["formular"] = "M 6,3 L 15,3 L 19,7 L 19,21 L 6,21 Z M 9,11 L 16,11 M 9,15 L 16,15 M 9,18 L 13,18",
        // Diagnose: Lupe
        ["diagnose"] = "M 5,10 A 5,5 0 1 1 15,10 A 5,5 0 1 1 5,10 M 14,14 L 20,20",
        // Wartung: Schraubenschluessel
        ["wartung"] = "M 15,4 A 4.5,4.5 0 1 0 20,9 L 17,10 L 14,7 L 15,4 M 13,11 L 5,19",
        // Bestellung: Einkaufswagen
        ["bestellung"] = "M 3,5 L 6,5 L 8,15 L 18,15 L 20,8 L 7,8 M 9,19 A 1.2,1.2 0 1 1 11.4,19 A 1.2,1.2 0 1 1 9,19 M 15,19 A 1.2,1.2 0 1 1 17.4,19 A 1.2,1.2 0 1 1 15,19",
        // Bauteile: Chip mit Beinchen
        ["bauteile"] = "M 7,7 L 17,7 L 17,17 L 7,17 Z M 10,4 L 10,7 M 14,4 L 14,7 M 10,17 L 10,20 M 14,17 L 14,20 M 4,10 L 7,10 M 4,14 L 7,14 M 17,10 L 20,10 M 17,14 L 20,14",
        // Rack: drei Einschuebe
        ["rack"] = "M 5,4 L 19,4 L 19,20 L 5,20 Z M 5,9 L 19,9 M 5,14 L 19,14 M 8,6.5 L 10,6.5 M 8,11.5 L 10,11.5 M 8,17 L 10,17",
    };

    /// <summary>Alle bekannten Arten (Test: jede Art der Spielgeschichte hat ein eigenes Symbol).</summary>
    public static IReadOnlyCollection<string> Arten => Pfade.Keys;

    /// <summary>Symbol als Kreis mit Linie, <paramref name="groesse"/> Bildpunkte.</summary>
    public static Control Bauen(string art, double groesse)
    {
        var pfad = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(Pfade.TryGetValue(art, out var p) ? p : Pfade["liste"]),
            StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            Width = groesse * 0.58,
            Height = groesse * 0.58,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        }.Farbe(Shape.StrokeProperty, "Pinsel.accent");
        return new Border
        {
            Width = groesse,
            Height = groesse,
            CornerRadius = new CornerRadius(groesse / 2),
            Child = pfad,
            IsHitTestVisible = false,
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt");
    }
}
