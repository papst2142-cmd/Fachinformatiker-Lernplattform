using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;
using V2 = System.Numerics.Vector2;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Gelaende (S 0.42.0, Weisung P12-192): Huegel und gemalte Hoehen in der Draufsicht als Hoehenlinien (alle 0,25, jede
/// 0,5 beschriftet, ganz in der Karte und nie unter Kamera-Karte oder Werkzeugleiste: <see cref="Anzeigen"/>), Umriss je
/// Huegel; Pinsel (heben, senken, glaetten): ein Finger malt einen Zug, jeder Zug ist ein Schritt im Verlauf.
/// </summary>
public sealed partial class KartenFlaeche
{
    /// <summary>Abstand der Hoehenlinien.</summary>
    public const float HoehenlinienStufe = 0.25f;

    /// <summary>Beschriftete Hoehenlinien (jede zweite) liegen hoechstens so dicht beieinander (Karten-Einheiten).</summary>
    private const float SchildAbstand = 3f;

    private static readonly IBrush HoehenFarbe = new SolidColorBrush(Color.Parse("#5E7F3E"));
    private static readonly IBrush HuegelFlaeche = new SolidColorBrush(Color.Parse("#7FA85A"), 0.25);
    private static readonly IBrush PinselFarbe = new SolidColorBrush(Color.Parse("#6D4C9F"), 0.25);

    private string? _gelaendeSchluessel;
    private List<(float Stufe, V2 A, V2 B)> _hoehenlinien = [];

    /// <summary>Start-Test: Zahl der gezeichneten Hoehenlinien-Stuecke und ihre Beschriftungen im letzten Bild.</summary>
    public int HoehenlinienGezeichnet { get; private set; }

    /// <summary>Start-Test: Beschriftungen der Hoehenlinien (Text, Bildflaeche) im letzten Bild.</summary>
    public IReadOnlyList<(string Text, Rect Rahmen)> HoehenSchilder { get; private set; } = [];

    /// <summary>Hoehenlinien nur neu rechnen, wenn sich das Gelaende geaendert hat.</summary>
    private List<(float Stufe, V2 A, V2 B)> Hoehenlinien(PyObjekt a, Gelaende g)
    {
        var schluessel = PythonJson.Text(AussenBearbeitung.GelaendeAbschnitt(a));
        if (schluessel != _gelaendeSchluessel)
        {
            var (b, h) = Welt();
            _hoehenlinien = g.Hoehenlinien(HoehenlinienStufe, (float)b, (float)h);
            _gelaendeSchluessel = schluessel;
        }
        return _hoehenlinien;
    }

    /// <summary>Huegel-Flaechen (unter allem anderen, nach dem Rasen gezeichnet).</summary>
    private void GelaendeFlaechenZeichnen(DrawingContext ctx, PyObjekt a, double dx, double dy)
    {
        if (!Zeigt(Ebene.Gelaende) || AussenBearbeitung.GelaendeVon(a) is not { } g)
        {
            return;
        }
        for (var i = 0; i < g.Huegel.Count; i++)
        {
            var s = new AussenStelle(AussenArt.Huegel, i);
            var (ox, oy) = s == Auswahl ? (dx, dy) : (0, 0);
            var umriss = g.Huegel[i].Umriss();
            var geo = new StreamGeometry();
            using (var c = geo.Open())
            {
                c.BeginFigure(P(umriss[0].X + ox, umriss[0].Y + oy), true);
                foreach (var q in umriss.Skip(1))
                {
                    c.LineTo(P(q.X + ox, q.Y + oy));
                }
                c.EndFigure(true);
            }
            ctx.DrawGeometry(HuegelFlaeche, new Pen(HoehenFarbe, 1.2, new DashStyle([4, 3], 0)), geo);
        }
    }

    /// <summary>Hoehenlinien mit Beschriftung (ueber Strassen und Haeusern nicht noetig: dort liegt nie ein Huegel).</summary>
    private void HoehenlinienZeichnen(DrawingContext ctx, PyObjekt a)
    {
        HoehenlinienGezeichnet = 0;
        HoehenSchilder = [];
        if (!Zeigt(Ebene.Gelaende) || AussenBearbeitung.GelaendeVon(a) is not { } g)
        {
            return;
        }
        var linien = Hoehenlinien(a, g);
        var duenn = new Pen(HoehenFarbe, 1);
        var dick = new Pen(HoehenFarbe, 1.8);
        foreach (var (stufe, p, q) in linien)
        {
            ctx.DrawLine(Math.Abs((stufe / 0.5f) - MathF.Round(stufe / 0.5f)) < 0.01f ? dick : duenn, P(p.X, p.Y), P(q.X, q.Y));
            HoehenlinienGezeichnet++;
        }
        if (!Zeigt(Ebene.Beschriftungen) || _zoom < 12)
        {
            return;
        }
        // je Stufe 0,5 / 1,0 / 1,5 ein Schild, weitere erst SchildAbstand weiter (sonst stapeln sie sich am Gipfel)
        var schilder = new List<(string, Rect)>();
        var gesetzt = new List<(float, V2)>();
        var rand = new Rect(P(0, 0), P(Welt().B, Welt().H));
        foreach (var (stufe, p, q) in linien)
        {
            if (Math.Abs((stufe / 0.5f) - MathF.Round(stufe / 0.5f)) >= 0.01f)
            {
                continue;
            }
            var m = (p + q) / 2;
            if (gesetzt.Any(x => MathF.Abs(x.Item1 - stufe) < 0.01f && V2.Distance(x.Item2, m) < SchildAbstand))
            {
                continue;
            }
            gesetzt.Add((stufe, m));
            var text = Texte.HoehenlinieText(stufe);
            var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 11, HoehenFarbe);
            var bild = P(m.X, m.Y);
            var (bx, by, _) = Beschriftung.InKarte(bild.X - (ft.Width / 2) - 2, bild.Y - (ft.Height / 2) - 1, ft.Width + 4, ft.Height + 2, rand.Left, rand.Top, rand.Right, rand.Bottom);
            var r = Anzeigen.Zeichnen(ctx, ft, text, new Point(bx, by), new Size(2, 1), new SolidColorBrush(Color.Parse("#B9D29A"), 0.85), null, 2,
                VerdecktFuer(this), Bounds.Size, _anzeigen);
            schilder.Add((text, r));
        }
        HoehenSchilder = schilder;
    }

    // ---- Pinsel ----------------------------------------------------------------------------------

    private PinselArt? _pinselArt;
    private readonly List<V2> _pinselZug = [];
    private V2 _pinselStart;

    /// <summary>Pinsel an (Heben, Senken, Glaetten) oder aus (null). An: kein Setzen, keine Auswahl, kein Massband.</summary>
    public PinselArt? GelaendePinsel
    {
        get => _pinselArt;
        set
        {
            if (_pinselArt == value)
            {
                return;
            }
            _pinselArt = value;
            _pinselZug.Clear();
            if (value is not null)
            {
                SetzenBeenden();
                MassbandAn = false;
                Waehlen(null);
            }
            PinselGeaendert?.Invoke();
            Gemeldet?.Invoke(value is null ? Texte.PinselAus : Texte.PinselHinweis);
            InvalidateVisual();
        }
    }

    /// <summary>Groesse des Pinsels (Halbmesser in Feldern, <see cref="AussenBearbeitung.PinselGroessen"/>).</summary>
    public float PinselGroesse { get; set; } = 1f;

    /// <summary>Welle E4 Nachtrag: Staerke eines Zugs (<see cref="AussenBearbeitung.PinselStaerken"/>), voreingestellt „Mittel“.</summary>
    public float PinselStaerke { get; set; } = AussenBearbeitung.PinselStaerken[1];

    /// <summary>Pinsel an/aus oder anderer Pinsel (Knoepfe markieren).</summary>
    public event Action? PinselGeaendert;

    /// <summary>Start-Test: Stellen des laufenden Zugs (Welt).</summary>
    public IReadOnlyList<V2> PinselZugPunkte => _pinselZug;

    /// <summary>Druck mit dem Pinsel: der Zug beginnt hier.</summary>
    private void PinselDruecken(Point bild)
    {
        var w = WeltPunkt(bild);
        _pinselStart = new V2((float)w.X, (float)w.Y);
        _pinselZug.Clear();
        _pinselZug.Add(_pinselStart);
        InvalidateVisual();
    }

    /// <summary>Fingerschritt mit dem Pinsel; true = erledigt.</summary>
    private bool PinselAnwenden(Fingergeste.Schritt s)
    {
        switch (s.Art)
        {
            case Fingergeste.Art.StueckZiehen when _pinselZug.Count > 0:
            {
                var p = _pinselStart + new V2((float)(s.Dx / _zoom), (float)(s.Dy / _zoom));
                if (V2.Distance(p, _pinselZug[^1]) >= Gelaende.Raster / 4)
                {
                    _pinselZug.Add(p);
                    InvalidateVisual();
                }
                return true;
            }
            case Fingergeste.Art.Tippen when _rechtsDruck:
                _pinselZug.Clear();
                GelaendePinsel = null;
                return true;
            case Fingergeste.Art.StueckFertig or Fingergeste.Art.Tippen when _pinselZug.Count > 0 && !_nurSchieben:
                PinselZugAnwenden([.. _pinselZug]);
                _pinselZug.Clear();
                InvalidateVisual();
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                _pinselZug.Clear();
                InvalidateVisual();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Einen Pinselzug ueber die Weltpunkte malen (ein Schritt; auch fuer den Start-Test). false = nichts geaendert.</summary>
    internal bool PinselZugAnwenden(IReadOnlyList<V2> zug)
    {
        if (_projekt is null || _projekt.NurLesen || _pinselArt is not { } art)
        {
            return false;
        }
        var groesse = PinselGroesse;
        var staerke = PinselStaerke;
        var malbar = _projekt.PinselMalbar();
        var geaendert = _projekt.AussenAendern(Texte.PinselZug, a => AussenBearbeitung.Pinselzug(a, art, zug, groesse, staerke, malbar) > 0);
        Gemeldet?.Invoke(geaendert ? Texte.PinselZug : Texte.PinselNichts);
        return geaendert;
    }

    private void PinselZeichnen(DrawingContext ctx)
    {
        if (_pinselArt is null || _pinselZug.Count == 0)
        {
            return;
        }
        var r = PinselGroesse * _zoom;
        foreach (var p in _pinselZug)
        {
            ctx.DrawEllipse(PinselFarbe, null, P(p.X, p.Y), r, r);
        }
    }
}
