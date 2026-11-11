using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.22.0, Ansicht „Zusammenhänge“ (Weisung P12-92, Entwurf P12-60): Kaesten je Ort mit Reiter, aufklappbar
/// („Alle zeigen“/„Weniger zeigen“, „Alle aufklappen/zuklappen“), Linien „erst nach“ grau mit runden Ecken und Punkten an
/// den Enden, Zoomen und Schieben wie in der Draufsicht (Maus, Rad, ein und zwei Finger, Kamera-Karte mit „Alles zeigen“).
/// Die Anordnung rechnet <see cref="Zusammenhaenge"/>; hier wird nur gezeichnet und bedient.
/// </summary>
internal sealed class ZusammenhangAnsicht : Grid
{
    private readonly Panel _sicht = new() { ClipToBounds = true };
    // oben links verankert: groesser als die Sicht wuerde das Panel sie sonst mittig setzen (Versatz ausserhalb der Kamera)
    private readonly Canvas _flaeche = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly LinienFlaeche _linien = new();
    private readonly LinienFlaeche _pfeilEbene = new();
    private readonly HashSet<string> _offen = new(StringComparer.Ordinal);
    private readonly Button _alleAuf;
    private readonly Fingergeste _geste = new();
    private Spielgeschichte? _geschichte;
    private Func<string, string> _name = o => o;
    private double _zoom = 1;
    private Point _versatz;
    private bool _eingepasst = true;
    private bool _gefangen;

    /// <summary>Ein Kasten wurde angetippt (Ort): rechts dessen Spielgeschichte.</summary>
    public event Action<string>? OrtGewaehlt;

    /// <summary>Knopf „Aufgabe → Ort“.</summary>
    public event Action? ZurListe;

    /// <summary>
    /// Welle Q1 (Weisung P12-186): Pfeil von einer Aufgabe (erste Kennung) auf eine andere gezogen (zweite Kennung):
    /// die zweite kommt „erst nach“ der ersten.
    /// </summary>
    public event Action<string, string>? PfeilGezogen;

    /// <summary>Welle Q1: Umschaltung „je Ort | je Questgeber“ geaendert (true = je Questgeber).</summary>
    public event Action<bool>? AnsichtGewechselt;

    /// <summary>Welle Q1: zeigt die Ansicht die Gruppen je Questgeber?</summary>
    public bool JeQuestgeber { get; private set; }

    /// <summary>Welle Q1: duerfen Pfeile gezogen werden (Projekt nicht nur zum Lesen)?</summary>
    public bool PfeilErlaubt { get; set; } = true;

    /// <summary>Knoepfe der Umschaltung (Start-Test).</summary>
    public (Button JeOrt, Button JeQuestgeber) Umschaltung { get; }

    /// <summary>Die Zeilen (Aufgabe, Flaeche in der Anordnung) beim letzten Zeigen (Start-Test: Pfeil ziehen).</summary>
    public IReadOnlyList<(string Id, Zusammenhaenge.Flaeche Ort)> ZeilenOrte => _zeilen;

    private readonly List<(string Id, Zusammenhaenge.Flaeche Ort)> _zeilen = [];
    private string? _pfeilVon;
    private Point _pfeilStart;

    /// <summary>Die Anordnung beim letzten Zeigen (Start-Test: Kreuzungen, Kaesten).</summary>
    public Zusammenhaenge? Plan { get; private set; }

    /// <summary>Gegenprobe des Start-Tests: Reiter mit dem Innenabstand vor der Korrektur (Name wird getrennt).</summary>
    public static bool ReiterAltFuerTests { get; set; }

    /// <summary>Gegenprobe des Start-Tests: gerade Linien wie ein einfacher Plan.</summary>
    public bool AltFuerTests { get; set; }

    /// <summary>Aufgeklappte Orte.</summary>
    public IReadOnlySet<string> Offen => _offen;

    /// <summary>Knoepfe „Alle zeigen“/„Weniger zeigen“ je Ort (Start-Test).</summary>
    public Dictionary<string, Button> ZeigenKnoepfe { get; } = new(StringComparer.Ordinal);

    /// <summary>Kopf je Ort (antippbar, Start-Test).</summary>
    public Dictionary<string, Klickflaeche> Koepfe { get; } = new(StringComparer.Ordinal);

    /// <summary>Texte in den Kaesten (Start-Test: nie abgeschnitten).</summary>
    public List<TextBlock> KastenTexte { get; } = [];

    /// <summary>Zoom und Versatz (Start-Test, Fotos).</summary>
    public (double Zoom, Point Versatz) Kamera => (_zoom, _versatz);

    public ZusammenhangAnsicht()
    {
        RowDefinitions = new RowDefinitions("Auto,*");
        _alleAuf = Schrift.Knopf(Texte.GeschichteAlleAuf).Mit(Texte.GeschichteAlleAufTipp);
        _alleAuf.Click += (_, _) => AlleUmschalten();
        var liste = Schrift.Knopf(Texte.GeschichteZurListe).Mit(Texte.GeschichteZurListeTipp);
        liste.Click += (_, _) => ZurListe?.Invoke();
        var titel = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Schrift.Text(Texte.GeschichteZusammenhaenge, "Gross", FontWeight.Bold),
                Schrift.Text(Texte.GeschichteZusammenhaengeZeile, "Klein", farbe: "Pinsel.muted"),
            },
        };
        var kopf = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        kopf.Children.Add(titel);
        // Welle Q1: Umschaltung „je Ort | je Questgeber“ (zwei Knoepfe, der gewaehlte betont)
        var jeOrt = Schrift.Knopf(Texte.VerkettungJeOrt).Mit(Texte.VerkettungTipp);
        var jeQg = Schrift.Knopf(Texte.VerkettungJeQuestgeber).Mit(Texte.VerkettungTipp);
        jeOrt.Click += (_, _) => AnsichtSetzen(false);
        jeQg.Click += (_, _) => AnsichtSetzen(true);
        Umschaltung = (jeOrt, jeQg);
        UmschaltungFarben();
        var knoepfe = new WrapPanel { Children = { jeOrt, jeQg, _alleAuf, liste }, VerticalAlignment = VerticalAlignment.Top, ItemSpacing = 6, LineSpacing = 6 };
        Grid.SetColumn(knoepfe, 1);
        kopf.Children.Add(knoepfe);
        var kopfRahmen = new Border { Child = kopf, Padding = new Thickness(16, 12, 16 + Dialog.RandFuerLeiste, 4), BorderThickness = new Thickness(0, 0, 0, 1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        Children.Add(kopfRahmen);

        _flaeche.Children.Add(_linien);
        _flaeche.RenderTransformOrigin = RelativePoint.TopLeft;
        _sicht.Children.Add(_flaeche);
        _sicht.Farbe(Panel.BackgroundProperty, "Pinsel.bg");
        _sicht.AddHandler(PointerPressedEvent, Gedrueckt, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _sicht.AddHandler(PointerMovedEvent, Bewegt, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _sicht.AddHandler(PointerReleasedEvent, Losgelassen, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _sicht.AddHandler(PointerCaptureLostEvent, (_, e) =>
        {
            _geste.Verloren(e.Pointer.Id);
            if (_pfeilVon is not null)
            {
                _pfeilVon = null;
                _pfeilEbene.Pfeil = null;
                _pfeilEbene.InvalidateVisual();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        _sicht.PointerWheelChanged += (_, e) =>
        {
            var p = e.GetPosition(_sicht);
            Zoomen(e.Delta.Y > 0 ? 1.15 : 1 / 1.15, p);
            e.Handled = true;
        };
        _sicht.SizeChanged += (_, _) =>
        {
            if (_eingepasst)
            {
                Einpassen();
            }
        };
        Button Klein(string text, string name, string tipp, Action a)
        {
            var k = Schrift.Knopf(text).Vorlese(name).Mit(tipp);
            k.Click += (_, _) => a();
            return k;
        }
        var kamera = Oberflaeche.Kamerakarte.Bauen(new WrapPanel
        {
            Children =
            {
                Klein("+", Texte.Vergroessern, Texte.ZoomTipp, () => Zoomen(1.25, null)),
                Klein("−", Texte.Verkleinern, Texte.ZoomTipp, () => Zoomen(1 / 1.25, null)),
                Klein(Texte.GeschichteAllesZeigen, Texte.GeschichteAllesZeigen, Texte.GeschichteAllesZeigenTipp, Einpassen),
            },
        });
        Kamerakarte = kamera;
        var mitte = Oberflaeche.Kamerakarte.Ueber(_sicht, kamera);
        Grid.SetRow(mitte, 1);
        Children.Add(mitte);
        Schrift.Geaendert += () =>
        {
            if (_geschichte is not null)
            {
                Neu();
            }
        };
    }

    /// <summary>Welle Q1: zwischen „je Ort“ und „je Questgeber“ wechseln.</summary>
    public void AnsichtSetzen(bool jeQuestgeber)
    {
        if (JeQuestgeber == jeQuestgeber)
        {
            return;
        }
        JeQuestgeber = jeQuestgeber;
        _offen.Clear();
        _eingepasst = true;
        UmschaltungFarben();
        AnsichtGewechselt?.Invoke(jeQuestgeber);
    }

    private void UmschaltungFarben()
    {
        foreach (var (k, an) in new[] { (Umschaltung.JeOrt, !JeQuestgeber), (Umschaltung.JeQuestgeber, JeQuestgeber) })
        {
            k.Farbe(Button.BackgroundProperty, an ? "VerlaufW.primary" : "Pinsel.card_alt");
            if (k.Content is TextBlock t)
            {
                t.Farbe(TextBlock.ForegroundProperty, an ? "Pinsel.on_accent" : "Pinsel.text");
            }
            k.Vorlese(an ? Texte.VerkettungGewaehlt(Py_Text(k)) : Py_Text(k));
        }
    }

    private static string Py_Text(Button k) => k.Content is TextBlock t ? t.Text ?? "" : k.Content as string ?? "";

    /// <summary>Die Kamera-Karte (Start-Test: verdeckt keinen Kasten beim Einpassen).</summary>
    public Border Kamerakarte { get; }

    /// <summary>Spielgeschichte zeigen (neu aufbauen; aufgeklappte Orte bleiben).</summary>
    public void Zeigen(Spielgeschichte g, Func<string, string> name)
    {
        _geschichte = g;
        _name = name;
        _offen.IntersectWith(g.Zahlen().Keys);
        Neu();
    }

    /// <summary>Einen Ort auf- oder zuklappen (Knopf „Alle zeigen“/„Weniger zeigen“).</summary>
    public void Umschalten(string ort)
    {
        if (!_offen.Remove(ort))
        {
            _offen.Add(ort);
        }
        Neu();
    }

    /// <summary>„Alle aufklappen“ bzw. „Alle zuklappen“.</summary>
    public void AlleUmschalten()
    {
        if (_geschichte is null)
        {
            return;
        }
        if (_offen.Count == _geschichte.Zahlen().Count)
        {
            _offen.Clear();
        }
        else
        {
            _offen.UnionWith(_geschichte.Zahlen().Keys);
        }
        Neu();
    }

    /// <summary>Aufgeklappte Orte setzen (Start-Test, Fotos).</summary>
    public void OffenSetzen(IEnumerable<string> orte)
    {
        _offen.Clear();
        _offen.UnionWith(orte);
        Neu();
    }

    // ---- Aufbau --------------------------------------------------------------------------------------

    private static double Groesse(string stufe) =>
        Application.Current?.TryGetResource("Schrift." + stufe, null, out var v) == true && v is double d ? d : Schrift.Grundgroessen[stufe];

    private static FontFamily Familie() =>
        Application.Current?.TryGetResource("ContentControlThemeFontFamily", null, out var v) == true && v is FontFamily f ? f : FontFamily.Default;

    /// <summary>Text mit fester Schrift (gemessen vor dem Einhaengen, gleich gezeichnet).</summary>
    private static TextBlock Text(string text, Zusammenhaenge.Schrift art, string farbe = "Pinsel.text")
    {
        var t = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = Familie(),
            FontSize = Groesse(art switch { Zusammenhaenge.Schrift.Titel => "Normal", _ => "Klein" }),
            FontWeight = art is Zusammenhaenge.Schrift.Reiter or Zusammenhaenge.Schrift.Titel ? FontWeight.Bold : FontWeight.Normal,
        };
        return t.Farbe(TextBlock.ForegroundProperty, farbe);
    }

    private static readonly Zusammenhaenge.Masse Masse = new(
        (text, breite, art) =>
        {
            var t = Text(text, art);
            t.Measure(new Size(Math.Max(1, breite), double.PositiveInfinity));
            return Math.Ceiling(t.DesiredSize.Height);
        },
        (text, art) =>
        {
            var t = Text(text, art);
            t.Measure(Size.Infinity);
            return Math.Ceiling(t.DesiredSize.Width);
        });

    private void Neu()
    {
        if (_geschichte is not { } g)
        {
            return;
        }
        var plan = Zusammenhaenge.Bauen(g, _name, _offen, Masse, AltFuerTests);
        Plan = plan;
        _flaeche.Children.Clear();
        _flaeche.Children.Add(_linien);
        ZeigenKnoepfe.Clear();
        Koepfe.Clear();
        _zeilen.Clear();
        KastenTexte.Clear();
        _flaeche.Width = plan.Breite;
        _flaeche.Height = plan.Hoehe;
        _linien.Width = plan.Breite;
        _linien.Height = plan.Hoehe;
        _linien.Plan = plan;
        _pfeilEbene.Width = plan.Breite;
        _pfeilEbene.Height = plan.Hoehe;
        void Setzen(Control c, Zusammenhaenge.Flaeche f)
        {
            c.Width = f.B;
            c.Height = f.H;
            Canvas.SetLeft(c, f.X);
            Canvas.SetTop(c, f.Y);
            _flaeche.Children.Add(c);
        }
        foreach (var k in plan.Kaesten)
        {
            var koerper = new Border { CornerRadius = new CornerRadius(0, 10, 10, 10), BorderThickness = new Thickness(1) }
                .Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
            Setzen(koerper, k.Koerper);
            var reiterText = Text(k.Name, Zusammenhaenge.Schrift.Reiter);
            reiterText.VerticalAlignment = VerticalAlignment.Center;
            KastenTexte.Add(reiterText);
            var reiter = new Border
            {
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                BorderThickness = new Thickness(1, 1, 1, 0),
                // Rand 1 + Innenabstand 15 = die 16 je Seite, mit denen die Anordnung den Reiter misst
                Padding = new Thickness(ReiterAltFuerTests ? 16 : 15, 0),
                Child = reiterText,
            }.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
            Setzen(reiter, k.Reiter with { H = k.Reiter.H + 1 });
            // Kopf: Symbol, Zahl und Tag; antippen = rechts die Spielgeschichte des Orts
            var titel = Text(Zusammenhaenge.KopfTitel(k.Zahl), Zusammenhaenge.Schrift.Titel);
            var zeile = Text(Zusammenhaenge.KopfZeile(k.FreiAb), Zusammenhaenge.Schrift.Klein, "Pinsel.muted");
            KastenTexte.Add(titel);
            KastenTexte.Add(zeile);
            var kopfInhalt = new Grid { ColumnDefinitions = new ColumnDefinitions(Zusammenhaenge.SymbolBreite + ",*"), ColumnSpacing = 8 };
            var symbol = AufgabenSymbol.Bauen("liste", Zusammenhaenge.SymbolBreite);
            symbol.VerticalAlignment = VerticalAlignment.Center;
            kopfInhalt.Children.Add(symbol);
            var texte = new StackPanel { Children = { titel, zeile }, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(texte, 1);
            kopfInhalt.Children.Add(texte);
            var ort = k.Ort;
            var kopf = new Klickflaeche { Child = kopfInhalt, Background = Brushes.Transparent, Klick = () => OrtGewaehlt?.Invoke(ort) };
            kopf.Vorlese(Texte.GeschichteKastenName(k.Name, k.Zahl, k.FreiAb)).Mit(Texte.GeschichteKastenTipp);
            Koepfe[ort] = kopf;
            Setzen(kopf, k.Kopf);
            foreach (var z in k.Zeilen)
            {
                var r = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions(Zusammenhaenge.SymbolBreite + ",*," + Zusammenhaenge.TagBreite),
                    ColumnSpacing = 4,
                    // Rand 1 + 3 + zwei Abstaende 4 = die 16, die die Anordnung je Zeile abzieht
                    Margin = new Thickness(3, 0, 3, 0),
                };
                var s = AufgabenSymbol.Bauen(z.Aufgabe.Typ, 22);
                s.VerticalAlignment = VerticalAlignment.Center;
                r.Children.Add(s);
                var t = Text(z.Aufgabe.Titel, Zusammenhaenge.Schrift.Zeile);
                t.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(t, 1);
                r.Children.Add(t);
                var tag = Text(Texte.GeschichteTag(z.Aufgabe.AbTag), Zusammenhaenge.Schrift.Klein, "Pinsel.muted");
                tag.TextAlignment = TextAlignment.Right;
                tag.TextWrapping = TextWrapping.NoWrap;
                tag.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(tag, 2);
                r.Children.Add(tag);
                KastenTexte.Add(t);
                KastenTexte.Add(tag);
                var rahmen = new Border { Child = r, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) }
                    .Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
                ToolTip.SetTip(rahmen, Texte.AufgabenArt(z.Aufgabe.Typ) + ": " + z.Aufgabe.Titel + "\n" + Texte.VerkettungZiehenTipp);
                Setzen(rahmen, z.Ort);
                _zeilen.Add((z.Aufgabe.Id, z.Ort));
            }
            // „Alle zeigen“ / „Weniger zeigen“ wie ein Verweis
            var zeigen = new Button
            {
                Content = Text(k.Offen ? Texte.GeschichteWenigerZeigen : Texte.GeschichteAlleZeigen, Zusammenhaenge.Schrift.Klein, "Pinsel.accent"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
            }.Vorlese(k.Offen ? Texte.GeschichteWenigerZeigenName(k.Name) : Texte.GeschichteAlleZeigenName(k.Name)).Mit(Texte.GeschichteAlleZeigenTipp);
            zeigen.Click += (_, _) => Umschalten(ort);
            ZeigenKnoepfe[ort] = zeigen;
            Setzen(zeigen, k.Knopf);
        }
        _flaeche.Children.Add(_pfeilEbene);
        if (_alleAuf.Content is TextBlock at)
        {
            at.Text = _offen.Count == g.Zahlen().Count && _offen.Count > 0 ? Texte.GeschichteAlleZu : Texte.GeschichteAlleAuf;
        }
        if (_eingepasst)
        {
            Einpassen();
        }
        else
        {
            Anwenden();
        }
        _linien.InvalidateVisual();
    }

    // ---- Kamera --------------------------------------------------------------------------------------

    /// <summary>Orte, deren Kasten (wie gezeichnet) unter der Kamera-Karte liegt (Start-Test nach „Alles zeigen“).</summary>
    public IReadOnlyList<string> UnterDerKamerakarte()
    {
        if (Plan is not { } p || Kamerakarte.TranslatePoint(default, _sicht) is not { } o)
        {
            return [];
        }
        var karte = new Rect(o, Kamerakarte.Bounds.Size);
        return [.. p.Kaesten.Where(k => new Rect((k.Aussen.X * _zoom) + _versatz.X, (k.Aussen.Y * _zoom) + _versatz.Y,
            k.Aussen.B * _zoom, k.Aussen.H * _zoom).Intersects(karte)).Select(k => k.Ort)];
    }

    /// <summary>Liegt die ganze Ansicht nach „Alles zeigen“ in der Sicht?</summary>
    public bool GanzSichtbar => Plan is { } p && _versatz.X >= -0.5 && _versatz.Y >= -0.5
        && (p.Breite * _zoom) + _versatz.X <= _sicht.Bounds.Width + 0.5 && (p.Hoehe * _zoom) + _versatz.Y <= _sicht.Bounds.Height + 0.5;

    /// <summary>„Alles zeigen“: die ganze Ansicht ins Fenster (unten Platz fuer die Kamera-Karte), hoechstens 1:1.</summary>
    public void Einpassen()
    {
        if (Plan is not { } p || _sicht.Bounds.Width <= 0 || _sicht.Bounds.Height <= 0)
        {
            _eingepasst = true;
            return;
        }
        var unten = Oberflaeche.Kamerakarte.Hoehe(Kamerakarte);
        var hoehe = Math.Max(40, _sicht.Bounds.Height - unten);
        _zoom = Math.Clamp(Math.Min(_sicht.Bounds.Width / p.Breite, hoehe / p.Hoehe), 0.1, 1);
        _versatz = new Point(Math.Max(0, (_sicht.Bounds.Width - (p.Breite * _zoom)) / 2), Math.Max(0, (hoehe - (p.Hoehe * _zoom)) / 2));
        _eingepasst = true;
        Anwenden();
    }

    private void Zoomen(double faktor, Point? um)
    {
        var mitte = um ?? new Point(_sicht.Bounds.Width / 2, _sicht.Bounds.Height / 2);
        var neu = Math.Clamp(_zoom * faktor, 0.1, 3);
        var f = neu / _zoom;
        _versatz = new Point(mitte.X - ((mitte.X - _versatz.X) * f), mitte.Y - ((mitte.Y - _versatz.Y) * f));
        _zoom = neu;
        _eingepasst = false;
        Anwenden();
    }

    private void Anwenden()
    {
        _flaeche.RenderTransform = new MatrixTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_versatz.X, _versatz.Y));
    }

    /// <summary>Punkt der Sicht in der Anordnung (ohne Zoom und Versatz).</summary>
    public Point InAnordnung(Point p) => new((p.X - _versatz.X) / _zoom, (p.Y - _versatz.Y) / _zoom);

    /// <summary>Punkt der Anordnung in der Sicht (Start-Test).</summary>
    public Point InSicht(Point p) => new((p.X * _zoom) + _versatz.X, (p.Y * _zoom) + _versatz.Y);

    /// <summary>Die Sicht (Start-Test: Zeiger dorthin schicken).</summary>
    public Control Sicht => _sicht;

    /// <summary>Aufgabe unter einem Punkt der Anordnung (null = keine).</summary>
    public string? ZeileBei(Point a) =>
        _zeilen.FirstOrDefault(z => a.X >= z.Ort.X && a.X <= z.Ort.Rechts && a.Y >= z.Ort.Y && a.Y <= z.Ort.Unten).Id;

    /// <summary>Wird gerade ein Pfeil gezogen (Start-Test)?</summary>
    public bool PfeilLaeuft => _pfeilVon is not null;

    private void Gedrueckt(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetPosition(_sicht);
        var punkt = e.GetCurrentPoint(_sicht);
        // Welle Q1: ein Druck (Maus links, Finger, Stift) auf eine Aufgabe beginnt einen Pfeil, solange kein anderer Finger liegt
        if (PfeilErlaubt && !_geste.Aktiv && (e.Pointer.Type != PointerType.Mouse || punkt.Properties.IsLeftButtonPressed)
            && ZeileBei(InAnordnung(p)) is { } von)
        {
            _pfeilVon = von;
            _pfeilStart = InAnordnung(p);
            _pfeilEbene.Pfeil = (_pfeilStart, _pfeilStart);
            e.Pointer.Capture(_sicht);
            e.Handled = true;
            return;
        }
        _geste.Druecken(e.Pointer.Id, p.X, p.Y, false);
        _gefangen = false;
    }

    private void Bewegt(object? sender, PointerEventArgs e)
    {
        if (_pfeilVon is not null)
        {
            _pfeilEbene.Pfeil = (_pfeilStart, InAnordnung(e.GetPosition(_sicht)));
            _pfeilEbene.InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (!_geste.Aktiv)
        {
            return;
        }
        var p = e.GetPosition(_sicht);
        var s = _geste.Bewegen(e.Pointer.Id, p.X, p.Y);
        switch (s.Art)
        {
            case Fingergeste.Art.AnsichtSchieben:
                if (!_gefangen)
                {
                    // erst ab einer Bewegung fangen: ein Tipp auf einen Knopf bleibt ein Klick
                    e.Pointer.Capture(_sicht);
                    _gefangen = true;
                }
                _versatz += new Point(s.Dx, s.Dy);
                _eingepasst = false;
                Anwenden();
                e.Handled = true;
                break;
            case Fingergeste.Art.Zoomen:
                _versatz += new Point(s.Dx, s.Dy);
                Zoomen(s.Faktor, new Point(s.X, s.Y));
                e.Handled = true;
                break;
        }
    }

    private void Losgelassen(object? sender, PointerReleasedEventArgs e)
    {
        if (_pfeilVon is { } von)
        {
            var ende = InAnordnung(e.GetPosition(_sicht));
            _pfeilVon = null;
            _pfeilEbene.Pfeil = null;
            _pfeilEbene.InvalidateVisual();
            e.Pointer.Capture(null);
            e.Handled = true;
            if (ZeileBei(ende) is { } auf && auf != von)
            {
                PfeilGezogen?.Invoke(von, auf);
            }
            return;
        }
        // erst auswerten, dann loslassen (Lehre S 0.5.1: sonst kommt PointerCaptureLost zuerst)
        _geste.Loslassen(e.Pointer.Id);
        if (_gefangen && !_geste.Aktiv)
        {
            e.Pointer.Capture(null);
            _gefangen = false;
        }
    }

    /// <summary>Linien mit runden Ecken und Punkten an den Enden (unter den Kaesten).</summary>
    private sealed class LinienFlaeche : Control
    {
        public Zusammenhaenge? Plan { get; set; }

        /// <summary>Welle Q1: Pfeil, der gerade gezogen wird (Anfang, Ende in der Anordnung).</summary>
        public (Point A, Point B)? Pfeil { get; set; }

        public LinienFlaeche() => IsHitTestVisible = false;

        private void PfeilZeichnen(DrawingContext ctx)
        {
            if (Pfeil is not { } pf)
            {
                return;
            }
            var farbe = new SolidColorBrush(Color.Parse(FarbenAnwenden.Aktuell.Farben["accent"]));
            var stift = new Pen(farbe, 3, lineCap: PenLineCap.Round);
            ctx.DrawLine(stift, pf.A, pf.B);
            var d = pf.B - pf.A;
            var l = Laenge(pf.A, pf.B);
            if (l < 4)
            {
                return;
            }
            var r = d / l;
            var n = new Point(-r.Y, r.X);
            ctx.DrawLine(stift, pf.B, pf.B - (r * 12) + (n * 7));
            ctx.DrawLine(stift, pf.B, pf.B - (r * 12) - (n * 7));
        }

        public override void Render(DrawingContext ctx)
        {
            PfeilZeichnen(ctx);
            if (Plan is not { } p)
            {
                return;
            }

            var farben = FarbenAnwenden.Aktuell.Farben;
            var grau = new SolidColorBrush(Color.Parse(farben["muted"]));
            var stift = new Pen(grau, 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var punkte = new HashSet<(double, double)>();
            foreach (var l in p.Linien)
            {
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    var pt = l.Punkte.Select(q => new Point(q.X, q.Y)).ToList();
                    g.BeginFigure(pt[0], false);
                    for (var i = 1; i < pt.Count; i++)
                    {
                        if (i + 1 < pt.Count)
                        {
                            // runde Ecke: Radius hoechstens halb so gross wie die kuerzere Strecke
                            var a = pt[i - 1];
                            var b = pt[i];
                            var c = pt[i + 1];
                            var r = Math.Min(8, Math.Min(Laenge(a, b), Laenge(b, c)) / 2);
                            if (r < 0.5)
                            {
                                g.LineTo(b);
                                continue;
                            }
                            var vorher = b - ((b - a) / Laenge(a, b) * r);
                            var nachher = b + ((c - b) / Laenge(b, c) * r);
                            g.LineTo(vorher);
                            g.QuadraticBezierTo(b, nachher);
                        }
                        else
                        {
                            g.LineTo(pt[i]);
                        }
                    }
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, stift, geo);
                punkte.Add((l.Punkte[0].X, l.Punkte[0].Y));
                punkte.Add((l.Punkte[^1].X, l.Punkte[^1].Y));
            }
            foreach (var (x, y) in punkte)
            {
                ctx.DrawEllipse(grau, null, new Point(x, y), 3.5, 3.5);
            }
        }

        private static double Laenge(Point a, Point b) => Math.Max(1e-9, Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y))));
    }
}
