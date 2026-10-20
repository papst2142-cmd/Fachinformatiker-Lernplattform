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
        var liste = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        var inhalte = laden.IsCompletedSuccessfully ? laden.Result : null;
        var untertitelZeilen = new List<(LernenEintrag Eintrag, TextBlock Zeile)>();
        foreach (var eintrag in Navigation.Lernen)
        {
            var (id, titel) = (eintrag.Id, eintrag.Titel);
            var untertitel = inhalte is null ? FarbenTexte.Laden : Navigation.LernenUntertitel(eintrag, inhalte);
            var text = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    Schrift.Text(titel, "Mittel", FontWeight.Bold),
                    Schrift.Text(untertitel, "Klein", FontWeight.Normal, "Pinsel.muted"),
                },
            };
            untertitelZeilen.Add((eintrag, (TextBlock)text.Children[1]));
            var knopf = new Button
            {
                Content = text,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
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
