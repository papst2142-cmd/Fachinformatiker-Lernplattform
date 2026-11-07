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
    private Fingergeste _geste = new();
    private Point _ziehWelt;
    private bool _gezogen;

    /// <summary>Gegenprobe im Start-Test: Fingerauswertung wie vor S 0.5.1 (jeder Druck beginnt neu).</summary>
    public bool GesteAltFuerTests { set => _geste = new Fingergeste(altFuerTests: value); }

    /// <summary>Massstab (Bildpunkte je Feld), fuer den Start-Test der Finger.</summary>
    public double Zoom => _zoom;

    /// <summary>Lage der Ansicht (Start-Test: Schieben).</summary>
    public Point Versatz => _versatz;

    /// <summary>Liegt an dieser Stelle des Bilds ein Stueck, das ein Finger ziehen wuerde? (Start-Test)</summary>
    public bool StueckAnFuerTests(Point bild) =>
        Stand is { } a && AussenBearbeitung.StueckBei(a, WeltPunkt(bild).X, WeltPunkt(bild).Y, Math.Max(0.3, 10 / _zoom)) is not null;

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
    public AussenArt? SetzenArt
    {
        get => _setzenArt;
        set
        {
            if (_setzenArt == value)
            {
                return;
            }
            _setzenArt = value;
            SetzenGeaendert?.Invoke();
        }
    }

    private AussenArt? _setzenArt;

    /// <summary>S 0.9.1 (Weisung P12-53 Teil A): das Setzen beginnt oder endet (Markierung des Knopfs links,
    /// Knopf "Setzen beenden", Schatten in 3D).</summary>
    public event Action? SetzenGeaendert;

    /// <summary>
    /// S 0.9.1 (Weisung P12-53 Teil A, Nico: "Man kann Strassen oder Fluesse nicht wieder abwaehlen"): das Setzen
    /// beenden, ohne etwas zu setzen. Kein Schritt im Verlauf, die Karte bleibt, wie sie ist. Vorher ging das nur mit
    /// Escape (am Tablet gar nicht), sonst endete das Setzen erst mit dem naechsten gesetzten Stueck.
    /// </summary>
    public void SetzenBeenden()
    {
        SetzenBlueprint = null;
        SetzenModell = null;
        SetzenArt = null;
        InvalidateVisual();
    }

    /// <summary>
    /// Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 4): Vierteldrehungen fuer das naechste gesetzte Stueck (Strasse
    /// waagerecht oder senkrecht, Park und Katalog-Stueck in 90-Grad-Schritten). Bleibt stehen, bis es wieder geaendert
    /// wird (mehrere senkrechte Strassen hintereinander). Knopf "Drehen" beim Setzen und Taste R.
    /// </summary>
    public int SetzenDreh { get; set; }

    /// <summary>Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 2): Leertaste gehalten (setzt die Hauptansicht), dann schiebt die Maus.</summary>
    public static bool LeertasteGehalten { get; set; }

    private bool _nurSchieben;

    /// <summary>Schiebt dieser Druck nur die Ansicht? (Maus mit rechter oder mittlerer Taste oder bei gehaltener Leertaste.)</summary>
    internal static bool NurSchiebenDruck(PointerPressedEventArgs e, Visual v)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return false;
        }
        var k = e.GetCurrentPoint(v).Properties;
        return k.IsRightButtonPressed || k.IsMiddleButtonPressed || LeertasteGehalten;
    }

    /// <summary>Kann das Spiel dieses Stueck gedreht zeigen (Strasse, Park, Katalog-Stueck)?</summary>
    public static bool DrehbarBeimSetzen(AussenArt? art) => art is AussenArt.Strasse or AussenArt.Kurve or AussenArt.Park or AussenArt.Katalog or AussenArt.Fluss or AussenArt.Teich or AussenArt.Steg;

    /// <summary>Beim Setzen um 90 Grad weiterdrehen (Knopf und Taste R).</summary>
    public void SetzenDrehen()
    {
        if (DrehbarBeimSetzen(SetzenArt))
        {
            SetzenDreh = (SetzenDreh + 1) % 4;
            AuswahlGeaendert?.Invoke();
        }
    }

    /// <summary>Modell fuer ein neues Haus ohne Ort; bei einer Kurve ihre Kennung (<see cref="Strassenkurve.Kennung"/>).</summary>
    public string? SetzenModell { get; set; }

    /// <summary>Welle K1 (Weisung P12-55): wird die naechste Kurve gespiegelt gesetzt?</summary>
    public bool SetzenGespiegelt => SetzenArt == AussenArt.Kurve && Strassenkurve.Lesen(SetzenModell) is { Gespiegelt: true };

    /// <summary>Welle K1: beim Setzen einer Kurve links und rechts tauschen (Knopf „Spiegeln“ und Taste M).</summary>
    public void SetzenSpiegeln()
    {
        if (SetzenArt == AussenArt.Kurve && Strassenkurve.Lesen(SetzenModell) is { } k)
        {
            SetzenModell = Strassenkurve.Kennung(k.Form, k.Winkel, k.Radius, !k.Gespiegelt);
            AuswahlGeaendert?.Invoke();
        }
    }

    /// <summary>Welle K1: die gewaehlte Kurve spiegeln (ein Schritt im Verlauf).</summary>
    public void Spiegeln()
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s)
        {
            return;
        }
        if (AussenBearbeitung.SpiegelnNicht(_projekt.AussenStand, s) is { } grund)
        {
            Gemeldet?.Invoke(grund);
            return;
        }
        _projekt.AussenAendern(Texte.Spiegeln, a => AussenBearbeitung.Spiegeln(a, s));
        Gemeldet?.Invoke(Texte.KurveGespiegelt);
        Waehlen(s);
    }

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
                {
                    // S 0.8.0: glatter Bogen mit Ufer wie im Spiel (vorher eine Linie durch die Punkte)
                    var glatt = Kurve.Glatt(WasserPunkte(a, new AussenStelle(AussenArt.Fluss, i, -1)), StadtSzene.Radien(l));
                    Linie(ctx, glatt, new Pen(Pinsel(UferFarbe), (StadtSzene.FlussBreite + 0.3) * _zoom, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Round));
                    Linie(ctx, glatt, new Pen(Pinsel(WasserFarbe), StadtSzene.FlussBreite * _zoom, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Round));
                    break;
                }
            }
        }
        // S 0.8.0: Teiche (Ufer, Wasser mit gerundeten Ecken) und Stege
        var teiche = Aussen.Teiche(a);
        for (var i = 0; i < teiche.Count; i++)
        {
            var punkte = WasserPunkte(a, new AussenStelle(AussenArt.Teich, i, -1));
            if (punkte.Count >= 3)
            {
                var t = new Teich(punkte);
                Flaeche(ctx, t.MitUfer, Pinsel(UferFarbe));
                Flaeche(ctx, t.Rund, Pinsel(WasserFarbe));
            }
        }
        var stege = Aussen.Stege(a);
        for (var i = 0; i < stege.Count; i++)
        {
            if (Wasser.Steg(stege[i]) is { } steg)
            {
                var st = new AussenStelle(AussenArt.Steg, i);
                var (x1, z1, x2, z2) = steg.Rechteck;
                ctx.DrawRectangle(Pinsel("#9A6B3F"), new Pen(Pinsel("#6E4A2A"), 1), new Rect(P(x1 + Dx(st), z1 + Dy(st)), P(x2 + Dx(st), z2 + Dy(st))));
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
        // Welle K3: Mittelinsel jedes Kreisverkehrs (heller Rand, Rasen, Baum) wie im Spiel
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is PyObjekt l && Kreisverkehr.Radius(l) is { } kr && Kreisverkehr.Linie(l["punkte"]) is var ring && Kreisverkehr.Rund(ring, kr))
            {
                var s = new AussenStelle(AussenArt.Strasse, i);
                var m = Kreisverkehr.Mitte(ring) + new System.Numerics.Vector2((float)Dx(s), (float)Dy(s));
                var ri = Kreisverkehr.InselRadius(kr);
                Flaeche(ctx, Kreisverkehr.Kreis(m, ri), Pinsel("#D8D4CA"));
                Flaeche(ctx, Kreisverkehr.Kreis(m, ri - Kreisverkehr.RandBreite), Pinsel("#7FB069"));
                ctx.DrawEllipse(Pinsel("#3F7D3A", 0.85), new Pen(Pinsel("#2A5527"), 1), P(m.X, m.Y), Kreisverkehr.BaumRadius * _zoom, Kreisverkehr.BaumRadius * _zoom);
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
            var mitInnen = (s.Art == AussenArt.Haus && bearbeitbar.Contains(s.Ort)) || (s.Art == AussenArt.Gebaeude && _projekt.InnenortVon(s) is not null);
            // Fussweg und Tuer: Haus mit Ort aus dem Zugang, Gebaeude ohne Ort aus dem Zierweg seines Modells (gehoert wie im
            // Spiel zum ersten Gebaeude dieses Modells; Sammelliste 225, Weisung P12-33)
            if ((s.Art == AussenArt.Haus ? AussenBearbeitung.Zugang(a, s.Ort) : Blueprint.ZierwegVon(a, s)) is { } z)
            {
                Linie(ctx, z["weg"], new Pen(Pinsel("#EFEBE2"), StadtSzene.WegBreite * _zoom, lineCap: PenLineCap.Flat), Dx(s), Dy(s));
                if (z["tuer"] is List<object?> t && t.Count >= 2)
                {
                    ctx.DrawEllipse(Pinsel("#7B4B24"), null, P(Py.F(t[0]) + Dx(s), Py.F(t[1]) + Dy(s)), 0.14 * _zoom, 0.14 * _zoom);
                }
            }
            ctx.DrawRectangle(Pinsel(s.Art == AussenArt.Haus ? "#C9B79C" : "#A8A49C"), new Pen(mitInnen ? Farbe("accent") : Pinsel("#5C554B"), mitInnen ? 3 : 1), r);
            var name = s.Art == AussenArt.Haus ? OrtName(s.Ort) : AussenBearbeitung.BauName(a, s) ?? Py.Str(Objekt(land[s.Nummer])["modell"]);
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
                case AussenArt.Steg:
                    if (Wasser.Steg(Aussen.Stege(a)[aw.Nummer]) is { } sg)
                    {
                        var (x1, z1, x2, z2) = sg.Rechteck;
                        ctx.DrawRectangle(null, stift, new Rect(P(x1 + dx, z1 + dy), P(x2 + dx, z2 + dy)).Inflate(3));
                    }
                    break;
                case AussenArt.Teich or AussenArt.Fluss:
                {
                    // S 0.8.0: Rand und Griffe an den Eckpunkten (Weisung P12-47: am Tablet mit einem Finger ziehen)
                    var ganz = AussenBearbeitung.Ganz(aw);
                    var punkte = WasserPunkte(a, ganz);
                    if (aw.Art == AussenArt.Teich)
                    {
                        Flaeche(ctx, new Teich(punkte).MitUfer, null, stift);
                    }
                    else
                    {
                        Linie(ctx, Kurve.Glatt(punkte, StadtSzene.Radien(SpielDaten.Objekt(Aussen.Landschaft(a)[aw.Nummer]))), stift);
                    }
                    var r = Math.Max(GriffRadius, 0.15 * _zoom);
                    for (var k = 0; k < punkte.Count; k++)
                    {
                        ctx.DrawEllipse(k == aw.Punkt ? Farbe("accent") : Brushes.White, new Pen(Farbe("accent"), 2), P(punkte[k].X, punkte[k].Y), r, r);
                    }
                    break;
                }
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

    /// <summary>Farben des Wassers in der Draufsicht (wie im Spiel, etwas kraeftiger).</summary>
    private const string WasserFarbe = "#5B9BD5";

    private const string UferFarbe = "#C9B98A";

    /// <summary>Halbmesser der Griffe an den Eckpunkten in Bildpunkten (gut mit dem Finger zu treffen).</summary>
    public const double GriffRadius = 7;

    /// <summary>Eckpunkte eines Teichs oder Flusses, waehrend des Ziehens mit dem gezogenen Punkt oder ganz verschoben.</summary>
    private List<System.Numerics.Vector2> WasserPunkte(PyObjekt a, AussenStelle ganz)
    {
        var punkte = AussenBearbeitung.EckpunkteWerte(a, ganz);
        if (_gezogen && Auswahl is { } aw && AussenBearbeitung.Ganz(aw) == ganz)
        {
            var d = new System.Numerics.Vector2((float)_ziehWelt.X, (float)_ziehWelt.Y);
            for (var k = 0; k < punkte.Count; k++)
            {
                if (aw.Punkt < 0 || aw.Punkt == k)
                {
                    punkte[k] += d;
                }
            }
        }
        return punkte;
    }

    private void Flaeche(DrawingContext ctx, IReadOnlyList<System.Numerics.Vector2> punkte, IBrush? fuellung, Pen? stift = null)
    {
        if (punkte.Count < 3)
        {
            return;
        }
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(P(punkte[0].X, punkte[0].Y), true);
            foreach (var q in punkte.Skip(1))
            {
                g.LineTo(P(q.X, q.Y));
            }
            g.EndFigure(true);
        }
        ctx.DrawGeometry(fuellung, stift, geo);
    }

    private void Linie(DrawingContext ctx, IReadOnlyList<System.Numerics.Vector2> punkte, Pen stift)
    {
        if (punkte.Count < 2)
        {
            return;
        }
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(P(punkte[0].X, punkte[0].Y), false);
            foreach (var q in punkte.Skip(1))
            {
                g.LineTo(P(q.X, q.Y));
            }
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, stift, geo);
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

    /// <summary>S 0.7.0: waehlen wie ein Tipp in der Draufsicht (mit Beschreibung in der Meldezeile), auch aus der 3D-Ansicht.</summary>
    public void WaehlenMitMeldung(AussenStelle? s)
    {
        Waehlen(s);
        if (s is not null && Stand is { } a)
        {
            Gemeldet?.Invoke(AussenBearbeitung.Beschreibung(a, s, OrtName));
        }
    }

    /// <summary>S 0.7.0: Innenraum des Hauses oeffnen wie ein Doppeltipp (true = es hat einen).</summary>
    public bool InnenOeffnen(AussenStelle s)
    {
        if (_projekt?.InnenortVon(s) is not { } innenort)
        {
            return false;
        }
        OrtGewaehlt?.Invoke(innenort);
        return true;
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
        var dreh = DrehbarBeimSetzen(art) ? SetzenDreh : 0;
        var neu = _projekt.AussenAendern("Setzen", a =>
        {
            // gedreht setzen ist ein einziger Schritt im Verlauf
            var s = AussenBearbeitung.Setzen(a, art, x, y, raster, modell);
            for (var i = 0; s is not null && i < dreh; i++)
            {
                AussenBearbeitung.Drehen(a, s);
            }
            return s;
        });
        SetzenArt = null;
        if (neu is not null)
        {
            Gemeldet?.Invoke((art == AussenArt.Katalog ? KatalogPruefung.Name(Katalog.Aktiv, Katalog.Praefix + SetzenModell)
                : art == AussenArt.Kurve && Strassenkurve.Lesen(modell) is { } k ? Strassenkurve.Name(k.Form, k.Winkel, k.Radius)
                : art == AussenArt.Kreisverkehr && AussenBearbeitung.KreisverkehrRadius(modell) is { } kr ? Texte.KreisverkehrName(kr) : ArtName(art)) + " gesetzt");
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
        AussenArt.Kurve => Texte.Kurve90,
        AussenArt.Kreisverkehr => Texte.KreisverkehrKlein,
        AussenArt.Haus => Texte.Haus,
        AussenArt.Gebaeude => Texte.HausOhneOrt,
        AussenArt.Baum => Texte.Baum,
        AussenArt.Park => Texte.Park,
        AussenArt.Beet => Texte.Beet,
        AussenArt.Laterne => Texte.Laterne,
        AussenArt.Katalog => Texte.VollerKatalog,
        AussenArt.Blueprint => Texte.BlueprintSetzen,
        AussenArt.Fluss => Texte.Fluss,
        AussenArt.Teich => Texte.Teich,
        AussenArt.Steg => Texte.Steg,
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

    /// <summary>S 0.8.0: Eckpunkt dazu (Teich, Fluss), der neue Punkt ist danach gewaehlt.</summary>
    public void EckpunktDazu()
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s || !AussenBearbeitung.MitEckpunkten(s.Art))
        {
            return;
        }
        var neu = _projekt.AussenAendern(Texte.EckpunktDazu, a => AussenBearbeitung.EckpunktDazu(a, s));
        Gemeldet?.Invoke(Texte.EckpunktDazu);
        Waehlen(neu ?? s);
    }

    /// <summary>S 0.8.0: gewaehlten Eckpunkt weg; danach ist das ganze Stueck gewaehlt.</summary>
    public void EckpunktWeg()
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s || AussenBearbeitung.EckpunktWegNicht(_projekt.AussenStand, s) is { } grund)
        {
            if (Auswahl is not null && _projekt is not null)
            {
                Gemeldet?.Invoke(AussenBearbeitung.EckpunktWegNicht(_projekt.AussenStand, Auswahl) ?? "");
            }
            return;
        }
        _projekt.AussenAendern(Texte.EckpunktWeg, a => AussenBearbeitung.EckpunktWeg(a, s));
        Gemeldet?.Invoke(Texte.EckpunktWeg);
        Waehlen(AussenBearbeitung.Ganz(s));
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
                SetzenBeenden();
                Waehlen(null);
                return true;
            case Key.Delete or Key.Back when Auswahl is not null:
                Loeschen();
                return true;
            case Key.R when !strg && DrehbarBeimSetzen(SetzenArt):
                SetzenDrehen();
                return true;
            case Key.R when !strg && Auswahl is not null:
                Drehen();
                return true;
            case Key.D when strg && Auswahl is not null:
                Kopieren();
                return true;
            case Key.M when !strg && SetzenArt == AussenArt.Kurve:
                SetzenSpiegeln();
                return true;
            case Key.M when !strg && Auswahl is { } kurve && _projekt is not null && AussenBearbeitung.IstKurve(_projekt.AussenStand, kurve):
                Spiegeln();
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
        e.Pointer.Capture(this);
        var erster = !_geste.Aktiv;
        if (erster && NurSchiebenDruck(e, this))
        {
            // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 2): rechte oder mittlere Maustaste oder Leertaste schiebt
            // die Ansicht auch ueber Stuecken, waehlt nichts und setzt nichts
            _nurSchieben = true;
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, false));
            return;
        }
        var s = SetzenArt is null ? AussenBearbeitung.StueckBei(a, w.X, w.Y, Math.Max(0.3, 10 / _zoom)) : null;
        Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, s is not null && _projekt?.NurLesen == false));
        // nur der erste Finger waehlt (S 0.5.1: ein zweiter Finger aendert die Auswahl nie)
        if (!erster || SetzenArt is not null)
        {
            return;
        }
        if (e.ClickCount == 2 && s is not null && _projekt!.InnenortVon(s) is { } innenort)
        {
            OrtGewaehlt?.Invoke(innenort);
            return;
        }
        Waehlen(s);
        if (s is not null)
        {
            Gemeldet?.Invoke(AussenBearbeitung.Beschreibung(a, s, OrtName));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        TippZeigen(p);
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

    /// <summary>Ergebnis der Fingerauswertung (S 0.5.1): Setzen beim Loslassen ohne Bewegung, ein Finger auf freier
    /// Flaeche schiebt die Ansicht, zwei Finger zoomen und schieben.</summary>
    private void Anwenden(Fingergeste.Schritt s)
    {
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when SetzenArt is not null && !_nurSchieben:
                var w = WeltPunkt(new Point(s.X, s.Y));
                SetzenAn(w.X, w.Y);
                break;
            case Fingergeste.Art.StueckZiehen when Auswahl is not null:
                _gezogen = true;
                _ziehWelt = new Point(s.Dx / _zoom, s.Dy / _zoom);
                InvalidateVisual();
                break;
            case Fingergeste.Art.StueckFertig when _gezogen:
                _gezogen = false;
                var (dx, dy) = (s.Dx / _zoom, s.Dy / _zoom);
                _ziehWelt = default;
                Verschieben(dx, dy);
                InvalidateVisual();
                break;
            case Fingergeste.Art.StueckAbbrechen:
                _gezogen = false;
                _ziehWelt = default;
                InvalidateVisual();
                break;
            case Fingergeste.Art.AnsichtSchieben:
                _versatz += new Vector(s.Dx, s.Dy);
                InvalidateVisual();
                break;
            case Fingergeste.Art.Zoomen:
                _versatz += new Vector(s.Dx, s.Dy);
                ZoomUm(new Point(s.X, s.Y), s.Faktor);
                break;
        }
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

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomUm(e.GetPosition(this), e.Delta.Y > 0 ? 1.15 : 1 / 1.15);
        e.Handled = true;
    }
}
