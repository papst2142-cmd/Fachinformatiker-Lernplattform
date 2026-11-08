using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 5 „Etagen“ (Weisung P12-73, Plan P12-23b Abschnitte 1 und 2): Bearbeitung der Etagen und Uebergaenge im
/// Grundriss des Erdgeschosses (<paramref>eg</paramref>), ohne Oberflaeche. Jede Aenderung laeuft im Werkzeug ueber
/// <see cref="Projekt.EtagenAendern{T}"/> (ein Schritt im Verlauf, Rueckgaengig nimmt das ganze Haus zurueck).
/// </summary>
public static class EtagenBearbeitung
{
    /// <summary>Feld eines Halts: Lage des Stuecks beim letzten Abgleich [x, y, dreh] (die Ankunft wandert mit dem Stueck).</summary>
    public const string FeldStueck = "stueck";

    /// <summary>
    /// Neue Etage oben anlegen (Vorschlag: Aussenwaende und Flur des Erdgeschosses, Plan Abschnitt 1). Rueckgabe: ihre
    /// Nummer; -1, wenn schon <see cref="Etagen.Hoechstens"/> Etagen da sind (Nico 4a, Hinweis <see cref="Texte.EtageHoechstens"/>).
    /// </summary>
    public static int Hinzufuegen(PyObjekt eg)
    {
        var anzahl = Etagen.Anzahl(eg);
        if (anzahl >= Etagen.Hoechstens)
        {
            return -1;
        }
        if (Get(eg, Etagen.FeldEtagen) is not List<object?> liste)
        {
            liste = [];
            Py.Setzen(eg, Etagen.FeldEtagen, liste);
        }
        liste.Add(new PyObjekt { { "gebaeude", Etagen.Vorschlag(eg) } });
        return anzahl + 1;
    }

    /// <summary>Etage umbenennen (leerer Name = Standardname „1. Obergeschoss“).</summary>
    public static void Umbenennen(PyObjekt eg, int etage, string name)
    {
        if (etage < 1 || !Etagen.Gibt(eg, etage))
        {
            return;
        }
        var e = Objekt(Etagen.Liste(eg)[etage - 1]);
        name = name.Trim();
        if (name.Length == 0 || name == Etagen.StandardName(etage))
        {
            e.RemoveAll(p => p.Key == "name");
        }
        else
        {
            Py.Setzen(e, "name", name.Length > 40 ? name[..40] : name);
        }
    }

    /// <summary>Fuehrt ein Uebergang zu dieser Etage (dann darf sie nicht geloescht werden)?</summary>
    public static bool HatUebergang(PyObjekt eg, int etage) =>
        Etagen.Uebergaenge(eg, "").Any(u => u.Auf(etage) is not null)
        || (Etagen.Gibt(eg, etage) && Etagen.Einrichtung(Etagen.Gebaeude(eg, etage)).Any(d => Etagen.IstStueck(Py.Str(d["typ"]))));

    /// <summary>Etage loeschen (nicht das Erdgeschoss, nicht mit Uebergang); die Etagen darueber ruecken nach unten.</summary>
    public static bool Loeschen(PyObjekt eg, int etage)
    {
        if (etage < 1 || !Etagen.Gibt(eg, etage) || HatUebergang(eg, etage))
        {
            return false;
        }
        Etagen.Liste(eg).RemoveAt(etage - 1);
        foreach (var h in Halte(eg))
        {
            if (Py.Int(h["etage"]) > etage)
            {
                Py.Setzen(h, "etage", Py.Int(h["etage"]) - 1);
            }
        }
        if (Etagen.Anzahl(eg) == 0)
        {
            eg.RemoveAll(p => p.Key == Etagen.FeldEtagen);
        }
        return true;
    }

    private static IEnumerable<PyObjekt> Halte(PyObjekt eg) =>
        (Get(eg, Etagen.FeldUebergaenge) as List<object?> ?? []).OfType<PyObjekt>()
            .SelectMany(u => (Get(u, "halte") as List<object?> ?? []).OfType<PyObjekt>());

    private static List<object?> UebergangListe(PyObjekt eg)
    {
        if (Get(eg, Etagen.FeldUebergaenge) is not List<object?> liste)
        {
            liste = [];
            Py.Setzen(eg, Etagen.FeldUebergaenge, liste);
        }
        return liste;
    }

    private static PyObjekt? UebergangDaten(PyObjekt eg, string id) =>
        (Get(eg, Etagen.FeldUebergaenge) as List<object?> ?? []).OfType<PyObjekt>().FirstOrDefault(u => Get(u, "id") as string == id);

    internal static PyObjekt HaltDaten(PyObjekt d, int etage)
    {
        var (x, y, blick) = Etagen.Vor(d);
        return new PyObjekt
        {
            { "etage", (long)etage }, { "ankunft", new List<object?> { x, y, blick } },
            { FeldStueck, new List<object?> { d["x"], d["y"], Py.Int(Get(d, "dreh", 0L)) } },
        };
    }

    /// <summary>Naechste freie Kennung „u1“, „u2“ …</summary>
    private static string NeueId(PyObjekt eg)
    {
        var da = Etagen.Uebergaenge(eg, "").Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        for (var i = 1; ; i++)
        {
            var id = "u" + i.ToString(CultureInfo.InvariantCulture);
            if (!da.Contains(id))
            {
                return id;
            }
        }
    }

    /// <summary>
    /// Ziele, die das Stueck <paramref name="d"/> auf Etage <paramref name="etage"/> haben kann: Treppe = die Etage darueber
    /// und darunter, Fahrstuhl = alle anderen Etagen.
    /// </summary>
    public static List<int> MoeglicheZiele(PyObjekt eg, int etage, PyObjekt d)
    {
        var alle = Enumerable.Range(0, Etagen.Anzahl(eg) + 1).Where(k => k != etage && Etagen.Gibt(eg, k));
        return Py.Str(d["typ"]) == Etagen.Treppe ? [.. alle.Where(k => Math.Abs(k - etage) == 1)] : [.. alle];
    }

    /// <summary>Etagen, zu denen das Stueck heute fuehrt (ohne die eigene).</summary>
    public static List<int> Ziele(PyObjekt eg, int etage, PyObjekt d) =>
        Get(d, Etagen.FeldLink) is string id && Etagen.Von(eg, "", id) is { } u ? [.. u.Ziele(etage)] : [];

    /// <summary>
    /// Ziel waehlen (Plan Abschnitt 2): legt den Uebergang an (falls noch keiner) und das Gegenstueck an derselben Stelle auf
    /// Etage <paramref name="ziel"/>, mit der Ankunft einen Schritt davor. Treppe: ein neues Ziel ersetzt das alte (das alte
    /// Gegenstueck faellt weg). Fahrstuhl: ein schon gewaehltes Ziel wird wieder abgewaehlt. false = geht nicht.
    /// </summary>
    public static bool ZielSetzen(PyObjekt eg, int etage, Stelle stelle, int ziel)
    {
        var g = Etagen.Gebaeude(eg, etage);
        var d = Bearbeitung.Deko(g, stelle);
        var typ = Py.Str(d["typ"]);
        if (!Etagen.IstStueck(typ) || !MoeglicheZiele(eg, etage, d).Contains(ziel))
        {
            return false;
        }
        if (Get(d, Etagen.FeldLink) is not string id || UebergangDaten(eg, id) is null)
        {
            id = NeueId(eg);
            Py.Setzen(d, Etagen.FeldLink, id);
            UebergangListe(eg).Add(new PyObjekt { { "id", id }, { "art", typ }, { "halte", new List<object?> { HaltDaten(d, etage) } } });
        }
        var u = UebergangDaten(eg, id)!;
        var halte = Liste(u["halte"]);
        var da = halte.OfType<PyObjekt>().FirstOrDefault(h => Py.Int(h["etage"]) == ziel);
        if (da is not null)
        {
            if (typ == Etagen.Treppe)
            {
                return false;
            }
            // Fahrstuhl: abwaehlen, Gegenstueck dort weg
            halte.Remove(da);
            StueckeEntfernen(Etagen.Gebaeude(eg, ziel), id);
            return true;
        }
        if (typ == Etagen.Treppe)
        {
            foreach (var alt in halte.OfType<PyObjekt>().Where(h => Py.Int(h["etage"]) != etage).ToList())
            {
                halte.Remove(alt);
                StueckeEntfernen(Etagen.Gebaeude(eg, (int)Py.Int(alt["etage"])), id);
            }
        }
        var zielG = Etagen.Gebaeude(eg, ziel);
        var gegen = new PyObjekt { { "typ", typ }, { "x", d["x"] }, { "y", d["y"] }, { "w", d["w"] }, { "h", d["h"] } };
        if (Py.Hat(d, "dreh"))
        {
            gegen.Add("dreh", d["dreh"]);
        }
        gegen.Add(Etagen.FeldLink, id);
        var bereich = Bearbeitung.BereichBei(zielG, Py.F(d["x"]) + (Py.F(d["w"]) / 2), Py.F(d["y"]) + (Py.F(d["h"]) / 2)) ?? "flur";
        var ziel2 = Bearbeitung.Bereich(zielG, bereich);
        if (ziel2["deko"] is not List<object?> liste)
        {
            liste = [];
            Py.Setzen(ziel2, "deko", liste);
        }
        liste.Add(gegen);
        halte.Add(HaltDaten(gegen, ziel));
        return true;
    }

    private static void StueckeEntfernen(PyObjekt g, string id)
    {
        foreach (var (bereich, _) in Bearbeitung.Bereiche(g).Select(b => (b.Bereich, 0)).Prepend(("flur", 0)).Distinct())
        {
            if (Bearbeitung.Bereich(g, bereich)["deko"] is List<object?> liste)
            {
                liste.RemoveAll(x => x is PyObjekt o && Get(o, Etagen.FeldLink) as string == id);
            }
        }
    }

    /// <summary>Ankunft des Uebergangs <paramref name="id"/> auf Etage <paramref name="etage"/> setzen (auf 0,01 gerundet).</summary>
    public static bool AnkunftSetzen(PyObjekt eg, string id, int etage, double x, double y)
    {
        if (HaltVon(eg, id, etage) is not { } h)
        {
            return false;
        }
        var a = Liste(h["ankunft"]);
        Py.Setzen(h, "ankunft", new List<object?> { Bearbeitung.Runden(x), Bearbeitung.Runden(y), a[2] });
        return true;
    }

    /// <summary>Blick der Ankunft um 90 Grad im Uhrzeigersinn (von oben) drehen.</summary>
    public static bool BlickDrehen(PyObjekt eg, string id, int etage)
    {
        if (HaltVon(eg, id, etage) is not { } h)
        {
            return false;
        }
        var a = Liste(h["ankunft"]);
        // Blick 0 = nach +y (im Plan nach unten); im Uhrzeigersinn von oben: unten → links → oben → rechts
        var neu = ((Py.Int(a[2]) + 90) % 360 + 360) % 360;
        Py.Setzen(h, "ankunft", new List<object?> { a[0], a[1], neu });
        return true;
    }

    private static PyObjekt? HaltVon(PyObjekt eg, string id, int etage) =>
        UebergangDaten(eg, id) is { } u ? (Get(u, "halte") as List<object?> ?? []).OfType<PyObjekt>().FirstOrDefault(h => Py.Int(h["etage"]) == etage) : null;

    /// <summary>Ankunftsstellen der Etage: (Kennung, Art, x, y, Blick).</summary>
    public static List<(string Id, string Art, double X, double Y, long Blick)> Ankuenfte(PyObjekt eg, int etage) =>
        [.. Etagen.Uebergaenge(eg, "").Where(u => Etagen.Arten.Contains(u.Art) && u.Auf(etage) is not null)
            .Select(u => (u.Id, u.Art, u.Auf(etage)!.X, u.Auf(etage)!.Y, u.Auf(etage)!.Blick))];

    /// <summary>
    /// Nach jeder Aenderung am Haus: Uebergaenge mit den Stuecken abgleichen. Ein geloeschtes Stueck nimmt bei der Treppe
    /// den ganzen Uebergang mit (beide Stuecke), beim Fahrstuhl nur seinen Halt; ein verschobenes oder gedrehtes Stueck nimmt
    /// seine Ankunft mit; verwaiste Verweise und doppelte Stuecke verlieren ihren Verweis.
    /// </summary>
    public static void Aufraeumen(PyObjekt eg)
    {
        if (Get(eg, Etagen.FeldUebergaenge) is not List<object?> liste)
        {
            return;
        }
        foreach (var u in liste.OfType<PyObjekt>().ToList())
        {
            if (Get(u, "id") is not string id || Get(u, "halte") is not List<object?> halte)
            {
                continue;
            }
            var weg = false;
            foreach (var h in halte.OfType<PyObjekt>().ToList())
            {
                var etage = (int)Py.Int(Get(h, "etage", -1L));
                if (!Etagen.Gibt(eg, etage))
                {
                    halte.Remove(h);
                    continue;
                }
                var stuecke = Etagen.Stuecke(Etagen.Gebaeude(eg, etage), id);
                if (stuecke.Count == 0)
                {
                    halte.Remove(h);
                    weg |= Py.Str(u["art"]) == Etagen.Treppe;
                    continue;
                }
                // doppelt (z. B. alte Kopie): das Stueck an der gemerkten Stelle behaelt den Verweis
                var gemerkt = Get(h, FeldStueck) as List<object?>;
                var bleibt = stuecke.FirstOrDefault(s => gemerkt is { Count: 3 } && Py.F(s["x"]) == Py.F(gemerkt[0]) && Py.F(s["y"]) == Py.F(gemerkt[1])) ?? stuecke[0];
                foreach (var andere in stuecke.Where(s => !ReferenceEquals(s, bleibt)))
                {
                    andere.RemoveAll(p => p.Key == Etagen.FeldLink);
                }
                MitStueck(h, bleibt);
            }
            if (weg || halte.Count == 0)
            {
                liste.Remove(u);
                for (var k = 0; k <= Etagen.Anzahl(eg); k++)
                {
                    if (Etagen.Gibt(eg, k))
                    {
                        StueckeEntfernen(Etagen.Gebaeude(eg, k), id);
                    }
                }
            }
        }
        // Verweise ohne Uebergang fallen weg
        var ids = liste.OfType<PyObjekt>().Select(u => Get(u, "id") as string).ToHashSet(StringComparer.Ordinal);
        for (var k = 0; k <= Etagen.Anzahl(eg); k++)
        {
            if (!Etagen.Gibt(eg, k))
            {
                continue;
            }
            foreach (var d in Etagen.Einrichtung(Etagen.Gebaeude(eg, k)).Where(d => Get(d, Etagen.FeldLink) is string s && !ids.Contains(s)))
            {
                d.RemoveAll(p => p.Key == Etagen.FeldLink);
            }
        }
        if (liste.Count == 0)
        {
            eg.RemoveAll(p => p.Key == Etagen.FeldUebergaenge);
        }
    }

    /// <summary>Hat sich das Stueck bewegt, wandert die Ankunft mit (gedreht: neu einen Schritt davor).</summary>
    private static void MitStueck(PyObjekt h, PyObjekt d)
    {
        var dreh = Py.Int(Get(d, "dreh", 0L));
        if (Get(h, FeldStueck) is List<object?> { Count: 3 } alt && Get(h, "ankunft") is List<object?> { Count: 3 } a)
        {
            if (Py.Int(alt[2]) != dreh)
            {
                var (x, y, blick) = Etagen.Vor(d);
                Py.Setzen(h, "ankunft", new List<object?> { x, y, blick });
            }
            else
            {
                var (dx, dy) = (Py.F(d["x"]) - Py.F(alt[0]), Py.F(d["y"]) - Py.F(alt[1]));
                if (dx != 0 || dy != 0)
                {
                    Py.Setzen(h, "ankunft", new List<object?> { Bearbeitung.Runden(Py.F(a[0]) + dx), Bearbeitung.Runden(Py.F(a[1]) + dy), a[2] });
                }
            }
        }
        Py.Setzen(h, FeldStueck, new List<object?> { d["x"], d["y"], dreh });
    }
}
