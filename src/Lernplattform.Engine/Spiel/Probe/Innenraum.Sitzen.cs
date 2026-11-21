using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Welle A 0.81.0 (Sammelliste 349): wie die Spielfigur einen Platz einnimmt.</summary>
public enum SitzArt
{
    /// <summary>Sitzen auf einem Sitzmoebel (Knopf „Hinsetzen“).</summary>
    Sitzen,

    /// <summary>Sitzen auf dem Stuhl an einem Schreibtisch (Stuhl antippen, erst dann die PC-Knoepfe).</summary>
    PcStuhl,

    /// <summary>Liegen im Bett oder auf der Matratze (Bett antippen oder Knopf „Hinlegen“).</summary>
    Liegen,

    /// <summary>Welle A 0.93.0 (Sammelliste 352): zurueckgelehnt sitzen auf Sofa, Sessel und Liegestuhl (Knopf „Zurücklehnen“ im Sitzen).</summary>
    Zurueck,
}

/// <summary>
/// Welle A 0.93.0 (Weisung P12-303, Sammelliste 352): Haltung der stehenden Figur an ihrer Stelle (nur Darstellung). Gehen beendet
/// sie; Springen ist ein kurzer Zustand daneben (<c>StadtSpiel.Springt</c>).
/// </summary>
public enum Koerperhaltung
{
    /// <summary>Stehen oder Gehen wie bisher.</summary>
    Stehen,

    /// <summary>Geduckt in der Hocke.</summary>
    Ducken,

    /// <summary>Auf Haenden und Knien.</summary>
    Kriechen,
}

/// <summary>
/// Welle A 0.81.0 (Sammelliste 349): ein Platz, auf den sich die Spielfigur setzen oder legen kann. <see cref="Ort"/> ist die
/// Stelle der Figur (Boden der Figur, Y = Hoehe ueber dem Boden des Raums), <see cref="Blick"/> ihr Blick wie bei
/// <see cref="Sitzplatz"/>. Beim Liegen ist <see cref="Ort"/> die Stelle der Fuesse, der Kopf liegt gegen den Blick.
/// <see cref="Rechteck"/> ist die Grundflaeche des Moebels (x1, z1, x2, z2) fuer „steht davor“ und Antippen.
/// </summary>
public sealed record Sitzgelegenheit(SitzArt Art, Vector3 Ort, float Blick, (float X1, float Z1, float X2, float Z2) Rechteck, PyObjekt? Deko, string Modell)
{
    /// <summary>Mitte der Grundflaeche.</summary>
    public Vector2 Mitte => new((Rechteck.X1 + Rechteck.X2) / 2, (Rechteck.Z1 + Rechteck.Z2) / 2);

    /// <summary>Abstand eines Punkts zur Grundflaeche (0 = darauf).</summary>
    public double Abstand(double x, double z) =>
        SpielKern.Abstand(Rechteck.X1, Rechteck.Z1, Rechteck.X2 - Rechteck.X1, Rechteck.Z2 - Rechteck.Z1, x, z);
}

public sealed partial class Innenraum
{
    /// <summary>
    /// Welle A 0.81.0: Sitz- und Liegemodelle (Name in modelle.bin) mit Hoehe der Sitzflaeche und Lage der Lehne als Anteil
    /// des Modells (y von unten, z von hinten; vorn ist +Z). Gemessen an den Modellen (Hoehenprofil), geprueft von
    /// <c>SpielHaltungTests</c> (keine Durchdringung von Figur und Moebel). Die Stuehle am Ess- und Besprechungstisch
    /// („chair“) fehlen mit Absicht: die Oberschenkel der sitzenden Figur stiessen an jeder Stelle an die Tischplatte oder an
    /// den Stuhl (gemessen 0,03 bis 0,09 Figurhoehe hinter der Vorderkante, je 1 bis 42 Ecken).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (float Sitz, float Lehne, SitzArt Art)> SitzModelle =
        new Dictionary<string, (float, float, SitzArt)>(StringComparer.Ordinal)
        {
            ["bench"] = (0.511f, 0.25f, SitzArt.Sitzen),
            ["loungeSofa"] = (0.5f, 0.25f, SitzArt.Sitzen),
            ["loungeChair"] = (0.5f, 0.25f, SitzArt.Sitzen),
            ["loungeSofaOttoman"] = (1f, 0f, SitzArt.Sitzen),
            ["loungeChairRelax"] = (0.365f, 0.25f, SitzArt.Sitzen),
            ["kkChairA"] = (0.397f, 0.35f, SitzArt.Sitzen),
            ["kkArmchair"] = (0.408f, 0.35f, SitzArt.Sitzen),
            ["bedSingle"] = (0.667f, 0f, SitzArt.Liegen),
            ["bedDouble"] = (0.667f, 0f, SitzArt.Liegen),
        };

    /// <summary>
    /// Welle A 0.81.0 (Sammelliste 351, Weisung P12-276, Nico: „zu weit vorne“): so weit (Anteil der Figurhoehe) sitzt die Figur
    /// in der Haltung ohne Tisch (<see cref="Figur.SitzenFrei"/>) weiter hinten als an der Vorderkante: bis an die Lehne oder so
    /// weit, wie die Unterschenkel vor der Kante frei bleiben (gemessen, je Modell der kleinste Wert ueber alle Groessen, 0,01
    /// Reserve fuer die Pruefung mit Verschiebung 0,02;
    /// <c>SpielSitzenOhneTischTests</c> prueft: keine Durchdringung, und 0,05 weiter hinten stiesse die Figur an).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (float Wert, float Bezug)> SitzTiefe = new Dictionary<string, (float, float)>(StringComparer.Ordinal)
    {
        // die Bank gibt es verschieden tief (0,5 und 0,6): je 0,1 tiefer 0,8 davon weiter nach hinten
        ["bench"] = (0.045f, 0.5f),
        ["loungeSofa"] = (0.055f, 0f),
        ["loungeChair"] = (0.045f, 0f),
        ["loungeChairRelax"] = (0.085f, 0f),
        ["kkChairA"] = (0.04f, 0f), // A 1.1.0 (Massstab 0,75 m): die kleinere Figur passt jetzt bis an die Lehne (bis A 1.0.0 0, der Kopf stiess an die Anzeigetafel)
        ["kkArmchair"] = (0.055f, 0f),
    };

    /// <summary>
    /// Welle A 0.93.0 (Weisung P12-303, Sammelliste 352: „zurueckgelehnt sitzen auf Sofa, Sessel und Liegestuhl“): auf diesen
    /// Modellen gibt es neben dem Sitzplatz einen Platz zum Zuruecklehnen an derselben Stelle (gemessen: an jedem Platz der Raeume
    /// bleibt die zurueckgelehnte Figur frei, <c>SpielHaltungTests</c>). Wohnung: Sofa (loungeSofa), Sessel (loungeChair),
    /// Liegestuhl (loungeChairRelax).
    /// </summary>
    public static readonly IReadOnlySet<string> ZurueckModelle = new HashSet<string>(StringComparer.Ordinal) { "loungeSofa", "loungeChair", "loungeChairRelax" };

    /// <summary>Welle A 0.93.0: der zurueckgelehnte Platz zum Sitzplatz <paramref name="sitz"/> (gleiches Moebel), sonst null.</summary>
    public Sitzgelegenheit? ZurueckZu(Sitzgelegenheit sitz) =>
        _sitzgelegenheiten.FirstOrDefault(s => s.Art == SitzArt.Zurueck && ReferenceEquals(s.Deko, sitz.Deko) && s.Modell == sitz.Modell && s.Rechteck == sitz.Rechteck);

    /// <summary>Welle A 0.93.0: der Sitzplatz zum zurueckgelehnten Platz <paramref name="zurueck"/> (gleiches Moebel), sonst null.</summary>
    public Sitzgelegenheit? SitzZu(Sitzgelegenheit zurueck) =>
        _sitzgelegenheiten.FirstOrDefault(s => s.Art == SitzArt.Sitzen && ReferenceEquals(s.Deko, zurueck.Deko) && s.Modell == zurueck.Modell && s.Rechteck == zurueck.Rechteck);

    /// <summary>Welle A 0.81.0 (Sammelliste 351): Weg nach hinten (Welt) auf einem Modell der Tiefe <paramref name="tiefe"/> (Welt) fuer die Figur der Hoehe <paramref name="figur"/>.</summary>
    public static float Zurueck(string name, float tiefe, float figur) =>
        Figur.SitzenWieBisher || !SitzTiefe.TryGetValue(name, out var t) ? 0f : (t.Wert * figur) + (t.Bezug > 0 ? 0.8f * MathF.Max(0f, tiefe - t.Bezug) : 0f);

    private readonly List<Sitzgelegenheit> _sitzgelegenheiten = [];

    private PyObjekt? _dekoJetzt;

    /// <summary>Welle A 0.81.0: alle Plaetze des Raums zum Sitzen und Liegen (auch besetzte; frei prueft <see cref="Frei"/>).</summary>
    public IReadOnlyList<Sitzgelegenheit> Sitzgelegenheiten => _sitzgelegenheiten;

    /// <summary>Welle A 0.81.0: der Platz ist frei (keine Person sitzt dort und keine steht naeher als 0,8 an ihm).</summary>
    public bool Frei(Sitzgelegenheit s) =>
        !Personen.Any(p => Sitz(p) is { } sitz ? Vector2.Distance(sitz.Stuhl, s.Mitte) < 0.3f : Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(s.Ort.X, s.Ort.Z)) < 0.8f);

    /// <summary>Welle A 0.81.0: freier Platz der Art <paramref name="art"/>, vor dem die Figur steht (Grundflaeche in Reichweite), der naechste.</summary>
    public Sitzgelegenheit? FreierPlatzNahe(double x, double z, Func<Sitzgelegenheit, bool> art) =>
        _sitzgelegenheiten.Where(s => art(s) && Frei(s) && s.Abstand(x, z) <= SpielKern.MoebelReichweite)
            .OrderBy(s => s.Abstand(x, z)).ThenBy(s => Vector2.Distance(s.Mitte, new Vector2((float)x, (float)z))).FirstOrDefault();

    /// <summary>Welle A 0.81.0 (Nico 1b): angetippter freier Stuhl an einem Schreibtisch (Tippen auf die Grundflaeche des Stuhls, 0,15 Zugabe).</summary>
    public Sitzgelegenheit? PcStuhlBei(double x, double z) =>
        _sitzgelegenheiten.Where(s => s.Art == SitzArt.PcStuhl && Frei(s) && s.Abstand(x, z) <= 0.15).OrderBy(s => s.Abstand(x, z)).FirstOrDefault();

    /// <summary>Welle A 0.81.0 (Nico 3a): freier Liegeplatz auf dem angetippten Einrichtungsstueck (Bett, Matratze).</summary>
    public Sitzgelegenheit? LiegeplatzBei(double x, double z) =>
        _sitzgelegenheiten.Where(s => s.Art == SitzArt.Liegen && Frei(s) && s.Abstand(x, z) <= 0.05).OrderBy(s => s.Abstand(x, z)).FirstOrDefault();

    /// <summary>Phase 9 W1 / A 0.81.0: der Stuhl am Schreibtisch ist auch ein Platz der Spielfigur (Stelle und Blick wie die Personen).</summary>
    private void PcStuhlMerken(Sitzplatz platz, (float X, float Z, float W, float T) stuhl)
    {
        // Stelle und Blick bleiben die der Personen; ragte die Figur dort ueber eine Wand, gibt es den Platz nicht
        var s = new Sitzgelegenheit(SitzArt.PcStuhl, new Vector3(platz.X, 0, platz.Z), platz.Blick, (stuhl.X, stuhl.Z, stuhl.X + stuhl.W, stuhl.Z + stuhl.T), _dekoJetzt, "chairDesk");
        if (!UeberWand(new Vector2(s.Ort.X, s.Ort.Z), new Vector2(MathF.Sin(s.Blick), MathF.Cos(s.Blick)), Grundflaeche(s.Art), FigurHoehe, s.Mitte))
        {
            _sitzgelegenheiten.Add(s);
        }
    }

    /// <summary>
    /// Welle A 0.81.0: Quader um die Figur der Hoehe 1 (x halb, y von bis, z von bis; in Blickrichtung +z), sitzend und liegend,
    /// mit Reserve fuer Muetze und Spitzhut (sitzend). <c>SpielHaltungTests</c> prueft, dass jede Ecke der Figur in ihnen liegt.
    /// </summary>
    public static IReadOnlyList<(float Halb, float Y1, float Y2, float Z1, float Z2)> FigurQuader(SitzArt art) => art == SitzArt.Liegen
        ? [(0.19f, -0.15f, 0.16f, -1.01f, 0.01f)]
        : art == SitzArt.Zurueck
            // Welle A 0.93.0: Oberkoerper und Kopf weiter hinten, die Haende im Schoss
            ? [(0.12f, 0.09f, 0.26f, 0.10f, 0.30f), (0.12f, 0.24f, 0.46f, -0.07f, 0.185f), (0.13f, 0.44f, 0.61f, -0.155f, 0.285f), (0.125f, 0.59f, 0.86f, -0.245f, 0.065f), (0.19f, 0.83f, 1.04f, -0.31f, 0.13f)]
        : art == SitzArt.Sitzen && !Figur.SitzenWieBisher
            // Sammelliste 351: ohne Tisch (Figur.SitzenFrei) reichen Knie und Fuesse weiter nach vorn
            ? [(0.14f, 0.27f, 0.90f, -0.145f, 0.16f), (0.19f, 0.86f, 1.08f, -0.215f, 0.22f), (0.12f, 0.27f, 0.43f, -0.06f, 0.24f), (0.12f, 0.09f, 0.30f, 0.04f, 0.30f), (0.19f, 0.44f, 0.65f, -0.06f, 0.35f)]
            : [(0.14f, 0.27f, 0.90f, -0.145f, 0.16f), (0.19f, 0.86f, 1.08f, -0.215f, 0.22f), (0.12f, 0.27f, 0.43f, -0.06f, 0.18f), (0.12f, 0.09f, 0.30f, 0.04f, 0.22f), (0.19f, 0.44f, 0.65f, -0.06f, 0.35f)];

    /// <summary>
    /// Welle A 0.81.0: am Ende des Aufbaus die Plaetze streichen, an denen die Figur (ihre Quader, 0,02 Spiel) ein anderes Stueck
    /// der Einrichtung oder der Fest-Deko beruehrt (z. B. ein Sessel direkt vor der Kuechenzeile).
    /// </summary>
    private void SitzePruefen()
    {
        var andere = _dekoUmrisse.Select(u => (Deko: (PyObjekt?)u.Deko, Kaesten: (IEnumerable<(Vector3, Vector3)>)[
                .. u.Kaesten.Where(k => k.Art == TeilArt.Quader).Select(Huelle),
                .. u.Modelle.Select(m => Huelle(m.Name, m.Lage)).OfType<(Vector3, Vector3)>()]))
            .Append((Deko: (PyObjekt?)null, Kaesten: [.. FestKaesten.Where(k => k.Art == TeilArt.Quader).Select(Huelle), .. FestModelle.Select(m => Huelle(m.Name, m.Lage)).OfType<(Vector3, Vector3)>()]))
            .ToList();
        // flach auf dem Boden (Teppich, Matte bis 0,05 hoch) stoert nie
        _sitzgelegenheiten.RemoveAll(s => andere.Any(a => !ReferenceEquals(a.Deko, s.Deko) && a.Kaesten.Any(k => k.Item2.Y > 0.05f && Beruehrt(s, FigurHoehe, k.Item1, k.Item2))));
    }

    private static (Vector3, Vector3) Huelle(Teil t) =>
        (new Vector3(MathF.Min(t.X1, t.X2), MathF.Min(t.Y1, t.Y2), MathF.Min(t.Z1, t.Z2)), new Vector3(MathF.Max(t.X1, t.X2), MathF.Max(t.Y1, t.Y2), MathF.Max(t.Z1, t.Z2)));

    private (Vector3, Vector3)? Huelle(string name, Matrix4x4 lage)
    {
        if (Grafik?.Modell(name) is not { } gm)
        {
            return null;
        }
        var ecken = Enumerable.Range(0, 8).Select(i => Vector3.Transform(new Vector3((i & 1) == 0 ? gm.Min.X : gm.Max.X, (i & 2) == 0 ? gm.Min.Y : gm.Max.Y, (i & 4) == 0 ? gm.Min.Z : gm.Max.Z), lage)).ToList();
        return (ecken.Aggregate(Vector3.Min), ecken.Aggregate(Vector3.Max));
    }

    /// <summary>Welle A 0.81.0: ein Quader der Figur auf dem Platz (0,02 Spiel) schneidet den achsgleichen Kasten lo..hi.</summary>
    public static bool Beruehrt(Sitzgelegenheit s, float hoehe, Vector3 lo, Vector3 hi)
    {
        const float spiel = 0.02f;
        var f = new Vector2(MathF.Sin(s.Blick), MathF.Cos(s.Blick));
        var q = new Vector2(f.Y, -f.X);
        var ort = new Vector2(s.Ort.X, s.Ort.Z);
        foreach (var (halb, y1, y2, z1, z2) in FigurQuader(s.Art))
        {
            if (s.Ort.Y + (y1 * hoehe) - spiel >= hi.Y || s.Ort.Y + (y2 * hoehe) + spiel <= lo.Y)
            {
                continue;
            }
            float vorn = (z2 * hoehe) + spiel, hinten = (z1 * hoehe) - spiel, seite = (halb * hoehe) + spiel;
            Vector2[] ecken = [ort + (f * vorn) + (q * seite), ort + (f * vorn) - (q * seite), ort + (f * hinten) - (q * seite), ort + (f * hinten) + (q * seite)];
            if (Schneidet(ecken, new Vector2(lo.X, lo.Z), new Vector2(hi.X, hi.Z)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Welle A 0.81.0: Plaetze der eben gebauten Modelle (ab <paramref name="ab"/> in den Stuecken) merken.</summary>
    private void SitzeMerken(int ab)
    {
        for (var i = ab; i < _stuecke.Count; i++)
        {
            if (Grafik?.Modell(_stuecke[i].Name) is { } gm && Platz(_stuecke[i].Name, gm.Min, gm.Max, _stuecke[i].Lage, Innenraum.FigurHoehe, _dekoJetzt) is { } s)
            {
                if (VorDerWand(s) is { } frei)
                {
                    _sitzgelegenheiten.Add(frei);
                }
                // Welle A 0.93.0 (Sammelliste 352): auf Sofa, Sessel und Liegestuhl auch zurueckgelehnt (eigener Platz, eigene Pruefung)
                if (s.Art == SitzArt.Sitzen && ZurueckModelle.Contains(s.Modell) && VorDerWand(s with { Art = SitzArt.Zurueck }) is { } zurueck)
                {
                    _sitzgelegenheiten.Add(zurueck);
                }
            }
        }
    }

    /// <summary>
    /// Welle A 0.81.0 (Regel 347): Grundflaeche der Figur (Anteile der Figurhoehe ab ihrer Stelle, in Blickrichtung) mit Reserve
    /// fuer Kopfbedeckung und Haende: sitzend 0,21 nach hinten (Spitzhut), 0,35 nach vorn, 0,19 zur Seite; liegend die ganze
    /// Laenge nach hinten (Kopf), ohne Kopfbedeckung (die Figur legt sie im Bett ab).
    /// </summary>
    public static (float Hinten, float Vorn, float Halb) Grundflaeche(SitzArt art) =>
        art == SitzArt.Liegen ? (1.02f, 0.02f, 0.19f) : art == SitzArt.Zurueck ? (0.32f, 0.35f, 0.19f) : (0.21f, 0.35f, 0.19f);

    /// <summary>
    /// Welle A 0.81.0: der Platz so, dass die Grundflaeche der Figur nicht ueber die Mitte einer Wand reicht (0,02 Spiel wie
    /// <c>SpielWandTests</c>); sitzend darf die Figur dafuer bis 0,07 Figurhoehe nach vorn ruecken, sonst gibt es den Platz nicht.
    /// </summary>
    private Sitzgelegenheit? VorDerWand(Sitzgelegenheit s)
    {
        var f = new Vector2(MathF.Sin(s.Blick), MathF.Cos(s.Blick));
        for (var k = 0; k <= (s.Art == SitzArt.Liegen ? 0 : 7); k++)
        {
            var ort = s.Ort + (new Vector3(f.X, 0, f.Y) * (k * 0.01f * FigurHoehe));
            if (!UeberWand(new Vector2(ort.X, ort.Z), f, Grundflaeche(s.Art), FigurHoehe, s.Mitte))
            {
                return s with { Ort = ort };
            }
        }
        return null;
    }

    /// <summary>Welle A 0.81.0 (Sammelliste 351, nur fuer Pruefungen): die Grundflaeche der Figur auf dem Platz reicht ueber die Mitte einer Wand.</summary>
    internal bool UeberDieWand(Sitzgelegenheit s) => UeberWand(new Vector2(s.Ort.X, s.Ort.Z), new Vector2(MathF.Sin(s.Blick), MathF.Cos(s.Blick)), Grundflaeche(s.Art), FigurHoehe, s.Mitte);

    private bool UeberWand(Vector2 ort, Vector2 f, (float Hinten, float Vorn, float Halb) g, float hoehe, Vector2 anker)
    {
        const float spiel = 0.02f;
        var q = new Vector2(f.Y, -f.X);
        // dazu 0,02 rundum: die Figur darf auch 0,02 neben ihrer Stelle frei sein (Pruefung mit Schub)
        float vorn = (g.Vorn * hoehe) + spiel, hinten = (g.Hinten * hoehe) + spiel, halb = (g.Halb * hoehe) + spiel;
        Vector2[] ecken = [ort + (f * vorn) + (q * halb), ort + (f * vorn) - (q * halb), ort - (f * hinten) - (q * halb), ort - (f * hinten) + (q * halb)];
        foreach (var w in _waende)
        {
            Vector2 lo = new(w.Min.X, w.Min.Z), hi = new(w.Max.X, w.Max.Z);
            if (hi.X - lo.X <= hi.Y - lo.Y)
            {
                var mitte = (lo.X + hi.X) / 2;
                var plus = w.Aussen.X != 0 ? w.Aussen.X > 0 : anker.X < mitte;
                (lo.X, hi.X) = plus ? (mitte + spiel, hi.X + 1) : (lo.X - 1, mitte - spiel);
                (lo.Y, hi.Y) = (lo.Y + spiel, hi.Y - spiel);
            }
            else
            {
                var mitte = (lo.Y + hi.Y) / 2;
                var plus = w.Aussen.Y != 0 ? w.Aussen.Y > 0 : anker.Y < mitte;
                (lo.Y, hi.Y) = plus ? (mitte + spiel, hi.Y + 1) : (lo.Y - 1, mitte - spiel);
                (lo.X, hi.X) = (lo.X + spiel, hi.X - spiel);
            }
            if (lo.X < hi.X && lo.Y < hi.Y && Schneidet(ecken, lo, hi))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Viereck (Ecken der Reihe nach) und achsgleiches Rechteck schneiden sich (Trennachsen).</summary>
    private static bool Schneidet(Vector2[] ecken, Vector2 lo, Vector2 hi)
    {
        Vector2[] rechteck = [lo, new(hi.X, lo.Y), hi, new(lo.X, hi.Y)];
        var achsen = new List<Vector2> { Vector2.UnitX, Vector2.UnitY };
        for (var i = 0; i < ecken.Length; i++)
        {
            var kante = ecken[(i + 1) % ecken.Length] - ecken[i];
            achsen.Add(new Vector2(-kante.Y, kante.X));
        }
        foreach (var a in achsen)
        {
            var (amin, amax) = (ecken.Min(e => Vector2.Dot(e, a)), ecken.Max(e => Vector2.Dot(e, a)));
            var (bmin, bmax) = (rechteck.Min(e => Vector2.Dot(e, a)), rechteck.Max(e => Vector2.Dot(e, a)));
            if (amax <= bmin || bmax <= amin)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Welle A 0.81.0: Platz der Figur (Hoehe <paramref name="figur"/>) auf einem Modell mit Huelle <paramref name="min"/> bis
    /// <paramref name="max"/> in der Lage <paramref name="lage"/>, sonst null (kein Sitzmodell). Sitzen: Gesaess auf der
    /// Sitzflaeche, die Unterschenkel knapp vor der Vorderkante (<see cref="Figur.SitzVor"/>), Blick nach vorn. Liegen: auf
    /// dem Ruecken, der Kopf am Kopfende (hinten) und ganz auf der Liegeflaeche.
    /// </summary>
    public static Sitzgelegenheit? Platz(string name, Vector3 min, Vector3 max, Matrix4x4 lage, float figur, PyObjekt? deko)
    {
        if (!SitzModelle.TryGetValue(name, out var m))
        {
            return null;
        }
        var g = max - min;
        Vector3 L(float fx, float fy, float fz) => Vector3.Transform(min + (g * new Vector3(fx, fy, fz)), lage);
        var ecken = new[] { L(0, 0, 0), L(1, 0, 0), L(0, 0, 1), L(1, 0, 1) };
        var rechteck = (ecken.Min(e => e.X), ecken.Min(e => e.Z), ecken.Max(e => e.X), ecken.Max(e => e.Z));
        var vorn = L(0.5f, m.Sitz, 1f);
        var hinten = L(0.5f, m.Sitz, m.Lehne);
        var f = Vector2.Normalize(new Vector2(vorn.X - hinten.X, vorn.Z - hinten.Z));
        var blick = MathF.Atan2(f.X, f.Y);
        if (m.Art == SitzArt.Liegen)
        {
            // Sammelliste 380 (Nico: „laengs, Kopf am Kopfteil“): immer laengs vom Kopfteil (hinten) zum Fussende, fuer jedes
            // Bett; bis A 0.89.0 lag die Figur quer, wenn das Bett breiter als lang gezeichnet war (Gegenprobe BettQuerAltFuerTests)
            var laengsZ = !BettQuerAltFuerTests.Value
                || Vector3.Distance(L(0.5f, m.Sitz, 0f), L(0.5f, m.Sitz, 1f)) >= Vector3.Distance(L(0f, m.Sitz, 0.5f), L(1f, m.Sitz, 0.5f));
            var (kopfende, fussende, rand) = laengsZ
                ? (L(0.5f, m.Sitz, 0f), L(0.5f, m.Sitz, 1f), Figur.LiegenKopfteil)
                : (L(0f, m.Sitz, (1 + Figur.LiegenKopfteil) / 2), L(1f, m.Sitz, (1 + Figur.LiegenKopfteil) / 2), 0f);
            var richtung = new Vector2(fussende.X - kopfende.X, fussende.Z - kopfende.Z);
            var lang = richtung.Length();
            richtung /= lang;
            var kopf = MathF.Max((rand * lang) + Figur.LiegenRand, MathF.Min(Figur.LiegenKopf * lang, lang - figur - Figur.LiegenRand));
            var fuss = new Vector2(kopfende.X, kopfende.Z) + (richtung * (kopf + figur));
            return new Sitzgelegenheit(SitzArt.Liegen, new Vector3(fuss.X, fussende.Y + ((Figur.LiegenRuecken + Figur.LiegenUeber) * figur), fuss.Y),
                MathF.Atan2(richtung.X, richtung.Y), rechteck, deko, name);
        }
        // Sammelliste 351: ohne Tisch weiter nach hinten an die Lehne (Zurueck), mit der Gegenprobe wie bis P12-276
        var stelle = new Vector2(vorn.X, vorn.Z) - (f * ((Figur.SitzVor * figur) + Zurueck(name, Vector3.Distance(L(0.5f, m.Sitz, 0f), vorn), figur)));
        return new Sitzgelegenheit(SitzArt.Sitzen, new Vector3(stelle.X, vorn.Y - ((Figur.SitzUnten - Figur.SitzSpalt) * figur), stelle.Y), blick, rechteck, deko, name);
    }
}
