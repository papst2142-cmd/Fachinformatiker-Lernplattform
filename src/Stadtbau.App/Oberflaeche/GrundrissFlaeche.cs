using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 2: Grundriss eines Innenraums von oben (Plan Abschnitt 3). Zeichnet Raeume, Flur, Waende mit Tueren,
/// Einrichtung, Personen und die Funde der Pruefung. Bedienung mit Maus, Finger und Tastatur: Stueck antippen waehlt,
/// Ziehen verschiebt (auf dem Raster), Ziehen auf freier Flaeche schiebt die Ansicht, Mausrad und zwei Finger zoomen.
/// Jede Aenderung laeuft ueber <see cref="Projekt.Aendern{T}"/> (Rueckgaengig, automatisches Speichern).
/// </summary>
public sealed class GrundrissFlaeche : Control
{
    private Projekt? _projekt;
    private string? _ort;
    private double _zoom = 30;
    private Point _versatz;
    private bool _eingepasst;

    // Ziehen
    private Vector _ziehWelt;

    /// <summary>Wird gerade ein Stueck mit einem Finger (oder der Maus) gezogen?</summary>
    private bool ZiehtStueck => _ziehen;
    private bool _ziehen;

    /// <summary>Patch S 0.5.1 (Weisung P12-37 Punkt 3): Maus und Finger, zwei Finger zoomen und schieben.</summary>
    private Fingergeste _geste = new();

    /// <summary>Nur Start-Test (Gegenprobe): Fingerauswertung wie vor S 0.5.1.</summary>
    public bool GesteAltFuerTests { set => _geste = new Fingergeste(altFuerTests: value); }

    public GrundrissFlaeche()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Stueck, das gerade gewaehlt ist (Feld rechts zeigt es).</summary>
    public Stelle? Auswahl { get; private set; }

    /// <summary>Typ aus dem Katalog, der beim naechsten Tippen gesetzt wird (null = waehlen und schieben).</summary>
    public string? SetzenTyp { get; set; }

    /// <summary>Auf dem Raster 0,5 schieben (aus: 0,1).</summary>
    public bool Raster { get; set; } = true;

    /// <summary>Funde der letzten Pruefung (mit Stelle als Markierung im Grundriss).</summary>
    public IReadOnlyList<StadtbauFund> Funde { get; set; } = [];

    public event Action? AuswahlGeaendert;

    /// <summary>Text fuer Screenreader und Statuszeile nach einer Aenderung.</summary>
    public event Action<string>? Gemeldet;

    public double Zoom => _zoom;

    /// <summary>Lage der Ansicht (Start-Test: Schieben).</summary>
    public Point Versatz => _versatz;

    /// <summary>Liegt an dieser Stelle des Bilds ein Stueck? (Start-Test der Finger)</summary>
    public bool StueckAnFuerTests(Point bild) => Gebaeude is { } g && Bearbeitung.StueckBei(g, Welt(bild).X, Welt(bild).Y) is not null;

    public void Zeigen(Projekt? projekt, string? ort)
    {
        var anderer = !ReferenceEquals(projekt, _projekt) || ort != _ort;
        _projekt = projekt;
        _ort = ort;
        if (anderer)
        {
            Waehlen(null);
            _eingepasst = false;
            Einpassen();
        }
        InvalidateVisual();
    }

    private PyObjekt? Gebaeude => _projekt is not null && _ort is not null ? _projekt.Grundriss(_ort) : null;

    public void Waehlen(Stelle? stelle)
    {
        Auswahl = stelle;
        AuswahlGeaendert?.Invoke();
        InvalidateVisual();
    }

    // ---- Ansicht ---------------------------------------------------------------------------------

    public void Einpassen()
    {
        if (Gebaeude is not { } g || Bounds.Width < 10 || Bounds.Height < 10)
        {
            return;
        }
        double b = Py.F(g["breite"]), h = Py.F(g["hoehe"]);
        const double rand = 24;
        _zoom = Math.Max(4, Math.Min((Bounds.Width - (2 * rand)) / b, (Bounds.Height - (2 * rand)) / h));
        _versatz = new Point((Bounds.Width - (b * _zoom)) / 2, (Bounds.Height - (h * _zoom)) / 2);
        _eingepasst = true;
        InvalidateVisual();
    }

    public void Vergroessern() => ZoomUm(new Point(Bounds.Width / 2, Bounds.Height / 2), 1.25);

    public void Verkleinern() => ZoomUm(new Point(Bounds.Width / 2, Bounds.Height / 2), 0.8);

    private void ZoomUm(Point mitte, double faktor)
    {
        var neu = Math.Clamp(_zoom * faktor, 4, 400);
        var welt = Welt(mitte);
        _zoom = neu;
        _versatz = new Point(mitte.X - (welt.X * _zoom), mitte.Y - (welt.Y * _zoom));
        InvalidateVisual();
    }

    private Point Welt(Point bild) => new((bild.X - _versatz.X) / _zoom, (bild.Y - _versatz.Y) / _zoom);

    private Rect Bild(double x, double y, double w, double h) => new(_versatz.X + (x * _zoom), _versatz.Y + (y * _zoom), w * _zoom, h * _zoom);

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_eingepasst || e.PreviousSize.Width < 10)
        {
            Einpassen();
        }
    }

    // ---- Zeichnen --------------------------------------------------------------------------------

    private static IBrush Farbe(string name) => new SolidColorBrush(Color.Parse(FarbenAnwenden.Aktuell.Farben[name]));

    private static readonly Dictionary<string, string> BodenFarben = new(StringComparer.Ordinal)
    {
        ["parkett"] = "#C9A878",
        ["teppich"] = "#8E9DB4",
        ["fliesen"] = "#D9D7CF",
        ["beton"] = "#A9A9A4",
    };

    private static readonly Dictionary<string, string> GruppenFarben = new(StringComparer.Ordinal)
    {
        ["Regale"] = "#7B5B3A",
        ["Tische"] = "#9C6B3F",
        ["Sitzen"] = "#4F7FA8",
        ["Geräte"] = "#4A4F57",
        ["Wohnen"] = "#8A5C8E",
        ["Deko"] = "#4E8A4E",
        ["Wand"] = "#6D6D6D",
        ["Boden"] = "#B08D57",
        ["Sonstiges"] = "#7A7A7A",
    };

    private readonly List<string> _effektFormen = [];

    /// <summary>Formen der Effekte beim letzten Zeichnen (Start-Test: gleich der Draufsicht, Weisung P12-25).</summary>
    public IReadOnlyList<string> EffektFormen => _effektFormen;

    public override void Render(DrawingContext ctx)
    {
        _effektFormen.Clear();
        ctx.FillRectangle(Farbe("bg"), new Rect(Bounds.Size));
        if (Gebaeude is not { } g)
        {
            return;
        }
        var schrift = new Typeface(FontFamily.Default);
        double breite = Py.F(g["breite"]), hoehe = Py.F(g["hoehe"]);
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#2B2B2B")), Bild(-0.15, -0.15, breite + 0.3, hoehe + 0.3));

        // Boeden: Flur, dann die Raeume (Namen erst nach der Einrichtung, Sammelliste 219 Punkt 1)
        var bereiche = Bearbeitung.Bereiche(g);
        foreach (var (bereich, _) in bereiche)
        {
            var (x, y, w, h) = Bearbeitung.Flaeche(g, bereich);
            var b = Bearbeitung.Bereich(g, bereich);
            var boden = b["boden"] is string s && BodenFarben.TryGetValue(s, out var f) ? f : "#D0CCC0";
            ctx.FillRectangle(new SolidColorBrush(Color.Parse(boden)), Bild(x, y, w, h));
        }

        // Waende mit Tueroeffnungen (wie das Spiel sie baut)
        var wand = new SolidColorBrush(Color.Parse("#3A3A3A"));
        foreach (var (x1, y1, x2, y2) in Wege.Wandstuecke(g))
        {
            ctx.FillRectangle(wand, Bild(x1, y1, x2 - x1, y2 - y1));
        }

        // Einrichtung
        var rand = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1);
        foreach (var (stelle, d) in Bearbeitung.Alle(g))
        {
            if (ZiehtStueck && stelle == Auswahl)
            {
                continue;
            }
            StueckZeichnen(ctx, schrift, d, rand, 0, 0, 1);
        }

        // Personen
        if (_projekt is not null && _ort is not null)
        {
            foreach (var p in _projekt.Personen(_ort))
            {
                if (Get(p, "platz") is List<object?> pl && pl.Count >= 2)
                {
                    var m = Bild(Py.F(pl[0]), Py.F(pl[1]), 0, 0).TopLeft;
                    ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#E07A3F")), new Pen(Brushes.White, 2), m, 0.28 * _zoom, 0.28 * _zoom);
                    Schreiben(ctx, schrift, Get(p, "name") as string ?? "", m + new Point(0.32 * _zoom, -8), 11, Brushes.Black, 0.7);
                }
            }
        }

        // Namen der Raeume ueber der oberen Wand auf einem Schild, zuletzt gezeichnet: kein Stueck verdeckt sie
        foreach (var (bereich, name) in bereiche.Where(b => b.Name.Length > 0))
        {
            NamenSchild(ctx, schrift, name, Bearbeitung.NamenLage(g, bereich));
        }

        // Ausgewaehltes Stueck (beim Ziehen an der neuen Stelle)
        if (Auswahl is { } a && Gueltig(g, a))
        {
            var d = Bearbeitung.Deko(g, a);
            var (dx, dy) = ZiehtStueck ? (_ziehWelt.X, _ziehWelt.Y) : (0, 0);
            if (ZiehtStueck)
            {
                StueckZeichnen(ctx, schrift, d, rand, dx, dy, 0.85);
            }
            var r = Bild(Py.F(d["x"]) + dx, Py.F(d["y"]) + dy, Py.F(d["w"]), Py.F(d["h"]));
            ctx.DrawRectangle(null, new Pen(Farbe("accent"), 3), r.Inflate(3));
        }

        // Funde mit Stelle: rote und gelbe Ringe
        foreach (var f in Funde)
        {
            if (f.X is { } fx && f.Y is { } fy)
            {
                var m = Bild(fx, fy, 0, 0).TopLeft;
                var stift = new Pen(new SolidColorBrush(Color.Parse(f.Rot ? "#E53935" : "#F9A825")), 3);
                ctx.DrawEllipse(null, stift, m, Math.Max(10, 0.45 * _zoom), Math.Max(10, 0.45 * _zoom));
            }
        }
    }

    private void StueckZeichnen(DrawingContext ctx, Typeface schrift, PyObjekt d, Pen rand, double dx, double dy, double deckkraft)
    {
        var typ = Py.Str(d["typ"]);
        var teil = Bearbeitung.Teil(typ);
        var farbe = Color.Parse(teil is not null && GruppenFarben.TryGetValue(teil.Gruppe, out var f) ? f : "#7A7A7A");
        var r = Bild(Py.F(d["x"]) + dx, Py.F(d["y"]) + dy, Py.F(d["w"]), Py.F(d["h"]));
        using (ctx.PushOpacity(deckkraft))
        {
            if (Katalog.Aktiv?.Eintrag(typ) is { Art: KatalogArt.Effekt } effekt)
            {
                // Effekt "Nur Aussehen" (Antwort P12-23 3a): dasselbe Zeichen wie in der Draufsicht (Weisung P12-25)
                var radius = Math.Max(7, Math.Min(r.Width, r.Height) / 2);
                _effektFormen.Add(EffektZeichen.Zeichnen(ctx, r.Center, radius, effekt.Farbe));
                return;
            }
            ctx.DrawRectangle(new SolidColorBrush(farbe, 0.85), rand, r);
            if (Py.Hat(d, "ware"))
            {
                ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#FFD54F")), new Pen(Brushes.Black, 1), r.TopRight + new Point(-6, 6), 4, 4);
            }
            // Phase 13 Welle 3b: Stueck aus dem vollen Katalog mit seinem Vorschaubild
            if (Katalog.IstKatalogTyp(typ) && KatalogBilder.Feld(typ) is { } bild && r.Width > 8 && r.Height > 8)
            {
                var seite = Math.Min(r.Width, r.Height);
                ctx.DrawImage(bild.Bild, bild.Quelle, new Rect(r.Center.X - (seite / 2), r.Center.Y - (seite / 2), seite, seite));
            }
            if (r.Width > 46 && r.Height > 14)
            {
                Schreiben(ctx, schrift, Bearbeitung.TeilName(typ), r.TopLeft + new Point(3, 1), 10, Brushes.White, 1, r.Width - 6);
            }
        }
    }

    /// <summary>Groesse der Raumnamen in Bildpunkten.</summary>
    public const double NamenGroesse = 12;

    /// <summary>Lage des Namensschilds eines Bereichs im Bild (fuer Zeichnen und Start-Test).</summary>
    public Rect NamenRechteck(string name, (double X, double Unterkante) lage)
    {
        var ft = new FormattedText(name, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), NamenGroesse, Brushes.Black);
        var unten = Bild(lage.X, lage.Unterkante, 0, 0).TopLeft;
        return new Rect(unten.X - 3, unten.Y - ft.Height - 2, ft.Width + 6, ft.Height + 2);
    }

    private void NamenSchild(DrawingContext ctx, Typeface schrift, string name, (double X, double Unterkante) lage)
    {
        var r = NamenRechteck(name, lage);
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse(FarbenAnwenden.Aktuell.Farben["card"]), 0.92), new Pen(Farbe("border"), 1), r, 4, 4);
        Schreiben(ctx, schrift, name, r.TopLeft + new Point(3, 1), NamenGroesse, Farbe("text"), 1);
    }

    private static void Schreiben(DrawingContext ctx, Typeface schrift, string text, Point ort, double groesse, IBrush pinsel, double deckkraft, double? breite = null)
    {
        var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, groesse, pinsel);
        if (breite is { } b)
        {
            ft.MaxTextWidth = Math.Max(1, b);
            ft.MaxLineCount = 1;
            ft.Trimming = TextTrimming.CharacterEllipsis;
        }
        using (ctx.PushOpacity(deckkraft))
        {
            ctx.DrawText(ft, ort);
        }
    }

    private static bool Gueltig(PyObjekt g, Stelle s)
    {
        try
        {
            _ = Bearbeitung.Deko(g, s);
            return true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidCastException or NullReferenceException)
        {
            return false;
        }
    }

    // ---- Bedienung -------------------------------------------------------------------------------

    private bool _nurSchieben;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (Gebaeude is not { } g || _projekt is null || _ort is null)
        {
            return;
        }
        var p = e.GetPosition(this);
        var w = Welt(p);
        if (!_geste.Aktiv && KartenFlaeche.NurSchiebenDruck(e, this))
        {
            // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 2): wie in der Draufsicht nur die Ansicht schieben
            _nurSchieben = true;
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, false));
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        var treffer = SetzenTyp is null ? Bearbeitung.StueckBei(g, w.X, w.Y) : null;
        var erster = !_geste.Aktiv;
        Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, treffer is not null));
        // der erste Finger waehlt sofort (wie bisher); ein zweiter Finger aendert die Auswahl nie
        if (erster && SetzenTyp is null && treffer != Auswahl)
        {
            Waehlen(treffer);
        }
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        Anwenden(_geste.Bewegen(e.Pointer.Id, p.X, p.Y));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        // zuerst auswerten, dann die Erfassung freigeben: Capture(null) loest sofort PointerCaptureLost aus, und
        // "Verloren" haette den Finger schon vergessen (Tippen setzte nichts, Ziehen schob nichts)
        Anwenden(_geste.Loslassen(e.Pointer.Id));
        e.Pointer.Capture(null);
        if (!_geste.Aktiv)
        {
            _nurSchieben = false;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Anwenden(_geste.Verloren(e.Pointer.Id));
    }

    /// <summary>Ergebnis der Fingerauswertung umsetzen (Setzen erst beim Loslassen, damit zwei Finger nichts setzen).</summary>
    private void Anwenden(Fingergeste.Schritt s)
    {
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when SetzenTyp is not null && !_nurSchieben:
                var w = Welt(new Point(s.X, s.Y));
                SetzenAn(w.X, w.Y);
                break;
            case Fingergeste.Art.StueckZiehen:
                _ziehWelt = new Vector(s.Dx / _zoom, s.Dy / _zoom);
                _ziehen = true;
                InvalidateVisual();
                break;
            case Fingergeste.Art.StueckAbbrechen:
                _ziehWelt = default;
                _ziehen = false;
                InvalidateVisual();
                break;
            case Fingergeste.Art.StueckFertig when Auswahl is { } a && _projekt is not null && _ort is not null && !_projekt.NurLesen:
                var (dx, dy) = (s.Dx / _zoom, s.Dy / _zoom);
                _ziehWelt = default;
                _ziehen = false;
                var neu = _projekt.Aendern(_ort, "Verschieben", g => Bearbeitung.Verschieben(g, a, dx, dy, Raster));
                Waehlen(neu);
                Gemeldet?.Invoke("Verschoben");
                break;
            case Fingergeste.Art.AnsichtSchieben:
                _versatz += new Vector(s.Dx, s.Dy);
                InvalidateVisual();
                break;
            case Fingergeste.Art.Zoomen:
                _versatz += new Vector(s.Dx, s.Dy);
                ZoomUm(new Point(s.X, s.Y), s.Faktor);
                break;
            default:
                if (s.Art == Fingergeste.Art.StueckFertig)
                {
                    _ziehWelt = default;
                    _ziehen = false;
                    InvalidateVisual();
                }
                break;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomUm(e.GetPosition(this), e.Delta.Y > 0 ? 1.15 : 1 / 1.15);
        e.Handled = true;
    }

    /// <summary>Tasten (vom Fenster weitergereicht): Pfeile schieben, R dreht, Entf loescht, Strg+D kopiert.</summary>
    public bool Taste(Key taste, KeyModifiers mod)
    {
        if (_projekt is null || _ort is null || Auswahl is not { } a || _projekt.NurLesen)
        {
            return false;
        }
        var schritt = Raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        switch (taste)
        {
            case Key.Left or Key.Right or Key.Up or Key.Down when mod == KeyModifiers.None:
                var (dx, dy) = taste switch
                {
                    Key.Left => (-schritt, 0.0),
                    Key.Right => (schritt, 0.0),
                    Key.Up => (0.0, -schritt),
                    _ => (0.0, schritt),
                };
                Waehlen(_projekt.Aendern(_ort, "Verschieben", g => Bearbeitung.Verschieben(g, a, dx, dy, Raster)));
                return true;
            case Key.R when mod == KeyModifiers.None:
                Drehen();
                return true;
            case Key.Delete or Key.Back when mod == KeyModifiers.None:
                Loeschen();
                return true;
            case Key.D when mod == KeyModifiers.Control:
                Kopieren();
                return true;
            case Key.Escape:
                Waehlen(null);
                return true;
            default:
                return false;
        }
    }

    public void Drehen()
    {
        if (_projekt is not null && _ort is not null && Auswahl is { } a && !_projekt.NurLesen)
        {
            _projekt.Aendern(_ort, "Drehen", g => Bearbeitung.Drehen(g, a));
            Waehlen(a);
            Gemeldet?.Invoke("Gedreht");
        }
    }

    public void Kopieren()
    {
        if (_projekt is not null && _ort is not null && Auswahl is { } a && !_projekt.NurLesen)
        {
            Waehlen(_projekt.Aendern(_ort, "Kopie", g => Bearbeitung.Kopieren(g, a)));
            Gemeldet?.Invoke("Kopie angelegt");
        }
    }

    public void Loeschen()
    {
        if (_projekt is not null && _ort is not null && Auswahl is { } a && !_projekt.NurLesen)
        {
            _projekt.Aendern(_ort, "Löschen", g => Bearbeitung.Loeschen(g, a));
            Waehlen(null);
            Gemeldet?.Invoke("Gelöscht");
        }
    }

    /// <summary>Das Stueck aus dem Katalog (<see cref="SetzenTyp"/>) mit der Mitte bei (x, y) setzen und waehlen.</summary>
    public Stelle? SetzenAn(double x, double y)
    {
        if (_projekt is null || _ort is null || SetzenTyp is not { } typ || _projekt.NurLesen)
        {
            return null;
        }
        var neu = _projekt.Aendern(_ort, "Setzen", g => Bearbeitung.Setzen(g, typ, x, y, Raster));
        SetzenTyp = null;
        if (neu is not null)
        {
            Waehlen(neu);
            Gemeldet?.Invoke(Bearbeitung.TeilName(typ) + " gesetzt");
        }
        return neu;
    }

    /// <summary>Fuer Tests und Start-Test: Stueck an einer Weltstelle antippen wie mit dem Finger.</summary>
    public Point BildPunkt(double x, double y) => new(_versatz.X + (x * _zoom), _versatz.Y + (y * _zoom));
}
