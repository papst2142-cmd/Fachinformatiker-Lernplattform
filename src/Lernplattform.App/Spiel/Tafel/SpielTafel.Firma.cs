using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Firma wie GameScreen.open_firm und _fill_firm (mobile/src/spiel.py): Kopf oder Gruendung, Reiter
/// (Betrieb.Reiter) und je Reiter eine Seite. Hier: Auftraege, Projekte, Mitarbeiter, Bewerbungen;
/// Gebaeude, Finanzen, Marketing, Zertifizierungen und Kredite in SpielTafel.Firma.Geld.cs.
/// </summary>
public sealed partial class SpielTafel
{
    /// <summary>MOOD_COLOR</summary>
    private static readonly IReadOnlyDictionary<string, string> StimmungFarbe = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["gut"] = "green", ["okay"] = "text_dim", ["schlecht"] = "yellow", ["kritisch"] = "pink",
    };

    // Zustand der Firmenansicht wie in GameScreen (firm_tab, offer_for, markup, ...)
    private string _firmaReiter = "auftraege";
    private string? _angebotFuer;
    private long? _zuschlag;
    private FormularBrett? _angebotBrett;
    private bool _angebotHilfe;
    private string? _zuweisenFuer;
    private string? _teamFuer;
    private string? _weiterbildungFuer;
    private string? _weiterbildungFach;
    private readonly HashSet<string> _detailsFuer = new(StringComparer.Ordinal);
    private readonly HashSet<string> _themenFuer = new(StringComparer.Ordinal);
    private readonly HashSet<(string, string)> _faecherOffen = [];

    /// <summary>open_firm (open_orders, open_projects mit reiter).</summary>
    public void FirmaOeffnen(string? reiter = null)
    {
        if (reiter is not null)
        {
            _firmaReiter = reiter;
        }
        _weiterbildungFuer = _weiterbildungFach = null;
        try
        {
            _s.Verwaltung.Neuladen();
        }
        catch (SpielBelegtAusnahme)
        {
            // Lesen geht weiter mit dem Stand im Speicher
        }
        Oeffnen(Krumen("SPIEL", "FIRMA"), () => Firma(Stand));
    }

    /// <summary>_fill_firm</summary>
    private IEnumerable<Control?> Firma(Spielstand stand)
    {
        yield return PlatzLeiste();
        yield return Py.Wahr(stand.Firma) ? FirmenKopf(stand) : FirmenGruendung(stand);
        var reiter = Betrieb.Reiter(stand);
        if (!reiter.Any(r => r.Id == _firmaReiter))
        {
            _firmaReiter = reiter[0].Id;
        }
        if (reiter.Count > 1)
        {
            yield return Pillen(reiter, _firmaReiter, FirmaWaehlen);
        }
        var seite = _firmaReiter switch
        {
            "projekte" => FirmaProjekte(stand),
            "mitarbeiter" => FirmaMitarbeiter(stand),
            "bewerbungen" => FirmaBewerbungen(stand),
            "gebaeude" => FirmaGebaeude(stand),
            "finanzen" => FirmaFinanzen(stand),
            "marketing" => FirmaMarketing(stand),
            "zertifizierungen" => FirmaZertifizierungen(stand),
            "kredite" => FirmaKredite(stand),
            _ => FirmaAuftraege(stand),
        };
        foreach (var teil in seite)
        {
            yield return teil;
        }
    }

    /// <summary>_firm_choose</summary>
    private void FirmaWaehlen(string reiter)
    {
        _firmaReiter = reiter;
        _weiterbildungFuer = _weiterbildungFach = null;
        _angebotFuer = _zuweisenFuer = _teamFuer = null;
        Neu();
    }

    /// <summary>_firm_changed: notify_progress, render, _fill_firm.</summary>
    private void FirmaGeaendert() => Fortschritt();

    /// <summary>Eine Firmen-Aktion ausfuehren (try: self.game... except ValueError: toast).</summary>
    private bool FirmaTun(Func<Spielablauf, PyObjekt> aktion) => Tun(aktion) is not null;

    /// <summary>app.confirm(titel, text, confirmed)</summary>
    private static async void Bestaetigen(string titel, string text, Action bestaetigt)
    {
        if (await Dialog.Zeigen(titel, text, "Abbrechen", "Ja") == 1)
        {
            bestaetigt();
        }
    }

    private static Button Breit(Button knopf)
    {
        knopf.HorizontalAlignment = HorizontalAlignment.Stretch;
        return knopf;
    }

    /// <summary>_person_box</summary>
    private static Border PersonKasten(IEnumerable<Control?> teile, string rand = "border") =>
        Flaeche(Stapel(6, teile), "card_alt", rand, innen: new Thickness(12));

    /// <summary>_stack_large: ab 160 % untereinander in voller Breite, sonst nebeneinander mit Umbruch.</summary>
    private static Control Knopfreihe(params Button[] knoepfe) =>
        Schrift.Gross ? Stapel(8, knoepfe.Select(k => (Control?)Breit(k))) : Reihe(knoepfe);

    /// <summary>Farbstreifen und zwei Zeilen (Titel, Kunde) wie _project_title.</summary>
    private static Control TitelMitStreifen(object? titel, object? unter, object? kategorie, string? marke = null)
    {
        var farbe = FachFarbe(kategorie);
        var zeilen = Stapel(2, [T(Py.Str(titel), 15, "text", true), T(Py.Str(unter), 12, farbe, true),
            string.IsNullOrEmpty(marke) ? null : T(marke, 12, "yellow", true)]);
        return Zeile(zeilen, null, 10, Strich(farbe, 6, 38));
    }

    // -- Kopf und Gruendung -----------------------------------------------------

    /// <summary>_firm_head</summary>
    private Control FirmenKopf(Spielstand stand)
    {
        var firma = Objekt(stand.Firma);
        var stufe = stand.FirmenStufe();
        return Karte("Eigene Firma", [
            T(Py.Str(firma["name"]), 22, "text", true),
            T(PyFormat.Prozent("%s · %s", Objekt(Betrieb.FirmaRegeln(I)["gebaeude"])["name"], stufe["name"]), 14, "green", true),
            T("Kontostand: " + Euro(stand.Geld), 13, stand.Geld >= 0 ? "text_dim" : "red"),
            T(Betrieb.FirmenZeile(stand), 13, "text_dim"),
        ], "green", PyFormat.Prozent("seit Tag %d · %s", firma["tag"], Spielstaende.SchwierigkeitAnzeige(stand)));
    }

    /// <summary>_firm_founding</summary>
    private Control FirmenGruendung(Spielstand stand)
    {
        var fehlt = stand.GruendungFehlt();
        if (fehlt.Count > 0)
        {
            var anteil = stand.GruendungsFortschritt() * 100;
            return Karte("Eigenes Unternehmen", [
                Balken("Weg zum eigenen Unternehmen", "green", "accent", anteil, PyFormat.Prozent("%d %%", Core.Mathe.PythonRunden.Runde(anteil))),
                T(PyFormat.Prozent("Noch nicht so weit: %s.", string.Join(", ", fehlt)), 14, "text_soft"),
                T("Sobald alles erfüllt ist, kannst du hier als Konkurrenz zu Bitweiche deine eigene Firma gründen.", 12, "muted"),
            ], "green");
        }
        var name = Eingabe(Betrieb.StandardFirmenName(stand), "Wie heißt deine Firma?");
        return Karte("Eigenes Unternehmen", [
            T(Betrieb.GruendungText(I), 14, "text_soft"),
            Label("Firmenname"), name,
            Breit(Knopf("Firma gründen", () => Gruenden(name.Text))),
        ], "green");
    }

    /// <summary>_found</summary>
    private void Gruenden(string? name)
    {
        var kosten = Objekt(Betrieb.FirmaRegeln(I)["gruendung"])["kosten"];
        Bestaetigen("Firma gründen", PyFormat.Prozent("„%s“ für %s gründen? Danach arbeitest du nicht mehr bei Bitweiche.",
            (name ?? "").Trim(), Euro(kosten)), () =>
        {
            if (Tun(a => GruendenAm(a, name)) is not { } daten)
            {
                return;
            }
            _firmaReiter = "bewerbungen";
            FirmaGeaendert();
            _s.Spiel.NotizSetzen("Willkommen in deiner Firma", Betrieb.GegruendetText(Py.Str(daten["name"]), I));
        });
    }

    // -- Auftraege ----------------------------------------------------------------

    /// <summary>_firm_auftraege</summary>
    private IEnumerable<Control?> FirmaAuftraege(Spielstand stand)
    {
        var regeln = Betrieb.AngebotRegeln(I);
        var angebote = new List<Control?>
        {
            T("Wähle deinen Gewinnzuschlag und rechne das Angebot durch. Gegen dich bieten Bitweiche und andere Firmen, wer genau, " +
                "siehst du erst im Ergebnis. Bist du am günstigsten und stimmt die Rechnung, bekommst du den Auftrag. Offene Anfragen " +
                "verfallen beim Feierabend.", 12, "text_dim"),
        };
        var markt = Betrieb.MarktStand(stand, I);
        if (markt.Count > 0)
        {
            angebote.Add(MarktKasten(markt));
        }
        foreach (var anfrage in stand.Anfragen())
        {
            angebote.Add(AnfrageKasten(anfrage));
        }
        var tickets = stand.KundenTickets();
        var frei = tickets.Count(t => !Py.Wahr(t["an"]));
        var grenzen = Betrieb.TicketRegeln(I);
        var zeilen = new List<Control?>
        {
            T(PyFormat.Prozent("Verteile die Tickets an deine Leute oder übernimm selbst welche (höchstens %d, mit deinem Wissensstand). " +
                "Jeder Mitarbeiter schafft %s pro Tag, die Chance hängt vom Wert im Thema des Tickets ab.",
                grenzen["spieler_max"], PyFormat.Plural(grenzen["mitarbeiter_max"], "Ticket", "Tickets")), 12, "text_dim"),
        };
        var stufen = _s.Verwaltung.Ablauf?.FirmenStufen();
        foreach (var ticket in tickets)
        {
            zeilen.Add(KundenTicketKasten(stand, ticket, stufen));
        }
        yield return Karte("Kundenanfragen", angebote, "pink",
            PyFormat.Prozent("Handlungskosten %d %%, USt %d %%", regeln["handlungskosten"], regeln["ust"]));
        yield return Karte("Kundentickets", zeilen, "accent", PyFormat.Prozent("%d von %d verteilt", tickets.Count - frei, tickets.Count));
    }

    /// <summary>market_box</summary>
    private static Control MarktKasten(List<(string Kopf, string Text)> zeilen)
    {
        var teile = new List<Control?> { Label("Markt", "accent") };
        foreach (var (kopf, text) in zeilen)
        {
            teile.Add(T(kopf, 13, "text", true));
            teile.Add(T(text, 11, "text_dim"));
        }
        return Flaeche(Stapel(3, teile), Mix("card", "accent", 0.08), Mix("accent", "card", 0.45), innen: new Thickness(12));
    }

    /// <summary>_inquiry_box</summary>
    private Control AnfrageKasten(PyObjekt anfrage)
    {
        var ergebnis = anfrage["ergebnis"] as PyObjekt;
        var teile = new List<Control?>
        {
            T(Py.Str(Objekt(anfrage["kunde"])["name"]), 15, "text", true),
            T(PyFormat.Prozent("%d × %s", anfrage["menge"], anfrage["artikel"]), 13, "pink", true),
        };
        if (Betrieb.AnfrageMarkeText(anfrage) is { Length: > 0 } marke)
        {
            teile.Add(T(marke, 12, "yellow", true));
        }
        teile.Add(T(Py.Str(anfrage["text"]), 13, "text_soft"));
        teile.Add(T(Betrieb.AnfrageStandText(anfrage, I), 12, ergebnis is null || !Py.Wahr(ergebnis) ? "text_dim"
            : Py.Wahr(ergebnis["gewonnen"]) ? "green" : "red"));
        if (ergebnis is not null && Py.Wahr(ergebnis))
        {
            teile.AddRange(ErgebnisZeilen(ergebnis));
        }
        else if (_angebotFuer == Py.Str(anfrage["id"]))
        {
            teile.AddRange(AngebotRechnen(anfrage));
        }
        else
        {
            var id = Py.Str(anfrage["id"]);
            teile.Add(Breit(Knopf("Angebot kalkulieren", () => Kalkulieren(id), hoehe: 38)));
        }
        return PersonKasten(teile);
    }

    /// <summary>offer_result_text und die Probleme der Rechnung.</summary>
    private IEnumerable<Control?> ErgebnisZeilen(PyObjekt ergebnis)
    {
        var (_, text) = Betrieb.AngebotErgebnisText(ergebnis, I);
        yield return T(text, 12, "text_soft");
        foreach (var zeile in ListeOderLeer(ergebnis, "probleme"))
        {
            yield return T("• " + Py.Str(zeile), 11, "red");
        }
    }

    /// <summary>_offer_calc</summary>
    private IEnumerable<Control?> AngebotRechnen(PyObjekt anfrage, bool projekt = false)
    {
        var regeln = Betrieb.AngebotRegeln(I);
        var hilfen = projekt ? Betrieb.ProjektRegeln(I)["hilfe"] : regeln["hilfe"];
        var werte = Liste(regeln["zuschlaege"]).Select(Py.Int).ToList();
        yield return Label("Gewinnzuschlag");
        yield return Pillen(werte.Select(w => (w.ToString(System.Globalization.CultureInfo.InvariantCulture), PyFormat.Prozent("%d %%", w))),
            _zuschlag is { } z && werte.Contains(z) ? z.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
            w => { _zuschlag = long.Parse(w, System.Globalization.CultureInfo.InvariantCulture); Neu(); });
        var abbrechen = Knopf("Abbrechen", KalkulierenAbbrechen, "ghost", 38);
        if (_zuschlag is null)
        {
            yield return T("Je höher der Zuschlag, desto mehr bleibt hängen, aber desto eher ist ein Mitbewerber günstiger.", 12, "muted");
            yield return Breit(abbrechen);
            yield break;
        }
        var alt = _angebotBrett?.Antwort as PyObjekt;
        _angebotBrett = new FormularBrett(projekt ? Betrieb.ProjektAufgabe(anfrage, _zuschlag, I) : Betrieb.AnfrageAufgabe(anfrage, _zuschlag, I), I);
        if (alt is not null)
        {
            _angebotBrett.Vorbelegen(alt);
        }
        yield return _angebotBrett;
        if (_angebotHilfe)
        {
            var liste = hilfen as List<object?> ?? [];
            yield return Flaeche(Stapel(4, liste.Select(z => (Control?)T("• " + Py.Str(z), 12, "text_soft"))),
                Mix("card_alt", "accent", 0.08), Mix("card_alt", "accent", 0.08), ecken: 10, innen: new Thickness(10), randBreite: 0);
        }
        yield return Breit(Knopf("Angebot abschicken", () => AngebotSenden(anfrage, projekt), hoehe: 40));
        var hilfe = Knopf(_angebotHilfe ? "Hilfe ausblenden" : "Hilfe", () => { _angebotHilfe = !_angebotHilfe; Neu(); }, "ghost", 38);
        yield return Schrift.Gross ? Stapel(8, [Breit(hilfe), Breit(abbrechen)]) : Zeile(hilfe, abbrechen, 8);
    }

    /// <summary>_calc</summary>
    private void Kalkulieren(string id)
    {
        _angebotFuer = id;
        _zuschlag = null;
        _angebotBrett = null;
        _angebotHilfe = false;
        Neu();
    }

    /// <summary>_cancel_calc</summary>
    private void KalkulierenAbbrechen()
    {
        _angebotFuer = null;
        _angebotBrett = null;
        Neu();
    }

    /// <summary>_send_offer</summary>
    private void AngebotSenden(PyObjekt anfrage, bool projekt)
    {
        if (_angebotBrett is null || !_angebotBrett.Fertig)
        {
            Hinweisen("Bitte rechne das Angebot zuerst durch.", "yellow");
            return;
        }
        var antwort = (PyObjekt)_angebotBrett.Antwort!;
        var zuschlag = _zuschlag;
        if (Tun(a => projekt ? a.ProjektAnbieten(anfrage["id"], zuschlag, antwort) : a.Anbieten(anfrage["id"], zuschlag, antwort)) is not { } daten)
        {
            return;
        }
        _angebotFuer = null;
        _angebotBrett = null;
        var (kopf, _) = Betrieb.AngebotErgebnisText(daten, I);
        Hinweisen(kopf, Py.Wahr(daten["gewonnen"]) ? "green" : "yellow");
        FirmaGeaendert();
    }

    /// <summary>_customer_ticket_box</summary>
    private Control KundenTicketKasten(Spielstand stand, PyObjekt ticket, PyObjekt? stufen)
    {
        var vergeben = Py.Wahr(ticket["an"]);
        var teile = new List<Control?>
        {
            TitelMitStreifen(ticket["titel"], Objekt(ticket["kunde"])["name"], ticket["cat"]),
            T(Py.Str(ticket["text"]), 13, "text_soft"),
            T(Betrieb.TicketZeile(ticket), 12, vergeben ? "green" : "text_dim"),
        };
        var id = Py.Str(ticket["id"]);
        if (vergeben)
        {
            return PersonKasten(teile);
        }
        if (_zuweisenFuer != id)
        {
            teile.Add(Breit(Knopf("Zuweisen", () => { _zuweisenFuer = id; Neu(); }, hoehe: 38)));
            return PersonKasten(teile);
        }
        var thema = Py.Str(ticket["thema"]);
        teile.Add(Label(PyFormat.Prozent("Wer übernimmt? (Wert in %s)", SpielThemen.Jetzt.Kurzname.GetValueOrDefault(thema, ""))));
        foreach (var option in Betrieb.TicketKandidaten(stand, ticket, stufen, I))
        {
            var problem = Py.Wahr(option["problem"]);
            var an = option["an"];
            teile.Add(Breit(Knopf(PyFormat.Prozent("%s · %d · Chance %d %%", option["name"], option["wert"], option["chance"]),
                () => Delegieren(ticket["id"], an), "ghost", 38, !problem)));
            if (problem)
            {
                teile.Add(T(Py.Str(option["problem"]), 11, "muted"));
            }
        }
        teile.Add(Breit(Knopf("Abbrechen", () => { _zuweisenFuer = null; Neu(); }, "ghost", 38)));
        return PersonKasten(teile);
    }

    /// <summary>_delegate</summary>
    private void Delegieren(object? ticket, object? person)
    {
        if (FirmaTun(a => a.Delegieren(ticket, person)))
        {
            _zuweisenFuer = null;
            FirmaGeaendert();
        }
    }

    // -- Projekte -------------------------------------------------------------

    /// <summary>_firm_projekte</summary>
    private IEnumerable<Control?> FirmaProjekte(Spielstand stand)
    {
        var laufend = stand.LaufendeProjekte();
        var stufen = _s.Verwaltung.Ablauf?.FirmenStufen();
        var zeilen = new List<Control?>();
        if (laufend.Count == 0)
        {
            zeilen.Add(T("Gerade läuft kein Projekt. Gib unten ein Angebot für eine Ausschreibung ab. Gewinnst du, stellst du hier das Team zusammen.",
                14, "text_soft"));
        }
        foreach (var projekt in laufend)
        {
            zeilen.Add(ProjektKasten(stand, projekt, stufen));
        }
        var regeln = Betrieb.ProjektRegeln(I);
        var ausschreibungen = new List<Control?>
        {
            T(PyFormat.Prozent("Rechne das Angebot wie bei den Anfragen: Projektarbeit (Punkte × %s) plus Material, dazu Handlungskosten und " +
                "dein Zuschlag. Bei Projekten bieten meist zwei oder drei Firmen mit. Bei Gewinn gibt es %d %% Anzahlung, den Rest bei Fertigstellung.",
                Euro(regeln["stundensatz"]), regeln["anzahlung"]), 12, "text_dim"),
        };
        var problem = Betrieb.ProjektAngebotProblem(stand, I);
        var liste = stand.Ausschreibungen();
        if (liste.Count == 0)
        {
            ausschreibungen.Add(T("Gerade liegt keine Ausschreibung vor.", 14, "text_soft"));
        }
        foreach (var projekt in liste)
        {
            ausschreibungen.Add(AusschreibungKasten(stand, projekt, problem));
        }
        yield return Karte("Laufende Projekte", zeilen, "green", PyFormat.Prozent("%d von %d", laufend.Count, stand.ProjektGrenze()));
        yield return Karte("Ausschreibungen", ausschreibungen, "pink",
            "alle " + PyFormat.Plural(regeln["abstand_tage"], "Arbeitstag", "Arbeitstage") + " eine neue");
        var fertig = stand.FertigeProjekte();
        if (fertig.Count > 0)
        {
            var fertigZeilen = new List<Control?>();
            foreach (var projekt in Enumerable.Reverse(fertig.TakeLast(6)))
            {
                var tage = ListeOderLeer(projekt, "tage");
                var letzte = tage.Count > 0 ? Objekt(tage[^1]) : [];
                var verzug = letzte["verzug"];
                fertigZeilen.Add(T(PyFormat.Prozent("• %s · %s · fertig an Arbeitstag %d%s", projekt["titel"], projekt["kunde_kurz"], projekt["fertig"],
                    Py.Wahr(verzug) ? PyFormat.Prozent(" · %d %s zu spät", verzug, Py.Int(verzug) == 1 ? "Tag" : "Tage") : " · pünktlich"), 12, "text_soft"));
            }
            yield return Karte("Abgeschlossene Projekte", fertigZeilen, "accent", PyFormat.Prozent("%d insgesamt", fertig.Count));
        }
    }

    /// <summary>_project_title</summary>
    private Control ProjektTitel(PyObjekt projekt) =>
        TitelMitStreifen(projekt["titel"], projekt["kunde_kurz"], projekt["cat"], Betrieb.GrossMarkeText(projekt, I));

    /// <summary>_project_details</summary>
    private IEnumerable<Control?> ProjektDetails(PyObjekt projekt)
    {
        var schluessel = Py.Str(Py.Wahr(projekt["projekt"]) ? projekt["projekt"] : projekt["id"]);
        if (!_detailsFuer.Contains(schluessel))
        {
            yield break;
        }
        var vorlage = Betrieb.ProjektEinzelheiten(projekt, I);
        var zeilen = new List<Control?>();
        foreach (var (kopf, feld) in new[] { ("Kunde", "kunde"), ("Ausgangssituation", "ausgangssituation"), ("Auftrag", "auftrag") })
        {
            zeilen.Add(Label(kopf));
            zeilen.Add(T(Py.Str(vorlage[feld]), 12, "text_soft"));
        }
        zeilen.Add(Label("Rahmenbedingungen"));
        zeilen.AddRange(Liste(vorlage["rahmenbedingungen"]).Select(z => (Control?)T("• " + Py.Str(z), 12, "text_soft")));
        if (Py.Wahr(vorlage["lernbar"]))
        {
            zeilen.Add(T(PyFormat.Prozent("Tipp: Unter „Testprojekt“ im Lernbereich kannst du dieses Projekt durcharbeiten. Dann arbeitet dein Team %d %% schneller.",
                Get(Betrieb.ProjektRegeln(I), "lernbonus", 0L)), 11, "muted"));
        }
        yield return Flaeche(Stapel(4, zeilen), Mix("card_alt", "accent", 0.08), Mix("card_alt", "accent", 0.08), ecken: 10,
            innen: new Thickness(10), randBreite: 0);
    }

    /// <summary>_details_button</summary>
    private Button DetailsKnopf(object? schluessel)
    {
        var id = Py.Str(schluessel);
        return Knopf(_detailsFuer.Contains(id) ? "Details ausblenden" : "Details", () =>
        {
            if (!_detailsFuer.Remove(id))
            {
                _detailsFuer.Add(id);
            }
            Neu();
        }, "ghost", 38);
    }

    /// <summary>_project_box</summary>
    private Control ProjektKasten(Spielstand stand, PyObjekt projekt, PyObjekt? stufen)
    {
        var pid = Py.Str(projekt["projekt"]);
        var anteil = Py.F(projekt["stand"]) / Py.F(projekt["aufwand"]) * 100;
        var team = Py.Wahr(projekt["team"]);
        var teile = new List<Control?>
        {
            ProjektTitel(projekt),
            Balken("Fortschritt", "green", "accent", anteil, PyFormat.Prozent("%d %%", Core.Mathe.PythonRunden.Runde(anteil))),
            T(Betrieb.ProjektPhasenText(projekt, I), 12, "text_soft"),
            T(Betrieb.ProjektStandText(stand, projekt, stufen, I), 12, stand.Tag > Py.Int(projekt["frist_tag"]) ? "red" : "text_dim"),
            T(Betrieb.ProjektTeamText(stand, projekt), 13, team ? "green" : "orange", true),
        };
        var bearbeiten = _teamFuer == pid;
        var teamKnopf = Knopf(bearbeiten ? "Team fertig" : "Team ändern", () => { _teamFuer = bearbeiten ? null : pid; Neu(); },
            bearbeiten ? "ghost" : "primary", 38);
        teile.Add(Reihe(teamKnopf, DetailsKnopf(pid)));
        if (bearbeiten)
        {
            teile.Add(Label("Wer arbeitet mit? (antippen)"));
            foreach (var option in Betrieb.ProjektKandidaten(stand, projekt, stufen, I))
            {
                var imTeam = Py.Wahr(option["im_team"]);
                var problem = Py.Wahr(option["problem"]);
                var an = option["an"];
                teile.Add(Breit(Knopf(PyFormat.Prozent("%s · %d · %s Punkte am Tag", option["name"], option["wert"], PyFormat.Num(option["punkte"])),
                    () => Mitglied(projekt["projekt"], an), imTeam ? "success" : "ghost", 38, imTeam || !problem)));
                if (problem)
                {
                    teile.Add(T(Py.Str(option["problem"]), 11, "muted"));
                }
            }
            teile.Add(T("Wer im Projekt ist, macht keine Kundentickets und keine Routineaufträge. Du selbst hast dann nur noch einen Ticketplatz.",
                11, "muted"));
        }
        teile.AddRange(ProjektDetails(projekt));
        return PersonKasten(teile);
    }

    /// <summary>_tender_box</summary>
    private Control AusschreibungKasten(Spielstand stand, PyObjekt projekt, string problem)
    {
        var ergebnis = projekt["ergebnis"] as PyObjekt;
        var mitErgebnis = ergebnis is not null && Py.Wahr(ergebnis);
        var teile = new List<Control?>
        {
            ProjektTitel(projekt),
            T(Py.Str(projekt["text"]), 13, "text_soft"),
            T(Betrieb.AusschreibungZeile(projekt, I), 12, "text_dim", true),
            T(Betrieb.AusschreibungStandText(stand, projekt, I), 12, !mitErgebnis ? "text_dim" : Py.Wahr(ergebnis!["gewonnen"]) ? "green" : "red"),
        };
        var id = Py.Str(projekt["id"]);
        if (mitErgebnis)
        {
            teile.AddRange(ErgebnisZeilen(ergebnis!));
            teile.Add(Reihe(DetailsKnopf(id)));
        }
        else if (_angebotFuer == id)
        {
            teile.AddRange(ProjektDetails(projekt));
            teile.AddRange(AngebotRechnen(projekt, projekt: true));
            return PersonKasten(teile);
        }
        else
        {
            var frei = problem.Length == 0;
            teile.Add(Reihe(Knopf("Angebot kalkulieren", () => Kalkulieren(id), hoehe: 38, frei: frei), DetailsKnopf(id)));
            if (!frei)
            {
                teile.Add(T(problem, 11, "muted"));
            }
        }
        teile.AddRange(ProjektDetails(projekt));
        return PersonKasten(teile);
    }

    /// <summary>_toggle_member</summary>
    private void Mitglied(object? projekt, object? person)
    {
        if (FirmaTun(a => a.ProjektMitglied(projekt, person)))
        {
            FirmaGeaendert();
        }
    }

    // -- Mitarbeiter und Bewerbungen ----------------------------------------------

    /// <summary>_person_card</summary>
    private List<Control?> PersonKarte(Spielstand stand, PyObjekt eintrag, bool bewerbung = false)
    {
        var rolle = Py.Str(eintrag["rolle"]);
        var bitweiche = Py.Str(eintrag["herkunft"]) == "bitweiche";
        if (bitweiche && !rolle.Contains("Bitweiche", StringComparison.Ordinal))
        {
            rolle += " · früher bei Bitweiche";
        }
        var zeilen = Stapel(2, [T(Py.Str(eintrag["name"]), 15, "text", true), T(rolle, 12, bitweiche ? "pink" : "accent", true)]);
        var id = Py.Str(eintrag["id"]);
        var teile = new List<Control?>
        {
            Zeile(zeilen, null, 12, new Avatar(eintrag["aussehen"] as PyObjekt, 56)),
            FertigkeitBalken(eintrag),
            T(Betrieb.StaerkenText(eintrag), 12, "text_dim"),
            T(Betrieb.GeldText(eintrag, I), 12, "text_dim"),
        };
        if (!bewerbung && Py.Hat(eintrag, "stimmung"))
        {
            teile.Add(T(Betrieb.StimmungText(eintrag, I), 12, StimmungFarbe[Betrieb.StimmungStufe(eintrag["stimmung"], I)]));
        }
        if (_themenFuer.Contains(id) || _faecherOffen.Any(p => p.Item1 == id))
        {
            var grenze = !bewerbung ? Betrieb.DeckelText(Objekt(eintrag["themen"]), I) : "";
            if (grenze.Length > 0)
            {
                teile.Add(T(grenze, 11, "yellow"));
            }
        }
        var notiz = !bewerbung ? Betrieb.WeiterbildungText(stand, eintrag) : PyFormat.Prozent("Bewerbung liegt vor bis Arbeitstag %d", eintrag["bis_tag"]);
        if (notiz.Length > 0)
        {
            teile.Add(T(notiz, 12, !bewerbung ? "yellow" : "muted"));
        }
        if (Py.Wahr(eintrag["macke_info"]))
        {
            teile.AddRange(MackeKasten(Objekt(eintrag["macke_info"])));
        }
        else if (Py.Wahr(eintrag["macke"]))
        {
            teile.Add(T(Py.Str(eintrag["macke"]), 11, "muted"));
        }
        return teile;
    }

    /// <summary>quirk_block</summary>
    private static IEnumerable<Control?> MackeKasten(PyObjekt info)
    {
        if (Betrieb.MackeZeilen(info) is not { } zeilen)
        {
            yield break;
        }
        var teile = new List<Control?>
        {
            Reihe(T(PyFormat.Prozent("Macke: %s", zeilen["titel"]), 12, "text", true), StufenKaestchen(Py.Int(info["stufe"])),
                T(Py.Str(zeilen["stufe"]), 12, "purple")),
            T(Py.Str(zeilen["zitat"]), 11, "muted"),
        };
        if (Py.Wahr(zeilen["plus"]))
        {
            teile.Add(T("+ " + Py.Str(zeilen["plus"]), 11, "green"));
        }
        if (Py.Wahr(zeilen["minus"]))
        {
            teile.Add(T("- " + Py.Str(zeilen["minus"]), 11, "yellow"));
        }
        if (Py.Wahr(zeilen["hinweis"]))
        {
            teile.Add(T(Py.Str(zeilen["hinweis"]), 11, "muted"));
        }
        yield return Stapel(2, teile);
    }

    /// <summary>stage_boxes: drei Kaestchen fuer die Auspraegung einer Macke.</summary>
    private static Control StufenKaestchen(long stufe, string farbe = "purple")
    {
        var reihe = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        for (var nummer = 1; nummer <= 3; nummer++)
        {
            reihe.Children.Add(new Border
            {
                Width = 16, Height = 9, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1),
                Background = Pinsel(nummer <= stufe ? farbe : "card"), BorderBrush = Pinsel(farbe),
            });
        }
        return reihe;
    }

    /// <summary>_skill_bar: Spur und Fuellung im Verhaeltnis des Anteils.</summary>
    private static Control FertigkeitStrich(double anteil, string farbe, double hoehe)
    {
        var teil = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, anteil)) * 1000, MidpointRounding.ToEven);
        var gitter = new Grid { Height = hoehe };
        gitter.ColumnDefinitions.Add(new ColumnDefinition(Math.Max(1, teil), GridUnitType.Star));
        gitter.ColumnDefinitions.Add(new ColumnDefinition(Math.Max(1, 1000 - teil), GridUnitType.Star));
        var fuellung = new Border { CornerRadius = new CornerRadius(hoehe / 2), Background = Pinsel(farbe), IsVisible = teil > 0 };
        if (teil >= 1000)
        {
            Grid.SetColumnSpan(fuellung, 2);
        }
        gitter.Children.Add(fuellung);
        return new Border { Child = gitter, CornerRadius = new CornerRadius(hoehe / 2), Background = Pinsel("ring_bg"), Height = hoehe };
    }

    /// <summary>_skill_bars: Balken je Fachbereich, Antippen klappt die Themen mit den genauen Werten auf.</summary>
    private Control FertigkeitBalken(PyObjekt eintrag)
    {
        var id = Py.Str(eintrag["id"]);
        var zeilen = new List<Control?>();
        foreach (var reihe in Betrieb.SkillBalken(eintrag, I))
        {
            var kat = Py.Str(reihe["cat"]);
            var farbe = Hex(FachFarbe(kat));
            var offen = _themenFuer.Contains(id) || _faecherOffen.Contains((id, kat));
            var pfeil = Symbole.Block("chevron_right", 16, Ton(farbe));
            if (offen)
            {
                // EXPAND_MORE: derselbe Pfeil um 90 Grad gedreht (das Zeichen fehlt in fisi_symbole.otf)
                pfeil.RenderTransform = new RotateTransform(90);
            }
            var name = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { pfeil, T(Py.Str(reihe["name"]), 12, farbe, true) } };
            zeilen.Add(Flaeche(BalkenZeile(name, 110, FertigkeitStrich(Py.F(reihe["anteil"]), farbe, 10), T(Py.Str(reihe["wert"]), 12, "text", true)),
                "card_alt", "card_alt", () => FachUmschalten(id, kat), 0, new Thickness(0, 3), 0));
            if (!offen)
            {
                continue;
            }
            foreach (var thema in Liste(reihe["themen"]).Select(Objekt))
            {
                var zeile = BalkenZeile(T(Py.Str(thema["name"]), 11, "text_dim"), 92,
                    FertigkeitStrich(Py.F(thema["anteil"]), Mix(farbe, "card_alt", 0.35), 6),
                    T(Py.Str(thema["wert"]), 11, Py.Wahr(thema["grenze"]) ? "yellow" : "text_soft"));
                zeile.Margin = new Thickness(18, 0, 0, 0);
                zeilen.Add(zeile);
            }
        }
        return Stapel(2, zeilen);
    }

    /// <summary>Name, Balken und Wert in einer Zeile; ab 160 % der Name ueber Balken und Wert.</summary>
    private static Control BalkenZeile(Control name, double breite, Control strich, TextBlock wert)
    {
        wert.TextAlignment = TextAlignment.Right;
        wert.MinWidth = Schrift.Gross ? 30 * Schrift.Faktor : 30;
        strich.VerticalAlignment = VerticalAlignment.Center;
        var balken = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        balken.Children.Add(strich);
        Grid.SetColumn(wert, 1);
        balken.Children.Add(wert);
        if (Schrift.Gross)
        {
            return Stapel(2, [name, balken]);
        }
        name.Width = breite;
        name.VerticalAlignment = VerticalAlignment.Center;
        var zeile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6 };
        zeile.Children.Add(name);
        Grid.SetColumn(balken, 1);
        zeile.Children.Add(balken);
        return zeile;
    }

    /// <summary>_toggle_cat</summary>
    private void FachUmschalten(string person, string kat)
    {
        if (_themenFuer.Remove(person))
        {
            foreach (var andere in SpielThemen.Jetzt.Fachbereiche.Where(k => k != kat))
            {
                _faecherOffen.Add((person, andere));
            }
        }
        else if (!_faecherOffen.Remove((person, kat)))
        {
            _faecherOffen.Add((person, kat));
        }
        Neu();
    }

    /// <summary>_toggle_topics</summary>
    private void ThemenUmschalten(string person)
    {
        if (!_themenFuer.Remove(person))
        {
            _themenFuer.Add(person);
        }
        else
        {
            _faecherOffen.RemoveWhere(p => p.Item1 == person);
        }
        Neu();
    }

    /// <summary>_firm_mitarbeiter</summary>
    private IEnumerable<Control?> FirmaMitarbeiter(Spielstand stand)
    {
        var personal = stand.PersonalListe();
        var teile = new List<Control?>();
        if (personal.Count == 0)
        {
            teile.Add(T("Noch arbeitest du allein. Unter „Bewerbungen“ findest du Leute für deine Firma.", 14, "text_soft"));
        }
        else
        {
            teile.Add(T("Heute: " + Betrieb.FirmenTagText(stand.FirmenTag(), I), 12, "text_dim"));
        }
        foreach (var entscheidung in stand.OffeneEntscheidungen())
        {
            teile.Add(EntscheidungKasten(entscheidung, (e, w) => { if (FirmaTun(a => a.Entscheiden(e, w))) { FirmaGeaendert(); } }));
        }
        var gebaeude = Py.Str(Objekt(Betrieb.FirmaRegeln(I)["gebaeude"])["name"]);
        foreach (var eintrag in personal)
        {
            var person = PersonKarte(stand, eintrag);
            var id = Py.Str(eintrag["id"]);
            if (stand.Zweigstelle is PyObjekt filiale && Py.Wahr(filiale))
            {
                var dort = Py.Str(eintrag["standort"]) == OrtFiliale;
                var ziel = dort ? OrtBuero : OrtFiliale;
                person.Add(T("Arbeitsplatz: " + (dort ? Py.Str(filiale["name"]) : gebaeude), 12, dort ? "purple" : "green"));
                person.Add(Breit(Knopf(dort ? "Zum Gewerbehof" : "In die Filiale", () => Versetzen(eintrag["id"], ziel), "ghost", 36, stand.OrtFrei(ziel) > 0)));
            }
            person.Add(Knopfreihe(
                Knopf("Weiterbilden", () => { _weiterbildungFuer = _weiterbildungFuer == id ? null : id; _weiterbildungFach = null; Neu(); }, "ghost", 36),
                Knopf("Themen", () => ThemenUmschalten(id), _themenFuer.Contains(id) ? "success" : "ghost", 36),
                Knopf("Entlassen", () => Entlassen(eintrag), "ghost", 36)));
            var coaching = Betrieb.CoachingAngebot(stand, eintrag["id"], I);
            if (Py.Wahr(coaching["macke"]))
            {
                var problem = Py.Wahr(coaching["problem"]);
                person.Add(Breit(Knopf(PyFormat.Prozent("Coaching (%s)", Euro(coaching["preis"])), () => Coachen(eintrag), "ghost", 36, !problem)));
                if (problem)
                {
                    person.Add(T(Py.Str(coaching["problem"]), 12, "muted"));
                }
            }
            if (_weiterbildungFuer == id)
            {
                person.AddRange(WeiterbildungWahl(stand, eintrag));
            }
            teile.Add(PersonKasten(person));
        }
        yield return Karte("Mitarbeiter", teile, "accent", PyFormat.Prozent("%d von %d Plätzen", personal.Count, stand.Kapazitaet));
    }

    /// <summary>_training_choice: erst den Fachbereich waehlen, dann ein Thema oder den ganzen Fachbereich.</summary>
    private IEnumerable<Control?> WeiterbildungWahl(Spielstand stand, PyObjekt eintrag)
    {
        var regeln = Objekt(Betrieb.FirmaRegeln(I)["weiterbildung"]);
        var kat = _weiterbildungFach;
        var werte = Objekt(eintrag["werte"]);
        if (kat is null)
        {
            yield return T("Weiterbildung in welchem Fachbereich?", 13, "text", true);
            foreach (var k in SpielThemen.Jetzt.Fachbereiche)
            {
                yield return Breit(Knopf(PyFormat.Prozent("%s %d", FachKurz(k), werte[k]), () => { _weiterbildungFach = k; Neu(); }, "ghost", 36));
            }
            yield break;
        }
        var ganz = Objekt(regeln["fachbereich"]);
        yield return T(PyFormat.Prozent("Weiterbildung %s: ein Thema oder der ganze Fachbereich?", FachKurz(kat)), 13, "text", true);
        var angebote = new List<PyObjekt> { Betrieb.WeiterbildungAngebot(stand, eintrag["id"], kat, I) };
        angebote.AddRange(SpielThemen.Jetzt.ThemenVon[kat].Select(t => Betrieb.WeiterbildungAngebot(stand, eintrag["id"], t, I)));
        var themen = Objekt(eintrag["themen"]);
        foreach (var angebot in angebote)
        {
            var thema = angebot["thema"];
            var text = Py.Wahr(thema)
                ? PyFormat.Prozent("%s %d (%s)", SpielThemen.Jetzt.Kurzname[Py.Str(thema)], Py.Int(themen[Py.Str(thema)]), Euro(angebot["preis"]))
                : PyFormat.Prozent("Ganzer Fachbereich (%s)", Euro(angebot["preis"]));
            var ziel = Py.Wahr(thema) ? Py.Str(thema) : kat;
            yield return Breit(Knopf(text, () => Weiterbilden(eintrag["id"], ziel), "ghost", 36, !Py.Wahr(angebot["problem"])));
        }
        yield return T(PyFormat.Prozent("Thema: +%d, dauert %d Arbeitstage. Ganzer Fachbereich: +%d auf alle Themen, dauert %d Arbeitstage. " +
            "Höchstens %d, in der Zeit kein Umsatz. Durch Arbeit allein geht es nur bis %d.",
            regeln["plus"], angebote.Count > 1 ? angebote[1]["tage"] : regeln["tage"], ganz["plus"], angebote[0]["tage"], regeln["max"],
            Betrieb.LernDeckel(I)), 11, "muted");
        if (angebote.All(a => Py.Wahr(a["problem"])))
        {
            yield return T(Py.Str(angebote[0]["problem"]), 12, "yellow");
        }
        yield return Breit(Knopf("Anderer Fachbereich", () => { _weiterbildungFach = null; Neu(); }, "ghost", 36));
    }

    /// <summary>_train</summary>
    private void Weiterbilden(object? person, string ziel)
    {
        if (FirmaTun(a => a.Weiterbilden(person, ziel)))
        {
            _weiterbildungFuer = _weiterbildungFach = null;
            FirmaGeaendert();
        }
    }

    /// <summary>_coach</summary>
    private void Coachen(PyObjekt eintrag)
    {
        var angebot = Betrieb.CoachingAngebot(Stand, eintrag["id"], I);
        if (Py.Wahr(angebot["problem"]))
        {
            Hinweisen(Py.Str(angebot["problem"]), "yellow");
            return;
        }
        Bestaetigen("Coaching", PyFormat.Prozent("Coaching für %s: %s, %d Arbeitstage ohne Umsatz. Danach wirkt die Schwäche der Macke „%s“ eine Stufe schwächer.",
            eintrag["name"], Euro(angebot["preis"]), angebot["tage"], Objekt(eintrag["macke_info"])["name"]), () =>
        {
            if (FirmaTun(a => a.Coachen(eintrag["id"])))
            {
                FirmaGeaendert();
            }
        });
    }

    /// <summary>_fire</summary>
    private void Entlassen(PyObjekt eintrag) =>
        Bestaetigen("Entlassen", SpielAktionen.EntlassenFrage(Stand, eintrag["id"], eintrag["name"]), () =>
        {
            if (FirmaTun(a => a.Entlassen(eintrag["id"])))
            {
                FirmaGeaendert();
            }
        });

    /// <summary>_transfer</summary>
    private void Versetzen(object? person, string ort)
    {
        if (FirmaTun(a => a.Versetzen(person, ort)))
        {
            FirmaGeaendert();
        }
    }

    /// <summary>_firm_bewerbungen</summary>
    private IEnumerable<Control?> FirmaBewerbungen(Spielstand stand)
    {
        var gefunden = Betrieb.Bewerbungen(stand, I);
        var frei = stand.Kapazitaet - stand.Personal.Count;
        var regeln = Objekt(Betrieb.FirmaRegeln(I)["bewerbung"]);
        var teile = new List<Control?>
        {
            T(PyFormat.Prozent("Freie Plätze: %d von %d. Gehalt und Umsatz richten sich nach den Werten je Fachbereich.", Math.Max(0, frei), stand.Kapazitaet),
                12, "text_dim"),
        };
        if (gefunden.Count == 0)
        {
            teile.Add(T("Gerade liegen keine Bewerbungen vor.", 14, "text_soft"));
        }
        foreach (var eintrag in gefunden)
        {
            var person = PersonKarte(stand, eintrag, bewerbung: true);
            var id = eintrag["id"];
            person.Add(Breit(Knopf("Einstellen", () => { if (FirmaTun(a => a.Einstellen(id))) { FirmaGeaendert(); } }, hoehe: 38, frei: frei > 0)));
            teile.Add(PersonKasten(person));
        }
        yield return Karte("Bewerbungen", teile, "pink", "alle " + PyFormat.Plural(regeln["abstand_tage"], "Tag", "Tage") + " neue");
    }
}
