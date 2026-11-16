using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.83.0 (Weisung P12-282 Punkt 3): wann das Spiel das kurze Schild mit dem Namen eines Stadtteils zeigt. Nur
/// Darstellung: draussen, sobald die Figur einen anderen benannten Stadtteil betritt (auch beim ersten Bild, wenn sie schon in
/// einem steht). Verlaesst sie einen Stadtteil ins Unbenannte, kommt nichts; kehrt sie aus einem Haus in denselben Stadtteil
/// zurueck, auch nichts. Ohne Namen (Stadt 1, Fassung 0) kommt nie ein Schild. Kein Spielstand, kein Ereignis.
/// </summary>
public sealed class StadtteilSchild
{
    /// <summary>So lange steht das Schild (Sekunden), danach blendet es aus.</summary>
    public const double Sekunden = 3.5;

    private string? _zuletzt;

    /// <summary>Der Stadtteil an der Stelle der Figur beim letzten Takt (null = keiner oder drinnen).</summary>
    public string? Zuletzt => _zuletzt;

    /// <summary>Ein Takt (je Bild): Rueckgabe = Name fuer ein neues Schild, sonst null.</summary>
    public string? Takt(StadtSpiel spiel)
    {
        if (spiel.ImInnenraum || spiel.Szene.StadtteilNamen.Count == 0)
        {
            return null;
        }
        var name = Stadtteile.An(spiel.Szene.StadtteilNamen, spiel.FigurX, spiel.FigurZ)?.Name;
        if (name == _zuletzt)
        {
            return null;
        }
        _zuletzt = name;
        return name;
    }
}
