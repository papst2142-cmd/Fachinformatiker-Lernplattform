using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.36.0 / A 0.61.0 (Weisung P12-171, Kurzplan P12-102): Bushaltestellen und Parkplaetze setzen, laengs schieben,
/// kopieren und loeschen. Daten <c>stadtdaten.haltestellen</c> = <c>[{x, y}]</c> und <c>stadtdaten.parkplaetze</c> =
/// <c>[{x, y, plaetze, reihen}]</c>: ein Punkt auf der Seite, das Spiel legt ihn auf die naechste gerade Strasse mit Gehweg
/// (<see cref="Haltestellen.Legen"/>, <see cref="Parkplaetze.Legen"/>). Gespeichert wird der Punkt
/// <see cref="Seitenpunkt"/> neben der Strassenmitte auf der Seite (so bleibt die Seite beim Laden gleich). Drehen gibt es
/// nicht (beide richten sich nach der Strasse). Wird die Strasse verschoben, wandern sie mit; wird sie geloescht, fallen sie weg.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>So weit neben der Strassenmitte liegt der gespeicherte Punkt (auf dem Gehweg der Seite).</summary>
    public const float Seitenpunkt = 0.5f;

    /// <summary>Plaetze und Reihen eines neu gesetzten Parkplatzes.</summary>
    public const int ParkplatzPlaetzeNeu = 6, ParkplatzReihenNeu = 1;

    /// <summary>Kopie einer Haltestelle: so weit laengs daneben.</summary>
    public const float HaltestelleKopieVersatz = 3f;

    private static string HaltAbschnitt(AussenArt art) => art == AussenArt.Haltestelle ? Haltestellen.Abschnitt : Parkplaetze.Abschnitt;

    /// <summary>Liste der Haltestellen bzw. Parkplaetze (leer, wenn es keine gibt).</summary>
    public static List<object?> HaltListe(PyObjekt aussen, AussenArt art) => Get(Aussen.Stadtdaten(aussen), HaltAbschnitt(art)) as List<object?> ?? [];

    private static List<object?> HaltListeAnlegen(PyObjekt aussen, AussenArt art)
    {
        if (Get(Aussen.Stadtdaten(aussen), HaltAbschnitt(art)) is not List<object?> l)
        {
            l = [];
            Py.Setzen(Aussen.Stadtdaten(aussen), HaltAbschnitt(art), l);
        }
        return l;
    }

    /// <summary>Strassen mit Gehweg (Linie, halbe Breite bis zur Gehwegkante), auf die Haltestellen und Parkplaetze gelegt werden.</summary>
    public static List<(List<Vector2> Linie, float Halb)> Gehwege(PyObjekt aussen) =>
        Haltestellen.Gehwege(Aussen.Landschaft(aussen).OfType<PyObjekt>());

    /// <summary>Die Haltestelle an der Stelle (null = liegt an keiner geraden Strasse mit Gehweg).</summary>
    public static Haltestelle? HaltestelleVon(PyObjekt aussen, AussenStelle s) =>
        s.Nummer < HaltListe(aussen, AussenArt.Haltestelle).Count && Zebrastreifen.Lesen(HaltListe(aussen, AussenArt.Haltestelle)[s.Nummer]) is { } p
            ? Haltestellen.Legen(s.Nummer + 1, p, Gehwege(aussen)) : null;

    /// <summary>Der Parkplatz an der Stelle (null = falsch gespeichert oder an keiner geraden Strasse mit Gehweg).</summary>
    public static Parkplatz? ParkplatzVon(PyObjekt aussen, AussenStelle s) =>
        s.Nummer < HaltListe(aussen, AussenArt.Parkplatz).Count && Parkplaetze.Lesen(HaltListe(aussen, AussenArt.Parkplatz)[s.Nummer]) is var (p, n, r)
            ? Parkplaetze.Legen(s.Nummer + 1, p, n, r, Gehwege(aussen)) : null;

    /// <summary>Alle Haltestellen und Parkplaetze als Stellen mit ihrem Mittelpunkt (Haltestelle: Haeuschen; Parkplatz: Flaeche).</summary>
    private static IEnumerable<(AussenStelle Stelle, double X, double Y)> AlleHalte(PyObjekt aussen)
    {
        foreach (var art in (AussenArt[])[AussenArt.Haltestelle, AussenArt.Parkplatz])
        {
            var liste = HaltListe(aussen, art);
            for (var i = 0; i < liste.Count; i++)
            {
                var s = new AussenStelle(art, i);
                if (HaltMitte(aussen, s) is { } m)
                {
                    yield return (s, m.X, m.Y);
                }
                else if (Zebrastreifen.Lesen(liste[i]) is { } p)
                {
                    yield return (s, p.X, p.Y);
                }
            }
        }
    }

    /// <summary>Mitte zum Anzeigen und Waehlen (Haltestelle: Haeuschen; Parkplatz: Mitte der Flaeche).</summary>
    public static Vector2? HaltMitte(PyObjekt aussen, AussenStelle s) => s.Art switch
    {
        AussenArt.Haltestelle => HaltestelleVon(aussen, s)?.Haeuschen,
        AussenArt.Parkplatz when ParkplatzVon(aussen, s) is { } pp => pp.Punkt(0, pp.Tiefe / 2),
        _ => null,
    };

    /// <summary>Umriss zum Waehlen und als Schatten: Haltestelle = Haeuschen bis zur Fahrspur, Parkplatz = Flaeche mit Einfahrt.</summary>
    public static Vector2[]? HaltUmriss(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art == AussenArt.Haltestelle && HaltestelleVon(aussen, s) is { } h)
        {
            var l = h.Fahrt * (Haltestellen.ZickzackLang / 2);
            var c = h.Mitte + (h.Fahrt * (Haltestellen.SchildVor - (Haltestellen.ZickzackLang / 2)));
            var innen = c + (h.Aussen * 0.02f);
            var aussenKante = c + (h.Aussen * (h.Halb + Haltestellen.HaeuschenAbstand + Haltestellen.HaeuschenTief));
            return [innen - l, innen + l, aussenKante + l, aussenKante - l];
        }
        if (s.Art == AussenArt.Parkplatz && ParkplatzVon(aussen, s) is { } pp)
        {
            var e = pp.Einfahrt.A;
            var halbGasse = pp.Laengs * (Parkplaetze.Gasse / 2);
            return [e - halbGasse, pp.Punkt(-pp.Breite / 2, 0), pp.Punkt(-pp.Breite / 2, pp.Tiefe), pp.Punkt(pp.Breite / 2, pp.Tiefe),
                pp.Punkt(pp.Breite / 2, 0), e + halbGasse];
        }
        return null;
    }

    /// <summary>Eine Haltestelle oder ein Parkplatz unter dem Punkt (im Umriss oder bis zur halben Greifweite daneben)?</summary>
    private static AussenStelle? HaltBei(PyObjekt aussen, Vector2 p, double greifen, Func<AussenArt, bool> an)
    {
        foreach (var art in (AussenArt[])[AussenArt.Haltestelle, AussenArt.Parkplatz])
        {
            if (!an(art))
            {
                continue;
            }
            var liste = HaltListe(aussen, art);
            for (var i = liste.Count - 1; i >= 0; i--)
            {
                var s = new AussenStelle(art, i);
                if (HaltUmriss(aussen, s) is { } u && (Wasser.Drin(u, p) || Kurve.Abstand(p, [.. u, u[0]]) <= greifen / 2))
                {
                    return s;
                }
            }
        }
        return null;
    }

    /// <summary>Wohin eine Haltestelle beim Setzen an (x, y) kaeme (Vorschau; null = keine gerade Strasse mit Gehweg in der Naehe).</summary>
    public static Haltestelle? HaltestelleVorschau(PyObjekt aussen, double x, double y) =>
        Haltestellen.Legen(0, new Vector2((float)x, (float)y), Gehwege(aussen));

    /// <summary>Wohin ein Parkplatz beim Setzen an (x, y) kaeme (Vorschau).</summary>
    public static Parkplatz? ParkplatzVorschau(PyObjekt aussen, double x, double y, int plaetze = ParkplatzPlaetzeNeu, int reihen = ParkplatzReihenNeu) =>
        Parkplaetze.Legen(0, new Vector2((float)x, (float)y), plaetze, reihen, Gehwege(aussen));

    /// <summary>Alle Haltestellen und Parkplaetze zum Zeichnen; der gewaehlte beim Ziehen um (dx, dy) laengs verschoben (wie <see cref="HaltVerschieben"/>).</summary>
    public static List<(AussenStelle Stelle, Haltestelle? Halt, Parkplatz? Park)> HalteZumZeichnen(PyObjekt aussen, AussenStelle? auswahl, double dx, double dy)
    {
        var liste = new List<(AussenStelle, Haltestelle?, Parkplatz?)>();
        var gehwege = Gehwege(aussen);
        foreach (var art in (AussenArt[])[AussenArt.Haltestelle, AussenArt.Parkplatz])
        {
            var daten = HaltListe(aussen, art);
            for (var i = 0; i < daten.Count; i++)
            {
                var s = new AussenStelle(art, i);
                var p = Zebrastreifen.Lesen(daten[i]);
                if (p is null)
                {
                    continue;
                }
                var punkt = p.Value;
                if (s == auswahl && (dx != 0 || dy != 0) && HaltLage(aussen, s) is { } lage)
                {
                    var neu = punkt + (lage.Richtung * Vector2.Dot(new Vector2((float)dx, (float)dy), lage.Richtung));
                    var geht = art == AussenArt.Haltestelle ? Haltestellen.Legen(0, neu, gehwege) is not null
                        : Parkplaetze.Lesen(daten[i]) is var (_, n0, r0) && Parkplaetze.Legen(0, neu, n0, r0, gehwege) is not null;
                    punkt = geht ? neu : punkt;
                }
                if (art == AussenArt.Haltestelle)
                {
                    liste.Add((s, Haltestellen.Legen(i + 1, punkt, gehwege), null));
                }
                else if (Parkplaetze.Lesen(daten[i]) is var (_, n, r))
                {
                    liste.Add((s, null, Parkplaetze.Legen(i + 1, punkt, n, r, gehwege)));
                }
            }
        }
        return liste;
    }

    private static PyObjekt HaltObjekt(Vector2 mitte, Vector2 aussenRichtung)
    {
        var p = mitte + (aussenRichtung * Seitenpunkt);
        return new() { { "x", Bearbeitung.Zahl(Math.Round(p.X, 4)) }, { "y", Bearbeitung.Zahl(Math.Round(p.Y, 4)) } };
    }

    private static PyObjekt ParkObjekt(Vector2 mitte, Vector2 aussenRichtung, int plaetze, int reihen)
    {
        var o = HaltObjekt(mitte, aussenRichtung);
        Py.Setzen(o, "plaetze", (long)plaetze);
        Py.Setzen(o, "reihen", (long)reihen);
        return o;
    }

    /// <summary>Haltestelle bzw. Parkplatz an die naechste gerade Strasse mit Gehweg setzen; keine da: null.</summary>
    private static AussenStelle? HaltSetzen(PyObjekt aussen, AussenArt art, double x, double y)
    {
        PyObjekt eintrag;
        if (art == AussenArt.Haltestelle)
        {
            if (HaltestelleVorschau(aussen, x, y) is not { } h)
            {
                return null;
            }
            eintrag = HaltObjekt(h.Mitte, h.Aussen);
        }
        else
        {
            if (ParkplatzVorschau(aussen, x, y) is not { } pp)
            {
                return null;
            }
            eintrag = ParkObjekt(pp.Vorn - (pp.Aussen * pp.Halb), pp.Aussen, pp.Plaetze, pp.Reihen);
        }
        var liste = HaltListeAnlegen(aussen, art);
        liste.Add(eintrag);
        return new AussenStelle(art, liste.Count - 1);
    }

    /// <summary>Lotfuss auf der Strassenmitte und Seite des Eintrags (null = liegt an keiner Strasse).</summary>
    private static (Vector2 Mitte, Vector2 Richtung, Vector2 Aussen)? HaltLage(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art == AussenArt.Haltestelle && HaltestelleVon(aussen, s) is { } h)
        {
            return (h.Mitte, h.Richtung, h.Aussen);
        }
        if (s.Art == AussenArt.Parkplatz && ParkplatzVon(aussen, s) is { } pp)
        {
            return (pp.Vorn - (pp.Aussen * pp.Halb), pp.Laengs, pp.Aussen);
        }
        return null;
    }

    /// <summary>Nur laengs der Strasse schieben (der Anteil quer zaehlt nicht); kaeme er von der Strasse, bleibt er stehen.</summary>
    private static void HaltVerschieben(PyObjekt aussen, AussenStelle s, double dx, double dy)
    {
        if (HaltLage(aussen, s) is not { } lage)
        {
            return;
        }
        var t = Vector2.Dot(new Vector2((float)dx, (float)dy), lage.Richtung);
        HaltNeuLegen(aussen, s, lage.Mitte + (lage.Richtung * t), lage.Aussen);
    }

    /// <summary>Den Eintrag an die Mitte <paramref name="mitte"/> (Seite <paramref name="aussenRichtung"/>) legen, wenn er dort an einer Strasse liegt.</summary>
    private static bool HaltNeuLegen(PyObjekt aussen, AussenStelle s, Vector2 mitte, Vector2 aussenRichtung)
    {
        var liste = HaltListe(aussen, s.Art);
        var p = mitte + (aussenRichtung * Seitenpunkt);
        if (s.Art == AussenArt.Haltestelle)
        {
            if (Haltestellen.Legen(0, p, Gehwege(aussen)) is not { } h)
            {
                return false;
            }
            liste[s.Nummer] = HaltObjekt(h.Mitte, h.Aussen);
            return true;
        }
        if (Parkplaetze.Lesen(liste[s.Nummer]) is not var (_, n, r) || Parkplaetze.Legen(0, p, n, r, Gehwege(aussen)) is not { } pp)
        {
            return false;
        }
        liste[s.Nummer] = ParkObjekt(pp.Vorn - (pp.Aussen * pp.Halb), pp.Aussen, n, r);
        return true;
    }

    /// <summary>Kopie laengs daneben (Haltestelle 3 weiter, Parkplatz um seine Breite und 0,5); geht das nicht, auf der anderen Seite der Laenge nach.</summary>
    private static AussenStelle? HaltKopieren(PyObjekt aussen, AussenStelle s)
    {
        if (HaltLage(aussen, s) is not { } lage)
        {
            return null;
        }
        var weit = s.Art == AussenArt.Haltestelle ? HaltestelleKopieVersatz : (ParkplatzVon(aussen, s)?.Breite ?? 0) + 0.5f;
        var liste = HaltListe(aussen, s.Art);
        foreach (var richtung in (float[])[1f, -1f])
        {
            liste.Add(Py.Kopie(liste[s.Nummer]));
            var k = new AussenStelle(s.Art, liste.Count - 1);
            if (HaltNeuLegen(aussen, k, lage.Mitte + (lage.Richtung * (weit * richtung)), lage.Aussen))
            {
                return k;
            }
            liste.RemoveAt(liste.Count - 1);
        }
        return null;
    }

    /// <summary>Plaetze und Reihen eines Parkplatzes setzen (nur gueltige Werte; ein Schritt im Verlauf ueber <see cref="Projekt.AussenAendern"/>).</summary>
    public static bool ParkplatzAendern(PyObjekt aussen, AussenStelle s, int plaetze, int reihen)
    {
        if (s.Art != AussenArt.Parkplatz || !Parkplaetze.Gueltig(plaetze, reihen) || s.Nummer >= HaltListe(aussen, s.Art).Count
            || HaltListe(aussen, s.Art)[s.Nummer] is not PyObjekt o)
        {
            return false;
        }
        Py.Setzen(o, "plaetze", (long)plaetze);
        Py.Setzen(o, "reihen", (long)reihen);
        return true;
    }

    private static void HaltLoeschen(PyObjekt aussen, AussenStelle s)
    {
        var liste = HaltListe(aussen, s.Art);
        liste.RemoveAt(s.Nummer);
        if (liste.Count == 0)
        {
            // der letzte weg: der Abschnitt faellt weg (wie vor dem ersten Setzen)
            var d = Aussen.Stadtdaten(aussen);
            var rest = d.Where(e => e.Key != HaltAbschnitt(s.Art)).ToList();
            d.Clear();
            d.AddRange(rest);
        }
    }

    /// <summary>Haltestellen und Parkplaetze an der Strasse <paramref name="strasse"/> (vorher bestimmen, solange sie daran liegen).</summary>
    private static List<AussenStelle> HalteAuf(PyObjekt aussen, AussenStelle strasse)
    {
        var linie = Linie(Objekt(Aussen.Landschaft(aussen)[strasse.Nummer])["punkte"]);
        var stellen = new List<AussenStelle>();
        foreach (var art in (AussenArt[])[AussenArt.Haltestelle, AussenArt.Parkplatz])
        {
            for (var i = 0; i < HaltListe(aussen, art).Count; i++)
            {
                var s = new AussenStelle(art, i);
                if (HaltLage(aussen, s) is { } lage && Kurve.Abstand(lage.Mitte, linie) <= 0.05f)
                {
                    stellen.Add(s);
                }
            }
        }
        return stellen;
    }

    /// <summary>Nach dem Schieben einer Strasse: ihre Haltestellen und Parkplaetze um denselben Weg mitnehmen.</summary>
    private static void HalteMitnehmen(PyObjekt aussen, List<AussenStelle> halte, double dx, double dy)
    {
        foreach (var s in halte)
        {
            if (Zebrastreifen.Lesen(HaltListe(aussen, s.Art)[s.Nummer]) is { } p && HaltListe(aussen, s.Art)[s.Nummer] is PyObjekt o)
            {
                Py.Setzen(o, "x", Bearbeitung.Zahl(Math.Round(p.X + dx, 4)));
                Py.Setzen(o, "y", Bearbeitung.Zahl(Math.Round(p.Y + dy, 4)));
            }
        }
    }

    /// <summary>Gedreht mit einer Gruppe: der gespeicherte Punkt dreht mit (danach liegt er an der gedrehten Strasse).</summary>
    private static void HaltPunktSetzen(PyObjekt aussen, AussenStelle s, Vector2 p)
    {
        if (s.Nummer < HaltListe(aussen, s.Art).Count && HaltListe(aussen, s.Art)[s.Nummer] is PyObjekt o)
        {
            Py.Setzen(o, "x", Bearbeitung.Zahl(Math.Round(p.X, 4)));
            Py.Setzen(o, "y", Bearbeitung.Zahl(Math.Round(p.Y, 4)));
        }
    }

    /// <summary>Gespeicherter Punkt (null = falsch gespeichert).</summary>
    private static Vector2? HaltPunkt(PyObjekt aussen, AussenStelle s) =>
        s.Nummer < HaltListe(aussen, s.Art).Count ? Zebrastreifen.Lesen(HaltListe(aussen, s.Art)[s.Nummer]) : null;

    /// <summary>Nach dem Loeschen einer Strasse: Eintraege, die an keiner Strasse mit Gehweg mehr liegen, fallen weg (von hinten).</summary>
    private static void HalteOhneStrasseEntfernen(PyObjekt aussen, List<AussenStelle> halte)
    {
        foreach (var s in halte.OrderByDescending(h => h.Art).ThenByDescending(h => h.Nummer))
        {
            if (HaltLage(aussen, s) is null)
            {
                HaltLoeschen(aussen, s);
            }
        }
    }

    private static string HaltBeschreibung(PyObjekt aussen, AussenStelle s, string lage)
    {
        if (s.Art == AussenArt.Haltestelle)
        {
            return Texte.Haltestelle + " Nr. " + (s.Nummer + 1).ToString(De) + lage;
        }
        var text = Texte.Parkplatz + " Nr. " + (s.Nummer + 1).ToString(De);
        if (Parkplaetze.Lesen(HaltListe(aussen, s.Art)[s.Nummer]) is var (_, n, r))
        {
            text += ", " + Texte.ParkplatzPlaetzeText(n, r);
        }
        return text + lage;
    }
}
