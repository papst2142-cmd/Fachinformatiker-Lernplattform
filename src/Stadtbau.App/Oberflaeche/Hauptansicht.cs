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
using Lernplattform.Spiel;
using Lernplattform.Views;
using Stadtbau.Core;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitte 3 bis 5): Hauptansicht des Werkzeugs. Kopfleiste mit Neu, Oeffnen,
/// Speichern, Rueckgaengig, Wiederholen, Datei speichern, An CC senden und Hell/Dunkel; Reiter Draufsicht, Innenraeume
/// (Katalog links, Grundriss in der Mitte, Auswahl, Raum, Grenzen und Pruefungen rechts) und seit Welle 3 (Weisung P12-18)
/// 3D-Ansicht. Die Draufsicht bearbeitet die Stadt draussen (Katalog links, Karte, Auswahl und Pruefungen draussen rechts),
/// die 3D-Ansicht zeigt die Stadt wie das Spiel (Tag/Nacht, Jahreszeit, Fest, Probe laufen, Grenzen der Gesamtansicht).
/// Auf einem Handy nur der Hinweis <see cref="Texte.NurHandy"/>.
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
    private bool _drei;

    /// <summary>Weisung P12-21 (Teilbau): Vorrat des letzten 3D-Baus, Unveraendertes wird uebernommen.</summary>
    private readonly StadtSzene.Vorrat _vorrat3D = new();
    private int _pruefStand;
    private int _aussenPruefStand;
    private int _aussenStand;
    private int _dreiGebaut = -1;
    private Ansicht3D.Einstellung _einstellung = Ansicht3D.Einstellung.Standard;
    private Ansicht3D.Einstellung? _dreiEinstellung;
    private bool _dreiBaut;
    private long _dreiAenderung;

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
    private KatalogAnsicht? _katalogInnen;
    private KatalogAnsicht? _katalogAussen;
    private readonly Grid _innenRaster = new() { ColumnDefinitions = new ColumnDefinitions("260,*,340") };
    private readonly Grid _aussenRaster = new() { ColumnDefinitions = new ColumnDefinitions("260,*,340") };
    private Control? _kartenAnsicht;
    private Control? _dreiAnsicht;

    // Stadt draussen und 3D
    public StadtFlaeche Drei { get; } = new();
    private readonly StackPanel _aussenAuswahl = new() { Spacing = 6 };

    /// <summary>Texte der Karte "Ausgewaehltes Stueck" draussen (Start-Test, Weisung P12-25).</summary>
    public IEnumerable<string> AussenAuswahlTexte => _aussenAuswahl.Children.OfType<TextBlock>().Select(t => t.Text ?? "");
    private readonly StackPanel _aussenPruefungen = new() { Spacing = 6 };
    private readonly StackPanel _ortListe = new() { Spacing = 6 };
    private readonly TextBlock _aussenHinweis = Schrift.Text(Texte.DraufsichtHinweis, "Klein", farbe: "Pinsel.muted");
    private readonly CheckBox _aussenRasterAus = new() { Content = Schrift.Text(Texte.RasterAus, "Klein") };
    private readonly ComboBox _modellWahl = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _dreiStatus = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
    private readonly TextBlock _dreiGrenzText = Schrift.Text(Texte.GrenzenNochNicht, "Klein");
    private readonly ProgressBar _dreiGrenzBalken = new() { Minimum = 0, Maximum = 100, Height = 10, MinHeight = 10 };

    /// <summary>Letzte Pruefung draussen (fuer Start-Test und Anzeige).</summary>
    public List<StadtbauFund> AussenFunde { get; private set; } = [];

    /// <summary>Grenzen der Gesamtansicht aus der letzten Messung (null = noch nicht gemessen).</summary>
    public (long Dreiecke, int Aufrufe)? GrenzenDraussen { get; private set; }

    /// <summary>Letzte Pruefung draussen fertig (Start-Test wartet darauf).</summary>
    public Task AussenGeprueft => _aussenGeprueft.Task;

    private TaskCompletionSource _aussenGeprueft = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>3D-Ansicht mit dem aktuellen Stand gezeigt (Start-Test wartet darauf).</summary>
    public Task DreiFertig => _dreiFertig.Task;

    private TaskCompletionSource _dreiFertig = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Zeit von der Aenderung (oder dem Oeffnen) bis zum ersten Bild der neuen Szene in ms.</summary>
    public double DreiZeitMs { get; private set; }

    public bool DreiZeigt => _drei;

    private ComboBox? _lichtWahl, _jahrWahl, _festWahl;
    private bool _wahlSetzen;

    /// <summary>Die drei Auswahlfelder zeigen die Einstellung (auch wenn sie nicht ueber die Felder kam, z. B. Grenzen messen).</summary>
    private void WahlZeigen(Ansicht3D.Einstellung e)
    {
        _wahlSetzen = true;
        try
        {
            Waehle(_lichtWahl, e.Nacht);
            Waehle(_jahrWahl, e.Jahreszeit);
            Waehle(_festWahl, e.Fest ?? "");
        }
        finally
        {
            _wahlSetzen = false;
        }

        static void Waehle(ComboBox? c, object wert)
        {
            if (c?.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, wert)) is { } item)
            {
                c.SelectedItem = item;
            }
        }
    }

    /// <summary>Was die drei Auswahlfelder der 3D-Ansicht zeigen (Start-Test: gleich der Einstellung).</summary>
    public string DreiWahlText => string.Join(" / ", new[] { _lichtWahl, _jahrWahl, _festWahl }.Select(c => (c?.SelectedItem as ComboBoxItem)?.Content as string ?? "-"));

    /// <summary>Texte der Liste "Pruefungen draussen" (Start-Test: nach der Pruefung nie mehr "Pruefung laeuft").</summary>
    public string AussenPruefungText => string.Join(" | ", _aussenPruefungen.Children.Select(c => c switch
    {
        TextBlock t => t.Text,
        Button { Content: StackPanel sp } => string.Join(" ", sp.Children.OfType<TextBlock>().Select(t => t.Text)),
        _ => "",
    }));

    public bool InnenZeigt => _innen;

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
        Karte.AuswahlGeaendert += AussenAuswahlZeigen;
        Karte.Gemeldet += Melden;
        _aussenRasterAus.IsCheckedChanged += (_, _) => Karte.Raster = _aussenRasterAus.IsChecked != true;
        Drei.BildGezeichnet += DreiBildGezeichnet;
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
            _dreiAnsicht = DreiBauen();
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
        Knopf(Texte.Ansicht3D, DreiZeigen);
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
        _kopf[Texte.Innenraeume].IsEnabled = mit && _projekt!.Orte.Count > 0;
        _kopf[Texte.Ansicht3D].IsEnabled = mit;
        _kopf[Texte.Neu].IsEnabled = _daten is not null;
        _kopf[Texte.Oeffnen].IsEnabled = _daten is not null;
        _kopf[Texte.Speichern].IsEnabled = schreiben;
        _kopf[Texte.Rueckgaengig].IsEnabled = schreiben && _projekt!.SchritteZurueck > 0;
        _kopf[Texte.Wiederholen].IsEnabled = schreiben && _projekt!.SchritteVor > 0;
        _kopf[Texte.DateiSpeichern].IsEnabled = mit;
        _kopf[Texte.AnCcSenden].IsEnabled = mit;
        foreach (var (name, k) in _kopf)
        {
            var aktiv = mit && ((name == Texte.Innenraeume && _innen) || (name == Texte.Draufsicht && !_innen && !_drei) || (name == Texte.Ansicht3D && _drei));
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
            var k = Schrift.Knopf(Projekt.AnzeigeName(datei));
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
        _ort = ort ?? (projekt.Ort.Length > 0 ? projekt.Ort : null);
        _aussenStand++;
        _dreiGebaut = -1;
        GrenzenDraussen = null;
        _dreiGrenzText.Text = Texte.GrenzenNochNicht;
        _dreiGrenzBalken.Value = 0;
        Karte.Waehlen(null);
        OrtListeZeigen();
        _modellWahl.Items.Clear();
        foreach (var m in AussenBearbeitung.HausModelle(projekt.AussenStand))
        {
            _modellWahl.Items.Add(new ComboBoxItem { Content = m, Tag = m });
        }
        _modellWahl.SelectedIndex = _modellWahl.Items.Count > 0 ? 0 : -1;
        TitelSetzen();
        ReiterZeigen(innen && projekt.Orte.Count > 0);
        AussenPruefenStarten();
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
        _aussenStand++;
        _dreiAenderung = Stopwatch.GetTimestamp();
        Karte.InvalidateVisual();
        AussenAuswahlZeigen();
        if (_drei)
        {
            // Bei sichtbarer 3D-Ansicht zuerst die Szene bauen, die Pruefung draussen danach (sonst teilen sich beide den
            // Prozessor, und das Bild kommt spaeter; CI Windows Lauf 550: 590 bis 710 ms statt rund 430)
            _pruefungWartet = true;
            AussenPruefungLaeuft();
            DreiAktualisieren();
        }
        else
        {
            AussenPruefenStarten();
        }
        if (_projekt is not null && _projekt.Ort != _ort && _projekt.Ort.Length > 0 && _innen)
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
        innen = innen && _projekt.Orte.Count > 0;
        _innen = innen;
        _drei = false;
        if (innen)
        {
            _innenAnsicht ??= InnenBauen();
            _inhalt.Content = _innenAnsicht;
            OrtOeffnen(_ort is { } o && _projekt.Orte.Contains(o) ? o : _projekt.Orte[0]);
        }
        else
        {
            _kartenAnsicht ??= KarteBauen();
            _inhalt.Content = _kartenAnsicht;
            Karte.Zeigen(_projekt);
            AussenAuswahlZeigen();
            AussenPruefungZeigen();
        }
        KnoepfeFreigeben();
    }

    private Control KarteBauen()
    {
        // links: Stuecke fuer draussen
        Button KatalogKnopf(string text, AussenArt art, Func<string?>? modell = null)
        {
            var k = new Button { Content = Schrift.Text(text, "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }.Vorlese(text + " setzen");
            k.Click += (_, _) => AussenSetzenWaehlen(art, modell?.Invoke());
            return k;
        }
        var reihe = new WrapPanel
        {
            Children =
            {
                KatalogKnopf(Texte.Strasse, AussenArt.Strasse), KatalogKnopf(Texte.Baum, AussenArt.Baum), KatalogKnopf(Texte.Park, AussenArt.Park),
                KatalogKnopf(Texte.Beet, AussenArt.Beet), KatalogKnopf(Texte.Laterne, AussenArt.Laterne),
            },
        };
        var modellTitel = Schrift.Text(Texte.Modell, "Klein").Fuer(_modellWahl);
        var haus = KatalogKnopf(Texte.HausSetzen, AussenArt.Gebaeude, () => _modellWahl.SelectedItem is ComboBoxItem { Tag: string m } ? m : null);
        var links = new StackPanel
        {
            Spacing = 8,
            Children = { Schrift.Text(Texte.AussenKatalog, "Mittel", FontWeight.Bold), _aussenHinweis, reihe, VollerKatalogKnopf(false), Schrift.Text(Texte.HausOhneOrt, gewicht: FontWeight.Bold), modellTitel, _modellWahl, haus, _aussenRasterAus },
        };
        var linksRollen = new ScrollViewer { Content = Schrift.Karte(links), Margin = new Thickness(12, 12, 0, 12) };

        Karte.Vorlese(Texte.Draufsicht);
        Button Klein(string text, string name, Action a)
        {
            var k = Schrift.Knopf(text).Vorlese(name);
            k.Click += (_, _) => a();
            return k;
        }
        var zoom = new WrapPanel { Children = { Klein("+", Texte.Vergroessern, Karte.Vergroessern), Klein("−", Texte.Verkleinern, Karte.Verkleinern), Klein(Texte.Einpassen, Texte.Einpassen, Karte.Einpassen) } };
        var mitte = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(12) };
        mitte.Children.Add(new Border { Child = Karte, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1) }.Farbe(Border.BorderBrushProperty, "Pinsel.border"));
        Grid.SetRow(zoom, 1);
        zoom.Margin = new Thickness(0, 8, 0, 0);
        mitte.Children.Add(zoom);

        var rechts = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Auswahl, "Mittel", FontWeight.Bold), _aussenAuswahl } }),
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.PruefungenDraussen, "Mittel", FontWeight.Bold), _aussenPruefungen } }),
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Innenraeume, "Mittel", FontWeight.Bold), _ortListe } }),
            },
        };
        var rechtsRollen = new ScrollViewer { Content = new Border { Child = rechts, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) }, Margin = new Thickness(0, 12, 12, 12) };
        var g = _aussenRaster;
        g.Children.Add(linksRollen);
        Grid.SetColumn(mitte, 1);
        g.Children.Add(mitte);
        Grid.SetColumn(rechtsRollen, 2);
        g.Children.Add(rechtsRollen);
        return g;
    }

    // ---- voller Katalog (Phase 13 Welle 3b) -------------------------------------------------------

    private Button VollerKatalogKnopf(bool innen)
    {
        var k = Schrift.Knopf(Texte.VollerKatalogOeffnen, betont: true).Vorlese(Texte.VollerKatalog);
        k.HorizontalAlignment = HorizontalAlignment.Stretch;
        k.IsEnabled = Katalog.Aktiv is not null;
        k.Click += (_, _) => KatalogOeffnen(innen);
        return k;
    }

    /// <summary>Der Katalog der Seite (innen oder draussen), sobald er einmal geoeffnet wurde.</summary>
    public KatalogAnsicht? KatalogFuer(bool innen) => innen ? _katalogInnen : _katalogAussen;

    /// <summary>Den vollen Katalog ueber Katalog und Flaeche legen (rechts bleiben Auswahl und Pruefungen sichtbar).</summary>
    public KatalogAnsicht KatalogOeffnen(bool innen)
    {
        var k = innen ? _katalogInnen : _katalogAussen;
        if (k is null)
        {
            k = new KatalogAnsicht { Margin = new Thickness(12) };
            Grid.SetColumnSpan(k, 2);
            k.Geschlossen += () => KatalogSchliessen(innen);
            k.Gewaehlt += e =>
            {
                KatalogSchliessen(innen);
                if (innen)
                {
                    SetzenWaehlen(e.Typ);
                }
                else
                {
                    AussenSetzenWaehlen(AussenArt.Katalog, e.Id);
                }
            };
            (innen ? _innenRaster : _aussenRaster).Children.Add(k);
            if (innen)
            {
                _katalogInnen = k;
            }
            else
            {
                _katalogAussen = k;
            }
            if (Katalog.Aktiv is { Gruppen.Count: > 0 } kat)
            {
                k.GruppeZeigen(kat.Gruppen[0]);
            }
        }
        k.IsVisible = true;
        k.SucheFokus();
        Melden(Texte.VollerKatalog);
        return k;
    }

    private void KatalogSchliessen(bool innen)
    {
        if ((innen ? _katalogInnen : _katalogAussen) is { } k)
        {
            k.IsVisible = false;
        }
    }

    private void OrtListeZeigen()
    {
        _ortListe.Children.Clear();
        if (_projekt is null || _projekt.Orte.Count == 0)
        {
            _ortListe.Children.Add(Schrift.Text(Texte.KeineInnenraeume, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        foreach (var o in _projekt.Orte)
        {
            var k = Schrift.Knopf(_daten!.OrtName(o));
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            k.Click += (_, _) => OrtOeffnen(o);
            _ortListe.Children.Add(k);
        }
    }

    private void AussenSetzenWaehlen(AussenArt art, string? modell)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        Karte.SetzenArt = art;
        Karte.SetzenModell = modell;
        _aussenHinweis.Text = (art switch
        {
            AussenArt.Gebaeude => Texte.HausOhneOrt + " (" + modell + ")",
            AussenArt.Katalog => KatalogPruefung.Name(Katalog.Aktiv, Katalog.Praefix + modell),
            _ => KatalogName(art),
        }) + ": " + Texte.AussenSetzenAktiv;
        Melden(_aussenHinweis.Text);
    }

    private static string KatalogName(AussenArt art) => art switch
    {
        AussenArt.Strasse => Texte.Strasse,
        AussenArt.Baum => Texte.Baum,
        AussenArt.Park => Texte.Park,
        AussenArt.Beet => Texte.Beet,
        AussenArt.Laterne => Texte.Laterne,
        AussenArt.Katalog => Texte.VollerKatalog,
        _ => Texte.HausOhneOrt,
    };

    private void AussenAuswahlZeigen()
    {
        _aussenAuswahl.Children.Clear();
        _aussenHinweis.Text = Karte.SetzenArt is { } art ? KatalogName(art) + ": " + Texte.AussenSetzenAktiv : Texte.DraufsichtHinweis;
        if (_projekt is null || Karte.Auswahl is not { } s || !AussenBearbeitung.Gueltig(_projekt.AussenStand, s))
        {
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.NichtsGewaehltDraussen, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        var a = _projekt.AussenStand;
        _aussenAuswahl.Children.Add(Schrift.Text(AussenBearbeitung.Beschreibung(a, s, _daten!.OrtName), "Klein"));
        var (lx, ly) = AussenBearbeitung.Lage(a, s);
        var x = Feld(lx);
        var y = Feld(ly);
        var zeilen = new List<(string, Control)> { (Texte.X, x), (Texte.Y, y) };
        TextBox? gehweg = null, laenge = null, vor = null, seitlich = null, kw = null, kt = null;
        CheckBox? doppelt = null;
        if (s.Art == AussenArt.Katalog)
        {
            var (gw, gt) = AussenBearbeitung.KatalogGroesse(a, s);
            kw = Feld(gw);
            kt = Feld(gt);
            zeilen.Add((Texte.Breite, kw));
            zeilen.Add((Texte.Tiefe, kt));
        }
        if (s.Art == AussenArt.Strasse)
        {
            gehweg = Feld(AussenBearbeitung.Gehweg(a, s));
            laenge = Feld(AussenBearbeitung.Laenge(a, s));
            zeilen.Add((Texte.GehwegBreite, gehweg));
            zeilen.Add((Texte.Laenge, laenge));
        }
        if (s.Art == AussenArt.Paketplatz)
        {
            var (v, sw) = AussenBearbeitung.PaketWerte(a);
            vor = Feld(v);
            seitlich = Feld(sw);
            zeilen = [(Texte.PaketVor, vor), (Texte.PaketSeitlich, seitlich)];
        }
        _aussenAuswahl.Children.Add(Zeilen([.. zeilen]));
        if (s.Art == AussenArt.Laterne)
        {
            doppelt = new CheckBox { Content = Schrift.Text(Texte.Doppelt, "Klein"), IsChecked = SpielDaten.Liste(Aussen.Laternen(a)[s.Nummer]) is { Count: > 2 } l && l[2] is true };
            doppelt.IsCheckedChanged += (_, _) =>
            {
                var an = doppelt.IsChecked == true;
                _projekt.AussenAendern("Laterne", aa => AussenBearbeitung.LaterneDoppelt(aa, s, an));
                Melden(an ? Texte.LaterneDoppelt : Texte.Laterne);
            };
            _aussenAuswahl.Children.Add(doppelt);
        }
        var neu = Schrift.Knopf(Texte.Uebernehmen);
        neu.Click += (_, _) =>
        {
            if (s.Art == AussenArt.Paketplatz)
            {
                if (Zahl(vor!, out var nv) && Zahl(seitlich!, out var ns))
                {
                    _projekt.AussenAendern("Werte", aa => Py.Setzen(SpielDaten.Objekt(Aussen.Stadtdaten(aa)["darstellung"]), Lieferpaket.Abschnitt,
                        new PyObjekt { { "vor", Bearbeitung.Zahl(nv) }, { "seitlich", Bearbeitung.Zahl(ns) } }));
                    Melden("Werte übernommen");
                }
                else
                {
                    Melden("Bitte nur Zahlen eintragen (z. B. 2,5).");
                }
                return;
            }
            if (!Zahl(x, out var nx) || !Zahl(y, out var ny) || (gehweg is not null && !Zahl(gehweg, out _)) || (laenge is not null && !Zahl(laenge, out _))
                || (kw is not null && !Zahl(kw, out _)) || (kt is not null && !Zahl(kt, out _)))
            {
                Melden("Bitte nur Zahlen eintragen (z. B. 2,5).");
                return;
            }
            var ng = 0.0;
            var nl = 0.0;
            if (gehweg is not null && laenge is not null)
            {
                Zahl(gehweg, out ng);
                Zahl(laenge, out nl);
            }
            _projekt.AussenAendern("Werte", aa =>
            {
                if (s.Art == AussenArt.Strasse)
                {
                    AussenBearbeitung.GehwegSetzen(aa, s, ng);
                    if (Math.Abs(nl - AussenBearbeitung.Laenge(aa, s)) > 0.005)
                    {
                        AussenBearbeitung.LaengeSetzen(aa, s, nl);
                    }
                }
                if (kw is not null && kt is not null && Zahl(kw, out var nw) && Zahl(kt, out var nt))
                {
                    AussenBearbeitung.KatalogGroesseSetzen(aa, s, nw, nt);
                }
                AussenBearbeitung.Platzieren(aa, s, nx, ny);
            });
            Karte.Waehlen(AussenBearbeitung.Gueltig(_projekt.AussenStand, s) ? s : null);
            Melden("Werte übernommen");
        };
        var drehen = Schrift.Knopf(Texte.Drehen);
        drehen.Click += (_, _) => Karte.Drehen();
        drehen.IsEnabled = AussenBearbeitung.DrehenNicht(s) is null;
        var kopie = Schrift.Knopf(Texte.Kopie);
        kopie.Click += (_, _) => Karte.Kopieren();
        kopie.IsEnabled = s.Art is not (AussenArt.Haus or AussenArt.Paketplatz);
        var weg = Schrift.Gefahrknopf(Texte.Loeschen);
        weg.Click += (_, _) => Karte.Loeschen();
        weg.IsEnabled = AussenBearbeitung.LoeschenNicht(s) is null;
        _aussenAuswahl.Children.Add(new WrapPanel { Children = { neu, drehen, kopie, weg } });
        // warum etwas nicht geht, steht klein darunter
        foreach (var grund in new[] { AussenBearbeitung.DrehenNicht(s), AussenBearbeitung.LoeschenNicht(s) }.OfType<string>().Distinct())
        {
            _aussenAuswahl.Children.Add(Schrift.Text(grund, "Klein", farbe: "Pinsel.muted"));
        }
        if (s.Art == AussenArt.Haus && _projekt.Orte.Contains(s.Ort))
        {
            var oeffnen = Schrift.Knopf(Texte.Innenraeume + ": " + _daten.OrtName(s.Ort));
            oeffnen.Click += (_, _) => OrtOeffnen(s.Ort);
            _aussenAuswahl.Children.Add(oeffnen);
        }
        foreach (var k in _aussenAuswahl.GetLogicalDescendants().OfType<Control>().Where(c => c is TextBox or CheckBox || (c is Button b && b.Content is TextBlock t && t.Text != Texte.Innenraeume + ": " + _daten.OrtName(s.Ort))))
        {
            if (_projekt.NurLesen)
            {
                k.IsEnabled = false;
            }
        }
    }

    // ---- Pruefung draussen ------------------------------------------------------------------------

    /// <summary>Pruefung draussen wartet auf den Bau der 3D-Szene (nur bei sichtbarer 3D-Ansicht).</summary>
    private bool _pruefungWartet;

    private void AussenPruefungLaeuft()
    {
        if (_aussenGeprueft.Task.IsCompleted)
        {
            _aussenGeprueft = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _aussenPruefungen.Children.Clear();
        _aussenPruefungen.Children.Add(Schrift.Text(Texte.PruefungLaeuft, "Klein", farbe: "Pinsel.muted"));
    }

    private void AussenPruefenStarten()
    {
        if (_projekt is null || _daten is null)
        {
            return;
        }
        _pruefungWartet = false;
        var stand = ++_aussenPruefStand;
        var daten = _daten;
        // Kopie auf dem Oberflaechen-Thread: der Hintergrund liest nie den Stand, der gerade bearbeitet wird
        var fassung = Aussen.Fassung((PyObjekt)Py.Kopie(_projekt.AussenStand)!, _projekt.Basis, _projekt.Innenraeume(), 1, 0, Werkzeug.Kennzeile, "", "");
        AussenPruefungLaeuft();
        var uhr = Stopwatch.StartNew();
        _ = Task.Run(() => Projekt.PruefenAussen(fassung, daten)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (stand != _aussenPruefStand)
            {
                return;
            }
            if (t.IsFaulted)
            {
                _ordner.Fehler("Prüfung draußen: " + t.Exception?.InnerException);
                AussenFunde = [new StadtbauFund("Die Prüfung ist abgebrochen: " + t.Exception?.InnerException?.Message, "Bitte CC Bescheid geben.", true)];
            }
            else
            {
                AussenFunde = t.Result;
            }
            Messwerte.Reihe("Prüfen draußen im Hintergrund (Angabe)", uhr.Elapsed.TotalMilliseconds);
            AussenPruefungZeigen(fertig: true);
            _aussenGeprueft.TrySetResult();
            // danach die 3D-Szene im Voraus bauen: "3D-Ansicht" zeigt sie dann ohne Wartezeit
            DreiAktualisieren();
        }), TaskScheduler.Default);
    }

    /// <summary>Funde in Karte und Liste zeigen; die Liste nur mit fertigem Ergebnis (sonst bleibt "Pruefung laeuft").</summary>
    private void AussenPruefungZeigen(bool fertig = false)
    {
        Karte.Funde = AussenFunde;
        Karte.InvalidateVisual();
        if (_aussenPruefStand == 0 || (!fertig && !_aussenGeprueft.Task.IsCompleted))
        {
            return;
        }
        _aussenPruefungen.Children.Clear();
        if (AussenFunde.Count == 0)
        {
            _aussenPruefungen.Children.Add(Schrift.Text("✓ " + Texte.AllesGruen, farbe: "Pinsel.green"));
            return;
        }
        foreach (var f in AussenFunde.OrderByDescending(f => f.Rot))
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
            k.Click += (_, _) =>
            {
                if (_projekt is not null && f.X is { } fx && f.Y is { } fy)
                {
                    Karte.Waehlen(AussenBearbeitung.StueckBei(_projekt.AussenStand, fx, fy, 0.6));
                    Karte.Focus();
                }
            };
            _aussenPruefungen.Children.Add(k);
        }
    }

    // ---- 3D-Ansicht -------------------------------------------------------------------------------

    private Control DreiBauen()
    {
        ComboBox Wahl(IEnumerable<(string Name, object Wert)> werte, int start)
        {
            var c = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var (n, w) in werte)
            {
                c.Items.Add(new ComboBoxItem { Content = n, Tag = w });
            }
            c.SelectedIndex = start;
            return c;
        }
        var licht = _lichtWahl = Wahl([(Texte.Tag, false), (Texte.Nacht, true)], 0);
        var jahr = _jahrWahl = Wahl([("Frühling", Jahreszeit.Fruehling), ("Sommer", Jahreszeit.Sommer), ("Herbst", Jahreszeit.Herbst), ("Winter", Jahreszeit.Winter)], 1);
        var fest = _festWahl = Wahl([(Texte.KeinFest, ""), ("Ostern", Festzeit.Ostern), ("Halloween", Festzeit.Halloween), ("Weihnachten", Festzeit.Weihnachten), ("Silvester", Festzeit.Silvester)], 0);
        void Wechsel()
        {
            if (_wahlSetzen)
            {
                return;
            }
            if (licht.SelectedItem is ComboBoxItem { Tag: bool nacht } && jahr.SelectedItem is ComboBoxItem { Tag: Jahreszeit j } && fest.SelectedItem is ComboBoxItem { Tag: string f })
            {
                EinstellungSetzen(new Ansicht3D.Einstellung(nacht, j, f.Length > 0 ? f : null));
            }
        }
        licht.SelectionChanged += (_, _) => Wechsel();
        jahr.SelectionChanged += (_, _) => Wechsel();
        fest.SelectionChanged += (_, _) => Wechsel();
        var messen = Schrift.Knopf(Texte.GrenzenMessen);
        messen.Click += (_, _) => _ = GrenzenMessenAsync();
        _dreiGrenzBalken.Farbe(ProgressBar.ForegroundProperty, "Pinsel.green");
        var rechts = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Ansicht3D, "Mittel", FontWeight.Bold), Schrift.Text(Texte.Ansicht3DHinweis, "Klein", farbe: "Pinsel.muted"), _dreiStatus } }),
                Schrift.Karte(new StackPanel
                {
                    Spacing = 6,
                    Children = { Zeilen((Texte.Tag + " / " + Texte.Nacht, licht), (Texte.Jahreszeit, jahr), (Texte.Fest, fest)) },
                }),
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.ProbeLaufen, "Mittel", FontWeight.Bold), Schrift.Text(Texte.ProbeLaufenHinweis, "Klein", farbe: "Pinsel.muted") } }),
                Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.GrenzenGesamt, "Mittel", FontWeight.Bold), _dreiGrenzBalken, _dreiGrenzText, messen } }),
            },
        };
        var rechtsRollen = new ScrollViewer { Content = new Border { Child = rechts, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) }, Margin = new Thickness(0, 12, 12, 12) };
        var mitte = new Border { Child = Drei, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1), Margin = new Thickness(12) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,340") };
        g.Children.Add(mitte);
        Grid.SetColumn(rechtsRollen, 1);
        g.Children.Add(rechtsRollen);
        return g;
    }

    public void DreiZeigen()
    {
        if (_projekt is null)
        {
            return;
        }
        _drei = true;
        _innen = false;
        _dreiAnsicht ??= DreiBauen();
        _inhalt.Content = _dreiAnsicht;
        KnoepfeFreigeben();
        _dreiAenderung = Stopwatch.GetTimestamp();
        if (DreiAktuell && Drei.Spiel is not null && !_dreiBaut)
        {
            // im Voraus gebaut: nur noch zeigen
            if (_dreiFertig.Task.IsCompleted)
            {
                _dreiFertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            Tageslicht.FesteUhr = _einstellung.Uhr;
            _dreiWartetAufBild = true;
            DispatcherTimer.RunOnce(DreiOhneGrafikPruefen, TimeSpan.FromSeconds(1));
            Drei.AnfordernBild();
        }
        else
        {
            DreiAktualisieren();
        }
        Drei.Focus();
    }

    public void EinstellungSetzen(Ansicht3D.Einstellung einstellung)
    {
        _einstellung = einstellung;
        WahlZeigen(einstellung);
        _dreiAenderung = Stopwatch.GetTimestamp();
        if (_drei)
        {
            DreiAktualisieren();
        }
    }

    /// <summary>
    /// Die Szene neu bauen, wenn sich Stadt oder Einstellung seit dem letzten Bau geaendert haben (nur, solange die 3D-Ansicht
    /// zu sehen ist; im Hintergrund, ein Bau zur Zeit, danach der neueste Stand).
    /// </summary>
    private bool DreiAktuell => _dreiGebaut == _aussenStand && _dreiEinstellung == _einstellung;

    /// <summary>Kommt eine Sekunde nach dem Zeigen kein Bild und gibt es kein OpenGL: Hinweis zeigen und nicht weiter warten.</summary>
    private void DreiOhneGrafikPruefen()
    {
        if (_drei && _dreiWartetAufBild && !Drei.GlBereit)
        {
            _dreiWartetAufBild = false;
            _dreiStatus.Text = Texte.Ansicht3DOhneGrafik;
            _dreiFertig.TrySetResult();
        }
    }

    private void DreiAktualisieren()
    {
        if (_projekt is null || _daten is null || _dreiBaut)
        {
            return;
        }
        if (DreiAktuell)
        {
            Tageslicht.FesteUhr = _einstellung.Uhr;
            if (_drei)
            {
                Drei.AnfordernBild();
            }
            else if (_pruefungWartet)
            {
                AussenPruefenStarten();
            }
            return;
        }
        if (_dreiFertig.Task.IsCompleted)
        {
            _dreiFertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        var stand = _aussenStand;
        var einstellung = _einstellung;
        var fassung = Aussen.Fassung((PyObjekt)Py.Kopie(_projekt.AussenStand)!, _projekt.Basis, _projekt.Innenraeume(), 1, 0, Werkzeug.Kennzeile, "", "");
        var daten = _daten;
        _dreiBaut = true;
        _dreiStatus.Text = Texte.Ansicht3DBaut;
        var uhr = Stopwatch.StartNew();
        _ = Task.Run(() => Ansicht3D.Bauen(fassung, daten, einstellung, _vorrat3D)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _dreiBaut = false;
            Messwerte.Reihe("3D bauen im Hintergrund (Angabe)", uhr.Elapsed.TotalMilliseconds);
            if (t.IsFaulted)
            {
                _ordner.Fehler("3D-Ansicht: " + t.Exception?.InnerException);
                _dreiStatus.Text = Texte.Ansicht3DFehler;
                _dreiGebaut = stand;
                _dreiEinstellung = einstellung;
                _dreiFertig.TrySetResult();
                if (_pruefungWartet)
                {
                    AussenPruefenStarten();
                }
                return;
            }
            var alt = Drei.AktuelleKamera;
            var (ax, az, ab, ad) = alt is null ? (float.NaN, 0f, 0f, 0f) : (alt.ZielX, alt.ZielZ, alt.Abstand, alt.Drehung);
            Tageslicht.FesteUhr = einstellung.Uhr;
            Drei.Spiel = t.Result;
            if (!float.IsNaN(ax) && Drei.AktuelleKamera is { } neu)
            {
                // die Kamera bleibt, wo sie war (sonst springt sie nach jeder Aenderung auf die Figur)
                neu.Setzen(ax, az);
                neu.Zoomen(neu.Abstand / ab);
                neu.Drehung = ad;
            }
            _dreiGebaut = stand;
            _dreiEinstellung = einstellung;
            _dreiStatus.Text = "";
            if (_drei)
            {
                _dreiWartetAufBild = true;
                DispatcherTimer.RunOnce(DreiOhneGrafikPruefen, TimeSpan.FromSeconds(1));
            }
            if (!DreiAktuell)
            {
                // inzwischen geaendert: den neuesten Stand bauen (die Pruefung wartet weiter)
                DreiAktualisieren();
            }
            else if (_pruefungWartet)
            {
                AussenPruefenStarten();
            }
        }), TaskScheduler.Default);
    }

    private bool _dreiWartetAufBild;

    private void DreiBildGezeichnet()
    {
        if (!_dreiWartetAufBild)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (!_dreiWartetAufBild)
            {
                return;
            }
            _dreiWartetAufBild = false;
            DreiZeitMs = Stopwatch.GetElapsedTime(_dreiAenderung).TotalMilliseconds;
            Messwerte.Reihe("3D bis zum Bild (Angabe)", DreiZeitMs);
            if (_dreiGebaut == _aussenStand && _dreiEinstellung == _einstellung)
            {
                _dreiFertig.TrySetResult();
            }
        });
    }

    /// <summary>
    /// Grenzen wie die Gesamtansicht des Spiels (Start-Test GesamtansichtAsync): Nacht, Winter, Weihnachten, Kamera ganz
    /// weit weg ueber der Mitte, zwei Bilder, dann Dreiecke und Zeichenaufrufe des letzten Bilds. Danach wie vorher.
    /// </summary>
    public async Task GrenzenMessenAsync()
    {
        if (_projekt is null || !_drei)
        {
            return;
        }
        var vorher = _einstellung;
        EinstellungSetzen(Ansicht3D.Einstellung.Grenzen);
        await DreiFertig;
        if (!Drei.GlBereit || Drei.AktuelleKamera is not { } kamera || Drei.Spiel is not { } spiel)
        {
            _dreiGrenzText.Text = Texte.Ansicht3DOhneGrafik;
            EinstellungSetzen(vorher);
            return;
        }
        var (ax, az, ab) = (kamera.ZielX, kamera.ZielZ, kamera.Abstand);
        kamera.Zoomen(kamera.Abstand / Kamera.AbstandMax);
        kamera.Setzen(spiel.Szene.Breite / 2, spiel.Szene.Tiefe / 2);
        await Drei.NaechstesBildAsync();
        await Drei.NaechstesBildAsync();
        // Stuecke aus dem vollen Katalog zeichnet die 3D-Ansicht erst ab Welle 4; ihre echten Dreiecke zaehlen schon jetzt mit
        GrenzenDraussen = (Drei.LetzteDreiecke + KatalogPruefung.DreieckeAussen(Katalog.Aktiv, _projekt.AussenStand), Drei.LetzteZeichenaufrufe);
        kamera.Setzen(ax, az);
        kamera.Zoomen(kamera.Abstand / ab);
        var (dreiecke, aufrufe) = GrenzenDraussen.Value;
        var anteil = StadtbauGrenzen.Anteil(dreiecke, aufrufe);
        _dreiGrenzBalken.Value = Math.Min(100, anteil * 100);
        _dreiGrenzBalken.Farbe(ProgressBar.ForegroundProperty, anteil > 1 ? "Pinsel.red" : anteil >= StadtbauPruefung.GelbAb ? "Pinsel.yellow" : "Pinsel.green");
        _dreiGrenzText.Text = Texte.Grenzzeile(dreiecke, aufrufe, StadtbauGrenzen.Dreiecke, StadtbauGrenzen.Aufrufe);
        AutomationProperties.SetName(_dreiGrenzBalken, Texte.GrenzenGesamt + ": " + _dreiGrenzText.Text);
        Melden(Texte.GrenzenGesamt + ": " + _dreiGrenzText.Text);
        EinstellungSetzen(vorher);
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
        var links = new StackPanel { Spacing = 8, Children = { ortTitel, _ortWahl, Schrift.Text(Texte.Katalog, "Mittel", FontWeight.Bold), _katalogHinweis, VollerKatalogKnopf(true) } };
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

        var g = _innenRaster;
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
        if (!_projekt.Orte.Contains(ort))
        {
            return;
        }
        _ort = ort;
        _projekt.Ort = ort;
        if (!_innen)
        {
            _innen = true;
            _drei = false;
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
        _katalogHinweis.Text = Bearbeitung.TeilName(typ) + ": " + Texte.SetzenAktiv;
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
        _katalogHinweis.Text = Flaeche.SetzenTyp is { } typ ? Bearbeitung.TeilName(typ) + ": " + Texte.SetzenAktiv : Texte.KatalogHinweis;
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
            // Regeln des gemeinsamen Teils plus Katalog-Stuecke; Grenzen mit deren echten Dreiecken
            return KatalogPruefung.Innen(Katalog.Aktiv, ort, g, personen, daten.Grafik, daten.Geschaefte, daten.Fassung.Innenraeume);
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
            NachVerlauf();
            Melden("Rückgängig gemacht");
        }
    }

    public void Wiederholen()
    {
        if (_projekt?.Wiederholen() == true)
        {
            NachVerlauf();
            Melden("Wiederholt");
        }
    }

    /// <summary>Nach Rueckgaengig/Wiederholen: Auswahl weg; ein Schritt draussen zeigt die Draufsicht, wenn der Innenraum offen war.</summary>
    private void NachVerlauf()
    {
        Flaeche.Waehlen(null);
        Karte.Waehlen(null);
        if (_projekt!.LetzterSchrittDraussen && _innen)
        {
            ReiterZeigen(innen: false);
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
        if (_kartenAnsicht is not null && _aussenGeprueft.Task.IsCompleted)
        {
            AussenPruefungZeigen();
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
        var leer = new RadioButton { Content = Schrift.Text(Texte.NeuLeer), GroupName = "art" };
        var ort = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        foreach (var o in _daten.Orte)
        {
            ort.Items.Add(new ComboBoxItem { Content = _daten.OrtName(o), Tag = o });
        }
        ort.SelectedIndex = 0;
        var breite = new TextBox { Text = "32", MaxLength = 5, IsEnabled = false };
        var hoehe = new TextBox { Text = "24", MaxLength = 5, IsEnabled = false };
        // Sammelliste 219 Punkt 3: Vorschlag fuer den Namen (mit Umlauten), solange der Name nicht selbst getippt wurde.
        var vorschlag = "";
        void Vorschlagen()
        {
            var art = innen.IsChecked == true ? Texte.NeuInnenraum : leer.IsChecked == true ? Texte.NeuLeer : Texte.NeuStadt;
            var ortName = ort.SelectedItem is ComboBoxItem { Content: string on } ? on : null;
            var v = Texte.Vorschlag(art, ortName);
            if ((name.Text ?? "") == vorschlag)
            {
                name.Text = v;
            }
            vorschlag = v;
        }
        void Umschalten()
        {
            ort.IsEnabled = innen.IsChecked == true;
            breite.IsEnabled = hoehe.IsEnabled = leer.IsChecked == true;
            Vorschlagen();
        }
        innen.IsCheckedChanged += (_, _) => Umschalten();
        leer.IsCheckedChanged += (_, _) => Umschalten();
        stadt.IsCheckedChanged += (_, _) => Umschalten();
        ort.SelectionChanged += (_, _) => Vorschlagen();
        Vorschlagen();
        var fehler = Schrift.Text("", "Klein", farbe: "Pinsel.red");
        var inhalt = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(Texte.NeuName, gewicht: FontWeight.Bold).Fuer(name), name,
                Schrift.Text(Texte.NeuNameHinweis, "Klein", farbe: "Pinsel.muted"),
                Schrift.Text(Texte.NeuArt, gewicht: FontWeight.Bold), stadt, innen, leer,
                Schrift.Text(Texte.NeuOrt, "Klein").Fuer(ort), ort,
                Schrift.Text(Texte.LeerBreite, "Klein").Fuer(breite), breite,
                Schrift.Text(Texte.LeerHoehe, "Klein").Fuer(hoehe), hoehe,
                fehler,
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
            (double, double)? groesse = null;
            if (leer.IsChecked == true)
            {
                if (!Zahl(breite, out var bw) || !Zahl(hoehe, out var hw) || !Aussen.GroesseGueltig(bw, hw))
                {
                    fehler.Text = Texte.LeerGroesseUngueltig;
                    continue;
                }
                groesse = (bw, hw);
            }
            try
            {
                var gewaehlt = innen.IsChecked == true && ort.SelectedItem is ComboBoxItem { Tag: string o } ? o : null;
                NeuAnlegen(n, gewaehlt, groesse);
                return;
            }
            catch (IOException e)
            {
                fehler.Text = e.Message;
            }
        }
    }

    /// <summary>Neues Projekt anlegen und zeigen (Ort gesetzt: gleich im Innenraum, sonst Draufsicht).</summary>
    public void NeuAnlegen(string name, string? ort, (double Breite, double Hoehe)? leer = null)
    {
        var p = Projekt.Neu(name, _daten!, _ordner, ort, leer);
        ProjektSetzen(p, ort ?? p.Orte.FirstOrDefault(), innen: ort is not null);
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
            var k = Schrift.Knopf(Projekt.AnzeigeName(datei) + " (" + File.GetLastWriteTime(datei).ToString("dd.MM.yyyy HH:mm", De) + ")");
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
        else if (e.Source is not TextBox && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not TextBox)
        {
            if (_innen)
            {
                e.Handled = Flaeche.Taste(e.Key, e.KeyModifiers);
            }
            else if (!_drei)
            {
                e.Handled = Karte.Taste(e.Key, e.KeyModifiers);
            }
        }
    }
}
