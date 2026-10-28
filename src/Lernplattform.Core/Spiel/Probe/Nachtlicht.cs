using System.Globalization;
using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 8 W10 (Sammelliste 150, Nico 3b): was nachts in der Stadt leuchtet. Nur Darstellung (kein Ereignis, kein
/// Spielwert, Fingerabdruecke bleiben): beim Bau der Netze bekommen leuchtende Flaechen die Marke
/// <see cref="Marke"/> auf die Farbe (<see cref="Netz.Leuchten"/>), der Shader mischt sie nachts zum Lampenlicht.
/// Keine echten Lichtquellen, keine Schatten, kein eigener Zeichenaufruf.
/// <list type="bullet">
/// <item>Eigene Bloecke: die Fenster, in denen schon am Tag Licht brennt (Wuerfel <see cref="StadtSzene.Licht"/>).</item>
/// <item>CC0-Modelle: je Glasscheibe (zusammenhaengende Glasflaeche einer Richtung) ein fester Wuerfel, etwa jede
/// zweite; schmale Glaskanten (Dicke einer Scheibe, Seitenkante der Bank) leuchten nie.</item>
/// <item><see cref="Anzahl"/> Strassenlaternen am Strassenrand, je zwei Quader (24 Dreiecke): Pfosten und Lampe.</item>
/// </list>
/// </summary>
public static class Nachtlicht
{
    /// <summary>Marke auf der Farbe (alle drei Anteile + 4); Schnee hat + 2 (<see cref="Jahreszeitfarben.SchneeMarke"/>).</summary>
    public const float Marke = 4f;

    /// <summary>So viele Strassenlaternen (Nico 3b: etwa 30, hoechstens etwa +1 000 Dreiecke).</summary>
    public const int Anzahl = 30;

    /// <summary>Dreiecke je Laterne (zwei Quader).</summary>
    public const int DreieckeJeLaterne = 24;

    /// <summary>Anteil leuchtender Glasscheiben an den CC0-Modellen.</summary>
    public const double GlasAnteil = 0.5;

    /// <summary>Schmalere Glasflaechen (zweitgroesste Ausdehnung) leuchten nie: Kanten und Dicke einer Scheibe.</summary>
    public const float GlasMindestens = 0.08f;

    /// <summary>Hoehe einer Laterne (etwa 4 m; 0,72 = 3,6 m Strassenbreite).</summary>
    public const float LaterneHoehe = 0.8f;

    private static readonly Vector3 Pfosten = Netz.Farbe("#1F2937");
    private static readonly Vector3 Lampe = Netz.Farbe("#FDE68A");

    /// <summary>Ist die Farbe mit der Marke versehen?</summary>
    public static bool Leuchtet(Vector3 farbe) => farbe.X >= Marke - 0.5f;

    /// <summary>
    /// Glasscheiben eines eben eingefuegten CC0-Modells (Dreiecke ab Index <paramref name="von"/> in
    /// <see cref="Netz.Indizes"/>) markieren:
    /// Dreiecke mit Glasfarbe (<see cref="StadtTueren.IstGlas"/>), die eine Ecke teilen und in dieselbe Richtung
    /// zeigen, bilden eine Scheibe; der Wuerfel aus <paramref name="seed"/> und der Mitte entscheidet je Scheibe.
    /// Rueckgabe: Zahl der leuchtenden Scheiben.
    /// </summary>
    public static int GlasMarkieren(Netz netz, int von, string seed)
    {
        var indizes = netz.IndizesFeld;
        var dreiecke = new List<int>();
        for (var i = von; i + 2 < indizes.Length; i += 3)
        {
            if (StadtTueren.IstGlas(netz.FarbeAn((int)indizes[i])))
            {
                dreiecke.Add(i);
            }
        }
        if (dreiecke.Count == 0)
        {
            return 0;
        }
        // Scheiben: Vereinigung ueber gemeinsame Ecken (auf 1/1000 gerundet) bei gleicher Richtung
        var eltern = Enumerable.Range(0, dreiecke.Count).ToArray();
        int Wurzel(int i)
        {
            while (eltern[i] != i)
            {
                eltern[i] = eltern[eltern[i]];
                i = eltern[i];
            }
            return i;
        }
        var erste = new Dictionary<(int, int, int, int, int, int), int>();
        for (var i = 0; i < dreiecke.Count; i++)
        {
            var n = netz.Normale((int)indizes[dreiecke[i]]);
            var richtung = ((int)MathF.Round(n.X * 20), (int)MathF.Round(n.Y * 20), (int)MathF.Round(n.Z * 20));
            for (var k = 0; k < 3; k++)
            {
                var p = netz.Ort((int)indizes[dreiecke[i] + k]);
                var schluessel = ((int)MathF.Round(p.X * 1000), (int)MathF.Round(p.Y * 1000), (int)MathF.Round(p.Z * 1000),
                    richtung.Item1, richtung.Item2, richtung.Item3);
                if (erste.TryGetValue(schluessel, out var j))
                {
                    eltern[Wurzel(i)] = Wurzel(j);
                }
                else
                {
                    erste[schluessel] = i;
                }
            }
        }
        var scheiben = new Dictionary<int, (Vector3 Min, Vector3 Max, List<int> Dreiecke)>();
        for (var i = 0; i < dreiecke.Count; i++)
        {
            var w = Wurzel(i);
            if (!scheiben.TryGetValue(w, out var s))
            {
                s = (new Vector3(float.MaxValue), new Vector3(float.MinValue), []);
            }
            for (var k = 0; k < 3; k++)
            {
                var p = netz.Ort((int)indizes[dreiecke[i] + k]);
                s.Min = Vector3.Min(s.Min, p);
                s.Max = Vector3.Max(s.Max, p);
            }
            s.Dreiecke.Add(dreiecke[i]);
            scheiben[w] = s;
        }
        var an = 0;
        var ecken = new HashSet<int>();
        foreach (var (min, max, liste) in scheiben.Values)
        {
            var mass = max - min;
            Span<float> seiten = [mass.X, mass.Y, mass.Z];
            seiten.Sort();
            if (seiten[1] < GlasMindestens)
            {
                continue;
            }
            var mitte = (min + max) / 2;
            var name = string.Create(CultureInfo.InvariantCulture, $"{seed}|{MathF.Round(mitte.X, 2)}|{MathF.Round(mitte.Y, 2)}|{MathF.Round(mitte.Z, 2)}");
            if (!StadtSzene.Licht(name, 0, GlasAnteil))
            {
                continue;
            }
            foreach (var d in liste)
            {
                for (var k = 0; k < 3; k++)
                {
                    ecken.Add((int)indizes[d + k]);
                }
            }
            an++;
        }
        foreach (var e in ecken)
        {
            netz.Leuchten(e, e + 1);
        }
        return an;
    }

    /// <summary>Eine Laterne: Pfosten und Lampe (leuchtet nachts), unten mittig auf (x, z).</summary>
    public static void Laterne(Netz netz, float x, float z)
    {
        const float pfosten = 0.022f;
        const float lampe = 0.055f;
        var h = LaterneHoehe;
        netz.Hinzu(new Teil(TeilArt.Quader, x - pfosten, 0, z - pfosten, x + pfosten, h - (2 * lampe), z + pfosten, Pfosten));
        var von = netz.AnzahlEcken;
        netz.Hinzu(new Teil(TeilArt.Quader, x - lampe, h - (2 * lampe), z - lampe, x + lampe, h, z + lampe, Lampe));
        netz.Leuchten(von, netz.AnzahlEcken);
    }

    /// <summary>
    /// Plaetze der Laternen: am Rand jeder Strasse (Schritt 0,5), nicht auf Strassen, Fusswegen, Gleis, Fluss,
    /// in Haeusern, an Baeumen, vor Tueren oder an der Fest-Deko; davon <see cref="Anzahl"/> gleichmaessig
    /// verteilt (immer der Platz am weitesten von allen schon gewaehlten, Beginn nahe der Mitte). Fest fuer
    /// dieselbe Stadt.
    /// </summary>
    public static List<Vector2> Plaetze(StadtSzene szene, int anzahl = Anzahl)
    {
        var abstand = (StadtSzene.StrassenBreite / 2) + 0.14f;
        var kandidaten = new List<Vector2>();
        var kaesten = szene.Haeuser.Select(h => (h.Min, h.Max)).Concat(szene.Orte.Select(o => (o.KastenMin, o.KastenMax))).ToList();
        var tueren = szene.Tueren.Select(t => t.WegAnfang).Concat(szene.Tueren.Select(t => t.Mitte)).ToList();
        foreach (var strasse in szene.Strassen)
        {
            for (var i = 0; i + 1 < strasse.Count; i++)
            {
                var a = strasse[i];
                var b = strasse[i + 1];
                var laenge = Vector2.Distance(a, b);
                if (laenge < 1e-4f)
                {
                    continue;
                }
                var d = (b - a) / laenge;
                var quer = new Vector2(-d.Y, d.X);
                for (var s = 0.25f; s < laenge; s += 0.5f)
                {
                    var p = a + (d * s);
                    foreach (var seite in (float[])[1f, -1f])
                    {
                        var q = p + (quer * abstand * seite);
                        if (Frei(szene, q, kaesten, tueren))
                        {
                            kandidaten.Add(q);
                        }
                    }
                }
            }
        }
        var gewaehlt = new List<Vector2>();
        if (kandidaten.Count == 0)
        {
            return gewaehlt;
        }
        var mitte = new Vector2(szene.Breite / 2, szene.Tiefe / 2);
        var naechste = new float[kandidaten.Count];
        var start = 0;
        for (var i = 1; i < kandidaten.Count; i++)
        {
            if (Vector2.DistanceSquared(kandidaten[i], mitte) < Vector2.DistanceSquared(kandidaten[start], mitte))
            {
                start = i;
            }
        }
        Array.Fill(naechste, float.MaxValue);
        var neu = start;
        while (gewaehlt.Count < anzahl)
        {
            gewaehlt.Add(kandidaten[neu]);
            var weiteste = -1;
            for (var i = 0; i < kandidaten.Count; i++)
            {
                naechste[i] = MathF.Min(naechste[i], Vector2.DistanceSquared(kandidaten[i], kandidaten[neu]));
                if (naechste[i] > 0.36f && (weiteste < 0 || naechste[i] > naechste[weiteste]))
                {
                    weiteste = i;
                }
            }
            if (weiteste < 0)
            {
                break;
            }
            neu = weiteste;
        }
        return gewaehlt;
    }

    private static bool Frei(StadtSzene szene, Vector2 q, List<(Vector3 Min, Vector3 Max)> kaesten, List<Vector2> tueren)
    {
        if (q.X < 0.3f || q.Y < 0.3f || q.X > szene.Breite - 0.3f || q.Y > szene.Tiefe - 0.3f)
        {
            return false;
        }
        foreach (var (linie, halb) in szene.BaumSperren)
        {
            if (Kurve.Abstand(q, linie) < halb + 0.08f)
            {
                return false;
            }
        }
        foreach (var (min, max) in kaesten)
        {
            var dx = MathF.Max(MathF.Max(min.X - q.X, 0), q.X - max.X);
            var dz = MathF.Max(MathF.Max(min.Z - q.Y, 0), q.Y - max.Z);
            if ((dx * dx) + (dz * dz) < 0.15f * 0.15f)
            {
                return false;
            }
        }
        foreach (var (m, r) in szene.Baeume)
        {
            if (Vector2.Distance(q, m) < r + 0.1f)
            {
                return false;
            }
        }
        foreach (var t in tueren)
        {
            if (Vector2.Distance(q, t) < 0.6f)
            {
                return false;
            }
        }
        foreach (var f in szene.FestGesetzt)
        {
            if (Vector2.Distance(q, new Vector2(f.X, f.Z)) < f.R + 0.15f)
            {
                return false;
            }
        }
        return true;
    }
}
