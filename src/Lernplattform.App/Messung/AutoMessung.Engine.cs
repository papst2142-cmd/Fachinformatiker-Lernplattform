using System.Globalization;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Messung;

/// <summary>
/// Engine festigen Stufe 2 Teil 1 (Weisung P12-176): Messpunkte „Stadt bauen“ (Zeit fuer Bauen und Vorbereiten aller Netze,
/// einmalig, streng, Grenze <see cref="StadtLast.BauenGrenzeMs"/>) und „Netzdaten“ (MB aller Netze, Grenze
/// <see cref="StadtLast.NetzdatenGrenzeMb"/>) an der teuersten Lage: alle Orte sichtbar, Winter, Weihnachten. Die Szene wird
/// dafuer frisch gebaut (nicht aus dem Vorrat), das Spiel bleibt unveraendert.
/// </summary>
public static partial class AutoMessung
{
    private static int StadtLastMessen(StadtSpiel spiel)
    {
        var inv = CultureInfo.InvariantCulture;
        var e = StadtLast.Messen(() => StadtSzene.Bauen(spiel.Stadt, spiel.Ablauf.Stand, spiel.Grafik, Festzeit.Weihnachten, spiel.Feste, Jahreszeit.Winter));
        Messwerte.Notiere("Spiel: Stadt bauen und Netzdaten (alle Orte, Winter, Weihnachten): " + StadtLast.Text(e));
        var funde = Ergebnis("Spiel: Stadt bauen (Bauen und Vorbereiten, einmalig)", Messwiederholung.Einmal(e.GesamtMs, StadtLast.BauenGrenzeMs));
        Messwerte.Notiere(string.Create(inv, $"Spiel: Netzdaten: {e.Mb:F1} MB (Grenze {StadtLast.NetzdatenGrenzeMb:F0} MB)"));
        if (e.Mb > StadtLast.NetzdatenGrenzeMb)
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel: Netzdaten {e.Mb:F1} MB ueber Grenze {StadtLast.NetzdatenGrenzeMb:F0} MB ({e.Netze} Netze)"));
            funde++;
        }
        return funde;
    }
}
