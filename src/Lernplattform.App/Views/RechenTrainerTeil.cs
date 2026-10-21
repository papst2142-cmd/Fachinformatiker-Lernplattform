using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Rechner;
using Lernplattform.Design;
using Lernplattform.Programm;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Views;

/// <summary>
/// Phase 3, W3 (Paket R7, Weisung P3-12): Reiter "Trainer" der Rechner-Seite wie TrainerPanel in
/// Python (app_gui 5490-5757, mobile/src/main.py 2950-3081), ein Aufbau fuer PC und Handy: Karte
/// "Rechen-Trainer" (Aufgabenart, Schwierigkeit, "Runde starten", Statistikzeile) und Karte
/// "Aufgabe" (Kopf, Aufgabentext, Felder mit Rueckmeldung, Rechenweg im Ergebniskasten, Knopf
/// und Zaehler). Der Ablauf (Speichern, Statistik, Zustaende) steht im Core
/// (<see cref="RechenTrainerAblauf"/>); nach jedem Pruefen wird der Abgleich angestossen (PC und
/// Handy, P3-12 F1 a). Der Knopf ist ohne Runde auf beiden Geraeten gesperrt. Ein Fehler beim
/// Speichern laeuft ueber den vorhandenen Weg des Lernstands zum Hinweis "Datenbankfehler".
/// </summary>
public sealed class RechenTrainerTeil : StackPanel
{
    private readonly Func<bool> _schmal;
    private readonly Pillen _arten;
    private readonly Pillen _stufen;
    private readonly TextBlock _statistik = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
    private readonly TextBlock _kopf = RechnerTeile.Unterschrift("");
    private readonly TextBlock _aufgabe = Schrift.Text("", "Mittel", FontWeight.Bold, "Pinsel.text_soft");
    private readonly StackPanel _felder = new() { Spacing = 10 };
    private readonly TextBlock _rueckmeldung = Schrift.Text("", "Normal", FontWeight.Bold, "Pinsel.text");
    private readonly Ergebniskasten _rechenweg = new();
    private readonly Button _knopf;
    private readonly TextBlock _zaehler = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
    private readonly Dictionary<string, TextBox> _eingaben = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WortText> _marken = new(StringComparer.Ordinal);
    private RechenTrainerAblauf? _ablauf;

    /// <param name="schmal">Aktueller Aufbau (schmal = Handy-Rechenweg), beim Pruefen gelesen.</param>
    public RechenTrainerTeil(Func<bool> schmal)
    {
        _schmal = schmal;
        Spacing = 14;
        _arten = new Pillen(RechenTrainer.Arten, RechenTrainer.Arten[0].Art, art => _ablauf?.SetzeArt(art));
        _stufen = new Pillen(RechenTrainer.Stufen.Select(s => (s.Stufe.ToString(System.Globalization.CultureInfo.InvariantCulture), s.Name)),
            RechenTrainer.Stufen[0].Stufe.ToString(System.Globalization.CultureInfo.InvariantCulture),
            stufe => _ablauf?.SetzeStufe(int.Parse(stufe, System.Globalization.CultureInfo.InvariantCulture)));
        var starten = RechnerSeite.Knopf(RundeStarten, Starten);
        _statistik.VerticalAlignment = VerticalAlignment.Center;
        var einstellung = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                LernTeile.Label(TrainerArt), _arten,
                Abstand(LernTeile.Label(TrainerStufe), 6), _stufen,
                new WrapPanel { Children = { starten, _statistik } },
            },
        };
        _knopf = RechnerSeite.Knopf(Pruefen, Druecken);
        _zaehler.VerticalAlignment = VerticalAlignment.Center;
        _zaehler.Margin = new Thickness(8, 0, 0, 8);
        _rechenweg.IsVisible = false;
        var aufgabe = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                _aufgabe, _felder, _rueckmeldung, _rechenweg,
                new WrapPanel { Children = { _knopf, _zaehler } },
            },
        };
        Children.Add(RechnerTeile.Karte(RechenTrainer.Titel, "Pinsel.accent", RechnerTeile.Unterschrift(TrainerUnter), einstellung));
        Children.Add(RechnerTeile.Karte(Aufgabe, "Pinsel.purple", _kopf, aufgabe));
        Anzeigen();
    }

    /// <summary>Der Ablauf (fuer den Start ohne Bedienung); erst nach dem ersten Zeigen da.</summary>
    internal RechenTrainerAblauf? Ablauf => _ablauf;

    /// <summary>Der Ergebniskasten des Rechenwegs (fuer den Start ohne Bedienung).</summary>
    internal Ergebniskasten Rechenweg => _rechenweg;

    /// <summary>Reiter "Trainer" wird gezeigt: beim ersten Mal Ablauf anlegen und Statistik lesen (Tabellenzeile 1).</summary>
    public void Gezeigt()
    {
        if (_ablauf is null)
        {
            _ablauf = new RechenTrainerAblauf(Zustand.Schreiber);
            _ablauf.SetzeArt(_arten.Wert);
            _ablauf.SetzeStufe(int.Parse(_stufen.Wert, System.Globalization.CultureInfo.InvariantCulture));
            _ablauf.NachSpeichern += AbgleichDienst.NachDemLernen;     // L10: bald abgleichen (schedule), PC und Handy
        }
        _ablauf.ErstesZeigen();
        Anzeigen();
    }

    /// <summary>"Runde starten" (fuer den Start ohne Bedienung auch direkt).</summary>
    internal void Starten()
    {
        Gezeigt();
        _ablauf!.RundeStarten();
        FelderBauen();
        Anzeigen();
        if (_eingaben.Count > 0)
        {
            _eingaben.Values.First().Focus();
        }
    }

    /// <summary>Knopf "Prüfen" / "Nächste Aufgabe" / "Auswertung" (PC auch Eingabetaste im Feld).</summary>
    internal void Druecken()
    {
        if (_ablauf is null)
        {
            return;
        }
        var antworten = _eingaben.ToDictionary(p => p.Key, p => p.Value.Text ?? "", StringComparer.Ordinal);
        var vorher = _ablauf.Zustand;
        var pruefung = _ablauf.Druecken(antworten, _schmal());
        if (pruefung is not null)
        {
            MarkenSetzen(pruefung);
        }
        else if (vorher == TrainerZustand.Geprueft)
        {
            FelderBauen();
        }
        Anzeigen();
    }

    // -- Start ohne Bedienung ----------------------------------------------------------------

    private RechenTrainerAblauf? _echt;
    private bool _probe;

    /// <summary>
    /// Start ohne Bedienung: eine Runde mit festem Startwert OHNE Speichern (Ablauf ohne
    /// Schreiber, kein Abgleich) zeigen, auf Wunsch geprueft (alle Felder leer = falsch, also
    /// alle "richtig wäre") oder bis zur Auswertung. Der echte Ablauf bleibt unberuehrt.
    /// </summary>
    internal void Probe(string art, int stufe, bool pruefen, bool beenden = false)
    {
        if (!_probe)
        {
            _echt = _ablauf;
            _probe = true;
        }
        _ablauf = new RechenTrainerAblauf(null, () => 4711);
        _ablauf.SetzeArt(art);
        _ablauf.SetzeStufe(stufe);
        _ablauf.RundeStarten();
        FelderBauen();
        if (pruefen || beenden)
        {
            Druecken();
        }
        while (beenden && _ablauf.Zustand != TrainerZustand.Beendet)
        {
            Druecken();
        }
        Anzeigen();
    }

    /// <summary>Probe beenden: der echte Ablauf wird wieder gezeigt (eingetippte Antworten sind dann leer).</summary>
    internal void ProbeEnde()
    {
        if (!_probe)
        {
            return;
        }
        _ablauf = _echt;
        _echt = null;
        _probe = false;
        FelderBauen();
        if (_ablauf?.Pruefung is { } pruefung)
        {
            MarkenSetzen(pruefung);
        }
        Anzeigen();
    }

    private void MarkenSetzen(TrainerPruefung pruefung)
    {
        foreach (var marke in pruefung.Marken)
        {
            var text = _marken[marke.Schluessel];
            text.Text = marke.Text;
            text.Farbe(TextBlock.ForegroundProperty, marke.Richtig ? "Pinsel.green" : "Pinsel.red");
            text.IsVisible = true;
        }
        _rechenweg.Setzen(pruefung.Rechenweg);
    }

    private void FelderBauen()
    {
        _felder.Children.Clear();
        _eingaben.Clear();
        _marken.Clear();
        if (_ablauf?.Aufgabe is not { } aufgabe)
        {
            return;
        }
        foreach (var (schluessel, beschriftung) in aufgabe.Felder)
        {
            var feld = RechnerTeile.Feld("", beschriftung, breite: 260);
            feld.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    Druecken();
                }
            };
            // WortText: eine lange Adresse ("richtig wäre: 2001:0db8:…") bleibt ganz, notfalls kleiner
            var marke = new WortText("", "Klein", FontWeight.SemiBold, "Pinsel.muted");
            marke.IsVisible = false;
            _eingaben[schluessel] = feld;
            _marken[schluessel] = marke;
            var zeile = RechnerTeile.Zeile(beschriftung, feld).HoechstBreite(560);
            zeile.HorizontalAlignment = HorizontalAlignment.Left;     // nicht mittig in der Karte
            _felder.Children.Add(new StackPanel
            {
                Spacing = 4,
                Children = { zeile, marke },
            });
        }
    }

    private void Anzeigen()
    {
        var a = _ablauf;
        _statistik.Text = a?.Statistik ?? "";
        _statistik.IsVisible = _statistik.Text.Length > 0;
        _kopf.Text = a?.Kopf ?? "";
        _kopf.IsVisible = _kopf.Text.Length > 0;
        _aufgabe.Text = a?.AufgabeText ?? Lernplattform.Core.Texte.Beschriftungen.Einsetzen(TrainerStart, RechenTrainer.Runde);
        _rueckmeldung.Text = a?.Rueckmeldung ?? "";
        _rueckmeldung.IsVisible = _rueckmeldung.Text.Length > 0;
        var zustand = a?.Zustand ?? TrainerZustand.KeineRunde;
        _rueckmeldung.Farbe(TextBlock.ForegroundProperty, zustand switch
        {
            TrainerZustand.Geprueft => a!.Pruefung!.AllesRichtig ? "Pinsel.green" : "Pinsel.yellow",
            TrainerZustand.Beendet => "Pinsel.text_dim",
            _ => "Pinsel.text",
        });
        _rechenweg.IsVisible = zustand == TrainerZustand.Geprueft;
        if (zustand != TrainerZustand.Offen && zustand != TrainerZustand.Geprueft)
        {
            _felder.Children.Clear();
            _eingaben.Clear();
            _marken.Clear();
        }
        ((TextBlock)_knopf.Content!).Text = a?.Knopf ?? Pruefen;
        _knopf.IsEnabled = a?.KnopfAktiv ?? false;
        _zaehler.Text = a?.Zaehler ?? "";
    }

    private static T Abstand<T>(T element, double oben) where T : Control
    {
        element.Margin = new Thickness(0, oben, 0, 0);
        return element;
    }
}
