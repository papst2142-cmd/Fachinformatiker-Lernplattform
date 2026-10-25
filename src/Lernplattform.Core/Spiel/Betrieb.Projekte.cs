using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: die eigene Firma als Modulfunktionen von <c>fisi_game.py</c> (Python 0.63.2), Teil
/// Projekte (Z. 8467-8589 Grossauftraege, Z. 9629-10030 Kundenprojekte): Ausschreibungen,
/// Projektangebote, Teams, Tagesleistung und Ergebnisse am Feierabend.
/// </summary>
public static partial class Betrieb
{
    // -- Grossauftraege (ab 0.42) -------------------------------------------

    /// <summary>gross_template</summary>
    public static PyObjekt? GrossVorlage(object? kennung, PyObjekt inhalt) =>
        ListeOderLeer(Wirtschaft.GrossRegeln(inhalt), "liste").Select(Objekt).FirstOrDefault(e => Py.Gleich(e["id"], kennung));

    /// <summary>gross_tenders: grosse und oeffentliche Ausschreibungen, die am Tag vorliegen.</summary>
    public static List<PyObjekt> GrossAusschreibungen(Spielstand stand, long tag, PyObjekt inhalt)
    {
        var regeln = Wirtschaft.GrossRegeln(inhalt);
        if (!Py.Wahr(stand.Firma) || !Py.Wahr(regeln["liste"]) || stand.Zertifikate.Count == 0)
        {
            return [];
        }
        var (abstand, gilt) = (Py.Int(regeln["abstand_tage"]), Py.Int(regeln["gilt_tage"]));
        var erster = Py.Int(Objekt(stand.Firma)["tag"]) + Py.Int(Get(regeln, "versatz", 0L));
        var start = stand.FirmenStartwert;
        var benutzt = new Dictionary<string, long>();
        var ergebnis = new List<PyObjekt>();
        var platz = 0L;
        object? zuletzt = null;
        var liste = Liste(regeln["liste"]).Select(Objekt).ToList();
        while (erster + platz * abstand <= tag)
        {
            var beginn = erster + platz * abstand;
            var fertig = stand.ZertifikateFertig(beginn);
            var belegt = new PySet(stand.Projekte.Values.Select(Objekt).Where(p =>
                Py.Int(Py.Wahr(Get(p, "tag", 0L)) ? p["tag"] : 0L) < beginn
                && (!Py.Wahr(p["fertig"]) || !Py.Kleiner(p["fertig"], beginn))).Select(p => p["vorlage"]));
            var auswahl = liste.Where(e => fertig.Count > 0 && !Py.Gleich(e["id"], zuletzt)
                && !belegt.Contains(Wirtschaft.GrossPrefix + Py.Str(e["id"]))
                && ListeOderLeer(e, "braucht").All(fertig.Contains)).ToList();
            zuletzt = null;
            if (auswahl.Count > 0)
            {
                long Oft(PyObjekt e) => benutzt.GetValueOrDefault(Py.Str(e["id"]));
                var wenigste = auswahl.Min(Oft);
                auswahl = auswahl.Where(e => Oft(e) == wenigste).ToList();
                var eintrag = auswahl[0];
                var bester = Wurf(start, beginn, "gross|" + Py.Str(eintrag["id"]));
                foreach (var e in auswahl.Skip(1))
                {
                    var w = Wurf(start, beginn, "gross|" + Py.Str(e["id"]));
                    if (w < bester)
                    {
                        (eintrag, bester) = (e, w);
                    }
                }
                benutzt[Py.Str(eintrag["id"])] = Oft(eintrag) + 1;
                zuletzt = eintrag["id"];
                if (beginn + gilt - 1 >= tag)
                {
                    ergebnis.Add(GrossAusschreibung(stand, eintrag, platz, beginn, inhalt));
                }
            }
            platz++;
        }
        return ergebnis;
    }

    /// <summary>_gross_tender</summary>
    private static PyObjekt GrossAusschreibung(Spielstand stand, PyObjekt eintrag, long platz, long beginn, PyObjekt inhalt)
    {
        var regeln = Wirtschaft.GrossRegeln(inhalt);
        var stufe = Objekt(regeln["stufe"]);
        var salz = Prozent("gross%d-", platz);
        var start = stand.FirmenStartwert;
        var spanne = Liste(stufe["material"]);
        var (unten, oben) = (Py.F(spanne[0]), Py.F(spanne[1]));
        var material = PythonRunden.Runde((unten + Wurf(start, beginn, salz + "material") * (oben - unten)) / 10.0) * 10;
        var lage = MarktVerschiebung(stand, beginn, inhalt);
        var (gebote, ausgefallen) = MitbewerberGebote("projekt", "normal", eintrag["cat"], start, beginn, salz, inhalt,
            extra: Py.Int(Get(regeln, "mitbieter_extra", 0L)), schub: Py.Int(lage["shift"]));
        var billigst = Billigst(gebote);
        var kennung = Prozent("%s%d", Wirtschaft.GrossPrefix, platz + 1);
        var was = Py.Wahr(eintrag["oeffentlich"]) ? "Öffentliche Ausschreibung" : "Großauftrag";
        var namen = ListeOderLeer(eintrag, "braucht").Select(z => Get(Wirtschaft.Zertifikat(z, inhalt) ?? [], "kurz", z)).ToList();
        return new PyObjekt
        {
            { "id", kennung }, { "slot", platz }, { "vorlage", Wirtschaft.GrossPrefix + Py.Str(eintrag["id"]) }, { "folge", 0L },
            { "titel", eintrag["titel"] }, { "kunde", eintrag["kunde"] }, { "kunde_kurz", eintrag["kunde_kurz"] },
            { "cat", eintrag["cat"] }, { "thema", eintrag["thema"] },
            { "schwierigkeit", Get(stufe, "name", "Großauftrag") },
            { "aufwand", Py.Int(stufe["aufwand"]) }, { "anforderung", Py.Int(stufe["anforderung"]) },
            { "material", Rabatt(material, stand.RaumEffekt("material_rabatt")) },
            { "material_markt", material }, { "frist", Py.Int(stufe["frist"]) }, { "von_tag", beginn },
            { "bis_tag", beginn + Py.Int(regeln["gilt_tage"]) - 1 },
            { "markt", billigst["zuschlag"] }, { "laune", billigst["laune"] },
            { "konkurrent", billigst["id"] }, { "bieter", gebote.Cast<object?>().ToList() }, { "ausgefallen", ausgefallen },
            { "text", Prozent("%s von „%s“: %s", was, eintrag["kunde"], eintrag["auftrag"]) },
            { "ausgangssituation", eintrag["ausgangssituation"] }, { "auftrag", eintrag["auftrag"] },
            { "rahmenbedingungen", ListeOderLeer(eintrag, "rahmenbedingungen").ToList() },
            { "gross", true }, { "oeffentlich", Py.Wahr(eintrag["oeffentlich"]) },
            { "braucht", ListeOderLeer(eintrag, "braucht").ToList() }, { "zertifikate", namen },
            { "gegenwind", lage["gegenwind"] },
            { "ergebnis", stand.ProjektAngebote.Get(kennung) },
        };
    }

    /// <summary>gross_badge_text: "Öffentlicher Großauftrag · nur mit ISO 27001" (leer bei normalen Projekten).</summary>
    public static string GrossMarkeText(PyObjekt projekt, PyObjekt inhalt)
    {
        var vorlage = Py.Str(Get(projekt, "vorlage", ""));
        if (!Py.Wahr(projekt["gross"]) && !vorlage.StartsWith(Wirtschaft.GrossPrefix, StringComparison.Ordinal))
        {
            return "";
        }
        var eintrag = (vorlage.StartsWith(Wirtschaft.GrossPrefix, StringComparison.Ordinal)
            ? GrossVorlage(vorlage[Wirtschaft.GrossPrefix.Length..], inhalt) : GrossVorlage(vorlage, inhalt)) ?? [];
        var braucht = Py.Wahr(eintrag["braucht"]) ? Liste(eintrag["braucht"]) : Py.Wahr(projekt["braucht"]) ? Liste(projekt["braucht"]) : [];
        var namen = braucht.Select(z => Py.Str(Get(Wirtschaft.Zertifikat(z, inhalt) ?? [], "kurz", z)));
        return Prozent("%s · nur mit %s", Py.Wahr(eintrag["oeffentlich"]) ? "Öffentlicher Großauftrag" : "Großauftrag", string.Join(" und ", namen));
    }

    /// <summary>project_details: Kunde, Ausgangssituation, Auftrag, Rahmenbedingungen eines Projekts.</summary>
    public static PyObjekt ProjektEinzelheiten(PyObjekt projekt, PyObjekt inhalt)
    {
        if (projekt["vorlage"] is string v && v.StartsWith(Wirtschaft.GrossPrefix, StringComparison.Ordinal))
        {
            var eintrag = GrossVorlage(v[Wirtschaft.GrossPrefix.Length..], inhalt) ?? projekt;
            return new PyObjekt
            {
                { "kunde", Get(eintrag, "kunde", "") }, { "ausgangssituation", Get(eintrag, "ausgangssituation", "") },
                { "auftrag", Get(eintrag, "auftrag", "") }, { "rahmenbedingungen", ListeOderLeer(eintrag, "rahmenbedingungen").ToList() },
                { "lernbar", false },
            };
        }
        var e = Objekt((Py.Wahr(inhalt["projektarbeiten"]) ? Liste(inhalt["projektarbeiten"]) : [])[(int)Py.Int(projekt["vorlage"])]);
        return new PyObjekt
        {
            { "kunde", e["branche"] }, { "ausgangssituation", e["ausgangssituation"] }, { "auftrag", e["auftrag"] },
            { "rahmenbedingungen", ListeOderLeer(e, "rahmenbedingungen").ToList() }, { "lernbar", true },
        };
    }

    // -- Kundenprojekte (ab 0.35) -------------------------------------------

    /// <summary>project_rules</summary>
    public static PyObjekt ProjektRegeln(PyObjekt inhalt) => OderLeer(FirmaRegeln(inhalt)["projekte"]);

    /// <summary>project_limit: wie viele Projekte gleichzeitig laufen koennen.</summary>
    public static long ProjektGrenze(Spielstand stand, PyObjekt inhalt)
    {
        var tabelle = Py.Wahr(ProjektRegeln(inhalt)["laufend"]) ? Objekt(ProjektRegeln(inhalt)["laufend"]) : new PyObjekt { { "1", 1L } };
        var nummer = Py.Wahr(stand.Firma) ? Py.Int(Objekt(stand.Firma)["stufe"]) : 1;
        var bester = 1L;
        foreach (var (k, v) in tabelle)
        {
            if (long.Parse(k, System.Globalization.CultureInfo.InvariantCulture) <= nummer)
            {
                bester = Math.Max(bester, Py.Int(v));
            }
        }
        // Eigener Serverraum (ab 0.36): ein Projekt mehr
        return bester + Py.Int(stand.RaumEffekt("projekte_plus"));
    }

    /// <summary>customer_short: kurzer Kundenname aus der Branche des Testprojekts.</summary>
    public static string KundeKurz(string branche)
    {
        if (branche.Contains('(', StringComparison.Ordinal) && branche.Contains(')', StringComparison.Ordinal))
        {
            var auf = branche.IndexOf('(', StringComparison.Ordinal);
            var zu = branche.IndexOf(')', StringComparison.Ordinal);
            var innen = auf + 1 <= zu ? branche[(auf + 1)..zu] : "";
            innen = Rechner.PythonZeichen.Strip(innen.Split(',')[0]);
            // str.isupper() eines Zeichens: Grossbuchstabe (Lu)
            if (innen.Length > 0 && char.IsUpper(innen[0]))
            {
                return innen;
            }
        }
        var teil = branche.Split(',')[0];
        var mit = teil.IndexOf(" mit ", StringComparison.Ordinal);
        return Rechner.PythonZeichen.Strip(mit >= 0 ? teil[..mit] : teil);
    }

    /// <summary>project_for_slot: die Ausschreibung Nummer platz seit der Gruendung.</summary>
    public static PyObjekt ProjektFuerPlatz(Spielstand stand, long platz, PyObjekt inhalt)
    {
        var regeln = ProjektRegeln(inhalt);
        var vorlagen = Py.Wahr(inhalt["projektarbeiten"]) ? Liste(inhalt["projektarbeiten"]) : [];
        var start = stand.FirmenStartwert;
        var reihe = Enumerable.Range(0, vorlagen.Count).OrderBy(i => Wurf(start, 0, Prozent("projekt|%d", (long)i))).ToList();
        var index = reihe[(int)PyMod(platz, reihe.Count)];
        var vorlage = Objekt(vorlagen[index]);
        var stufe = Objekt(Objekt(regeln["stufen"])[Py.Str(vorlage["schwierigkeit"])]);
        var beginn = Py.Int(Objekt(stand.Firma)["tag"]) + platz * Py.Int(regeln["abstand_tage"]);
        var salz = Prozent("projekt%d-", platz);
        var spanne = Liste(stufe["material"]);
        var (unten, oben) = (Py.F(spanne[0]), Py.F(spanne[1]));
        var material = PythonRunden.Runde((unten + Wurf(start, beginn, salz + "material") * (oben - unten)) / 10.0) * 10;
        var fach = vorlage["cat"] is string c && T.Kurz.TryGetValue(c, out var k) ? k : vorlage["cat"];
        var lage = MarktVerschiebung(stand, beginn, inhalt);
        var (gebote, ausgefallen) = MitbewerberGebote("projekt", "normal", fach, start, beginn, salz, inhalt, schub: Py.Int(lage["shift"]));
        var billigst = Billigst(gebote);
        var kennung = Prozent("%s%d", ProjektPrefix, platz + 1);
        var kunde = KundeKurz(Py.Str(vorlage["branche"]));
        var text = Py.Format(Py.Str(Waehle(Liste(regeln["texte"]), start, beginn, salz + "text")),
            new PyObjekt { { "kunde", kunde }, { "auftrag", vorlage["auftrag"] } });
        return new PyObjekt
        {
            { "id", kennung }, { "slot", platz }, { "vorlage", (long)index }, { "folge", PyDiv(platz, reihe.Count) },
            { "titel", vorlage["title"] }, { "kunde", vorlage["branche"] },
            { "kunde_kurz", kunde }, { "cat", fach }, { "thema", vorlage["thema"] },
            { "schwierigkeit", vorlage["schwierigkeit"] }, { "aufwand", Py.Int(stufe["aufwand"]) },
            { "anforderung", Py.Int(stufe["anforderung"]) },
            // Mit eigenem Lager ist das Material guenstiger (ab 0.36)
            { "material", Rabatt(material, stand.RaumEffekt("material_rabatt")) },
            { "material_markt", material },
            { "frist", Py.Int(stufe["frist"]) }, { "von_tag", beginn },
            { "bis_tag", beginn + Py.Int(regeln["gilt_tage"]) - 1 },
            { "markt", billigst["zuschlag"] }, { "laune", billigst["laune"] },
            { "konkurrent", billigst["id"] }, { "bieter", gebote.Cast<object?>().ToList() }, { "ausgefallen", ausgefallen },
            { "text", text }, { "ausgangssituation", vorlage["ausgangssituation"] },
            { "auftrag", vorlage["auftrag"] },
            { "rahmenbedingungen", ListeOderLeer(vorlage, "rahmenbedingungen").ToList() },
            { "gegenwind", lage["gegenwind"] },
            { "ergebnis", stand.ProjektAngebote.Get(kennung) },
        };
    }

    /// <summary>project_tenders: Ausschreibungen, die an einem Arbeitstag vorliegen.</summary>
    public static List<PyObjekt> ProjektAusschreibungen(Spielstand stand, long tag, PyObjekt inhalt)
    {
        var regeln = ProjektRegeln(inhalt);
        if (!Py.Wahr(stand.Firma) || !Py.Wahr(inhalt["projektarbeiten"]) || regeln.Count == 0)
        {
            return [];
        }
        var (abstand, gilt) = (Py.Int(regeln["abstand_tage"]), Py.Int(regeln["gilt_tage"]));
        var seit = tag - Py.Int(Objekt(stand.Firma)["tag"]);
        if (seit < 0)
        {
            return [];
        }
        var ergebnis = new List<PyObjekt>();
        for (var platz = Math.Max(0, PyDiv(seit - gilt, abstand)); platz < PyDiv(seit, abstand) + 1; platz++)
        {
            var eintrag = ProjektFuerPlatz(stand, platz, inhalt);
            if (Py.Int(eintrag["von_tag"]) <= tag && tag <= Py.Int(eintrag["bis_tag"]))
            {
                ergebnis.Add(eintrag);
            }
        }
        var offen = (int)Py.Int(Get(regeln, "max_offen", 2L));
        return offen == 0 ? ergebnis : ergebnis.Skip(Math.Max(0, ergebnis.Count - offen)).ToList();
    }

    /// <summary>project_task: das Projekt als Formular-Aufgabe (zwei Positionen).</summary>
    public static PyObjekt ProjektAufgabe(PyObjekt projekt, object? zuschlag, PyObjekt inhalt)
    {
        var regeln = ProjektRegeln(inhalt);
        var angebote = AngebotRegeln(inhalt);
        return new PyObjekt
        {
            { "id", projekt["id"] }, { "typ", "formular" }, { "art", "angebot" },
            { "titel", Prozent("Angebot für „%s“", projekt["titel"]) },
            { "ticket", new List<object?> { projekt["text"] } },
            { "frage", "Wähle deinen Gewinnzuschlag und rechne das Angebot durch." },
            { "daten", new PyObjekt
                {
                    { "positionen", new List<object?>
                        {
                            new PyObjekt { { "text", "Projektarbeit" }, { "menge", projekt["aufwand"] }, { "einheit", "Punkte" }, { "preis", regeln["stundensatz"] } },
                            new PyObjekt { { "text", "Material und Lizenzen" }, { "menge", 1L }, { "preis", projekt["material"] } },
                        }
                    },
                    { "einkauf_name", "Einzelkosten gesamt" },
                    { "handlungskosten", angebote["handlungskosten"] }, { "gewinn", zuschlag }, { "ust", angebote["ust"] },
                }
            },
            { "hilfe", ListeOderLeer(regeln, "hilfe").ToList() },
        };
    }

    /// <summary>project_offer_problem: warum man gerade auf kein Projekt bieten kann (leer = geht).</summary>
    public static string ProjektAngebotProblem(Spielstand stand, PyObjekt inhalt)
    {
        var grenze = ProjektGrenze(stand, inhalt);
        return stand.LaufendeProjekte().Count >= grenze
            ? Prozent("Deine Firma schafft höchstens %s gleichzeitig. Bring zuerst ein laufendes Projekt zu Ende.",
                grenze == 1 ? "1 Projekt" : Prozent("%d Projekte", grenze))
            : "";
    }

    /// <summary>project_offer_result: bewertet ein Projektangebot wie eine Anfrage.</summary>
    public static PyObjekt ProjektAngebotErgebnis(Spielstand stand, PyObjekt projekt, object? zuschlag, PyObjekt? antwort, long tag, PyObjekt inhalt)
    {
        var regeln = ProjektRegeln(inhalt);
        var aufgabe = ProjektAufgabe(projekt, zuschlag, inhalt);
        var marktProjekt = Py.Flach(projekt);
        Py.Setzen(marktProjekt, "material", Get(projekt, "material_markt", projekt["material"]));
        var markt = ProjektAufgabe(marktProjekt, zuschlag, inhalt);
        var daten = AngebotBewerten(stand, aufgabe, projekt, antwort, inhalt, markt);
        var gewonnen = Py.Wahr(daten["gewonnen"]);
        var anzahlung = gewonnen ? PythonRunden.Runde(Py.F(daten["netto"]) * Py.F(regeln["anzahlung"]) / 100.0) : 0L;
        foreach (var p in new PyObjekt
        {
            { "projekt", projekt["id"] }, { "vorlage", projekt["vorlage"] }, { "folge", projekt["folge"] }, { "titel", projekt["titel"] },
            { "kunde", projekt["kunde"] }, { "kunde_kurz", projekt["kunde_kurz"] }, { "cat", projekt["cat"] },
            { "schwierigkeit", projekt["schwierigkeit"] }, { "tag", tag }, { "aufwand", projekt["aufwand"] },
            { "anforderung", projekt["anforderung"] }, { "material", gewonnen ? projekt["material"] : 0L },
            { "anzahlung", anzahlung }, { "frist", projekt["frist"] }, { "frist_tag", tag + Py.Int(projekt["frist"]) - 1 },
            { "geld", gewonnen ? anzahlung - Py.Int(projekt["material"]) : 0L },
        })
        {
            Py.Setzen(daten, p.Key, p.Value);
        }
        return daten;
    }

    /// <summary>project_points: Tagesleistung einer Person im Projekt (eine Nachkommastelle).</summary>
    public static object ProjektPunkte(object? wert, PyObjekt inhalt)
    {
        var regel = ProjektRegeln(inhalt)["leistung"] is PyObjekt r && Py.Wahr(r) ? r : new PyObjekt { { "basis", 2L }, { "je_punkt", 0.1 } };
        return Py.Rund(Py.Plus(regel["basis"], Py.Mal(regel["je_punkt"], Py.F(wert))), 1);
    }

    /// <summary>project_value: Wert einer Person im Thema des Projekts (Spielfigur: Wissensstand).</summary>
    public static long ProjektWert(Spielstand stand, object? person, PyObjekt projekt, PyObjekt? stufen, PyObjekt inhalt)
    {
        var thema = ProjektThema(projekt, inhalt);
        if (Py.Gleich(person, Selbst))
        {
            return ThemaStand(stufen, thema, projekt["cat"]);
        }
        if (Py.Wahr(thema))
        {
            return stand.PersonalThemaWert(person, Py.Str(thema));
        }
        return Py.Int(Get(stand.PersonalWerte(person), Py.Str(projekt["cat"]), 0L));
    }

    /// <summary>person_name</summary>
    public static object? PersonName(Spielstand stand, object? person)
    {
        if (Py.Gleich(person, Selbst))
        {
            return Prozent("Ich selbst (%s)", Py.Wahr(Get(stand.Profil ?? [], "name")) ? stand.Profil!["name"] : "Ich");
        }
        return Get(stand.Personal.Get(person) is PyObjekt d && Py.Wahr(d) ? d : [], "name", person);
    }

    /// <summary>project_candidates: wer im Projektteam mitarbeiten kann (zuerst die Spielfigur).</summary>
    public static List<PyObjekt> ProjektKandidaten(Spielstand stand, PyObjekt projekt, PyObjekt? stufen, PyObjekt inhalt)
    {
        var ergebnis = new List<PyObjekt>();
        var eigeneTickets = stand.UebernommenVon(Selbst).Count;
        var woanders = stand.ProjektVon(Selbst);
        var team = Liste(projekt["team"]);
        var problem = "";
        if (Py.Wahr(woanders) && !Py.Gleich(woanders, projekt["projekt"]))
        {
            problem = Prozent("Du arbeitest schon im Projekt „%s“ mit.", Objekt(stand.Projekte[woanders])["titel"]);
        }
        else if (!Py.Enthaelt(team, Selbst) && eigeneTickets >= TicketGrenze(Selbst, inhalt))
        {
            problem = Prozent("Du hast heute schon %s übernommen.", Plural((long)eigeneTickets, "Kundenticket", "Kundentickets"));
        }
        var wert = ProjektWert(stand, Selbst, projekt, stufen, inhalt);
        ergebnis.Add(new PyObjekt
        {
            { "an", Selbst }, { "name", PersonName(stand, Selbst) }, { "wert", wert }, { "punkte", ProjektPunkte(wert, inhalt) },
            { "im_team", Py.Enthaelt(team, Selbst) }, { "problem", problem },
        });
        foreach (var e in stand.PersonalListe())
        {
            wert = ProjektWert(stand, e["id"], projekt, stufen, inhalt);
            var p = "";
            if (Py.Wahr(e["weiterbildung"]) || Py.Wahr(e["abwesend"]))
            {
                p = AbwesendProblem(e["name"], Objekt(Py.Wahr(e["weiterbildung"]) ? e["weiterbildung"] : e["abwesend"]));
            }
            else if (Py.Wahr(e["projekt"]) && !Py.Gleich(e["projekt"], projekt["projekt"]))
            {
                p = Prozent("%s arbeitet schon im Projekt „%s“ mit.", e["name"], Objekt(stand.Projekte[e["projekt"]])["titel"]);
            }
            else if (Py.Wahr(e["serverfarm"]))
            {
                p = Prozent(FarmBeschaeftigtText, e["name"]);
            }
            else if (!Py.Enthaelt(team, e["id"]) && stand.UebernommenVon(e["id"]).Count > 0)
            {
                p = Prozent("%s hat heute schon ein Kundenticket.", e["name"]);
            }
            ergebnis.Add(new PyObjekt
            {
                { "an", e["id"] }, { "name", e["name"] }, { "wert", wert },
                { "punkte", MitarbeiterProjektPunkte(stand, e["id"], wert, inhalt) },
                { "im_team", Py.Enthaelt(team, e["id"]) }, { "problem", p },
            });
        }
        return ergebnis;
    }

    private static bool ArbeitetMit(Spielstand stand, object? person) =>
        Py.Gleich(person, Selbst) || (stand.Personal.ContainsKey(person) && stand.Abwesenheit(person) is null);

    /// <summary>project_team_points: Punkte, die das Team an einem Arbeitstag schafft (ohne Zufall).</summary>
    public static double ProjektTeamPunkte(Spielstand stand, PyObjekt projekt, PyObjekt? stufen, PyObjekt inhalt)
    {
        var summe = 0.0;
        var team = Liste(projekt["team"]);
        foreach (var person in team)
        {
            if (!ArbeitetMit(stand, person))
            {
                continue;
            }
            summe += Py.F(MitarbeiterProjektPunkte(stand, person, ProjektWert(stand, person, projekt, stufen, inhalt), inhalt));
        }
        return PythonRundenStellen.Runde(summe * TeamFaktor(stand, team), 1);
    }

    /// <summary>project_phases: [(name, fertig, aktuell)] der Phasen je Fachbereich.</summary>
    public static List<(string Name, bool Fertig, bool Aktuell)> ProjektPhasen(PyObjekt projekt, PyObjekt inhalt)
    {
        var tabelle = OderLeer(ProjektRegeln(inhalt)["phasen"]);
        var namen = (Py.Wahr(tabelle[Py.Str(projekt["cat"])]) ? Liste(tabelle[Py.Str(projekt["cat"])]) : ["Planung", "Umsetzung", "Abschluss"])
            .Select(Py.Str).ToList();
        var anteil = Py.Wahr(projekt["aufwand"]) ? Math.Min(1.0, Py.F(projekt["stand"]) / Py.F(projekt["aufwand"])) : 1.0;
        var fertig = Py.Wahr(projekt["fertig"]) || anteil >= 1 ? namen.Count : (int)(anteil * namen.Count);
        return [.. namen.Select((n, i) => (n, i < fertig, i == fertig))];
    }

    /// <summary>project_phase_text: "Erledigt: ... · Jetzt: ... · Danach: ..."</summary>
    public static string ProjektPhasenText(PyObjekt projekt, PyObjekt inhalt)
    {
        var phasen = ProjektPhasen(projekt, inhalt);
        var teile = new List<string>();
        var fertig = phasen.Where(p => p.Fertig).Select(p => p.Name).ToList();
        var jetzt = phasen.Where(p => p.Aktuell).Select(p => p.Name).ToList();
        var spaeter = phasen.Where(p => !p.Fertig && !p.Aktuell).Select(p => p.Name).ToList();
        if (fertig.Count > 0)
        {
            teile.Add("Erledigt: " + string.Join(", ", fertig));
        }
        if (jetzt.Count > 0)
        {
            teile.Add("Jetzt: " + jetzt[0]);
        }
        if (spaeter.Count > 0)
        {
            teile.Add("Danach: " + string.Join(", ", spaeter));
        }
        return string.Join(" · ", teile);
    }

    /// <summary>project_phase_name</summary>
    public static string ProjektPhasenName(PyObjekt projekt, PyObjekt inhalt)
    {
        var phasen = ProjektPhasen(projekt, inhalt);
        var jetzt = phasen.Where(p => p.Aktuell).ToList();
        return jetzt.Count > 0 ? jetzt[0].Name : phasen[^1].Name;
    }

    /// <summary>project_status_text: "Tag 2 von 5 · 21 von 35 Punkten · ..."</summary>
    public static string ProjektStandText(Spielstand stand, PyObjekt projekt, PyObjekt? stufen, PyObjekt inhalt)
    {
        var tag = stand.Tag - Py.Int(projekt["tag"]) + 1;
        var punkteText = Num(PythonRundenStellen.Runde(Py.F(projekt["stand"]), 1));
        var text = Prozent("Tag %d von %d · %s von %d Punkten", tag, projekt["frist"], punkteText, projekt["aufwand"]);
        if (tag > Py.Int(projekt["frist"]))
        {
            text = Prozent("Frist überschritten (%d von %d Tagen) · %s von %d Punkten", tag, projekt["frist"], punkteText, projekt["aufwand"]);
        }
        if (Liste(projekt["team"]).Count == 0)
        {
            return text + " · noch kein Team";
        }
        if (stufen is not null)
        {
            var punkte = ProjektTeamPunkte(stand, projekt, stufen, inhalt);
            if (punkte > 0)
            {
                var rest = Math.Max(0.0, Py.F(projekt["aufwand"]) - Py.F(projekt["stand"]));
                var tage = (long)Math.Ceiling(rest / punkte);
                text += Prozent(" · Team schafft etwa %s Punkte am Tag, %s", Num(punkte),
                    tage <= 1 ? "heute fertig" : Prozent("noch etwa %d Arbeitstage", tage));
            }
        }
        return text;
    }

    /// <summary>project_team_text</summary>
    public static string ProjektTeamText(Spielstand stand, PyObjekt projekt)
    {
        var team = Liste(projekt["team"]);
        if (team.Count == 0)
        {
            return "Noch kein Team";
        }
        return "Team: " + string.Join(", ", team.Select(p => Py.Gleich(p, Selbst) ? "du" : KurzName(PersonName(stand, p))));
    }

    /// <summary>project_outcomes: Ergebnisse der Projekt-Arbeitstage fuer den Feierabend.</summary>
    public static List<PyObjekt> ProjektAusgaenge(Spielstand stand, PyObjekt? stufen, PySet? gelernt, PyObjekt inhalt)
    {
        var regeln = ProjektRegeln(inhalt);
        var ergebnis = new List<PyObjekt>();
        foreach (var projekt in stand.LaufendeProjekte())
        {
            var team = Liste(projekt["team"]);
            if (stand.ProjektTage.Contains(new PyTupel(projekt["projekt"], stand.Tag)) || team.Count == 0)
            {
                continue;
            }
            var anteile = new List<PyObjekt>();
            var bester = 0L;
            foreach (var person in team)
            {
                if (!ArbeitetMit(stand, person))
                {
                    continue;
                }
                var wert = ProjektWert(stand, person, projekt, stufen, inhalt);
                bester = Math.Max(bester, wert);
                anteile.Add(new PyObjekt
                {
                    { "an", person }, { "name", PersonName(stand, person) }, { "punkte", MitarbeiterProjektPunkte(stand, person, wert, inhalt) },
                });
            }
            if (anteile.Count == 0)
            {
                continue;
            }
            // Ab 0.43: wer das ganze Team mitzieht (Macke), wirkt einmal
            var faktor = TeamFaktor(stand, team);
            var rueckschlag = "";
            var bonus = gelernt is not null && gelernt.Contains(projekt["vorlage"]);
            if (bonus)
            {
                faktor *= 1 + Py.F(Get(regeln, "lernbonus", 0L)) / 100.0;
            }
            var regel = OderLeer(regeln["rueckschlag"]);
            // Eigener Serverraum (ab 0.36): seltener Rueckschlaege
            var chance = Py.F(Get(regel, "chance", 0L)) * Py.F(stand.RaumEffekt("rueckschlag_faktor", 1.0));
            if (bester < Py.Int(projekt["anforderung"]) && Wurf(stand.FirmenStartwert, stand.Tag, "rueck|" + Py.Str(projekt["projekt"])) < chance)
            {
                faktor *= Py.F(Get(regel, "faktor", 0.5));
                var tabelle = OderLeer(regeln["rueckschlaege"]);
                var texte = Py.Wahr(tabelle[Py.Str(projekt["cat"])]) ? Liste(tabelle[Py.Str(projekt["cat"])])
                    : ["Es gab Probleme, heute ging es nur langsam voran."];
                rueckschlag = Py.Str(Waehle(texte, stand.FirmenStartwert, stand.Tag, "rueck-text|" + Py.Str(projekt["projekt"])));
            }
            var punkte = PythonRundenStellen.Runde(Py.F(Py.Summe(anteile.Select(a => a["punkte"]))) * faktor, 1);
            var neu = Py.F(projekt["stand"]) + punkte;
            var aufwand = Py.F(projekt["aufwand"]);
            var jetzt = neu < aufwand ? neu : aufwand;
            var eintrag = new PyObjekt
            {
                { "projekt", projekt["projekt"] }, { "titel", projekt["titel"] }, { "kunde", Get(projekt, "kunde_kurz", "") },
                { "tag", stand.Tag }, { "beitraege", anteile.Cast<object?>().ToList() }, { "rueckschlag", rueckschlag },
                { "lernbonus", bonus }, { "punkte", punkte }, { "stand", PythonRundenStellen.Runde(jetzt, 1) },
                { "aufwand", projekt["aufwand"] }, { "fertig", jetzt >= aufwand - 0.001 }, { "geld", 0L }, { "verzug", 0L },
                { "abzug", 0L }, { "reputation", new PyObjekt() },
            };
            if (Py.Wahr(eintrag["fertig"]))
            {
                var spaet = Math.Max(0, stand.Tag - Py.Int(projekt["frist_tag"]));
                var verzug = OderLeer(regeln["verzug"]);
                var abzug = Math.Min(Py.Int(Get(verzug, "max_prozent", 30L)), spaet * Py.Int(Get(verzug, "prozent_je_tag", 5L)));
                var rest = Py.F(projekt["netto"]) - Py.F(projekt["anzahlung"]) - Py.F(projekt["netto"]) * abzug / 100.0;
                Py.Setzen(eintrag, "verzug", spaet);
                Py.Setzen(eintrag, "abzug", abzug);
                Py.Setzen(eintrag, "geld", PythonRunden.Runde(rest));
                if (spaet != 0)
                {
                    Py.Setzen(eintrag, "reputation", new PyObjekt
                    {
                        { "kundenzufriedenheit", Math.Max(-20, spaet * Py.Int(Get(verzug, "kundenzufriedenheit_je_tag", -2L))) },
                    });
                }
                else
                {
                    var rep = new PyObjekt();
                    foreach (var p in OderLeer(regeln["puenktlich"]).Where(p => Py.Wahr(p.Value)))
                    {
                        rep.Add(p.Key, p.Value);
                    }
                    Py.Setzen(eintrag, "reputation", rep);
                }
            }
            ergebnis.Add(eintrag);
        }
        return ergebnis;
    }

    /// <summary>project_day_text: Zeile fuer den Feierabend zu einem Projekt-Arbeitstag.</summary>
    public static string ProjektTagText(PyObjekt eintrag)
    {
        var text = Prozent("„%s“: +%s Punkte, jetzt %s von %d", eintrag["titel"], Num(eintrag["punkte"]), Num(eintrag["stand"]), eintrag["aufwand"]);
        if (Py.Wahr(eintrag["lernbonus"]))
        {
            text += " (mit Lernbonus)";
        }
        if (Py.Wahr(eintrag["rueckschlag"]))
        {
            text += Prozent(". Rückschlag: %s", eintrag["rueckschlag"]);
        }
        if (Py.Wahr(eintrag["fertig"]))
        {
            text += Py.Wahr(eintrag["verzug"])
                ? Prozent(". Fertig, aber %d %s zu spät: Restzahlung +%s (%d %% Abzug)", eintrag["verzug"],
                    Py.Int(eintrag["verzug"]) == 1 ? "Tag" : "Tage", Wohnung.GanzeEuro(eintrag["geld"]), eintrag["abzug"])
                : Prozent(". Pünktlich fertig! Restzahlung +%s", Wohnung.GanzeEuro(eintrag["geld"]));
        }
        return text;
    }

    /// <summary>tender_line: Zeile unter einer Ausschreibung.</summary>
    public static string AusschreibungZeile(PyObjekt projekt, PyObjekt inhalt) =>
        Prozent("%s · %s · %d Punkte Aufwand · Frist %d Arbeitstage", FachText(projekt["cat"], ProjektThema(projekt, inhalt)),
            projekt["schwierigkeit"], projekt["aufwand"], projekt["frist"]);

    /// <summary>tender_status_text: noch offen (bis wann) oder Ergebnis.</summary>
    public static string AusschreibungStandText(Spielstand stand, PyObjekt projekt, PyObjekt inhalt)
    {
        if (projekt["ergebnis"] is not PyObjekt ergebnis || !Py.Wahr(ergebnis))
        {
            var rest = Py.Int(projekt["bis_tag"]) - stand.Tag;
            return Prozent("Angebot möglich %s", rest <= 0 ? "nur noch heute" : Prozent("noch %d Arbeitstage", rest + 1));
        }
        if (Py.Wahr(ergebnis["gewonnen"]))
        {
            return "Gewonnen · läuft unter „Laufende Projekte“";
        }
        if (Py.Gleich(ergebnis["grund"], "rechenfehler"))
        {
            return "Verloren · Fehler im Angebot";
        }
        return Prozent("Verloren · %s war günstiger", Mitbewerber(Py.Wahr(ergebnis["konkurrent"]) ? ergebnis["konkurrent"] : Bitweiche, inhalt)["kurz"]);
    }

    /// <summary>project_office_text: Satz eines Mitarbeiters im Buero, der im Projekt mitarbeitet.</summary>
    public static string ProjektBueroText(Spielstand stand, PyObjekt projekt, PyObjekt inhalt)
    {
        var texte = Py.Wahr(ProjektRegeln(inhalt)["buero"]) ? Liste(ProjektRegeln(inhalt)["buero"]) : ["Ich arbeite am Projekt „{titel}“."];
        return Py.Format(Py.Str(Waehle(texte, stand.FirmenStartwert, stand.Tag, "buero|" + Py.Str(projekt["projekt"]))), new PyObjekt
        {
            { "phase", ProjektPhasenName(projekt, inhalt) }, { "titel", projekt["titel"] }, { "kunde", Get(projekt, "kunde_kurz", "") },
        });
    }

    /// <summary>projects_summary: laufende Projekte und offene Ausschreibungen.</summary>
    public static string ProjekteZeile(Spielstand stand)
    {
        var teile = new List<string>();
        var laufend = stand.LaufendeProjekte();
        if (laufend.Count > 0)
        {
            var ohne = laufend.Count(p => Liste(p["team"]).Count == 0);
            teile.Add(Prozent("%s läuft", laufend.Count == 1 ? "1 Projekt" : Prozent("%d Projekte", (long)laufend.Count)));
            if (ohne > 0)
            {
                teile[^1] += Prozent(" (%s ohne Team)", ohne == 1 ? "eins" : Prozent("%d", (long)ohne));
            }
        }
        var offen = stand.Ausschreibungen().Count(a => !Py.Wahr(a["ergebnis"]));
        if (offen > 0)
        {
            teile.Add(offen == 1 ? "1 Ausschreibung" : Prozent("%d Ausschreibungen", (long)offen));
        }
        return teile.Count > 0 ? Prozent("Projekte: %s", string.Join(" · ", teile)) : "";
    }
}
