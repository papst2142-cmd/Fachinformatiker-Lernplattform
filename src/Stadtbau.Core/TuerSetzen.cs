using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.28.0 (Weisung P12-121, Nico 2a 3a, Sammelliste 260): die Tuer eines Hauses selbst setzen.
/// <list type="bullet">
/// <item>Knopf „Tür setzen“, dann eine Wand antippen (Draufsicht oder 3D): die Tuer kommt an die naechste Stelle dieser Wand,
/// mindestens <see cref="EckAbstand"/> von jeder Ecke. Der Fussweg fuehrt gerade hinaus und dann zur naechsten Strasse wie bei
/// „Tür dazu“. Mit dem Finger oder der Maus an der Wand entlang ziehen geht auch um die Ecke. Jedes Setzen ist ein Schritt.</item>
/// <item>Liegt auf derselben Wand bis <see cref="FangWeite"/> eine eigene Tuer des CC0-Modells, rastet die Tuer auf ihre Mitte ein
/// (E1, nur mit „Einrasten“).</item>
/// <item>Gespeichert wie bisher: Gebaeude ohne Ort als Zierweg (<c>stadtdaten.darstellung.zierwege</c>), Orte (auch mit
/// Innenraum und das Zuhause) als <c>stadtdaten.zugaenge_neu</c>; der Versatz <c>darstellung.modell_tuer</c> des Orts faellt
/// weg. Das Spiel liest beides schon und setzt die Tuer auf die echte Wand (<c>StadtTueren.Wand</c>); kein neues Feld.</item>
/// </list>
/// </summary>
public static class TuerSetzen
{
    /// <summary>Mindestabstand der Tuermitte von einer Hausecke.</summary>
    public const float EckAbstand = 0.3f;

    /// <summary>Bis zu diesem Abstand (laengs der Wand) rastet die Tuer auf die Mitte einer Tuer des CC0-Modells ein.</summary>
    public const float FangWeite = 0.6f;

    /// <summary>Kann an diesem Stueck eine Tuer gesetzt werden? null = ja, sonst der Grund.</summary>
    public static string? Nicht(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art == AussenArt.Haus)
        {
            return AussenBearbeitung.Ort(aussen, s.Ort) is null ? Texte.TuerNurGebaeude : null;
        }
        // S 0.31.0 (Weisung P12-130 Frage 2 = a): auch ein Gebaeude ohne Ort mit Innenraum (Blueprint, uebernommenes
        // Katalog-Stueck), das schon eine Tuer hat, bekommt „Tür setzen“ (wie ein Ort mit Innenraum seit S 0.28.0)
        if (s.Art == AussenArt.Gebaeude && Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is PyObjekt g
            && Aussen.Bauten(aussen)[Py.Str(g["modell"])] is PyObjekt && GebaeudeTuer.HatTuer(aussen, s))
        {
            return null;
        }
        var grund = GebaeudeTuer.DazuNicht(aussen, s);
        return grund is null || grund == Texte.TuerSchonDa ? null : grund;
    }

    /// <summary>Tuer (Mitte auf der Wand) des Hauses, sonst null.</summary>
    public static Vector2? Tuer(PyObjekt aussen, AussenStelle s)
    {
        var z = s.Art == AussenArt.Haus ? AussenBearbeitung.Zugang(aussen, s.Ort) : s.Art == AussenArt.Gebaeude ? Blueprint.ZierwegVon(aussen, s) : null;
        return z?["tuer"] is List<object?> { Count: 2 } t ? new Vector2((float)Py.F(t[0]), (float)Py.F(t[1])) : null;
    }

    /// <summary>
    /// Naechster Platz fuer die Tuer auf einer der vier Waende des Rechtecks (x, y, w, h) zum Punkt <paramref name="p"/>:
    /// Mitte der Tuer (auf 0,01) und Richtung hinaus. <paramref name="modellTueren"/>: Tueren des CC0-Modells (Punkte auf der
    /// Wand), auf die die Tuer bis <see cref="FangWeite"/> einrastet.
    /// </summary>
    public static (Vector2 Punkt, Vector2 Aussen) AufWand((double X, double Y, double W, double H) r, Vector2 p, IReadOnlyList<Vector2>? modellTueren = null)
    {
        var (x1, y1, x2, y2) = ((float)r.X, (float)r.Y, (float)(r.X + r.W), (float)(r.Y + r.H));
        var waende = new (Vector2 A, Vector2 B, Vector2 N)[]
        {
            (new(x1, y2), new(x2, y2), new(0, 1)), (new(x2, y1), new(x2, y2), new(1, 0)),
            (new(x1, y1), new(x2, y1), new(0, -1)), (new(x1, y1), new(x1, y2), new(-1, 0)),
        };
        var best = (P: Vector2.Zero, N: Vector2.Zero, D: float.MaxValue, Wand: -1);
        for (var i = 0; i < waende.Length; i++)
        {
            var (a, b, n) = waende[i];
            var q = AufStrecke(a, b, p);
            var d = Vector2.Distance(q, p);
            if (d < best.D - 1e-5f)
            {
                best = (q, n, d, i);
            }
        }
        var (wa, wb, _) = waende[best.Wand];
        if (modellTueren is not null)
        {
            // auf die naechste Tuer des Modells an derselben Wand (Lot hoechstens 0,4 von ihr, die Kaesten der Szene und die
            // Grundflaeche der Daten unterscheiden sich um Bruchteile)
            var bisher = best.P;
            var weite = FangWeite;
            foreach (var m in modellTueren)
            {
                var q = AufStrecke(wa, wb, m);
                var laengs = Vector2.Distance(q, bisher);
                if (Vector2.Distance(q, m) <= 0.4f && laengs <= weite)
                {
                    (best.P, weite) = (q, laengs);
                }
            }
        }
        return (new Vector2(MathF.Round(best.P.X, 2), MathF.Round(best.P.Y, 2)), best.N);
    }

    /// <summary>Lot von p auf die Wand a–b, mindestens <see cref="EckAbstand"/> von den Ecken (kurze Wand: Mitte).</summary>
    private static Vector2 AufStrecke(Vector2 a, Vector2 b, Vector2 p)
    {
        var l = Vector2.Distance(a, b);
        if (l < 1e-6f)
        {
            return a;
        }
        var u = (b - a) / l;
        var t = l <= 2 * EckAbstand ? l / 2 : Math.Clamp(Vector2.Dot(p - a, u), EckAbstand, l - EckAbstand);
        return a + (u * t);
    }

    /// <summary>
    /// Tuer des Hauses <paramref name="s"/> an die Wand bei <paramref name="p"/> setzen (Gebaeude ohne Tuer: wie „Tür dazu“,
    /// mit eigener Kopie des Modells, wenn noetig). Rueckgabe: gelber Hinweis (Weg erreicht keine Strasse), Grund, warum es nicht
    /// geht, oder null. Wirft nichts.
    /// </summary>
    public static string? Setzen(PyObjekt aussen, AussenStelle s, Vector2 p, IReadOnlyList<Vector2>? modellTueren = null)
    {
        if (Nicht(aussen, s) is { } nicht)
        {
            return nicht;
        }
        if (s.Art == AussenArt.Gebaeude && !GebaeudeTuer.HatTuer(aussen, s))
        {
            return GebaeudeTuer.Dazu(aussen, s, einrasten: false, punkt: p, modellTueren: modellTueren);
        }
        var (tuer, n) = AufWand(AussenBearbeitung.HausFlaeche(aussen, s), p, modellTueren);
        var weg = new List<object?> { Punkt(tuer), Punkt(tuer + (n * GebaeudeTuer.WegStart)) };
        PyObjekt zugang;
        if (s.Art == AussenArt.Haus)
        {
            var sd = Aussen.Stadtdaten(aussen);
            zugang = new PyObjekt { { "tuer", Punkt(tuer) }, { "weg", weg } };
            Py.Setzen(Abschnitt(sd, "zugaenge_neu"), s.Ort, zugang);
            // Fund 262: eingerastet auf eine Tuer des Modells (Punkt genau auf ihrer Mitte), bleibt der Versatz des Modells
            // (darstellung.modell_tuer): sonst rueckte das Modell um ihn zur Seite und die Tuer laege neben der CC0-Tuer
            var eingerastet = modellTueren?.Any(m => Vector2.Distance(m, tuer) <= StadtTueren.EigeneTuerFang) == true;
            if (!eingerastet && Get(Objekt(Get(sd, "darstellung") ?? new PyObjekt()), "modell_tuer") is PyObjekt mt)
            {
                // das Modell sitzt nicht mehr mit seiner eigenen Tuer auf dem Weg
                mt.RemoveAll(e => e.Key == s.Ort);
            }
        }
        else
        {
            zugang = Blueprint.ZierwegVon(aussen, s)!;
            Py.Setzen(zugang, "tuer", Punkt(tuer));
            Py.Setzen(zugang, "weg", weg);
        }
        return WegZurStrasse(aussen, s, zugang) ? null : Texte.TuerWegOhneStrasse;
    }

    /// <summary>
    /// Welle S 0.33.0 (Weisung P12-148, Sammelliste 272): die Tuer an den Platz <paramref name="platz"/> der gezeichneten Fassade
    /// setzen (<see cref="TuerFassade.Finden"/>). Der Versatz <c>darstellung.modell_tuer</c> des Orts faellt immer weg (die
    /// Fassade ist ohne ihn gerechnet). Ein Fensterteil eines eigenen Gebaeudes wird dabei zum Tuerteil, das bisherige Tuerteil
    /// zu diesem Fensterteil (Frage 2 = a), alles ein Schritt. Rueckgabe: Hinweis (verschoben, keine glatte Stelle, Weg ohne
    /// Strasse), Grund oder null.
    /// </summary>
    public static string? SetzenAn(PyObjekt aussen, AussenStelle s, TuerFassade.Platz platz)
    {
        if (Nicht(aussen, s) is { } nicht)
        {
            return nicht;
        }
        if (s.Art == AussenArt.Gebaeude && !GebaeudeTuer.HatTuer(aussen, s))
        {
            // ohne Tuer: wie „Tür dazu“ (Kopie des Modells, Zierweg), dann an den Platz
            if (GebaeudeTuer.Dazu(aussen, s, einrasten: false, punkt: platz.Punkt) is { } grund && grund != Texte.TuerWegOhneStrasse)
            {
                return grund;
            }
        }
        var tuer = platz.Punkt;
        var weg = new List<object?> { Punkt3(tuer), Punkt3(tuer + (platz.Aussen * GebaeudeTuer.WegStart)) };
        string? modell;
        PyObjekt zugang;
        if (s.Art == AussenArt.Haus)
        {
            var sd = Aussen.Stadtdaten(aussen);
            zugang = new PyObjekt { { "tuer", Punkt3(tuer) }, { "weg", weg } };
            Py.Setzen(Abschnitt(sd, "zugaenge_neu"), s.Ort, zugang);
            if (platz.Eigene)
            {
                // Sammelliste 312: die Tuer des Modells ist die Tuer, ohne Versatz (wie die Betriebswerkstatt der Ausgangsstadt)
                Py.Setzen(Abschnitt(Abschnitt(sd, "darstellung"), "modell_tuer"), s.Ort, 0L);
            }
            else if (Get(Objekt(Get(sd, "darstellung") ?? new PyObjekt()), "modell_tuer") is PyObjekt mt)
            {
                mt.RemoveAll(e => e.Key == s.Ort);
            }
            modell = Py.Str(AussenBearbeitung.Ort(aussen, s.Ort)!["modell"]);
        }
        else
        {
            zugang = Blueprint.ZierwegVon(aussen, s)!;
            Py.Setzen(zugang, "tuer", Punkt3(tuer));
            Py.Setzen(zugang, "weg", weg);
            // Sammelliste 312: Zierweg auf die eigene Tuer des Modells (das Spiel zeichnet dort keine zweite Tuer)
            if (platz.Eigene)
            {
                Py.Setzen(zugang, "modell_tuer", true);
            }
            else
            {
                zugang.RemoveAll(e => e.Key == "modell_tuer");
            }
            modell = Py.Str(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["modell"]);
        }
        if (platz.Fensterteil is { } teil)
        {
            FensterZurTuer(aussen, modell, teil);
        }
        var strasse = WegZurStrasse(aussen, s, zugang);
        return !strasse ? Texte.TuerWegOhneStrasse
            : platz.OhneGlatte ? Texte.TuerKeineGlatteStelle
            : platz.Verschoben ? Texte.TuerEingerastetGlatt
            : null;
    }

    /// <summary>
    /// Frage 2 = a: das Fensterteil Nummer <paramref name="teil"/> im Erdgeschoss des eigenen Gebaeudes wird zum Tuerteil (Art des
    /// bisherigen Tuerteils, sonst „Tür“), das bisherige Tuerteil im Erdgeschoss zu diesem Fensterteil; Eingang und Block
    /// (<c>karte.modelle</c>) folgen. Jedes Teil behaelt Feld und Drehung.
    /// </summary>
    internal static void FensterZurTuer(PyObjekt aussen, string modell, int teil)
    {
        var sd = Aussen.Stadtdaten(aussen);
        if (Get(Bauteile.Gebaeude(sd), modell) is not PyObjekt e)
        {
            return;
        }
        var teile = Bauteile.TeileLesen(Get(e, "teile"));
        if (teil < 0 || teil >= teile.Count)
        {
            return;
        }
        var fenster = teile[teil];
        var alteTuer = teile.FindIndex(t => t.Stockwerk == 0 && t.Art is { Tuer: true } && Bauteile.IstWohnbau(t.Id));
        var tuerId = alteTuer >= 0 ? teile[alteTuer].Id : Bauteile.Praefix + "building-door";
        if (alteTuer >= 0)
        {
            teile[alteTuer] = teile[alteTuer] with { Id = fenster.Id };
        }
        teile[teil] = fenster with { Id = tuerId };
        Py.Setzen(e, "teile", Bauteile.TeileDaten(teile));
        Py.Setzen(e, "eingang", Bauteile.EingaengeDaten([new Bauteile.Eingang(fenster.X, fenster.Y, fenster.Fassade)]));
        var gm = Bauteile.Zusammensetzen(modell, teile, Katalog.Bauteil);
        if (Get(Aussen.Karte(aussen), "modelle") is PyObjekt kartenModelle)
        {
            Py.Setzen(kartenModelle, modell, Bauteile.Block(gm));
        }
    }

    /// <summary>
    /// Weg des Zugangs <paramref name="zugang"/> zur Strasse (wie „Tür dazu“): S 0.34.1 rechtwinklig mit runden Ecken
    /// (<see cref="GebaeudeTuer.WegRechtwinklig"/>, Sammelliste 278/280).
    /// </summary>
    internal static bool WegZurStrasse(PyObjekt aussen, AussenStelle s, PyObjekt zugang)
    {
        var strassen = AussenBearbeitung.Strassen(aussen);
        var laternen = Aussen.Laternen(aussen).OfType<List<object?>>().Select(l => new Vector2((float)Py.F(l[0]), (float)Py.F(l[1]))).ToList();
        return GebaeudeTuer.WegRechtwinklig(zugang, AussenBearbeitung.HausFlaeche(aussen, s), strassen, laternen);
    }

    /// <summary>
    /// Wand fuer eine neue Tuer („Tür dazu“): die Wand, deren Mitte (einen Schritt vor der Wand) der naechsten Strasse am
    /// naechsten liegt; ohne Strasse die Vorderseite. Gleich weit: Vorderseite, dann im Uhrzeigersinn.
    /// </summary>
    public static Vector2 WandZurStrasse(PyObjekt aussen, AussenStelle s)
    {
        var vorne = AussenBearbeitung.HausFront(aussen, s);
        var strassen = AussenBearbeitung.Strassen(aussen).Where(l => l.Count >= 2).ToList();
        if (strassen.Count == 0)
        {
            return vorne;
        }
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(aussen, s);
        var mitte = new Vector2((float)(x + (w / 2)), (float)(y + (h / 2)));
        var best = (N: vorne, D: float.MaxValue);
        var n = vorne;
        for (var i = 0; i < 4; i++)
        {
            var tief = n.X != 0 ? w / 2 : h / 2;
            var vor = mitte + (n * (float)(tief + GebaeudeTuer.WegStart));
            var d = strassen.Min(l => Kurve.Abstand(vor, l));
            if (d < best.D - 1e-4f)
            {
                best = (n, d);
            }
            n = new Vector2(-n.Y, n.X);
        }
        return best.N;
    }

    /// <summary>
    /// Tueren des CC0-Modells eines Hauses (Punkte auf den Waenden des Kastens) aus der gebauten Szene <paramref name="szene"/>.
    /// Gesucht wird mit derselben Regel wie im Spiel (<see cref="StadtTueren.ModellTueren"/>) an allen vier Waenden, mit den
    /// Originalfarben der Modelle (die Szene hat Tueren auf der Rueckseite schon in Wandfarbe umgefaerbt).
    /// </summary>
    public static List<Vector2> ModellTuerMitten(StadtSzene szene, GrafikBibliothek? grafik, (double X, double Y, double W, double H) r)
    {
        var mitte = new Vector2((float)(r.X + (r.W / 2)), (float)(r.Y + (r.H / 2)));
        if (grafik is null || szene.Haeuser.FirstOrDefault(h => mitte.X >= h.Min.X && mitte.X <= h.Max.X && mitte.Y >= h.Min.Z && mitte.Y <= h.Max.Z) is not { } haus)
        {
            return [];
        }
        var ergebnis = new List<Vector2>();
        var c = new Vector2((haus.Min.X + haus.Max.X) / 2, (haus.Min.Z + haus.Max.Z) / 2);
        // Fund 262: Gebaeude aus dem Editor haben Glastueren (Tuer-Teile), die Grafik der Szene kennt ihr Modell
        var g = szene.Grafik ?? grafik;
        var glas = Enumerable.Range(haus.StueckeVon, haus.StueckeBis - haus.StueckeVon).Any(i => StadtTueren.MitGlasTuer(szene.Stuecke[i].Name));
        foreach (var aussen in new Vector2[] { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) })
        {
            var tief = MathF.Abs(aussen.X) > 0.5f ? (haus.Max.X - haus.Min.X) / 2 : (haus.Max.Z - haus.Min.Z) / 2;
            var lang = MathF.Abs(aussen.X) > 0.5f ? (haus.Max.Z - haus.Min.Z) / 2 : (haus.Max.X - haus.Min.X) / 2;
            var wand = c + (aussen * tief);
            var quer = new Vector2(-aussen.Y, aussen.X);
            foreach (var t in StadtTueren.ModellTueren(szene.Stuecke, haus.StueckeVon, haus.StueckeBis, i => g.Modell(szene.Stuecke[i].Name), wand, aussen, weite: lang, glas: glas))
            {
                ergebnis.Add(wand + (quer * t.Mitte));
            }
        }
        return ergebnis;
    }

    /// <summary>S 0.33.0: auf 0,001 (eine Tuer genau wie in der Ausgangsstadt muss bis 0,005 gleich liegen).</summary>
    private static List<object?> Punkt3(Vector2 p) => [Bearbeitung.Zahl(Math.Round(p.X, 3)), Bearbeitung.Zahl(Math.Round(p.Y, 3))];

    private static List<object?> Punkt(Vector2 p) => [Bearbeitung.Zahl(Math.Round(p.X, 2)), Bearbeitung.Zahl(Math.Round(p.Y, 2))];

    private static PyObjekt Abschnitt(PyObjekt o, string name)
    {
        if (Get(o, name) is not PyObjekt a)
        {
            a = new PyObjekt();
            Py.Setzen(o, name, a);
        }
        return a;
    }
}
