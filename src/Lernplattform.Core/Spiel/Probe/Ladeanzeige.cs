namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Weisung P8-55 (W12, Teilplan Abschnitt 3.1): Ladeanzeige "Stadt wird geladen …" beim Oeffnen der Spielseite,
/// solange vorgeladen wird. Sie erscheint erst nach <see cref="ZeigenAbSekunden"/> (kein kuenstliches Warten, ein
/// schnelles Vorladen blitzt nicht auf) und verschwindet, sobald alles in der Grafikkarte ist.
/// </summary>
public static class Ladeanzeige
{
    /// <summary>Erst ab dieser Ladedauer sichtbar (Nico 1a, Weisung P8-52).</summary>
    public const double ZeigenAbSekunden = 0.3;

    /// <summary>Ist die Anzeige zu sehen? <paramref name="sekunden"/> = Zeit seit Beginn des Vorladens.</summary>
    public static bool Zeigen(bool laedt, double sekunden) => laedt && sekunden >= ZeigenAbSekunden;

    /// <summary>Anteil fuer den Fortschrittsring (0 bis 1): erledigte Schritte des Hochladens von allen.</summary>
    public static double Anteil(int erledigt, int gesamt) => gesamt <= 0 ? 0 : Math.Clamp((double)erledigt / gesamt, 0, 1);
}
