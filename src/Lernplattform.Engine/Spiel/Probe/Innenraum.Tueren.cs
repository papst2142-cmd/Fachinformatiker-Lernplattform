using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Eine Raumtuer im Grundriss (Welle A 1.6.0, Sammelliste 443): Lage in der Wand und Seite des Raums.</summary>
/// <param name="Nr">Nummer im Innenraum (Reihenfolge der Raeume).</param>
/// <param name="Raum">Kennung des Raums.</param>
/// <param name="Waagerecht">Die Wand laeuft in x-Richtung (Tuer im Norden oder Sueden des Raums).</param>
/// <param name="Linie">Lage der Wandmitte quer zur Wand (z bei waagerecht, sonst x).</param>
/// <param name="Von">Anfang der Oeffnung laengs der Wand.</param>
/// <param name="Bis">Ende der Oeffnung laengs der Wand.</param>
/// <param name="Innen">Richtung quer zur Wand, in der der Raum liegt (+1 oder -1).</param>
public sealed record RaumTuer(int Nr, string Raum, bool Waagerecht, float Linie, float Von, float Bis, float Innen)
{
    /// <summary>Mitte der Oeffnung (x, z).</summary>
    public Vector2 Mitte => Waagerecht ? new Vector2((Von + Bis) / 2, Linie) : new Vector2(Linie, (Von + Bis) / 2);

    /// <summary>Punkt (laengs, quer) in (x, z).</summary>
    public Vector2 Punkt(float laengs, float quer) => Waagerecht ? new Vector2(laengs, quer) : new Vector2(quer, laengs);

    /// <summary>(laengs, quer) eines Punkts (x, z).</summary>
    public (float Laengs, float Quer) Lokal(float x, float z) => Waagerecht ? (x, z) : (z, x);
}

/// <summary>Lage eines offenen Tuerblatts: Angel an <c>von</c> oder <c>bis</c>, aufgeschwenkt zur Seite +1 oder -1 quer zur Wand.</summary>
public readonly record struct TuerOffen(bool AngelVon, float Seite);

/// <summary>
/// Welle A 1.6.0 (Sammelliste 443, Weisungen P12-359/P12-360, Nico 1c 2a): echte Tueren im Innenraum. Jeder Ausgang ist eine
/// geschlossene Tuer (Zarge, Sturz, Blatt, Griff; hinaus fuehrt die Handlung an der Tuer). Jede Raumtuer hat Zarge und Sturz in
/// der Wand (feste Teile, wie ein Wandstueck) und ein Tuerblatt, das zu ist, bis die Figur es oeffnet (<see cref="TuerBlattNetz"/>,
/// offen 90 Grad zu der Seite, auf der es kein Einrichtungsstueck beruehrt). Offene Bereiche (<c>tuer.offen</c>) und breite
/// Durchgaenge (breiter als <see cref="TuerHoechstens"/>) bleiben ohne Tuer (Nico 2a). Nur Darstellung und Sperre der Figur im
/// Spiel: Grundrisse, Laufraster und Spielkern bleiben unveraendert. Gleich im Spiel und im Werkzeug (gemeinsame Engine).
/// </summary>
public sealed partial class Innenraum
{
    /// <summary>Breiteste Oeffnung (Einheiten), die eine Tuer bekommt; breitere Durchgaenge bleiben offen (Nico 2a).</summary>
    public const float TuerHoechstens = 2.5f;

    /// <summary>Tuerblatt (Holz).</summary>
    public static readonly Vector3 TuerBlattFarbe = Netz.Farbe("#8A5A36");

    /// <summary>Zarge (Rahmen).</summary>
    public static readonly Vector3 TuerZargeFarbe = Netz.Farbe("#5B5470");

    /// <summary>Griff.</summary>
    public static readonly Vector3 TuerGriffFarbe = Netz.Farbe("#C9CED6");

    /// <summary>Hoehe der Tueroeffnung (darueber der Sturz in Wandfarbe).</summary>
    public const float TuerOben = WandHoehe * 0.86f;

    /// <summary>Breite der Zarge laengs der Wand.</summary>
    public const float ZargeBreite = 0.06f;

    /// <summary>Dicke der Zarge quer zur Wand (halb, wie die Wand).</summary>
    internal const float ZargeHalb = 0.08f;

    /// <summary>Dicke des Tuerblatts (halb).</summary>
    internal const float BlattHalb = 0.035f;

    /// <summary>
    /// So nah (quer zur Wand, Mitte der Figur) kommt die Figur an ein geschlossenes Tuerblatt: halbe Dicke des Blatts plus Koerper
    /// der Figur (<see cref="Wege.Koerper"/> 0,3), damit sie das Blatt nie durchdringt.
    /// </summary>
    public const float TuerSperreQuer = BlattHalb + 0.3f;

    private readonly List<RaumTuer> _raumTueren = [];

    /// <summary>Alle Raumtueren dieses Innenraums (ohne offene Bereiche und breite Durchgaenge).</summary>
    public IReadOnlyList<RaumTuer> RaumTueren => _raumTueren;

    /// <summary>Ausgang (Eingangstuer in der Westwand, laengs z von..bis), null im Obergeschoss.</summary>
    public (float Von, float Bis)? Ausgang { get; private set; }

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): Innenraeume wie bis A 1.5.0, ohne Tueren (Ausgang und Raeume offen).</summary>
    public static bool OhneTuerenFuerTests { get => OhneTueren.Value; set => OhneTueren.Value = value; }

    private static readonly AsyncLocal<bool> OhneTueren = new();

    /// <summary>Raumtueren aus dem Grundriss lesen und ihre Zarge mit Sturz als Wandstueck bauen (nach den Innenwaenden).</summary>
    private void RaumTuerenBauen()
    {
        if (OhneTueren.Value)
        {
            return;
        }
        foreach (var raum in Liste(Gebaeude["raeume"]).Select(Objekt))
        {
            if (Get(raum, "tuer") is not PyObjekt tuer || tuer.Count == 0 || Py.Wahr(Get(tuer, "offen", false)))
            {
                continue;
            }
            float von = (float)Py.F(tuer["von"]), bis = (float)Py.F(tuer["bis"]);
            if (bis - von > TuerHoechstens || bis - von < 3 * ZargeBreite)
            {
                continue;
            }
            float x = (float)Py.F(raum["x"]), y = (float)Py.F(raum["y"]), w = (float)Py.F(raum["w"]), h = (float)Py.F(raum["h"]);
            var seite = Wege.TuerSeite(raum, Gebaeude);
            var waagerecht = seite is "n" or "s";
            var linie = seite switch { "n" => y, "s" => y + h, "w" => x, _ => x + w };
            var t = new RaumTuer(_raumTueren.Count, Py.Str(raum["id"]), waagerecht, linie, von, bis, seite is "n" or "w" ? 1f : -1f);
            _raumTueren.Add(t);
            var erstes = _teile.Count;
            _teile.Add(TuerQuader(t, von, TuerOben, -ZargeHalb, bis, WandHoehe - 0.06f, ZargeHalb, WandFarbe));
            _teile.Add(TuerQuader(t, von, WandHoehe - 0.06f, -ZargeHalb, bis, WandHoehe, ZargeHalb,
                WandFarbe == Farben.Wand ? Farben.WandKante : Mischen(WandFarbe, Vector3.Zero, 0.2f)));
            _teile.Add(TuerQuader(t, von, 0, -ZargeHalb, von + ZargeBreite, TuerOben, ZargeHalb, TuerZargeFarbe));
            _teile.Add(TuerQuader(t, bis - ZargeBreite, 0, -ZargeHalb, bis, TuerOben, ZargeHalb, TuerZargeFarbe));
            _teile.Add(TuerQuader(t, von, TuerOben - ZargeBreite, -ZargeHalb, bis, TuerOben, ZargeHalb, TuerZargeFarbe));
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            for (var i = erstes; i < _teile.Count; i++)
            {
                min = Vector3.Min(min, new Vector3(_teile[i].X1, _teile[i].Y1, _teile[i].Z1));
                max = Vector3.Max(max, new Vector3(_teile[i].X2, _teile[i].Y2, _teile[i].Z2));
            }
            _waende.Add(new InnenWand(erstes, _teile.Count, min, max, Vector2.Zero) { Tuer = t });
        }
    }

    /// <summary>Waagrechte Kaesten (min, max in X/Z) der zwei Pfosten der Zarge (alles andere der Zarge liegt ueber dem Kopf).</summary>
    public static ((Vector2 Min, Vector2 Max) Von, (Vector2 Min, Vector2 Max) Bis) TuerPfosten(RaumTuer t)
    {
        (Vector2, Vector2) Kasten(float a1, float a2) => t.Waagerecht
            ? (new Vector2(a1, t.Linie - ZargeHalb), new Vector2(a2, t.Linie + ZargeHalb))
            : (new Vector2(t.Linie - ZargeHalb, a1), new Vector2(t.Linie + ZargeHalb, a2));
        return (Kasten(t.Von, t.Von + ZargeBreite), Kasten(t.Bis - ZargeBreite, t.Bis));
    }

    /// <summary>Quader in Koordinaten der Tuer: a laengs der Wand, h Hoehe, q quer zur Wand (relativ zur Wandmitte).</summary>
    private static Teil TuerQuader(RaumTuer t, float a1, float h1, float q1, float a2, float h2, float q2, Vector3 farbe) =>
        t.Waagerecht
            ? Quader(a1, h1, t.Linie + q1, a2, h2, t.Linie + q2, farbe)
            : Quader(t.Linie + q1, h1, a1, t.Linie + q2, h2, a2, farbe);

    /// <summary>Ausgangstuer (zu): Sturz, Zarge, Blatt und Griff innen. Achse x = 0 (Westwand), laengs z von..bis.</summary>
    private void AusgangTuer(float von, float bis, Vector3 wandFarbe, float a0, float a1)
    {
        const float z = 0.07f;
        _teile.Add(Quader(a0, TuerOben, von, a1, WandHoehe, bis, wandFarbe));
        _teile.Add(Quader(a0, 0, von, a1, TuerOben, von + z, TuerZargeFarbe));
        _teile.Add(Quader(a0, 0, bis - z, a1, TuerOben, bis, TuerZargeFarbe));
        _teile.Add(Quader(a0, TuerOben - z, von, a1, TuerOben, bis, TuerZargeFarbe));
        _teile.Add(Quader(-0.05f, 0.01f, von + z, 0.05f, TuerOben - z, bis - z, TuerBlattFarbe));
        // Griff innen, gegenueber der Angel
        var gz = bis - z - 0.16f;
        var gy = TuerOben * 0.52f;
        _teile.Add(Quader(0.05f, gy - 0.03f, gz - 0.16f, 0.11f, gy + 0.03f, gz, TuerGriffFarbe));
    }

    /// <summary>Teile des Tuerblatts (zu oder offen in <paramref name="offen"/>) mit Griff.</summary>
    public IEnumerable<Teil> TuerBlattTeile(RaumTuer t, TuerOffen? offen)
    {
        var gy = TuerOben * 0.52f;
        if (offen is not { } o)
        {
            yield return TuerQuader(t, t.Von + ZargeBreite, 0.01f, -BlattHalb, t.Bis - ZargeBreite, TuerOben - ZargeBreite, BlattHalb, TuerBlattFarbe);
            // Griff auf beiden Seiten, gegenueber der Angel (an „von“)
            yield return TuerQuader(t, t.Bis - ZargeBreite - 0.3f, gy - 0.03f, -0.09f, t.Bis - ZargeBreite - 0.12f, gy + 0.03f, 0.09f, TuerGriffFarbe);
            yield break;
        }
        var (b1, b2) = OffenesBlatt(t, o);
        yield return TuerQuader(t, b1.A1, 0.01f, b1.Q1, b1.A2, TuerOben - ZargeBreite, b1.Q2, TuerBlattFarbe);
        yield return TuerQuader(t, b2.A1, gy - 0.03f, b2.Q1, b2.A2, gy + 0.03f, b2.Q2, TuerGriffFarbe);
    }

    /// <summary>Rechteck (laengs a, quer q relativ zur Wandmitte) des offenen Blatts und seines Griffs.</summary>
    private static ((float A1, float Q1, float A2, float Q2) Blatt, (float A1, float Q1, float A2, float Q2) Griff) OffenesBlatt(RaumTuer t, TuerOffen o)
    {
        var tief = t.Bis - t.Von - (2 * ZargeBreite);
        var a1 = o.AngelVon ? t.Von + ZargeBreite : t.Bis - ZargeBreite - (2 * BlattHalb);
        var a2 = a1 + (2 * BlattHalb);
        var (q1, q2) = o.Seite > 0 ? (ZargeHalb, ZargeHalb + tief) : (-ZargeHalb - tief, -ZargeHalb);
        // Griff am freien Ende des Blatts, zur Oeffnung hin
        var (g1, g2) = o.Seite > 0 ? (q2 - 0.3f, q2 - 0.12f) : (q1 + 0.12f, q1 + 0.3f);
        var (ga1, ga2) = o.AngelVon ? (a2, a2 + 0.06f) : (a1 - 0.06f, a1);
        return ((a1, q1, a2, q2), (ga1, g1, ga2, g2));
    }

    /// <summary>Alle Tuerblaetter als ein Netz; <paramref name="offen"/> nennt je Tuer die offene Lage (fehlt = zu).</summary>
    public Netz TuerBlattNetz(IReadOnlyDictionary<int, TuerOffen> offen) =>
        Netz.Aus(_raumTueren.SelectMany(t => TuerBlattTeile(t, offen.TryGetValue(t.Nr, out var o) ? o : null)).ToList());

    private readonly Dictionary<int, TuerOffen?> _offenLage = [];

    /// <summary>
    /// Lage, in der die Tuer offen steht: die erste der vier Lagen (Angel an „von“ in den Raum, an „bis“ in den Raum, an „von“ zur
    /// anderen Seite, an „bis“ zur anderen Seite), deren Blatt kein Einrichtungsstueck und keine Person beruehrt; null = keine.
    /// </summary>
    public TuerOffen? OffenLage(RaumTuer t)
    {
        if (_offenLage.TryGetValue(t.Nr, out var fertig))
        {
            return fertig;
        }
        TuerOffen? gefunden = null;
        foreach (var lage in new[] { new TuerOffen(true, t.Innen), new TuerOffen(false, t.Innen), new TuerOffen(true, -t.Innen), new TuerOffen(false, -t.Innen) })
        {
            var (b, _) = OffenesBlatt(t, lage);
            var p1 = t.Punkt(b.A1, t.Linie + b.Q1);
            var p2 = t.Punkt(b.A2, t.Linie + b.Q2);
            var lo = new Vector3(MathF.Min(p1.X, p2.X), 0.01f, MathF.Min(p1.Y, p2.Y));
            var hi = new Vector3(MathF.Max(p1.X, p2.X), TuerOben, MathF.Max(p1.Y, p2.Y));
            var frei = _dekoUmrisse.SelectMany(d => d.Teile).All(k => !Beruehrt(lo, hi, new Vector3(k.X1, k.Y1, k.Z1), new Vector3(k.X2, k.Y2, k.Z2)))
                && Personen.All(p => p.X < lo.X - 0.3f || p.X > hi.X + 0.3f || p.Z < lo.Z - 0.3f || p.Z > hi.Z + 0.3f)
                && hi.X <= Breite && lo.X >= 0 && hi.Z <= Tiefe && lo.Z >= 0;
            if (frei)
            {
                gefunden = lage;
                break;
            }
        }
        _offenLage[t.Nr] = gefunden;
        return gefunden;
    }

    private static bool Beruehrt(Vector3 alo, Vector3 ahi, Vector3 blo, Vector3 bhi)
    {
        const float spiel = 0.02f;
        return MathF.Min(ahi.X, bhi.X) - MathF.Max(alo.X, blo.X) > spiel && MathF.Min(ahi.Y, bhi.Y) - MathF.Max(alo.Y, blo.Y) > spiel
            && MathF.Min(ahi.Z, bhi.Z) - MathF.Max(alo.Z, blo.Z) > spiel;
    }

    /// <summary>Sperrflaeche eines geschlossenen Blatts (x1, z1, x2, z2): die Oeffnung, quer bis <see cref="TuerSperreQuer"/>.</summary>
    public static (float X1, float Z1, float X2, float Z2) TuerSperre(RaumTuer t)
    {
        var p1 = t.Punkt(t.Von, t.Linie - TuerSperreQuer);
        var p2 = t.Punkt(t.Bis, t.Linie + TuerSperreQuer);
        return (MathF.Min(p1.X, p2.X), MathF.Min(p1.Y, p2.Y), MathF.Max(p1.X, p2.X), MathF.Max(p1.Y, p2.Y));
    }

    /// <summary>
    /// Anteil (0 bis 1) der Strecke a..b, an dem sie zuerst in die Sperre einer geschlossenen Tuer tritt (<paramref name="zu"/> sagt je
    /// Tuer, ob sie zu ist); null = frei. Beginnt die Strecke schon in der Sperre, zaehlt nur ein Weg tiefer hinein (zur Wandmitte).
    /// </summary>
    public float? TuerAufStrecke(float ax, float az, float bx, float bz, Func<RaumTuer, bool> zu)
    {
        float? erster = null;
        foreach (var t in _raumTueren)
        {
            if (!zu(t))
            {
                continue;
            }
            var (x1, z1, x2, z2) = TuerSperre(t);
            if (ax >= x1 && ax <= x2 && az >= z1 && az <= z2)
            {
                // schon in der Sperre (z. B. die Tuer ging hinter ihr zu): nur weg von der Wandmitte darf sie
                var (_, qa) = t.Lokal(ax, az);
                var (_, qb) = t.Lokal(bx, bz);
                if (MathF.Abs(qb - t.Linie) < MathF.Abs(qa - t.Linie) - 1e-5f && Innerhalb(t, bx, bz))
                {
                    erster = 0;
                }
                continue;
            }
            if (Schnitt(ax, az, bx, bz, x1, z1, x2, z2) is { } s && (erster is null || s < erster))
            {
                erster = s;
            }
        }
        return erster;
    }

    private static bool Innerhalb(RaumTuer t, float x, float z)
    {
        var (a, _) = t.Lokal(x, z);
        return a >= t.Von && a <= t.Bis;
    }

    /// <summary>Liang-Barsky: Anteil, an dem die Strecke das Rechteck betritt (Anfang ausserhalb), sonst null.</summary>
    private static float? Schnitt(float ax, float az, float bx, float bz, float x1, float z1, float x2, float z2)
    {
        var (dx, dz) = (bx - ax, bz - az);
        float t0 = 0, t1 = 1;
        foreach (var (p, q) in new[] { (-dx, ax - x1), (dx, x2 - ax), (-dz, az - z1), (dz, z2 - az) })
        {
            if (MathF.Abs(p) < 1e-9f)
            {
                if (q < 0)
                {
                    return null;
                }
                continue;
            }
            var r = q / p;
            if (p < 0)
            {
                t0 = MathF.Max(t0, r);
            }
            else
            {
                t1 = MathF.Min(t1, r);
            }
            if (t0 > t1)
            {
                return null;
            }
        }
        return t0;
    }

    /// <summary>Naechste Raumtuer, deren Mitte hoechstens <paramref name="reichweite"/> von (x, z) liegt; null = keine.</summary>
    public RaumTuer? RaumTuerNahe(float x, float z, float reichweite) =>
        _raumTueren.Where(t => Vector2.Distance(t.Mitte, new Vector2(x, z)) <= reichweite)
            .OrderBy(t => Vector2.Distance(t.Mitte, new Vector2(x, z))).FirstOrDefault();

    /// <summary>Steht (x, z) am Ausgang (hoechstens <paramref name="reichweite"/> von der Oeffnung der Tuer in der Westwand)?</summary>
    public bool AmAusgang(float x, float z, float reichweite) =>
        Ausgang is var (von, bis) && new Vector2(x, MathF.Max(MathF.Max(von - z, 0), z - bis)).Length() <= reichweite;
}
