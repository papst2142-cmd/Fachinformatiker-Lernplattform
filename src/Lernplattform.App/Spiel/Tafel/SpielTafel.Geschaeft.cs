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

    /// <summary>Sortiment des Geschaefts, in dem die Figur steht (typ: gleich die Teile dieses Typs).</summary>
    public void GeschaeftOeffnen(string? laden = null, string? typ = null)
    {
        _laden = laden ?? _s.Spiel.Ort;
        if (_laden is null || _s.Geschaefte is null)
        {
            return;
        }
        if (typ is not null)
        {
            _teileTyp = typ;
        }
        _stapel.Clear();
        Oeffnen(Krumen("SPIEL", _laden == "moebelhaus" ? "MÖBELHAUS" : "ELEKTROMARKT"), () => Geschaeft(Stand));
    }

    private IEnumerable<Control?> Geschaeft(Spielstand stand)
    {
        var g = _s.Geschaefte!;
        var laden = _laden!;
        var waren = g.Waren(laden);
        yield return T(PyFormat.Prozent("Kontostand: %s", Euro(stand.Geld)), 14, "text", true);
        var moebel = waren.Where(w => w.Art == Geschaefte.ArtMoebel)
            .Select(w => (Control?)WarenZeile(w, stand.Geld >= w.Preis, () => MoebelKaufen(w))).ToList();
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

    /// <summary>_item_row: Name und Preis links, Knopf rechts.</summary>
    private static Border WarenZeile(Ware ware, bool frei, Action kaufen)
    {
        var text = Stapel(2, [T(ware.Name, 14, "text", true), T(Euro(ware.Preis), 12, "text_dim")]);
        return Flaeche(Zeile(text, Knopf("Kaufen", kaufen, "ghost", 36, frei)), "card_alt", "border", innen: new Avalonia.Thickness(12, 8));
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
