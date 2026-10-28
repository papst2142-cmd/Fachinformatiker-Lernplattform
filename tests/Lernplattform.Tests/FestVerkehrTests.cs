using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W11 (Weisung P8-45, Teilplan P8-35 mit Nachtraegen): Festzeiten-Verkehr. Richtige Gestalten je Fest und
/// Uhrzeit, Wechsel genau an den Grenzen, Gestalten durchdringen sich nie (Luecke mindestens 0,05 in einer Stunde
/// Fahrt), kein Grundriss ueber der Fahrbahnkante, Leuchtmarke nur auf dem Licht (P8-43), Ferne-Form. Nur
/// Darstellung: die Fingerabdruecke pruefen die bestehenden Tests.
/// </summary>
public sealed class FestVerkehrTests
{
    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static StadtSzene Szene(string? fest, Jahreszeit j) =>
        StadtSzene.Bauen(new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value), null, Grafik, fest, FestDekoTests.Daten.Value, j);

    private static readonly FestVerkehr.Satz[] Feste = [FestVerkehr.Satz.Halloween, FestVerkehr.Satz.Weihnachten, FestVerkehr.Satz.Ostern];

    [Fact]
    public void SatzJeFestUndUhrzeit()
    {
        // Abend = ab Beginn der Daemmerung bis zum Schlafen; Ostern tagsueber (07:00 bis zur Daemmerung), abends Autos
        Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Festzeit.Halloween, 1049, Jahreszeit.Herbst));
        Assert.Equal(FestVerkehr.Satz.Halloween, FestVerkehr.Fuer(Festzeit.Halloween, 1050, Jahreszeit.Herbst));
        Assert.Equal(FestVerkehr.Satz.Halloween, FestVerkehr.Fuer(Festzeit.Halloween, Stadt.UhrSteht, Jahreszeit.Herbst));
        Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Festzeit.Weihnachten, 959, Jahreszeit.Winter));
        Assert.Equal(FestVerkehr.Satz.Weihnachten, FestVerkehr.Fuer(Festzeit.Weihnachten, 960, Jahreszeit.Winter));
        Assert.Equal(FestVerkehr.Satz.Ostern, FestVerkehr.Fuer(Festzeit.Ostern, 420, Jahreszeit.Fruehling));
        Assert.Equal(FestVerkehr.Satz.Ostern, FestVerkehr.Fuer(Festzeit.Ostern, 1049, Jahreszeit.Fruehling));
        Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Festzeit.Ostern, 1050, Jahreszeit.Fruehling));
        foreach (var j in new[] { Jahreszeit.Fruehling, Jahreszeit.Sommer, Jahreszeit.Herbst, Jahreszeit.Winter })
        {
            for (var uhr = 420L; uhr <= Stadt.UhrSteht; uhr += 15)
            {
                Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(null, uhr, j));
                Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Festzeit.Silvester, uhr, j));
            }
        }
        // Festbeginn und -ende nur ueber Festzeit (das Datum liest nur Festzeit.Datum)
        string? Fest(int jahr, int monat, int tag) => Festzeit.Fuer(new DateOnly(jahr, monat, tag));
        Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Fest(2026, 10, 23), 1200, Jahreszeit.Herbst));
        Assert.Equal(FestVerkehr.Satz.Halloween, FestVerkehr.Fuer(Fest(2026, 10, 24), 1200, Jahreszeit.Herbst));
        Assert.Equal(FestVerkehr.Satz.Halloween, FestVerkehr.Fuer(Fest(2026, 11, 1), 1200, Jahreszeit.Herbst));
        Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Fest(2026, 11, 2), 1200, Jahreszeit.Herbst));
        Assert.Equal(FestVerkehr.Satz.Weihnachten, FestVerkehr.Fuer(Fest(2026, 12, 26), 1000, Jahreszeit.Winter));
        Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Fest(2026, 12, 27), 1000, Jahreszeit.Winter));
        var ostersonntag = Festzeit.Ostersonntag(2027);
        Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Festzeit.Fuer(ostersonntag.AddDays(-8)), 600, Jahreszeit.Fruehling));
        Assert.Equal(FestVerkehr.Satz.Ostern, FestVerkehr.Fuer(Festzeit.Fuer(ostersonntag.AddDays(-7)), 600, Jahreszeit.Fruehling));
        Assert.Equal(FestVerkehr.Satz.Ostern, FestVerkehr.Fuer(Festzeit.Fuer(ostersonntag.AddDays(1)), 600, Jahreszeit.Fruehling));
        Assert.Equal(FestVerkehr.Satz.Autos, FestVerkehr.Fuer(Festzeit.Fuer(ostersonntag.AddDays(2)), 600, Jahreszeit.Fruehling));
    }

    [Fact]
    public void GestaltenJeFest()
    {
        var szene = Szene(null, Jahreszeit.Sommer);
        Assert.Empty(FestVerkehr.Gestalten(Grafik, FestVerkehr.Satz.Autos));
        Assert.Equal(6, FestVerkehr.VerkehrFuer(szene, FestVerkehr.Satz.Autos).Autos.Count);
        var halloween = FestVerkehr.Gestalten(Grafik, FestVerkehr.Satz.Halloween);
        Assert.Equal(["zombie", "geist", "hexe", "skelett", "vampir", "geist"], halloween.Select(g => g.Platz.Art));
        Assert.Equal([0, 1, 2, 3, 4, 5], halloween.Select(g => g.Platz.Strecke));
        var weihnachten = FestVerkehr.Gestalten(Grafik, FestVerkehr.Satz.Weihnachten);
        Assert.Equal(["mann", "geschenk", "geschenk", "geschenk"], weihnachten.Select(g => g.Platz.Art));
        Assert.All(weihnachten, g => Assert.Equal(2, g.Platz.Strecke));
        // Geschenke absteigend: gross, mittel, klein
        var hoehen = weihnachten.Skip(1).Select(g => Hoehe(g.Bilder[0])).ToList();
        Assert.True(hoehen[0] > hoehen[1] && hoehen[1] > hoehen[2], string.Join(", ", hoehen));
        var ostern = FestVerkehr.Gestalten(Grafik, FestVerkehr.Satz.Ostern);
        Assert.Equal(3, ostern.Count(g => g.Platz.Art == "hase"));
        Assert.Equal(3, ostern.Count(g => g.Platz.Art == "huhn"));
        foreach (var satz in Feste)
        {
            var v = FestVerkehr.VerkehrFuer(szene, satz);
            var plaetze = FestVerkehr.Plaetze(satz);
            Assert.Equal(plaetze.Select(p => p.Strecke).Distinct().Order(), v.Autos.Select(a => a.Nummer).Order());
            Assert.All(v.Autos, a => Assert.Equal(FestVerkehr.Tempo(plaetze.First(p => p.Strecke == a.Nummer).Art), a.Tempo));
            Assert.Equal(FestVerkehr.Abstand, v.AbstandVorne);
            // nah nie mehr Netze als das Feld im Shader (StadtFlaeche.AutosHoechstens = 16)
            Assert.True(FestVerkehr.Netze(FestVerkehr.Gestalten(Grafik, satz), false).Count <= 16);
        }
    }

    [Fact]
    public void SchrittbilderDerPuppen()
    {
        foreach (var g in FestVerkehr.Gestalten(Grafik, FestVerkehr.Satz.Halloween))
        {
            Assert.Equal(FestVerkehr.Geht(g.Platz.Art) ? FestVerkehr.Schrittbilder : 1, g.Bilder.Count);
            if (g.Bilder.Count > 1)
            {
                // die Bilder unterscheiden sich (Beine und Arme), haben aber dieselbe Zahl Dreiecke
                Assert.All(g.Bilder, b => Assert.Equal(g.Bilder[0].Indizes.Count, b.Indizes.Count));
                Assert.NotEqual(g.Bilder[0].Ecken, g.Bilder[1].Ecken);
                Assert.NotEqual(g.Bilder[0].Ecken, g.Bilder[2].Ecken);
            }
        }
        // Folge links, Mitte, rechts, Mitte nach dem Weg
        Assert.Equal([0, 1, 2, 1, 0], new[] { 0.01f, 0.13f, 0.25f, 0.37f, 0.49f }.Select(FestVerkehr.Bild));
        // je Gestalt genau ein Bild sichtbar, die anderen auf einen Punkt verkleinert
        var szene = Szene(Festzeit.Halloween, Jahreszeit.Herbst);
        var gs = FestVerkehr.Gestalten(Grafik, FestVerkehr.Satz.Halloween);
        var v = FestVerkehr.VerkehrFuer(szene, FestVerkehr.Satz.Halloween);
        var lagen = new Matrix4x4[16];
        var n = FestVerkehr.Lagen(gs, v, false, lagen);
        Assert.Equal(gs.Sum(g => g.Bilder.Count), n);
        Assert.Equal(gs.Count, lagen.Take(n).Count(m => m.M11 != 0 || m.M22 != 0 || m.M33 != 0));
        Assert.Equal(gs.Count, FestVerkehr.Lagen(gs, v, true, lagen));
    }

    /// <summary>
    /// P8-39: jedes Fest eine Stunde in Schritten von 1/60 s mit allen Verkehrsregeln; in jedem Schritt zwischen allen
    /// Grundrissen (gedrehte Rechtecke aus den Netzen) eine Luecke von mindestens 0,05, und kein Grundriss ragt ueber
    /// die Fahrbahnkante (halbe Strassenbreite von der Strassenmitte).
    /// </summary>
    [Theory]
    [InlineData(FestVerkehr.Satz.Halloween)]
    [InlineData(FestVerkehr.Satz.Weihnachten)]
    [InlineData(FestVerkehr.Satz.Ostern)]
    public void GestaltenDurchdringenSichNie(FestVerkehr.Satz satz)
    {
        var szene = Szene(null, Jahreszeit.Sommer);
        var gs = FestVerkehr.Gestalten(Grafik, satz);
        var v = FestVerkehr.VerkehrFuer(szene, satz);
        var grundrisse = gs.Select(g => Grundriss(g.Bilder)).ToList();
        var huellen = gs.Select(g => Huelle(g.Bilder)).ToList();
        var kleinste = float.MaxValue;
        var engstes = "";
        var rand = 0f;
        for (var schritt = 0; schritt < 3600 * 60; schritt++)
        {
            var ecken = new List<Vector2[]>();
            for (var i = 0; i < gs.Count; i++)
            {
                var (ort, r, _) = FestVerkehr.Lage(gs[i], v)!.Value;
                var quer = new Vector2(-r.Y, r.X);
                var (minX, maxX, minZ, maxZ) = grundrisse[i];
                // lokal: +Z = Fahrtrichtung, +X = links (Drehung wie FestVerkehr.Matrix)
                Vector2 W(float x, float z) => ort + (r * z) - (quer * x);
                var e = new[] { W(minX, minZ), W(maxX, minZ), W(maxX, maxZ), W(minX, maxZ) };
                ecken.Add(e);
                // Das Rechteck ist die grobe Pruefung; ragt eine Ecke hinaus, entscheidet der echte Umriss (konvexe
                // Huelle aller Ecken der Schrittbilder), denn die Ecke des Rechtecks gehoert zu keiner Gestalt.
                var innen = (Vector2 p) => p.X >= 0 && p.Y >= 0 && p.X <= szene.Breite && p.Y <= szene.Tiefe;
                var punkte = e.Where(innen).Any(p => AusserhalbFahrbahn(szene, p) > 1e-3f)
                    ? huellen[i].Select(h => W(h.X, h.Y)).Where(innen)
                    : [];
                foreach (var p in punkte)
                {
                    var d = AusserhalbFahrbahn(szene, p);
                    rand = MathF.Max(rand, d);
                    Assert.True(d <= 1e-3f,
                        string.Create(CultureInfo.InvariantCulture, $"{satz}: {gs[i].Platz.Art} ragt bei {schritt / 60.0:F1} s um {d:F3} ueber die Fahrbahnkante bei {p.X:F2};{p.Y:F2}"));
                }
            }
            for (var i = 0; i < gs.Count; i++)
            {
                for (var k = i + 1; k < gs.Count; k++)
                {
                    var luecke = Luecke(ecken[i], ecken[k]);
                    if (luecke < kleinste)
                    {
                        kleinste = luecke;
                        engstes = string.Create(CultureInfo.InvariantCulture, $"{gs[i].Platz.Art}/{gs[k].Platz.Art} bei {schritt / 60.0:F1} s");
                    }
                }
            }
            v.Weiter(1.0 / 60, -100, -100);
        }
        Assert.True(kleinste >= 0.05f, string.Create(CultureInfo.InvariantCulture, $"{satz}: kleinste Luecke {kleinste:F3} ({engstes})"));
        // sie laufen wirklich (jede Gestalt kommt voran)
        Assert.All(v.Autos, a => Assert.True(a.Weg > 0));
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"{satz}: kleinste Luecke {kleinste:F3} ({engstes}), hoechstens {rand:F3} ausserhalb der Fahrbahn"));
    }

    /// <summary>
    /// P8-43: nur das Licht traegt die Leuchtmarke (Glas, Kerze, ausgeschnittenes Kuerbisgesicht, leuchtende Augen);
    /// Rahmen, Griff, Dach, Kuerbisschale und die Gestalt selbst nicht. Ferne-Formen leuchten nie.
    /// </summary>
    [Fact]
    public void LeuchtmarkeNurAufLicht()
    {
        // jedes leuchtende Modell hat Licht und anderes (sonst wuerde es als Klotz leuchten)
        foreach (var name in new[] { "hw-lantern_standing", "hw-pumpkin_orange_jackolantern", "gy-pumpkin-carved", "gy-lightpost-single", "gy-lightpost-double" })
        {
            var m = Grafik.Modell(name)!;
            var licht = Enumerable.Range(0, m.Dreiecke).Count(d => FestVerkehr.IstLicht(m, d) || Nachtlicht.IstGlas(new Vector3(m.Farben[d * 3], m.Farben[(d * 3) + 1], m.Farben[(d * 3) + 2]) / 255f));
            Assert.True(licht > 0 && licht < m.Dreiecke / 2, $"{name}: {licht} von {m.Dreiecke} Dreiecken Licht");
        }
        foreach (var satz in Feste)
        {
            foreach (var g in FestVerkehr.Gestalten(Grafik, satz))
            {
                foreach (var netz in g.Bilder)
                {
                    var leuchtend = 0;
                    for (var e = 0; e < netz.AnzahlEcken; e++)
                    {
                        var f = netz.FarbeAn(e);
                        if (!Nachtlicht.Leuchtet(f))
                        {
                            continue;
                        }
                        leuchtend++;
                        var roh = Roh(f);
                        Assert.True(IstLicht(roh), string.Create(CultureInfo.InvariantCulture, $"{g.Platz.Art}: leuchtet in {roh.X * 255:F0},{roh.Y * 255:F0},{roh.Z * 255:F0}"));
                    }
                    Assert.True(leuchtend < netz.AnzahlEcken / 10, $"{g.Platz.Art}: {leuchtend} von {netz.AnzahlEcken} Ecken leuchten");
                }
                for (var e = 0; e < g.Fern.AnzahlEcken; e++)
                {
                    Assert.False(Nachtlicht.Leuchtet(g.Fern.FarbeAn(e)), g.Platz.Art + ": Ferne-Form leuchtet");
                }
                Assert.Equal(24, g.Fern.Indizes.Count / 3);
            }
        }
        // mit Licht: der Geist (Laterne), die Hexe (Kuerbislaterne), Zombie und Skelett (Kuerbis-Eimer), der Vampir (Augen)
        var halloween = FestVerkehr.Gestalten(Grafik, FestVerkehr.Satz.Halloween);
        Assert.All(halloween, g => Assert.True(Enumerable.Range(0, g.Bilder[0].AnzahlEcken).Any(e => Nachtlicht.Leuchtet(g.Bilder[0].FarbeAn(e))), g.Platz.Art + " ohne Licht"));
    }

    /// <summary>Farbe ohne Leuchtmarke und ohne Schnee-Marke.</summary>
    private static Vector3 Roh(Vector3 f)
    {
        var ohne = f - new Vector3(Nachtlicht.Marke);
        return ohne.X >= Jahreszeitfarben.SchneeMarke - 0.5f ? ohne - new Vector3(Jahreszeitfarben.SchneeMarke) : ohne;
    }

    /// <summary>Licht: hell gelb-orange (Glas, Kerze, Kuerbisgesicht, Augen der Fledermaus) oder die roten Augen des Vampirs.</summary>
    private static bool IstLicht(Vector3 f)
    {
        int r = (int)MathF.Round(f.X * 255), g = (int)MathF.Round(f.Y * 255), b = (int)MathF.Round(f.Z * 255);
        return FestVerkehr.IstLichtFarbe(r, g, b) || (r, g, b) == (0xDC, 0x26, 0x26) || (r == g && g == b && r < 60);
    }

    private static float Hoehe(Netz n)
    {
        var max = float.MinValue;
        for (var e = 0; e < n.AnzahlEcken; e++)
        {
            max = MathF.Max(max, n.Ort(e).Y);
        }
        return max;
    }

    /// <summary>Konvexe Huelle aller Ecken aller Schrittbilder im Grundriss (lokal: X quer, Y laengs).</summary>
    private static List<Vector2> Huelle(IReadOnlyList<Netz> bilder)
    {
        var punkte = new List<Vector2>();
        foreach (var n in bilder)
        {
            for (var e = 0; e < n.AnzahlEcken; e++)
            {
                var p = n.Ort(e);
                punkte.Add(new Vector2(p.X, p.Z));
            }
        }
        punkte = punkte.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        static float Kreuz(Vector2 o, Vector2 a, Vector2 b) => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));
        var h = new List<Vector2>();
        foreach (var richtung in new[] { punkte, Enumerable.Reverse(punkte).ToList() })
        {
            var start = h.Count;
            foreach (var p in richtung)
            {
                while (h.Count >= start + 2 && Kreuz(h[^2], h[^1], p) <= 0)
                {
                    h.RemoveAt(h.Count - 1);
                }
                h.Add(p);
            }
            h.RemoveAt(h.Count - 1);
        }
        return h;
    }

    /// <summary>Grundriss ueber alle Schrittbilder (lokal: x quer, z laengs).</summary>
    private static (float MinX, float MaxX, float MinZ, float MaxZ) Grundriss(IReadOnlyList<Netz> bilder)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var n in bilder)
        {
            for (var e = 0; e < n.AnzahlEcken; e++)
            {
                var p = n.Ort(e);
                minX = MathF.Min(minX, p.X);
                maxX = MathF.Max(maxX, p.X);
                minZ = MathF.Min(minZ, p.Z);
                maxZ = MathF.Max(maxZ, p.Z);
            }
        }
        return (minX, maxX, minZ, maxZ);
    }

    /// <summary>
    /// Wie weit liegt p ausserhalb der gezeichneten Fahrbahn (0 = darauf)? Die Fahrbahn ist je Abschnitt ein Band der
    /// Strassenbreite, an Knicken und Kreuzungen um die halbe Breite verlaengert, an Sackgassen um
    /// <see cref="StadtSzene.StrassenEnden"/> (wie StadtSzene zeichnet).
    /// </summary>
    internal static float AusserhalbFahrbahn(StadtSzene szene, Vector2 p)
    {
        var best = float.MaxValue;
        foreach (var s in szene.Strassen)
        {
            for (var i = 0; i + 1 < s.Count; i++)
            {
                var a = s[i];
                var b = s[i + 1];
                var laenge = Vector2.Distance(a, b);
                var r = (b - a) / laenge;
                float Ende(Vector2 q, float sonst) => szene.StrassenEnden.FirstOrDefault(e => Vector2.Distance(e.Punkt, q) < 1e-4f) is { Verlaengert: > 0 } e ? e.Verlaengert : sonst;
                var vorn = i == 0 ? Ende(a, StadtSzene.StrassenBreite / 2) : StadtSzene.StrassenBreite / 2;
                var hinten = i + 2 == s.Count ? Ende(b, StadtSzene.StrassenBreite / 2) : StadtSzene.StrassenBreite / 2;
                var t = Vector2.Dot(p - a, r);
                var quer = MathF.Abs(Vector2.Dot(p - a, new Vector2(-r.Y, r.X)));
                var laengs = MathF.Max(MathF.Max(-vorn - t, 0), t - (laenge + hinten));
                var aus = MathF.Max(quer - (StadtSzene.StrassenBreite / 2), 0);
                best = MathF.Min(best, MathF.Sqrt((laengs * laengs) + (aus * aus)));
            }
        }
        return best;
    }

    /// <summary>Luecke zwischen zwei konvexen Vierecken (negativ, wenn sie sich schneiden).</summary>
    internal static float Luecke(Vector2[] a, Vector2[] b)
    {
        foreach (var poly in new[] { a, b })
        {
            for (var i = 0; i < 4; i++)
            {
                var kante = poly[(i + 1) % 4] - poly[i];
                var achse = Vector2.Normalize(new Vector2(-kante.Y, kante.X));
                float aMin = a.Min(p => Vector2.Dot(p, achse)), aMax = a.Max(p => Vector2.Dot(p, achse));
                float bMin = b.Min(p => Vector2.Dot(p, achse)), bMax = b.Max(p => Vector2.Dot(p, achse));
                if (aMax < bMin || bMax < aMin)
                {
                    var kleinste = float.MaxValue;
                    for (var k = 0; k < 4; k++)
                    {
                        for (var j = 0; j < 4; j++)
                        {
                            kleinste = MathF.Min(kleinste, Kurve.Abstand(a[k], (IReadOnlyList<Vector2>)[b[j], b[(j + 1) % 4]]));
                            kleinste = MathF.Min(kleinste, Kurve.Abstand(b[k], (IReadOnlyList<Vector2>)[a[j], a[(j + 1) % 4]]));
                        }
                    }
                    return kleinste;
                }
            }
        }
        return -1;
    }
}
