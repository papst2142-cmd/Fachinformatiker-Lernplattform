using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.3.1 (Weisung P12-348, Sammelliste 432, Nico 22.11.2026: „Bei Bild 4 schwebt das pinke Rechteck, was ein Bild
/// darstellen soll, in der Luft“; Nico 2a „Staffelei“). Abnahme: kein Bild schwebt. Jedes Bild in jedem Innenraum (beide
/// Fassungen, jeder Spielstand) und ein gekauftes Bild in der Wohnung (an der Wand und mitten im Raum) haengt entweder an einer
/// Wand (ein Teil des Bilds hoechstens <see cref="Innenraum.BildWandAbstand"/> plus 0,02 vor einer Wand) oder steht auf dem
/// Boden (auf der Staffelei, ein Teil reicht bis 0). Das Bild im Moebelhaus steht auf der Staffelei, das Bild an der Wand der
/// Wohnung haengt weiter an der Wand. Gegenprobe: wie bis A 1.3.0 (<see cref="Innenraum.BildAltFuerTests"/>) schwebt das Bild
/// im Moebelhaus.
/// </summary>
public class SpielBildTests
{
    private enum Lage
    {
        Wand,
        Boden,
        Schwebt,
    }

    private static Lage Pruefen(Innenraum innen, DekoUmriss u)
    {
        if (u.Kaesten.Any(k => MathF.Min(k.Y1, k.Y2) <= 0.001f))
        {
            return Lage.Boden;
        }
        var rand = Innenraum.BildWandAbstand + 0.02f;
        var waende = SpielWandTests.WandQuader(innen).ToList();
        return u.Kaesten.Any(k => waende.Any(w =>
            MathF.Min(k.X1, k.X2) - rand < MathF.Max(w.Q.X1, w.Q.X2) && MathF.Min(w.Q.X1, w.Q.X2) < MathF.Max(k.X1, k.X2) + rand
            && MathF.Min(k.Z1, k.Z2) - rand < MathF.Max(w.Q.Z1, w.Q.Z2) && MathF.Min(w.Q.Z1, w.Q.Z2) < MathF.Max(k.Z1, k.Z2) + rand))
            ? Lage.Wand : Lage.Schwebt;
    }

    /// <summary>Jedes Bild: Name des Raums und seine Lage.</summary>
    private static List<(string Raum, Lage Lage)> AlleBilder()
    {
        var liste = new List<(string, Lage)>();
        foreach (var (name, innenraeume) in SpielWandTests.Fassungen())
        {
            foreach (var innen in SpielWandTests.AlleRaeume(innenraeume))
            {
                liste.AddRange(innen.DekoUmrisse.Where(u => Py.Str(u.Deko["typ"]) == "bild").Select(u => ($"{name} {innen.Ansicht}", Pruefen(innen, u))));
            }
        }
        // gekauftes Bild in der Wohnung: an die Wand gestellt und mitten im Raum
        var inhalt = SpielCharakterTests.InhaltMitNeuen.Value;
        var bleibe = SpielDaten.Liste(SpielDaten.Objekt(inhalt["wohnungen"])["wohnungen"]).Select(SpielDaten.Objekt).First();
        var raum = SpielDaten.Liste(SpielDaten.Objekt(bleibe["gebaeude"])["raeume"]).Select(SpielDaten.Objekt).First();
        double rx = Py.F(raum["x"]), ry = Py.F(raum["y"]), rw = Py.F(raum["w"]), rh = Py.F(raum["h"]);
        foreach (var (wo, ecke) in new[] { ("Wand", true), ("Mitte", false) })
        {
            var stand = new Spielablauf(inhalt).Stand;
            stand.Wohnung = bleibe["id"];
            stand.Moebel["probe"] = "bild";
            var (x, y) = ecke ? Wohnung.Andocken(stand, "bild", rx + (rw / 2), ry, 0L) : ((object)Math.Round(rx + (rw / 2) - 0.4, 2), (object)Math.Round(ry + (rh / 2), 2));
            stand.Einrichtungen[stand.Wohnung] = new PyObjekt { { "moebel", new PyObjekt { { "probe", new List<object?> { x, y, 0L } } } }, { "boeden", new PyObjekt() } };
            var innen = Innenraum.Bauen("zuhause", stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;
            liste.AddRange(innen.DekoUmrisse.Where(u => Py.Str(u.Deko["typ"]) == "bild").Select(u => ("Wohnung " + wo, Pruefen(innen, u))));
        }
        return liste;
    }

    [Fact]
    public void KeinBildSchwebt_MoebelhausAufDerStaffelei_WohnungAnDerWand()
    {
        var bilder = AlleBilder();
        var funde = bilder.Where(b => b.Lage == Lage.Schwebt).Select(b => b.Raum + ": Bild schwebt").ToList();
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.Contains(bilder, b => b.Raum.EndsWith("geschaeft:moebelhaus", StringComparison.Ordinal) && b.Lage == Lage.Boden);
        Assert.Contains(bilder, b => b.Raum == "Wohnung Wand" && b.Lage == Lage.Wand);
        Assert.Contains(bilder, b => b.Raum == "Wohnung Mitte" && b.Lage == Lage.Boden);
    }

    /// <summary>Gegenprobe: wie bis A 1.3.0 schwebt das Bild im Moebelhaus (und mitten in der Wohnung).</summary>
    [Fact]
    public void Gegenprobe_BildWieBisA130_SchwebtImMoebelhaus()
    {
        Innenraum.BildAltFuerTests.Value = true;
        try
        {
            var bilder = AlleBilder();
            Assert.Contains(bilder, b => b.Raum.EndsWith("geschaeft:moebelhaus", StringComparison.Ordinal) && b.Lage == Lage.Schwebt);
            Assert.Contains(bilder, b => b.Raum == "Wohnung Mitte" && b.Lage == Lage.Schwebt);
            Assert.Contains(bilder, b => b.Raum == "Wohnung Wand" && b.Lage == Lage.Wand);
        }
        finally
        {
            Innenraum.BildAltFuerTests.Value = false;
        }
    }
}
