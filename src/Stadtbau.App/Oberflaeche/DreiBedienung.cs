using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Spiel;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

using Point = Avalonia.Point;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.7.0 (Weisung P12-45, Plan P12-26b Abschnitt 9): Bauen in der 3D-Ansicht. Eine durchsichtige Ebene ueber der
/// Spiel-Flaeche nimmt Maus und Finger an, die Spiel-Flaeche selbst bleibt unveraendert (das Spiel bedient sie wie
/// bisher). Alle Schritte laufen ueber die Draufsicht (<see cref="KartenFlaeche"/>: dieselbe Auswahl, dieselbe Karte
/// rechts, derselbe Verlauf). Treffer mit einem Strahl aus der Kamera (<see cref="Bauen3D.Treffer"/>), der Schatten am
/// Boden wird hier gezeichnet (kein Neubau der Szene), gruen/rot nach derselben Pruefung wie die Liste „Prüfungen
/// draußen“ (im Hintergrund, der neueste Wunsch gewinnt).
/// Maus: links auf ein Stueck waehlt und zieht es, links auf freier Flaeche schiebt die Ansicht, Doppelklick auf ein Haus
/// oeffnet den Innenraum, rechts ziehen dreht (links/rechts) und kippt (hoch/runter), mittlere Taste oder Leertaste
/// schiebt, Mausrad zoomt. Finger: ein Finger wie die linke Maustaste, zwei Finger zoomen (auseinander), kippen
/// (gemeinsam hoch/runter) und drehen (Drehung der Finger). Mit „Figur laufen lassen“ laeuft die Figur dorthin.
/// </summary>
public sealed class DreiBedienung : Control
{
    private readonly StadtFlaeche _drei;
    private readonly KartenFlaeche _karte;
    private Fingergeste _geste = new();
    private readonly Dictionary<long, Point> _finger = [];
    private double _winkel;

    private enum Griff { Keiner, Bauen, Schieben, Drehen }

    private Griff _griff;
    private Point _letzt;
    private Vector2? _ziehStart;

    // Schatten
    private Bauen3D.Vorschau? _vorschau;
    private bool _ziehen;
    private (double, double)? _schluessel;
    private bool? _passt;
    private string? _grund;
    private int _nr;
    private bool _laeuft;
    private int _standNr;
    private int _basisNr = -1;
    private List<StadtbauFund>? _basis;

    /// <summary>Gegenprobe im Start-Test: Fingerauswertung wie vor S 0.5.1 (jeder Druck beginnt neu).</summary>
    public bool GesteAltFuerTests { set => _geste = new Fingergeste(altFuerTests: value); }

    public DreiBedienung(StadtFlaeche drei, KartenFlaeche karte)
    {
        _drei = drei;
        _karte = karte;
        Focusable = true;
        ClipToBounds = true;
        // die Kamera bewegt sich: Auswahl und Schatten mitzeichnen (das Ereignis kommt aus dem Zeichnen)
        _drei.BildGezeichnet += () => Dispatcher.UIThread.Post(InvalidateVisual);
        _karte.AuswahlGeaendert += () =>
        {
            if (_karte.SetzenArt is null && !_ziehen)
            {
                SchattenWeg();
            }
            InvalidateVisual();
        };
    }

    /// <summary>Schalter „Figur laufen lassen“: Tippen laesst die Figur laufen, gebaut wird nichts.</summary>
    public bool FigurLaufen { get; set; }

    /// <summary>Zeit des letzten Schattens bis zum fertigen Umriss in ms (Start-Test „3D Schatten folgt“).</summary>
    public double SchattenMs { get; private set; }

    /// <summary>Gruen (true), rot (false) oder noch in Pruefung (null); null auch ohne Schatten.</summary>
    public bool? Passt => _vorschau is null ? null : _passt;

    /// <summary>Gibt es gerade einen Schatten am Boden?</summary>
    public bool SchattenDa => _vorschau is { Umriss.Count: > 0 };

    public string? SchattenText => _vorschau is null ? null : Texte.Schatten(_passt, _grund);

    /// <summary>Letzter Abbruch der Schatten-Pruefung (Start-Test meldet ihn).</summary>
    public string? Abbruch { get; private set; }

    /// <summary>Pruefung des Schattens ist fertig (Start-Test).</summary>
    public event Action? SchattenGeprueft;

    private Projekt? Projekt => _karte.Projekt;

    private Kamera? Kamera => _drei.AktuelleKamera;

    /// <summary>Der Stand draussen hat sich geaendert (Hauptansicht): Funde von vorher neu rechnen, Schatten neu.</summary>
    public void StandGeaendert()
    {
        _standNr++;
        _basis = null;
        if (_karte.SetzenArt is not null && _schluessel is not null && _letztBoden is { } b)
        {
            _schluessel = null;
            SchattenSetzen(b.X, b.Y);
        }
        else if (!_ziehen)
        {
            SchattenWeg();
        }
        InvalidateVisual();
    }

    private Vector2? _letztBoden;

    // ---- Kamera -----------------------------------------------------------------------------------

    public void Kippen(float grad)
    {
        if (Kamera is { } k)
        {
            k.Neigung = Bauen3D.Kippen(k.Neigung, grad);
            _drei.AnfordernBild();
        }
    }

    public void GanzeStadt()
    {
        if (Kamera is { } k && Projekt is { } p)
        {
            var (x, z, abstand) = Bauen3D.GanzeStadt(p.AussenStand);
            k.Setzen(x, z);
            k.Zoomen(k.Abstand / abstand);
            k.Neigung = Bauen3D.NeigungMax - 15;
            _drei.AnfordernBild();
        }
    }

    public void Zoomen(float faktor)
    {
        if (Kamera is { } k)
        {
            k.Zoomen(faktor);
            _drei.AnfordernBild();
        }
    }

    private Vector2? BodenAn(Point p) =>
        Kamera?.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height);

    /// <summary>Stueck unter dem Bildpunkt (Fassade zuerst, sonst Boden wie in der Draufsicht).</summary>
    public AussenStelle? TrefferAn(Point p)
    {
        if (Kamera is not { } k || Projekt is not { } proj || k.Strahl((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is not { } strahl)
        {
            return null;
        }
        var szene = _drei.Spiel?.Szene.Haeuser.Select(h => (h.Min, h.Max)).ToList();
        var haeuser = Bauen3D.Haeuser(proj.AussenStand, szene is null ? null : f => Bauen3D.HoeheAus(szene, f));
        return Bauen3D.Treffer(proj.AussenStand, strahl.Start, strahl.Richtung, haeuser, Bauen3D.Greifen(k.Abstand));
    }

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
        _griff = Griff.Bauen;
        if (e.Pointer.Type != PointerType.Mouse)
        {
            _finger[e.Pointer.Id] = p;
            if (_finger.Count == 2)
            {
                _winkel = Winkel();
            }
        }
        var erster = !_geste.Aktiv;
        var bauen = !FigurLaufen && Projekt is { NurLesen: false };
        AussenStelle? s = null;
        if (!FigurLaufen && _karte.SetzenArt is null && erster)
        {
            s = TrefferAn(p);
        }
        // beim Setzen zieht der Finger den Schatten mit (am Tablet gibt es kein Schweben)
        Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, bauen && (s is not null || _karte.SetzenArt is not null)), p);
        if (!erster || FigurLaufen)
        {
            return;
        }
        if (_karte.SetzenArt is not null)
        {
            if (bauen)
            {
                SchattenAn(p);
            }
            return;
        }
        if (e.ClickCount == 2 && s is not null && _karte.InnenOeffnen(s))
        {
            return;
        }
        if (Projekt is not null)
        {
            _karte.WaehlenMitMeldung(s);
        }
        _ziehStart = BodenAn(p);
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
                _drei.AnfordernBild();
                return;
            case Griff.Schieben when Kamera is { } k:
                k.Verschieben((float)(p.X - _letzt.X), (float)(p.Y - _letzt.Y), (float)Bounds.Height);
                _letzt = p;
                _drei.AnfordernBild();
                return;
            case Griff.Keiner:
                // Maus schwebt: beim Setzen folgt der Schatten
                if (e.Pointer.Type == PointerType.Mouse && _karte.SetzenArt is not null && !FigurLaufen && Projekt is { NurLesen: false })
                {
                    SchattenAn(p);
                }
                return;
        }
        if (_finger.ContainsKey(e.Pointer.Id))
        {
            _finger[e.Pointer.Id] = p;
        }
        Anwenden(_geste.Bewegen(e.Pointer.Id, p.X, p.Y), p);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this);
        if (_griff is Griff.Drehen or Griff.Schieben)
        {
            _griff = Griff.Keiner;
            e.Pointer.Capture(null);
            return;
        }
        // zuerst auswerten, dann freigeben (wie die Draufsicht, Lehre S 0.5.1)
        Anwenden(_geste.Loslassen(e.Pointer.Id), p);
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
        Anwenden(_geste.Verloren(e.Pointer.Id), default);
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

    private void Anwenden(Fingergeste.Schritt s, Point p)
    {
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when FigurLaufen:
                _drei.Tippen(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.Tippen when _karte.SetzenArt is not null:
            case Fingergeste.Art.StueckFertig when _karte.SetzenArt is not null:
                SetzenAn(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.StueckZiehen when _karte.SetzenArt is not null:
                SchattenAn(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.StueckZiehen when _karte.Auswahl is not null && _ziehStart is { } a && BodenAn(new Point(s.X, s.Y)) is { } b:
                ZiehenVorschau(b.X - a.X, b.Y - a.Y);
                break;
            case Fingergeste.Art.StueckFertig when _ziehen:
            {
                _ziehen = false;
                var (dx, dy) = _ziehDelta;
                SchattenWeg();
                _karte.Verschieben(dx, dy);
                break;
            }
            case Fingergeste.Art.StueckAbbrechen:
                if (_ziehen)
                {
                    _ziehen = false;
                    SchattenWeg();
                }
                break;
            case Fingergeste.Art.AnsichtSchieben when Kamera is { } k:
                k.Verschieben((float)s.Dx, (float)s.Dy, (float)Bounds.Height);
                _drei.AnfordernBild();
                break;
            case Fingergeste.Art.Zoomen when Kamera is { } k:
            {
                k.Zoomen((float)s.Faktor);
                // zwei Finger gemeinsam hoch = steiler (mehr von oben), Drehung der Finger dreht die Kamera
                k.Neigung = Bauen3D.Kippen(k.Neigung, (float)(-s.Dy * 0.3));
                if (_finger.Count >= 2)
                {
                    var w = Winkel();
                    var d = w - _winkel;
                    d = d > 180 ? d - 360 : d < -180 ? d + 360 : d;
                    k.Drehen((float)d);
                    _winkel = w;
                }
                _drei.AnfordernBild();
                break;
            }
        }
    }

    /// <summary>Setzen an einem Bildpunkt (Tippen oder Start-Test): ein Schritt im Verlauf ueber die Draufsicht.</summary>
    public AussenStelle? SetzenAn(Point p)
    {
        if (BodenAn(p) is not { } b)
        {
            return null;
        }
        SchattenWeg();
        return _karte.SetzenAn(b.X, b.Y);
    }

    // ---- Schatten ---------------------------------------------------------------------------------

    private (double, double) _ziehDelta;

    /// <summary>Schatten fuer das naechste gesetzte Stueck unter dem Bildpunkt (Schweben, Finger beim Setzen).</summary>
    public void SchattenAn(Point p)
    {
        if (BodenAn(p) is { } b)
        {
            SchattenSetzen(b.X, b.Y);
        }
    }

    private double Schritt => _karte.Raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;

    private (double, double) Runden(double x, double y) => (Math.Round(x / Schritt) * Schritt, Math.Round(y / Schritt) * Schritt);

    private void SchattenSetzen(double x, double y)
    {
        if (Projekt is not { } proj || _karte.SetzenArt is not { } art)
        {
            return;
        }
        _letztBoden = new Vector2((float)x, (float)y);
        var schluessel = Runden(x, y);
        if (_schluessel == schluessel && _vorschau is not null)
        {
            return;
        }
        _schluessel = schluessel;
        var uhr = Stopwatch.StartNew();
        var dreh = KartenFlaeche.DrehbarBeimSetzen(art) ? _karte.SetzenDreh : 0;
        _vorschau = Bauen3D.Setzen(proj.AussenStand, art, x, y, _karte.Raster, _karte.SetzenModell, dreh, _karte.SetzenBlueprint);
        SchattenMs = uhr.Elapsed.TotalMilliseconds;
        PruefungAnstossen();
    }

    private void ZiehenVorschau(double dx, double dy)
    {
        if (Projekt is not { } proj || _karte.Auswahl is not { } s)
        {
            return;
        }
        _ziehen = true;
        _ziehDelta = (dx, dy);
        var schluessel = Runden(dx, dy);
        if (_schluessel == schluessel && _vorschau is not null)
        {
            return;
        }
        _schluessel = schluessel;
        var uhr = Stopwatch.StartNew();
        _vorschau = Bauen3D.Verschieben(proj.AussenStand, s, dx, dy, _karte.Raster);
        SchattenMs = uhr.Elapsed.TotalMilliseconds;
        PruefungAnstossen();
    }

    /// <summary>Schatten weg (gesetzt, abgebrochen, anderes Werkzeug).</summary>
    public void SchattenWeg()
    {
        _vorschau = null;
        _schluessel = null;
        _passt = null;
        _grund = null;
        _nr++;
        InvalidateVisual();
    }

    private void PruefungAnstossen()
    {
        _nr++;
        if (_vorschau is { Stelle: null } v)
        {
            _passt = false;
            _grund = v.Grund;
            SchattenGeprueft?.Invoke();
        }
        else
        {
            _passt = null;
            _grund = null;
            PruefungStarten();
        }
        InvalidateVisual();
    }

    private void PruefungStarten()
    {
        if (_laeuft || _vorschau is not { Stelle: not null } v || Projekt is not { } proj)
        {
            return;
        }
        _laeuft = true;
        var nr = _nr;
        var standNr = _standNr;
        var basis = _basisNr == standNr ? _basis : null;
        var vorherStand = basis is null ? (PyObjekt)Py.Kopie(proj.AussenStand)! : null;
        var fassung = proj.Basis;
        var innen = proj.Innenraeume();
        var daten = proj.Daten;
        var nachher = v.Stand;
        _ = Task.Run(() =>
        {
            var vorher = basis ?? Bauen3D.Pruefen(vorherStand!, fassung, innen, daten);
            return (vorher, neu: Bauen3D.NeuRot(vorher, Bauen3D.Pruefen(nachher, fassung, innen, daten)));
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _laeuft = false;
            if (t.IsFaulted)
            {
                // die Liste „Prüfungen draußen“ meldet denselben Abbruch; der Schatten bleibt grau
                Abbruch = t.Exception?.InnerException?.Message;
                return;
            }
            {
                if (standNr == _standNr)
                {
                    _basis = t.Result.vorher;
                    _basisNr = standNr;
                }
                if (nr == _nr && standNr == _standNr)
                {
                    _passt = t.Result.neu is null;
                    _grund = t.Result.neu?.Text;
                    SchattenGeprueft?.Invoke();
                    InvalidateVisual();
                    return;
                }
            }
            // inzwischen ein neuerer Wunsch: den neuesten pruefen
            PruefungStarten();
        }), TaskScheduler.Default);
    }

    // ---- Zeichnen ---------------------------------------------------------------------------------

    private static readonly IBrush Gruen = new SolidColorBrush(Color.Parse("#34D399"), 0.5);
    private static readonly IBrush Rot = new SolidColorBrush(Color.Parse("#F87171"), 0.55);
    private static readonly IBrush Grau = new SolidColorBrush(Color.Parse("#9CA3AF"), 0.45);
    private static readonly IPen RandHell = new Pen(Brushes.White, 3);
    private static readonly IPen RandDunkel = new Pen(new SolidColorBrush(Color.Parse("#111827")), 1.5);

    /// <summary>Umriss beim letzten Zeichnen in Bildpunkten (Start-Test: der Schatten liegt unter dem Zeiger).</summary>
    public Rect SchattenRahmen { get; private set; }

    public override void Render(DrawingContext ctx)
    {
        // durchsichtig, aber trifft (sonst gehen Maus und Finger an die Spiel-Flaeche)
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (Kamera is not { } k || Projekt is not { } proj)
        {
            return;
        }
        if (_karte.Auswahl is { } s && !_ziehen)
        {
            foreach (var vieleck in Bauen3D.Umriss(proj.AussenStand, s))
            {
                if (Geometrie(k, vieleck) is { } g)
                {
                    ctx.DrawGeometry(null, RandHell, g);
                    ctx.DrawGeometry(null, RandDunkel, g);
                }
            }
        }
        SchattenRahmen = default;
        if (_vorschau is { } v)
        {
            var pinsel = _passt switch { true => Gruen, false => Rot, null => Grau };
            var rahmen = default(Rect?);
            foreach (var vieleck in v.Umriss)
            {
                if (Geometrie(k, vieleck) is { } g)
                {
                    ctx.DrawGeometry(pinsel, RandDunkel, g);
                    rahmen = rahmen is { } r ? r.Union(g.Bounds) : g.Bounds;
                }
            }
            SchattenRahmen = rahmen ?? default;
            if (SchattenText is { } text)
            {
                var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 15, Brushes.White)
                {
                    MaxTextWidth = Math.Max(40, Bounds.Width - 40),
                };
                var ort = new Point(16, Bounds.Height - ft.Height - 22);
                ctx.DrawRectangle(_passt == false ? new SolidColorBrush(Color.Parse("#7F1D1D"), 0.9) : new SolidColorBrush(Color.Parse("#111827"), 0.8), null,
                    new Rect(ort.X - 8, ort.Y - 5, ft.Width + 16, ft.Height + 10), 6, 6);
                ctx.DrawText(ft, ort);
            }
        }
    }

    private StreamGeometry? Geometrie(Kamera k, Vector2[] vieleck)
    {
        var punkte = new List<Point>(vieleck.Length);
        foreach (var e in vieleck)
        {
            if (k.Bildpunkt(new Vector3(e.X, Bauen3D.SchattenHoehe, e.Y), (float)Bounds.Width, (float)Bounds.Height) is not { } b)
            {
                return null;
            }
            punkte.Add(new Point(b.X, b.Y));
        }
        if (punkte.Count < 3)
        {
            return null;
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
}
