using Avalonia;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.96.0 (Sammelliste 385 Welle 2, Weisung P12-309, Nico Antworten 2a und 4): Finger auf der Flaeche (Handy und
/// Tablet). Ein Daumen, der in der unteren Haelfte des freien Bereichs aufsetzt, wird ab einem kleinen Weg zum Stick
/// (<see cref="Sticks"/>): links gehen, rechts drehen (draussen die Kamera, in der Ich-Sicht den Blick, wie die rechte Maustaste).
/// Ein kurzer Tipp ohne Ziehen bleibt Antippen (wenn eingeschaltet), auf beiden Haelften. Beide Sticks gehen zugleich. Zwei
/// Finger, die nicht beide Stick-Daumen sind, zoomen, drehen und neigen wie heute; ein Finger oben zieht wie heute. Keine Sticks
/// ohne Spiel, im Werkzeug, ohne 3D und solange Spiel-Handy oder Spielansichten offen sind (<see cref="SticksGesperrt"/>).
/// </summary>
public sealed partial class StadtFlaeche
{
    private readonly Sticks _sticks = new();

    /// <summary>Kandidaten (Daumen unten, noch nicht gezogen) mit ihrer letzten Stelle.</summary>
    private readonly Dictionary<long, Point> _kandidatPunkt = [];

    /// <summary>Die zwei Sticks (Start-Test, Anzeige der Seite).</summary>
    public Sticks StickZustand => _sticks;

    /// <summary>Ein Stick erschien, bewegte sich oder verschwand (die Seite zeichnet ihn neu).</summary>
    public event Action? SticksGeaendert;

    /// <summary>Setzt die Seite: true, solange etwas ueber der Flaeche liegt (Spiel-Handy, Spielansichten, Ortsliste ohne 3D).</summary>
    public Func<bool>? SticksGesperrt { get; set; }

    /// <summary>Gibt es gerade Sticks? (Spiel mit fester Kamera, 3D bereit, nichts liegt darueber.)</summary>
    public bool SticksMoeglich => FesteKamera && !OhneGehen && _spiel is not null && GlBereit && !(SticksGesperrt?.Invoke() ?? false);

    /// <summary>Freier Bereich fuer die Sticks (zwischen Kopfzeile und Leiste, in Punkten der Flaeche).</summary>
    public Sticks.Bereich StickBereich
    {
        get
        {
            var b = new Sticks.Bereich(Bounds.Width, Bounds.Height, Frei.Oben, Frei.Unten, LeisteX.Von, LeisteX.Bis);
            b = KnopfSperre is { } sperre ? b with { Sperre = sperre(b) } : b;
            // Welle A 1.6.0 (Sammelliste 444): Knopf der Handlung und Ich-Sicht ueber dem linken Stick
            b = KnopfSperreLinks is { } links ? b with { SperreLinks = links(b) } : b;
            // Welle A 1.6.0 (Weisung P12-361): Handy-Symbol neben einem Stick
            return KnopfSperreTelefon is { } handy ? b with { SperreTelefon = handy(b) } : b;
        }
    }

    /// <summary>Welle A 1.6.0 (Weisung P12-361): Flaeche des Handy-Symbols, die beide Sticks frei halten (setzt die Seite).</summary>
    public Func<Sticks.Bereich, (double X1, double Y1, double X2, double Y2)>? KnopfSperreTelefon { get; set; }

    /// <summary>Welle A 1.0.0 (Sammelliste 405): Flaeche der runden Knoepfe, die der rechte Stick frei haelt (setzt die Seite).</summary>
    public Func<Sticks.Bereich, (double X1, double Y1, double X2, double Y2)>? KnopfSperre { get; set; }

    /// <summary>Welle A 1.6.0 (Sammelliste 444): Flaeche von Handlung und Ich-Sicht, die der linke Stick frei haelt (setzt die Seite).</summary>
    public Func<Sticks.Bereich, (double X1, double Y1, double X2, double Y2)>? KnopfSperreLinks { get; set; }

    /// <summary>Finger setzt auf (Touch und Stift; auch der Start-Test mit gedachten Fingern).</summary>
    public void FingerAuf(long id, Point p)
    {
        if (_zeiger.Count == 0 && SticksMoeglich && _sticks.Druecken(id, p.X, p.Y, StickBereich))
        {
            _kandidatPunkt[id] = p;
            if (_kandidatPunkt.Count == 1 && !_sticks.Aktiv)
            {
                _gezogen = false;
            }
            return;
        }
        if (_kandidatPunkt.Count > 0)
        {
            // ein zweiter Finger, der kein Stick-Daumen ist: die Kandidaten werden wieder gewoehnliche Finger (zwei Finger wie heute)
            foreach (var (k, kp) in _kandidatPunkt)
            {
                _zeiger[k] = kp;
            }
            _kandidatPunkt.Clear();
            _sticks.KandidatenVergessen();
        }
        _zeiger[id] = p;
        if (_zeiger.Count == 1)
        {
            _druckStart = p;
            _gezogen = false;
        }
        else if (_zeiger.Count == 2)
        {
            _abstandZwei = Abstand();
            _winkelZwei = Winkel();
            _mitteZwei = MitteY();
            _gezogen = true;
        }
    }

    /// <summary>Finger bewegt sich.</summary>
    public void FingerBewegt(long id, Point p)
    {
        if (_sticks.IstStick(id) || _kandidatPunkt.ContainsKey(id))
        {
            if (_kandidatPunkt.ContainsKey(id))
            {
                _kandidatPunkt[id] = p;
            }
            if (_sticks.Bewegen(id, p.X, p.Y) is not null)
            {
                _kandidatPunkt.Remove(id);
                _gezogen = true;
                SticksGeaendert?.Invoke();
                AnfordernBild();
            }
            return;
        }
        if (!_zeiger.TryGetValue(id, out var vorher))
        {
            return;
        }
        _zeiger[id] = p;
        if (AktuelleKamera is not { } kamera)
        {
            return;
        }
        if (_zeiger.Count >= 2)
        {
            // zwei Finger: Abstand = Zoom, Winkel = Drehen um die Hochachse; Welle A 0.80.0: draussen neigt die Mitte der
            // Finger (gemeinsam hoch oder runter), der Zoom bleibt im Bereich 7 bis 9 (Kamerabedienung)
            var neu = Abstand();
            var faktor = _abstandZwei > 1 && neu > 1 ? neu / _abstandZwei : 0;
            _abstandZwei = neu;
            var winkel = Winkel();
            var dreh = winkel - _winkelZwei;
            dreh = dreh > 180 ? dreh - 360 : dreh < -180 ? dreh + 360 : dreh;
            _winkelZwei = winkel;
            var mitte = MitteY();
            var fest = KameraFest;
            Kamerabedienung.ZweiFinger(kamera, fest, faktor, dreh, mitte - _mitteZwei);
            _mitteZwei = mitte;
            if (!fest)
            {
                _folgenAb = _uhr.Elapsed.TotalSeconds + 3;
            }
            AnfordernBild();
            return;
        }
        if (!_gezogen && Math.Abs(p.X - _druckStart.X) + Math.Abs(p.Y - _druckStart.Y) > ZiehenAb)
        {
            _gezogen = true;
        }
        if (_gezogen && Kamerabedienung.Ziehen(kamera, KameraFest, p.X - vorher.X, p.Y - vorher.Y, Bounds.Height))
        {
            // Welle A 0.80.0: draussen verschiebt ein Finger nichts mehr (Nico Antwort 3), drinnen wie bisher
            _folgenAb = _uhr.Elapsed.TotalSeconds + 3;
            AnfordernBild();
        }
    }

    /// <summary>Finger hebt ab: ein Stick verschwindet; ein Tipp ohne Ziehen geht wie heute (nie, solange ein Stick da ist).</summary>
    public void FingerAb(long id, Point p)
    {
        if (_sticks.Loslassen(id))
        {
            SticksGeaendert?.Invoke();
            AnfordernBild();
            return;
        }
        var warKandidat = _kandidatPunkt.Remove(id);
        var war = _zeiger.Remove(id) || warKandidat;
        if (war && _zeiger.Count == 0 && _kandidatPunkt.Count == 0 && !_gezogen && !_sticks.Aktiv)
        {
            Tippen(p);
        }
    }

    /// <summary>Finger verloren (ohne Loslassen): nie ein Tipp.</summary>
    public void FingerWeg(long id)
    {
        _zeiger.Remove(id);
        _kandidatPunkt.Remove(id);
        if (_sticks.Loslassen(id))
        {
            SticksGeaendert?.Invoke();
            AnfordernBild();
        }
    }

    /// <summary>Alle Sticks und Kandidaten los (Fokus verloren, Spiel-Handy offen, Seite weg, Spiel gewechselt).</summary>
    public void SticksLos()
    {
        foreach (var id in _kandidatPunkt.Keys)
        {
            _zeiger.Remove(id);
        }
        _kandidatPunkt.Clear();
        if (_sticks.Aktiv || _sticks.Kandidaten > 0)
        {
            _sticks.AlleLos();
            SticksGeaendert?.Invoke();
            AnfordernBild();
        }
    }

    /// <summary>Je Bild: ist etwas ueber die Flaeche gekommen, verschwinden die Sticks; der rechte Stick dreht.</summary>
    private void SticksTakt(Kamera kamera, double dt)
    {
        if (!_sticks.Aktiv)
        {
            return;
        }
        if (!SticksMoeglich)
        {
            _kandidatPunkt.Clear();
            _sticks.AlleLos();
            SticksGeaendert?.Invoke();
            return;
        }
        // Welle A 1.6.0 (Sammelliste 446): Regler „Empfindlichkeit rechter Stick“ (Vorgabe = unveraendert)
        var (dx, dy) = Empfindlichkeit.Umsehen(_sticks.Drehen(dt));
        if (Math.Abs(dx) + Math.Abs(dy) > 1e-9)
        {
            // wie die rechte Maustaste: waagrecht drehen, senkrecht neigen (draussen und in der Ich-Sicht)
            Kamerabedienung.RechteMaus(kamera, KameraFest, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: dy, Grad: (float)dx * Kameragriff.GradJePunkt));
        }
        // solange ein Stick gehalten wird, laeuft die Bildschleife weiter
        AnfordernBild();
    }
}
