using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L6: Pruefungstrainer wie app_gui.QuizView (PC-Wortlaut, PC und Handy gleich).
/// Art "Uebung" mit Core.Lernen.Uebung; Art "Prüfung" ist die Klausursimulation (Phase 4 W2,
/// <see cref="KlausurTeil"/>); eine laufende Pruefung geht beim Zeigen vor.
/// Unter dem Ergebnis steht der Hinweis "Übungsprüfung, keine offizielle IHK-Prüfung."
/// (Weisung P2-4).
/// </summary>
public sealed class TrainerSeite : UserControl
{
    private readonly StackPanel _liste = new() { Spacing = 14, Margin = new Thickness(16) };
    private readonly List<Action<Uebung>> _wartend = [];
    private readonly DispatcherTimer _takt = new() { Interval = TimeSpan.FromSeconds(1) };
    private Uebung? _u;
    private Func<Task>? _nachher;
    private Lerninhalte? _inhalte;      // follow_up: zweiter Teil von "Jetzt üben" nach dem Ergebnis
    private readonly Func<string, Task> _themaUeben;
    private KlausurTeil? _klausur;

    private Pillen _art = null!;
    private StackPanel _uebungTeil = null!;
    private Control _pruefungTeil = null!;
    private Pillen _bereich = null!;
    private Pillen _status = null!;
    private ComboBox _themen = null!;
    private readonly List<string?> _themenIds = [];
    private bool _themenSetzen;
    private TextBlock _anzahl = null!;
    private Button _starten = null!;
    private TextBlock _pool = null!;
    private TextBlock _fortschritt = null!;
    private TextBlock _punkte = null!;
    private TextBlock _uhr = null!;
    private TextBlock _frage = null!;
    private StackPanel _optionen = null!;
    private TextBlock _erklaerung = null!;
    private TextBlock _hinweisKurz = null!;
    private Button _einreichen = null!;
    private Button _abbrechen = null!;

    private readonly System.Diagnostics.Stopwatch _messuhr = System.Diagnostics.Stopwatch.StartNew();

    /// <param name="themaUeben">practice_topic ("Jetzt üben" unter "Nach Themen" der Pruefung).</param>
    public TrainerSeite(Func<string, Task>? themaUeben = null)
    {
        _themaUeben = themaUeben ?? (_ => Task.CompletedTask);
        _liste.Children.Add(Schrift.Text(FarbenTexte.Laden, "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        Content = new ScrollViewer { Content = _liste };
        _takt.Tick += (_, _) => UhrZeigen();
        Geladen = LadenAsync();
    }

    public Task Geladen { get; }

    /// <summary>Phase 9 W2: vor dem Verwerfen der Seite (Wechsel der Fachrichtung) die Pruefung festhalten und abmelden.</summary>
    public void Abmelden() => _klausur?.Abmelden();

    /// <summary>Zeit vom Bauen der Seite bis die Vorbereitung steht (Messwert).</summary>
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
            var uebung = await Task.Run(() => new Uebung(inhalte, Zustand.BuchLesen, Zustand.Schreiber,
                () => Zustand.NeuReihenfolge(gewicht, Quellen.Quiz)));
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _u = uebung;
                uebung.NachSpeichern += AbgleichDienst.NachDemLernen;     // L10: bald abgleichen (schedule)
                Aufbauen(inhalte);
                foreach (var schritt in _wartend)
                {
                    schritt(uebung);
                }
                _wartend.Clear();
                Vorbereitung();
                if (_klausur!.Laeuft && _art.Wert != TrainerTexte.Pruefung)
                {
                    _art.Waehle(TrainerTexte.Pruefung);     // on_show: eine laufende Pruefung geht vor
                    ArtZeigen(TrainerTexte.Pruefung);
                }
                InhaltMs = _messuhr.Elapsed.TotalMilliseconds;
            });
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }

    private void Mit(Action<Uebung> schritt)
    {
        if (_u is { } u)
        {
            schritt(u);
        }
        else
        {
            _wartend.Add(schritt);
        }
    }

    /// <summary>on_show: laufende Pruefung geht vor; sonst ohne laufende Session den Pool neu rechnen.</summary>
    public void Gezeigt()
    {
        if (_klausur is not { } klausur)
        {
            return;
        }
        if (_art.Wert != TrainerTexte.Pruefung && klausur.Laeuft)
        {
            _art.Waehle(TrainerTexte.Pruefung);
            ArtZeigen(TrainerTexte.Pruefung);
            return;
        }
        if (_art.Wert == TrainerTexte.Pruefung)
        {
            klausur.Zeigen();
        }
        else if (_u is { Laeuft: false } u)
        {
            u.PoolNeu();
            Vorbereitung();
        }
    }

    /// <summary>practice: gezielte Runde (Jetzt wiederholen), Art "Übung".</summary>
    /// <param name="reihenfolgeBehalten">keep_order (Jetzt üben, Phase 2 L7).</param>
    /// <param name="nachher">follow_up: nach dem Ergebnis der Runde (Jetzt üben, zweiter Teil).</param>
    public void Ueben(IEnumerable<string> fragen, bool reihenfolgeBehalten = false, Func<Task>? nachher = null)
    {
        var liste = fragen.ToList();
        Mit(u =>
        {
            _nachher = null;
            var pruefung = _art.Wert != TrainerTexte.Uebung;
            if (KlausurAblauf.UebungGesperrt(pruefung, pruefung && _klausur!.Laeuft))
            {
                _ = Dialog.Zeigen(PruefungTexte.LaeuftTitel, PruefungTexte.LaeuftText, Dialog.Ok);
                return;
            }
            if (pruefung)
            {
                _art.Waehle(TrainerTexte.Uebung);
                ArtZeigen(TrainerTexte.Uebung);
            }
            if (u.Laeuft)
            {
                _takt.Stop();
                u.Abbrechen();
            }
            u.Ueben(liste, reihenfolgeBehalten);
            if (u.Laeuft)
            {
                _nachher = nachher;
                Begonnen();
            }
        });
    }

    /// <summary>Laeuft gerade eine Runde (fuer "Jetzt üben": nur dann gibt es einen zweiten Teil)?</summary>
    public bool Laeuft => _u?.Laeuft ?? false;

    /// <summary>
    /// jump_to_question (Suche, Phase 2 L8): Art "Übung", laufende Runde beenden (ohne
    /// Ergebnis), die Frage mit aufgedeckter Antwort und Erklärung zeigen.
    /// </summary>
    public void Springen(string frage) => Mit(u =>
    {
        if (_art.Wert != TrainerTexte.Uebung && !_klausur!.Laeuft)
        {
            _art.Waehle(TrainerTexte.Uebung);
            ArtZeigen(TrainerTexte.Uebung);
        }
        var eintrag = _inhalte?.Quizfragen.FirstOrDefault(q => q.Frage == frage);
        if (eintrag is null)
        {
            return;
        }
        _nachher = null;
        _takt.Stop();
        u.Abbrechen();
        Zurueckgesetzt();
        _frage.Text = eintrag.Frage;
        _optionen.Children.Clear();
        foreach (var option in eintrag.Optionen)
        {
            _optionen.Children.Add(LernTeile.Option(option, "trainer"));
        }
        LernTeile.Aufdecken(_optionen, eintrag.Antwort);
        _erklaerung.Text = eintrag.Text("exp") ?? "";
        _erklaerung.Farbe(TextBlock.ForegroundProperty, "Pinsel.text_dim");
        _einreichen.IsEnabled = false;
    });

    private void Aufbauen(Lerninhalte inhalte)
    {
        _inhalte = inhalte;
        _liste.Children.Clear();
        _art = new Pillen(TrainerTexte.Arten, TrainerTexte.Uebung, ArtZeigen);
        _liste.Children.Add(_art);

        _bereich = new Pillen(Fachbereiche.Alle.Select(f => (f, Fachbereiche.Kurzname[f])).Prepend((KartenTexte.Alle, KartenTexte.Alle)),
            KartenTexte.Alle, f => Mit(u => { u.SetzeFachbereich(f); Vorbereitung(); }));
        _status = new Pillen(KartenTexte.StatusReiter, FragenStatus.Alle, s => Mit(u => { u.SetzeStatus(s); Vorbereitung(); }));
        _themen = LernTeile.Themenliste();
        _themen.SelectionChanged += (_, _) =>
        {
            if (!_themenSetzen && _themen.SelectedIndex >= 0)
            {
                var thema = _themenIds[_themen.SelectedIndex] ?? KartenTexte.Alle;
                Mit(u => { u.SetzeThema(thema); Vorbereitung(); });
            }
        };
        var weniger = Stufenknopf("-", TrainerTexte.Weniger, -1);
        var mehr = Stufenknopf("+", TrainerTexte.Mehr, 1);
        _anzahl = Schrift.Text("", "Normal", FontWeight.Bold);
        _anzahl.TextAlignment = TextAlignment.Center;
        var anzahlFeld = new Border
        {
            Child = _anzahl,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 6),
            Margin = new Thickness(6, 0, 6, 8),
            VerticalAlignment = VerticalAlignment.Center,
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Breite(58);
        var anzahlText = Schrift.Text(TrainerTexte.Fragenanzahl, "Normal", FontWeight.Normal, "Pinsel.text_dim");
        anzahlText.VerticalAlignment = VerticalAlignment.Center;
        anzahlText.Margin = new Thickness(0, 0, 10, 8);
        _starten = LernTeile.Knopf(TrainerTexte.Starten, true);
        _starten.Margin = new Thickness(8, 0, 8, 8);
        _starten.Click += (_, _) => Starten();
        _pool = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.muted");
        _uebungTeil = new StackPanel { Spacing = 14 };
        _uebungTeil.Children.Add(Schrift.Karte(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Schrift.Text(TrainerTexte.SessionTitel, "Mittel", FontWeight.Bold),
                Schrift.Text(TrainerTexte.Katalog(inhalte.Quizfragen.Count), "Klein", FontWeight.Normal, "Pinsel.muted"),
                LernTeile.Label(KartenTexte.Fachbereich), _bereich,
                LernTeile.Label(KartenTexte.Status), _status,
                LernTeile.Label(KartenTexte.Thema), _themen,
                new WrapPanel { Margin = new Thickness(0, 8, 0, 0), Children = { anzahlText, weniger, anzahlFeld, mehr, _starten } },
                _pool,
            },
        }));

        _fortschritt = Schrift.Text(TrainerTexte.FortschrittLeer, "Normal", FontWeight.Bold, "Pinsel.text_dim");
        _punkte = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        _punkte.VerticalAlignment = VerticalAlignment.Center;
        _uhr = Schrift.Text(TrainerTexte.UhrLeer, "Normal", FontWeight.Bold, "Pinsel.accent");
        var rechts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { _punkte, _uhr } };
        DockPanel.SetDock(rechts, Dock.Right);
        _uebungTeil.Children.Add(Schrift.Karte(new DockPanel { Children = { rechts, _fortschritt } }));

        // Fix A 0.14.2 (Weisung P8-9): LernText wie bei den Karteikarten, sehr lange Woerter bleiben ganz
        _frage = new LernText(TrainerTexte.StartHinweis, "Gross", FontWeight.SemiBold, "Pinsel.text_soft");
        _optionen = new StackPanel { Spacing = 6 };
        _erklaerung = new LernText("", "Normal", FontWeight.Normal, "Pinsel.text_dim");
        _hinweisKurz = Schrift.Text(TrainerTexte.HinweisKurz, "Klein", FontWeight.Normal, "Pinsel.muted");
        _hinweisKurz.IsVisible = false;
        _uebungTeil.Children.Add(Schrift.Karte(new StackPanel
        {
            Spacing = 10,
            Children = { Schrift.Text(TrainerTexte.AufgabeTitel, "Mittel", FontWeight.Bold), _frage, _hinweisKurz, _optionen, _erklaerung },
        }));

        _einreichen = LernTeile.Knopf(TrainerTexte.Einreichen, true);
        _einreichen.IsEnabled = false;
        _einreichen.Click += (_, _) => Einreichen();
        _abbrechen = LernTeile.Knopf(TrainerTexte.Abbrechen);
        _abbrechen.IsEnabled = false;
        _abbrechen.Click += async (_, _) => await AbbrechenAsync();
        _uebungTeil.Children.Add(new WrapPanel { Children = { _einreichen, _abbrechen } });
        _liste.Children.Add(_uebungTeil);

        _klausur = new KlausurTeil(inhalte, _themaUeben);
        _pruefungTeil = _klausur.Teil;
        _pruefungTeil.IsVisible = false;
        _liste.Children.Add(_pruefungTeil);
    }

    private Button Stufenknopf(string zeichen, string name, int schritte)
    {
        var knopf = LernTeile.Knopf(zeichen);
        knopf.Margin = new Thickness(0, 0, 0, 8);
        Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        ToolTip.SetTip(knopf, name);
        knopf.Click += (_, _) => Mit(u =>
        {
            u.AnzahlAendern(schritte);
            _anzahl.Text = u.Anzahl.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
        return knopf;
    }

    /// <summary>_on_mode: zur Pruefung laufende Uebung beenden (ohne Ergebnis), zurueck Pruefung verbergen.</summary>
    private void ArtZeigen(string art)
    {
        if (art == TrainerTexte.Pruefung)
        {
            if (_u is { Laeuft: true } u)
            {
                _nachher = null;
                _takt.Stop();
                u.Abbrechen();
                Zurueckgesetzt();
            }
            _uebungTeil.IsVisible = false;
            _pruefungTeil.IsVisible = true;
            _klausur!.Zeigen();
        }
        else
        {
            _klausur!.Verbergen();
            _pruefungTeil.IsVisible = false;
            _uebungTeil.IsVisible = true;
        }
        if (Content is ScrollViewer rollen)
        {
            rollen.Offset = default;      // to_top
        }
    }

    /// <summary>Nur fuer den Start ohne Bedienung und die Tests.</summary>
    internal KlausurTeil? Klausur => _klausur;

    /// <summary>Nur fuer den Start ohne Bedienung: Umschalter "Übung"/"Prüfung" wie ein Klick.</summary>
    internal void ArtWaehlen(string art)
    {
        if (_art.Wert != art)
        {
            _art.Waehle(art);
            ArtZeigen(art);
        }
    }

    /// <summary>Filter, Pool und Fragenanzahl zeigen (_update_pool).</summary>
    private void Vorbereitung()
    {
        if (_u is not { } u)
        {
            return;
        }
        _bereich.Waehle(u.Fachbereich);
        _status.Waehle(u.Status);
        _themenSetzen = true;
        var themen = u.Themen;
        if (_themenIds.Count != themen.Count + 1 || !_themenIds.Skip(1).SequenceEqual(themen.Select(t => (string?)t.Id)))
        {
            _themenIds.Clear();
            _themenIds.Add(null);
            _themenIds.AddRange(themen.Select(t => (string?)t.Id));
            _themen.ItemsSource = themen.Select(t => t.Name).Prepend(KartenTexte.Alle).ToList();
        }
        _themen.SelectedIndex = Math.Max(0, _themenIds.IndexOf(u.Thema == KartenTexte.Alle ? null : u.Thema));
        _themenSetzen = false;
        _anzahl.Text = u.Anzahl.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _pool.Text = u.PoolText;
    }

    private void Starten()
    {
        _nachher = null;
        if (_u is not { } u)
        {
            return;
        }
        if (u.Starten() is { } hinweis)
        {
            _ = Dialog.Zeigen(KartenTexte.HinweisTitel, hinweis, Dialog.Ok);
            return;
        }
        Begonnen();
    }

    private void Begonnen()
    {
        _starten.IsEnabled = false;
        _abbrechen.IsEnabled = true;
        _hinweisKurz.IsVisible = false;
        _takt.Start();
        UhrZeigen();
        FrageZeigen();
    }

    private void UhrZeigen()
    {
        if (_u is { } u)
        {
            _uhr.Text = u.Uhr;
        }
    }

    private void FrageZeigen()
    {
        if (_u is not { Frage: not null } u)
        {
            return;
        }
        _frage.Text = u.FrageText;
        _erklaerung.Text = "";
        _optionen.Children.Clear();
        foreach (var option in u.Optionen)
        {
            _optionen.Children.Add(LernTeile.Option(option, "trainer"));
        }
        _fortschritt.Text = u.Fortschritt;
        _punkte.Text = u.RichtigText;
        ((TextBlock)_einreichen.Content!).Text = TrainerTexte.Einreichen;
        _einreichen.IsEnabled = true;
    }

    private void Einreichen()
    {
        if (_u is not { Laeuft: true } u)
        {
            return;
        }
        if (u.Beantwortet)
        {
            if (u.Weiter() is { } ergebnis)
            {
                Beendet(ergebnis);
            }
            else
            {
                FrageZeigen();
            }
            return;
        }
        var auswahl = LernTeile.Gewaehlt(_optionen);
        var richtigeAntwort = u.Frage?.Antwort;
        if (u.Einreichen(auswahl) is not { } antwort)
        {
            return;
        }
        if (antwort.Warnung is { } warnung)
        {
            _ = Dialog.Zeigen(KartenTexte.HinweisTitel, warnung, Dialog.Ok);
            return;
        }
        LernTeile.Aufdecken(_optionen, richtigeAntwort);
        _erklaerung.Text = antwort.Erklaerung;
        _erklaerung.Farbe(TextBlock.ForegroundProperty, antwort.Richtig ? "Pinsel.green" : "Pinsel.text_dim");
        _punkte.Text = u.RichtigText;
        ((TextBlock)_einreichen.Content!).Text = antwort.Knopf;
    }

    private void Beendet(UebungErgebnis ergebnis)
    {
        _takt.Stop();
        _optionen.Children.Clear();
        _erklaerung.Text = "";
        Zurueckgesetzt();
        _frage.Text = ergebnis.Text;
        _hinweisKurz.IsVisible = true;
        if (_nachher is { } nachher)
        {
            _nachher = null;
            DispatcherTimer.RunOnce(() => _ = nachher(), TimeSpan.FromMilliseconds(300));
        }
    }

    private async Task AbbrechenAsync()
    {
        if (_u is not { Laeuft: true } u)
        {
            return;
        }
        if (await Dialog.Zeigen(TrainerTexte.Abbrechen, TrainerTexte.AbbrechenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0 || !u.Laeuft)
        {
            return;
        }
        _nachher = null;
        _takt.Stop();
        u.Abbrechen();
        Zurueckgesetzt();
        _frage.Text = TrainerTexte.Abgebrochen;
        _optionen.Children.Clear();
        _erklaerung.Text = "";
    }

    /// <summary>
    /// Nur fuer den Start ohne Bedienung: 0 = Frage mit Antworten und Erklaerung (laengste Frage
    /// des Katalogs), 1 = Ergebnis mit Hinweis, -1 = zurueck. Speichert nichts.
    /// </summary>
    internal void Probe(int art, Lerninhalte inhalte)
    {
        if (_u is not { Laeuft: false })
        {
            return;
        }
        _optionen.Children.Clear();
        _erklaerung.Text = "";
        _hinweisKurz.IsVisible = false;
        if (art < 0)
        {
            _frage.Text = TrainerTexte.StartHinweis;
            _punkte.Text = "";
            _fortschritt.Text = TrainerTexte.FortschrittLeer;
            return;
        }
        if (art == 0)
        {
            var frage = inhalte.Quizfragen.MaxBy(q => (q.Frage ?? "").Length + q.Optionen.Sum(o => o.Length))!;
            _frage.Text = Beschriftungen.Einsetzen(TrainerTexte.FrageNummer, 50, frage.Frage!);
            foreach (var option in frage.Optionen)
            {
                _optionen.Children.Add(LernTeile.Option(option, "probe"));
            }
            LernTeile.Aufdecken(_optionen, frage.Antwort);
            _erklaerung.Text = Beschriftungen.Einsetzen(TrainerTexte.ErklaerungFalsch, frage.Antwort!) + frage.Text("exp");
            _fortschritt.Text = Beschriftungen.Einsetzen(TrainerTexte.Fortschritt, 50, 50);
            _punkte.Text = Beschriftungen.Einsetzen(TrainerTexte.Richtig, 49);
            return;
        }
        var prozent = Trainer.Prozent(49, 50);
        _frage.Text = new UebungErgebnis(49, 50, prozent, Trainer.Note(prozent), 3599, false).Text;
        _hinweisKurz.IsVisible = true;
    }

    // _reset_controls
    private void Zurueckgesetzt()
    {
        _starten.IsEnabled = true;
        _abbrechen.IsEnabled = false;
        _einreichen.IsEnabled = false;
        ((TextBlock)_einreichen.Content!).Text = TrainerTexte.Einreichen;
        _fortschritt.Text = TrainerTexte.FortschrittLeer;
        _uhr.Text = TrainerTexte.UhrLeer;
        Vorbereitung();
    }
}
