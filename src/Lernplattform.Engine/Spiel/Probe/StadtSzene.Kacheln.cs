using System.Numerics;
using System.Runtime.CompilerServices;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280, Plan P12-165): die Netze der Stadt je Kachel (<see cref="Stadtteile"/>).
/// Geplant wird wie bisher die ganze Stadt (<see cref="Bauen"/>: Strassen, Kurven, Kreuzungen, Gehwege, Bahn, Fluss,
/// Laternen, Laufraster); erst die fertigen Dreiecke kommen in ihre Kachel (<see cref="Netz.NurKachel"/>). So gibt es keine
/// Naht und nichts doppelt. Immer geladen bleiben der Boden (zwei Quader und das Gelaende, <see cref="BodenNetzBauen"/>), die
/// Fernform aller Haeuser als einfache Kaesten (<see cref="FernKaestenBauen"/>), die Saetze des Instancing und die Lichter der
/// Ampeln. Alles andere (Netz der Stadt ohne Boden, Kleingruen, Paket-Netz, Haeuser) gibt es je Kachel
/// (<see cref="KachelVorbereiten"/>). Alle Kacheln zusammen ergeben dieselben Dreiecke wie der ganze Bau (Test); die
/// Darstellung aendert sich nicht, nur was gerade geladen ist.
/// </summary>
public sealed partial class StadtSzene
{
    /// <summary>Welle Nachladen: Auswahl beim Bau eines Netzes (nur Boden oder nur eine Kachel).</summary>
    private sealed record Auswahl(Stadtteile.Kachel? Kachel, bool NurBoden, float Breite = 0, float Tiefe = 0);

    /// <summary>Die Netze einer Kachel (alle nah, nach Zellen von <see cref="PaketZelle"/> geordnet).</summary>
    public sealed record KachelNetze(Stadtteile.Kachel Kachel, Netz Stadt, Netz? Gruen, Netz? Paket, IReadOnlyList<(StadtHaus Haus, Netz Netz)> Haeuser)
    {
        /// <summary>Alle Netze der Kachel.</summary>
        public IEnumerable<Netz> Netze => new[] { Stadt, Gruen, Paket }.OfType<Netz>().Concat(Haeuser.Select(h => h.Netz));

        /// <summary>Netzdaten der Kachel in Byte (wie <see cref="Stadtbau.StadtLast.Bytes"/>).</summary>
        public long Bytes => Netze.Sum(n => ((long)n.Ecken.Count * 4) + ((long)n.Indizes.Count * 4));

        /// <summary>Dreiecke der Kachel.</summary>
        public long Dreiecke => Netze.Sum(n => (long)n.Indizes.Count / 3);
    }

    /// <summary>Was beim Nachladen immer geladen bleibt (klein).</summary>
    public sealed record Grundlage(Netz Boden, Netz FernKaesten, Instanzplan? Instanzen, IReadOnlyList<Netz> AmpelLichter);

    private Grundlage? _grundlage;

    /// <summary>
    /// Welle Nachladen: Boden, Fernform der Haeuser, Instancing und Ampel-Lichter (einmal je Szene, dann behalten).
    /// Der Plan des Instancing ist derselbe wie beim ganzen Bau; die Kacheln lassen seine Stuecke aus.
    /// </summary>
    public Grundlage GrundlageVorbereiten()
    {
        if (_grundlage is { } g)
        {
            return g;
        }
        var plan = Instanzen.AusFuerTests ? null : InstanzplanBauen();
        var boden = NetzBauen(false, plan, new Auswahl(null, true));
        g = new Grundlage(boden, FernKaestenBauen(), plan, Probe.Ampeln.LichtNetze(Ampeln) ?? []);
        _grundlage = g;
        return g;
    }

    /// <summary>Welle Nachladen: alle Netze der Kachel <paramref name="k"/> (darf ausserhalb des Oberflaechen-Threads laufen).</summary>
    public KachelNetze KachelVorbereiten(Stadtteile.Kachel k)
    {
        var plan = GrundlageVorbereiten().Instanzen;
        var auswahl = new Auswahl(k, false, Breite, Tiefe);
        var stadt = NetzBauen(false, plan, auswahl);
        stadt.NachZellenOrdnen(PaketZelle);
        var gruen = GruenNetzBauen(plan, k);
        gruen?.NachZellenOrdnen(PaketZelle);
        Netz? paket = null;
        if (Grafik is not null || Andreaskreuze.Count > 0 || Ampeln.Count > 0 || Haltestellen.Count > 0)
        {
            paket = PaketNetzRoh(false, auswahl).NurKachel(k, Breite, Tiefe);
            paket.NachZellenOrdnen(PaketZelle);
        }
        var haeuser = new List<(StadtHaus, Netz)>();
        foreach (var h in Haeuser)
        {
            var m = (h.Min + h.Max) / 2;
            if (Stadtteile.Von(m.X, m.Z, Breite, Tiefe) == k)
            {
                haeuser.Add((h, HausNetz(h)));
            }
        }
        return new KachelNetze(k, stadt, gruen, paket, haeuser);
    }

    /// <summary>Kachel eines Hauses (nach der Mitte seines Kastens).</summary>
    public Stadtteile.Kachel KachelVon(StadtHaus h)
    {
        var m = (h.Min + h.Max) / 2;
        return Stadtteile.Von(m.X, m.Z, Breite, Tiefe);
    }

    /// <summary>Farbe der Fernform eines Hauses (Kasten, im Dunst kaum noch zu unterscheiden).</summary>
    public static readonly Vector3 FernHausFarbe = Netz.Farbe("#8C8378");

    /// <summary>
    /// Welle Nachladen (Nico 3b: Fernform plus leichter Dunst): je Haus ein Kasten in seiner Groesse, nach Kacheln geordnet
    /// (eine Zelle je Kachel); gezeichnet nur fuer Kacheln, die nicht nah geladen sind.
    /// </summary>
    public Netz FernKaestenBauen()
    {
        var netz = new Netz();
        foreach (var h in Haeuser)
        {
            netz.Hinzu(Quader(h.Min.X, 0f, h.Min.Z, h.Max.X, MathF.Max(h.Max.Y, 0.2f), h.Max.Z, FernHausFarbe));
        }
        netz.NachZellenOrdnen(Stadtteile.Groesse);
        return netz;
    }

    /// <summary>Welle Nachladen: nimmt die Auswahl dieses Teil (Index <paramref name="i"/>; 0 und 1 sind die Bodenquader)?</summary>
    private static bool Nimmt(Auswahl? auswahl, int i, Teil t)
    {
        if (auswahl is null)
        {
            return true;
        }
        if (auswahl.NurBoden)
        {
            return i < 2;
        }
        if (i < 2 || auswahl.Kachel is not { } k)
        {
            return false;
        }
        float minX = MathF.Min(t.X1, t.X2), maxX = MathF.Max(t.X1, t.X2), minZ = MathF.Min(t.Z1, t.Z2), maxZ = MathF.Max(t.Z1, t.Z2);
        if (t.Art == TeilArt.Band)
        {
            // ein Band reicht um seine halbe Breite (Y2) neben die Linie von (X1, Z1) nach (X2, Z2)
            (minX, maxX, minZ, maxZ) = (minX - t.Y2, maxX + t.Y2, minZ - t.Y2, maxZ + t.Y2);
        }
        if (t.Dreh != Vector3.Zero)
        {
            // gedrehter Teil: gedreht wird um (Dreh.X, Dreh.Y), also Kreis um diesen Punkt bis zur fernsten Ecke
            var r = MathF.Sqrt(MathF.Max(Quadrat(minX - t.Dreh.X), Quadrat(maxX - t.Dreh.X)) + MathF.Max(Quadrat(minZ - t.Dreh.Y), Quadrat(maxZ - t.Dreh.Y)));
            (minX, maxX, minZ, maxZ) = (t.Dreh.X - r, t.Dreh.X + r, t.Dreh.Y - r, t.Dreh.Y + r);
        }
        return Beruehrt(auswahl, k, minX, maxX, minZ, maxZ, 0.01f);
    }

    private static bool Nimmt(Auswahl? auswahl, Streifen s)
    {
        if (auswahl is null)
        {
            return true;
        }
        if (auswahl.NurBoden || auswahl.Kachel is not { } k || s.Punkte.Count == 0)
        {
            return false;
        }
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in s.Punkte)
        {
            (minX, maxX, minZ, maxZ) = (MathF.Min(minX, p.X), MathF.Max(maxX, p.X), MathF.Min(minZ, p.Y), MathF.Max(maxZ, p.Y));
        }
        return Beruehrt(auswahl, k, minX, maxX, minZ, maxZ, s.Breite + 0.5f);
    }

    private static bool Nimmt(Auswahl? auswahl, Bodenflaeche f)
    {
        if (auswahl is null)
        {
            return true;
        }
        if (auswahl.NurBoden || auswahl.Kachel is not { } k || f.Punkte.Count == 0)
        {
            return false;
        }
        return Beruehrt(auswahl, k, f.Punkte.Min(p => p.X), f.Punkte.Max(p => p.X), f.Punkte.Min(p => p.Y), f.Punkte.Max(p => p.Y), 0.5f);
    }

    private static bool Nimmt(Auswahl? auswahl, Matrix4x4 lage, GrafikModell modell)
    {
        if (auswahl is null)
        {
            return true;
        }
        if (auswahl.NurBoden || auswahl.Kachel is not { } k)
        {
            return false;
        }
        var massstab = MathF.Max(new Vector3(lage.M11, lage.M12, lage.M13).Length(), MathF.Max(new Vector3(lage.M21, lage.M22, lage.M23).Length(), new Vector3(lage.M31, lage.M32, lage.M33).Length()));
        var r = (Radius(modell) * massstab) + 0.01f;
        return Beruehrt(auswahl, k, lage.M41 - r, lage.M41 + r, lage.M43 - r, lage.M43 + r, 0f);
    }

    private static float Quadrat(float v) => v * v;

    private static bool Beruehrt(Auswahl auswahl, Stadtteile.Kachel k, float minX, float maxX, float minZ, float maxZ, float rand) =>
        Netz.Beruehrt(k, minX - rand, maxX + rand, minZ - rand, maxZ + rand, auswahl.Breite, auswahl.Tiefe);

    private static readonly ConditionalWeakTable<GrafikModell, StrongBox<float>> ModellRadien = new();

    /// <summary>Groesster Abstand einer Ecke des Modells vom Ursprung des Modells.</summary>
    private static float Radius(GrafikModell m) => ModellRadien.GetValue(m, mm =>
    {
        var r = 0f;
        for (var i = 0; i + 2 < mm.Ecken.Length; i += 3)
        {
            r = MathF.Max(r, new Vector3(mm.Ecken[i], mm.Ecken[i + 1], mm.Ecken[i + 2]).Length());
        }
        return new StrongBox<float>(r);
    }).Value;
}
