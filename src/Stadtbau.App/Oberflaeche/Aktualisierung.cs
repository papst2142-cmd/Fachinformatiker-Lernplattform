using Lernplattform.Views;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 2: Update des Werkzeugs am PC. Das Startprogramm setzt <see cref="Pruefen"/> (Velopack, nur Releases
/// der Linie vS); nach dem Laden fragt das Werkzeug einmal nach. Projekte sind nach jeder Aenderung gespeichert, das
/// Update braucht deshalb keine Kopie wie in der Lernplattform.
/// </summary>
public static class Aktualisierung
{
    /// <summary>Sucht eine neue Version; Rueckgabe: Schritt "laden und neu starten" oder null.</summary>
    public static Func<Task<Func<Task>?>>? Pruefen { get; set; }

    internal static async Task NachLadenAsync(Action<string> fehler)
    {
        if (Pruefen is not { } pruefen)
        {
            return;
        }
        try
        {
            if (await Task.Run(pruefen) is { } einspielen
                && await Dialog.Zeigen("Neue Version", "Es gibt eine neue Version des Stadtbau-Werkzeugs. Deine Projekte bleiben erhalten.",
                    "Jetzt aktualisieren", "Später") == 0)
            {
                using (Dialog.Warten("Neue Version wird geladen …"))
                {
                    await einspielen();
                }
            }
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException)
        {
            fehler("Update: " + e.GetType().Name + ": " + e.Message);
        }
    }
}
