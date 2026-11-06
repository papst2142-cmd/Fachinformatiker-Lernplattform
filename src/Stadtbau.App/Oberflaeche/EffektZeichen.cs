using Avalonia;
using Avalonia.Media;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Weisung P12-25 Abschnitt 3 Punkt 2: Zeichen eines Effekts (Partikel, "Nur Aussehen") in der Draufsicht und im
/// Grundriss. Ein Stern mit vier langen und vier kurzen Zacken in der Farbe des Effekts, dunkel umrandet, auf einem
/// violetten Leuchtrand. Eine Laterne bleibt ein gelber Punkt; beide Ansichten zeichnen den Effekt nur hier.
/// </summary>
internal static class EffektZeichen
{
    /// <summary>Name der Form (Start-Test: Effekt und Laterne verschieden, Draufsicht und Grundriss gleich).</summary>
    public const string Stern = "Stern";

    /// <summary>Form der Laterne in der Draufsicht (nur zum Vergleich im Start-Test).</summary>
    public const string Punkt = "Punkt";

    /// <summary>Gegenprobe: zeichnet den Effekt wieder als gelben Punkt wie vor der Nachbesserung.</summary>
    public static bool AltFuerTests { get; set; }

    private static readonly Color Schein = Color.Parse("#B388FF");
    private static readonly Color RandFarbe = Color.Parse("#311B92");

    /// <summary>Effekt zeichnen; gibt die gezeichnete Form zurueck.</summary>
    public static string Zeichnen(DrawingContext ctx, Point mitte, double radius, string? farbe)
    {
        var kern = Color.TryParse(farbe ?? "", out var f) ? f : Color.Parse("#FFD54F");
        if (AltFuerTests)
        {
            ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#FFC107"), 0.30), null, mitte, radius * 1.5, radius * 1.5);
            ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#FFC107"), 0.55), null, mitte, radius, radius);
            ctx.DrawEllipse(new SolidColorBrush(kern), new Pen(new SolidColorBrush(Color.Parse("#7A5A00")), 1.5), mitte, radius * 0.5, radius * 0.5);
            return Punkt;
        }
        ctx.DrawEllipse(new SolidColorBrush(Schein, 0.28), null, mitte, radius * 1.45, radius * 1.45);
        ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Schein, 0.9), 2), mitte, radius * 1.15, radius * 1.15);
        ctx.DrawGeometry(new SolidColorBrush(kern), new Pen(new SolidColorBrush(RandFarbe), 1.5), Form(mitte, radius * 1.1));
        return Stern;
    }

    /// <summary>Stern mit 4 langen und 4 kurzen Zacken.</summary>
    public static StreamGeometry Form(Point mitte, double radius)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        for (var i = 0; i < 16; i++)
        {
            var winkel = (Math.PI / 8 * i) - (Math.PI / 2);
            var r = (i % 4) switch { 0 => radius, 2 => radius * 0.62, _ => radius * 0.3 };
            var p = new Point(mitte.X + (r * Math.Cos(winkel)), mitte.Y + (r * Math.Sin(winkel)));
            if (i == 0)
            {
                c.BeginFigure(p, true);
            }
            else
            {
                c.LineTo(p);
            }
        }
        c.EndFigure(true);
        return g;
    }
}
