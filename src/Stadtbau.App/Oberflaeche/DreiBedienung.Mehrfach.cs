using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Stadtbau.Core;
using Point = Avalonia.Point;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136): Mehrfachauswahl und Reihe in der 3D-Ansicht, gleiche Schritte wie die Draufsicht
/// (<see cref="KartenFlaeche"/>). Maus: Umschalt+Klick nimmt dazu oder heraus, Umschalt+Ziehen ueber freie Flaeche zieht
/// einen Rahmen im Bild (gewaehlt wird, dessen Mitte im Rahmen liegt), Ziehen an einem gewaehlten Stueck schiebt die
/// ganze Gruppe (Schatten gruen/rot wie beim einzelnen Stueck). Finger: mit „Mehrere wählen“ dasselbe mit einem Finger,
/// zwei Finger bewegen die Kamera wie immer. Reihe: Tipp Anfang, Tipp Ende oder ein Finger zieht in einem Zug.
/// </summary>
public sealed partial class DreiBedienung
{
    private sealed record MehrDruck(AussenStelle? Stueck, bool Umschalten, Point Start, Vector2? Boden);

    private MehrDruck? _mehr;
    private Point? _mehrRahmenEnde;
    private bool _gruppeZiehen;
    private bool _reiheAnfangHier;

    /// <summary>Rahmen beim letzten Zeichnen in Bildpunkten (Start-Test).</summary>
    public Rect RahmenBild { get; private set; }

    /// <summary>Druck mit mehreren Stuecken? (Umschalt mit der Maus, „Mehrere wählen“, ein Stueck der Gruppe)</summary>
    private bool MehrDrucken(PointerPressedEventArgs e, AussenStelle? s, Point p)
    {
        var umschalt = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Pointer.Type == PointerType.Mouse;
        var inGruppe = s is not null && _karte.GruppeAktiv && _karte.Gruppe.Contains(AussenBearbeitung.Ganz(s));
        if (!_karte.MehrereWaehlen && !umschalt && !inGruppe)
        {
            _mehr = null;
            return false;
        }
        _mehr = new MehrDruck(s, _karte.MehrereWaehlen || umschalt, p, BodenAn(p));
        _mehrRahmenEnde = null;
        return true;
    }

    private (double X, double Y)? BildVon(double x, double y) =>
        Kamera is { } k && k.Bildpunkt(new Vector3((float)x, Bauen3D.SchattenHoehe, (float)y), (float)Bounds.Width, (float)Bounds.Height) is { } b
            ? (b.X, b.Y) : null;

    /// <summary>Fingerauswertung eines Drucks mit mehreren Stuecken (true = verarbeitet).</summary>
    private bool MehrAnwenden(Fingergeste.Schritt s)
    {
        if (_mehr is not { } md)
        {
            return false;
        }
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen:
                _mehr = null;
                if (md.Stueck is { } st)
                {
                    if (md.Umschalten)
                    {
                        _karte.UmschaltenMitMeldung(st);
                    }
                    else
                    {
                        _karte.WaehlenMitMeldung(st);
                    }
                }
                return true;
            case Fingergeste.Art.StueckZiehen when md.Stueck is { } zst:
            {
                if (Projekt is not { NurLesen: false } proj || md.Boden is not { } a || BodenAn(new Point(s.X, s.Y)) is not { } b)
                {
                    return true;
                }
                if (!_gruppeZiehen && !_karte.Gewaehlt.Contains(AussenBearbeitung.Ganz(zst)))
                {
                    _karte.GruppeSetzen(AussenBearbeitung.Umschalten(_karte.Gewaehlt, zst));
                }
                _gruppeZiehen = true;
                GruppeVorschau(proj, b.X - a.X, b.Y - a.Y);
                return true;
            }
            case Fingergeste.Art.StueckZiehen:
                _mehrRahmenEnde = new Point(s.X, s.Y);
                InvalidateVisual();
                return true;
            case Fingergeste.Art.StueckFertig when _gruppeZiehen:
            {
                _mehr = null;
                _gruppeZiehen = false;
                _ziehen = false;
                var (dx, dy) = _ziehDelta;
                SchattenWeg();
                _karte.GruppeVerschieben(dx, dy);
                return true;
            }
            case Fingergeste.Art.StueckFertig:
                _mehr = null;
                if (_mehrRahmenEnde is { } ende && Projekt is { } p)
                {
                    var drin = AussenBearbeitung.ImBildRahmen(p.AussenStand, BildVon, md.Start.X, md.Start.Y, ende.X, ende.Y);
                    _karte.RahmenWaehlen(drin, md.Umschalten);
                }
                _mehrRahmenEnde = null;
                InvalidateVisual();
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                _mehr = null;
                _mehrRahmenEnde = null;
                if (_gruppeZiehen)
                {
                    _gruppeZiehen = false;
                    _ziehen = false;
                    SchattenWeg();
                }
                InvalidateVisual();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Schatten der verschobenen Gruppe (gleich dem Schritt, ohne Einrasten), Pruefung im Hintergrund.</summary>
    private void GruppeVorschau(Projekt proj, double dx, double dy)
    {
        _ziehen = true;
        _ziehDelta = (dx, dy);
        var schluessel = Runden(dx, dy);
        if (_schluessel == schluessel && _vorschau is not null)
        {
            return;
        }
        _schluessel = schluessel;
        var uhr = Stopwatch.StartNew();
        _vorschau = Bauen3D.GruppeVerschieben(proj.AussenStand, _karte.Gewaehlt, dx, dy, _karte.Raster);
        SchattenMs = uhr.Elapsed.TotalMilliseconds;
        PruefungAnstossen();
    }

    // ---- Reihe --------------------------------------------------------------------------------------

    private bool Reiht => _karte.ReiheAktiv && !FigurLaufen && Projekt is { NurLesen: false };

    /// <summary>Tippen und Ziehen beim Setzen einer Reihe (true = verarbeitet).</summary>
    private bool ReiheAnwenden(Fingergeste.Schritt s)
    {
        var b = BodenAn(new Point(s.X, s.Y));
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when b is { } t:
                ReiheTippen(t);
                return true;
            case Fingergeste.Art.StueckZiehen:
                if (_karte.ReiheAnfang is null && BodenAn(new Point(s.X - s.Dx, s.Y - s.Dy)) is { } start)
                {
                    _karte.ReiheAnfangSetzen(start.X, start.Y);
                    _reiheAnfangHier = true;
                }
                if (b is { } z)
                {
                    _karte.ReiheZielSetzen(z.X, z.Y);
                }
                return true;
            case Fingergeste.Art.StueckFertig when b is { } f:
                _karte.ReiheBis(f.X, f.Y);
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                if (_reiheAnfangHier)
                {
                    _karte.ReiheAbbrechen();
                }
                return true;
            default:
                return false;
        }
    }

    /// <summary>Erster Tipp = Anfang, zweiter = Reihe setzen (auch fuer den Start-Test).</summary>
    internal void ReiheTippen(Vector2 b)
    {
        if (_karte.ReiheAnfang is null)
        {
            _karte.ReiheAnfangSetzen(b.X, b.Y);
        }
        else
        {
            _karte.ReiheBis(b.X, b.Y);
        }
    }

    /// <summary>Start-Test: Tipp der Reihe an einem Bildpunkt.</summary>
    internal void ReiheTippenFuerTests(Point p)
    {
        if (BodenAn(p) is { } b)
        {
            ReiheTippen(b);
        }
    }

    /// <summary>Start-Test: Maus schwebt beim Setzen einer Reihe (Vorschau bis hier).</summary>
    internal void ReiheSchwebenFuerTests(Point p)
    {
        if (BodenAn(p) is { } b)
        {
            _karte.ReiheZielSetzen(b.X, b.Y);
        }
    }

    /// <summary>Rahmen der Vorschau einer Reihe beim letzten Zeichnen (Start-Test).</summary>
    public Rect ReiheRahmen { get; private set; }

    // ---- Zeichnen -----------------------------------------------------------------------------------

    private void MehrZeichnen(DrawingContext ctx, Kamera k, PyObjekt stand)
    {
        RahmenBild = default;
        var akzent = new SolidColorBrush(Color.Parse(Lernplattform.Design.FarbenAnwenden.Aktuell.Farben["accent"]));
        if (_karte.GruppeAktiv && !_ziehen)
        {
            var stift = new Pen(akzent, 3);
            var huelle = default(Rect?);
            foreach (var s in _karte.Gruppe.Where(s => AussenBearbeitung.Gueltig(stand, s)))
            {
                foreach (var vieleck in Bauen3D.Umriss(stand, s))
                {
                    if (Geometrie(k, vieleck) is { } g)
                    {
                        ctx.DrawGeometry(null, RandHell, g);
                        ctx.DrawGeometry(null, stift, g);
                        huelle = huelle is { } h ? h.Union(g.Bounds) : g.Bounds;
                    }
                }
            }
            if (huelle is { } r)
            {
                var ft = new FormattedText(Texte.GruppeGewaehlt(_karte.Gruppe.Count), CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default), 13, Brushes.White);
                var ort = new Point(Math.Clamp(r.X, 4, Math.Max(4, Bounds.Width - ft.Width - 12)), Math.Max(4, r.Y - ft.Height - 10));
                ctx.DrawRectangle(akzent, null, new Rect(ort.X, ort.Y, ft.Width + 10, ft.Height + 4), 4, 4);
                ctx.DrawText(ft, new Point(ort.X + 5, ort.Y + 2));
            }
        }
        if (_mehr is { Stueck: null } md && _mehrRahmenEnde is { } ende)
        {
            var r = new Rect(md.Start, ende).Normalize();
            ctx.DrawRectangle(new SolidColorBrush(akzent.Color, 0.12), new Pen(akzent, 2, new DashStyle([6, 4], 0)), r);
            RahmenBild = r;
        }
    }

    private void ReiheZeichnen(DrawingContext ctx, Kamera k)
    {
        ReiheRahmen = default;
        if (!_karte.ReiheAktiv)
        {
            return;
        }
        if (_karte.ReiheAnfang is { } a && BildVon(a.X, a.Y) is { } pa)
        {
            if (_karte.ReiheZiel is { } z && BildVon(z.X, z.Y) is { } pz)
            {
                ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#111827")), 1.5, new DashStyle([4, 3], 0)), new Point(pa.X, pa.Y), new Point(pz.X, pz.Y));
            }
            ctx.DrawEllipse(Brushes.White, RandDunkel, new Point(pa.X, pa.Y), 7, 7);
        }
        if (_karte.ReiheVorschau is not { } plan)
        {
            return;
        }
        var rahmen = default(Rect?);
        foreach (var st in plan.Stuecke)
        {
            var (hw, ht) = _karte.SetzenArt == AussenArt.Baum ? (AussenBearbeitung.ReiheBaumRadius, AussenBearbeitung.ReiheBaumRadius) : (st.W / 2, st.T / 2);
            Vector2[] vieleck =
            [
                new((float)(st.X - hw), (float)(st.Y - ht)), new((float)(st.X + hw), (float)(st.Y - ht)),
                new((float)(st.X + hw), (float)(st.Y + ht)), new((float)(st.X - hw), (float)(st.Y + ht)),
            ];
            if (Geometrie(k, vieleck) is { } g)
            {
                ctx.DrawGeometry(st.Frei ? Gruen : Rot, RandDunkel, g);
                rahmen = rahmen is { } r ? r.Union(g.Bounds) : g.Bounds;
            }
        }
        ReiheRahmen = rahmen ?? default;
        var text = Texte.ReiheInfo(plan.Frei, plan.Ausgelassen, plan.Dreiecke);
        var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 15, Brushes.White)
        {
            MaxTextWidth = Math.Max(40, Bounds.Width - 40),
        };
        var ort = new Point(16, Bounds.Height - ft.Height - 22);
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#111827"), 0.8), null, new Rect(ort.X - 8, ort.Y - 5, ft.Width + 16, ft.Height + 10), 6, 6);
        ctx.DrawText(ft, ort);
    }
}
