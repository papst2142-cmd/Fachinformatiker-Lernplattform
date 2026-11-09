using Lernplattform.Core.Spiel.Ton;

namespace Stadtbau.Core;

/// <summary>
/// S 0.25.1 (Weisung P12-109, Sammelliste 252, Wunsch Nico): am PC den Bildschirm fuer das Vollbild waehlen wie in der
/// Lernplattform (Sammelliste 205). Gemerkt in einstellungen.json des Werkzeugordners (Schluessel „vollbild_bildschirm“,
/// Lage und Groesse, sonst der Name; Regeln aus <see cref="VollbildBildschirm"/> der Lernplattform, unveraendert genutzt).
/// </summary>
public static class VollbildZielWahl
{
    /// <summary>Die Wahl steht nur am PC und nur ab zwei Bildschirmen im Menue.</summary>
    public static bool Zeigen(bool pc, int bildschirme) => pc && bildschirme >= 2;

    /// <summary>
    /// Stelle des Bildschirms, auf den das Fenster vor dem Vollbild muss, sonst null (keine Wahl, Bildschirm fehlt, oder das
    /// Fenster steht schon dort; beim Start wird immer gesetzt, weil das Fenster noch keine Lage hat).
    /// </summary>
    public static int? Ziel(IReadOnlyList<Bildschirm> alle, Bildschirm? gemerkt, int? jetzt, bool beimStart)
    {
        if (VollbildBildschirm.Finden(alle, gemerkt) is not { } stelle)
        {
            return null;
        }
        return !beimStart && jetzt == stelle ? null : stelle;
    }
}
