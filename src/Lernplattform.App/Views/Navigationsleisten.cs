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
    private readonly Dictionary<string, (Border Pille, Control Aus, Control An)> _untenTeile = new(StringComparer.Ordinal);

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

    /// <summary>Knopf "Programm beenden" der Seitenleiste (Start-Test).</summary>
    public Button? BeendenKnopf { get; private set; }

    /// <summary>
    /// Weisung P8-56: Zeile wie <see cref="Eintrag"/> (Symbol 22, Text 14 fett text_dim, ohne Strich), schliesst das
    /// Fenster wie das Kreuz: Spielschritte schreiben, Abgleich, Sperren und Velopack laufen im Programmende (App.Exit).
    /// </summary>
    private static Button BeendenEintrag()
    {
        // A 1.15.0: ohne Symbol wie die anderen Zeilen im Entwurf (Variante B), Text buendig mit Hilfe und Optionen
        var text = Schrift.Pt(Navigation.ProgrammBeenden, 14, FontWeight.Bold, "Pinsel.text_dim");
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.Wrap;
        var zeile = new Border { Padding = new Thickness(16, 0, 8, 0), Child = text };
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
        knopf.Click += (_, _) => Programmende.Beenden();
        return knopf;
    }
    private readonly Dictionary<string, string> _titel = new(StringComparer.Ordinal);

    /// <summary>Beschriftung eines Eintrags der Seitenleiste (Start-Test).</summary>
    public string Titel(string id) => _titel[id];
    private readonly Dictionary<string, (Border Strich, TextBlock Text, bool Unter)> _seitenTeile = new(StringComparer.Ordinal);
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

    /// <summary>Gruppenkopf wie im Entwurf: klein (11), fett, muted, Abstand oben 10 und unten 4.</summary>
    private static TextBlock Gruppenkopf(string titel)
    {
        var text = Schrift.Pt(titel, 11, FontWeight.Bold, "Pinsel.muted");
        text.LetterSpacing = 0.8;
        text.TextWrapping = TextWrapping.Wrap;
        text.Margin = new Thickness(12, 10, 12, 4);
        return text;
    }

    /// <summary>
    /// Zeile wie im Entwurf: Eintrag fett 14 text_dim, Unterpunkt 13 muted (eingerueckt, darf umbrechen); aktiv card_hi
    /// mit Strich 4 links (Verlauf accent) und Text text.
    /// </summary>
    private Button Eintrag(LeistenZeile zeile)
    {
        var id = zeile.Id!;
        var unter = zeile.Art == LeistenArt.Unter;
        var strich = new Border { Width = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Stretch };
        var text = unter ? Schrift.Pt(zeile.Titel, 13, FontWeight.Normal, "Pinsel.muted") : Schrift.Pt(zeile.Titel, 14, FontWeight.Bold, "Pinsel.text_dim");
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.Wrap;
        // Abstand als Rand eines Rahmens, nicht des Textes (sonst gilt er in der Layoutpruefung als abgeschnitten)
        var textRahmen = new Border { Padding = new Thickness(unter ? 30 : 12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Child = text };
        var inhalt = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(textRahmen, 1);
        inhalt.Children.Add(strich);
        inhalt.Children.Add(textRahmen);
        var knopf = new Button
        {
            Content = inhalt,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0, unter ? 7 : 9),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
        };
        Avalonia.Automation.AutomationProperties.SetName(knopf, zeile.Titel);
        knopf.Click += (_, _) => _oeffnen(id);
        _seitenKnoepfe[id] = knopf;
        _seitenTeile[id] = (strich, text, unter);
        _titel[id] = zeile.Titel;
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
            Volatile.Write(ref _statusNochmal, 1);    // Phase 9 W2: nach der laufenden Lesung noch einmal (z. B. neue Sicht)
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
                if (Interlocked.Exchange(ref _statusNochmal, 0) == 1)
                {
                    LernstatusLesen();
                }
            }
        });
    }

    private int _statusNochmal;

    /// <summary>Start-Test: Zeile "x / y" im Kasten LERNSTATUS (leer, bis sie gelesen ist).</summary>
    public string LernstatusText => _statusWert?.Text ?? "";

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

    /// <summary>Drei Punkte fuer „Mehr“ (eigene Umrisse, 24 x 24).</summary>
    private static readonly Geometry DreiPunkte = Geometry.Parse(
        "M4,12 A2,2 0 1 1 8,12 A2,2 0 1 1 4,12 Z M10,12 A2,2 0 1 1 14,12 A2,2 0 1 1 10,12 Z M16,12 A2,2 0 1 1 20,12 A2,2 0 1 1 16,12 Z");

    /// <summary>
    /// A 1.15.0 (Entwurf Variante B): Symbol der unteren Leiste; „ZIFFER_n“ zeigt die Ziffer n als Text (Teil 1, Teil 2),
    /// „MEHR“ drei Punkte, sonst das Material-Symbol. Groesse 24, waechst mit der Schrift.
    /// </summary>
    private static Control UntenSymbol(string name, string pinsel)
    {
        if (name.StartsWith("ZIFFER_", StringComparison.Ordinal))
        {
            var ziffer = Schrift.Pt(name["ZIFFER_".Length..], 22, FontWeight.Normal, pinsel);
            ziffer.HorizontalAlignment = HorizontalAlignment.Center;
            ziffer.VerticalAlignment = VerticalAlignment.Center;
            Avalonia.Automation.AutomationProperties.SetAccessibilityView(ziffer, Avalonia.Automation.AccessibilityView.Raw);
            return ziffer;
        }
        if (name == "MEHR")
        {
            var punkte = new PathIcon { Data = DreiPunkte, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                .Farbe(PathIcon.ForegroundProperty, pinsel);
            Mitwachsen(punkte, 24);
            Avalonia.Automation.AutomationProperties.SetAccessibilityView(punkte, Avalonia.Automation.AccessibilityView.Raw);
            return punkte;
        }
        return Symbole.Block(name, 24, pinsel);
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
            var aus = UntenSymbol(punkt.Symbol, "Pinsel.muted");
            var an = UntenSymbol(punkt.SymbolAktiv, "Pinsel.accent");
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

    /// <summary>
    /// Sammelliste 341 (Start-Test): welche Eintraege gerade markiert aussehen, je Teil die Kennungen, deren Farbe genau die
    /// Ressource der Markierung der aktuellen Farbwelt ist (Knopf card_hi, Strich Verlauf accent, Text text,
    /// Pille unten card_hi). Nur fuer Tests.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> MarkiertFuerTests()
    {
        object? Res(string schluessel) => Seitenleiste.TryFindResource(schluessel, out var wert) ? wert : null;
        var hi = Res("Pinsel.card_hi");
        var verlauf = Res("Verlauf.accent");
        var akzent = Res("Pinsel.accent");
        var text = Res("Pinsel.text");
        IReadOnlyList<string> Wo<T>(IEnumerable<KeyValuePair<string, T>> quelle, Func<T, object?> wert, object? soll) =>
            soll is null ? [] : quelle.Where(q => ReferenceEquals(wert(q.Value), soll)).Select(q => q.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Knopf"] = Wo(_seitenKnoepfe, k => k.Background, hi),
            ["Strich"] = Wo(_seitenTeile, t => t.Strich.Background, verlauf),
            ["Text"] = Wo(_seitenTeile, t => t.Text.Foreground, text),
            ["Pille"] = Wo(_untenTeile, t => t.Pille.Background, hi),
        };
    }

    /// <summary>Sammelliste 341: Kennung des Eintrags der unteren Leiste, der zu einer Seite gehoert (null = keiner).</summary>
    public string? UntenZielFuerTests(string id, string? eltern = null) => UntenZiel(id, eltern) is { Length: > 0 } z ? z : null;

    /// <summary>Sammelliste 341: alle Kennungen der Seitenleiste (nur fuer Tests).</summary>
    public IReadOnlyList<string> SeitenKennungenFuerTests => _seitenKnoepfe.Keys.ToList();

    private string? _markiert;
    private string? _markiertEltern;

    /// <summary>
    /// Hebt den Eintrag der offenen Seite hervor. Zeigt eine Leiste die Seite nicht selbst, gilt der Eintrag aus
    /// <paramref name="eltern"/> (Herkunft, z. B. der Pruefungsteil, aus dem eine Uebung kam) bzw. <see cref="Pruefungsteile.Eltern"/>.
    /// </summary>
    public void Markiere(string id, string? eltern = null)
    {
        _markiert = id;
        _markiertEltern = eltern;
        var seite = SeitenZiel(id, eltern);
        foreach (var (kennung, knopf) in _seitenKnoepfe)
        {
            Hervorheben(knopf, kennung == seite);
        }
        var unten = UntenZiel(id, eltern);
        foreach (var (kennung, (pille, aus, an)) in _untenTeile)
        {
            var aktiv = kennung == unten;
            if (aktiv)
            {
                pille.FarbeNeu(Border.BackgroundProperty, "Pinsel.card_hi");
            }
            else
            {
                // Sammelliste 341: ClearValue beendet die Bindung nicht (nach hell/dunkel kam die Markierung zurueck)
                Markierung.Loesen(pille, Border.BackgroundProperty);
                pille.Background = Brushes.Transparent;
            }
            aus.IsVisible = !aktiv;
            an.IsVisible = aktiv;
        }
    }

    /// <summary>Eintrag der Seitenleiste zu einer Seite (leer = keiner).</summary>
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

    /// <summary>NavRow.set_active: aktiv card_hi, Strich mit Verlauf, Text text, Symbol und Pfeil accent; sonst sidebar, muted/text_dim.</summary>
    private void Hervorheben(Button knopf, bool aktiv)
    {
        if (aktiv)
        {
            knopf.FarbeNeu(Button.BackgroundProperty, "Pinsel.card_hi");
        }
        else
        {
            Markierung.Loesen(knopf, Button.BackgroundProperty);
            knopf.Background = Brushes.Transparent;
        }
        var id = _seitenKnoepfe.First(k => k.Value == knopf).Key;
        if (!_seitenTeile.TryGetValue(id, out var teile))
        {
            return;
        }
        if (aktiv)
        {
            teile.Strich.FarbeNeu(Border.BackgroundProperty, "Verlauf.accent");
        }
        else
        {
            Markierung.Loesen(teile.Strich, Border.BackgroundProperty);
            teile.Strich.Background = Brushes.Transparent;
        }
        teile.Text.Farbe(TextBlock.ForegroundProperty, aktiv ? "Pinsel.text" : teile.Unter ? "Pinsel.muted" : "Pinsel.text_dim");
        if (aktiv)
        {
            // Sidebar.set_active (B-D-09): den aktiven Eintrag ganz ins Bild rollen
            Avalonia.Threading.Dispatcher.UIThread.Post(() => knopf.BringIntoView(), Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }
}
