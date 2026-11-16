namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280, Nico 3b: Fernform plus leichter Dunst am Rand der Sicht). Der Dunst legt die
/// Farbe des Himmels ueber alles, was weit vom Auge entfernt ist: ab <see cref="Von"/> beginnt er, ab <see cref="Bis"/> hat er
/// seine volle Staerke. Er gilt nur im Spiel mit fester Kamera und Nachladen; im Werkzeug, innen und ohne Nachladen gibt es
/// keinen Dunst (Bild wie vorher).
/// </summary>
public static class Dunst
{
    /// <summary>
    /// Staerke des Dunstes (0 = kein Dunst, 1 = ganz Himmelsfarbe). DER EINE WERT, den Nico einstellen laesst
    /// („Dunst passt / staerker / schwaecher“, Zwischenmeldung P12-166).
    /// </summary>
    public const float Staerke = 0.6f;

    /// <summary>
    /// Ab diesem Abstand vom Auge (waagrecht) beginnt der Dunst; naeher bleibt das Bild wie ohne Dunst. Die feste Kamera sieht
    /// den Boden bei Neigung 35 bis rund 19 vom Auge, bei der Startneigung 52 bis rund 11: der Dunst liegt so nur am oberen Rand.
    /// </summary>
    public const float Von = 12f;

    /// <summary>Ab diesem Abstand vom Auge ist der Dunst voll (Rand der nah geladenen Kacheln, <see cref="Stadtbau.Ladeplan.NahWeg"/>).</summary>
    public const float Bis = Stadtbau.Ladeplan.NahWeg;

    /// <summary>Anteil des Dunstes bei <paramref name="abstand"/> (gleiche Rechnung wie im Pixel-Shader: smoothstep).</summary>
    public static float Anteil(float abstand)
    {
        var t = Math.Clamp((abstand - Von) / (Bis - Von), 0f, 1f);
        return Staerke * t * t * (3f - (2f * t));
    }
}
