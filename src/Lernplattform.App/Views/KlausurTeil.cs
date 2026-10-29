using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, W2 (PB5-PB7): Art "Prüfung" im Pruefungstrainer wie app_gui.ExamPanel v0.63.2
/// (3379-3915) und overall_card (4109-4151), PC-Wortlaut auf PC und Handy (Wortliste W2).
/// Auswahl je Fachrichtung (AE, DPA, DV: b1/b2 gesperrt), laufende Pruefung mit Uhr ohne Pause
/// (Zeit ueber start_utc), Rueckfragen mit "Ja"/"Nein", Selbstbewertung, Ergebnis mit "Nach
/// Themen". Abschluss in einer Buchung (Abschluss.Abschliessen, E6); misslingt er, bleibt die
/// Pruefung in Phase "bewertung" und die Bewertung stehen (Weisung P4-6, Frage 4 a).
/// </summary>
public sealed class KlausurTeil
{
    private static readonly bool Pc = !OperatingSystem.IsAndroid();

    private readonly StackPanel _koerper = new() { Spacing = 14 };
    private readonly KlausurAblauf _a;
    private readonly Func<string, Task> _themaUeben;
    private readonly DispatcherTimer _takt = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _speichern = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private readonly Dictionary<string, TextBox> _felder = new(StringComparer.Ordinal);
    private int _aufgabe;
    private int _wisoSeite;
    private TextBlock? _uhr;
    private TextBlock? _fortschritt;
    private TextBlock? _summe;
    private StackPanel? _aufgabenTeil;
    private bool _abgeben;

    public KlausurTeil(Lerninhalte inhalte, Func<string, Task> themaUeben)
    {
        _a = new KlausurAblauf(new KlausurInhalte(inhalte, Zustand.Umbenennungen),
            new LaufendePruefung(() => Zustand.Einstellungen.Laden(), Zustand.Aendere), () => Zustand.Schreiber,
            () => DateTimeOffset.Now, TimeZoneInfo.Local);
        _themaUeben = themaUeben;
        _takt.Tick += (_, _) => Ticken();
        _speichern.Tick += (_, _) => Festhalten();
        Zustand.BeimBeenden += Verbergen;      // Antworten beim Beenden festhalten
        // W4 (Sammelliste 51): vor Sicherung und Uebernahme und beim Verlassen der App am Handy sofort festhalten (flush_inputs)
        Zustand.BeiEingabenSpeichern += Festhalten;
    }

    public Control Teil => _koerper;

    /// <summary>Phase 9 W2: vor dem Verwerfen der Seite (Wechsel der Fachrichtung) festhalten und abmelden.</summary>
    public void Abmelden()
    {
        Verbergen();
        Zustand.BeimBeenden -= Verbergen;
        Zustand.BeiEingabenSpeichern -= Festhalten;
    }

    /// <summary>Gespeicherte laufende Pruefung (auch "bewertung")? (fp.load_running() is not None)</summary>
    public bool Laeuft => _a.Laeuft;

    /// <summary>Bewertung wird gezeigt (grading).</summary>
    public bool Bewertet { get; private set; }

    private KlausurInhalte Inhalte => _a.Inhalte;

    private Fachrichtung Fachrichtung => Fachrichtung.Nach(Ausbildung.Aktuell(Zustand.Schreiber));

    // -- Allgemein ----------------------------------------------------------------------------

    private void Leeren()
    {
        Bewertet = false;
        _takt.Stop();
        _felder.Clear();
        _uhr = _fortschritt = _summe = null;
        _aufgabenTeil = null;
        _koerper.Children.Clear();
    }

    /// <summary>show: laufende Pruefung fortsetzen (abgelaufen: abgeben) oder Auswahl zeigen.</summary>
    public void Zeigen() => _ = SchrittAsync(_a.Zeigen());

    /// <summary>hide: geplantes Speichern jetzt erledigen, Uhr anhalten.</summary>
    public void Verbergen()
    {
        Festhalten();
        _takt.Stop();
    }

    /// <summary>
    /// Ansicht nach einem Schritt des Ablaufs. Hinweis "Zeit abgelaufen" bei offenen Aufgaben vor
    /// der Bewertung, bei WiSo nach dem Ergebnis (wie Python). "Nicht gespeichert": die Bewertung
    /// bleibt stehen (WiSo: Karte mit dem Hinweis).
    /// </summary>
    private async Task SchrittAsync(KlausurSchritt schritt)
    {
        var nichtGespeichert = schritt.Hinweis == TrainerTexte.NichtGespeichert;
        if (nichtGespeichert)
        {
            if (Bewertet && !_a.Wiso)
            {
                await Dialog.Zeigen(schritt.HinweisTitel!, schritt.Hinweis!, Dialog.Ok);
            }
            else
            {
                BewertungZeigen();
            }
            return;
        }
        if (schritt.Ansicht == KlausurAnsicht.Bewertung && schritt.Hinweis is { } vorher)
        {
            await Dialog.Zeigen(schritt.HinweisTitel!, vorher, Dialog.Ok);
        }
        _aufgabe = 0;
        switch (schritt.Ansicht)
        {
            case KlausurAnsicht.Auswahl:
                AuswahlZeigen();
                break;
            case KlausurAnsicht.Lauf:
                _wisoSeite = 0;
                LaufZeigen();
                break;
            case KlausurAnsicht.Bewertung:
                BewertungZeigen();
                break;
            case KlausurAnsicht.Ergebnis:
                if (!_a.Probe)
                {
                    AbgleichDienst.NachDemLernen();        // notify_progress
                }
                ErgebnisZeigen(_a.ErgebnisArt!, _a.Ergebnis!);
                break;
        }
        if (schritt.Ansicht != KlausurAnsicht.Bewertung && schritt.Hinweis is { } nachher)
        {
            await Dialog.Zeigen(schritt.HinweisTitel!, nachher, Dialog.Ok);
        }
    }

    // -- Auswahl ------------------------------------------------------------------------------

    /// <param name="probe">Nur Start ohne Bedienung: Fachrichtung, Verlauf und Projektnote statt der gespeicherten.</param>
    private void AuswahlZeigen((Fachrichtung Fachrichtung, List<KlausurEintrag> Verlauf, double? Projekt)? probe = null)
    {
        Leeren();
        var fachrichtung = probe?.Fachrichtung ?? Fachrichtung;
        var intro = new StackPanel { Spacing = 6 };
        intro.Children.Add(new LernText(Beschriftungen.Einsetzen(PruefungTexte.Einleitung, TrainerTexte.NoteLang), "Klein",
            FontWeight.Normal, "Pinsel.text_soft"));
        intro.Children.Add(Schrift.Text(TrainerTexte.HinweisKurz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        foreach (var bereich in fachrichtung.Bereiche.Where(b => b.Rolle != "projekt"))
        {
            intro.Children.Add(Startzeile(bereich));
        }
        _koerper.Children.Add(Karte(PruefungTexte.AuswahlTitel, PruefungTexte.AuswahlUntertitel, intro));
        var verlauf = probe?.Verlauf ?? Gesamtstand.Verlauf(Zustand.Lernstand);
        _koerper.Children.Add(VerlaufKarte(verlauf));
        var projekt = probe is { } p ? p.Projekt
            : Gesamtstand.Projektschaetzung(Zustand.Schreiber.ProjektFelder(), Json.Text(Zustand.Werte[Gesamtstand.AktivSchluessel]));
        _koerper.Children.Add(GesamtKarte(fachrichtung, verlauf, projekt));
    }

    private Border Startzeile(Pruefungsbereich bereich)
    {
        var angabe = bereich.Art == Klausur.Wiso
            ? Beschriftungen.Einsetzen(PruefungTexte.AngabeWiso, bereich.Minuten, Klausur.WisoAnzahl)
            : Beschriftungen.Einsetzen(PruefungTexte.AngabeOffen, bereich.Minuten);
        var text = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new LernText(bereich.Name, "Normal", FontWeight.Bold, "Pinsel.text"),
                Schrift.Text(angabe, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        };
        // Phase 9 W2: b1/b2 von AE, DPA und DV erst, wenn jedes Thema mindestens eine Aufgabe hat
        var eigene = Klausur.QuelleFachrichtung(bereich.Quelle) is not null;
        if (Klausur.Art(bereich.Art) is { } art && bereich.Quelle != "keine" && (!eigene || Inhalte.Spielbar(art.Art)))
        {
            var knopf = LernTeile.Knopf(Beschriftungen.Einsetzen(PruefungTexte.Starten, art.Kurz), true);
            knopf.VerticalAlignment = VerticalAlignment.Center;
            knopf.Click += async (_, _) => await StartenAsync(art.Art);
            return Zeile(new NebenOderUnter(220, 14) { Children = { text, knopf } }, "Pinsel.card_alt", new Thickness(14, 10));
        }
        // AE, DPA, DV: fuer die Bereiche b1/b2 gibt es noch keine Aufgaben (Phase 9 W2: eigener Text)
        text.Children.Add(new LernText(eigene ? FachrichtungTexte.KlausurOhneAufgaben : Beschriftungen.Platzhalter, "Klein",
            FontWeight.Normal, "Pinsel.text_dim"));
        return Zeile(text, "Pinsel.card_alt", new Thickness(14, 10));
    }

    private static Border VerlaufKarte(List<KlausurEintrag> verlauf)
    {
        var inhalt = new StackPanel { Spacing = 2 };
        if (verlauf.Count == 0)
        {
            inhalt.Children.Add(Schrift.Text(PruefungTexte.KeinePruefung, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        foreach (var zeile in Gesamtstand.Letzte(verlauf))
        {
            var note = Schrift.Text(zeile.Note, "Klein", FontWeight.Bold, LernTeile.Pinsel(Gesamtstand.Notenfarbe(zeile.Wert)));
            inhalt.Children.Add(new WrapPanel
            {
                Children =
                {
                    Schrift.Text(zeile.Datum, "Klein", FontWeight.Normal, "Pinsel.muted").Breite(150),
                    Schrift.Text(zeile.Name, "Klein", FontWeight.Bold).Breite(110),
                    Schrift.Text(zeile.Punkte, "Klein", FontWeight.Normal, "Pinsel.text_soft").Breite(110),
                    note,
                },
            });
        }
        return Karte(PruefungTexte.LetztePruefungen, Beschriftungen.Einsetzen(PruefungTexte.Insgesamt, verlauf.Count), inhalt);
    }

    /// <summary>overall_card: letzte Pruefung je Bereich plus Projektnote des aktiven Projekts.</summary>
    private static Border GesamtKarte(Fachrichtung fachrichtung, List<KlausurEintrag> verlauf, double? projekt)
    {
        var kachel = Gesamtstand.Kachel(fachrichtung, verlauf, projekt);
        var inhalt = new StackPanel { Spacing = 2 };
        inhalt.Children.Add(Schrift.Text(string.Join("\n", kachel.Zeilen), "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        if (kachel.Hinweis is { } hinweis)
        {
            inhalt.Children.Add(Abstand(Schrift.Text(hinweis, "Klein", FontWeight.Normal, "Pinsel.muted"), 10));
        }
        else
        {
            var ergebnis = kachel.Ergebnis;
            var farbe = ergebnis.Bestanden ? "Pinsel.green" : "Pinsel.red";
            inhalt.Children.Add(Abstand(Schrift.Text(kachel.GesamtZeile!, "Normal", FontWeight.Bold, farbe), 10));
            foreach (var regel in ergebnis.Regeln)
            {
                inhalt.Children.Add(Schrift.Text(Gesamtstand.Regel(regel), "Klein", FontWeight.Normal, regel.Ok ? "Pinsel.green" : "Pinsel.red"));
            }
            if (ergebnis.Ergaenzung.Length > 0)
            {
                inhalt.Children.Add(Abstand(Schrift.Text(ergebnis.Ergaenzung, "Klein", FontWeight.Normal, "Pinsel.yellow"), 8));
            }
        }
        return Karte(PruefungTexte.Gesamtergebnis, PruefungTexte.Gewichtung, inhalt);
    }

    /// <summary>
    /// start: laeuft schon eine Pruefung (auch "bewertung"), wird sie gezeigt und nie
    /// ueberschrieben, vor und nach der Rueckfrage.
    /// </summary>
    private async Task StartenAsync(string art)
    {
        if (_a.Laeuft)
        {
            Zeigen();
            return;
        }
        var bereich = Klausur.Art(art)!;
        var frage = Beschriftungen.Einsetzen(Geraetetexte.PruefungStartFrage(Pc), bereich.Name, bereich.Minuten);
        if (await Dialog.Zeigen(PruefungTexte.StartTitel, frage, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        await SchrittAsync(_a.Starten(art));
    }

    // -- Laufende Pruefung --------------------------------------------------------------------

    private void LaufZeigen()
    {
        Leeren();
        var art = Klausur.Art(_a.Art)!;
        _uhr = Schrift.Text("", "Zahl", FontWeight.Bold, "Pinsel.accent");
        var verbleibend = Schrift.Text(PruefungTexte.Verbleibend, "Klein", FontWeight.Normal, "Pinsel.muted");
        verbleibend.VerticalAlignment = VerticalAlignment.Bottom;
        verbleibend.Margin = new Thickness(10, 0, 0, 6);
        var abgeben = LernTeile.Knopf(PruefungTexte.Abgeben, true);
        abgeben.Click += async (_, _) => await AbgebenAsync(auto: false);
        var abbrechen = LernTeile.Knopf(PruefungTexte.Abbrechen);
        abbrechen.Click += async (_, _) => await AbbrechenAsync();
        _fortschritt = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        var kopf = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new WrapPanel { Children = { new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 8), Children = { _uhr, verbleibend } }, abgeben, abbrechen } },
                _fortschritt,
            },
        };
        _koerper.Children.Add(Karte(art.Kurz, art.Name, kopf));
        _aufgabenTeil = new StackPanel { Spacing = 14 };
        if (_a.Wiso)
        {
            _koerper.Children.Add(_aufgabenTeil);
            WisoMalen();
        }
        else
        {
            _koerper.Children.Add(Aufgabenpillen(n =>
            {
                Festhalten();
                _aufgabe = n;
                AufgabeMalen();
            }));
            _koerper.Children.Add(_aufgabenTeil);
            AufgabeMalen();
        }
        Ticken();
        if (_a.ImLauf && !_a.Probe)
        {
            _takt.Start();
        }
    }

    private Pillen Aufgabenpillen(Action<int> gewechselt)
    {
        var anzahl = (_a.Zustand!["aufgaben"] as JsonArray)?.Count ?? 0;
        var optionen = Enumerable.Range(0, anzahl).Select(n => (Nummer(n), Beschriftungen.Einsetzen(PruefungTexte.AufgabeNummer, n + 1)));
        return new Pillen(optionen, Nummer(_aufgabe), w => gewechselt(int.Parse(w, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static string Nummer(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>_tick: Restzeit (rot unter 5 Minuten); bei 0 automatisch abgeben.</summary>
    private void Ticken()
    {
        if (_a.Zustand is null || _uhr is null)
        {
            _takt.Stop();
            return;
        }
        var rest = _a.Rest;
        _uhr.Text = Klausur.Zeittext(rest);
        _uhr.Farbe(TextBlock.ForegroundProperty, rest < 300 ? "Pinsel.red" : "Pinsel.accent");
        if (rest <= 0 && _a.ImLauf && !_a.Probe && !_abgeben)    // offene Rueckfrage: sie gibt danach ab
        {
            _takt.Stop();
            _ = AbgebenAsync(auto: true);
        }
    }

    private void FortschrittZeigen()
    {
        if (_fortschritt is not null && _a.Zustand is { } zustand)
        {
            _fortschritt.Text = Klausur.Fortschritt(Inhalte, zustand);
        }
    }

    private void AufgabeMalen()
    {
        var zustand = _a.Zustand!;
        _aufgabenTeil!.Children.Clear();
        _felder.Clear();
        var details = Klausur.Details(Inhalte, zustand, _aufgabe)!;
        var inhalt = new StackPanel { Spacing = 6 };
        if (details.Einleitung.Length > 0)
        {
            inhalt.Children.Add(new LernText(details.Einleitung, "Normal", FontWeight.Normal, "Pinsel.text_soft"));
        }
        for (var teil = 0; teil < details.Teile.Count; teil++)
        {
            var t = details.Teile[teil];
            var schluessel = Klausur.Schluessel(_aufgabe, teil);
            inhalt.Children.Add(Abstand(new LernText(Teiltitel(teil, t), "Normal", FontWeight.Bold, "Pinsel.text"), 14));
            var feld = new TextBox
            {
                AcceptsReturn = true,
                NewLine = "\n",
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 96,
                MaxHeight = 420,
                Text = Klausur.Antwort(zustand, schluessel),
            }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
            Avalonia.Automation.AutomationProperties.SetName(feld, Teiltitel(teil, t));
            feld.TextChanged += (_, _) =>
            {
                // _schedule_save: 1,2 s nach der letzten Eingabe speichern
                _speichern.Stop();
                _speichern.Start();
            };
            _felder[schluessel] = feld;
            inhalt.Children.Add(feld);
        }
        _aufgabenTeil.Children.Add(Karte(Beschriftungen.Einsetzen(PruefungTexte.AufgabeNummer, _aufgabe + 1),
            Beschriftungen.Einsetzen(PruefungTexte.AufgabeUntertitel, details.Titel), inhalt));
        FortschrittZeigen();
    }

    private static string Teiltitel(int teil, Teilaufgabe t) =>
        Beschriftungen.Einsetzen(PruefungTexte.Teilaufgabe, ((char)('a' + teil)).ToString(), t.Text, t.Punkte);

    private void WisoMalen()
    {
        var zustand = _a.Zustand!;
        _aufgabenTeil!.Children.Clear();
        var fragen = (zustand["fragen"] as JsonArray ?? []).Select(f => Json.Text(f) ?? "").ToList();
        var seiten = (fragen.Count + Klausur.WisoSeite - 1) / Klausur.WisoSeite;
        var antworten = zustand["antworten"] as JsonObject ?? [];
        var optionen = zustand["optionen"] as JsonObject ?? [];
        var inhalt = new StackPanel { Spacing = 4 };
        var start = _wisoSeite * Klausur.WisoSeite;
        for (var nummer = start; nummer < Math.Min(fragen.Count, start + Klausur.WisoSeite); nummer++)
        {
            var frage = fragen[nummer];
            inhalt.Children.Add(Abstand(new LernText(Beschriftungen.Einsetzen(PruefungTexte.FrageNummer, nummer + 1, frage), "Normal",
                FontWeight.Bold, "Pinsel.text"), 12));
            var liste = new StackPanel { Spacing = 6 };
            var texte = (optionen[frage] as JsonArray)?.Select(o => Json.Text(o) ?? "").ToList()
                ?? Inhalte.Quizfrage(frage)?.Optionen.ToList() ?? [];
            var gewaehlt = Json.Text(antworten[frage]);
            foreach (var text in texte)
            {
                liste.Children.Add(Option(text, "wiso" + Nummer(nummer), gewaehlt is { Length: > 0 } && text == gewaehlt, () =>
                {
                    _a.Waehlen(frage, text);
                    FortschrittZeigen();
                }));
            }
            inhalt.Children.Add(liste);
        }
        _aufgabenTeil.Children.Add(Karte(PruefungTexte.WisoTitel, Beschriftungen.Einsetzen(PruefungTexte.Seite, _wisoSeite + 1, seiten), inhalt));
        var zurueck = LernTeile.Knopf(PruefungTexte.Zurueck);
        zurueck.IsEnabled = _wisoSeite > 0;
        zurueck.Click += (_, _) => Blaettern(-1);
        var weiter = LernTeile.Knopf(PruefungTexte.Weiter);
        weiter.IsEnabled = _wisoSeite < seiten - 1;
        weiter.Click += (_, _) => Blaettern(1);
        _aufgabenTeil.Children.Add(new WrapPanel { Children = { zurueck, weiter } });
        FortschrittZeigen();
    }

    /// <summary>Antwort wie LernTeile.Option (OptionList), Text mit LernText (sehr lange Woerter der WiSo-Antworten).</summary>
    private static Border Option(string text, string gruppe, bool gewaehlt, Action gewechselt)
    {
        var wahl = new RadioButton
        {
            GroupName = gruppe,
            Content = new LernText(text, "Normal", FontWeight.Normal, "Pinsel.text_soft"),
            Tag = text,
            IsChecked = gewaehlt,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Avalonia.Automation.AutomationProperties.SetName(wahl, text);
        wahl.IsCheckedChanged += (_, _) =>
        {
            if (wahl.IsChecked == true)
            {
                gewechselt();
            }
        };
        return new Border
        {
            Child = wahl,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 4),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private void Blaettern(int schritt)
    {
        _wisoSeite += schritt;
        WisoMalen();
        _koerper.Children[0].BringIntoView();      // to_top
    }

    /// <summary>_flush_answers: geaenderte Antworten der offenen Aufgabe speichern.</summary>
    private void Festhalten()
    {
        _speichern.Stop();
        if (_a.Festhalten(_felder.Select(f => (f.Key, f.Value.Text))))
        {
            FortschrittZeigen();
        }
    }

    private async Task AbbrechenAsync()
    {
        var zustand = _a.Zustand;
        if (await Dialog.Zeigen(PruefungTexte.AbbrechenTitel, PruefungTexte.AbbrechenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0
            || _a.Zustand != zustand || !_a.ImLauf)     // inzwischen abgegeben (Zeit abgelaufen): nichts verwerfen
        {
            return;
        }
        _speichern.Stop();
        await SchrittAsync(_a.Abbrechen());
    }

    /// <summary>_submit: nach Rueckfrage (nicht bei abgelaufener Zeit) abgeben; WiSo gleich auswerten.</summary>
    private async Task AbgebenAsync(bool auto)
    {
        if (_abgeben || _a.Zustand is not { } zustand || !_a.ImLauf)
        {
            return;
        }
        _abgeben = true;
        try
        {
            var nein = !auto && await Dialog.Zeigen(PruefungTexte.AbgebenTitel, PruefungTexte.AbgebenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0;
            if (_a.Zustand != zustand || !_a.ImLauf)
            {
                return;
            }
            // Lief die Zeit waehrend der Rueckfrage ab, wird trotzdem abgegeben (keine Pause), mit Hinweis
            auto = auto || _a.Rest <= 0;
            if (nein && !auto)
            {
                return;
            }
            Festhalten();
            _takt.Stop();
            await SchrittAsync(_a.Abgeben(auto));
        }
        finally
        {
            _abgeben = false;
        }
    }

    // -- Bewertung ----------------------------------------------------------------------------

    private void BewertungZeigen()
    {
        Leeren();
        Bewertet = true;
        var art = Klausur.Art(_a.Art)!;
        var abschliessen = LernTeile.Knopf(PruefungTexte.BewertungAbschliessen, true);
        abschliessen.Click += async (_, _) => await AbschliessenAsync();
        var kopf = new StackPanel { Spacing = 10 };
        if (_a.Wiso)
        {
            // Nur nach einem misslungenen Abschluss: WiSo hat keine Bewertung, hier wird nur neu gespeichert
            kopf.Children.Add(Schrift.Text(TrainerTexte.NichtGespeichert, "Klein", FontWeight.Normal, "Pinsel.red"));
            abschliessen.HorizontalAlignment = HorizontalAlignment.Left;
            kopf.Children.Add(abschliessen);
            _koerper.Children.Add(Karte(PruefungTexte.BewertungTitel, art.Name, kopf));
            return;
        }
        _summe = Schrift.Text("", "Normal", FontWeight.Bold);
        _summe.VerticalAlignment = VerticalAlignment.Center;
        kopf.Children.Add(new LernText(PruefungTexte.BewertungAnleitung, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        kopf.Children.Add(new NebenOderUnter(160, 14) { Children = { _summe, abschliessen } });
        _koerper.Children.Add(Karte(PruefungTexte.BewertungTitel, art.Name, kopf));
        _koerper.Children.Add(Aufgabenpillen(n =>
        {
            _aufgabe = n;
            BewertungMalen();
            _koerper.Children[0].BringIntoView();
        }));
        _aufgabenTeil = new StackPanel { Spacing = 14 };
        _koerper.Children.Add(_aufgabenTeil);
        BewertungMalen();
    }

    private void BewertungMalen()
    {
        var zustand = _a.Zustand!;
        _aufgabenTeil!.Children.Clear();
        var aufgabe = _aufgabe;
        var details = Klausur.Details(Inhalte, zustand, aufgabe)!;
        var inhalt = new StackPanel { Spacing = 4 };
        for (var teil = 0; teil < details.Teile.Count; teil++)
        {
            var t = details.Teile[teil];
            var nr = teil;
            var schluessel = Klausur.Schluessel(aufgabe, teil);
            inhalt.Children.Add(Abstand(new LernText(Teiltitel(teil, t), "Normal", FontWeight.Bold, "Pinsel.text"), 14));
            inhalt.Children.Add(Abstand(LernTeile.Label(PruefungTexte.DeineAntwort), 6));
            var eigene = Klausur.Antwort(zustand, schluessel);
            inhalt.Children.Add(new Border
            {
                Child = new LernText(eigene.Length > 0 ? eigene : PruefungTexte.KeineAntwort, "Normal", FontWeight.Normal, "Pinsel.text_soft"),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8),
                Margin = new Thickness(0, 4, 0, 8),
            }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border"));
            inhalt.Children.Add(LernTeile.Label(PruefungTexte.Musterloesung));
            var haken = _a.Haken(aufgabe, teil);
            var feld = new NumericUpDown
            {
                Minimum = 0,
                Maximum = t.Punkte,
                Increment = 1,
                Value = _a.Punkte(aufgabe, teil),
                FormatString = "0",
                MinWidth = 140,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var setzen = false;
            for (var k = 0; k < t.Kriterien.Count; k++)
            {
                var index = k;
                var wahl = new CheckBox
                {
                    Content = new LernText(t.Kriterien[k], "Klein", FontWeight.Normal, "Pinsel.text_soft"),
                    IsChecked = index < haken.Count && Json.Zahl(haken[index]) is > 0,
                };
                Avalonia.Automation.AutomationProperties.SetName(wahl, t.Kriterien[k]);
                wahl.IsCheckedChanged += (_, _) =>
                {
                    var vorschlag = _a.Haken(aufgabe, nr, index, wahl.IsChecked == true);
                    setzen = true;
                    feld.Value = vorschlag;
                    setzen = false;
                    SummeZeigen();
                };
                inhalt.Children.Add(wahl);
            }
            Avalonia.Automation.AutomationProperties.SetName(feld, PruefungTexte.Punkte);
            feld.ValueChanged += (_, e) =>
            {
                if (setzen || e.NewValue is not decimal wert)
                {
                    return;
                }
                _a.PunkteSetzen(aufgabe, nr, (int)wert);
                SummeZeigen();
            };
            inhalt.Children.Add(new WrapPanel
            {
                Margin = new Thickness(0, 8, 0, 0),
                Children =
                {
                    Mitte(Schrift.Text(PruefungTexte.Punkte, "Klein", FontWeight.Normal, "Pinsel.text_dim"), new Thickness(0, 0, 10, 0)),
                    feld,
                    Mitte(Schrift.Text(Beschriftungen.Einsetzen(PruefungTexte.VonMax, t.Punkte), "Klein", FontWeight.Normal, "Pinsel.muted"),
                        new Thickness(10, 0, 0, 0)),
                },
            });
        }
        _aufgabenTeil.Children.Add(Karte(Beschriftungen.Einsetzen(PruefungTexte.AufgabeNummer, aufgabe + 1),
            Beschriftungen.Einsetzen(PruefungTexte.TitelThema, details.Titel, details.Thema), inhalt));
        SummeZeigen();
    }

    private void SummeZeigen()
    {
        if (_summe is not null && _a.Zustand is { } zustand)
        {
            _summe.Text = Klausur.Zwischenstand(Inhalte, zustand);
        }
    }

    /// <summary>_finish: offene Aufgaben nach Rueckfrage; bei Fehler bleibt die Bewertung stehen.</summary>
    private async Task AbschliessenAsync()
    {
        if (_a.Zustand is not { } zustand)
        {
            return;
        }
        if (!_a.Wiso
            && await Dialog.Zeigen(PruefungTexte.BewertungAbschliessen, PruefungTexte.AbschliessenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        if (_a.Zustand == zustand)
        {
            await SchrittAsync(_a.Abschliessen());
        }
    }

    // -- Ergebnis -----------------------------------------------------------------------------

    private void ErgebnisZeigen(string art, KlausurErgebnis ergebnis)
    {
        Leeren();
        var bereich = Klausur.Art(art)!;
        var punkte = Schrift.Text(Gesamtstand.Punkte(ergebnis), "Zahl", FontWeight.Bold, LernTeile.Pinsel(Gesamtstand.Notenfarbe(ergebnis.Punkte)));
        punkte.Margin = new Thickness(0, 0, 18, 0);
        var note = Schrift.Text(Gesamtstand.NoteDauer(ergebnis), "Normal", FontWeight.Bold);
        note.VerticalAlignment = VerticalAlignment.Bottom;
        note.Margin = new Thickness(0, 0, 0, 6);
        var inhalt = new StackPanel { Spacing = 6, Children = { new WrapPanel { Children = { punkte, note } } } };
        foreach (var zeile in Gesamtstand.Ergebniszeilen(art, ergebnis))
        {
            inhalt.Children.Add(new LernText(zeile, "Normal", FontWeight.Normal, "Pinsel.text_soft"));
        }
        _koerper.Children.Add(Karte(PruefungTexte.ErgebnisTitel, bereich.Name, inhalt));

        var themen = new StackPanel { Spacing = 8 };
        foreach (var balken in Gesamtstand.Themen(ergebnis))
        {
            var kopf = new TitelUndWert(160) { Children = { new WortText(balken.Name, "Klein", FontWeight.Normal, "Pinsel.text_soft"),
                Schrift.Text(balken.Wert, "Klein", FontWeight.Bold, balken.Schwach ? "Pinsel.red" : "Pinsel.green") } };
            themen.Children.Add(new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    kopf,
                    new ProgressBar { Minimum = 0, Maximum = 100, Value = balken.Anteil, Height = 8, MinWidth = 40 }
                        .Farbe(ProgressBar.ForegroundProperty, balken.Schwach ? "Pinsel.red" : "Pinsel.green")
                        .Farbe(ProgressBar.BackgroundProperty, "Pinsel.card_hi"),
                },
            });
        }
        var schwach = Bewertung.Schwach(ergebnis.Themen);
        if (schwach.Count > 0)
        {
            themen.Children.Add(Abstand(Schrift.Text(PruefungTexte.Ueben, "Klein", FontWeight.Bold, "Pinsel.red"), 8));
            foreach (var name in schwach)
            {
                themen.Children.Add(FortschrittSeite.UebenZeile(name, null, "Pinsel.red", "Pinsel.card_alt", () => _ = _themaUeben(name)));
            }
        }
        _koerper.Children.Add(Karte(PruefungTexte.NachThemen, PruefungTexte.Schwachstellen, themen));
        var zurueck = LernTeile.Knopf(PruefungTexte.ZurAuswahl);
        zurueck.HorizontalAlignment = HorizontalAlignment.Left;
        zurueck.Click += (_, _) => AuswahlZeigen();
        _koerper.Children.Add(zurueck);
    }

    // -- Start ohne Bedienung -----------------------------------------------------------------

    /// <summary>Namen der Ansichten von <see cref="Probe"/> (Nummer = Index).</summary>
    internal static readonly string[] ProbeNamen =
    [
        "pruefung/auswahl-si-gesamtergebnis", "pruefung/auswahl-ae-gesperrt", "pruefung/lauf-aufgabe", "pruefung/lauf-wiso",
        "pruefung/bewertung", "pruefung/ergebnis-aufgabe", "pruefung/ergebnis-wiso", "pruefung/wiso-nicht-gespeichert",
    ];

    /// <summary>
    /// Nur fuer den Start ohne Bedienung: Ansicht <paramref name="art"/> (siehe <see cref="ProbeNamen"/>)
    /// mit den laengsten Texten zeigen, ohne zu speichern und ohne laufende Uhr; -1 = wieder wie gespeichert.
    /// </summary>
    internal void Probe(int art)
    {
        _speichern.Stop();
        _takt.Stop();
        if (art < 0)
        {
            Zeigen();       // wieder wie gespeichert (Probe aus)
            return;
        }
        _aufgabe = 0;
        _wisoSeite = 0;
        switch (art)
        {
            case 0:
                _a.ProbeSetzen(null);
                AuswahlZeigen((Fachrichtung.Nach("si"), ProbeVerlauf(), 66.5));
                break;
            case 1:
                _a.ProbeSetzen(null);
                AuswahlZeigen((Fachrichtung.Nach("ae"), ProbeVerlauf(), null));
                break;
            case 2:
                _a.ProbeSetzen(ProbeAufgaben(bewertet: false));
                LaufZeigen();
                break;
            case 3:
                _a.ProbeSetzen(ProbeWiso());
                LaufZeigen();
                break;
            case 4:
                _a.ProbeSetzen(ProbeAufgaben(bewertet: true));
                BewertungZeigen();
                break;
            case 5:
            case 6:
                var zustand = art == 5 ? ProbeAufgaben(bewertet: true) : ProbeWiso();
                Klausur.Abgeben(zustand, Klausur.Sekunden(DateTimeOffset.Now), TimeZoneInfo.Local);
                var ergebnis = Bewertung.Auswerten(Inhalte, zustand);
                _a.ProbeSetzen(zustand, ergebnis);
                ErgebnisZeigen(Json.Text(zustand["art"])!, ergebnis);
                break;
            default:
                var wiso = ProbeWiso();
                wiso["phase"] = Klausur.Bewertung;
                _a.ProbeSetzen(wiso);
                BewertungZeigen();
                break;
        }
        _takt.Stop();
    }

    /// <summary>Zeigt die Probe gerade die Bewertung einer Aufgabe (Aufgabe wechseln messen)?</summary>
    internal void ProbeAufgabe(int nummer)
    {
        _aufgabe = nummer;
        if (Bewertet)
        {
            BewertungMalen();
        }
        else
        {
            AufgabeMalen();
        }
    }

    /// <summary>Naechste Seite der WiSo-Probe (Blaettern messen).</summary>
    internal void ProbeBlaettern() => Blaettern(1);

    private static List<KlausurEintrag> ProbeVerlauf()
    {
        var werte = new (string Art, double Punkte)[]
        {
            ("wiso", 81.7), ("netzwerke", 55), ("konzeption", 41), ("ap1", 72.5), ("wiso", 100), ("netzwerke", 29), ("konzeption", 92),
            ("ap1", 0), ("wiso", 49.9),
        };
        return [.. werte.Select((w, i) => new KlausurEintrag(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"2026-10-{28 - i:00} 23:{59 - i:00}:05"), w.Art, w.Punkte, Core.Lernen.Trainer.Note(w.Punkte), new JsonObject(), null))];
    }

    /// <summary>Konzeption mit den vier Aufgaben der laengsten Texte, Antworten = Musterloesung.</summary>
    private JsonObject ProbeAufgaben(bool bewertet)
    {
        var art = Klausur.Art(Klausur.Konzeption)!;
        var laengste = Enumerable.Range(0, Inhalte.Quelle(art.Quelle).Count)
            .Select(i => (Index: i, Aufgabe: Inhalte.Teile(art.Quelle, i)))
            .Where(a => a.Aufgabe is not null)
            .OrderByDescending(a => a.Aufgabe!.Einleitung.Length + a.Aufgabe.Teile.Sum(t => t.Text.Length + t.Kriterien.Sum(k => k.Length)))
            .ThenBy(a => a.Index)
            .Take(4).ToList();
        var antworten = new JsonObject();
        var punkte = new JsonObject();
        var kriterien = new JsonObject();
        for (var n = 0; n < laengste.Count; n++)
        {
            var teile = laengste[n].Aufgabe!.Teile;
            for (var t = 0; t < teile.Count; t++)
            {
                var schluessel = Klausur.Schluessel(n, t);
                antworten[schluessel] = teile[t].Loesung;
                if (bewertet)
                {
                    // abwechselnd alles und nichts abgehakt: schwache Themen im Ergebnis
                    var alle = (n + t) % 2 == 0;
                    kriterien[schluessel] = new JsonArray([.. teile[t].Kriterien.Select(_ => (JsonNode?)alle)]);
                    punkte[schluessel] = alle ? teile[t].Punkte : 0;
                }
            }
        }
        var start = DateTimeOffset.Now;
        return new JsonObject
        {
            ["art"] = art.Art,
            ["start"] = start.LocalDateTime.ToString(Klausur.ZeitFormat, System.Globalization.CultureInfo.InvariantCulture),
            ["start_utc"] = start.ToUnixTimeSeconds() - 754,
            ["minuten"] = art.Minuten,
            ["phase"] = bewertet ? Klausur.Bewertung : Klausur.Laeuft,
            ["antworten"] = antworten,
            ["punkte"] = punkte,
            ["kriterien"] = kriterien,
            ["aufgaben"] = new JsonArray([.. laengste.Select(a => (JsonNode?)new JsonObject { ["quelle"] = art.Quelle, ["index"] = a.Index })]),
        };
    }

    /// <summary>WiSo mit den 30 laengsten Fragen (laengste zuerst), jede dritte falsch beantwortet.</summary>
    private JsonObject ProbeWiso()
    {
        var art = Klausur.Art(Klausur.Wiso)!;
        var fragen = Inhalte.WisoPool(art)
            .OrderByDescending(q => q.Length + (Inhalte.Quizfrage(q)?.Optionen.Sum(o => o.Length) ?? 0)).ThenBy(q => q, StringComparer.Ordinal)
            .Take(Klausur.WisoAnzahl).ToList();
        var antworten = new JsonObject();
        var optionen = new JsonObject();
        for (var i = 0; i < fragen.Count; i++)
        {
            var frage = Inhalte.Quizfrage(fragen[i])!;
            optionen[fragen[i]] = new JsonArray([.. frage.Optionen.Select(o => (JsonNode?)o)]);
            antworten[fragen[i]] = i % 3 == 0 ? frage.Optionen.First(o => o != frage.Antwort) : frage.Antwort;
        }
        var start = DateTimeOffset.Now;
        return new JsonObject
        {
            ["art"] = art.Art,
            ["start"] = start.LocalDateTime.ToString(Klausur.ZeitFormat, System.Globalization.CultureInfo.InvariantCulture),
            ["start_utc"] = start.ToUnixTimeSeconds() - 3420,
            ["minuten"] = art.Minuten,
            ["phase"] = Klausur.Laeuft,
            ["antworten"] = antworten,
            ["punkte"] = new JsonObject(),
            ["kriterien"] = new JsonObject(),
            ["fragen"] = new JsonArray([.. fragen.Select(q => (JsonNode?)q)]),
            ["optionen"] = optionen,
        };
    }

    // -- Bausteine ----------------------------------------------------------------------------

    /// <summary>Card(title, subtitle): Titel, Untertitel, Inhalt.</summary>
    private static Border Karte(string titel, string untertitel, Control inhalt) =>
        Schrift.KopfKarte(titel, untertitel, inhalt);   // A 0.12.2: Kartenkopf wie ui.Card

    private static Border Zeile(Control inhalt, string flaeche, Thickness innen) => new Border
    {
        Child = inhalt,
        Padding = innen,
        Margin = new Thickness(0, 10, 0, 0),
        CornerRadius = new CornerRadius(12),
        BorderThickness = new Thickness(1),
    }.Farbe(Border.BackgroundProperty, flaeche).Farbe(Border.BorderBrushProperty, "Pinsel.border");

    private static T Abstand<T>(T element, double oben) where T : Control
    {
        element.Margin = new Thickness(0, oben, 0, 0);
        return element;
    }

    private static TextBlock Mitte(TextBlock text, Thickness rand)
    {
        text.VerticalAlignment = VerticalAlignment.Center;
        text.Margin = rand;
        return text;
    }
}

/// <summary>
/// Wie <see cref="TitelUndWert"/> (rechts daneben, solange links mindestens <c>mindestens</c> x
/// Schriftfaktor bleiben), aber untereinander bekommt das rechte Element die ganze Breite: ein
/// Knopf bricht dann um, statt aus der Karte zu ragen.
/// </summary>
internal sealed class NebenOderUnter(double mindestens, double abstand = 10) : Panel
{
    private bool Nebeneinander(double breite, double rechts) =>
        double.IsInfinity(breite) || breite - rechts - abstand >= mindestens * Schrift.Faktor;

    protected override Size MeasureOverride(Size verfuegbar)
    {
        Children[1].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var rechts = Children[1].DesiredSize;
        if (Nebeneinander(verfuegbar.Width, rechts.Width))
        {
            var rest = double.IsInfinity(verfuegbar.Width) ? double.PositiveInfinity : verfuegbar.Width - rechts.Width - abstand;
            Children[0].Measure(new Size(rest, double.PositiveInfinity));
            var links = Children[0].DesiredSize;
            return new Size(double.IsInfinity(verfuegbar.Width) ? links.Width + abstand + rechts.Width : verfuegbar.Width,
                Math.Max(links.Height, rechts.Height));
        }
        Children[0].Measure(new Size(verfuegbar.Width, double.PositiveInfinity));
        Children[1].Measure(new Size(verfuegbar.Width, double.PositiveInfinity));
        return new Size(verfuegbar.Width, Children[0].DesiredSize.Height + 8 + Children[1].DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size endgroesse)
    {
        Children[1].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var rechts = Children[1].DesiredSize;
        if (Nebeneinander(endgroesse.Width, rechts.Width))
        {
            Children[0].Arrange(new Rect(0, 0, Math.Max(0, endgroesse.Width - rechts.Width - abstand), endgroesse.Height));
            Children[1].Arrange(new Rect(endgroesse.Width - rechts.Width, Math.Max(0, (endgroesse.Height - rechts.Height) / 2),
                rechts.Width, rechts.Height));
            return endgroesse;
        }
        Children[0].Measure(new Size(endgroesse.Width, double.PositiveInfinity));
        Children[1].Measure(new Size(endgroesse.Width, double.PositiveInfinity));
        var oben = Children[0].DesiredSize.Height;
        Children[0].Arrange(new Rect(0, 0, endgroesse.Width, oben));
        Children[1].Arrange(new Rect(0, oben + 8, Math.Min(endgroesse.Width, Children[1].DesiredSize.Width), Children[1].DesiredSize.Height));
        return endgroesse;
    }
}
