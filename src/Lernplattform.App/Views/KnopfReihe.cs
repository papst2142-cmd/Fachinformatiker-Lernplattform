using Avalonia;
using Avalonia.Controls;

namespace Lernplattform.Views;

/// <summary>
/// Phase 4, W4: Knopfzeile des Rundgangs, die sich der Breite anpasst. Kinder: zuerst der Hauptknopf
/// ("Weiter" bzw. "Speichern und loslegen"), dann die Nebenknoepfe ("Überspringen", ggf. "Zurück").
/// Passt alles in eine Zeile (nur wenn <see cref="EineZeile"/>, PC wie app_gui 0.63.2): links
/// "Überspringen", rechts "Zurück" und der Hauptknopf. Sonst wie main.py (Handy): oben der Hauptknopf
/// ueber die ganze Breite, darunter die Nebenknoepfe gleich breit nebeneinander; passen sie auch so
/// nicht (grosse Schrift, schmales Fenster), steht jeder Knopf ueber die ganze Breite untereinander.
/// </summary>
public sealed class KnopfReihe : Panel
{
    private enum Art { Zeile, HauptOben, Untereinander }

    private Art _art;

    /// <summary>Eine Zeile erlaubt (PC); am Handy immer mindestens "Hauptknopf oben".</summary>
    public bool EineZeile { get; init; }

    protected override Size MeasureOverride(Size verfuegbar)
    {
        var kinder = Children.Where(k => k.IsVisible).ToList();
        if (kinder.Count == 0)
        {
            return default;
        }
        foreach (var kind in kinder)
        {
            kind.Measure(Size.Infinity);
        }
        var breite = verfuegbar.Width;
        var neben = kinder.Skip(1).ToList();
        var summe = kinder.Sum(k => k.DesiredSize.Width);
        var breitesterNeben = neben.Count == 0 ? 0 : neben.Max(k => k.DesiredSize.Width);
        _art = EineZeile && summe <= breite ? Art.Zeile
            : breitesterNeben * neben.Count <= breite && kinder[0].DesiredSize.Width <= breite ? Art.HauptOben
            : Art.Untereinander;
        if (_art == Art.Zeile)
        {
            return new Size(summe, kinder.Max(k => k.DesiredSize.Height));
        }
        // jetzt mit der echten Breite messen (Text darf umbrechen)
        kinder[0].Measure(new Size(breite, double.PositiveInfinity));
        var hoehe = kinder[0].DesiredSize.Height;
        if (_art == Art.HauptOben)
        {
            var spalte = neben.Count == 0 ? breite : breite / neben.Count;
            var zeile = 0.0;
            foreach (var k in neben)
            {
                k.Measure(new Size(spalte, double.PositiveInfinity));
                zeile = Math.Max(zeile, k.DesiredSize.Height);
            }
            return new Size(breite, hoehe + zeile);
        }
        foreach (var k in neben)
        {
            k.Measure(new Size(breite, double.PositiveInfinity));
            hoehe += k.DesiredSize.Height;
        }
        return new Size(breite, hoehe);
    }

    protected override Size ArrangeOverride(Size endgroesse)
    {
        var kinder = Children.Where(k => k.IsVisible).ToList();
        if (kinder.Count == 0)
        {
            return endgroesse;
        }
        var breite = endgroesse.Width;
        var neben = kinder.Skip(1).ToList();
        switch (_art)
        {
            case Art.Zeile:
            {
                var hoehe = endgroesse.Height;
                // "Überspringen" links, rechts "Zurück" und der Hauptknopf
                kinder[1 % kinder.Count].Arrange(new Rect(0, 0, kinder[1 % kinder.Count].DesiredSize.Width, hoehe));
                var x = breite - kinder[0].DesiredSize.Width;
                kinder[0].Arrange(new Rect(x, 0, kinder[0].DesiredSize.Width, hoehe));
                foreach (var k in neben.Skip(1).Reverse())
                {
                    x -= k.DesiredSize.Width;
                    k.Arrange(new Rect(x, 0, k.DesiredSize.Width, hoehe));
                }
                break;
            }
            case Art.HauptOben:
            {
                var oben = kinder[0].DesiredSize.Height;
                kinder[0].Arrange(new Rect(0, 0, breite, oben));
                var spalte = neben.Count == 0 ? breite : breite / neben.Count;
                var zeile = neben.Count == 0 ? 0 : neben.Max(k => k.DesiredSize.Height);
                for (var i = 0; i < neben.Count; i++)
                {
                    neben[i].Arrange(new Rect(i * spalte, oben, spalte, zeile));
                }
                break;
            }
            default:
            {
                var y = 0.0;
                foreach (var k in kinder)
                {
                    k.Arrange(new Rect(0, y, breite, k.DesiredSize.Height));
                    y += k.DesiredSize.Height;
                }
                break;
            }
        }
        return endgroesse;
    }
}
