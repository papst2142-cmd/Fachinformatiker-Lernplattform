using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Design;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 444, Weisungen P12-359/P12-360, Nico 3a und 4): mit 3D gibt es kein Dreieck-Menue mit den Knoepfen
/// des Orts mehr. Was die Figur an ihrer Stelle tun kann (<see cref="StadtSpiel.Handlungen"/>), loest am PC die Taste F aus (die
/// Anzeige „F: …“ unten zeigt die Haupthandlung, ein Klick darauf tut dasselbe), am Handy und Tablet der runde Knopf ueber dem
/// linken Stick (Symbol Tuer, Bus oder Hand, darueber der Name). Gehalten (F oder Knopf) zeigt sich eine kleine Auswahl aller
/// Handlungen. „Spiel verlassen“ ist nur noch ein X oben rechts in der Ecke, hinterlegt mit der Farbe des Programms (Weisung
/// P12-361); das Spiel-Handy ist wieder ein rundes Handy-Symbol mit der Zahl der offenen Auftraege am Daumen (rechts neben dem
/// linken Stick, Nico 1a, Weisung P12-362), am PC unten links neben der Ich-Sicht (2a). Die Ich-Sicht
/// ist ein kleines rundes Symbol (Auge) rechts neben dem Handlungsknopf, am PC unten links (die Taste bleibt). Unten bleibt der
/// Hinweis als kurze Zeile. Ohne 3D und beim Einrichten bleibt alles wie bis A 1.5.0 (Gegenprobe <see cref="MenueAltFuerTests"/>).
/// </summary>
public sealed partial class SpielSeite
{
    /// <summary>Gegenprobe (Start-Test, je Seite wie <see cref="OrtslisteAltFuerTests"/>): Dreieck-Menue wie bis A 1.5.0.</summary>
    internal bool MenueAltFuerTests { get; set; }

    /// <summary>
    /// Start-Test (je Seite): die Teile vor A 1.6.0 pruefen Wege durch Raeume und Knoepfe des Menues; dort oeffnet die Figur die
    /// Raumtueren selbst wie im Werkzeug (jedes neu gesetzte Spiel). Die Tueren selbst prueft der Teil „Handlung 444“ ohne das.
    /// </summary>
    internal bool TuerenSelbstFuerTests { get; set; }

    /// <summary>Handlung statt Menue: mit 3D, nicht beim Einrichten.</summary>
    public bool HandlungModus => !MenueAltFuerTests && Flaeche.GlBereit && !_ersatz.IsVisible && Spiel is { Einrichten: false };

    private DaumenKnopf? _handlungRund;
    private DaumenKnopf? _ichRund;
    private readonly TextBlock _handlungNameText = new() { TextWrapping = TextWrapping.NoWrap };
    private Border? _handlungName;
    private Button? _fAnzeige;
    private Border? _auswahl;
    private readonly StackPanel _auswahlListe = new() { Spacing = 6 };
    private string _auswahlSchluessel = "";
    private Klickflaeche? _xKnopf;
    private TextBlock? _xSymbol;
    private string _xSymbolFarbe = "";
    private DaumenKnopf? _handyRund;
    private readonly TextBlock _handyZahl = new() { FontWeight = FontWeight.Bold, TextAlignment = TextAlignment.Center };
    private Border? _handyZahlRand;
    private readonly DispatcherTimer _fLang = new() { Interval = TimeSpan.FromSeconds(HandlungKnopf.LangAb) };
    private bool _fGedrueckt;
    private bool _fLangAusgeloest;
    private bool? _letzterModus;

    /// <summary>Start-Test, Fotos: der runde Handlungsknopf (Handy, Tablet).</summary>
    public DaumenKnopf? HandlungRund => _handlungRund;

    /// <summary>Start-Test, Fotos: das runde Symbol der Ich-Sicht (Handy, Tablet, PC unten links).</summary>
    public DaumenKnopf? IchRund => _ichRund;

    /// <summary>Start-Test: die Anzeige „F: …“ am PC.</summary>
    public Button? FAnzeige => _fAnzeige;

    /// <summary>Start-Test: das X oben rechts in der Ecke (Spiel verlassen).</summary>
    public Klickflaeche? XKnopf => _xKnopf;

    /// <summary>Start-Test: Farbe (Hex) von Flaeche und Symbol des X.</summary>
    public (string Flaeche, string Symbol) XFarben => (AkzentHex(), _xSymbolFarbe);

    /// <summary>Start-Test, Fotos: das runde Handy-Symbol.</summary>
    public DaumenKnopf? HandyRund => _handyRund;

    /// <summary>Start-Test: Zahl am Handy-Symbol (leer = keine Zahl zu sehen).</summary>
    public string HandyZahl => _handyZahlRand is { IsVisible: true } ? _handyZahl.Text ?? "" : "";

    /// <summary>Start-Test: ist die Auswahl aller Handlungen offen, und ihre Knoepfe.</summary>
    public IReadOnlyList<Button> AuswahlKnoepfe => _auswahl is { IsVisible: true } ? [.. _auswahlListe.Children.OfType<Button>()] : [];

    /// <summary>Start-Test: Rechteck der Kopfzeile in Punkten der Seite (null = nicht angeordnet).</summary>
    public Rect? KopfRechteck() => _kopf.IsEffectivelyVisible && _kopf.TranslatePoint(default, this) is { } p ? new Rect(p, _kopf.Bounds.Size) : null;

    /// <summary>Text des Namens ueber dem Handlungsknopf (leer = unsichtbar).</summary>
    public string HandlungNameText => _handlungName is { IsVisible: true } ? _handlungNameText.Text ?? "" : "";

    /// <summary>Anzeige „F: …“ fuer den Stapel der unteren Karte (ueber dem Hinweis, auch zugeklappt zu sehen).</summary>
    private Button FAnzeigeBauen()
    {
        var knopf = Schrift.Knopf("", true);
        knopf.HorizontalAlignment = HorizontalAlignment.Center;
        knopf.IsVisible = false;
        knopf.Click += (_, _) => HaupthandlungAusfuehren();
        _fAnzeige = knopf;
        return knopf;
    }

    /// <summary>X oben rechts in die Ebene, runde Knoepfe (Handlung, Ich-Sicht, Handy) und Auswahl auf die Ebene der Daumen.</summary>
    private void HandlungAnlegen(Grid ebene)
    {
        // Weisung P12-361 (Nico: „das X oben rechts direkt in der Ecke … hinterlegt mit der Farbe des Programms“)
        _xSymbol = Symbole.Block("close_rounded", 22, "Pinsel.text");
        _xSymbol.HorizontalAlignment = HorizontalAlignment.Center;
        _xSymbol.VerticalAlignment = VerticalAlignment.Center;
        var xKnopf = new Klickflaeche
        {
            Child = _xSymbol,
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8, 0, 0, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            IsVisible = false,
            Klick = () => _verlassen(),
        };
        xKnopf.Farbe(Border.BackgroundProperty, "Pinsel.accent");
        xKnopf.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && new Rect(xKnopf.Bounds.Size).Contains(e.GetPosition(xKnopf)))
            {
                e.Handled = true;
                _verlassen();
            }
        };
        xKnopf.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                _verlassen();
            }
        };
        Avalonia.Automation.AutomationProperties.SetName(xKnopf, StadtTexte.Verlassen);
        Grid.SetColumn(xKnopf, 1);
        ebene.Children.Add(xKnopf);
        _xKnopf = xKnopf;

        // Weisung P12-361: das Spiel-Handy wieder als Handy-Symbol mit der Zahl der offenen Auftraege, am Daumen
        _handyRund = new DaumenKnopf(DaumenSymbol.Telefon) { IsVisible = false };
        _handyRund.Getippt += () => _ = QuestlogZeigenAsync();
        _handyZahl.Groesse("Klein");
        _handyZahl.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        _handyZahlRand = new Border
        {
            Child = _handyZahl,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(5, 0),
            MinWidth = 20,
            IsHitTestVisible = false,
            IsVisible = false,
        };
        _handyZahlRand.Farbe(Border.BorderBrushProperty, "Pinsel.green");
        _handyZahlRand.Farbe(Border.BackgroundProperty, "Pinsel.card");

        _handlungRund = new DaumenKnopf(DaumenSymbol.Hand) { IsVisible = false };
        _handlungRund.Getippt += () => HaupthandlungAusfuehren();
        _handlungRund.Lang += () => AuswahlZeigen(true);
        _ichRund = new DaumenKnopf(DaumenSymbol.Auge) { IsVisible = false };
        _ichRund.Getippt += () =>
        {
            Flaeche.IchUmschalten();
            IchKnopfSetzen();
        };
        _handlungNameText.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        _handlungNameText.Groesse("Klein");
        _handlungName = new Border
        {
            Child = _handlungNameText,
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 3),
            IsHitTestVisible = false,
            IsVisible = false,
        };
        _handlungName.Farbe(Border.BackgroundProperty, "Pinsel.card");
        var titel = Schrift.Text(SpielProbeTexte.HandlungAuswahl, "Normal", FontWeight.Bold);
        var zu = Schrift.Knopf(SpielProbeTexte.HandlungAuswahlSchliessen);
        zu.Click += (_, _) => AuswahlZu();
        var karte = Schrift.Karte(new StackPanel { Spacing = 8, Children = { titel, _auswahlListe, zu } });
        karte.Padding = new Thickness(12, 10);
        karte.MaxWidth = 300;
        karte.IsVisible = false;
        _auswahl = karte;
        _daumenEbene.Children.Add(_handlungName);
        _daumenEbene.Children.Add(_handlungRund);
        _daumenEbene.Children.Add(_ichRund);
        _daumenEbene.Children.Add(_handyRund);
        _daumenEbene.Children.Add(_handyZahlRand);
        _daumenEbene.Children.Add(karte);
        _fLang.Tick += (_, _) =>
        {
            _fLang.Stop();
            _fLangAusgeloest = true;
            AuswahlZeigen(true);
        };
        // der linke Stick setzt nicht auf Handlung und Ich-Sicht auf (nur Handy und Tablet)
        Flaeche.KnopfSperreLinks = b => DaumenKnoepfeAn && EigeneLagen is null && (_ichRund?.IsVisible ?? false) ? HandlungKnopf.Sperre(b) : default;
        // kein Stick setzt auf dem Handy-Symbol auf
        Flaeche.KnopfSperreTelefon = b => DaumenKnoepfeAn && EigeneLagen is null && (_handyRund?.IsVisible ?? false) ? HandlungKnopf.TelefonSperre(b) : default;
    }

    private static string AkzentHex()
    {
        var akzent = Application.Current?.TryFindResource("Pinsel.accent", out var a) == true && a is ISolidColorBrush ab ? ab.Color : Colors.DeepSkyBlue;
        return $"#{akzent.R:X2}{akzent.G:X2}{akzent.B:X2}";
    }

    /// <summary>Die Haupthandlung ausfuehren (Taste, Knopf, Anzeige).</summary>
    public bool HaupthandlungAusfuehren()
    {
        if (!HandlungModus || Spiel is not { Haupthandlung: { } h })
        {
            return false;
        }
        return HandlungAusfuehren(h.Aktion);
    }

    /// <summary>Eine Handlung ausfuehren (Auswahl, Haupthandlung); true = ausgefuehrt.</summary>
    public bool HandlungAusfuehren(string aktion)
    {
        AuswahlZu();
        if (Spiel is not { } spiel || !spiel.Handeln(aktion))
        {
            return false;
        }
        Beschriften();
        Flaeche.AnfordernBild();
        return true;
    }

    /// <summary>Taste F gedrueckt (Wiederholung der Taste zaehlt nicht): nach der Zeit die Auswahl.</summary>
    private bool HandelnTasteRunter()
    {
        if (!HandlungModus)
        {
            return false;
        }
        if (!_fGedrueckt)
        {
            _fGedrueckt = true;
            _fLangAusgeloest = false;
            _fLang.Stop();
            _fLang.Start();
        }
        return true;
    }

    /// <summary>Taste F losgelassen: kurz = Haupthandlung (lang hat schon die Auswahl gezeigt).</summary>
    private bool HandelnTasteHoch()
    {
        if (!_fGedrueckt)
        {
            return false;
        }
        _fGedrueckt = false;
        _fLang.Stop();
        if (!_fLangAusgeloest)
        {
            if (_auswahl is { IsVisible: true })
            {
                AuswahlZu();
            }
            else
            {
                HaupthandlungAusfuehren();
            }
        }
        _fLangAusgeloest = false;
        return true;
    }

    /// <summary>Die kleine Auswahl aller Handlungen zeigen (nur mit mindestens einer Handlung).</summary>
    public void AuswahlZeigen(bool an)
    {
        if (_auswahl is null)
        {
            return;
        }
        if (!an || !HandlungModus || Spiel is not { } spiel || spiel.Handlungen.Count == 0)
        {
            AuswahlZu();
            return;
        }
        AuswahlFuellen(spiel.Handlungen);
        _auswahl.IsVisible = true;
        HandlungKnoepfeLegen();
        if (_auswahlListe.Children.OfType<Button>().FirstOrDefault() is { } erster)
        {
            erster.Focus();
        }
    }

    private void AuswahlZu()
    {
        if (_auswahl is { IsVisible: true })
        {
            _auswahl.IsVisible = false;
            Focus();
        }
    }

    private void AuswahlFuellen(IReadOnlyList<Handlung> liste)
    {
        var schluessel = string.Join("|", liste.Select(h => h.Aktion + "#" + h.Name));
        if (schluessel == _auswahlSchluessel)
        {
            return;
        }
        _auswahlSchluessel = schluessel;
        _auswahlListe.Children.Clear();
        foreach (var h in liste)
        {
            var knopf = Schrift.Knopf(h.Name, true);
            knopf.HorizontalAlignment = HorizontalAlignment.Stretch;
            var aktion = h.Aktion;
            knopf.Click += (_, _) => HandlungAusfuehren(aktion);
            _auswahlListe.Children.Add(knopf);
        }
    }

    /// <summary>Sichtbarkeit der alten und neuen Bedienung nach dem Zustand (am Ende jedes Beschriftens).</summary>
    private void HandlungSetzen()
    {
        if (_xKnopf is null || _xSymbol is null || _fAnzeige is null)
        {
            return;
        }
        var modus = HandlungModus;
        if (_letzterModus != modus)
        {
            _letzterModus = modus;
            Anordnen();
        }
        var spiel = Spiel;
        var haupt = modus ? spiel?.Haupthandlung : null;
        if (modus)
        {
            // die Knoepfe des Orts und „Spiel verlassen“, „Ich-Sicht“, Handy unten gibt es hier nicht (Knoepfe nur der Probe bleiben)
            _ortKnoepfe.IsVisible = false;
            _betreten.IsVisible = _zurStadt.IsVisible = _warten.IsVisible = _einrichten.IsVisible = _orteKnopf.IsVisible = false;
            VerlassenKnopf.IsVisible = false;
            _ichHuelle.IsVisible = false;
            if (_questKnopf is not null)
            {
                _questKnopf.IsVisible = false;
            }
            _knoepfeRolle.IsVisible = _knoepfe.IsVisible && _knoepfe.Children.Any(c => c.IsVisible);
        }
        else
        {
            _ortKnoepfe.IsVisible = true;
            _knoepfeRolle.IsVisible = true;
        }
        if (_xKnopf.IsVisible != modus)
        {
            _xKnopf.IsVisible = modus;
            // Welle A 1.13.0: der Platz fuer die Hinweiszeile neben der Uhr haengt am X
            HinweisObenLegen();
        }
        if (modus)
        {
            // Symbol des X schwarz oder weiss, je nachdem, was auf der Farbe des Programms mehr Kontrast hat
            var farbe = Symbolfarbe.Auf(AkzentHex());
            if (farbe != _xSymbolFarbe)
            {
                _xSymbolFarbe = farbe;
                _xSymbol.Foreground = new SolidColorBrush(Color.Parse(farbe));
            }
        }
        // PC: Anzeige „F: …“ ueber dem Hinweis
        var fAn = modus && !DaumenKnoepfeAn && haupt is not null;
        _fAnzeige.IsVisible = fAn;
        if (fAn && haupt is { } h)
        {
            var taste = Tastenbelegung.Aktuell.Tasten(Tastenbelegung.Handeln).Count > 0
                ? Tastenbelegung.Aktuell.AnzeigeFuer(Tastenbelegung.Handeln) : null;
            var text = taste is null ? h.Name : string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.HandlungTaste, taste, h.Name);
            if (_fAnzeige.Content is TextBlock block)
            {
                block.Text = text;
            }
            else
            {
                _fAnzeige.Content = text;
            }
            Avalonia.Automation.AutomationProperties.SetName(_fAnzeige, text);
        }
        UntenSetzen();
        HinweisObenZeigen();
        if (_auswahl is { IsVisible: true })
        {
            if (!modus || spiel is null || spiel.Handlungen.Count == 0)
            {
                AuswahlZu();
            }
            else
            {
                AuswahlFuellen(spiel.Handlungen);
            }
        }
        HandlungKnoepfeLegen();
    }

    /// <summary>Lage, Symbol und Sichtbarkeit von Handlungsknopf, Name, Ich-Sicht und Auswahl (nach jeder Groessenaenderung).</summary>
    private void HandlungKnoepfeLegen()
    {
        if (_handlungRund is null || _ichRund is null || _handlungName is null || _auswahl is null)
        {
            return;
        }
        var bereich = Flaeche.StickBereich;
        // Welle A 1.9.0 (Sammelliste 450): eigene Anordnung (Lage und Groesse je Knopf); null = Standard wie A 1.8.0
        var eigene = DaumenKnoepfeAn ? EigeneLagen : null;
        ((double X, double Y) Handlung, (double X, double Y) Ich)? lage = null;
        if (eigene is not null)
        {
            lage = (Mitte(eigene[KnopfAnordnung.Knopf.Handlung]), Mitte(eigene[KnopfAnordnung.Knopf.Ich]));
        }
        else if (DaumenKnoepfeAn && HandlungKnopf.Lage(bereich) is { } standard)
        {
            lage = (standard.Handlung, standard.Ich);
        }
        var f0 = Flaeche.TranslatePoint(default, this);
        var an = HandlungModus && Ebene.IsVisible && _anordnen is null && !HandyOffen && !(Tafel?.IsVisible ?? false) && f0 is not null
            && (!DaumenKnoepfeAn || lage is not null);
        var haupt = an ? Spiel?.Haupthandlung : null;
        _handlungRund.IsVisible = an && DaumenKnoepfeAn && haupt is not null;
        _handlungName.IsVisible = _handlungRund.IsVisible;
        _ichRund.IsVisible = an;
        var handyLage = eigene is not null ? Mitte(eigene[KnopfAnordnung.Knopf.Telefon]) : DaumenKnoepfeAn ? HandlungKnopf.Telefon(bereich) : null;
        var handyAn = an && Spiel is not null && (!DaumenKnoepfeAn || handyLage is not null);
        if (_handyRund is not null && _handyZahlRand is not null)
        {
            // Sichtbarkeit nur bei einer Aenderung setzen (ein Wechsel hin und her bei jedem Anordnen gibt eine Schleife im Layout)
            _handyRund.IsVisible = handyAn;
            if (!handyAn || f0 is null)
            {
                _handyZahlRand.IsVisible = false;
            }
        }
        if (!an || f0 is not { } p0)
        {
            _auswahl.IsVisible = _auswahl.IsVisible && an;
            return;
        }
        var (g, k) = HandlungKnopf.Groesse(bereich.Breite, bereich.Hoehe);
        // Welle A 1.9.0: Groesse je Knopf (Ich-Sicht und Handy heute gleich gross, eigene Groesse je Knopf)
        var kHandy = k;
        if (DaumenKnoepfeAn && eigene is not null)
        {
            g = KnopfAnordnung.Durchmesser(KnopfAnordnung.Knopf.Handlung, bereich, eigene[KnopfAnordnung.Knopf.Handlung].Prozent);
            k = KnopfAnordnung.Durchmesser(KnopfAnordnung.Knopf.Ich, bereich, eigene[KnopfAnordnung.Knopf.Ich].Prozent);
            kHandy = KnopfAnordnung.Durchmesser(KnopfAnordnung.Knopf.Telefon, bereich, eigene[KnopfAnordnung.Knopf.Telefon].Prozent);
        }
        double? Treffer(double d) => DaumenKnoepfeAn && eigene is not null ? KnopfAnordnung.Treffer(d) : null;
        _ichRund.Setzen(k, DaumenSymbol.Auge, Flaeche.IchAn, Flaeche.IchAn ? SpielProbeTexte.IchSichtBeenden : SpielProbeTexte.IchSichtKnopf, treffer: Treffer(k));
        double obenKante;
        if (DaumenKnoepfeAn && lage is { } l)
        {
            Legen(_ichRund, l.Ich, k);
            if (handyLage is { } hl)
            {
                HandyLegen(hl);
            }
            obenKante = Math.Min(l.Handlung.Y - (g / 2), l.Ich.Y - (k / 2));
            if (haupt is { } h)
            {
                _handlungRund.Setzen(g, h.Art switch
                {
                    HandlungArt.Tuer => DaumenSymbol.Tuer,
                    HandlungArt.Bus => DaumenSymbol.Bus,
                    _ => DaumenSymbol.Hand,
                }, false, h.Name, treffer: Treffer(g));
                Legen(_handlungRund, l.Handlung, g);
                _handlungNameText.Text = h.Name;
                _handlungName.Measure(Size.Infinity);
                var ds = _handlungName.DesiredSize;
                // ueber dem Knopf; ist dort unter der Kopfzeile kein Platz (Handy quer), rechts neben dem Auge (Weisung P12-363)
                var nameLage = eigene is not null
                    ? HandlungKnopf.NameFrei(bereich, ds.Width, ds.Height, (l.Handlung, g), (l.Ich, k), KnopfAnordnung.Alle
                        .Select(kn => (Mitte(eigene[kn]), KnopfAnordnung.Durchmesser(kn, bereich, eigene[kn].Prozent))).ToList())
                    : HandlungKnopf.Name(bereich, ds.Width, ds.Height);
                if (nameLage is { } nl)
                {
                    var links = p0.X + nl.Ecke.X;
                    var oben = p0.Y + nl.Ecke.Y;
                    Canvas.SetLeft(_handlungName, links);
                    Canvas.SetTop(_handlungName, oben);
                    _handlungName.Arrange(new Rect(links, oben, ds.Width, ds.Height));
                    obenKante = Math.Min(obenKante, nl.Ecke.Y);
                }
            }
            if (_auswahl.IsVisible)
            {
                AuswahlLegen(p0.X + 8, p0.Y + obenKante - 8, false);
            }
            return;
        }
        // PC: Ich-Sicht unten links; liegt dort die untere Karte, steht es ueber ihr
        var h0 = Flaeche.Bounds.Height;
        var x = 16 + (k / 2);
        var y = h0 - 16 - (k / 2);
        if (Flaeche.Frei.Unten > 0 && x + (k / 2) + 8 > Flaeche.LeisteX.Von)
        {
            y = h0 - Flaeche.Frei.Unten - 8 - (k / 2);
        }
        Legen(_ichRund, (x, y), k);
        // PC (E1, Weisung P12-361): das Handy-Symbol rechts neben der Ich-Sicht
        HandyLegen((x + k + 8, y));
        if (_auswahl.IsVisible)
        {
            var mitte = Flaeche.LeisteX.Bis > Flaeche.LeisteX.Von ? (Flaeche.LeisteX.Von + Flaeche.LeisteX.Bis) / 2 : Flaeche.Bounds.Width / 2;
            AuswahlLegen(p0.X + mitte, p0.Y + h0 - Flaeche.Frei.Unten - 8, true);
        }

        void HandyLegen((double X, double Y) m)
        {
            if (!handyAn || _handyRund is null || _handyZahlRand is null || Spiel is not { } spiel)
            {
                return;
            }
            var zahl = Questlog.ZahlOffen(spiel);
            _handyRund.Setzen(kHandy, DaumenSymbol.Telefon, false,
                SpielProbeTexte.KnopfTelefon + ", " + string.Format(CultureInfo.InvariantCulture, VorleseTexte.Auftraege, zahl), treffer: Treffer(kHandy));
            Legen(_handyRund, m, kHandy);
            var text = zahl.ToString(CultureInfo.InvariantCulture);
            if (_handyZahl.Text != text)
            {
                _handyZahl.Text = text;
            }
            _handyZahlRand.IsVisible = zahl > 0;
            if (zahl > 0)
            {
                // Zahl oben rechts am Symbol (halb darueber), nie ueber den Rand der Seite
                _handyZahlRand.Measure(Size.Infinity);
                var ds = _handyZahlRand.DesiredSize;
                var links = Math.Min(p0.X + m.X + (kHandy * 0.2), Bounds.Width - ds.Width - 2);
                var oben = Math.Max(p0.Y + m.Y - (kHandy / 2) - (ds.Height * 0.35), 2);
                Canvas.SetLeft(_handyZahlRand, links);
                Canvas.SetTop(_handyZahlRand, oben);
                _handyZahlRand.Arrange(new Rect(links, oben, ds.Width, ds.Height));
            }
        }

        void Legen(DaumenKnopf knopf, (double X, double Y) m, double d0)
        {
            // Welle A 1.9.0: die Flaeche zum Tippen (bei eigener Groesse unter 40 Punkten groesser als der Knopf)
            var d = Math.Max(d0, knopf.Width);
            Canvas.SetLeft(knopf, p0.X + m.X - (d / 2));
            Canvas.SetTop(knopf, p0.Y + m.Y - (d / 2));
            // sofort an die neue Stelle (Sammelliste 162)
            knopf.Arrange(new Rect(Canvas.GetLeft(knopf), Canvas.GetTop(knopf), d, d));
        }
    }

    /// <summary>Auswahl mit der unteren Kante bei <paramref name="unten"/> (links bzw. mittig an <paramref name="x"/>), nie ueber die Seite hinaus.</summary>
    private void AuswahlLegen(double x, double unten, bool mittig)
    {
        if (_auswahl is null)
        {
            return;
        }
        _auswahl.Measure(new Size(Math.Min(300, Math.Max(0, Bounds.Width - 16)), Math.Max(0, Bounds.Height - 16)));
        var ds = _auswahl.DesiredSize;
        var links = Math.Clamp(mittig ? x - (ds.Width / 2) : x, 8, Math.Max(8, Bounds.Width - ds.Width - 8));
        var oben = Math.Clamp(unten - ds.Height, 8, Math.Max(8, Bounds.Height - ds.Height - 8));
        Canvas.SetLeft(_auswahl, links);
        Canvas.SetTop(_auswahl, oben);
        _auswahl.Arrange(new Rect(links, oben, ds.Width, ds.Height));
    }

    /// <summary>
    /// Welle A 1.13.0 (Sammelliste 464): mit der Handlung steht der Hinweis oben; die Karte unten gibt es dann nur noch fuer die
    /// Anzeige „F: …“ am PC, den Text der Gegenprobe (und die Knoepfe der Probe-APK), sonst ist unten nichts mehr im Weg.
    /// Nur bei einer Aenderung (Lehre A 1.6.0); auch nach jedem neuen Hinweisfeld gerufen.
    /// </summary>
    private void UntenSetzen()
    {
        var untenDa = !HandlungModus || (_fAnzeige?.IsVisible ?? false) || _zeileRolle.IsVisible || (_knoepfeRolle.IsVisible && _knoepfe.Children.Any(c => c.IsVisible));
        if (_unten.IsVisible != untenDa)
        {
            _unten.IsVisible = untenDa;
        }
    }
}
