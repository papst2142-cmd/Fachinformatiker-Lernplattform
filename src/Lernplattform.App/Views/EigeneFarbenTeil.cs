using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 1, K5/K8: "Eigene Farben" wie in der Python-Linie (ab 0.57/0.58): drei Regler-Gruppen
/// (Akzent 1, Akzent 2, Hintergrund), Vorschau, Kontrast-Warnung und Zustandszeile. Die
/// Regler aendern nur die Vorschau; gespeichert wird erst mit "Speichern". Ein Klick auf
/// eine Vorlage setzt nur die Regler, wenn eigene Farben gespeichert oder Regler bewegt sind
/// (tile_sets_sliders). Alle Rechnungen kommen aus Core.Design.EigeneFarben (G6b).
/// </summary>
public sealed class EigeneFarbenTeil
{
    private readonly Func<bool, bool> _ergebnis;
    private readonly Dictionary<(string Teil, string Kanal), (Slider Regler, TextBlock Wert)> _regler = [];
    private readonly Dictionary<string, Border> _muster = [];
    private readonly TextBlock _zustand = Schrift.Text("", "Normal", FontWeight.SemiBold);
    private readonly TextBlock _warnung = Schrift.Text("", "Klein");
    private readonly Border _vorschau = new() { CornerRadius = new CornerRadius(12), Padding = new Thickness(12) };
    private readonly Border _vorschauKarte = new() { CornerRadius = new CornerRadius(10), Padding = new Thickness(12), BorderThickness = new Thickness(1) };
    private readonly TextBlock _vTitel = new() { Text = EigeneFarben.VorschauUeberschrift, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _vText = new() { Text = EigeneFarben.VorschauText, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _vNeben = new() { Text = EigeneFarben.VorschauNebentext, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _vLink = new() { Text = EigeneFarben.VorschauLink, TextDecorations = TextDecorations.Underline };
    private readonly Border _vKnopf = new() { CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 6), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _vKnopfText = new() { Text = EigeneFarben.VorschauKnopf };

    private string _darstellung = Farbwelt.Dunkel, _grund = Farbwelt.StandardGrundfarbe, _hinter = Farbwelt.StandardHintergrund;
    private EigeneWerte? _gespeichert;
    private EigeneWerte _werte = EigeneFarben.Start(Farbwelt.Dunkel, Farbwelt.StandardGrundfarbe, Farbwelt.StandardHintergrund);
    private (string Grund, string Hinter)? _welt;
    private bool _setzen;

    public EigeneFarbenTeil(Func<bool, bool> ergebnis) => _ergebnis = ergebnis;

    /// <summary>Hintergrund-Kacheln, deren Name von der Darstellung abhaengt.</summary>
    public List<(Farbwelt.Hintergrund Hintergrund, TextBlock Name)> Hintergrundnamen { get; } = [];

    public Control Bauen()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Schrift.Text(EigeneFarben.Titel, "Klein", FontWeight.Bold, "Pinsel.muted"));
        panel.Children.Add(Schrift.Text(EigeneFarben.Hinweis, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        panel.Children.Add(Schrift.Text(EigeneFarben.KachelHinweis, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        panel.Children.Add(_zustand);
        foreach (var (teil, teilName) in EigeneFarben.Teile)
        {
            var muster = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), VerticalAlignment = VerticalAlignment.Center };
            _muster[teil] = muster;
            var gruppe = new StackPanel { Spacing = 4 };
            gruppe.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { muster, Schrift.Text(teilName, "Normal", FontWeight.SemiBold) },
            });
            foreach (var kanal in EigeneFarben.Kanaele)
            {
                var regler = new Slider
                {
                    Minimum = kanal.Min,
                    Maximum = kanal.Max,
                    TickFrequency = 1,
                    IsSnapToTickEnabled = true,
                    SmallChange = 1,
                    LargeChange = 10,
                    MinWidth = 160,
                };
                var wert = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.text_dim");
                wert.MinWidth = 48;
                wert.VerticalAlignment = VerticalAlignment.Center;
                var name = Schrift.Text(kanal.Name, "Klein", FontWeight.Normal, "Pinsel.text_dim");
                name.VerticalAlignment = VerticalAlignment.Center;
                var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
                name.MinWidth = 90;
                Grid.SetColumn(regler, 1);
                Grid.SetColumn(wert, 2);
                zeile.Children.Add(name);
                zeile.Children.Add(regler);
                zeile.Children.Add(wert);
                var (t, k) = (teil, kanal);
                regler.ValueChanged += (_, e) =>
                {
                    if (_setzen)
                    {
                        return;
                    }
                    var neu = (int)Math.Round(e.NewValue);
                    _werte = Mit(_werte, t, k.Id, neu);
                    Zeichnen();
                };
                _regler[(teil, kanal.Id)] = (regler, wert);
                gruppe.Children.Add(zeile);
            }
            panel.Children.Add(gruppe);
        }

        _vorschauKarte.Child = new StackPanel { Spacing = 6, Children = { _vTitel, _vText, _vNeben, _vLink, _vKnopf } };
        _vKnopf.Child = _vKnopfText;
        _vorschau.Child = new StackPanel
        {
            Spacing = 6,
            Children = { Schrift.Text(EigeneFarben.Vorschau, "Klein", FontWeight.Bold, "Pinsel.muted"), _vorschauKarte },
        };
        foreach (var text in (TextBlock[])[_vTitel, _vText, _vNeben, _vLink, _vKnopfText])
        {
            text.Groesse(text == _vTitel ? "Mittel" : "Normal");
        }
        panel.Children.Add(_vorschau);
        panel.Children.Add(_warnung);

        var speichern = Schrift.Knopf(EigeneFarben.SpeichernText, true);
        speichern.Click += (_, _) => Speichern();
        var zurueck = Schrift.Knopf(EigeneFarben.ZuruecksetzenText);
        zurueck.Click += (_, _) => Zuruecksetzen();
        panel.Children.Add(new WrapPanel { Children = { speichern, zurueck } });
        Neu();
        return panel;
    }

    private static EigeneWerte Mit(EigeneWerte werte, string teil, string kanal, int wert)
    {
        static Regler Setze(Regler r, string kanal, int wert) => kanal switch
        {
            "h" => r with { H = wert },
            "s" => r with { S = wert },
            _ => r with { L = wert },
        };
        return teil switch
        {
            EigeneFarben.TeilAkzent1 => werte with { Akzent1 = Setze(werte.Akzent1, kanal, wert) },
            EigeneFarben.TeilAkzent2 => werte with { Akzent2 = Setze(werte.Akzent2, kanal, wert) },
            _ => werte with { Hintergrund = Setze(werte.Hintergrund, kanal, wert) },
        };
    }

    private static Regler Teil(EigeneWerte werte, string teil) => teil switch
    {
        EigeneFarben.TeilAkzent1 => werte.Akzent1,
        EigeneFarben.TeilAkzent2 => werte.Akzent2,
        _ => werte.Hintergrund,
    };

    /// <summary>Gespeicherten Stand neu lesen (nach Speichern oder Wechsel der Darstellung).</summary>
    public void Neu()
    {
        var w = Zustand.Werte;
        (_darstellung, _grund, _hinter) = (Einstellungen.Darstellung(w), Einstellungen.Grundfarbe(w), Einstellungen.Hintergrund(w));
        _gespeichert = EigeneFarben.Gespeichert(w[EigeneFarben.Schluessel]).TryGetValue(_darstellung, out var e) ? e : null;
        _welt = null;
        Zeige(_gespeichert ?? EigeneFarben.Start(_darstellung, _grund, _hinter));
        foreach (var (h, name) in Hintergrundnamen)
        {
            name.Text = FarbenTexte.HintergrundName(h, _darstellung);
        }
    }

    private void Zeige(EigeneWerte werte)
    {
        _werte = werte;
        _setzen = true;
        foreach (var (teil, teilName) in EigeneFarben.Teile)
        {
            var r = Teil(werte, teil);
            foreach (var kanal in EigeneFarben.Kanaele)
            {
                var wert = kanal.Id switch { "h" => r.H, "s" => r.S, _ => r.L };
                _regler[(teil, kanal.Id)].Regler.Value = wert;
                Avalonia.Automation.AutomationProperties.SetName(_regler[(teil, kanal.Id)].Regler,
                    EigeneFarben.ReglerBeschriftung(teilName, kanal, wert));
            }
        }
        _setzen = false;
        Zeichnen();
    }

    private void Zeichnen()
    {
        var (grund, hinter) = _welt ?? (_grund, _hinter);
        var palette = EigeneFarben.WertePalette(_werte, _darstellung, grund, hinter);
        foreach (var (teil, teilName) in EigeneFarben.Teile)
        {
            var r = Teil(_werte, teil);
            _muster[teil].Background = new SolidColorBrush(Color.Parse(EigeneFarben.HslZuHex(r.H, r.S, r.L)));
            foreach (var kanal in EigeneFarben.Kanaele)
            {
                var wert = kanal.Id switch { "h" => r.H, "s" => r.S, _ => r.L };
                _regler[(teil, kanal.Id)].Wert.Text = EigeneFarben.ReglerText(kanal, wert);
                Avalonia.Automation.AutomationProperties.SetName(_regler[(teil, kanal.Id)].Regler,
                    EigeneFarben.ReglerBeschriftung(teilName, kanal, wert));
            }
        }
        SolidColorBrush P(string name) => new(Color.Parse(palette.Farben[name]));
        _vorschau.Background = P("bg");
        _vorschauKarte.Background = P("card");
        _vorschauKarte.BorderBrush = P("border");
        _vTitel.Foreground = P("text");
        _vText.Foreground = P("text");
        _vNeben.Foreground = P("text_dim");
        _vLink.Foreground = P("accent");
        var (von, bis) = palette.Verlaeufe["primary"];
        _vKnopf.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse(von), 0), new GradientStop(Color.Parse(bis), 1) },
        };
        _vKnopfText.Foreground = P("on_accent");
        _zustand.Text = EigeneFarben.ZustandText(_werte, _gespeichert, _darstellung, _grund, _hinter);
        // Kartenfarben der Spielwelt kommen mit dem Spiel (Phase 5); bis dahin ohne Kartenpruefung
        var zeilen = EigeneFarben.Warnung(palette);
        if (zeilen.Count > 0)
        {
            _warnung.Text = "⚠ " + string.Join("\n", zeilen);
            _warnung.Farbe(TextBlock.ForegroundProperty, "Pinsel.red");
        }
        else
        {
            _warnung.Text = "✓ " + EigeneFarben.AllesGut;
            _warnung.Farbe(TextBlock.ForegroundProperty, "Pinsel.green");
        }
    }

    /// <summary>Klick auf eine Vorlage (Grundfarbe oder Hintergrund).</summary>
    public void Kachel(string? grundfarbe = null, string? hintergrund = null)
    {
        if (EigeneFarben.KachelSetztRegler(_darstellung, _werte, _gespeichert is not null, _welt is not null, _grund, _hinter))
        {
            // nur die Regler auf diese Farbwelt stellen (set_world), gespeichert wird mit "Speichern"
            var aktuell = _welt ?? (_grund, _hinter);
            var welt = (grundfarbe ?? aktuell.Item1, hintergrund ?? aktuell.Item2);
            _welt = welt == (_grund, _hinter) ? null : welt;
            var (g, h) = _welt ?? (_grund, _hinter);
            Zeige(EigeneFarben.Start(_darstellung, g, h));
            return;
        }
        var gespeichert = Zustand.Aendere(o =>
        {
            if (grundfarbe is not null)
            {
                o[Einstellungen.GrundfarbeSchluessel] = grundfarbe;
            }
            if (hintergrund is not null)
            {
                o[Einstellungen.HintergrundSchluessel] = hintergrund;
            }
        });
        if (_ergebnis(gespeichert))
        {
            Zustand.DesignAnwenden();
        }
        Neu();
    }

    private void Speichern() => Ablegen(EigeneFarben.ZuSpeichern(_werte, _darstellung,
        (_welt ?? (_grund, _hinter)).Item1, (_welt ?? (_grund, _hinter)).Item2), _welt);

    private void Zuruecksetzen()
    {
        if (_gespeichert is not null)
        {
            Ablegen(null, null);
            return;
        }
        // Noch nichts gespeichert: nur die Regler auf die Farbwelt stellen
        _welt = null;
        Zeige(EigeneFarben.Start(_darstellung, _grund, _hinter));
    }

    private void Ablegen(EigeneWerte? werte, (string Grund, string Hinter)? welt)
    {
        var darstellung = _darstellung;
        var gespeichert = Zustand.Aendere(o =>
        {
            if (welt is { } w)
            {
                o[Einstellungen.GrundfarbeSchluessel] = w.Grund;
                o[Einstellungen.HintergrundSchluessel] = w.Hinter;
            }
            var neu = EigeneFarben.SpeicherWert(o[EigeneFarben.Schluessel], darstellung, werte);
            if (neu is null)
            {
                o.Remove(EigeneFarben.Schluessel);
            }
            else
            {
                o[EigeneFarben.Schluessel] = neu;
            }
        });
        if (_ergebnis(gespeichert))
        {
            Zustand.DesignAnwenden();
            Neu();
        }
    }
}
