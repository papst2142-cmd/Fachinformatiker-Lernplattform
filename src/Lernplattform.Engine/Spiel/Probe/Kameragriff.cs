namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 8 W8 (Sammelliste 132 und 135, E2 P8-16 Punkt 4): Maus in Stadt und Innenraum. Linke Taste:
/// Tippen (ohne Bewegung) oder Ziehen (Ansicht verschieben) wie heute; rechte Taste gedrueckt halten und
/// waagrecht bewegen dreht die Kamera um <see cref="GradJePunkt"/> je Bildpunkt (nur Drehen, kein Neigen).
/// Die rechte Taste loest nie Tippen und damit nie Gehen aus, auch ohne Bewegung; waren beide Tasten
/// gedrueckt, gibt es kein Tippen. Finger gehen weiter ueber die Gesten der Flaeche.
/// </summary>
public sealed class Kameragriff(double ziehenAb)
{
    public const float GradJePunkt = 0.4f;

    public enum Art { Nichts, Ziehen, Drehen }

    /// <summary>Ergebnis einer Bewegung: Ziehen um (Dx, Dy) Bildpunkte oder Drehen um Grad.</summary>
    public readonly record struct Schritt(Art Art, double Dx = 0, double Dy = 0, float Grad = 0);

    private bool _links;
    private bool _rechts;
    private bool _beide;
    private bool _gezogen;
    private double _startX;
    private double _startY;
    private double _x;
    private double _y;

    public bool Gedrueckt => _links || _rechts;

    public void Druecken(bool rechts, double x, double y)
    {
        if (!Gedrueckt)
        {
            _startX = x;
            _startY = y;
            _gezogen = false;
            _beide = false;
        }
        else
        {
            _beide = true;
        }
        if (rechts)
        {
            _rechts = true;
        }
        else
        {
            _links = true;
        }
        _x = x;
        _y = y;
    }

    public Schritt Bewegen(double x, double y)
    {
        var dx = x - _x;
        var dy = y - _y;
        _x = x;
        _y = y;
        if (_rechts)
        {
            return dx == 0 ? new Schritt(Art.Nichts) : new Schritt(Art.Drehen, Grad: (float)dx * GradJePunkt);
        }
        if (!_links)
        {
            return new Schritt(Art.Nichts);
        }
        if (!_gezogen && Math.Abs(x - _startX) + Math.Abs(y - _startY) > ziehenAb)
        {
            _gezogen = true;
        }
        return _gezogen ? new Schritt(Art.Ziehen, dx, dy) : new Schritt(Art.Nichts);
    }

    /// <summary>Taste losgelassen; true = Tippen (nur linke Taste allein, ohne Ziehen).</summary>
    public bool Loslassen(bool rechts)
    {
        var tippen = !rechts && _links && !_rechts && !_beide && !_gezogen;
        if (rechts)
        {
            _rechts = false;
        }
        else
        {
            _links = false;
        }
        return tippen;
    }

    /// <summary>Zeiger verloren (Fenster verlassen, anderes Fenster): alles los, kein Tippen.</summary>
    public void Abbrechen()
    {
        _links = false;
        _rechts = false;
    }
}
