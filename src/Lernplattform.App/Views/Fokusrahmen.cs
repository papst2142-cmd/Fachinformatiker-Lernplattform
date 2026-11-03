using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 11 W2 (Weisung P11-20, Plan Phase 11 Abschnitt 3): sichtbarer Fokusrahmen fuer die Bedienung mit der
/// Tastatur (Tab, Pfeiltasten). Er erscheint nur, wenn der Fokus mit der Tastatur kommt, nie beim Klicken oder
/// Tippen. Ein Rahmen von <see cref="Dicke"/> px in der Textfarbe (Pinsel.text, in jeder Farbwelt kontrastreich zu
/// Hintergrund und Karten), <see cref="Abstand"/> px ausserhalb des Elements, mit dessen Ecken (Pillen bleiben
/// rund). Ersetzt den eckigen schwarz-weissen Rahmen von Fluent, der im dunklen Modus kaum zu sehen war.
/// Elemente mit eigenem Fokusbild aus Fluent (Eingabefeld, Auswahlliste) behalten es.
/// </summary>
public static class Fokusrahmen
{
    public const double Dicke = 2;
    public const double Abstand = 2;

    internal static void Einrichten(Application app)
    {
        var stil = new Style(x => x.OfType<AdornerLayer>());
        stil.Setters.Add(new Setter(AdornerLayer.DefaultFocusAdornerProperty, new FuncTemplate<Control>(Bauen)));
        app.Styles.Add(stil);
    }

    /// <summary>Ein Rahmen (fuer den Start-Test auch einzeln baubar).</summary>
    public static Border Bauen()
    {
        var rahmen = new Border
        {
            BorderThickness = new Thickness(Dicke),
            Margin = new Thickness(-(Dicke + Abstand)),
            IsHitTestVisible = false,
        }.Farbe(Border.BorderBrushProperty, "Pinsel.text");
        // der Rahmen liegt ausserhalb des Elements: ohne das schneidet die Ebene ihn an dessen Rand ab (nur die Ecken blieben)
        AdornerLayer.SetIsClipEnabled(rahmen, false);
        rahmen.AttachedToVisualTree += (_, _) =>
        {
            var ecken = AdornerLayer.GetAdornedElement(rahmen) switch
            {
                Border b => b.CornerRadius,
                TemplatedControl t => t.CornerRadius,
                _ => new CornerRadius(4),
            };
            // die Ecken des Rahmens liegen um Dicke + Abstand weiter aussen
            var plus = Dicke + Abstand;
            rahmen.CornerRadius = new CornerRadius(Rund(ecken.TopLeft, plus), Rund(ecken.TopRight, plus),
                Rund(ecken.BottomRight, plus), Rund(ecken.BottomLeft, plus));
        };
        return rahmen;
    }

    private static double Rund(double ecke, double plus) => ecke > 0 ? ecke + plus : 0;
}
