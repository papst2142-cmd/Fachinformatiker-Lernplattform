using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Spiel;
using Lernplattform.Design;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>Uebersicht und Knoepfe der Orte wie GameScreen._overview, _ticket_card und run_action.</summary>
public sealed partial class SpielTafel
{
    /// <summary>PRIORITY_COLOR</summary>
    private static readonly IReadOnlyDictionary<string, string> PrioritaetFarbe = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["niedrig"] = "muted", ["normal"] = "cyan", ["hoch"] = "yellow", ["kritisch"] = "red",
    };

    /// <summary>STATUS_TEXT</summary>
    private static readonly IReadOnlyDictionary<string, (string Text, string Farbe)> StatusText = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        [Ereignisse.Offen] = ("offen", "cyan"), [Ereignisse.Richtig] = ("erledigt", "green"),
        [Ereignisse.Falsch] = ("mit Fehlern", "red"), [Ereignisse.StatusVerschoben] = ("verschoben", "yellow"),
        [Ereignisse.Wartet] = ("wartet auf Teil", "purple"),
    };

    /// <summary>AXIS_GRADIENT</summary>
    private static (string, string) AchsenVerlauf(string achse) => achse switch
    {
        "fachkompetenz" => ("cyan", "#6366F1"),
        "zuverlaessigkeit" => FarbenAnwenden.Aktuell.Verlaeufe["success"],
        "kundenzufriedenheit" => ("#F59E0B", "pink"),
        _ => ("#DB2777", "purple"),
    };

    /// <summary>cat_color(key): Farbe eines Fachbereichs (Kurzname).</summary>
    private static string FachFarbe(object? kurz) =>
        SpielThemen.Jetzt.Name.TryGetValue(Py.Str(kurz), out var name) && ListenTeil.Farben.TryGetValue(name, out var farbe) ? farbe : "Pinsel.accent";

    /// <summary>CATEGORY_SHORT[CAT_NAME[key]]</summary>
    private static string FachKurz(object? kurz) =>
        SpielThemen.Jetzt.Name.TryGetValue(Py.Str(kurz), out var name) ? SpielThemen.Jetzt.FachKurzname.GetValueOrDefault(name, name) : Py.Str(kurz);

    /// <summary>self.room: gewaehlter Raum im Buero (null = alle Tickets).</summary>
    private object? Raum { get; set; }

    /// <summary>Uebersicht oeffnen (in 3D eine eigene Seite: Figur, Tickets, Reputation, Wissen).</summary>
    public void UebersichtOeffnen()
    {
        if (Stand.Profil is null)
        {
            Zeichnen();
            return;
        }
        _stapel.Clear();
        Oeffnen(Krumen("PRAXIS", "SPIEL"), () => Uebersicht(Stand));
    }

    private IEnumerable<Control?> Uebersicht(Spielstand stand)
    {
        if (stand.Profil is null)
        {
            yield break;
        }
        var stufen = _s.Fachwissen();
        var ziel = Objekt(Objekt(I["balancing"])["gruendung"]);
        var firma = stand.Firma as PyObjekt;
        var gruendung = Balken("Weg zum eigenen Unternehmen", "green", "accent", stand.GruendungsFortschritt() * 100,
            "Ziel " + Euro(ziel["startkapital"]));
        gruendung.IsVisible = firma is null || !Py.Wahr(firma);
        var mitFirma = Py.Wahr(stand.Firma);
        var stelle = mitFirma ? PyFormat.Prozent("Geschäftsführung · %s", firma!["name"])
            : PyFormat.Prozent("%s · %s", stand.Rang, Objekt(I["gebaeude"])["firma"]);
        var hinweis = mitFirma ? "" : SpielTexte.RangHinweis(stand);
        var geld = mitFirma ? "Kontostand: " + Euro(stand.Geld)
            : PyFormat.Prozent("Kontostand: %s · Gehalt: %s/Tag", Euro(stand.Geld), Euro(stand.Gehalt));
        var knoepfe = new List<Control?> { Knopf("Figur bearbeiten", FigurAendern, "ghost", 38) };
        if (mitFirma || stand.GruendungBereit())
        {
            knoepfe.Add(Knopf(mitFirma ? "Firma öffnen" : "Firma gründen", () => FirmaOeffnen(), mitFirma ? "ghost" : "primary", 38));
        }
        // in 3D ist die Stadt die Karte; ihre Knoepfe ("Firma öffnen", "Meine Reise") stehen hier
        if (!mitFirma)
        {
            knoepfe.Add(Knopf("Firma öffnen", () => FirmaOeffnen(), "ghost", 38));
        }
        knoepfe.Add(Knopf("Meine Reise", () => ReiseOeffnen(), "ghost", 38));
        var zeilen = Stapel(3, [
            T(Py.Str(stand.Profil["name"]), 22, "text", true),
            T(stelle, 14, "accent", true),
            hinweis.Length > 0 ? T(hinweis, 12, "muted") : null,
            T(geld, 12, "text_dim"),
            mitFirma ? T(Betrieb.FirmenZeile(stand), 12, "text_dim") : null,
            stand.Miete != 0 ? T(PyFormat.Prozent("Miete: %s/Tag", Euro(stand.Miete)), 12, "text_dim") : null,
        ]);
        var figur = new Avatar(stand.Profil["aussehen"] as PyObjekt, 84);
        Control kopf = Schrift.Gross ? Stapel(8, [figur, zeilen]) : Zeile(zeilen, null, 14, figur);
        yield return PlatzLeiste();
        yield return Karte("Spielfigur", [kopf, gruendung, Reihe(knoepfe)], "accent2", PyFormat.Prozent("Arbeitstag %d", stand.Tag));
        yield return TicketKarte(stand);

        var balken = new List<Control?>();
        foreach (var (achse, name) in Betrieb.AchsenName)
        {
            var (a, b) = AchsenVerlauf(achse);
            var wert = stand.Reputation[achse];
            balken.Add(Balken(name, a, b, Py.F(wert), PyFormat.Prozent("%d / 100", wert)));
        }
        yield return Karte("Reputation", balken, "purple",
            PyFormat.Prozent("Ansehen %d %%", Core.Mathe.PythonRunden.Runde(stand.MittlereReputation)));

        var wissen = new List<Control?>();
        foreach (var kurz in SpielThemen.Jetzt.Fachbereiche)
        {
            var farbe = FachFarbe(kurz);
            var wert = Get(stufen, kurz, 0L);
            wissen.Add(Balken(FachKurz(kurz), farbe, Hell(farbe, 0.35), Py.F(wert), PyFormat.Prozent("%d %%", wert)));
        }
        wissen.Add(T("Aus deinem Lernfortschritt: Wer lernt, schafft schwierigere Tickets.", 11, "muted"));
        yield return Karte("Wissensstand", wissen, "green");
    }

    /// <summary>_ticket_card</summary>
    private Control TicketKarte(Spielstand stand)
    {
        List<(PyObjekt Auftrag, string Status)> tickets;
        var teile = new List<Control?>();
        string titel, akzent, unter;
        var mitFirma = Py.Wahr(stand.Firma);
        if (Raum is not null && SpielAnsicht.Raum(Raum, I) is { } raum)
        {
            tickets = SpielAnsicht.TicketsImRaum(stand, Raum);
            teile.Add(T(Py.Str(raum["text"]), 13, "text_dim"));
            var leute = SpielOrte.Personen(OrtBuero, stand);
            if (leute.Any(p => Py.Wahr(Get(p, "lagerist")) && Py.Gleich(p["raum"], raum["id"])))
            {
                teile.Add(Lagerhaus(stand));
            }
            foreach (var person in leute.Where(p => Py.Gleich(p["raum"], raum["id"])))
            {
                teile.Add(Stapel(2, [T(PyFormat.Prozent("%s · %s", person["name"], person["rolle"]), 14, "text", true),
                    T(Py.Str(person["macke"]), 12, "muted")]));
            }
            teile.Add(Reihe(Knopf("Alle Tickets zeigen", () => { Raum = null; Neu(); }, "ghost", 38)));
            (titel, akzent, unter) = (Py.Str(raum["name"]), FachFarbe(raum["cat"]), "Tickets hier");
        }
        else
        {
            tickets = stand.TicketsDesTages();
            if (Betrieb.MorgenText(stand.Tag, I, stand) is string szene && szene.Length > 0)
            {
                teile.Add(Hinweis([Label("Heute", "purple"), T(szene, 13, "text_soft")]));
            }
            if (mitFirma)
            {
                foreach (var entscheidung in stand.OffeneEntscheidungen())
                {
                    teile.Add(EntscheidungKasten(entscheidung, EntscheidenHier));
                }
            }
            (titel, akzent) = ("Tickets heute", "accent2");
            unter = mitFirma && tickets.Count == 0 ? "" : PyFormat.Prozent("%d von %d bearbeitet", stand.Bearbeitet.Count, tickets.Count);
        }

        if (tickets.Count == 0)
        {
            var wartend = stand.WartenAufLieferung().Count;
            var text = Raum is not null ? "In diesem Raum ist heute nichts zu tun."
                : mitFirma ? (Betrieb.AuftraegeZeile(stand) is { Length: > 0 } z ? z : Betrieb.FirmaRuhigText)
                : stand.AllesErledigt() ? Py.Str(Objekt(I["story"])["alle_erledigt"])
                : wartend > 0 ? PyFormat.Prozent("Heute stehen keine Tickets an. %s auf eine Lieferung.",
                    wartend == 1 ? "1 Auftrag wartet" : PyFormat.Prozent("%d Aufträge warten", wartend))
                : "Heute stehen keine Tickets an.";
            teile.Add(T(text, 14, "text_soft"));
            if (Raum is null && mitFirma && stand.FirmaOffen() != 0)
            {
                teile.Add(Knopf("Aufträge öffnen", () => FirmaOeffnen("auftraege")));
            }
            if (Raum is null && mitFirma && Betrieb.ProjekteZeile(stand) is { Length: > 0 } projekte)
            {
                teile.Add(T(projekte, 14, "text_soft"));
                teile.Add(Knopf("Projekte öffnen", () => FirmaOeffnen("projekte")));
            }
            if (Raum is null && stand.GruendungBereit())
            {
                teile.Add(T(Betrieb.GruendungLockText, 14, "green", true));
                teile.Add(Knopf("Firma gründen", () => FirmaOeffnen()));
            }
        }
        foreach (var (auftrag, status) in tickets)
        {
            teile.Add(TicketZeile(auftrag, status));
        }
        if (Raum is null)
        {
            teile.Add(Knopf("Feierabend machen", () => AktionAusfuehren("feierabend"), "accent", frei: stand.FeierabendMoeglich()));
        }
        return Karte(titel, teile, akzent, unter);
    }

    /// <summary>_warehouse: was unterwegs ist, was da ist und was ausgeliefert wurde.</summary>
    private Control Lagerhaus(Spielstand stand)
    {
        var lager = stand.Lagerhaus();
        string TeilName(object? t) => Py.Str(Teil(t, I)?["name"]);
        var abschnitte = new List<(string, List<string>)>
        {
            ("Unterwegs", [.. Liste(lager["unterwegs"]).Select(Objekt).Select(e => PyFormat.Prozent("%d × %s · %s · kommt an Arbeitstag %d",
                e["menge"], TeilName(e["teil"]), SpielAnsicht.Haendler(e["haendler"], I)?["name"], e["ankunft"]))]),
            ("Auf Lager", [.. Liste(lager["bestand"]).Select(b => (List<object?>)b!).Select(b => PyFormat.Prozent("%d × %s", b[1], TeilName(b[0])))]),
            ("Ausgeliefert", [.. Liste(lager["ausgeliefert"]).Select(Objekt).Select(e => PyFormat.Prozent("%d × %s an %s",
                e["menge"], TeilName(e["teil"]), Person(e["empfaenger"], I)?["name"]))]),
        };
        var zeilen = new List<Control?>();
        foreach (var (titel, liste) in abschnitte)
        {
            if (liste.Count > 0)
            {
                zeilen.Add(Label(titel));
                zeilen.AddRange(liste.Select(z => (Control?)T(z, 13, "text_soft")));
            }
        }
        if (zeilen.Count == 0)
        {
            zeilen.Add(T("Das Lager ist leer. Bestellte Ware taucht hier auf, sobald sie unterwegs ist.", 13, "muted"));
        }
        return Flaeche(Stapel(4, zeilen), innen: new Avalonia.Thickness(14, 10));
    }

    /// <summary>_ticket_row</summary>
    private Control TicketZeile(PyObjekt auftrag, string status)
    {
        var person = Person(auftrag["auftraggeber"], I);
        var (text, farbe) = StatusText[status];
        var wo = Py.Str(SpielAnsicht.Raum(auftrag["raum"], I)?["name"]);
        if (Py.Str(AuftragOrt(auftrag, I)) != OrtBuero)
        {
            wo = "beim Kunden · " + wo;
        }
        var zeilen = new List<Control?>
        {
            Py.Wahr(Get(auftrag, "zwischenfall")) ? Label("Zwischenfall", "red") : null,
            T(Py.Str(auftrag["titel"]), 14, "text", true),
            T(PyFormat.Prozent("%s · %s", person?["name"], wo), 12, "muted"),
        };
        var id = Py.Str(auftrag["id"]);
        return Flaeche(Zeile(Stapel(2, zeilen), T(text, 12, farbe, true), 12,
                Strich(PrioritaetFarbe.GetValueOrDefault(Py.Str(auftrag["prioritaet"]), "cyan"))),
            klick: status == Ereignisse.Offen ? () => TicketOeffnen(id) : null, innen: new Avalonia.Thickness(12, 10));
    }

    /// <summary>decision_box: Entscheidung zum Personal, je Moeglichkeit ein Knopf mit der Folge darunter.</summary>
    private static Control EntscheidungKasten(PyObjekt entscheidung, Action<object?, object?> waehlen)
    {
        var teile = new List<Control?>
        {
            Label("Entscheidung", "pink"),
            T(Py.Str(entscheidung["titel"]), 15, "text", true),
            T(Py.Str(entscheidung["text"]), 13, "text_soft"),
        };
        foreach (var option in Liste(entscheidung["optionen"]).Select(Objekt))
        {
            var problem = Py.Wahr(option["problem"]);
            var knopf = Knopf(Py.Str(option["label"]), () => waehlen(entscheidung["id"], option["id"]), "ghost", 38, !problem);
            knopf.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            teile.Add(Stapel(4, [knopf, T(Py.Str(problem ? option["problem"] : option["folge"]), 11, problem ? "yellow" : "text_dim")]));
        }
        return Flaeche(Stapel(6, teile), Mix("card", "pink", 0.08), Mix("pink", "card", 0.45), innen: new Avalonia.Thickness(12));
    }

    /// <summary>_decide_here</summary>
    private void EntscheidenHier(object? entscheidung, object? wahl)
    {
        if (Tun(a => a.Entscheiden(entscheidung, wahl)) is not null)
        {
            Fortschritt();
        }
    }

    // -- Knoepfe der Orte (run_action) --------------------------------------

    /// <summary>run_action: Knoepfe unter den Orten (Auftrag, Personal, Feierabend, Lernen, Schlafen).</summary>
    public void AktionAusfuehren(string aktion)
    {
        if (IstLadenAktion(aktion))
        {
            // W7: Sortiment und Kaufen im Moebelhaus und Elektromarkt
            LadenAktion(aktion);
            return;
        }
        if (aktion.StartsWith("auftrag:", StringComparison.Ordinal))
        {
            TicketOeffnen(aktion["auftrag:".Length..], vonOrt: true);
        }
        else if (aktion == SpielOrte.AktionPersonal)
        {
            FirmaOeffnen("mitarbeiter");
        }
        else if (aktion == "feierabend")
        {
            if (Tun(a => a.Feierabend()) is not { } daten)
            {
                return;
            }
            Raum = null;
            _stapel.Clear();
            var tag = Py.Int(daten["tag"]);
            _s.Abgleichen();
            _s.Spiel.NotizSetzen(PyFormat.Prozent("Feierabend nach Arbeitstag %d", tag),
                PyFormat.Prozent("%s %s", Betrieb.FeierabendSzene(tag, I, Py.Wahr(Get(daten, "firma"))), Wohnung.FeierabendGeldText(daten, I)));
            _s.Spiel.Neubeginn();
            Fortschritt();
        }
        else if (aktion == "lernen")
        {
            _wege?.Karten(null, null);
        }
        else if (aktion == "schlafen")
        {
            _s.Spiel.NotizSetzen("Gute Nacht", Wohnung.SchlafText(Stand));
            _s.Spiel.Auffrischen();
        }
    }
}
