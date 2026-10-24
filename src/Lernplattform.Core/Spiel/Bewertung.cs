using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Auswertung eines Tickets wie <c>fisi_game.evaluate</c> (Python 0.63.2, Z. 3070-3230)
/// mit Geld, Reputation und Problemen je Aufgabentyp. Stoppgrenze (Plan Phase 7, Abschnitt 10).
/// Vergleichswerte G46 (<c>spiel_bewertung.json</c>).
/// </summary>
public static partial class Bewertung
{
    /// <summary>_scaled: int(round(wert * faktor)).</summary>
    public static long Skaliert(object? wert, object? faktor) => Py.Mal(wert, faktor) switch
    {
        long n => n,
        double d => PythonRunden.Runde(d),
        var x => throw new InvalidDataException("Zahl erwartet: " + x),
    };

    /// <summary>defer_payload: Ticket verschieben kostet Zuverlaessigkeit (Zwischenfaelle sind danach weg).</summary>
    public static PyObjekt Verschieben(PyObjekt auftrag, long tag, PyObjekt balancing)
    {
        var faktor = Get(Objekt(balancing["prioritaet_faktor"]), Py.Str(auftrag["prioritaet"]), 1L);
        var verlust = Skaliert(balancing["verschieben_verlust"], faktor);
        var daten = new PyObjekt
        {
            { "aufgabe", auftrag["id"] }, { "tag", tag }, { "reputation", new PyObjekt { { "zuverlaessigkeit", -verlust } } },
        };
        if (Py.Wahr(auftrag["zwischenfall"]))
        {
            // Zwischenfaelle warten nicht - er ist danach weg
            Py.Setzen(daten, "zwischenfall", true);
        }
        return daten;
    }
}
