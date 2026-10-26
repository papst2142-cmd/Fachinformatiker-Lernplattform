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

    private static long _wechsel;

    /// <summary>Wartet, bis kein anderer Vorgang laeuft; Freigabe mit Dispose.</summary>
    public static IDisposable Sperren()
    {
        Sperre.Wait();
        Interlocked.Increment(ref _wechsel);
        return new Freigabe();
    }

    /// <summary>
    /// Weisung P8-6 (Sammelliste 108): zaehlt jedes <see cref="Sperren"/> (Abgleich, Uebernahme, Sicherung
    /// einspielen, Loeschen, Update). Nur diese Vorgaenge aendern sync_meta oder tauschen die Datenbank; wer
    /// einen Wert daraus unter der Sperre aus <see cref="Versuchen"/> gelesen hat, darf ihn behalten, solange
    /// diese Zahl gleich bleibt (das Spiel liest die Uhrabweichung so nur einmal je Sitzung).
    /// </summary>
    public static long Wechsel => Interlocked.Read(ref _wechsel);

    /// <summary>
    /// Phase 7, W8 (R3): wie <see cref="Sperren"/>, aber ohne zu warten. null: gerade laeuft ein
    /// anderer Vorgang (Abgleich, Uebernahme, Update, Sicherung einspielen); dann schreibt das Spiel
    /// nichts und die Oberflaeche lehnt mit AbgleichTexte.Belegt ab.
    /// </summary>
    public static IDisposable? Versuchen() => Sperre.Wait(0) ? new Freigabe() : null;

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
