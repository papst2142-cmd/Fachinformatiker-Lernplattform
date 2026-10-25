using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Rechner;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>Eine Zeile der Bestenliste (Tabelle spiel_bestenliste): Wert wie die Spalte REAL (float oder None).</summary>
public sealed record BestenZeile(string Zeit, string Lauf, string Art, string Schluessel, double? Wert, PyObjekt Daten);

/// <summary>
/// Phase 7, W3: Erfolge und Bestenliste aus <c>fisi_game.py</c> (Python 0.63.2, Z. 12165-12760).
/// Abzeichen und Bestwerte werden aus dem Spielstand berechnet (run_metrics); die Bestenliste gilt
/// ueber alle Durchgaenge.
/// </summary>
public static partial class Betrieb
{
    /// <summary>TIER_NAMES</summary>
    public static readonly IReadOnlyDictionary<string, string> StufenNamen = new Dictionary<string, string>
    {
        ["bronze"] = "Bronze", ["silber"] = "Silber", ["gold"] = "Gold",
    };

    /// <summary>TIER_ORDER</summary>
    public static readonly IReadOnlyList<string> StufenFolge = ["bronze", "silber", "gold"];

    /// <summary>EURO_METRICS</summary>
    public static readonly IReadOnlyList<string> EuroKennzahlen = ["kontostand", "tagesumsatz", "groesster_auftrag"];

    /// <summary>FIRM_INCOME</summary>
    public static readonly IReadOnlyList<string> FirmenEinnahmen =
        [Ereignisse.BuchUmsatz, Ereignisse.BuchAngebote, Ereignisse.BuchKundentickets, Ereignisse.BuchProjekte];

    /// <summary>RECORD_SUMS</summary>
    public static readonly IReadOnlyList<string> BestenSummen = ["diensttage"];

    public const string BestDurchgang = "durchgang";   // REC_RUN
    public const string BestWert = "bestwert";          // REC_BEST
    public const string BestErfolg = "erfolg";          // REC_BADGE
    public const int BestenAbstand = 20;                // RECORD_EVERY
    public const int WissenZiel = 70;                   // KNOWLEDGE_GOAL
    public const int StimmungGut = 70;                  // MOOD_GOOD
    public const int FehlerfreiMindestens = 3;          // FLAWLESS_MIN

    public const string BestenHilfe = "Die Bestenliste unter Spiel > Reise > Erfolge sammelt Bestwerte und "
        + "Abzeichen über alle Spielstände. Sie bleibt beim Zurücksetzen des "
        + "Spielstands und beim Löschen der Lerndaten erhalten.";

    public const string BestenFrage = "Wirklich die Bestenliste löschen? Bestwerte und Abzeichen früherer "
        + "Spielstände sind danach weg. Der laufende Spielstand trägt sich gleich "
        + "wieder ein.";

    /// <summary>LEVEL_METRICS</summary>
    public static readonly IReadOnlyList<string> StufenKennzahlen = ["rang", "gebaeude"];

    /// <summary>RUN_METRICS</summary>
    public static readonly IReadOnlyList<string> LaufKennzahlen =
    [
        "profil", "tickets", "zwischenfaelle", "fehlerfrei_serie", "rang", "ansehen",
        "wohnung_gekauft", "diensttage", "gegruendet", "mitarbeiter", "gebaeude", "filiale",
        "zertifikate", "grossauftraege", "projekte", "angebote_serie", "mitbewerber_besiegt",
        "kontostand", "tagesumsatz", "kredit_getilgt", "mahnung_vorbei", "mahnung_hoch_vorbei",
        "steuer_serie", "stimmung_serie", "stimmung_max", "weiterbildungen", "experte",
        "geschlichtet", "abwerbung_abgewehrt", "macke_gezaehmt", "themen_70", "fachbereiche_70",
        "auftraege", "groesster_auftrag", "tage_gruendung", "tage_stufe5", "tage_filiale",
        "story_einfach", "story_normal",
        "serverfarm_einfach", "serverfarm_normal", "serverfarm_fehlerfrei", "serverfarm_puenktlich",
        "serverfarm_tage",
    ];

    /// <summary>STORY_BADGES</summary>
    public static readonly IReadOnlyList<string> StoryAbzeichen = ["story_einfach", "story_normal"];

    /// <summary>achievement_rules</summary>
    public static PyObjekt ErfolgRegeln(PyObjekt inhalt) => OderLeer(inhalt["erfolge"]);

    private static List<PyObjekt> ErfolgListe(PyObjekt inhalt) => [.. ListeOderLeer(ErfolgRegeln(inhalt), "erfolge").Select(Objekt)];

    private static List<PyObjekt> BestwertListe(PyObjekt inhalt) => [.. ListeOderLeer(ErfolgRegeln(inhalt), "bestwerte").Select(Objekt)];

    /// <summary>achievement_rule</summary>
    public static PyObjekt? ErfolgRegel(object? kennung, PyObjekt inhalt) => ErfolgListe(inhalt).FirstOrDefault(r => Py.Gleich(r["id"], kennung));

    /// <summary>record_rule</summary>
    public static PyObjekt? BestwertRegel(object? kennung, PyObjekt inhalt) => BestwertListe(inhalt).FirstOrDefault(r => Py.Gleich(r["id"], kennung));

    /// <summary>_longest: laengste Serie von True.</summary>
    public static long LaengsteSerie(IEnumerable<bool> merker)
    {
        long beste = 0, lauf = 0;
        foreach (var m in merker)
        {
            lauf = m ? lauf + 1 : 0;
            beste = Math.Max(beste, lauf);
        }
        return beste;
    }

    private static object? MaxListe(IEnumerable<object?> werte)
    {
        object? beste = null;
        var erstes = true;
        foreach (var w in werte)
        {
            if (erstes || Py.Kleiner(beste, w))
            {
                beste = w;
            }
            erstes = false;
        }
        return erstes ? throw new InvalidOperationException("max() arg is an empty sequence") : beste;
    }

    /// <summary>
    /// run_metrics: alle Kennzahlen des laufenden Durchgangs (siehe RUN_METRICS); null = nicht zu
    /// berechnen. Unter "_texte" stehen Erklaerungen zu einzelnen Werten (groesster Auftrag).
    /// </summary>
    public static PyObjekt LaufWerte(Spielstand stand, PyObjekt? wissen = null, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var m = new PyObjekt();
        foreach (var k in LaufKennzahlen)
        {
            m.Add(k, 0L);
        }
        var texte = new PyObjekt();
        m.Add("_texte", texte);
        void S(string k, object? w) => Py.Setzen(m, k, w);
        long Z(string k) => Py.Int(m[k]);
        S("profil", stand.Profil is not null ? 1L : 0L);
        if (stand.Profil is not null && stand.AllesErledigt())
        {
            S("story_" + Py.Str(stand.Stufe), 1L);
        }
        var abnahme = (stand.Farm as PyObjekt ?? [])["abnahme"];
        S("serverfarm_tage", null);
        if (Py.Wahr(abnahme))
        {
            var a = Objekt(abnahme);
            S("serverfarm_" + Py.Str(Get(a, "stufe", stand.Stufe)), 1L);
            S("serverfarm_fehlerfrei", Py.Wahr(a["fehlerfrei"]) ? 1L : 0L);
            S("serverfarm_puenktlich", Py.Wahr(a["puenktlich"]) ? 1L : 0L);
            var dauer = Py.Int(Get(a, "dauer", 0L));
            S("serverfarm_tage", dauer != 0 ? dauer : null);
        }
        var jeTag = new Dictionary<long, long[]>();
        var gesehen = new PySet();
        var angebote = new List<bool>();
        foreach (var e in stand.Verlauf)
        {
            var d = e.Daten;
            if (e.Art == Ereignisse.Erledigt)
            {
                var tag = Py.IstInt(d["tag"]) ? Py.Int(d["tag"]) : 0;
                if (!jeTag.TryGetValue(tag, out var eimer))
                {
                    jeTag[tag] = eimer = [0, 0];
                }
                eimer[Py.Wahr(d["richtig"]) ? 0 : 1] += 1;
                if (Py.Wahr(d["zwischenfall"]))
                {
                    S("zwischenfaelle", Z("zwischenfaelle") + (Py.Wahr(d["richtig"]) ? 1 : 0));
                }
                else if (Py.Wahr(d["richtig"]))
                {
                    S("tickets", Z("tickets") + 1);
                }
            }
            else if (e.Art == Ereignisse.Umzug && Py.Wahr(d["wohnung"]) && !Py.Wahr(d["miete"]))
            {
                S("wohnung_gekauft", 1L);
            }
            else if (e.Art is Ereignisse.AngebotGewonnen or Ereignisse.AngebotVerloren or Ereignisse.ProjektGewonnen or Ereignisse.ProjektVerloren)
            {
                var schluessel = Py.Wahr(d["anfrage"]) ? d["anfrage"] : d["projekt"];
                if (Py.Wahr(schluessel) && !gesehen.Contains(schluessel) && Py.Wahr(stand.Firma))
                {
                    gesehen.Add(schluessel);
                    angebote.Add(e.Art is Ereignisse.AngebotGewonnen or Ereignisse.ProjektGewonnen);
                }
            }
        }
        long[] Eimer(long tag) => jeTag.TryGetValue(tag, out var e) ? e : [0, 0];
        S("fehlerfrei_serie", LaengsteSerie(Enumerable.Range(1, (int)stand.TageFertig)
            .Select(t => Eimer(t)[0] >= FehlerfreiMindestens && Eimer(t)[1] == 0)));
        var namen = Liste(stand.Balancing["raenge"]).Select(r => Objekt(r)["name"]).ToList();
        S("rang", stand.RangProtokoll.Where(r => namen.Any(n => Py.Gleich(n, r[1])))
            .Select(r => (long)namen.FindIndex(n => Py.Gleich(n, r[1]))).Append(0L).Max());
        S("ansehen", Py.Rund(MaxListe(stand.TagesProtokoll.Select(t => t[1]).Append(stand.MittlereReputation))));
        S("diensttage", stand.TageFertig);
        S("kontostand", MaxListe(stand.Kontostaende.Select(k => k[1]).Append(0L)));
        var firma = stand.Firma as PyObjekt;
        var hatFirma = Py.Wahr(firma);
        S("gegruendet", hatFirma ? 1L : 0L);
        S("mitarbeiter", stand.PersonalHoechst);
        var stufe = hatFirma ? Py.Int(firma!["stufe"]) : 0;
        var raeume = SonderRaeume(inhalt);
        S("gebaeude", stufe < 3 ? 0L : stufe < 5 ? 1L : raeume.All(r => stand.Raeume.ContainsKey(r["id"])) ? 3L : 2L);
        var zweig = stand.Zweigstelle as PyObjekt;
        S("filiale", Py.Wahr(zweig) ? zweig!["stufe"] : 0L);
        S("zertifikate", (long)stand.Zertifikate.Values.Select(Objekt).Count(z => Py.Int(Get(z, "bis_tag", 0L)) <= stand.Tag));
        var gewonnen = stand.Angebote.Values.Concat(stand.ProjektAngebote.Values).Select(Objekt).Where(a => Py.Wahr(a["gewonnen"])).ToList();
        S("grossauftraege", (long)stand.ProjektAngebote.Values.Select(Objekt)
            .Count(a => Py.Wahr(a["gewonnen"]) && Py.Str(Get(a, "vorlage", "")).StartsWith(Wirtschaft.GrossPrefix, StringComparison.Ordinal)));
        S("projekte", (long)stand.FertigeProjekte().Count);
        S("angebote_serie", LaengsteSerie(angebote));
        S("mitbewerber_besiegt", (long)new PySet(gewonnen.Select(a => Py.Wahr(a["konkurrent"]) ? a["konkurrent"] : Bitweiche)).Count);
        PyObjekt? groesster = null;
        foreach (var a in gewonnen)
        {
            if (groesster is null || Netto(groesster) < Netto(a))
            {
                groesster = a;
            }
        }
        S("groesster_auftrag", groesster is null ? null : PythonRunden.Runde(Netto(groesster)));
        if (groesster is not null)
        {
            Py.Setzen(texte, "groesster_auftrag", Py.Wahr(groesster["titel"]) ? groesster["titel"]
                : Prozent("%s × %s", Get(groesster, "menge", 1L), Get(groesster, "artikel", "Artikel")));
        }
        var tage = stand.Kassenbuch.Keys.Where(t => hatFirma && !Py.Kleiner(t, firma!["tag"])).ToList();
        S("tagesumsatz", MaxListe(tage.Select(t =>
        {
            var ein = Objekt(Objekt(stand.Kassenbuch.Get(t))["ein"]);
            return Py.Summe(FirmenEinnahmen.Select(k => Get(ein, k, 0L)));
        }).Append(0L)));
        var kunden = (long)stand.TicketErgebnisse.Values.Select(Objekt).Count(t => Py.Wahr(t["erfolg"]));
        S("auftraege", Z("tickets") + kunden + Z("projekte"));
        S("tage_gruendung", hatFirma ? Py.Int(firma!["tag"]) : null);
        S("tage_stufe5", hatFirma && stand.AusbauTage.ContainsKey(5L) ? Py.Int(stand.AusbauTage.Get(5L)) - Py.Int(firma!["tag"]) : null);
        S("tage_filiale", hatFirma && Py.Wahr(zweig) ? Py.Int(zweig!["tag"]) - Py.Int(firma!["tag"]) : null);
        S("kredit_getilgt", (long)stand.KreditProtokoll.Cast<PyTupel>().Count(k => Py.Str(k[1]) is "zurueckgezahlt" or "abgeloest"));
        S("mahnung_vorbei", (long)stand.MahnUeberstanden.Count);
        S("mahnung_hoch_vorbei", (long)stand.MahnUeberstanden.Count(s => !Py.Kleiner(s, 2L)));
        var steuer = new List<bool>();
        foreach (var eintrag in stand.SteuerProtokoll.Cast<PyTupel>())
        {
            if (Py.Gleich(eintrag[1], "voranmeldung"))
            {
                steuer.Add(!Py.Wahr(Objekt(eintrag[2])["offen"]));
            }
            else if (Py.Gleich(eintrag[1], "ausfall"))
            {
                steuer.Add(false);
            }
        }
        S("steuer_serie", LaengsteSerie(steuer));
        long serie = 0, besteSerie = 0;
        long? vorher = null;
        foreach (var eintrag in stand.StimmungsProtokoll)
        {
            var tag = Py.Int(eintrag[0]);
            var gut = !Py.Kleiner(eintrag[1], (long)StimmungGut);
            if (gut && vorher is not null && tag == vorher + 1)
            {
                serie += 1;
            }
            else
            {
                serie = gut ? 1 : 0;
            }
            vorher = tag;
            besteSerie = Math.Max(besteSerie, serie);
        }
        S("stimmung_serie", besteSerie);
        S("stimmung_max", stand.StimmungsProtokoll.Count > 0 ? Py.Rund(MaxListe(stand.StimmungsProtokoll.Select(s => s[1]))) : null);
        S("weiterbildungen", (long)stand.Weiterbildungen.Select(Objekt).Count(w => !Py.Gleich(w["art"], Coaching)));
        S("experte", Py.Int(MaxListe(stand.Personal.Keys
            .Select(p => MaxListe(stand.PersonalThemen(p).Select(t => t.Value).Append(0L))).Append(0L))));
        var antworten = stand.Antworten.Values.Select(Objekt).ToList();
        S("geschlichtet", (long)antworten.Count(a => Py.Gleich(a["art"], Ereignisse.Konflikt) && Py.Gleich(a["wahl"], Schlichten)));
        S("abwerbung_abgewehrt", (long)antworten.Count(a => Py.Gleich(a["art"], Ereignisse.Rueckhol)
            && Py.Wahr(a["wahl"]) && !Py.Gleich(a["wahl"], RueckholGehen)));
        S("macke_gezaehmt", 0L);
        foreach (var person in stand.Personal.Keys)
        {
            var macke = stand.MackeVon(person);
            if (Py.Wahr(macke) && Py.Kleiner(1L, macke!["start"]) && Py.Gleich(macke["stufe"], 1L))
            {
                S("macke_gezaehmt", 1L);
                break;
            }
        }
        if (wissen is null)
        {
            S("themen_70", null);
            S("fachbereiche_70", null);
        }
        else
        {
            var t = SpielThemen.Jetzt;
            bool Gekonnt(string thema) => !Py.Kleiner(Get(wissen, thema, 0L), (long)WissenZiel);
            S("themen_70", (long)t.Reihenfolge.Count(Gekonnt));
            S("fachbereiche_70", (long)t.ThemenVon.Values.Count(themen => themen.Count > 0 && themen.All(Gekonnt)));
        }
        if (!hatFirma)
        {
            foreach (var k in new[] { "mitarbeiter", "tagesumsatz", "stimmung_max" })
            {
                S(k, Py.Wahr(m[k]) ? m[k] : null);
            }
        }
        return m;
    }

    private static double Netto(PyObjekt a) => Py.F(Py.Wahr(Get(a, "netto", 0L)) ? Get(a, "netto", 0L) : 0L);

    /// <summary>achievement_level: wie viele Stufen mit diesem Wert erreicht sind.</summary>
    public static long ErfolgStufe(PyObjekt regel, object? wert)
    {
        if (wert is null)
        {
            return 0;
        }
        long stufe = 0;
        foreach (var s in Liste(regel["stufen"]).Select(Objekt))
        {
            if (!Py.Kleiner(wert, Get(s, "ab", 1L)))
            {
                stufe += 1;
            }
            else
            {
                break;
            }
        }
        return stufe;
    }

    /// <summary>metric_text</summary>
    public static string KennzahlText(object? schluessel, object? wert)
    {
        if (wert is null)
        {
            return "–";
        }
        return EuroKennzahlen.Contains(Py.Str(schluessel)) ? Wohnung.GanzeEuro(wert) : GanzPunkt(wert);
    }

    /// <summary>stage_goal_text: was fuer eine Stufe noetig ist, z.B. "500" oder "Senior".</summary>
    public static string StufenZielText(PyObjekt regel, int index)
    {
        var stufen = Liste(regel["stufen"]);
        var s = Objekt(stufen[index]);
        if (Py.Wahr(s["text"]))
        {
            return Py.Str(s["text"]);
        }
        if (Py.Hat(s, "ab") && (!Py.Gleich(s["ab"], 1L) || stufen.Count > 1))
        {
            return KennzahlText(regel["wert"], s["ab"]);
        }
        return "";
    }

    /// <summary>record_value_text: Bestwert fuer die Anzeige (mit Einheit).</summary>
    public static string BestwertText(PyObjekt regel, object? wert)
    {
        if (wert is null)
        {
            return "–";
        }
        var einheit = regel["einheit"];
        if (Py.Gleich(einheit, "euro"))
        {
            return Wohnung.GanzeEuro(wert);
        }
        if (Py.Gleich(einheit, "tag"))
        {
            return Prozent("Tag %d", wert);
        }
        if (Py.Gleich(einheit, "tage"))
        {
            return TageText(Py.Int(wert));
        }
        return KennzahlText(regel["id"], wert);
    }

    /// <summary>_better</summary>
    public static bool Besser(PyObjekt regel, object? wert, object? anderer)
    {
        if (anderer is null)
        {
            return wert is not null;
        }
        if (wert is null)
        {
            return false;
        }
        return Py.Gleich(regel["besser"], "weniger") ? Py.Kleiner(wert, anderer) : Py.Kleiner(anderer, wert);
    }

    /// <summary>record_runs: Kennungen aller Durchgaenge in der Bestenliste, aelteste zuerst.</summary>
    public static List<string> BestenLaeufe(IEnumerable<BestenZeile> zeilen) =>
        [.. zeilen.Select(z => z.Lauf).Where(l => l.Length > 0).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>record_best: (Wert, Daten, Durchgang) des besten Eintrags zu einem Bestwert.</summary>
    public static (object? Wert, PyObjekt Daten, string? Lauf) BestenBester(IEnumerable<BestenZeile> zeilen, string schluessel, PyObjekt regel,
        ICollection<string>? laeufe = null, string? ohne = null)
    {
        (object? Wert, PyObjekt Daten, string? Lauf) beste = (null, [], null);
        foreach (var z in zeilen)
        {
            if (z.Art != BestWert || z.Schluessel != schluessel || z.Lauf == ohne || z.Wert is null)
            {
                continue;
            }
            if (laeufe is not null && !laeufe.Contains(z.Lauf))
            {
                continue;
            }
            if (Besser(regel, z.Wert, beste.Wert))
            {
                beste = (z.Wert, z.Daten, z.Lauf);
            }
        }
        return beste;
    }

    /// <summary>record_board: die Bestenliste fuer die Anzeige.</summary>
    public static PyObjekt BestenTafel(IReadOnlyList<BestenZeile> zeilen, Spielstand? stand, PyObjekt inhalt)
    {
        var laeufe = BestenLaeufe(zeilen);
        var aktuell = stand is not null && stand.Profil is not null ? stand.ErstesEreignis : null;
        if (Py.Wahr(aktuell) && !laeufe.Contains(aktuell!))
        {
            laeufe.Add(aktuell!);
            laeufe.Sort(StringComparer.Ordinal);
        }
        var nummer = laeufe.Select((l, i) => (l, i)).ToDictionary(p => p.l, p => (long)p.i + 1);
        var werte = Py.Wahr(aktuell) ? LaufWerte(stand!, null, inhalt) : [];
        var tage = new Dictionary<string, long>();
        var reihe = new List<string>();
        void Tage(string lauf, long wert)
        {
            if (!tage.ContainsKey(lauf))
            {
                reihe.Add(lauf);
            }
            tage[lauf] = Math.Max(tage.GetValueOrDefault(lauf, 0), wert);
        }
        foreach (var z in zeilen)
        {
            if (z.Art == BestWert && z.Schluessel == "diensttage" && z.Wert is { } w)
            {
                Tage(z.Lauf, (long)w);
            }
        }
        if (Py.Wahr(aktuell))
        {
            Tage(aktuell!, Py.Int(Py.Wahr(werte["diensttage"]) ? werte["diensttage"] : 0L));
        }
        var liste = new List<object?>();
        foreach (var regel in BestwertListe(inhalt))
        {
            var (wert, daten, lauf) = BestenBester(zeilen, Py.Str(regel["id"]), regel);
            var meins = Py.Wahr(aktuell) ? werte[Py.Str(regel["id"])] : null;
            if (!Py.Gleich(regel["besser"], "weniger") && !Py.Wahr(meins))
            {
                meins = null;
            }
            if (Py.Wahr(aktuell) && Besser(regel, meins, wert))
            {
                (wert, daten, lauf) = (meins, new PyObjekt
                {
                    { "tag", stand!.TageFertig }, { "text", Get(Objekt(werte["_texte"]), Py.Str(regel["id"]), "") },
                }, aktuell);
            }
            daten = Py.Wahr(daten) ? daten : [];
            liste.Add(new PyObjekt
            {
                { "id", regel["id"] }, { "name", regel["name"] }, { "text", Get(regel, "text", "") },
                { "wert", wert }, { "wert_text", BestwertText(regel, wert) },
                { "durchgang", lauf is not null && nummer.TryGetValue(lauf, out var n) ? n : null }, { "tag", daten["tag"] },
                { "extra", Get(daten, "text", "") },
                { "aktuell", meins }, { "aktuell_text", BestwertText(regel, meins) },
                { "rekord", Py.Wahr(aktuell) && lauf == aktuell && wert is not null },
            });
        }
        return new PyObjekt
        {
            { "durchgaenge", (long)laeufe.Count }, { "diensttage", reihe.Sum(l => tage[l]) },
            { "werte", liste }, { "aktuell", aktuell is not null && nummer.TryGetValue(aktuell, out var a) ? a : null },
        };
    }

    /// <summary>record_detail: zweite Zeile unter einem Bestwert (PC und Handy gleich).</summary>
    public static string BestenZeileText(PyObjekt eintrag, long laeufe)
    {
        if (eintrag["wert"] is null)
        {
            return Py.Wahr(eintrag["text"]) ? Py.Str(eintrag["text"]) : "noch nicht erreicht";
        }
        var teile = new List<string>();
        if (laeufe > 1)
        {
            if (Py.Wahr(eintrag["rekord"]))
            {
                teile.Add("in diesem Durchgang");
            }
            else
            {
                teile.Add(Py.Wahr(eintrag["durchgang"]) ? Prozent("%d. Durchgang", eintrag["durchgang"]) : "");
                if (eintrag["aktuell"] is not null)
                {
                    teile.Add(Prozent("jetzt %s", eintrag["aktuell_text"]));
                }
            }
        }
        teile.Add(Py.Str(Py.Wahr(eintrag["extra"]) ? eintrag["extra"] : eintrag["text"]));
        return string.Join(" · ", teile.Where(t => t.Length > 0));
    }

    /// <summary>record_subtitle</summary>
    public static string BestenUntertitel(PyObjekt tafel)
    {
        var laeufe = Py.Int(tafel["durchgaenge"]);
        return laeufe <= 1 ? "über alle Spielstände · dein erster Durchgang"
            : Prozent("über alle Spielstände · %d Durchgänge · %d Diensttage", laeufe, tafel["diensttage"]);
    }

    /// <summary>badge_status: (Zeile unter dem Namen, Farbe-Schluessel) fuer ein Abzeichen.</summary>
    public static (string Text, string Farbe) AbzeichenStatus(PyObjekt eintrag)
    {
        if (Py.Wahr(eintrag["geheim"]))
        {
            return ("noch nicht entdeckt", "muted");
        }
        var stufen = Liste(eintrag["stufen"]).Select(Objekt).ToList();
        var frueher = "";
        if (Py.Kleiner(eintrag["stufe"], eintrag["je"]))
        {
            frueher = Prozent(" · früher: %s", stufen[(int)Py.Int(eintrag["je"]) - 1]["name"]);
        }
        if (Py.Wahr(eintrag["nur_normal"]) && !Py.Wahr(eintrag["tier"]))
        {
            return ("nur auf Normal" + frueher, "muted");
        }
        var danach = "";
        if (eintrag["naechste"] is PyObjekt folgende && Py.Wahr(folgende))
        {
            var fortschritt = Py.Str(eintrag["fortschritt"]);
            const string Ziel = "nächstes Ziel: ";
            if (fortschritt == "Wissensstand wird geladen")
            {
                danach = fortschritt;
            }
            else if (fortschritt.StartsWith(Ziel, StringComparison.Ordinal))
            {
                danach = Prozent("%s bei: %s", folgende["name"], fortschritt[Ziel.Length..]);
            }
            else
            {
                danach = Prozent("%s: %s", folgende["name"], fortschritt.Length > 0 ? fortschritt : "noch offen");
            }
        }
        if (Py.Wahr(eintrag["tier"]))
        {
            var s = stufen[(int)Py.Int(eintrag["stufe"]) - 1];
            var text = Prozent("%s erreicht (Tag %d)", s["name"], Py.Wahr(s["erreicht"]) ? s["erreicht"] : 1L);
            if (danach.Length > 0)
            {
                text += " · " + danach;
            }
            return (text + frueher, Py.Str(eintrag["tier"]));
        }
        return ((danach.Length > 0 ? danach : "noch offen") + frueher, "muted");
    }

    /// <summary>badge_goals: die Ziele aller Stufen in einer Zeile.</summary>
    public static string AbzeichenZiele(PyObjekt eintrag)
    {
        if (Py.Wahr(eintrag["geheim"]))
        {
            return "";
        }
        return string.Join(" · ", Liste(eintrag["stufen"]).Select(Objekt).Select(s => PythonZeichen.Strip(Prozent("%s %s", s["name"], s["ziel"]))));
    }

    /// <summary>badge_history: (erfolg, stufe) -> Durchgaenge, in denen die Stufe erreicht wurde.</summary>
    public static PyDict AbzeichenVerlauf(IEnumerable<BestenZeile> zeilen)
    {
        var ergebnis = new PyDict();
        foreach (var z in zeilen)
        {
            if (z.Art != BestErfolg || !z.Schluessel.Contains(':', StringComparison.Ordinal))
            {
                continue;
            }
            var stelle = z.Schluessel.LastIndexOf(':');
            var stufe = z.Schluessel[(stelle + 1)..];
            if (stufe.Length > 0 && stufe.All(char.IsDigit) && long.TryParse(stufe, out var n))
            {
                ((PySet)ergebnis.SetDefault(new PyTupel(z.Schluessel[..stelle], n), new PySet())!).Add(z.Lauf);
            }
        }
        return ergebnis;
    }

    /// <summary>achievement_overview: alle Abzeichen fuer die Anzeige, in der Reihenfolge von erfolge.json.</summary>
    public static List<PyObjekt> ErfolgUebersicht(Spielstand stand, PyObjekt? wissen = null, IReadOnlyList<BestenZeile>? zeilen = null, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var werte = LaufWerte(stand, wissen, inhalt);
        var verlauf = AbzeichenVerlauf(zeilen ?? []);
        var ergebnis = new List<PyObjekt>();
        foreach (var regel in ErfolgListe(inhalt))
        {
            var stufen = new List<object?>();
            long erreichtStufe = 0;
            var regelStufen = Liste(regel["stufen"]).Select(Objekt).ToList();
            for (var i = 0; i < regelStufen.Count; i++)
            {
                var s = regelStufen[i];
                var erreicht = stand.Erfolge.Get(new PyTupel(regel["id"], (long)i + 1)) as PyObjekt;
                if (Py.Wahr(erreicht))
                {
                    erreichtStufe = i + 1;
                }
                stufen.Add(new PyObjekt
                {
                    { "tier", s["stufe"] }, { "name", StufenNamen[Py.Str(s["stufe"])] }, { "ziel", StufenZielText(regel, i) },
                    { "erreicht", Py.Wahr(erreicht) ? Py.Int(Py.Wahr(erreicht!["tag"]) ? erreicht["tag"] : 0L) : null },
                    { "moment", Py.Wahr(s["moment"]) },
                });
            }
            var je = verlauf.Where(p => Py.Gleich(((PyTupel)p.Key!)[0], regel["id"]) && ((PySet)p.Value!).Count > 0)
                .Select(p => Py.Int(((PyTupel)p.Key!)[1])).Append(erreichtStufe).Max();
            var laeufe = new PySet();
            foreach (var (schluessel, gefunden) in verlauf)
            {
                if (Py.Gleich(((PyTupel)schluessel!)[0], regel["id"]))
                {
                    laeufe.UnionWith((PySet)gefunden!);
                }
            }
            var wert = werte[Py.Str(regel["wert"])];
            var folgende = erreichtStufe < regelStufen.Count ? regelStufen[(int)erreichtStufe] : null;
            var fortschritt = "";
            var anteil = 1.0;
            if (folgende is not null)
            {
                var ziel = Get(folgende, "ab", 1L);
                var habe = Py.Wahr(wert) ? wert : 0L;
                anteil = Py.Wahr(ziel) ? Math.Max(0.0, Math.Min(1.0, Py.F(habe) / Py.F(ziel))) : 0.0;
                if (wert is null && Py.Wahr(regel["wissen"]))
                {
                    fortschritt = "Wissensstand wird geladen";
                }
                else if (StufenKennzahlen.Contains(Py.Str(regel["wert"])) && Py.Wahr(folgende["text"]))
                {
                    fortschritt = Prozent("nächstes Ziel: %s", folgende["text"]);
                }
                else if (Py.Kleiner(1L, ziel))
                {
                    fortschritt = Prozent("%s / %s", KennzahlText(regel["wert"], Py.Min(habe, ziel)), KennzahlText(regel["wert"], ziel));
                }
            }
            ergebnis.Add(new PyObjekt
            {
                { "id", regel["id"] }, { "name", regel["name"] }, { "gruppe", regel["gruppe"] },
                { "text", regel["text"] }, { "bild", regel["bild"] }, { "farbe", Get(regel, "farbe", "") },
                { "ort", regel["ort"] }, { "stufe", erreichtStufe },
                { "tier", erreichtStufe > 0 ? regelStufen[(int)erreichtStufe - 1]["stufe"] : null },
                { "stufen", stufen },
                { "naechste", erreichtStufe < stufen.Count ? stufen[(int)erreichtStufe] : null },
                { "fortschritt", fortschritt }, { "anteil", anteil },
                { "balken", fortschritt.Length > 0 && !StufenKennzahlen.Contains(Py.Str(regel["wert"]))
                    && !(wert is null && Py.Wahr(regel["wissen"])) && !(Py.Wahr(regel["nur_normal"]) && stand.Leicht) },
                { "geheim", Py.Wahr(regel["geheim"]) && je == 0 },
                { "nur_normal", Py.Wahr(regel["nur_normal"]) && stand.Leicht },
                { "je", je }, { "durchgaenge", (long)laeufe.Count },
            });
        }
        return ergebnis;
    }

    /// <summary>achievement_counts: (erreichte Stufen, alle Stufen, {tier: Anzahl}).</summary>
    public static (long Erreicht, long Alle, PyObjekt Stufen) ErfolgZaehler(IEnumerable<PyObjekt> eintraege)
    {
        var stufen = new PyObjekt();
        foreach (var s in StufenFolge)
        {
            stufen.Add(s, 0L);
        }
        long erreicht = 0, alle = 0;
        foreach (var e in eintraege)
        {
            var liste = Liste(e["stufen"]).Select(Objekt).ToList();
            alle += liste.Count;
            foreach (var s in liste.Where(s => s["erreicht"] is not null))
            {
                erreicht += 1;
                Py.Setzen(stufen, Py.Str(s["tier"]), Py.Int(stufen[Py.Str(s["tier"])]) + 1);
            }
        }
        return (erreicht, alle, stufen);
    }

    /// <summary>achievement_groups</summary>
    public static List<(string Id, string Name)> ErfolgGruppen(PyObjekt inhalt) =>
        [("alle", "Alle"), .. ListeOderLeer(ErfolgRegeln(inhalt), "gruppen").Select(Liste).Select(g => (Py.Str(g[0]), Py.Str(g[1])))];

    /// <summary>unlock_info: was der Meilenstein-Moment (oder der kurze Hinweis) zu einer neuen Stufe zeigt.</summary>
    public static PyObjekt FreischaltInfo(Spielstand stand, PyObjekt regel, long stufe, IReadOnlyList<BestenZeile> zeilen, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var s = Objekt(Liste(regel["stufen"])[(int)stufe - 1]);
        var laeufe = AbzeichenVerlauf(zeilen).Get(new PyTupel(regel["id"], stufe)) as PySet ?? [];
        var andere = BestenLaeufe(zeilen).Where(l => l != stand.ErstesEreignis).ToList();
        var erstes = laeufe.All(l => Py.Gleich(l, stand.ErstesEreignis));
        var ziel = StufenZielText(regel, (int)stufe - 1);
        var text = Py.Str(regel["text"]);
        if (ziel.Length > 0)
        {
            text = Prozent("%s: %s", PythonZeichen.Strip(text, ".", links: false), ziel);
        }
        if (Py.Wahr(s["moment"]) && Py.Wahr(regel["moment_text"]))
        {
            text = Py.Format(Py.Str(regel["moment_text"]), new PyObjekt
            {
                { "ziel", ziel }, { "tag", stand.Tag }, { "spieler", Get(stand.Profil ?? [], "name", "") },
                { "firma", Py.Wahr(stand.Firma) ? Objekt(stand.Firma)["name"] : "Deine Firma" },
                { "filiale", Py.Wahr(stand.Zweigstelle) ? Objekt(stand.Zweigstelle)["name"] : "Die Filiale" },
            });
        }
        var bestwert = "";
        var schluessel = regel["bestwert"];
        if (Py.Wahr(schluessel) && andere.Count > 0)
        {
            var regelBest = BestwertRegel(schluessel, inhalt)!;
            var meins = LaufWerte(stand, null, inhalt)[Py.Str(schluessel)];
            var (wert, _, _) = BestenBester(zeilen, Py.Str(schluessel), regelBest, ohne: stand.ErstesEreignis);
            if (wert is not null && Besser(regelBest, meins, wert))
            {
                bestwert = "Neuer Bestwert! Bisher: " + BestwertText(regelBest, wert);
            }
        }
        var tier = Py.Str(s["stufe"]);
        return new PyObjekt
        {
            { "erfolg", regel["id"] }, { "stufe", stufe }, { "name", regel["name"] },
            { "tier", s["stufe"] }, { "tier_name", StufenNamen[tier] },
            { "text", text }, { "moment", Py.Wahr(s["moment"]) },
            { "erstes_mal", erstes && andere.Count > 0 }, { "bestwert", bestwert },
            { "bild", regel["bild"] }, { "farbe", Get(regel, "farbe", "") }, { "ort", regel["ort"] },
            { "hinweis", Prozent("Abzeichen: %s · %s", regel["name"], StufenNamen[tier]) },
        };
    }

    /// <summary>story_done_day: Arbeitstag des letzten Bitweiche-Auftrags (sehr gross, solange einer offen ist).</summary>
    public static long StoryFertigTag(Spielstand stand)
    {
        if (!stand.AllesErledigt())
        {
            return 1_000_000_000;
        }
        var tage = stand.Verlauf.Where(e => e.Art == Ereignisse.Erledigt && Py.Wahr(e.Daten["richtig"]) && Py.IstInt(e.Daten["tag"]))
            .Select(e => Py.Int(Py.Wahr(e.Daten["tag"]) ? e.Daten["tag"] : 0L)).ToList();
        return tage.Count > 0 ? tage.Max() : 0;
    }

    /// <summary>new_achievements: erreichte, aber noch nicht festgehaltene Stufen [(regel, stufe)].</summary>
    public static List<(PyObjekt Regel, long Stufe)> NeueErfolge(Spielstand stand, PyObjekt? wissen = null, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var werte = LaufWerte(stand, wissen, inhalt);
        var ergebnis = new List<(PyObjekt, long)>();
        foreach (var regel in ErfolgListe(inhalt))
        {
            if (Py.Wahr(regel["wissen"]) && wissen is null)
            {
                continue;
            }
            for (long stufe = 1; stufe <= ErfolgStufe(regel, werte[Py.Str(regel["wert"])]); stufe++)
            {
                if (!stand.Erfolge.ContainsKey(new PyTupel(regel["id"], stufe)))
                {
                    ergebnis.Add((regel, stufe));
                }
            }
        }
        return ergebnis;
    }

    /// <summary>
    /// record_rows: Zeilen, die fuer den laufenden Durchgang neu in die Bestenliste gehoeren
    /// [(art, schluessel, wert, daten)]. Ein besserer Wert nur, wenn es noch keinen Eintrag gibt,
    /// der letzte RECORD_EVERY Arbeitstage her ist oder erzwungen (vor dem Zuruecksetzen).
    /// </summary>
    public static List<(string Art, string Schluessel, object? Wert, PyObjekt Daten)> BestenNeu(Spielstand stand, IReadOnlyList<BestenZeile> zeilen,
        PyObjekt? inhalt = null, bool erzwingen = false)
    {
        inhalt ??= stand.Inhalt;
        var lauf = stand.ErstesEreignis;
        if (!Py.Wahr(lauf) || stand.Profil is null)
        {
            return [];
        }
        var meine = zeilen.Where(z => z.Lauf == lauf).ToList();
        var ergebnis = new List<(string, string, object?, PyObjekt)>();
        if (!meine.Any(z => z.Art == BestDurchgang))
        {
            ergebnis.Add((BestDurchgang, "start", null, new PyObjekt { { "name", stand.Profil["name"] } }));
        }
        var werte = LaufWerte(stand, null, inhalt);
        var regeln = BestwertListe(inhalt).Concat(BestenSummen.Select(k => new PyObjekt { { "id", k }, { "besser", "mehr" } }));
        foreach (var regel in regeln)
        {
            var id = Py.Str(regel["id"]);
            var wert = werte[id];
            if (wert is null || (!Py.Gleich(regel["besser"], "weniger") && !Py.Wahr(wert)))
            {
                continue;
            }
            var (beste, _, _) = BestenBester(meine, id, regel);
            var letzte = meine.Where(z => z.Art == BestWert && z.Schluessel == id)
                .Select(z => Py.Int(Py.Wahr(z.Daten["tag"]) ? z.Daten["tag"] : 0L)).Append(-BestenAbstand).Max();
            var faellig = erzwingen || beste is null || stand.TageFertig - letzte >= BestenAbstand;
            if (faellig && Besser(regel, wert, beste))
            {
                ergebnis.Add((BestWert, id, wert, new PyObjekt
                {
                    { "tag", stand.TageFertig }, { "text", Get(Objekt(werte["_texte"]), id, "") },
                }));
            }
        }
        var habe = meine.Where(z => z.Art == BestErfolg).Select(z => z.Schluessel).ToHashSet(StringComparer.Ordinal);
        var erfolge = stand.Erfolge.Select(p => ((PyTupel)p.Key!, Objekt(p.Value)))
            .OrderBy(p => Py.Str(p.Item1[0]), StringComparer.Ordinal).ThenBy(p => Py.Int(p.Item1[1]));
        foreach (var (schluessel, daten) in erfolge)
        {
            var text = Prozent("%s:%d", schluessel[0], schluessel[1]);
            if (!habe.Contains(text))
            {
                ergebnis.Add((BestErfolg, text, schluessel[1], new PyObjekt { { "tag", daten["tag"] } }));
            }
        }
        return ergebnis;
    }
}
