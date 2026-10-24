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

    private readonly HashSet<(int, int)> _frei;

    public Wegsuche(IEnumerable<(int Spalte, int Zeile)> frei, double schritt)
    {
        _frei = [.. frei];
        Schritt = schritt;
    }

    /// <summary>Kantenlaenge einer Zelle in Karteneinheiten (WALK_STEP).</summary>
    public double Schritt { get; }

    public int AnzahlFrei => _frei.Count;

    public (int Spalte, int Zeile) Zelle(double x, double y) => ((int)(x / Schritt), (int)(y / Schritt));

    public (double X, double Y) Mitte((int Spalte, int Zeile) zelle) => ((zelle.Spalte + 0.5) * Schritt, (zelle.Zeile + 0.5) * Schritt);

    public bool IstFrei((int, int) zelle) => _frei.Contains(zelle);

    public bool KannStehen(double x, double y) => _frei.Contains(Zelle(x, y));

    public (int Spalte, int Zeile)? NaechsteFreie(double x, double y)
    {
        var start = Zelle(x, y);
        if (_frei.Contains(start))
        {
            return start;
        }
        (int, int)? beste = null;
        long besteWeite = long.MaxValue;
        foreach (var zelle in _frei)
        {
            long dx = zelle.Item1 - start.Spalte, dy = zelle.Item2 - start.Zeile;
            var weite = (dx * dx) + (dy * dy);
            if (weite < besteWeite || (weite == besteWeite && beste is { } b && zelle.CompareTo(b) < 0))
            {
                beste = zelle;
                besteWeite = weite;
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
            if (!_frei.Contains(Zelle(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t))))
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
        var vorher = new Dictionary<(int, int), (int, int)?> { [von] = null };
        var reihe = new List<(int, int)> { von };
        (int, int)? gefunden = null;
        for (var i = 0; i < reihe.Count; i++)   // Breitensuche (die Liste waechst mit)
        {
            var zelle = reihe[i];
            if (zelle == nach)
            {
                gefunden = zelle;
                break;
            }
            foreach (var (dx, dy) in Nachbarn)
            {
                var naechste = (zelle.Item1 + dx, zelle.Item2 + dy);
                if (!_frei.Contains(naechste) || vorher.ContainsKey(naechste))
                {
                    continue;
                }
                // diagonal nur, wenn beide Nachbarn frei sind (keine Ecken schneiden)
                if (dx != 0 && dy != 0 && (!_frei.Contains((zelle.Item1 + dx, zelle.Item2)) || !_frei.Contains((zelle.Item1, zelle.Item2 + dy))))
                {
                    continue;
                }
                vorher[naechste] = zelle;
                reihe.Add(naechste);
            }
        }
        if (gefunden is null)
        {
            return [];
        }
        var zellen = new List<(int, int)>();
        for (var z = gefunden; z is { } jetzt; z = vorher[jetzt])
        {
            zellen.Add(jetzt);
        }
        zellen.Reverse();
        var punkte = zellen.Select(Mitte).ToList();
        if (KannStehen(ziel.X, ziel.Y))
        {
            punkte[^1] = ziel;
        }
        // Glaetten: Zwischenpunkte weglassen, solange die gerade Strecke frei ist
        var glatt = new List<(double X, double Y)> { punkte[0] };
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
        return glatt.Skip(1).ToList();
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
