using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Aussehen der Spielfigur wie <c>fisi_game.APPEARANCE</c> (Python 0.63.2, Z. 155-195):
/// je Teil die Wahl (Schluessel, Anzeigename), die erste ist die Voreinstellung.
/// </summary>
public static class Aussehen
{
    /// <summary>APPEARANCE in Pythons Reihenfolge.</summary>
    public static readonly IReadOnlyList<(string Teil, IReadOnlyList<(string Kennung, string Name)> Wahl)> Teile =
    [
        ("haut", [("hell", "Hell"), ("mittel", "Mittel"), ("oliv", "Oliv"), ("dunkel", "Dunkel")]),
        ("frisur", [("kurz", "Kurz"), ("lang", "Lang"), ("zopf", "Zopf"), ("locken", "Locken"), ("glatze", "Glatze")]),
        ("haarfarbe", [("schwarz", "Schwarz"), ("braun", "Braun"), ("blond", "Blond"), ("rot", "Rot"), ("grau", "Grau")]),
        ("oberteil", [("cyan", "Cyan"), ("pink", "Pink"), ("violett", "Violett"), ("gruen", "Grün"), ("orange", "Orange")]),
        ("extra", [("keins", "Nichts"), ("brille", "Brille"), ("headset", "Headset"), ("kappe", "Kappe")]),
        ("kreis", [("pink", "Pink"), ("cyan", "Cyan"), ("gruen", "Grün"), ("violett", "Violett"), ("orange", "Orange"), ("gelb", "Gelb")]),
    ];

    /// <summary>DEFAULT_APPEARANCE</summary>
    public static PyObjekt Voreinstellung()
    {
        var d = new PyObjekt();
        foreach (var (teil, wahl) in Teile)
        {
            d.Add(teil, wahl[0].Kennung);
        }
        return d;
    }

    /// <summary>normalize_appearance: unbekannte oder fehlende Werte durch die Voreinstellung ersetzen.</summary>
    public static PyObjekt Normalisieren(object? aussehen)
    {
        var ergebnis = Voreinstellung();
        var quelle = Py.Wahr(aussehen) ? aussehen as PyObjekt ?? throw new InvalidDataException("aussehen ist kein dict") : new PyObjekt();
        foreach (var (teil, wahl) in Teile)
        {
            var wert = quelle[teil];
            if (wahl.Any(w => Py.Gleich(w.Kennung, wert)))
            {
                Py.Setzen(ergebnis, teil, wert);
            }
        }
        return ergebnis;
    }
}
