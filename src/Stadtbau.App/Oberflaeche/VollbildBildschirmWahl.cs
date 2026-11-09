using Avalonia;
using Avalonia.Controls;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Spiel;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.25.1 (Weisung P12-109, Sammelliste 252): setzt das Fenster des Werkzeugs vor dem Vollbild auf den gewaehlten
/// Bildschirm, wie <c>VollbildZiel</c> der Lernplattform (deren Liste der Bildschirme wird genutzt, die Lernplattform bleibt
/// unveraendert). Ohne Wahl oder wenn der Bildschirm fehlt: Bildschirm des Fensters (wie bisher).
/// </summary>
internal static class VollbildBildschirmWahl
{
    public static void Setzen(Window fenster, Werkzeugordner ordner, bool beimStart)
    {
        try
        {
            var alle = fenster.Screens.All;
            var jetzt = fenster.Screens.ScreenFromWindow(fenster) is { } j ? alle.ToList().FindIndex(s => s.Bounds == j.Bounds) : -1;
            if (VollbildZielWahl.Ziel(VollbildZiel.Liste(fenster), WerkzeugEinstellungen.VollbildBildschirm(ordner), jetzt >= 0 ? jetzt : null, beimStart) is not { } stelle
                || stelle >= alle.Count)
            {
                return;
            }
            // aus dem Vollbild heraus erst zurueck ins Fenster, sonst bleibt das System beim alten Bildschirm
            if (fenster.WindowState == WindowState.FullScreen)
            {
                fenster.WindowState = WindowState.Normal;
            }
            if (beimStart)
            {
                fenster.WindowStartupLocation = WindowStartupLocation.Manual;
            }
            var bereich = alle[stelle].WorkingArea;
            fenster.Position = new PixelPoint(bereich.X + Math.Max(0, bereich.Width / 10), bereich.Y + Math.Max(0, bereich.Height / 10));
        }
        catch (Exception e)
        {
            ordner.Fehler("Vollbild-Bildschirm: " + e.GetType().Name + ": " + e.Message);
        }
    }
}
