using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.2.0 (Sammelliste 411, Weisung P12-332 Punkt 2, Nico: „der Karton glitcht durch den Tisch“). Nicos Ziel: in keinem
/// Innenraum steckt ein Einrichtungsstueck in einem anderen. Geprueft werden alle Paare von Stuecken, die sich im Grundriss
/// ueberlappen, in allen Innenraeumen (neu, Tag 200, gespielter Stand, dazu die Wohnungen): kein gezeichnetes Teil des einen
/// liegt in einem Teil des anderen (Durchdringung je Achse hoechstens 0,02). Ausgenommen sind nur Stuecke, die flach auf dem
/// Boden oder an der Wand liegen (Teppich, Fussmatte, Markierung, Bild, Whiteboard), und Paare, die zusammen ein Stueck bilden
/// (Liste <see cref="Erlaubt"/> mit Grund). Gegenprobe: Kartons wie bis A 1.1.0 (zwei uebereinander, Tisch mit Streben) stecken im Tisch.
/// </summary>
public sealed class SpielDurchdringungTests
{
    private const float Toleranz = 0.02f;

    /// <summary>Flach am Boden oder an der Wand: steht nichts im Weg.</summary>
    private static readonly HashSet<string> Flach = ["teppich", "fussmatte", "markierung", "bild", "whiteboard", "wimpel", "kamera"];

    /// <summary>Paare (Typ a, Typ b), die zusammen ein Stueck bilden, mit Grund.</summary>
    private static readonly Dictionary<(string, string), string> Erlaubt = new()
    {
        [("gleis", "zug")] = "der Zug steht mit seinen Raedern auf dem Gleis (Betriebswerkstatt)",
    };

    private static bool Ueber(PyObjekt a, PyObjekt b) =>
        Py.F(a["x"]) < Py.F(b["x"]) + Py.F(b["w"]) && Py.F(b["x"]) < Py.F(a["x"]) + Py.F(a["w"])
        && Py.F(a["y"]) < Py.F(b["y"]) + Py.F(b["h"]) && Py.F(b["y"]) < Py.F(a["y"]) + Py.F(a["h"]);

    private static float Tief(float a1, float a2, float b1, float b2) =>
        Math.Min(Math.Max(a1, a2), Math.Max(b1, b2)) - Math.Max(Math.Min(a1, a2), Math.Min(b1, b2));

    internal static IEnumerable<(string Stand, Innenraum Innen)> AlleMitWohnung()
    {
        foreach (var r in SpielSitzenTests.AlleRaeume())
        {
            yield return r;
        }
        var gespielt = SpielFingerabdruckTests.Gespielt.Value.Stand;
        if (Innenraum.Bauen("zuhause", gespielt, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value) is { } zuhause)
        {
            yield return ("gespielt", zuhause);
        }
        var neu = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        if (Innenraum.Bauen("zuhause", neu, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value) is { } start)
        {
            yield return ("neu", start);
        }
    }

    /// <summary>Paare (ueberlappend im Grundriss) und Funde.</summary>
    internal static (int Paare, List<string> Funde) Pruefen(Func<string, string, bool>? nur = null)
    {
        var paare = 0;
        var funde = new List<string>();
        foreach (var (stand, innen) in AlleMitWohnung())
        {
            var umrisse = innen.DekoUmrisse.Where(u => !Flach.Contains(Py.Str(u.Deko["typ"])) && u.Teile.Count > 0).ToList();
            for (var i = 0; i < umrisse.Count; i++)
            {
                for (var j = i + 1; j < umrisse.Count; j++)
                {
                    var (a, b) = (umrisse[i], umrisse[j]);
                    var (ta, tb) = (Py.Str(a.Deko["typ"]), Py.Str(b.Deko["typ"]));
                    if (!Ueber(a.Deko, b.Deko) || Erlaubt.ContainsKey((ta, tb)) || Erlaubt.ContainsKey((tb, ta)) || (nur is not null && !nur(ta, tb)))
                    {
                        continue;
                    }
                    paare++;
                    var fund = a.Teile.SelectMany(x => b.Teile.Select(y => (Tief(x.X1, x.X2, y.X1, y.X2), Tief(x.Y1, x.Y2, y.Y1, y.Y2), Tief(x.Z1, x.Z2, y.Z1, y.Z2))))
                        .FirstOrDefault(d => d.Item1 > Toleranz && d.Item2 > Toleranz && d.Item3 > Toleranz);
                    if (fund != default && funde.Count < 40)
                    {
                        funde.Add(string.Create(CultureInfo.InvariantCulture,
                            $"{stand} {innen.Ansicht}: {ta} {Py.F(a.Deko["x"])}/{Py.F(a.Deko["y"])} in {tb} {Py.F(b.Deko["x"])}/{Py.F(b.Deko["y"])} ({fund.Item1:F2} x {fund.Item2:F2} x {fund.Item3:F2})"));
                    }
                }
            }
        }
        return (paare, funde);
    }

    [Fact]
    public void Einrichtung_DurchdringtSichNie_Gegenprobe()
    {
        var (paare, funde) = Pruefen();
        Assert.True(paare >= 10, "zu wenige ueberlappende Paare: " + paare);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Innenraum.KartonsAltFuerTests.Value = true;
        try
        {
            var (_, alt) = Pruefen((a, b) => a == "kartons" || b == "kartons");
            Assert.Contains(alt, f => f.Contains("kartons 13.8/11.55", StringComparison.Ordinal));
        }
        finally
        {
            Innenraum.KartonsAltFuerTests.Value = false;
        }
    }
}
