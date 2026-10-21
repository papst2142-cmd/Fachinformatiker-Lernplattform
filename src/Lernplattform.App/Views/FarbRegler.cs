using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Design;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L12 (Sammelliste 14, Weisung P2-14): Regler der eigenen Farben mit farbiger Spur
/// wie in der Python-Linie (GradientSlider am PC, Spur hinter ft.Slider am Handy). Die Spur ist
/// ein Verlauf mit einer Stuetzfarbe je ganzzahligem Wert (Frage 4 a), Hoehe 10, ganz
/// abgerundet, 1 px Rand in field_border; der Knopf (Radius 9) ist hell oder dunkel mit Rand
/// in der anderen Farbe (Core.Design.Reglerspur, G23). Bedienung bleibt der Slider von Avalonia;
/// Pfeile +-1, Umschalt+Pfeil und Bild auf/ab +-10, der Farbton laeuft im Kreis (_nudge).
/// </summary>
public sealed class FarbRegler
{
    /// <summary>Python: GradientSlider.TRACK_H.</summary>
    public const double SpurHoehe = 10;

    /// <summary>Python: GradientSlider.KNOB_R.</summary>
    public const double KnopfRadius = 9;

    private readonly ReglerKanal _kanal;
    private readonly Border _spur;
    private readonly SolidColorBrush _fuellung = new(Colors.White);
    private readonly SolidColorBrush _rand = new(Colors.Black);
    private double?[]? _gezeichnet;

    public FarbRegler(ReglerKanal kanal)
    {
        _kanal = kanal;
        Regler = new Slider
        {
            Minimum = kanal.Min,
            Maximum = kanal.Max,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            SmallChange = 1,
            LargeChange = 10,
            MinWidth = 160,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Spur des Sliders unsichtbar, die farbige Spur liegt dahinter (wie am Handy in Python)
        foreach (var name in (string[])["SliderTrackFill", "SliderTrackFillPointerOver", "SliderTrackFillPressed", "SliderTrackFillDisabled",
                     "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed", "SliderTrackValueFillDisabled"])
        {
            Regler.Resources[name] = Brushes.Transparent;
        }
        foreach (var name in (string[])["SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed", "SliderThumbBackgroundDisabled"])
        {
            Regler.Resources[name] = _fuellung;
        }
        Regler.Resources["SliderHorizontalThumbWidth"] = 2 * KnopfRadius;
        Regler.Resources["SliderHorizontalThumbHeight"] = 2 * KnopfRadius;
        Regler.Resources["SliderThumbCornerRadius"] = new CornerRadius(KnopfRadius);
        // Die Vorlage des Sliders setzt Groesse, Fuellung und Rand des Knopfes selbst (Rand 0);
        // eigene Werte direkt am Knopf haben Vorrang
        Regler.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Track>("PART_Track")?.Thumb is { } knopf)
            {
                knopf.Width = 2 * KnopfRadius;
                knopf.Height = 2 * KnopfRadius;
                knopf.CornerRadius = new CornerRadius(KnopfRadius);
                knopf.Background = _fuellung;
                knopf.BorderBrush = _rand;
                // Python: Ring = Radius - innerer Radius, mindestens 2 (max(2, px(2.5)))
                knopf.BorderThickness = new Thickness(2.5);
            }
        };
        Regler.AddHandler(InputElement.KeyDownEvent, Taste, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _spur = new Border
        {
            Height = SpurHoehe,
            CornerRadius = new CornerRadius(SpurHoehe / 2),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(KnopfRadius, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        }.Farbe(Border.BorderBrushProperty, "Pinsel.field_border");
        Teil = new Panel { Children = { _spur, Regler } };
    }

    /// <summary>Der Slider (Wert, Bedienung, Bildschirmleser).</summary>
    public Slider Regler { get; }

    /// <summary>Spur und Slider zum Einfuegen in die Zeile.</summary>
    public Control Teil { get; }

    /// <summary>Anzahl der Stuetzfarben der Spur (fuer Test und Messung).</summary>
    public int Stuetzen => (_spur.Background as LinearGradientBrush)?.GradientStops.Count ?? 0;

    /// <summary>Spur und Knopf auf die Reglerwerte der Gruppe bringen (nur bei Aenderung, wie Python _tracks).</summary>
    public void Zeichne(double?[] werte)
    {
        if (_gezeichnet is not null && _gezeichnet.SequenceEqual(werte))
        {
            return;
        }
        _gezeichnet = [.. werte];
        var farben = Reglerspur.Stuetzfarben(werte, _kanal.Id);
        var stopps = new GradientStops();
        var letzte = farben.Count - 1;
        for (var i = 0; i <= letzte; i++)
        {
            stopps.Add(new GradientStop(Color.Parse(farben[i]), i / (double)letzte));
        }
        _spur.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = stopps,
        };
        var (fuellung, rand) = Reglerspur.Knopf(werte, _kanal.Id);
        _fuellung.Color = Color.Parse(fuellung);
        _rand.Color = Color.Parse(rand);
    }

    private void Taste(object? sender, KeyEventArgs e)
    {
        var umschalt = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        int? schritt = e.Key switch
        {
            Key.Left or Key.Down => umschalt && e.Key == Key.Left ? -10 : -1,
            Key.Right or Key.Up => umschalt && e.Key == Key.Right ? 10 : 1,
            Key.PageUp => 10,
            Key.PageDown => -10,
            _ => null,
        };
        if (schritt is not { } s)
        {
            return;
        }
        // Der neue Wert geht wie beim Ziehen ueber ValueChanged an die Vorschau
        Regler.Value = Reglerspur.Schritt(_kanal, (int)Math.Round(Regler.Value), s);
        e.Handled = true;
    }
}
