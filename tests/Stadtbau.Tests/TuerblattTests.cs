using System.Numerics;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Sammelliste 267 (Weisung P12-129/P12-140, Nico: die Tuer an den Tuer-Teilen des Gebaeude-Editors sieht seit Fund 262 aus
/// wie ein Fenster). Jetzt wird die eigene Glastuer zum dunklen Tuerblatt mit Klinke; sie bleibt in ihrer Ebene (Regel 262
/// unveraendert gruen), die Klinke steht in der Laibung (nicht vor der Fassade). Gegenprobe: Verhalten von A 0.55.0
/// (<see cref="StadtTueren.TuerblattAltFuerTests"/>) = Glas, keine Klinke.
/// </summary>
public partial class TuerBuendigTests
{
    private static List<Teil> Klinken(StadtSzene sz, StadtTuer t) =>
        [.. sz.Teile.Where(k => k.Farbe == StadtTueren.Klinke && Vector2.Distance(new Vector2((k.X1 + k.X2) / 2, (k.Z1 + k.Z2) / 2), t.Mitte) <= 0.5f)];

    [Theory]
    [MemberData(nameof(TuerTeile))]
    public void EditorTuerteil_Tuerblatt_UndKlinke_InDerLaibung_GegenprobeGlas(string tuer)
    {
        var p = Gesetzt("Haus " + tuer, Haus(tuer));
        var sz = Szene(p);
        var t = Assert.Single(sz.Tueren);
        Assert.True(t.Modell);
        var haus = Assert.Single(sz.Haeuser, h => h.Id.StartsWith(Bauteile.ModellPraefix, StringComparison.Ordinal));
        var eigene = Assert.Single(StadtTueren.ModellTueren(sz.Stuecke, haus.StueckeVon, haus.StueckeBis, sz.ModellAn, t.Mitte, t.Aussen, weite: 0.1f, glas: true),
            g => g.A1 <= 0.001f && g.A2 >= -0.001f);
        var gm = sz.ModellAn(eigene.Stueck)!;
        foreach (var d in eigene.Dreiecke)
        {
            var f = new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f;
            Assert.True(StadtTueren.IstTuerblatt(f), $"{tuer}: Dreieck {d} ist noch Glas");
        }
        var klinke = Assert.Single(Klinken(sz, t));
        // Klinke auf dem Tuerblatt (quer innerhalb, unter der Oberkante) und hinter der Fassade (Laibung 0,1 tief)
        var quer = new Vector2(-t.Aussen.Y, t.Aussen.X);
        var ecken = new[] { new Vector2(klinke.X1, klinke.Z1), new Vector2(klinke.X2, klinke.Z2) };
        foreach (var e in ecken)
        {
            var r = e - t.Mitte;
            var q = Vector2.Dot(r, quer);
            Assert.InRange(q, eigene.A1, eigene.A2);
            Assert.True(Vector2.Dot(r, t.Aussen) <= StadtTueren.KlinkeVor + 0.001f, $"{tuer}: Klinke steht vor der Laibung");
        }
        Assert.InRange(klinke.Y2, 0.05f, eigene.Oben);
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
        StadtTueren.TuerblattAltFuerTests = true;
        try
        {
            var alt = Szene(p);
            var ta = Assert.Single(alt.Tueren);
            Assert.Empty(Klinken(alt, ta));
            var ga = alt.ModellAn(eigene.Stueck)!;
            Assert.All(eigene.Dreiecke, d => Assert.True(StadtTueren.IstGlas(new Vector3(ga.Farben[d * 3], ga.Farben[(d * 3) + 1], ga.Farben[(d * 3) + 2]) / 255f)));
        }
        finally
        {
            StadtTueren.TuerblattAltFuerTests = false;
        }
    }
}
