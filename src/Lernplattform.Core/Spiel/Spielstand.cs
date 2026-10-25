using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Zufall;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>Ein Eintrag des Ereignisprotokolls: (Zeitstempel, Typ, Daten) wie in Python.</summary>
public sealed record SpielEreignis(object? Zeit, string Art, PyObjekt Daten);

/// <summary>
/// Phase 7, W2: der aus dem Ereignisprotokoll berechnete Spielstand, Nachbau von
/// <c>fisi_game.GameState</c> (Python 0.63.2, Z. 3314-4930). Alle Felder heissen wie in Python
/// (Kommentar) und stehen in derselben Reihenfolge; Werte sind Python-Werte (long, double,
/// string, <see cref="PyObjekt"/>, <see cref="PyDict"/>, <see cref="PySet"/>, <see cref="PyTupel"/>).
/// W2 baut den Stand ohne eigene Firma; die Ereignisse und Feierabend-Teile der Firma und der
/// Serverfarm kommen in W3 (bis dahin: NotSupportedException). Stoppgrenze (Plan Phase 7,
/// Abschnitt 10). Vergleichswerte G44 (<c>spiel_neuaufbau.json</c>) und G45 (<c>spiel_tickets.json</c>).
/// </summary>
public sealed partial class Spielstand
{
    // Felder in der Reihenfolge von GameState.__init__ (vars())
    public PyObjekt Inhalt { get; }                       // content
    public PyObjekt? Profil;                              // profile
    public PyObjekt Reputation = new();                   // reputation
    public long Geld;                                     // money
    public long TageFertig;                               // days_done
    public PySet Geloest = [];                            // solved
    public PyDict Bearbeitet = new();                     // handled
    public List<SpielEreignis> Verlauf = [];              // history
    public long TicketsFertig;                            // tickets_done
    public List<PyObjekt> Lieferungen = [];               // deliveries
    public PyDict Verbaut = new();                        // used
    public PyDict Falschbestellungen = new();             // wrong_orders
    public PySet Zurueckgeschickt = [];                   // returned_orders
    public PySet GeseheneZwischenfaelle = [];             // seen_incidents
    public PyDict ZwischenfallTage = new();               // incident_days
    public List<PyObjekt> Ersatzbestellungen = [];        // spare_orders
    public object? Wohnung;                               // home_id
    public PyDict Moebel = new();                         // furniture
    public PyDict Einrichtungen = new();                  // layouts
    public long Miete;                                    // rent
    public long Kaution;                                  // deposit
    public string? ErstesEreignis;                        // first_event
    public object StartReputation;                        // start_reputation
    public object? Firma;                                 // firm
    public PyDict Personal = new();                       // staff
    public PySet JeEingestellt = [];                      // ever_hired
    public List<object?> Weiterbildungen = [];            // trainings
    public PyDict Kassenbuch = new();                     // book
    public List<PyTupel> Kontostaende = [];               // balances
    public PyDict Angebote = new();                       // offers
    public PyDict Delegationen = new();                   // delegations
    public PyDict TicketErgebnisse = new();               // ticket_results
    public PyDict ProjektAngebote = new();                // project_offers
    public PyDict Projekte = new();                       // projects
    public PySet ProjektTage = [];                        // project_days
    public PyDict Raeume = new();                         // rooms
    public PyDict PersonalZuwachs = new();                // staff_gains
    public PyDict PersonalRoutine = new();                // staff_routine
    public List<object?> LernProtokoll = [];              // learn_log
    public List<PyTupel> TagesProtokoll = [];            // day_log
    public List<PyTupel> RangProtokoll = [];             // rank_log
    public object? RangGesehen;                           // _rank_seen
    public PyDict Kredite = new();                        // loans
    public long Mahnstufe;                                // dunning
    public long MahnTag;                                  // dunning_day
    public List<object?> KreditProtokoll = [];            // loan_log
    public long SteuerRuecklage;                          // tax_reserve
    public long SteuerSchuld;                             // tax_debt
    public PyObjekt SteuerZeitraum = NeuerSteuerZeitraum(); // tax_period
    public long SteuerTage;                               // tax_days
    public List<object?> SteuerProtokoll = [];            // tax_log
    public List<object?> Werbungen = [];                  // ads
    public List<object?> WerbeProtokoll = [];             // ad_log
    public PyDict Zertifikate = new();                    // certs
    public PyDict Stimmung = new();                       // mood
    public PyDict StimmungTief = new();                   // mood_low
    public PyDict Verstimmt = new();                      // upset
    public List<object?> Abwesenheiten = [];              // absences
    public List<object?> PersonalProtokoll = [];          // personal_log
    public PyDict Antworten = new();                      // answers
    public PySet SchlichtTage = [];                       // mediation_days
    public PyDict SelbstGekuendigt = new();               // quit_staff
    public bool Live;                                     // _live
    public PyDict Gehend = new();                         // leaving
    public PyDict Zurueckgekehrt = new();                 // returned_staff
    public object? Zweigstelle;                           // branch
    public PyDict PersonalOrt = new();                    // staff_site
    public PyDict Erfolge = new();                        // achievements
    public long PersonalHoechst;                          // staff_peak
    public PyDict AusbauTage = new();                     // expand_days
    public List<PyTupel> StimmungsProtokoll = [];        // mood_log
    public long MahnHoechst;                              // dunning_peak
    public List<object?> MahnUeberstanden = [];           // dunning_over
    public object? Schwierigkeitsgrad;                    // difficulty
    public object? FarmFrei;                              // farm_unlocked
    public object? Farm;                                  // farm

    public PyObjekt Balancing => Objekt(Inhalt["balancing"]);

    /// <summary>GameState(events, content): alle Ereignisse in ihrer Reihenfolge anwenden.</summary>
    public Spielstand(IEnumerable<SpielEreignis> ereignisse, PyObjekt inhalt)
    {
        Inhalt = inhalt;
        var start = Balancing["reputation_start"]!;
        foreach (var achse in Ereignisse.Achsen)
        {
            Py.Setzen(Reputation, achse, start);
        }
        var hardware = Objekt(Inhalt["hardware"]);
        if (hardware["grundbestand"] is PyObjekt grund)
        {
            foreach (var (teil, anzahl) in grund)
            {
                Lieferungen.Add(new PyObjekt
                {
                    { "teil", teil }, { "menge", Py.Int(anzahl) }, { "ankunft", 0L }, { "aufgabe", Grundbestand }, { "haendler", "" },
                });
            }
        }
        var wohnungen = Objekt(Inhalt["wohnungen"]);
        Wohnung = wohnungen["start"];
        if (Get(wohnungen, "start_moebel", new PyObjekt()) is PyObjekt startMoebel)
        {
            foreach (var (stueck, moebel) in startMoebel)
            {
                Moebel[stueck] = moebel;
            }
        }
        StartReputation = start;
        RangGesehen = Objekt(Liste(Balancing["raenge"])[0])["name"];

        // Ab 0.63.2 (E-5, Weg A): eindeutige Doppel zaehlen nur einmal, das erste gilt.
        var einmal = new PySet();
        foreach (var e in ereignisse)
        {
            Anwenden(e, einmal);
        }
        BearbeitetMarkieren();
    }

    /// <summary>new_tax_period: ein leerer Voranmeldungszeitraum.</summary>
    public static PyObjekt NeuerSteuerZeitraum() => new()
    {
        { "start", null }, { "ust", 0.0 }, { "vst", 0.0 }, { "umsatz", 0L }, { "ausgaben", 0L }, { "tage", 0L },
    };

    /// <summary>double_of: Schluessel eines eindeutigen Doppels (E-5) oder null.</summary>
    public static PyTupel? DoppelVon(string art, PyObjekt daten)
    {
        if (art == Ereignisse.Erledigt)
        {
            var aufgabe = daten["aufgabe"];
            var tag = daten["tag"];
            return aufgabe is string { Length: > 0 } && Py.IstNurInt(tag) && Py.Int(tag) > 0
                ? new PyTupel(art, aufgabe, tag)
                : null;
        }
        if (art == Ereignisse.Feierabend)
        {
            var tag = daten["tag"];
            return Py.IstNurInt(tag) && Py.Int(tag) > 0 ? new PyTupel(art, tag) : null;
        }
        return null;
    }

    /// <summary>counted_once: die Doppel-Merker zum jetzigen Stand, aus history.</summary>
    public PySet EinmalGezaehlt() => new(Verlauf.Select(e => (object?)DoppelVon(e.Art, e.Daten)).Where(k => k is not null));

    /// <summary>
    /// apply_event: ein Ereignis anwenden (Rumpf der Ereignis-Schleife). false: als eindeutiges
    /// Doppel uebersprungen.
    /// </summary>
    public bool Anwenden(SpielEreignis e, PySet einmal)
    {
        var (art, daten) = (e.Art, e.Daten);
        if (DoppelVon(art, daten) is { } doppel)
        {
            if (einmal.Contains(doppel))
            {
                return false;
            }
            einmal.Add(doppel);
        }
        Verlauf.Add(e);
        ErstesEreignis ??= Py.Str(e.Zeit);
        var heute = Py.IstInt(daten["tag"]) ? (Py.Wahr(daten["tag"]) ? Py.Int(daten["tag"]) : 0) : 0;
        if (heute == 0)
        {
            heute = TageFertig + 1;
        }
        if (Ereignisse.Firma.Contains(art) || Ereignisse.Farm.Contains(art))
        {
            throw new NotSupportedException("Ereignis " + art + ": eigene Firma und Serverfarm kommen in Phase 7 W3");
        }
        if (art == Ereignisse.Schwierigkeit && Schwierigkeitsgrad is null
            && daten["stufe"] is string stufe && stufe is Ereignisse.Einfach or Ereignisse.Normal)
        {
            Schwierigkeitsgrad = stufe;
        }
        if (art == Ereignisse.Erfolg)
        {
            var schluessel = new PyTupel(daten["erfolg"], Py.Int(Get(daten, "stufe", 0L) is var s && Py.Wahr(s) ? s : 0L));
            if (Py.Wahr(schluessel[0]) && Py.Wahr(schluessel[1]) && !Erfolge.ContainsKey(schluessel))
            {
                Erfolge[schluessel] = Py.Flach(daten);
            }
        }
        if (art is Ereignisse.Erledigt or Ereignisse.Verschoben && Py.Wahr(daten["zwischenfall"]))
        {
            GeseheneZwischenfaelle.Add(daten["aufgabe"]);
            var fall = Auftrag(daten["aufgabe"], Inhalt) ?? new PyObjekt();
            var gruppe = Get(fall, "gruppe", daten["aufgabe"]);
            var tag = Py.Int(Get(daten, "tag", 0L) is var t && Py.Wahr(t) ? t : 0L);
            ZwischenfallTage[gruppe] = Math.Max(Py.Int(ZwischenfallTage.Get(gruppe, 0L)), tag);
        }
        if (art == Ereignisse.Ersatzteil)
        {
            Geld += Py.Int(Get(daten, "geld", 0L));
            Buchen(heute, Ereignisse.BuchWaren, Get(daten, "geld", 0L));
            Lieferungen.Add(new PyObjekt
            {
                { "teil", daten["teil"] }, { "menge", 1L }, { "ankunft", Get(daten, "ankunft", 0L) }, { "aufgabe", daten["aufgabe"] },
                { "haendler", Get(daten, "haendler", "") }, { "nachbestellt", true },
            });
            Ersatzbestellungen.Add(Py.Flach(daten));
        }
        if (art is Ereignisse.Umzug or Ereignisse.MoebelGekauft or Ereignisse.MoebelVerkauft)
        {
            Buchen(heute, Ereignisse.BuchWohnen, Get(daten, "geld", 0L));
        }
        if (art == Ereignisse.Umzug)
        {
            Geld += Py.Int(Get(daten, "geld", 0L));
            Wohnung = Get(daten, "wohnung", Wohnung);
            Miete = Py.Int(Get(daten, "miete", 0L));
            Kaution = Py.Int(Get(daten, "kaution", 0L));
        }
        else if (art == Ereignisse.MoebelGekauft)
        {
            Geld += Py.Int(Get(daten, "geld", 0L));
            Moebel[daten["stueck"]] = daten["moebel"];
        }
        else if (art == Ereignisse.MoebelVerkauft)
        {
            Geld += Py.Int(Get(daten, "geld", 0L));
            Moebel.Pop(daten["stueck"]);
        }
        else if (art == Ereignisse.Einrichtung)
        {
            Einrichtungen[daten["wohnung"]] = daten;
        }
        if (art == Ereignisse.Profil)
        {
            // Der juengste Eintrag gewinnt (Ereignisse sind sortiert)
            Profil = new PyObjekt { { "name", Get(daten, "name", "") }, { "aussehen", Aussehen.Normalisieren(daten["aussehen"]) } };
        }
        else if (art is Ereignisse.Erledigt or Ereignisse.Verschoben)
        {
            ReputationAendern(Py.Wahr(daten["reputation"]) ? Objekt(daten["reputation"]) : new PyObjekt());
            Geld += Py.Int(Get(daten, "geld", 0L));
            Buchen(heute, Ereignisse.BuchAuftraege, Get(daten, "geld", 0L));
            if (art == Ereignisse.Erledigt)
            {
                ErledigtAnwenden(daten);
            }
        }
        else if (art == Ereignisse.Feierabend)
        {
            FeierabendAnwenden(daten, heute);
        }
        return true;
    }

    private void ErledigtAnwenden(PyObjekt daten)
    {
        TicketsFertig += 1;
        if (Py.Wahr(daten["fehllieferung"]))
        {
            // Falsch bestellt - die Ware kommt trotzdem ins Lager
            var waren = ListeOderLeer(daten, "lieferung").Select(i =>
            {
                var neu = Py.Flach(Objekt(i));
                Py.Setzen(neu, "aufgabe", daten["aufgabe"]);
                Py.Setzen(neu, "falsch", true);
                return neu;
            }).ToList();
            Lieferungen.AddRange(waren);
            Falschbestellungen[daten["aufgabe"]] = waren;
        }
        if (Py.Wahr(daten["ruecksendung"]))
        {
            foreach (var (teil, anzahl) in Objekt(daten["ruecksendung"]))
            {
                Verbaut[teil] = Py.Int(Verbaut.Get(teil, 0L)) + Py.Int(anzahl);
            }
        }
        if (Py.Wahr(daten["richtig"]) && Py.Wahr(daten["falschlieferung"]))
        {
            Falschbestellungen.Pop(daten["falschlieferung"]);
            Zurueckgeschickt.Add(daten["falschlieferung"]);
        }
        if (Py.Wahr(daten["richtig"]))
        {
            Geloest.Add(daten["aufgabe"]);
            foreach (var i in ListeOderLeer(daten, "lieferung"))
            {
                var neu = Py.Flach(Objekt(i));
                Py.Setzen(neu, "aufgabe", daten["aufgabe"]);
                Lieferungen.Add(neu);
            }
            foreach (var teil in ListeOderLeer(daten, "aus_lager"))
            {
                Verbaut[teil] = Py.Int(Verbaut.Get(teil, 0L)) + 1;
            }
        }
    }

    private void FeierabendAnwenden(PyObjekt daten, long heute)
    {
        TageFertig += 1;
        Geld += Py.Int(Get(daten, "gehalt", 0L)) - Py.Int(Get(daten, "miete", 0L));
        Buchen(heute, Ereignisse.BuchGehalt, Get(daten, "gehalt", 0L));
        Buchen(heute, Ereignisse.BuchWohnen, -Py.Int(Get(daten, "miete", 0L)));
        var firma = Py.Wahr(daten["firma"]) ? Objekt(daten["firma"]) : new PyObjekt();
        Live = Py.Hat(firma, "personal");
        if (Projekte.Count > 0 || Farm is not null || Personal.Count > 0 || Firma is not null)
        {
            throw new NotSupportedException("Feierabend der eigenen Firma kommt in Phase 7 W3");
        }
        Geld += Py.Int(Get(firma, "umsatz", 0L)) - Py.Int(Get(firma, "gehaelter", 0L)) - Py.Int(Get(firma, "nebenkosten", 0L));
        Buchen(heute, Ereignisse.BuchUmsatz, Get(firma, "umsatz", 0L));
        Buchen(heute, Ereignisse.BuchGehaelter, -Py.Int(Get(firma, "gehaelter", 0L)));
        Buchen(heute, Ereignisse.BuchNebenkosten, -Py.Int(Get(firma, "nebenkosten", 0L)));
        if (Py.Wahr(firma["tickets"]) || Py.Wahr(firma["projekte"]) || Py.Wahr(firma["serverfarm"])
            || Py.Wahr(firma["serverfarm_frei"]) || (Live && Py.Wahr(firma["personal"])))
        {
            throw new NotSupportedException("Feierabend der eigenen Firma kommt in Phase 7 W3");
        }
        Live = false;
        Kontostaende.Add(new PyTupel(heute, Geld));
        StartReputation = MittlereReputation;
        TagesProtokoll.Add(new PyTupel(heute, PythonRunden.Runde1(MittlereReputation)));
        var rang = RangFuer(MittlereReputation, Balancing, Tag);
        if (!Py.Gleich(rang, RangGesehen))
        {
            RangProtokoll.Add(new PyTupel(Tag, rang));
            RangGesehen = rang;
        }
    }

    /// <summary>_mark_handled: Tickets des laufenden Tages (Tag steht in den Nutzdaten).</summary>
    public void BearbeitetMarkieren()
    {
        Bearbeitet = new PyDict();
        foreach (var e in Verlauf)
        {
            Notieren(e.Art, e.Daten);
        }
    }

    /// <summary>note_handled: nach dem Anwenden eines einzelnen neuen Ereignisses.</summary>
    public void BearbeitetNotieren(string art, PyObjekt daten, long tagVorher)
    {
        if (Tag != tagVorher)
        {
            BearbeitetMarkieren();
            return;
        }
        Notieren(art, daten);
    }

    private void Notieren(string art, PyObjekt daten)
    {
        if (art is Ereignisse.Erledigt or Ereignisse.Verschoben or Ereignisse.Ersatzteil && Py.Gleich(daten["tag"], Tag))
        {
            Bearbeitet[daten["aufgabe"]] = art == Ereignisse.Verschoben ? Ereignisse.StatusVerschoben
                : art == Ereignisse.Ersatzteil ? Ereignisse.Wartet
                : Py.Wahr(daten["richtig"]) ? Ereignisse.Richtig : Ereignisse.Falsch;
        }
    }

    /// <summary>_book: Kassenbuch je Arbeitstag.</summary>
    public void Buchen(long tag, string art, object? betrag)
    {
        var euro = Py.Int(Py.Wahr(betrag) ? betrag : 0L);
        if (euro == 0)
        {
            return;
        }
        var eintrag = (PyObjekt)Kassenbuch.SetDefault(tag, new PyObjekt { { "ein", new PyObjekt() }, { "aus", new PyObjekt() } })!;
        var seite = Objekt(eintrag[euro > 0 ? "ein" : "aus"]);
        Py.Setzen(seite, art, Py.Int(Get(seite, art, 0L)) + Math.Abs(euro));
    }

    /// <summary>_apply_reputation: je Achse zwischen 0 und 100.</summary>
    public void ReputationAendern(PyObjekt aenderung)
    {
        foreach (var (achse, wert) in aenderung)
        {
            if (Py.Hat(Reputation, achse))
            {
                Py.Setzen(Reputation, achse, Math.Max(0, Math.Min(100, Py.Int(Reputation[achse]) + Py.Int(wert))));
            }
        }
    }

    // -- Kennzahlen ---------------------------------------------------------

    /// <summary>day</summary>
    public long Tag => TageFertig + 1;

    /// <summary>mean_reputation</summary>
    public double MittlereReputation => Reputation.Sum(p => Py.Int(p.Value)) / (double)Reputation.Count;

    /// <summary>rank</summary>
    public object? Rang => RangFuer(MittlereReputation, Balancing, Tag);

    /// <summary>day_rank: Rang zu Beginn des Arbeitstags.</summary>
    public object? TagesRang => RangFuer(Py.F(StartReputation), Balancing, Tag);

    /// <summary>level</summary>
    public object Stufe => Schwierigkeitsgrad ?? Ereignisse.Normal;

    /// <summary>easy</summary>
    public bool Leicht => Py.Gleich(Schwierigkeitsgrad, Ereignisse.Einfach);

    /// <summary>ease: Regel des Schwierigkeitsgrads.</summary>
    public object? Erleichterung(string schluessel, object? normal) => SchwierigkeitsRegel(Stufe, schluessel, normal, Inhalt);

    /// <summary>tickets_today</summary>
    public long TicketsHeute => TicketsJeTag(TagesRang, Balancing);

    /// <summary>next_rank()</summary>
    public PyTupel? NaechsterRang() => NaechsterRangFuer(MittlereReputation, Tag, Balancing);

    /// <summary>salary: Gehalt von Bitweiche, nach der Gruendung keins mehr.</summary>
    public object? Gehalt => Py.Wahr(Firma) ? 0L : Get(Objekt(Balancing["gehalt_pro_tag"]), Py.Str(Rang), 0L);

    /// <summary>rank_for: Rang zur mittleren Reputation (mit tag erst ab seinem Arbeitstag).</summary>
    public static object? RangFuer(double wert, PyObjekt balancing, long? tag = null)
    {
        var raenge = Liste(balancing["raenge"]).Select(Objekt).ToList();
        var name = raenge[0]["name"];
        foreach (var rang in raenge)
        {
            if (wert >= Py.F(rang["ab"]) && (tag is null || tag >= Py.F(Get(rang, "ab_tag", 1L))))
            {
                name = rang["name"];
            }
        }
        return name;
    }

    /// <summary>tickets_per_day</summary>
    public static long TicketsJeTag(object? rang, PyObjekt balancing)
    {
        var wert = balancing["tickets_pro_tag"];
        if (wert is PyObjekt je)
        {
            return Py.Int(rang is string r && Py.Hat(je, r) ? je[r] : je.Select(p => p.Value).MinBy(Py.F));
        }
        return Py.Int(wert);
    }

    /// <summary>next_rank: (name, fehlende Reputation, fehlende Tage) oder null.</summary>
    public static PyTupel? NaechsterRangFuer(double wert, long tag, PyObjekt balancing)
    {
        var jetzt = RangFuer(wert, balancing, tag);
        var raenge = Liste(balancing["raenge"]).Select(Objekt).ToList();
        var index = raenge.FindIndex(r => Py.Gleich(r["name"], jetzt));
        if (index + 1 >= raenge.Count)
        {
            return null;
        }
        var rang = raenge[index + 1];
        return new PyTupel(rang["name"], Math.Max(0, (long)Math.Ceiling(Py.F(rang["ab"]) - wert)),
            Math.Max(0, Py.Int(Get(rang, "ab_tag", 1L)) - tag));
    }

    /// <summary>difficulty_rule: Wert einer Einfach-Regel (sonst der Normal-Wert).</summary>
    public static object? SchwierigkeitsRegel(object? stufe, string schluessel, object? normal, PyObjekt inhalt)
    {
        if (!Py.Gleich(stufe, Ereignisse.Einfach))
        {
            return normal;
        }
        var regeln = Py.Wahr(Objekt(inhalt["balancing"])["schwierigkeit"]) ? Objekt(Objekt(inhalt["balancing"])["schwierigkeit"]) : new PyObjekt();
        var einfach = Py.Wahr(regeln[Ereignisse.Einfach]) ? Objekt(regeln[Ereignisse.Einfach]) : new PyObjekt();
        var wert = einfach[schluessel];
        return wert ?? normal;
    }

    // -- Lager ------------------------------------------------------------

    private static bool Angekommen(PyObjekt lieferung, long tag) => Py.F(lieferung["ankunft"]) <= tag;

    /// <summary>arrived: ist die Ware einer Bestellung komplett da?</summary>
    public bool Da(object? aufgabe)
    {
        var waren = Lieferungen.Where(l => Py.Gleich(l["aufgabe"], aufgabe)).ToList();
        return waren.Count > 0 && waren.All(l => Angekommen(l, Tag));
    }

    /// <summary>stock: Lagerbestand {bauteil_id: anzahl}, angekommen minus verbaut.</summary>
    public PyDict Lager()
    {
        var ergebnis = new PyDict();
        foreach (var l in Lieferungen)
        {
            if (Angekommen(l, Tag) && !Py.Wahr(l["empfaenger"]))
            {
                ergebnis[l["teil"]] = Py.Plus(ergebnis.Get(l["teil"], 0L), l["menge"]);
            }
        }
        foreach (var (teil, anzahl) in Verbaut)
        {
            ergebnis[teil] = Py.Plus(ergebnis.Get(teil, 0L), Py.Mal(-1L, anzahl));
        }
        var bestand = new PyDict();
        foreach (var (teil, anzahl) in ergebnis)
        {
            if (Py.F(anzahl) > 0)
            {
                bestand[teil] = anzahl;
            }
        }
        return bestand;
    }

    /// <summary>warehouse: unterwegs, Bestand (in Reihenfolge der Hardware) und ausgeliefert.</summary>
    public PyObjekt Lagerhaus()
    {
        var unterwegs = Lieferungen.Where(l => !Angekommen(l, Tag)).OrderBy(l => Py.F(l["ankunft"])).ToList();
        var ausgeliefert = Lieferungen.Where(l => Angekommen(l, Tag) && Py.Wahr(l["empfaenger"])).ToList();
        var hardware = Objekt(Inhalt["hardware"]);
        var reihenfolge = Liste(hardware["teile"]).Concat(Py.Hat(hardware, "ersatzteile") ? Liste(hardware["ersatzteile"]) : [])
            .Select(t => Objekt(t)["id"]).ToList();
        var zeilen = Lager().Select(p => new PyTupel(p.Key, p.Value))
            .OrderBy(z => reihenfolge.FindIndex(k => Py.Gleich(k, z[0])) is var i and >= 0 ? i : reihenfolge.Count).ToList();
        return new PyObjekt { { "unterwegs", unterwegs }, { "bestand", zeilen }, { "ausgeliefert", ausgeliefert } };
    }

    /// <summary>available_parts: Teile auf der Werkbank plus passende Teile aus dem Lager.</summary>
    public List<object?> VerfuegbareTeile(PyObjekt auftrag)
    {
        var teile = Py.Hat(auftrag, "teile") ? Liste(auftrag["teile"]).ToList() : [];
        var erlaubt = ListeOderLeer(auftrag, "aus_lager").ToList();
        if (Py.Wahr(auftrag["austausch"]))
        {
            erlaubt.Add(Objekt(auftrag["austausch"])["typ"]);
        }
        var lager = Lager();
        foreach (var kennung in lager.Keys)
        {
            var t = Teil(kennung, Inhalt);
            if (t is not null && erlaubt.Any(e => Py.Gleich(e, t["typ"])) && !teile.Any(k => Py.Gleich(k, kennung)))
            {
                teile.Add(kennung);
            }
        }
        teile.AddRange(LagerKombis(lager, erlaubt, Inhalt).Where(k => !teile.Any(t => Py.Gleich(t, k))).Cast<object?>().ToList());
        return teile;
    }
}
