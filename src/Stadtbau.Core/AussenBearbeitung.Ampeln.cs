using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.35.0 / A 0.60.0 (Weisung P12-166, Kurzplan P12-79 Wunsch 6): Ampeln setzen, schieben (zur naechsten Kreuzung)
/// und loeschen. Daten <c>stadtdaten.ampeln</c> = <c>[{x, y}]</c>, die Mitte liegt immer genau auf einer Kreuzung oder
/// Einmuendung (<see cref="Ampeln.Legen"/>, Fang <see cref="Ampeln.Fang"/>, nie am Kreisverkehr); die Arme kommen aus den
/// Strassen. Kopieren und Drehen gibt es nicht (eine Ampel gehoert zu genau einer Kreuzung und richtet sich selbst aus).
/// Wird eine Strasse der Kreuzung verschoben, rueckt die Ampel mit; wird sie geloescht und es bleibt keine Kreuzung, faellt
/// die Ampel weg.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Liste der Ampeln (leer, wenn es keine gibt).</summary>
    public static List<object?> AmpelListe(PyObjekt aussen) => Get(Aussen.Stadtdaten(aussen), Ampeln.Abschnitt) as List<object?> ?? [];

    private static List<object?> AmpelListeAnlegen(PyObjekt aussen)
    {
        if (Get(Aussen.Stadtdaten(aussen), Ampeln.Abschnitt) is not List<object?> l)
        {
            l = [];
            Py.Setzen(Aussen.Stadtdaten(aussen), Ampeln.Abschnitt, l);
        }
        return l;
    }

    /// <summary>Alle Ampeln als Stellen mit ihrer Mitte.</summary>
    private static IEnumerable<(AussenStelle Stelle, double X, double Y)> AlleAmpeln(PyObjekt aussen)
    {
        var liste = AmpelListe(aussen);
        for (var i = 0; i < liste.Count; i++)
        {
            if (Zebrastreifen.Lesen(liste[i]) is { } p)
            {
                yield return (new AussenStelle(AussenArt.Ampel, i), p.X, p.Y);
            }
        }
    }

    private static List<IReadOnlyList<Vector2>> AmpelStrassen(PyObjekt aussen) => [.. Strassen(aussen)];

    /// <summary>Die Ampel an der Stelle mit ihren Armen (null = steht an keiner Kreuzung).</summary>
    public static Ampel? AmpelVon(PyObjekt aussen, AussenStelle s) =>
        Zebrastreifen.Lesen(AmpelListe(aussen)[s.Nummer]) is { } p ? Ampeln.Legen(s.Nummer + 1, p, AmpelStrassen(aussen), Ampeln.AufMitte) : null;

    /// <summary>Greifweite um die Mitte einer Ampel (die ganze Kreuzung mit den Furten).</summary>
    public const float AmpelGreifen = Ampeln.FurtMitte;

    /// <summary>Eine Ampel unter dem Punkt (Kreuzung bis zu den Furten, dazu die Masten)?</summary>
    private static AussenStelle? AmpelBei(PyObjekt aussen, Vector2 p, double greifen)
    {
        var liste = AmpelListe(aussen);
        for (var i = liste.Count - 1; i >= 0; i--)
        {
            if (Zebrastreifen.Lesen(liste[i]) is not { } q)
            {
                continue;
            }
            if (Vector2.Distance(q, p) <= AmpelGreifen + (greifen / 2))
            {
                return new AussenStelle(AussenArt.Ampel, i);
            }
            if (AmpelVon(aussen, new AussenStelle(AussenArt.Ampel, i)) is { } a && a.Masten.Any(m => Vector2.Distance(m.Fuss, p) <= Ampeln.MastGrundriss + (greifen / 2)))
            {
                return new AussenStelle(AussenArt.Ampel, i);
            }
        }
        return null;
    }

    /// <summary>Wohin eine Ampel beim Setzen an (x, y) kaeme (Vorschau; null = keine Kreuzung in der Naehe oder am Kreisverkehr).</summary>
    public static Ampel? AmpelVorschau(PyObjekt aussen, double x, double y) =>
        Ampeln.Legen(0, new Vector2((float)x, (float)y), AmpelStrassen(aussen));

    /// <summary>Ampel auf die naechste Kreuzung (bis <see cref="Ampeln.Fang"/>) setzen; steht dort schon eine oder ist keine da: null.</summary>
    private static AussenStelle? AmpelSetzen(PyObjekt aussen, double x, double y)
    {
        if (AmpelVorschau(aussen, x, y) is not { } a || AmpelAn(aussen, a.Mitte) is not null)
        {
            return null;
        }
        var liste = AmpelListeAnlegen(aussen);
        liste.Add(AmpelObjekt(a.Mitte));
        return new AussenStelle(AussenArt.Ampel, liste.Count - 1);
    }

    /// <summary>Nummer der Ampel an der Kreuzung <paramref name="mitte"/> (null = keine).</summary>
    public static int? AmpelAn(PyObjekt aussen, Vector2 mitte)
    {
        var liste = AmpelListe(aussen);
        for (var i = 0; i < liste.Count; i++)
        {
            if (Zebrastreifen.Lesen(liste[i]) is { } q && Vector2.Distance(q, mitte) < 0.5f)
            {
                return i;
            }
        }
        return null;
    }

    private static PyObjekt AmpelObjekt(Vector2 m) => new() { { "x", Bearbeitung.Zahl(Math.Round(m.X, 4)) }, { "y", Bearbeitung.Zahl(Math.Round(m.Y, 4)) } };

    /// <summary>Zur naechsten freien Kreuzung um die verschobene Mitte (bis zum Fang); sonst bleibt die Ampel stehen.</summary>
    private static void AmpelVerschieben(PyObjekt aussen, AussenStelle s, double dx, double dy)
    {
        if (Zebrastreifen.Lesen(AmpelListe(aussen)[s.Nummer]) is not { } p)
        {
            return;
        }
        if (Ampeln.Legen(0, p + new Vector2((float)dx, (float)dy), AmpelStrassen(aussen)) is { } neu
            && (AmpelAn(aussen, neu.Mitte) is not { } da || da == s.Nummer))
        {
            AmpelListe(aussen)[s.Nummer] = AmpelObjekt(neu.Mitte);
        }
    }

    private static void AmpelLoeschen(PyObjekt aussen, int nummer)
    {
        var liste = AmpelListe(aussen);
        liste.RemoveAt(nummer);
        if (liste.Count == 0)
        {
            // die letzte Ampel weg: der Abschnitt faellt weg (wie vor dem ersten Setzen)
            var d = Aussen.Stadtdaten(aussen);
            var rest = d.Where(e => e.Key != Ampeln.Abschnitt).ToList();
            d.Clear();
            d.AddRange(rest);
        }
    }

    /// <summary>Nummern und Mitten der Ampeln an der Strasse <paramref name="strasse"/> (vorher bestimmen).</summary>
    private static List<(int Nummer, Vector2 Mitte)> AmpelnAuf(PyObjekt aussen, AussenStelle strasse)
    {
        var linie = Linie(Objekt(Aussen.Landschaft(aussen)[strasse.Nummer])["punkte"]);
        var liste = AmpelListe(aussen);
        var nummern = new List<(int, Vector2)>();
        for (var i = 0; i < liste.Count; i++)
        {
            if (Zebrastreifen.Lesen(liste[i]) is { } p && Kurve.Abstand(p, linie) <= 0.05f)
            {
                nummern.Add((i, p));
            }
        }
        return nummern;
    }

    /// <summary>
    /// Nach dem Schieben einer Strasse: jede ihrer Ampeln an die Kreuzung um die mitgeschobene Mitte legen, sonst an die
    /// Kreuzung an der alten Stelle; gibt es keine, bleibt sie stehen (die Pruefung meldet sie rot).
    /// </summary>
    private static void AmpelnNachlegen(PyObjekt aussen, List<(int Nummer, Vector2 Mitte)> ampeln, double dx, double dy)
    {
        var strassen = AmpelStrassen(aussen);
        var liste = AmpelListe(aussen);
        foreach (var (i, alt) in ampeln)
        {
            if ((Ampeln.Legen(0, alt + new Vector2((float)dx, (float)dy), strassen) ?? Ampeln.Legen(0, alt, strassen)) is { } neu)
            {
                liste[i] = AmpelObjekt(neu.Mitte);
            }
        }
    }

    /// <summary>Nach dem Loeschen einer Strasse: Ampeln, an deren Stelle keine Kreuzung mehr ist, fallen weg (von hinten).</summary>
    private static void AmpelnOhneKreuzungEntfernen(PyObjekt aussen, List<(int Nummer, Vector2 Mitte)> ampeln)
    {
        var strassen = AmpelStrassen(aussen);
        foreach (var (i, p) in ampeln.OrderByDescending(a => a.Nummer))
        {
            if (Ampeln.Legen(0, p, strassen, Ampeln.AufMitte) is null)
            {
                AmpelLoeschen(aussen, i);
            }
        }
    }

    private static string AmpelBeschreibung(AussenStelle s, string lage) => Texte.Ampel + " Nr. " + (s.Nummer + 1).ToString(De) + lage;
}
