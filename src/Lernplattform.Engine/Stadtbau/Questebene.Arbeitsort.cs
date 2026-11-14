using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle E4 „Aufgaben an Gebäuden“ (A 0.75.0 / S 0.53.0, Weisung P12-247, Kurzplan Meldung P12-147, Nico 1a 2a 3c 4b 5a):
/// eine NEUE Quest (1a) kann einen eigenen Arbeitsort haben, getrennt vom Platz ihres Questgebers. Im Eintrag der Quest in
/// <c>stadtdaten.quests</c> steht dann das Objekt <c>arbeitsort</c> mit <c>ort</c> (Spiel-Ort: Büro, Kunden-Ort, Geschäft
/// oder Schalter, 2a; nie draussen, nie ein selbst gebautes Gebäude), <c>raum</c> (Raum im Büro oder beim Kunden; im
/// Geschäft und am Schalter leer = der ganze Laden), <c>etage</c>, <c>x</c>/<c>y</c> (die Stelle) und <c>wartet</c>
/// (3c: wartet der Questgeber dort, solange die Quest offen ist).
/// <para>Im Spiel wird daraus eine Aufgabe mit <c>ort</c>/<c>raum</c> wie die vorhandene Spielgeschichte: wartet = ja
/// schreibt <c>stelle</c> (und <c>etage</c>), dann stellt <see cref="SpielOrte"/> den Questgeber dorthin (dieselbe Regel wie
/// „IP-Plan für Talheim-Nord“); wartet = nein schreibt <see cref="FeldOhneQuestgeber"/> und <see cref="FeldArbeitsstelle"/>,
/// dann bleibt der Questgeber an seinem Platz und der Auftrag liegt im Raum am Arbeitsort (<see cref="Innenraum"/>).
/// Keine neue Ereignisart, kein Feld im Spielstand.</para>
/// <para>Eine Stadt-Datei mit Arbeitsort hat die Formatnummer 2 (<see cref="StadtFassung.FormatFuer"/>), sonst bleibt
/// alles bitgleich; ein älteres Programm lehnt sie mit klarem Grund ab (nie still weg).</para>
/// </summary>
public static partial class Questebene
{
    /// <summary>Feld des Arbeitsorts im Eintrag einer neuen Quest.</summary>
    public const string FeldArbeitsort = "arbeitsort";

    /// <summary>Merkmal an einer Aufgabe: der Auftrag liegt am Arbeitsort, der Questgeber wartet dort nicht (3c = nein).</summary>
    public const string FeldOhneQuestgeber = "ohne_questgeber";

    /// <summary>Stelle des Arbeitsorts an einer Aufgabe ohne wartenden Questgeber (nur fuer das Antippen im Geschaeft).</summary>
    public const string FeldArbeitsstelle = "arbeitsstelle";

    /// <summary>So nah muss die Figur im Geschaeft an der Stelle eines Auftrags ohne Questgeber stehen (Feldmass).</summary>
    public const double ArbeitsortReichweite = 1.5;

    /// <summary>Art eines Arbeitsorts.</summary>
    public enum ArbeitsortArt
    {
        Buero,
        Kunde,
        Geschaeft,
    }

    /// <summary>Ein Ort, der Arbeitsort sein kann; <paramref name="FreiAb"/> = ab welchem Spieltag es ihn gibt.</summary>
    public sealed record ArbeitsortWahl(string Ort, ArbeitsortArt Art, long FreiAb);

    /// <summary>Gelesener Arbeitsort einer Quest.</summary>
    public sealed record Arbeitsort(string Ort, string Raum, int Etage, double X, double Y, bool Wartet);

    /// <summary>Die Orte, die Arbeitsort sein koennen (2a): Buero, Kunden-Orte, Geschaefte und Schalter mit Innenraum.</summary>
    public static List<ArbeitsortWahl> Arbeitsorte(PyObjekt inhalt, PyObjekt? innenraeume)
    {
        var liste = new List<ArbeitsortWahl>();
        if (inhalt["gebaeude"] is PyObjekt)
        {
            liste.Add(new ArbeitsortWahl(OrtBuero, ArbeitsortArt.Buero, 1));
        }
        foreach (var k in Kundenorte(inhalt))
        {
            if (k["id"] is string id && k["gebaeude"] is PyObjekt)
            {
                liste.Add(new ArbeitsortWahl(id, ArbeitsortArt.Kunde, Get(k, "ab_tag", 1L) is long t ? t : 1));
            }
        }
        foreach (var ort in SpielKern.Laeden.Concat(SpielKern.SchalterOrte))
        {
            if (innenraeume is not null && Get(innenraeume, ort) is PyObjekt laden && laden["gebaeude"] is PyObjekt)
            {
                liste.Add(new ArbeitsortWahl(ort, ArbeitsortArt.Geschaeft, 1));
            }
        }
        return liste;
    }

    /// <summary>Hat irgendeine Quest der Stadt einen Arbeitsort? (Formatnummer 2 der Stadt-Datei, 4 des Projekts.)</summary>
    public static bool HatArbeitsort(PyObjekt? stadtdaten) =>
        stadtdaten is not null && Get(stadtdaten, AbschnittQuests) is List<object?> l && l.OfType<PyObjekt>().Any(e => Py.Hat(e, FeldArbeitsort));

    /// <summary>Arbeitsort aus dem Eintrag lesen (nur Form, keine Pruefung gegen das Spiel); null = fehlt oder nicht lesbar.</summary>
    public static Arbeitsort? ArbeitsortLesen(PyObjekt quest)
    {
        if (Get(quest, FeldArbeitsort) is not PyObjekt a || Get(a, "ort") is not string { Length: > 0 } ort || Get(a, "raum", "") is not string raum
            || Get(a, "etage", 0L) is not long etage || etage < 0 || Get(a, "x") is not (long or double) || Get(a, "y") is not (long or double)
            || Get(a, "wartet", true) is not bool wartet || a.Any(p => p.Key is not ("ort" or "raum" or "etage" or "x" or "y" or "wartet")))
        {
            return null;
        }
        return new Arbeitsort(ort, raum, (int)etage, Py.F(a["x"]), Py.F(a["y"]), wartet);
    }

    /// <summary>Begehbare Zellen eines Grundrisses, die vom Eingang (Startplatz) aus zu Fuss erreichbar sind.</summary>
    public static HashSet<(long, long)> Erreichbar(PyObjekt grundriss)
    {
        var frei = Wege.BasisRaster(grundriss);
        var ergebnis = new HashSet<(long, long)>();
        var (sx, sy) = Wege.StartPlatz(grundriss);
        if (Wege.NaechsteFrei(sx, sy, frei) is not { } start)
        {
            return ergebnis;
        }
        var offen = new Queue<(long, long)>();
        offen.Enqueue(start);
        ergebnis.Add(start);
        while (offen.Count > 0)
        {
            var (s, z) = offen.Dequeue();
            foreach (var n in ((long, long)[])[(s + 1, z), (s - 1, z), (s, z + 1), (s, z - 1)])
            {
                if (frei.Contains(n) && ergebnis.Add(n))
                {
                    offen.Enqueue(n);
                }
            }
        }
        return ergebnis;
    }

    /// <summary>
    /// So weit liegt eine Stelle am Arbeitsort mindestens von jedem Hindernis (Wand, Einrichtung als Rechteck des Grundrisses;
    /// die Modelle fuellen ihr Rechteck): die stehende Figur (halbe Breite knapp 0,5) durchdringt dann nichts.
    /// </summary>
    public const double ArbeitsortFrei = 0.7;

    /// <summary>
    /// Stuehle ragen ueber das Rechteck ihres Tischs hinaus (Schreibtisch 0,76 vor der Kante, Besprechungs- und Esstisch
    /// 0,7 auf beiden langen Seiten, <see cref="Innenraum"/>): um so viel waechst das Rechteck dieser Tische.
    /// </summary>
    public const double StuhlUeberstand = 0.8;

    /// <summary>Tische, an denen im Innenraum Stuehle stehen.</summary>
    public static readonly IReadOnlyList<string> TischeMitStuhl = ["schreibtisch", "besprechungstisch", "esstisch"];

    /// <summary>So weit liegt eine Stelle mindestens von jeder anderen Person am Ort (Ansprechen bleibt eindeutig).</summary>
    public const double ArbeitsortPersonAbstand = 1.6;

    /// <summary>
    /// Die Stellen, an denen gearbeitet werden kann: erreichbar, mindestens <see cref="ArbeitsortFrei"/> von jedem Hindernis
    /// (Tische mit Stuhl um <see cref="StuhlUeberstand"/> groesser) und <see cref="ArbeitsortPersonAbstand"/> von jeder Person
    /// in <paramref name="personen"/>.
    /// </summary>
    public static HashSet<(long, long)> Stellen(PyObjekt grundriss, IReadOnlyList<(double X, double Y)> personen)
    {
        var hindernisse = Wege.Hindernisse(grundriss);
        foreach (var d in ListeOderLeer(grundriss, "raeume").Select(Objekt).SelectMany(r => ListeOderLeer(r, "deko").Select(Objekt))
            .Concat(grundriss["flur"] is PyObjekt flur ? ListeOderLeer(flur, "deko").Select(Objekt) : []))
        {
            if (TischeMitStuhl.Contains(Py.Str(d["typ"])))
            {
                double x = Py.F(d["x"]), y = Py.F(d["y"]);
                hindernisse.Add((x - StuhlUeberstand, y - StuhlUeberstand, x + Py.F(d["w"]) + StuhlUeberstand, y + Py.F(d["h"]) + StuhlUeberstand));
            }
        }
        var ergebnis = new HashSet<(long, long)>();
        foreach (var zelle in Erreichbar(grundriss))
        {
            var (cx, cy) = Wege.Mitte(zelle);
            if (hindernisse.All(h => Abstand(h, cx, cy) >= ArbeitsortFrei)
                && personen.All(p => ((p.X - cx) * (p.X - cx)) + ((p.Y - cy) * (p.Y - cy)) >= ArbeitsortPersonAbstand * ArbeitsortPersonAbstand))
            {
                ergebnis.Add(zelle);
            }
        }
        return ergebnis;
    }

    private static double Abstand((double X1, double Y1, double X2, double Y2) r, double x, double y)
    {
        var dx = Math.Max(Math.Max(r.X1 - x, 0), x - r.X2);
        var dy = Math.Max(Math.Max(r.Y1 - y, 0), y - r.Y2);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Personen, die am Arbeitsort <paramref name="ort"/> (Etage <paramref name="etage"/>) stehen koennen: Kolleginnen und
    /// Kunden mit Platz dort, Verkaufspersonen des Geschaefts, wartende Personen vorhandener Auftraege und Questgeber mit
    /// Platz dort; ohne <paramref name="ausser"/> (der Questgeber der Quest selbst). Bewusst alle (auch die nur an manchen
    /// Tagen da sind).
    /// </summary>
    public static List<(double X, double Y)> ArbeitsortPersonen(string ort, int etage, PyObjekt inhalt, PyObjekt? innenraeume, PyObjekt? stadtdaten, string? ausser)
    {
        var liste = new List<(double, double)>();
        void Platz(object? p)
        {
            if (p is List<object?> { Count: >= 2 } l && l[0] is long or double && l[1] is long or double)
            {
                liste.Add((Py.F(l[0]), Py.F(l[1])));
            }
        }
        foreach (var p in AllePersonen(inhalt))
        {
            if (!Py.Gleich(p["id"], ausser) && Py.Gleich(Get(p, "ort", OrtBuero), ort) && Etage(p) == etage)
            {
                Platz(Get(p, "platz"));
            }
        }
        if (etage == 0 && innenraeume is not null && Get(innenraeume, ort) is PyObjekt laden)
        {
            foreach (var p in ListeOderLeer(laden, "personen").Select(Objekt))
            {
                Platz(Get(p, "platz"));
            }
        }
        if (etage == 0)
        {
            foreach (var a in Liste(inhalt["aufgaben"]).Select(Objekt).Where(a => Py.Wahr(Get(a, "stelle")) && Py.Gleich(AuftragOrt(a, inhalt), ort)))
            {
                if (!Py.Gleich(Get(a, "auftraggeber"), ausser))
                {
                    Platz(a["stelle"]);
                }
            }
        }
        if (stadtdaten is not null && Get(stadtdaten, AbschnittQuestgeber) is List<object?> qg)
        {
            foreach (var q in qg.OfType<PyObjekt>())
            {
                if (!Py.Gleich(Get(q, "id"), ausser) && Get(q, "platz") is PyObjekt pl && !Py.Wahr(Get(pl, "draussen")) && Py.Gleich(Get(pl, "ort"), ort)
                    && (Get(pl, "etage", 0L) is long e ? e : 0) == etage && Get(pl, "x") is long or double && Get(pl, "y") is long or double)
                {
                    liste.Add((Py.F(pl["x"]), Py.F(pl["y"])));
                }
            }
        }
        return liste;
    }

    /// <summary>
    /// Erste freie Stelle im Raum <paramref name="raum"/> (leer = im ganzen Grundriss): die Stelle aus <see cref="Stellen"/>,
    /// die der Mitte des Raums am naechsten liegt (gleich weit: kleinere Spalte, dann Zeile); null = keine. Werte auf 2
    /// Stellen gerundet.
    /// </summary>
    public static (double X, double Y)? ErsteFreieStelle(PyObjekt grundriss, string raum, IReadOnlyList<(double X, double Y)>? personen = null)
    {
        double mx, my;
        if (raum.Length > 0)
        {
            if (ListeOderLeer(grundriss, "raeume").Select(Objekt).FirstOrDefault(r => Py.Gleich(r["id"], raum)) is not { } r)
            {
                return null;
            }
            mx = Py.F(r["x"]) + (Py.F(r["w"]) / 2);
            my = Py.F(r["y"]) + (Py.F(r["h"]) / 2);
        }
        else
        {
            mx = Py.F(grundriss["breite"]) / 2;
            my = Py.F(grundriss["hoehe"]) / 2;
        }
        (long, long)? beste = null;
        var besterAbstand = double.MaxValue;
        foreach (var zelle in Stellen(grundriss, personen ?? []).OrderBy(z => z.Item1).ThenBy(z => z.Item2))
        {
            var (cx, cy) = Wege.Mitte(zelle);
            if (raum.Length > 0 && !Py.Gleich(Wege.RaumBei(cx, cy, grundriss)?["id"], raum))
            {
                continue;
            }
            var d = ((cx - mx) * (cx - mx)) + ((cy - my) * (cy - my));
            if (d < besterAbstand)
            {
                besterAbstand = d;
                beste = zelle;
            }
        }
        if (beste is not { } b)
        {
            return null;
        }
        var (x, y) = Wege.Mitte(b);
        return (Math.Round(x, 2), Math.Round(y, 2));
    }

    /// <summary>Raeume eines Grundrisses (Kennung, Name) in ihrer Reihenfolge.</summary>
    public static List<(string Id, string Name)> Raeume(PyObjekt grundriss) =>
        [.. ListeOderLeer(grundriss, "raeume").Select(Objekt).Where(r => r["id"] is string).Select(r => (Py.Str(r["id"]), Py.Str(Get(r, "name", r["id"]))))];

    /// <summary>
    /// Rote Funde zum Arbeitsort einer neuen Quest (leer = in Ordnung). <paramref name="n"/> = Name der Quest fuer den Text,
    /// <paramref name="questgeberDraussen"/> = ihr Questgeber steht draussen.
    /// </summary>
    public static List<string> ArbeitsortFunde(string n, PyObjekt quest, PyObjekt inhalt, PyObjekt? innenraeume, long abTag, bool questgeberDraussen,
        PyObjekt? stadtdaten = null)
    {
        var funde = new List<string>();
        void R(string text) => funde.Add($"Quest „{n}“: {text}");
        if (ArbeitsortLesen(quest) is not { } a)
        {
            R("der Arbeitsort ist nicht lesbar.");
            return funde;
        }
        if (Arbeitsorte(inhalt, innenraeume).FirstOrDefault(w => w.Ort == a.Ort) is not { } wahl)
        {
            R(a.Ort.StartsWith(BauPraefix, StringComparison.Ordinal)
                ? "ein selbst gebautes Gebäude kann erst mit dem Einbau ins Spiel Arbeitsort sein."
                : $"den Arbeitsort „{a.Ort}“ gibt es im Spiel nicht (möglich sind Büro, Kunden-Orte, Geschäfte und Schalter).");
            return funde;
        }
        if (ArbeitsGrundriss(a.Ort, a.Etage, inhalt, innenraeume) is not { } g)
        {
            R(string.Create(CultureInfo.InvariantCulture, $"die Etage {a.Etage} gibt es am Arbeitsort nicht."));
            return funde;
        }
        if (wahl.Art == ArbeitsortArt.Geschaeft)
        {
            if (a.Raum.Length > 0)
            {
                R("im Geschäft und am Schalter gilt der ganze Raum, ein Raum wird dort nicht gewählt.");
            }
        }
        else if (a.Raum.Length == 0 || !Raeume(g).Any(r => r.Id == a.Raum))
        {
            R($"den Raum „{a.Raum}“ gibt es am Arbeitsort nicht.");
            return funde;
        }
        if (abTag < wahl.FreiAb)
        {
            R(string.Create(CultureInfo.InvariantCulture, $"den Arbeitsort gibt es erst ab Tag {wahl.FreiAb}, die Quest beginnt an Tag {abTag}."));
        }
        if (a.X < 0 || a.Y < 0 || a.X > Py.F(g["breite"]) || a.Y > Py.F(g["hoehe"]))
        {
            R("die Stelle liegt außerhalb des Arbeitsorts.");
        }
        else if (a.Raum.Length > 0 && !Py.Gleich(Wege.RaumBei(a.X, a.Y, g)?["id"], a.Raum))
        {
            R($"die Stelle liegt nicht im Raum „{a.Raum}“.");
        }
        else if (!Wege.BasisRaster(g).Contains(Wege.Zelle(a.X, a.Y)))
        {
            R("an der Stelle am Arbeitsort kann niemand stehen.");
        }
        else if (!Erreichbar(g).Contains(Wege.Zelle(a.X, a.Y)))
        {
            R("die Stelle am Arbeitsort ist vom Eingang aus nicht erreichbar.");
        }
        else if (!Stellen(g, []).Contains(Wege.Zelle(a.X, a.Y)))
        {
            R(string.Create(CultureInfo.InvariantCulture, $"die Stelle am Arbeitsort liegt zu nah an Wand oder Einrichtung (mindestens {ArbeitsortFrei:0.0} Abstand)."));
        }
        else if (!Stellen(g, ArbeitsortPersonen(a.Ort, a.Etage, inhalt, innenraeume, stadtdaten, Get(quest, "questgeber") as string)).Contains(Wege.Zelle(a.X, a.Y)))
        {
            R(string.Create(CultureInfo.InvariantCulture, $"die Stelle am Arbeitsort liegt zu nah an einer anderen Person (mindestens {ArbeitsortPersonAbstand:0.0} Abstand)."));
        }
        if (a.Wartet && questgeberDraussen)
        {
            R("ein Questgeber, der draußen steht, kann nicht am Arbeitsort warten (Schalter „wartet“ auf nein stellen).");
        }
        return funde;
    }

    /// <summary>Grundriss eines Arbeitsorts wie im Spiel (<see cref="Innenraum.ArbeitsGrundriss"/>).</summary>
    public static PyObjekt? ArbeitsGrundriss(string ort, int etage, PyObjekt inhalt, PyObjekt? innenraeume) =>
        Innenraum.ArbeitsGrundriss(ort, etage, inhalt, innenraeume);

    /// <summary>Felder der Aufgabe im Spiel aus dem Arbeitsort (an <see cref="NeueAufgabe"/>).</summary>
    private static void ArbeitsortEinbauen(PyObjekt aufgabe, Arbeitsort a)
    {
        Py.Setzen(aufgabe, "ort", a.Ort);
        Py.Setzen(aufgabe, "raum", a.Raum);
        if (a.Wartet)
        {
            Py.Setzen(aufgabe, "stelle", new List<object?> { a.X, a.Y });
        }
        else
        {
            Py.Setzen(aufgabe, FeldOhneQuestgeber, true);
            Py.Setzen(aufgabe, FeldArbeitsstelle, new List<object?> { a.X, a.Y });
        }
        Py.Setzen(aufgabe, "etage", (long)a.Etage);
    }

    /// <summary>
    /// Offener Auftrag ohne wartenden Questgeber an dieser Stelle eines Innenraums (Raum am Arbeitsort; im Geschaeft bis
    /// <see cref="ArbeitsortReichweite"/> an der Stelle), sonst null.
    /// </summary>
    public static PyObjekt? AuftragOhneQuestgeber(string ort, int etage, double x, double y, PyObjekt grundriss, ISpielSicht stand)
    {
        foreach (var liste in stand.Auftraege(ort).Values)
        {
            foreach (var a in Liste(liste).Select(Objekt))
            {
                if (!Py.Wahr(Get(a, FeldOhneQuestgeber)) || (Get(a, "etage", 0L) is long e ? e : 0) != etage)
                {
                    continue;
                }
                if (Get(a, "raum") is string { Length: > 0 } raum)
                {
                    if (Py.Gleich(Wege.RaumBei(x, y, grundriss)?["id"], raum))
                    {
                        return a;
                    }
                }
                else if (Get(a, FeldArbeitsstelle) is List<object?> { Count: 2 } s)
                {
                    double dx = Py.F(s[0]) - x, dy = Py.F(s[1]) - y;
                    if ((dx * dx) + (dy * dy) <= ArbeitsortReichweite * ArbeitsortReichweite)
                    {
                        return a;
                    }
                }
            }
        }
        return null;
    }
}
