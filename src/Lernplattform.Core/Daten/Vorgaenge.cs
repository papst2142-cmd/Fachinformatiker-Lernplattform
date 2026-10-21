namespace Lernplattform.Core.Daten;

/// <summary>
/// Phase 2, W4 (Weisung P2-11 A1): Uebernahme, Abgleich und Ruecksicherung schliessen sich
/// gegenseitig aus. Laeuft einer dieser Vorgaenge, wartet der naechste, bis er fertig ist.
/// Gewartet wird nie auf dem Oberflaechen-Faden (alle Aufrufer laufen in Task.Run bzw. im
/// Hintergrund). Uebernahme und Sicherung fuer die alte App fragen vorher <see cref="Belegt"/>
/// und lehnen dann mit AbgleichTexte.Belegt ab (Weisung P2-12 A2); der automatische Abgleich
/// wartet im Hintergrund. Gilt im ganzen Programm (ein Prozess, eine Sperre).
/// </summary>
public static class Vorgaenge
{
    private static readonly SemaphoreSlim Sperre = new(1, 1);

    /// <summary>Wartet, bis kein anderer Vorgang laeuft; Freigabe mit Dispose.</summary>
    public static IDisposable Sperren()
    {
        Sperre.Wait();
        return new Freigabe();
    }

    /// <summary>Laeuft gerade ein Vorgang (Abgleich, Uebernahme, Sicherung fuer die alte App)?</summary>
    public static bool Belegt => Sperre.CurrentCount == 0;

    private sealed class Freigabe : IDisposable
    {
        private int _frei;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _frei, 1) == 0)
            {
                Sperre.Release();
            }
        }
    }
}
