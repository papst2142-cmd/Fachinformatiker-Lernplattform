namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 3b (Weisung P12-24, Fund Chat: "TALHEIM" ragte bei Tablet 1280x800 ueber den rechten Kartenrand):
/// Lage einer Beschriftung in der Draufsicht. Eine Beschriftung beginnt an ihrem Punkt; ragt sie ueber den Rand der Karte,
/// rueckt sie nach innen. Ist sie breiter als die Karte, wird sie auf die Breite der Karte gekuerzt (mit "…").
/// Alle Werte in Bildpunkten.
/// </summary>
public static class Beschriftung
{
    /// <summary>Nur fuer die Gegenprobe in Tests und im Start-Test: Beschriftungen ohne Rand-Regel (wie vor S 0.3.0).</summary>
    public static bool OhneRandFuerTests { get; set; }

    /// <summary>
    /// Lage (links, oben) und hoechste Breite einer Beschriftung der Groesse <paramref name="breite"/> x <paramref name="hoehe"/>
    /// am Punkt (<paramref name="x"/>, <paramref name="y"/>) in der Karte (<paramref name="links"/> bis <paramref name="rechts"/>,
    /// <paramref name="oben"/> bis <paramref name="unten"/>).
    /// </summary>
    public static (double X, double Y, double Breite) InKarte(double x, double y, double breite, double hoehe,
        double links, double oben, double rechts, double unten)
    {
        if (OhneRandFuerTests)
        {
            return (x, y, breite);
        }
        var platz = Math.Max(0, rechts - links);
        var b = Math.Min(breite, platz);
        var nx = Math.Max(links, Math.Min(x, rechts - b));
        var ny = Math.Max(oben, Math.Min(y, unten - hoehe));
        return (nx, ny, b);
    }

    /// <summary>Liegt das Rechteck ganz in der Karte (Grenze 0,5 Bildpunkte fuer die Rundung)?</summary>
    public static bool Innen(double x, double y, double breite, double hoehe, double links, double oben, double rechts, double unten) =>
        x >= links - 0.5 && y >= oben - 0.5 && x + breite <= rechts + 0.5 && y + hoehe <= unten + 0.5;
}
