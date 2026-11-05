using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Phase 7, W7 (Weisung P7-12): Sortiment von Moebelhaus und Elektromarkt. Aufbau wie die Karte
/// "Möbelhaus" der Handy-App (_home_editor: Zeilen mit Name, Preis und "Kaufen", nur mit genug Geld);
/// der Elektromarkt hat dazu die Teile fuer die Firma (nach Typ, Ladenpreis, sofort im Lager).
/// Gekauft wird nur im geoeffneten Geschaeft, in dem die Figur steht.
/// </summary>
public sealed partial class SpielTafel
{
    private string? _laden;
    private string _teileTyp = "";
    private string _kleiderSlot = "";

    /// <summary>Sortiment des Geschaefts, in dem die Figur steht (typ: gleich die Teile dieses Typs).</summary>
    public void GeschaeftOeffnen(string? laden = null, string? typ = null)
    {
        _laden = laden ?? _s.Spiel.Ort;
        if (_laden is null || _s.Geschaefte is null)
        {
            return;
        }
        // Phase 8 W6: das Sortiment folgt der Festzeit, mit der der Innenraum gebaut wurde
        _s.Geschaefte.Festzeit = _s.Spiel.Innen?.Fest ?? _s.Spiel.Szene.Fest;
        if (typ is not null)
        {
            _teileTyp = typ;
            _kleiderSlot = typ;
        }
        _stapel.Clear();
        Oeffnen(Krumen("SPIEL", _laden switch { "moebelhaus" => "MÖBELHAUS", Kleidung.Laden => "KAUFHAUS", _ => "ELEKTROMARKT" }), () => Geschaeft(Stand));
    }

    private IEnumerable<Control?> Geschaeft(Spielstand stand)
    {
        var g = _s.Geschaefte!;
        var laden = _laden!;
        var waren = g.Waren(laden);
        yield return T(PyFormat.Prozent("Kontostand: %s", Euro(stand.Geld)), 14, "text", true);
        if (laden == Kleidung.Laden)
        {
            yield return KleidungKarte(stand, g);
            yield break;
        }
        var moebel = waren.Where(w => w.Art == Geschaefte.ArtMoebel)
            .Select(w => (Control?)WarenZeile(w, stand.Geld >= w.Preis, () => MoebelKaufen(w), zusatz: SchrankZusatz(w))).ToList();
        if (moebel.Count > 0)
        {
            yield return Karte(laden == "moebelhaus" ? "Möbelhaus" : "Für die Wohnung", moebel, "purple", "Einmal bezahlen, für immer behalten");
        }
        var teile = waren.Where(w => w.Art == Geschaefte.ArtTeil).ToList();
        if (teile.Count == 0)
        {
            yield break;
        }
        var typen = teile.Select(w => w.Typ).Distinct().ToList();
        if (!typen.Contains(_teileTyp))
        {
            _teileTyp = typen[0];
        }
        var firma = Py.Wahr(stand.Firma);
        var zeilen = new List<Control?>
        {
            Pillen(typen.Select(t => (t, Geschaefte.TypName(t, I))), _teileTyp, t => { _teileTyp = t; Neu(); }),
        };
        if (!firma)
        {
            zeilen.Add(T(Geschaefte.NurMitFirma, 13, "text_dim"));
        }
        zeilen.AddRange(teile.Where(w => w.Typ == _teileTyp)
            .Select(w => (Control?)WarenZeile(w, firma && stand.Geld >= w.Preis, () => TeilKaufen(w))));
        yield return Karte("Für die Firma", zeilen, "cyan", "Sofort im Lager, etwas teurer als beim Versand");
    }

    /// <summary>_item_row: Name und Preis links, Knopf rechts; Phase 12 Welle 1: darunter ein Zusatz (Plaetze eines Schranks).</summary>
    private static Border WarenZeile(Ware ware, bool frei, Action kaufen, string knopf = "Kaufen", string? zusatz = null)
    {
        var text = Stapel(2, [T(ware.Name, 14, "text", true), T(Euro(ware.Preis), 12, "text_dim"), zusatz is null ? null : T(zusatz, 12, "accent")]);
        return Flaeche(Zeile(text, Knopf(knopf, kaufen, "ghost", 36, frei)), "card_alt", "border", innen: new Avalonia.Thickness(12, 8));
    }

    /// <summary>Phase 12 Welle 1: Plaetze fuer Kleidung bei einem Kleiderschrank (sonst null).</summary>
    private string? SchrankZusatz(Ware ware) =>
        ware.Typ == Charakter.SchrankTyp && Wohnung.MoebelStueck(ware.Id, I) is { } m ? Charakter.PlaetzeText(Charakter.Plaetze(m)) : null;

    /// <summary>
    /// Phase 8 W5 (Weisung P8-8c): Kleidung im Kaufhaus je Abteilung. Noch nicht gekauft: "Kaufen" (zieht gleich an).
    /// Phase 12 Welle 1 (Nico 2a): gekauft steht „Im Schrank“ (an- und ausziehen nur am Kleiderschrank); ohne Kleiderschrank
    /// oder bei vollem Schrank ein Hinweis und „Kaufen“ ist aus.
    /// </summary>
    private Control KleidungKarte(Spielstand stand, Geschaefte g)
    {
        var stellen = Kleidung.Stellen(g.Daten);
        if (!stellen.Any(s => s.Id == _kleiderSlot))
        {
            _kleiderSlot = stellen.Count > 0 ? stellen[0].Id : "";
        }
        var zeilen = new List<Control?>
        {
            Pillen(stellen.Select(s => (s.Id, s.Abteilung)), _kleiderSlot, s => { _kleiderSlot = s; Neu(); }),
        };
        var grund = Charakter.KaufGrund(stand);
        if (grund.Length > 0)
        {
            zeilen.Add(Flaeche(T(grund, 13, "text"), "card_alt", "orange", innen: new Avalonia.Thickness(12, 8)));
        }
        foreach (var ware in g.Waren(Kleidung.Laden).Where(w => w.Art == Kleidung.Art && w.Typ == _kleiderSlot))
        {
            zeilen.Add(stand.Kleider.ContainsKey(ware.Id)
                ? WarenZeile(ware, false, () => { }, Charakter.ImSchrank)
                : WarenZeile(ware, grund.Length == 0 && stand.Geld >= ware.Preis, () => KleidungKaufen(ware)));
        }
        return Karte(Kleidung.Ueberschrift, zeilen, "pink", Kleidung.FuerDieFigur);
    }

    private void KleidungKaufen(Ware ware)
    {
        if (Tun(a => a.KleidungKaufenMitPlatz(_s.Geschaefte!, _s.Stadt, _laden!, ware.Id)) is null)
        {
            return;
        }
        Hinweisen(PyFormat.Prozent("Gekauft: %s. Deine Figur trägt es jetzt.", ware.Name), "green");
        Gekauft();
    }

    /// <summary>Kleidung anziehen oder ablegen (stueck null), seit Phase 12 Welle 1 nur am Kleiderschrank.</summary>
    private void KleidungAnziehen(string slot, string? stueck)
    {
        if (_s.Geschaefte is not { } g || Tun(a => a.KleidungAnziehen(g, slot, stueck)) is null)
        {
            return;
        }
        Gekauft();
    }

    private void MoebelKaufen(Ware ware)
    {
        if (Tun(a => new PyObjekt { { "stueck", a.MoebelKaufen(_s.Geschaefte!, _s.Stadt, _laden!, ware.Id) } }) is null)
        {
            return;
        }
        Hinweisen(PyFormat.Prozent("Gekauft: %s. Es wartet zu Hause im Karton.", ware.Name), "green");
        Gekauft();
    }

    private void TeilKaufen(Ware ware)
    {
        if (Tun(a => a.TeilKaufen(_s.Geschaefte!, _s.Stadt, _laden!, ware.Id)) is null)
        {
            return;
        }
        Hinweisen(PyFormat.Prozent("Gekauft: %s. Es liegt jetzt im Lager.", ware.Name), "green");
        Gekauft();
    }

    private void Gekauft()
    {
        _s.Abgleichen();
        _s.Spiel.Auffrischen();
        Neu();
        Geaendert?.Invoke();
    }

    /// <summary>"Kaufen" an der Auslage im Laden (Knopf der Stadt): Moebelstueck sofort kaufen.</summary>
    private void AuslageKaufen(string moebel)
    {
        _laden = _s.Spiel.Ort;
        if (_laden is not null && _s.Geschaefte?.Finden(_laden, Geschaefte.ArtMoebel, moebel) is { } ware)
        {
            MoebelKaufen(ware);
        }
    }

    private static bool IstLadenAktion(string aktion) =>
        aktion == Innenraum.AktionSortiment || aktion.StartsWith(Innenraum.AktionSortiment + ":", StringComparison.Ordinal)
        || aktion.StartsWith(Innenraum.AktionKaufen, StringComparison.Ordinal);

    private void LadenAktion(string aktion)
    {
        if (aktion.StartsWith(Innenraum.AktionKaufen, StringComparison.Ordinal))
        {
            AuslageKaufen(aktion[Innenraum.AktionKaufen.Length..]);
            return;
        }
        GeschaeftOeffnen(null, aktion.Length > Innenraum.AktionSortiment.Length ? aktion[(Innenraum.AktionSortiment.Length + 1)..] : null);
    }
}
