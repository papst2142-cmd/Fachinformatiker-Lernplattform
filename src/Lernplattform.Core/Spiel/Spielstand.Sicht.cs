using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Weisung P12-179 Schritt 2: der Spielstand liefert der Engine ihre Sicht (<see cref="ISpielSicht"/>). Nur angehaengte
/// Schnittstelle, jedes Glied reicht das Feld unveraendert weiter; kein Verhalten des Spielstands aendert sich.
/// </summary>
public sealed partial class Spielstand : ISpielSicht
{
    PyObjekt ISpielSicht.Inhalt => Inhalt;

    long ISpielSicht.Tag => Tag;

    long ISpielSicht.TageFertig => TageFertig;

    PyObjekt? ISpielSicht.Profil => Profil;

    long ISpielSicht.Geld => Geld;

    object? ISpielSicht.Firma => Firma;

    object? ISpielSicht.Zweigstelle => Zweigstelle;

    PyDict ISpielSicht.Raeume => Raeume;

    object? ISpielSicht.Wohnung => Wohnung;

    PySet ISpielSicht.Geloest => Geloest;

    object? ISpielSicht.FarmFrei => FarmFrei;

    object? ISpielSicht.Farm => Farm;

    PyDict ISpielSicht.Auftraege(object? ort) => Auftraege(ort);
}
