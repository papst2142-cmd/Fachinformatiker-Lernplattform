using System.Diagnostics;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280, Nico 4a und 5b): entscheidet ohne Grafikkarte, welche Kacheln einer Szene
/// geladen sind. Immer da ist die <see cref="StadtSzene.Grundlage"/> (Boden, Fernkaesten, Instancing, Ampel-Lichter). Die
/// Kacheln im Kreis <see cref="Ladeplan.Nah"/> um die Figur liegen auf der Grafikkarte (<see cref="Geladen"/>), der naechste
/// Ring bis <see cref="Ladeplan.Vor"/> wird im Hintergrund vorbereitet (<see cref="Bereit"/>, eine Kachel nach der anderen).
/// Freigegeben wird erst ueber <see cref="Ladeplan.NahWeg"/> bzw. <see cref="Ladeplan.VorWeg"/>. Die Oberflaeche ruft je Bild
/// <see cref="Takt"/>, laedt hoch, was <see cref="Naechste"/> nennt, und meldet es mit <see cref="Hochgeladen"/>. Fehlt eine
/// noetige Kachel (Sprung der Figur), baut <see cref="JetztBauen"/> sie sofort. Tests nutzen dieselbe Klasse ohne
/// Hintergrund (<c>hintergrund: false</c>).
/// </summary>
public sealed class Nachlader
{
    /// <summary>
    /// Eine fertige Kachel zum Hochladen: Stadtnetz, Paket-Netz und (wenn die Grafikstufe es zeigt) Kleingruen in EINEM Netz
    /// (ein Zeichenaufruf je Lauf, nach Zellen geordnet), dazu die Haeuser. <see cref="BauMs"/> = Zeit fuer den Bau.
    /// </summary>
    public sealed record Paket(Stadtteile.Kachel Kachel, Netz Fest, IReadOnlyList<(StadtHaus Haus, Netz Netz)> Haeuser, long Bytes, double BauMs);

    private readonly Dictionary<Stadtteile.Kachel, long> _geladen = [];
    private readonly Dictionary<Stadtteile.Kachel, Paket> _bereit = [];
    private readonly bool _hintergrund;
    private Task<Paket>? _bau;
    private Stadtteile.Kachel _bauKachel;
    private List<Stadtteile.Kachel> _noetig = [];
    private List<Stadtteile.Kachel> _vor = [];
    private (float X, float Z)? _ort;
    private (float X, float Z)? _ziel;
    private List<Stadtteile.Kachel> _zielKacheln = [];

    public Nachlader(StadtSzene szene, bool mitGruen, bool hintergrund = true)
    {
        Szene = szene;
        MitGruen = mitGruen;
        _hintergrund = hintergrund;
        // Welle Nachladen: die Laternen (jede Kachel braucht sie) entstehen neben der Grundlage auf einem zweiten Kern
        var laternen = Task.Run(() => szene.Laternen);
        Grundlage = szene.GrundlageVorbereiten();
        _ = laternen.GetAwaiter().GetResult();
        HoechstBytes = Bytes;
    }

    public StadtSzene Szene { get; }

    /// <summary>Kleingruen im Netz der Kachel (Grafikstufe mit Kleingruen beim Abstand der festen Kamera).</summary>
    public bool MitGruen { get; }

    public StadtSzene.Grundlage Grundlage { get; }

    /// <summary>Kacheln auf der Grafikkarte mit ihren Netzdaten in Byte.</summary>
    public IReadOnlyDictionary<Stadtteile.Kachel, long> Geladen => _geladen;

    /// <summary>Vorbereitete Kacheln (gebaut, noch nicht hochgeladen).</summary>
    public IReadOnlyDictionary<Stadtteile.Kachel, Paket> Bereit => _bereit;

    /// <summary>Die nah noetigen Kacheln beim letzten <see cref="Takt"/> (naechste zuerst).</summary>
    public IReadOnlyList<Stadtteile.Kachel> Noetig => _noetig;

    /// <summary>
    /// Ankunft mit Bus oder Zug (Weisung P12-280, 5b): das Ende des Wegs der Figur. Die dort noetigen Kacheln werden nach dem
    /// Ring um die Figur im Hintergrund vorbereitet und nicht verworfen, solange das Ziel gilt (null = kein Ziel).
    /// </summary>
    public (float X, float Z)? Ziel
    {
        get => _ziel;
        set
        {
            if (value == _ziel)
            {
                return;
            }
            _ziel = value;
            _zielKacheln = value is { } z ? Ladeplan.Noetig(z.X, z.Z, Szene.Breite, Szene.Tiefe) : [];
        }
    }

    /// <summary>Gleichzeitig geladene Netzdaten in Byte: Grundlage, Kacheln auf der Grafikkarte und vorbereitete (Nico 4a).</summary>
    public long Bytes => Grundlage.Bytes + _geladen.Values.Sum() + _bereit.Values.Sum(p => p.Bytes);

    /// <summary>Hoechster Wert von <see cref="Bytes"/> seit dem Anlegen.</summary>
    public long HoechstBytes { get; private set; }

    /// <summary>Bauzeiten der Kacheln in ms (Messpunkt „Kachel nachladen“, Hintergrund), in der Reihenfolge des Baus.</summary>
    public List<double> BauZeiten { get; } = [];

    /// <summary>Wie oft eine noetige Kachel sofort gebaut werden musste (sie war nicht vorbereitet).</summary>
    public int Sofort { get; private set; }

    /// <summary>Eine Kachel bauen und packen (darf ausserhalb des Oberflaechen-Threads laufen).</summary>
    public static Paket Packen(StadtSzene szene, Stadtteile.Kachel k, bool mitGruen)
    {
        var uhr = Stopwatch.StartNew();
        var n = szene.KachelVorbereiten(k);
        var fest = new Netz();
        fest.Anhaengen(n.Stadt);
        if (n.Paket is { } p)
        {
            fest.Anhaengen(p);
        }
        if (mitGruen && n.Gruen is { } g)
        {
            fest.Anhaengen(g);
        }
        fest.NachZellenOrdnen(StadtSzene.PaketZelle);
        var bytes = StadtLast.Bytes(fest) + n.Haeuser.Sum(h => StadtLast.Bytes(h.Netz));
        return new Paket(k, fest, n.Haeuser, bytes, uhr.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Ein Takt an der Stelle (<paramref name="x"/>, <paramref name="z"/>): fertigen Hintergrundbau uebernehmen, ferne Kacheln
    /// freigeben (Rueckgabe: diese Kacheln muss die Oberflaeche auf der Grafikkarte freigeben), den naechsten Bau starten.
    /// </summary>
    public List<Stadtteile.Kachel> Takt(float x, float z)
    {
        Uebernehmen();
        if (_ort is not { } o || MathF.Abs(o.X - x) + MathF.Abs(o.Z - z) > 0.5f)
        {
            _ort = (x, z);
            _noetig = Ladeplan.Noetig(x, z, Szene.Breite, Szene.Tiefe);
            _vor = Ladeplan.Vorbereiten(x, z, Szene.Breite, Szene.Tiefe);
        }
        var frei = new List<Stadtteile.Kachel>();
        foreach (var k in _geladen.Keys)
        {
            if (Ladeplan.FreigebenNah(k, x, z))
            {
                frei.Add(k);
            }
        }
        foreach (var k in frei)
        {
            _geladen.Remove(k);
        }
        foreach (var k in _bereit.Keys.Where(k => Ladeplan.FreigebenVor(k, x, z) && !_zielKacheln.Contains(k)).ToList())
        {
            _bereit.Remove(k);
        }
        if (_bau is null && _vor.Concat(_zielKacheln).Where(k => !_geladen.ContainsKey(k) && !_bereit.ContainsKey(k)).Take(1).ToList() is [var naechste])
        {
            _bauKachel = naechste;
            if (_hintergrund)
            {
                var szene = Szene;
                var gruen = MitGruen;
                _bau = Task.Run(() => Packen(szene, naechste, gruen));
            }
            else
            {
                Ablegen(Packen(Szene, naechste, MitGruen));
            }
        }
        Merken();
        return frei;
    }

    /// <summary>Die naechste noetige Kachel, die vorbereitet und noch nicht hochgeladen ist (null = keine).</summary>
    public Paket? Naechste()
    {
        foreach (var k in _noetig)
        {
            if (!_geladen.ContainsKey(k) && _bereit.TryGetValue(k, out var p))
            {
                return p;
            }
        }
        return null;
    }

    /// <summary>Noetige Kacheln, die weder geladen noch vorbereitet sind (naechste zuerst).</summary>
    public IEnumerable<Stadtteile.Kachel> Fehlend() => _noetig.Where(k => !_geladen.ContainsKey(k) && !_bereit.ContainsKey(k));

    /// <summary>Die Kachel liegt jetzt auf der Grafikkarte (ihre Netze im Prozessor werden nicht mehr gehalten).</summary>
    public void Hochgeladen(Paket p)
    {
        _bereit.Remove(p.Kachel);
        _geladen[p.Kachel] = p.Bytes;
        Merken();
    }

    /// <summary>Eine noetige Kachel sofort bauen (wartet auf einen laufenden Bau derselben Kachel) und vorbereitet ablegen.</summary>
    public Paket JetztBauen(Stadtteile.Kachel k)
    {
        if (_bau is { } bau && _bauKachel == k)
        {
            bau.Wait();
            Uebernehmen();
        }
        if (!_bereit.TryGetValue(k, out var p))
        {
            Sofort++;
            p = Packen(Szene, k, MitGruen);
            Ablegen(p);
        }
        Merken();
        return p;
    }

    /// <summary>
    /// Start (Ladebild, ausserhalb des Oberflaechen-Threads): die fehlenden <paramref name="kacheln"/> auf allen Kernen zugleich
    /// bauen und vorbereitet ablegen (danach liefert <see cref="JetztBauen"/> sie ohne Bau). Jede Kachel wird genau wie mit
    /// <see cref="Packen"/> gebaut, nur nebeneinander; die Szene rechnet ihre einmaligen Teile (Grundlage, Laternen) vorher.
    /// </summary>
    public void StartBauen(IReadOnlyList<Stadtteile.Kachel> kacheln)
    {
        _bau?.Wait();
        Uebernehmen();
        var fehlt = kacheln.Where(k => !_geladen.ContainsKey(k) && !_bereit.ContainsKey(k)).Distinct().ToList();
        var pakete = new Paket[fehlt.Count];
        var (szene, gruen) = (Szene, MitGruen);
        _ = szene.Laternen;
        Parallel.For(0, fehlt.Count, i => pakete[i] = Packen(szene, fehlt[i], gruen));
        foreach (var p in pakete)
        {
            Ablegen(p);
        }
        Merken();
    }

    /// <summary>Alles auf der Grafikkarte gilt als verloren (OpenGL neu): die Kacheln werden neu hochgeladen.</summary>
    public void GrafikVerloren()
    {
        _geladen.Clear();
        _ort = null;
    }

    private void Uebernehmen()
    {
        if (_bau is { IsCompleted: true } bau)
        {
            _bau = null;
            if (bau.IsCompletedSuccessfully)
            {
                Ablegen(bau.Result);
            }
        }
    }

    private void Ablegen(Paket p)
    {
        BauZeiten.Add(p.BauMs);
        if (!_geladen.ContainsKey(p.Kachel))
        {
            _bereit[p.Kachel] = p;
        }
    }

    private void Merken() => HoechstBytes = Math.Max(HoechstBytes, Bytes);
}
