using Avalonia.Controls;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.46.0 (Weisung P12-220, Sammelliste 324): „Mit Bahnhof verknüpfen“ und „Verknüpfung lösen“ unter „Ausgewähltes
/// Stück“ am Bahnsteig und am Bahnhof. Nach „Mit Bahnhof verknüpfen“ waehlt der naechste Tipp auf der Draufsicht das
/// Gegenstueck; jede Verknuepfung und jedes Loesen ist ein Schritt im Verlauf (Rueckgaengig).
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Stelle, an der „Mit Bahnhof verknüpfen“ gestartet wurde (wartet auf den Tipp), sonst null.</summary>
    private AussenStelle? _bahnsteigWahl;

    /// <summary>Wartet die Draufsicht auf den Tipp des Gegenstuecks (Start-Test)?</summary>
    public bool BahnsteigWahlOffen => _bahnsteigWahl is not null;

    private void BahnsteigTeil(AussenStelle s)
    {
        if (_projekt is null || _daten is null)
        {
            return;
        }
        var a = _projekt.AussenStand;
        var amBahnsteig = BahnsteigVerknuepfung.BahnsteigBei(a, s) is not null;
        var amBahnhof = s.Art == AussenArt.Haus && s.Ort.Length > 0 && BahnsteigVerknuepfung.IstBahnhof(a, s.Ort, _daten.Fassung);
        if (!amBahnsteig && !amBahnhof)
        {
            return;
        }
        if (amBahnsteig && BahnsteigVerknuepfung.BahnhofVon(a, s) is { } b)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.BahnsteigGehoertZu(BahnsteigVerknuepfung.Name(a, b)), "Klein"));
        }
        var zahl = amBahnhof ? BahnsteigVerknuepfung.BahnsteigeVon(a, s.Ort).Count : 0;
        if (zahl > 0)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.BahnsteigeAmBahnhof(zahl), "Klein"));
        }
        var verknuepfen = Schrift.Knopf(Texte.BahnsteigVerknuepfenKnopf).Mit(Texte.BahnsteigVerknuepfenTipp);
        verknuepfen.IsEnabled = !_projekt.NurLesen;
        verknuepfen.Click += (_, _) => BahnsteigWahlStarten(s);
        var reihe = new WrapPanel { Children = { verknuepfen } };
        if (amBahnsteig ? BahnsteigVerknuepfung.BahnhofVon(a, s) is not null : zahl > 0)
        {
            var loesen = Schrift.Knopf(Texte.BahnsteigLoesenKnopf).Mit(Texte.BahnsteigLoesenTipp);
            loesen.IsEnabled = !_projekt.NurLesen;
            loesen.Click += (_, _) => BahnsteigLoesen(s);
            reihe.Children.Add(loesen);
        }
        _aussenAuswahl.Children.Add(reihe);
    }

    /// <summary>„Mit Bahnhof verknüpfen“ an <paramref name="s"/>: der naechste Tipp auf der Draufsicht waehlt das Gegenstueck.</summary>
    public void BahnsteigWahlStarten(AussenStelle s)
    {
        if (_projekt is null || _daten is null || _projekt.NurLesen)
        {
            return;
        }
        var amBahnsteig = BahnsteigVerknuepfung.BahnsteigBei(_projekt.AussenStand, s) is not null;
        ReiterZeigen(innen: false);
        _bahnsteigWahl = s;
        Karte.TippWahl = (x, y) => BahnsteigGetippt(x, y);
        Melden(amBahnsteig ? Texte.BahnsteigTippBahnhof : Texte.BahnsteigTippBahnsteig);
    }

    /// <summary>Tipp auf der Draufsicht waehrend der Wahl: passendes Gegenstueck = verknuepfen (ein Schritt).</summary>
    public string? BahnsteigGetippt(double x, double y)
    {
        if (_projekt is null || _daten is null || _bahnsteigWahl is not { } start)
        {
            return Texte.BahnsteigTippDaneben;
        }
        var a = _projekt.AussenStand;
        var amBahnsteig = BahnsteigVerknuepfung.BahnsteigBei(a, start) is not null;
        if (BahnsteigVerknuepfung.Treffer(a, x, y, sucheBahnsteig: !amBahnsteig, _daten.Fassung) is not { } ziel)
        {
            Melden(Texte.BahnsteigTippDaneben);
            return Texte.BahnsteigTippDaneben;
        }
        var (bahnsteig, ort) = amBahnsteig ? (start, ziel.Ort) : (ziel, start.Ort);
        return BahnsteigVerknuepfen(bahnsteig, ort);
    }

    /// <summary>Bahnsteig an <paramref name="bahnsteig"/> mit Ort <paramref name="ort"/> verknuepfen (ein Schritt). Rueckgabe: Grund oder null.</summary>
    public string? BahnsteigVerknuepfen(AussenStelle bahnsteig, string ort)
    {
        if (_projekt is null || _daten is null)
        {
            return Texte.BahnsteigNurBahnsteig;
        }
        BahnsteigWahlEnde();
        var fassung = _daten.Fassung;
        var name = BahnsteigVerknuepfung.Name(_projekt.AussenStand, ort);
        string? grund = BahnsteigVerknuepfung.Verknuepfen((Lernplattform.Core.Daten.PyObjekt)Lernplattform.Core.Spiel.Py.Kopie(_projekt.AussenStand)!, bahnsteig, ort, fassung);
        if (grund is null)
        {
            _projekt.AussenAendern(Texte.BahnsteigSchritt(name), a => grund = BahnsteigVerknuepfung.Verknuepfen(a, bahnsteig, ort, fassung));
        }
        Melden(grund ?? Texte.BahnsteigVerknuepft(name));
        Karte.Waehlen(bahnsteig);
        return grund;
    }

    /// <summary>„Verknüpfung lösen“ (ein Schritt).</summary>
    public void BahnsteigLoesen(AussenStelle s)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        BahnsteigWahlEnde();
        _projekt.AussenAendern(Texte.BahnsteigLoesenSchritt, a => BahnsteigVerknuepfung.Loesen(a, s));
        AussenAuswahlZeigen();
    }

    private void BahnsteigWahlEnde()
    {
        _bahnsteigWahl = null;
        Karte.TippWahl = null;
    }

    /// <summary>Escape: Wahl des Gegenstuecks abbrechen.</summary>
    private bool BahnsteigWahlAbbrechen()
    {
        if (_bahnsteigWahl is null)
        {
            return false;
        }
        BahnsteigWahlEnde();
        AussenAuswahlZeigen();
        return true;
    }
}
