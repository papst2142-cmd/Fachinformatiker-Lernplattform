using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.0.0 (Sammelliste 410, Nico: „Drucker (grauer Kasten) neu im realistischen Stil wie Server und PC“). Feste
/// Dreieckszahl unter der Grenze (<see cref="Innenraum.WerkstattGrenzen"/>), in allen Innenraeumen bleibt jeder Drucker in
/// seinem Rechteck (±0,02), beruehrt kein anderes Stueck und keine Wand. Gegenprobe: wie bis A 0.99.0 zwei Kaesten
/// (<c>Innenraum.DruckerAltFuerTests</c>).
/// </summary>
public sealed class SpielDruckerTests
{
    private const int NeuDrucker = 468;
    private const int AltDrucker = 24;

    private static int Dreiecke(IEnumerable<Teil> teile) => Netz.Aus(teile.ToList()).Indizes.Count / 3;

    [Fact]
    public void Drucker_DreieckeFest_UnterGrenze_GenauerAlsAlt()
    {
        var neu = Dreiecke(Innenraum.WerkstattTeile("drucker", 0.9f, 0.75f));
        Assert.Equal(NeuDrucker, neu);
        Assert.True(neu <= Innenraum.WerkstattGrenzen["drucker"], "ueber der Grenze: " + neu);
        Assert.True(neu >= AltDrucker * 10, "nicht genauer als bisher: " + neu);
    }

    [Fact]
    public void Drucker_InAllenRaeumen_ImRechteck_OhneNachbarnUndWand_Gegenprobe()
    {
        var (anzahl, funde) = Pruefen();
        Assert.True(anzahl >= 5, "zu wenige Drucker: " + anzahl);
        Assert.True(funde.Count == 0, string.Join("\n", funde.Distinct().Take(20)));
        Innenraum.DruckerAltFuerTests.Value = true;
        try
        {
            var alt = SpielSitzenTests.AlleRaeume().SelectMany(r => r.Innen.DekoUmrisse).First(u => Py.Gleich(u.Deko["typ"], "drucker"));
            Assert.Equal(AltDrucker, Dreiecke(alt.Teile));
        }
        finally
        {
            Innenraum.DruckerAltFuerTests.Value = false;
        }
    }

    private static (int Anzahl, List<string> Funde) Pruefen()
    {
        const float spiel = 0.02f;
        var anzahl = 0;
        var funde = new List<string>();
        static float Tief(float a1, float a2, float b1, float b2) =>
            MathF.Min(MathF.Max(a1, a2), MathF.Max(b1, b2)) - MathF.Max(MathF.Min(a1, a2), MathF.Min(b1, b2));
        foreach (var (stand, innen) in SpielSitzenTests.AlleRaeume())
        {
            var drucker = innen.DekoUmrisse.Where(u => Py.Gleich(u.Deko["typ"], "drucker")).ToList();
            foreach (var u in drucker)
            {
                anzahl++;
                var (x, z, w, t) = ((float)Py.F(u.Deko["x"]), (float)Py.F(u.Deko["y"]), (float)Py.F(u.Deko["w"]), (float)Py.F(u.Deko["h"]));
                var name = $"{stand} {innen.Ansicht} Drucker {x}/{z}";
                Assert.True(Dreiecke(u.Teile) == NeuDrucker, name + ": " + Dreiecke(u.Teile));
                foreach (var k in u.Teile.Where(k => k.Art == TeilArt.Quader))
                {
                    if (MathF.Min(k.X1, k.X2) < x - spiel || MathF.Max(k.X1, k.X2) > x + w + spiel || MathF.Min(k.Z1, k.Z2) < z - spiel
                        || MathF.Max(k.Z1, k.Z2) > z + t + spiel || MathF.Min(k.Y1, k.Y2) < -spiel || MathF.Max(k.Y1, k.Y2) > Innenraum.IchWandHoehe)
                    {
                        funde.Add(name + ": Kasten ausserhalb");
                    }
                }
                foreach (var andere in innen.DekoUmrisse.Where(a => !ReferenceEquals(a, u)))
                {
                    if (u.Teile.Any(k => andere.Teile.Where(o => o.Art == TeilArt.Quader).Any(o =>
                        Tief(k.X1, k.X2, o.X1, o.X2) > spiel && Tief(k.Y1, k.Y2, o.Y1, o.Y2) > spiel && Tief(k.Z1, k.Z2, o.Z1, o.Z2) > spiel)))
                    {
                        funde.Add(name + " beruehrt " + Py.Str(andere.Deko["typ"]));
                    }
                }
            }
            funde.AddRange(SpielWandTests.Funde(innen, drucker).Select(f => stand + " " + innen.Ansicht + ": " + f));
        }
        return (anzahl, funde);
    }
}
