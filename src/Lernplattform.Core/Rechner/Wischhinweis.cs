namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3, W3 (R6-Nachtrag, Weisung P3-12): Regel des Hinweises "→ wischen" unter einem
/// Ergebniskasten wie FitBox._refit (mobile/src/ui.py v0.63.2): sichtbar, wenn nach
/// fit_lines (<see cref="Zeilen.Kasten"/>) noch eine Zeile laenger als der Kasten ist.
/// PC = Handy (in Python nur am Handy). Den Verlauf rechts und das zweite, schmalere Setzen
/// (A5-d) baut C# nicht; der Kasten laesst sich seitlich schieben. Vergleichswerte G27
/// "wischen".
/// </summary>
public static class Wischhinweis
{
    /// <summary>Ist eine Zeile des gesetzten Textes laenger als <paramref name="zeichen"/>?</summary>
    public static bool Noetig(string gesetzt, int zeichen) =>
        zeichen > 0 && gesetzt.Split('\n').Any(zeile => zeile.Length > zeichen);
}
