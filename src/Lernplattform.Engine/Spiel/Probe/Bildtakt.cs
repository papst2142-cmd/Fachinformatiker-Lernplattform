namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Patch A 1.20.1 (Sammelliste 473, Weisung P12-449 Punkt 1): Bildbremse und Zeitschritt im Takt des Bildschirms. Ein Bild kommt
/// immer zum naechsten Takt des Bildschirms (60 Hz: alle 16,7 ms), nachdem es angefordert wurde. Bis A 1.20.0 forderte die
/// Bildbremse das naechste Bild genau 1 / Bilder Sekunden nach dem Beginn des letzten an; das Warten endet aber immer etwas
/// spaeter (Wecker, Oberflaechen-Faden), darum verpasste die Anforderung oft knapp den Takt und das Bild kam einen Takt spaeter:
/// bei 30 Bildern je Sekunde abwechselnd nach 33 und 50 ms, bei 60 nach 17 und 33 ms. Jetzt fordert sie einen halben Takt vor
/// dem Ziel an; das Bild kommt dann genau zum Ziel-Takt, nie frueher (also nie mehr Bilder als gewaehlt). Ist der Takt des
/// Bildschirms unbekannt (0, z. B. am PC), bleibt alles wie bisher.
/// </summary>
public static class Bildtakt
{
    /// <summary>
    /// Sekunden, die die Bildbremse noch warten muss, bis sie das naechste Bild anfordert (0 oder weniger = sofort), wenn seit dem
    /// Beginn des letzten Bilds <paramref name="seitBeginn"/> Sekunden vergangen sind; <paramref name="bilder"/> = Bilder je
    /// Sekunde hoechstens (groesser 0), <paramref name="hertz"/> = Takt des Bildschirms (0 = unbekannt).
    /// </summary>
    public static double Warten(double seitBeginn, int bilder, double hertz)
    {
        var ziel = 1.0 / bilder;
        if (hertz > 0)
        {
            var takt = 1.0 / hertz;
            var takte = Math.Max(1, Math.Round(ziel / takt));
            ziel = (takte * takt) - (takt / 2);
        }
        return ziel - seitBeginn;
    }

    /// <summary>
    /// Zeitschritt fuer Gehen, Kamera und Verkehr aus dem gemessenen Abstand zweier Bilder: bei bekanntem Takt auf ganze Takte
    /// gerundet (mindestens einer), denn sichtbar wird jedes Bild erst zum Takt. So geht die Figur je gezeigtem Bild gleich weit,
    /// auch wenn das Zeichnen mal etwas frueher oder spaeter beginnt (bis A 1.20.0 ruckte sie dabei vor und zurueck).
    /// </summary>
    public static double Schritt(double gemessen, double hertz)
    {
        if (hertz <= 0 || gemessen <= 0)
        {
            return gemessen;
        }
        return Math.Max(1, Math.Round(gemessen * hertz)) / hertz;
    }
}
