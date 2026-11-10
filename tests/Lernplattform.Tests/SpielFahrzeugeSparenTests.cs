using System.Numerics;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.62.0 / S 0.37.0 (Weisung P12-174, Sammelliste 285, Nico 1a, Chat 2a): die sechs Autos werden in einer
/// gesparten Fassung gezeichnet (neue Modelle „&lt;Name&gt;-12“ hinten an modelle.bin, alte bleiben). Geprueft je Auto:
/// weniger Dreiecke, gleicher Umriss, Raeder rund mit 12 Ecken, keine Flaeche nach unten unter dem Wagenboden, keine
/// Dreiecke ohne Flaeche, Fernform aus D1 nicht groesser; Gegenproben mit den alten Modellen.
/// </summary>
public class SpielFahrzeugeSparenTests
{
    private static GrafikBibliothek G => SpielGrafikTests.Bibliothek.Value;

    /// <summary>Hoehe im Modell, bis zu der Flaechen nach unten wegfallen (UNTEN_SPAREN in wandle_modelle.py).</summary>
    private const float Unten = 0.16f;

    public static TheoryData<string> Autos() => [.. Fahrzeugmodelle.Gespart];

    private static GrafikModell Alt(string n) => G.Modell(n)!;

    private static GrafikModell Neu(string n) => G.Modell(n + Fahrzeugmodelle.Zusatz)!;

    private static IEnumerable<Vector3[]> Dreiecke(GrafikModell m)
    {
        var e = m.Ecken;
        for (var i = 0; i < e.Length; i += 9)
        {
            yield return [new(e[i], e[i + 1], e[i + 2]), new(e[i + 3], e[i + 4], e[i + 5]), new(e[i + 6], e[i + 7], e[i + 8])];
        }
    }

    /// <summary>Je Rad die Ecken auf dem Reifen (Halbmesser 0,3 um die Achse in Hoehe 0,3) unter der Achse: Zahl der Winkel.</summary>
    private static List<int> EckenUnten(GrafikModell m)
    {
        var punkte = Dreiecke(m).SelectMany(d => d).Select(p => new Vector3(MathF.Round(p.X, 4), MathF.Round(p.Y, 4), MathF.Round(p.Z, 4))).ToHashSet();
        // Achsen: Beruehrpunkte der Reifen mit dem Boden
        var achsen = punkte.Where(p => MathF.Abs(p.Y) < 1e-4f && MathF.Abs(p.X) > 0.2f).Select(p => p.Z).Distinct().Order().ToList();
        var zahl = new List<int>();
        foreach (var z in achsen)
        {
            foreach (var seite in new[] { -1, 1 })
            {
                zahl.Add(punkte.Where(p => p.X * seite > 0.2f && p.Y < 0.3f - 1e-4f
                        && MathF.Abs(MathF.Sqrt(((p.Y - 0.3f) * (p.Y - 0.3f)) + ((p.Z - z) * (p.Z - z))) - 0.3f) < 0.003f)
                    .Select(p => (int)MathF.Round(MathF.Atan2(p.Y - 0.3f, p.Z - z) * 180 / MathF.PI)).Distinct().Count());
            }
        }
        return zahl;
    }

    private static int NachUnten(GrafikModell m) => Dreiecke(m).Count(d =>
    {
        var n = Vector3.Cross(d[1] - d[0], d[2] - d[0]);
        return n.Length() > 0 && n.Y / n.Length() < -0.9f && d.Max(p => p.Y) <= Unten;
    });

    private static int OhneFlaeche(GrafikModell m) => Dreiecke(m).Count(d => Vector3.Cross(d[1] - d[0], d[2] - d[0]).LengthSquared() < 1e-14f);

    [Theory]
    [MemberData(nameof(Autos))]
    public void GespartesAuto_WenigerDreiecke_GleicherUmriss(string auto)
    {
        var (alt, neu) = (Alt(auto), Neu(auto));
        Assert.True(neu.Dreiecke <= alt.Dreiecke * 0.8, $"{auto}: {neu.Dreiecke} statt {alt.Dreiecke}");
        Assert.True(Vector3.Distance(alt.Min, neu.Min) < 0.001f && Vector3.Distance(alt.Max, neu.Max) < 0.001f,
            $"{auto}: Umriss {neu.Min}-{neu.Max} statt {alt.Min}-{alt.Max}");
    }

    [Theory]
    [MemberData(nameof(Autos))]
    public void GespartesAuto_RaederMit12Ecken_Gegenprobe(string auto)
    {
        // unter der Achse liegen bei 12 Ecken 5 Ecken echt zwischen den Seiten (30 Grad), bei 24 Ecken 11 (15 Grad)
        Assert.Equal([5, 5, 5, 5], EckenUnten(Neu(auto)));
        Assert.Equal([11, 11, 11, 11], EckenUnten(Alt(auto)));
    }

    [Theory]
    [MemberData(nameof(Autos))]
    public void GespartesAuto_KeineFlaecheNachUntenUndOhneFlaeche_Gegenprobe(string auto)
    {
        Assert.Equal(0, NachUnten(Neu(auto)));
        Assert.Equal(0, OhneFlaeche(Neu(auto)));
        Assert.True(NachUnten(Alt(auto)) > 0, auto + ": Gegenprobe, das alte Modell hat Flaechen nach unten");
    }

    [Theory]
    [MemberData(nameof(Autos))]
    public void GespartesAuto_FernformNichtGroesser(string auto) =>
        Assert.True(Fernform.Bauen(Neu(auto)).Dreiecke <= Fernform.Bauen(Alt(auto)).Dreiecke, auto);

    [Fact]
    public void Tabelle_ZeichnetGespart_AlteNurInDerGegenprobe()
    {
        foreach (var auto in Fahrzeugmodelle.Gespart)
        {
            Assert.Equal(auto + "-12", Fahrzeugmodelle.Zeichnen(auto));
            Assert.Equal(Neu(auto).Dreiecke, FestVerkehr.AutoNetz(G, auto, false).Indizes.Count / 3);
        }
        // Bus, Figur und Unbekanntes bleiben; Parkplatz und Strecken nennen nur Autos mit gesparter Fassung
        Assert.Equal(Buslinie.Modell, Fahrzeugmodelle.Zeichnen(Buslinie.Modell));
        Assert.Equal(FestVerkehr.WartendModell, Fahrzeugmodelle.Zeichnen(FestVerkehr.WartendModell));
        Assert.All(Parkplaetze.Modelle, m => Assert.Contains(m, Fahrzeugmodelle.Gespart));
        Assert.All(Verkehr.Strecken, s => Assert.Contains(s.Modell, Fahrzeugmodelle.Gespart));
        Fahrzeugmodelle.AltFuerTests = true;
        try
        {
            Assert.Equal("car-taxi", Fahrzeugmodelle.Zeichnen("car-taxi"));
            Assert.True(FestVerkehr.AutoNetz(G, "car-taxi", false).Indizes.Count / 3 > Alt("car-taxi").Dreiecke * 0.99);
        }
        finally
        {
            Fahrzeugmodelle.AltFuerTests = false;
        }
    }
}
