using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W1: Seite "Spiel" als 3D-Stichprobe (Plan Abschnitt 4). Oben Kopfkarte mit
/// Probe-Zeile (Spielzeit, Bilder/s, OpenGL), Hinweiszeile und Knoepfen, darunter die
/// 3D-Flaeche; die Ortsliste ist immer erreichbar (Knopf "Orte") und ersetzt die 3D-Flaeche,
/// wenn OpenGL fehlt (Plan D3). Nichts wird gespeichert.
/// </summary>
public sealed class SpielSeite : Grid
{
    /// <summary>So lange wartet die Seite auf OpenGL, dann gilt "ohne Grafik".</summary>
    public static readonly TimeSpan GlWarten = TimeSpan.FromSeconds(4);

    /// <summary>Kleinste Hoehe der 3D-Flaeche in Punkten.</summary>
    public const double MinHoehe = 280;

    private readonly ProbeSpiel _spiel;
    private readonly TextBlock _probeZeile;
    private readonly TextBlock _zeile;
    private readonly Button _betreten;
    private readonly Button _zurStadt;
    private readonly Button _warten;
    private readonly Button _orteKnopf;
    private readonly Border _ortsliste;
    private readonly Border _ersatz;
    private readonly Border _flaecheRahmen;
    private readonly DispatcherTimer _takt;
    private readonly long _gebaut = Stopwatch.GetTimestamp();
    private long _angehaengt = Stopwatch.GetTimestamp();
    private readonly TaskCompletionSource<bool> _glEntschieden = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _ersatzErzwungen;
    private int _anhaengen;

    public SpielSeite(ProbeWelt welt)
    {
        _spiel = new ProbeSpiel(welt);
        Flaeche = new StadtFlaeche(_spiel);
        // Inhalt in einem ScrollViewer: die 3D-Flaeche fuellt den Rest der Seite, ist aber nie
        // niedriger als MinHoehe (Handy mit grosser Schrift: dann wird die Seite verschoben)
        var innen = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(16), RowSpacing = 12 };
        Children.Add(new ScrollViewer { Content = innen, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });

        _probeZeile = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        _zeile = Schrift.Text(_spiel.Zeile);
        _betreten = Schrift.Knopf(SpielProbeTexte.Betreten, true);
        _betreten.Click += (_, _) => _spiel.Betreten();
        _zurStadt = Schrift.Knopf(SpielProbeTexte.ZurStadt, true);
        _zurStadt.Click += (_, _) => _spiel.Verlassen();
        _warten = Schrift.Knopf(SpielProbeTexte.Warten);
        _warten.Click += (_, _) => _spiel.Warten(60);
        _orteKnopf = Schrift.Knopf(SpielProbeTexte.Orte);
        _orteKnopf.Click += (_, _) => OrtslisteZeigen(!_ortsliste!.IsVisible);
        var knoepfe = new WrapPanel { Children = { _betreten, _zurStadt, _warten, _orteKnopf } };
        var kopf = Schrift.KopfKarte(SpielProbeTexte.Titel, null, new StackPanel { Spacing = 8, Children = { _probeZeile, _zeile, knoepfe } });
        innen.Children.Add(kopf);

        var liste = new StackPanel { Spacing = 8 };
        foreach (var haus in welt.Haeuser)
        {
            var knopf = Schrift.Knopf(SpielProbeTexte.Hingehen);
            knopf.Click += (_, _) =>
            {
                _spiel.GeheZu(haus);
                Flaeche.AnfordernBild();
                if (_ersatz!.IsVisible)
                {
                    // ohne 3D: sofort ankommen (Wegzeit zaehlt trotzdem)
                    _spiel.SofortAnkommen();
                }
            };
            var name = Schrift.Text(haus.Name, "Normal", FontWeight.SemiBold);
            liste.Children.Add(new StackPanel { Spacing = 4, Children = { name, knopf } });
        }
        _ortsliste = Schrift.Karte(liste);
        _ortsliste.IsVisible = false;
        _ersatz = Schrift.Karte(Schrift.Text(SpielProbeTexte.OhneGrafik));
        _ersatz.IsVisible = false;
        _flaecheRahmen = new Border { CornerRadius = new CornerRadius(16), ClipToBounds = true, Child = Flaeche };
        var unten = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            RowSpacing = 12,
        };
        unten.Children.Add(_ersatz);
        Grid.SetRow(_ortsliste, 1);
        unten.Children.Add(_ortsliste);
        Grid.SetRow(_flaecheRahmen, 2);
        unten.Children.Add(_flaecheRahmen);
        Grid.SetRow(unten, 1);
        innen.Children.Add(unten);
        void Hoehe() => _flaecheRahmen.Height = Math.Max(MinHoehe, Bounds.Height - 2 * 16 - 12 - kopf.Bounds.Height);
        SizeChanged += (_, _) => Hoehe();
        kopf.SizeChanged += (_, _) => Hoehe();

        _spiel.Geaendert += Beschriften;
        Flaeche.BildGezeichnet += () =>
        {
            ErstesBildMs ??= Stopwatch.GetElapsedTime(_gebaut).TotalMilliseconds;
            OeffnenMs ??= Stopwatch.GetElapsedTime(_angehaengt).TotalMilliseconds;
        };
        Flaeche.GlGestartet += () =>
        {
            if (Flaeche.GlBereit)
            {
                _glEntschieden.TrySetResult(true);
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
        };
        Beschriften();
    }

    public StadtFlaeche Flaeche { get; }

    public ProbeSpiel Spiel => _spiel;

    /// <summary>Vom Bauen der Seite bis zum ersten gezeichneten 3D-Bild (null = noch nicht).</summary>
    public double? ErstesBildMs { get; private set; }

    /// <summary>Vom letzten Anhaengen (Seite gezeigt) bis zum ersten 3D-Bild danach (null = noch nicht).</summary>
    public double? OeffnenMs { get; private set; }

    /// <summary>true = OpenGL steht, false = Ersatz (Ortsliste) ohne 3D.</summary>
    public Task<bool> GlEntschieden => _glEntschieden.Task;

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    public bool OrtslisteSichtbar => _ortsliste.IsVisible;

    public bool ErsatzSichtbar => _ersatz.IsVisible;

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
        Beschriften();
    }

    private void Beschriften()
    {
        var inv = CultureInfo.InvariantCulture;
        var teile = new List<string> { string.Format(inv, SpielProbeTexte.Spielzeit, _spiel.Uhrzeit) };
        if (Flaeche.GlBereit && !_ersatzErzwungen)
        {
            teile.Add(string.Format(inv, SpielProbeTexte.Bilder, Flaeche.BilderJeSekunde));
            teile.Add(Flaeche.GlText);
        }
        else if (!_ersatz.IsVisible)
        {
            teile.Add(SpielProbeTexte.GrafikStartet);
        }
        _probeZeile.Text = string.Join(" · ", teile);
        _zeile.Text = _spiel.Zeile;
        _betreten.IsVisible = _spiel.KannBetreten;
        _zurStadt.IsVisible = _spiel.ImInnenraum;
        _warten.IsVisible = !_spiel.ImInnenraum;
    }
}
