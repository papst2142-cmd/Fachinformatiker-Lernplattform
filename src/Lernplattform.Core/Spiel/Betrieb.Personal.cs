using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Zufall;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: die eigene Firma als Modulfunktionen von <c>fisi_game.py</c> (Python 0.63.2), Teil
/// Personal (Z. 6156-7230): Regeln, Werte je Thema, Bewerber, Weiterbildung, Macken, Stimmung,
/// Personal-Ereignisse und -Entscheidungen. Alle Zufallszahlen ueber den SHA-256-Wuerfel.
/// </summary>
public static partial class Betrieb
{
    public const string KollegePrefix = "kollege:";        // STAFF_PREFIX
    public const string Selbst = "ich";                    // SELF
    public const string AnfragePrefix = "anfrage:";        // INQUIRY_PREFIX
    public const string TicketPrefix = "kundenticket:";    // TICKET_PREFIX
    public const string ProjektPrefix = "projekt:";        // PROJECT_PREFIX
    public const int FirmenNameMax = 40;                   // FIRM_NAME_MAX

    public const string Coaching = "coaching";             // TRAINING_COACHING
    public const string AbwesendKrank = "krank";           // ABSENT_SICK
    public const string AbwesendUrlaub = "urlaub";         // ABSENT_VACATION
    public const string PersonalKrank = "mitarbeiter_krank";       // PERSONAL_SICK
    public const string PersonalUrlaub = "urlaub_angefragt";       // PERSONAL_VACATION
    public const string PersonalKonflikt = "konflikt";             // PERSONAL_CONFLICT
    public const string PersonalKuendigt = "kuendigung_selbst";    // PERSONAL_QUIT
    public const string PersonalWarnung = "unzufrieden";           // PERSONAL_WARN
    public const string PersonalSchwaecher = "macke_abgeschwaecht"; // PERSONAL_WEAKER
    public const string EntscheidungUrlaub = "urlaub";     // DECISION_VACATION
    public const string EntscheidungKonflikt = "konflikt"; // DECISION_CONFLICT
    public const string UrlaubJa = "ja";                   // VACATION_YES
    public const string UrlaubNein = "nein";               // VACATION_NO
    public const string Schlichten = "schlichten";         // CONFLICT_MEDIATE
    public const string SeiteA = "a";                      // CONFLICT_SIDE_A
    public const string SeiteB = "b";                      // CONFLICT_SIDE_B
    public const string Ignorieren = "ignorieren";         // CONFLICT_IGNORE
    public const string PersonalRueckhol = "rueckhol";     // PERSONAL_RECALL
    public const string EntscheidungRueckhol = "rueckhol"; // DECISION_RECALL
    public const string RueckholGehalt = "gehalt";         // RECALL_RAISE
    public const string RueckholBonus = "bonus";           // RECALL_BONUS
    public const string RueckholGehen = "gehen";           // RECALL_LET
    public const string AktionPersonal = "personal";       // ACTION_PERSONAL
    public const string EntscheidungenOffenText = "Erst die offenen Entscheidungen zum Personal treffen.";
    public const int SkillBalkenMax = 100;                 // SKILL_BAR_MAX

    /// <summary>HARDWARE_TOPICS</summary>
    public static readonly IReadOnlySet<string> HardwareThemen = new HashSet<string> { "hardware", "storage", "verkabelung" };

    /// <summary>LEARN_REASONS</summary>
    public static readonly IReadOnlyDictionary<string, string> LernGruende = new Dictionary<string, string>
    {
        ["ticket"] = "Kundenticket", ["projekt"] = "Projekt", ["routine"] = "Routinearbeit",
    };

    /// <summary>FIRM_TABS: Reiter im Unterpunkt "Firma" (PC und Handy gleich beschriftet).</summary>
    public static readonly IReadOnlyList<(string Id, string Name)> FirmenReiter =
    [
        ("auftraege", "Aufträge"), ("projekte", "Projekte"), ("mitarbeiter", "Mitarbeiter"), ("bewerbungen", "Bewerbungen"),
        ("gebaeude", "Gebäude"), ("marketing", "Marketing"), ("zertifizierungen", "Zertifizierungen"), ("finanzen", "Finanzen"),
        ("kredite", "Kredite"),
    ];

    public const string FirmaRuhigText = "Für heute ist alles verteilt und angeboten. Deine Leute kümmern sich "
        + "außerdem um Routineaufträge, die bringen jeden Arbeitstag Umsatz. "
        + "Die Ergebnisse der Kundentickets gibt es beim Feierabend.";   // FIRM_IDLE_TEXT

    public const string GruendungLockText = "Du hast alles, was du für eine eigene Firma brauchst: genug Erspartes, "
        + "gutes Ansehen und keinen offenen Auftrag mehr.";              // FOUNDING_TEASER

    private static SpielThemen T => SpielThemen.Jetzt;

    private static PyObjekt O(object? wert) => wert as PyObjekt ?? [];

    private static PyObjekt OderLeer(object? wert) => Py.Wahr(wert) ? Objekt(wert) : [];

    /// <summary>_dice</summary>
    internal static double Wurf(string startwert, long tag, string salz) => Wuerfel.Werfen(startwert, tag, salz);

    /// <summary>_pick</summary>
    internal static T2 Waehle<T2>(IReadOnlyList<T2> auswahl, string startwert, long runde, string salz) =>
        auswahl[(int)(Wurf(startwert, runde, salz) * auswahl.Count)];

    /// <summary>_between: ganze Zahl von unten bis oben (beide einschliesslich), fest gewuerfelt.</summary>
    internal static long Zwischen(string startwert, long tag, string salz, long unten, long oben) =>
        unten + Math.Min(oben - unten, (long)(Wurf(startwert, tag, salz) * (oben - unten + 1)));

    /// <summary>firm_tabs: vor der Gruendung gibt es nur die Finanzen.</summary>
    public static List<(string Id, string Name)> Reiter(Spielstand stand) =>
        Py.Wahr(stand.Firma) ? [.. FirmenReiter] : [.. FirmenReiter.Where(r => r.Id == "finanzen")];

    /// <summary>values_text: "Netzwerk 35 · Sicherheit 15 · ..."</summary>
    public static string WerteText(PyObjekt werte) =>
        string.Join(" · ", T.Fachbereiche.Select(k => Prozent("%s %d", T.FachKurzname[T.Name[k]], Get(werte, k, 0L))));

    /// <summary>staff_money_text: Gehalt und Umsatz einer Person pro Arbeitstag.</summary>
    public static string GeldText(PyObjekt eintrag, PyObjekt inhalt)
    {
        var umsatz = eintrag["umsatz"] ?? MitarbeiterUmsatz(Objekt(eintrag["werte"]), inhalt);
        return Prozent("Gehalt %s · Umsatz %s pro Arbeitstag", GanzeEuroAnzeige(eintrag["gehalt"]), GanzeEuroAnzeige(umsatz));
    }

    /// <summary>_whole_euro_shown: wie _whole_euro, mit geschuetztem Leerzeichen vor "€".</summary>
    public static string GanzeEuroAnzeige(object? wert) => Wohnung.GanzeEuro(wert).Replace(" €", " €", StringComparison.Ordinal);

    /// <summary>training_text: Zeile zur laufenden Weiterbildung (oder Abwesenheit, oder leer).</summary>
    public static string WeiterbildungText(Spielstand stand, PyObjekt eintrag)
    {
        if (!Py.Wahr(eintrag["weiterbildung"]))
        {
            return AbwesenheitText(eintrag);
        }
        var w = Objekt(eintrag["weiterbildung"]);
        var rest = Py.Int(w["bis_tag"]) - stand.Tag;
        if (Py.Gleich(w["art"], Coaching))
        {
            return Prozent("Im Coaching (Macke „%s“) · noch %s", Get(OderLeer(eintrag["macke_info"]), "name", ""), TageText(rest));
        }
        var was = Py.Wahr(w["thema"])
            ? Prozent("%s, +%d", T.Kurzname[(string)w["thema"]!], w["plus"])
            : Prozent("%s, +%d je Thema", T.FachKurzname[T.Name[(string)w["cat"]!]], w["plus"]);
        return Prozent("In Weiterbildung (%s) · noch %s", was, rest == 1 ? "1 Arbeitstag" : Prozent("%d Arbeitstage", rest));
    }

    /// <summary>firm_rules</summary>
    public static PyObjekt FirmaRegeln(PyObjekt inhalt) => Objekt(inhalt["firma"]);

    /// <summary>learn_rules</summary>
    public static PyObjekt LernRegeln(PyObjekt inhalt) => Objekt(FirmaRegeln(inhalt)["lernen"]);

    /// <summary>learn_cap: Grenze fuer das Lernen durch Arbeit je Thema (ab 0.40).</summary>
    public static object? LernDeckel(PyObjekt inhalt)
    {
        var regel = LernRegeln(inhalt);
        return Py.Min(Get(regel, "deckel", regel["max"]), regel["max"]);
    }

    /// <summary>topics_at_cap: Themen, in denen die Person durch Arbeit nichts mehr dazulernt.</summary>
    public static List<string> ThemenAmDeckel(PyObjekt themen, PyObjekt inhalt)
    {
        var deckel = LernDeckel(inhalt);
        return [.. T.Reihenfolge.Where(t => !Py.Kleiner(Get(themen, t, 0L), deckel))];
    }

    /// <summary>cap_text: "Grenze erreicht: SQL · Routing – weiter nur mit Weiterbildung" (ab 0.40).</summary>
    public static string DeckelText(PyObjekt themen, PyObjekt inhalt)
    {
        var erreicht = ThemenAmDeckel(themen, inhalt);
        return erreicht.Count == 0 ? ""
            : Prozent("Grenze erreicht: %s – weiter nur mit Weiterbildung", string.Join(" · ", erreicht.Select(t => T.Kurzname[t])));
    }

    /// <summary>cat_values: Werte je Thema -> Werte je Fachbereich (gerundeter Durchschnitt).</summary>
    public static PyObjekt Fachwerte(PyObjekt themen)
    {
        var ergebnis = new PyObjekt();
        foreach (var k in T.Fachbereiche)
        {
            var werte = T.ThemenVon[k].Select(t => Get(themen, t, 0L)).ToList();
            ergebnis.Add(k, werte.Count > 0 ? PythonRunden.Runde(Py.F(Py.Summe(werte)) / werte.Count) : 0L);
        }
        return ergebnis;
    }

    /// <summary>strongest_topics: die staerksten Themen (bei Gleichstand in der Reihenfolge der Themen).</summary>
    public static List<object?> StaerksteThemen(PyObjekt themen, int anzahl = 2) =>
        [.. T.Reihenfolge.OrderBy(t => -Py.F(Get(themen, t, 0L))).ThenBy(T.Platz).Take(anzahl)];

    /// <summary>spread_topics: Werte je Fachbereich -> je Thema, fest gestreut um +-streuung.</summary>
    public static PyObjekt ThemenStreuen(PyObjekt werte, string startwert, object? streuung)
    {
        var ergebnis = new PyObjekt();
        foreach (var k in T.Fachbereiche)
        {
            var themen = T.ThemenVon[k];
            var versatz = themen.Select(t => (object?)Py.Mal(Wurf(startwert, 0, "thema|" + t) * 2 - 1, streuung)).ToList();
            var mittel = versatz.Count > 0 ? Py.F(Py.Summe(versatz)) / versatz.Count : 0;
            for (var i = 0; i < themen.Count; i++)
            {
                var wert = Py.F(Py.Plus(Get(werte, k, 0L), versatz[i])) - mittel;
                ergebnis.Add(themen[i], Math.Max(0, Math.Min(90, PythonRunden.Runde(wert))));
            }
        }
        return ergebnis;
    }

    /// <summary>add_strengths</summary>
    public static PyObjekt StaerkenDazu(PyObjekt themen, IEnumerable<object?> staerken, object? plus)
    {
        var ergebnis = Py.Flach(themen);
        foreach (var t in staerken)
        {
            if (t is string th && Py.Hat(ergebnis, th))
            {
                Py.Setzen(ergebnis, th, Py.Min(90L, Py.Plus(ergebnis[th], Py.Int(plus))));
            }
        }
        return ergebnis;
    }

    /// <summary>staff_base_topics: Werte je Thema bei der Einstellung (vor 0.38 fest gestreut).</summary>
    public static PyObjekt BasisThemen(object? person, PyObjekt daten, PyObjekt inhalt)
    {
        var ergebnis = new PyObjekt();
        if (Py.Wahr(daten["themen"]))
        {
            var themen = Objekt(daten["themen"]);
            foreach (var t in T.Reihenfolge)
            {
                ergebnis.Add(t, Py.F(Get(themen, t, 0L)));
            }
            return ergebnis;
        }
        var streuung = Get(LernRegeln(inhalt), "alt_streuung", 5L);
        foreach (var (t, w) in ThemenStreuen(OderLeer(daten["werte"]), "alt|" + Py.Str(person), streuung))
        {
            ergebnis.Add(t, Py.F(w));
        }
        return ergebnis;
    }

    /// <summary>ticket_topic: Thema einer Kundenticket-Vorlage (ab 0.38).</summary>
    public static object? TicketThema(object? vorlage, PyObjekt inhalt) =>
        Liste(TicketRegeln(inhalt)["vorlagen"]).Select(Objekt).FirstOrDefault(v => Py.Gleich(v["id"], vorlage))?["thema"];

    /// <summary>project_topic: Thema eines Projekts (aeltere Spielstaende: aus der Projektarbeit).</summary>
    public static object? ProjektThema(PyObjekt projekt, PyObjekt inhalt)
    {
        if (Py.Wahr(projekt["thema"]))
        {
            return projekt["thema"];
        }
        var vorlagen = Py.Wahr(inhalt["projektarbeiten"]) ? Liste(inhalt["projektarbeiten"]) : [];
        return projekt["vorlage"] is long i && 0 <= i && i < vorlagen.Count ? Objekt(vorlagen[(int)i])["thema"] : null;
    }

    /// <summary>topic_level: Wissensstand der Spielfigur im Thema (sonst im Fachbereich).</summary>
    public static long ThemaStand(PyObjekt? stufen, object? thema, object? fach)
    {
        stufen ??= [];
        var fachwert = fach is string f ? Get(stufen, f, 0L) : 0L;
        return Py.Rund(Py.Wahr(thema) && thema is string t ? Get(stufen, t, fachwert) : fachwert);
    }

    /// <summary>strengths_text: "Stark in: IPv4 62 · Linux 55"</summary>
    public static string StaerkenText(PyObjekt eintrag)
    {
        var themen = OderLeer(eintrag["themen"]);
        var namen = Py.Wahr(eintrag["staerken"]) ? Liste(eintrag["staerken"]) : StaerksteThemen(themen);
        return "Stark in: " + string.Join(" · ", namen.Select(t => Prozent("%s %d", T.Kurzname[(string)t!], Py.Int(Get(themen, (string)t!, 0L)))));
    }

    /// <summary>topics_text: "Grundlagen 32 · IPv4 40 · ..." (die Themen eines Fachbereichs)</summary>
    public static string ThemenText(PyObjekt themen, string fach) =>
        string.Join(" · ", T.ThemenVon[fach].Select(t => Prozent("%s %d", T.Kurzname[t], Py.Int(Get(themen, t, 0L)))));

    /// <summary>skill_bars: Fachbereiche einer Person fuer die Balken (PC und Handy gleich).</summary>
    public static List<PyObjekt> SkillBalken(PyObjekt eintrag, PyObjekt inhalt)
    {
        var themen = OderLeer(eintrag["themen"]);
        var werte = Py.Wahr(eintrag["werte"]) ? Objekt(eintrag["werte"]) : Fachwerte(themen);
        var deckel = LernDeckel(inhalt);
        var ergebnis = new List<PyObjekt>();
        foreach (var k in T.Fachbereiche)
        {
            var wert = Py.Int(Get(werte, k, 0L));
            var zeilen = new List<object?>();
            foreach (var t in T.ThemenVon[k])
            {
                var zahl = Py.Int(Get(themen, t, 0L));
                zeilen.Add(new PyObjekt
                {
                    { "thema", t }, { "name", T.Kurzname[t] }, { "wert", zahl },
                    { "anteil", Math.Max(0.0, Math.Min(1.0, zahl / (double)SkillBalkenMax)) }, { "grenze", !Py.Kleiner(zahl, deckel) },
                });
            }
            ergebnis.Add(new PyObjekt
            {
                { "cat", k }, { "name", T.FachKurzname[T.Name[k]] }, { "wert", wert },
                { "anteil", Math.Max(0.0, Math.Min(1.0, wert / (double)SkillBalkenMax)) }, { "themen", zeilen },
            });
        }
        return ergebnis;
    }

    /// <summary>learned_text: "Tim: Verkabelung 38 → 40 (Kundenticket)"</summary>
    public static string GelerntText(PyObjekt eintrag) => Prozent("%s: %s %d → %d (%s)",
        KurzName(Get(eintrag, "name", "")), eintrag["thema"] is string t && T.Kurzname.TryGetValue(t, out var k) ? k : eintrag["thema"],
        Py.Int(Get(eintrag, "vorher", 0L)), Py.Int(Get(eintrag, "nachher", 0L)),
        eintrag["grund"] is string g && LernGruende.TryGetValue(g, out var grund) ? grund : "");

    /// <summary>short_name: der Vorname, bei einem Titel wie "Dr." Titel und Nachname.</summary>
    public static string KurzName(object? name)
    {
        var teile = Rechner.PythonZeichen.Split((string)name!);
        if (teile.Count > 2 && teile[0].EndsWith('.'))
        {
            return teile[0] + " " + teile[^1];
        }
        return teile[0];
    }

    /// <summary>staff_salary</summary>
    public static long MitarbeiterGehalt(PyObjekt werte, PyObjekt inhalt)
    {
        var regel = Objekt(FirmaRegeln(inhalt)["gehalt"]);
        return Py.Rund(Py.Plus(regel["basis"], Py.Mal(regel["je_punkt"], Py.Summe(werte.Select(p => p.Value)))));
    }

    /// <summary>staff_revenue</summary>
    public static long MitarbeiterUmsatz(PyObjekt werte, PyObjekt inhalt)
    {
        var regel = Objekt(FirmaRegeln(inhalt)["umsatz"]);
        return Py.Rund(Py.Plus(regel["basis"], Py.Mal(regel["je_punkt"], Py.Summe(werte.Select(p => p.Value)))));
    }

    /// <summary>staff_focus: Fachbereich mit dem hoechsten Wert (bei Gleichstand der erste).</summary>
    public static string Schwerpunkt(PyObjekt werte)
    {
        string? bester = null;
        foreach (var k in T.Fachbereiche)
        {
            if (bester is null || Py.Kleiner(Get(werte, bester, 0L), Get(werte, k, 0L)))
            {
                bester = k;
            }
        }
        return bester!;
    }

    /// <summary>staff_role</summary>
    public static string Rolle(PyObjekt werte) => "Schwerpunkt " + T.FachKurzname[T.Name[Schwerpunkt(werte)]];

    /// <summary>_applicant: ein generischer Bewerber (fest aus Startwert, Runde und Nummer).</summary>
    private static PyObjekt Bewerber(PyObjekt regeln, string startwert, long runde, long nummer, PyObjekt inhalt)
    {
        var salz = Prozent("b%d-%d", runde, nummer);
        var regel = Objekt(regeln["bewerbung"]);
        var (unten, oben) = (regel["werte_min"], regel["werte_max"]);
        var werte = new PyObjekt();
        foreach (var k in T.Fachbereiche)
        {
            var roh = Py.Plus(unten, Py.Mal(Wurf(startwert, runde, salz + k), Py.Minus(oben, unten)));
            werte.Add(k, PythonRunden.Runde(Py.F(roh) / 5.0) * 5);
        }
        var fokus = Waehle(T.Fachbereiche, startwert, runde, salz + "schwerpunkt");
        Py.Setzen(werte, fokus, Py.Min(90L, Py.Plus(werte[fokus], regel["schwerpunkt_plus"])));
        // Ab 0.38: Werte je Thema, dazu Staerken (die erste im Schwerpunkt)
        var themen = ThemenStreuen(werte, startwert + "|" + salz, Get(regel, "thema_streuung", 10L));
        var staerken = new List<object?> { Waehle(T.ThemenVon[fokus], startwert, runde, salz + "staerke0") };
        for (var extra = 1; extra < Py.Int(Get(regel, "staerken", 2L)); extra++)
        {
            var rest = T.Reihenfolge.Where(t => !Py.Enthaelt(staerken, t)).ToList();
            staerken.Add(Waehle(rest, startwert, runde, salz + "staerke" + extra));
        }
        themen = StaerkenDazu(themen, staerken, Get(regel, "staerke_plus", 20L));
        werte = Fachwerte(themen);
        var namen = Objekt(regeln["namen"]);
        var aussehen = new PyObjekt();
        foreach (var (teil, wahl) in Aussehen.Teile)
        {
            if (teil != "kreis")
            {
                aussehen.Add(teil, Waehle(wahl.Select(w => w.Kennung).ToList(), startwert, runde, salz + teil));
            }
        }
        aussehen.Add("kreis", "violett");
        // Ab 0.43: Macken als Objekte, die Bitweiche-Wechsler haben ihre eigene
        var macke = Waehle(Liste(regeln["macken"]).Select(Objekt).Where(m => !Py.Wahr(m["kollege"])).ToList(), startwert, runde, salz + "macke");
        var kennung = Prozent("bw%d-%d", runde, nummer);
        // Ab 0.44: Manche kommen von einem Mitbewerber (der sie spaeter zurueckwill)
        var frueher = Py.Str(FruehererArbeitgeberVon(startwert, kennung, inhalt));
        var rolle = Rolle(werte);
        if (frueher.Length > 0)
        {
            rolle = Prozent("%s, bisher bei %s", rolle, Mitbewerber(frueher, inhalt)["kurz"]);
        }
        return new PyObjekt
        {
            { "id", kennung }, { "macke", macke["text"] }, { "macke_id", macke["id"] }, { "vorher", frueher },
            { "macke_info", MackeZustand(kennung, new PyObjekt { { "macke_id", macke["id"] } }, 0, 0, inhalt) },
            { "name", Prozent("%s %s", Waehle(Liste(namen["vornamen"]), startwert, runde, salz + "vor"), Waehle(Liste(namen["nachnamen"]), startwert, runde, salz + "nach")) },
            { "aussehen", Aussehen.Normalisieren(aussehen) }, { "werte", werte }, { "themen", themen },
            { "staerken", StaerksteThemen(themen) }, { "gehalt", MitarbeiterGehalt(werte, inhalt) }, { "herkunft", "bewerbung" },
            { "schwerpunkt", Schwerpunkt(werte) }, { "rolle", rolle },
        };
    }

    /// <summary>switchers: Bitweiche-Kollegen, die sich gerade bewerben: (eintrag, erster Tag, letzter Tag).</summary>
    public static List<(PyObjekt Eintrag, long Erster, long Letzter)> Wechsler(Spielstand stand, PyObjekt inhalt)
    {
        if (!Py.Wahr(stand.Firma))
        {
            return [];
        }
        var start = Py.Int(Objekt(stand.Firma)["tag"]);
        var ergebnis = new List<(PyObjekt, long, long)>();
        foreach (var e in (Get(FirmaRegeln(inhalt), "wechsel", new List<object?>()) as List<object?> ?? []).Select(Objekt))
        {
            var erster = start + Py.Int(e["nach_tagen"]);
            var letzter = erster + Py.Int(Get(e, "offen_tage", 10L)) - 1;
            ergebnis.Add((e, erster, letzter));
        }
        return ergebnis;
    }

    /// <summary>applicants: Bewerbungen, die heute vorliegen (ohne schon Eingestellte).</summary>
    public static List<PyObjekt> Bewerbungen(Spielstand stand, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        if (!Py.Wahr(stand.Firma))
        {
            return [];
        }
        var regeln = FirmaRegeln(inhalt);
        var bewerbung = Objekt(regeln["bewerbung"]);
        var schritt = Py.Int(bewerbung["abstand_tage"]);
        var start = Py.Int(Objekt(stand.Firma)["tag"]);
        var runde = PyDiv(Math.Max(0, stand.Tag - start), schritt);
        var startwert = Py.Str(Objekt(stand.Firma)["name"]) + "|" + start;
        var ergebnis = new List<PyObjekt>();
        foreach (var (e, erster, letzter) in Wechsler(stand, inhalt))
        {
            var kennung = KollegePrefix + Py.Str(e["kollege"]);
            var person = Person(e["kollege"], inhalt);
            if (person is null || stand.JeEingestellt.Contains(kennung) || !(erster <= stand.Tag && stand.Tag <= letzter))
            {
                continue;
            }
            var themen = StaerkenDazu(ThemenStreuen(Objekt(e["werte"]), "wechsel|" + Py.Str(e["kollege"]), Get(bewerbung, "thema_streuung", 10L)),
                Py.Wahr(e["staerken"]) ? Liste(e["staerken"]) : [], Get(bewerbung, "staerke_plus", 20L));
            var werte = Fachwerte(themen);
            ergebnis.Add(new PyObjekt
            {
                { "id", kennung }, { "name", person["name"] }, { "aussehen", Aussehen.Normalisieren(person["aussehen"]) },
                { "werte", werte }, { "themen", themen }, { "staerken", StaerksteThemen(themen) },
                { "gehalt", MitarbeiterGehalt(werte, inhalt) }, { "herkunft", "bitweiche" }, { "schwerpunkt", Schwerpunkt(werte) },
                { "macke", Get(person, "macke", "") }, { "macke_id", (MackeFuerKollege(e["kollege"], inhalt) ?? [])["id"] },
                { "macke_info", MackeZustand(kennung, new PyObjekt { { "herkunft", "bitweiche" } }, 0, 0, inhalt) },
                { "rolle", Prozent("bisher %s bei Bitweiche", person["rolle"]) }, { "bis_tag", letzter },
            });
        }
        for (long nummer = 0; nummer < Py.Int(bewerbung["anzahl"]); nummer++)
        {
            var e = Bewerber(regeln, startwert, runde, nummer, inhalt);
            if (stand.JeEingestellt.Contains(e["id"]))
            {
                continue;
            }
            Py.Setzen(e, "bis_tag", start + (runde + 1) * schritt - 1);
            ergebnis.Add(e);
        }
        var schwaecher = stand.Erleichterung("macken_schwaeche_faktor", 1.0);
        if (!Py.Gleich(schwaecher, 1L))
        {
            // Ab 0.47 auf "Einfach": die Schwaeche wirkt nur halb so stark
            foreach (var e in ergebnis)
            {
                if (e["macke_info"] is PyObjekt info && Py.Wahr(info))
                {
                    Py.Setzen(info, "faktor", Py.F(info["faktor"]) * Py.F(schwaecher));
                    Py.Setzen(info, "minus_texte", Objekt(info["minus"]).Select(p => (object?)MackeWirkungText(p.Key, Py.F(p.Value) * Py.F(info["faktor"]))).ToList());
                }
            }
        }
        return ergebnis;
    }

    /// <summary>a // b fuer ganze Zahlen wie Python (abrunden).</summary>
    internal static long PyDiv(long a, long b)
    {
        var q = a / b;
        return a % b != 0 && (a < 0) != (b < 0) ? q - 1 : q;
    }

    /// <summary>switch_news: Story-Moment am Morgen, ein Bitweiche-Kollege will wechseln.</summary>
    public static string WechselNachricht(Spielstand stand, PyObjekt inhalt) => string.Join("\n\n",
        Wechsler(stand, inhalt).Where(w => w.Erster == stand.Tag && !stand.JeEingestellt.Contains(KollegePrefix + Py.Str(w.Eintrag["kollege"])))
            .Select(w => SpielInhalt.Zeilen(w.Eintrag["text"])));

    /// <summary>training_offer: was eine Weiterbildung kostet und bringt (problem leer = moeglich).</summary>
    public static PyObjekt WeiterbildungAngebot(Spielstand stand, object? person, string ziel, PyObjekt inhalt)
    {
        var regeln = Objekt(FirmaRegeln(inhalt)["weiterbildung"]);
        var ganz = T.Fachbereiche.Contains(ziel);
        var regel = ganz ? Objekt(Get(regeln, "fachbereich", regeln)) : regeln;
        var gemacht = stand.WeiterbildungenVon(person).Count(e => !Py.Gleich(e["art"], Coaching));
        // Eigener Schulungsraum (ab 0.36): guenstiger und kuerzer
        var preis = Rabatt(Py.Int(Py.Plus(regel["preis"], Py.Mal(regeln["aufschlag"], (long)gemacht))), stand.RaumEffekt("weiterbildung_rabatt"));
        var tage = Math.Max(1, Py.Int(regel["tage"]) - Py.Int(stand.RaumEffekt("weiterbildung_tage_minus")));
        // Ab 0.43: Manche Macken lernen lieber mit den Haenden
        tage += PythonRunden.Runde(stand.MackeWert(person, "weiterbildung_tage"));
        string? fach = ganz ? ziel : T.FachbereichVon.TryGetValue(ziel, out var lang) && T.Kurz.TryGetValue(lang, out var kurz) ? kurz : null;
        var ergebnis = new PyObjekt
        {
            { "preis", preis }, { "tage", tage }, { "plus", 0L }, { "problem", "" }, { "art", ganz ? "fachbereich" : "thema" },
            { "cat", fach }, { "thema", ganz ? null : ziel },
        };
        if (!stand.Personal.ContainsKey(person))
        {
            Py.Setzen(ergebnis, "problem", "Diese Person arbeitet nicht bei dir.");
            return ergebnis;
        }
        if (fach is null)
        {
            Py.Setzen(ergebnis, "problem", "Unbekanntes Thema.");
            return ergebnis;
        }
        var oben = Py.Int(regeln["max"]);
        var werte = stand.PersonalThemen(person, laufend: true);
        if (ganz)
        {
            var platz = T.ThemenVon[fach].Max(t => oben - Py.F(Get(werte, t, 0L)));
            Py.Setzen(ergebnis, "plus", platz > 0 ? Py.Int(regel["plus"]) : 0L);
        }
        else
        {
            Py.Setzen(ergebnis, "plus", Math.Max(0, Math.Min(Py.Int(regel["plus"]), (long)(oben - Py.F(Get(werte, ziel, 0L))))));
        }
        if (stand.WeiterbildungVon(person) is not null)
        {
            Py.Setzen(ergebnis, "problem", "Die Person ist gerade schon in einer Weiterbildung.");
        }
        else if (stand.Weg(person) is { } weg)
        {
            Py.Setzen(ergebnis, "problem", AbwesendProblem(Get(Objekt(stand.Personal[person]), "name", ""), weg));
        }
        else if (!Py.Wahr(ergebnis["plus"]))
        {
            Py.Setzen(ergebnis, "problem", Prozent("%s ist das Maximum (%d) erreicht.", ganz ? "In allen Themen des Fachbereichs" : "In diesem Thema", oben));
        }
        else if (stand.Geld < Py.Int(ergebnis["preis"]))
        {
            Py.Setzen(ergebnis, "problem", Prozent("Dafür reicht dein Geld noch nicht (%s fehlen).", Wohnung.GanzeEuro(Py.Int(ergebnis["preis"]) - Math.Max(0, stand.Geld))));
        }
        return ergebnis;
    }

    /// <summary>discounted: Preis mit Rabatt in Prozent (ganze Euro).</summary>
    public static long Rabatt(long preis, object? prozent) =>
        !Py.Wahr(prozent) ? preis : PythonRunden.Runde(preis * (1 - Py.F(prozent) / 100.0));

    // -- Macken, Stimmung, Krankheit, Urlaub, Konflikte (ab 0.43) --------------

    /// <summary>personal_rules</summary>
    public static PyObjekt PersonalRegeln(PyObjekt inhalt) => Objekt(FirmaRegeln(inhalt)["personal"]);

    /// <summary>quirk_by_id</summary>
    public static PyObjekt? MackeNach(object? kennung, PyObjekt inhalt) =>
        Liste(FirmaRegeln(inhalt)["macken"]).OfType<PyObjekt>().FirstOrDefault(m => Py.Gleich(m["id"], kennung));

    /// <summary>quirk_for_colleague: die Macke eines Bitweiche-Kollegen, der zu dir wechselt.</summary>
    public static PyObjekt? MackeFuerKollege(object? kollege, PyObjekt inhalt) =>
        Liste(FirmaRegeln(inhalt)["macken"]).Select(Objekt).FirstOrDefault(m => Py.Gleich(m["kollege"], kollege));

    /// <summary>_quirk_for: Macke aus den Einstellungsdaten.</summary>
    private static PyObjekt? MackeFuer(object? person, PyObjekt daten, PyObjekt inhalt)
    {
        var macke = Py.Wahr(daten["macke_id"]) ? MackeNach(daten["macke_id"], inhalt) : null;
        var kennung = Py.Str(person);
        if (macke is null && kennung.StartsWith(KollegePrefix, StringComparison.Ordinal))
        {
            macke = MackeFuerKollege(kennung[KollegePrefix.Length..], inhalt);
        }
        if (macke is null && Py.Wahr(daten["macke"]))
        {
            macke = Liste(FirmaRegeln(inhalt)["macken"]).Select(Objekt).FirstOrDefault(m => Py.Gleich(m["text"], daten["macke"]));
        }
        return macke;
    }

    /// <summary>quirk_start_stage: ausgepraegt (3) oder mittel (2); Bitweiche-Kollegen starten bei mittel.</summary>
    public static long MackeStartStufe(object? person, PyObjekt daten)
    {
        if (Py.Gleich(daten["herkunft"], "bitweiche") || Py.Str(person).StartsWith(KollegePrefix, StringComparison.Ordinal))
        {
            return 2;
        }
        return Wurf(Py.Str(person), 0, "macke-stufe") < 0.5 ? 3 : 2;
    }

    /// <summary>quirk_state: Macke mit Stufe am Tag (null ohne bekannte Macke).</summary>
    public static PyObjekt? MackeZustand(object? person, PyObjekt daten, long tag, long gecoacht, PyObjekt inhalt, object? schwaecher = null)
    {
        var macke = MackeFuer(person, daten, inhalt);
        if (macke is null)
        {
            return null;
        }
        var regeln = Objekt(PersonalRegeln(inhalt)["macken"]);
        var alle = Math.Max(1, Py.Int(regeln["abschwaechen_tage"]));
        var start = MackeStartStufe(person, daten);
        var dabei = Py.Wahr(daten["tag"]) ? Math.Max(0, tag - Py.Int(Py.Wahr(Get(daten, "tag", tag)) ? Get(daten, "tag", tag) : tag)) : 0;
        var stufe = Math.Max(1, start - dabei / alle - gecoacht);
        var eintrag = Liste(Objekt(regeln["stufen"])[stufe.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        var (name, prozent) = (eintrag[0], eintrag[1]);
        var ergebnis = new PyObjekt
        {
            { "id", macke["id"] }, { "name", macke["name"] }, { "text", macke["text"] },
            { "plus", Py.Flach(OderLeer(macke["plus"])) }, { "minus", Py.Flach(OderLeer(macke["minus"])) },
            { "start", start }, { "stufe", stufe }, { "stufe_name", name },
            { "faktor", Py.F(prozent) / 100.0 * Py.F(schwaecher ?? 1.0) },
            { "naechste_in", stufe > 1 ? alle - dabei % alle : null },
        };
        Py.Setzen(ergebnis, "plus_texte", Objekt(ergebnis["plus"]).Select(p => (object?)MackeWirkungText(p.Key, p.Value)).ToList());
        Py.Setzen(ergebnis, "minus_texte", Objekt(ergebnis["minus"]).Select(p => (object?)MackeWirkungText(p.Key, Py.Mal(p.Value, ergebnis["faktor"]))).ToList());
        return ergebnis;
    }

    /// <summary>quirk_effect: Wirkung einer Macke fuer einen Schluessel.</summary>
    public static double MackeWirkung(PyObjekt? macke, string schluessel)
    {
        if (macke is null || !Py.Wahr(macke))
        {
            return 0;
        }
        return Py.F(Py.Plus(Get(Objekt(macke["plus"]), schluessel, 0L), Py.Mal(Get(Objekt(macke["minus"]), schluessel, 0L), macke["faktor"])));
    }

    /// <summary>_signed: "+5", "-2,5", mit Einheit.</summary>
    public static string MitVorzeichen(object? wert, string einheit = "")
    {
        var w = PythonRundenStellen.Runde(Py.F(wert), 1);
        var text = w == Math.Truncate(w) ? Prozent("%d", w) : Prozent("%.1f", w).Replace(".", ",", StringComparison.Ordinal);
        return (w > 0 ? "+" + text : text) + einheit;
    }

    /// <summary>_training_days_text</summary>
    private static string WeiterbildungTageText(object? wert)
    {
        var tage = PythonRunden.Runde(Py.F(wert));
        return tage <= 0 ? "Weiterbildung dauert nicht mehr länger" : Prozent("Weiterbildung dauert %s länger", TageText(tage));
    }

    /// <summary>quirk_effect_text: eine Wirkung als kurzer Text, "Ticket-Chance +10 %".</summary>
    public static string MackeWirkungText(string schluessel, object? wert)
    {
        var schlicht = MitVorzeichen(wert).TrimStart('+', '-');
        return schluessel switch
        {
            "umsatz" => "Routineumsatz " + MitVorzeichen(wert, " %"),
            "chance" => "Ticket-Chance " + MitVorzeichen(wert, " %"),
            "chance_netzwerk" => "Ticket-Chance bei Netzwerk-Tickets " + MitVorzeichen(wert, " %"),
            "chance_hardware" => "Ticket-Chance bei Hardware-Tickets " + MitVorzeichen(wert, " %"),
            "chance_talbahn" => "Ticket-Chance bei Talbahn-Kunden " + MitVorzeichen(wert, " %"),
            "projekt" => "Projektleistung " + MitVorzeichen(wert, " %"),
            "team_projekt" => "Projektleistung des ganzen Teams " + MitVorzeichen(wert, " %"),
            "team_lernen" => "Kollegen im Projektteam lernen beim Abschluss " + MitVorzeichen(wert),
            "kz_erfolg" => "Kundenzufriedenheit bei geschafften Tickets " + MitVorzeichen(wert),
            "kz_fehlschlag" => "Verpatzte Tickets kosten " + schlicht + " Kundenzufriedenheit weniger",
            "lernen" => "Lernt durch Arbeit " + MitVorzeichen(wert, " %"),
            "lernen_hardware" => "Lernt in Hardware-Themen " + MitVorzeichen(wert, " %"),
            "lernen_andere" => "Lernt in allen anderen Themen " + MitVorzeichen(wert, " %"),
            "urlaub" => "Fragt " + schlicht + " % öfter nach Urlaub",
            "krank" => "Wird " + schlicht + " % öfter krank",
            "konflikt" => "Streit im Team " + schlicht + " % häufiger",
            "weiterbildung_tage" => WeiterbildungTageText(wert),
            "stimmung_team" => "Stimmung der Kollegen erholt sich schneller (" + MitVorzeichen(wert) + " pro Tag)",
            "stimmung_fehlschlag" => "Stimmung -" + schlicht + " nach einem verpatzten Ticket",
            _ => schluessel + " " + MitVorzeichen(wert),
        };
    }

    /// <summary>quirk_bar: drei Kaestchen fuer die Stufe, "■■□" = mittel.</summary>
    public static string MackeBalken(object? stufe)
    {
        var s = (int)Math.Max(0, Math.Min(3, Py.Int(stufe)));
        return new string('■', s) + new string('□', 3 - s);
    }

    /// <summary>quirk_hint: Hinweis zur Abschwaechung (oder leer).</summary>
    public static string MackeHinweis(PyObjekt? macke)
    {
        if (macke is null || !Py.Wahr(macke))
        {
            return "";
        }
        if (Py.Int(macke["stufe"]) <= 1)
        {
            return "Schwächer wird die Macke nicht mehr.";
        }
        var tage = Py.Int(macke["naechste_in"]);
        return Prozent("Schwächt sich ab: nächste Stufe in %s (oder per Coaching).", tage == 1 ? "1 Arbeitstag" : Prozent("%d Arbeitstagen", tage));
    }

    /// <summary>quirk_lines: Macke fuer die Anzeige (null ohne Macke).</summary>
    public static PyObjekt? MackeZeilen(PyObjekt? macke) => macke is null || !Py.Wahr(macke) ? null : new PyObjekt
    {
        { "titel", macke["name"] }, { "balken", MackeBalken(macke["stufe"]) }, { "stufe", macke["stufe_name"] },
        { "zitat", "„" + Py.Str(macke["text"]) + "“" }, { "plus", string.Join(" · ", Liste(macke["plus_texte"]).Select(Py.Str)) },
        { "minus", string.Join(" · ", Liste(macke["minus_texte"]).Select(Py.Str)) }, { "hinweis", MackeHinweis(macke) },
    };

    /// <summary>mood_level: gut / okay / schlecht / kritisch (fuer die Farbe).</summary>
    public static string StimmungStufe(object? wert, PyObjekt inhalt)
    {
        var regeln = Objekt(PersonalRegeln(inhalt)["stimmung"]);
        return !Py.Kleiner(wert, regeln["ziel"]) ? "gut" : !Py.Kleiner(wert, regeln["tief"]) ? "okay" : !Py.Kleiner(wert, regeln["kritisch"]) ? "schlecht" : "kritisch";
    }

    /// <summary>mood_name</summary>
    public static string StimmungName(object? wert, PyObjekt inhalt)
    {
        var stufe = StimmungStufe(wert, inhalt);
        return stufe == "kritisch" ? "denkt an Kündigung" : stufe;
    }

    /// <summary>mood_text: "Stimmung 70 (gut)", dazu der Leistungsabzug.</summary>
    public static string StimmungText(PyObjekt eintrag, PyObjekt inhalt)
    {
        var text = Prozent("Stimmung %d (%s)", eintrag["stimmung"], StimmungName(eintrag["stimmung"], inhalt));
        var form = OderLeer(eintrag["form"]);
        if (Py.Wahr(form["leistung"]))
        {
            text += Prozent(" · %d %% weniger Leistung (%s)", form["leistung"], string.Join(", ", Liste(form["gruende"]).Select(Py.Str)));
        }
        return text;
    }

    /// <summary>absence_text: Zeile fuer Mitarbeiter, die krank oder im Urlaub sind (oder leer).</summary>
    public static string AbwesenheitText(PyObjekt eintrag)
    {
        if (!Py.Wahr(eintrag["abwesend"]))
        {
            return "";
        }
        var weg = Objekt(eintrag["abwesend"]);
        return Prozent("%s bis einschließlich Arbeitstag %d", Py.Gleich(weg["art"], AbwesendKrank) ? "Krank" : "Im Urlaub", Py.Int(weg["bis"]) - 1);
    }

    /// <summary>_days_text</summary>
    public static string TageText(long anzahl) => anzahl == 1 ? "1 Arbeitstag" : Prozent("%d Arbeitstage", anzahl);

    /// <summary>coaching_offer: was ein Coaching kostet (danach wirkt die Schwaeche eine Stufe schwaecher).</summary>
    public static PyObjekt CoachingAngebot(Spielstand stand, object? person, PyObjekt inhalt)
    {
        var regeln = Objekt(PersonalRegeln(inhalt)["coaching"]);
        var preis = Rabatt(Py.Int(regeln["preis"]), stand.RaumEffekt("weiterbildung_rabatt"));
        var tage = Math.Max(1, Py.Int(regeln["tage"]) - Py.Int(stand.RaumEffekt("weiterbildung_tage_minus")));
        var macke = stand.MackeVon(person);
        var ergebnis = new PyObjekt
        {
            { "preis", preis }, { "tage", tage }, { "problem", "" }, { "macke", macke?["id"] }, { "stufe", macke is not null ? macke["stufe"] : 0L },
        };
        var name = Get(stand.Personal.Get(person) as PyObjekt ?? [], "name", "");
        var weg = stand.Abwesenheit(person);
        if (!stand.Personal.ContainsKey(person))
        {
            Py.Setzen(ergebnis, "problem", "Diese Person arbeitet nicht bei dir.");
        }
        else if (macke is null)
        {
            Py.Setzen(ergebnis, "problem", Prozent("%s hat keine Macke, an der ein Coaching etwas ändert.", name));
        }
        else if (Py.Int(macke["stufe"]) <= 1)
        {
            Py.Setzen(ergebnis, "problem", "Die Macke ist schon so schwach, wie sie werden kann.");
        }
        else if (weg is not null && Py.Wahr(weg))
        {
            Py.Setzen(ergebnis, "problem", AbwesendProblem(name, weg));
        }
        else if (stand.Geld < preis)
        {
            Py.Setzen(ergebnis, "problem", Prozent("Dafür reicht dein Geld noch nicht (%s fehlen).", Wohnung.GanzeEuro(preis - Math.Max(0, stand.Geld))));
        }
        return ergebnis;
    }

    /// <summary>absent_problem: warum jemand heute nichts uebernehmen kann.</summary>
    public static string AbwesendProblem(object? name, PyObjekt weg)
    {
        var art = weg["art"];
        return Py.Gleich(art, AbwesendKrank) ? Prozent("%s ist krank.", name)
            : Py.Gleich(art, AbwesendUrlaub) ? Prozent("%s ist im Urlaub.", name)
            : Py.Gleich(art, Coaching) ? Prozent("%s ist gerade im Coaching.", name)
            : Prozent("%s ist gerade in einer Weiterbildung.", name);
    }

    /// <summary>staff_ticket_chance: Wert im Thema, Macke (auch nur bei passenden Tickets) und Stimmung.</summary>
    public static long MitarbeiterTicketChance(Spielstand stand, object? person, PyObjekt ticket, object? wert, PyObjekt inhalt)
    {
        var bonus = stand.MackeWert(person, "chance");
        if (Py.Gleich(ticket["cat"], "netzwerk"))
        {
            bonus += stand.MackeWert(person, "chance_netzwerk");
        }
        if (ticket["thema"] is string t && HardwareThemen.Contains(t))
        {
            bonus += stand.MackeWert(person, "chance_hardware");
        }
        if (Py.Gleich(OderLeer(ticket["kunde"])["art"], "talbahn"))
        {
            bonus += stand.MackeWert(person, "chance_talbahn");
        }
        bonus -= Py.F(stand.PersonalForm(person)["chance"]);
        return TicketChance(wert, ticket["anforderung"], inhalt, bonus);
    }

    /// <summary>staff_project_points: Projektleistung am Tag (bei Mitarbeitern mit Macke und Stimmung).</summary>
    public static object MitarbeiterProjektPunkte(Spielstand stand, object? person, object? wert, PyObjekt inhalt)
    {
        var punkte = ProjektPunkte(wert, inhalt);
        if (Py.Gleich(person, Selbst) || !stand.Personal.ContainsKey(person))
        {
            return punkte;
        }
        var faktor = (1 + stand.MackeWert(person, "projekt") / 100.0) * (1 - Py.F(stand.PersonalForm(person)["leistung"]) / 100.0);
        return PythonRundenStellen.Runde(Math.Max(0.0, Py.F(punkte) * faktor), 1);
    }

    /// <summary>team_factor: wer das ganze Team mitzieht (Whiteboard), wirkt einmal.</summary>
    public static double TeamFaktor(Spielstand stand, IEnumerable<object?> leute)
    {
        var werte = leute.Where(p => stand.Personal.ContainsKey(p) && stand.Abwesenheit(p) is null).Select(p => stand.MackeWert(p, "team_projekt")).ToList();
        // max([...] or [0]): der erste groesste Wert
        double bester = 0;
        for (var i = 0; i < werte.Count; i++)
        {
            if (i == 0 || werte[i] > bester)
            {
                bester = werte[i];
            }
        }
        return 1 + bester / 100.0;
    }

    /// <summary>_vacation_line</summary>
    private static string UrlaubZeile(PyObjekt eintrag, PyObjekt inhalt)
    {
        var regeln = Objekt(PersonalRegeln(inhalt)["urlaub"]);
        var texte = Py.Wahr(regeln["texte"]) ? Liste(regeln["texte"]) : [];
        if (Py.Wahr(eintrag["talbahn"]) && Py.Wahr(regeln["text_talbahn"]))
        {
            return Py.Str(regeln["text_talbahn"]);
        }
        return texte.Count > 0 ? Py.Str(Waehle(texte, Py.Str(eintrag["anfrage"]), 0, "text")) : "";
    }

    /// <summary>conflict_pair: wer sich streitet ([a, b]) und der Text.</summary>
    public static (List<object?> Paar, string Text) KonfliktPaar(Spielstand stand, List<object?> leute, string startwert, long tag, PyObjekt inhalt)
    {
        var regel = Objekt(PersonalRegeln(inhalt)["konflikt"]);
        var summen = leute.Where(p => Py.Gleich((stand.MackeVon(p) ?? [])["id"], "gute_laune")).ToList();
        var genau = leute.Where(p => (stand.MackeVon(p) ?? [])["id"] is "pedantisch" or "gruendlich").ToList();
        if (summen.Count > 0 && genau.Count > 0 && Py.Wahr(regel["summen"]) && Wurf(startwert, tag, "konflikt-summen") < 0.5)
        {
            return ([Waehle(summen, startwert, tag, "summt"), Waehle(genau, startwert, tag, "genervt")], Py.Str(regel["summen"]));
        }
        var reihe = leute.OrderBy(p => Wurf(startwert, tag, "konflikt-paar|" + Py.Str(p))).ToList();
        return (reihe.Take(2).ToList(), Py.Str(Waehle(Liste(regel["texte"]), startwert, tag, "konflikt-text")));
    }

    /// <summary>_genitive: "Tims", aber "Jonas’"</summary>
    public static string Genitiv(string name)
    {
        var laenge = Rechner.PythonZeichen.Laenge(name);
        var letztes = laenge > 0 ? Suche.PythonKlein.Klein(Rechner.PythonZeichen.Teil(name, laenge - 1, laenge)) : "";
        return name + ("sßxz".Contains(letztes, StringComparison.Ordinal) ? "’" : "s");
    }

    /// <summary>_pair_names: Vornamen der beiden, bei gleichem Vornamen die ganzen Namen.</summary>
    public static PyObjekt PaarNamen(object? erster, object? zweiter)
    {
        var (a, b) = (Py.Str(erster), Py.Str(zweiter));
        if (KurzName(a) != KurzName(b))
        {
            (a, b) = (KurzName(a), KurzName(b));
        }
        return new PyObjekt { { "a", a }, { "b", b }, { "a_von", Genitiv(a) }, { "b_von", Genitiv(b) } };
    }

    /// <summary>
    /// personal_events: was beim Feierabend fuer den naechsten Arbeitstag feststeht (stand = nach
    /// dem Feierabend): Kuendigungen, Warnungen, Krankmeldungen, Urlaubsanfragen, Konflikte,
    /// Rueckhol-Angebote und schwaecher werdende Macken.
    /// </summary>
    public static List<object?> PersonalEreignisse(Spielstand stand, long tag, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var regeln = PersonalRegeln(inhalt);
        var startwert = stand.FirmenStartwert;
        var morgen = tag + 1;
        var ergebnis = new List<PyObjekt>();
        var namen = new PyDict();
        foreach (var (person, daten) in stand.Personal)
        {
            namen[person] = Get(Objekt(daten), "name", "");
        }
        // Ab 0.47 auf "Einfach": niemand kuendigt (die Stimmung wirkt weiter)
        var kuendigen = Py.Wahr(stand.Erleichterung("kuendigung", true));
        foreach (var person in stand.Personal.Keys.ToList())
        {
            var tief = kuendigen ? Py.Int(stand.StimmungTief.Get(person, 0L)) : 0;
            if (tief >= Py.Int(Objekt(regeln["stimmung"])["kuendigung_nach"]))
            {
                ergebnis.Add(new PyObjekt { { "art", PersonalKuendigt }, { "id", person }, { "name", namen[person] } });
            }
            else if (tief == 1)
            {
                ergebnis.Add(new PyObjekt { { "art", PersonalWarnung }, { "id", person }, { "name", namen[person] } });
            }
        }
        var gehen = new PySet(ergebnis.Where(e => Py.Gleich(e["art"], PersonalKuendigt)).Select(e => e["id"]));
        var da = stand.Personal.Keys.Where(p => !gehen.Contains(p) && stand.Abwesenheit(p, morgen) is null).ToList();
        var regel = Objekt(regeln["krankheit"]);
        foreach (var person in da)
        {
            var chance = Py.F(regel["chance"]) * Math.Max(0.0, 1 + stand.MackeWert(person, "krank", morgen) / 100.0) * Py.F(stand.Erleichterung("krankheit_faktor", 1.0));
            if (Wurf(startwert, tag, "krank|" + Py.Str(person)) * 100 < chance)
            {
                var tage = Zwischen(startwert, tag, "krank-tage|" + Py.Str(person), Py.Int(regel["tage_min"]), Py.Int(regel["tage_max"]));
                ergebnis.Add(new PyObjekt { { "art", PersonalKrank }, { "id", person }, { "name", namen[person] }, { "von", morgen }, { "bis", morgen + tage } });
            }
        }
        var krank = new PySet(ergebnis.Where(e => Py.Gleich(e["art"], PersonalKrank)).Select(e => e["id"]));
        regel = Objekt(regeln["urlaub"]);
        // Kleine Story-Elemente: hoechstens eine Anfrage pro Feierabend und dazwischen Ruhe
        var protokoll = stand.PersonalProtokoll.Cast<PyTupel>().Select(p => (Tag: Py.Int(p[0]), Eintrag: Objekt(p[1]))).ToList();
        var zuletzt = protokoll.Where(p => Py.Gleich(p.Eintrag["art"], PersonalUrlaub)).Select(p => p.Tag).DefaultIfEmpty(-1000).Max();
        foreach (var person in da)
        {
            var eingestellt = Py.Int(Py.Wahr(Get(Objekt(stand.Personal[person]), "tag", 0L)) ? Get(Objekt(stand.Personal[person]), "tag", 0L) : 0L);
            if (krank.Contains(person) || tag - eingestellt < Py.Int(regel["ab_tagen"]) || tag - zuletzt < Py.Int(Get(regel, "abstand_firma", 0L))
                || ergebnis.Any(e => Py.Gleich(e["art"], PersonalUrlaub)))
            {
                continue;
            }
            var gefragt = protokoll.Where(p => Py.Gleich(p.Eintrag["art"], PersonalUrlaub) && Py.Gleich(p.Eintrag["id"], person)).Select(p => p.Tag).ToList();
            if (gefragt.Count > 0 && tag - gefragt[^1] < Py.Int(regel["abstand"]))
            {
                continue;
            }
            if (stand.Abwesenheiten.Select(Objekt).Any(e => Py.Gleich(e["id"], person) && Py.Gleich(e["art"], AbwesendUrlaub) && Py.Int(e["bis"]) > morgen))
            {
                continue;
            }
            var chance = Py.F(regel["chance"]) * Math.Max(0.0, 1 + stand.MackeWert(person, "urlaub", morgen) / 100.0);
            if (Wurf(startwert, tag, "urlaub|" + Py.Str(person)) * 100 < chance)
            {
                var start = morgen + Py.Int(regel["vorlauf"]);
                var tage = Zwischen(startwert, tag, "urlaub-tage|" + Py.Str(person), Py.Int(regel["tage_min"]), Py.Int(regel["tage_max"]));
                var e = new PyObjekt
                {
                    { "art", PersonalUrlaub }, { "anfrage", Prozent("urlaub:%d:%s", tag, person) }, { "id", person }, { "name", namen[person] },
                    { "von", start }, { "bis", start + tage },
                };
                if (Py.Gleich((stand.MackeVon(person, morgen) ?? [])["id"], "talbahn"))
                {
                    Py.Setzen(e, "talbahn", true);
                }
                ergebnis.Add(e);
            }
        }
        regel = Objekt(regeln["konflikt"]);
        var leute = da.Where(p => !krank.Contains(p)).ToList();
        zuletzt = protokoll.Where(p => Py.Gleich(p.Eintrag["art"], PersonalKonflikt)).Select(p => p.Tag).DefaultIfEmpty(-1000).Max();
        if (leute.Count >= Py.Int(regel["ab_mitarbeiter"]) && tag - zuletzt >= Py.Int(regel["abstand"]))
        {
            var schub = leute.Select(p => stand.MackeWert(p, "konflikt", morgen)).Max();
            var chance = Py.F(regel["chance"]) * Math.Max(0.0, 1 + schub / 100.0) * Py.F(stand.Erleichterung("konflikt_faktor", 1.0));
            if (Wurf(startwert, tag, "konflikt") * 100 < chance)
            {
                var (paar, text) = KonfliktPaar(stand, leute, startwert, tag, inhalt);
                var (erster, zweiter) = (paar[0], paar[1]);
                ergebnis.Add(new PyObjekt
                {
                    { "art", PersonalKonflikt }, { "konflikt", Prozent("konflikt:%d", tag) }, { "a", erster }, { "b", zweiter },
                    { "name_a", namen[erster] }, { "name_b", namen[zweiter] }, { "text", Py.Format(text, PaarNamen(namen[erster], namen[zweiter])) },
                });
            }
        }
        // Ab 0.44: Ein Mitbewerber will jemanden zurueck (nicht, wer schon mit Urlaub oder Streit beschaeftigt ist)
        var vergeben = new PySet(ergebnis.Select(e => e["id"]).Concat(ergebnis.Select(e => e["a"])).Concat(ergebnis.Select(e => e["b"])));
        ergebnis.AddRange(RueckholEreignisse(stand, tag, da.Where(p => !krank.Contains(p)).ToList(), vergeben, inhalt));
        foreach (var person in stand.Personal.Keys)
        {
            if (gehen.Contains(person))
            {
                continue;
            }
            var (vorher, nachher) = (stand.MackeVon(person, tag), stand.MackeVon(person, morgen));
            if (vorher is not null && nachher is not null && Py.Int(nachher["stufe"]) < Py.Int(vorher["stufe"]))
            {
                ergebnis.Add(new PyObjekt
                {
                    { "art", PersonalSchwaecher }, { "id", person }, { "name", namen[person] }, { "macke", nachher["name"] },
                    { "stufe", nachher["stufe"] }, { "stufe_name", nachher["stufe_name"] },
                });
            }
        }
        return [.. ergebnis];
    }

    /// <summary>personal_decisions: offene Entscheidungen zum Personal (Urlaub, Rueckhol, Konflikt).</summary>
    public static List<PyObjekt> PersonalEntscheidungen(Spielstand stand, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var regeln = PersonalRegeln(inhalt);
        var ergebnis = new List<PyObjekt>();
        foreach (var paar in stand.PersonalProtokoll.Cast<PyTupel>())
        {
            var tag = Py.Int(paar[0]);
            var e = Objekt(paar[1]);
            var art = e["art"];
            if (Py.Gleich(art, PersonalUrlaub))
            {
                var schluessel = e["anfrage"];
                if (stand.Antworten.ContainsKey(schluessel) || !stand.Personal.ContainsKey(e["id"]) || Py.Int(e["von"]) <= stand.Tag)
                {
                    continue;
                }
                var erster = KurzName(e["name"]);
                var regel = Objekt(regeln["urlaub"]);
                var vorher = stand.Antworten.Values.Select(Objekt).Where(a => Py.Gleich(a["id"], e["id"]) && Py.Wahr(a["anfrage"])).ToList();
                var wieder = vorher.Count > 0 && Py.Gleich(vorher[^1]["art"], Ereignisse.UrlaubNein);
                var anzahl = Py.Int(e["bis"]) - Py.Int(e["von"]);
                var text = Prozent("%s fragt, ob %s von Arbeitstag %d bis %d Urlaub nehmen darf (%s).", e["name"], erster, e["von"], Py.Int(e["bis"]) - 1, TageText(anzahl));
                var zeile = UrlaubZeile(e, inhalt);
                if (zeile.Length > 0)
                {
                    text += " " + zeile;
                }
                ergebnis.Add(new PyObjekt
                {
                    { "id", schluessel }, { "art", EntscheidungUrlaub }, { "person", e["id"] }, { "name", e["name"] }, { "von", e["von"] }, { "bis", e["bis"] },
                    { "titel", Prozent("Urlaubsanfrage von %s", e["name"]) }, { "text", text },
                    { "optionen", new List<object?>
                    {
                        new PyObjekt { { "id", UrlaubJa }, { "label", "Genehmigen" }, { "problem", "" },
                            { "folge", Prozent("%s fehlt %s, dafür Stimmung %s.", erster, TageText(anzahl), MitVorzeichen(regel["genehmigt"])) } },
                        new PyObjekt { { "id", UrlaubNein }, { "label", "Ablehnen" }, { "problem", "" },
                            { "folge", Prozent("%s bleibt da, aber Stimmung %s%s.", erster, MitVorzeichen(regel[wieder ? "abgelehnt_wieder" : "abgelehnt"]),
                                wieder ? " (schon das zweite Nein in Folge)" : "") } },
                    } },
                });
            }
            else if (Py.Gleich(art, PersonalRueckhol))
            {
                if (stand.Antworten.ContainsKey(e["rueckhol"]) || tag != stand.Tag - 1 || !stand.Personal.ContainsKey(e["id"]))
                {
                    continue;
                }
                ergebnis.Add(RueckholEntscheidung(stand, e, inhalt));
            }
            else if (Py.Gleich(art, PersonalKonflikt))
            {
                var schluessel = e["konflikt"];
                if (stand.Antworten.ContainsKey(schluessel) || tag != stand.Tag - 1 || !stand.Personal.ContainsKey(e["a"]) || !stand.Personal.ContainsKey(e["b"]))
                {
                    continue;
                }
                var regel = Objekt(regeln["konflikt"]);
                var namen = PaarNamen(e["name_a"], e["name_b"]);
                var (erster, zweiter) = (namen["a"], namen["b"]);
                var grenze = EigeneTicketGrenze(stand, inhalt);
                var belegt = stand.UebernommenVon(Selbst).Count >= grenze;
                ergebnis.Add(new PyObjekt
                {
                    { "id", schluessel }, { "art", EntscheidungKonflikt }, { "a", e["a"] }, { "b", e["b"] }, { "name_a", e["name_a"] }, { "name_b", e["name_b"] },
                    { "titel", Prozent("Streit zwischen %s und %s", erster, zweiter) }, { "text", Py.Str(e["text"]) + " Du musst eingreifen." },
                    { "optionen", new List<object?>
                    {
                        new PyObjekt { { "id", Schlichten }, { "label", "Schlichten" },
                            { "folge", Prozent("Ein Gespräch zu dritt kostet dich heute einen Kundenticket-Platz. Beide: Stimmung %s.", MitVorzeichen(regel["schlichten"])) },
                            { "problem", belegt ? "Heute hast du keine Zeit mehr für ein Gespräch, deine Kundenticket-Plätze sind belegt." : "" } },
                        new PyObjekt { { "id", SeiteA }, { "label", Prozent("%s recht geben", erster) }, { "problem", "" },
                            { "folge", Prozent("%s: Stimmung %s. %s: Stimmung %s.", erster, MitVorzeichen(regel["partei_plus"]), zweiter, MitVorzeichen(regel["partei_minus"])) } },
                        new PyObjekt { { "id", SeiteB }, { "label", Prozent("%s recht geben", zweiter) }, { "problem", "" },
                            { "folge", Prozent("%s: Stimmung %s. %s: Stimmung %s.", zweiter, MitVorzeichen(regel["partei_plus"]), erster, MitVorzeichen(regel["partei_minus"])) } },
                        new PyObjekt { { "id", Ignorieren }, { "label", "Ignorieren" }, { "problem", "" },
                            { "folge", Prozent("Beide: Stimmung %s und %s lang %d %% weniger Leistung.", MitVorzeichen(regel["ignorieren"]),
                                TageText(Py.Int(regel["verstimmt_tage"])), regel["verstimmt_leistung"]) } },
                    } },
                });
            }
        }
        return ergebnis;
    }

    /// <summary>decision_result_text: kurze Bestaetigung nach einer Entscheidung.</summary>
    public static string EntscheidungErgebnisText(PyObjekt entscheidung, object? wahl)
    {
        var option = Liste(entscheidung["optionen"]).Select(Objekt).FirstOrDefault(o => Py.Gleich(o["id"], wahl));
        var label = option is not null ? option["label"] : wahl;
        return Prozent("%s: %s. %s", entscheidung["titel"], label, option is not null ? option["folge"] : "");
    }

    /// <summary>personal_news: Story-Moment am Morgen zum Personal.</summary>
    public static string PersonalNachricht(Spielstand stand, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var zeilen = new List<string>();
        foreach (var paar in stand.PersonalProtokoll.Cast<PyTupel>())
        {
            if (Py.Int(paar[0]) != stand.Tag - 1)
            {
                continue;
            }
            var e = Objekt(paar[1]);
            var art = e["art"];
            var erster = Py.Wahr(e["name"]) ? KurzName(Get(e, "name", "")) : "";
            if (Py.Gleich(art, PersonalKrank))
            {
                zeilen.Add(Prozent("%s hat sich krankgemeldet und fällt %s aus (bis einschließlich Arbeitstag %d).", e["name"],
                    TageText(Py.Int(e["bis"]) - Py.Int(e["von"])), Py.Int(e["bis"]) - 1));
            }
            else if (Py.Gleich(art, PersonalUrlaub))
            {
                zeilen.Add(Prozent("%s möchte Urlaub nehmen und wartet auf deine Antwort.", e["name"]));
            }
            else if (Py.Gleich(art, PersonalKonflikt))
            {
                zeilen.Add("Ärger im Team: " + Py.Str(e["text"]));
            }
            else if (Py.Gleich(art, PersonalKuendigt))
            {
                zeilen.Add(Prozent("%s hat gekündigt. Die Stimmung war zu lange im Keller, der Platz ist wieder frei.", e["name"]));
            }
            else if (Py.Gleich(art, PersonalWarnung))
            {
                zeilen.Add(Prozent("%s wirkt sehr unzufrieden. Wenn sich nichts bessert, kündigt %s bald.", e["name"], erster));
            }
            else if (Py.Gleich(art, PersonalSchwaecher))
            {
                zeilen.Add(Prozent("%s hat sich eingearbeitet: Die Macke „%s“ wirkt schwächer (jetzt %s).", e["name"], e["macke"], e["stufe_name"]));
            }
            else if (Py.Gleich(art, PersonalRueckhol))
            {
                zeilen.Add("Abwerbeversuch: " + Py.Str(e["text"]));
            }
        }
        foreach (var wert in stand.Zurueckgekehrt.Values)
        {
            var t = (PyTupel)wert!;
            if (Py.Int(t[0]) == stand.Tag - 1)
            {
                zeilen.Add(Prozent("%s arbeitet ab heute wieder bei %s. Der Platz ist frei.", t[2], Mitbewerber(Py.Wahr(t[1]) ? t[1] : Bitweiche, inhalt)["kurz"]));
            }
        }
        foreach (var e in stand.Abwesenheiten.Select(Objekt))
        {
            if (Py.Gleich(e["art"], AbwesendUrlaub) && Py.Int(e["von"]) == stand.Tag && stand.Personal.ContainsKey(e["id"]))
            {
                zeilen.Add(Prozent("%s ist ab heute im Urlaub (bis einschließlich Arbeitstag %d).", Get(Objekt(stand.Personal[e["id"]]), "name", ""), Py.Int(e["bis"]) - 1));
            }
        }
        if (stand.OffeneEntscheidungen().Count > 0)
        {
            zeilen.Add("Unter Firma > Mitarbeiter wartet eine Entscheidung auf dich.");
        }
        return string.Join("\n", zeilen);
    }

    /// <summary>conflict_choice_text: wie du dich bei einem Konflikt entschieden hast.</summary>
    public static string KonfliktWahlText(PyObjekt daten)
    {
        var namen = PaarNamen(Get(daten, "name_a", ""), Get(daten, "name_b", ""));
        var wahl = daten["wahl"];
        return Py.Gleich(wahl, Schlichten) ? "Du hast geschlichtet."
            : Py.Gleich(wahl, SeiteA) ? Prozent("Du hast %s recht gegeben.", namen["a"])
            : Py.Gleich(wahl, SeiteB) ? Prozent("Du hast %s recht gegeben.", namen["b"])
            : "Du hast den Streit ignoriert.";
    }

    /// <summary>learned_today: was die Mitarbeiter mit diesem Feierabend dazulernen (nur fuer die Anzeige).</summary>
    public static List<object?> HeuteGelernt(Spielstand stand, Spielstand nachher, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var deckel = LernDeckel(inhalt);
        var ergebnis = new List<object?>();
        foreach (var eintrag in nachher.LernProtokoll.Cast<PyTupel>())
        {
            if (Py.Int(eintrag[0]) != stand.Tag)
            {
                continue;
            }
            var person = eintrag[1];
            var (vorher, jetzt) = (Py.Int(eintrag[3]), Py.Int(eintrag[4]));
            if (jetzt > vorher)
            {
                ergebnis.Add(new PyObjekt
                {
                    { "id", person }, { "name", Get(nachher.Personal.Get(person) as PyObjekt ?? [], "name", "") }, { "thema", eintrag[2] },
                    { "vorher", vorher }, { "nachher", jetzt }, { "grund", eintrag[5] }, { "grenze", !Py.Kleiner(eintrag[4], deckel) },
                });
            }
        }
        return ergebnis;
    }
}
