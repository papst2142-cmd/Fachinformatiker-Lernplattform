using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle K1 (S 0.10.0, Weisung P12-55, Plan P12-36 Abschnitt 5): Kurven fuer Strassen und Radien am Fluss. Eine Kurve
/// ist eine Strasse mit vielen Punkten und dem Feld <c>kurve</c> (<see cref="Strassenkurve"/>); gesetzt wird sie als
/// <see cref="AussenArt.Kurve"/> mit der Kennung aus <see cref="Strassenkurve.Kennung"/> (Form, Winkel, Radius, gespiegelt),
/// danach ist sie eine <see cref="AussenArt.Strasse"/>. Sie bleibt beim Schieben, Drehen und Spiegeln ganz (alle Punkte
/// zusammen); liegt ein Ende bis <see cref="EndeFangweite"/> neben einem freien Strassenende, rastet die ganze Kurve dort mit Lage und
/// Richtung ein (Patch S 0.12.1, <see cref="KurveEinrasten"/>).
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Ist das Stueck eine Kurve (Strasse mit Feld <c>kurve</c>)?</summary>
    public static bool IstKurve(PyObjekt aussen, AussenStelle s) =>
        s.Art == AussenArt.Strasse && Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is PyObjekt l && Strassenkurve.IstKurve(l);

    /// <summary>Die Angabe <c>kurve</c> einer Kurve (Form, Winkel, Radius), sonst null.</summary>
    public static (string Form, int Winkel, double Radius)? KurveVon(PyObjekt aussen, AussenStelle s)
    {
        if (!IstKurve(aussen, s) || Objekt(Aussen.Landschaft(aussen)[s.Nummer])[Strassenkurve.Feld] is not PyObjekt k
            || k["form"] is not string form || k["winkel"] is not (long or double) || k["radius"] is not (long or double))
        {
            return null;
        }
        return (form, (int)Py.F(k["winkel"]), Py.F(k["radius"]));
    }

    /// <summary>Neue Kurve mit der Mitte bei (x, y) auf dem Raster 0,5 (Kennung siehe <see cref="Strassenkurve.Kennung"/>).</summary>
    private static AussenStelle? KurveSetzen(PyObjekt aussen, double x, double y, string? kennung, bool einrasten = true)
    {
        if (Strassenkurve.Lesen(kennung) is not { } k)
        {
            return null;
        }
        var linie = Strassenkurve.Mittellinie(k.Form, k.Winkel, k.Radius, k.Gespiegelt);
        var min = new Vector2(linie.Min(p => p.X), linie.Min(p => p.Y));
        var max = new Vector2(linie.Max(p => p.X), linie.Max(p => p.Y));
        var mitte = new Vector2((float)Bearbeitung.AufRaster(x, Bearbeitung.Raster), (float)Bearbeitung.AufRaster(y, Bearbeitung.Raster));
        var versatz = mitte - ((min + max) / 2);
        // ganz auf die Karte schieben (soweit sie passt)
        var (breite, hoehe) = Aussen.Groesse(aussen);
        if (max.X - min.X > breite || max.Y - min.Y > hoehe)
        {
            return null;
        }
        versatz.X += MathF.Max(0, -(min.X + versatz.X)) - MathF.Max(0, max.X + versatz.X - (float)breite);
        versatz.Y += MathF.Max(0, -(min.Y + versatz.Y)) - MathF.Max(0, max.Y + versatz.Y - (float)hoehe);
        var punkte = new List<object?>(linie.Select(p => (object?)KurvenPunkt(p + versatz)));
        var land = Aussen.Landschaft(aussen);
        land.Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", punkte },
            { "gehweg", Bearbeitung.Zahl(NeuerGehweg) },
            { Strassenkurve.Feld, new PyObjekt { { "form", k.Form }, { "winkel", (long)k.Winkel }, { "radius", Bearbeitung.Zahl(k.Radius) } } },
        });
        var s = new AussenStelle(AussenArt.Strasse, land.Count - 1);
        if (einrasten)
        {
            KurveEinrasten(aussen, s);
        }
        return s;
    }

    /// <summary>Warum sich das Stueck nicht spiegeln laesst (null = es geht; nur Kurven).</summary>
    public static string? SpiegelnNicht(PyObjekt aussen, AussenStelle s) => IstKurve(aussen, s) ? null : Texte.SpiegelnNurKurve;

    /// <summary>Kurve spiegeln (links und rechts tauschen): an der Geraden durch den Anfang in Anfangsrichtung, der Anfang bleibt.</summary>
    public static bool Spiegeln(PyObjekt aussen, AussenStelle s)
    {
        if (SpiegelnNicht(aussen, s) is not null)
        {
            return false;
        }
        var punkte = Liste(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["punkte"]);
        var linie = Linie(punkte);
        var a = linie[0];
        var d = Vector2.Normalize(linie[1] - linie[0]);
        for (var i = 0; i < punkte.Count; i++)
        {
            var v = linie[i] - a;
            var laengs = Vector2.Dot(v, d) * d;
            punkte[i] = KurvenPunkt(a + laengs - (v - laengs));
        }
        KurveEinrasten(aussen, s);
        return true;
    }

    // ---- Radius je Eckpunkt am Fluss -------------------------------------------------------------------

    /// <summary>Laesst sich am gewaehlten Eckpunkt ein Radius waehlen (Eckpunkt eines Flusses, nicht Anfang oder Ende)?</summary>
    public static bool RadiusWaehlbar(PyObjekt aussen, AussenStelle s) =>
        s.Art == AussenArt.Fluss && s.Punkt > 0 && s.Punkt < Eckpunkte(aussen, s).Count - 1;

    /// <summary>Wunschradius am gewaehlten Eckpunkt des Flusses (ohne Feld <c>radien</c>: 3).</summary>
    public static double FlussRadius(PyObjekt aussen, AussenStelle s)
    {
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        return l[Strassenkurve.RadienFeld] is List<object?> r && s.Punkt >= 0 && s.Punkt < r.Count ? Py.F(r[s.Punkt]) : Kurve.Radius;
    }

    /// <summary>Radius am gewaehlten Eckpunkt setzen (2, 3 oder 5); sind danach alle 3, faellt das Feld weg (wie ohne).</summary>
    public static bool FlussRadiusSetzen(PyObjekt aussen, AussenStelle s, double radius)
    {
        if (!RadiusWaehlbar(aussen, s) || !Strassenkurve.FlussRadien.Any(r => Math.Abs(r - radius) < 1e-9))
        {
            return false;
        }
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        var n = Eckpunkte(aussen, s).Count;
        var radien = RadienListe(l, n);
        radien[s.Punkt] = Bearbeitung.Zahl(radius);
        RadienAufraeumen(l);
        return true;
    }

    /// <summary>Feld <c>radien</c> mit genau <paramref name="n"/> Werten (fehlende = 3).</summary>
    private static List<object?> RadienListe(PyObjekt l, int n)
    {
        if (l[Strassenkurve.RadienFeld] is not List<object?> radien)
        {
            radien = [];
            Py.Setzen(l, Strassenkurve.RadienFeld, radien);
        }
        while (radien.Count < n)
        {
            radien.Add(Bearbeitung.Zahl(Kurve.Radius));
        }
        if (radien.Count > n)
        {
            radien.RemoveRange(n, radien.Count - n);
        }
        return radien;
    }

    /// <summary>Sind alle Radien 3 (wie ohne Feld), faellt das Feld weg.</summary>
    private static void RadienAufraeumen(PyObjekt l)
    {
        if (l[Strassenkurve.RadienFeld] is List<object?> r && r.All(w => w is long or double && Math.Abs(Py.F(w) - Kurve.Radius) < 1e-9))
        {
            var rest = l.Where(p => p.Key != Strassenkurve.RadienFeld).ToList();
            l.Clear();
            l.AddRange(rest);
        }
    }

    /// <summary>Nach Eckpunkt dazu/weg: das Feld <c>radien</c> passt sich an (neuer Punkt mit Radius 3).</summary>
    private static void RadienNachEinfuegen(PyObjekt aussen, AussenStelle s, int neu)
    {
        if (s.Art == AussenArt.Fluss && Objekt(Aussen.Landschaft(aussen)[s.Nummer])[Strassenkurve.RadienFeld] is List<object?> r)
        {
            r.Insert(Math.Min(neu, r.Count), Bearbeitung.Zahl(Kurve.Radius));
        }
    }

    private static void RadienNachEntfernen(PyObjekt aussen, AussenStelle s, int weg)
    {
        if (s.Art != AussenArt.Fluss)
        {
            return;
        }
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        if (l[Strassenkurve.RadienFeld] is List<object?> r && weg < r.Count)
        {
            r.RemoveAt(weg);
            RadienAufraeumen(l);
        }
    }

    /// <summary>Geglaetteter Linienzug eines Flusses wie im Spiel (mit Radius je Eckpunkt).</summary>
    public static List<Vector2> FlussGlatt(PyObjekt aussen, AussenStelle s) =>
        Kurve.Glatt(EckpunkteWerte(aussen, s), StadtSzene.Radien(Objekt(Aussen.Landschaft(aussen)[s.Nummer])));
}
