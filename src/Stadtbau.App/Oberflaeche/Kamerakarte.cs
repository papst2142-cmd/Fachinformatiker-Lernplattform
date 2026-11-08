using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Design;
using Lernplattform.Views;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle Q (Weisung P12-76 Punkt 8, Kurzplan Meldung P12-42 Punkt 8): kleine Karte unten rechts in der Ansicht mit den
/// Knoepfen der Kamera (naeher, weiter, alles zeigen, kippen) und dem Schalter „Einrasten“. Gleich in „Stadt von oben“, in der
/// 3D-Ansicht, im Grundriss und im Gebaeude-Editor. <see cref="Ueber"/> legt sie ueber die Flaeche; die Flaeche haelt beim
/// Einpassen unten so viel Platz frei, wie die Karte hoch ist (<see cref="Hoehe"/>), damit sie nie einen Hausnamen verdeckt.
/// </summary>
internal static class Kamerakarte
{
    /// <summary>Abstand der Karte zum Rand der Ansicht.</summary>
    public const double Rand = 10;

    /// <summary>Die Karte mit ihren Reihen (jede Reihe ein WrapPanel oder ein Schalter).</summary>
    public static Border Bauen(params Control[] reihen)
    {
        var inhalt = new StackPanel { Spacing = 6 };
        foreach (var r in reihen)
        {
            inhalt.Children.Add(r);
        }
        var karte = new Border
        {
            Child = inhalt,
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(Rand),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
        return karte.Vorlese(Texte.KameraKarte);
    }

    /// <summary>Die Karte ueber die Flaeche legen (beide im selben Raster, die Karte unten rechts).</summary>
    public static Grid Ueber(Control flaeche, Border karte, Action<double>? hoeheGeaendert = null)
    {
        var g = new Grid();
        g.Children.Add(flaeche);
        g.Children.Add(karte);
        if (hoeheGeaendert is not null)
        {
            karte.SizeChanged += (_, _) => hoeheGeaendert(Hoehe(karte));
            karte.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty)
                {
                    hoeheGeaendert(Hoehe(karte));
                }
            };
        }
        return g;
    }

    /// <summary>Platz, den die Karte unten in der Ansicht braucht (0, wenn sie nicht sichtbar ist).</summary>
    public static double Hoehe(Border karte) => karte.IsVisible ? karte.Bounds.Height + (2 * Rand) : 0;
}
