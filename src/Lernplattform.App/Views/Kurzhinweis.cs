using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, W3: kurzer Hinweis unten wie show_badge_toast (fisi_game_gui 6680): Flaeche card_hi,
/// Rahmen yellow, 4 000 ms (TOAST_MS). Ein neuer Hinweis ersetzt den alten, ein Klick schliesst ihn.
/// Gleich auf PC und Handy. Genutzt fuer "Gespeichert: …" und die Vorlage im Abschlussprojekt.
/// </summary>
public static class Kurzhinweis
{
    /// <summary>TOAST_MS (fisi_core 2549).</summary>
    public const int Dauer = 4000;

    private static Panel? _ebene;
    private static DispatcherTimer? _uhr;

    /// <summary>Gerade gezeigter Hinweis (fuer die Layoutpruefung im Start ohne Bedienung).</summary>
    public static Control? Offen { get; private set; }

    /// <summary>Wird von MainView gesetzt: die Ebene ueber allen Seiten (laesst Klicks daneben durch).</summary>
    internal static void Ebene(Panel ebene) => _ebene = ebene;

    /// <param name="farbe">Rand des Hinweises (Phase 7 W6: wie toast(message, color) im Spiel), Vorgabe gelb.</param>
    public static void Zeigen(string text, string farbe = "Pinsel.yellow")
    {
        Schliessen();
        if (_ebene is not { } ebene)
        {
            return;
        }
        var hinweis = new Border
        {
            // ein langer Dateiname ("Gespeichert: Abschlussprojekt_…") darf nach "_" umbrechen (U+200B ohne Breite),
            // sonst trennt schmale Breite mit grosser Schrift ihn mitten im Wort; der Vorlesename bleibt unveraendert
            Child = Schrift.Text(text.Replace("_", "_\u200B", StringComparison.Ordinal), "Klein", FontWeight.Bold, "Pinsel.text"),
            Padding = new Thickness(18, 10),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(16, 0, 16, 24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            MaxWidth = 560,
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_hi").Farbe(Border.BorderBrushProperty, farbe);
        hinweis.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Schliessen();
        };
        Avalonia.Automation.AutomationProperties.SetName(hinweis, text);
        ebene.Children.Add(hinweis);
        ebene.IsVisible = true;
        Offen = hinweis;
        _uhr = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Dauer) };
        _uhr.Tick += (_, _) => Schliessen();
        _uhr.Start();
    }

    /// <summary>close_badge_toasts.</summary>
    public static void Schliessen()
    {
        _uhr?.Stop();
        _uhr = null;
        if (_ebene is { } ebene)
        {
            ebene.Children.Clear();
            ebene.IsVisible = false;
        }
        Offen = null;
    }
}
