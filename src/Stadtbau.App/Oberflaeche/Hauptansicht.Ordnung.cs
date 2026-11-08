using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle Q (Weisung P12-76, Kurzplan Meldung P12-42 Punkte 1 bis 10, Nico: alle Fragen = a): Ordnung der Knoepfe.
/// Kopf: Titelzeile mit Projektname und Speicherstand, rechts die Menues „Projekt ▾“ und „⚙ Einstellungen ▾“ und
/// „Werkzeug beenden“; darunter die Reiter als eine Leiste und rechts daneben Rueckgaengig/Wiederholen als Pfeile.
/// Die Menues liegen als Karte in einer eigenen Ebene ueber der Ansicht (kein eigenes Fenster: gleich am PC und am Tablet,
/// Eintraege mindestens 44 Bildpunkte hoch). Links in „Stadt von oben“ eine Suche und vier klappbare Gruppen, von denen
/// nur eine offen ist (gemerkt in einstellungen.json). Fenster „Tastenkürzel“ mit F1, ? und aus dem Menue.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Hoehe eines Menue-Eintrags (Tablet: mindestens 44 Bildpunkte, Weisung P12-76 Punkt 10).</summary>
    public const double EintragHoehe = 44;

    /// <summary>Die vier Reiter in der Reihenfolge der Leiste.</summary>
    public static readonly string[] Reiter = [Texte.Draufsicht, Texte.Innenraeume, Texte.Ansicht3D, Texte.GebaeudeEntwerfen];

    private readonly TextBlock _speicherStand = Schrift.Text("", "Klein");
    private readonly Panel _menueEbene = new() { IsVisible = false };
    private readonly Border _menueKarte = new()
    {
        Padding = new Thickness(6),
        CornerRadius = new CornerRadius(12),
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        MinWidth = 300,
        MaxWidth = 420,
    };
    private readonly StackPanel _projektMenue = new();
    private readonly StackPanel _einstellungenMenue = new();
    private Button? _menueAnker;

    /// <summary>Die Menue-Eintraege je Menue (Start-Test: Hoehe, Namen, Erreichbarkeit).</summary>
    internal Dictionary<string, List<Button>> MenueEintraege { get; } = new(StringComparer.Ordinal);

    /// <summary>Das offene Menue (null = keins).</summary>
    internal string? MenueOffen => _menueEbene.IsVisible ? _menueAnker is { } a ? MenueName(a) : null : null;

    /// <summary>Die Menue-Karte (Start-Test).</summary>
    internal Border MenueKarte => _menueKarte;

    private string? MenueName(Button anker) => _kopf.FirstOrDefault(k => k.Value == anker).Key;

    // ---- Kopf ---------------------------------------------------------------------------------------

    private Border KopfBauen()
    {
        // Titelzeile: links Projekt und Speicherstand, rechts die Menues und "Werkzeug beenden"
        _titel.VerticalAlignment = VerticalAlignment.Center;
        _titel.TextTrimming = TextTrimming.CharacterEllipsis;
        _speicherStand.VerticalAlignment = VerticalAlignment.Center;
        var titelLinks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _titel, _speicherStand } };
        var projekt = MenueKnopf(Texte.MenueProjekt, Texte.MenueProjekt + " ▾", _projektMenue);
        var einstellungen = MenueKnopf(Texte.MenueEinstellungen, "⚙ " + Texte.MenueEinstellungen + " ▾", _einstellungenMenue);
        _beenden.Margin = new Thickness(0);
        var rechts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { projekt, einstellungen, _beenden } };
        var titelZeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        titelZeile.Children.Add(titelLinks);
        Grid.SetColumn(rechts, 1);
        titelZeile.Children.Add(rechts);

        // Menue Projekt (Punkt 3) und Menue Einstellungen (Punkt 5)
        Eintrag(_projektMenue, Texte.MenueProjekt, Texte.Neu, () => _ = NeuAsync());
        Eintrag(_projektMenue, Texte.MenueProjekt, Texte.Oeffnen, () => _ = OeffnenAsync());
        Eintrag(_projektMenue, Texte.MenueProjekt, Texte.Speichern, SpeichernVonHand, "Strg+S");
        _projektMenue.Children.Add(Trenner());
        Eintrag(_projektMenue, Texte.MenueProjekt, Texte.DateiSpeichern, () => _ = DateiSpeichernAsync());
        Eintrag(_projektMenue, Texte.MenueProjekt, Texte.AnCcSenden, () => _ = SendenAsync());
        Eintrag(_einstellungenMenue, Texte.MenueEinstellungen, Texte.Dunkel, FarbeWechseln);
        Eintrag(_einstellungenMenue, Texte.MenueEinstellungen, Texte.Vollbild, VollbildUmschalten, "F11");
        var update = Eintrag(_einstellungenMenue, Texte.MenueEinstellungen, Texte.UpdateSuchen, () => _ = Aktualisierung.VonHandAsync(_ordner.Fehler));
        // nur wo es ein Update gibt (PC aus der Installation, Tablet, Start-Test), wie der Knopf auf der Startseite
        update.IsVisible = Aktualisierung.Pruefen is not null;
        Eintrag(_einstellungenMenue, Texte.MenueEinstellungen, Texte.Tastenkuerzel, () => _ = TastenZeigenAsync(), "F1");
        _einstellungenMenue.Children.Add(Trenner());
        _einstellungenMenue.Children.Add(new Border
        {
            Padding = new Thickness(12, 6),
            Child = Schrift.Text(Texte.Version + ": " + Werkzeug.Kennzeile, "Klein", farbe: "Pinsel.muted"),
        });

        // Reiter als eine zusammenhaengende Leiste (Punkt 1), rechts daneben die Pfeile (Punkt 4)
        var leiste = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var name in Reiter)
        {
            var k = Schrift.Knopf(name).Mit(Texte.KopfTipps[name]);
            k.CornerRadius = new CornerRadius(0);
            k.BorderThickness = new Thickness(0);
            k.Margin = new Thickness(0);
            k.Padding = new Thickness(18, 10);
            k.MinHeight = EintragHoehe;
            k.Click += (_, _) => ReiterGedrueckt(name);
            _kopf[name] = k;
            if (leiste.Children.Count > 0)
            {
                leiste.Children.Add(new Border { Width = 1 }.Farbe(Border.BackgroundProperty, "Pinsel.border"));
            }
            leiste.Children.Add(k);
        }
        var reiterRahmen = new Border
        {
            Child = leiste,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
        }.Farbe(Border.BorderBrushProperty, "Pinsel.border").Farbe(Border.BackgroundProperty, "Pinsel.card_alt");
        AutomationProperties.SetName(reiterRahmen, Texte.ReiterName);
        var pfeile = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                PfeilKnopf(Texte.Rueckgaengig, "M 9,5 L 4,10 L 9,15 M 4,10 L 15,10 C 18.5,10 21,12.5 21,15.5 C 21,18.5 18.5,21 15.5,21 L 12,21", Rueckgaengig),
                PfeilKnopf(Texte.Wiederholen, "M 16,5 L 21,10 L 16,15 M 21,10 L 10,10 C 6.5,10 4,12.5 4,15.5 C 4,18.5 6.5,21 9.5,21 L 13,21", Wiederholen),
            },
        };
        var reiterZeile = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { reiterRahmen, pfeile } };

        var kopf = new StackPanel { Spacing = 10, Children = { titelZeile, reiterZeile } };

        // Menue-Ebene: ein Tippen neben das Menue schliesst es
        _menueKarte.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border_hi");
        _menueKarte.BoxShadow = BoxShadows.Parse("0 6 18 0 #40000000");
        var daneben = new Border { Background = Brushes.Transparent };
        daneben.PointerPressed += (_, e) =>
        {
            MenueZu();
            e.Handled = true;
        };
        _menueEbene.Children.Add(daneben);
        _menueEbene.Children.Add(_menueKarte);

        WerkzeugVollbild.Geaendert += VollbildText;
        VollbildText();
        return new Border { Padding = new Thickness(16, 10, 16, 10), Child = kopf }.Farbe(Border.BackgroundProperty, "Pinsel.card");
    }

    private Button MenueKnopf(string name, string text, StackPanel menue)
    {
        var k = Schrift.Knopf(text).Vorlese(name).Mit(Texte.KopfTipps[name]);
        k.MinHeight = EintragHoehe;
        k.Click += (_, _) =>
        {
            if (_menueEbene.IsVisible && _menueAnker == k)
            {
                MenueZu();
            }
            else
            {
                MenueZeigen(k, menue);
            }
        };
        _kopf[name] = k;
        MenueEintraege[name] = [];
        return k;
    }

    /// <summary>Ein Eintrag im Menue: Beschriftung links, Taste grau rechts; der Knopf schliesst zuerst das Menue.</summary>
    private Button Eintrag(StackPanel menue, string menueName, string text, Action aktion, string? taste = null)
    {
        var k = new Button
        {
            Content = Schrift.Text(text),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = EintragHoehe,
            Padding = new Thickness(12, 8, taste is null ? 12 : 72, 8),
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        }.Vorlese(taste is null ? text : text + ", " + taste).Mit(Texte.KopfTipps[text]);
        k.Click += (_, _) =>
        {
            MenueZu();
            aktion();
        };
        _kopf[text] = k;
        MenueEintraege[menueName].Add(k);
        if (taste is null)
        {
            menue.Children.Add(k);
            return k;
        }
        var zeile = new Grid();
        zeile.Children.Add(k);
        var t = Schrift.Text(taste, "Klein", farbe: "Pinsel.muted");
        t.HorizontalAlignment = HorizontalAlignment.Right;
        t.VerticalAlignment = VerticalAlignment.Center;
        t.Margin = new Thickness(0, 0, 12, 0);
        t.IsHitTestVisible = false;
        zeile.Children.Add(t);
        menue.Children.Add(zeile);
        return k;
    }

    private static Border Trenner() => new Border { Height = 1, Margin = new Thickness(8, 4) }.Farbe(Border.BackgroundProperty, "Pinsel.border");

    private Button PfeilKnopf(string name, string daten, Action aktion)
    {
        var pfad = new Path
        {
            Data = Geometry.Parse(daten),
            Width = 25,
            Height = 26,
            StrokeThickness = 2.2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        pfad.Farbe(Shape.StrokeProperty, "Pinsel.text");
        var k = new Button
        {
            Content = pfad,
            Width = EintragHoehe,
            Height = EintragHoehe,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(EintragHoehe / 2),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        }.Vorlese(name).Mit(Texte.KopfTipps[name]);
        k.Farbe(Button.BackgroundProperty, "Pinsel.card_alt");
        k.Click += (_, _) => aktion();
        _kopf[name] = k;
        return k;
    }

    private void ReiterGedrueckt(string name)
    {
        if (name == Texte.Draufsicht)
        {
            ReiterZeigen(innen: false);
        }
        else if (name == Texte.Innenraeume)
        {
            ReiterZeigen(innen: true);
        }
        else if (name == Texte.Ansicht3D)
        {
            DreiZeigen();
        }
        else
        {
            _ = EntwerfenAsync();
        }
    }

    private void MenueZeigen(Button anker, StackPanel menue)
    {
        _menueAnker = anker;
        _menueKarte.Child = menue;
        var unten = anker.TranslatePoint(new Point(anker.Bounds.Width, anker.Bounds.Height), this) ?? new Point(Bounds.Width - 16, 60);
        _menueKarte.Margin = new Thickness(0, unten.Y + 4, Math.Max(8, Bounds.Width - unten.X), 0);
        _menueEbene.IsVisible = true;
        if (MenueEintraege.TryGetValue(MenueName(anker) ?? "", out var eintraege) && eintraege.FirstOrDefault(e => e.IsVisible && e.IsEnabled) is { } erster)
        {
            erster.Focus();
        }
        Ansage.Sagen(MenueName(anker) ?? "");
    }

    private void MenueZu()
    {
        if (!_menueEbene.IsVisible)
        {
            return;
        }
        _menueEbene.IsVisible = false;
        _menueAnker?.Focus();
    }

    /// <summary>Titel und Speicherstand (Punkt 2): „Projekt „Name“ · gespeichert ✓“.</summary>
    private void TitelSetzen()
    {
        _titel.Text = _projekt is null ? Werkzeug.Name : Texte.ProjektTitel(_projekt.Name) + (_projekt.NurLesen ? " (nur ansehen)" : "");
        SpeicherStandZeigen();
    }

    private void SpeicherStandZeigen()
    {
        _speicherStand.IsVisible = _projekt is { NurLesen: false };
        var offen = Ungespeichert;
        _speicherStand.Text = "· " + (offen ? Texte.NichtGespeichert : Texte.SpeicherStand);
        _speicherStand.Farbe(TextBlock.ForegroundProperty, offen ? "Pinsel.red" : "Pinsel.green");
    }

    // ---- Tastenkuerzel (Punkt 9) --------------------------------------------------------------------

    /// <summary>Fenster mit allen Tastenkuerzeln (F1, ? oder Menue Einstellungen).</summary>
    public Task<int> TastenZeigenAsync()
    {
        var liste = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16, RowSpacing = 6 };
        var zeile = 0;
        foreach (var (taste, wirkung, _) in Texte.TastenListe)
        {
            liste.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var t = Schrift.Text(taste, gewicht: FontWeight.Bold);
            var w = Schrift.Text(wirkung);
            Grid.SetRow(t, zeile);
            Grid.SetRow(w, zeile);
            Grid.SetColumn(w, 1);
            liste.Children.Add(t);
            liste.Children.Add(w);
            zeile++;
        }
        var inhalt = new StackPanel { Spacing = 12, Children = { liste, Schrift.Text(Texte.TastenTablet, "Klein", farbe: "Pinsel.muted") } };
        return Dialog.Zeigen(Texte.TastenTitel, inhalt, Texte.Schliessen);
    }

    // ---- Linke Spalte draussen: Suche und Gruppen (Punkt 6) ------------------------------------------

    private readonly TextBox _suche = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _keinTreffer = Schrift.Text(Texte.SucheKeinTreffer, "Klein", farbe: "Pinsel.muted");
    private readonly List<Klappgruppe> _gruppenLinks = [];
    private string _offeneGruppe = "";

    /// <summary>Die vier Gruppen links (Start-Test).</summary>
    internal IReadOnlyList<Klappgruppe> GruppenLinks => _gruppenLinks;

    /// <summary>Die Liste „Meine Blueprints“ (Start-Test).</summary>
    internal StackPanel BlueprintListe => _blueprintListe;

    /// <summary>Das Suchfeld links (Start-Test).</summary>
    internal TextBox SucheFeld => _suche;

    /// <summary>Hinweis „Nichts gefunden“ sichtbar (Start-Test).</summary>
    internal bool KeinTrefferSichtbar => _keinTreffer.IsEffectivelyVisible;

    /// <summary>Kennung der gemerkten offenen Gruppe (leer = alle zu).</summary>
    internal string OffeneGruppeLinks => _offeneGruppe;

    private void GruppenEinrichten()
    {
        _offeneGruppe = WerkzeugEinstellungen.OffeneGruppe(_ordner, [.. _gruppenLinks.Select(g => g.Kennung)], _gruppenLinks[0].Kennung);
        foreach (var g in _gruppenLinks)
        {
            g.Offen = g.Kennung == _offeneGruppe;
            g.Umgeschaltet += GruppeUmgeschaltet;
            _gruppen.Add(g.Kopf);
        }
        _suche.PlaceholderText = Texte.StueckSuchenPlatzhalter;
        _suche.Vorlese(Texte.StueckSuchen).Mit(Texte.StueckSuchenTipp);
        _suche.TextChanged += (_, _) => SucheAnwenden();
        _keinTreffer.IsVisible = false;
    }

    private bool SucheAktiv => (_suche.Text ?? "").Trim().Length > 0;

    /// <summary>Nur eine Gruppe offen; die offene merkt sich das Werkzeug (nicht waehrend einer Suche).</summary>
    private void GruppeUmgeschaltet(Klappgruppe g)
    {
        if (SucheAktiv)
        {
            return;
        }
        if (g.Offen)
        {
            foreach (var andere in _gruppenLinks.Where(a => a != g))
            {
                andere.Offen = false;
            }
        }
        _offeneGruppe = g.Offen ? g.Kennung : "";
        try
        {
            WerkzeugEinstellungen.OffeneGruppeSetzen(_ordner, _offeneGruppe);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Gruppe merken: " + e.Message);
        }
    }

    /// <summary>Knoepfe einer Gruppe, die die Suche filtert (Setzen-Knoepfe und die Kaesten der Blueprints).</summary>
    private IEnumerable<(Control Element, string Text)> Suchbar(Klappgruppe g)
    {
        foreach (var w in g.Inhalt.Children.OfType<WrapPanel>())
        {
            foreach (var k in w.Children.OfType<Button>())
            {
                if (k.Content is TextBlock { Text: { } t })
                {
                    yield return (k, t);
                }
            }
        }
        if (g.Inhalt.Children.Contains(_blueprintListe))
        {
            foreach (var (kasten, knoepfe) in BlueprintKaesten)
            {
                if (knoepfe.FirstOrDefault()?.Content is TextBlock { Text: { } t })
                {
                    yield return (kasten, t);
                }
            }
        }
    }

    /// <summary>Suche anwenden: Gruppen mit Treffern offen, Knoepfe ohne Treffer weg; leer = alles wie vorher.</summary>
    private void SucheAnwenden()
    {
        var such = Katalog.Normal((_suche.Text ?? "").Trim());
        var treffer = 0;
        foreach (var g in _gruppenLinks)
        {
            var hier = 0;
            foreach (var (element, text) in Suchbar(g))
            {
                var ja = such.Length == 0 || Katalog.Normal(text).Contains(such, StringComparison.Ordinal);
                element.IsVisible = ja;
                hier += ja ? 1 : 0;
            }
            // waehrend einer Suche nur Reihen mit Treffern (Ueberschriften, Auswahlfelder und Schalter der Gruppe weg)
            foreach (var c in g.Inhalt.Children)
            {
                c.IsVisible = such.Length == 0 || c switch
                {
                    WrapPanel w => w.Children.Any(k => k.IsVisible),
                    StackPanel l when l == _blueprintListe => l.Children.Any(k => k.IsVisible),
                    _ => false,
                };
            }
            if (such.Length == 0)
            {
                g.IsVisible = true;
                g.Offen = g.Kennung == _offeneGruppe;
            }
            else
            {
                g.IsVisible = hier > 0;
                g.Offen = hier > 0;
            }
            treffer += hier;
        }
        _keinTreffer.IsVisible = such.Length > 0 && treffer == 0;
    }

    // ---- Start-Test und Fotos -----------------------------------------------------------------------

    /// <summary>Wie oft man tippen muss, bis ein Element zu sehen ist: 0 sichtbar, 1 hinter einer Gruppe oder einem Menue, sonst -1.</summary>
    internal int KlicksBis(Control c)
    {
        if (c.IsEffectivelyVisible)
        {
            return 0;
        }
        if (!c.IsVisible)
        {
            return -1;
        }
        if (c is Button b && MenueEintraege.FirstOrDefault(m => m.Value.Contains(b)).Key is { } menue)
        {
            return _kopf[menue].IsEffectivelyVisible ? 1 : -1;
        }
        var gruppe = c.GetLogicalAncestors().OfType<Klappgruppe>().FirstOrDefault();
        if (gruppe is not null && !gruppe.Offen && gruppe.Kopf.IsEffectivelyVisible)
        {
            // alles zwischen Element und Gruppe muss sichtbar sein (nur die Gruppe ist zu)
            return c.GetLogicalAncestors().TakeWhile(a => a != gruppe).OfType<Control>().All(a => a.IsVisible || a == gruppe.Koerper) ? 1 : -1;
        }
        return -1;
    }

    /// <summary>Start-Test: die Gruppe links oeffnen, in der ein Element steht (wie ein Tippen auf ihren Kopf).</summary>
    internal void GruppeOeffnenFuer(Control c)
    {
        if (c.GetLogicalAncestors().OfType<Klappgruppe>().FirstOrDefault() is { Offen: false } g)
        {
            g.Kopf.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        }
    }

    /// <summary>Fotos: Menue Projekt offen.</summary>
    public void ProjektMenueFuerFotos() => MenueZeigen(_kopf[Texte.MenueProjekt], _projektMenue);

    /// <summary>Fotos: Menue Einstellungen offen.</summary>
    public void EinstellungenMenueFuerFotos() => MenueZeigen(_kopf[Texte.MenueEinstellungen], _einstellungenMenue);

    /// <summary>Fotos und Start-Test: Menue schliessen.</summary>
    public void MenueZuFuerFotos() => MenueZu();

    /// <summary>Fotos: Fenster „Tastenkürzel“ (schliesst der Aufrufer ueber den Knopf).</summary>
    public void TastenFuerFotos() => _ = TastenZeigenAsync();

    /// <summary>Fotos und Start-Test: Text ins Suchfeld links.</summary>
    public void SucheFuerFotos(string text) => _suche.Text = text;
}
