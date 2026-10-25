using Lernplattform.Core.Daten;
using Lernplattform.Core.Rechner;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Wissensstand fuer die Firma: <see cref="Stufen"/> = firm_levels (Fachbereiche und Themen zusammen),
/// <see cref="Themen"/> = topic_knowledge, <see cref="Projekte"/> = learned_projects (im Lernbereich
/// bearbeitete Testprojekte). <see cref="Personal"/> ist personal_events (Tests koennen es abschalten
/// wie das Szenario des Fingerabdrucks).
/// </summary>
public sealed record FirmenWissen(PyObjekt Stufen, PyObjekt Themen, PySet Projekte)
{
    public Func<Spielstand, long, PyObjekt, List<object?>> Personal { get; init; } = (stand, tag, inhalt) => Betrieb.PersonalEreignisse(stand, tag, inhalt);
}

/// <summary>
/// Phase 7, W3: die Aktionen der eigenen Firma aus der Klasse <c>Game</c> (Python 0.63.2, Z. 5554-6006),
/// als reine Funktionen wie in W2: Pruefung und die Daten des einen Ereignisses, das Python schreibt.
/// </summary>
public static partial class SpielAktionen
{
    private static void FirmaNoetig(Spielstand stand)
    {
        if (!Py.Wahr(stand.Firma))
        {
            throw new SpielFehler("Dafür brauchst du zuerst eine eigene Firma.");
        }
    }

    /// <summary>" ".join(name.split())[:FIRM_NAME_MAX]</summary>
    private static string FirmenName(string? name) => Codepunkte(string.Join(" ", PythonZeichen.Split(name ?? "")), Betrieb.FirmenNameMax);

    private static string GeldFehlt(object? preis, long geld) =>
        Prozent("Dafür reicht dein Geld noch nicht (%s fehlen).", Wohnung.GanzeEuro(Py.Minus(preis, Math.Max(0, geld))));

    /// <summary>found_firm: gruendet die eigene Firma.</summary>
    public static List<(string Art, PyObjekt Daten)> Gruenden(Spielstand stand, string? name)
    {
        var kurz = FirmenName(name);
        if (kurz.Length == 0)
        {
            throw new SpielFehler("Bitte gib deiner Firma einen Namen.");
        }
        if (Py.Wahr(stand.Firma))
        {
            throw new SpielFehler("Du hast schon eine Firma gegründet.");
        }
        var fehlt = stand.GruendungFehlt();
        if (fehlt.Count > 0)
        {
            throw new SpielFehler(Prozent("Für die Gründung fehlt noch: %s.", string.Join(", ", fehlt)));
        }
        var kosten = Py.Int(Objekt(Objekt(stand.Inhalt["firma"])["gruendung"])["kosten"]);
        return Eins(Ereignisse.Gegruendet, new PyObjekt { { "tag", stand.Tag }, { "name", kurz }, { "geld", -kosten } });
    }

    /// <summary>hire: eine vorliegende Bewerbung annehmen.</summary>
    public static List<(string Art, PyObjekt Daten)> Einstellen(Spielstand stand, object? bewerbung)
    {
        FirmaNoetig(stand);
        var person = Betrieb.Bewerbungen(stand, stand.Inhalt).FirstOrDefault(b => Py.Gleich(b["id"], bewerbung))
            ?? throw new SpielFehler("Diese Bewerbung liegt nicht (mehr) vor.");
        if (stand.Personal.Count >= stand.Kapazitaet)
        {
            throw new SpielFehler("Alle Arbeitsplätze sind besetzt. Für mehr Leute musst du das Gebäude ausbauen.");
        }
        if (stand.Geld <= 0)
        {
            throw new SpielFehler("Mit leerem Konto kannst du niemanden einstellen.");
        }
        var daten = new PyObjekt();
        foreach (var k in new[] { "id", "name", "aussehen", "werte", "themen", "gehalt", "herkunft", "schwerpunkt", "macke", "rolle" })
        {
            daten.Add(k, Py.Hat(person, k) ? person[k] : throw new KeyNotFoundException(k));
        }
        if (Py.Wahr(person["macke_id"]))
        {
            daten.Add("macke_id", person["macke_id"]);
        }
        if (Py.Hat(person, "vorher"))
        {
            daten.Add("vorher", person["vorher"]);
        }
        daten.Add("tag", stand.Tag);
        // Ab 0.45: Ist der Gewerbehof voll, faengt die Person in der Filiale an
        if (stand.OrtFrei(OrtBuero) <= 0 && stand.OrtFrei(OrtFiliale) > 0)
        {
            daten.Add("standort", OrtFiliale);
        }
        return Eins(Ereignisse.Eingestellt, daten);
    }

    private static void ArbeitetHier(Spielstand stand, object? person)
    {
        if (!stand.Personal.ContainsKey(person))
        {
            throw new SpielFehler("Diese Person arbeitet nicht bei dir.");
        }
    }

    /// <summary>fire</summary>
    public static List<(string Art, PyObjekt Daten)> Entlassen(Spielstand stand, object? person)
    {
        FirmaNoetig(stand);
        ArbeitetHier(stand, person);
        return Eins(Ereignisse.Gekuendigt, new PyObjekt { { "id", person }, { "tag", stand.Tag } });
    }

    /// <summary>fire_projects: die laufenden Projekte, in deren Team die Person ist.</summary>
    public static List<PyObjekt> EntlassenProjekte(Spielstand stand, object? person) =>
        [.. stand.LaufendeProjekte().Where(p => Py.Enthaelt(p["team"], person))];

    /// <summary>fire_question: Rueckfrage beim Entlassen (PC und Handy gleich).</summary>
    public static string EntlassenFrage(Spielstand stand, object? person, object? name)
    {
        var text = Prozent("%s wirklich entlassen? Die Person bewirbt sich danach nicht erneut.", name);
        foreach (var projekt in EntlassenProjekte(stand, person))
        {
            var titel = Py.Wahr(projekt["titel"]) ? projekt["titel"] : projekt["projekt"];
            text += Liste(projekt["team"]).Any(p => !Py.Gleich(p, person))
                ? Prozent(" %s arbeitet im Projekt „%s“. Das Projekt läuft ohne diese Person weiter; das Team änderst du unter „Team ändern“.", name, titel)
                : Prozent(" %s arbeitet im Projekt „%s“. Das Projekt bleibt bestehen, kommt aber ohne Team nicht voran; das Team änderst du unter „Team ändern“.", name, titel);
        }
        return text;
    }

    /// <summary>train: Weiterbildung in einem Thema oder Fachbereich.</summary>
    public static List<(string Art, PyObjekt Daten)> Weiterbilden(Spielstand stand, object? person, string ziel)
    {
        FirmaNoetig(stand);
        var angebot = Betrieb.WeiterbildungAngebot(stand, person, ziel, stand.Inhalt);
        if (Py.Wahr(angebot["problem"]))
        {
            throw new SpielFehler(Py.Str(angebot["problem"]));
        }
        var daten = new PyObjekt
        {
            { "id", person }, { "cat", angebot["cat"] }, { "art", angebot["art"] }, { "geld", Py.Minus(0L, angebot["preis"]) },
            { "tag", stand.Tag }, { "bis_tag", Py.Plus(stand.Tag, angebot["tage"]) }, { "plus", angebot["plus"] },
        };
        if (Py.Wahr(angebot["thema"]))
        {
            daten.Add("thema", angebot["thema"]);
        }
        return Eins(Ereignisse.Weiterbildung, daten);
    }

    /// <summary>coach: Coaching gegen die Macke.</summary>
    public static List<(string Art, PyObjekt Daten)> Coachen(Spielstand stand, object? person)
    {
        FirmaNoetig(stand);
        var angebot = Betrieb.CoachingAngebot(stand, person, stand.Inhalt);
        if (Py.Wahr(angebot["problem"]))
        {
            throw new SpielFehler(Py.Str(angebot["problem"]));
        }
        return Eins(Ereignisse.Weiterbildung, new PyObjekt
        {
            { "id", person }, { "art", Betrieb.Coaching }, { "cat", null }, { "geld", Py.Minus(0L, angebot["preis"]) },
            { "tag", stand.Tag }, { "bis_tag", Py.Plus(stand.Tag, angebot["tage"]) }, { "plus", 0L }, { "macke", angebot["macke"] },
        });
    }

    /// <summary>decide: Entscheidung zum Personal (Urlaub, Rueckhol-Angebot, Konflikt).</summary>
    public static List<(string Art, PyObjekt Daten)> Entscheiden(Spielstand stand, object? entscheidung, object? wahl)
    {
        FirmaNoetig(stand);
        var eintrag = stand.OffeneEntscheidungen().FirstOrDefault(e => Py.Gleich(e["id"], entscheidung))
            ?? throw new SpielFehler("Diese Entscheidung steht nicht (mehr) an.");
        var option = Liste(eintrag["optionen"]).Select(Objekt).FirstOrDefault(o => Py.Gleich(o["id"], wahl))
            ?? throw new SpielFehler("Diese Möglichkeit gibt es hier nicht.");
        if (Py.Wahr(option["problem"]))
        {
            throw new SpielFehler(Py.Str(option["problem"]));
        }
        if (Py.Gleich(eintrag["art"], Betrieb.EntscheidungUrlaub))
        {
            return Eins(Py.Gleich(wahl, Betrieb.UrlaubJa) ? Ereignisse.UrlaubJa : Ereignisse.UrlaubNein, new PyObjekt
            {
                { "anfrage", eintrag["id"] }, { "id", eintrag["person"] }, { "name", eintrag["name"] },
                { "von", eintrag["von"] }, { "bis", eintrag["bis"] }, { "tag", stand.Tag },
            });
        }
        if (Py.Gleich(eintrag["art"], Betrieb.EntscheidungRueckhol))
        {
            var daten = new PyObjekt
            {
                { "rueckhol", eintrag["id"] }, { "id", eintrag["person"] }, { "name", eintrag["name"] },
                { "firma", eintrag["firma"] }, { "wahl", wahl }, { "tag", stand.Tag },
            };
            if (Py.Gleich(wahl, Betrieb.RueckholGehalt))
            {
                daten.Add("gehalt", eintrag["gehalt_neu"]);
            }
            else if (Py.Gleich(wahl, Betrieb.RueckholBonus))
            {
                daten.Add("geld", -Py.Int(eintrag["bonus"]));
            }
            return Eins(Ereignisse.Rueckhol, daten);
        }
        return Eins(Ereignisse.Konflikt, new PyObjekt
        {
            { "konflikt", eintrag["id"] }, { "a", eintrag["a"] }, { "b", eintrag["b"] },
            { "name_a", eintrag["name_a"] }, { "name_b", eintrag["name_b"] }, { "wahl", wahl }, { "tag", stand.Tag },
        });
    }

    /// <summary>expand: das Gebaeude eine Stufe ausbauen.</summary>
    public static List<(string Art, PyObjekt Daten)> Ausbauen(Spielstand stand)
    {
        FirmaNoetig(stand);
        var stufe = stand.NaechsteStufe() ?? throw new SpielFehler("Das Gebäude ist fertig ausgebaut.");
        if (Py.Kleiner(stand.Geld, stufe["preis"]))
        {
            throw new SpielFehler(GeldFehlt(stufe["preis"], stand.Geld));
        }
        return Eins(Ereignisse.Erweitert, new PyObjekt { { "stufe", stufe["stufe"] }, { "geld", -Py.Int(stufe["preis"]) }, { "tag", stand.Tag } });
    }

    /// <summary>build_room: einen Sonderraum ausbauen.</summary>
    public static List<(string Art, PyObjekt Daten)> RaumBauen(Spielstand stand, object? raum)
    {
        FirmaNoetig(stand);
        var status = Betrieb.RaumStand(stand, stand.Inhalt).FirstOrDefault(r => Py.Gleich(r["id"], raum))
            ?? throw new SpielFehler("Diesen Raum gibt es nicht.");
        if (Py.Wahr(status["problem"]))
        {
            throw new SpielFehler(Py.Str(status["problem"]));
        }
        return Eins(Ereignisse.Raum, new PyObjekt { { "raum", raum }, { "geld", -Py.Int(status["preis"]) }, { "tag", stand.Tag } });
    }

    /// <summary>open_branch: die Filiale (Stufe 1) eroeffnen.</summary>
    public static List<(string Art, PyObjekt Daten)> FilialeEroeffnen(Spielstand stand, string? name)
    {
        FirmaNoetig(stand);
        var kurz = FirmenName(name);
        if (kurz.Length == 0)
        {
            throw new SpielFehler("Bitte gib deiner Filiale einen Namen.");
        }
        if (Py.Wahr(stand.Zweigstelle))
        {
            throw new SpielFehler("Die Filiale ist schon eröffnet.");
        }
        var fehlt = Betrieb.FilialeFehlt(stand, stand.Inhalt);
        if (fehlt.Count > 0)
        {
            throw new SpielFehler(Prozent("Für die Filiale fehlt noch: %s.", string.Join(", ", fehlt)));
        }
        var stufe = Betrieb.FilialStufe(1L, stand.Inhalt)!;
        if (Py.Kleiner(stand.Geld, stufe["preis"]))
        {
            throw new SpielFehler(GeldFehlt(stufe["preis"], stand.Geld));
        }
        return Eins(Ereignisse.Filiale, new PyObjekt { { "stufe", 1L }, { "name", kurz }, { "geld", -Py.Int(stufe["preis"]) }, { "tag", stand.Tag } });
    }

    /// <summary>expand_branch</summary>
    public static List<(string Art, PyObjekt Daten)> FilialeAusbauen(Spielstand stand)
    {
        FirmaNoetig(stand);
        if (!Py.Wahr(stand.Zweigstelle))
        {
            throw new SpielFehler("Eröffne zuerst die Filiale.");
        }
        var stufe = stand.NaechsteFilialStufe() ?? throw new SpielFehler("Die Filiale ist fertig ausgebaut.");
        if (Py.Kleiner(stand.Geld, stufe["preis"]))
        {
            throw new SpielFehler(GeldFehlt(stufe["preis"], stand.Geld));
        }
        return Eins(Ereignisse.Filiale, new PyObjekt { { "stufe", stufe["stufe"] }, { "geld", -Py.Int(stufe["preis"]) }, { "tag", stand.Tag } });
    }

    /// <summary>transfer: an den anderen Standort versetzen.</summary>
    public static List<(string Art, PyObjekt Daten)> Versetzen(Spielstand stand, object? person, object? ort)
    {
        FirmaNoetig(stand);
        ArbeitetHier(stand, person);
        if (!Py.Gleich(ort, OrtBuero) && !Py.Gleich(ort, OrtFiliale))
        {
            throw new SpielFehler("Unbekannter Standort.");
        }
        if (Py.Gleich(ort, OrtFiliale) && !Py.Wahr(stand.Zweigstelle))
        {
            throw new SpielFehler("Eröffne zuerst die Filiale.");
        }
        if (Py.Gleich(stand.OrtVon(person), ort))
        {
            throw new SpielFehler("Die Person arbeitet schon dort.");
        }
        if (stand.OrtFrei(Py.Str(ort)) <= 0)
        {
            throw new SpielFehler("Dort ist kein Arbeitsplatz mehr frei.");
        }
        return Eins(Ereignisse.Versetzt, new PyObjekt
        {
            { "id", person }, { "name", Get(Objekt(stand.Personal.Get(person)), "name", "") }, { "standort", ort }, { "tag", stand.Tag },
        });
    }

    private static void ZuschlagPruefen(object? zuschlag, IEnumerable<object?> erlaubt)
    {
        if (!erlaubt.Any(z => Py.Gleich(z, zuschlag)))
        {
            throw new SpielFehler("Bitte wähle einen Gewinnzuschlag.");
        }
    }

    /// <summary>send_offer: Angebot zu einer Kundenanfrage; das Ergebnis steht sofort fest.</summary>
    public static List<(string Art, PyObjekt Daten)> Anbieten(Spielstand stand, object? anfrage, object? zuschlag, PyObjekt? antwort)
    {
        FirmaNoetig(stand);
        var eintrag = stand.Anfragen().FirstOrDefault(a => Py.Gleich(a["id"], anfrage));
        if (eintrag is null || Py.Wahr(eintrag["ergebnis"]))
        {
            throw new SpielFehler("Diese Anfrage liegt heute nicht (mehr) vor.");
        }
        ZuschlagPruefen(zuschlag, Liste(Betrieb.AngebotRegeln(stand.Inhalt)["zuschlaege"]));
        var daten = Betrieb.AngebotErgebnis(stand, eintrag, zuschlag, antwort, stand.Inhalt);
        return Eins(Py.Wahr(daten["gewonnen"]) ? Ereignisse.AngebotGewonnen : Ereignisse.AngebotVerloren, daten);
    }

    /// <summary>delegate: ein Kundenticket an eine Person oder an sich selbst (SELF) geben.</summary>
    public static List<(string Art, PyObjekt Daten)> Delegieren(Spielstand stand, object? ticket, object? person, PyObjekt stufen)
    {
        FirmaNoetig(stand);
        var eintrag = stand.KundenTickets().FirstOrDefault(t => Py.Gleich(t["id"], ticket))
            ?? throw new SpielFehler("Dieses Kundenticket liegt heute nicht (mehr) vor.");
        if (Py.Wahr(eintrag["an"]))
        {
            throw new SpielFehler("Dieses Kundenticket ist schon verteilt.");
        }
        var option = Betrieb.TicketKandidaten(stand, eintrag, stufen, stand.Inhalt).FirstOrDefault(o => Py.Gleich(o["an"], person))
            ?? throw new SpielFehler("Diese Person arbeitet nicht bei dir.");
        if (Py.Wahr(option["problem"]))
        {
            throw new SpielFehler(Py.Str(option["problem"]));
        }
        return Eins(Ereignisse.Delegiert, new PyObjekt
        {
            { "ticket", ticket }, { "vorlage", eintrag["vorlage"] }, { "tag", stand.Tag },
            { "an", person }, { "name", option["name"] }, { "chance", option["chance"] },
        });
    }

    /// <summary>take_loan: festes Paket oder freie Summe und Laufzeit.</summary>
    public static List<(string Art, PyObjekt Daten)> KreditAufnehmen(Spielstand stand, object? summe, object? laufzeit, object? paket = null)
    {
        FirmaNoetig(stand);
        PyObjekt? gewaehlt = null;
        if (Py.Wahr(paket) && !Py.Gleich(paket, Wirtschaft.FreierKredit))
        {
            gewaehlt = Liste(Wirtschaft.KreditRegeln(stand.Inhalt)["pakete"]).Select(Objekt).FirstOrDefault(p => Py.Gleich(p["id"], paket))
                ?? throw new SpielFehler("Dieses Kreditpaket gibt es nicht.");
            (summe, laufzeit) = (gewaehlt["summe"], gewaehlt["laufzeit"]);
        }
        var angebot = Wirtschaft.KreditAngebot(stand, summe, laufzeit, gewaehlt, stand.Inhalt);
        if (Py.Wahr(angebot["problem"]))
        {
            throw new SpielFehler(Py.Str(angebot["problem"]));
        }
        return Eins(Ereignisse.Kredit, new PyObjekt
        {
            { "kredit", Prozent("%s%d:%d", Wirtschaft.KreditPrefix, stand.Tag, stand.Kredite.Count + 1) },
            { "tag", stand.Tag }, { "name", angebot["name"] }, { "paket", angebot["paket"] },
            { "summe", angebot["summe"] }, { "laufzeit", angebot["laufzeit"] }, { "zins", angebot["zins"] }, { "rate", angebot["rate"] },
        });
    }

    /// <summary>repay_loan: einen laufenden Kredit vorzeitig abloesen.</summary>
    public static List<(string Art, PyObjekt Daten)> KreditAbloesen(Spielstand stand, object? kredit)
    {
        FirmaNoetig(stand);
        if (stand.Kredite.Get(kredit) is not PyObjekt eintrag || eintrag["ende"] is not null)
        {
            throw new SpielFehler("Dieser Kredit läuft nicht (mehr).");
        }
        var abloese = Wirtschaft.KreditAbloese(eintrag, stand.Inhalt);
        if (Py.Kleiner(stand.Geld, abloese["gesamt"]))
        {
            throw new SpielFehler(Prozent("Zum Ablösen fehlen dir noch %s.", Wohnung.GanzeEuro(Py.Minus(abloese["gesamt"], Math.Max(0, stand.Geld)))));
        }
        return Eins(Ereignisse.KreditAbgeloest, new PyObjekt { { "kredit", kredit }, { "tag", stand.Tag }, { "geld", Py.Minus(0L, abloese["gesamt"]) } });
    }

    /// <summary>book_ad: Werbung laufend (Kosten je Arbeitstag) oder einmalig (sofort bezahlt).</summary>
    public static List<(string Art, PyObjekt Daten)> WerbungBuchen(Spielstand stand, object? werbung, object? art)
    {
        FirmaNoetig(stand);
        var angebot = Wirtschaft.WerbeAngebot(stand, werbung, art, stand.Inhalt);
        if (Py.Wahr(angebot["problem"]))
        {
            throw new SpielFehler(Py.Str(angebot["problem"]));
        }
        var tag = stand.Tag;
        var daten = new PyObjekt { { "werbung", werbung }, { "art", art }, { "tag", tag }, { "ab_tag", tag + 1 } };
        if (Py.Gleich(art, Wirtschaft.WerbungEinmalig))
        {
            daten.Add("bis_tag", Py.Plus(tag, angebot["tage"]));
            daten.Add("geld", Py.Minus(0L, angebot["preis"]));
        }
        else
        {
            daten.Add("kosten", angebot["kosten"]);
        }
        return Eins(Ereignisse.Werbung, daten);
    }

    /// <summary>stop_ad: laufende Werbung kuendigen (heute kostet sie noch).</summary>
    public static List<(string Art, PyObjekt Daten)> WerbungBeenden(Spielstand stand, object? werbung)
    {
        FirmaNoetig(stand);
        var buchung = stand.WerbeBuchung(werbung, stand.Tag);
        if (buchung is null || !Py.Gleich(buchung["art"], Wirtschaft.WerbungLaufend))
        {
            throw new SpielFehler("Diese Werbung läuft nicht dauerhaft.");
        }
        return Eins(Ereignisse.WerbungEnde, new PyObjekt { { "werbung", werbung }, { "tag", stand.Tag } });
    }

    /// <summary>start_cert: Zertifizierung beginnen (kostet sofort, gilt nach den Arbeitstagen).</summary>
    public static List<(string Art, PyObjekt Daten)> ZertifizierungStarten(Spielstand stand, object? zert)
    {
        FirmaNoetig(stand);
        var angebot = Wirtschaft.ZertAngebot(stand, zert, stand.Inhalt);
        if (Py.Wahr(angebot["problem"]))
        {
            throw new SpielFehler(Py.Str(angebot["problem"]));
        }
        return Eins(Ereignisse.Zertifizierung, new PyObjekt
        {
            { "zert", zert }, { "tag", stand.Tag }, { "bis_tag", Py.Plus(stand.Tag, angebot["tage"]) }, { "geld", Py.Minus(0L, angebot["preis"]) },
        });
    }

    /// <summary>farm_start: Ausschreibung der Serverfarm oeffnen.</summary>
    public static List<(string Art, PyObjekt Daten)> FarmStarten(Spielstand stand)
    {
        FirmaNoetig(stand);
        if (stand.FarmFrei is null)
        {
            throw new SpielFehler(Betrieb.FarmFreiHinweis(stand, stand.Inhalt));
        }
        if (stand.Farm is not null)
        {
            throw new SpielFehler("Die Serverfarm läuft schon.");
        }
        return Eins(Ereignisse.FarmStart, new PyObjekt { { "tag", stand.Tag } });
    }

    /// <summary>farm_solve: eine Serverfarm-Aufgabe auswerten (beim Angebot mit Gewinnzuschlag).</summary>
    public static List<(string Art, PyObjekt Daten)> FarmLoesen(Spielstand stand, object? aufgabe, object? antwort, bool hilfe, PyObjekt themen, object? zuschlag = null)
    {
        FirmaNoetig(stand);
        var auftrag = Betrieb.FarmAufgabe(aufgabe, stand.Inhalt);
        var problem = auftrag is not null ? Betrieb.FarmAufgabeProblem(stand, aufgabe, stand.Inhalt) : "Diese Aufgabe gibt es nicht.";
        if (problem.Length > 0)
        {
            throw new SpielFehler(problem);
        }
        if (Py.Gleich(auftrag!["typ"], Betrieb.FarmAngebotTyp))
        {
            ZuschlagPruefen(zuschlag, Betrieb.FarmZuschlaege(stand.Inhalt));
        }
        return Eins(Ereignisse.FarmAufgabe, Betrieb.FarmBewerten(stand, auftrag, antwort, hilfe, themen, zuschlag, stand.Inhalt));
    }

    private static List<object?> OhneDoppelte(Spielstand stand, IEnumerable<object?>? team)
    {
        var liste = (team ?? []).ToList();
        var ergebnis = liste.Where((p, i) => !liste.Take(i).Any(q => Py.Gleich(p, q))).ToList();
        // Ab 0.62.4 (B-F-01): Wer nicht mehr hier arbeitet, faellt still heraus
        return [.. ergebnis.Where(p => !stand.Ausgeschieden(p))];
    }

    /// <summary>farm_set_team: das Serverfarm-Team neu zusammenstellen.</summary>
    public static List<(string Art, PyObjekt Daten)> FarmTeamSetzen(Spielstand stand, IEnumerable<object?>? team, PyObjekt themen)
    {
        FirmaNoetig(stand);
        if (!stand.FarmLaeuft)
        {
            throw new SpielFehler("Die Serverfarm läuft nicht (mehr).");
        }
        var neu = OhneDoppelte(stand, team);
        var optionen = new PyDict();
        foreach (var o in Betrieb.FarmKandidaten(stand, themen, stand.Inhalt))
        {
            optionen[o["an"]] = o;
        }
        var jetzt = Liste(Objekt(stand.Farm)["team"]);
        var grenze = Py.Int(Get(Betrieb.FarmRegeln(stand.Inhalt), "team_max", 5L));
        if (neu.Count > grenze)
        {
            throw new SpielFehler(Prozent("Ins Serverfarm-Team passen höchstens %d Leute.", grenze));
        }
        foreach (var person in neu)
        {
            if (optionen.Get(person) is not PyObjekt option)
            {
                throw new SpielFehler("Diese Person arbeitet nicht bei dir.");
            }
            if (!Py.Enthaelt(jetzt, person) && Py.Wahr(option["problem"]) && !Py.Str(option["problem"]).StartsWith("Das Team ist voll", StringComparison.Ordinal))
            {
                throw new SpielFehler(Py.Str(option["problem"]));
            }
        }
        return Eins(Ereignisse.FarmTeam, new PyObjekt { { "tag", stand.Tag }, { "team", neu } });
    }

    private static List<object?> Umschalten(List<object?> team, object? person)
    {
        var liste = team.ToList();
        var stelle = liste.FindIndex(p => Py.Gleich(p, person));
        if (stelle >= 0)
        {
            liste.RemoveAt(stelle);
        }
        else
        {
            liste.Add(person);
        }
        return liste;
    }

    /// <summary>farm_toggle_member</summary>
    public static List<(string Art, PyObjekt Daten)> FarmMitglied(Spielstand stand, object? person, PyObjekt themen)
    {
        if (!stand.FarmLaeuft)
        {
            throw new SpielFehler("Die Serverfarm läuft nicht (mehr).");
        }
        return FarmTeamSetzen(stand, Umschalten(Liste(Objekt(stand.Farm)["team"]), person), themen);
    }

    /// <summary>send_project_offer: Angebot fuer eine Projektausschreibung; das Ergebnis steht sofort fest.</summary>
    public static List<(string Art, PyObjekt Daten)> ProjektAnbieten(Spielstand stand, object? projekt, object? zuschlag, PyObjekt? antwort)
    {
        FirmaNoetig(stand);
        var eintrag = stand.Ausschreibungen().FirstOrDefault(p => Py.Gleich(p["id"], projekt));
        if (eintrag is null || Py.Wahr(eintrag["ergebnis"]))
        {
            throw new SpielFehler("Diese Ausschreibung liegt nicht (mehr) vor.");
        }
        var problem = Betrieb.ProjektAngebotProblem(stand, stand.Inhalt);
        if (problem.Length > 0)
        {
            throw new SpielFehler(problem);
        }
        ZuschlagPruefen(zuschlag, Liste(Betrieb.AngebotRegeln(stand.Inhalt)["zuschlaege"]));
        var daten = Betrieb.ProjektAngebotErgebnis(stand, eintrag, zuschlag, antwort, stand.Tag, stand.Inhalt);
        return Eins(Py.Wahr(daten["gewonnen"]) ? Ereignisse.ProjektGewonnen : Ereignisse.ProjektVerloren, daten);
    }

    /// <summary>set_project_team</summary>
    public static List<(string Art, PyObjekt Daten)> ProjektTeamSetzen(Spielstand stand, object? projekt, IEnumerable<object?>? team, PyObjekt stufen)
    {
        FirmaNoetig(stand);
        if (stand.Projekte.Get(projekt) is not PyObjekt eintrag || Py.Wahr(eintrag["fertig"]))
        {
            throw new SpielFehler("Dieses Projekt läuft nicht (mehr).");
        }
        var neu = OhneDoppelte(stand, team);
        var optionen = new PyDict();
        foreach (var o in Betrieb.ProjektKandidaten(stand, eintrag, stufen, stand.Inhalt))
        {
            optionen[o["an"]] = o;
        }
        foreach (var person in neu)
        {
            if (optionen.Get(person) is not PyObjekt option)
            {
                throw new SpielFehler("Diese Person arbeitet nicht bei dir.");
            }
            if (Py.Wahr(option["problem"]) && !Py.Enthaelt(eintrag["team"], person))
            {
                throw new SpielFehler(Py.Str(option["problem"]));
            }
        }
        return Eins(Ereignisse.ProjektTeam, new PyObjekt { { "projekt", projekt }, { "tag", stand.Tag }, { "team", neu } });
    }

    /// <summary>toggle_project_member</summary>
    public static List<(string Art, PyObjekt Daten)> ProjektMitglied(Spielstand stand, object? projekt, object? person, PyObjekt stufen)
    {
        if (stand.Projekte.Get(projekt) is not PyObjekt eintrag)
        {
            throw new SpielFehler("Dieses Projekt läuft nicht (mehr).");
        }
        return ProjektTeamSetzen(stand, projekt, Umschalten(Liste(eintrag["team"]), person), stufen);
    }
}
