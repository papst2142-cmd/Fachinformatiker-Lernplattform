using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Sammelliste 264 (Weisung P12-130 Frage 3 = c): Regeln fuer <c>stadtdaten.farben</c> (<see cref="Gebaeudefarben"/>), alle
/// rot: kein Objekt; Kennung ist weder ein Ort noch das Modell eines Gebaeudes ohne Ort; Eintrag kein Objekt oder leer;
/// unbekannter Teil (nur fassade, dach, rahmen, tuer); Wert keine Farbe <c>#RRGGBB</c> (ausserhalb des Bereichs). Ohne Feld
/// (Fassung 0) kein Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    private static void FarbenDaten(StadtFassung fassung, PyObjekt welt, List<StadtbauFund> funde)
    {
        if (!Py.Hat(fassung.Stadtdaten, Gebaeudefarben.Schluessel))
        {
            return;
        }
        const string Rat = "Farbe des Gebäudes über „Farbe“ neu wählen oder „Wie vorher“ tippen.";
        if (Get(fassung.Stadtdaten, Gebaeudefarben.Schluessel) is not PyObjekt alle)
        {
            funde.Add(new StadtbauFund("Die Farben der Gebäude sind falsch gespeichert.", Rat, true));
            return;
        }
        var kennungen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var quelle in new[] { Get(fassung.Karte, "orte"), Get(fassung.Stadtdaten, "neue_orte") })
        {
            foreach (var o in (quelle as List<object?> ?? []).OfType<PyObjekt>())
            {
                kennungen.Add(Py.Str(o["id"]));
            }
        }
        foreach (var g in ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "gebaeude")))
        {
            kennungen.Add(Py.Str(g["modell"]));
        }
        foreach (var (kennung, wert) in alle)
        {
            var name = kennung;
            if (!kennungen.Contains(kennung))
            {
                funde.Add(new StadtbauFund($"Farben für „{name}“: dieses Gebäude gibt es nicht.", Rat, true));
                continue;
            }
            if (wert is not PyObjekt teile || teile.Count == 0)
            {
                funde.Add(new StadtbauFund($"Die Farben von „{name}“ sind falsch gespeichert.", Rat, true));
                continue;
            }
            foreach (var (teil, farbe) in teile)
            {
                if (teil == Gebaeudefarben.TuerSchluessel)
                {
                    if (!Gebaeudefarben.TuerGueltig(farbe))
                    {
                        funde.Add(new StadtbauFund($"Die Tür-Farbe von „{name}“ gibt es nicht.", Rat, true));
                    }
                }
                else if (!Gebaeudefarben.Teile.Contains(teil))
                {
                    funde.Add(new StadtbauFund($"Die Farben von „{name}“ nennen einen unbekannten Teil „{teil}“.", Rat, true));
                }
                else if (!Gebaeudefarben.Gueltig(farbe))
                {
                    funde.Add(new StadtbauFund($"Die Farbe „{Gebaeudefarben.TeilName(teil)}“ von „{name}“ liegt außerhalb des Bereichs.", Rat, true));
                }
            }
        }
    }
}
