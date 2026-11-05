using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitte 3 und 5): Innenraeume einer Fassung fuer das Stadtbau-Werkzeug.
/// In Fassung 0 ist ein Geschaeft in Schichten beschrieben (Grundriss in <c>innenraeume[ort]</c>, dazu die angehaengten
/// Zusaetze <c>zusatz</c>, <c>zusatz_nachtrag</c>, <c>zusatz_moebel</c>, <c>zusatz_schraenke</c>). Das Werkzeug bearbeitet
/// den fertigen Grundriss so, wie das Spiel ihn beim Betreten baut (<see cref="Grundriss"/>), und schreibt ihn beim
/// Uebernehmen flach zurueck (<see cref="MitGrundriss"/>): der Grundriss enthaelt dann alles, die Zusaetze fuer diesen
/// Ort fallen weg (nur die Plaetze der Personen aus <c>zusatz_moebel</c> bleiben). Das Spiel baut aus beidem denselben
/// Innenraum (Test <c>InnenGrundrissTests</c>). Bearbeitbar sind Geschaefte und Schalter (Laeden mit Auslage, Bank, Amt).
/// </summary>
public static class InnenGrundriss
{
    /// <summary>Zusaetze, die beim Flachschreiben fuer den Ort wegfallen (ihr Inhalt steht dann im Grundriss).</summary>
    private static readonly string[] Schichten = ["zusatz", "zusatz_nachtrag", "zusatz_schraenke"];

    /// <summary>Orte, deren Innenraum das Werkzeug bearbeitet, in der Reihenfolge der Datei.</summary>
    public static List<string> Orte(PyObjekt innenraeume) =>
        [.. innenraeume.Where(p => (Geschaefte.Laeden.Contains(p.Key) || Schalter.Orte.Contains(p.Key)) && p.Value is PyObjekt o && o["gebaeude"] is PyObjekt)
            .Select(p => p.Key)];

    /// <summary>Der fertige Grundriss eines Orts mit allen Zusaetzen (eine Kopie, wie das Spiel ihn beim Betreten baut).</summary>
    public static PyObjekt Grundriss(PyObjekt innenraeume, string ort) =>
        (PyObjekt)Py.Kopie(Innenraum.LadenGebaeude(innenraeume, ort))!;

    /// <summary>Die Personen des Orts (Verkaeufer, Schalter) mit ihrem Platz in der Darstellung.</summary>
    public static List<PyObjekt> Personen(PyObjekt innenraeume, string ort)
    {
        var plaetze = Plaetze(innenraeume, ort);
        var liste = new List<PyObjekt>();
        foreach (var p in ListeOderLeer(Objekt(innenraeume[ort]), "personen").Select(Objekt))
        {
            var kopie = (PyObjekt)Py.Kopie(p)!;
            if (plaetze.TryGetValue(Py.Str(p["id"]), out var platz))
            {
                Py.Setzen(kopie, "platz", Py.Kopie(platz));
            }
            liste.Add(kopie);
        }
        return liste;
    }

    private static Dictionary<string, object?> Plaetze(PyObjekt innenraeume, string ort)
    {
        var plaetze = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (Get(innenraeume, "zusatz_moebel") is PyObjekt zm && Get(zm, ort) is PyObjekt z && Get(z, "raeume") is PyObjekt raeume)
        {
            foreach (var (_, raum) in raeume)
            {
                if (raum is PyObjekt r && Get(r, "plaetze") is PyObjekt pl)
                {
                    foreach (var (id, platz) in pl)
                    {
                        plaetze[id] = platz;
                    }
                }
            }
        }
        return plaetze;
    }

    /// <summary>
    /// Die Innenraeume mit dem bearbeiteten Grundriss fuer <paramref name="ort"/> (Kopie, das Original bleibt): der
    /// Grundriss ersetzt den des Orts, die Schichten fuer den Ort fallen weg, aus <c>zusatz_moebel</c> bleiben nur die
    /// Plaetze der Personen. Reihenfolge der Schluessel bleibt.
    /// </summary>
    public static PyObjekt MitGrundriss(PyObjekt innenraeume, string ort, PyObjekt gebaeude)
    {
        var neu = (PyObjekt)Py.Kopie(innenraeume)!;
        Py.Setzen(Objekt(neu[ort]), "gebaeude", Py.Kopie(gebaeude));
        foreach (var schicht in Schichten)
        {
            if (Get(neu, schicht) is PyObjekt s && Py.Hat(s, ort))
            {
                Ohne(s, ort);
            }
        }
        if (Get(neu, "zusatz_moebel") is PyObjekt zm && Get(zm, ort) is PyObjekt z)
        {
            var nurPlaetze = new PyObjekt();
            if (Get(z, "raeume") is PyObjekt raeume)
            {
                var r2 = new PyObjekt();
                foreach (var (id, raum) in raeume)
                {
                    if (raum is PyObjekt r && Get(r, "plaetze") is PyObjekt pl)
                    {
                        r2.Add(id, new PyObjekt { { "plaetze", pl } });
                    }
                }
                if (r2.Count > 0)
                {
                    nurPlaetze.Add("raeume", r2);
                }
            }
            if (nurPlaetze.Count > 0)
            {
                Py.Setzen(zm, ort, nurPlaetze);
            }
            else
            {
                Ohne(zm, ort);
            }
        }
        return neu;
    }

    private static void Ohne(PyObjekt objekt, string schluessel)
    {
        var rest = objekt.Where(p => p.Key != schluessel).ToList();
        objekt.Clear();
        foreach (var (k, v) in rest)
        {
            objekt.Add(k, v);
        }
    }

    /// <summary>Alle Einrichtungsstuecke eines Grundrisses (Flur und Raeume) mit ihrem Bereich (Raum-Id oder "flur").</summary>
    public static IEnumerable<(string Bereich, PyObjekt Deko)> Einrichtung(PyObjekt gebaeude)
    {
        foreach (var d in ListeOderLeer(Objekt(gebaeude["flur"]), "deko").OfType<PyObjekt>())
        {
            yield return ("flur", d);
        }
        foreach (var raum in Liste(gebaeude["raeume"]).OfType<PyObjekt>())
        {
            foreach (var d in ListeOderLeer(raum, "deko").OfType<PyObjekt>())
            {
                yield return (Py.Str(raum["id"]), d);
            }
        }
    }
}
