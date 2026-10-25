using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Zufall;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Wohnung, Moebel und Miete wie <c>fisi_game.py</c> (Python 0.63.2, Z. 13355-13999:
/// apartment, furniture_item, home_layout, placed_furniture, placement_problem, dock_position,
/// rotated_position, furniture_at/near, home_message, rent_terms, move_offer, move_texts,
/// rent_text, moves_available). Die Zeichnung der Wohnung (home_building) kommt mit der Darstellung.
/// </summary>
public static class Wohnung
{
    public const double Raster = 0.25;           // GRID
    public const double TuerFrei = 1.0;          // DOOR_CLEARANCE
    public const double MoebelReichweite = 0.9;  // FURNITURE_REACH
    public const double EingangReichweite = 1.4; // ENTRANCE_REACH

    /// <summary>WALK_FREE: darueber laeuft man (oder es haengt an der Wand).</summary>
    public static readonly IReadOnlyList<string> Begehbar = ["teppich", "fussmatte", "whiteboard", "markierung", "bild", "wimpel", "kamera"];

    /// <summary>Ein aufgestelltes Moebel (placed_furniture).</summary>
    public sealed record Aufgestellt(string Typ, double X, double Y, object? W, object? H, long Dreh, string Stueck, object? Moebel, object? Blick);

    private static PyObjekt Wohnungen(PyObjekt inhalt) => Objekt(inhalt["wohnungen"]);

    /// <summary>apartment</summary>
    public static PyObjekt? Bleibe(object? kennung, PyObjekt inhalt) =>
        Liste(Wohnungen(inhalt)["wohnungen"]).Select(Objekt).FirstOrDefault(w => Py.Gleich(w["id"], kennung));

    /// <summary>furniture_item</summary>
    public static PyObjekt? MoebelStueck(object? kennung, PyObjekt inhalt) =>
        Liste(Wohnungen(inhalt)["moebel"]).Select(Objekt).FirstOrDefault(m => Py.Gleich(m["id"], kennung));

    /// <summary>shop_items</summary>
    public static List<PyObjekt> Laden(PyObjekt inhalt) =>
        [.. Liste(Wohnungen(inhalt)["moebel"]).Select(Objekt).Where(m => Py.Wahr(Get(m, "laden", true)))];

    /// <summary>furniture_size: (Breite, Hoehe) nach der Drehung.</summary>
    public static (object? W, object? H) Groesse(PyObjekt moebel, object? dreh) =>
        PyMod(Py.Int(dreh), 2) != 0 ? (moebel["h"], moebel["w"]) : (moebel["w"], moebel["h"]);

    private static long PyMod(long a, long b) => ((a % b) + b) % b;

    /// <summary>home_layout: {"moebel": {stueck: [x, y, dreh]}, "boeden": {raum: [art, farbe]}}, nur Moebel, die man besitzt.</summary>
    public static PyObjekt Einrichtung(Spielstand stand)
    {
        var bleibe = Bleibe(stand.Wohnung, stand.Inhalt)!;
        var daten = stand.Einrichtungen.Get(stand.Wohnung) as PyObjekt
            ?? (stand.Einrichtungen.ContainsKey(stand.Wohnung) && stand.Einrichtungen[stand.Wohnung] is not null
                ? throw new InvalidDataException("Einrichtung ist kein dict")
                : Get(bleibe, "einrichtung") as PyObjekt ?? []);
        var moebel = new PyObjekt();
        foreach (var (k, v) in Get(daten, "moebel") as PyObjekt ?? [])
        {
            if (stand.Moebel.ContainsKey(k))
            {
                moebel.Add(k, Liste(v).ToList());
            }
        }
        var boeden = new PyObjekt();
        foreach (var (k, v) in Get(daten, "boeden") as PyObjekt ?? [])
        {
            boeden.Add(k, Liste(v).ToList());
        }
        return new PyObjekt { { "moebel", moebel }, { "boeden", boeden } };
    }

    /// <summary>placed_furniture: aufgestellte Moebel nach Stueck sortiert.</summary>
    public static List<Aufgestellt> AufgestellteMoebel(Spielstand stand, PyObjekt? einrichtung = null)
    {
        einrichtung ??= Einrichtung(stand);
        var ergebnis = new List<Aufgestellt>();
        foreach (var (k, v) in Objekt(einrichtung["moebel"]).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var wert = Liste(v);
            if (wert.Count != 3)
            {
                throw new InvalidDataException("ValueError: Moebel braucht x, y, dreh");
            }
            var moebel = MoebelStueck(stand.Moebel.Get(k), stand.Inhalt);
            if (!Py.Wahr(moebel))
            {
                continue;
            }
            var (w, h) = Groesse(moebel!, wert[2]);
            ergebnis.Add(new Aufgestellt(Py.Str(moebel!["typ"]), Py.F(wert[0]), Py.F(wert[1]), w, h, Py.Int(wert[2]), k, moebel["id"], Get(moebel, "blick", "s")));
        }
        return ergebnis;
    }

    /// <summary>boxed_furniture: Moebel im Karton als (stueck, moebel), nach Stueck sortiert.</summary>
    public static List<(object? Stueck, object? Moebel)> ImKarton(Spielstand stand, PyObjekt? einrichtung = null)
    {
        einrichtung ??= Einrichtung(stand);
        var aufgestellt = Objekt(einrichtung["moebel"]);
        return [.. stand.Moebel.Where(p => !Py.Hat(aufgestellt, Py.Str(p.Key))).Select(p => (p.Key, p.Value)).OrderBy(p => Py.Str(p.Key), StringComparer.Ordinal)];
    }

    /// <summary>Eine Flaeche; Roh haelt die Werte mit ihrem Python-Typ (int bleibt int, fuer dock_position).</summary>
    private readonly record struct Flaeche(double X, double Y, double W, double H, object? Id = null, object? Name = null, PyObjekt? Roh = null);

    private static bool Ueberlappt(Flaeche a, Flaeche b) =>
        a.X < b.X + b.W - 1e-6 && b.X < a.X + a.W - 1e-6 && a.Y < b.Y + b.H - 1e-6 && b.Y < a.Y + a.H - 1e-6;

    private static bool Innen(Flaeche d, Flaeche f) => f.X <= d.X && d.X + d.W <= f.X + f.W && f.Y <= d.Y && d.Y + d.H <= f.Y + f.H;

    private static Flaeche AusDict(PyObjekt d) => new(Py.F(d["x"]), Py.F(d["y"]), Py.F(d["w"]), Py.F(d["h"]), Get(d, "id"), Get(d, "name"), d);

    /// <summary>_areas: Raeume und Flur als Flaechen.</summary>
    private static List<Flaeche> Bereiche(PyObjekt gebaeude)
    {
        var flur = Objekt(gebaeude["flur"]);
        var bereiche = Liste(gebaeude["raeume"]).Select(Objekt).Select(AusDict).ToList();
        var roh = new PyObjekt { { "id", "flur" }, { "name", Get(flur, "name", "Flur") }, { "x", 0L }, { "y", flur["y"] }, { "w", gebaeude["breite"] }, { "h", flur["h"] } };
        bereiche.Add(new Flaeche(0, Py.F(flur["y"]), Py.F(gebaeude["breite"]), Py.F(flur["h"]), "flur", Get(flur, "name", "Flur"), roh));
        return bereiche;
    }

    /// <summary>_door_zones: Flaechen vor und hinter jeder Tuer, die frei bleiben muessen.</summary>
    private static List<Flaeche> TuerZonen(PyObjekt gebaeude)
    {
        var zonen = new List<Flaeche>();
        foreach (var raum in Liste(gebaeude["raeume"]).Select(Objekt))
        {
            if (Get(raum, "tuer") is not PyObjekt tuer || tuer.Count == 0)
            {
                continue;
            }
            var seite = Wege.TuerSeite(raum, gebaeude);
            var (von, bis) = (Py.F(tuer["von"]), Py.F(tuer["bis"]));
            if (seite is "s" or "n")
            {
                var wandY = seite == "s" ? Py.F(raum["y"]) + Py.F(raum["h"]) : Py.F(raum["y"]);
                zonen.Add(new Flaeche(von, wandY - TuerFrei, bis - von, 2 * TuerFrei));
            }
            else
            {
                var wandX = seite == "w" ? Py.F(raum["x"]) : Py.F(raum["x"]) + Py.F(raum["w"]);
                zonen.Add(new Flaeche(wandX - TuerFrei, von, 2 * TuerFrei, bis - von));
            }
        }
        var eingang = Objekt(Objekt(gebaeude["flur"])["eingang"]);
        zonen.Add(new Flaeche(0, Py.F(eingang["von"]), TuerFrei * 1.5, Py.F(eingang["bis"]) - Py.F(eingang["von"])));
        return zonen;
    }

    /// <summary>snap: auf das Raster legen.</summary>
    public static double Einrasten(object? wert) => PythonRunden.Runde(Py.F(wert) / Raster) * Raster;

    private static PyObjekt Grundriss(Spielstand stand) => Objekt(Bleibe(stand.Wohnung, stand.Inhalt)!["gebaeude"]);

    /// <summary>placement_problem: warum ein Moebel hier nicht stehen kann ("" = passt).</summary>
    public static string PlatzProblem(Spielstand stand, object? stueck, object? moebelKennung, object? x, object? y, object? dreh, PyObjekt? einrichtung = null)
    {
        var moebel = MoebelStueck(moebelKennung, stand.Inhalt);
        if (moebel is null)
        {
            return "Dieses Möbelstück gibt es nicht.";
        }
        var gebaeude = Grundriss(stand);
        var (w, h) = Groesse(moebel, dreh);
        var kasten = new Flaeche(Py.F(x), Py.F(y), Py.F(w), Py.F(h));
        if (!Bereiche(gebaeude).Any(b => Innen(kasten, b)))
        {
            return Prozent("%s passt hier nicht hin: Es muss ganz in einem Raum stehen.", moebel["name"]);
        }
        var frei = Begehbar.Contains(Py.Str(moebel["typ"]));
        if (!frei && TuerZonen(gebaeude).Any(z => Ueberlappt(kasten, z)))
        {
            return Prozent("%s würde eine Tür versperren.", moebel["name"]);
        }
        var andere = new List<(string Typ, Flaeche F, object? Moebel)>();
        foreach (var raum in Liste(gebaeude["raeume"]).Select(Objekt))
        {
            andere.AddRange(ListeOderLeerGet(raum, "deko").Select(Objekt).Select(d => (Py.Str(d["typ"]), AusDict(d), Get(d, "moebel"))));
        }
        andere.AddRange(ListeOderLeerGet(Objekt(gebaeude["flur"]), "deko").Select(Objekt).Select(d => (Py.Str(d["typ"]), AusDict(d), Get(d, "moebel"))));
        foreach (var a in AufgestellteMoebel(stand, einrichtung ?? Einrichtung(stand)))
        {
            if (!Py.Gleich(a.Stueck, stueck))
            {
                andere.Add((a.Typ, new Flaeche(a.X, a.Y, Py.F(a.W), Py.F(a.H)), a.Moebel));
            }
        }
        foreach (var (typ, f, kennung) in andere)
        {
            var anderesFrei = Begehbar.Contains(typ);
            if (frei != anderesFrei && (frei || anderesFrei))
            {
                continue;   // Teppich unter Moebeln ist erlaubt
            }
            if (Ueberlappt(kasten, f))
            {
                var name = MoebelStueck(kennung, stand.Inhalt);
                return Prozent("%s stößt an %s.", moebel["name"], name is not null ? name["name"] : "die Einrichtung");
            }
        }
        return "";
    }

    private static List<object?> ListeOderLeerGet(PyObjekt d, string k) => Py.Hat(d, k) ? Liste(d[k]) : [];

    private static Flaeche? BereichBei(PyObjekt gebaeude, double x, double y) =>
        Bereiche(gebaeude).Where(b => b.X <= x && x < b.X + b.W && b.Y <= y && y < b.Y + b.H).Cast<Flaeche?>().FirstOrDefault();

    /// <summary>round(x, 2): ein int bleibt int (wie in Python).</summary>
    private static object Rund2(object? wert) => wert is long l ? (object)l : PythonRundenStellen.Runde(Py.F(wert), 2);

    /// <summary>dock_position: Ecke oben links fuer ein Moebel mit der Mitte bei (cx, cy), an die Wand gerueckt.
    /// Liegt es an der Wand, ist der Wert wie in Python die Zahl aus dem Grundriss (oft int).</summary>
    public static (object X, object Y) Andocken(Spielstand stand, object? moebelKennung, double cx, double cy, object? dreh)
    {
        var moebel = MoebelStueck(moebelKennung, stand.Inhalt)!;
        var (w, h) = Groesse(moebel, dreh);
        object x = Einrasten(cx - Py.F(w) / 2.0);
        object y = Einrasten(cy - Py.F(h) / 2.0);
        if (BereichBei(Grundriss(stand), cx, cy)?.Roh is { } b)
        {
            if (Py.F(w) <= Py.F(b["w"]))
            {
                x = Py.Min(Py.Max(x, b["x"]), Py.Minus(Py.Plus(b["x"], b["w"]), w))!;
            }
            if (Py.F(h) <= Py.F(b["h"]))
            {
                y = Py.Min(Py.Max(y, b["y"]), Py.Minus(Py.Plus(b["y"], b["h"]), h))!;
            }
        }
        return (Rund2(x), Rund2(y));
    }

    /// <summary>rotated_position: nach dem Drehen liegt, was an einer Wand lag, wieder an derselben Wand.</summary>
    public static (object X, object Y) Gedreht(Spielstand stand, object? moebelKennung, List<object?> platz, object? dreh)
    {
        var moebel = MoebelStueck(moebelKennung, stand.Inhalt)!;
        var (aw, ah) = Groesse(moebel, platz[2]);
        var (altW, altH) = (Py.F(aw), Py.F(ah));
        var (px, py) = (Py.F(platz[0]), Py.F(platz[1]));
        var cx = px + altW / 2.0;
        var cy = py + altH / 2.0;
        var (x, y) = Andocken(stand, moebelKennung, cx, cy, dreh);
        var (w, h) = Groesse(moebel, dreh);
        if (BereichBei(Grundriss(stand), cx, cy)?.Roh is { } b)
        {
            var (rechts, unten) = (Py.Plus(b["x"], b["w"]), Py.Plus(b["y"], b["h"]));
            if (Math.Abs(px - Py.F(b["x"])) < 0.01)
            {
                x = b["x"]!;
            }
            else if (Math.Abs(px + altW - Py.F(rechts)) < 0.01 && Py.F(w) <= Py.F(b["w"]))
            {
                x = Py.Minus(rechts, w);
            }
            if (Math.Abs(py - Py.F(b["y"])) < 0.01)
            {
                y = b["y"]!;
            }
            else if (Math.Abs(py + altH - Py.F(unten)) < 0.01 && Py.F(h) <= Py.F(b["h"]))
            {
                y = Py.Minus(unten, h);
            }
        }
        return (Rund2(x), Rund2(y));
    }

    /// <summary>furniture_at: aufgestelltes Moebel an (x, y), ein Teppich nur, wenn nichts darauf steht.</summary>
    public static Aufgestellt? MoebelBei(Spielstand stand, double x, double y) =>
        AufgestellteMoebel(stand).Where(m => m.X <= x && x <= m.X + Py.F(m.W) && m.Y <= y && y <= m.Y + Py.F(m.H))
            .OrderBy(m => Begehbar.Contains(m.Typ) ? 1 : 0).FirstOrDefault();

    /// <summary>furniture_near: benutzbares Moebel in Reichweite der Figur.</summary>
    public static Aufgestellt? MoebelNahe(Spielstand stand, double x, double y)
    {
        Aufgestellt? beste = null;
        double? besteWeite = null;
        foreach (var m in AufgestellteMoebel(stand))
        {
            var info = MoebelStueck(m.Moebel, stand.Inhalt);
            if (info is null || !(Py.Wahr(Get(info, "aktion")) || Py.Wahr(Get(info, "texte"))))
            {
                continue;
            }
            var dx = Math.Max(Math.Max(m.X - x, 0), x - m.X - Py.F(m.W));
            var dy = Math.Max(Math.Max(m.Y - y, 0), y - m.Y - Py.F(m.H));
            var weite = PythonHypot(dx, dy);
            if (weite <= MoebelReichweite && (besteWeite is null || weite < besteWeite))
            {
                (beste, besteWeite) = (m, weite);
            }
        }
        return beste;
    }

    /// <summary>math.hypot fuer zwei Werte (korrekt gerundet wie CPython 3.12).</summary>
    private static double PythonHypot(double a, double b) => double.Hypot(a, b);

    /// <summary>at_entrance: steht die Figur an der Eingangstuer (links im Flur)?</summary>
    public static bool AmEingang(double x, double y, PyObjekt gebaeude)
    {
        var eingang = Objekt(Objekt(gebaeude["flur"])["eingang"]);
        return x <= EingangReichweite && Py.F(eingang["von"]) - 0.6 <= y && y <= Py.F(eingang["bis"]) + 0.6;
    }

    /// <summary>home_message: (Ueberschrift, Text, [(aktion, Beschriftung)]) in der Wohnung.</summary>
    public static (string Titel, string Text, List<(string Aktion, string Name)> Knoepfe) Nachricht(double x, double y, Spielstand stand)
    {
        var bleibe = Bleibe(stand.Wohnung, stand.Inhalt)!;
        var gebaeude = Objekt(bleibe["gebaeude"]);
        if (AmEingang(x, y, gebaeude))
        {
            return ("Wohnungstür", "Von hier geht es zur Arbeit.", [("buero", "Zur Arbeit")]);
        }
        if (MoebelNahe(stand, x, y) is { } m)
        {
            var info = MoebelStueck(m.Moebel, stand.Inhalt)!;
            var texte = Py.Wahr(Get(info, "texte")) ? Liste(info["texte"]) : [];
            var text = texte.Count > 0 ? Py.Str(texte[(int)(Wuerfel.Werfen(stand.ErstesEreignis ?? "", stand.Tag, m.Stueck) * texte.Count)]) : "";
            var knoepfe = new List<(string, string)>();
            if (Py.Gleich(Get(info, "aktion"), "lernen"))
            {
                knoepfe.Add(("lernen", "Lernen"));
                text = text.Length > 0 ? text : "Hier lernt es sich in Ruhe.";
            }
            else if (Py.Gleich(Get(info, "aktion"), "schlafen"))
            {
                knoepfe.Add(("schlafen", "Schlafen"));
                text = text.Length > 0 ? text : "Ein gemütliches Plätzchen.";
            }
            return (Py.Str(info["name"]), text, knoepfe);
        }
        var bereich = BereichBei(gebaeude, x, y);
        var wo = bereich is { } b && !Py.Gleich(b.Id, "flur") ? Prozent("Du bist im Raum %s.", b.Name) : "Du bist in der Diele.";
        return (Py.Str(bleibe["name"]), wo + " Geh zum Bett, zum Schreibtisch oder zur Wohnungstür.", []);
    }

    /// <summary>sleep_text</summary>
    public static string SchlafText(Spielstand stand)
    {
        var texte = Get(Objekt(stand.Inhalt["story"]), "schlafen");
        if (!Py.Wahr(texte))
        {
            return "Gute Nacht!";
        }
        if (texte is string s)
        {
            return s;
        }
        var liste = Liste(texte);
        return Py.Str(liste[(int)(((stand.Tag % liste.Count) + liste.Count) % liste.Count)]);
    }

    /// <summary>rent_terms: (Miete pro Arbeitstag, Kaution) laut balancing.json.</summary>
    public static (long Miete, long Kaution) MietBedingungen(PyObjekt bleibe, PyObjekt inhalt)
    {
        var regeln = Objekt(Objekt(inhalt["balancing"])["miete"]);
        var miete = Py.Int(Get(Objekt(regeln["pro_tag"]), Py.Str(bleibe["id"]), 0L));
        var kaution = Bewertung.Skaliert(Get(bleibe, "preis", 0L), regeln["kaution_anteil"]);
        return (miete, kaution);
    }

    /// <summary>move_offer: {"kosten", "miete", "zurueck", "fehlt"} eines Umzugs.</summary>
    public static PyObjekt UmzugAngebot(Spielstand stand, PyObjekt bleibe, bool miete)
    {
        var (proTag, kosten) = miete ? MietBedingungen(bleibe, stand.Inhalt) : (0L, Py.Int(Get(bleibe, "preis", 0L)));
        var zurueck = stand.Kaution;
        var fehlt = Math.Max(0, kosten - Math.Max(0, stand.Geld + zurueck));
        return new PyObjekt { { "kosten", kosten }, { "miete", proTag }, { "zurueck", zurueck }, { "fehlt", fehlt } };
    }

    /// <summary>_whole_euro: "{:,.0f} €" mit Tausenderpunkt.</summary>
    public static string GanzeEuro(object? wert)
    {
        var text = wert is double d ? Fest(d, 0) : Py.Int(wert).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var minus = text.StartsWith('-') ? "-" : "";
        var ziffern = text.TrimStart('-');
        var gruppen = new List<string>();
        while (ziffern.Length > 3)
        {
            gruppen.Insert(0, ziffern[^3..]);
            ziffern = ziffern[..^3];
        }
        gruppen.Insert(0, ziffern);
        return minus + string.Join(".", gruppen) + " €";
    }

    /// <summary>move_texts: (Preiszeile, Rueckfrage vor dem Umzug).</summary>
    public static (string Preis, string Frage) UmzugTexte(Spielstand stand, PyObjekt bleibe, bool miete)
    {
        var angebot = UmzugAngebot(stand, bleibe, miete);
        string preis, frage;
        if (miete)
        {
            preis = Prozent("Kaution %s · Miete %s/Tag", GanzeEuro(angebot["kosten"]), GanzeEuro(angebot["miete"]));
            frage = Prozent("In die Wohnung „%s“ ziehen? Kaution %s, danach %s Miete pro Arbeitstag.", bleibe["name"], GanzeEuro(angebot["kosten"]), GanzeEuro(angebot["miete"]));
        }
        else
        {
            preis = GanzeEuro(angebot["kosten"]);
            frage = Prozent("Für %s in die Wohnung „%s“ umziehen?", preis, bleibe["name"]);
        }
        if (Py.Wahr(angebot["zurueck"]))
        {
            frage += Prozent(" Die Kaution von %s für deine jetzige Wohnung bekommst du zurück.", GanzeEuro(angebot["zurueck"]));
        }
        return (preis, frage + " Alle Möbel kommen dabei in Umzugskartons.");
    }

    /// <summary>rent_text: Zeile zur jetzigen Wohnung ("" = gekauft oder Startwohnung).</summary>
    public static string MietText(Spielstand stand) => stand.Miete != 0
        ? Prozent("Zur Miete: %s pro Arbeitstag, Kaution %s hinterlegt", GanzeEuro(stand.Miete), GanzeEuro(stand.Kaution))
        : "";

    /// <summary>day_end_money_text: "Gehalt: +190 €." bzw. mit Miete; mit Firma der Tagesabschluss der Firma.</summary>
    public static string FeierabendGeldText(PyObjekt daten, PyObjekt? inhalt = null)
    {
        var text = Py.Wahr(Get(daten, "firma")) ? Betrieb.FirmenTagText(Objekt(daten["firma"]), inhalt ?? throw new ArgumentNullException(nameof(inhalt)))
            : "Gehalt: +" + GanzeEuro(Get(daten, "gehalt", 0L)) + ".";
        if (Py.Wahr(Get(daten, "miete")))
        {
            text += " Miete: -" + GanzeEuro(daten["miete"]) + ".";
        }
        return text;
    }

    /// <summary>moves_available: Wohnungen, die teurer sind als die jetzige.</summary>
    public static List<PyObjekt> Umzuege(Spielstand stand)
    {
        var jetzt = Bleibe(stand.Wohnung, stand.Inhalt)!;
        return [.. Liste(Wohnungen(stand.Inhalt)["wohnungen"]).Select(Objekt).Where(w => Py.Kleiner(Get(jetzt, "preis", 0L), Get(w, "preis", 0L)))];
    }
}
