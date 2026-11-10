using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.31.0 (Sammelliste 264, Weisung P12-130 Frage 3 = c; Tuer-Farben Nico 10.11.2026): Knopf „Farbe“ unter
/// „Ausgewähltes Stück“ an jedem Gebaeude (Ort, Gebaeude ohne Ort, eigenes Gebaeude). Er klappt die
/// <see cref="FarbWahl"/> auf; jede Wahl ist ein Schritt im Verlauf, die Vorschau in 2D und 3D folgt sofort.
/// </summary>
public sealed partial class Hauptansicht
{
    private bool _farbeOffen;

    /// <summary>Start-Test: die offene Farbwahl (null = zu).</summary>
    internal FarbWahl? Farbwahl { get; private set; }

    /// <summary>Start-Test: Knopf „Farbe“ (null = kein Gebaeude gewaehlt).</summary>
    internal Avalonia.Controls.Button? FarbeKnopf { get; private set; }

    private void FarbeTeil(AussenStelle s)
    {
        Farbwahl = null;
        FarbeKnopf = null;
        if (_projekt is null || AussenBearbeitung.FarbKennung(_projekt.AussenStand, s) is not { } kennung)
        {
            return;
        }
        var k = Schrift.Knopf(_farbeOffen ? Texte.FarbeZu : Texte.FarbeKnopf, betont: _farbeOffen).Vorlese(_farbeOffen ? Texte.FarbeZu : Texte.FarbeKnopf).Mit(Texte.FarbeKnopfTipp);
        k.Click += (_, _) =>
        {
            _farbeOffen = !_farbeOffen;
            AussenAuswahlZeigen();
        };
        FarbeKnopf = k;
        _aussenAuswahl.Children.Add(k);
        if (!_farbeOffen || _projekt.NurLesen)
        {
            return;
        }
        Farbwahl = new FarbWahl(
            teil => AussenBearbeitung.FarbeVon(_projekt.AussenStand, kennung, teil),
            (teil, wert) => FarbeSetzen(kennung, teil, wert));
        _aussenAuswahl.Children.Add(Farbwahl.Ansicht);
    }

    /// <summary>Farbe eines Teils setzen (ein Schritt; auch fuer den Start-Test).</summary>
    public void FarbeSetzen(string kennung, string teil, string? wert)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        var gewaehlt = Karte.Auswahl;
        _projekt.AussenAendern(Texte.FarbeSchritt(Lernplattform.Core.Spiel.Probe.Gebaeudefarben.TeilName(teil)), aa => AussenBearbeitung.FarbeSetzen(aa, kennung, teil, wert));
        if (gewaehlt is not null && AussenBearbeitung.Gueltig(_projekt.AussenStand, gewaehlt))
        {
            Karte.Waehlen(gewaehlt);
        }
    }
}
