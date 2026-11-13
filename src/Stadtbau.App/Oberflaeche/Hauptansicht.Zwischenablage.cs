using Avalonia.Controls;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.47.0 (Weisung P12-226): W1 „Kopieren (für andere Projekte)“ (Strg+C) unter „Ausgewähltes Stück“ und bei
/// mehreren gewaehlten Stuecken, „Einfügen“ (Strg+V) in der Werkzeugleiste: die Vorschau folgt der Maus, ein Tipp setzt
/// die Mitte (ein Schritt, Escape bricht ab). W2 „Laterne versetzen“ am roten Fund einer Laterne: Vorschlag im Hintergrund,
/// Vorschau auf der Karte, „Übernehmen“ = ein Schritt.
/// </summary>
public sealed partial class Hauptansicht
{
    private Button _einfuegenChip = null!;
    private Zwischenablage? _einfuegen;
    private CancellationTokenSource? _laterneSuche;

    /// <summary>Start-Test: Knopf „Einfügen“ und ob gerade eingefuegt wird.</summary>
    internal Button EinfuegenKnopf => _einfuegenChip;
    internal bool EinfuegenOffen => _einfuegen is not null;

    /// <summary>Start-Test: letzte Suche von „Laterne versetzen“ (fertig mit Vorschlag oder Grund).</summary>
    internal Task<(LaterneVersetzen.Vorschlag? Vorschlag, string? Grund)>? LaterneSuche { get; private set; }

    /// <summary>Start-Test: Zeile unter dem Knopf („Suche freie Stelle …“, danach Vorschlag oder Grund).</summary>
    internal TextBlock? LaterneStatus { get; private set; }

    private Button KopierenKnopf(IReadOnlyList<AussenStelle> gruppe)
    {
        var k = Schrift.Knopf(Texte.InZwischenablage).Vorlese(Texte.InZwischenablage).Mit(Texte.InZwischenablageTipp);
        k.IsEnabled = gruppe.Any(AussenBearbeitung.Kopierbar);
        k.Click += (_, _) => InZwischenablageLegen(gruppe);
        return k;
    }

    /// <summary>Gewaehlte Stuecke in die Zwischenablage des Werkzeugordners legen (kein Schritt im Verlauf).</summary>
    public string InZwischenablageLegen(IReadOnlyList<AussenStelle> gruppe)
    {
        if (_projekt is null || gruppe.Count == 0)
        {
            return Texte.NichtsKopierbar;
        }
        try
        {
            if (AussenBearbeitung.InZwischenablage(_projekt, gruppe, out var uebersprungen) is not { } z)
            {
                Melden(Texte.NichtsKopierbar);
                return Texte.NichtsKopierbar;
            }
            z.Ablegen(_ordner);
            var text = Texte.InZwischenablageGelegt(z.Anzahl, uebersprungen);
            Melden(text);
            EinfuegenKnopfZeigen();
            return text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or StadtbauFehler)
        {
            _ordner.Fehler("Zwischenablage: " + e.GetType().Name + ": " + e.Message);
            Melden(e.Message);
            return e.Message;
        }
    }

    private void EinfuegenKnopfZeigen()
    {
        if (_einfuegenChip is null)
        {
            return;
        }
        var z = Zwischenablage.Laden(_ordner);
        Tipp.Setzen(_einfuegenChip, z is null ? Texte.EinfuegenLeer : Texte.EinfuegenTipp(z.Anzahl, z.Projekt));
        ChipMarkieren(_einfuegenChip, _einfuegen is not null);
    }

    /// <summary>„Einfügen“ (Knopf, Strg+V): die Vorschau folgt der Maus, der naechste Tipp setzt die Mitte.</summary>
    public string? EinfuegenStarten()
    {
        if (_projekt is null || _projekt.NurLesen || _daten is null)
        {
            return Texte.EinfuegenLeer;
        }
        if (_einfuegen is not null)
        {
            EinfuegenEnde();
            return null;
        }
        if (Zwischenablage.Laden(_ordner) is not { } z)
        {
            Melden(Texte.EinfuegenLeer);
            return Texte.EinfuegenLeer;
        }
        ReiterZeigen(innen: false);
        SetzenBeenden();
        _einfuegen = z;
        Karte.EinfuegenAblage = z;
        Karte.TippWahl = (x, y) => EinfuegenAn(x, y);
        ChipMarkieren(_einfuegenChip, true);
        Melden(Texte.EinfuegenHinweis);
        return null;
    }

    /// <summary>Die Zwischenablage mit der Mitte bei (x, y) einfuegen (ein Schritt). Rueckgabe: Grund oder null.</summary>
    public string? EinfuegenAn(double x, double y)
    {
        if (_projekt is null || _daten is null || _einfuegen is not { } z)
        {
            return Texte.EinfuegenLeer;
        }
        var stadt1 = _daten.Fassung;
        var raster = Karte.Raster;
        AussenBearbeitung.EinfuegenErgebnis? ergebnis = null;
        // Vorab auf einer Kopie: ein Grund aendert nichts und legt keinen Schritt an
        var probe = AussenBearbeitung.Einfuegen((PyObjekt)Py.Kopie(_projekt.AussenStand)!, z, x, y, raster, stadt1);
        if (probe.Grund is { } g)
        {
            Melden(g);
            Karte.EinfuegenSchweben(x, y);
            return g;
        }
        _projekt.AussenAendern(Texte.EinfuegenSchritt, a => ergebnis = AussenBearbeitung.Einfuegen(a, z, x, y, raster, stadt1));
        EinfuegenEnde();
        if (ergebnis is not { } e || e.Grund is not null)
        {
            Melden(ergebnis?.Grund ?? Texte.EinfuegenLeer);
            return ergebnis?.Grund ?? Texte.EinfuegenLeer;
        }
        Karte.GruppeSetzen(e.Auswahl);
        Melden(string.Join(" ", new[] { Texte.Eingefuegt(e.Erledigt, e.Uebersprungen) }.Concat(e.Hinweise)));
        return null;
    }

    private void EinfuegenEnde()
    {
        if (_einfuegen is null)
        {
            return;
        }
        _einfuegen = null;
        Karte.EinfuegenAblage = null;
        Karte.TippWahl = null;
        ChipMarkieren(_einfuegenChip, false);
    }

    /// <summary>Escape: Einfuegen abbrechen.</summary>
    private bool EinfuegenAbbrechen()
    {
        if (_einfuegen is null)
        {
            return false;
        }
        EinfuegenEnde();
        Melden(Texte.SetzenBeendet);
        return true;
    }

    // ---- W2 Laterne versetzen ----------------------------------------------------------------------

    /// <summary>Eintrag eines Funds; am roten Fund einer Laterne mit dem Knopf „Laterne versetzen“ darunter.</summary>
    private Control AussenFundEintrag(StadtbauFund f)
    {
        var eintrag = FundListe.Eintrag(f, f.X is not null && f.Y is not null ? () => ZuAussenFund(f) : null);
        if (_projekt is null || _projekt.NurLesen || LaterneVersetzen.Nummer(_projekt.AussenStand, f) is not { } nummer)
        {
            return eintrag;
        }
        var unten = new StackPanel { Spacing = 4, Margin = new Avalonia.Thickness(8, 2, 0, 6) };
        var knopf = Schrift.Knopf(Texte.LaterneVersetzen).Vorlese(Texte.LaterneVersetzen).Mit(Texte.LaterneVersetzenTipp);
        knopf.Tag = nummer;
        knopf.Click += (_, _) => _ = LaterneVersetzenSuchenAsync(nummer, f, unten);
        unten.Children.Add(knopf);
        // Tag = Fund wie der Eintrag selbst: AussenPruefungText (Start-Test, Vorlesen der Liste) liest ihn daran
        return new StackPanel { Tag = f, Children = { eintrag, unten } };
    }

    /// <summary>Freie Stelle fuer Laterne <paramref name="nummer"/> im Hintergrund suchen und als Vorschau zeigen.</summary>
    public async Task LaterneVersetzenSuchenAsync(int nummer, StadtbauFund? f, StackPanel? ziel)
    {
        if (_projekt is not { } p || _daten is not { } daten)
        {
            return;
        }
        _laterneSuche?.Cancel();
        var abbruch = new CancellationTokenSource();
        _laterneSuche = abbruch;
        Karte.LaterneVorschlag = null;
        var status = Schrift.Text(Texte.LaterneVersetzenSucht, "Klein", farbe: "Pinsel.muted");
        LaterneStatus = status;
        ZielZeigen(status);
        if (f is not null && f.X is not null)
        {
            ZuAussenFund(f);
        }
        var stand = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var innen = p.Innenraeume();
        var suche = Task.Run(() => LaterneVersetzen.Suchen(stand, p.Basis, innen, daten, p.SzeneOhneLaternen, nummer, abbruch.Token), abbruch.Token);
        LaterneSuche = suche;
        (LaterneVersetzen.Vorschlag? Vorschlag, string? Grund) erg;
        try
        {
            erg = await suche;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (abbruch.IsCancellationRequested || !ReferenceEquals(_projekt, p))
        {
            return;
        }
        if (erg.Grund is { } grund || erg.Vorschlag is not { } v)
        {
            status.Text = erg.Grund ?? Texte.LaterneKeinPlatz;
            Melden(status.Text ?? "");
            return;
        }
        Karte.LaterneVorschlag = v;
        status.Text = Texte.LaterneVersetzenVorschlag(System.Numerics.Vector2.Distance(v.Von, v.Nach));
        var uebernehmen = Schrift.Knopf(Texte.LaterneVersetzenUebernehmen, betont: true).Vorlese(Texte.LaterneVersetzenUebernehmen).Mit(Texte.LaterneVersetzenTipp);
        uebernehmen.Click += (_, _) => LaterneUebernehmen(v);
        ZielZeigen(new StackPanel { Spacing = 4, Children = { status, uebernehmen } });
        Melden(status.Text);

        void ZielZeigen(Control c)
        {
            if (ziel is null)
            {
                return;
            }
            while (ziel.Children.Count > 1)
            {
                ziel.Children.RemoveAt(1);
            }
            ziel.Children.Add(c);
        }
    }

    /// <summary>Vorschlag uebernehmen (ein Schritt); hat sich die Stadt geaendert, kommt ein Hinweis statt eines Schritts.</summary>
    public bool LaterneUebernehmen(LaterneVersetzen.Vorschlag v)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return false;
        }
        Karte.LaterneVorschlag = null;
        var ok = _projekt.AussenAendern(Texte.LaterneVersetzenSchritt, a => LaterneVersetzen.Anwenden(a, v));
        Melden(ok ? Texte.LaterneVersetzenSchritt : Texte.LaterneVersetzenVeraltet);
        return ok;
    }
}
