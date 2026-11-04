using Avalonia;
using Avalonia.Controls;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel.Ton;

namespace Lernplattform.Spiel;

/// <summary>
/// Patch A 0.37.0 (Sammelliste 205, Wunsch Nico): setzt das Fenster vor dem Vollbild auf den in den Optionen gewaehlten
/// Bildschirm. Ohne Wahl, oder wenn der Bildschirm fehlt, bleibt es beim Bildschirm des Fensters (wie bisher). Am PC
/// unter Windows, Linux (X11/XWayland) und am Mac, soweit Avalonia die Lage eines Fensters setzen kann.
/// </summary>
internal static class VollbildZiel
{
    public static IReadOnlyList<Bildschirm> Liste(Window fenster) =>
        fenster.Screens.All.Select(s => new Bildschirm(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height, s.IsPrimary,
            s.DisplayName ?? "")).ToList();

    public static void Setzen(Window fenster, bool beimStart)
    {
        try
        {
            var alle = fenster.Screens.All;
            if (VollbildBildschirm.Finden(Liste(fenster), VollbildBildschirm.Lesen(Programm.Zustand.Werte)) is not { } stelle
                || stelle >= alle.Count)
            {
                return;
            }
            var ziel = alle[stelle];
            if (!beimStart && fenster.Screens.ScreenFromWindow(fenster) is { } jetzt && jetzt.Bounds == ziel.Bounds)
            {
                return;   // schon dort
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
            var bereich = ziel.WorkingArea;
            fenster.Position = new PixelPoint(bereich.X + Math.Max(0, bereich.Width / 10), bereich.Y + Math.Max(0, bereich.Height / 10));
        }
        catch (Exception e)
        {
            Fehlerprotokoll.Schreiben(e);
        }
    }
}
