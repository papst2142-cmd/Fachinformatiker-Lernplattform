using System.Globalization;
using System.Text.Json;
using Lernplattform.Core.Mathe;

namespace Lernplattform.Tests;

/// <summary>
/// Potenz x ** y bitgleich wie Python unter Linux, Vergleichswerte G14
/// (tests/Vergleich/potenz.json, erzeugt von werkzeuge/vergleich/erzeuge_potenz.py).
/// Math.Pow weicht unter Windows und macOS im letzten Bit ab (CI-Lauf 39406464451);
/// PythonPow muss auf allen drei Systemen jedes Bit treffen.
/// </summary>
public class PotenzTests
{
    private static readonly Lazy<JsonElement> Daten = new(() =>
    {
        using var f = File.OpenRead(Pfade.Vergleich("potenz.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    });

    // Python float.hex: [-]0x<0|1>.<hex>p<exp>, dazu inf, -inf, nan
    private static double AusHex(string text)
    {
        switch (text)
        {
            case "inf": return double.PositiveInfinity;
            case "-inf": return double.NegativeInfinity;
            case "nan": return double.NaN;
        }
        bool negativ = text.StartsWith('-');
        var rest = negativ ? text[1..] : text;
        Assert.StartsWith("0x", rest);
        int punkt = rest.IndexOf('.'), p = rest.IndexOf('p');
        ulong fuehrend = ulong.Parse(rest[2..punkt], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var bruch = rest[(punkt + 1)..p].PadRight(13, '0');
        Assert.Equal(13, bruch.Length);
        ulong mantisse = ulong.Parse(bruch, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int exponent = int.Parse(rest[(p + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        // fuehrend 0: Null oder unternormale Zahl (Exponent -1022), sonst normale Zahl
        ulong bits = fuehrend == 0 ? mantisse : ((ulong)(exponent + 1023) << 52) | mantisse;
        if (fuehrend == 0 && mantisse != 0)
        {
            Assert.Equal(-1022, exponent);
        }
        if (negativ)
        {
            bits |= 0x8000000000000000;
        }
        return BitConverter.UInt64BitsToDouble(bits);
    }

    [Fact]
    public void HexLeserStimmt()
    {
        Assert.Equal(1.0, AusHex("0x1.0000000000000p+0"));
        Assert.Equal(-2.5, AusHex("-0x1.4000000000000p+1"));
        Assert.Equal(double.Epsilon, AusHex("0x0.0000000000001p-1022"));
        Assert.Equal(0x8000000000000000UL, BitConverter.DoubleToUInt64Bits(AusHex("-0x0.0p+0")));
        Assert.Equal(1.0 / 3.0, AusHex("0x1.5555555555555p-2"));
        Assert.Equal(0.0, AusHex("0x0.0p+0"));
    }

    [Fact]
    public void PotenzBitgleichWiePython()
    {
        var liste = Daten.Value.GetProperty("potenz");
        Assert.True(liste.GetArrayLength() >= 8000);
        int geprueft = 0;
        foreach (var e in liste.EnumerateArray())
        {
            double x = AusHex(e[0].GetString()!), y = AusHex(e[1].GetString()!);
            var soll = e[2].GetString()!;
            double ist = PythonPow.Pow(x, y);
            switch (soll)
            {
                case "complex":
                    Assert.True(double.IsNaN(ist), $"{e[0]} ** {e[1]}: Python komplex, C# {ist:R}");
                    break;
                case "OverflowError":
                case "ZeroDivisionError":
                    Assert.True(double.IsInfinity(ist), $"{e[0]} ** {e[1]}: Python {soll}, C# {ist:R}");
                    break;
                default:
                    var erwartet = AusHex(soll);
                    Assert.True(BitConverter.DoubleToUInt64Bits(erwartet) == BitConverter.DoubleToUInt64Bits(ist),
                        $"{e[0]} ** {e[1]}: Python {erwartet:R}, C# {ist:R}");
                    break;
            }
            geprueft++;
        }
        Assert.Equal(liste.GetArrayLength(), geprueft);
    }
}
