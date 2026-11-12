using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.45.0 (Weisung P12-212 Punkte 4 und 5): „Als Spielort festlegen“ unter „Ausgewähltes Stück“ (ein Gebaeude der
/// leeren Stadt wird ein Ort der Stadt 1, ein Schritt) und die Karte „Laufzeiten zu Fuß“ rechts (gerechnet mit der Pruefung).
/// </summary>
public sealed partial class Hauptansicht
{
    private AussenStelle? _spielortFuer;
    private readonly StackPanel _laufzeitenInhalt = new() { Spacing = 4 };
    private Klappgruppe? _laufzeitenGruppe;
    private List<Laufzeiten.Weg>? _laufzeiten;
    private bool _laufzeitenAlle;

    /// <summary>Wie viele Wege die Liste ohne „Alle Wege zeigen“ zeigt.</summary>
    public const int LaufzeitenZuerst = 10;

    /// <summary>Gebaeude, fuer das die Liste „Als Spielort festlegen“ offen ist (Start-Test).</summary>
    public AussenStelle? SpielortOffen => _spielortFuer;

    /// <summary>Zuletzt gerechnete Laufzeiten (null = noch nicht gerechnet).</summary>
    public IReadOnlyList<Laufzeiten.Weg>? LaufzeitenListe => _laufzeiten;

    /// <summary>Klappgruppe „Laufzeiten zu Fuß“ (Start-Test).</summary>
    internal Klappgruppe? LaufzeitenGruppe => _laufzeitenGruppe;

    private void SpielortTeil(AussenStelle s)
    {
        if (_projekt is null || _daten is null)
        {
            return;
        }
        if (_spielortFuer is { } offen && offen != s)
        {
            _spielortFuer = null;
        }
        var a = _projekt.AussenStand;
        if (!Spielort.KnopfZeigen(a, s, _daten.Fassung))
        {
            return;
        }
        var nicht = Spielort.FestlegenNicht(a, s, _daten.Fassung);
        if (_spielortFuer is null || nicht is not null)
        {
            _spielortFuer = null;
            var knopf = Schrift.Knopf(Texte.SpielortKnopf).Mit(nicht ?? Texte.SpielortKnopfTipp);
            ToolTip.SetShowOnDisabled(knopf, true);
            knopf.IsEnabled = nicht is null && !_projekt.NurLesen;
            knopf.Click += (_, _) => SpielortAuf(s);
            _aussenAuswahl.Children.Add(knopf);
            if (nicht is not null)
            {
                _aussenAuswahl.Children.Add(Schrift.Text(nicht, "Klein", farbe: "Pinsel.muted"));
            }
            return;
        }
        var kasten = new StackPanel { Spacing = 6 };
        kasten.Children.Add(Schrift.Text(Texte.SpielortKnopf, gewicht: FontWeight.Bold));
        kasten.Children.Add(Schrift.Text(Texte.SpielortHinweis, "Klein", farbe: "Pinsel.muted"));
        foreach (var ort in Spielort.FehlendeFuer(a, s, _daten.Fassung))
        {
            var id = Py.Str(ort["id"]);
            var name = Spielort.Name(ort);
            var k = Schrift.Knopf(Spielort.Vorschlag(a, s, ort) ? Texte.SpielortVorschlag(name) : name).Mit(Texte.SpielortKnopfTipp);
            k.Click += (_, _) => SpielortFestlegen(s, id);
            kasten.Children.Add(k);
        }
        var schliessen = Schrift.Knopf(Texte.SpielortSchliessen);
        schliessen.Click += (_, _) =>
        {
            _spielortFuer = null;
            AussenAuswahlZeigen();
        };
        kasten.Children.Add(schliessen);
        _aussenAuswahl.Children.Add(new Border
        {
            Child = kasten,
            Padding = new Avalonia.Thickness(8),
            CornerRadius = new Avalonia.CornerRadius(6),
            BorderThickness = new Avalonia.Thickness(1),
        }.Farbe(Border.BorderBrushProperty, "Pinsel.border"));
    }

    /// <summary>Liste der fehlenden Orte fuer dieses Gebaeude oeffnen.</summary>
    public void SpielortAuf(AussenStelle s)
    {
        if (_projekt is null || _daten is null || Spielort.FestlegenNicht(_projekt.AussenStand, s, _daten.Fassung) is not null)
        {
            return;
        }
        _spielortFuer = s;
        AussenAuswahlZeigen();
    }

    /// <summary>Gebaeude als Ort <paramref name="id"/> festlegen: ein Schritt, danach ist der neue Ort gewaehlt. Rueckgabe: Grund oder null.</summary>
    public string? SpielortFestlegen(AussenStelle s, string id)
    {
        if (_projekt is null || _daten is null)
        {
            return Texte.SpielortUnbekannt;
        }
        var fassung = _daten.Fassung;
        var name = Spielort.Stadt1Orte(fassung).Where(x => Py.Gleich(x.Ort["id"], id)).Select(x => Spielort.Name(x.Ort)).FirstOrDefault() ?? id;
        string? grund = null;
        _projekt.AussenAendern(Texte.SpielortSchritt(name), a => grund = Spielort.Festlegen(a, s, id, fassung));
        _spielortFuer = null;
        if (grund is not null)
        {
            Melden(grund);
            AussenAuswahlZeigen();
            return grund;
        }
        Melden(Texte.SpielortFestgelegt(name));
        Karte.Waehlen(new AussenStelle(AussenArt.Haus, -1, 0, id));
        return null;
    }

    /// <summary>Liste „Laufzeiten zu Fuß“ neu zeigen (nach jeder Pruefung draussen).</summary>
    private void LaufzeitenZeigen()
    {
        _laufzeitenInhalt.Children.Clear();
        _laufzeitenInhalt.Children.Add(Schrift.Text(Texte.LaufzeitenHinweis, "Klein", farbe: "Pinsel.muted"));
        if (_laufzeiten is not { } wege)
        {
            _laufzeitenInhalt.Children.Add(Schrift.Text(Texte.LaufzeitenRechnet, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        if (wege.Count == 0)
        {
            _laufzeitenInhalt.Children.Add(Schrift.Text(Texte.LaufzeitenKeine, "Klein"));
            return;
        }
        foreach (var w in _laufzeitenAlle ? wege : wege.Take(LaufzeitenZuerst))
        {
            var gelb = w.Minuten is null || w.Minuten >= Laufzeiten.GelbAb;
            _laufzeitenInhalt.Children.Add(Schrift.Text((gelb ? "⚠ " : "") + Texte.Laufzeit(w.VonName, w.NachName, w.Minuten), "Klein", farbe: gelb ? "Pinsel.orange" : "Pinsel.text"));
        }
        if (wege.Count > LaufzeitenZuerst)
        {
            var k = Schrift.Knopf(_laufzeitenAlle ? Texte.LaufzeitenWeniger : Texte.LaufzeitenAlle);
            k.Click += (_, _) =>
            {
                _laufzeitenAlle = !_laufzeitenAlle;
                LaufzeitenZeigen();
            };
            _laufzeitenInhalt.Children.Add(k);
        }
    }
}
