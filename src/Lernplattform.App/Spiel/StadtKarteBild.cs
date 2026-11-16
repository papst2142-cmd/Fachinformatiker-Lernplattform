using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268, Nico Antwort 5): die gezeichnete Karte der Stadt im Spiel-Handy (<see cref="StadtKarte"/>).
/// Antippen einer Nummer oder eines Orts-Hauses meldet den Ort (<see cref="OrtGewaehlt"/>), die Seite laesst die Figur sofort
/// losgehen. Die Farben sind die einer Landkarte (gleich in hell und dunkel, wie ein Bild); Texte der Bedienung stehen als
/// echte Texte und Knoepfe unter der Karte (Screenreader, Kontrast). Norden oben, die ganze Stadt passt hinein.
/// </summary>
public sealed class StadtKarteBild : Control
{
    private static readonly IBrush Gras = new SolidColorBrush(Color.FromRgb(143, 178, 122));
    private static readonly IBrush Park = new SolidColorBrush(Color.FromRgb(112, 158, 96));
    private static readonly IBrush Wasser = new SolidColorBrush(Color.FromRgb(74, 154, 200));
    private static readonly IBrush Haus = new SolidColorBrush(Color.FromRgb(214, 204, 190));
    private static readonly IBrush OrtHaus = new SolidColorBrush(Color.FromRgb(200, 135, 90));
    private static readonly IBrush HausRand = new SolidColorBrush(Color.FromRgb(110, 70, 50));
    private static readonly IBrush Fahrbahn = new SolidColorBrush(Color.FromRgb(66, 60, 88));
    private static readonly IBrush Gehweg = new SolidColorBrush(Color.FromRgb(150, 148, 160));
    private static readonly IBrush Gleis = new SolidColorBrush(Color.FromRgb(120, 84, 60));
    private static readonly IBrush Marke = new SolidColorBrush(Color.FromRgb(36, 32, 52));
    private static readonly IBrush Lila = new SolidColorBrush(Color.FromRgb(139, 61, 240));
    private static readonly IBrush HaltGruen = new SolidColorBrush(Color.FromRgb(30, 120, 66));
    private static readonly IBrush Weiss = Brushes.White;
    private static readonly IBrush Cyan = new SolidColorBrush(Color.FromRgb(20, 170, 215));
    private static readonly IBrush Bereich = new SolidColorBrush(Color.FromRgb(58, 40, 96));
    private static readonly IBrush BereichFlaeche = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
    private static readonly IBrush BereichSchild = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255));

    private StadtKarte? _karte;
    private Vector2 _figur;
    private Point? _druck;

    public StadtKarteBild()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>Ein Ort wurde angetippt (Kennung).</summary>
    public event Action<string>? OrtGewaehlt;

    /// <summary>Die Karte und die Stelle der Figur setzen (neu zeichnen).</summary>
    public void Setzen(StadtKarte karte, Vector2 figur)
    {
        _karte = karte;
        _figur = figur;
        Avalonia.Automation.AutomationProperties.SetName(this, string.Format(CultureInfo.InvariantCulture, KarteTexte.BildName, karte.Orte.Count));
        InvalidateMeasure();
        InvalidateVisual();
    }

    public StadtKarte? Karte => _karte;

    /// <summary>Massstab des letzten Plans (Bildpunkte je Feld), fuer Start-Test und Treffer.</summary>
    public float Massstab => _karte?.Massstab((float)Bounds.Width, (float)Bounds.Height) ?? 1f;

    private static float Faktor => (float)Schrift.Faktor;

    protected override Size MeasureOverride(Size verfuegbar)
    {
        if (_karte is null)
        {
            return default;
        }
        var breite = double.IsInfinity(verfuegbar.Width) ? 360 : verfuegbar.Width;
        var hoehe = breite * _karte.Szene.Tiefe / Math.Max(1, _karte.Szene.Breite);
        return new Size(breite, hoehe);
    }

    /// <summary>Bildpunkt des Orts mit dieser Kennung (Mitte der Marke) oder null (Start-Test: echtes Antippen).</summary>
    public Point? MarkeVon(string id)
    {
        if (_karte is not { } k)
        {
            return null;
        }
        var i = k.Orte.ToList().FindIndex(o => o.Id == id);
        return i < 0 ? null : Punkt(k.Marken(Massstab, Faktor)[i]);
    }

    /// <summary>Start-Test (Fund P12-283): Marken, die ueber den Rand ragen oder sich ueberlappen (leer = gut).</summary>
    public List<string> MarkenFund() => _karte is { } k ? k.MarkenFund(k.Marken(Massstab, Faktor), Massstab, Faktor) : [];

    /// <summary>Antippen an (x, y) in Punkten der Karte (auch fuer den Start-Test); true = ein Ort war getroffen.</summary>
    public bool Tippen(Point p)
    {
        if (_karte?.Treffer(new Vector2((float)p.X, (float)p.Y), Massstab, Faktor) is not { } ort)
        {
            return false;
        }
        OrtGewaehlt?.Invoke(ort.Id);
        return true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _druck = e.GetPosition(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this);
        // nur ein Tipp (kein Wischen der Rolle): wenig Bewegung zwischen Druecken und Loslassen
        if (_druck is { } d && Math.Abs(p.X - d.X) + Math.Abs(p.Y - d.Y) < 12 && Tippen(p))
        {
            e.Handled = true;
        }
        _druck = null;
    }

    private static Point Punkt(Vector2 v) => new(v.X, v.Y);

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        if (_karte is not { } karte || Bounds.Width < 1)
        {
            return;
        }
        var s = Massstab;
        var sz = karte.Szene;
        Point P(Vector2 w) => Punkt(StadtKarte.Bild(w, s));
        dc.DrawRectangle(Gras, null, new Rect(0, 0, sz.Breite * s, sz.Tiefe * s), 10, 10);
        foreach (var (x1, z1, x2, z2) in sz.Parks)
        {
            dc.DrawRectangle(Park, null, new Rect(P(new(x1, z1)), P(new(x2, z2))));
        }
        foreach (var t in sz.Teiche)
        {
            dc.DrawGeometry(Wasser, null, Flaeche(t.Rund.Select(P)));
        }
        foreach (var fluss in sz.Fluesse)
        {
            Linie(dc, fluss.Select(P), new Pen(Wasser, Math.Max(3, 1.6 * s), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
        }
        foreach (var g in sz.Gleise)
        {
            Linie(dc, g.Select(P), new Pen(Gleis, Math.Max(2, 0.5 * s), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
        }
        var gehweg = new Pen(Gehweg, Math.Max(4, 1.8 * s), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var fahrbahn = new Pen(Fahrbahn, Math.Max(2, 1.0 * s), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        foreach (var st in sz.Strassen)
        {
            Linie(dc, st.Select(P), gehweg);
        }
        foreach (var st in sz.Strassen)
        {
            Linie(dc, st.Select(P), fahrbahn);
        }
        var rand = new Pen(HausRand, 1);
        var orteIds = karte.Orte.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var h in sz.Haeuser.Where(h => !orteIds.Contains(h.Id)))
        {
            dc.DrawGeometry(Haus, rand, Flaeche(h.Grundflaeche.Select(P)));
        }
        foreach (var o in karte.Orte)
        {
            dc.DrawRectangle(OrtHaus, rand, new Rect(P(o.Min), P(o.Max)));
        }
        var f = Faktor;
        // Welle A 0.83.0 (Weisung P12-282): Stadtteile mit Namen als gestrichelter Rahmen, Name unten im Rahmen
        var teilRand = new Pen(Bereich, 2, new DashStyle([4, 3], 0));
        foreach (var b in sz.StadtteilNamen)
        {
            dc.DrawRectangle(BereichFlaeche, teilRand, new Rect(P(new((float)b.X1, (float)b.Z1)), P(new((float)b.X2, (float)b.Z2))));
        }
        foreach (var b in sz.StadtteilNamen)
        {
            var text = Text(b.Name, Bereich, 11 * f, true);
            // unten in der Mitte des Rahmens: oben stehen in einer Stadt meist die Marken der Orte (Bild P12-168)
            var unten = P(new((float)((b.X1 + b.X2) / 2), (float)b.Z2));
            var kasten = new Rect(unten.X - (text.Width / 2) - (5 * f), unten.Y - text.Height - (5 * f), text.Width + (10 * f), text.Height + (2 * f));
            dc.DrawRectangle(BereichSchild, null, kasten, 5 * f, 5 * f);
            dc.DrawText(text, new Point(kasten.X + (5 * f), kasten.Y + f));
        }
        foreach (var h in sz.Haltestellen)
        {
            var m = P(h.Mitte);
            var r = 8 * f;
            dc.DrawEllipse(HaltGruen, new Pen(Weiss, 1.5), m, r, r);
            Mitte(dc, Text("H", Weiss, 10 * f, true), m);
        }
        var marken = karte.Marken(s, f);
        for (var i = 0; i < karte.Orte.Count; i++)
        {
            var o = karte.Orte[i];
            var m = Punkt(marken[i]);
            var r = StadtKarte.MarkeRadius * f;
            dc.DrawEllipse(Marke, new Pen(Weiss, 1.5), m, r, r);
            Mitte(dc, Text(o.Nr.ToString(CultureInfo.InvariantCulture), Weiss, 11 * f, true), m);
            if (o.Aufgabe)
            {
                var a = new Point(m.X + r, m.Y - r);
                dc.DrawEllipse(Lila, new Pen(Weiss, 1.5), a, 7 * f, 7 * f);
                Mitte(dc, Text("!", Weiss, 10 * f, true), a);
            }
        }
        var du = P(_figur);
        dc.DrawEllipse(null, new Pen(Cyan, 4), du, 12 * f, 12 * f);
        dc.DrawEllipse(Marke, null, du, 4 * f, 4 * f);
    }

    private static void Linie(DrawingContext dc, IEnumerable<Point> punkte, Pen stift)
    {
        var p = punkte.ToList();
        if (p.Count < 2)
        {
            return;
        }
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(p[0], false);
            foreach (var q in p.Skip(1))
            {
                c.LineTo(q);
            }
            c.EndFigure(false);
        }
        dc.DrawGeometry(null, stift, g);
    }

    private static StreamGeometry Flaeche(IEnumerable<Point> punkte)
    {
        var p = punkte.ToList();
        var g = new StreamGeometry();
        if (p.Count < 3)
        {
            return g;
        }
        using var c = g.Open();
        c.BeginFigure(p[0], true);
        foreach (var q in p.Skip(1))
        {
            c.LineTo(q);
        }
        c.EndFigure(true);
        return g;
    }

    private FormattedText Text(string text, IBrush farbe, double groesse, bool fett) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Avalonia.Controls.Documents.TextElement.GetFontFamily(this), FontStyle.Normal, fett ? FontWeight.Bold : FontWeight.Normal),
            groesse, farbe);

    /// <summary>Text mittig um <paramref name="m"/> mit seiner echten Breite (Lehre A 0.77.0: nie MaxTextWidth ohne Breite).</summary>
    private static void Mitte(DrawingContext dc, FormattedText text, Point m) =>
        dc.DrawText(text, new Point(m.X - (text.Width / 2), m.Y - (text.Height / 2)));
}
