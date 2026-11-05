using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Start-Test (Phase 7 W6): die Ansichten der Tafel gezielt zeigen (Layoutpruefung, Fotos). Nur
/// lesend, ausser <see cref="ProbeFirmaAnlegen"/>, das im leeren Datenordner des Start-Tests einen
/// Durchgang mit eigener Firma anlegt (Weg wie fingerabdruck.py szenario045, nur angehaengte Zeilen).
/// </summary>
public sealed partial class SpielTafel
{
    internal SpielSitzung Sitzung => _s;

    /// <summary>Spalte mit Kopf und Inhalt (Fotos der ganzen Seite).</summary>
    internal StackPanel ProbeSpalte => (StackPanel)_rolle.Content!;

    /// <summary>Start-Test: neue Abzeichen nur abholen, kein Hinweisfenster (es haelt die Layoutpruefung an).</summary>
    internal bool OhneFenster { get; set; }

    private PyObjekt? _probeMoment;

    /// <summary>Platzwahl wie nach dem Start (noch kein Platz gewaehlt).</summary>
    internal void ProbePlatzwahl()
    {
        _stapel.Clear();
        _entwurf.Clear();
        _waehlen = true;
        Zeichnen();
    }

    /// <summary>Phase 12 Welle 1: der Kleiderschrank (Name, Aussehen mit gesperrten Teilen, Kleidung); vorher „Figur bearbeiten“.</summary>
    internal void ProbeFigur()
    {
        _waehlen = false;
        _s.PlatzGewaehlt = true;
        KleiderschrankOeffnen();
    }

    /// <summary>Phase 12 Welle 1: neue Figur anlegen mit Nicos Hinweis „Einmalig“ (leerer Spielstand, nichts geschrieben).</summary>
    internal void ProbeAnlegen()
    {
        _waehlen = false;
        _entwurf.Clear();
        _stapel.Clear();
        var leer = new Spielstand([], I);
        Oeffnen(Krumen("PRAXIS", "SPIEL"), () => FigurBearbeiten(leer));
    }

    /// <summary>
    /// Phase 12 Welle 1: im Durchgang von <see cref="ProbeFirmaAnlegen"/> einen kleinen Kleiderschrank kaufen, aufstellen und
    /// so viel Kleidung eintragen, wie er Plaetze hat (Kaufhaus „voll“, Kleiderschrank mit Kleidung). Nur einmal; nur
    /// angehaengte Zeilen. false, wenn es keinen Durchgang, keine Geschaefte oder keinen freien Platz gibt.
    /// </summary>
    internal bool ProbeSchrankAnlegen()
    {
        var v = _s.Verwaltung;
        if (v.Ablauf?.Stand.Profil is null || _s.Geschaefte is not { } g)
        {
            return false;
        }
        if (Charakter.SchrankAufgestellt(v.Ablauf.Stand) && !Charakter.Plaetze(v.Ablauf.Stand).Frei)
        {
            return true;
        }
        var a = v.ZumSchreiben();
        if (!Charakter.SchrankAufgestellt(a.Stand))
        {
            var stueck = "probe-schrank";
            if (!a.Stand.Moebel.ContainsKey(stueck))
            {
                a.Ausfuehren(SpielAktionen.MoebelKaufen(a.Stand, "kleiderschrank_klein", stueck));
            }
            var gestellt = false;
            for (var y = 1.0; y < 12 && !gestellt; y += 0.5)
            {
                for (var x = 1.0; x < 16 && !gestellt; x += 0.5)
                {
                    try
                    {
                        a.Ausfuehren(SpielAktionen.AufstellenBei(a.Stand, stueck, x, y, 0L));
                        gestellt = true;
                    }
                    catch (SpielFehler)
                    {
                        // naechste Stelle
                    }
                }
            }
            if (!gestellt)
            {
                return false;
            }
        }
        foreach (var ware in Kleidung.Alle(g.Daten).Where(k => !a.Stand.Kleider.ContainsKey(k.Id) && k.Festzeit is null))
        {
            if (!Charakter.Plaetze(a.Stand).Frei)
            {
                break;
            }
            a.Log(Kleidung.Gekauft, new PyObjekt { { "stueck", ware.Id }, { "geld", checked(-ware.Preis) }, { "tag", a.Stand.Tag }, { "laden", Kleidung.Laden } });
        }
        _s.Abgleichen();
        return Charakter.SchrankAufgestellt(a.Stand) && !Charakter.Plaetze(a.Stand).Frei;
    }

    /// <summary>
    /// Phase 12 Welle 2: im Durchgang von <see cref="ProbeFirmaAnlegen"/> den „Schreibtisch mit PC“ und das Buecherregal
    /// kaufen und aufstellen und zwei Moebel online bestellen (Pflanze: 1 Arbeitstag, Sofa: 3), damit der Online-Shop die
    /// Karte „Unterwegs zu dir“ zeigt. Nur einmal; nur angehaengte Zeilen. false ohne Durchgang, Geschaefte oder Platz.
    /// </summary>
    internal bool ProbePcAnlegen()
    {
        var v = _s.Verwaltung;
        if (v.Ablauf?.Stand.Profil is null || _s.Geschaefte is not { } g)
        {
            return false;
        }
        var a = v.ZumSchreiben();
        foreach (var typ in new[] { Spielwelt.PcTyp, Spielwelt.RegalTyp })
        {
            if (Spielwelt.Aufgestellt(a.Stand, typ))
            {
                continue;
            }
            var stueck = "probe-" + typ;
            if (!a.Stand.Moebel.ContainsKey(stueck))
            {
                a.Ausfuehren(SpielAktionen.MoebelKaufen(a.Stand, typ, stueck));
            }
            var gestellt = false;
            for (var y = 1.0; y < 12 && !gestellt; y += 0.5)
            {
                for (var x = 1.0; x < 16 && !gestellt; x += 0.5)
                {
                    try
                    {
                        a.Ausfuehren(SpielAktionen.AufstellenBei(a.Stand, stueck, x, y, 0L));
                        gestellt = true;
                    }
                    catch (SpielFehler)
                    {
                        // naechste Stelle
                    }
                }
            }
            if (!gestellt)
            {
                return false;
            }
        }
        if (Spielwelt.Bestellungen(a.Stand).Count == 0)
        {
            a.Bestellen(g, "moebelhaus", "pflanze");
            a.Bestellen(g, "moebelhaus", "sofa");
        }
        _s.Abgleichen();
        return Spielwelt.Aufgestellt(a.Stand, Spielwelt.PcTyp) && Spielwelt.Bestellungen(a.Stand).Count > 0;
    }

    internal void ProbeUebersicht()
    {
        _waehlen = false;
        _entwurf.Clear();
        UebersichtOeffnen();
    }

    internal void ProbeReise(string reiter)
    {
        _waehlen = false;
        _entwurf.Clear();
        _stapel.Clear();
        ReiseOeffnen(reiter);
    }

    internal void ProbeFirma(string reiter)
    {
        _waehlen = false;
        _entwurf.Clear();
        _stapel.Clear();
        FirmaOeffnen(reiter);
    }

    /// <summary>W7: Sortiment eines Geschaefts (im Elektromarkt mit den Teilen des Typs); false ohne Geschaefte.</summary>
    internal bool ProbeGeschaeft(string laden, string? typ = null)
    {
        _waehlen = false;
        _entwurf.Clear();
        if (_s.Geschaefte is null)
        {
            return false;
        }
        GeschaeftOeffnen(laden, typ);
        return _laden == laden;
    }

    /// <summary>W7b: Seite am Schalter von Bank oder Amt (ohne Schreiben); false, wenn sie nicht offen ist.</summary>
    internal bool ProbeSchalter(string ort)
    {
        _waehlen = false;
        _entwurf.Clear();
        SchalterOeffnen(ort);
        return _stapel.Count == 1 && _schalter == ort;
    }

    /// <summary>W7: die Teile-Typen des Elektromarkts (Reihenfolge des Sortiments).</summary>
    internal List<string> ProbeTeileTypen() =>
        _s.Geschaefte is { } g ? [.. g.Waren("elektromarkt").Where(w => w.Art == Geschaefte.ArtTeil).Select(w => w.Typ).Distinct()] : [];

    /// <summary>Ein Ticket des Typs als Seite (ohne Einreichen); false, wenn es keins gibt.</summary>
    internal bool ProbeTicket(string typ)
    {
        _waehlen = false;
        _entwurf.Clear();
        _stapel.Clear();
        var auftrag = Liste(I["aufgaben"]).Select(Objekt).FirstOrDefault(a => Py.Str(a["typ"]) == typ);
        if (auftrag is null)
        {
            return false;
        }
        TicketOeffnen(Py.Str(auftrag["id"]));
        return _ticket is not null;
    }

    /// <summary>Meilenstein (moment_card) des ersten Abzeichens als Seite, wie im Hinweisfenster.</summary>
    internal bool ProbeMeilenstein()
    {
        _stapel.Clear();
        if (_probeMoment is not { } info)
        {
            return false;
        }
        Oeffnen(Krumen("SPIEL", "MEILENSTEIN"), () => [MomentKarte(info)]);
        return true;
    }

    /// <summary>
    /// Durchgang auf Platz 1 mit Figur und eigener Firma anlegen (nur, wenn es noch keinen gibt): alle
    /// Aufgaben erledigt, 114 Arbeitstage, Erspartes, Gruendung, ein Ausbau, drei Feierabende und zwei
    /// Bewerbungen eingestellt. Die Abzeichen bleiben stehen (fuer <see cref="ProbeMeilenstein"/>).
    /// </summary>
    internal void ProbeFirmaAnlegen()
    {
        var v = _s.Verwaltung;
        if (v.Ablauf?.Stand.Firma is PyObjekt f && Py.Wahr(f))
        {
            return;
        }
        if (v.Lauf is null)
        {
            v.NeuerLauf(1);
        }
        var a = v.ZumSchreiben();
        if (a.Stand.Profil is null)
        {
            a.Profil("Nico", new PyObjekt
            {
                { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" },
            });
        }
        var aufgaben = Liste(I["aufgaben"]).Select(Objekt).ToList();
        for (var i = 0; i < aufgaben.Count; i++)
        {
            var reputation = new PyObjekt();
            foreach (var achse in Ereignisse.Achsen)
            {
                reputation.Add(achse, 1L);
            }
            a.Log(Ereignisse.Erledigt, new PyObjekt
            {
                { "aufgabe", aufgaben[i]["id"] }, { "tag", (long)Math.Min(113, 1 + (i / 4)) }, { "richtig", true }, { "geld", 60L }, { "reputation", reputation },
            });
        }
        for (long tag = 1; tag < 115; tag++)
        {
            a.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", tag }, { "gehalt", tag > 60 ? 250L : 140L }, { "rang", "Senior" } });
        }
        a.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 114L }, { "richtig", false }, { "geld", 400000L } });
        a.Neuaufbau();
        a.Gruenden("Nico IT-Service");
        a.Ausbauen();
        for (var tag = 0; tag < 3; tag++)
        {
            foreach (var entscheidung in a.Stand.OffeneEntscheidungen())
            {
                a.Entscheiden(entscheidung["id"], Objekt(Liste(entscheidung["optionen"])[0])["id"]);
            }
            a.Feierabend();
        }
        foreach (var bewerbung in Betrieb.Bewerbungen(a.Stand, I).Take(2))
        {
            if (a.Stand.Personal.Count < a.Stand.Kapazitaet)
            {
                a.Einstellen(bewerbung["id"]);
            }
        }
        _probeMoment = a.FreischaltungenAbholen().FirstOrDefault(i => Py.Wahr(i["moment"]));
        _s.PlatzGewaehlt = true;
        _s.Abgleichen();
    }
}
