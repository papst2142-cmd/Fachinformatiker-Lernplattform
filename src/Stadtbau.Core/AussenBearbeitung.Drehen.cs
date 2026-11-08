using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle E (S 0.13.0, Weisungen P12-63, P12-67 Fragen 1 = a und 2 = a): Drehen.
/// <list type="bullet">
/// <item>Linien (Strasse, Kurve, Kreisverkehr, Fluss, Teich, Bahn) drehen in Schritten von <see cref="FeinGrad"/> Grad (Taste R,
/// Umschalt+R rueckwaerts) und ueber das Feld „Winkel“ auf 1 Grad genau (<see cref="WinkelSetzen"/>). Das Spiel zeichnet
/// Linien in jedem Winkel.</item>
/// <item>Haeuser (mit Ort, ohne Ort, Blueprints) drehen in 90-Grad-Schritten (<see cref="Hausdrehung"/>): Tuer, Fussweg
/// und Schild drehen mit, der Fussweg wird danach wieder an die naechste Strasse gelegt.</item>
/// <item>Katalog-Stueck, Steg und Park bleiben bei Vierteldrehungen (Nico 2a).</item>
/// </list>
/// Gedreht wird im Uhrzeigersinn (von oben gesehen), positive Grad = Uhrzeigersinn.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Schritt des feinen Drehens (R, Umschalt+R).</summary>
    public const int FeinGrad = 15;

    /// <summary>Lassen sich Stuecke dieser Art fein (nicht nur in Vierteln) drehen?</summary>
    public static bool FeinDrehbar(AussenArt art) => art is AussenArt.Strasse or AussenArt.Kurve or AussenArt.Kreisverkehr or AussenArt.Fluss or AussenArt.Teich or AussenArt.Bahn;

    /// <summary>Schritt in Grad fuer die Taste R und den Drehgriff: Linien 15, alles andere 90.</summary>
    public static int DrehSchritt(AussenArt art) => FeinDrehbar(art) ? FeinGrad : 90;

    /// <summary>Hat das Stueck ein Feld „Winkel“ (Strasse, Kurve, Fluss, Bahn; nicht der Ring eines Kreisverkehrs)?</summary>
    public static bool MitWinkel(PyObjekt aussen, AussenStelle s) =>
        IstLinienzug(s.Art) || (s.Art == AussenArt.Strasse && !Kreisverkehr.IstRing(Objekt(Aussen.Landschaft(aussen)[s.Nummer])));

    /// <summary>
    /// Winkel einer Linie in Grad (0 bis 359, ganz): Richtung vom ersten zum letzten Punkt, 0 = nach rechts (Osten), 90 = nach
    /// unten (Sueden). Null, wenn das Stueck kein Feld „Winkel“ hat.
    /// </summary>
    public static int? Winkel(PyObjekt aussen, AussenStelle s)
    {
        if (!MitWinkel(aussen, s))
        {
            return null;
        }
        var linie = IstLinienzug(s.Art) ? Linie(Eckpunkte(aussen, Ganz(s))) : Linie(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["punkte"]);
        if (linie.Count < 2 || Vector2.Distance(linie[0], linie[^1]) < 1e-4f)
        {
            return 0;
        }
        var d = linie[^1] - linie[0];
        var grad = (int)Math.Round(Math.Atan2(d.Y, d.X) * 180 / Math.PI, MidpointRounding.AwayFromZero);
        return ((grad % 360) + 360) % 360;
    }

    /// <summary>Linie auf diesen Winkel (Grad, wie <see cref="Winkel"/>) drehen; true = gedreht.</summary>
    public static bool WinkelSetzen(PyObjekt aussen, AussenStelle s, int grad)
    {
        if (Winkel(aussen, s) is not { } jetzt)
        {
            return false;
        }
        var delta = ((((grad - jetzt) % 360) + 540) % 360) - 180;
        return delta != 0 && Drehen(aussen, s, delta);
    }

    /// <summary>Ein Schritt mit der Taste R (vorwaerts) bzw. Umschalt+R (rueckwaerts).</summary>
    public static bool DrehenSchritt(PyObjekt aussen, AussenStelle s, bool rueckwaerts = false) =>
        Drehen(aussen, s, (rueckwaerts ? -1 : 1) * DrehSchritt(s.Art));

    /// <summary>
    /// Um <paramref name="grad"/> Grad im Uhrzeigersinn drehen. Vielfache von 90 drehen genau wie <see cref="Drehen(PyObjekt, AussenStelle)"/>;
    /// andere Winkel nur fuer Linien (sonst false). Ein fein gedrehtes Stueck rastet nicht wieder ein (das Einrasten wuerde den
    /// Winkel sofort zuruecknehmen).
    /// </summary>
    public static bool Drehen(PyObjekt aussen, AussenStelle s, int grad)
    {
        grad %= 360;
        if (grad == 0 || DrehenNicht(s) is not null)
        {
            return false;
        }
        if (grad % 90 == 0)
        {
            var mal = ((grad / 90) + 4) % 4;
            var ok = false;
            for (var i = 0; i < mal; i++)
            {
                ok |= Drehen(aussen, s);
            }
            return ok;
        }
        if (!FeinDrehbar(s.Art))
        {
            return false;
        }
        var w = grad * Math.PI / 180;
        var (sin, cos) = Math.SinCos(w);
        Vector2 Um(Vector2 p, Vector2 m)
        {
            var d = p - m;
            return new Vector2((float)(m.X + (d.X * cos) - (d.Y * sin)), (float)(m.Y + (d.X * sin) + (d.Y * cos)));
        }
        if (IstWasserOderBahn(s.Art))
        {
            var punkte = Eckpunkte(aussen, Ganz(s));
            var linie = Linie(punkte);
            var mw = Mitte(linie);
            for (var i = 0; i < punkte.Count; i++)
            {
                punkte[i] = PunktObjekt(Um(linie[i], mw));
            }
            return true;
        }
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        var sp = Liste(l["punkte"]);
        var sl = Linie(sp);
        var kreis = Kreisverkehr.IstRing(l) ? KreisverkehrTeile(aussen, s) : default;
        var m = kreis.Zufahrten is not null ? Kreisverkehr.Mitte(sl) : Mitte(sl);
        var fein = Strassenkurve.IstKurve(l);
        for (var i = 0; i < sp.Count; i++)
        {
            sp[i] = fein ? KurvenPunkt(Um(sl[i], m)) : PunktObjekt(Um(sl[i], m));
        }
        if (kreis.Zufahrten is not null)
        {
            KreisverkehrMitnehmen(aussen, kreis, p => Um(p, m));
        }
        return true;
    }

    private static Vector2 Mitte(List<Vector2> linie) =>
        new((linie.Min(p => p.X) + linie.Max(p => p.X)) / 2, (linie.Min(p => p.Y) + linie.Max(p => p.Y)) / 2);

    // ---- Haeuser ------------------------------------------------------------------------------------

    /// <summary>Vierteldrehungen eines Hauses (mit Ort) oder Gebaeudes (ohne Ort), sonst 0.</summary>
    public static int HausDreh(PyObjekt aussen, AussenStelle s) => s.Art switch
    {
        AussenArt.Gebaeude => Hausdrehung.Von(Objekt(Aussen.Landschaft(aussen)[s.Nummer])),
        AussenArt.Haus => Hausdrehung.VonOrt(Aussen.Stadtdaten(aussen), s.Ort),
        _ => 0,
    };

    /// <summary>Mitte der Bloecke eines Hauses in der Stadt (um sie dreht es sich).</summary>
    public static Vector2 HausMitte(PyObjekt aussen, AussenStelle s)
    {
        var o = s.Art == AussenArt.Haus ? Ort(aussen, s.Ort)! : Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        return new Vector2((float)Py.F(o["x"]), (float)Py.F(o["y"])) + Hausdrehung.Mitte(Get(Modelle(aussen), Py.Str(o["modell"])));
    }

    /// <summary>Richtung, in die die Vorderseite eines Hauses zeigt (ungedreht nach Sueden).</summary>
    public static Vector2 HausFront(PyObjekt aussen, AussenStelle s) => Hausdrehung.Drehen(new Vector2(0, 1), Vector2.Zero, HausDreh(aussen, s));

    /// <summary>Haus um <paramref name="mal"/> Vierteldrehungen im Uhrzeigersinn drehen (mit Tuer, Fussweg, Schild).</summary>
    private static bool HausDrehen(PyObjekt aussen, AussenStelle s, int mal)
    {
        var m = HausMitte(aussen, s);
        var neu = (((HausDreh(aussen, s) + mal) % 4) + 4) % 4;
        void Punkt2(object? punkt)
        {
            var p = Liste(punkt);
            var (x, y) = Hausdrehung.Drehen(Py.F(p[0]), Py.F(p[1]), m.X, m.Y, mal);
            p[0] = Bearbeitung.Zahl(x);
            p[1] = Bearbeitung.Zahl(y);
        }
        void Zugang2(PyObjekt z)
        {
            Punkt2(z["tuer"]);
            var weg = Liste(z["weg"]);
            foreach (var p in weg)
            {
                Punkt2(p);
            }
            WegAnschliessen(weg, Strassen(aussen));
        }
        if (s.Art == AussenArt.Gebaeude)
        {
            var g = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
            if (neu == 0)
            {
                g.RemoveAll(p => p.Key == Hausdrehung.Feld);
            }
            else
            {
                Py.Setzen(g, Hausdrehung.Feld, (long)neu);
            }
            foreach (var z in Zierwege(aussen, Py.Str(g["modell"])))
            {
                Zugang2(z);
            }
            return true;
        }
        var sd = Aussen.Stadtdaten(aussen);
        if (Get(sd, "darstellung") is not PyObjekt d)
        {
            d = [];
            Py.Setzen(sd, "darstellung", d);
        }
        if (Get(d, Hausdrehung.Abschnitt) is not PyObjekt h)
        {
            h = [];
            Py.Setzen(d, Hausdrehung.Abschnitt, h);
        }
        if (neu == 0)
        {
            h.RemoveAll(p => p.Key == s.Ort);
            if (h.Count == 0)
            {
                d.RemoveAll(p => p.Key == Hausdrehung.Abschnitt);
            }
        }
        else
        {
            Py.Setzen(h, s.Ort, (long)neu);
        }
        var o = Ort(aussen, s.Ort)!;
        if (o["schild"] is List<object?> schild)
        {
            Punkt2(schild);
        }
        if (Zugang(aussen, s.Ort) is { } zu)
        {
            Zugang2(zu);
        }
        return true;
    }
}
