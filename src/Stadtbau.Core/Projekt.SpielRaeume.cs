using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.60.0 (Weisung P12-337): die Raeume des Spiels (<see cref="Core.SpielRaeume"/>) im Projekt. Sie stehen nicht in
/// <see cref="Innenorte"/> (die Liste der bearbeitbaren Innenraeume, der Pruefung und des Exports bleibt wie bisher), lassen
/// sich aber mit ihrem Schluessel ansehen: <see cref="Grundriss"/> und <see cref="Personen"/> liefern genau den Grundriss
/// und die Personen, die das Spiel beim Betreten baut. Jede Aenderung lehnt das Projekt ab (nur ansehen).
/// </summary>
public sealed partial class Projekt
{
    private readonly Dictionary<string, Innenraum?> _spielRaeume = [];

    /// <summary>Alle Raeume des Spiels zu diesen Spieldaten (Wohnungen, Buero, Serverfarm, Zweigstelle, Kunden).</summary>
    public List<SpielRaeume.Raum> SpielRaeumeListe() => SpielRaeume.Alle(Daten.Inhalt, Innenraeume());

    /// <summary>Der Raum des Spiels, gebaut wie im Spiel (einmal je Projekt, die Spieldaten aendern sich im Werkzeug nicht).</summary>
    public Innenraum? SpielRaum(string schluessel)
    {
        if (!_spielRaeume.TryGetValue(schluessel, out var innen))
        {
            var innenraeume = Innenraeume();
            innen = SpielRaeume.Finden(schluessel, Daten.Inhalt, innenraeume) is { } raum
                ? SpielRaeume.Bauen(raum, Daten.Inhalt, innenraeume, Daten.Grafik, Daten.Geschaefte.Daten, Daten.Feste)
                : null;
            _spielRaeume[schluessel] = innen;
        }
        return innen;
    }

    /// <summary>Name eines Raums des Spiels fuer die Anzeige.</summary>
    public string SpielRaumName(string schluessel) =>
        SpielRaeume.Finden(schluessel, Daten.Inhalt, Innenraeume())?.Name ?? schluessel;

    private PyObjekt SpielRaumGrundriss(string schluessel) => SpielRaum(schluessel)?.Gebaeude ?? throw new KeyNotFoundException(schluessel);
}
