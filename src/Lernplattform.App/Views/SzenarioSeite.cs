using Avalonia;
using Avalonia.Controls;
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
/// Phase 4, PB3: Seiten AP1-Szenarien, AP2-Szenarien und Testprojekt wie app_gui v0.63.2
/// (ScenarioViewBase/Ap1ScenarioView/ScenarioView 4154-4370, ProjectView 4375-4585), PC-Wortlaut
/// auf PC und Handy (Wortliste W1). Liste links, rechts Aufgabe, eigene Loesung (nur Szenarien,
/// nur fuer die Sitzung), Musterloesung bzw. Loesungsansaetze zum Aufdecken mit "Gewusst"/
/// "Nicht gewusst". Ablauf und Speichern in Core.Lernen.Szenarioablauf. Ab der Breite
/// <see cref="MainView.BreitGrenze"/> stehen Liste und Aufgabe nebeneinander (2 : 5), darunter
/// untereinander. Testprojekt: Knopf "Als Vorlage fürs Abschlussprojekt" (W3, Nico; use_as_template).
/// </summary>
public sealed class SzenarioSeite : UserControl
{
    private readonly string _quelle;
    private readonly Action<Lerninhalt>? _alsVorlage;
    private readonly Grid _raster = new();
    private readonly StackPanel _detail = new() { Spacing = 14 };
    private readonly List<Action> _wartend = [];
    private readonly System.Diagnostics.Stopwatch _uhr = System.Diagnostics.Stopwatch.StartNew();
    private Szenarioablauf? _a;
    private ListenTeil _liste = null!;
    private Border _listenKarte = null!;
    private TextBlock _statuszeile = null!;
    private LernText _titel = null!;
    private LernText _kopfzeile = null!;
    private LernText _aufgabe = null!;
    private TextBox _eigene = null!;
    private LernText _loesung = null!;
    private WrapPanel _bewerten = null!;
    private Button _umschalten = null!;
    private bool? _breit;
    private double _listenBreite;
    private (double Groesse, double Breite)? _laengstes;
    private bool _eigeneSetzen;

    public SzenarioSeite(string quelle, Action<Lerninhalt>? alsVorlage = null)
    {
        _quelle = quelle;
        _alsVorlage = alsVorlage;
        Content = new ScrollViewer { Content = new StackPanel { Margin = new Thickness(16), Children = { _raster } } };
        _raster.Children.Add(Schrift.Text(FarbenTexte.Laden, "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        Geladen = LadenAsync();
        _beimBeenden = () => _a?.Speichern();
        Zustand.BeimBeenden += _beimBeenden;      // flush_inputs beim Beenden
        SizeChanged += (_, e) => Aufteilen(e.NewSize.Width);
        _schrift = () => Dispatcher.UIThread.Post(() => Aufteilen(Bounds.Width));
        Schrift.Geaendert += _schrift;
    }

    private readonly Action _beimBeenden;
    private readonly Action _schrift;

    /// <summary>Phase 9 W2: vor dem Verwerfen der Seite (Wechsel der Fachrichtung) speichern und abmelden.</summary>
    public void Abmelden()
    {
        _a?.Speichern();
        Zustand.BeimBeenden -= _beimBeenden;
        Schrift.Geaendert -= _schrift;
    }

    private bool Projekt => _quelle == Quellen.Projekt;

    /// <summary>Fertig aufgebaut (Lernstand gelesen, erster Eintrag gezeigt).</summary>
    public Task Geladen { get; }

    /// <summary>Zeit vom Bauen der Seite bis der erste Eintrag steht (Messwert).</summary>
    public double? InhaltMs { get; private set; }

    internal Szenarioablauf? Ablauf => _a;

    internal ListenTeil Liste => _liste;

    private async Task LadenAsync()
    {
        try
        {
            var inhalte = await Zustand.Inhalte;
            if (inhalte is null)
            {
                return;
            }
            var eintraege = _quelle switch
            {
                Quellen.Ap1 => inhalte.Ap1Szenarien,
                Quellen.Ap2 => inhalte.Ap2Szenarien,
                _ => inhalte.Testprojekte,
            };
            if (eintraege.Count == 0)
            {
                // Phase 9 W2: AE, DPA und DV haben hier (noch) keine eigenen Aufgaben
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _raster.Children.Clear();
                    _raster.Children.Add(Schrift.Karte(new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            KartenTitel(_quelle switch { Quellen.Ap1 => SzenarienTexte.Ap1Titel, Quellen.Ap2 => SzenarienTexte.Ap2Titel, _ => TestprojektTexte.ListeTitel }),
                            new LernText(FachrichtungTexte.KeineAufgaben, "Normal", FontWeight.Normal, "Pinsel.text_dim"),
                        },
                    }));
                    InhaltMs = _uhr.Elapsed.TotalMilliseconds;
                });
                return;
            }
            var ablauf = new Szenarioablauf(_quelle, eintraege, Zustand.BuchLesen, Zustand.Schreiber);
            await Task.Run(() => ablauf.Stati());
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _a = ablauf;
                Aufbauen(ablauf);
                ablauf.NachSpeichern += AbgleichDienst.NachDemLernen;     // notify_progress: bald abgleichen
                Laden(0, sprung: false);
                foreach (var schritt in _wartend)
                {
                    schritt();
                }
                _wartend.Clear();
                InhaltMs = _uhr.Elapsed.TotalMilliseconds;
            });
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
        }
    }

    private void Mit(Action schritt)
    {
        if (_a is not null)
        {
            schritt();
        }
        else
        {
            _wartend.Add(schritt);
        }
    }

    /// <summary>
    /// load_scenario/load_project von aussen (Suchtreffer, Notizblock "Jetzt üben"); Phase 9 W2: mit der
    /// gespeicherten Position (Nummer - 1), die Seite zeigt den Eintrag nur, wenn die Sicht ihn hat.
    /// </summary>
    public void Oeffnen(int gespeichert) => Mit(() =>
    {
        if (_a!.Position(gespeichert) is var position and >= 0)
        {
            Laden(position);
        }
    });

    /// <summary>Suchtreffer: das erste Szenario mit gleichem Titel (open_search_hit).</summary>
    public void Springen(string titel) => Mit(() =>
    {
        for (var i = 0; i < _a!.Eintraege.Count; i++)
        {
            if (_a.Eintraege[i].Titel == titel)
            {
                Laden(i);
                return;
            }
        }
    });

    /// <summary>on_show: der Lernstand kann sich (Abgleich, andere Seite) geaendert haben.</summary>
    public void Gezeigt()
    {
        if (_a is { } a)
        {
            a.NeuLesen();
            _liste.Neu(seiteBehalten: true);
            StatusZeigen();
        }
    }

    private static TextBlock KartenTitel(string text) => Schrift.Text(text, "Mittel", FontWeight.Bold);

    private void Aufbauen(Szenarioablauf a)
    {
        _raster.Children.Clear();
        var anzahl = a.Eintraege.Count;
        var listenKopf = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                KartenTitel(_quelle switch { Quellen.Ap1 => SzenarienTexte.Ap1Titel, Quellen.Ap2 => SzenarienTexte.Ap2Titel, _ => TestprojektTexte.ListeTitel }),
                Schrift.Text(Projekt ? Beschriftungen.Einsetzen(TestprojektTexte.AnzahlAuftraege, Core.Dashboard.DashboardTexte.DeInt(anzahl))
                    : Beschriftungen.Einsetzen(SzenarienTexte.AnzahlAufgaben, anzahl), "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        };
        _liste = Projekt
            ? new ListenTeil(a.Eintraege, "schwierigkeit", SzenarienTexte.Schwierigkeit, true, SzenarienTexte.Kundenauftraegen,
                TestprojektTexte.Unterzeile, a.Stati, p => Laden(p))
            : new ListenTeil(a.Eintraege, "theme", SzenarienTexte.Thema, false, SzenarienTexte.Aufgaben,
                e => e.Text("theme") ?? "", a.Stati, p => Laden(p));
        _listenKarte = Schrift.Karte(new StackPanel { Spacing = 10, Children = { listenKopf, _liste } });
        _liste.Neu();

        _statuszeile = Schrift.Text("", "Klein", FontWeight.Bold, "Pinsel.muted");
        _titel = new LernText("", "Gross", FontWeight.SemiBold, "Pinsel.text");
        _aufgabe = new LernText("", "Normal", FontWeight.Normal, "Pinsel.text_soft");
        _loesung = new LernText("", "Normal", FontWeight.Normal, "Pinsel.text_soft");
        _detail.Children.Clear();
        if (Projekt)
        {
            _kopfzeile = new LernText("", "Klein", FontWeight.Normal, "Pinsel.muted");
            _detail.Children.Add(new StackPanel { Spacing = 2, Children = { _titel, _kopfzeile, _statuszeile } });
            _detail.Children.Add(Schrift.Karte(new StackPanel { Spacing = 8, Children = { KartenTitel(TestprojektTexte.Kundenauftrag), _aufgabe } }));
        }
        else
        {
            _detail.Children.Add(Schrift.Karte(new StackPanel
            {
                Spacing = 8,
                Children = { KartenTitel(SzenarienTexte.Aufgabenstellung), _statuszeile, _titel, _aufgabe },
            }));
            _eigene = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 96, MaxHeight = 420 }
                .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
            Avalonia.Automation.AutomationProperties.SetName(_eigene, SzenarienTexte.DeineLoesung);
            _eigene.TextChanged += (_, _) =>
            {
                if (!_eigeneSetzen && _a is { } ablauf)
                {
                    ablauf.EigeneLoesungSetzen(ablauf.Index, _eigene.Text ?? "");
                }
            };
            _detail.Children.Add(Schrift.Karte(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    KartenTitel(SzenarienTexte.DeineLoesung),
                    Schrift.Text(SzenarienTexte.DeineLoesungUnter, "Klein", FontWeight.Normal, "Pinsel.muted"),
                    Schrift.Text(SzenarienTexte.DeineLoesungHinweis, "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                    _eigene,
                },
            }));
        }
        var gewusst = LernTeile.Knopf(KartenTexte.Gewusst, true);
        gewusst.Click += (_, _) => Bewerten(true);
        var nichtGewusst = LernTeile.Knopf(KartenTexte.NichtGewusst);
        nichtGewusst.Click += (_, _) => Bewerten(false);
        var wusstest = Schrift.Text(KartenTexte.Wusstest, "Normal", FontWeight.Normal, "Pinsel.text_dim");
        wusstest.VerticalAlignment = VerticalAlignment.Center;
        wusstest.Margin = new Thickness(0, 0, 12, 8);
        _bewerten = new WrapPanel { IsVisible = false, Margin = new Thickness(0, 12, 0, 0), Children = { wusstest, gewusst, nichtGewusst } };
        _detail.Children.Add(Schrift.Karte(new StackPanel
        {
            Spacing = 8,
            Children = { KartenTitel(Projekt ? TestprojektTexte.Loesungsansaetze : SzenarienTexte.Musterloesung), _loesung, _bewerten },
        }));
        _umschalten = LernTeile.Knopf(Projekt ? TestprojektTexte.Anzeigen : SzenarienTexte.Anzeigen, true);
        _umschalten.Click += (_, _) => Umschalten();
        var naechstes = LernTeile.Knopf(Projekt ? TestprojektTexte.Naechstes : SzenarienTexte.Naechstes);
        naechstes.Click += (_, _) => Laden(_liste.NaechsteNach(a.Index));
        var knoepfe = new WrapPanel { Children = { _umschalten, naechstes } };
        if (Projekt && _alsVorlage is { } alsVorlage)
        {
            var vorlage = LernTeile.Knopf(ProjektTexte.AlsVorlage);
            vorlage.Click += (_, _) => alsVorlage(a.Eintrag);
            knoepfe.Children.Add(vorlage);
        }
        _detail.Children.Add(knoepfe);

        _raster.Children.Add(_listenKarte);
        _raster.Children.Add(_detail);
        _breit = null;
        Aufteilen(Bounds.Width);
    }

    /// <summary>
    /// Breit: Liste und Aufgabe nebeneinander (2 : 5 wie Python); die Liste ist mindestens so
    /// breit, dass das laengste Wort eines Titels in Stufe Normal ganz bleibt (Python trennt es
    /// mit fisi_trennung). Reicht dafuer weniger als die Haelfte der Breite nicht, oder ist das
    /// Fenster schmaler als <see cref="MainView.BreitGrenze"/>, stehen beide untereinander.
    /// </summary>
    private void Aufteilen(double breite)
    {
        if (_detail.Parent is null || breite <= 0 || _a is not { } a)
        {
            return;
        }
        var groesse = Schrift.Grundgroessen["Normal"] * Schrift.Faktor;
        if (_laengstes is not { } l || l.Groesse != groesse)
        {
            var titel = string.Join(" ", a.Eintraege.Select((e, i) => SzenarienTexte.Eintrag(i, e.Titel ?? "", true)));
            _laengstes = l = (groesse, LernText.LaengstesWort(titel, new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold),
                Schrift.Grundgroessen["Normal"]));
        }
        // Rand der Seite, Karte, Zeile und Farbstreifen
        var noetig = l.Breite + 2 * 16 + 2 * 16 + 2 * 11 + 14 + 8;
        var inhalt = breite - 32;
        var listenBreite = Math.Max(inhalt * 2 / 7, noetig);
        var breit = breite >= MainView.BreitGrenze && listenBreite <= inhalt / 2;
        if (_breit == breit && (!breit || Math.Abs(listenBreite - _listenBreite) < 1))
        {
            return;
        }
        _breit = breit;
        _listenBreite = listenBreite;
        _raster.ColumnDefinitions = breit
            ? [new ColumnDefinition(listenBreite, GridUnitType.Pixel), new ColumnDefinition(1, GridUnitType.Star)]
            : new ColumnDefinitions("*");
        _raster.RowDefinitions = breit ? new RowDefinitions("Auto") : new RowDefinitions("Auto,Auto");
        Grid.SetColumn(_detail, breit ? 1 : 0);
        Grid.SetRow(_detail, breit ? 0 : 1);
        _listenKarte.Margin = breit ? new Thickness(0, 0, 8, 0) : new Thickness(0, 0, 0, 14);
        _detail.Margin = breit ? new Thickness(8, 0, 0, 0) : new Thickness(0);
        _listenKarte.VerticalAlignment = VerticalAlignment.Top;
    }

    /// <summary>
    /// load_scenario / load_project: offene Aufdeckung speichern, Eintrag zeigen, Loesung ausgeblendet.
    /// Am Handy (untereinander) rueckt die Aufgabe ins Bild, ausser beim ersten Zeigen.
    /// </summary>
    private void Laden(int position, bool sprung = true)
    {
        if (_a is not { } a)
        {
            return;
        }
        a.Laden(position);
        var eintrag = a.Eintrag;
        _bewerten.IsVisible = false;
        _titel.Text = eintrag.Titel ?? "";
        if (Projekt)
        {
            _kopfzeile.Text = TestprojektTexte.Kopfzeile(eintrag);
            _aufgabe.Text = TestprojektTexte.Aufgabe(eintrag);
            _loesung.Text = TestprojektTexte.NochAusgeblendet;
            ((TextBlock)_umschalten.Content!).Text = TestprojektTexte.Anzeigen;
        }
        else
        {
            _aufgabe.Text = eintrag.Text("text") ?? "";
            _loesung.Text = SzenarienTexte.NochAusgeblendet;
            ((TextBlock)_umschalten.Content!).Text = SzenarienTexte.Anzeigen;
            _eigeneSetzen = true;
            _eigene.Text = a.EigeneLoesung(position);
            _eigeneSetzen = false;
        }
        _liste.Neu(seiteBehalten: true);
        _liste.Zeigen(position);
        StatusZeigen();
        if (sprung && _breit == false)
        {
            _detail.BringIntoView();
        }
    }

    /// <summary>toggle_solution / toggle_hints.</summary>
    private void Umschalten()
    {
        if (_a is not { } a)
        {
            return;
        }
        a.Umschalten();
        if (a.Sichtbar)
        {
            _loesung.Text = Projekt ? TestprojektTexte.Hinweise(a.Eintrag) : a.Eintrag.Text("solution") ?? "";
            ((TextBlock)_umschalten.Content!).Text = Projekt ? TestprojektTexte.Ausblenden : SzenarienTexte.Ausblenden;
        }
        else
        {
            _loesung.Text = Projekt ? TestprojektTexte.Ausgeblendet : SzenarienTexte.Ausgeblendet;
            ((TextBlock)_umschalten.Content!).Text = Projekt ? TestprojektTexte.Anzeigen : SzenarienTexte.Anzeigen;
        }
        _bewerten.IsVisible = a.Offen is not null;
    }

    /// <summary>rate: Selbsteinschaetzung, danach Liste und Status neu.</summary>
    private void Bewerten(bool richtig)
    {
        if (_a is not { } a)
        {
            return;
        }
        _bewerten.IsVisible = false;
        a.Bewerten(richtig);
        _liste.Neu(seiteBehalten: true);
        StatusZeigen();
    }

    private void StatusZeigen()
    {
        if (_a is not { } a)
        {
            return;
        }
        var (text, farbe) = a.StatusZeile;
        _statuszeile.Text = text;
        _statuszeile.Farbe(TextBlock.ForegroundProperty, LernTeile.Pinsel(farbe));
    }

    /// <summary>
    /// Nur fuer den Start ohne Bedienung: 0 = laengster Titel mit aufgedeckter Loesung und
    /// "Wusstest du es?" (ohne zu speichern), 1 = letzte Seite der Liste, -1 = zurueck auf den ersten Eintrag.
    /// </summary>
    internal void Probe(int art)
    {
        if (_a is not { } a)
        {
            return;
        }
        if (art < 0)
        {
            Laden(0, sprung: false);
            return;
        }
        if (art == 1)
        {
            _liste.Blaettern(1000);
            return;
        }
        var lang = Enumerable.Range(0, a.Eintraege.Count).MaxBy(i => (a.Eintraege[i].Titel ?? "").Length + (a.Eintraege[i].Text("solution") ?? "").Length / 50);
        Laden(lang, sprung: false);
        _loesung.Text = Projekt ? TestprojektTexte.Hinweise(a.Eintrag) : a.Eintrag.Text("solution") ?? "";
        ((TextBlock)_umschalten.Content!).Text = Projekt ? TestprojektTexte.Ausblenden : SzenarienTexte.Ausblenden;
        _bewerten.IsVisible = true;
    }
}
