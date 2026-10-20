using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Dashboard;

namespace Lernplattform.Core.Mathe;

/// <summary>
/// Phase 2, L7: Rundungen genau wie Python v0.63.2 (Vergleichswerte G18). Nie Math.Round:
/// round(x) rundet den exakten Wert der Gleitkommazahl halb zur geraden Zahl, round(x, n)
/// ebenso auf n Nachkommastellen (12,35 ist binaer etwas weniger und wird 12,3).
/// </summary>
public static class PythonRunden
{
    /// <summary>int(round(x)): ganze Zahl, bei genau ,5 zur geraden.</summary>
    public static long Runde(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }
        var unten = Math.Floor(x);
        var rest = x - unten;          // exakt: Abstand zur naechsten ganzen Zahl darunter
        var ganz = (long)unten;
        if (rest > 0.5 || (rest == 0.5 && (ganz & 1) != 0))
        {
            ganz += 1;
        }
        return ganz;
    }

    /// <summary>round(x, 1).</summary>
    public static double Runde1(double x) => PyText.Runde1(x);

    /// <summary>
    /// fisi_pruefung.punkte_text: Decimal(repr(round(x, 9))) auf eine Stelle abgerundet,
    /// Komma, ",0" entfaellt, -0 wird "0"; inf und nan ergeben "-".
    /// </summary>
    public static string PunkteText(double wert)
    {
        if (double.IsNaN(wert) || double.IsInfinity(wert))
        {
            return "-";
        }
        // round(x, 9) auf dem exakten Wert (halb gerade), dann abrunden auf Zehntel
        var neun = Gerundet(wert, 9);
        var zehntel = BigInteger.DivRem(neun, 100_000_000, out var rest);
        if (rest.Sign < 0)
        {
            zehntel -= 1;              // ROUND_FLOOR
        }
        var negativ = zehntel.Sign < 0;
        var betrag = BigInteger.Abs(zehntel);
        var text = (negativ ? "-" : "") + (betrag / 10).ToString(CultureInfo.InvariantCulture) + ","
            + (betrag % 10).ToString(CultureInfo.InvariantCulture);
        return text.EndsWith(",0", StringComparison.Ordinal) ? text[..^2] : text;
    }

    /// <summary>Exakter Wert von x mal 10^stellen, halb gerade auf eine ganze Zahl gerundet.</summary>
    internal static BigInteger Gerundet(double x, int stellen)
    {
        if (x == 0)
        {
            return BigInteger.Zero;
        }
        var bits = BitConverter.DoubleToInt64Bits(x);
        var negativ = bits < 0;
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
        exponent -= 1075;
        var zaehler = new BigInteger(mantisse) * BigInteger.Pow(10, stellen);
        var nenner = BigInteger.One;
        if (exponent > 0)
        {
            zaehler <<= exponent;
        }
        else
        {
            nenner <<= -exponent;
        }
        var ganz = BigInteger.DivRem(zaehler, nenner, out var rest);
        var doppelt = rest * 2;
        if (doppelt > nenner || (doppelt == nenner && !ganz.IsEven))
        {
            ganz += 1;
        }
        return negativ ? -ganz : ganz;
    }
}
