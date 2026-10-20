using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L7: Kacheln nebeneinander, solange jede mindestens <c>mindestens</c> x Schriftfaktor
/// breit ist, sonst untereinander (Handy). Alle Spalten gleich breit, Zeilen so hoch wie die
/// hoechste Kachel der Zeile.
/// </summary>
public sealed class Spaltenraster(double mindestens, double abstand = 12) : Panel
{
    private int Spalten(double breite) =>
        double.IsInfinity(breite) ? Math.Max(1, Children.Count)
            : Math.Clamp((int)((breite + abstand) / (mindestens * Schrift.Faktor + abstand)), 1, Math.Max(1, Children.Count));

    protected override Size MeasureOverride(Size verfuegbar)
    {
        var spalten = Spalten(verfuegbar.Width);
        var breite = double.IsInfinity(verfuegbar.Width) ? mindestens * Schrift.Faktor : (verfuegbar.Width - abstand * (spalten - 1)) / spalten;
        double hoehe = 0, zeile = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(breite, double.PositiveInfinity));
            zeile = Math.Max(zeile, Children[i].DesiredSize.Height);
            if (i % spalten == spalten - 1 || i == Children.Count - 1)
            {
                hoehe += zeile + (hoehe > 0 ? abstand : 0);
                zeile = 0;
            }
        }
        return new Size(double.IsInfinity(verfuegbar.Width) ? breite * spalten + abstand * (spalten - 1) : verfuegbar.Width, hoehe);
    }

    protected override Size ArrangeOverride(Size endgroesse)
    {
        var spalten = Spalten(endgroesse.Width);
        var breite = (endgroesse.Width - abstand * (spalten - 1)) / spalten;
        double y = 0;
        for (var start = 0; start < Children.Count; start += spalten)
        {
            var zeile = Children.Skip(start).Take(spalten).ToList();
            var hoehe = zeile.Max(k => k.DesiredSize.Height);
            for (var i = 0; i < zeile.Count; i++)
            {
                zeile[i].Arrange(new Rect(i * (breite + abstand), y, breite, hoehe));
            }
            y += hoehe + abstand;
        }
        return endgroesse;
    }
}

/// <summary>
/// Phase 2, L7: Tabelle wie die Treeviews im Fortschritt (Kopfzeile, Zeilen mittig, hoechstens
/// <c>zeilen</c> Zeilen hoch, darin rollbar, Zeilen werden nur gebaut, wenn sie sichtbar sind).
/// Ist die Tabelle schmaler als 110 Punkte je Spalte x Schriftfaktor, steht jede Zeile als
/// kleiner Block untereinander ("Spalte: Wert"), damit nichts aus der Karte ragt.
/// </summary>
public sealed class Tabelle : UserControl
{
    private readonly IReadOnlyList<string> _spalten;
    private readonly int _zeilen;
    private IReadOnlyList<IReadOnlyList<string>> _werte = [];
    private bool? _breit;

    public Tabelle(IReadOnlyList<string> spalten, int zeilen)
    {
        _spalten = spalten;
        _zeilen = zeilen;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        SizeChanged += (_, e) => Aufbau(e.NewSize.Width);
        Schrift.Geaendert += SchriftGeaendert;
        DetachedFromVisualTree += (_, _) => Schrift.Geaendert -= SchriftGeaendert;
        AttachedToVisualTree += (_, _) =>
        {
            Schrift.Geaendert -= SchriftGeaendert;
            Schrift.Geaendert += SchriftGeaendert;
        };
    }

    public void Daten(IReadOnlyList<IReadOnlyList<string>> werte)
    {
        _werte = werte;
        _breit = null;
        Aufbau(Bounds.Width);
    }

    private void SchriftGeaendert()
    {
        _breit = null;
        Aufbau(Bounds.Width);
    }

    private void Aufbau(double breite)
    {
        var breit = breite <= 0 || breite >= 110 * _spalten.Count * Schrift.Faktor;
        if (_breit == breit && Content is not null)
        {
            return;
        }
        _breit = breit;
        var liste = new ItemsControl
        {
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<IReadOnlyList<string>>((zeile, _) => breit ? Zeile(zeile) : Block(zeile)),
            ItemsSource = _werte,
        };
        // hoechstens so viele Zeilen hoch wie in Python (height=9 bzw. 6), darin rollen
        var rollen = new ScrollViewer { Content = liste, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        rollen.Bind(MaxHeightProperty, rollen.GetResourceObservable("Schrift.Klein", v => v is double d ? _zeilen * (breit ? 2.6 : 2.6 * (_spalten.Count + 1)) * d : 400));
        var panel = new StackPanel { Spacing = 4 };
        if (breit)
        {
            panel.Children.Add(Kopf());
        }
        panel.Children.Add(rollen);
        Content = panel;
    }

    private Grid Raster()
    {
        var raster = new Grid { ColumnSpacing = 8 };
        for (var i = 0; i < _spalten.Count; i++)
        {
            // erste Spalte (Datum & Uhrzeit) breiter wie in Python (170 statt 90 bis 110)
            raster.ColumnDefinitions.Add(new ColumnDefinition(i == 0 ? 1.6 : 1, GridUnitType.Star));
        }
        return raster;
    }

    private Control Kopf()
    {
        var raster = Raster();
        for (var i = 0; i < _spalten.Count; i++)
        {
            var text = new WortText(_spalten[i], "Klein", FontWeight.Bold, "Pinsel.muted");
            text.TextAlignment = TextAlignment.Center;
            Grid.SetColumn(text, i);
            raster.Children.Add(text);
        }
        return new Border { Child = raster, Padding = new Thickness(0, 4), BorderThickness = new Thickness(0, 0, 0, 1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private Control Zeile(IReadOnlyList<string> zeile)
    {
        var raster = Raster();
        for (var i = 0; i < _spalten.Count; i++)
        {
            var text = new WortText(i < zeile.Count ? zeile[i] : "", "Klein", FontWeight.Normal, "Pinsel.text");
            text.TextAlignment = TextAlignment.Center;
            Grid.SetColumn(text, i);
            raster.Children.Add(text);
        }
        return new Border { Child = raster, Padding = new Thickness(0, 5), BorderThickness = new Thickness(0, 0, 0, 1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private Control Block(IReadOnlyList<string> zeile)
    {
        var block = new StackPanel { Spacing = 2 };
        block.Children.Add(Schrift.Text(zeile.Count > 0 ? zeile[0] : "", "Klein", FontWeight.Bold));
        for (var i = 1; i < _spalten.Count && i < zeile.Count; i++)
        {
            block.Children.Add(Schrift.Text(_spalten[i] + ": " + zeile[i], "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        }
        return new Border { Child = block, Padding = new Thickness(0, 6), BorderThickness = new Thickness(0, 0, 0, 1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }
}

/// <summary>
/// Phase 2, L7: Titel und Wert nebeneinander (Wert rechts), solange dem Titel mindestens
/// <c>mindestens</c> x Schriftfaktor Punkte bleiben; sonst steht der Wert unter dem Titel.
/// </summary>
public sealed class TitelUndWert(double mindestens, double abstand = 10) : Panel
{
    private bool Nebeneinander(double breite) =>
        Children.Count < 2 || double.IsInfinity(breite) || breite - Children[1].DesiredSize.Width - abstand >= mindestens * Schrift.Faktor;

    protected override Size MeasureOverride(Size verfuegbar)
    {
        if (Children.Count < 2)
        {
            Children.FirstOrDefault()?.Measure(verfuegbar);
            return Children.FirstOrDefault()?.DesiredSize ?? default;
        }
        Children[1].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var wert = Children[1].DesiredSize;
        if (Nebeneinander(verfuegbar.Width))
        {
            var rest = double.IsInfinity(verfuegbar.Width) ? double.PositiveInfinity : verfuegbar.Width - wert.Width - abstand;
            Children[0].Measure(new Size(rest, double.PositiveInfinity));
            var titel = Children[0].DesiredSize;
            return new Size(double.IsInfinity(verfuegbar.Width) ? titel.Width + abstand + wert.Width : verfuegbar.Width, Math.Max(titel.Height, wert.Height));
        }
        Children[0].Measure(new Size(verfuegbar.Width, double.PositiveInfinity));
        return new Size(verfuegbar.Width, Children[0].DesiredSize.Height + 2 + wert.Height);
    }

    protected override Size ArrangeOverride(Size endgroesse)
    {
        if (Children.Count < 2)
        {
            Children.FirstOrDefault()?.Arrange(new Rect(endgroesse));
            return endgroesse;
        }
        var wert = Children[1].DesiredSize;
        if (Nebeneinander(endgroesse.Width))
        {
            Children[0].Arrange(new Rect(0, 0, Math.Max(0, endgroesse.Width - wert.Width - abstand), endgroesse.Height));
            Children[1].Arrange(new Rect(endgroesse.Width - wert.Width, 0, wert.Width, wert.Height));
        }
        else
        {
            var titel = Children[0].DesiredSize.Height;
            Children[0].Arrange(new Rect(0, 0, endgroesse.Width, titel));
            Children[1].Arrange(new Rect(0, titel + 2, wert.Width, wert.Height));
        }
        return endgroesse;
    }
}

/// <summary>
/// Phase 2, L7: Text der Abdeckung mit sehr langen Woertern (z. B. "betriebsverfassungsrechtlichen").
/// Passt das laengste Wort in der eingestellten Schriftstufe nicht in die Zeile (Handy, sehr grosse
/// Schrift), wird nur dieser Text so weit verkleinert, dass das Wort ganz bleibt, nie unter die
/// Stufe Normal. Sonst wie <see cref="Schrift.Text"/>.
/// </summary>
public sealed class WortText : TextBlock
{
    private double _soll;

    public WortText(string text, string groesse, FontWeight gewicht, string farbe)
    {
        Text = text;
        FontWeight = gewicht;
        TextWrapping = TextWrapping.Wrap;
        _soll = Schrift.Grundgroessen[groesse];
        Untergrenze = _soll;
        this.Farbe(ForegroundProperty, farbe);
        this.GetResourceObservable("Schrift." + groesse).Subscribe(new Beobachter(v =>
        {
            if (v is double d)
            {
                _soll = d;
                FontSize = d;
                InvalidateMeasure();
            }
        }));
    }

    /// <summary>Kleinste Groesse (Stufe Normal dieser Schrift).</summary>
    public double Untergrenze { get; }

    protected override Size MeasureOverride(Size verfuegbar)
    {
        var groesse = _soll;
        if (!double.IsInfinity(verfuegbar.Width) && verfuegbar.Width > 0 && Text is { Length: > 0 } text)
        {
            var laengstes = text.Split(' ', ' ').Max(w => Breite(w, _soll));
            if (laengstes > verfuegbar.Width)
            {
                groesse = Math.Max(Untergrenze, Math.Floor(_soll * verfuegbar.Width / laengstes * 10) / 10 - 0.1);
            }
        }
        if (FontSize != groesse)
        {
            FontSize = groesse;
        }
        return base.MeasureOverride(verfuegbar);
    }

    private double Breite(string wort, double groesse) =>
        new FormattedText(wort, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight), groesse, null).WidthIncludingTrailingWhitespace;

    private sealed class Beobachter(Action<object?> weiter) : IObserver<object?>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(object? value) => weiter(value);
    }
}
