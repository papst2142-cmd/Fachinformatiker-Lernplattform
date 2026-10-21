using System.Globalization;

namespace Lernplattform.Core.Mathe;

/// <summary>
/// Phase 3, R1/R3: round(x, n) genau wie Python 3.12: der exakte Wert von x wird halb zur
/// geraden Zahl auf n Nachkommastellen gerundet und dieser Dezimalwert dann korrekt gerundet
/// als Gleitkommazahl gelesen. Nutzt PythonRunden.Gerundet und aendert PythonRunden nicht.
/// </summary>
public static class PythonRundenStellen
{
    /// <summary>round(x, stellen) fuer stellen &gt;= 0; nan und unendlich bleiben wie sie sind.</summary>
    public static double Runde(double x, int stellen)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stellen);
        if (double.IsNaN(x) || double.IsInfinity(x))
        {
            return x;
        }
        var ganz = PythonRunden.Gerundet(x, stellen);
        var text = ganz.ToString(CultureInfo.InvariantCulture) + "E-" + stellen.ToString(CultureInfo.InvariantCulture);
        var wert = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        return wert == 0 ? (x < 0 ? -0.0 : 0.0) : wert;
    }
}
