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
    private readonly KlausurInhalte _inhalte;
    private readonly LaufendePruefung _ablage = new(() => Zustand.Einstellungen.Laden(), Zustand.Aendere);
    private readonly Func<string, Task> _themaUeben;
    private readonly DispatcherTimer _takt = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _speichern = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private readonly Dictionary<string, TextBox> _felder = new(StringComparer.Ordinal);
    private JsonObject? _zustand;
    private int _aufgabe;
    private int _wisoSeite;
    private TextBlock? _uhr;
    private TextBlock? _fortschritt;
    private TextBlock? _summe;
    private StackPanel? _aufgabenTeil;
    private bool _abgeben;

    public KlausurTeil(Lerninhalte inhalte, Func<string, Task> themaUeben)
    {
        _inhalte = new KlausurInhalte(inhalte, Zustand.Umbenennungen);
        _themaUeben = themaUeben;
        _takt.Tick += (_, _) => Ticken();
        _speichern.Tick += (_, _) => Festhalten();
        Zustand.BeimBeenden += Verbergen;      // Antworten beim Beenden festhalten
    }

    public Control Teil => _koerper;

    /// <summary>Gespeicherte laufende Pruefung (auch "bewertung")? (fp.load_running() is not None)</summary>
    public bool Laeuft => _ablage.Laden(_inhalte) is not null;

    /// <summary>Bewertung wird gezeigt (grading).</summary>
    public bool Bewertet { get; private set; }

    private Fachrichtung Fachrichtung => Fachrichtung.Nach(Ausbildung.Aktuell(Zustand.Schreiber));

    private static double Jetzt => Klausur.Sekunden(DateTimeOffset.Now);

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
    public void Zeigen()
    {
        _zustand = _ablage.Laden(_inhalte);
        if (_zustand is null)
        {
            AuswahlZeigen();
        }
        else if (Json.Text(_zustand["phase"]) == Klausur.Laeuft)
        {
            if (Klausur.Rest(_zustand, Jetzt, TimeZoneInfo.Local) <= 0)
            {
                _ = AbgebenAsync(auto: true);
            }
            else
            {
                LaufZeigen();
            }
        }
        else
        {
            BewertungZeigen();
        }
    }

    /// <summary>hide: geplantes Speichern jetzt erledigen, Uhr anhalten.</summary>
    public void Verbergen()
    {
        _speichern.Stop();
        Festhalten();
        _takt.Stop();
    }

    // -- Auswahl ------------------------------------------------------------------------------

    private void AuswahlZeigen()
    {
        Leeren();
        var fachrichtung = Fachrichtung;
        var intro = new StackPanel { Spacing = 6 };
        intro.Children.Add(new LernText(Beschriftungen.Einsetzen(PruefungTexte.Einleitung, TrainerTexte.NoteLang), "Klein",
            FontWeight.Normal, "Pinsel.text_soft"));
        intro.Children.Add(Schrift.Text(TrainerTexte.HinweisKurz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        foreach (var bereich in fachrichtung.Bereiche.Where(b => b.Rolle != "projekt"))
        {
            intro.Children.Add(Startzeile(bereich));
        }
        _koerper.Children.Add(Karte(PruefungTexte.AuswahlTitel, PruefungTexte.AuswahlUntertitel, intro));
        var verlauf = Gesamtstand.Verlauf(Zustand.Lernstand);
        _koerper.Children.Add(VerlaufKarte(verlauf));
        _koerper.Children.Add(GesamtKarte(fachrichtung, verlauf));
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
        var zeile = new TitelUndWert(220, 14) { Children = { text } };
        if (Klausur.Art(bereich.Art) is { } art && bereich.Quelle != "keine")
        {
            var knopf = LernTeile.Knopf(Beschriftungen.Einsetzen(PruefungTexte.Starten, art.Kurz), true);
            knopf.VerticalAlignment = VerticalAlignment.Center;
            knopf.Click += async (_, _) => await StartenAsync(art.Art);
            zeile.Children.Add(knopf);
        }
        else
        {
            // AE, DPA, DV: die Bereiche b1/b2 gibt es noch nicht (Wortliste W2 Abschnitt 6)
            var gesperrt = Schrift.Text(Beschriftungen.Platzhalter, "Klein", FontWeight.Normal, "Pinsel.text_dim");
            gesperrt.VerticalAlignment = VerticalAlignment.Center;
            zeile.Children.Add(gesperrt);
        }
        return Zeile(zeile, "Pinsel.card_alt", new Thickness(14, 10));
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
    private static Border GesamtKarte(Fachrichtung fachrichtung, List<KlausurEintrag> verlauf)
    {
        var projekt = Gesamtstand.Projektschaetzung(Zustand.Schreiber.ProjektFelder(), Json.Text(Zustand.Werte[Gesamtstand.AktivSchluessel]));
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
        if (Laeuft)
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
        if (Laeuft)
        {
            Zeigen();
            return;
        }
        var zustand = Klausur.Neu(_inhalte, art, Gesamtstand.Verlauf(Zustand.Lernstand), Klausur.Zufall(), DateTimeOffset.Now, TimeZoneInfo.Local);
        if (!_ablage.Starten(zustand))
        {
            // ohne gespeicherte Startzeit liefe die Uhr nach dem Schliessen nicht weiter
            await Dialog.Zeigen(Optionen.SpeichernFehlgeschlagenTitel, Optionen.SpeichernFehlgeschlagen, Dialog.Ok);
            return;
        }
        _zustand = zustand;
        _aufgabe = 0;
        _wisoSeite = 0;
        LaufZeigen();
    }

    // -- Laufende Pruefung --------------------------------------------------------------------

    private void LaufZeigen()
    {
        Leeren();
        var zustand = _zustand!;
        var art = Klausur.Art(Json.Text(zustand["art"]))!;
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
        if (art.Art == Klausur.Wiso)
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
        if (Json.Text(zustand["phase"]) == Klausur.Laeuft)
        {
            _takt.Start();
        }
    }

    private Pillen Aufgabenpillen(Action<int> gewechselt)
    {
        var anzahl = (_zustand!["aufgaben"] as JsonArray)?.Count ?? 0;
        var optionen = Enumerable.Range(0, anzahl).Select(n => (Nummer(n), Beschriftungen.Einsetzen(PruefungTexte.AufgabeNummer, n + 1)));
        return new Pillen(optionen, Nummer(_aufgabe), w => gewechselt(int.Parse(w, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static string Nummer(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>_tick: Restzeit (rot unter 5 Minuten); bei 0 automatisch abgeben.</summary>
    private void Ticken()
    {
        if (_zustand is not { } zustand || _uhr is null)
        {
            _takt.Stop();
            return;
        }
        var rest = Klausur.Rest(zustand, Jetzt, TimeZoneInfo.Local);
        _uhr.Text = Klausur.Zeittext(rest);
        _uhr.Farbe(TextBlock.ForegroundProperty, rest < 300 ? "Pinsel.red" : "Pinsel.accent");
        if (rest <= 0 && Json.Text(zustand["phase"]) == Klausur.Laeuft)
        {
            _takt.Stop();
            _ = AbgebenAsync(auto: true);
        }
    }

    private void FortschrittZeigen()
    {
        if (_fortschritt is not null && _zustand is not null)
        {
            _fortschritt.Text = Klausur.Fortschritt(_inhalte, _zustand);
        }
    }

    private void AufgabeMalen()
    {
        var zustand = _zustand!;
        _aufgabenTeil!.Children.Clear();
        _felder.Clear();
        var details = Klausur.Details(_inhalte, zustand, _aufgabe)!;
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
        var zustand = _zustand!;
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
                ?? _inhalte.Quizfrage(frage)?.Optionen.ToList() ?? [];
            var gewaehlt = Json.Text(antworten[frage]);
            foreach (var text in texte)
            {
                var zeile = LernTeile.Option(text, "wiso" + Nummer(nummer));
                var wahl = (RadioButton)zeile.Child!;
                wahl.IsChecked = gewaehlt is { Length: > 0 } && text == gewaehlt;
                wahl.IsCheckedChanged += (_, _) =>
                {
                    if (wahl.IsChecked == true)
                    {
                        Waehlen(frage, text);
                    }
                };
                liste.Children.Add(zeile);
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

    private void Blaettern(int schritt)
    {
        _wisoSeite += schritt;
        WisoMalen();
        _koerper.Children[0].BringIntoView();      // to_top
    }

    /// <summary>_choose: Antwort sofort speichern.</summary>
    private void Waehlen(string frage, string wert)
    {
        if (_zustand is not { } zustand || Json.Text(zustand["phase"]) != Klausur.Laeuft)
        {
            return;
        }
        ((JsonObject)zustand["antworten"]!)[frage] = wert;
        _ablage.Speichern(zustand);
        FortschrittZeigen();
    }

    /// <summary>_flush_answers: geaenderte Antworten der offenen Aufgabe speichern.</summary>
    private void Festhalten()
    {
        _speichern.Stop();
        if (_zustand is not { } zustand || Json.Text(zustand["phase"]) != Klausur.Laeuft || Json.Text(zustand["art"]) == Klausur.Wiso)
        {
            return;
        }
        var antworten = (JsonObject)zustand["antworten"]!;
        var geaendert = false;
        foreach (var (schluessel, feld) in _felder)
        {
            var wert = Klausur.Feldtext(feld.Text);
            // antworten.get(key, "") != value
            var gleich = antworten.ContainsKey(schluessel) ? Json.Text(antworten[schluessel]) == wert : wert.Length == 0;
            if (!gleich)
            {
                antworten[schluessel] = wert;
                geaendert = true;
            }
        }
        if (geaendert)
        {
            _ablage.Speichern(zustand);
            FortschrittZeigen();
        }
    }

    private async Task AbbrechenAsync()
    {
        if (await Dialog.Zeigen(PruefungTexte.AbbrechenTitel, PruefungTexte.AbbrechenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        _speichern.Stop();
        _ablage.Loeschen();
        _zustand = null;
        AuswahlZeigen();
    }

    /// <summary>_submit: nach Rueckfrage (nicht bei abgelaufener Zeit) abgeben; WiSo gleich auswerten.</summary>
    private async Task AbgebenAsync(bool auto)
    {
        if (_abgeben || _zustand is not { } zustand || Json.Text(zustand["phase"]) != Klausur.Laeuft)
        {
            return;
        }
        _abgeben = true;
        try
        {
            if (!auto && await Dialog.Zeigen(PruefungTexte.AbgebenTitel, PruefungTexte.AbgebenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
            {
                return;
            }
            if (_zustand != zustand || Json.Text(zustand["phase"]) != Klausur.Laeuft)
            {
                return;
            }
            Festhalten();
            _takt.Stop();
            Klausur.Abgeben(zustand, Jetzt, TimeZoneInfo.Local);
            if (Json.Text(zustand["art"]) == Klausur.Wiso)
            {
                // anders als Python vorher gespeichert: misslingt der Abschluss, bleibt die Abgabe (Frage 4 a)
                _ablage.Speichern(zustand);
                if (Abschliessen() && auto)
                {
                    await Dialog.Zeigen(PruefungTexte.ZeitTitel, PruefungTexte.ZeitWiso, Dialog.Ok);
                }
                return;
            }
            _ablage.Speichern(zustand);
            if (auto)
            {
                await Dialog.Zeigen(PruefungTexte.ZeitTitel, PruefungTexte.ZeitOffen, Dialog.Ok);
            }
            _aufgabe = 0;
            BewertungZeigen();
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
        var zustand = _zustand!;
        var art = Klausur.Art(Json.Text(zustand["art"]))!;
        var abschliessen = LernTeile.Knopf(PruefungTexte.BewertungAbschliessen, true);
        abschliessen.Click += async (_, _) => await AbschliessenAsync();
        var kopf = new StackPanel { Spacing = 10 };
        if (art.Art == Klausur.Wiso)
        {
            // Nur nach einem misslungenen Abschluss: WiSo hat keine Bewertung, hier wird nur neu gespeichert
            kopf.Children.Add(Schrift.Text(TrainerTexte.NichtGespeichert, "Klein", FontWeight.Normal, "Pinsel.red"));
            kopf.Children.Add(abschliessen);
            _koerper.Children.Add(Karte(PruefungTexte.BewertungTitel, art.Name, kopf));
            return;
        }
        _summe = Schrift.Text("", "Normal", FontWeight.Bold);
        _summe.VerticalAlignment = VerticalAlignment.Center;
        kopf.Children.Add(new LernText(PruefungTexte.BewertungAnleitung, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        kopf.Children.Add(new TitelUndWert(160, 14) { Children = { _summe, abschliessen } });
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
        var zustand = _zustand!;
        _aufgabenTeil!.Children.Clear();
        var details = Klausur.Details(_inhalte, zustand, _aufgabe)!;
        var punkte = (JsonObject)zustand["punkte"]!;
        var kriterien = (JsonObject)zustand["kriterien"]!;
        var inhalt = new StackPanel { Spacing = 4 };
        for (var teil = 0; teil < details.Teile.Count; teil++)
        {
            var t = details.Teile[teil];
            var schluessel = Klausur.Schluessel(_aufgabe, teil);
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
            // marks = setdefault(key, [False] * len(kriterien))
            if (kriterien[schluessel] is not JsonArray haken)
            {
                haken = new JsonArray([.. t.Kriterien.Select(_ => (JsonNode?)false)]);
                kriterien[schluessel] = haken;
            }
            var feld = new NumericUpDown
            {
                Minimum = 0,
                Maximum = t.Punkte,
                Increment = 1,
                Value = (decimal)Math.Truncate(Json.Zahl(punkte[schluessel]) ?? 0),
                FormatString = "0",
                MinWidth = 140,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var setzen = false;
            for (var k = 0; k < t.Kriterien.Count; k++)
            {
                var index = k;
                var haken1 = new CheckBox
                {
                    Content = new LernText(t.Kriterien[k], "Klein", FontWeight.Normal, "Pinsel.text_soft"),
                    IsChecked = index < haken.Count && Json.Zahl(haken[index]) is > 0,
                };
                Avalonia.Automation.AutomationProperties.SetName(haken1, t.Kriterien[k]);
                haken1.IsCheckedChanged += (_, _) =>
                {
                    // toggled: Haken merken, Punkte vorschlagen, speichern
                    while (haken.Count <= index)
                    {
                        haken.Add(false);
                    }
                    haken[index] = haken1.IsChecked == true;
                    var vorschlag = Klausur.Vorschlag(t.Punkte, haken.Select(h => Json.Zahl(h) is > 0).ToList());
                    punkte[schluessel] = vorschlag;
                    setzen = true;
                    feld.Value = vorschlag;
                    setzen = false;
                    _ablage.Speichern(zustand);
                    SummeZeigen();
                };
                inhalt.Children.Add(haken1);
            }
            Avalonia.Automation.AutomationProperties.SetName(feld, PruefungTexte.Punkte);
            feld.ValueChanged += (_, e) =>
            {
                if (setzen || e.NewValue is not decimal wert)
                {
                    return;
                }
                punkte[schluessel] = (int)wert;
                _ablage.Speichern(zustand);
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
        _aufgabenTeil.Children.Add(Karte(Beschriftungen.Einsetzen(PruefungTexte.AufgabeNummer, _aufgabe + 1),
            Beschriftungen.Einsetzen(PruefungTexte.TitelThema, details.Titel, details.Thema), inhalt));
        SummeZeigen();
    }

    private void SummeZeigen()
    {
        if (_summe is not null && _zustand is not null)
        {
            _summe.Text = Klausur.Zwischenstand(_inhalte, _zustand);
        }
    }

    /// <summary>_finish: offene Aufgaben nach Rueckfrage; bei Fehler bleibt die Bewertung stehen.</summary>
    private async Task AbschliessenAsync()
    {
        if (_zustand is not { } zustand)
        {
            return;
        }
        if (Json.Text(zustand["art"]) != Klausur.Wiso
            && await Dialog.Zeigen(PruefungTexte.BewertungAbschliessen, PruefungTexte.AbschliessenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        if (_zustand == zustand)
        {
            Abschliessen();
        }
    }

    /// <summary>Speichern in einer Buchung; true: gespeichert, Ergebnis gezeigt.</summary>
    private bool Abschliessen()
    {
        var zustand = _zustand!;
        var art = Json.Text(zustand["art"])!;
        if (Abschluss.Abschliessen(_inhalte, Zustand.Schreiber, _ablage, zustand) is not { } ergebnis)
        {
            // Frage 4 a: Pruefung bleibt in Phase "bewertung", die Bewertung bleibt stehen
            if (Bewertet && art != Klausur.Wiso)
            {
                _ = Dialog.Zeigen(KartenTexte.HinweisTitel, TrainerTexte.NichtGespeichert, Dialog.Ok);
            }
            else
            {
                BewertungZeigen();      // WiSo: Karte mit dem Hinweis und "Bewertung abschließen"
            }
            return false;
        }
        _zustand = null;
        AbgleichDienst.NachDemLernen();        // notify_progress
        ErgebnisZeigen(art, ergebnis);
        return true;
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

    // -- Bausteine ----------------------------------------------------------------------------

    /// <summary>Card(title, subtitle): Titel, Untertitel, Inhalt.</summary>
    private static Border Karte(string titel, string untertitel, Control inhalt)
    {
        var kopf = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new LernText(titel, "Mittel", FontWeight.Bold, "Pinsel.text"),
                new LernText(untertitel, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        };
        return Schrift.Karte(new StackPanel { Spacing = 10, Children = { kopf, inhalt } });
    }

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
