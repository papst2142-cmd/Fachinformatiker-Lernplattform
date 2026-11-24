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
    private DaumenKnopf? _liegenRund;
    private readonly Dictionary<HaltungKnopf.Knopf, Border> _tastenSchilder = [];

    /// <summary>Start-Test und Fotos: die runden Knoepfe auch am PC (null = nur Android).</summary>
    internal bool? DaumenKnoepfeFuerTests { get; set; }

    /// <summary>Am Handy und Tablet gibt es die runden Knoepfe statt der drei Menue-Eintraege.</summary>
    public bool DaumenKnoepfeAn => DaumenKnoepfeFuerTests ?? OperatingSystem.IsAndroid();

    /// <summary>
    /// Start-Test: die runden Knoepfe (Springen, Haltung = seit A 1.7.0 Hinknien, Liegen = Hinlegen). Am PC sind es seit A 1.7.0
    /// dieselben Knoepfe als kleine Symbole mit der Taste darunter (Sammelliste 447, Nico 4a).
    /// </summary>
    public (DaumenKnopf Springen, DaumenKnopf Haltung, DaumenKnopf Liegen) DaumenKnoepfe => (_springenRund!, _haltungRund!, _liegenRund!);

    /// <summary>Start-Test: Text und Rechteck (Punkte der Spielflaeche) des Tastenschilds unter einem Symbol am PC (null = unsichtbar).</summary>
    public (string Text, Rect Lage)? TastenSchild(HaltungKnopf.Knopf knopf) =>
        _tastenSchilder.TryGetValue(knopf, out var b) && b.IsVisible && b.Child is TextBlock t && Flaeche.TranslatePoint(default, this) is { } f0
            ? (t.Text ?? "", new Rect(Canvas.GetLeft(b) - f0.X, Canvas.GetTop(b) - f0.Y, b.Bounds.Width, b.Bounds.Height)) : null;

    /// <summary>Start-Test: Rechteck eines sichtbaren runden Knopfs in Punkten der Spielflaeche (null = unsichtbar).</summary>
    public Rect? DaumenRechteck(DaumenKnopf knopf) =>
        knopf.IsVisible && Flaeche.TranslatePoint(default, this) is { } f0
            ? new Rect(Canvas.GetLeft(knopf) - f0.X, Canvas.GetTop(knopf) - f0.Y, knopf.Width, knopf.Height) : null;

    private void DaumenKnoepfeAnlegen()
    {
        _springenRund = new DaumenKnopf(DaumenSymbol.Pfeil);
        _springenRund.Getippt += () => HaltungAusfuehren(Tastenbelegung.Springen);
        _haltungRund = new DaumenKnopf(DaumenSymbol.Stehen) { WischNachUnten = true };
        _haltungRund.Getippt += () => HaltungRundAusfuehren(HaltungKnopf.Knopf.Knien, lang: false);
        _haltungRund.Lang += () => HaltungRundAusfuehren(HaltungKnopf.Knopf.Knien, lang: true);
        _liegenRund = new DaumenKnopf(DaumenSymbol.Liegen);
        _liegenRund.Getippt += () => HaltungRundAusfuehren(HaltungKnopf.Knopf.Liegen, lang: false);
        _liegenRund.Lang += () => HaltungRundAusfuehren(HaltungKnopf.Knopf.Liegen, lang: true);
        _daumenEbene.Children.Add(_springenRund);
        _daumenEbene.Children.Add(_haltungRund);
        _daumenEbene.Children.Add(_liegenRund);
        foreach (var knopf in new[] { HaltungKnopf.Knopf.Springen, HaltungKnopf.Knopf.Knien, HaltungKnopf.Knopf.Liegen })
        {
            // Welle A 1.7.0 (Nico 4a): am PC steht unter jedem Symbol seine Taste (nur Anzeige, nicht antippbar)
            var schild = new Border
            {
                IsVisible = false,
                IsHitTestVisible = false,
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 1, 6, 1),
                Child = new TextBlock { FontSize = 12, FontWeight = FontWeight.Bold },
            };
            schild.Bind(Border.BackgroundProperty, schild.GetResourceObservable("Pinsel.card"));
            schild.Bind(Border.BorderBrushProperty, schild.GetResourceObservable("Pinsel.text"));
            ((TextBlock)schild.Child).Bind(TextBlock.ForegroundProperty, schild.GetResourceObservable("Pinsel.text"));
            _tastenSchilder[knopf] = schild;
            _daumenEbene.Children.Add(schild);
        }
        Children.Add(_daumenEbene);
    }

    private void HaltungRundAusfuehren(HaltungKnopf.Knopf knopf, bool lang)
    {
        if (Spiel is not { } spiel)
        {
            return;
        }
        // Gegenprobe bis A 1.6.0 (zwei Knoepfe): der eine Knopf „Haltung“
        var aufgabe = HaltungKnopf.Lage(Flaeche.StickBereich) is { Liegen: null } && DaumenKnoepfeAn
            ? HaltungKnopf.Aufgabe(spiel.Koerper, lang) : HaltungKnopf.Aufgabe(knopf, spiel.Koerper, lang);
        if (aufgabe is not null)
        {
            HaltungAusfuehren(aufgabe);
        }
    }

    /// <summary>Lage, Sichtbarkeit, Symbol und Leuchten der runden Knoepfe (nach jeder Aenderung des Spiels und der Groesse).</summary>
    private void DaumenSetzen()
    {
        HandlungKnoepfeLegen();
        if (_springenRund is null || _haltungRund is null || _liegenRund is null)
        {
            return;
        }
        // Welle A 1.7.0 (Sammelliste 447): Handy und Tablet drei runde Knoepfe im Bogen am rechten Daumen (Nico 1a), am PC drei kleine
        // Symbole unten rechts mit ihrer Taste (Nico 4a)
        var handy = DaumenKnoepfeAn;
        var bereich = Flaeche.StickBereich;
        var lage = handy ? HaltungKnopf.Lage(bereich) : HaltungKnopf.LageTasten(bereich);
        var an = Flaeche.GlBereit && !_ersatz.IsVisible && Spiel is { KoerperMoeglich: true } && Ebene.IsVisible
            && !HandyOffen && !(Tafel?.IsVisible ?? false) && lage is not null && Flaeche.TranslatePoint(default, this) is not null;
        var drei = lage is { Liegen: not null };
        _springenRund.IsVisible = an;
        _haltungRund.IsVisible = an;
        _liegenRund.IsVisible = an && drei;
        foreach (var (art0, schild) in _tastenSchilder)
        {
            schild.IsVisible = an && !handy && (drei || art0 != HaltungKnopf.Knopf.Liegen);
        }
        if (!an || lage is not { } l || Flaeche.TranslatePoint(default, this) is not { } f0)
        {
            return;
        }
        var koerper = Spiel?.Koerper ?? Koerperhaltung.Stehen;
        var (g, k) = handy ? HaltungKnopf.Groessen(bereich.Breite, bereich.Hoehe) : HaltungKnopf.GroessenTasten;
        var knoepfe = new List<(HaltungKnopf.Knopf Art, DaumenKnopf Knopf, (double X, double Y) Mitte, double D)>
        {
            (HaltungKnopf.Knopf.Springen, _springenRund, l.Springen, g),
            (HaltungKnopf.Knopf.Knien, _haltungRund, l.Knien, k),
        };
        if (!drei)
        {
            // Gegenprobe bis A 1.6.0: Pfeil und ein Knopf „Haltung“ mit Strichfiguren
            _springenRund.Setzen(g, DaumenSymbol.Pfeil, false, SpielProbeTexte.KnopfSpringen);
            _haltungRund.Setzen(k, koerper switch
            {
                Koerperhaltung.Ducken => DaumenSymbol.Ducken,
                Koerperhaltung.Kriechen => DaumenSymbol.Kriechen,
                _ => DaumenSymbol.Stehen,
            }, koerper != Koerperhaltung.Stehen, koerper == Koerperhaltung.Stehen ? SpielProbeTexte.KnopfDucken : SpielProbeTexte.KnopfAufstehen);
        }
        else
        {
            // Konzept Punkt 4 und 5: weisse Figuren auf halb durchsichtigen dunklen Scheiben; das Symbol zeigt die naechste Haltung,
            // die aktuelle leuchtet in der Grundfarbe des Programms
            knoepfe.Add((HaltungKnopf.Knopf.Liegen, _liegenRund, l.Liegen!.Value, k));
            _springenRund.Setzen(g, DaumenSymbol.Springen, false, SpielProbeTexte.KnopfSpringen, scheibe: true);
            var (knienAuf, knienLeuchtet) = HaltungKnopf.Zustand(HaltungKnopf.Knopf.Knien, koerper);
            _haltungRund.Setzen(k, knienAuf ? DaumenSymbol.Aufstehen : DaumenSymbol.Knien, knienLeuchtet,
                knienAuf ? SpielProbeTexte.KnopfAufstehen : SpielProbeTexte.KnopfHinknien, scheibe: true);
            var (liegenAuf, liegenLeuchtet) = HaltungKnopf.Zustand(HaltungKnopf.Knopf.Liegen, koerper);
            _liegenRund.Setzen(k, liegenAuf ? DaumenSymbol.Aufstehen : DaumenSymbol.Liegen, liegenLeuchtet,
                liegenAuf ? SpielProbeTexte.KnopfAufstehen : SpielProbeTexte.KnopfHinlegen, scheibe: true);
        }
        foreach (var (art, knopf, (x, y), d) in knoepfe)
        {
            Canvas.SetLeft(knopf, f0.X + x - (d / 2));
            Canvas.SetTop(knopf, f0.Y + y - (d / 2));
            // sofort an die neue Stelle (Sammelliste 162)
            knopf.Arrange(new Rect(Canvas.GetLeft(knopf), Canvas.GetTop(knopf), d, d));
            if (!handy && _tastenSchilder.TryGetValue(art, out var schild) && schild.Child is TextBlock text)
            {
                var aufgabe = art switch
                {
                    HaltungKnopf.Knopf.Springen => Tastenbelegung.Springen,
                    HaltungKnopf.Knopf.Knien => Tastenbelegung.Ducken,
                    _ => Tastenbelegung.Kriechen,
                };
                var taste = Tastenbelegung.Aktuell.Tasten(aufgabe).Count > 0 ? Tastenbelegung.Aktuell.AnzeigeFuer(aufgabe) : "–";
                if (text.Text != taste)
                {
                    text.Text = taste;
                }
                schild.Measure(Size.Infinity);
                var w = schild.DesiredSize.Width;
                Canvas.SetLeft(schild, f0.X + x - (w / 2));
                Canvas.SetTop(schild, f0.Y + y + (d / 2) + 4);
                schild.Arrange(new Rect(Canvas.GetLeft(schild), Canvas.GetTop(schild), w, schild.DesiredSize.Height));
            }
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

    /// <summary>Welle A 1.6.0 (Sammelliste 444): Handlung an einer Tuer.</summary>
    Tuer,

    /// <summary>Welle A 1.6.0: Handlung an Bus oder Zug.</summary>
    Bus,

    /// <summary>Welle A 1.6.0: Handlung an einem Gegenstand oder einer Person.</summary>
    Hand,

    /// <summary>Welle A 1.6.0: Ich-Sicht an und aus.</summary>
    Auge,

    /// <summary>Welle A 1.6.0 (Weisung P12-361): das Spiel-Handy.</summary>
    Telefon,

    /// <summary>Welle A 1.7.0 (Sammelliste 447): springende Figur mit Sprungbogen.</summary>
    Springen,

    /// <summary>Welle A 1.7.0: kniende Figur auf dem Boden (Hinknien).</summary>
    Knien,

    /// <summary>Welle A 1.7.0: flach liegende Figur auf dem Boden (Hinlegen).</summary>
    Liegen,

    /// <summary>Welle A 1.7.0: stehende Figur (in der Haltung: aufstehen).</summary>
    Aufstehen,
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
    private bool? _scheibe;
    private Viewbox? _rahmen;
    private Point? _start;

    /// <summary>
    /// Welle A 1.7.0 (Sammelliste 447, Nico 2a): nach unten wischen (ab <see cref="HaltungKnopf.WischAb"/> Punkten, mehr nach unten
    /// als zur Seite) loest wie langes Druecken <see cref="Lang"/> aus (Hinknien: gleich hinlegen).
    /// </summary>
    public bool WischNachUnten { get; init; }

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
        _rahmen = new Viewbox { Child = _symbol, Margin = new Thickness(6) };
        raster.Children.Add(_rahmen);
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
    /// <remarks>
    /// <paramref name="scheibe"/> (Welle A 1.7.0, Sammelliste 447, Konzept Punkt 5): weisse Figur auf halb durchsichtiger dunkler
    /// Scheibe mit hellem Rand (Abnahmebild P12-231 Ordner 06), das Symbol nimmt zwei Drittel des Kreises ein; leuchtend wie bisher
    /// in der Grundfarbe mit Symbol schwarz oder weiss (<see cref="Symbolfarbe.Auf"/>).
    /// </remarks>
    public void Setzen(double durchmesser, DaumenSymbol symbol, bool leuchtet, string name, bool scheibe = false)
    {
        Width = Height = durchmesser;
        if (_rahmen is { } rahmen)
        {
            var rand = scheibe ? durchmesser * 0.17 : 6;
            if (Math.Abs(rahmen.Margin.Left - rand) > 1e-9)
            {
                rahmen.Margin = new Thickness(rand);
            }
        }
        CornerRadius = new CornerRadius(durchmesser / 2);
        Avalonia.Automation.AutomationProperties.SetName(this, name);
        var app = Application.Current;
        var akzent = app?.TryFindResource("Pinsel.accent", out var a) == true && a is ISolidColorBrush ab ? ab.Color : Colors.DeepSkyBlue;
        var akzentHex = $"#{akzent.R:X2}{akzent.G:X2}{akzent.B:X2}";
        var symbolFarbe = leuchtet ? Symbolfarbe.Auf(akzentHex) : null;
        if (_leuchtet != leuchtet || _symbolFarbe != symbolFarbe || _gezeichnet != symbol || (leuchtet && _akzentHex != akzentHex) || _scheibe != scheibe)
        {
            _scheibe = scheibe;
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
            else if (scheibe)
            {
                // Welle A 1.7.0: dunkle Scheibe (Deckkraft 0,47), weisser Rand (0,59), weisses Symbol
                _fuellung.Fill = new SolidColorBrush(Color.FromRgb(0x18, 0x20, 0x28));
                _rand.Stroke = Brushes.White;
                BoxShadow = default;
                symbolFarbe = "#FFFFFF";
            }
            else
            {
                // ruhig: Kartenfarbe halb durchsichtig, Rand und Symbol in der Textfarbe (wie die Sticks)
                _fuellung.Bind(Shape.FillProperty, _fuellung.GetResourceObservable("Pinsel.card"));
                _fuellung.Opacity = 0.5;
                _rand.Bind(Shape.StrokeProperty, _rand.GetResourceObservable("Pinsel.text"));
                BoxShadow = default;
            }
            _fuellung.Opacity = leuchtet ? 1 : scheibe ? 0.47 : 0.5;
            _rand.Opacity = leuchtet ? 1 : scheibe ? 0.59 : 0.55;
            _rand.StrokeThickness = scheibe ? Math.Max(1, durchmesser * 0.03) : 1.5;
            Zeichnen(symbol, symbolFarbe);
            var textFarbe = app?.TryFindResource("Pinsel.text", out var t) == true && t is ISolidColorBrush tb ? tb.Color : Colors.White;
            Farben = (symbolFarbe ?? $"#{textFarbe.R:X2}{textFarbe.G:X2}{textFarbe.B:X2}", leuchtet ? akzentHex : null);
        }
    }

    private string? _zeichenFarbe;

    private void Zeichnen(DaumenSymbol symbol, string? farbe)
    {
        _zeichenFarbe = farbe;
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
            case DaumenSymbol.Tuer:
                // Welle A 1.6.0 (Sammelliste 444): Tuer mit Rahmen, Klinke und Boden
                Linie("M3,21.5 L21,21.5", 2);
                Linie("M6.5,21.5 L6.5,2.5 L17.5,2.5 L17.5,21.5");
                Linie("M9.5,21.5 L9.5,5.5 L17.5,5.5", 1.6);
                Kopf(14.8, 12.8, 1.4);
                break;
            case DaumenSymbol.Bus:
                Linie("M5,3.5 L19,3.5 Q20.5,3.5 20.5,5 L20.5,17.5 L3.5,17.5 L3.5,5 Q3.5,3.5 5,3.5 Z");
                Linie("M3.5,10.5 L20.5,10.5", 2);
                Linie("M12,3.5 L12,10.5", 1.6);
                Kopf(7.5, 19.8, 1.9);
                Kopf(16.5, 19.8, 1.9);
                break;
            case DaumenSymbol.Hand:
                // offene Hand: Handflaeche und vier Finger, Daumen seitlich
                Linie("M8,21 L8,13 Q8,11 6.5,10 L4.5,8.5", 2.2);
                Linie("M8,13 L8,4.5 M11,12 L11,3 M14,12 L14,4 M17,13 L17,6.5", 2.2);
                Linie("M17,13 L17,16 Q17,21 12.5,21 L8,21", 2.2);
                break;
            case DaumenSymbol.Telefon:
                // Welle A 1.6.0 (Weisung P12-361): Handy hochkant mit Bildschirm und Knopf
                Linie("M8.5,2.5 L15.5,2.5 Q17.5,2.5 17.5,4.5 L17.5,19.5 Q17.5,21.5 15.5,21.5 L8.5,21.5 Q6.5,21.5 6.5,19.5 L6.5,4.5 Q6.5,2.5 8.5,2.5 Z", 2.2);
                Linie("M9,5.5 L15,5.5 L15,16.5 L9,16.5 Z", 1.4);
                Linie("M11,19 L13,19", 2);
                break;
            case DaumenSymbol.Springen:
            case DaumenSymbol.Knien:
            case DaumenSymbol.Liegen:
            case DaumenSymbol.Aufstehen:
                // Welle A 1.7.0 (Sammelliste 447, Abnahmebild P12-231 Ordner 06): Figuren auf dem Raster 100 des Entwurfs (Faktor
                // 0,24), Glieder 1,8 dick, Rumpf 2,34, Kopf Halbmesser 2,16; Boden duenn, Sprungbogen gestrichelt
                Figur(symbol);
                break;
            case DaumenSymbol.Auge:
                Linie("M2,12 Q12,3 22,12 Q12,21 2,12 Z", 2.2);
                Kopf(12, 12, 3.2);
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

    private static readonly Dictionary<DaumenSymbol, (char Art, double[] Werte)[]> Figuren = new()
    {
        [DaumenSymbol.Aufstehen] = [('k', [50, 15, 9]), ('r', [50, 28, 50, 56]), ('l', [50, 32, 36, 44, 31, 56]), ('l', [50, 32, 64, 44, 69, 56]),
            ('l', [50, 56, 42, 71, 40, 88]), ('l', [50, 56, 58, 71, 60, 88])],
        [DaumenSymbol.Springen] = [('k', [54, 12, 9]), ('r', [53, 25, 50, 49]), ('l', [53, 29, 39, 23, 31, 10]), ('l', [53, 29, 67, 22, 75, 10]),
            ('l', [50, 49, 36, 57, 40, 72]), ('l', [50, 49, 63, 59, 60, 75]), ('b', [12, 94, 88, 94, 80])],
        [DaumenSymbol.Knien] = [('k', [43, 22, 9]), ('r', [44, 35, 49, 60]), ('l', [44, 39, 57, 49, 67, 58]), ('l', [44, 39, 35, 52, 32, 64]),
            ('l', [49, 60, 37, 83, 17, 84]), ('l', [49, 60, 69, 61, 70, 84]), ('g', [8, 90, 92])],
        [DaumenSymbol.Liegen] = [('k', [79, 54, 9]), ('r', [68, 65, 38, 68]), ('l', [67, 66, 79, 74, 94, 72]), ('l', [65, 67, 57, 76, 69, 77]),
            ('l', [38, 68, 22, 73, 6, 72]), ('l', [38, 68, 26, 60, 14, 67]), ('g', [2, 84, 98])],
    };

    private void Figur(DaumenSymbol symbol)
    {
        const double f = 0.24;
        const double dicke = 7.5 * f;
        string P(double x, double y) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{x * f:0.###},{y * f:0.###}");
        foreach (var (art, w) in Figuren[symbol])
        {
            switch (art)
            {
                case 'k':
                    FigurKopf(w[0] * f, w[1] * f, w[2] * f);
                    break;
                case 'l':
                case 'r':
                    var daten = "M" + P(w[0], w[1]);
                    for (var i = 2; i < w.Length; i += 2)
                    {
                        daten += " L" + P(w[i], w[i + 1]);
                    }
                    FigurLinie(daten, art == 'r' ? dicke * 1.3 : dicke);
                    break;
                case 'g':
                    FigurLinie("M" + P(w[0], w[1]) + " L" + P(w[2], w[1]), dicke * 0.55);
                    break;
                default:
                    // Sprungbogen: gestrichelt (jedes zweite von 14 Stuecken) unter der Figur
                    const int n = 14;
                    (double X, double Y) Q(double t) => (w[0] + ((w[2] - w[0]) * t), w[1] - (4 * (w[1] - w[4]) * t * (1 - t)));
                    for (var i = 0; i < n; i += 2)
                    {
                        var (a, b) = (Q(i / (double)n), Q((i + 1) / (double)n));
                        FigurLinie("M" + P(a.X, a.Y) + " L" + P(b.X, b.Y), dicke * 0.5);
                    }
                    break;
            }
        }
    }

    private void FigurLinie(string daten, double dicke)
    {
        var pfad = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(daten),
            StrokeThickness = dicke,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Stroke = new SolidColorBrush(Color.Parse(_zeichenFarbe ?? "#FFFFFF")),
        };
        _symbol.Children.Add(pfad);
    }

    private void FigurKopf(double x, double y, double r)
    {
        var kopf = new Ellipse { Width = 2 * r, Height = 2 * r, Fill = new SolidColorBrush(Color.Parse(_zeichenFarbe ?? "#FFFFFF")) };
        Canvas.SetLeft(kopf, x - r);
        Canvas.SetTop(kopf, y - r);
        _symbol.Children.Add(kopf);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (WischNachUnten && _start is { } start && !_langAusgeloest)
        {
            var p = e.GetPosition(this);
            var (dx, dy) = (p.X - start.X, p.Y - start.Y);
            if (dy >= HaltungKnopf.WischAb && dy > Math.Abs(dx))
            {
                _lang.Stop();
                _langAusgeloest = true;
                Lang?.Invoke();
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _start = e.GetPosition(this);
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
        _start = null;
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

    /// <summary>Start-Test (Welle A 1.7.0): nach unten wischen wie ein Finger (Druck, Bewegung um <paramref name="dy"/> Punkte nach unten).</summary>
    public void WischenFuerTests(double dy)
    {
        _start = new Point(Width / 2, Height / 2);
        _langAusgeloest = false;
        if (WischNachUnten && dy >= HaltungKnopf.WischAb)
        {
            _langAusgeloest = true;
            Lang?.Invoke();
        }
        _start = null;
        _langAusgeloest = false;
    }
}
