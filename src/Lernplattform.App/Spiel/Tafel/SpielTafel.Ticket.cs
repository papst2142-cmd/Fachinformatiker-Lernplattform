using Avalonia;
using Avalonia.Controls;
using Lernplattform.Design;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>Ticket als eigene Seite wie GameScreen.open_ticket, _submit, _defer, _order_spare (mobile/src/spiel.py).</summary>
public sealed partial class SpielTafel
{
    /// <summary>EMPTY_HINT</summary>
    private static readonly IReadOnlyDictionary<string, string> LeerHinweis = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bauteile"] = "Bitte setze zuerst Bauteile ein.",
        ["bestellung"] = "Der Warenkorb ist noch leer.",
        ["rack"] = "Bitte baue zuerst Geräte in den Schrank ein.",
        ["formular"] = "Bitte fülle zuerst die Felder aus.",
        ["terminal"] = "Bitte führe zuerst alle Schritte im Terminal aus.",
        ["diagnose"] = "Bitte wähle Ursache und Maßnahme.",
        ["austausch"] = "Bitte wähle Ursache, Maßnahme, Ersatzteil und Ablauf.",
        ["wartung"] = "Bitte prüfe und bewerte alle Prüfpunkte und wähle den Abschluss.",
        ["bestueckung"] = "Bitte wähle für jeden Server alle Teile und das RAID-Level.",
    };

    /// <summary>Das offene Ticket (self.task, used_help, answered, options ...).</summary>
    private sealed class TicketSeite
    {
        public required PyObjekt Auftrag { get; init; }
        public required Brett Brett { get; init; }
        public required List<object?> Verfuegbar { get; init; }
        public required bool VonOrt { get; init; }
        public bool Hilfe { get; set; }
        public bool Beantwortet { get; set; }
        public StackPanel HilfeKasten { get; } = new() { Spacing = 10 };
        public StackPanel Ergebnis { get; } = new() { Spacing = 10 };
        public StackPanel Knoepfe { get; } = new() { Spacing = 10 };
        public Button? HilfeKnopf { get; set; }
        public List<Control> Teile { get; } = [];
    }

    private TicketSeite? _ticket;

    /// <summary>Ticket zum Bearbeiten oeffnen (aus der Uebersicht oder von einem Ort in der Stadt).</summary>
    public void TicketOeffnen(string kennung, bool vonOrt = false)
    {
        var stand = Stand;
        if (stand.Vorbereitet(Auftrag(kennung, I)) is not { } auftrag)
        {
            return;
        }
        var person = Person(auftrag["auftraggeber"], I);
        var luecken = Bewertung.Luecken(auftrag, _s.Themenwissen());
        var verfuegbar = stand.VerfuegbareTeile(auftrag);
        var typ = Py.Str(auftrag["typ"]);
        Brett brett = typ switch
        {
            "auswahl" => new AuswahlBrett(auftrag, I),
            "bauteile" => new BauteileBrett(auftrag, I, verfuegbar, stand.Lager()),
            "bestellung" => new BestellungBrett(auftrag, I),
            "rack" => new RackBrett(auftrag, I),
            "formular" => new FormularBrett(auftrag, I),
            "terminal" => new TerminalBrett(auftrag, I),
            "diagnose" => new DiagnoseBrett(auftrag, I, verfuegbar, stand.Lager(), teil => _ = NachbestellenAsync(teil)),
            "wartung" => new WartungBrett(auftrag, I),
            _ => new ZuordnungBrett(auftrag, I),
        };
        var seite = new TicketSeite { Auftrag = auftrag, Brett = brett, Verfuegbar = verfuegbar, VonOrt = vonOrt };
        _ticket = seite;
        var prioritaet = Py.Str(auftrag["prioritaet"]);
        var farbe = PrioritaetFarbe.GetValueOrDefault(prioritaet, "cyan");
        seite.Teile.Add(T(Py.Str(auftrag["titel"]), 19, "text", true));
        seite.Teile.Add(T(PyFormat.Prozent("%sPriorität %s · %s", Py.Wahr(Get(auftrag, "zwischenfall")) ? "Zwischenfall · " : "", prioritaet,
            FachKurz(auftrag["cat"])), 12, farbe));
        seite.Teile.Add(Karte("Ticket", [
            T(PyFormat.Prozent("%s · %s", person?["name"], person?["rolle"]), 14, "text", true),
            T(Py.Str(auftrag["ticket"]), 14, "text_soft"),
        ], farbe));
        if (luecken.Count > 0 && _s.Lernen is { } lernen)
        {
            var thema = luecken[0].Thema;
            var karten = Knopf(PyFormat.Prozent("Karteikarten zu %s", SpielThemen.Jetzt.Kurzname.GetValueOrDefault(thema, thema)),
                () => _wege?.Karten(SpielThemen.Jetzt.FachbereichVon.GetValueOrDefault(thema), thema), "ghost", 38);
            seite.Teile.Add(Flaeche(Stapel(8, [T(SpielTexte.LueckenWarnung(luecken, lernen), 13, "yellow"), Reihe(karten)]),
                Mix("card", "yellow", 0.12), Mix("yellow", "card", 0.35), ecken: 14, innen: new Thickness(14)));
        }
        seite.Teile.Add(Karte("Aufgabe", [T(Py.Str(auftrag["frage"]), 16, "text", true), brett, seite.HilfeKasten, seite.Ergebnis], "accent"));
        var einreichen = Knopf("Lösung einreichen", Einreichen);
        seite.HilfeKnopf = Knopf("Hilfe", HilfeZeigen, "ghost");
        var verschieben = Knopf("Verschieben", () => _ = VerschiebenAsync(), "ghost");
        seite.Knoepfe.Children.Add(Schrift.Gross ? Stapel(10, [einreichen, seite.HilfeKnopf]) : Zeile(einreichen, seite.HilfeKnopf));
        seite.Knoepfe.Children.Add(verschieben);
        seite.Knoepfe.Children.Add(T(PyFormat.Prozent("Ohne Hilfe gibt es %d %% Bonus.",
            Core.Mathe.PythonRunden.Runde(Py.F(Objekt(I["balancing"])["bonus_ohne_hilfe"]) * 100)), 12, "muted"));
        seite.Teile.Add(seite.Knoepfe);
        Oeffnen(Krumen("PRAXIS", "SPIEL"), () => seite.Teile);
    }

    /// <summary>_show_help</summary>
    private void HilfeZeigen()
    {
        if (_ticket is not { } t || t.Hilfe || t.Beantwortet)
        {
            return;
        }
        t.Hilfe = true;
        t.HilfeKnopf!.IsEnabled = false;
        t.HilfeKnopf.Opacity = 0.4;
        t.HilfeKasten.Children.Clear();
        t.HilfeKasten.Children.Add(Flaeche(Stapel(6, [Label("Hilfe", "accent"), T(Py.Str(t.Auftrag["hilfe"]), 14, "text_soft")]),
            Mix("card", "accent", 0.08), Mix("accent", "card", 0.5), innen: new Thickness(14)));
    }

    /// <summary>_submit</summary>
    private void Einreichen()
    {
        if (_ticket is not { } t || t.Beantwortet)
        {
            return;
        }
        var typ = Py.Str(t.Auftrag["typ"]);
        if (!t.Brett.Fertig)
        {
            Hinweisen(typ == "auswahl" ? "Bitte wähle eine Antwort aus."
                : SpielAnsicht.ProblemTypen.Contains(typ) ? LeerHinweis[t.Brett.HinweisSchluessel]
                : "Bitte ordne zuerst alle Begriffe zu.", "yellow");
            return;
        }
        var antwort = t.Brett.Antwort;
        if (Tun(a => a.Loesen(t.Auftrag["id"], antwort, t.Hilfe)) is not { } daten)
        {
            return;
        }
        t.Beantwortet = true;
        var richtig = Py.Wahr(daten["richtig"]);
        t.Brett.Aufdecken(richtig);
        Programm.AbgleichDienst.NachDemLernen();
        var farbe = richtig ? "green" : "red";
        t.Ergebnis.Children.Clear();
        t.Ergebnis.Children.Add(Flaeche(Stapel(8, [
            T(SpielAnsicht.Ergebnis(t.Auftrag, daten, t.Verfuegbar, I), 14, farbe, true),
            T(Py.Str(t.Auftrag["erklaerung"]), 14, "text_soft"),
        ]), Mix("card", farbe, 0.1), Mix(farbe, "card", 0.4), innen: new Thickness(14)));
        if (_s.Suche is { } suche)
        {
            var links = SpielAnsicht.LernLinks(t.Auftrag, suche, 5);
            if (links.Count > 0)
            {
                t.Ergebnis.Children.Add(Label("Passend dazu lernen", "purple"));
                foreach (var treffer in links)
                {
                    t.Ergebnis.Children.Add(Flaeche(T(PyFormat.Prozent("%s · %s", treffer.Art, treffer.Titel), 13, "text_soft"), rand: "card_alt",
                        klick: () => _wege?.Treffer(treffer), ecken: 10, innen: new Thickness(12, 10), randBreite: 0));
                }
            }
        }
        t.Knoepfe.Children.Clear();
        // RETURN_LABEL: zurueck in die Ansicht, aus der das Ticket kam
        var ort = t.VonOrt ? _s.Spiel.Ort : null;
        var zurueck = ort == OrtBuero ? "Zurück ins Büro"
            : ort is not null && Kundenorte(I).Any(k => Py.Str(k["id"]) == ort) ? "Zurück zum Kunden" : "Zurück zur Übersicht";
        t.Knoepfe.Children.Add(Knopf(zurueck, TicketSchliessen));
        _s.Abgleichen();
        Geaendert?.Invoke();
        if (_s.Verwaltung.Ablauf is { Freigeschaltet.Count: > 0 })
        {
            Avalonia.Threading.DispatcherTimer.RunOnce(Freischaltungen, TimeSpan.FromMilliseconds(400));
        }
    }

    /// <summary>_order_spare: fehlendes Ersatzteil nachbestellen, das Ticket wartet.</summary>
    private async Task NachbestellenAsync(object? teil)
    {
        if (_ticket is not { } t || t.Beantwortet)
        {
            return;
        }
        var eintrag = Teil(teil, I)!;
        var tage = SpielAnsicht.ErsatzLieferTage(I);
        var frage = PyFormat.Prozent("%s für %s nachbestellen? Es kommt %s, bis dahin wartet das Ticket.", eintrag["name"], Euro(eintrag["preis"]),
            tage == 1 ? "am nächsten Arbeitstag" : PyFormat.Prozent("in %d Arbeitstagen", tage));
        if (await Dialog.Zeigen("Ersatzteil nachbestellen", frage, "Abbrechen", "Ja") != 1)
        {
            return;
        }
        Tun(a => a.Ersatzteil(t.Auftrag["id"], teil));
        Programm.AbgleichDienst.NachDemLernen();
        TicketSchliessen();
    }

    /// <summary>_defer</summary>
    private async Task VerschiebenAsync()
    {
        if (_ticket is not { } t || t.Beantwortet)
        {
            return;
        }
        if (await Dialog.Zeigen("Ticket verschieben", "Das Ticket auf einen späteren Arbeitstag verschieben? Das kostet etwas Zuverlässigkeit.",
            "Abbrechen", "Ja") != 1)
        {
            return;
        }
        Tun(a => a.Verschieben(t.Auftrag["id"]));
        Programm.AbgleichDienst.NachDemLernen();
        TicketSchliessen();
    }

    /// <summary>_close: zurueck zum Ort (Stadt) oder zur Uebersicht.</summary>
    private void TicketSchliessen()
    {
        var vonOrt = _ticket?.VonOrt ?? false;
        _ticket = null;
        if (_stapel.Count > 0)
        {
            _stapel.RemoveAt(_stapel.Count - 1);
        }
        if (vonOrt)
        {
            _stapel.Clear();
        }
        Gezeigt();
    }
}
