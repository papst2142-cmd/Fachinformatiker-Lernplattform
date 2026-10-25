using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: Geld der eigenen Firma wie <c>fisi_game.py</c> (Python 0.63.2, Z. 7556-8467):
/// Kredite (Zins, Annuitaet, Tilgungsplan, Bonitaet, Ablösung, Mahnstufe), Umsatzsteuer
/// (Zeitraum, Zahllast, Ruecklage), Werbung und Zertifizierungen. Potenzen nur mit
/// <see cref="PythonPow"/>, Rundungen mit <see cref="PythonRunden"/>. Stoppgrenze (Weisung P7-4).
/// </summary>
public static class Wirtschaft
{
    public const string KreditPrefix = "kredit:";   // LOAN_PREFIX
    public const string FreierKredit = "frei";      // FREE_LOAN
    public const string WerbungLaufend = "laufend"; // AD_RUNNING
    public const string WerbungEinmalig = "einmalig"; // AD_ONCE
    public const string GrossPrefix = "gross:";     // GROSS_PREFIX

    /// <summary>AD_MODES: Knopftexte als Handlung (ab 0.63.2, B-D-10).</summary>
    public static readonly IReadOnlyList<(string Art, string Label)> WerbeArten = [(WerbungLaufend, "Laufend buchen"), (WerbungEinmalig, "Einmalig buchen")];

    /// <summary>TAX_INCOME: Einnahmen mit Umsatzsteuer.</summary>
    public static readonly IReadOnlyList<string> SteuerEinnahmen =
        [Ereignisse.BuchUmsatz, Ereignisse.BuchKundentickets, Ereignisse.BuchAngebote, Ereignisse.BuchProjekte];

    /// <summary>TAX_INPUT: Ausgaben mit Vorsteuer.</summary>
    public static readonly IReadOnlyList<string> SteuerVorsteuer =
        [Ereignisse.BuchNebenkosten, Ereignisse.BuchMaterial, Ereignisse.BuchAusbau, Ereignisse.BuchWeiterbildung, Ereignisse.BuchWerbung, Ereignisse.BuchZertifikat];

    private static string Euro(object? wert) => Wohnung.GanzeEuro(wert);

    // -- Kredite (ab 0.41) -----------------------------------------------------

    /// <summary>loan_rules</summary>
    public static PyObjekt KreditRegeln(PyObjekt inhalt) => Objekt(Objekt(inhalt["balancing"])["kredite"]);

    /// <summary>loan_terms: erlaubte Laufzeiten in Arbeitstagen (aufsteigend).</summary>
    public static List<long> Laufzeiten(PyObjekt inhalt) =>
        [.. Objekt(Objekt(KreditRegeln(inhalt)["zins"])["basis"]).Select(p => Py.Int(p.Key)).Order()];

    /// <summary>loan_rate_pa: Grundzins nach Laufzeit plus Aufschlag fuer grosse Summen.</summary>
    public static double JahresZins(long summe, long laufzeit, PyObjekt inhalt)
    {
        var regel = Objekt(KreditRegeln(inhalt)["zins"]);
        var basis = Py.F(Objekt(regel["basis"])[laufzeit.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        var schritte = Betrieb.PyDiv(Math.Max(0, summe - Py.Int(regel["groesse_ab"])), Py.Int(regel["groesse_schritt"]));
        return PythonRundenStellen.Runde(basis + schritte * Py.F(regel["groesse_plus"]), 2);
    }

    /// <summary>day_interest: Zinssatz pro Arbeitstag als Bruchteil.</summary>
    public static double TagesZins(object? jahresZins, PyObjekt inhalt) =>
        Py.F(jahresZins) / 100.0 / Py.F(KreditRegeln(inhalt)["tage_pro_jahr"]);

    /// <summary>annuity: gleichbleibende Rate pro Arbeitstag (Zins + Tilgung), aufgerundet.</summary>
    public static long Annuitaet(long summe, object? jahresZins, long laufzeit, PyObjekt inhalt)
    {
        var zins = TagesZins(jahresZins, inhalt);
        if (zins <= 0)
        {
            return (long)Math.Ceiling(summe / (double)laufzeit);
        }
        return (long)Math.Ceiling(summe * zins / (1.0 - PythonPow.Pow(1.0 + zins, -laufzeit)));
    }

    /// <summary>dunning_step: Regeln der Mahnstufe (1 bis 3) oder null bei Stufe 0.</summary>
    public static PyObjekt? MahnStufe(long stufe, PyObjekt inhalt)
    {
        var stufen = Liste(Objekt(KreditRegeln(inhalt)["mahnung"])["stufen"]);
        return stufe > 0 ? Objekt(stufen[(int)Math.Min(stufe, stufen.Count) - 1]) : null;
    }

    /// <summary>loan_interest: aktueller Zinssatz eines Kredits (vereinbart plus Mahnaufschlag).</summary>
    public static double KreditZins(PyObjekt kredit, long mahnstufe, PyObjekt inhalt)
    {
        var stufe = MahnStufe(mahnstufe, inhalt);
        return PythonRundenStellen.Runde(Py.F(kredit["zins"]) + (stufe is not null ? Py.F(Get(stufe, "zins_plus", 0L)) : 0), 2);
    }

    /// <summary>loan_due: Rate beim naechsten Feierabend (die letzte ist meist kleiner).</summary>
    public static long KreditFaellig(PyObjekt kredit, long mahnstufe, PyObjekt inhalt)
    {
        var zinsen = PythonRunden.Runde(Py.F(kredit["rest"]) * TagesZins(KreditZins(kredit, mahnstufe, inhalt), inhalt));
        return Math.Min(Py.Int(kredit["rate"]), Py.Int(kredit["rest"]) + Py.Int(kredit["zins_offen"]) + Py.Int(kredit["gebuehren"]) + zinsen);
    }

    /// <summary>loan_payoff: Restschuld, offene Zinsen und Gebuehren plus Vorfaelligkeit.</summary>
    public static PyObjekt KreditAbloese(PyObjekt kredit, PyObjekt inhalt)
    {
        var gebuehr = (long)Math.Ceiling(Py.F(kredit["rest"]) * Py.F(KreditRegeln(inhalt)["abloesen_prozent"]) / 100.0);
        var ergebnis = new PyObjekt
        {
            { "tilgung", kredit["rest"] }, { "zinsen", kredit["zins_offen"] }, { "gebuehren", Py.Int(kredit["gebuehren"]) + gebuehr }, { "vorfaelligkeit", gebuehr },
        };
        ergebnis.Add("gesamt", Py.Int(ergebnis["tilgung"]) + Py.Int(ergebnis["zinsen"]) + Py.Int(ergebnis["gebuehren"]));
        return ergebnis;
    }

    /// <summary>loan_schedule: Tilgungsplan ab einem Tag, wenn jede Rate klappt.</summary>
    public static List<PyObjekt> TilgungsPlan(PyObjekt kredit, long startTag, long mahnstufe, PyObjekt inhalt, int grenze = 1000)
    {
        var (rest, zinsOffen, gebuehren) = (Py.Int(kredit["rest"]), Py.Int(kredit["zins_offen"]), Py.Int(kredit["gebuehren"]));
        var tagesZins = TagesZins(KreditZins(kredit, mahnstufe, inhalt), inhalt);
        var zeilen = new List<PyObjekt>();
        var tag = startTag;
        while ((rest > 0 || zinsOffen > 0 || gebuehren > 0) && zeilen.Count < grenze)
        {
            zinsOffen += PythonRunden.Runde(rest * tagesZins);
            var faellig = Math.Min(Py.Int(kredit["rate"]), rest + zinsOffen + gebuehren);
            var bezahltGebuehren = Math.Min(faellig, gebuehren);
            var bezahltZinsen = Math.Min(faellig - bezahltGebuehren, zinsOffen);
            var tilgung = faellig - bezahltGebuehren - bezahltZinsen;
            gebuehren -= bezahltGebuehren;
            zinsOffen -= bezahltZinsen;
            rest -= tilgung;
            zeilen.Add(new PyObjekt
            {
                { "tag", tag }, { "rate", faellig }, { "zinsen", bezahltZinsen }, { "tilgung", tilgung }, { "gebuehren", bezahltGebuehren }, { "rest", Math.Max(0, rest) },
            });
            if (faellig <= 0)
            {
                break;
            }
            tag += 1;
        }
        return zeilen;
    }

    /// <summary>loan_offer_numbers: Zins, Rate und Kosten eines neuen Kredits (ohne Bonitaet).</summary>
    public static PyObjekt KreditZahlen(long summe, long laufzeit, PyObjekt inhalt)
    {
        var jahresZins = JahresZins(summe, laufzeit, inhalt);
        var rate = Annuitaet(summe, jahresZins, laufzeit, inhalt);
        var kredit = new PyObjekt { { "rest", summe }, { "zins_offen", 0L }, { "gebuehren", 0L }, { "rate", rate }, { "zins", jahresZins } };
        var zinsen = TilgungsPlan(kredit, 1, 0, inhalt).Sum(z => Py.Int(z["zinsen"]));
        return new PyObjekt
        {
            { "summe", summe }, { "laufzeit", laufzeit }, { "zins", jahresZins }, { "rate", rate }, { "zinsen_gesamt", zinsen }, { "gesamt", summe + zinsen },
        };
    }

    private static readonly IReadOnlyList<string> UmsatzArten = SteuerEinnahmen;

    /// <summary>firm_income_recent: Umsatz der letzten abgeschlossenen Arbeitstage (ohne Kredite).</summary>
    public static long UmsatzZuletzt(Spielstand stand, long tage)
    {
        var erster = stand.Tag - tage;
        long summe = 0;
        foreach (var (tag, eintrag) in stand.Kassenbuch)
        {
            if (erster <= Py.Int(tag) && Py.Int(tag) < stand.Tag)
            {
                summe += Objekt(Objekt(eintrag)["ein"]).Where(p => UmsatzArten.Contains(p.Key)).Sum(p => Py.Int(p.Value));
            }
        }
        return summe;
    }

    private static readonly IReadOnlyList<string> KostenArten =
        [Ereignisse.BuchGehaelter, Ereignisse.BuchNebenkosten, Ereignisse.BuchMaterial, Ereignisse.BuchWerbung, Ereignisse.BuchSteuer];

    /// <summary>firm_profit_recent: durchschnittlicher Gewinn pro Arbeitstag vor Kreditkosten.</summary>
    public static double GewinnZuletzt(Spielstand stand, long tage)
    {
        var erster = stand.Tag - tage;
        long summe = 0;
        foreach (var (tag, eintrag) in stand.Kassenbuch)
        {
            if (erster <= Py.Int(tag) && Py.Int(tag) < stand.Tag)
            {
                var ein = Objekt(Objekt(eintrag)["ein"]);
                summe += ein.Where(p => UmsatzArten.Contains(p.Key)).Sum(p => Py.Int(p.Value));
                summe -= Objekt(Objekt(eintrag)["aus"]).Where(p => KostenArten.Contains(p.Key)).Sum(p => Py.Int(p.Value));
                summe += Py.Int(Get(ein, Ereignisse.BuchSteuer, 0L));
            }
        }
        return summe / (double)Math.Max(1, tage);
    }

    /// <summary>rate_limit: (hoechstens tragbare Raten pro Arbeitstag, davon schon belegt).</summary>
    public static (long Hoechstens, long Belegt) RatenGrenze(Spielstand stand, PyObjekt inhalt)
    {
        var regeln = KreditRegeln(inhalt);
        var gewinn = GewinnZuletzt(stand, Py.Int(Objekt(regeln["bonitaet"])["umsatz_tage"]));
        var oben = Math.Max(0, (long)Math.Truncate(gewinn * Py.F(Get(Objekt(regeln["rahmen"]), "raten_anteil", 1.0))));
        return (oben, stand.LaufendeKredite().Sum(k => Py.Int(k["rate"])));
    }

    /// <summary>credit_check: Bonitaet der Firma (ok, Punkte, Umsatz, Rahmen, frei, ab_tag, sperre, Raten).</summary>
    public static PyObjekt Bonitaet(Spielstand stand, PyObjekt inhalt)
    {
        var regeln = KreditRegeln(inhalt);
        var regel = Objekt(regeln["bonitaet"]);
        var ergebnis = new PyObjekt
        {
            { "ok", false }, { "punkte", new List<object?>() }, { "umsatz", 0L }, { "rahmen", 0L }, { "frei", 0L }, { "ab_tag", 0L },
            { "sperre", "" }, { "raten_max", 0L }, { "raten_belegt", 0L },
        };
        if (!Py.Wahr(stand.Firma))
        {
            Liste(ergebnis["punkte"]).Add(new PyObjekt { { "text", "Eigene Firma gegründet" }, { "ok", false } });
            return ergebnis;
        }
        var firma = Objekt(stand.Firma);
        var seit = stand.Tag - Py.Int(firma["tag"]);
        Py.Setzen(ergebnis, "ab_tag", Py.Int(firma["tag"]) + Py.Int(regel["firmentage"]));
        var umsatz = UmsatzZuletzt(stand, Py.Int(regel["umsatz_tage"]));
        Py.Setzen(ergebnis, "umsatz", umsatz);
        var punkte = new List<object?>
        {
            new PyObjekt { { "text", Prozent("Firma seit mindestens %d Arbeitstagen (jetzt %d)", regel["firmentage"], seit) }, { "ok", seit >= Py.Int(regel["firmentage"]) } },
            new PyObjekt { { "text", Prozent("Umsatz der letzten %d Arbeitstage mindestens %s (jetzt %s)", regel["umsatz_tage"], Euro(regel["mindest_umsatz"]), Euro(umsatz)) },
                { "ok", umsatz >= Py.Int(regel["mindest_umsatz"]) } },
            new PyObjekt { { "text", Prozent("Ansehen mindestens %d %% (jetzt %d %%)", regel["mindest_reputation"], PythonRunden.Runde(stand.MittlereReputation)) },
                { "ok", stand.MittlereReputation >= Py.F(regel["mindest_reputation"]) } },
        };
        var stufe = MahnStufe(stand.Mahnstufe, inhalt);
        if (stufe is not null)
        {
            var f = Py.F(Get(stufe, "rahmen_faktor", 0L));
            punkte.Add(new PyObjekt
            {
                { "text", Prozent("Keine Mahnstufe, die neue Kredite sperrt (jetzt: %s%s)", stufe["name"], f != 0 ? Prozent(", nur %d %% des Rahmens", PythonRunden.Runde(100 * f)) : "") },
                { "ok", f > 0 },
            });
        }
        Py.Setzen(ergebnis, "punkte", punkte);
        Py.Setzen(ergebnis, "ok", punkte.All(p => Py.Wahr(Objekt(p)["ok"])));
        var rahmen = Objekt(regeln["rahmen"]);
        var faktor = Py.F(rahmen["faktor_umsatz"]);
        var boni = Py.Wahr(rahmen["ansehen_bonus"]) ? Liste(rahmen["ansehen_bonus"]).Select(Objekt).OrderBy(b => -Py.F(b["ab"])).ToList() : [];
        foreach (var bonus in boni)
        {
            if (!Py.Kleiner(stand.MittlereReputation, bonus["ab"]))
            {
                faktor *= Py.F(bonus["faktor"]);
                break;
            }
        }
        if (stufe is not null)
        {
            faktor *= Py.F(Get(stufe, "rahmen_faktor", 0L));
            if (Py.F(Get(stufe, "rahmen_faktor", 0L)) == 0)
            {
                Py.Setzen(ergebnis, "sperre", Prozent("Bei „%s“ gibt dir die Bank keinen neuen Kredit. Die Stufe sinkt nach %d Arbeitstagen ohne geplatzte Rate.",
                    stufe["name"], Objekt(regeln["mahnung"])["abbau_tage"]));
            }
        }
        var schritt = Py.Int(rahmen["runden"]);
        var grenze = Math.Min(Py.Int(rahmen["hoechstens"]), Betrieb.PyDiv((long)Math.Truncate(umsatz * faktor), schritt) * schritt);
        Py.Setzen(ergebnis, "rahmen", Py.Wahr(ergebnis["ok"]) ? grenze : 0L);
        Py.Setzen(ergebnis, "frei", Math.Max(0, Py.Int(ergebnis["rahmen"]) - stand.LaufendeKredite().Sum(k => Py.Int(k["rest"]))));
        var (hoechstens, belegt) = RatenGrenze(stand, inhalt);
        Py.Setzen(ergebnis, "raten_max", hoechstens);
        Py.Setzen(ergebnis, "raten_belegt", belegt);
        return ergebnis;
    }

    /// <summary>credit_frame_text: "Tragbare Raten: ..." unter dem Kreditrahmen.</summary>
    public static string RahmenText(PyObjekt pruefung) => Prozent(
        "Tragbare Raten: höchstens %s pro Arbeitstag (die Hälfte des Gewinns der letzten Arbeitstage), davon belegt %s.",
        Euro(pruefung["raten_max"]), Euro(pruefung["raten_belegt"]));

    /// <summary>credit_wait_text: Hinweis, solange die Firma noch keinen Kredit bekommt.</summary>
    public static string WarteText(Spielstand stand, PyObjekt pruefung)
    {
        if (Py.Wahr(pruefung["sperre"]))
        {
            return "Gerade gibt dir die Bank keinen neuen Kredit. Die Bank prüft:";
        }
        if (Py.Wahr(stand.Firma) && stand.Tag < Py.Int(pruefung["ab_tag"]))
        {
            return Prozent("Noch bekommt deine Firma keinen Kredit. Frühestens ab Arbeitstag %d, wenn dann auch Umsatz und Ansehen reichen. Die Bank prüft:", pruefung["ab_tag"]);
        }
        return "Noch bekommt deine Firma keinen Kredit. Die Bank prüft:";
    }

    /// <summary>loan_offer: Angebot der Bank fuer Summe und Laufzeit (mit "problem", falls es nicht geht).</summary>
    public static PyObjekt KreditAngebot(Spielstand stand, object? summeWert, object? laufzeitWert, PyObjekt? paket, PyObjekt inhalt, PyObjekt? pruefung = null)
    {
        var regeln = KreditRegeln(inhalt);
        var (summe, laufzeit) = (Py.Int(summeWert), Py.Int(laufzeitWert));
        var problem = "";
        if (!Laufzeiten(inhalt).Contains(laufzeit))
        {
            return new PyObjekt { { "summe", summe }, { "laufzeit", laufzeit }, { "problem", "Diese Laufzeit bietet die Bank nicht an." } };
        }
        var angebot = KreditZahlen(summe, laufzeit, inhalt);
        pruefung ??= Bonitaet(stand, inhalt);
        var frei = Objekt(regeln["frei"]);
        if (!Py.Wahr(stand.Firma))
        {
            problem = "Kredite gibt es nur für die eigene Firma.";
        }
        else if (Py.Wahr(pruefung["sperre"]))
        {
            problem = Py.Str(pruefung["sperre"]);
        }
        else if (!Py.Wahr(pruefung["ok"]))
        {
            problem = "Deine Firma ist noch nicht kreditwürdig.";
        }
        else if (paket is null && (summe < Py.Int(frei["min"]) || PyMod(summe, Py.Int(frei["schritt"])) != 0))
        {
            problem = Prozent("Die Bank leiht mindestens %s, in Schritten von %s.", Euro(frei["min"]), Euro(frei["schritt"]));
        }
        else if (summe > Py.Int(pruefung["frei"]))
        {
            problem = Prozent("Dein Kreditrahmen reicht dafür nicht (frei: %s).", Euro(pruefung["frei"]));
        }
        else if (Py.Int(angebot["rate"]) + Py.Int(pruefung["raten_belegt"]) > Py.Int(pruefung["raten_max"]))
        {
            problem = Prozent("Die Rate ist zu hoch: Alle Raten zusammen dürfen höchstens %s pro Arbeitstag sein (die Hälfte deines Gewinns), %s sind schon belegt. Eine längere Laufzeit senkt die Rate.",
                Euro(pruefung["raten_max"]), Euro(pruefung["raten_belegt"]));
        }
        Py.Setzen(angebot, "problem", problem);
        Py.Setzen(angebot, "paket", paket is not null ? paket["id"] : FreierKredit);
        Py.Setzen(angebot, "name", paket is not null ? paket["name"] : "Freier Kredit " + Euro(summe));
        return angebot;
    }

    private static long PyMod(long a, long b) => ((a % b) + b) % b;

    /// <summary>loan_packages: die festen Kreditpakete mit Zins, Rate und ob sie gerade gehen.</summary>
    public static List<PyObjekt> KreditPakete(Spielstand stand, PyObjekt inhalt)
    {
        var pruefung = Bonitaet(stand, inhalt);
        var ergebnis = new List<PyObjekt>();
        foreach (var paket in Liste(KreditRegeln(inhalt)["pakete"]).Select(Objekt))
        {
            var angebot = KreditAngebot(stand, paket["summe"], paket["laufzeit"], paket, inhalt, pruefung);
            Py.Setzen(angebot, "text", Get(paket, "text", ""));
            ergebnis.Add(angebot);
        }
        return ergebnis;
    }

    /// <summary>free_loan_limits: (kleinste, groesste, Schritt) fuer die frei waehlbare Summe.</summary>
    public static (long Kleinste, long Groesste, long Schritt) FreiGrenzen(Spielstand stand, PyObjekt inhalt)
    {
        var regel = Objekt(KreditRegeln(inhalt)["frei"]);
        var pruefung = Bonitaet(stand, inhalt);
        var schritt = Py.Int(regel["schritt"]);
        var oben = Betrieb.PyDiv(Py.Int(pruefung["frei"]), schritt) * schritt;
        return (Py.Int(regel["min"]), Math.Max(Py.Int(regel["min"]), oben), schritt);
    }

    /// <summary>clamp_loan_amount</summary>
    public static long SummeBegrenzen(Spielstand stand, object? summe, PyObjekt inhalt)
    {
        var (unten, oben, schritt) = FreiGrenzen(stand, inhalt);
        return Math.Max(unten, Math.Min(oben, Betrieb.PyDiv(Py.Int(summe), schritt) * schritt));
    }

    /// <summary>percent_text: 8.5 -> "8,5 %", 10.0 -> "10 %"; nan und inf als "–".</summary>
    public static string ProzentText(object? wert)
    {
        var w = Py.F(wert);
        if (!double.IsFinite(w))
        {
            return "–";
        }
        var text = Fest(w, 2).TrimEnd('0').TrimEnd('.');
        if (text == "-0")
        {
            text = "0";
        }
        return text.Replace(".", ",", StringComparison.Ordinal) + " %";
    }

    /// <summary>term_text</summary>
    public static string LaufzeitText(object? laufzeit) => Py.Int(laufzeit) == 1 ? "1 Arbeitstag" : Prozent("%d Arbeitstage", Py.Int(laufzeit));

    /// <summary>loan_offer_text: eine Zeile zu einem Angebot.</summary>
    public static string AngebotText(PyObjekt angebot) => Prozent("%s über %s · %s p. a. · Rate %s pro Arbeitstag · Zinsen gesamt %s",
        Euro(angebot["summe"]), LaufzeitText(angebot["laufzeit"]), ProzentText(angebot["zins"]), Euro(angebot["rate"]), Euro(angebot["zinsen_gesamt"]));

    /// <summary>loan_confirm_text</summary>
    public static string KreditFrage(PyObjekt angebot, PyObjekt inhalt)
    {
        var was = Py.Gleich(angebot["paket"], FreierKredit) ? "Einen Kredit über " + Euro(angebot["summe"])
            : Prozent("„%s“ über %s", angebot["name"], Euro(angebot["summe"]));
        return Prozent("%s aufnehmen?\n\nBank: %s, Laufzeit %s, Zins %s p. a. Jeden Feierabend werden automatisch %s abgebucht (Zins und Tilgung). "
            + "Zurück zahlst du insgesamt %s, davon %s Zinsen.\n\nReicht das Konto beim Feierabend nicht, platzt die Rate: Mahnstufe, Gebühr und weniger Ansehen.",
            was, KreditRegeln(inhalt)["bank"], LaufzeitText(angebot["laufzeit"]), ProzentText(angebot["zins"]), Euro(angebot["rate"]),
            Euro(angebot["gesamt"]), Euro(angebot["zinsen_gesamt"]));
    }

    /// <summary>loan_status: Anzeige eines laufenden Kredits mit Tilgungsplan ab heute.</summary>
    public static PyObjekt KreditStand(Spielstand stand, PyObjekt kredit, PyObjekt inhalt)
    {
        var zeilen = TilgungsPlan(kredit, stand.Tag, stand.Mahnstufe, inhalt);
        return new PyObjekt
        {
            { "id", kredit["id"] }, { "name", kredit["name"] }, { "summe", kredit["summe"] }, { "tag", kredit["tag"] },
            { "rest", Py.Int(kredit["rest"]) + Py.Int(kredit["zins_offen"]) + Py.Int(kredit["gebuehren"]) }, { "tilgung_offen", kredit["rest"] },
            { "zins", KreditZins(kredit, stand.Mahnstufe, inhalt) }, { "zins_vereinbart", kredit["zins"] }, { "rate", kredit["rate"] },
            { "naechste_rate", zeilen.Count > 0 ? zeilen[0]["rate"] : 0L }, { "raten_bezahlt", kredit["raten"] }, { "raten_offen", (long)zeilen.Count },
            { "ausfaelle", kredit["ausfaelle"] }, { "gebuehren", kredit["gebuehren"] }, { "bezahlt_zins", kredit["bezahlt_zins"] },
            { "bezahlt_tilgung", kredit["bezahlt_tilgung"] }, { "bezahlt_gebuehren", kredit["bezahlt_gebuehren"] },
            { "zinsen_noch", zeilen.Sum(z => Py.Int(z["zinsen"])) }, { "letzter_tag", zeilen.Count > 0 ? zeilen[^1]["tag"] : stand.Tag },
            { "plan", zeilen.Cast<object?>().ToList() }, { "abloesen", KreditAbloese(kredit, inhalt) },
        };
    }

    /// <summary>loan_status_lines: Zeilen zu einem laufenden Kredit.</summary>
    public static List<string> KreditZeilen(PyObjekt e)
    {
        var zeilen = new List<string>
        {
            Prozent("Restschuld %s von %s · Zins %s p. a.%s", Euro(e["rest"]), Euro(e["summe"]), ProzentText(e["zins"]),
                Py.Kleiner(e["zins_vereinbart"], e["zins"]) ? " (mit Mahnaufschlag)" : ""),
            Prozent("Rate %s pro Arbeitstag · %d von %d Raten bezahlt · noch %s (bis Arbeitstag %d)", Euro(e["rate"]), e["raten_bezahlt"],
                Py.Plus(e["raten_bezahlt"], e["raten_offen"]), LaufzeitText(e["raten_offen"]), e["letzter_tag"]),
            Prozent("Bisher bezahlt: Zinsen %s · Tilgung %s%s", Euro(e["bezahlt_zins"]), Euro(e["bezahlt_tilgung"]),
                Py.Wahr(e["bezahlt_gebuehren"]) ? " · Gebühren " + Euro(e["bezahlt_gebuehren"]) : ""),
        };
        if (Py.Wahr(e["ausfaelle"]))
        {
            zeilen.Add(Prozent("Geplatzte Raten: %d%s", e["ausfaelle"], Py.Wahr(e["gebuehren"]) ? " · offene Gebühren " + Euro(e["gebuehren"]) : ""));
        }
        return zeilen;
    }

    /// <summary>loan_plan_rows: die naechsten Zeilen des Tilgungsplans plus (falls mehr) die letzte.</summary>
    public static (List<PyTupel> Zeilen, int Ausgelassen) PlanZeilen(PyObjekt e, int anzahl = 8)
    {
        var zeilen = Liste(e["plan"]).Select(Objekt).ToList();
        var gezeigt = zeilen.Take(anzahl).ToList();
        var ausgelassen = 0;
        if (zeilen.Count > anzahl)
        {
            ausgelassen = zeilen.Count - anzahl - 1;
            gezeigt.Add(zeilen[^1]);
        }
        return ([.. gezeigt.Select(z => new PyTupel(z["tag"], z["rate"], z["zinsen"], Py.Plus(z["tilgung"], z["gebuehren"]), z["rest"]))], ausgelassen);
    }

    /// <summary>loan_payoff_text</summary>
    public static string AbloeseText(PyObjekt e, PyObjekt inhalt)
    {
        var abloese = Objekt(e["abloesen"]);
        var offen = Py.Int(abloese["zinsen"]) + Py.Int(abloese["gebuehren"]) - Py.Int(abloese["vorfaelligkeit"]);
        return Prozent("„%s“ jetzt ablösen? Du zahlst %s: Restschuld %s%s und %s Vorfälligkeitsentschädigung (%s der Restschuld).",
            e["name"], Euro(abloese["gesamt"]), Euro(abloese["tilgung"]), offen != 0 ? ", offene Zinsen und Gebühren " + Euro(offen) : "",
            Euro(abloese["vorfaelligkeit"]), ProzentText(KreditRegeln(inhalt)["abloesen_prozent"]));
    }

    /// <summary>dunning_text: Hinweis zur Mahnstufe (leer bei Stufe 0).</summary>
    public static string MahnText(Spielstand stand, PyObjekt inhalt)
    {
        var stufe = MahnStufe(stand.Mahnstufe, inhalt);
        if (stufe is null)
        {
            return "";
        }
        var regeln = Objekt(KreditRegeln(inhalt)["mahnung"]);
        var rest = Math.Max(1, Py.Int(regeln["abbau_tage"]) - (stand.Tag - 1 - stand.MahnTag));
        var folgen = new List<string> { "jede weitere geplatzte Zahlung (Kreditrate oder Umsatzsteuer) kostet Gebühren und Ansehen" };
        if (Py.Wahr(stufe["zins_plus"]))
        {
            folgen.Add("Zinsaufschlag " + ProzentText(stufe["zins_plus"]) + " auf alle Kredite");
        }
        folgen.Add(Py.F(Get(stufe, "rahmen_faktor", 0L)) == 0 ? "keine neuen Kredite"
            : Prozent("nur %d %% des Kreditrahmens", PythonRunden.Runde(100 * Py.F(stufe["rahmen_faktor"]))));
        return Prozent("Mahnstufe %d von %d: %s. Folgen: %s. Ohne geplatzte Zahlung sinkt die Stufe %s.", stand.Mahnstufe, (long)Liste(regeln["stufen"]).Count,
            stufe["name"], string.Join(", ", folgen), rest == 1 ? "beim nächsten Feierabend" : Prozent("nach %d Arbeitstagen", rest));
    }

    /// <summary>loans_today: was beim Feierabend mit den Krediten passiert ist (nur Anzeige).</summary>
    public static List<object?> KrediteHeute(Spielstand nachher, long tag)
    {
        var ergebnis = new List<object?>();
        foreach (var t in nachher.KreditProtokoll.Cast<PyTupel>())
        {
            var art = Py.Str(t[1]);
            if (Py.Int(t[0]) != tag || art is "aufgenommen" or "abgeloest")
            {
                continue;
            }
            var e = Py.Flach(Objekt(t[3]));
            Py.Setzen(e, "art", art);
            Py.Setzen(e, "kredit", t[2]);
            Py.Setzen(e, "name", Get(nachher.Kredite.Get(t[2]) as PyObjekt ?? [], "name", ""));
            ergebnis.Add(e);
        }
        return ergebnis;
    }

    private static string AnsehenText(PyObjekt reputation) =>
        string.Join(", ", reputation.Select(p => Prozent("%s %d", Betrieb.AchsenName[p.Key], p.Value)));

    /// <summary>loan_day_lines: Zeilen fuer den Feierabend.</summary>
    public static List<string> KreditTagZeilen(IEnumerable<object?> eintraege)
    {
        var zeilen = new List<string>();
        foreach (var e in eintraege.Select(Objekt))
        {
            var art = e["art"];
            if (Py.Gleich(art, "rate"))
            {
                zeilen.Add(Prozent("%s: Rate %s (Zinsen %s, Tilgung %s%s)", e["name"], Euro(e["rate"]), Euro(e["zinsen"]), Euro(e["tilgung"]),
                    Py.Wahr(e["gebuehren"]) ? ", Gebühren " + Euro(e["gebuehren"]) : ""));
            }
            else if (Py.Gleich(art, "ausfall") && Py.Wahr(e["einfach"]))
            {
                // Ab 0.47 auf "Einfach": keine Mahnung, die Rate haengt hinten an
                zeilen.Add(Prozent("%s: Rate %s diesmal ausgesetzt, das Konto hat nicht gereicht. Keine Mahnung, die Rate hängt hinten an.", e["name"], Euro(e["rate"])));
            }
            else if (Py.Gleich(art, "ausfall"))
            {
                var text = Prozent("%s: Rate %s geplatzt, das Konto hat nicht gereicht! Mahnstufe %d, Gebühr %s auf die Restschuld",
                    e["name"], Euro(e["rate"]), Get(e, "stufe", 1L), Euro(Get(e, "gebuehr", 0L)));
                if (Py.Wahr(e["reputation"]))
                {
                    text += ", Ansehen: " + AnsehenText(Objekt(e["reputation"]));
                }
                zeilen.Add(text);
            }
            else if (Py.Gleich(art, "zurueckgezahlt"))
            {
                zeilen.Add(Prozent("%s ist vollständig zurückgezahlt.", e["name"]));
            }
            else if (Py.Gleich(art, "mahnstufe"))
            {
                zeilen.Add(Prozent("Pünktlich bezahlt: Die Mahnstufe sinkt auf %d.", e["stufe"]));
            }
        }
        return zeilen;
    }

    /// <summary>fixed_costs_text: laufende Kosten pro Arbeitstag (Finanzen).</summary>
    public static string FesteKostenText(Spielstand stand)
    {
        var zahlen = stand.FirmenTag();
        var teile = new List<string> { "Gehälter " + Euro(Get(zahlen, "gehaelter", 0L)), "Nebenkosten " + Euro(Get(zahlen, "nebenkosten", 0L)) };
        if (stand.LaufendeKredite().Count > 0)
        {
            teile.Add("Kreditraten " + Euro(stand.KreditRaten()));
        }
        if (stand.WerbeKosten() != 0)
        {
            teile.Add("Werbung " + Euro(stand.WerbeKosten()));
        }
        if (stand.Miete != 0)
        {
            teile.Add("Miete " + Euro(stand.Miete));
        }
        return "Feste Kosten pro Arbeitstag: " + string.Join(" · ", teile);
    }

    // -- Umsatzsteuer (ab 0.42) ------------------------------------------------

    /// <summary>tax_rules</summary>
    public static PyObjekt SteuerRegeln(PyObjekt inhalt) => Objekt(Objekt(inhalt["balancing"])["steuern"]);

    /// <summary>tax_due_amount: Umsatzsteuer minus Vorsteuer (ganze Euro, negativ = Erstattung).</summary>
    public static long SteuerZahllast(PyObjekt zeitraum) => PythonRunden.Runde(Py.F(zeitraum["ust"]) - Py.F(zeitraum["vst"]));

    /// <summary>tax_share: Umsatzsteuer in einem Bruttobetrag.</summary>
    public static long SteuerAnteil(object? betrag, PyObjekt inhalt)
    {
        var satz = Py.F(SteuerRegeln(inhalt)["satz"]);
        return PythonRunden.Runde(Py.F(betrag) * satz / (100.0 + satz));
    }

    /// <summary>tax_next_due: Arbeitstag, an dessen Feierabend die naechste Voranmeldung faellig ist.</summary>
    public static long NaechsteVoranmeldung(Spielstand stand, PyObjekt inhalt)
    {
        var alle = Math.Max(1, Py.Int(SteuerRegeln(inhalt)["faellig_tage"]));
        return stand.Tag + alle - PyMod(stand.SteuerTage, alle) - 1;
    }

    /// <summary>tax_active: Umsatzsteuer im Spiel? (auf "Einfach" nicht)</summary>
    public static bool SteuerAktiv(Spielstand stand) => Py.Wahr(stand.Firma) && Py.Wahr(stand.Erleichterung("umsatzsteuer", true));

    /// <summary>tax_status: Ruecklage, laufender Zeitraum, naechste Faelligkeit, Schuld, letzte Voranmeldungen.</summary>
    public static PyObjekt SteuerStand(Spielstand stand, PyObjekt inhalt)
    {
        var regeln = SteuerRegeln(inhalt);
        var zeitraum = stand.SteuerZeitraum;
        var protokoll = stand.SteuerProtokoll.Cast<PyTupel>().ToList();
        var gemeldet = protokoll.Where(t => Py.Gleich(t[1], "voranmeldung")).Select(t => Objekt(t[2])).ToList();
        var letzte = gemeldet.Skip(Math.Max(0, gemeldet.Count - 4)).Reverse().Cast<object?>().ToList();
        return new PyObjekt
        {
            { "finanzamt", regeln["finanzamt"] }, { "satz", regeln["satz"] }, { "ruecklage", stand.SteuerRuecklage }, { "schuld", stand.SteuerSchuld },
            { "ust", PythonRunden.Runde(Py.F(zeitraum["ust"])) }, { "vorsteuer", PythonRunden.Runde(Py.F(zeitraum["vst"])) },
            { "zahllast", SteuerZahllast(zeitraum) }, { "umsatz", zeitraum["umsatz"] }, { "ausgaben", zeitraum["ausgaben"] }, { "tage", zeitraum["tage"] },
            { "faellig_tage", Py.Int(regeln["faellig_tage"]) }, { "naechste", NaechsteVoranmeldung(stand, inhalt) },
            { "letzte", letzte }, { "anzahl", (long)gemeldet.Count },
            { "gezahlt", gemeldet.Sum(e => Py.Int(Get(e, "aus_ruecklage", 0L)) + Py.Int(Get(e, "vom_konto", 0L)))
                + protokoll.Where(t => Py.Gleich(t[1], "nachgezahlt")).Sum(t => Py.Int(Objekt(t[2])["betrag"])) },
            { "erstattet", gemeldet.Sum(e => Py.Int(Get(e, "erstattung", 0L))) },
        };
    }

    /// <summary>tax_status_lines: Zeilen zur Umsatzsteuer.</summary>
    public static List<string> SteuerZeilen(PyObjekt e)
    {
        var anzeige = (Func<object?, string>)Betrieb.GanzeEuroAnzeige;
        var zeilen = new List<string>
        {
            Prozent("Steuerrücklage %s · nächste Voranmeldung beim Feierabend von Arbeitstag %d", Euro(e["ruecklage"]), e["naechste"]),
            Prozent("Dieser Zeitraum (%s): Umsatzsteuer %s aus %s Einnahmen, Vorsteuer %s aus %s Ausgaben, Zahllast bisher %s",
                !Py.Wahr(e["tage"]) ? "noch kein Feierabend" : LaufzeitText(e["tage"]), anzeige(e["ust"]), anzeige(e["umsatz"]),
                anzeige(e["vorsteuer"]), anzeige(e["ausgaben"]),
                Py.Int(e["zahllast"]) < 0 ? "Erstattung " + anzeige(-Py.Int(e["zahllast"])) : anzeige(e["zahllast"])),
        };
        if (Py.Wahr(e["anzahl"]))
        {
            zeilen.Add(Prozent("Bisher %d Voranmeldungen · gezahlt %s%s", e["anzahl"], Euro(e["gezahlt"]), Py.Wahr(e["erstattet"]) ? " · erstattet " + Euro(e["erstattet"]) : ""));
        }
        return zeilen;
    }

    /// <summary>tax_filing_text: eine Voranmeldung in einer Zeile.</summary>
    public static string VoranmeldungText(PyObjekt d, bool offen = true)
    {
        var text = Prozent("Arbeitstag %d bis %d: Umsatzsteuer %s − Vorsteuer %s = ", d["von"], d["bis"], Euro(d["ust"]), Euro(d["vorsteuer"]));
        if (Py.Wahr(d["erstattung"]))
        {
            return text + "Erstattung " + Euro(d["erstattung"]);
        }
        text += "Zahllast " + Euro(d["zahllast"]);
        if (Py.Wahr(d["offen"]) && offen)
        {
            text += Prozent(" · %s konnten nicht bezahlt werden", Euro(d["offen"]));
        }
        return text;
    }

    /// <summary>tax_debt_text: Warnung bei offener Steuerschuld (leer, wenn alles bezahlt ist).</summary>
    public static string SteuerSchuldText(Spielstand stand, PyObjekt inhalt) => stand.SteuerSchuld == 0 ? ""
        : Prozent("Offene Umsatzsteuer beim %s: %s. Sie wird beim nächsten Feierabend automatisch bezahlt, sobald das Konto reicht. "
            + "Bis dahin gilt jeder Feierabend als geplatzte Zahlung (Mahnstufe wie bei Kreditraten).", SteuerRegeln(inhalt)["finanzamt"], Euro(stand.SteuerSchuld));

    /// <summary>taxes_today: was beim Feierabend mit der Umsatzsteuer passiert ist (nur Anzeige).</summary>
    public static List<object?> SteuernHeute(Spielstand nachher, long tag) =>
        [.. nachher.SteuerProtokoll.Cast<PyTupel>().Where(t => Py.Int(t[0]) == tag).Select(t =>
        {
            var e = Py.Flach(Objekt(t[2]));
            Py.Setzen(e, "art", t[1]);
            return (object?)e;
        })];

    /// <summary>tax_day_lines: Zeilen fuer den Feierabend.</summary>
    public static List<string> SteuerTagZeilen(IEnumerable<object?> eintraege, PyObjekt inhalt)
    {
        var zeilen = new List<string>();
        var amt = SteuerRegeln(inhalt)["finanzamt"];
        foreach (var e in eintraege.Select(Objekt))
        {
            var art = e["art"];
            if (Py.Gleich(art, "ruecklage"))
            {
                zeilen.Add(Py.Int(e["betrag"]) > 0
                    ? Prozent("In die Steuerrücklage: %s (jetzt %s)", Euro(e["betrag"]), Euro(e["ruecklage"]))
                    : "Aus der Steuerrücklage zurück (Vorsteuer): " + Euro(-Py.Int(e["betrag"])));
            }
            else if (Py.Gleich(art, "voranmeldung"))
            {
                var text = Prozent("Umsatzsteuer-Voranmeldung ans %s: %s", amt, VoranmeldungText(e, false));
                if (Py.Wahr(e["aus_ruecklage"]))
                {
                    text += " · aus der Rücklage " + Euro(e["aus_ruecklage"]);
                }
                if (Py.Wahr(e["vom_konto"]))
                {
                    text += " · vom Konto " + Euro(e["vom_konto"]);
                }
                if (Py.Wahr(e["frei"]))
                {
                    text += " · Rest der Rücklage zurück aufs Konto: " + Euro(e["frei"]);
                }
                if (Py.Wahr(e["offen"]))
                {
                    text += Prozent(" · %s konnten nicht bezahlt werden", Euro(e["offen"]));
                }
                zeilen.Add(text);
            }
            else if (Py.Gleich(art, "nachgezahlt"))
            {
                zeilen.Add(Prozent("Offene Umsatzsteuer nachgezahlt: %s%s", Euro(e["betrag"]), Py.Wahr(e["schuld"]) ? Prozent(" (noch offen %s)", Euro(e["schuld"])) : ""));
            }
            else if (Py.Gleich(art, "ausfall"))
            {
                var text = Prozent("Umsatzsteuer %s nicht bezahlt, das Konto hat nicht gereicht! Mahnstufe %d, Säumnisgebühr %s", Euro(e["betrag"]), e["stufe"], Euro(e["gebuehr"]));
                if (Py.Wahr(e["reputation"]))
                {
                    text += ", Ansehen: " + AnsehenText(Objekt(e["reputation"]));
                }
                zeilen.Add(text);
            }
        }
        return zeilen;
    }

    // -- Werbung (ab 0.42) -----------------------------------------------------

    /// <summary>marketing_rules</summary>
    public static PyObjekt MarketingRegeln(PyObjekt inhalt) =>
        Get(Objekt(inhalt["balancing"]), "marketing") is PyObjekt m && Py.Wahr(m) ? m : new PyObjekt { { "formen", new List<object?>() } };

    /// <summary>ad_forms</summary>
    public static List<PyObjekt> WerbeFormen(PyObjekt inhalt) =>
        Py.Wahr(MarketingRegeln(inhalt)["formen"]) ? [.. Liste(MarketingRegeln(inhalt)["formen"]).Select(Objekt)] : [];

    /// <summary>ad_form</summary>
    public static PyObjekt? WerbeForm(object? kennung, PyObjekt inhalt) => WerbeFormen(inhalt).FirstOrDefault(f => Py.Gleich(f["id"], kennung));

    /// <summary>ad_active: wirkt die gebuchte Werbung an diesem Arbeitstag?</summary>
    public static bool WerbungAktiv(PyObjekt buchung, long tag)
    {
        if (Py.Int(Get(buchung, "ab_tag", 0L)) > tag)
        {
            return false;
        }
        return WerbungOffen(buchung, tag);
    }

    /// <summary>ad_open: laeuft die Buchung am Tag noch oder beginnt sie erst?</summary>
    public static bool WerbungOffen(PyObjekt buchung, long tag)
    {
        if (Py.Gleich(buchung["art"], WerbungEinmalig))
        {
            return tag <= Py.Int(Get(buchung, "bis_tag", 0L));
        }
        return buchung["ende"] is null || tag <= Py.Int(buchung["ende"]);
    }

    /// <summary>marketing_effects: Summe der Wirkung aller Werbung, die am Tag laeuft.</summary>
    public static PyObjekt WerbeWirkung(Spielstand stand, long? tag = null, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var ergebnis = new PyObjekt { { "anfragen", 0.0 }, { "tickets", 0.0 }, { "groesse", 0L }, { "vorteil", 0L } };
        foreach (var buchung in stand.WerbungAktiv(tag ?? stand.Tag))
        {
            var form = WerbeForm(buchung["werbung"], inhalt) ?? [];
            foreach (var (k, w) in Py.Wahr(form["wirkung"]) ? Objekt(form["wirkung"]) : [])
            {
                if (Py.Hat(ergebnis, k))
                {
                    Py.Setzen(ergebnis, k, Py.Plus(ergebnis[k], w));
                }
            }
        }
        return ergebnis;
    }

    /// <summary>_extra_count: 0,5 -> an etwa jedem zweiten Tag eins mehr (fest gewuerfelt).</summary>
    public static long ExtraAnzahl(object? wert, string startwert, long tag, string salz)
    {
        var ganz = Py.Int(wert);
        return ganz + (Betrieb.Wurf(startwert, tag, salz) < Py.F(wert) - ganz ? 1 : 0);
    }

    /// <summary>_share_text</summary>
    private static string AnteilText(object? wert) => Prozent("%.1f", Py.F(wert)).TrimEnd('0').TrimEnd('.').Replace(".", ",", StringComparison.Ordinal);

    /// <summary>effect_text: "+1 Anfrage pro Tag · 10 % größere Bestellungen · +2 % Preisvorteil"</summary>
    public static string WirkungText(PyObjekt wirkung)
    {
        var teile = new List<string>();
        if (Py.Wahr(wirkung["anfragen"]))
        {
            teile.Add(Prozent("+%s %s pro Tag", AnteilText(wirkung["anfragen"]), Py.Gleich(wirkung["anfragen"], 1L) ? "Anfrage" : "Anfragen"));
        }
        if (Py.Wahr(wirkung["tickets"]))
        {
            teile.Add(Prozent("+%s %s pro Tag", AnteilText(wirkung["tickets"]), Py.Gleich(wirkung["tickets"], 1L) ? "Kundenticket" : "Kundentickets"));
        }
        if (Py.Wahr(wirkung["groesse"]))
        {
            teile.Add(Prozent("%d %% größere Bestellungen", wirkung["groesse"]));
        }
        if (Py.Wahr(wirkung["vorteil"]))
        {
            teile.Add(Prozent("+%d %% Preisvorteil gegen Mitbewerber", wirkung["vorteil"]));
        }
        return string.Join(" · ", teile);
    }

    /// <summary>ad_offer: was eine Buchung kostet (mit "problem", falls es gerade nicht geht).</summary>
    public static PyObjekt WerbeAngebot(Spielstand stand, object? kennung, object? art, PyObjekt? inhalt = null)
    {
        var form = WerbeForm(kennung, inhalt ?? stand.Inhalt);
        if (form is null)
        {
            return new PyObjekt { { "problem", "Diese Werbeform gibt es nicht." } };
        }
        var ergebnis = new PyObjekt
        {
            { "id", kennung }, { "name", form["name"] }, { "art", art }, { "kosten", 0L }, { "preis", 0L }, { "tage", 0L }, { "problem", "" },
        };
        if (Py.Gleich(art, WerbungEinmalig))
        {
            Py.Setzen(ergebnis, "preis", Py.Int(Objekt(form["einmalig"])["preis"]));
            Py.Setzen(ergebnis, "tage", Py.Int(Objekt(form["einmalig"])["tage"]));
        }
        else if (Py.Gleich(art, WerbungLaufend))
        {
            Py.Setzen(ergebnis, "kosten", Py.Int(form["laufend"]));
        }
        else
        {
            Py.Setzen(ergebnis, "problem", "Bitte wähle laufend oder einmalig.");
            return ergebnis;
        }
        var buchung = stand.WerbeBuchung(kennung, stand.Tag);
        var kurz = Get(form, "kurz", form["name"]);
        if (!Py.Wahr(stand.Firma))
        {
            Py.Setzen(ergebnis, "problem", "Werbung gibt es nur für die eigene Firma.");
        }
        else if (buchung is not null)
        {
            Py.Setzen(ergebnis, "problem", Py.Gleich(buchung["art"], WerbungLaufend) ? Prozent("„%s“ läuft schon.", kurz)
                : Prozent("„%s“ läuft noch bis Arbeitstag %d.", kurz, buchung["bis_tag"]));
        }
        else if (Py.Gleich(art, WerbungEinmalig) && stand.Geld < Py.Int(ergebnis["preis"]))
        {
            Py.Setzen(ergebnis, "problem", Prozent("Dafür reicht dein Geld nicht (%s fehlen).", Euro(Py.Int(ergebnis["preis"]) - Math.Max(0, stand.Geld))));
        }
        else if (Py.Gleich(art, WerbungLaufend) && stand.Geld <= 0)
        {
            Py.Setzen(ergebnis, "problem", "Mit leerem Konto kannst du keine Werbung buchen.");
        }
        return ergebnis;
    }

    /// <summary>ad_status: alle Werbeformen mit Stand und Angeboten.</summary>
    public static List<PyObjekt> WerbeStand(Spielstand stand, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var ergebnis = new List<PyObjekt>();
        foreach (var form in WerbeFormen(inhalt))
        {
            var buchung = stand.WerbeBuchung(form["id"], stand.Tag);
            var status = "";
            if (buchung is not null)
            {
                var start = Py.Int(buchung["ab_tag"]);
                if (Py.Gleich(buchung["art"], WerbungLaufend))
                {
                    status = Prozent("Läuft dauerhaft · %s pro Arbeitstag", Euro(buchung["kosten"]));
                    if (buchung["ende"] is not null)
                    {
                        status = "Gekündigt · heute zum letzten Mal";
                    }
                    else if (start > stand.Tag)
                    {
                        status += " · wirkt ab dem nächsten Arbeitstag";
                    }
                    else
                    {
                        status += Prozent(" · seit Arbeitstag %d", start);
                    }
                }
                else
                {
                    var rest = Py.Int(buchung["bis_tag"]) - Math.Max(stand.Tag, start - 1);
                    status = start > stand.Tag
                        ? "Einmalig gebucht · wirkt ab dem nächsten Arbeitstag für " + LaufzeitText(rest)
                        : Prozent("Einmalig gebucht · wirkt noch %s (bis Arbeitstag %d)", rest <= 0 ? "heute" : LaufzeitText(rest + 1), buchung["bis_tag"]);
                }
            }
            var angebote = new PyObjekt();
            foreach (var (art, _) in WerbeArten)
            {
                angebote.Add(art, WerbeAngebot(stand, form["id"], art, inhalt));
            }
            ergebnis.Add(new PyObjekt
            {
                { "id", form["id"] }, { "name", form["name"] }, { "kurz", Get(form, "kurz", form["name"]) }, { "text", Get(form, "text", "") },
                { "wirkung", WirkungText(Py.Wahr(form["wirkung"]) ? Objekt(form["wirkung"]) : []) }, { "laufend", Py.Int(form["laufend"]) },
                { "einmalig", Py.Flach(Objekt(form["einmalig"])) }, { "buchung", buchung }, { "stand", status },
                { "aktiv", buchung is not null && WerbungAktiv(buchung, stand.Tag) }, { "angebote", angebote },
            });
        }
        return ergebnis;
    }

    /// <summary>ad_price_text: "Laufend 110 € pro Arbeitstag · einmalig 900 € für 10 Arbeitstage (90 € pro Tag)"</summary>
    public static string WerbePreisText(PyObjekt e)
    {
        var einmal = Objekt(e["einmalig"]);
        var anzeige = (Func<object?, string>)Betrieb.GanzeEuroAnzeige;
        return Prozent("Laufend %s pro Arbeitstag · einmalig %s für %s (%s pro Tag)", anzeige(e["laufend"]), anzeige(einmal["preis"]),
            LaufzeitText(einmal["tage"]), anzeige(PythonRunden.Runde(Py.F(einmal["preis"]) / Py.F(einmal["tage"]))));
    }

    /// <summary>ad_confirm_text</summary>
    public static string WerbeFrage(PyObjekt angebot) => Py.Gleich(angebot["art"], WerbungEinmalig)
        ? Prozent("„%s“ einmalig buchen?\n\nDu zahlst sofort %s. Die Werbung wirkt ab dem nächsten Arbeitstag %s lang und endet dann von selbst.",
            angebot["name"], Euro(angebot["preis"]), LaufzeitText(angebot["tage"]))
        : Prozent("„%s“ dauerhaft buchen?\n\nJeden Feierabend werden automatisch %s abgebucht, bis du kündigst. Die Werbung wirkt ab dem nächsten Arbeitstag.",
            angebot["name"], Euro(angebot["kosten"]));

    /// <summary>marketing_summary_text: was die Werbung heute bewirkt.</summary>
    public static string MarketingText(Spielstand stand, PyObjekt? inhalt = null)
    {
        var text = WirkungText(WerbeWirkung(stand, inhalt: inhalt));
        var kosten = stand.WerbeKosten();
        if (text.Length == 0)
        {
            return "Gerade läuft keine Werbung. Ohne Werbung kommen nur die üblichen Anfragen.";
        }
        return Prozent("Deine Werbung bringt heute: %s.%s", text, kosten != 0 ? Prozent(" Laufende Kosten: %s pro Arbeitstag.", Euro(kosten)) : "");
    }

    // -- Zertifizierungen (ab 0.42) --------------------------------------------

    /// <summary>cert_rules</summary>
    public static PyObjekt ZertRegeln(PyObjekt inhalt) =>
        Get(Objekt(inhalt["balancing"]), "zertifizierungen") is PyObjekt z && Py.Wahr(z) ? z : new PyObjekt { { "liste", new List<object?>() } };

    /// <summary>certificates</summary>
    public static List<PyObjekt> Zertifikate(PyObjekt inhalt) =>
        Py.Wahr(ZertRegeln(inhalt)["liste"]) ? [.. Liste(ZertRegeln(inhalt)["liste"]).Select(Objekt)] : [];

    /// <summary>certificate</summary>
    public static PyObjekt? Zertifikat(object? kennung, PyObjekt inhalt) => Zertifikate(inhalt).FirstOrDefault(z => Py.Gleich(z["id"], kennung));

    /// <summary>advantage_text: {"alle": 2, "sicherheit": 4} -> "+2 % bei allen Angeboten · +4 % bei Projekten ..."</summary>
    public static string VorteilText(PyObjekt vorteil) => string.Join(" · ", vorteil.Select(p => p.Key == "alle"
        ? Prozent("+%d %% bei allen Angeboten", p.Value)
        : Prozent("+%d %% bei Projekten %s", p.Value, SpielThemen.Jetzt.Name.TryGetValue(p.Key, out var n) ? n : p.Key)));

    /// <summary>cert_advantage: Preisvorteil aus allen abgeschlossenen Zertifizierungen.</summary>
    public static long ZertVorteil(Spielstand stand, object? fach = null, long? tag = null, PyObjekt? inhalt = null)
    {
        long summe = 0;
        foreach (var kennung in stand.ZertifikateFertig(tag))
        {
            var vorteil = Get(Zertifikat(kennung, inhalt ?? stand.Inhalt) ?? [], "vorteil") is PyObjekt v ? v : [];
            summe += Py.Int(Get(vorteil, "alle", 0L));
            if (Py.Wahr(fach))
            {
                summe += Py.Int(Get(vorteil, Py.Str(fach), 0L));
            }
        }
        return summe;
    }

    /// <summary>cert_offer</summary>
    public static PyObjekt ZertAngebot(Spielstand stand, object? kennung, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var zert = Zertifikat(kennung, inhalt);
        if (zert is null)
        {
            return new PyObjekt { { "problem", "Diese Zertifizierung gibt es nicht." } };
        }
        var ergebnis = new PyObjekt { { "id", kennung }, { "name", zert["name"] }, { "preis", Py.Int(zert["preis"]) }, { "tage", Py.Int(zert["tage"]) }, { "problem", "" } };
        var laufend = stand.ZertifikateLaufend();
        var grenze = Py.Int(Get(ZertRegeln(inhalt), "gleichzeitig", 1L));
        if (!Py.Wahr(stand.Firma))
        {
            Py.Setzen(ergebnis, "problem", "Zertifizierungen gibt es nur für die eigene Firma.");
        }
        else if (stand.ZertifikateFertig().Contains(kennung))
        {
            Py.Setzen(ergebnis, "problem", "Deine Firma hat diese Zertifizierung schon.");
        }
        else if (stand.Zertifikate.ContainsKey(kennung))
        {
            Py.Setzen(ergebnis, "problem", Prozent("Läuft schon, fertig beim Feierabend von Arbeitstag %d.", Py.Int(Objekt(stand.Zertifikate[kennung])["bis_tag"]) - 1));
        }
        else if (laufend.Count >= grenze)
        {
            var anderes = Zertifikat(laufend[0]["zert"], inhalt) ?? [];
            Py.Setzen(ergebnis, "problem", Prozent("Es läuft schon eine Zertifizierung (%s, bis Arbeitstag %d).", Get(anderes, "kurz", ""), Py.Int(laufend[0]["bis_tag"]) - 1));
        }
        else if (stand.Geld < Py.Int(ergebnis["preis"]))
        {
            Py.Setzen(ergebnis, "problem", Prozent("Dafür reicht dein Geld nicht (%s fehlen).", Euro(Py.Int(ergebnis["preis"]) - Math.Max(0, stand.Geld))));
        }
        return ergebnis;
    }

    /// <summary>cert_status: alle Zertifizierungen mit Stand (erworben, laeuft, offen).</summary>
    public static List<PyObjekt> ZertStand(Spielstand stand, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var gross = Py.Wahr(GrossRegeln(inhalt)["liste"]) ? Liste(GrossRegeln(inhalt)["liste"]).Select(Objekt).ToList() : [];
        var fertig = stand.ZertifikateFertig();
        var ergebnis = new List<PyObjekt>();
        foreach (var zert in Zertifikate(inhalt))
        {
            var daten = stand.Zertifikate.Get(zert["id"]) as PyObjekt;
            string zustand, text;
            if (fertig.Contains(zert["id"]))
            {
                (zustand, text) = ("erworben", Prozent("Erworben an Arbeitstag %d", Py.Int(daten!["bis_tag"])));
            }
            else if (daten is not null && Py.Wahr(daten))
            {
                zustand = "laeuft";
                var rest = Py.Int(daten["bis_tag"]) - stand.Tag;
                text = "Läuft · fertig " + (rest <= 1 ? "beim heutigen Feierabend" : Prozent("in %d Arbeitstagen", rest));
            }
            else
            {
                (zustand, text) = ("offen", "");
            }
            ergebnis.Add(new PyObjekt
            {
                { "id", zert["id"] }, { "name", zert["name"] }, { "kurz", Get(zert, "kurz", zert["name"]) }, { "art", Get(zert, "art", "firma") },
                { "text", Get(zert, "text", "") }, { "preis", Py.Int(zert["preis"]) }, { "tage", Py.Int(zert["tage"]) },
                { "vorteil", VorteilText(Py.Wahr(zert["vorteil"]) ? Objekt(zert["vorteil"]) : []) },
                { "freischaltet", gross.Where(g => Py.Enthaelt(Get(g, "braucht", new List<object?>()), zert["id"])).Select(g => g["titel"]).ToList() },
                { "stand", zustand }, { "stand_text", text }, { "angebot", ZertAngebot(stand, zert["id"], inhalt) },
            });
        }
        return ergebnis;
    }

    /// <summary>cert_unlock_text: "Schaltet Großaufträge frei: ..."</summary>
    public static string FreischaltText(PyObjekt e) => !Py.Wahr(e["freischaltet"]) ? ""
        : "Schaltet Großaufträge frei: " + string.Join("; ", Liste(e["freischaltet"]).Select(t => "„" + Py.Str(t) + "“"));

    /// <summary>cert_confirm_text</summary>
    public static string ZertFrage(PyObjekt angebot) => Prozent(
        "„%s“ beginnen?\n\nKosten %s sofort. Audit, Schulungen und Prüfung dauern %s, danach gilt die Zertifizierung dauerhaft. Deine Leute arbeiten in der Zeit normal weiter.",
        angebot["name"], Euro(angebot["preis"]), LaufzeitText(angebot["tage"]));

    /// <summary>certs_summary_text</summary>
    public static string ZertText(Spielstand stand, PyObjekt? inhalt = null)
    {
        var fertig = stand.ZertifikateFertig();
        if (fertig.Count == 0)
        {
            return "Noch keine Zertifizierung. Mit Zertifizierungen bekommst du große und öffentliche Aufträge und bessere Chancen gegen die Mitbewerber.";
        }
        var namen = Zertifikate(inhalt ?? stand.Inhalt).Where(z => fertig.Contains(z["id"])).Select(z => Py.Str(Get(z, "kurz", z["name"])));
        return "Deine Firma ist zertifiziert: " + string.Join(", ", namen) + ".";
    }

    /// <summary>offer_advantage_parts: woraus sich der Preisvorteil zusammensetzt.</summary>
    public static List<(string Grund, long Prozent)> VorteilTeile(Spielstand stand, object? fach = null, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var regel = Objekt(Betrieb.AngebotRegeln(inhalt)["vorteil"]);
        var teile = new List<(string, long)>();
        if (!Py.Kleiner(Get(stand.Reputation, "kundenzufriedenheit", 0L), regel["ab_kundenzufriedenheit"]))
        {
            teile.Add(("guter Ruf", Py.Int(regel["prozent"])));
        }
        var zert = ZertVorteil(stand, fach, inhalt: inhalt);
        if (zert != 0)
        {
            teile.Add(("Zertifizierungen", zert));
        }
        var werbung = Py.Int(WerbeWirkung(stand, inhalt: inhalt)["vorteil"]);
        if (werbung != 0)
        {
            teile.Add(("Werbung", werbung));
        }
        return teile;
    }

    /// <summary>gross_rules</summary>
    public static PyObjekt GrossRegeln(PyObjekt inhalt) => Get(ZertRegeln(inhalt), "grossauftraege") is PyObjekt g && Py.Wahr(g) ? g : [];
}
