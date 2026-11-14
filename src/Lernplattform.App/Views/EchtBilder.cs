using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 0.77.0, Welle 2 "Prüfung im echten Format" (Weisung P12-265, Plan P12-152 Abschnitt 3): gemeinsamer Teil der
/// gezeichneten Bilder (Netzplan, Monitor-Bild, UML-Aktivitaet, ER in Chen). Immer aus Daten gezeichnet, Farben ueber
/// "Pinsel.*" (hell und dunkel), Schrift waechst mit der Schriftstufe ("Schrift.Klein"). Die Breite kommt vom
/// <see cref="Bildkasten"/> (<see cref="Ziel"/>); reicht sie nicht fuer die kleinste lesbare Groesse, wird das Bild
/// breiter und seitlich wischbar.
/// </summary>
public abstract class EchtBild : Control
{
    public static readonly StyledProperty<IBrush?> LinieProperty = AvaloniaProperty.Register<EchtBild, IBrush?>(nameof(Linie));
    public static readonly StyledProperty<IBrush?> LeiseProperty = AvaloniaProperty.Register<EchtBild, IBrush?>(nameof(Leise));
    public static readonly StyledProperty<IBrush?> FlaecheProperty = AvaloniaProperty.Register<EchtBild, IBrush?>(nameof(Flaeche));
    public static readonly StyledProperty<IBrush?> AkzentProperty = AvaloniaProperty.Register<EchtBild, IBrush?>(nameof(Akzent));
    public static readonly StyledProperty<IBrush?> RasterProperty = AvaloniaProperty.Register<EchtBild, IBrush?>(nameof(Raster));
    public static readonly StyledProperty<IBrush?> GrundProperty = AvaloniaProperty.Register<EchtBild, IBrush?>(nameof(Grund));
    public static readonly StyledProperty<double> GroesseProperty = AvaloniaProperty.Register<EchtBild, double>(nameof(Groesse), 12);

    static EchtBild()
    {
        AffectsRender<EchtBild>(LinieProperty, LeiseProperty, FlaecheProperty, AkzentProperty, RasterProperty, GrundProperty);
        AffectsMeasure<EchtBild>(GroesseProperty);
    }

    protected EchtBild()
    {
        this.Farbe(LinieProperty, "Pinsel.text").Farbe(LeiseProperty, "Pinsel.muted").Farbe(FlaecheProperty, "Pinsel.card_alt")
            .Farbe(AkzentProperty, "Pinsel.accent").Farbe(RasterProperty, "Pinsel.border").Farbe(GrundProperty, "Pinsel.card");
        Bind(GroesseProperty, this.GetResourceObservable("Schrift.Klein", v => v is double d ? d : 12));
    }

    public IBrush? Linie { get => GetValue(LinieProperty); set => SetValue(LinieProperty, value); }
    public IBrush? Leise { get => GetValue(LeiseProperty); set => SetValue(LeiseProperty, value); }
    public IBrush? Flaeche { get => GetValue(FlaecheProperty); set => SetValue(FlaecheProperty, value); }
    public IBrush? Akzent { get => GetValue(AkzentProperty); set => SetValue(AkzentProperty, value); }
    public IBrush? Raster { get => GetValue(RasterProperty); set => SetValue(RasterProperty, value); }
    public IBrush? Grund { get => GetValue(GrundProperty); set => SetValue(GrundProperty, value); }
    public double Groesse { get => GetValue(GroesseProperty); set => SetValue(GroesseProperty, value); }

    /// <summary>Verfuegbare Breite (vom Bildkasten); unendlich = bequeme Breite.</summary>
    public double Ziel { get; set; } = double.PositiveInfinity;

    /// <summary>Breite, unter der das Bild nicht mehr lesbar waere (darunter wird gewischt).</summary>
    public double Mindestbreite { get; private set; }

    /// <summary>Schriftgroesse als Mass fuer alles (Abstaende, Symbole).</summary>
    protected double F => Groesse;

    /// <summary>Plant das Bild fuer eine Breite; gibt die Groesse zurueck und merkt sich alles fuer <see cref="Render"/>.</summary>
    protected abstract Size Planen(double breite);

    /// <summary>
    /// Stelle (x), die beim Wischen zuerst in der Mitte stehen soll; null = links anfangen. Der Netzplan beginnt oben
    /// beim ersten Geraet (sonst saehe man am Handy mit grosser Schrift zuerst nur leere Flaeche).
    /// </summary>
    public virtual double? Anfang => null;

    /// <summary>Mindestbreite und bequeme Breite des Bilds bei der aktuellen Schrift.</summary>
    protected abstract (double Mindest, double Bequem) Breiten();

    protected override Size MeasureOverride(Size verfuegbar)
    {
        var (mindest, bequem) = Breiten();
        Mindestbreite = mindest;
        var breite = double.IsInfinity(Ziel) ? bequem : Math.Max(mindest, Math.Min(Ziel, bequem));
        return Planen(breite);
    }

    protected override Size ArrangeOverride(Size endgroesse)
    {
        Planen(endgroesse.Width);
        return endgroesse;
    }

    protected FormattedText Text(string text, IBrush? farbe, double faktor = 1, bool fett = false, double breite = 0)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Avalonia.Controls.Documents.TextElement.GetFontFamily(this), FontStyle.Normal, fett ? FontWeight.Bold : FontWeight.Normal),
            F * faktor, farbe);
        if (breite > 0)
        {
            ft.MaxTextWidth = breite;
            ft.TextAlignment = TextAlignment.Center;
            ft.Trimming = TextTrimming.None;    // nie "..." statt Text: die Zelle ist breit genug fuer das laengste Wort
        }
        return ft;
    }

    /// <summary>Breite des laengsten Worts von <paramref name="text"/> (eine Zelle muss mindestens so breit sein).</summary>
    protected double WortBreite(string text, double faktor = 1, bool fett = false) =>
        text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(w => Text(w, Linie, faktor, fett).Width).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Text mittig um (x, y) zeichnen. Ohne feste Breite ist MaxTextWidth riesig (nicht 0): dann zaehlt die echte Breite,
    /// sonst laege der Text weit links ausserhalb und Avalonia liesse das ganze Bild weg (Fund A 0.77.0, Netzplan leer).
    /// </summary>
    protected static void Mitte(DrawingContext dc, FormattedText text, double x, double y)
    {
        var breite = text.TextAlignment == TextAlignment.Center ? text.MaxTextWidth : text.Width;
        dc.DrawText(text, new Point(x - breite / 2, y - text.Height / 2));
    }

    protected Pen Stift(double dicke = 1.4, IBrush? farbe = null) => new(farbe ?? Linie, Math.Max(1, dicke * F / 12));

    /// <summary>Pfeilspitze an <paramref name="spitze"/>, aus Richtung <paramref name="von"/>.</summary>
    protected void Pfeil(DrawingContext dc, Point von, Point spitze)
    {
        var dx = spitze.X - von.X;
        var dy = spitze.Y - von.Y;
        var laenge = Math.Sqrt(dx * dx + dy * dy);
        if (laenge < 0.01)
        {
            return;
        }
        var (ux, uy) = (dx / laenge, dy / laenge);
        var l = 0.7 * F;
        var b = 0.35 * F;
        var basis = new Point(spitze.X - ux * l, spitze.Y - uy * l);
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(spitze, true);
            c.LineTo(new Point(basis.X - uy * b, basis.Y + ux * b));
            c.LineTo(new Point(basis.X + uy * b, basis.Y - ux * b));
            c.EndFigure(true);
        }
        dc.DrawGeometry(Linie, null, g);
    }

    protected static StreamGeometry Raute(Point mitte, double breite, double hoehe)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(new Point(mitte.X, mitte.Y - hoehe / 2), true);
        c.LineTo(new Point(mitte.X + breite / 2, mitte.Y));
        c.LineTo(new Point(mitte.X, mitte.Y + hoehe / 2));
        c.LineTo(new Point(mitte.X - breite / 2, mitte.Y));
        c.EndFigure(true);
        return g;
    }
}

/// <summary>
/// Netzplan: Geraete mit Symbol, Name und Angaben im Raster, Leitungen als Linien; beschriftete Leitungen tragen eine
/// Nummer, der Text steht in der Legende unter dem Bild (nie uebereinander, bei jeder Schrift lesbar).
/// </summary>
public sealed class NetzplanBild : EchtBild
{
    private readonly EchtNetzplan _plan;
    private readonly int _spalten;
    private readonly int _zeilen;
    private double _zelle;
    private double _links;
    private readonly List<double> _zeilenOben = [];
    private readonly Dictionary<string, (Point Symbol, double Unten)> _orte = new(StringComparer.Ordinal);

    public NetzplanBild(EchtNetzplan plan)
    {
        _plan = plan;
        _spalten = plan.Knoten.Max(k => k.X) + 1;
        _zeilen = plan.Knoten.Max(k => k.Y) + 1;
    }

    private double Symbol => 2.6 * F;

    public override double? Anfang => _orte.Count == 0 ? null : _orte.Values.MinBy(o => (o.Symbol.Y, o.Symbol.X)).Symbol.X;

    private double Luecke => 4.2 * F;

    /// <summary>Kleinste Zelle: 7,5 Schriftgroessen, aber nie schmaler als das laengste Wort eines Namens oder einer Angabe.</summary>
    private double ZelleMin => Math.Max(7.5 * F, _plan.Knoten.Max(k => Math.Max(WortBreite(k.Name, 1, fett: true),
        k.Info.Select(i => WortBreite(i, 0.9)).DefaultIfEmpty(0).Max())) + 0.8 * F);

    protected override (double Mindest, double Bequem) Breiten() => (_spalten * ZelleMin, _spalten * Math.Max(12 * F, ZelleMin));

    private (FormattedText Name, FormattedText? Info) Texte(EchtKnoten k)
    {
        var breite = _zelle - 0.6 * F;
        var name = Text(k.Name, Linie, 1, fett: true, breite: breite);
        var info = k.Info.Count > 0 ? Text(string.Join("\n", k.Info), Leise, 0.9, breite: breite) : null;
        return (name, info);
    }

    protected override Size Planen(double breite)
    {
        var min = ZelleMin;
        _zelle = Math.Clamp(breite / _spalten, min, Math.Max(12 * F, min));
        _links = Math.Max(0, (breite - _zelle * _spalten) / 2);
        _zeilenOben.Clear();
        _orte.Clear();
        var hoehen = new double[_zeilen];
        foreach (var k in _plan.Knoten)
        {
            var (name, info) = Texte(k);
            hoehen[k.Y] = Math.Max(hoehen[k.Y], Symbol + 0.3 * F + name.Height + (info?.Height ?? 0));
        }
        var y = 0.4 * F;
        for (var r = 0; r < _zeilen; r++)
        {
            _zeilenOben.Add(y);
            y += Math.Max(hoehen[r], Symbol) + Luecke;
        }
        foreach (var k in _plan.Knoten)
        {
            var (name, info) = Texte(k);
            var mitte = new Point(_links + (k.X + 0.5) * _zelle, _zeilenOben[k.Y] + Symbol / 2);
            _orte[k.Id] = (mitte, _zeilenOben[k.Y] + Symbol + 0.3 * F + name.Height + (info?.Height ?? 0));
        }
        return new Size(Math.Max(breite, _zelle * _spalten), y - Luecke + 0.4 * F);
    }

    public override void Render(DrawingContext dc)
    {
        // Leitungen zuerst (Symbole liegen darueber); Anker: unten am ganzen Geraet, oben am Symbol
        var nummer = 0;
        foreach (var kante in _plan.Kanten)
        {
            var a = _orte[kante.Von];
            var b = _orte[kante.Nach];
            var (oben, unten) = a.Symbol.Y <= b.Symbol.Y ? (a, b) : (b, a);
            Point p1, p2;
            Point m;
            if (Math.Abs(a.Symbol.Y - b.Symbol.Y) < 0.5)
            {
                var (l, r) = a.Symbol.X < b.Symbol.X ? (a, b) : (b, a);
                p1 = new Point(l.Symbol.X + Symbol / 2, l.Symbol.Y);
                p2 = new Point(r.Symbol.X - Symbol / 2, r.Symbol.Y);
                dc.DrawLine(Stift(1.6), p1, p2);
                m = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            }
            else
            {
                // rechtwinklig wie in einem gezeichneten Netzplan: senkrecht herunter, waagerecht auf halber Hoehe,
                // senkrecht zum Geraet; die Nummer sitzt auf dem letzten Stueck (gehoert eindeutig zu diesem Geraet)
                p1 = new Point(oben.Symbol.X, oben.Unten + 0.15 * F);
                p2 = new Point(unten.Symbol.X, unten.Symbol.Y - Symbol / 2 - 0.1 * F);
                var knick = p2.Y - (Luecke - 0.3 * F) * 0.62;
                var linie = new StreamGeometry();
                using (var c = linie.Open())
                {
                    c.BeginFigure(p1, false);
                    if (Math.Abs(p1.X - p2.X) > 0.5)
                    {
                        c.LineTo(new Point(p1.X, knick));
                        c.LineTo(new Point(p2.X, knick));
                    }
                    c.LineTo(p2);
                    c.EndFigure(false);
                }
                dc.DrawGeometry(null, Stift(1.6), linie);
                m = Math.Abs(p1.X - p2.X) > 0.5 ? new Point(p2.X, (knick + p2.Y) / 2) : new Point(p1.X, (p1.Y + p2.Y) / 2);
            }
            if (kante.Text.Length > 0)
            {
                nummer++;
                var r = 0.75 * F;
                dc.DrawEllipse(Grund, Stift(1.2, Akzent), m, r, r);
                Mitte(dc, Text(nummer.ToString(CultureInfo.InvariantCulture), Akzent, 0.85, fett: true), m.X, m.Y);
            }
        }
        foreach (var k in _plan.Knoten)
        {
            var mitte = _orte[k.Id].Symbol;
            Geraet(dc, k.Typ, mitte, Symbol);
            var (name, info) = Texte(k);
            var y = mitte.Y + Symbol / 2 + 0.3 * F;
            dc.DrawText(name, new Point(mitte.X - name.MaxTextWidth / 2, y));
            if (info is not null)
            {
                dc.DrawText(info, new Point(mitte.X - info.MaxTextWidth / 2, y + name.Height));
            }
        }
    }

    /// <summary>Einfaches Symbol je Geraet in einem Quadrat der Seite <paramref name="s"/>.</summary>
    private void Geraet(DrawingContext dc, string typ, Point m, double s)
    {
        var stift = Stift(1.4);
        var fein = Stift(1.1);
        Rect Kasten(double b, double h, double dy = 0) => new(m.X - b * s / 2, m.Y - h * s / 2 + dy * s, b * s, h * s);
        switch (typ)
        {
            case "internet":
                var wolke = new StreamGeometry();
                using (var c = wolke.Open())
                {
                    c.BeginFigure(new Point(m.X - 0.42 * s, m.Y + 0.22 * s), true);
                    c.ArcTo(new Point(m.X - 0.22 * s, m.Y - 0.08 * s), new Size(0.17 * s, 0.17 * s), 0, false, SweepDirection.Clockwise);
                    c.ArcTo(new Point(m.X + 0.18 * s, m.Y - 0.14 * s), new Size(0.22 * s, 0.22 * s), 0, false, SweepDirection.Clockwise);
                    c.ArcTo(new Point(m.X + 0.42 * s, m.Y + 0.22 * s), new Size(0.2 * s, 0.2 * s), 0, false, SweepDirection.Clockwise);
                    c.EndFigure(true);
                }
                dc.DrawGeometry(Flaeche, stift, wolke);
                break;
            case "router":
                dc.DrawEllipse(Flaeche, stift, m, 0.42 * s, 0.3 * s);
                foreach (var (dx, dy) in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0) })
                {
                    var von = new Point(m.X + dx * 0.08 * s, m.Y + dy * 0.05 * s);
                    var bis = new Point(m.X + dx * 0.3 * s, m.Y + dy * 0.2 * s);
                    dc.DrawLine(fein, von, bis);
                    Pfeil(dc, von, bis);
                }
                break;
            case "firewall":
                var wand = Kasten(0.8, 0.6);
                dc.DrawRectangle(Flaeche, stift, wand);
                for (var i = 1; i < 3; i++)
                {
                    var y = wand.Top + wand.Height * i / 3;
                    dc.DrawLine(fein, new Point(wand.Left, y), new Point(wand.Right, y));
                }
                for (var i = 0; i < 3; i++)
                {
                    var versatz = i % 2 == 0 ? 0.5 : 0.25;
                    var y0 = wand.Top + wand.Height * i / 3;
                    for (var x = wand.Left + wand.Width * versatz; x < wand.Right - 1; x += wand.Width / 2)
                    {
                        dc.DrawLine(fein, new Point(x, y0), new Point(x, y0 + wand.Height / 3));
                    }
                }
                break;
            case "switch":
                var sw = Kasten(0.9, 0.4);
                dc.DrawRectangle(Flaeche, stift, sw, 0.06 * s, 0.06 * s);
                var y1 = sw.Top + sw.Height * 0.33;
                var y2 = sw.Top + sw.Height * 0.67;
                dc.DrawLine(fein, new Point(sw.Left + 0.15 * s, y1), new Point(sw.Right - 0.2 * s, y1));
                Pfeil(dc, new Point(sw.Left + 0.15 * s, y1), new Point(sw.Right - 0.15 * s, y1));
                dc.DrawLine(fein, new Point(sw.Right - 0.15 * s, y2), new Point(sw.Left + 0.2 * s, y2));
                Pfeil(dc, new Point(sw.Right - 0.15 * s, y2), new Point(sw.Left + 0.15 * s, y2));
                break;
            case "server":
            case "nas":
                var gehaeuse = typ == "server" ? Kasten(0.5, 0.85) : Kasten(0.75, 0.55);
                dc.DrawRectangle(Flaeche, stift, gehaeuse, 0.04 * s, 0.04 * s);
                var faecher = typ == "server" ? 3 : 2;
                for (var i = 0; i < faecher; i++)
                {
                    var hoehe = gehaeuse.Height / (faecher + 0.6);
                    var fach = new Rect(gehaeuse.Left + 0.08 * s, gehaeuse.Top + 0.3 * hoehe + i * hoehe, gehaeuse.Width - 0.16 * s, hoehe * 0.65);
                    dc.DrawRectangle(null, fein, fach);
                    dc.DrawEllipse(Akzent, null, new Point(fach.Right - 0.07 * s, fach.Center.Y), 0.03 * s, 0.03 * s);
                }
                break;
            case "pc":
                var bild = Kasten(0.8, 0.55, -0.1);
                dc.DrawRectangle(Flaeche, stift, bild, 0.04 * s, 0.04 * s);
                dc.DrawLine(stift, new Point(m.X, bild.Bottom), new Point(m.X, bild.Bottom + 0.14 * s));
                dc.DrawLine(stift, new Point(m.X - 0.2 * s, bild.Bottom + 0.14 * s), new Point(m.X + 0.2 * s, bild.Bottom + 0.14 * s));
                break;
            case "notebook":
                var schirm = Kasten(0.62, 0.42, -0.08);
                dc.DrawRectangle(Flaeche, stift, schirm, 0.03 * s, 0.03 * s);
                var unterteil = new StreamGeometry();
                using (var c = unterteil.Open())
                {
                    c.BeginFigure(new Point(schirm.Left, schirm.Bottom + 0.02 * s), true);
                    c.LineTo(new Point(schirm.Right, schirm.Bottom + 0.02 * s));
                    c.LineTo(new Point(schirm.Right + 0.1 * s, schirm.Bottom + 0.14 * s));
                    c.LineTo(new Point(schirm.Left - 0.1 * s, schirm.Bottom + 0.14 * s));
                    c.EndFigure(true);
                }
                dc.DrawGeometry(Flaeche, stift, unterteil);
                break;
            case "drucker":
                var papier = Kasten(0.48, 0.3, -0.25);
                dc.DrawRectangle(Grund, fein, papier);
                var koerper = Kasten(0.85, 0.42, 0.05);
                dc.DrawRectangle(Flaeche, stift, koerper, 0.05 * s, 0.05 * s);
                dc.DrawLine(fein, new Point(koerper.Left + 0.18 * s, koerper.Bottom - 0.1 * s), new Point(koerper.Right - 0.18 * s, koerper.Bottom - 0.1 * s));
                break;
            default:    // wlan
                var fuss = Kasten(0.6, 0.22, 0.25);
                dc.DrawRectangle(Flaeche, stift, fuss, 0.04 * s, 0.04 * s);
                var antenne = new Point(m.X, fuss.Top);
                dc.DrawLine(stift, antenne, new Point(m.X, m.Y - 0.05 * s));
                foreach (var r in new[] { 0.18, 0.32 })
                {
                    var bogen = new StreamGeometry();
                    using (var c = bogen.Open())
                    {
                        var mitte = new Point(m.X, m.Y - 0.05 * s);
                        c.BeginFigure(new Point(mitte.X - r * s * 0.7, mitte.Y - r * s * 0.7), false);
                        c.ArcTo(new Point(mitte.X + r * s * 0.7, mitte.Y - r * s * 0.7), new Size(r * s, r * s), 0, false, SweepDirection.Clockwise);
                        c.EndFigure(false);
                    }
                    dc.DrawGeometry(null, fein, bogen);
                }
                break;
        }
    }
}

/// <summary>Monitor-Bild: je Messreihe ein Verlauf mit Raster (0, 25, 50, 75, 100 % des Hoechstwerts) und Zeitmarken.</summary>
public sealed class MonitorBild(EchtMonitor monitor) : EchtBild
{
    private double _breite;

    private double Diagramm => 6.5 * F;

    protected override (double Mindest, double Bequem) Breiten() => (16 * F, 46 * F);

    private static string Wert(double w) => w.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));

    private double Achse(EchtReihe r) => Enumerable.Range(0, 5).Max(i => Text(Wert(r.Max * i / 4), Leise, 0.85).Width) + 0.6 * F;

    private double ReiheHoehe => Text("Ag", Linie).Height * 1.3 + Diagramm + Text("Ag", Leise, 0.85).Height + 1.4 * F;

    protected override Size Planen(double breite)
    {
        _breite = breite;
        return new Size(breite, monitor.Reihen.Count * ReiheHoehe + Text("Ag", Leise, 0.85).Height + 0.4 * F);
    }

    public override void Render(DrawingContext dc)
    {
        var y = 0.2 * F;
        foreach (var r in monitor.Reihen)
        {
            var titel = Text(r.Name + (r.Einheit.Length > 0 ? " (" + r.Einheit + ")" : ""), Linie, 1, fett: true);
            dc.DrawText(titel, new Point(0, y));
            var oben = y + titel.Height * 1.3;
            var links = Achse(r);
            var rechts = _breite - 0.8 * F;
            for (var i = 0; i <= 4; i++)
            {
                var yy = oben + Diagramm * (1 - i / 4.0);
                dc.DrawLine(new Pen(Raster, 1, i == 0 ? null : new DashStyle([2, 4], 0)), new Point(links, yy), new Point(rechts, yy));
                var beschriftung = Text(Wert(r.Max * i / 4), Leise, 0.85);
                dc.DrawText(beschriftung, new Point(links - 0.4 * F - beschriftung.Width, yy - beschriftung.Height / 2));
            }
            var schritt = (rechts - links) / Math.Max(1, monitor.Marken.Count - 1);
            var punkte = r.Werte.Select((w, i) => new Point(links + i * schritt, oben + Diagramm * (1 - w / r.Max))).ToList();
            var flaeche = new StreamGeometry();
            using (var c = flaeche.Open())
            {
                c.BeginFigure(new Point(punkte[0].X, oben + Diagramm), true);
                foreach (var p in punkte)
                {
                    c.LineTo(p);
                }
                c.LineTo(new Point(punkte[^1].X, oben + Diagramm));
                c.EndFigure(true);
            }
            dc.DrawGeometry(Flaeche, null, flaeche);
            for (var i = 1; i < punkte.Count; i++)
            {
                dc.DrawLine(Stift(2, Akzent), punkte[i - 1], punkte[i]);
            }
            foreach (var p in punkte)
            {
                dc.DrawEllipse(Akzent, null, p, 0.18 * F, 0.18 * F);
            }
            // Zeitmarken: so viele, dass sie sich nicht beruehren (erste und letzte immer)
            var breiteste = monitor.Marken.Max(m => Text(m, Leise, 0.85).Width) + 0.8 * F;
            var jede = Math.Max(1, (int)Math.Ceiling(breiteste / schritt));
            var zuletzt = int.MinValue / 2;
            for (var i = 0; i < monitor.Marken.Count; i++)
            {
                var zeigen = i % jede == 0 || (i == monitor.Marken.Count - 1 && (i - zuletzt) * schritt >= breiteste);
                if (!zeigen)
                {
                    continue;
                }
                zuletzt = i;
                var marke = Text(monitor.Marken[i], Leise, 0.85);
                var x = Math.Clamp(links + i * schritt - marke.Width / 2, 0, _breite - marke.Width);
                dc.DrawText(marke, new Point(x, oben + Diagramm + 0.2 * F));
            }
            y += ReiheHoehe;
        }
        var achse = Text(monitor.Achse, Leise, 0.85);
        dc.DrawText(achse, new Point(_breite - achse.Width - 0.8 * F, y - 1.2 * F));
    }
}

/// <summary>
/// Diagramm einer Zeichenaufgabe: UML-Aktivitaet (Start, Aktion, Verzweigung, Zusammenfuehrung, Ende, Pfeile mit
/// Bedingung) oder ER-Modell in Chen (Entitaet als Rechteck, Beziehung als Raute, Attribut als Ellipse, Schluessel
/// unterstrichen, Kardinalitaet an der Entitaet). Bausteine stehen im Raster aus den Daten.
/// </summary>
public sealed class DiagrammBild : EchtBild
{
    private readonly EchtDiagramm _d;
    private readonly int _spalten;
    private readonly int _zeilen;
    private double _zelle;
    private double _links;
    private readonly Dictionary<string, (Point Mitte, Size Form, FormattedText? Text)> _orte = new(StringComparer.Ordinal);

    public DiagrammBild(EchtDiagramm diagramm)
    {
        _d = diagramm;
        _spalten = diagramm.Elemente.Max(e => e.X) + 1;
        _zeilen = diagramm.Elemente.Max(e => e.Y) + 1;
    }

    private double Luecke => 2.4 * F;

    /// <summary>UML beginnt beim Startknoten (der Ablauf laeuft von oben nach unten), ER links.</summary>
    public override double? Anfang => _d.Art == EchtZeichnung.Uml && _d.Elemente.FirstOrDefault(e => e.Typ == "start") is { } s
        && _orte.TryGetValue(s.Id, out var o) ? o.Mitte.X : null;

    /// <summary>Kleinste Zelle: 7,5 Schriftgroessen, aber jedes Wort passt in seine Form (Rechnung wie in <see cref="Form"/>).</summary>
    private double ZelleMin => Math.Max(7.5 * F, _d.Elemente.Max(e => e.Typ switch
    {
        "beziehung" => (WortBreite(e.Text, 1, fett: true) + 0.2 * F) / (0.92 * 0.55),
        "attribut" => (WortBreite(e.Text, 0.95) + 0.2 * F) / (0.9 * 0.74),
        "aktion" => (WortBreite(e.Text) + 1.2 * F) / 0.86,
        "entitaet" => (WortBreite(e.Text, 1, fett: true) + 1.2 * F) / 0.8,
        _ => 0,
    }));

    protected override (double Mindest, double Bequem) Breiten() => (_spalten * ZelleMin, _spalten * Math.Max(11.5 * F, ZelleMin));

    private (Size Form, FormattedText? Text) Form(EchtElement e)
    {
        switch (e.Typ)
        {
            case "start":
                return (new Size(1.4 * F, 1.4 * F), null);
            case "ende":
                return (new Size(1.6 * F, 1.6 * F), null);
            case "verzweigung":
            case "zusammenfuehrung":
                return (new Size(1.9 * F, 1.9 * F), null);
            case "beziehung":
                var rb = 0.92 * _zelle;
                var rt = Text(e.Text, Linie, 1, fett: true, breite: rb * 0.55);
                return (new Size(rb, Math.Max(2.8 * F, rt.Height * 2 + 0.8 * F)), rt);
            case "attribut":
                var ab = 0.9 * _zelle;
                var at = Text(e.Text, Linie, 0.95, breite: ab * 0.74);
                return (new Size(ab, Math.Max(2.2 * F, at.Height + 1.0 * F)), at);
            default:    // aktion, entitaet
                var b = (e.Typ == "aktion" ? 0.86 : 0.8) * _zelle;
                var t = Text(e.Text, Linie, 1, fett: e.Typ == "entitaet", breite: b - 1.0 * F);
                return (new Size(b, Math.Max(2.4 * F, t.Height + 1.0 * F)), t);
        }
    }

    protected override Size Planen(double breite)
    {
        var min = ZelleMin;
        _zelle = Math.Clamp(breite / _spalten, min, Math.Max(11.5 * F, min));
        _links = Math.Max(0, (breite - _zelle * _spalten) / 2);
        _orte.Clear();
        var hoehen = new double[_zeilen];
        var formen = _d.Elemente.ToDictionary(e => e.Id, Form, StringComparer.Ordinal);
        foreach (var e in _d.Elemente)
        {
            hoehen[e.Y] = Math.Max(hoehen[e.Y], formen[e.Id].Form.Height);
        }
        var oben = new double[_zeilen];
        var y = 0.5 * F;
        for (var r = 0; r < _zeilen; r++)
        {
            oben[r] = y;
            y += Math.Max(hoehen[r], 1.4 * F) + Luecke;
        }
        foreach (var e in _d.Elemente)
        {
            var mitte = new Point(_links + (e.X + 0.5) * _zelle, oben[e.Y] + Math.Max(hoehen[e.Y], 1.4 * F) / 2);
            _orte[e.Id] = (mitte, formen[e.Id].Form, formen[e.Id].Text);
        }
        return new Size(Math.Max(breite, _zelle * _spalten), y - Luecke + 0.5 * F);
    }

    /// <summary>Rand einer Form in Richtung (dx, dy) ab der Mitte (Rechteck, Raute, Ellipse, Kreis).</summary>
    private Point Rand(EchtElement e, double dx, double dy)
    {
        var (m, s, _) = _orte[e.Id];
        var laenge = Math.Sqrt(dx * dx + dy * dy);
        if (laenge < 1e-9)
        {
            return m;
        }
        var (ux, uy) = (dx / laenge, dy / laenge);
        var a = s.Width / 2;
        var b = s.Height / 2;
        double t = e.Typ switch
        {
            "beziehung" or "verzweigung" or "zusammenfuehrung" => 1 / (Math.Abs(ux) / a + Math.Abs(uy) / b),
            "attribut" or "start" or "ende" => 1 / Math.Sqrt(ux * ux / (a * a) + uy * uy / (b * b)),
            _ => Math.Min(Math.Abs(ux) < 1e-9 ? double.MaxValue : a / Math.Abs(ux), Math.Abs(uy) < 1e-9 ? double.MaxValue : b / Math.Abs(uy)),
        };
        return new Point(m.X + ux * t, m.Y + uy * t);
    }

    public override void Render(DrawingContext dc)
    {
        foreach (var v in _d.Verbindungen)
        {
            if (_d.Art == EchtZeichnung.Er)
            {
                ErLinie(dc, v);
            }
            else
            {
                UmlPfeil(dc, v);
            }
        }
        foreach (var e in _d.Elemente)
        {
            var (m, s, text) = _orte[e.Id];
            var stift = Stift(1.5);
            switch (e.Typ)
            {
                case "start":
                    dc.DrawEllipse(Linie, null, m, s.Width / 2, s.Height / 2);
                    break;
                case "ende":
                    dc.DrawEllipse(Grund, stift, m, s.Width / 2, s.Height / 2);
                    dc.DrawEllipse(Linie, null, m, s.Width / 2 - 0.3 * F, s.Height / 2 - 0.3 * F);
                    break;
                case "verzweigung":
                case "zusammenfuehrung":
                case "beziehung":
                    dc.DrawGeometry(Flaeche, stift, Raute(m, s.Width, s.Height));
                    break;
                case "attribut":
                    dc.DrawEllipse(Flaeche, stift, m, s.Width / 2, s.Height / 2);
                    break;
                case "aktion":
                    dc.DrawRectangle(Flaeche, stift, new Rect(m.X - s.Width / 2, m.Y - s.Height / 2, s.Width, s.Height), 0.8 * F, 0.8 * F);
                    break;
                default:    // entitaet
                    dc.DrawRectangle(Flaeche, stift, new Rect(m.X - s.Width / 2, m.Y - s.Height / 2, s.Width, s.Height));
                    break;
            }
            if (text is not null)
            {
                Mitte(dc, text, m.X, m.Y);
                if (e.Schluessel)
                {
                    // Schluessel unterstrichen: unter jeder Zeile des Texts
                    var unten = m.Y + text.Height / 2 - 0.08 * F;
                    var halb = Math.Min(text.Width, text.MaxTextWidth) / 2;
                    dc.DrawLine(Stift(1.1), new Point(m.X - halb, unten), new Point(m.X + halb, unten));
                }
            }
        }
    }

    private void ErLinie(DrawingContext dc, EchtVerbindung v)
    {
        var a = _d.Element(v.Von);
        var b = _d.Element(v.Nach);
        var ma = _orte[a.Id].Mitte;
        var mb = _orte[b.Id].Mitte;
        var p1 = Rand(a, mb.X - ma.X, mb.Y - ma.Y);
        var p2 = Rand(b, ma.X - mb.X, ma.Y - mb.Y);
        dc.DrawLine(Stift(1.4), p1, p2);
        if (v.Kard.Length == 0)
        {
            return;
        }
        // Kardinalitaet an der Seite der Entitaet, leicht neben der Linie
        var (ent, anderer) = a.Typ == "entitaet" ? (p1, p2) : (p2, p1);
        var dx = anderer.X - ent.X;
        var dy = anderer.Y - ent.Y;
        var laenge = Math.Max(1e-9, Math.Sqrt(dx * dx + dy * dy));
        var (ux, uy) = (dx / laenge, dy / laenge);
        var abstand = Math.Min(1.1 * F, laenge / 2);
        var punkt = new Point(ent.X + ux * abstand - uy * 0.8 * F, ent.Y + uy * abstand + ux * 0.8 * F);
        Mitte(dc, Text(v.Kard, Akzent, 1.05, fett: true), punkt.X, punkt.Y);
    }

    private void UmlPfeil(DrawingContext dc, EchtVerbindung v)
    {
        var a = _d.Element(v.Von);
        var b = _d.Element(v.Nach);
        var ma = _orte[a.Id].Mitte;
        var mb = _orte[b.Id].Mitte;
        var waagrecht = Math.Abs(ma.Y - mb.Y) < 0.5;
        var senkrecht = Math.Abs(ma.X - mb.X) < 0.5;
        List<Point> weg;
        if (waagrecht || senkrecht)
        {
            weg = [Rand(a, mb.X - ma.X, mb.Y - ma.Y), Rand(b, ma.X - mb.X, ma.Y - mb.Y)];
        }
        else if (a.Typ == "verzweigung")
        {
            // aus der Raute zur Seite, dann senkrecht zum Ziel
            var start = Rand(a, mb.X - ma.X, 0);
            weg = [start, new Point(mb.X, ma.Y), Rand(b, 0, ma.Y - mb.Y)];
        }
        else
        {
            // senkrecht aus dem Baustein, dann waagrecht in das Ziel
            var start = Rand(a, 0, mb.Y - ma.Y);
            weg = [start, new Point(ma.X, mb.Y), Rand(b, ma.X - mb.X, 0)];
        }
        var stift = Stift(1.4);
        for (var i = 1; i < weg.Count; i++)
        {
            dc.DrawLine(stift, weg[i - 1], weg[i]);
        }
        Pfeil(dc, weg[^2], weg[^1]);
        if (v.Text.Length == 0)
        {
            return;
        }
        var bedingung = Text(v.Text, Leise, 0.9);
        var p = weg[0];
        var q = weg[1];
        Point ort;
        if (Math.Abs(q.Y - p.Y) < 0.5)
        {
            // waagrechter Anfang: ueber der Linie, vom Baustein weg
            var x = q.X > p.X ? p.X + 0.3 * F : p.X - 0.3 * F - bedingung.Width;
            ort = new Point(x, p.Y - bedingung.Height - 0.1 * F);
        }
        else
        {
            // senkrechter Anfang: rechts neben der Linie
            var y = q.Y > p.Y ? p.Y + 0.15 * F : p.Y - 0.15 * F - bedingung.Height;
            ort = new Point(p.X + 0.35 * F, y);
        }
        dc.DrawText(bedingung, ort);
    }
}

/// <summary>
/// Rahmen fuer ein gezeichnetes Bild: gibt ihm die verfuegbare Breite, macht es seitlich wischbar, wenn es schmaler
/// nicht lesbar waere ("→ wischen" darunter, Platz fuer die Leiste wie <see cref="Tabellenkasten"/>).
/// </summary>
public sealed class Bildkasten : StackPanel
{
    private readonly ScrollViewer _rollen;
    private readonly TextBlock _hinweis;

    public Bildkasten(EchtBild bild)
    {
        Bild = bild;
        Spacing = 2;
        _rollen = new ScrollViewer
        {
            Content = bild,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _hinweis = new TextBlock
        {
            Text = RechnerTexte.Wischen,
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
        }.Groesse("Klein");
        _hinweis.Farbe(TextBlock.ForegroundProperty, "Pinsel.muted");
        Children.Add(_rollen);
        Children.Add(_hinweis);
        _rollen.ScrollChanged += (_, _) => ZumAnfang();
    }

    private (double Bild, double Platz) _angefangenBei = (-1, -1);
    private bool _anfangOffen;

    protected override Size ArrangeOverride(Size endgroesse)
    {
        var groesse = base.ArrangeOverride(endgroesse);
        // neue Breite des Bilds oder des Platzes (erstes Zeigen, andere Schrift, anderes Fenster): einmal zur
        // Anfangsstelle des Bilds rollen, sobald der Bildlauf die neue Breite kennt
        var jetzt = (Bild.Bounds.Width, endgroesse.Width);
        if (Bild.Anfang is not null && (Math.Abs(jetzt.Item1 - _angefangenBei.Bild) > 0.5 || Math.Abs(jetzt.Item2 - _angefangenBei.Platz) > 0.5))
        {
            _angefangenBei = jetzt;
            _anfangOffen = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(ZumAnfang, Avalonia.Threading.DispatcherPriority.Background);
        }
        return groesse;
    }

    private void ZumAnfang()
    {
        if (!_anfangOffen || Bild.Anfang is not { } x || _rollen.Extent.Width < Bild.Bounds.Width - 0.5 || _rollen.Viewport.Width <= 0
            || Math.Abs(_rollen.Viewport.Width - _rollen.Bounds.Width) > 0.5)
        {
            return;
        }
        _anfangOffen = false;
        if (_rollen.Extent.Width > _rollen.Viewport.Width)
        {
            var ziel = Math.Clamp(x - (_rollen.Viewport.Width / 2), 0, _rollen.Extent.Width - _rollen.Viewport.Width);
            _rollen.Offset = new Vector(ziel, _rollen.Offset.Y);
        }
    }

    public EchtBild Bild { get; }

    public ScrollViewer Rollen => _rollen;

    public bool WischHinweis => _hinweis.IsVisible;

    protected override Size MeasureOverride(Size verfuegbar)
    {
        Bild.Ziel = verfuegbar.Width;
        Bild.InvalidateMeasure();
        Bild.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var wischen = !double.IsInfinity(verfuegbar.Width) && Bild.DesiredSize.Width > verfuegbar.Width + 0.5;
        if (_hinweis.IsVisible != wischen)
        {
            _hinweis.IsVisible = wischen;
            Bild.Margin = wischen ? new Thickness(0, 0, 0, Dialog.RandFuerLeiste) : default;
        }
        return base.MeasureOverride(verfuegbar);
    }
}

/// <summary>Bild mit Legende und Knopf "Als Text zeigen" (Textfassung fuer Screenreader und kleine Bildschirme).</summary>
public static class EchtBilder
{
    /// <summary>Netzplan, Monitor-Bild oder Diagramm samt Textfassung; null bei Anlagen ohne Bild.</summary>
    public static StackPanel? Anlage(EchtAnlage anlage, bool alsText = false)
    {
        if (anlage.Netzplan is { } plan)
        {
            var legende = plan.Beschriftet.Select((k, i) => (i + 1).ToString(CultureInfo.InvariantCulture) + "  "
                + plan.Geraet(k.Von).Name + " – " + plan.Geraet(k.Nach).Name + ": " + k.Text).ToList();
            return MitText(new NetzplanBild(plan), anlage.Titel, legende, EchtZeichnung.Textfassung(plan), anlage.Zeilen, alsText);
        }
        return anlage.Monitor is { } monitor
            ? MitText(new MonitorBild(monitor), anlage.Titel, [], EchtZeichnung.Textfassung(monitor), anlage.Zeilen, alsText)
            : null;
    }

    public static StackPanel Diagramm(EchtDiagramm diagramm, string titel, bool alsText = false) =>
        MitText(new DiagrammBild(diagramm), titel, [], EchtZeichnung.Textfassung(diagramm), [], alsText);

    private static StackPanel MitText(EchtBild bild, string titel, IReadOnlyList<string> legende, IReadOnlyList<string> textfassung,
        IReadOnlyList<string> zusatz, bool alsText)
    {
        Avalonia.Automation.AutomationProperties.SetName(bild, Beschriftungen.Einsetzen(EchtTexte.BildName, titel));
        var teil = new StackPanel { Spacing = 6 };
        teil.Children.Add(new Bildkasten(bild));
        if (legende.Count > 0)
        {
            teil.Children.Add(LernTeile.Label(EchtTexte.Leitungen));
            foreach (var zeile in legende)
            {
                teil.Children.Add(new LernText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
            }
        }
        foreach (var zeile in zusatz)
        {
            teil.Children.Add(new LernText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        }
        var text = new StackPanel { Spacing = 4, IsVisible = alsText };
        foreach (var zeile in textfassung)
        {
            text.Children.Add(new LernText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        }
        var knopf = LernTeile.Knopf(alsText ? EchtTexte.BildZeigen : EchtTexte.AlsText);
        Avalonia.Automation.AutomationProperties.SetName(knopf, alsText ? EchtTexte.BildZeigen : EchtTexte.AlsText);
        knopf.HorizontalAlignment = HorizontalAlignment.Left;
        knopf.Click += (_, _) =>
        {
            text.IsVisible = !text.IsVisible;
            var name = text.IsVisible ? EchtTexte.BildZeigen : EchtTexte.AlsText;
            ((TextBlock)knopf.Content!).Text = name;
            Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        };
        teil.Children.Add(knopf);
        teil.Children.Add(text);
        return teil;
    }
}
