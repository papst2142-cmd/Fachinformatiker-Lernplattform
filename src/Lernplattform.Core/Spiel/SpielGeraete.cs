using System.Runtime.CompilerServices;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 8, W3 (Weisung P8-5, L3, Nicos Antwort 1a): welches Geraet die Schritte der Stadt geschrieben hat.
/// Haben PC und Handy offline am selben Spieltag gespielt, nimmt die Tagesuhr der Stadt das Geraet, das an
/// diesem Tag am weitesten gespielt hat, statt die Minuten beider Geraete zu addieren (<see cref="Stadt.Stand"/>).
/// Das Geraet steht nur in der Spalte geraet der Datenbank, nicht in den Daten des Ereignisses (die bleiben,
/// wie sie sind, die Fingerabdruecke aendern sich nicht). Zugeordnet wird ueber Zeitstempel und Art; eine
/// Zeile, die die Datenbank noch nicht kennt (wartet in der Schlange, schrittweise angewendet), stammt vom
/// eigenen Geraet. Ohne Zuordnung (Spiel im Arbeitsspeicher, Probe) zaehlt alles als ein Geraet wie bisher.
/// </summary>
public sealed class SpielGeraete
{
    private static readonly ConditionalWeakTable<Spielstand, SpielGeraete> Staende = new();

    private readonly Dictionary<(string Zeit, string Art), string> geraete;

    /// <param name="eigenes">Das Geraet dieses Programms.</param>
    /// <param name="zeilen">Zeitstempel, Art und Geraet der Stadt-Zeilen des Durchgangs.</param>
    public SpielGeraete(string eigenes, IEnumerable<(string Zeit, string Art, string Geraet)> zeilen)
    {
        Eigenes = eigenes;
        geraete = [];
        var doppelt = new HashSet<(string, string)>();
        foreach (var (zeit, art, geraet) in zeilen)
        {
            if (geraete.TryGetValue((zeit, art), out var schon) && schon != geraet)
            {
                doppelt.Add((zeit, art));   // zwei Geraete in derselben Sekunde: nicht zuzuordnen
            }
            geraete[(zeit, art)] = geraet;
        }
        foreach (var schluessel in doppelt)
        {
            geraete.Remove(schluessel);
        }
    }

    public string Eigenes { get; }

    /// <summary>Das Geraet eines Ereignisses (unbekannt oder nicht eindeutig: das eigene).</summary>
    public string Von(SpielEreignis ereignis) =>
        ereignis.Zeit is string zeit && geraete.TryGetValue((zeit, ereignis.Art), out var geraet) ? geraet : Eigenes;

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
