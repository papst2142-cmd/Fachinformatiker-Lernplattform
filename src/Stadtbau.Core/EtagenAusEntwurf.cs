using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;
using Teil = Lernplattform.Core.Stadtbau.Bauteile.Teil;
using Eingang = Lernplattform.Core.Stadtbau.Bauteile.Eingang;
using Seiten = Lernplattform.Core.Stadtbau.Bauteile.Seiten;

namespace Stadtbau.Core;

/// <summary>
/// S 0.19.0 „Obergeschosse eigener Gebaeude“ (Weisung P12-80, Nico 1a 2a 3a 4a 5a, Plan Meldung P12-51 Abschnitt 2): aus den
/// Ebenen eines Entwurfs werden Etagen des Innenraums (W5-Daten im Grundriss des Erdgeschosses), ohne Oberflaeche.
/// <list type="bullet">
/// <item>Jede Ebene 1–4 mit Waenden wird eine Etage: das Rechteck um ihre Wandfelder, je Feld <see cref="Bauteile.InnenJeFeld"/>
/// Einheiten, ein Raum ueber die ganze Flaeche, ohne Eingang. Die Dach-Ebene wird keine Etage.</item>
/// <item>Treppenhaus (2a): je zwei Nachbar-Ebenen eine Treppe; sie steht auf beiden Ebenen an derselben Stelle (Lage im
/// Grundriss des Erdgeschosses), im Erdgeschoss im Flur ganz hinten. Die Treppe zur Ebene darueber steht neben der Treppe
/// von unten (eine Treppe verbindet genau zwei Etagen, W5), jede zweite wieder an derselben Stelle.</item>
/// <item>Pruefung (3a, 5a): rot, wenn eine Etage ueber die Grundflaeche ihrer Ebene hinausragt; gelb, wenn eine Ebene Waende,
/// aber keine Etage hat oder eine Etage kleiner als ihre Ebene ist. Beides behebt <see cref="Neu"/> („Vorschlag neu“).</item>
/// </list>
/// Lage: wie im Vorschlag des Erdgeschosses (<see cref="Entwurf.GrundrissVorschlag"/>) wird die Fassade mit dem Eingang die
/// linke Wand (x = Tiefe ins Haus, y = laengs der Fassade). Jede Etage hat ihren eigenen Ursprung in der Ecke ihres Rechtecks.
/// </summary>
public static class EtagenAusEntwurf
{
    /// <summary>Rechteck im Grundriss des Erdgeschosses (Einheiten).</summary>
    public readonly record struct Rechteck(double X0, double Y0, double X1, double Y1)
    {
        public double Breite => X1 - X0;

        public double Hoehe => Y1 - Y0;
    }

    /// <summary>Abstand der Treppe zur Wand.</summary>
    public const double Rand = 0.3;

    /// <summary>Freier Platz zwischen zwei Treppen und um die Ankunftsstelle.</summary>
    public const double Luft = 0.4;

    /// <summary>Gegenprobe: das Treppenhaus fehlt (wie vor S 0.19.0 waere jede Etage rot, nicht erreichbar).</summary>
    public static bool OhneTreppeFuerTests
    {
        get => OhneTreppe.Value;
        set => OhneTreppe.Value = value;
    }

    private static readonly AsyncLocal<bool> OhneTreppe = new();

    // ---- Ebenen -------------------------------------------------------------------------------------

    /// <summary>Lage eines Felds im Grundriss des Erdgeschosses: (Tiefe, seitlich) in Feldern.</summary>
    private static (int D, int S) ImPlan(int x, int y, (int X0, int Y0, int X1, int Y1) g, Seiten seite) => seite switch
    {
        Seiten.Sued => (g.Y1 - y, x - g.X0),
        Seiten.Nord => (y - g.Y0, g.X1 - x),
        Seiten.Ost => (g.X1 - x, g.Y1 - y),
        _ => (x - g.X0, y - g.Y0),
    };

    /// <summary>
    /// Rechteck der Ebene <paramref name="ebene"/> (0 = Erdgeschoss mit allen belegten Feldern, 1–4 nur Wandfelder) im
    /// Grundriss des Erdgeschosses; null = keine Wand (oder kein Eingang).
    /// </summary>
    public static Rechteck? Ebene(IReadOnlyList<Teil> teile, IReadOnlyList<Eingang> eingaenge, int ebene)
    {
        var eg = teile.Where(t => t.Stockwerk == 0 && t.Art?.BelegtFeld == true).ToList();
        if (eg.Count == 0 || eingaenge.Count == 0)
        {
            return null;
        }
        var g = (eg.Min(t => t.X), eg.Min(t => t.Y), eg.Max(t => t.X), eg.Max(t => t.Y));
        var felder = ebene == 0 ? eg : [.. teile.Where(t => t.Stockwerk == ebene && t.Art?.IstWand == true)];
        if (felder.Count == 0)
        {
            return null;
        }
        var lagen = felder.Select(t => ImPlan(t.X, t.Y, g, eingaenge[0].Seite)).ToList();
        var k = Bauteile.InnenJeFeld;
        return new Rechteck(lagen.Min(l => l.D) * k, lagen.Min(l => l.S) * k, (lagen.Max(l => l.D) + 1) * k, (lagen.Max(l => l.S) + 1) * k);
    }

    /// <summary>Rechtecke der Ebenen 1–4 (null = keine Wand auf dieser Ebene), Index 0 = 1. OG.</summary>
    public static List<Rechteck?> Obergeschosse(IReadOnlyList<Teil> teile, IReadOnlyList<Eingang> eingaenge) =>
        [.. Enumerable.Range(1, Bauteile.HoechsteWandEbene).Select(k => Ebene(teile, eingaenge, k))];

    /// <summary>Zahl der Etagen, die der Vorschlag anlegt (Ebenen von unten an ohne Luecke).</summary>
    public static int Anzahl(IReadOnlyList<Teil> teile, IReadOnlyList<Eingang> eingaenge) =>
        Obergeschosse(teile, eingaenge).TakeWhile(r => r is not null).Count();

    // ---- Vorschlag ----------------------------------------------------------------------------------

    private static object Z(double d) => Bearbeitung.Zahl(d);

    /// <summary>Grundriss einer Etage: ein Raum (der Flur) ueber die ganze Flaeche, ohne Eingang.</summary>
    public static PyObjekt EtageVorschlag(Rechteck r) => new()
    {
        { "breite", Z(r.Breite) }, { "hoehe", Z(r.Hoehe) }, { "fenster", new List<object?>() },
        {
            "flur", new PyObjekt
            {
                { "y", Z(0) }, { "h", Z(r.Hoehe) }, { "name", "Flur" }, { "boden", "parkett" },
                { "spieler", new List<object?> { Z(r.Breite / 2), Z(r.Hoehe / 2) } }, { "deko", new List<object?>() },
            }
        },
        { "raeume", new List<object?>() },
    };

    /// <summary>
    /// Etagen und Treppenhaus neu vorschlagen (Plan Abschnitt 2 Punkte 1 und 2): vorhandene Etagen, Uebergaenge und ihre
    /// Stuecke im Erdgeschoss fallen weg, die uebrige Einrichtung des Erdgeschosses bleibt. Rueckgabe: Zahl der Etagen.
    /// </summary>
    public static int Neu(PyObjekt eg, IReadOnlyList<Teil> teile, IReadOnlyList<Eingang> eingaenge)
    {
        // alte Stuecke der Uebergaenge im Erdgeschoss weg (Treppe und Fahrstuhl gehoeren zu den alten Etagen)
        var ids = Etagen.Uebergaenge(eg, "").Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (bereich, _) in Bearbeitung.Bereiche(eg).Prepend(("flur", "")).Distinct())
        {
            if (Get(Bearbeitung.Bereich(eg, bereich), "deko") is List<object?> liste)
            {
                liste.RemoveAll(x => x is PyObjekt o && Get(o, Etagen.FeldLink) is string id && ids.Contains(id));
            }
        }
        eg.RemoveAll(p => p.Key is Etagen.FeldEtagen or Etagen.FeldUebergaenge);
        var rechtecke = Obergeschosse(teile, eingaenge).TakeWhile(r => r is not null).Select(r => r!.Value).ToList();
        if (rechtecke.Count == 0)
        {
            return 0;
        }
        var etagen = new List<object?>();
        foreach (var r in rechtecke)
        {
            etagen.Add(new PyObjekt { { "gebaeude", EtageVorschlag(r) } });
        }
        Py.Setzen(eg, Etagen.FeldEtagen, etagen);
        if (!OhneTreppeFuerTests)
        {
            Treppenhaus(eg, Ebene(teile, eingaenge, 0)!.Value, rechtecke);
        }
        return rechtecke.Count;
    }

    /// <summary>Ein Platz einer Treppe im Grundriss des Erdgeschosses: Ecke, Groesse und Drehung (Front = Ankunftsseite).</summary>
    private readonly record struct Platz(double X, double Y, double W, double H, int Dreh);

    private static IEnumerable<Platz> Kandidaten(Rechteck flaeche)
    {
        // zuerst laengs mit der Front zum Eingang (Drehung 1, Ankunft davor), ganz hinten; dann jede Lage im Raster 0,5
        foreach (var dreh in new[] { 1, 0, 2, 3 })
        {
            var (w, h) = dreh % 2 == 1 ? (3.0, 1.6) : (1.6, 3.0);
            for (var x = flaeche.X1 - Rand - w; x >= flaeche.X0 + Rand - 1e-9; x -= 0.5)
            {
                for (var y = flaeche.Y0 + Rand; y <= flaeche.Y1 - Rand - h + 1e-9; y += 0.5)
                {
                    yield return new Platz(Math.Round(x, 2), Math.Round(y, 2), w, h, dreh);
                }
            }
        }
    }

    private static PyObjekt Stueck(Platz p, double ox, double oy, string id) => new()
    {
        { "typ", Etagen.Treppe }, { "x", Z(p.X - ox) }, { "y", Z(p.Y - oy) }, { "w", Z(p.W) }, { "h", Z(p.H) }, { "dreh", (long)p.Dreh },
        { Etagen.FeldLink, id },
    };

    private static (double X, double Y) Ankunft(Platz p)
    {
        var (x, y, _) = Etagen.Vor(new PyObjekt { { "x", p.X }, { "y", p.Y }, { "w", p.W }, { "h", p.H }, { "dreh", (long)p.Dreh } });
        return (x, y);
    }

    private static bool Ueberlappt(double ax, double ay, double aw, double ah, double bx, double by, double bw, double bh, double luft) =>
        ax < bx + bw + luft && bx < ax + aw + luft && ay < by + bh + luft && by < ay + ah + luft;

    /// <summary>Passt der Platz in die Flaeche (mit Ankunft), ohne andere Stuecke, Ankuenfte und den Startplatz zu stoeren?</summary>
    private static bool Passt(Platz p, Rechteck flaeche, List<(double X, double Y, double W, double H)> belegt, List<(double X, double Y)> frei)
    {
        if (p.X < flaeche.X0 + Rand - 1e-9 || p.Y < flaeche.Y0 + Rand - 1e-9 || p.X + p.W > flaeche.X1 - Rand + 1e-9 || p.Y + p.H > flaeche.Y1 - Rand + 1e-9)
        {
            return false;
        }
        var (ax, ay) = Ankunft(p);
        if (ax < flaeche.X0 + Luft || ay < flaeche.Y0 + Luft || ax > flaeche.X1 - Luft || ay > flaeche.Y1 - Luft)
        {
            return false;
        }
        foreach (var b in belegt)
        {
            if (Ueberlappt(p.X, p.Y, p.W, p.H, b.X, b.Y, b.W, b.H, Luft) || Ueberlappt(ax - Luft, ay - Luft, 2 * Luft, 2 * Luft, b.X, b.Y, b.W, b.H, 0))
            {
                return false;
            }
        }
        return !frei.Any(f => Ueberlappt(p.X, p.Y, p.W, p.H, f.X - Luft, f.Y - Luft, 2 * Luft, 2 * Luft, 0));
    }

    /// <summary>
    /// Treppenhaus: Treppe n verbindet Ebene n und n+1 und steht auf beiden an derselben Stelle (im Grundriss des
    /// Erdgeschosses). Bevorzugt an der Stelle der Treppe n-2 (gestapelt), sonst der erste freie Platz von hinten.
    /// </summary>
    private static void Treppenhaus(PyObjekt eg, Rechteck egRechteck, List<Rechteck> oben)
    {
        // Flaechen je Ebene im Grundriss des Erdgeschosses; im Erdgeschoss nur der Flur (Raeume haben Waende und Tueren)
        var flur = Objekt(eg["flur"]);
        var flurY = Py.F(flur["y"]);
        var flaechen = new List<Rechteck> { new(0, flurY, Py.F(eg["breite"]), flurY + Py.F(flur["h"])) };
        flaechen.AddRange(oben.Select(r => new Rechteck(r.X0 - egRechteck.X0, r.Y0 - egRechteck.Y0, r.X1 - egRechteck.X0, r.Y1 - egRechteck.Y0)));
        // belegt je Ebene (Erdgeschoss: vorhandene Einrichtung im Flur), frei: Startplatz und Eingang
        var belegt = flaechen.Select(_ => new List<(double X, double Y, double W, double H)>()).ToList();
        var frei = flaechen.Select(_ => new List<(double X, double Y)>()).ToList();
        foreach (var (_, d) in InnenGrundriss.Einrichtung(eg))
        {
            belegt[0].Add((Py.F(d["x"]), Py.F(d["y"]), Py.F(d["w"]), Py.F(d["h"])));
        }
        if (Get(flur, "spieler") is List<object?> { Count: 2 } sp)
        {
            frei[0].Add((Py.F(sp[0]), Py.F(sp[1])));
        }
        if (Get(flur, "eingang") is PyObjekt ein)
        {
            frei[0].Add((0.5, (Py.F(ein["von"]) + Py.F(ein["bis"])) / 2));
        }
        var plaetze = new List<Platz>();
        var uebergaenge = new List<object?>();
        for (var n = 0; n < oben.Count; n++)
        {
            bool Frei(Platz p) => Passt(p, flaechen[n], belegt[n], frei[n]) && Passt(p, flaechen[n + 1], belegt[n + 1], frei[n + 1]);
            var bevorzugt = n >= 2 ? [plaetze[n - 2]] : Array.Empty<Platz>();
            var gefunden = bevorzugt.Concat(Kandidaten(flaechen[n])).Where(Frei).Select(p => (Platz?)p).FirstOrDefault();
            if (gefunden is not { } p)
            {
                // kein Platz: diese und alle Etagen darueber bleiben ohne Treppe (die Pruefung zeigt sie rot)
                break;
            }
            plaetze.Add(p);
            var id = "u" + (n + 1).ToString(CultureInfo.InvariantCulture);
            var halte = new List<object?>();
            for (var k = n; k <= n + 1; k++)
            {
                var g = Etagen.Gebaeude(eg, k);
                var (ox, oy) = k == 0 ? (0.0, 0.0) : (flaechen[k].X0, flaechen[k].Y0);
                var stueck = Stueck(p, ox, oy, id);
                var bereich = k == 0 ? "flur" : Bearbeitung.BereichBei(g, p.X - ox + (p.W / 2), p.Y - oy + (p.H / 2)) ?? "flur";
                var ziel = Bearbeitung.Bereich(g, bereich);
                if (Get(ziel, "deko") is not List<object?> liste)
                {
                    liste = [];
                    Py.Setzen(ziel, "deko", liste);
                }
                liste.Add(stueck);
                halte.Add(EtagenBearbeitung.HaltDaten(stueck, k));
                belegt[k].Add((p.X, p.Y, p.W, p.H));
                frei[k].Add(Ankunft(p));
            }
            uebergaenge.Add(new PyObjekt { { "id", id }, { "art", Etagen.Treppe }, { "halte", halte } });
        }
        if (uebergaenge.Count > 0)
        {
            Py.Setzen(eg, Etagen.FeldUebergaenge, uebergaenge);
        }
    }

    // ---- Pruefung -----------------------------------------------------------------------------------

    /// <summary>
    /// Regeln zwischen Entwurf und Etagen (Nico 3a, 5a), nur Werkzeug. Rot: Etage ragt ueber die Grundflaeche ihrer Ebene
    /// hinaus (oder die Ebene hat keine Waende mehr). Gelb: Ebene mit Waenden ohne Etage („noch nicht begehbar“), Etage
    /// kleiner als ihre Ebene (der Entwurf wurde geaendert). Funde zur Ebene ohne Etage stehen beim Erdgeschoss.
    /// </summary>
    public static List<EtagenFund> Pruefen(PyObjekt eg, IReadOnlyList<Teil> teile, IReadOnlyList<Eingang> eingaenge)
    {
        var funde = new List<EtagenFund>();
        var ebenen = Obergeschosse(teile, eingaenge);
        var anzahl = Etagen.Anzahl(eg);
        for (var k = 1; k <= Bauteile.HoechsteWandEbene; k++)
        {
            var r = ebenen[k - 1];
            if (k > anzahl || !Etagen.Gibt(eg, k))
            {
                if (r is not null)
                {
                    funde.Add(new EtagenFund(0, new StadtbauFund(Texte.EtageFehltImInnenraum(Entwurf.EbeneName(k)), Texte.EtagenVorschlagNeuTipp, false, null, null)));
                }
                continue;
            }
            var g = Etagen.Gebaeude(eg, k);
            if (Get(g, "breite") is not (long or double) || Get(g, "hoehe") is not (long or double))
            {
                continue; // die W5-Pruefung meldet das rot
            }
            double b = Py.F(g["breite"]), h = Py.F(g["hoehe"]);
            var name = Etagen.Name(eg, k);
            if (r is null || b > r.Value.Breite + 1e-9 || h > r.Value.Hoehe + 1e-9)
            {
                funde.Add(new EtagenFund(k, new StadtbauFund(Texte.EtageRagtHinaus(name, Entwurf.EbeneName(k)), Texte.EtageRagtHinausTipp, true, null, null)));
            }
            else if (b < r.Value.Breite - 1e-9 || h < r.Value.Hoehe - 1e-9)
            {
                funde.Add(new EtagenFund(k, new StadtbauFund(Texte.EtagePasstNichtMehr(name, Entwurf.EbeneName(k)), Texte.EtagenVorschlagNeuTipp, false, null, null)));
            }
        }
        return funde;
    }

    /// <summary>Teile und Eingang des selbst gebauten Gebaeudes <paramref name="modell"/> in der Stadt (null = kein Entwurf).</summary>
    public static (List<Teil> Teile, List<Eingang> Eingaenge)? AusStadt(PyObjekt aussen, string modell) =>
        Get(Bauteile.Gebaeude(Aussen.Stadtdaten(aussen)), modell) is PyObjekt e
            ? (Bauteile.TeileLesen(Get(e, "teile")), Bauteile.EingaengeLesen(Get(e, "eingang") ?? new List<object?>()))
            : null;
}
