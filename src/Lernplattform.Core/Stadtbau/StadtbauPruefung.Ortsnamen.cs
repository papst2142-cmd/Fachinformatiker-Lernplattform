using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.52.0 / S 0.21.0 (Weisung P12-89, Nico 08.11.2026 14:06 MEZ „Stellwerk Talheim-Nord bitte.“): Namen der Orte
/// in einer Fassung. Das Spiel zeigt den Namen eines Orts in der Stadt (Schild, Ortsliste, Karte) aus der Karte der Fassung
/// (<c>Stadt.Orte</c>, Feld <c>name</c>); gespeichert wird in Ereignissen und Spielstand nur die Kennung. Darum darf das
/// Werkzeug den Namen aendern; die Kennung, der Kunde, die Aufgaben und der Innenraum bleiben.
/// Regeln (alle rot): Name leer, Name doppelt (gross/klein egal), Name laenger als <see cref="OrtNameHoechstens"/> Zeichen.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>
    /// Hoechstens so viele Zeichen hat der Name eines Orts (E1: laengster Name der Fassung 0 „Rechenzentrum Lokschuppen“ hat 25,
    /// die Katalog-Namen haben dieselbe Grenze 28; das Schild ueber dem Haus bleibt damit lesbar).
    /// </summary>
    public const int OrtNameHoechstens = 28;

    /// <summary>Grund, warum ein Name fuer einen Ort nicht geht (null = geht). <paramref name="andere"/> = Namen der anderen Orte.</summary>
    public static string? OrtNameNicht(string? name, IEnumerable<string> andere)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0)
        {
            return "Der Name ist leer.";
        }
        if (n.Length > OrtNameHoechstens)
        {
            return $"Der Name „{n}“ hat {n.Length} Zeichen, erlaubt sind höchstens {OrtNameHoechstens}.";
        }
        if (andere.Any(a => string.Equals(a.Trim(), n, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Den Namen „{n}“ hat schon ein anderer Ort.";
        }
        return null;
    }

    private static void OrtsnamenRegeln(StadtFassung fassung, List<StadtbauFund> funde)
    {
        var orte = ListeOderLeer(fassung.Karte, "orte").OfType<PyObjekt>().Concat(ListeOderLeer(fassung.Stadtdaten, "neue_orte").OfType<PyObjekt>()).ToList();
        for (var i = 0; i < orte.Count; i++)
        {
            var o = orte[i];
            var id = Py.Str(o["id"]);
            var name = Get(o, "name") as string;
            // doppelt: nur der zweite und jeder weitere Ort mit dem Namen ist ein Fund
            var vorher = orte.Take(i).Select(v => Get(v, "name") as string).OfType<string>();
            if (OrtNameNicht(name, vorher) is { } grund)
            {
                funde.Add(new StadtbauFund($"Ort „{(string.IsNullOrWhiteSpace(name) ? id : name)}“: {grund}",
                    "Im Feld „Name des Orts“ einen anderen Namen eingeben.", true, Py.F(o["x"]), Py.F(o["y"])));
            }
        }
    }
}
