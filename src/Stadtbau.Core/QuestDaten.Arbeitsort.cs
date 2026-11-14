using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle E4 „Aufgaben an Gebäuden“ (S 0.53.0, Weisung P12-247, Nico 1a 2a 3c 4b): Arbeitsort einer NEUEN Quest setzen,
/// aendern und entfernen. Vorhandene Quests (Bestand) bekommen keinen (1a). Jede Aenderung ist im Projekt genau ein Schritt
/// (<see cref="Projekt.QuestArbeitsort"/>); gleicher Wert = kein Schritt. Wird der Ort gewechselt (Karte oder Ziehen in der
/// Draufsicht, 4b), springen Raum und Stelle auf die erste freie Stelle im ersten Raum.
/// </summary>
public static partial class QuestDaten
{
    /// <summary>
    /// Arbeitsort auf den Spiel-Ort <paramref name="ort"/> (Etage <paramref name="etage"/>) setzen: Raum = der erste Raum mit
    /// einer freien Stelle (im Geschaeft und am Schalter leer), Stelle = die erste freie Stelle darin; „wartet“ bleibt wie
    /// vorher (neu: ja). false = keine neue Quest, kein moeglicher Arbeitsort oder keine freie Stelle (nichts geaendert).
    /// </summary>
    public static bool ArbeitsortSetzen(PyObjekt inhalt, PyObjekt aussen, PyObjekt? innenraeume, string id, string ort, int etage = 0)
    {
        if (NeueQuest(aussen, id) is not { } q || Questebene.Arbeitsorte(inhalt, innenraeume).FirstOrDefault(w => w.Ort == ort) is not { } wahl
            || Questebene.ArbeitsGrundriss(ort, etage, inhalt, innenraeume) is not { } g)
        {
            return false;
        }
        var raeume = wahl.Art == Questebene.ArbeitsortArt.Geschaeft ? [""] : Questebene.Raeume(g).Select(r => r.Id).ToList();
        var personen = Personen(inhalt, aussen, innenraeume, q, ort, etage);
        foreach (var raum in raeume)
        {
            if (Questebene.ErsteFreieStelle(g, raum, personen) is { } stelle)
            {
                var wartet = Questebene.ArbeitsortLesen(q)?.Wartet ?? true;
                Schreiben(q, new Questebene.Arbeitsort(ort, raum, etage, stelle.X, stelle.Y, wartet));
                return true;
            }
        }
        return false;
    }

    /// <summary>Raum am Arbeitsort waehlen (Buero, Kunde): Stelle = erste freie Stelle im Raum. false = nicht moeglich.</summary>
    public static bool ArbeitsortRaum(PyObjekt inhalt, PyObjekt aussen, PyObjekt? innenraeume, string id, string raum)
    {
        if (NeueQuest(aussen, id) is not { } q || Questebene.ArbeitsortLesen(q) is not { } a
            || Questebene.ArbeitsGrundriss(a.Ort, a.Etage, inhalt, innenraeume) is not { } g
            || Questebene.ErsteFreieStelle(g, raum, Personen(inhalt, aussen, innenraeume, q, a.Ort, a.Etage)) is not { } stelle)
        {
            return false;
        }
        Schreiben(q, a with { Raum = raum, X = stelle.X, Y = stelle.Y });
        return true;
    }

    /// <summary>
    /// Stelle am Arbeitsort (Tippen im Grundriss des Geschaefts oder Schalters): die naechste Stelle, an der gearbeitet werden
    /// kann (<see cref="Questebene.Stellen"/>: erreichbar, frei von Einrichtung und Personen); im Buero und beim Kunden nur im
    /// gewaehlten Raum. false = keine solche Stelle.
    /// </summary>
    public static bool ArbeitsortStelle(PyObjekt inhalt, PyObjekt aussen, PyObjekt? innenraeume, string id, double x, double y)
    {
        if (NeueQuest(aussen, id) is not { } q || Questebene.ArbeitsortLesen(q) is not { } a
            || Questebene.ArbeitsGrundriss(a.Ort, a.Etage, inhalt, innenraeume) is not { } g)
        {
            return false;
        }
        var beste = Questebene.Stellen(g, Personen(inhalt, aussen, innenraeume, q, a.Ort, a.Etage))
            .Select(z => (z, m: Wege.Mitte(z)))
            .Where(p => a.Raum.Length == 0 || Py.Gleich(Wege.RaumBei(p.m.Item1, p.m.Item2, g)?["id"], a.Raum))
            .OrderBy(p => ((p.m.Item1 - x) * (p.m.Item1 - x)) + ((p.m.Item2 - y) * (p.m.Item2 - y))).ThenBy(p => p.z.Item1).ThenBy(p => p.z.Item2)
            .Select(p => ((double, double)?)p.m).FirstOrDefault();
        if (beste is not { } b)
        {
            return false;
        }
        Schreiben(q, a with { X = Math.Round(b.Item1, 2), Y = Math.Round(b.Item2, 2) });
        return true;
    }

    /// <summary>Schalter „Questgeber wartet am Arbeitsort“ (3c). false = keine neue Quest mit Arbeitsort.</summary>
    public static bool ArbeitsortWartet(PyObjekt aussen, string id, bool wartet)
    {
        if (NeueQuest(aussen, id) is not { } q || Questebene.ArbeitsortLesen(q) is not { } a)
        {
            return false;
        }
        Schreiben(q, a with { Wartet = wartet });
        return true;
    }

    /// <summary>Arbeitsort entfernen (die Quest gilt wieder am Platz ihres Questgebers). false = keine neue Quest.</summary>
    public static bool ArbeitsortEntfernen(PyObjekt aussen, string id)
    {
        if (NeueQuest(aussen, id) is not { } q || !Py.Hat(q, Questebene.FeldArbeitsort))
        {
            return false;
        }
        var rest = q.Where(p => p.Key != Questebene.FeldArbeitsort).ToList();
        q.Clear();
        q.AddRange(rest);
        return true;
    }

    /// <summary>Personen am Arbeitsort ohne den Questgeber der Quest (Abstand der Stelle, wie die Pruefung).</summary>
    private static List<(double X, double Y)> Personen(PyObjekt inhalt, PyObjekt aussen, PyObjekt? innenraeume, PyObjekt q, string ort, int etage) =>
        Questebene.ArbeitsortPersonen(ort, etage, inhalt, innenraeume, Aussen.Stadtdaten(aussen), Get(q, "questgeber") as string);

    /// <summary>Arbeitsort in fester Reihenfolge der Felder (ort, raum, etage, x, y, wartet) schreiben.</summary>
    private static void Schreiben(PyObjekt q, Questebene.Arbeitsort a) =>
        Py.Setzen(q, Questebene.FeldArbeitsort, new PyObjekt
        {
            { "ort", a.Ort }, { "raum", a.Raum }, { "etage", (long)a.Etage }, { "x", a.X }, { "y", a.Y }, { "wartet", a.Wartet },
        });
}
