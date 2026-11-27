using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>Ein Eintrag der Navigation (Kennung, Beschriftung, Unterpunkte).</summary>
/// <summary>A 0.12.2: Symbol (aus) und SymbolAktiv (gewaehlt) wie NAV am Handy.</summary>
public sealed record NavPunkt(string Id, string Titel, IReadOnlyList<NavPunkt> Unter, string Symbol = "", string SymbolAktiv = "");

/// <summary>
/// Phase 1, K6: Seitenleiste (breit, Eintraege wie am PC) und untere Leiste
/// (schmal, 6 Eintraege wie am Handy). Beide werden einmal gebaut; je nach
/// Fensterbreite ist eine davon sichtbar.
/// </summary>
public sealed class Navigationsleisten
{
    private readonly Action<string> _oeffnen;
    private readonly Dictionary<string, Button> _seitenKnoepfe = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _untenKnoepfe = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Border Pille, SatzSymbol Symbol, TextBlock Text)> _untenTeile = new(StringComparer.Ordinal);

    public Control Seitenleiste { get; }
    public Control UntereLeiste { get; }

    public Navigationsleisten(Action<string> oeffnen)
    {
        _oeffnen = oeffnen;
        Seitenleiste = BaueSeitenleiste();
        UntereLeiste = BaueUntereLeiste();
    }

    /// <summary>
    /// Phase 8 W2b (Sammelliste 114/115): Seitenleiste wie Python 0.63.2 PC (Sidebar, NavRow): Logo-Ring 34
    /// (Dicke 5, Punkt 12) mit "FI" (21 fett) und "Lernplattform" (11 muted); je Zeile Symbol 22 (Unterpunkte 20)
    /// und Text 14 fett text_dim (Unterpunkte 13 muted, je Ebene 12 eingerueckt), Aufklapp-Pfeil bei Gruppen.
    /// A 0.38.0 (Sammelliste 207): Eintraege wie die untere Leiste am Handy (Start, Lernen, Rechner, Spiel,
    /// Fortschritt, Optionen), Gruppen zugeklappt, nur eine offen; unten angeheftet Hilfe, Programm beenden,
    /// Kasten LERNSTATUS (11 fett muted, Balken "Karten und Fragen bearbeitet" mit x / y) und Version (11 muted).
    /// </summary>
    private Control BaueSeitenleiste()
    {
        // Welle A 1.15.0 (Weisungen P12-406/P12-407, Variante B): Zeilen wie das Deckblatt der gewaehlten Fachrichtung
        // (Core/Texte/Pruefungsteile.cs), ohne Symbole und ohne Aufklappen, genau wie das Entwurfsbild
        var oben = new StackPanel { Spacing = 1, Margin = new Thickness(12, 0, 12, 8) };
        _oben = oben;
        ZeilenBauen(Core.Pruefung.Fachrichtung.Nach(Zustand.Sicht));
        var unten = new StackPanel { Spacing = 1, Margin = new Thickness(12, 8, 12, 0) };
        foreach (var zeile in Pruefungsteile.SeitenleisteUnten)
        {
            unten.Children.Add(Eintrag(zeile));
        }
        // Weisung P8-56: "Programm beenden" immer; Patch A 0.37.0 (Sammelliste 206, Antwort 4a) auch auf Android (Tablet quer)
        BeendenKnopf = BeendenEintrag();
        unten.Children.Add(BeendenKnopf);
        // A 1.15.0: unten angeheftet nur Hilfe, Optionen und Programm beenden (wie im Entwurf Hilfe und Optionen); LERNSTATUS und
        // Version stehen am Ende des Bildlaufs, damit bei 1280 x 800 alle Eintraege bis Fortschritt ohne Rollen zu sehen sind
        var fuss = new StackPanel { Margin = new Thickness(0, 0, 0, 12), Children = { unten } };
        var rollen = new ScrollViewer
        {
            Content = new StackPanel { Children = { oben, Version() } },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        var aufbau = new DockPanel();
        var logo = LogoZeile();
        DockPanel.SetDock(logo, Dock.Top);
        DockPanel.SetDock(fuss, Dock.Bottom);
        aufbau.Children.Add(logo);
        aufbau.Children.Add(fuss);
        aufbau.Children.Add(rollen);
        _rollen = rollen;
        var leiste = new Border
        {
            Width = 244,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = aufbau,
        };
        // Breite waechst mit der Schrift mit, damit "Abschlussprojekt" mit Symbol einzeilig bleibt
        leiste.Bind(Layoutable.WidthProperty, leiste.GetResourceObservable("Schrift.Normal", v => v is double d ? 244 * d / 14 : 244));
        return leiste.Farbe(Border.BackgroundProperty, "Pinsel.sidebar").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private ScrollViewer? _rollen;

    /// <summary>Knopf "Programm beenden" der Seitenleiste (Start-Test).</summary>
    public Button? BeendenKnopf { get; private set; }

    /// <summary>
    /// Weisung P8-56: Zeile wie <see cref="Eintrag"/> (Symbol 22, Text 14 fett text_dim, ohne Strich), schliesst das
    /// Fenster wie das Kreuz: Spielschritte schreiben, Abgleich, Sperren und Velopack laufen im Programmende (App.Exit).
    /// </summary>
    private Button BeendenEintrag()
    {
        // A 1.17.0 (Weisung P12-430, Entwurf P12-271): wie die Hauptpunkte mit Symbol (Stecker), Text buendig mit Hilfe und Optionen
        var (inhalt, _, symbol, _) = Hauptzeile(Navigation.ProgrammBeenden, Pruefungsteile.Beenden);
        _symbole[Pruefungsteile.Beenden] = symbol;
        var knopf = new Button
        {
            Content = inhalt,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
        };
        Avalonia.Automation.AutomationProperties.SetName(knopf, Navigation.ProgrammBeenden);
        knopf.Click += (_, _) => Programmende.Beenden();
        return knopf;
    }
    private readonly Dictionary<string, string> _titel = new(StringComparer.Ordinal);

    /// <summary>Beschriftung eines Eintrags der Seitenleiste (Start-Test).</summary>
    public string Titel(string id) => _titel[id];
    private readonly Dictionary<string, (Border Strich, TextBlock Text, bool Unter)> _seitenTeile = new(StringComparer.Ordinal);
    // A 1.17.0: Symbole der Hauptpunkte am PC (auch „Programm beenden“), je Kennung
    private readonly Dictionary<string, SatzSymbol> _symbole = new(StringComparer.Ordinal);

    /// <summary>Logo wie Sidebar (pady 22/20, padx 20): Ring 34 mit Punkt 12, "FI" und "Lernplattform".</summary>
    private static Control LogoZeile()
    {
        var ring = new VerlaufRing(1, "accent", "accent2", 5) { Width = 34, Height = 34 };
        var punkt = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            .Farbe(Border.BackgroundProperty, "Pinsel.accent2");
        var marke = new Viewbox { VerticalAlignment = VerticalAlignment.Center, Child = new Grid { Width = 34, Height = 34, Children = { ring, punkt } } };
        Mitwachsen(marke, 34);
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(marke, Avalonia.Automation.AccessibilityView.Raw);
        var name = Schrift.Pt(Navigation.Logo, 21, FontWeight.Bold);
        name.VerticalAlignment = VerticalAlignment.Center;
        name.Margin = new Thickness(12, 0, 0, 0);
        var zusatz = Schrift.Pt(Navigation.LogoZusatz, 11, FontWeight.Normal, "Pinsel.muted");
        zusatz.VerticalAlignment = VerticalAlignment.Center;
        zusatz.Margin = new Thickness(7, 7, 0, 0);
        var zeile = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 22, 20, 20), Children = { marke, name, zusatz } };
        Avalonia.Automation.AutomationProperties.SetName(zeile, Texte.Programmname);
        return zeile;
    }

    /// <summary>
    /// Am PC waechst die ganze Seitenleiste mit der Schrift (wie apply_ui_scale in Python 0.63.2), auch Symbole
    /// und Logo; sonst stuenden bei "Maximal" Symbole von 22 Punkten neben Text von 28.
    /// </summary>
    private static void SymbolMitwachsen(TextBlock symbol, double basis) =>
        symbol.Bind(TextBlock.FontSizeProperty, symbol.GetResourceObservable("Schrift.Normal", v => v is double d ? basis * d / Schrift.Grundgroessen["Normal"] : basis));

    private static void Mitwachsen(Control element, double basis)
    {
        element.Bind(Layoutable.WidthProperty, element.GetResourceObservable("Schrift.Normal", v => v is double d ? basis * d / Schrift.Grundgroessen["Normal"] : basis));
        element.Bind(Layoutable.HeightProperty, element.GetResourceObservable("Schrift.Normal", v => v is double d ? basis * d / Schrift.Grundgroessen["Normal"] : basis));
    }

    private StackPanel? _oben;
    private string _fachrichtung = "";

    /// <summary>
    /// Baut die Zeilen im Bildlauf fuer eine Fachrichtung (Gruppenkopf ABSCHLUSSPRÜFUNG &lt;Kürzel&gt; und Namen der Bereiche wie
    /// das Deckblatt). Nach einem Wechsel der Fachrichtung aus <see cref="MainView"/>; gleiche Fachrichtung baut nichts neu.
    /// </summary>
    public void ZeilenBauen(Core.Pruefung.Fachrichtung fachrichtung)
    {
        if (_oben is null || _fachrichtung == fachrichtung.Id)
        {
            return;
        }
        _fachrichtung = fachrichtung.Id;
        foreach (var id in _oben.Children.OfType<Button>().Select(k => _seitenKnoepfe.First(e => e.Value == k).Key).ToList())
        {
            _seitenKnoepfe.Remove(id);
            _seitenTeile.Remove(id);
            _titel.Remove(id);
            _symbole.Remove(id);
        }
        _oben.Children.Clear();
        foreach (var zeile in Pruefungsteile.Seitenleiste(fachrichtung))
        {
            _oben.Children.Add(zeile.Art == LeistenArt.Gruppe ? Gruppenkopf(zeile.Titel) : Eintrag(zeile));
        }
        if (_markiert is { } id2)
        {
            Markiere(id2, _markiertEltern);
        }
    }

    /// <summary>Start-Test: Zeilen der Seitenleiste in der Reihenfolge der Anzeige (Art, Kennung, Text).</summary>
    public IReadOnlyList<LeistenZeile> ZeilenFuerTests =>
        [.. (_oben?.Children ?? []).Select(c => c is Button k
            ? new LeistenZeile(_seitenTeile[_seitenKnoepfe.First(e => e.Value == k).Key].Unter ? LeistenArt.Unter : LeistenArt.Eintrag,
                _seitenKnoepfe.First(e => e.Value == k).Key, _seitenTeile[_seitenKnoepfe.First(e => e.Value == k).Key].Text.Text ?? "")
            : new LeistenZeile(LeistenArt.Gruppe, null, (c as TextBlock)?.Text ?? ""))];

    /// <summary>Gruppenkopf wie im Entwurf P12-271: klein (11), fett, text_dim, Abstand oben 8 und unten 3.</summary>
    private static TextBlock Gruppenkopf(string titel)
    {
        var text = Schrift.Pt(titel, 11, FontWeight.Bold, "Pinsel.text_dim");
        text.LetterSpacing = 0.9;
        text.TextWrapping = TextWrapping.Wrap;
        text.Margin = new Thickness(12, 8, 12, 3);
        return text;
    }

    /// <summary>Symbolgroesse der Hauptpunkte am PC (waechst mit der Schrift wie die ganze Seitenleiste).</summary>
    public const double SymbolPc = 20;

    /// <summary>
    /// A 1.17.0 (Entwurf P12-271 Satz 3): Hauptzeile = Symbol 20, 12 Abstand, Text 14 halbfett in text, darueber der Rand
    /// links (3, folgt der runden Ecke wie im Entwurf, nur gewaehlt sichtbar). Liefert Inhalt (mit 7 Abstand oben und unten),
    /// Rand, Symbol und Text.
    /// </summary>
    private static (Panel Inhalt, Border Strich, SatzSymbol Symbol, TextBlock Text) Hauptzeile(string titel, string id)
    {
        var strich = new Border { BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(12), IsHitTestVisible = false };
        var symbol = new SatzSymbol(Pruefungsteile.Symbole[id], "Pinsel.muted", "Pinsel.sidebar", SymbolPc) { VerticalAlignment = VerticalAlignment.Center };
        Mitwachsen(symbol, SymbolPc);
        var symbolRahmen = new Border { Padding = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = symbol };
        var text = Schrift.Pt(titel, 14, FontWeight.SemiBold, "Pinsel.text");
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.Wrap;
        // Abstand als Rand eines Rahmens, nicht des Textes (sonst gilt er in der Layoutpruefung als abgeschnitten)
        var textRahmen = new Border { Padding = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Child = text };
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(3, 7, 0, 7) };
        Grid.SetColumn(textRahmen, 1);
        zeile.Children.Add(symbolRahmen);
        zeile.Children.Add(textRahmen);
        return (new Panel { Children = { zeile, strich } }, strich, symbol, text);
    }

    /// <summary>
    /// Zeile wie im Entwurf P12-271: Hauptpunkt mit Symbol, Text 14 halbfett (<see cref="Hauptzeile"/>), gewaehlt card_hi mit
    /// Rand 3 links in accent und Symbol in accent; Unterpunkt ohne Symbol, 13 text_dim, buendig unter dem Text der
    /// Hauptpunkte (48), gewaehlt fett in text ohne Flaeche (die Flaeche traegt sein Hauptpunkt).
    /// </summary>
    private Button Eintrag(LeistenZeile zeile)
    {
        var id = zeile.Id!;
        var unter = zeile.Art == LeistenArt.Unter;
        Border strich;
        TextBlock text;
        Control inhalt;
        if (unter)
        {
            strich = new Border { Width = 0 };
            text = Schrift.Pt(zeile.Titel, 13, FontWeight.Normal, "Pinsel.text_dim");
            text.VerticalAlignment = VerticalAlignment.Center;
            text.TextWrapping = TextWrapping.Wrap;
            // Abstand als Rand eines Rahmens, nicht des Textes (sonst gilt er in der Layoutpruefung als abgeschnitten)
            inhalt = new Border { Padding = new Thickness(48, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Child = text };
        }
        else
        {
            (var raster, strich, var symbol, text) = Hauptzeile(zeile.Titel, id);
            _symbole[id] = symbol;
            inhalt = raster;
        }
        var knopf = new Button
        {
            Content = inhalt,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0, unter ? 5 : 0),
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
        };
        Avalonia.Automation.AutomationProperties.SetName(knopf, zeile.Titel);
        knopf.Click += (_, _) => _oeffnen(id);
        _seitenKnoepfe[id] = knopf;
        _seitenTeile[id] = (strich, text, unter);
        _titel[id] = zeile.Titel;
        return knopf;
    }

    private static Control Version()
    {
        var text = Schrift.Pt(Beschriftungen.Einsetzen(Navigation.Fusszeile, Aktualisierung.Aktualisierung.Anzeige), 11, FontWeight.Normal, "Pinsel.muted");
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.Margin = new Thickness(0, 8, 0, 14);
        return text;
    }

    /// <summary>Start-Test (A 1.15.0, Weisung P12-409): alle Texte der Seitenleiste, fuer die Gegenprobe ohne Kasten LERNSTATUS.</summary>
    public IReadOnlyList<string> SeitenleisteTexteFuerTests =>
        [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(Seitenleiste).OfType<TextBlock>().Select(t => t.Text ?? "")];

    /// <summary>Start-Test: Klick auf einen Eintrag der Seitenleiste wie mit der Maus.</summary>
    public void KlickFuerTests(string id) => _seitenKnoepfe[id].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Start-Test (Hinweis Chat, Weisung P11-37): bekommt der Eintrag den Fokus wie per Tab, steht er danach ganz im
    /// sichtbaren Teil des Rollbereichs? Liefert oben und unten (Bildpunkte relativ zum sichtbaren Teil) und die Hoehe.
    /// </summary>
    public async Task<(double Oben, double Unten, double Sichtbar)?> FokusSichtbarFuerTestsAsync(string id)
    {
        if (_rollen is null || !_seitenKnoepfe.TryGetValue(id, out var knopf) || !knopf.IsEffectivelyVisible)
        {
            return null;
        }
        knopf.Focus(Avalonia.Input.NavigationMethod.Tab);
        for (var i = 0; i < 3; i++)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
        }
        if (knopf.TranslatePoint(new Point(0, 0), _rollen) is not { } p)
        {
            return null;
        }
        return (p.Y, p.Y + knopf.Bounds.Height, _rollen.Viewport.Height);
    }

    /// <summary>Symbolgroesse der unteren Leiste (waechst mit der Schrift).</summary>
    public const double SymbolUnten = 24;

    /// <summary>Weisung P12-432: Masse der Pille wie im Entwurf P12-271 (Breite 60 x Faktor bis 64, Hoehe 32 x Faktor bis 1,5).</summary>
    public const double PilleBreite = 60, PilleBreiteHoechstens = 64, PilleHoehe = 32;

    /// <summary>
    /// A 1.17.0 (Weisung P12-430): Symbol der unteren Leiste aus Satz 3 (<see cref="Symbole.Satz3"/>), nicht gewaehlt Form
    /// muted auf sidebar; die Hervorhebung setzt Form accent auf card_hi (<see cref="Markiere"/>). Groesse 24, waechst mit der Schrift.
    /// </summary>
    private static SatzSymbol UntenSymbol(string name)
    {
        var symbol = new SatzSymbol(name, "Pinsel.muted", "Pinsel.sidebar", SymbolUnten)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Weisung P12-432 (Nico: Pille breit wie im Entwurf): das Symbol waechst wie im Entwurf nur bis zum 1,35-fachen
        Unten(symbol, Layoutable.WidthProperty, f => SymbolUnten * Math.Min(f, 1.35));
        Unten(symbol, Layoutable.HeightProperty, f => SymbolUnten * Math.Min(f, 1.35));
        return symbol;
    }

    /// <summary>Bindet eine Groesse der unteren Leiste an den Schriftfaktor (Faktor 1 = Stufe Normal).</summary>
    private static void Unten(Control element, AvaloniaProperty<double> eigenschaft, Func<double, double> wert) =>
        element.Bind(eigenschaft, element.GetResourceObservable("Schrift.Normal",
            v => wert(v is double d ? d / Schrift.Grundgroessen["Normal"] : 1)));

    private Control BaueUntereLeiste()
    {
        // Grid mit gleich breiten Spalten statt UniformGrid: UniformGrid rundet jede Zelle auf
        // ganze Punkte und schiebt die letzte bis 2 Punkte ueber den Rand (Layoutpruefung 1360 breit)
        // A 0.12.2 wie ft.NavigationBar am Handy: Hoehe 80, Symbol 24 (aus: OUTLINED muted, an: ROUNDED
        // accent) in einer Pille 64 x 32 (card_hi), Beschriftung darunter text_dim
        var raster = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", Texte.NavigationHandy.Count))) };
        foreach (var punkt in Texte.NavigationHandy)
        {
            // A 1.17.0 (Entwurf P12-271): Wort nicht gewaehlt muted, gewaehlt text halbfett (Markiere)
            var beschriftung = new TextBlock
            {
                Text = punkt.Titel,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Center,
            }.Groesse("Leiste");
            beschriftung.FarbeNeu(TextBlock.ForegroundProperty, "Pinsel.muted");
            var symbol = UntenSymbol(punkt.Symbol);
            var pille = new Border
            {
                CornerRadius = new CornerRadius(999),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = symbol,
                Background = Brushes.Transparent,
            };
            // Weisung P12-432 (Nico: „breit wie im Entwurf“, nie rund): Masse genau wie der Entwurf P12-271, Breite min(60 x f, 64),
            // Hoehe 32 x min(f, 1,5); in jeder Schriftstufe breiter als hoch (hoechstens 64 x 48) und am schmalen Handy in der Spalte
            Unten(pille, Layoutable.WidthProperty, f => Math.Min(PilleBreite * f, PilleBreiteHoechstens));
            Unten(pille, Layoutable.HeightProperty, f => PilleHoehe * Math.Min(f, 1.5));
            // Weisung P12-433 (Entwurf gilt woertlich): Abstand Pille-Wort 4 x f, Rand oben 8 und unten 12, keine Mindesthoehe
            var stapel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { pille, beschriftung } };
            Unten(stapel, StackPanel.SpacingProperty, f => 4 * f);
            var knopf = new Button
            {
                Content = stapel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 8, 0, 12),
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(0),
            };
            Avalonia.Automation.AutomationProperties.SetName(knopf, punkt.Titel);
            knopf.Click += (_, _) => _oeffnen(punkt.Id);
            _untenKnoepfe[punkt.Id] = knopf;
            _untenTeile[punkt.Id] = (pille, symbol, beschriftung);
            Grid.SetColumn(knopf, raster.Children.Count);
            raster.Children.Add(knopf);
        }
        // Entwurf P12-271: Strich 1 oben in der Randfarbe
        var leiste = new Border { Child = raster, IsVisible = false, BorderThickness = new Thickness(0, 1, 0, 0) };
        leiste.Farbe(Border.BorderBrushProperty, "Pinsel.border");
        return leiste.Farbe(Border.BackgroundProperty, "Pinsel.sidebar");
    }

    /// <summary>
    /// Sammelliste 341 (Start-Test): welche Eintraege gerade markiert aussehen, je Teil die Kennungen, deren Farbe genau die
    /// Ressource der Markierung der aktuellen Farbwelt ist. A 1.17.0 (Entwurf P12-271): Knopf card_hi, Strich (Rand links)
    /// accent und Symbol accent am Hauptpunkt; Text fett in text am Unterpunkt; unten Pille card_hi, Symbol accent und Wort
    /// text; seit P12-434 Pille und Grund des Symbols unten Pinsel.pille (<see cref="Core.Design.Leistenpille"/>). Nur fuer Tests.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> MarkiertFuerTests()
    {
        object? Res(string schluessel) => Seitenleiste.TryFindResource(schluessel, out var wert) ? wert : null;
        var hi = Res("Pinsel.card_hi");
        var pille = Res("Pinsel.pille");
        var akzent = Res("Pinsel.accent");
        var text = Res("Pinsel.text");
        IReadOnlyList<string> Wo<T>(IEnumerable<KeyValuePair<string, T>> quelle, Func<T, bool> markiert) =>
            quelle.Where(q => markiert(q.Value)).Select(q => q.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Knopf"] = hi is null ? [] : Wo(_seitenKnoepfe, k => ReferenceEquals(k.Background, hi)),
            ["Strich"] = akzent is null ? [] : Wo(_seitenTeile, t => !t.Unter && ReferenceEquals(t.Strich.BorderBrush, akzent)),
            ["Symbol"] = akzent is null ? [] : Wo(_symbole, y => ReferenceEquals(y.Form, akzent)),
            ["Text"] = text is null ? [] : Wo(_seitenTeile, t => t.Unter && ReferenceEquals(t.Text.Foreground, text) && t.Text.FontWeight == FontWeight.Bold),
            ["Pille"] = pille is null ? [] : Wo(_untenTeile, t => ReferenceEquals(t.Pille.Background, pille)),
            ["SymbolUnten"] = akzent is null ? [] : Wo(_untenTeile, t => ReferenceEquals(t.Symbol.Form, akzent) && ReferenceEquals(t.Symbol.Grund, pille)),
            ["WortUnten"] = text is null ? [] : Wo(_untenTeile, t => ReferenceEquals(t.Text.Foreground, text) && t.Text.FontWeight == FontWeight.SemiBold),
        };
    }

    /// <summary>
    /// A 1.17.0 (Start-Test): was fuer eine Seite markiert sein soll, aus der Zuordnung der Leisten (<see cref="Pruefungsteile.Eltern"/>,
    /// <see cref="Pruefungsteile.Oberpunkt"/>), gleiche Schluessel wie <see cref="MarkiertFuerTests"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SollMarkiertFuerTests(string id, string? eltern = null)
    {
        var seite = SeitenZiel(id, eltern);
        var haupt = Haupt(seite);
        var unten = UntenZiel(id, eltern);
        IReadOnlyList<string> Eins(string? k) => string.IsNullOrEmpty(k) ? [] : [k];
        var unterpunkt = seite.Length > 0 && _seitenTeile.TryGetValue(seite, out var t) && t.Unter ? seite : null;
        return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Knopf"] = Eins(haupt),
            ["Strich"] = Eins(haupt),
            ["Symbol"] = Eins(haupt),
            ["Text"] = Eins(unterpunkt),
            ["Pille"] = Eins(unten),
            ["SymbolUnten"] = Eins(unten),
            ["WortUnten"] = Eins(unten),
        };
    }

    /// <summary>
    /// A 1.17.0 (Weisung P12-430 B6, Layoutpruefung „Leiste passt in jeder Schriftstufe“): ist die untere Leiste zu sehen, muss in
    /// jeder Spalte das ganze Wort (ohne Umbruch und ohne Kuerzen gemessen) und die Pille mit dem Symbol innerhalb des Knopfs
    /// liegen, und die Leiste muss ganz im Fenster liegen. Leer = passt. <paramref name="gegenprobeWort"/> misst fuer den ersten
    /// Knopf statt seines Worts dieses Wort (nur fuer die Gegenprobe im Start-Test; die Oberflaeche bleibt unveraendert).
    /// </summary>
    public IReadOnlyList<string> LeistePasstFunde(string? gegenprobeWort = null)
    {
        var funde = new List<string>();
        if (!UntereLeiste.IsEffectivelyVisible)
        {
            return funde;
        }
        var wurzel = TopLevel.GetTopLevel(UntereLeiste);
        if (wurzel is not null && UntereLeiste.TranslatePoint(new Point(0, 0), wurzel) is { } oben
            && (oben.X < -0.5 || oben.X + UntereLeiste.Bounds.Width > wurzel.Bounds.Width + 0.5 || oben.Y + UntereLeiste.Bounds.Height > wurzel.Bounds.Height + 0.5))
        {
            funde.Add($"untere Leiste ragt aus dem Fenster ({oben.X:0.#}/{oben.Y:0.#}, {UntereLeiste.Bounds.Width:0.#} x {UntereLeiste.Bounds.Height:0.#})");
        }
        foreach (var (id, knopf) in _untenKnoepfe)
        {
            var (pille, _, wort) = _untenTeile[id];
            // Wortbreite ohne Grenze gemessen (wie es ungekuerzt braucht), nach Bildpunkt-Rundung verglichen
            var probe = new TextBlock { Text = gegenprobeWort is not null && funde.Count == 0 && id == _untenKnoepfe.Keys.First() ? gegenprobeWort : wort.Text, FontSize = wort.FontSize, FontWeight = FontWeight.SemiBold, FontFamily = wort.FontFamily };
            probe.Measure(Size.Infinity);
            var breit = Math.Ceiling(probe.DesiredSize.Width);
            var platz = Math.Floor(knopf.Bounds.Width);
            if (breit > platz)
            {
                funde.Add($"untere Leiste „{probe.Text}“: Wort {breit:0} breiter als die Spalte {platz:0}");
            }
            // Weisung P12-432 (Nico: „breit wie im Entwurf“, nie rund): die Pille ist in jeder Stufe deutlich breiter als hoch
            if (pille.Bounds.Width < 1.3 * pille.Bounds.Height)
            {
                funde.Add($"untere Leiste „{wort.Text}“: Pille nicht breit ({pille.Bounds.Width:0.#} x {pille.Bounds.Height:0.#})");
            }
            foreach (var (name, teil) in new (string, Control)[] { ("Wort", wort), ("Pille", pille) })
            {
                if (teil.TranslatePoint(new Point(0, 0), knopf) is { } p
                    && (p.X < -0.5 || p.Y < -0.5 || p.X + teil.Bounds.Width > knopf.Bounds.Width + 0.5 || p.Y + teil.Bounds.Height > knopf.Bounds.Height + 0.5))
                {
                    funde.Add($"untere Leiste „{wort.Text}“: {name} ragt aus dem Knopf ({p.X:0.#}/{p.Y:0.#}, {teil.Bounds.Width:0.#} x {teil.Bounds.Height:0.#} in {knopf.Bounds.Width:0.#} x {knopf.Bounds.Height:0.#})");
                }
            }
        }
        return funde;
    }

    /// <summary>Sammelliste 341: Kennung des Eintrags der unteren Leiste, der zu einer Seite gehoert (null = keiner).</summary>
    public string? UntenZielFuerTests(string id, string? eltern = null) => UntenZiel(id, eltern) is { Length: > 0 } z ? z : null;

    /// <summary>Sammelliste 341: alle Kennungen der Seitenleiste (nur fuer Tests).</summary>
    public IReadOnlyList<string> SeitenKennungenFuerTests => _seitenKnoepfe.Keys.ToList();

    /// <summary>A 1.17.0 (Start-Test): die Symbole der Leisten (Seitenleiste je Kennung, untere Leiste je Kennung).</summary>
    public (IReadOnlyDictionary<string, SatzSymbol> Seite, IReadOnlyDictionary<string, SatzSymbol> Unten) SymboleFuerTests =>
        (_symbole, _untenTeile.ToDictionary(t => t.Key, t => t.Value.Symbol, StringComparer.Ordinal));

    private string? _markiert;
    private string? _markiertEltern;

    /// <summary>
    /// Hebt den Eintrag der offenen Seite hervor. Zeigt eine Leiste die Seite nicht selbst, gilt der Eintrag aus
    /// <paramref name="eltern"/> (Herkunft, z. B. der Pruefungsteil, aus dem eine Uebung kam) bzw. <see cref="Pruefungsteile.Eltern"/>.
    /// Am PC traegt der Hauptpunkt die Flaeche (auch fuer seine Unterpunkte), der Unterpunkt wird fett.
    /// </summary>
    public void Markiere(string id, string? eltern = null)
    {
        _markiert = id;
        _markiertEltern = eltern;
        var seite = SeitenZiel(id, eltern);
        var haupt = Haupt(seite);
        foreach (var (kennung, knopf) in _seitenKnoepfe)
        {
            Hervorheben(knopf, kennung, _seitenTeile[kennung].Unter ? kennung == seite : kennung == haupt);
        }
        var unten = UntenZiel(id, eltern);
        foreach (var (kennung, (pille, symbol, wort)) in _untenTeile)
        {
            var aktiv = kennung == unten;
            if (aktiv)
            {
                pille.FarbeNeu(Border.BackgroundProperty, "Pinsel.pille");
            }
            else
            {
                // Sammelliste 341: ClearValue beendet die Bindung nicht (nach hell/dunkel kam die Markierung zurueck)
                Markierung.Loesen(pille, Border.BackgroundProperty);
                pille.Background = Brushes.Transparent;
            }
            symbol.FarbenSetzen(aktiv ? "Pinsel.accent" : "Pinsel.muted", aktiv ? "Pinsel.pille" : "Pinsel.sidebar");
            wort.FarbeNeu(TextBlock.ForegroundProperty, aktiv ? "Pinsel.text" : "Pinsel.muted");
            wort.FontWeight = aktiv ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }

    /// <summary>Hauptpunkt zu einem Eintrag der Seitenleiste (Unterpunkt: sein Oberpunkt; leer = keiner).</summary>
    private string Haupt(string seite) =>
        seite.Length == 0 ? "" : _seitenTeile.TryGetValue(seite, out var t) && t.Unter ? Pruefungsteile.Oberpunkt(seite) ?? "" : seite;

    /// <summary>Eintrag der Seitenleiste zu einer Seite (leer = keiner); „Prüfung“ selbst steht fuer die gezeigte Haelfte.</summary>
    private string SeitenZiel(string id, string? eltern)
    {
        if (_seitenKnoepfe.ContainsKey(id))
        {
            return id;
        }
        if (eltern is not null)
        {
            return SeitenZiel(eltern, null);
        }
        return Pruefungsteile.Eltern(id, false) is { } e && _seitenKnoepfe.ContainsKey(e) ? e : "";
    }

    /// <summary>Eintrag der unteren Leiste zu einer Seite (leer = keiner).</summary>
    private string UntenZiel(string id, string? eltern)
    {
        if (_untenTeile.ContainsKey(id))
        {
            return id;
        }
        if (eltern is not null)
        {
            return UntenZiel(eltern, null);
        }
        return Pruefungsteile.Eltern(id, true) is { } e && _untenTeile.ContainsKey(e) ? e : "";
    }

    /// <summary>
    /// A 1.17.0 (Entwurf P12-271, Nico „genau so“): Hauptpunkt gewaehlt card_hi, Rand links accent, Symbol accent auf card_hi;
    /// sonst durchsichtig, Symbol muted auf sidebar. Unterpunkt gewaehlt fett in text, sonst normal in text_dim.
    /// </summary>
    private void Hervorheben(Button knopf, string id, bool aktiv)
    {
        var teile = _seitenTeile[id];
        if (aktiv && !teile.Unter)
        {
            knopf.FarbeNeu(Button.BackgroundProperty, "Pinsel.card_hi");
            teile.Strich.FarbeNeu(Border.BorderBrushProperty, "Pinsel.accent");
        }
        else
        {
            Markierung.Loesen(knopf, Button.BackgroundProperty);
            knopf.Background = Brushes.Transparent;
            Markierung.Loesen(teile.Strich, Border.BorderBrushProperty);
            teile.Strich.BorderBrush = Brushes.Transparent;
        }
        if (_symbole.TryGetValue(id, out var symbol))
        {
            symbol.FarbenSetzen(aktiv ? "Pinsel.accent" : "Pinsel.muted", aktiv ? "Pinsel.card_hi" : "Pinsel.sidebar");
        }
        if (teile.Unter)
        {
            teile.Text.FarbeNeu(TextBlock.ForegroundProperty, aktiv ? "Pinsel.text" : "Pinsel.text_dim");
            teile.Text.FontWeight = aktiv ? FontWeight.Bold : FontWeight.Normal;
        }
        if (aktiv)
        {
            // Sidebar.set_active (B-D-09): den aktiven Eintrag ganz ins Bild rollen
            Avalonia.Threading.Dispatcher.UIThread.Post(() => knopf.BringIntoView(), Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }
}
