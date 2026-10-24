using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 0.12.2: Fortschrittsring wie ui.Ring am Handy: grauer Kreis (ring_bg), darauf ein Bogen ab
/// 12 Uhr im Uhrzeigersinn mit runden Enden und Farbverlauf von Farbe A nach Farbe B. Bei 0 ist
/// der Bogen unsichtbar. Farben sind Namen der Farbwelt (z. B. "accent", "purple"); ein
/// Farbwechsel zeichnet neu.
/// </summary>
public sealed class VerlaufRing : Control
{
    private readonly double _dicke;
    private readonly string _a;
    private readonly string _b;
    private double _anteil;

    public VerlaufRing(double anteil, string farbeA, string? farbeB = null, double dicke = 11)
    {
        _anteil = Math.Max(0, Math.Min(1, anteil));
        _a = farbeA;
        _b = farbeB ?? farbeA;
        _dicke = dicke;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        ResourcesChanged += (_, _) => InvalidateVisual();
    }

    public double Anteil
    {
        get => _anteil;
        set
        {
            _anteil = Math.Max(0, Math.Min(1, value));
            InvalidateVisual();
        }
    }

    private static Color Farbe(string name) => Color.Parse(FarbenAnwenden.Hex(name));

    public override void Render(DrawingContext dc)
    {
        var groesse = Math.Min(Bounds.Width, Bounds.Height);
        if (groesse <= _dicke)
        {
            return;
        }
        var mitte = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = (groesse - _dicke) / 2;
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Farbe("ring_bg")), _dicke), mitte, radius, radius);
        if (_anteil <= 0)
        {
            return;
        }
        var winkel = Math.Max(0.02, _anteil) * 360;
        var pinsel = new ConicGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Angle = 0,
            GradientStops =
            {
                new GradientStop(Farbe(_a), 0),
                new GradientStop(Farbe(_b), Math.Max(0.01, _anteil)),
                new GradientStop(_anteil >= 0.999 ? Farbe(_a) : Farbe(_b), 1),
            },
        };
        var stift = new Pen(pinsel, _dicke, lineCap: PenLineCap.Round);
        if (winkel >= 359.9)
        {
            dc.DrawEllipse(null, stift, mitte, radius, radius);
            return;
        }
        var start = Punkt(mitte, radius, 0);
        var ende = Punkt(mitte, radius, winkel);
        var bogen = new StreamGeometry();
        using (var ctx = bogen.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.ArcTo(ende, new Size(radius, radius), 0, winkel > 180, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        dc.DrawGeometry(null, stift, bogen);
    }

    /// <summary>Punkt auf dem Kreis; 0 Grad = 12 Uhr, im Uhrzeigersinn.</summary>
    private static Point Punkt(Point mitte, double radius, double grad)
    {
        var rad = (grad - 90) * Math.PI / 180;
        return new Point(mitte.X + radius * Math.Cos(rad), mitte.Y + radius * Math.Sin(rad));
    }

    /// <summary>
    /// Ring mit grosser Zahl und (optional) kleiner Unterzeile in der Mitte; die Groesse waechst mit
    /// der Schrift (grow). Gross: Stufe der Zahl (Python big_size), klein: Stufe der Unterzeile.
    /// </summary>
    public static Grid MitText(double anteil, string farbeA, string? farbeB, string gross, string? klein, double groesse,
        double dicke, string grossStufe = "Titel", string kleinStufe = "Winzig")
    {
        var ring = new VerlaufRing(anteil, farbeA, farbeB, dicke);
        var texte = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(dicke + 1) };
        var zahl = Schrift.Text(gross, grossStufe, FontWeight.Bold);
        zahl.TextAlignment = TextAlignment.Center;
        zahl.HorizontalAlignment = HorizontalAlignment.Center;
        texte.Children.Add(zahl);
        if (!string.IsNullOrEmpty(klein))
        {
            var unter = Schrift.Text(klein, kleinStufe, FontWeight.Normal, "Pinsel.text_dim");
            unter.TextAlignment = TextAlignment.Center;
            unter.HorizontalAlignment = HorizontalAlignment.Center;
            texte.Children.Add(unter);
        }
        var feld = new Grid { Children = { ring, texte } };
        feld.Bind(WidthProperty, feld.GetResourceObservable("Schrift.Normal", v => v is double d ? groesse * d / Schrift.Grundgroessen["Normal"] : groesse));
        feld.Bind(HeightProperty, feld.GetResourceObservable("Schrift.Normal", v => v is double d ? groesse * d / Schrift.Grundgroessen["Normal"] : groesse));
        Avalonia.Automation.AutomationProperties.SetName(feld, string.Join(" ", new[] { gross, klein }.Where(t => !string.IsNullOrEmpty(t))));
        return feld;
    }
}
