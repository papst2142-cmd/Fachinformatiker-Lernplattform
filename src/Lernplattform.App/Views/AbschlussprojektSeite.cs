using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Projekt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, W3 (PB8): Seite "Abschlussprojekt" wie app_gui FinalProjectView v0.63.2 (4586-5141),
/// PC-Wortlaut auf PC und Handy (Wortliste W3). Kopfzeile mit Auswahl, "Neues Projekt", "Löschen",
/// "Als PDF", "Als Text" (bricht um), darunter 6 Reiter als Pillen. Ein Reiter wird beim ersten
/// Oeffnen gebaut und danach wiederverwendet; ein neuer Reiter beginnt oben. Ablauf und Speichern
/// in <see cref="ProjektAblauf"/> (800 ms nach dem letzten Tastendruck, beim Verlassen eines Feldes,
/// Reiterwechsel, Projektwahl, Seite verlassen, Beenden, Hintergrund, vor Sicherung und Uebernahme).
/// </summary>
public sealed class AbschlussprojektSeite : UserControl
{
    private static readonly bool Handy = OperatingSystem.IsAndroid();

    private readonly ScrollViewer _scroll = new();
    private readonly StackPanel _seite = new() { Margin = new Thickness(16), Spacing = 14 };
    private readonly ContentControl _bereich = new();
    private readonly Dictionary<string, Control> _reiter = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action> _auffrischen = new(StringComparer.Ordinal);

    private Action? _vorlagenFuellen;

    /// <summary>Phase 9 W2: nach dem Wechsel der Sicht die Vorlagen (Testprojekte) neu fuellen, sobald die Inhalte fertig sind.</summary>
    public void SichtNeu() => _ = Zustand.Inhalte.ContinueWith(_ => Dispatcher.UIThread.Post(() => _vorlagenFuellen?.Invoke()), TaskScheduler.Default);
    private readonly DispatcherTimer _takt = new();
    private readonly ProjektAblauf _a;
    private ComboBox _projekte = null!;
    private Pillen _pillen = null!;
    private List<string> _kennungen = [];
    private bool _setzen;
    private int _gebautMit;

    public AbschlussprojektSeite()
    {
        _a = new ProjektAblauf(() => Zustand.Schreiber, () => Zustand.Einstellungen.Laden(), Zustand.Aendere, Zustand.FachfragenSicht,
            () => Abgleich.Eingerichtet(Zustand.Einstellungen.Laden()));
        _a.NachSpeichern += AbgleichDienst.NachDemLernen;            // Weisung P4-8, Frage 2 a: PC wie Handy
        _a.NeuGeladen += Neuaufbau;
        _a.Geaendert += Auffrischen;
        _a.MenueGeaendert += MenueFuellen;
        _a.FragenNeu += () =>
        {
            // _set_category: Reiter "Fachgespräch" neu bauen
            if (_reiter.Remove("fachgespraech", out var alt) && _bereich.Content == alt)
            {
                _bereich.Content = null;
            }
            _auffrischen.Remove("fachgespraech");
        };
        _takt.Tick += (_, _) => Takt();
        Zustand.BeiEingabenSpeichern += Speichern;
        _seite.Children.Add(Kopfzeile());
        _pillen = new Pillen(Abschlussprojekt.Reiter, _a.Reiter, ReiterZeigen);
        _seite.Children.Add(_pillen);
        _seite.Children.Add(_bereich);
        _scroll.Content = _seite;
        Content = _scroll;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        _a.Laden();
        LadenMs = uhr.Elapsed.TotalMilliseconds;
    }

    internal ProjektAblauf Ablauf => _a;

    /// <summary>Zeit fuer das erste Laden des Projekts samt Reiter (Messwert).</summary>
    public double LadenMs { get; }

    // -- Aufbau ---------------------------------------------------------------------------------

    private Control Kopfzeile()
    {
        _projekte = LernTeile.Themenliste();
        _projekte.MinWidth = 220;
        _projekte.MaxWidth = 420;
        _projekte.Margin = new Thickness(0, 0, 8, 8);
        Avalonia.Automation.AutomationProperties.SetName(_projekte, ProjektTexte.Projekt);
        _projekte.SelectionChanged += (_, _) =>
        {
            if (!_setzen && _projekte.SelectedIndex is >= 0 and var i && i < _kennungen.Count)
            {
                _a.Waehlen(_kennungen[i]);
            }
        };
        var beschriftung = Schrift.Text(ProjektTexte.Projekt, "Klein", FontWeight.Normal, "Pinsel.text_dim");
        beschriftung.VerticalAlignment = VerticalAlignment.Center;
        beschriftung.Margin = new Thickness(0, 0, 10, 8);
        var neu = LernTeile.Knopf(ProjektTexte.NeuesProjekt);
        neu.Click += (_, _) => _a.NeuesProjekt();
        var loeschen = LernTeile.Knopf(ProjektTexte.Loeschen);
        loeschen.Click += async (_, _) => await LoeschenAsync();
        var pdf = LernTeile.Knopf(ProjektTexte.AlsPdf, true);
        pdf.Click += async (_, _) => await ExportAsync("pdf");
        var text = LernTeile.Knopf(ProjektTexte.AlsText);
        text.Click += async (_, _) => await ExportAsync("txt");
        return new WrapPanel { Children = { beschriftung, _projekte, neu, loeschen, pdf, text } };
    }

    /// <summary>_load_project: Reiter abbauen, Auswahl neu, gezeigten Reiter neu bauen.</summary>
    private void Neuaufbau()
    {
        _takt.Stop();
        _reiter.Clear();
        _auffrischen.Clear();
        _bereich.Content = null;
        _gebautMit = _a.MaxStunden;
        MenueFuellen();
        ReiterZeigen(_a.Reiter, wechseln: false);
    }

    private void MenueFuellen()
    {
        var menue = _a.Menue();
        _setzen = true;
        _kennungen = menue.Select(m => m.Kennung).ToList();
        _projekte.ItemsSource = menue.Select(m => m.Titel).ToList();
        _projekte.SelectedIndex = _a.Projekt is { } p ? _kennungen.IndexOf(p) : -1;
        _setzen = false;
    }

    /// <summary>_on_tab: erst speichern, Reiter bauen oder wiederverwenden, nach oben, Werte auffrischen.</summary>
    private void ReiterZeigen(string reiter) => ReiterZeigen(reiter, wechseln: true);

    private void ReiterZeigen(string reiter, bool wechseln)
    {
        if (wechseln)
        {
            _a.ReiterWechseln(reiter);
        }
        if (!_reiter.TryGetValue(reiter, out var inhalt))
        {
            inhalt = _reiter[reiter] = reiter switch
            {
                "uebersicht" => Uebersicht(),
                "antrag" => Antrag(),
                "zeitplan" => Zeitplan(),
                "kosten" => Kosten(),
                "doku" => Doku(),
                _ => Fachgespraech(),
            };
        }
        _bereich.Content = inhalt;
        _pillen.Waehle(reiter);
        _scroll.Offset = default;
        Auffrischen();
    }

    private void Auffrischen()
    {
        foreach (var auffrischen in _auffrischen.Values)
        {
            auffrischen();
        }
    }

    private static Border Karte(string titel, string? untertitel, Control inhalt) =>
        Schrift.KopfKarte(titel, untertitel, inhalt);   // A 0.12.2: Kartenkopf wie ui.Card

    private static TextBlock Hinweis(string text) => Schrift.Text(text, "Klein", FontWeight.Normal, "Pinsel.muted");

    /// <summary>_entry: einzeiliges Feld; speichert 800 ms nach dem Tippen und beim Verlassen.</summary>
    private TextBox Feld(string feld, string vorgabe = "", double? breite = null, string? name = null)
    {
        var box = new TextBox { Text = _a.Startwert(feld, vorgabe), TextWrapping = TextWrapping.NoWrap };
        Anmelden(box, feld, name, textfeld: false);
        if (breite is { } b)
        {
            box.Breite(b);
            box.HorizontalAlignment = HorizontalAlignment.Left;
        }
        return box;
    }

    /// <summary>_textarea: mehrzeilig, waechst mit (min_height bis max_height Zeilen, dann Rollbalken).</summary>
    private TextBox Textfeld(string feld, string name, int min = 3, int max = 14)
    {
        var zeile = Schrift.Grundgroessen["Normal"] * Schrift.Faktor * 1.45;
        var box = new TextBox
        {
            Text = _a.Startwert(feld),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = min * zeile + 12,
            MaxHeight = max * zeile + 12,
        };
        Anmelden(box, feld, name, textfeld: true);
        return box;
    }

    private void Anmelden(TextBox box, string feld, string? name, bool textfeld)
    {
        box.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
        if (name is not null)
        {
            Avalonia.Automation.AutomationProperties.SetName(box, name);
        }
        _a.Eingabe(feld, () => box.Text ?? "", textfeld);
        box.TextChanged += (_, _) =>
        {
            _a.Getippt(feld);
            _takt.Stop();
            _takt.Interval = TimeSpan.FromMilliseconds(Abschlussprojekt.SpeicherVerzug);
            _takt.Start();
        };
        box.LostFocus += (_, _) => Speichern();
    }

    private void Takt()
    {
        _takt.Stop();
        if (!_a.Takt() && _a.Frist is { } frist)
        {
            _takt.Interval = TimeSpan.FromMilliseconds(Math.Max(1, (frist - DateTimeOffset.UtcNow).TotalMilliseconds));
            _takt.Start();
        }
    }

    /// <summary>_flush von aussen (Seite verlassen, Beenden, Hintergrund, vor Sicherung und Uebernahme).</summary>
    public void Speichern()
    {
        _takt.Stop();
        _a.Speichern();
    }

    /// <summary>_form_row: Beschriftung links (340), Feld daneben; schmal steht das Feld darunter.</summary>
    private static Formzeile Zeile(string beschriftung, Control feld) =>
        new(340, 140) { Children = { Schrift.Text(beschriftung, "Klein", FontWeight.Normal, "Pinsel.text_dim"), feld } };

    private StackPanel Checkliste(IEnumerable<(string Schluessel, string Text)> punkte)
    {
        var liste = new StackPanel { Spacing = 4 };
        foreach (var (schluessel, text) in punkte)
        {
            var haken = new CheckBox
            {
                IsChecked = Abschlussprojekt.Abgehakt(_a.Felder, schluessel),
                Content = Schrift.Text(text, "Klein", FontWeight.Normal, "Pinsel.text_soft"),
            };
            Avalonia.Automation.AutomationProperties.SetName(haken, text);
            haken.IsCheckedChanged += (_, _) => _a.Haken(schluessel, haken.IsChecked == true);
            liste.Children.Add(haken);
        }
        return liste;
    }

    // -- Reiter --------------------------------------------------------------------------------

    private Control Uebersicht()
    {
        var eckdaten = new StackPanel { Spacing = 8 };
        foreach (var (feld, name) in Abschlussprojekt.Uebersicht)
        {
            eckdaten.Children.Add(Zeile(name, Feld(feld, name: name)));
        }
        var fachbereich = LernTeile.Themenliste();
        var werte = Fachbereiche.Alle.Prepend(ProjektTexte.KeinFachbereich).ToList();
        fachbereich.ItemsSource = werte;
        fachbereich.SelectedIndex = Math.Max(0, werte.IndexOf(_a.Fachbereich));
        Avalonia.Automation.AutomationProperties.SetName(fachbereich, ProjektTexte.Fachbereich);
        fachbereich.SelectionChanged += (_, _) =>
        {
            if (fachbereich.SelectedItem is string wert && wert != _a.Fachbereich)
            {
                _a.FachbereichSetzen(wert);
            }
        };
        // die Auswahl ist mindestens 260 breit (Themenliste): reicht der Platz daneben nicht, steht sie darunter
        eckdaten.Children.Add(new Formzeile(340, 260)
        {
            Children = { Schrift.Text(ProjektTexte.Fachbereich, "Klein", FontWeight.Normal, "Pinsel.text_dim"), fachbereich },
        });

        var fortschritt = Schrift.Text("", "Normal", FontWeight.Bold);
        var offen = Hinweis("");
        var stand = new StackPanel { Spacing = 8, Children = { fortschritt, Checkliste(Abschlussprojekt.ChecklisteUebersicht), offen } };

        var note = new StackPanel
        {
            Spacing = 6,
            Children = { Hinweis(ProjektTexte.ProjektnoteHinweis), Zeile(ProjektTexte.ErwartetePunkte, Feld(Abschlussprojekt.SchaetzungFeld, breite: 90,
                name: ProjektTexte.ErwartetePunkte)) },
        };

        var vorlage = new StackPanel { Spacing = 8, Children = { Hinweis(ProjektTexte.VorlageHinweis) } };
        var reihe = new WrapPanel();
        var bereich = LernTeile.Themenliste();
        bereich.Margin = new Thickness(0, 0, 8, 8);
        bereich.ItemsSource = Fachbereiche.Alle;
        var auftrag = LernTeile.Themenliste();
        auftrag.Margin = new Thickness(0, 0, 8, 8);
        List<Lerninhalt> auswahl = [];
        void Fuellen()
        {
            var testprojekte = Zustand.Inhalte.IsCompletedSuccessfully ? Zustand.Inhalte.Result?.Testprojekte ?? [] : [];
            auswahl = testprojekte.Where(p => p.Fachbereich == (string?)bereich.SelectedItem).ToList();
            auftrag.ItemsSource = auswahl.Select(p => p.Titel ?? "").ToList();
            auftrag.SelectedIndex = auswahl.Count > 0 ? 0 : -1;
        }
        bereich.SelectionChanged += (_, _) => Fuellen();
        _vorlagenFuellen = Fuellen;
        bereich.SelectedIndex = 0;
        var uebernehmen = LernTeile.Knopf(ProjektTexte.Uebernehmen);
        uebernehmen.Click += (_, _) =>
        {
            if (auftrag.SelectedIndex is >= 0 and var i && i < auswahl.Count)
            {
                Vorlage(auswahl[i]);
            }
        };
        reihe.Children.Add(bereich);
        reihe.Children.Add(auftrag);
        reihe.Children.Add(uebernehmen);
        vorlage.Children.Add(reihe);
        if (!Zustand.Inhalte.IsCompleted)
        {
            _ = Zustand.Inhalte.ContinueWith(_ => Dispatcher.UIThread.Post(Fuellen), TaskScheduler.Default);
        }

        _auffrischen["uebersicht"] = () =>
        {
            fortschritt.Text = _a.ErledigtText();
            offen.Text = _a.AntragText();
        };
        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Karte(ProjektTexte.Eckdaten, ProjektTexte.EckdatenUntertitel, eckdaten),
                Karte(ProjektTexte.Stand, ProjektTexte.StandUntertitel, stand),
                Karte(ProjektTexte.Projektnote, ProjektTexte.ProjektnoteUntertitel, note),
                Karte(ProjektTexte.Vorlage, ProjektTexte.VorlageUntertitel, vorlage),
            },
        };
    }

    private Control Antrag()
    {
        var felder = new StackPanel { Spacing = 2 };
        foreach (var f in Abschlussprojekt.Antrag)
        {
            var titel = Schrift.Text(f.Name, "Normal", FontWeight.Bold);
            titel.Margin = new Thickness(0, 12, 0, 2);
            felder.Children.Add(titel);
            felder.Children.Add(Hinweis(f.Zusatz));
            felder.Children.Add(Textfeld(f.Schluessel, f.Name));
        }
        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Karte(ProjektTexte.Antrag, ProjektTexte.AntragUntertitel, felder),
                Karte(ProjektTexte.AntragPruefen, null, Checkliste(Abschlussprojekt.ChecklisteAntrag(_a.MaxStunden))),
            },
        };
    }

    private Control Zeitplan()
    {
        var zeilen = new StackPanel { Spacing = 8 };
        foreach (var p in Abschlussprojekt.Phasen)
        {
            Control links = p.Schluessel.StartsWith("frei", StringComparison.Ordinal)
                ? new WrapPanel
                {
                    Children =
                    {
                        Feld("phase_name_" + p.Schluessel, breite: 300, name: ProjektTexte.Weitere),
                        Abstand(Hinweis(ProjektTexte.WeiterePhase), 10),
                    },
                }
                : Schrift.Text(p.Name, "Klein", FontWeight.Normal, "Pinsel.text_dim");
            zeilen.Children.Add(new Formzeile(240, 70, rechts: true)
            {
                Children = { links, Feld("phase_" + p.Schluessel, p.Vorgabe != 0 ? p.Vorgabe.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
                    70, p.Name.Length > 0 ? p.Name : ProjektTexte.Weitere) },
            });
        }
        var summe = Schrift.Text("", "Normal", FontWeight.Bold);
        summe.Margin = new Thickness(0, 4, 0, 0);
        var warnung = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.yellow");
        zeilen.Children.Add(summe);
        zeilen.Children.Add(warnung);
        var karte = Karte(ProjektTexte.Zeitplanung, Beschriftungen.Einsetzen(ProjektTexte.ZeitplanUntertitel, _a.MaxStunden), zeilen);
        _auffrischen["zeitplan"] = () =>
        {
            var (text, rot) = _a.SummeText();
            summe.Text = text;
            summe.Farbe(TextBlock.ForegroundProperty, rot ? "Pinsel.red" : "Pinsel.text");
            warnung.Text = _a.WarnungenText();
            warnung.IsVisible = warnung.Text.Length > 0;
        };
        return karte;
    }

    private static T Abstand<T>(T element, double links) where T : Control
    {
        element.Margin = new Thickness(links, 0, 0, 0);
        element.VerticalAlignment = VerticalAlignment.Center;
        return element;
    }

    /// <summary>Betragsfeld (100) mit Abstand und "€" in der Zeile der Sachkosten.</summary>
    private const double SachBetragBreite = 122;

    private Control Kosten()
    {
        var saetze = new StackPanel { Spacing = 8 };
        foreach (var f in Abschlussprojekt.Kostenfelder)
        {
            saetze.Children.Add(Zeile(f.Name, Feld("kosten_" + f.Schluessel, f.Zusatz, 100, f.Name)));
        }
        // Bezeichnung 300 breit, Betrag daneben; reicht die Breite nicht (Handy, grosse Schrift), steht der Betrag darunter
        var kopf = new Formzeile(300, SachBetragBreite, abstand: 8)
        {
            Children =
            {
                Schrift.Text(ProjektTexte.Bezeichnung, "Klein", FontWeight.Bold, "Pinsel.muted"),
                Schrift.Text(ProjektTexte.Betrag, "Klein", FontWeight.Bold, "Pinsel.muted"),
            },
        };
        var sach = new StackPanel { Spacing = 6, Children = { kopf } };
        for (var n = 0; n < Abschlussprojekt.Sachkostenzeilen; n++)
        {
            var nummer = Beschriftungen.Einsetzen(ProjektTexte.Position, n + 1);
            var betrag = Feld("material_betrag_" + n, breite: 100, name: ProjektTexte.Betrag + " " + nummer);
            var zeile = new Formzeile(300, SachBetragBreite, abstand: 8)
            {
                Children =
                {
                    Feld("material_name_" + n, name: ProjektTexte.Bezeichnung + " " + nummer),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children = { betrag, Abstand(Hinweis(ProjektTexte.Euro), 6) },
                    },
                },
            };
            sach.Children.Add(zeile);
        }
        var ergebnis = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.text_soft");
        _auffrischen["kosten"] = () => ergebnis.Text = _a.KostenText();
        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Karte(ProjektTexte.Kostenrechnung, ProjektTexte.KostenUntertitel, saetze),
                Karte(ProjektTexte.Sachkosten, ProjektTexte.SachkostenUntertitel, sach),
                Karte(ProjektTexte.Ergebnis, null, ergebnis),
            },
        };
    }

    private Control Doku()
    {
        var inhalt = new StackPanel { Spacing = 2 };
        inhalt.Children.Add(Zeile(ProjektTexte.Seitenvorgabe, Feld(Abschlussprojekt.SeitenFeld,
            Abschlussprojekt.SeitenVorgabe.ToString(System.Globalization.CultureInfo.InvariantCulture), 70, ProjektTexte.Seitenvorgabe)));
        var seiten = Schrift.Text("", "Normal", FontWeight.Bold);
        seiten.Margin = new Thickness(0, 8, 0, 0);
        inhalt.Children.Add(seiten);
        inhalt.Children.Add(Hinweis(Beschriftungen.Einsetzen(ProjektTexte.SeitenHinweis, Abschlussprojekt.ZeichenJeSeite)));
        foreach (var k in Abschlussprojekt.Kapitel)
        {
            var titel = Schrift.Text(k.Name, "Normal", FontWeight.Bold);
            titel.Margin = new Thickness(0, 12, 0, 2);
            inhalt.Children.Add(titel);
            inhalt.Children.Add(Hinweis(k.Zusatz));
            inhalt.Children.Add(Textfeld("kapitel_" + k.Schluessel, k.Name, 4, 18));
        }
        _auffrischen["doku"] = () =>
        {
            var (text, rot) = _a.SeitenText();
            seiten.Text = text;
            seiten.Farbe(TextBlock.ForegroundProperty, rot ? "Pinsel.red" : "Pinsel.text");
        };
        return Karte(ProjektTexte.Dokumentation, ProjektTexte.DokuUntertitel, inhalt);
    }

    private Control Fachgespraech()
    {
        var praesentation = Karte(ProjektTexte.Praesentation, ProjektTexte.PraesentationUntertitel, Checkliste(Abschlussprojekt.ChecklistePraesentation));
        var stand = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.text_dim");
        var frage = Schrift.Text("", "Mittel", FontWeight.SemiBold);
        frage.Margin = new Thickness(0, 6, 0, 0);
        var status = Schrift.Text("", "Klein", FontWeight.Bold, "Pinsel.muted");
        var hinweis = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.text_soft");
        var umschalten = LernTeile.Knopf(ProjektTexte.HinweisZeigen, true);
        var weiter = LernTeile.Knopf(ProjektTexte.NaechsteFrage);
        var gewusst = LernTeile.Knopf(ProjektTexte.Gewusst, true);
        var nichtGewusst = LernTeile.Knopf(ProjektTexte.NichtGewusst);
        var wusstest = Schrift.Text(ProjektTexte.WusstestDu, "Normal", FontWeight.Normal, "Pinsel.text_dim");
        wusstest.VerticalAlignment = VerticalAlignment.Center;
        wusstest.Margin = new Thickness(0, 0, 12, 8);
        var bewerten = new WrapPanel { IsVisible = false, Margin = new Thickness(0, 4, 0, 0), Children = { wusstest, gewusst, nichtGewusst } };
        var anzahl = _a.AktuelleFragen().Count;
        var uebung = Karte(ProjektTexte.FachgespraechUeben, Beschriftungen.Einsetzen(ProjektTexte.FragenUntertitel, anzahl), new StackPanel
        {
            Spacing = 6,
            Children = { stand, frage, status, hinweis, new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { umschalten, weiter } }, bewerten },
        });
        void Malen()
        {
            if (_a.AktuelleFrage() is not { } aktuell)
            {
                return;
            }
            frage.Text = aktuell.Frage.Frage;
            status.Text = aktuell.Bewertung switch { true => ProjektTexte.Gewusst, false => ProjektTexte.NichtGewusst, _ => ProjektTexte.NichtBewertet };
            status.Farbe(TextBlock.ForegroundProperty, aktuell.Bewertung switch { true => "Pinsel.green", false => "Pinsel.red", _ => "Pinsel.muted" });
            hinweis.Text = _a.HinweisOffen ? aktuell.Frage.Hinweis : "";
            hinweis.IsVisible = _a.HinweisOffen;
            ((TextBlock)umschalten.Content!).Text = _a.HinweisOffen ? ProjektTexte.HinweisAusblenden : ProjektTexte.HinweisZeigen;
            bewerten.IsVisible = _a.HinweisOffen;
            stand.Text = _a.FragenStand();
        }
        umschalten.Click += (_, _) =>
        {
            _a.HinweisUmschalten();
            Malen();
        };
        weiter.Click += (_, _) =>
        {
            _a.Weiter();
            Malen();
        };
        gewusst.Click += (_, _) =>
        {
            _a.Bewerten(true);
            Malen();
        };
        nichtGewusst.Click += (_, _) =>
        {
            _a.Bewerten(false);
            Malen();
        };
        if (anzahl > 0)
        {
            _auffrischen["fachgespraech"] = Malen;
        }
        return new StackPanel { Spacing = 14, Children = { praesentation, uebung } };
    }

    // -- Aktionen ------------------------------------------------------------------------------

    private async Task LoeschenAsync()
    {
        var (titel, text) = _a.LoeschenFrage();
        if (await Dialog.Zeigen(titel, text, TrainerTexte.Ja, TrainerTexte.Nein) == 0)
        {
            _takt.Stop();
            _a.Loeschen();
        }
    }

    /// <summary>use_template (Reiter Übersicht oder Knopf auf der Seite Testprojekt).</summary>
    public void Vorlage(Lerninhalt testprojekt)
    {
        _takt.Stop();
        Kurzhinweis.Zeigen(_a.Vorlage(testprojekt));
    }

    /// <summary>
    /// _export: speichern, Datei-Dialog (PC und Handy gleich), PDF oder Text (PC CRLF + BOM, Handy LF
    /// ohne BOM). Misslingt das Schreiben: Nr 28, Einzelheiten nur ins Fehlerprotokoll.
    /// </summary>
    private async Task ExportAsync(string endung)
    {
        if (_exportLaeuft)
        {
            return;               // Doppelklick: ein Dialog genuegt
        }
        _exportLaeuft = true;
        try
        {
            await ExportSchreibenAsync(endung);
        }
        finally
        {
            _exportLaeuft = false;
        }
    }

    private bool _exportLaeuft;

    private async Task ExportSchreibenAsync(string endung)
    {
        _takt.Stop();
        var vorschlag = _a.ExportName(endung);
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }
        IStorageFile? datei;
        try
        {
            datei = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = ProjektTexte.DialogTitel,
                SuggestedFileName = vorschlag,
                DefaultExtension = endung,
                FileTypeChoices = [endung == "pdf"
                    ? new FilePickerFileType(ProjektTexte.PdfTyp) { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }
                    : new FilePickerFileType(ProjektTexte.TextTyp) { Patterns = ["*.txt"], MimeTypes = ["text/plain"] }],
            });
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            await Dialog.Zeigen(ProjektTexte.SpeichernFehlgeschlagen, ProjektTexte.DateiNichtGespeichert, Dialog.Ok);
            return;
        }
        if (datei is null)
        {
            return;
        }
        var name = datei.Name;    // vor Dispose lesen
        try
        {
            var daten = _a.ExportDaten(endung, Handy, DateTime.Now);
            await using var strom = await datei.OpenWriteAsync();
            if (strom.CanSeek)
            {
                strom.SetLength(0);
            }
            await strom.WriteAsync(daten);
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            await Dialog.Zeigen(ProjektTexte.SpeichernFehlgeschlagen, ProjektTexte.DateiNichtGespeichert, Dialog.Ok);
            return;
        }
        finally
        {
            datei.Dispose();
        }
        Kurzhinweis.Zeigen(Beschriftungen.Einsetzen(ProjektTexte.Gespeichert, name));
    }

    /// <summary>on_show (beim Zeigen und nach dem Abgleich): nachladen, wenn nichts offen ist.</summary>
    public void Gezeigt()
    {
        if (!_a.Zeigen() && _gebautMit != _a.MaxStunden)
        {
            Neuaufbau();          // andere Ausbildung: Stundentexte der Reiter neu
        }
    }

    /// <summary>Nur fuer den Start ohne Bedienung: Reiter zeigen (mit Speichern davor wie ein Klick).</summary>
    internal void Probe(string reiter) => ReiterZeigen(reiter);

    /// <summary>Nur fuer den Start ohne Bedienung: Hinweis der Frage zeigen bzw. schliessen (ohne zu bewerten).</summary>
    internal void ProbeHinweis(bool offen)
    {
        if (_a.HinweisOffen != offen)
        {
            _a.HinweisUmschalten();
        }
        Auffrischen();
    }

    /// <summary>Nur fuer den Start ohne Bedienung: ein Projekt laden (Projektwechsel messen), ohne Lernstand zu schreiben.</summary>
    internal void ProbeLaden(string? projekt) => _a.Laden(projekt);
}

/// <summary>
/// _form_row mit FlowRow (D2): Beschriftung links mit fester Breite (x Schriftfaktor), Feld daneben
/// (rechts: am rechten Rand); reicht die Breite nicht fuer Beschriftung und Feld, steht das Feld darunter.
/// </summary>
public sealed class Formzeile(double beschriftung, double feld, bool rechts = false, double abstand = 10) : Panel
{
    private bool Nebeneinander(double breite) =>
        Children.Count < 2 || double.IsInfinity(breite) || breite >= (beschriftung + feld) * Schrift.Faktor + abstand;

    protected override Size MeasureOverride(Size verfuegbar)
    {
        if (Children.Count < 2)
        {
            Children.FirstOrDefault()?.Measure(verfuegbar);
            return Children.FirstOrDefault()?.DesiredSize ?? default;
        }
        if (Nebeneinander(verfuegbar.Width))
        {
            var links = beschriftung * Schrift.Faktor;
            var rest = double.IsInfinity(verfuegbar.Width) ? double.PositiveInfinity : verfuegbar.Width - links - abstand;
            Children[0].Measure(new Size(rechts && !double.IsInfinity(rest) ? Math.Max(links, verfuegbar.Width - abstand - Feldbreite(rest)) : links,
                double.PositiveInfinity));
            Children[1].Measure(new Size(rest, double.PositiveInfinity));
            var breite = double.IsInfinity(verfuegbar.Width) ? links + abstand + Children[1].DesiredSize.Width : verfuegbar.Width;
            return new Size(breite, Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
        }
        Children[0].Measure(new Size(verfuegbar.Width, double.PositiveInfinity));
        Children[1].Measure(new Size(verfuegbar.Width, double.PositiveInfinity));
        return new Size(verfuegbar.Width, Children[0].DesiredSize.Height + 4 + Children[1].DesiredSize.Height);
    }

    private double Feldbreite(double rest)
    {
        Children[1].Measure(new Size(rest, double.PositiveInfinity));
        return Children[1].DesiredSize.Width;
    }

    protected override Size ArrangeOverride(Size endgroesse)
    {
        if (Children.Count < 2)
        {
            Children.FirstOrDefault()?.Arrange(new Rect(endgroesse));
            return endgroesse;
        }
        if (Nebeneinander(endgroesse.Width))
        {
            var links = beschriftung * Schrift.Faktor;
            var hoehe = endgroesse.Height;
            if (rechts)
            {
                var w = Children[1].DesiredSize.Width;
                Children[0].Arrange(new Rect(0, 0, Math.Max(0, endgroesse.Width - w - abstand), hoehe));
                Children[1].Arrange(new Rect(endgroesse.Width - w, 0, w, Children[1].DesiredSize.Height));
            }
            else
            {
                Children[0].Arrange(new Rect(0, 0, links, hoehe));
                Children[1].Arrange(new Rect(links + abstand, 0, Math.Max(0, endgroesse.Width - links - abstand), Children[1].DesiredSize.Height));
            }
            return endgroesse;
        }
        var oben = Children[0].DesiredSize.Height;
        Children[0].Arrange(new Rect(0, 0, endgroesse.Width, oben));
        Children[1].Arrange(new Rect(0, oben + 4, endgroesse.Width, Children[1].DesiredSize.Height));
        return endgroesse;
    }
}
