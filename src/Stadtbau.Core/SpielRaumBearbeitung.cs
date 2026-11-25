using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.64.0 (Weisung P12-381, Nico 2c, Bild 07 P12-245): was „Raum ändern“ an einem Raum des Spiels zusaetzlich zu
/// Moebeln, Raeumen (<see cref="Bearbeitung.Raum"/>) und Tueren (<see cref="Bearbeitung.Tuer"/>) aendert: die Lage und Breite
/// des Flurs bzw. der Diele („Wand ziehen“) und die Groesse des ganzen Hauses („Raumgröße“). Alles auf dem Raster
/// <see cref="Bearbeitung.Raster"/>; ob das Ergebnis im Spiel geht, sagt die Fundliste (<see cref="SpielRaumRegeln"/>).
/// </summary>
public static class SpielRaumBearbeitung
{
    /// <summary>Flur bzw. Diele: obere Wand bei <paramref name="y"/>, Breite <paramref name="h"/> (mindestens ein Raster).</summary>
    public static void Flur(PyObjekt gebaeude, double y, double h)
    {
        var flur = (PyObjekt)gebaeude["flur"]!;
        Py.Setzen(flur, "y", Bearbeitung.Zahl(Math.Max(0, Bearbeitung.AufRaster(y, Bearbeitung.Raster))));
        Py.Setzen(flur, "h", Bearbeitung.Zahl(Math.Max(Bearbeitung.Raster, Bearbeitung.AufRaster(h, Bearbeitung.Raster))));
    }

    /// <summary>Groesse des ganzen Hauses (Breite, Tiefe; mindestens 2 Einheiten).</summary>
    public static void Haus(PyObjekt gebaeude, double breite, double tiefe)
    {
        Py.Setzen(gebaeude, "breite", Bearbeitung.Zahl(Math.Max(2, Bearbeitung.AufRaster(breite, Bearbeitung.Raster))));
        Py.Setzen(gebaeude, "hoehe", Bearbeitung.Zahl(Math.Max(2, Bearbeitung.AufRaster(tiefe, Bearbeitung.Raster))));
    }
}
