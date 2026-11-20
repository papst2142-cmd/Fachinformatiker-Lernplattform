using Lernplattform.Core.Spiel;
using Lernplattform.Core.Design;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.99.0 (Sammelliste 400, Weisungen P12-319/P12-320, Nico 1a 2a 3b): am Handy und Tablet zwei runde Knoepfe am rechten
/// Daumen, ueber dem Bereich des rechten Sticks (<see cref="HaltungKnopf.Lage"/>): gross „Springen“ (Pfeil nach oben), klein
/// „Haltung“ (Symbol der Haltung: stehend, geduckt, kriechend). Haltung: tippen = ducken, lang druecken = kriechen, in der Haltung
/// tippen = aufstehen (<see cref="HaltungKnopf.Aufgabe"/>). In Ducken und Kriechen leuchtet der Knopf in der Grundfarbe des Programms,
/// das Symbol ist darauf schwarz oder weiss (was mehr Kontrast hat). Rund, leicht durchsichtig, ohne Schrift. Sichtbar nur, solange
/// die Figur Haltungen kann; die drei Eintraege im Dreieck-Menue entfallen am Handy und Tablet (am PC bleiben sie mit den Tasten).
/// Screenreader-Namen wie die Menue-Eintraege („Springen“, „Ducken“, „Aufstehen“).
/// </summary>
public sealed partial class SpielSeite
{
    private readonly Canvas _daumenEbene = new() { ClipToBounds = true };
    private DaumenKnopf? _springenRund;
    private DaumenKnopf? _haltungRund;

    /// <summary>Start-Test und Fotos: die runden Knoepfe auch am PC (null = nur Android).</summary>
    internal bool? DaumenKnoepfeFuerTests { get; set; }

    /// <summary>Am Handy und Tablet gibt es die runden Knoepfe statt der drei Menue-Eintraege.</summary>
    public bool DaumenKnoepfeAn => DaumenKnoepfeFuerTests ?? OperatingSystem.IsAndroid();

    /// <summary>Start-Test: die zwei runden Knoepfe (Springen, Haltung).</summary>
    public (DaumenKnopf Springen, DaumenKnopf Haltung) DaumenKnoepfe => (_springenRund!, _haltungRund!);

    /// <summary>Start-Test: Rechteck eines sichtbaren runden Knopfs in Punkten der Spielflaeche (null = unsichtbar).</summary>
    public Rect? DaumenRechteck(DaumenKnopf knopf) =>
        knopf.IsVisible && Flaeche.TranslatePoint(default, this) is { } f0
            ? new Rect(Canvas.GetLeft(knopf) - f0.X, Canvas.GetTop(knopf) - f0.Y, knopf.Width, knopf.Height) : null;

    private void DaumenKnoepfeAnlegen()
    {
        _springenRund = new DaumenKnopf(DaumenSymbol.Pfeil);
        _springenRund.Getippt += () => HaltungAusfuehren(Tastenbelegung.Springen);
        _haltungRund = new DaumenKnopf(DaumenSymbol.Stehen);
        _haltungRund.Getippt += () => HaltungRundAusfuehren(lang: false);
        _haltungRund.Lang += () => HaltungRundAusfuehren(lang: true);
        _daumenEbene.Children.Add(_springenRund);
        _daumenEbene.Children.Add(_haltungRund);
        Children.Add(_daumenEbene);
    }

    private void HaltungRundAusfuehren(bool lang)
    {
        if (Spiel is { } spiel && HaltungKnopf.Aufgabe(spiel.Koerper, lang) is { } aufgabe)
        {
            HaltungAusfuehren(aufgabe);
        }
    }

    /// <summary>Lage, Sichtbarkeit, Symbol und Leuchten der runden Knoepfe (nach jeder Aenderung des Spiels und der Groesse).</summary>
    private void DaumenSetzen()
    {
        if (_springenRund is null || _haltungRund is null)
        {
            return;
        }
        var lage = HaltungKnopf.Lage(Flaeche.StickBereich);
        var an = DaumenKnoepfeAn && Flaeche.GlBereit && !_ersatz.IsVisible && Spiel is { KoerperMoeglich: true } && Ebene.IsVisible
            && !HandyOffen && !(Tafel?.IsVisible ?? false) && lage is not null && Flaeche.TranslatePoint(default, this) is not null;
        _springenRund.IsVisible = an;
        _haltungRund.IsVisible = an;
        if (!an || lage is not { } l || Flaeche.TranslatePoint(default, this) is not { } f0)
        {
            return;
        }
        var koerper = Spiel?.Koerper ?? Koerperhaltung.Stehen;
        var (g, k) = HaltungKnopf.Groesse(Flaeche.StickBereich.Breite, Flaeche.StickBereich.Hoehe);
        _springenRund.Setzen(g, DaumenSymbol.Pfeil, false, SpielProbeTexte.KnopfSpringen);
        _haltungRund.Setzen(k, koerper switch
        {
            Koerperhaltung.Ducken => DaumenSymbol.Ducken,
            Koerperhaltung.Kriechen => DaumenSymbol.Kriechen,
            _ => DaumenSymbol.Stehen,
        }, koerper != Koerperhaltung.Stehen, koerper == Koerperhaltung.Stehen ? SpielProbeTexte.KnopfDucken : SpielProbeTexte.KnopfAufstehen);
        foreach (var (knopf, (x, y), d) in new[] { (_springenRund, l.Springen, g), (_haltungRund, l.Haltung, k) })
        {
            Canvas.SetLeft(knopf, f0.X + x - (d / 2));
            Canvas.SetTop(knopf, f0.Y + y - (d / 2));
            // sofort an die neue Stelle (Sammelliste 162)
            knopf.Arrange(new Rect(Canvas.GetLeft(knopf), Canvas.GetTop(knopf), d, d));
        }
    }
}

/// <summary>Symbol eines runden Knopfs am rechten Daumen.</summary>
public enum DaumenSymbol
{
    Pfeil,
    Stehen,
    Ducken,
    Kriechen,
}

/// <summary>
/// Runder Knopf am rechten Daumen (Welle A 0.99.0): antippbare Flaeche (<see cref="Klickflaeche"/>, Screenreader: Schaltflaeche,
/// Invoke = tippen) mit kurzem Tippen und langem Druck (<see cref="HaltungKnopf.LangAb"/>). Das Symbol ist mit Linien gezeichnet
/// (Figur aus Kopf, Rumpf, Armen und Beinen), keine Schrift.
/// </summary>
public sealed class DaumenKnopf : Klickflaeche
{
    private readonly Ellipse _fuellung = new();
    private readonly Ellipse _rand = new() { StrokeThickness = 1.5 };
    private readonly Canvas _symbol = new() { Width = 24, Height = 24 };
    private readonly DispatcherTimer _lang = new() { Interval = TimeSpan.FromSeconds(HaltungKnopf.LangAb) };
    private bool _langAusgeloest;
    private DaumenSymbol? _gezeichnet;
    private bool? _leuchtet;
    private string? _symbolFarbe;
    private string? _akzentHex;

    /// <summary>Kurz getippt (oder Screenreader).</summary>
    public event Action? Getippt;

    /// <summary>Lang gedrueckt (ausgeloest, sobald die Zeit um ist; das Loslassen danach tippt nicht mehr).</summary>
    public event Action? Lang;

    /// <summary>Start-Test: das Symbol, das der Knopf gerade zeigt.</summary>
    public DaumenSymbol Symbol { get; private set; }

    /// <summary>Start-Test: der Knopf leuchtet (Haltung gilt).</summary>
    public bool Leuchtet => _leuchtet == true;

    /// <summary>Start-Test: Farbe des Symbols (Hex) und der Fuellung beim Leuchten.</summary>
    public (string Symbol, string? Fuellung) Farben { get; private set; } = ("", null);

    public DaumenKnopf(DaumenSymbol symbol)
    {
        Background = Brushes.Transparent;
        IsVisible = false;
        var raster = new Grid();
        raster.Children.Add(_fuellung);
        raster.Children.Add(_rand);
        raster.Children.Add(new Viewbox { Child = _symbol, Margin = new Thickness(6) });
        Child = raster;
        Klick = () => Getippt?.Invoke();
        _lang.Tick += (_, _) =>
        {
            _lang.Stop();
            _langAusgeloest = true;
            Lang?.Invoke();
        };
        Symbol = symbol;
    }

    /// <summary>Groesse, Symbol, Leuchten und Name setzen (zeichnet nur bei einer Aenderung neu).</summary>
    public void Setzen(double durchmesser, DaumenSymbol symbol, bool leuchtet, string name)
    {
        Width = Height = durchmesser;
        CornerRadius = new CornerRadius(durchmesser / 2);
        Avalonia.Automation.AutomationProperties.SetName(this, name);
        var app = Application.Current;
        var akzent = app?.TryFindResource("Pinsel.accent", out var a) == true && a is ISolidColorBrush ab ? ab.Color : Colors.DeepSkyBlue;
        var akzentHex = $"#{akzent.R:X2}{akzent.G:X2}{akzent.B:X2}";
        var symbolFarbe = leuchtet ? Symbolfarbe.Auf(akzentHex) : null;
        if (_leuchtet != leuchtet || _symbolFarbe != symbolFarbe || _gezeichnet != symbol || (leuchtet && _akzentHex != akzentHex))
        {
            _leuchtet = leuchtet;
            _akzentHex = akzentHex;
            _symbolFarbe = symbolFarbe;
            if (leuchtet)
            {
                // Grundfarbe des Programms (Nico 3b), fast deckend, mit leichtem Schein
                _fuellung.Fill = new SolidColorBrush(akzent, 0.9);
                _rand.Stroke = new SolidColorBrush(Color.Parse(symbolFarbe!), 0.7);
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 14, Spread = 2, Color = Color.FromArgb(160, akzent.R, akzent.G, akzent.B) });
            }
            else
            {
                // ruhig: Kartenfarbe halb durchsichtig, Rand und Symbol in der Textfarbe (wie die Sticks)
                _fuellung.Bind(Shape.FillProperty, _fuellung.GetResourceObservable("Pinsel.card"));
                _fuellung.Opacity = 0.5;
                _rand.Bind(Shape.StrokeProperty, _rand.GetResourceObservable("Pinsel.text"));
                BoxShadow = default;
            }
            _fuellung.Opacity = leuchtet ? 1 : 0.5;
            _rand.Opacity = leuchtet ? 1 : 0.55;
            Zeichnen(symbol, symbolFarbe);
            var textFarbe = app?.TryFindResource("Pinsel.text", out var t) == true && t is ISolidColorBrush tb ? tb.Color : Colors.White;
            Farben = (symbolFarbe ?? $"#{textFarbe.R:X2}{textFarbe.G:X2}{textFarbe.B:X2}", leuchtet ? akzentHex : null);
        }
    }

    private void Zeichnen(DaumenSymbol symbol, string? farbe)
    {
        _gezeichnet = symbol;
        Symbol = symbol;
        _symbol.Children.Clear();
        IBrush? pinsel = farbe is null ? null : new SolidColorBrush(Color.Parse(farbe));
        void Linie(string daten, double dicke = 2.4)
        {
            var pfad = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(daten),
                StrokeThickness = dicke,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
            };
            if (pinsel is null)
            {
                pfad.Bind(Shape.StrokeProperty, pfad.GetResourceObservable("Pinsel.text"));
            }
            else
            {
                pfad.Stroke = pinsel;
            }
            _symbol.Children.Add(pfad);
        }
        void Kopf(double x, double y, double r)
        {
            var kopf = new Ellipse { Width = 2 * r, Height = 2 * r };
            if (pinsel is null)
            {
                kopf.Bind(Shape.FillProperty, kopf.GetResourceObservable("Pinsel.text"));
            }
            else
            {
                kopf.Fill = pinsel;
            }
            Canvas.SetLeft(kopf, x - r);
            Canvas.SetTop(kopf, y - r);
            _symbol.Children.Add(kopf);
        }
        switch (symbol)
        {
            case DaumenSymbol.Pfeil:
                Linie("M12,20 L12,6", 3);
                Linie("M5.5,11.5 L12,4.5 L18.5,11.5", 3);
                break;
            case DaumenSymbol.Stehen:
                Kopf(12, 3.8, 2.4);
                Linie("M12,7.5 L12,14.5");
                Linie("M7,11.5 L12,9 L17,11.5");
                Linie("M12,14.5 L8.5,21.5 M12,14.5 L15.5,21.5");
                break;
            case DaumenSymbol.Ducken:
                Kopf(13, 6.5, 2.4);
                Linie("M12.5,9.5 L10.5,15.5");
                Linie("M12,11 L17,13.5");
                Linie("M10.5,15.5 L16,16.5 L16,21.5 M10.5,15.5 L7.5,21.5");
                break;
            default:
                // kriechend (Chat-Hinweis P12-320: groesser und klar, Kopf, Arme und Beine deutlich): Kopf vorn rechts, Ruecken
                // waagerecht, zwei Arme senkrecht zum Boden, zwei Beine mit Knie am Boden
                Kopf(20, 8.5, 2.6);
                Linie("M17.5,10.5 L6,11", 2.6);
                Linie("M16.5,10.8 L16,19.5 M14.5,10.9 L13,19.5", 2.4);
                Linie("M6,11 L8.5,18 L4,19.5 M7.5,11 L10.5,18.5", 2.4);
                break;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _langAusgeloest = false;
        _lang.Stop();
        _lang.Start();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _lang.Stop();
        var drauf = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        if (!_langAusgeloest && drauf)
        {
            Getippt?.Invoke();
        }
        _langAusgeloest = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _lang.Stop();
    }

    /// <summary>Start-Test: langen Druck ausloesen wie nach Ablauf der Zeit.</summary>
    public void LangFuerTests() => Lang?.Invoke();
}
