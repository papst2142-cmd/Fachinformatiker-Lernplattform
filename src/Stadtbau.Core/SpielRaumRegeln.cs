using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle A 1.10.0 / S 0.64.0 (Weisung P12-381, Nico 2c: „alles ist aenderbar, auch Innenwaende, Tueren und die Raumgroesse“):
/// die Regeln des Spiels fuer einen im Werkzeug geaenderten Raum des Spiels. Jeder Fund ist rot und sperrt den Export: Tuer
/// nach draussen zugestellt, ein Raum nicht erreichbar, Flur (1,80 m) bzw. Diele (1,32 m) oder Innentuer (0,90 m) zu schmal,
/// Arbeitsplaetze der Firma bzw. Zweigstelle verstellt oder nicht erreichbar, in einer Wohnung kein Platz mehr fuer Schreibtisch
/// (PC) und Kleiderschrank oder Start-Einrichtung und Umzugskartons ausserhalb, Stellen der Auftraege nicht im Raum, dazu jede
/// Meldung der Ladepruefung des Spiels (<see cref="SpielPruefung.Gebaeude"/>, <see cref="SpielPruefung.Wohnungen"/>). Die
/// allgemeinen Regeln eines Innenraums (Lage, Einrichtung, Personen erreichbar, Grenzen) prueft das Werkzeug wie bei jedem Raum.
/// </summary>
public static class SpielRaumRegeln
{
    /// <summary>Mindestbreite der Diele einer Wohnung in Einheiten (1,32 m).</summary>
    public const double Diele = Flurbreite.Diele;

    /// <summary>Mindestbreite jedes anderen Flurs in Einheiten (1,80 m).</summary>
    public const double Flur = Flurbreite.Flur;

    /// <summary>Mindestbreite einer Innentuer in Einheiten (0,90 m).</summary>
    public const double Tuer = 0.9 * Innenraum.Meter;

    /// <summary>Schrittweite der Suche nach einem Platz fuer Schreibtisch und Kleiderschrank (wie das Raster der Wohnung, doppelt).</summary>
    public const double Suchraster = 2 * Wohnung.Raster;

    private const double Eps = 1e-6;

    private static string M(double einheiten) => (einheiten / Innenraum.Meter).ToString("0.00", CultureInfo.GetCultureInfo("de-DE")) + " m";

    /// <summary>
    /// Funde fuer den Raum <paramref name="raum"/> (Schluessel ohne „spiel:“) mit dem bearbeiteten Grundriss
    /// <paramref name="neu"/>; <paramref name="inhalt"/> und <paramref name="innenraeume"/> sind die Spieldaten ohne diese Aenderung.
    /// </summary>
    public static List<StadtbauFund> Pruefen(string raum, PyObjekt neu, PyObjekt inhalt, PyObjekt? innenraeume)
    {
        var funde = new List<StadtbauFund>();
        var ort = SpielRaumErsatz.Ort(raum);
        if (ort is null || SpielRaumErsatz.Alt(raum, inhalt, innenraeume) is not { } alt)
        {
            funde.Add(new StadtbauFund("Diesen Raum gibt es im Spiel nicht.", "Zurück auf den Stand des Spiels.", true));
            return funde;
        }
        PyObjekt g;
        PyObjekt inhaltNeu;
        try
        {
            var stueck = SpielRaumErsatz.Bauen(raum, neu, inhalt, innenraeume);
            inhaltNeu = SpielRaumErsatz.Inhalt(inhalt, [stueck]);
            g = stueck.Neu;
            _ = Objekt(g["flur"]);
            _ = Liste(g["raeume"]);
        }
        catch (Exception fehler) when (fehler is InvalidDataException or KeyNotFoundException or InvalidCastException or ArgumentException or IOException)
        {
            funde.Add(new StadtbauFund("Der Raum lässt sich im Spiel nicht einsetzen (" + fehler.GetType().Name + ").", "Letzten Schritt rückgängig machen.", true));
            return funde;
        }
        var wohnung = ort == OrtZuhause;
        var flur = Objekt(g["flur"]);
        var fh = Py.F(flur["h"]);
        var fy = Py.F(flur["y"]);
        var mindest = wohnung ? Diele : Flur;
        if (fh < mindest - Eps)
        {
            funde.Add(new StadtbauFund($"{(wohnung ? "Diele" : "Flur")} nur {M(fh)} breit (mindestens {M(mindest)}).",
                "Die Wand am Flur zurückziehen, bis er breit genug ist.", true, Py.F(g["breite"]) / 2, fy + (fh / 2)));
        }
        foreach (var r in Liste(g["raeume"]).Select(Objekt))
        {
            if (Get(r, "tuer") is PyObjekt t && t.Count > 0 && Py.Hat(t, "von") && Py.Hat(t, "bis"))
            {
                var breite = Py.F(t["bis"]) - Py.F(t["von"]);
                if (breite < Tuer - Eps)
                {
                    var (tx, ty) = TuerMitte(r, g, t);
                    funde.Add(new StadtbauFund($"Tür zu „{Py.Str(Get(r, "name", r["id"]))}“ nur {M(breite)} breit (mindestens {M(Tuer)}).",
                        "Die Tür breiter machen („Tür setzen“).", true, tx, ty));
                }
            }
        }
        PyMenge frei;
        try
        {
            frei = Wege.BasisRaster(g);
        }
        catch (Exception fehler) when (fehler is InvalidDataException or KeyNotFoundException or InvalidCastException or OverflowException)
        {
            funde.Add(new StadtbauFund("Die Wege im Raum lassen sich nicht rechnen (" + fehler.GetType().Name + ").", "Letzten Schritt rückgängig machen.", true));
            return funde;
        }
        var start = wohnung && Py.Hat(flur, "spieler") && Get(flur, "spieler") is List<object?> { Count: >= 2 } sp ? (Py.F(sp[0]), Py.F(sp[1])) : Wege.StartPlatz(g);
        if (!frei.Contains(Wege.Zelle(start.Item1, start.Item2)))
        {
            funde.Add(new StadtbauFund("Die Tür nach draußen ist zugestellt: am Eingang kann niemand stehen.",
                "Den Eingang freiräumen oder die Wand dort zurückziehen.", true, start.Item1, start.Item2));
            return funde;
        }
        var erreichbar = Wohnung.Erreichbar(frei, start);
        foreach (var r in Liste(g["raeume"]).Select(Objekt))
        {
            double x = Py.F(r["x"]), y = Py.F(r["y"]), w = Py.F(r["w"]), h = Py.F(r["h"]);
            if (!erreichbar.Select(Wege.Mitte).Any(c => x <= c.Item1 && c.Item1 < x + w && y <= c.Item2 && c.Item2 < y + h))
            {
                funde.Add(new StadtbauFund($"Raum „{Py.Str(Get(r, "name", r["id"]))}“ ist vom Eingang aus nicht erreichbar.",
                    "Die Tür des Raums zum Flur hin setzen oder den Weg freiräumen.", true, x + (w / 2), y + (h / 2)));
            }
        }
        // Ladepruefung des Spiels fuer diesen Grundriss (Personen im eigenen Raum, Tueren zum Flur, bekannte Einrichtung)
        // Einrichtung (Typen, Lage im Raum) prueft das Werkzeug mit seinem ganzen Katalog; die Ladepruefung kennt nur die Typen
        // der Spieldaten (nicht die angehaengten Zusaetze), darum hier nur ihre Regeln zu Raeumen, Tueren, Personen und Kartons
        foreach (var p in Ladepruefung(raum, ort, g, inhaltNeu).Where(p => !p.Contains("Einrichtung '", StringComparison.Ordinal)))
        {
            funde.Add(new StadtbauFund("Das Spiel lädt den Raum so nicht: " + p, "Letzten Schritt rückgängig machen oder die Stelle anpassen.", true));
        }
        if (raum.StartsWith("buero:firma", StringComparison.Ordinal) || raum.StartsWith(OrtFiliale, StringComparison.Ordinal))
        {
            Arbeitsplaetze(raum, inhalt, frei, erreichbar, funde);
        }
        if (wohnung)
        {
            WohnungPruefen(raum["zuhause:".Length..], inhaltNeu, funde);
        }
        Auftraege(alt, g, inhalt, frei, erreichbar, funde);
        return funde;
    }

    private static (double X, double Y) TuerMitte(PyObjekt r, PyObjekt g, PyObjekt t)
    {
        var mitte = (Py.F(t["von"]) + Py.F(t["bis"])) / 2;
        return Wege.TuerSeite(r, g) switch
        {
            "w" => (Py.F(r["x"]), mitte),
            "o" => (Py.F(r["x"]) + Py.F(r["w"]), mitte),
            "s" => (mitte, Py.F(r["y"]) + Py.F(r["h"])),
            _ => (mitte, Py.F(r["y"])),
        };
    }

    private static List<string> Ladepruefung(string raum, string ort, PyObjekt g, PyObjekt inhalt)
    {
        if (ort == Innenraum.Serverfarm)
        {
            return [];
        }
        if (ort == OrtZuhause)
        {
            var id = raum["zuhause:".Length..];
            var vorne = PyFormat.Prozent("Spiel-Wohnung %s", id);
            return [.. SpielPruefung.Wohnungen(inhalt).Where(p => p.StartsWith(vorne + ":", StringComparison.Ordinal) || p.StartsWith(vorne + "-", StringComparison.Ordinal))];
        }
        if (raum == "buero:bitweiche")
        {
            return SpielPruefung.Gebaeude(g, "Spiel", ListeOderLeer(inhalt, "kollegen").Select(Objekt).Where(k => Py.Wahr(Get(k, "raum")) && RaumIn(g, k["raum"])));
        }
        if (raum.StartsWith("kunde:", StringComparison.Ordinal))
        {
            var kunde = Kundenorte(inhalt).FirstOrDefault(k => Py.Str(k["id"]) == ort);
            return kunde is null ? [] : SpielPruefung.Gebaeude(g, PyFormat.Prozent("Spiel-Kunde %s", ort), Py.Hat(kunde, "personen") ? ListeOderLeer(kunde, "personen").Select(Objekt) : []);
        }
        return SpielPruefung.Gebaeude(g, raum, [.. Plaetze(raum, inhalt).Select((pl, i) => new PyObjekt
        {
            { "id", "platz" + i.ToString(CultureInfo.InvariantCulture) }, { "raum", pl.Raum }, { "platz", new List<object?> { pl.X, pl.Y } },
        })]);
    }

    private static bool RaumIn(PyObjekt g, object? id) => Liste(g["raeume"]).Select(Objekt).Any(r => Py.Gleich(r["id"], id));

    /// <summary>Arbeitsplaetze einer Stufe der Firma bzw. der Zweigstelle (x, y, Raum).</summary>
    public static List<(double X, double Y, object? Raum)> Plaetze(string raum, PyObjekt inhalt)
    {
        var firma = Get(inhalt, "firma") as PyObjekt ?? [];
        var (bereich, nummer) = raum.StartsWith("buero:firma", StringComparison.Ordinal)
            ? (Get(firma, "gebaeude") as PyObjekt, raum["buero:firma".Length..])
            : (Get(firma, "filiale") as PyObjekt, raum[OrtFiliale.Length..]);
        if (bereich is null || !int.TryParse(nummer, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return [];
        }
        var stufen = ListeOderLeer(bereich, "stufen");
        if (n < 1 || n > stufen.Count)
        {
            return [];
        }
        return [.. ListeOderLeer(Objekt(stufen[n - 1]), "plaetze").Select(Liste).Where(p => p.Count == 3).Select(p => (Py.F(p[0]), Py.F(p[1]), p[2]))];
    }

    private static void Arbeitsplaetze(string raum, PyObjekt inhalt, PyMenge frei, HashSet<(long, long)> erreichbar, List<StadtbauFund> funde)
    {
        foreach (var (x, y, _) in Plaetze(raum, inhalt))
        {
            var zelle = Wege.Zelle(x, y);
            if (!frei.Contains(zelle))
            {
                funde.Add(new StadtbauFund(string.Create(CultureInfo.GetCultureInfo("de-DE"), $"Schreibtisch der Firma: Arbeitsplatz bei {x:0.0}/{y:0.0} ist verstellt."),
                    "Möbel oder Wand vom Arbeitsplatz wegnehmen.", true, x, y));
            }
            else if (!erreichbar.Contains(zelle))
            {
                funde.Add(new StadtbauFund(string.Create(CultureInfo.GetCultureInfo("de-DE"), $"Schreibtisch der Firma: Arbeitsplatz bei {x:0.0}/{y:0.0} ist nicht erreichbar."),
                    "Den Weg vom Eingang zum Arbeitsplatz freiräumen.", true, x, y));
            }
        }
    }

    /// <summary>Moebel, fuer die eine Wohnung immer einen erreichbaren Platz haben muss (Schreibtisch mit PC, Kleiderschrank).</summary>
    public static readonly IReadOnlyList<(string Moebel, string Name)> WohnungBraucht = [("schreibtisch", "Schreibtisch (PC)"), ("kleiderschrank", "Kleiderschrank")];

    private static void WohnungPruefen(string id, PyObjekt inhalt, List<StadtbauFund> funde)
    {
        var bleibe = Wohnung.Bleibe(id, inhalt);
        if (bleibe is null)
        {
            return;
        }
        foreach (var (moebel, name) in WohnungBraucht)
        {
            if (Wohnung.MoebelStueck(moebel, inhalt) is not null && !PlatzFuer(id, moebel, inhalt))
            {
                funde.Add(new StadtbauFund($"In der Wohnung ist kein erreichbarer Platz mehr für den {name}.",
                    "Räume größer machen oder Wände so ziehen, dass das Möbel hineinpasst.", true));
            }
        }
    }

    /// <summary>
    /// Gibt es in der Wohnung einen Platz, an dem das Moebel nach den Regeln des Einrichtens stehen darf (neben der
    /// Start-Einrichtung) und erreichbar bleibt, ohne einen Raum oder ein anderes Moebel zu versperren?
    /// </summary>
    public static bool PlatzFuer(string wohnungId, string moebel, PyObjekt inhalt)
    {
        var bleibe = Wohnung.Bleibe(wohnungId, inhalt)!;
        var stand = new Spielstand([], inhalt) { Wohnung = bleibe["id"], Moebel = new PyDict() };
        var start = Get(Objekt(inhalt["wohnungen"]), "start_moebel") as PyObjekt ?? [];
        foreach (var (stueck, kennung) in start)
        {
            stand.Moebel[stueck] = kennung;
        }
        const string Probe = "_pruefung";
        stand.Moebel[Probe] = moebel;
        var basis = new PyObjekt();
        foreach (var (k, v) in Get(Get(bleibe, "einrichtung") as PyObjekt ?? [], "moebel") as PyObjekt ?? [])
        {
            basis.Add(k, Py.Kopie(v));
        }
        var g = Objekt(bleibe["gebaeude"]);
        double breite = Py.F(g["breite"]), hoehe = Py.F(g["hoehe"]);
        var flur = Objekt(g["flur"]);
        var sp = Liste(flur["spieler"]);
        (double, double) startplatz = (Py.F(sp[0]), Py.F(sp[1]));
        for (long dreh = 0; dreh < 4; dreh++)
        {
            for (var y = 0.0; y < hoehe; y += Suchraster)
            {
                for (var x = 0.0; x < breite; x += Suchraster)
                {
                    var einrichtung = new PyObjekt { { "moebel", basis }, { "boeden", new PyObjekt() } };
                    if (Wohnung.PlatzProblem(stand, Probe, moebel, x, y, dreh, einrichtung).Length > 0)
                    {
                        continue;
                    }
                    var mit = new PyObjekt();
                    foreach (var (k, v) in basis)
                    {
                        mit.Add(k, v);
                    }
                    mit.Add(Probe, new List<object?> { x, y, dreh });
                    var einr = new PyObjekt { { "moebel", mit }, { "boeden", new PyObjekt() } };
                    var grundriss = SpielOrte.WohnungsGrundriss(stand, einr, 0);
                    var ziele = Wohnung.WegZiele(stand, einr);
                    var (w, h) = Wohnung.Groesse(Wohnung.MoebelStueck(moebel, inhalt)!, dreh);
                    ziele.Add(new PyObjekt { { "art", "moebel" }, { "name", moebel }, { "stueck", Probe }, { "x", x }, { "y", y }, { "w", w }, { "h", h } });
                    if (Wohnung.Unerreichbar(Wege.BasisRaster(grundriss), startplatz, ziele).Count == 0)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static void Auftraege(PyObjekt alt, PyObjekt g, PyObjekt inhalt, PyMenge frei, HashSet<(long, long)> erreichbar, List<StadtbauFund> funde)
    {
        var ids = Liste(alt["raeume"]).Select(Objekt).Select(r => Py.Str(r["id"])).ToHashSet(StringComparer.Ordinal);
        foreach (var liste in new[] { "aufgaben", "zwischenfaelle" })
        {
            foreach (var a in ListeOderLeer(inhalt, liste).OfType<PyObjekt>())
            {
                if (Get(a, "raum") is not string raumId || !ids.Contains(raumId) || Get(a, "stelle") is not List<object?> { Count: >= 2 } stelle)
                {
                    continue;
                }
                var (x, y) = (Py.F(stelle[0]), Py.F(stelle[1]));
                var hier = Wege.RaumBei(x, y, g);
                var titel = Py.Str(Get(a, "titel", Get(a, "id", "")));
                if (hier is null || !Py.Gleich(hier["id"], raumId))
                {
                    funde.Add(new StadtbauFund($"Auftrag „{titel}“: seine Stelle liegt nicht mehr im Raum.", "Die Wand so ziehen, dass die Stelle im Raum bleibt.", true, x, y));
                }
                else if (!erreichbar.Select(Wege.Mitte).Any(c => ((c.Item1 - x) * (c.Item1 - x)) + ((c.Item2 - y) * (c.Item2 - y)) <= Wege.Reichweite * Wege.Reichweite))
                {
                    // wie im Spiel: die Stelle darf an einem Moebel liegen, die Figur muss nur in Reichweite kommen
                    funde.Add(new StadtbauFund($"Auftrag „{titel}“: seine Stelle ist nicht erreichbar.", "Den Weg zur Stelle freiräumen.", true, x, y));
                }
            }
        }
    }
}
