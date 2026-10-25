using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Phase 7, W6: zeichnet Formen aus <see cref="SpielBilder"/> (Python-dicts "rect", "oval", "line",
/// "poly") eingepasst in ein Feld, wie shapes_canvas und FloorPlan._shape in mobile/src/spiel.py:
/// Striche mit runden Enden, Strichbreite max(1, lw x Massstab).
/// </summary>
public sealed class Formen : Control
{
    private readonly IReadOnlyList<PyObjekt> _formen;
    private readonly double _w;
    private readonly double _h;

    public Formen(IReadOnlyList<PyObjekt> formen, double w, double h, double breite, double hoehe)
    {
        _formen = formen;
        _w = w;
        _h = h;
        Width = breite;
        Height = hoehe;
    }

    /// <summary>badge_image: rundes Abzeichen (gesperrt mit Schloss, wenn stufe null ist).</summary>
    public static Formen Abzeichen(string? bild, string? farbe, string? stufe, double groesse = 56) =>
        new(SpielBilder.Abzeichen(Design.FarbenAnwenden.Aktuell, bild, farbe, stufe), SpielBilder.AbzeichenGroesse, SpielBilder.AbzeichenGroesse, groesse, groesse);

    public override void Render(DrawingContext context)
    {
        var massstab = Math.Min(Width / _w, Height / _h);
        var ox = (Width - (massstab * _w)) / 2.0;
        var oy = (Height - (massstab * _h)) / 2.0;
        foreach (var form in _formen)
        {
            Zeichnen(context, form, massstab, ox, oy);
        }
    }

    private static double D(object? wert) => Py.F(wert);

    private static IPen Strich(string farbe, double breite, double massstab) =>
        new Pen(Bau.Pinsel(farbe), Math.Max(1.0, breite * massstab), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    private static void Zeichnen(DrawingContext context, PyObjekt form, double s, double ox, double oy)
    {
        switch (form["k"] as string)
        {
            case "rect" or "oval":
            {
                var rechteck = new Rect(ox + (D(form["x"]) * s), oy + (D(form["y"]) * s), Math.Max(0, D(form["w"]) * s), Math.Max(0, D(form["h"]) * s));
                var fuellung = form["fill"] is string f && f.Length > 0 ? Bau.Pinsel(f) : null;
                var linie = form["line"] is string l && l.Length > 0 ? Strich(l, D(form["lw"]), s) : null;
                if ((string)form["k"]! == "oval")
                {
                    var mitte = rechteck.Center;
                    context.DrawEllipse(fuellung, linie, mitte, rechteck.Width / 2, rechteck.Height / 2);
                }
                else
                {
                    var r = D(form["r"]) * s;
                    context.DrawRectangle(fuellung, linie, new RoundedRect(rechteck, r));
                }
                break;
            }
            case "line":
            {
                var p = (List<object?>)form["pts"]!;
                context.DrawLine(Strich((string)form["color"]!, D(form["lw"]), s),
                    new Point(ox + (D(p[0]) * s), oy + (D(p[1]) * s)), new Point(ox + (D(p[2]) * s), oy + (D(p[3]) * s)));
                break;
            }
            case "poly":
            {
                var p = (List<object?>)form["pts"]!;
                var geometrie = new StreamGeometry();
                using (var g = geometrie.Open())
                {
                    g.BeginFigure(new Point(ox + (D(p[0]) * s), oy + (D(p[1]) * s)), true);
                    for (var i = 2; i + 1 < p.Count; i += 2)
                    {
                        g.LineTo(new Point(ox + (D(p[i]) * s), oy + (D(p[i + 1]) * s)));
                    }
                    g.EndFigure(true);
                }
                var fuellung = form["fill"] is string f && f.Length > 0 ? Bau.Pinsel(f) : null;
                var linie = form["line"] is string l && l.Length > 0 ? Strich(l, D(form["lw"]), s) : null;
                context.DrawGeometry(fuellung, linie, geometrie);
                break;
            }
        }
    }
}

/// <summary>
/// avatar(): die Spielfigur im Kreis wie mobile/src/spiel.avatar_shapes (Kreis card_hi, Figur auf 84 %,
/// Unterkante in den Kreis eingepasst, Ecken 6 x Massstab, Striche max(2, 3 x Massstab)).
/// </summary>
public sealed class Avatar : Control
{
    private readonly PyObjekt? _aussehen;

    public Avatar(PyObjekt? aussehen, double groesse = 96)
    {
        _aussehen = aussehen;
        Width = Height = groesse;
    }

    public override void Render(DrawingContext context)
    {
        var size = Width;
        context.DrawEllipse(Bau.Pinsel("card_hi"), null, new Point(size / 2, size / 2), (size / 2) - 1, (size / 2) - 1);
        var inner = 0.84;
        var offset = size * (1 - inner) / 2;
        var s = size / 100.0 * inner;
        foreach (var (art, (x1, y1, x2, y2), farbe) in SpielBilder.Avatar(_aussehen))
        {
            double ax = offset + (x1 * s), ay = offset + (size * 0.04) + (y1 * s);
            double bx = offset + (x2 * s), by = offset + (size * 0.04) + (y2 * s);
            by = Math.Min(by, size * 0.93);
            var pinsel = Bau.Pinsel(farbe);
            if (art == "oval")
            {
                context.DrawEllipse(pinsel, null, new Point((ax + bx) / 2, (ay + by) / 2), Math.Max(0, (bx - ax) / 2), Math.Max(0, (by - ay) / 2));
            }
            else if (art == "rect")
            {
                context.DrawRectangle(pinsel, null, new RoundedRect(new Rect(ax, ay, Math.Max(0, bx - ax), Math.Max(0, by - ay)), 6 * s));
            }
            else
            {
                context.DrawLine(new Pen(pinsel, Math.Max(2, 3 * s), lineCap: PenLineCap.Round), new Point(ax, ay), new Point(bx, by));
            }
        }
    }
}
