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

using Point = Avalonia.Point;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.50.0 (Weisung P12-234 mit P12-231 Antwort 1b): Bauen im Innenraum in 3D. Eine durchsichtige Ebene ueber der 3D-Flaeche
/// des Innenraums nimmt Maus und Finger an (die Spiel-Flaeche bleibt unveraendert). Alle Schritte laufen ueber den Grundriss
/// (<see cref="GrundrissFlaeche"/>: dieselbe Auswahl, dieselbe Karte rechts, derselbe Verlauf, dasselbe Raster). Treffer mit
/// einem Strahl aus der Kamera (<see cref="InnenBauen3D.Treffer"/>); der Schatten am Boden wird hier gezeichnet (kein Neubau),
/// gruen/rot nach derselben Pruefung wie die Liste rechts im Grundriss (<see cref="InnenBauen3D.Pruefen"/>, im Hintergrund, der
/// neueste Wunsch gewinnt).
/// Maus: links auf ein Moebel waehlt und zieht es, links auf freier Flaeche schiebt die Ansicht, rechts ziehen dreht und kippt,
/// mittlere Taste oder Leertaste schiebt, Mausrad zoomt. Finger: ein Finger wie die linke Maustaste, zwei Finger zoomen,
/// kippen und drehen; ein zweiter Finger setzt und schiebt nie. Ist links ein Stueck zum Setzen gewaehlt, folgt der Schatten
/// der Maus (am Tablet dem Finger), Tippen setzt. Mit „Figur laufen lassen“ laeuft die Figur dorthin, gebaut wird dann nichts.
/// </summary>
public sealed class InnenDreiBedienung : Control
{
    private readonly StadtFlaeche _drei;
    private readonly GrundrissFlaeche _grund;
    private readonly Func<Innenraum?> _innen;
    private readonly Func<IReadOnlyList<Rect>> _verdeckt;
    private Fingergeste _geste = new();
    private readonly Dictionary<long, Point> _finger = [];
    private double _winkel;

    private enum Griff { Keiner, Bauen, Schieben, Drehen }

    private Griff _griff;
    private Point _letzt;
    private Vector2? _ziehStart;

    // Schatten
    private InnenBauen3D.Vorschau? _vorschau;
    private bool _ziehen;
    private (double, double) _ziehDelta;
    private (double, double)? _schluessel;
    private bool? _passt;
    private string? _grundText;
    private int _nr;
    private bool _laeuft;
    private string? _basisSchluessel;
    private List<StadtbauFund>? _basis;

    public InnenDreiBedienung(StadtFlaeche drei, GrundrissFlaeche grund, Func<Innenraum?> innen, Func<IReadOnlyList<Rect>> verdeckt)
    {
        _drei = drei;
        _grund = grund;
        _innen = innen;
        _verdeckt = verdeckt;
        Focusable = true;
        ClipToBounds = true;
        // die Kamera bewegt sich: Auswahl und Schatten mitzeichnen (das Ereignis kommt aus dem Zeichnen)
        _drei.BildGezeichnet += () => Dispatcher.UIThread.Post(InvalidateVisual);
        _grund.AuswahlGeaendert += () =>
        {
            if (_grund.SetzenTyp is null && !_ziehen)
            {
                SchattenWeg();
            }
            InvalidateVisual();
        };
    }

    /// <summary>Nur Start-Test (Gegenprobe): Fingerauswertung wie vor S 0.5.1 (jeder Druck beginnt neu).</summary>
    public bool GesteAltFuerTests { set => _geste = new Fingergeste(altFuerTests: value); }

    /// <summary>Schalter „Figur laufen lassen“: Tippen laesst die Figur laufen, gebaut wird nichts.</summary>
    public bool FigurLaufen { get; set; }

    /// <summary>Gruen (true), rot (false) oder noch in Pruefung (null); null auch ohne Schatten.</summary>
    public bool? Passt => _vorschau is null ? null : _passt;

    /// <summary>Gibt es gerade einen Schatten am Boden?</summary>
    public bool SchattenDa => _vorschau is { Umriss.Length: > 0 };

    /// <summary>Text unter dem Schatten (null = kein Schatten).</summary>
    public string? SchattenText => _vorschau is null ? null : Texte.Schatten(_passt, _grundText);

    /// <summary>Zeit vom letzten Schatten-Wunsch bis zum fertigen Umriss in ms (Start-Test).</summary>
    public double SchattenMs { get; private set; }

    /// <summary>Letzter Abbruch der Schatten-Pruefung (Start-Test meldet ihn).</summary>
    public string? Abbruch { get; private set; }

    /// <summary>Pruefung des Schattens ist fertig (Start-Test).</summary>
    public event Action? SchattenGeprueft;

    private Kamera? Kamera => _drei.AktuelleKamera;

    private Projekt? Projekt => _grund.Projekt;

    private bool Bauen => !FigurLaufen && Projekt is { NurLesen: false } && _grund.Ort is not null;

    /// <summary>Der Grundriss hat sich geaendert (Hauptansicht): Funde von vorher neu, Schatten weg.</summary>
    public void StandGeaendert()
    {
        _basis = null;
        _basisSchluessel = null;
        if (!_ziehen && _grund.SetzenTyp is null)
        {
            SchattenWeg();
        }
        InvalidateVisual();
    }

    // ---- Treffer ------------------------------------------------------------------------------------

    private Vector2? BodenAn(Point p) =>
        Kamera?.Boden((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height);

    /// <summary>Moebel unter dem Bildpunkt (vorderster Kasten, sonst Boden wie der Grundriss) oder null.</summary>
    public Stelle? TrefferAn(Point p)
    {
        if (Kamera is not { } k || _grund.GebaeudeJetzt is not { } g
            || k.Strahl((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height) is not { } strahl)
        {
            return null;
        }
        return InnenBauen3D.Treffer(g, InnenBauen3D.Kaesten(g, _innen()), strahl.Start, strahl.Richtung);
    }

    /// <summary>Bildpunkt einer Stelle am Boden des Innenraums (Start-Test, Fotos).</summary>
    public Point? BildpunktAm(double x, double z, float hoehe = 0) =>
        Kamera?.Bildpunkt(new Vector3((float)x, hoehe, (float)z), (float)Bounds.Width, (float)Bounds.Height) is { } b ? new Point(b.X, b.Y) : null;

    // ---- Eingabe ------------------------------------------------------------------------------------

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
        Stelle? s = null;
        if (erster && Bauen && _grund.SetzenTyp is null)
        {
            s = TrefferAn(p);
        }
        // beim Setzen zieht der Finger den Schatten mit (am Tablet gibt es kein Schweben)
        Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, Bauen && (s is not null || _grund.SetzenTyp is not null)), p);
        if (!erster || FigurLaufen)
        {
            return;
        }
        if (_grund.SetzenTyp is not null)
        {
            if (Bauen)
            {
                SchattenAn(p);
            }
            return;
        }
        if (Projekt is not null && s != _grund.Auswahl)
        {
            _grund.Waehlen(s);
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
                if (e.Pointer.Type == PointerType.Mouse && _grund.SetzenTyp is not null && Bauen)
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
        // zuerst auswerten, dann freigeben (wie Grundriss und Draufsicht, Lehre S 0.5.1)
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
        if (Kamera is { } k)
        {
            k.Zoomen(MathF.Pow(1.12f, (float)e.Delta.Y));
            _drei.AnfordernBild();
        }
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
            case Fingergeste.Art.Tippen when _grund.SetzenTyp is not null && Bauen:
            case Fingergeste.Art.StueckFertig when _grund.SetzenTyp is not null && Bauen:
                SetzenAn(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.StueckZiehen when _grund.SetzenTyp is not null:
                SchattenAn(new Point(s.X, s.Y));
                break;
            case Fingergeste.Art.StueckZiehen when _grund.Auswahl is not null && _ziehStart is { } a && BodenAn(new Point(s.X, s.Y)) is { } b:
                ZiehenVorschau(b.X - a.X, b.Y - a.Y);
                break;
            case Fingergeste.Art.StueckFertig when _ziehen:
            {
                _ziehen = false;
                var (dx, dy) = _ziehDelta;
                SchattenWeg();
                _grund.VerschiebenUm(dx, dy);
                break;
            }
            case Fingergeste.Art.StueckAbbrechen:
                if (_ziehen || _grund.SetzenTyp is not null)
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
                // zwei Finger gemeinsam hoch = steiler, Drehung der Finger dreht die Kamera (wie draussen in 3D)
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

    /// <summary>Das gewaehlte Stueck dort setzen, wo der Bildpunkt den Boden trifft (derselbe Schritt wie im Grundriss).</summary>
    public Stelle? SetzenAn(Point p)
    {
        if (BodenAn(p) is not { } b)
        {
            return null;
        }
        SchattenWeg();
        return _grund.SetzenAn(b.X, b.Y);
    }

    /// <summary>Das gewaehlte Moebel um den Weg am Boden zwischen zwei Bildpunkten verschieben (Start-Test, gleich dem Ziehen).</summary>
    public Stelle? ZiehenVon(Point von, Point bis) =>
        BodenAn(von) is { } a && BodenAn(bis) is { } b ? _grund.VerschiebenUm(b.X - a.X, b.Y - a.Y) : null;

    // ---- Schatten -----------------------------------------------------------------------------------

    private double Schritt => _grund.Raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;

    private (double, double) Runden(double x, double y) => (Math.Round(x / Schritt) * Schritt, Math.Round(y / Schritt) * Schritt);

    /// <summary>Schatten fuer das naechste gesetzte Stueck unter dem Bildpunkt (Schweben, Finger beim Setzen).</summary>
    public void SchattenAn(Point p)
    {
        if (BodenAn(p) is not { } b || _grund.GebaeudeJetzt is not { } g || _grund.SetzenTyp is not { } typ)
        {
            return;
        }
        var schluessel = Runden(b.X, b.Y);
        if (_schluessel == schluessel && _vorschau is not null)
        {
            return;
        }
        _schluessel = schluessel;
        var uhr = Stopwatch.StartNew();
        _vorschau = InnenBauen3D.Setzen(g, typ, b.X, b.Y, _grund.Raster);
        SchattenMs = uhr.Elapsed.TotalMilliseconds;
        PruefungAnstossen();
    }

    private void ZiehenVorschau(double dx, double dy)
    {
        if (_grund.GebaeudeJetzt is not { } g || _grund.Auswahl is not { } s)
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
        _vorschau = InnenBauen3D.Verschieben(g, s, dx, dy, _grund.Raster);
        SchattenMs = uhr.Elapsed.TotalMilliseconds;
        PruefungAnstossen();
    }

    /// <summary>Schatten weg (gesetzt, abgebrochen, abgewaehlt).</summary>
    public void SchattenWeg()
    {
        _vorschau = null;
        _schluessel = null;
        _passt = null;
        _grundText = null;
        _nr++;
        InvalidateVisual();
    }

    private void PruefungAnstossen()
    {
        _nr++;
        if (_vorschau is { Stelle: null } v)
        {
            _passt = false;
            _grundText = v.Grund;
            SchattenGeprueft?.Invoke();
        }
        else
        {
            _passt = null;
            _grundText = null;
            PruefungStarten();
        }
        InvalidateVisual();
    }

    private void PruefungStarten()
    {
        if (_laeuft || _vorschau is not { Stelle: not null } v || Projekt is not { } proj || _grund.Ort is not { } ort
            || _grund.GebaeudeJetzt is not { } g)
        {
            return;
        }
        _laeuft = true;
        var nr = _nr;
        var vorherText = ort + "\n" + PythonJson.Text(g);
        var basis = _basisSchluessel == vorherText ? _basis : null;
        var vorher = basis is null ? (PyObjekt)Py.Kopie(g)! : null;
        var auftrag = InnenBauen3D.Vorbereiten(proj, ort);
        var nachher = v.Gebaeude;
        _ = Task.Run(() =>
        {
            var alt = basis ?? InnenBauen3D.Pruefen(auftrag, vorher!).Funde;
            return (alt, neu: Bauen3D.NeuRot(alt, InnenBauen3D.Pruefen(auftrag, nachher).Funde));
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _laeuft = false;
            if (t.IsFaulted)
            {
                // die Pruefung rechts meldet denselben Abbruch; der Schatten bleibt grau
                Abbruch = t.Exception?.InnerException?.Message;
                return;
            }
            if (_grund.Ort is { } jetzt && _grund.GebaeudeJetzt is { } gj && jetzt + "\n" + PythonJson.Text(gj) == vorherText)
            {
                (_basis, _basisSchluessel) = (t.Result.alt, vorherText);
                if (nr == _nr)
                {
                    _passt = t.Result.neu is null;
                    _grundText = t.Result.neu?.Text;
                    SchattenGeprueft?.Invoke();
                    InvalidateVisual();
                    return;
                }
            }
            // inzwischen ein neuerer Wunsch oder ein anderer Stand: den neuesten pruefen
            PruefungStarten();
        }), TaskScheduler.Default);
    }

    // ---- Zeichnen -----------------------------------------------------------------------------------

    private static readonly IBrush Gruen = new SolidColorBrush(Color.Parse("#34D399"), 0.5);
    private static readonly IBrush Rot = new SolidColorBrush(Color.Parse("#F87171"), 0.55);
    private static readonly IBrush Grau = new SolidColorBrush(Color.Parse("#9CA3AF"), 0.45);
    private static readonly IPen RandHell = new Pen(Brushes.White, 3);
    private static readonly IPen RandDunkel = new Pen(new SolidColorBrush(Color.Parse("#111827")), 1.5);

    /// <summary>Umriss des Schattens beim letzten Zeichnen in Bildpunkten (Start-Test: der Schatten liegt unter dem Zeiger).</summary>
    public Rect SchattenRahmen { get; private set; }

    /// <summary>Rahmen der Auswahl beim letzten Zeichnen in Bildpunkten (Start-Test, leer = keine Auswahl sichtbar).</summary>
    public Rect AuswahlRahmen { get; private set; }

    /// <summary>Weisung P12-137: wohin die Ebene ihre Anzeigen zuletzt gezeichnet hat (Text, Rahmen).</summary>
    public IReadOnlyList<(string Text, Rect Rahmen)> AnzeigenGezeichnet => _anzeigen;

    private readonly List<(string Text, Rect Rahmen)> _anzeigen = [];

    public override void Render(DrawingContext ctx)
    {
        // durchsichtig, aber trifft (sonst gehen Maus und Finger an die Spiel-Flaeche)
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        _anzeigen.Clear();
        AuswahlRahmen = default;
        SchattenRahmen = default;
        if (Kamera is not { } k || _grund.GebaeudeJetzt is not { } g)
        {
            return;
        }
        if (_grund.Auswahl is { } s && !_ziehen && InnenBauen3D.Kaesten(g, _innen()).FirstOrDefault(x => x.Stelle == s) is { } kasten)
        {
            // Auswahl: Kasten des Moebels unten und oben, damit man es auch hinter anderen erkennt
            var unten = Ecken(kasten.Min, kasten.Max, kasten.Min.Y + 0.02f);
            var oben = Ecken(kasten.Min, kasten.Max, kasten.Max.Y);
            Rect? rahmen = null;
            foreach (var vieleck in new[] { unten, oben })
            {
                if (Geometrie(k, vieleck) is { } geo)
                {
                    ctx.DrawGeometry(null, RandHell, geo);
                    ctx.DrawGeometry(null, RandDunkel, geo);
                    rahmen = rahmen is { } r ? r.Union(geo.Bounds) : geo.Bounds;
                }
            }
            AuswahlRahmen = rahmen ?? default;
        }
        if (_vorschau is { } v)
        {
            var pinsel = _passt switch { true => Gruen, false => Rot, null => Grau };
            if (v.Umriss.Length > 0 && Geometrie(k, [.. v.Umriss.Select(e => new Vector3(e.X, Bauen3D.SchattenHoehe, e.Y))]) is { } geo)
            {
                ctx.DrawGeometry(pinsel, RandDunkel, geo);
                SchattenRahmen = geo.Bounds;
            }
            if (SchattenText is { } text)
            {
                var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 15, Brushes.White)
                {
                    MaxTextWidth = Math.Max(40, Bounds.Width - 40),
                };
                Anzeigen.Zeichnen(ctx, ft, text, new Point(8, Bounds.Height - ft.Height - 27), new Size(8, 5),
                    _passt == false ? new SolidColorBrush(Color.Parse("#7F1D1D"), 0.9) : new SolidColorBrush(Color.Parse("#111827"), 0.8), null, 6, _verdeckt(), Bounds.Size, _anzeigen);
            }
        }
    }

    private static Vector3[] Ecken(Vector3 min, Vector3 max, float y) =>
        [new(min.X, y, min.Z), new(max.X, y, min.Z), new(max.X, y, max.Z), new(min.X, y, max.Z)];

    private StreamGeometry? Geometrie(Kamera k, Vector3[] vieleck)
    {
        var punkte = new List<Point>(vieleck.Length);
        foreach (var e in vieleck)
        {
            if (k.Bildpunkt(e, (float)Bounds.Width, (float)Bounds.Height) is not { } b)
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
