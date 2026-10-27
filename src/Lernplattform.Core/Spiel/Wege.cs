using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: ein Ort zum Laufen wie das content-dict, das <c>fisi_game.walk_path</c> bekommt:
/// Grundriss (<c>gebaeude</c>) und die Personen dort (<c>kollegen</c>, mit "platz"). Das Raster
/// wird je Ort einmal berechnet (Python: _GRID_CACHE und _PEOPLE_CACHE).
/// </summary>
public sealed class LaufOrt(PyObjekt gebaeude, List<object?> personen)
{
    private PyMenge? basis;
    private PyMenge? frei;

    public PyObjekt Gebaeude { get; } = gebaeude;

    public List<object?> Personen { get; } = personen;

    /// <summary>_base_grid: begehbare Zellen ohne Personen.</summary>
    public PyMenge Basis => basis ??= Wege.BasisRaster(Gebaeude);

    /// <summary>_walk_grid: begehbare Zellen, Personen stehen im Weg.</summary>
    public PyMenge Frei => frei ??= Wege.LaufRaster(Basis, Personen);

    private FreiFeld? feld;

    /// <summary>Weisung P7-16: <see cref="Frei"/> als Feld fuer die Breitensuche (gleiche Zellen, schneller).</summary>
    internal FreiFeld Feld => feld ??= new FreiFeld(Frei);
}

/// <summary>
/// Weisung P7-16: die begehbaren Zellen eines Orts als Feld mit wiederverwendeten Puffern fuer die
/// Breitensuche (keine neuen Listen je Schritt). Die Reihenfolge der Suche bleibt die von walk_path.
/// </summary>
internal sealed class FreiFeld
{
    private readonly bool[] _frei;
    private readonly long _spalte0, _zeile0;
    private readonly int _spalten, _zeilen;

    public FreiFeld(PyMenge frei)
    {
        if (frei.Count == 0)
        {
            _frei = [];
            Vorher = Marke = Reihe = Ziel = [];
            return;
        }
        _spalte0 = frei.Min(z => z.Spalte);
        _zeile0 = frei.Min(z => z.Zeile);
        _spalten = checked((int)(frei.Max(z => z.Spalte) - _spalte0 + 1));
        _zeilen = checked((int)(frei.Max(z => z.Zeile) - _zeile0 + 1));
        _frei = new bool[checked(_spalten * _zeilen)];
        foreach (var (spalte, zeile) in frei)
        {
            _frei[Index(spalte, zeile)] = true;
        }
        Vorher = new int[_frei.Length];
        Marke = new int[_frei.Length];
        Reihe = new int[_frei.Length];
        Ziel = new int[_frei.Length];
    }

    public int[] Vorher { get; }

    public int[] Marke { get; }

    public int[] Reihe { get; }

    /// <summary>Marke der Zielzellen dieser Suche.</summary>
    public int[] Ziel { get; }

    public int Runde { get; private set; }

    public object Sperre { get; } = new();

    public int NeueRunde()
    {
        if (++Runde == int.MaxValue)
        {
            Array.Clear(Marke);
            Array.Clear(Ziel);
            Runde = 1;
        }
        return Runde;
    }

    public int Index(long spalte, long zeile) => (int)(((spalte - _spalte0) * _zeilen) + (zeile - _zeile0));

    public bool Frei(long spalte, long zeile) =>
        (ulong)(spalte - _spalte0) < (ulong)_spalten && (ulong)(zeile - _zeile0) < (ulong)_zeilen && _frei[Index(spalte, zeile)];

    public (long, long) Zelle(int index) => ((index / _zeilen) + _spalte0, (index % _zeilen) + _zeile0);
}

/// <summary>
/// Phase 7, W2: Laufwege der Spielfigur wie <c>fisi_game.py</c> (Python 0.63.2, Z. 11532-11840):
/// Raster mit Schrittweite 0,25, Wandstuecke mit Tueroeffnungen, Breitensuche mit acht
/// Richtungen und Glaetten. Bei gleichem Abstand entscheidet die Reihenfolge der Python-Menge
/// (<see cref="PyMenge"/>). Vergleichswerte G48 (<c>spiel_wege.json</c>).
/// </summary>
public static class Wege
{
    public const double Schritt = 0.25;       // WALK_STEP
    public const double Koerper = 0.3;        // BODY_RADIUS
    public const double PersonRadius = 0.5;   // PERSON_RADIUS
    public const double Reichweite = 1.3;     // REACH
    public const double WandInnen = 0.16;     // WALL_INNER

    /// <summary>WALK_FREE: darueber laeuft man (oder es haengt an der Wand).</summary>
    public static readonly IReadOnlyList<string> Begehbar =
        ["teppich", "fussmatte", "whiteboard", "markierung", "bild", "wimpel", "kamera"];

    private static readonly (int, int)[] Richtungen = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>door_side: "s", "n" oder per "seite" "w"/"o".</summary>
    public static string TuerSeite(PyObjekt raum, PyObjekt gebaeude)
    {
        var tuer = Py.Wahr(raum["tuer"]) ? (PyObjekt)raum["tuer"]! : new PyObjekt();
        if (Py.Wahr(tuer["seite"]))
        {
            return (string)tuer["seite"]!;
        }
        var flur = (PyObjekt)gebaeude["flur"]!;
        return Py.F(raum["y"]) + Py.F(raum["h"]) <= Py.F(flur["y"]) ? "s" : "n";
    }

    /// <summary>_segments: Wandstuecke als Rechtecke (x1, y1, x2, y2), Tueroeffnungen ausgespart.</summary>
    public static List<(double, double, double, double)> Wandstuecke(PyObjekt gebaeude)
    {
        var breite = Py.F(gebaeude["breite"]);
        var hoehe = Py.F(gebaeude["hoehe"]);
        var halb = WandInnen / 2.0;
        var waende = new List<(double, double, double, double)>();

        void Waagerecht(double x1, double x2, double y, (double, double)? luecke)
        {
            if (luecke is var (von, bis))
            {
                waende.Add((x1, y - halb, von, y + halb));
                waende.Add((bis, y - halb, x2, y + halb));
            }
            else
            {
                waende.Add((x1, y - halb, x2, y + halb));
            }
        }

        void Senkrecht(double x, double y1, double y2, (double, double)? luecke)
        {
            if (luecke is var (von, bis))
            {
                waende.Add((x - halb, y1, x + halb, von));
                waende.Add((x - halb, bis, x + halb, y2));
            }
            else
            {
                waende.Add((x - halb, y1, x + halb, y2));
            }
        }

        foreach (var eintrag in Liste(gebaeude["raeume"]))
        {
            var raum = (PyObjekt)eintrag!;
            double x = Py.F(raum["x"]), y = Py.F(raum["y"]), w = Py.F(raum["w"]), h = Py.F(raum["h"]);
            (double, double)? luecke = Py.Wahr(raum["tuer"])
                ? (Py.F(((PyObjekt)raum["tuer"]!)["von"]), Py.F(((PyObjekt)raum["tuer"]!)["bis"]))
                : null;
            var seite = TuerSeite(raum, gebaeude);
            Waagerecht(x, x + w, y, seite == "n" ? luecke : null);
            Waagerecht(x, x + w, y + h, seite == "s" ? luecke : null);
            Senkrecht(x, y, y + h, seite == "w" ? luecke : null);
            Senkrecht(x + w, y, y + h, seite == "o" ? luecke : null);
        }
        waende.Add((0, -1, breite, 0.15));
        waende.Add((0, hoehe - 0.15, breite, hoehe + 1));
        waende.Add((-1, 0, 0.15, hoehe));
        waende.Add((breite - 0.15, 0, breite + 1, hoehe));
        return waende;
    }

    /// <summary>_obstacles: Wandstuecke und Einrichtung (ohne <see cref="Begehbar"/>).</summary>
    public static List<(double, double, double, double)> Hindernisse(PyObjekt gebaeude)
    {
        var rechtecke = Wandstuecke(gebaeude);
        var deko = Liste(gebaeude["raeume"]).SelectMany(r => DekoListe((PyObjekt)r!)).ToList();
        deko.AddRange(DekoListe((PyObjekt)gebaeude["flur"]!));
        foreach (var d in deko)
        {
            if (Begehbar.Contains((string)d["typ"]!))
            {
                continue;
            }
            double x = Py.F(d["x"]), y = Py.F(d["y"]);
            rechtecke.Add((x, y, x + Py.F(d["w"]), y + Py.F(d["h"])));
        }
        return rechtecke;
    }

    private static IEnumerable<PyObjekt> DekoListe(PyObjekt d) =>
        Py.Hat(d, "deko") ? Liste(d["deko"]).Select(e => (PyObjekt)e!) : [];

    /// <summary>
    /// _base_grid. Weisung P8-6 (109): jedes Rechteck prueft nur die Zellen, deren Mitte naeher als
    /// <see cref="Koerper"/> (plus eine Zelle Rand) liegt; weiter weg ist dx oder dy mindestens Koerper und
    /// die Zelle nie gesperrt. Die Pruefung je Zelle und Rechteck ist dieselbe Rechnung wie vorher, die
    /// Zellen kommen in derselben Reihenfolge in die Menge (gleiche Reihenfolge wie Python, G48).
    /// </summary>
    public static PyMenge BasisRaster(PyObjekt gebaeude)
    {
        var spalten = (long)(Py.F(gebaeude["breite"]) / Schritt);
        var zeilen = (long)(Py.F(gebaeude["hoehe"]) / Schritt);
        var rechtecke = Hindernisse(gebaeude);
        var grenze = Koerper * Koerper;
        var gesperrt = new bool[Math.Max(0, spalten) * Math.Max(0, zeilen)];
        foreach (var (x1, y1, x2, y2) in rechtecke)
        {
            var (s1, s2) = Bereich(x1, x2, spalten);
            var (z1, z2) = Bereich(y1, y2, zeilen);
            for (var spalte = s1; spalte <= s2; spalte++)
            {
                var cx = (spalte + 0.5) * Schritt;
                for (var zeile = z1; zeile <= z2; zeile++)
                {
                    var index = spalte * zeilen + zeile;
                    if (gesperrt[index])
                    {
                        continue;
                    }
                    var cy = (zeile + 0.5) * Schritt;
                    var dx = Math.Max(Math.Max(x1 - cx, 0), cx - x2);
                    var dy = Math.Max(Math.Max(y1 - cy, 0), cy - y2);
                    if (dx * dx + dy * dy < grenze)
                    {
                        gesperrt[index] = true;
                    }
                }
            }
        }
        var frei = new PyMenge();
        for (long spalte = 0; spalte < spalten; spalte++)
        {
            for (long zeile = 0; zeile < zeilen; zeile++)
            {
                if (!gesperrt[spalte * zeilen + zeile])
                {
                    frei.Add((spalte, zeile));
                }
            }
        }
        return frei;
    }

    /// <summary>
    /// Zellen 0 bis anzahl-1, deren Mitte von [a, b] weniger als <see cref="Koerper"/> entfernt sein kann
    /// (eine Zelle Rand mehr, damit Rundung nichts auslaesst). Leer: (1, 0).
    /// </summary>
    private static (long Von, long Bis) Bereich(double a, double b, long anzahl)
    {
        var von = Math.Floor((a - Koerper) / Schritt) - 1;
        var bis = Math.Ceiling((b + Koerper) / Schritt) + 1;
        if (double.IsNaN(von) || double.IsNaN(bis))
        {
            return (0, anzahl - 1);   // nie bei echten Inhalten: dann wie vorher jede Zelle pruefen
        }
        return ((long)Math.Max(von, 0), (long)Math.Min(bis, anzahl - 1));
    }

    /// <summary>_walk_grid (ohne Zwischenspeicher): Zellen um jede Person sind gesperrt.</summary>
    public static PyMenge LaufRaster(PyMenge basis, List<object?> personen)
    {
        var reichweite = (long)(PersonRadius / Schritt) + 1;
        var grenze = PythonPow.Pow(PersonRadius, 2);
        var gesperrt = new HashSet<(long, long)>();
        foreach (var person in personen)
        {
            if (Platz(person) is not var (px, py))
            {
                continue;
            }
            var (spalte0, zeile0) = Zelle(px, py);
            for (var spalte = spalte0 - reichweite; spalte <= spalte0 + reichweite; spalte++)
            {
                for (var zeile = zeile0 - reichweite; zeile <= zeile0 + reichweite; zeile++)
                {
                    double cx = (spalte + 0.5) * Schritt, cy = (zeile + 0.5) * Schritt;
                    if (PythonPow.Pow(px - cx, 2) + PythonPow.Pow(py - cy, 2) < grenze)
                    {
                        gesperrt.Add((spalte, zeile));
                    }
                }
            }
        }
        return gesperrt.Count > 0 ? basis.Ohne(gesperrt) : basis;
    }

    /// <summary>"platz" einer Person, null wenn er fehlt oder leer ist.</summary>
    public static (double, double)? Platz(object? person)
    {
        var platz = ((PyObjekt)person!)["platz"];
        return Py.Wahr(platz) ? (Py.F(((List<object?>)platz!)[0]), Py.F(((List<object?>)platz!)[1])) : null;
    }

    /// <summary>_cell</summary>
    public static (long, long) Zelle(double x, double y) => ((long)(x / Schritt), (long)(y / Schritt));

    /// <summary>_center</summary>
    public static (double, double) Mitte((long, long) zelle) => ((zelle.Item1 + 0.5) * Schritt, (zelle.Item2 + 0.5) * Schritt);

    /// <summary>can_stand</summary>
    public static bool KannStehen(double x, double y, LaufOrt ort) => ort.Frei.Contains(Zelle(x, y));

    /// <summary>_nearest_free: Zelle des Punkts oder die naechste freie (bei Gleichstand die erste der Menge).</summary>
    public static (long, long)? NaechsteFrei(double x, double y, PyMenge frei)
    {
        var start = Zelle(x, y);
        if (frei.Contains(start))
        {
            return start;
        }
        (long, long)? beste = null;
        long besterAbstand = 0;
        foreach (var zelle in frei)
        {
            // int ** 2 in Python: ganze Zahl, exakt
            long ds = zelle.Spalte - start.Item1, dz = zelle.Zeile - start.Item2;
            var abstand = checked(ds * ds + dz * dz);
            if (beste is null || abstand < besterAbstand)
            {
                beste = zelle;
                besterAbstand = abstand;
            }
        }
        return beste;
    }

    /// <summary>_line_free: geht die gerade Strecke a -> b nur ueber begehbare Zellen?</summary>
    public static bool StreckeFrei((double, double) a, (double, double) b, PyMenge frei)
    {
        var schritte = (long)(Math.Max(Math.Abs(b.Item1 - a.Item1), Math.Abs(b.Item2 - a.Item2)) / (Schritt / 3.0)) + 1;
        for (long i = 0; i <= schritte; i++)
        {
            var t = i / (double)schritte;
            if (!frei.Contains(Zelle(a.Item1 + (b.Item1 - a.Item1) * t, a.Item2 + (b.Item2 - a.Item2) * t)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Wie <see cref="StreckeFrei((double, double), (double, double), PyMenge)"/> ueber das Feld derselben Zellen (Weisung P8-6, 109).</summary>
    internal static bool StreckeFrei((double, double) a, (double, double) b, FreiFeld frei)
    {
        var schritte = (long)(Math.Max(Math.Abs(b.Item1 - a.Item1), Math.Abs(b.Item2 - a.Item2)) / (Schritt / 3.0)) + 1;
        for (long i = 0; i <= schritte; i++)
        {
            var t = i / (double)schritte;
            var (spalte, zeile) = Zelle(a.Item1 + (b.Item1 - a.Item1) * t, a.Item2 + (b.Item2 - a.Item2) * t);
            if (!frei.Frei(spalte, zeile))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// walk_path: Weg von start zu ziel als Liste von Punkten (ohne den Startpunkt). reichweite &gt; 0:
    /// es genuegt, bis auf diesen Abstand heranzukommen. Leer = schon da oder nicht erreichbar.
    /// </summary>
    public static List<(double, double)> Weg((double, double) start, (double, double) ziel, double reichweite, LaufOrt ort)
    {
        var frei = ort.Frei;
        var anfang = NaechsteFrei(start.Item1, start.Item2, frei);
        var ziele = new HashSet<(long, long)>();
        if (reichweite > 0)
        {
            var grenze = reichweite * reichweite;
            // Weisung P8-6 (109): liegt die Mitte in x oder y deutlich weiter als reichweite weg, ist ihr Quadrat
            // sicher groesser als grenze; nur die anderen Zellen rechnen wie vorher (PythonPow, gleiches Ergebnis)
            var schranke = reichweite * (1 + 1e-9);
            foreach (var zelle in frei)
            {
                var m = Mitte(zelle);
                double dx = m.Item1 - ziel.Item1, dy = m.Item2 - ziel.Item2;
                if (Math.Abs(dx) > schranke || Math.Abs(dy) > schranke)
                {
                    continue;
                }
                if (PythonPow.Pow(dx, 2) + PythonPow.Pow(dy, 2) <= grenze)
                {
                    ziele.Add(zelle);
                }
            }
        }
        else if (NaechsteFrei(ziel.Item1, ziel.Item2, frei) is { } zielZelle)
        {
            ziele.Add(zielZelle);
        }
        if (anfang is not { } beginn || ziele.Count == 0 || ziele.Contains(beginn))
        {
            return [];
        }
        // Breitensuche ueber das Feld (Weisung P7-16), Reihenfolge wie walk_path
        var feld = ort.Feld;
        var zellen = new List<(long, long)>();
        lock (feld.Sperre)
        {
            var runde = feld.NeueRunde();
            foreach (var zelle in ziele)
            {
                feld.Ziel[feld.Index(zelle.Item1, zelle.Item2)] = runde;
            }
            var erste = feld.Index(beginn.Item1, beginn.Item2);
            feld.Marke[erste] = runde;
            feld.Vorher[erste] = -1;
            feld.Reihe[0] = erste;
            int lesen = 0, schreiben = 1, gefunden = -1;
            while (lesen < schreiben)
            {
                var i = feld.Reihe[lesen++];
                if (feld.Ziel[i] == runde)
                {
                    gefunden = i;
                    break;
                }
                var (spalte, zeile) = feld.Zelle(i);
                foreach (var (dx, dy) in Richtungen)
                {
                    long ns = spalte + dx, nz = zeile + dy;
                    if (!feld.Frei(ns, nz))
                    {
                        continue;
                    }
                    var n = feld.Index(ns, nz);
                    if (feld.Marke[n] == runde)
                    {
                        continue;
                    }
                    // diagonal nur, wenn beide Nachbarn frei sind (keine Ecken schneiden)
                    if (dx != 0 && dy != 0 && (!feld.Frei(spalte + dx, zeile) || !feld.Frei(spalte, zeile + dy)))
                    {
                        continue;
                    }
                    feld.Marke[n] = runde;
                    feld.Vorher[n] = i;
                    feld.Reihe[schreiben++] = n;
                }
            }
            if (gefunden < 0)
            {
                return [];
            }
            for (var i = gefunden; i >= 0; i = feld.Vorher[i])
            {
                zellen.Add(feld.Zelle(i));
            }
        }
        zellen.Reverse();
        var punkte = zellen.Select(Mitte).ToList();
        if (reichweite <= 0 && KannStehen(ziel.Item1, ziel.Item2, ort))
        {
            punkte[^1] = ziel;
        }
        // Glaetten: Zwischenpunkte weglassen, solange die gerade Strecke frei ist
        var glatt = new List<(double, double)> { punkte[0] };
        var index = 0;
        while (index < punkte.Count - 1)
        {
            var weiter = punkte.Count - 1;
            while (weiter > index + 1 && !StreckeFrei(punkte[index], punkte[weiter], feld))
            {
                weiter--;
            }
            glatt.Add(punkte[weiter]);
            index = weiter;
        }
        return glatt.Skip(1).ToList();
    }

    /// <summary>person_at: Person an der Stelle (x, y) oder null.</summary>
    public static PyObjekt? PersonBei(double x, double y, LaufOrt ort, double radius = 0.7)
    {
        foreach (var person in ort.Personen)
        {
            if (Platz(person) is var (px, py) && PythonPow.Pow(px - x, 2) + PythonPow.Pow(py - y, 2) <= radius * radius)
            {
                return (PyObjekt)person!;
            }
        }
        return null;
    }

    /// <summary>person_near: Person in Reichweite der Spielfigur (zum Ansprechen) oder null.</summary>
    public static PyObjekt? PersonNahe(double x, double y, LaufOrt ort) => PersonBei(x, y, ort, Reichweite + 0.15);

    /// <summary>start_position</summary>
    public static (double, double) StartPlatz(PyObjekt gebaeude)
    {
        var flur = (PyObjekt)gebaeude["flur"]!;
        if (!Py.Hat(flur, "spieler"))
        {
            return (2.4, 7.0);
        }
        var p = (List<object?>)flur["spieler"]!;
        return (Py.F(p[0]), Py.F(p[1]));
    }

    /// <summary>room_at: Raum an der Stelle (x, y), der Flur ist kein Raum.</summary>
    public static PyObjekt? RaumBei(double x, double y, PyObjekt gebaeude)
    {
        foreach (var eintrag in Liste(gebaeude["raeume"]))
        {
            var r = (PyObjekt)eintrag!;
            double rx = Py.F(r["x"]), ry = Py.F(r["y"]);
            if (rx <= x && x < rx + Py.F(r["w"]) && ry <= y && y < ry + Py.F(r["h"]))
            {
                return r;
            }
        }
        return null;
    }

    private static List<object?> Liste(object? wert) => wert as List<object?> ?? throw new InvalidDataException("Liste erwartet");
}
