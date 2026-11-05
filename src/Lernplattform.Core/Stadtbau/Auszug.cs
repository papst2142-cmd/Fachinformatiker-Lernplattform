using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 1 (Weisung P12-16, Plan Abschnitt 6): das Spiel schreibt die heutige Stadt 1 einmal selbst als Fassung 0
/// aus: Karte, Stadtdaten und Innenraeume unveraendert aus den drei Dateien, dazu die bisher fest im Programm stehenden
/// Laternenplaetze (<see cref="Nachtlicht.Skizze"/>) und Beete (<see cref="Nachtlicht.Beete"/>). Das Ergebnis ist bytegleich
/// mit <c>inhalte_csharp/spiel/stadtbau/stadt1_fassung0.fistadt</c> (Test <c>StadtbauFassungTests</c>); aendert sich eine
/// der Quellen, ist der Test rot (die heutige Stadt ist geschuetzt, Aenderungen kommen nur als neue Fassung).
/// </summary>
public static class Auszug
{
    /// <summary>Fester Zeitpunkt der Fassung 0 (damit der Auszug bei jedem Lauf bytegleich ist).</summary>
    public const string Erstellt0 = "2026-11-05T22:30:00+01:00";

    public const string Werkzeug0 = "Auszug aus dem Spiel (Phase 13 Welle 1)";

    public const string Nachricht0 = "Stadt 1 bis A 0.41.1, geschützt; Änderungen nur als neue Fassung.";

    /// <summary>Fassung 0 aus <c>inhalte/spiel/orte.json</c>, <c>inhalte_csharp/spiel/stadt.json</c> und <c>innenraeume.json</c>.</summary>
    public static StadtFassung Fassung0(ReadOnlySpan<byte> orte, ReadOnlySpan<byte> stadt, ReadOnlySpan<byte> innenraeume) => new()
    {
        StadtId = AktiveFassung.Stadt1,
        Nummer = 0,
        Basis = null,
        Werkzeug = Werkzeug0,
        Erstellt = Erstellt0,
        Nachricht = Nachricht0,
        Karte = SpielDaten.Objekt(Py.Lesen(orte)),
        Stadtdaten = SpielDaten.Objekt(Py.Lesen(stadt)),
        Innenraeume = SpielDaten.Objekt(Py.Lesen(innenraeume)),
        Laternen = Nachtlicht.Skizze,
        Beete = Nachtlicht.Beete,
    };
}
