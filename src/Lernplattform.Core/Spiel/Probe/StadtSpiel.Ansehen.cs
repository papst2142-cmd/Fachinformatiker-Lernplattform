namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// S 0.49.0 / A 0.72.0 (Weisung P12-231 Antwort 1a): einen Innenraum ansehen, ohne hinzugehen und ohne hineinzugehen. Nur das
/// Stadtbau-Werkzeug ruft das (Innenraum in 3D neben dem Grundriss); das Spiel betritt Orte weiter nur ueber
/// <see cref="Betreten"/>. Es wird nichts geschrieben (kein Ereignis), Oeffnungszeiten und Sperren spielen keine Rolle.
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>
    /// Den fertigen Innenraum <paramref name="innen"/> zeigen (gebaut mit <see cref="Probe.Innenraum.Bauen"/>, im Hintergrund
    /// vorbereitet) und die Figur an (<paramref name="x"/>, <paramref name="z"/>) mit Blick <paramref name="blick"/> stellen.
    /// </summary>
    public void Ansehen(Innenraum innen, float x, float z, float blick)
    {
        WegLeeren();
        Innen = innen;
        EinrichtenBeenden();
        FigurX = x;
        FigurZ = z;
        FigurRichtung = blick;
        InnenText();
        InnenGeaendert?.Invoke();
        Geaendert?.Invoke();
    }
}
