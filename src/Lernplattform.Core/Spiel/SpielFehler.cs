namespace Lernplattform.Core.Spiel;

/// <summary>
/// Fehler einer Spielaktion mit dem Text fuer die Anzeige (ValueError in Python). Weisung P12-179: unveraendert aus
/// SpielAktionen.cs in eine eigene Datei, gehoert zu den Grundlagen (die Engine faengt ihn beim Laden einer Stadt).
/// </summary>
public sealed class SpielFehler(string text) : Exception(text);
