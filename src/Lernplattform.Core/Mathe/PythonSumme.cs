namespace Lernplattform.Core.Mathe;

/// <summary>
/// Phase 2, L7: Pythons sum() fuer Gleitkommazahlen. Ab Python 3.12 rechnet sum() mit
/// Ausgleich der Rundungsfehler (Neumaier); eine einfache Schleife weicht im letzten Bit ab
/// (gefunden mit G18, Rahmenplan-Abdeckung).
/// </summary>
public static class PythonSumme
{
    public static double Summe(IEnumerable<double> werte)
    {
        double summe = 0.0, ausgleich = 0.0;
        foreach (var x in werte)
        {
            var t = summe + x;
            ausgleich += Math.Abs(summe) >= Math.Abs(x) ? (summe - t) + x : (x - t) + summe;
            summe = t;
        }
        if (ausgleich != 0 && double.IsFinite(ausgleich))
        {
            summe += ausgleich;
        }
        return summe;
    }
}
