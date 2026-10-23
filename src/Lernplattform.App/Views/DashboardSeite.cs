using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
    private readonly StackPanel _liste = new() { Spacing = 14, Margin = new Thickness(16) };

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

    private void Zeigen(DashboardWerte w)
    {
        if (DashboardTexte.Hinweis(w.Zustand) is { } hinweis)
        {
            _liste.Children.Add(Schrift.Karte(Schrift.Text(hinweis, "Normal", FontWeight.SemiBold, "Pinsel.orange")));
        }
        // Banner
        var banner = new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    Schrift.Text(w.Banner.Titel, "Banner", FontWeight.Bold, "Pinsel.on_accent"),
                    Schrift.Text(Geraetetexte.Banner(w.Banner, Pc), "Normal", FontWeight.Normal, "Pinsel.on_accent"),
                    Schrift.Text(w.Banner.Gross, "Zahl", FontWeight.Bold, "Pinsel.on_accent"),
                    Schrift.Text(w.Banner.GrossUnter, "Klein", FontWeight.Normal, "Pinsel.on_accent"),
                },
            },
        }.Farbe(Border.BackgroundProperty, "Verlauf.hero");
        _liste.Children.Add(banner);

        // Heute: Tagesziel und Wiederholung
        var heute = new StackPanel { Spacing = 8 };
        if (w.Tagesziel.Sichtbar)
        {
            heute.Children.Add(Zeile(Ring(w.Tagesziel.Anteil, w.Tagesziel.RingText, "Pinsel.green"), Schrift.Text(w.Tagesziel.Text)));
        }
        heute.Children.Add(Schrift.Text(w.Faellig.Text));
        // Wie Python: PC zeigt den Knopf immer, gesperrt ohne faellige Aufgaben
        // (set_enabled(plan.count() > 0)); Handy zeigt ihn nur mit faelligen Aufgaben
        // (visible = plan.count() > 0). Klick auf beiden Geraeten gleich (start_review).
        if (Pc || w.Faellig.Wiederholen)
        {
            var knopf = LernTeile.Knopf(DashboardTexte.Wiederholen, true);
            knopf.IsEnabled = w.Faellig.Wiederholen;
            knopf.Click += (_, _) => _wiederholen();
            heute.Children.Add(new WrapPanel { Children = { knopf } });
        }
        _liste.Children.Add(Karte(DashboardTexte.HeuteTitel, DashboardTexte.HeuteUnter, heute));

        // Vier Ringe: Karteikarten, Pruefungstrainer, AP1, AP2
        var ringe = new WrapPanel();
        foreach (var k in w.Kennzahlen)
        {
            ringe.Children.Add(Schrift.Karte(new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    Schrift.Text(k.Titel, "Normal", FontWeight.SemiBold),
                    Ring(k.Anteil, k.Gross, "Pinsel.accent"),
                    Schrift.Text(k.Unter, "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                },
            }.Breite(150)));
            ((Border)ringe.Children[^1]).Margin = new Thickness(0, 0, 10, 10);
        }
        _liste.Children.Add(ringe);

        // Erfolgsquote
        _liste.Children.Add(Karte(DashboardTexte.QuoteTitel, DashboardTexte.QuoteUntertitel, new StackPanel
        {
            Spacing = 4,
            Children = { Schrift.Text(w.Quote.Text, "Titel", FontWeight.Bold), Schrift.Text(w.Quote.Unter, "Klein", FontWeight.Normal, "Pinsel.text_dim") },
        }));

        // Lernverlauf (14 Tage)
        var hoechst = Math.Max(1, w.Verlauf.Max(t => t.Anzahl));
        var balken = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", w.Verlauf.Select(_ => "*"))), Height = 140 };
        for (var i = 0; i < w.Verlauf.Count; i++)
        {
            var tag = w.Verlauf[i];
            var saeule = new Border
            {
                Height = 100.0 * tag.Anzahl / hoechst,
                Margin = new Thickness(2, 0),
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Bottom,
            }.Farbe(Border.BackgroundProperty, "Pinsel.accent");
            ToolTip.SetTip(saeule, tag.Beschriftung + ": " + tag.Anzahl);
            var spalte = new DockPanel { LastChildFill = true };
            var datum = new TextBlock { Text = i % 2 == 0 ? tag.Beschriftung[..2] : "", HorizontalAlignment = HorizontalAlignment.Center }
                .Groesse("Klein").Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
            DockPanel.SetDock(datum, Dock.Bottom);
            spalte.Children.Add(datum);
            spalte.Children.Add(saeule);
            Grid.SetColumn(spalte, i);
            balken.Children.Add(spalte);
        }
        _liste.Children.Add(Karte(DashboardTexte.VerlaufTitel, Beschriftungen.Einsetzen(DashboardTexte.VerlaufUnter, DashboardWerte.Tage), balken));

        // Abdeckung
        var abdeckung = new StackPanel { Spacing = 8 };
        foreach (var k in w.Kennzahlen)
        {
            abdeckung.Children.Add(Schrift.Text(k.Titel + "  " + k.BalkenText, "Klein"));
            abdeckung.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = k.BalkenProzent, Height = 8 }
                .Farbe(ProgressBar.ForegroundProperty, "Pinsel.accent").Farbe(ProgressBar.BackgroundProperty, "Pinsel.ring_bg"));
        }
        _liste.Children.Add(Karte(DashboardTexte.AbdeckungTitel, DashboardTexte.AbdeckungUnter, abdeckung));

        // Fortschritt je Fachbereich
        var fach = new WrapPanel();
        foreach (var f in w.Fachbereiche)
        {
            var feld = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(0, 0, 10, 10),
                Children =
                {
                    Ring(f.Abdeckung / 100.0, Geraetetexte.Ring(f, Pc), "Pinsel.accent2"),
                    Schrift.Text(f.Kurz, "Normal", FontWeight.SemiBold),
                    Schrift.Text(f.Detail, "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                },
            }.Breite(140);
            fach.Children.Add(feld);
        }
        // Untertitel "Auswahl zeigt die Themen" erst mit dem Reinzoom (Weisung P1-2 3.4: nichts zusagen, was fehlt)
        _liste.Children.Add(Karte(DashboardTexte.FachTitel, null, fach));

        // Aktivitaeten
        var aktiv = new StackPanel { Spacing = 8 };
        if (w.Aktivitaeten.Count == 0)
        {
            aktiv.Children.Add(Schrift.Text(Geraetetexte.AktivitaetenLeer(Pc), "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        }
        foreach (var a in w.Aktivitaeten)
        {
            aktiv.Children.Add(new StackPanel
            {
                Children =
                {
                    Schrift.Text(Geraetetexte.AktivitaetZeile1(a, Pc)),
                    Schrift.Text(Geraetetexte.AktivitaetZeile2(a, Pc), "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                },
            });
        }
        _liste.Children.Add(Karte(DashboardTexte.AktivitaetenTitel, DashboardTexte.AktivitaetenUnter, aktiv));

        // Lerntage (Monat)
        var kalender = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*") };
        var zeile = 0;
        kalender.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        string[] wochentage = ["M", "D", "M", "D", "F", "S", "S"];
        for (var i = 0; i < 7; i++)
        {
            var kopf = new TextBlock { Text = wochentage[i], HorizontalAlignment = HorizontalAlignment.Center }
                .Groesse("Klein").Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
            Grid.SetColumn(kopf, i);
            kalender.Children.Add(kopf);
        }
        foreach (var woche in w.Kalender.Wochen)
        {
            zeile++;
            kalender.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var i = 0; i < woche.Count; i++)
            {
                if (woche[i] == 0)
                {
                    continue;
                }
                var gelernt = w.Kalender.Lerntage.Contains(woche[i]);
                var tag = new Border
                {
                    Margin = new Thickness(2),
                    Padding = new Thickness(0, 4),
                    CornerRadius = new CornerRadius(6),
                    Child = new TextBlock { Text = woche[i].ToString(System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Center }
                        .Groesse("Klein").Farbe(TextBlock.ForegroundProperty, gelernt ? "Pinsel.on_accent" : "Pinsel.text"),
                };
                tag.Farbe(Border.BackgroundProperty, gelernt ? "Pinsel.green" : "Pinsel.card_alt");
                Grid.SetColumn(tag, i);
                Grid.SetRow(tag, zeile);
                kalender.Children.Add(tag);
            }
        }
        _liste.Children.Add(Karte(DashboardTexte.KalenderTitel, DashboardTexte.KalenderUnter,
            new StackPanel { Spacing = 8, Children = { Schrift.Text(w.Kalender.Titel, "Normal", FontWeight.SemiBold), kalender } }));

        // AP1 und AP2 Pruefungsthemen
        foreach (var (titel, unter, themen) in (ReadOnlySpan<(string, string, IReadOnlyList<DashboardThema>)>)
            [(DashboardTexte.Ap1Titel, DashboardTexte.Ap1Unter, w.Ap1Themen), (DashboardTexte.Ap2Titel, DashboardTexte.Ap2Unter, w.Ap2Themen)])
        {
            var feld = new WrapPanel();
            foreach (var t in themen)
            {
                feld.Children.Add(new StackPanel
                {
                    Spacing = 4,
                        Margin = new Thickness(0, 0, 10, 10),
                    Children = { Ring(t.Prozent / 100.0, t.RingText, "Pinsel.purple"), Schrift.Text(t.Name, "Klein") },
                }.Breite(140));
            }
            _liste.Children.Add(Karte(titel, unter, feld));
        }
    }

    private static TextBlock Unter(string? unter)
    {
        var text = Schrift.Text(unter ?? "", "Klein", FontWeight.Normal, "Pinsel.muted");
        text.IsVisible = unter is not null;
        return text;
    }

    private static Border Karte(string titel, string? unter, Control inhalt) =>
        Schrift.Karte(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children = { Schrift.Text(titel, "Mittel", FontWeight.Bold), Unter(unter) },
                },
                inhalt,
            },
        });

    private static Control Zeile(Control links, Control rechts)
    {
        rechts.VerticalAlignment = VerticalAlignment.Center;
        var raster = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        Grid.SetColumn(rechts, 1);
        raster.Children.Add(links);
        raster.Children.Add(rechts);
        return raster;
    }

    /// <summary>Ring mit Anteil 0..1 und Text in der Mitte.</summary>
    private static Control Ring(double anteil, string text, string farbe)
    {
        anteil = Math.Max(0, Math.Min(1, anteil));
        var hinten = new Arc { StartAngle = -90, SweepAngle = 360, StrokeThickness = 8 }.Farbe(Shape.StrokeProperty, "Pinsel.ring_bg");
        var vorne = new Arc { StartAngle = -90, SweepAngle = 360 * anteil, StrokeThickness = 8, StrokeLineCap = PenLineCap.Round }
            .Farbe(Shape.StrokeProperty, farbe);
        var mitte = new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            .Groesse("Klein").Farbe(TextBlock.ForegroundProperty, "Pinsel.text");
        mitte.FontWeight = FontWeight.Bold;
        var feld = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Children = { hinten, vorne, mitte } };
        // Ring waechst mit der Schrift, damit "100 %" hineinpasst
        feld.Bind(Layoutable.WidthProperty, feld.GetResourceObservable("Schrift.Klein", v => v is double d ? 64 * d / 12 : 64));
        feld.Bind(Layoutable.HeightProperty, feld.GetResourceObservable("Schrift.Klein", v => v is double d ? 64 * d / 12 : 64));
        Avalonia.Automation.AutomationProperties.SetName(feld, text);
        return feld;
    }
}
