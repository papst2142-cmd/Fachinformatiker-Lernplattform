using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: die Pruefungen aus <c>validate_game_content</c>, die mit der Logik der Firma kamen
/// (Python 0.63.2): <c>_validate_firm</c> mit Auftraegen, Projekten, Sonderraeumen, Personal, Markt
/// und Filiale (Z. 10029-10568), <c>_validate_loans</c>, <c>_validate_business</c>,
/// <c>_validate_achievements</c> (Z. 13063), <c>_validate_world</c> (Z. 14876),
/// <c>_validate_fit_task</c> (Z. 15496) und <c>_validate_farm</c> (Z. 16202), in derselben
/// Reihenfolge und mit denselben Texten.
/// </summary>
public static partial class SpielPruefung
{
    /// <summary>ROOM_EFFECTS</summary>
    public static readonly IReadOnlyList<string> RaumVorteile = ["material_rabatt", "anfragen_plus", "kundenzufriedenheit_plus",
        "projekte_plus", "rueckschlag_faktor", "weiterbildung_rabatt", "weiterbildung_tage_minus"];

    /// <summary>QUIRK_KEYS</summary>
    public static readonly IReadOnlyList<string> MackenWirkungen = ["umsatz", "chance", "chance_netzwerk", "chance_hardware", "chance_talbahn",
        "projekt", "team_projekt", "team_lernen", "kz_erfolg", "kz_fehlschlag", "lernen", "lernen_hardware", "lernen_andere", "urlaub", "krank",
        "konflikt", "weiterbildung_tage", "stimmung_team", "stimmung_fehlschlag"];

    /// <summary>BADGE_PICTURES (nur die Namen; gezeichnet wird in der Oberflaeche)</summary>
    public static readonly IReadOnlyList<string> AbzeichenBilder = ["haus", "stern", "ticket", "blitz", "haken", "schild", "schluessel", "gleise",
        "personen", "urkunde", "formular", "kran", "diagramm", "pfeil", "pokal", "muenzen", "anker", "herz", "hut", "sprechblase", "magnet",
        "schraube", "buch", "server"];

    /// <summary>MAP_ROLES</summary>
    public static readonly IReadOnlyList<string> KartenRollen = ["bitweiche", "kunde", "firma", "zuhause", "filiale", "mitbewerber"];

    /// <summary>MAP_VIEWS</summary>
    public static readonly IReadOnlyList<string> KartenAnsichten = ["buero", "zuhause", "firma", "filiale", "serverfarm"];

    /// <summary>MAP_VIEW_PREFIXES</summary>
    public static readonly IReadOnlyList<string> KartenAnsichtVorsaetze = ["kunde:", "firma:", "mitbewerber:"];

    /// <summary>MAP_UNLOCKS</summary>
    public static readonly IReadOnlyList<string> KartenFreischaltungen = ["immer", "ab_tag", "kundenort", "aufgabe", "gruendung_moeglich",
        "gegruendet", "stufe_min", "filiale_moeglich", "filiale_gebaut", "serverfarm"];

    /// <summary>MAP_PARTS</summary>
    public static readonly IReadOnlyList<string> KartenTeile = ["block", "flaeche", "schild", "kreis", "mast", "tor", "baum", "geruest"];

    /// <summary>MAP_PART_CONDITIONS</summary>
    public static readonly IReadOnlyList<string> KartenBedingungen = ["ab_stufe", "bis_stufe", "ab_filiale", "bis_filiale", "raum", "wohnung",
        "erledigt", "offen", "gegruendet", "serverfarm_fertig"];

    /// <summary>MAP_ROOFS</summary>
    public static readonly IReadOnlyList<string> KartenDaecher = ["flach", "sattel", "shed"];

    /// <summary>MAP_LANDSCAPE</summary>
    public static readonly IReadOnlyList<string> KartenLandschaft = ["park", "fluss", "strasse", "bahn", "baum", "ortsname", "beschriftung", "gebaeude"];

    /// <summary>FARM_TYPES = TASK_TYPES + ("angebot", "bestueckung")</summary>
    public static IReadOnlyList<string> FarmTypen => [.. Aufgabentypen, "angebot", "bestueckung"];

    private static readonly string[] KartenTeilFelder = ["art", "x", "y", "w", "t", "h", "r", "dach", "fenster", "licht", "farbe", "lichtfarbe"];

    // -- Python-Hilfen ----------------------------------------------------

    /// <summary>isinstance(wert, (int, float)) (bool ist in Python ein int)</summary>
    private static bool IstZahl(object? wert) => wert is long or int or bool or double;

    /// <summary>a &lt;= b &lt;= c ... wie Python (bricht beim ersten falschen Vergleich ab)</summary>
    private static bool Kette(params object?[] werte)
    {
        for (var i = 0; i + 1 < werte.Length; i++)
        {
            if (!(Py.F(werte[i]) <= Py.F(werte[i + 1])))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>for key in wert: dict -> Schluessel, Liste -> Eintraege, Text -> Zeichen</summary>
    private static List<object?> Schluessel(object? wert) => wert switch
    {
        null => throw new InvalidDataException("None ist nicht iterierbar"),
        PyObjekt o => [.. o.Select(e => (object?)e.Key)],
        List<object?> l => l,
        string s => [.. s.EnumerateRunes().Select(r => (object?)r.ToString())],
        _ => throw new InvalidDataException("nicht iterierbar"),
    };

    /// <summary>d.get(k) or {} / d.get(k) or [] fuer beliebige Werte</summary>
    private static object? Oder(object? wert, object? leer) => Py.Wahr(wert) ? wert : leer;

    /// <summary>d.get(k) fuer einen Schluessel, der auch None sein darf</summary>
    private static object? GetN(PyObjekt d, object? k) => k is string s ? Get(d, s) : null;

    /// <summary>low, high = wert</summary>
    private static (object? A, object? B) Zwei(object? wert)
    {
        var l = Liste(wert);
        return l.Count == 2 ? (l[0], l[1]) : throw new InvalidDataException("zwei Werte erwartet");
    }

    private static bool Format(object? text, params string[] namen) => FormatGeht(text as string ?? throw new InvalidDataException("Text erwartet"), namen);

    private static bool Eindeutig(List<object?> werte)
    {
        for (var i = 0; i < werte.Count; i++)
        {
            for (var j = i + 1; j < werte.Count; j++)
            {
                if (Py.Gleich(werte[i], werte[j]))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool Steigend(List<object?> werte)
    {
        for (var i = 0; i + 1 < werte.Count; i++)
        {
            if (Py.Kleiner(werte[i + 1], werte[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool Gleich(IEnumerable<object?> a, IEnumerable<object?> b) => new PySet(a).SetEquals(new PySet(b));

    // -- Firma ------------------------------------------------------------

    /// <summary>_validate_orders</summary>
    public static List<string> FirmaAuftraege(PyObjekt regeln, SpielThemen themen)
    {
        var p = new List<string>();
        foreach (var k in new[] { "kunden", "angebote", "tickets" })
        {
            if (!Py.Hat(regeln, k))
            {
                p.Add(Prozent("Spiel-Firma: Abschnitt '%s' fehlt", k));
            }
        }
        if (p.Count > 0)
        {
            return p;
        }
        var angebote = Objekt(regeln["angebote"]);
        var tickets = Objekt(regeln["tickets"]);
        var arten = Get(angebote, "arten", new PyObjekt());
        var kunden = new PyDict();
        foreach (var kunde in Objekte(regeln["kunden"]))
        {
            if (kunden.ContainsKey(Get(kunde, "id")))
            {
                p.Add(Prozent("Spiel-Firma: Kunde '%s' doppelt", Get(kunde, "id")));
            }
            kunden[Get(kunde, "id")] = kunde;
            if (!Py.Enthaelt(arten, Get(kunde, "art")))
            {
                p.Add(Prozent("Spiel-Firma: Kunde '%s' hat eine unbekannte Art", Get(kunde, "id")));
            }
            foreach (var k in new[] { "name", "kontakt", "satz" })
            {
                if (!Py.Wahr(Get(kunde, k)))
                {
                    p.Add(Prozent("Spiel-Firma: Kunde '%s' ohne %s", Get(kunde, "id"), k));
                }
            }
        }
        if (kunden.Count < 3)
        {
            p.Add("Spiel-Firma: zu wenige Kunden");
        }
        foreach (var (k, art) in Objekt(arten))
        {
            var a = Objekt(art);
            if (!Kette(0L, a["von"], a["bis"], 100L))
            {
                p.Add(Prozent("Spiel-Firma: Spanne der Kundenart '%s' ungueltig", k));
            }
        }
        foreach (var (k, laune) in O(angebote, "laune"))
        {
            if (!Kette(0L, Get(Objekt(laune), "chance", 0L), 0.5) || !Py.Wahr(Get(Objekt(laune), "text")))
            {
                p.Add(Prozent("Spiel-Firma: Laune '%s' ungueltig", k));
            }
        }
        var zuschlaege = L(angebote, "zuschlaege");
        if (zuschlaege.Count == 0 || zuschlaege.Any(z => !Kette(0L, z, 100L)))
        {
            p.Add("Spiel-Firma: Gewinnzuschlaege fehlen oder sind ungueltig");
        }
        if (!Py.Wahr(Get(angebote, "artikel")))
        {
            p.Add("Spiel-Firma: keine Artikel fuer Anfragen");
        }
        foreach (var artikel in L(angebote, "artikel").Select(Objekt))
        {
            var (niedrig, hoch) = Zwei(artikel["preis"]);
            var (wenig, viel) = Zwei(artikel["menge"]);
            if (!(Py.F(niedrig) > 0 && Kette(niedrig, hoch)) || !(Py.F(wenig) > 0 && Kette(wenig, viel)))
            {
                p.Add(Prozent("Spiel-Firma: Artikel '%s' mit ungueltiger Spanne", artikel["name"]));
            }
        }
        foreach (var text in L(angebote, "texte"))
        {
            if (!Format(text, "kontakt", "menge", "artikel", "lieferzeit"))
            {
                p.Add("Spiel-Firma: Anfragetext mit unbekanntem Platzhalter");
            }
        }
        if (!Py.Wahr(Get(angebote, "texte")))
        {
            p.Add("Spiel-Firma: keine Anfragetexte");
        }
        var gesehen = new PySet();
        var stufen = Get(tickets, "stufen", new PyObjekt());
        foreach (var vorlage in L(tickets, "vorlagen").Select(Objekt))
        {
            var wo = Prozent("Spiel-Firma Kundenticket '%s'", Get(vorlage, "id"));
            if (gesehen.Contains(Get(vorlage, "id")))
            {
                p.Add(Prozent("%s: doppelt", wo));
            }
            gesehen.Add(Get(vorlage, "id"));
            if (!In(themen.Fachbereiche, Get(vorlage, "cat")))
            {
                p.Add(Prozent("%s: unbekannter Fachbereich", wo));
            }
            else if (!In(themen.ThemenVon[(string)vorlage["cat"]!], Get(vorlage, "thema")))
            {
                p.Add(Prozent("%s: Thema fehlt oder passt nicht zum Fachbereich", wo));
            }
            if (!Py.Enthaelt(stufen, Get(vorlage, "stufe")))
            {
                p.Add(Prozent("%s: unbekannte Schwierigkeit", wo));
            }
            if (!kunden.ContainsKey(Get(vorlage, "kunde")))
            {
                p.Add(Prozent("%s: unbekannter Kunde", wo));
            }
            if (!Py.Wahr(Get(vorlage, "titel")) || Laenge(Get(vorlage, "text", "")) > MaxTicket)
            {
                p.Add(Prozent("%s: Titel fehlt oder Text zu lang", wo));
            }
        }
        var noetig = Math.Max(Py.Int(Get(tickets, "mindestens", 0L)), Py.Int(Get(tickets, "grundzahl", 0L)) + Py.Int(Get(tickets, "je_mitarbeiter", 0L)) * 10);
        if (gesehen.Count < noetig)
        {
            p.Add(Prozent("Spiel-Firma: zu wenige Kundentickets (mindestens %d)", noetig));
        }
        return p;
    }

    /// <summary>_validate_projects</summary>
    public static List<string> FirmaProjekte(PyObjekt regeln, PyObjekt inhalt, SpielThemen themen)
    {
        var p = new List<string>();
        foreach (var k in new[] { "mitbewerber", "projekte" })
        {
            if (!Py.Hat(regeln, k))
            {
                p.Add(Prozent("Spiel-Firma: Abschnitt '%s' fehlt", k));
            }
        }
        if (p.Count > 0)
        {
            return p;
        }
        var mitbewerber = Objekt(regeln["mitbewerber"]);
        var firmen = L(mitbewerber, "firmen").Select(Objekt).ToList();
        var ids = firmen.Select(f => Get(f, "id")).ToList();
        if (Doppelt(ids) || !ids.Any(i => Py.Gleich(i, Betrieb.Bitweiche)))
        {
            p.Add("Spiel-Firma: Mitbewerber doppelt oder Bitweiche fehlt");
        }
        foreach (var firma in firmen)
        {
            var wo = Prozent("Spiel-Firma Mitbewerber '%s'", Get(firma, "id"));
            foreach (var k in new[] { "name", "kurz", "text" })
            {
                if (!Py.Wahr(Get(firma, k)))
                {
                    p.Add(Prozent("%s: %s fehlt", wo, k));
                }
            }
            if (!Py.Wahr(Get(firma, "kundenart")) && !Kette(0L, Get(firma, "von", -1L), Get(firma, "bis", -1L), 100L))
            {
                p.Add(Prozent("%s: Spanne ungueltig", wo));
            }
            foreach (var fach in Schluessel(Oder(Get(firma, "fach"), new PyObjekt())))
            {
                if (!In(themen.Fachbereiche, fach))
                {
                    p.Add(Prozent("%s: unbekannter Fachbereich '%s'", wo, fach));
                }
            }
            foreach (var (k, laune) in O(firma, "laune"))
            {
                if (!Kette(0L, Get(Objekt(laune), "chance", 0L), 0.5) || !Py.Wahr(Get(Objekt(laune), "text")))
                {
                    p.Add(Prozent("%s: Laune '%s' ungueltig", wo, k));
                }
            }
            var gewicht = O(firma, "gewicht");
            if (Py.F(Get(gewicht, "anfrage", 0L)) <= 0 && Py.F(Get(gewicht, "projekt", 0L)) <= 0)
            {
                p.Add(Prozent("%s: bietet nie mit", wo));
            }
        }
        var spanne = O(mitbewerber, "projekt_bieter");
        if (!(Py.F(Get(spanne, "von", 0L)) >= 1 && Kette(Get(spanne, "von", 0L), Get(spanne, "bis", 0L), (long)firmen.Count)))
        {
            p.Add("Spiel-Firma: projekt_bieter ungueltig");
        }
        var projekte = Objekt(regeln["projekte"]);
        foreach (var k in new[] { "abstand_tage", "gilt_tage", "stundensatz", "anzahlung", "stufen", "phasen", "rueckschlaege", "texte", "buero" })
        {
            if (!Py.Hat(projekte, k))
            {
                p.Add(Prozent("Spiel-Firma: projekte.%s fehlt", k));
            }
        }
        if (p.Count > 0)
        {
            return p;
        }
        var stufen = projekte["stufen"];
        foreach (var vorlage in L(inhalt, "projektarbeiten").Select(Objekt))
        {
            if (!Py.Enthaelt(stufen, Get(vorlage, "schwierigkeit")))
            {
                p.Add(Prozent("Spiel-Firma: keine Projektstufe fuer '%s'", Get(vorlage, "schwierigkeit")));
            }
            var fach = Get(vorlage, "cat") is string voll && themen.Kurz.TryGetValue(voll, out var kurz) ? kurz : null;
            if (Py.Len(Oder(GetN(Objekt(projekte["phasen"]), fach), new List<object?>())) < 2 || !Py.Wahr(GetN(Objekt(projekte["rueckschlaege"]), fach)))
            {
                p.Add(Prozent("Spiel-Firma: Phasen oder Rueckschlaege fuer '%s' fehlen", Get(vorlage, "cat")));
            }
        }
        foreach (var (k, stufe) in Objekt(stufen))
        {
            var s = Objekt(stufe);
            var (niedrig, hoch) = Zwei(s["material"]);
            if (!(0 < Py.F(s["aufwand"])) || !Kette(0L, niedrig, hoch) || Py.F(s["frist"]) < 1)
            {
                p.Add(Prozent("Spiel-Firma: Projektstufe '%s' ungueltig", k));
            }
        }
        foreach (var text in Liste(projekte["texte"]))
        {
            if (!Format(text, "kunde", "auftrag"))
            {
                p.Add("Spiel-Firma: Ausschreibungstext mit unbekanntem Platzhalter");
            }
        }
        foreach (var text in Liste(projekte["buero"]))
        {
            if (!Format(text, "phase", "titel", "kunde"))
            {
                p.Add("Spiel-Firma: Buerosatz mit unbekanntem Platzhalter");
            }
        }
        if (Py.Kleiner(projekte["gilt_tage"], projekte["abstand_tage"]))
        {
            p.Add("Spiel-Firma: Ausschreibungen gelten kuerzer als ihr Abstand");
        }
        return p;
    }

    /// <summary>_validate_rooms</summary>
    public static List<string> FirmaRaeume(PyObjekt regeln)
    {
        var p = new List<string>();
        var aufbau = Get(regeln, "sonderraeume");
        if (!Py.Wahr(aufbau))
        {
            return p;
        }
        var setup = Objekt(aufbau);
        var stufen = L(Objekt(regeln["gebaeude"]), "stufen").Select(Objekt).ToList();
        var raeume = L(setup, "raeume").Select(Objekt).ToList();
        if (Doppelt(raeume.Select(r => Get(r, "id")).ToList()))
        {
            p.Add("Spiel-Firma: Sonderraum-Kennungen sind nicht eindeutig");
        }
        foreach (var raum in raeume)
        {
            var wo = Prozent("Spiel-Firma Sonderraum %s", Get(raum, "id"));
            foreach (var k in new[] { "name", "raum", "vorteil" })
            {
                if (!Py.Wahr(Get(raum, k)))
                {
                    p.Add(Prozent("%s: '%s' fehlt", wo, k));
                }
            }
            var nummer = Get(raum, "ab_stufe");
            if (!Py.IstInt(nummer) || !(1 <= Py.Int(nummer) && Py.Int(nummer) <= stufen.Count))
            {
                p.Add(Prozent("%s: ab_stufe ungueltig", wo));
                continue;
            }
            if (!Py.IstInt(Get(raum, "preis")) || Py.Int(raum["preis"]) <= 0 || !Py.IstInt(Get(raum, "nebenkosten")) || Py.Int(raum["nebenkosten"]) < 0)
            {
                p.Add(Prozent("%s: Preis oder Nebenkosten ungueltig", wo));
            }
            foreach (var stufe in stufen.Skip((int)Py.Int(nummer) - 1))
            {
                var flaechen = new PySet(Objekte(Objekt(stufe["gebaeude"])["raeume"]).Select(r => r["id"]));
                if (!flaechen.Contains(raum["raum"]))
                {
                    p.Add(Prozent("%s: Flaeche '%s' fehlt in Stufe %d", wo, raum["raum"], stufe["stufe"]));
                }
                if (L(stufe, "plaetze").Any(platz => Py.Gleich(Liste(platz) is { Count: 3 } drei ? drei[2] : throw new InvalidDataException("x, y, raum erwartet"), raum["raum"])))
                {
                    p.Add(Prozent("%s: Arbeitsplatz in der Flaeche", wo));
                }
            }
            foreach (var (k, wert) in O(raum, "effekt"))
            {
                if (!RaumVorteile.Contains(k) || !IstZahl(wert))
                {
                    p.Add(Prozent("%s: unbekannter Vorteil '%s'", wo, k));
                }
            }
            if (!Py.Wahr(Get(raum, "effekt")))
            {
                p.Add(Prozent("%s: kein Vorteil", wo));
            }
        }
        return p;
    }

    /// <summary>_validate_personal</summary>
    public static List<string> FirmaPersonal(PyObjekt regeln, PySet kollegen)
    {
        var p = new List<string>();
        var gesehen = new PySet();
        var macken = L(regeln, "macken");
        foreach (var eintrag in macken)
        {
            if (eintrag is not PyObjekt macke)
            {
                p.Add("Spiel-Firma: Macke ohne Kennung (ab 0.43 Objekte)");
                continue;
            }
            var wo = Prozent("Spiel-Firma Macke %s", Get(macke, "id"));
            foreach (var k in new[] { "id", "name", "text" })
            {
                if (!Py.Wahr(Get(macke, k)))
                {
                    p.Add(Prozent("%s: '%s' fehlt", wo, k));
                }
            }
            if (gesehen.Contains(Get(macke, "id")))
            {
                p.Add(Prozent("%s: Kennung doppelt", wo));
            }
            gesehen.Add(Get(macke, "id"));
            if (!Py.Wahr(Get(macke, "plus")) || !Py.Wahr(Get(macke, "minus")))
            {
                p.Add(Prozent("%s: braucht Staerke (plus) und Schwaeche (minus)", wo));
            }
            foreach (var seite in new[] { "plus", "minus" })
            {
                foreach (var (k, wert) in O(macke, seite))
                {
                    if (!MackenWirkungen.Contains(k) || !IstZahl(wert))
                    {
                        p.Add(Prozent("%s: unbekannte Wirkung '%s'", wo, k));
                    }
                }
            }
            if (Py.Wahr(Get(macke, "kollege")) && !kollegen.Contains(macke["kollege"]))
            {
                p.Add(Prozent("%s: unbekannte Person '%s'", wo, macke["kollege"]));
            }
        }
        if (macken.Count(m => m is PyObjekt o && !Py.Wahr(Get(o, "kollege"))) < 5)
        {
            p.Add("Spiel-Firma: zu wenige Macken fuer Bewerber");
        }
        var personal = O(regeln, "personal");
        foreach (var k in new[] { "stimmung", "krankheit", "urlaub", "konflikt", "coaching", "macken" })
        {
            if (!Py.Hat(personal, k))
            {
                p.Add(Prozent("Spiel-Firma: personal.%s fehlt", k));
            }
        }
        if (p.Count > 0)
        {
            return p;
        }
        var stufen = Schluessel(Oder(Get(Objekt(personal["macken"]), "stufen"), new PyObjekt())).Select(s => (string)s!).Order(StringComparer.Ordinal);
        if (!stufen.SequenceEqual(["1", "2", "3"]))
        {
            p.Add("Spiel-Firma: personal.macken.stufen braucht die Stufen 1 bis 3");
        }
        var konflikt = Objekt(personal["konflikt"]);
        var texte = L(konflikt, "texte");
        if (texte.Count < 3)
        {
            p.Add("Spiel-Firma: zu wenige Konflikt-Texte");
        }
        foreach (var text in texte.Append(Get(konflikt, "summen", "")))
        {
            if (!Format(text, "a", "b", "a_von", "b_von"))
            {
                p.Add(Prozent("Spiel-Firma: Konflikt-Text mit falschem Platzhalter: %s", Anfang(text)));
            }
        }
        return p;
    }

    /// <summary>_validate_market</summary>
    public static List<string> FirmaMarkt(PyObjekt regeln, SpielThemen themen)
    {
        var p = new List<string>();
        var rivalitaet = O(regeln, "rivalitaet");
        var rueckhol = O(rivalitaet, "rueckhol");
        foreach (var k in new[] { "vorher_anteil", "ab_tagen", "chance", "abstand", "abstand_firma", "gehalt_plus", "bonus_tage" })
        {
            if (!IstZahl(Get(rueckhol, k)) || Py.F(rueckhol[k]) < 0)
            {
                p.Add(Prozent("Spiel-Firma: rivalitaet.rueckhol.%s fehlt oder ist ungueltig", k));
            }
        }
        var spanne = Liste(Oder(Get(rueckhol, "angebot_plus"), new List<object?>()));
        if (spanne.Count != 2 || Py.Kleiner(spanne[1], spanne[0]))
        {
            p.Add("Spiel-Firma: rivalitaet.rueckhol.angebot_plus braucht [von, bis]");
        }
        var texte = L(rueckhol, "texte").Concat(L(rueckhol, "texte_bitweiche")).ToList();
        if (L(rueckhol, "texte").Count < 2)
        {
            p.Add("Spiel-Firma: zu wenige Rueckhol-Texte");
        }
        foreach (var text in texte)
        {
            if (!Format(text, "firma", "name", "plus"))
            {
                p.Add(Prozent("Spiel-Firma: Rueckhol-Text mit falschem Platzhalter: %s", Anfang(text)));
            }
        }
        var wind = O(rivalitaet, "gegenwind");
        foreach (var k in new[] { "fenster", "mindestens", "ab_quote", "zuschlag_minus" })
        {
            if (!IstZahl(Get(wind, k)) || Py.F(wind[k]) < 0)
            {
                p.Add(Prozent("Spiel-Firma: rivalitaet.gegenwind.%s fehlt oder ist ungueltig", k));
            }
        }
        if (Py.Hat(wind, "vorteil_faktor") && (!IstZahl(wind["vorteil_faktor"]) || !Kette(0L, wind["vorteil_faktor"], 1L)))
        {
            p.Add("Spiel-Firma: rivalitaet.gegenwind.vorteil_faktor muss zwischen 0 und 1 liegen");
        }
        if (Py.IstInt(Get(wind, "mindestens")) && Py.IstInt(Get(wind, "fenster")) && Py.Int(wind["mindestens"]) > Py.Int(wind["fenster"]))
        {
            p.Add("Spiel-Firma: gegenwind.mindestens liegt ueber dem Fenster");
        }
        var markt = O(regeln, "markt");
        var phasen = O(O(markt, "konjunktur"), "phasen");
        if (!Gleich(phasen.Select(e => (object?)e.Key), ["neutral", "aufschwung", "abschwung"]))
        {
            p.Add("Spiel-Firma: markt.konjunktur.phasen braucht neutral, aufschwung und abschwung");
        }
        foreach (var (k, phase) in phasen)
        {
            foreach (var feld in new[] { "name", "tage", "anfragen", "zuschlag", "umsatz" })
            {
                if (!Py.Enthaelt(phase, feld))
                {
                    p.Add(Prozent("Spiel-Firma: Konjunktur %s ohne '%s'", k, feld));
                }
            }
        }
        var trends = O(markt, "trends");
        foreach (var k in new[] { "erster_nach", "dauer", "pause" })
        {
            var s = L(trends, k);
            if (s.Count != 2 || Py.Kleiner(s[1], s[0]) || Py.F(s[0]) < 1)
            {
                p.Add(Prozent("Spiel-Firma: markt.trends.%s braucht [von, bis]", k));
            }
        }
        var gesehen = new PySet();
        foreach (var trend in L(trends, "liste").Select(Objekt))
        {
            var wo = Prozent("Spiel-Firma Trend %s", Get(trend, "id"));
            foreach (var feld in new[] { "id", "name", "cat", "start", "ende", "artikel" })
            {
                if (!Py.Wahr(Get(trend, feld)))
                {
                    p.Add(Prozent("%s: '%s' fehlt", wo, feld));
                }
            }
            if (gesehen.Contains(Get(trend, "id")))
            {
                p.Add(Prozent("%s: Kennung doppelt", wo));
            }
            gesehen.Add(Get(trend, "id"));
            if (Py.Wahr(Get(trend, "cat")) && !In(themen.Fachbereiche, trend["cat"]))
            {
                p.Add(Prozent("%s: unbekannter Fachbereich '%s'", wo, trend["cat"]));
            }
            foreach (var artikel in L(trend, "artikel").Select(Objekt))
            {
                if (L(artikel, "preis").Count != 2 || L(artikel, "menge").Count != 2 || !Py.Wahr(Get(artikel, "name")))
                {
                    p.Add(Prozent("%s: Artikel unvollstaendig", wo));
                }
            }
        }
        if (L(trends, "liste").Count < 2)
        {
            p.Add("Spiel-Firma: mindestens zwei Trends noetig");
        }
        return p;
    }

    /// <summary>Gebaeudestufen der Firma und der Filiale: Grundriss, Arbeitsplaetze, frei?</summary>
    private static List<string> Stufe(PyObjekt stufe, string wo, string kennung)
    {
        var p = new List<string>();
        var plaetze = L(stufe, "plaetze").Select(Liste).ToList();
        var personen = plaetze.Select((pl, i) => new PyObjekt
        {
            { "id", kennung + i }, { "raum", pl.Count == 3 ? pl[2] : throw new InvalidDataException("x, y, raum erwartet") },
            { "platz", new List<object?> { pl[0], pl[1] } },
        });
        p.AddRange(Gebaeude(Objekt(stufe["gebaeude"]), wo, personen.ToList()));
        var frei = Wege.BasisRaster(Objekt(stufe["gebaeude"]));
        foreach (var pl in plaetze)
        {
            if (!frei.Contains(Wege.Zelle(Py.F(pl[0]), Py.F(pl[1]))))
            {
                p.Add(Prozent("%s: Arbeitsplatz %.1f/%.1f ist verstellt", wo, pl[0], pl[1]));
            }
        }
        return p;
    }

    /// <summary>_validate_firm</summary>
    public static List<string> Firma(PyObjekt inhalt, SpielThemen themen)
    {
        var p = new List<string>();
        var regeln = O(inhalt, "firma");
        if (regeln.Count == 0)
        {
            return ["Spiel: firma.json fehlt"];
        }
        foreach (var k in new[] { "gruendung", "gebaeude", "bewerbung", "gehalt", "umsatz", "weiterbildung", "namen", "macken" })
        {
            if (!Py.Hat(regeln, k))
            {
                p.Add(Prozent("Spiel-Firma: Abschnitt '%s' fehlt", k));
            }
        }
        if (p.Count > 0)
        {
            return p;
        }
        var stufen = L(Objekt(regeln["gebaeude"]), "stufen").Select(Objekt).ToList();
        if (stufen.Count == 0)
        {
            p.Add("Spiel-Firma: keine Gebaeudestufen");
        }
        var plaetze = 0;
        for (var nummer = 1; nummer <= stufen.Count; nummer++)
        {
            var stufe = stufen[nummer - 1];
            var wo = Prozent("Spiel-Firma Stufe %d", (long)nummer);
            if (!Py.Gleich(Get(stufe, "stufe"), (long)nummer))
            {
                p.Add(Prozent("%s: falsche Nummer", wo));
            }
            if (nummer == 1 && Py.Wahr(Get(stufe, "preis", 0L)))
            {
                p.Add(Prozent("%s: die Startstufe kostet nichts extra", wo));
            }
            var anzahl = L(stufe, "plaetze").Count;
            if (anzahl <= plaetze)
            {
                p.Add(Prozent("%s: bringt keine zusaetzlichen Arbeitsplaetze", wo));
            }
            plaetze = anzahl;
            p.AddRange(Stufe(stufe, wo, "platz"));
        }
        var namen = Objekt(regeln["namen"]);
        foreach (var k in new[] { "vornamen", "nachnamen" })
        {
            if (L(namen, k).Count < 5)
            {
                p.Add(Prozent("Spiel-Firma: zu wenige %s", k));
            }
        }
        var bewerbung = Objekt(regeln["bewerbung"]);
        if (!Kette(0L, bewerbung["werte_min"], bewerbung["werte_max"], 90L))
        {
            p.Add("Spiel-Firma: Wertebereich der Bewerber ungueltig");
        }
        p.AddRange(FirmaAuftraege(regeln, themen));
        p.AddRange(FirmaProjekte(regeln, inhalt, themen));
        p.AddRange(FirmaRaeume(regeln));
        var ids = new PySet(Objekte(inhalt["kollegen"]).Select(k => k["id"]));
        foreach (var wechsel in Liste(Get(regeln, "wechsel", new List<object?>())).Select(Objekt))
        {
            if (!ids.Contains(Get(wechsel, "kollege")))
            {
                p.Add(Prozent("Spiel-Firma: Wechsel von unbekannter Person '%s'", Get(wechsel, "kollege")));
            }
            if (!Gleich(Schluessel(Get(wechsel, "werte", new PyObjekt())), themen.Fachbereiche))
            {
                p.Add(Prozent("Spiel-Firma: Wechsel %s braucht Werte fuer alle Fachbereiche", Get(wechsel, "kollege")));
            }
            foreach (var thema in L(wechsel, "staerken"))
            {
                if (!(thema is string t && themen.FachbereichVon.ContainsKey(t)))
                {
                    p.Add(Prozent("Spiel-Firma: Wechsel %s mit unbekannter Staerke '%s'", Get(wechsel, "kollege"), thema));
                }
            }
        }
        p.AddRange(FirmaPersonal(regeln, ids));
        p.AddRange(FirmaMarkt(regeln, themen));
        p.AddRange(Filiale(regeln));
        var lernen = O(regeln, "lernen");
        foreach (var k in new[] { "ticket_erfolg", "projekt_fertig", "projekt_puenktlich", "routine_tage", "routine_plus", "halb_ab", "deckel", "max", "alt_streuung" })
        {
            if (!IstZahl(Get(lernen, k)) || Py.F(lernen[k]) < 0)
            {
                p.Add(Prozent("Spiel-Firma: lernen.%s fehlt oder ist ungueltig", k));
            }
        }
        if (IstZahl(Get(lernen, "deckel")) && IstZahl(Get(lernen, "max")) && Py.Kleiner(lernen["max"], lernen["deckel"]))
        {
            p.Add("Spiel-Firma: lernen.deckel liegt ueber lernen.max");
        }
        var fach = O(O(regeln, "weiterbildung"), "fachbereich");
        foreach (var k in new[] { "preis", "tage", "plus" })
        {
            if (!Py.IstInt(Get(fach, k)) || Py.Int(fach[k]) <= 0)
            {
                p.Add(Prozent("Spiel-Firma: weiterbildung.fachbereich.%s ungueltig", k));
            }
        }
        return p;
    }

    /// <summary>_validate_branch</summary>
    public static List<string> Filiale(PyObjekt regeln)
    {
        var p = new List<string>();
        var filiale = O(regeln, "filiale");
        if (filiale.Count == 0)
        {
            return p;
        }
        var voraussetzung = O(filiale, "voraussetzung");
        foreach (var k in new[] { "stufe", "mitarbeiter", "firmentage" })
        {
            if (!Py.IstInt(Get(voraussetzung, k)) || Py.Int(voraussetzung[k]) < 0)
            {
                p.Add(Prozent("Spiel-Filiale: voraussetzung.%s fehlt oder ist ungueltig", k));
            }
        }
        if (Py.Kleiner((long)L(Objekt(regeln["gebaeude"]), "stufen").Count, Get(voraussetzung, "stufe", 0L)))
        {
            p.Add("Spiel-Filiale: voraussetzung.stufe gibt es nicht");
        }
        if (!Py.Wahr(Get(filiale, "name_vorschlag")))
        {
            p.Add("Spiel-Filiale: name_vorschlag fehlt");
        }
        var plaetze = 0;
        var stufen = L(filiale, "stufen").Select(Objekt).ToList();
        for (var nummer = 1; nummer <= stufen.Count; nummer++)
        {
            var stufe = stufen[nummer - 1];
            var wo = Prozent("Spiel-Filiale Stufe %d", (long)nummer);
            if (!Py.Gleich(Get(stufe, "stufe"), (long)nummer))
            {
                p.Add(Prozent("%s: falsche Nummer", wo));
            }
            if (!Py.IstInt(Get(stufe, "preis")) || Py.Int(stufe["preis"]) <= 0)
            {
                p.Add(Prozent("%s: Preis fehlt", wo));
            }
            var anzahl = L(stufe, "plaetze").Count;
            if (anzahl <= plaetze)
            {
                p.Add(Prozent("%s: bringt keine zusaetzlichen Arbeitsplaetze", wo));
            }
            plaetze = anzahl;
            p.AddRange(Stufe(stufe, wo, "filiale"));
        }
        if (stufen.Count == 0)
        {
            p.Add("Spiel-Filiale: keine Stufen");
        }
        var orte = new PySet(Liste(Get(regeln, "kunden", new List<object?>())).Select(Objekt).Where(k => Py.Wahr(Get(k, "ort"))).Select(k => k["ort"]));
        foreach (var ort in L(O(filiale, "naehe"), "orte"))
        {
            if (!orte.Contains(ort))
            {
                p.Add(Prozent("Spiel-Filiale: kein Kunde mit ort '%s'", ort));
            }
        }
        return p;
    }

    // -- Kredite und Geschaeft --------------------------------------------

    /// <summary>_validate_loans</summary>
    public static List<string> Kredite(PyObjekt inhalt)
    {
        var regeln = O(O(inhalt, "balancing"), "kredite");
        if (regeln.Count == 0)
        {
            return ["Spiel: balancing.kredite fehlt"];
        }
        var p = new List<string>();
        foreach (var k in new[] { "bank", "tage_pro_jahr", "bonitaet", "rahmen", "frei", "zins", "pakete", "abloesen_prozent", "mahnung" })
        {
            if (!Py.Hat(regeln, k))
            {
                p.Add(Prozent("Spiel-Kredite: '%s' fehlt", k));
            }
        }
        if (p.Count > 0)
        {
            return p;
        }
        var basis = Objekt(Objekt(regeln["zins"])["basis"]);
        var laufzeiten = basis.Select(e => e.Key).ToList();
        if (laufzeiten.Count == 0 || !laufzeiten.All(k => k.Length > 0 && k.All(char.IsAsciiDigit) && long.Parse(k, System.Globalization.CultureInfo.InvariantCulture) > 0))
        {
            p.Add("Spiel-Kredite: Laufzeiten in zins.basis ungueltig");
            return p;
        }
        var saetze = laufzeiten.Select(k => long.Parse(k, System.Globalization.CultureInfo.InvariantCulture)).Order()
            .Select(t => Py.F(Py.Hat(basis, t.ToString(System.Globalization.CultureInfo.InvariantCulture)) ? basis[t.ToString(System.Globalization.CultureInfo.InvariantCulture)]
                : throw new KeyNotFoundException(t.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ToList();
        if (!saetze.SequenceEqual(saetze.Order()))
        {
            p.Add("Spiel-Kredite: laengere Laufzeit muss mindestens so teuer sein");
        }
        var ids = new PySet();
        foreach (var paket in Objekte(regeln["pakete"]))
        {
            if (ids.Contains(Get(paket, "id")) || Py.Gleich(Get(paket, "id"), Wirtschaft.FreierKredit))
            {
                p.Add(Prozent("Spiel-Kredite: Paket-Kennung '%s' doppelt oder reserviert", Get(paket, "id")));
            }
            ids.Add(Get(paket, "id"));
            if (!Py.Hat(basis, Py.Str(Get(paket, "laufzeit"))))
            {
                p.Add(Prozent("Spiel-Kredite: Paket %s mit unbekannter Laufzeit", paket["id"]));
            }
            if (!Py.IstInt(Get(paket, "summe")) || Py.Int(paket["summe"]) <= 0)
            {
                p.Add(Prozent("Spiel-Kredite: Paket %s ohne gueltige Summe", paket["id"]));
            }
        }
        var mahnstufen = L(Objekt(regeln["mahnung"]), "stufen").Select(Objekt).ToList();
        var nummern = mahnstufen.Select(s => Get(s, "stufe")).ToList();
        if (!Py.Gleich(nummern, Enumerable.Range(1, mahnstufen.Count).Select(i => (object?)(long)i).ToList()) || mahnstufen.Count == 0)
        {
            p.Add("Spiel-Kredite: Mahnstufen muessen 1, 2, 3 ... heissen");
        }
        foreach (var stufe in mahnstufen)
        {
            foreach (var k in Schluessel(Oder(Get(stufe, "reputation"), new PyObjekt())))
            {
                if (!In(Ereignisse.Achsen, k))
                {
                    p.Add(Prozent("Spiel-Kredite: Mahnstufe %s mit unbekannter Achse '%s'", Get(stufe, "stufe"), k));
                }
            }
        }
        return p;
    }

    /// <summary>_validate_business</summary>
    public static List<string> Geschaeft(PyObjekt inhalt, SpielThemen themen)
    {
        var balancing = O(inhalt, "balancing");
        var p = new List<string>();
        var steuer = O(balancing, "steuern");
        if (steuer.Count == 0)
        {
            p.Add("Spiel: balancing.steuern fehlt");
        }
        else
        {
            foreach (var k in new[] { "finanzamt", "satz", "faellig_tage", "ruecklage_prozent" })
            {
                if (!Py.Hat(steuer, k))
                {
                    p.Add(Prozent("Spiel-Steuern: '%s' fehlt", k));
                }
            }
            var satz = Py.F(Get(steuer, "satz", 0L));
            if (!(0 < satz && satz < 100))
            {
                p.Add("Spiel-Steuern: Steuersatz ungueltig");
            }
            if (Py.Int(Get(steuer, "faellig_tage", 0L)) < 1)
            {
                p.Add("Spiel-Steuern: faellig_tage muss mindestens 1 sein");
            }
        }
        string[] wirkungen = ["anfragen", "tickets", "groesse", "vorteil"];
        var ids = new PySet();
        foreach (var form in L(O(balancing, "marketing"), "formen").Select(Objekt))
        {
            var wo = Prozent("Spiel-Werbung %s", Get(form, "id"));
            if (!Py.Wahr(Get(form, "id")) || ids.Contains(form["id"]))
            {
                p.Add(Prozent("%s: Kennung fehlt oder doppelt", wo));
            }
            ids.Add(Get(form, "id"));
            if (Py.Int(Get(form, "laufend", 0L)) <= 0)
            {
                p.Add(Prozent("%s: laufende Kosten fehlen", wo));
            }
            var einmal = O(form, "einmalig");
            if (Py.Int(Get(einmal, "preis", 0L)) <= 0 || Py.Int(Get(einmal, "tage", 0L)) <= 0)
            {
                p.Add(Prozent("%s: einmalig braucht preis und tage", wo));
            }
            var wirkung = O(form, "wirkung");
            if (wirkung.Count == 0 || wirkung.Any(e => !wirkungen.Contains(e.Key)))
            {
                p.Add(Prozent("%s: unbekannte oder keine Wirkung", wo));
            }
        }
        if (ids.Count == 0)
        {
            p.Add("Spiel: balancing.marketing ohne Werbeformen");
        }
        var zert = O(balancing, "zertifizierungen");
        var zertIds = new PySet();
        foreach (var z in L(zert, "liste").Select(Objekt))
        {
            var wo = Prozent("Spiel-Zertifizierung %s", Get(z, "id"));
            if (!Py.Wahr(Get(z, "id")) || zertIds.Contains(z["id"]))
            {
                p.Add(Prozent("%s: Kennung fehlt oder doppelt", wo));
            }
            zertIds.Add(Get(z, "id"));
            if (Py.Int(Get(z, "preis", 0L)) <= 0 || Py.Int(Get(z, "tage", 0L)) <= 0)
            {
                p.Add(Prozent("%s: preis und tage fehlen", wo));
            }
            foreach (var k in Schluessel(Oder(Get(z, "vorteil"), new PyObjekt())))
            {
                if (!Py.Gleich(k, "alle") && !In(themen.Fachbereiche, k))
                {
                    p.Add(Prozent("%s: unbekannter Fachbereich '%s'", wo, k));
                }
            }
        }
        if (zertIds.Count == 0)
        {
            p.Add("Spiel: balancing.zertifizierungen ohne Liste");
        }
        var gross = O(zert, "grossauftraege");
        foreach (var k in new[] { "abstand_tage", "gilt_tage", "stufe", "liste" })
        {
            if (!Py.Hat(gross, k))
            {
                p.Add(Prozent("Spiel-Grossauftraege: '%s' fehlt", k));
            }
        }
        var grossIds = new PySet();
        foreach (var g in L(gross, "liste").Select(Objekt))
        {
            var wo = Prozent("Spiel-Grossauftrag %s", Get(g, "id"));
            if (!Py.Wahr(Get(g, "id")) || grossIds.Contains(g["id"]))
            {
                p.Add(Prozent("%s: Kennung fehlt oder doppelt", wo));
            }
            grossIds.Add(Get(g, "id"));
            if (!In(themen.Fachbereiche, Get(g, "cat")))
            {
                p.Add(Prozent("%s: unbekannter Fachbereich", wo));
            }
            else if (!In(themen.ThemenVon[(string)g["cat"]!], Get(g, "thema")))
            {
                p.Add(Prozent("%s: Thema passt nicht zum Fachbereich", wo));
            }
            var braucht = L(g, "braucht");
            if (braucht.Count == 0 || braucht.Any(b => !zertIds.Contains(b)))
            {
                p.Add(Prozent("%s: braucht unbekannte oder keine Zertifizierung", wo));
            }
            foreach (var k in new[] { "titel", "kunde", "kunde_kurz", "ausgangssituation", "auftrag" })
            {
                if (!Py.Wahr(Get(g, k)))
                {
                    p.Add(Prozent("%s: '%s' fehlt", wo, k));
                }
            }
        }
        return p;
    }

    // -- Erfolge und Karte ------------------------------------------------

    /// <summary>_validate_achievements</summary>
    public static List<string> Erfolge(PyObjekt inhalt)
    {
        var regeln = Betrieb.ErfolgRegeln(inhalt);
        if (regeln.Count == 0)
        {
            return ["Spiel: erfolge.json fehlt"];
        }
        var p = new List<string>();
        var gruppen = new PySet(L(regeln, "gruppen").Select(g => g is string s ? s[..1] : Liste(g)[0]));
        var orte = new PySet(L(O(inhalt, "orte"), "orte").Select(o => Objekt(o)["id"]));
        var ids = new PySet();
        foreach (var regel in L(regeln, "erfolge").Select(Objekt))
        {
            var wo = Prozent("Spiel-Erfolg %s", Get(regel, "id"));
            if (ids.Contains(Get(regel, "id")))
            {
                p.Add(Prozent("%s: Kennung doppelt", wo));
            }
            ids.Add(Get(regel, "id"));
            if (!gruppen.Contains(Get(regel, "gruppe")))
            {
                p.Add(Prozent("%s: unbekannte Gruppe '%s'", wo, Get(regel, "gruppe")));
            }
            if (!In(Betrieb.LaufKennzahlen, Get(regel, "wert")))
            {
                p.Add(Prozent("%s: unbekannter Wert '%s'", wo, Get(regel, "wert")));
            }
            if (!In(AbzeichenBilder, Get(regel, "bild")))
            {
                p.Add(Prozent("%s: unbekanntes Bild '%s'", wo, Get(regel, "bild")));
            }
            if (Py.Wahr(Get(regel, "ort")) && !orte.Contains(regel["ort"]))
            {
                p.Add(Prozent("%s: unbekannter Ort '%s'", wo, regel["ort"]));
            }
            if (Py.Wahr(Get(regel, "bestwert")) && Betrieb.BestwertRegel(regel["bestwert"], inhalt) is null)
            {
                p.Add(Prozent("%s: unbekannter Bestwert '%s'", wo, regel["bestwert"]));
            }
            if (Py.Wahr(Get(regel, "moment_text")) && !Format(regel["moment_text"], "ziel", "tag", "spieler", "firma", "filiale"))
            {
                p.Add(Prozent("%s: unbekannter Platzhalter im moment_text", wo));
            }
            var stufen = L(regel, "stufen").Select(Objekt).ToList();
            if (stufen.Count == 0 || stufen.Count > 3)
            {
                p.Add(Prozent("%s: 1 bis 3 Stufen noetig", wo));
            }
            var folge = stufen.Select(s => Get(s, "stufe")).ToList();
            var plaetze = folge.Where(t => In(Betrieb.StufenFolge, t)).Select(t => Betrieb.StufenFolge.ToList().IndexOf((string)t!)).ToList();
            if (folge.Any(t => !In(Betrieb.StufenFolge, t)) || !plaetze.SequenceEqual(plaetze.Order()))
            {
                p.Add(Prozent("%s: Stufen muessen Bronze, Silber, Gold sein (aufsteigend)", wo));
            }
            var ziele = stufen.Select(s => Get(s, "ab", 1L)).ToList();
            if (!Steigend(ziele) || !Eindeutig(ziele))
            {
                p.Add(Prozent("%s: Stufen-Werte muessen steigen", wo));
            }
        }
        foreach (var regel in L(regeln, "bestwerte").Select(Objekt))
        {
            if (!In(Betrieb.LaufKennzahlen, Get(regel, "id")))
            {
                p.Add(Prozent("Spiel-Bestwert %s: unbekannter Wert", Get(regel, "id")));
            }
            if (!In(["mehr", "weniger"], Get(regel, "besser")))
            {
                p.Add(Prozent("Spiel-Bestwert %s: besser muss mehr oder weniger sein", Get(regel, "id")));
            }
        }
        return p;
    }

    /// <summary>_part_visible mit _map_context(None): Spielstand ohne Firma, Filiale, Raeume und Erledigtes.</summary>
    private static bool TeilSichtbarLeer(PyObjekt teil, object? wohnung)
    {
        if (Py.Hat(teil, "ab_stufe") && Py.Kleiner(0L, teil["ab_stufe"]))
        {
            return false;
        }
        if (Py.Hat(teil, "bis_stufe") && Py.Kleiner(teil["bis_stufe"], 0L))
        {
            return false;
        }
        if (Py.Hat(teil, "ab_filiale") && Py.Kleiner(0L, teil["ab_filiale"]))
        {
            return false;
        }
        if (Py.Hat(teil, "bis_filiale") && Py.Kleiner(teil["bis_filiale"], 0L))
        {
            return false;
        }
        if (Py.Hat(teil, "raum") || Py.Hat(teil, "erledigt"))
        {
            return false;   // keine Raeume gebaut, nichts erledigt
        }
        if (Py.Hat(teil, "wohnung") && !Py.Enthaelt(teil["wohnung"], wohnung))
        {
            return false;
        }
        if (Py.Hat(teil, "gegruendet") && Py.Wahr(teil["gegruendet"]))
        {
            return false;
        }
        if (Py.Hat(teil, "serverfarm_fertig") && Py.Wahr(teil["serverfarm_fertig"]))
        {
            return false;
        }
        return true;
    }

    /// <summary>_validate_world</summary>
    public static List<string> Welt(PyObjekt inhalt)
    {
        var regeln = O(inhalt, "orte");
        if (regeln.Count == 0)
        {
            return ["Spiel: orte.json fehlt"];
        }
        var p = new List<string>();
        var welt = O(regeln, "welt");
        var (breite, hoehe) = (Get(welt, "breite", 0L), Get(welt, "hoehe", 0L));
        if (Py.F(breite) <= 0 || Py.F(hoehe) <= 0)
        {
            p.Add("Spiel-Karte: Breite/Hoehe fehlt");
        }
        var modelle = O(regeln, "modelle");
        foreach (var stueck in L(welt, "landschaft").Select(Objekt))
        {
            if (!In(KartenLandschaft, Get(stueck, "typ")))
            {
                p.Add(Prozent("Spiel-Karte: unbekannte Landschaft '%s'", Get(stueck, "typ")));
            }
            if (Py.Gleich(Get(stueck, "typ"), "gebaeude") && !Py.Enthaelt(modelle, Get(stueck, "modell")))
            {
                p.Add(Prozent("Spiel-Karte: unbekanntes Modell '%s'", Get(stueck, "modell")));
            }
        }
        var kundenorte = Kundenorte(inhalt).Select(o => o["id"]).Distinct(PyVergleich.Gleich).ToList();
        var orteMenge = new PySet(kundenorte);
        var auftraege = new PySet(Objekte(inhalt["aufgaben"]).Select(a => a["id"]));
        var raeume = new PySet(Betrieb.SonderRaeume(inhalt).Select(r => r["id"]));
        var wohnungen = Objekte(Objekt(inhalt["wohnungen"])["wohnungen"]).Select(w => w["id"]).Distinct(PyVergleich.Gleich).ToList();
        var wohnMenge = new PySet(wohnungen);
        var gesehen = new PySet();
        var orte = L(regeln, "orte").Select(Objekt).ToList();
        foreach (var ort in orte)
        {
            var wo = Prozent("Spiel-Ort %s", Get(ort, "id"));
            if (!Py.Wahr(Get(ort, "id")) || gesehen.Contains(ort["id"]))
            {
                p.Add(Prozent("%s: Kennung fehlt oder doppelt", wo));
            }
            gesehen.Add(Get(ort, "id"));
            if (!Py.Wahr(Get(ort, "name")))
            {
                p.Add(Prozent("%s: Name fehlt", wo));
            }
            if (!Py.Enthaelt(modelle, Get(ort, "modell")))
            {
                p.Add(Prozent("%s: unbekanntes Modell '%s'", wo, Get(ort, "modell")));
            }
            if (!(Kette(0L, Get(ort, "x", -1L), breite) && Kette(0L, Get(ort, "y", -1L), hoehe)))
            {
                p.Add(Prozent("%s: liegt ausserhalb der Karte", wo));
            }
            var schild = L(ort, "schild");
            if (schild.Count != 2 || !(Kette(0L, schild[0], breite) && Kette(0L, schild[1], hoehe)))
            {
                p.Add(Prozent("%s: Schild fehlt oder liegt ausserhalb", wo));
            }
            var varianten = new List<object?> { ort };
            varianten.AddRange(new[] { "nach_gruendung", "vor_gruendung", "vor_filiale" }.Where(k => Py.Hat(ort, k)).Select(k => ort[k]));
            foreach (var v in varianten)
            {
                var variante = Objekt(v);
                if (Py.Hat(variante, "rolle") && !In(KartenRollen, variante["rolle"]))
                {
                    p.Add(Prozent("%s: unbekannte Rolle '%s'", wo, variante["rolle"]));
                }
                var ansicht = Get(variante, "ansicht");
                if (ansicht is null && !ReferenceEquals(variante, ort))
                {
                    continue;
                }
                var text = Py.Str(ansicht);
                if (!In(KartenAnsichten, ansicht) && !KartenAnsichtVorsaetze.Any(vorsatz => text.StartsWith(vorsatz, StringComparison.Ordinal)))
                {
                    p.Add(Prozent("%s: unbekannte Ansicht '%s'", wo, ansicht));
                }
                else if (text.StartsWith("kunde:", StringComparison.Ordinal) && !orteMenge.Contains(((string)ansicht!).Split(':', 2)[1]))
                {
                    p.Add(Prozent("%s: unbekannter Kundenort in '%s'", wo, ansicht));
                }
            }
            var frei = Oder(Get(ort, "frei"), new PyObjekt());
            if (!Py.Wahr(frei))
            {
                p.Add(Prozent("%s: Freischaltung ('frei') fehlt", wo));
            }
            foreach (var k in Schluessel(frei))
            {
                if (!In(KartenFreischaltungen, k))
                {
                    p.Add(Prozent("%s: unbekannte Freischaltung '%s'", wo, k));
                }
            }
            if (Py.Enthaelt(frei, "kundenort") && !orteMenge.Contains(Objekt(frei)["kundenort"]))
            {
                p.Add(Prozent("%s: unbekannter Kundenort '%s'", wo, Objekt(frei)["kundenort"]));
            }
            if (Py.Enthaelt(frei, "aufgabe") && !auftraege.Contains(Objekt(frei)["aufgabe"]))
            {
                p.Add(Prozent("%s: unbekannte Aufgabe '%s'", wo, Objekt(frei)["aufgabe"]));
            }
        }
        // Python geht hier eine Menge durch (Reihenfolge je Lauf anders, Hash-Zufall); C# nimmt sie sortiert,
        // der Erzeuger sortiert diese Meldungen ebenso
        foreach (var kunde in kundenorte.OrderBy(k => Py.Str(k), StringComparer.Ordinal))
        {
            if (!orte.Any(o => Py.Gleich(Get(O(o, "frei"), "kundenort"), kunde)))
            {
                p.Add(Prozent("Spiel-Karte: Kundenort '%s' fehlt auf der Karte", kunde));
            }
        }
        foreach (var (modell, teileWert) in modelle)
        {
            var wo = Prozent("Spiel-Modell %s", modell);
            if (!Py.Wahr(teileWert))
            {
                p.Add(Prozent("%s: keine Teile", wo));
            }
            foreach (var teil in Liste(Oder(teileWert, new List<object?>())).Select(Objekt))
            {
                if (!In(KartenTeile, Get(teil, "art")))
                {
                    p.Add(Prozent("%s: unbekanntes Teil '%s'", wo, Get(teil, "art")));
                    continue;
                }
                if (Py.Gleich(teil["art"], "block"))
                {
                    if (!new[] { "w", "t", "h" }.All(k => IstZahl(Get(teil, k)) && Py.F(teil[k]) > 0))
                    {
                        p.Add(Prozent("%s: Block ohne Groesse", wo));
                    }
                    if (!In(KartenDaecher, Get(teil, "dach", "flach")))
                    {
                        p.Add(Prozent("%s: unbekanntes Dach '%s'", wo, Get(teil, "dach")));
                    }
                }
                foreach (var (k, _) in teil)
                {
                    if (!KartenTeilFelder.Contains(k) && !KartenBedingungen.Contains(k))
                    {
                        p.Add(Prozent("%s: unbekannte Angabe '%s'", wo, k));
                    }
                }
                if (Py.Hat(teil, "raum") && !raeume.Contains(teil["raum"]))
                {
                    p.Add(Prozent("%s: unbekannter Sonderraum '%s'", wo, teil["raum"]));
                }
                foreach (var wohnung in Schluessel(Oder(Get(teil, "wohnung"), new List<object?>())))
                {
                    if (!wohnMenge.Contains(wohnung))
                    {
                        p.Add(Prozent("%s: unbekannte Wohnung '%s'", wo, wohnung));
                    }
                }
                foreach (var k in new[] { "erledigt", "offen" })
                {
                    if (Py.Hat(teil, k) && !auftraege.Contains(teil[k]))
                    {
                        p.Add(Prozent("%s: unbekannte Aufgabe '%s'", wo, teil[k]));
                    }
                }
            }
        }
        // Jede Wohnung braucht ein sichtbares Aussenmodell (Python: Menge, C# sortiert wie der Erzeuger)
        foreach (var ort in orte)
        {
            if (!Py.Gleich(Get(ort, "ansicht"), "zuhause"))
            {
                continue;
            }
            foreach (var wohnung in wohnungen.OrderBy(w => Py.Str(w), StringComparer.Ordinal))
            {
                var teile = GetN(modelle, Get(ort, "modell")) is { } t ? Liste(t).Select(Objekt) : [];
                if (!teile.Any(teil => Py.Gleich(teil["art"], "block") && TeilSichtbarLeer(teil, wohnung)))
                {
                    p.Add(Prozent("Spiel-Karte: kein Gebaeude fuer Wohnung '%s'", wohnung));
                }
            }
        }
        return p;
    }

    // -- Serverfarm -------------------------------------------------------

    /// <summary>_validate_fit_task</summary>
    public static List<string> FitAuftrag(PyObjekt auftrag, PyObjekt inhalt)
    {
        var p = new List<string>();
        var spec = O(auftrag, "bestueckung");
        var vorlagen = Betrieb.FitVorlagen(auftrag);
        if (vorlagen.Count == 0)
        {
            return ["Bestueckung ohne Vorlagen"];
        }
        if (Py.Wahr(Get(spec, "rack")) && Betrieb.FarmRackAufgabe(spec["rack"], inhalt) is null)
        {
            p.Add(Prozent("unbekanntes Rack '%s'", spec["rack"]));
        }
        foreach (var vorlage in vorlagen)
        {
            foreach (var k in new[] { "id", "name", "anzahl", "schaechte", "netzteil_plaetze", "soll" })
            {
                if (!Py.Hat(vorlage, k))
                {
                    p.Add(Prozent("Vorlage %s: Feld '%s' fehlt", Get(vorlage, "id"), k));
                }
            }
            if (Py.Wahr(Get(vorlage, "geraet")) && RackGeraet(vorlage["geraet"], inhalt) is null)
            {
                p.Add(Prozent("Vorlage %s: unbekanntes Geraet", Get(vorlage, "id")));
            }
            foreach (var stufe in L(O(vorlage, "soll"), "raid"))
            {
                if (!In(Betrieb.RaidStufen, stufe))
                {
                    p.Add(Prozent("Vorlage %s: unbekanntes RAID '%s'", Get(vorlage, "id"), stufe));
                }
            }
        }
        if (p.Count == 0 && Betrieb.FitLoesung(auftrag, inhalt) is null)
        {
            p.Add("Bestueckung hat keine richtige Loesung");
        }
        return p;
    }

    /// <summary>_validate_farm</summary>
    public static List<string> Farm(PyObjekt inhalt, Lerninhalte lernen, SpielThemen themen)
    {
        var daten = O(inhalt, "serverfarm");
        if (daten.Count == 0)
        {
            return [];
        }
        var p = new List<string>();
        var regeln = Betrieb.FarmRegeln(inhalt);
        foreach (var k in new[] { "freischaltung", "tage", "zuschlag_max", "anzahlung", "fehler", "verzug", "kabelfehler", "strafe", "bonus" })
        {
            if (!Py.Hat(regeln, k))
            {
                p.Add(Prozent("Spiel-Balancing serverfarm: '%s' fehlt", k));
            }
        }
        var frei = O(regeln, "freischaltung");
        if (Py.Wahr(Get(frei, "raum")) && Betrieb.SonderRaum(frei["raum"], inhalt) is null)
        {
            p.Add(Prozent("Spiel-Serverfarm: unbekannter Sonderraum '%s'", frei["raum"]));
        }
        foreach (var zert in L(frei, "zertifizierungen"))
        {
            if (Wirtschaft.Zertifikat(zert, inhalt) is null)
            {
                p.Add(Prozent("Spiel-Serverfarm: unbekannte Zertifizierung '%s'", zert));
            }
        }
        if (Math.Abs(Py.F(Py.Summe(L(regeln, "verzug").Select(z => Liste(z)[0]))) - 1.0) > 0.001)
        {
            p.Add("Spiel-Serverfarm: Anteile des Lieferverzugs ergeben nicht 1");
        }
        var personen = new PySet(L(daten, "personen").Select(x => Objekt(x)["id"]));
        foreach (var person in L(daten, "personen").Select(Objekt))
        {
            foreach (var (teil, wert) in O(person, "aussehen"))
            {
                var wahl = Aussehen.Teile.FirstOrDefault(t => t.Teil == teil).Wahl;
                if (wahl is null || !wahl.Any(w => Py.Gleich(w.Kennung, wert)))
                {
                    p.Add(Prozent("Spiel-Serverfarm: %s hat ungueltiges Aussehen %s=%s", person["id"], teil, wert));
                }
            }
        }
        var phasenListe = L(daten, "phasen").Select(Objekt).ToList();
        var phasen = phasenListe.Select(ph => ph["id"]).ToList();
        if (Doppelt(phasen) || phasen.Count == 0)
        {
            p.Add("Spiel-Serverfarm: Phasen fehlen oder sind doppelt");
        }
        foreach (var k in new[] { Betrieb.FarmPlanung, Betrieb.FarmLieferung, Betrieb.FarmEinbau })
        {
            if (!phasen.Any(ph => Py.Gleich(ph, k)))
            {
                p.Add(Prozent("Spiel-Serverfarm: Phase '%s' fehlt", k));
            }
        }
        var tage = phasenListe.Select(ph => Get(ph, "soll_tag", 0L)).ToList();
        if (!Steigend(tage) || (tage.Count > 0 && !Py.Gleich(tage[^1], Get(regeln, "tage"))))
        {
            p.Add("Spiel-Serverfarm: soll_tag muss steigen und mit 'tage' enden");
        }
        foreach (var phase in phasenListe)
        {
            foreach (var thema in L(phase, "themen"))
            {
                if (!(thema is string t && themen.FachbereichVon.ContainsKey(t)))
                {
                    p.Add(Prozent("Spiel-Serverfarm: Phase %s: unbekanntes Thema '%s'", phase["id"], thema));
                }
            }
            if (Py.Wahr(Get(phase, "team_punkte")) && !Py.Wahr(Get(phase, "themen")))
            {
                p.Add(Prozent("Spiel-Serverfarm: Phase %s hat Team-Punkte ohne Themen", phase["id"]));
            }
            if (Betrieb.FarmAufgaben(phase["id"], inhalt).Count == 0)
            {
                p.Add(Prozent("Spiel-Serverfarm: Phase %s ohne Aufgaben", phase["id"]));
            }
        }
        foreach (var art in Betrieb.FitArten)
        {
            if (Betrieb.FitTeile(art, inhalt).Count == 0)
            {
                p.Add(Prozent("Spiel-Serverfarm: keine Teile der Art '%s'", art));
            }
        }
        var gesehen = new PySet();
        foreach (var teil in L(daten, "teile").Select(Objekt))
        {
            if (!In(Betrieb.FitArten, Get(teil, "typ")) || gesehen.Contains(Get(teil, "id")) || !(Py.F(Get(teil, "preis", 0L)) > 0))
            {
                p.Add(Prozent("Spiel-Serverfarm: Teil %s ungueltig", Get(teil, "id")));
            }
            gesehen.Add(Get(teil, "id"));
        }
        gesehen = [];
        foreach (var auftrag in L(daten, "aufgaben").Select(Objekt))
        {
            var wo = Prozent("Spiel-Serverfarm-Aufgabe %s", Get(auftrag, "id"));
            foreach (var feld in new[] { "id", "typ", "phase", "person", "cat", "titel", "hilfe", "erklaerung", "suchbegriffe" })
            {
                if (!Py.Wahr(Get(auftrag, feld)))
                {
                    p.Add(Prozent("%s: Feld '%s' fehlt", wo, feld));
                }
            }
            if (!Py.Gleich(Get(auftrag, "typ"), "terminal") && !Py.Wahr(Get(auftrag, "ticket")))
            {
                p.Add(Prozent("%s: Feld 'ticket' fehlt", wo));
            }
            if (gesehen.Contains(Get(auftrag, "id")))
            {
                p.Add(Prozent("%s: Kennung doppelt", wo));
            }
            gesehen.Add(Get(auftrag, "id"));
            if (!In(FarmTypen, Get(auftrag, "typ")))
            {
                p.Add(Prozent("%s: unbekannter Typ '%s'", wo, Get(auftrag, "typ")));
                continue;
            }
            if (!phasen.Any(ph => Py.Gleich(ph, Get(auftrag, "phase"))))
            {
                p.Add(Prozent("%s: unbekannte Phase", wo));
            }
            if (!personen.Contains(Get(auftrag, "person")))
            {
                p.Add(Prozent("%s: unbekannte Person '%s'", wo, Get(auftrag, "person")));
            }
            if (!(Get(auftrag, "cat") is string cat && Fachbereiche.Schluessel.ContainsKey(cat)))
            {
                p.Add(Prozent("%s: unbekannter Fachbereich", wo));
            }
            foreach (var (k, wert) in O(auftrag, "anforderungen"))
            {
                if (!themen.FachbereichVon.ContainsKey(k) || !Kette(0L, wert, 100L))
                {
                    p.Add(Prozent("%s: ungueltige Anforderung %s", wo, k));
                }
            }
            if (Laenge(Get(auftrag, "ticket", "")) > MaxTicket)
            {
                p.Add(Prozent("%s: Tickettext zu lang", wo));
            }
            var typ = (string)auftrag["typ"]!;
            var eigene = typ switch
            {
                "bauteile" or "bestellung" => HardwareAuftrag(auftrag, inhalt),
                "rack" => RackAuftrag(auftrag, inhalt),
                "formular" => FormAuftrag(auftrag, inhalt),
                "terminal" => TerminalAuftrag(auftrag, inhalt),
                "diagnose" => DiagnoseAuftrag(auftrag, inhalt),
                "bestueckung" => FitAuftrag(auftrag, inhalt),
                _ => [],
            };
            p.AddRange(eigene.Select(t => wo + ": " + t));
            List<object?> texte = [];
            if (typ == "auswahl")
            {
                var optionen = L(auftrag, "optionen");
                if (optionen.Count < 2 || Doppelt(optionen) || !optionen.Any(o => Py.Gleich(o, Get(auftrag, "antwort"))))
                {
                    p.Add(Prozent("%s: Antworten ungueltig", wo));
                }
                texte = optionen;
            }
            else if (typ == "zuordnung")
            {
                var paare = L(auftrag, "paare").Select(Liste).ToList();
                var links = paare.Select(x => x[0]).ToList();
                var rechts = paare.Select(x => x[1]).ToList();
                if (paare.Count < 2 || Doppelt(links) || Doppelt(rechts))
                {
                    p.Add(Prozent("%s: braucht mindestens 2 eindeutige Paare", wo));
                }
                texte = [.. links, .. rechts];
            }
            foreach (var text in texte)
            {
                if (Laenge(text) > MaxOption)
                {
                    p.Add(Prozent("%s: Antwort zu lang (Handy): %s", wo, Anfang(text)));
                }
            }
            if (typ == Betrieb.FarmAngebotTyp && (!Py.Wahr(Get(daten, "angebot")) || !Py.Wahr(Get(Objekt(daten["angebot"]), "positionen"))))
            {
                p.Add(Prozent("%s: Angebot ohne Positionen", wo));
            }
            foreach (var begriff in L(auftrag, "suchbegriffe"))
            {
                if (!FindetLernstoff(Py.Str(begriff), lernen))
                {
                    p.Add(Prozent("%s: Suchbegriff '%s' findet nichts", wo, begriff));
                }
            }
        }
        foreach (var auftrag in Betrieb.FarmAufgaben(null, inhalt))
        {
            if (!Py.Gleich(auftrag["typ"], "bestueckung"))
            {
                continue;
            }
            foreach (var vorlage in Betrieb.FitVorlagen(auftrag))
            {
                var rack = Betrieb.FarmRackAufgabe(Get(O(auftrag, "bestueckung"), "rack"), inhalt);
                if (rack is not null && Py.Wahr(Get(vorlage, "geraet"))
                    && Liste(rack["geraete"]).Count(g => Py.Gleich(g, vorlage["geraet"])) < Py.F(Get(vorlage, "anzahl", 1L)))
                {
                    p.Add(Prozent("Spiel-Serverfarm: Rack hat zu wenige '%s' fuer die Bestueckung", vorlage["geraet"]));
                }
            }
        }
        return p;
    }
}
