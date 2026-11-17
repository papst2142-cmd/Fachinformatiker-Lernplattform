using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>Welle S 0.56.0 (Sammelliste 362): Haeuser beim „Stadtteil einsetzen“ wie beim Einfuegen aus der Zwischenablage.</summary>
public static partial class AussenBearbeitung
{
    /// <summary>
    /// Ein Haus-Stueck der Zwischenablage um (<paramref name="dx"/>, <paramref name="dy"/>) versetzt einsetzen (dieselbe Rechnung
    /// wie <see cref="Einfuegen"/>: Spielort, den das Ziel schon hat, = Gebaeude ohne Ort mit Hinweis; Modell gleichen Namens mit
    /// anderen Teilen = Grund).
    /// </summary>
    internal static (AussenStelle? Stelle, string? Grund, string? Hinweis) HausEinsetzen(PyObjekt ziel, PyObjekt stueck, double dx, double dy, StadtFassung stadt1) =>
        HausEinfuegen(ziel, stueck, dx, dy, stadt1);
}
