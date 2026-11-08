using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 3b (Weisung P12-19 Abschnitt 2): der volle CC0-Katalog. Gruppen nach Paket und Art, Suche nach Namen
/// (deutsch und englisch), kleine Vorschaubilder. Gezeigt wird je Seite hoechstens <see cref="Seite"/> Stuecke; ein Paket
/// wird erst gelesen, wenn eines seiner Stuecke sichtbar wird (<see cref="KatalogBilder"/>). Ein Tipp auf ein Stueck
/// meldet es ueber <see cref="Gewaehlt"/>; die Hauptansicht setzt es dann wie die Stuecke des einfachen Katalogs.
/// </summary>
public sealed class KatalogAnsicht : Border
{
    /// <summary>Stuecke je Seite (mit "Weitere anzeigen" kommt die naechste Seite dazu).</summary>
    public const int Seite = 48;

    private const double Kachel = 104;

    private readonly TextBox _suche = new() { PlaceholderText = Texte.KatalogSucheHinweis, MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly WrapPanel _gruppen = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _liste = new() { Spacing = 8 };
    private readonly TextBlock _zahl = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private readonly ScrollViewer _rollen;
    private readonly Dictionary<string, ToggleButton> _gruppenKnoepfe = new(StringComparer.Ordinal);
    private List<KatalogEintrag> _treffer = [];
    private int _gezeigt;
    private string? _gruppe;

    /// <summary>Ein Stueck wurde angetippt.</summary>
    public event Action<KatalogEintrag>? Gewaehlt;

    /// <summary>"Katalog schliessen" wurde angetippt.</summary>
    public event Action? Geschlossen;

    /// <summary>Gruppe, die gerade gezeigt wird (null: Suche ueber alle Gruppen).</summary>
    public string? Gruppe => _gruppe;

    /// <summary>Anzahl der Treffer der aktuellen Auswahl.</summary>
    public int Treffer => _treffer.Count;

    /// <summary>Anzahl der gezeigten Kacheln.</summary>
    public int Gezeigt => _gezeigt;

    public KatalogAnsicht()
    {
        Padding = new Thickness(12);
        CornerRadius = new CornerRadius(12);
        BorderThickness = new Thickness(1);
        this.Farbe(BackgroundProperty, "Pinsel.card").Farbe(BorderBrushProperty, "Pinsel.border");
        var schliessen = Schrift.Knopf(Texte.KatalogSchliessen).Vorlese(Texte.KatalogSchliessen);
        schliessen.Click += (_, _) => Geschlossen?.Invoke();
        var kopf = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(schliessen, Dock.Right);
        kopf.Children.Add(schliessen);
        kopf.Children.Add(Schrift.Text(Texte.VollerKatalog, "Mittel", FontWeight.Bold));
        var sucheTitel = Schrift.Text(Texte.KatalogSuche, "Klein", FontWeight.Bold).Fuer(_suche);
        _suche.TextChanged += (_, _) => Suchen(_suche.Text ?? "");
        _rollen = new ScrollViewer
        {
            Content = new Border { Child = _liste, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, Dialog.RandFuerLeiste) },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var oben = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                kopf,
                Schrift.Text(Texte.VollerKatalogHinweis, "Klein", farbe: "Pinsel.muted"),
                sucheTitel, _suche,
                Schrift.Text(Texte.KatalogGruppen, "Klein", FontWeight.Bold), _gruppen, _zahl,
            },
        };
        var raster = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        raster.Children.Add(oben);
        Grid.SetRow(_rollen, 1);
        raster.Children.Add(_rollen);
        Child = raster;
        GruppenBauen();
    }

    private void GruppenBauen()
    {
        _gruppen.Children.Clear();
        _gruppenKnoepfe.Clear();
        if (Katalog.Aktiv is not { } k)
        {
            _liste.Children.Add(Schrift.Text(Texte.KatalogFehlt, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        foreach (var g in k.Gruppen)
        {
            var text = g + " (" + k.AnzahlIn(g).ToString("N0", Hauptansicht.De) + ")";
            var knopf = new ToggleButton { Content = Schrift.Text(text, "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }.Vorlese(Texte.KatalogGruppen + ": " + text);
            knopf.Click += (_, _) => GruppeZeigen(g);
            _gruppen.Children.Add(knopf);
            _gruppenKnoepfe[g] = knopf;
        }
    }

    /// <summary>Eine Gruppe zeigen (Suche wird geleert).</summary>
    public void GruppeZeigen(string gruppe)
    {
        if (Katalog.Aktiv is not { } k)
        {
            return;
        }
        _gruppe = gruppe;
        if (!string.IsNullOrEmpty(_suche.Text))
        {
            _suche.Text = "";
        }
        Zeigen([.. k.InGruppe(gruppe)]);
    }

    /// <summary>Nach Namen suchen (deutsch oder englisch); leer: die gewaehlte Gruppe wieder zeigen.</summary>
    public void Suchen(string eingabe)
    {
        if (Katalog.Aktiv is not { } k)
        {
            return;
        }
        if (_suche.Text != eingabe)
        {
            _suche.Text = eingabe;
            return; // TextChanged ruft erneut
        }
        if (string.IsNullOrWhiteSpace(eingabe))
        {
            if (_gruppe is { } g)
            {
                Zeigen([.. k.InGruppe(g)]);
            }
            else
            {
                Zeigen([]);
            }
            return;
        }
        _gruppe = null;
        Zeigen(k.Suchen(eingabe));
    }

    private void Zeigen(List<KatalogEintrag> treffer)
    {
        foreach (var (g, knopf) in _gruppenKnoepfe)
        {
            knopf.IsChecked = g == _gruppe;
        }
        _treffer = treffer;
        _gezeigt = 0;
        _liste.Children.Clear();
        _zahl.Text = _gruppe is null && string.IsNullOrWhiteSpace(_suche.Text) ? "" : Texte.KatalogTreffer(treffer.Count);
        if (treffer.Count == 0 && !string.IsNullOrWhiteSpace(_suche.Text))
        {
            _liste.Children.Add(Schrift.Text(Texte.KatalogKeinTreffer, "Klein", farbe: "Pinsel.muted"));
        }
        Weitere();
        _rollen.Offset = default;
    }

    /// <summary>Die naechste Seite anhaengen (Kacheln nach Paket, mit Ueberschrift je Paket).</summary>
    public void Weitere()
    {
        if (Katalog.Aktiv is not { } k)
        {
            return;
        }
        if (_liste.Children.LastOrDefault() is Button { Tag: "mehr" } alt)
        {
            _liste.Children.Remove(alt);
        }
        var bis = Math.Min(_treffer.Count, _gezeigt + Seite);
        WrapPanel? reihe = _liste.Children.LastOrDefault() as WrapPanel;
        string? paket = _gezeigt > 0 ? _treffer[_gezeigt - 1].Paket : null;
        for (var i = _gezeigt; i < bis; i++)
        {
            var e = _treffer[i];
            if (e.Paket != paket || reihe is null)
            {
                paket = e.Paket;
                _liste.Children.Add(Schrift.Text(k.PaketTitel.GetValueOrDefault(e.Paket, e.Paket), "Klein", FontWeight.Bold, "Pinsel.muted"));
                reihe = new WrapPanel { Orientation = Orientation.Horizontal };
                _liste.Children.Add(reihe);
            }
            reihe.Children.Add(KachelBauen(e));
        }
        _gezeigt = bis;
        if (_gezeigt < _treffer.Count)
        {
            var mehr = Schrift.Knopf(Texte.KatalogMehr + " (" + (_treffer.Count - _gezeigt).ToString("N0", Hauptansicht.De) + ")").Vorlese(Texte.KatalogMehr);
            mehr.Tag = "mehr";
            mehr.Click += (_, _) => Weitere();
            _liste.Children.Add(mehr);
        }
    }

    /// <summary>Kleinster Anteil der Schriftgroesse, auf den ein Kachel-Name mit langem Wort verkleinert wird.</summary>
    public const double MinAnteil = 0.8;

    /// <summary>Platz fuer den Namen in einer Kachel (ein Bildpunkt Reserve fuer die Rundung des Layouts).</summary>
    public const double NamenPlatz = Kachel - 12 - 1;

    /// <summary>
    /// Sammelliste 222 (Weisung P12-78): Schriftgroesse eines Kachel-Namens. Passt das laengste Wort bei
    /// <paramref name="soll"/> in <see cref="NamenPlatz"/>, bleibt sie; sonst so klein, dass es passt (auf 0,25 abgerundet),
    /// hoechstens bis <see cref="MinAnteil"/> × soll. <c>Passt</c> = das laengste Wort passt bei der Groesse.
    /// </summary>
    public static (double Groesse, bool Passt) NamensGroesse(string text, double soll)
    {
        var familie = Application.Current?.TryGetResource("ContentControlThemeFontFamily", null, out var f) == true && f is FontFamily ff
            ? ff : FontFamily.Default;
        var schrift = new Typeface(familie, FontStyle.Normal, FontWeight.Normal);
        var breite = LernText.LaengstesWort(text, schrift, soll);
        if (breite <= NamenPlatz)
        {
            return (soll, true);
        }
        var g = Math.Max(soll * MinAnteil, Math.Floor(soll * NamenPlatz / breite * 4) / 4);
        return (g, LernText.LaengstesWort(text, schrift, g) <= NamenPlatz);
    }

    /// <summary>Gegenprobe des Start-Tests: Kacheln wie vor der Nachbesserung (Hoehe nach Namen, "Effekt" als eigene Zeile).</summary>
    public static bool UngleichFuerTests { get; set; }

    /// <summary>
    /// Eine Kachel (Weisung P12-25 Abschnitt 3 Punkt 1): alle gleich breit und gleich hoch, oben buendig, das Bild an
    /// derselben Stelle. Der Name hat immer Platz fuer genau 2 Zeilen (laenger: mit "…" gekuerzt, voll im Tipp); ein
    /// Effekt ist ein Schild auf dem Bild, keine eigene Zeile.
    /// </summary>
    private Button KachelBauen(KatalogEintrag e)
    {
        var bild = KatalogBilder.Bild(e);
        var name = Schrift.Text(e.Anzeige, "Klein");
        if (NamensGroesse(e.Anzeige, Schrift.Grundgroessen["Klein"] * Schrift.Faktor) is var (groesse, _) && groesse < Schrift.Grundgroessen["Klein"] * Schrift.Faktor)
        {
            // Sammelliste 222: ein langes deutsches Wort ("Christbaumständer") passt sonst nicht in die Kachel und wuerde
            // mitten im Wort getrennt; dann wird nur dieser Name kleiner, hoechstens bis MinAnteil
            name = new TextBlock { Text = e.Anzeige, FontSize = groesse }.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        }
        name.TextWrapping = TextWrapping.Wrap;
        name.TextAlignment = TextAlignment.Center;
        name.MaxLines = 2;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        // Platz fuer genau 2 Zeilen: ein unsichtbarer Platzhalter mit 2 Zeilen in derselben Zelle (gleiche Schrift und
        // Stufe, waechst also mit der Schriftgroesse mit), darueber der Name
        Control namensFeld = name;
        if (!UngleichFuerTests)
        {
            var platz = Schrift.Text("X\nX", "Klein");
            platz.Opacity = 0;
            platz.IsHitTestVisible = false;
            Avalonia.Automation.AutomationProperties.SetAccessibilityView(platz, Avalonia.Automation.AccessibilityView.Raw);
            namensFeld = new Grid { Children = { platz, name } };
        }
        var flaeche = new Grid { Width = 72, Height = 72, HorizontalAlignment = HorizontalAlignment.Center };
        flaeche.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(6),
            Child = bild is null ? null : new Image { Source = bild, Width = 72, Height = 72 },
        }.Farbe(BackgroundProperty, "Pinsel.bg"));
        var inhalt = new StackPanel { Spacing = 2, Width = Kachel - 12, VerticalAlignment = VerticalAlignment.Top };
        inhalt.Children.Add(flaeche);
        inhalt.Children.Add(namensFeld);
        if (e.Art == KatalogArt.Effekt)
        {
            if (UngleichFuerTests)
            {
                inhalt.Children.Add(Schrift.Text(Texte.Effekt, "Klein", farbe: "Pinsel.orange"));
            }
            else
            {
                flaeche.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 0), Margin = new Thickness(2),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                    Child = Schrift.Text(Texte.Effekt, "Klein", FontWeight.Bold, "Pinsel.text"),
                }.Farbe(BackgroundProperty, "Pinsel.card"));
            }
        }
        var art = e.Art == KatalogArt.Effekt ? Texte.Effekt + ", " + Texte.EffektHinweis : Texte.KatalogDreiecke(e.Dreiecke);
        var k = new Button
        {
            Content = inhalt, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(4), Width = Kachel,
            VerticalAlignment = UngleichFuerTests ? VerticalAlignment.Center : VerticalAlignment.Top,
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Tag = e,
        }.Vorlese(e.Anzeige + " setzen");
        ToolTip.SetTip(k, Texte.KatalogTipp(e.Anzeige, e.Englisch, art));
        k.Click += (_, _) => Gewaehlt?.Invoke(e);
        return k;
    }

    /// <summary>Kacheln je Reihe des Katalogs (je Paket ein WrapPanel; Start-Test, Weisung P12-25).</summary>
    public IEnumerable<IReadOnlyList<Button>> KachelGruppen =>
        _liste.Children.OfType<WrapPanel>().Select(w => (IReadOnlyList<Button>)[.. w.Children.OfType<Button>()]);

    /// <summary>Bildflaeche (72 x 72) einer Kachel.</summary>
    public static Control? KachelBild(Button kachel) => (kachel.Content as StackPanel)?.Children.FirstOrDefault();

    /// <summary>Ein Stueck waehlen wie mit einem Tipp auf seine Kachel (Start-Test).</summary>
    public void Waehlen(KatalogEintrag e) => Gewaehlt?.Invoke(e);

    /// <summary>Gezeigte Stuecke (Start-Test).</summary>
    public IEnumerable<KatalogEintrag> GezeigteStuecke => _treffer.Take(_gezeigt);

    /// <summary>Fokus in das Suchfeld (beim Oeffnen).</summary>
    public void SucheFokus() => _suche.Focus();
}
