using Avalonia;
using Avalonia.Media;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle Q1 (S 0.40.0, Weisung P12-186, Bild 4 aus Meldung P12-111): ein Questgeber in der Draufsicht oder im Grundriss als
/// gruener Kreis mit Pfeil in Blickrichtung und dem Namen daneben (gewaehlt: dicker Rand). Gleiche Zeichnung an beiden Stellen.
/// </summary>
internal sealed record QuestgeberMarke(string Id, double X, double Y, long Blick, string Name, bool Gewaehlt)
{
    public static readonly IBrush Gruen = new SolidColorBrush(Color.Parse("#2E8B57"));

    public static readonly IBrush Rand = new SolidColorBrush(Color.Parse("#0E4D2C"));

    /// <summary>S 0.53.0 E4: Marke eines Arbeitsorts im Grundriss (lila Kreis mit „A“ wie in der Draufsicht, ohne Blickpfeil).</summary>
    public bool Arbeitsort { get; init; }

    /// <summary>Zeichnen um den Bildpunkt <paramref name="m"/> mit Radius <paramref name="r"/>.</summary>
    public void Zeichnen(DrawingContext ctx, Point m, double r, Typeface schrift, IBrush textFarbe)
    {
        if (Arbeitsort)
        {
            ArbeitsortZeichnen(ctx, m, r * 1.3, schrift, textFarbe);
            return;
        }
        var (rx, ry) = Etagen.Richtung(Blick);
        var basis = m + new Vector(rx * r * 0.9, ry * r * 0.9);
        var spitze = m + new Vector(rx * r * 2.1, ry * r * 2.1);
        var quer = new Vector(-ry, rx) * (r * 0.75);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(spitze, true);
            g.LineTo(basis + quer);
            g.LineTo(basis - quer);
            g.EndFigure(true);
        }
        ctx.DrawGeometry(Rand, null, geo);
        ctx.DrawEllipse(Gruen, new Pen(Gewaehlt ? Brushes.White : Rand, Gewaehlt ? 3 : 2), m, r, r);
        var ft = new FormattedText(Name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, schrift, 13, textFarbe)
        {
            MaxTextWidth = 220,
        };
        if (Gewaehlt)
        {
            ft.SetFontWeight(FontWeight.Bold);
        }
        ctx.DrawText(ft, new Point(m.X + r + 5, m.Y - (ft.Height / 2)));
    }

    private void ArbeitsortZeichnen(DrawingContext ctx, Point m, double r, Typeface schrift, IBrush textFarbe)
    {
        var lila = new SolidColorBrush(Color.Parse(KartenFlaeche.Lila));
        ctx.DrawEllipse(lila, new Pen(Brushes.White, Gewaehlt ? 3 : 2), m, r, r);
        var a = new FormattedText("A", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(schrift.FontFamily, FontStyle.Normal, FontWeight.Bold), Math.Max(10, r * 1.1), Brushes.White);
        ctx.DrawText(a, m - new Vector(a.Width / 2, a.Height / 2));
        var ft = new FormattedText(Name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, schrift, 13, textFarbe)
        {
            MaxTextWidth = 220,
        };
        ft.SetFontWeight(FontWeight.Bold);
        ctx.DrawText(ft, new Point(m.X + r + 5, m.Y - (ft.Height / 2)));
    }
}
