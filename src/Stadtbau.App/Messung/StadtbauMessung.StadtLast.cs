using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;

namespace Stadtbau.Messung;

/// <summary>
/// Engine festigen Stufe 2 Teil 1 (Weisung P12-176): Messpunkte „Stadt bauen“ (einmalig, streng, Grenze
/// <see cref="StadtLast.BauenGrenzeMs"/>) und „Netzdaten“ (Grenze <see cref="StadtLast.NetzdatenGrenzeMb"/>) an Stadt 1 im
/// Werkzeug, Lage wie die Grenzen der 3D-Ansicht (Nacht, Winter, Weihnachten, Probestand mit allen Orten). Gleiche Rechnung
/// wie im Spiel (<see cref="StadtLast"/>); die Szene wird frisch gebaut, ohne Vorrat.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task StadtLastAsync(Projekt p)
    {
        var (stand, daten) = (p.Stand(), p.Daten);
        var spiel = await Task.Run(() => Ansicht3D.Bauen(stand, daten, Ansicht3D.Einstellung.Grenzen));
        var e = await Task.Run(() => StadtLast.Messen(() => StadtSzene.Bauen(spiel.Stadt, spiel.Ablauf.Stand, spiel.Grafik,
            Festzeit.Weihnachten, spiel.Feste, Jahreszeit.Winter)));
        Messwerte.Notiere("Werkzeug: Stadt bauen und Netzdaten Stadt 1 (Nacht, Winter, Weihnachten): " + StadtLast.Text(e));
        Messwerte.Reihe("Werkzeug: Stadt bauen (Bauen und Vorbereiten, einmalig)", e.GesamtMs);
        if (e.GesamtMs > StadtLast.BauenGrenzeMs)
        {
            Fund(string.Create(Inv, $"Werkzeug: Stadt bauen (Bauen und Vorbereiten, einmalig): {e.GesamtMs:F0} ms ueber Grenze {StadtLast.BauenGrenzeMs:F0} ms; Einzelmessungen {e.GesamtMs:F0} ms (einmaliger Messpunkt, nicht wiederholt)"));
        }
        Messwerte.Notiere(string.Create(Inv, $"Werkzeug: Netzdaten: {e.Mb:F1} MB (Grenze {StadtLast.NetzdatenGrenzeMb:F0} MB)"));
        if (e.Mb > StadtLast.NetzdatenGrenzeMb)
        {
            Fund(string.Create(Inv, $"Werkzeug: Netzdaten {e.Mb:F1} MB ueber Grenze {StadtLast.NetzdatenGrenzeMb:F0} MB ({e.Netze} Netze)"));
        }
    }
}
