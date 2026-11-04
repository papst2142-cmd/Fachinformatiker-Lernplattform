using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Lernplattform.Views;

namespace Lernplattform.Programm;

/// <summary>
/// Weisung P8-56 (Nico, Wahl b): Programm beenden ueber den Knopf der Seitenleiste (PC). Schliesst das Hauptfenster
/// wie das Kreuz in der Titelleiste; alles Weitere (Spielschritte schreiben, Messdatei, Abgleich vor dem Ende,
/// Sperren freigeben, Velopack) laeuft im Programmende von App (desktop.Exit), wie beim Schliessen des Fensters.
/// Patch A 0.37.0 (Sammelliste 206, Weisung P11-34 Antwort 4a, ersetzt P8-56 b "nur am PC"): auch am Handy und
/// Tablet (Seitenleiste und Karte in den Optionen). Dort dieselben Schritte wie am PC (<see cref="Aufraeumen"/>),
/// der Abgleich im Hintergrund (die Oberflaeche zeigt "Programm wird beendet …"), danach beendet die Plattform
/// die Aufgabe und den Prozess (<see cref="Geraet"/>, MainActivity); der naechste Start ist ein Kaltstart.
/// </summary>
public static class Programmende
{
    /// <summary>Android (MainActivity): Aufgabe schliessen und Prozess beenden; am PC null.</summary>
    public static Action? Geraet { get; set; }

    private static int _laeuft;

    public static void Fenster()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Close();
        }
    }

    /// <summary>Knopf "Programm beenden" (PC: Fenster schliessen wie das Kreuz; Handy: Aufraeumen, dann Ende).</summary>
    public static void Beenden()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            Fenster();
            return;
        }
        if (Interlocked.Exchange(ref _laeuft, 1) == 1)
        {
            return;
        }
        _ = HandyAsync();
    }

    private static async Task HandyAsync()
    {
        try
        {
            using (Dialog.Warten(Core.Texte.Navigation.BeendenWarten))
            {
                // wie am PC; nur der Abgleich (hoechstens 12 s) laeuft nicht im Oberflaechen-Faden
                VorDemAbgleich();
                await Task.Run(Abgleich);
                Diagnosedienst.NachDemAbgleich();
            }
        }
        catch (Exception e)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(e);
        }
        Geraet?.Invoke();
    }

    /// <summary>
    /// Schritte am Programmende (PC aus desktop.Exit, Handy aus <see cref="Beenden"/>): zuerst alle Spielschritte
    /// (Weisung P7-17), Diagnose ("endet", Messdatei), Zustand, dann der letzte Abgleich, wenn seither gelernt wurde
    /// (L10, nicht beim Ende fuer ein Update), zuletzt die Zeile nach dem Abgleich.
    /// </summary>
    public static void Aufraeumen()
    {
        VorDemAbgleich();
        Abgleich();
        Diagnosedienst.NachDemAbgleich();
    }

    private static void VorDemAbgleich()
    {
        Core.Spiel.Spielschlange.AlleLeeren();
        Diagnosedienst.Beenden();
        Zustand.Beenden();
    }

    private static void Abgleich()
    {
        if (!UpdateDienst.FuerUpdateBeendet)
        {
            AbgleichDienst.VorDemBeenden();
        }
    }
}
