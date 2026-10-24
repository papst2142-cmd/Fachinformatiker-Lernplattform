using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Auswertung eines Tickets wie <c>fisi_game.evaluate</c> (Python 0.63.2, Z. 3070-3230)
/// mit Geld, Reputation und Problemen je Aufgabentyp. Stoppgrenze (Plan Phase 7, Abschnitt 10).
/// Vergleichswerte G46 (<c>spiel_bewertung.json</c>).
/// </summary>
public static partial class Bewertung
{
    /// <summary>_scaled: int(round(wert * faktor)).</summary>
    public static long Skaliert(object? wert, object? faktor) => Py.Mal(wert, faktor) switch
    {
        long n => n,
        double d => PythonRunden.Runde(d),
        var x => throw new InvalidDataException("Zahl erwartet: " + x),
    };

    /// <summary>defer_payload: Ticket verschieben kostet Zuverlaessigkeit (Zwischenfaelle sind danach weg).</summary>
    public static PyObjekt Verschieben(PyObjekt auftrag, long tag, PyObjekt balancing)
    {
        var faktor = Get(Objekt(balancing["prioritaet_faktor"]), Py.Str(auftrag["prioritaet"]), 1L);
        var verlust = Skaliert(balancing["verschieben_verlust"], faktor);
        var daten = new PyObjekt
        {
            { "aufgabe", auftrag["id"] }, { "tag", tag }, { "reputation", new PyObjekt { { "zuverlaessigkeit", -verlust } } },
        };
        if (Py.Wahr(auftrag["zwischenfall"]))
        {
            // Zwischenfaelle warten nicht - er ist danach weg
            Py.Setzen(daten, "zwischenfall", true);
        }
        return daten;
    }

    /// <summary>_eased_balancing: balancing.json mit den Einfach-Faktoren fuer falsche Tickets.</summary>
    public static PyObjekt EinfachBalancing(Spielstand stand)
    {
        var balancing = stand.Balancing;
        if (!stand.Leicht)
        {
            return balancing;
        }
        var ergebnis = Py.Flach(balancing);
        Py.Setzen(ergebnis, "fehlerkosten", Skaliert(balancing["fehlerkosten"], stand.Erleichterung("fehlerkosten_faktor", 1.0)));
        // Faktor statt Wert, damit auch der eigene Verlust eines Auftrags gilt
        Py.Setzen(ergebnis, "verlust_faktor", stand.Erleichterung("reputation_verlust_faktor", 1.0));
        return ergebnis;
    }

    /// <summary>requirement_gaps: Themen unter der Anforderung als (thema, benoetigt, vorhanden).</summary>
    public static List<(string Thema, object? Noetig, object? Da)> Luecken(PyObjekt auftrag, PyObjekt stufen)
    {
        var luecken = new List<(string, object?, object?)>();
        foreach (var (k, noetig) in Get(auftrag, "anforderungen") as PyObjekt ?? [])
        {
            var da = Get(stufen, k, 0.0);
            if (Py.Kleiner(da, noetig))
            {
                luecken.Add((k, noetig, da));
            }
        }
        return luecken;
    }

    private static PyObjekt? AlsObjekt(object? antwort) => antwort as PyObjekt;

    /// <summary>answer_problems: Probleme einer Loesung als Saetze.</summary>
    public static List<string> AntwortProbleme(PyObjekt auftrag, object? antwort, IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt) =>
        Py.Str(auftrag["typ"]) switch
        {
            "bauteile" => BauProbleme(auftrag, AlsObjekt(antwort), verfuegbar, inhalt),
            "bestellung" => BestellProbleme(auftrag, AlsObjekt(antwort), inhalt),
            "rack" => RackProbleme(auftrag, AlsObjekt(antwort), inhalt),
            "formular" => FormProbleme(auftrag, AlsObjekt(antwort)),
            "terminal" => TerminalPruefen(auftrag, AlsObjekt(antwort), inhalt).Probleme,
            "diagnose" => DiagnosePruefen(auftrag, AlsObjekt(antwort), inhalt, verfuegbar).Probleme,
            "wartung" => WartungPruefen(auftrag, AlsObjekt(antwort)).Probleme,
            "bestueckung" => throw new NotSupportedException("Serverfarm-Aufgabe bestueckung kommt in Phase 7 W3"),
            _ => [],
        };

    private static readonly HashSet<string> ProblemTypen = ["bauteile", "bestellung", "rack", "formular", "terminal", "diagnose", "wartung", "bestueckung"];

    /// <summary>check_answer: (richtig, anzahl_fehler).</summary>
    public static (bool Richtig, long Fehler) Pruefen(PyObjekt auftrag, object? antwort, IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt)
    {
        var typ = Py.Str(auftrag["typ"]);
        if (typ == "auswahl")
        {
            var richtig = Py.Gleich(antwort, auftrag["antwort"]);
            return (richtig, richtig ? 0 : 1);
        }
        if (typ == "zuordnung")
        {
            var zuordnung = Py.Wahr(antwort) ? (PyObjekt)antwort! : [];
            long fehler = Liste(auftrag["paare"]).Select(Liste).Count(p => !Py.Gleich(Get(zuordnung, Py.Str(p[0])), p[1]));
            return (fehler == 0, fehler);
        }
        if (typ == "terminal")
        {
            // Ein Fehlgriff ist erlaubt - richtig heisst hier nicht "ohne Probleme"
            var ergebnis = TerminalPruefen(auftrag, AlsObjekt(antwort), inhalt);
            return (ergebnis.Richtig, ergebnis.Probleme.Count);
        }
        if (ProblemTypen.Contains(typ))
        {
            var probleme = AntwortProbleme(auftrag, antwort, verfuegbar, inhalt);
            return (probleme.Count == 0, probleme.Count);
        }
        throw new InvalidDataException("Unbekannter Aufgabentyp: " + typ);
    }

    /// <summary>valid_carts: alle gueltigen Warenkoerbe, guenstigster zuerst.</summary>
    public static List<PyObjekt> Warenkoerbe(PyObjekt auftrag, PyObjekt inhalt)
    {
        var angebote = Liste(auftrag["angebote"]).Select(Objekt).ToList();
        var bedarfe = Liste(auftrag["bedarf"]).Select(Objekt).ToList();
        var grenzen = new long[angebote.Count];
        for (var i = 0; i < angebote.Count; i++)
        {
            if (AngebotBedarf(angebote[i], auftrag, inhalt) is not int index)
            {
                grenzen[i] = 1;
                continue;
            }
            var bedarf = bedarfe[index];
            var meiste = Py.Int(bedarf["menge"]);
            if (Zusammen.TryGetValue(Py.Str(bedarf["typ"]), out var k) && Get(bedarf, "min") is PyObjekt min && min.Count > 0 && Py.Hat(min, k))
            {
                // Kleine Teile addieren sich: so viele, wie es fuer alle Einheiten braucht
                var groesse = Teil(angebote[i]["teil"], inhalt)![k];
                if (!Py.IstNurInt(groesse) || !Py.IstNurInt(min[k]))
                {
                    throw new NotSupportedException("valid_carts: Groesse ist keine ganze Zahl");
                }
                meiste *= Math.Max(1, -PyDiv(-Py.Int(min[k]), Math.Max(1, Py.Int(groesse))));
            }
            grenzen[i] = meiste + 1;
        }
        var ergebnis = new List<(PyObjekt Korb, (object, object) Summe)>();
        foreach (var zahlen in Produkt(grenzen))
        {
            var korb = new PyObjekt();
            for (var i = 0; i < angebote.Count; i++)
            {
                if (zahlen[i] != 0)
                {
                    Py.Setzen(korb, Py.Str(angebote[i]["id"]), zahlen[i]);
                }
            }
            if (korb.Count > 0 && BestellProbleme(auftrag, korb, inhalt).Count == 0)
            {
                ergebnis.Add((korb, KorbSumme(auftrag, korb)));
            }
        }
        return [.. ergebnis.OrderBy(e => Py.F(e.Summe.Item1)).ThenBy(e => Py.F(e.Summe.Item2)).Select(e => e.Korb)];
    }

    /// <summary>find_solution: eine richtige Loesung oder null.</summary>
    public static object? Loesung(PyObjekt auftrag, IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt)
    {
        switch (Py.Str(auftrag["typ"]))
        {
            case "auswahl":
                return auftrag["antwort"];
            case "zuordnung":
                var paare = new PyObjekt();
                foreach (var p in Liste(auftrag["paare"]).Select(Liste))
                {
                    Py.Setzen(paare, Py.Str(p[0]), p[1]);
                }
                return paare;
            case "bestellung":
                var koerbe = Warenkoerbe(auftrag, inhalt);
                return koerbe.Count > 0 ? koerbe[0] : null;
            case "rack":
                return RackLoesung(auftrag, inhalt);
            case "bestueckung":
                throw new NotSupportedException("Serverfarm-Aufgabe bestueckung kommt in Phase 7 W3");
            case "terminal":
                return TerminalLoesung(auftrag);
            case "diagnose":
                return DiagnoseLoesung(auftrag, inhalt, verfuegbar);
            case "wartung":
                return WartungLoesung(auftrag);
            case "formular":
                return FormLoesung(auftrag);
        }
        var erlaubt = verfuegbar ?? ListeOderLeerGet(auftrag, "teile");
        var plaetze = Liste(auftrag["slots"]);
        var optional = ListeOderLeer(auftrag, "optional");
        var wahl = new List<List<object?>>();
        foreach (var platz in plaetze)
        {
            var moeglich = erlaubt.Where(k => Py.Gleich(Get(Teil(k, inhalt) ?? [], "typ"), platz)).ToList();
            if (Py.Enthaelt(optional, platz))
            {
                moeglich.Insert(0, null);
            }
            wahl.Add(moeglich.Count > 0 ? moeglich : [null]);
        }
        (object Preis, PyObjekt Antwort)? beste = null;
        foreach (var zahlen in Produkt([.. wahl.Select(w => (long)w.Count)]))
        {
            var antwort = new PyObjekt();
            for (var i = 0; i < plaetze.Count; i++)
            {
                var kennung = wahl[i][(int)zahlen[i]];
                if (Py.Wahr(kennung))
                {
                    Py.Setzen(antwort, Py.Str(plaetze[i]), kennung);
                }
            }
            if (BauProbleme(auftrag, antwort, erlaubt, inhalt).Count == 0)
            {
                object preis = 0L;
                foreach (var (_, kennung) in antwort)
                {
                    preis = Py.Plus(preis, Teil(kennung, inhalt)!["preis"]);
                }
                if (beste is null || Py.Kleiner(preis, beste.Value.Preis))
                {
                    beste = (preis, antwort);
                }
            }
        }
        return beste?.Antwort;
    }

    private static List<object?> Lieferung(PyObjekt auftrag, PyObjekt antwort, long tag) =>
        Liste(auftrag["angebote"]).Select(Objekt).Where(a => Menge(antwort, a["id"]) > 0)
            .Select(a => (object?)new PyObjekt
            {
                { "teil", a["teil"] }, { "menge", Py.Int(antwort[Py.Str(a["id"])]) }, { "haendler", a["haendler"] },
                { "ankunft", Py.Plus(tag, a["lieferzeit"]) },
            }).ToList();

    /// <summary>
    /// evaluate: Nutzdaten des Ereignisses ticket_erledigt (Geld und Reputation werden mitgespeichert).
    /// verfuegbar = bereitliegende Teile, lager = Lagerbestand {bauteil: anzahl}.
    /// </summary>
    public static PyObjekt Auswerten(PyObjekt auftrag, object? antwort, bool hilfe, PyObjekt stufen, long tag, PyObjekt? balancing,
        IReadOnlyList<object?>? verfuegbar, PyObjekt inhalt, PyDict? lager)
    {
        balancing = balancing is { Count: > 0 } ? balancing : Objekt(inhalt["balancing"]);
        var typ = Py.Str(auftrag["typ"]);
        var probleme = AntwortProbleme(auftrag, antwort, verfuegbar, inhalt);
        var (richtig, fehler) = Pruefen(auftrag, antwort, verfuegbar, inhalt);
        var unter = Luecken(auftrag, stufen).Count > 0;
        var achsen = new List<string> { "fachkompetenz" };
        achsen.AddRange(ListeOderLeer(auftrag, "achsen").Select(Py.Str).Where(a => a != "fachkompetenz"));
        var belohnung = Objekt(auftrag["belohnung"]);
        var delta = new Dictionary<string, object>();
        foreach (var k in Ereignisse.Achsen)
        {
            delta[k] = 0L;
        }
        delta["zuverlaessigkeit"] = Py.Plus(delta["zuverlaessigkeit"], balancing["zuverlaessigkeit_pro_ticket"]);

        TerminalErgebnis? terminal = null;
        DiagnoseErgebnis? diagnose = null;
        object gefahr = 0L;
        var systematisch = false;
        if (typ == "terminal")
        {
            terminal = TerminalPruefen(auftrag, AlsObjekt(antwort), inhalt);
            gefahr = terminal.Gefahr;
        }
        else if (typ == "diagnose")
        {
            diagnose = DiagnosePruefen(auftrag, AlsObjekt(antwort), inhalt, verfuegbar);
            (gefahr, systematisch) = (diagnose.Gefahr, diagnose.Systematisch);
        }
        if (Py.Wahr(gefahr))
        {
            // Gefaehrlicher Befehl oder unsichere Massnahme
            delta["sicherheit"] = Py.Minus(delta["sicherheit"], balancing["gefahr_verlust"]);
        }
        if (systematisch)
        {
            delta["zuverlaessigkeit"] = Py.Plus(delta["zuverlaessigkeit"], balancing["systematik_bonus"]);
        }

        object geld;
        if (richtig)
        {
            object faktor = 1.0;
            if (!hilfe)
            {
                faktor = Py.Plus(faktor, balancing["bonus_ohne_hilfe"]);
            }
            if (!unter)
            {
                faktor = Py.Plus(faktor, balancing["bonus_sicher_geloest"]);
            }
            geld = Skaliert(belohnung["geld"], faktor);
            foreach (var a in achsen)
            {
                delta[a] = Py.Plus(delta[a], Skaliert(belohnung["reputation"], faktor));
            }
        }
        else
        {
            var faktor = unter ? balancing["faktor_unter_niveau"] : 1L;
            geld = Py.Mal(balancing["fehlerkosten"], -1L);
            var verlust = Py.Mal(Get(auftrag, "verlust", balancing["reputation_verlust"]), Get(balancing, "verlust_faktor", 1L));
            foreach (var a in achsen)
            {
                delta[a] = Py.Minus(delta[a], Skaliert(verlust, faktor));
            }
        }

        var ruf = new PyObjekt();
        foreach (var k in Ereignisse.Achsen)
        {
            if (Py.Wahr(delta[k]))
            {
                ruf.Add(k, delta[k]);
            }
        }
        var daten = new PyObjekt
        {
            { "aufgabe", auftrag["id"] }, { "tag", tag }, { "richtig", richtig }, { "fehler", fehler }, { "hilfe", hilfe },
            { "unter_niveau", unter }, { "geld", geld }, { "reputation", ruf },
        };
        if (probleme.Count > 0)
        {
            Py.Setzen(daten, "probleme", probleme.Select(p => (object?)p).ToList());
        }
        if (terminal is not null)
        {
            Py.Setzen(daten, "fehlgriffe", terminal.Fehlgriffe);
            Py.Setzen(daten, "gefahr", terminal.Gefahr);
        }
        if (diagnose is not null)
        {
            Py.Setzen(daten, "pruefungen", diagnose.Pruefungen);
            Py.Setzen(daten, "systematisch", diagnose.Systematisch);
            Py.Setzen(daten, "gefahr", diagnose.Gefahr);
            var ersatz = richtig ? ErsatzAusLager(auftrag, lager, inhalt) : null;
            if (richtig && Py.Wahr(Get(auftrag, "austausch")))
            {
                ersatz = Get((PyObjekt)antwort!, "teil");
            }
            if (Py.Wahr(ersatz))
            {
                Py.Setzen(daten, "verbaut", new List<object?> { ersatz });
                Py.Setzen(daten, "aus_lager", new List<object?> { ersatz });
            }
        }
        if (typ == "bestellung")
        {
            var korb = (PyObjekt)antwort!;
            var (summe, _) = KorbSumme(auftrag, korb);
            if (richtig)
            {
                // Die Ware kommt je Angebot nach dessen Lieferzeit an
                Py.Setzen(daten, "kosten", summe);
                var lieferung = Lieferung(auftrag, korb, tag);
                Py.Setzen(daten, "lieferung", lieferung);
                if (Py.Wahr(Get(auftrag, "empfaenger")))
                {
                    foreach (var posten in lieferung.Cast<PyObjekt>())
                    {
                        Py.Setzen(posten, "empfaenger", auftrag["empfaenger"]);
                    }
                }
                var bonus = Skaliert(Py.Max(0L, Py.Minus(auftrag["budget"], summe)), balancing["ersparnis_anteil"]);
                if (bonus != 0)
                {
                    Py.Setzen(daten, "ersparnis_bonus", bonus);
                    Py.Setzen(daten, "geld", Py.Plus(daten["geld"], bonus));
                }
            }
            else
            {
                // Falsch bestellt: Die Ware kommt trotzdem (ins Lager) - ein Zwischenfall "Falsche Ware" folgt
                var lieferung = Lieferung(auftrag, korb, tag);
                if (lieferung.Count > 0)
                {
                    Py.Setzen(daten, "kosten", summe);
                    Py.Setzen(daten, "lieferung", lieferung);
                    Py.Setzen(daten, "fehllieferung", true);
                }
            }
            if (richtig && Py.Wahr(Get(auftrag, "kein_sparbonus")) && Py.Wahr(Get(daten, "ersparnis_bonus")))
            {
                // Nach einer Falschlieferung gibt es keinen Spar-Bonus mehr
                var bonus = daten["ersparnis_bonus"];
                daten.RemoveAll(p => p.Key == "ersparnis_bonus");
                Py.Setzen(daten, "geld", Py.Minus(daten["geld"], bonus));
                Py.Setzen(daten, "bonus_verloren", true);
            }
        }
        if (Py.Wahr(Get(auftrag, "falschlieferung")))
        {
            Py.Setzen(daten, "falschlieferung", auftrag["falschlieferung"]);
            if (richtig)
            {
                // Zurueck geht, was von der falschen Ware noch im Lager liegt
                var bestand = lager?.Kopie() ?? new PyDict();
                var zurueck = new PyObjekt();
                foreach (var posten in ListeOderLeer(auftrag, "falsche_ware").Select(Liste))
                {
                    var (kennung, anzahl) = (posten[0], Py.Int(posten[1]));
                    var vorrat = bestand.Get(kennung, 0L);
                    object menge = Py.Kleiner(vorrat, anzahl) ? vorrat! : anzahl;
                    if (Py.Kleiner(0L, menge))
                    {
                        bestand[kennung] = Py.Minus(bestand[kennung], menge);
                        Py.Setzen(zurueck, Py.Str(kennung), Py.Plus(Get(zurueck, Py.Str(kennung), 0L), menge));
                    }
                }
                Py.Setzen(daten, "ruecksendung", zurueck);
                Py.Setzen(daten, "ruecksendekosten", Get(auftrag, "ruecksendekosten", 0L));
                Py.Setzen(daten, "geld", Py.Minus(daten["geld"], daten["ruecksendekosten"]));
            }
        }
        if (Py.Wahr(Get(auftrag, "zwischenfall")))
        {
            Py.Setzen(daten, "zwischenfall", true);
        }
        if (richtig && typ == "rack" && Get(auftrag, "lager_ab") is { } ab)
        {
            // Geraete ab Platz lager_ab kamen aus dem Lager
            var benutzt = RackBelegt(auftrag, AlsObjekt(antwort), inhalt).Where(r => !Py.Kleiner(r.Index, ab)).Select(r => r.Geraet["id"]).ToList();
            if (benutzt.Count > 0)
            {
                Py.Setzen(daten, "verbaut", benutzt);
                Py.Setzen(daten, "aus_lager", benutzt.ToList());
            }
        }
        if (richtig && typ == "bauteile")
        {
            var pc = (PyObjekt)antwort!;
            var bestand = lager?.Kopie() ?? new PyDict();
            var benutzt = new List<object?>();
            var teile = ListeOderLeerGet(auftrag, "teile");
            foreach (var (_, kennung) in pc)
            {
                var (grund, anzahl) = Py.Wahr(kennung) ? KombiTeilen(kennung) : (kennung, 1L);
                if (Py.Wahr(kennung) && !Py.Enthaelt(teile, kennung) && !Py.Kleiner(bestand.Get(grund, 0L), anzahl))
                {
                    bestand[grund] = Py.Minus(bestand[grund], anzahl);
                    for (var i = 0; i < anzahl; i++)
                    {
                        benutzt.Add(grund);
                    }
                }
            }
            Py.Setzen(daten, "verbaut", Liste(auftrag["slots"]).Where(p => Py.Wahr(Get(pc, Py.Str(p)))).Select(p => pc[Py.Str(p)]).ToList());
            Py.Setzen(daten, "aus_lager", benutzt);
        }
        return daten;
    }
}
