namespace Lernplattform.Spiel;

/// <summary>
/// Weisung P7-16 (Haenger "ausserhalb"): Werte des Betriebssystems fuer langsame Bilder in spiel_bilder.log.
/// Die Plattform setzt sie (Android: MainActivity mit Speicherbereinigung von Java, Waermezustand,
/// ausgelassenen Bildern und Beruehrungen); am PC bleiben sie leer. Nur messen, nichts aendern.
/// </summary>
public static class Systemwerte
{
    /// <summary>Zeile fuer den Kopf der Datei (z. B. Android-Version, Bildwiederholrate), null = keine.</summary>
    public static Func<string?>? Kopf { get; set; }

    /// <summary>
    /// Werte im Zeitraum von <c>von</c> bis jetzt (Zeitstempel von <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>),
    /// gerufen nur fuer langsame Bilder im Oberflaechen-Thread; null = nichts.
    /// </summary>
    public static Func<long, string?>? Seit { get; set; }

    /// <summary>
    /// Weisung P7-17: Zeitstempel (<see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>) des letzten Fingers
    /// auf oder ab, gesetzt von der Plattform (Android: MainActivity), 0 = keiner.
    /// </summary>
    public static long Finger { get; set; }
}
