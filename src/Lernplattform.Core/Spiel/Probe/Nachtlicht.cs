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
/// <item>Phase 8 W11 (Nico P8-45): <see cref="Anzahl"/> Strassenlaternen aus dem Graveyard Kit nach Nicos Skizze
/// (<see cref="Skizze"/>), im eigenen Netz (<see cref="StadtSzene.PaketNetzBauen"/>); nur Glas und Leuchtkern leuchten.</item>
/// </list>
/// </summary>
public static class Nachtlicht
{
    /// <summary>Marke auf der Farbe (alle drei Anteile + 4); Schnee hat + 2 (<see cref="Jahreszeitfarben.SchneeMarke"/>).</summary>
    public const float Marke = 4f;

    /// <summary>Phase 8 W11 (Nico 28.10.2026 21:58: "Es sind damit nur noch 21 Laternen"): 16 einfache und 5 Doppel-Laternen.</summary>
    public const int Anzahl = 21, Einfach = 16, Doppelt = 5;

    /// <summary>Anteil leuchtender Glasscheiben an den CC0-Modellen.</summary>
    public const double GlasAnteil = 0.5;

    /// <summary>Schmalere Glasflaechen (zweitgroesste Ausdehnung) leuchten nie: Kanten und Dicke einer Scheibe.</summary>
    public const float GlasMindestens = 0.08f;

    /// <summary>Hoehe einer Laterne (etwa 4 m; 0,72 = 3,6 m Strassenbreite).</summary>
    public const float LaterneHoehe = 0.8f;

    /// <summary>Farbe des Glases der Paket-Laterne (gelb, leuchtet nachts) und des Leuchtkerns.</summary>
    public static readonly Vector3 LampeGlas = new Vector3(255, 192, 68) / 255f;

    private static readonly Vector3 Pfosten = Netz.Farbe("#1F2937");

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

    /// <summary>Abstand des Masts von der Strassenmitte (halbe Strassenbreite und 0,14 Gehweg).</summary>
    public const float Rand = (StadtSzene.StrassenBreite / 2) + 0.14f;

    /// <summary>Kleinster Abstand zwischen zwei Laternen.</summary>
    public const float Zwischen = 1.0f;

    /// <summary>Mindestabstaende des Masts (P8-41/P8-44): Tuer, Weg zur Tuer, Modell und Teil, Fluss.</summary>
    public const float AbstandTuer = 1.5f, AbstandWeg = 0.45f, AbstandModell = 0.2f, AbstandFluss = 0.75f;

    /// <summary>
    /// Nicos Skizze (Weisung P8-45, Anlagen <c>P8-45_Laternen_Nico/01_Laternen_Position_PC.png</c> und
    /// <c>02_Laternen_2_PC.png</c>): blaue Kreise einfach, rot-blaue doppelt. Je Kreis der Ort am Strassenrand
    /// (Westen nach Osten, Norden nach Sueden), uebertragen aus beiden Blickwinkeln.
    /// </summary>
    public static readonly (float X, float Z, bool Doppelt)[] Skizze =
    [
        (7.0f, 0.75f, false), (8.0f, 2.5f, false), (7.0f, 4.3f, false), (8.0f, 7.8f, false), (7.0f, 11.3f, true),
        (8.0f, 13.9f, false), (7.0f, 16.0f, false), (3.6f, 20.3f, true), (0.75f, 21.3f, false), (7.5f, 21.3f, true),
        (9.9f, 21.3f, false), (11.9f, 20.3f, false), (18.6f, 21.3f, true), (18.9f, 12.0f, true), (17.9f, 14.2f, false),
        (18.9f, 16.8f, false), (22.9f, 20.3f, false), (27.1f, 21.3f, false), (31.25f, 21.3f, false), (30.3f, 14.65f, false),
        (30.3f, 18.5f, false),
    ];

    /// <summary>
    /// Eine Laterne: Nummer (1 bis 21 wie in der Skizze), Mast unten auf <paramref name="Ort"/>, Arm-Richtung (einfach zur
    /// Strasse, doppelt laengs der Strasse), Ziel aus der Skizze und wie weit der Platz davon liegt.
    /// </summary>
    public sealed record Laterne(int Nr, Vector2 Ort, bool Doppelt, Vector2 Richtung, Vector2 Ziel)
    {
        public float Verschoben => Vector2.Distance(Ort, Ziel);
    }

    /// <summary>
    /// Plaetze der Laternen: jeder Kreis der Skizze auf den naechsten erlaubten Platz am Strassenrand (Abstand
    /// <see cref="Rand"/> von der Strassenmitte, Schritt 0,05; <see cref="Regeln"/>), mindestens <see cref="Zwischen"/>
    /// von den schon gesetzten. Die Plaetze haengen nicht an der Festzeit (die Deko aller Festzeiten bleibt frei) und
    /// nicht an der Jahreszeit; fest fuer dieselbe Stadt.
    /// </summary>
    public static List<Laterne> Plaetze(StadtSzene szene)
    {
        var regeln = new Regeln(szene);
        var kandidaten = new List<(Vector2 Q, Vector2 Laengs, Vector2 ZurStrasse)>();
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
                var schritte = (int)MathF.Floor(laenge / 0.05f);
                for (var k = 0; k <= schritte; k++)
                {
                    var p = a + (d * (k * 0.05f));
                    foreach (var seite in (float[])[1f, -1f])
                    {
                        kandidaten.Add((p + (quer * Rand * seite), d, -quer * seite));
                    }
                }
            }
        }
        var liste = new List<Laterne>();
        for (var nr = 0; nr < Skizze.Length; nr++)
        {
            var (x, z, doppelt) = Skizze[nr];
            var ziel = new Vector2(x, z);
            foreach (var (q, laengs, zurStrasse) in kandidaten.OrderBy(k => Vector2.DistanceSquared(k.Q, ziel)))
            {
                if (liste.Any(l => Vector2.Distance(l.Ort, q) < Zwischen) || !regeln.Pruefen(q).Frei)
                {
                    continue;
                }
                liste.Add(new Laterne(nr + 1, q, doppelt, doppelt ? laengs : zurStrasse, ziel));
                break;
            }
        }
        return liste;
    }

    /// <summary>
    /// Regeln eines Laternenplatzes (P8-41/P8-44, Sammelliste 158), als Tests: nicht vor einer Tuer oder auf dem Weg zur
    /// Tuer, nicht in oder an Bruecke, Fluss, Haus, Baum, Busch, Stein, Beet, Zaun, Bank, Schild, Haltestelle, Deko
    /// (auch der Deko jeder Festzeit), Gleis und Fahrbahn. Die Deko der heutigen Festzeit zaehlt ueber die Plaetze
    /// aller Festzeiten (so steht jede Laterne an jedem Tag gleich).
    /// </summary>
    public sealed class Regeln
    {
        private readonly StadtSzene _szene;
        private readonly List<(string Name, Vector2 Min, Vector2 Max)> _kaesten = [];

        public Regeln(StadtSzene szene)
        {
            _szene = szene;
            for (var i = 0; i < Math.Min(szene.Stuecke.Count, szene.FestStueckeVon); i++)
            {
                var s = szene.Stuecke[i];
                if (szene.Grafik?.Modell(s.Name) is not { } m)
                {
                    continue;
                }
                var min = new Vector2(float.MaxValue);
                var max = new Vector2(float.MinValue);
                foreach (var x in (float[])[m.Min.X, m.Max.X])
                {
                    foreach (var z in (float[])[m.Min.Z, m.Max.Z])
                    {
                        var w = Vector3.Transform(new Vector3(x, 0, z), s.Lage);
                        min = Vector2.Min(min, new Vector2(w.X, w.Z));
                        max = Vector2.Max(max, new Vector2(w.X, w.Z));
                    }
                }
                _kaesten.Add((s.Name, min, max));
            }
            for (var i = 0; i < Math.Min(szene.Teile.Count, szene.FestTeileVon); i++)
            {
                var t = szene.Teile[i];
                if (t.Art == TeilArt.Band || MathF.Max(t.Y1, t.Y2) < 0.12f)
                {
                    continue;
                }
                _kaesten.Add(("Teil " + t.Art, new Vector2(MathF.Min(t.X1, t.X2), MathF.Min(t.Z1, t.Z2)), new Vector2(MathF.Max(t.X1, t.X2), MathF.Max(t.Z1, t.Z2))));
            }
        }

        /// <summary>Ist der Platz frei? Sonst der erste verletzte Grund und sein Abstand.</summary>
        public (bool Frei, string Grund, float Abstand) Pruefen(Vector2 q)
        {
            var sz = _szene;
            if (q.X < 0.3f || q.Y < 0.3f || q.X > sz.Breite - 0.3f || q.Y > sz.Tiefe - 0.3f)
            {
                return (false, "Kartenrand", 0);
            }
            foreach (var (linie, halb) in sz.BaumSperren)
            {
                var d = Kurve.Abstand(q, linie) - halb;
                if (d < 0.08f)
                {
                    return (false, halb >= (StadtSzene.StrassenBreite / 2) - 0.01f ? "Fahrbahn oder Gleis" : "Fussweg", d);
                }
            }
            foreach (var h in sz.Haeuser.Select(h => (h.Min, h.Max)).Concat(sz.Orte.Select(o => (Min: o.KastenMin, Max: o.KastenMax))))
            {
                var dx = MathF.Max(MathF.Max(h.Min.X - q.X, 0), q.X - h.Max.X);
                var dz = MathF.Max(MathF.Max(h.Min.Z - q.Y, 0), q.Y - h.Max.Z);
                var d = MathF.Sqrt((dx * dx) + (dz * dz));
                if (d < AbstandModell)
                {
                    return (false, "Haus", d);
                }
            }
            foreach (var (m, r) in sz.Baeume)
            {
                var d = Vector2.Distance(q, m) - r;
                if (d < 0.1f)
                {
                    return (false, "Baum", d);
                }
            }
            foreach (var t in sz.Tueren)
            {
                var d = MathF.Min(Vector2.Distance(q, t.Mitte), Vector2.Distance(q, t.WegAnfang));
                if (d < AbstandTuer)
                {
                    return (false, "Tuer " + t.Ort, d);
                }
                var w = Kurve.Abstand(q, (IReadOnlyList<Vector2>)[t.Mitte, t.WegAnfang]);
                if (w < AbstandWeg)
                {
                    return (false, "Weg zur Tuer " + t.Ort, w);
                }
            }
            foreach (var (name, min, max) in _kaesten)
            {
                var dx = MathF.Max(MathF.Max(min.X - q.X, 0), q.X - max.X);
                var dz = MathF.Max(MathF.Max(min.Y - q.Y, 0), q.Y - max.Y);
                var d = MathF.Sqrt((dx * dx) + (dz * dz));
                if (d < AbstandModell)
                {
                    return (false, name, d);
                }
            }
            foreach (var (art, gx, gz, gr) in sz.GruenKlein?.Plaetze ?? [])
            {
                var d = Vector2.Distance(q, new Vector2(gx, gz)) - gr;
                if (d < AbstandModell)
                {
                    return (false, "Gruen " + art, d);
                }
            }
            foreach (var (p, r) in sz.FestPlaetzeAlle)
            {
                var d = Vector2.Distance(q, p) - r;
                if (d < 0.15f)
                {
                    return (false, "Fest-Deko", d);
                }
            }
            foreach (var u in sz.Uebergaenge)
            {
                var d = Vector2.Distance(q, u.Mitte) - 0.5f;
                if (d < AbstandModell)
                {
                    return (false, "Bahnuebergang", d);
                }
            }
            foreach (var b in sz.Bruecken)
            {
                var d = Vector2.Distance(q, b) - 1.2f;
                if (d < AbstandModell)
                {
                    return (false, "Bruecke", d);
                }
            }
            foreach (var f in sz.Fluesse)
            {
                var d = Kurve.Abstand(q, f) - (StadtSzene.FlussBreite / 2);
                if (d < AbstandFluss)
                {
                    return (false, "Fluss", d);
                }
            }
            return (true, "-", 0);
        }
    }

    /// <summary>Hoehe des Paket-Modells der Laterne (etwas hoeher als die W10-Laterne).</summary>
    public const float PaketHoehe = LaterneHoehe * 1.15f;

    /// <summary>Lage des Paket-Modells einer Laterne (Anker am Mast, Modell-Z = Arm).</summary>
    public static Matrix4x4 Lage(GrafikModell m, Laterne l)
    {
        var s = PaketHoehe / (m.Max.Y - m.Min.Y);
        return StadtSzene.Lage(m, new Vector3(0, m.Min.Y, 0), new Vector3(s), new Vector3(l.Ort.X, 0, l.Ort.Y), l.Richtung);
    }

    /// <summary>Name des Paket-Modells einer Laterne.</summary>
    public static string Modell(Laterne l) => l.Doppelt ? "gy-lightpost-double" : "gy-lightpost-single";

    /// <summary>
    /// Eine Laterne ins Netz: nah das Paket-Modell, nur das gelbe Glas leuchtet nachts, dazu je Lampe ein Leuchtkern
    /// (Kasten der Glasflaechen, quer etwas groesser), damit die Lampe nachts deutlich leuchtet (P8-42); fern ein Mast
    /// und je Lampe ein leuchtender Wuerfel.
    /// </summary>
    public static void Hinzu(Netz netz, GrafikBibliothek grafik, Laterne l, bool fern)
    {
        if (grafik.Modell(Modell(l)) is not { } m)
        {
            return;
        }
        var lage = Lage(m, l);
        var kaesten = GlasKaesten(m, LampeGlas);
        if (fern)
        {
            const float mast = 0.025f;
            netz.Hinzu(new Teil(TeilArt.Quader, l.Ort.X - mast, 0, l.Ort.Y - mast, l.Ort.X + mast, PaketHoehe * 0.9f, l.Ort.Y + mast, Pfosten));
            foreach (var (kmin, kmax) in kaesten)
            {
                var c = Vector3.Transform((kmin + kmax) / 2, lage);
                const float halb = 0.045f;
                var von = netz.AnzahlEcken;
                netz.Hinzu(new Teil(TeilArt.Quader, c.X - halb, c.Y - halb, c.Z - halb, c.X + halb, c.Y + halb, c.Z + halb, LampeGlas));
                netz.Leuchten(von, netz.AnzahlEcken);
            }
            return;
        }
        netz.Hinzu(m, lage, (i, _, _) =>
        {
            var f = new Vector3(m.Farben[i * 3], m.Farben[(i * 3) + 1], m.Farben[(i * 3) + 2]) / 255f;
            return IstGlas(f) ? f + new Vector3(Marke) : f;
        });
        foreach (var (kmin, kmax) in kaesten)
        {
            var mitte = (kmin + kmax) / 2;
            var halb = (kmax - kmin) / 2 * new Vector3(1.12f, 0.92f, 1.12f);
            var von = netz.AnzahlEcken;
            netz.Hinzu(Kasten(mitte - halb, mitte + halb, LampeGlas), lage);
            netz.Leuchten(von, netz.AnzahlEcken);
        }
    }

    /// <summary>Ist die Farbe das gelbe Glas der Paket-Laterne?</summary>
    public static bool IstGlas(Vector3 farbe) => Vector3.Distance(farbe, LampeGlas) < 0.05f;

    /// <summary>Kaesten der Glasflaechen (Farbe <paramref name="glas"/>), getrennt nach der Seite (Modell-Z).</summary>
    public static List<(Vector3 Min, Vector3 Max)> GlasKaesten(GrafikModell m, Vector3 glas)
    {
        var gruppen = new SortedDictionary<int, (Vector3 Min, Vector3 Max)>();
        for (var d = 0; d < m.Dreiecke; d++)
        {
            var f = new Vector3(m.Farben[d * 3], m.Farben[(d * 3) + 1], m.Farben[(d * 3) + 2]) / 255f;
            if (Vector3.Distance(f, glas) >= 0.05f)
            {
                continue;
            }
            for (var e = 0; e < 3; e++)
            {
                var o = new Vector3(m.Ecken[(d * 9) + (e * 3)], m.Ecken[(d * 9) + (e * 3) + 1], m.Ecken[(d * 9) + (e * 3) + 2]);
                var k = o.Z > 0.05f ? 1 : o.Z < -0.05f ? -1 : 0;
                gruppen[k] = gruppen.TryGetValue(k, out var g) ? (Vector3.Min(g.Min, o), Vector3.Max(g.Max, o)) : (o, o);
            }
        }
        return [.. gruppen.Values];
    }

    /// <summary>Kasten als Modell (12 Dreiecke) in einer Farbe.</summary>
    private static GrafikModell Kasten(Vector3 a, Vector3 b, Vector3 farbe)
    {
        var e = new List<float>();
        Vector3 P(int i) => new((i & 1) == 0 ? a.X : b.X, (i & 2) == 0 ? a.Y : b.Y, (i & 4) == 0 ? a.Z : b.Z);
        int[][] seiten = [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]];
        foreach (var s in seiten)
        {
            foreach (var i in (int[])[s[0], s[1], s[2], s[0], s[2], s[3]])
            {
                var p = P(i);
                e.AddRange([p.X, p.Y, p.Z]);
            }
        }
        var f = new byte[12 * 3];
        for (var i = 0; i < 12; i++)
        {
            f[i * 3] = (byte)MathF.Round(farbe.X * 255);
            f[(i * 3) + 1] = (byte)MathF.Round(farbe.Y * 255);
            f[(i * 3) + 2] = (byte)MathF.Round(farbe.Z * 255);
        }
        return new GrafikModell("kasten", [.. e], f);
    }
}
