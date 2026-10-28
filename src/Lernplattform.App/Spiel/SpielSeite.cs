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
    private readonly Border _kopf;
    private readonly Grid _mitte;
    private readonly Button _betreten;
    private readonly Button _zurStadt;
    private readonly Button _warten;
    private readonly Button _orteKnopf;
    private readonly Button _einrichten;
    private readonly Button _uebersicht;
    private readonly WrapPanel _ortKnoepfe = new();
    private readonly StackPanel _einrichtenTafel = new() { Spacing = 6 };
    private string _tafelSchluessel = "";
    private readonly StackPanel _liste = new() { Spacing = 8 };
    private readonly Border _ortsliste;
    private readonly Border _ersatz;
    private readonly Border _flaecheRahmen;
    private readonly Canvas _schilder = new() { ClipToBounds = true };
    private readonly Dictionary<string, Button> _schildKnoepfe = [];
    private readonly Dictionary<Button, (double Schrift, Size Groesse)> _schildGroessen = [];
    private readonly DispatcherTimer _takt;
    private readonly long _gebaut = Stopwatch.GetTimestamp();
    private long _angehaengt = Stopwatch.GetTimestamp();
    private readonly TaskCompletionSource<bool> _glEntschieden = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _ersatzErzwungen;
    private int _anhaengen;
    private TopLevel? _oben;
    private bool _autoRandVorher = true;
    private Control? _randInhalt;
    private bool _randVorher;

    /// <summary>Stadt mit Spielstand und Spielansichten (W6): die Tafel liegt ueber der Stadt.</summary>
    public SpielSeite(Func<SpielSitzung> laden, Action verlassen, Tafel.SpielWege? wege) : this(verlassen)
    {
        Geladen = SitzungLadenAsync(laden, wege);
    }

    /// <summary>Nur die Stadt (Probe aus dem Arbeitsspeicher, ohne Spielansichten).</summary>
    public SpielSeite(Func<StadtSpiel> laden, Action verlassen) : this(verlassen)
    {
        Geladen = LadenAsync(laden);
    }

    private SpielSeite(Action verlassen)
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
        _kopf = kopf;
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
        _einrichten = Schrift.Knopf(SpielProbeTexte.Einrichten);
        _einrichten.Click += (_, _) => Spiel?.EinrichtenUmschalten();
        _uebersicht = Schrift.Knopf(StadtTexte.Uebersicht);
        _uebersicht.IsVisible = false;
        _uebersicht.Click += (_, _) => Tafel?.UebersichtOeffnen();
        var knoepfe = new WrapPanel { Children = { _ortKnoepfe, _betreten, _einrichten, _zurStadt, _warten, _uebersicht, _orteKnopf } };
        // lange Zeilen (Hinweis zur Bedienung) rollen in der Karte, die Knoepfe bleiben immer zu sehen
        // (Handy quer: wenig Hoehe); die Rolle umfasst nur den Text, nie die 3D-Flaeche
        _zeileRolle = new ScrollViewer
        {
            Content = new StackPanel { Spacing = 8, Children = { _zeile, _einrichtenTafel } },
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
        // Nachtrag 1 zu P7-13 (Wunsch 2): die Kamera haelt die Figur zwischen Kopfzeile und Textkarte
        ebene.LayoutUpdated += (_, _) => FreiSetzen();

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
        _takt = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) =>
        {
            var beginn = Stopwatch.GetTimestamp();
            Beschriften();
            ZeitenSchreiben(false);
            Flaeche.AuftragGemessen(Stopwatch.GetElapsedTime(beginn).TotalMilliseconds);
        });
        AttachedToVisualTree += (_, _) =>
        {
            // die Seite wird beim Seitenwechsel ab- und angehaengt; OpenGL richtet sich dann neu ein
            _angehaengt = Stopwatch.GetTimestamp();
            OeffnenMs = null;
            _takt.Start();
            // Phase 8 W8: Toene (Geraet oeffnen und Toene lesen im Hintergrund)
            TonDienst.SpielBetreten();
            Flaeche.AnfordernBild();
            _oben = TopLevel.GetTopLevel(this);
            if (_oben is not null)
            {
                _oben.BackRequested += Zurueck;
                _oben.LayoutUpdated += LayoutGezaehlt;
                // P7-10 Nachtrag 1: die 3D-Flaeche reicht bis an den Rand (auch neben die Kamera-Aussparung),
                // nur die Knoepfe und Karten bleiben im sicheren Bereich
                // Nachtrag 1 zu P7-13 (schwarzer Rand links, S24): Avalonia liest AutoSafeAreaPadding am Inhalt
                // des TopLevel (MainView, TopLevel.InvalidateChildInsetsPadding), nicht am TopLevel selbst;
                // vorher blieb der sichere Rand als Innenabstand der MainView stehen (schwarzer Streifen
                // neben der Kamera-Aussparung) und die Ebene zog ihn ein zweites Mal ab
                _randInhalt = _oben.Content as Control;
                if (_randInhalt is not null)
                {
                    _autoRandVorher = TopLevel.GetAutoSafeAreaPadding(_randInhalt);
                    TopLevel.SetAutoSafeAreaPadding(_randInhalt, false);
                }
                if (_oben.InsetsManager is { } rand)
                {
                    // Nachtrag 1 zu P7-12 (S24): ohne Rand-zu-Rand legt Android die Ansicht neben die
                    // Kamera-Aussparung (schwarzer Streifen links im Querformat), trotz ShortEdges
                    _randVorher = rand.DisplayEdgeToEdgePreference;
                    rand.DisplayEdgeToEdgePreference = true;
                    rand.SafeAreaChanged += SicherAendern;
                }
                SicherSetzen();
            }
            Flaeche.Focus();
            if (Tafel is { } tafel && _tafelAn)
            {
                tafel.Gezeigt();
            }
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
            // Weisung P7-17: beim Verlassen des Spiels alle Spielschritte fertig schreiben
            Core.Spiel.Spielschlange.AlleLeeren();
            // Phase 8 W8: Toene nur auf der Spielseite
            TonDienst.SpielVerlassen();
            _anhaengen++;
            _takt.Stop();
            ZeitenSchreiben(true);
            if (_oben is not null)
            {
                _oben.BackRequested -= Zurueck;
                _oben.LayoutUpdated -= LayoutGezaehlt;
                if (_oben.InsetsManager is { } rand)
                {
                    rand.SafeAreaChanged -= SicherAendern;
                    rand.DisplayEdgeToEdgePreference = _randVorher;
                }
                if (_randInhalt is not null)
                {
                    TopLevel.SetAutoSafeAreaPadding(_randInhalt, _autoRandVorher);
                    _randInhalt = null;
                }
                _oben = null;
            }
        };
        Beschriften();
    }

    private void SicherAendern(object? sender, Avalonia.Controls.Platform.SafeAreaChangedArgs e) => SicherSetzen();

    /// <summary>Rand der Ebene = fester Rand plus sicherer Bereich des Geraets (Kamera-Aussparung, Leisten).</summary>
    private void SicherSetzen()
    {
        var sicher = _oben?.InsetsManager?.SafeAreaPadding ?? default;
        Ebene.Margin = new Thickness(Rand + sicher.Left, Rand + sicher.Top, Rand + sicher.Right, Rand + sicher.Bottom);
    }

    private async Task<StadtSpiel> LadenAsync(Func<StadtSpiel> laden)
    {
        var spiel = await Task.Run(laden);
        SpielSetzen(spiel);
        return spiel;
    }

    private async Task<StadtSpiel> SitzungLadenAsync(Func<SpielSitzung> laden, Tafel.SpielWege? wege)
    {
        var sitzung = await Task.Run(laden);
        var tafel = new Tafel.SpielTafel(sitzung, _verlassen, wege);
        tafel.Geaendert += () =>
        {
            Beschriften();
            Flaeche.AnfordernBild();
        };
        // die Tafel deckt die ganze Seite ab: Ebene und Namensschilder darunter treten solange zurueck
        tafel.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
            {
                Ebene.IsVisible = !tafel.IsVisible;
                _schilder.IsVisible = !tafel.IsVisible && !_ersatz.IsVisible;
            }
        };
        Tafel = tafel;
        _sitzung = sitzung;
        _tafelAn = true;
        Children.Add(tafel);
        sitzung.SpielGewechselt += () =>
        {
            if (_tafelAn)
            {
                Einsetzen(sitzung.Spiel);
            }
        };
        Einsetzen(sitzung.Spiel);
        if (IsAttachedToVisualTree())
        {
            tafel.Gezeigt();
        }
        else
        {
            tafel.Zeichnen();
        }
        return sitzung.Spiel;
    }

    /// <summary>Die Spielansichten ueber der Stadt (null ohne Spielstand, nur Probe).</summary>
    public Tafel.SpielTafel? Tafel { get; private set; }

    private SpielSitzung? _sitzung;
    private bool _tafelAn;

    /// <summary>
    /// Spiel einsetzen (der Start-Test setzt je Durchgang ein frisches aus dem Arbeitsspeicher ein):
    /// die Spielansichten treten dann zurueck, bis <see cref="TafelZeigen"/> sie wieder holt.
    /// </summary>
    public void SpielSetzen(StadtSpiel spiel)
    {
        _tafelAn = false;
        if (Tafel is { } tafel)
        {
            tafel.IsVisible = false;
        }
        Einsetzen(spiel);
        _uebersicht.IsVisible = false;
    }

    /// <summary>
    /// Weisung P8-5 (L1): nach einem Abgleich mit neuen oder entfernten Eintraegen den Spielstand neu lesen
    /// (Stadt und Spielansichten); nicht, solange nur die Probe oder ein Spiel aus dem Start-Test zu sehen ist.
    /// </summary>
    public void NachAbgleich()
    {
        if (Tafel is { } tafel && _tafelAn)
        {
            tafel.NachAbgleich();
        }
    }

    /// <summary>Spielansichten wieder zeigen (zurueck zum Spielstand der Sitzung).</summary>
    public void TafelZeigen()
    {
        if (Tafel is not { } tafel || _sitzung is null)
        {
            return;
        }
        _tafelAn = true;
        Einsetzen(_sitzung.Spiel);
        tafel.Gezeigt();
    }

    private void Einsetzen(StadtSpiel spiel)
    {
        if (Tafel is { } tafel)
        {
            if (Spiel is { } vorher)
            {
                vorher.Aktion -= tafel.AktionAusfuehren;
            }
            if (_tafelAn)
            {
                spiel.Aktion += tafel.AktionAusfuehren;
            }
            _uebersicht.IsVisible = _tafelAn;
        }
        if (Spiel is { } alt)
        {
            alt.Geaendert -= Beschriften;
            alt.SzeneGeaendert -= ListeBauen;
            alt.Ton -= TonDienst.Anlass;
        }
        Spiel = spiel;
        Flaeche.Spiel = spiel;
        spiel.Ton += TonDienst.Anlass;
        spiel.Geaendert += Beschriften;
        spiel.SzeneGeaendert += ListeBauen;
        ListeBauen();
        Beschriften();
    }

    public StadtFlaeche Flaeche { get; }

    /// <summary>Das Spiel, sobald die Inhalte geladen sind (sonst null).</summary>
    public StadtSpiel? Spiel { get; private set; }

    /// <summary>Fertig, wenn Inhalte und Stadt geladen sind.</summary>
    public Task<StadtSpiel> Geladen { get; } = null!;

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
    private long _zeitenStand = -1;
    private long _zeitenUm;

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 4): Bildzeiten alle 10 s (und beim Verlassen) nach spiel_bilder.log im
    /// Datenordner schreiben, im Hintergrund; "Problem melden" zeigt die Datei mit.
    /// </summary>
    private void ZeitenSchreiben(bool sofort)
    {
        var zeiten = Flaeche.Zeiten;
        if (zeiten.Gesamt == _zeitenStand || (!sofort && Stopwatch.GetElapsedTime(_zeitenUm).TotalSeconds < BildZeiten.FensterSekunden))
        {
            return;
        }
        _zeitenStand = zeiten.Gesamt;
        _zeitenUm = Stopwatch.GetTimestamp();
        var text = zeiten.Text();
        string pfad;
        try
        {
            pfad = Path.Combine(Core.Daten.Ablage.Datenordner(), BildZeiten.DateiName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return;
        }
        _ = Task.Run(() =>
        {
            try
            {
                File.WriteAllText(pfad, text);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // nur Messung: ohne Datei geht es weiter
            }
        });
    }

    /// <summary>Verdeckte Hoehe oben (bis unter die Kopfzeile) und unten (ab der Textkarte) an die Flaeche geben.</summary>
    private void LayoutGezaehlt(object? sender, EventArgs e) => Flaeche.LayoutGezaehlt();

    /// <summary>Weisung P7-15: Layout der Spielseite messen (Zeit gehoert zur Luecke vor dem naechsten Bild).</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var beginn = Stopwatch.GetTimestamp();
        var groesse = base.MeasureOverride(availableSize);
        Flaeche.LayoutGemessen(Stopwatch.GetElapsedTime(beginn).TotalMilliseconds);
        return groesse;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var beginn = Stopwatch.GetTimestamp();
        var groesse = base.ArrangeOverride(finalSize);
        Flaeche.LayoutGemessen(Stopwatch.GetElapsedTime(beginn).TotalMilliseconds);
        return groesse;
    }

    private void FreiSetzen()
    {
        if (_kopf.TranslatePoint(new Point(0, _kopf.Bounds.Height), Flaeche) is { } oben
            && _unten.TranslatePoint(default, Flaeche) is { } unten && Flaeche.Bounds.Height > 1)
        {
            Flaeche.Frei = (Math.Clamp(oben.Y, 0, Flaeche.Bounds.Height), _unten.IsVisible ? Math.Clamp(Flaeche.Bounds.Height - unten.Y, 0, Flaeche.Bounds.Height) : 0);
        }
    }

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
        if (Tafel is { IsVisible: true } tafel && tafel.Zurueck())
        {
            return;
        }
        if (Spiel is { Einrichten: true } einrichten)
        {
            einrichten.EinrichtenUmschalten();
        }
        else if (Spiel is { ImInnenraum: true } spiel)
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
        _schildGroessen.Clear();
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
        var skalierung = Math.Max(1, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
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
            // Sammelliste 112: ein unsichtbares Schild misst 0 x 0 (Avalonia). Mit dieser Groesse lag es beim
            // ersten Zeigen um die halbe Breite zu weit rechts und galt als ganz sichtbar; ohne weiteres Bild
            // (Bildschleife ruht) blieb es so und ragte ueber den Rand. Darum gilt die zuletzt sichtbar gemessene
            // Groesse (je Schriftgroesse); fehlt sie, wird das Schild einmal sichtbar gemessen.
            var warSichtbar = schild.IsVisible;
            var schrift = ((TextBlock)schild.Content!).FontSize;
            if (warSichtbar || !_schildGroessen.TryGetValue(schild, out var gemerkt) || gemerkt.Schrift != schrift)
            {
                schild.IsVisible = true;
                if (!schild.IsMeasureValid)
                {
                    schild.Measure(Size.Infinity);
                }
                gemerkt = (schrift, schild.DesiredSize);
                _schildGroessen[schild] = gemerkt;
            }
            var w = gemerkt.Groesse.Width;
            var h = gemerkt.Groesse.Height;
            // auf ganze Bildpunkte gerundet und nur bei einer Aenderung gesetzt: jede neue Lage ordnet die
            // Ebene neu an (Wunsch 4, Nachtrag 1 zu P7-13)
            var links = Math.Round((p.X - (w / 2)) * skalierung) / skalierung;
            var oben = Math.Round((p.Y - h) * skalierung) / skalierung;
            // Nachtrag 1 zu P7-13 (Wunsch 2): nah an der Figur liegt das Schild eines Hauses oft ueber dem
            // Bild; steht das Haus (Mitte der Front am Boden) im freien Bereich, haengt das Schild oben an dessen Rand
            var frei = Flaeche.Frei.Oben;
            if (oben < frei && kamera.Bildpunkt(new System.Numerics.Vector3((ort.KastenMin.X + ort.KastenMax.X) / 2, 0, ort.KastenMax.Z), breite, hoehe) is { } front
                && front.Y >= frei + h && front.Y <= hoehe - Flaeche.Frei.Unten)
            {
                oben = Math.Round(frei * skalierung) / skalierung;
            }
            var sichtbar = links >= 0 && oben >= 0 && links + w <= breite && oben + h <= hoehe;
            if (schild.IsVisible != sichtbar)
            {
                schild.IsVisible = sichtbar;
            }
            if (!warSichtbar && !sichtbar)
            {
                continue;
            }
            if (Canvas.GetLeft(schild) != links)
            {
                Canvas.SetLeft(schild, links);
            }
            if (Canvas.GetTop(schild) != oben)
            {
                Canvas.SetTop(schild, oben);
            }
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
        _zurStadt.IsVisible = spiel.ImInnenraum && !spiel.Einrichten;
        _warten.IsVisible = spiel.KannWarten;
        _orteKnopf.IsVisible = !spiel.ImInnenraum;
        _einrichten.IsVisible = spiel.KannEinrichten;
        if (_einrichten.Content is TextBlock einrichtenText)
        {
            einrichtenText.Text = spiel.Einrichten ? SpielProbeTexte.Fertig : SpielProbeTexte.Einrichten;
        }
        else
        {
            _einrichten.Content = spiel.Einrichten ? SpielProbeTexte.Fertig : SpielProbeTexte.Einrichten;
        }
        TafelBauen(spiel);
    }

    /// <summary>Knoepfe des Ortes (place_message) und beim Einrichten Auswahl und Karton (wie Python _home_editor).</summary>
    private void TafelBauen(StadtSpiel spiel)
    {
        var karton = spiel.Einrichten ? spiel.ImKarton() : [];
        var schluessel = string.Join("|", spiel.Knoepfe.Select(k => k.Aktion + k.Name)) + "#" + spiel.Einrichten + "#" + spiel.Ausgewaehlt
            + "#" + spiel.AusgewaehltSteht + "#" + (spiel.Vorschau is null) + "#" + string.Join(",", karton.Select(k => k.Stueck));
        if (schluessel == _tafelSchluessel)
        {
            return;
        }
        _tafelSchluessel = schluessel;
        _ortKnoepfe.Children.Clear();
        foreach (var (aktion, name) in spiel.Knoepfe)
        {
            var knopf = Schrift.Knopf(name, true);
            knopf.Click += (_, _) => spiel.Knopf(aktion);
            _ortKnoepfe.Children.Add(knopf);
        }
        _ortKnoepfe.IsVisible = _ortKnoepfe.Children.Count > 0;
        _einrichtenTafel.Children.Clear();
        _einrichtenTafel.IsVisible = spiel.Einrichten;
        if (!spiel.Einrichten)
        {
            return;
        }
        if (spiel.AusgewaehltName is { } name2)
        {
            _einrichtenTafel.Children.Add(Schrift.Text(SpielProbeTexte.Ausgewaehlt, "Klein", FontWeight.Bold, "Pinsel.muted"));
            _einrichtenTafel.Children.Add(Schrift.Text(name2, "Normal", FontWeight.Bold));
            var reihe = new WrapPanel();
            var drehen = Schrift.Knopf(SpielProbeTexte.Drehen);
            drehen.Click += (_, _) => spiel.Drehen();
            reihe.Children.Add(drehen);
            if (spiel.Vorschau is not null)
            {
                var aufstellen = Schrift.Knopf(SpielProbeTexte.Aufstellen, true);
                aufstellen.Click += (_, _) => spiel.Aufstellen();
                reihe.Children.Add(aufstellen);
            }
            if (spiel.AusgewaehltSteht)
            {
                var karton2 = Schrift.Knopf(SpielProbeTexte.InDenKarton);
                karton2.Click += (_, _) => spiel.InDenKarton();
                reihe.Children.Add(karton2);
            }
            _einrichtenTafel.Children.Add(reihe);
        }
        _einrichtenTafel.Children.Add(Schrift.Text(SpielProbeTexte.ImKarton, "Klein", FontWeight.Bold, "Pinsel.muted"));
        if (karton.Count == 0)
        {
            _einrichtenTafel.Children.Add(Schrift.Text(SpielProbeTexte.AllesAusgepackt, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        foreach (var (stueck, name) in karton)
        {
            var aufstellen = Schrift.Knopf(SpielProbeTexte.Aufstellen);
            aufstellen.Click += (_, _) => spiel.Waehlen(stueck);
            var reihe = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var text = Schrift.Text(name, "Normal", FontWeight.SemiBold);
            text.VerticalAlignment = VerticalAlignment.Center;
            reihe.Children.Add(text);
            Grid.SetColumn(aufstellen, 1);
            reihe.Children.Add(aufstellen);
            _einrichtenTafel.Children.Add(reihe);
        }
    }
}
