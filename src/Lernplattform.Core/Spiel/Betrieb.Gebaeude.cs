using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: die eigene Firma als Modulfunktionen von <c>fisi_game.py</c> (Python 0.63.2), Teil
/// Gebaeude (Z. 7230-7560): Sonderraeume, Filiale, Gruendung, Kassenbuch und Tageszeilen.
/// </summary>
public static partial class Betrieb
{
    /// <summary>AXES: Schluessel -> Anzeigename der vier Achsen der Reputation.</summary>
    public static readonly IReadOnlyDictionary<string, string> AchsenName = new Dictionary<string, string>
    {
        ["fachkompetenz"] = "Fachkompetenz", ["zuverlaessigkeit"] = "Zuverlässigkeit",
        ["kundenzufriedenheit"] = "Kundenzufriedenheit", ["sicherheit"] = "Sicherheitsbewusstsein",
    };

    /// <summary>room_rules: firma.json "sonderraeume".</summary>
    public static PyObjekt RaumRegeln(PyObjekt inhalt) => OderLeer(FirmaRegeln(inhalt)["sonderraeume"]);

    /// <summary>special_rooms</summary>
    public static List<PyObjekt> SonderRaeume(PyObjekt inhalt) => [.. ListeOderLeer(RaumRegeln(inhalt), "raeume").Select(Objekt)];

    /// <summary>special_room</summary>
    public static PyObjekt? SonderRaum(object? raum, PyObjekt inhalt) =>
        SonderRaeume(inhalt).FirstOrDefault(r => Py.Gleich(r["id"], raum));

    /// <summary>room_status: die Sonderraeume fuer die Oberflaeche (problem leer = jetzt ausbaubar).</summary>
    public static List<PyObjekt> RaumStand(Spielstand stand, PyObjekt inhalt)
    {
        var stufe = Py.Wahr(stand.Firma) ? Py.Int(Objekt(stand.Firma)["stufe"]) : 0;
        var ergebnis = new List<PyObjekt>();
        foreach (var regel in SonderRaeume(inhalt))
        {
            var eintrag = new PyObjekt();
            foreach (var k in new[] { "id", "name", "vorteil", "preis", "nebenkosten", "ab_stufe" })
            {
                eintrag.Add(k, Py.Hat(regel, k) ? regel[k] : throw new KeyNotFoundException(k));
            }
            var gebaut = stand.HatRaum(regel["id"]);
            eintrag.Add("gebaut", gebaut);
            string problem;
            if (gebaut)
            {
                problem = "Schon ausgebaut.";
            }
            else if (stufe < Py.Int(regel["ab_stufe"]))
            {
                problem = Prozent("Erst ab Ausbaustufe %d möglich.", regel["ab_stufe"]);
            }
            else if (stand.Geld < Py.Int(regel["preis"]))
            {
                problem = Prozent("Dafür reicht dein Geld noch nicht (%s fehlen).",
                    Wohnung.GanzeEuro(Py.Int(regel["preis"]) - Math.Max(0, stand.Geld)));
            }
            else
            {
                problem = "";
            }
            eintrag.Add("problem", problem);
            ergebnis.Add(eintrag);
        }
        return ergebnis;
    }

    /// <summary>room_status_text</summary>
    public static string RaumStandText(PyObjekt eintrag) => Py.Wahr(eintrag["gebaut"])
        ? Prozent("Nebenkosten +%s pro Arbeitstag", Wohnung.GanzeEuro(eintrag["nebenkosten"]))
        : Prozent("%s · Nebenkosten +%s pro Arbeitstag", Wohnung.GanzeEuro(eintrag["preis"]), Wohnung.GanzeEuro(eintrag["nebenkosten"]));

    /// <summary>firm_building: Grundriss der Ausbaustufe; nicht ausgebaute Sonderraeume sind Leerstand.</summary>
    public static PyObjekt FirmenGebaeude(Spielstand stand, PyObjekt inhalt)
    {
        var gebaeude = Objekt(stand.FirmenStufe()["gebaeude"]);
        var regeln = RaumRegeln(inhalt);
        var leer = new Dictionary<string, PyObjekt>();
        foreach (var regel in ListeOderLeer(regeln, "raeume").Select(Objekt))
        {
            if (!stand.HatRaum(regel["id"]))
            {
                leer[Py.Str(regel["raum"])] = regel;
            }
        }
        var raeume = Liste(gebaeude["raeume"]).Select(Objekt).ToList();
        if (leer.Count == 0 || !raeume.Any(r => r["id"] is string id && leer.ContainsKey(id)))
        {
            return gebaeude;
        }
        var aussehen = OderLeer(regeln["leerstand"]);
        var ergebnis = Objekt(Py.Kopie(gebaeude));
        foreach (var raum in Liste(ergebnis["raeume"]).Select(Objekt))
        {
            if (raum["id"] is not string id || !leer.TryGetValue(id, out var regel))
            {
                continue;
            }
            Py.Setzen(raum, "name", Get(aussehen, "name", "Leerstand"));
            Py.Setzen(raum, "kurz", Get(aussehen, "kurz", "Frei"));
            Py.Setzen(raum, "farbe", Get(aussehen, "farbe", "#8B93A1"));
            Py.Setzen(raum, "boden", Get(aussehen, "boden", "beton"));
            Py.Setzen(raum, "deko", new List<object?>());
            Py.Setzen(raum, "leerstand", regel["id"]);
            Py.Setzen(raum, "text", Prozent(Py.Str(Get(aussehen, "text", "Hier ist Platz für den Sonderraum „%s“.")), regel["name"])
                + " " + Py.Str(regel["vorteil"]));
        }
        return ergebnis;
    }

    /// <summary>branch_rules</summary>
    public static PyObjekt FilialRegeln(PyObjekt inhalt) => OderLeer(FirmaRegeln(inhalt)["filiale"]);

    /// <summary>branch_stage: Stufe der Filiale (1 = Eroeffnung) oder null.</summary>
    public static PyObjekt? FilialStufe(object? nummer, PyObjekt inhalt)
    {
        var n = Py.Wahr(nummer) ? Py.Int(nummer) : 0;
        return ListeOderLeer(FilialRegeln(inhalt), "stufen").Select(Objekt)
            .FirstOrDefault(s => Py.Int(Get(s, "stufe", 0L)) == n);
    }

    /// <summary>branch_missing: was fuer die Eroeffnung der Filiale noch fehlt (ohne das Geld).</summary>
    public static List<string> FilialeFehlt(Spielstand stand, PyObjekt inhalt)
    {
        var regeln = FilialRegeln(inhalt);
        var braucht = OderLeer(regeln["voraussetzung"]);
        if (regeln.Count == 0 || !Py.Wahr(stand.Firma))
        {
            return ["eine eigene Firma"];
        }
        var firma = Objekt(stand.Firma);
        var fehlt = new List<string>();
        if (Py.Int(firma["stufe"]) < Py.Int(Get(braucht, "stufe", 0L)))
        {
            var stufe = Liste(Objekt(FirmaRegeln(inhalt)["gebaeude"])["stufen"]).Select(Objekt)
                .FirstOrDefault(s => Py.Gleich(s["stufe"], Py.Int(braucht["stufe"]))) ?? [];
            fehlt.Add(Prozent("Gewerbehof mindestens Stufe %d (%s)", braucht["stufe"], Get(stufe, "name", "")));
        }
        if (stand.Personal.Count < Py.Int(Get(braucht, "mitarbeiter", 0L)))
        {
            fehlt.Add(Prozent("mindestens %d Mitarbeiter (jetzt %d)", braucht["mitarbeiter"], (long)stand.Personal.Count));
        }
        var alter = stand.Tag - Py.Int(firma["tag"]);
        if (alter < Py.Int(Get(braucht, "firmentage", 0L)))
        {
            fehlt.Add(Prozent("Firma mindestens %d Arbeitstage alt (jetzt %d)", braucht["firmentage"], alter));
        }
        return fehlt;
    }

    /// <summary>branch_possible: steht die Filiale auf der Karte?</summary>
    public static bool FilialeMoeglich(Spielstand? stand, PyObjekt inhalt) =>
        stand is not null && Py.Wahr(stand.Firma) && (Py.Wahr(stand.Zweigstelle) || FilialeFehlt(stand, inhalt).Count == 0);

    /// <summary>branch_inquiries: zusaetzliche Kundenanfragen pro Tag durch die Filiale.</summary>
    public static long FilialAnfragen(Spielstand stand, PyObjekt inhalt) =>
        Py.Wahr(stand.Zweigstelle) ? Py.Int(Get(FilialRegeln(inhalt), "anfragen_plus", 0L)) : 0;

    /// <summary>branch_nearness: Preisvorteil in Prozentpunkten bei Kunden nahe der Filiale.</summary>
    public static long FilialNaehe(Spielstand stand, object? kunde, PyObjekt inhalt)
    {
        if (!Py.Wahr(stand.Zweigstelle) || !Py.Wahr(kunde))
        {
            return 0;
        }
        var regel = OderLeer(FilialRegeln(inhalt)["naehe"]);
        var eintrag = FirmaKunde(kunde, inhalt) ?? [];
        return Py.Wahr(eintrag["ort"]) && Py.Enthaelt(Py.Wahr(regel["orte"]) ? regel["orte"] : new List<object?>(), eintrag["ort"])
            ? Py.Int(Get(regel, "vorteil", 0L)) : 0;
    }

    /// <summary>branch_building</summary>
    public static PyObjekt? FilialGebaeude(Spielstand stand) => stand.FilialStufe() is PyObjekt s ? Objekt(s["gebaeude"]) : null;

    /// <summary>branch_status: die Filiale fuer die Oberflaeche.</summary>
    public static PyObjekt FilialStand(Spielstand stand, PyObjekt inhalt)
    {
        var regeln = FilialRegeln(inhalt);
        var naechste = stand.NaechsteFilialStufe();
        var offen = Py.Wahr(stand.Zweigstelle);
        var fehlt = offen ? [] : FilialeFehlt(stand, inhalt);
        var eintrag = new PyObjekt
        {
            { "offen", offen },
            { "name", offen ? Objekt(stand.Zweigstelle)["name"] : Get(regeln, "name_vorschlag", "") },
            { "stufe", stand.FilialStufe() },
            { "plaetze", stand.OrtKapazitaet(OrtFiliale) },
            { "belegt", (long)stand.PersonalAm(OrtFiliale).Count },
            { "fehlt", fehlt.Cast<object?>().ToList() },
            { "naechste", naechste },
            { "problem", "" },
        };
        if (fehlt.Count > 0)
        {
            Py.Setzen(eintrag, "problem", Prozent("Noch nicht so weit: %s.", string.Join(", ", fehlt)));
        }
        else if (naechste is null)
        {
            Py.Setzen(eintrag, "problem", "Die Filiale ist fertig ausgebaut.");
        }
        else if (stand.Geld < Py.Int(naechste["preis"]))
        {
            Py.Setzen(eintrag, "problem", Prozent("Dafür reicht dein Geld noch nicht (%s fehlen).",
                Wohnung.GanzeEuro(Py.Int(naechste["preis"]) - Math.Max(0, stand.Geld))));
        }
        return eintrag;
    }

    /// <summary>branch_effect_text: kurzer Satz zu den Vorteilen der Filiale.</summary>
    public static string FilialVorteilText(PyObjekt inhalt)
    {
        var regeln = FilialRegeln(inhalt);
        var naehe = OderLeer(regeln["naehe"]);
        var teile = new List<string>();
        if (Py.Wahr(regeln["anfragen_plus"]))
        {
            teile.Add("+" + Plural(regeln["anfragen_plus"], "Kundenanfrage", "Kundenanfragen") + " pro Tag");
        }
        if (Py.Wahr(naehe["vorteil"]))
        {
            var orte = Py.Wahr(naehe["orte"]) ? naehe["orte"] : new List<object?>();
            var namen = (Get(FirmaRegeln(inhalt), "kunden", new List<object?>()) as List<object?> ?? [])
                .Select(Objekt).Where(k => Py.Enthaelt(orte, k["ort"])).Select(k => Py.Str(k["name"])).ToList();
            teile.Add(Prozent("+%d %% Preisvorteil bei %s", naehe["vorteil"], namen.Count > 0 ? string.Join(" und ", namen) : "Kunden vor Ort"));
        }
        return string.Join(", ", teile);
    }

    /// <summary>branch_opened_text</summary>
    public static string FilialeEroeffnetText(string name, PyObjekt inhalt) =>
        (Py.Wahr(FilialRegeln(inhalt)["eroeffnet"]) ? Py.Str(FilialRegeln(inhalt)["eroeffnet"]) : "%s ist eröffnet.")
            .Replace("%s", name, StringComparison.Ordinal);

    /// <summary>founding_text</summary>
    public static string GruendungText(PyObjekt inhalt)
    {
        var regeln = Objekt(FirmaRegeln(inhalt)["gruendung"]);
        return Py.Str(SpielInhalt.Zeilen(regeln["text"])).Replace("%s", Wohnung.GanzeEuro(regeln["kosten"]), StringComparison.Ordinal);
    }

    /// <summary>founded_text</summary>
    public static string GegruendetText(string name, PyObjekt inhalt) =>
        Py.Str(SpielInhalt.Zeilen(Objekt(FirmaRegeln(inhalt)["gruendung"])["gegruendet"])).Replace("%s", name, StringComparison.Ordinal);

    /// <summary>default_firm_name: "&lt;Vorname&gt; IT-Service".</summary>
    public static string StandardFirmenName(Spielstand stand)
    {
        var name = Rechner.PythonZeichen.Strip(Py.Str(Get(stand.Profil ?? [], "name", "")));
        return name.Length > 0 ? Prozent("%s IT-Service", Rechner.PythonZeichen.Split(name)[0]) : "IT-Service";
    }

    /// <summary>finance_days: die letzten Arbeitstage im Kassenbuch (neuester zuerst).</summary>
    public static List<PyObjekt> FinanzTage(Spielstand stand, int anzahl = 7)
    {
        var ergebnis = new List<PyObjekt>();
        foreach (var tag in stand.Kassenbuch.Keys.OrderByDescending(Py.Int).Take(anzahl))
        {
            var eintrag = Objekt(stand.Kassenbuch[tag]);
            var ein = Objekt(eintrag["ein"]);
            var aus = Objekt(eintrag["aus"]);
            var einnahmen = Py.Summe(ein.Select(p => p.Value));
            var ausgaben = Py.Summe(aus.Select(p => p.Value));
            // Ab 0.41: ein Kredit ist kein Gewinn und die Tilgung kein Verlust
            var kredit = Py.Minus(Get(ein, Ereignisse.BuchKredit, 0L), Get(aus, Ereignisse.BuchTilgung, 0L));
            ergebnis.Add(new PyObjekt
            {
                { "tag", tag }, { "ein", Py.Flach(ein) }, { "aus", Py.Flach(aus) },
                { "einnahmen", einnahmen }, { "ausgaben", ausgaben },
                { "gewinn", Py.Minus(Py.Minus(einnahmen, ausgaben), kredit) },
            });
        }
        return ergebnis;
    }

    /// <summary>finance_totals: Summen ueber das ganze Spiel je Art (ein, aus).</summary>
    public static (PyObjekt Ein, PyObjekt Aus) FinanzSummen(Spielstand stand)
    {
        var ein = new PyObjekt();
        var aus = new PyObjekt();
        foreach (var eintrag in stand.Kassenbuch.Values.Select(Objekt))
        {
            foreach (var (ziel, quelle) in new[] { (ein, Objekt(eintrag["ein"])), (aus, Objekt(eintrag["aus"])) })
            {
                foreach (var p in quelle)
                {
                    Py.Setzen(ziel, p.Key, Py.Plus(Get(ziel, p.Key, 0L), p.Value));
                }
            }
        }
        return (ein, aus);
    }

    /// <summary>balance_series: Kontostand nach den letzten Feierabenden (Beschriftungen, Werte).</summary>
    public static (List<string> Namen, List<long> Werte) KontoVerlauf(Spielstand stand, int grenze = 30)
    {
        var punkte = stand.Kontostaende.Skip(Math.Max(0, stand.Kontostaende.Count - grenze))
            .Select(p => (Tag: Py.Int(p[0]), Geld: Py.Int(p[1]))).Append((Tag: stand.Tag, Geld: stand.Geld)).ToList();
        return ([.. punkte.Select(p => Prozent("T%d", p.Tag))], [.. punkte.Select(p => Math.Max(0, p.Geld))]);
    }

    /// <summary>firm_summary: eine Zeile zur Firma (Uebersicht auf PC und Handy).</summary>
    public static string FirmenZeile(Spielstand stand)
    {
        var zahlen = stand.FirmenTag();
        var gewinn = Py.Int(zahlen["umsatz"]) - Py.Int(zahlen["gehaelter"]) - Py.Int(zahlen["nebenkosten"]) - stand.KreditRaten() - stand.WerbeKosten();
        return Prozent("%d von %d Plätzen besetzt · heute %s%s", (long)stand.Personal.Count, stand.Kapazitaet,
            gewinn >= 0 ? "+" : "-", Wohnung.GanzeEuro(Math.Abs(gewinn)));
    }

    /// <summary>firm_day_text: Feierabendzeilen der Firma mit Tickets, Projekten, Serverfarm, Krediten, Steuer, Gelerntem.</summary>
    public static string FirmenTagText(PyObjekt zahlen, PyObjekt inhalt)
    {
        var text = Prozent("Umsatz Mitarbeiter: +%s. Gehälter: -%s. Nebenkosten: -%s.",
            Wohnung.GanzeEuro(Get(zahlen, "umsatz", 0L)), Wohnung.GanzeEuro(Get(zahlen, "gehaelter", 0L)),
            Wohnung.GanzeEuro(Get(zahlen, "nebenkosten", 0L)));
        var kredite = (Py.Wahr(zahlen["kredite"]) ? Liste(zahlen["kredite"]) : []).Select(Objekt).ToList();
        var bezahlt = Py.Summe(kredite.Where(k => Py.Gleich(k["art"], "rate")).Select(k => k["rate"]));
        if (Py.Wahr(bezahlt))
        {
            text += Prozent(" Kreditraten: -%s.", Wohnung.GanzeEuro(bezahlt));
        }
        if (Py.Wahr(zahlen["werbung"]))
        {
            text += Prozent(" Werbung: -%s.", Wohnung.GanzeEuro(zahlen["werbung"]));
        }
        var steuern = (Py.Wahr(zahlen["steuer"]) ? Liste(zahlen["steuer"]) : []).Select(Objekt).ToList();
        var ruecklage = Py.Summe(steuern.Where(s => Py.Gleich(s["art"], "ruecklage")).Select(s => s["betrag"]));
        if (Py.Kleiner(0L, ruecklage))
        {
            text += Prozent(" Umsatzsteuer in die Rücklage: -%s.", Wohnung.GanzeEuro(ruecklage));
        }
        text += Block("Kundentickets", (Py.Wahr(zahlen["tickets"]) ? Liste(zahlen["tickets"]) : []).Select(t => TicketErgebnisText(Objekt(t))));
        text += Block("Projekte", (Py.Wahr(zahlen["projekte"]) ? Liste(zahlen["projekte"]) : []).Select(p => ProjektTagText(Objekt(p))));
        var farm = Py.Wahr(zahlen["serverfarm"]) ? FarmTagText(null, Objekt(zahlen["serverfarm"]), inhalt) : [];
        if (Py.Wahr(zahlen["serverfarm_frei"]))
        {
            farm.Insert(0, FarmFreiText(inhalt));
        }
        text += Block(FarmTitel, farm);
        text += Block("Kredite", Wirtschaft.KreditTagZeilen(kredite));
        text += Block("Umsatzsteuer", Wirtschaft.SteuerTagZeilen(steuern, inhalt));
        var gelernt = (Py.Wahr(zahlen["gelernt"]) ? Liste(zahlen["gelernt"]) : []).Select(Objekt).ToList();
        text += Block("Dazugelernt", gelernt.Where(g => !Py.Wahr(g["grenze"])).Select(GelerntText));
        // Ab 0.40: wer die Grenze fuers Lernen durch Arbeit erreicht, steht extra
        text += Block("Grenze erreicht (weiter nur mit Weiterbildung)", gelernt.Where(g => Py.Wahr(g["grenze"])).Select(GelerntText));
        return text;
    }

    private static string Block(string titel, IEnumerable<string> zeilen)
    {
        var liste = zeilen.ToList();
        return liste.Count == 0 ? "" : "\n\n" + titel + ":\n" + string.Join("\n", liste.Select(z => "• " + z));
    }

    /// <summary>offer_rules</summary>
    public static PyObjekt AngebotRegeln(PyObjekt inhalt) => Objekt(FirmaRegeln(inhalt)["angebote"]);

    /// <summary>ticket_rules</summary>
    public static PyObjekt TicketRegeln(PyObjekt inhalt) => Objekt(FirmaRegeln(inhalt)["tickets"]);

    /// <summary>firm_customer</summary>
    public static PyObjekt? FirmaKunde(object? kunde, PyObjekt inhalt) =>
        (Get(FirmaRegeln(inhalt), "kunden", new List<object?>()) as List<object?> ?? []).Select(Objekt).FirstOrDefault(k => Py.Gleich(k["id"], kunde));
}
