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
            ZiehZuruecksetzen();
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
        ZiehZuruecksetzen();
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
    public static bool DrehbarBeimSetzen(AussenArt? art) => art is AussenArt.Strasse or AussenArt.Kurve or AussenArt.Park or AussenArt.Katalog or AussenArt.Fluss or AussenArt.Bahn
        or AussenArt.Teich or AussenArt.Steg or AussenArt.Gebaeude or AussenArt.Blueprint;

    /// <summary>Beim Setzen um 90 Grad weiterdrehen (Knopf und Taste R).</summary>
    public void SetzenDrehen(bool rueckwaerts = false)
    {
        if (DrehbarBeimSetzen(SetzenArt))
        {
            SetzenDreh = (SetzenDreh + (rueckwaerts ? 3 : 1)) % 4;
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

    /// <summary>Welle Q: Platz unten fuer die Kamera-Karte (Bildpunkte). Aendert er sich, passt eine eingepasste Karte neu ein.</summary>
    public double UntenFrei
    {
        get => _untenFrei;
        set
        {
            if (Math.Abs(_untenFrei - value) < 0.5)
            {
                return;
            }
            _untenFrei = value;
            // nur neu einpassen, solange die Ansicht noch genau so steht wie nach dem letzten Einpassen (sonst bliebe ein
            // eigener Zoom beim Wechsel zwischen Draufsicht und 3D nicht stehen)
            if (_eingepasst && _zoom == _einpassZoom && _versatz == _einpassVersatz)
            {
                Einpassen();
            }
        }
    }

    private double _untenFrei;
    private double _einpassZoom = double.NaN;
    private Point _einpassVersatz;

    /// <summary>Gegenprobe Welle Q: Einpassen ohne Platz fuer die Kamera-Karte (wie bis S 0.16.0).</summary>
    internal static bool UntenFreiAltFuerTests { get; set; }

    /// <summary>Rechteck der ganzen Stadt in Bildpunkten der Flaeche (Start-Test).</summary>
    public Rect WeltFlaeche
    {
        get
        {
            var (b, h) = Welt();
            return new Rect(_versatz.X, _versatz.Y, b * _zoom, h * _zoom);
        }
    }

    public void Einpassen()
    {
        if (Bounds.Width < 10)
        {
            return;
        }
        var (b, h) = Welt();
        // Welle Q (Weisung P12-76 Punkt 8): unten bleibt der Platz der Kamera-Karte frei, sie verdeckt nie einen Hausnamen
        var hoehe = Math.Max(48, Bounds.Height - (UntenFreiAltFuerTests ? 0 : UntenFrei));
        _zoom = Math.Max(4, Math.Min((Bounds.Width - 24) / b, (hoehe - 24) / h));
        _versatz = new Point((Bounds.Width - (b * _zoom)) / 2, (hoehe - (h * _zoom)) / 2);
        _eingepasst = true;
        _einpassZoom = _zoom;
        _einpassVersatz = _versatz;
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

    /// <summary>Start-Test: Bildpunkt eines Punkts der Karte (Umkehrung von <see cref="WeltPunkt"/>).</summary>
    internal Point BildPunktFuerTests(double x, double y) => new((x * _zoom) + _versatz.X, (y * _zoom) + _versatz.Y);

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
        // Welle Z: Fusswege (hell wie die Wege zu den Tueren, runde Ecken wie im Spiel)
        for (var i = 0; i < Aussen.Fusswege(a).Count; i++)
        {
            var fs = new AussenStelle(AussenArt.Fussweg, i);
            var glatt = AussenBearbeitung.FusswegGlatt(a, fs).Select(q => q + new System.Numerics.Vector2((float)Dx(fs), (float)Dy(fs))).ToList();
            Linie(ctx, glatt, new Pen(Pinsel("#EFEBE2"), Fusswege.Breite * _zoom, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Round));
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
        // Welle K2: Bahn als glattes Gleis wie im Spiel (Schotterbett, Schwellen gestrichelt), beim Ziehen mit dem Finger mit
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is PyObjekt l && Py.Gleich(l["typ"], "bahn"))
            {
                var glatt = Kurve.Glatt(WasserPunkte(a, new AussenStelle(AussenArt.Bahn, i, -1)), StadtSzene.Radien(l));
                Linie(ctx, glatt, new Pen(Pinsel(GleisbettFarbe), StadtSzene.GleisBreite * _zoom, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Round));
                Linie(ctx, glatt, new Pen(Pinsel("#5A4632"), 0.25 * _zoom, new DashStyle([0.4, 0.4], 0), lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Round));
            }
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
                case AussenArt.Fussweg:
                    Linie(ctx, AussenBearbeitung.FusswegGlatt(a, aw).Select(q => q + new System.Numerics.Vector2((float)dx, (float)dy)).ToList(), stift);
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
                case AussenArt.Teich or AussenArt.Fluss or AussenArt.Bahn:
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
        ZiehVorschauZeichnen(ctx);
        AussehenVorschauZeichnen(ctx);
        GriffZeichnen(ctx);
    }

    /// <summary>Farben des Wassers in der Draufsicht (wie im Spiel, etwas kraeftiger).</summary>
    private const string WasserFarbe = "#5B9BD5";

    private const string UferFarbe = "#C9B98A";

    /// <summary>Welle K2: Schotterbett der Bahn in der Draufsicht.</summary>
    private const string GleisbettFarbe = "#9C8E7C";

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

    // ---- Ziehen von Punkt zu Punkt (Welle Z) ------------------------------------------------------

    /// <summary>Was gerade von Punkt zu Punkt gezogen wird (null = kein Ziehen gewaehlt).</summary>
    public ZiehArt? ZiehArtAktiv => SetzenArt == AussenArt.Ziehen ? AussenBearbeitung.ZiehArtVon(SetzenModell) : null;

    /// <summary>Anfang (wie angetippt, Weltpunkt) nach dem ersten Tipp, sonst null.</summary>
    public Point? ZiehAnfang { get; private set; }

    /// <summary>Wo das Ende gerade laege (Zeiger, Finger), fuer die Vorschau.</summary>
    public Point? ZiehZiel { get; private set; }

    /// <summary>Grund der letzten Vorschau ohne Verbindung (null = sie passt).</summary>
    public string? ZiehGrund { get; private set; }

    /// <summary>Anfang oder Vorschau geaendert (Hinweis links, 3D-Schatten).</summary>
    public event Action? ZiehenGeaendert;

    private bool _anfangInDieserGeste;
    private bool _rechtsDruck;

    private void ZiehZuruecksetzen()
    {
        if (ZiehAnfang is null && ZiehZiel is null)
        {
            return;
        }
        ZiehAnfang = null;
        ZiehZiel = null;
        ZiehGrund = null;
        ZiehenGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Ein eben erst in dieser Geste begonnener Anfang faellt weg (zweiter Finger in der 3D-Ansicht), ohne Meldung.</summary>
    public void ZiehAnfangVerwerfen() => ZiehZuruecksetzen();

    /// <summary>Den Anfang setzen (erster Tipp, auch aus der 3D-Ansicht).</summary>
    public void ZiehAnfangSetzen(double x, double y)
    {
        if (ZiehArtAktiv is null || _projekt is null || _projekt.NurLesen)
        {
            return;
        }
        ZiehAnfang = new Point(x, y);
        ZiehZiel = null;
        ZiehGrund = null;
        ZiehenGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Vorschau bis (x, y) (Zeiger schwebt, Finger zieht).</summary>
    public void ZiehZielSetzen(double x, double y)
    {
        ZiehZiel = new Point(x, y);
        if (ZiehAnfang is { } a && ZiehArtAktiv is { } art && Stand is { } stand)
        {
            ZiehGrund = AussenBearbeitung.ZiehPlan(stand, art, a.X, a.Y, x, y, Raster).Plan.Grund;
        }
        ZiehenGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Abbrechen (Escape, Rechtsklick): true = es gab einen Anfang, er ist verworfen; das Ziehen bleibt gewaehlt.</summary>
    public bool ZiehAbbrechen()
    {
        if (ZiehAnfang is null)
        {
            return false;
        }
        ZiehZuruecksetzen();
        Gemeldet?.Invoke(Texte.ZiehenAbgebrochen);
        return true;
    }

    /// <summary>
    /// Vom Anfang bis (x, y) ziehen: ein Schritt im Verlauf (einrasten, verbinden, setzen). Passt keine Verbindung, bleibt
    /// alles, wie es ist, und die Meldung nennt den Grund (der Anfang bleibt fuer einen neuen Versuch). Das Ziehen bleibt
    /// gewaehlt, der naechste Tipp ist wieder ein Anfang (mehrere Strecken hintereinander).
    /// </summary>
    public AussenStelle? ZiehenBis(double x, double y)
    {
        if (_projekt is null || _projekt.NurLesen || ZiehArtAktiv is not { } art || ZiehAnfang is not { } a)
        {
            return null;
        }
        var raster = Raster;
        var (_, _, plan) = AussenBearbeitung.ZiehPlan(_projekt.AussenStand, art, a.X, a.Y, x, y, raster);
        if (!plan.Gefunden)
        {
            ZiehGrund = plan.Grund;
            ZiehenGeaendert?.Invoke();
            Gemeldet?.Invoke(plan.Grund!);
            InvalidateVisual();
            return null;
        }
        var stuecke = art == ZiehArt.Strasse ? Verbindung.Stuecke(plan).Count : 1;
        var neu = _projekt.AussenAendern(AussenBearbeitung.ZiehName(art) + " " + Texte.Gezogen, st => AussenBearbeitung.Ziehen(st, art, a.X, a.Y, x, y, raster, out _));
        ZiehAnfang = null;
        ZiehZiel = null;
        ZiehGrund = null;
        ZiehenGeaendert?.Invoke();
        Gemeldet?.Invoke(Texte.ZiehenGesetzt(AussenBearbeitung.ZiehName(art), stuecke));
        Waehlen(neu);
        return neu;
    }

    // ---- Aussehen tauschen (Welle S 0.21.0, Weisungen P12-87/P12-88) ---------------------------------

    private (PyObjekt Stand, string Ort, bool Gut)? _aussehenVorschau;

    /// <summary>
    /// Vorschau „Aussehen tauschen“: die Stadt nach dem Tausch (eine Kopie, nie der Stand des Projekts), der Ort und ob es
    /// passt (gruen) oder nicht (rot). Gezeichnet werden das neue Haus als Schatten, Tuer, Fussweg und ein stehen
    /// gebliebener Bahnsteig. null = keine Vorschau.
    /// </summary>
    public (PyObjekt Stand, string Ort, bool Gut)? AussehenVorschau
    {
        get => _aussehenVorschau;
        set
        {
            _aussehenVorschau = value;
            InvalidateVisual();
        }
    }

    private void AussehenVorschauZeichnen(DrawingContext ctx)
    {
        if (_aussehenVorschau is not { } v)
        {
            return;
        }
        var farbe = v.Gut ? "#34D399" : "#F87171";
        var s = new AussenStelle(AussenArt.Haus, -1, 0, v.Ort);
        if (AussenBearbeitung.Gueltig(v.Stand, s))
        {
            var (x, y, w, h) = AussenBearbeitung.HausFlaeche(v.Stand, s);
            var r = new Rect(P(x, y), P(x + w, y + h));
            ctx.FillRectangle(Pinsel(farbe, 0.55), r);
            ctx.DrawRectangle(new Pen(Pinsel("#111827"), 1.5, new DashStyle([4, 3], 0)), r);
        }
        if (AussenBearbeitung.Zugang(v.Stand, v.Ort) is { } z)
        {
            Linie(ctx, z["weg"], new Pen(Pinsel(farbe), Math.Max(2, StadtSzene.WegBreite * _zoom), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), 0, 0);
            var t = (List<object?>)z["tuer"]!;
            ctx.DrawEllipse(Brushes.White, new Pen(Pinsel("#111827"), 2), P(Py.F(t[0]), Py.F(t[1])), 5, 5);
        }
        var land = Aussen.Landschaft(v.Stand);
        var nr = land.FindIndex(t => t is PyObjekt g && Py.Gleich(g["modell"], Bahnsteig.ModellName(v.Ort)));
        if (nr >= 0)
        {
            var (bx, by, bw, bh) = AussenBearbeitung.HausFlaeche(v.Stand, new AussenStelle(AussenArt.Gebaeude, nr));
            ctx.DrawRectangle(Pinsel(farbe, 0.3), new Pen(Pinsel("#111827"), 1, new DashStyle([2, 2], 0)), new Rect(P(bx, by), P(bx + bw, by + bh)));
        }
    }

    private void ZiehVorschauZeichnen(DrawingContext ctx)
    {
        if (ZiehArtAktiv is not { } art || Stand is not { } a)
        {
            return;
        }
        var akzent = Farbe("accent");
        if (ZiehAnfang is { } anfang && ZiehZiel is { } ziel)
        {
            var (pa, pb, plan) = AussenBearbeitung.ZiehPlan(a, art, anfang.X, anfang.Y, ziel.X, ziel.Y, Raster);
            var linie = AussenBearbeitung.ZiehLinie(art, plan);
            var farbe = plan.Gefunden ? "#34D399" : "#F87171";
            Linie(ctx, linie, new Pen(Pinsel(farbe, 0.6), 2 * AussenBearbeitung.ZiehHalb(art) * _zoom, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Round));
            Linie(ctx, linie, new Pen(Pinsel("#111827"), 1.5, new DashStyle([4, 3], 0)));
            Fangpunkt(ctx, pa, akzent);
            Fangpunkt(ctx, pb, akzent);
        }
        else if (ZiehAnfang is { } nurAnfang)
        {
            Fangpunkt(ctx, AussenBearbeitung.Fangen(a, art, nurAnfang.X, nurAnfang.Y, Raster), akzent);
        }
        else if (ZiehZiel is { } schwebt)
        {
            // noch kein Anfang: zeigen, wo er einrasten wuerde
            Fangpunkt(ctx, AussenBearbeitung.Fangen(a, art, schwebt.X, schwebt.Y, Raster), akzent);
        }
    }

    /// <summary>Ein eingerasteter Punkt: Kreis, mit Richtung ein kurzer Strich in die neue Verbindung.</summary>
    private void Fangpunkt(DrawingContext ctx, Ziehpunkt p, IBrush akzent)
    {
        var m = P(p.Lage.X, p.Lage.Y);
        ctx.DrawEllipse(Brushes.White, new Pen(akzent, 2), m, GriffRadius, GriffRadius);
        if (p.Weg is { } w)
        {
            ctx.DrawLine(new Pen(akzent, 3), m, new Point(m.X + (w.X * 22), m.Y + (w.Y * 22)));
        }
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
        // gedreht setzen und einrasten ist ein einziger Schritt im Verlauf (Welle E: gleicher Weg wie die Vorschau in 3D)
        var neu = _projekt.AussenAendern("Setzen", a => AussenBearbeitung.SetzenUndDrehen(a, art, x, y, raster, modell, dreh));
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
        var (raster, dreh) = (Raster, SetzenDreh);
        var neu = _projekt.AussenAendern(Texte.BlueprintSetzen + " „" + b.Name + "“", a => b.SetzenUndDrehen(a, x, y, raster, dreh, out grund));
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
        AussenArt.Bahn => Texte.Bahn,
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

    /// <summary>Welle E: ein Drehschritt (Linien 15 Grad, sonst 90; Knopf „Drehen“, Taste R, Umschalt+R rueckwaerts).</summary>
    public void Drehen(bool rueckwaerts = false)
    {
        if (Auswahl is { } s)
        {
            DrehenUm((rueckwaerts ? -1 : 1) * AussenBearbeitung.DrehSchritt(s.Art));
        }
    }

    /// <summary>Welle E: um so viele Grad drehen (Drehgriff), ein Schritt im Verlauf.</summary>
    public void DrehenUm(int grad)
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s || grad % 360 == 0)
        {
            return;
        }
        if (AussenBearbeitung.DrehenNicht(s) is { } grund)
        {
            Gemeldet?.Invoke(grund);
            return;
        }
        var raster = Raster;
        _projekt.AussenAendern("Drehen", a =>
        {
            AussenBearbeitung.Drehen(a, s, grad);
            // ein Haus legt sich nach dem Drehen wieder an den Gehweg (nur mit "Einrasten")
            if (raster && s.Art is AussenArt.Haus or AussenArt.Gebaeude)
            {
                AussenBearbeitung.HausAnGehweg(a, s);
            }
        });
        Gemeldet?.Invoke(ArtName(s.Art) + " gedreht (" + grad.ToString(CultureInfo.InvariantCulture) + " Grad)");
        Waehlen(s);
    }

    /// <summary>Welle E: Feld „Winkel“ (1 Grad) einer Linie setzen.</summary>
    public void WinkelSetzen(int grad)
    {
        if (_projekt is null || _projekt.NurLesen || Auswahl is not { } s || !AussenBearbeitung.MitWinkel(_projekt.AussenStand, s))
        {
            return;
        }
        _projekt.AussenAendern(Texte.Winkel, a => AussenBearbeitung.WinkelSetzen(a, s, grad));
        Gemeldet?.Invoke(Texte.Winkel + " " + grad.ToString(CultureInfo.InvariantCulture) + " Grad");
        Waehlen(s);
    }

    // ---- Drehgriff (Welle E) ----------------------------------------------------------------------

    private bool _griffZiehen;
    private Point _griffPunkt;

    /// <summary>Mitte des gewaehlten Stuecks im Bild, wenn es einen Drehgriff hat (drehbar, nicht beim Setzen).</summary>
    public Point? GriffMitte
    {
        get
        {
            if (Stand is not { } a || _projekt is null || _projekt.NurLesen || SetzenArt is not null || Auswahl is not { } s
                || !AussenBearbeitung.Gueltig(a, s) || AussenBearbeitung.DrehenNicht(s) is not null || (AussenBearbeitung.MitEckpunkten(s.Art) && s.Punkt >= 0))
            {
                return null;
            }
            var m = AussenBearbeitung.Alle(a).FirstOrDefault(z => z.Stelle == s);
            return P(m.X, m.Y);
        }
    }

    /// <summary>Lage des Drehgriffs im Bild (Start-Test).</summary>
    public Point? Griff => GriffMitte is { } m ? new Point(Drehgriff.Lage(m.X, m.Y).X, Drehgriff.Lage(m.X, m.Y).Y) : null;

    private void GriffZeichnen(DrawingContext ctx)
    {
        if (GriffMitte is not { } m)
        {
            return;
        }
        var (gx, gy) = Drehgriff.Lage(m.X, m.Y);
        var stift = new Pen(Farbe("accent"), 2);
        ctx.DrawLine(new Pen(Farbe("accent"), 1.5, new DashStyle([3, 2], 0)), m, new Point(gx, gy));
        ctx.DrawEllipse(Brushes.White, stift, new Point(gx, gy), Drehgriff.Halbmesser, Drehgriff.Halbmesser);
        // Pfeil im Kreis (Viertelbogen)
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            var r = Drehgriff.Halbmesser * 0.55;
            g.BeginFigure(new Point(gx - r, gy), false);
            g.ArcTo(new Point(gx, gy - r), new Size(r, r), 0, false, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, stift, geo);
        if (_griffZiehen)
        {
            var grad = Drehgriff.Grad(m.X, m.Y, _griffPunkt.X, _griffPunkt.Y, Auswahl is { } s ? AussenBearbeitung.DrehSchritt(s.Art) : 90);
            var w = (-90 + grad) * Math.PI / 180;
            var lang = Drehgriff.Abstand;
            ctx.DrawLine(new Pen(Farbe("accent"), 3), m, new Point(m.X + (lang * Math.Cos(w)), m.Y + (lang * Math.Sin(w))));
            Text(ctx, new Typeface(FontFamily.Default), (grad > 0 ? "+" : "") + grad.ToString(CultureInfo.InvariantCulture) + "°", new Point(gx + 16, gy - 8), 12, Farbe("text"), null);
        }
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
            case Key.Escape when ZiehAbbrechen():
                return true;
            case Key.Escape:
                SetzenBeenden();
                Waehlen(null);
                return true;
            case Key.Delete or Key.Back when Auswahl is not null:
                Loeschen();
                return true;
            case Key.R when !strg && DrehbarBeimSetzen(SetzenArt):
                SetzenDrehen(mod.HasFlag(KeyModifiers.Shift));
                return true;
            case Key.R when !strg && Auswahl is not null:
                Drehen(mod.HasFlag(KeyModifiers.Shift));
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
        if (erster && !_griffZiehen && GriffMitte is { } gm && Drehgriff.Getroffen(gm.X, gm.Y, p.X, p.Y) && !NurSchiebenDruck(e, this))
        {
            // Welle E: Drehgriff (ein Finger dreht in Rasten); weitere Finger zaehlen erst nach dem Loslassen
            _griffZiehen = true;
            _griffPunkt = p;
            _griffZeiger = e.Pointer.Id;
            InvalidateVisual();
            return;
        }
        if (_griffZiehen)
        {
            return;
        }
        if (erster)
        {
            _anfangInDieserGeste = false;
            _rechtsDruck = e.Pointer.Type == PointerType.Mouse && e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        }
        if (erster && NurSchiebenDruck(e, this))
        {
            // Nachbesserung S 0.5.1 (Weisung P12-40 Punkt 2): rechte oder mittlere Maustaste oder Leertaste schiebt
            // die Ansicht auch ueber Stuecken, waehlt nichts und setzt nichts
            _nurSchieben = true;
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, false));
            return;
        }
        var s = SetzenArt is null ? AussenBearbeitung.StueckBei(a, w.X, w.Y, Math.Max(0.3, 10 / _zoom)) : null;
        // Welle Z: beim Ziehen zieht ein Finger vom Anfang zum Ende (wie ein Stueck), zwei Finger bleiben bei der Ansicht
        var zieht = ZiehArtAktiv is not null && _projekt?.NurLesen == false;
        Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, zieht || (s is not null && _projekt?.NurLesen == false)));
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
        if (_griffZiehen)
        {
            if (e.Pointer.Id == _griffZeiger)
            {
                _griffPunkt = p;
                InvalidateVisual();
            }
            return;
        }
        TippZeigen(p);
        if (!_geste.Aktiv && ZiehArtAktiv is not null && e.Pointer.Type == PointerType.Mouse)
        {
            // Maus schwebt: die Vorschau folgt
            var w = WeltPunkt(p);
            ZiehZielSetzen(w.X, w.Y);
        }
        Anwenden(_geste.Bewegen(e.Pointer.Id, p.X, p.Y));
    }

    private int _griffZeiger;

    /// <summary>Drehgriff loslassen: um die gerasteten Grad drehen (ein Schritt).</summary>
    private void GriffLoslassen(Point p)
    {
        _griffZiehen = false;
        if (GriffMitte is { } m && Auswahl is { } s)
        {
            DrehenUm(Drehgriff.Grad(m.X, m.Y, p.X, p.Y, AussenBearbeitung.DrehSchritt(s.Art)));
        }
        InvalidateVisual();
    }

    /// <summary>Start-Test: Drehgriff mit dem Finger von seiner Lage bis (x, y) ziehen (wie Druecken, Bewegen, Loslassen).</summary>
    internal void GriffZiehenFuerTests(double x, double y)
    {
        if (Griff is { } g)
        {
            _griffZiehen = true;
            _griffPunkt = g;
            GriffLoslassen(new Point(x, y));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_griffZiehen)
        {
            if (e.Pointer.Id == _griffZeiger)
            {
                GriffLoslassen(e.GetPosition(this));
                e.Pointer.Capture(null);
            }
            return;
        }
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
        if (_griffZiehen && e.Pointer.Id == _griffZeiger)
        {
            _griffZiehen = false;
            InvalidateVisual();
            return;
        }
        Anwenden(_geste.Verloren(e.Pointer.Id));
    }

    /// <summary>Ergebnis der Fingerauswertung (S 0.5.1): Setzen beim Loslassen ohne Bewegung, ein Finger auf freier
    /// Flaeche schiebt die Ansicht, zwei Finger zoomen und schieben.</summary>
    private void Anwenden(Fingergeste.Schritt s)
    {
        if (ZiehArtAktiv is not null && ZiehenAnwenden(s))
        {
            return;
        }
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

    /// <summary>Welle Z: Tippen, Ziehen und Loslassen beim Ziehen von Punkt zu Punkt (true = verarbeitet).</summary>
    private bool ZiehenAnwenden(Fingergeste.Schritt s)
    {
        var w = WeltPunkt(new Point(s.X, s.Y));
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when _nurSchieben:
                // Rechtsklick ohne Bewegung bricht ab (mit Bewegung schiebt er nur die Ansicht)
                if (_rechtsDruck && !ZiehAbbrechen())
                {
                    SetzenBeenden();
                }
                return true;
            case Fingergeste.Art.Tippen:
                if (ZiehAnfang is null)
                {
                    ZiehAnfangSetzen(w.X, w.Y);
                }
                else
                {
                    ZiehenBis(w.X, w.Y);
                }
                return true;
            case Fingergeste.Art.StueckZiehen:
                if (ZiehAnfang is null)
                {
                    var start = WeltPunkt(new Point(s.X - s.Dx, s.Y - s.Dy));
                    ZiehAnfangSetzen(start.X, start.Y);
                    _anfangInDieserGeste = true;
                }
                ZiehZielSetzen(w.X, w.Y);
                return true;
            case Fingergeste.Art.StueckFertig:
                ZiehenBis(w.X, w.Y);
                return true;
            case Fingergeste.Art.StueckAbbrechen:
                // ein zweiter Finger: die Ansicht bewegt sich, ein eben erst begonnener Anfang faellt weg
                if (_anfangInDieserGeste)
                {
                    ZiehZuruecksetzen();
                }
                else
                {
                    ZiehZiel = null;
                    InvalidateVisual();
                }
                return true;
            default:
                return false;
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
