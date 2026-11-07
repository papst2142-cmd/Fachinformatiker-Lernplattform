using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// S 0.8.0 (Weisung P12-47, Plan P12-26b Abschnitt 10): Wasser bearbeiten. Fluss in <c>karte.welt.landschaft</c>
/// (Typ <c>fluss</c>, Punkte wie eine Strasse, Breite fest), Teiche und Stege in <c>stadtdaten.wasser</c>. Teich und Fluss
/// haben Eckpunkte zum Ziehen (Stelle mit <see cref="AussenStelle.Punkt"/> ab 0; -1 = das ganze Stueck). Frage 7 = a:
/// freie Form mit gerundeten Ecken; die festen Formen (<see cref="Wasser.FormPunkte"/>) sind nur eine Hilfe zum Setzen.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Breite und Tiefe eines neuen Teichs (eckig, Ecken gerundet).</summary>
    public const double NeuerTeichBreite = 3;

    public const double NeuerTeichTiefe = 2;

    /// <summary>Laenge eines neuen Flusses (drei Punkte, der mittlere zum Ziehen eines Bogens).</summary>
    public const double NeuerFluss = 6;

    /// <summary>Laenge und Breite eines neuen Stegs.</summary>
    public const double NeuerStegLaenge = 2;

    public const double NeuerStegBreite = 0.5;

    /// <summary>Wenigstens so viele Eckpunkte hat ein Teich (Eckpunkt weg geht darunter nicht).</summary>
    public const int TeichMindestPunkte = 3;

    /// <summary>Greifradius eines Eckpunkts (Teich, Fluss).</summary>
    public const double EckGreifen = 0.3;

    private static bool IstWasser(AussenArt art) => art is AussenArt.Fluss or AussenArt.Teich or AussenArt.Steg;

    /// <summary>Hat das Stueck Eckpunkte zum Ziehen (Teich, Fluss)?</summary>
    public static bool MitEckpunkten(AussenArt art) => art is AussenArt.Teich or AussenArt.Fluss;

    /// <summary>Das ganze Stueck zu einer Eckpunkt-Stelle (Punkt -1).</summary>
    public static AussenStelle Ganz(AussenStelle s) => MitEckpunkten(s.Art) ? s with { Punkt = -1 } : s;

    /// <summary>Die Liste der Eckpunkte eines Teichs oder Flusses (Objekte [x, y]).</summary>
    public static List<object?> Eckpunkte(PyObjekt aussen, AussenStelle s) => s.Art == AussenArt.Teich
        ? Liste(Objekt(Aussen.Teiche(aussen)[s.Nummer])["punkte"])
        : Liste(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["punkte"]);

    /// <summary>Eckpunkte als Vektoren (wie gesetzt).</summary>
    public static List<Vector2> EckpunkteWerte(PyObjekt aussen, AussenStelle s) => Linie(Eckpunkte(aussen, s));

    private static IEnumerable<(AussenStelle Stelle, double X, double Y)> AlleWasser(PyObjekt aussen)
    {
        var land = Aussen.Landschaft(aussen);
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is PyObjekt l && Py.Gleich(l["typ"], "fluss") && Linie(l["punkte"]) is { Count: > 0 } linie)
            {
                var m = linie[linie.Count / 2];
                yield return (new AussenStelle(AussenArt.Fluss, i, -1), m.X, m.Y);
            }
        }
        var teiche = Aussen.Teiche(aussen);
        for (var i = 0; i < teiche.Count; i++)
        {
            if (Wasser.Punkte(teiche[i]) is { Count: > 0 } p)
            {
                yield return (new AussenStelle(AussenArt.Teich, i, -1), p.Average(q => q.X), p.Average(q => q.Y));
            }
        }
        var stege = Aussen.Stege(aussen);
        for (var i = 0; i < stege.Count; i++)
        {
            if (stege[i] is PyObjekt o)
            {
                yield return (new AussenStelle(AussenArt.Steg, i), Py.F(o["x"]), Py.F(o["y"]));
            }
        }
    }

    /// <summary>Gibt es die Eckpunkt-Stelle (noch)?</summary>
    private static bool EckpunktGueltig(PyObjekt aussen, AussenStelle s) =>
        Alle(aussen).Any(a => a.Stelle == Ganz(s)) && s.Punkt < Eckpunkte(aussen, s).Count;

    /// <summary>Eckpunkt eines Teichs oder Flusses beim Punkt (naechster innerhalb von <see cref="EckGreifen"/>), sonst null.</summary>
    public static AussenStelle? EckpunktBei(PyObjekt aussen, double x, double y, double greifen = EckGreifen)
    {
        var p = new Vector2((float)x, (float)y);
        AussenStelle? best = null;
        var bestD = (float)greifen;
        foreach (var (s, _, _) in AlleWasser(aussen).Where(a => MitEckpunkten(a.Stelle.Art)).ToList())
        {
            var punkte = EckpunkteWerte(aussen, s);
            for (var k = 0; k < punkte.Count; k++)
            {
                var d = Vector2.Distance(p, punkte[k]);
                if (d <= bestD)
                {
                    (best, bestD) = (s with { Punkt = k }, d);
                }
            }
        }
        return best;
    }

    /// <summary>Steg, Teich (samt Ufer) oder Fluss (samt Ufer) beim Punkt; Reihenfolge Steg, Teich, Fluss.</summary>
    private static AussenStelle? WasserBei(PyObjekt aussen, Vector2 p, AussenArt welche)
    {
        var alle = AlleWasser(aussen).ToList();
        if (welche == AussenArt.Steg)
        {
            foreach (var (s, _, _) in alle.Where(a => a.Stelle.Art == AussenArt.Steg).Reverse())
            {
                if (Wasser.Steg(Aussen.Stege(aussen)[s.Nummer]) is { } steg)
                {
                    var (x1, z1, x2, z2) = steg.Rechteck;
                    if (p.X >= x1 - 0.05f && p.X <= x2 + 0.05f && p.Y >= z1 - 0.05f && p.Y <= z2 + 0.05f)
                    {
                        return s;
                    }
                }
            }
            return null;
        }
        if (welche == AussenArt.Teich)
        {
            foreach (var (s, _, _) in alle.Where(a => a.Stelle.Art == AussenArt.Teich).Reverse())
            {
                if (Wasser.Punkte(Aussen.Teiche(aussen)[s.Nummer]) is { Count: >= 3 } punkte && Wasser.Drin(new Teich(punkte).MitUfer, p))
                {
                    return s;
                }
            }
            return null;
        }
        return alle.Where(a => a.Stelle.Art == AussenArt.Fluss)
            .Select(a => (a.Stelle, D: Kurve.Abstand(p, FlussGlatt(aussen, a.Stelle))))
            .Where(a => a.D <= ((StadtSzene.FlussBreite + 0.3f) / 2) + 0.05f).OrderBy(a => a.D).Select(a => a.Stelle).FirstOrDefault();
    }

    /// <summary>Neues Wasser-Stueck mit der Mitte bei (x, y); <paramref name="form"/> = "rund" fuer einen runden Teich (Hilfe), sonst eckig.</summary>
    private static AussenStelle? WasserSetzen(PyObjekt aussen, AussenArt art, double x, double y, bool raster, string? form)
    {
        var (breite, hoehe) = Aussen.Groesse(aussen);
        var r = raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        double Auf(double w) => Bearbeitung.AufRaster(w, r);
        switch (art)
        {
            case AussenArt.Teich:
            {
                var (bw, bt) = (Math.Min(NeuerTeichBreite, breite), Math.Min(NeuerTeichTiefe, hoehe));
                var cx = Math.Clamp(Auf(x), bw / 2, breite - (bw / 2));
                var cy = Math.Clamp(Auf(y), bt / 2, hoehe - (bt / 2));
                var punkte = Wasser.FormPunkte(form == "rund" ? Wasser.Form.Rund : Wasser.Form.Eckig, (float)cx, (float)cy, (float)bw, (float)bt);
                var teiche = Liste(Aussen.WasserAnlegen(aussen)["teiche"]);
                teiche.Add(new PyObjekt { { "punkte", punkte.Select(q => (object?)Punkt(q.X, q.Y)).ToList() } });
                return new AussenStelle(AussenArt.Teich, teiche.Count - 1, -1);
            }
            case AussenArt.Steg:
            {
                var stege = Liste(Aussen.WasserAnlegen(aussen)["stege"]);
                stege.Add(new PyObjekt
                {
                    { "x", Bearbeitung.Zahl(Auf(x)) }, { "y", Bearbeitung.Zahl(Auf(y)) },
                    { "w", Bearbeitung.Zahl(NeuerStegLaenge) }, { "t", Bearbeitung.Zahl(NeuerStegBreite) }, { "dreh", 0L },
                });
                return new AussenStelle(AussenArt.Steg, stege.Count - 1);
            }
            default:
            {
                var mx = Bearbeitung.AufRaster(x, Bearbeitung.Raster);
                var my = Bearbeitung.AufRaster(y, Bearbeitung.Raster);
                var land = Aussen.Landschaft(aussen);
                var a = Math.Max(0, mx - (NeuerFluss / 2));
                var b = Math.Min(breite, mx + (NeuerFluss / 2));
                land.Add(new PyObjekt { { "typ", "fluss" }, { "punkte", new List<object?> { Punkt(a, my), Punkt((a + b) / 2, my), Punkt(b, my) } } });
                return new AussenStelle(AussenArt.Fluss, land.Count - 1, -1);
            }
        }
    }

    private static void WasserVerschieben(PyObjekt aussen, AussenStelle s, double dx, double dy)
    {
        void Schieben(object? punkt)
        {
            var p = Liste(punkt);
            p[0] = Bearbeitung.Zahl(Py.F(p[0]) + dx);
            p[1] = Bearbeitung.Zahl(Py.F(p[1]) + dy);
        }
        if (s.Art == AussenArt.Steg)
        {
            var o = Objekt(Aussen.Stege(aussen)[s.Nummer]);
            Py.Setzen(o, "x", Bearbeitung.Zahl(Py.F(o["x"]) + dx));
            Py.Setzen(o, "y", Bearbeitung.Zahl(Py.F(o["y"]) + dy));
            return;
        }
        var punkte = Eckpunkte(aussen, s);
        if (s.Punkt >= 0)
        {
            Schieben(punkte[s.Punkt]);
            return;
        }
        foreach (var p in punkte)
        {
            Schieben(p);
        }
    }

    private static (double X, double Y) WasserLage(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art == AussenArt.Steg)
        {
            var o = Objekt(Aussen.Stege(aussen)[s.Nummer]);
            return (Py.F(o["x"]), Py.F(o["y"]));
        }
        var punkte = EckpunkteWerte(aussen, s);
        if (s.Punkt >= 0)
        {
            return (punkte[s.Punkt].X, punkte[s.Punkt].Y);
        }
        // Teich und Fluss: der erste Eckpunkt (wie der erste Punkt einer Strasse)
        return (punkte[0].X, punkte[0].Y);
    }

    /// <summary>Vierteldrehung im Uhrzeigersinn: Steg ueber dreh; Teich und Fluss drehen ihre Punkte um die Mitte (auf dem Raster 0,5).</summary>
    private static bool WasserDrehen(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art == AussenArt.Steg)
        {
            var o = Objekt(Aussen.Stege(aussen)[s.Nummer]);
            Py.Setzen(o, "dreh", (Py.Int(Get(o, "dreh", 0L)) + 1) % 4);
            return true;
        }
        var punkte = Eckpunkte(aussen, s);
        var linie = Linie(punkte);
        var m = new Vector2((float)Bearbeitung.AufRaster((linie.Min(p => p.X) + linie.Max(p => p.X)) / 2, Bearbeitung.Raster),
            (float)Bearbeitung.AufRaster((linie.Min(p => p.Y) + linie.Max(p => p.Y)) / 2, Bearbeitung.Raster));
        for (var i = 0; i < punkte.Count; i++)
        {
            var d = linie[i] - m;
            punkte[i] = Punkt(m.X - d.Y, m.Y + d.X);
        }
        return true;
    }

    private static AussenStelle? WasserKopieren(PyObjekt aussen, AussenStelle s)
    {
        s = Ganz(s);
        switch (s.Art)
        {
            case AussenArt.Steg:
            {
                var stege = Aussen.Stege(aussen);
                var kopie = (PyObjekt)Py.Kopie(stege[s.Nummer])!;
                Py.Setzen(kopie, "y", Bearbeitung.Zahl(Py.F(kopie["y"]) + Py.F(kopie["t"]) + Bearbeitung.Raster));
                stege.Add(kopie);
                return new AussenStelle(AussenArt.Steg, stege.Count - 1);
            }
            case AussenArt.Teich:
            {
                var teiche = Aussen.Teiche(aussen);
                var kopie = (PyObjekt)Py.Kopie(teiche[s.Nummer])!;
                var punkte = Liste(kopie["punkte"]);
                var versatz = Linie(punkte) is var l && l.Count > 0 ? l.Max(p => p.X) - l.Min(p => p.X) + Bearbeitung.Raster + (2 * Wasser.UferBreite) : 1;
                foreach (var p in punkte)
                {
                    var q = Liste(p);
                    q[0] = Bearbeitung.Zahl(Py.F(q[0]) + versatz);
                }
                teiche.Add(kopie);
                return new AussenStelle(AussenArt.Teich, teiche.Count - 1, -1);
            }
            default:
            {
                var land = Aussen.Landschaft(aussen);
                var kopie = (PyObjekt)Py.Kopie(land[s.Nummer])!;
                foreach (var p in Liste(kopie["punkte"]))
                {
                    var q = Liste(p);
                    q[1] = Bearbeitung.Zahl(Py.F(q[1]) + 1.5);
                }
                land.Add(kopie);
                return new AussenStelle(AussenArt.Fluss, land.Count - 1, -1);
            }
        }
    }

    private static void WasserLoeschen(PyObjekt aussen, AussenStelle s)
    {
        switch (s.Art)
        {
            case AussenArt.Steg:
                Aussen.Stege(aussen).RemoveAt(s.Nummer);
                break;
            case AussenArt.Teich:
                Aussen.Teiche(aussen).RemoveAt(s.Nummer);
                break;
            default:
                Aussen.Landschaft(aussen).RemoveAt(s.Nummer);
                break;
        }
    }

    /// <summary>
    /// Eckpunkt dazu: nach dem gewaehlten Eckpunkt (Mitte zur naechsten Ecke), beim ganzen Stueck in der Mitte der laengsten
    /// Kante (Fluss: des laengsten Stuecks). Rueckgabe: der neue Eckpunkt.
    /// </summary>
    public static AussenStelle? EckpunktDazu(PyObjekt aussen, AussenStelle s)
    {
        if (!MitEckpunkten(s.Art))
        {
            return null;
        }
        var punkte = Eckpunkte(aussen, s);
        var l = Linie(punkte);
        var geschlossen = s.Art == AussenArt.Teich;
        var kanten = geschlossen ? l.Count : l.Count - 1;
        if (kanten < 1)
        {
            return null;
        }
        var nach = s.Punkt >= 0 && (geschlossen || s.Punkt < l.Count - 1) ? s.Punkt
            : Enumerable.Range(0, kanten).MaxBy(i => Vector2.Distance(l[i], l[(i + 1) % l.Count]));
        if (!geschlossen && s.Punkt == l.Count - 1)
        {
            nach = l.Count - 2;   // am Ende des Flusses: zwischen den letzten beiden Punkten
        }
        var m = (l[nach] + l[(nach + 1) % l.Count]) / 2;
        punkte.Insert(nach + 1, Punkt(m.X, m.Y));
        RadienNachEinfuegen(aussen, s, nach + 1);
        return Ganz(s) with { Punkt = nach + 1 };
    }

    /// <summary>Warum der Eckpunkt nicht weg kann (null = es geht).</summary>
    public static string? EckpunktWegNicht(PyObjekt aussen, AussenStelle s) =>
        !MitEckpunkten(s.Art) || s.Punkt < 0 ? Texte.EckpunktWaehlen
        : Eckpunkte(aussen, s).Count <= (s.Art == AussenArt.Teich ? TeichMindestPunkte : 2) ? Texte.EckpunktWenigstens
        : null;

    /// <summary>Gewaehlten Eckpunkt entfernen (Teich bleibt mit mindestens drei, Fluss mit zwei Punkten).</summary>
    public static bool EckpunktWeg(PyObjekt aussen, AussenStelle s)
    {
        if (EckpunktWegNicht(aussen, s) is not null)
        {
            return false;
        }
        Eckpunkte(aussen, s).RemoveAt(s.Punkt);
        RadienNachEntfernen(aussen, s, s.Punkt);
        return true;
    }

    /// <summary>Schalter „Eis im Winter“ setzen (Frage 6 = a); aus nimmt den Schluessel weg (wie ohne Schalter).</summary>
    public static void EisSetzen(PyObjekt aussen, bool an)
    {
        var w = Aussen.WasserAnlegen(aussen);
        if (an)
        {
            Py.Setzen(w, "eis", true);
        }
        else
        {
            var rest = w.Where(p => p.Key != "eis").ToList();
            w.Clear();
            w.AddRange(rest);
        }
    }

    /// <summary>Laenge und Breite eines Stegs.</summary>
    public static (double W, double T) StegGroesse(PyObjekt aussen, AussenStelle s)
    {
        var o = Objekt(Aussen.Stege(aussen)[s.Nummer]);
        return (Py.F(o["w"]), Py.F(o["t"]));
    }

    /// <summary>Laenge und Breite eines Stegs setzen (Mitte bleibt; 0 oder weniger zeigt die Pruefung rot).</summary>
    public static void StegGroesseSetzen(PyObjekt aussen, AussenStelle s, double w, double t)
    {
        var o = Objekt(Aussen.Stege(aussen)[s.Nummer]);
        Py.Setzen(o, "w", Bearbeitung.Zahl(w));
        Py.Setzen(o, "t", Bearbeitung.Zahl(t));
    }

    private static string WasserBeschreibung(PyObjekt aussen, AussenStelle s, string lage)
    {
        var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        return s.Art switch
        {
            AussenArt.Steg => string.Create(de, $"{Texte.Steg}, {StegGroesse(aussen, s).W:0.##} lang, {StegGroesse(aussen, s).T:0.##} breit") + lage,
            _ => (s.Art == AussenArt.Teich ? Texte.Teich : Texte.Fluss)
                + (s.Punkt >= 0 ? ", " + Texte.Eckpunkt + " " + (s.Punkt + 1).ToString(de) + " von " + Eckpunkte(aussen, s).Count.ToString(de)
                    : ", " + Eckpunkte(aussen, s).Count.ToString(de) + " " + Texte.Eckpunkte) + lage,
        };
    }
}
