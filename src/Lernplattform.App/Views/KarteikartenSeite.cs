using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L4: Karteikarten wie app_gui.CardsView (PC-Wortlaut, PC und Handy gleich).
/// Ablauf und Speichern in Core.Lernen.Karteikarten; die Seite zeigt nur an. Lernstand
/// und Reihenfolge werden im Hintergrund gelesen, die Seite erscheint sofort.
/// </summary>
public sealed class KarteikartenSeite : UserControl
{
    private readonly StackPanel _liste = new() { Spacing = 14, Margin = new Thickness(16) };
    private readonly List<Action<Karteikarten>> _wartend = [];
    private Karteikarten? _k;

    private Pillen _bereich = null!;
    private Pillen _status = null!;
    private Pillen _modus = null!;
    private ComboBox _themen = null!;
    private readonly List<string?> _themenIds = [];
    private bool _themenSetzen;
    private TextBlock _zaehltext = null!;
    private TextBlock _untertitel = null!;
    private TextBlock _statuszeile = null!;
    private TextBlock _frage = null!;
    private StackPanel _freitext = null!;
    private TextBox _eingabe = null!;
    private StackPanel _optionen = null!;
    private TextBlock _aufdecken = null!;
    private TextBlock _rueckmeldung = null!;
    private TextBlock _loesung = null!;
    private WrapPanel _bewerten = null!;
    private Button _pruefen = null!;
    private TextBlock _zaehler = null!;

    private readonly System.Diagnostics.Stopwatch _uhr = System.Diagnostics.Stopwatch.StartNew();

    public KarteikartenSeite()
    {
        _liste.Children.Add(Schrift.Text(FarbenTexte.Laden, "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        Content = new ScrollViewer { Content = _liste };
        Geladen = LadenAsync();
        _beimBeenden = () => _k?.Beenden();
        Zustand.BeimBeenden += _beimBeenden;
    }

    private readonly Action _beimBeenden;

    /// <summary>Phase 9 W2: vor dem Verwerfen der Seite (Wechsel der Fachrichtung) speichern und abmelden.</summary>
    public void Abmelden()
    {
        _k?.Beenden();
        Zustand.BeimBeenden -= _beimBeenden;
    }

    /// <summary>Fertig aufgebaut (Lernstand gelesen, erste Karte gezeigt).</summary>
    public Task Geladen { get; }

    /// <summary>Zeit vom Bauen der Seite bis die erste Karte steht (Messwert).</summary>
    public double? InhaltMs { get; private set; }

    private async Task LadenAsync()
    {
        try
        {
            var inhalte = await Zustand.Inhalte;
            if (inhalte is null)
            {
                return;
            }
            var gewicht = await Zustand.Gewicht;
            var karten = await Task.Run(() => new Karteikarten(inhalte, Zustand.BuchLesen, Zustand.Schreiber,
                () => Zustand.NeuReihenfolge(gewicht, Quellen.Karte)));
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _k = karten;
                Aufbauen(inhalte);
                karten.NachSpeichern += () => Nachher();
                karten.NachSpeichern += AbgleichDienst.NachDemLernen;     // L10: bald abgleichen (schedule)
                foreach (var schritt in _wartend)
                {
                    schritt(karten);
                }
                _wartend.Clear();
                Anzeigen();
                InhaltMs = _uhr.Elapsed.TotalMilliseconds;
            });
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }

    private void Mit(Action<Karteikarten> schritt)
    {
        if (_k is { } k)
        {
            schritt(k);
            Anzeigen();
        }
        else
        {
            _wartend.Add(schritt);
        }
    }

    /// <summary>Fachbereich aus der Seitenleiste (open_cards).</summary>
    public void SetzeFachbereich(string fachbereich, string? thema = null) => Mit(k => k.SetzeFachbereich(fachbereich, thema));

    /// <summary>Gezielte Runde (Jetzt wiederholen).</summary>
    public void Ueben(IEnumerable<string> fragen)
    {
        var liste = fragen.ToList();
        Mit(k => k.Ueben(liste));
    }

    /// <summary>Suche: zur Karte springen (jump_to_question).</summary>
    public void Springen(string frage) => Mit(k => k.Springen(frage));

    /// <summary>on_show: neue Antworten (z. B. nach einer Uebernahme) im Status beruecksichtigen.</summary>
    public void Gezeigt()
    {
        if (_k is { } k)
        {
            k.NeuLesen();
            if (k.Karte is not null)
            {
                StatusZeigen(k);
            }
        }
    }

    private void Aufbauen(Lerninhalte inhalte)
    {
        _liste.Children.Clear();
        _bereich = new Pillen(Fachbereiche.Alle.Select(f => (f, Fachbereiche.Kurzname[f])).Prepend((KartenTexte.Alle, KartenTexte.Alle)),
            KartenTexte.Alle, f => Mit(k => k.SetzeFachbereich(f)));
        _status = new Pillen(KartenTexte.StatusReiter, FragenStatus.Alle, s => Mit(k => k.SetzeStatus(s)));
        _modus = new Pillen(KartenTexte.Modi, KartenTexte.Freitext, m => Mit(k => k.SetzeModus(m)));
        _themen = LernTeile.Themenliste();
        _themen.SelectionChanged += (_, _) =>
        {
            if (!_themenSetzen && _themen.SelectedIndex >= 0)
            {
                var thema = _themenIds[_themen.SelectedIndex] ?? KartenTexte.Alle;
                Mit(k => k.SetzeThema(thema));
            }
        };
        _zaehltext = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        _liste.Children.Add(Schrift.Karte(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                LernTeile.Label(KartenTexte.Fachbereich), _bereich,
                LernTeile.Label(KartenTexte.Status), _status,
                LernTeile.Label(KartenTexte.Thema), _themen,
                _zaehltext,
                LernTeile.Label(KartenTexte.Lernmodus), _modus,
            },
        }));

        _untertitel = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.accent");
        _statuszeile = Schrift.Text("", "Klein", FontWeight.Bold, "Pinsel.muted");
        // Fix A 0.14.2 (Weisung P8-9): LernText, damit ein sehr langes Wort in schmaler Ansicht bei grosser Schrift ganz bleibt
        _frage = new LernText("", "Gross", FontWeight.SemiBold, "Pinsel.text_soft");
        _liste.Children.Add(Schrift.Karte(new StackPanel
        {
            Spacing = 6,
            Children = { Schrift.Text(KartenTexte.FrageTitel, "Mittel", FontWeight.Bold), _untertitel, _statuszeile, _frage },
        }));

        _eingabe = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110 }
            .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt");
        Avalonia.Automation.AutomationProperties.SetName(_eingabe, KartenTexte.AntwortTitel);
        // Strg+Eingabe prueft direkt aus dem Textfeld (wie Python ab 0.56)
        _eingabe.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                Pruefen();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _freitext = new StackPanel { Spacing = 8, Children = { Schrift.Text(KartenTexte.FreitextHinweis, "Normal", FontWeight.Normal, "Pinsel.text_dim"), _eingabe } };
        _optionen = new StackPanel { Spacing = 6 };
        _aufdecken = Schrift.Text(KartenTexte.AufdeckenHinweis, "Normal", FontWeight.Normal, "Pinsel.text_dim");
        _rueckmeldung = new LernText("", "Normal", FontWeight.Bold, "Pinsel.text");
        _loesung = new LernText("", "Normal", FontWeight.Normal, "Pinsel.text_dim");
        var gewusst = LernTeile.Knopf(KartenTexte.Gewusst, true);
        gewusst.Click += (_, _) => Bewerten(true);
        var nichtGewusst = LernTeile.Knopf(KartenTexte.NichtGewusst);
        nichtGewusst.Click += (_, _) => Bewerten(false);
        var wusstest = Schrift.Text(KartenTexte.Wusstest, "Normal", FontWeight.Normal, "Pinsel.text_dim");
        wusstest.VerticalAlignment = VerticalAlignment.Center;
        wusstest.Margin = new Thickness(0, 0, 12, 8);
        _bewerten = new WrapPanel { IsVisible = false, Children = { wusstest, gewusst, nichtGewusst } };
        _liste.Children.Add(Schrift.Karte(new StackPanel
        {
            Spacing = 8,
            Children = { Schrift.Text(KartenTexte.AntwortTitel, "Mittel", FontWeight.Bold), _freitext, _optionen, _aufdecken, _rueckmeldung, _loesung, _bewerten },
        }));

        var zurueck = LernTeile.Knopf(KartenTexte.Zurueck);
        zurueck.Click += (_, _) => Mit(k => k.Zurueck());
        _pruefen = LernTeile.Knopf(KartenTexte.Pruefen, true);
        _pruefen.Click += (_, _) => Pruefen();
        var weiter = LernTeile.Knopf(KartenTexte.Naechste);
        weiter.Click += (_, _) => Mit(k => k.Weiter());
        _zaehler = Schrift.Text("", "Normal", FontWeight.Bold, "Pinsel.text_dim");
        _zaehler.VerticalAlignment = VerticalAlignment.Center;
        _zaehler.Margin = new Thickness(4, 0, 0, 8);
        _liste.Children.Add(new WrapPanel { Children = { zurueck, _pruefen, weiter, _zaehler } });
    }

    /// <summary>update_ui: Filter, Karte und Eingabebereich neu zeigen (Rueckmeldung leer).</summary>
    private void Anzeigen()
    {
        if (_k is not { } k)
        {
            return;
        }
        _bereich.Waehle(k.Fachbereich);
        _status.Waehle(k.Status);
        _modus.Waehle(k.Modus);
        ThemenZeigen(k);
        _zaehltext.Text = k.ZaehlText;
        _rueckmeldung.Text = "";
        _loesung.Text = "";
        _bewerten.IsVisible = false;
        _optionen.Children.Clear();
        _freitext.IsVisible = false;
        _optionen.IsVisible = false;
        _aufdecken.IsVisible = false;
        _zaehler.Text = k.Zaehler;
        if (k.Karte is not { } karte)
        {
            _frage.Text = KartenTexte.Leer;
            _untertitel.Text = "";
            _statuszeile.Text = "";
            _pruefen.IsEnabled = false;
            return;
        }
        _pruefen.IsEnabled = true;
        _frage.Text = karte.Frage;
        _untertitel.Text = k.Untertitel;
        StatusZeigen(k);
        ((TextBlock)_pruefen.Content!).Text = k.PruefKnopf;
        if (k.Modus == KartenTexte.Freitext)
        {
            _freitext.IsVisible = true;
            _eingabe.Text = "";
        }
        else if (k.Modus == KartenTexte.Mehrfachwahl)
        {
            _optionen.IsVisible = true;
            foreach (var option in k.Optionen)
            {
                _optionen.Children.Add(LernTeile.Option(option, "karteikarte"));
            }
        }
        else
        {
            _aufdecken.IsVisible = true;
        }
    }

    private void ThemenZeigen(Karteikarten k)
    {
        _themenSetzen = true;
        var themen = k.Themen;
        var namen = themen.Select(t => t.Name).Prepend(KartenTexte.Alle).ToList();
        if (_themenIds.Count != namen.Count || !_themenIds.Skip(1).SequenceEqual(themen.Select(t => (string?)t.Id)))
        {
            _themenIds.Clear();
            _themenIds.Add(null);
            _themenIds.AddRange(themen.Select(t => (string?)t.Id));
            _themen.ItemsSource = namen;
        }
        _themen.SelectedIndex = Math.Max(0, _themenIds.IndexOf(k.Thema == KartenTexte.Alle ? null : k.Thema));
        _themenSetzen = false;
    }

    private void StatusZeigen(Karteikarten k)
    {
        var (text, farbe) = k.StatusZeile;
        _statuszeile.Text = text;
        _statuszeile.Farbe(TextBlock.ForegroundProperty, LernTeile.Pinsel(farbe));
    }

    private void Pruefen()
    {
        if (_k is not { } k)
        {
            return;
        }
        var auswahl = LernTeile.Gewaehlt(_optionen);
        if (k.Pruefen(_eingabe.Text, auswahl) is not { } antwort)
        {
            return;
        }
        if (antwort.Warnung is { } warnung)
        {
            _ = Dialog.Zeigen(KartenTexte.HinweisTitel, warnung, Dialog.Ok);
            return;
        }
        _rueckmeldung.Text = antwort.Text;
        _rueckmeldung.Farbe(TextBlock.ForegroundProperty, LernTeile.Pinsel(antwort.Farbe));
        _loesung.Text = antwort.Loesung;
        if (k.Modus == KartenTexte.Mehrfachwahl)
        {
            LernTeile.Aufdecken(_optionen, k.Karte?.Antwort);
        }
        _bewerten.IsVisible = antwort.Bewerten;
    }

    private void Bewerten(bool richtig)
    {
        _bewerten.IsVisible = false;
        _k?.Bewerten(richtig);
    }

    /// <summary>
    /// Nur fuer den Start ohne Bedienung: Ansicht nach dem Pruefen je Modus (0 Freitext,
    /// 1 Multiple Choice, 2 Aufdecken mit "Wusstest du es?") mit dem laengsten Themennamen,
    /// ohne etwas zu speichern. -1 = zurueck auf "Alle" und Freitext.
    /// </summary>
    internal void Probe(int art, string? frage = null)
    {
        if (_k is not { } k)
        {
            return;
        }
        if (art < 0)
        {
            k.SetzeModus(KartenTexte.Freitext);
            k.SetzeFachbereich(KartenTexte.Alle);
            Anzeigen();
            return;
        }
        var lang = k.Themen.MaxBy(t => t.Name.Length);
        if (frage is not null)
        {
            // Fix A 0.14.2: eine bestimmte Karte (Start-Test: laengstes Wort je Fachbereich)
            k.Springen(frage);
        }
        else if (lang is not null && k.Thema != lang.Id)
        {
            k.SetzeThema(lang.Id);
        }
        k.SetzeModus(KartenTexte.Modi[art].Wert);
        Anzeigen();
        if (k.Karte is not { } karte)
        {
            return;
        }
        var voll = karte.Text("a_full") ?? "";
        (_rueckmeldung.Text, _loesung.Text) = art switch
        {
            0 => (KartenTexte.FreitextVergleich, KartenTexte.MusterloesungVor + voll),
            1 => (KartenTexte.McFalschVor + karte.Antwort, voll),
            _ => (KartenTexte.Musterloesung, voll),
        };
        _rueckmeldung.Farbe(TextBlock.ForegroundProperty, LernTeile.Pinsel(art == 0 ? "yellow" : art == 1 ? "red" : "accent"));
        if (art == 1 && _optionen.Children.OfType<Border>().Select(b => b.Child).OfType<RadioButton>().FirstOrDefault(r => (string?)r.Tag != karte.Antwort) is { } falsch)
        {
            falsch.IsChecked = true;
            LernTeile.Aufdecken(_optionen, karte.Antwort);
        }
        _bewerten.IsVisible = art == 2;
    }

    // Nach dem Speichern: Status und Zaehltext neu (das Buch ist neu gelesen)
    private void Nachher()
    {
        if (_k is { } k)
        {
            _zaehltext.Text = k.ZaehlText;
            if (k.Karte is not null)
            {
                StatusZeigen(k);
            }
        }
    }
}
