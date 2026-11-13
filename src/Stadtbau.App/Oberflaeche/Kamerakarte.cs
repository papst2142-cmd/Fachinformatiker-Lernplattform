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

    /// <summary>Knopf „Einklappen“/„Kamera“ jeder Karte (Start-Test).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Border, Button> Klappen = new();

    /// <summary>
    /// Die Karte mit ihren Reihen (jede Reihe ein WrapPanel oder ein Schalter). Welle S 0.52.0 (Sammelliste 334, Weisung
    /// P12-237 Punkt 5: die Karte deckte im Innenraum in 3D ein Viertel der Flaeche und einen Teil des roten Schattens): oben
    /// rechts der Knopf „Einklappen“; eingeklappt bleibt nur der kleine Knopf „Kamera“, ein Tipp klappt sie wieder auf. Die
    /// Flaeche haelt unten nur noch so viel Platz frei, wie die eingeklappte Karte hoch ist (<see cref="Hoehe"/>).
    /// </summary>
    public static Border Bauen(params Control[] reihen)
    {
        var inhalt = new StackPanel { Spacing = 6 };
        var klappe = new Button
        {
            Content = Schrift.Text(Texte.KameraEinklappen, "Klein"),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
        }.Vorlese(Texte.KameraEinklappen).Mit(Texte.KameraEinklappenTipp);
        inhalt.Children.Add(klappe);
        var huelle = new StackPanel { Spacing = 6 };
        foreach (var r in reihen)
        {
            huelle.Children.Add(r);
        }
        inhalt.Children.Add(huelle);
        klappe.Click += (_, _) => Umschalten(klappe, inhalt);
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
        Klappen.AddOrUpdate(karte, klappe);
        return karte.Vorlese(Texte.KameraKarte);
    }

    private static void Umschalten(Button klappe, StackPanel inhalt)
    {
        // eingeklappt: die Reihen liegen in einer unsichtbaren Huelle; so behalten sie ihre eigene Sichtbarkeit (z. B. die
        // Zeile „Etage“, die nur bei Etagen da ist) und kommen beim Aufklappen genau so wieder
        var huelle = (Panel)inhalt.Children[1];
        var zu = huelle.IsVisible;
        huelle.IsVisible = !zu;
        var text = zu ? Texte.KameraAufklappen : Texte.KameraEinklappen;
        klappe.Content = Schrift.Text(text, "Klein");
        var tipp = zu ? Texte.KameraAufklappenTipp : Texte.KameraEinklappenTipp;
        klappe.Vorlese(text);
        Tipp.Setzen(klappe, tipp);
    }

    /// <summary>Ist die Karte eingeklappt? (Start-Test)</summary>
    public static bool Eingeklappt(Border karte) =>
        karte.Child is StackPanel s && s.Children.Count > 1 && !s.Children[1].IsVisible;

    /// <summary>Knopf „Einklappen“/„Kamera“ der Karte (Start-Test).</summary>
    public static Button? KlappKnopf(Border karte) => Klappen.TryGetValue(karte, out var k) ? k : null;

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
