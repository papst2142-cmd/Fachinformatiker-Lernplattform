using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Patch S 0.46.1 (Weisung P12-225 a, Nico): ein Ort mit dem alten Haltepunkt (Birkenhain) bekommt das neue Aussehen
/// „Haltepunkt mit freiem Bahnsteig“ (<see cref="Zusatzmodelle.HaltepunktFrei"/>). Bis S 0.46.0 gab es das Modell nur beim
/// Setzen eines neuen Gebaeudes, „Aussehen tauschen“ bot es nicht an und war an schraeg gedrehten Haeusern gesperrt.
/// Ablauf (ein Schritt): Haus gerade drehen (Tuer und Weg drehen mit), Modell tauschen (Mitte der Bloecke bleibt), Tuer ans
/// Haeuschen wie „Tür dazu“ (<see cref="GebaeudeTuer.EingangPlatz"/>), dann ans Gleis legen wie ein Bahnhof
/// (<see cref="AussenBearbeitung.BahnhofAnGleis"/>: Bahnsteigkante parallel, <see cref="AussenBearbeitung.BahnsteigKante"/>
/// vor der Gleismitte, auch am schraegen Gleis). Kennung, Name, Innenraum und Aufgaben des Orts bleiben.
/// </summary>
public static class HaltepunktTausch
{
    /// <summary>Grundmodell des alten Haltepunkts (Stadt 1).</summary>
    public const string Alt = "haltepunkt";

    /// <summary>Zeigt die Auswahl den Knopf? Nur an einem Ort mit dem alten Haltepunkt (auch einer Kopie davon).</summary>
    public static bool KnopfZeigen(PyObjekt aussen, string ort) =>
        AussenBearbeitung.Ort(aussen, ort) is { } o && o["modell"] is string m && GebaeudeTuer.Teile(aussen, m).Grund == Alt;

    /// <summary>Platz fuer die Tuer am neuen Haus (auf einer Kopie gerechnet, blockiert: im Hintergrund rufen); null = Mitte.</summary>
    public static TuerFassade.Platz? Platz(Projekt p, PyObjekt aussen, string ort)
    {
        if (!KnopfZeigen(aussen, ort))
        {
            return null;
        }
        var k = (PyObjekt)Py.Kopie(aussen)!;
        var s = Stelle(ort);
        Vorbereiten(k, s);
        return GebaeudeTuer.EingangPlatz(p, k, s, EndlageAlt.Value ? null : Endlage(k, s));
    }

    /// <summary>Gegenprobe: Tuer wie „Tür dazu“ vor dem Drehen ans Gleis, Weg nicht neu (erster Entwurf, Weg erreichte keine Strasse).</summary>
    public static bool EndlageAltFuerTests { get => EndlageAlt.Value; set => EndlageAlt.Value = value; }
    private static readonly AsyncLocal<bool> EndlageAlt = new();

    /// <summary>
    /// Wohin ein Punkt des gerade gedrehten Hauses kommt, wenn es danach ans Gleis gelegt wird (auf einer Kopie gerechnet);
    /// null, wenn es nicht ans Gleis rueckt.
    /// </summary>
    private static Func<Vector2, Vector2>? Endlage(PyObjekt aussen, AussenStelle s)
    {
        var k = (PyObjekt)Py.Kopie(aussen)!;
        var vorher = AussenBearbeitung.HausMitte(k, s);
        if (!AussenBearbeitung.BahnhofAnGleis(k, s))
        {
            return null;
        }
        var nachher = AussenBearbeitung.HausMitte(k, s);
        var lage = new Hausdrehung.Feinlage(nachher, Hausdrehung.Winkel(AussenBearbeitung.HausDreh(k, s), AussenBearbeitung.HausGrad(k, s)));
        return q => lage.Punkt(q - vorher + nachher);
    }

    /// <summary>
    /// Das neue Aussehen setzen (ein Schritt, Platz aus <see cref="Platz"/> auf demselben Stand). Rueckgabe: Hinweis
    /// (nicht am Gleis, Weg ohne Strasse) oder null. Ohne alten Haltepunkt aendert sich nichts.
    /// </summary>
    public static string? Tauschen(PyObjekt aussen, string ort, TuerFassade.Platz? platz)
    {
        if (!KnopfZeigen(aussen, ort))
        {
            return Texte.AussehenKeinOrt;
        }
        var s = Stelle(ort);
        Vorbereiten(aussen, s);
        var hinweis = platz is null ? null : GebaeudeTuer.EingangAn(aussen, s, platz);
        var gerueckt = AussenBearbeitung.BahnhofAnGleis(aussen, s);
        if (gerueckt && platz is not null && !EndlageAlt.Value)
        {
            hinweis = WegNeu(aussen, s) ? null : Texte.TuerWegOhneStrasse;
        }
        return !(gerueckt || AmGleis(aussen, s)) ? Texte.HaltepunktNichtAmGleis : hinweis;
    }

    /// <summary>
    /// Nach dem Ruecken ans Gleis den Weg der Tuer neu fuehren wie „Tür setzen“ (rechtwinklig zur Hauswand, letztes Stueck quer
    /// auf die Strasse): gerechnet im Rahmen des Hauses (Viertel), Strassen und Laternen dorthin zurueckgedreht. true = Strasse
    /// erreicht.
    /// </summary>
    private static bool WegNeu(PyObjekt aussen, AussenStelle s)
    {
        if (Get(Aussen.Stadtdaten(aussen), "zugaenge_neu") is not PyObjekt zn || Get(zn, s.Ort) is not PyObjekt zugang)
        {
            return false;
        }
        var lage = AussenBearbeitung.HausLage(aussen, s);
        var (x, y, w, h) = AussenBearbeitung.HausViertel(aussen, s);
        var tuerL = (List<object?>)zugang["tuer"]!;
        var tuer = lage.Zurueck(new Vector2((float)Py.F(tuerL[0]), (float)Py.F(tuerL[1])));
        // Aussenseite = naechste Seite des Rechtecks
        var seiten = new (float D, Vector2 N)[]
        {
            (MathF.Abs(tuer.X - (float)x), new Vector2(-1, 0)), (MathF.Abs(tuer.X - (float)(x + w)), new Vector2(1, 0)),
            (MathF.Abs(tuer.Y - (float)y), new Vector2(0, -1)), (MathF.Abs(tuer.Y - (float)(y + h)), new Vector2(0, 1)),
        };
        var n = seiten.MinBy(q => q.D).N;
        static List<object?> P(Vector2 p) => [Bearbeitung.Zahl(Math.Round(p.X, 3)), Bearbeitung.Zahl(Math.Round(p.Y, 3))];
        var probe = new PyObjekt { { "tuer", P(tuer) }, { "weg", new List<object?> { P(tuer), P(tuer + (n * GebaeudeTuer.WegStart)) } } };
        var strassen = AussenBearbeitung.Strassen(aussen).Select(l => l.Select(lage.Zurueck).ToList()).ToList();
        var laternen = Aussen.Laternen(aussen).OfType<List<object?>>().Select(l => lage.Zurueck(new Vector2((float)Py.F(l[0]), (float)Py.F(l[1])))).ToList();
        if (!GebaeudeTuer.WegRechtwinklig(probe, (x, y, w, h), strassen, laternen))
        {
            return false;
        }
        var weg = ((List<object?>)probe["weg"]!).OfType<List<object?>>()
            .Select(q => (object?)P(lage.Punkt(new Vector2((float)Py.F(q[0]), (float)Py.F(q[1]))))).ToList();
        // die Tuer bleibt genau, wo sie war
        weg[0] = new List<object?> { tuerL[0], tuerL[1] };
        Py.Setzen(zugang, "weg", weg);
        Py.Setzen(zugang, "radien", probe["radien"]);
        return true;
    }

    private static AussenStelle Stelle(string ort) => new(AussenArt.Haus, -1, 0, ort);

    /// <summary>Gerade drehen, Modell tauschen (Mitte der Bloecke bleibt an derselben Stelle).</summary>
    private static void Vorbereiten(PyObjekt aussen, AussenStelle s)
    {
        var winkel = Hausdrehung.Winkel(AussenBearbeitung.HausDreh(aussen, s), AussenBearbeitung.HausGrad(aussen, s));
        if (winkel != 0)
        {
            AussenBearbeitung.Drehen(aussen, s, 360 - winkel);
        }
        var o = AussenBearbeitung.Ort(aussen, s.Ort)!;
        var mitte = AussenBearbeitung.HausMitte(aussen, s);
        Zusatzmodelle.Uebernehmen(aussen, Zusatzmodelle.HaltepunktFrei);
        Py.Setzen(o, "modell", Zusatzmodelle.HaltepunktFrei);
        var neu = Hausdrehung.Mitte(Get(AussenBearbeitung.Modelle(aussen), Zusatzmodelle.HaltepunktFrei));
        Py.Setzen(o, "x", Bearbeitung.Zahl(mitte.X - neu.X));
        Py.Setzen(o, "y", Bearbeitung.Zahl(mitte.Y - neu.Y));
        // der Versatz der alten Tuer gehoert zum alten Modell
        if (Get(Aussen.Stadtdaten(aussen), "darstellung") is PyObjekt d && Get(d, "modell_tuer") is PyObjekt mt)
        {
            mt.RemoveAll(e => e.Key == s.Ort);
        }
    }

    /// <summary>Liegt die Mitte des Hauses hoechstens <see cref="GleisNah"/> von einer Bahn (dann lag es schon richtig)?</summary>
    private static bool AmGleis(PyObjekt aussen, AussenStelle s)
    {
        var m = AussenBearbeitung.HausMitte(aussen, s);
        return Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "bahn"))
            .Any(t => Kurve.Abstand(m, ((List<object?>)t["punkte"]!).OfType<List<object?>>().Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1]))).ToList()) <= GleisNah);
    }

    /// <summary>So nah liegt die Mitte eines Haltepunkts am Gleis (Haeuschen hinter einem Bahnsteig von 0,8).</summary>
    public const float GleisNah = 1.5f;
}
