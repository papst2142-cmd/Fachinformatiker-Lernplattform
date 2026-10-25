using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W5 (Weisung P7-10): die Orte mit Innenraum wie <c>fisi_game.py</c> (Python 0.63.2):
/// _deco_visible/_visible_building, people_at_site, firm_people, site_content (nur Grundriss und
/// Personen), home_building, at_entrance, place_message, office_message und warehouse_summary.
/// Die 3D-Innenraeume zeichnen genau diesen Grundriss; Wege und Texte sind dieselben wie in den
/// 2D-Ansichten der Python-Linie. Vergleichswerte G49 (<c>spiel_orte.json</c>).
/// </summary>
public static class SpielOrte
{
    /// <summary>DECISIONS_OPEN_TEXT</summary>
    public const string EntscheidungenOffen = "Erst die offenen Entscheidungen zum Personal treffen.";

    /// <summary>ACTION_PERSONAL</summary>
    public const string AktionPersonal = "personal";

    /// <summary>_deco_visible: erst nach ab_aufgabe, nur bis bis_aufgabe.</summary>
    public static bool DekoSichtbar(PyObjekt deko, PySet geloest) =>
        !(Py.Wahr(Get(deko, "ab_aufgabe")) && !geloest.Contains(deko["ab_aufgabe"]))
        && !(Py.Wahr(Get(deko, "bis_aufgabe")) && geloest.Contains(deko["bis_aufgabe"]));

    /// <summary>_visible_building: Grundriss mit der gerade sichtbaren Einrichtung.</summary>
    public static PyObjekt SichtbaresGebaeude(PyObjekt gebaeude, PySet geloest)
    {
        var deko = Liste(gebaeude["raeume"]).Select(Objekt).SelectMany(r => ListeOderLeer(r, "deko"))
            .Concat(ListeOderLeer(Get(gebaeude, "flur") as PyObjekt ?? [], "deko")).Select(Objekt).ToList();
        if (!deko.Any(d => Py.Wahr(Get(d, "ab_aufgabe")) || Py.Wahr(Get(d, "bis_aufgabe"))))
        {
            return gebaeude;
        }
        var ergebnis = Objekt(Py.Kopie(gebaeude));
        foreach (var raum in Liste(ergebnis["raeume"]).Select(Objekt))
        {
            Py.Setzen(raum, "deko", ListeOderLeer(raum, "deko").Where(d => DekoSichtbar(Objekt(d), geloest)).ToList());
        }
        if (Get(ergebnis, "flur") is PyObjekt flur)
        {
            Py.Setzen(flur, "deko", ListeOderLeer(flur, "deko").Where(d => DekoSichtbar(Objekt(d), geloest)).ToList());
        }
        return ergebnis;
    }

    /// <summary>firm_people: die Mitarbeiter am Standort als Personen (ohne Kranke und Urlauber).</summary>
    public static List<PyObjekt> FirmenPersonen(Spielstand stand, string ort)
    {
        var ergebnis = new List<PyObjekt>();
        var tickets = stand.KundenTickets();
        foreach (var e in stand.PersonalListe())
        {
            if (!Py.Wahr(Get(e, "platz")) || Py.Wahr(Get(e, "abwesend")) || !Py.Gleich(Get(e, "standort"), ort))
            {
                continue;
            }
            var platz = Liste(e["platz"]);
            var weiterbildung = Get(e, "weiterbildung");
            var ticket = tickets.FirstOrDefault(t => Py.Gleich(Get(t, "an"), e["id"]));
            var projekt = Py.Wahr(Get(e, "projekt")) ? stand.Projekte.Get(e["projekt"]) as PyObjekt : null;
            ergebnis.Add(new PyObjekt
            {
                { "id", e["id"] }, { "name", e["name"] }, { "rolle", e["rolle"] }, { "kundenticket", ticket },
                { "projekt_text", projekt is not null ? Betrieb.ProjektBueroText(stand, projekt, stand.Inhalt) : "" },
                { "raum", platz[2] }, { "platz", new List<object?> { Py.F(platz[0]), Py.F(platz[1]) } },
                { "aussehen", Get(e, "aussehen") }, { "macke", Get(e, "macke", "") },
                { "mitarbeiter", true }, { "umsatz", e["umsatz"] },
                { "in_weiterbildung", Py.Wahr(weiterbildung) },
                { "im_coaching", Py.Wahr(weiterbildung) && Py.Gleich(Get(Objekt(weiterbildung), "art"), Betrieb.Coaching) },
            });
        }
        return ergebnis;
    }

    /// <summary>people_at_site: Personen am Ort mit ihrem Platz; wer einen offenen Auftrag mit "stelle" hat, wartet dort.</summary>
    public static List<PyObjekt> Personen(string ort, Spielstand stand)
    {
        if (ort == OrtBuero && Py.Wahr(stand.Firma))
        {
            return FirmenPersonen(stand, OrtBuero);
        }
        if (ort == OrtFiliale)
        {
            return FirmenPersonen(stand, OrtFiliale);
        }
        var wartet = new Dictionary<string, (object? Ort, object? Stelle)>();
        foreach (var auftrag in stand.OffeneTickets())
        {
            if (Py.Wahr(Get(auftrag, "stelle")) && !wartet.ContainsKey(Py.Str(auftrag["auftraggeber"])))
            {
                wartet[Py.Str(auftrag["auftraggeber"])] = (AuftragOrt(auftrag, stand.Inhalt), auftrag["stelle"]);
            }
        }
        var ergebnis = new List<PyObjekt>();
        foreach (var person in AllePersonen(stand.Inhalt))
        {
            if (!PersonDa(person, stand.Tag))
            {
                continue;
            }
            var (wo, stelle) = wartet.TryGetValue(Py.Str(person["id"]), out var w) ? w : (Get(person, "ort", OrtBuero), Get(person, "platz"));
            if (Py.Gleich(wo, ort) && Py.Wahr(stelle))
            {
                var kopie = new PyObjekt();
                foreach (var (k, v) in person)
                {
                    kopie.Add(k, v);
                }
                var s = Liste(stelle);
                Py.Setzen(kopie, "platz", new List<object?> { Py.F(s[0]), Py.F(s[1]) });
                ergebnis.Add(kopie);
            }
        }
        return ergebnis;
    }

    /// <summary>site_content(ort, stand)["gebaeude"]: Grundriss des Ortes mit der sichtbaren Einrichtung.</summary>
    public static PyObjekt Gebaeude(string ort, Spielstand stand)
    {
        if (ort == OrtZuhause)
        {
            return WohnungsGrundriss(stand);
        }
        PyObjekt basis;
        if (ort == OrtBuero)
        {
            basis = Py.Wahr(stand.Firma) ? Betrieb.FirmenGebaeude(stand, stand.Inhalt) : Objekt(stand.Inhalt["gebaeude"]);
        }
        else if (ort == OrtFiliale)
        {
            basis = Betrieb.FilialGebaeude(stand) ?? throw new SpielFehler("Die Filiale ist noch nicht eröffnet.");
        }
        else
        {
            var kundenort = Kundenorte(stand.Inhalt).FirstOrDefault(o => Py.Gleich(o["id"], ort)) ?? throw new SpielFehler("Unbekannter Ort: " + ort);
            basis = Objekt(kundenort["gebaeude"]);
        }
        return SichtbaresGebaeude(basis, stand.Geloest);
    }

    /// <summary>_inside: liegt die Einrichtung ganz in der Flaeche?</summary>
    private static bool Innen(PyObjekt d, PyObjekt f) =>
        Py.F(f["x"]) <= Py.F(d["x"]) && Py.F(d["x"]) + Py.F(d["w"]) <= Py.F(f["x"]) + Py.F(f["w"])
        && Py.F(f["y"]) <= Py.F(d["y"]) && Py.F(d["y"]) + Py.F(d["h"]) <= Py.F(f["y"]) + Py.F(f["h"]);

    /// <summary>
    /// home_building: Grundriss der Wohnung mit Boeden und Moebeln aus der Einrichtung (Teppiche zuerst,
    /// damit sie unter den Moebeln liegen), dazu der Kartonstapel, solange etwas im Karton ist.
    /// <paramref name="einrichtung"/>/<paramref name="karton"/>: angenommene Einrichtung fuer die Vorschau.
    /// </summary>
    public static PyObjekt WohnungsGrundriss(Spielstand stand, PyObjekt? einrichtung = null, int? karton = null)
    {
        var bleibe = Wohnung.Bleibe(stand.Wohnung, stand.Inhalt)!;
        einrichtung ??= Wohnung.Einrichtung(stand);
        karton ??= Wohnung.ImKarton(stand, einrichtung).Count;
        var gebaeude = Objekt(Py.Kopie(bleibe["gebaeude"]));
        var boeden = Objekt(einrichtung["boeden"]);
        foreach (var raum in Liste(gebaeude["raeume"]).Select(Objekt))
        {
            var boden = Get(boeden, Py.Str(raum["id"])) is List<object?> b ? b : [null, null];
            if (boden[0] is string art && Wohnung.BodenArten.Contains(art))
            {
                Py.Setzen(raum, "boden", art);
            }
            // "bodenfarbe" setzt Python als Hexwert aus dem Farbthema (floor_color); die 3D-Darstellung
            // liest die Farbe selbst aus der Einrichtung (boeden), der Grundriss bleibt ohne sie
        }
        var flur = Objekt(gebaeude["flur"]);
        var raeume = Liste(gebaeude["raeume"]).Select(Objekt).ToList();
        void Ablegen(PyObjekt deko)
        {
            var ziel = raeume.FirstOrDefault(r => Innen(deko, r));
            var halter = ziel ?? flur;
            if (Get(halter, "deko") is not List<object?> liste)
            {
                liste = [];
                Py.Setzen(halter, "deko", liste);
            }
            liste.Add(deko);
        }
        // sorted(..., key=typ not in WALK_FREE) ist stabil: begehbare Stuecke zuerst
        var moebel = Wohnung.AufgestellteMoebel(stand, einrichtung);
        foreach (var m in moebel.Where(m => Wohnung.Begehbar.Contains(m.Typ)).Concat(moebel.Where(m => !Wohnung.Begehbar.Contains(m.Typ))))
        {
            Ablegen(new PyObjekt
            {
                { "typ", m.Typ }, { "x", m.X }, { "y", m.Y }, { "w", m.W }, { "h", m.H }, { "dreh", m.Dreh },
                { "stueck", m.Stueck }, { "moebel", m.Moebel }, { "blick", m.Blick },
            });
        }
        if (karton > 0 && Py.Wahr(Get(bleibe, "kartons")))
        {
            var k = Liste(bleibe["kartons"]);
            Ablegen(new PyObjekt { { "typ", "kartons" }, { "x", k[0] }, { "y", k[1] }, { "w", k[2] }, { "h", k[3] }, { "umzug", true } });
        }
        return gebaeude;
    }

    /// <summary>at_entrance: steht die Figur an der Eingangstuer (links im Flur)?</summary>
    public static bool AmEingang(double x, double y, PyObjekt gebaeude) => Wohnung.AmEingang(x, y, gebaeude);

    /// <summary>warehouse_summary: Lagerstand in einem Satz (fuer den Lageristen im Buero).</summary>
    public static string LagerSatz(Spielstand stand)
    {
        var lager = stand.Lagerhaus();
        var teile = new List<string>();
        var anzahl = ((System.Collections.IEnumerable)lager["bestand"]!).Cast<PyTupel>().Sum(z => Py.Int(z[1]));
        if (anzahl != 0)
        {
            teile.Add(Prozent("Auf Lager: %d Teil%s.", anzahl, anzahl == 1 ? "" : "e"));
        }
        if (((System.Collections.IEnumerable)lager["unterwegs"]!).Cast<object?>().ToList() is { Count: > 0 } unterwegs)
        {
            var erste = Objekt(unterwegs[0]);
            teile.Add(Prozent("Nächste Lieferung: %s an Arbeitstag %d.", Teil(erste["teil"], stand.Inhalt)!["name"], erste["ankunft"]));
        }
        return teile.Count > 0 ? string.Join(" ", teile) : "Das Lager ist leer, es ist nichts bestellt.";
    }

    /// <summary>office_message: (Ueberschrift, Text) unter der Grossansicht.</summary>
    public static (string Titel, string Text) BueroNachricht(double x, double y, PyObjekt? person, PyDict auftraege, PyObjekt gebaeude, Spielstand stand)
    {
        if (person is not null)
        {
            var erster = Betrieb.KurzName(person["name"]);
            var aufgaben = auftraege.Get(person["id"]) is List<object?> l ? l : [];
            if (aufgaben.Count > 0)
            {
                var mehr = aufgaben.Count > 1 ? Prozent(" (und %d weitere)", aufgaben.Count - 1) : "";
                var a = Objekt(aufgaben[0]);
                return (Prozent("%s hat einen Auftrag für dich", erster), Prozent("„%s“ · Priorität %s%s", a["titel"], a["prioritaet"], mehr));
            }
            var kopf = Prozent("%s · %s", person["name"], person["rolle"]);
            if (Py.Wahr(Get(person, "mitarbeiter")) && Py.Wahr(Get(person, "projekt_text")))
            {
                return (kopf, Prozent("%s Heute keine Routineaufträge, der Tag gehört dem Projekt.", person["projekt_text"]));
            }
            if (Py.Wahr(Get(person, "mitarbeiter")) && Py.Wahr(Get(person, "kundenticket")))
            {
                var t = Objekt(person["kundenticket"]);
                return (kopf, Prozent("Sitzt heute an „%s“ für %s (Chance %d %%). Dazu Routineaufträge: %s Umsatz pro Arbeitstag.",
                    t["titel"], Objekt(t["kunde"])["name"], t["chance"], Wohnung.GanzeEuro(Get(person, "umsatz", 0L))));
            }
            if (Py.Wahr(Get(person, "mitarbeiter")))
            {
                return (kopf, Py.Wahr(Get(person, "in_weiterbildung"))
                    ? (Py.Wahr(Get(person, "im_coaching")) ? "Im Coaching, heute kein Umsatz." : "In der Weiterbildung, heute kein Umsatz.")
                    : Prozent("Kümmert sich um Routineaufträge: %s Umsatz pro Arbeitstag. %s", Wohnung.GanzeEuro(Get(person, "umsatz", 0L)), Get(person, "macke", "")));
            }
            if (Py.Wahr(Get(person, "lagerist")))
            {
                return (kopf, "Gerade nichts für dich. " + LagerSatz(stand));
            }
            return (kopf, Prozent("Gerade nichts für dich. %s", Get(person, "macke", "")));
        }
        var raum = Wege.RaumBei(x, y, gebaeude);
        var wartend = auftraege.Values.Sum(v => Liste(v).Count);
        var wo = raum is not null ? Prozent("Du bist im Raum %s.", raum["name"]) : "Du bist im Flur.";
        if (wartend != 0)
        {
            var hinweis = wartend == 1 ? Prozent("%d Auftrag wartet.", wartend) : Prozent("%d Aufträge warten.", wartend);
            return (wo, hinweis + " Wer einen hat, trägt ein grünes „!“.");
        }
        if (Py.Wahr(stand.Firma))
        {
            return (wo, "Deine Leute kümmern sich um die Routineaufträge. Feierabend machst du an der Eingangstür.");
        }
        return (wo, "Heute wartet kein Auftrag mehr.");
    }

    /// <summary>
    /// place_message: Text und Knoepfe unter der Ansicht eines Ortes (Ueberschrift, Text, [(aktion, Beschriftung)]).
    /// Aktionen: "auftrag:&lt;id&gt;", "feierabend", "buero", "zuhause", "kunde", "lernen", "schlafen", "personal".
    /// </summary>
    public static (string Titel, string Text, List<(string Aktion, string Name)> Knoepfe) Nachricht(string ort, double x, double y, PyObjekt? person, Spielstand stand, PyObjekt? gebaeude = null)
    {
        if (ort == OrtZuhause)
        {
            return Wohnung.Nachricht(x, y, stand);
        }
        gebaeude ??= Gebaeude(ort, stand);
        var auftraege = stand.Auftraege(ort);
        var knoepfe = new List<(string, string)>();
        if (person is not null)
        {
            var (titel, text) = BueroNachricht(x, y, person, auftraege, gebaeude, stand);
            if (auftraege.Get(person["id"]) is List<object?> { Count: > 0 } aufgaben)
            {
                knoepfe.Add(("auftrag:" + Py.Str(Objekt(aufgaben[0])["id"]), "Auftrag annehmen"));
            }
            return (titel, text, knoepfe);
        }
        if (AmEingang(x, y, gebaeude))
        {
            if (ort == OrtBuero)
            {
                if (stand.FeierabendMoeglich())
                {
                    return ("Eingangstür", "Für heute ist alles erledigt. Zeit für den Feierabend!", [("feierabend", "Feierabend machen")]);
                }
                if (Py.Wahr(stand.Firma))
                {
                    return ("Eingangstür", EntscheidungenOffen, [(AktionPersonal, "Zu den Entscheidungen")]);
                }
                var offen = stand.OffeneTickets().Count;
                return ("Eingangstür", Prozent("Noch nicht: %s offen.", offen == 1 ? "1 Ticket ist" : Prozent("%d Tickets sind", offen)), []);
            }
            if (ort == OrtFiliale)
            {
                return ("Ausgang", "Von hier geht es zurück zum Gewerbehof Am Stellwerk.", [("buero", "Zum Gewerbehof")]);
            }
            return ("Ausgang", "Hier geht es zurück ins Büro.", [("buero", "Zurück ins Büro")]);
        }
        var (t, s) = BueroNachricht(x, y, null, auftraege, gebaeude, stand);
        return (t, s, knoepfe);
    }
}
