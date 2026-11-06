namespace Stadtbau.Core;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-37 Punkt 3, Nico: "Mal nimmt er das Zoomen an mal nicht"): Auswertung von Maus und Fingern
/// fuer Draufsicht und Grundriss, ohne Oberflaeche (testbar mit nachgestellten Ereignissen).
/// Ein Finger: Tippen (ohne Bewegung), ein Stueck ziehen (Start auf einem Stueck) oder die Ansicht schieben (Start auf
/// freier Flaeche). Zwei Finger, auch wenn der zweite spaeter kommt: Abstand = Zoomen, Mitte = Ansicht schieben. Sobald
/// der zweite Finger aufliegt, zaehlt der erste nicht mehr als Tippen oder Stueck-Ziehen; ein schon begonnenes Ziehen
/// eines Stuecks wird abgebrochen (das Stueck bleibt, wo es war). Erst wenn alle Finger oben sind, beginnt eine neue Geste.
/// Ursache vorher: die Draufsicht kannte keine zwei Finger, der Grundriss liess die Gestenerkennung von Avalonia und die
/// eigene Auswertung des ersten Fingers gleichzeitig laufen (der zweite Finger setzte Auswahl und Start neu, der Zoom
/// rechnete von einem veralteten Wert, wenn die Geste ohne "Ende" abbrach).
/// </summary>
public sealed class Fingergeste(double ziehenAb = 6, bool altFuerTests = false)
{
    public enum Art { Nichts, Tippen, StueckZiehen, StueckFertig, StueckAbbrechen, AnsichtSchieben, Zoomen }

    /// <summary>Ergebnis eines Ereignisses: Tippen an (X, Y); Stueck um (Dx, Dy) seit dem Druck; Ansicht um (Dx, Dy);
    /// Zoomen um Faktor an der Mitte (X, Y), dazu Ansicht um (Dx, Dy).</summary>
    public readonly record struct Schritt(Art Art, double X = 0, double Y = 0, double Dx = 0, double Dy = 0, double Faktor = 1);

    private readonly Dictionary<long, (double X, double Y)> _finger = [];
    private (double X, double Y) _start;
    private bool _aufStueck;
    private bool _gezogen;
    private bool _zwei;
    private double _abstand;
    private (double X, double Y) _mitte;

    /// <summary>Liegen gerade Finger (oder die Maus) auf?</summary>
    public bool Aktiv => _finger.Count > 0;

    /// <summary>Laeuft eine Geste mit zwei Fingern (bis alle Finger oben sind)?</summary>
    public bool ZweiFinger => _zwei;

    public Schritt Druecken(long id, double x, double y, bool aufStueck)
    {
        if (altFuerTests)
        {
            // Gegenprobe: wie vor S 0.5.1, jeder Druck beginnt eine neue Ein-Finger-Geste
            _finger.Clear();
        }
        _finger[id] = (x, y);
        if (_finger.Count == 1)
        {
            _start = (x, y);
            _aufStueck = aufStueck;
            _gezogen = false;
            _zwei = false;
            return new Schritt(Art.Nichts);
        }
        var abbrechen = _aufStueck && _gezogen;
        _zwei = true;
        _abstand = Abstand();
        _mitte = Mitte();
        return new Schritt(abbrechen ? Art.StueckAbbrechen : Art.Nichts);
    }

    public Schritt Bewegen(long id, double x, double y)
    {
        if (!_finger.TryGetValue(id, out var vorher))
        {
            return new Schritt(Art.Nichts);
        }
        _finger[id] = (x, y);
        if (_zwei)
        {
            if (_finger.Count < 2)
            {
                return new Schritt(Art.Nichts);
            }
            var abstand = Abstand();
            var mitte = Mitte();
            var faktor = _abstand > 1 && abstand > 1 ? abstand / _abstand : 1;
            var schritt = new Schritt(Art.Zoomen, mitte.X, mitte.Y, mitte.X - _mitte.X, mitte.Y - _mitte.Y, faktor);
            _abstand = abstand;
            _mitte = mitte;
            return schritt;
        }
        if (!_gezogen && Math.Abs(x - _start.X) + Math.Abs(y - _start.Y) < ziehenAb)
        {
            return new Schritt(Art.Nichts);
        }
        _gezogen = true;
        return _aufStueck
            ? new Schritt(Art.StueckZiehen, x, y, x - _start.X, y - _start.Y)
            : new Schritt(Art.AnsichtSchieben, x, y, x - vorher.X, y - vorher.Y);
    }

    public Schritt Loslassen(long id)
    {
        if (!_finger.Remove(id, out var p))
        {
            return new Schritt(Art.Nichts);
        }
        if (_finger.Count > 0 || _zwei)
        {
            if (_finger.Count == 0)
            {
                _zwei = false;
            }
            return new Schritt(Art.Nichts);
        }
        if (!_gezogen)
        {
            return new Schritt(Art.Tippen, p.X, p.Y);
        }
        _gezogen = false;
        return _aufStueck ? new Schritt(Art.StueckFertig, p.X, p.Y, p.X - _start.X, p.Y - _start.Y) : new Schritt(Art.Nichts);
    }

    /// <summary>Zeiger verloren (Avalonia nimmt die Erfassung weg): Finger vergessen, ein Ziehen bricht ab.</summary>
    public Schritt Verloren(long id)
    {
        if (!_finger.Remove(id))
        {
            return new Schritt(Art.Nichts);
        }
        var abbrechen = _aufStueck && _gezogen && !_zwei;
        if (_finger.Count == 0)
        {
            _zwei = false;
            _gezogen = false;
        }
        return new Schritt(abbrechen ? Art.StueckAbbrechen : Art.Nichts);
    }

    private double Abstand()
    {
        var f = _finger.Values.Take(2).ToArray();
        return Math.Sqrt(((f[0].X - f[1].X) * (f[0].X - f[1].X)) + ((f[0].Y - f[1].Y) * (f[0].Y - f[1].Y)));
    }

    private (double X, double Y) Mitte()
    {
        var f = _finger.Values.Take(2).ToArray();
        return ((f[0].X + f[1].X) / 2, (f[0].Y + f[1].Y) / 2);
    }
}
