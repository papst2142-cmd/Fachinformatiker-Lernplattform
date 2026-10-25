using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Zufall;
using static Lernplattform.Core.Spiel.Bewertung;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Anzeigetexte des Spiel-Kerns fuer die Ticket-Bretter wie <c>fisi_game.py</c>
/// (Python 0.63.2): Warnung bei fehlendem Wissen, Rack, Formular, Terminal, Diagnose, Ersatzteile,
/// Warenkorb, Bauteile, Loesungstext und Rang-Hinweis. PC und Handy zeigen dieselben Texte.
/// Vergleichswerte G49 (<c>spiel_texte.json</c>).
/// </summary>
public static class SpielTexte
{
    private static PyObjekt Hw(PyObjekt inhalt) => Objekt(inhalt["hardware"]);

    private static string FormName(object? wert, PyObjekt inhalt) =>
        Py.Str(Get(Objekt(Hw(inhalt)["formfaktoren"]), Py.Str(wert), wert));

    /// <summary>gap_warning: Warnhinweis der weichen Sperre ("" ohne Luecken).</summary>
    public static string LueckenWarnung(IReadOnlyList<(string Thema, object? Noetig, object? Da)> luecken, Lerninhalte lernen)
    {
        if (luecken.Count == 0)
        {
            return "";
        }
        var teile = luecken.Select(l =>
        {
            var thema = lernen.ThemaNach[l.Thema];
            return Prozent("%s: %s (%d %% nötig, du hast %d %%)", Fachbereiche.Kurzname[thema.Fachbereich], thema.Name, l.Noetig, Py.Int(l.Da));
        });
        return Prozent("Dafür fehlt dir noch Wissen: %s. Du kannst das Ticket trotzdem bearbeiten, ein Fehler kostet dann aber doppelt Reputation. Tipp: Vorher ein paar Karteikarten %s lernen.",
            string.Join("; ", teile), luecken.Count == 1 ? "zu diesem Thema" : "zu diesen Themen");
    }

    // ---------------------------------------------------------------- Rack

    /// <summary>rack_source_text: woher ein Geraet kommt (noch nicht eingebaut).</summary>
    public static string RackHerkunft(PyObjekt auftrag, long index) =>
        Py.Kleiner(index, Get(auftrag, "lager_ab", (long)Liste(auftrag["geraete"]).Count)) ? "liegt bereit" : "aus dem Lager";

    private static string RackTyp(object? art, PyObjekt inhalt) =>
        Py.Str(Get(Get(Hw(inhalt), "rack_typen") as PyObjekt ?? [], Py.Str(art), art));

    private static string HeText(long unten, long hoehe)
    {
        var oben = unten + hoehe - 1;
        return oben == unten ? Prozent("HE %d", unten) : Prozent("HE %d–%d", unten, oben);
    }

    private static List<(string Art, object? Anzahl)> NachRackTyp(PyObjekt? zaehler, PyObjekt inhalt)
    {
        var ordnung = (Get(Hw(inhalt), "rack_typen") as PyObjekt ?? []).Select(p => p.Key).ToList();
        return (zaehler ?? []).OrderBy(p => ordnung.IndexOf(p.Key) is var i and >= 0 ? i : ordnung.Count).Select(p => (p.Key, p.Value)).ToList();
    }

    private static object Summe(IEnumerable<object?> werte) => Py.Summe(werte);

    /// <summary>rack_summary: Live-Anzeige unter dem Schrank (Text, ueberlastet).</summary>
    public static (string Text, bool Ueberlastet) RackZusammenfassung(PyObjekt auftrag, PyObjekt? antwort, PyObjekt inhalt)
    {
        var belegt = RackBelegt(auftrag, antwort, inhalt);
        var schrank = Objekt(auftrag["schrank"]);
        var he = Summe(belegt.Select(r => r.Geraet["he"]));
        var gewicht = Summe(belegt.Select(r => r.Geraet["gewicht"]));
        var strom = Summe(belegt.Where(r => !Py.Gleich(r.Geraet["typ"], "usv")).Select(r => r.Geraet["watt"]));
        var regeln = Objekt(Hw(inhalt)["rack_regeln"]);
        object usv = Summe(belegt.Where(r => Py.Gleich(r.Geraet["typ"], "usv")).Select(r => Get(r.Geraet, "leistung", 0L)));
        usv = Py.Plus(usv, Get(schrank, "usv_watt", 0L));
        long? leistung = Py.Wahr(usv) ? Py.Int(Py.Mal(usv, Py.Minus(1L, regeln["strom_reserve"]))) : null;
        var teile = new List<string>
        {
            Prozent("%d von %d HE", he, schrank["he"]), Prozent("%d von %d kg", gewicht, schrank["traglast"]),
            Prozent("%d W%s", strom, leistung is long l && l != 0 ? Prozent(" von %d W (USV)", l) : ""),
        };
        var ueber = Py.Kleiner(schrank["traglast"], gewicht) || (leistung is long k && Py.Kleiner(k, strom))
            || (Py.Wahr(Get(schrank, "kuehlung")) && Py.Kleiner(schrank["kuehlung"], strom));
        return (string.Join(" · ", teile), ueber);
    }

    /// <summary>rack_header: Schrank und Vorgaben als Zeilen.</summary>
    public static List<string> RackKopf(PyObjekt auftrag, PyObjekt inhalt)
    {
        var schrank = Objekt(auftrag["schrank"]);
        var erste = Prozent("Schrank: %d HE · Traglast %d kg", schrank["he"], schrank["traglast"]);
        if (Py.Wahr(Get(schrank, "kuehlung")))
        {
            erste += Prozent(" · Kühlung %d W", schrank["kuehlung"]);
        }
        if (Py.Wahr(Get(schrank, "usv_watt")))
        {
            erste += Prozent(" · USV im Raum %d W", schrank["usv_watt"]);
        }
        var zeilen = new List<string> { erste };
        var regeln = Get(auftrag, "vorgaben") as PyObjekt ?? [];
        var gewollt = NachRackTyp(Get(regeln, "mindestens") as PyObjekt, inhalt).Select(p => Prozent("%d × %s", p.Anzahl, RackTyp(p.Art, inhalt))).ToList();
        if (Py.Wahr(Get(regeln, "ports_min")))
        {
            gewollt.Add(Prozent("mind. %d Ports", regeln["ports_min"]));
        }
        if (Py.Wahr(Get(regeln, "frei_min")))
        {
            gewollt.Add(Prozent("%d HE frei lassen", regeln["frei_min"]));
        }
        if (gewollt.Count > 0)
        {
            zeilen.Add("Verlangt: " + string.Join(", ", gewollt));
        }
        return zeilen;
    }

    /// <summary>rack_specs: kurze Beschreibung eines Rack-Geraets.</summary>
    public static string RackDaten(PyObjekt geraet)
    {
        var teile = new List<string> { Prozent("%d HE", geraet["he"]), Prozent("%d kg", geraet["gewicht"]) };
        if (Py.Gleich(geraet["typ"], "usv"))
        {
            teile.Add(Prozent("liefert %d W", geraet["leistung"]));
        }
        else if (Py.Wahr(geraet["watt"]))
        {
            teile.Add(Prozent("%d W", geraet["watt"]));
        }
        else
        {
            teile.Add("passiv, kein Strom");
        }
        if (Py.Wahr(Get(geraet, "ports")))
        {
            teile.Add(Prozent("%d Ports", geraet["ports"]));
        }
        if (Py.Wahr(Get(geraet, "schaechte")))
        {
            teile.Add(Prozent("%d Schächte", geraet["schaechte"]));
        }
        return string.Join(" · ", teile);
    }

    /// <summary>rack_lines: Belegung als lesbare Zeilen von unten nach oben.</summary>
    public static List<string> RackZeilen(PyObjekt auftrag, PyObjekt? antwort, PyObjekt inhalt) =>
        [.. RackBelegt(auftrag, antwort, inhalt).Select(r => HeText(r.Unten, Py.Int(r.Geraet["he"])) + ": " + Py.Str(r.Geraet["name"]))];

    /// <summary>rack_place: Geraet Nr. index ab HE unten einbauen; ragt es oben heraus, rutscht es nach unten; Geraete im Weg kommen zurueck.</summary>
    public static PyObjekt RackSetzen(PyObjekt auftrag, PyObjekt? antwort, long index, object? unten, PyObjekt inhalt)
    {
        var geraete = Liste(auftrag["geraete"]);
        var geraet = RackGeraet(geraete[(int)index], inhalt)!;
        var groesse = Py.Int(Objekt(auftrag["schrank"])["he"]);
        var hoehe = Py.Int(geraet["he"]);
        var u = Math.Max(1, Math.Min(Py.Int(unten), groesse - hoehe + 1));
        var oben = u + hoehe - 1;
        var ergebnis = new PyObjekt();
        foreach (var (k, anderesUnten) in antwort ?? [])
        {
            if (Py.Int(k) == index)
            {
                continue;
            }
            var anderes = RackGeraet(geraete[(int)Py.Int(k)], inhalt)!;
            var au = Py.Int(anderesUnten);
            var anderesOben = au + Py.Int(anderes["he"]) - 1;
            if (anderesOben < u || au > oben)
            {
                Py.Setzen(ergebnis, k, au);
            }
        }
        Py.Setzen(ergebnis, index.ToString(System.Globalization.CultureInfo.InvariantCulture), u);
        return ergebnis;
    }

    /// <summary>rack_occupant: Nummer des Geraets in HE einheit, oder null.</summary>
    public static long? RackBelegtVon(PyObjekt auftrag, PyObjekt? antwort, long einheit, PyObjekt inhalt)
    {
        var geraete = Liste(auftrag["geraete"]);
        foreach (var (k, unten) in antwort ?? [])
        {
            var geraet = RackGeraet(geraete[(int)Py.Int(k)], inhalt)!;
            var u = Py.Int(unten);
            if (u <= einheit && einheit < u + Py.Int(geraet["he"]))
            {
                return Py.Int(k);
            }
        }
        return null;
    }

    // ------------------------------------------------------------ Formular

    private static string EuroGegeben(object? wert) => Euro(wert).Replace(" €", " €", StringComparison.Ordinal);

    /// <summary>form_given: Ausgangsdaten eines Formulars als Zeilen.</summary>
    public static List<string> FormAusgang(PyObjekt auftrag)
    {
        var art = Get(auftrag, "art");
        var daten = Get(auftrag, "daten") as PyObjekt ?? [];
        switch (art)
        {
            case "ip_plan":
            {
                var zeilen = new List<string> { Prozent("Netz: %s", daten["netz"]) };
                zeilen.AddRange(Liste(daten["teilnetze"]).Select(Objekt).Select(t => Prozent("%s: %d Geräte", t["name"], t["hosts"])));
                zeilen.Add(Py.Wahr(Get(daten, "vlsm")) ? "Vergabe nach Größe, größtes Netz zuerst, lückenlos" : "Gleich große Teilnetze in der genannten Reihenfolge");
                return zeilen;
            }
            case "raid":
                return [Prozent("%s mit %d Festplatten zu je %s %s", daten["level"], daten["platten"], Num(daten["groesse"]), Get(daten, "einheit", "TB"))];
            case "angebot" when Py.Wahr(Get(daten, "positionen")):
            {
                var zeilen = new List<string>();
                foreach (var p in Liste(daten["positionen"]).Select(Objekt))
                {
                    zeilen.Add(Py.Gleich(p["menge"], 1L) && !Py.Wahr(Get(p, "einheit"))
                        ? Prozent("%s: %s", p["text"], EuroGegeben(p["preis"]))
                        : Prozent("%s: %d %s × %s", p["text"], p["menge"], Get(p, "einheit", "Stück"), EuroGegeben(p["preis"])));
                }
                zeilen.Add(Prozent("Handlungskosten %s %% · Gewinn %s %% · Umsatzsteuer %s %%", Num(daten["handlungskosten"]), Num(daten["gewinn"]), Num(daten["ust"])));
                return zeilen;
            }
            case "angebot":
                return
                [
                    Prozent("%d × %s zu je %s (Einkauf)", daten["menge"], Get(daten, "artikel", "Artikel"), EuroGegeben(daten["einkaufspreis"])),
                    Prozent("Handlungskosten %s %% · Gewinn %s %% · Umsatzsteuer %s %%", Num(daten["handlungskosten"]), Num(daten["gewinn"]), Num(daten["ust"])),
                ];
            case "drucker":
            {
                var netz = Rechner.PythonIp.Netz(Py.Str(daten["netz"]))!;
                var bereich = Liste(daten["bereich"]);
                string Host(object? nummer) => Rechner.PythonIp.V4Text(netz.Netzadresse.Wert + Py.Int(nummer));
                var zeilen = new List<string>
                {
                    Prozent("Netz: %s · Gateway ist die erste Host-Adresse", daten["netz"]),
                    Prozent("Druckerbereich: %s bis %s, die erste freie Adresse nehmen", Host(bereich[0]), Host(bereich[1])),
                };
                var belegt = ListeOderLeer(daten, "belegt").OrderBy(Py.F).ToList();
                if (belegt.Count > 0)
                {
                    zeilen.Add("Schon belegt: " + string.Join(", ", belegt.Select(Host)));
                }
                if (Py.Gleich(Get(daten, "dns"), "gateway"))
                {
                    zeilen.Add("DNS übernimmt ebenfalls der Router (Gateway)");
                }
                zeilen.AddRange(ListeOderLeer(daten, "hinweise").Select(Py.Str));
                return zeilen;
            }
            case "leasing":
            {
                var zeilen = new List<string> { "Kaufpreis: " + Euro(daten["kaufpreis"]), Prozent("Leasing: %d Monate zu je %s", daten["laufzeit"], Euro(daten["rate"])) };
                if (Py.Wahr(Get(daten, "sonderzahlung")))
                {
                    zeilen.Add("Sonderzahlung zu Beginn: " + Euro(daten["sonderzahlung"]));
                }
                if (Py.Wahr(Get(daten, "restwert")))
                {
                    zeilen.Add("Übernahme am Ende (Restwert): " + Euro(daten["restwert"]));
                }
                return zeilen;
            }
        }
        return [];
    }

    // ------------------------------------------------- Terminal und Diagnose

    /// <summary>terminal_step_label: "Schritt 2 von 5".</summary>
    public static string TerminalSchritt(PyObjekt auftrag, long index) => Prozent("Schritt %d von %d", index + 1, (long)Liste(auftrag["schritte"]).Count);

    /// <summary>terminal_try: (art, befehl, ausgabe, hinweis); art richtig, falsch oder gefaehrlich.</summary>
    public static (string Art, string Befehl, List<string> Ausgabe, object? Hinweis) TerminalVersuch(PyObjekt auftrag, int schritt, int befehl)
    {
        var b = Objekt(Liste(Objekt(Liste(auftrag["schritte"])[schritt])["befehle"])[befehl]);
        var art = Py.Wahr(Get(b, "richtig")) ? "richtig" : Py.Wahr(Get(b, "gefaehrlich")) ? "gefaehrlich" : "falsch";
        return (art, Py.Str(b["befehl"]), ListeOderLeer(b, "ausgabe").Select(Py.Str).ToList(), Get(b, "hinweis", ""));
    }

    /// <summary>terminal_log: alles, was im Terminal steht, als (rolle, text).</summary>
    public static List<(string Rolle, string Text)> TerminalProtokoll(PyObjekt auftrag, IReadOnlyList<IReadOnlyList<int>> versuche)
    {
        var zeilen = ListeOderLeer(auftrag, "start").Select(z => ("start", Py.Str(z))).ToList();
        for (var s = 0; s < versuche.Count; s++)
        {
            foreach (var index in versuche[s])
            {
                var (art, befehl, ausgabe, _) = TerminalVersuch(auftrag, s, index);
                zeilen.Add(("eingabe", befehl));
                var rolle = art switch { "richtig" => "ausgabe", "falsch" => "fehler", _ => "gefahr" };
                zeilen.AddRange(ausgabe.Select(z => (z.StartsWith("# ", StringComparison.Ordinal) ? "kommentar" : rolle, z)));
            }
        }
        return zeilen;
    }

    /// <summary>terminal_current_step: Index des Schritts, der dran ist (null, wenn alle erledigt).</summary>
    public static int? TerminalAktuell(PyObjekt auftrag, IReadOnlyList<IReadOnlyList<int>> versuche)
    {
        var schritte = Liste(auftrag["schritte"]).Select(Objekt).ToList();
        for (var i = 0; i < schritte.Count; i++)
        {
            var v = i < versuche.Count ? versuche[i] : [];
            if (RichtigerBefehl(schritte[i]) is not int r || !v.Contains(r))
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>terminal_commands: die richtigen Befehle der Reihe nach.</summary>
    public static List<string> TerminalBefehle(PyObjekt auftrag) =>
        [.. Liste(auftrag["schritte"]).Select(Objekt).Select(s => Py.Str(Objekt(Liste(s["befehle"])[RichtigerBefehl(s)!.Value])["befehl"]))];

    /// <summary>diagnosis_counter</summary>
    public static string DiagnoseZaehler(PyObjekt auftrag, int erledigt) =>
        Prozent("%d Prüfung%s durchgeführt · sinnvoll sind etwa %d", (long)erledigt, erledigt == 1 ? "" : "en", auftrag["ziel_pruefungen"]);

    /// <summary>spare_parts_text: "Im Lager bereit: ..." fuer Diagnosen mit Ersatzteil.</summary>
    public static string ErsatzImLager(PyObjekt auftrag, IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt)
    {
        if (!Py.Wahr(Get(auftrag, "aus_lager")))
        {
            return "";
        }
        var namen = new List<string>();
        foreach (var kennung in verfuegbar ?? [])
        {
            var teil = Teil(kennung, inhalt);
            if (teil is not null && Py.Enthaelt(auftrag["aus_lager"], teil["typ"]) && !namen.Contains(Py.Str(teil["name"])))
            {
                namen.Add(Py.Str(teil["name"]));
            }
        }
        return namen.Count == 0 ? "Im Lager liegt gerade kein passendes Ersatzteil." : "Im Lager bereit: " + string.Join(", ", namen);
    }

    /// <summary>spare_specs: Merkmale eines Ersatzteils als kurze Zeile.</summary>
    public static string ErsatzDaten(PyObjekt teil, PyObjekt inhalt) =>
        string.Join(" · ", ErsatzMerkmale(teil["typ"], inhalt).Where(m => Get(teil, Py.Str(m.Feld)) is not null)
            .Select(m => MerkmalWert(Get(teil, Py.Str(m.Feld)), m.Einheit)));

    /// <summary>exchange_steps: alle Schritte zur Auswahl (richtige und falsche), fest gemischt.</summary>
    public static List<string> AustauschSchritte(PyObjekt auftrag)
    {
        var austausch = Get(auftrag, "austausch") as PyObjekt ?? [];
        var id = Py.Str(auftrag["id"]);
        return [.. ListeOderLeer(austausch, "schritte").Concat(ListeOderLeer(austausch, "falsch")).Select(Py.Str).OrderBy(t => Wuerfel.Werfen(id, 0, t))];
    }

    /// <summary>maintenance_counter</summary>
    public static string WartungZaehler(PyObjekt auftrag, PyObjekt? bewertung)
    {
        var punkte = Liste(auftrag["pruefpunkte"]).Select(Objekt).ToList();
        long erledigt = (bewertung ?? []).Count(p => punkte.Any(q => Py.Gleich(q["id"], p.Key)));
        long auffaellig = (bewertung ?? []).Count(p => Py.Gleich(p.Value, BewertungAuffaellig));
        return Prozent("%d von %d Prüfpunkten bewertet · %d auffällig", erledigt, (long)punkte.Count, auffaellig);
    }

    // ------------------------------------------------- Bauteile und Bestellung

    /// <summary>cart_lines: "2 × DDR4 16 GB (2 × 8 GB) von Bitlager24".</summary>
    public static List<string> KorbZeilen(PyObjekt auftrag, PyObjekt? korb, PyObjekt inhalt)
    {
        var zeilen = new List<string>();
        foreach (var angebot in Liste(auftrag["angebote"]).Select(Objekt))
        {
            var wert = korb is null ? null : Get(korb, Py.Str(angebot["id"]), 0L);
            var anzahl = Py.Wahr(wert) ? Py.Int(wert) : 0;
            if (anzahl != 0)
            {
                var haendler = Liste(Hw(inhalt)["haendler"]).Select(Objekt).First(h => Py.Gleich(h["id"], angebot["haendler"]));
                zeilen.Add(Prozent("%d × %s von %s", anzahl, Teil(angebot["teil"], inhalt)!["name"], haendler["name"]));
            }
        }
        return zeilen;
    }

    private static string Groesse(object? groesse)
    {
        var g = Py.Int(groesse);
        return g % 1000 == 0 ? Prozent("%d TB", g / 1000) : Prozent("%d GB", g);
    }

    /// <summary>part_specs: kurze technische Beschreibung eines Bauteils.</summary>
    public static string TeilDaten(PyObjekt teil, PyObjekt inhalt)
    {
        var art = Py.Str(teil["typ"]);
        switch (art)
        {
            case "mainboard":
                var m2 = Py.Wahr(teil["m2"]) ? Prozent("%d × M.2", teil["m2"]) : "kein M.2";
                return Prozent("Sockel %s · %s · %s · %d RAM-Plätze · %s", teil["sockel"], teil["ram_typ"], FormName(teil["formfaktor"], inhalt), teil["ram_slots"], m2);
            case "cpu":
                return Prozent("Sockel %s · %d W · %s", teil["sockel"], teil["tdp"], Py.Wahr(teil["igpu"]) ? "mit Grafik" : "ohne Grafik");
            case "ram":
                return Prozent("%s · %d GB · %d Modul%s", teil["ram_typ"], teil["groesse"], teil["module"], Py.Gleich(teil["module"], 1L) ? "" : "e");
            case "ssd":
                return Prozent("%s · %s", Py.Gleich(teil["anschluss"], "m2") ? "M.2 (NVMe)" : "SATA", Groesse(teil["groesse"]));
            case "gpu":
                return Prozent("braucht %d W", teil["watt"]);
            case "netzteil":
                return Prozent("liefert %d W", teil["watt"]);
            case "gehaeuse":
                return "für " + string.Join(", ", Liste(teil["formfaktoren"]).Select(f => FormName(f, inhalt)));
            case "notebook":
                return Prozent("%d GB RAM", teil["groesse"]);
        }
        if (Get(Hw(inhalt), "rack_typen") is PyObjekt r && Py.Hat(r, art))
        {
            return RackDaten(teil);
        }
        if (Get(Hw(inhalt), "ersatz_typen") is PyObjekt e && Py.Hat(e, art))
        {
            return ErsatzDaten(teil, inhalt);
        }
        return "";
    }

    /// <summary>build_rules_text: Vorgaben eines PC-Auftrags in einer Zeile.</summary>
    public static string BauVorgaben(PyObjekt auftrag)
    {
        var regeln = Get(auftrag, "vorgaben") as PyObjekt ?? [];
        var teile = new List<string>();
        if (Py.Wahr(Get(regeln, "ram_min")))
        {
            teile.Add(Prozent("mind. %d GB RAM", regeln["ram_min"]));
        }
        if (Py.Wahr(Get(regeln, "speicher_min")))
        {
            teile.Add(Prozent("mind. %s SSD", Groesse(regeln["speicher_min"])));
        }
        if (Py.Wahr(Get(regeln, "grafikkarte")))
        {
            teile.Add("Grafikkarte nötig");
        }
        if (Py.Wahr(Get(regeln, "budget")))
        {
            teile.Add(Prozent("höchstens %d €", regeln["budget"]));
        }
        return string.Join(" · ", teile);
    }

    /// <summary>order_header: Bedarf, Budget und Frist als Zeilen.</summary>
    public static List<string> BestellKopf(PyObjekt auftrag)
    {
        var zeilen = Liste(auftrag["bedarf"]).Select(Objekt).Select(b => Prozent("%d × %s", b["menge"], b["text"])).ToList();
        var tage = auftrag["frist"];
        zeilen.Add(Prozent("Budget %d € · Frist %d Arbeitstag%s", auftrag["budget"], tage, Py.Gleich(tage, 1L) ? "" : "e"));
        return zeilen;
    }

    /// <summary>cart_summary: Live-Anzeige unter dem Warenkorb (Text, zu_teuer, zu_spaet).</summary>
    public static (string Text, bool ZuTeuer, bool ZuSpaet) KorbZusammenfassung(PyObjekt auftrag, PyObjekt? korb)
    {
        var (summe, laengste) = KorbSumme(auftrag, korb);
        long anzahl = 0;
        foreach (var (_, v) in korb ?? [])
        {
            anzahl += Py.Wahr(v) ? Py.Int(v) : 0;
        }
        if (anzahl == 0)
        {
            return ("leer", false, false);
        }
        var text = Prozent("%d Artikel · %d € von %d € · Lieferung in %d Arbeitstag%s", anzahl, summe, auftrag["budget"], laengste, Py.Gleich(laengste, 1L) ? "" : "en");
        return (text, Py.Kleiner(auftrag["budget"], summe), Py.Kleiner(auftrag["frist"], laengste));
    }

    /// <summary>solution_text: eine richtige Loesung als Text (Rueckmeldung nach Fehlern).</summary>
    public static string LoesungText(PyObjekt auftrag, IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt)
    {
        var loesung = Loesung(auftrag, verfuegbar, inhalt);
        if (!Py.Wahr(loesung))
        {
            return "";
        }
        switch (Py.Str(auftrag["typ"]))
        {
            case "bestellung":
                return "So hätte es gepasst: " + string.Join(", ", KorbZeilen(auftrag, (PyObjekt)loesung!, inhalt)) + ".";
            case "bauteile":
                var pc = (PyObjekt)loesung!;
                return "Eine passende Zusammenstellung: " + string.Join(", ", Liste(auftrag["slots"]).Where(p => Py.Wahr(Get(pc, Py.Str(p))))
                    .Select(p => Py.Str(Teil(pc[Py.Str(p)], inhalt)!["name"]))) + ".";
            case "rack":
                return "Eine passende Belegung: " + string.Join(", ", RackZeilen(auftrag, (PyObjekt)loesung!, inhalt)) + ".";
            case "terminal":
                return "Richtiger Ablauf: " + string.Join(" → ", TerminalBefehle(auftrag));
            case "diagnose":
                var text = Prozent("Richtig: Ursache – %s. Maßnahme – %s.", Py.Str(auftrag["ursache"]).TrimEnd('.'), Py.Str(auftrag["massnahme"]).TrimEnd('.'));
                if (Get((PyObjekt)loesung!, "teil") is { } teil && Py.Wahr(teil))
                {
                    text += Prozent(" Ersatzteil – %s.", Teil(teil, inhalt)!["name"]);
                }
                return text;
            case "wartung":
                var auffaellig = Liste(auftrag["pruefpunkte"]).Select(Objekt).Where(p => Py.Wahr(Get(p, "auffaellig"))).Select(p => Py.Str(p["text"]).TrimEnd('.')).ToList();
                var t = auffaellig.Count > 0 ? Prozent("Auffällig: %s.", string.Join(", ", auffaellig)) : "Alle Prüfpunkte sind in Ordnung.";
                return Prozent("%s Abschluss – %s.", t, Py.Str(auftrag["abschluss_antwort"]).TrimEnd('.'));
        }
        return "";
    }

    /// <summary>rank_hint: was bis zur naechsten Befoerderung fehlt (oder "").</summary>
    public static string RangHinweis(Spielstand stand)
    {
        if (stand.NaechsterRang() is not { } naechster)
        {
            return "";
        }
        var (name, ansehen, tage) = (naechster[0], naechster[1], naechster[2]);
        var fehlt = new List<string>();
        if (Py.Wahr(tage))
        {
            fehlt.Add(Prozent("noch %d Arbeitstag%s", tage, Py.Gleich(tage, 1L) ? "" : "e"));
        }
        if (Py.Wahr(ansehen))
        {
            fehlt.Add(Prozent("noch %d %% Ansehen", ansehen));
        }
        return fehlt.Count == 0 ? Prozent("Nächster Rang: %s ab dem nächsten Arbeitstag", name) : Prozent("Nächster Rang: %s, %s", name, string.Join(" und ", fehlt));
    }
}
