using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 1.2.0 (Weisung P12-332, Kurzplan P12-205 Weg A, Nico 1a: „Ich wollte mehr Platz nach links und rechts zur Wand haben.
/// Er sollte nicht laenger werden, nur breiter“): der Flur eines Innenraums wird breiter, seine Laenge bleibt. Jeder Grundriss
/// ist ein Band: Raeume oben, der Flur quer durch das ganze Haus, Raeume unten. Die Wand unter dem Flur rueckt mit allen Raeumen
/// darunter (Tueren, Einrichtung, Personen, Fenster) um <see cref="Umrechnung.Zuwachs"/> nach aussen; die Raeume behalten ihre
/// Groesse, das Haus wird innen tiefer. Die Grundrisse in <c>inhalte/</c> (Sollwerte), die Regeln des Spiel-Kerns und die
/// Spielstaende bleiben in den alten Zahlen: nur der Innenraum (Darstellung, Laufen, Kamera) rechnet mit dem breiten Grundriss,
/// und jede Stelle, die einen Punkt zwischen Spiel-Kern und Innenraum reicht, rechnet ueber <see cref="Umrechnung.Hin"/> und
/// <see cref="Umrechnung.Zurueck"/>. Zielbreite: Wohnungen 2,2 Einheiten (1,32 m), alle anderen Flure mindestens 3 Einheiten
/// (1,80 m); Laeden, Schalter und Obergeschosse (vom Werkzeug gebaut) bleiben wie sie sind.
/// </summary>
public static class Flurbreite
{
    /// <summary>Breite der Diele einer Wohnung (Einheiten).</summary>
    public const double Diele = 2.2;

    /// <summary>Mindestbreite jedes anderen Flurs (Einheiten).</summary>
    public const double Flur = 3.0;

    private static readonly AsyncLocal<bool> Aus = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): kein Flur wird breiter (Stand bis A 1.1.0).</summary>
    public static bool AusFuerTests { get => Aus.Value; set => Aus.Value = value; }

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

    /// <summary>Gilt die Verbreiterung fuer diese Ansicht und diesen Ort? (nie in Laeden, Schaltern und Obergeschossen)</summary>
    public static bool Gilt(string ansicht, string ort) => !Aus.Value && !SpielKern.Laeden.Contains(ort) && !SpielKern.SchalterOrte.Contains(ort)
        && Stadtbau.Etagen.Teilen(ansicht).Etage == 0;

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

    /// <summary>Breiter Grundriss (Kopie; der alte bleibt unveraendert).</summary>
    public static PyObjekt Anwenden(PyObjekt gebaeude, Umrechnung u)
    {
        if (u.Zuwachs <= 0)
        {
            return gebaeude;
        }
        var g = (PyObjekt)Py.Kopie(gebaeude)!;
        Py.Setzen(g, "hoehe", Py.F(g["hoehe"]) + u.Zuwachs);
        var flur = Objekt(g["flur"]);
        Py.Setzen(flur, "h", u.Neu);
        if (Get(flur, "spieler") is List<object?> { Count: >= 2 } sp)
        {
            sp[1] = u.Hin(Py.F(sp[1]));
        }
        if (Get(flur, "eingang") is PyObjekt ein)
        {
            var v = u.Verschiebung(Py.F(ein["von"]), Py.F(ein["bis"]) - Py.F(ein["von"]));
            Py.Setzen(ein, "von", Py.F(ein["von"]) + v);
            Py.Setzen(ein, "bis", Py.F(ein["bis"]) + v);
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
                Py.Setzen(r, "h", Py.F(r["h"]) + u.Zuwachs);
                foreach (var d in ListeOderLeer(r, "deko").Select(Objekt))
                {
                    Rechteck(d, u);
                }
                if (Get(r, "tuer") is PyObjekt t && Get(t, "seite") is string seite && seite is "w" or "o")
                {
                    var v = u.Verschiebung(Py.F(t["von"]), Py.F(t["bis"]) - Py.F(t["von"]));
                    Py.Setzen(t, "von", Py.F(t["von"]) + v);
                    Py.Setzen(t, "bis", Py.F(t["bis"]) + v);
                }
                continue;
            }
            if (Py.F(r["y"]) < u.Unten - 1e-6)
            {
                continue;
            }
            Py.Setzen(r, "y", Py.F(r["y"]) + u.Zuwachs);
            foreach (var d in ListeOderLeer(r, "deko").Select(Objekt))
            {
                Py.Setzen(d, "y", Py.F(d["y"]) + u.Zuwachs);
            }
        }
        foreach (var f in ListeOderLeer(g, "rolltor").OfType<List<object?>>().Where(f => f.Count >= 4))
        {
            // Rolltor: bleibt gleich gross an seiner Wand (wie ein Rechteck im Flur)
            var (y1, y2) = (Py.F(f[1]), Py.F(f[3]));
            var v = u.Verschiebung(Math.Min(y1, y2), Math.Abs(y2 - y1));
            f[1] = y1 + v;
            f[3] = y2 + v;
        }
        foreach (var feld in new[] { "fenster" })
        {
            foreach (var f in ListeOderLeer(g, feld).OfType<List<object?>>())
            {
                for (var i = 1; i < f.Count; i += 2)
                {
                    f[i] = u.Hin(Py.F(f[i]));
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
    /// Nur Darstellung und Laufen im Innenraum: die Mitte (Wege des Spiel-Kerns) bleibt, wo es geht. Kopie; leer = nichts zu tun.
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
            Py.Setzen(t, "von", Math.Round(von, 6));
            Py.Setzen(t, "bis", Math.Round(von + breite, 6));
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
        Py.Setzen(d, "y", Py.F(d["y"]) + u.Verschiebung(Py.F(d["y"]), h));
    }

    /// <summary>Person mit Platz im Innenraum (Kopie ihrer Daten; der Spielstand bleibt).</summary>
    public static InnenPerson Person(InnenPerson p, Umrechnung u)
    {
        if (u.Zuwachs <= 0)
        {
            return p;
        }
        var daten = Py.Flach(p.Daten);
        var z = u.Hin(p.Z);
        if (Get(daten, "platz") is List<object?> { Count: >= 2 } pl)
        {
            Py.Setzen(daten, "platz", new List<object?> { pl[0], z });
        }
        return p with { Z = (float)z, Daten = daten };
    }
}
