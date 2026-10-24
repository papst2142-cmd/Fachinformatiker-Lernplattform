using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>Phase 1, K6: Sammelseite "Lernen" (nur im schmalen Aufbau in der unteren Leiste), wie main.LearnScreen.</summary>
public static class LernenSeite
{
    public static Control Bauen(Action<string> oeffnen, Task<Lerninhalte?> laden)
    {
        var liste = new StackPanel { Spacing = 12, Margin = new Thickness(16, 4, 16, 24) };   // screen_list(spacing=12)
        var inhalte = laden.IsCompletedSuccessfully ? laden.Result : null;
        var untertitelZeilen = new List<(LernenEintrag Eintrag, TextBlock Zeile)>();
        foreach (var eintrag in Navigation.Lernen)
        {
            var (id, titel) = (eintrag.Id, eintrag.Titel);
            var untertitel = inhalte is null ? FarbenTexte.Laden : Navigation.LernenUntertitel(eintrag, inhalte);
            // A 0.12.2 wie LearnScreen am Handy: Symbolfeld 52 (Radius 16, Verlauf schraeg, Symbol 26 on_accent),
            // Titel 16 fett, Unterzeile 12 muted, Pfeil rechts muted; Kachel card mit Rand, Radius 16, Rand innen 14
            // (ab 160 % Texte darunter)
            var feld = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(16),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Symbole.Block(eintrag.Symbol, 26, "Pinsel.on_accent"),
            };
            if (eintrag.Farben.Count == 2)
            {
                feld.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.Parse(eintrag.Farben[0]), 0), new GradientStop(Color.Parse(eintrag.Farben[1]), 1) },
                };
            }
            else
            {
                feld.Farbe(Border.BackgroundProperty, "Verlauf." + eintrag.Farben[0]);
            }
            var unter = Schrift.Pt(untertitel, 12, FontWeight.Normal, "Pinsel.muted");
            var text = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { Schrift.Pt(titel, 16, FontWeight.Bold), unter },
            };
            untertitelZeilen.Add((eintrag, unter));
            var pfeil = Symbole.Block("CHEVRON_RIGHT", 24, "Pinsel.muted");
            var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 14, RowSpacing = 10 };
            Grid.SetColumn(pfeil, 2);
            zeile.Children.Add(feld);
            zeile.Children.Add(text);
            zeile.Children.Add(pfeil);
            // Ab 160 % (LARGE_FACTOR, "Kacheln untereinander") stehen Titel und Unterzeile unter Symbol und
            // Pfeil in voller Breite: neben dem Symbolfeld brach "Abschlussprojekt" sonst mitten im Wort
            Schrift.BeiStufe(zeile, () =>
            {
                var unten = Schrift.Gross;
                zeile.RowDefinitions = new RowDefinitions(unten ? "Auto,Auto" : "Auto");
                Grid.SetRow(text, unten ? 1 : 0);
                Grid.SetColumn(text, unten ? 0 : 1);
                Grid.SetColumnSpan(text, unten ? 3 : 1);
            });
            var knopf = new Button
            {
                Content = zeile,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
            }.Farbe(Button.BackgroundProperty, "Pinsel.card").Farbe(Button.BorderBrushProperty, "Pinsel.border");
            Avalonia.Automation.AutomationProperties.SetName(knopf, titel);
            knopf.Click += (_, _) => oeffnen(id);
            liste.Children.Add(knopf);
        }
        if (inhalte is null)
        {
            // Inhalte laden noch im Hintergrund: Zahlen nachtragen, sobald sie da sind
            _ = laden.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result is { } fertig)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var (eintrag, zeile) in untertitelZeilen)
                        {
                            zeile.Text = Navigation.LernenUntertitel(eintrag, fertig);
                        }
                    });
                }
            }, TaskScheduler.Default);
        }
        return new ScrollViewer { Content = liste };
    }
}
