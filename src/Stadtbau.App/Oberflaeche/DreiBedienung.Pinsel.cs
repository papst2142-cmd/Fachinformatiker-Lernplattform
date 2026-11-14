using Avalonia;
using Avalonia.Media;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using V2 = System.Numerics.Vector2;
using V3 = System.Numerics.Vector3;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle E4 Nachtrag (Weisung P12-249, Sammelliste 339, Nico: „auch in 3d soll der funktionieren“): der Gelaende-Pinsel
/// (Heben, Senken, Glaetten, Groesse) wirkt auch in der 3D-Ansicht. Ein Finger oder die linke Maustaste malt einen Zug,
/// zwei Finger und rechte/mittlere Maustaste bewegen weiter nur die Kamera. Die Stelle ist der Punkt auf dem Gelaende
/// unter dem Zeiger (Strahl auf den Boden mit der Hoehe des Gelaendes, <see cref="Kamera.Boden(float, float, float, float, Func{float, float, float}?)"/>);
/// der Zug geht ueber dieselbe Rechnung wie in der Draufsicht (<see cref="KartenFlaeche.PinselZugAnwenden"/> mit
/// <see cref="AussenBearbeitung.Pinselzug"/>), ein Zug = ein Schritt. Waehrend des Zugs liegt der Pinselkreis am Boden.
/// </summary>
public sealed partial class DreiBedienung
{
    private static readonly IBrush PinselKreisFarbe = new SolidColorBrush(Color.Parse("#6D4C9F"), 0.3);
    private static readonly IPen PinselKreisRand = new Pen(new SolidColorBrush(Color.Parse("#4A2F75")), 1.5);

    private readonly List<V2> _pinselZug = [];

    /// <summary>Start-Test: Stellen des laufenden Zugs in 3D (Welt).</summary>
    public IReadOnlyList<V2> PinselZugPunkte => _pinselZug;

    /// <summary>Start-Test: Bildflaeche des zuletzt gezeichneten Pinselkreises (leer = keiner).</summary>
    public Rect PinselKreisRahmen { get; private set; }

    /// <summary>Malt der Pinsel gerade in 3D (an, nicht im Figur-Modus)?</summary>
    private bool Malt => _karte.GelaendePinsel is not null && !FigurLaufen;

    /// <summary>Hoehe des Gelaendes an (x, z) aus dem aktuellen Stand (null = kein Gelaende).</summary>
    private Func<float, float, float>? GelaendeHoehe() =>
        Projekt is { } proj && AussenBearbeitung.GelaendeVon(proj.AussenStand) is { } g ? g.Hoehe : null;

    /// <summary>Punkt auf dem Gelaende unter dem Bildpunkt (auch auf schon gehobenem Gelaende).</summary>
    internal V2? GelaendeAn(Point p) =>
        Kamera?.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height, GelaendeHoehe());

    /// <summary>Druck mit dem Pinsel: der Zug beginnt an der Stelle unter dem Zeiger.</summary>
    private void PinselDruecken(Point p)
    {
        _pinselZug.Clear();
        if (GelaendeAn(p) is { } w)
        {
            _pinselZug.Add(w);
        }
        InvalidateVisual();
    }

    /// <summary>Schritt der Fingerauswertung mit dem Pinsel; true = erledigt.</summary>
    private bool PinselAnwenden(Fingergeste.Schritt s)
    {
        switch (s.Art)
        {
            case Fingergeste.Art.StueckZiehen when _pinselZug.Count > 0:
                if (GelaendeAn(new Point(s.X, s.Y)) is { } w && V2.Distance(w, _pinselZug[^1]) >= Gelaende.Raster / 4)
                {
                    _pinselZug.Add(w);
                    InvalidateVisual();
                }
                return true;
            case Fingergeste.Art.StueckFertig or Fingergeste.Art.Tippen when _pinselZug.Count > 0:
                _karte.PinselZugAnwenden([.. _pinselZug]);
                _pinselZug.Clear();
                InvalidateVisual();
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                // ein zweiter Finger: die Kamera bewegt sich, der angefangene Zug faellt weg (kein Schritt)
                _pinselZug.Clear();
                InvalidateVisual();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Pinselkreis am Boden (Halbmesser = Pinselgroesse) an jeder Stelle des laufenden Zugs.</summary>
    private void PinselZeichnen(DrawingContext ctx, Kamera k)
    {
        PinselKreisRahmen = default;
        if (!Malt || _pinselZug.Count == 0)
        {
            return;
        }
        var hoehe = GelaendeHoehe();
        var r = _karte.PinselGroesse;
        Rect? rahmen = null;
        foreach (var m in _pinselZug)
        {
            var punkte = new List<Point>(24);
            for (var i = 0; i < 24; i++)
            {
                var w = i * MathF.PI * 2 / 24;
                var x = m.X + (r * MathF.Cos(w));
                var z = m.Y + (r * MathF.Sin(w));
                var y = (hoehe?.Invoke(x, z) ?? 0f) + Bauen3D.SchattenHoehe;
                if (k.Bildpunkt(new V3(x, y, z), (float)Bounds.Width, (float)Bounds.Height) is { } b)
                {
                    punkte.Add(new Point(b.X, b.Y));
                }
            }
            if (punkte.Count < 3)
            {
                continue;
            }
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(punkte[0], true);
                foreach (var q in punkte.Skip(1))
                {
                    c.LineTo(q);
                }
                c.EndFigure(true);
            }
            ctx.DrawGeometry(PinselKreisFarbe, PinselKreisRand, g);
            rahmen = rahmen is { } a ? a.Union(g.Bounds) : g.Bounds;
        }
        PinselKreisRahmen = rahmen ?? default;
    }
}
