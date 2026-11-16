using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle Nachladen S 0.54.0 (Weisung P12-280, Nico 1c): Werte je Stadtteil (Kachel 16 × 16) fuer den Schalter
/// „Stadtteile zeigen“. Gerechnet in der Szene der Grenzen (Nacht, Winter, Weihnachten: die meisten Netze), genau wie das
/// Spiel eine Kachel nachlaedt (<see cref="Nachlader.Packen"/>, mit Kleingruen): Dreiecke, Netze (= Zeichenaufrufe der
/// Kachel) und Netzdaten. Nur Anzeige; ob eine Kachel zu teuer ist, rechnet erst die Welle S 0.55.0 (je Kachel live).
/// </summary>
public sealed record StadtteilWert(Stadtteile.Kachel Kachel, long Dreiecke, int Netze, long Bytes)
{
    /// <summary>Name der Kachel (A1, B3 …).</summary>
    public string Name => Kachel.Name;

    /// <summary>Netzdaten in MB.</summary>
    public double Mb => Bytes / 1048576.0;

    /// <summary>Alle Kacheln eines gebauten Spiels (Reihe fuer Reihe, links nach rechts), auf allen Kernen gerechnet.</summary>
    public static IReadOnlyList<StadtteilWert> Rechnen(StadtSpiel spiel)
    {
        var szene = spiel.Szene;
        var kacheln = Stadtteile.Alle(szene.Breite, szene.Tiefe).ToList();
        var werte = new StadtteilWert[kacheln.Count];
        _ = szene.GrundlageVorbereiten();
        _ = szene.Laternen;
        Parallel.For(0, kacheln.Count, i =>
        {
            var p = Nachlader.Packen(szene, kacheln[i], mitGruen: true);
            var netze = (p.Fest.Indizes.Count > 0 ? 1 : 0) + p.Haeuser.Count;
            werte[i] = new StadtteilWert(kacheln[i], (p.Fest.Indizes.Count / 3) + p.Haeuser.Sum(h => (long)h.Netz.Indizes.Count / 3), netze, p.Bytes);
        });
        return werte;
    }

    /// <summary>Stand eines Projekts bauen (Szene der Grenzen) und alle Kacheln rechnen (blockiert, im Hintergrund rufen).</summary>
    public static IReadOnlyList<StadtteilWert> Fuer(StadtFassung stand, Spieldaten daten) =>
        Rechnen(Ansicht3D.Bauen(stand, daten, Ansicht3D.Einstellung.Grenzen));
}
