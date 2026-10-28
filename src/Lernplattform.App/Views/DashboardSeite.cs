using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 1, K7: Dashboard mit allen Karten wie in der Python-Linie. Die Werte kommen
/// aus Core.Dashboard (Vergleichswerte G13), gelesen nur lesend aus lernplattform.db
/// (query_only; eine fehlende Datei wird nicht angelegt). Gerechnet wird im Hintergrund,
/// die Seite erscheint sofort mit "Wird geladen …".
/// </summary>
public sealed class DashboardSeite : UserControl
{
    private static readonly bool Pc = !OperatingSystem.IsAndroid();
    private readonly StackPanel _liste = new() { Spacing = 14, Margin = new Thickness(16, 4, 16, 24) };   // screen_list

    private readonly Action _wiederholen;
    private bool _gezeigt;

    /// <param name="wiederholen">"Jetzt wiederholen" (start_review), Phase 2 L4.</param>
    public DashboardSeite(Action wiederholen)
    {
        _wiederholen = wiederholen;
        DefektKarte();
        _liste.Children.Add(Schrift.Text(FarbenTexte.Laden, "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        Content = new ScrollViewer { Content = _liste };
        Geladen = LadenAsync();
    }

    /// <summary>Erstes Mal fertig gerechnet und gezeigt (fuer die Messung im Start ohne Bedienung).</summary>
    public TaskCompletionSource Fertig { get; } = new();

    /// <summary>Die zuletzt gestartete Rechnung (erstes Laden oder <see cref="Neu"/>).</summary>
    public Task Geladen { get; private set; }

    /// <summary>
    /// Bei jedem erneuten Zeigen neu rechnen wie Python (refresh): Tagesziel-Einstellungen
    /// oder ein neuer Tag wirken sofort. Die bisherigen Karten bleiben stehen, bis die
    /// neuen Werte fertig sind.
    /// </summary>
    public void Neu()
    {
        if (Geladen.IsCompleted)
        {
            Geladen = LadenAsync();
        }
    }

    private void DefektKarte()
    {
        if (Zustand.DefektHinweis is { } defekt)
        {
            _liste.Children.Add(Schrift.Karte(new StackPanel
            {
                Spacing = 4,
                Children = { Schrift.Text(defekt.Titel, "Mittel", FontWeight.Bold, "Pinsel.orange"), Schrift.Text(defekt.Text) },
            }));
        }
    }

    private async Task LadenAsync()
    {
        DashboardWerte? werte = null;
        try
        {
            var inhalte = await Zustand.DashboardInhalte;
            if (inhalte is not null)
            {
                var ziel = Einstellungen.Ziel(Zustand.Werte);
                var name = Core.Hilfe.Einrichten.NameLesen(Zustand.Werte);    // W4: "Hallo <Name>" (greeting)
                var pfad = System.IO.Path.Combine(Ablage.Datenordner(), Lernstand.DateiName);
                werte = await Task.Run(() => DashboardWerte.Lesen(pfad, DateOnly.FromDateTime(DateTime.Now), inhalte, ziel, name));
            }
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                // Neu rechnen ohne Ergebnis: bisherige Karten stehen lassen
                if (werte is null && _gezeigt)
                {
                    return;
                }
                _liste.Children.Clear();
                DefektKarte();
                if (werte is not null)
                {
                    Zeigen(werte);
                    _gezeigt = true;
                }
                Fertig.TrySetResult();
            }
            catch (Exception fehler)
            {
                // Start ohne Bedienung endet dann rot statt zu warten
                Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
                Fertig.TrySetException(fehler);
            }
        });
    }

    /// <summary>Ringfarben der vier Ringkarten und Abdeckungsbalken (Reihenfolge Karteikarten, Pruefungstrainer, AP1, AP2).</summary>
    private static readonly (string A, string B)[] RingFarben = [("accent", "purple"), ("purple", "accent2"), ("blue", "accent"), ("accent2", "orange")];

    /// <summary>THEME_COLOR: Farbe je Themenblock der AP1/AP2 in Reihenfolge der Themen.</summary>
    private static readonly string[] ThemenFarben = ["cyan", "pink", "purple", "blue", "green"];

    /// <summary>kind_color: Farbe des Punkts je Aktivitaetsart.</summary>
    private static string ArtFarbe(string art) => art switch
    {
        "Karteikarte" => "Pinsel.accent",
        "Prüfungsfrage" => "Pinsel.purple",
        "AP1-Szenario" => "Pinsel.blue",
        "AP2-Szenario" => "Pinsel.accent2",
        "Testprojekt" => "Pinsel.orange",
        "Test-Session" => "Pinsel.green",
        _ => "Pinsel.muted",
    };

    /// <summary>Kalender "Lerntage" (fuer die Messung des Blaetterns im Start ohne Bedienung).</summary>
    public LerntageKalender? Kalender { get; private set; }

    private DashboardWerte? _letzte;
    private bool _breit;

    /// <summary>Phase 8 W10 (Nico 4a): breite Anordnung, sobald der Inhalt breit genug ist (<see cref="DashboardAnordnung"/>).</summary>
    public bool Breit => _breit;

    /// <summary>Hoehe des Inhalts in Punkten (Start-Test: weniger Rollen in der breiten Anordnung).</summary>
    public double Hoehe => _liste.Bounds.Height;

    /// <summary>
    /// Start-Test: die Titel der fuenf Kacheln der ersten Reihe (vier Ringe und Erfolgsquote) in der breiten Anordnung.
    /// </summary>
    public IReadOnlyList<TextBlock> KachelTitel()
    {
        if (!_breit || _letzte is not { } w)
        {
            return [];
        }
        var namen = w.Kennzahlen.Select(k => Beschriftungen.Gross(k.Titel)).Append(Beschriftungen.Gross(DashboardTexte.QuoteTitel)).ToList();
        var bloecke = _liste.GetVisualDescendants().OfType<TextBlock>().ToList();
        return [.. namen.Select(n => bloecke.FirstOrDefault(b => b.Text == n)).OfType<TextBlock>()];
    }

    private bool BreitJetzt() => DashboardAnordnung.Breit(Bounds.Width - _liste.Margin.Left - _liste.Margin.Right, Schrift.Faktor, Schrift.Gross);

    /// <summary>Breite gewechselt: nur neu anordnen, die Werte bleiben (kein Neurechnen).</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_letzte is { } w && BreitJetzt() != _breit)
        {
            _liste.Children.Clear();
            DefektKarte();
            Zeigen(w);
        }
    }

    /// <summary>Zwei Karten nebeneinander im Verhaeltnis lg:rg (pair_rows wie Python am PC).</summary>
    private static Grid Paar(Control links, Control rechts, double lg = 1, double rg = 1)
    {
        var g = new Grid { ColumnSpacing = 14, ColumnDefinitions = new ColumnDefinitions($"{lg.ToString(System.Globalization.CultureInfo.InvariantCulture)}*,{rg.ToString(System.Globalization.CultureInfo.InvariantCulture)}*") };
        Grid.SetColumn(rechts, 1);
        g.Children.Add(links);
        g.Children.Add(rechts);
        return g;
    }

    /// <summary>
    /// A 0.12.2: Aufbau wie DashboardScreen am Handy (Python 0.63.2): Banner, Heute, vier Ringkarten in
    /// zwei Spalten, Erfolgsquote, Lernverlauf als Liniendiagramm, Abdeckung, Fachbereiche (3 + 2),
    /// Aktivitaeten, Lerntage mit Blaettern, AP1/AP2-Themen. Werte und Texte unveraendert aus Core.
    /// </summary>
    private void Zeigen(DashboardWerte w)
    {
        _letzte = w;
        var breit = _breit = BreitJetzt();
        var gross = Schrift.Gross;   // ab 160 % Banner und Ringkarten untereinander (LARGE_FACTOR am Handy)
        if (DashboardTexte.Hinweis(w.Zustand) is { } hinweis)
        {
            _liste.Children.Add(Schrift.Karte(Schrift.Text(hinweis, "Normal", FontWeight.SemiBold, "Pinsel.orange")));
        }
        _liste.Children.Add(Banner(w.Banner, gross));

        // Heute: Tagesziel und Wiederholung
        var heute = new StackPanel { Spacing = 10 };
        var heuteRechts = new StackPanel { Spacing = 10, Orientation = breit ? Orientation.Horizontal : Orientation.Vertical, HorizontalAlignment = breit ? HorizontalAlignment.Right : HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        if (w.Tagesziel.Sichtbar)
        {
            var ring = VerlaufRing.MitText(w.Tagesziel.Anteil, w.Tagesziel.Erreicht ? "green" : "accent", null, w.Tagesziel.RingText, null, 74, 7);
            SetzeRingGroessen(ring, 14);
            heute.Children.Add(Zeile(ring, Schrift.Pt(w.Tagesziel.Text, 14, FontWeight.Bold), 14));
        }
        var faellig = Schrift.Pt(w.Faellig.Text, 14, FontWeight.Bold);
        faellig.VerticalAlignment = VerticalAlignment.Center;
        heuteRechts.Children.Add(faellig);
        // Wie Python: PC zeigt den Knopf immer, gesperrt ohne faellige Aufgaben
        // (set_enabled(plan.count() > 0)); Handy zeigt ihn nur mit faelligen Aufgaben
        // (visible = plan.count() > 0). Klick auf beiden Geraeten gleich (start_review).
        if (Pc || w.Faellig.Wiederholen)
        {
            var knopf = LernTeile.Knopf(DashboardTexte.Wiederholen, true);
            knopf.IsEnabled = w.Faellig.Wiederholen;
            knopf.Margin = new Thickness(0);
            knopf.Click += (_, _) => _wiederholen();
            heuteRechts.Children.Add(new WrapPanel { Children = { knopf } });
        }
        Control heuteInhalt = heute;
        if (breit)
        {
            var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 14 };
            zeile.Children.Add(heute);
            Grid.SetColumn(heuteRechts, 1);
            zeile.Children.Add(heuteRechts);
            heuteInhalt = zeile;
        }
        else
        {
            heute.Children.Add(heuteRechts);
        }
        _liste.Children.Add(Schrift.KopfKarte(DashboardTexte.HeuteTitel, DashboardTexte.HeuteUnter, heuteInhalt, "Pinsel.green"));

        // Vier Ringkarten, je zwei nebeneinander (ab 160 % untereinander)
        var jeRing = breit ? w.Kennzahlen.Count : 2;
        Grid? fuenf = null;
        for (var i = 0; i < w.Kennzahlen.Count; i += jeRing)
        {
            var reihe = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
            fuenf = reihe;
            for (var j = i; j < Math.Min(i + jeRing, w.Kennzahlen.Count); j++)
            {
                var k = w.Kennzahlen[j];
                var (a, b) = RingFarben[j % RingFarben.Length];
                var ring = VerlaufRing.MitText(k.Anteil, a, b, k.Gross, k.Unter, 112, 11);
                var karte = Schrift.KopfKarte(k.Titel, null, new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = ring,
                });
                karte.Padding = new Thickness(14);
                // ring_fit (Nacharbeit M1): Rand seitlich 14, hoechstens bis 6 enger, damit Ring und Titel
                // in die Karte passen (bei 384 Punkten: Normal und Gross 14, Sehr gross 11)
                karte.SizeChanged += (_, e) =>
                {
                    var innen = Math.Floor(e.NewSize.Width) - 2;
                    var ringGroesse = Math.Max(40, Math.Min(Math.Round(112 * Schrift.Faktor), innen - 2 * 6));
                    var seite = Math.Min(14, Math.Max(6, Math.Floor((innen - ringGroesse) / 2)));
                    if (Math.Abs(karte.Padding.Left - seite) > 0.1)
                    {
                        karte.Padding = new Thickness(seite, 14);
                    }
                };
                if (gross)
                {
                    reihe.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    Grid.SetRow(karte, j - i);
                }
                else
                {
                    reihe.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                    Grid.SetColumn(karte, j - i);
                }
                reihe.Children.Add(karte);
            }
            _liste.Children.Add(reihe);
        }

        // Erfolgsquote
        var quote = Schrift.KopfKarte(DashboardTexte.QuoteTitel, DashboardTexte.QuoteUntertitel, new StackPanel
        {
            Spacing = 10,
            Children = { Schrift.Pt(w.Quote.Text, 34, FontWeight.Bold, "Pinsel.accent"), Schrift.Pt(w.Quote.Unter, 13, FontWeight.Normal, "Pinsel.muted") },
        }, "Pinsel.accent2", oben: breit);
        if (breit && fuenf is not null)
        {
            fuenf.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Grid.SetColumn(quote, fuenf.ColumnDefinitions.Count - 1);
            fuenf.Children.Add(quote);
        }
        else
        {
            _liste.Children.Add(quote);
        }

        // Lernverlauf (14 Tage) als Liniendiagramm mit Flaeche und Legende
        var diagramm = new LinienDiagramm(200, "accent");
        diagramm.Daten(w.Verlauf.Select(t => t.Beschriftung).ToList(), DashboardTexte.VerlaufSerie, w.Verlauf.Select(t => (double)t.Anzahl).ToList());
        Avalonia.Automation.AutomationProperties.SetName(diagramm, string.Join(", ", w.Verlauf.Select(t => t.Beschriftung + " " + t.Anzahl)));
        var verlauf = Schrift.KopfKarte(DashboardTexte.VerlaufTitel, Beschriftungen.Einsetzen(DashboardTexte.VerlaufUnter, DashboardWerte.Tage), diagramm);

        // Abdeckung: Verlaufsbalken je Bereich
        var abdeckung = new StackPanel { Spacing = 14 };
        for (var i = 0; i < w.Kennzahlen.Count; i++)
        {
            var k = w.Kennzahlen[i];
            var (a, b) = RingFarben[i % RingFarben.Length];
            abdeckung.Children.Add(Balken(k.Titel, k.BalkenText, k.BalkenProzent, a, b));
        }
        var abdeckungKarte = Schrift.KopfKarte(DashboardTexte.AbdeckungTitel, DashboardTexte.AbdeckungUnter, abdeckung, "Pinsel.purple");
        if (breit)
        {
            _liste.Children.Add(Paar(verlauf, abdeckungKarte, 3, 2));
        }
        else
        {
            _liste.Children.Add(verlauf);
            _liste.Children.Add(abdeckungKarte);
        }

        // Fortschritt je Fachbereich: Ringe 3 + 2 in den Fachbereichsfarben; ab 160 % je Reihe zwei
        // (bei drei Spalten passten "Netzwerk" und "Sicherheit" nicht mehr in ihre Spalte)
        var fach = new StackPanel { Spacing = 16 };
        var jeReihe = breit ? 5 : gross ? 2 : 3;
        for (var i = 0; i < w.Fachbereiche.Count; i += jeReihe)
        {
            var reihe = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", Math.Min(jeReihe, w.Fachbereiche.Count - i)))) };
            for (var j = i; j < Math.Min(i + jeReihe, w.Fachbereiche.Count); j++)
            {
                var f = w.Fachbereiche[j];
                var farbe = ListenTeil.Farben.TryGetValue(f.Name, out var pinsel) ? pinsel["Pinsel.".Length..] : "accent";
                var ring = VerlaufRing.MitText(f.Abdeckung / 100.0, farbe, null, Geraetetexte.Ring(f, Pc), null, 74, 7);
                SetzeRingGroessen(ring, 14);
                ring.HorizontalAlignment = HorizontalAlignment.Center;
                var name = Schrift.Pt(f.Kurz, 13, FontWeight.Bold);
                var detail = Schrift.Pt(f.Detail, 11, FontWeight.Normal, "Pinsel.muted");
                name.TextAlignment = detail.TextAlignment = TextAlignment.Center;
                var feld = new StackPanel { Spacing = 4, Children = { ring, name, detail } };
                Grid.SetColumn(feld, j - i);
                reihe.Children.Add(feld);
            }
            fach.Children.Add(reihe);
        }
        // Untertitel "Auswahl zeigt die Themen" erst mit dem Reinzoom (Weisung P1-2 3.4: nichts zusagen, was fehlt)
        _liste.Children.Add(Schrift.KopfKarte(DashboardTexte.FachTitel, null, fach, "Pinsel.green"));

        // Aktivitaeten als Kacheln mit Punkt in der Farbe der Art
        var aktiv = new StackPanel { Spacing = 6 };
        if (w.Aktivitaeten.Count == 0)
        {
            aktiv.Children.Add(Schrift.Pt(Geraetetexte.AktivitaetenLeer(Pc), 13, FontWeight.Normal, "Pinsel.muted"));
        }
        foreach (var a in w.Aktivitaeten)
        {
            var zeile1 = Schrift.Pt(Geraetetexte.AktivitaetZeile1(a, Pc), 13, FontWeight.Normal, "Pinsel.text_dim");
            zeile1.TextWrapping = TextWrapping.NoWrap;
            zeile1.TextTrimming = TextTrimming.CharacterEllipsis;
            var punkt = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), VerticalAlignment = VerticalAlignment.Center }
                .Farbe(Border.BackgroundProperty, ArtFarbe(a.Art));
            var texte = new StackPanel { Spacing = 1, Children = { zeile1, Schrift.Pt(Geraetetexte.AktivitaetZeile2(a, Pc), 11, FontWeight.Normal, "Pinsel.muted") } };
            aktiv.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8),
                Child = Zeile(punkt, texte, 10),
            }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt"));
        }
        var aktivKarte = Schrift.KopfKarte(DashboardTexte.AktivitaetenTitel, DashboardTexte.AktivitaetenUnter, aktiv);

        // Lerntage (Monat) mit Blaettern
        Kalender = new LerntageKalender(w.Kalender);
        var kalenderKarte = Schrift.KopfKarte(DashboardTexte.KalenderTitel, DashboardTexte.KalenderUnter, Kalender, "Pinsel.purple");
        if (breit)
        {
            _liste.Children.Add(Paar(aktivKarte, kalenderKarte));
        }
        else
        {
            _liste.Children.Add(aktivKarte);
            _liste.Children.Add(kalenderKarte);
        }
        var themenKarten = new List<Control>();

        // AP1 und AP2 Pruefungsthemen: je Thema Ring links, Name rechts; breit wie Python am PC (ThemeTimeline):
        // Ringe in einer Reihe, darunter Achse mit Punkt und Name (Nico 5a)
        foreach (var (titel, unter, themen, akzent) in (ReadOnlySpan<(string, string, IReadOnlyList<DashboardThema>, string)>)
            [(DashboardTexte.Ap1Titel, DashboardTexte.Ap1Unter, w.Ap1Themen, "Pinsel.blue"), (DashboardTexte.Ap2Titel, DashboardTexte.Ap2Unter, w.Ap2Themen, "Pinsel.accent2")])
        {
            if (breit)
            {
                themenKarten.Add(Schrift.KopfKarte(titel, unter, ThemenReihe(themen), akzent));
                continue;
            }
            var feld = new StackPanel { Spacing = 8 };
            for (var i = 0; i < themen.Count; i++)
            {
                var t = themen[i];
                var farbe = ThemenFarben[i % ThemenFarben.Length];
                var ring = VerlaufRing.MitText(t.Prozent / 100.0, farbe, FarbenAnwenden.MischName(farbe, "text", 0.25), t.RingText, null, 46, 5);
                SetzeRingGroessen(ring, 11);
                feld.Children.Add(Zeile(ring, Schrift.Pt(t.Name, 13, FontWeight.Normal, "Pinsel.text_dim"), 14));
            }
            themenKarten.Add(Schrift.KopfKarte(titel, unter, feld, akzent));
        }
        if (breit)
        {
            _liste.Children.Add(Paar(themenKarten[0], themenKarten[1]));
        }
        else
        {
            foreach (var k in themenKarten)
            {
                _liste.Children.Add(k);
            }
        }
    }

    /// <summary>
    /// ThemeTimeline (Python am PC): je Thema eine Spalte mit Ring (Prozent in der Mitte), Achse mit Punkt (gefuellt ab
    /// 1 %) und Name darunter, zentriert und umbrochen.
    /// </summary>
    private static Grid ThemenReihe(IReadOnlyList<DashboardThema> themen)
    {
        var reihe = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", Math.Max(1, themen.Count)))) };
        for (var i = 0; i < themen.Count; i++)
        {
            var t = themen[i];
            var farbe = ThemenFarben[i % ThemenFarben.Length];
            var ring = VerlaufRing.MitText(t.Prozent / 100.0, farbe, FarbenAnwenden.MischName(farbe, "text", 0.25), t.RingText, null, 64, 6);
            SetzeRingGroessen(ring, 11);
            ring.HorizontalAlignment = HorizontalAlignment.Center;
            // Achse: links bis zur Mitte (ausser beim ersten), rechts ab der Mitte (ausser beim letzten)
            var achse = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Height = 14 };
            if (i > 0)
            {
                achse.Children.Add(new Border { Height = 2, VerticalAlignment = VerticalAlignment.Center }.Farbe(Border.BackgroundProperty, "Pinsel.border_hi"));
            }
            if (i < themen.Count - 1)
            {
                var rechts = new Border { Height = 2, VerticalAlignment = VerticalAlignment.Center }.Farbe(Border.BackgroundProperty, "Pinsel.border_hi");
                Grid.SetColumn(rechts, 1);
                achse.Children.Add(rechts);
            }
            var punkt = new Ellipse
            {
                Width = t.Prozent > 0 ? 12 : 10,
                Height = t.Prozent > 0 ? 12 : 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                StrokeThickness = 2,
            }.Farbe(Shape.FillProperty, t.Prozent > 0 ? "Pinsel." + farbe : "Pinsel.ring_bg").Farbe(Shape.StrokeProperty, "Pinsel.card");
            Grid.SetColumnSpan(punkt, 2);
            achse.Children.Add(punkt);
            var name = Schrift.Pt(t.Name, 11, FontWeight.Normal, "Pinsel.text_dim");
            name.TextWrapping = TextWrapping.Wrap;
            name.TextAlignment = TextAlignment.Center;
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(4, 0);
            var spalte = new StackPanel { Spacing = 8, Children = { ring, achse, name } };
            Grid.SetColumn(spalte, i);
            reihe.Children.Add(spalte);
        }
        return reihe;
    }

    /// <summary>HeroPanel: Titel 22 fett, Zeile 13, rechts grosse Zahl 30 mit Unterzeile 12; ab 160 % untereinander.</summary>
    private static Border Banner(DashboardBanner banner, bool gross)
    {
        var hell = FarbenAnwenden.Mischung("on_accent", "accent2", 0.25);
        var texte = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Schrift.Pt(banner.Titel, 22, FontWeight.Bold, "Pinsel.on_accent"),
                Schrift.Pt(Geraetetexte.Banner(banner, Pc), 13, FontWeight.Normal, hell),
            },
        };
        var zahl = Schrift.Pt(banner.Gross, 30, FontWeight.Bold, "Pinsel.on_accent");
        var unter = Schrift.Pt(banner.GrossUnter, 12, FontWeight.Normal, hell);
        var zahlen = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { zahl, unter } };
        zahl.TextAlignment = unter.TextAlignment = gross ? TextAlignment.Left : TextAlignment.Right;
        zahlen.HorizontalAlignment = gross ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        Control inhalt;
        if (gross)
        {
            inhalt = new StackPanel { Spacing = 12, Children = { texte, zahlen } };
        }
        else
        {
            var raster = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
            Grid.SetColumn(zahlen, 1);
            raster.Children.Add(texte);
            raster.Children.Add(zahlen);
            inhalt = raster;
        }
        return new Border { CornerRadius = new CornerRadius(18), Padding = new Thickness(20), Child = inhalt }
            .Farbe(Border.BackgroundProperty, "Verlauf.hero");
    }

    /// <summary>GradientBar: Name links (13 text_dim), Wert rechts (12 muted), darunter Balken 8 mit Verlauf.</summary>
    private static Control Balken(string name, string wert, double prozent, string a, string b)
    {
        var anteil = (int)Math.Round(Math.Max(0, Math.Min(100, prozent)) * 10);
        var kopf = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        var wertText = Schrift.Pt(wert, 12, FontWeight.Normal, "Pinsel.muted");
        Grid.SetColumn(wertText, 1);
        kopf.Children.Add(Schrift.Pt(name, 13, FontWeight.Normal, "Pinsel.text_dim"));
        kopf.Children.Add(wertText);
        var spur = new Grid { Height = 8, ColumnDefinitions = new ColumnDefinitions($"{anteil}*,{1000 - anteil}*") };
        var hinten = new Border { CornerRadius = new CornerRadius(4) }.Farbe(Border.BackgroundProperty, "Pinsel.ring_bg");
        Grid.SetColumnSpan(hinten, 2);
        spur.Children.Add(hinten);
        if (anteil > 0)
        {
            spur.Children.Add(new Border { CornerRadius = new CornerRadius(4) }.Farbe(Border.BackgroundProperty, FarbenAnwenden.Paar(a, b)));
        }
        var feld = new StackPanel { Spacing = 6, Children = { kopf, spur } };
        Avalonia.Automation.AutomationProperties.SetName(feld, name + " " + wert);
        return feld;
    }

    /// <summary>Zahl in der Mitte eines kleinen Rings: big_size wie am Handy.</summary>
    private static void SetzeRingGroessen(Grid ring, double gross)
    {
        if (ring.Children[1] is StackPanel texte && texte.Children[0] is TextBlock zahl)
        {
            zahl.Bind(Avalonia.Controls.Documents.TextElement.FontSizeProperty, zahl.GetResourceObservable("Schrift.Normal", v => v is double d ? gross * d / Schrift.Grundgroessen["Normal"] : gross));
        }
    }

    private static Control Zeile(Control links, Control rechts, double abstand)
    {
        rechts.VerticalAlignment = VerticalAlignment.Center;
        links.VerticalAlignment = VerticalAlignment.Center;
        var raster = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = abstand };
        Grid.SetColumn(rechts, 1);
        raster.Children.Add(links);
        raster.Children.Add(rechts);
        return raster;
    }
}
