using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

using Point = Avalonia.Point;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.9.0 (Weisung P12-48, Frage 5 = a): Bedienung der 3D-Vorschau im Editor „Gebäude entwerfen“. Eine durchsichtige
/// Ebene ueber der Spiel-Flaeche (wie <see cref="DreiBedienung"/> beim Bauen in 3D). Ein Finger (oder die linke Maustaste)
/// waehlt ein Teil oder setzt das gewaehlte Teil auf das Feld unter dem Finger; auf freier Flaeche ziehen schiebt die
/// Ansicht. Zwei Finger zoomen, kippen (gemeinsam hoch/runter) und drehen (Drehung der Finger). Verschoben wird ein Teil
/// nur mit den Pfeil-Knoepfen rechts (oder den Pfeiltasten). Maus: rechts ziehen dreht und kippt, mittlere Taste oder
/// Leertaste schiebt, Mausrad zoomt. Beim Setzen zeigt ein Schatten auf dem Feld gruen (passt) oder rot (neuer roter Fund).
/// </summary>
public sealed class EntwurfBedienung : Control
{
    private readonly EntwurfAnsicht _ansicht;
    private Fingergeste _geste = new();
    private readonly Dictionary<long, Point> _finger = [];
    private double _winkel;

    private enum Griff { Keiner, Finger, Schieben, Drehen }

    private Griff _griff;
    private Point _letzt;

    // Schatten
    private (int X, int Y)? _feld;
    private bool? _passt;
    private string? _grund;

    /// <summary>Gegenprobe im Start-Test: Fingerauswertung wie vor S 0.5.1 (jeder Druck beginnt neu).</summary>
    public bool GesteAltFuerTests { set => _geste = new Fingergeste(altFuerTests: value); }

    internal EntwurfBedienung(EntwurfAnsicht ansicht)
    {
        _ansicht = ansicht;
        Focusable = true;
        ClipToBounds = true;
        // die Kamera bewegt sich: Raster, Auswahl und Schatten mitzeichnen (das Ereignis kommt aus dem Zeichnen)
        _ansicht.Drei.BildGezeichnet += () => Dispatcher.UIThread.Post(InvalidateVisual);
    }

    /// <summary>Gruen (true), rot (false); null ohne Schatten.</summary>
    public bool? Passt => _feld is null ? null : _passt;

    /// <summary>Gibt es gerade einen Schatten?</summary>
    public bool SchattenDa => _feld is not null;

    /// <summary>Feld des Schattens im Bauraster.</summary>
    public (int X, int Y)? SchattenFeld => _feld;

    public string? SchattenText => _feld is null ? null : Texte.Schatten(_passt, _grund);

    /// <summary>Zeit fuer den letzten Schatten (Pruefung mit dem neuen Teil) in ms.</summary>
    public double SchattenMs { get; private set; }

    private Kamera? Kamera => _ansicht.Drei.AktuelleKamera;

    /// <summary>Der Entwurf, die Ebene oder das Teil zum Setzen hat sich geaendert: Schatten neu.</summary>
    public void StandGeaendert()
    {
        if (_feld is { } f && _ansicht.SetzenTeil is not null)
        {
            SchattenSetzen(f.X, f.Y);
        }
        else
        {
            SchattenWeg();
        }
        InvalidateVisual();
    }

    // ---- Kamera -----------------------------------------------------------------------------------

    public void Kippen(float grad)
    {
        if (Kamera is { } k)
        {
            k.Neigung = Bauen3D.Kippen(k.Neigung, grad);
            _ansicht.Drei.AnfordernBild();
        }
    }

    public void Drehen(float grad)
    {
        if (Kamera is { } k)
        {
            k.Drehen(grad);
            _ansicht.Drei.AnfordernBild();
        }
    }

    public void Zoomen(float faktor)
    {
        if (Kamera is { } k)
        {
            k.Zoomen(faktor);
            _ansicht.Drei.AnfordernBild();
        }
    }

    private (Vector3 Start, Vector3 Richtung)? StrahlAn(Point p) =>
        Kamera?.Strahl((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height);

    /// <summary>Feld der gewaehlten Ebene unter dem Bildpunkt (oder null).</summary>
    public (int X, int Y)? FeldAn(Point p) =>
        StrahlAn(p) is { } s ? EntwurfAnsicht3D.Feld(s.Start, s.Richtung, _ansicht.Ebene, RasterHalle) : null;

    /// <summary>
    /// S 0.31.0: Raster einer Halle (1/3 Feld, Ebene 1,0)? Ja bei einer Halle, ohne Teil einer Gebaeudeart auch, wenn ein
    /// Hallen-Teil zum Setzen gewaehlt ist.
    /// </summary>
    public bool RasterHalle =>
        _ansicht.SetzenTeil is { } id && !_ansicht.Entwurf.Teile.Any(t => !Bauteile.IstAufsatz(t.Id)) && !Bauteile.IstAufsatz(id)
            ? Bauteile.IstHalle(id)
            : _ansicht.Entwurf.Halle;

    /// <summary>Teil unter dem Bildpunkt (Ebenen bis zur gewaehlten) oder -1.</summary>
    public int TeilAn(Point p) =>
        StrahlAn(p) is { } s ? EntwurfAnsicht3D.Treffer(_ansicht.Entwurf, s.Start, s.Richtung, _ansicht.Ebene) : -1;

    // ---- Eingabe ----------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        e.Handled = true;
        var p = e.GetPosition(this);
        e.Pointer.Capture(this);
        if (e.Pointer.Type == PointerType.Mouse && !_geste.Aktiv)
        {
            var k = e.GetCurrentPoint(this).Properties;
            if (k.IsRightButtonPressed)
            {
                _griff = Griff.Drehen;
                _letzt = p;
                return;
            }
            if (k.IsMiddleButtonPressed || KartenFlaeche.LeertasteGehalten)
            {
                _griff = Griff.Schieben;
                _letzt = p;
                return;
            }
        }
        _griff = Griff.Finger;
        if (e.Pointer.Type != PointerType.Mouse)
        {
            _finger[e.Pointer.Id] = p;
            if (_finger.Count == 2)
            {
                _winkel = Winkel();
            }
            if (_ansicht.SetzenTeil is not null && _finger.Count == 1)
            {
                // am Tablet gibt es kein Schweben: der Schatten erscheint unter dem Finger
                SchattenAn(p);
            }
        }
        // Frage 5 = a: ein Finger zieht nie ein Teil (Verschieben nur mit den Pfeilen)
        Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, aufStueck: false));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        switch (_griff)
        {
            case Griff.Drehen when Kamera is { } k:
                k.Drehen((float)((p.X - _letzt.X) * 0.4));
                k.Neigung = Bauen3D.Kippen(k.Neigung, (float)((p.Y - _letzt.Y) * 0.3));
                _letzt = p;
                _ansicht.Drei.AnfordernBild();
                return;
            case Griff.Schieben when Kamera is { } k:
                k.Verschieben((float)(p.X - _letzt.X), (float)(p.Y - _letzt.Y), (float)Bounds.Height);
                _letzt = p;
                _ansicht.Drei.AnfordernBild();
                return;
            case Griff.Keiner:
                // Maus schwebt: beim Setzen folgt der Schatten
                if (e.Pointer.Type == PointerType.Mouse && _ansicht.SetzenTeil is not null)
                {
                    SchattenAn(p);
                }
                return;
        }
        if (_finger.ContainsKey(e.Pointer.Id))
        {
            _finger[e.Pointer.Id] = p;
        }
        Anwenden(_geste.Bewegen(e.Pointer.Id, p.X, p.Y));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_griff is Griff.Drehen or Griff.Schieben)
        {
            _griff = Griff.Keiner;
            e.Pointer.Capture(null);
            return;
        }
        // zuerst auswerten, dann freigeben (Lehre S 0.5.1)
        Anwenden(_geste.Loslassen(e.Pointer.Id));
        _finger.Remove(e.Pointer.Id);
        e.Pointer.Capture(null);
        if (!_geste.Aktiv)
        {
            _griff = Griff.Keiner;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Anwenden(_geste.Verloren(e.Pointer.Id));
        _finger.Remove(e.Pointer.Id);
        if (!_geste.Aktiv)
        {
            _griff = Griff.Keiner;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoomen(MathF.Pow(1.12f, (float)e.Delta.Y));
        e.Handled = true;
    }

    private double Winkel()
    {
        var f = _finger.Values.Take(2).ToArray();
        return f.Length < 2 ? 0 : Math.Atan2(f[1].Y - f[0].Y, f[1].X - f[0].X) * 180 / Math.PI;
    }

    private void Anwenden(Fingergeste.Schritt s)
    {
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen:
                Tippen(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.AnsichtSchieben when Kamera is { } k:
                k.Verschieben((float)s.Dx, (float)s.Dy, (float)Bounds.Height);
                _ansicht.Drei.AnfordernBild();
                break;
            case Fingergeste.Art.Zoomen when Kamera is { } k:
            {
                k.Zoomen((float)s.Faktor);
                // zwei Finger gemeinsam hoch = steiler, Drehung der Finger dreht die Kamera
                k.Neigung = Bauen3D.Kippen(k.Neigung, (float)(-s.Dy * 0.3));
                if (_finger.Count >= 2)
                {
                    var w = Winkel();
                    var d = w - _winkel;
                    d = d > 180 ? d - 360 : d < -180 ? d + 360 : d;
                    k.Drehen((float)d);
                    _winkel = w;
                }
                _ansicht.Drei.AnfordernBild();
                break;
            }
        }
    }

    /// <summary>Tippen (Finger oder linke Maustaste ohne Ziehen): setzen oder waehlen.</summary>
    public void Tippen(Point p)
    {
        if (_ansicht.SetzenTeil is not null)
        {
            SetzenAn(p);
            return;
        }
        var i = TeilAn(p);
        _ansicht.Waehlen(i);
        if (i >= 0 && _ansicht.Entwurf.Teile[i] is var t)
        {
            _ansicht.Melden(Texte.EntwurfGewaehlt(t.Art?.Anzeige ?? t.Name));
        }
    }

    /// <summary>Setzen an einem Bildpunkt (Tippen oder Start-Test): ein Schritt im Verlauf des Entwurfs.</summary>
    public int SetzenAn(Point p)
    {
        if (FeldAn(p) is not { } f)
        {
            return -1;
        }
        SchattenWeg();
        return _ansicht.SetzenAn(f.X, f.Y);
    }

    // ---- Schatten ---------------------------------------------------------------------------------

    /// <summary>Schatten fuer das naechste gesetzte Teil unter dem Bildpunkt (Schweben, Finger beim Setzen).</summary>
    public void SchattenAn(Point p)
    {
        if (FeldAn(p) is { } f)
        {
            if (_feld != f)
            {
                SchattenSetzen(f.X, f.Y);
            }
        }
        else if (_feld is not null)
        {
            SchattenWeg();
        }
    }

    private void SchattenSetzen(int x, int y)
    {
        if (_ansicht.SetzenTeil is not { } id)
        {
            return;
        }
        var uhr = Stopwatch.StartNew();
        _feld = (x, y);
        // dieselbe Pruefung wie die Liste rechts, mit dem Teil auf einer Kopie: rot nur bei einem neuen roten Fund
        var vorher = _ansicht.Funde;
        var kopie = new Entwurf { HalleWunsch = _ansicht.Entwurf.HalleWunsch };
        kopie.Teile.AddRange(_ansicht.Entwurf.Teile);
        kopie.Eingaenge.AddRange(_ansicht.Entwurf.Eingaenge);
        if (kopie.Setzen(id, x, y, _ansicht.Ebene, _ansicht.SetzenDreh) < 0)
        {
            (_passt, _grund) = (false, Texte.EntwurfNichtImRaster);
        }
        else
        {
            var neu = Bauen3D.NeuRot(vorher, kopie.Pruefen(Katalog.Bauteil));
            (_passt, _grund) = (neu is null, neu?.Text);
        }
        SchattenMs = uhr.Elapsed.TotalMilliseconds;
        InvalidateVisual();
    }

    /// <summary>Schatten weg (gesetzt, abgebrochen, anderes Teil).</summary>
    public void SchattenWeg()
    {
        _feld = null;
        _passt = null;
        _grund = null;
        InvalidateVisual();
    }

    // ---- Zeichnen ---------------------------------------------------------------------------------

    private static readonly IBrush Gruen = new SolidColorBrush(Color.Parse("#34D399"), 0.5);
    private static readonly IBrush Rot = new SolidColorBrush(Color.Parse("#F87171"), 0.55);
    private static readonly IPen RasterStift = new Pen(new SolidColorBrush(Color.Parse("#FFFFFF"), 0.35), 1);
    private static readonly IPen RasterRand = new Pen(new SolidColorBrush(Color.Parse("#111827"), 0.6), 1.5);
    private static readonly IPen RandHell = new Pen(Brushes.White, 3);
    private static readonly IPen RandDunkel = new Pen(new SolidColorBrush(Color.Parse("#111827")), 1.5);

    /// <summary>Umriss des Schattens beim letzten Zeichnen in Bildpunkten (Start-Test).</summary>
    public Rect SchattenRahmen { get; private set; }

    /// <summary>Weisung P12-137: Rahmen der Elemente ueber der Flaeche (Kamera-Karte), bezogen auf die uebergebene Flaeche.</summary>
    public Func<Visual, IReadOnlyList<Rect>>? Verdecker { get; set; }

    /// <summary>Weisung P12-137: wohin die Ebene ihre Anzeigen zuletzt gezeichnet hat (Text, Rahmen).</summary>
    public IReadOnlyList<(string Text, Rect Rahmen)> AnzeigenGezeichnet => _anzeigen;

    private readonly List<(string Text, Rect Rahmen)> _anzeigen = [];

    public override void Render(DrawingContext ctx)
    {
        // durchsichtig, aber trifft (sonst gehen Maus und Finger an die Spiel-Flaeche)
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        _anzeigen.Clear();
        if (Kamera is not { } k)
        {
            return;
        }
        if (_ansicht.RasterZeigen)
        {
            RasterZeichnen(ctx, k);
        }
        if (_ansicht.Auswahl is var a and >= 0 && a < _ansicht.Entwurf.Teile.Count)
        {
            var (min, max) = EntwurfAnsicht3D.Kasten(_ansicht.Entwurf.Teile[a], _ansicht.Entwurf.Halle);
            if (Kanten(k, min, max) is { } g)
            {
                ctx.DrawGeometry(null, RandHell, g);
                ctx.DrawGeometry(null, RandDunkel, g);
            }
        }
        SchattenRahmen = default;
        if (_feld is { } f && Vieleck(k, EntwurfAnsicht3D.FeldEcken(f.X, f.Y, _ansicht.Ebene, 0.03f, RasterHalle)) is { } schatten)
        {
            ctx.DrawGeometry(_passt == false ? Rot : Gruen, RandDunkel, schatten);
            SchattenRahmen = schatten.Bounds;
            if (SchattenText is { } text)
            {
                var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 15, Brushes.White)
                {
                    MaxTextWidth = Math.Max(40, Bounds.Width - 40),
                };
                Anzeigen.Zeichnen(ctx, ft, text, new Point(8, Bounds.Height - ft.Height - 27), new Size(8, 5),
                    _passt == false ? new SolidColorBrush(Color.Parse("#7F1D1D"), 0.9) : new SolidColorBrush(Color.Parse("#111827"), 0.8), null, 6, Verdecker?.Invoke(this) ?? [], Bounds.Size, _anzeigen);
            }
        }
    }

    private void RasterZeichnen(DrawingContext ctx, Kamera k)
    {
        var halle = RasterHalle;
        var h = (_ansicht.Ebene * Bauteile.EbeneHoehe(halle)) + 0.02f;
        var f = Bauteile.Feld(halle);
        var felder = Entwurf.RasterVon(halle);
        float r = EntwurfAnsicht3D.Rand, n = Entwurf.Raster;
        for (var i = 0; i <= felder; i++)
        {
            var stift = i == 0 || i == felder ? RasterRand : RasterStift;
            Linie(ctx, k, stift, new Vector3(r + (i * f), h, r), new Vector3(r + (i * f), h, r + n));
            Linie(ctx, k, stift, new Vector3(r, h, r + (i * f)), new Vector3(r + n, h, r + (i * f)));
        }
    }

    private Point? Punkt(Kamera k, Vector3 v) =>
        k.Bildpunkt(v, (float)Bounds.Width, (float)Bounds.Height) is { } b ? new Point(b.X, b.Y) : null;

    /// <summary>
    /// Linie des Rasters feldweise: liegt ein Ende hinter der Kamera (nah herangezoomt), fehlen nur die Stuecke dort,
    /// nicht die ganze Linie.
    /// </summary>
    private void Linie(DrawingContext ctx, Kamera k, IPen stift, Vector3 a, Vector3 b)
    {
        var vorher = Punkt(k, a);
        for (var i = 1; i <= Entwurf.Raster; i++)
        {
            var jetzt = Punkt(k, Vector3.Lerp(a, b, i / (float)Entwurf.Raster));
            if (vorher is { } p && jetzt is { } q)
            {
                ctx.DrawLine(stift, p, q);
            }
            vorher = jetzt;
        }
    }

    private StreamGeometry? Vieleck(Kamera k, Vector3[] ecken)
    {
        var punkte = new List<Point>(ecken.Length);
        foreach (var e in ecken)
        {
            if (Punkt(k, e) is not { } b)
            {
                return null;
            }
            punkte.Add(b);
        }
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(punkte[0], true);
            foreach (var q in punkte.Skip(1))
            {
                c.LineTo(q);
            }
            c.EndFigure(true);
        }
        return g;
    }

    /// <summary>Die 12 Kanten eines Kastens als Linien (Auswahl).</summary>
    private StreamGeometry? Kanten(Kamera k, Vector3 min, Vector3 max)
    {
        var e = new Vector3[8];
        for (var i = 0; i < 8; i++)
        {
            e[i] = new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
        }
        var p = new Point[8];
        for (var i = 0; i < 8; i++)
        {
            if (Punkt(k, e[i]) is not { } b)
            {
                return null;
            }
            p[i] = b;
        }
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            for (var i = 0; i < 8; i++)
            {
                foreach (var bit in new[] { 1, 2, 4 })
                {
                    if ((i & bit) == 0)
                    {
                        c.BeginFigure(p[i], false);
                        c.LineTo(p[i | bit]);
                        c.EndFigure(false);
                    }
                }
            }
        }
        return g;
    }
}
