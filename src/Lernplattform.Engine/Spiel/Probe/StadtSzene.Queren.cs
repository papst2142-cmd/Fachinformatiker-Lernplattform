using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Patch A 0.75.1 (Sammelliste 342, Weisung P12-258, Nico: „Der Charakter soll auch nicht auf der Straße/Schiene gehen. Nur auf
/// dem Zebrastreifen und bei Ampeln, oder Bahnübergängen.“): Engine-Regel fuer jede Stadt mit Gehwegen (Stadt 1 und Staedte aus
/// dem Werkzeug). Das Laufraster sperrte die Fahrbahn schon seit A 0.54.0 je Zelle (Mitte der Zelle), aber
/// <list type="bullet">
/// <item>eine freie Zelle am Bordstein ragt bis zu einer halben Zelle in die Fahrbahn: geglaettete gerade Strecken und ein
/// angetipptes Ziel lagen dort auf der Fahrbahn (<see cref="FahrbahnFein"/> prueft sie jetzt fein),</item>
/// <item>eine Querung ging irgendwo ueber die Breite des Zebrastreifens oder der Ampel-Furt und schraeg; jetzt geht die Figur
/// genau ueber die Mitte, quer zur Strasse (<see cref="LaufWeg"/>).</item>
/// </list>
/// Gleise sind gesperrt wie bisher (nur Bahnuebergaenge). Ohne Strasse mit Gehweg (Fassung 0) aendert sich nichts.
/// </summary>
public sealed partial class StadtSzene
{
    /// <summary>Raster der feinen Fahrbahn-Sperre.</summary>
    public const float FeinRaster = 0.05f;

    /// <summary>Abstand der Proben auf einer geraden Strecke gegen die feine Sperre.</summary>
    public const double FeinProbe = 0.02;

    /// <summary>So weit neben dem Fahrbahnrand steht die Figur vor und nach einer Querung (auf dem Gehweg).</summary>
    public const float QuerRand = 0.06f;

    /// <summary>Gegenprobe Patch A 0.75.1: Wege wie bis A 0.75.0 (keine feine Sperre, Querung irgendwo, Ort zu Ort ueber das Stadt-Raster).</summary>
    public static bool QuerenAltFuerTests { get => QuerenAlt.Value; set => QuerenAlt.Value = value; }

    private static readonly AsyncLocal<bool> QuerenAlt = new();

    /// <summary>Gezeichnete Fusswege (Zugaenge, Zierwege, Fusswege), die das Laufraster immer frei laesst.</summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> GehWege { get; private init; } = [];

    /// <summary>
    /// Fusswege, die ein Gleis abseits jeder Strasse queren (Fund Patch A 0.75.1: in Fassung 0 die Zugaenge zu Birkenhain,
    /// Talheim und Talheim-Nord; die Figur darf dort wie bisher ueber das Gleis, Fassung 0 wird nie geaendert).
    /// </summary>
    public IEnumerable<IReadOnlyList<Vector2>> FusswegeUeberGleis => GehWege.Where(w => w.Count >= 2 && Enumerable.Range(1, w.Count - 1)
        .SelectMany(i => Enumerable.Range(0, Math.Max(1, (int)(Vector2.Distance(w[i - 1], w[i]) / 0.05f)) + 1)
            .Select(k => Vector2.Lerp(w[i - 1], w[i], k / Math.Max(1f, (int)(Vector2.Distance(w[i - 1], w[i]) / 0.05f)))))
        .Any(p => Gleise.Any(g => g.Count >= 2 && Kurve.Abstand(p, g) < GleisBreite / 2)
            && !Strassen.Any(st => st.Count >= 2 && Kurve.Abstand(p, st) < StrassenBreite / 2)));

    /// <summary>Alle Uebergaenge ueber die Fahrbahn: Zebrastreifen und die Furten der Ampeln.</summary>
    public IReadOnlyList<Zebra> Querungen => _querungen ??= [.. Zebras, .. Ampeln.SelectMany(a => a.Furten)];

    private IReadOnlyList<Zebra>? _querungen;

    /// <summary>
    /// Feine Sperre: true, wo die Mitte der Figur auf der Fahrbahn einer Strasse mit Gehweg (ausser auf einem Uebergang) oder
    /// auf einem Gleis (ausser auf einer Strasse oder ihrem Gehweg: Bahnuebergang; und auf einem gezeichneten Fussweg, der das
    /// Gleis quert: <see cref="FusswegeUeberGleis"/>; Laufraster und feine Sperre bleiben darin gleich) laege. Ein Feld von <see cref="FeinRaster"/>
    /// ist gesperrt, wenn irgendein Punkt darin naeher als die halbe Strassen- bzw. Gleisbreite an der Mitte liegt; frei wird
    /// es nur, wenn es ganz auf dem Uebergang bzw. der Strasse liegt. Ohne Strasse mit Gehweg und ohne Gleis null.
    /// </summary>
    private static Func<double, double, bool>? FeinSperre(float breite, float tiefe, List<(List<Vector2> Linie, float Halb)>? gehwege,
        List<Zebra>? querungen, List<List<Vector2>> gleise, List<List<Vector2>> strassen, List<List<Vector2>> wege)
    {
        gehwege ??= [];
        if (gehwege.Count == 0 && !gleise.Any(g => g.Count >= 2))
        {
            return null;
        }
        var spalten = (int)Math.Ceiling(breite / FeinRaster);
        var zeilen = (int)Math.Ceiling(tiefe / FeinRaster);
        // je Feld Merkmale (Bits), Strecke fuer Strecke gesetzt (gleich der Pruefung gegen den ganzen Linienzug, aber nur nahe Felder)
        const byte gleisNah = 1, strasseGanz = 2, fahrbahnNah = 4, querungGanz = 8;
        const float diag = FeinRaster * 0.7072f;
        Vector2 Mitte(int s, int z) => new((s + 0.5f) * FeinRaster, (z + 0.5f) * FeinRaster);
        // Welle Nachladen A 0.82.0 (grosse Karten): die Felder werden erst beim ersten Abfragen eines Blocks von Block x Block
        // Feldern berechnet (dieselben Strecken, dieselbe Rechnung je Feld, nur auf den Block beschraenkt; gleiches Ergebnis wie
        // das ganze Feld auf einmal). Die Strecken stehen in derselben Reihenfolge wie bisher.
        var strecken = new List<(Vector2 P, Vector2 Q, float Weite, byte Bit)>();
        void Merken(IReadOnlyList<Vector2> linie, float weite, byte bit)
        {
            for (var i = 1; i < linie.Count; i++)
            {
                strecken.Add((linie[i - 1], linie[i], weite, bit));
            }
        }
        // Gleis: gesperrt, ausser das Feld liegt ganz auf einer Strasse oder ihrem Gehweg (Bahnuebergang) oder auf einem Fussweg
        foreach (var gleis in gleise.Where(g => g.Count >= 2))
        {
            Merken(gleis, (GleisBreite / 2) + diag, gleisNah);
        }
        foreach (var st in strassen.Where(st => st.Count >= 2))
        {
            Merken(st, (StrassenBreite / 2) - diag, strasseGanz);
        }
        foreach (var (linie, halb) in gehwege.Where(g => g.Linie.Count >= 2))
        {
            Merken(linie, halb - diag, strasseGanz);
            // Fahrbahn einer Strasse mit Gehweg: gesperrt, ausser das Feld liegt ganz auf einem Uebergang
            Merken(linie, (StrassenBreite / 2) + diag, fahrbahnNah);
        }
        foreach (var w in wege.Where(w => w.Count >= 2))
        {
            Merken(w, WegBreite / 2, strasseGanz);
        }
        var queren = (querungen ?? []).ToList();
        const int block = 128;
        var blockSpalten = (spalten + block - 1) / block;
        var blockZeilen = (zeilen + block - 1) / block;
        var bloecke = new bool[]?[blockSpalten * blockZeilen];
        var sperre = new object();
        bool[] Block(int bs, int bz)
        {
            var (bs0, bs1) = (bs * block, Math.Min(spalten - 1, ((bs + 1) * block) - 1));
            var (bz0, bz1) = (bz * block, Math.Min(zeilen - 1, ((bz + 1) * block) - 1));
            var hoch = bz1 - bz0 + 1;
            var merkmal = new byte[(bs1 - bs0 + 1) * hoch];
            foreach (var (p, q, weite, bit) in strecken)
            {
                var s0 = Math.Max(bs0, Math.Max(0, (int)((MathF.Min(p.X, q.X) - weite) / FeinRaster) - 1));
                var s1 = Math.Min(bs1, Math.Min(spalten - 1, (int)((MathF.Max(p.X, q.X) + weite) / FeinRaster) + 1));
                var z0 = Math.Max(bz0, Math.Max(0, (int)((MathF.Min(p.Y, q.Y) - weite) / FeinRaster) - 1));
                var z1 = Math.Min(bz1, Math.Min(zeilen - 1, (int)((MathF.Max(p.Y, q.Y) + weite) / FeinRaster) + 1));
                if (s0 > s1 || z0 > z1)
                {
                    continue;
                }
                var d = q - p;
                var ll = Vector2.Dot(d, d);
                for (var s = s0; s <= s1; s++)
                {
                    for (var z = z0; z <= z1; z++)
                    {
                        var m = Mitte(s, z);
                        var t = ll > 0 ? Math.Clamp(Vector2.Dot(m - p, d) / ll, 0f, 1f) : 0f;
                        if (Vector2.DistanceSquared(m, p + (d * t)) <= weite * weite)
                        {
                            merkmal[((s - bs0) * hoch) + z - bz0] |= bit;
                        }
                    }
                }
            }
            foreach (var q in queren)
            {
                var r = (StrassenBreite / 2) + 0.3f;
                var s0 = Math.Max(bs0, Math.Max(0, (int)((q.Mitte.X - r) / FeinRaster) - 1));
                var s1 = Math.Min(bs1, Math.Min(spalten - 1, (int)((q.Mitte.X + r) / FeinRaster) + 1));
                var z0 = Math.Max(bz0, Math.Max(0, (int)((q.Mitte.Y - r) / FeinRaster) - 1));
                var z1 = Math.Min(bz1, Math.Min(zeilen - 1, (int)((q.Mitte.Y + r) / FeinRaster) + 1));
                for (var s = s0; s <= s1; s++)
                {
                    for (var z = z0; z <= z1; z++)
                    {
                        if (q.Drauf(Mitte(s, z), -diag, 0.2f))
                        {
                            merkmal[((s - bs0) * hoch) + z - bz0] |= querungGanz;
                        }
                    }
                }
            }
            var gesperrt = new bool[merkmal.Length];
            for (var i = 0; i < merkmal.Length; i++)
            {
                var m = merkmal[i];
                gesperrt[i] = ((m & gleisNah) != 0 && (m & strasseGanz) == 0) || ((m & fahrbahnNah) != 0 && (m & querungGanz) == 0);
            }
            return gesperrt;
        }
        return (x, y) =>
        {
            if (QuerenAlt.Value || x < 0 || y < 0)
            {
                return false;
            }
            var s = (int)(x / FeinRaster);
            var z = (int)(y / FeinRaster);
            if (s >= spalten || z >= zeilen)
            {
                return false;
            }
            var nr = ((s / block) * blockZeilen) + (z / block);
            var feld = Volatile.Read(ref bloecke[nr]);
            if (feld is null)
            {
                lock (sperre)
                {
                    feld = bloecke[nr];
                    if (feld is null)
                    {
                        feld = Block(s / block, z / block);
                        Volatile.Write(ref bloecke[nr], feld);
                    }
                }
            }
            var hoch = Math.Min(zeilen - 1, (((z / block) + 1) * block) - 1) - ((z / block) * block) + 1;
            return feld[((s % block) * hoch) + (z % block)];
        };
    }

    /// <summary>
    /// Weg der Figur zu Fuss von <paramref name="start"/> nach <paramref name="ziel"/> (ohne den Startpunkt, leer = schon da oder
    /// nicht erreichbar): Laufraster, danach geht jede Querung einer Fahrbahn genau ueber die Mitte des Zebrastreifens bzw. der
    /// Furt, quer zur Strasse, von Bordstein zu Bordstein.
    /// </summary>
    public IReadOnlyList<(double X, double Y)> LaufWeg((double X, double Y) start, (double X, double Y) ziel)
    {
        var weg = Laufraster.Weg(start, ziel);
        if (weg.Count == 0 || QuerenAlt.Value || Querungen.Count == 0)
        {
            return weg;
        }
        // Punkte mit Start; jedes Stueck, das einen Uebergang beruehrt, gehoert zu einem Lauf ueber diesen Uebergang
        var punkte = new List<Vector2>(weg.Count + 1) { new((float)start.X, (float)start.Y) };
        punkte.AddRange(weg.Select(p => new Vector2((float)p.X, (float)p.Y)));
        var neu = new List<(double X, double Y)>(weg.Count + 4);
        var halb = (StrassenBreite / 2) + QuerRand;
        var i = 0;
        while (i + 1 < punkte.Count)
        {
            if (Querung(punkte[i], punkte[i + 1]) is not { } q)
            {
                neu.Add((punkte[i + 1].X, punkte[i + 1].Y));
                i++;
                continue;
            }
            // Lauf: alle folgenden Stuecke, die denselben Uebergang beruehren
            var k = i + 1;
            while (k + 1 < punkte.Count && Querung(punkte[k], punkte[k + 1]) == q)
            {
                k++;
            }
            float Quer(Vector2 p) => Vector2.Dot(p - q.Mitte, q.Quer);
            var (rein, raus) = (Quer(punkte[i]), Quer(punkte[k]));
            var reinDraussen = MathF.Abs(rein) >= StrassenBreite / 2;
            var rausDraussen = MathF.Abs(raus) >= StrassenBreite / 2;
            if (reinDraussen)
            {
                var a = q.Mitte + (q.Quer * (MathF.Sign(rein) * halb));
                neu.Add((a.X, a.Y));
            }
            if (rausDraussen && (!reinDraussen || MathF.Sign(rein) != MathF.Sign(raus)))
            {
                var b = q.Mitte + (q.Quer * (MathF.Sign(raus) * halb));
                neu.Add((b.X, b.Y));
            }
            else if (reinDraussen && rausDraussen)
            {
                // nur an den Uebergang heran und auf derselben Seite zurueck: nicht auf die Fahrbahn
                neu.RemoveAt(neu.Count - 1);
            }
            neu.Add((punkte[k].X, punkte[k].Y));
            i = k;
        }
        return neu;
    }

    /// <summary>Der Uebergang, dessen Fahrbahn die Strecke betritt (null = keiner).</summary>
    private Zebra? Querung(Vector2 a, Vector2 b)
    {
        var proben = Math.Max(1, (int)(Vector2.Distance(a, b) / (float)FeinProbe));
        for (var i = 0; i <= proben; i++)
        {
            var p = Vector2.Lerp(a, b, i / (float)proben);
            foreach (var q in Querungen)
            {
                if (q.Drauf(p, 0.15f, 0f))
                {
                    return q;
                }
            }
        }
        return null;
    }
}
