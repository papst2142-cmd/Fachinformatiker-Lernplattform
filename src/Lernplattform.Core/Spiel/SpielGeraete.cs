using System.Runtime.CompilerServices;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 8, W3 (Weisung P8-5, L3, Nicos Antwort 1a): welches Geraet die Schritte der Stadt geschrieben hat.
/// Haben PC und Handy offline am selben Spieltag gespielt, nimmt die Tagesuhr der Stadt das Geraet, das an
/// diesem Tag am weitesten gespielt hat, statt die Minuten beider Geraete zu addieren (<see cref="Stadt.Stand"/>).
/// Das Geraet steht nur in der Spalte geraet der Datenbank, nicht in den Daten des Ereignisses (die bleiben,
/// wie sie sind, die Fingerabdruecke aendern sich nicht). FW8 (Sammelliste 118): zugeordnet wird ueber
/// Zeitstempel, Art und Daten der Zeile und ihre Stelle in der Reihenfolge des Neuaufbaus (Zeitstempel, dann
/// id). Schreiben zwei Geraete in derselben Sekunde dieselbe Art, unterscheiden die Daten sie; sind auch die
/// Daten gleich, sind die Schritte gleich, und jedes Geraet bekommt einen davon (wie in der Datenbank). Eine
/// Zeile, die die Datenbank noch nicht kennt (wartet in der Schlange, schrittweise angewendet), stammt vom
/// eigenen Geraet. Ohne Zuordnung (Spiel im Arbeitsspeicher, Probe) zaehlt alles als ein Geraet wie bisher.
/// </summary>
public sealed class SpielGeraete
{
    private static readonly ConditionalWeakTable<Spielstand, SpielGeraete> Staende = new();

    // Schluessel eines Ereignisses (Zeitstempel, Art, Daten als Text); je Ereignis-Objekt nur einmal gerechnet
    private static readonly ConditionalWeakTable<SpielEreignis, string> Schluessel = new();

    private readonly Dictionary<string, List<string>> geraete = new(StringComparer.Ordinal);

    /// <param name="eigenes">Das Geraet dieses Programms.</param>
    /// <param name="zeilen">Zeitstempel, Art, Daten und Geraet der Stadt-Zeilen des Durchgangs, in der Reihenfolge des Neuaufbaus.</param>
    public SpielGeraete(string eigenes, IEnumerable<(string Zeit, string Art, PyObjekt Daten, string Geraet)> zeilen)
    {
        Eigenes = eigenes;
        foreach (var (zeit, art, daten, geraet) in zeilen)
        {
            var schluessel = SchluesselVon(zeit, art, daten);
            if (!geraete.TryGetValue(schluessel, out var liste))
            {
                geraete[schluessel] = liste = [];
            }
            liste.Add(geraet);
        }
    }

    public string Eigenes { get; }

    private static string SchluesselVon(object? zeit, string art, PyObjekt daten) =>
        PythonJson.Standard(new List<object?> { zeit, art, daten });

    /// <summary>
    /// Zuordnung fuer einen Durchlauf durch den Verlauf: jedes Ereignis einer Stadt-Art der Reihe nach uebergeben
    /// (auch die, die danach nicht zaehlen); die n-te Zeile mit gleichem Schluessel gehoert dem n-ten Geraet.
    /// </summary>
    public Func<SpielEreignis, string> Durchlauf()
    {
        var gesehen = new Dictionary<string, int>(StringComparer.Ordinal);
        return ereignis =>
        {
            var schluessel = Schluessel.GetValue(ereignis, e => SchluesselVon(e.Zeit, e.Art, e.Daten));
            var n = gesehen.TryGetValue(schluessel, out var bisher) ? bisher : 0;
            gesehen[schluessel] = n + 1;
            return geraete.TryGetValue(schluessel, out var liste) && n < liste.Count ? liste[n] : Eigenes;
        };
    }

    /// <summary>Die Zuordnung an einen Spielstand haengen (der Spielablauf tut das bei jedem neuen Stand).</summary>
    public static void Setzen(Spielstand stand, SpielGeraete? geraete)
    {
        if (geraete is null)
        {
            Staende.Remove(stand);
        }
        else
        {
            Staende.AddOrUpdate(stand, geraete);
        }
    }

    /// <summary>Die Zuordnung eines Spielstands (null: alles ein Geraet).</summary>
    public static SpielGeraete? Von(Spielstand stand) => Staende.TryGetValue(stand, out var geraete) ? geraete : null;
}
