using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.27.0 (Weisung P12-118 Teil B, Kurzplan Meldung P12-79 Wunsch 5): Notizen auf der Karte, nur zum Planen.
/// Setzen, Text aendern, verschieben und loeschen sind je ein Schritt im Verlauf. Die Notizen stehen im Teil
/// <see cref="NotizTeil"/> der Stadt draussen (<c>[{x, y, text}]</c>) und damit nur in der Projektdatei (wie
/// <see cref="LaternenAutomatik.Teil"/>): <see cref="Aussen.Fassung"/> nimmt nur Karte, Stadtdaten, Laternen und Beete,
/// also kommen sie nie in die Stadt-Datei und nie ins Spiel. Zu sehen nur in der Draufsicht (in 3D nicht anwaehlbar).
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Teil der Stadt draussen mit den Notizen (nur Werkzeug).</summary>
    public const string NotizTeil = "notizen";

    /// <summary>Hoechstens so viele Zeichen hat eine Notiz.</summary>
    public const int NotizHoechstens = 300;

    /// <summary>So weit liegt eine Kopie neben dem Original.</summary>
    public const double NotizKopieVersatz = 1.0;

    /// <summary>Halbe Seite des Notiz-Zeichens in Karten-Einheiten (zum Greifen und fuer die Draufsicht).</summary>
    public const double NotizHalb = 0.35;

    /// <summary>Arten, die nur in der Draufsicht zu sehen sind (in der 3D-Ansicht nie anwaehlbar).</summary>
    public static readonly IReadOnlySet<AussenArt> NurDraufsicht = new HashSet<AussenArt> { AussenArt.Notiz };

    /// <summary>Liste der Notizen (leer, wenn es keine gibt).</summary>
    public static List<object?> NotizListe(PyObjekt aussen) => Get(aussen, NotizTeil) as List<object?> ?? [];

    private static List<object?> NotizListeAnlegen(PyObjekt aussen)
    {
        if (Get(aussen, NotizTeil) is not List<object?> l)
        {
            l = [];
            Py.Setzen(aussen, NotizTeil, l);
        }
        return l;
    }

    /// <summary>Text der Notiz an der Stelle.</summary>
    public static string NotizText(PyObjekt aussen, AussenStelle s) => Objekt(NotizListe(aussen)[s.Nummer])["text"] as string ?? "";

    /// <summary>Alle Notizen als Stellen mit ihrer Mitte.</summary>
    private static IEnumerable<(AussenStelle Stelle, double X, double Y)> AlleNotizen(PyObjekt aussen)
    {
        var liste = NotizListe(aussen);
        for (var i = 0; i < liste.Count; i++)
        {
            if (liste[i] is PyObjekt n && Py.Hat(n, "x") && Py.Hat(n, "y"))
            {
                yield return (new AussenStelle(AussenArt.Notiz, i), Py.F(n["x"]), Py.F(n["y"]));
            }
        }
    }

    /// <summary>Die oberste Notiz unter dem Punkt (Zeichen plus Greifradius), sonst null.</summary>
    private static AussenStelle? NotizBei(PyObjekt aussen, Vector2 p, double greifen)
    {
        var weit = NotizHalb + Math.Max(0, greifen - NotizHalb);
        foreach (var (s, x, y) in AlleNotizen(aussen).Reverse())
        {
            if (Math.Abs(p.X - x) <= weit && Math.Abs(p.Y - y) <= weit)
            {
                return s;
            }
        }
        return null;
    }

    /// <summary>Warum der Text nicht geht (null = er geht): leer oder zu lang.</summary>
    public static string? NotizTextNicht(string text) =>
        text.Trim().Length == 0 ? Texte.NotizLeer : text.Trim().Length > NotizHoechstens ? Texte.NotizZuLang : null;

    /// <summary>Neue Notiz mit dem Text <see cref="Texte.NotizNeu"/> (Mitte auf dem Raster); ausserhalb der Karte: null.</summary>
    private static AussenStelle NotizSetzen(PyObjekt aussen, double x, double y, bool raster)
    {
        var r = raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        var liste = NotizListeAnlegen(aussen);
        liste.Add(new PyObjekt
        {
            { "x", Bearbeitung.Zahl(Bearbeitung.AufRaster(x, r)) },
            { "y", Bearbeitung.Zahl(Bearbeitung.AufRaster(y, r)) },
            { "text", Texte.NotizNeu },
        });
        return new AussenStelle(AussenArt.Notiz, liste.Count - 1);
    }

    /// <summary>Text der Notiz setzen (vorne und hinten ohne Leerzeichen); null = gesetzt, sonst der Grund (nichts geaendert).</summary>
    public static string? NotizTextSetzen(PyObjekt aussen, AussenStelle s, string text)
    {
        if (NotizTextNicht(text) is { } grund)
        {
            return grund;
        }
        Py.Setzen(Objekt(NotizListe(aussen)[s.Nummer]), "text", text.Trim());
        return null;
    }

    private static void NotizVerschieben(PyObjekt aussen, AussenStelle s, double dx, double dy)
    {
        var n = Objekt(NotizListe(aussen)[s.Nummer]);
        Py.Setzen(n, "x", Bearbeitung.Zahl(Py.F(n["x"]) + dx));
        Py.Setzen(n, "y", Bearbeitung.Zahl(Py.F(n["y"]) + dy));
    }

    private static AussenStelle NotizKopieren(PyObjekt aussen, AussenStelle s)
    {
        var liste = NotizListe(aussen);
        var kopie = (PyObjekt)Py.Kopie(liste[s.Nummer])!;
        Py.Setzen(kopie, "x", Bearbeitung.Zahl(Py.F(kopie["x"]) + NotizKopieVersatz));
        liste.Add(kopie);
        return new AussenStelle(AussenArt.Notiz, liste.Count - 1);
    }

    private static void NotizLoeschen(PyObjekt aussen, int nummer)
    {
        var liste = NotizListe(aussen);
        liste.RemoveAt(nummer);
        if (liste.Count == 0)
        {
            // die letzte Notiz weg: der Teil faellt weg (wie vor der ersten Notiz)
            var rest = aussen.Where(e => e.Key != NotizTeil).ToList();
            aussen.Clear();
            aussen.AddRange(rest);
        }
    }

    private static string NotizBeschreibung(PyObjekt aussen, AussenStelle s, string lage)
    {
        var text = NotizText(aussen, s).ReplaceLineEndings(" ");
        return Texte.Notiz + " Nr. " + (s.Nummer + 1).ToString(De) + ": „" + (text.Length > 40 ? text[..39] + "…" : text) + "“" + lage;
    }
}
