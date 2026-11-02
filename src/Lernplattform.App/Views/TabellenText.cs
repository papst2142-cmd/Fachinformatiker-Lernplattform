using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Sammelliste 177 (Antwort 177a, FW5a) und Teil 2 (Nico 02.11.2026 Antwort b, Weisung P11-12): Text der Aufgaben und
/// Musterloesungen mit eingerueckten Tabellen. Der Fliesstext steht wie bisher in <see cref="LernText"/>, jede Tabelle
/// (<see cref="Tabellenzeilen.Teile"/>) in einem <see cref="Tabellenkasten"/>: feste Zeichenbreite, nie umgebrochen.
/// Passt sie auch in der Stufe Klein nicht in die Breite (Handy, grosse Schrift), laesst sie sich seitlich wischen
/// und darunter steht "→ wischen" wie unter den Rechner-Ergebnissen. Regel nach der Breite, PC = Handy.
/// </summary>
public sealed class TabellenText : StackPanel
{
    private readonly string _groesse;
    private readonly FontWeight _gewicht;
    private readonly string _farbe;
    private string _text = "";

    public TabellenText(string text, string groesse, FontWeight gewicht, string farbe)
    {
        _groesse = groesse;
        _gewicht = gewicht;
        _farbe = farbe;
        Text = text;
    }

    /// <summary>Der ganze Text (Fliesstext und Tabellen).</summary>
    public string Text
    {
        get => _text;
        set
        {
            value ??= "";
            if (value == _text && Children.Count > 0)
            {
                return;
            }
            _text = value;
            Children.Clear();
            var teile = Tabellenzeilen.Teile(value);
            for (var i = 0; i < teile.Count; i++)
            {
                var (teil, tabelle) = teile[i];
                // ein leerer letzter Teil ist nur der Umbruch nach einer Tabelle am Textende
                if (!tabelle && teil.Length == 0 && i == teile.Count - 1 && i > 0)
                {
                    continue;
                }
                Children.Add(tabelle ? new Tabellenkasten(teil, _groesse, _gewicht, _farbe) : new LernText(teil, _groesse, _gewicht, _farbe));
            }
        }
    }

    /// <summary>Die Tabellen dieses Textes (Start ohne Bedienung, Bilder).</summary>
    public IEnumerable<Tabellenkasten> Tabellen => Children.OfType<Tabellenkasten>();
}

/// <summary>
/// Eine Tabelle aus <see cref="TabellenText"/>: Zeilen in <see cref="Festbreite.Schrift"/> ohne Umbruch, Groesse nach
/// <see cref="Tabellenzeilen.Schriftgroesse"/> (Text, kleiner bis zur Stufe Klein, sonst wischen mit Hinweis).
/// </summary>
public sealed class Tabellenkasten : StackPanel
{
    private readonly TextBlock _zeilen;
    private readonly ScrollViewer _rollen;
    private readonly TextBlock _hinweis;
    private readonly int _zeichen;
    private double _soll;

    public Tabellenkasten(string zeilen, string groesse, FontWeight gewicht, string farbe)
    {
        Spacing = 2;
        _soll = Schrift.Grundgroessen[groesse];
        _zeichen = zeilen.Split('\n').Max(z => z.TrimEnd().Length);
        _zeilen = new TextBlock
        {
            Text = zeilen,
            FontFamily = Festbreite.Schrift,
            FontWeight = gewicht,
            TextWrapping = TextWrapping.NoWrap,
            FontSize = _soll,
        };
        _zeilen.Farbe(TextBlock.ForegroundProperty, farbe);
        _rollen = new ScrollViewer
        {
            Content = _zeilen,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _hinweis = new TextBlock
        {
            Text = RechnerTexte.Wischen,
            FontFamily = Festbreite.Schrift,
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
        }.Groesse("Klein");
        _hinweis.Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
        Children.Add(_rollen);
        Children.Add(_hinweis);
        this.GetResourceObservable("Schrift." + groesse).Subscribe(new Beobachter(v =>
        {
            if (v is double d)
            {
                _soll = d;
                InvalidateMeasure();
            }
        }));
    }

    /// <summary>Die Zeilen der Tabelle.</summary>
    public TextBlock Zeilen => _zeilen;

    /// <summary>Der seitlich verschiebbare Bereich.</summary>
    public ScrollViewer Rollen => _rollen;

    /// <summary>Steht "→ wischen" unter der Tabelle?</summary>
    public bool WischHinweis => _hinweis.IsVisible;

    protected override Size MeasureOverride(Size verfuegbar)
    {
        var untergrenze = _soll * Schrift.Grundgroessen["Klein"] / Schrift.Grundgroessen["Normal"];
        var (groesse, wischen) = Tabellenzeilen.Schriftgroesse(_soll, untergrenze, _zeichen, Festbreite.Zeichenbreite(100) / 100, verfuegbar.Width);
        if (_zeilen.FontSize != groesse)
        {
            _zeilen.FontSize = groesse;
        }
        if (_hinweis.IsVisible != wischen)
        {
            _hinweis.IsVisible = wischen;
            // Platz unter der letzten Zeile fuer die waagrechte Scrollleiste (sonst liegt sie ueber der Zeile)
            _zeilen.Padding = wischen ? new Thickness(0, 0, 0, Dialog.RandFuerLeiste) : default;
        }
        return base.MeasureOverride(verfuegbar);
    }

    private sealed class Beobachter(Action<object?> weiter) : IObserver<object?>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(object? value) => weiter(value);
    }
}
