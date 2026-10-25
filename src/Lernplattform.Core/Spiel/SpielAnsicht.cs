using Lernplattform.Core.Daten;
using Lernplattform.Core.Zufall;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Suche;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W6: die Hilfen aus <c>fisi_game.py</c> (Python 0.63.2), die nur die Spielansichten
/// brauchen und bisher fehlten: Rueckmeldung nach einem Ticket (result_text, reaction_text),
/// passende Lerninhalte (learn_links), Rack-Werte (rack_bars, rack_checks, RACK_COLORS), Diagnose,
/// Wartung, Formular, Ersatzteile (spare_kinds, spare_catalog, spare_delivery_days), Orte
/// (open_places, place_label, site_name, branch_empty_hint, room, dealer), tickets_in_room und
/// slot_delete_question. Nur lesend, Vergleichswerte G50.
/// </summary>
public static class SpielAnsicht
{
    /// <summary>PROBLEM_TYPES: Aufgabentypen mit einer Liste von Problemen in der Auswertung.</summary>
    public static readonly IReadOnlySet<string> ProblemTypen = new HashSet<string>(StringComparer.Ordinal)
    {
        "bauteile", "bestellung", "rack", "formular", "terminal", "diagnose", "wartung", "bestueckung",
    };

    public const string BewertungOk = "ok";               // RATING_OK
    public const string BewertungAuffaellig = "auffaellig";  // RATING_ISSUE

    /// <summary>RATING_TEXT</summary>
    public static readonly IReadOnlyDictionary<string, string> BewertungText = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [BewertungOk] = "in Ordnung", [BewertungAuffaellig] = "auffällig",
    };

    /// <summary>RACK_COLORS als Namen der Farbwelt.</summary>
    public static readonly IReadOnlyDictionary<string, string> RackFarben = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["usv"] = "yellow", ["server"] = "cyan", ["storage"] = "purple", ["switch"] = "green", ["patchpanel"] = "pink",
    };

    /// <summary>JOURNEY_GROUPS</summary>
    public static readonly IReadOnlyList<(string Id, string Name)> ReiseGruppen =
        [("alle", "Alles"), ("story", "Story"), ("karriere", "Karriere"), ("firma", "Firma")];

    /// <summary>slot_delete_question</summary>
    public static string LoeschFrage(PyObjekt platz) =>
        Prozent("%s wirklich löschen?\n\nDer Spielstand ist danach auf PC und Handy weg "
            + "und lässt sich nicht wiederherstellen. Bestwerte und Abzeichen bleiben "
            + "in der Bestenliste.", Spielstaende.Zeile(platz));

    // -- Rueckmeldung nach dem Einreichen ------------------------------------

    /// <summary>reaction_text: kurzer Satz der Person, die den Auftrag gestellt hat.</summary>
    public static string Reaktion(PyObjekt auftrag, PyObjekt daten, PyObjekt inhalt)
    {
        var person = Person(auftrag["auftraggeber"], inhalt);
        if (person is null || Py.Wahr(Get(auftrag, "falschlieferung")))
        {
            return "";
        }
        var reaktionen = Get(person, "reaktionen") as PyObjekt ?? [];
        var texte = Get(reaktionen, Py.Wahr(daten["richtig"]) ? "richtig" : "falsch") is List<object?> l ? l : [];
        if (texte.Count == 0)
        {
            return "";
        }
        var text = texte[(int)(Wuerfel.Werfen(Py.Str(auftrag["id"]), Py.Int(Get(daten, "tag", 0L)), "reaktion") * texte.Count)];
        return Prozent("%s: „%s“", Betrieb.KurzName(person["name"]), text);
    }

    private static string Mal(object? anzahl, string einzahl, string mehrzahl) => Plural(anzahl, einzahl, mehrzahl);

    /// <summary>result_text: Rueckmeldung nach dem Bearbeiten eines Tickets (Zeilen mit "\n").</summary>
    public static string Ergebnis(PyObjekt auftrag, PyObjekt daten, IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt)
    {
        var zeilen = new List<string>();
        var reaktion = Reaktion(auftrag, daten, inhalt);
        var richtig = Py.Wahr(daten["richtig"]);
        var typ = Py.Str(auftrag["typ"]);
        IEnumerable<string> Punkte() => (Get(daten, "probleme") is List<object?> p ? p : []).Select(t => "• " + Py.Str(t));
        if (richtig)
        {
            zeilen.Add("Richtig gelöst!");
        }
        else if (typ == "zuordnung")
        {
            zeilen.Add(Prozent("Leider nicht ganz: %s %s nicht.", Mal(daten["fehler"], "Zuordnung", "Zuordnungen"),
                Py.Gleich(daten["fehler"], 1L) ? "stimmt" : "stimmen"));
        }
        else if (typ == "terminal")
        {
            var fehlgriffe = Get(daten, "fehlgriffe", 0L);
            var erlaubt = Get(auftrag, "fehlgriffe_erlaubt", Objekt(inhalt["balancing"])["terminal_fehlgriffe_erlaubt"]);
            if (Py.Wahr(Get(daten, "gefahr")))
            {
                zeilen.Add("Leider nicht ganz: Ein gefährlicher Befehl war dabei.");
            }
            else if (Py.Kleiner(erlaubt, fehlgriffe))
            {
                zeilen.Add(Prozent("Leider nicht ganz: %d Fehlgriffe, erlaubt %s %d.", fehlgriffe, Py.Gleich(erlaubt, 1L) ? "ist" : "sind", erlaubt));
            }
            else
            {
                zeilen.Add("Leider nicht ganz: Nicht alle Schritte sind erledigt.");
            }
            zeilen.AddRange(Punkte());
        }
        else if (typ is "diagnose" or "wartung")
        {
            zeilen.Add("Leider nicht ganz.");
            zeilen.AddRange(Punkte());
            zeilen.Add(SpielTexte.LoesungText(auftrag, verfuegbar, inhalt));
        }
        else if (typ == "formular")
        {
            var anzahl = daten["fehler"];
            zeilen.Add(Prozent("Leider nicht ganz: %s.", Py.Gleich(anzahl, 1L) ? "1 Feld stimmt nicht" : Prozent("%d Felder stimmen nicht", anzahl)));
            zeilen.AddRange(Punkte());
        }
        else if (ProblemTypen.Contains(typ))
        {
            var anzahl = daten["fehler"];
            zeilen.Add(Prozent("Leider nicht ganz: %s gefunden.", Py.Gleich(anzahl, 1L) ? "1 Problem" : Prozent("%d Probleme", anzahl)));
            zeilen.AddRange(Punkte());
            var hinweis = SpielTexte.LoesungText(auftrag, verfuegbar, inhalt);
            if (hinweis.Length > 0)
            {
                zeilen.Add(hinweis);
            }
        }
        else
        {
            zeilen.Add(Prozent("Leider falsch. Richtig wäre: %s", auftrag["antwort"]));
        }
        if (richtig && typ == "terminal" && Py.Wahr(Get(daten, "fehlgriffe")))
        {
            zeilen.Add("Ein Fehlgriff war dabei, das ist noch erlaubt.");
        }
        if (richtig && typ == "diagnose")
        {
            var anzahl = Get(daten, "pruefungen", 0L);
            var endung = Py.Gleich(anzahl, 1L) ? "" : "en";
            zeilen.Add(Py.Wahr(Get(daten, "systematisch"))
                ? Prozent("Systematisch vorgegangen: Fehler mit %d Prüfung%s gefunden.", anzahl, endung)
                : Prozent("Fehler gefunden mit %d Prüfung%s, %d hätten gereicht.", anzahl, endung, auftrag["ziel_pruefungen"]));
        }
        if (richtig && typ == "diagnose" && Py.Wahr(Get(daten, "aus_lager")))
        {
            zeilen.Add(Prozent("Ersatzteil aus dem Lager eingebaut: %s.", Teil(Liste(daten["aus_lager"])[0], inhalt)!["name"]));
        }
        if (richtig && Py.Wahr(Get(auftrag, "austausch")))
        {
            zeilen.Add(Prozent("Funktionstest: %s", Objekt(auftrag["austausch"])["test"]));
        }
        if (richtig && typ == "wartung")
        {
            var auffaellig = Liste(auftrag["pruefpunkte"]).Count(p => Py.Wahr(Get(Objekt(p), "auffaellig")));
            var folge = Liste(inhalt["aufgaben"]).Select(Objekt).Where(a => Py.Gleich(Get(a, "nach"), auftrag["id"])).ToList();
            zeilen.Add(auffaellig == 0
                ? "Wartung dokumentiert: alles in Ordnung."
                : Prozent("Wartung dokumentiert: %s.%s", auffaellig == 1 ? "1 Auffälligkeit" : Prozent("%d Auffälligkeiten", (long)auffaellig),
                    folge.Count > 0 ? Prozent(" Folgeauftrag ab dem nächsten Arbeitstag: %s.", folge[0]["titel"]) : ""));
        }
        if (Py.Wahr(Get(daten, "lieferung")))
        {
            object? letzte = null;
            foreach (var l in Liste(daten["lieferung"]))
            {
                var ankunft = Objekt(l)["ankunft"];
                letzte = letzte is null ? ankunft : Py.Max(letzte, ankunft);
            }
            zeilen.Add(Py.Wahr(Get(daten, "fehllieferung"))
                ? Prozent("Die Bestellung über %d € geht trotzdem raus und kommt an Arbeitstag %d. Dann meldet sich Rainer im Lager wegen der falschen Ware.",
                    Get(daten, "kosten", 0L), letzte)
                : Prozent("Bestellt für %d €. Die Ware kommt an Arbeitstag %d.", Get(daten, "kosten", 0L), letzte));
        }
        if (Py.Wahr(Get(daten, "bonus_verloren")))
        {
            zeilen.Add("Kein Spar-Bonus: Der ist durch die Falschlieferung verloren.");
        }
        if (Py.Wahr(Get(daten, "falschlieferung")) && richtig)
        {
            var zurueck = Get(daten, "ruecksendung") as PyObjekt ?? [];
            if (zurueck.Count > 0)
            {
                zeilen.Add(Prozent("Zurückgeschickt: %s.", string.Join(", ", zurueck.Select(p => Prozent("%d × %s", p.Value, Teil(p.Key, inhalt)!["name"])))));
            }
            zeilen.Add(Prozent("Rücksendekosten: -%d €. Die Bestellung kommt jetzt wieder, dann ohne Spar-Bonus.", Get(daten, "ruecksendekosten", 0L)));
        }
        if (Py.Wahr(Get(daten, "ersparnis_bonus")))
        {
            zeilen.Add(Prozent("Sparsam bestellt: +%d € Bonus.", daten["ersparnis_bonus"]));
        }
        if (Py.Wahr(Get(daten, "aus_lager")) && typ == "rack")
        {
            zeilen.Add(Prozent("Aus dem Lager eingebaut: %s.", string.Join(", ", Liste(daten["aus_lager"]).Select(t => Py.Str(Teil(t, inhalt)!["name"])))));
        }
        if (reaktion.Length > 0)
        {
            zeilen.Add(reaktion);
        }
        var geld = daten["geld"];
        zeilen.Add(Prozent("%s%d € Spielgeld", Py.Kleiner(geld, 0L) ? "" : "+", geld));
        foreach (var (schluessel, wert) in Objekt(daten["reputation"]))
        {
            zeilen.Add(Prozent("%s%d %s", Py.Kleiner(wert, 0L) ? "" : "+", wert,
                Betrieb.AchsenName.TryGetValue(schluessel, out var name) ? name : schluessel));
        }
        if (richtig && !Py.Wahr(daten["hilfe"]))
        {
            zeilen.Add("Bonus: ohne Hilfe gelöst.");
        }
        if (Py.Wahr(daten["unter_niveau"]) && !richtig)
        {
            zeilen.Add("Doppelter Reputationsverlust, weil dir hier noch Wissen fehlt.");
        }
        return string.Join("\n", zeilen);
    }

    // -- Passend dazu lernen ---------------------------------------------------

    /// <summary>
    /// learn_links: passende Karteikarten und Pruefungsfragen zu einer Aufgabe, bevorzugt aus ihrem
    /// Fachbereich und mit dem Begriff in der Frage selbst (Treffer wie search_content).
    /// </summary>
    public static List<SuchTreffer> LernLinks(PyObjekt auftrag, Suchtext suche, int grenze = 6)
    {
        var heim = Get(auftrag, "cat") is string cat && SpielThemen.Jetzt.Name.TryGetValue(cat, out var n) ? n : null;
        var gefunden = new List<SuchTreffer>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var begriff in Get(auftrag, "suchbegriffe") is List<object?> b ? b : [])
        {
            var wort = Py.Str(begriff);
            var treffer = suche.Suchen(wort).Where(t => t.Art is Texte.SucheTexte.ArtKarte or Texte.SucheTexte.ArtQuiz).Take(50).ToList();
            var klein = PythonKlein.Klein(wort);
            // sorted ist stabil wie OrderBy
            foreach (var t in treffer.OrderBy(t => t.Fachbereich != heim).ThenBy(t => !PythonKlein.Klein(t.Titel).Contains(klein, StringComparison.Ordinal)))
            {
                if (gesehen.Add(t.Titel))
                {
                    gefunden.Add(t);
                }
            }
        }
        return [.. gefunden.Take(grenze)];
    }

    // -- Rack ----------------------------------------------------------------

    private static long Leistung(List<Bewertung.RackPlatz> belegt) =>
        Py.Int(Py.Summe(belegt.Where(r => Py.Str(r.Geraet["typ"]) != "usv").Select(r => r.Geraet["watt"])));

    private static long? UsvLeistung(PyObjekt auftrag, List<Bewertung.RackPlatz> belegt, PyObjekt inhalt)
    {
        var regeln = Objekt(Objekt(inhalt["hardware"])["rack_regeln"]);
        var summe = Py.Plus(Py.Summe(belegt.Where(r => Py.Str(r.Geraet["typ"]) == "usv").Select(r => Get(r.Geraet, "leistung", 0L))),
            Get(Objekt(auftrag["schrank"]), "usv_watt", 0L));
        return !Py.Wahr(summe) ? null : Py.Int(Py.Mal(summe, Py.Minus(1L, regeln["strom_reserve"])));
    }

    private static string TypName(object? art, PyObjekt inhalt) =>
        Py.Str(Get(Get(Objekt(inhalt["hardware"]), "rack_typen") as PyObjekt ?? [], Py.Str(art), art));

    /// <summary>
    /// rack_bars: Balken der Rack-Ansicht [{"id", "name", "wert", "grenze", "text", "anteil", "zu_viel"}].
    /// watt: Leistungsaufnahme aus einer Bestueckung (sonst die Typenschild-Werte).
    /// </summary>
    public static List<PyObjekt> RackBalken(PyObjekt auftrag, PyObjekt? antwort, PyObjekt inhalt, object? watt = null)
    {
        var belegt = Bewertung.RackBelegt(auftrag, antwort, inhalt);
        var schrank = Objekt(auftrag["schrank"]);
        var benutzt = Py.Summe(belegt.Select(r => r.Geraet["he"]));
        var gewicht = Py.Summe(belegt.Select(r => r.Geraet["gewicht"]));
        var strom = watt is null ? Leistung(belegt) : Py.Int(watt);
        var usv = UsvLeistung(auftrag, belegt, inhalt);
        var reserve = PythonRunden.Runde(Py.F(Py.Mal(Objekt(Objekt(inhalt["hardware"])["rack_regeln"])["strom_reserve"], 100L)));
        var balken = new List<PyObjekt>();
        void Neu(string id, string name, object wert, object? grenze, string text)
        {
            object anteil = Py.Wahr(grenze) ? Math.Min(1.0, Py.F(wert) / Py.F(grenze)) : (Py.Wahr(wert) ? 1.0 : 0.0);
            balken.Add(new PyObjekt
            {
                { "id", id }, { "name", name }, { "wert", wert }, { "grenze", grenze }, { "text", text }, { "anteil", anteil },
                { "zu_viel", Py.Wahr(grenze) && Py.Kleiner(grenze, wert) },
            });
        }
        Neu("he", "Höheneinheiten", benutzt, schrank["he"], Prozent("%d von %d HE belegt", benutzt, schrank["he"]));
        if (usv is { } leistung && leistung != 0)
        {
            Neu("strom", Prozent("Strom (USV, %d %% Reserve)", reserve), strom, leistung, Prozent("%s W von %s W", Num(strom), Num(leistung)));
        }
        else
        {
            balken.Add(new PyObjekt
            {
                { "id", "strom" }, { "name", "Strom (USV)" }, { "wert", strom }, { "grenze", 0L },
                { "text", Prozent("%s W, noch keine USV", Num(strom)) }, { "anteil", strom != 0 ? (object)1.0 : 0L }, { "zu_viel", false },
            });
        }
        if (Py.Wahr(Get(schrank, "kuehlung")))
        {
            Neu("kuehlung", "Kühlung (Abwärme)", strom, schrank["kuehlung"], Prozent("%s W von %s W", Num(strom), Num(schrank["kuehlung"])));
        }
        Neu("gewicht", "Gewicht (Traglast)", gewicht, schrank["traglast"], Prozent("%d kg von %d kg", gewicht, schrank["traglast"]));
        return balken;
    }

    /// <summary>rack_checks: Vorgaben und Einbauregeln mit Haken [{"text", "ok"}].</summary>
    public static List<PyObjekt> RackPruefungen(PyObjekt auftrag, PyObjekt? antwort, PyObjekt inhalt, object? watt = null)
    {
        var regeln = Objekt(Objekt(inhalt["hardware"])["rack_regeln"]);
        var belegt = Bewertung.RackBelegt(auftrag, antwort, inhalt);
        var schrank = Objekt(auftrag["schrank"]);
        var liste = new List<PyObjekt>();
        void Neu(string text, bool ok) => liste.Add(new PyObjekt { { "text", text }, { "ok", ok } });
        if (belegt.Count == 0)
        {
            Neu("Mindestens ein Gerät eingebaut", false);
        }
        var vorgaben = Get(auftrag, "vorgaben") as PyObjekt ?? [];
        var reihenfolge = (Get(Objekt(inhalt["hardware"]), "rack_typen") as PyObjekt ?? []).Select(p => p.Key).ToList();
        var mindestens = Get(vorgaben, "mindestens") as PyObjekt ?? [];
        foreach (var (art, anzahl) in mindestens.OrderBy(p => reihenfolge.IndexOf(p.Key) is var i && i >= 0 ? i : reihenfolge.Count))
        {
            var da = (long)belegt.Count(r => Py.Gleich(r.Geraet["typ"], art));
            Neu(Prozent("%d × %s eingebaut (jetzt %d)", anzahl, TypName(art, inhalt), da), !Py.Kleiner(da, anzahl));
        }
        if (Py.Wahr(Get(vorgaben, "ports_min")))
        {
            foreach (var art in (string[])["switch", "patchpanel"])
            {
                var ports = Py.Summe(belegt.Where(r => Py.Gleich(r.Geraet["typ"], art)).Select(r => Get(r.Geraet, "ports", 0L)));
                Neu(Prozent("mind. %d Ports %s (jetzt %d)", vorgaben["ports_min"], TypName(art, inhalt), ports), !Py.Kleiner(ports, vorgaben["ports_min"]));
            }
        }
        if (Py.Wahr(Get(vorgaben, "frei_min")))
        {
            var frei = Py.Minus(schrank["he"], Py.Summe(belegt.Select(r => r.Geraet["he"])));
            Neu(Prozent("%d HE frei für später (jetzt %d)", vorgaben["frei_min"], Py.Max(0L, frei)), !Py.Kleiner(frei, vorgaben["frei_min"]));
        }
        var groesse = Py.Int(schrank["he"]);
        var benutzt = new HashSet<long>();
        bool ueberlappt = false, draussen = false;
        foreach (var r in belegt)
        {
            var oben = r.Unten + Py.Int(r.Geraet["he"]) - 1;
            if (oben > groesse)
            {
                draussen = true;
                continue;
            }
            for (var he = r.Unten; he <= oben; he++)
            {
                ueberlappt |= !benutzt.Add(he);
            }
        }
        Neu("Alles im Schrank, nichts überlappt", !ueberlappt && !draussen);
        var grenze = Math.Max(1, (long)Math.Floor(groesse / 3.0));
        var schwer = belegt.Where(r => !Py.Kleiner(r.Geraet["gewicht"], regeln["schwer_ab_kg"])).ToList();
        if (schwer.Count > 0)
        {
            Neu(Prozent("Schwere Geräte (ab %d kg) unten, Beginn bis HE %d", regeln["schwer_ab_kg"], grenze), schwer.All(r => r.Unten <= grenze));
        }
        var usvListe = belegt.Where(r => Py.Gleich(r.Geraet["typ"], "usv")).ToList();
        var andere = belegt.Where(r => !Py.Gleich(r.Geraet["typ"], "usv")).ToList();
        if (usvListe.Count > 0)
        {
            Neu("USV ganz unten", andere.Count == 0 || usvListe.Max(r => r.Unten) <= andere.Min(r => r.Unten));
        }
        if (belegt.Any(r => Py.Str(r.Geraet["typ"]) is "switch" or "patchpanel"))
        {
            Neu("Patchpanel direkt neben einem Switch",
                !Bewertung.RackProbleme(auftrag, antwort, inhalt).Any(t => t.Contains("direkt darüber oder darunter", StringComparison.Ordinal)));
        }
        var gewicht = Py.Summe(belegt.Select(r => r.Geraet["gewicht"]));
        Neu(Prozent("Traglast %d kg (jetzt %d kg)", schrank["traglast"], gewicht), !Py.Kleiner(schrank["traglast"], gewicht));
        var strom = watt is null ? Leistung(belegt) : Py.Int(watt);
        var usv = UsvLeistung(auftrag, belegt, inhalt);
        if (usv is { } leistung)
        {
            Neu(Prozent("USV reicht mit %d %% Reserve", (long)PythonRunden.Runde(Py.F(Py.Mal(regeln["strom_reserve"], 100L)))), strom <= leistung);
        }
        if (Py.Wahr(Get(schrank, "kuehlung")))
        {
            Neu("Kühlung schafft die Abwärme", !Py.Kleiner(schrank["kuehlung"], strom));
        }
        return liste;
    }

    // -- Diagnose, Wartung, Formular -------------------------------------------

    /// <summary>diagnosis_check</summary>
    public static PyObjekt? DiagnosePruefung(PyObjekt auftrag, object? kennung) =>
        Liste(auftrag["pruefungen"]).Select(Objekt).FirstOrDefault(p => Py.Gleich(p["id"], kennung));

    /// <summary>diagnosis_key_checks</summary>
    public static List<object?> DiagnoseWichtig(PyObjekt auftrag) =>
        [.. Liste(auftrag["pruefungen"]).Select(Objekt).Where(p => Py.Wahr(Get(p, "entscheidend"))).Select(p => p["id"])];

    /// <summary>maintenance_point</summary>
    public static PyObjekt? WartungPunkt(PyObjekt auftrag, object? kennung) =>
        ListeOderLeer(auftrag, "pruefpunkte").Select(Objekt).FirstOrDefault(p => Py.Gleich(p["id"], kennung));

    /// <summary>form_check: {feld_id: richtig} fuer alle Felder.</summary>
    public static PyObjekt FormPruefung(PyObjekt auftrag, PyObjekt? antwort)
    {
        var ergebnis = new PyObjekt();
        foreach (var feld in Bewertung.FormFelder(auftrag))
        {
            ergebnis.Add(feld.Id, Bewertung.FeldRichtig(feld, antwort is null ? null : Get(antwort, feld.Id)));
        }
        return ergebnis;
    }

    // -- Ersatzteile -----------------------------------------------------------

    /// <summary>spare_kinds: alle Teile-Typen, die ausgetauscht werden koennen ({typ: Name}).</summary>
    public static PyObjekt ErsatzTypen(PyObjekt inhalt)
    {
        var hardware = Objekt(inhalt["hardware"]);
        var typen = new PyObjekt();
        foreach (var quelle in (string[])["typen", "ersatz_typen"])
        {
            foreach (var (k, v) in Get(hardware, quelle) as PyObjekt ?? [])
            {
                Py.Setzen(typen, k, v);
            }
        }
        return typen;
    }

    /// <summary>spare_catalog: alle Teile, die fuer einen Austausch infrage kommen (gleicher Typ).</summary>
    public static List<PyObjekt> ErsatzKatalog(PyObjekt auftrag, PyObjekt inhalt)
    {
        var austausch = Get(auftrag, "austausch") as PyObjekt ?? [];
        var hardware = Objekt(inhalt["hardware"]);
        return [.. Liste(hardware["teile"]).Concat(Get(hardware, "ersatzteile") as List<object?> ?? []).Select(Objekt)
            .Where(t => Py.Gleich(t["typ"], Get(austausch, "typ")))];
    }

    /// <summary>spare_delivery_days</summary>
    public static long ErsatzLieferTage(PyObjekt inhalt) =>
        Py.Int(Get(Get(Objekt(inhalt["hardware"]), "nachbestellung") as PyObjekt ?? [], "lieferzeit", 1L));

    // -- Orte ------------------------------------------------------------------

    /// <summary>open_places: Kundenorte, die man schon besuchen kann (ab_tag erreicht).</summary>
    public static List<PyObjekt> OffeneOrte(Spielstand? stand, PyObjekt inhalt)
    {
        var tag = stand?.Tag ?? 1;
        return [.. Kundenorte(inhalt).Where(o => !Py.Kleiner(tag, Get(o, "ab_tag", 1L)))];
    }

    /// <summary>place_label: ab drei Orten die Kurzform.</summary>
    public static string OrtBeschriftung(PyObjekt ort, IReadOnlyCollection<PyObjekt> orte) =>
        Py.Str(orte.Count > 2 ? Get(ort, "kurz", ort["name"]) : ort["name"]);

    /// <summary>site_name</summary>
    public static string OrtName(string ort, PyObjekt inhalt, Spielstand? stand = null)
    {
        if (ort == OrtBuero)
        {
            return stand is not null && Py.Wahr(stand.Firma) ? Py.Str(Objekt(stand.Firma)["name"]) : Py.Str(Objekt(inhalt["gebaeude"])["firma"]);
        }
        if (ort == OrtZuhause)
        {
            return "Zuhause";
        }
        if (ort == OrtFiliale)
        {
            return stand is not null && Py.Wahr(stand.Zweigstelle) ? Py.Str(Objekt(stand.Zweigstelle)["name"]) : "Filiale";
        }
        var kunde = Kundenorte(inhalt).FirstOrDefault(o => Py.Gleich(o["id"], ort));
        return kunde is not null ? Py.Str(kunde["name"]) : ort;
    }

    /// <summary>branch_empty_hint: Text, wenn heute niemand in der Filiale ist (sonst "").</summary>
    public static string FilialeLeer(Spielstand? stand)
    {
        if (stand is null || !Py.Wahr(stand.Zweigstelle) || SpielOrte.Personen(OrtFiliale, stand).Count > 0)
        {
            return "";
        }
        var personal = stand.PersonalAm(OrtFiliale);
        return personal.Count == 0 ? "Noch niemand versetzt" : Prozent("Heute niemand da (%d abwesend)", (long)personal.Count);
    }

    /// <summary>room: Raum im Buero, bei einem Kunden oder in der eigenen Firma (letzte Ausbaustufe).</summary>
    public static PyObjekt? Raum(object? kennung, PyObjekt inhalt)
    {
        var gebaeude = new List<PyObjekt> { Objekt(inhalt["gebaeude"]) };
        gebaeude.AddRange(Kundenorte(inhalt).Select(o => Objekt(o["gebaeude"])));
        var stufen = Get(Get(Get(inhalt, "firma") as PyObjekt ?? [], "gebaeude") as PyObjekt ?? [], "stufen") as List<object?> ?? [];
        if (stufen.Count > 0)
        {
            gebaeude.Add(Objekt(Objekt(stufen[^1])["gebaeude"]));
        }
        foreach (var g in gebaeude)
        {
            foreach (var raum in Liste(g["raeume"]).Select(Objekt))
            {
                if (Py.Gleich(raum["id"], kennung))
                {
                    return raum;
                }
            }
        }
        return null;
    }

    /// <summary>dealer</summary>
    public static PyObjekt? Haendler(object? kennung, PyObjekt inhalt) =>
        Liste(Objekt(inhalt["hardware"])["haendler"]).Select(Objekt).FirstOrDefault(h => Py.Gleich(h["id"], kennung));

    /// <summary>tickets_in_room</summary>
    public static List<(PyObjekt Auftrag, string Status)> TicketsImRaum(Spielstand stand, object? raum) =>
        [.. stand.TicketsDesTages().Where(t => Py.Gleich(t.Auftrag["raum"], raum))];
}
