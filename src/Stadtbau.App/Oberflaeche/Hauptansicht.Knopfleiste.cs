using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Sammelliste 379 (Weisung P12-298, Nico Antwort a): die Knopfleiste der Karte ist EINE schmale Zeile mit vier kleineren
/// Knoepfen: „Wählen ▾“ (Einzeln, Mehrere), „Einfügen“, „Werkzeuge ▾“ (Maßband, Notiz, Plätze, Stadtteil-Namen) und
/// „Anzeigen ▾“ (Ebenen, Wärmekarte, Stadtteile mit Häkchen). Der aktive Modus steht im Knopf („Werkzeuge: Maßband“). Die
/// Menues sind dieselben Knoepfe wie bisher (Namen fuer den Screenreader, Tipps, Tasten P, T, B, L, N, U, W bleiben); ein
/// Tipp auf einen Eintrag schliesst das Menue. Ganz rechts klappt „‹“ die Zeile ein (eingeklappt bleibt „Knöpfe ›“).
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Gegenprobe (nur Tests, beim Bau gelesen): Knopfleiste in zwei Reihen wie bis S 0.57.0.</summary>
    internal static bool KnopfleisteAltFuerTests { get; set; }

    private readonly Border _leisteMenue = new()
    {
        Padding = new Thickness(6),
        CornerRadius = new CornerRadius(12),
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsVisible = false,
        MinWidth = 220,
    };
    private readonly Dictionary<string, Button> _leisteKnoepfe = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StackPanel> _leisteMenues = new(StringComparer.Ordinal);
    private Button? _leisteKlappe;
    private string? _leisteOffen;

    /// <summary>Start-Test: die vier Knoepfe der Zeile (Schluessel <see cref="Texte.LeisteWaehlen"/> …).</summary>
    internal IReadOnlyDictionary<string, Button> LeisteKnoepfe => _leisteKnoepfe;

    /// <summary>Start-Test: das offene Menue der Zeile (null = keins) und seine Karte.</summary>
    internal string? LeisteMenueOffen => _leisteMenue.IsVisible ? _leisteOffen : null;
    internal Border LeisteMenueKarte => _leisteMenue;

    /// <summary>Start-Test: Knopf „‹“/„Knöpfe ›“.</summary>
    internal Button? LeisteKlappKnopf => _leisteKlappe;

    /// <summary>Ist die Zeile eingeklappt?</summary>
    internal bool LeisteEingeklappt { get; private set; }

    /// <summary>Die Knoepfe der Leiste in die Zeile und die drei Menues verteilen (nach dem Bau aller Knoepfe).</summary>
    private void KnopfleisteOrdnen(Grid mitte)
    {
        if (KnopfleisteAltFuerTests)
        {
            return;
        }
        _werkzeugLeiste.Children.Clear();
        _waehlenChip.Content = Schrift.Text(Texte.LeisteEinzeln);
        _mehrereChip.Content = Schrift.Text(Texte.LeisteMehrere);
        _ebenenChip.Content = Schrift.Text(Texte.LeisteEbenen);
        var menues = new (string Name, Button[] Eintraege)[]
        {
            (Texte.LeisteWaehlen, [_waehlenChip, _mehrereChip]),
            (Texte.LeisteWerkzeuge, [_massbandChip, _notizChip, _plaetzeChip, _namenChip]),
            (Texte.LeisteAnzeigen, [_ebenenChip, _waermeChip, _stadtteileChip]),
        };
        foreach (var (name, eintraege) in menues)
        {
            var liste = new StackPanel { Spacing = 4 };
            foreach (var e in eintraege)
            {
                e.HorizontalAlignment = HorizontalAlignment.Stretch;
                e.HorizontalContentAlignment = HorizontalAlignment.Left;
                e.Margin = new Thickness(0);
                e.MinHeight = EintragHoehe;
                // ein Tipp auf einen Eintrag schliesst das Menue (die Aktion des Knopfs laeuft wie bisher)
                e.AddHandler(Button.ClickEvent, (_, _) => LeisteMenueZu(), RoutingStrategies.Bubble, handledEventsToo: true);
                liste.Children.Add(e);
            }
            _leisteMenues[name] = liste;
        }
        Button Klein(string name, Action aktion)
        {
            var k = new Button
            {
                Content = Schrift.Text(name, "Klein", FontWeight.Bold),
                Padding = new Thickness(12, 4),
                MinHeight = 34,
                Margin = new Thickness(0, 0, 6, 0),
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
            }.Vorlese(name).Mit(Texte.LeisteTipps[name]);
            k.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.BorderBrushProperty, "Pinsel.border");
            k.Click += (_, _) => aktion();
            _leisteKnoepfe[name] = k;
            return k;
        }
        var zeile = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (name, _) in menues.Take(1))
        {
            zeile.Children.Add(Klein(name, () => LeisteMenueUmschalten(name)));
        }
        // „Einfügen“: kleiner Knopf, der den bisherigen Knopf drueckt (Zustand, Tipp und Aktion bleiben dort)
        var einfuegen = Klein(Texte.Einfuegen, () => _einfuegenChip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        zeile.Children.Add(einfuegen);
        foreach (var (name, _) in menues.Skip(1))
        {
            zeile.Children.Add(Klein(name, () => LeisteMenueUmschalten(name)));
        }
        _ebenenHinweis.Margin = new Thickness(4, 0, 6, 0);
        zeile.Children.Add(_ebenenHinweis);
        _leisteKlappe = new Button
        {
            Content = Schrift.Text("‹", "Klein", FontWeight.Bold),
            Padding = new Thickness(10, 4),
            MinHeight = 34,
            CornerRadius = new CornerRadius(999),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
        }.Vorlese(Texte.LeisteEinklappen).Mit(Texte.LeisteEinklappenTipp);
        _leisteKlappe.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.BorderBrushProperty, "Pinsel.border");
        _leisteKlappe.Click += (_, _) => LeisteKlappen(!LeisteEingeklappt);
        zeile.Children.Add(_leisteKlappe);
        _werkzeugLeiste.Children.Add(zeile);

        _leisteMenue.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
        _leisteMenue.BoxShadow = BoxShadows.Parse("0 6 18 0 #40000000");
        mitte.Children.Add(_leisteMenue);
        Karte.Verdecker = v => Anzeigen.Rahmen(v, _kameraAussen, _werkzeugLeiste, _ebenenKarte, _grenzLeiste, _leisteMenue);
        // ein Tipp auf die Karte schliesst ein offenes Menue der Zeile
        Karte.AddHandler(InputElement.PointerPressedEvent, (_, _) => LeisteMenueZu(), RoutingStrategies.Tunnel, handledEventsToo: true);
        LeisteZeigen();
    }

    /// <summary>Menue der Zeile oeffnen bzw. schliessen (Knopf antippen).</summary>
    internal void LeisteMenueUmschalten(string name)
    {
        if (_leisteMenue.IsVisible && _leisteOffen == name)
        {
            LeisteMenueZu();
            return;
        }
        if (!_leisteMenues.TryGetValue(name, out var liste) || !_leisteKnoepfe.TryGetValue(name, out var anker))
        {
            return;
        }
        _ebenenKarte.IsVisible = false;
        _leisteOffen = name;
        _leisteMenue.Child = liste;
        var oben = anker.TranslatePoint(new Point(0, anker.Bounds.Height), _werkzeugLeiste) ?? new Point(0, anker.Bounds.Height);
        _leisteMenue.Margin = new Thickness(_werkzeugLeiste.Margin.Left + oben.X, _werkzeugLeiste.Margin.Top + oben.Y + 4, 0, 0);
        _leisteMenue.IsVisible = true;
        Antippbar(_leisteMenue);
        if (liste.Children.OfType<Button>().FirstOrDefault(b => b.IsVisible && b.IsEnabled) is { } erster)
        {
            erster.Focus();
        }
        Ansage.Sagen(name);
        LeisteZeigen();
    }

    /// <summary>
    /// Start-Test (Bedienweg wie ein Mensch): das Menue oeffnen, in dem <paramref name="eintrag"/> steht (ist es schon offen
    /// oder steht der Knopf in keinem Menue, aendert sich nichts).
    /// </summary>
    internal void LeisteMenueFuer(Button eintrag)
    {
        if (_leisteMenues.FirstOrDefault(m => m.Value.Children.Contains(eintrag)).Key is { } name && LeisteMenueOffen != name)
        {
            if (LeisteEingeklappt)
            {
                LeisteKlappen(false);
            }
            LeisteMenueUmschalten(name);
        }
    }

    /// <summary>Offenes Menue der Zeile schliessen.</summary>
    internal void LeisteMenueZu()
    {
        if (!_leisteMenue.IsVisible)
        {
            return;
        }
        _leisteMenue.IsVisible = false;
        LeisteZeigen();
    }

    /// <summary>Zeile ein- oder ausklappen; eingeklappt bleibt nur „Knöpfe ›“.</summary>
    internal void LeisteKlappen(bool zu)
    {
        if (_leisteKlappe is null)
        {
            return;
        }
        LeisteEingeklappt = zu;
        LeisteMenueZu();
        if (zu)
        {
            _ebenenKarte.IsVisible = false;
        }
        foreach (var c in ((StackPanel)_werkzeugLeiste.Children[0]).Children)
        {
            if (c != _leisteKlappe)
            {
                c.IsVisible = !zu;
            }
        }
        _leisteKlappe.Content = Schrift.Text(zu ? Texte.LeisteAufklappenText : "‹", "Klein", FontWeight.Bold);
        _leisteKlappe.Vorlese(zu ? Texte.LeisteAufklappen : Texte.LeisteEinklappen);
        Tipp.Setzen(_leisteKlappe, zu ? Texte.LeisteAufklappenTipp : Texte.LeisteEinklappenTipp);
        Ansage.Sagen(zu ? Texte.LeisteEingeklapptAnsage : Texte.LeisteAufgeklapptAnsage);
        ChipsZeigen();
        Antippbar(_werkzeugLeiste);
    }

    /// <summary>Beschriftung und Markierung der vier Knoepfe nach dem Zustand (aktiver Modus im Knopf).</summary>
    private void LeisteZeigen()
    {
        if (_leisteKnoepfe.Count == 0)
        {
            return;
        }
        var werkzeug = Karte.MassbandAn ? Texte.Massband : Karte.SetzenArt == AussenArt.Notiz ? Texte.Notiz : Karte.PlaetzeAn ? Texte.Plaetze
            : Karte.NamenAn ? Texte.StadtteilNamen : null;
        var waehlen = Karte.MehrereWaehlen ? Texte.LeisteMehrere : Texte.LeisteEinzeln;
        var anzeigen = new List<string>();
        if (Karte.WaermeAn)
        {
            anzeigen.Add(Texte.Waermekarte);
        }
        if (Karte.StadtteileAn)
        {
            anzeigen.Add(Texte.Stadtteile);
        }
        if (Karte.EbenenAus.Count > 0)
        {
            anzeigen.Add(Texte.LeisteEbenen);
        }
        void Setzen(string name, string text, bool an)
        {
            var k = _leisteKnoepfe[name];
            var offen = _leisteMenue.IsVisible && _leisteOffen == name;
            var anzeige = name == Texte.Einfuegen ? text : text + (offen ? " ▴" : " ▾");
            if (k.Content is TextBlock t)
            {
                t.Text = anzeige;
                t.Farbe(TextBlock.ForegroundProperty, an ? "Pinsel.on_accent" : "Pinsel.text");
            }
            k.Farbe(Button.BackgroundProperty, an ? "VerlaufW.primary" : "Pinsel.card_alt");
            AutomationProperties.SetName(k, text);
        }
        Setzen(Texte.LeisteWaehlen, Texte.LeisteWaehlen + ": " + waehlen, werkzeug is null && Karte.SetzenArt is null);
        Setzen(Texte.LeisteWerkzeuge, werkzeug is null ? Texte.LeisteWerkzeuge : Texte.LeisteWerkzeuge + ": " + werkzeug, werkzeug is not null);
        Setzen(Texte.Einfuegen, Texte.Einfuegen, _einfuegen is not null);
        _leisteKnoepfe[Texte.Einfuegen].IsVisible = !LeisteEingeklappt && !_drei;
        Setzen(Texte.LeisteAnzeigen, anzeigen.Count == 0 ? Texte.LeisteAnzeigen : Texte.LeisteAnzeigen + ": " + string.Join(", ", anzeigen), anzeigen.Count > 0);
        // Haekchen vor den Anzeigen, die an sind
        Haken(_waermeChip, Texte.Waermekarte, Karte.WaermeAn);
        Haken(_stadtteileChip, Texte.Stadtteile, Karte.StadtteileAn);
        Haken(_ebenenChip, Texte.LeisteEbenen, Karte.EbenenAus.Count == 0);
        // in 3D gibt es nur Waehlen und Massband: leere Menues und Knoepfe ausblenden
        foreach (var (name, liste) in _leisteMenues.Where(m => m.Key != Texte.Einfuegen))
        {
            _leisteKnoepfe[name].IsVisible = !LeisteEingeklappt && liste.Children.OfType<Button>().Any(b => b.IsVisible);
        }
    }

    /// <summary>
    /// Eben gezeigte oder verschobene Knoepfe sofort antippbar machen. Gefunden im Start-Test S 0.58.0: Avalonia 12 prueft
    /// Treffer (Maus, Finger) gegen die beim letzten Zeichnen gemerkten Grenzen; ein Menue, das an anderer Stelle oder mit
    /// anderem Inhalt wieder erscheint, wurde gezeichnet, ein Tipp darauf ging aber an die Karte darunter. Nach dem ersten
    /// Zeichnen werden darum alle Teile noch einmal gezeichnet (merkt die Grenzen neu).
    /// </summary>
    internal static void Antippbar(Control c)
    {
        // nach dem ersten Zeichnen (naechstes Bild, danach wartende Arbeit) alle Teile neu zeichnen lassen
        TopLevel.GetTopLevel(c)?.RequestAnimationFrame(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var v in c.GetSelfAndVisualDescendants())
            {
                v.InvalidateVisual();
            }
        }, Avalonia.Threading.DispatcherPriority.Background));
    }

    private static void Haken(Button? k, string text, bool an)
    {
        if (k?.Content is TextBlock t)
        {
            t.Text = (an ? "✓ " : "    ") + text;
        }
    }
}
