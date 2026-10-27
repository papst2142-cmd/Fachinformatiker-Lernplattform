using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2: Auswahl in Pillenform wie PillGroup in Python (genau eine aktiv). Bricht bei
/// wenig Platz in die naechste Zeile um; Beschriftungen werden nie mitten im Wort getrennt.
/// </summary>
public sealed class Pillen : WrapPanel
{
    private readonly Dictionary<string, Button> _knoepfe = new(StringComparer.Ordinal);
    private readonly Action<string> _geaendert;

    public Pillen(IEnumerable<(string Wert, string Text)> optionen, string gewaehlt, Action<string> geaendert)
    {
        _geaendert = geaendert;
        foreach (var (wert, text) in optionen)
        {
            var knopf = new Button
            {
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }.Groesse(),
                Padding = new Thickness(14, 6),
                Margin = new Thickness(0, 0, 8, 8),
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
            }.Farbe(Button.BorderBrushProperty, "Pinsel.border");
            Avalonia.Automation.AutomationProperties.SetName(knopf, text);
            knopf.Click += (_, _) =>
            {
                if (Wert != wert)
                {
                    Waehle(wert);
                    _geaendert(wert);
                }
            };
            _knoepfe[wert] = knopf;
            Children.Add(knopf);
        }
        Wert = gewaehlt;
        Waehle(gewaehlt);
    }

    public string Wert { get; private set; }

    /// <summary>Auswahl setzen, ohne zu melden (select_value(notify=False)).</summary>
    public void Waehle(string wert)
    {
        Wert = wert;
        foreach (var (kennung, knopf) in _knoepfe)
        {
            var aktiv = kennung == wert;
            // wie NeoButton kind="pill": aktiv Verlauf primary mit on_accent, sonst card_alt mit text_dim
            knopf.Farbe(Button.BackgroundProperty, aktiv ? "Verlauf.primary" : "Pinsel.card_alt");
            ((TextBlock)knopf.Content!).Farbe(TextBlock.ForegroundProperty, aktiv ? "Pinsel.on_accent" : "Pinsel.text_dim");
        }
    }
}

/// <summary>Kleine Hilfen fuer die Lernseiten.</summary>
public static class LernTeile
{
    /// <summary>Beschriftung ueber einer Auswahl (FACHBEREICH, STATUS ...).</summary>
    public static TextBlock Label(string text) => Schrift.Text(text, "Klein", FontWeight.Bold, "Pinsel.muted");

    /// <summary>
    /// Aufklappliste "Thema": lange Namen brechen um (auch in der geschlossenen Liste), damit
    /// sie am Handy ohne Abschneiden lesbar sind (Weisung P2-4).
    /// </summary>
    public static ComboBox Themenliste()
    {
        var liste = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 260,
            MaxWidth = 520,
            ItemTemplate = new FuncDataTemplate<string>((text, _) =>
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }.Groesse()),
        };
        return liste.Farbe(ComboBox.BackgroundProperty, "Pinsel.card_alt").Farbe(ComboBox.ForegroundProperty, "Pinsel.text");
    }

    /// <summary>Eine Antwort zum Auswaehlen wie OptionList: Zeile auf card_alt mit Rahmen.</summary>
    public static Border Option(string text, string gruppe)
    {
        var wahl = new RadioButton
        {
            GroupName = gruppe,
            // Fix A 0.14.2 (Weisung P8-9): sehr lange Woerter bleiben ganz (LernText)
            Content = new LernText(text, "Normal", FontWeight.Normal, "Pinsel.text_soft"),
            Tag = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Avalonia.Automation.AutomationProperties.SetName(wahl, text);
        return new Border
        {
            Child = wahl,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 4),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    /// <summary>Gewaehlte Antwort einer Liste aus <see cref="Option"/>.</summary>
    public static string? Gewaehlt(Panel liste) =>
        liste.Children.OfType<Border>().Select(b => b.Child).OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag as string;

    /// <summary>reveal: richtige Antwort gruen umrandet, falsch gewaehlte rot (wie OptionList.reveal).</summary>
    public static void Aufdecken(Panel liste, string? richtig)
    {
        foreach (var zeile in liste.Children.OfType<Border>())
        {
            if (zeile.Child is not RadioButton wahl)
            {
                continue;
            }
            if ((string?)wahl.Tag == richtig)
            {
                zeile.Farbe(Border.BorderBrushProperty, "Pinsel.green");
            }
            else if (wahl.IsChecked == true)
            {
                zeile.Farbe(Border.BorderBrushProperty, "Pinsel.red");
            }
        }
    }

    /// <summary>
    /// Knopf der Lernseiten (Schrift.Knopf); gesperrt mit Schrift "muted" wie NeoButton
    /// (sonst stuende weisse Schrift auf der hellgrauen Flaeche gesperrter Knoepfe).
    /// </summary>
    public static Button Knopf(string text, bool betont = false)
    {
        var knopf = Schrift.Knopf(text, betont);
        var beschriftung = (TextBlock)knopf.Content!;
        var farbe = betont ? "Pinsel.on_accent" : "Pinsel.text";
        knopf.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputElement.IsEnabledProperty)
            {
                beschriftung.Farbe(TextBlock.ForegroundProperty, knopf.IsEnabled ? farbe : "Pinsel.muted");
            }
        };
        return knopf;
    }

    /// <summary>Farbschluessel aus Core (green, red, fertig, gelb, rot, offen ...) als Pinsel.</summary>
    public static string Pinsel(string farbe) => farbe switch
    {
        "fertig" or "green" => "Pinsel.green",
        "rot" or "red" => "Pinsel.red",
        "gelb" or "yellow" => "Pinsel.yellow",
        "accent" => "Pinsel.accent",
        _ => "Pinsel.muted",
    };
}
