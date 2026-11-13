using System.Numerics;
using Avalonia.Media;
using Stadtbau.Core;
using Point = Avalonia.Point;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.28.0 (Weisung P12-121, Nico 2a): Tuer setzen in der 3D-Ansicht. Ein Tipp auf die Fassade (oder neben das
/// Haus auf den Boden) setzt die Tuer an die naechste Stelle der Wand, ein Finger zieht sie an der Wand entlang, auch um
/// die Ecke; jedes Loslassen ist ein Schritt ueber die Draufsicht (<see cref="KartenFlaeche.TuerSetzenAn"/>).
/// </summary>
public sealed partial class DreiBedienung
{
    /// <summary>Weltstelle (x, y der Karte) fuer die Tuer unter dem Bildpunkt: Treffer auf dem Kasten des Hauses, sonst Boden.</summary>
    public Vector2? TuerPunktAn(Point p)
    {
        if (Kamera is not { } k || Projekt is not { } proj || _karte.TuerFuer is not { } s
            || k.Strahl((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is not { } strahl)
        {
            return null;
        }
        var szene = _drei.Spiel?.Szene.Haeuser.Select(h => (h.Min, h.Max)).ToList();
        var haus = Bauen3D.Haeuser(proj.AussenStand, szene is null ? null : f => Bauen3D.HoeheAus(szene, f)).FirstOrDefault(h => h.Stelle == s);
        // S 0.33.0 (Weisung P12-148 Frage 1 = a): Treffer auf die gezeichneten Bloecke des Hauses (dort sitzt die Tuer)
        if (haus is not null && _karte.Fassade is { } f)
        {
            // S 0.49.0: die Bloecke eines schraeg gedrehten Hauses liegen im Rahmen des Hauses, der Strahl wird dorthin gedreht
            var fein = f.Fein;
            var s0 = fein.Zurueck(new Vector2(strahl.Start.X, strahl.Start.Z));
            var r0 = fein.RichtungZurueck(new Vector2(strahl.Richtung.X, strahl.Richtung.Z));
            var (start, richtung) = (new System.Numerics.Vector3(s0.X, strahl.Start.Y, s0.Y), new System.Numerics.Vector3(r0.X, strahl.Richtung.Y, r0.Y));
            var naechster = f.Bloecke.Select(b => Bauen3D.Schnitt(start, richtung, new System.Numerics.Vector3(b.X1, 0, b.Z1), new System.Numerics.Vector3(b.X2, haus.Max.Y, b.Z2)))
                .Where(t => t is not null).Min();
            if (naechster is { } tb)
            {
                var q = start + (richtung * tb);
                return fein.Punkt(new Vector2(q.X, q.Z));
            }
        }
        if (haus is not null && Bauen3D.Schnitt(strahl.Start, strahl.Richtung, haus.Min, haus.Max) is { } t)
        {
            var q = strahl.Start + (strahl.Richtung * t);
            return new Vector2(q.X, q.Z);
        }
        return BodenAn(p);
    }

    /// <summary>Fingerschritt beim Tuer-Setzen (true = verarbeitet).</summary>
    private bool TuerAnwenden(Fingergeste.Schritt s)
    {
        var p = new Point(s.X, s.Y);
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen:
            case Fingergeste.Art.StueckFertig:
                if (TuerPunktAn(p) is { } t)
                {
                    _karte.TuerSetzenAn(t.X, t.Y, Kamera is { } k ? Math.Max(1.5, Bauen3D.Greifen(k.Abstand) * 3) : null);
                }
                return true;
            case Fingergeste.Art.StueckZiehen:
                if (TuerPunktAn(p) is { } v)
                {
                    _karte.TuerVorschauAn(v.X, v.Y);
                }
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                _karte.TuerVorschauWeg();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Start-Test: Tippen beim Tuer-Setzen an einem Bildpunkt.</summary>
    internal void TuerTippenFuerTests(Point p) => TuerAnwenden(new Fingergeste.Schritt(Fingergeste.Art.Tippen, p.X, p.Y, 0, 0, 1));

    private static readonly IPen TuerStift = new Pen(new SolidColorBrush(Color.Parse("#E08A00")), 5, lineCap: PenLineCap.Round);
    private static readonly IPen TuerRand = new Pen(Brushes.White, 8, lineCap: PenLineCap.Round);

    /// <summary>Tuer des Hauses beim Tuer-Setzen (Vorschau oder gesetzte Tuer) als Strich am Fuss der Wand mit Pfeil hinaus.</summary>
    private void TuerZeichnen(DrawingContext ctx, Lernplattform.Core.Spiel.Probe.Kamera k)
    {
        if (_karte.TuerFuer is not { } s || Projekt is not { } proj || !AussenBearbeitung.Gueltig(proj.AussenStand, s))
        {
            return;
        }
        (Vector2 P, Vector2 N)? tuer = _karte.TuerVorschau is { } v ? (v.Punkt, v.Aussen)
            : TuerSetzen.Tuer(proj.AussenStand, s) is { } t ? (t, AussenBearbeitung.HausTuerSeite(proj.AussenStand, s)) : null;
        if (tuer is not { } d)
        {
            return;
        }
        var quer = new Vector2(-d.N.Y, d.N.X) * (Lernplattform.Core.Spiel.Probe.StadtSzene.TuerBreite / 2);
        var (w, h) = ((float)Bounds.Width, (float)Bounds.Height);
        Point? Bild(Vector2 q, float hoehe) => k.Bildpunkt(new Vector3(q.X, hoehe, q.Y), w, h) is { } b ? new Point(b.X, b.Y) : null;
        if (Bild(d.P - quer, 0.05f) is { } a && Bild(d.P + quer, 0.05f) is { } b && Bild(d.P, 0.05f) is { } m && Bild(d.P + (d.N * 0.5f), 0.05f) is { } vor)
        {
            ctx.DrawLine(TuerRand, a, b);
            ctx.DrawLine(TuerStift, a, b);
            ctx.DrawLine(TuerStift, m, vor);
        }
    }
}
