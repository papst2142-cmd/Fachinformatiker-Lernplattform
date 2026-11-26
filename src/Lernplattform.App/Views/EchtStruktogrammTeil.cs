using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 1.12.0 "Prüfung im echten Format" W2 (Weisung P12-390, Plan P12-250 Abschnitt 3): Baukasten für ein Struktogramm.
/// Ein Aufbau für PC und Handy und für alle Eingaben:
/// <list type="bullet">
/// <item>Knopf je Baustein (sieben): der Block kommt an die Stelle, die die Auswahl "Neuer Block kommt …" nennt.</item>
/// <item>Maus und Finger: Antippen eines Blocks oder Zweigs wählt ihn (kein Ziehen).</item>
/// <item>Tastatur und Screenreader: Liste "Deine Zeichnung" (Pfeil hoch/runter wandert, Eingabetaste öffnet den Text), beim
/// gewählten Block Textfeld, "Neuer Block kommt …", "Nach oben", "Nach unten", "Heraus", "Entfernen" (Rückfrage, wenn er
/// Blöcke enthält), bei der Fallauswahl "Fall hinzufügen", beim Fall "Fall entfernen".</item>
/// </list>
/// Alle Änderungen laufen über <see cref="EchtStruktogramm"/>; <see cref="Json"/> ist die Zeichnung zum Speichern.
/// </summary>
public sealed class EchtStruktogrammTeil : StackPanel
{
    private readonly EchtStruktogramm _bau;
    private readonly Action<bool> _geaendert;
    private readonly Action<string?>? _gewaehltGeaendert;
    private readonly StruktogrammBild _bild;
    private readonly Bildkasten _kasten;
    private readonly LernText _status;
    private readonly StackPanel _karte = new() { Spacing = 8 };
    private readonly StackPanel _liste = new() { Spacing = 4 };
    private readonly Dictionary<string, Button> _setzen = new(StringComparer.Ordinal);
    private string? _gewaehlt;
    private int _ziel;
    private Point? _druck;

    /// <param name="geaendert">Zeichnung geändert; true = sofort speichern, false = nach kurzer Pause (Text).</param>
    /// <param name="gewaehlt">Gewählte Stelle beim Aufbau (die Seite baut sich z. B. bei neuer Breite neu).</param>
    /// <param name="gewaehltGeaendert">Meldet jede neue Wahl, damit sie einen Neuaufbau übersteht.</param>
    public EchtStruktogrammTeil(EchtStruktogramm bau, Action<bool> geaendert, string? gewaehlt = null, Action<string?>? gewaehltGeaendert = null)
    {
        _bau = bau;
        _geaendert = geaendert;
        _gewaehltGeaendert = gewaehltGeaendert;
        _gewaehlt = bau.IstBlock(gewaehlt) || bau.IstZweig(gewaehlt) ? gewaehlt : null;
        Spacing = 8;
        Children.Add(LernTeile.Label(EchtTexte.Baukasten));
        Children.Add(new LernText(EchtTexte.StruktogrammAnleitung, "Klein", FontWeight.Normal, "Pinsel.muted"));
        if (bau.Unlesbar)
        {
            Children.Add(new LernText(EchtTexte.StruktogrammUnlesbar, "Klein", FontWeight.Bold, "Pinsel.red"));
        }
        var palette = new WrapPanel();
        foreach (var typ in EchtStruktogramm.Bausteine)
        {
            var knopf = Knopf(EchtStruktogramm.Knopf(typ));
            knopf.Margin = new Thickness(0, 0, 8, 8);
            knopf.Click += (_, _) => Setzen(typ);
            _setzen[typ] = knopf;
            palette.Children.Add(knopf);
        }
        Children.Add(palette);
        _bild = new StruktogrammBild(bau.Bloecke) { Gewaehlt = _gewaehlt };
        Avalonia.Automation.AutomationProperties.SetName(_bild, Beschriftungen.Einsetzen(EchtTexte.BildName, EchtTexte.StruktogrammFlaeche));
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

    // -- Bedienung -------------------------------------------------------------------------------------------------------

    private void Setzen(string typ)
    {
        if (_bau.Einfuegen(typ, _gewaehlt, _ziel) is not { } pfad)
        {
            Hinweis(EchtTexte.StruktogrammVoll);
            return;
        }
        Waehlen(pfad, aenderung: true);
        // Text gleich eintippen (Tastatur): Fokus ins Textfeld des neuen Blocks
        TextFokus();
    }

    private void TextFokus() =>
        Dispatcher.UIThread.Post(() => _karte.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault()?.Focus(), DispatcherPriority.Background);

    private void Tippen(Point punkt)
    {
        if (_bild.Stelle(punkt) is { } stelle)
        {
            Waehlen(stelle.Length == 0 ? null : stelle, aenderung: false);
        }
    }

    private void Waehlen(string? pfad, bool aenderung)
    {
        _gewaehlt = pfad;
        _ziel = 0;
        _gewaehltGeaendert?.Invoke(pfad);
        Hinweis(null);
        if (aenderung)
        {
            Neu(sofort: true);
        }
        else
        {
            _bild.Gewaehlt = pfad;
            KarteMalen();
            ListeMalen();
        }
    }

    /// <summary>Nach einer Änderung: Bild, Karte und Liste neu, speichern.</summary>
    private void Neu(bool sofort)
    {
        if (!_bau.IstBlock(_gewaehlt) && !_bau.IstZweig(_gewaehlt))
        {
            _gewaehlt = null;
            _gewaehltGeaendert?.Invoke(null);
        }
        _bild.Daten = _bau.Bloecke;
        _bild.Gewaehlt = _gewaehlt;
        KarteMalen();
        ListeMalen();
        _geaendert(sofort);
    }

    /// <summary>Nach einer Text-Änderung: Bild und Liste neu, die Karte bleibt (Fokus im Feld), speichern nach Pause.</summary>
    private void TextNeu()
    {
        _bild.Daten = _bau.Bloecke;
        ListeMalen();
        _geaendert(false);
    }

    private void Hinweis(string? text)
    {
        _status.Text = text ?? "";
        _status.IsVisible = text is not null;
    }

    private async void Entfernen(string pfad)
    {
        var inhalt = _bau.Inhalt(pfad);
        if (inhalt > 0 && _bau.Block(pfad) is { } block)
        {
            var frage = Beschriftungen.Einsetzen(EchtTexte.EntfernenFrage, EchtStruktogramm.Kurz(block), inhalt);
            if (await Dialog.Rueckfrage(EchtTexte.EntfernenFrageTitel, frage, 1, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
            {
                return;
            }
        }
        if (_bau.Entfernen(pfad))
        {
            _gewaehlt = null;
            _gewaehltGeaendert?.Invoke(null);
            Neu(sofort: true);
        }
    }

    // -- Karte des gewählten Blocks --------------------------------------------------------------------------------------

    private void KarteMalen()
    {
        _karte.Children.Clear();
        var block = _bau.Block(_gewaehlt);
        var zweig = block is null ? _bau.Zweig(_gewaehlt) : null;
        if (block is null && zweig is null)
        {
            _karte.Children.Add(new LernText(EchtTexte.NichtsGewaehltBlock, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        else
        {
            _karte.Children.Add(new LernText(Beschriftungen.Einsetzen(EchtTexte.Gewaehlt, _bau.Beschreibung(_gewaehlt)), "Normal", FontWeight.Bold,
                "Pinsel.text"));
            var feldname = block is not null ? EchtStruktogramm.Feldname(block.Typ) : zweig!.Art == EchtStruktogramm.Fall ? EchtTexte.FeldFall : null;
            if (feldname is not null)
            {
                var pfad = _gewaehlt!;
                var feld = Feld(block?.Text ?? zweig!.Text, feldname);
                feld.TextChanged += (_, _) =>
                {
                    if (_bau.Beschriften(pfad, feld.Text ?? ""))
                    {
                        TextNeu();
                    }
                };
                _karte.Children.Add(new FeldZeile(Beschriftung(feldname), feld.Breite(380)));
            }
        }

        // "Neuer Block kommt …" (Vorgabe: nach dem gewählten Block, in einen Zweig ans Ende, sonst ans Ende)
        var ziele = _bau.Ziele(_gewaehlt);
        var auswahl = Auswahl(ziele, EchtTexte.NeuerBlockKommt);
        auswahl.SelectedIndex = Math.Clamp(_ziel, 0, ziele.Count - 1);
        auswahl.SelectionChanged += (_, _) => _ziel = Math.Max(0, auswahl.SelectedIndex);
        _karte.Children.Add(new FeldZeile(Beschriftung(EchtTexte.NeuerBlockKommt), auswahl.Breite(380)));

        if (block is not null || zweig is not null)
        {
            var knoepfe = new WrapPanel();
            var pfad = _gewaehlt!;
            void Dazu(string text, string name, bool an, Action tun)
            {
                var k = Knopf(text, name);
                k.Margin = new Thickness(0, 0, 8, 8);
                k.IsEnabled = an;
                k.Click += (_, _) => tun();
                knoepfe.Children.Add(k);
            }
            if (block is not null)
            {
                Dazu(EchtTexte.NachOben, EchtTexte.NachOben, _bau.Schiebbar(pfad, -1), () => Schieben(pfad, -1, EchtTexte.NachOben));
                Dazu(EchtTexte.NachUnten, EchtTexte.NachUnten, _bau.Schiebbar(pfad, 1), () => Schieben(pfad, 1, EchtTexte.NachUnten));
                Dazu(EchtTexte.Heraus, EchtTexte.HerausName, _bau.Herausbar(pfad), () =>
                {
                    if (_bau.Heraus(pfad) is { } neu)
                    {
                        _gewaehlt = neu;
                        _gewaehltGeaendert?.Invoke(neu);
                        Neu(sofort: true);
                    }
                });
                Dazu(EchtTexte.Entfernen, EchtTexte.Entfernen, true, () => Entfernen(pfad));
                if (block.Typ == EchtStruktogramm.Fallauswahl)
                {
                    Dazu(EchtTexte.FallNeu, EchtTexte.FallNeu, _bau.FallMoeglich(pfad), () =>
                    {
                        if (_bau.FallNeu(pfad) is { } neu)
                        {
                            Waehlen(neu, aenderung: true);
                            TextFokus();
                        }
                    });
                }
            }
            else if (zweig!.Art == EchtStruktogramm.Fall)
            {
                Dazu(EchtTexte.FallEntfernen, EchtTexte.FallEntfernen, _bau.FallEntfernbar(pfad), () =>
                {
                    if (_bau.FallEntfernen(pfad) is { } neu)
                    {
                        Waehlen(neu, aenderung: true);
                    }
                });
            }
            _karte.Children.Add(knoepfe);
        }
    }

    private void Schieben(string pfad, int richtung, string name)
    {
        if (_bau.Schieben(pfad, richtung) is not { } neu)
        {
            return;
        }
        _gewaehlt = neu;
        _gewaehltGeaendert?.Invoke(neu);
        Neu(sofort: true);
        // Fokus bleibt auf dem gleichen Knopf (Tastatur: mehrmals drücken)
        Dispatcher.UIThread.Post(() => _karte.GetLogicalDescendants().OfType<Button>()
            .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == name && b.IsEnabled)?.Focus(), DispatcherPriority.Background);
    }

    private static LernText Beschriftung(string text) => new(text, "Klein", FontWeight.Normal, "Pinsel.text_soft")
    {
        VerticalAlignment = VerticalAlignment.Center,
    };

    // -- Liste für Screenreader und Tastatur --------------------------------------------------------------------------------

    private void ListeMalen()
    {
        _liste.Children.Clear();
        var zeilen = _bau.Zeilen();
        if (zeilen.Count == 0)
        {
            _liste.Children.Add(new LernText(EchtTexte.StruktogrammLeer, "Klein", FontWeight.Normal, "Pinsel.muted"));
            return;
        }
        var knoepfe = new List<Button>();
        foreach (var (pfad, zeile, ebene) in zeilen)
        {
            var gewaehlt = pfad == _gewaehlt;
            var knopf = new Button
            {
                Content = new LernText(zeile, "Klein", gewaehlt ? FontWeight.Bold : FontWeight.Normal, gewaehlt ? "Pinsel.accent" : "Pinsel.text_soft"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 6),
                Margin = new Thickness(Math.Min(ebene, 6) * 14, 0, 0, 0),
            }.Farbe(Button.BackgroundProperty, "Pinsel.card_alt");
            Avalonia.Automation.AutomationProperties.SetName(knopf, _bau.Beschreibung(pfad));
            knopf.Click += (_, _) => Waehlen(pfad, aenderung: false);
            var nr = knoepfe.Count;
            knopf.AddHandler(KeyDownEvent, (_, e) =>
            {
                // Pfeil hoch/runter wandert durch die Liste und wählt, Eingabetaste öffnet den Text
                if (e.Key is Key.Down or Key.Up)
                {
                    var ziel = nr + (e.Key == Key.Down ? 1 : -1);
                    if (ziel >= 0 && ziel < zeilen.Count)
                    {
                        Waehlen(zeilen[ziel].Pfad, aenderung: false);
                        Dispatcher.UIThread.Post(() => (_liste.Children.ElementAtOrDefault(ziel) as Button)?.Focus(), DispatcherPriority.Background);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    Waehlen(pfad, aenderung: false);
                    TextFokus();
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);
            knoepfe.Add(knopf);
            _liste.Children.Add(knopf);
        }
    }

    // -- Bausteine -------------------------------------------------------------------------------------------------------

    private static Button Knopf(string text, string? name = null)
    {
        var knopf = LernTeile.Knopf(text);
        Avalonia.Automation.AutomationProperties.SetName(knopf, name ?? text);
        return knopf;
    }

    private static TextBox Feld(string text, string name)
    {
        var feld = new TextBox { Text = text, TextWrapping = TextWrapping.Wrap, MaxLength = EchtStruktogramm.TextLaenge, AcceptsReturn = false }
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

    // -- Probe (Start-Test) ------------------------------------------------------------------------------------------------

    internal EchtStruktogramm ProbeBau => _bau;

    internal StruktogrammBild ProbeBild => _bild;

    internal Bildkasten ProbeKasten => _kasten;

    internal string? ProbeGewaehlt => _gewaehlt;

    internal StackPanel ProbeKarte => _karte;

    internal StackPanel ProbeListe => _liste;

    internal Button ProbeSetzen(string typ) => _setzen[typ];

    /// <summary>Probe: Stelle wählen wie mit einem Klick in der Liste.</summary>
    internal void ProbeWaehlen(string? pfad) => Waehlen(pfad, aenderung: false);

    /// <summary>Probe: Ziel "Neuer Block kommt …" setzen (Nummer in <see cref="EchtStruktogramm.Ziele"/>).</summary>
    internal void ProbeZiel(int ziel)
    {
        _ziel = ziel;
        KarteMalen();
    }
}
