using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 9 W4 (Weisung P9-10 Teil B, Sammelliste 178): Karte "Spiel" wie _build_spiel (app_gui.py) und der Bereich
/// "spiel" der Handy-App (mobile/src/main.py) in Python 0.63.2: oben die Zeile zum Platz und Schwierigkeitsgrad
/// (slot_options_text nach game.reload(), fett), darunter WOHNUNGEN mit "Einmalzahlung" / "Miete" (einstellungen.json
/// dieses Geraets, Schluessel spiel_miete, geschrieben nur ueber Einstellungen.Aendern) und der Erklaertext.
/// Die Zeile wird bei jedem Aufklappen der Karte im Hintergrund gelesen (wie _show_difficulty beim Zeigen der Optionen).
/// </summary>
public sealed partial class OptionenSeite
{
    private readonly List<RadioButton> _miete = [];
    private TextBlock? _spielZeile;
    private int _spielZeileLauf;

    /// <summary>Start-Test: die angezeigte Zeile zum Platz und Schwierigkeitsgrad (null ohne Karte).</summary>
    public string? SpielZeile => _spielZeile?.Text;

    /// <summary>Start-Test: wird fertig, wenn die Zeile nach dem letzten Anstoss gelesen ist.</summary>
    public Task SpielZeileGelesen { get; private set; } = Task.CompletedTask;

    private Control SpielKarte()
    {
        var panel = new StackPanel { Spacing = 6 };
        _spielZeile = new LernText("", "Normal", FontWeight.Bold, "Pinsel.text_soft");
        panel.Children.Add(_spielZeile);
        var titel = Abschnitt(Wohnung.MieteTitel);
        titel.Margin = new Avalonia.Thickness(0, 4, 0, 0);
        panel.Children.Add(titel);
        var aktuell = Wohnung.MieteGewaehlt(Zustand.Werte) ? "miete" : "einmal";
        var reihe = new WrapPanel();
        foreach (var (id, text) in Wohnung.MieteWahl)
        {
            var wahl = Auswahl(text, "spiel_miete", id == aktuell);
            wahl.Tag = id;
            wahl.IsCheckedChanged += (_, _) =>
            {
                if (_setzen || wahl.IsChecked != true)
                {
                    return;
                }
                if (!Ergebnis(Zustand.Aendere(werte => Wohnung.MieteSetzen(werte, id == "miete"))))
                {
                    Zuruecksetzen(_miete, Wohnung.MieteGewaehlt(Zustand.Werte) ? "miete" : "einmal");
                }
            };
            _miete.Add(wahl);
            reihe.Children.Add(wahl);
        }
        panel.Children.Add(reihe);
        panel.Children.Add(new LernText(Wohnung.MieteHilfe(Lernplattform.Spiel.SpielStart.Balancing()), "Winzig", FontWeight.Normal, "Pinsel.muted"));
        return panel;     // die Zeile oben liest erst das Aufklappen (alle Karten starten zugeklappt)
    }

    /// <summary>Zeile zum Platz und Schwierigkeitsgrad neu lesen (Hintergrund); nur das Ergebnis des letzten Anstosses wird gezeigt.</summary>
    private void SpielZeileNeu()
    {
        if (_spielZeile is null)
        {
            return;
        }
        var lauf = ++_spielZeileLauf;
        SpielZeileGelesen = LesenAsync();

        async Task LesenAsync()
        {
            var zeile = await Task.Run(Lernplattform.Spiel.SpielStart.OptionenZeile);
            if (lauf == _spielZeileLauf && _spielZeile is not null)
            {
                _spielZeile.Text = zeile;      // await kehrt auf den Oberflaechen-Faden zurueck
            }
        }
    }
}
