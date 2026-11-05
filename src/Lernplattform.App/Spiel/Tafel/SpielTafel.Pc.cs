using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Views;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>
/// Phase 12 Welle 2 (Plan Phase 12 Abschnitte 2 und 3, Weisungen P12-3/P12-4/P12-5): der PC zu Hause am „Schreibtisch mit PC“
/// mit drei Reitern. Online-Shop: alle Moebel aus Moebelhaus und Elektromarkt zum gleichen Preis, Lieferzeit nach Groesse
/// (<see cref="Spielwelt.Liefertage"/>), das Paket steht dann vor der Haustuer. Kredite: nur ansehen (aufnehmen und abloesen
/// am Schalter der Bank). Gezahltes: Kontostand, Einnahmen und Ausgaben, mit Firma die Umsatzsteuer.
/// </summary>
public sealed partial class SpielTafel
{
    public const string PcShop = "shop";
    public const string PcKredite = "kredite";
    public const string PcGezahlt = "gezahlt";

    private string _pcReiter = PcShop;

    /// <summary>PC zu Hause oeffnen (Knopf „PC benutzen“ am Schreibtisch; ohne PC der Hinweis).</summary>
    public void PcOeffnen(string? reiter = null)
    {
        if (!Spielwelt.Aufgestellt(Stand, Spielwelt.PcTyp))
        {
            _ = Dialog.Zeigen(Spielwelt.PcTitel, Spielwelt.OhneComputer, Dialog.Ok);
            return;
        }
        if (reiter is not null)
        {
            _pcReiter = reiter;
        }
        // Kredite aufnehmen und abloesen gehen hier nicht (nur am Schalter der Bank)
        _schalter = null;
        _stapel.Clear();
        Oeffnen(Krumen("SPIEL", "PC ZU HAUSE"), () => PcSeite(Stand));
    }

    private IEnumerable<Control?> PcSeite(Spielstand stand)
    {
        yield return T(PyFormat.Prozent("Kontostand: %s", Euro(stand.Geld)), 14, "text", true);
        yield return Pillen([(PcShop, Spielwelt.ReiterShop), (PcKredite, Spielwelt.ReiterKredite), (PcGezahlt, Spielwelt.ReiterGezahlt)],
            _pcReiter, r => { _pcReiter = r; Neu(); });
        var seite = _pcReiter switch
        {
            PcKredite => PcKrediteSeite(stand),
            PcGezahlt => FirmaFinanzen(stand, amPc: true),
            _ => PcShopSeite(stand),
        };
        foreach (var teil in seite)
        {
            yield return teil;
        }
    }

    private IEnumerable<Control?> PcShopSeite(Spielstand stand)
    {
        if (_s.Geschaefte is not { } g)
        {
            yield break;
        }
        var bestellt = Spielwelt.Bestellungen(stand);
        if (bestellt.Count > 0)
        {
            var zeilen = bestellt.Select(b => (Control?)Zeile(T(Py.Str(Wohnung.MoebelStueck(b.Moebel, I)?["name"]), 14, "text", true),
                T(Spielwelt.Ankunft(b.Liefertag, stand.Tag), 12, b.Liefertag <= stand.Tag ? "green" : "accent", true))).ToList();
            yield return Karte(Spielwelt.Unterwegs, zeilen, "orange", PyFormat.Prozent("%d bestellt", bestellt.Count));
        }
        foreach (var laden in Spielwelt.ShopLaeden)
        {
            var zeilen = new List<Control?>();
            foreach (var w in g.Waren(laden).Where(w => w.Art == Geschaefte.ArtMoebel))
            {
                if (Wohnung.MoebelStueck(w.Id, I) is not { } m)
                {
                    continue;
                }
                var zusatz = Spielwelt.LieferText(Spielwelt.Liefertage(m)) + (SchrankZusatz(w) is { } s ? " · " + s : "");
                zeilen.Add(WarenZeile(w, stand.Geld >= w.Preis, () => Bestellen(laden, w), Spielwelt.KnopfBestellen, zusatz));
            }
            yield return Karte(laden == "moebelhaus" ? Spielwelt.ShopMoebel : Spielwelt.ShopElektro, zeilen,
                laden == "moebelhaus" ? "purple" : "cyan", Spielwelt.ShopUnter);
        }
    }

    private IEnumerable<Control?> PcKrediteSeite(Spielstand stand)
    {
        yield return T(Spielwelt.KrediteHinweis, 13, "text_dim");
        yield return LaufendeKredite(stand, ansehen: true);
    }

    private void Bestellen(string laden, Ware ware)
    {
        if (Tun(a => { a.Bestellen(_s.Geschaefte!, laden, ware.Id); return []; }) is null)
        {
            return;
        }
        var tage = Wohnung.MoebelStueck(ware.Id, I) is { } m ? Spielwelt.Liefertage(m) : 1;
        Hinweisen(PyFormat.Prozent("Bestellt: %s. %s, das Paket steht dann vor deiner Haustür.", ware.Name, Spielwelt.LieferText(tage)), "green");
        Gekauft();
    }

    /// <summary>„Paket mitnehmen“ an der Haustuer: alle angekommenen Pakete in den Karton (Nico 4a).</summary>
    private void PaketMitnehmen()
    {
        var anzahl = Spielwelt.Angekommen(Stand).Count;
        if (Tun(a => a.PaketAbholen()) is null)
        {
            return;
        }
        _s.Abgleichen();
        _s.Spiel.PaketGenommen(anzahl);
        Neu();
        Geaendert?.Invoke();
    }

    /// <summary>„Meine Reise“ am Buecherregal (ohne Regal der Hinweis).</summary>
    private void ReiseAmRegal()
    {
        if (!Spielwelt.Aufgestellt(Stand, Spielwelt.RegalTyp))
        {
            _ = Dialog.Zeigen(Spielwelt.KnopfReise, Spielwelt.OhneRegal, Dialog.Ok);
            return;
        }
        _stapel.Clear();
        ReiseOeffnen();
    }

    /// <summary>„Firma öffnen“ am Schreibtisch im eigenen Buero.</summary>
    private void FirmaAmTisch()
    {
        _stapel.Clear();
        FirmaOeffnen();
    }

    /// <summary>Knoepfe der Spielwelt (Phase 12 Welle 2), sonst false.</summary>
    private bool SpielweltAktion(string aktion)
    {
        switch (aktion)
        {
            case Spielwelt.AktionComputer:
                PcOeffnen();
                return true;
            case Spielwelt.AktionReise:
                ReiseAmRegal();
                return true;
            case Spielwelt.AktionFirma:
                FirmaAmTisch();
                return true;
            case Spielwelt.AktionPaket:
                PaketMitnehmen();
                return true;
            default:
                return false;
        }
    }
}
