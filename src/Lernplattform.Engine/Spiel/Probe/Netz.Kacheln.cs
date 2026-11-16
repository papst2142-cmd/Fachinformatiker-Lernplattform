using System.Numerics;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280): ein Netz auf eine Kachel (<see cref="Stadtteile"/>) zuschneiden. Jedes Dreieck,
/// das ganz in einer Kachel liegt, kommt unveraendert in genau die Kachel seiner Mitte (links und oben eingeschlossen). Ein
/// Dreieck, das ueber eine Kachelgrenze reicht (grosse Flaechen wie lange Strassenbaender, Parks, Fluss), wird an den Linien des
/// Rasters geteilt; jedes Stueck kommt in die Kachel seiner Mitte. So ergeben alle Kacheln zusammen genau die Flaeche des
/// ganzen Netzes, nichts doppelt und ohne Naht (die Ecken der Stuecke liegen auf den Kanten des Dreiecks, Normale und Farbe
/// werden linear dazwischen gerechnet, wie die Grafikkarte sie ohnehin ueber das Dreieck verteilt).
/// </summary>
public sealed partial class Netz
{
    /// <summary>Dreiecke, die beim Zuschneiden geteilt wurden (Tests, Messung).</summary>
    public int Geteilt { get; private set; }

    /// <summary>
    /// Nur der Teil dieses Netzes in der Kachel <paramref name="k"/> einer Karte von <paramref name="breite"/> ×
    /// <paramref name="tiefe"/> (neues Netz, dieses bleibt unveraendert). Was ueber den Rand der Karte hinausreicht, gehoert
    /// zur naechsten Kachel am Rand (<see cref="Stadtteile.Von"/>), damit nichts verloren geht.
    /// </summary>
    public Netz NurKachel(Stadtteile.Kachel k, float breite, float tiefe)
    {
        bool Drin(Vector3 m) => Stadtteile.Von(m.X, m.Z, breite, tiefe) == k;
        var neu = new Netz { UnterseiteBis = UnterseiteBis };
        var abbild = new Dictionary<uint, uint>();
        Span<float> a = stackalloc float[Zahlen];
        Span<float> b = stackalloc float[Zahlen];
        Span<float> c = stackalloc float[Zahlen];
        for (var d = 0; d + 2 < _indizes.Count; d += 3)
        {
            var i0 = _indizes[d];
            var i1 = _indizes[d + 1];
            var i2 = _indizes[d + 2];
            var p0 = Ort((int)i0);
            var p1 = Ort((int)i1);
            var p2 = Ort((int)i2);
            var minX = MathF.Min(p0.X, MathF.Min(p1.X, p2.X));
            var maxX = MathF.Max(p0.X, MathF.Max(p1.X, p2.X));
            var minZ = MathF.Min(p0.Z, MathF.Min(p1.Z, p2.Z));
            var maxZ = MathF.Max(p0.Z, MathF.Max(p1.Z, p2.Z));
            if (!Beruehrt(k, minX, maxX, minZ, maxZ, breite, tiefe))
            {
                continue;
            }
            if (!Kreuzt(minX, maxX) && !Kreuzt(minZ, maxZ))
            {
                // ganz in einer Zelle des Rasters: die Kachel seiner Mitte bekommt es unveraendert
                var m = (p0 + p1 + p2) / 3f;
                if (Drin(m))
                {
                    neu._indizes.Add(Uebernehmen(neu, abbild, i0));
                    neu._indizes.Add(Uebernehmen(neu, abbild, i1));
                    neu._indizes.Add(Uebernehmen(neu, abbild, i2));
                }
                continue;
            }
            Geteilt++;
            Lesen((int)i0, a);
            Lesen((int)i1, b);
            Lesen((int)i2, c);
            var stuecke = new List<List<float[]>> { new() { a.ToArray(), b.ToArray(), c.ToArray() } };
            stuecke = Teilen(stuecke, 0, minX, maxX);
            stuecke = Teilen(stuecke, 2, minZ, maxZ);
            foreach (var st in stuecke)
            {
                if (st.Count < 3 || Flaeche(st) <= 1e-10f)
                {
                    continue;
                }
                var m = Vector3.Zero;
                foreach (var e in st)
                {
                    m += new Vector3(e[0], e[1], e[2]);
                }
                m /= st.Count;
                if (!Drin(m))
                {
                    continue;
                }
                var basis = (uint)neu.AnzahlEcken;
                foreach (var e in st)
                {
                    neu._ecken.AddRange(e);
                }
                for (var f = 1; f + 1 < st.Count; f++)
                {
                    neu._indizes.Add(basis);
                    neu._indizes.Add(basis + (uint)f);
                    neu._indizes.Add(basis + (uint)f + 1);
                }
            }
        }
        return neu;
    }

    /// <summary>Beruehrt der Rahmen die Kachel (Randkacheln reichen bis ins Unendliche)?</summary>
    internal static bool Beruehrt(Stadtteile.Kachel k, float minX, float maxX, float minZ, float maxZ, float breite, float tiefe)
    {
        var (spalten, reihen) = Stadtteile.Raster(breite, tiefe);
        var links = k.Spalte == 0 ? float.NegativeInfinity : k.Min.X;
        var rechts = k.Spalte == spalten - 1 ? float.PositiveInfinity : k.Max.X;
        var oben = k.Reihe == 0 ? float.NegativeInfinity : k.Min.Y;
        var unten = k.Reihe == reihen - 1 ? float.PositiveInfinity : k.Max.Y;
        return maxX >= links && minX <= rechts && maxZ >= oben && minZ <= unten;
    }

    /// <summary>Liegt eine Linie des Rasters echt zwischen <paramref name="min"/> und <paramref name="max"/>?</summary>
    private static bool Kreuzt(float min, float max) => MathF.Floor(min / Stadtteile.Groesse) != MathF.Floor(max / Stadtteile.Groesse)
        && LinienZwischen(min, max).Any();

    private static IEnumerable<float> LinienZwischen(float min, float max)
    {
        for (var l = (MathF.Floor(min / Stadtteile.Groesse) + 1) * Stadtteile.Groesse; l < max; l += Stadtteile.Groesse)
        {
            if (l > min)
            {
                yield return l;
            }
        }
    }

    private uint Uebernehmen(Netz neu, Dictionary<uint, uint> abbild, uint alt)
    {
        if (abbild.TryGetValue(alt, out var n))
        {
            return n;
        }
        n = (uint)neu.AnzahlEcken;
        for (var z = 0; z < Zahlen; z++)
        {
            neu._ecken.Add(_ecken[((int)alt * Zahlen) + z]);
        }
        abbild[alt] = n;
        return n;
    }

    private void Lesen(int e, Span<float> ziel)
    {
        for (var z = 0; z < Zahlen; z++)
        {
            ziel[z] = _ecken[(e * Zahlen) + z];
        }
    }

    /// <summary>Jedes Vieleck an allen Rasterlinien der Achse (0 = x, 2 = z) zwischen min und max teilen.</summary>
    private static List<List<float[]>> Teilen(List<List<float[]>> stuecke, int achse, float min, float max)
    {
        foreach (var linie in LinienZwischen(min, max))
        {
            var neu = new List<List<float[]>>();
            foreach (var st in stuecke)
            {
                var (unten, oben) = Schneiden(st, achse, linie);
                if (unten.Count >= 3)
                {
                    neu.Add(unten);
                }
                if (oben.Count >= 3)
                {
                    neu.Add(oben);
                }
            }
            stuecke = neu;
        }
        return stuecke;
    }

    /// <summary>Ein Vieleck an der Ebene achse = wert in zwei Teile schneiden (Sutherland-Hodgman, Reihenfolge bleibt).</summary>
    private static (List<float[]> Unten, List<float[]> Oben) Schneiden(List<float[]> vieleck, int achse, float wert)
    {
        var unten = new List<float[]>();
        var oben = new List<float[]>();
        for (var i = 0; i < vieleck.Count; i++)
        {
            var p = vieleck[i];
            var q = vieleck[(i + 1) % vieleck.Count];
            var dp = p[achse] - wert;
            var dq = q[achse] - wert;
            if (dp <= 0)
            {
                unten.Add(p);
            }
            if (dp >= 0)
            {
                oben.Add(p);
            }
            if ((dp < 0 && dq > 0) || (dp > 0 && dq < 0))
            {
                var t = dp / (dp - dq);
                var s = new float[Zahlen];
                for (var z = 0; z < Zahlen; z++)
                {
                    s[z] = p[z] + ((q[z] - p[z]) * t);
                }
                s[achse] = wert;
                unten.Add(s);
                oben.Add(s);
            }
        }
        return (unten, oben);
    }

    /// <summary>Flaeche eines ebenen Vielecks im Raum (Faecher von der ersten Ecke).</summary>
    private static float Flaeche(List<float[]> st)
    {
        var o = new Vector3(st[0][0], st[0][1], st[0][2]);
        var summe = Vector3.Zero;
        for (var i = 1; i + 1 < st.Count; i++)
        {
            summe += Vector3.Cross(new Vector3(st[i][0], st[i][1], st[i][2]) - o, new Vector3(st[i + 1][0], st[i + 1][1], st[i + 1][2]) - o);
        }
        return summe.Length() / 2f;
    }

    /// <summary>
    /// Welle Nachladen: die Ecken und Dreiecke von <paramref name="andere"/> unveraendert anhaengen (Zahl fuer Zahl, ohne
    /// Umrechnung). So werden Stadtnetz, Paket-Netz und Kleingruen einer Kachel ein Netz und ein Zeichenaufruf je Lauf.
    /// </summary>
    public void Anhaengen(Netz andere)
    {
        var basis = (uint)AnzahlEcken;
        _ecken.AddRange(andere._ecken);
        foreach (var j in andere._indizes)
        {
            _indizes.Add(basis + j);
        }
    }

    /// <summary>Flaeche aller Dreiecke (Tests: alle Kacheln zusammen = ganzes Netz).</summary>
    public double FlaecheGesamt()
    {
        double summe = 0;
        for (var d = 0; d + 2 < _indizes.Count; d += 3)
        {
            var p0 = Ort((int)_indizes[d]);
            summe += Vector3.Cross(Ort((int)_indizes[d + 1]) - p0, Ort((int)_indizes[d + 2]) - p0).Length() / 2.0;
        }
        return summe;
    }
}
