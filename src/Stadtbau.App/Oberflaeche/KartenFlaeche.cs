using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Design;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 2: Draufsicht der Stadt aus der Fassung (Plan Abschnitt 3, Teil 1 nur ansehen): Parks, Fluss, Strassen,
/// Bahn und die Orte mit Namen. Ein Ort mit bearbeitbarem Innenraum ist hervorgehoben; Antippen oeffnet ihn.
/// Die Stadt aussen zu bearbeiten kommt mit Welle 3 (S 0.2.0).
/// </summary>
public sealed class KartenFlaeche : Control
{
    private Spieldaten? _daten;
    private double _zoom = 20;
    private Point _versatz;

    public KartenFlaeche()
    {
        ClipToBounds = true;
        Focusable = false;
    }

    public event Action<string>? OrtGewaehlt;

    public void Zeigen(Spieldaten? daten)
    {
        _daten = daten;
        Einpassen();
        InvalidateVisual();
    }

    private (double B, double H) Welt()
    {
        var welt = _daten is null ? null : Get(_daten.Fassung.Karte, "welt") as PyObjekt;
        return welt is null ? (32, 24) : (Py.F(welt["breite"]), Py.F(welt["hoehe"]));
    }

    private void Einpassen()
    {
        if (Bounds.Width < 10)
        {
            return;
        }
        var (b, h) = Welt();
        _zoom = Math.Min((Bounds.Width - 24) / b, (Bounds.Height - 24) / h);
        _versatz = new Point((Bounds.Width - (b * _zoom)) / 2, (Bounds.Height - (h * _zoom)) / 2);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Einpassen();
    }

    private Point P(double x, double y) => new(_versatz.X + (x * _zoom), _versatz.Y + (y * _zoom));

    /// <summary>Alle Orte der Karte (Grundkarte und neue Orte aus den Stadtdaten) mit Lage.</summary>
    public IEnumerable<(string Id, string Name, double X, double Y)> Orte()
    {
        if (_daten is null)
        {
            yield break;
        }
        foreach (var quelle in new[] { Get(_daten.Fassung.Karte, "orte"), Get(_daten.Fassung.Stadtdaten, "neue_orte") })
        {
            if (quelle is List<object?> liste)
            {
                foreach (var o in liste.OfType<PyObjekt>())
                {
                    yield return (Py.Str(o["id"]), o["name"] as string ?? Py.Str(o["id"]), Py.F(o["x"]), Py.F(o["y"]));
                }
            }
        }
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse(FarbenAnwenden.Aktuell.Farben["bg"])), new Rect(Bounds.Size));
        if (_daten is null)
        {
            return;
        }
        var (b, h) = Welt();
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#B9D29A")), new Rect(P(0, 0), P(b, h)));
        var welt = Get(_daten.Fassung.Karte, "welt") as PyObjekt;
        var schrift = new Typeface(FontFamily.Default);
        foreach (var l in welt is null ? [] : ListeOderLeer(welt, "landschaft").OfType<PyObjekt>())
        {
            switch (l["typ"] as string)
            {
                case "park":
                    ctx.FillRectangle(new SolidColorBrush(Color.Parse("#8DBA6A")), new Rect(P(Py.F(l["x"]), Py.F(l["y"])), P(Py.F(l["x"]) + Py.F(l["w"]), Py.F(l["y"]) + Py.F(l["h"]))));
                    break;
                case "fluss":
                    Linie(ctx, l, new Pen(new SolidColorBrush(Color.Parse("#5B9BD5")), 0.9 * _zoom, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
                    break;
                case "strasse":
                    Linie(ctx, l, new Pen(new SolidColorBrush(Color.Parse("#6B6B6B")), 0.8 * _zoom, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
                    break;
                case "bahn":
                    Linie(ctx, l, new Pen(new SolidColorBrush(Color.Parse("#5A4632")), 0.25 * _zoom, new DashStyle([2, 1], 0)));
                    break;
                case "ortsname":
                case "beschriftung":
                    Text(ctx, schrift, l["text"] as string ?? "", P(Py.F(l["x"]), Py.F(l["y"])), 12, Brushes.Black);
                    break;
            }
        }
        var bearbeitbar = new HashSet<string>(_daten.Orte, StringComparer.Ordinal);
        foreach (var (id, name, x, y) in Orte())
        {
            var mit = bearbeitbar.Contains(id);
            var m = P(x, y);
            ctx.DrawEllipse(new SolidColorBrush(Color.Parse(mit ? FarbenAnwenden.Aktuell.Farben["accent"] : "#8A8A8A")), new Pen(Brushes.White, 2), m, mit ? 9 : 6, mit ? 9 : 6);
            Text(ctx, schrift, name, m + new Point(11, -9), mit ? 13 : 11, Brushes.Black);
        }
    }

    private void Linie(DrawingContext ctx, PyObjekt l, Pen stift)
    {
        if (l["punkte"] is not List<object?> punkte || punkte.Count < 2)
        {
            return;
        }
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            var erster = true;
            foreach (var p in punkte.OfType<List<object?>>())
            {
                var q = P(Py.F(p[0]), Py.F(p[1]));
                if (erster)
                {
                    g.BeginFigure(q, false);
                    erster = false;
                }
                else
                {
                    g.LineTo(q);
                }
            }
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, stift, geo);
    }

    private static void Text(DrawingContext ctx, Typeface schrift, string text, Point ort, double groesse, IBrush pinsel) =>
        ctx.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, groesse, pinsel), ort);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_daten is null)
        {
            return;
        }
        var p = e.GetPosition(this);
        var bearbeitbar = new HashSet<string>(_daten.Orte, StringComparer.Ordinal);
        foreach (var (id, _, x, y) in Orte())
        {
            var m = P(x, y);
            if (bearbeitbar.Contains(id) && Math.Abs(m.X - p.X) < 22 && Math.Abs(m.Y - p.Y) < 22)
            {
                OrtGewaehlt?.Invoke(id);
                e.Handled = true;
                return;
            }
        }
    }
}
