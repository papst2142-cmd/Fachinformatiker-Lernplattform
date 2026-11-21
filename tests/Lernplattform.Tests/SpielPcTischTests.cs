using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.0.0 (Sammelliste 408, Nico: „die PCs unter dem Tisch stecken in Tischbeinen und Querstreben“). Nicos Ziel: in
/// keinem Raum durchdringt ein PC einen Tisch, unter dem er steht (Durchdringung 0, Toleranz 0,02 je Achse). Geprueft fuer alle
/// Tische mit PC in allen Innenraeumen (neu, Tag 200, gespielter Stand). Gegenprobe ueber <c>Innenraum.PcTischAltFuerTests</c>
/// (Tisch wie bis A 0.99.0).
/// </summary>
public sealed class SpielPcTischTests
{
    private const float Toleranz = 0.02f;

    private static readonly HashSet<string> Tische = ["tisch", "werkbank", "schreibtisch", "besprechungstisch", "esstisch"];

    private static bool Ueber(PyObjekt a, PyObjekt b) =>
        Py.F(a["x"]) < Py.F(b["x"]) + Py.F(b["w"]) && Py.F(b["x"]) < Py.F(a["x"]) + Py.F(a["w"])
        && Py.F(a["y"]) < Py.F(b["y"]) + Py.F(b["h"]) && Py.F(b["y"]) < Py.F(a["y"]) + Py.F(a["h"]);

    private static float Tief(float a1, float a2, float b1, float b2) =>
        Math.Min(Math.Max(a1, a2), Math.Max(b1, b2)) - Math.Max(Math.Min(a1, a2), Math.Min(b1, b2));

    /// <summary>Paare (PC unter Tisch) und Funde (Teile, die sich durchdringen).</summary>
    private static (int Paare, List<string> Funde) Pruefen()
    {
        var paare = 0;
        var funde = new List<string>();
        foreach (var (stand, innen) in SpielSitzenTests.AlleRaeume())
        {
            var umrisse = innen.DekoUmrisse;
            foreach (var pc in umrisse.Where(u => Py.Gleich(u.Deko["typ"], "pc")))
            {
                foreach (var tisch in umrisse.Where(u => Tische.Contains(Py.Str(u.Deko["typ"])) && Ueber(u.Deko, pc.Deko)))
                {
                    paare++;
                    foreach (var a in pc.Teile)
                    {
                        foreach (var b in tisch.Teile)
                        {
                            var (dx, dy, dz) = (Tief(a.X1, a.X2, b.X1, b.X2), Tief(a.Y1, a.Y2, b.Y1, b.Y2), Tief(a.Z1, a.Z2, b.Z1, b.Z2));
                            if (dx > Toleranz && dy > Toleranz && dz > Toleranz && funde.Count < 20)
                            {
                                funde.Add(string.Create(CultureInfo.InvariantCulture,
                                    $"{stand} {innen.Ansicht}: PC {Py.F(pc.Deko["x"])}/{Py.F(pc.Deko["y"])} in {tisch.Deko["typ"]} ({dx:F2} x {dy:F2} x {dz:F2})"));
                            }
                        }
                    }
                }
            }
        }
        return (paare, funde);
    }

    [Fact]
    public void PcUnterTisch_DurchdringtNie_Gegenprobe()
    {
        var (paare, funde) = Pruefen();
        // Bitweiche-Werkstatt 2 PCs, dazu Kunden; je Stand gezaehlt
        Assert.True(paare >= 6, "zu wenige Tische mit PC: " + paare);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Innenraum.PcTischAltFuerTests.Value = true;
        try
        {
            var (_, alt) = Pruefen();
            Assert.True(alt.Count > 0, "Gegenprobe: wie bis A 0.99.0 steckt der PC im Tisch");
        }
        finally
        {
            Innenraum.PcTischAltFuerTests.Value = false;
        }
    }
}
