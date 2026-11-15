using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268, Nicos Antworten P12-257 Abschnitt 2 auf den Kurzplan P12-150): feste naehere Kamera
/// draussen im Spiel. Abstand fest 8 mit kleinem Rest-Zoom 7 bis 9 (Mausrad, Tasten +/−, zwei Finger), ohne Verschieben;
/// rechte Maustaste bzw. zwei Finger drehen und neigen, Neigung 35 bis 70 Grad. Innenraeume und das Stadtbau-Werkzeug
/// behalten ihre Kamera (<see cref="Kamera"/> ohne Bereich). Die Regeln stehen nur hier; die Spielflaeche ruft sie auf.
/// </summary>
public static class SpielKamera
{
    /// <summary>Fester Abstand draussen (Nico Antwort 1).</summary>
    public const float Abstand = 8f;

    /// <summary>Rest-Zoom (Nico Antwort 3): naechster Abstand.</summary>
    public const float AbstandMin = 7f;

    /// <summary>Rest-Zoom (Nico Antwort 3): weitester Abstand.</summary>
    public const float AbstandMax = 9f;

    /// <summary>Neigung beim Start wie bis A 0.79.0.</summary>
    public const float NeigungStart = 52f;

    /// <summary>Flachster Blick (Nico Antwort 2).</summary>
    public const float NeigungMin = 35f;

    /// <summary>Steilster Blick (Nico Antwort 2).</summary>
    public const float NeigungMax = 70f;

    /// <summary>Neigen mit der rechten Maustaste: Grad je Bildpunkt senkrecht (Maus nach oben = steiler).</summary>
    public const float NeigenJePunkt = 0.25f;

    /// <summary>Neigen mit zwei Fingern: Grad je Bildpunkt, um den beide Finger gemeinsam hoch oder runter wandern.</summary>
    public const float NeigenJeFingerPunkt = 0.25f;

    /// <summary>Neigen mit einer Taste (R steiler, F flacher): Grad je Druck.</summary>
    public const float NeigenJeTaste = 5f;

    /// <summary>Neue Kamera draussen auf (x, z) mit Abstand 8, Neigung 52 und den Bereichen dieser Welle.</summary>
    public static Kamera Draussen(float x, float z, float breite, float tiefe) =>
        new(x, z, Abstand)
        {
            Grenzen = (0, 0, breite, tiefe),
            Bereich = (AbstandMin, AbstandMax),
            NeigungBereich = (NeigungMin, NeigungMax),
            Neigung = NeigungStart,
        };
}
