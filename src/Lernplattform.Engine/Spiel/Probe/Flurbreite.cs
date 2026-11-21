using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 1.2.0 (Weisung P12-332, Nico 1a: „Ich wollte mehr Platz nach links und rechts zur Wand haben.
/// Er sollte nicht laenger werden, nur breiter“): der Flur eines Innenraums wird breiter, seine Laenge bleibt. Jeder Grundriss
/// ist ein Band: Raeume oben, der Flur quer durch das ganze Haus, Raeume unten. Die Wand unter dem Flur rueckt mit allen Raeumen
/// darunter (Tueren, Einrichtung, Personen, Fenster) um <see cref="Umrechnung.Zuwachs"/> nach aussen; die Raeume behalten ihre
/// Groesse, das Haus wird innen tiefer. Zielbreite: Wohnungen 2,2 Einheiten (1,32 m), alle anderen Flure mindestens 3 Einheiten
/// (1,80 m). Weisung P12-334 (Nico: Weg B „Neue Grundrisse als Daten“): diese Rechnung laeuft nie zur Laufzeit, sie erzeugt nur
/// die neuen Grundrisse in <c>inhalte_csharp/spiel/grundrisse_breit.json</c> (<see cref="GrundrisseBreit"/>), die Spiel und
/// Werkzeug gleich lesen; Laeden, Schalter und Obergeschosse stehen dort nicht und bleiben.
/// </summary>
public static class Flurbreite
{
    /// <summary>Breite der Diele einer Wohnung (Einheiten).</summary>
    public const double Diele = 2.2;

    /// <summary>Mindestbreite jedes anderen Flurs (Einheiten).</summary>
    public const double Flur = 3.0;

    /// <summary>Zielbreite des Flurs (Einheiten) fuer den Ort und die heutige Breite; 0 = bleibt.</summary>
    public static double Ziel(string ort, double heute) => ort == OrtZuhause ? Math.Max(Diele, heute) : heute < Flur ? Flur : 0;

    /// <summary>
    /// Umrechnung eines Grundrisses mit Flur von <see cref="Oben"/> bis <see cref="Unten"/> (alt) auf die neue Breite. Leer
    /// (<see cref="Zuwachs"/> 0): alles bleibt. Punkte im Flur werden gleichmaessig gestreckt, Punkte darunter um den Zuwachs verschoben.
    /// </summary>
    public sealed record Umrechnung(double Oben, double Unten, double Zuwachs)
    {
        /// <summary>Keine Aenderung.</summary>
        public static readonly Umrechnung Keine = new(0, 0, 0);

        /// <summary>Breite des Flurs alt (Einheiten).</summary>
        public double Alt => Unten - Oben;

        /// <summary>Breite des Flurs neu (Einheiten).</summary>
        public double Neu => Alt + Zuwachs;

        /// <summary>y im alten Grundriss (Spiel-Kern, Spielstand) zu y im Innenraum.</summary>
        public double Hin(double y) => Zuwachs <= 0 || y <= Oben ? y : y >= Unten ? y + Zuwachs : Oben + ((y - Oben) * Neu / Alt);

        /// <summary>y im Innenraum zu y im alten Grundriss.</summary>
        public double Zurueck(double y) => Zuwachs <= 0 || y <= Oben ? y : y >= Unten + Zuwachs ? y - Zuwachs : Oben + ((y - Oben) * Alt / Neu);

        /// <summary>
        /// Verschiebung eines Rechtecks (alt, y oben, Hoehe h): unter dem Flur um den Zuwachs; im Flur bleibt es an seiner Wand
        /// (Mitte in der oberen Haelfte: 0, in der unteren: der Zuwachs, in der Mitte (+-0,1): die Haelfte, z. B. die Eingangstuer).
        /// </summary>
        public double Verschiebung(double y, double h)
        {
            var m = y + (h / 2);
            var mitte = (Oben + Unten) / 2;
            return Zuwachs <= 0 || m <= Oben ? 0 : m >= Unten || m > mitte + 0.1 ? Zuwachs : m < mitte - 0.1 ? 0 : Zuwachs / 2;
        }
    }

    /// <summary>Umrechnung fuer einen Grundriss (Keine, wenn es keinen Flur gibt, er schon breit genug ist oder ein Raum ihn quert).</summary>
    public static Umrechnung Fuer(string ort, PyObjekt gebaeude)
    {
        if (Get(gebaeude, "flur") is not PyObjekt flur || Get(flur, "y") is null || Get(flur, "h") is null)
        {
            return Umrechnung.Keine;
        }
        var oben = Py.F(flur["y"]);
        var h = Py.F(flur["h"]);
        var ziel = Ziel(ort, h);
        if (ziel <= h + 1e-9 || h <= 0)
        {
            return Umrechnung.Keine;
        }
        var unten = oben + h;
        // ein Raum, der in den Flur hineinragt, ohne ihn ganz zu queren, laesst sich nicht strecken: dann bleibt alles
        // (Test prueft, dass es keinen gibt); ein Raum, der ihn ganz quert (Lager der Bitweiche), wird mit ihm tiefer
        if (ListeOderLeer(gebaeude, "raeume").Select(Objekt).Any(r => Quert(r, oben, unten) && (Py.F(r["y"]) > oben + 1e-6 || Py.F(r["y"]) + Py.F(r["h"]) < unten - 1e-6)))
        {
            return Umrechnung.Keine;
        }
        return new Umrechnung(oben, unten, ziel - h);
    }

    /// <summary>Neuer Wert mit dem Python-Typ des alten: ganze Zahl bleibt ganze Zahl, wenn das Ergebnis ganz ist (json 1 statt 1.0).</summary>
    public static object Zahl(double neu, object? alt) =>
        alt is long && Math.Abs(neu - Math.Round(neu)) < 1e-9 ? (object)(long)Math.Round(neu) : Math.Round(neu, 6);

    /// <summary>Breiter Grundriss (Kopie; der alte bleibt unveraendert).</summary>
    public static PyObjekt Anwenden(PyObjekt gebaeude, Umrechnung u)
    {
        if (u.Zuwachs <= 0)
        {
            return gebaeude;
        }
        var g = (PyObjekt)Py.Kopie(gebaeude)!;
        Py.Setzen(g, "hoehe", Zahl(Py.F(g["hoehe"]) + u.Zuwachs, g["hoehe"]));
        var flur = Objekt(g["flur"]);
        Py.Setzen(flur, "h", Zahl(u.Neu, flur["h"]));
        if (Get(flur, "spieler") is List<object?> { Count: >= 2 } sp)
        {
            sp[1] = Zahl(u.Hin(Py.F(sp[1])), sp[1]);
        }
        if (Get(flur, "eingang") is PyObjekt ein)
        {
            var v = u.Verschiebung(Py.F(ein["von"]), Py.F(ein["bis"]) - Py.F(ein["von"]));
            Py.Setzen(ein, "von", Zahl(Py.F(ein["von"]) + v, ein["von"]));
            Py.Setzen(ein, "bis", Zahl(Py.F(ein["bis"]) + v, ein["bis"]));
        }
        foreach (var d in ListeOderLeer(flur, "deko").Select(Objekt))
        {
            Rechteck(d, u);
        }
        foreach (var r in ListeOderLeer(g, "raeume").Select(Objekt))
        {
            if (Quert(r, u.Oben, u.Unten))
            {
                // quert den Flur ganz: Raum wird um den Zuwachs tiefer, Einrichtung bleibt an ihrer Wand, Seitentuer im Flur mittig
                Py.Setzen(r, "h", Zahl(Py.F(r["h"]) + u.Zuwachs, r["h"]));
                foreach (var d in ListeOderLeer(r, "deko").Select(Objekt))
                {
                    Rechteck(d, u);
                }
                if (Get(r, "tuer") is PyObjekt t && Get(t, "seite") is string seite && seite is "w" or "o")
                {
                    var v = u.Verschiebung(Py.F(t["von"]), Py.F(t["bis"]) - Py.F(t["von"]));
                    Py.Setzen(t, "von", Zahl(Py.F(t["von"]) + v, t["von"]));
                    Py.Setzen(t, "bis", Zahl(Py.F(t["bis"]) + v, t["bis"]));
                }
                continue;
            }
            if (Py.F(r["y"]) < u.Unten - 1e-6)
            {
                continue;
            }
            Py.Setzen(r, "y", Zahl(Py.F(r["y"]) + u.Zuwachs, r["y"]));
            foreach (var d in ListeOderLeer(r, "deko").Select(Objekt))
            {
                Py.Setzen(d, "y", Zahl(Py.F(d["y"]) + u.Zuwachs, d["y"]));
            }
        }
        foreach (var f in ListeOderLeer(g, "rolltor").OfType<List<object?>>().Where(f => f.Count >= 4))
        {
            // Rolltor: bleibt gleich gross an seiner Wand (wie ein Rechteck im Flur)
            var (y1, y2) = (Py.F(f[1]), Py.F(f[3]));
            var v = u.Verschiebung(Math.Min(y1, y2), Math.Abs(y2 - y1));
            f[1] = Zahl(y1 + v, f[1]);
            f[3] = Zahl(y2 + v, f[3]);
        }
        foreach (var feld in new[] { "fenster" })
        {
            foreach (var f in ListeOderLeer(g, feld).OfType<List<object?>>())
            {
                for (var i = 1; i < f.Count; i += 2)
                {
                    f[i] = Zahl(u.Hin(Py.F(f[i])), f[i]);
                }
            }
        }
        return g;
    }

    /// <summary>Mindestbreite jeder Innentuer (Einheiten, 0,90 m; Weisung P12-332, Chat 3a).</summary>
    public const double Tuer = 1.5;

    /// <summary>Abstand einer verbreiterten Tuer zur Ecke ihres Raums (Einheiten).</summary>
    public const double TuerEcke = 0.1;

    /// <summary>
    /// Welle A 1.2.0 (Weisung P12-332, Chat 3a): jede Innentuer (nicht offene Durchgaenge) wird mindestens <see cref="Tuer"/> breit,
    /// gleichmaessig um ihre Mitte; reicht die Wand ihres Raums dafuer nicht, rueckt sie von der Ecke weg (<see cref="TuerEcke"/>).
    /// Die Mitte bleibt, wo es geht (Wege und Stellen bleiben gueltig). Kopie; leer = nichts zu tun.
    /// </summary>
    public static PyObjekt Tueren(PyObjekt gebaeude)
    {
        var raeume = ListeOderLeer(gebaeude, "raeume").Select(Objekt).ToList();
        if (!raeume.Any(Schmal))
        {
            return gebaeude;
        }
        var g = (PyObjekt)Py.Kopie(gebaeude)!;
        foreach (var r in ListeOderLeer(g, "raeume").Select(Objekt).Where(Schmal))
        {
            var t = Objekt(r["tuer"]);
            var quer = Get(t, "seite") is string seite && seite is "w" or "o";
            var (a, l) = quer ? (Py.F(r["y"]), Py.F(r["h"])) : (Py.F(r["x"]), Py.F(r["w"]));
            var mitte = (Py.F(t["von"]) + Py.F(t["bis"])) / 2;
            var breite = Math.Min(Tuer, l - (2 * TuerEcke));
            var von = Math.Clamp(mitte - (breite / 2), a + TuerEcke, a + l - TuerEcke - breite);
            Py.Setzen(t, "von", Zahl(von, t["von"]));
            Py.Setzen(t, "bis", Zahl(von + breite, t["bis"]));
        }
        return g;
    }

    /// <summary>Hat der Raum eine Tuer (kein offener Durchgang) schmaler als <see cref="Tuer"/>?</summary>
    private static bool Schmal(PyObjekt r) => Get(r, "tuer") is PyObjekt t && Get(t, "von") is not null && Get(t, "bis") is not null
        && !Py.Wahr(Get(t, "offen")) && Py.F(t["bis"]) - Py.F(t["von"]) < Tuer - 1e-9;

    /// <summary>Quert der Raum den Flur (oben bis unten, alt)?</summary>
    private static bool Quert(PyObjekt r, double oben, double unten) =>
        Py.F(r["y"]) < unten - 1e-6 && Py.F(r["y"]) + Py.F(r["h"]) > oben + 1e-6;

    /// <summary>Rechteck (x, y, w, h) im Flur: bleibt an seiner Wand (<see cref="Umrechnung.Verschiebung"/>, Groesse gleich).</summary>
    private static void Rechteck(PyObjekt d, Umrechnung u)
    {
        if (Get(d, "y") is null)
        {
            return;
        }
        var h = Get(d, "h") is { } hh ? Py.F(hh) : 0;
        Py.Setzen(d, "y", Zahl(Py.F(d["y"]) + u.Verschiebung(Py.F(d["y"]), h), d["y"]));
    }
}
