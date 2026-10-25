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

    /// <summary>"Figur bearbeiten" (Aussehen, Name, Schwierigkeit).</summary>
    internal void ProbeFigur()
    {
        _waehlen = false;
        _s.PlatzGewaehlt = true;
        FigurAendern();
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
