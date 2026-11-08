using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.21.0 (Weisungen P12-87/P12-88, Kurzplan Meldung P12-56): „Aussehen tauschen“ rechts bei einem gewaehlten Ort.
/// Die Liste der Vorlagen steht in der rechten Spalte, damit die Karte frei bleibt: Antippen zeigt das neue Haus als Schatten
/// (gruen = passt, rot = mit Grund), „Übernehmen“ tauscht in einem Schritt (Rueckgaengig geht). Gerechnet wird auf einer
/// Kopie mit <see cref="OrtAussehen.Tauschen"/>, genau wie der echte Schritt; die Pruefung laeuft im Hintergrund, der neueste
/// Wunsch gewinnt.
/// </summary>
public sealed partial class Hauptansicht
{
    private string? _aussehenOrt;
    private List<OrtAussehen.Vorlage> _aussehenVorlagen = [];
    private int _aussehenWahl = -1;
    private int _aussehenNr;
    private int _aussehenStand = -1;
    private (string? Grund, StadtbauFund? Rot, List<string> Hinweise, bool Fertig)? _aussehenErgebnis;
    private TaskCompletionSource _aussehenFertig = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Ist die Liste „Aussehen tauschen“ offen (fuer welchen Ort)?</summary>
    public string? AussehenOffen => _aussehenOrt;

    /// <summary>Vorlagen der offenen Liste (Start-Test).</summary>
    public IReadOnlyList<OrtAussehen.Vorlage> AussehenVorlagen => _aussehenVorlagen;

    /// <summary>Ergebnis der Vorschau: Grund (nichts geht), neuer roter Fund, Hinweise; null ohne Wahl.</summary>
    public (string? Grund, StadtbauFund? Rot, List<string> Hinweise, bool Fertig)? AussehenErgebnis => _aussehenErgebnis;

    /// <summary>Knopf bzw. offene Liste „Aussehen tauschen“ fuer die Auswahl draussen.</summary>
    private void AussehenTeil(AussenStelle s)
    {
        if (_projekt is null)
        {
            return;
        }
        if (_aussehenOrt is not null && (s.Art != AussenArt.Haus || s.Ort != _aussehenOrt))
        {
            AussehenZu(neuZeigen: false);
        }
        if (s.Art != AussenArt.Haus)
        {
            return;
        }
        if (_aussehenOrt is null)
        {
            var nicht = OrtAussehen.TauschenNicht(_projekt.AussenStand, s.Ort);
            var knopf = Schrift.Knopf(Texte.AussehenTauschen).Mit(nicht ?? Texte.AussehenTauschenTipp);
            ToolTip.SetShowOnDisabled(knopf, true);
            knopf.IsEnabled = nicht is null && !_projekt.NurLesen;
            knopf.Click += (_, _) => AussehenAuf(s.Ort);
            _aussenAuswahl.Children.Add(knopf);
            if (nicht is not null)
            {
                _aussenAuswahl.Children.Add(Schrift.Text(nicht, "Klein", farbe: "Pinsel.muted"));
            }
            return;
        }
        // Stand hat sich geaendert (Rueckgaengig, andere Aenderung): Vorlagen und Vorschau neu
        if (_aussehenStand != AussehenStandNr())
        {
            _aussehenVorlagen = OrtAussehen.Vorlagen(_projekt.AussenStand, Aussen.Aus(_projekt.Basis), _aussehenOrt, Blueprints);
            if (_aussehenWahl >= _aussehenVorlagen.Count)
            {
                _aussehenWahl = -1;
            }
            AussehenVorschauRechnen();
        }
        var kasten = new StackPanel { Spacing = 6 };
        kasten.Children.Add(Schrift.Text(Texte.AussehenTauschen, gewicht: FontWeight.Bold));
        kasten.Children.Add(Schrift.Text(Texte.AussehenHinweis, "Klein", farbe: "Pinsel.muted"));
        if (_aussehenVorlagen.Count == 0)
        {
            kasten.Children.Add(Schrift.Text(Texte.AussehenKeineVorlage, "Klein"));
        }
        for (var i = 0; i < _aussehenVorlagen.Count; i++)
        {
            var v = _aussehenVorlagen[i];
            var name = Texte.AussehenVorlageName(v.Name, v.AusDerStadt);
            var gewaehlt = i == _aussehenWahl;
            var k = Schrift.Knopf(name, betont: gewaehlt).Vorlese(gewaehlt ? name + ", gewählt" : name)
                .Mit(v.Zurueck ? Texte.AussehenAltZurueckTipp : Texte.AussehenTauschenTipp);
            var nr = i;
            k.Click += (_, _) => AussehenWaehlen(nr);
            kasten.Children.Add(k);
        }
        if (_aussehenErgebnis is { } e)
        {
            if (e.Grund is not null)
            {
                kasten.Children.Add(Schrift.Text(e.Grund, "Klein", farbe: "Pinsel.red"));
            }
            else if (!e.Fertig)
            {
                kasten.Children.Add(Schrift.Text(Texte.AussehenPruefungLaeuft, "Klein", farbe: "Pinsel.muted"));
            }
            else if (e.Rot is { } rot)
            {
                kasten.Children.Add(Schrift.Text(Texte.AussehenNeuRot(rot.Text), "Klein", farbe: "Pinsel.red"));
            }
            else
            {
                kasten.Children.Add(Schrift.Text(Texte.AussehenPasst, "Klein", farbe: "Pinsel.green"));
            }
            foreach (var h in e.Hinweise)
            {
                kasten.Children.Add(Schrift.Text(h, "Klein"));
            }
        }
        var uebernehmen = Schrift.Knopf(Texte.AussehenUebernehmen, betont: true).Mit(Texte.AussehenHinweis);
        ToolTip.SetShowOnDisabled(uebernehmen, true);
        uebernehmen.IsEnabled = _aussehenErgebnis is { Grund: null } && !_projekt.NurLesen;
        uebernehmen.Click += (_, _) => AussehenUebernehmen();
        var schliessen = Schrift.Knopf(Texte.AussehenSchliessen);
        schliessen.Click += (_, _) => AussehenZu(neuZeigen: true);
        kasten.Children.Add(new WrapPanel { Children = { uebernehmen, schliessen } });
        _aussenAuswahl.Children.Add(new Border
        {
            Child = kasten,
            Padding = new Avalonia.Thickness(8),
            CornerRadius = new Avalonia.CornerRadius(6),
            BorderThickness = new Avalonia.Thickness(1),
        }.Farbe(Border.BorderBrushProperty, "Pinsel.border"));
    }

    private int AussehenStandNr() => _projekt is null ? -1 : (_projekt.SchritteZurueck * 100_000) + _projekt.Verlauf.Count;

    /// <summary>Liste „Aussehen tauschen“ fuer einen Ort oeffnen.</summary>
    public void AussehenAuf(string ort)
    {
        if (_projekt is null || OrtAussehen.TauschenNicht(_projekt.AussenStand, ort) is not null)
        {
            return;
        }
        _aussehenOrt = ort;
        _aussehenWahl = -1;
        _aussehenErgebnis = null;
        _aussehenStand = -1;
        Karte.AussehenVorschau = null;
        AussenAuswahlZeigen();
    }

    /// <summary>Liste schliessen, Vorschau weg.</summary>
    private void AussehenZu(bool neuZeigen)
    {
        _aussehenOrt = null;
        _aussehenWahl = -1;
        _aussehenErgebnis = null;
        _aussehenNr++;
        Karte.AussehenVorschau = null;
        _aussehenFertig.TrySetResult();
        if (neuZeigen)
        {
            AussenAuswahlZeigen();
        }
    }

    /// <summary>Eine Vorlage antippen: Schatten sofort, Pruefung im Hintergrund. Die Aufgabe endet, wenn das Ergebnis da ist.</summary>
    public Task AussehenWaehlen(int nr)
    {
        if (_aussehenOrt is null || nr < 0 || nr >= _aussehenVorlagen.Count)
        {
            return Task.CompletedTask;
        }
        _aussehenWahl = nr;
        AussehenVorschauRechnen();
        var t = _aussehenFertig.Task;
        AussenAuswahlZeigen();
        return t;
    }

    private void AussehenVorschauRechnen()
    {
        _aussehenStand = AussehenStandNr();
        _aussehenFertig.TrySetResult();
        _aussehenFertig = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var nr = ++_aussehenNr;
        if (_projekt is null || _aussehenOrt is not { } ort || _aussehenWahl < 0)
        {
            _aussehenErgebnis = null;
            Karte.AussehenVorschau = null;
            _aussehenFertig.TrySetResult();
            return;
        }
        var v = _aussehenVorlagen[_aussehenWahl];
        var kopie = (PyObjekt)Py.Kopie(_projekt.AussenStand)!;
        var r = OrtAussehen.Tauschen(kopie, ort, v.Blueprint, Aussen.Aus(_projekt.Basis));
        if (r.Grund is not null)
        {
            _aussehenErgebnis = (r.Grund, null, r.Hinweise, true);
            Karte.AussehenVorschau = (_projekt.AussenStand, ort, false);
            _aussehenFertig.TrySetResult();
            return;
        }
        _aussehenErgebnis = (null, null, r.Hinweise, false);
        Karte.AussehenVorschau = (kopie, ort, true);
        var vorherStand = (PyObjekt)Py.Kopie(_projekt.AussenStand)!;
        var fassung = _projekt.Basis;
        var innen = _projekt.Innenraeume();
        var daten = _projekt.Daten;
        var fertig = _aussehenFertig;
        _ = Hintergrund(Task.Run(() => Bauen3D.NeuRot(Bauen3D.Pruefen(vorherStand, fassung, innen, daten), Bauen3D.Pruefen(kopie, fassung, innen, daten))))
            .ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (nr != _aussehenNr)
                {
                    return;
                }
                if (t.IsFaulted)
                {
                    _ordner.Fehler("Aussehen tauschen: " + t.Exception?.InnerException);
                }
                var rot = t.IsFaulted ? new StadtbauFund("Die Prüfung ist abgebrochen: " + t.Exception?.InnerException?.Message, "Bitte CC Bescheid geben.", true) : t.Result;
                _aussehenErgebnis = (null, rot, r.Hinweise, true);
                Karte.AussehenVorschau = (kopie, ort, rot is null);
                AussenAuswahlZeigen();
                fertig.TrySetResult();
            }), TaskScheduler.Default);
    }

    /// <summary>„Übernehmen“: ein Schritt im Verlauf (Rueckgaengig geht), Liste zu, der Ort bleibt gewaehlt.</summary>
    public string? AussehenUebernehmen()
    {
        if (_projekt is null || _aussehenOrt is not { } ort || _aussehenWahl < 0 || _aussehenWahl >= _aussehenVorlagen.Count)
        {
            return Texte.AussehenKeineVorlage;
        }
        var v = _aussehenVorlagen[_aussehenWahl];
        var basis = Aussen.Aus(_projekt.Basis);
        var r = _projekt.AussenAendern(Texte.AussehenTauschen, a => OrtAussehen.Tauschen(a, ort, v.Blueprint, basis));
        if (r.Grund is not null)
        {
            Melden(r.Grund);
            return r.Grund;
        }
        AussehenZu(neuZeigen: false);
        Melden(Texte.AussehenGetauscht + ": „" + _projekt.OrtName(ort) + "“ – " + v.Name);
        Karte.Waehlen(new AussenStelle(AussenArt.Haus, -1, 0, ort));
        return null;
    }
}
