using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 3, W2 (Paket R6): Festbreitenschrift der Ergebniskaesten. JetBrains Mono 2.304 Regular
/// (SIL OFL 1.1, Weisung P3-4 Frage 1) liegt im Programm (Assets/Fonts), auf allen Systemen gleich.
/// </summary>
public static class Festbreite
{
    public static readonly FontFamily Schrift = new("avares://Lernplattform.App/Assets/Fonts#JetBrains Mono");

    /// <summary>Breite eines Zeichens ("0", wie box_chars in Python) bei dieser Schriftgroesse.</summary>
    public static double Zeichenbreite(double groesse) => Breite("0000000000", groesse) / 10;

    /// <summary>Breite eines Textes in der Festbreitenschrift.</summary>
    public static double Breite(string text, double groesse) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Schrift), groesse, null)
            .WidthIncludingTrailingWhitespace;
}

/// <summary>
/// Ergebniskasten der Rechner wie Python (PC SubnetCalc.put, Handy FitBox): merkt sich den
/// Rohtext und zeigt fit_lines(Rohtext, Zeichen) (Core <see cref="Zeilen.Kasten"/>). Die Zeichen
/// je Zeile folgen aus der Breite des Kastens und der Zeichenbreite der Festbreitenschrift; neu
/// gesetzt wird nur, wenn sich Rohtext, Zeichenzahl oder "einheitlich" (schmaler Aufbau) aendern.
/// Tabellen, die nicht umbrechen duerfen, lassen sich seitlich schieben.
/// W3 (R6-Nachtrag, Weisung P3-12): ist nach fit_lines noch eine Zeile laenger als der Kasten,
/// steht darunter "→ wischen" wie unter der FitBox am Handy (PC = Handy, Regel
/// <see cref="Wischhinweis.Noetig"/>).
/// </summary>
public sealed class Ergebniskasten : StackPanel
{
    private readonly Border _rahmen;
    private readonly SelectableTextBlock _text;
    private readonly ScrollViewer _rollen;
    private readonly TextBlock _hinweis;
    private string _roh;
    private int? _zeichen;
    private bool _einheitlich;

    public Ergebniskasten(string text = "")
    {
        Spacing = 4;
        _roh = text;
        _text = new SelectableTextBlock { FontFamily = Festbreite.Schrift, TextWrapping = TextWrapping.NoWrap, Text = text }
            .Groesse("Klein");
        _text.Farbe(TextBlock.ForegroundProperty, "Pinsel.text_dim");
        _rollen = new ScrollViewer
        {
            Content = _text,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _rahmen = new Border
        {
            Child = _rollen,
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
        };
        _rahmen.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
        // Festbreitenschrift wie im Kasten (wie Python: hat den Pfeil sicher), rechtsbuendig
        _hinweis = new TextBlock
        {
            Text = RechnerTexte.Wischen,
            FontFamily = Festbreite.Schrift,
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
        }.Groesse("Klein");
        _hinweis.Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
        Children.Add(_rahmen);
        Children.Add(_hinweis);
        _rollen.SizeChanged += (_, _) => Einpassen();
        _text.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.FontSizeProperty)
            {
                Einpassen();
            }
        };
    }

    /// <summary>Rohtext (vor dem Umbruch).</summary>
    public string Roh => _roh;

    /// <summary>Angezeigter Text (nach dem Umbruch).</summary>
    public string Angezeigt => _text.Text ?? "";

    /// <summary>Zeichen je Zeile beim letzten Umbruch (null, solange der Kasten keine Breite hat).</summary>
    public int? Zeichen => _zeichen;

    /// <summary>Steht "→ wischen" unter dem Kasten?</summary>
    public bool WischHinweis => _hinweis.IsVisible;

    /// <summary>Text setzen (Rohtext merken, umbrechen).</summary>
    public void Setzen(string text)
    {
        _roh = text;
        _zeichen = null;
        Einpassen();
    }

    /// <summary>Schmaler Aufbau: fit_lines(einheitlich=True) wie die FitBox am Handy.</summary>
    public bool Einheitlich
    {
        get => _einheitlich;
        set
        {
            if (_einheitlich != value)
            {
                _einheitlich = value;
                _zeichen = null;
                Einpassen();
            }
        }
    }

    private void Einpassen()
    {
        var breite = _rollen.Bounds.Width;
        if (breite <= 1)
        {
            _text.Text = _roh;
            _hinweis.IsVisible = false;
            return;
        }
        var zeichen = Math.Max(1, (int)Math.Floor((breite + 0.01) / Festbreite.Zeichenbreite(_text.FontSize)));
        if (zeichen == _zeichen)
        {
            return;
        }
        _zeichen = zeichen;
        _text.Text = Zeilen.Kasten(_roh, zeichen, _einheitlich);
        _hinweis.IsVisible = Wischhinweis.Noetig(_text.Text, zeichen);
    }
}

/// <summary>
/// Beschriftung und Eingabefeld in einer Zeile (wie Python "Beschriftung  [Feld]"), solange beides
/// nebeneinander passt; sonst steht die Beschriftung ueber dem Feld. So bricht kein Wort der
/// Beschriftung mitten durch, auch nicht bei grosser Schrift am Handy.
/// </summary>
public sealed class FeldZeile : Panel
{
    private const double Abstand = 12;
    private const double Untereinander = 6;
    private bool _nebeneinander;

    public FeldZeile(Control beschriftung, Control feld)
    {
        Children.Add(beschriftung);
        Children.Add(feld);
    }

    private Control Beschriftung => Children[0];
    private Control Feld => Children[1];

    protected override Size MeasureOverride(Size verfuegbar)
    {
        Feld.Measure(verfuegbar);
        Beschriftung.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var natuerlich = Beschriftung.DesiredSize.Width;
        _nebeneinander = natuerlich + Abstand + Feld.DesiredSize.Width <= verfuegbar.Width;
        if (_nebeneinander)
        {
            Beschriftung.Measure(new Size(verfuegbar.Width - Abstand - Feld.DesiredSize.Width, double.PositiveInfinity));
            return new Size(double.IsInfinity(verfuegbar.Width) ? natuerlich + Abstand + Feld.DesiredSize.Width : verfuegbar.Width,
                Math.Max(Beschriftung.DesiredSize.Height, Feld.DesiredSize.Height));
        }
        Beschriftung.Measure(new Size(verfuegbar.Width, double.PositiveInfinity));
        return new Size(verfuegbar.Width, Beschriftung.DesiredSize.Height + Untereinander + Feld.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size endgroesse)
    {
        var feld = Feld.DesiredSize;
        if (_nebeneinander)
        {
            var hoehe = Math.Max(Beschriftung.DesiredSize.Height, feld.Height);
            Beschriftung.Arrange(new Rect(0, (hoehe - Beschriftung.DesiredSize.Height) / 2,
                endgroesse.Width - Abstand - feld.Width, Beschriftung.DesiredSize.Height));
            Feld.Arrange(new Rect(endgroesse.Width - feld.Width, (hoehe - feld.Height) / 2, feld.Width, feld.Height));
            return endgroesse;
        }
        Beschriftung.Arrange(new Rect(0, 0, endgroesse.Width, Beschriftung.DesiredSize.Height));
        Feld.Arrange(new Rect(0, Beschriftung.DesiredSize.Height + Untereinander, Math.Min(feld.Width, endgroesse.Width), feld.Height));
        return endgroesse;
    }
}

/// <summary>
/// Haelt ein Feld mit Hoechstbreite am linken Rand: es fuellt die Breite bis zu seiner
/// Hoechstbreite und steht nicht mittig in der Karte (Stretch mit MaxWidth zentriert sonst).
/// </summary>
public sealed class LinksBuendig : Decorator
{
    protected override Size ArrangeOverride(Size endgroesse)
    {
        Child?.Arrange(new Rect(0, 0, Math.Min(endgroesse.Width, Child.MaxWidth), endgroesse.Height));
        return endgroesse;
    }
}

/// <summary>Kleine Hilfen der Rechner-Seite.</summary>
public static class RechnerTeile
{
    /// <summary>Hoechstbreite, die mit der Schrift waechst (Feldzeilen am breiten PC nicht ueber die ganze Karte).</summary>
    public static T HoechstBreite<T>(this T element, double basis) where T : Control
    {
        element.Bind(Layoutable.MaxWidthProperty, element.GetResourceObservable("Schrift.Normal",
            v => v is double d ? basis * d / Design.Schrift.Grundgroessen["Normal"] : basis));
        return element;
    }

    /// <summary>Eingabefeld der Rechner (Text, Farbe, Schrift; Zahlenfelder am Handy mit Zifferntastatur wie Python).</summary>
    public static TextBox Feld(string wert, string name, bool zahl = false, double breite = 0)
    {
        var feld = new TextBox { Text = wert }
            .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
        Avalonia.Automation.AutomationProperties.SetName(feld, name);
        if (zahl)
        {
            Avalonia.Input.TextInput.TextInputOptions.SetContentType(feld, Avalonia.Input.TextInput.TextInputContentType.Number);
        }
        if (breite > 0)
        {
            feld.Breite(breite);
        }
        else
        {
            feld.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        return feld;
    }

    /// <summary>Kleine Beschriftung ueber oder neben einem Feld (font small, text_dim).</summary>
    public static TextBlock Beschriftung(string text) => Design.Schrift.Text(text, "Klein", FontWeight.Normal, "Pinsel.text_dim");

    /// <summary>Feld mit Beschriftung daneben (oder darueber, wenn es nicht passt).</summary>
    public static FeldZeile Zeile(string beschriftung, Control feld) =>
        new FeldZeile(new WortText(beschriftung, "Klein", FontWeight.Normal, "Pinsel.text_dim"), feld);

    /// <summary>
    /// Karte mit Titel in Grossbuchstaben in der Farbe der Karte (Python Card: Strich, TITEL, Unterschrift).
    /// Die Unterschrift laesst sich spaeter aendern (set_subtitle).
    /// </summary>
    public static Border Karte(string titel, string farbe, TextBlock unter, Control inhalt) =>
        Design.Schrift.Karte(new StackPanel { Spacing = 12, Children = { Design.Schrift.KartenKopf(titel, unter, farbe), inhalt } });

    /// <summary>Unterschrift einer Karte (tiny, muted).</summary>
    public static TextBlock Unterschrift(string text) => Design.Schrift.Text(text, "Winzig", FontWeight.Normal, "Pinsel.muted");
}
