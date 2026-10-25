using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Suche;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Inhaltspruefung der Spielinhalte wie <c>validate_game_content</c> aus
/// <c>fisi_game.py</c> (Python 0.63.2, Z. 485-2830): Grundrisse, Wohnungen, Miete, Raenge,
/// Hardware, Rack-Geraete, Falschlieferung und jeder Auftrag mit den Pruefungen seines Typs,
/// in derselben Reihenfolge und mit denselben Texten. Leere Liste = in Ordnung.
/// Noch nicht enthalten (kommen mit ihrer Logik in W3): _validate_firm, _validate_loans,
/// _validate_business, _validate_world, _validate_achievements, _validate_farm.
/// </summary>
public static class SpielPruefung
{
    public const int MaxTicket = 700;     // MAX_TICKET_CHARS
    public const int MaxOption = 110;     // MAX_OPTION_CHARS
    public const int MaxTerminal = 60;    // MAX_TERMINAL_CHARS
    public const int MaxBefehl = 120;     // MAX_COMMAND_CHARS
    public const int MaxErgebnis = 220;   // MAX_RESULT_CHARS

    public static readonly IReadOnlyList<string> Prioritaeten = ["niedrig", "normal", "hoch", "kritisch"];

    public static readonly IReadOnlyList<string> Aufgabentypen = ["auswahl", "zuordnung", "bauteile", "bestellung", "rack", "formular", "terminal", "diagnose", "wartung"];

    public static readonly IReadOnlyList<string> Steckplaetze = ["mainboard", "cpu", "ram", "ssd", "gpu", "netzteil", "gehaeuse"];

    public static readonly IReadOnlyDictionary<string, string[]> TeilFelder = new Dictionary<string, string[]>
    {
        ["mainboard"] = ["sockel", "ram_typ", "formfaktor", "ram_slots", "m2"],
        ["cpu"] = ["sockel", "tdp", "igpu"],
        ["ram"] = ["ram_typ", "groesse", "module"],
        ["ssd"] = ["anschluss", "groesse"],
        ["gpu"] = ["watt"],
        ["netzteil"] = ["watt"],
        ["gehaeuse"] = ["formfaktoren"],
        ["notebook"] = ["groesse"],
    };

    public static readonly IReadOnlyList<string> BauVorgaben = ["ram_min", "speicher_min", "grafikkarte", "budget"];

    public static readonly IReadOnlyList<string> RackFelder = ["typ", "name", "he", "gewicht", "watt"];

    public static readonly IReadOnlyList<string> RackVorgaben = ["mindestens", "ports_min", "frei_min"];

    public static readonly IReadOnlyList<string> FormArten = ["ip_plan", "raid", "angebot", "leasing", "drucker"];

    public static readonly IReadOnlyList<string> Systeme = ["linux", "windows"];

    public const int AustauschMin = 3, AustauschMax = 6;   // SPARE_STEPS

    /// <summary>DECO_TYPES</summary>
    public static readonly IReadOnlyList<string> Einrichtung =
    [
        "schreibtisch", "pflanze", "regal", "aktenregal", "sessel", "besprechungstisch", "kaffeemaschine", "rack", "usv", "klima",
        "werkbank", "tisch", "pc", "drucker", "kartons", "whiteboard", "wasserspender", "feuerloescher", "teppich", "fussmatte", "bank",
        "hochregal", "palette", "hubwagen", "markierung",
        "bett", "matratze", "sofa", "couchtisch", "fernseher", "esstisch", "kuechenzeile", "kuehlschrank", "badewanne", "dusche", "wc",
        "waschbecken", "kleiderschrank", "stehlampe", "aquarium", "waschmaschine", "sitzsack", "kommode", "liegestuhl", "grill",
        "sonnenschirm", "hochbeet", "spielkonsole", "bild",
        "automat", "gleis", "zug", "tresen", "monitorwand", "leitstand", "absperrband", "wimpel", "anzeigetafel", "kamera", "bauzaun",
    ];

    // -- Python-Hilfen ----------------------------------------------------

    private static bool In(IEnumerable<string> liste, object? wert) => wert is string s && liste.Contains(s);

    private static bool Hat(object? behaelter, object? schluessel) => behaelter is PyObjekt o && schluessel is string s && Py.Hat(o, s);

    private static int Laenge(object? text) => text is string s ? s.EnumerateRunes().Count() : Py.Len(text);

    private static string Anfang(object? text) => string.Concat(Py.Str(text).EnumerateRunes().Take(40).Select(r => r.ToString()));

    private static bool Doppelt(IReadOnlyCollection<object?> werte) => new PySet(werte).Count != werte.Count;

    private static List<object?> L(PyObjekt d, string k) => Py.Wahr(Get(d, k)) ? Liste(d[k]) : [];

    private static PyObjekt O(PyObjekt d, string k) => Py.Wahr(Get(d, k)) ? Objekt(d[k]) : [];

    private static IEnumerable<PyObjekt> Objekte(object? liste) => Liste(liste).Select(Objekt);

    private static PyObjekt Hw(PyObjekt inhalt) => Objekt(inhalt["hardware"]);

    private static bool Innen(PyObjekt d, PyObjekt f) =>
        Py.F(f["x"]) <= Py.F(d["x"]) && Py.F(d["x"]) + Py.F(d["w"]) <= Py.F(f["x"]) + Py.F(f["w"])
        && Py.F(f["y"]) <= Py.F(d["y"]) && Py.F(d["y"]) + Py.F(d["h"]) <= Py.F(f["y"]) + Py.F(f["h"]);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Lerninhalte, Dictionary<string, bool>> Treffer = new();

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Lerninhalte, Suchtext> Suchtexte = new();

    /// <summary>learn_links_for_term(term, limit=1): findet der Begriff eine Karteikarte oder Pruefungsfrage?</summary>
    public static bool FindetLernstoff(string begriff, Lerninhalte lernen)
    {
        var cache = Treffer.GetValue(lernen, _ => []);
        lock (cache)
        {
            if (!cache.TryGetValue(begriff, out var da))
            {
                var text = Suchtexte.GetValue(lernen, l => new Suchtext(l));
                da = cache[begriff] = text.Suchen(begriff).Any(t => t.Art is Texte.SucheTexte.ArtKarte or Texte.SucheTexte.ArtQuiz);
            }
            return da;
        }
    }

    /// <summary>str.format(bestellung=..., ware=...) laeuft ohne KeyError, IndexError und ValueError?</summary>
    public static bool FormatGeht(string text, IReadOnlyCollection<string> namen)
    {
        var i = 0;
        while (i < text.Length)
        {
            var z = text[i];
            if (z == '}')
            {
                if (i + 1 < text.Length && text[i + 1] == '}')
                {
                    i += 2;
                    continue;
                }
                return false;   // Single '}' encountered in format string
            }
            if (z != '{')
            {
                i++;
                continue;
            }
            if (i + 1 < text.Length && text[i + 1] == '{')
            {
                i += 2;
                continue;
            }
            // Feld bis zur passenden schliessenden Klammer (Format-Angabe darf {} enthalten)
            var tiefe = 1;
            var j = i + 1;
            while (j < text.Length && tiefe > 0)
            {
                tiefe += text[j] == '{' ? 1 : text[j] == '}' ? -1 : 0;
                j++;
            }
            if (tiefe > 0)
            {
                return false;   // expected '}' before end of string
            }
            var feld = text[(i + 1)..(j - 1)];
            var ende = feld.IndexOfAny(['!', ':']);
            var name = ende < 0 ? feld : feld[..ende];
            var grund = name.Split('.', '[')[0];
            if (grund.Length == 0 || grund.All(char.IsAsciiDigit) || !namen.Contains(grund))
            {
                return false;   // IndexError (Platz) oder KeyError (Name)
            }
            if (ende >= 0 && feld[ende] == '!' && !(feld.Length > ende + 1 && "rsa".Contains(feld[ende + 1])
                && (feld.Length == ende + 2 || feld[ende + 2] == ':')))
            {
                return false;   // ValueError: Unknown conversion specifier
            }
            i = j;
        }
        return true;
    }

    // -- Grundrisse und Wohnungen -----------------------------------------

    /// <summary>_validate_building</summary>
    public static List<string> Gebaeude(PyObjekt gebaeude, string name, IEnumerable<PyObjekt> personen, bool fachRaeume = true)
    {
        var probleme = new List<string>();
        foreach (var raum in Objekte(gebaeude["raeume"]))
        {
            var wo = Prozent("%s-Raum %s", name, Get(raum, "id"));
            if (fachRaeume && !Py.Wahr(Get(raum, "farbe")) && !(Get(raum, "cat") is string fach && Fachbereiche.Schluessel.ContainsKey(fach)))
            {
                probleme.Add(Prozent("%s: unbekannter Fachbereich '%s'", wo, Get(raum, "cat")));
            }
            if (!fachRaeume && !Py.Wahr(Get(raum, "farbe")))
            {
                probleme.Add(Prozent("%s: Farbe fehlt", wo));
            }
            if (Py.F(raum["x"]) < 0 || Py.F(raum["y"]) < 0 || Py.F(Py.Plus(raum["x"], raum["w"])) > Py.F(gebaeude["breite"])
                || Py.F(Py.Plus(raum["y"], raum["h"])) > Py.F(gebaeude["hoehe"]))
            {
                probleme.Add(Prozent("%s: liegt ausserhalb des Grundrisses", wo));
            }
            var tuer = Get(raum, "tuer") as PyObjekt;
            if (Py.Wahr(tuer) && Wege.TuerSeite(raum, gebaeude) is "w" or "o")
            {
                var flur = Objekt(gebaeude["flur"]);
                var (von, bis) = (Py.F(tuer!["von"]), Py.F(tuer["bis"]));
                if (!(Math.Max(Py.F(raum["y"]), Py.F(flur["y"])) <= von && von < bis
                      && bis <= Math.Min(Py.F(Py.Plus(raum["y"], raum["h"])), Py.F(Py.Plus(flur["y"], flur["h"])))))
                {
                    probleme.Add(Prozent("%s: Seitentuer fuehrt nicht in den Flur", wo));
                }
            }
            else if (Py.Wahr(tuer))
            {
                var (von, bis) = (Py.F(tuer!["von"]), Py.F(tuer["bis"]));
                if (!(Py.F(raum["x"]) <= von && von < bis && bis <= Py.F(Py.Plus(raum["x"], raum["w"]))))
                {
                    probleme.Add(Prozent("%s: Tuer liegt nicht an der Raumwand", wo));
                }
            }
            foreach (var deko in Py.Hat(raum, "deko") ? Objekte(raum["deko"]) : [])
            {
                if (!In(Einrichtung, Get(deko, "typ")))
                {
                    probleme.Add(Prozent("%s: unbekannte Einrichtung '%s'", wo, Get(deko, "typ")));
                }
                else if (!Innen(deko, raum))
                {
                    probleme.Add(Prozent("%s: Einrichtung '%s' ragt aus dem Raum", wo, deko["typ"]));
                }
            }
        }
        var halle = Get(gebaeude, "flur") as PyObjekt ?? [];
        var halleFlaeche = new PyObjekt { { "x", 0L }, { "y", Get(halle, "y", 0L) }, { "w", gebaeude["breite"] }, { "h", Get(halle, "h", 0L) } };
        foreach (var deko in Py.Hat(halle, "deko") ? Objekte(halle["deko"]) : [])
        {
            if (!In(Einrichtung, Get(deko, "typ")) || !Innen(deko, halleFlaeche))
            {
                probleme.Add(Prozent("%s-Flur: Einrichtung '%s' unbekannt oder ausserhalb", name, Get(deko, "typ")));
            }
        }
        var raeume = new Dictionary<string, PyObjekt>();
        foreach (var raum in Objekte(gebaeude["raeume"]))
        {
            raeume[Py.Str(raum["id"])] = raum;
        }
        foreach (var person in personen)
        {
            var wo = Prozent("Spiel-Person %s", Get(person, "id"));
            if (Get(person, "raum") is not string r || !raeume.TryGetValue(r, out var eigener))
            {
                probleme.Add(Prozent("%s: unbekannter Raum '%s'", wo, Get(person, "raum")));
            }
            else if (Py.Wahr(Get(person, "platz")))
            {
                var platz = Liste(person["platz"]);
                if (!ReferenceEquals(Wege.RaumBei(Py.F(platz[0]), Py.F(platz[1]), gebaeude), eigener))
                {
                    probleme.Add(Prozent("%s: Platz liegt nicht im eigenen Raum", wo));
                }
            }
        }
        return probleme;
    }

    /// <summary>_validate_homes</summary>
    public static List<string> Wohnungen(PyObjekt inhalt)
    {
        var probleme = new List<string>();
        var wohnungen = Objekt(inhalt["wohnungen"]);
        var moebel = new PySet(Objekte(wohnungen["moebel"]).Select(m => m["id"]));
        foreach (var m in Objekte(wohnungen["moebel"]))
        {
            var wo = Prozent("Spiel-Moebel %s", Get(m, "id"));
            if (!In(Einrichtung, Get(m, "typ")))
            {
                probleme.Add(Prozent("%s: unbekannter Typ '%s'", wo, Get(m, "typ")));
            }
            if (!(Py.F(Get(m, "w", 0L)) > 0 && Py.F(Get(m, "h", 0L)) > 0 && Py.F(Get(m, "preis", -1L)) >= 0))
            {
                probleme.Add(Prozent("%s: Groesse oder Preis fehlt", wo));
            }
            if (Get(m, "aktion") is not (null or "lernen" or "schlafen"))
            {
                probleme.Add(Prozent("%s: unbekannte Aktion '%s'", wo, Get(m, "aktion")));
            }
        }
        var start = Get(wohnungen, "start_moebel") as PyObjekt ?? [];
        foreach (var (_, kennung) in start)
        {
            if (!moebel.Contains(kennung))
            {
                probleme.Add(Prozent("Spiel-Wohnung: Start-Moebel '%s' unbekannt", kennung));
            }
        }
        var ids = Objekte(wohnungen["wohnungen"]).Select(w => w["id"]).ToList();
        if (!ids.Any(i => Py.Gleich(i, wohnungen["start"])) || Doppelt(ids))
        {
            probleme.Add("Spiel-Wohnung: Start-Wohnung fehlt oder Kennung doppelt");
        }
        foreach (var bleibe in Objekte(wohnungen["wohnungen"]))
        {
            var name = Prozent("Spiel-Wohnung %s", bleibe["id"]);
            var gebaeude = Objekt(bleibe["gebaeude"]);
            probleme.AddRange(Gebaeude(gebaeude, name, [], fachRaeume: false));
            var k = Liste(bleibe["kartons"]);
            var kiste = new PyObjekt { { "x", k[0] }, { "y", k[1] }, { "w", k[2] }, { "h", k[3] } };
            var flur = Objekt(gebaeude["flur"]);
            var bereiche = Objekte(gebaeude["raeume"]).Append(new PyObjekt { { "x", 0L }, { "y", flur["y"] }, { "w", gebaeude["breite"] }, { "h", flur["h"] } });
            if (!bereiche.Any(b => Innen(kiste, b)))
            {
                probleme.Add(Prozent("%s: Umzugskartons liegen ausserhalb", name));
            }
            var stand = new Spielstand([], inhalt) { Wohnung = bleibe["id"] };
            stand.Moebel = new PyDict();
            foreach (var (stueck, kennung) in start)
            {
                stand.Moebel[stueck] = kennung;
            }
            var leer = new PyObjekt { { "moebel", new PyObjekt() }, { "boeden", new PyObjekt() } };
            foreach (var (stueck, platz) in O(O(bleibe, "einrichtung"), "moebel"))
            {
                var p = Liste(platz);
                var problem = Wohnung.PlatzProblem(stand, stueck, stand.Moebel.Get(stueck), p[0], p[1], p[2], leer);
                if (problem.Length > 0)
                {
                    probleme.Add(Prozent("%s: Start-Einrichtung: %s", name, problem));
                }
            }
        }
        return probleme;
    }

    /// <summary>_validate_rent</summary>
    public static List<string> Miete(PyObjekt inhalt)
    {
        if (Get(Objekt(inhalt["balancing"]), "miete") is not PyObjekt regeln)
        {
            return ["Spiel-Balancing: Abschnitt 'miete' fehlt"];
        }
        var probleme = new List<string>();
        var anteil = Get(regeln, "kaution_anteil");
        if (anteil is not (long or double or bool) || !(0 <= Py.F(anteil) && Py.F(anteil) <= 1))
        {
            probleme.Add("Spiel-Balancing: kaution_anteil muss zwischen 0 und 1 liegen");
        }
        var proTag = O(regeln, "pro_tag");
        var wohnungen = Objekt(inhalt["wohnungen"]);
        var ids = Objekte(wohnungen["wohnungen"]).Select(w => w["id"]).ToList();
        foreach (var id in ids)
        {
            var wert = Hat(proTag, id) ? proTag[(string)id!] : null;
            if (wert is not (long or bool) || Py.F(wert) < 0)
            {
                probleme.Add(Prozent("Spiel-Balancing: keine gueltige Miete fuer Wohnung '%s'", id));
            }
        }
        if (Hat(proTag, wohnungen["start"]) && Py.Wahr(proTag[(string)wohnungen["start"]!]))
        {
            probleme.Add("Spiel-Balancing: die Startwohnung muss mietfrei sein");
        }
        foreach (var (k, _) in proTag)
        {
            if (!ids.Any(i => Py.Gleich(i, k)))
            {
                probleme.Add(Prozent("Spiel-Balancing: Miete fuer unbekannte Wohnung '%s'", k));
            }
        }
        return probleme;
    }

    // -- Gesamtpruefung ---------------------------------------------------

    /// <summary>validate_game_content (ohne Firma, Kredite, Geschaeft, Welt, Erfolge, Serverfarm; W3).</summary>
    public static List<string> Pruefen(PyObjekt inhalt, Lerninhalte lernen)
    {
        var probleme = new List<string>();
        var orte = new List<(object? Ort, List<object?> Raeume)> { (OrtBuero, Objekte(Objekt(inhalt["gebaeude"])["raeume"]).Select(r => r["id"]).ToList()) };
        var firma = Get(inhalt, "firma") as PyObjekt ?? [];
        foreach (var stufe in L(O(firma, "gebaeude"), "stufen").TakeLast(1))
        {
            orte.Add(("firma", Objekte(Objekt(Objekt(stufe)["gebaeude"])["raeume"]).Select(r => r["id"]).ToList()));
        }
        foreach (var stufe in L(O(firma, "filiale"), "stufen").TakeLast(1))
        {
            orte.Add((OrtFiliale, Objekte(Objekt(Objekt(stufe)["gebaeude"])["raeume"]).Select(r => r["id"]).ToList()));
        }
        foreach (var ort in Kundenorte(inhalt))
        {
            orte.Add((ort["id"], Objekte(Objekt(ort["gebaeude"])["raeume"]).Select(r => r["id"]).ToList()));
        }
        // dict: ein spaeterer gleicher Ort ersetzt den frueheren
        var ortRaeume = new Dictionary<string, PySet>();
        foreach (var (ort, raeume) in orte)
        {
            ortRaeume[Py.Str(ort)] = new PySet(raeume);
        }
        var alleRaeume = new PySet(ortRaeume.Values.SelectMany(r => r));
        var raumIds = ortRaeume.Values.SelectMany(r => r).ToList();
        if (Doppelt(raumIds))
        {
            probleme.Add("Spiel: Raum-Kennungen sind nicht eindeutig");
        }
        var allePersonen = AllePersonen(inhalt);
        var personen = new PySet(allePersonen.Select(p => p["id"]));
        if (personen.Count != allePersonen.Count)
        {
            probleme.Add("Spiel: Personen-Kennungen sind nicht eindeutig");
        }
        var balancing = Objekt(inhalt["balancing"]);

        probleme.AddRange(Gebaeude(Objekt(inhalt["gebaeude"]), "Spiel", Objekte(inhalt["kollegen"])));
        foreach (var ort in Kundenorte(inhalt))
        {
            probleme.AddRange(Gebaeude(Objekt(ort["gebaeude"]), Prozent("Spiel-Kunde %s", ort["id"]), Py.Hat(ort, "personen") ? Objekte(ort["personen"]) : []));
        }
        probleme.AddRange(Wohnungen(inhalt));
        probleme.AddRange(Miete(inhalt));

        var jeTag = balancing["tickets_pro_tag"];
        var gehalt = Objekt(balancing["gehalt_pro_tag"]);
        foreach (var rang in Objekte(balancing["raenge"]))
        {
            if (!Hat(gehalt, rang["name"]))
            {
                probleme.Add(Prozent("Spiel-Balancing: kein Gehalt fuer Rang '%s'", rang["name"]));
            }
            if (jeTag is PyObjekt t && !(Py.F(Hat(t, rang["name"]) ? t[(string)rang["name"]!] : 0L) >= 1))
            {
                probleme.Add(Prozent("Spiel-Balancing: keine Tickets pro Tag fuer Rang '%s'", rang["name"]));
            }
        }
        var tage = Objekte(balancing["raenge"]).Select(r => Py.F(Get(r, "ab_tag", 1L))).ToList();
        if (!tage.SequenceEqual(tage.Order()) || tage[0] != 1)
        {
            probleme.Add("Spiel-Balancing: ab_tag der Raenge muss bei 1 beginnen und steigen");
        }

        probleme.AddRange(Hardware(inhalt));
        probleme.AddRange(RackHardware(inhalt));
        probleme.AddRange(Falschlieferung(inhalt));

        var gesehen = new PySet();
        var tickets = new PyDict();
        var nummer = 0;
        foreach (var auftrag in Objekte(inhalt["aufgaben"]).Concat(Py.Hat(inhalt, "zwischenfaelle") ? Objekte(inhalt["zwischenfaelle"]) : []))
        {
            nummer++;
            var wo = Prozent("Spiel-Aufgabe Nr. %d (%s)", (long)nummer, Get(auftrag, "id"));
            foreach (var feld in new[] { "id", "typ", "titel", "auftraggeber", "raum", "prioritaet", "cat", "ticket", "frage", "hilfe", "erklaerung", "suchbegriffe", "belohnung" })
            {
                if (!Py.Wahr(Get(auftrag, feld)))
                {
                    probleme.Add(Prozent("%s: Feld '%s' fehlt oder ist leer", wo, feld));
                }
            }
            if (gesehen.Contains(Get(auftrag, "id")))
            {
                probleme.Add(Prozent("%s: Kennung doppelt vorhanden", wo));
            }
            if (SpielInhalt.PlatzhalterUebrig(auftrag))
            {
                probleme.Add(Prozent("%s: Platzhalter {{...}} ohne Wert", wo));
            }
            if (tickets.ContainsKey(Get(auftrag, "ticket")))
            {
                probleme.Add(Prozent("%s: gleicher Tickettext wie '%s'", wo, tickets[Get(auftrag, "ticket")]));
            }
            tickets[Get(auftrag, "ticket")] = Get(auftrag, "id");
            gesehen.Add(Get(auftrag, "id"));
            var typ = Get(auftrag, "typ");
            if (!In(Aufgabentypen, typ))
            {
                probleme.Add(Prozent("%s: unbekannter Typ '%s'", wo, typ));
            }
            if (!alleRaeume.Contains(Get(auftrag, "raum")))
            {
                probleme.Add(Prozent("%s: unbekannter Raum '%s'", wo, Get(auftrag, "raum")));
            }
            if (!personen.Contains(Get(auftrag, "auftraggeber")))
            {
                probleme.Add(Prozent("%s: unbekannter Auftraggeber '%s'", wo, Get(auftrag, "auftraggeber")));
            }
            else
            {
                probleme.AddRange(OrtProbleme(auftrag, wo, ortRaeume, inhalt));
            }
            if (!In(Prioritaeten, Get(auftrag, "prioritaet")))
            {
                probleme.Add(Prozent("%s: unbekannte Prioritaet", wo));
            }
            if (!(Get(auftrag, "cat") is string cat && Fachbereiche.Schluessel.ContainsKey(cat)))
            {
                probleme.Add(Prozent("%s: unbekannter Fachbereich '%s'", wo, Get(auftrag, "cat")));
            }
            foreach (var (thema, wert) in O(auftrag, "anforderungen"))
            {
                if (!lernen.ThemaNach.ContainsKey(thema) || !(0 <= Py.F(wert) && Py.F(wert) <= 100))
                {
                    probleme.Add(Prozent("%s: ungueltige Anforderung %s: %s (erwartet wird ein Thema aus inhalte/themen.json)", wo, thema, wert));
                }
            }
            foreach (var achse in L(auftrag, "achsen"))
            {
                if (!In(Ereignisse.Achsen, achse))
                {
                    probleme.Add(Prozent("%s: unbekannte Reputations-Achse '%s'", wo, achse));
                }
            }
            if (Laenge(Get(auftrag, "ticket", "")) > MaxTicket)
            {
                probleme.Add(Prozent("%s: Tickettext laenger als %d Zeichen (Handy)", wo, (long)MaxTicket));
            }

            if (Py.Wahr(Get(auftrag, "nach")) && Auftrag(auftrag["nach"], inhalt) is null)
            {
                probleme.Add(Prozent("%s: Voraussetzung '%s' gibt es nicht", wo, auftrag["nach"]));
            }
            if (Py.Wahr(Get(auftrag, "empfaenger")) && !personen.Contains(auftrag["empfaenger"]))
            {
                probleme.Add(Prozent("%s: unbekannter Empfaenger '%s'", wo, auftrag["empfaenger"]));
            }
            var eigene = typ switch
            {
                "bauteile" or "bestellung" => HardwareAuftrag(auftrag, inhalt),
                "rack" => RackAuftrag(auftrag, inhalt),
                "formular" => FormAuftrag(auftrag, inhalt),
                "terminal" => TerminalAuftrag(auftrag, inhalt),
                "diagnose" => DiagnoseAuftrag(auftrag, inhalt),
                "wartung" => WartungAuftrag(auftrag, inhalt),
                _ => [],
            };
            probleme.AddRange(eigene.Select(t => wo + ": " + t));

            List<object?> texte;
            if (Py.Gleich(typ, "auswahl"))
            {
                var optionen = L(auftrag, "optionen");
                if (optionen.Count < 2 || Doppelt(optionen))
                {
                    probleme.Add(Prozent("%s: braucht mindestens 2 verschiedene Antworten", wo));
                }
                if (!optionen.Any(o => Py.Gleich(o, Get(auftrag, "antwort"))))
                {
                    probleme.Add(Prozent("%s: richtige Antwort steht nicht in den Optionen", wo));
                }
                texte = optionen;
            }
            else if (Py.Gleich(typ, "zuordnung"))
            {
                var paare = L(auftrag, "paare").Select(Liste).ToList();
                var links = paare.Select(p => p[0]).ToList();
                var rechts = paare.Select(p => p[1]).ToList();
                if (paare.Count < 2 || Doppelt(links) || Doppelt(rechts))
                {
                    probleme.Add(Prozent("%s: braucht mindestens 2 eindeutige Paare", wo));
                }
                texte = [.. links, .. rechts];
            }
            else
            {
                texte = [];
            }
            foreach (var text in texte)
            {
                if (Laenge(text) > MaxOption)
                {
                    probleme.Add(Prozent("%s: Antwort laenger als %d Zeichen (Handy): %s", wo, (long)MaxOption, Anfang(text)));
                }
            }
            foreach (var begriff in L(auftrag, "suchbegriffe"))
            {
                if (!FindetLernstoff(Py.Str(begriff), lernen))
                {
                    probleme.Add(Prozent("%s: Suchbegriff '%s' findet keine Karteikarte oder Quizfrage", wo, begriff));
                }
            }
        }
        return probleme;
    }

    /// <summary>Auftraggeber da, Ort und Raum passen, die Stelle ist begehbar (Teil von validate_game_content).</summary>
    private static List<string> OrtProbleme(PyObjekt auftrag, string wo, Dictionary<string, PySet> ortRaeume, PyObjekt inhalt)
    {
        var probleme = new List<string>();
        var ort = AuftragOrt(auftrag, inhalt);
        var person = Person(auftrag["auftraggeber"], inhalt)!;
        var start = Get(auftrag, "ab_tag", 1L);
        if (Py.Kleiner(start, Get(person, "ab_tag", 1L)))
        {
            probleme.Add(Prozent("%s: %s ist erst ab Tag %d da", wo, person["id"], person["ab_tag"]));
        }
        if (person["bis_tag"] is not null && Py.Kleiner(person["bis_tag"], start) && !Py.Wahr(Get(person, "nachfolger")))
        {
            probleme.Add(Prozent("%s: %s ist nur bis Tag %d da", wo, person["id"], person["bis_tag"]));
        }
        var kundenort = Kundenorte(inhalt).FirstOrDefault(o => Py.Gleich(o["id"], ort));
        if (kundenort is not null && Py.Kleiner(start, Get(kundenort, "ab_tag", 1L)))
        {
            probleme.Add(Prozent("%s: Ort '%s' gibt es erst ab Tag %d", wo, ort, kundenort["ab_tag"]));
        }
        if (ort is not string o2 || !ortRaeume.TryGetValue(o2, out var raeume))
        {
            probleme.Add(Prozent("%s: unbekannter Ort '%s'", wo, ort));
        }
        else if (!raeume.Contains(Get(auftrag, "raum")))
        {
            probleme.Add(Prozent("%s: Raum '%s' liegt nicht am Ort '%s'", wo, Get(auftrag, "raum"), ort));
        }
        else if (Py.Wahr(Get(auftrag, "stelle")))
        {
            var gebaeude = OrtGebaeude(o2, inhalt);
            var stelle = Liste(auftrag["stelle"]);
            var (x, y) = (Py.F(stelle[0]), Py.F(stelle[1]));
            var raum = Wege.RaumBei(x, y, gebaeude);
            if (raum is null || !Py.Gleich(raum["id"], auftrag["raum"]))
            {
                probleme.Add(Prozent("%s: Stelle liegt nicht im Raum '%s'", wo, auftrag["raum"]));
            }
            else if (!Wege.BasisRaster(gebaeude).Contains(Wege.Zelle(x, y)))
            {
                probleme.Add(Prozent("%s: an der Stelle kann niemand stehen", wo));
            }
        }
        return probleme;
    }

    /// <summary>site_content(ort, None)["gebaeude"]: Grundriss mit der Einrichtung, die ohne erledigte Auftraege zu sehen ist.</summary>
    private static PyObjekt OrtGebaeude(string ort, PyObjekt inhalt)
    {
        PyObjekt basis;
        if (ort == OrtBuero)
        {
            basis = Objekt(inhalt["gebaeude"]);
        }
        else if (ort == OrtFiliale)
        {
            throw new InvalidOperationException("Die Filiale ist noch nicht eröffnet.");
        }
        else
        {
            var kundenort = Kundenorte(inhalt).FirstOrDefault(o => Py.Gleich(o["id"], ort)) ?? throw new InvalidOperationException("Unbekannter Ort: " + ort);
            basis = Objekt(kundenort["gebaeude"]);
        }
        static bool Sichtbar(PyObjekt deko) => !Py.Wahr(Get(deko, "ab_aufgabe"));   // bis_aufgabe: nichts ist erledigt
        var ergebnis = (PyObjekt)Py.Kopie(basis)!;
        foreach (var raum in Objekte(ergebnis["raeume"]))
        {
            Py.Setzen(raum, "deko", (Py.Hat(raum, "deko") ? Objekte(raum["deko"]) : []).Where(Sichtbar).Cast<object?>().ToList());
        }
        if (Get(ergebnis, "flur") is PyObjekt flur)
        {
            Py.Setzen(flur, "deko", (Py.Hat(flur, "deko") ? Objekte(flur["deko"]) : []).Where(Sichtbar).Cast<object?>().ToList());
        }
        return ergebnis;
    }

    // -- Hardware ---------------------------------------------------------

    /// <summary>_validate_wrong_delivery</summary>
    public static List<string> Falschlieferung(PyObjekt inhalt)
    {
        if (Get(Hw(inhalt), "falschlieferung") is not PyObjekt vorlage || vorlage.Count == 0)
        {
            return ["hardware.json: Vorlage 'falschlieferung' fehlt"];
        }
        var probleme = new List<string>();
        foreach (var feld in new[] { "titel", "auftraggeber", "raum", "prioritaet", "cat", "ticket", "frage", "optionen", "antwort", "hilfe", "erklaerung", "belohnung" })
        {
            if (!Py.Wahr(Get(vorlage, feld)))
            {
                probleme.Add(Prozent("falschlieferung: Feld '%s' fehlt", feld));
            }
        }
        if (!L(vorlage, "optionen").Any(o => Py.Gleich(o, Get(vorlage, "antwort"))))
        {
            probleme.Add("falschlieferung: Antwort steht nicht in den Optionen");
        }
        if (Person(Get(vorlage, "auftraggeber"), inhalt) is null)
        {
            probleme.Add("falschlieferung: unbekannter Auftraggeber");
        }
        if (!Objekte(Objekt(inhalt["gebaeude"])["raeume"]).Any(r => Py.Gleich(r["id"], Get(vorlage, "raum"))))
        {
            probleme.Add("falschlieferung: unbekannter Raum");
        }
        string[] namen = ["bestellung", "ware"];
        if (!FormatGeht(Py.Str(SpielInhalt.Zeilen(Get(vorlage, "ticket"))), namen) || !FormatGeht(Py.Str(Get(vorlage, "titel", "")), namen))
        {
            probleme.Add("falschlieferung: nur {bestellung} und {ware} als Platzhalter");
        }
        return probleme;
    }

    /// <summary>_validate_hardware</summary>
    public static List<string> Hardware(PyObjekt inhalt)
    {
        var probleme = new List<string>();
        var gesehen = new PySet();
        var hw = Hw(inhalt);
        var typen = Objekt(hw["typen"]);
        foreach (var teil in Objekte(hw["teile"]))
        {
            var wo = Prozent("Spiel-Bauteil %s", Get(teil, "id"));
            if (gesehen.Contains(Get(teil, "id")))
            {
                probleme.Add(Prozent("%s: Kennung doppelt vorhanden", wo));
            }
            gesehen.Add(Get(teil, "id"));
            if (Get(teil, "typ") is not string typ || !TeilFelder.ContainsKey(typ) || !Py.Hat(typen, typ))
            {
                probleme.Add(Prozent("%s: unbekannter Typ '%s'", wo, Get(teil, "typ")));
                continue;
            }
            if (!Py.Wahr(Get(teil, "name")) || !(Py.F(Get(teil, "preis", 0L)) > 0))
            {
                probleme.Add(Prozent("%s: Name oder Preis fehlt", wo));
            }
            foreach (var feld in TeilFelder[typ])
            {
                if (!Py.Hat(teil, feld))
                {
                    probleme.Add(Prozent("%s: Feld '%s' fehlt", wo, feld));
                }
            }
            var formen = new List<object?> { Get(teil, "formfaktor") };
            formen.AddRange(L(teil, "formfaktoren"));
            foreach (var form in formen)
            {
                if (form is not null && !Py.Enthaelt(hw["formfaktoren"], form))
                {
                    probleme.Add(Prozent("%s: unbekannter Formfaktor '%s'", wo, form));
                }
            }
        }
        var haendler = Objekte(hw["haendler"]).Select(h => h["id"]).ToList();
        if (Doppelt(haendler))
        {
            probleme.Add("Spiel-Haendler: Kennung doppelt vorhanden");
        }
        var arten = O(hw, "ersatz_typen");
        foreach (var teil in L(hw, "ersatzteile").Select(Objekt))
        {
            var wo = Prozent("Spiel-Ersatzteil %s", Get(teil, "id"));
            if (gesehen.Contains(Get(teil, "id")))
            {
                probleme.Add(Prozent("%s: Kennung doppelt vorhanden", wo));
            }
            gesehen.Add(Get(teil, "id"));
            if (!Hat(arten, Get(teil, "typ")))
            {
                probleme.Add(Prozent("%s: unbekannter Typ '%s'", wo, Get(teil, "typ")));
                continue;
            }
            if (!Py.Wahr(Get(teil, "name")) || !(Py.F(Get(teil, "preis", 0L)) > 0))
            {
                probleme.Add(Prozent("%s: Name oder Preis fehlt", wo));
            }
            foreach (var (feld, _, _) in Bewertung.ErsatzMerkmale(teil["typ"], inhalt))
            {
                if (!Hat(teil, feld))
                {
                    probleme.Add(Prozent("%s: Merkmal '%s' fehlt", wo, feld));
                }
            }
        }
        foreach (var (art, _) in arten)
        {
            if (Bewertung.ErsatzMerkmale(art, inhalt).Count == 0)
            {
                probleme.Add(Prozent("Spiel-Ersatzteile: keine Merkmale fuer Typ '%s'", art));
            }
        }
        foreach (var (teil, anzahl) in Get(hw, "grundbestand") as PyObjekt ?? [])
        {
            if (!gesehen.Contains(teil) || !(Py.Int(anzahl) >= 1))
            {
                probleme.Add(Prozent("Spiel-Grundbestand: unbekanntes Teil '%s'", teil));
            }
        }
        if (Py.Wahr(Get(hw, "ersatzteile")))
        {
            var nach = Get(hw, "nachbestellung") as PyObjekt ?? [];
            if (!haendler.Any(h => Py.Gleich(h, Get(nach, "haendler"))) || !(Py.F(Get(nach, "lieferzeit", 0L)) >= 1))
            {
                probleme.Add("Spiel-Nachbestellung: Haendler oder Lieferzeit fehlt");
            }
        }
        return probleme;
    }

    /// <summary>_validate_rack_hardware</summary>
    public static List<string> RackHardware(PyObjekt inhalt)
    {
        var probleme = new List<string>();
        var gesehen = new PySet();
        var hw = Hw(inhalt);
        var arten = O(hw, "rack_typen");
        foreach (var geraet in L(hw, "rack_geraete").Select(Objekt))
        {
            var wo = Prozent("Spiel-Rack-Geraet %s", Get(geraet, "id"));
            if (gesehen.Contains(Get(geraet, "id")))
            {
                probleme.Add(Prozent("%s: Kennung doppelt vorhanden", wo));
            }
            gesehen.Add(Get(geraet, "id"));
            foreach (var feld in RackFelder)
            {
                if (!Py.Hat(geraet, feld))
                {
                    probleme.Add(Prozent("%s: Feld '%s' fehlt", wo, feld));
                }
            }
            if (!Hat(arten, Get(geraet, "typ")))
            {
                probleme.Add(Prozent("%s: unbekannter Typ '%s'", wo, Get(geraet, "typ")));
            }
            if (Py.Gleich(Get(geraet, "typ"), "usv") && !(Py.F(Get(geraet, "leistung", 0L)) > 0))
            {
                probleme.Add(Prozent("%s: USV ohne Leistung", wo));
            }
            if (Get(geraet, "typ") is "switch" or "patchpanel" && !(Py.F(Get(geraet, "ports", 0L)) > 0))
            {
                probleme.Add(Prozent("%s: Ports fehlen", wo));
            }
        }
        if (Py.Wahr(Get(hw, "rack_geraete")) && !Py.Wahr(Get(hw, "rack_regeln")))
        {
            probleme.Add("Spiel-Hardware: 'rack_regeln' fehlen");
        }
        return probleme;
    }

    private static PyObjekt? Haendler(object? kennung, PyObjekt inhalt) =>
        Objekte(Hw(inhalt)["haendler"]).FirstOrDefault(h => Py.Gleich(h["id"], kennung));

    /// <summary>_stock_users_problems: holen mehr Auftraege Ware aus einer Bestellung, als sie liefert?</summary>
    private static List<string> LagerNutzer(PyObjekt erste, PyObjekt inhalt)
    {
        var nutzer = Objekte(inhalt["aufgaben"]).Where(a => Py.Gleich(Get(a, "nach"), erste["id"]) && Py.Wahr(Get(a, "aus_lager"))).ToList();
        var arten = new PySet(nutzer.SelectMany(a => Liste(a["aus_lager"])));
        var menge = Py.Summe(Objekte(erste["bedarf"]).Where(b => arten.Contains(b["typ"])).Select(b => b["menge"]));
        return Py.Kleiner(menge, (long)nutzer.Count) ? [Prozent("mehr Auftraege holen Ware aus '%s', als bestellt wird", erste["id"])] : [];
    }

    /// <summary>_validate_hardware_task: Aufbau pruefen und durch Ausprobieren beweisen, dass es eine Loesung gibt.</summary>
    public static List<string> HardwareAuftrag(PyObjekt auftrag, PyObjekt inhalt)
    {
        var probleme = new List<string>();
        var hw = Hw(inhalt);
        if (Py.Gleich(auftrag["typ"], "bestellung"))
        {
            var angebote = L(auftrag, "angebote").Select(Objekt).ToList();
            var bedarf = L(auftrag, "bedarf").Select(Objekt).ToList();
            if (angebote.Count == 0 || bedarf.Count == 0)
            {
                return ["Angebote oder Bedarf fehlen"];
            }
            if (!(Py.F(Get(auftrag, "budget", 0L)) > 0) || !(Py.F(Get(auftrag, "frist", 0L)) >= 1))
            {
                probleme.Add("Budget oder Frist fehlt");
            }
            if (Doppelt(angebote.Select(a => Get(a, "id")).ToList()))
            {
                probleme.Add("Angebots-Kennung doppelt vorhanden");
            }
            foreach (var b in bedarf)
            {
                var typ = Get(b, "typ");
                if (!Hat(hw["typen"], typ) && !Hat(O(hw, "rack_typen"), typ) && !Hat(O(hw, "ersatz_typen"), typ)
                    || !Py.Wahr(Get(b, "text")) || !(Py.F(Get(b, "menge", 0L)) >= 1))
                {
                    probleme.Add(Prozent("Bedarf unvollstaendig: %s", Get(b, "text")));
                }
                foreach (var anderes in L(b, "fuer"))
                {
                    if (Teil(anderes, inhalt) is null)
                    {
                        probleme.Add(Prozent("Bedarf verweist auf unbekanntes Teil '%s'", anderes));
                    }
                }
            }
            foreach (var a in angebote)
            {
                var teil = Teil(Get(a, "teil"), inhalt);
                if (teil is null)
                {
                    probleme.Add(Prozent("Angebot %s: unbekanntes Teil", Get(a, "id")));
                }
                else if (Haendler(Get(a, "haendler"), inhalt) is null)
                {
                    probleme.Add(Prozent("Angebot %s: unbekannter Haendler", Get(a, "id")));
                }
                else if (!(Py.F(Get(a, "preis", 0L)) > 0) || !(Py.F(Get(a, "lieferzeit", 0L)) >= 1))
                {
                    probleme.Add(Prozent("Angebot %s: Preis oder Lieferzeit fehlt", Get(a, "id")));
                }
                else if (bedarf.Count(b => Bewertung.BedarfPasstNicht(teil, b, inhalt).Length == 0) > 1)
                {
                    probleme.Add(Prozent("Angebot %s passt zu mehreren Bedarfen", a["id"]));
                }
            }
            if (probleme.Count == 0 && Bewertung.Warenkoerbe(auftrag, inhalt).Count == 0)
            {
                probleme.Add("keine gueltige Bestellung moeglich");
            }
            return probleme;
        }

        var plaetze = L(auftrag, "slots");
        if (plaetze.Count == 0 || plaetze.Any(p => !In(Steckplaetze, p)))
        {
            return ["Steckplaetze fehlen oder sind unbekannt"];
        }
        foreach (var kennung in L(auftrag, "teile"))
        {
            if (Teil(kennung, inhalt) is null)
            {
                probleme.Add(Prozent("unbekanntes Bauteil '%s'", kennung));
            }
        }
        foreach (var platz in L(auftrag, "optional").Concat(L(auftrag, "aus_lager")))
        {
            if (!plaetze.Any(p => Py.Gleich(p, platz)))
            {
                probleme.Add(Prozent("'%s' ist kein Steckplatz dieses Auftrags", platz));
            }
        }
        foreach (var (k, _) in O(auftrag, "vorgaben"))
        {
            if (!BauVorgaben.Contains(k))
            {
                probleme.Add(Prozent("unbekannte Vorgabe '%s'", k));
            }
        }
        if (probleme.Count > 0)
        {
            return probleme;
        }
        var extras = new List<List<object?>> { new() };
        if (Py.Wahr(Get(auftrag, "aus_lager")))
        {
            var erste = Auftrag(Get(auftrag, "nach"), inhalt);
            if (erste is null || !Py.Gleich(erste["typ"], "bestellung"))
            {
                return ["Teile aus dem Lager brauchen eine Bestellung als Voraussetzung"];
            }
            extras = [];
            var koerbe = Bewertung.Warenkoerbe(erste, inhalt);
            if (koerbe.Count == 0)
            {
                return [Prozent("die Bestellung davor ('%s') hat keine gueltige Loesung", erste["id"])];
            }
            var ausLager = Liste(auftrag["aus_lager"]);
            foreach (var korb in koerbe)
            {
                var lager = new PyDict();
                foreach (var a in Objekte(erste["angebote"]))
                {
                    if (Py.Wahr(Get(korb, Py.Str(a["id"]))) && ausLager.Any(t => Py.Gleich(t, Teil(a["teil"], inhalt)!["typ"])))
                    {
                        lager[a["teil"]] = Py.Plus(lager.Get(a["teil"], 0L), korb[Py.Str(a["id"])]);
                    }
                }
                var extra = lager.Keys.ToList();
                extra.AddRange(LagerKombis(lager, ausLager, inhalt));
                extras.Add(extra);
            }
            probleme.AddRange(LagerNutzer(erste, inhalt));
        }
        var teile = L(auftrag, "teile");
        var vorhanden = Py.Hat(auftrag, "teile") ? Liste(auftrag["teile"]) : [];
        foreach (var extra in extras)
        {
            var verfuegbar = teile.Concat(extra.Where(p => !vorhanden.Any(t => Py.Gleich(t, p)))).ToList();
            if (Bewertung.Loesung(auftrag, verfuegbar, inhalt) is null)
            {
                probleme.Add("PC laesst sich mit den bereitliegenden Teilen nicht bauen");
                break;
            }
        }
        return probleme;
    }

    /// <summary>_validate_rack_task</summary>
    public static List<string> RackAuftrag(PyObjekt auftrag, PyObjekt inhalt)
    {
        var probleme = new List<string>();
        var schrank = Get(auftrag, "schrank") as PyObjekt ?? [];
        if (!(Py.F(Get(schrank, "he", 0L)) >= 3) || !(Py.F(Get(schrank, "traglast", 0L)) > 0))
        {
            probleme.Add("Schrank braucht HE (mind. 3) und Traglast");
        }
        var geraete = L(auftrag, "geraete");
        if (geraete.Count == 0)
        {
            probleme.Add("keine Geraete");
        }
        foreach (var g in geraete)
        {
            if (RackGeraet(g, inhalt) is null)
            {
                probleme.Add(Prozent("unbekanntes Rack-Geraet '%s'", g));
            }
        }
        var vorgaben = O(auftrag, "vorgaben");
        foreach (var (k, _) in vorgaben)
        {
            if (!RackVorgaben.Contains(k))
            {
                probleme.Add(Prozent("unbekannte Vorgabe '%s'", k));
            }
        }
        foreach (var (art, _) in O(vorgaben, "mindestens"))
        {
            if (!Py.Hat(O(Hw(inhalt), "rack_typen"), art))
            {
                probleme.Add(Prozent("unbekannter Geraetetyp '%s'", art));
            }
        }
        if (probleme.Count > 0)
        {
            return probleme;
        }
        if (Py.Wahr(Get(auftrag, "aus_lager")))
        {
            var erste = Auftrag(Get(auftrag, "nach"), inhalt);
            if (erste is null || !Py.Gleich(erste["typ"], "bestellung"))
            {
                return ["Geraete aus dem Lager brauchen eine Bestellung als Voraussetzung"];
            }
            var koerbe = Bewertung.Warenkoerbe(erste, inhalt);
            if (koerbe.Count == 0)
            {
                return [Prozent("die Bestellung '%s' hat keine richtige Loesung", erste["id"])];
            }
            var ausLager = Liste(auftrag["aus_lager"]);
            foreach (var korb in koerbe)
            {
                var liste = Liste(auftrag["geraete"]).ToList();
                foreach (var a in Objekte(erste["angebote"]))
                {
                    var g = RackGeraet(a["teil"], inhalt);
                    if (g is not null && ausLager.Any(t => Py.Gleich(t, g["typ"])))
                    {
                        var anzahl = Py.Wahr(Get(korb, Py.Str(a["id"]))) ? Py.Int(korb[Py.Str(a["id"])]) : 0;
                        for (var i = 0; i < anzahl; i++)
                        {
                            liste.Add(a["teil"]);
                        }
                    }
                }
                var probe = Py.Flach(auftrag);
                Py.Setzen(probe, "geraete", liste);
                Py.Setzen(probe, "lager_ab", (long)Liste(auftrag["geraete"]).Count);
                if (Bewertung.RackLoesung(probe, inhalt) is null)
                {
                    probleme.Add(Prozent("Schrank laesst sich mit der Lieferung %s nicht richtig bestuecken",
                        string.Join(", ", liste.Select(Py.Str).OrderBy(s => s, StringComparer.Ordinal))));
                    break;
                }
            }
            return probleme;
        }
        if (Bewertung.RackLoesung(auftrag, inhalt) is null)
        {
            probleme.Add("Schrank laesst sich mit den bereitliegenden Geraeten nicht richtig bestuecken");
        }
        return probleme;
    }

    /// <summary>_validate_form_task</summary>
    public static List<string> FormAuftrag(PyObjekt auftrag, PyObjekt inhalt)
    {
        if (!In(FormArten, Get(auftrag, "art")))
        {
            return [Prozent("unbekannte Formular-Art '%s'", Get(auftrag, "art"))];
        }
        if (!Py.Wahr(Get(auftrag, "daten")))
        {
            return ["Ausgangsdaten fehlen"];
        }
        List<Bewertung.FormFeld> felder;
        try
        {
            felder = Bewertung.FormFelder(auftrag);
            SpielTexte.FormAusgang(auftrag);
        }
        catch (Exception fehler) when (fehler is Bewertung.FormFehler or KeyNotFoundException or InvalidCastException or FormatException
                                           or InvalidDataException or DivideByZeroException or ArgumentException or OverflowException)
        {
            return ["Formular laesst sich nicht berechnen: " + fehler.Message];
        }
        if (felder.Count == 0)
        {
            return ["keine Eingabefelder"];
        }
        if (Bewertung.FormProbleme(auftrag, Bewertung.Loesung(auftrag, null, inhalt) as PyObjekt).Count > 0)
        {
            return ["die eigene Loesung wird nicht als richtig erkannt"];
        }
        return [];
    }

    /// <summary>_validate_exchange</summary>
    public static List<string> Austausch(PyObjekt auftrag, PyObjekt inhalt)
    {
        var austausch = Objekt(auftrag["austausch"]);
        var probleme = new List<string>();
        if (Py.Wahr(Get(auftrag, "zwischenfall")))
        {
            return ["Zwischenfaelle warten nicht - kein Austausch mit Nachbestellung"];
        }
        var hw = Hw(inhalt);
        if (!Hat(O(hw, "typen"), Get(austausch, "typ")) && !Hat(O(hw, "ersatz_typen"), Get(austausch, "typ")))
        {
            return [Prozent("Austausch: unbekannter Teile-Typ '%s'", Get(austausch, "typ"))];
        }
        var katalog = Objekte(hw["teile"]).Concat(L(hw, "ersatzteile").Select(Objekt)).Where(t => Py.Gleich(t["typ"], Get(austausch, "typ"))).ToList();
        var passend = katalog.Where(t => Bewertung.ErsatzPasst(auftrag, t["id"], inhalt).Count == 0).ToList();
        if (passend.Count == 0)
        {
            probleme.Add("Austausch: kein passendes Ersatzteil im Katalog");
        }
        if (passend.Count == katalog.Count)
        {
            probleme.Add("Austausch: jedes Teil passt - die Wahl waere egal");
        }
        var merkmale = Bewertung.ErsatzMerkmale(austausch["typ"], inhalt).Select(m => m.Feld).ToList();
        foreach (var (k, _) in O(austausch, "passt"))
        {
            if (!merkmale.Any(m => Py.Gleich(m, k)))
            {
                probleme.Add(Prozent("Austausch: Merkmal '%s' hat keine Bezeichnung", k));
            }
        }
        var schritte = L(austausch, "schritte");
        var falsch = L(austausch, "falsch");
        if (!(AustauschMin <= schritte.Count && schritte.Count <= AustauschMax))
        {
            probleme.Add(Prozent("Austausch: braucht %d bis %d Schritte", (long)AustauschMin, (long)AustauschMax));
        }
        var alle = schritte.Concat(falsch).ToList();
        if (Doppelt(alle))
        {
            probleme.Add("Austausch: Schritte doppelt");
        }
        foreach (var text in alle)
        {
            if (Laenge(text) > MaxOption)
            {
                probleme.Add(Prozent("Austausch: Schritt zu lang (Handy): %s", Anfang(text)));
            }
        }
        if (!Py.Wahr(Get(austausch, "test")))
        {
            probleme.Add("Austausch: Ergebnis des Funktionstests fehlt");
        }
        else if (Laenge(austausch["test"]) > MaxErgebnis)
        {
            probleme.Add("Austausch: Funktionstest-Text zu lang (Handy)");
        }
        return probleme;
    }

    /// <summary>_validate_maintenance_task</summary>
    public static List<string> WartungAuftrag(PyObjekt auftrag, PyObjekt inhalt)
    {
        var probleme = new List<string>();
        var punkte = L(auftrag, "pruefpunkte").Select(Objekt).ToList();
        var ids = punkte.Select(p => Get(p, "id")).ToList();
        if (punkte.Count < 3 || Doppelt(ids) || !ids.All(Py.Wahr))
        {
            return ["braucht mindestens 3 Pruefpunkte mit eindeutiger Kennung"];
        }
        foreach (var p in punkte)
        {
            if (!Py.Wahr(Get(p, "text")) || !Py.Wahr(Get(p, "ergebnis")))
            {
                probleme.Add(Prozent("Pruefpunkt '%s': Text oder Ergebnis fehlt", p["id"]));
            }
            if (Get(p, "auffaellig", false) is not bool)
            {
                probleme.Add(Prozent("Pruefpunkt '%s': auffaellig muss true/false sein", p["id"]));
            }
            if (Laenge(Get(p, "text", "")) > MaxOption || Laenge(Get(p, "ergebnis", "")) > MaxErgebnis)
            {
                probleme.Add(Prozent("Pruefpunkt '%s': Text zu lang (Handy)", p["id"]));
            }
        }
        var optionen = L(auftrag, "abschluss");
        if (optionen.Count < 3 || Doppelt(optionen))
        {
            probleme.Add("Abschluss: braucht mindestens 3 verschiedene Moeglichkeiten");
        }
        if (!optionen.Any(o => Py.Gleich(o, Get(auftrag, "abschluss_antwort"))))
        {
            probleme.Add("Abschluss: richtige Antwort steht nicht in der Auswahl");
        }
        foreach (var text in optionen)
        {
            if (Laenge(text) > MaxOption)
            {
                probleme.Add(Prozent("Abschluss zu lang (Handy): %s", Anfang(text)));
            }
        }
        if (probleme.Count == 0 && !Bewertung.WartungPruefen(auftrag, Bewertung.WartungLoesung(auftrag)).Richtig)
        {
            probleme.Add("die eigene Loesung wird nicht als richtig erkannt");
        }
        return probleme;
    }

    /// <summary>_validate_terminal_task</summary>
    public static List<string> TerminalAuftrag(PyObjekt auftrag, PyObjekt inhalt)
    {
        var probleme = new List<string>();
        if (!In(Systeme, Get(auftrag, "system")))
        {
            probleme.Add(Prozent("unbekanntes System '%s'", Get(auftrag, "system")));
        }
        if (!Py.Wahr(Get(auftrag, "prompt")))
        {
            probleme.Add("Eingabezeile (prompt) fehlt");
        }
        var schritte = L(auftrag, "schritte").Select(Objekt).ToList();
        if (schritte.Count < 2)
        {
            probleme.Add("braucht mindestens 2 Schritte");
            return probleme;
        }
        var zeilen = L(auftrag, "start").ToList();
        var nummer = 0;
        foreach (var schritt in schritte)
        {
            nummer++;
            var wo = Prozent("Schritt %d", (long)nummer);
            var befehle = L(schritt, "befehle").Select(Objekt).ToList();
            if (!Py.Wahr(Get(schritt, "ziel")))
            {
                probleme.Add(Prozent("%s: Ziel fehlt", wo));
            }
            if (!(2 <= befehle.Count && befehle.Count <= 4))
            {
                probleme.Add(Prozent("%s: braucht 2 bis 4 Befehle", wo));
                continue;
            }
            var texte = befehle.Select(b => Get(b, "befehl")).ToList();
            if (!texte.All(Py.Wahr) || Doppelt(texte))
            {
                probleme.Add(Prozent("%s: Befehle fehlen oder sind doppelt", wo));
            }
            if (befehle.Count(b => Py.Wahr(Get(b, "richtig"))) != 1)
            {
                probleme.Add(Prozent("%s: braucht genau einen richtigen Befehl", wo));
            }
            foreach (var b in befehle)
            {
                if (Py.Wahr(Get(b, "richtig")) && Py.Wahr(Get(b, "gefaehrlich")))
                {
                    probleme.Add(Prozent("%s: richtiger Befehl ist als gefaehrlich markiert", wo));
                }
                if (!Py.Wahr(Get(b, "richtig")) && !(Py.Wahr(Get(b, "ausgabe")) && Py.Wahr(Get(b, "hinweis"))))
                {
                    probleme.Add(Prozent("%s: falscher Befehl '%s' braucht Ausgabe und Hinweis", wo, Get(b, "befehl")));
                }
                if (Laenge(Py.Wahr(Get(b, "befehl")) ? b["befehl"] : "") > MaxBefehl)
                {
                    probleme.Add(Prozent("%s: Befehl laenger als %d Zeichen", wo, (long)MaxBefehl));
                }
                zeilen.AddRange(L(b, "ausgabe"));
                if (Laenge(Get(b, "hinweis", "")) > MaxErgebnis)
                {
                    probleme.Add(Prozent("%s: Hinweis zu lang (Handy)", wo));
                }
            }
        }
        foreach (var zeile in zeilen)
        {
            if (Laenge(zeile) > MaxTerminal)
            {
                probleme.Add(Prozent("Terminalzeile laenger als %d Zeichen (Handy): %s", (long)MaxTerminal, Anfang(zeile)));
            }
        }
        if (Laenge(Get(auftrag, "prompt", "")) > 24)
        {
            probleme.Add("Eingabezeile zu lang");
        }
        if (probleme.Count == 0 && !Bewertung.TerminalPruefen(auftrag, Bewertung.TerminalLoesung(auftrag), inhalt).Richtig)
        {
            probleme.Add("die eigene Loesung wird nicht als richtig erkannt");
        }
        return probleme;
    }

    /// <summary>_validate_diagnosis_task</summary>
    public static List<string> DiagnoseAuftrag(PyObjekt auftrag, PyObjekt inhalt)
    {
        var probleme = new List<string>();
        var pruefungen = L(auftrag, "pruefungen").Select(Objekt).ToList();
        var ids = pruefungen.Select(p => Get(p, "id")).ToList();
        if (pruefungen.Count < 4 || Doppelt(ids) || !ids.All(Py.Wahr))
        {
            return ["braucht mindestens 4 Pruefungen mit eindeutiger Kennung"];
        }
        foreach (var p in pruefungen)
        {
            if (!Py.Wahr(Get(p, "text")) || !Py.Wahr(Get(p, "ergebnis")))
            {
                probleme.Add(Prozent("Pruefung '%s': Text oder Ergebnis fehlt", p["id"]));
            }
            if (Laenge(Get(p, "text", "")) > MaxOption || Laenge(Get(p, "ergebnis", "")) > MaxErgebnis)
            {
                probleme.Add(Prozent("Pruefung '%s': Text zu lang (Handy)", p["id"]));
            }
        }
        var schluessel = pruefungen.Where(p => Py.Wahr(Get(p, "entscheidend"))).Select(p => p["id"]).ToList();
        if (schluessel.Count == 0)
        {
            probleme.Add("keine entscheidende Pruefung");
        }
        var ziel = Py.F(Get(auftrag, "ziel_pruefungen", 0L));
        if (!(schluessel.Count <= ziel && ziel <= pruefungen.Count))
        {
            probleme.Add(Prozent("ziel_pruefungen muss zwischen %d und %d liegen", (long)schluessel.Count, (long)pruefungen.Count));
        }
        foreach (var (feld, antwortFeld, name) in new[] { ("ursachen", "ursache", "Ursache"), ("massnahmen", "massnahme", "Massnahme") })
        {
            var optionen = L(auftrag, feld);
            if (optionen.Count < 3 || Doppelt(optionen))
            {
                probleme.Add(Prozent("%s: braucht mindestens 3 verschiedene Moeglichkeiten", name));
            }
            if (!optionen.Any(o => Py.Gleich(o, Get(auftrag, antwortFeld))))
            {
                probleme.Add(Prozent("%s: richtige Antwort steht nicht in der Auswahl", name));
            }
            foreach (var text in optionen)
            {
                if (Laenge(text) > MaxOption)
                {
                    probleme.Add(Prozent("%s zu lang (Handy): %s", name, Anfang(text)));
                }
            }
        }
        foreach (var text in L(auftrag, "unsicher"))
        {
            if (!L(auftrag, "massnahmen").Any(m => Py.Gleich(m, text)) || Py.Gleich(text, Get(auftrag, "massnahme")))
            {
                probleme.Add(Prozent("unsichere Massnahme passt nicht zur Auswahl: %s", Anfang(text)));
            }
        }
        if (Py.Wahr(Get(auftrag, "aus_lager")))
        {
            var erste = Auftrag(Get(auftrag, "nach"), inhalt);
            if (erste is null || !Py.Gleich(erste["typ"], "bestellung"))
            {
                probleme.Add("Ersatzteile aus dem Lager brauchen eine Bestellung als Voraussetzung");
            }
            else
            {
                var typen = new PySet(Objekte(erste["bedarf"]).Select(b => b["typ"]));
                foreach (var art in Liste(auftrag["aus_lager"]))
                {
                    if (!typen.Contains(art))
                    {
                        probleme.Add(Prozent("'%s' wird in '%s' nicht bestellt", art, erste["id"]));
                    }
                }
                probleme.AddRange(LagerNutzer(erste, inhalt));
            }
        }
        if (Py.Wahr(Get(auftrag, "austausch")))
        {
            probleme.AddRange(Austausch(auftrag, inhalt));
        }
        if (probleme.Count == 0 && !Bewertung.DiagnosePruefen(auftrag, Bewertung.DiagnoseLoesung(auftrag, inhalt, null), inhalt, null).Richtig)
        {
            probleme.Add("die eigene Loesung wird nicht als richtig erkannt");
        }
        return probleme;
    }
}
