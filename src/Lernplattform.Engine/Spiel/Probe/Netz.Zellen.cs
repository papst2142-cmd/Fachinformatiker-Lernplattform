using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle 327 (Weisung P12-244, Kurzplan P12-142 Weg a): ein Netz, das nur zum Teil im Bild ist, in Zellen des Bodens
/// geteilt. Die Dreiecke werden nach der Zelle ihrer Mitte geordnet (Reihe fuer Reihe, jede zweite Reihe rueckwaerts, damit
/// benachbarte Zellen auch in der Reihenfolge nebeneinander liegen); die Ecken bleiben, wie sie sind, nur die Indizes werden
/// umgestellt. Gezeichnet werden nur die Zellen, deren Kugel im Sichtkegel liegt (<see cref="FestUebergang.ImSichtkegel"/>,
/// wie bei den Haeusern seit D1), und aufeinanderfolgende sichtbare Zellen in EINEM Aufruf (<see cref="Laeufe"/>). Sind alle
/// Zellen im Bild, ist das ein Aufruf mit allen Dreiecken wie vorher. Das Aussehen bleibt gleich: ausserhalb des Bilds
/// faellt nur weg, was man ohnehin nicht sieht.
/// </summary>
public sealed partial class Netz
{
    /// <summary>Eine Zelle: Dreiecke ab Index <see cref="Start"/> (Anzahl Indizes <see cref="Anzahl"/>) mit ihrer Kugel.</summary>
    public readonly record struct Zelle(int Start, int Anzahl, Vector3 Mitte, float Halb);

    /// <summary>Die Zellen nach <see cref="NachZellenOrdnen"/> (in Reihenfolge der Indizes, keine leere); null = nicht geteilt.</summary>
    public IReadOnlyList<Zelle>? Zellen { get; private set; }

    /// <summary>
    /// Gegenprobe Welle 327 (nur Tests und Start-Test, je Ablauf): Zellen nicht beachten, das Netz ganz zeichnen und zaehlen wie
    /// bis A 0.73.0 (Stadt 1 in „Niedrig“ dann ueber der Grenze).
    /// </summary>
    public static bool OhneZellenFuerTests { get => OhneZellen.Value; set => OhneZellen.Value = value; }

    private static readonly AsyncLocal<bool> OhneZellen = new();

    /// <summary>
    /// Die Dreiecke nach Zellen von <paramref name="groesse"/> × <paramref name="groesse"/> (Boden, x und z) ordnen.
    /// Innerhalb einer Zelle bleibt die Reihenfolge der Dreiecke wie vorher (stabil).
    /// </summary>
    public void NachZellenOrdnen(float groesse)
    {
        var dreiecke = _indizes.Count / 3;
        if (dreiecke == 0)
        {
            Zellen = [];
            return;
        }
        var spalte = new int[dreiecke];
        var reihe = new int[dreiecke];
        int minS = int.MaxValue, maxS = int.MinValue, minR = int.MaxValue;
        for (var d = 0; d < dreiecke; d++)
        {
            var m = (Ort((int)_indizes[3 * d]) + Ort((int)_indizes[(3 * d) + 1]) + Ort((int)_indizes[(3 * d) + 2])) / 3f;
            spalte[d] = (int)MathF.Floor(m.X / groesse);
            reihe[d] = (int)MathF.Floor(m.Z / groesse);
            minS = Math.Min(minS, spalte[d]);
            maxS = Math.Max(maxS, spalte[d]);
            minR = Math.Min(minR, reihe[d]);
        }
        var breite = (long)maxS - minS + 1;
        var schluessel = new long[dreiecke];
        for (var d = 0; d < dreiecke; d++)
        {
            long r = reihe[d] - minR, s = spalte[d] - minS;
            schluessel[d] = (r * breite) + (r % 2 == 0 ? s : breite - 1 - s);
        }
        var folge = StabilNachSchluessel(schluessel);
        var alt = _indizes.ToArray();
        var zellen = new List<Zelle>();
        var start = 0;
        for (var i = 0; i < dreiecke; i++)
        {
            var d = folge[i];
            _indizes[3 * i] = alt[3 * d];
            _indizes[(3 * i) + 1] = alt[(3 * d) + 1];
            _indizes[(3 * i) + 2] = alt[(3 * d) + 2];
            if (i + 1 == dreiecke || schluessel[folge[i + 1]] != schluessel[d])
            {
                zellen.Add(ZelleAus(start, i + 1));
                start = i + 1;
            }
        }
        Zellen = zellen;
    }

    /// <summary>
    /// Die Dreiecke stabil nach Schluessel (Zaehlsortierung, linear: das Werkzeug baut das Paket-Netz nach jeder Aenderung neu,
    /// Messpunkt „3D nach Aenderung“ 400 ms). Bei sehr weit gestreuten Schluesseln stabil ueber Schluessel und Stelle.
    /// </summary>
    private static int[] StabilNachSchluessel(long[] schluessel)
    {
        var n = schluessel.Length;
        var folge = new int[n];
        long min = long.MaxValue, max = long.MinValue;
        foreach (var k in schluessel)
        {
            min = Math.Min(min, k);
            max = Math.Max(max, k);
        }
        var spanne = max - min + 1;
        if (spanne > (4L * n) + 4096)
        {
            for (var d = 0; d < n; d++)
            {
                folge[d] = d;
            }
            Array.Sort(folge, (a, b) => schluessel[a] != schluessel[b] ? schluessel[a].CompareTo(schluessel[b]) : a.CompareTo(b));
            return folge;
        }
        var anfang = new int[spanne + 1];
        foreach (var k in schluessel)
        {
            anfang[k - min + 1]++;
        }
        for (var i = 1; i < anfang.Length; i++)
        {
            anfang[i] += anfang[i - 1];
        }
        for (var d = 0; d < n; d++)
        {
            folge[anfang[schluessel[d] - min]++] = d;
        }
        return folge;
    }

    /// <summary>Zelle aus den Dreiecken <paramref name="von"/> bis vor <paramref name="bis"/> (Kugel wie <see cref="Kugel"/>).</summary>
    private Zelle ZelleAus(int von, int bis)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 3 * von; i < 3 * bis; i++)
        {
            var p = Ort((int)_indizes[i]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new Zelle(3 * von, 3 * (bis - von), (min + max) / 2, (max - min).Length() / 2);
    }

    /// <summary>
    /// Die Aufrufe fuer ein Bild: aufeinanderfolgende Zellen im Sichtkegel als ein Lauf (Start, Anzahl Indizes) in
    /// <paramref name="laeufe"/> (vorher geleert). <paramref name="alle"/> = ohne Sichtkegel (Gegenprobe der Tests);
    /// ein ungeteiltes Netz ist ein Lauf mit allem. Dieselbe Rechnung nutzen das Zeichnen und die Zaehlung ohne Grafikkarte.
    /// </summary>
    public void Laeufe(Matrix4x4 sichtProj, bool alle, List<(int Start, int Anzahl)> laeufe) =>
        Laeufe(Zellen, _indizes.Count, sichtProj, alle, laeufe);

    /// <summary>
    /// Wie <see cref="Laeufe(Matrix4x4, bool, List{ValueTuple{int, int}})"/> fuer ein hochgeladenes Netz mit
    /// <paramref name="indizes"/> Indizes, das nur seine <paramref name="zellen"/> behalten hat.
    /// </summary>
    public static void Laeufe(IReadOnlyList<Zelle>? zellen, int indizes, Matrix4x4 sichtProj, bool alle, List<(int Start, int Anzahl)> laeufe)
    {
        laeufe.Clear();
        if (zellen is null || OhneZellen.Value || (zellen.Count > 0 ? zellen[^1].Start + zellen[^1].Anzahl : 0) != indizes)
        {
            // ungeteilt, oder nach dem Ordnen noch etwas angehaengt: alles in einem Aufruf (nie etwas weglassen)
            if (indizes > 0)
            {
                laeufe.Add((0, indizes));
            }
            return;
        }
        foreach (var z in zellen)
        {
            if (!alle && !FestUebergang.ImSichtkegel(sichtProj, z.Mitte, z.Halb))
            {
                continue;
            }
            if (laeufe.Count > 0 && laeufe[^1] is var (s, a) && s + a == z.Start)
            {
                laeufe[^1] = (s, a + z.Anzahl);
            }
            else
            {
                laeufe.Add((z.Start, z.Anzahl));
            }
        }
    }
}
