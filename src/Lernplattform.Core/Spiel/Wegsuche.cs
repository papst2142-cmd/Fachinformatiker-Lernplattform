namespace Lernplattform.Core.Spiel;

/// <summary>
/// Wegsuche auf einem Raster wie fisi_game.walk_path (Python 0.63.2, Z. 11680): Breitensuche
/// ueber 8 Nachbarn (diagonal nur, wenn beide Nachbarzellen frei sind), danach Glaetten,
/// solange die gerade Strecke nur ueber freie Zellen geht. Zellen sind (Spalte, Zeile),
/// Punkte in Karteneinheiten. Phase 7 W1 (Stichprobe) nutzt sie fuer Stadt und Innenraum;
/// der Vergleich mit Python (G48) folgt in W2. Offen fuer G48: Python waehlt bei gleich
/// weiten naechsten freien Zellen die erste in der Reihenfolge seiner Menge (set); hier
/// gewinnt die kleinste (Spalte, Zeile).
/// </summary>
public sealed class Wegsuche
{
    private static readonly (int Dx, int Dy)[] Nachbarn =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    // Weisung P7-16: Raster als Feld statt Menge, Suche mit wiederverwendeten Puffern (keine neuen
    // Listen je Schritt). Ergebnis wie vorher: gleiche Nachbar-Reihenfolge, gleiche Breitensuche.
    private readonly bool[] _frei;
    private readonly int _spalte0, _zeile0, _spalten, _zeilen;
    private readonly int[] _vorher, _marke, _reihe;
    private int _runde;
    private readonly object _sperre = new();

    public Wegsuche(IEnumerable<(int Spalte, int Zeile)> frei, double schritt)
    {
        var liste = frei.ToList();
        Schritt = schritt;
        AnzahlFrei = liste.Distinct().Count();
        if (liste.Count == 0)
        {
            _frei = [];
            _vorher = _marke = _reihe = [];
            return;
        }
        _spalte0 = liste.Min(z => z.Spalte);
        _zeile0 = liste.Min(z => z.Zeile);
        _spalten = liste.Max(z => z.Spalte) - _spalte0 + 1;
        _zeilen = liste.Max(z => z.Zeile) - _zeile0 + 1;
        _frei = new bool[_spalten * _zeilen];
        foreach (var (spalte, zeile) in liste)
        {
            _frei[Index(spalte, zeile)] = true;
        }
        _vorher = new int[_frei.Length];
        _marke = new int[_frei.Length];
        _reihe = new int[_frei.Length];
    }

    /// <summary>Kantenlaenge einer Zelle in Karteneinheiten (WALK_STEP).</summary>
    public double Schritt { get; }

    public int AnzahlFrei { get; }

    private int Index(int spalte, int zeile) => ((spalte - _spalte0) * _zeilen) + (zeile - _zeile0);

    private bool Frei(int spalte, int zeile) =>
        (uint)(spalte - _spalte0) < (uint)_spalten && (uint)(zeile - _zeile0) < (uint)_zeilen && _frei[Index(spalte, zeile)];

    public (int Spalte, int Zeile) Zelle(double x, double y) => ((int)(x / Schritt), (int)(y / Schritt));

    public (double X, double Y) Mitte((int Spalte, int Zeile) zelle) => ((zelle.Spalte + 0.5) * Schritt, (zelle.Zeile + 0.5) * Schritt);

    public bool IstFrei((int, int) zelle) => Frei(zelle.Item1, zelle.Item2);

    public bool KannStehen(double x, double y)
    {
        var (spalte, zeile) = Zelle(x, y);
        return Frei(spalte, zeile);
    }

    public (int Spalte, int Zeile)? NaechsteFreie(double x, double y)
    {
        var start = Zelle(x, y);
        if (Frei(start.Spalte, start.Zeile))
        {
            return start;
        }
        // naechste freie Zelle; bei gleichem Abstand die kleinste (Spalte, Zeile): Spalten und Zeilen
        // aufsteigend, nur ein echt kleinerer Abstand ersetzt
        (int, int)? beste = null;
        long besteWeite = long.MaxValue;
        for (var s = 0; s < _spalten; s++)
        {
            long dx = s + _spalte0 - start.Spalte;
            for (var z = 0; z < _zeilen; z++)
            {
                if (!_frei[(s * _zeilen) + z])
                {
                    continue;
                }
                long dy = z + _zeile0 - start.Zeile;
                var weite = (dx * dx) + (dy * dy);
                if (weite < besteWeite)
                {
                    beste = (s + _spalte0, z + _zeile0);
                    besteWeite = weite;
                }
            }
        }
        return beste;
    }

    /// <summary>Geht die gerade Strecke a -> b nur ueber freie Zellen? (_line_free)</summary>
    public bool StreckeFrei((double X, double Y) a, (double X, double Y) b)
    {
        var schritte = (int)(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) / (Schritt / 3.0)) + 1;
        for (var i = 0; i <= schritte; i++)
        {
            var t = i / (double)schritte;
            if (!KannStehen(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Weg von <paramref name="start"/> zu <paramref name="ziel"/> als Punkte ohne den Startpunkt.
    /// Leer = schon da oder nicht erreichbar.
    /// </summary>
    public IReadOnlyList<(double X, double Y)> Weg((double X, double Y) start, (double X, double Y) ziel)
    {
        var beginn = NaechsteFreie(start.X, start.Y);
        var zielZelle = NaechsteFreie(ziel.X, ziel.Y);
        if (beginn is not { } von || zielZelle is not { } nach || von == nach)
        {
            return [];
        }
        List<(double X, double Y)> punkte;
        lock (_sperre)
        {
            if (++_runde == int.MaxValue)
            {
                Array.Clear(_marke);
                _runde = 1;
            }
            int anfang = Index(von.Item1, von.Item2), ende = Index(nach.Item1, nach.Item2);
            _marke[anfang] = _runde;
            _vorher[anfang] = -1;
            _reihe[0] = anfang;
            int lesen = 0, schreiben = 1;
            var gefunden = false;
            while (lesen < schreiben)   // Breitensuche
            {
                var i = _reihe[lesen++];
                if (i == ende)
                {
                    gefunden = true;
                    break;
                }
                int spalte = (i / _zeilen) + _spalte0, zeile = (i % _zeilen) + _zeile0;
                foreach (var (dx, dy) in Nachbarn)
                {
                    int ns = spalte + dx, nz = zeile + dy;
                    if (!Frei(ns, nz))
                    {
                        continue;
                    }
                    var n = Index(ns, nz);
                    if (_marke[n] == _runde)
                    {
                        continue;
                    }
                    // diagonal nur, wenn beide Nachbarn frei sind (keine Ecken schneiden)
                    if (dx != 0 && dy != 0 && (!Frei(spalte + dx, zeile) || !Frei(spalte, zeile + dy)))
                    {
                        continue;
                    }
                    _marke[n] = _runde;
                    _vorher[n] = i;
                    _reihe[schreiben++] = n;
                }
            }
            if (!gefunden)
            {
                return [];
            }
            var anzahl = 0;
            for (var i = ende; i >= 0; i = _vorher[i])
            {
                anzahl++;
            }
            var feld = new (double X, double Y)[anzahl];
            var k = anzahl;
            for (var i = ende; i >= 0; i = _vorher[i])
            {
                feld[--k] = Mitte(((i / _zeilen) + _spalte0, (i % _zeilen) + _zeile0));
            }
            punkte = [.. feld];
        }
        if (KannStehen(ziel.X, ziel.Y))
        {
            punkte[^1] = ziel;
        }
        // Glaetten: Zwischenpunkte weglassen, solange die gerade Strecke frei ist
        var glatt = new List<(double X, double Y)>();
        var index = 0;
        while (index < punkte.Count - 1)
        {
            var weiter = punkte.Count - 1;
            while (weiter > index + 1 && !StreckeFrei(punkte[index], punkte[weiter]))
            {
                weiter--;
            }
            glatt.Add(punkte[weiter]);
            index = weiter;
        }
        return glatt;
    }

    /// <summary>Laenge eines Wegs ab <paramref name="start"/> in Karteneinheiten.</summary>
    public static double Laenge((double X, double Y) start, IReadOnlyList<(double X, double Y)> weg)
    {
        var summe = 0.0;
        var von = start;
        foreach (var p in weg)
        {
            summe += Math.Sqrt(((p.X - von.X) * (p.X - von.X)) + ((p.Y - von.Y) * (p.Y - von.Y)));
            von = p;
        }
        return summe;
    }

    /// <summary>
    /// Freie Zellen eines Rasters <paramref name="breite"/> x <paramref name="hoehe"/>: frei ist eine
    /// Zelle, deren Mitte mindestens <paramref name="koerper"/> von jedem Hindernis entfernt ist
    /// (wie _base_grid mit BODY_RADIUS).
    /// </summary>
    public static IEnumerable<(int, int)> FreieZellen(double breite, double hoehe, double schritt, double koerper,
        IReadOnlyList<(double X1, double Y1, double X2, double Y2)> hindernisse)
    {
        var spalten = (int)(breite / schritt);
        var zeilen = (int)(hoehe / schritt);
        for (var s = 0; s < spalten; s++)
        {
            var cx = (s + 0.5) * schritt;
            for (var z = 0; z < zeilen; z++)
            {
                var cy = (z + 0.5) * schritt;
                var frei = true;
                foreach (var (x1, y1, x2, y2) in hindernisse)
                {
                    var dx = Math.Max(Math.Max(x1 - cx, 0), cx - x2);
                    var dy = Math.Max(Math.Max(y1 - cy, 0), cy - y2);
                    if ((dx * dx) + (dy * dy) < koerper * koerper)
                    {
                        frei = false;
                        break;
                    }
                }
                if (frei)
                {
                    yield return (s, z);
                }
            }
        }
    }
}
