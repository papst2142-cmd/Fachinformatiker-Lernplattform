using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, PB3: Liste der AP1-/AP2-Szenarien und Testprojekte wie app_gui.PagedList (v0.63.2):
/// Suche (Titel oder Nummer), Filter FACHBEREICH (nur Testprojekt), THEMA bzw. SCHWIERIGKEIT und
/// STATUS, Zaehler, 15 Eintraege je Seite mit Status-Punkt und Haken, Blaettern. Filtern und
/// Blaettern rechnet Core.Lernen.Listen; die Liste zeigt nur an.
/// </summary>
public sealed class ListenTeil : StackPanel
{
    /// <summary>CATEGORY_COLOR (Farbe des Streifens je Fachbereich, wie die Suche).</summary>
    public static readonly IReadOnlyDictionary<string, string> Farben = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Fachbereiche.Netzwerk] = "Pinsel.cyan",
        [Fachbereiche.Sicherheit] = "Pinsel.pink",
        [Fachbereiche.Systeme] = "Pinsel.purple",
        [Fachbereiche.Wirtschaft] = "Pinsel.green",
        [Fachbereiche.Datenbanken] = "Pinsel.orange",
    };

    private readonly IReadOnlyList<Lerninhalt> _eintraege;
    private readonly Func<Lerninhalt, string> _unterzeile;
    private readonly Func<Dictionary<int, (string Status, string Stufe)>> _statiQuelle;
    private readonly Action<int> _gewaehlt;
    private readonly string _gruppenFeld;
    private readonly string _nomen;
    private readonly TextBox _suche;
    private readonly ComboBox? _fachbereich;
    private readonly ComboBox _gruppe;
    private readonly ComboBox _status;
    private readonly List<string> _gruppen;
    private readonly TextBlock _zaehler = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
    private readonly StackPanel _zeilen = new() { Spacing = 8 };
    private readonly TextBlock _leer = Schrift.Text(SzenarienTexte.KeineTreffer, "Normal", FontWeight.Normal, "Pinsel.text_dim");
    private readonly TextBlock _seiteText = Schrift.Text("", "Normal", FontWeight.Bold, "Pinsel.text_dim");
    private readonly Button _zurueck;
    private readonly Button _weiter;
    private bool _setzen;

    public ListenTeil(IReadOnlyList<Lerninhalt> eintraege, string gruppenFeld, string gruppenName, bool mitFachbereich, string nomen,
        Func<Lerninhalt, string> unterzeile, Func<Dictionary<int, (string Status, string Stufe)>> statiQuelle, Action<int> gewaehlt)
    {
        _eintraege = eintraege;
        _gruppenFeld = gruppenFeld;
        _nomen = nomen;
        _unterzeile = unterzeile;
        _statiQuelle = statiQuelle;
        _gewaehlt = gewaehlt;
        Spacing = 6;
        Gefiltert = [.. Enumerable.Range(0, eintraege.Count)];

        _suche = new TextBox { PlaceholderText = SzenarienTexte.SucheHinweis, HorizontalAlignment = HorizontalAlignment.Stretch }
            .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
        Avalonia.Automation.AutomationProperties.SetName(_suche, SzenarienTexte.SucheHinweis);
        _suche.TextChanged += (_, _) => Neu();
        Children.Add(_suche);

        if (mitFachbereich)
        {
            _fachbereich = Auswahl(Fachbereiche.Alle.Select(f => Fachbereiche.Kurzname[f]).Prepend(FragenStatus.Alle));
            Children.Add(Ueberschrift(SzenarienTexte.Fachbereich));
            Children.Add(_fachbereich);
        }
        _gruppen = Listen.Gruppenwerte(eintraege, gruppenFeld);
        _gruppe = Auswahl(_gruppen.Prepend(FragenStatus.Alle));
        Children.Add(Ueberschrift(Beschriftungen.Gross(gruppenName)));
        Children.Add(_gruppe);
        _status = Auswahl(Listen.StatusFilter);
        Children.Add(Ueberschrift(SzenarienTexte.Status));
        Children.Add(_status);

        _zaehler.Margin = new Thickness(0, 12, 0, 2);
        Children.Add(_zaehler);
        Children.Add(_zeilen);
        _leer.IsVisible = false;
        Children.Add(_leer);

        _zurueck = Pfeil("←", SzenarienTexte.SeiteZurueck, -1);
        _weiter = Pfeil("→", SzenarienTexte.SeiteWeiter, 1);
        _seiteText.HorizontalAlignment = HorizontalAlignment.Center;
        _seiteText.VerticalAlignment = VerticalAlignment.Center;
        var blaettern = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 10, 0, 0) };
        Grid.SetColumn(_seiteText, 1);
        Grid.SetColumn(_weiter, 2);
        blaettern.Children.Add(_zurueck);
        blaettern.Children.Add(_seiteText);
        blaettern.Children.Add(_weiter);
        Children.Add(blaettern);
    }

    /// <summary>Positionen nach Suche und Filtern (self.filtered).</summary>
    public List<int> Gefiltert { get; private set; }

    public Dictionary<int, (string Status, string Stufe)> Stati { get; private set; } = [];

    /// <summary>Seite ab 0.</summary>
    public int Seite { get; private set; }

    /// <summary>Markierte Position (self.active).</summary>
    public int? Aktiv { get; private set; }

    /// <summary>Wert der Status-Auswahl (fuer next_after).</summary>
    public string Status => (string?)_status.SelectedItem ?? FragenStatus.Alle;

    private static TextBlock Ueberschrift(string text)
    {
        var label = LernTeile.Label(text);
        label.Margin = new Thickness(0, 10, 0, 4);
        return label;
    }

    private ComboBox Auswahl(IEnumerable<string> werte)
    {
        var liste = LernTeile.Themenliste();
        liste.HorizontalAlignment = HorizontalAlignment.Stretch;
        liste.MinWidth = 0;
        liste.ItemsSource = werte.ToList();
        liste.SelectedIndex = 0;
        liste.SelectionChanged += (_, _) =>
        {
            if (!_setzen)
            {
                Neu();
            }
        };
        return liste;
    }

    private Button Pfeil(string zeichen, string name, int schritt)
    {
        var knopf = LernTeile.Knopf(zeichen);
        knopf.Margin = new Thickness(0);
        ToolTip.SetTip(knopf, name);
        Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        knopf.Click += (_, _) => Blaettern(schritt);
        return knopf;
    }

    private string Fachbereich()
    {
        if (_fachbereich?.SelectedItem is string kurz && kurz != FragenStatus.Alle)
        {
            return Fachbereiche.Alle.FirstOrDefault(f => Fachbereiche.Kurzname[f] == kurz) ?? FragenStatus.Alle;
        }
        return FragenStatus.Alle;
    }

    /// <summary>refresh: Lernstand und Filter neu anwenden; ohne seiteBehalten zurueck auf Seite 1.</summary>
    public void Neu(bool seiteBehalten = false)
    {
        Stati = _statiQuelle();
        Gefiltert = Listen.Positionen(_eintraege, _suche.Text, Fachbereich(), _gruppenFeld,
            (string?)_gruppe.SelectedItem ?? FragenStatus.Alle, Status, Stati);
        if (!seiteBehalten)
        {
            Seite = 0;
        }
        Zeichnen();
    }

    /// <summary>turn: eine Seite vor oder zurueck.</summary>
    public void Blaettern(int schritt)
    {
        Seite += schritt;
        Zeichnen();
    }

    /// <summary>show: Position markieren und zu ihrer Seite blaettern; passt sie nicht zu den Filtern, werden diese zurueckgesetzt.</summary>
    public void Zeigen(int position)
    {
        Aktiv = position;
        Stati = _statiQuelle();
        if (!Gefiltert.Contains(position))
        {
            _setzen = true;
            _suche.Text = "";
            if (_fachbereich is not null)
            {
                _fachbereich.SelectedIndex = 0;
            }
            _gruppe.SelectedIndex = 0;
            _status.SelectedIndex = 0;
            _setzen = false;
            Gefiltert = Listen.Positionen(_eintraege, "", FragenStatus.Alle, _gruppenFeld, FragenStatus.Alle, FragenStatus.Alle, Stati);
        }
        Seite = Gefiltert.IndexOf(position) / Listen.Seitengroesse;
        Zeichnen();
    }

    /// <summary>next_after der aktuellen Auswahl.</summary>
    public int NaechsteNach(int position) => Listen.NaechsteNach(position, Gefiltert, _eintraege.Count, Status, Stati);

    private void Zeichnen()
    {
        var (sichtbar, seite, seiten) = Listen.Seite(Gefiltert, Seite);
        Seite = seite;
        _zeilen.Children.Clear();
        _leer.IsVisible = sichtbar.Count == 0;
        foreach (var position in sichtbar)
        {
            _zeilen.Children.Add(Zeile(position));
        }
        _zaehler.Text = SzenarienTexte.ZaehlerText(Gefiltert.Count, _eintraege.Count, _nomen);
        _seiteText.Text = SzenarienTexte.SeiteText(seite, seiten);
        _zurueck.IsEnabled = seite > 0;
        _weiter.IsEnabled = seite < seiten - 1;
    }

    private Border Zeile(int position)
    {
        var eintrag = _eintraege[position];
        var (status, stufe) = Stati.TryGetValue(position, out var s) ? s : (FragenStatus.Offen, "");
        var aktiv = position == Aktiv;
        var titel = new LernText(SzenarienTexte.Eintrag(position, eintrag.Titel ?? "", status == FragenStatus.Fertig), "Normal", FontWeight.SemiBold,
            aktiv ? "Pinsel.text" : "Pinsel.text_dim");
        var unter = new LernText(SzenarienTexte.Unterzeile(_unterzeile(eintrag), status), "Klein", FontWeight.Normal,
            LernTeile.Pinsel(stufe.Length > 0 ? stufe : status));
        var streifen = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 10, 2) }
            .Farbe(Border.BackgroundProperty, Farben.GetValueOrDefault(eintrag.Fachbereich ?? "", "Pinsel.muted"));
        var inhalt = new DockPanel { Children = { streifen, new StackPanel { Spacing = 2, Children = { titel, unter } } } };
        DockPanel.SetDock(streifen, Dock.Left);
        var zeile = new Border
        {
            Child = inhalt,
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
        }.Farbe(Border.BackgroundProperty, aktiv ? "Pinsel.card_hi" : "Pinsel.card_alt")
            .Farbe(Border.BorderBrushProperty, aktiv ? "Pinsel.purple" : "Pinsel.border");
        Avalonia.Automation.AutomationProperties.SetName(zeile, titel.Text);
        zeile.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _gewaehlt(position);
            }
        };
        zeile.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                _gewaehlt(position);
            }
        };
        return zeile;
    }
}
