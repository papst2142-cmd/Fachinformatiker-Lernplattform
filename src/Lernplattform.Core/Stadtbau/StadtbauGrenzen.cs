using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitt 4): Grenzen der 3D-Zeichnung wie im Spiel (Nico 29.10.2026:
/// 160 000 Dreiecke, 40 Zeichenaufrufe). Ein Innenraum zeichnet das Spiel (<c>StadtFlaeche</c>) mit einem Aufruf fuer den
/// Raum ohne Waende, einem je Wandstueck (jede Wand wird einzeln durchsichtig), einem fuer die eigene Figur und einem je
/// Person; Dreiecke = Indizes / 3 je Netz. <see cref="Innen"/> zaehlt genau diese Netze (die eigene Figur in der
/// Grundform ohne Kleidung); der Start-Test des Spiels vergleicht die Zaehlung mit dem gezeichneten Bild.
/// </summary>
public static partial class StadtbauGrenzen
{
    public const long Dreiecke = 160_000;

    public const int Aufrufe = 40;

    /// <summary>Dreiecke und Aufrufe eines Innenraums; <paramref name="figurDreiecke"/> = Dreiecke der eigenen Figur (null = Grundform).</summary>
    public static (long Dreiecke, int Aufrufe) Innen(Innenraum innen, long? figurDreiecke = null, IReadOnlyDictionary<string, PyObjekt?>? aussehen = null)
    {
        long dreiecke = innen.NetzOhneWaende.Indizes.Count / 3;
        var aufrufe = 1;
        foreach (var wand in innen.Waende)
        {
            dreiecke += innen.WandNetzFertig(wand).Indizes.Count / 3;
            aufrufe++;
        }
        dreiecke += figurDreiecke ?? Figur.Netz(innen.Grafik, null, 1f).Indizes.Count / 3;
        aufrufe++;
        foreach (var person in innen.Personen)
        {
            var bild = aussehen is not null && aussehen.TryGetValue(person.Id, out var a) ? a : person.Aussehen;
            dreiecke += (innen.PersonNetz(person.Id, bild) ?? Figur.Netz(innen.Grafik, bild, 1f)).Indizes.Count / 3;
            aufrufe++;
        }
        return (dreiecke, aufrufe);
    }

    /// <summary>Anteil an der Grenze (der groessere von Dreiecken und Aufrufen), 1 = genau an der Grenze.</summary>
    public static double Anteil(long dreiecke, int aufrufe) => Math.Max((double)dreiecke / Dreiecke, (double)aufrufe / Aufrufe);
}
