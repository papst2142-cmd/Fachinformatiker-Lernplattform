using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W4 (Weisung P7-9): Seite "Spiel" mit der begehbaren 3D-Stadt im Vollbild (W1 war die
/// Stichprobe). Die 3D-Flaeche fuellt die ganze Seite; Kopfzeile (Tag, Spielzeit, Bilder/s),
/// "Spiel verlassen", Ortsliste, Hinweiszeile mit Knoepfen und die Namensschilder der Orte liegen
/// als Ebenen darueber. Keine ScrollView umschliesst die Flaeche (sonst nimmt sie am Handy die
/// Gesten, Fehler aus der S24-Probe W1); nur die Ortsliste selbst scrollt. Ohne OpenGL nach
/// <see cref="GlWarten"/>: Hinweis und Ortsliste statt der Flaeche (Plan D3). Die Inhalte laden im
/// Hintergrund (Seite oeffnet sofort). Bis W6 ist der Spielstand nur im Arbeitsspeicher (eigene
/// Probe-Spielfigur), nichts wird gespeichert.
/// </summary>
public sealed class SpielSeite : Grid
{
    /// <summary>So lange wartet die Seite auf OpenGL, dann gilt "ohne Grafik".</summary>
    public static readonly TimeSpan GlWarten = TimeSpan.FromSeconds(4);

    private const double Rand = 12;
    private const double OrtslisteBreite = 300;
    private const double UntenBreite = 720;

    private readonly Action _verlassen;
    private readonly TextBlock _kopfzeile;
    private readonly TextBlock _probeZeile;
    private readonly TextBlock _zeile;
    private readonly ScrollViewer _zeileRolle;
    private readonly Border _unten;
    private readonly Grid _mitte;
    private readonly Button _betreten;
    private readonly Button _zurStadt;
    private readonly Button _warten;
    private readonly Button _orteKnopf;
    private readonly StackPanel _liste = new() { Spacing = 8 };
    private readonly Border _ortsliste;
    private readonly Border _ersatz;
    private readonly Border _flaecheRahmen;
    private readonly Canvas _schilder = new() { ClipToBounds = true };
    private readonly Dictionary<string, Button> _schildKnoepfe = [];
    private readonly DispatcherTimer _takt;
    private readonly long _gebaut = Stopwatch.GetTimestamp();
    private long _angehaengt = Stopwatch.GetTimestamp();
    private readonly TaskCompletionSource<bool> _glEntschieden = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _ersatzErzwungen;
    private int _anhaengen;
    private TopLevel? _oben;

    public SpielSeite(Func<StadtSpiel> laden, Action verlassen)
    {
        _verlassen = verlassen;
        Flaeche = new StadtFlaeche();
        Focusable = true;
        _flaecheRahmen = new Border { Child = Flaeche, ClipToBounds = true };
        Children.Add(_flaecheRahmen);
        Children.Add(_schilder);

        _kopfzeile = Schrift.Text(SpielProbeTexte.GrafikStartet, "Normal", FontWeight.Bold);
        _probeZeile = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        var kopf = Schrift.Karte(new StackPanel { Spacing = 2, Children = { _kopfzeile, _probeZeile } });
        kopf.Padding = new Thickness(14, 8);
        kopf.HorizontalAlignment = HorizontalAlignment.Left;
        var verlassenKnopf = Schrift.Knopf(StadtTexte.Verlassen, true);
        verlassenKnopf.Margin = new Thickness(0);
        verlassenKnopf.VerticalAlignment = VerticalAlignment.Top;
        verlassenKnopf.Click += (_, _) => _verlassen();
        VerlassenKnopf = verlassenKnopf;

        _zeile = Schrift.Text("");
        _betreten = Schrift.Knopf(SpielProbeTexte.Betreten, true);
        _betreten.Click += (_, _) => Spiel?.Betreten();
        _zurStadt = Schrift.Knopf(SpielProbeTexte.ZurStadt, true);
        _zurStadt.Click += (_, _) => Spiel?.Verlassen();
        _warten = Schrift.Knopf(StadtTexte.Warten);
        _warten.Click += (_, _) => Spiel?.Warten();
        _orteKnopf = Schrift.Knopf(SpielProbeTexte.Orte);
        _orteKnopf.Click += (_, _) => OrtslisteZeigen(!_ortsliste!.IsVisible);
        var knoepfe = new WrapPanel { Children = { _betreten, _zurStadt, _warten, _orteKnopf } };
        // lange Zeilen (Hinweis zur Bedienung) rollen in der Karte, die Knoepfe bleiben immer zu sehen
        // (Handy quer: wenig Hoehe); die Rolle umfasst nur den Text, nie die 3D-Flaeche
        _zeileRolle = new ScrollViewer
        {
            Content = _zeile,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        var unten = Schrift.Karte(new StackPanel { Spacing = 8, Children = { _zeileRolle, knoepfe } });
        unten.Padding = new Thickness(14, 10, 14, 2);
        unten.MaxWidth = UntenBreite;
        unten.HorizontalAlignment = HorizontalAlignment.Left;
        _unten = unten;

        _ortsliste = Schrift.Karte(new ScrollViewer
        {
            Content = _liste,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });
        _ortsliste.Padding = new Thickness(14, 10);
        _ortsliste.Width = OrtslisteBreite;
        _ortsliste.HorizontalAlignment = HorizontalAlignment.Right;
        _ortsliste.Margin = new Thickness(Rand, Rand, 0, Rand);
        _ortsliste.IsVisible = false;
        _ersatz = Schrift.Karte(Schrift.Text(SpielProbeTexte.OhneGrafik));
        _ersatz.Margin = new Thickness(0, Rand, 0, Rand);
        _ersatz.VerticalAlignment = VerticalAlignment.Top;
        _ersatz.IsVisible = false;

        // Ebene ueber der Flaeche: ohne Hintergrund, leere Stellen lassen jeden Finger zur Flaeche durch
        var ebene = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(Rand),
        };
        ebene.Children.Add(kopf);
        Grid.SetColumn(verlassenKnopf, 1);
        ebene.Children.Add(verlassenKnopf);
        // Ersatz und Ortsliste ueber beide Spalten, der Ersatz-Hinweis ueber der Liste: sonst macht die
        // Ortsliste die rechte Spalte breit und drueckt am schmalen Handy die Kopfzeile zusammen; ist die
        // Seite schmaler als die Liste, wird die Liste schmaler
        var mitte = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        mitte.Children.Add(_ersatz);
        Grid.SetRow(_ortsliste, 1);
        mitte.Children.Add(_ortsliste);
        Grid.SetRow(mitte, 1);
        Grid.SetColumnSpan(mitte, 2);
        ebene.Children.Add(mitte);
        _mitte = mitte;
        ebene.SizeChanged += (_, _) => Anordnen();
        _ortsliste.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
            {
                Anordnen();
            }
        };
        Grid.SetRow(unten, 2);
        Grid.SetColumnSpan(unten, 2);
        ebene.Children.Add(unten);
        Children.Add(ebene);
        Ebene = ebene;

        Flaeche.BildGezeichnet += () =>
        {
            ErstesBildMs ??= Stopwatch.GetElapsedTime(_gebaut).TotalMilliseconds;
            OeffnenMs ??= Stopwatch.GetElapsedTime(_angehaengt).TotalMilliseconds;
            SchilderSetzen();
        };
        Flaeche.GlGestartet += () =>
        {
            if (Flaeche.GlBereit)
            {
                _glEntschieden.TrySetResult(true);
                if (_ersatz!.IsVisible && !_ersatzErzwungen)
                {
                    // OpenGL kam erst nach der Wartezeit (kalter Start): 3D zurueckholen, Ortsliste bleibt ueber "Orte"
                    _ersatz.IsVisible = false;
                    _ortsliste!.IsVisible = false;
                    _flaecheRahmen!.IsVisible = true;
                    Beschriften();
                }
            }
            else
            {
                ErsatzZeigen();
            }
        };
        _takt = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Beschriften());
        AttachedToVisualTree += (_, _) =>
        {
            // die Seite wird beim Seitenwechsel ab- und angehaengt; OpenGL richtet sich dann neu ein
            _angehaengt = Stopwatch.GetTimestamp();
            OeffnenMs = null;
            _takt.Start();
            Flaeche.AnfordernBild();
            _oben = TopLevel.GetTopLevel(this);
            if (_oben is not null)
            {
                _oben.BackRequested += Zurueck;
            }
            Flaeche.Focus();
            var anhaengen = ++_anhaengen;
            DispatcherTimer.RunOnce(() =>
            {
                // nur, wenn die Seite seitdem durchgehend zu sehen war und OpenGL nicht kam
                if (anhaengen == _anhaengen && IsAttachedToVisualTree() && !Flaeche.GlBereit)
                {
                    ErsatzZeigen();
                }
            }, GlWarten);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _anhaengen++;
            _takt.Stop();
            if (_oben is not null)
            {
                _oben.BackRequested -= Zurueck;
                _oben = null;
            }
        };
        Beschriften();
        Geladen = LadenAsync(laden);
    }

    private async Task<StadtSpiel> LadenAsync(Func<StadtSpiel> laden)
    {
        var spiel = await Task.Run(laden);
        SpielSetzen(spiel);
        return spiel;
    }

    /// <summary>Spiel einsetzen (nach dem Laden; der Start-Test setzt je Durchgang ein frisches ein).</summary>
    public void SpielSetzen(StadtSpiel spiel)
    {
        if (Spiel is { } alt)
        {
            alt.Geaendert -= Beschriften;
            alt.SzeneGeaendert -= ListeBauen;
        }
        Spiel = spiel;
        Flaeche.Spiel = spiel;
        spiel.Geaendert += Beschriften;
        spiel.SzeneGeaendert += ListeBauen;
        ListeBauen();
        Beschriften();
    }

    public StadtFlaeche Flaeche { get; }

    /// <summary>Das Spiel, sobald die Inhalte geladen sind (sonst null).</summary>
    public StadtSpiel? Spiel { get; private set; }

    /// <summary>Fertig, wenn Inhalte und Stadt geladen sind.</summary>
    public Task<StadtSpiel> Geladen { get; }

    public Button VerlassenKnopf { get; }

    /// <summary>Ebene ueber der 3D-Flaeche (Start-Test: laesst sie die Mitte frei?).</summary>
    public Grid Ebene { get; }

    /// <summary>Vom Bauen der Seite bis zum ersten gezeichneten 3D-Bild (null = noch nicht).</summary>
    public double? ErstesBildMs { get; private set; }

    /// <summary>Vom letzten Anhaengen (Seite gezeigt) bis zum ersten 3D-Bild danach (null = noch nicht).</summary>
    public double? OeffnenMs { get; private set; }

    /// <summary>true = OpenGL steht, false = Ersatz (Ortsliste) ohne 3D.</summary>
    public Task<bool> GlEntschieden => _glEntschieden.Task;

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    public bool OrtslisteSichtbar => _ortsliste.IsVisible;

    /// <summary>
    /// Ebene an die Groesse anpassen: Ortsliste hoechstens 300 breit; ist Platz fuer Liste und untere
    /// Karte nebeneinander (PC, Handy quer), reicht die Liste bis unten und die Karte wird schmaler,
    /// sonst bleibt die Liste ueber der Karte. Der Text der unteren Karte bekommt hoechstens ein
    /// Viertel der Hoehe, damit die Knoepfe am Handy quer nie aus dem Bild rutschen.
    /// </summary>
    private void Anordnen()
    {
        var groesse = Ebene.Bounds.Size;
        if (groesse.Width <= 0 || groesse.Height <= 0)
        {
            return;
        }
        _ortsliste.Width = Math.Max(0, Math.Min(OrtslisteBreite, groesse.Width - Rand));
        var nebeneinander = groesse.Width >= (2 * OrtslisteBreite) + (3 * Rand);
        Grid.SetRowSpan(_mitte, nebeneinander ? 2 : 1);
        _unten.MaxWidth = nebeneinander && _ortsliste.IsVisible
            ? Math.Min(UntenBreite, groesse.Width - _ortsliste.Width - (2 * Rand))
            : UntenBreite;
        _zeileRolle.MaxHeight = Math.Max(40, groesse.Height / 4);
    }

    public bool ErsatzSichtbar => _ersatz.IsVisible;

    /// <summary>Sichtbare Namensschilder ueber der Stadt (Start-Test, Fotos).</summary>
    public int SchilderSichtbar => _schildKnoepfe.Values.Count(k => k.IsVisible);

    public void OrtslisteZeigen(bool sichtbar)
    {
        _ortsliste.IsVisible = sichtbar || _ersatz.IsVisible;
    }

    /// <summary>Ohne OpenGL: Hinweis und Ortsliste statt der 3D-Flaeche (Plan D3).</summary>
    public void ErsatzZeigen()
    {
        _ersatz.IsVisible = true;
        _ortsliste.IsVisible = true;
        _flaecheRahmen.IsVisible = false;
        _schilder.IsVisible = false;
        _glEntschieden.TrySetResult(false);
        Beschriften();
    }

    /// <summary>Start-Test: Ersatzansicht vorfuehren (und wieder zurueck), ohne OpenGL abzuschalten.</summary>
    public void ErsatzVorfuehren(bool an)
    {
        _ersatzErzwungen = an;
        _ersatz.IsVisible = an || !Flaeche.GlBereit;
        _ortsliste.IsVisible = an || _ersatz.IsVisible;
        _flaecheRahmen.IsVisible = !_ersatz.IsVisible;
        _schilder.IsVisible = !_ersatz.IsVisible;
        Beschriften();
    }

    /// <summary>Esc und Zurueck-Taste: aus dem Buero zur Stadt, aus der Stadt "Spiel verlassen".</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && !e.Handled)
        {
            Zurueck(this, e);
        }
    }

    private void Zurueck(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Spiel is { ImInnenraum: true } spiel)
        {
            spiel.Verlassen();
        }
        else
        {
            _verlassen();
        }
    }

    private void ListeBauen()
    {
        if (Spiel is not { } spiel)
        {
            return;
        }
        _liste.Children.Clear();
        foreach (var ort in spiel.Szene.Orte)
        {
            var id = ort.Id;
            var knopf = Schrift.Knopf(SpielProbeTexte.Hingehen);
            knopf.Click += (_, _) =>
            {
                spiel.GeheZu(id);
                if (_ersatz.IsVisible)
                {
                    // ohne 3D sofort ankommen (die Wegzeit zaehlt trotzdem)
                    spiel.SofortAnkommen();
                }
                Flaeche.AnfordernBild();
            };
            _liste.Children.Add(new StackPanel { Spacing = 4, Children = { Schrift.Text(ort.Name, "Normal", FontWeight.SemiBold), knopf } });
        }
        foreach (var alt in _schildKnoepfe.Values)
        {
            _schilder.Children.Remove(alt);
        }
        _schildKnoepfe.Clear();
        foreach (var ort in spiel.Szene.Orte)
        {
            var id = ort.Id;
            var text = new TextBlock { Text = ort.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.NoWrap }.Groesse("Klein");
            text.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
            var schild = new Button
            {
                Content = text,
                Padding = new Thickness(10, 4),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                IsVisible = false,
            };
            schild.Farbe(Button.BackgroundProperty, "Pinsel.card").Farbe(Button.BorderBrushProperty, "Pinsel.border_hi");
            Avalonia.Automation.AutomationProperties.SetName(schild, ort.Name);
            schild.Click += (_, _) =>
            {
                spiel.GeheZu(id);
                Flaeche.AnfordernBild();
            };
            _schildKnoepfe[id] = schild;
            _schilder.Children.Add(schild);
        }
        SchilderSetzen();
    }

    /// <summary>Namensschilder ueber die Haeuser setzen (je Bild); nur ganz sichtbare Schilder zeigen.</summary>
    private void SchilderSetzen()
    {
        if (Spiel is not { } spiel || Flaeche.AktuelleKamera is not { } kamera)
        {
            return;
        }
        var breite = (float)Flaeche.Bounds.Width;
        var hoehe = (float)Flaeche.Bounds.Height;
        foreach (var ort in spiel.Szene.Orte)
        {
            if (!_schildKnoepfe.TryGetValue(ort.Id, out var schild))
            {
                continue;
            }
            if (spiel.ImInnenraum || !Flaeche.GlBereit || _ersatz.IsVisible || kamera.Bildpunkt(ort.Schild, breite, hoehe) is not { } p)
            {
                schild.IsVisible = false;
                continue;
            }
            if (!schild.IsMeasureValid)
            {
                schild.Measure(Size.Infinity);
            }
            var w = schild.DesiredSize.Width;
            var h = schild.DesiredSize.Height;
            var links = p.X - (w / 2);
            var oben = p.Y - h;
            schild.IsVisible = links >= 0 && oben >= 0 && links + w <= breite && oben + h <= hoehe;
            Canvas.SetLeft(schild, links);
            Canvas.SetTop(schild, oben);
        }
    }

    private void Beschriften()
    {
        var inv = CultureInfo.InvariantCulture;
        var teile = new List<string>();
        if (Flaeche.GlBereit && !_ersatzErzwungen)
        {
            teile.Add(string.Format(inv, SpielProbeTexte.Bilder, Flaeche.BilderJeSekunde));
            teile.Add(Flaeche.GlKurz);
        }
        else if (!_ersatz.IsVisible)
        {
            teile.Add(SpielProbeTexte.GrafikStartet);
        }
        _probeZeile.Text = string.Join(" · ", teile);
        _probeZeile.IsVisible = teile.Count > 0;
        if (Spiel is not { } spiel)
        {
            _betreten.IsVisible = _zurStadt.IsVisible = _warten.IsVisible = false;
            return;
        }
        _kopfzeile.Text = spiel.Kopfzeile;
        _zeile.Text = spiel.Zeile;
        _betreten.IsVisible = spiel.KannBetreten;
        _zurStadt.IsVisible = spiel.ImInnenraum;
        _warten.IsVisible = spiel.KannWarten;
        _orteKnopf.IsVisible = !spiel.ImInnenraum;
    }
}
