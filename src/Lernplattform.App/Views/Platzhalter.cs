using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>Phase 1, K6: noch nicht gebaute Bereiche zeigen ihren Titel und den Platzhalter-Hinweis (Nico, Frage 2a).</summary>
public static class Platzhalter
{
    public const string Text = Core.Texte.Beschriftungen.Platzhalter;

    public static Control Bauen(string titel)
    {
        var inhalt = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(titel, "Titel", FontWeight.SemiBold),
                Schrift.Text(Text, "Normal", FontWeight.Normal, "Pinsel.text_dim"),
            },
        };
        return new ScrollViewer
        {
            Content = new Border { Margin = new Thickness(16), Child = Schrift.Karte(inhalt), VerticalAlignment = VerticalAlignment.Top },
        };
    }
}
