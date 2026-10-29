using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Lernplattform.Programm;

/// <summary>
/// Weisung P8-56 (Nico, Wahl b): Programm beenden ueber den Knopf der Seitenleiste (PC). Schliesst das Hauptfenster
/// wie das Kreuz in der Titelleiste; alles Weitere (Spielschritte schreiben, Messdatei, Abgleich vor dem Ende,
/// Sperren freigeben, Velopack) laeuft im Programmende von App (desktop.Exit), wie beim Schliessen des Fensters.
/// </summary>
public static class Programmende
{
    public static void Fenster()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Close();
        }
    }
}
