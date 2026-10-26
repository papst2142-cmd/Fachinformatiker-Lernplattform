using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 0.12.2: Klappkarte wie ui.FoldCard(marker=True) am Handy: ganze Karte (card, Rand border,
/// Radius 16, Rand innen 16); Kopf als Schaltflaeche (mindestens 48 hoch: 10 oben und unten) mit
/// Pfeil 28 in der Akzentfarbe links (ARROW_RIGHT zu, ARROW_DROP_DOWN offen), Strich 4 x 14, Titel in
/// Grossbuchstaben (11 fett, Akzentfarbe) mit Unterschrift darunter (11 muted) und rechts
/// "aufklappen"/"zuklappen" (12 fett text_dim; ab 160 % unter Titel und Unterschrift). Bleibt ein <see cref="Expander"/> (IsExpanded,
/// Bildschirmleser "Titel, zugeklappt"), nur mit eigenem Aussehen.
/// </summary>
public static class Klappkarte
{
    public static Expander Bauen(string titel, string? unter, Control inhalt, string akzent = "Pinsel.accent", bool offen = false) =>
        Bauen(titel, string.IsNullOrEmpty(unter) ? null : Schrift.Text(unter, "Winzig", FontWeight.Normal, "Pinsel.muted"), inhalt, akzent, offen);

    /// <summary>Phase 8 W2b: mit eigener Unterschrift (die Optionen aendern sie, z. B. "Version … ist verfügbar.").</summary>
    public static Expander Bauen(string titel, TextBlock? unterText, Control inhalt, string akzent = "Pinsel.accent", bool offen = false)
    {
        var klappe = new Expander
        {
            Content = inhalt,
            IsExpanded = offen,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        klappe.Template = new FuncControlTemplate<Expander>((e, _) =>
        {
            var pfeil = Symbole.Block("ARROW_RIGHT", 28, akzent);
            var strich = new Border { Width = 4, Height = 14, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center }
                .Farbe(Border.BackgroundProperty, akzent);
            var titelText = Schrift.Text(Beschriftungen.Gross(titel), "Winzig", FontWeight.Bold, akzent);
            var texte = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { titelText } };
            if (unterText is not null)
            {
                (unterText.Parent as Panel)?.Children.Remove(unterText);
                texte.Children.Add(unterText);
            }
            var zustand = Schrift.Text("", "Klein", FontWeight.Bold, "Pinsel.text_dim");
            zustand.TextWrapping = TextWrapping.NoWrap;
            zustand.VerticalAlignment = VerticalAlignment.Center;
            var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 6 };
            Grid.SetColumn(strich, 1);
            Grid.SetColumn(texte, 2);
            // ACTION_GAP 8 als Rand eines Rahmens, nicht der Beschriftung (sonst gilt sie als abgeschnitten)
            var zustandRahmen = new Border { Padding = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = zustand };
            Grid.SetColumn(zustandRahmen, 3);
            zeile.Children.Add(pfeil);
            zeile.Children.Add(strich);
            zeile.Children.Add(texte);
            // Ab 160 % steht "aufklappen"/"zuklappen" unter Titel und Unterschrift (wie tile_row ab
            // LARGE_FACTOR): rechts daneben brach ein langer Titel wie "ZURUECKSETZEN" sonst mitten im Wort
            Schrift.BeiStufe(zeile, () =>
            {
                var unten = Schrift.Gross;
                if (unten == texte.Children.Contains(zustandRahmen) && zustandRahmen.Parent is not null)
                {
                    return;
                }
                (zustandRahmen.Parent as Panel)?.Children.Remove(zustandRahmen);
                zustandRahmen.Padding = new Thickness(unten ? 0 : 8, 0, 0, 0);
                zustandRahmen.HorizontalAlignment = HorizontalAlignment.Left;
                (unten ? texte.Children : zeile.Children).Add(zustandRahmen);
            });
            var kopf = new Button
            {
                Content = zeile,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0, 10),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
            };
            kopf.Click += (_, _) => e.IsExpanded = !e.IsExpanded;
            var koerper = new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                Margin = new Thickness(0, 12, 0, 0),
                [!ContentPresenter.ContentProperty] = e[!ContentControl.ContentProperty],
                [!ContentPresenter.ContentTemplateProperty] = e[!ContentControl.ContentTemplateProperty],
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            void Zeigen()
            {
                pfeil.Text = Symbole.Text(e.IsExpanded ? "ARROW_DROP_DOWN" : "ARROW_RIGHT");
                zustand.Text = e.IsExpanded ? KlappTexte.Zuklappen : KlappTexte.Aufklappen;
                koerper.IsVisible = e.IsExpanded;
                Avalonia.Automation.AutomationProperties.SetName(kopf, KlappTexte.Name(titel, e.IsExpanded));
            }
            Zeigen();
            e.GetObservable(Expander.IsExpandedProperty).Subscribe(new Beobachter(Zeigen));
            return new Border
            {
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Child = new StackPanel { Children = { kopf, koerper } },
            }.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
        });
        Avalonia.Automation.AutomationProperties.SetName(klappe, titel);
        return klappe;
    }

    private sealed class Beobachter(Action aktion) : IObserver<bool>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(bool value) => aktion();
    }
}
