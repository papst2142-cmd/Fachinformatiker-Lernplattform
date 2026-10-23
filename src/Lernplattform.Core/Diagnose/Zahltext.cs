using System.Globalization;
using System.Numerics;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Phase 5 W2: Zahl wie Pythons "%.<stellen>f" (exakter Binaerwert, bei genau halber Stelle auf die
/// gerade Ziffer gerundet) und mit Komma statt Punkt (Problembericht _size_text, Leistungsmessung
/// _number, Haenger-Diagnose). .NET rundet einen genau halben Wert weg von 0 ("1,25" -> "1,3"),
/// Python auf gerade ("1,2").
/// </summary>
public static class Zahltext
{
    /// <summary>("%.<stellen>f" % wert).replace(".", ",").</summary>
    public static string Komma(double wert, int stellen) => Punkt(wert, stellen).Replace('.', ',');

    /// <summary>"%.<stellen>f" % wert (mit Punkt).</summary>
    public static string Punkt(double wert, int stellen)
    {
        if (double.IsNaN(wert) || double.IsInfinity(wert))
        {
            return wert.ToString(CultureInfo.InvariantCulture).ToLowerInvariant().Replace("∞", "inf", StringComparison.Ordinal);
        }
        var minus = double.IsNegative(wert);
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(wert));
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantisse = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0)
        {
            exponent = 1;
        }
        else
        {
            mantisse |= 1L << 52;
        }
        exponent -= 1075;   // wert = mantisse * 2^exponent
        var zaehler = new BigInteger(mantisse) * BigInteger.Pow(10, stellen);
        BigInteger ganz;
        if (exponent >= 0)
        {
            ganz = zaehler << exponent;
        }
        else
        {
            var nenner = BigInteger.One << -exponent;
            ganz = BigInteger.DivRem(zaehler, nenner, out var rest);
            var vergleich = (rest * 2).CompareTo(nenner);
            if (vergleich > 0 || (vergleich == 0 && !ganz.IsEven))
            {
                ganz += 1;
            }
        }
        var ziffern = ganz.ToString(CultureInfo.InvariantCulture).PadLeft(stellen + 1, '0');
        var text = stellen == 0 ? ziffern : ziffern[..^stellen] + "." + ziffern[^stellen..];
        return (minus ? "-" : "") + text;
    }
}
