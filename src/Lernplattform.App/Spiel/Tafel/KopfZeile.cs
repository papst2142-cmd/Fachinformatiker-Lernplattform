using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Sammelliste 381 (Weisung P12-298): Kopfzeile der Spiel-Tafel. Links die Krumen, rechts Geld und „Zurück“. Passen die
/// Krumen ganz neben den rechten Teil (Vergleich nach der Rundung auf Bildpunkte des Geraets, nie mit fester Zugabe), steht
/// alles in einer Zeile; sonst stehen die Krumen in voller Breite oben und Geld und „Zurück“ rechtsbuendig darunter, damit
/// kein Wort mitten getrennt wird (Layoutpruefung: „HAUS|E“ am Handy 384 breit mit Schrift „riesig“).
/// </summary>
internal sealed class KopfZeile : Panel
{
    private readonly Control _links;
    private readonly Control _rechts;
    private readonly double _abstand;

    public KopfZeile(Control links, Control rechts, double abstand)
    {
        (_links, _rechts, _abstand) = (links, rechts, abstand);
        links.VerticalAlignment = VerticalAlignment.Center;
        rechts.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(links);
        Children.Add(rechts);
    }

    /// <summary>Stehen die Krumen ueber Geld und „Zurück“ (nach dem letzten Layout)?</summary>
    internal bool Untereinander { get; private set; }

    private double Platz(double punkte)
    {
        var skalierung = TopLevel.GetTopLevel(this)?.RenderScaling is > 0 and var s ? s : 1;
        return Math.Round(punkte * skalierung) / skalierung;
    }

    protected override Size MeasureOverride(Size verfuegbar)
    {
        _rechts.Measure(Size.Infinity);
        var r = _rechts.DesiredSize;
        _links.Measure(new Size(double.PositiveInfinity, verfuegbar.Height));
        var natuerlich = _links.DesiredSize.Width;
        var breite = double.IsInfinity(verfuegbar.Width) ? natuerlich + _abstand + r.Width : verfuegbar.Width;
        var daneben = breite - r.Width - _abstand;
        Untereinander = natuerlich > Platz(daneben);
        if (!Untereinander)
        {
            _links.Measure(new Size(Math.Max(0, daneben), verfuegbar.Height));
            return new Size(breite, Math.Max(_links.DesiredSize.Height, r.Height));
        }
        _links.Measure(new Size(breite, verfuegbar.Height));
        return new Size(breite, _links.DesiredSize.Height + 4 + r.Height);
    }

    protected override Size ArrangeOverride(Size endgroesse)
    {
        var r = _rechts.DesiredSize;
        if (!Untereinander)
        {
            var hoehe = endgroesse.Height;
            var lw = Math.Max(0, endgroesse.Width - r.Width - _abstand);
            _links.Arrange(new Rect(0, (hoehe - _links.DesiredSize.Height) / 2, lw, _links.DesiredSize.Height));
            _rechts.Arrange(new Rect(endgroesse.Width - r.Width, (hoehe - r.Height) / 2, r.Width, r.Height));
            return endgroesse;
        }
        var lh = _links.DesiredSize.Height;
        _links.Arrange(new Rect(0, 0, endgroesse.Width, lh));
        _rechts.Arrange(new Rect(endgroesse.Width - r.Width, lh + 4, r.Width, r.Height));
        return endgroesse;
    }
}
