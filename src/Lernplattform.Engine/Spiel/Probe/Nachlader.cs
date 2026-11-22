using System.Diagnostics;
using System.Numerics;
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
    private Task<(Paket? Paket, Exception? Fehler)>? _bau;
    private Stadtteile.Kachel _bauKachel;
    private List<Stadtteile.Kachel> _noetig = [];
    private List<Stadtteile.Kachel> _vor = [];
    private (float X, float Z)? _ort;
    private (float X, float Z)? _ziel;
    private List<Stadtteile.Kachel> _zielKacheln = [];
    private readonly HashSet<Stadtteile.Kachel> _kaputt = [];

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

    /// <summary>Die Kacheln zu <see cref="BauZeiten"/> (gleiche Stelle), fuer die Wiederholung einer Messung.</summary>
    public List<Stadtteile.Kachel> BauKacheln { get; } = [];

    /// <summary>
    /// Welle Fehlerpruefung Teil 2 (Weisung P12-345, Sammelliste 430): wohin ein Fehler beim Bau einer Kachel geschrieben
    /// wird. Das Programm setzt es beim Start (Lernplattform: <c>fehler.log</c> im Datenordner, Werkzeug: <c>fehler.log</c> des
    /// Werkzeugs); null = nirgends. <see cref="Protokoll"/> einer Instanz geht vor (Tests).
    /// </summary>
    public static Action<string>? Fehlerziel { get; set; }

    /// <summary>Ziel der Fehler dieser Instanz (Vorgabe: <see cref="Fehlerziel"/>).</summary>
    public Action<string>? Protokoll { get; set; }

    /// <summary>
    /// Kacheln, deren Bau gescheitert ist (Sammelliste 430): sie werden in dieser Sitzung nicht noch einmal gebaut und bleiben
    /// fern gezeichnet (Fernkasten der Grundlage), nie ein Loch und kein Absturz.
    /// </summary>
    public IReadOnlySet<Stadtteile.Kachel> Kaputt => _kaputt;

    /// <summary>Nur fuer Tests (Sammelliste 430): scheitert der Bau dieser Kachel mit einem eingeschleusten Fehler.</summary>
    public static readonly AsyncLocal<Func<Stadtteile.Kachel, bool>?> FehlerFuerTests = new();

    /// <summary>Wie oft eine noetige Kachel sofort gebaut werden musste (sie war nicht vorbereitet).</summary>
    public int Sofort { get; private set; }

    /// <summary>Eine Kachel bauen und packen (darf ausserhalb des Oberflaechen-Threads laufen).</summary>
    public static Paket Packen(StadtSzene szene, Stadtteile.Kachel k, bool mitGruen)
    {
        if (FehlerFuerTests.Value is { } fehler && fehler(k))
        {
            throw new InvalidOperationException("eingeschleuster Fehler (Test) in Kachel " + k.Spalte + "," + k.Reihe);
        }
        var uhr = Stopwatch.StartNew();
        var n = szene.KachelVorbereiten(k);
        var p = Packen(n, mitGruen);
        return p with { BauMs = uhr.Elapsed.TotalMilliseconds };
    }

    /// <summary>
    /// Welle S 0.55.0: schon gebaute Netze einer Kachel packen (das Werkzeug packt eine Kachel mit und ohne Kleingruen aus
    /// EINEM Bau, fuer die Stufen Mittel/Hoch und Niedrig). Gleiche Rechnung wie <see cref="Packen(StadtSzene, Stadtteile.Kachel, bool)"/>.
    /// </summary>
    public static Paket Packen(StadtSzene.KachelNetze n, bool mitGruen)
    {
        var uhr = Stopwatch.StartNew();
        var k = n.Kachel;
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
    /// Kugel um eine Kachel fuer den Sichtkegel (Boden bis zur hoechsten Stelle eines Hauses, mindestens 6 fuer Baeume und
    /// Laternen). Zeichnen (<c>StadtFlaeche</c>) und Zaehlung ohne Grafikkarte (<see cref="StadtbauGrenzen.Nachladen"/>) nutzen sie.
    /// </summary>
    public static (Vector3 Mitte, float Halb) Kugel(StadtSzene.Grundlage? grundlage, Stadtteile.Kachel k)
    {
        var h = MathF.Max(6f, grundlage?.Hoehen.GetValueOrDefault(k) ?? 0f);
        var m = k.Mitte;
        return (new Vector3(m.X, h / 2, m.Y), MathF.Sqrt((2 * (Stadtteile.Groesse / 2) * (Stadtteile.Groesse / 2)) + (h * h / 4)));
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
        if (_bau is null && _vor.Concat(_zielKacheln).Where(k => !_geladen.ContainsKey(k) && !_bereit.ContainsKey(k) && !_kaputt.Contains(k)).Take(1).ToList() is [var naechste])
        {
            _bauKachel = naechste;
            if (_hintergrund)
            {
                var szene = Szene;
                var gruen = MitGruen;
                // die Aufgabe selbst wirft nie (Sammelliste 430): ein Fehler kommt als Ergebnis zurueck und wird in Uebernehmen gemeldet
                _bau = Task.Run(() => Versuchen(szene, naechste, gruen));
            }
            else if (Versuchen(Szene, naechste, MitGruen) is { } ergebnis)
            {
                Annehmen(naechste, ergebnis);
            }
        }
        Merken();
        return frei;
    }

    /// <summary>
    /// Welle A 0.95.0 (Sammelliste 385): ist die Kachel unter (<paramref name="x"/>, <paramref name="z"/>) auf der Grafikkarte?
    /// Beim freien Gehen zaehlt eine noch nicht geladene Kachel als gesperrt (die Figur faellt nie ins Leere).
    /// </summary>
    public bool IstGeladen(float x, float z) => _geladen.ContainsKey(Stadtteile.Von(x, z, Szene.Breite, Szene.Tiefe));

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
    public IEnumerable<Stadtteile.Kachel> Fehlend() => _noetig.Where(k => !_geladen.ContainsKey(k) && !_bereit.ContainsKey(k) && !_kaputt.Contains(k));

    /// <summary>Die Kachel liegt jetzt auf der Grafikkarte (ihre Netze im Prozessor werden nicht mehr gehalten).</summary>
    public void Hochgeladen(Paket p)
    {
        _bereit.Remove(p.Kachel);
        _geladen[p.Kachel] = p.Bytes;
        Merken();
    }

    /// <summary>
    /// Eine noetige Kachel sofort bauen (wartet auf einen laufenden Bau derselben Kachel) und vorbereitet ablegen. Null, wenn ihr
    /// Bau gescheitert ist (Sammelliste 430: Eintrag in <c>fehler.log</c>, die Kachel bleibt fern gezeichnet).
    /// </summary>
    public Paket? JetztBauen(Stadtteile.Kachel k)
    {
        if (_bau is { } bau && _bauKachel == k)
        {
            bau.Wait();
            Uebernehmen();
        }
        if (_kaputt.Contains(k))
        {
            return null;
        }
        if (!_bereit.TryGetValue(k, out var p))
        {
            Sofort++;
            var ergebnis = Versuchen(Szene, k, MitGruen);
            Annehmen(k, ergebnis);
            if (ergebnis.Paket is not { } neu)
            {
                Merken();
                return null;
            }
            p = neu;
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
        var fehlt = kacheln.Where(k => !_geladen.ContainsKey(k) && !_bereit.ContainsKey(k) && !_kaputt.Contains(k)).Distinct().ToList();
        var ergebnisse = new (Paket? Paket, Exception? Fehler)[fehlt.Count];
        var (szene, gruen) = (Szene, MitGruen);
        _ = szene.Laternen;
        // Sammelliste 430: ein Fehler einer Kachel schlaegt nicht durch (kein Abbruch des Ladebilds), er wird gemeldet
        Parallel.For(0, fehlt.Count, i => ergebnisse[i] = Versuchen(szene, fehlt[i], gruen));
        for (var i = 0; i < fehlt.Count; i++)
        {
            Annehmen(fehlt[i], ergebnisse[i]);
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
            Annehmen(_bauKachel, bau.IsCompletedSuccessfully ? bau.Result : (null, bau.Exception?.GetBaseException() ?? new InvalidOperationException("Bau abgebrochen")));
        }
    }

    /// <summary>Eine Kachel bauen; ein Fehler wird als Ergebnis zurueckgegeben, nie geworfen (Sammelliste 430).</summary>
    private static (Paket? Paket, Exception? Fehler) Versuchen(StadtSzene szene, Stadtteile.Kachel k, bool mitGruen)
    {
        try
        {
            return (Packen(szene, k, mitGruen), null);
        }
        catch (Exception x) when (x is not OutOfMemoryException)
        {
            return (null, x);
        }
    }

    /// <summary>Ergebnis eines Baus uebernehmen: Paket ablegen oder Kachel als kaputt merken und den Fehler melden.</summary>
    private void Annehmen(Stadtteile.Kachel k, (Paket? Paket, Exception? Fehler) ergebnis)
    {
        if (ergebnis.Paket is { } p)
        {
            Ablegen(p);
            return;
        }
        if (_kaputt.Add(k))
        {
            (Protokoll ?? Fehlerziel)?.Invoke(FehlerText(k, ergebnis.Fehler));
        }
    }

    /// <summary>Text des Eintrags in fehler.log: Kachel, Art und Text des Fehlers, Stapel.</summary>
    public static string FehlerText(Stadtteile.Kachel k, Exception? fehler) =>
        "Nachladen: Kachel " + k.Spalte.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
        + k.Reihe.ToString(System.Globalization.CultureInfo.InvariantCulture) + " nicht gebaut, wird fern gezeichnet: "
        + (fehler?.ToString() ?? "unbekannter Fehler");

    private void Ablegen(Paket p)
    {
        BauZeiten.Add(p.BauMs);
        BauKacheln.Add(p.Kachel);
        if (!_geladen.ContainsKey(p.Kachel))
        {
            _bereit[p.Kachel] = p;
        }
    }

    private void Merken() => HoechstBytes = Math.Max(HoechstBytes, Bytes);
}
