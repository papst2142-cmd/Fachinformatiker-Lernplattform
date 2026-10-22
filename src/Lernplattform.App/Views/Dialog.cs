using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2: Hinweis- und Rueckfragefenster (wie messagebox in Python) als Ebene ueber der
/// Seite, gleich auf PC und Handy. Titel, Text oder eigener Inhalt, Knoepfe; Rueckgabe ist
/// die Nummer des gewaehlten Knopfs, -1 bei Schliessen mit Escape bzw. ohne Ebene.
/// </summary>
public static class Dialog
{
    public const string Ok = "OK";

    private static Panel? _ebene;

    /// <summary>Gerade offenes Fenster (fuer die Layoutpruefung im Start ohne Bedienung).</summary>
    public static Control? Offen { get; private set; }

    /// <summary>Wird von MainView gesetzt: die Ebene ueber allen Seiten.</summary>
    internal static void Ebene(Panel ebene) => _ebene = ebene;

    public static Task<int> Zeigen(string titel, string text, params string[] knoepfe) =>
        Zeigen(titel, Schrift.Text(text), knoepfe);

    public static Task<int> Zeigen(string titel, Control inhalt, params string[] knoepfe) => Zeigen(titel, inhalt, null, knoepfe);

    /// <summary>W4: wie oben; vorbereiten bekommt die Knoepfe (z. B. "Speichern" erst nach einer Wahl freigeben).</summary>
    public static Task<int> Zeigen(string titel, Control inhalt, Action<IReadOnlyList<Button>>? vorbereiten, params string[] knoepfe)
    {
        var fertig = new TaskCompletionSource<int>();
        if (_ebene is not { } ebene)
        {
            fertig.SetResult(-1);
            return fertig.Task;
        }
        var reihe = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var fenster = new Border();
        void Schliessen(int wahl)
        {
            ebene.Children.Remove(fenster);
            ebene.IsVisible = ebene.Children.Count > 0;
            Offen = ebene.Children.Count > 0 ? (Control)ebene.Children[^1] : null;
            fertig.TrySetResult(wahl);
        }
        for (var i = 0; i < knoepfe.Length; i++)
        {
            var nummer = i;
            var knopf = Schrift.Knopf(knoepfe[i], i == 0);
            knopf.Click += (_, _) => Schliessen(nummer);
            reihe.Children.Add(knopf);
        }
        vorbereiten?.Invoke([.. reihe.Children.OfType<Button>()]);
        var koerper = new StackPanel
        {
            Spacing = 10,
            Children = { Schrift.Text(titel, "Gross", FontWeight.Bold), inhalt, reihe },
        };
        var karte = Schrift.Karte(new ScrollViewer { Content = koerper });
        karte.MaxWidth = 560;
        karte.Margin = new Thickness(16);
        karte.HorizontalAlignment = HorizontalAlignment.Stretch;
        karte.VerticalAlignment = VerticalAlignment.Center;
        Avalonia.Automation.AutomationProperties.SetName(karte, titel);
        fenster.Child = karte;
        fenster.Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0));
        fenster.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Schliessen(-1);
            }
        };
        ebene.Children.Add(fenster);
        ebene.IsVisible = true;
        Offen = fenster;
        // Fokus auf den ersten Knopf (Eingabe/Leertaste bestaetigt), W4: den ersten freigegebenen
        Avalonia.Threading.Dispatcher.UIThread.Post(() => reihe.Children.FirstOrDefault(k => k.IsEnabled)?.Focus());
        return fertig.Task;
    }

    /// <summary>Alle offenen Fenster schliessen (Start ohne Bedienung).</summary>
    public static void AlleSchliessen()
    {
        if (_ebene is { } ebene)
        {
            foreach (var fenster in ebene.Children.OfType<Border>().ToList())
            {
                if (fenster.Child is Border karte && karte.Child is ScrollViewer { Content: StackPanel { Children: [.., WrapPanel reihe] } }
                    && reihe.Children.LastOrDefault() is Button knopf)
                {
                    knopf.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                }
            }
        }
    }
}
