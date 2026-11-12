using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18, Plan Abschnitt 3): die 3D-Ansicht des Werkzeugs zeigt die Stadt genau so, wie das
/// Spiel sie baut (<see cref="StadtSzene"/> ueber <see cref="StadtSpiel"/>), mit dem Probe-Spielstand der Pruefung
/// (<see cref="StadtbauSpiel.Probestand"/>: alle Orte sichtbar). Gebaut wird im Hintergrund und nur nach einer
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

    /// <summary>Gegenprobe Fund 305: Laterne ohne freien Platz bricht den Bau ab (Stand S 0.42.0).</summary>
    public static bool LaterneAltFuerTests { get => LaterneAlt.Value; set => LaterneAlt.Value = value; }
    private static readonly AsyncLocal<bool> LaterneAlt = new();

    /// <summary>Spiel fuer die 3D-Ansicht bauen (blockiert: im Hintergrund rufen). Netze sind danach fertig.</summary>
    public static StadtSpiel Bauen(StadtFassung stand, Spieldaten daten, Einstellung einstellung, StadtSzene.Vorrat? vorrat = null)
    {
        Stadt stadt;
        // E2 (Weisung P12-204): ein Schalter „nachsichtig“ fuer alle Ansichten des Werkzeugs
        using (Nachsicht.Werkzeugansicht())
        {
            stadt = stand.StadtFuer(stand.MitKarte(daten.Inhalt));
        }
        // S 0.20.0 (Weisung P12-84): die Autos fahren im Werkzeug die Wege von Tag 1 (der Probe-Spielstand steht auf Tag 201)
        stadt.VerkehrTag = Autostrecken.WerkzeugTag;
        // S 0.36.0 (Weisung P12-170 Hinweis 4): im Werkzeug sind alle Stellplaetze belegt (Grenzen zaehlen den vollen Parkplatz)
        stadt.ParkplaetzeVoll = true;
        var ablauf = StadtbauSpiel.Probestand(stadt.Inhalt);
        // Phase 13 Welle 4a: Stuecke aus dem Katalog zeichnet die 3D-Ansicht wie das Spiel nach dem Einbau
        var grafik = Katalog.Aktiv?.MitStuecken(daten.Grafik, stand) ?? daten.Grafik;
        // S 0.9.0: selbst gebaute Gebaeude (stadtdaten.bauteile) mit demselben Code wie das Spiel nach dem Einbau
        grafik = MitGebaeuden(grafik, stand);
        // Fund 305 (P12-199): eine Laterne ohne freien Platz laesst nur die Ansicht aus (vorher brach der ganze Bau ab, die
        // 3D-Ansicht blieb beim alten Bild mit „Fehler“); die Pruefung meldet sie weiter rot. Gegenprobe LaterneAltFuerTests
        // (seit E2 ueber denselben Schalter <see cref="Nachsicht"/>)
        using (LaterneAltFuerTests ? null : Nachsicht.Werkzeugansicht())
        {
            var spiel = new StadtSpiel(stadt, ablauf, grafik, stand.Innenraeume, daten.Geschaefte.Daten, daten.Feste,
                () => einstellung.Fest, () => einstellung.Jahreszeit, vorrat);
            spiel.Szene.Vorbereiten();
            return spiel;
        }
    }

    /// <summary>
    /// S 0.9.0: die Grafik mit den zusammengesetzten selbst gebauten Gebaeuden (<see cref="Bauteile.MitGebaeuden"/>). Fehlt
    /// ein Teil im Katalog oder ist der Aufbau falsch, bleibt das Gebaeude ein schlichter Block (die Pruefung meldet es).
    /// </summary>
    public static GrafikBibliothek? MitGebaeuden(GrafikBibliothek? grafik, StadtFassung stand)
    {
        try
        {
            return Bauteile.MitGebaeuden(grafik, stand.Stadtdaten, Katalog.Bauteil);
        }
        catch (InvalidDataException)
        {
            return grafik;
        }
    }
}
