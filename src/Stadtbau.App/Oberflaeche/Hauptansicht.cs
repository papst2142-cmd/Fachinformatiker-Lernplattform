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
public sealed partial class Hauptansicht : UserControl
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
    private readonly Button _beenden;

    // Innenraeume
    public GrundrissFlaeche Flaeche { get; } = new();
    public KartenFlaeche Karte { get; } = new();

    private DreiBedienung? _bedienung3D;

    /// <summary>S 0.7.0: Bauen in der 3D-Ansicht (Ebene ueber der Spiel-Flaeche).</summary>
    public DreiBedienung Bedienung3D => _bedienung3D ??= new DreiBedienung(Drei, Karte);

    private readonly ContentControl _aussenMitte = new();
    private Control? _karteMitte;
    private Control? _dreiMitte;
    private readonly StackPanel _aussenRechts = new() { Spacing = 12 };
    private readonly List<Control> _dreiKarten = [];
    private readonly CheckBox _figurLaufen = new() { Content = Schrift.Text(Texte.FigurLaufen, "Klein") };
    private readonly ComboBox _ortWahl = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _auswahl = new() { Spacing = 6 };
    private readonly StackPanel _raum = new() { Spacing = 6 };
    private readonly StackPanel _pruefungen = new() { Spacing = 6 };
    private readonly TextBlock _grenzText = Schrift.Text("", "Klein");
    private readonly ProgressBar _grenzBalken = new() { Minimum = 0, Maximum = 100, Height = 10, MinHeight = 10 };
    private readonly TextBlock _katalogHinweis = Schrift.Text(Texte.KatalogHinweis, "Klein", farbe: "Pinsel.muted");
    // Welle E: "Raster aus (feiner schieben)" ist jetzt der Schalter "Einrasten" (an = Raster 0,5 und Einrasten, Taste E)
    private readonly CheckBox _rasterAus = new() { Content = Schrift.Text(Texte.Einrasten, "Klein"), IsChecked = true };
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

    /// <summary>Beschriftungen der Knoepfe in der Karte "Auswahl" draussen (Start-Test).</summary>
    /// <summary>Knoepfe der Karte "Auswahl" draussen mit an/aus (Start-Test, Weisung P12-40 Punkt 4).</summary>
    public List<(string Text, bool An)> AussenAuswahlKnoepfeAn => [.. _aussenAuswahl.GetLogicalDescendants().OfType<Button>().Select(k => (k.Content is TextBlock t ? t.Text ?? "" : k.Content as string ?? "", k.IsEnabled))];

    /// <summary>Alle Knoepfe der Ansicht mit Beschriftung (Start-Test: "Ganze Stadt zeigen").</summary>
    public List<Button> KnoepfeMit(string text) => [.. this.GetLogicalDescendants().OfType<Button>().Where(k => k.Content is TextBlock t && t.Text == text)];

    public List<string> AussenAuswahlKnoepfe => [.. _aussenAuswahl.GetLogicalDescendants().OfType<Button>().Select(k => k.Content is TextBlock t ? t.Text ?? "" : k.Content as string ?? "")];
    private readonly StackPanel _aussenPruefungen = new() { Spacing = 6 };
    private readonly StackPanel _ortListe = new() { Spacing = 6 };
    private readonly StackPanel _blueprintListe = new() { Spacing = 10 };
    private readonly TextBlock _aussenHinweis = Schrift.Text(Texte.DraufsichtHinweis, "Klein", farbe: "Pinsel.muted");

    /// <summary>S 0.9.1 (Weisung P12-53 Teil A): Knopf „Setzen beenden“ links bei den Setzen-Knoepfen, nur beim Setzen sichtbar.</summary>
    private readonly Button _setzenBeendenLinks = Schrift.Knopf(Texte.SetzenBeenden).Vorlese(Texte.SetzenBeenden);

    /// <summary>Der Knopf links, mit dem das Setzen gewaehlt wurde (markiert, nochmal antippen waehlt ab); null = keiner.</summary>
    private Button? _setzenKnopf;

    /// <summary>Hat der markierte Knopf die Form von <see cref="Schrift.Knopf"/> (Flaeche "Pinsel.card_alt")?</summary>
    private bool _setzenKnopfRund;

    /// <summary>Gegenprobe im Start-Test und in Tests: Verhalten vor S 0.9.1 (zweites Antippen waehlt erneut, nichts wird abgewaehlt).</summary>
    public static bool AbwaehlenAltFuerTests { get; set; }

    /// <summary>Start-Test: der markierte Setzen-Knopf (null = keiner) und der Knopf „Setzen beenden“ links.</summary>
    public Button? SetzenKnopfMarkiert => _setzenKnopf;

    public Button SetzenBeendenKnopf => _setzenBeendenLinks;
    private readonly CheckBox _aussenRasterAus = new() { Content = Schrift.Text(Texte.Einrasten, "Klein"), IsChecked = true };

    /// <summary>Welle E: der Schalter „Einrasten“ draussen (fuer den Start-Test).</summary>
    internal CheckBox EinrastenSchalter => _aussenRasterAus;

    /// <summary>S 0.8.0 (Weisung P12-47, Frage 6 = a): Schalter „Eis im Winter“ (<c>stadtdaten.wasser.eis</c>).</summary>
    private readonly CheckBox _eisImWinter = new() { Content = Schrift.Text(Texte.EisImWinter, "Klein") };

    private bool _eisSetzen;

    /// <summary>Start-Test: der Schalter „Eis im Winter“.</summary>
    internal CheckBox EisSchalterFuerTests => _eisImWinter;
    private readonly ComboBox _modellWahl = new() { HorizontalAlignment = HorizontalAlignment.Stretch };

    /// <summary>Welle K1 (Weisung P12-55): Radius der naechsten Kurve 90° (1, 2 oder 3, Vorgabe 2).</summary>
    private readonly ComboBox _kurvenRadius = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        ItemsSource = new[] { "1", "2", "3" },
        SelectedIndex = 1,
    };

    /// <summary>Start-Test: Auswahl Radius der Kurve 90°.</summary>
    internal ComboBox KurvenRadiusWahl => _kurvenRadius;

    /// <summary>Start-Test: die Knoepfe Kurve 90°, Kurve 45°, S-Kurve.</summary>
    internal List<Button> KurvenKnoepfe { get; } = [];

    /// <summary>Welle Z: die Knoepfe „Straße ziehen“, „Fußweg ziehen“, „Fluss ziehen“, seit K2 „Bahn ziehen“ (Start-Test).</summary>
    internal List<Button> ZiehKnoepfe { get; } = [];

    /// <summary>
    /// Welle Z (Weisung P12-71): Baustein mit den Knoepfen (seit K2 vier) zum Ziehen von Punkt zu Punkt. Ein eigener kleiner Baustein,
    /// damit Welle Q ihn ohne Umbau an eine andere Stelle setzen kann.
    /// </summary>
    private WrapPanel ZiehenBaustein()
    {
        Button ZiehKnopf(string text, string tipp, ZiehArt art)
        {
            var k = new Button { Content = Schrift.Text(text, "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }.Vorlese(text).Mit(tipp);
            k.Click += (_, _) => SetzenKnopfGedrueckt(k, false, () => AussenSetzenWaehlen(AussenArt.Ziehen, AussenBearbeitung.ZiehKennung(art)));
            ZiehKnoepfe.Add(k);
            return k;
        }
        return new WrapPanel
        {
            Children =
            {
                ZiehKnopf(Texte.StrasseZiehen, Texte.StrasseZiehenTipp, ZiehArt.Strasse),
                ZiehKnopf(Texte.FusswegZiehen, Texte.FusswegZiehenTipp, ZiehArt.Fussweg),
                ZiehKnopf(Texte.FlussZiehen, Texte.FlussZiehenTipp, ZiehArt.Fluss),
                ZiehKnopf(Texte.BahnZiehen, Texte.BahnZiehenTipp, ZiehArt.Bahn),
            },
        };
    }

    /// <summary>Text des Hinweises links (Start-Test, Sammelliste 236).</summary>
    internal string AussenHinweisText => _aussenHinweis.Text ?? "";

    /// <summary>Gegenprobe Sammelliste 236: der Hinweis links zeigt den Grund einer roten Vorschau nicht (Verhalten bis S 0.14.0).</summary>
    internal static bool GrundErstNachTippFuerTests { get; set; }

    /// <summary>Welle K2: der Knopf „Bahn“ zum Setzen eines geraden Gleises (Start-Test).</summary>
    internal Button? BahnKnopf { get; private set; }

    /// <summary>Welle K3: die Knoepfe „Kreisverkehr klein/groß“ (Start-Test).</summary>
    internal List<Button> KreisKnoepfe { get; } = [];

    private double KurvenRadiusGewaehlt => _kurvenRadius.SelectedIndex switch { 0 => 1, 2 => 3, _ => 2 };
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
        // Patch S 0.5.1 (Weisung P12-35 Punkt 1): "Werkzeug beenden" ganz rechts in der Titelzeile der Kopfleiste
        var titelZeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _titel.VerticalAlignment = VerticalAlignment.Center;
        titelZeile.Children.Add(_titel);
        _beenden = Schrift.Knopf(Texte.WerkzeugBeenden).Mit(Texte.KopfTipps[Texte.WerkzeugBeenden]);
        _beenden.Click += (_, _) => BeendenKnopf();
        _beenden.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(_beenden, 1);
        titelZeile.Children.Add(_beenden);
        var kopf = new StackPanel { Spacing = 8, Children = { titelZeile, _knoepfe } };
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
        Karte.SetzenGeaendert += SetzenZustandZeigen;
        Karte.ZiehenGeaendert += () => _aussenHinweis.Text = SetzenHinweis();
        _setzenBeendenLinks.IsVisible = false;
        _setzenBeendenLinks.HorizontalAlignment = HorizontalAlignment.Stretch;
        _setzenBeendenLinks.Mit(Texte.SetzenBeendenTipp);
        _setzenBeendenLinks.Click += (_, _) => SetzenBeenden();
        _aussenRasterAus.Vorlese(Texte.Einrasten).Mit(Texte.EinrastenTipp);
        _aussenRasterAus.IsCheckedChanged += (_, _) => Karte.Raster = _aussenRasterAus.IsChecked == true;
        _eisImWinter.Vorlese(Texte.EisImWinter).Mit(Texte.EisTipp);
        _eisImWinter.IsCheckedChanged += (_, _) =>
        {
            if (_eisSetzen || _projekt is null || _projekt.NurLesen)
            {
                return;
            }
            var an = _eisImWinter.IsChecked == true;
            if (an != Aussen.Eis(_projekt.AussenStand))
            {
                _projekt.AussenAendern(Texte.EisImWinter, aa => AussenBearbeitung.EisSetzen(aa, an));
                Melden(Texte.EisImWinter + (an ? ": an" : ": aus"));
            }
        };
        Drei.BildGezeichnet += DreiBildGezeichnet;
        _rasterAus.Vorlese(Texte.Einrasten).Mit(Texte.EinrastenInnenTipp);
        _rasterAus.IsCheckedChanged += (_, _) => Flaeche.Raster = _rasterAus.IsChecked == true;
        _ortWahl.SelectionChanged += (_, _) =>
        {
            if (!_ortWahlFuellt && _ortWahl.SelectedItem is ComboBoxItem { Tag: string o } && o != (_ort is null ? null : Etagen.Teilen(_ort).Ort))
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
            // Tipp vor dem Klick-Handler (ein langer Druck am Tablet zeigt nur den Tipp, siehe Tipp.Mit)
            var k = Schrift.Knopf(text, betont).Mit(Texte.KopfTipps[text]);
            k.Click += (_, _) => aktion();
            _kopf[text] = k;
            _knoepfe.Children.Add(k);
        }
        Knopf(Texte.Draufsicht, () => ReiterZeigen(innen: false));
        Knopf(Texte.Innenraeume, () => ReiterZeigen(innen: true));
        Knopf(Texte.Ansicht3D, DreiZeigen);
        Knopf(Texte.GebaeudeEntwerfen, () => _ = EntwerfenAsync());
        Knopf(Texte.Neu, () => _ = NeuAsync());
        Knopf(Texte.Oeffnen, () => _ = OeffnenAsync());
        Knopf(Texte.Speichern, SpeichernVonHand);
        Knopf(Texte.Rueckgaengig, Rueckgaengig);
        Knopf(Texte.Wiederholen, Wiederholen);
        Knopf(Texte.DateiSpeichern, () => _ = DateiSpeichernAsync());
        Knopf(Texte.AnCcSenden, () => _ = SendenAsync());
        Knopf(Texte.Vollbild, VollbildUmschalten);
        Knopf(Texte.Dunkel, FarbeWechseln);
        WerkzeugVollbild.Geaendert += VollbildText;
        VollbildText();
    }

    /// <summary>Patch S 0.5.1 (Weisung P12-37 Punkt 2): Knopf "Vollbild" und F11; gemerkt in einstellungen.json.</summary>
    public void VollbildUmschalten()
    {
        var neu = !WerkzeugVollbild.An;
        try
        {
            WerkzeugEinstellungen.VollbildSetzen(_ordner, neu);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Vollbild merken: " + e.Message);
            Melden(Texte.VollbildFehler);
        }
        WerkzeugVollbild.Setzen(neu);
    }

    private void VollbildText()
    {
        if (_kopf.TryGetValue(Texte.Vollbild, out var k) && k.Content is TextBlock t)
        {
            t.Text = WerkzeugVollbild.An ? Texte.VollbildBeenden : Texte.Vollbild;
        }
    }

    private void TitelSetzen() =>
        _titel.Text = Werkzeug.Kennzeile + (_projekt is null ? "" : " – " + _projekt.Name + (_projekt.NurLesen ? " (nur ansehen)" : ""));

    private void KnoepfeFreigeben()
    {
        var mit = _projekt is not null;
        var schreiben = mit && !_projekt!.NurLesen;
        _kopf[Texte.Draufsicht].IsEnabled = mit;
        _kopf[Texte.Innenraeume].IsEnabled = mit && _projekt!.Innenorte.Count > 0;
        _kopf[Texte.Ansicht3D].IsEnabled = mit;
        _kopf[Texte.Neu].IsEnabled = _daten is not null;
        _kopf[Texte.Oeffnen].IsEnabled = _daten is not null;
        _kopf[Texte.Speichern].IsEnabled = schreiben;
        _kopf[Texte.Rueckgaengig].IsEnabled = schreiben && _projekt!.SchritteZurueck > 0;
        _kopf[Texte.Wiederholen].IsEnabled = schreiben && _projekt!.SchritteVor > 0;
        _kopf[Texte.DateiSpeichern].IsEnabled = mit;
        _kopf[Texte.AnCcSenden].IsEnabled = mit;
        _kopf[Texte.GebaeudeEntwerfen].IsEnabled = _daten is not null;
        if (_entwurf is { } entwurf)
        {
            // S 0.9.0: im Editor gelten nur Rueckgaengig/Wiederholen des Entwurfs; Projekt-Knoepfe erst nach "Zurück zur Stadt"
            foreach (var name in new[] { Texte.Draufsicht, Texte.Innenraeume, Texte.Ansicht3D, Texte.Neu, Texte.Oeffnen, Texte.Speichern, Texte.DateiSpeichern, Texte.AnCcSenden })
            {
                _kopf[name].IsEnabled = false;
            }
            _kopf[Texte.Rueckgaengig].IsEnabled = entwurf.Entwurf.KannZurueck;
            _kopf[Texte.Wiederholen].IsEnabled = entwurf.Entwurf.KannWiederholen;
        }
        foreach (var (name, k) in _kopf)
        {
            var aktiv = name == Texte.GebaeudeEntwerfen ? _entwurf is not null : _entwurf is null && mit && ((name == Texte.Innenraeume && _innen) || (name == Texte.Draufsicht && !_innen && !_drei) || (name == Texte.Ansicht3D && _drei));
            k.Farbe(Button.BackgroundProperty, aktiv ? "VerlaufW.primary" : "Pinsel.card_alt");
            if (k.Content is TextBlock t)
            {
                t.Farbe(TextBlock.ForegroundProperty, aktiv ? "Pinsel.on_accent" : "Pinsel.text");
            }
        }
    }

    /// <summary>Start-Test: das Startbild zeigen (Knopf "Nach Updates suchen").</summary>
    public void StartZeigenFuerTests() => StartZeigen();

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
        // Nachbesserung S 0.5.1 (Weisung P12-42 Punkt 1): nur wo es ein Update gibt (PC aus der Installation, Start-Test)
        var update = Schrift.Knopf(Texte.UpdateSuchen).Mit(Texte.UpdateSuchenTipp);
        update.Click += (_, _) => _ = Aktualisierung.VonHandAsync(_ordner.Fehler);
        update.IsVisible = Aktualisierung.Pruefen is not null;
        var karte = Schrift.Karte(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Schrift.Text(Texte.Willkommen, "Titel", FontWeight.Bold),
                Schrift.Text(Texte.WillkommenText),
                Schrift.Text(Texte.WasIstEinProjekt, "Mittel", FontWeight.Bold),
                Schrift.Text(Texte.WasIstEinProjektText),
                new WrapPanel { Children = { neu, oeffnen, update } },
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
        BlueprintsZeigen();
        _modellWahl.Items.Clear();
        foreach (var m in AussenBearbeitung.HausModelle(projekt.AussenStand))
        {
            _modellWahl.Items.Add(new ComboBoxItem { Content = m, Tag = m });
        }
        _modellWahl.SelectedIndex = _modellWahl.Items.Count > 0 ? 0 : -1;
        TitelSetzen();
        ReiterZeigen(innen && projekt.Innenorte.Count > 0);
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
        // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 5): Rueckgaengig/Wiederholen zuerst, vor allem anderen
        KnoepfeFreigeben();
        if (_projekt?.SpeicherFehler is { } fehler)
        {
            Melden(Texte.AutoSpeichernFehler + fehler);
        }
        if (_projekt is not null && string.Join("|", _projekt.Bauten.Select(_projekt.OrtName)) != _bautenText)
        {
            // ein Blueprint mit Innenraum gesetzt oder das Gebaeude geloescht: Ortsliste neu; ist der offene Innenraum weg, nach draussen
            OrtListeZeigen();
            if (_innen && _ort is { } offen && !_projekt.GibtInnenort(offen))
            {
                _ort = null;
                ReiterZeigen(innen: false);
            }
            KnoepfeFreigeben();
        }
        _aussenStand++;
        _dreiAenderung = Stopwatch.GetTimestamp();
        _bedienung3D?.StandGeaendert();
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
        EtagenZeigen();
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
        innen = innen && _projekt.Innenorte.Count > 0;
        _innen = innen;
        _drei = false;
        if (innen)
        {
            _innenAnsicht ??= InnenBauen();
            _inhalt.Content = _innenAnsicht;
            OrtOeffnen(_ort is { } o && _projekt.GibtInnenort(o) ? o : _projekt.Innenorte[0]);
        }
        else
        {
            _kartenAnsicht ??= KarteBauen();
            _inhalt.Content = _kartenAnsicht;
            DreiTeileZeigen(false);
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
            var k = new Button { Content = Schrift.Text(text, "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }.Vorlese(text + " setzen").Mit(Texte.SetzenKnopfTipp);
            k.Click += (_, _) => SetzenKnopfGedrueckt(k, false, () => AussenSetzenWaehlen(art, modell?.Invoke()));
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
        // Welle K1 (Weisung P12-55): Kurven mit Tipp, Radius der Kurve 90° darunter
        Button KurvenKnopf(string text, string tipp, Func<string> kennung)
        {
            var k = new Button { Content = Schrift.Text(text, "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }.Vorlese(text + " setzen").Mit(tipp + " " + Texte.SetzenKnopfTipp);
            k.Click += (_, _) => SetzenKnopfGedrueckt(k, false, () => AussenSetzenWaehlen(AussenArt.Kurve, kennung()));
            KurvenKnoepfe.Add(k);
            return k;
        }
        var kurven = new WrapPanel
        {
            Children =
            {
                KurvenKnopf(Texte.Kurve90, Texte.Kurve90Tipp, () => Strassenkurve.Kennung(Strassenkurve.Bogen, 90, KurvenRadiusGewaehlt, false)),
                KurvenKnopf(Texte.Kurve45, Texte.Kurve45Tipp, () => Strassenkurve.Kennung(Strassenkurve.Bogen, 45, 2, false)),
                KurvenKnopf(Texte.SKurve, Texte.SKurveTipp, () => Strassenkurve.Kennung(Strassenkurve.S, 45, 2, false)),
            },
        };
        // Welle K3 (Weisung P12-59, Frage 1 = a): Kreisverkehr mit Radius 2 oder 3
        Button KreisKnopf(string text, string tipp, double radius)
        {
            var k = new Button { Content = Schrift.Text(text, "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }.Vorlese(text + " setzen").Mit(tipp + " " + Texte.SetzenKnopfTipp);
            k.Click += (_, _) => SetzenKnopfGedrueckt(k, false, () => AussenSetzenWaehlen(AussenArt.Kreisverkehr, AussenBearbeitung.Kennung(radius)));
            KreisKnoepfe.Add(k);
            return k;
        }
        var kreise = new WrapPanel
        {
            Children = { KreisKnopf(Texte.KreisverkehrKlein, Texte.KreisverkehrKleinTipp, 2), KreisKnopf(Texte.KreisverkehrGross, Texte.KreisverkehrGrossTipp, 3) },
        };
        var radiusTitel = Schrift.Text(Texte.KurvenRadius, "Klein").Fuer(_kurvenRadius);
        _kurvenRadius.Vorlese(Texte.KurvenRadius).Mit(Texte.KurvenRadiusTipp);
        _kurvenRadius.SelectionChanged += (_, _) =>
        {
            // eine Kurve 90° im Setzen nimmt den neuen Radius gleich mit
            if (Karte.SetzenArt == AussenArt.Kurve && Strassenkurve.Lesen(Karte.SetzenModell) is { Form: Strassenkurve.Bogen, Winkel: 90 } k)
            {
                Karte.SetzenModell = Strassenkurve.Kennung(k.Form, 90, KurvenRadiusGewaehlt, k.Gespiegelt);
                _setzenName = Strassenkurve.Name(k.Form, 90, KurvenRadiusGewaehlt);
                AussenAuswahlZeigen();
            }
        };
        // S 0.8.0 (Weisung P12-47): Wasser, jeder Knopf mit Tipp
        Button WasserKnopf(string text, string tipp, AussenArt art, string? form = null)
        {
            var k = new Button { Content = Schrift.Text(text, "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }.Vorlese(text + " setzen").Mit(tipp + " " + Texte.SetzenKnopfTipp);
            k.Click += (_, _) => SetzenKnopfGedrueckt(k, false, () => AussenSetzenWaehlen(art, form));
            return k;
        }
        var wasser = new WrapPanel
        {
            Children =
            {
                WasserKnopf(Texte.Fluss, Texte.FlussTipp, AussenArt.Fluss), WasserKnopf(Texte.Teich, Texte.TeichTipp, AussenArt.Teich),
                WasserKnopf(Texte.TeichRund, Texte.TeichRundTipp, AussenArt.Teich, "rund"), WasserKnopf(Texte.Steg, Texte.StegTipp, AussenArt.Steg),
            },
        };
        // Welle K2 (Weisung P12-72): Bahn setzen (eigener kleiner Baustein, Welle Q kann ihn verschieben)
        var bahn = new WrapPanel { Children = { WasserKnopf(Texte.Bahn, Texte.BahnTipp, AussenArt.Bahn) } };
        BahnKnopf = (Button)bahn.Children[0];
        var modellTitel = Schrift.Text(Texte.Modell, "Klein").Fuer(_modellWahl);
        var haus = KatalogKnopf(Texte.HausSetzen, AussenArt.Gebaeude, () => _modellWahl.SelectedItem is ComboBoxItem { Tag: string m } ? m : null);
        var links = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Gruppe(Schrift.Text(Texte.AussenKatalog, "Mittel", FontWeight.Bold), Texte.TippAussenKatalog), _aussenHinweis, _setzenBeendenLinks, reihe, ZiehenBaustein(), kurven, radiusTitel, _kurvenRadius, kreise, VollerKatalogKnopf(false),
                Gruppe(Schrift.Text(Texte.WasserGruppe, gewicht: FontWeight.Bold), Texte.TippWasser), wasser, _eisImWinter,
                Gruppe(Schrift.Text(Texte.BahnGruppe, gewicht: FontWeight.Bold), Texte.TippBahn), bahn,
                Gruppe(Schrift.Text(Texte.HausOhneOrt, gewicht: FontWeight.Bold), Texte.TippHausOhneOrt), modellTitel, _modellWahl, haus,
                Gruppe(Schrift.Text(Texte.MeineBlueprints, gewicht: FontWeight.Bold), Texte.TippMeineBlueprints), Schrift.Text(Texte.BlueprintsHinweis, "Klein", farbe: "Pinsel.muted"), _blueprintListe, _aussenRasterAus,
            },
        };
        var linksRollen = new ScrollViewer { Content = Schrift.Karte(links), Margin = new Thickness(12, 12, 0, 12) };

        Karte.Vorlese(Texte.Draufsicht);
        Button Klein(string text, string name, Action a)
        {
            var k = Schrift.Knopf(text).Vorlese(name);
            k.Click += (_, _) => a();
            return k;
        }
        var zoom = new WrapPanel { Children = { Klein("+", Texte.Vergroessern, Karte.Vergroessern).Mit(Texte.ZoomTipp), Klein("−", Texte.Verkleinern, Karte.Verkleinern).Mit(Texte.ZoomTipp), Klein(Texte.GanzeStadtZeigen, Texte.GanzeStadtZeigen, Karte.Einpassen).Mit(Texte.GanzeStadtZeigenTipp) } };
        var mitte = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(12) };
        mitte.Children.Add(new Border { Child = Karte, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1) }.Farbe(Border.BorderBrushProperty, "Pinsel.border"));
        Grid.SetRow(zoom, 1);
        zoom.Margin = new Thickness(0, 8, 0, 0);
        mitte.Children.Add(zoom);
        // S 0.7.0: die Mitte zeigt die Draufsicht oder die 3D-Ansicht; links und rechts bleiben dieselben
        _karteMitte = mitte;
        _aussenMitte.Content = mitte;

        var rechts = _aussenRechts;
        rechts.Children.Add(Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Auswahl, "Mittel", FontWeight.Bold), _aussenAuswahl } }));
        rechts.Children.Add(Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.PruefungenDraussen, "Mittel", FontWeight.Bold), _aussenPruefungen } }));
        rechts.Children.Add(Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Innenraeume, "Mittel", FontWeight.Bold), _ortListe } }));
        var rechtsRollen = new ScrollViewer { Content = new Border { Child = rechts, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) }, Margin = new Thickness(0, 12, 12, 12) };
        var g = _aussenRaster;
        g.Children.Add(linksRollen);
        Grid.SetColumn(_aussenMitte, 1);
        g.Children.Add(_aussenMitte);
        Grid.SetColumn(rechtsRollen, 2);
        g.Children.Add(rechtsRollen);
        return g;
    }

    // ---- voller Katalog (Phase 13 Welle 3b) -------------------------------------------------------

    private Button VollerKatalogKnopf(bool innen)
    {
        var k = Schrift.Knopf(Texte.VollerKatalogOeffnen, betont: true).Vorlese(Texte.VollerKatalog);
        Gruppe(k, Texte.TippVollerKatalog);
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
        _bautenText = _projekt is null ? "" : string.Join("|", _projekt.Bauten.Select(_projekt.OrtName));
        OrtWahlFuellen();
        if (_projekt is null || _projekt.Innenorte.Count == 0)
        {
            _ortListe.Children.Add(Schrift.Text(Texte.KeineInnenraeume, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        foreach (var o in _projekt.Orte)
        {
            _ortListe.Children.Add(OrtKnopf(o));
        }
        if (_projekt.Bauten.Count > 0)
        {
            // Meldung P12-23 Frage 1 a: Innenraeume gesetzter Blueprints, nur im Werkzeug
            _ortListe.Children.Add(Schrift.Text(Texte.NeueGebaeude, "Mittel", FontWeight.Bold));
            _ortListe.Children.Add(Schrift.Text(Texte.BauInnenNochNichtTipp, "Klein", farbe: "Pinsel.muted"));
            foreach (var o in _projekt.Bauten)
            {
                _ortListe.Children.Add(OrtKnopf(o));
            }
        }

        Button OrtKnopf(string o)
        {
            var k = Schrift.Knopf(_projekt!.OrtName(o));
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            k.Click += (_, _) => OrtOeffnen(o);
            return k;
        }
    }

    /// <summary>Namen der neuen Gebaeude beim letzten Fuellen der Ortsliste (aendern sie sich, wird die Liste neu gefuellt).</summary>
    private string _bautenText = "";

    /// <summary>Auswahl "Ort" im Innenraum: Orte der Basis und neue Gebaeude des Projekts.</summary>
    private void OrtWahlFuellen()
    {
        if (_innenAnsicht is null || _projekt is null)
        {
            return;
        }
        _ortWahlFuellt = true;
        _ortWahl.Items.Clear();
        foreach (var o in _projekt.Innenorte)
        {
            _ortWahl.Items.Add(new ComboBoxItem { Content = _projekt.OrtName(o), Tag = o });
        }
        _ortWahl.SelectedItem = _ortWahl.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag as string == (_ort is null ? null : Etagen.Teilen(_ort).Ort));
        _ortWahlFuellt = false;
    }

    private bool _ortWahlFuellt;

    /// <summary>Name des Stuecks, das gerade gesetzt wird (mit Modell), fuer den Hinweis ueber der Karte.</summary>
    private string? _setzenName;

    /// <summary>Start-Test: ein Stueck zum Setzen waehlen wie mit dem Knopf links.</summary>
    public void AussenSetzenWaehlenFuerTests(AussenArt art, string? modell = null) => AussenSetzenWaehlen(art, modell);

    private void AussenSetzenWaehlen(AussenArt art, string? modell)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        // ein anderes Stueck (auch aus dem vollen Katalog): die alte Markierung links geht weg
        KnopfMarkieren(false);
        _setzenKnopf = null;
        Karte.SetzenArt = art;
        Karte.SetzenModell = modell;
        _setzenName = art switch
        {
            AussenArt.Teich when modell == "rund" => Texte.TeichRund,
            AussenArt.Gebaeude => Texte.HausOhneOrt + " (" + modell + ")",
            AussenArt.Katalog => KatalogPruefung.Name(Katalog.Aktiv, Katalog.Praefix + modell),
            AussenArt.Kurve when Strassenkurve.Lesen(modell) is { } k => Strassenkurve.Name(k.Form, k.Winkel, k.Radius),
            AussenArt.Kreisverkehr when AussenBearbeitung.KreisverkehrRadius(modell) is { } kr => Texte.KreisverkehrName(kr),
            AussenArt.Ziehen => AussenBearbeitung.ZiehArtVon(modell) switch
            {
                ZiehArt.Fussweg => Texte.FusswegZiehen,
                ZiehArt.Fluss => Texte.FlussZiehen,
                ZiehArt.Bahn => Texte.BahnZiehen,
                _ => Texte.StrasseZiehen,
            },
            _ => KatalogName(art),
        };
        // zeigt beim Setzen den Knopf "Drehen" (Strasse, Park, Katalog-Stueck)
        AussenAuswahlZeigen();
        Melden(_aussenHinweis.Text ?? "");
    }

    /// <summary>
    /// S 0.9.1 (Weisung P12-53 Teil A, Sammelliste 228): ein Setzen-Knopf links wurde angetippt. Ist er schon der
    /// gewaehlte, waehlt das zweite Antippen ihn ab (nichts gesetzt, kein Schritt im Verlauf); sonst waehlt er das
    /// Stueck und wird sichtbar markiert.
    /// </summary>
    private void SetzenKnopfGedrueckt(Button knopf, bool rund, Action waehlen)
    {
        if (!AbwaehlenAltFuerTests && ReferenceEquals(_setzenKnopf, knopf) && Karte.SetzenArt is not null)
        {
            SetzenBeenden();
            return;
        }
        waehlen();
        if (Karte.SetzenArt is null)
        {
            return;
        }
        if (!ReferenceEquals(_setzenKnopf, knopf))
        {
            KnopfMarkieren(false);
            _setzenKnopf = knopf;
            _setzenKnopfRund = rund;
            KnopfMarkieren(true);
        }
    }

    /// <summary>Start-Test: einen Setzen-Knopf links antippen wie mit dem Finger.</summary>
    public void SetzenKnopfDrueckenFuerTests(Button knopf) => knopf.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    /// <summary>Setzen beenden ohne zu setzen (Knopf „Setzen beenden“, zweites Antippen des Knopfs, Escape).</summary>
    public void SetzenBeenden()
    {
        if (Karte.SetzenArt is null)
        {
            return;
        }
        Karte.SetzenBeenden();
        Melden(Texte.SetzenBeendet);
    }

    /// <summary>Markierung des gewaehlten Knopfs wie ein betonter Knopf (Verlauf, helle Schrift), Name mit „gewählt“.</summary>
    private void KnopfMarkieren(bool an)
    {
        if (_setzenKnopf is not { } k)
        {
            return;
        }
        var text = k.Content as TextBlock;
        var name = Avalonia.Automation.AutomationProperties.GetName(k) ?? "";
        const string marke = ", " + Texte.SetzenGewaehlt;
        if (an)
        {
            k.Farbe(Button.BackgroundProperty, "VerlaufW.primary").Farbe(Button.ForegroundProperty, "Pinsel.on_accent");
            text?.Farbe(TextBlock.ForegroundProperty, "Pinsel.on_accent");
            if (!name.EndsWith(marke, StringComparison.Ordinal))
            {
                k.Vorlese(name + marke);
            }
            return;
        }
        if (_setzenKnopfRund)
        {
            k.Farbe(Button.BackgroundProperty, "Pinsel.card_alt").Farbe(Button.ForegroundProperty, "Pinsel.text");
        }
        else
        {
            k.ClearValue(Button.BackgroundProperty);
            k.ClearValue(Button.ForegroundProperty);
        }
        text?.Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        if (name.EndsWith(marke, StringComparison.Ordinal))
        {
            k.Vorlese(name[..^marke.Length]);
        }
    }

    /// <summary>Das Setzen hat begonnen oder geendet (auch durch Setzen eines Stuecks oder Escape).</summary>
    private void SetzenZustandZeigen()
    {
        var setzen = Karte.SetzenArt is not null;
        _setzenBeendenLinks.IsVisible = setzen;
        if (!setzen)
        {
            KnopfMarkieren(false);
            _setzenKnopf = null;
            AussenAuswahlZeigen();
        }
    }

    private static string KatalogName(AussenArt art) => art switch
    {
        AussenArt.Strasse => Texte.Strasse,
        AussenArt.Baum => Texte.Baum,
        AussenArt.Park => Texte.Park,
        AussenArt.Beet => Texte.Beet,
        AussenArt.Laterne => Texte.Laterne,
        AussenArt.Katalog => Texte.VollerKatalog,
        AussenArt.Blueprint => Texte.BlueprintSetzen,
        AussenArt.Fluss => Texte.Fluss,
        AussenArt.Teich => Texte.Teich,
        AussenArt.Steg => Texte.Steg,
        AussenArt.Fussweg => Texte.Fussweg,
        AussenArt.Bahn => Texte.Bahn,
        AussenArt.Ziehen => Texte.StrasseZiehen,
        _ => Texte.HausOhneOrt,
    };

    /// <summary>
    /// Hinweis links: beim Ziehen „Anfang antippen“ bzw. „Ende antippen“ (Welle Z), sonst wie bisher. Sammelliste 236 (Weisung
    /// P12-72 Punkt 3): solange die Vorschau rot ist, steht ihr Grund hier (wie in 3D im Schatten-Text), nicht erst nach dem Tipp.
    /// </summary>
    private string SetzenHinweis() => Karte.SetzenArt switch
    {
        null => Texte.DraufsichtHinweis,
        AussenArt.Ziehen when Karte.ZiehAnfang is not null && Karte.ZiehGrund is { } grund && !GrundErstNachTippFuerTests =>
            (_setzenName ?? KatalogName(AussenArt.Ziehen)) + ": " + grund + " " + Texte.ZiehenEnde,
        AussenArt.Ziehen => (_setzenName ?? KatalogName(AussenArt.Ziehen)) + ": " + (Karte.ZiehAnfang is null ? Texte.ZiehenAnfang : Texte.ZiehenEnde),
        { } art => (_setzenName ?? KatalogName(art)) + ": " + Texte.AussenSetzenAktiv,
    };

    private void AussenAuswahlZeigen()
    {
        _aussenAuswahl.Children.Clear();
        _eisSetzen = true;
        _eisImWinter.IsChecked = _projekt is not null && Aussen.Eis(_projekt.AussenStand);
        _eisImWinter.IsEnabled = _projekt is not null && !_projekt.NurLesen;
        _eisSetzen = false;
        _aussenHinweis.Text = SetzenHinweis();
        if (Karte.SetzenArt is null)
        {
            _setzenName = null;
        }
        if (Karte.SetzenArt is not null && _projekt is not null && !_projekt.NurLesen)
        {
            // S 0.9.1 (Weisung P12-53 Teil A): auch rechts bei der Auswahl, gut erreichbar am Tablet
            var beenden = Schrift.Knopf(Texte.SetzenBeenden).Vorlese(Texte.SetzenBeenden).Mit(Texte.SetzenBeendenTipp);
            beenden.Click += (_, _) => SetzenBeenden();
            _aussenAuswahl.Children.Add(beenden);
        }
        if (_projekt is not null && !_projekt.NurLesen && KartenFlaeche.DrehbarBeimSetzen(Karte.SetzenArt))
        {
            // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 4): schon beim Setzen drehen, ein Knopf statt einer versteckten Geste
            var lage = Karte.SetzenArt switch
            {
                AussenArt.Strasse => Karte.SetzenDreh % 2 == 0 ? Texte.SetzenWaagerecht : Texte.SetzenSenkrecht,
                AussenArt.Kurve => Texte.SetzenKurveLage(Karte.SetzenDreh, Karte.SetzenGespiegelt),
                _ => Texte.SetzenGedreht(Karte.SetzenDreh),
            };
            var setzenDrehen = Schrift.Knopf(Texte.Drehen, betont: true).Mit(Texte.DrehenTipp);
            setzenDrehen.Click += (_, _) => Karte.SetzenDrehen();
            _aussenAuswahl.Children.Add(Schrift.Text(lage, "Klein"));
            if (Karte.SetzenArt == AussenArt.Kurve)
            {
                // Welle K1: Spiegeln (Taste M) neben Drehen
                var setzenSpiegeln = Schrift.Knopf(Texte.Spiegeln).Mit(Texte.SpiegelnTipp);
                setzenSpiegeln.Click += (_, _) => Karte.SetzenSpiegeln();
                _aussenAuswahl.Children.Add(new WrapPanel { Children = { setzenDrehen, setzenSpiegeln } });
                return;
            }
            _aussenAuswahl.Children.Add(setzenDrehen);
            return;
        }
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
        TextBox? gehweg = null, laenge = null, vor = null, seitlich = null, kw = null, kt = null, winkel = null;
        CheckBox? doppelt = null;
        if (s.Art == AussenArt.Katalog)
        {
            var (gw, gt) = AussenBearbeitung.KatalogGroesse(a, s);
            kw = Feld(gw);
            kt = Feld(gt);
            zeilen.Add((Texte.Breite, kw));
            zeilen.Add((Texte.Tiefe, kt));
        }
        if (s.Art == AussenArt.Steg)
        {
            var (sw, st) = AussenBearbeitung.StegGroesse(a, s);
            kw = Feld(sw);
            kt = Feld(st);
            zeilen.Add((Texte.StegLaenge, kw));
            zeilen.Add((Texte.StegBreite, kt));
        }
        // Welle K3: auch ein Kreisverkehr hat eine feste Laenge (der Radius bestimmt sie)
        var istKurve = AussenBearbeitung.IstKurve(a, s) || AussenBearbeitung.IstKreisverkehr(a, s);
        if (s.Art == AussenArt.Strasse)
        {
            gehweg = Feld(AussenBearbeitung.Gehweg(a, s));
            zeilen.Add((Texte.GehwegBreite, gehweg));
            if (!istKurve)
            {
                // Welle K1: eine Kurve hat eine feste Laenge (Radius und Winkel)
                laenge = Feld(AussenBearbeitung.Laenge(a, s));
                zeilen.Add((Texte.Laenge, laenge));
            }
        }
        if (AussenBearbeitung.Winkel(a, s) is { } grad)
        {
            // Welle E (Weisung P12-67 Frage 2 = a): Winkel auf 1 Grad genau
            winkel = Feld(grad);
            winkel.Mit(Texte.WinkelTipp);
            zeilen.Add((Texte.Winkel, winkel));
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
                || (kw is not null && !Zahl(kw, out _)) || (kt is not null && !Zahl(kt, out _)) || (winkel is not null && !Zahl(winkel, out _)))
            {
                Melden("Bitte nur Zahlen eintragen (z. B. 2,5).");
                return;
            }
            var ng = 0.0;
            var nl = 0.0;
            if (gehweg is not null)
            {
                Zahl(gehweg, out ng);
            }
            if (laenge is not null)
            {
                Zahl(laenge, out nl);
            }
            _projekt.AussenAendern("Werte", aa =>
            {
                if (s.Art == AussenArt.Strasse)
                {
                    AussenBearbeitung.GehwegSetzen(aa, s, ng);
                    if (laenge is not null && Math.Abs(nl - AussenBearbeitung.Laenge(aa, s)) > 0.005)
                    {
                        AussenBearbeitung.LaengeSetzen(aa, s, nl);
                    }
                }
                if (kw is not null && kt is not null && Zahl(kw, out var nw) && Zahl(kt, out var nt))
                {
                    if (s.Art == AussenArt.Steg)
                    {
                        AussenBearbeitung.StegGroesseSetzen(aa, s, nw, nt);
                    }
                    else
                    {
                        AussenBearbeitung.KatalogGroesseSetzen(aa, s, nw, nt);
                    }
                }
                AussenBearbeitung.Platzieren(aa, s, nx, ny);
                if (winkel is not null && Zahl(winkel, out var nwk))
                {
                    AussenBearbeitung.WinkelSetzen(aa, s, (int)Math.Round(nwk, MidpointRounding.AwayFromZero));
                }
            });
            Karte.Waehlen(AussenBearbeitung.Gueltig(_projekt.AussenStand, s) ? s : null);
            Melden("Werte übernommen");
        };
        // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 4): "Drehen" vorn und hervorgehoben; was das Spiel nicht drehen kann,
        // ist grau, der Grund steht als Tipp am Knopf (auch grau sichtbar) und klein darunter
        var drehenNicht = AussenBearbeitung.DrehenNicht(s);
        var drehen = Schrift.Knopf(Texte.Drehen, betont: drehenNicht is null).Mit(drehenNicht ?? Texte.DrehenTipp);
        ToolTip.SetShowOnDisabled(drehen, true);
        drehen.Click += (_, _) => Karte.Drehen();
        drehen.IsEnabled = drehenNicht is null;
        var kopie = Schrift.Knopf(Texte.Kopie);
        kopie.Click += (_, _) => Karte.Kopieren();
        kopie.IsEnabled = s.Art is not (AussenArt.Haus or AussenArt.Paketplatz);
        var weg = Schrift.Gefahrknopf(Texte.Loeschen);
        weg.Click += (_, _) => Karte.Loeschen();
        weg.IsEnabled = AussenBearbeitung.LoeschenNicht(s) is null;
        var reiheKnoepfe = new WrapPanel { Children = { drehen, neu, kopie, weg } };
        if (istKurve)
        {
            // Welle K1: Spiegeln nur an Kurven (Taste M)
            var spiegeln = Schrift.Knopf(Texte.Spiegeln).Mit(Texte.SpiegelnTipp);
            spiegeln.Click += (_, _) => Karte.Spiegeln();
            reiheKnoepfe.Children.Insert(1, spiegeln);
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.KurveLaengeFest, "Klein", farbe: "Pinsel.muted"));
        }
        _aussenAuswahl.Children.Add(reiheKnoepfe);
        if (AussenBearbeitung.RadiusWaehlbar(a, s))
        {
            // Welle K1: Radius am gewaehlten Eckpunkt des Flusses (2, 3 oder 5); Welle K2: der Bahn (3 oder 4,5)
            var jetzt = AussenBearbeitung.FlussRadius(a, s);
            var radien = new WrapPanel();
            foreach (var r in AussenBearbeitung.Radien(s.Art))
            {
                var text = r.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                var k = Schrift.Knopf(text, betont: Math.Abs(r - jetzt) < 1e-9).Vorlese(Texte.RadiusHier + " " + text)
                    .Mit(s.Art == AussenArt.Bahn ? Texte.BahnRadiusTipp : Texte.RadiusHierTipp);
                var wert = r;
                k.Click += (_, _) =>
                {
                    _projekt.AussenAendern(Texte.RadiusHier, aa => AussenBearbeitung.FlussRadiusSetzen(aa, s, wert));
                    Melden(Texte.RadiusHier + " " + text);
                    Karte.Waehlen(s);
                };
                radien.Children.Add(k);
            }
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.RadiusHier, "Klein"));
            _aussenAuswahl.Children.Add(radien);
        }
        if (AussenBearbeitung.MitEckpunkten(s.Art))
        {
            // S 0.8.0: Eckpunkte von Teich und Fluss dazu und weg (Ziehen geht direkt an den Griffen)
            var dazu = Schrift.Knopf(Texte.EckpunktDazu).Mit(Texte.EckpunktDazuTipp);
            dazu.Click += (_, _) => Karte.EckpunktDazu();
            var wegNicht = AussenBearbeitung.EckpunktWegNicht(a, s);
            var eckWeg = Schrift.Knopf(Texte.EckpunktWeg).Mit(wegNicht ?? Texte.EckpunktWegTipp);
            ToolTip.SetShowOnDisabled(eckWeg, true);
            eckWeg.Click += (_, _) => Karte.EckpunktWeg();
            eckWeg.IsEnabled = wegNicht is null;
            _aussenAuswahl.Children.Add(new WrapPanel { Children = { dazu, eckWeg } });
        }
        // warum etwas nicht geht, steht klein darunter
        if (AussenBearbeitung.DrehenNicht(s) is null && !_projekt.NurLesen)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.DrehgriffHinweis, "Klein", farbe: "Pinsel.muted"));
        }
        foreach (var grund in new[] { AussenBearbeitung.DrehenNicht(s), AussenBearbeitung.LoeschenNicht(s) }.OfType<string>().Distinct())
        {
            _aussenAuswahl.Children.Add(Schrift.Text(grund, "Klein", farbe: "Pinsel.muted"));
        }
        var innenort = _projekt.InnenortVon(s);
        var oeffnenText = innenort is null ? null : Texte.Innenraeume + ": " + _projekt.OrtName(innenort);
        if (innenort is not null)
        {
            var oeffnen = Schrift.Knopf(oeffnenText!);
            oeffnen.Click += (_, _) => OrtOeffnen(innenort);
            _aussenAuswahl.Children.Add(oeffnen);
        }
        if (s.Art is AussenArt.Haus or AussenArt.Gebaeude)
        {
            // Phase 13 Welle 4c (Weisung P12-32): das Gebaeude als Vorlage unter eigenem Namen
            var bp = Schrift.Knopf(Texte.AlsBlueprintSpeichern);
            bp.Click += (_, _) => _ = AlsBlueprintSpeichernAsync(s);
            _aussenAuswahl.Children.Add(bp);
        }
        foreach (var k in _aussenAuswahl.GetLogicalDescendants().OfType<Control>().Where(c => c is TextBox or CheckBox || (c is Button b && b.Content is TextBlock t && t.Text != oeffnenText)))
        {
            if (_projekt.NurLesen)
            {
                k.IsEnabled = false;
            }
        }
    }

    // ---- Blueprints (Phase 13 Welle 4c, Weisung P12-32) -------------------------------------------

    /// <summary>Die Blueprints der Liste "Meine Blueprints" (auch unlesbare, mit Grund).</summary>
    public IReadOnlyList<Blueprint.Eintrag> Blueprints { get; private set; } = [];

    /// <summary>Datenordner des Werkzeugs (Start-Test).</summary>
    public Werkzeugordner Ablage => _ordner;

    /// <summary>Uhr fuer Sicherungen der Blueprints.</summary>
    private static DateTime Jetzt => DateTime.Now;

    /// <summary>Liste "Meine Blueprints" neu lesen und zeigen.</summary>
    /// <summary>Je Eintrag der Liste "Meine Blueprints": sein Kasten und die Knoepfe darin (Start-Test: Knoepfe im Kasten).</summary>
    public List<(Border Kasten, List<Button> Knoepfe)> BlueprintKaesten { get; } = [];

    /// <summary>Nur Start-Test (Gegenprobe Weisung P12-33): Liste wie vor der Nachbesserung, Knoepfe ausserhalb der Kaesten.</summary>
    internal static bool BlueprintListeAltFuerTests { get; set; }

    public void BlueprintsZeigen()
    {
        _blueprintListe.Children.Clear();
        BlueprintKaesten.Clear();
        try
        {
            Blueprints = Blueprint.Alle(_ordner);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Blueprints lesen: " + e.GetType().Name + ": " + e.Message);
            Blueprints = [];
        }
        if (Blueprints.Count == 0)
        {
            _blueprintListe.Children.Add(Schrift.Text(Texte.KeineBlueprints, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        var nurLesen = _projekt is null || _projekt.NurLesen;
        foreach (var e in Blueprints)
        {
            // Weisung P12-33: je Eintrag ein Kasten mit Rahmen; oben der Name (antippen = setzen), darunter Umbenennen
            // und Loeschen nebeneinander. Eine kaputte Datei: Name als Text, roter Hinweis, nur Loeschen.
            var eintrag = new StackPanel { Spacing = 6 };
            var knoepfe = new List<Button>();
            if (e.Vorlage is { } b)
            {
                var setzen = Schrift.Knopf(e.Name).Vorlese(Texte.BlueprintSetzen + " „" + e.Name + "“");
                setzen.HorizontalAlignment = HorizontalAlignment.Stretch;
                setzen.Margin = new Thickness(0);
                setzen.IsEnabled = !nurLesen;
                setzen.Click += (_, _) => SetzenKnopfGedrueckt(setzen, true, () => BlueprintSetzenWaehlen(b));
                eintrag.Children.Add(setzen);
                knoepfe.Add(setzen);
            }
            else
            {
                eintrag.Children.Add(Schrift.Text(e.Name, "Klein", FontWeight.Bold));
                eintrag.Children.Add(Schrift.Text(e.Fehler ?? "", "Klein", farbe: "Pinsel.red"));
            }
            var loeschen = Klein(Schrift.Gefahrknopf(Texte.Loeschen).Vorlese(Texte.Loeschen + " „" + e.Name + "“"));
            loeschen.Click += (_, _) => _ = BlueprintLoeschenAsync(e);
            var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,8,*") };
            if (e.Vorlage is not null)
            {
                var umbenennen = Klein(Schrift.Knopf(Texte.Umbenennen).Vorlese(Texte.Umbenennen + " „" + e.Name + "“"));
                umbenennen.Click += (_, _) => _ = BlueprintUmbenennenAsync(e);
                zeile.Children.Add(umbenennen);
                knoepfe.Add(umbenennen);
            }
            Grid.SetColumn(loeschen, 2);
            zeile.Children.Add(loeschen);
            knoepfe.Add(loeschen);
            var kasten = new Border
            {
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Child = BlueprintListeAltFuerTests ? eintrag : new StackPanel { Spacing = 8, Children = { eintrag, zeile } },
            }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border_hi");
            _blueprintListe.Children.Add(kasten);
            if (BlueprintListeAltFuerTests)
            {
                _blueprintListe.Children.Add(zeile);
            }
            BlueprintKaesten.Add((kasten, knoepfe));
        }

        static Button Klein(Button k)
        {
            k.Padding = new Thickness(10, 4);
            if (k.Content is TextBlock t)
            {
                t.Groesse("Klein");
            }
            k.Margin = new Thickness(0);
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            return k;
        }
    }

    /// <summary>Blueprint zum Setzen waehlen: das naechste Tippen auf die Karte setzt ihn.</summary>
    public void BlueprintSetzenWaehlen(Blueprint b)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        KnopfMarkieren(false);
        _setzenKnopf = null;
        Karte.SetzenArt = AussenArt.Blueprint;
        Karte.SetzenBlueprint = b;
        Karte.SetzenModell = null;
        _setzenName = Texte.BlueprintSetzen + " „" + b.Name + "“";
        // rechts bei der Auswahl: Knopf "Setzen beenden" (S 0.9.1)
        AussenAuswahlZeigen();
        _aussenHinweis.Text = Texte.BlueprintSetzen + " „" + b.Name + "“: " + Texte.AussenSetzenAktiv;
        Melden(_aussenHinweis.Text);
    }

    /// <summary>Vorschlag fuer den Namen: Name des Orts bzw. des Gebaeudes, sonst leer.</summary>
    private string BlueprintVorschlag(AussenStelle s)
    {
        var a = _projekt!.AussenStand;
        var v = s.Art == AussenArt.Haus ? _daten!.OrtName(s.Ort)
            : Aussen.Landschaft(a).ElementAtOrDefault(s.Nummer) is PyObjekt g && Blueprint.Zierweg(a, Py.Str(g["modell"]))?["name"] is string n ? n : "";
        return Blueprint.NameGueltig(v) ? v : "";
    }

    /// <summary>Gebaeude als Blueprint speichern (ohne Fenster, fuer den Start-Test): null = gespeichert, sonst der Grund.</summary>
    public string? BlueprintSpeichern(AussenStelle s, string name)
    {
        if (_projekt is null)
        {
            return Texte.BlueprintNurHaus;
        }
        if (!Blueprint.NameGueltig(name))
        {
            return Texte.NameUngueltig;
        }
        try
        {
            if (Blueprint.Aus(_projekt, s, name) is not { } b)
            {
                return Texte.BlueprintNurHaus;
            }
            b.Speichern(_ordner, Jetzt);
        }
        catch (IOException e)
        {
            return e.Message;
        }
        BlueprintsZeigen();
        Melden(Texte.BlueprintGespeichert + ": „" + name + "“");
        return null;
    }

    private async Task AlsBlueprintSpeichernAsync(AussenStelle s)
    {
        var name = new TextBox { Text = BlueprintVorschlag(s), MaxLength = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
        var fehler = Schrift.Text("", "Klein", farbe: "Pinsel.red");
        var inhalt = new StackPanel
        {
            Spacing = 8,
            Children = { Schrift.Text(Texte.BlueprintName, gewicht: FontWeight.Bold).Fuer(name), name, Schrift.Text(Texte.BlueprintNameHinweis, "Klein", farbe: "Pinsel.muted"), fehler },
        };
        while (true)
        {
            if (await Dialog.Zeigen(Texte.BlueprintTitel, inhalt, Texte.Speichern, Texte.Abbrechen) != 0)
            {
                return;
            }
            if (BlueprintSpeichern(s, (name.Text ?? "").Trim()) is not { } grund)
            {
                return;
            }
            fehler.Text = grund;
        }
    }

    /// <summary>Umbenennen ohne Fenster (Start-Test): null = umbenannt, sonst der Grund.</summary>
    public string? BlueprintUmbenennen(Blueprint.Eintrag e, string name)
    {
        if (!Blueprint.NameGueltig(name))
        {
            return Texte.NameUngueltig;
        }
        try
        {
            Blueprint.Umbenennen(_ordner, e.Datei, name, Jetzt);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            return x is StadtbauFehler f ? string.Join(" ", f.Funde) : x.Message;
        }
        BlueprintsZeigen();
        Melden(Texte.Umbenennen + ": „" + name + "“");
        return null;
    }

    private async Task BlueprintUmbenennenAsync(Blueprint.Eintrag e)
    {
        var name = new TextBox { Text = e.Name, MaxLength = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
        var fehler = Schrift.Text("", "Klein", farbe: "Pinsel.red");
        var inhalt = new StackPanel
        {
            Spacing = 8,
            Children = { Schrift.Text(Texte.BlueprintName, gewicht: FontWeight.Bold).Fuer(name), name, Schrift.Text(Texte.BlueprintNameHinweis, "Klein", farbe: "Pinsel.muted"), fehler },
        };
        while (true)
        {
            if (await Dialog.Zeigen(Texte.UmbenennenTitel, inhalt, Texte.Umbenennen, Texte.Abbrechen) != 0)
            {
                return;
            }
            if (BlueprintUmbenennen(e, (name.Text ?? "").Trim()) is not { } grund)
            {
                return;
            }
            fehler.Text = grund;
        }
    }

    /// <summary>Loeschen nach der Rueckfrage (die Datei kommt vorher in die Sicherungen).</summary>
    private async Task BlueprintLoeschenAsync(Blueprint.Eintrag e)
    {
        if (await Dialog.Zeigen(Texte.BlueprintLoeschenTitel, Texte.BlueprintLoeschenFrage(e.Name), Texte.Loeschen, Texte.Abbrechen) != 0)
        {
            return;
        }
        BlueprintLoeschen(e);
    }

    /// <summary>Loeschen ohne Rueckfrage (nur nach der Rueckfrage und im Start-Test).</summary>
    public void BlueprintLoeschen(Blueprint.Eintrag e)
    {
        try
        {
            Blueprint.Loeschen(_ordner, e.Datei, Jetzt);
            Melden(Texte.BlueprintGeloescht + ": „" + e.Name + "“");
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Blueprint löschen: " + x.GetType().Name + ": " + x.Message);
            Melden(x.Message);
        }
        BlueprintsZeigen();
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
        var bauten = (PyObjekt)Py.Kopie(Aussen.Bauten(_projekt.AussenStand))!;
        AussenPruefungLaeuft();
        var uhr = Stopwatch.StartNew();
        _ = Hintergrund(Task.Run(() => Projekt.PruefenAussen(fassung, daten, bauten))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
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
        _figurLaufen.IsCheckedChanged += (_, _) =>
        {
            Bedienung3D.FigurLaufen = _figurLaufen.IsChecked == true;
            Bedienung3D.SchattenWeg();
        };
        _figurLaufen.Vorlese(Texte.FigurLaufen);
        // S 0.7.0 (Plan P12-26b Abschnitt 9): rechts dieselbe Spalte wie in der Draufsicht, die Karten der 3D-Ansicht
        // kommen dazu (oben die 3D-Karte, unter „Ausgewähltes Stück“ Tag/Nacht, Jahreszeit, Fest, Probe und Grenzen)
        Control[] oben = [Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.Ansicht3D, "Mittel", FontWeight.Bold), Schrift.Text(Texte.Ansicht3DHinweis, "Klein", farbe: "Pinsel.muted"), _dreiStatus } })];
        Control[] unter =
        [
            Schrift.Karte(new StackPanel
            {
                Spacing = 6,
                Children = { Zeilen((Texte.Tag + " / " + Texte.Nacht, licht), (Texte.Jahreszeit, jahr), (Texte.Fest, fest)) },
            }),
            Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.ProbeLaufen, "Mittel", FontWeight.Bold), Schrift.Text(Texte.ProbeLaufenHinweis, "Klein", farbe: "Pinsel.muted"), _figurLaufen } }),
            Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.GrenzenGesamt, "Mittel", FontWeight.Bold), _dreiGrenzBalken, _dreiGrenzText, messen } }),
        ];
        _kartenAnsicht ??= KarteBauen();
        _aussenRechts.Children.Insert(0, oben[0]);
        for (var i = 0; i < unter.Length; i++)
        {
            _aussenRechts.Children.Insert(2 + i, unter[i]);
        }
        _dreiKarten.AddRange(oben);
        _dreiKarten.AddRange(unter);

        Button Klein(string text, string name, string tipp, Action a)
        {
            var k = Schrift.Knopf(text).Vorlese(name).Mit(tipp);
            k.Click += (_, _) => a();
            return k;
        }
        var knoepfe = new WrapPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                Klein("+", Texte.Vergroessern, Texte.ZoomTipp, () => Bedienung3D.Zoomen(1.25f)),
                Klein("−", Texte.Verkleinern, Texte.ZoomTipp, () => Bedienung3D.Zoomen(0.8f)),
                Klein(Texte.GanzeStadtZeigen, Texte.GanzeStadtZeigen, Texte.GanzeStadt3DTipp, Bedienung3D.GanzeStadt),
                Klein(Texte.Steiler, Texte.Steiler, Texte.KippenTipp, () => Bedienung3D.Kippen(8)),
                Klein(Texte.Flacher, Texte.Flacher, Texte.KippenTipp, () => Bedienung3D.Kippen(-8)),
            },
        };
        var ebene = new Grid();
        ebene.Children.Add(Drei);
        // ohne Tipp: ein Tipp ueber der ganzen Ansicht laege beim Bauen ueber der Stadt (Foto S 0.7.0)
        ebene.Children.Add(Bedienung3D.Vorlese(Texte.Ansicht3D));
        var flaeche = new Border { Child = ebene, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        var mitte = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(12) };
        mitte.Children.Add(flaeche);
        Grid.SetRow(knoepfe, 1);
        mitte.Children.Add(knoepfe);
        _dreiMitte = mitte;
        DreiTeileZeigen(_drei);
        return mitte;
    }

    /// <summary>S 0.7.0: Mitte (Draufsicht oder 3D) und die Karten der 3D-Ansicht rechts umschalten.</summary>
    private void DreiTeileZeigen(bool drei)
    {
        _aussenMitte.Content = drei ? _dreiMitte : _karteMitte;
        foreach (var k in _dreiKarten)
        {
            k.IsVisible = drei;
        }
        if (!drei)
        {
            _bedienung3D?.SchattenWeg();
        }
    }

    public void DreiZeigen()
    {
        if (_projekt is null)
        {
            return;
        }
        _drei = true;
        _innen = false;
        _kartenAnsicht ??= KarteBauen();
        _dreiAnsicht ??= DreiBauen();
        _inhalt.Content = _kartenAnsicht;
        DreiTeileZeigen(true);
        Karte.Zeigen(_projekt);
        AussenAuswahlZeigen();
        AussenPruefungZeigen();
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
        Bedienung3D.Focus();
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
        _ = Hintergrund(Task.Run(() => Ansicht3D.Bauen(fassung, daten, einstellung, _vorrat3D))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
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
            var (ax, az, ab, ad, an) = alt is null ? (float.NaN, 0f, 0f, 0f, 0f) : (alt.ZielX, alt.ZielZ, alt.Abstand, alt.Drehung, alt.Neigung);
            Tageslicht.FesteUhr = einstellung.Uhr;
            Drei.Spiel = t.Result;
            if (!float.IsNaN(ax) && Drei.AktuelleKamera is { } neu)
            {
                // die Kamera bleibt, wo sie war (sonst springt sie nach jeder Aenderung auf die Figur)
                neu.Setzen(ax, az);
                neu.Zoomen(neu.Abstand / ab);
                neu.Drehung = ad;
                neu.Neigung = an;
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
        var (ax, az, ab, an) = (kamera.ZielX, kamera.ZielZ, kamera.Abstand, kamera.Neigung);
        // gemessen wird wie im Spiel (Neigung der Stadt), auch wenn die Kamera gekippt ist
        kamera.Neigung = Bauen3D.NeigungSpiel;
        kamera.Zoomen(kamera.Abstand / Kamera.AbstandMax);
        kamera.Setzen(spiel.Szene.Breite / 2, spiel.Szene.Tiefe / 2);
        await Drei.NaechstesBildAsync();
        await Drei.NaechstesBildAsync();
        // Phase 13 Welle 4a: die 3D-Ansicht zeichnet die Stuecke aus dem Katalog selbst (wie das Spiel); gezaehlt wird das Bild
        GrenzenDraussen = (Drei.LetzteDreiecke, Drei.LetzteZeichenaufrufe);
        kamera.Setzen(ax, az);
        kamera.Zoomen(kamera.Abstand / ab);
        kamera.Neigung = an;
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
        foreach (var o in _projekt?.Innenorte ?? _daten!.Orte)
        {
            _ortWahl.Items.Add(new ComboBoxItem { Content = _projekt?.OrtName(o) ?? _daten!.OrtName(o), Tag = o });
        }
        var ortTitel = Gruppe(Schrift.Text(Texte.Ort, "Mittel", FontWeight.Bold).Fuer(_ortWahl), Texte.TippOrt);
        // Welle W5: Etagenliste links ueber dem Katalog (Plan P12-23b Abschnitt 1)
        var links = new StackPanel { Spacing = 8, Children = { ortTitel, _ortWahl, EtagenBaustein(), Gruppe(Schrift.Text(Texte.Katalog, "Mittel", FontWeight.Bold), Texte.TippKatalog), _katalogHinweis, VollerKatalogKnopf(true) } };
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
            Gruppe(klappe, Texte.TippGruppe(gruppe.Key));
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
                Klein("+", Texte.Vergroessern, Flaeche.Vergroessern).Mit(Texte.ZoomTipp),
                Klein("−", Texte.Verkleinern, Flaeche.Verkleinern).Mit(Texte.ZoomTipp),
                Klein(Texte.GanzenRaumZeigen, Texte.GanzenRaumZeigen, Flaeche.Einpassen).Mit(Texte.GanzenRaumZeigenTipp),
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
        if (!_projekt.GibtInnenort(ort))
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
        _ortWahlFuellt = true;
        foreach (var item in _ortWahl.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag as string == Etagen.Teilen(ort).Ort)
            {
                _ortWahl.SelectedItem = item;
            }
        }
        _ortWahlFuellt = false;
        Flaeche.Zeigen(_projekt, ort);
        EtagenZeigen();
        AuswahlZeigen();
        RaumZeigen();
        KnoepfeFreigeben();
        PruefenStarten();
        Melden(_projekt.OrtName(ort) + " geöffnet");
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
        var drehen = Schrift.Knopf(Texte.Drehen, betont: true).Mit(Texte.DrehenTipp);
        drehen.Click += (_, _) => Flaeche.Drehen();
        var kopie = Schrift.Knopf(Texte.Kopie);
        kopie.Click += (_, _) => Flaeche.Kopieren();
        var weg = Schrift.Gefahrknopf(Texte.Loeschen);
        weg.Click += (_, _) => Flaeche.Loeschen();
        _auswahl.Children.Add(new WrapPanel { Children = { drehen, neu, kopie, weg } });
        var waren = _daten!.Waren(Etagen.Teilen(ort).Ort);
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
        // Welle W5: Treppe und Fahrstuhl: Ziel und Blick der Ankunft
        EtagenAuswahl(d, ort, a);
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
        // Welle W5: eine Etage „ort@k“ prueft mit dem Ort und der Etage, dazu die Regeln zwischen den Etagen (wie Projekt.PruefenMitGrenzen)
        var (basis, etage) = Etagen.Teilen(ort);
        var eg = Etagen.Hat(_projekt.Grundriss(basis)) ? (PyObjekt)Py.Kopie(_projekt.Grundriss(basis))! : null;
        if (_geprueft.Task.IsCompleted)
        {
            _geprueft = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        var uhr = Stopwatch.StartNew();
        _ = Hintergrund(Task.Run(() =>
        {
            // Regeln des gemeinsamen Teils plus Katalog-Stuecke; Grenzen mit deren echten Dreiecken
            var (funde, grenzen) = KatalogPruefung.Innen(Katalog.Aktiv, basis, g, personen, daten.Grafik, daten.Geschaefte, daten.Fassung.Innenraeume, etage);
            if (eg is not null)
            {
                funde.AddRange(StadtbauPruefung.Etagen(basis, eg, daten.Grafik, (_, _) => []).Where(f => f.Etage == etage).Select(f => f.Fund));
            }
            return (funde, grenzen);
        })).ContinueWith(t => Dispatcher.UIThread.Post(() =>
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
        if (_projekt?.SpeicherFehler is { } fehler && !text.StartsWith(Texte.AutoSpeichernFehler, StringComparison.Ordinal))
        {
            text += " – " + Texte.AutoSpeichernFehler + fehler;
        }
        _status.Text = text;
        Ansage.Sagen(text);
    }

    public void Rueckgaengig()
    {
        if (_entwurf is { } e)
        {
            if (e.Entwurf.KannZurueck)
            {
                e.Entwurf.Zurueck();
                e.Waehlen(-1);
                Melden("Rückgängig gemacht");
            }
            return;
        }
        if (_projekt?.Rueckgaengig() == true)
        {
            NachVerlauf();
            Melden("Rückgängig gemacht");
        }
    }

    public void Wiederholen()
    {
        if (_entwurf is { } e)
        {
            if (e.Entwurf.KannWiederholen)
            {
                e.Entwurf.Wiederholen();
                e.Waehlen(-1);
                Melden("Wiederholt");
            }
            return;
        }
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
            _ => "Fehler: " + ergebnis.Art + (ergebnis.Code > 0 ? " (HTTP " + ergebnis.Code.ToString(De) + ")" : "") + ". Nimm „" + Texte.DateiSpeichern + "“ und gib die Datei an CC.",
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
            top.AddHandler(KeyUpEvent, TasteLos, RoutingStrategies.Tunnel);
            // Tablet: Wunsch "Systemleisten aus" an Avalonia geben (am PC gibt es keinen InsetsManager)
            WerkzeugVollbild.Leisten = top.InsetsManager;
            WerkzeugVollbild.LeistenMerken();
        }
    }

    private bool Drei3DTaste(Key taste)
    {
        switch (taste)
        {
            case Key.PageUp:
                Bedienung3D.Kippen(8);
                return true;
            case Key.PageDown:
                Bedienung3D.Kippen(-8);
                return true;
            default:
                return Drei.Taste(taste);
        }
    }

    private static void TasteLos(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            KartenFlaeche.LeertasteGehalten = false;
        }
    }

    private void TasteGedrueckt(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 && e.KeyModifiers == KeyModifiers.None)
        {
            VollbildUmschalten();
            e.Handled = true;
            return;
        }
        if (Dialog.Offen is null && _entwurf is not null)
        {
            e.Handled = EntwurfTaste(e);
            return;
        }
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
            if (e.Key == Key.Space)
            {
                // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 2): Leertaste halten und mit der Maus ziehen schiebt die Ansicht
                KartenFlaeche.LeertasteGehalten = true;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.E && e.KeyModifiers == KeyModifiers.None)
            {
                // Welle E: Einrasten an/aus (draussen und innen je ein Schalter)
                var schalter = _innen ? _rasterAus : _aussenRasterAus;
                schalter.IsChecked = schalter.IsChecked != true;
                Melden(Texte.Einrasten + (schalter.IsChecked == true ? ": an" : ": aus"));
                e.Handled = true;
                return;
            }
            if (_innen)
            {
                e.Handled = Flaeche.Taste(e.Key, e.KeyModifiers);
            }
            else if (!_drei)
            {
                e.Handled = Karte.Taste(e.Key, e.KeyModifiers);
            }
            else
            {
                // S 0.7.0: in 3D zuerst die Tasten der Draufsicht (Auswahl schieben, drehen, loeschen, kopieren), dann
                // Bild hoch/runter kippt, sonst die Kamera-Tasten des Spiels
                e.Handled = Karte.Taste(e.Key, e.KeyModifiers) || Drei3DTaste(e.Key);
            }
        }
    }
}
