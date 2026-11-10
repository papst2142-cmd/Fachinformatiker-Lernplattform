using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136, Kurzplan P12-79 Wunsch 1): mehrere Stuecke waehlen und gemeinsam schieben, drehen,
/// kopieren, loeschen (jede Aktion ein Schritt im Verlauf, <see cref="AussenBearbeitung.Verschieben(PyObjekt, IReadOnlyList{AussenStelle}, double, double, bool)"/>).
/// Waehlen: Umschalt+Klick nimmt ein Stueck dazu oder heraus, Umschalt+Ziehen auf freier Flaeche zieht einen Rahmen (Maus);
/// mit „Mehrere wählen“ (Knopf, Taste U, fuer den Finger) tut ein Tipp dasselbe wie Umschalt+Klick und ein Finger auf
/// freier Flaeche zieht den Rahmen (zwei Finger bewegen die Ansicht wie immer). Ein Druck auf ein gewaehltes Stueck zieht
/// die ganze Gruppe. Escape oder „Auswahl aufheben“ leert die Gruppe.
/// </summary>
public sealed partial class KartenFlaeche
{
    private List<AussenStelle> _gruppe = [];

    /// <summary>Gewaehlte Stuecke (nur wenn es mindestens zwei sind; ein einzelnes steht in <see cref="Auswahl"/>).</summary>
    public IReadOnlyList<AussenStelle> Gruppe => _gruppe;

    /// <summary>Sind mehrere Stuecke gewaehlt?</summary>
    public bool GruppeAktiv => _gruppe.Count > 1;

    /// <summary>Alles Gewaehlte (Gruppe, sonst das eine Stueck, sonst nichts).</summary>
    public IReadOnlyList<AussenStelle> Gewaehlt => GruppeAktiv ? _gruppe : Auswahl is { } s ? [AussenBearbeitung.Ganz(s)] : [];

    private bool _mehrereWaehlen;

    /// <summary>„Mehrere wählen“: Tippen nimmt dazu oder heraus, ein Finger auf freier Flaeche zieht einen Rahmen.</summary>
    public bool MehrereWaehlen
    {
        get => _mehrereWaehlen;
        set
        {
            if (_mehrereWaehlen == value)
            {
                return;
            }
            _mehrereWaehlen = value;
            if (value)
            {
                SetzenBeenden();
                MassbandAn = false;
            }
            MehrereGeaendert?.Invoke();
            InvalidateVisual();
        }
    }

    /// <summary>„Mehrere wählen“ an oder aus (Knopf, Markierung).</summary>
    public event Action? MehrereGeaendert;

    /// <summary>Mehrere Stuecke waehlen (0 = nichts, 1 = wie <see cref="Waehlen"/>).</summary>
    public void GruppeSetzen(IEnumerable<AussenStelle> stellen)
    {
        var liste = stellen.Select(AussenBearbeitung.Ganz).Distinct().Where(s => Stand is { } a && AussenBearbeitung.Gueltig(a, s)).ToList();
        if (liste.Count <= 1)
        {
            Waehlen(liste.FirstOrDefault());
            return;
        }
        TuerSetzenBeenden();
        Auswahl = null;
        _gruppe = liste;
        AuswahlGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Ein Stueck dazunehmen oder herausnehmen (Umschalt+Klick, Tipp mit „Mehrere wählen“).</summary>
    public void Umschalten(AussenStelle s)
    {
        var drin = Gewaehlt.Contains(AussenBearbeitung.Ganz(s));
        GruppeSetzen(AussenBearbeitung.Umschalten(Gewaehlt, s));
        GewaehltMelden(drin ? Texte.GruppeHeraus : null);
    }

    /// <summary>Alle Stuecke der Karte waehlen, die zu sehen sind (Strg+A; automatische Laternen nicht).</summary>
    public void AlleWaehlen()
    {
        if (Stand is not { } a)
        {
            return;
        }
        var (b, h) = Aussen.Groesse(a);
        GruppeSetzen(AussenBearbeitung.ImRahmen(a, -1, -1, b + 1, h + 1, AusArten));
        GewaehltMelden(null);
    }

    private void GewaehltMelden(string? vorher)
    {
        var n = Gewaehlt.Count;
        Gemeldet?.Invoke((vorher is null ? "" : vorher + " ") + (n == 1 && Stand is { } a && Auswahl is { } s ? AussenBearbeitung.Beschreibung(a, s, OrtName) : Texte.GruppeGewaehlt(n)));
    }

    // ---- Aktionen (je ein Schritt) -----------------------------------------------------------------

    /// <summary>Die Gruppe verschieben (ein Schritt, ohne Einrasten).</summary>
    public void GruppeVerschieben(double dx, double dy)
    {
        if (_projekt is null || _projekt.NurLesen || Gewaehlt.Count == 0)
        {
            return;
        }
        var (gruppe, raster) = (Gewaehlt.ToList(), Raster);
        var e = _projekt.AussenAendern(Texte.GruppeVerschoben, a => AussenBearbeitung.Verschieben(a, gruppe, dx, dy, raster));
        Gemeldet?.Invoke(Texte.GruppeVerschobenMeldung(e.Erledigt));
        GruppeSetzen(e.Auswahl);
    }

    /// <summary>Die Gruppe um ihre Mitte vierteldrehen (ein Schritt).</summary>
    public void GruppeDrehen(bool rueckwaerts = false)
    {
        if (_projekt is null || _projekt.NurLesen || !GruppeAktiv)
        {
            return;
        }
        var gruppe = _gruppe.ToList();
        var e = _projekt.AussenAendern(Texte.GruppeGedreht, a => AussenBearbeitung.Drehen(a, gruppe, rueckwaerts));
        Gemeldet?.Invoke(Texte.GruppeGedrehtMeldung(e.Erledigt));
        GruppeSetzen(e.Auswahl);
    }

    /// <summary>Die Gruppe daneben kopieren (ein Schritt); danach sind die Kopien gewaehlt.</summary>
    public void GruppeKopieren()
    {
        if (_projekt is null || _projekt.NurLesen || !GruppeAktiv)
        {
            return;
        }
        var gruppe = _gruppe.ToList();
        var e = _projekt.AussenAendern(Texte.GruppeKopiert, a => AussenBearbeitung.Kopieren(a, gruppe));
        Gemeldet?.Invoke(Texte.GruppeKopiertMeldung(e.Erledigt, e.Uebersprungen));
        GruppeSetzen(e.Auswahl.Count > 0 ? e.Auswahl : gruppe);
    }

    /// <summary>Die Gruppe loeschen (ein Schritt); Haeuser mit Ort und Paketplatz bleiben stehen.</summary>
    public void GruppeLoeschen()
    {
        if (_projekt is null || _projekt.NurLesen || !GruppeAktiv)
        {
            return;
        }
        var gruppe = _gruppe.ToList();
        var e = _projekt.AussenAendern(Texte.GruppeGeloescht, a => AussenBearbeitung.Loeschen(a, gruppe));
        Gemeldet?.Invoke(Texte.GruppeGeloeschtMeldung(e.Erledigt, e.Uebersprungen));
        if (e.Uebersprungen > 0 && Stand is { } st)
        {
            // was stehen bleiben musste, bleibt gewaehlt
            GruppeSetzen(gruppe.Where(s => s.Art is AussenArt.Haus or AussenArt.Paketplatz && AussenBearbeitung.Gueltig(st, s)));
            return;
        }
        Waehlen(null);
    }

    // ---- Druck, Rahmen, Ziehen ----------------------------------------------------------------------

    /// <summary>Ein Druck, der mehrere Stuecke betrifft: auf ein Stueck (Tipp/Ziehen) oder auf freie Flaeche (Rahmen).</summary>
    private sealed record MehrfachDruck(AussenStelle? Stueck, bool Umschalten, Point Start);

    private MehrfachDruck? _mehrfachDruck;
    private Point? _rahmenEnde;
    private bool _gruppeGezogen;

    /// <summary>Rahmen beim letzten Zeichnen in Bildpunkten (Start-Test), leer = keiner.</summary>
    public Rect RahmenBild { get; private set; }

    /// <summary>Greift dieser Druck mehrere Stuecke (Umschalt, „Mehrere wählen“, oder ein Stueck der Gruppe)?</summary>
    private bool MehrfachDrucken(PointerPressedEventArgs e, AussenStelle? s, Point p)
    {
        var umschalt = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Pointer.Type == PointerType.Mouse;
        var inGruppe = s is not null && GruppeAktiv && _gruppe.Contains(AussenBearbeitung.Ganz(s));
        if (!MehrereWaehlen && !umschalt && !inGruppe)
        {
            _mehrfachDruck = null;
            return false;
        }
        _mehrfachDruck = new MehrfachDruck(s, MehrereWaehlen || umschalt, p);
        _rahmenEnde = null;
        return true;
    }

    /// <summary>Fingerauswertung fuer einen Druck mit mehreren Stuecken (true = verarbeitet).</summary>
    private bool MehrfachAnwenden(Fingergeste.Schritt s)
    {
        if (_mehrfachDruck is not { } md)
        {
            return false;
        }
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen:
                _mehrfachDruck = null;
                if (md.Stueck is { } st)
                {
                    if (md.Umschalten)
                    {
                        Umschalten(st);
                    }
                    else
                    {
                        WaehlenMitMeldung(st);
                    }
                }
                return true;
            case Fingergeste.Art.StueckZiehen when md.Stueck is { } zst:
                if (_projekt is null || _projekt.NurLesen)
                {
                    return true;
                }
                if (!_gruppeGezogen && !Gewaehlt.Contains(AussenBearbeitung.Ganz(zst)))
                {
                    // ein noch nicht gewaehltes Stueck ziehen: es kommt dazu, dann zieht die ganze Gruppe
                    GruppeSetzen(AussenBearbeitung.Umschalten(Gewaehlt, zst));
                }
                _gruppeGezogen = true;
                _ziehWelt = new Point(s.Dx / _zoom, s.Dy / _zoom);
                InvalidateVisual();
                return true;
            case Fingergeste.Art.StueckZiehen:
                _rahmenEnde = new Point(s.X, s.Y);
                InvalidateVisual();
                return true;
            case Fingergeste.Art.StueckFertig when _gruppeGezogen:
            {
                _mehrfachDruck = null;
                _gruppeGezogen = false;
                var (dx, dy) = (s.Dx / _zoom, s.Dy / _zoom);
                _ziehWelt = default;
                // auch ein einzelnes Stueck im Modus „Mehrere wählen“ wie die Gruppe: ohne Einrasten (gleich der Vorschau)
                GruppeVerschieben(dx, dy);
                InvalidateVisual();
                return true;
            }
            case Fingergeste.Art.StueckFertig:
                _mehrfachDruck = null;
                if (_rahmenEnde is { } ende)
                {
                    RahmenFertig(md.Start, ende, md.Umschalten);
                }
                _rahmenEnde = null;
                InvalidateVisual();
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                // ein zweiter Finger: die Ansicht bewegt sich, Rahmen und Ziehen fallen weg
                _mehrfachDruck = null;
                _rahmenEnde = null;
                _gruppeGezogen = false;
                _ziehWelt = default;
                InvalidateVisual();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Rahmen von a bis b (Bildpunkte) fertig: die Stuecke darin waehlen (<paramref name="dazu"/>: zur Gruppe dazu).</summary>
    public void RahmenFertig(Point a, Point b, bool dazu)
    {
        if (Stand is not { } st)
        {
            return;
        }
        var (wa, wb) = (WeltPunkt(a), WeltPunkt(b));
        var drin = AussenBearbeitung.ImRahmen(st, wa.X, wa.Y, wb.X, wb.Y, AusArten);
        GruppeSetzen(dazu ? Gewaehlt.Concat(drin) : drin);
        GewaehltMelden(null);
    }

    /// <summary>3D: die Stuecke eines Rahmens waehlen (<paramref name="dazu"/>: zur Gruppe dazu), mit Meldung.</summary>
    public void RahmenWaehlen(IEnumerable<AussenStelle> drin, bool dazu)
    {
        GruppeSetzen(dazu ? Gewaehlt.Concat(drin) : drin);
        GewaehltMelden(null);
    }

    /// <summary>3D: ein Stueck dazunehmen oder herausnehmen, ohne das Fenster zu wechseln.</summary>
    public void UmschaltenMitMeldung(AussenStelle s) => Umschalten(s);

    /// <summary>Gruppe, Ziehen der Gruppe und Rahmen zeichnen.</summary>
    private void MehrfachZeichnen(DrawingContext ctx, PyObjekt a)
    {
        RahmenBild = default;
        if (GruppeAktiv || _gruppeGezogen)
        {
            var (dx, dy) = _gruppeGezogen ? (_ziehWelt.X, _ziehWelt.Y) : (0, 0);
            var akzent = Farbe("accent");
            var stift = new Pen(akzent, 3);
            var fuellung = new SolidColorBrush(Color.Parse(Lernplattform.Design.FarbenAnwenden.Aktuell.Farben["accent"]), _gruppeGezogen ? 0.35 : 0.18);
            var huelle = default(Rect?);
            foreach (var s in Gewaehlt.Where(s => AussenBearbeitung.Gueltig(a, s)))
            {
                foreach (var vieleck in Bauen3D.Umriss(a, s))
                {
                    var punkte = vieleck.Select(q => new System.Numerics.Vector2((float)(q.X + dx), (float)(q.Y + dy))).ToList();
                    Flaeche(ctx, punkte, fuellung, stift);
                    foreach (var q in punkte)
                    {
                        var r = new Rect(P(q.X, q.Y), new Size(0, 0));
                        huelle = huelle is { } h ? h.Union(r) : r;
                    }
                }
            }
            if (huelle is { } rahmen)
            {
                rahmen = rahmen.Inflate(8);
                ctx.DrawRectangle(null, new Pen(akzent, 1.5, new DashStyle([6, 4], 0)), rahmen, 4, 4);
                var ft = new FormattedText(Texte.GruppeGewaehlt(Gewaehlt.Count), CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default), 13, Brushes.White);
                var ort = new Point(Math.Clamp(rahmen.X, 4, Math.Max(4, Bounds.Width - ft.Width - 12)), Math.Max(ObenFrei + 2, rahmen.Y - ft.Height - 8));
                Anzeigen.Zeichnen(ctx, ft, Texte.GruppeGewaehlt(Gewaehlt.Count), ort, new Size(5, 2), akzent, null, 4, VerdecktFuer(this), Bounds.Size, _anzeigen);
            }
        }
        if (_mehrfachDruck is { Stueck: null } md && _rahmenEnde is { } ende)
        {
            var r = new Rect(md.Start, ende).Normalize();
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse(Lernplattform.Design.FarbenAnwenden.Aktuell.Farben["accent"]), 0.12),
                new Pen(Farbe("accent"), 2, new DashStyle([6, 4], 0)), r);
            RahmenBild = r;
        }
    }
}
