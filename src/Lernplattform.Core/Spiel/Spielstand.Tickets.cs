using Lernplattform.Core.Daten;
using Lernplattform.Core.Zufall;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Tickets des Arbeitstags wie <c>GameState</c> (Python 0.63.2, Z. 4729-4930):
/// Tagesauswahl (_pool, _pool_order), Zwischenfall des Tages (fester Wuerfel aus Spielbeginn
/// und Tag), falsche Lieferungen, vorbereitete Auftraege. Vergleichswerte G45.
/// </summary>
public sealed partial class Spielstand
{
    /// <summary>_ready: ein Auftrag mit "nach" wartet auf den vorigen (bei einer Bestellung auf die Ware).</summary>
    public bool Bereit(PyObjekt auftrag)
    {
        var vorher = auftrag["nach"];
        if (!Py.Wahr(vorher))
        {
            return true;
        }
        if (!Geloest.Contains(vorher))
        {
            return false;
        }
        var erster = Auftrag(vorher, Inhalt);
        return erster is null || !Py.Gleich(erster["typ"], "bestellung") || Da(vorher);
    }

    /// <summary>_pool_order: Story-Auftraege nach Tag und Platz, Vorlagen nach Tag und Wuerfel.</summary>
    private static (long Gruppe, double AbTag, double Folge) PoolSchluessel(int index, PyObjekt auftrag) =>
        !Py.Wahr(auftrag["vorlage"])
            ? (0, Py.F(Get(auftrag, "ab_tag", 1L)), index)
            : (1, Py.F(Get(auftrag, "ab_tag", 1L)), Wuerfel.Werfen("mischung", 0, Py.Str(auftrag["id"])));

    /// <summary>_pool: offene Auftraege, die heute drankommen koennen, in Verteilreihenfolge.</summary>
    public List<PyObjekt> Vorrat()
    {
        var auftraege = Liste(Inhalt["aufgaben"]).Select(Objekt)
            .Select((a, i) => (Schluessel: PoolSchluessel(i, a), Auftrag: a))
            .Where(p => Py.F(Get(p.Auftrag, "ab_tag", 1L)) <= Tag
                && !Geloest.Contains(p.Auftrag["id"])
                && !Bearbeitet.ContainsKey(p.Auftrag["id"])
                && !Falschbestellungen.ContainsKey(p.Auftrag["id"])
                && !WartetAufErsatz(p.Auftrag["id"])
                && Bereit(p.Auftrag))
            .ToList();
        // sort ist stabil wie Python; die Schluessel sind Tupel aus Zahlen
        return auftraege.OrderBy(p => p.Schluessel).Select(p => Besetzt(p.Auftrag)!).ToList();
    }

    /// <summary>staffed: Auftrag einer Person im Ruhestand uebernimmt die Nachfolge.</summary>
    public PyObjekt? Besetzt(PyObjekt? auftrag)
    {
        if (auftrag is null)
        {
            return null;
        }
        var person = AktivePerson(auftrag["auftraggeber"], Tag, Inhalt);
        if (Py.Gleich(person, auftrag["auftraggeber"]))
        {
            return auftrag;
        }
        var neu = Py.Flach(auftrag);
        Py.Setzen(neu, "auftraggeber", person);
        return neu;
    }

    /// <summary>waiting_for_spare: wartet der Auftrag auf ein nachbestelltes Ersatzteil?</summary>
    public bool WartetAufErsatz(object? aufgabe) =>
        Ersatzbestellungen.Any(b => Py.Gleich(b["aufgabe"], aufgabe) && Py.F(Get(b, "ankunft", 0L)) > Tag);

    /// <summary>spare_order_for: das juengste nachbestellte Ersatzteil eines Auftrags.</summary>
    public PyObjekt? ErsatzFuer(object? aufgabe) => Ersatzbestellungen.LastOrDefault(b => Py.Gleich(b["aufgabe"], aufgabe));

    /// <summary>waiting_for_delivery: Auftraege, die nur noch auf eine Lieferung warten.</summary>
    public List<PyObjekt> WartenAufLieferung() =>
        Liste(Inhalt["aufgaben"]).Select(Objekt)
            .Where(a => !Geloest.Contains(a["id"]) && ((Geloest.Contains(a["nach"]) && !Bereit(a)) || WartetAufErsatz(a["id"])))
            .ToList();

    /// <summary>open_tickets: offene Tickets des Tages (falsche Lieferungen, Zwischenfall, Auswahl).</summary>
    public List<PyObjekt> OffeneTickets()
    {
        if (Py.Wahr(Firma))
        {
            return [];       // Bitweiche-Tickets gibt es nach der Gruendung nicht mehr
        }
        var jeTag = TicketsHeute;
        var regulaer = Bearbeitet.Keys.Count(k => !IstZwischenfall(k, Inhalt));
        var ergebnis = Vorrat().Take((int)Math.Max(0, jeTag - regulaer)).ToList();
        var fall = ZwischenfallHeute();
        if (fall is not null && !Bearbeitet.ContainsKey(fall["id"]))
        {
            ergebnis.Insert(0, fall);
        }
        return FalscheLieferungen().Concat(ergebnis).ToList();
    }

    /// <summary>wrong_deliveries: offene Zwischenfaelle "Falsche Ware".</summary>
    public List<PyObjekt> FalscheLieferungen()
    {
        var ergebnis = new List<PyObjekt>();
        foreach (var (bestellung, waren) in Falschbestellungen)
        {
            var kennung = Falschlieferung + Py.Str(bestellung);
            if (Bearbeitet.ContainsKey(kennung) || ((List<PyObjekt>)waren!).Any(w => !Angekommen(w, Tag)))
            {
                continue;
            }
            if (Auftrag(kennung, Inhalt) is { } auftrag)
            {
                ergebnis.Add(auftrag);
            }
        }
        return ergebnis;
    }

    /// <summary>incident_chance: Chance auf einen Zwischenfall an diesem Tag (gestaffelt).</summary>
    public static double ZwischenfallChance(long tag, PyObjekt regeln)
    {
        var chance = Get(regeln, "chance", 0L);
        var staffel = ListeOderLeer(regeln, "staffel").Select(Objekt).OrderBy(s => Py.F(s["ab_tag"]));
        foreach (var stufe in staffel)
        {
            if (tag >= Py.F(stufe["ab_tag"]))
            {
                chance = stufe["chance"];
            }
        }
        return Py.F(chance);
    }

    /// <summary>incident_today: der Zwischenfall des Tages oder null (hoechstens einer, jeder nur einmal).</summary>
    public PyObjekt? ZwischenfallHeute()
    {
        foreach (var kennung in Bearbeitet.Keys)
        {
            if (IstZwischenfall(kennung, Inhalt) && !((string)kennung!).StartsWith(Falschlieferung, StringComparison.Ordinal))
            {
                return Besetzt(Auftrag(kennung, Inhalt));
            }
        }
        var regeln = Py.Wahr(Balancing["zwischenfaelle"]) ? Objekt(Balancing["zwischenfaelle"]) : new PyObjekt();
        if (Profil is null || Tag < Py.F(Get(regeln, "ab_tag", 2L)))
        {
            return null;
        }
        var start = ErstesEreignis ?? "";
        if (Wuerfel.Werfen(start, Tag, "chance") >= ZwischenfallChance(Tag, regeln))
        {
            return null;
        }
        var abstand = Get(regeln, "abstand_gleiche_art", 0L);
        var kandidaten = (Py.Hat(Inhalt, "zwischenfaelle") ? Liste(Inhalt["zwischenfaelle"]) : []).Select(Objekt)
            .Where(a => !GeseheneZwischenfaelle.Contains(a["id"])
                && Py.F(Get(a, "ab_tag", 1L)) <= Tag && Bereit(a)
                && MelderDa(a)
                && Tag - Py.F(ZwischenfallTage.Get(Get(a, "gruppe", a["id"]), Py.Mal(-1L, abstand))) >= Py.F(abstand))
            .ToList();
        if (kandidaten.Count == 0)
        {
            return null;
        }
        return Besetzt(kandidaten[(int)(Wuerfel.Werfen(start, Tag, "wahl") * kandidaten.Count)]);
    }

    /// <summary>_reporter_present: Zwischenfaelle meldet nur, wer noch da ist.</summary>
    private bool MelderDa(PyObjekt auftrag) =>
        Person(auftrag["auftraggeber"], Inhalt) is not { } person || PersonDa(person, Tag);

    /// <summary>prepared_task: falsche Ware, Bestellung nach Ruecksendung, Rack mit Geraeten aus dem Lager.</summary>
    public PyObjekt? Vorbereitet(PyObjekt? auftrag)
    {
        if (auftrag is null)
        {
            return null;
        }
        auftrag = Besetzt(auftrag)!;
        if (Py.Wahr(auftrag["falschlieferung"]))
        {
            var waren = Falschbestellungen.Get(auftrag["falschlieferung"]) as List<PyObjekt> ?? [];
            var text = string.Join(", ", waren.Select(w => Py.Int(w["menge"]) + " × " + Py.Str(Teil(w["teil"], Inhalt)!["name"])));
            var ergebnis = FalschlieferungAuftrag((string)auftrag["falschlieferung"]!, Inhalt, text)!;
            Py.Setzen(ergebnis, "falsche_ware", waren.Select(w => (object?)new List<object?> { w["teil"], w["menge"] }).ToList());
            return ergebnis;
        }
        if (Py.Gleich(auftrag["typ"], "bestellung") && Zurueckgeschickt.Contains(auftrag["id"]))
        {
            var neu = Py.Flach(auftrag);
            Py.Setzen(neu, "kein_sparbonus", true);
            return neu;
        }
        if (!Py.Gleich(auftrag["typ"], "rack") || !Py.Wahr(auftrag["aus_lager"]))
        {
            return auftrag;
        }
        var geraete = ListeOderLeer(auftrag, "geraete").ToList();
        var anfang = geraete.Count;
        foreach (var (kennung, anzahl) in Lager())
        {
            var geraet = RackGeraet(kennung, Inhalt);
            if (geraet is not null && Py.Enthaelt(auftrag["aus_lager"], geraet["typ"]))
            {
                for (long i = 0; i < Py.Int(anzahl); i++)
                {
                    geraete.Add(kennung);
                }
            }
        }
        var rack = Py.Flach(auftrag);
        Py.Setzen(rack, "geraete", geraete);
        Py.Setzen(rack, "lager_ab", (long)anfang);
        return rack;
    }

    /// <summary>todays_tickets: [(aufgabe, status)] des Tages.</summary>
    public List<(PyObjekt Auftrag, string Status)> TicketsDesTages()
    {
        var ergebnis = new List<(PyObjekt, string)>();
        foreach (var (kennung, status) in Bearbeitet)
        {
            if (Besetzt(Auftrag(kennung, Inhalt)) is { } auftrag)
            {
                ergebnis.Add((auftrag, (string)status!));
            }
        }
        ergebnis.AddRange(OffeneTickets().Select(a => (a, Ereignisse.Offen)));
        return ergebnis;
    }

    /// <summary>quests: offene Tickets je Auftraggeber (mit ort nur die an diesem Ort).</summary>
    public PyDict Auftraege(object? ort = null)
    {
        var ergebnis = new PyDict();
        foreach (var auftrag in OffeneTickets())
        {
            if (ort is not null && !Py.Gleich(AuftragOrt(auftrag, Inhalt), ort))
            {
                continue;
            }
            ((List<object?>)ergebnis.SetDefault(auftrag["auftraggeber"], new List<object?>())!).Add(auftrag);
        }
        return ergebnis;
    }

    /// <summary>open_count_by_site</summary>
    public PyDict OffenJeOrt()
    {
        var zaehler = new PyDict();
        foreach (var auftrag in OffeneTickets())
        {
            var ort = AuftragOrt(auftrag, Inhalt);
            zaehler[ort] = Py.Int(zaehler.Get(ort, 0L)) + 1;
        }
        return zaehler;
    }

    /// <summary>open_count_by_room</summary>
    public PyDict OffenJeRaum()
    {
        var zaehler = new PyDict();
        foreach (var auftrag in OffeneTickets())
        {
            zaehler[auftrag["raum"]] = Py.Int(zaehler.Get(auftrag["raum"], 0L)) + 1;
        }
        return zaehler;
    }

    /// <summary>can_end_day (ohne eigene Firma: keine offenen Tickets mehr).</summary>
    public bool FeierabendMoeglich()
    {
        if (Py.Wahr(Firma))
        {
            throw new NotSupportedException("Feierabend der eigenen Firma kommt in Phase 7 W3");
        }
        return OffeneTickets().Count == 0;
    }

    /// <summary>all_done: keine Aufgabe mehr offen.</summary>
    public bool AllesErledigt() => Liste(Inhalt["aufgaben"]).All(a => Geloest.Contains(Objekt(a)["id"]));

    /// <summary>status_of</summary>
    public string StatusVon(object? aufgabe) => (string)Bearbeitet.Get(aufgabe, Ereignisse.Offen)!;

    /// <summary>is_open</summary>
    public bool IstOffen(object? aufgabe) => OffeneTickets().Any(a => Py.Gleich(a["id"], aufgabe));
}
