namespace Lernplattform.Spiel;

/// <summary>
/// Phase 8 W8 Vorarbeit (Weisung P8-16 a): Brücke zur Ton-Probe der Probe-APK. Nur die Probe-APK
/// (Android, -p:Probe=true) setzt <see cref="Geraet"/>; in jedem anderen Bau bleibt es null und die
/// Optionen zeigen nichts davon. Nicht für die echte App: der Tondienst kommt mit dem Bau von W8.
/// </summary>
public static class TonProbe
{
    /// <summary>Ausgabe der Probe-APK; null = keine Ton-Probe in diesem Bau.</summary>
    public static ITonProbe? Geraet { get; set; }
}

/// <summary>Was die Ton-Probe kann: starten (Schleife), anhalten, Lautstärke 0 bis 1 sofort setzen.</summary>
public interface ITonProbe
{
    /// <summary>Startet die Ausgabe (beim ersten Mal Gerät öffnen); Rückgabe ist die Zeile für die Anzeige.</summary>
    string Starten(float lautstaerke);

    void Anhalten();

    void Lautstaerke(float wert);
}
