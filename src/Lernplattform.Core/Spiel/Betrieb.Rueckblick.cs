using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Rechner;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: Rueckblick "Meine Reise" aus <c>fisi_game.py</c> (Python 0.63.2, Z. 11842-12162):
/// Ereignistagebuch (journey), Filter und Statistik.
/// </summary>
public static partial class Betrieb
{
    public const string ReiseStory = "story";       // JOURNEY_STORY
    public const string ReiseKarriere = "karriere"; // JOURNEY_CAREER
    public const string ReiseFirma = "firma";       // JOURNEY_FIRM
    public const int ReiseTextMax = 220;            // JOURNEY_TEXT_MAX

    /// <summary>TICKET_MILESTONES</summary>
    public static readonly IReadOnlySet<long> TicketMeilensteine = new HashSet<long> { 1, 10, 25, 50, 100, 150, 200, 300, 400, 500, 750, 1000 };

    /// <summary>_short: Leerraum zusammenfassen und auf JOURNEY_TEXT_MAX Zeichen kuerzen.</summary>
    public static string ReiseKurz(object? text, int grenze = ReiseTextMax)
    {
        var t = string.Join(" ", PythonZeichen.Split(Py.Wahr(text) ? Py.Str(text) : ""));
        if (PythonZeichen.Laenge(t) <= grenze)
        {
            return t;
        }
        var teil = PythonZeichen.Teil(t, 0, grenze - 1);
        var stelle = teil.LastIndexOf(' ');
        return (stelle < 0 ? teil : teil[..stelle]) + " …";
    }

    /// <summary>_story_text: erster Absatz eines Story-Textes (Liste von Zeilen oder Text).</summary>
    public static string StoryText(object? wert)
    {
        if (wert is List<object?> liste)
        {
            wert = liste.Count > 0 ? liste[0] : "";
            if (wert is List<object?> innen)
            {
                wert = innen.Count > 0 ? innen[0] : "";
            }
        }
        return (Py.Wahr(wert) ? Py.Str(wert) : "").Split('\n')[0];
    }

    /// <summary>journey: das Ereignistagebuch, aelteste zuerst; je Eintrag {"tag", "gruppe", "art", "titel", "text"}.</summary>
    public static List<PyObjekt> Reise(Spielstand stand, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        var eintraege = new List<PyObjekt>();
        var nummern = new List<long>();
        void Neu(object? tag, string gruppe, string art, string titel, object? text = null)
        {
            nummern.Add(eintraege.Count);
            eintraege.Add(new PyObjekt
            {
                { "tag", Py.Int(tag) }, { "gruppe", gruppe }, { "art", art }, { "titel", titel }, { "text", ReiseKurz(text ?? "") },
            });
        }
        var namen = Liste(stand.Balancing["raenge"]).Select(r => Objekt(r)["name"]).ToList();
        var bitweiche = Objekt(inhalt["gebaeude"])["firma"];
        long tagNr = 1, geloest = 0, zwischenfaelle = 0, gewonnen = 0;
        var begonnen = false;
        // Python ueberschreibt "names" im Konflikt-Zweig mit _pair_names (ein dict): danach gilt in
        // der Rang-Schleife jeder Rang als Stufe 0 (wie Python, Sammelliste)
        var namenUeberschrieben = false;
        var eingestellt = new PyDict();
        var beantwortet = new PySet();
        string Name(PyObjekt d) => Py.Str(Get(d, "name", ""));
        foreach (var e in stand.Verlauf)
        {
            var (art, d) = (e.Art, e.Daten);
            var tag = Py.IstInt(d["tag"]) && Py.Wahr(d["tag"]) ? Py.Int(d["tag"]) : tagNr;
            if (art == Ereignisse.Profil && !begonnen)
            {
                begonnen = true;
                Neu(tag, ReiseKarriere, "start", "Erster Arbeitstag",
                    Prozent("%s fängt bei %s an.", Py.Wahr(d["name"]) ? d["name"] : "Deine Figur", bitweiche));
            }
            else if (art == Ereignisse.Erledigt && Py.Wahr(d["zwischenfall"]))
            {
                var auftrag = Auftrag(d["aufgabe"], inhalt) ?? [];
                string titel;
                if (Py.Wahr(d["richtig"]))
                {
                    zwischenfaelle += 1;
                    titel = zwischenfaelle == 1 ? "Erster Zwischenfall gemeistert" : "Zwischenfall gemeistert";
                }
                else
                {
                    titel = "Zwischenfall nicht gemeistert";
                }
                Neu(tag, ReiseKarriere, "zwischenfall", titel, Get(auftrag, "titel", ""));
            }
            else if (art == Ereignisse.Erledigt && Py.Wahr(d["richtig"]))
            {
                geloest += 1;
                if (TicketMeilensteine.Contains(geloest))
                {
                    Neu(tag, ReiseKarriere, "meilenstein", geloest == 1 ? "Erstes Ticket gelöst" : Prozent("%d Tickets gelöst", geloest),
                        geloest == 1 ? Get(Auftrag(d["aufgabe"], inhalt) ?? [], "titel", "") : "");
                }
            }
            else if (art == Ereignisse.Umzug)
            {
                var bleibe = Wohnung.Bleibe(d["wohnung"], inhalt) ?? [];
                Neu(tag, ReiseKarriere, "umzug", Prozent("Umzug: %s", Get(bleibe, "name", "neue Wohnung")),
                    Py.Wahr(d["miete"]) ? Prozent("Gemietet für %s pro Arbeitstag.", Wohnung.GanzeEuro(d["miete"])) : "Gekauft.");
            }
            else if (art == Ereignisse.Gegruendet && Py.Wahr(d["name"]))
            {
                if (!eintraege.Any(x => Py.Gleich(x["art"], "gruendung")))
                {
                    Neu(tag, ReiseFirma, "gruendung", Prozent("Firma gegründet: %s", d["name"]),
                        Prozent("Abschied von %s - ab jetzt bist du dein eigener Chef.", bitweiche));
                }
            }
            else if (art == Ereignisse.Eingestellt && Py.Wahr(d["id"]) && !eingestellt.ContainsKey(d["id"]))
            {
                eingestellt[d["id"]] = Get(d, "name", "");
                Neu(tag, ReiseFirma, "einstellung", Prozent("Eingestellt: %s", Get(d, "name", "")), Get(d, "rolle", ""));
            }
            else if (art == Ereignisse.Gekuendigt && eingestellt.ContainsKey(d["id"]))
            {
                Neu(tag, ReiseFirma, "kuendigung", Prozent("Gekündigt: %s", eingestellt.Get(d["id"])));
            }
            else if (art == Ereignisse.Weiterbildung && Py.Gleich(d["art"], Coaching) && eingestellt.ContainsKey(d["id"]))
            {
                var macke = MackeNach(d["macke"], inhalt) ?? [];
                Neu(tag, ReiseFirma, "coaching", Prozent("Coaching: %s", eingestellt.Get(d["id"])),
                    Prozent("Gegen die Macke „%s“.", Get(macke, "name", "")));
            }
            else if (art is Ereignisse.UrlaubJa or Ereignisse.UrlaubNein && Py.Wahr(d["anfrage"]) && !beantwortet.Contains(d["anfrage"]))
            {
                beantwortet.Add(d["anfrage"]);
                var ok = art == Ereignisse.UrlaubJa;
                Neu(tag, ReiseFirma, art, Prozent("Urlaub %s: %s", ok ? "genehmigt" : "abgelehnt", Name(d)),
                    Prozent("Arbeitstag %s bis %s.", d["von"], Py.Int(Get(d, "bis", 1L)) - 1));
            }
            else if (art == Ereignisse.Rueckhol && Py.Wahr(d["rueckhol"]) && !beantwortet.Contains(d["rueckhol"]))
            {
                beantwortet.Add(d["rueckhol"]);
                var firma = Mitbewerber(Py.Wahr(d["firma"]) ? d["firma"] : Bitweiche, inhalt)["kurz"];
                Neu(tag, ReiseFirma, Ereignisse.Rueckhol,
                    Py.Gleich(d["wahl"], RueckholGehen) ? Prozent("Zurück zu %s: %s", firma, Name(d)) : Prozent("Abwerbeversuch abgewehrt: %s", Name(d)),
                    RueckholWahlText(d, inhalt));
            }
            else if (art == Ereignisse.Konflikt && Py.Wahr(d["konflikt"]) && !beantwortet.Contains(d["konflikt"]))
            {
                beantwortet.Add(d["konflikt"]);
                var paar = PaarNamen(Get(d, "name_a", ""), Get(d, "name_b", ""));
                namenUeberschrieben = true;
                Neu(tag, ReiseFirma, Ereignisse.Konflikt, Prozent("Streit: %s und %s", paar["a"], paar["b"]), KonfliktWahlText(d));
            }
            else if (art == Ereignisse.Erweitert)
            {
                var firma = Py.Wahr(inhalt["firma"]) ? Objekt(inhalt["firma"]) : [];
                var gebaeude = Get(firma, "gebaeude", new PyObjekt()) as PyObjekt ?? [];
                var stufen = Py.Wahr(gebaeude["stufen"]) ? Liste(gebaeude["stufen"]) : [];
                var stufe = stufen.Select(Objekt).FirstOrDefault(s => Py.Int(Get(s, "stufe", 0L)) == Py.Int(Get(d, "stufe", 0L))) ?? [];
                Neu(tag, ReiseFirma, "ausbau", Prozent("Gebäude ausgebaut: %s", Get(stufe, "name", Prozent("Stufe %s", d["stufe"]))),
                    Prozent("Ausbaustufe %s", d["stufe"]));
            }
            else if (art == Ereignisse.Raum)
            {
                var raum = SonderRaum(d["raum"], inhalt) ?? [];
                Neu(tag, ReiseFirma, "raum", Prozent("Neuer Raum: %s", Get(raum, "name", d["raum"])));
            }
            else if (art == Ereignisse.Filiale)
            {
                var stufe = FilialStufe(d["stufe"], inhalt) ?? [];
                if (Py.Int(Get(d, "stufe", 0L)) == 1 && Py.Wahr(d["name"]))
                {
                    if (!eintraege.Any(x => Py.Gleich(x["art"], "filiale")))
                    {
                        Neu(tag, ReiseFirma, "filiale", Prozent("Filiale eröffnet: %s", d["name"]),
                            Prozent("Zweiter Standort: %s.", Get(FilialRegeln(inhalt), "name", "Lindenau")));
                    }
                }
                else
                {
                    Neu(tag, ReiseFirma, "ausbau", Prozent("Filiale ausgebaut: %s", Get(stufe, "name", Prozent("Stufe %s", d["stufe"]))),
                        Prozent("Ausbaustufe %s", d["stufe"]));
                }
            }
            else if (art == Ereignisse.Erfolg && !Py.Wahr(d["nachgetragen"]))
            {
                var regel = ErfolgRegel(d["erfolg"], inhalt);
                var stufe = Py.Int(Py.Wahr(Get(d, "stufe", 0L)) ? Get(d, "stufe", 0L) : 0L);
                if (regel is not null && Py.Wahr(regel))
                {
                    var stufen = Liste(regel["stufen"]);
                    var schluessel = new PyTupel(d["erfolg"], stufe);
                    if (stufe > 0 && stufe <= stufen.Count && Py.Wahr(Objekt(stufen[(int)stufe - 1])["moment"]) && !beantwortet.Contains(schluessel))
                    {
                        beantwortet.Add(schluessel);
                        var tier = Py.Str(Objekt(stufen[(int)stufe - 1])["stufe"]);
                        Neu(tag, Py.Gleich(regel["gruppe"], "karriere") || Py.Gleich(regel["gruppe"], "wissen") ? ReiseKarriere : ReiseFirma, "erfolg",
                            Prozent("Abzeichen: %s · %s", regel["name"], StufenNamen[tier]), regel["text"]);
                    }
                }
            }
            else if (art == Ereignisse.Versetzt && eingestellt.ContainsKey(d["id"]))
            {
                Neu(tag, ReiseFirma, "versetzung", Prozent("Versetzt: %s", eingestellt.Get(d["id"])),
                    Prozent("Arbeitet jetzt %s.", Py.Gleich(d["standort"], OrtFiliale) ? "in der Filiale" : "im Gewerbehof Am Stellwerk"));
            }
            else if (art is Ereignisse.AngebotGewonnen or Ereignisse.AngebotVerloren)
            {
                var kunde = Get(FirmaKunde(d["kunde"], inhalt) ?? [], "name", "einen Kunden");
                var was = Prozent("%s × %s", Get(d, "menge", 1L), Get(d, "artikel", "Artikel"));
                if (art == Ereignisse.AngebotGewonnen)
                {
                    gewonnen += 1;
                    Neu(tag, ReiseFirma, "angebot", gewonnen == 1 ? "Erstes Angebot gewonnen" : "Angebot gewonnen",
                        Prozent("%s für %s, Gewinn %s.", was, kunde, Wohnung.GanzeEuro(Get(d, "geld", 0L))));
                }
                else
                {
                    var grund = Py.Gleich(d["grund"], "rechenfehler") ? "Fehler in der Kalkulation"
                        : Prozent("%s war günstiger", Mitbewerber(Py.Wahr(d["konkurrent"]) ? d["konkurrent"] : Bitweiche, inhalt)["kurz"]);
                    Neu(tag, ReiseFirma, "angebot_verloren", "Angebot verloren", Prozent("%s für %s: %s.", was, kunde, grund));
                }
            }
            else if (art == Ereignisse.ProjektGewonnen)
            {
                Neu(tag, ReiseFirma, "projekt", Prozent("Projekt gewonnen: %s", Get(d, "titel", "")), Get(d, "kunde", ""));
            }
            else if (art == Ereignisse.ProjektVerloren)
            {
                Neu(tag, ReiseFirma, "projekt_verloren", Prozent("Projekt verloren: %s", Get(d, "titel", "")), Get(d, "kunde", ""));
            }
            else if (art == Ereignisse.Feierabend)
            {
                tagNr += 1;
                var firma = Py.Wahr(d["firma"]) ? Objekt(d["firma"]) : [];
                foreach (var eintrag in (Py.Wahr(firma["personal"]) ? Liste(firma["personal"]) : []).Select(Objekt))
                {
                    var was = eintrag["art"];
                    if (Py.Gleich(was, PersonalKrank))
                    {
                        Neu(tag, ReiseFirma, PersonalKrank, Prozent("Krank: %s", Get(eintrag, "name", "")),
                            Prozent("Fällt %s aus.", TageText(Py.Int(eintrag["bis"]) - Py.Int(eintrag["von"]))));
                    }
                    else if (Py.Gleich(was, PersonalKuendigt))
                    {
                        Neu(tag, ReiseFirma, PersonalKuendigt, Prozent("Selbst gekündigt: %s", Get(eintrag, "name", "")),
                            "Die Stimmung war zu lange im Keller.");
                    }
                    else if (Py.Gleich(was, PersonalSchwaecher))
                    {
                        Neu(tag, ReiseFirma, PersonalSchwaecher, Prozent("Macke schwächer: %s", Get(eintrag, "name", "")),
                            Prozent("„%s“ jetzt %s.", Get(eintrag, "macke", ""), Get(eintrag, "stufe_name", "")));
                    }
                }
                foreach (var eintrag in (Py.Wahr(firma["projekte"]) ? Liste(firma["projekte"]) : []).Select(Objekt))
                {
                    if (!Py.Wahr(eintrag["fertig"]))
                    {
                        continue;
                    }
                    var projekt = stand.Projekte.Get(eintrag["projekt"]) as PyObjekt ?? [];
                    var t = Get(eintrag, "tag", tag);
                    if (Py.Gleich(projekt["fertig"], Py.Int(Py.Wahr(t) ? t : tag)))
                    {
                        Neu(tag, ReiseFirma, "projekt_fertig", Prozent("Projekt abgeschlossen: %s", Get(projekt, "titel", "")), Get(projekt, "kunde", ""));
                    }
                }
            }
        }
        // Kredite (ab 0.41): aufgenommen, zurueckgezahlt, geplatzte Raten
        foreach (var eintrag in stand.KreditProtokoll.Cast<PyTupel>())
        {
            var (tag, was, kennung, d) = (eintrag[0], Py.Str(eintrag[1]), eintrag[2], Objekt(eintrag[3]));
            var kredit = stand.Kredite.Get(kennung) as PyObjekt ?? [];
            if (was == "aufgenommen")
            {
                Neu(tag, ReiseFirma, "kredit", Prozent("Kredit aufgenommen: %s", Get(kredit, "name", "")),
                    Prozent("%s über %s, %s p. a.", Wohnung.GanzeEuro(Get(kredit, "summe", 0L)), Wirtschaft.LaufzeitText(Get(kredit, "laufzeit", 0L)),
                        Wirtschaft.ProzentText(Get(kredit, "zins", 0L))));
            }
            else if (was is "zurueckgezahlt" or "abgeloest")
            {
                Neu(tag, ReiseFirma, "kredit_ende",
                    Prozent(was == "abgeloest" ? "Kredit abgelöst: %s" : "Kredit zurückgezahlt: %s", Get(kredit, "name", "")),
                    Prozent("Zinsen insgesamt %s.", Wohnung.GanzeEuro(Get(kredit, "bezahlt_zins", 0L))));
            }
            else if (was == "ausfall" && Py.Wahr(d["einfach"]))
            {
                Neu(tag, ReiseFirma, "kredit_ausfall", "Kreditrate ausgesetzt",
                    Prozent("%s: Das Konto hat nicht gereicht, die Rate hängt hinten an.", Get(kredit, "name", "")));
            }
            else if (was == "ausfall")
            {
                var schritt = Wirtschaft.MahnStufe(Py.Int(Get(d, "stufe", 1L)), inhalt) ?? [];
                Neu(tag, ReiseFirma, "kredit_ausfall", "Kreditrate geplatzt",
                    Prozent("%s: Mahnstufe %d (%s).", Get(kredit, "name", ""), Get(d, "stufe", 1L), Get(schritt, "name", "")));
            }
        }
        // Ab 0.42: Werbung, Zertifizierungen und Umsatzsteuer
        foreach (var d in stand.Werbungen.Select(Objekt))
        {
            var form = Wirtschaft.WerbeForm(d["werbung"], inhalt) ?? [];
            Neu(Get(d, "tag", 1L), ReiseFirma, "werbung", Prozent("Werbung gebucht: %s", Get(form, "kurz", d["werbung"])),
                Py.Gleich(d["art"], Wirtschaft.WerbungEinmalig) ? Prozent("Einmalig für %s.", Wohnung.GanzeEuro(-Py.Int(Get(d, "geld", 0L))))
                    : Prozent("Dauerhaft für %s pro Arbeitstag.", Wohnung.GanzeEuro(Get(d, "kosten", 0L))));
        }
        foreach (var (kennung, wert) in stand.Zertifikate)
        {
            var d = Objekt(wert);
            if (Py.Int(Get(d, "bis_tag", 0L)) <= stand.Tag)
            {
                var zert = Wirtschaft.Zertifikat(kennung, inhalt) ?? [];
                Neu(Py.Int(d["bis_tag"]) - 1, ReiseFirma, "zertifizierung", Prozent("Zertifiziert: %s", Get(zert, "name", kennung)),
                    Wirtschaft.VorteilText(Py.Wahr(zert["vorteil"]) ? Objekt(zert["vorteil"]) : []));
            }
        }
        var gemeldet = 0;
        foreach (var eintrag in stand.SteuerProtokoll.Cast<PyTupel>())
        {
            var (tag, was, d) = (eintrag[0], Py.Str(eintrag[1]), Objekt(eintrag[2]));
            if (was == "voranmeldung")
            {
                gemeldet += 1;
                if (gemeldet == 1)
                {
                    Neu(tag, ReiseFirma, "steuer", "Erste Umsatzsteuer-Voranmeldung", Wirtschaft.VoranmeldungText(d) + ".");
                }
            }
            else if (was == "ausfall")
            {
                var schritt = Wirtschaft.MahnStufe(Py.Int(Get(d, "stufe", 1L)), inhalt) ?? [];
                Neu(tag, ReiseFirma, "steuer_ausfall", "Umsatzsteuer nicht bezahlt",
                    Prozent("Mahnstufe %d (%s).", Get(d, "stufe", 1L), Get(schritt, "name", "")));
            }
        }
        // Ab 0.44: Konjunktur und Trends (Story), Gegenwind der Mitbewerber (Firma)
        foreach (var (tag, was, titel, text) in MarktEreignisse(stand, inhalt))
        {
            Neu(tag, was.StartsWith("wettbewerb", StringComparison.Ordinal) ? ReiseFirma : ReiseStory, was, titel, text);
        }
        // Ab 0.52: Grossauftrag Serverfarm
        foreach (var (tag, was, titel, text) in FarmReise(stand, inhalt))
        {
            Neu(tag, ReiseFirma, was, titel, text);
        }
        long vorher = 0;
        foreach (var eintrag in stand.RangProtokoll)
        {
            var rang = eintrag[1];
            var index = namenUeberschrieben ? -1 : namen.FindIndex(n => Py.Gleich(n, rang));
            long stufe = index < 0 ? 0 : index;
            Neu(eintrag[0], ReiseKarriere, "rang", Prozent(stufe > vorher ? "Befördert: %s" : "Zurückgestuft: %s", rang));
            vorher = stufe;
        }
        var story = Objekt(inhalt["story"]);
        foreach (var (schluessel, wert) in Py.Wahr(story["tage"]) ? Objekt(story["tage"]) : [])
        {
            if (PythonZeichen.IstZiffernText(schluessel) && PythonZeichen.Ganzzahl(schluessel, out var n) && n <= stand.Tag && begonnen)
            {
                Neu((long)n, ReiseStory, "story", "Arbeitstag " + schluessel, StoryText(wert));
            }
        }
        // Story-Momente stehen am Morgen, also vor allem anderen des Tages
        return [.. eintraege.Select((e, i) => (e, i))
            .OrderBy(p => Py.Int(p.e["tag"])).ThenBy(p => Py.Gleich(p.e["art"], "story") ? 0 : 1).ThenBy(p => p.i)
            .Select(p => p.e)];
    }

    /// <summary>journey_filter</summary>
    public static List<PyObjekt> ReiseFilter(IEnumerable<PyObjekt> eintraege, string? gruppe) =>
        gruppe is null or "alle" ? [.. eintraege] : [.. eintraege.Where(e => Py.Gleich(e["gruppe"], gruppe))];

    /// <summary>journey_stats: Rueckblick-Statistik aus dem Protokoll.</summary>
    public static PyObjekt ReiseStatistik(Spielstand stand, PyObjekt? inhalt = null)
    {
        inhalt ??= stand.Inhalt;
        long tickets = 0, richtig = 0, zwischenfaelle = 0, zwischenRichtig = 0, verschoben = 0;
        var t = SpielThemen.Jetzt;
        var jeBereich = new PyObjekt();
        foreach (var k in t.Fachbereiche)
        {
            jeBereich.Add(k, 0L);
        }
        var jeTag = new Dictionary<long, long[]>();
        foreach (var e in stand.Verlauf)
        {
            var d = e.Daten;
            if (e.Art == Ereignisse.Verschoben)
            {
                verschoben += 1;
            }
            if (e.Art != Ereignisse.Erledigt)
            {
                continue;
            }
            var tag = Py.IstInt(d["tag"]) ? Py.Int(d["tag"]) : 0;
            if (Py.Wahr(d["zwischenfall"]))
            {
                zwischenfaelle += 1;
                zwischenRichtig += Py.Wahr(d["richtig"]) ? 1 : 0;
                continue;
            }
            tickets += 1;
            if (!jeTag.TryGetValue(tag, out var eimer))
            {
                jeTag[tag] = eimer = [0, 0];
            }
            if (Py.Wahr(d["richtig"]))
            {
                richtig += 1;
                eimer[0] += 1;
                var auftrag = Auftrag(d["aufgabe"], inhalt) ?? [];
                if (auftrag["cat"] is string cat && Py.Hat(jeBereich, cat))
                {
                    Py.Setzen(jeBereich, cat, Py.Int(jeBereich[cat]) + 1);
                }
            }
            else
            {
                eimer[1] += 1;
            }
        }
        var kunden = stand.TicketErgebnisse.Values.Select(Objekt).Count(e => Py.Wahr(e["erfolg"]));
        var angebote = stand.Angebote.Values.Concat(stand.ProjektAngebote.Values).Select(Objekt).ToList();
        var verdient = Py.Summe(stand.Kassenbuch.Values.Select(tag => Py.Summe(Objekt(Objekt(tag)["ein"]).Select(p => p.Value))));
        var tage = jeTag.Keys.Where(k => k != 0).Order().ToList();
        tage = tage.Skip(Math.Max(0, tage.Count - 30)).ToList();
        var ansehen = stand.TagesProtokoll.Skip(Math.Max(0, stand.TagesProtokoll.Count - 30)).ToList();
        return new PyObjekt
        {
            { "diensttage", stand.TageFertig }, { "tickets", tickets }, { "richtig", richtig },
            { "quote", tickets > 0 ? PythonRunden.Runde(100.0 * richtig / tickets) : 0L },
            { "zwischenfaelle", zwischenRichtig }, { "zwischenfaelle_gesamt", zwischenfaelle }, { "verschoben", verschoben },
            { "kundenprojekte", (long)stand.FertigeProjekte().Count }, { "projekte_laufend", (long)stand.LaufendeProjekte().Count },
            { "kundentickets", (long)kunden }, { "angebote_gewonnen", (long)angebote.Count(a => Py.Wahr(a["gewonnen"])) },
            { "angebote", (long)angebote.Count }, { "mitarbeiter", (long)stand.JeEingestellt.Count }, { "verdient", verdient },
            { "je_fachbereich", jeBereich },
            { "tage", tage.Select(x => (object?)("T" + x)).ToList() },
            { "tage_richtig", tage.Select(x => (object?)jeTag[x][0]).ToList() },
            { "tage_falsch", tage.Select(x => (object?)jeTag[x][1]).ToList() },
            { "ansehen_tage", ansehen.Select(x => (object?)Prozent("T%d", x[0])).ToList() },
            { "ansehen", ansehen.Select(x => x[1]).ToList() },
        };
    }
}
