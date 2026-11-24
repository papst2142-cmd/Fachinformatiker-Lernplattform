using System.Text.Json;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.7.0 (Sammelliste 447, Weisung P12-367, Bodenmessung aus Meldung P12-233). Abnahmesatz: beim Kriechen liegt die
/// Figur flach auf dem Bauch; in jedem Bild der Bewegung <see cref="Figur.Robben"/> und im stillen Bild
/// <see cref="Figur.RobbenStill"/> liegt die tiefste Stelle von Brust, Bauch und Becken hoechstens 0,02 Figurhoehen ueber dem
/// Boden, und keine Ecke der Figur liegt unter dem Boden. Boden = Fusssohle der stehenden Figur. Welche Dreiecke zu Brust,
/// Bauch und Becken gehoeren, steht in <c>Testdaten/figur_rumpf.json</c> (erzeugt von <c>werkzeuge/grafik/rumpf_figur.py</c>;
/// die Dreiecke liegen in jedem Bild der Figur in derselben Reihenfolge). Gegenprobe: das alte Kriechen (Stand bis A 1.6.0,
/// auf Knien und Haenden) liegt mit dem Rumpf weit ueber dem Boden.
/// </summary>
public sealed class KriechenBodenTests
{
    /// <summary>Hoechster Abstand der tiefsten Rumpf-Stelle zum Boden, in Figurhoehen (Weisung P12-367).</summary>
    private const float RumpfHoechstens = 0.02f;

    /// <summary>So weit darf eine Ecke unter dem Boden liegen (Rechengenauigkeit), in Figurhoehen.</summary>
    private const float UnterBoden = 0.001f;

    private static readonly string[] Teile = { "brust", "bauch", "becken" };

    private static Dictionary<string, int[]> Rumpf()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Pfade.Wurzel, "tests", "Lernplattform.Tests", "Testdaten", "figur_rumpf.json")));
        return Teile.ToDictionary(t => t, t => doc.RootElement.GetProperty(t).EnumerateArray().Select(e => e.GetInt32()).ToArray());
    }

    /// <summary>Je Teil die tiefste Stelle ueber dem Boden und die tiefste Ecke der ganzen Figur, beides in Figurhoehen.</summary>
    internal static (Dictionary<string, float> Teil, float Tiefste) Messen(GrafikModell modell, GrafikModell steht, Dictionary<string, int[]> rumpf)
    {
        var boden = steht.Min.Y;
        var hoehe = steht.Max.Y - steht.Min.Y;
        float Tief(int d) => MathF.Min(modell.Ecken[(d * 9) + 1], MathF.Min(modell.Ecken[(d * 9) + 4], modell.Ecken[(d * 9) + 7]));
        var teil = rumpf.ToDictionary(r => r.Key, r => (r.Value.Min(Tief) - boden) / hoehe);
        return (teil, (modell.Min.Y - boden) / hoehe);
    }

    [Fact]
    public void Kriechen_RumpfAufDemBoden_NichtsDarunter_GegenprobeAltesKriechen()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var steht = g.Modell(Figur.Modell)!;
        var rumpf = Rumpf();
        Assert.True(Teile.All(t => rumpf[t].Length > 10), "Rumpf-Liste leer");
        Assert.True(rumpf.Values.SelectMany(v => v).All(d => d >= 0 && d < steht.Dreiecke), "Rumpf-Liste passt nicht zur Figur");
        var bilder = Enumerable.Range(0, Figur.RobbenBilder).Select(b => Figur.BildName(Figur.Robben, b)).Append(Figur.BildName(Figur.RobbenStill, 0)).ToList();
        var funde = new List<string>();
        foreach (var name in bilder)
        {
            var m = g.Modell(name);
            Assert.True(m is not null, name + " fehlt");
            Assert.Equal(steht.Dreiecke, m!.Dreiecke);
            var (teil, tiefste) = Messen(m, steht, rumpf);
            foreach (var (t, wert) in teil.Where(x => x.Value > RumpfHoechstens))
            {
                funde.Add($"{name} {t}: {wert:0.000} ueber dem Boden (Grenze {RumpfHoechstens})");
            }
            if (tiefste < -UnterBoden)
            {
                funde.Add($"{name}: Ecke {-tiefste:0.000} unter dem Boden");
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        // die Tabelle waehlt beim Kriechen genau diese Bilder
        Assert.Equal(Figur.RobbenStill, Figur.HaltungBild(Koerperhaltung.Kriechen, false, false));
        Assert.Equal(Figur.Robben, Figur.HaltungBild(Koerperhaltung.Kriechen, false, true));
        // Gegenprobe: das alte Kriechen (bis A 1.6.0) liegt mit jedem Teil des Rumpfs ueber der Grenze
        Figur.KriechenAltFuerTests = true;
        try
        {
            var alt = g.Modell(Figur.BildName(Figur.HaltungBild(Koerperhaltung.Kriechen, false, true)!, 0))!;
            var (teil, _) = Messen(alt, steht, rumpf);
            Assert.True(teil.Values.All(w => w > 0.1f), "Gegenprobe greift nicht: " + string.Join(", ", teil.Select(x => $"{x.Key} {x.Value:0.000}")));
        }
        finally
        {
            Figur.KriechenAltFuerTests = false;
        }
        // Gegenprobe: eine unter den Boden geschobene Figur faellt auf
        var tiefer = new GrafikModell("tiefer", g.Modell(Figur.BildName(Figur.RobbenStill, 0))!.Ecken.Select((v, i) => i % 3 == 1 ? v - 0.05f : v).ToArray(), steht.Farben);
        Assert.True(Messen(tiefer, steht, rumpf).Tiefste < -UnterBoden, "Gegenprobe unter dem Boden greift nicht");
    }
    /// <summary>
    /// Weisung P12-367: Schuhsohlen von oben nicht hautfarben. Die Unterseite der Fuesse (in der stehenden Figur nach unten, bis
    /// <see cref="Figur.SohleBis"/>) ist beim Robben oben zu sehen; jede Ecke dort ist grau wie eine Sohle (Rot, Gruen und Blau fast
    /// gleich, dunkel), bei jeder Hautfarbe. Gegenprobe <see cref="Figur.SohleAltFuerTests"/> (Stand bis A 1.6.0): Hautfarbe.
    /// </summary>
    [Fact]
    public void Robben_SohlenVonObenNichtHautfarben_Gegenprobe()
    {
        var g = SpielGrafikTests.Bibliothek.Value;
        var steht = g.Modell(Figur.Modell)!;
        var hoehe = steht.Max.Y - steht.Min.Y;
        var sohlen = Enumerable.Range(0, steht.Dreiecke)
            .Where(d => ((((steht.Ecken[(d * 9) + 1] + steht.Ecken[(d * 9) + 4] + steht.Ecken[(d * 9) + 7]) / 3) - steht.Min.Y) / hoehe) < Figur.SohleBis && Figur.SohleUnten(steht, d))
            .ToList();
        Assert.True(sohlen.Count >= 20, "Sohlen nicht gefunden: " + sohlen.Count);
        (int Grau, int Haut) Zaehlen(string haut)
        {
            var netz = Figur.Netz(g, new Lernplattform.Core.Daten.PyObjekt { { "haut", haut } }, 1f, null, Figur.RobbenStill, 0);
            int grau = 0, hautfarben = 0;
            foreach (var d in sohlen)
            {
                for (var k = 0; k < 3; k++)
                {
                    var f = netz.FarbeAn((int)netz.Indizes[(d * 3) + k]);
                    if (MathF.Abs(f.X - f.Y) < 0.05f && MathF.Abs(f.Y - f.Z) < 0.05f && f.X < 0.5f)
                    {
                        grau++;
                    }
                    if (f.X - f.Z > 0.08f)
                    {
                        hautfarben++;
                    }
                }
            }
            return (grau, hautfarben);
        }
        foreach (var haut in Figur.Haut.Keys)
        {
            Assert.Equal((sohlen.Count * 3, 0), Zaehlen(haut));
        }
        Figur.SohleAltFuerTests = true;
        try
        {
            Assert.True(Zaehlen("hell").Haut > sohlen.Count, "Gegenprobe greift nicht");
        }
        finally
        {
            Figur.SohleAltFuerTests = false;
        }
    }
}
