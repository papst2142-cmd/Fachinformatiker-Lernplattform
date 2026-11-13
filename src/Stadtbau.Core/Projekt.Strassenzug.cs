using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>Welle S 0.51.0 (Weisung P12-241): Strasse mit Ausstattung ziehen als EIN Schritt im Verlauf.</summary>
public sealed partial class Projekt
{
    /// <summary>
    /// Pruefung draussen fuer die Nachpruefung eines Strassenzugs (darf im Hintergrund laufen): ein Stand nach dem Schritt mit
    /// denselben automatischen Laternen, die der echte Schritt aufstellt, und dieselbe Pruefung wie die Liste rechts.
    /// </summary>
    public Func<PyObjekt, List<StadtbauFund>> ZugPruefen()
    {
        var laternen = LaternenFuerVorschau();
        var (basis, innen, daten) = (Basis, Innenraeume(), Daten);
        return stand => Bauen3D.Pruefen(laternen(stand), basis, innen, daten);
    }

    /// <summary>
    /// Strasse von (ax, ay) nach (bx, by) mit der Ausstattung <paramref name="vorlage"/> ziehen: ein Schritt (Strasse,
    /// Ausstattung und automatische Laternen zusammen, einmal rueckgaengig). <paramref name="plan"/> = fertig nachgepruefter Plan
    /// derselben Punkte aus der Vorschau, sonst wird hier geplant und nachgeprueft.
    /// </summary>
    public (AussenStelle? Strasse, List<AussenStelle> Neu, StrassenzugPlan Plan) StrassenzugZiehen(double ax, double ay, double bx, double by, bool raster,
        Strassenzug vorlage, StrassenzugPlan? plan = null)
    {
        plan ??= AussenBearbeitung.StrassenzugPlanen(AussenStand, ax, ay, bx, by, raster, vorlage, ZugPruefen());
        var fertig = plan;
        if (!fertig.Gefunden)
        {
            return (null, [], fertig);
        }
        return AussenAendern(Texte.Strasse + " " + Texte.Gezogen + " (" + vorlage.Name + ")",
            a => AussenBearbeitung.StrassenzugZiehen(a, ax, ay, bx, by, raster, vorlage, fertig));
    }
}
