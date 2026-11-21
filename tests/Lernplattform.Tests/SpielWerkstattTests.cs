using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.99.0 (Weisung P12-317, Sammelliste 355/356, Nico F1 = c): Serverschrank, USV, PC und Werkbank (mit Mainboard,
/// RAM, CPU) genauer als der Entwurf P12-194. Feste Dreieckszahlen je Teil, Obergrenzen mit Grund
/// (<see cref="Innenraum.WerkstattGrenzen"/>), jedes Teil bleibt in seinem Rechteck (±0,02) und beruehrt weder Wand noch
/// Nachbarn. Gegenprobe: alte Geometrie (<see cref="Innenraum.WerkstattAltFuerTests"/>) hat die Zahlen von heute.
/// </summary>
public class SpielWerkstattTests
{
    /// <summary>Dreiecke je Teil: heute (bis A 0.98.0), Entwurf P12-194, neu (fest).</summary>
    public static TheoryData<string, int, int, int> Zahlen => new()
    {
        { "rack", 276, 1836, NeuRack },
        { "usv", 36, 168, NeuUsv },
        { "pc", 12, 224, NeuPc },
        { "mainboard", 0, 288, NeuMainboard },
        { "ram", 0, 360, NeuRam },
        { "cpu", 0, 36, NeuCpu },
    };

    private const int NeuRack = 4752;
    private const int NeuUsv = 564;
    private const int NeuPc = 696;
    private const int NeuMainboard = 792;
    private const int NeuRam = 444;
    private const int NeuCpu = 192;

    private static int Dreiecke(IEnumerable<Teil> teile) => Netz.Aus(teile.ToList()).Indizes.Count / 3;

    private static Innenraum Serverfarm() =>
        Innenraum.Bauen("serverfarm", new Spielablauf(SpielInhaltTests.Inhalt).Stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;

    /// <summary>Feste Dreieckszahl je Teil, unter der Obergrenze, deutlich ueber dem Entwurf; alt = heute (Gegenprobe).</summary>
    [Theory]
    [MemberData(nameof(Zahlen))]
    public void DreieckeJeTeil_Fest_UnterGrenze_UeberEntwurf(string art, int heute, int entwurf, int neu)
    {
        var jetzt = Dreiecke(Innenraum.WerkstattTeile(art, 1.3f, 2.6f));
        Assert.Equal(neu, jetzt);
        Assert.True(jetzt <= Innenraum.WerkstattGrenzen[art], art + ": " + jetzt + " ueber der Grenze " + Innenraum.WerkstattGrenzen[art]);
        Assert.True(jetzt >= entwurf * 1.05, art + ": " + jetzt + " nicht genauer als der Entwurf " + entwurf);
        if (art is "rack" or "usv")
        {
            Innenraum.WerkstattAltFuerTests.Value = true;
            try
            {
                var alt = Serverfarm().DekoUmrisse.First(u => Py.Str(u.Deko["typ"]) == art).Teile;
                Assert.Equal(heute, Dreiecke(alt));
            }
            finally
            {
                Innenraum.WerkstattAltFuerTests.Value = false;
            }
        }
    }

    /// <summary>
    /// In der Serverfarm: acht Schraenke, vier USV, Werkbank mit den festen Zahlen; zusammen hoechstens 60 000 Dreiecke (unter
    /// der Haelfte der kleinsten Grenze 125 000, Niedrig), das ganze Innen-Netz ebenso.
    /// </summary>
    [Fact]
    public void Serverfarm_ZahlenUndWeitUnterGrenze()
    {
        var innen = Serverfarm();
        var teile = innen.DekoUmrisse.Where(u => Py.Str(u.Deko["typ"]) is "rack" or "usv" or "werkbank")
            .Select(u => (Typ: Py.Str(u.Deko["typ"]), Dreiecke: Dreiecke(u.Teile.Where(t => t.Art == TeilArt.Quader)))).ToList();
        Assert.Equal(Enumerable.Repeat(NeuRack, 8), teile.Where(t => t.Typ == "rack").Select(t => t.Dreiecke));
        Assert.Equal(Enumerable.Repeat(NeuUsv, 4), teile.Where(t => t.Typ == "usv").Select(t => t.Dreiecke));
        var werkbank = Assert.Single(teile, t => t.Typ == "werkbank").Dreiecke;
        Assert.True(werkbank <= Innenraum.WerkstattGrenzen["werkbank"], "Werkbank " + werkbank);
        Assert.True(teile.Sum(t => t.Dreiecke) <= 60_000, "Werkstatt zusammen " + teile.Sum(t => t.Dreiecke));
        var gesamt = innen.NetzOhneWaende.Indizes.Count / 3;
        Assert.True(gesamt <= 60_000, "Serverfarm gesamt " + gesamt);
    }

    /// <summary>
    /// Jedes Werkstatt-Teil bleibt in seinem Rechteck (±0,02, Hoehe 0 bis zur Decke der Ich-Sicht), beruehrt kein anderes
    /// Einrichtungsstueck (Durchdringung 0, ±0,02) und keine Wand (<see cref="SpielWandTests.Funde"/>).
    /// Gegenprobe: ein Schrank 0,1 nach vorn verschoben ragt aus dem Rechteck.
    /// </summary>
    [Fact]
    public void Teile_ImRechteck_OhneNachbarnUndWand()
    {
        const float spiel = 0.02f;
        var innen = Serverfarm();
        var werkstatt = innen.DekoUmrisse.Where(u => Py.Str(u.Deko["typ"]) is "rack" or "usv" or "werkbank").ToList();
        Assert.Equal(13, werkstatt.Count);
        var funde = new List<string>();
        static (float X, float Z, float W, float T) Rechteck(DekoUmriss u) =>
            ((float)Py.F(u.Deko["x"]), (float)Py.F(u.Deko["y"]), (float)Py.F(u.Deko["w"]), (float)Py.F(u.Deko["h"]));
        bool Drin(Teil t, (float X, float Z, float W, float T) r) =>
            MathF.Min(t.X1, t.X2) >= r.X - spiel && MathF.Max(t.X1, t.X2) <= r.X + r.W + spiel
            && MathF.Min(t.Z1, t.Z2) >= r.Z - spiel && MathF.Max(t.Z1, t.Z2) <= r.Z + r.T + spiel
            && MathF.Min(t.Y1, t.Y2) >= -spiel && MathF.Max(t.Y1, t.Y2) <= Innenraum.IchWandHoehe;
        foreach (var u in werkstatt)
        {
            var r = Rechteck(u);
            var typ = Py.Str(u.Deko["typ"]);
            foreach (var t in u.Teile.Where(t => t.Art == TeilArt.Quader).Where(t => !Drin(t, r)))
            {
                funde.Add(typ + " bei " + r.X + "/" + r.Z + ": Kasten " + t.X1 + "/" + t.Z1 + "-" + t.X2 + "/" + t.Z2 + " ausserhalb");
            }
            foreach (var andere in innen.DekoUmrisse.Where(a => !ReferenceEquals(a, u)))
            {
                foreach (var t in u.Teile)
                {
                    foreach (var o in andere.Teile.Where(o => o.Art == TeilArt.Quader))
                    {
                        float Tief(float a1, float a2, float b1, float b2) =>
                            MathF.Min(MathF.Max(a1, a2), MathF.Max(b1, b2)) - MathF.Max(MathF.Min(a1, a2), MathF.Min(b1, b2));
                        if (Tief(t.X1, t.X2, o.X1, o.X2) > spiel && Tief(t.Y1, t.Y2, o.Y1, o.Y2) > spiel && Tief(t.Z1, t.Z2, o.Z1, o.Z2) > spiel)
                        {
                            funde.Add(typ + " bei " + r.X + "/" + r.Z + " beruehrt " + Py.Str(andere.Deko["typ"]));
                            break;
                        }
                    }
                }
            }
        }
        funde.AddRange(SpielWandTests.Funde(innen, werkstatt));
        Assert.True(funde.Count == 0, string.Join("\n", funde.Distinct().Take(20)));
        // Gegenprobe: verschoben ragt der Schrank aus dem Rechteck
        var rack = werkstatt.First(u => Py.Str(u.Deko["typ"]) == "rack");
        var rr = Rechteck(rack);
        Assert.Contains(rack.Teile.Where(t => t.Art == TeilArt.Quader), t => !Drin(t with { Z1 = t.Z1 + 0.1f, Z2 = t.Z2 + 0.1f }, rr));
    }

    /// <summary>E1: die Schraenke zeigen mit der Front (Schienen, Geraete) zum Gang im Sueden, nicht in die schmale Luecke daneben.</summary>
    [Fact]
    public void Schraenke_FrontZumGang()
    {
        foreach (var u in Serverfarm().DekoUmrisse.Where(u => Py.Str(u.Deko["typ"]) == "rack"))
        {
            // die Lochreihe/Blenden liegen vorn: die meisten Kaesten nahe der Vorderkante (groesstes z)
            var z1 = (float)(Py.F(u.Deko["y"]) + Py.F(u.Deko["h"]));
            var vorn = u.Teile.Count(t => MathF.Max(t.Z1, t.Z2) > z1 - 0.3f);
            Assert.True(vorn > u.Teile.Count / 2, "Front nicht zum Gang: " + vorn + " von " + u.Teile.Count);
        }
    }
}
