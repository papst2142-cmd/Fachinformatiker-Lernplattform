using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 2): Stadtteil-Namen in der Draufsicht. Die Bereiche stehen immer gestrichelt mit
/// ihrem Namen auf der Karte (nur wenn die Stadt welche hat). Mit „Stadtteil-Namen“ (Knopf, Taste B) zieht ein Finger oder
/// die Maus ein Rechteck (<see cref="NameGezogen"/>), Tippen in einen Bereich waehlt ihn (<see cref="NameGetippt"/>); zwei
/// Finger, rechte Maus und Leertaste bleiben bei der Ansicht. Tastatur und Screenreader: Liste rechts (Hauptansicht).
/// </summary>
public sealed partial class KartenFlaeche
{
    private bool _namenAn;
    private int? _nameGewaehlt;
    private (double X1, double Z1, double X2, double Z2)? _namenZug;

    /// <summary>Stadtteil-Namen bearbeiten (Ziehen legt einen neuen an).</summary>
    public bool NamenAn
    {
        get => _namenAn;
        set
        {
            if (_namenAn == value)
            {
                return;
            }
            _namenAn = value;
            _namenZug = null;
            if (value)
            {
                // wie das Massband: kein Setzen, kein Pinsel, keine Auswahl eines Stuecks
                GelaendePinsel = null;
                SetzenBeenden();
                MassbandAn = false;
                MehrereWaehlen = false;
                Waehlen(null);
            }
            InvalidateVisual();
            NamenGeaendert?.Invoke();
        }
    }

    /// <summary>Stadtteil-Namen an oder aus (auch von selbst, wenn ein anderes Werkzeug beginnt).</summary>
    public event Action? NamenGeaendert;

    /// <summary>Gewaehlter Stadtteil (Stelle in der Liste), hervorgehoben.</summary>
    public int? NameGewaehlt
    {
        get => _nameGewaehlt;
        set
        {
            _nameGewaehlt = value;
            InvalidateVisual();
        }
    }

    /// <summary>Ein Rechteck wurde gezogen (Welt, auf Raster und Karte gebracht).</summary>
    public event Action<double, double, double, double>? NameGezogen;

    /// <summary>In einen Bereich getippt (Stelle) oder daneben (null).</summary>
    public event Action<int?>? NameGetippt;

    /// <summary>Start-Test: gezeichnete Namen und ob eine Vorschau des Rechtecks stand.</summary>
    public (int Namen, bool Vorschau) NamenGezeichnet { get; private set; }

    /// <summary>Vorschau des gezogenen Rechtecks (Welt), null ohne.</summary>
    public (double X1, double Z1, double X2, double Z2)? NamenZug => _namenZug;

    private (double X1, double Z1, double X2, double Z2) NamenRechteck(Point a, Point b)
    {
        var (wa, wb) = (WeltPunkt(a), WeltPunkt(b));
        var (breite, tiefe) = Welt();
        return StadtteilNamen.Rechteck(wa.X, wa.Y, wb.X, wb.Y, breite, tiefe);
    }

    /// <summary>Tippen, Ziehen und Loslassen beim Bearbeiten der Namen (true = verarbeitet).</summary>
    private bool NamenAnwenden(Fingergeste.Schritt s)
    {
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when !_nurSchieben:
            {
                var w = WeltPunkt(new Point(s.X, s.Y));
                int? treffer = null;
                if (Stand is { } a)
                {
                    var liste = StadtteilNamen.Eintraege(a);
                    for (var i = 0; i < liste.Count; i++)
                    {
                        if (liste[i] is { } b && b.Drin(w.X, w.Y))
                        {
                            treffer = i;
                            break;
                        }
                    }
                }
                NameGetippt?.Invoke(treffer);
                return true;
            }
            case Fingergeste.Art.StueckZiehen:
                _namenZug = NamenRechteck(new Point(s.X - s.Dx, s.Y - s.Dy), new Point(s.X, s.Y));
                InvalidateVisual();
                return true;
            case Fingergeste.Art.StueckFertig:
            {
                var r = NamenRechteck(new Point(s.X - s.Dx, s.Y - s.Dy), new Point(s.X, s.Y));
                _namenZug = null;
                InvalidateVisual();
                NameGezogen?.Invoke(r.X1, r.Z1, r.X2, r.Z2);
                return true;
            }
            case Fingergeste.Art.StueckAbbrechen:
                _namenZug = null;
                InvalidateVisual();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Start-Test: Rechteck ziehen wie ein Finger (Druecken, Bewegen, Loslassen in Bildpunkten).</summary>
    internal void NamenZiehenFuerTests(Point von, Point bis)
    {
        NamenAnwenden(new Fingergeste.Schritt(Fingergeste.Art.StueckZiehen, bis.X, bis.Y, bis.X - von.X, bis.Y - von.Y, 1));
        NamenAnwenden(new Fingergeste.Schritt(Fingergeste.Art.StueckFertig, bis.X, bis.Y, bis.X - von.X, bis.Y - von.Y, 1));
    }

    /// <summary>Weltpunkt in der Mitte der Ansicht (fuer „Neuer Stadtteil“ mit der Tastatur).</summary>
    public Point AnsichtMitte => WeltPunkt(new Point(Bounds.Width / 2, (ObenFrei + Bounds.Height) / 2));

    /// <summary>Bildpunkt eines Weltpunkts (Start-Test).</summary>
    internal Point BildPunkt(double x, double z) => P(x, z);

    private void NamenZeichnen(DrawingContext ctx)
    {
        NamenGezeichnet = default;
        if (Stand is not { } a)
        {
            return;
        }
        var liste = StadtteilNamen.Eintraege(a);
        var namen = 0;
        var dunkel = Pinsel("#4A148C");
        var gestrichelt = new Pen(Pinsel("#6A1B9A", 0.9), 2) { DashStyle = new DashStyle([4, 3], 0) };
        var gewaehlt = new Pen(Pinsel("#D81B60"), 3.5);
        var schrift = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
        for (var i = 0; i < liste.Count; i++)
        {
            if (liste[i] is not { } b)
            {
                continue;
            }
            var r = new Rect(P(b.X1, b.Z1), P(b.X2, b.Z2));
            var istGewaehlt = _namenAn && _nameGewaehlt == i;
            if (_namenAn)
            {
                ctx.FillRectangle(Pinsel(istGewaehlt ? "#D81B60" : "#7B1FA2", istGewaehlt ? 0.16 : 0.07), r);
            }
            ctx.DrawRectangle(null, istGewaehlt ? gewaehlt : gestrichelt, r);
            var text = new FormattedText(b.Name, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, 13, dunkel)
            {
                MaxTextWidth = Math.Max(1, r.Width - 12),
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            if (r.Width < 30 || r.Height < 22)
            {
                continue;
            }
            // Schild unten links im Bereich (oben links steht beim Raster der Kacheln deren Name)
            var kasten = new Rect(r.X + 4, r.Bottom - text.Height - 8, text.Width + 8, text.Height + 4);
            ctx.FillRectangle(Pinsel("#FFFFFF", 0.88), kasten, 4);
            ctx.DrawText(text, new Point(kasten.X + 4, kasten.Y + 2));
            namen++;
        }
        var vorschau = false;
        if (_namenZug is { } z)
        {
            var r = new Rect(P(z.X1, z.Z1), P(z.X2, z.Z2));
            ctx.FillRectangle(Pinsel("#D81B60", 0.12), r);
            ctx.DrawRectangle(null, new Pen(Pinsel("#D81B60"), 2.5) { DashStyle = new DashStyle([6, 3], 0) }, r);
            var masse = new FormattedText(Texte.StadtteilMasse(z.X2 - z.X1, z.Z2 - z.Z1), CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, 13, dunkel);
            var kasten = new Rect(r.X + 4, r.Y + 4, masse.Width + 8, masse.Height + 4);
            ctx.FillRectangle(Pinsel("#FFFFFF", 0.9), kasten, 4);
            ctx.DrawText(masse, new Point(kasten.X + 4, kasten.Y + 2));
            vorschau = true;
        }
        NamenGezeichnet = (namen, vorschau);
    }
}
