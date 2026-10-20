using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Lernplattform.Core.Design;

namespace Lernplattform.Design;

/// <summary>
/// Setzt eine berechnete Farbwelt als Ressourcen der Anwendung. Alle Flaechen
/// und Schriften holen ihre Farbe ueber "Pinsel.&lt;name&gt;" bzw. "Verlauf.&lt;name&gt;"
/// (DynamicResource); ein Wechsel tauscht nur diese Werte aus, die Oberflaeche
/// wird nicht neu aufgebaut.
/// </summary>
public static class FarbenAnwenden
{
    public static Palette Aktuell { get; private set; } =
        Farbwelt.Berechne(Farbwelt.Dunkel, Farbwelt.StandardGrundfarbe, Farbwelt.StandardHintergrund);

    public static void Anwenden(Palette palette)
    {
        var app = Application.Current ?? throw new InvalidOperationException("Keine Anwendung");
        var res = app.Resources;
        foreach (var (name, hex) in palette.Farben)
        {
            res["Pinsel." + name] = new SolidColorBrush(Color.Parse(hex));
        }
        foreach (var (name, (von, bis)) in palette.Verlaeufe)
        {
            res["Verlauf." + name] = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse(von), 0), new GradientStop(Color.Parse(bis), 1) },
            };
        }
        app.RequestedThemeVariant = palette.Hell ? ThemeVariant.Light : ThemeVariant.Dark;
        Aktuell = palette;
    }

    /// <summary>Bindet eine Eigenschaft an eine Farb-Ressource (wie DynamicResource in XAML).</summary>
    public static T Farbe<T>(this T element, AvaloniaProperty eigenschaft, string schluessel) where T : Control
    {
        element.Bind(eigenschaft, element.GetResourceObservable(schluessel));
        return element;
    }
}
