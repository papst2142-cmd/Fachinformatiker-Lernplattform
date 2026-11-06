using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>Ein Stueck der Einrichtung im Grundriss: Bereich ("flur" oder Raum-Id) und Stelle in dessen Liste "deko".</summary>
public sealed record Stelle(string Bereich, int Nummer);

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitt 3): Bearbeitung eines Grundrisses (Aufbau wie innenraeume.json).
/// Raster 0,5 wie das Laufraster des Spiels fuer alles, was das Gehen betrifft (Raeume, Tueren, Flur); Einrichtung rastet
/// mit 0,5 ein, mit "Raster aus" mit 0,1 (verschoben wird um ganze Rasterschritte, die Ausrichtung zu den Nachbarn bleibt).
/// Gedreht wird in Vierteldrehungen im Uhrzeigersinn um die Mitte (Feld "dreh" wie im Spiel, Rechteck mitgedreht).
/// Alle Werte werden auf 0,01 gerundet (lesbare Datei, gleiche Zahl auf jedem Geraet).
/// </summary>
public static class Bearbeitung
{
    public const double Raster = 0.5;

    public const double RasterFein = 0.1;

    /// <summary>Kleinste Groesse eines Stuecks.</summary>
    public const double Kleinste = 0.1;

    public static double Runden(double wert) => Math.Round(wert * 100, MidpointRounding.AwayFromZero) / 100;

    /// <summary>Wert auf das Raster legen.</summary>
    public static double AufRaster(double wert, double raster) => Runden(Math.Round(wert / raster, MidpointRounding.AwayFromZero) * raster);

    /// <summary>Zahl wie im Grundriss (ganze Zahl bleibt Kommazahl, z. B. 2.0), gerundet auf 0,01.</summary>
    public static object Zahl(double wert) => Runden(wert);

    public static PyObjekt Deko(PyObjekt gebaeude, Stelle stelle) =>
        Objekt(Liste(Bereich(gebaeude, stelle.Bereich)["deko"])[stelle.Nummer]);

    /// <summary>Flur oder Raum mit dieser Id.</summary>
    public static PyObjekt Bereich(PyObjekt gebaeude, string bereich) =>
        bereich == "flur" ? Objekt(gebaeude["flur"]) : Liste(gebaeude["raeume"]).OfType<PyObjekt>().First(r => Py.Str(r["id"]) == bereich);

    /// <summary>Flaeche eines Bereichs (x, y, w, h); der Flur geht ueber die ganze Breite.</summary>
    public static (double X, double Y, double W, double H) Flaeche(PyObjekt gebaeude, string bereich)
    {
        if (bereich == "flur")
        {
            var f = Objekt(gebaeude["flur"]);
            return (0, Py.F(f["y"]), Py.F(gebaeude["breite"]), Py.F(f["h"]));
        }
        var r = Bereich(gebaeude, bereich);
        return (Py.F(r["x"]), Py.F(r["y"]), Py.F(r["w"]), Py.F(r["h"]));
    }

    /// <summary>Bereich, in dem ein Punkt liegt (Raum vor Flur), sonst null.</summary>
    public static string? BereichBei(PyObjekt gebaeude, double x, double y)
    {
        foreach (var r in Liste(gebaeude["raeume"]).OfType<PyObjekt>())
        {
            var (rx, ry, rw, rh) = Flaeche(gebaeude, Py.Str(r["id"]));
            if (x >= rx && x <= rx + rw && y >= ry && y <= ry + rh)
            {
                return Py.Str(r["id"]);
            }
        }
        var (fx, fy, fw, fh) = Flaeche(gebaeude, "flur");
        return x >= fx && x <= fx + fw && y >= fy && y <= fy + fh ? "flur" : null;
    }

    /// <summary>Alle Stuecke mit Stelle (Flur zuerst, dann die Raeume).</summary>
    public static IEnumerable<(Stelle Stelle, PyObjekt Deko)> Alle(PyObjekt gebaeude)
    {
        var flur = ListeOderLeer(Objekt(gebaeude["flur"]), "deko");
        for (var i = 0; i < flur.Count; i++)
        {
            yield return (new Stelle("flur", i), Objekt(flur[i]));
        }
        foreach (var r in Liste(gebaeude["raeume"]).OfType<PyObjekt>())
        {
            var deko = ListeOderLeer(r, "deko");
            for (var i = 0; i < deko.Count; i++)
            {
                yield return (new Stelle(Py.Str(r["id"]), i), Objekt(deko[i]));
            }
        }
    }

    /// <summary>Oberstes Stueck unter einem Punkt (zuletzt gezeichnetes zuerst), sonst null.</summary>
    public static Stelle? StueckBei(PyObjekt gebaeude, double x, double y)
    {
        Stelle? treffer = null;
        foreach (var (stelle, d) in Alle(gebaeude))
        {
            double dx = Py.F(d["x"]), dy = Py.F(d["y"]);
            if (x >= dx && x <= dx + Py.F(d["w"]) && y >= dy && y <= dy + Py.F(d["h"]))
            {
                treffer = stelle;
            }
        }
        return treffer;
    }

    /// <summary>Neues Stueck aus dem Katalog mit der Mitte bei (x, y), auf das Raster gelegt; Rueckgabe: seine Stelle oder null (ausserhalb).</summary>
    public static Stelle? Setzen(PyObjekt gebaeude, string typ, double x, double y, bool raster = true)
    {
        var teil = InnenKatalog.Teil(typ) ?? throw new ArgumentException("unbekannter Typ " + typ, nameof(typ));
        var bereich = BereichBei(gebaeude, x, y);
        if (bereich is null)
        {
            return null;
        }
        var r = raster ? Raster : RasterFein;
        var (bx, by, bw, bh) = Flaeche(gebaeude, bereich);
        var nx = Math.Clamp(AufRaster(x - (teil.Breite / 2), r), bx, Math.Max(bx, bx + bw - teil.Breite));
        var ny = Math.Clamp(AufRaster(y - (teil.Tiefe / 2), r), by, Math.Max(by, by + bh - teil.Tiefe));
        var d = new PyObjekt { { "typ", typ }, { "x", Zahl(nx) }, { "y", Zahl(ny) }, { "w", Zahl(teil.Breite) }, { "h", Zahl(teil.Tiefe) } };
        var ziel = Bereich(gebaeude, bereich);
        if (ziel["deko"] is not List<object?> liste)
        {
            liste = [];
            Py.Setzen(ziel, "deko", liste);
        }
        liste.Add(d);
        return new Stelle(bereich, liste.Count - 1);
    }

    /// <summary>
    /// Stueck um (dx, dy) verschieben, in ganzen Rasterschritten; wechselt die Mitte in einen anderen Bereich, zieht das
    /// Stueck dorthin um. Rueckgabe: neue Stelle.
    /// </summary>
    public static Stelle Verschieben(PyObjekt gebaeude, Stelle stelle, double dx, double dy, bool raster = true)
    {
        var d = Deko(gebaeude, stelle);
        var r = raster ? Raster : RasterFein;
        var x = Runden(Py.F(d["x"]) + (Math.Round(dx / r, MidpointRounding.AwayFromZero) * r));
        var y = Runden(Py.F(d["y"]) + (Math.Round(dy / r, MidpointRounding.AwayFromZero) * r));
        return Platzieren(gebaeude, stelle, x, y);
    }

    /// <summary>Stueck an genaue Werte setzen (Feld rechts): x, y wie eingegeben (auf 0,01 gerundet).</summary>
    public static Stelle Platzieren(PyObjekt gebaeude, Stelle stelle, double x, double y)
    {
        var d = Deko(gebaeude, stelle);
        Py.Setzen(d, "x", Zahl(x));
        Py.Setzen(d, "y", Zahl(y));
        var mitte = BereichBei(gebaeude, x + (Py.F(d["w"]) / 2), y + (Py.F(d["h"]) / 2));
        if (mitte is null || mitte == stelle.Bereich)
        {
            return stelle;
        }
        Liste(Bereich(gebaeude, stelle.Bereich)["deko"]).RemoveAt(stelle.Nummer);
        var ziel = Bereich(gebaeude, mitte);
        if (ziel["deko"] is not List<object?> liste)
        {
            liste = [];
            Py.Setzen(ziel, "deko", liste);
        }
        liste.Add(d);
        return new Stelle(mitte, liste.Count - 1);
    }

    /// <summary>Vierteldrehung im Uhrzeigersinn um die Mitte (Breite und Tiefe tauschen, "dreh" + 1).</summary>
    public static void Drehen(PyObjekt gebaeude, Stelle stelle)
    {
        var d = Deko(gebaeude, stelle);
        double x = Py.F(d["x"]), y = Py.F(d["y"]), w = Py.F(d["w"]), h = Py.F(d["h"]);
        var (mx, my) = (x + (w / 2), y + (h / 2));
        Py.Setzen(d, "x", Zahl(mx - (h / 2)));
        Py.Setzen(d, "y", Zahl(my - (w / 2)));
        Py.Setzen(d, "w", Zahl(h));
        Py.Setzen(d, "h", Zahl(w));
        Py.Setzen(d, "dreh", ((Py.Hat(d, "dreh") ? Py.Int(d["dreh"]) : 0L) + 1) % 4);
    }

    /// <summary>Kopie daneben (rechts versetzt um die eigene Breite, sonst darunter). Rueckgabe: Stelle der Kopie.</summary>
    public static Stelle Kopieren(PyObjekt gebaeude, Stelle stelle)
    {
        var d = Deko(gebaeude, stelle);
        var kopie = (PyObjekt)Py.Kopie(d)!;
        var (bx, _, bw, _) = Flaeche(gebaeude, stelle.Bereich);
        var rechts = Py.F(d["x"]) + (2 * Py.F(d["w"])) + Raster <= bx + bw;
        Py.Setzen(kopie, "x", Zahl(Py.F(d["x"]) + (rechts ? Py.F(d["w"]) + Raster : 0)));
        Py.Setzen(kopie, "y", Zahl(Py.F(d["y"]) + (rechts ? 0 : Py.F(d["h"]) + Raster)));
        var liste = Liste(Bereich(gebaeude, stelle.Bereich)["deko"]);
        liste.Add(kopie);
        return new Stelle(stelle.Bereich, liste.Count - 1);
    }

    public static void Loeschen(PyObjekt gebaeude, Stelle stelle) =>
        Liste(Bereich(gebaeude, stelle.Bereich)["deko"]).RemoveAt(stelle.Nummer);

    /// <summary>Groesse setzen (ab 0,1).</summary>
    public static void Groesse(PyObjekt gebaeude, Stelle stelle, double w, double h)
    {
        var d = Deko(gebaeude, stelle);
        Py.Setzen(d, "w", Zahl(Math.Max(Kleinste, w)));
        Py.Setzen(d, "h", Zahl(Math.Max(Kleinste, h)));
    }

    /// <summary>Ware zuordnen (leer = keine Ware, das Stueck ist dann nur Einrichtung).</summary>
    public static void Ware(PyObjekt gebaeude, Stelle stelle, string? ware)
    {
        var d = Deko(gebaeude, stelle);
        if (string.IsNullOrEmpty(ware))
        {
            var rest = d.Where(p => p.Key != "ware").ToList();
            d.Clear();
            d.AddRange(rest);
        }
        else
        {
            Py.Setzen(d, "ware", ware);
        }
    }

    /// <summary>Raum auf das Raster legen und setzen (x, y, w, h); die Tuer bleibt an ihrer Wand und wird mitgeschoben.</summary>
    public static void Raum(PyObjekt gebaeude, string id, double x, double y, double w, double h)
    {
        var r = Bereich(gebaeude, id);
        var dx = AufRaster(x, Raster) - Py.F(r["x"]);
        Py.Setzen(r, "x", Zahl(AufRaster(x, Raster)));
        Py.Setzen(r, "y", Zahl(AufRaster(y, Raster)));
        Py.Setzen(r, "w", Zahl(Math.Max(1, AufRaster(w, Raster))));
        Py.Setzen(r, "h", Zahl(Math.Max(1, AufRaster(h, Raster))));
        if (Get(r, "tuer") is PyObjekt t && t.Count > 0 && Wege.TuerSeite(r, gebaeude) is "s" or "n")
        {
            Py.Setzen(t, "von", Zahl(Py.F(t["von"]) + dx));
            Py.Setzen(t, "bis", Zahl(Py.F(t["bis"]) + dx));
        }
    }

    /// <summary>Tuer eines Raums (von, bis auf der Wand zum Flur), auf das Raster gelegt.</summary>
    public static void Tuer(PyObjekt gebaeude, string id, double von, double bis)
    {
        var r = Bereich(gebaeude, id);
        var t = Get(r, "tuer") as PyObjekt ?? [];
        Py.Setzen(t, "von", Zahl(AufRaster(Math.Min(von, bis), Raster)));
        Py.Setzen(t, "bis", Zahl(AufRaster(Math.Max(von, bis), Raster)));
        Py.Setzen(r, "tuer", t);
    }

    /// <summary>Boden eines Bereichs (parkett, teppich, fliesen, beton, ...).</summary>
    public static void Boden(PyObjekt gebaeude, string bereich, string boden) => Py.Setzen(Bereich(gebaeude, bereich), "boden", boden);

    /// <summary>Boeden, die das Spiel zeichnet.</summary>
    public static readonly IReadOnlyList<(string Kennung, string Name)> Boeden =
        [("parkett", "Parkett"), ("teppich", "Teppich"), ("fliesen", "Fliesen"), ("beton", "Beton")];

    /// <summary>Kurztext eines Stuecks fuer Anzeige und Screenreader.</summary>
    public static string Beschreibung(PyObjekt d) =>
        InnenKatalog.Name(Py.Str(d["typ"])) + (Py.Hat(d, "ware") ? " (Ware " + Py.Str(d["ware"]) + ")" : "")
        + string.Create(CultureInfo.GetCultureInfo("de-DE"), $", x {Py.F(d["x"]):0.##}, y {Py.F(d["y"]):0.##}, {Py.F(d["w"]):0.##} × {Py.F(d["h"]):0.##}");
}
