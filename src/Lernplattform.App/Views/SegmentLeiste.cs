using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 1.17.0 (Weisung P12-430 B1, Reiter B aus Meldung P12-268, Hervorhebung wie Entwurf P12-271): Segment-Leiste oben auf der
/// Seite „Prüfung“: zwei gleich breite Haelften „AP1 / 20 %“ und „AP2 / 80 %“ in einer Flaeche card_alt mit Rand; die gezeigte
/// Haelfte im Verlauf primary mit Schrift on_accent, die andere text_dim. Am PC hoechstens 420 breit (wie im Entwurf).
/// Der Bildschirmleser liest die Woerter („AP1 20 %“).
/// </summary>
public sealed class SegmentLeiste : Border
{
    /// <summary>Hoechste Breite am PC (Entwurf P12-271).</summary>
    public const double BreiteHoechstens = 420;

    private readonly Dictionary<string, Button> _haelften = new(StringComparer.Ordinal);

    /// <param name="gewaehlt">gezeigte Haelfte (<see cref="Pruefungsteile.Teil1"/> oder <see cref="Pruefungsteile.Teil2"/>)</param>
    /// <param name="gewichte">Gewicht in Prozent je Haelfte</param>
    /// <param name="breit">PC-Ansicht (dann hoechstens <see cref="BreiteHoechstens"/> breit, links)</param>
    /// <param name="waehlen">Klick auf die andere Haelfte</param>
    public SegmentLeiste(string gewaehlt, (int Ap1, int Ap2) gewichte, bool breit, Action<string> waehlen)
    {
        var raster = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        foreach (var (id, titel, gewicht) in new[] { (Pruefungsteile.Teil1, Pruefungsteile.Teil1Titel, gewichte.Ap1), (Pruefungsteile.Teil2, Pruefungsteile.Teil2Titel, gewichte.Ap2) })
        {
            var aktiv = id == gewaehlt;
            var farbe = aktiv ? "Pinsel.on_accent" : "Pinsel.text_dim";
            var oben = Schrift.Pt(titel, 13, FontWeight.Bold, farbe);
            var unten = Schrift.Pt(Beschriftungen.Einsetzen(Pruefungsteile.SegmentGewicht, gewicht), 10.5, FontWeight.Medium, farbe);
            oben.TextAlignment = unten.TextAlignment = TextAlignment.Center;
            var knopf = new Button
            {
                Content = new StackPanel { Children = { oben, unten } },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(4, 6),
                CornerRadius = new CornerRadius(11),
                Background = Brushes.Transparent,
            };
            if (aktiv)
            {
                knopf.Farbe(Button.BackgroundProperty, "Verlauf.primary");
            }
            Avalonia.Automation.AutomationProperties.SetName(knopf, titel + " " + Beschriftungen.Einsetzen(Pruefungsteile.SegmentGewicht, gewicht));
            knopf.Click += (_, _) =>
            {
                if (!aktiv)
                {
                    waehlen(id);
                }
            };
            Grid.SetColumn(knopf, raster.Children.Count);
            raster.Children.Add(knopf);
            _haelften[id] = knopf;
        }
        Child = raster;
        Padding = new Thickness(3);
        CornerRadius = new CornerRadius(14);
        BorderThickness = new Thickness(1);
        if (breit)
        {
            MaxWidth = BreiteHoechstens;
            HorizontalAlignment = HorizontalAlignment.Left;
            Width = BreiteHoechstens;
        }
        this.Farbe(BackgroundProperty, "Pinsel.card_alt").Farbe(BorderBrushProperty, "Pinsel.border");
        Gewaehlt = gewaehlt;
    }

    /// <summary>Gezeigte Haelfte.</summary>
    public string Gewaehlt { get; }

    /// <summary>Start-Test: Klick auf eine Haelfte wie mit dem Finger.</summary>
    public void KlickFuerTests(string id) => _haelften[id].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
}
