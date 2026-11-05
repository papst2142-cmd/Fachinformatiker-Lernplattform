using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Weisungen P12-6/P12-7 (Fund Bild 06): in jedem Geschaeft (Moebelhaus, Elektromarkt, Kaufhaus, mit allen Zusaetzen
/// wie beim Betreten) beruehrt kein Stueck der Auslage ein anderes, jede Ware ist zu Fuss erreichbar, und hinter den
/// Kleiderschraenken bleibt ein Gang frei.
/// Gemessen wird, was gezeichnet ist (<see cref="Innenraum.DekoUmrisse"/>, Modelle mit ihrer Lage), nicht nur das
/// Rechteck aus innenraeume.json (Stuehle am Esstisch, Bildschirme auf dem Schreibtisch ragen darueber hinaus).
/// Grenze 0, Toleranz 0,02 wie <see cref="SpielSitzenTests"/>.
/// </summary>
public class SpielAuslageTests
{
    private const float Toleranz = 0.02f;

    internal static IEnumerable<(string Ort, List<DekoUmriss> Auslage, List<DekoUmriss> Alle)> Geschaefte()
    {
        var stand = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        foreach (var ort in new[] { "moebelhaus", "elektromarkt", "kaufhaus" })
        {
            var innen = Innenraum.Bauen("geschaeft:" + ort, stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value);
            Assert.NotNull(innen);
            var alle = innen.DekoUmrisse.ToList();
            yield return (ort, alle.Where(u => Py.Hat(u.Deko, "ware")).ToList(), alle);
        }
    }

    private static string Name(DekoUmriss u) => Py.Str(u.Deko["typ"]) + " " + (Py.Hat(u.Deko, "ware") ? Py.Str(u.Deko["ware"]) : "") +
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $" @{Py.F(u.Deko["x"])},{Py.F(u.Deko["y"])}");

    /// <summary>Ueberlappung zweier Flaechen (x1, z1, x2, z2), je Seite um die Toleranz kleiner.</summary>
    private static bool Ueberlappt((float X1, float Z1, float X2, float Z2) a, (float X1, float Z1, float X2, float Z2) b) =>
        Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1) > Toleranz && Math.Min(a.Z2, b.Z2) - Math.Max(a.Z1, b.Z1) > Toleranz;

    /// <summary>Kein Auslage-Stueck beruehrt mit seiner gezeichneten Grundflaeche ein anderes Stueck (Auslage oder Einrichtung, ohne Bodenbelag).</summary>
    [Fact]
    public void AuslageUeberschneidetSichNicht()
    {
        var funde = new List<string>();
        foreach (var (ort, auslage, alle) in Geschaefte())
        {
            Assert.NotEmpty(auslage);
            foreach (var a in auslage)
            {
                var fa = a.Flaeche;
                foreach (var b in alle.Where(b => !ReferenceEquals(a, b) && b.Flaeche.Oben > 0.1f))
                {
                    // Paare nur einmal (beide Auslage: das zweite Mal ueberspringen)
                    if (auslage.Contains(b) && auslage.IndexOf(b) < auslage.IndexOf(a))
                    {
                        continue;
                    }
                    var fb = b.Flaeche;
                    if (Ueberlappt((fa.X1, fa.Z1, fa.X2, fa.Z2), (fb.X1, fb.Z1, fb.X2, fb.Z2)))
                    {
                        funde.Add(ort + ": " + Name(a) + " / " + Name(b));
                    }
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>
    /// Weisung P12-7 (Nico): hinter den vier Kleiderschraenken des Moebelhauses bleibt ein Gang frei. Die Figur geht von
    /// links neben dem kleinen bis rechts neben den begehbaren Schrank, ohne nach vorn um die Schraenke herum zu muessen
    /// und ohne durch die Reihe dahinter auszuweichen (jeder Punkt des Wegs liegt zwischen Auslage und Rueckseite der Schraenke).
    /// </summary>
    [Fact]
    public void GangHinterDenSchraenkenFrei()
    {
        var stand = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        var innen = Innenraum.Bauen("geschaeft:moebelhaus", stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;
        var schraenke = innen.DekoUmrisse.Where(u => Py.Gleich(u.Deko["typ"], "kleiderschrank")).Select(u => u.Flaeche).ToList();
        Assert.Equal(4, schraenke.Count);
        float links = schraenke.Min(f => f.X1), rechts = schraenke.Max(f => f.X2), hinten = schraenke.Min(f => f.Z1);
        var vor = innen.DekoUmrisse.Where(u => Py.Hat(u.Deko, "ware") && u.Flaeche.Z2 <= hinten && u.Flaeche.X2 > links && u.Flaeche.X1 < rechts)
            .Max(u => u.Flaeche.Z2);
        var mitte = (vor + hinten) / 2;
        var weg = innen.Weg(links - 0.6, mitte, rechts + 0.6, mitte);
        Assert.NotEmpty(weg);
        Assert.True(Math.Abs(weg[^1].X - (rechts + 0.6)) < 0.3, "Weg endet bei " + weg[^1]);
        Assert.All(weg, p => Assert.True(p.Y > vor && p.Y < hinten, "Weg verlaesst den Gang zwischen Auslage und Schraenken: " + p));
    }

    /// <summary>
    /// Bekannt und nicht Teil dieser Welle (Sammelliste 216): im Elektromarkt sperrt die durchgehende zweite Reihe
    /// (Regale y 4,2 mit 0,5 Abstand, rechts die Auslage 20,7/4,2) den Gang zur oberen Reihe; deren Regale erreicht die Figur nicht.
    /// Feste Tabelle: wird sie behoben, Tabelle leeren.
    /// </summary>
    private static readonly string[] BekanntUnerreichbar =
    [
        "elektromarkt teile:mainboard", "elektromarkt teile:cpu", "elektromarkt teile:ram", "elektromarkt teile:ssd",
        "elektromarkt teile:gpu", "elektromarkt teile:netzteil", "elektromarkt teile:gehaeuse", "elektromarkt teile:notebook",
    ];

    /// <summary>
    /// Jede Ware der Auslage ist zu Fuss erreichbar: Tippen auf die Ware laeuft vom Eingang wie im Spiel
    /// (<see cref="StadtSpiel"/>: Ziel Mitte, Reichweite 0,9 + halbe kleinere Seite); der Weg endet in der Reichweite
    /// von <see cref="Innenraum.WareNahe"/> (0,9 + 0,3 vom Rand). Ausnahmen nur laut <see cref="BekanntUnerreichbar"/>.
    /// </summary>
    [Fact]
    public void JedeWareErreichbar()
    {
        var stand = new Spielablauf(SpielInhaltTests.Inhalt).Stand;
        var funde = new List<string>();
        foreach (var ort in new[] { "moebelhaus", "elektromarkt", "kaufhaus" })
        {
            var innen = Innenraum.Bauen("geschaeft:" + ort, stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;
            foreach (var d in Core.Spiel.Geschaefte.Auslage(new PyObjekt { { "gebaeude", innen.Gebaeude } }))
            {
                double x = Py.F(d["x"]), z = Py.F(d["y"]), w = Py.F(d["w"]), t = Py.F(d["h"]);
                var weg = innen.Weg(innen.StartX, innen.StartZ, x + (w / 2), z + (t / 2), Wohnung.MoebelReichweite + (Math.Min(w, t) / 2));
                var (ex, ez) = weg.Count == 0 ? (innen.StartX, innen.StartZ) : weg[^1];
                var dx = Math.Max(Math.Max(x - ex, 0), ex - x - w);
                var dz = Math.Max(Math.Max(z - ez, 0), ez - z - t);
                if (Math.Sqrt((dx * dx) + (dz * dz)) > Wohnung.MoebelReichweite + 0.3)
                {
                    funde.Add(ort + " " + Py.Str(d["ware"]));
                }
            }
        }
        Assert.Equal(BekanntUnerreichbar.Order(StringComparer.Ordinal), funde.Order(StringComparer.Ordinal));
    }
}
