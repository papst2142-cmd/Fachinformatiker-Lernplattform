using Avalonia.Controls;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.36.0 (Weisung P12-171): Plaetze (2 bis 12, Knoepfe − und +) und Reihen (eine oder zwei) eines gewaehlten
/// Parkplatzes unter „Ausgewaehltes Stueck“; jede Aenderung ist ein Schritt im Verlauf.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Start-Test: Knoepfe „−“ und „+“ der Plaetze und die beiden Reihen-Knoepfe (null = kein Parkplatz gewaehlt).</summary>
    internal (Button Weniger, Button Mehr, Button EineReihe, Button ZweiReihen)? ParkplatzKnoepfe { get; private set; }

    private void ParkplatzTeil(AussenStelle s)
    {
        ParkplatzKnoepfe = null;
        if (_projekt is null || s.Art != AussenArt.Parkplatz
            || Parkplaetze.Lesen(AussenBearbeitung.HaltListe(_projekt.AussenStand, s.Art).ElementAtOrDefault(s.Nummer)) is not var (_, n, r))
        {
            return;
        }
        var zahl = Schrift.Text(n.ToString(System.Globalization.CultureInfo.InvariantCulture), "Normal");
        var weniger = Schrift.Knopf("−").Vorlese(Texte.ParkplatzPlaetze + " weniger").Mit(Texte.ParkplatzTipp);
        var mehr = Schrift.Knopf("+").Vorlese(Texte.ParkplatzPlaetze + " mehr").Mit(Texte.ParkplatzTipp);
        weniger.IsEnabled = !_projekt.NurLesen && n > Parkplaetze.Mindestens;
        mehr.IsEnabled = !_projekt.NurLesen && n < Parkplaetze.Hoechstens;
        weniger.Click += (_, _) => ParkplatzSetzen(s, n - 1, r);
        mehr.Click += (_, _) => ParkplatzSetzen(s, n + 1, r);
        var eine = Schrift.Knopf(Texte.ParkplatzEineReihe, betont: r == 1).Vorlese(Texte.ParkplatzEineReihe);
        var zwei = Schrift.Knopf(Texte.ParkplatzZweiReihen, betont: r == 2).Vorlese(Texte.ParkplatzZweiReihen);
        eine.IsEnabled = zwei.IsEnabled = !_projekt.NurLesen;
        eine.Click += (_, _) => ParkplatzSetzen(s, n, 1);
        zwei.Click += (_, _) => ParkplatzSetzen(s, n, 2);
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.ParkplatzPlaetze, "Klein"));
        _aussenAuswahl.Children.Add(new WrapPanel { Children = { weniger, zahl, mehr } });
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.ParkplatzReihen, "Klein"));
        _aussenAuswahl.Children.Add(new WrapPanel { Children = { eine, zwei } });
        ParkplatzKnoepfe = (weniger, mehr, eine, zwei);
    }

    /// <summary>Plaetze und Reihen setzen (ein Schritt; auch fuer den Start-Test). false = ungueltig oder kein Projekt.</summary>
    internal bool ParkplatzSetzen(AussenStelle s, int plaetze, int reihen)
    {
        if (_projekt is null || _projekt.NurLesen || !_projekt.AussenAendern(Texte.Parkplatz, a => AussenBearbeitung.ParkplatzAendern(a, s, plaetze, reihen)))
        {
            return false;
        }
        Melden(Texte.Parkplatz + ": " + Texte.ParkplatzPlaetzeText(plaetze, reihen));
        Karte.Waehlen(s);
        return true;
    }
}
