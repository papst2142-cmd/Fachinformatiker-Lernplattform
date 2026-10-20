using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Tests;

/// <summary>
/// Spike B: Zufall exakt wie Python. Prueft gegen Vergleichswerte G2
/// (tests/Vergleich/zufall.json, erzeugt aus der Python-Linie).
/// Erfolg = 100 % gleiche Werte.
/// </summary>
public class ZufallTests
{
    private static readonly string[] Namen =
        ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliett"];

    private static JsonElement Lade()
    {
        using var datei = File.OpenRead(Pfade.Vergleich("zufall.json"));
        return JsonDocument.Parse(datei).RootElement.Clone();
    }

    public static TheoryData<string, int> Faelle()
    {
        var daten = Lade();
        var faelle = new TheoryData<string, int>();
        for (int i = 0; i < daten.GetProperty("text").GetArrayLength(); i++)
        {
            faelle.Add("text", i);
        }
        for (int i = 0; i < daten.GetProperty("zahl").GetArrayLength(); i++)
        {
            faelle.Add("zahl", i);
        }
        return faelle;
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void FolgeGleichWiePython(string art, int index)
    {
        var fall = Lade().GetProperty(art)[index];
        var startwert = fall.GetProperty("startwert").GetString()!;
        var rng = art == "text"
            ? new PythonZufall(startwert)
            : new PythonZufall(BigInteger.Parse(startwert, CultureInfo.InvariantCulture));

        foreach (var schritt in fall.GetProperty("schritte").EnumerateArray())
        {
            var name = schritt[0].GetString()!;
            var soll = schritt[1];
            var teile = name.Split(':');
            switch (teile[0])
            {
                case "random":
                    foreach (var w in soll.EnumerateArray())
                    {
                        Assert.Equal(double.Parse(w.GetString()!, CultureInfo.InvariantCulture), rng.Random());
                    }
                    break;
                case "getrandbits":
                    foreach (var w in soll.EnumerateArray())
                    {
                        Assert.Equal(BigInteger.Parse(w.GetString()!, CultureInfo.InvariantCulture),
                            rng.GetRandBits(int.Parse(teile[1], CultureInfo.InvariantCulture)));
                    }
                    break;
                case "randint":
                    foreach (var w in soll.EnumerateArray())
                    {
                        Assert.Equal(w.GetInt64(), rng.RandInt(long.Parse(teile[1], CultureInfo.InvariantCulture),
                            long.Parse(teile[2], CultureInfo.InvariantCulture)));
                    }
                    break;
                case "randrange":
                    foreach (var w in soll.EnumerateArray())
                    {
                        var ist = teile.Length == 2
                            ? rng.RandRange(long.Parse(teile[1], CultureInfo.InvariantCulture))
                            : rng.RandRange(long.Parse(teile[1], CultureInfo.InvariantCulture),
                                long.Parse(teile[2], CultureInfo.InvariantCulture),
                                long.Parse(teile[3], CultureInfo.InvariantCulture));
                        Assert.Equal(w.GetInt64(), ist);
                    }
                    break;
                case "choice":
                    foreach (var w in soll.EnumerateArray())
                    {
                        Assert.Equal(w.GetString(), rng.Choice(Namen));
                    }
                    break;
                case "shuffle":
                    var liste = Enumerable.Range(0, int.Parse(teile[1], CultureInfo.InvariantCulture)).ToList();
                    rng.Shuffle(liste);
                    Assert.Equal(soll.EnumerateArray().Select(e => e.GetInt32()), liste);
                    break;
                case "sample":
                    var menge = Enumerable.Range(0, int.Parse(teile[1], CultureInfo.InvariantCulture)).ToList();
                    var stichprobe = rng.Sample(menge, int.Parse(teile[2], CultureInfo.InvariantCulture));
                    Assert.Equal(soll.EnumerateArray().Select(e => e.GetInt32()), stichprobe);
                    break;
                default:
                    Assert.Fail("Unbekannter Schritt in zufall.json: " + name);
                    break;
            }
        }
    }

    [Fact]
    public void WuerfelGleichWiePython()
    {
        var werte = Lade().GetProperty("wuerfel");
        Assert.Equal(1000, werte.GetArrayLength());
        foreach (var w in werte.EnumerateArray())
        {
            var soll = double.Parse(w[3].GetString()!, CultureInfo.InvariantCulture);
            Assert.Equal(soll, Wuerfel.Werfen(w[0].GetString()!, w[1].GetInt64(), w[2].GetString()!));
        }
    }
}
