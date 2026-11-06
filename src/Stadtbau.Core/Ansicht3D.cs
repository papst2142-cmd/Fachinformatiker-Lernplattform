using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18, Plan Abschnitt 3): die 3D-Ansicht des Werkzeugs zeigt die Stadt genau so, wie das
/// Spiel sie baut (<see cref="StadtSzene"/> ueber <see cref="StadtSpiel"/>), mit dem Probe-Spielstand der Pruefung
/// (<see cref="StadtbauPruefung.Probestand"/>: alle Orte sichtbar). Gebaut wird im Hintergrund und nur nach einer
/// Aenderung; die Szene laesst sich nur als Ganzes bauen (rund 150 ms plus 160 ms fuer die Netze), die Grafikkarte
/// laedt dann nur Stadt, Haeuser, Gruen und Paket-Netz neu. "Probe laufen": Tippen laesst die Figur gehen wie im Spiel
/// (der Spielstand lebt nur im Speicher, nichts wird geschrieben).
/// </summary>
public static class Ansicht3D
{
    /// <summary>Was die Ansicht zeigt: Tag/Nacht (feste Uhr), Jahreszeit, Fest (null = keins).</summary>
    public sealed record Einstellung(bool Nacht, Jahreszeit Jahreszeit, string? Fest)
    {
        public static readonly Einstellung Standard = new(false, Jahreszeit.Sommer, null);

        /// <summary>Gesamtansicht fuer die Grenzen: Nacht, Winter, Weihnachten (Plan Abschnitt 6, die meisten Netze).</summary>
        public static readonly Einstellung Grenzen = new(true, Jahreszeit.Winter, Festzeit.Weihnachten);

        /// <summary>Uhr in Minuten fuer <c>Tageslicht.FesteUhr</c> (Mittag 12:00, Nacht 22:00).</summary>
        public int Uhr => Nacht ? 22 * 60 : 12 * 60;
    }

    /// <summary>Spiel fuer die 3D-Ansicht bauen (blockiert: im Hintergrund rufen). Netze sind danach fertig.</summary>
    public static StadtSpiel Bauen(StadtFassung stand, Spieldaten daten, Einstellung einstellung)
    {
        var stadt = stand.StadtFuer(stand.MitKarte(daten.Inhalt));
        var ablauf = StadtbauPruefung.Probestand(stadt.Inhalt);
        var spiel = new StadtSpiel(stadt, ablauf, daten.Grafik, stand.Innenraeume, daten.Geschaefte.Daten, daten.Feste,
            () => einstellung.Fest, () => einstellung.Jahreszeit);
        spiel.Szene.Vorbereiten();
        return spiel;
    }
}
