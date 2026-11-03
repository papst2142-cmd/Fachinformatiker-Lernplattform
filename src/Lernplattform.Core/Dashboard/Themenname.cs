namespace Lernplattform.Core.Dashboard;

/// <summary>
/// Phase 11 FW10 (Weisung P11-19, Nico 03.11.2026, Antwort 2a, Sammelliste 195): Themenname in der Themenreihe
/// des Dashboards (PC, je Thema eine Spalte). Passt das laengste Wort eines Namens nicht in seine Spalte, wird
/// nur dieser Name verkleinert, bis das Wort passt, hoechstens auf <see cref="Untergrenze"/> (85 %) der
/// eingestellten Schrift. Passt es, bleibt die Schrift unveraendert (alle anderen Namen wie bisher).
/// Anlass: auf dem Intel-Mac misst die Schrift minimal breiter, "Netzwerkdesign" wurde bei "Gross" mitten getrennt.
/// </summary>
public static class Themenname
{
    /// <summary>Kleinste Schrift als Anteil der eingestellten.</summary>
    public const double Untergrenze = 0.85;

    /// <summary>
    /// Schriftgroesse fuer einen Namen: <paramref name="soll"/> = eingestellte Groesse,
    /// <paramref name="laengstesWort"/> = Breite des laengsten Worts bei <paramref name="soll"/>,
    /// <paramref name="verfuegbar"/> = Breite der Spalte. Auf eine Zehntelstufe abgerundet und 0,1 kleiner
    /// (Rundung der Schriftmessung), nie unter <see cref="Untergrenze"/> × soll.
    /// </summary>
    public static double Groesse(double soll, double laengstesWort, double verfuegbar)
    {
        if (soll <= 0 || double.IsNaN(verfuegbar) || double.IsInfinity(verfuegbar) || verfuegbar <= 0 || laengstesWort <= verfuegbar)
        {
            return soll;
        }
        var passend = Math.Floor(soll * verfuegbar / laengstesWort * 10) / 10 - 0.1;
        return Math.Max(soll * Untergrenze, passend);
    }
}
