using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18, Plan Abschnitt 3): die 3D-Ansicht des Werkzeugs zeigt die Stadt genau so, wie das
/// Spiel sie baut (<see cref="StadtSzene"/> ueber <see cref="StadtSpiel"/>), mit dem Probe-Spielstand der Pruefung
/// (<see cref="StadtbauPruefung.Probestand"/>: alle Orte sichtbar). Gebaut wird im Hintergrund und nur nach einer
/// Aenderung. Mit einem <see cref="StadtSzene.Vorrat"/> (Weisung P12-21, Teilbau) kommen unveraenderte Orte, Hausnetze
/// und das Paket-Netz aus dem letzten Bau; neu gebaut wird nur Geaendertes (das Ergebnis ist dasselbe wie ganz neu). "Probe laufen": Tippen laesst die Figur gehen wie im Spiel
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
    public static StadtSpiel Bauen(StadtFassung stand, Spieldaten daten, Einstellung einstellung, StadtSzene.Vorrat? vorrat = null)
    {
        var stadt = stand.StadtFuer(stand.MitKarte(daten.Inhalt));
        var ablauf = StadtbauPruefung.Probestand(stadt.Inhalt);
        // Phase 13 Welle 4a: Stuecke aus dem Katalog zeichnet die 3D-Ansicht wie das Spiel nach dem Einbau
        var grafik = Katalog.Aktiv?.MitStuecken(daten.Grafik, stand) ?? daten.Grafik;
        var spiel = new StadtSpiel(stadt, ablauf, grafik, stand.Innenraeume, daten.Geschaefte.Daten, daten.Feste,
            () => einstellung.Fest, () => einstellung.Jahreszeit, vorrat);
        spiel.Szene.Vorbereiten();
        return spiel;
    }
}
