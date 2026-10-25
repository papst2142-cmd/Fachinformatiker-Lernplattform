using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W3: die eigene Firma im Spielstand, Nachbau von <c>fisi_game.GameState</c> (Python 0.63.2,
/// Z. 3678-4675): Firmen-Ereignisse, Projekttage, Serverfarm, Kredite, Mahnstufe, Werbung,
/// Umsatzsteuer, Lernen der Mitarbeiter, Personal (Macken, Stimmung, Abwesenheit), Standorte.
/// Stoppgrenze wie <c>Spielstand.cs</c>.
/// </summary>
public sealed partial class Spielstand
{
    // Zwischenspeicher wie in Python (gehoeren nicht zu den verglichenen Feldern)
    private readonly Dictionary<object, PyObjekt> themenBasis = new(PyVergleich.Gleich!);   // _topic_base
    internal readonly PyDict Druck = new();                                  // _pressure

    private static SpielThemen T => SpielThemen.Jetzt;

    private PyObjekt FirmaD => Objekt(Firma);

    /// <summary>_apply_firm: Ereignisse der eigenen Firma (Doppel und ueberholte zaehlen nicht).</summary>
    private void FirmaAnwenden(string art, PyObjekt daten, long tag)
    {
        if (art == Ereignisse.Gegruendet)
        {
            if (Firma is null)
            {
                Firma = new PyObjekt { { "name", Get(daten, "name", "") }, { "tag", Py.Int(Get(daten, "tag", tag)) }, { "stufe", 1L } };
                Geld += Py.Int(Get(daten, "geld", 0L));
                Buchen(tag, Ereignisse.BuchGruendung, Get(daten, "geld", 0L));
            }
            return;
        }
        if (Firma is null)
        {
            return;
        }
        if (art == Ereignisse.Eingestellt && Py.Wahr(daten["id"]) && !Personal.ContainsKey(daten["id"]))
        {
            var id = daten["id"];
            Personal[id] = Py.Flach(daten);
            JeEingestellt.Add(id);
            PersonalHoechst = Math.Max(PersonalHoechst, Personal.Count);
            Stimmung[id] = Py.F(Objekt(Betrieb.PersonalRegeln(Inhalt)["stimmung"])["start"]);
            if (Py.Gleich(daten["standort"], OrtFiliale))
            {
                PersonalOrt[id] = OrtFiliale;
            }
        }
        else if (art == Ereignisse.Gekuendigt)
        {
            MitarbeiterGeht(daten["id"]);
        }
        else if (art == Ereignisse.Filiale)
        {
            var nummer = Py.Int(Get(daten, "stufe", 0L));
            var jetzt = Zweigstelle is PyObjekt z ? Py.Int(z["stufe"]) : 0;
            if (nummer == jetzt + 1 && Betrieb.FilialStufe(nummer, Inhalt) is not null)
            {
                if (Zweigstelle is null)
                {
                    Zweigstelle = new PyObjekt
                    {
                        { "name", Py.Wahr(daten["name"]) ? daten["name"] : Get(Betrieb.FilialRegeln(Inhalt), "name_vorschlag", "Filiale") },
                        { "tag", Py.Int(Get(daten, "tag", tag)) }, { "stufe", 1L },
                    };
                }
                else
                {
                    Py.Setzen(Objekt(Zweigstelle), "stufe", nummer);
                }
                Geld += Py.Int(Get(daten, "geld", 0L));
                Buchen(tag, Ereignisse.BuchAusbau, Get(daten, "geld", 0L));
            }
        }
        else if (art == Ereignisse.Versetzt && Personal.ContainsKey(daten["id"]))
        {
            if (Py.Gleich(daten["standort"], OrtFiliale))
            {
                PersonalOrt[daten["id"]] = OrtFiliale;
            }
            else
            {
                PersonalOrt.Pop(daten["id"]);
            }
        }
        else if (art is Ereignisse.UrlaubJa or Ereignisse.UrlaubNein or Ereignisse.Konflikt or Ereignisse.Rueckhol)
        {
            EntscheidungAnwenden(art, daten, tag);
        }
        else if (art == Ereignisse.Weiterbildung && Personal.ContainsKey(daten["id"]))
        {
            Weiterbildungen.Add(Py.Flach(daten));
            Geld += Py.Int(Get(daten, "geld", 0L));
            Buchen(tag, Ereignisse.BuchWeiterbildung, Get(daten, "geld", 0L));
        }
        else if (art == Ereignisse.Erweitert && Py.Int(Get(daten, "stufe", 0L)) == Py.Int(FirmaD["stufe"]) + 1)
        {
            Py.Setzen(FirmaD, "stufe", Py.Int(FirmaD["stufe"]) + 1);
            AusbauTage[FirmaD["stufe"]] = tag;
            Geld += Py.Int(Get(daten, "geld", 0L));
            Buchen(tag, Ereignisse.BuchAusbau, Get(daten, "geld", 0L));
        }
        else if (art == Ereignisse.Raum && !Raeume.ContainsKey(daten["raum"]))
        {
            var regel = Betrieb.SonderRaum(daten["raum"], Inhalt);
            if (Py.Wahr(regel) && Py.Int(FirmaD["stufe"]) >= Py.Int(regel!["ab_stufe"]))
            {
                Raeume[daten["raum"]] = Py.Flach(daten);
                Geld += Py.Int(Get(daten, "geld", 0L));
                Buchen(tag, Ereignisse.BuchAusbau, Get(daten, "geld", 0L));
            }
        }
        else if (art is Ereignisse.AngebotGewonnen or Ereignisse.AngebotVerloren && Py.Wahr(daten["anfrage"])
            && !Angebote.ContainsKey(daten["anfrage"]))
        {
            var neu = Py.Flach(daten);
            Py.Setzen(neu, "gewonnen", art == Ereignisse.AngebotGewonnen);
            Angebote[daten["anfrage"]] = neu;
            Geld += Py.Int(Get(daten, "geld", 0L));
            Buchen(tag, Ereignisse.BuchAngebote, Get(daten, "geld", 0L));
            ReputationAendern(Py.Wahr(daten["reputation"]) ? Objekt(daten["reputation"]) : new PyObjekt());
        }
        else if (art == Ereignisse.Delegiert && Py.Wahr(daten["ticket"]) && !Delegationen.ContainsKey(daten["ticket"]))
        {
            // Auf zwei Geraeten gleichzeitig verteilt: Wer schon voll ist, bekommt nichts mehr dazu
            var vergeben = Delegationen.Values.Select(Objekt).Count(e => Py.Gleich(e["tag"], daten["tag"]) && Py.Gleich(e["an"], daten["an"]));
            if (vergeben < Betrieb.TicketGrenze(daten["an"], Inhalt))
            {
                Delegationen[daten["ticket"]] = Py.Flach(daten);
            }
        }
        else if (art is Ereignisse.ProjektGewonnen or Ereignisse.ProjektVerloren && Py.Wahr(daten["projekt"])
            && !ProjektAngebote.ContainsKey(daten["projekt"]))
        {
            var gewonnen = art == Ereignisse.ProjektGewonnen;
            var angebot = Py.Flach(daten);
            Py.Setzen(angebot, "gewonnen", gewonnen);
            ProjektAngebote[daten["projekt"]] = angebot;
            if (gewonnen)
            {
                var projekt = Py.Flach(daten);
                Py.Setzen(projekt, "stand", 0.0);
                Py.Setzen(projekt, "team", new List<object?>());
                Py.Setzen(projekt, "fertig", null);
                Py.Setzen(projekt, "tage", new List<object?>());
                Projekte[daten["projekt"]] = projekt;
                Geld += Py.Int(Get(daten, "anzahlung", 0L)) - Py.Int(Get(daten, "material", 0L));
                Buchen(tag, Ereignisse.BuchProjekte, Get(daten, "anzahlung", 0L));
                Buchen(tag, Ereignisse.BuchMaterial, -Py.Int(Get(daten, "material", 0L)));
            }
        }
        else if (art == Ereignisse.Kredit && Py.Wahr(daten["kredit"]) && !Kredite.ContainsKey(daten["kredit"]))
        {
            // Doppelt (zwei Geraete gleichzeitig) zaehlt nur einmal
            var summe = Py.Int(Get(daten, "summe", 0L));
            Kredite[daten["kredit"]] = new PyObjekt
            {
                { "id", daten["kredit"] }, { "name", Py.Wahr(daten["name"]) ? daten["name"] : "Kredit" },
                { "paket", Get(daten, "paket", "") }, { "summe", summe },
                { "laufzeit", Py.Int(Get(daten, "laufzeit", 0L)) }, { "zins", Py.F(Get(daten, "zins", 0L)) },
                { "rate", Py.Int(Get(daten, "rate", 0L)) }, { "tag", Py.Int(Get(daten, "tag", tag)) },
                { "rest", summe }, { "zins_offen", 0L }, { "gebuehren", 0L }, { "raten", 0L },
                { "ausfaelle", 0L }, { "bezahlt_zins", 0L }, { "bezahlt_tilgung", 0L },
                { "bezahlt_gebuehren", 0L }, { "ende", null }, { "ende_art", "" },
            };
            Geld += summe;
            Buchen(tag, Ereignisse.BuchKredit, summe);
            KreditProtokoll.Add(new PyTupel(tag, "aufgenommen", daten["kredit"], Py.Flach(daten)));
        }
        else if (art == Ereignisse.KreditAbgeloest && Kredite.ContainsKey(daten["kredit"]))
        {
            var kredit = Objekt(Kredite[daten["kredit"]]);
            if (kredit["ende"] is not null)
            {
                return;
            }
            var abloese = Wirtschaft.KreditAbloese(kredit, Inhalt);
            Geld -= Py.Int(abloese["gesamt"]);
            Buchen(tag, Ereignisse.BuchTilgung, -Py.Int(abloese["tilgung"]));
            Buchen(tag, Ereignisse.BuchZinsen, -Py.Int(abloese["zinsen"]));
            Buchen(tag, Ereignisse.BuchKreditGebuehren, -Py.Int(abloese["gebuehren"]));
            Py.Setzen(kredit, "bezahlt_tilgung", Py.Plus(kredit["bezahlt_tilgung"], abloese["tilgung"]));
            Py.Setzen(kredit, "bezahlt_zins", Py.Plus(kredit["bezahlt_zins"], abloese["zinsen"]));
            Py.Setzen(kredit, "bezahlt_gebuehren", Py.Plus(kredit["bezahlt_gebuehren"], abloese["gebuehren"]));
            Py.Setzen(kredit, "rest", 0L);
            Py.Setzen(kredit, "zins_offen", 0L);
            Py.Setzen(kredit, "gebuehren", 0L);
            Py.Setzen(kredit, "ende", tag);
            Py.Setzen(kredit, "ende_art", "abgeloest");
            KreditProtokoll.Add(new PyTupel(tag, "abgeloest", kredit["id"], abloese));
        }
        else if (art == Ereignisse.Werbung && Py.Wahr(daten["werbung"]))
        {
            // Doppelt (zwei Geraete) oder schon aktiv: zaehlt nicht
            if (Py.Wahr(Wirtschaft.WerbeForm(daten["werbung"], Inhalt)) && WerbeBuchung(daten["werbung"], Py.Int(Get(daten, "tag", tag))) is null)
            {
                var neu = Py.Flach(daten);
                Py.Setzen(neu, "ende", null);
                Werbungen.Add(neu);
                Geld += Py.Int(Get(daten, "geld", 0L));
                Buchen(tag, Ereignisse.BuchWerbung, Get(daten, "geld", 0L));
            }
        }
        else if (art == Ereignisse.WerbungEnde)
        {
            var buchung = WerbeBuchung(daten["werbung"], Py.Int(Get(daten, "tag", tag)));
            if (Py.Wahr(buchung) && Py.Gleich(buchung!["art"], Wirtschaft.WerbungLaufend))
            {
                Py.Setzen(buchung, "ende", Py.Int(Get(daten, "tag", tag)));
            }
        }
        else if (art == Ereignisse.Zertifizierung && Py.Wahr(daten["zert"]) && !Zertifikate.ContainsKey(daten["zert"]))
        {
            if (Py.Wahr(Wirtschaft.Zertifikat(daten["zert"], Inhalt)))
            {
                Zertifikate[daten["zert"]] = Py.Flach(daten);
                Geld += Py.Int(Get(daten, "geld", 0L));
                Buchen(tag, Ereignisse.BuchZertifikat, Get(daten, "geld", 0L));
            }
        }
        else if (art == Ereignisse.ProjektTeam && Projekte.ContainsKey(daten["projekt"]))
        {
            var projekt = Objekt(Projekte[daten["projekt"]]);
            if (Py.Wahr(projekt["fertig"]))
            {
                return;
            }
            var team = ListeOderLeer(daten, "team").Where(Py.Wahr).ToList();
            // Jede Person arbeitet nur in einem Projekt mit (ab 0.52 auch nicht gleichzeitig an der Serverfarm)
            foreach (var anderes in Projekte.Values.Select(Objekt))
            {
                if (!ReferenceEquals(anderes, projekt))
                {
                    Py.Setzen(anderes, "team", Liste(anderes["team"]).Where(p => !Py.Enthaelt(team, p)).ToList());
                }
            }
            if (Farm is PyObjekt farm)
            {
                Py.Setzen(farm, "team", Liste(farm["team"]).Where(p => !Py.Enthaelt(team, p)).ToList());
            }
            Py.Setzen(projekt, "team", team);
        }
    }

    /// <summary>_apply_project_day: Punkte dazu, bei Fertigstellung die Restzahlung.</summary>
    private bool ProjektTagAnwenden(PyObjekt eintrag, long tag)
    {
        var schluessel = new PyTupel(eintrag["projekt"], Py.Int(Py.Wahr(Get(eintrag, "tag", tag)) ? Get(eintrag, "tag", tag) : tag));
        if (Projekte.Get(eintrag["projekt"]) is not PyObjekt projekt || ProjektTage.Contains(schluessel) || Py.Wahr(projekt["fertig"]))
        {
            return false;
        }
        ProjektTage.Add(schluessel);
        var summe = Py.F(projekt["stand"]) + Py.F(Get(eintrag, "punkte", 0L));
        Py.Setzen(projekt, "stand", summe < Py.F(projekt["aufwand"]) ? summe : Py.F(projekt["aufwand"]));
        Liste(projekt["tage"]).Add(Py.Flach(eintrag));
        if (Py.Wahr(eintrag["fertig"]))
        {
            Py.Setzen(projekt, "fertig", schluessel[1]);
            Py.Setzen(projekt, "team", new List<object?>());
            Geld += Py.Int(Get(eintrag, "geld", 0L));
            Buchen(tag, Ereignisse.BuchProjekte, Get(eintrag, "geld", 0L));
            ReputationAendern(Py.Wahr(eintrag["reputation"]) ? Objekt(eintrag["reputation"]) : new PyObjekt());
        }
        return true;
    }

    // -- Grossauftrag Serverfarm (ab 0.52) --------------------------------------

    private PyObjekt? FarmD => Farm as PyObjekt;

    /// <summary>_apply_farm: Ereignisse der Serverfarm (Doppel zaehlen einmal).</summary>
    private void FarmAnwenden(string art, PyObjekt daten, long tag)
    {
        if (art == Ereignisse.FarmStart)
        {
            if (Farm is null && FarmFrei is not null && Py.Wahr(Firma))
            {
                Farm = Betrieb.NeueFarm(Py.Int(Py.Wahr(Get(daten, "tag", tag)) ? Get(daten, "tag", tag) : tag));
            }
            return;
        }
        var farm = FarmD;
        if (farm is null || Py.Wahr(farm["fertig"]))
        {
            return;
        }
        if (art == Ereignisse.FarmAufgabe)
        {
            var aufgabe = daten["aufgabe"];
            var aufgaben = Objekt(farm["aufgaben"]);
            if (!Py.Wahr(aufgabe) || Py.Hat(aufgaben, Py.Str(aufgabe)) || !Py.Wahr(Betrieb.FarmAufgabe(aufgabe, Inhalt)))
            {
                return;
            }
            Py.Setzen(aufgaben, (string)aufgabe!, Py.Flach(daten));
            Geld += Py.Int(Get(daten, "geld", 0L));
            Buchen(tag, Ereignisse.BuchProjekte, Get(daten, "anzahlung", 0L));
            Buchen(tag, Ereignisse.BuchMaterial, -Py.Int(Get(daten, "kosten", 0L)));
            Buchen(tag, Ereignisse.BuchNacharbeit, -Py.Int(Get(daten, "strafe", 0L)));
            ReputationAendern(Py.Wahr(daten["reputation"]) ? Objekt(daten["reputation"]) : new PyObjekt());
        }
        else if (art == Ereignisse.FarmTeam)
        {
            var team = new List<object?>();
            foreach (var person in ListeOderLeer(daten, "team"))
            {
                if (Py.Wahr(person) && Personal.ContainsKey(person) && !Py.Enthaelt(team, person))
                {
                    team.Add(person);
                }
            }
            var grenze = (int)Py.Int(Get(Betrieb.FarmRegeln(Inhalt), "team_max", 5L));
            team = team.Take(Math.Max(0, grenze)).ToList();
            foreach (var projekt in Projekte.Values.Select(Objekt))
            {
                Py.Setzen(projekt, "team", Liste(projekt["team"]).Where(p => !Py.Enthaelt(team, p)).ToList());
            }
            Py.Setzen(farm, "team", team);
        }
    }

    /// <summary>_apply_farm_day: Team-Punkte, Wuerfel, Phasenende, Abnahme.</summary>
    private void FarmTagAnwenden(PyObjekt eintrag, long tag)
    {
        var farm = FarmD;
        var schluessel = Py.Int(Py.Wahr(Get(eintrag, "tag", tag)) ? Get(eintrag, "tag", tag) : tag);
        if (farm is null || Py.Wahr(farm["fertig"]) || ((PySet)farm["tag_set"]!).Contains(schluessel))
        {
            return;
        }
        ((PySet)farm["tag_set"]!).Add(schluessel);
        Liste(farm["tage"]).Add(Py.Flach(eintrag));
        var phase = eintrag["phase"];
        if (Py.Wahr(eintrag["punkte"]))
        {
            var punkte = (PyDict)farm["punkte"]!;
            punkte[phase] = Py.F(punkte.Get(phase, 0.0)) + Py.F(eintrag["punkte"]);
        }
        foreach (var anteil in ListeOderLeer(eintrag, "beitraege").Select(Objekt))
        {
            var beitraege = (PyDict)farm["beitraege"]!;
            beitraege[anteil["an"]] = Py.F(beitraege.Get(anteil["an"], 0.0)) + Py.F(Get(anteil, "punkte", 0L));
        }
        if (Py.Hat(eintrag, "kabelfehler"))
        {
            Py.Setzen(farm, "kabelfehler", ListeOderLeer(eintrag, "kabelfehler").ToList());
            Py.Setzen(farm, "nacharbeit_bis", eintrag["nacharbeit_bis"]);
        }
        if (Py.Wahr(eintrag["nacharbeit_geld"]))
        {
            Geld -= Py.Int(eintrag["nacharbeit_geld"]);
            Buchen(tag, Ereignisse.BuchNacharbeit, -Py.Int(eintrag["nacharbeit_geld"]));
        }
        if (Py.Hat(eintrag, "verzug"))
        {
            Py.Setzen(farm, "verzug", Py.Int(eintrag["verzug"]));
            Py.Setzen(farm, "liefertag", Py.Int(Py.Wahr(eintrag["liefertag"]) ? eintrag["liefertag"] : tag + 1));
        }
        if (Py.Wahr(eintrag["fertig"]))
        {
            ((PyDict)farm["fertig_phasen"]!)[phase] = schluessel;
            var regel = Betrieb.FarmPhase(phase, Inhalt) ?? new PyObjekt();
            var menge = Py.F(Get(Betrieb.FarmRegeln(Inhalt), "lernen_phase", 0L));
            foreach (var person in FarmTeam())
            {
                foreach (var thema in ListeOderLeer(regel, "themen").Take(1))
                {
                    Lernen(person, thema, menge, tag, "projekt");
                }
            }
        }
        if (Py.Wahr(eintrag["abnahme"]))
        {
            var daten = Objekt(eintrag["abnahme"]);
            Py.Setzen(farm, "abnahme", Py.Flach(daten));
            Py.Setzen(farm, "fertig", schluessel);
            Py.Setzen(farm, "team", new List<object?>());
            Geld += Py.Int(Get(daten, "geld", 0L));
            Buchen(tag, Ereignisse.BuchProjekte, Get(daten, "geld", 0L));
            ReputationAendern(Py.Wahr(daten["reputation"]) ? Objekt(daten["reputation"]) : new PyObjekt());
        }
    }

    /// <summary>farm_team: wer gerade im Serverfarm-Team ist (leer, wenn sie nicht laeuft).</summary>
    public List<object?> FarmTeam() => FarmD is { } farm && !Py.Wahr(farm["fertig"])
        ? Liste(farm["team"]).Where(p => Personal.ContainsKey(p)).ToList()
        : [];

    /// <summary>farm_running</summary>
    public bool FarmLaeuft => FarmD is { } farm && !Py.Wahr(farm["fertig"]);

    // -- Kredite (ab 0.41) -------------------------------------------------------

    /// <summary>_loan_day: Zinsen des Tages, Rate abbuchen; zurueck die geplatzten Zahlungen.</summary>
    private List<(string Art, PyObjekt? Posten, long Betrag)> KreditTag(long tag)
    {
        var geplatzt = new List<(string, PyObjekt?, long)>();
        foreach (var kredit in LaufendeKredite())
        {
            if (Py.Int(kredit["tag"]) > tag)
            {
                continue;
            }
            Py.Setzen(kredit, "zins_offen", Py.Int(kredit["zins_offen"]) + Mathe.PythonRunden.Runde(Py.F(kredit["rest"])
                * Wirtschaft.TagesZins(Wirtschaft.KreditZins(kredit, Mahnstufe, Inhalt), Inhalt)));
            var faellig = Math.Min(Py.Int(kredit["rate"]), Py.Int(kredit["rest"]) + Py.Int(kredit["zins_offen"]) + Py.Int(kredit["gebuehren"]));
            if (Geld >= faellig)
            {
                var gebuehren = Math.Min(faellig, Py.Int(kredit["gebuehren"]));
                var zinsen = Math.Min(faellig - gebuehren, Py.Int(kredit["zins_offen"]));
                var tilgung = faellig - gebuehren - zinsen;
                Py.Setzen(kredit, "gebuehren", Py.Int(kredit["gebuehren"]) - gebuehren);
                Py.Setzen(kredit, "zins_offen", Py.Int(kredit["zins_offen"]) - zinsen);
                Py.Setzen(kredit, "rest", Py.Int(kredit["rest"]) - tilgung);
                Py.Setzen(kredit, "raten", Py.Int(kredit["raten"]) + 1);
                Py.Setzen(kredit, "bezahlt_gebuehren", Py.Int(kredit["bezahlt_gebuehren"]) + gebuehren);
                Py.Setzen(kredit, "bezahlt_zins", Py.Int(kredit["bezahlt_zins"]) + zinsen);
                Py.Setzen(kredit, "bezahlt_tilgung", Py.Int(kredit["bezahlt_tilgung"]) + tilgung);
                Geld -= faellig;
                Buchen(tag, Ereignisse.BuchKreditGebuehren, -gebuehren);
                Buchen(tag, Ereignisse.BuchZinsen, -zinsen);
                Buchen(tag, Ereignisse.BuchTilgung, -tilgung);
                KreditProtokoll.Add(new PyTupel(tag, "rate", kredit["id"], new PyObjekt
                {
                    { "rate", faellig }, { "zinsen", zinsen }, { "tilgung", tilgung }, { "gebuehren", gebuehren }, { "rest", kredit["rest"] },
                }));
                if (Py.Int(kredit["rest"]) <= 0 && Py.Int(kredit["zins_offen"]) <= 0 && Py.Int(kredit["gebuehren"]) <= 0)
                {
                    Py.Setzen(kredit, "rest", 0L);
                    Py.Setzen(kredit, "ende", tag);
                    Py.Setzen(kredit, "ende_art", "zurueckgezahlt");
                    KreditProtokoll.Add(new PyTupel(tag, "zurueckgezahlt", kredit["id"], new PyObjekt()));
                }
            }
            else
            {
                geplatzt.Add(("kredit", kredit, faellig));
            }
        }
        return geplatzt;
    }

    /// <summary>_dunning_day: die zentrale Mahnstufe fuer Kredite und Umsatzsteuer.</summary>
    private void MahnTagAnwenden(long tag, List<(string Art, PyObjekt? Posten, long Betrag)> geplatzt)
    {
        var regeln = Wirtschaft.KreditRegeln(Inhalt);
        var mahnung = Objekt(regeln["mahnung"]);
        var stufen = Liste(mahnung["stufen"]);
        if (geplatzt.Count > 0 && !Py.Wahr(Erleichterung("mahnstufen", true)))
        {
            // Ab 0.47 auf "Einfach": keine Mahnstufe, keine Gebuehr, kein Ansehensverlust
            foreach (var (art, posten, faellig) in geplatzt)
            {
                if (art == "kredit")
                {
                    Py.Setzen(posten!, "ausfaelle", Py.Int(posten!["ausfaelle"]) + 1);
                    KreditProtokoll.Add(new PyTupel(tag, "ausfall", posten["id"], new PyObjekt
                    {
                        { "rate", faellig }, { "stufe", 0L }, { "gebuehr", 0L }, { "reputation", new PyObjekt() }, { "einfach", true },
                    }));
                }
            }
            return;
        }
        if (geplatzt.Count > 0)
        {
            // Hoechstens eine Stufe pro Feierabend, auch wenn mehreres platzt
            Mahnstufe = Math.Min(stufen.Count, Mahnstufe + 1);
            MahnTag = tag;
            MahnHoechst = Math.Max(MahnHoechst, Mahnstufe);
            var stufe = Objekt(stufen[(int)Mahnstufe - 1]);
            ReputationAendern(Py.Wahr(stufe["reputation"]) ? Objekt(stufe["reputation"]) : new PyObjekt());
            var gebuehr = Py.Int(Get(stufe, "gebuehr", 0L));
            for (var nummer = 0; nummer < geplatzt.Count; nummer++)
            {
                var (art, posten, faellig) = geplatzt[nummer];
                // Das Ansehen sinkt einmal pro Feierabend (steht beim ersten Posten)
                var reputation = nummer == 0 ? Py.Flach(Py.Wahr(stufe["reputation"]) ? Objekt(stufe["reputation"]) : new PyObjekt()) : new PyObjekt();
                if (art == "kredit")
                {
                    Py.Setzen(posten!, "gebuehren", Py.Int(posten!["gebuehren"]) + gebuehr);
                    Py.Setzen(posten, "ausfaelle", Py.Int(posten["ausfaelle"]) + 1);
                    KreditProtokoll.Add(new PyTupel(tag, "ausfall", posten["id"], new PyObjekt
                    {
                        { "rate", faellig }, { "stufe", Mahnstufe }, { "gebuehr", gebuehr }, { "reputation", reputation },
                    }));
                }
                else
                {
                    SteuerSchuld += gebuehr;
                    SteuerProtokoll.Add(new PyTupel(tag, "ausfall", new PyObjekt
                    {
                        { "betrag", faellig }, { "stufe", Mahnstufe }, { "gebuehr", gebuehr }, { "reputation", reputation }, { "schuld", SteuerSchuld },
                    }));
                }
            }
        }
        else if (Mahnstufe != 0 && tag - MahnTag >= Py.Int(mahnung["abbau_tage"]))
        {
            Mahnstufe -= 1;
            MahnTag = tag;
            KreditProtokoll.Add(new PyTupel(tag, "mahnstufe", "", new PyObjekt { { "stufe", Mahnstufe } }));
            if (Mahnstufe == 0)
            {
                MahnUeberstanden.Add(MahnHoechst);
                MahnHoechst = 0;
            }
        }
    }

    // -- Umsatzsteuer und Werbung (ab 0.42) ---------------------------------------

    /// <summary>_ads_day: laufende Werbung kostet ihren Tagessatz.</summary>
    private void WerbeTag(long tag)
    {
        foreach (var buchung in Werbungen.Select(Objekt))
        {
            if (Py.Gleich(buchung["art"], Wirtschaft.WerbungLaufend) && Wirtschaft.WerbungAktiv(buchung, tag))
            {
                var kosten = Py.Int(Get(buchung, "kosten", 0L));
                Geld -= kosten;
                Buchen(tag, Ereignisse.BuchWerbung, -kosten);
                WerbeProtokoll.Add(new PyTupel(tag, buchung["werbung"], kosten));
            }
        }
    }

    /// <summary>_tax_day: Umsatzsteuer des Tages, Ruecklage, Voranmeldung; zurueck die geplatzte Zahlung.</summary>
    private List<(string Art, PyObjekt? Posten, long Betrag)> SteuerTag(long tag)
    {
        var regeln = Wirtschaft.SteuerRegeln(Inhalt);
        var satz = Py.F(regeln["satz"]);
        var eintrag = Kassenbuch.Get(tag) is PyObjekt e && Py.Wahr(e) ? e : new PyObjekt { { "ein", new PyObjekt() }, { "aus", new PyObjekt() } };
        var ein = Objekt(eintrag["ein"]);
        var aus = Objekt(eintrag["aus"]);
        var bruttoEin = Wirtschaft.SteuerEinnahmen.Sum(k => Py.Int(Get(ein, k, 0L)));
        var bruttoAus = Wirtschaft.SteuerVorsteuer.Sum(k => Py.Int(Get(aus, k, 0L)));
        var zeitraum = SteuerZeitraum;
        if (zeitraum["start"] is null)
        {
            Py.Setzen(zeitraum, "start", tag);
        }
        Py.Setzen(zeitraum, "ust", Py.F(zeitraum["ust"]) + bruttoEin * satz / (100.0 + satz));
        Py.Setzen(zeitraum, "vst", Py.F(zeitraum["vst"]) + bruttoAus * satz / (100.0 + satz));
        Py.Setzen(zeitraum, "umsatz", Py.Int(zeitraum["umsatz"]) + bruttoEin);
        Py.Setzen(zeitraum, "ausgaben", Py.Int(zeitraum["ausgaben"]) + bruttoAus);
        Py.Setzen(zeitraum, "tage", Py.Int(zeitraum["tage"]) + 1);
        SteuerTage += 1;
        var faellig = Wirtschaft.SteuerZahllast(zeitraum);
        // Ruecklage: so viel, wie bisher an Zahllast aufgelaufen ist
        var ziel = Math.Max(0, Mathe.PythonRunden.Runde(faellig * Py.F(Get(regeln, "ruecklage_prozent", 100L)) / 100.0));
        var verschieben = ziel - SteuerRuecklage;
        if (verschieben > 0)
        {
            verschieben = Math.Min(verschieben, Math.Max(0, Geld));
        }
        if (verschieben != 0)
        {
            Geld -= verschieben;
            SteuerRuecklage += verschieben;
            Buchen(tag, Ereignisse.BuchSteuer, -verschieben);
            SteuerProtokoll.Add(new PyTupel(tag, "ruecklage", new PyObjekt { { "betrag", verschieben }, { "ruecklage", SteuerRuecklage } }));
        }
        if (SteuerTage % Math.Max(1, Py.Int(regeln["faellig_tage"])) == 0)
        {
            var daten = new PyObjekt
            {
                { "von", zeitraum["start"] }, { "bis", tag }, { "ust", Mathe.PythonRunden.Runde(Py.F(zeitraum["ust"])) },
                { "vorsteuer", Mathe.PythonRunden.Runde(Py.F(zeitraum["vst"])) }, { "zahllast", faellig },
                { "umsatz", zeitraum["umsatz"] }, { "ausgaben", zeitraum["ausgaben"] },
            };
            if (faellig >= 0)
            {
                var ausRuecklage = Math.Min(SteuerRuecklage, faellig);
                SteuerRuecklage -= ausRuecklage;
                var rest = faellig - ausRuecklage;
                Py.Setzen(daten, "aus_ruecklage", ausRuecklage);
                Py.Setzen(daten, "vom_konto", 0L);
                if (rest > 0)
                {
                    var bezahlt = Math.Min(rest, Math.Max(0, Geld));
                    Geld -= bezahlt;
                    Buchen(tag, Ereignisse.BuchSteuer, -bezahlt);
                    Py.Setzen(daten, "vom_konto", bezahlt);
                    SteuerSchuld += rest - bezahlt;
                    Py.Setzen(daten, "offen", rest - bezahlt);
                }
            }
            else
            {
                Geld -= faellig;
                Buchen(tag, Ereignisse.BuchSteuer, -faellig);
                Py.Setzen(daten, "erstattung", -faellig);
            }
            if (SteuerRuecklage != 0)
            {
                // Was in der Ruecklage uebrig ist, kommt aufs Konto zurueck
                Py.Setzen(daten, "frei", SteuerRuecklage);
                Geld += SteuerRuecklage;
                Buchen(tag, Ereignisse.BuchSteuer, SteuerRuecklage);
                SteuerRuecklage = 0;
            }
            SteuerProtokoll.Add(new PyTupel(tag, "voranmeldung", daten));
            SteuerZeitraum = NeuerSteuerZeitraum();
        }
        if (SteuerSchuld != 0)
        {
            var bezahlt = Math.Min(SteuerSchuld, Math.Max(0, Geld));
            if (bezahlt != 0)
            {
                Geld -= bezahlt;
                SteuerSchuld -= bezahlt;
                Buchen(tag, Ereignisse.BuchSteuer, -bezahlt);
                SteuerProtokoll.Add(new PyTupel(tag, "nachgezahlt", new PyObjekt { { "betrag", bezahlt }, { "schuld", SteuerSchuld } }));
            }
            if (SteuerSchuld != 0)
            {
                return [("steuer", null, SteuerSchuld)];
            }
        }
        return [];
    }

    /// <summary>ad_booking: die gebuchte Werbung dieser Form, die am Tag laeuft oder erst beginnt.</summary>
    public PyObjekt? WerbeBuchung(object? werbung, long tag) =>
        Werbungen.Select(Objekt).FirstOrDefault(b => Py.Gleich(b["werbung"], werbung) && Wirtschaft.WerbungOffen(b, tag));

    /// <summary>ads_active: Werbung, die am Tag wirkt.</summary>
    public List<PyObjekt> WerbungAktiv(long? tag = null) => Werbungen.Select(Objekt).Where(b => Wirtschaft.WerbungAktiv(b, tag ?? Tag)).ToList();

    /// <summary>ad_costs: laufende Werbekosten pro Arbeitstag.</summary>
    public long WerbeKosten(long? tag = null) =>
        WerbungAktiv(tag).Where(b => Py.Gleich(b["art"], Wirtschaft.WerbungLaufend)).Sum(b => Py.Int(Get(b, "kosten", 0L)));

    /// <summary>certs_held: abgeschlossene Zertifizierungen am Tag.</summary>
    public PySet ZertifikateFertig(long? tag = null)
    {
        var t = tag ?? Tag;
        return new PySet(Zertifikate.Where(p => Py.Int(Get(Objekt(p.Value), "bis_tag", 0L)) <= t).Select(p => p.Key));
    }

    /// <summary>cert_running</summary>
    public List<PyObjekt> ZertifikateLaufend(long? tag = null)
    {
        var t = tag ?? Tag;
        return Zertifikate.Values.Select(Objekt).Where(d => Py.Int(Get(d, "bis_tag", 0L)) > t).ToList();
    }

    /// <summary>running_loans: laufende Kredite in der Reihenfolge der Aufnahme.</summary>
    public List<PyObjekt> LaufendeKredite() => Kredite.Values.Select(Objekt).Where(k => k["ende"] is null).ToList();

    /// <summary>done_loans</summary>
    public List<PyObjekt> FertigeKredite() => Kredite.Values.Select(Objekt).Where(k => k["ende"] is not null).ToList();

    /// <summary>loan_debt: Restschuld aller laufenden Kredite (mit offenen Zinsen und Gebuehren).</summary>
    public long KreditSchuld() => LaufendeKredite().Sum(k => Py.Int(k["rest"]) + Py.Int(k["zins_offen"]) + Py.Int(k["gebuehren"]));

    /// <summary>loan_rates: faellige Raten beim naechsten Feierabend.</summary>
    public long KreditRaten() => LaufendeKredite().Sum(k => Wirtschaft.KreditFaellig(k, Mahnstufe, Inhalt));

    // -- Lernen der Mitarbeiter (ab 0.38) ---------------------------------------

    /// <summary>_learn: Punkte durch Arbeit dazu (ab halb_ab halb so viele, nie ueber den Deckel).</summary>
    internal void Lernen(object? person, object? thema, object? menge, long tag, string grund)
    {
        if (!Personal.ContainsKey(person) || !T.IstThema(thema) || Py.F(menge) <= 0)
        {
            return;
        }
        var betrag = menge;
        if (Live)
        {
            var schluessel = Betrieb.HardwareThemen.Contains((string)thema!) ? "lernen_hardware" : "lernen_andere";
            betrag = Py.F(betrag) * Math.Max(0.0, 1 + (MackeWert(person, "lernen", tag) + MackeWert(person, schluessel, tag)) / 100.0);
            if (Py.F(betrag) <= 0)
            {
                return;
            }
        }
        var regel = Betrieb.LernRegeln(Inhalt);
        var vorher = Py.F(PersonalThemen(person, tag)[(string)thema!]);
        if (Py.F(vorher) >= Py.F(regel["halb_ab"]))
        {
            betrag = Py.F(betrag) / 2.0;
        }
        var deckel = Py.F(Betrieb.LernDeckel(Inhalt));
        var summe = vorher + Py.F(betrag);
        var nachher = summe < deckel ? summe : deckel;
        if (nachher <= vorher)
        {
            return;
        }
        var zuwachs = (PyObjekt)PersonalZuwachs.SetDefault(person, new PyObjekt())!;
        Py.Setzen(zuwachs, (string)thema!, Py.F(Get(zuwachs, (string)thema!, 0.0)) + nachher - vorher);
        LernProtokoll.Add(new PyTupel(tag, person, thema, vorher, nachher, grund));
    }

    /// <summary>_learn_from_ticket</summary>
    private void LernenAusTicket(PyObjekt eintrag, long tag) =>
        Lernen(eintrag["an"], Betrieb.TicketThema(eintrag["vorlage"], Inhalt), Betrieb.LernRegeln(Inhalt)["ticket_erfolg"], tag, "ticket");

    /// <summary>_learn_from_project</summary>
    private void LernenAusProjekt(PyObjekt eintrag, long tag)
    {
        var regel = Betrieb.LernRegeln(Inhalt);
        var projekt = Projekte.Get(eintrag["projekt"]) as PyObjekt ?? new PyObjekt();
        var thema = Betrieb.ProjektThema(projekt, Inhalt);
        var menge = Py.Plus(regel["projekt_fertig"], Py.Wahr(eintrag["verzug"]) ? 0L : regel["projekt_puenktlich"]);
        var leute = ListeOderLeer(eintrag, "beitraege").Select(a => Objekt(a)["an"]).ToList();
        foreach (var person in leute)
        {
            Lernen(person, thema, menge, tag, "projekt");
        }
        if (Live)
        {
            // Ab 0.43: Wer gern erklaert, bringt den anderen im Team etwas bei
            foreach (var lehrer in leute)
            {
                var extra = MackeWert(lehrer, "team_lernen", tag);
                foreach (var person in leute)
                {
                    if (extra != 0 && !Py.Gleich(person, lehrer))
                    {
                        Lernen(person, thema, extra, tag, "erklaert");
                    }
                }
            }
        }
    }

    /// <summary>_learn_routine</summary>
    private void LernenRoutine(PySet beschaeftigt, long tag)
    {
        var regel = Betrieb.LernRegeln(Inhalt);
        var alle = Math.Max(1, Py.Int(regel["routine_tage"]));
        foreach (var person in Personal.Keys.ToList())
        {
            if (beschaeftigt.Contains(person) || Abwesenheit(person, tag) is not null)
            {
                continue;
            }
            var anzahl = Py.Int(PersonalRoutine.Get(person, 0L)) + 1;
            PersonalRoutine[person] = anzahl;
            if (anzahl % alle == 0)
            {
                var themen = PersonalThemen(person, tag);
                string? bestes = null;
                foreach (var thema in T.Reihenfolge)
                {
                    if (bestes is null || Py.F(themen[thema]) > Py.F(themen[bestes]))
                    {
                        bestes = thema;
                    }
                }
                Lernen(person, bestes, regel["routine_plus"], tag, "routine");
            }
        }
    }

    // -- Gruendung und Gebaeude --------------------------------------------------

    private PyObjekt GruendungsZiel => Objekt(Balancing["gruendung"]);

    /// <summary>founding_progress: Fortschritt zum eigenen Unternehmen (0.0 bis 1.0).</summary>
    public double GruendungsFortschritt()
    {
        var ziel = GruendungsZiel;
        var kapital = Math.Min(1.0, Math.Max(0, Geld) / Py.F(ziel["startkapital"]));
        var ruf = Math.Min(1.0, MittlereReputation / Py.F(ziel["mindest_reputation"]));
        var fortschritt = Math.Min(kapital, ruf);
        var aufgaben = Liste(Inhalt["aufgaben"]);
        if (Py.Wahr(ziel["alle_erledigt"]) && aufgaben.Count > 0)
        {
            var fertig = aufgaben.Count(a => Geloest.Contains(Objekt(a)["id"]));
            fortschritt = Math.Min(fortschritt, fertig / (double)aufgaben.Count);
        }
        if (fortschritt > 0.99 && GruendungFehlt().Count > 0)
        {
            fortschritt = 0.99;
        }
        return fortschritt;
    }

    /// <summary>founding_missing: was fuer die Gruendung noch fehlt (leer = alles erfuellt).</summary>
    public List<string> GruendungFehlt()
    {
        var ziel = GruendungsZiel;
        var fehlt = new List<string>();
        if (Py.Wahr(Firma))
        {
            return fehlt;
        }
        if (Py.Kleiner(Geld, ziel["startkapital"]))
        {
            fehlt.Add(PyFormat.Prozent("noch %s Erspartes", Spiel.Wohnung.GanzeEuro(Py.Minus(ziel["startkapital"], Math.Max(0, Geld)))));
        }
        if (Py.Kleiner(MittlereReputation, ziel["mindest_reputation"]))
        {
            fehlt.Add(PyFormat.Prozent("Ansehen %d %% statt %d %%", Mathe.PythonRunden.Runde(MittlereReputation), ziel["mindest_reputation"]));
        }
        if (Py.Wahr(ziel["alle_erledigt"]) && !AllesErledigt())
        {
            var offen = Liste(Inhalt["aufgaben"]).Count(a => !Geloest.Contains(Objekt(a)["id"]));
            fehlt.Add(PyFormat.Prozent("%s bei Bitweiche offen", offen == 1 ? "1 Auftrag" : PyFormat.Prozent("%d Aufträge", (long)offen)));
        }
        return fehlt;
    }

    /// <summary>founding_ready</summary>
    public bool GruendungBereit() => Profil is not null && Firma is null && GruendungFehlt().Count == 0;

    private List<object?> GebaeudeStufen => Liste(Objekt(Objekt(Inhalt["firma"])["gebaeude"])["stufen"]);

    /// <summary>firm_stage: die Ausbaustufe des eigenen Gebaeudes.</summary>
    public PyObjekt FirmenStufe()
    {
        var stufen = GebaeudeStufen;
        var nummer = Firma is PyObjekt f ? Py.Int(f["stufe"]) : 1;
        return Objekt(stufen[(int)Math.Min(nummer, stufen.Count) - 1]);
    }

    /// <summary>next_stage</summary>
    public PyObjekt? NaechsteStufe()
    {
        var stufen = GebaeudeStufen;
        var nummer = Firma is PyObjekt f ? Py.Int(f["stufe"]) : 1;
        return nummer < stufen.Count ? Objekt(stufen[(int)nummer]) : null;
    }

    /// <summary>capacity: alle Arbeitsplaetze (Gewerbehof plus Filiale).</summary>
    public long Kapazitaet => OrtKapazitaet(OrtBuero) + OrtKapazitaet(OrtFiliale);

    /// <summary>site_capacity</summary>
    public long OrtKapazitaet(string ort)
    {
        if (!Py.Wahr(Firma))
        {
            return 0;
        }
        if (ort == OrtFiliale)
        {
            return FilialStufe() is { } stufe ? Py.Len(stufe["plaetze"]) : 0;
        }
        return Py.Len(FirmenStufe()["plaetze"]);
    }

    /// <summary>branch_stage: die Ausbaustufe der Filiale oder null.</summary>
    public PyObjekt? FilialStufe() => Zweigstelle is PyObjekt z && Py.Wahr(z) ? Betrieb.FilialStufe(z["stufe"], Inhalt) : null;

    /// <summary>next_branch_stage</summary>
    public PyObjekt? NaechsteFilialStufe() => Betrieb.FilialStufe((Zweigstelle is PyObjekt z && Py.Wahr(z) ? Py.Int(z["stufe"]) : 0) + 1, Inhalt);

    /// <summary>site_of</summary>
    public object? OrtVon(object? person) => PersonalOrt.Get(person, OrtBuero);

    /// <summary>site_staff</summary>
    public List<object?> PersonalAm(string ort) => Personal.Keys.Where(p => Py.Gleich(OrtVon(p), ort)).ToList();

    /// <summary>site_free: freie Arbeitsplaetze an einem Standort.</summary>
    public long OrtFrei(string ort) => OrtKapazitaet(ort) - PersonalAm(ort).Count;

    /// <summary>has_room</summary>
    public bool HatRaum(object? raum) => Raeume.ContainsKey(raum);

    /// <summary>room_effect: Vorteil aller ausgebauten Sonderraeume (Summe, bei "_faktor" das Produkt).</summary>
    public object? RaumEffekt(string schluessel, object? standard = null)
    {
        var werte = Raeume.Keys.Select(r => Get(Get(Betrieb.SonderRaum(r, Inhalt) ?? new PyObjekt(), "effekt", new PyObjekt()) as PyObjekt ?? [], schluessel))
            .Where(w => w is not null).ToList();
        if (werte.Count == 0)
        {
            return standard ?? 0L;
        }
        if (schluessel.EndsWith("_faktor", StringComparison.Ordinal))
        {
            var ergebnis = 1.0;
            foreach (var w in werte)
            {
                ergebnis *= Py.F(w);
            }
            return ergebnis;
        }
        return Py.Summe(werte);
    }

    /// <summary>firm_costs: Nebenkosten pro Arbeitstag (Gebaeudestufe, Sonderraeume, Filiale).</summary>
    public long FirmenKosten()
    {
        if (!Py.Wahr(Firma))
        {
            return 0;
        }
        var raeume = Raeume.Keys.Sum(r => Py.Int(Get(Betrieb.SonderRaum(r, Inhalt) ?? new PyObjekt(), "nebenkosten", 0L)));
        var filiale = Py.Int(Get(FilialStufe() ?? new PyObjekt(), "nebenkosten", 0L));
        return Py.Int(Get(FirmenStufe(), "nebenkosten", 0L)) + raeume + filiale;
    }

    // -- Mitarbeiter -------------------------------------------------------------

    /// <summary>training_of: laufende Weiterbildung am Tag (oder null).</summary>
    public PyObjekt? WeiterbildungVon(object? person, long? tag = null)
    {
        var t = tag ?? Tag;
        return Weiterbildungen.Select(Objekt).FirstOrDefault(e => Py.Gleich(e["id"], person)
            && Py.F(Get(e, "tag", 0L)) <= t && t < Py.F(Get(e, "bis_tag", 0L)));
    }

    /// <summary>staff_topics: Werte je Thema (Einstellung, Weiterbildungen, durch Arbeit gelernt).</summary>
    public PyObjekt PersonalThemen(object? person, long? tag = null, bool laufend = false)
    {
        var t = tag ?? Tag;
        var daten = Personal.Get(person) as PyObjekt ?? new PyObjekt();
        var schluessel = person ?? "None";
        if (!themenBasis.TryGetValue(schluessel, out var basis))
        {
            basis = Betrieb.BasisThemen(person, daten, Inhalt);
            themenBasis[schluessel] = basis;
        }
        var werte = Py.Flach(basis);
        foreach (var e in Weiterbildungen.Select(Objekt))
        {
            if (Py.Gleich(e["id"], person) && (laufend || Py.F(Get(e, "bis_tag", 0L)) <= t))
            {
                var plus = Py.Int(Get(e, "plus", 0L));
                // Vor 0.38 gab es nur Weiterbildungen je Fachbereich (ohne "thema")
                IEnumerable<object?> themen = Py.Wahr(e["thema"]) ? [e["thema"]]
                    : e["cat"] is string c && T.ThemenVon.TryGetValue(c, out var l) ? l : [];
                foreach (var thema in themen)
                {
                    if (thema is string th && Py.Hat(werte, th))
                    {
                        Py.Setzen(werte, th, Py.Plus(werte[th], plus));
                    }
                }
            }
        }
        if (PersonalZuwachs.Get(person) is PyObjekt zuwachs)
        {
            foreach (var (thema, plus) in zuwachs)
            {
                Py.Setzen(werte, thema, Py.Plus(Get(werte, thema, 0L), plus));
            }
        }
        var oben = Py.F(Betrieb.LernRegeln(Inhalt)["max"]);
        var ergebnis = new PyObjekt();
        foreach (var (thema, wert) in werte)
        {
            var w = Py.F(wert);
            var unten = w < oben ? w : oben;
            ergebnis.Add(thema, unten > 0.0 ? unten : 0.0);
        }
        return ergebnis;
    }

    /// <summary>staff_topic_value</summary>
    public long PersonalThemaWert(object? person, string thema, long? tag = null) => Py.Int(Get(PersonalThemen(person, tag), thema, 0L));

    /// <summary>staff_values: Werte je Fachbereich = Durchschnitt seiner Themen.</summary>
    public PyObjekt PersonalWerte(object? person, long? tag = null, bool laufend = false) => Betrieb.Fachwerte(PersonalThemen(person, tag, laufend));

    /// <summary>trainings_of</summary>
    public List<PyObjekt> WeiterbildungenVon(object? person) => Weiterbildungen.Select(Objekt).Where(e => Py.Gleich(e["id"], person)).ToList();

    // -- Personal (ab 0.43) -------------------------------------------------------

    /// <summary>absence_of: laufende Weiterbildung/Coaching oder krank/Urlaub am Tag, sonst null.</summary>
    public PyObjekt? Abwesenheit(object? person, long? tag = null)
    {
        var t = tag ?? Tag;
        if (WeiterbildungVon(person, t) is { } weiterbildung && Py.Wahr(weiterbildung))
        {
            return weiterbildung;
        }
        return Abwesenheiten.Select(Objekt).FirstOrDefault(e => Py.Gleich(e["id"], person) && Py.F(e["von"]) <= t && t < Py.F(e["bis"]));
    }

    /// <summary>away_of: krank oder im Urlaub (ohne Weiterbildung), sonst null.</summary>
    public PyObjekt? Weg(object? person, long? tag = null)
    {
        var e = Abwesenheit(person, tag);
        return e is not null && Py.Wahr(e) && e["art"] is Betrieb.AbwesendKrank or Betrieb.AbwesendUrlaub ? e : null;
    }

    /// <summary>quirk_of: die Macke mit Stufe am Tag.</summary>
    public PyObjekt? MackeVon(object? person, long? tag = null)
    {
        if (Personal.Get(person) is not PyObjekt daten)
        {
            return null;
        }
        var t = tag ?? Tag;
        var gecoacht = Weiterbildungen.Select(Objekt).Count(e => Py.Gleich(e["id"], person)
            && Py.Gleich(e["art"], Betrieb.Coaching) && Py.F(Get(e, "bis_tag", 0L)) <= t);
        // Ab 0.47 auf "Einfach": die Schwaeche wirkt nur halb so stark
        return Betrieb.MackeZustand(person, daten, t, gecoacht, Inhalt, Erleichterung("macken_schwaeche_faktor", 1.0));
    }

    /// <summary>quirk_value: Wirkung der Macke fuer einen Schluessel (0 ohne Macke).</summary>
    public double MackeWert(object? person, string schluessel, long? tag = null)
    {
        var macke = MackeVon(person, tag);
        return macke is not null ? Betrieb.MackeWirkung(macke, schluessel) : 0;
    }

    /// <summary>mood_of</summary>
    public long StimmungVon(object? person) =>
        Py.Rund(Stimmung.ContainsKey(person) ? Stimmung[person] : Objekt(Betrieb.PersonalRegeln(Inhalt)["stimmung"])["start"]);

    /// <summary>staff_form: Abzug durch schlechte Stimmung oder einen ignorierten Konflikt.</summary>
    public PyObjekt PersonalForm(object? person, long? tag = null)
    {
        var t = tag ?? Tag;
        var regeln = Betrieb.PersonalRegeln(Inhalt);
        var stimmung = Objekt(regeln["stimmung"]);
        var konflikt = Objekt(regeln["konflikt"]);
        object leistung = 0L, chance = 0L;
        var gruende = new List<object?>();
        if (Py.Kleiner(Stimmung.Get(person, 100L), stimmung["tief"]))
        {
            leistung = Py.Plus(leistung, stimmung["tief_leistung"]);
            chance = Py.Plus(chance, stimmung["tief_chance"]);
            gruende.Add("schlechte Stimmung");
        }
        if (Py.Int(Verstimmt.Get(person, 0L)) > t)
        {
            leistung = Py.Plus(leistung, konflikt["verstimmt_leistung"]);
            chance = Py.Plus(chance, konflikt["verstimmt_chance"]);
            gruende.Add("verstimmt nach dem Konflikt");
        }
        return new PyObjekt { { "leistung", leistung }, { "chance", chance }, { "gruende", gruende } };
    }

    /// <summary>staff_revenue_of: Routineumsatz pro Arbeitstag mit Macke, Stimmung und Konjunktur.</summary>
    public long PersonalUmsatz(object? person, PyObjekt werte, long? tag = null)
    {
        var basis = Betrieb.MitarbeiterUmsatz(werte, Inhalt);
        // Ab 0.44: Die Konjunktur hebt oder senkt den Routineumsatz
        var konjunktur = Py.F(Get(Objekt(Betrieb.Marktlage(this, tag ?? Tag, Inhalt)["phase_info"]), "umsatz", 0L));
        var faktor = (1 + MackeWert(person, "umsatz", tag) / 100.0) * (1 - Py.F(PersonalForm(person, tag)["leistung"]) / 100.0) * (1 + konjunktur / 100.0);
        return Mathe.PythonRunden.Runde(Math.Max(0.0, basis * faktor));
    }

    /// <summary>_mood_change</summary>
    private void StimmungAendern(object? person, object? delta)
    {
        if (Stimmung.ContainsKey(person))
        {
            Stimmung[person] = Math.Max(0.0, Math.Min(100.0, Py.F(Stimmung[person]) + Py.F(delta)));
        }
    }

    /// <summary>_mood_after_failure: ein verpatztes Kundenticket schlaegt aufs Gemuet (Kundenfluesterer).</summary>
    private void StimmungNachFehlschlag(PyObjekt eintrag, long tag) =>
        StimmungAendern(eintrag["an"], -MackeWert(eintrag["an"], "stimmung_fehlschlag", tag));

    /// <summary>_apply_decision: Antwort auf Urlaubsanfrage, Konflikt oder Rueckhol-Angebot (nur die erste zaehlt).</summary>
    private void EntscheidungAnwenden(string art, PyObjekt daten, long tag)
    {
        var schluessel = Py.Wahr(daten["anfrage"]) ? daten["anfrage"] : Py.Wahr(daten["konflikt"]) ? daten["konflikt"] : daten["rueckhol"];
        if (!Py.Wahr(schluessel) || Antworten.ContainsKey(schluessel))
        {
            return;
        }
        var regeln = Betrieb.PersonalRegeln(Inhalt);
        var antwort = Py.Flach(daten);
        Py.Setzen(antwort, "art", art);
        Antworten[schluessel] = antwort;
        if (art == Ereignisse.UrlaubJa)
        {
            var person = daten["id"];
            Abwesenheiten.Add(new PyObjekt
            {
                { "id", person }, { "art", Betrieb.AbwesendUrlaub }, { "von", Py.Int(Get(daten, "von", 0L)) }, { "bis", Py.Int(Get(daten, "bis", 0L)) },
            });
            StimmungAendern(person, Objekt(regeln["urlaub"])["genehmigt"]);
        }
        else if (art == Ereignisse.UrlaubNein)
        {
            // Zweimal hintereinander abgelehnt: das sitzt tiefer
            var vorher = Antworten.Values.Select(Objekt).Where(e => Py.Gleich(e["id"], daten["id"]) && Py.Wahr(e["anfrage"])
                && !Py.Gleich(e["anfrage"], schluessel)).ToList();
            var wieder = vorher.Count > 0 && Py.Gleich(vorher[^1]["art"], Ereignisse.UrlaubNein);
            var urlaub = Objekt(regeln["urlaub"]);
            StimmungAendern(daten["id"], Py.Rund(Py.Mal(urlaub[wieder ? "abgelehnt_wieder" : "abgelehnt"], Erleichterung("urlaub_abgelehnt_faktor", 1.0))));
        }
        else if (art == Ereignisse.Konflikt)
        {
            var regel = Objekt(regeln["konflikt"]);
            var (erster, zweiter) = (daten["a"], daten["b"]);
            var wahl = daten["wahl"];
            if (Py.Gleich(wahl, Betrieb.Schlichten))
            {
                SchlichtTage.Add(Py.Int(Get(daten, "tag", tag)));
                StimmungAendern(erster, regel["schlichten"]);
                StimmungAendern(zweiter, regel["schlichten"]);
            }
            else if (Py.Gleich(wahl, Betrieb.SeiteA) || Py.Gleich(wahl, Betrieb.SeiteB))
            {
                var (gewinner, verlierer) = Py.Gleich(wahl, Betrieb.SeiteA) ? (erster, zweiter) : (zweiter, erster);
                StimmungAendern(gewinner, regel["partei_plus"]);
                StimmungAendern(verlierer, regel["partei_minus"]);
            }
            else
            {
                var bis = Py.Int(Get(daten, "tag", tag)) + Py.Int(regel["verstimmt_tage"]);
                foreach (var person in new[] { erster, zweiter })
                {
                    StimmungAendern(person, regel["ignorieren"]);
                    Verstimmt[person] = Math.Max(Py.Int(Verstimmt.Get(person, 0L)), bis);
                }
            }
        }
        else if (art == Ereignisse.Rueckhol && Personal.ContainsKey(daten["id"]))
        {
            var regel = Get(Betrieb.RivalRegeln(Inhalt), "rueckhol") is PyObjekt r && Py.Wahr(r) ? r : new PyObjekt();
            var person = daten["id"];
            var wahl = daten["wahl"];
            var mitarbeiter = Objekt(Personal[person]);
            if (Py.Gleich(wahl, Betrieb.RueckholGehalt))
            {
                Py.Setzen(mitarbeiter, "gehalt", Py.Int(Py.Wahr(daten["gehalt"]) ? daten["gehalt"] : Get(mitarbeiter, "gehalt", 0L)));
                StimmungAendern(person, Get(regel, "gehalt_stimmung", 0L));
            }
            else if (Py.Gleich(wahl, Betrieb.RueckholBonus))
            {
                Geld += Py.Int(Get(daten, "geld", 0L));
                Buchen(tag, Ereignisse.BuchGehaelter, Get(daten, "geld", 0L));
                StimmungAendern(person, Get(regel, "bonus_stimmung", 0L));
            }
            else
            {
                Gehend[person] = Py.Int(Get(daten, "tag", tag));
                Antworten["leaving:" + Py.Str(person)] = Py.Flach(daten);
            }
        }
    }

    /// <summary>_personal_day: Stimmung erholt sich, dazu die Personal-Ereignisse des Feierabends.</summary>
    private void PersonalTag(long tag, List<object?> eintraege)
    {
        var regeln = Objekt(Betrieb.PersonalRegeln(Inhalt)["stimmung"]);
        var aufmuntern = Personal.Keys.Select(p => (Person: p, Wert: MackeWert(p, "stimmung_team", tag))).ToList();
        foreach (var person in Personal.Keys.ToList())
        {
            var schritt = Py.F(regeln["erholung"]) + PythonSummeOhne(aufmuntern, person);
            var wert = Stimmung.ContainsKey(person) ? Py.F(Stimmung[person]) : Py.F(regeln["start"]);
            var ziel = Py.F(regeln["ziel"]);
            if (wert < ziel)
            {
                wert = Math.Min(ziel, wert + schritt);
            }
            else if (wert > ziel)
            {
                wert = Math.Max(ziel, wert - Py.F(regeln["erholung"]));
            }
            Stimmung[person] = wert;
            StimmungTief[person] = wert < Py.F(regeln["kritisch"]) ? Py.Int(StimmungTief.Get(person, 0L)) + 1 : 0L;
        }
        foreach (var e in eintraege.Select(Objekt))
        {
            PersonalProtokoll.Add(new PyTupel(tag, Py.Flach(e)));
            var art = e["art"];
            if (Py.Gleich(art, Betrieb.PersonalKrank) && Personal.ContainsKey(e["id"]))
            {
                Abwesenheiten.Add(new PyObjekt { { "id", e["id"] }, { "art", Betrieb.AbwesendKrank }, { "von", Py.Int(e["von"]) }, { "bis", Py.Int(e["bis"]) } });
            }
            else if (Py.Gleich(art, Betrieb.PersonalKuendigt) && Personal.ContainsKey(e["id"]))
            {
                MitarbeiterGeht(e["id"]);
                SelbstGekuendigt[e["id"]] = tag;
            }
        }
    }

    /// <summary>sum(value for other, value in cheer.items() if other != staff_id) wie Python (Neumaier bei Kommazahlen).</summary>
    private static double PythonSummeOhne(List<(object? Person, double Wert)> werte, object? ohne) =>
        Py.F(Py.Summe(werte.Where(w => !Py.Gleich(w.Person, ohne)).Select(w => (object?)w.Wert)));

    /// <summary>_staff_leaves: die Person verlaesst die Firma (aus Personal, Standort, Projekt- und Farm-Team).</summary>
    private PyObjekt? MitarbeiterGeht(object? person)
    {
        var daten = Personal.Get(person) as PyObjekt;
        Personal.Pop(person);
        PersonalOrt.Pop(person);
        foreach (var projekt in Projekte.Values.Select(Objekt))
        {
            if (Py.Enthaelt(projekt["team"], person))
            {
                Py.Setzen(projekt, "team", Liste(projekt["team"]).Where(p => !Py.Gleich(p, person)).ToList());
            }
        }
        if (Farm is PyObjekt farm && Py.Enthaelt(farm["team"], person))
        {
            Py.Setzen(farm, "team", Liste(farm["team"]).Where(p => !Py.Gleich(p, person)).ToList());
        }
        return daten;
    }

    /// <summary>left_firm: war die Person hier angestellt und ist es nicht mehr?</summary>
    public bool Ausgeschieden(object? person) => JeEingestellt.Contains(person) && !Personal.ContainsKey(person);

    /// <summary>open_decisions: offene Entscheidungen zum Personal.</summary>
    public List<PyObjekt> OffeneEntscheidungen() => Py.Wahr(Firma) ? Betrieb.PersonalEntscheidungen(this, Inhalt) : [];

    /// <summary>staff_list: Mitarbeiter in der Reihenfolge der Einstellung, mit Arbeitsplatz je Standort.</summary>
    public List<PyObjekt> PersonalListe()
    {
        var plaetze = new Dictionary<string, List<object?>>
        {
            [OrtBuero] = Py.Wahr(Firma) ? Liste(FirmenStufe()["plaetze"]) : [],
            [OrtFiliale] = Get(FilialStufe() ?? new PyObjekt(), "plaetze") is List<object?> l && l.Count > 0 ? l : [],
        };
        var besetzt = new Dictionary<string, int> { [OrtBuero] = 0, [OrtFiliale] = 0 };
        var ergebnis = new List<PyObjekt>();
        var farmTeam = FarmTeam();
        foreach (var (person, wert) in Personal)
        {
            var themen = PersonalThemen(person);
            var e = Py.Flach(Objekt(wert));
            Py.Setzen(e, "werte", Betrieb.Fachwerte(themen));
            Py.Setzen(e, "themen", themen);
            Py.Setzen(e, "staerken", Betrieb.StaerksteThemen(themen));
            Py.Setzen(e, "weiterbildung", WeiterbildungVon(person));
            Py.Setzen(e, "abwesend", Weg(person));
            Py.Setzen(e, "projekt", ProjektVon(person));
            Py.Setzen(e, "serverfarm", Py.Enthaelt(farmTeam, person));
            Py.Setzen(e, "macke_info", MackeVon(person));
            Py.Setzen(e, "stimmung", StimmungVon(person));
            Py.Setzen(e, "form", PersonalForm(person));
            // Wer in einem Projekt mitarbeitet, macht keine Routineauftraege; im Serverfarm-Team
            // laeuft ein Teil davon nebenbei weiter (ab 0.52).
            long umsatz = Py.Wahr(e["weiterbildung"]) || Py.Wahr(e["abwesend"]) || Py.Wahr(e["projekt"]) ? 0 : PersonalUmsatz(person, Objekt(e["werte"]));
            Py.Setzen(e, "umsatz", umsatz);
            if (Py.Wahr(e["serverfarm"]) && umsatz != 0)
            {
                Py.Setzen(e, "umsatz", Mathe.PythonRunden.Runde(umsatz * Py.F(Betrieb.FarmTeamUmsatz(Inhalt)) / 100.0));
            }
            var ort = (string)OrtVon(person)!;
            Py.Setzen(e, "standort", ort);
            var index = besetzt[ort];
            besetzt[ort] += 1;
            if (index < plaetze[ort].Count)
            {
                Py.Setzen(e, "platz", plaetze[ort][index]);
            }
            ergebnis.Add(e);
        }
        return ergebnis;
    }

    /// <summary>firm_day: Umsatz, Gehaelter und Nebenkosten des laufenden Arbeitstags.</summary>
    public PyObjekt FirmenTag()
    {
        if (!Py.Wahr(Firma))
        {
            return new PyObjekt();
        }
        var liste = PersonalListe();
        return new PyObjekt
        {
            { "umsatz", liste.Sum(e => Py.Int(e["umsatz"])) }, { "gehaelter", liste.Sum(e => Py.Int(Get(e, "gehalt", 0L))) }, { "nebenkosten", FirmenKosten() },
        };
    }

    /// <summary>firm_seed</summary>
    public string FirmenStartwert => Firma is PyObjekt f && Py.Wahr(f) ? Py.Str(f["name"]) + "|" + Py.Str(f["tag"]) : "";

    /// <summary>inquiries: Kundenanfragen des laufenden Arbeitstags.</summary>
    public List<PyObjekt> Anfragen() => Betrieb.AnfragenFuerTag(this, Tag, Inhalt);

    /// <summary>customer_tickets: Kundentickets des laufenden Arbeitstags.</summary>
    public List<PyObjekt> KundenTickets() => Betrieb.TicketsFuerTag(this, Tag, Inhalt);

    /// <summary>delegated_to: Kundentickets, die eine Person am Tag uebernommen hat.</summary>
    public List<PyObjekt> UebernommenVon(object? person, long? tag = null)
    {
        var t = tag ?? Tag;
        return Delegationen.Values.Select(Objekt).Where(e => Py.Gleich(e["tag"], t) && Py.Gleich(e["an"], person)).ToList();
    }

    /// <summary>firm_open_count: offene Anfragen und noch nicht verteilte Kundentickets heute.</summary>
    public long FirmaOffen() => !Py.Wahr(Firma) ? 0
        : Anfragen().Count(e => !Py.Wahr(e["ergebnis"])) + KundenTickets().Count(e => !Py.Wahr(e["an"]));

    // -- Kundenprojekte (ab 0.35) ------------------------------------------

    /// <summary>running_projects: gewonnene, noch nicht fertige Projekte (aelteste zuerst).</summary>
    public List<PyObjekt> LaufendeProjekte() => Projekte.Values.Select(Objekt).Where(p => !Py.Wahr(p["fertig"])).ToList();

    /// <summary>done_projects</summary>
    public List<PyObjekt> FertigeProjekte() => Projekte.Values.Select(Objekt).Where(p => Py.Wahr(p["fertig"])).ToList();

    /// <summary>project_of: Kennung des laufenden Projekts, in dem eine Person mitarbeitet.</summary>
    public object? ProjektVon(object? person) => LaufendeProjekte().FirstOrDefault(p => Py.Enthaelt(p["team"], person))?["projekt"];

    /// <summary>project_limit</summary>
    public long ProjektGrenze() => Betrieb.ProjektGrenze(this, Inhalt);

    /// <summary>tenders: Projektausschreibungen heute (mit Ergebnis, falls schon angeboten).</summary>
    public List<PyObjekt> Ausschreibungen() => [.. Betrieb.ProjektAusschreibungen(this, Tag, Inhalt), .. Betrieb.GrossAusschreibungen(this, Tag, Inhalt)];
}
