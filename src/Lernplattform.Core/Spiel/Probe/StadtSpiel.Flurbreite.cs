namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 1.2.0 (Weisung P12-332): der Innenraum hat einen breiteren Flur als der Grundriss des Spiel-Kerns
/// (<see cref="Flurbreite"/>). Die Regeln der Wohnung (Moebel antippen, aufstellen, Raum der Figur, Kleiderschrank) lesen den
/// Grundriss und die Moebelplaetze aus dem Spielstand, also in den alten Zahlen: jede Stelle aus dem Innenraum geht ueber
/// <see cref="KernZ"/> dorthin, jede Stelle von dort ueber <see cref="InnenZ"/> bzw. <see cref="InnenRechteckZ"/> zurueck.
/// </summary>
public sealed partial class StadtSpiel
{
    private Flurbreite.Umrechnung Umrechnung => Innen?.Umrechnung ?? Flurbreite.Umrechnung.Keine;

    /// <summary>z im Innenraum zu y im Grundriss des Spiel-Kerns.</summary>
    private double KernZ(double z) => Umrechnung.Zurueck(z);

    /// <summary>y eines Punkts im Grundriss des Spiel-Kerns zu z im Innenraum.</summary>
    private double InnenZ(double y) => Umrechnung.Hin(y);

    /// <summary>Oberkante eines Rechtecks (Moebel, Vorschau) aus dem Spiel-Kern im Innenraum.</summary>
    public double InnenRechteckZ(double y, double h) => y + Umrechnung.Verschiebung(y, h);

    /// <summary>Oberkante eines Rechtecks im Innenraum (z1 bis z2) im Grundriss des Spiel-Kerns.</summary>
    private double KernRechteckZ(double z1, double z2)
    {
        var mitte = (z1 + z2) / 2;
        return z1 + (KernZ(mitte) - mitte);
    }
}
