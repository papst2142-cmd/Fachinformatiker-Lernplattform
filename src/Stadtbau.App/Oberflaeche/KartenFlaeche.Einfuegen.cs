using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.47.0 (Weisung P12-226): Vorschau beim Einfuegen aus der Zwischenablage (Umrisse folgen der Maus bzw. dem
/// letzten Tipp, gruen = passt auf die Karte, rot = ragt ueber den Rand) und der Vorschlag von „Laterne versetzen“
/// (alte Stelle rot, neue gruen, Pfeil dazwischen). Beides ist nur Anzeige; der Schritt geschieht in der Hauptansicht.
/// </summary>
public sealed partial class KartenFlaeche
{
    private Zwischenablage? _einfuegen;
    private Vector2? _einfuegenMitte;
    private LaterneVersetzen.Vorschlag? _laterneVorschlag;

    /// <summary>Zwischenablage, die gerade eingefuegt wird (null = kein Einfuegen).</summary>
    internal Zwischenablage? EinfuegenAblage
    {
        get => _einfuegen;
        set
        {
            _einfuegen = value;
            _einfuegenMitte = null;
            InvalidateVisual();
        }
    }

    /// <summary>Wo die Mitte der Vorschau gerade liegt (Start-Test).</summary>
    internal Vector2? EinfuegenMitte => _einfuegenMitte;

    /// <summary>Die Vorschau an (x, y) legen (Maus schwebt oder Start-Test).</summary>
    internal void EinfuegenSchweben(double x, double y)
    {
        if (_einfuegen is null)
        {
            return;
        }
        _einfuegenMitte = new Vector2((float)x, (float)y);
        InvalidateVisual();
    }

    /// <summary>Vorschlag von „Laterne versetzen“ auf der Karte zeigen (null = keiner).</summary>
    internal LaterneVersetzen.Vorschlag? LaterneVorschlag
    {
        get => _laterneVorschlag;
        set
        {
            _laterneVorschlag = value;
            InvalidateVisual();
        }
    }

    private void EinfuegenSchwebenMaus(PointerEventArgs e, Point p)
    {
        if (_einfuegen is not null && !_geste.Aktiv && e.Pointer.Type == PointerType.Mouse)
        {
            var w = WeltPunkt(p);
            EinfuegenSchweben(w.X, w.Y);
        }
    }

    private void EinfuegenVorschauZeichnen(DrawingContext ctx)
    {
        if (_einfuegen is not { } z || _einfuegenMitte is not { } m || Stand is not { } a)
        {
            return;
        }
        var (dx, dy) = AussenBearbeitung.EinfuegenVersatz(z, m.X, m.Y, Raster);
        var gut = AussenBearbeitung.EinfuegenPasst(a, z, dx, dy);
        var fuellung = Pinsel(gut ? "#34D399" : "#F87171", 0.45);
        var stift = new Pen(Pinsel("#111827"), 1.5, new DashStyle([4, 3], 0));
        var mitte = new Vector2((float)(z.Mitte.X + dx), (float)(z.Mitte.Y + dy));
        foreach (var u in z.Umrisse)
        {
            Flaeche(ctx, u.Select(p => p + mitte).ToList(), fuellung, stift);
        }
        ctx.DrawEllipse(Brushes.White, new Pen(Pinsel("#111827"), 2), P(mitte.X, mitte.Y), 4, 4);
    }

    private void LaterneVorschlagZeichnen(DrawingContext ctx)
    {
        if (_laterneVorschlag is not { } v)
        {
            return;
        }
        var von = P(v.Von.X, v.Von.Y);
        var nach = P(v.Nach.X, v.Nach.Y);
        ctx.DrawLine(new Pen(Pinsel("#111827"), 2, new DashStyle([4, 3], 0)), von, nach);
        ctx.DrawEllipse(Pinsel("#F87171", 0.7), new Pen(Pinsel("#111827"), 1.5), von, 8, 8);
        ctx.DrawEllipse(Pinsel("#34D399", 0.9), new Pen(Pinsel("#111827"), 2), nach, 9, 9);
    }
}
