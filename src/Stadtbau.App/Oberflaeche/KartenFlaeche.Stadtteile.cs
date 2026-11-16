using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle Nachladen S 0.54.0 (Weisung P12-280, Nico 1c): „Stadtteile zeigen“ legt das feste Raster der Kacheln (16 × 16
/// Felder) ueber die Draufsicht, mit dem Namen jeder Kachel (A1, B3 …) und, sobald gerechnet, ihren Dreiecken und Netzdaten
/// (<see cref="StadtteilWert"/>). Nur Anzeige.
/// </summary>
public sealed partial class KartenFlaeche
{
    private bool _stadtteileAn;
    private IReadOnlyList<StadtteilWert>? _stadtteile;

    /// <summary>Raster der Stadtteile zeigen (Knopf „Stadtteile“, Taste T).</summary>
    public bool StadtteileAn
    {
        get => _stadtteileAn;
        set
        {
            _stadtteileAn = value;
            InvalidateVisual();
        }
    }

    /// <summary>Werte je Kachel (null = noch nicht gerechnet: nur Raster und Namen).</summary>
    public IReadOnlyList<StadtteilWert>? Stadtteile
    {
        get => _stadtteile;
        set
        {
            _stadtteile = value;
            if (_stadtteileAn)
            {
                InvalidateVisual();
            }
        }
    }

    /// <summary>Start-Test: Namen und Wertezeilen im letzten Bild, Linien des Rasters.</summary>
    public (int Namen, int Werte, int Linien) StadtteileGezeichnet { get; private set; }

    /// <summary>Kachel an einem Weltpunkt der Karte (fuer Vorlesen und 3D).</summary>
    public Stadtteile.Kachel StadtteilAn(double x, double y)
    {
        var (b, h) = Welt();
        return Lernplattform.Core.Stadtbau.Stadtteile.Von((float)x, (float)y, (float)b, (float)h);
    }

    private void StadtteileZeichnen(DrawingContext ctx)
    {
        StadtteileGezeichnet = default;
        if (!_stadtteileAn)
        {
            return;
        }
        var (b, h) = Welt();
        var groesse = Lernplattform.Core.Stadtbau.Stadtteile.Groesse;
        var stift = new Pen(Pinsel("#3949AB", 0.9), 2);
        var linien = 0;
        // Linien an jeder Kachelgrenze und am Rand der Karte (auch wenn die Karte kein Vielfaches von 16 ist)
        static IEnumerable<double> Grenzen(double ende, double schritt)
        {
            for (var v = 0.0; v < ende - 0.001; v += schritt)
            {
                yield return v;
            }
            yield return ende;
        }
        foreach (var x in Grenzen(b, groesse))
        {
            ctx.DrawLine(stift, P(x, 0), P(x, h));
            linien++;
        }
        foreach (var y in Grenzen(h, groesse))
        {
            ctx.DrawLine(stift, P(0, y), P(b, y));
            linien++;
        }
        // Namen nur, wenn eine Kachel gross genug im Bild ist; Werte erst ab etwa 110 Bildpunkten Breite
        var breite = groesse * _zoom;
        var namen = 0;
        var werte = 0;
        if (breite >= 26)
        {
            var schrift = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
            var normal = new Typeface(FontFamily.Default);
            var grund = Pinsel("#FFFFFF", 0.82);
            var text = Pinsel("#1A237E");
            var je = _stadtteile?.ToDictionary(w => w.Kachel);
            foreach (var k in Lernplattform.Core.Stadtbau.Stadtteile.Alle((float)b, (float)h))
            {
                var ecke = P(k.Min.X, k.Min.Y);
                if (ecke.X > Bounds.Width || ecke.Y > Bounds.Height || ecke.X + breite < 0 || ecke.Y + breite < 0)
                {
                    continue;
                }
                var name = new FormattedText(k.Name, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, Math.Min(16, breite / 3), text);
                FormattedText? zeile = null;
                if (breite >= 110 && je is not null && je.TryGetValue(k, out var w))
                {
                    zeile = new FormattedText(Texte.StadtteilWerte(w.Dreiecke, w.Mb), CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, normal, 11, text)
                    {
                        MaxTextWidth = breite - 8,
                        MaxLineCount = 1,
                        Trimming = TextTrimming.CharacterEllipsis,
                    };
                }
                var hoch = name.Height + (zeile?.Height ?? 0);
                var kasten = new Rect(ecke.X + 3, ecke.Y + 3, Math.Max(name.Width, zeile?.Width ?? 0) + 6, hoch + 4);
                ctx.FillRectangle(grund, kasten, 3);
                ctx.DrawText(name, new Point(kasten.X + 3, kasten.Y + 2));
                namen++;
                if (zeile is not null)
                {
                    ctx.DrawText(zeile, new Point(kasten.X + 3, kasten.Y + 2 + name.Height));
                    werte++;
                }
            }
        }
        StadtteileGezeichnet = (namen, werte, linien);
    }
}
