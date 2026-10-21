using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Core.Rechner;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Views;

/// <summary>
/// Phase 3 W4c (Weisung P3-23 Punkt 3a): Karte "WiSo-Rechner" der Rechner-Seite genau nach der
/// freigegebenen Wortliste (Fi-Austausch anlagen/P3-22_Wortliste_W4c.md). Hinweiszeile unter der
/// Unterzeile, innere Pillen fuer sechs Rechner (immer nur einer sichtbar, Wahl bleibt, solange das
/// Programm laeuft), je Rechner Felder mit Vorgaben, "Berechnen", roter Hinweis, Ergebniskasten und
/// Rechenweg (Zahlen der letzten gueltigen Berechnung, vorher die Vorgaben). Zinsen mit den
/// Laufzeit-Pillen Tage/Monate/Jahre (F2). Gerechnet und geschrieben wird im Core
/// (<see cref="WisoBerichte"/>, Vergleichswerte G30), hier wird nur angezeigt; nichts wird gespeichert.
/// </summary>
internal sealed class WisoKarte
{
    private readonly Func<bool> _schmal;
    private readonly Pillen _auswahl;
    private readonly Dictionary<string, Teil> _teile = new(StringComparer.Ordinal);

    private sealed class Teil
    {
        public required StackPanel Inhalt;
        public required Dictionary<string, TextBox> Felder;
        public required Ergebniskasten Kasten;
        public required Fehlerhinweis Fehler;
        public required Rechenwegteil Weg;
        public Pillen? Laufzeit;
        public WisoEingabe? Letzte;
    }

    public WisoKarte(Func<bool> schmal)
    {
        _schmal = schmal;
        _auswahl = new Pillen(WisoAuswahl, WisoAuswahl[0].Wert, AuswahlWechseln);
        var hinweis = Lernplattform.Design.Schrift.Text(WisoHinweis, "Klein", FontWeight.Normal, "Pinsel.text_dim");
        hinweis.TextWrapping = TextWrapping.Wrap;
        var inhalt = new StackPanel { Spacing = 10, Children = { hinweis, _auswahl } };
        foreach (var (rechner, _) in WisoAuswahl)
        {
            var teil = Bauen(rechner);
            teil.Inhalt.IsVisible = rechner == WisoAuswahl[0].Wert;
            _teile[rechner] = teil;
            inhalt.Children.Add(teil.Inhalt);
        }
        Karte = RechnerTeile.Karte(WisoTitle, "Pinsel.pink", RechnerTeile.Unterschrift(WisoSubtitle), inhalt);
    }

    public Border Karte { get; }

    /// <summary>Die sechs Rechenwege (fuer Aufbauwechsel und Aufklappen der Rechner-Seite).</summary>
    public IEnumerable<Rechenwegteil> Rechenwege => _teile.Values.Select(t => t.Weg);

    /// <summary>Gewaehlter Rechner der inneren Pillen.</summary>
    internal string Gewaehlt => _auswahl.Wert;

    internal Ergebniskasten Kasten(string rechner) => _teile[rechner].Kasten;

    internal Rechenwegteil Weg(string rechner) => _teile[rechner].Weg;

    internal string Fehler(string rechner) => _teile[rechner].Fehler.Text ?? "";

    private Teil Bauen(string rechner)
    {
        var felder = new Dictionary<string, TextBox>(StringComparer.Ordinal);
        var zeilen = new List<Control>();
        foreach (var (schluessel, beschriftung, vorgabe) in WisoFelder[rechner])
        {
            var feld = RechnerTeile.Feld(vorgabe, beschriftung, zahl: true, breite: 160);
            felder[schluessel] = feld;
            zeilen.Add(RechnerTeile.Zeile(beschriftung, feld));
        }
        Teil? teil = null;
        var weg = new Rechenwegteil(() => WisoBerichte.Rechenweg(teil!.Letzte ?? Vorgabe(rechner)));
        var panel = new StackPanel { Spacing = 10, Children = { RechnerSeite.Felder([.. zeilen]) } };
        Pillen? laufzeit = null;
        if (rechner == "zinsen")
        {
            laufzeit = new Pillen(LaufzeitEinheiten, LaufzeitEinheiten[0].Wert, _ => { });
            panel.Children.Add(laufzeit);
        }
        var kasten = new Ergebniskasten(NochKeineBerechnung);
        var fehler = new Fehlerhinweis();
        teil = new Teil { Inhalt = panel, Felder = felder, Kasten = kasten, Fehler = fehler, Weg = weg, Laufzeit = laufzeit };
        panel.Children.Add(RechnerSeite.Knopf(Berechnen, () => Rechnen(rechner)));
        panel.Children.Add(fehler);
        panel.Children.Add(kasten);
        panel.Children.Add(weg.Knopf);
        panel.Children.Add(weg.Kasten);
        RechnerSeite.EnterRechnet(() => Rechnen(rechner), [.. felder.Values]);
        return teil;
    }

    private static WisoEingabe Vorgabe(string rechner) =>
        WisoBerichte.Lesen(rechner, WisoFelder[rechner].ToDictionary(f => f.Schluessel, f => (string?)f.Vorgabe));

    private void AuswahlWechseln(string wert)
    {
        foreach (var (rechner, teil) in _teile)
        {
            teil.Inhalt.IsVisible = rechner == wert;
        }
    }

    /// <summary>Inneren Rechner waehlen wie mit der Pille (fuer den Start ohne Bedienung).</summary>
    internal void Waehle(string rechner)
    {
        _auswahl.Waehle(rechner);
        AuswahlWechseln(rechner);
    }

    internal void Rechnen(string rechner)
    {
        var teil = _teile[rechner];
        try
        {
            teil.Letzte = WisoBerichte.Lesen(rechner, teil.Felder.ToDictionary(p => p.Key, p => p.Value.Text), teil.Laufzeit?.Wert ?? "tage");
            teil.Kasten.Setzen(WisoBerichte.Bericht(teil.Letzte, _schmal()));
            teil.Fehler.Zeigen(null);
        }
        catch (RechnerFehler f)
        {
            // wie die anderen Karten: Kasten leer, dann die Meldung (roter Hinweis, kein Fenster)
            teil.Kasten.Setzen("");
            teil.Fehler.Zeigen(f.Message);
        }
        teil.Weg.Fassung(_schmal());
    }

    /// <summary>Felder setzen (fuer den Start ohne Bedienung); Laufzeit-Einheit nur bei Zinsen.</summary>
    internal void Eingabe(string rechner, IReadOnlyList<string> werte, string? laufzeit = null)
    {
        var teil = _teile[rechner];
        var i = 0;
        foreach (var (schluessel, _, _) in WisoFelder[rechner])
        {
            teil.Felder[schluessel].Text = werte[i++];
        }
        if (laufzeit is not null)
        {
            teil.Laufzeit!.Waehle(laufzeit);
        }
    }

    /// <summary>Aufbau gewechselt: sichtbare Ergebnisse in der passenden Fassung neu schreiben.</summary>
    public void AufbauGeaendert()
    {
        foreach (var teil in _teile.Values)
        {
            if (teil.Letzte is not null && teil.Kasten.Roh.Length > 0)
            {
                teil.Kasten.Setzen(WisoBerichte.Bericht(teil.Letzte, _schmal()));
            }
        }
    }
}
