using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 0.78.0 "Zeichen-Baukasten" (Weisung P12-266): Zeichenflaeche einer Zeichenaufgabe im Lauf. Ein Aufbau fuer PC und Handy
/// und fuer alle Eingaben:
/// <list type="bullet">
/// <item>Knopf je Baustein ("Aktion setzen"): der Baustein erscheint an der naechsten freien Stelle (unter dem gewaehlten).</item>
/// <item>Maus und Finger: auf einen Baustein tippen = waehlen, auf ein freies Feld = den gewaehlten dorthin setzen,
/// "Verbinden durch Antippen" und dann das Ziel antippen.</item>
/// <item>Tastatur und Screenreader: Liste "Deine Zeichnung" (je Baustein ein Knopf mit Stelle und "verbunden mit …"), beim
/// gewaehlten Baustein Text, Schluessel, "Nach links/rechts/oben/unten", "Verbinden mit" (Auswahl) und je Verbindung
/// Bedingung, Kardinalitaet und "entfernen".</item>
/// </list>
/// Alle Aenderungen laufen ueber <see cref="EchtBaukasten"/> (nur lesbare Zustaende); <see cref="Geaendert"/> meldet die
/// Zeichnung zum Speichern (sofort, bei Texten nach einer kurzen Pause).
/// </summary>
public sealed class EchtBaukastenTeil : StackPanel
{
    private readonly EchtBaukasten _bau;
    private readonly Action<bool> _geaendert;
    private readonly Action<string?>? _gewaehltGeaendert;
    private readonly DiagrammBild _bild;
    private readonly Bildkasten _kasten;
    private readonly LernText _status;
    private readonly StackPanel _karte = new() { Spacing = 8 };
    private readonly StackPanel _liste = new() { Spacing = 6 };
    private readonly Dictionary<string, Button> _setzen = new(StringComparer.Ordinal);
    private string? _gewaehlt;
    private bool _verbinden;
    private Point? _druck;

    /// <param name="geaendert">Zeichnung geaendert; true = sofort speichern, false = nach kurzer Pause (Text).</param>
    /// <param name="gewaehlt">Gewaehlter Baustein beim Aufbau (die Seite baut sich z. B. bei neuer Breite neu).</param>
    /// <param name="gewaehltGeaendert">Meldet jede neue Wahl, damit sie einen Neuaufbau uebersteht.</param>
    public EchtBaukastenTeil(EchtBaukasten bau, Action<bool> geaendert, string? gewaehlt = null, Action<string?>? gewaehltGeaendert = null)
    {
        _bau = bau;
        _geaendert = geaendert;
        _gewaehltGeaendert = gewaehltGeaendert;
        _gewaehlt = bau.Element(gewaehlt)?.Id;
        Spacing = 8;
        Children.Add(LernTeile.Label(EchtTexte.Baukasten));
        Children.Add(new LernText(EchtTexte.BaukastenAnleitung, "Klein", FontWeight.Normal, "Pinsel.muted"));
        if (bau.Unlesbar)
        {
            Children.Add(new LernText(EchtTexte.ZeichnungUnlesbar, "Klein", FontWeight.Bold, "Pinsel.red"));
        }
        var palette = new WrapPanel();
        foreach (var typ in EchtZeichnung.Bausteine[bau.Art])
        {
            var text = Beschriftungen.Einsetzen(EchtTexte.BausteinSetzen, EchtZeichnung.Baustein(typ));
            var knopf = Knopf(text);
            knopf.Margin = new Thickness(0, 0, 8, 8);
            knopf.Click += (_, _) => Setzen(typ);
            _setzen[typ] = knopf;
            palette.Children.Add(knopf);
        }
        Children.Add(palette);
        _bild = new DiagrammBild(bau.Diagramm, raster: true) { Gewaehlt = _gewaehlt };
        Avalonia.Automation.AutomationProperties.SetName(_bild, Beschriftungen.Einsetzen(EchtTexte.BildName,
            EchtTexte.Zeichenflaeche + ": " + EchtZeichnung.Name(bau.Art)));
        _bild.PointerPressed += (_, e) => _druck = e.GetPosition(_bild);
        _bild.PointerReleased += (_, e) =>
        {
            var los = e.GetPosition(_bild);
            if (_druck is { } druck && Math.Abs(los.X - druck.X) < 12 && Math.Abs(los.Y - druck.Y) < 12)
            {
                Tippen(los);
            }
            _druck = null;
        };
        _kasten = new Bildkasten(_bild);
        Children.Add(_kasten);
        _status = new LernText("", "Klein", FontWeight.Bold, "Pinsel.accent") { IsVisible = false };
        Children.Add(_status);
        Children.Add(new Border
        {
            Child = _karte,
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border"));
        Children.Add(LernTeile.Label(EchtTexte.DeineZeichnung));
        Children.Add(_liste);
        KarteMalen();
        ListeMalen();
    }

    /// <summary>Die Zeichnung zum Speichern (leer = nichts Eigenes).</summary>
    public string Json => _bau.Json();

    // -- Bedienung ------------------------------------------------------------------------------

    private void Setzen(string typ)
    {
        if (_bau.Setzen(typ, _gewaehlt) is not { } id)
        {
            Hinweis(EchtTexte.RasterVoll);
            return;
        }
        _verbinden = false;
        Waehlen(id, aenderung: true);
        // Text gleich eintippen (Tastatur): Fokus ins Textfeld des neuen Bausteins
        if (EchtBaukasten.MitText(typ) && _karte.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault() is { } feld)
        {
            Dispatcher.UIThread.Post(() => feld.Focus(), DispatcherPriority.Background);
        }
    }

    private void Tippen(Point punkt)
    {
        if (_bild.Feld(punkt) is not { } feld)
        {
            return;
        }
        var ziel = _bau.An(feld.X, feld.Y);
        if (_verbinden && _gewaehlt is { } von)
        {
            _verbinden = false;
            if (ziel is not null && ziel.Id != von)
            {
                Verbinden(von, ziel.Id);
            }
            else
            {
                KarteMalen();
                Hinweis(null);
            }
            return;
        }
        if (ziel is not null)
        {
            Waehlen(ziel.Id, aenderung: false);
        }
        else if (_gewaehlt is { } id && !_bau.IstFest(id))
        {
            if (_bau.Verschieben(id, feld.X, feld.Y))
            {
                Neu(sofort: true);
            }
        }
        else
        {
            Waehlen(null, aenderung: false);
        }
    }

    private void Verbinden(string von, string nach)
    {
        var fehler = _bau.Verbinden(von, nach);
        Neu(sofort: fehler is null);
        Hinweis(fehler);
    }

    private void Waehlen(string? id, bool aenderung)
    {
        _gewaehlt = id;
        _gewaehltGeaendert?.Invoke(id);
        _verbinden = false;
        Hinweis(null);
        if (aenderung)
        {
            Neu(sofort: true);
        }
        else
        {
            _bild.Gewaehlt = id;
            KarteMalen();
            ListeMalen();
        }
    }

    /// <summary>Nach einer Aenderung: Bild, Karte und Liste neu, speichern.</summary>
    private void Neu(bool sofort)
    {
        if (_bau.Element(_gewaehlt) is null)
        {
            _gewaehlt = null;
        }
        _bild.Daten = _bau.Diagramm;
        _bild.Gewaehlt = _gewaehlt;
        KarteMalen();
        ListeMalen();
        _geaendert(sofort);
    }

    /// <summary>Nach einer Text-Aenderung: Bild und Liste neu, die Karte bleibt (Fokus im Feld), speichern nach Pause.</summary>
    private void TextNeu()
    {
        _bild.Daten = _bau.Diagramm;
        ListeMalen();
        _geaendert(false);
    }

    private void Hinweis(string? text)
    {
        var zeigen = text ?? (_verbinden && _bau.Element(_gewaehlt) is { } e
            ? Beschriftungen.Einsetzen(EchtTexte.VerbindenHinweis, EchtZeichnung.Bezeichnung(_bau.Diagramm, e)) : null);
        _status.Text = zeigen ?? "";
        _status.IsVisible = zeigen is not null;
    }

    // -- Karte des gewaehlten Bausteins ---------------------------------------------------------------

    private void KarteMalen()
    {
        _karte.Children.Clear();
        if (_bau.Element(_gewaehlt) is not { } e)
        {
            _karte.Children.Add(new LernText(EchtTexte.NichtsGewaehlt, "Klein", FontWeight.Normal, "Pinsel.muted"));
            return;
        }
        var d = _bau.Diagramm;
        var fest = _bau.IstFest(e.Id);
        _karte.Children.Add(Schrift.Text(Beschriftungen.Einsetzen(EchtTexte.Gewaehlt, _bau.Name(e)), "Normal", FontWeight.Bold, "Pinsel.text"));
        if (fest)
        {
            _karte.Children.Add(new LernText(EchtTexte.Fest, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        else
        {
            if (EchtBaukasten.MitText(e.Typ))
            {
                _karte.Children.Add(LernTeile.Label(EchtTexte.BausteinText));
                var feld = Feld(e.Text, EchtTexte.BausteinText);
                feld.TextChanged += (_, _) =>
                {
                    if (_bau.Beschriften(e.Id, feld.Text ?? ""))
                    {
                        TextNeu();
                    }
                };
                _karte.Children.Add(feld);
            }
            if (e.Typ == "attribut")
            {
                var schluessel = new CheckBox
                {
                    Content = new LernText(EchtTexte.SchluesselAttribut, "Normal", FontWeight.Normal, "Pinsel.text_soft"),
                    IsChecked = e.Schluessel,
                };
                Avalonia.Automation.AutomationProperties.SetName(schluessel, EchtTexte.SchluesselAttribut);
                schluessel.IsCheckedChanged += (_, _) =>
                {
                    if (_bau.Schluessel(e.Id, schluessel.IsChecked == true))
                    {
                        Neu(sofort: true);
                    }
                };
                _karte.Children.Add(schluessel);
            }
            var schieben = new WrapPanel();
            foreach (var (text, dx, dy) in new[] { (EchtTexte.NachLinks, -1, 0), (EchtTexte.NachRechts, 1, 0), (EchtTexte.NachOben, 0, -1),
                (EchtTexte.NachUnten, 0, 1) })
            {
                var knopf = Knopf(text);
                knopf.Margin = new Thickness(0, 0, 8, 8);
                knopf.IsEnabled = _bau.An(e.X + dx, e.Y + dy) is null && e.X + dx >= 0 && e.Y + dy >= 0
                    && e.X + dx < EchtZeichnung.RasterBreite && e.Y + dy < EchtZeichnung.RasterHoehe;
                knopf.Click += (_, _) =>
                {
                    if (_bau.Element(e.Id) is { } jetzt && _bau.Verschieben(e.Id, jetzt.X + dx, jetzt.Y + dy))
                    {
                        Neu(sofort: true);
                        // Fokus bleibt auf dem gleichen Knopf (Tastatur: mehrmals druecken)
                        Dispatcher.UIThread.Post(() => _karte.GetLogicalDescendants().OfType<Button>()
                            .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == text && b.IsEnabled)?.Focus(),
                            DispatcherPriority.Background);
                    }
                };
                schieben.Children.Add(knopf);
            }
            _karte.Children.Add(schieben);
        }

        // Verbinden: durch Antippen (Maus, Finger) oder ueber die Auswahl (Tastatur, Screenreader)
        var antippen = Knopf(_verbinden ? EchtTexte.VerbindenAbbrechen : EchtTexte.VerbindenTippen);
        antippen.HorizontalAlignment = HorizontalAlignment.Left;
        antippen.Click += (_, _) =>
        {
            _verbinden = !_verbinden;
            KarteMalen();
            Hinweis(null);
        };
        _karte.Children.Add(antippen);
        var andere = d.Elemente.Where(x => x.Id != e.Id && _bau.VerbindungenVon(e.Id).All(v => v.Von != x.Id && v.Nach != x.Id))
            .OrderBy(x => x.Y).ThenBy(x => x.X).ToList();
        if (andere.Count > 0)
        {
            _karte.Children.Add(LernTeile.Label(EchtTexte.VerbindenMit));
            var auswahl = Auswahl(andere.Select(x => _bau.Name(x)).ToList(), EchtTexte.VerbindenMit);
            auswahl.SelectedIndex = 0;
            var verbinden = Knopf(EchtTexte.Verbinden);
            verbinden.HorizontalAlignment = HorizontalAlignment.Left;
            verbinden.Click += (_, _) =>
            {
                if (auswahl.SelectedIndex >= 0 && auswahl.SelectedIndex < andere.Count)
                {
                    Verbinden(e.Id, andere[auswahl.SelectedIndex].Id);
                }
            };
            _karte.Children.Add(auswahl);
            _karte.Children.Add(verbinden);
        }

        var verbindungen = _bau.VerbindungenVon(e.Id);
        if (verbindungen.Count > 0)
        {
            _karte.Children.Add(LernTeile.Label(EchtTexte.Verbindungen));
        }
        foreach (var v in verbindungen)
        {
            var anderer = d.Element(v.Von == e.Id ? v.Nach : v.Von);
            var name = EchtZeichnung.Bezeichnung(d, anderer);
            var zeile = new StackPanel { Spacing = 4 };
            var muster = _bau.Art == EchtZeichnung.Er ? EchtTexte.LinieZu : v.Von == e.Id ? EchtTexte.PfeilZu : EchtTexte.PfeilVon;
            zeile.Children.Add(new LernText(Beschriftungen.Einsetzen(muster, name), "Klein", FontWeight.Bold, "Pinsel.text_soft"));
            if (_bau.BedingungMoeglich(v))
            {
                var titel = Beschriftungen.Einsetzen(EchtTexte.Bedingung, EchtZeichnung.Bezeichnung(d, d.Element(v.Nach)));
                var bedingung = Feld(v.Text, titel);
                bedingung.TextChanged += (_, _) =>
                {
                    if (_bau.Bedingung(v.Von, v.Nach, bedingung.Text ?? ""))
                    {
                        TextNeu();
                    }
                };
                zeile.Children.Add(bedingung);
            }
            if (_bau.KardinalitaetMoeglich(v))
            {
                var entitaet = d.Element(d.Element(v.Von).Typ == "entitaet" ? v.Von : v.Nach);
                var titel = Beschriftungen.Einsetzen(EchtTexte.Kardinalitaet, EchtZeichnung.Bezeichnung(d, entitaet));
                List<string> werte = ["", .. EchtZeichnung.Kardinalitaeten];
                var kard = Auswahl(werte.Select(w => w.Length == 0 ? EchtTexte.OhneKardinalitaet : w).ToList(), titel);
                kard.SelectedIndex = Math.Max(0, werte.IndexOf(v.Kard));
                kard.SelectionChanged += (_, _) =>
                {
                    if (kard.SelectedIndex >= 0 && _bau.Kardinalitaet(v.Von, v.Nach, werte[kard.SelectedIndex]))
                    {
                        _bild.Daten = _bau.Diagramm;
                        ListeMalen();
                        _geaendert(true);
                    }
                };
                zeile.Children.Add(LernTeile.Label(titel));
                zeile.Children.Add(kard);
            }
            if (!_bau.IstFesteVerbindung(v))
            {
                var trennen = Knopf(Beschriftungen.Einsetzen(EchtTexte.VerbindungEntfernen, name));
                trennen.HorizontalAlignment = HorizontalAlignment.Left;
                trennen.Click += (_, _) =>
                {
                    if (_bau.Trennen(v.Von, v.Nach))
                    {
                        Neu(sofort: true);
                    }
                };
                zeile.Children.Add(trennen);
            }
            _karte.Children.Add(new Border { Child = zeile, Padding = new Thickness(0, 4, 0, 0) });
        }
        if (!fest)
        {
            var entfernen = Knopf(EchtTexte.BausteinEntfernen);
            entfernen.HorizontalAlignment = HorizontalAlignment.Left;
            entfernen.Click += (_, _) =>
            {
                if (_bau.Entfernen(e.Id))
                {
                    _gewaehlt = null;
                    Neu(sofort: true);
                }
            };
            _karte.Children.Add(entfernen);
        }
    }

    // -- Liste fuer Screenreader und Tastatur ---------------------------------------------------------

    private void ListeMalen()
    {
        _liste.Children.Clear();
        var zeilen = _bau.Zeilen();
        if (zeilen.Count == 0)
        {
            _liste.Children.Add(new LernText(EchtTexte.ZeichnungLeer, "Klein", FontWeight.Normal, "Pinsel.muted"));
            return;
        }
        foreach (var (id, zeile) in zeilen)
        {
            var gewaehlt = id == _gewaehlt;
            var knopf = new Button
            {
                Content = new LernText(zeile, "Klein", gewaehlt ? FontWeight.Bold : FontWeight.Normal, gewaehlt ? "Pinsel.accent" : "Pinsel.text_soft"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 6),
            }.Farbe(Button.BackgroundProperty, "Pinsel.card_alt");
            Avalonia.Automation.AutomationProperties.SetName(knopf, zeile);
            knopf.Click += (_, _) => Waehlen(id, aenderung: false);
            _liste.Children.Add(knopf);
        }
    }

    // -- Bausteine ------------------------------------------------------------------------------

    private static Button Knopf(string text)
    {
        var knopf = LernTeile.Knopf(text);
        Avalonia.Automation.AutomationProperties.SetName(knopf, text);
        return knopf;
    }

    private static TextBox Feld(string text, string name)
    {
        var feld = new TextBox { Text = text, TextWrapping = TextWrapping.Wrap, MaxLength = EchtBaukasten.TextLaenge }
            .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card").Groesse();
        Avalonia.Automation.AutomationProperties.SetName(feld, name);
        return feld;
    }

    private static ComboBox Auswahl(IReadOnlyList<string> eintraege, string name)
    {
        var liste = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = eintraege,
            ItemTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }.Groesse()),
        }.Farbe(ComboBox.BackgroundProperty, "Pinsel.card").Farbe(ComboBox.ForegroundProperty, "Pinsel.text");
        Avalonia.Automation.AutomationProperties.SetName(liste, name);
        return liste;
    }

    // -- Probe (Start-Test) ---------------------------------------------------------------------------

    internal EchtBaukasten ProbeBau => _bau;

    internal DiagrammBild ProbeBild => _bild;

    internal Bildkasten ProbeKasten => _kasten;

    internal string? ProbeGewaehlt => _gewaehlt;

    internal StackPanel ProbeKarte => _karte;

    internal StackPanel ProbeListe => _liste;

    internal Button ProbeSetzen(string typ) => _setzen[typ];

    /// <summary>Probe: Baustein waehlen wie mit einem Klick in der Liste.</summary>
    internal void ProbeWaehlen(string? id) => Waehlen(id, aenderung: false);
}
