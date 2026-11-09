using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.26.0 / A 0.54.0 (Weisung P12-115, Meldung P12-75 Abschnitt 4, Sammelliste 255): Zebrastreifen setzen,
/// schieben (nur laengs der Strasse), kopieren und loeschen. Daten <c>stadtdaten.zebrastreifen</c> = <c>[{x, y}]</c>, die
/// Mitte liegt immer genau auf der Mitte einer geraden Strasse (<see cref="Zebrastreifen.Legen"/>, Fang
/// <see cref="Zebrastreifen.Fang"/>); die Richtung kommt aus der Strasse. Wird die Strasse verschoben, wandert der
/// Zebrastreifen mit; wird sie geloescht, faellt er weg.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>So weit liegt eine Kopie laengs der Strasse neben dem Original.</summary>
    public const double ZebraKopieVersatz = 2.5;

    /// <summary>Liste der Zebrastreifen (leer, wenn es keine gibt).</summary>
    public static List<object?> ZebraListe(PyObjekt aussen) => Get(Aussen.Stadtdaten(aussen), Zebrastreifen.Abschnitt) as List<object?> ?? [];

    private static List<object?> ZebraListeAnlegen(PyObjekt aussen)
    {
        if (Get(Aussen.Stadtdaten(aussen), Zebrastreifen.Abschnitt) is not List<object?> l)
        {
            l = [];
            Py.Setzen(Aussen.Stadtdaten(aussen), Zebrastreifen.Abschnitt, l);
        }
        return l;
    }

    /// <summary>Alle Zebrastreifen als Stellen mit ihrer Mitte.</summary>
    private static IEnumerable<(AussenStelle Stelle, double X, double Y)> AlleZebras(PyObjekt aussen)
    {
        var liste = ZebraListe(aussen);
        for (var i = 0; i < liste.Count; i++)
        {
            if (Zebrastreifen.Lesen(liste[i]) is { } p)
            {
                yield return (new AussenStelle(AussenArt.Zebrastreifen, i), p.X, p.Y);
            }
        }
    }

    /// <summary>Gerade Strassen fuer das Einrasten (ohne Ring; Kurven haben mehr als zwei Punkte).</summary>
    private static List<IReadOnlyList<Vector2>> ZebraStrassen(PyObjekt aussen) => [.. Strassen(aussen)];

    /// <summary>Der Zebrastreifen an der Stelle mit Richtung (null = liegt auf keiner geraden Strasse).</summary>
    public static Zebra? ZebraVon(PyObjekt aussen, AussenStelle s) =>
        Zebrastreifen.Lesen(ZebraListe(aussen)[s.Nummer]) is { } p ? Zebrastreifen.Legen(s.Nummer + 1, p, ZebraStrassen(aussen), Zebrastreifen.Fang) : null;

    /// <summary>Ein Zebrastreifen unter dem Punkt (Flaeche quer ueber die Fahrbahn, etwas Rand)?</summary>
    private static AussenStelle? ZebraBei(PyObjekt aussen, Vector2 p, double greifen)
    {
        var strassen = ZebraStrassen(aussen);
        var liste = ZebraListe(aussen);
        for (var i = liste.Count - 1; i >= 0; i--)
        {
            if (Zebrastreifen.Lesen(liste[i]) is { } q && Zebrastreifen.Legen(i + 1, q, strassen) is { } z && z.Drauf(p, (float)greifen / 2, (float)greifen / 2))
            {
                return new AussenStelle(AussenArt.Zebrastreifen, i);
            }
        }
        return null;
    }

    /// <summary>Zebrastreifen quer auf die naechste gerade Strasse (bis <see cref="Zebrastreifen.Fang"/>) setzen; sonst null.</summary>
    private static AussenStelle? ZebraSetzen(PyObjekt aussen, double x, double y)
    {
        if (Zebrastreifen.Legen(0, new Vector2((float)x, (float)y), ZebraStrassen(aussen)) is not { } z)
        {
            return null;
        }
        var liste = ZebraListeAnlegen(aussen);
        liste.Add(ZebraObjekt(z.Mitte));
        return new AussenStelle(AussenArt.Zebrastreifen, liste.Count - 1);
    }

    /// <summary>Wohin ein Zebrastreifen beim Setzen an (x, y) kaeme (Vorschau; null = keine gerade Strasse in der Naehe).</summary>
    public static Zebra? ZebraVorschau(PyObjekt aussen, double x, double y) =>
        Zebrastreifen.Legen(0, new Vector2((float)x, (float)y), ZebraStrassen(aussen));

    private static PyObjekt ZebraObjekt(Vector2 m) => new() { { "x", Bearbeitung.Zahl(Math.Round(m.X, 4)) }, { "y", Bearbeitung.Zahl(Math.Round(m.Y, 4)) } };

    /// <summary>Nur laengs der Strasse verschieben (der Anteil quer zur Strasse faellt weg, am Ende der Strasse bleibt er stehen).</summary>
    private static void ZebraVerschieben(PyObjekt aussen, AussenStelle s, double dx, double dy)
    {
        if (ZebraVon(aussen, s) is not { } z)
        {
            return;
        }
        var laengs = Vector2.Dot(new Vector2((float)dx, (float)dy), z.Richtung);
        var ziel = z.Mitte + (z.Richtung * laengs);
        if (Zebrastreifen.Legen(0, ziel, ZebraStrassen(aussen), 0.01f) is { } neu)
        {
            ZebraListe(aussen)[s.Nummer] = ZebraObjekt(neu.Mitte);
        }
    }

    /// <summary>Kopie laengs der Strasse daneben (passt dort keine hin: auf der anderen Seite; sonst null).</summary>
    private static AussenStelle? ZebraKopieren(PyObjekt aussen, AussenStelle s)
    {
        if (ZebraVon(aussen, s) is not { } z)
        {
            return null;
        }
        foreach (var richtung in (float[])[1, -1])
        {
            var ziel = z.Mitte + (z.Richtung * (float)(ZebraKopieVersatz * richtung));
            if (Zebrastreifen.Legen(0, ziel, ZebraStrassen(aussen), 0.01f) is { } neu)
            {
                ZebraListe(aussen).Add(ZebraObjekt(neu.Mitte));
                return new AussenStelle(AussenArt.Zebrastreifen, ZebraListe(aussen).Count - 1);
            }
        }
        return null;
    }

    private static void ZebraLoeschen(PyObjekt aussen, int nummer)
    {
        var liste = ZebraListe(aussen);
        liste.RemoveAt(nummer);
        if (liste.Count == 0)
        {
            // der letzte Zebrastreifen weg: der Abschnitt faellt weg (wie vor dem ersten Setzen)
            var d = Aussen.Stadtdaten(aussen);
            var rest = d.Where(e => e.Key != Zebrastreifen.Abschnitt).ToList();
            d.Clear();
            d.AddRange(rest);
        }
    }

    /// <summary>Nummern der Zebrastreifen auf der geraden Strasse (vorher bestimmen, solange sie dort liegen).</summary>
    private static List<int> ZebrasAuf(PyObjekt aussen, AussenStelle strasse)
    {
        var linie = Linie(Objekt(Aussen.Landschaft(aussen)[strasse.Nummer])["punkte"]);
        var liste = ZebraListe(aussen);
        var nummern = new List<int>();
        if (linie.Count != 2)
        {
            return nummern;
        }
        for (var i = 0; i < liste.Count; i++)
        {
            if (Zebrastreifen.Lesen(liste[i]) is { } p && Kurve.Abstand(p, linie) <= 0.02f)
            {
                nummern.Add(i);
            }
        }
        return nummern;
    }

    /// <summary>Zebrastreifen einer verschobenen Strasse mitnehmen.</summary>
    private static void ZebrasMitnehmen(PyObjekt aussen, List<int> nummern, double dx, double dy)
    {
        var liste = ZebraListe(aussen);
        foreach (var i in nummern)
        {
            if (Zebrastreifen.Lesen(liste[i]) is { } p)
            {
                liste[i] = ZebraObjekt(p + new Vector2((float)dx, (float)dy));
            }
        }
    }

    /// <summary>Nach dem Einrasten der Strasse die Zebrastreifen wieder genau auf ihre Mitte legen (bis zum Fang).</summary>
    private static void ZebrasNachlegen(PyObjekt aussen, List<int> nummern)
    {
        var liste = ZebraListe(aussen);
        foreach (var i in nummern)
        {
            if (Zebrastreifen.Lesen(liste[i]) is { } p && Zebrastreifen.Legen(0, p, ZebraStrassen(aussen)) is { } z)
            {
                liste[i] = ZebraObjekt(z.Mitte);
            }
        }
    }

    /// <summary>Zebrastreifen einer geloeschten Strasse entfernen (von hinten, damit die Nummern stimmen).</summary>
    private static void ZebrasEntfernen(PyObjekt aussen, List<int> nummern)
    {
        foreach (var i in nummern.OrderDescending())
        {
            ZebraLoeschen(aussen, i);
        }
    }

    private static string ZebraBeschreibung(AussenStelle s, string lage) => Texte.Zebrastreifen + " Nr. " + (s.Nummer + 1).ToString(De) + lage;
}
