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
    private readonly Dictionary<string, StackPanel> _unterpunkte = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Border Pille, TextBlock Aus, TextBlock An)> _untenTeile = new(StringComparer.Ordinal);

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
    /// (Dicke 5, Punkt 12) mit "FI" (21 fett) und "Lernplattform" (11 muted); Eintraege in Gruppen mit kleiner
    /// Ueberschrift (11 fett muted wie MENÜ), je Zeile Symbol 22 (Unterpunkte 20) und Text 14 fett text_dim
    /// (Unterpunkte 13 muted), Aufklapp-Pfeil bei Karteikarten und Spiel; unten angeheftet Hilfe, Optionen,
    /// Kasten LERNSTATUS (11 fett muted, Balken "Karten und Fragen bearbeitet" mit x / y) und Version (11 muted).
    /// </summary>
    private Control BaueSeitenleiste()
    {
        foreach (var punkt in Texte.NavigationPc)
        {
            _punkte[punkt.Id] = punkt;
        }
        var oben = new StackPanel { Spacing = 1, Margin = new Thickness(12, 0, 12, 8) };
        foreach (var (titel, ids) in Navigation.PcGruppen)
        {
            if (titel is not null)
            {
                oben.Children.Add(Gruppe(titel));
            }
            foreach (var id in ids)
            {
                EintragMitUnterpunkten(_punkte[id], oben);
            }
        }
        var unten = new StackPanel { Spacing = 1, Margin = new Thickness(12, 8, 12, 0) };
        foreach (var id in Navigation.PcUnten)
        {
            EintragMitUnterpunkten(_punkte[id], unten);
        }
        // Weisung P8-56 (Nico, Wahl b): "Programm beenden" immer, aber nur am PC (auf Android nicht zeigen)
        if (!OperatingSystem.IsAndroid())
        {
            BeendenKnopf = BeendenEintrag();
            unten.Children.Add(BeendenKnopf);
        }
        var fuss = new StackPanel { Children = { unten, LernstatusKasten(), Version() } };
        var rollen = new ScrollViewer { Content = oben, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
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

    /// <summary>Knopf "Programm beenden" (nur PC; Start-Test), sonst null.</summary>
    public Button? BeendenKnopf { get; private set; }

    /// <summary>Symbol "Ausschalten" (eigene Umrisse: Strich und offener Ring, 24 x 24).</summary>
    private static readonly Geometry Ausschalten = Geometry.Parse(
        "M11,2 L13,2 L13,12 L11,12 Z M16.9,4.6 L15.8,6.3 A7.5,7.5 0 1 1 8.2,6.3 L7.1,4.6 A9.5,9.5 0 1 0 16.9,4.6 Z");

    /// <summary>
    /// Weisung P8-56: Zeile wie <see cref="Eintrag"/> (Symbol 22, Text 14 fett text_dim, ohne Strich), schliesst das
    /// Fenster wie das Kreuz: Spielschritte schreiben, Abgleich, Sperren und Velopack laufen im Programmende (App.Exit).
    /// </summary>
    private static Button BeendenEintrag()
    {
        var symbol = new PathIcon { Data = Ausschalten, Width = 22, Height = 22 }.Farbe(PathIcon.ForegroundProperty, "Pinsel.muted");
        Mitwachsen(symbol, 22);
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(symbol, Avalonia.Automation.AccessibilityView.Raw);
        var symbolRahmen = new Border { Padding = new Thickness(8, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Child = symbol };
        var text = Schrift.Pt(Navigation.ProgrammBeenden, 14, FontWeight.Bold, "Pinsel.text_dim");
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.NoWrap;
        // Platz des Strichs (4 + 6) wie bei den Eintraegen, damit Symbol und Text buendig stehen
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("10,Auto,*") };
        Grid.SetColumn(symbolRahmen, 1);
        Grid.SetColumn(text, 2);
        zeile.Children.Add(symbolRahmen);
        zeile.Children.Add(text);
        var knopf = new Button
        {
            Content = zeile,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0, 9),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
        };
        Avalonia.Automation.AutomationProperties.SetName(knopf, Navigation.ProgrammBeenden);
        knopf.Click += (_, _) => Programmende.Fenster();
        return knopf;
    }
    private readonly Dictionary<string, NavPunkt> _punkte = new(StringComparer.Ordinal);

    /// <summary>Beschriftung eines Eintrags der Seitenleiste (Start-Test).</summary>
    public string Titel(string id) => _punkte[id].Titel;
    private readonly HashSet<string> _offen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _pfeile = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Border Strich, TextBlock Symbol, TextBlock Text, bool Unter)> _seitenTeile = new(StringComparer.Ordinal);
    private TextBlock? _statusWert;
    private Grid? _statusSpur;
    private int _statusLaeuft;

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

    /// <summary>Kleine Gruppen-Ueberschrift wie "MENÜ" (label 11 fett, muted, links 24 bzw. 12 im Rand der Liste).</summary>
    private static TextBlock Gruppe(string titel)
    {
        var text = Schrift.Pt(titel, 11, FontWeight.Bold, "Pinsel.muted");
        text.Margin = new Thickness(12, 10, 0, 6);
        return text;
    }

    private void EintragMitUnterpunkten(NavPunkt punkt, Panel ziel)
    {
        ziel.Children.Add(Eintrag(punkt, _seitenKnoepfe, 0));
        if (punkt.Unter.Count > 0)
        {
            var unter = new StackPanel { Spacing = 1, IsVisible = false };
            foreach (var u in punkt.Unter)
            {
                unter.Children.Add(Eintrag(u, _seitenKnoepfe, 1));
            }
            _unterpunkte[punkt.Id] = unter;
            ziel.Children.Add(unter);
        }
    }

    /// <summary>
    /// Zeile wie NavRow: Strich 4 x 20 links (nur aktiv, Verlauf accent), Symbol 22 (Unterpunkt 20, eingerueckt),
    /// Text, bei Unterpunkten rechts der Pfeil (zu: nach unten, offen: nach oben).
    /// </summary>
    private Button Eintrag(NavPunkt punkt, Dictionary<string, Button> ablage, int ebene)
    {
        var unter = ebene > 0;
        var strich = new Border { Width = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) }.HoeheMitSchrift(20);
        var symbol = Symbole.Block(punkt.Symbol, unter ? 20 : 22, "Pinsel.muted");
        SymbolMitwachsen(symbol, unter ? 20 : 22);
        // Abstand als Rand eines Rahmens, nicht des Symbols (sonst gilt es in der Layoutpruefung als abgeschnitten)
        var symbolRahmen = new Border { Padding = new Thickness(unter ? 20 : 8, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Child = symbol };
        var text = unter ? Schrift.Pt(punkt.Titel, 13, FontWeight.Normal, "Pinsel.muted") : Schrift.Pt(punkt.Titel, 14, FontWeight.Bold, "Pinsel.text_dim");
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.NoWrap;
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        Grid.SetColumn(symbolRahmen, 1);
        Grid.SetColumn(text, 2);
        zeile.Children.Add(strich);
        zeile.Children.Add(symbolRahmen);
        zeile.Children.Add(text);
        if (punkt.Unter.Count > 0)
        {
            var pfeil = Symbole.Block("chevron_right", 16, "Pinsel.muted");
            SymbolMitwachsen(pfeil, 16);
            pfeil.RenderTransform = new RotateTransform(90);
            var pfeilRahmen = new Border { Padding = new Thickness(8, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Child = pfeil };
            Grid.SetColumn(pfeilRahmen, 3);
            zeile.Children.Add(pfeilRahmen);
            _pfeile[punkt.Id] = pfeil;
        }
        var knopf = new Button
        {
            Content = zeile,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0, unter ? 8 : 9),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
        };
        Avalonia.Automation.AutomationProperties.SetName(knopf, punkt.Titel);
        knopf.Click += (_, _) =>
        {
            // wie Sidebar._on_nav: Eintrag mit Unterpunkten klappt auf bzw. zu und oeffnet die Seite
            if (_unterpunkte.ContainsKey(punkt.Id) && !_offen.Remove(punkt.Id))
            {
                _offen.Add(punkt.Id);
            }
            _oeffnen(punkt.Id);
            Aufklappen();
        };
        ablage[punkt.Id] = knopf;
        _seitenTeile[punkt.Id] = (strich, symbol, text, unter);
        return knopf;
    }

    /// <summary>Kasten LERNSTATUS (card, Radius 14, Rand border, aussen 14, innen 14/12/10).</summary>
    private Control LernstatusKasten()
    {
        var titel = Schrift.Pt(Navigation.Lernstatus, 11, FontWeight.Bold, "Pinsel.muted");
        var name = Schrift.Pt(Core.Dashboard.DashboardTexte.SeitenleisteBalken, 13, FontWeight.Normal, "Pinsel.text_dim");
        name.TextWrapping = TextWrapping.Wrap;
        _statusWert = Schrift.Pt("", 13, FontWeight.Normal, "Pinsel.muted");
        _statusWert.HorizontalAlignment = HorizontalAlignment.Right;
        _statusSpur = new Grid { Height = 9, ColumnDefinitions = new ColumnDefinitions("0*,1000*") };
        var hinten = new Border { CornerRadius = new CornerRadius(4.5) }.Farbe(Border.BackgroundProperty, "Pinsel.ring_bg");
        Grid.SetColumnSpan(hinten, 2);
        _statusSpur.Children.Add(hinten);
        _statusSpur.Children.Add(new Border { CornerRadius = new CornerRadius(4.5), IsVisible = false }.Farbe(Border.BackgroundProperty, FarbenAnwenden.Paar("accent", "accent2")));
        var kasten = new Border
        {
            Margin = new Thickness(14, 8, 14, 0),
            Padding = new Thickness(14, 12, 14, 10),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 6, Children = { titel, name, _statusWert, _statusSpur } },
        }.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
        return kasten;
    }

    private static Control Version()
    {
        var text = Schrift.Pt(Beschriftungen.Einsetzen(Navigation.Fusszeile, Aktualisierung.Aktualisierung.Anzeige), 11, FontWeight.Normal, "Pinsel.muted");
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.Margin = new Thickness(0, 8, 0, 14);
        return text;
    }

    /// <summary>Zeigt bearbeitet / gesamt im Kasten LERNSTATUS (Sidebar.update_status).</summary>
    public void Lernstatus(long bearbeitet, int gesamt)
    {
        if (_statusWert is null || _statusSpur is null)
        {
            return;
        }
        _statusWert.Text = Core.Dashboard.DashboardTexte.DeInt(bearbeitet) + " / " + Core.Dashboard.DashboardTexte.DeInt(gesamt);
        var anteil = (int)Math.Round(Math.Max(0, Math.Min(1, (double)bearbeitet / Math.Max(1, gesamt))) * 1000);
        _statusSpur.ColumnDefinitions[0].Width = new GridLength(anteil, GridUnitType.Star);
        _statusSpur.ColumnDefinitions[1].Width = new GridLength(1000 - anteil, GridUnitType.Star);
        _statusSpur.Children[1].IsVisible = anteil > 0;
        Avalonia.Automation.AutomationProperties.SetName(_statusSpur, Core.Dashboard.DashboardTexte.SeitenleisteBalken + " " + _statusWert.Text);
    }

    /// <summary>Liest den Lernstatus im Hintergrund neu (nach jedem Seitenwechsel; hoechstens eine Lesung zugleich).</summary>
    public void LernstatusLesen()
    {
        if (Interlocked.Exchange(ref _statusLaeuft, 1) == 1)
        {
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                if (await Zustand.DashboardInhalte is { } inhalte)
                {
                    var pfad = System.IO.Path.Combine(Core.Daten.Ablage.Datenordner(), Core.Daten.Lernstand.DateiName);
                    var (bearbeitet, gesamt) = Core.Dashboard.Lernstatus.Lesen(pfad, inhalte);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Lernstatus(bearbeitet, gesamt));
                }
            }
            catch (Exception fehler)
            {
                Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            }
            finally
            {
                Interlocked.Exchange(ref _statusLaeuft, 0);
            }
        });
    }

    /// <summary>Start-Test: Unterpunkte eines Eintrags auf- bzw. zuklappen (ohne die Seite zu oeffnen).</summary>
    public void Umschalten(string id)
    {
        if (!_offen.Remove(id))
        {
            _offen.Add(id);
        }
        Aufklappen();
    }

    /// <summary>Start-Test: Unterpunkte sichtbar?</summary>
    public bool Offen(string id) => _unterpunkte.TryGetValue(id, out var unter) && unter.IsVisible;

    /// <summary>Start-Test: Zeile "x / y" im Kasten LERNSTATUS (leer, bis sie gelesen ist).</summary>
    public string LernstatusText => _statusWert?.Text ?? "";

    /// <summary>Start-Test: Eintraege der Seitenleiste mit Symbol (Kennung, Symboltext).</summary>
    public IEnumerable<(string Id, string Symbol)> SeitenSymbole => _seitenTeile.Select(t => (t.Key, t.Value.Symbol.Text ?? ""));

    /// <summary>Unterpunkte zeigen bzw. verbergen, Pfeil drehen (offen: nach oben).</summary>
    private void Aufklappen()
    {
        foreach (var (eltern, unter) in _unterpunkte)
        {
            var offen = _offen.Contains(eltern);
            unter.IsVisible = offen;
            if (_pfeile.TryGetValue(eltern, out var pfeil))
            {
                pfeil.RenderTransform = new RotateTransform(offen ? -90 : 90);
            }
        }
    }

    private Control BaueUntereLeiste()
    {
        // Grid mit gleich breiten Spalten statt UniformGrid: UniformGrid rundet jede Zelle auf
        // ganze Punkte und schiebt die letzte bis 2 Punkte ueber den Rand (Layoutpruefung 1360 breit)
        // A 0.12.2 wie ft.NavigationBar am Handy: Hoehe 80, Symbol 24 (aus: OUTLINED muted, an: ROUNDED
        // accent) in einer Pille 64 x 32 (card_hi), Beschriftung darunter text_dim
        var raster = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", Texte.NavigationHandy.Count))) };
        foreach (var punkt in Texte.NavigationHandy)
        {
            var beschriftung = new TextBlock
            {
                Text = punkt.Titel,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Center,
            }.Groesse("Leiste").Farbe(TextBlock.ForegroundProperty, "Pinsel.text_dim");
            var aus = Symbole.Block(punkt.Symbol, 24, "Pinsel.muted");
            var an = Symbole.Block(punkt.SymbolAktiv, 24, "Pinsel.accent");
            an.IsVisible = false;
            var pille = new Border
            {
                CornerRadius = new CornerRadius(999),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Panel { Children = { aus, an } },
                Background = Brushes.Transparent,
            }.Breite(64);
            pille.Bind(Layoutable.HeightProperty, pille.GetResourceObservable("Schrift.Normal", v => v is double d ? 32 * d / Schrift.Grundgroessen["Normal"] : 32));
            var knopf = new Button
            {
                Content = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { pille, beschriftung } },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 12, 0, 16),
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(0),
            };
            knopf.Bind(Layoutable.MinHeightProperty, knopf.GetResourceObservable("Schrift.Normal", v => v is double d ? 80 * d / Schrift.Grundgroessen["Normal"] : 80));
            Avalonia.Automation.AutomationProperties.SetName(knopf, punkt.Titel);
            knopf.Click += (_, _) => _oeffnen(punkt.Id);
            _untenKnoepfe[punkt.Id] = knopf;
            _untenTeile[punkt.Id] = (pille, aus, an);
            Grid.SetColumn(knopf, raster.Children.Count);
            raster.Children.Add(knopf);
        }
        var leiste = new Border { Child = raster, IsVisible = false };
        return leiste.Farbe(Border.BackgroundProperty, "Pinsel.sidebar");
    }

    /// <summary>Hebt den Eintrag der offenen Seite hervor (am Handy: Lernbereiche unter "Lernen").</summary>
    public void Markiere(string id)
    {
        foreach (var (kennung, knopf) in _seitenKnoepfe)
        {
            Hervorheben(knopf, kennung == id);
        }
        // wie Sidebar.set_active: eine Unterseite klappt ihren Eintrag auf; der Eintrag selbst klappt per Tippen
        foreach (var eltern in _unterpunkte.Keys)
        {
            var punkt = Texte.NavigationPc.First(p => p.Id == eltern);
            if (punkt.Unter.Any(u => u.Id == id))
            {
                _offen.Add(eltern);
            }
        }
        Aufklappen();
        var unten = Texte.HandyZiel(id);
        foreach (var (kennung, (pille, aus, an)) in _untenTeile)
        {
            var aktiv = kennung == unten;
            if (aktiv)
            {
                pille.Farbe(Border.BackgroundProperty, "Pinsel.card_hi");
            }
            else
            {
                pille.ClearValue(Border.BackgroundProperty);
                pille.Background = Brushes.Transparent;
            }
            aus.IsVisible = !aktiv;
            an.IsVisible = aktiv;
        }
    }

    /// <summary>NavRow.set_active: aktiv card_hi, Strich mit Verlauf, Text text, Symbol und Pfeil accent; sonst sidebar, muted/text_dim.</summary>
    private void Hervorheben(Button knopf, bool aktiv)
    {
        if (aktiv)
        {
            knopf.Farbe(Button.BackgroundProperty, "Pinsel.card_hi");
        }
        else
        {
            knopf.ClearValue(Button.BackgroundProperty);
            knopf.Background = Brushes.Transparent;
        }
        var id = _seitenKnoepfe.First(k => k.Value == knopf).Key;
        if (!_seitenTeile.TryGetValue(id, out var teile))
        {
            return;
        }
        if (aktiv)
        {
            teile.Strich.Farbe(Border.BackgroundProperty, "Verlauf.accent");
        }
        else
        {
            teile.Strich.ClearValue(Border.BackgroundProperty);
            teile.Strich.Background = Brushes.Transparent;
        }
        teile.Symbol.Farbe(TextBlock.ForegroundProperty, aktiv ? "Pinsel.accent" : "Pinsel.muted");
        teile.Text.Farbe(TextBlock.ForegroundProperty, aktiv ? "Pinsel.text" : teile.Unter ? "Pinsel.muted" : "Pinsel.text_dim");
        if (_pfeile.TryGetValue(id, out var pfeil))
        {
            pfeil.Farbe(TextBlock.ForegroundProperty, aktiv ? "Pinsel.accent" : "Pinsel.muted");
        }
        if (aktiv)
        {
            // Sidebar.set_active (B-D-09): den aktiven Eintrag ganz ins Bild rollen
            Avalonia.Threading.Dispatcher.UIThread.Post(() => knopf.BringIntoView(), Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }
}
