using Avalonia;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>"Meine Reise": Rueckblick und Erfolge wie GameScreen.open_journey (mobile/src/spiel.py).</summary>
public sealed partial class SpielTafel
{
    /// <summary>JOURNEY_TABS</summary>
    private static readonly (string, string)[] ReiseReiter = [("rueckblick", "Rückblick"), ("erfolge", "Erfolge")];

    private string _reiseGruppe = "alle";
    private int _reiseSeite;
    private string _reiseReiter = "rueckblick";
    private string _abzeichenGruppe = "alle";

    /// <summary>JOURNEY_COLOR</summary>
    private static string ReiseFarbe(object? gruppe) => Py.Str(gruppe) switch
    {
        Betrieb.ReiseStory => "purple",
        Betrieb.ReiseKarriere => "accent",
        Betrieb.ReiseFirma => "green",
        _ => "accent",
    };

    /// <summary>tier_color: Farbe einer Abzeichen-Stufe (offen "muted").</summary>
    private static string StufenFarbe(object? stufe) =>
        stufe is string s && SpielBilder.StufenFarben.TryGetValue(s, out var farbe) ? farbe : "muted";

    /// <summary>open_journey</summary>
    public void ReiseOeffnen(string? reiter = null)
    {
        if (!_s.PlatzGewaehlt || Stand.Profil is null)
        {
            // open_achievements: erst einen Platz waehlen
            Zeichnen();
            return;
        }
        Verwalten(() => _s.Verwaltung.Neuladen());
        Verwalten(() => _s.Verwaltung.Ablauf?.WissenPruefen());
        _reiseSeite = 0;
        if (reiter is not null)
        {
            _reiseReiter = reiter;
            _stapel.Clear();
        }
        Oeffnen(Krumen("SPIEL", "REISE"), () => Reise(Stand));
    }

    private IEnumerable<Control?> Reise(Spielstand stand)
    {
        yield return PlatzLeiste();
        yield return Pillen(ReiseReiter, _reiseReiter, r => { _reiseReiter = r; Neu(); });
        if (_reiseReiter == "erfolge")
        {
            foreach (var teil in Erfolge(stand))
            {
                yield return teil;
            }
            yield break;
        }
        var werte = Betrieb.ReiseStatistik(stand, I);
        var kacheln = new (string, string, string)[]
        {
            ("Diensttage", Py.Str(werte["diensttage"]), "Arbeitstage"),
            ("Tickets gelöst", Py.Str(werte["richtig"]), PyFormat.Prozent("von %d · %d %%", werte["tickets"], werte["quote"])),
            ("Zwischenfälle", Py.Str(werte["zwischenfaelle"]), PyFormat.Prozent("gemeistert von %d", werte["zwischenfaelle_gesamt"])),
            ("Kundenprojekte", Py.Str(werte["kundenprojekte"]), "abgeschlossen"),
            ("Angebote", Py.Str(werte["angebote_gewonnen"]), PyFormat.Prozent("gewonnen von %d", werte["angebote"])),
            ("Verdient", Euro(werte["verdient"]), "alle Einnahmen"),
        };
        // ab 160 % eine Kachel je Zeile
        var jeZeile = Schrift.Gross ? 1 : 2;
        var raster = new List<Control?>();
        for (var i = 0; i < kacheln.Length; i += jeZeile)
        {
            raster.Add(Nebeneinander(kacheln.Skip(i).Take(jeZeile).Select(k => Kachel(k.Item1, k.Item2, k.Item3, "border"))));
        }
        yield return Karte("Rückblick", raster, "accent2",
            PyFormat.Prozent("%s · Tag %d · %s", stand.Profil!["name"], stand.Tag, Spielstaende.SchwierigkeitAnzeige(stand)));

        var tickets = new LinienDiagramm(160, "green");
        tickets.Daten([.. Liste(werte["tage"]).Select(Py.Str)], "", [.. Liste(werte["tage_richtig"]).Select(Py.F)]);
        yield return Karte("Tickets je Arbeitstag", [T("Gelöste Tickets der letzten 30 Arbeitstage mit Tickets", 11, "muted"), tickets], "accent");
        var ansehen = new LinienDiagramm(150, "purple");
        ansehen.Daten([.. Liste(werte["ansehen_tage"]).Select(Py.Str)], "", [.. Liste(werte["ansehen"]).Select(Py.F)], 100);
        yield return Karte("Ansehen im Verlauf", [ansehen], "purple", "nach jedem Feierabend");

        var je = Objekt(werte["je_fachbereich"]);
        var spitze = je.Select(p => Py.Int(p.Value)).DefaultIfEmpty(0).Max();
        if (spitze == 0)
        {
            spitze = 1;
        }
        var balken = new List<Control?>();
        foreach (var kurz in SpielThemen.Jetzt.Fachbereiche)
        {
            var farbe = FachFarbe(kurz);
            var wert = Py.Int(je[kurz]);
            balken.Add(Balken(FachKurz(kurz), farbe, Hell(farbe, 0.35), 100.0 * wert / spitze, Py.Str(wert)));
        }
        balken.Add(T(PyFormat.Prozent("Mitarbeiter eingestellt: %d · Tickets verschoben: %d · Kundentickets deiner Leute: %d",
            werte["mitarbeiter"], werte["verschoben"], werte["kundentickets"]), 12, "text_dim"));
        yield return Karte("Gelöst je Fachbereich", balken, "green");

        var eintraege = Betrieb.ReiseFilter(Betrieb.Reise(stand, I), _reiseGruppe);
        eintraege.Reverse();   // neueste zuerst
        var tagebuch = new List<Control?>
        {
            Pillen(SpielAnsicht.ReiseGruppen.Select(g => (g.Id, g.Name)), _reiseGruppe, g => { _reiseGruppe = g; _reiseSeite = 0; Neu(); }),
        };
        const int Groesse = 12;
        var seiten = Math.Max(1, (eintraege.Count + Groesse - 1) / Groesse);
        _reiseSeite = Math.Max(0, Math.Min(_reiseSeite, seiten - 1));
        foreach (var eintrag in eintraege.Skip(_reiseSeite * Groesse).Take(Groesse))
        {
            var zeilen = new List<Control?>
            {
                T(PyFormat.Prozent("Tag %d", eintrag["tag"]), 11, "muted", true),
                T(Py.Str(eintrag["titel"]), 14, "text", true),
                Py.Wahr(eintrag["text"]) ? T(Py.Str(eintrag["text"]), 12, "text_dim") : null,
            };
            tagebuch.Add(Flaeche(Zeile(Stapel(2, zeilen), null, 12, Strich(ReiseFarbe(eintrag["gruppe"]))), innen: new Thickness(12, 8)));
        }
        if (eintraege.Count == 0)
        {
            tagebuch.Add(T("Noch keine Einträge - dein erster Arbeitstag wartet.", 14, "text_soft"));
        }
        if (seiten > 1)
        {
            var neuer = Knopf("Neuere", () => { _reiseSeite--; Neu(); }, "ghost", 36);
            var seite = T(PyFormat.Prozent("Seite %d / %d", _reiseSeite + 1, seiten), 13, "text_dim");
            var aeltere = Knopf("Ältere", () => { _reiseSeite++; Neu(); }, "ghost", 36);
            tagebuch.Add(Schrift.Gross ? Stapel(8, [neuer, seite, aeltere]) : Zeile(seite, aeltere, 10, neuer));
        }
        yield return Karte("Tagebuch", tagebuch, "purple", PyFormat.Prozent("%d Einträge · neueste zuerst", eintraege.Count));
    }

    /// <summary>Teile gleich breit nebeneinander (ft.Row mit expand=True je Teil).</summary>
    private static Grid Nebeneinander(IEnumerable<Control> teile, double abstand = 8)
    {
        var liste = teile.ToList();
        var raster = new Grid { ColumnSpacing = abstand };
        for (var i = 0; i < liste.Count; i++)
        {
            raster.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(liste[i], i);
            raster.Children.Add(liste[i]);
        }
        return raster;
    }

    /// <summary>_stat_tile</summary>
    private static Control Kachel(string titel, string wert, string unter, string rand, string wertFarbe = "text", double wertGroesse = 20) =>
        Flaeche(Stapel(1, [T(Beschriftungen.Gross(titel), 10, "muted", true), T(wert, wertGroesse, wertFarbe, true), T(unter, 11, "text_dim")]),
            rand: rand, innen: new Thickness(12, 8));

    /// <summary>ft.ProgressBar: schmaler Balken mit Anteil 0..1.</summary>
    private static Control Fortschrittsbalken(double anteil, string farbe, double hoehe = 6)
    {
        var teil = (int)Math.Round(Math.Max(0, Math.Min(1, anteil)) * 1000, MidpointRounding.ToEven);
        var spur = new Grid { Height = hoehe, ColumnDefinitions = new ColumnDefinitions($"{teil}*,{1000 - teil}*") };
        var hinten = new Border { CornerRadius = new CornerRadius(hoehe / 2) }.Farbe(Border.BackgroundProperty, "Pinsel.border");
        Grid.SetColumnSpan(hinten, 2);
        spur.Children.Add(hinten);
        if (teil > 0)
        {
            spur.Children.Add(new Border { CornerRadius = new CornerRadius(hoehe / 2), Background = Pinsel(farbe) });
        }
        return spur;
    }

    /// <summary>_achievements: Bestwerte und Abzeichen.</summary>
    private IEnumerable<Control?> Erfolge(Spielstand stand)
    {
        var zeilen = _s.Bestenliste();
        var tafel = Betrieb.BestenTafel(zeilen, stand, I);
        var laeufe = Py.Int(tafel["durchgaenge"]);
        var kacheln = new List<Control>();
        foreach (var eintrag in Liste(tafel["werte"]).Select(Objekt))
        {
            var rekord = Py.Wahr(eintrag["rekord"]) && laeufe > 1;
            kacheln.Add(Kachel(Py.Str(eintrag["name"]), Py.Str(eintrag["wert_text"]), Betrieb.BestenZeileText(eintrag, laeufe),
                rekord ? "yellow" : "border", eintrag["wert"] is not null ? "text" : "muted", 18));
        }
        var raster = new List<Control?> { T(Betrieb.BestenUntertitel(tafel), 12, "muted") };
        for (var i = 0; i < kacheln.Count; i += 2)
        {
            raster.Add(Nebeneinander(kacheln.Skip(i).Take(2)));
        }
        yield return Karte("Bestwerte", raster, "yellow");

        var eintraege = Betrieb.ErfolgUebersicht(stand, _s.Themenwissen(), zeilen, I);
        var (erreicht, alle, stufen) = Betrieb.ErfolgZaehler(eintraege);
        var abzeichen = new List<Control?>
        {
            T(PyFormat.Prozent("%d von %d Stufen · Bronze %d · Silber %d · Gold %d", erreicht, alle, stufen["bronze"], stufen["silber"], stufen["gold"]), 12, "muted"),
            Pillen(Betrieb.ErfolgGruppen(I), _abzeichenGruppe, g => { _abzeichenGruppe = g; Neu(); }),
        };
        foreach (var eintrag in eintraege)
        {
            if (_abzeichenGruppe == "alle" || _abzeichenGruppe == Py.Str(eintrag["gruppe"]))
            {
                abzeichen.Add(AbzeichenKachel(eintrag));
            }
        }
        yield return Karte("Abzeichen", abzeichen, "purple");
    }

    /// <summary>_badge_tile</summary>
    private static Control AbzeichenKachel(PyObjekt eintrag)
    {
        var stufe = eintrag["tier"] as string;
        var geheim = Py.Wahr(eintrag["geheim"]);
        var (status, farbe) = Betrieb.AbzeichenStatus(eintrag);
        var info = new List<Control?>
        {
            T(geheim ? "???" : Py.Str(eintrag["name"]), 14, stufe is not null ? "text" : "text_dim", true),
            T(status, 12, SpielBilder.StufenSchrift(FarbenAnwenden.Aktuell, farbe), true),
        };
        if (Py.Wahr(eintrag["naechste"]) && Py.Wahr(eintrag["balken"]) && !geheim)
        {
            info.Add(Fortschrittsbalken(Py.F(eintrag["anteil"]), StufenFarbe(Objekt(eintrag["naechste"])["tier"])));
        }
        var zeilen = new List<Control?>
        {
            Zeile(Stapel(3, info), null, 12, Formen.Abzeichen(eintrag["bild"] as string, eintrag["farbe"] as string, stufe, 56)),
            T(geheim ? "Noch nicht entdeckt." : Py.Str(eintrag["text"]), 12, "text_dim"),
        };
        if (Liste(eintrag["stufen"]).Count > 1)
        {
            zeilen.Add(T(Betrieb.AbzeichenZiele(eintrag), 11, "muted"));
        }
        return Flaeche(Stapel(6, zeilen), rand: stufe is not null ? StufenFarbe(stufe) : "border", innen: new Thickness(12, 10));
    }
}
