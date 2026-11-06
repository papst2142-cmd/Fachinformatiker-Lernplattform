using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18, Plan Abschnitt 3): Draufsicht der Stadt draussen zum Bearbeiten. Zeichnet Parks,
/// Fluss, Strassen mit Gehweg, Bahn, Haeuser (Orte und Gebaeude ohne Ort) mit Tuer und Fussweg, Baeume, Beete, Laternen
/// und den Paketplatz aus dem Stand des Projekts. Antippen waehlt ein Stueck, Ziehen verschiebt es (Raster 0,5), ein
/// Stueck aus dem Katalog wird mit einem Tipp gesetzt; Tasten: Pfeile schieben, R dreht, Entf loescht, Strg+D kopiert,
/// Esc hebt die Auswahl auf. Ein Doppeltipp auf ein Haus mit Innenraum oeffnet den Innenraum. Rote und gelbe Ringe
/// zeigen die Funde der Pruefung. Jede Aenderung laeuft ueber <see cref="Projekt.AussenAendern{T}"/> (Verlauf).
/// </summary>
public sealed class KartenFlaeche : Control
{
    private Projekt? _projekt;
    private double _zoom = 20;
    private Point _versatz;
    private bool _eingepasst;
    private Point? _druck;
    private Point _ziehWelt;
    private bool _gezogen;

    public KartenFlaeche()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    /// <summary>Doppeltipp auf ein Haus mit Innenraum.</summary>
    public event Action<string>? OrtGewaehlt;

    public event Action? AuswahlGeaendert;

    public event Action<string>? Gemeldet;

    public AussenStelle? Auswahl { get; private set; }

    /// <summary>Stueck aus dem Katalog, das beim naechsten Tipp gesetzt wird (null = waehlen).</summary>
    public AussenArt? SetzenArt { get; set; }

    /// <summary>Modell fuer ein neues Haus ohne Ort.</summary>
    public string? SetzenModell { get; set; }

    /// <summary>Phase 13 Welle 4c: der Blueprint, der beim naechsten Tippen gesetzt wird (mit <see cref="AussenArt.Blueprint"/>).</summary>
    public Blueprint? SetzenBlueprint { get; set; }

    /// <summary>Raster 0,5 (aus: 0,1 fuer Gruen, Laternen und Paketplatz).</summary>
    public bool Raster { get; set; } = true;

    public List<StadtbauFund> Funde { get; set; } = [];

    public Projekt? Projekt => _projekt;

    public void Zeigen(Projekt? projekt)
    {
        if (!ReferenceEquals(_projekt, projekt))
        {
            Auswahl = null;
            _eingepasst = false;
        }
        _projekt = projekt;
        Einpassen();
        InvalidateVisual();
    }

    private PyObjekt? Stand => _projekt?.AussenStand;

    private (double B, double H) Welt() => Stand is { } a ? Aussen.Groesse(a) : (32, 24);

    public void Einpassen()
    {
        if (Bounds.Width < 10)
        {
            return;
        }
        var (b, h) = Welt();
        _zoom = Math.Max(4, Math.Min((Bounds.Width - 24) / b, (Bounds.Height - 24) / h));
        _versatz = new Point((Bounds.Width - (b * _zoom)) / 2, (Bounds.Height - (h * _zoom)) / 2);
        _eingepasst = true;
        InvalidateVisual();
    }

    public void Vergroessern() => ZoomUm(new Point(Bounds.Width / 2, Bounds.Height / 2), 1.25);

    public void Verkleinern() => ZoomUm(new Point(Bounds.Width / 2, Bounds.Height / 2), 0.8);

    private void ZoomUm(Point mitte, double faktor)
    {
        var welt = WeltPunkt(mitte);
        _zoom = Math.Clamp(_zoom * faktor, 4, 200);
        _versatz = new Point(mitte.X - (welt.X * _zoom), mitte.Y - (welt.Y * _zoom));
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_eingepasst || e.PreviousSize.Width < 10)
        {
            Einpassen();
        }
    }

    private Point P(double x, double y) => new(_versatz.X + (x * _zoom), _versatz.Y + (y * _zoom));

    /// <summary>Rand der Karte in Bildpunkten beim letzten Zeichnen (Start-Test).</summary>
    public Rect KartenRand { get; private set; }

    /// <summary>Beschriftungen (Ortsnamen) mit ihrem Rechteck beim letzten Zeichnen (Start-Test: nie ueber den Kartenrand).</summary>
    public IReadOnlyList<(string Text, Rect Flaeche)> Beschriftungen { get; private set; } = [];

    /// <summary>Formen beim letzten Zeichnen: Laterne (Effekt false) und je Effekt seine Form (Start-Test, Weisung P12-25).</summary>
    public IReadOnlyList<(string Form, bool Effekt)> Formen { get; private set; } = [];

    private Point WeltPunkt(Point bild) => new((bild.X - _versatz.X) / _zoom, (bild.Y - _versatz.Y) / _zoom);

    /// <summary>Bildpunkt eines Weltpunkts (Start-Test).</summary>
    public Point Bildpunkt(double x, double y) => P(x, y);

    private string OrtName(string id) => _projekt?.Daten.OrtName(id) ?? id;

    // ---- Zeichnen --------------------------------------------------------------------------------

    private static IBrush Pinsel(string hex, double deckkraft = 1) => new SolidColorBrush(Color.Parse(hex), deckkraft);

    private static IBrush Farbe(string name) => new SolidColorBrush(Color.Parse(FarbenAnwenden.Aktuell.Farben[name]));

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Farbe("bg"), new Rect(Bounds.Size));
        if (Stand is not { } a)
        {
            return;
        }
        var (b, h) = Welt();
        var schrift = new Typeface(FontFamily.Default);
        ctx.FillRectangle(Pinsel("#B9D29A"), new Rect(P(0, 0), P(b, h)));
        var land = Aussen.Landschaft(a);
        var (dx, dy) = _gezogen && Auswahl is not null ? (_ziehWelt.X, _ziehWelt.Y) : (0, 0);
        double Dx(AussenStelle s) => s == Auswahl ? dx : 0;
        double Dy(AussenStelle s) => s == Auswahl ? dy : 0;
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is not PyObjekt l)
            {
                continue;
            }
            switch (l["typ"] as string)
            {
                case "park":
                {
                    var s = new AussenStelle(AussenArt.Park, i);
                    ctx.FillRectangle(Pinsel("#8DBA6A"), new Rect(P(Py.F(l["x"]) + Dx(s), Py.F(l["y"]) + Dy(s)), P(Py.F(l["x"]) + Py.F(l["w"]) + Dx(s), Py.F(l["y"]) + Py.F(l["h"]) + Dy(s))));
                    break;
                }
                case "fluss":
                    Linie(ctx, l["punkte"], new Pen(Pinsel("#5B9BD5"), 0.9 * _zoom, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), 0, 0);
                    break;
            }
        }
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is PyObjekt l && Py.Gleich(l["typ"], "strasse"))
            {
                var s = new AussenStelle(AussenArt.Strasse, i);
                var gehweg = AussenBearbeitung.Gehweg(a, s);
                if (gehweg > 0)
                {
                    Linie(ctx, l["punkte"], new Pen(Pinsel("#D8D4CA"), (StadtSzene.StrassenBreite + (2 * gehweg)) * _zoom, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Round), Dx(s), Dy(s));
                }
                Linie(ctx, l["punkte"], new Pen(Pinsel("#6B6B6B"), StadtSzene.StrassenBreite * _zoom, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Dx(s), Dy(s));
            }
        }
        foreach (var l in land.OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "bahn")))
        {
            Linie(ctx, l["punkte"], new Pen(Pinsel("#5A4632"), 0.25 * _zoom, new DashStyle([2, 1], 0)), 0, 0);
        }

        // Haeuser mit Fussweg und Tuer
        var bearbeitbar = new HashSet<string>(_projekt!.Orte, StringComparer.Ordinal);
        foreach (var (s, _, _) in AussenBearbeitung.Alle(a).Where(x => x.Stelle.Art is AussenArt.Haus or AussenArt.Gebaeude).ToList())
        {
            var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(a, s);
            var r = new Rect(P(hx + Dx(s), hy + Dy(s)), P(hx + hw + Dx(s), hy + hh + Dy(s)));
            var mitInnen = s.Art == AussenArt.Haus && bearbeitbar.Contains(s.Ort);
            if (s.Art == AussenArt.Haus && AussenBearbeitung.Zugang(a, s.Ort) is { } z)
            {
                Linie(ctx, z["weg"], new Pen(Pinsel("#EFEBE2"), StadtSzene.WegBreite * _zoom, lineCap: PenLineCap.Flat), Dx(s), Dy(s));
                if (z["tuer"] is List<object?> t && t.Count >= 2)
                {
                    ctx.DrawEllipse(Pinsel("#7B4B24"), null, P(Py.F(t[0]) + Dx(s), Py.F(t[1]) + Dy(s)), 0.14 * _zoom, 0.14 * _zoom);
                }
            }
            ctx.DrawRectangle(Pinsel(s.Art == AussenArt.Haus ? "#C9B79C" : "#A8A49C"), new Pen(mitInnen ? Farbe("accent") : Pinsel("#5C554B"), mitInnen ? 3 : 1), r);
            var name = s.Art == AussenArt.Haus ? OrtName(s.Ort) : Py.Str(Objekt(land[s.Nummer])["modell"]);
            if (r.Width > 30)
            {
                Text(ctx, schrift, name, r.TopLeft + new Point(3, 2), 11, Brushes.Black, r.Width - 6);
            }
        }

        // Gruen, Laternen, Paketplatz
        foreach (var (s, x, y) in AussenBearbeitung.Alle(a).Where(x => x.Stelle.Art is AussenArt.Baum or AussenArt.Beet or AussenArt.Laterne or AussenArt.Paketplatz).ToList())
        {
            var m = P(x + Dx(s), y + Dy(s));
            switch (s.Art)
            {
                case AussenArt.Baum:
                    ctx.DrawEllipse(Pinsel("#3F7D3A", 0.85), new Pen(Pinsel("#2A5527"), 1), m, 0.45 * _zoom, 0.45 * _zoom);
                    break;
                case AussenArt.Beet:
                    ctx.DrawRectangle(Pinsel("#9C6B3F"), new Pen(Pinsel("#C2457A"), 2), new Rect(m.X - (0.3 * _zoom), m.Y - (0.2 * _zoom), 0.6 * _zoom, 0.4 * _zoom));
                    break;
                case AussenArt.Laterne:
                    var doppelt = Liste(Aussen.Laternen(a)[s.Nummer]) is { Count: > 2 } lp && lp[2] is true;
                    ctx.DrawEllipse(Pinsel("#FFD54F"), new Pen(Brushes.Black, 1), m, Math.Max(3, 0.12 * _zoom), Math.Max(3, 0.12 * _zoom));
                    if (doppelt)
                    {
                        ctx.DrawEllipse(null, new Pen(Pinsel("#FFD54F"), 2), m, Math.Max(6, 0.22 * _zoom), Math.Max(6, 0.22 * _zoom));
                    }
                    break;
                case AussenArt.Paketplatz:
                    var k = Math.Max(5, Lieferpaket.Breite * 1.6 * _zoom);
                    ctx.DrawRectangle(Pinsel("#B98A4F"), new Pen(Brushes.Black, 1), new Rect(m.X - k, m.Y - k, 2 * k, 2 * k));
                    break;
            }
        }
        // Katalog-Stuecke (Phase 13 Welle 3b): Rechteck mit Vorschaubild, Effekt als leuchtendes Zeichen
        var formen = new List<(string Form, bool Effekt)>();
        if (AussenBearbeitung.Alle(a).Any(x => x.Stelle.Art == AussenArt.Laterne))
        {
            formen.Add((EffektZeichen.Punkt, false));
        }
        foreach (var (s, _, _) in AussenBearbeitung.Alle(a).Where(x => x.Stelle.Art == AussenArt.Katalog).ToList())
        {
            var (kx, ky, kw, kh) = AussenBearbeitung.KatalogFlaeche(a, s);
            var r = new Rect(P(kx + Dx(s), ky + Dy(s)), P(kx + kw + Dx(s), ky + kh + Dy(s)));
            var typ = Katalog.Praefix + AussenBearbeitung.KatalogId(a, s);
            var eintrag = Katalog.Aktiv?.Eintrag(typ);
            if (eintrag is { Art: KatalogArt.Effekt })
            {
                // eigenes Zeichen (Stern mit Leuchtrand), nie wie eine Laterne (Weisung P12-25)
                var radius = Math.Max(7, Math.Min(r.Width, r.Height) / 2);
                formen.Add((EffektZeichen.Zeichnen(ctx, r.Center, radius, eintrag.Farbe), true));
                continue;
            }
            ctx.DrawRectangle(Pinsel("#E8E2D6", 0.85), new Pen(eintrag is null ? Pinsel("#C62828") : Pinsel("#5C554B"), 1), r);
            if (KatalogBilder.Feld(typ) is { } bild && r.Width > 6 && r.Height > 6)
            {
                var seite = Math.Min(r.Width, r.Height);
                ctx.DrawImage(bild.Bild, bild.Quelle, new Rect(r.Center.X - (seite / 2), r.Center.Y - (seite / 2), seite, seite));
            }
        }

        // Beschriftungen bleiben ganz in der Karte (Fund Chat zu S 0.2.0: "TALHEIM" ragte am Tablet ueber den Rand)
        var rand = new Rect(P(0, 0), P(b, h));
        KartenRand = rand;
        var beschriftungen = new List<(string, Rect)>();
        foreach (var l in land.OfType<PyObjekt>().Where(l => l["typ"] as string is "ortsname" or "beschriftung"))
        {
            var text = l["text"] as string ?? "";
            var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, 12, Brushes.Black);
            var p = P(Py.F(l["x"]), Py.F(l["y"]));
            var (bx, by, bb) = Beschriftung.InKarte(p.X, p.Y, ft.Width, ft.Height, rand.Left, rand.Top, rand.Right, rand.Bottom);
            Text(ctx, schrift, text, new Point(bx, by), 12, Brushes.Black, bb < ft.Width ? bb : null);
            beschriftungen.Add((text, new Rect(bx, by, Math.Min(bb, ft.Width), ft.Height)));
        }
        Beschriftungen = beschriftungen;
        Formen = formen;

        // Auswahl
        if (Auswahl is { } aw && AussenBearbeitung.Gueltig(a, aw))
        {
            var stift = new Pen(Farbe("accent"), 3);
            switch (aw.Art)
            {
                case AussenArt.Strasse:
                    Linie(ctx, Objekt(land[aw.Nummer])["punkte"], new Pen(Farbe("accent"), 3), dx, dy);
                    break;
                case AussenArt.Haus or AussenArt.Gebaeude:
                    var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(a, aw);
                    ctx.DrawRectangle(null, stift, new Rect(P(hx + dx, hy + dy), P(hx + hw + dx, hy + hh + dy)).Inflate(3));
                    break;
                case AussenArt.Katalog:
                    var (kx, ky, kw, kh) = AussenBearbeitung.KatalogFlaeche(a, aw);
                    ctx.DrawRectangle(null, stift, new Rect(P(kx + dx, ky + dy), P(kx + kw + dx, ky + kh + dy)).Inflate(3));
                    break;
                case AussenArt.Park:
                    var l = Objekt(land[aw.Nummer]);
                    ctx.DrawRectangle(null, stift, new Rect(P(Py.F(l["x"]) + dx, Py.F(l["y"]) + dy), P(Py.F(l["x"]) + Py.F(l["w"]) + dx, Py.F(l["y"]) + Py.F(l["h"]) + dy)).Inflate(3));
                    break;
                default:
                    var (x, y) = AussenBearbeitung.Alle(a).First(z => z.Stelle == aw) is var t ? (t.X, t.Y) : (0, 0);
                    ctx.DrawEllipse(null, stift, P(x + dx, y + dy), Math.Max(9, 0.5 * _zoom), Math.Max(9, 0.5 * _zoom));
                    break;
            }
        }

        // Funde mit Stelle
        foreach (var f in Funde)
        {
            if (f.X is { } fx && f.Y is { } fy)
            {
                var stift = new Pen(Pinsel(f.Rot ? "#E53935" : "#F9A825"), 3);
                ctx.DrawEllipse(null, stift, P(fx, fy), Math.Max(10, 0.45 * _zoom), Math.Max(10, 0.45 * _zoom));
            }
        }
    }

    private void Linie(DrawingContext ctx, object? punkte, Pen stift, double dx, double dy)
    {
        if (punkte is not List<object?> liste || liste.Count < 2)
        {
            return;
        }
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            var erster = true;
            foreach (var p in liste.OfType<List<object?>>())
            {
                var q = P(Py.F(p[0]) + dx, Py.F(p[1]) + dy);
                if (erster)
                {
                    g.BeginFigure(q, false);
                    erster = false;
                }
                else
                {
                    g.LineTo(q);
                }
            }
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, stift, geo);
    }

    private static void Text(DrawingContext ctx, Typeface schrift, string text, Point ort, double groesse, IBrush pinsel, double? breite)
    {
        var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, schrift, groesse, pinsel);
        if (breite is { } b)
        {
            ft.MaxTextWidth = Math.Max(1, b);
            ft.MaxLineCount = 1;
            ft.Trimming = TextTrimming.CharacterEllipsis;
        }
        ctx.DrawText(ft, ort);
    }

    // ---- Bedienen --------------------------------------------------------------------------------

    public void Waehlen(AussenStelle? s)
    {
        Auswahl = s;
        AuswahlGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Stueck aus dem Katalog an diese Stelle setzen (Start-Test und Tipp).</summary>
    public AussenStelle? SetzenAn(double x, double y)
    {
        if (_projekt is null || _projekt.NurLesen || SetzenArt is not { } art)
        {
            return null;
        }
        var modell = SetzenModell;
        var raster = Raster;
        if (art == AussenArt.Blueprint)
        {
            return BlueprintSetzen(x, y);
        }
        var neu = _projekt.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, art, x, y, raster, modell));
        SetzenArt = null;
        if (neu is not null)
        {
            Gemeldet?.Invoke((art == AussenArt.Katalog ? KatalogPruefung.Name(Katalog.Aktiv, Katalog.Praefix + SetzenModell) : ArtName(art)) + " gesetzt");
        }
        Waehlen(neu);
        return neu;
    }

    /// <summary>Phase 13 Welle 4c: den gewaehlten Blueprint setzen (ein Schritt im Verlauf, danach ein normales Gebaeude).</summary>
    private AussenStelle? BlueprintSetzen(double x, double y)
    {
        var b = SetzenBlueprint;
        SetzenArt = null;
        SetzenBlueprint = null;
        if (b is null || _projekt is null)
        {
            return null;
        }
        string? grund = null;
        var neu = _projekt.AussenAendern(Texte.BlueprintSetzen + " „" + b.Name + "“", a => b.Setzen(a, x, y, out grund));
        Gemeldet?.Invoke(neu is null ? grund ?? Texte.BlueprintAusserhalb : Texte.BlueprintGesetzt + ": „" + b.Name + "“");
        Waehlen(neu);
        return neu;
    }

    private static string ArtName(AussenArt art) => art switch
    {
        AussenArt.Strasse => Texte.Strasse,
        AussenArt.Haus => Texte.Haus,
        AussenArt.Gebaeude => Texte.HausOhneOrt,
        AussenArt.Baum => Texte.Baum,
        AussenArt.Park => Texte.Park,
        AussenArt.Beet => Texte.Beet,
        AussenArt.Laterne => Texte.Laterne,
        AussenArt.Katalog => Texte.VollerKatalog,
        AussenArt.Blueprint => Texte.BlueprintSetzen,
        _ => Texte.Paketplatz,
    };

    /// <summary>Ausgewaehltes Stueck um (dx, dy) verschieben (Verlauf, automatisch gespeichert).</summary>
    public void Verschieben(double dx, double dy)
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s)
        {
            return;
        }
        var raster = Raster;
        _projekt.AussenAendern("Verschieben", a => AussenBearbeitung.Verschieben(a, s, dx, dy, raster));
        Gemeldet?.Invoke(ArtName(s.Art) + " verschoben");
        Waehlen(AussenBearbeitung.Gueltig(_projekt.AussenStand, s) ? s : null);
    }

    public void Drehen()
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s)
        {
            return;
        }
        if (AussenBearbeitung.DrehenNicht(s) is { } grund)
        {
            Gemeldet?.Invoke(grund);
            return;
        }
        _projekt.AussenAendern("Drehen", a => AussenBearbeitung.Drehen(a, s));
        Gemeldet?.Invoke(ArtName(s.Art) + " gedreht");
        Waehlen(s);
    }

    public void Kopieren()
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s)
        {
            return;
        }
        var neu = _projekt.AussenAendern("Kopie", a => AussenBearbeitung.Kopieren(a, s));
        Gemeldet?.Invoke(neu is null ? Texte.KopieNicht : ArtName(s.Art) + " kopiert");
        Waehlen(neu ?? s);
    }

    public void Loeschen()
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s)
        {
            return;
        }
        if (AussenBearbeitung.LoeschenNicht(s) is { } grund)
        {
            Gemeldet?.Invoke(grund);
            return;
        }
        _projekt.AussenAendern("Löschen", a => AussenBearbeitung.Loeschen(a, s));
        Gemeldet?.Invoke(ArtName(s.Art) + " gelöscht");
        Waehlen(null);
    }

    /// <summary>Tasten fuer die Karte (vom Fenster weitergereicht); true = verarbeitet.</summary>
    public bool Taste(Key taste, KeyModifiers mod)
    {
        var strg = mod.HasFlag(KeyModifiers.Control) || mod.HasFlag(KeyModifiers.Meta);
        var schritt = Raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        switch (taste)
        {
            case Key.Escape:
                SetzenArt = null;
                Waehlen(null);
                return true;
            case Key.Delete or Key.Back when Auswahl is not null:
                Loeschen();
                return true;
            case Key.R when !strg && Auswahl is not null:
                Drehen();
                return true;
            case Key.D when strg && Auswahl is not null:
                Kopieren();
                return true;
            case Key.Left when Auswahl is not null:
                Verschieben(-schritt, 0);
                return true;
            case Key.Right when Auswahl is not null:
                Verschieben(schritt, 0);
                return true;
            case Key.Up when Auswahl is not null:
                Verschieben(0, -schritt);
                return true;
            case Key.Down when Auswahl is not null:
                Verschieben(0, schritt);
                return true;
            default:
                return false;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Stand is not { } a)
        {
            return;
        }
        Focus();
        var p = e.GetPosition(this);
        var w = WeltPunkt(p);
        e.Handled = true;
        if (SetzenArt is not null)
        {
            SetzenAn(w.X, w.Y);
            return;
        }
        var s = AussenBearbeitung.StueckBei(a, w.X, w.Y, Math.Max(0.3, 10 / _zoom));
        if (e.ClickCount == 2 && s is { Art: AussenArt.Haus } haus && _projekt!.Orte.Contains(haus.Ort))
        {
            OrtGewaehlt?.Invoke(haus.Ort);
            return;
        }
        Waehlen(s);
        if (s is not null)
        {
            Gemeldet?.Invoke(AussenBearbeitung.Beschreibung(a, s, OrtName));
            _druck = p;
            _ziehWelt = default;
            _gezogen = false;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        TippZeigen(e.GetPosition(this));
        if (_druck is not { } start || Auswahl is null || _projekt?.NurLesen != false)
        {
            return;
        }
        var p = e.GetPosition(this);
        if (!_gezogen && Math.Abs(p.X - start.X) + Math.Abs(p.Y - start.Y) < 6)
        {
            return;
        }
        _gezogen = true;
        _ziehWelt = new Point((p.X - start.X) / _zoom, (p.Y - start.Y) / _zoom);
        InvalidateVisual();
    }

    private string? _tipp;

    /// <summary>Voller Name des Stuecks unter dem Zeiger als Tooltip (gekuerzte Hausnamen, Katalog-Stuecke).</summary>
    private void TippZeigen(Point bild)
    {
        string? text = null;
        if (Stand is { } a && _projekt is not null)
        {
            var w = WeltPunkt(bild);
            if (AussenBearbeitung.StueckBei(a, w.X, w.Y, 6 / _zoom) is { } s && s.Art is AussenArt.Haus or AussenArt.Gebaeude or AussenArt.Katalog)
            {
                text = AussenBearbeitung.Beschreibung(a, s, OrtName);
            }
        }
        if (text != _tipp)
        {
            _tipp = text;
            ToolTip.SetTip(this, text);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_druck is null)
        {
            return;
        }
        _druck = null;
        e.Pointer.Capture(null);
        if (_gezogen)
        {
            _gezogen = false;
            var (dx, dy) = (_ziehWelt.X, _ziehWelt.Y);
            _ziehWelt = default;
            Verschieben(dx, dy);
        }
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomUm(e.GetPosition(this), e.Delta.Y > 0 ? 1.15 : 1 / 1.15);
        e.Handled = true;
    }
}
