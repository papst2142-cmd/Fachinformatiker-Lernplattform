using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Core.Fortschritt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L7: gemeinsame Teile der Diagramme (Farben ueber "Pinsel.*", Schrift waechst mit
/// der Schriftstufe). Achse, Beschriftungsabstand und Kurve rechnet Core.Fortschritt.Diagramm.
/// </summary>
public abstract class DiagrammFlaeche : Control
{
    public static readonly StyledProperty<IBrush?> RasterProperty = AvaloniaProperty.Register<DiagrammFlaeche, IBrush?>(nameof(Raster));
    public static readonly StyledProperty<IBrush?> LeiseProperty = AvaloniaProperty.Register<DiagrammFlaeche, IBrush?>(nameof(Leise));
    public static readonly StyledProperty<IBrush?> GedimmtProperty = AvaloniaProperty.Register<DiagrammFlaeche, IBrush?>(nameof(Gedimmt));
    public static readonly StyledProperty<IBrush?> HintergrundProperty = AvaloniaProperty.Register<DiagrammFlaeche, IBrush?>(nameof(Hintergrund));
    public static readonly StyledProperty<double> SchriftProperty = AvaloniaProperty.Register<DiagrammFlaeche, double>(nameof(Schrift), 11);

    static DiagrammFlaeche()
    {
        AffectsRender<DiagrammFlaeche>(RasterProperty, LeiseProperty, GedimmtProperty, HintergrundProperty, SchriftProperty);
    }

    protected DiagrammFlaeche(double hoehe)
    {
        this.Farbe(RasterProperty, "Pinsel.border").Farbe(LeiseProperty, "Pinsel.muted").Farbe(GedimmtProperty, "Pinsel.text_dim")
            .Farbe(HintergrundProperty, "Pinsel.card");
        // Python F["tiny"] (11 bei Normal), waechst mit der Schriftstufe
        Bind(SchriftProperty, this.GetResourceObservable("Schrift.Klein", v => v is double d ? d * 11 / 12 : 11));
        Bind(HeightProperty, this.GetResourceObservable("Schrift.Klein", v => v is double d ? hoehe * Math.Max(1, d / 12 * 0.75 + 0.25) : hoehe));
    }

    public IBrush? Raster { get => GetValue(RasterProperty); set => SetValue(RasterProperty, value); }
    public IBrush? Leise { get => GetValue(LeiseProperty); set => SetValue(LeiseProperty, value); }
    public IBrush? Gedimmt { get => GetValue(GedimmtProperty); set => SetValue(GedimmtProperty, value); }
    public IBrush? Hintergrund { get => GetValue(HintergrundProperty); set => SetValue(HintergrundProperty, value); }
    public double Schrift { get => GetValue(SchriftProperty); set => SetValue(SchriftProperty, value); }

    /// <summary>Punkte je Python-px (px() bei Normal = 1).</summary>
    protected double Px(double wert) => wert * Schrift / 11;

    protected FormattedText Text(string text, IBrush? farbe) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Avalonia.Controls.Documents.TextElement.GetFontFamily(this)), Schrift, farbe);

    /// <summary>Waagrechte Rasterlinie: unterste durchgezogen, sonst gestrichelt (2, 4).</summary>
    protected void Rasterlinie(DrawingContext dc, double links, double rechts, double y, bool durchgezogen)
    {
        var stift = new Pen(Raster, 1, durchgezogen ? null : new DashStyle([2, 4], 0));
        dc.DrawLine(stift, new Point(links, y), new Point(rechts, y));
    }

    /// <summary>Text mit Ausrichtung: "e" rechts, "w" links, sonst Mitte; senkrecht mittig um y.</summary>
    protected static void Schreibe(DrawingContext dc, FormattedText text, double x, double y, string anker = "c")
    {
        var links = anker switch { "e" => x - text.Width, "w" => x, _ => x - text.Width / 2 };
        dc.DrawText(text, new Point(links, y - text.Height / 2));
    }
}

/// <summary>
/// LineChart (fisi_widgets): Liniendiagramm mit Flaeche unter der Kurve, Gitternetz, Punkten
/// bei Werten ueber 0 und gestrichelter Ziellinie (Tagesziel).
/// </summary>
public sealed class LinienDiagramm : DiagrammFlaeche
{
    public static readonly StyledProperty<IBrush?> LinieProperty = AvaloniaProperty.Register<LinienDiagramm, IBrush?>(nameof(Linie));
    public static readonly StyledProperty<IBrush?> ZielFarbeProperty = AvaloniaProperty.Register<LinienDiagramm, IBrush?>(nameof(ZielFarbe));

    private IReadOnlyList<string> _beschriftungen = [];
    private IReadOnlyList<double> _werte = [];
    private string _name = "";
    private double? _fest;
    private (double Wert, string Text)? _ziel;

    static LinienDiagramm()
    {
        AffectsRender<LinienDiagramm>(LinieProperty, ZielFarbeProperty);
    }

    public LinienDiagramm(double hoehe, string linie) : base(hoehe)
    {
        this.Farbe(LinieProperty, "Pinsel." + linie).Farbe(ZielFarbeProperty, "Pinsel.green");
    }

    public IBrush? Linie { get => GetValue(LinieProperty); set => SetValue(LinieProperty, value); }
    public IBrush? ZielFarbe { get => GetValue(ZielFarbeProperty); set => SetValue(ZielFarbeProperty, value); }

    /// <summary>set_data: eine Reihe (Name, Werte); fest = Obergrenze der y-Achse; ziel = Ziellinie.</summary>
    public void Daten(IReadOnlyList<string> beschriftungen, string name, IReadOnlyList<double> werte, double? fest = null, (double Wert, string Text)? ziel = null)
    {
        _beschriftungen = beschriftungen;
        _name = name;
        _werte = werte;
        _fest = fest;
        _ziel = ziel;
        InvalidateVisual();
    }

    public override void Render(DrawingContext dc)
    {
        var (breite, hoehe) = (Bounds.Width, Bounds.Height);
        if (breite <= 1 || hoehe <= 1 || _beschriftungen.Count == 0)
        {
            return;
        }
        var (spitze, schritt) = Diagramm.Achse(_werte, _fest, _ziel?.Wert);
        var yTexte = Enumerable.Range(0, 5).Select(i => Text(Diagramm.AchsenText(schritt * i), Leise)).ToList();
        var links = Math.Max(Px(42), yTexte.Max(t => t.Width) + Px(16));
        double rechts = Px(14), oben = Px(22), unten = Px(30);
        var (flB, flH) = (breite - links - rechts, hoehe - oben - unten);
        if (flB <= 10 || flH <= 10)
        {
            return;
        }
        for (var i = 0; i < 5; i++)
        {
            var y = oben + flH - flH * i / 4;
            Rasterlinie(dc, links, breite - rechts, y, i == 0);
            Schreibe(dc, yTexte[i], links - Px(8), y, "e");
        }
        var anzahl = _beschriftungen.Count;
        var xs = Enumerable.Range(0, anzahl).Select(i => anzahl == 1 ? links + flB / 2 : links + flB * i / (anzahl - 1)).ToList();
        var xTexte = _beschriftungen.Select(b => Text(b, Leise)).ToList();
        var platz = flB / Math.Max(1, anzahl - 1);
        var gezeigt = Diagramm.Gezeigt(anzahl, Diagramm.Schrittweite(anzahl, platz, xTexte.Max(t => t.Width)));
        if (gezeigt.Count > 1)
        {
            var letzteLinks = xs[^1] + Px(6) - xTexte[^1].Width;
            var davor = gezeigt[^2];
            if (xs[davor] + xTexte[davor].Width / 2 + Px(4) > letzteLinks)
            {
                gezeigt.Remove(davor);
            }
        }
        foreach (var i in gezeigt)
        {
            var letzte = i == anzahl - 1 && anzahl > 1;
            Schreibe(dc, xTexte[i], xs[i] + (letzte ? Px(6) : 0), hoehe - unten + Px(15), letzte ? "e" : "c");
        }

        var boden = oben + flH;
        var punkte = _werte.Select((w, i) => (X: xs[i], Y: oben + flH - flH * (w / spitze))).ToList();
        if (punkte.Count >= 2)
        {
            var kurve = Kurve(punkte);
            var flaeche = new StreamGeometry();
            using (var g = flaeche.Open())
            {
                g.BeginFigure(new Point(punkte[0].X, boden), true);
                g.LineTo(new Point(punkte[0].X, punkte[0].Y));
                ZeichneKurve(g, punkte, kurve);
                g.LineTo(new Point(punkte[^1].X, boden));
                g.EndFigure(true);
            }
            var farbe = (Linie as ISolidColorBrush)?.Color ?? Colors.Gray;
            var verlauf = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(107, farbe.R, farbe.G, farbe.B), 0), new GradientStop(Color.FromArgb(0, farbe.R, farbe.G, farbe.B), 1) },
            };
            dc.DrawGeometry(verlauf, null, flaeche);
            var linie = new StreamGeometry();
            using (var g = linie.Open())
            {
                g.BeginFigure(new Point(punkte[0].X, punkte[0].Y), false);
                ZeichneKurve(g, punkte, kurve);
                g.EndFigure(false);
            }
            dc.DrawGeometry(null, new Pen(Linie, Math.Max(2, Px(2.5)), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), linie);
        }
        var punkt = Px(4);
        for (var i = 0; i < punkte.Count; i++)
        {
            if (_werte[i] > 0)
            {
                dc.DrawEllipse(Linie, new Pen(Hintergrund, Px(2)), new Point(punkte[i].X, punkte[i].Y), punkt, punkt);
            }
        }
        if (_ziel is { } ziel && ziel.Wert > 0 && ziel.Wert <= spitze)
        {
            var y = oben + flH - flH * (ziel.Wert / spitze);
            var strich = new Pen(ZielFarbe, Math.Max(1, Px(1.5)), new DashStyle([4, 2.67], 0));
            dc.DrawLine(strich, new Point(links, y), new Point(breite - rechts, y));
            var text = Text(ziel.Text, ZielFarbe);
            Schreibe(dc, text, breite - rechts, oben - Px(10), "e");
            var probe = breite - rechts - text.Width - Px(6);
            dc.DrawLine(strich, new Point(probe - Px(18), oben - Px(10)), new Point(probe, oben - Px(10)));
        }
        // Legende oben links
        var lx = links + Px(4);
        dc.DrawEllipse(Linie, null, new Point(lx + Px(4), oben - Px(10)), Px(4), Px(4));
        Schreibe(dc, Text(_name, Gedimmt), lx + Px(14), oben - Px(10), "w");
    }

    private static List<((double X, double Y) C1, (double X, double Y) C2)>? Kurve(List<(double X, double Y)> punkte) =>
        punkte.Count < 3 ? null : Diagramm.Kontrollpunkte(punkte);

    private static void ZeichneKurve(StreamGeometryContext g, List<(double X, double Y)> punkte, List<((double X, double Y) C1, (double X, double Y) C2)>? kurve)
    {
        for (var i = 1; i < punkte.Count; i++)
        {
            if (kurve is null)
            {
                g.LineTo(new Point(punkte[i].X, punkte[i].Y));
            }
            else
            {
                var (c1, c2) = kurve[i - 1];
                g.CubicBezierTo(new Point(c1.X, c1.Y), new Point(c2.X, c2.Y), new Point(punkte[i].X, punkte[i].Y));
            }
        }
    }
}

/// <summary>
/// ShareBars (fisi_widgets): gestapelte Balken in Prozent je Tag, richtig unten (gruen),
/// falsch oben (rot, schraffiert); ueber jedem Balken der Anteil richtig, Tage ohne Aufgaben
/// mit Strich. Legende oben mit "Zahl über dem Balken: % richtig".
/// </summary>
public sealed class AnteilBalken : DiagrammFlaeche
{
    public static readonly StyledProperty<IBrush?> RichtigProperty = AvaloniaProperty.Register<AnteilBalken, IBrush?>(nameof(Richtig));
    public static readonly StyledProperty<IBrush?> FalschProperty = AvaloniaProperty.Register<AnteilBalken, IBrush?>(nameof(Falsch));

    private IReadOnlyList<TagesAnteil> _tage = [];

    static AnteilBalken()
    {
        AffectsRender<AnteilBalken>(RichtigProperty, FalschProperty);
    }

    public AnteilBalken(double hoehe) : base(hoehe)
    {
        this.Farbe(RichtigProperty, "Pinsel.green").Farbe(FalschProperty, "Pinsel.red");
    }

    public IBrush? Richtig { get => GetValue(RichtigProperty); set => SetValue(RichtigProperty, value); }
    public IBrush? Falsch { get => GetValue(FalschProperty); set => SetValue(FalschProperty, value); }

    public void Daten(IReadOnlyList<TagesAnteil> tage)
    {
        _tage = tage;
        InvalidateVisual();
    }

    /// <summary>Schraffur wie stipple "gray50": jedes zweite Pixel.</summary>
    private IBrush Schraffur()
    {
        var farbe = (Falsch as ISolidColorBrush)?.Color ?? Colors.Red;
        var kachel = new DrawingGroup();
        kachel.Children.Add(new GeometryDrawing { Brush = new SolidColorBrush(farbe), Geometry = new RectangleGeometry(new Rect(0, 0, 1, 1)) });
        kachel.Children.Add(new GeometryDrawing { Brush = new SolidColorBrush(farbe), Geometry = new RectangleGeometry(new Rect(1, 1, 1, 1)) });
        return new DrawingBrush(kachel)
        {
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(0, 0, 2, 2, RelativeUnit.Absolute),
            SourceRect = new RelativeRect(0, 0, 2, 2, RelativeUnit.Absolute),
        };
    }

    public override void Render(DrawingContext dc)
    {
        var (breite, hoehe) = (Bounds.Width, Bounds.Height);
        if (breite <= 1 || hoehe <= 1 || _tage.Count == 0)
        {
            return;
        }
        double links = Px(42), rechts = Px(14), oben = Px(40), unten = Px(30);
        // passt der Hinweis nicht neben die Legende, steht er darunter: dann mehr Platz oben
        var hinweis = Text(FortschrittTexte.BalkenLegende, Leise);
        var legendeBreite = Px(4) + Px(34) * 2 + Text(FortschrittTexte.AnteilRichtig, Gedimmt).Width + Text(FortschrittTexte.AnteilFalsch, Gedimmt).Width;
        var hinweisDaneben = links + legendeBreite + Px(6) + hinweis.Width <= breite - rechts;
        if (!hinweisDaneben)
        {
            hinweis.MaxTextWidth = Math.Max(10, breite - links - rechts);
            oben += hinweis.Height + Px(4);
        }
        var (flB, flH) = (breite - links - rechts, hoehe - oben - unten);
        if (flB <= 10 || flH <= 10)
        {
            return;
        }
        for (var i = 0; i < 5; i++)
        {
            var y = oben + flH - flH * i / 4;
            Rasterlinie(dc, links, breite - rechts, y, i == 0);
            Schreibe(dc, Text(string.Create(CultureInfo.InvariantCulture, $"{i * 25} %"), Leise), links - Px(8), y, "e");
        }
        var anzahl = _tage.Count;
        var platz = flB / anzahl;
        var balken = Math.Max(Px(4), Math.Min(Px(34), platz * 0.62));
        var xTexte = _tage.Select(t => Text(t.Beschriftung, Leise)).ToList();
        var gezeigt = new HashSet<int>(Diagramm.Gezeigt(anzahl, Diagramm.Schrittweite(anzahl, platz, xTexte.Max(t => t.Width))));
        var schraffur = Schraffur();
        var boden = oben + flH;
        for (var i = 0; i < anzahl; i++)
        {
            var tag = _tage[i];
            var mitte = links + platz * (i + 0.5);
            var (x1, x2) = (mitte - balken / 2, mitte + balken / 2);
            if (tag.Richtig is not { } anteil)
            {
                Schreibe(dc, Text(FortschrittTexte.AnteilLeerTag, Leise), mitte, boden - Px(8));
            }
            else
            {
                var grenze = boden - flH * anteil / 100.0;
                if (anteil != 0)
                {
                    dc.DrawRectangle(Richtig, null, new Rect(new Point(x1, grenze), new Point(x2, boden)));
                }
                if (tag.Falsch is > 0)
                {
                    dc.DrawRectangle(schraffur, new Pen(Falsch, 1), new Rect(new Point(x1, oben), new Point(x2, grenze)));
                }
                if (balken >= Px(18) || anzahl <= 14)
                {
                    Schreibe(dc, Text(anteil.ToString(CultureInfo.InvariantCulture), Gedimmt), mitte, oben - Px(8));
                }
            }
            if (gezeigt.Contains(i))
            {
                Schreibe(dc, xTexte[i], mitte, hoehe - unten + Px(15));
            }
        }
        // Legende (bei schmaler Flaeche bricht der Hinweis in eine eigene Zeile)
        var x = links + Px(4);
        var yLegende = Px(10);
        foreach (var (name, pinsel) in new[] { (FortschrittTexte.AnteilRichtig, Richtig), (FortschrittTexte.AnteilFalsch, (IBrush?)schraffur) })
        {
            dc.DrawRectangle(pinsel, new Pen(name == FortschrittTexte.AnteilRichtig ? Richtig : Falsch, 1), new Rect(x, yLegende - Px(5), Px(12), Px(10)));
            var text = Text(name, Gedimmt);
            Schreibe(dc, text, x + Px(18), yLegende, "w");
            x += Px(34) + text.Width;
        }
        if (hinweisDaneben)
        {
            Schreibe(dc, hinweis, x + Px(6), yLegende, "w");
        }
        else
        {
            dc.DrawText(hinweis, new Point(links + Px(4), yLegende + Px(8)));
        }
    }
}
