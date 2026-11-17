using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.89.0 (Weisung P12-297 B): die Sicht der Engine auf den Spielstand in einer Paket-Stadt. Alle Werte kommen aus dem
/// Spielstand (<see cref="Stand"/>, unveraendert); nur der Inhalt (Personen und Questgeber der Paket-Stadt) und die offenen
/// Auftraege (die Quests der Paket-Stadt, <see cref="PaketQuests"/>) sind die der Stadt. Nur Anzeige, schreibt nichts.
/// </summary>
public sealed class PaketSicht(Spielstand stand, Stadt stadt) : ISpielSicht
{
    public Spielstand Stand { get; } = stand;

    public Stadt Stadt { get; } = stadt;

    /// <summary>Die offenen Quests der Paket-Stadt.</summary>
    public IReadOnlyList<PyObjekt> Offene => PaketQuests.Offen(Stadt, Stand);

    public PyObjekt Inhalt => Stadt.Inhalt;

    public long Tag => Stand.Tag;

    public long TageFertig => Stand.TageFertig;

    public PyObjekt? Profil => Stand.Profil;

    public long Geld => Stand.Geld;

    public object? Firma => Stand.Firma;

    public object? Zweigstelle => Stand.Zweigstelle;

    public PyDict Raeume => Stand.Raeume;

    public object? Wohnung => Stand.Wohnung;

    public PySet Geloest => Stand.Geloest;

    public object? FarmFrei => Stand.FarmFrei;

    public object? Farm => Stand.Farm;

    public PyDict Auftraege(object? ort = null) => PaketQuests.Auftraege(Stadt, Stand, ort);
}
