using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: die eigene Firma als Modulfunktionen von <c>fisi_game.py</c> (Python 0.63.2), Teil
/// Markt (Z. 8589-9630): Kundenanfragen und Angebote, Kundentickets, Mitbewerber, Rueckhol-Angebote,
/// Gegenwind, Konjunktur und Trends. Alles fest gewuerfelt aus Firma, Tag und Kennung.
/// </summary>
public static partial class Betrieb
{
    public const string Bitweiche = "bitweiche";             // BITWEICHE
    public const string FarmBeschaeftigtText = "%s arbeitet an der Serverfarm mit.";   // FARM_BUSY_TEXT

    /// <summary>_BITWEICHE_DEFAULT</summary>
    private static PyObjekt BitweicheStandard() => new()
    {
        { "id", Bitweiche }, { "name", "Bitweiche IT-Service GmbH" }, { "kurz", "Bitweiche" }, { "text", "" },
        { "kundenart", true }, { "gewicht", new PyObjekt { { "anfrage", 1L }, { "projekt", 1L } } },
    };

    /// <summary>_money: round(wert + 1e-9, 2).</summary>
    internal static double Geld(object? wert) => PythonRundenStellen.Runde(Py.F(wert) + 1e-9, 2);

    private static List<object?> Paar(object? wert) => Liste(wert);

    // -- Anfragen und Angebote (ab 0.34) ------------------------------------

    /// <summary>inquiries_for_day: die Kundenanfragen eines Arbeitstags.</summary>
    public static List<PyObjekt> AnfragenFuerTag(Spielstand stand, long tag, PyObjekt inhalt)
    {
        if (!Py.Wahr(stand.Firma))
        {
            return [];
        }
        var regeln = AngebotRegeln(inhalt);
        var kunden = Liste(FirmaRegeln(inhalt)["kunden"]);
        var start = stand.FirmenStartwert;
        var erster = (long)(Wurf(start, tag, "anfrage-kunde") * kunden.Count);
        var ergebnis = new List<PyObjekt>();
        var rabatt = stand.RaumEffekt("material_rabatt");
        // Ab 0.42: Werbung bringt mehr Anfragen (Menge) und groessere Bestellungen
        var werbung = Wirtschaft.WerbeWirkung(stand, tag, inhalt);
        var extra = Wirtschaft.ExtraAnzahl(werbung["anfragen"], start, tag, "werbung-anfragen");
        // Ab 0.44: Konjunktur, Trend-Anfragen und Gegenwind der Mitbewerber
        var lage = MarktVerschiebung(stand, tag, inhalt);
        var markt = Objekt(lage["markt"]);
        var gesamt = Math.Max(1, Py.Int(regeln["pro_tag"]) + Py.Int(stand.RaumEffekt("anfragen_plus")) + extra
            + Py.Int(Get(Objekt(markt["phase_info"]), "anfragen", 0L)) + FilialAnfragen(stand, inhalt));
        var trend = markt["trend"] as PyObjekt;
        var hype = Py.Wahr(trend) ? OderLeer(MarktRegeln(inhalt)["trends"]) : [];
        var mitTrend = Py.Wahr(trend) && Wurf(start, tag, "trend-anfrage") * 100 < Py.F(Get(hype, "chance", 0L));
        for (var nummer = 0L; nummer < gesamt + (mitTrend ? 1 : 0); nummer++)
        {
            var salz = Prozent("anfrage%d-", nummer);
            var istTrend = mitTrend && nummer == gesamt;
            // Verschiedene Kunden am selben Tag
            var kunde = Objekt(kunden[(int)PyMod(erster + nummer * Math.Max(1, PyDiv(kunden.Count, 2) - 1), kunden.Count)]);
            var artikel = Objekt(Waehle(Liste(istTrend ? trend!["artikel"] : regeln["artikel"]), start, tag, salz + "artikel"));
            var preisSpanne = Paar(artikel["preis"]);
            var (unten, oben) = (Py.F(preisSpanne[0]), Py.F(preisSpanne[1]));
            var preis = PythonRunden.Runde((unten + Wurf(start, tag, salz + "preis") * (oben - unten)) / 5.0) * 5;
            var mengeSpanne = Paar(artikel["menge"]);
            var menge = Zwischen(start, tag, salz + "menge", Py.Int(mengeSpanne[0]), Py.Int(mengeSpanne[1]));
            if (Py.Wahr(werbung["groesse"]))
            {
                menge = (long)Math.Ceiling(menge * (1 + Py.F(werbung["groesse"]) / 100.0));
            }
            var fach = istTrend ? trend!["cat"] : null;
            var schub = Py.Int(lage["shift"]) + (istTrend ? Py.Int(Get(hype, "zuschlag", 0L)) : 0);
            var (gebote, ausgefallen) = MitbewerberGebote("anfrage", kunde["art"], fach, start, tag, salz, inhalt,
                schub: schub, zweiter: Py.F(lage["zweiter"]));
            var billigst = Billigst(gebote);
            var lieferung = Objekt(regeln["lieferzeit"]);
            var lieferzeit = Zwischen(start, tag, salz + "lieferzeit", Py.Int(lieferung["von"]), Py.Int(lieferung["bis"]));
            var text = Py.Format(Py.Str(Waehle(Liste(regeln["texte"]), start, tag, salz + "text")), new PyObjekt
            {
                { "kontakt", kunde["kontakt"] }, { "menge", menge }, { "artikel", artikel["name"] }, { "lieferzeit", lieferzeit },
            });
            if (istTrend && Py.Wahr(trend!["satz"]))
            {
                text = Prozent("%s „%s“", text, trend["satz"]);
            }
            var kennung = Prozent("%s%d:%d", AnfragePrefix, tag, nummer + 1);
            var eintrag = new PyObjekt();
            if (istTrend)
            {
                eintrag.Add("trend", trend!["id"]);
                eintrag.Add("trend_name", trend["name"]);
                eintrag.Add("cat", fach);
            }
            if (Py.Wahr(lage["gegenwind"]))
            {
                eintrag.Add("gegenwind", lage["gegenwind"]);
            }
            if (Py.Wahr(lage["konjunktur"]))
            {
                eintrag.Add("konjunktur", lage["phase"]);
            }
            foreach (var p in new PyObjekt
            {
                { "id", kennung }, { "tag", tag }, { "kunde", kunde }, { "artikel", artikel["name"] }, { "menge", menge },
                // Mit eigenem Lager kauft man guenstiger ein als die Mitbewerber
                { "einkaufspreis", Rabatt(preis, rabatt) }, { "listenpreis", preis }, { "lieferzeit", lieferzeit },
                { "markt", billigst["zuschlag"] }, { "laune", billigst["laune"] }, { "konkurrent", billigst["id"] },
                { "bieter", gebote.Cast<object?>().ToList() }, { "ausgefallen", ausgefallen.Cast<object?>().ToList() },
                { "text", Prozent("%s\n\n%s", text, kunde["satz"]) }, { "ergebnis", stand.Angebote.Get(kennung) },
            })
            {
                Py.Setzen(eintrag, p.Key, p.Value);
            }
            ergebnis.Add(eintrag);
        }
        return ergebnis;
    }

    /// <summary>a % b wie Python (Ergebnis mit dem Vorzeichen von b).</summary>
    internal static long PyMod(long a, long b) => a - PyDiv(a, b) * b;

    private static PyObjekt Billigst(List<PyObjekt> gebote)
    {
        var best = gebote[0];
        foreach (var g in gebote.Skip(1))
        {
            if (Py.Kleiner(g["zuschlag"], best["zuschlag"]))
            {
                best = g;
            }
        }
        return best;
    }

    /// <summary>_market_markup: Bitweiches Gewinnzuschlag (prozent, laune).</summary>
    private static (long Zuschlag, string Laune) MarktZuschlag(PyObjekt regeln, PyObjekt art, string start, long tag, string salz)
    {
        var (unten, oben) = (Py.Int(art["von"]), Py.Int(art["bis"]));
        var mitte = (Wurf(start, tag, salz + "markt") + Wurf(start, tag, salz + "markt2")) / 2.0;
        var markt = unten + Math.Min(oben - unten, (long)(mitte * (oben - unten + 1)));
        var launen = OderLeer(regeln["laune"]);
        var wurf = Wurf(start, tag, salz + "laune");
        var kampf = launen["kampfpreis"] as PyObjekt;
        var voll = launen["ausgelastet"] as PyObjekt;
        if (Py.Wahr(kampf) && wurf < Py.F(kampf!["chance"]))
        {
            return (Zwischen(start, tag, salz + "kampf", Py.Int(kampf["von"]), Py.Int(kampf["bis"])), "kampfpreis");
        }
        if (Py.Wahr(voll) && wurf > 1 - Py.F(voll!["chance"]))
        {
            return (markt + Zwischen(start, tag, salz + "teuer", Py.Int(voll["plus_von"]), Py.Int(voll["plus_bis"])), "ausgelastet");
        }
        return (markt, "");
    }

    /// <summary>inquiry_task: die Anfrage als Formular-Aufgabe (Zuschlagskalkulation).</summary>
    public static PyObjekt AnfrageAufgabe(PyObjekt anfrage, object? zuschlag, PyObjekt inhalt)
    {
        var regeln = AngebotRegeln(inhalt);
        return new PyObjekt
        {
            { "id", anfrage["id"] }, { "typ", "formular" }, { "art", "angebot" },
            { "titel", Prozent("Angebot für %s", Objekt(anfrage["kunde"])["name"]) },
            { "ticket", anfrage["text"] },
            { "frage", "Wähle deinen Gewinnzuschlag und rechne das Angebot durch." },
            { "daten", new PyObjekt
                {
                    { "menge", anfrage["menge"] }, { "artikel", anfrage["artikel"] }, { "einkaufspreis", anfrage["einkaufspreis"] },
                    { "handlungskosten", regeln["handlungskosten"] }, { "gewinn", zuschlag }, { "ust", regeln["ust"] },
                }
            },
            { "hilfe", ListeOderLeer(regeln, "hilfe").ToList() },
        };
    }

    private static PyObjekt Zahlen(PyObjekt daten)
    {
        var d = new PyObjekt();
        foreach (var (id, _, wert) in Bewertung.AngebotWerte(daten))
        {
            Py.Setzen(d, id, wert);
        }
        return d;
    }

    /// <summary>offer_numbers: {schluessel: wert} der richtigen Zuschlagskalkulation.</summary>
    public static PyObjekt AngebotZahlen(PyObjekt anfrage, object? zuschlag, PyObjekt inhalt) =>
        Zahlen(Objekt(AnfrageAufgabe(anfrage, zuschlag, inhalt)["daten"]));

    private static PyObjekt MitListenpreis(PyObjekt anfrage)
    {
        var kopie = Py.Flach(anfrage);
        Py.Setzen(kopie, "einkaufspreis", Get(anfrage, "listenpreis", anfrage["einkaufspreis"]));
        return kopie;
    }

    /// <summary>market_price: Nettopreis von Bitweiche zum Listenpreis plus Bitweiches Zuschlag.</summary>
    public static double MarktPreis(PyObjekt anfrage, PyObjekt inhalt)
    {
        var kosten = AngebotZahlen(MitListenpreis(anfrage), 0L, inhalt)["selbstkosten"];
        return Geld(Py.F(kosten) * (1 + Py.F(anfrage["markt"]) / 100.0));
    }

    /// <summary>offer_advantage: erlaubter Abstand ueber dem guenstigsten Mitbewerber in Prozent.</summary>
    public static long AngebotVorteil(Spielstand stand, PyObjekt inhalt, object? fach = null)
    {
        var summe = Wirtschaft.VorteilTeile(stand, fach, inhalt).Sum(t => t.Prozent);
        var oben = Wirtschaft.ZertRegeln(inhalt)["vorteil_max"];
        return oben is null ? summe : Math.Min(Py.Int(oben), summe);
    }

    /// <summary>offer_result: bewertet ein Angebot auf eine Anfrage.</summary>
    public static PyObjekt AngebotErgebnis(Spielstand stand, PyObjekt anfrage, object? zuschlag, PyObjekt? antwort, PyObjekt inhalt)
    {
        var regeln = AngebotRegeln(inhalt);
        var aufgabe = AnfrageAufgabe(anfrage, zuschlag, inhalt);
        var markt = AnfrageAufgabe(MitListenpreis(anfrage), zuschlag, inhalt);
        var daten = AngebotBewerten(stand, aufgabe, anfrage, antwort, inhalt, markt);
        var gewonnen = Py.Wahr(daten["gewonnen"]);
        var zufrieden = Py.Plus(regeln["kundenzufriedenheit_gewonnen"], Py.Int(stand.RaumEffekt("kundenzufriedenheit_plus")));
        foreach (var k in new[] { "trend", "trend_name", "gegenwind", "konjunktur" })
        {
            if (Py.Wahr(anfrage[k]))
            {
                Py.Setzen(daten, k, anfrage[k]);
            }
        }
        Py.Setzen(daten, "anfrage", anfrage["id"]);
        Py.Setzen(daten, "tag", anfrage["tag"]);
        Py.Setzen(daten, "kunde", Objekt(anfrage["kunde"])["id"]);
        Py.Setzen(daten, "artikel", anfrage["artikel"]);
        Py.Setzen(daten, "menge", anfrage["menge"]);
        Py.Setzen(daten, "zuschlag", zuschlag);
        Py.Setzen(daten, "geld", gewonnen ? Py.Rund(daten["gewinn"]) : 0L);
        Py.Setzen(daten, "reputation", gewonnen ? new PyObjekt { { "kundenzufriedenheit", zufrieden } } : new PyObjekt());
        return daten;
    }

    /// <summary>_judge_offer: gemeinsame Bewertung fuer Anfragen und Projekte.</summary>
    internal static PyObjekt AngebotBewerten(Spielstand stand, PyObjekt aufgabe, PyObjekt angebot, PyObjekt? antwort, PyObjekt inhalt,
        PyObjekt? marktAufgabe = null)
    {
        var probleme = Bewertung.FormProbleme(aufgabe, antwort);
        var zahlen = Zahlen(Objekt(aufgabe["daten"]));
        var kosten = zahlen["selbstkosten"];
        var fremdKosten = kosten;
        if (marktAufgabe is not null)
        {
            fremdKosten = Zahlen(Objekt(marktAufgabe["daten"]))["selbstkosten"];
        }
        var gebote = (Py.Wahr(angebot["bieter"]) ? Liste(angebot["bieter"]) : []).Select(g =>
        {
            var kopie = Py.Flach(Objekt(g));
            Py.Setzen(kopie, "netto", Geld(Py.F(fremdKosten) * (1 + Py.F(Objekt(g)["zuschlag"]) / 100.0)));
            return (object?)kopie;
        }).ToList();
        var markt = Geld(Py.F(fremdKosten) * (1 + Py.F(angebot["markt"]) / 100.0));
        var voll = AngebotVorteil(stand, inhalt, angebot["cat"]);
        var vorteil = voll;
        if (Py.Wahr(angebot["gegenwind"]))
        {
            // Im Gegenwind zaehlt der Preisvorteil nur anteilig (ab 0.44, fester Faktor)
            var faktor = Get(OderLeer(RivalRegeln(inhalt)["gegenwind"]), "vorteil_faktor", 1L);
            vorteil = (long)(vorteil * Py.F(faktor));
        }
        // Ab 0.45: Naehe zum Kunden durch die Filiale (zaehlt immer voll)
        var naehe = FilialNaehe(stand, angebot["kunde"] is PyObjekt k ? k["id"] : angebot["kunde"], inhalt);
        vorteil += naehe;
        var richtig = probleme.Count == 0;
        var billig = Py.F(zahlen["netto"]) <= Geld(markt * (1 + vorteil / 100.0)) + 0.001;
        var gewonnen = richtig && billig;
        var gruende = new List<object?>();
        if (vorteil != 0)
        {
            gruende.AddRange(Wirtschaft.VorteilTeile(stand, angebot["cat"], inhalt).Select(t => (object?)t.Grund));
            if (naehe != 0)
            {
                gruende.Add(Get(Objekt(FilialRegeln(inhalt)["naehe"]), "text", "Filiale"));
            }
        }
        var daten = new PyObjekt
        {
            { "zuschlag", Objekt(aufgabe["daten"])["gewinn"] }, { "antwort", antwort is null ? new PyObjekt() : Py.Flach(antwort) },
            { "richtig", richtig }, { "selbstkosten", kosten }, { "netto", zahlen["netto"] }, { "gewinn", zahlen["gewinn"] },
            { "marktpreis", markt }, { "markt_zuschlag", angebot["markt"] }, { "laune", Get(angebot, "laune", "") },
            { "konkurrent", Get(angebot, "konkurrent", "") }, { "bieter", gebote },
            { "ausgefallen", (Py.Wahr(angebot["ausgefallen"]) ? Liste(angebot["ausgefallen"]) : []).ToList() },
            { "vorteil", vorteil }, { "gewonnen", gewonnen }, { "vorteil_gruende", gruende },
            { "grund", gewonnen ? "" : !richtig ? "rechenfehler" : "preis" },
        };
        if (naehe != 0)
        {
            Py.Setzen(daten, "naehe", naehe);
        }
        if (Py.Wahr(angebot["gegenwind"]))
        {
            Py.Setzen(daten, "gegenwind", angebot["gegenwind"]);
            if (vorteil - naehe < voll)
            {
                Py.Setzen(daten, "vorteil_voll", voll + naehe);
            }
        }
        if (probleme.Count > 0)
        {
            Py.Setzen(daten, "probleme", probleme.Cast<object?>().ToList());
        }
        return daten;
    }

    /// <summary>offer_result_text: (Ueberschrift, Text) nach dem Abschicken eines Angebots.</summary>
    public static (string Kopf, string Text) AngebotErgebnisText(PyObjekt daten, PyObjekt inhalt)
    {
        var (kopf, text) = AngebotErgebnisGrundtext(daten, inhalt);
        var rivale = Mitbewerber(Py.Wahr(daten["konkurrent"]) ? daten["konkurrent"] : Bitweiche, inhalt);
        if (MitbewerberLaune(rivale, Py.Wahr(daten["laune"]) ? Py.Str(daten["laune"]) : "", inhalt) is PyObjekt laune)
        {
            text += " " + Py.Str(laune["text"]);
        }
        foreach (var r in Py.Wahr(daten["ausgefallen"]) ? Liste(daten["ausgefallen"]) : [])
        {
            if (MitbewerberLaune(Mitbewerber(r, inhalt), "ausgelastet", inhalt) is PyObjekt l)
            {
                text += " " + Py.Str(l["text"]);
            }
        }
        if (Py.Wahr(daten["gegenwind"]))
        {
            text += Prozent(" Die Mitbewerber haben gezielt %d Punkte günstiger gegen dich geboten.", daten["gegenwind"]);
            if (Py.Wahr(daten["vorteil_voll"]))
            {
                text += Prozent(" Dein Preisvorteil zählte dabei nur %d statt %d %%.", daten["vorteil"], daten["vorteil_voll"]);
            }
        }
        var andere = BieterText(daten, inhalt);
        if (andere.Length > 0)
        {
            text += "\n\n" + andere;
        }
        return (kopf, text);
    }

    /// <summary>inquiry_badge_text: "Trend-Auftrag: Cloud-Boom".</summary>
    public static string AnfrageMarkeText(PyObjekt anfrage) =>
        Py.Wahr(anfrage["trend_name"]) ? Prozent("Trend-Auftrag: %s", anfrage["trend_name"]) : "";

    /// <summary>bidders_text: "Mitgeboten haben: ..."</summary>
    public static string BieterText(PyObjekt daten, PyObjekt inhalt)
    {
        var gebote = (Py.Wahr(daten["bieter"]) ? Liste(daten["bieter"]) : []).Select(Objekt).Where(g => Py.Hat(g, "netto"))
            .OrderBy(g => Py.F(g["netto"])).ToList();
        if (gebote.Count < 2)
        {
            return "";
        }
        return Prozent("Mitgeboten haben: %s.", string.Join(", ", gebote.Select(g =>
            Prozent("%s %s (%d %%)", Mitbewerber(g["id"], inhalt)["kurz"], Euro(g["netto"]), g["zuschlag"]))));
    }

    private static (string, string) AngebotErgebnisGrundtext(PyObjekt daten, PyObjekt inhalt)
    {
        var projekt = Py.Wahr(daten["projekt"]);
        object? kundenName;
        string sache, gewonnenKopf;
        if (projekt)
        {
            kundenName = Py.Wahr(daten["kunde_kurz"]) ? daten["kunde_kurz"] : "Der Kunde";
            (sache, gewonnenKopf) = ("das Projekt", "Projekt gewonnen");
        }
        else
        {
            kundenName = (FirmaKunde(daten["kunde"], inhalt) ?? new PyObjekt { { "name", "Der Kunde" } })["name"];
            (sache, gewonnenKopf) = ("den Auftrag", "Auftrag gewonnen");
        }
        var rivale = Mitbewerber(Py.Wahr(daten["konkurrent"]) ? daten["konkurrent"] : Bitweiche, inhalt)["kurz"];
        var eigen = Euro(daten["netto"]);
        var markt = Euro(daten["marktpreis"]);
        if (Py.Wahr(daten["gewonnen"]))
        {
            string text;
            if (projekt)
            {
                text = Prozent("%s gibt dir den Zuschlag. Du lagst netto bei %s, am günstigsten unter "
                    + "den anderen war %s mit %s (Zuschlag %d %%). Anzahlung: +%s, "
                    + "Material und Lizenzen: -%s. Stell jetzt ein Team zusammen.", kundenName, eigen, rivale, markt,
                    daten["markt_zuschlag"], Wohnung.GanzeEuro(Get(daten, "anzahlung", 0L)), Wohnung.GanzeEuro(Get(daten, "material", 0L)));
            }
            else
            {
                text = Prozent("%s nimmt dein Angebot an. Du lagst netto bei %s, %s bei %s "
                    + "(Zuschlag %d %%). Gewinn für deine Firma: +%s.", kundenName, eigen, rivale, markt, daten["markt_zuschlag"],
                    Wohnung.GanzeEuro(daten["geld"]));
            }
            if (Py.Wahr(daten["vorteil"]) && Py.Kleiner(daten["marktpreis"], daten["netto"]))
            {
                text += Prozent(" Knapp über %s, aber %s hat den Ausschlag gegeben.", rivale, GruendeText(daten));
            }
            return (gewonnenKopf, text);
        }
        if (Py.Gleich(daten["grund"], "rechenfehler"))
        {
            return (Prozent("%s verloren: Fehler im Angebot", projekt ? "Projekt" : "Auftrag"),
                Prozent("%s hat einen Fehler in deiner Kalkulation gefunden und gibt %s an "
                    + "%s (netto %s). Richtig gerechnet wären es mit %d %% Zuschlag "
                    + "netto %s gewesen.", kundenName, sache, rivale, markt, daten["zuschlag"], eigen));
        }
        var kopf = Prozent("%s an %s verloren", projekt ? "Projekt" : "Auftrag", rivale);
        var rest = Prozent("Die Rechnung stimmt, aber %s war günstiger: netto %s (Zuschlag %d %%) "
            + "gegen deine %s (Zuschlag %d %%).", rivale, markt, daten["markt_zuschlag"], eigen, daten["zuschlag"]);
        if (Py.Wahr(daten["vorteil"]))
        {
            rest += Prozent(" Selbst mit dem Bonus für %s (%d %%) hat es nicht gereicht.", GruendeText(daten, kurz: true), daten["vorteil"]);
        }
        return (kopf, rest);
    }

    /// <summary>_reasons_text: woher der Preisvorteil kam (aeltere Ergebnisse: guter Ruf).</summary>
    private static string GruendeText(PyObjekt daten, bool kurz = false)
    {
        var gruende = Py.Wahr(daten["vorteil_gruende"]) ? Liste(daten["vorteil_gruende"]) : ["guter Ruf"];
        var worte = new Dictionary<string, (string, string)>
        {
            ["guter Ruf"] = ("dein guter Ruf bei den Kunden", "deinen guten Ruf"),
            ["Zertifizierungen"] = ("deine Zertifizierungen", "deine Zertifizierungen"),
            ["Werbung"] = ("deine Werbung", "deine Werbung"),
        };
        var namen = gruende.Select(g => Py.Str(g)).Select(g => worte.TryGetValue(g, out var w) ? (kurz ? w.Item2 : w.Item1) : g).ToList();
        return namen.Count == 1 ? namen[0] : string.Join(", ", namen.Take(namen.Count - 1)) + " und " + namen[^1];
    }

    /// <summary>inquiry_status_text: kurze Zeile zum Stand einer Anfrage.</summary>
    public static string AnfrageStandText(PyObjekt anfrage, PyObjekt inhalt)
    {
        if (anfrage["ergebnis"] is not PyObjekt ergebnis || !Py.Wahr(ergebnis))
        {
            var text = Prozent("Einkauf %s × %s · Lieferung in %s", anfrage["menge"], Euro(anfrage["einkaufspreis"]),
                Plural(anfrage["lieferzeit"], "Arbeitstag", "Arbeitstagen"));
            var liste = Get(anfrage, "listenpreis", anfrage["einkaufspreis"]);
            if (Py.Kleiner(anfrage["einkaufspreis"], liste))
            {
                text += Prozent(" · Lager-Rabatt (sonst %s)", Euro(liste));
            }
            return text;
        }
        if (Py.Wahr(ergebnis["gewonnen"]))
        {
            return Prozent("Gewonnen · Gewinn +%s", Wohnung.GanzeEuro(Get(ergebnis, "geld", 0L)));
        }
        if (Py.Gleich(ergebnis["grund"], "rechenfehler"))
        {
            return "Verloren · Fehler im Angebot";
        }
        return Prozent("Verloren · %s war günstiger", Mitbewerber(Py.Wahr(ergebnis["konkurrent"]) ? ergebnis["konkurrent"] : Bitweiche, inhalt)["kurz"]);
    }

    // -- Kundentickets ------------------------------------------------------

    /// <summary>ticket_limit</summary>
    public static long TicketGrenze(object? person, PyObjekt inhalt)
    {
        var regeln = TicketRegeln(inhalt);
        return Py.Int(Py.Gleich(person, Selbst) ? regeln["spieler_max"] : regeln["mitarbeiter_max"]);
    }

    /// <summary>own_ticket_limit: einer weniger im Projekt und nach einem Schlichtungsgespraech.</summary>
    public static long EigeneTicketGrenze(Spielstand stand, PyObjekt inhalt)
    {
        var grenze = Math.Max(1, TicketGrenze(Selbst, inhalt) - (Py.Wahr(stand.ProjektVon(Selbst)) ? 1 : 0));
        return grenze - (stand.SchlichtTage.Contains(stand.Tag) ? 1 : 0);
    }

    /// <summary>ticket_chance: Erfolgschance in Prozent (ganze Zahl).</summary>
    public static long TicketChance(object? wert, object? anforderung, PyObjekt inhalt, object? bonus = null)
    {
        var regel = Objekt(TicketRegeln(inhalt)["chance"]);
        var chance = Py.Plus(Py.Plus(regel["basis"], Py.Mal(regel["je_punkt"], Py.F(wert) - Py.F(anforderung))), Py.F(bonus ?? 0L));
        return Py.Rund(Py.Max(regel["min"], Py.Min(regel["max"], chance)));
    }

    /// <summary>tickets_for_day: die Kundentickets eines Arbeitstags.</summary>
    public static List<PyObjekt> TicketsFuerTag(Spielstand stand, long tag, PyObjekt inhalt)
    {
        if (!Py.Wahr(stand.Firma))
        {
            return [];
        }
        var regeln = TicketRegeln(inhalt);
        var start = stand.FirmenStartwert;
        var anzahl = Math.Max(Py.Int(regeln["mindestens"]), Py.Int(regeln["grundzahl"]) + Py.Int(regeln["je_mitarbeiter"]) * stand.Personal.Count);
        // Ab 0.42: Werbung bringt mehr Kundentickets
        anzahl += Wirtschaft.ExtraAnzahl(Wirtschaft.WerbeWirkung(stand, tag, inhalt)["tickets"], start, tag, "werbung-tickets");
        var reihe = Liste(regeln["vorlagen"]).Select(Objekt).OrderBy(v => Wurf(start, tag, "kt|" + Py.Str(v["id"]))).ToList();
        var gewaehlt = reihe.Take((int)Math.Max(0, anzahl)).ToList();
        // Schon verteilte Tickets bleiben, auch wenn inzwischen jemand gegangen ist
        foreach (var v in reihe.Skip((int)Math.Max(0, anzahl)))
        {
            if (stand.Delegationen.ContainsKey(Prozent("%s%d:%s", TicketPrefix, tag, v["id"])))
            {
                gewaehlt.Add(v);
            }
        }
        var ergebnis = new List<PyObjekt>();
        foreach (var v in gewaehlt)
        {
            var kennung = Prozent("%s%d:%s", TicketPrefix, tag, v["id"]);
            var stufe = Objekt(Objekt(regeln["stufen"])[Py.Str(v["stufe"])]);
            var vergeben = stand.Delegationen.Get(kennung) is PyObjekt d && Py.Wahr(d) ? d : [];
            ergebnis.Add(new PyObjekt
            {
                { "id", kennung }, { "vorlage", v["id"] }, { "titel", v["titel"] }, { "text", v["text"] },
                { "kunde", FirmaKunde(v["kunde"], inhalt) }, { "cat", v["cat"] }, { "thema", v["thema"] },
                { "stufe", stufe["name"] }, { "anforderung", stufe["anforderung"] }, { "geld", stufe["geld"] },
                { "an", vergeben["an"] }, { "name", vergeben["name"] }, { "chance", vergeben["chance"] },
                { "ergebnis", stand.TicketErgebnisse.Get(kennung) },
            });
        }
        return ergebnis;
    }

    /// <summary>ticket_candidates: wer ein Kundenticket uebernehmen kann (zuerst die Spielfigur).</summary>
    public static List<PyObjekt> TicketKandidaten(Spielstand stand, PyObjekt ticket, PyObjekt? stufen, PyObjekt inhalt)
    {
        var fach = ticket["cat"];
        var thema = ticket["thema"];
        var ergebnis = new List<PyObjekt>();
        // Ab 0.38 zaehlt der Wert im Thema des Tickets (Spielfigur: Wissensstand)
        var eigen = ThemaStand(stufen, thema, fach);
        var name = Py.Wahr(Get(stand.Profil ?? [], "name")) ? stand.Profil!["name"] : "Ich";
        var grenze = EigeneTicketGrenze(stand, inhalt);
        var problem = "";
        if (grenze <= 0)
        {
            problem = "Heute hast du keine Zeit mehr für Kundentickets, das Schlichtungsgespräch hat deinen Platz gekostet.";
        }
        else if (stand.UebernommenVon(Selbst).Count >= grenze)
        {
            problem = Prozent("Du hast heute schon %s übernommen.", grenze == 1 ? "ein Kundenticket" : Prozent("%d Kundentickets", grenze));
            if (Py.Wahr(stand.ProjektVon(Selbst)))
            {
                problem += " Mehr geht nicht, weil du im Projekt mitarbeitest.";
            }
            if (stand.SchlichtTage.Contains(stand.Tag))
            {
                problem += " Einen Platz hat heute das Schlichtungsgespräch gekostet.";
            }
        }
        ergebnis.Add(new PyObjekt
        {
            { "an", Selbst }, { "name", Prozent("Ich selbst (%s)", name) }, { "wert", eigen },
            { "chance", TicketChance(eigen, ticket["anforderung"], inhalt) }, { "problem", problem },
        });
        foreach (var e in stand.PersonalListe())
        {
            var wert = Py.Wahr(thema) ? Py.Int(Get(Objekt(e["themen"]), Py.Str(thema), 0L)) : Py.Int(Get(Objekt(e["werte"]), Py.Str(fach), 0L));
            var p = "";
            if (Py.Wahr(e["weiterbildung"]) || Py.Wahr(e["abwesend"]))
            {
                p = AbwesendProblem(e["name"], Objekt(Py.Wahr(e["weiterbildung"]) ? e["weiterbildung"] : e["abwesend"]));
            }
            else if (Py.Wahr(e["projekt"]))
            {
                p = Prozent("%s arbeitet im Projekt „%s“ mit.", e["name"], Objekt(stand.Projekte[e["projekt"]])["titel"]);
            }
            else if (Py.Wahr(e["serverfarm"]))
            {
                p = Prozent(FarmBeschaeftigtText, e["name"]);
            }
            else if (stand.UebernommenVon(e["id"]).Count >= TicketGrenze(e["id"], inhalt))
            {
                p = Prozent("%s hat heute schon ein Kundenticket.", e["name"]);
            }
            ergebnis.Add(new PyObjekt
            {
                { "an", e["id"] }, { "name", e["name"] }, { "wert", wert },
                { "chance", MitarbeiterTicketChance(stand, e["id"], ticket, wert, inhalt) }, { "problem", p },
            });
        }
        return ergebnis;
    }

    /// <summary>ticket_outcomes: Ergebnisse der heute verteilten Kundentickets fuer den Feierabend.</summary>
    public static List<PyObjekt> TicketAusgaenge(Spielstand stand, PyObjekt inhalt)
    {
        var regeln = TicketRegeln(inhalt);
        var ergebnis = new List<PyObjekt>();
        foreach (var ticket in stand.KundenTickets())
        {
            if (!Py.Wahr(ticket["an"]) || Py.Wahr(ticket["ergebnis"]))
            {
                continue;
            }
            var wurf = Wurf(stand.FirmenStartwert, stand.Tag, "erfolg|" + Py.Str(ticket["id"]));
            var erfolg = Py.Kleiner(wurf * 100, ticket["chance"]);
            var regel = Py.Flach(Objekt(regeln[erfolg ? "erfolg" : "fehlschlag"]));
            if (stand.Personal.ContainsKey(ticket["an"]))
            {
                // Ab 0.43: Macken, die Kunden besonders zufrieden machen
                var extra = stand.MackeWert(ticket["an"], erfolg ? "kz_erfolg" : "kz_fehlschlag");
                if (extra != 0)
                {
                    Py.Setzen(regel, "kundenzufriedenheit", Py.Rund(Py.Plus(Get(regel, "kundenzufriedenheit", 0L), extra)));
                    if (!erfolg)
                    {
                        Py.Setzen(regel, "kundenzufriedenheit", Py.Min(0L, regel["kundenzufriedenheit"]));
                    }
                }
            }
            var reputation = new PyObjekt();
            foreach (var p in regel.Where(p => Py.Wahr(p.Value)))
            {
                reputation.Add(p.Key, p.Value);
            }
            ergebnis.Add(new PyObjekt
            {
                { "ticket", ticket["id"] }, { "vorlage", ticket["vorlage"] }, { "titel", ticket["titel"] },
                { "kunde", Objekt(ticket["kunde"])["name"] }, { "an", ticket["an"] }, { "name", ticket["name"] },
                { "erfolg", erfolg }, { "geld", erfolg ? ticket["geld"] : 0L }, { "reputation", reputation },
            });
        }
        return ergebnis;
    }

    /// <summary>subject_text: "Netzwerk · WLAN" (ohne Thema nur der Fachbereich).</summary>
    public static string FachText(object? fach, object? thema = null)
    {
        var text = T.FachKurzname[T.Name[Py.Str(fach)]];
        return thema is string t && T.Kurzname.TryGetValue(t, out var k) ? Prozent("%s · %s", text, k) : text;
    }

    /// <summary>ticket_result_text</summary>
    public static string TicketErgebnisText(PyObjekt eintrag)
    {
        var wer = Py.Gleich(eintrag["an"], Selbst) ? "Du" : KurzName(Get(eintrag, "name", ""));
        return Py.Wahr(eintrag["erfolg"])
            ? Prozent("%s: „%s“ für %s erledigt, +%s", wer, eintrag["titel"], eintrag["kunde"], Wohnung.GanzeEuro(Get(eintrag, "geld", 0L)))
            : Prozent("%s: „%s“ für %s nicht geschafft, der Kunde ist unzufrieden", wer, eintrag["titel"], eintrag["kunde"]);
    }

    /// <summary>ticket_line: Fachbereich, Thema, Schwierigkeit, Honorar.</summary>
    public static string TicketZeile(PyObjekt ticket)
    {
        var text = Prozent("%s · %s · %s", FachText(ticket["cat"], ticket["thema"]), ticket["stufe"], Wohnung.GanzeEuro(ticket["geld"]));
        if (Py.Wahr(ticket["an"]))
        {
            var wer = Py.Gleich(ticket["an"], Selbst) ? "dir" : KurzName(ticket["name"]);
            text += Prozent(" · übernommen von %s (Chance %d %%)", wer, ticket["chance"]);
        }
        return text;
    }

    /// <summary>firm_orders_summary: was heute noch zu tun ist.</summary>
    public static string AuftraegeZeile(Spielstand stand)
    {
        var anfragen = stand.Anfragen().Count(a => !Py.Wahr(a["ergebnis"]));
        var tickets = stand.KundenTickets().Count(t => !Py.Wahr(t["an"]));
        var teile = new List<string>();
        if (anfragen > 0)
        {
            teile.Add(anfragen == 1 ? "1 Anfrage" : Prozent("%d Anfragen", (long)anfragen));
        }
        if (tickets > 0)
        {
            teile.Add(tickets == 1 ? "1 Kundenticket" : Prozent("%d Kundentickets", (long)tickets));
        }
        return teile.Count > 0 ? Prozent("Heute offen: %s", string.Join(" und ", teile)) : "";
    }

    // -- Mitbewerber (ab 0.35) ----------------------------------------------

    /// <summary>competitor_rules</summary>
    public static PyObjekt MitbewerberRegeln(PyObjekt inhalt) => OderLeer(FirmaRegeln(inhalt)["mitbewerber"]);

    /// <summary>competitors</summary>
    public static List<PyObjekt> AlleMitbewerber(PyObjekt inhalt)
    {
        var firmen = MitbewerberRegeln(inhalt)["firmen"];
        return Py.Wahr(firmen) ? [.. Liste(firmen).Select(Objekt)] : [BitweicheStandard()];
    }

    /// <summary>competitor</summary>
    public static PyObjekt Mitbewerber(object? kennung, PyObjekt inhalt)
    {
        foreach (var e in AlleMitbewerber(inhalt))
        {
            if (Py.Gleich(e["id"], kennung))
            {
                return e;
            }
        }
        return Py.Gleich(kennung, Bitweiche) ? BitweicheStandard()
            : new PyObjekt { { "id", kennung }, { "name", kennung }, { "kurz", kennung } };
    }

    /// <summary>competitor_mood: Laune einer Firma mit Text (Bitweiche aus angebote.laune).</summary>
    public static PyObjekt? MitbewerberLaune(PyObjekt? rivale, string laune, PyObjekt inhalt)
    {
        if (laune.Length == 0 || !Py.Wahr(rivale))
        {
            return null;
        }
        var launen = Py.Wahr(rivale!["kundenart"]) ? OderLeer(AngebotRegeln(inhalt)["laune"]) : OderLeer(rivale["laune"]);
        return launen[laune] as PyObjekt;
    }

    /// <summary>_weighted_order: Reihenfolge ohne Zuruecklegen, schwere Eintraege eher vorne.</summary>
    private static List<PyObjekt> GewichteteReihe(List<PyObjekt> eintraege, List<object?> gewichte, string start, long tag, string salz)
    {
        var geschluesselt = new List<(double Schluessel, PyObjekt Eintrag)>();
        foreach (var (e, g) in eintraege.Zip(gewichte))
        {
            if (!Py.Kleiner(0L, g))
            {
                continue;
            }
            var wurf = Math.Max(1e-9, Wurf(start, tag, salz + "|" + Py.Str(e["id"])));
            geschluesselt.Add((-Math.Log(wurf) / Py.F(g), e));
        }
        return [.. geschluesselt.OrderBy(p => p.Schluessel).Select(p => p.Eintrag)];
    }

    /// <summary>_rival_markup: (Zuschlag, Laune) einer Firma, null, wenn sie nicht bietet.</summary>
    private static (long Zuschlag, string Laune)? RivalZuschlag(PyObjekt rivale, PyObjekt regeln, object? art, object? fach, string start, long tag, string salz)
    {
        if (Py.Wahr(rivale["kundenart"]))
        {
            // Bitweiche wie in 0.34 (gleiche Wuerfel, damit nichts springt)
            return MarktZuschlag(regeln, Objekt(Objekt(regeln["arten"])[Py.Str(art)]), start, tag, salz);
        }
        salz = Prozent("%s%s-", salz, rivale["id"]);
        var (unten, oben) = (Py.Int(Get(rivale, "von", 5L)), Py.Int(Get(rivale, "bis", 25L)));
        var mitte = Py.Wahr(rivale["gleichmaessig"]) ? Wurf(start, tag, salz + "markt")
            : (Wurf(start, tag, salz + "markt") + Wurf(start, tag, salz + "markt2")) / 2.0;
        var zuschlag = unten + Math.Min(oben - unten, (long)(mitte * (oben - unten + 1)));
        if (Py.Wahr(fach))
        {
            zuschlag += Py.Int(Get(OderLeer(rivale["fach"]), Py.Str(fach), 0L));
        }
        var launen = OderLeer(rivale["laune"]);
        var wurf = Wurf(start, tag, salz + "laune");
        var kampf = launen["kampfpreis"] as PyObjekt;
        var voll = launen["ausgelastet"] as PyObjekt;
        if (Py.Wahr(kampf) && wurf < Py.F(kampf!["chance"]))
        {
            return (Zwischen(start, tag, salz + "kampf", Py.Int(kampf["von"]), Py.Int(kampf["bis"])), "kampfpreis");
        }
        if (Py.Wahr(voll) && wurf > 1 - Py.F(voll!["chance"]))
        {
            if (Py.Wahr(voll["bietet_nicht"]))
            {
                return null;
            }
            zuschlag += Zwischen(start, tag, salz + "teuer", Py.Int(Get(voll, "plus_von", 10L)), Py.Int(Get(voll, "plus_bis", 20L)));
            return (Math.Max(0, zuschlag), "ausgelastet");
        }
        return (Math.Max(0, zuschlag), "");
    }

    /// <summary>competitor_bids: wer mitbietet ([{"id", "zuschlag", "laune"}], [ausgefallene ids]).</summary>
    public static (List<PyObjekt> Gebote, List<object?> Ausgefallen) MitbewerberGebote(string art, object? kundenart, object? fach,
        string start, long tag, string salz, PyObjekt inhalt, long extra = 0, long schub = 0, double zweiter = 0.0)
    {
        var regeln = AngebotRegeln(inhalt);
        var aufbau = MitbewerberRegeln(inhalt);
        var rivalen = AlleMitbewerber(inhalt);
        long anzahl;
        if (art == "projekt")
        {
            var spanne = Py.Wahr(aufbau["projekt_bieter"]) ? Objekt(aufbau["projekt_bieter"]) : new PyObjekt { { "von", 2L }, { "bis", 3L } };
            anzahl = Zwischen(start, tag, salz + "bieterzahl", Py.Int(spanne["von"]), Py.Int(spanne["bis"]));
        }
        else
        {
            var einer = Py.F(Get(OderLeer(aufbau["anfrage_bieter"]), "eins", 1.0)) - zweiter;
            anzahl = Wurf(start, tag, salz + "bieterzahl") < einer ? 1 : 2;
        }
        anzahl += extra;
        var reihe = GewichteteReihe(rivalen, [.. rivalen.Select(r => Get(OderLeer(r["gewicht"]), art, 1L))], start, tag, salz + "bieter");
        var gebote = new List<PyObjekt>();
        var aus = new List<object?>();
        foreach (var rivale in reihe.Take((int)Math.Max(1, anzahl)))
        {
            var z = RivalZuschlag(rivale, regeln, kundenart, fach, start, tag, salz);
            if (z is null)
            {
                aus.Add(rivale["id"]);
                continue;
            }
            gebote.Add(new PyObjekt { { "id", rivale["id"] }, { "zuschlag", Math.Max(0, z.Value.Zuschlag + schub) }, { "laune", z.Value.Laune } });
        }
        if (gebote.Count == 0)
        {
            // Niemand da? Bitweiche bietet immer
            var z = RivalZuschlag(Mitbewerber(Bitweiche, inhalt), regeln, kundenart, fach, start, tag, salz)
                ?? throw new InvalidOperationException("Bitweiche bietet nicht");
            gebote.Add(new PyObjekt { { "id", Bitweiche }, { "zuschlag", Math.Max(0, z.Zuschlag + schub) }, { "laune", z.Laune } });
        }
        return (gebote, aus);
    }

    /// <summary>market_shift: Punkte auf die Zuschlaege der Mitbewerber an einem Tag (ab 0.44).</summary>
    public static PyObjekt MarktVerschiebung(Spielstand stand, long tag, PyObjekt inhalt, bool trend = false)
    {
        var markt = Marktlage(stand, tag, inhalt);
        var druck = Gegenwind(stand, tag, inhalt);
        var boom = Py.Int(Get(Objekt(markt["phase_info"]), "zuschlag", 0L));
        var hype = trend ? Py.Int(Get(OderLeer(MarktRegeln(inhalt)["trends"]), "zuschlag", 0L)) : 0;
        return new PyObjekt
        {
            { "shift", boom + hype - Py.Int(druck["minus"]) }, { "konjunktur", boom }, { "trend", hype },
            { "gegenwind", druck["minus"] }, { "zweiter", druck["zweiter"] }, { "phase", markt["phase"] }, { "markt", markt },
        };
    }

    /// <summary>lost_to: gegen wen Angebote verloren gingen [(name, anzahl)], meiste zuerst.</summary>
    public static List<(object? Name, long Anzahl)> VerlorenAn(Spielstand stand, PyObjekt inhalt)
    {
        var zaehler = new PyDict();
        foreach (var e in stand.Angebote.Values.Concat(stand.ProjektAngebote.Values).Select(Objekt))
        {
            if (Py.Wahr(e["gewonnen"]))
            {
                continue;
            }
            var r = Py.Wahr(e["konkurrent"]) ? e["konkurrent"] : Bitweiche;
            zaehler[r] = Py.Int(zaehler.Get(r, 0L)) + 1;
        }
        var reihe = AlleMitbewerber(inhalt).Select(r => r["id"]).ToList();
        return [.. zaehler.OrderBy(p => -Py.Int(p.Value)).ThenBy(p => reihe.FindIndex(x => Py.Gleich(x, p.Key)) is var i && i >= 0 ? i : 99)
            .Select(p => (Mitbewerber(p.Key, inhalt)["name"], Py.Int(p.Value)))];
    }

    // -- Rivalitaet und Markt (ab 0.44) -------------------------------------

    /// <summary>rivalry_rules</summary>
    public static PyObjekt RivalRegeln(PyObjekt inhalt) => OderLeer(FirmaRegeln(inhalt)["rivalitaet"]);

    /// <summary>market_rules</summary>
    public static PyObjekt MarktRegeln(PyObjekt inhalt) => OderLeer(FirmaRegeln(inhalt)["markt"]);

    /// <summary>former_for: frueherer Arbeitgeber eines normalen Bewerbers (Mitbewerber-id oder leer).</summary>
    public static object? FruehererArbeitgeberVon(string start, string person, PyObjekt inhalt)
    {
        var regel = OderLeer(RivalRegeln(inhalt)["rueckhol"]);
        var anteil = Py.F(Get(regel, "vorher_anteil", 0L));
        if (anteil == 0 || Wurf(start, 0, "vorher|" + person) * 100 >= anteil)
        {
            return "";
        }
        return Waehle(AlleMitbewerber(inhalt), start, 0, "vorher-firma|" + person)["id"];
    }

    /// <summary>former_employer: bei welchem Mitbewerber jemand vorher war (id oder leer).</summary>
    public static object? FruehererArbeitgeber(Spielstand stand, object? person, PyObjekt? daten = null)
    {
        daten ??= stand.Personal.Get(person) is PyObjekt d && Py.Wahr(d) ? d : [];
        if (Py.Gleich(daten["herkunft"], "bitweiche"))
        {
            return Bitweiche;
        }
        if (Py.Hat(daten, "vorher"))
        {
            return Py.Wahr(daten["vorher"]) ? daten["vorher"] : "";
        }
        if (!Py.Gleich(daten["herkunft"], "bewerbung"))
        {
            return "";
        }
        return FruehererArbeitgeberVon(stand.FirmenStartwert, Py.Str(person), stand.Inhalt);
    }

    /// <summary>recall_factor: wie viel wahrscheinlicher ein Rueckhol-Angebot bei dieser Stimmung ist.</summary>
    public static double RueckholFaktor(double stimmung, PyObjekt regel)
    {
        var teiler = Py.F(Get(regel, "stimmung_teiler", 40L));
        var faktor = 1 + (Py.F(Get(regel, "stimmung_ziel", 70L)) - stimmung) / (teiler != 0 ? teiler : 40.0);
        var unten = Py.F(Get(regel, "faktor_min", 0.5));
        var oben = Py.F(Get(regel, "faktor_max", 2.0));
        var m = faktor < oben ? faktor : oben;
        return m > unten ? m : unten;
    }

    /// <summary>recall_raise: neues Gehalt beim Gegenangebot (ganze Euro, mindestens +1).</summary>
    public static long RueckholGehaltNeu(object? gehalt, PyObjekt regel) =>
        Math.Max(Py.Int(gehalt) + 1, PythonRunden.Runde(Py.Int(gehalt) * (1 + Py.F(Get(regel, "gehalt_plus", 10L)) / 100.0)));

    /// <summary>recall_bonus</summary>
    public static long RueckholPraemie(object? gehalt, PyObjekt regel) => Py.Int(gehalt) * Py.Int(Get(regel, "bonus_tage", 10L));

    /// <summary>_recall_events: hoechstens ein Rueckhol-Angebot pro Feierabend (fest gewuerfelt).</summary>
    internal static List<PyObjekt> RueckholEreignisse(Spielstand stand, long tag, IEnumerable<object?> anwesend, ICollection<object?> vergeben, PyObjekt inhalt)
    {
        var regel = RivalRegeln(inhalt)["rueckhol"] as PyObjekt;
        if (!Py.Wahr(regel) || !Py.Wahr(stand.Erleichterung("rueckhol", true)))
        {
            return [];
        }
        var start = stand.FirmenStartwert;
        var protokoll = stand.PersonalProtokoll.Cast<PyTupel>().Select(t => (Tag: Py.Int(t[0]), Eintrag: Objekt(t[1]))).ToList();
        var zuletzt = protokoll.Where(p => Py.Gleich(p.Eintrag["art"], PersonalRueckhol)).Select(p => p.Tag).DefaultIfEmpty(-1000).Max();
        if (tag - zuletzt < Py.Int(Get(regel!, "abstand_firma", 0L)))
        {
            return [];
        }
        foreach (var person in anwesend)
        {
            var daten = Objekt(stand.Personal[person]);
            var rivale = FruehererArbeitgeber(stand, person, daten);
            if (!Py.Wahr(rivale) || vergeben.Contains(person) || stand.Gehend.ContainsKey(person))
            {
                continue;
            }
            if (tag - Py.Int(Py.Wahr(Get(daten, "tag", 0L)) ? daten["tag"] : 0L) < Py.Int(regel!["ab_tagen"]))
            {
                continue;
            }
            var gefragt = protokoll.Where(p => Py.Gleich(p.Eintrag["art"], PersonalRueckhol) && Py.Gleich(p.Eintrag["id"], person)).Select(p => p.Tag).ToList();
            if (gefragt.Count > 0 && tag - gefragt[^1] < Py.Int(regel["abstand"]))
            {
                continue;
            }
            var chance = Py.F(regel["chance"]) * RueckholFaktor(stand.StimmungVon(person), regel);
            if (Wurf(start, tag, "rueckhol|" + Py.Str(person)) * 100 >= chance)
            {
                continue;
            }
            var firma = Mitbewerber(rivale, inhalt);
            var spanne = Liste(regel["angebot_plus"]);
            var plus = Zwischen(start, tag, "rueckhol-plus|" + Py.Str(person), Py.Int(spanne[0]), Py.Int(spanne[1]));
            var texte = (Py.Gleich(rivale, Bitweiche) ? regel["texte_bitweiche"] : null) is { } tb && Py.Wahr(tb) ? tb : regel["texte"];
            var name = Get(daten, "name", "");
            var text = Py.Format(Py.Str(Waehle(Liste(texte), start, tag, "rueckhol-text|" + Py.Str(person))), new PyObjekt
            {
                { "firma", firma["kurz"] }, { "name", KurzName(name) }, { "plus", plus },
            });
            return
            [
                new PyObjekt
                {
                    { "art", PersonalRueckhol }, { "rueckhol", Prozent("rueckhol:%d:%s", tag, person) }, { "id", person },
                    { "name", name }, { "firma", rivale }, { "plus", plus }, { "text", text },
                },
            ];
        }
        return [];
    }

    /// <summary>_recall_decision</summary>
    internal static PyObjekt RueckholEntscheidung(Spielstand stand, PyObjekt eintrag, PyObjekt inhalt)
    {
        var regel = OderLeer(RivalRegeln(inhalt)["rueckhol"]);
        var daten = Objekt(stand.Personal[eintrag["id"]]);
        var vorname = KurzName(Get(eintrag, "name", ""));
        var gehalt = Py.Int(Get(daten, "gehalt", 0L));
        var neu = RueckholGehaltNeu(gehalt, regel);
        var praemie = RueckholPraemie(gehalt, regel);
        var firmaId = Py.Wahr(eintrag["firma"]) ? eintrag["firma"] : Bitweiche;
        var firma = Mitbewerber(firmaId, inhalt)["kurz"];
        return new PyObjekt
        {
            { "id", eintrag["rueckhol"] }, { "art", EntscheidungRueckhol }, { "person", eintrag["id"] }, { "name", eintrag["name"] },
            { "firma", firmaId }, { "gehalt_neu", neu }, { "bonus", praemie },
            { "titel", Prozent("%s will %s zurück", firma, eintrag["name"]) },
            { "text", Py.Str(eintrag["text"]) + " Hältst du dagegen?" },
            { "optionen", new List<object?>
                {
                    new PyObjekt
                    {
                        { "id", RueckholGehalt }, { "label", "Gehalt erhöhen" }, { "problem", "" },
                        { "folge", Prozent("%s bleibt. Gehalt dauerhaft +%d %% (%s statt %s pro Arbeitstag), Stimmung %s.", vorname,
                            Get(regel, "gehalt_plus", 10L), Wohnung.GanzeEuro(neu), Wohnung.GanzeEuro(gehalt), MitVorzeichen(Get(regel, "gehalt_stimmung", 0L))) },
                    },
                    new PyObjekt
                    {
                        { "id", RueckholBonus }, { "label", "Halteprämie zahlen" },
                        { "folge", Prozent("%s bleibt. Einmalig %s (%d Tagesgehälter), Stimmung %s.", vorname, Wohnung.GanzeEuro(praemie),
                            Py.Int(Get(regel, "bonus_tage", 10L)), MitVorzeichen(Get(regel, "bonus_stimmung", 0L))) },
                        { "problem", stand.Geld >= praemie ? "" : "Für die Halteprämie reicht dein Kontostand nicht." },
                    },
                    new PyObjekt
                    {
                        { "id", RueckholGehen }, { "label", "Ziehen lassen" }, { "problem", "" },
                        { "folge", Prozent("%s arbeitet heute noch mit und geht nach dem Feierabend zurück zu %s. Der Platz wird frei.", vorname, firma) },
                    },
                }
            },
        };
    }

    /// <summary>recall_choice_text: wie du auf ein Rueckhol-Angebot reagiert hast.</summary>
    public static string RueckholWahlText(PyObjekt daten, PyObjekt inhalt)
    {
        var firma = Mitbewerber(Py.Wahr(daten["firma"]) ? daten["firma"] : Bitweiche, inhalt)["kurz"];
        var name = KurzName(Get(daten, "name", ""));
        if (Py.Gleich(daten["wahl"], RueckholGehalt))
        {
            return Prozent("%s wollte %s zurück. Du hast das Gehalt auf %s erhöht.", firma, name, Wohnung.GanzeEuro(Get(daten, "gehalt", 0L)));
        }
        if (Py.Gleich(daten["wahl"], RueckholBonus))
        {
            return Prozent("%s wollte %s zurück. Du hast eine Halteprämie von %s gezahlt.", firma, name, Wohnung.GanzeEuro(-Py.Int(Get(daten, "geld", 0L))));
        }
        return Prozent("%s hat das Angebot von %s angenommen.", name, firma);
    }

    /// <summary>_decided_offers: alle Angebote als [(tag, id, gewonnen)], sortiert.</summary>
    private static List<PyTupel> EntschiedeneAngebote(Spielstand stand)
    {
        var eintraege = stand.Angebote.Values.Concat(stand.ProjektAngebote.Values).Select(Objekt).Select(e => new PyTupel(
            Py.Wahr(e["tag"]) ? Py.Int(e["tag"]) : 0L,
            Py.Str(Py.Wahr(e["anfrage"]) ? e["anfrage"] : Py.Wahr(e["projekt"]) ? e["projekt"] : ""),
            Py.Wahr(e["gewonnen"]))).ToList();
        return [.. eintraege.OrderBy(t => (long)t[0]!).ThenBy(t => (string)t[1]!, StringComparer.Ordinal).ThenBy(t => (bool)t[2]!)];
    }

    private static PyObjekt KeinGegenwind() => new()
    {
        { "aktiv", false }, { "gewonnen", 0L }, { "angebote", 0L }, { "minus", 0L }, { "zweiter", 0.0 }, { "faktor", 1.0 }, { "seit", 0L },
    };

    /// <summary>rivalry_pressure: Gegenwind der Mitbewerber an einem Arbeitstag (mit Zwischenspeicher wie Python).</summary>
    public static PyObjekt Gegenwind(Spielstand stand, long tag, PyObjekt inhalt)
    {
        var regel = RivalRegeln(inhalt)["gegenwind"] as PyObjekt;
        if (!Py.Wahr(regel) || !Py.Wahr(stand.Firma) || !Py.Wahr(stand.Erleichterung("gegenwind", true)))
        {
            return KeinGegenwind();
        }
        var stempel = (long)(stand.Angebote.Count + stand.ProjektAngebote.Count);
        var speicher = stand.Druck;
        if (!Py.Gleich(speicher.Get("stand"), stempel))
        {
            speicher.Clear();
            speicher["stand"] = stempel;
            speicher["angebote"] = EntschiedeneAngebote(stand);
        }
        if (speicher.ContainsKey(tag))
        {
            return Objekt(speicher[tag]);
        }
        var angebote = (List<PyTupel>)speicher["angebote"]!;
        var gegruendet = Py.Int(Objekt(stand.Firma)["tag"]);
        var seit = 0L;
        var bekannt = speicher.Keys.OfType<long>().Where(k => k < tag).ToList();
        var beginn = bekannt.Count > 0 ? bekannt.Max() + 1 : gegruendet;
        if (bekannt.Count > 0)
        {
            seit = Py.Int(Objekt(speicher[bekannt.Max()])["seit"]);
        }
        var ergebnis = KeinGegenwind();
        var fenster = (int)Py.Int(regel!["fenster"]);
        for (var jetzt = beginn; jetzt <= Math.Max(beginn, tag); jetzt++)
        {
            var vorher = angebote.Where(a => (long)a[0]! < jetzt).ToList();
            var auswahl = fenster == 0 ? vorher : vorher.Skip(Math.Max(0, vorher.Count - fenster)).ToList();
            var gewonnen = (long)auswahl.Count(a => (bool)a[2]!);
            var heiss = auswahl.Count >= Py.Int(regel["mindestens"]) && gewonnen * 100 >= Py.F(regel["ab_quote"]) * auswahl.Count;
            if (heiss && seit == 0)
            {
                seit = jetzt;
            }
            else if (!heiss && seit != 0 && jetzt - seit >= Py.Int(Get(regel, "mindest_tage", 0L)))
            {
                seit = 0;
            }
            var aktiv = seit != 0;
            ergebnis = new PyObjekt
            {
                { "aktiv", aktiv }, { "gewonnen", gewonnen }, { "angebote", (long)auswahl.Count },
                { "minus", aktiv ? Py.Int(regel["zuschlag_minus"]) : 0L },
                { "zweiter", aktiv ? Py.F(Get(regel, "zweiter_bieter", 0L)) : 0.0 },
                { "faktor", aktiv ? Py.F(Get(regel, "vorteil_faktor", 1L)) : 1.0 }, { "seit", seit },
            };
            speicher[jetzt] = ergebnis;
        }
        return speicher.Get(tag) as PyObjekt ?? ergebnis;
    }

    /// <summary>pressure_text: "Gegenwind: ..." (leer ohne Gegenwind).</summary>
    public static string GegenwindText(PyObjekt druck)
    {
        if (!Py.Wahr(druck["aktiv"]))
        {
            return "";
        }
        var text = Prozent("Gegenwind: Du hast %d deiner letzten %d Angebote gewonnen. Alle Mitbewerber "
            + "bieten deshalb gezielt %d Punkte günstiger gegen dich.", druck["gewonnen"], druck["angebote"], druck["minus"]);
        var faktor = Py.F(Get(druck, "faktor", 1L));
        if (faktor < 1)
        {
            text += faktor == 0.5 ? " Dein Preisvorteil zählt so lange nur halb."
                : Prozent(" Dein Preisvorteil zählt so lange nur zu %d %%.", PythonRunden.Runde(faktor * 100));
        }
        return text;
    }

    /// <summary>_phase_list: Konjunkturphasen ab markt.ab_tag bis mindestens bis (von, bis ausschliesslich, phase).</summary>
    private static List<(long Von, long Bis, string Art)> PhasenListe(string start, long bis, PyObjekt inhalt, bool ruhig = false)
    {
        var regel = MarktRegeln(inhalt);
        var aufbau = OderLeer(regel["konjunktur"]);
        var phasen = OderLeer(aufbau["phasen"]);
        if (phasen.Count == 0 || !Py.Hat(phasen, "neutral"))
        {
            return [];
        }
        var tag = Py.Int(Get(regel, "ab_tag", 101L));
        var ergebnis = new List<(long, long, string)>();
        var art = "neutral";
        var nummer = 0L;
        var auf = new[] { "aufschwung", "abschwung" }.Where(k => Py.Hat(phasen, k) && !(ruhig && k == "abschwung")).ToList();
        var schwung = "";
        while (tag <= bis)
        {
            var spanne = Liste(nummer == 0 ? aufbau["start_tage"] : Objekt(phasen[art])["tage"]);
            var laenge = Math.Max(1, Zwischen(start, nummer, "konjunktur-dauer", Py.Int(spanne[0]), Py.Int(spanne[1])));
            ergebnis.Add((tag, tag + laenge, art));
            tag += laenge;
            nummer++;
            if (art != "neutral")
            {
                schwung = art;
                art = "neutral";
            }
            else if (auf.Count == 0)
            {
                break;
            }
            else if (schwung.Length > 0 && auf.Count == 2)
            {
                // Meist folgt auf einen Abschwung ein Aufschwung und umgekehrt
                var anders = schwung == auf[0] ? auf[1] : auf[0];
                art = Wurf(start, nummer, "konjunktur-art") * 100 < Py.F(Get(aufbau, "wechsel_chance", 50L)) ? anders : schwung;
            }
            else
            {
                art = Waehle(auf, start, nummer, "konjunktur-art");
            }
        }
        return ergebnis;
    }

    /// <summary>_trend_list: Technologietrends (von, bis ausschliesslich, trend).</summary>
    private static List<(long Von, long Bis, PyObjekt Trend)> TrendListe(string start, long bis, PyObjekt inhalt)
    {
        var regel = MarktRegeln(inhalt);
        var aufbau = OderLeer(regel["trends"]);
        var trends = ListeOderLeer(aufbau, "liste").Select(Objekt).ToList();
        if (trends.Count == 0)
        {
            return [];
        }
        var erst = Liste(aufbau["erster_nach"]);
        var tag = Py.Int(Get(regel, "ab_tag", 101L)) + Zwischen(start, 0, "trend-erster", Py.Int(erst[0]), Py.Int(erst[1]));
        var ergebnis = new List<(long, long, PyObjekt)>();
        object? zuletzt = null;
        var nummer = 0L;
        var dauer = Liste(aufbau["dauer"]);
        var pause = Liste(aufbau["pause"]);
        while (tag <= bis)
        {
            var auswahl = trends.Where(t => !Py.Gleich(t["id"], zuletzt)).ToList();
            if (auswahl.Count == 0)
            {
                auswahl = trends;
            }
            var trend = Waehle(auswahl, start, nummer, "trend-art");
            var laenge = Math.Max(1, Zwischen(start, nummer, "trend-dauer", Py.Int(dauer[0]), Py.Int(dauer[1])));
            ergebnis.Add((tag, tag + laenge, trend));
            zuletzt = trend["id"];
            tag += laenge + Zwischen(start, nummer, "trend-pause", Py.Int(pause[0]), Py.Int(pause[1]));
            nummer++;
        }
        return ergebnis;
    }

    private static PyObjekt KeinePhase() => new() { { "name", "" }, { "anfragen", 0L }, { "zuschlag", 0L }, { "umsatz", 0L } };

    /// <summary>market_state: Konjunktur und Trend an einem Arbeitstag.</summary>
    public static PyObjekt Marktlage(Spielstand stand, long tag, PyObjekt inhalt)
    {
        var ergebnis = new PyObjekt
        {
            { "phase", "" }, { "phase_info", KeinePhase() }, { "phase_von", 0L }, { "phase_bis", 0L },
            { "trend", null }, { "trend_von", 0L }, { "trend_bis", 0L },
        };
        var regel = MarktRegeln(inhalt);
        if (!Py.Wahr(stand.Firma) || regel.Count == 0 || tag < Py.Int(Get(regel, "ab_tag", 101L)))
        {
            return ergebnis;
        }
        var start = stand.FirmenStartwert;
        foreach (var (von, bis, art) in PhasenListe(start, tag, inhalt, !Py.Wahr(stand.Erleichterung("abschwung", true))))
        {
            if (von <= tag && tag < bis)
            {
                Py.Setzen(ergebnis, "phase", art);
                Py.Setzen(ergebnis, "phase_info", Objekt(Objekt(Objekt(regel["konjunktur"])["phasen"])[art]));
                Py.Setzen(ergebnis, "phase_von", von);
                Py.Setzen(ergebnis, "phase_bis", bis);
            }
        }
        foreach (var (von, bis, trend) in TrendListe(start, tag, inhalt))
        {
            if (von <= tag && tag < bis)
            {
                Py.Setzen(ergebnis, "trend", trend);
                Py.Setzen(ergebnis, "trend_von", von);
                Py.Setzen(ergebnis, "trend_bis", bis);
            }
        }
        return ergebnis;
    }

    /// <summary>market_status: kleine Anzeige im Firmenbereich [(ueberschrift, text)].</summary>
    public static List<(string Kopf, string Text)> MarktStand(Spielstand stand, PyObjekt inhalt)
    {
        var jetzt = Marktlage(stand, stand.Tag, inhalt);
        var zeilen = new List<(string, string)>();
        if (Py.Wahr(jetzt["phase"]))
        {
            var info = Objekt(jetzt["phase_info"]);
            var wirkung = new List<string>();
            if (Py.Wahr(info["anfragen"]))
            {
                wirkung.Add(Prozent("%s Anfrage%s pro Tag", MitVorzeichen(info["anfragen"]), Math.Abs(Py.Int(info["anfragen"])) == 1 ? "" : "n"));
            }
            if (Py.Wahr(info["zuschlag"]))
            {
                wirkung.Add(Prozent("Marktpreise %s Punkte", MitVorzeichen(info["zuschlag"])));
            }
            if (Py.Wahr(info["umsatz"]))
            {
                wirkung.Add(Prozent("Routineumsatz %s", MitVorzeichen(info["umsatz"], " %")));
            }
            zeilen.Add((Prozent("Konjunktur: %s", info["name"]), wirkung.Count > 0 ? string.Join(" · ", wirkung) : "Anfragen und Preise normal"));
        }
        if (jetzt["trend"] is PyObjekt trend && Py.Wahr(trend))
        {
            var fach = trend["cat"];
            zeilen.Add((Prozent("Trend: %s", trend["name"]), Prozent("Noch %s · dazu Trend-Anfragen (%s)",
                TageText(Py.Int(jetzt["trend_bis"]) - stand.Tag),
                fach is string f && T.Name.TryGetValue(f, out var n) ? n : Get(trend, "cat", ""))));
        }
        var druck = GegenwindText(Gegenwind(stand, stand.Tag, inhalt));
        if (druck.Length > 0)
        {
            var stelle = druck.IndexOf(": ", StringComparison.Ordinal);
            zeilen.Add(("Gegenwind der Mitbewerber", druck[(stelle + 2)..]));
        }
        return zeilen;
    }

    /// <summary>market_events: was sich am Markt geaendert hat [(tag, art, titel, text)].</summary>
    public static List<(long Tag, string Art, string Titel, object? Text)> MarktEreignisse(Spielstand stand, PyObjekt inhalt)
    {
        if (!Py.Wahr(stand.Firma))
        {
            return [];
        }
        var regel = MarktRegeln(inhalt);
        var gegruendet = Py.Int(Objekt(stand.Firma)["tag"]);
        var ergebnis = new List<(long Tag, string Art, string Titel, object? Text)>();
        if (regel.Count > 0)
        {
            var start = stand.FirmenStartwert;
            var ab = Py.Int(Get(regel, "ab_tag", 101L));
            foreach (var (tag, _, art) in PhasenListe(start, stand.Tag, inhalt, !Py.Wahr(stand.Erleichterung("abschwung", true))))
            {
                if (tag <= gegruendet || (tag == ab && art == "neutral"))
                {
                    continue;
                }
                var info = Objekt(Objekt(Objekt(regel["konjunktur"])["phasen"])[art]);
                ergebnis.Add((tag, "konjunktur_wechsel", Prozent("Konjunktur: %s", info["name"]), Get(info, "text", "")));
            }
            foreach (var (tag, ende, trend) in TrendListe(start, stand.Tag, inhalt))
            {
                if (ende <= gegruendet)
                {
                    continue;
                }
                // Lief der Trend schon bei der Gruendung, steht er am Gruendungstag
                ergebnis.Add((Math.Max(tag, gegruendet), "trend_gestartet", Prozent("Trend: %s", trend["name"]), Get(trend, "start", "")));
                if (ende <= stand.Tag)
                {
                    ergebnis.Add((ende, "trend_beendet", Prozent("Trend vorbei: %s", trend["name"]), Get(trend, "ende", "")));
                }
            }
        }
        if (RivalRegeln(inhalt)["gegenwind"] is PyObjekt wind && Py.Wahr(wind))
        {
            var vorher = false;
            for (var tag = gegruendet + 1; tag <= stand.Tag; tag++)
            {
                var jetzt = Py.Wahr(Gegenwind(stand, tag, inhalt)["aktiv"]);
                if (jetzt != vorher)
                {
                    ergebnis.Add((tag, jetzt ? "wettbewerb_verschaerft" : "wettbewerb_entspannt",
                        jetzt ? "Gegenwind der Mitbewerber" : "Gegenwind vorbei", Get(wind, jetzt ? "text_start" : "text_ende", "")));
                }
                vorher = jetzt;
            }
        }
        return [.. ergebnis.OrderBy(e => e.Tag)];
    }

    /// <summary>market_news: Story-Moment am Morgen, was sich heute am Markt geaendert hat.</summary>
    public static string MarktNachricht(Spielstand stand, PyObjekt inhalt)
    {
        var zeilen = MarktEreignisse(stand, inhalt).Where(e => e.Tag == stand.Tag && Py.Wahr(e.Text)).Select(e => Py.Str(e.Text)).ToList();
        var regel = MarktRegeln(inhalt);
        if (regel.Count > 0 && stand.Tag == Py.Int(Get(regel, "ab_tag", 101L)) && Py.Int(Objekt(stand.Firma)["tag"]) < stand.Tag
            && Py.Wahr(regel["start_text"]))
        {
            zeilen.Insert(0, Py.Str(regel["start_text"]));
        }
        return string.Join("\n", zeilen);
    }
}
