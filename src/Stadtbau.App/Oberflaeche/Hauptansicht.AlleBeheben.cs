using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-231 Punkt 5, Kurzplan P12-131 Punkt 5): Knopf „Alle beheben, die sicher gehen“ oben in der
/// Liste „Stadt – n rot“, sobald mindestens ein roter Fund einen sicheren Vorschlag hat (<see cref="AlleBeheben.ArtVon"/>).
/// Die Suche rechnet im Hintergrund auf einer Kopie; das Ergebnis kommt als EIN Schritt (<see cref="AlleBeheben.Anwenden"/>),
/// danach steht „n behoben, m bleiben“ unter dem Knopf und in der Statuszeile.
/// </summary>
public sealed partial class Hauptansicht
{
    private CancellationTokenSource? _alleBehebenSuche;

    /// <summary>Knopf „Alle beheben, die sicher gehen“ beim letzten Zeigen der Liste (null = keiner, Start-Test).</summary>
    internal Button? AlleBehebenKnopf { get; private set; }

    /// <summary>Statuszeile unter dem Knopf (Suche, Ergebnis; Start-Test).</summary>
    internal TextBlock? AlleBehebenStatus { get; private set; }

    /// <summary>Letztes Ergebnis von „Alle beheben“ (Start-Test).</summary>
    internal AlleBeheben.Ergebnis? AlleBehebenLetztes { get; private set; }

    /// <summary>Text der Statuszeile nach dem letzten Lauf (bleibt ueber das Neuzeichnen der Liste stehen).</summary>
    private string? _alleBehebenText;

    /// <summary>Projekt und Zahl der Schritte direkt nach dem letzten Lauf: nur solange gilt die Statuszeile noch.</summary>
    private (Projekt? Projekt, int Schritte) _alleBehebenNach;

    /// <summary>Knopf und Statuszeile oben in der Liste draussen (nur mit einem roten Fund, der sicher behoben werden kann).</summary>
    private void AlleBehebenZeigen()
    {
        AlleBehebenKnopf = null;
        AlleBehebenStatus = null;
        if (_projekt is not { NurLesen: false } p)
        {
            return;
        }
        if (!ReferenceEquals(_alleBehebenNach.Projekt, p) || _alleBehebenNach.Schritte != p.SchritteZurueck)
        {
            // seit dem letzten Lauf hat sich die Stadt geaendert (oder ein anderes Projekt): das alte Ergebnis gilt nicht mehr
            _alleBehebenText = null;
        }
        var aussen = p.AussenStand;
        var sicher = AussenFunde.Count(f => AlleBeheben.ArtVon(aussen, f) is not null);
        if (sicher == 0 && _alleBehebenText is null)
        {
            return;
        }
        var kasten = new StackPanel { Spacing = 4, Margin = new Avalonia.Thickness(0, 0, 0, 6) };
        if (sicher > 0)
        {
            var knopf = Schrift.Knopf(Texte.AlleBeheben, betont: true).Vorlese(Texte.AlleBeheben).Mit(Texte.AlleBehebenTipp);
            knopf.Click += async (_, _) => await AlleBehebenAsync();
            AlleBehebenKnopf = knopf;
            kasten.Children.Add(knopf);
        }
        var status = Schrift.Text(_alleBehebenText ?? "", "Klein", farbe: "Pinsel.muted");
        status.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        status.IsVisible = _alleBehebenText is not null;
        AlleBehebenStatus = status;
        kasten.Children.Add(status);
        _aussenPruefungen.Children.Insert(0, kasten);
    }

    /// <summary>
    /// „Alle beheben, die sicher gehen“: sichere Behebungen im Hintergrund suchen, dann als ein Schritt uebernehmen. Rueckgabe:
    /// das Ergebnis (null = abgebrochen, veraltet oder kein Projekt).
    /// </summary>
    public async Task<AlleBeheben.Ergebnis?> AlleBehebenAsync()
    {
        if (_projekt is not { NurLesen: false } p || _daten is not { } daten)
        {
            return null;
        }
        _alleBehebenSuche?.Cancel();
        var abbruch = new CancellationTokenSource();
        _alleBehebenSuche = abbruch;
        if (AlleBehebenKnopf is { } k)
        {
            k.IsEnabled = false;
        }
        StatusSetzen(Texte.AlleBehebenSucht);
        var stand = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var innen = p.Innenraeume();
        var laternen = p.LaternenFuerVorschau();
        AlleBeheben.Ergebnis e;
        try
        {
            e = await Task.Run(() => AlleBeheben.Suchen(stand, p.Basis, innen, daten, p.SzeneOhneLaternen, laternen,
                (a, s) => TuerFassade.Rechnen(p, a, s), abbruch.Token), abbruch.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        if (abbruch.IsCancellationRequested || !ReferenceEquals(_projekt, p))
        {
            return null;
        }
        AlleBehebenLetztes = e;
        if (e.Behoben > 0 && !p.AussenAendern(Texte.AlleBehebenSchritt, a => AlleBeheben.Anwenden(a, e)))
        {
            StatusSetzen(Texte.AlleBehebenVeraltet);
            Melden(Texte.AlleBehebenVeraltet);
            return null;
        }
        var text = Texte.AlleBehebenErgebnis(e.Behoben, e.Bleiben);
        StatusSetzen(text);
        Melden(text);
        _alleBehebenNach = (p, p.SchritteZurueck);
        return e;

        void StatusSetzen(string t)
        {
            _alleBehebenText = t;
            if (AlleBehebenStatus is { } s)
            {
                s.Text = t;
                s.IsVisible = true;
            }
        }
    }
}
