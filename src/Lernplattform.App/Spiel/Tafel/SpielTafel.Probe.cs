using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lernplattform.Core.Texte;
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

    /// <summary>Spalte mit dem Inhalt unter dem festen Kopf (Fotos der ganzen Seite).</summary>
    internal StackPanel ProbeSpalte => (StackPanel)_rolle.Content!;

    /// <summary>Knopf „Zurueck“ oben in der Kopfzeile (Start-Test, Welle A 0.81.0).</summary>
    internal Button ZurueckKnopf => _zurueck;

    /// <summary>Bildlauf der Tafel (Start-Test).</summary>
    internal ScrollViewer Rolle => _rolle;

    /// <summary>
    /// Gegenprobe (Welle A 0.81.0, Weisung P12-273): <paramref name="imBildlauf"/> = true legt die Kopfzeile wie bis A 0.80.0
    /// in den Bildlauf (rollt mit dem Inhalt weg), false wieder fest darueber.
    /// </summary>
    internal void KopfImBildlaufFuerTests(bool imBildlauf)
    {
        var drin = _spalte.Children.Contains(_kopf);
        if (imBildlauf == drin)
        {
            return;
        }
        if (imBildlauf)
        {
            _kopfFest.Children.Remove(_kopf);
            _spalte.Children.Insert(0, _kopf);
            _kopfFest.IsVisible = false;
        }
        else
        {
            _spalte.Children.Remove(_kopf);
            _kopfFest.Children.Add(_kopf);
            _kopfFest.IsVisible = true;
        }
    }

    /// <summary>
    /// Layoutpruefung und Start-Test (Welle A 0.81.0, Weisung P12-273, Nico 5a): steht „Zurueck“ fest oben, auch ganz nach
    /// unten gerollt? Prueft Beschriftung, nicht im Bildlauf, im Bereich der Tafel und im oberen Drittel. Rollt dafuer ganz
    /// nach unten und danach wieder zurueck. null = in Ordnung, sonst der Fund.
    /// </summary>
    internal string? ZurueckFund()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (_zurueck.Content is not TextBlock { Text: KartenTexte.Zurueck })
        {
            return "Knopf „Zurück“ fehlt";
        }
        if (_rolle.IsVisualAncestorOf(_zurueck))
        {
            return "„Zurück“ liegt im Bildlauf (rollt weg)";
        }
        var vorher = _rolle.Offset;
        try
        {
            _rolle.Offset = new Avalonia.Vector(0, Math.Max(0, _rolle.Extent.Height - _rolle.Viewport.Height));
            if (!_zurueck.IsEffectivelyVisible || _zurueck.Bounds.Width < 1 || _zurueck.TranslatePoint(default, this) is not { } p)
            {
                return "„Zurück“ nicht zu sehen";
            }
            var z = new Avalonia.Rect(p, _zurueck.Bounds.Size);
            if (z.X < -0.5 || z.Y < -0.5 || z.Right > Bounds.Width + 0.5 || z.Bottom > Bounds.Height + 0.5 || z.Y > Bounds.Height / 3)
            {
                return string.Create(inv, $"„Zurück“ nicht oben in der Tafel ({z.X:0.#},{z.Y:0.#} {z.Width:0.#}x{z.Height:0.#} von {Bounds.Width:0.#}x{Bounds.Height:0.#})");
            }
            return null;
        }
        finally
        {
            _rolle.Offset = vorher;
        }
    }

    /// <summary>
    /// Gegenprobe Sammelliste 381 (Weisung P12-298): true zeigt das Geld wie bis A 0.89.0 als erste Zeile im Bildlauf
    /// (rollt weg), false fest in der Kopfzeile.
    /// </summary>
    internal bool GeldImBildlauf { get; private set; }

    internal void GeldImBildlaufFuerTests(bool imBildlauf)
    {
        GeldImBildlauf = imBildlauf;
        Zeichnen();
    }

    /// <summary>Knopf „Zurueck“ und das Geld der Kopfzeile (Start-Test, Sammelliste 381).</summary>
    internal TextBlock GeldWert => _geldWert;

    /// <summary>Sammelliste 381: die offene Seite ist ein Laden oder der Online-Shop (Geld fest im Kopf).</summary>
    internal bool SeiteMitGeld => _stapel.Count > 0 && _stapel[^1].MitGeld;

    /// <summary>
    /// Layoutpruefung und Start-Test (Sammelliste 381, Weisung P12-298): auf einer Seite mit Einkauf steht das Geld fest oben
    /// in der Kopfzeile neben „Zurueck“, auch ganz nach unten gerollt: richtiger Betrag, nicht im Bildlauf, im Bereich der
    /// Tafel, auf der Hoehe von „Zurueck“ (senkrecht ueberlappend) und links davon, im oberen Drittel; keine zweite Zeile
    /// „Kontostand:“ im Bildlauf. Auf anderen Seiten nichts. Rollt ganz nach unten und danach zurueck. null = in Ordnung.
    /// </summary>
    internal string? GeldFund()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (!SeiteMitGeld)
        {
            return _geld.IsVisible ? "Geld im Kopf auf einer Seite ohne Einkauf" : null;
        }
        var soll = Bau.Euro(Stand.Geld);
        if (_rolle.IsVisualAncestorOf(_geldWert) || _rolle.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith("Kontostand:", StringComparison.Ordinal) == true))
        {
            return "Geld steht im Bildlauf (rollt weg)";
        }
        if (_geldWert.Text != soll)
        {
            return $"Geld im Kopf „{_geldWert.Text}“ statt „{soll}“";
        }
        var vorher = _rolle.Offset;
        try
        {
            _rolle.Offset = new Avalonia.Vector(0, Math.Max(0, _rolle.Extent.Height - _rolle.Viewport.Height));
            if (!_geldWert.IsEffectivelyVisible || _geldWert.Bounds.Width < 1 || _geldWert.TranslatePoint(default, this) is not { } p
                || _zurueck.TranslatePoint(default, this) is not { } z)
            {
                return "Geld nicht zu sehen";
            }
            var g = new Avalonia.Rect(p, _geldWert.Bounds.Size);
            var zr = new Avalonia.Rect(z, _zurueck.Bounds.Size);
            if (g.X < -0.5 || g.Y < -0.5 || g.Right > Bounds.Width + 0.5 || g.Bottom > Bounds.Height + 0.5 || g.Y > Bounds.Height / 3)
            {
                return string.Create(inv, $"Geld nicht oben in der Tafel ({g.X:0.#},{g.Y:0.#} {g.Width:0.#}x{g.Height:0.#} von {Bounds.Width:0.#}x{Bounds.Height:0.#})");
            }
            if (g.Right > zr.X + 0.5 || g.Bottom < zr.Y || g.Y > zr.Bottom)
            {
                return string.Create(inv, $"Geld nicht neben „Zurück“ (Geld {g.X:0.#},{g.Y:0.#} {g.Width:0.#}x{g.Height:0.#}, Zurück {zr.X:0.#},{zr.Y:0.#})");
            }
            return null;
        }
        finally
        {
            _rolle.Offset = vorher;
        }
    }

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

    /// <summary>Teile der gerade gezeigten Seite (Start-Test 350: Vergleich mit dem Reiter „Übersicht“ im Handy).</summary>
    internal IReadOnlyList<Control> InhaltTeile => [.. _inhalt.Children];

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
