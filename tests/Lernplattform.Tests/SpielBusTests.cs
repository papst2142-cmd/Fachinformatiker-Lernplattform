using System.Numerics;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.61.0 (Weisung P12-171, Nico 4b): das Modell des Linienbusses (werkzeuge/grafik/bus_modell.py, angehaengt an
/// modelle.bin). Gelb und klar anders als das Taxi, nie als Licht gefaerbt, Front +Z, Tueren rechts in Fahrtrichtung (-X),
/// halbe Laenge im Spiel gleich <see cref="Buslinie.Halb"/>, und die Radkaesten sind zu und dunkel: kein Strahl quer durch
/// eine Radoeffnung kommt durch (Nico: „behebe das Problem, dass man bei den Reifen durchgucken kann“). Gegenprobe: ohne die
/// innere Wand (wie der Entwurf aus Meldung P12-102) gehen Strahlen durch.
/// </summary>
public sealed class SpielBusTests
{
    private static GrafikModell Bus => SpielGrafikTests.Bibliothek.Value.Modell(Buslinie.Modell)!;

    // Achsen und halbe Laenge der Radoeffnung im Modellraum (bus_modell.py: ACH_V, ACH_H, RAD, INNEN)
    private const float AchseVorn = 1.55f, AchseHinten = -1.65f, Rad = 0.36f, Innen = 0.38f;

    private static (Vector3 A, Vector3 B, Vector3 C, (byte R, byte G, byte B) Farbe) Dreieck(GrafikModell m, int i)
    {
        var e = m.Ecken;
        var k = i * 9;
        return (new Vector3(e[k], e[k + 1], e[k + 2]), new Vector3(e[k + 3], e[k + 4], e[k + 5]), new Vector3(e[k + 6], e[k + 7], e[k + 8]),
            (m.Farben[i * 3], m.Farben[(i * 3) + 1], m.Farben[(i * 3) + 2]));
    }

    private static IEnumerable<int> Alle(GrafikModell m) => Enumerable.Range(0, m.Dreiecke);

    /// <summary>Moeller-Trumbore: Abstand bis zum Treffer (beide Seiten), sonst null.</summary>
    private static float? Treffer(Vector3 start, Vector3 richtung, Vector3 a, Vector3 b, Vector3 c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(richtung, e2);
        var det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < 1e-9f)
        {
            return null;
        }
        var t0 = start - a;
        var u = Vector3.Dot(t0, p) / det;
        if (u < -1e-5f || u > 1 + 1e-5f)
        {
            return null;
        }
        var q = Vector3.Cross(t0, e1);
        var v = Vector3.Dot(richtung, q) / det;
        if (v < -1e-5f || u + v > 1 + 1e-5f)
        {
            return null;
        }
        var t = Vector3.Dot(e2, q) / det;
        return t > 0 ? t : null;
    }

    /// <summary>Ein Rad ist ein Dreieck ganz ausserhalb der inneren Kante (die Raeder des Lieferwagens liegen bei |x| 0,4 bis 0,65).</summary>
    private static bool IstRad(Vector3 a, Vector3 b, Vector3 c) => MathF.Abs(a.X) > Innen && MathF.Abs(b.X) > Innen && MathF.Abs(c.X) > Innen
        && new[] { a, b, c }.All(p => MathF.Abs(p.Y - 0.26f) < 0.32f);

    /// <summary>Strahlen quer (±X) durch jede Radoeffnung: Zahl der Strahlen ohne Treffer und der ersten Treffer, die nicht dunkel und kein Rad sind.</summary>
    private static (int Durch, int Hell, int Strahlen) Durchgucken(IReadOnlyList<int> dreiecke, GrafikModell m)
    {
        var (durch, hell, strahlen) = (0, 0, 0);
        foreach (var achse in new[] { AchseVorn, AchseHinten })
        {
            for (var z = achse - Rad + 0.03f; z <= achse + Rad - 0.03f; z += 0.05f)
            {
                for (var y = 0.14f; y <= 0.54f; y += 0.04f)
                {
                    foreach (var seite in new[] { -1f, 1f })
                    {
                        strahlen++;
                        var start = new Vector3(-3 * seite, y, z);
                        var richtung = new Vector3(seite, 0, 0);
                        (float T, int I)? naechster = null;
                        foreach (var i in dreiecke)
                        {
                            var (a, b, c, _) = Dreieck(m, i);
                            if (Treffer(start, richtung, a, b, c) is { } t && (naechster is null || t < naechster.Value.T))
                            {
                                naechster = (t, i);
                            }
                        }
                        if (naechster is not { } n)
                        {
                            durch++;
                            continue;
                        }
                        var (da, db, dc, farbe) = Dreieck(m, n.I);
                        if (!IstRad(da, db, dc) && Math.Max(farbe.R, Math.Max(farbe.G, farbe.B)) > 0x40)
                        {
                            hell++;
                        }
                    }
                }
            }
        }
        return (durch, hell, strahlen);
    }

    [Fact]
    public void Radkaesten_ZuUndDunkel_GegenprobeEntwurf()
    {
        var m = Bus;
        var (durch, hell, strahlen) = Durchgucken([.. Alle(m)], m);
        Assert.True(strahlen > 300, $"nur {strahlen} Strahlen");
        Assert.Equal(0, durch);
        Assert.Equal(0, hell);
        // Gegenprobe wie der Entwurf P12-102: ohne die innere dunkle Wand zwischen den Raedern sieht man quer durch den Bus
        var ohneWand = Alle(m).Where(i =>
        {
            var (a, b, c, f) = Dreieck(m, i);
            var wand = f == (0x1E, 0x1E, 0x22) && MathF.Abs(a.X) <= Innen + 0.001f && MathF.Abs(b.X) <= Innen + 0.001f && MathF.Abs(c.X) <= Innen + 0.001f;
            return !wand;
        }).ToList();
        Assert.True(ohneWand.Count < m.Dreiecke);
        var (durchEntwurf, _, _) = Durchgucken(ohneWand, m);
        Assert.True(durchEntwurf > strahlen / 10, $"Gegenprobe: ohne innere Wand gehen nur {durchEntwurf} von {strahlen} Strahlen durch");
    }

    [Fact]
    public void Farbe_Gelb_KeinLicht_AndersAlsDasTaxi()
    {
        // die Farbe mit der groessten Flaeche (der Lack; nach Zahl der Dreiecke gewaennen die fein gebauten Raeder)
        static (byte R, byte G, byte B) Haeufigste(GrafikModell m) =>
            Enumerable.Range(0, m.Dreiecke).Select(i => (F: Dreieck(m, i).Farbe, A: Vector3.Cross(Dreieck(m, i).B - Dreieck(m, i).A, Dreieck(m, i).C - Dreieck(m, i).A).Length()))
                .GroupBy(x => x.F).OrderByDescending(g => g.Sum(x => x.A)).First().Key;
        var bus = Haeufigste(Bus);
        Assert.Equal(((byte)0xEF, (byte)0xCB, (byte)0x10), bus);
        Assert.False(FestVerkehr.IstLichtFarbe(bus.R, bus.G, bus.B), "der Lack des Busses wuerde nachts leuchten");
        var taxi = SpielGrafikTests.Bibliothek.Value.Modell("car-taxi")!;
        var t = Haeufigste(taxi);
        // klar anders: der Gruenanteil (zitronengelb gegen orange-gelb) und der Abstand der Farben
        var abstand = MathF.Sqrt(MathF.Pow(bus.R - t.R, 2) + MathF.Pow(bus.G - t.G, 2) + MathF.Pow(bus.B - t.B, 2));
        Assert.True(abstand > 40, $"Bus {bus} und Taxi {t} sind zu aehnlich ({abstand:0})");
    }

    [Fact]
    public void Front_Tueren_Laenge()
    {
        var m = Bus;
        var punkte = Alle(m).SelectMany(i => { var (a, b, c, _) = Dreieck(m, i); return new[] { a, b, c }; }).ToList();
        var (zMin, zMax) = (punkte.Min(p => p.Z), punkte.Max(p => p.Z));
        // halbe Laenge im Spiel (Meter × 1,1 wie alle Fahrzeuge)
        Assert.Equal(Buslinie.Halb, (zMax - zMin) * StadtSzene.Meter * 1.1f / 2, 2);
        // Linienanzeige (leuchtet) und Scheinwerfer vorn (+Z), Tueren rechts in Fahrtrichtung (-X)
        var anzeige = Alle(m).Where(i => Dreieck(m, i).Farbe == (0xFF, 0xB0, 0x20)).ToList();
        Assert.NotEmpty(anzeige);
        Assert.All(anzeige, i => Assert.True(Dreieck(m, i).A.Z > 2.7f));
        var tueren = Alle(m).Where(i => Dreieck(m, i).Farbe == (0x5D, 0x71, 0x84)).ToList();
        Assert.Equal(4, tueren.Count);
        Assert.All(tueren, i => Assert.True(Dreieck(m, i).A.X < -0.6f));
    }
}
