using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.46.0 (Weisung P12-218, Nico 2 = a mit Aenderung, 4 = a): Gebaeude-Modelle, die nur das Werkzeug mitbringt (nicht in
/// der Stadt 1, die bitgleich bleibt). Die Liste „Modell“ bietet sie an (<see cref="AussenBearbeitung.Modelle"/>); beim Setzen
/// kommt das Modell in <c>karte.modelle</c> der Stadt (<see cref="Uebernehmen"/>), damit Stadt-Datei und Spiel es kennen. Ein
/// Modell gleichen Namens in der Stadt geht immer vor.
/// </summary>
public static class Zusatzmodelle
{
    /// <summary>Haltepunkt mit freiem Bahnsteig (0,8 tief), Wartehaeuschen hinter der Bahnsteigkante, Zugzielanzeiger daneben.</summary>
    public const string HaltepunktFrei = "haltepunkt_frei";

    /// <summary>Teile je Modell (gleicher Aufbau wie <c>modelle</c> in orte.json).</summary>
    public static PyObjekt Alle() => new()
    {
        {
            HaltepunktFrei, new List<object?>
            {
                // Bahnsteig frei: nichts steht darauf (P12-213)
                new PyObjekt { { "art", "flaeche" }, { "x", 0L }, { "y", 0L }, { "w", 2.4 }, { "h", 0.8 }, { "farbe", "bahnsteig" } },
                // Wartehaeuschen hinter der Bahnsteigkante (Seite weg vom Gleis)
                new PyObjekt { { "art", "block" }, { "x", 0.3 }, { "y", -0.6 }, { "w", 1.3 }, { "t", 0.6 }, { "h", 0.6 }, { "fenster", false } },
                // Zugzielanzeiger neben dem Haeuschen, nicht auf dem Bahnsteig (P12-218): Mast mit Breite
                new PyObjekt { { "art", "mast" }, { "x", 2.05 }, { "y", -0.3 }, { "h", 0.75 }, { "w", 0.5 } },
            }
        },
    };

    /// <summary>Haus und Bahnsteig gehoeren bei diesen Modellen fest zusammen: „Tür dazu“ trennt den Bahnsteig nicht ab.</summary>
    public static bool BahnsteigFest(string modell) => modell == HaltepunktFrei;

    /// <summary>Legt das Modell in <c>karte.modelle</c> an, wenn die Stadt es noch nicht hat und das Werkzeug es mitbringt.</summary>
    public static void Uebernehmen(PyObjekt aussen, string modell)
    {
        if (Get(AussenBearbeitung.ModelleDerStadt(aussen), modell) is not null || Get(Alle(), modell) is not { } teile)
        {
            return;
        }
        var karte = Aussen.Karte(aussen);
        if (Get(karte, "modelle") is not PyObjekt m)
        {
            m = new PyObjekt();
            Py.Setzen(karte, "modelle", m);
        }
        Py.Setzen(m, modell, teile);
    }
}
