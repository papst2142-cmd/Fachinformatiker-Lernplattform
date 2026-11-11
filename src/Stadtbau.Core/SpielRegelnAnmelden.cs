using System.Runtime.CompilerServices;
using Lernplattform.Core.Spiel;

namespace Stadtbau.Core;

/// <summary>
/// Weisung P12-179: die eigene Engine holt die Regeln des Spiels ueber <c>SpielKern.Regeln</c>; Lernplattform.Core meldet
/// sie beim Laden an. Das Werkzeug meldet sie beim eigenen Laden zusaetzlich an, damit keine Pruefung des Werkzeugs vor dem
/// ersten Zugriff auf Lernplattform.Core die Engine ruft (gleiche Regeln, ein weiterer Aufruf aendert nichts).
/// </summary>
internal static class SpielRegelnAnmelden
{
#pragma warning disable CA2255 // Modul-Start ist hier gewollt (siehe oben)
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Anmelden() => SpielKernRegeln.Anmelden();
}
