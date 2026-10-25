using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Rechner;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: Grossauftrag Serverfarm als Modulfunktionen von <c>fisi_game.py</c> (Python 0.63.2,
/// Z. 15095-16200): Server-Bestueckung (fit_*), Phasen, Team, Aufgaben, Feierabend und Abnahme.
/// Ohne <c>fit_bars</c> (braucht <c>rack_bars</c>, kommt mit der Darstellung).
/// </summary>
public static partial class Betrieb
{
    public const string FarmOrt = "lokschuppen";          // FARM_PLACE
    public const string FarmAnsicht = "serverfarm";       // FARM_VIEW
    public const string FarmAngebotTyp = "angebot";       // FARM_OFFER_TYPE
    public const string FarmLieferung = "lieferung";      // FARM_DELIVERY
    public const string FarmEinbau = "einbau";            // FARM_CABLING
    public const string FarmPlanung = "planung";          // FARM_PLANNING
    public const string FarmBestellung = "sf-bestellung"; // FARM_ORDER
    public const string FarmAngebot = "sf-angebot";       // FARM_OFFER
    public const string FarmTitel = "Großauftrag Serverfarm";   // FARM_TITLE

    /// <summary>FIT_KINDS</summary>
    public static readonly IReadOnlyList<string> FitArten = ["laufwerk", "ram", "cpu", "nic", "netzteil"];

    /// <summary>FIT_KIND_NAMES</summary>
    public static readonly IReadOnlyDictionary<string, string> FitArtNamen = new Dictionary<string, string>
    {
        ["laufwerk"] = "Laufwerke", ["ram"] = "Arbeitsspeicher", ["cpu"] = "Prozessoren", ["nic"] = "Netzwerkkarte", ["netzteil"] = "Netzteile",
    };

    /// <summary>FIT_COUNT_KEYS</summary>
    public static readonly IReadOnlyDictionary<string, string> FitAnzahlSchluessel = new Dictionary<string, string>
    {
        ["laufwerk"] = "laufwerke", ["ram"] = "ram_anzahl", ["cpu"] = "cpu_anzahl", ["netzteil"] = "netzteile",
    };

    /// <summary>RAID_LEVELS</summary>
    public static readonly IReadOnlyList<string> RaidStufen = ["RAID 0", "RAID 1", "RAID 5", "RAID 6", "RAID 10"];

    /// <summary>farm_content</summary>
    public static PyObjekt FarmInhalt(PyObjekt inhalt) => OderLeer(inhalt["serverfarm"]);

    /// <summary>farm_rules: balancing.json "serverfarm".</summary>
    public static PyObjekt FarmRegeln(PyObjekt inhalt) => OderLeer(Objekt(inhalt["balancing"])["serverfarm"]);

    /// <summary>farm_team_revenue: Prozent des Routineumsatzes, die Leute im Serverfarm-Team weiter bringen.</summary>
    public static long FarmTeamUmsatz(PyObjekt inhalt) => Math.Max(0, Math.Min(100, Py.Int(Get(FarmRegeln(inhalt), "team_umsatz", 0L))));

    /// <summary>farm_team_note</summary>
    public static string FarmTeamHinweis(PyObjekt inhalt)
    {
        var anteil = FarmTeamUmsatz(inhalt);
        const string text = "Wer an der Serverfarm mitarbeitet, macht keine Kundentickets und keine Projekte.";
        return anteil >= 100 ? text + " Die Routineaufträge laufen nebenbei voll weiter."
            : anteil == 50 ? text + " Die Routineaufträge laufen nebenbei zur Hälfte weiter."
            : anteil > 0 ? text + Prozent(" Die Routineaufträge laufen nebenbei zu %d %% weiter.", anteil)
            : text + " Auch keine Routineaufträge.";
    }

    /// <summary>farm_phases</summary>
    public static List<PyObjekt> FarmPhasen(PyObjekt inhalt) => [.. ListeOderLeer(FarmInhalt(inhalt), "phasen").Select(Objekt)];

    /// <summary>farm_phase_rule</summary>
    public static PyObjekt? FarmPhase(object? phase, PyObjekt inhalt) => FarmPhasen(inhalt).FirstOrDefault(p => Py.Gleich(p["id"], phase));

    /// <summary>farm_tasks</summary>
    public static List<PyObjekt> FarmAufgaben(object? phase, PyObjekt inhalt) =>
        [.. ListeOderLeer(FarmInhalt(inhalt), "aufgaben").Select(Objekt).Where(a => phase is null || Py.Gleich(a["phase"], phase))];

    /// <summary>farm_task</summary>
    public static PyObjekt? FarmAufgabe(object? kennung, PyObjekt inhalt) =>
        ListeOderLeer(FarmInhalt(inhalt), "aufgaben").Select(Objekt).FirstOrDefault(a => Py.Gleich(a["id"], kennung));

    /// <summary>farm_rack_task</summary>
    public static PyObjekt? FarmRackAufgabe(object? kennung, PyObjekt inhalt) =>
        FarmAufgabe(kennung, inhalt) is PyObjekt a && Py.Gleich(a["typ"], "rack") ? a : null;

    /// <summary>farm_person</summary>
    public static PyObjekt? FarmPerson(object? kennung, PyObjekt inhalt) =>
        ListeOderLeer(FarmInhalt(inhalt), "personen").Select(Objekt).FirstOrDefault(p => Py.Gleich(p["id"], kennung));

    /// <summary>farm_customer</summary>
    public static PyObjekt FarmKunde(PyObjekt inhalt) => OderLeer(FarmInhalt(inhalt)["kunde"]);

    /// <summary>_eased_days: Tage auf Einfach mal serverfarm_verzug_faktor (aufgerundet).</summary>
    private static long LeichtTage(Spielstand stand, object? tage) =>
        (long)Math.Ceiling(Py.F(Py.Mal(tage, stand.Erleichterung("serverfarm_verzug_faktor", 1.0))));

    /// <summary>farm_unlock_missing: was fuer den Lokschuppen noch fehlt (leer = alles da).</summary>
    public static List<string> FarmFehlt(Spielstand stand, PyObjekt inhalt)
    {
        var regel = OderLeer(FarmRegeln(inhalt)["freischaltung"]);
        if (!Py.Wahr(stand.Firma))
        {
            return ["eine eigene Firma"];
        }
        var fehlt = new List<string>();
        var stufe = Py.Int(Get(regel, "stufe", 4L));
        if (Py.Int(Objekt(stand.Firma)["stufe"]) < stufe)
        {
            var stufen = Liste(Objekt(FirmaRegeln(inhalt)["gebaeude"])["stufen"]);
            var name = stufe <= stufen.Count ? Py.Str(Objekt(stufen[(int)stufe - 1])["name"]) : Py.Str(stufe);
            fehlt.Add(Prozent("Gebäudestufe %d (%s)", stufe, name));
        }
        if (Py.Wahr(regel["raum"]) && !stand.HatRaum(regel["raum"]))
        {
            fehlt.Add(Py.Str(Get(SonderRaum(regel["raum"], inhalt) ?? [], "name", regel["raum"])));
        }
        var fertig = stand.ZertifikateFertig();
        foreach (var z in ListeOderLeer(regel, "zertifizierungen"))
        {
            if (!fertig.Contains(z))
            {
                fehlt.Add(Py.Str(Get(Wirtschaft.Zertifikat(z, inhalt) ?? [], "name", z)));
            }
        }
        return fehlt;
    }

    /// <summary>farm_unlock_text</summary>
    public static string FarmFreiHinweis(Spielstand stand, PyObjekt inhalt)
    {
        var fehlt = FarmFehlt(stand, inhalt);
        return fehlt.Count == 0 ? "Alles erfüllt: Nach dem nächsten Feierabend meldet sich ein neuer Großkunde."
            : Prozent("Ein Großauftrag wartet, sobald deine Firma so weit ist. Es fehlt noch: %s.", string.Join(", ", fehlt));
    }

    /// <summary>new_farm</summary>
    public static PyObjekt NeueFarm(long start) => new()
    {
        { "start", start }, { "aufgaben", new PyObjekt() }, { "team", new List<object?>() }, { "punkte", new PyDict() },
        { "fertig_phasen", new PyDict() }, { "tage", new List<object?>() }, { "tag_set", new PySet() }, { "liefertag", null },
        { "verzug", null }, { "kabelfehler", null }, { "nacharbeit_bis", null }, { "fertig", null }, { "abnahme", null },
        { "beitraege", new PyDict() }, { "lernen", new List<object?>() },
    };

    /// <summary>farm_deadline</summary>
    public static long FarmFrist(PyObjekt farm, PyObjekt inhalt) => Py.Int(farm["start"]) + Py.Int(Get(FarmRegeln(inhalt), "tage", 25L)) - 1;

    /// <summary>farm_current_phase: die laufende Phase oder null, wenn fertig.</summary>
    public static PyObjekt? FarmAktuellePhase(PyObjekt? farm, PyObjekt inhalt)
    {
        if (farm is null || Py.Wahr(farm["fertig"]))
        {
            return null;
        }
        var fertig = (PyDict)farm["fertig_phasen"]!;
        return FarmPhasen(inhalt).FirstOrDefault(p => !fertig.ContainsKey(p["id"]));
    }

    /// <summary>farm_phase_start: erster Arbeitstag einer Phase (oder null).</summary>
    public static long? FarmPhasenStart(PyObjekt farm, object? phase, PyObjekt inhalt)
    {
        object? vorher = null;
        var erste = true;
        foreach (var p in FarmPhasen(inhalt))
        {
            if (Py.Gleich(p["id"], phase))
            {
                if (erste)
                {
                    return Py.Int(farm["start"]);
                }
                var fertig = ((PyDict)farm["fertig_phasen"]!).Get(vorher);
                return fertig is not null ? Py.Int(fertig) + 1 : null;
            }
            vorher = p["id"];
            erste = false;
        }
        return null;
    }

    /// <summary>farm_phase_list: die Phasen mit Stand fuer die Ansicht.</summary>
    public static List<PyObjekt> FarmPhasenListe(Spielstand stand, PyObjekt inhalt)
    {
        var farm = stand.Farm as PyObjekt;
        var jetzt = FarmAktuellePhase(farm, inhalt);
        var ergebnis = new List<PyObjekt>();
        foreach (var phase in FarmPhasen(inhalt))
        {
            var aufgaben = FarmAufgaben(phase["id"], inhalt);
            var fertig = farm is not null ? ((PyDict)farm["fertig_phasen"]!).Get(phase["id"]) : null;
            var zustand = fertig is not null ? "fertig" : jetzt is not null && Py.Gleich(jetzt["id"], phase["id"]) ? "jetzt" : "spaeter";
            ergebnis.Add(new PyObjekt
            {
                { "id", phase["id"] }, { "name", phase["name"] }, { "kurz", phase["kurz"] }, { "soll_tag", phase["soll_tag"] },
                { "status", zustand }, { "start", farm is not null ? FarmPhasenStart(farm, phase["id"], inhalt) : null },
                { "ende", fertig },
                { "punkte", Py.Rund(farm is not null ? ((PyDict)farm["punkte"]!).Get(phase["id"], 0.0) : 0.0, 1) },
                { "team_punkte", phase["team_punkte"] }, { "aufgaben", (long)aufgaben.Count },
                { "erledigt", (long)aufgaben.Count(a => farm is not null && Py.Hat(Objekt(farm["aufgaben"]), Py.Str(a["id"]))) },
            });
        }
        return ergebnis;
    }

    /// <summary>farm_offer_task: das Angebot als Formular mit den Positionen aus serverfarm.json.</summary>
    public static PyObjekt FarmAngebotAufgabe(object? zuschlag, PyObjekt inhalt)
    {
        var aufgabe = FarmAufgabe(FarmAngebot, inhalt) ?? [];
        var angebote = AngebotRegeln(inhalt);
        var daten = OderLeer(FarmInhalt(inhalt)["angebot"]);
        return new PyObjekt
        {
            { "id", FarmAngebot }, { "typ", "formular" }, { "art", "angebot" },
            { "titel", Get(aufgabe, "titel", "Angebot") }, { "ticket", Get(aufgabe, "ticket", "") }, { "frage", Get(aufgabe, "frage", "") },
            { "daten", new PyObjekt
                {
                    { "positionen", ListeOderLeer(daten, "positionen").Select(p => (object?)Py.Flach(Objekt(p))).ToList() },
                    { "einkauf_name", Py.Wahr(daten["einkauf_name"]) ? daten["einkauf_name"] : "Einzelkosten gesamt" },
                    { "handlungskosten", angebote["handlungskosten"] }, { "gewinn", zuschlag }, { "ust", angebote["ust"] },
                }
            },
            { "hilfe", Get(aufgabe, "hilfe", "") }, { "erklaerung", Get(aufgabe, "erklaerung", "") },
        };
    }

    /// <summary>farm_markups</summary>
    public static List<object?> FarmZuschlaege(PyObjekt inhalt) => [.. Liste(AngebotRegeln(inhalt)["zuschlaege"])];

    /// <summary>farm_check_task: die Aufgabe so, wie sie geprueft wird (das Angebot als Formular).</summary>
    public static PyObjekt FarmPruefAufgabe(PyObjekt aufgabe, object? zuschlag, PyObjekt inhalt) =>
        Py.Gleich(aufgabe["typ"], FarmAngebotTyp)
            ? FarmAngebotAufgabe(zuschlag ?? Get(FarmRegeln(inhalt), "zuschlag_max", 20L), inhalt)
            : aufgabe;

    /// <summary>farm_solution: eine richtige Antwort.</summary>
    public static object? FarmLoesung(PyObjekt aufgabe, PyObjekt inhalt) =>
        Py.Gleich(aufgabe["typ"], "bestueckung") ? FitLoesung(aufgabe, inhalt) : Bewertung.Loesung(FarmPruefAufgabe(aufgabe, null, inhalt), null, inhalt);

    /// <summary>farm_evaluate: bewertet eine Serverfarm-Aufgabe (Nutzdaten fuer serverfarm_aufgabe).</summary>
    public static PyObjekt FarmBewerten(Spielstand stand, PyObjekt aufgabe, object? antwort, bool hilfe, PyObjekt? stufen, object? zuschlag, PyObjekt inhalt)
    {
        var regeln = FarmRegeln(inhalt);
        var pruef = FarmPruefAufgabe(aufgabe, zuschlag, inhalt);
        List<string> probleme;
        bool richtig;
        long fehler;
        if (Py.Gleich(aufgabe["typ"], "bestueckung"))
        {
            probleme = FitProbleme(aufgabe, antwort as PyObjekt, inhalt);
            (richtig, fehler) = (probleme.Count == 0, probleme.Count);
        }
        else
        {
            probleme = Bewertung.AntwortProbleme(pruef, antwort, null, inhalt);
            (richtig, fehler) = Bewertung.Pruefen(pruef, antwort, null, inhalt);
        }
        var luecken = Bewertung.Luecken(aufgabe, stufen ?? []);
        var daten = new PyObjekt
        {
            { "aufgabe", aufgabe["id"] }, { "tag", stand.Tag }, { "phase", aufgabe["phase"] }, { "richtig", richtig }, { "fehler", fehler },
            { "hilfe", hilfe }, { "unter_niveau", luecken.Count > 0 }, { "antwort", Py.Kopie(antwort) },
            { "anzahlung", 0L }, { "kosten", 0L }, { "strafe", 0L }, { "reputation", new PyObjekt() },
        };
        if (probleme.Count > 0)
        {
            Py.Setzen(daten, "probleme", probleme.Cast<object?>().ToList());
        }
        if (richtig)
        {
            var rep = new PyObjekt();
            foreach (var p in OderLeer(regeln["richtig"]).Where(p => Py.Wahr(p.Value)))
            {
                rep.Add(p.Key, Py.Int(p.Value));
            }
            Py.Setzen(daten, "reputation", rep);
        }
        else
        {
            var regel = OderLeer(regeln["fehler"]);
            var faktor = luecken.Count > 0 ? Py.F(Get(regel, "unter_niveau_faktor", 1L)) : 1.0;
            Py.Setzen(daten, "strafe", PythonRunden.Runde(Py.F(Get(regel, "geld", 0L)) * faktor * Py.F(stand.Erleichterung("fehlerkosten_faktor", 1.0))));
            var verlust = PythonRunden.Runde(Py.F(Get(regel, "kundenzufriedenheit", 0L)) * faktor * Py.F(stand.Erleichterung("reputation_verlust_faktor", 1.0)));
            if (verlust != 0)
            {
                Py.Setzen(daten, "reputation", new PyObjekt { { "kundenzufriedenheit", verlust } });
            }
        }
        if (Py.Gleich(aufgabe["typ"], FarmAngebotTyp))
        {
            var oben = Py.Int(Get(regeln, "zuschlag_max", 20L));
            var vereinbart = Math.Min(Py.Int(zuschlag), oben);
            var zahlen = Zahlen(Objekt(FarmAngebotAufgabe(vereinbart, inhalt)["daten"]));
            Py.Setzen(daten, "zuschlag", Py.Int(zuschlag));
            Py.Setzen(daten, "vereinbart", vereinbart);
            Py.Setzen(daten, "verhandelt", Py.Int(zuschlag) > oben);
            Py.Setzen(daten, "netto", zahlen["netto"]);
            Py.Setzen(daten, "selbstkosten", zahlen["selbstkosten"]);
            Py.Setzen(daten, "anzahlung", PythonRunden.Runde(Py.F(zahlen["netto"]) * Py.F(Get(regeln, "anzahlung", 30L)) / 100.0));
        }
        var nachbestellung = OderLeer(regeln["nachbestellung"]);
        var aufschlag = 1 + Py.F(Get(nachbestellung, "aufschlag", 15L)) / 100.0 * Py.F(stand.Erleichterung("fehlerkosten_faktor", 1.0));
        if (Py.Gleich(aufgabe["typ"], "bestellung"))
        {
            object summe, laengste;
            if (richtig)
            {
                (summe, laengste) = Bewertung.KorbSumme(aufgabe, antwort as PyObjekt);
            }
            else
            {
                var koerbe = Bewertung.Warenkoerbe(aufgabe, inhalt);
                (summe, laengste) = Bewertung.KorbSumme(aufgabe, koerbe[0]);
                summe = PythonRunden.Runde(Py.F(summe) * aufschlag);
                laengste = Py.Plus(laengste, LeichtTage(stand, Get(nachbestellung, "tage", 2L)));
                Py.Setzen(daten, "nachbestellt", true);
            }
            Py.Setzen(daten, "kosten", Py.Int(summe));
            Py.Setzen(daten, "lieferzeit", Py.Int(laengste));
        }
        if (Py.Gleich(aufgabe["typ"], "bestueckung"))
        {
            object kosten;
            if (richtig)
            {
                kosten = FitWerte(aufgabe, antwort as PyObjekt, inhalt)["kosten"]!;
            }
            else
            {
                kosten = PythonRunden.Runde(Py.F(FitWerte(aufgabe, FitLoesung(aufgabe, inhalt), inhalt)["kosten"]) * aufschlag);
                Py.Setzen(daten, "nachbestellt", true);
            }
            Py.Setzen(daten, "kosten", Py.Int(kosten));
        }
        Py.Setzen(daten, "geld", Py.Int(daten["anzahlung"]) - Py.Int(daten["kosten"]) - Py.Int(daten["strafe"]));
        return daten;
    }

    /// <summary>farm_team_value: Wert einer Person fuer eine Phase (Mittel ihrer Themen der Phase).</summary>
    public static long FarmTeamWert(Spielstand stand, object? person, PyObjekt phase, PyObjekt? stufen)
    {
        var themen = ListeOderLeer(phase, "themen");
        if (themen.Count == 0)
        {
            return 0;
        }
        var werte = Py.Gleich(person, Selbst)
            ? themen.Select(t => (object?)Py.F(Get(stufen ?? [], Py.Str(t), 0L))).ToList()
            : themen.Select(t => (object?)stand.PersonalThemaWert(person, Py.Str(t))).ToList();
        return Py.Rund(Py.F(Py.Summe(werte)) / werte.Count);
    }

    /// <summary>farm_candidates: wer ins Serverfarm-Team kann.</summary>
    public static List<PyObjekt> FarmKandidaten(Spielstand stand, PyObjekt? stufen, PyObjekt inhalt)
    {
        var farm = stand.Farm as PyObjekt;
        var phasen = FarmPhasen(inhalt);
        var phase = FarmAktuellePhase(farm, inhalt) ?? (phasen.Count > 0 ? phasen[0] : []);
        if (!Py.Wahr(phase["themen"]))
        {
            phase = phasen.FirstOrDefault(p => Py.Wahr(p["themen"]) && (farm is null || !((PyDict)farm["fertig_phasen"]!).ContainsKey(p["id"]))) ?? phase;
        }
        var team = farm is not null ? Liste(farm["team"]) : [];
        var grenze = Py.Int(Get(FarmRegeln(inhalt), "team_max", 5L));
        var ergebnis = new List<PyObjekt>();
        foreach (var e in stand.PersonalListe())
        {
            var wert = FarmTeamWert(stand, e["id"], phase, stufen);
            var problem = "";
            if (Py.Wahr(e["projekt"]))
            {
                problem = Prozent("%s arbeitet im Projekt „%s“ mit.", e["name"], Objekt(stand.Projekte[e["projekt"]])["titel"]);
            }
            else if (!Py.Enthaelt(team, e["id"]) && team.Count >= grenze)
            {
                problem = Prozent("Das Team ist voll (höchstens %d).", grenze);
            }
            else if (!Py.Enthaelt(team, e["id"]) && stand.UebernommenVon(e["id"]).Count > 0)
            {
                problem = Prozent("%s hat heute schon ein Kundenticket.", e["name"]);
            }
            var weg = Py.Wahr(e["weiterbildung"]) ? e["weiterbildung"] : e["abwesend"];
            ergebnis.Add(new PyObjekt
            {
                { "an", e["id"] }, { "name", e["name"] }, { "wert", wert },
                { "punkte", MitarbeiterProjektPunkte(stand, e["id"], wert, inhalt) }, { "im_team", Py.Enthaelt(team, e["id"]) },
                { "problem", problem }, { "fehlt", Py.Wahr(weg) ? AbwesendProblem(e["name"], Objekt(weg)) : "" },
                { "phase", Get(phase, "kurz", "") },
            });
        }
        return ergebnis;
    }

    /// <summary>farm_team_points: (Punkte heute, Beitraege) des Teams in einer Phase.</summary>
    public static (double Punkte, List<PyObjekt> Beitraege) FarmTeamPunkte(Spielstand stand, PyObjekt phase, PyObjekt? stufen, PyObjekt inhalt)
    {
        var farm = (PyObjekt)stand.Farm!;
        var anteile = new List<PyObjekt>();
        foreach (var person in Liste(farm["team"]))
        {
            if (!stand.Personal.ContainsKey(person) || stand.Abwesenheit(person) is not null)
            {
                continue;
            }
            var wert = FarmTeamWert(stand, person, phase, stufen);
            anteile.Add(new PyObjekt
            {
                { "an", person }, { "name", PersonName(stand, person) }, { "wert", wert },
                { "punkte", MitarbeiterProjektPunkte(stand, person, wert, inhalt) },
            });
        }
        var summe = PythonRundenStellen.Runde(Py.F(Py.Summe(anteile.Select(a => a["punkte"]))) * TeamFaktor(stand, Liste(farm["team"])), 1);
        return (summe, anteile);
    }

    /// <summary>farm_today_count: heute schon bearbeitete Serverfarm-Aufgaben.</summary>
    public static long FarmHeute(Spielstand stand) => stand.Farm is PyObjekt farm && Py.Wahr(farm)
        ? Objekt(farm["aufgaben"]).Count(p => Py.Int(Get(Objekt(p.Value), "tag", 0L)) == stand.Tag)
        : 0;

    /// <summary>farm_open_tasks: offene Aufgaben der laufenden Phase.</summary>
    public static List<PyObjekt> FarmOffen(Spielstand stand, PyObjekt inhalt)
    {
        var farm = stand.Farm as PyObjekt;
        var phase = FarmAktuellePhase(farm, inhalt);
        if (phase is null)
        {
            return [];
        }
        return [.. FarmAufgaben(phase["id"], inhalt).Where(a => !Py.Hat(Objekt(farm!["aufgaben"]), Py.Str(a["id"])))];
    }

    /// <summary>farm_task_problem: warum eine Aufgabe gerade nicht geht (leer = geht).</summary>
    public static string FarmAufgabeProblem(Spielstand stand, object? aufgabe, PyObjekt inhalt)
    {
        if (stand.Farm is not PyObjekt farm)
        {
            return "Die Serverfarm läuft noch nicht.";
        }
        if (Py.Wahr(farm["fertig"]))
        {
            return "Die Serverfarm ist schon abgenommen.";
        }
        if (Py.Hat(Objekt(farm["aufgaben"]), Py.Str(aufgabe)))
        {
            return "Diese Aufgabe ist schon erledigt.";
        }
        if (!FarmOffen(stand, inhalt).Any(a => Py.Gleich(a["id"], aufgabe)))
        {
            return "Diese Aufgabe kommt erst in einer späteren Phase.";
        }
        var grenze = Py.Int(Get(FarmRegeln(inhalt), "aufgaben_pro_tag", 3L));
        return FarmHeute(stand) >= grenze
            ? Prozent("Heute hast du schon %d Serverfarm-Aufgaben bearbeitet, die nächste gibt es morgen.", grenze)
            : "";
    }

    /// <summary>_farm_roll_delay: Lieferverzug in Tagen (Einfach verkuerzt).</summary>
    private static long FarmVerzugWuerfeln(Spielstand stand, long tag, PyObjekt inhalt)
    {
        var wurf = Wurf(stand.FirmenStartwert, tag, "serverfarm|verzug");
        var zeilen = Py.Wahr(FarmRegeln(inhalt)["verzug"]) ? Liste(FarmRegeln(inhalt)["verzug"]).Select(Liste).ToList()
            : [new List<object?> { 1L, 0L }];
        var summe = 0.0;
        var tage = zeilen[^1][1];
        foreach (var z in zeilen)
        {
            summe += Py.F(z[0]);
            if (wurf < summe)
            {
                tage = z[1];
                break;
            }
        }
        return LeichtTage(stand, tage);
    }

    /// <summary>farm_cable_chance: Chance auf einen Kabelfehler je Rack.</summary>
    public static double FarmKabelChance(Spielstand stand, PyObjekt inhalt)
    {
        var regel = OderLeer(FarmRegeln(inhalt)["kabelfehler"]);
        var team = Liste(((PyObjekt)stand.Farm!)["team"]).Where(p => stand.Personal.ContainsKey(p)).ToList();
        var bester = team.Count == 0 ? 0L : team.Select(p => stand.PersonalThemaWert(p, "verkabelung")).Max();
        var unten = Py.F(Get(regel, "min", 0.05));
        var chance = Py.F(Get(regel, "chance", 0.25)) - bester * Py.F(Get(regel, "je_punkt", 0L));
        return chance > unten ? chance : unten;
    }

    /// <summary>farm_acceptance: Abnahme beim letzten Feierabend.</summary>
    public static PyObjekt FarmAbnahme(Spielstand stand, long tag, PyObjekt inhalt)
    {
        var regeln = FarmRegeln(inhalt);
        var farm = (PyObjekt)stand.Farm!;
        var spaet = Math.Max(0, tag - FarmFrist(farm, inhalt));
        var regel = OderLeer(regeln["strafe"]);
        var hoechst = Py.F(Get(regel, "max_prozent", 10L));
        var roh = spaet * Py.F(Get(regel, "prozent_je_tag", 1L));
        var prozent = (roh < hoechst ? roh : hoechst) * Py.F(stand.Erleichterung("fehlerkosten_faktor", 1.0));
        var angebot = OderLeer(Objekt(farm["aufgaben"])[FarmAngebot]);
        var netto = Py.F(Get(angebot, "netto", 0L));
        var strafe = PythonRunden.Runde(netto * prozent / 100.0);
        var rest = PythonRunden.Runde(netto - Py.Int(Get(angebot, "anzahlung", 0L)) - strafe);
        var aufgaben = FarmAufgaben(null, inhalt);
        var richtig = (long)aufgaben.Count(a => Py.Wahr(OderLeer(Objekt(farm["aufgaben"])[Py.Str(a["id"])])["richtig"]));
        var fehlerfrei = richtig == aufgaben.Count;
        var bonus = fehlerfrei ? Py.Int(Get(regeln, "bonus", 0L)) : 0;
        var rep = new PyObjekt();
        foreach (var p in OderLeer(regeln["abschluss"]))
        {
            rep.Add(p.Key, Py.Int(p.Value));
        }
        if (spaet != 0)
        {
            Py.Setzen(rep, "kundenzufriedenheit", Py.Int(Get(rep, "kundenzufriedenheit", 0L)) + PythonRunden.Runde(
                spaet * Py.F(Get(regel, "kundenzufriedenheit_je_tag", -3L)) * Py.F(stand.Erleichterung("reputation_verlust_faktor", 1.0))));
        }
        var reputation = new PyObjekt();
        foreach (var p in rep.Where(p => Py.Wahr(p.Value)))
        {
            reputation.Add(p.Key, p.Value);
        }
        return new PyObjekt
        {
            { "tag", tag }, { "dauer", tag - Py.Int(farm["start"]) + 1 }, { "verzug", spaet },
            { "strafe_prozent", PythonRundenStellen.Runde(prozent, 1) }, { "strafe", strafe }, { "rest", rest },
            { "bonus", bonus }, { "fehlerfrei", fehlerfrei }, { "puenktlich", spaet == 0 },
            { "richtig", richtig }, { "aufgaben", (long)aufgaben.Count }, { "geld", rest + bonus },
            { "reputation", reputation }, { "stufe", stand.Stufe },
        };
    }

    /// <summary>farm_day: was der Feierabend fuer die Serverfarm bringt (firma.serverfarm) oder null.</summary>
    public static PyObjekt? FarmTag(Spielstand stand, PyObjekt? stufen, PyObjekt inhalt)
    {
        if (stand.Farm is not PyObjekt farm || Py.Wahr(farm["fertig"]) || ((PySet)farm["tag_set"]!).Contains(stand.Tag))
        {
            return null;
        }
        var phase = FarmAktuellePhase(farm, inhalt);
        if (phase is null)
        {
            return null;
        }
        var regeln = FarmRegeln(inhalt);
        var tag = stand.Tag;
        var eintrag = new PyObjekt { { "tag", tag }, { "phase", phase["id"] }, { "punkte", 0.0 }, { "beitraege", new List<object?>() } };
        var noetig = Py.F(Get(phase, "team_punkte", 0L));
        var hat = Py.F(((PyDict)farm["punkte"]!).Get(phase["id"], 0.0));
        if (noetig != 0 && hat < noetig && Liste(farm["team"]).Count > 0)
        {
            var (punkte, anteile) = FarmTeamPunkte(stand, phase, stufen, inhalt);
            Py.Setzen(eintrag, "punkte", punkte);
            Py.Setzen(eintrag, "beitraege", anteile.Cast<object?>().ToList());
            var neu = hat + punkte;
            hat = neu < noetig ? neu : noetig;
        }
        Py.Setzen(eintrag, "stand", PythonRundenStellen.Runde(hat, 1));
        var aufgaben = Objekt(farm["aufgaben"]);
        var bereit = FarmAufgaben(phase["id"], inhalt).All(a => Py.Hat(aufgaben, Py.Str(a["id"]))) && hat >= noetig - 0.001;
        if (Py.Gleich(phase["id"], FarmLieferung))
        {
            bereit = bereit && farm["liefertag"] is not null && tag >= Py.Int(farm["liefertag"]);
        }
        if (Py.Gleich(phase["id"], FarmEinbau) && bereit)
        {
            if (farm["kabelfehler"] is null)
            {
                var chance = FarmKabelChance(stand, inhalt);
                var regel = OderLeer(regeln["kabelfehler"]);
                var kaputt = FarmAufgaben(phase["id"], inhalt).Where(a => Py.Gleich(a["typ"], "rack")
                    && Wurf(stand.FirmenStartwert, tag, "serverfarm|kabel|" + Py.Str(a["id"])) < chance).Select(a => a["id"]).ToList();
                Py.Setzen(eintrag, "kabelfehler", kaputt);
                Py.Setzen(eintrag, "kabel_chance", PythonRundenStellen.Runde(chance, 3));
                if (kaputt.Count > 0)
                {
                    Py.Setzen(eintrag, "nacharbeit_geld", PythonRunden.Runde(kaputt.Count * Py.F(Get(regel, "geld", 400L))
                        * Py.F(stand.Erleichterung("fehlerkosten_faktor", 1.0))));
                    Py.Setzen(eintrag, "nacharbeit_bis", tag + kaputt.Count * Py.Int(Get(regel, "tage", 1L)));
                    bereit = false;
                }
            }
            else if (farm["nacharbeit_bis"] is not null && tag < Py.Int(farm["nacharbeit_bis"]))
            {
                bereit = false;
            }
        }
        if (bereit)
        {
            Py.Setzen(eintrag, "fertig", true);
            if (Py.Gleich(phase["id"], FarmPlanung))
            {
                var bestellung = OderLeer(aufgaben[FarmBestellung]);
                var verzug = FarmVerzugWuerfeln(stand, tag, inhalt);
                var ankunft = Py.Int(Get(bestellung, "tag", tag)) + Py.Int(Get(bestellung, "lieferzeit", 0L)) + verzug;
                Py.Setzen(eintrag, "verzug", verzug);
                Py.Setzen(eintrag, "liefertag", Math.Max(tag + 1, ankunft));
            }
            var phasen = FarmPhasen(inhalt);
            if (ReferenceEquals(phase, phasen[^1]) || Py.Gleich(phase["id"], phasen[^1]["id"]))
            {
                Py.Setzen(eintrag, "abnahme", FarmAbnahme(stand, tag, inhalt));
            }
        }
        return eintrag;
    }

    /// <summary>farm_status: alles fuer die Serverfarm-Ansicht.</summary>
    public static PyObjekt FarmStand(Spielstand stand, PyObjekt? stufen, PyObjekt inhalt)
    {
        var farm = stand.Farm as PyObjekt;
        var info = new PyObjekt
        {
            { "frei", stand.FarmFrei is not null }, { "gestartet", farm is not null }, { "fertig", farm is not null && Py.Wahr(farm["fertig"]) },
            { "kunde", FarmKunde(inhalt) }, { "fehlt", FarmFehlt(stand, inhalt).Cast<object?>().ToList() },
            { "tage", Get(FarmRegeln(inhalt), "tage", 25L) },
        };
        if (farm is null)
        {
            return info;
        }
        var phase = FarmAktuellePhase(farm, inhalt);
        foreach (var p in new PyObjekt
        {
            { "tag", stand.Tag - Py.Int(farm["start"]) + 1 }, { "frist_tag", FarmFrist(farm, inhalt) },
            { "phase", phase }, { "phasen", FarmPhasenListe(stand, inhalt).Cast<object?>().ToList() },
            { "offen", FarmOffen(stand, inhalt).Cast<object?>().ToList() }, { "heute", FarmHeute(stand) },
            { "limit", Py.Int(Get(FarmRegeln(inhalt), "aufgaben_pro_tag", 3L)) },
            { "team", Liste(farm["team"]).Select(p => PersonName(stand, p)).ToList() },
            { "angebot", Objekt(farm["aufgaben"])[FarmAngebot] }, { "abnahme", farm["abnahme"] },
            { "liefertag", farm["liefertag"] }, { "verzug", farm["verzug"] },
            { "kabelfehler", farm["kabelfehler"] }, { "nacharbeit_bis", farm["nacharbeit_bis"] },
        })
        {
            Py.Setzen(info, p.Key, p.Value);
        }
        if (phase is not null && Py.Wahr(phase["team_punkte"]) && Liste(farm["team"]).Count > 0)
        {
            Py.Setzen(info, "team_heute", FarmTeamPunkte(stand, phase, stufen, inhalt).Punkte);
        }
        return info;
    }

    /// <summary>farm_badge_count: Zahl am Lokschuppen auf der Karte.</summary>
    public static long FarmMarke(Spielstand? stand, PyObjekt inhalt)
    {
        if (stand is null || stand.FarmFrei is null)
        {
            return 0;
        }
        if (stand.Farm is not PyObjekt farm)
        {
            return 1;
        }
        if (Py.Wahr(farm["fertig"]))
        {
            return 0;
        }
        var rest = Py.Int(Get(FarmRegeln(inhalt), "aufgaben_pro_tag", 3L)) - FarmHeute(stand);
        return Math.Max(0, Math.Min(rest, FarmOffen(stand, inhalt).Count));
    }

    /// <summary>farm_place_text: eine Zeile zum Lokschuppen fuer die Kartenliste.</summary>
    public static string FarmOrtText(Spielstand? stand, PyObjekt inhalt)
    {
        if (stand?.Farm is not PyObjekt farm)
        {
            return Prozent("%s: Ausschreibung für ein Rechenzentrum.", Get(FarmKunde(inhalt), "name", ""));
        }
        if (Py.Wahr(farm["fertig"]))
        {
            return "Rechenzentrum in Betrieb, von deiner Firma gebaut.";
        }
        var phase = FarmAktuellePhase(farm, inhalt) ?? [];
        return Prozent("Serverfarm: Tag %d von %d · Phase %s", stand.Tag - Py.Int(farm["start"]) + 1,
            Py.Int(Get(FarmRegeln(inhalt), "tage", 25L)), Get(phase, "kurz", ""));
    }

    /// <summary>farm_unlocked_text</summary>
    public static string FarmFreiText(PyObjekt inhalt) =>
        Prozent("Neuer Großkunde: %s baut den alten Lokschuppen zum Rechenzentrum um und sucht "
            + "einen IT-Partner. Der Lokschuppen steht jetzt auf der Karte.", Get(FarmKunde(inhalt), "name", ""));

    /// <summary>farm_day_text: Zeile(n) fuer die Feierabend-Anzeige zur Serverfarm.</summary>
    public static List<string> FarmTagText(Spielstand? stand, PyObjekt eintrag, PyObjekt inhalt)
    {
        _ = stand;
        var phase = FarmPhase(eintrag["phase"], inhalt) ?? [];
        var zeilen = new List<string>();
        if (Py.Wahr(eintrag["punkte"]))
        {
            zeilen.Add(Prozent("Serverfarm (%s): Team +%s Punkte, jetzt %s von %d.", Get(phase, "kurz", ""), Num(eintrag["punkte"]),
                Num(Get(eintrag, "stand", 0L)), Get(phase, "team_punkte", 0L)));
        }
        if (Py.Wahr(eintrag["kabelfehler"]))
        {
            var tage = Py.Int(eintrag["nacharbeit_bis"]) - Py.Int(eintrag["tag"]);
            var namen = Liste(eintrag["kabelfehler"]).Select(a => Py.Str(Get(FarmAufgabe(a, inhalt) ?? [], "titel", a)).Split(':')[0]);
            zeilen.Add(Prozent("Kabeltest: Fehler in %s. Die Nacharbeit kostet %s und %d Tag%s.", string.Join(" und ", namen),
                Euro(Get(eintrag, "nacharbeit_geld", 0L)), tage, tage == 1 ? "" : "e"));
        }
        else if (eintrag["kabelfehler"] is List<object?> { Count: 0 } && Py.Gleich(phase["id"], FarmEinbau))
        {
            zeilen.Add("Kabeltest bestanden: Beide Racks sind sauber verkabelt.");
        }
        if (Py.Wahr(eintrag["fertig"]))
        {
            zeilen.Add(Prozent("Serverfarm: Phase „%s“ abgeschlossen.", Get(phase, "name", "")));
        }
        if (Py.Hat(eintrag, "verzug"))
        {
            var verzug = eintrag["verzug"];
            zeilen.Add(Prozent("Die Ware kommt am Arbeitstag %d%s.", eintrag["liefertag"], Py.Wahr(verzug)
                ? Prozent(" (Lieferverzug %d Tag%s)", verzug, Py.Int(verzug) == 1 ? "" : "e") : ""));
        }
        if (Py.Wahr(eintrag["abnahme"]))
        {
            var daten = Objekt(eintrag["abnahme"]);
            var text = Prozent("Abnahme bestanden! Restzahlung %s", Euro(daten["rest"]));
            if (Py.Wahr(daten["strafe"]))
            {
                text += Prozent(" nach Vertragsstrafe %s (%d Tag%s zu spät)", Euro(daten["strafe"]), daten["verzug"], Py.Int(daten["verzug"]) == 1 ? "" : "e");
            }
            if (Py.Wahr(daten["bonus"]))
            {
                text += Prozent(", dazu %s Bonus für die fehlerfreie Abnahme", Euro(daten["bonus"]));
            }
            zeilen.Add(text + ".");
        }
        return zeilen;
    }

    /// <summary>farm_result_text: (Ueberschrift, Text) nach dem Abschicken einer Serverfarm-Aufgabe.</summary>
    public static (string Kopf, string Text) FarmErgebnisText(PyObjekt daten, PyObjekt inhalt)
    {
        var aufgabe = FarmAufgabe(daten["aufgabe"], inhalt) ?? [];
        var teile = new List<string>();
        string kopf;
        if (Py.Wahr(daten["richtig"]))
        {
            kopf = "Richtig gelöst";
        }
        else
        {
            kopf = "Nicht ganz richtig";
            if (Py.Wahr(daten["strafe"]))
            {
                teile.Add(Prozent("Die Nacharbeit kostet %s.", Euro(daten["strafe"])));
            }
        }
        if (Py.Gleich(aufgabe["typ"], FarmAngebotTyp))
        {
            if (Py.Wahr(daten["verhandelt"]))
            {
                teile.Add(Prozent("Dr. Arendt verhandelt: %d %% Gewinnzuschlag sind ihr zu viel, sie bietet %d %%. Ihr werdet euch einig.",
                    daten["zuschlag"], daten["vereinbart"]));
            }
            teile.Add(Prozent("Auftrag erteilt: %s netto, die Anzahlung von %s ist da.", Euro(daten["netto"]), Euro(daten["anzahlung"])));
        }
        if (Py.Wahr(daten["kosten"]))
        {
            teile.Add(Prozent("%s %s.", Py.Wahr(daten["nachbestellt"]) ? "Express-Nachbestellung der richtigen Ware:" : "Hardware bezahlt:", Euro(daten["kosten"])));
        }
        if (Py.Wahr(daten["lieferzeit"]))
        {
            teile.Add(Prozent("Längste Lieferzeit: %s.", Plural(daten["lieferzeit"], "Arbeitstag", "Arbeitstage")));
        }
        return (kopf, string.Join(" ", teile));
    }

    /// <summary>farm_solution_lines: "So waere es gegangen" als Zeilen.</summary>
    public static List<string> FarmLoesungZeilen(PyObjekt aufgabe, PyObjekt inhalt)
    {
        var loesung = FarmLoesung(aufgabe, inhalt);
        if (loesung is null)
        {
            return [];
        }
        if (Py.Gleich(aufgabe["typ"], "bestueckung"))
        {
            return FitZeilen(aufgabe, (PyObjekt)loesung, inhalt);
        }
        if (Py.Gleich(aufgabe["typ"], FarmAngebotTyp))
        {
            var oben = Get(FarmRegeln(inhalt), "zuschlag_max", 20L);
            return [.. Bewertung.AngebotWerte(Objekt(FarmAngebotAufgabe(oben, inhalt)["daten"])).Select(w => Prozent("%s: %s", w.Name, Euro(w.Wert)))];
        }
        return !Py.Gleich(aufgabe["typ"], "auswahl")
            ? [.. Rechner.PythonZeichen.SplitLines(SpielTexte.LoesungText(aufgabe, null, inhalt)).Where(z => z.Length > 0)]
            : [Py.Str(aufgabe["antwort"])];
    }

    /// <summary>farm_journey: Eintraege fuer "Meine Reise" [(tag, art, titel, text)].</summary>
    public static List<(long Tag, string Art, string Titel, string Text)> FarmReise(Spielstand stand, PyObjekt inhalt)
    {
        var eintraege = new List<(long, string, string, string)>();
        var kunde = FarmKunde(inhalt);
        if (stand.FarmFrei is not null)
        {
            eintraege.Add((Py.Int(stand.FarmFrei), "serverfarm_frei", Prozent("Neuer Großkunde: %s", Get(kunde, "kurz", "")),
                Prozent("%s meldet sich: Der Lokschuppen wird zum Rechenzentrum.", Get(kunde, "name", ""))));
        }
        if (stand.Farm is not PyObjekt farm)
        {
            return eintraege;
        }
        if (Objekt(farm["aufgaben"])[FarmAngebot] is PyObjekt angebot && Py.Wahr(angebot))
        {
            eintraege.Add((Py.Int(angebot["tag"]), "serverfarm_auftrag", "Serverfarm: Auftrag erhalten",
                Prozent("Angebot über %s netto angenommen%s.", Euro(angebot["netto"]),
                    Py.Wahr(angebot["verhandelt"]) ? Prozent(", nach Verhandlung auf %d %%", angebot["vereinbart"]) : "")));
        }
        var phasen = FarmPhasen(inhalt);
        foreach (var phase in phasen)
        {
            var fertig = ((PyDict)farm["fertig_phasen"]!).Get(phase["id"]);
            if (fertig is not null && !ReferenceEquals(phase, phasen[^1]))
            {
                eintraege.Add((Py.Int(fertig), "serverfarm_phase", Prozent("Serverfarm: %s fertig", phase["kurz"]),
                    Prozent("Phase „%s“ abgeschlossen.", phase["name"])));
            }
        }
        if (Py.Wahr(farm["verzug"]))
        {
            var v = Py.Int(farm["verzug"]);
            eintraege.Add((FarmPhasenStart(farm, FarmLieferung, inhalt) is long s && s != 0 ? s : Py.Int(farm["start"]), "serverfarm_verzug",
                "Serverfarm: Lieferverzug", Prozent("Die Hardware kommt %d Tag%s später.", v, v == 1 ? "" : "e")));
        }
        if (Py.Wahr(farm["kabelfehler"]))
        {
            var tag = Py.Wahr(farm["nacharbeit_bis"]) ? Py.Int(farm["nacharbeit_bis"]) - 1 : Py.Int(farm["start"]);
            var n = (long)Liste(farm["kabelfehler"]).Count;
            eintraege.Add((tag, "serverfarm_kabel", "Serverfarm: Kabelfehler", Prozent("Nacharbeit an %d Rack%s.", n, n == 1 ? "" : "s")));
        }
        if (Py.Wahr(farm["abnahme"]))
        {
            var d = Objekt(farm["abnahme"]);
            eintraege.Add((Py.Int(d["tag"]), "serverfarm_abnahme", "Serverfarm abgenommen",
                Prozent("Nach %s%s, %d von %d Aufgaben richtig%s.", Plural(d["dauer"], "Arbeitstag", "Arbeitstagen"),
                    Py.Wahr(d["puenktlich"]) ? " (pünktlich)" : Prozent(" (%d Tag%s zu spät)", d["verzug"], Py.Int(d["verzug"]) == 1 ? "" : "e"),
                    d["richtig"], d["aufgaben"], Py.Wahr(d["fehlerfrei"]) ? ", fehlerfreie Abnahme" : "")));
        }
        return eintraege;
    }

    // -- Server-Bestueckung (ab 0.52) ---------------------------------------

    /// <summary>fit_part</summary>
    public static PyObjekt? FitTeil(object? kennung, PyObjekt inhalt) =>
        ListeOderLeer(FarmInhalt(inhalt), "teile").Select(Objekt).FirstOrDefault(t => Py.Gleich(t["id"], kennung));

    /// <summary>fit_parts: Teile einer Art, guenstigstes zuerst.</summary>
    public static List<PyObjekt> FitTeile(string art, PyObjekt inhalt) =>
        [.. ListeOderLeer(FarmInhalt(inhalt), "teile").Select(Objekt).Where(t => Py.Gleich(t["typ"], art))
            .OrderBy(t => Py.F(t["preis"])).ThenBy(t => Py.Str(t["id"]), StringComparer.Ordinal)];

    /// <summary>fit_templates</summary>
    public static List<PyObjekt> FitVorlagen(PyObjekt aufgabe) => [.. ListeOderLeer(OderLeer(aufgabe["bestueckung"]), "vorlagen").Select(Objekt)];

    /// <summary>_tb: Groesse in GB als lesbarer Text (TB ab 1000 GB).</summary>
    public static string Tb(object? gb)
    {
        var g = Py.F(gb);
        return g >= 1000 ? Prozent("%s TB", Num(PythonRundenStellen.Runde(g / 1000.0, 2))) : Prozent("%s GB", Num(PythonRundenStellen.Runde(g, 1)));
    }

    /// <summary>fit_part_text: "SSD 1,92 TB · 290 €"</summary>
    public static string FitTeilText(PyObjekt teil)
    {
        var art = Py.Str(teil["typ"]);
        string angabe;
        if (art == "laufwerk")
        {
            angabe = Prozent("%s %s", Py.Str(Get(teil, "art", "")).ToUpperInvariant(), Tb(teil["groesse"]));
        }
        else if (art == "ram")
        {
            angabe = Prozent("%d GB", teil["groesse"]);
        }
        else if (art == "cpu")
        {
            // ab 0.53 kurz (PC und Handy gleich)
            return Prozent("CPU %d Kerne · %s", teil["kerne"], Euro(teil["preis"]));
        }
        else if (art == "nic")
        {
            angabe = Prozent("2 × %d GbE", teil["gbit"]);
        }
        else
        {
            angabe = Prozent("%d W", teil["leistung"]);
        }
        return Prozent("%s · %s", Py.Wahr(teil["name"]) ? teil["name"] : angabe, Euro(teil["preis"]));
    }

    /// <summary>fit_options: Auswahl fuer eine Art [(teil_id, text)].</summary>
    public static List<(object? Id, string Text)> FitAuswahl(string art, PyObjekt inhalt) => [.. FitTeile(art, inhalt).Select(t => (t["id"], FitTeilText(t)))];

    /// <summary>fit_count_range: moegliche Stueckzahlen je Art.</summary>
    public static List<long> FitAnzahlen(PyObjekt vorlage, string art)
    {
        long Bis(string k, long std) => Py.Int(Get(vorlage, k, std));
        var oben = art switch
        {
            "laufwerk" => Bis("schaechte", 0),
            "ram" => Bis("ram_baenke", 0),
            "cpu" => Bis("sockel", 0),
            "netzteil" => Bis("netzteil_plaetze", 1),
            _ => 1,
        };
        var liste = new List<long>();
        for (var i = 1L; i <= oben; i++)
        {
            liste.Add(i);
        }
        return liste;
    }

    /// <summary>fit_kinds: welche Arten eine Vorlage hat.</summary>
    public static List<string> FitArtenVon(PyObjekt vorlage)
    {
        var arten = new List<string> { "laufwerk" };
        if (Py.Wahr(vorlage["ram_baenke"]))
        {
            arten.Add("ram");
        }
        if (Py.Wahr(vorlage["sockel"]))
        {
            arten.Add("cpu");
        }
        return [.. arten, "nic", "netzteil"];
    }

    /// <summary>_int: int(wert), bei TypeError/ValueError der Standard.</summary>
    private static long GanzOder(object? wert, long standard)
    {
        switch (wert)
        {
            case long or int or bool:
                return Py.Int(wert);
            case double d:
                return Py.Int(d);
            case string s when Rechner.PythonZeichen.Ganzzahl(s, out BigInteger n):
                return (long)n;
            default:
                return standard;
        }
    }

    private static PyObjekt? RaidObjekt(string stufe, long platten, object? groesse)
    {
        var w = Raid.EingabeWerte(stufe, (int)platten, Py.F(groesse));
        return w is null ? null : new PyObjekt
        {
            { "brutto", w.Brutto }, { "netto", w.Netto }, { "verlust", w.Verlust }, { "effizienz", w.Effizienz }, { "toleranz", (long)w.Toleranz },
        };
    }

    /// <summary>fit_server: Werte eines bestueckten Servers.</summary>
    public static PyObjekt FitServer(PyObjekt vorlage, PyObjekt? auswahl, PyObjekt inhalt)
    {
        auswahl ??= [];
        var werte = new PyObjekt
        {
            { "fehlt", new List<object?>() }, { "kosten", 0L }, { "watt", Py.Int(Get(vorlage, "grundlast", 0L)) }, { "ram", 0L }, { "kerne", 0L },
            { "gbit", 0L }, { "laufwerke", 0L }, { "raid", auswahl["raid"] }, { "raid_werte", null }, { "laufwerk", null },
            { "netzteil", null }, { "netzteile", 0L },
        };
        foreach (var art in FitArtenVon(vorlage))
        {
            var teil = FitTeil(auswahl[art], inhalt);
            if (teil is null || !Py.Gleich(teil["typ"], art))
            {
                Liste(werte["fehlt"]).Add(art);
                continue;
            }
            var anzahl = FitAnzahlSchluessel.TryGetValue(art, out var k) ? GanzOder(auswahl[k], 1) : 1;
            anzahl = Math.Max(0, anzahl);
            Py.Setzen(werte, "kosten", Py.Plus(werte["kosten"], Py.Mal(teil["preis"], anzahl)));
            Py.Setzen(werte, "watt", Py.Plus(werte["watt"], Py.Mal(Get(teil, "watt", 0L), anzahl)));
            switch (art)
            {
                case "laufwerk":
                    Py.Setzen(werte, "laufwerk", teil);
                    Py.Setzen(werte, "laufwerke", anzahl);
                    if (auswahl["raid"] is string r && RaidStufen.Contains(r))
                    {
                        Py.Setzen(werte, "raid_werte", RaidObjekt(r, anzahl, teil["groesse"]));
                    }
                    break;
                case "ram":
                    Py.Setzen(werte, "ram", Py.Mal(teil["groesse"], anzahl));
                    Py.Setzen(werte, "ram_anzahl", anzahl);
                    break;
                case "cpu":
                    Py.Setzen(werte, "kerne", Py.Mal(teil["kerne"], anzahl));
                    Py.Setzen(werte, "cpu_anzahl", anzahl);
                    break;
                case "nic":
                    Py.Setzen(werte, "gbit", teil["gbit"]);
                    break;
                default:
                    Py.Setzen(werte, "netzteil", teil);
                    Py.Setzen(werte, "netzteile", anzahl);
                    break;
            }
        }
        return werte;
    }

    /// <summary>fit_psu_needed: so viel muss ein Netzteil allein schaffen (mit Reserve wie beim PC).</summary>
    public static long FitNetzteilNoetig(object? watt, PyObjekt inhalt)
    {
        var reserve = Get(OderLeer(Objekt(inhalt["hardware"])["regeln"]), "netzteil_reserve", 1.3);
        return (long)Math.Ceiling(Py.F(Py.Mal(watt, reserve)));
    }

    /// <summary>fit_server_checks: Lastenheft einer Vorlage mit Haken.</summary>
    public static List<(string Text, bool Ok)> FitServerPruefungen(PyObjekt vorlage, PyObjekt werte, PyObjekt inhalt)
    {
        var soll = OderLeer(vorlage["soll"]);
        var pruef = new List<(string, bool)>();
        void Dazu(string text, bool ok) => pruef.Add((text, ok));
        var fehlt = Liste(werte["fehlt"]);
        foreach (var art in fehlt)
        {
            Dazu(Prozent("%s ausgewählt", FitArtNamen[Py.Str(art)]), false);
        }
        if (werte["laufwerk"] is PyObjekt platte && Py.Wahr(platte))
        {
            Dazu(Prozent("höchstens %d Laufwerke (Schächte)", vorlage["schaechte"]), !Py.Kleiner(vorlage["schaechte"], werte["laufwerke"]));
            if (Py.Wahr(soll["art"]))
            {
                Dazu(Prozent("Laufwerke: %s", Py.Str(soll["art"]).ToUpperInvariant()), Py.Gleich(platte["art"], soll["art"]));
            }
            var raid = werte["raid_werte"] as PyObjekt;
            if (werte["raid"] is not string stufe || !RaidStufen.Contains(stufe))
            {
                Dazu("RAID-Level gewählt", false);
            }
            else
            {
                Dazu(Prozent("%s mit %s möglich", stufe, Plural(werte["laufwerke"], "Laufwerk", "Laufwerken")), raid is not null);
                if (Py.Wahr(soll["raid"]))
                {
                    Dazu(Prozent("RAID-Level %s", string.Join(" oder ", Liste(soll["raid"]).Select(Py.Str))), Py.Enthaelt(soll["raid"], stufe));
                }
            }
            if (Py.Wahr(soll["netto_min"]))
            {
                Dazu(Prozent("mind. %s nutzbar (jetzt %s)", Tb(soll["netto_min"]), Tb(raid is not null ? raid["netto"] : 0L)),
                    raid is not null && !Py.Kleiner(raid["netto"], soll["netto_min"]));
            }
            if (Py.Wahr(soll["toleranz_min"]))
            {
                Dazu(Prozent("übersteht %d Plattenausfall%s", soll["toleranz_min"], Py.Int(soll["toleranz_min"]) == 1 ? "" : "e"),
                    raid is not null && !Py.Kleiner(raid["toleranz"], soll["toleranz_min"]));
            }
        }
        var arten = FitArtenVon(vorlage);
        if (arten.Contains("ram") && !Py.Enthaelt(fehlt, "ram"))
        {
            Dazu(Prozent("höchstens %d RAM-Bänke", vorlage["ram_baenke"]), !Py.Kleiner(vorlage["ram_baenke"], Get(werte, "ram_anzahl", 0L)));
            if (Py.Wahr(soll["ram_min"]))
            {
                Dazu(Prozent("mind. %d GB RAM (jetzt %d GB)", soll["ram_min"], werte["ram"]), !Py.Kleiner(werte["ram"], soll["ram_min"]));
            }
        }
        if (arten.Contains("cpu") && !Py.Enthaelt(fehlt, "cpu"))
        {
            Dazu(Prozent("höchstens %d Prozessoren (Sockel)", vorlage["sockel"]), !Py.Kleiner(vorlage["sockel"], Get(werte, "cpu_anzahl", 0L)));
            if (Py.Wahr(soll["kerne_min"]))
            {
                Dazu(Prozent("mind. %d Kerne (jetzt %d)", soll["kerne_min"], werte["kerne"]), !Py.Kleiner(werte["kerne"], soll["kerne_min"]));
            }
        }
        if (Py.Wahr(soll["gbit_min"]) && !Py.Enthaelt(fehlt, "nic"))
        {
            Dazu(Prozent("Netzwerk mind. %d GbE", soll["gbit_min"]), !Py.Kleiner(werte["gbit"], soll["gbit_min"]));
        }
        if (werte["netzteil"] is PyObjekt netzteil && Py.Wahr(netzteil))
        {
            var plaetze = Get(vorlage, "netzteil_plaetze", 1L);
            Dazu(Prozent("höchstens %d Netzteile", plaetze), !Py.Kleiner(plaetze, werte["netzteile"]));
            if (Py.Wahr(soll["redundant"]))
            {
                Dazu("Netzteile redundant (2 Stück)", Py.Int(werte["netzteile"]) >= 2);
            }
            var noetig = FitNetzteilNoetig(werte["watt"], inhalt);
            Dazu(Prozent("ein Netzteil trägt den Server allein (%d W nötig)", noetig), !Py.Kleiner(netzteil["leistung"], noetig));
        }
        return pruef;
    }

    /// <summary>fit_values: alles fuer die Anzeige der Bestueckung.</summary>
    public static PyObjekt FitWerte(PyObjekt aufgabe, PyObjekt? antwort, PyObjekt inhalt)
    {
        var angabe = OderLeer(aufgabe["bestueckung"]);
        var server = new List<object?>();
        object kosten = 0L, watt = 0L;
        foreach (var vorlage in FitVorlagen(aufgabe))
        {
            var werte = FitServer(vorlage, (antwort ?? [])[Py.Str(vorlage["id"])] as PyObjekt, inhalt);
            var anzahl = Py.Int(Get(vorlage, "anzahl", 1L));
            server.Add(new PyObjekt
            {
                { "vorlage", vorlage }, { "werte", werte },
                { "checks", FitServerPruefungen(vorlage, werte, inhalt).Select(p => (object?)new PyObjekt { { "text", p.Text }, { "ok", p.Ok } }).ToList() },
                { "kosten", Py.Mal(werte["kosten"], anzahl) }, { "watt", Py.Mal(werte["watt"], anzahl) },
            });
            kosten = Py.Plus(kosten, Py.Mal(werte["kosten"], anzahl));
            watt = Py.Plus(watt, Py.Mal(werte["watt"], anzahl));
        }
        var pruef = new List<object?>();
        var budget = Py.Int(Get(angabe, "budget", 0L));
        if (budget != 0)
        {
            pruef.Add(new PyObjekt { { "text", Prozent("Hardware im Budget von %s (jetzt %s)", Euro(budget), Euro(kosten)) }, { "ok", !Py.Kleiner(budget, kosten) } });
        }
        if (FarmRackAufgabe(angabe["rack"], inhalt) is PyObjekt rack)
        {
            var geraete = Liste(rack["geraete"]).Select(g => RackGeraet(g, inhalt)!).ToList();
            var usv = Py.Summe(geraete.Where(g => Py.Gleich(g["typ"], "usv")).Select(g => Get(g, "leistung", 0L)));
            var reserve = Objekt(Objekt(inhalt["hardware"])["rack_regeln"])["strom_reserve"];
            var kapazitaet = Py.Int(Py.Mal(usv, 1 - Py.F(reserve)));
            if (kapazitaet != 0)
            {
                pruef.Add(new PyObjekt { { "text", Prozent("USV im Rack schafft %s W (jetzt %s W)", Num(kapazitaet), Num(watt)) }, { "ok", !Py.Kleiner(kapazitaet, watt) } });
            }
            var schrank = Objekt(rack["schrank"]);
            if (Py.Wahr(schrank["kuehlung"]))
            {
                pruef.Add(new PyObjekt
                {
                    { "text", Prozent("Kühlung schafft %s W (jetzt %s W)", Num(schrank["kuehlung"]), Num(watt)) }, { "ok", !Py.Kleiner(schrank["kuehlung"], watt) },
                });
            }
        }
        return new PyObjekt { { "server", server }, { "kosten", kosten }, { "budget", budget }, { "watt", watt }, { "checks", pruef } };
    }

    /// <summary>fit_problems: Probleme der Bestueckung als Saetze (leer = richtig).</summary>
    public static List<string> FitProbleme(PyObjekt aufgabe, PyObjekt? antwort, PyObjekt inhalt)
    {
        var werte = FitWerte(aufgabe, antwort, inhalt);
        var probleme = new List<string>();
        foreach (var server in Liste(werte["server"]).Select(Objekt))
        {
            var name = Objekt(server["vorlage"])["name"];
            foreach (var p in Liste(server["checks"]).Select(Objekt).Where(p => !Py.Wahr(p["ok"])))
            {
                var text = Py.Str(p["text"]);
                probleme.Add(text.EndsWith("ausgewählt", StringComparison.Ordinal) || text == "RAID-Level gewählt"
                    ? Prozent("%s: %s fehlt.", name, text) : Prozent("%s: nicht erfüllt: %s.", name, text));
            }
        }
        foreach (var p in Liste(werte["checks"]).Select(Objekt).Where(p => !Py.Wahr(p["ok"])))
        {
            probleme.Add(Prozent("Nicht erfüllt: %s.", p["text"]));
        }
        return probleme;
    }

    /// <summary>(preis, watt) &lt; bester wie der Tupel-Vergleich in Python.</summary>
    private static bool Billiger((object Preis, object Watt) a, (object Preis, object Watt) b) =>
        Py.Kleiner(a.Preis, b.Preis) || (Py.Gleich(a.Preis, b.Preis) && Py.Kleiner(a.Watt, b.Watt));

    /// <summary>_fit_cheapest: guenstigste gueltige Bestueckung einer Vorlage.</summary>
    private static PyObjekt? FitGuenstigste(PyObjekt vorlage, PyObjekt inhalt)
    {
        var soll = OderLeer(vorlage["soll"]);
        var auswahl = new PyObjekt();
        ((object, object) Schluessel, PyObjekt Wahl)? bester = null;
        foreach (var platte in FitTeile("laufwerk", inhalt))
        {
            if (Py.Wahr(soll["art"]) && !Py.Gleich(platte["art"], soll["art"]))
            {
                continue;
            }
            foreach (var anzahl in FitAnzahlen(vorlage, "laufwerk"))
            {
                foreach (var stufe in (Py.Wahr(soll["raid"]) ? Liste(soll["raid"]).Select(Py.Str) : RaidStufen))
                {
                    var raid = RaidObjekt(stufe, anzahl, platte["groesse"]);
                    if (raid is null || Py.Kleiner(raid["netto"], Get(soll, "netto_min", 0L)) || Py.Kleiner(raid["toleranz"], Get(soll, "toleranz_min", 0L)))
                    {
                        continue;
                    }
                    var schluessel = (Py.Mal(platte["preis"], anzahl), Py.Mal(Get(platte, "watt", 0L), anzahl));
                    if (bester is null || Billiger(schluessel, bester.Value.Schluessel))
                    {
                        bester = (schluessel, new PyObjekt { { "laufwerk", platte["id"] }, { "laufwerke", anzahl }, { "raid", stufe } });
                    }
                }
            }
        }
        if (bester is null)
        {
            return null;
        }
        foreach (var p in bester.Value.Wahl)
        {
            Py.Setzen(auswahl, p.Key, p.Value);
        }
        foreach (var (art, noetig, feld) in new[] { ("ram", "ram_min", "groesse"), ("cpu", "kerne_min", "kerne") })
        {
            if (!FitArtenVon(vorlage).Contains(art))
            {
                continue;
            }
            ((object, object) Schluessel, object? Id, long Anzahl)? best = null;
            foreach (var teil in FitTeile(art, inhalt))
            {
                foreach (var anzahl in FitAnzahlen(vorlage, art))
                {
                    if (!Py.Kleiner(Py.Mal(teil[feld], anzahl), Get(soll, noetig, 0L)))
                    {
                        var schluessel = (Py.Mal(teil["preis"], anzahl), Py.Mal(Get(teil, "watt", 0L), anzahl));
                        if (best is null || Billiger(schluessel, best.Value.Schluessel))
                        {
                            best = (schluessel, teil["id"], anzahl);
                        }
                        break;
                    }
                }
            }
            if (best is null)
            {
                return null;
            }
            Py.Setzen(auswahl, art, best.Value.Id);
            Py.Setzen(auswahl, FitAnzahlSchluessel[art], best.Value.Anzahl);
        }
        var karten = FitTeile("nic", inhalt).Where(t => !Py.Kleiner(t["gbit"], Get(soll, "gbit_min", 0L))).ToList();
        if (karten.Count == 0)
        {
            return null;
        }
        Py.Setzen(auswahl, "nic", karten[0]["id"]);
        Py.Setzen(auswahl, "netzteile", Py.Wahr(soll["redundant"]) ? 2L : 1L);
        foreach (var netzteil in FitTeile("netzteil", inhalt))
        {
            Py.Setzen(auswahl, "netzteil", netzteil["id"]);
            var werte = FitServer(vorlage, auswahl, inhalt);
            if (!Py.Kleiner(netzteil["leistung"], FitNetzteilNoetig(werte["watt"], inhalt)))
            {
                return auswahl;
            }
        }
        return null;
    }

    /// <summary>fit_solution: eine richtige (guenstige) Bestueckung oder null.</summary>
    public static PyObjekt? FitLoesung(PyObjekt aufgabe, PyObjekt inhalt)
    {
        var antwort = new PyObjekt();
        foreach (var vorlage in FitVorlagen(aufgabe))
        {
            if (FitGuenstigste(vorlage, inhalt) is not PyObjekt wahl)
            {
                return null;
            }
            Py.Setzen(antwort, Py.Str(vorlage["id"]), wahl);
        }
        return FitProbleme(aufgabe, antwort, inhalt).Count == 0 ? antwort : null;
    }

    /// <summary>fit_lines: Bestueckung als lesbare Zeilen (fuer "So waere es gegangen").</summary>
    public static List<string> FitZeilen(PyObjekt aufgabe, PyObjekt? antwort, PyObjekt inhalt)
    {
        var zeilen = new List<string>();
        foreach (var server in Liste(FitWerte(aufgabe, antwort, inhalt)["server"]).Select(Objekt))
        {
            var werte = Objekt(server["werte"]);
            var vorlage = Objekt(server["vorlage"]);
            var teile = new List<string>();
            if (werte["laufwerk"] is PyObjekt platte && Py.Wahr(platte))
            {
                var raid = werte["raid_werte"] as PyObjekt;
                teile.Add(Prozent("%d × %s als %s (%s nutzbar)", werte["laufwerke"], FitTeilText(platte).Split(" · ")[0], werte["raid"],
                    raid is not null ? Tb(raid["netto"]) : "?"));
            }
            if (Py.Wahr(werte["ram"]))
            {
                teile.Add(Prozent("%d GB RAM", werte["ram"]));
            }
            if (Py.Wahr(werte["kerne"]))
            {
                teile.Add(Prozent("%d Kerne", werte["kerne"]));
            }
            if (Py.Wahr(werte["gbit"]))
            {
                teile.Add(Prozent("%d GbE", werte["gbit"]));
            }
            if (werte["netzteil"] is PyObjekt n && Py.Wahr(n))
            {
                teile.Add(Prozent("%d × %d-W-Netzteil", werte["netzteile"], n["leistung"]));
            }
            zeilen.Add(Prozent("%d × %s: %s", Get(vorlage, "anzahl", 1L), vorlage["name"], string.Join(", ", teile)));
        }
        return zeilen;
    }

    /// <summary>fit_view: Anzeige der Bestueckung fuer PC und Handy.</summary>
    public static PyObjekt FitAnsicht(PyObjekt aufgabe, PyObjekt? antwort, object? aktuell, PyObjekt inhalt)
    {
        var werte = FitWerte(aufgabe, antwort, inhalt);
        var alle = Liste(werte["server"]).Select(Objekt).ToList();
        var server = alle.First(s => Py.Gleich(Objekt(s["vorlage"])["id"], aktuell));
        var w = Objekt(server["werte"]);
        string raidText;
        bool? raidOk;
        if (w["raid_werte"] is PyObjekt raid)
        {
            var toleranz = Py.Int(raid["toleranz"]);
            raidText = Prozent("%s: %s nutzbar von %s · Effizienz %s %% · %s", w["raid"], Tb(raid["netto"]), Tb(raid["brutto"]),
                Num(PythonRundenStellen.Runde(Py.F(raid["effizienz"]), 1)),
                toleranz == 0 ? "übersteht keinen Plattenausfall" : toleranz == 1 ? "übersteht 1 Plattenausfall" : Prozent("übersteht %d Plattenausfälle", toleranz));
            raidOk = true;
        }
        else if (Py.Wahr(w["raid"]) && Py.Wahr(w["laufwerk"]))
        {
            var problem = Raid.EingabeProblem(Py.Str(w["raid"]), (int)Py.Int(w["laufwerke"]));
            raidText = problem.Length > 0 ? problem : Prozent("%s geht mit %s nicht.", w["raid"], Plural(w["laufwerke"], "Laufwerk", "Laufwerken"));
            raidOk = false;
        }
        else
        {
            (raidText, raidOk) = ("Laufwerke und RAID-Level wählen.", null);
        }
        var vorlage = Objekt(server["vorlage"]);
        var pruef = Liste(server["checks"]).ToList();
        foreach (var anderer in alle.Where(s => !ReferenceEquals(s, server)))
        {
            var offen = Liste(anderer["checks"]).Select(Objekt).Count(p => !Py.Wahr(p["ok"]));
            pruef.Add(new PyObjekt
            {
                { "text", Prozent("%s: %s", Objekt(anderer["vorlage"])["name"], offen == 0 ? "alle Vorgaben erfüllt"
                    : offen == 1 ? "1 Vorgabe offen" : Prozent("%d Vorgaben offen", (long)offen)) },
                { "ok", offen == 0 },
            });
        }
        pruef.AddRange(Liste(werte["checks"]));
        return new PyObjekt
        {
            { "raid_text", raidText }, { "raid_ok", raidOk },
            { "server_text", Prozent("Je Server: %s W, %s · %d Stück zusammen %s W, %s", Num(w["watt"]), Euro(w["kosten"]),
                Get(vorlage, "anzahl", 1L), Num(server["watt"]), Euro(server["kosten"])) },
            { "checks", pruef }, { "werte", w },
        };
    }
}
