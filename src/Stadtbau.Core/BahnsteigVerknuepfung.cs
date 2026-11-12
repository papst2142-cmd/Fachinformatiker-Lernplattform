using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.46.0 (Weisung P12-220, Sammelliste 324, Nico: „Ich brauche eine Funktion die es mir ermöglicht den Bahhof mit dem
/// Bahnsteig zu verknüpfen“): ein Gebaeude ohne Ort nur aus Bahnsteig-Flaechen bekommt das Feld
/// <see cref="Bahnsteig.BahnhofFeld"/> mit der Kennung des Bahnhofs (Daten in <c>karte.welt.landschaft</c>, keine neue
/// Formatnummer; das Spiel liest es: der Zug haelt dort). Ein Bahnsteig gehoert hoechstens zu einem Bahnhof (ein Feld), ein
/// Bahnhof kann mehrere Bahnsteige haben. Pruefregeln im gemeinsamen Teil (<c>StadtbauPruefung.AussehenRegeln</c>).
/// </summary>
public static class BahnsteigVerknuepfung
{
    /// <summary>Gebaeude ohne Ort an Stelle <paramref name="s"/>, wenn es ein Bahnsteig ist (nur Bahnsteig-Flaechen), sonst null.</summary>
    public static PyObjekt? BahnsteigBei(PyObjekt aussen, AussenStelle s) =>
        s.Art == AussenArt.Gebaeude && Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is PyObjekt g && Py.Gleich(g["typ"], "gebaeude")
            && Bahnsteig.Nur(Get(AussenBearbeitung.Modelle(aussen), Py.Str(g["modell"]))) ? g : null;

    /// <summary>
    /// Kann der Ort <paramref name="ort"/> einen Bahnsteig bekommen? Nicht die Orte ohne Bahnhof-Rolle
    /// (<see cref="Bahnsteig.OhneBahnsteig"/>, Stellwerk Talheim-Nord); sonst ein Ort, dessen Haus in Stadt 1 einen Bahnsteig
    /// hatte (Bahnhof Talheim, Birkenhain), oder dessen Modell ein Bahnhof ist.
    /// </summary>
    public static bool IstBahnhof(PyObjekt aussen, string ort, StadtFassung stadt1)
    {
        if (Bahnsteig.OhneBahnsteig.Contains(ort))
        {
            return false;
        }
        if (AussenBearbeitung.Orte(aussen).FirstOrDefault(o => Py.Gleich(o["id"], ort)) is not { } o)
        {
            return false;
        }
        if (ModellNamen.IstBahnhof(Py.Str(o["modell"])) || Bahnsteig.Hat(Get(AussenBearbeitung.Modelle(aussen), Py.Str(o["modell"]))))
        {
            return true;
        }
        var alt = Spielort.Stadt1Orte(stadt1).Select(x => x.Ort).FirstOrDefault(x => Py.Gleich(x["id"], ort));
        if (alt is null)
        {
            return false;
        }
        var modell = Py.Str(alt["modell"]);
        return Bahnsteig.Hat(Get(Get(stadt1.Karte, "modelle") as PyObjekt ?? [], modell) ?? Get(Get(stadt1.Stadtdaten, "neue_modelle") as PyObjekt ?? [], modell));
    }

    /// <summary>Bahnhoefe der Stadt (Kennung, Name), die einen Bahnsteig bekommen koennen.</summary>
    public static List<(string Id, string Name)> Bahnhoefe(PyObjekt aussen, StadtFassung stadt1) =>
        [.. AussenBearbeitung.Orte(aussen).Select(o => Py.Str(o["id"])).Where(id => IstBahnhof(aussen, id, stadt1))
            .Select(id => (id, Name(aussen, id)))];

    /// <summary>Name eines Orts wie auf der Karte.</summary>
    public static string Name(PyObjekt aussen, string ort) =>
        AussenBearbeitung.Orte(aussen).Where(o => Py.Gleich(o["id"], ort)).Select(o => Get(o, "name") as string).FirstOrDefault() ?? ort;

    /// <summary>Mit welchem Bahnhof ist der Bahnsteig an <paramref name="s"/> verknuepft (Kennung, sonst null)?</summary>
    public static string? BahnhofVon(PyObjekt aussen, AussenStelle s) => BahnsteigBei(aussen, s) is { } g ? Bahnsteig.BahnhofVon(g) : null;

    /// <summary>Nummern (in der Landschaft) der Bahnsteige, die mit Ort <paramref name="ort"/> verknuepft sind.</summary>
    public static List<int> BahnsteigeVon(PyObjekt aussen, string ort)
    {
        var land = Aussen.Landschaft(aussen);
        return [.. Enumerable.Range(0, land.Count).Where(i => land[i] is PyObjekt g && Bahnsteig.BahnhofVon(g) == ort)];
    }

    /// <summary>
    /// Bahnsteig an <paramref name="bahnsteig"/> mit Ort <paramref name="ort"/> verknuepfen (ersetzt eine alte Verknuepfung).
    /// Rueckgabe: Grund, warum es nicht geht, sonst null.
    /// </summary>
    public static string? Verknuepfen(PyObjekt aussen, AussenStelle bahnsteig, string ort, StadtFassung stadt1)
    {
        if (BahnsteigBei(aussen, bahnsteig) is not { } g)
        {
            return Texte.BahnsteigNurBahnsteig;
        }
        if (!IstBahnhof(aussen, ort, stadt1))
        {
            return Texte.BahnsteigKeinBahnhof(Name(aussen, ort));
        }
        Py.Setzen(g, Bahnsteig.BahnhofFeld, ort);
        return null;
    }

    /// <summary>Verknuepfung des Bahnsteigs an <paramref name="s"/> loesen; an einem Ort alle seine Bahnsteige. Rueckgabe: etwas geloest?</summary>
    public static bool Loesen(PyObjekt aussen, AussenStelle s)
    {
        if (BahnsteigBei(aussen, s) is { } g)
        {
            return g.RemoveAll(p => p.Key == Bahnsteig.BahnhofFeld) > 0;
        }
        if (s.Art == AussenArt.Haus && s.Ort.Length > 0)
        {
            var geloest = false;
            foreach (var i in BahnsteigeVon(aussen, s.Ort))
            {
                geloest |= Objekt(Aussen.Landschaft(aussen)[i]).RemoveAll(p => p.Key == Bahnsteig.BahnhofFeld) > 0;
            }
            return geloest;
        }
        return false;
    }

    /// <summary>
    /// Gegenstueck beim Tippen: ein Tipp an Welt-Punkt (x, y) trifft einen Ort (Bahnhof) bzw. einen Bahnsteig. Rueckgabe: die Stelle
    /// des getroffenen Stuecks (nur passende Art), sonst null.
    /// </summary>
    public static AussenStelle? Treffer(PyObjekt aussen, double x, double y, bool sucheBahnsteig, StadtFassung stadt1)
    {
        var s = AussenBearbeitung.StueckBei(aussen, x, y);
        if (s is null)
        {
            return null;
        }
        if (sucheBahnsteig)
        {
            return BahnsteigBei(aussen, s) is not null ? s : null;
        }
        return s.Art == AussenArt.Haus && s.Ort.Length > 0 && IstBahnhof(aussen, s.Ort, stadt1) ? s : null;
    }
}
