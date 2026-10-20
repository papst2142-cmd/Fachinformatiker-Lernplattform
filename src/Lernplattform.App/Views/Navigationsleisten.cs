using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>Ein Eintrag der Navigation (Kennung, Beschriftung, Unterpunkte).</summary>
public sealed record NavPunkt(string Id, string Titel, IReadOnlyList<NavPunkt> Unter);

/// <summary>
/// Phase 1, K6: Seitenleiste (breit, Eintraege wie am PC) und untere Leiste
/// (schmal, 6 Eintraege wie am Handy). Beide werden einmal gebaut; je nach
/// Fensterbreite ist eine davon sichtbar.
/// </summary>
public sealed class Navigationsleisten
{
    private readonly Action<string> _oeffnen;
    private readonly Dictionary<string, Button> _seitenKnoepfe = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _untenKnoepfe = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StackPanel> _unterpunkte = new(StringComparer.Ordinal);

    public Control Seitenleiste { get; }
    public Control UntereLeiste { get; }

    public Navigationsleisten(Action<string> oeffnen)
    {
        _oeffnen = oeffnen;
        Seitenleiste = BaueSeitenleiste();
        UntereLeiste = BaueUntereLeiste();
    }

    private Control BaueSeitenleiste()
    {
        var liste = new StackPanel { Spacing = 2, Margin = new Thickness(10, 14) };
        liste.Children.Add(new Border
        {
            Margin = new Thickness(6, 0, 6, 14),
            Child = Schrift.Text(Texte.Programmname, "Mittel", FontWeight.Bold),
        });
        foreach (var punkt in Texte.NavigationPc)
        {
            liste.Children.Add(Eintrag(punkt, _seitenKnoepfe, 0));
            if (punkt.Unter.Count > 0)
            {
                var unter = new StackPanel { Spacing = 2, IsVisible = false };
                foreach (var u in punkt.Unter)
                {
                    unter.Children.Add(Eintrag(u, _seitenKnoepfe, 1));
                }
                _unterpunkte[punkt.Id] = unter;
                liste.Children.Add(unter);
            }
        }
        var leiste = new Border
        {
            Width = 240,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new ScrollViewer { Content = liste },
        };
        // Breite waechst mit der Schrift mit, damit "Prüfungstrainer" einzeilig bleibt
        leiste.Bind(Layoutable.WidthProperty, leiste.GetResourceObservable("Schrift.Normal", v => v is double d ? 240 * d / 14 : 240));
        return leiste.Farbe(Border.BackgroundProperty, "Pinsel.sidebar").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private Button Eintrag(NavPunkt punkt, Dictionary<string, Button> ablage, int ebene)
    {
        var knopf = new Button
        {
            Content = Schrift.Text(punkt.Titel, ebene == 0 ? "Normal" : "Klein"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12 + 14 * ebene, 8, 12, 8),
            Background = Brushes.Transparent,
        };
        Avalonia.Automation.AutomationProperties.SetName(knopf, punkt.Titel);
        knopf.Click += (_, _) => _oeffnen(punkt.Id);
        ablage[punkt.Id] = knopf;
        return knopf;
    }

    private Control BaueUntereLeiste()
    {
        // Grid mit gleich breiten Spalten statt UniformGrid: UniformGrid rundet jede Zelle auf
        // ganze Punkte und schiebt die letzte bis 2 Punkte ueber den Rand (Layoutpruefung 1360 breit)
        var raster = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", Texte.NavigationHandy.Count))) };
        foreach (var punkt in Texte.NavigationHandy)
        {
            var beschriftung = new TextBlock
            {
                Text = punkt.Titel,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Center,
            }.Groesse("Leiste").Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
            var knopf = new Button
            {
                Content = beschriftung,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 14),
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(0),
            };
            Avalonia.Automation.AutomationProperties.SetName(knopf, punkt.Titel);
            knopf.Click += (_, _) => _oeffnen(punkt.Id);
            _untenKnoepfe[punkt.Id] = knopf;
            Grid.SetColumn(knopf, raster.Children.Count);
            raster.Children.Add(knopf);
        }
        var leiste = new Border { Child = raster, BorderThickness = new Thickness(0, 1, 0, 0), IsVisible = false };
        return leiste.Farbe(Border.BackgroundProperty, "Pinsel.sidebar").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    /// <summary>Hebt den Eintrag der offenen Seite hervor (am Handy: Lernbereiche unter "Lernen").</summary>
    public void Markiere(string id)
    {
        foreach (var (kennung, knopf) in _seitenKnoepfe)
        {
            Hervorheben(knopf, kennung == id);
        }
        foreach (var (eltern, unter) in _unterpunkte)
        {
            var punkt = Texte.NavigationPc.First(p => p.Id == eltern);
            unter.IsVisible = id == eltern || punkt.Unter.Any(u => u.Id == id);
        }
        var unten = Texte.HandyZiel(id);
        foreach (var (kennung, knopf) in _untenKnoepfe)
        {
            Hervorheben(knopf, kennung == unten);
        }
    }

    private static void Hervorheben(Button knopf, bool aktiv)
    {
        if (aktiv)
        {
            knopf.Farbe(Button.BackgroundProperty, "Pinsel.card_hi");
        }
        else
        {
            knopf.ClearValue(Button.BackgroundProperty);
            knopf.Background = Brushes.Transparent;
        }
        if (knopf.Content is TextBlock text)
        {
            text.Farbe(TextBlock.ForegroundProperty, aktiv ? "Pinsel.accent" : "Pinsel.text");
            text.FontWeight = aktiv ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }
}
