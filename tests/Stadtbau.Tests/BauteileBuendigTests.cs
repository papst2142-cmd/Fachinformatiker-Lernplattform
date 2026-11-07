using System.Numerics;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-52 (Nico-Fund 07.11.2026 09:56 MEZ: Dach und Tuer wirkten versetzt zum Grundgebaeude): jedes Wand- und
/// Dach-Teil steht nach dem Raster seines Pakets (Ursprung des Modells = Mitte des Felds auf dem Boden der Ebene), nie nach
/// seinem umschliessenden Kasten. Ueberstaende (Markise, Balkon, Sims, Dachueberstand) verschieben die Lage also nicht.
/// Ursache des Funds: vier Wandteile hat Kenney selbst nach dem Kasten mittig gesetzt (<see cref="Bauteile.RasterVersatz"/>).
/// Pruefung je Teil und je Drehung mit Grenze 0,01; die Gegenprobe richtet wie vermutet nach dem Kasten aus und ist rot.
/// </summary>
public class BauteileBuendigTests
{
    private const float Grenze = 0.01f;

    private static GrafikModell M(string name) => Katalog.Bauteil(Bauteile.Praefix + name) ?? throw new InvalidOperationException("Modell fehlt: " + name);

    /// <summary>Gegenprobe: Ausrichtung nach dem umschliessenden Kasten (Mitte des Kastens in die Mitte des Felds, vorderste
    /// Stelle an die Kante der Fassade), so wie der Verdacht in der Weisung lautete.</summary>
    private static Matrix4x4 NachKasten(Bauteile.Teil t, GrafikModell m)
    {
        var mitte = (m.Min + m.Max) / 2;
        var vorne = t.Art!.Fassade.HasFlag(Bauteile.Seiten.Sued) ? 0.5f - m.Max.Z : -mitte.Z;
        var schieben = Matrix4x4.CreateTranslation(-mitte.X, -m.Min.Y, vorne);
        return schieben * Matrix4x4.CreateRotationY(-MathF.PI / 2 * t.Dreh) * Matrix4x4.CreateTranslation(t.X + 0.5f, t.Stockwerk * Bauteile.Stockwerk, t.Y + 0.5f);
    }

    private static IEnumerable<string> WandUndDach() => Bauteile.Tabelle.Where(p => p.Value.Art is not (Bauteile.Art.Detail or Bauteile.Art.DachDetail)).Select(p => p.Key).Order(StringComparer.Ordinal);

    /// <summary>Flaeche der Dreiecke, die genau in der Ebene einer Feldkante liegen und nach aussen zeigen.</summary>
    private static float WandFlaeche(GrafikModell m, Matrix4x4 lage, Bauteile.Seiten seite, int x, int y)
    {
        var (normale, kante, achse) = seite switch
        {
            Bauteile.Seiten.Nord => (-Vector3.UnitZ, (float)y, 2),
            Bauteile.Seiten.Sued => (Vector3.UnitZ, y + 1f, 2),
            Bauteile.Seiten.West => (-Vector3.UnitX, (float)x, 0),
            _ => (Vector3.UnitX, x + 1f, 0),
        };
        var summe = 0f;
        for (var d = 0; d < m.Dreiecke; d++)
        {
            var p = new Vector3[3];
            for (var k = 0; k < 3; k++)
            {
                var i = (d * 9) + (k * 3);
                p[k] = Vector3.Transform(new Vector3(m.Ecken[i], m.Ecken[i + 1], m.Ecken[i + 2]), lage);
            }
            var kreuz = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
            if (kreuz.Length() < 1e-9f || MathF.Abs(Vector3.Dot(Vector3.Normalize(kreuz), normale)) < 0.99f)
            {
                continue;
            }
            if (p.All(q => MathF.Abs((achse == 0 ? q.X : q.Z) - kante) <= Grenze))
            {
                summe += kreuz.Length() / 2;
            }
        }
        return summe;
    }

    /// <summary>Alle Funde eines Teils in allen vier Drehungen (leer = buendig).</summary>
    private static List<string> Funde(string name, Func<Bauteile.Teil, GrafikModell, Matrix4x4> lageVon)
    {
        var funde = new List<string>();
        var a = Bauteile.Tabelle[name];
        var m = M(name);
        const int x = 3, y = 5, stock = 1;
        for (var dreh = 0; dreh < 4; dreh++)
        {
            var t = new Bauteile.Teil(Bauteile.Praefix + name, x, y, stock, dreh);
            var lage = lageVon(t, m);
            // Raster des Pakets: der Ursprung des Modells (bei den vier von Kenney mittig gesetzten Teilen um den Versatz
            // aus der Tabelle verschoben) liegt in der Mitte des Felds auf dem Boden der Ebene
            var ursprung = Vector3.Transform(new Vector3(0, 0, -Bauteile.RasterVersatz.GetValueOrDefault(name)), lage);
            if (Vector3.Distance(ursprung, new Vector3(x + 0.5f, stock * Bauteile.Stockwerk, y + 0.5f)) > Grenze)
            {
                funde.Add($"{name} Drehung {dreh}: Ursprung bei {ursprung}");
            }
            if (a.IstWand)
            {
                // jede Seite des Baukoerpers liegt genau in der Ebene der Feldkante (Ueberstaende ragen nur davor)
                foreach (var seite in Bauteile.Alle)
                {
                    var f = WandFlaeche(m, lage, seite, x, y);
                    if (f < 0.2f)
                    {
                        funde.Add($"{name} Drehung {dreh}: Wand {seite} nicht in der Feldkante ({f:F3})");
                    }
                }
            }
            else
            {
                // Dach: deckt das Feld ganz, Ueberstand nur nach aussen; wo es auf zwei Gegenseiten uebersteht, gleich weit
                var (min, max) = Kasten(m, lage);
                var ueber = new[] { x - min.X, max.X - (x + 1), y - min.Z, max.Z - (y + 1) };
                if (ueber.Any(u => u < -Grenze))
                {
                    funde.Add($"{name} Drehung {dreh}: deckt das Feld nicht ({string.Join(" ", ueber.Select(u => u.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)))})");
                }
                for (var paar = 0; paar < 4; paar += 2)
                {
                    if (ueber[paar] > Grenze && ueber[paar + 1] > Grenze && MathF.Abs(ueber[paar] - ueber[paar + 1]) > Grenze)
                    {
                        funde.Add($"{name} Drehung {dreh}: Ueberstand ungleich ({ueber[paar]:F3} / {ueber[paar + 1]:F3})");
                    }
                }
            }
        }
        return funde;
    }

    private static (Vector3 Min, Vector3 Max) Kasten(GrafikModell m, Matrix4x4 lage)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 0; i + 2 < m.Ecken.Length; i += 3)
        {
            var p = Vector3.Transform(new Vector3(m.Ecken[i], m.Ecken[i + 1], m.Ecken[i + 2]), lage);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }

    [Fact]
    public void JedesWandUndDachTeil_BuendigImRaster_InJederDrehung()
    {
        var funde = WandUndDach().SelectMany(n => Funde(n, Bauteile.Lage)).ToList();
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>Gegenprobe: die Ausrichtung von S 0.9.0 (ohne <see cref="Bauteile.RasterVersatz"/>).</summary>
    private static Matrix4x4 AltFuerTests(Bauteile.Teil t, GrafikModell m) =>
        Matrix4x4.CreateTranslation(0, 0, -Bauteile.RasterVersatz.GetValueOrDefault(t.Name)) * Bauteile.Lage(t, m);

    [Fact]
    public void Gegenprobe_AlteAusrichtung_S090_IstRot()
    {
        foreach (var n in Bauteile.RasterVersatz.Keys)
        {
            var funde = Funde(n, AltFuerTests);
            Assert.Contains(funde, f => f.Contains("nicht in der Feldkante", StringComparison.Ordinal));
        }
        Assert.Equal(4, Bauteile.RasterVersatz.Count);
    }

    [Fact]
    public void Gegenprobe_AusrichtungNachDemKasten_IstRot()
    {
        // mit Ueberstand: Fenster mit Markisen/Balkon/Sims, Vordach, Giebel-Ecke, Schraegdach
        var mitUeberstand = new[] { "building-window-awnings", "building-window-balcony", "building-window-sill", "roof-flat-awning-a", "roof-gable-corner", "roof-slanted" };
        foreach (var n in mitUeberstand)
        {
            Assert.True(Funde(n, NachKasten).Count > 0, n + ": Gegenprobe haette rot sein muessen");
        }
        // ohne Ueberstand ist der Kasten das Feld: beide Wege gleich (die Pruefung misst also wirklich die Lage)
        Assert.Empty(Funde("building-block", NachKasten));
    }

    [Fact]
    public void ZusammengesetztesHaus_EG_Obergeschosse_Giebeldach_UebereinanderBuendig()
    {
        // Nachbau von Nicos Haus: EG Tuer, davor Haustuer; 1. und 2. OG Fensterwand; Satteldach, in allen vier Drehungen
        foreach (var dreh in Enumerable.Range(0, 4))
        {
            var p = Bauteile.Praefix;
            var teile = new List<Bauteile.Teil>
            {
                new(p + "building-door", 0, 0, 0, dreh), new(p + "door-white", 0, 0, 0, dreh),
                new(p + "building-window-awnings", 0, 0, 1, dreh), new(p + "building-window", 0, 0, 2, dreh), new(p + "roof-gable", 0, 0, 3, dreh),
            };
            var gm = Bauteile.Zusammensetzen("bau:test", teile, Katalog.Bauteil);
            // jedes Stockwerk: Kasten des Baukoerpers (ohne Ueberstand) genau im Feld 0..1
            foreach (var t in teile.Where(t => t.Art!.IstWand))
            {
                var m = M(t.Name);
                foreach (var seite in Bauteile.Alle)
                {
                    Assert.True(WandFlaeche(m, Bauteile.Lage(t, m), seite, 0, 0) >= 0.2f, $"{t.Name} Drehung {dreh} {seite}");
                }
            }
            // das Dach liegt auf der Oberkante des 2. OG
            var dach = teile[^1];
            var (dmin, _) = Kasten(M(dach.Name), Bauteile.Lage(dach, M(dach.Name)));
            Assert.InRange(dmin.Y, (3 * Bauteile.Stockwerk) - Grenze, (3 * Bauteile.Stockwerk) + Grenze);
            Assert.True(gm.Dreiecke > 0);
        }
    }
}
