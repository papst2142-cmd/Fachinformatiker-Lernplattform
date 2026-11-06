using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Lernplattform.Core.Design;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Views;
using Stadtbau.Core;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitte 3 bis 5): Hauptansicht des Werkzeugs. Kopfleiste mit Neu, Oeffnen,
/// Speichern, Rueckgaengig, Wiederholen, Datei speichern, An CC senden und Hell/Dunkel; Reiter Draufsicht (Stadt nur
/// ansehen, Ort antippen) und Innenraeume (Katalog links, Grundriss in der Mitte, Auswahl, Raum, Grenzen und Pruefungen
/// rechts). Auf einem Handy nur der Hinweis <see cref="Texte.NurHandy"/>.
/// </summary>
public sealed class Hauptansicht : UserControl
{
    public static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Kuerzere Bildschirmseite, unter der das Geraet als Handy gilt (Plan Abschnitt 2).</summary>
    public const double HandyUnter = 600;

    /// <summary>Start-Test: die Handy-Regel auch am PC anwenden (sonst nur unter Android).</summary>
    public static bool HandyRegelImmer { get; set; }

    private readonly Werkzeugordner _ordner;
    private Spieldaten? _daten;
    private Projekt? _projekt;
    private string? _ort;
    private bool _dunkel;
    private bool _innen;
    private int _pruefStand;

    // Aufbau
    private readonly Panel _ebene = new() { IsVisible = false };
    private readonly Grid _raster = new();
    private readonly TextBlock _titel = Schrift.Text("", "Gross", FontWeight.Bold);
    private readonly WrapPanel _knoepfe = new() { Orientation = Orientation.Horizontal };
    private readonly ContentControl _inhalt = new();
    private readonly TextBlock _status = Ansage.Lebend(Schrift.Text("", "Klein", farbe: "Pinsel.muted"));
    private readonly Border _handy;
    private readonly Dictionary<string, Button> _kopf = new(StringComparer.Ordinal);

    // Innenraeume
    public GrundrissFlaeche Flaeche { get; } = new();
    public KartenFlaeche Karte { get; } = new();
    private readonly ComboBox _ortWahl = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _auswahl = new() { Spacing = 6 };
    private readonly StackPanel _raum = new() { Spacing = 6 };
    private readonly StackPanel _pruefungen = new() { Spacing = 6 };
    private readonly TextBlock _grenzText = Schrift.Text("", "Klein");
    private readonly ProgressBar _grenzBalken = new() { Minimum = 0, Maximum = 100, Height = 10, MinHeight = 10 };
    private readonly TextBlock _katalogHinweis = Schrift.Text(Texte.KatalogHinweis, "Klein", farbe: "Pinsel.muted");
    private readonly CheckBox _rasterAus = new() { Content = Schrift.Text(Texte.RasterAus, "Klein") };
    private Control? _innenAnsicht;
    private Control? _kartenAnsicht;

    /// <summary>Letzte Pruefung des gezeigten Orts (fuer Start-Test und Anzeige).</summary>
    public List<StadtbauFund> Funde { get; private set; } = [];

    public (long Dreiecke, int Aufrufe) GrenzenStand { get; private set; }

    public Projekt? Projekt => _projekt;

    public string? Ort => _ort;

    public Spieldaten? Daten => _daten;

    /// <summary>Daten geladen (Start-Test wartet darauf).</summary>
    public Task Geladen => _geladen.Task;

    private readonly TaskCompletionSource _geladen = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Letzte Pruefung im Hintergrund fertig (Start-Test wartet darauf).</summary>
    public Task Geprueft => _geprueft.Task;

    private TaskCompletionSource _geprueft = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Hauptansicht(Werkzeugordner ordner)
    {
        _ordner = ordner;
        Dialog.Ebene(_ebene);
        _handy = HandyHinweis();
        _raster.RowDefinitions = new RowDefinitions("Auto,*,Auto");
        var kopf = new StackPanel { Spacing = 8, Children = { _titel, _knoepfe } };
        var kopfRahmen = new Border { Padding = new Thickness(16, 12, 16, 4), Child = kopf }.Farbe(Border.BackgroundProperty, "Pinsel.card");
        _raster.Children.Add(kopfRahmen);
        Grid.SetRow(_inhalt, 1);
        _raster.Children.Add(_inhalt);
        var statusRahmen = new Border { Padding = new Thickness(16, 4), Child = _status }.Farbe(Border.BackgroundProperty, "Pinsel.card");
        Grid.SetRow(statusRahmen, 2);
        _raster.Children.Add(statusRahmen);
        KopfBauen();
        Content = new Panel
        {
            Children = { new Border { Child = _raster }.Farbe(Border.BackgroundProperty, "Pinsel.bg"), _handy, _ebene, Ansage.Ziel() },
        };
        Flaeche.AuswahlGeaendert += AuswahlZeigen;
        Flaeche.Gemeldet += Melden;
        Karte.OrtGewaehlt += o => OrtOeffnen(o);
        _rasterAus.IsCheckedChanged += (_, _) => Flaeche.Raster = _rasterAus.IsChecked != true;
        _ortWahl.SelectionChanged += (_, _) =>
        {
            if (_ortWahl.SelectedItem is ComboBoxItem { Tag: string o } && o != _ort)
            {
                OrtOeffnen(o);
            }
        };
        _inhalt.Content = Schrift.Text("Spieldaten werden geladen …");
        TitelSetzen();
        KnoepfeFreigeben();
        _ = LadenAsync();
    }

    // ---- Laden und Aufbau -------------------------------------------------------------------------

    private async Task LadenAsync()
    {
        var uhr = Stopwatch.StartNew();
        try
        {
            _daten = await Task.Run(() => Spieldaten.Laden(Lesen));
            Messwerte.Reihe("Spieldaten laden (Angabe)", uhr.Elapsed.TotalMilliseconds);
            // Ansichten gleich bauen und die Pruefung einmal im Hintergrund vorwaermen: das erste Oeffnen eines
            // Innenraums bleibt so unter der Grenze von 400 ms (Start-Test, Messpunkt "Projekt anlegen")
            _innenAnsicht = InnenBauen();
            _kartenAnsicht = KarteBauen();
            var daten = _daten;
            await Task.Run(() => Vorwaermen(daten));
            await AnsichtVorwaermenAsync(_kartenAnsicht);
            await AnsichtVorwaermenAsync(_innenAnsicht);
            StartZeigen();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or StadtbauFehler or FormatException or InvalidOperationException)
        {
            _ordner.Fehler("Laden der Spieldaten: " + e.GetType().Name + ": " + e.Message);
            _inhalt.Content = Schrift.Text("Die Spieldaten konnten nicht geladen werden: " + e.Message);
        }
        _geladen.TrySetResult();
        await Aktualisierung.NachLadenAsync(_ordner.Fehler);
    }

    /// <summary>
    /// Ansicht einmal unsichtbar zeigen (Vorlagen der Steuerelemente werden angewandt und bleiben): das erste echte
    /// Zeigen nach "Neu" oder "Oeffnen" ist dann schneller.
    /// </summary>
    private async Task AnsichtVorwaermenAsync(Control ansicht)
    {
        ansicht.Opacity = 0;
        ansicht.IsHitTestVisible = false;
        _inhalt.Content = ansicht;
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            for (var i = 0; i < 2; i++)
            {
                var fertig = new TaskCompletionSource();
                top.RequestAnimationFrame(_ => fertig.TrySetResult());
                await fertig.Task;
            }
        }
        _inhalt.Content = null;
        ansicht.Opacity = 1;
        ansicht.IsHitTestVisible = true;
    }

    /// <summary>Pruefung und Zaehlung einmal rechnen (JIT, Grafik), ohne Ergebnis.</summary>
    private static void Vorwaermen(Spieldaten daten)
    {
        var ort = daten.Orte[0];
        var g = InnenGrundriss.Grundriss(daten.Fassung.Innenraeume, ort);
        var personen = InnenGrundriss.Personen(daten.Fassung.Innenraeume, ort);
        _ = StadtbauPruefung.Innen(ort, g, personen, daten.Grafik, daten.Geschaefte, daten.Fassung.Innenraeume);
        _ = Wege.Wandstuecke(g);
    }

    /// <summary>Eingebettete Datei der Lernplattform-Oberflaeche (Spielinhalte, Fassung, Grafik).</summary>
    public static byte[]? Lesen(string pfad)
    {
        using var s = typeof(Lernplattform.App).Assembly.GetManifestResourceStream(pfad);
        if (s is null)
        {
            return null;
        }
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }

    private void KopfBauen()
    {
        void Knopf(string text, Action aktion, bool betont = false)
        {
            var k = Schrift.Knopf(text, betont);
            k.Click += (_, _) => aktion();
            _kopf[text] = k;
            _knoepfe.Children.Add(k);
        }
        Knopf(Texte.Draufsicht, () => ReiterZeigen(innen: false));
        Knopf(Texte.Innenraeume, () => ReiterZeigen(innen: true));
        Knopf(Texte.Neu, () => _ = NeuAsync());
        Knopf(Texte.Oeffnen, () => _ = OeffnenAsync());
        Knopf(Texte.Speichern, SpeichernVonHand);
        Knopf(Texte.Rueckgaengig, Rueckgaengig);
        Knopf(Texte.Wiederholen, Wiederholen);
        Knopf(Texte.DateiSpeichern, () => _ = DateiSpeichernAsync());
        Knopf(Texte.AnCcSenden, () => _ = SendenAsync());
        Knopf(Texte.Dunkel, FarbeWechseln);
    }

    private void TitelSetzen() =>
        _titel.Text = Werkzeug.Kennzeile + (_projekt is null ? "" : " – " + _projekt.Name + (_projekt.NurLesen ? " (nur ansehen)" : ""));

    private void KnoepfeFreigeben()
    {
        var mit = _projekt is not null;
        var schreiben = mit && !_projekt!.NurLesen;
        _kopf[Texte.Draufsicht].IsEnabled = mit;
        _kopf[Texte.Innenraeume].IsEnabled = mit;
        _kopf[Texte.Neu].IsEnabled = _daten is not null;
        _kopf[Texte.Oeffnen].IsEnabled = _daten is not null;
        _kopf[Texte.Speichern].IsEnabled = schreiben;
        _kopf[Texte.Rueckgaengig].IsEnabled = schreiben && _projekt!.SchritteZurueck > 0;
        _kopf[Texte.Wiederholen].IsEnabled = schreiben && _projekt!.SchritteVor > 0;
        _kopf[Texte.DateiSpeichern].IsEnabled = mit;
        _kopf[Texte.AnCcSenden].IsEnabled = mit;
        foreach (var (name, k) in _kopf)
        {
            var aktiv = (name == Texte.Innenraeume && _innen && mit) || (name == Texte.Draufsicht && !_innen && mit);
            k.Farbe(Button.BackgroundProperty, aktiv ? "VerlaufW.primary" : "Pinsel.card_alt");
            if (k.Content is TextBlock t)
            {
                t.Farbe(TextBlock.ForegroundProperty, aktiv ? "Pinsel.on_accent" : "Pinsel.text");
            }
        }
    }

    private void StartZeigen()
    {
        var liste = new StackPanel { Spacing = 4 };
        var projekte = Projekt.Liste(_ordner);
        if (projekte.Count == 0)
        {
            liste.Children.Add(Schrift.Text(Texte.KeineProjekte, farbe: "Pinsel.muted"));
        }
        foreach (var datei in projekte.Take(8))
        {
            var k = Schrift.Knopf(Path.GetFileNameWithoutExtension(datei));
            k.HorizontalAlignment = HorizontalAlignment.Left;
            k.Click += (_, _) => ProjektLaden(datei);
            liste.Children.Add(k);
        }
        var neu = Schrift.Knopf(Texte.NeuTitel, betont: true);
        neu.Click += (_, _) => _ = NeuAsync();
        var oeffnen = Schrift.Knopf(Texte.OeffnenTitel);
        oeffnen.Click += (_, _) => _ = OeffnenAsync();
        var karte = Schrift.Karte(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Schrift.Text(Texte.Willkommen, "Titel", FontWeight.Bold),
                Schrift.Text(Texte.WillkommenText),
                new WrapPanel { Children = { neu, oeffnen } },
                Schrift.Text(Texte.LetzteProjekte, "Mittel", FontWeight.Bold),
                liste,
            },
        });
        karte.MaxWidth = 720;
        karte.Margin = new Thickness(16);
        karte.HorizontalAlignment = HorizontalAlignment.Center;
        _inhalt.Content = new ScrollViewer { Content = karte };
        KnoepfeFreigeben();
    }

    private Border HandyHinweis()
    {
        var karte = Schrift.Karte(new StackPanel
        {
            Spacing = 10,
            Children = { Schrift.Text(Texte.NurHandy, "Titel", FontWeight.Bold), Schrift.Text(Texte.NurHandyText) },
        });
        karte.Margin = new Thickness(16);
        karte.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Child = karte, IsVisible = false }.Farbe(Border.BackgroundProperty, "Pinsel.bg");
    }

    /// <summary>Ist die kuerzere Seite kleiner als <see cref="HandyUnter"/>, gilt das Geraet als Handy.</summary>
    public static bool IstHandy(Size groesse) => Math.Min(groesse.Width, groesse.Height) < HandyUnter;

    public bool HandyHinweisSichtbar => _handy.IsVisible;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var handy = (OperatingSystem.IsAndroid() || HandyRegelImmer) && IstHandy(e.NewSize);
        _handy.IsVisible = handy;
        _raster.IsVisible = !handy;
    }

    // ---- Projekt ----------------------------------------------------------------------------------

    public void ProjektSetzen(Projekt projekt, string? ort, bool innen)
    {
        if (_projekt is not null)
        {
            _projekt.Geaendert -= ProjektGeaendert;
        }
        _projekt = projekt;
        _projekt.Geaendert += ProjektGeaendert;
        _ort = ort ?? projekt.Ort;
        TitelSetzen();
        ReiterZeigen(innen);
        if (projekt.NurLesen)
        {
            _ = Dialog.Zeigen(Texte.OeffnenTitel, Texte.NurLesen, Texte.Ok);
        }
    }

    private void ProjektLaden(string datei)
    {
        if (_daten is null)
        {
            return;
        }
        try
        {
            var p = Projekt.Oeffnen(datei, _daten, _ordner);
            ProjektSetzen(p, p.Ort, innen: true);
            Melden("Projekt „" + p.Name + "“ geöffnet");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or StadtbauFehler or UnauthorizedAccessException)
        {
            _ordner.Fehler("Öffnen: " + e.GetType().Name + ": " + e.Message);
            _ = Dialog.Zeigen(Texte.OeffnenTitel, "Die Datei lässt sich nicht öffnen: " + e.Message, Texte.Ok);
        }
    }

    private void ProjektGeaendert()
    {
        if (_projekt is not null && _projekt.Ort != _ort && _projekt.Ort.Length > 0)
        {
            // Rueckgaengig in einem anderen Ort: dorthin wechseln
            OrtOeffnen(_projekt.Ort);
            return;
        }
        Flaeche.InvalidateVisual();
        AuswahlZeigen();
        RaumZeigen();
        KnoepfeFreigeben();
        PruefenStarten();
    }

    public void ReiterZeigen(bool innen)
    {
        if (_projekt is null)
        {
            return;
        }
        _innen = innen;
        if (innen)
        {
            _innenAnsicht ??= InnenBauen();
            _inhalt.Content = _innenAnsicht;
            OrtOeffnen(_ort ?? _daten!.Orte[0]);
        }
        else
        {
            _kartenAnsicht ??= KarteBauen();
            _inhalt.Content = _kartenAnsicht;
            Karte.Zeigen(_daten);
        }
        KnoepfeFreigeben();
    }

    private Control KarteBauen()
    {
        var liste = new StackPanel { Spacing = 6 };
        liste.Children.Add(Schrift.Text(Texte.DraufsichtHinweis, "Klein", farbe: "Pinsel.muted"));
        foreach (var o in _daten!.Orte)
        {
            var k = Schrift.Knopf(_daten.OrtName(o));
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            k.Click += (_, _) => OrtOeffnen(o);
            liste.Children.Add(k);
        }
        var rechts = new ScrollViewer { Content = Schrift.Karte(new StackPanel { Spacing = 8, Children = { Schrift.Text(Texte.Innenraeume, "Mittel", FontWeight.Bold), liste } }), Margin = new Thickness(0, 12, 12, 12) };
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,300") };
        Karte.Margin = new Thickness(12);
        g.Children.Add(Karte);
        Grid.SetColumn(rechts, 1);
        g.Children.Add(rechts);
        return g;
    }

    // ---- Innenraeume ------------------------------------------------------------------------------

    private Control InnenBauen()
    {
        _ortWahl.Items.Clear();
        foreach (var o in _daten!.Orte)
        {
            _ortWahl.Items.Add(new ComboBoxItem { Content = _daten.OrtName(o), Tag = o });
        }
        var ortTitel = Schrift.Text(Texte.Ort, "Mittel", FontWeight.Bold).Fuer(_ortWahl);
        var links = new StackPanel { Spacing = 8, Children = { ortTitel, _ortWahl, Schrift.Text(Texte.Katalog, "Mittel", FontWeight.Bold), _katalogHinweis } };
        foreach (var gruppe in InnenKatalog.Teile.GroupBy(t => t.Gruppe))
        {
            var reihe = new WrapPanel();
            foreach (var teil in gruppe.OrderBy(t => t.Name, StringComparer.Create(De, false)))
            {
                var k = new Button
                {
                    Content = Schrift.Text(teil.Name, "Klein"),
                    Margin = new Thickness(0, 0, 4, 4),
                    Padding = new Thickness(8, 4),
                }.Vorlese(teil.Name + " setzen");
                k.Click += (_, _) => SetzenWaehlen(teil.Typ);
                reihe.Children.Add(k);
            }
            var klappe = new Expander { Header = Schrift.Text(gruppe.Key, farbe: "Pinsel.text"), Content = reihe, IsExpanded = gruppe.Key == "Regale", HorizontalAlignment = HorizontalAlignment.Stretch }
                .Vorlese(Texte.Katalog + " " + gruppe.Key);
            links.Children.Add(klappe);
        }
        var linksRollen = new ScrollViewer { Content = Schrift.Karte(links), Margin = new Thickness(12, 12, 0, 12) };

        Flaeche.Vorlese(Texte.Grundriss);
        Button Klein(string text, string name, Action a)
        {
            var k = Schrift.Knopf(text).Vorlese(name);
            k.Click += (_, _) => a();
            return k;
        }
        var zoom = new WrapPanel
        {
            Children =
            {
                Klein("+", Texte.Vergroessern, Flaeche.Vergroessern),
                Klein("−", Texte.Verkleinern, Flaeche.Verkleinern),
                Klein(Texte.Einpassen, Texte.Einpassen, Flaeche.Einpassen),
                _rasterAus,
            },
        };
        _rasterAus.VerticalAlignment = VerticalAlignment.Center;
        var mitte = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(12) };
        var flaechenRahmen = new Border { Child = Flaeche, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        mitte.Children.Add(flaechenRahmen);
        Grid.SetRow(zoom, 1);
        zoom.Margin = new Thickness(0, 8, 0, 0);
        mitte.Children.Add(zoom);

        _grenzBalken.Farbe(ProgressBar.ForegroundProperty, "Pinsel.green");
        var rechts = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Auswahl, "Mittel", FontWeight.Bold), _auswahl } }),
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Grenzen, "Mittel", FontWeight.Bold), _grenzBalken, _grenzText } }),
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Pruefungen, "Mittel", FontWeight.Bold), _pruefungen } }),
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Raum, "Mittel", FontWeight.Bold), _raum } }),
            },
        };
        var rechtsRollen = new ScrollViewer { Content = new Border { Child = rechts, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) }, Margin = new Thickness(0, 12, 12, 12) };

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*,340") };
        g.Children.Add(linksRollen);
        Grid.SetColumn(mitte, 1);
        g.Children.Add(mitte);
        Grid.SetColumn(rechtsRollen, 2);
        g.Children.Add(rechtsRollen);
        return g;
    }

    public void OrtOeffnen(string ort)
    {
        if (_projekt is null || _daten is null)
        {
            return;
        }
        _ort = ort;
        _projekt.Ort = ort;
        if (!_innen)
        {
            _innen = true;
            _innenAnsicht ??= InnenBauen();
            _inhalt.Content = _innenAnsicht;
        }
        foreach (var item in _ortWahl.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag as string == ort)
            {
                _ortWahl.SelectedItem = item;
            }
        }
        Flaeche.Zeigen(_projekt, ort);
        AuswahlZeigen();
        RaumZeigen();
        KnoepfeFreigeben();
        PruefenStarten();
        Melden(_daten.OrtName(ort) + " geöffnet");
    }

    private void SetzenWaehlen(string typ)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        Flaeche.SetzenTyp = typ;
        _katalogHinweis.Text = InnenKatalog.Name(typ) + ": " + Texte.SetzenAktiv;
        Melden(_katalogHinweis.Text);
    }

    private TextBox Feld(double wert) => new() { Text = wert.ToString("0.##", De), HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 60 };

    private static Grid Zeilen(params (string Name, Control Feld)[] zeilen)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 4, ColumnSpacing = 8 };
        for (var i = 0; i < zeilen.Length; i++)
        {
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var t = Schrift.Text(zeilen[i].Name, "Klein").Fuer(zeilen[i].Feld);
            t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(t, i);
            g.Children.Add(t);
            Grid.SetRow(zeilen[i].Feld, i);
            Grid.SetColumn(zeilen[i].Feld, 1);
            g.Children.Add(zeilen[i].Feld);
        }
        return g;
    }

    private static bool Zahl(TextBox f, out double wert) =>
        double.TryParse((f.Text ?? "").Replace('.', ','), NumberStyles.Float, De, out wert) && double.IsFinite(wert);

    private void AuswahlZeigen()
    {
        _auswahl.Children.Clear();
        _katalogHinweis.Text = Flaeche.SetzenTyp is { } typ ? InnenKatalog.Name(typ) + ": " + Texte.SetzenAktiv : Texte.KatalogHinweis;
        if (_projekt is null || _ort is null || Flaeche.Auswahl is not { } a)
        {
            _auswahl.Children.Add(Schrift.Text(Texte.NichtsGewaehlt, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        PyObjekt d;
        try
        {
            d = Bearbeitung.Deko(_projekt.Grundriss(_ort), a);
        }
        catch (ArgumentOutOfRangeException)
        {
            Flaeche.Waehlen(null);
            return;
        }
        var ort = _ort;
        _auswahl.Children.Add(Schrift.Text(Bearbeitung.Beschreibung(d), "Klein"));
        var x = Feld(Py.F(d["x"]));
        var y = Feld(Py.F(d["y"]));
        var w = Feld(Py.F(d["w"]));
        var h = Feld(Py.F(d["h"]));
        _auswahl.Children.Add(Zeilen((Texte.X, x), (Texte.Y, y), (Texte.Breite, w), (Texte.Tiefe, h)));
        var neu = Schrift.Knopf(Texte.Uebernehmen);
        neu.Click += (_, _) =>
        {
            if (Zahl(x, out var nx) && Zahl(y, out var ny) && Zahl(w, out var nw) && Zahl(h, out var nh))
            {
                var stelle = _projekt.Aendern(ort, "Werte", g =>
                {
                    Bearbeitung.Groesse(g, a, nw, nh);
                    return Bearbeitung.Platzieren(g, a, nx, ny);
                });
                Flaeche.Waehlen(stelle);
                Melden("Werte übernommen");
            }
            else
            {
                Melden("Bitte nur Zahlen eintragen (z. B. 2,5).");
            }
        };
        var drehen = Schrift.Knopf(Texte.Drehen);
        drehen.Click += (_, _) => Flaeche.Drehen();
        var kopie = Schrift.Knopf(Texte.Kopie);
        kopie.Click += (_, _) => Flaeche.Kopieren();
        var weg = Schrift.Gefahrknopf(Texte.Loeschen);
        weg.Click += (_, _) => Flaeche.Loeschen();
        _auswahl.Children.Add(new WrapPanel { Children = { neu, drehen, kopie, weg } });
        var waren = _daten!.Waren(ort);
        if (waren.Count > 0)
        {
            var wahl = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            wahl.Items.Add(new ComboBoxItem { Content = Texte.KeineWare, Tag = "" });
            var jetzt = d["ware"] as string ?? "";
            foreach (var (kennung, name) in waren)
            {
                var item = new ComboBoxItem { Content = name, Tag = kennung };
                wahl.Items.Add(item);
            }
            wahl.SelectedItem = wahl.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag! == jetzt) ?? wahl.Items[0];
            wahl.SelectionChanged += (_, _) =>
            {
                if (wahl.SelectedItem is ComboBoxItem { Tag: string k } && k != (Bearbeitung.Deko(_projekt.Grundriss(ort), a)["ware"] as string ?? ""))
                {
                    _projekt.Aendern(ort, "Ware", g => Bearbeitung.Ware(g, a, k));
                    Melden("Ware zugeordnet");
                }
            };
            _auswahl.Children.Add(Zeilen((Texte.Ware, wahl)));
        }
        foreach (var k in _auswahl.GetLogicalDescendants().OfType<Control>().Where(c => c is Button or TextBox or ComboBox))
        {
            k.IsEnabled = !_projekt.NurLesen;
        }
    }

    private void RaumZeigen()
    {
        _raum.Children.Clear();
        if (_projekt is null || _ort is null)
        {
            return;
        }
        var ort = _ort;
        var g = _projekt.Grundriss(ort);
        var wahl = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        wahl.Items.Add(new ComboBoxItem { Content = SpielDaten.Get(SpielDaten.Objekt(g["flur"]), "name") as string ?? "Flur", Tag = "flur" });
        foreach (var r in SpielDaten.Liste(g["raeume"]).OfType<PyObjekt>())
        {
            wahl.Items.Add(new ComboBoxItem { Content = SpielDaten.Get(r, "name") as string ?? Py.Str(r["id"]), Tag = Py.Str(r["id"]) });
        }
        var felder = new StackPanel { Spacing = 6 };
        void Zeigen(string bereich)
        {
            felder.Children.Clear();
            var b = Bearbeitung.Bereich(g, bereich);
            var boden = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var (kennung, name) in Bearbeitung.Boeden)
            {
                boden.Items.Add(new ComboBoxItem { Content = name, Tag = kennung });
            }
            boden.SelectedItem = boden.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag! == (b["boden"] as string ?? ""));
            boden.SelectionChanged += (_, _) =>
            {
                if (boden.SelectedItem is ComboBoxItem { Tag: string k } && k != (Bearbeitung.Bereich(_projekt.Grundriss(ort), bereich)["boden"] as string))
                {
                    _projekt.Aendern(ort, "Boden", gg => Bearbeitung.Boden(gg, bereich, k));
                    Melden("Boden geändert");
                }
            };
            felder.Children.Add(Zeilen((Texte.Boden, boden)));
            if (bereich == "flur")
            {
                return;
            }
            var (rx, ry, rw, rh) = Bearbeitung.Flaeche(g, bereich);
            var x = Feld(rx);
            var y = Feld(ry);
            var w = Feld(rw);
            var h = Feld(rh);
            var t = SpielDaten.Get(b, "tuer") as PyObjekt;
            var von = Feld(t is null ? 0 : Py.F(t["von"]));
            var bis = Feld(t is null ? 0 : Py.F(t["bis"]));
            felder.Children.Add(Zeilen((Texte.X, x), (Texte.Y, y), (Texte.Breite, w), (Texte.Tiefe, h), (Texte.Tuer + " (von)", von), (Texte.Tuer + " (bis)", bis)));
            var los = Schrift.Knopf(Texte.RaumAendern);
            los.Click += (_, _) =>
            {
                if (Zahl(x, out var nx) && Zahl(y, out var ny) && Zahl(w, out var nw) && Zahl(h, out var nh) && Zahl(von, out var nv) && Zahl(bis, out var nb))
                {
                    _projekt.Aendern(ort, "Raum", gg =>
                    {
                        Bearbeitung.Raum(gg, bereich, nx, ny, nw, nh);
                        Bearbeitung.Tuer(gg, bereich, nv, nb);
                    });
                    Melden("Raum geändert");
                }
                else
                {
                    Melden("Bitte nur Zahlen eintragen (z. B. 2,5).");
                }
            };
            felder.Children.Add(los);
        }
        wahl.SelectionChanged += (_, _) =>
        {
            if (wahl.SelectedItem is ComboBoxItem { Tag: string b })
            {
                Zeigen(b);
            }
        };
        _raum.Children.Add(Zeilen((Texte.Raum, wahl)));
        _raum.Children.Add(felder);
        wahl.SelectedIndex = wahl.Items.Count > 1 ? 1 : 0;
        foreach (var k in _raum.GetLogicalDescendants().OfType<Control>().Where(c => c is Button or TextBox or ComboBox))
        {
            k.IsEnabled = !_projekt.NurLesen || k == wahl;
        }
    }

    // ---- Pruefung und Grenzen ---------------------------------------------------------------------

    private void PruefenStarten()
    {
        if (_projekt is null || _ort is null || _daten is null)
        {
            return;
        }
        var stand = ++_pruefStand;
        var ort = _ort;
        var g = (PyObjekt)Py.Kopie(_projekt.Grundriss(ort))!;
        var personen = _projekt.Personen(ort);
        var daten = _daten;
        if (_geprueft.Task.IsCompleted)
        {
            _geprueft = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        var uhr = Stopwatch.StartNew();
        _ = Task.Run(() =>
        {
            var funde = StadtbauPruefung.Innen(ort, g, personen, daten.Grafik, daten.Geschaefte, daten.Fassung.Innenraeume);
            var raum = Innenraum.AusGrundrissMitPersonen(StadtbauPruefung.Ansicht(ort), g, personen, daten.Grafik,
                Geschaefte.Laeden.Contains(ort) ? daten.Geschaefte.Daten : null);
            return (funde, StadtbauGrenzen.Innen(raum));
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (stand != _pruefStand || ort != _ort)
            {
                return;
            }
            if (t.IsFaulted)
            {
                _ordner.Fehler("Prüfung: " + t.Exception?.InnerException);
                Funde = [new StadtbauFund("Die Prüfung ist abgebrochen: " + t.Exception?.InnerException?.Message, "Bitte CC Bescheid geben.", true)];
                GrenzenStand = default;
            }
            else
            {
                (Funde, GrenzenStand) = t.Result;
            }
            Messwerte.Reihe("Prüfen im Hintergrund (Angabe)", uhr.Elapsed.TotalMilliseconds);
            PruefungZeigen();
            _geprueft.TrySetResult();
        }), TaskScheduler.Default);
    }

    private void PruefungZeigen()
    {
        Flaeche.Funde = Funde;
        Flaeche.InvalidateVisual();
        var (dreiecke, aufrufe) = GrenzenStand;
        var anteil = StadtbauGrenzen.Anteil(dreiecke, aufrufe);
        _grenzBalken.Value = Math.Min(100, anteil * 100);
        _grenzBalken.Farbe(ProgressBar.ForegroundProperty, anteil > 1 ? "Pinsel.red" : anteil >= StadtbauPruefung.GelbAb ? "Pinsel.yellow" : "Pinsel.green");
        _grenzText.Text = Texte.Grenzzeile(dreiecke, aufrufe, StadtbauGrenzen.Dreiecke, StadtbauGrenzen.Aufrufe);
        AutomationProperties.SetName(_grenzBalken, Texte.Grenzen + ": " + _grenzText.Text);
        _pruefungen.Children.Clear();
        if (Funde.Count == 0)
        {
            _pruefungen.Children.Add(Schrift.Text("✓ " + Texte.AllesGruen, farbe: "Pinsel.green"));
            return;
        }
        foreach (var f in Funde.OrderByDescending(f => f.Rot))
        {
            var zeile = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    Schrift.Text((f.Rot ? "Rot: " : "Gelb: ") + f.Text, "Klein", FontWeight.Bold, f.Rot ? "Pinsel.red" : "Pinsel.orange"),
                    Schrift.Text(f.Tipp, "Klein", farbe: "Pinsel.muted"),
                },
            };
            var k = new Button { Content = zeile, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 6) }
                .Vorlese((f.Rot ? "Rot: " : "Gelb: ") + f.Text + ". " + f.Tipp);
            k.Click += (_, _) => ZuFund(f);
            _pruefungen.Children.Add(k);
        }
    }

    /// <summary>Antippen eines Funds springt an seine Stelle (Stueck dort waehlen).</summary>
    private void ZuFund(StadtbauFund f)
    {
        if (_projekt is null || _ort is null || f.X is not { } x || f.Y is not { } y)
        {
            return;
        }
        var g = _projekt.Grundriss(_ort);
        Flaeche.Waehlen(Bearbeitung.StueckBei(g, x, y) ?? Bearbeitung.Alle(g)
            .OrderBy(s => Math.Abs(Py.F(s.Deko["x"]) + (Py.F(s.Deko["w"]) / 2) - x) + Math.Abs(Py.F(s.Deko["y"]) + (Py.F(s.Deko["h"]) / 2) - y))
            .Select(s => (Stelle?)s.Stelle).FirstOrDefault());
        Flaeche.Focus();
    }

    // ---- Kopfleiste -------------------------------------------------------------------------------

    private void Melden(string text)
    {
        _status.Text = text;
        Ansage.Sagen(text);
    }

    public void Rueckgaengig()
    {
        if (_projekt?.Rueckgaengig() == true)
        {
            Flaeche.Waehlen(null);
            Melden("Rückgängig gemacht");
        }
    }

    public void Wiederholen()
    {
        if (_projekt?.Wiederholen() == true)
        {
            Flaeche.Waehlen(null);
            Melden("Wiederholt");
        }
    }

    public void SpeichernVonHand()
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        try
        {
            _projekt.Speichern(vonHand: true);
            Melden(Texte.Gespeichert + " (mit Sicherung)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Speichern: " + e.Message);
            _ = Dialog.Zeigen(Texte.Speichern, "Speichern hat nicht geklappt: " + e.Message, Texte.Ok);
        }
    }

    public void FarbeWechseln()
    {
        _dunkel = !_dunkel;
        FarbenAnwenden.Anwenden(Farbwelt.Berechne(_dunkel ? Farbwelt.Dunkel : Farbwelt.Hell, Farbwelt.StandardGrundfarbe, Farbwelt.StandardHintergrund));
        if (_kopf[Texte.Dunkel].Content is TextBlock t)
        {
            t.Text = _dunkel ? Texte.Hell : Texte.Dunkel;
        }
        Flaeche.InvalidateVisual();
        Karte.InvalidateVisual();
        KnoepfeFreigeben();
        if (_innenAnsicht is not null)
        {
            PruefungZeigen();
        }
    }

    public bool Dunkel => _dunkel;

    private async Task NeuAsync()
    {
        if (_daten is null)
        {
            return;
        }
        var name = new TextBox { Text = "", MaxLength = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
        var stadt = new RadioButton { Content = Schrift.Text(Texte.NeuStadt), GroupName = "art", IsChecked = true };
        var innen = new RadioButton { Content = Schrift.Text(Texte.NeuInnenraum), GroupName = "art" };
        var leer = new RadioButton { Content = Schrift.Text(Texte.NeuLeer), GroupName = "art", IsEnabled = false };
        var ort = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        foreach (var o in _daten.Orte)
        {
            ort.Items.Add(new ComboBoxItem { Content = _daten.OrtName(o), Tag = o });
        }
        ort.SelectedIndex = 0;
        innen.IsCheckedChanged += (_, _) => ort.IsEnabled = innen.IsChecked == true;
        var fehler = Schrift.Text("", "Klein", farbe: "Pinsel.red");
        var inhalt = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(Texte.NeuName, gewicht: FontWeight.Bold).Fuer(name), name,
                Schrift.Text(Texte.NeuNameHinweis, "Klein", farbe: "Pinsel.muted"),
                Schrift.Text(Texte.NeuArt, gewicht: FontWeight.Bold), stadt, innen, leer,
                Schrift.Text(Texte.NeuOrt, "Klein").Fuer(ort), ort, fehler,
            },
        };
        while (true)
        {
            var wahl = await Dialog.Zeigen(Texte.NeuTitel, inhalt, Texte.Anlegen, Texte.Abbrechen);
            if (wahl != 0)
            {
                return;
            }
            var n = (name.Text ?? "").Trim();
            if (!Projekt.NameGueltig(n))
            {
                fehler.Text = Texte.NameUngueltig;
                continue;
            }
            try
            {
                var gewaehlt = innen.IsChecked == true && ort.SelectedItem is ComboBoxItem { Tag: string o } ? o : null;
                NeuAnlegen(n, gewaehlt);
                return;
            }
            catch (IOException e)
            {
                fehler.Text = e.Message;
            }
        }
    }

    /// <summary>Neues Projekt anlegen und zeigen (Ort gesetzt: gleich im Innenraum, sonst Draufsicht).</summary>
    public void NeuAnlegen(string name, string? ort)
    {
        var p = Projekt.Neu(name, _daten!, _ordner, ort);
        ProjektSetzen(p, ort ?? _daten!.Orte[0], innen: ort is not null);
        Melden("Projekt „" + name + "“ angelegt");
    }

    private async Task OeffnenAsync()
    {
        if (_daten is null)
        {
            return;
        }
        var liste = new StackPanel { Spacing = 4 };
        var gewaehlt = (string?)null;
        var projekte = Projekt.Liste(_ordner);
        if (projekte.Count == 0)
        {
            liste.Children.Add(Schrift.Text(Texte.KeineProjekte, farbe: "Pinsel.muted"));
        }
        foreach (var datei in projekte)
        {
            var k = Schrift.Knopf(Path.GetFileNameWithoutExtension(datei) + " (" + File.GetLastWriteTime(datei).ToString("dd.MM.yyyy HH:mm", De) + ")");
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            k.Click += (_, _) =>
            {
                gewaehlt = datei;
                Dialog.AlleSchliessen();
            };
            liste.Children.Add(k);
        }
        var wahl = await Dialog.Zeigen(Texte.OeffnenTitel, new ScrollViewer { Content = liste, MaxHeight = 420 }, Texte.OeffnenDatei, Texte.Abbrechen);
        if (gewaehlt is not null)
        {
            ProjektLaden(gewaehlt);
            return;
        }
        if (wahl == 0 && TopLevel.GetTopLevel(this)?.StorageProvider is { CanOpen: true } sp)
        {
            var dateien = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Texte.OeffnenTitel,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Stadtbau-Projekt") { Patterns = ["*" + Projekt.Endung] }],
            });
            if (dateien.Count == 1)
            {
                await using var lesen = await dateien[0].OpenReadAsync();
                using var m = new MemoryStream();
                await lesen.CopyToAsync(m);
                _ordner.Anlegen();
                var ziel = Path.Combine(_ordner.Projekte, Path.GetFileName(dateien[0].Name));
                for (var n = 2; File.Exists(ziel); n++)
                {
                    ziel = Path.Combine(_ordner.Projekte, Path.GetFileNameWithoutExtension(dateien[0].Name) + "_" + n.ToString(De) + Projekt.Endung);
                }
                Werkzeugordner.SicherSchreiben(ziel, m.ToArray());
                ProjektLaden(ziel);
            }
        }
    }

    private Task FundeZeigen(StadtbauFehler fehler)
    {
        var liste = new StackPanel { Spacing = 4 };
        liste.Children.Add(Schrift.Text("Bitte zuerst diese Punkte lösen (rot in den Prüfungen):"));
        foreach (var f in fehler.Funde)
        {
            liste.Children.Add(Schrift.Text("• " + f, "Klein"));
        }
        return Dialog.Zeigen(Texte.ExportGesperrt, new ScrollViewer { Content = liste, MaxHeight = 420 }, Texte.Ok);
    }

    /// <summary>"Datei speichern": Export pruefen, dann am PC Speicherort waehlen (sonst Ordner exporte des Werkzeugs).</summary>
    private async Task DateiSpeichernAsync()
    {
        if (_projekt is null)
        {
            return;
        }
        var zeit = DateTimeOffset.Now;
        byte[] daten;
        var uhr = Stopwatch.StartNew();
        try
        {
            daten = _projekt.Exportieren(zeit);
        }
        catch (StadtbauFehler f)
        {
            Messwerte.Reihe("Export prüfen (Angabe)", uhr.Elapsed.TotalMilliseconds);
            await FundeZeigen(f);
            return;
        }
        Messwerte.Reihe("Export prüfen (Angabe)", uhr.Elapsed.TotalMilliseconds);
        string ort;
        if (!Automatisch && TopLevel.GetTopLevel(this)?.StorageProvider is { CanSave: true } sp)
        {
            var datei = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Texte.DateiSpeichern,
                SuggestedFileName = _projekt.ExportName(zeit),
                DefaultExtension = StadtFassung.Endung.TrimStart('.'),
                FileTypeChoices = [new FilePickerFileType("Stadt-Fassung") { Patterns = ["*" + StadtFassung.Endung] }],
            });
            if (datei is null)
            {
                return;
            }
            await using (var s = await datei.OpenWriteAsync())
            {
                await s.WriteAsync(daten);
            }
            ort = datei.Name;
        }
        else
        {
            ort = _projekt.ExportSpeichern(zeit);
        }
        LetzterExport = ort;
        await Dialog.Zeigen(Texte.ExportOk, "Die Datei liegt hier: " + Umbruch(ort), Texte.Ok);
    }

    /// <summary>Start ohne Bedienung: keine Dateiauswahl des Systems.</summary>
    public static bool Automatisch { get; set; }

    public string? LetzterExport { get; private set; }

    /// <summary>Lange Pfade duerfen nach \ / - umbrechen.</summary>
    private static string Umbruch(string pfad) =>
        pfad.Replace("\\", "\\​", StringComparison.Ordinal).Replace("/", "/​", StringComparison.Ordinal).Replace("-", "-​", StringComparison.Ordinal);

    private async Task SendenAsync()
    {
        if (_projekt is null)
        {
            return;
        }
        var zeit = DateTimeOffset.Now;
        byte[] datei;
        try
        {
            datei = _projekt.Exportieren(zeit);
        }
        catch (StadtbauFehler f)
        {
            await FundeZeigen(f);
            return;
        }
        var nachricht = new TextBox { Text = _projekt.Nachricht, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 2000, MinHeight = 90, HorizontalAlignment = HorizontalAlignment.Stretch };
        var frage = Schrift.Text(Texte.SendenFrage).Fuer(nachricht);
        if (await Dialog.Zeigen(Texte.SendenTitel, new StackPanel { Spacing = 8, Children = { frage, nachricht } }, Texte.Senden, Texte.Abbrechen) != 0)
        {
            return;
        }
        if (!_projekt.NurLesen)
        {
            _projekt.Nachricht = nachricht.Text ?? "";
            _projekt.Speichern(vonHand: false);
            datei = _projekt.Exportieren(zeit);
        }
        var geraet = OperatingSystem.IsAndroid() ? "Tablet" : "PC";
        VersandErgebnis ergebnis;
        using (Dialog.Warten("Wird gesendet …"))
        {
            ergebnis = await Task.Run(() => Projekt.Senden(datei, zeit, geraet, null));
        }
        var text = ergebnis.Art switch
        {
            VersandArt.Gesendet => "Die Datei ist bei CC angekommen: " + Umbruch(ergebnis.Datei ?? ""),
            VersandArt.NichtVerfuegbar => Texte.SendenNichtVerfuegbar,
            _ => "Fehler: " + ergebnis.Art + (ergebnis.Code > 0 ? " (HTTP " + ergebnis.Code.ToString(De) + ")" : "") + ". Nimm „Datei speichern“ und gib die Datei an CC.",
        };
        if (ergebnis.Art is not (VersandArt.Gesendet or VersandArt.NichtVerfuegbar))
        {
            _ordner.Fehler("Senden: " + ergebnis.Art + " " + ergebnis.Code.ToString(De));
        }
        await Dialog.Zeigen(ergebnis.Art == VersandArt.Gesendet ? Texte.Gesendet : Texte.SendenFehler, text, Texte.Ok);
    }

    // ---- Tasten -----------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.AddHandler(KeyDownEvent, TasteGedrueckt, RoutingStrategies.Tunnel);
        }
    }

    private void TasteGedrueckt(object? sender, KeyEventArgs e)
    {
        if (Dialog.Offen is not null || _projekt is null)
        {
            return;
        }
        var strg = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (strg && e.Key == Key.Z)
        {
            Rueckgaengig();
            e.Handled = true;
        }
        else if (strg && e.Key == Key.Y)
        {
            Wiederholen();
            e.Handled = true;
        }
        else if (strg && e.Key == Key.S)
        {
            SpeichernVonHand();
            e.Handled = true;
        }
        else if (_innen && e.Source is not TextBox && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not TextBox)
        {
            e.Handled = Flaeche.Taste(e.Key, e.KeyModifiers);
        }
    }
}
