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
public sealed partial class KartenFlaeche : Control
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
        Stand is { } a && StueckAn(a, bild, Math.Max(0.3, 10 / _zoom)) is not null;

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
            if (value is not null)
            {
                // S 0.27.0: Setzen beendet das Massband; S 0.28.0 auch das Tuer-Setzen
                MassbandAn = false;
                TuerSetzenBeenden();
            }
            ZiehZuruecksetzen();
            ReiheZuruecksetzen();
            if (value is not null)
            {
                // S 0.30.0: Setzen beendet „Mehrere wählen“
                MehrereWaehlen = false;
            }
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
            NeuEinpassenWennEingepasst();
        }
    }

    /// <summary>Weisung P12-137: Rahmen der Elemente ueber der Flaeche (Kamera-Karte, Werkzeugleiste), bezogen auf die uebergebene Flaeche.</summary>
    public Func<Visual, IReadOnlyList<Rect>>? Verdecker { get; set; }

    /// <summary>Weisung P12-137: wohin die Flaeche ihre Anzeigen zuletzt gezeichnet hat (Text, Rahmen).</summary>
    public IReadOnlyList<(string Text, Rect Rahmen)> AnzeigenGezeichnet => _anzeigen;

    private readonly List<(string Text, Rect Rahmen)> _anzeigen = [];

    internal IReadOnlyList<Rect> VerdecktFuer(Visual v) => Verdecker?.Invoke(v) ?? [];

    /// <summary>S 0.27.0: Platz oben fuer die Werkzeugleiste (Waehlen, Massband, Ebenen, Notiz), wie <see cref="UntenFrei"/>.</summary>
    public double ObenFrei
    {
        get => _obenFrei;
        set
        {
            if (Math.Abs(_obenFrei - value) < 0.5)
            {
                return;
            }
            _obenFrei = value;
            NeuEinpassenWennEingepasst();
        }
    }

    private double _obenFrei;

    private void NeuEinpassenWennEingepasst()
    {
        // nur neu einpassen, solange die Ansicht noch genau so steht wie nach dem letzten Einpassen (sonst bliebe ein
        // eigener Zoom beim Wechsel zwischen Draufsicht und 3D nicht stehen)
        if (_eingepasst && _zoom == _einpassZoom && _versatz == _einpassVersatz)
        {
            Einpassen();
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
        // S 0.27.0: oben bleibt der Platz der Werkzeugleiste frei
        var oben = UntenFreiAltFuerTests ? 0 : ObenFrei;
        var hoehe = Math.Max(48, Bounds.Height - (UntenFreiAltFuerTests ? 0 : UntenFrei) - oben);
        _zoom = Math.Max(4, Math.Min((Bounds.Width - 24) / b, (hoehe - 24) / h));
        _versatz = new Point((Bounds.Width - (b * _zoom)) / 2, oben + ((hoehe - (h * _zoom)) / 2));
        _eingepasst = true;
        _einpassZoom = _zoom;
        _einpassVersatz = _versatz;
        InvalidateVisual();
    }

    /// <summary>
    /// Welle S 0.33.0 (Sammelliste 273): Ansicht mittig auf die Weltstelle (x, y) (zwischen Werkzeugleiste und Kamera-Karte),
    /// mindestens Zoom 30, damit die Stelle gut zu sehen ist.
    /// </summary>
    public void Hinfahren(double x, double y)
    {
        if (Bounds.Width < 10)
        {
            return;
        }
        _zoom = Math.Max(_zoom, 30);
        var oben = ObenFrei;
        var hoehe = Math.Max(48, Bounds.Height - UntenFrei - oben);
        _versatz = new Point((Bounds.Width / 2) - (x * _zoom), oben + (hoehe / 2) - (y * _zoom));
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

    /// <summary>
    /// Welle S 0.22.0 (Weisung P12-91, Frage 3 = a): Zahl der Aufgaben der Spielgeschichte je Ort (null = keine Zahlen).
    /// </summary>
    public IReadOnlyDictionary<string, int>? GeschichteZahlen
    {
        get => _geschichteZahlen;
        set
        {
            _geschichteZahlen = value;
            InvalidateVisual();
        }
    }

    private IReadOnlyDictionary<string, int>? _geschichteZahlen;

    /// <summary>Zahlen am Haus beim letzten Zeichnen mit Ort und Rechteck (Start-Test: in der Karte, nicht verdeckt).</summary>
    public IReadOnlyList<(string Ort, int Zahl, Rect Flaeche, Rect Haus)> ZahlenAmHaus { get; private set; } = [];

    private Point WeltPunkt(Point bild) => new((bild.X - _versatz.X) / _zoom, (bild.Y - _versatz.Y) / _zoom);

    /// <summary>Start-Test: Bildpunkt eines Punkts der Karte (Umkehrung von <see cref="WeltPunkt"/>).</summary>
    /// <summary>Welle Q1: Questgeber draussen (setzt die Hauptansicht).</summary>
    internal IReadOnlyList<QuestgeberMarke> QuestgeberMarken { get; set; } = [];

    /// <summary>Welle Q1: Platz eines Questgebers draussen waehlen (gesetzt = ein Tipp ruft dies mit dem Punkt der Karte).</summary>
    internal Action<double, double>? QuestgeberSetzen { get; set; }

    /// <summary>Welle S 0.46.0 (Weisung P12-220): der naechste Tipp waehlt ein Stueck (Gegenstueck einer Bahnsteig-Verknuepfung).</summary>
    internal Action<double, double>? TippWahl { get; set; }

    internal Point BildPunktFuerTests(double x, double y) => new((x * _zoom) + _versatz.X, (y * _zoom) + _versatz.Y);

    /// <summary>Bildpunkt eines Weltpunkts (Start-Test).</summary>
    public Point Bildpunkt(double x, double y) => P(x, y);

    /// <summary>Schriftgroesse der Zahl am Haus.</summary>
    public const double ZahlGroesse = 12;

    private string OrtName(string id) => _projekt?.OrtName(id) ?? id;

    // ---- Zeichnen --------------------------------------------------------------------------------

    private static IBrush Pinsel(string hex, double deckkraft = 1) => new SolidColorBrush(Color.Parse(hex), deckkraft);

    private static IBrush Farbe(string name) => new SolidColorBrush(Color.Parse(FarbenAnwenden.Aktuell.Farben[name]));

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Farbe("bg"), new Rect(Bounds.Size));
        _anzeigen.Clear();
        if (Stand is not { } a)
        {
            return;
        }
        var (b, h) = Welt();
        var schrift = new Typeface(FontFamily.Default);
        ctx.FillRectangle(Pinsel("#B9D29A"), new Rect(P(0, 0), P(b, h)));
        var land = Aussen.Landschaft(a);
        var (dx, dy) = _gezogen && Auswahl is not null ? (_ziehWelt.X, _ziehWelt.Y) : (0, 0);
        // S 0.42.0: Huegel unter allem anderen, Hoehenlinien darueber
        GelaendeFlaechenZeichnen(ctx, a, dx, dy);
        HoehenlinienZeichnen(ctx, a);
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
                case "park" when Zeigt(Ebene.Gruen):
                {
                    var s = new AussenStelle(AussenArt.Park, i);
                    ctx.FillRectangle(Pinsel("#8DBA6A"), new Rect(P(Py.F(l["x"]) + Dx(s), Py.F(l["y"]) + Dy(s)), P(Py.F(l["x"]) + Py.F(l["w"]) + Dx(s), Py.F(l["y"]) + Py.F(l["h"]) + Dy(s))));
                    break;
                }
                case "fluss" when Zeigt(Ebene.Wasser):
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
        var teiche = Zeigt(Ebene.Wasser) ? Aussen.Teiche(a) : [];
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
        var stege = Zeigt(Ebene.Wasser) ? Aussen.Stege(a) : [];
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
        for (var i = 0; Zeigt(Ebene.Wege) && i < Aussen.Fusswege(a).Count; i++)
        {
            var fs = new AussenStelle(AussenArt.Fussweg, i);
            var glatt = AussenBearbeitung.FusswegGlatt(a, fs).Select(q => q + new System.Numerics.Vector2((float)Dx(fs), (float)Dy(fs))).ToList();
            Linie(ctx, glatt, new Pen(Pinsel("#EFEBE2"), Fusswege.Breite * _zoom, lineCap: PenLineCap.Flat, lineJoin: PenLineJoin.Round));
        }
        // S 0.26.0 (Weisung P12-115): Zebrastreifen als weisse Balken quer ueber die Fahrbahn wie im Spiel; beim Ziehen nur
        // laengs der Strasse
        foreach (var (s, z) in Zebras(a, dx, dy))
        {
            foreach (var (z1, z2) in Lernplattform.Core.Spiel.Probe.Zebrastreifen.BalkenVon(z))
            {
                Linie(ctx, [z1, z2], new Pen(Brushes.White, Lernplattform.Core.Spiel.Probe.Zebrastreifen.BalkenBreite * _zoom, lineCap: PenLineCap.Flat));
            }
        }
        // A 0.60.0 / S 0.35.0 (Weisung P12-166): Ampeln wie im Spiel: Furten mit Strichen, Haltelinien und die Masten als
        // dunkle Punkte mit drei Lichtern; beim Ziehen die Ampel an der Kreuzung, an die sie springen wuerde
        foreach (var (_, amp) in Ampeln(a, dx, dy))
        {
            var weiss = new Pen(Brushes.White, Lernplattform.Core.Spiel.Probe.Ampeln.FurtStrichBreite * _zoom, lineCap: PenLineCap.Flat);
            foreach (var furt in amp.Furten)
            {
                foreach (var (f1, f2) in Lernplattform.Core.Spiel.Probe.Ampeln.FurtStriche(furt))
                {
                    Linie(ctx, [f1, f2], weiss);
                }
            }
            foreach (var arm in amp.Arme)
            {
                var (h1, h2) = Lernplattform.Core.Spiel.Probe.Ampeln.Haltelinie(amp, arm);
                Linie(ctx, [h1, h2], new Pen(Brushes.White, Lernplattform.Core.Spiel.Probe.Ampeln.HalteBreite * _zoom, lineCap: PenLineCap.Flat));
            }
            foreach (var (fuss, _, _) in amp.Masten)
            {
                var r = Math.Max(3, 0.09 * _zoom);
                ctx.DrawEllipse(Pinsel("#2B2B2B"), new Pen(Pinsel("#F2C230"), Math.Max(1, r / 3)), P(fuss.X, fuss.Y), r, r);
            }
        }
        // A 0.61.0 / S 0.36.0 (Weisung P12-171): Haltestellen (gelbe Zickzack-Linie, Haeuschen, Schild) und Parkplaetze
        // (Flaeche, Einfahrt, weisse Linien, im Werkzeug alle Plaetze belegt) wie im Spiel; beim Ziehen nur laengs der Strasse
        foreach (var (_, halt, park) in AussenBearbeitung.HalteZumZeichnen(a, Auswahl, dx, dy))
        {
            if (park is { } pp)
            {
                var (e1, e2) = pp.Einfahrt;
                Linie(ctx, [e1, e2], new Pen(Pinsel("#6B6E75"), Lernplattform.Core.Spiel.Probe.Parkplaetze.Gasse * _zoom, lineCap: PenLineCap.Flat));
                Flaeche(ctx, pp.Ecken(), Pinsel("#6B6E75"));
                foreach (var (l1, l2) in pp.Linien())
                {
                    Linie(ctx, [l1, l2], new Pen(Brushes.White, Math.Max(1, 0.025 * _zoom), lineCap: PenLineCap.Flat));
                }
                foreach (var (mitte, blick) in pp.Stellplaetze())
                {
                    var quer = new System.Numerics.Vector2(-blick.Y, blick.X);
                    var t = blick * 0.26f;
                    var q = quer * 0.13f;
                    Flaeche(ctx, [mitte - t - q, mitte + t - q, mitte + t + q, mitte - t + q], Pinsel("#C9CED6"), new Pen(Pinsel("#3A3D42"), 1));
                }
            }
            if (halt is { } hs)
            {
                foreach (var (z1, z2) in Lernplattform.Core.Spiel.Probe.Haltestellen.Zickzack(hs))
                {
                    Linie(ctx, [z1, z2], new Pen(Pinsel("#F2C21B"), Math.Max(1, Lernplattform.Core.Spiel.Probe.Haltestellen.ZickzackBreite * _zoom), lineCap: PenLineCap.Flat));
                }
                Flaeche(ctx, hs.HaeuschenEcken(), Pinsel("#9CC3D5"), new Pen(Pinsel("#2E3338"), Math.Max(1, 0.03 * _zoom)));
                var r = Math.Max(4, 0.1 * _zoom);
                ctx.DrawEllipse(Pinsel("#F2C21B"), new Pen(Pinsel("#1E7A4C"), Math.Max(1, r / 3)), P(hs.Schild.X, hs.Schild.Y), r, r);
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
        var zahlen = new List<(string, int, Rect, Rect)>();
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
                if (Zeigt(Ebene.Wege))
                {
                    Linie(ctx, z["weg"], new Pen(Pinsel("#EFEBE2"), StadtSzene.WegBreite * _zoom, lineCap: PenLineCap.Flat), Dx(s), Dy(s));
                }
                if (z["tuer"] is List<object?> t && t.Count >= 2)
                {
                    var tuerId = AussenBearbeitung.FarbKennung(a, s) is { } kt ? AussenBearbeitung.FarbeVon(a, kt, Gebaeudefarben.TuerSchluessel) : null;
                    var tuerFarbe = Gebaeudefarben.TuerVarianten.FirstOrDefault(v => v.Id == tuerId).Wert ?? "#7B4B24";
                    ctx.DrawEllipse(Pinsel(tuerFarbe), new Pen(Pinsel("#5C554B"), 1), P(Py.F(t[0]) + Dx(s), Py.F(t[1]) + Dy(s)), 0.14 * _zoom, 0.14 * _zoom);
                }
            }
            // S 0.31.0 (Sammelliste 264): eigene Fassadenfarbe auch in der Draufsicht
            var kennung = AussenBearbeitung.FarbKennung(a, s);
            var fassade = kennung is null ? null : AussenBearbeitung.FarbeVon(a, kennung, "fassade");
            var hausPinsel = Pinsel(fassade ?? (s.Art == AussenArt.Haus ? "#C9B79C" : "#A8A49C"));
            var hausStift = new Pen(mitInnen ? Farbe("accent") : Pinsel("#5C554B"), mitInnen ? 3 : 1);
            if (AussenBearbeitung.HausGrad(a, s) != 0)
            {
                // Welle A 0.70.0 (Sammelliste 314): ein fein gedrehtes Haus als gedrehtes Rechteck
                var versatz = new System.Numerics.Vector2((float)Dx(s), (float)Dy(s));
                Flaeche(ctx, [.. AussenBearbeitung.HausEcken(a, s).Select(e => e + versatz)], hausPinsel, hausStift);
            }
            else
            {
                ctx.DrawRectangle(hausPinsel, hausStift, r);
            }
            var name = s.Art == AussenArt.Haus ? OrtName(s.Ort) : ModellNamen.Beschriftung(a, s);
            if (r.Width > 30 && Zeigt(Ebene.Beschriftungen))
            {
                Text(ctx, schrift, name, r.TopLeft + new Point(3, 2), 11, Brushes.Black, r.Width - 6);
            }
            if (s.Art == AussenArt.Haus && Zeigt(Ebene.Aufgaben) && _geschichteZahlen is { } gz && gz.TryGetValue(s.Ort, out var zahl) && zahl > 0)
            {
                zahlen.Add((s.Ort, zahl, default, r));
            }
        }

        // Gruen, Laternen, Paketplatz
        var aus = AusArten;
        foreach (var (s, x, y) in AussenBearbeitung.Alle(a).Where(x => x.Stelle.Art is AussenArt.Baum or AussenArt.Beet or AussenArt.Laterne or AussenArt.Paketplatz && !aus.Contains(x.Stelle.Art)).ToList())
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
        if (Zeigt(Ebene.Laternen) && AussenBearbeitung.Alle(a).Any(x => x.Stelle.Art == AussenArt.Laterne))
        {
            formen.Add((EffektZeichen.Punkt, false));
        }
        foreach (var (s, _, _) in AussenBearbeitung.Alle(a).Where(x => x.Stelle.Art == AussenArt.Katalog && Zeigt(Ebene.Katalog)).ToList())
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
        foreach (var l in land.OfType<PyObjekt>().Where(l => l["typ"] as string is "ortsname" or "beschriftung" && Zeigt(Ebene.Beschriftungen)))
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

        // Welle S 0.22.0: Zahl der Aufgaben der Spielgeschichte als Schild oben rechts auf der Kante des Hauses (ragt
        // nach oben hinaus, damit es den Namen im Haus nicht verdeckt), ganz in der Karte wie die Beschriftungen
        var gezeichnet = new List<(string, int, Rect, Rect)>();
        foreach (var (ort, zahl, _, r) in zahlen)
        {
            var ft = new FormattedText(zahl.ToString(CultureInfo.InvariantCulture), CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight,
                new Typeface(schrift.FontFamily, FontStyle.Normal, FontWeight.Bold), ZahlGroesse, Farbe("on_accent"));
            var hoehe = ft.Height + 4;
            var breite = Math.Max(hoehe, ft.Width + 14);
            var (zx, zy, _) = Beschriftung.InKarte(r.Right - breite - 4, r.Top - hoehe + 1, breite, hoehe, rand.Left, rand.Top, rand.Right, rand.Bottom);
            var schild = new Rect(zx, zy, breite, hoehe);
            ctx.DrawRectangle(Farbe("accent"), new Pen(Farbe("on_accent"), 1.5), schild, hoehe / 2, hoehe / 2);
            ctx.DrawText(ft, new Point(zx + ((breite - ft.Width) / 2), zy + 2));
            gezeichnet.Add((ort, zahl, schild, r));
        }
        ZahlenAmHaus = gezeichnet;

        // Welle Q1: Questgeber draussen (gruen, Pfeil = Blick)
        foreach (var q in QuestgeberMarken)
        {
            q.Zeichnen(ctx, new Point((q.X * _zoom) + _versatz.X, (q.Y * _zoom) + _versatz.Y), Math.Max(6, 0.2 * _zoom), schrift, Farbe("text"));
        }

        // S 0.27.0: Notizen oben auf allem
        if (Zeigt(Ebene.Notizen))
        {
            NotizenZeichnen(ctx, a, schrift, dx, dy);
        }

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
                case AussenArt.Zebrastreifen when Zebras(a, dx, dy).FirstOrDefault(e => e.Stelle == aw).Zebra is { } zw:
                    Flaeche(ctx, ZebraUmriss(zw), null, stift);
                    break;
                case AussenArt.Haltestelle or AussenArt.Parkplatz when AussenBearbeitung.HalteZumZeichnen(a, aw, dx, dy).FirstOrDefault(e => e.Stelle == aw) is var (_, haltW, parkW):
                    if (haltW is { } hz)
                    {
                        var l2 = hz.Fahrt * (Lernplattform.Core.Spiel.Probe.Haltestellen.ZickzackLang / 2);
                        var c = hz.Mitte + (hz.Fahrt * (Lernplattform.Core.Spiel.Probe.Haltestellen.SchildVor - (Lernplattform.Core.Spiel.Probe.Haltestellen.ZickzackLang / 2)));
                        var innen = c + (hz.Aussen * 0.02f);
                        var aussenK = c + (hz.Aussen * (hz.Halb + Lernplattform.Core.Spiel.Probe.Haltestellen.HaeuschenAbstand + Lernplattform.Core.Spiel.Probe.Haltestellen.HaeuschenTief));
                        Flaeche(ctx, [innen - l2, innen + l2, aussenK + l2, aussenK - l2], null, stift);
                    }
                    else if (parkW is { } pz)
                    {
                        Flaeche(ctx, pz.Ecken(), null, stift);
                    }
                    break;
                case AussenArt.Ampel when Ampeln(a, dx, dy).FirstOrDefault(e => e.Stelle == aw).Ampel is { } am:
                    ctx.DrawEllipse(null, stift, P(am.Mitte.X, am.Mitte.Y), AussenBearbeitung.AmpelGreifen * _zoom, AussenBearbeitung.AmpelGreifen * _zoom);
                    break;
                case AussenArt.Haus or AussenArt.Gebaeude when AussenBearbeitung.HausGrad(a, aw) != 0:
                    // Welle A 0.70.0: Rahmen um das fein gedrehte Haus (3 Bildpunkte Abstand wie beim Rechteck)
                    var hl = AussenBearbeitung.HausLage(a, aw);
                    var (vx, vy, vw, vh) = AussenBearbeitung.HausViertel(a, aw);
                    var hrand = 3 / Math.Max(_zoom, 1e-6);
                    var hv = new System.Numerics.Vector2((float)dx, (float)dy);
                    Flaeche(ctx, [.. hl.Ecken(vx - hrand, vy - hrand, vx + vw + hrand, vy + vh + hrand).Select(e => e + hv)], null, stift);
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
                case AussenArt.Notiz:
                    ctx.DrawRectangle(null, stift, NotizRechteck(a, aw, dx, dy).Inflate(3));
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

        // Welle D2: Waermekarte ueber der Stadt, unter Funden und Auswahl-Hilfen
        WaermeZeichnen(ctx);
        // Funde mit Stelle
        foreach (var f in Zeigt(Ebene.Funde) ? Funde : [])
        {
            if (f.X is { } fx && f.Y is { } fy)
            {
                var stift = new Pen(Pinsel(f.Rot ? "#E53935" : "#F9A825"), 3);
                ctx.DrawEllipse(null, stift, P(fx, fy), Math.Max(10, 0.45 * _zoom), Math.Max(10, 0.45 * _zoom));
            }
        }
        MehrfachZeichnen(ctx, a);
        ReiheZeichnen(ctx);
        ZiehVorschauZeichnen(ctx);
        AussehenVorschauZeichnen(ctx);
        EinfuegenVorschauZeichnen(ctx);
        LaterneVorschlagZeichnen(ctx);
        MassbandZeichnen(ctx);
        PinselZeichnen(ctx);
        TuerZeichnen(ctx);
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

    /// <summary>Alle Zebrastreifen mit Lage; der ausgewaehlte waehrend des Ziehens um den Anteil laengs der Strasse verschoben.</summary>
    private List<(AussenStelle Stelle, Lernplattform.Core.Spiel.Probe.Zebra Zebra)> Zebras(PyObjekt a, double dx, double dy)
    {
        var liste = new List<(AussenStelle, Lernplattform.Core.Spiel.Probe.Zebra)>();
        for (var i = 0; i < AussenBearbeitung.ZebraListe(a).Count; i++)
        {
            var s = new AussenStelle(AussenArt.Zebrastreifen, i);
            if (AussenBearbeitung.ZebraVon(a, s) is { } z)
            {
                if (s == Auswahl && (dx != 0 || dy != 0))
                {
                    z = z with { Mitte = z.Mitte + (z.Richtung * System.Numerics.Vector2.Dot(new System.Numerics.Vector2((float)dx, (float)dy), z.Richtung)) };
                }
                liste.Add((s, z));
            }
        }
        return liste;
    }

    /// <summary>Alle Ampeln mit Armen; die ausgewaehlte waehrend des Ziehens an der Kreuzung, an die sie springen wuerde.</summary>
    private static List<(AussenStelle Stelle, Lernplattform.Core.Spiel.Probe.Ampel Ampel)> AmpelnRoh(PyObjekt a, AussenStelle? auswahl, double dx, double dy)
    {
        var liste = new List<(AussenStelle, Lernplattform.Core.Spiel.Probe.Ampel)>();
        for (var i = 0; i < AussenBearbeitung.AmpelListe(a).Count; i++)
        {
            var s = new AussenStelle(AussenArt.Ampel, i);
            if (AussenBearbeitung.AmpelVon(a, s) is not { } amp)
            {
                continue;
            }
            if (s == auswahl && (dx != 0 || dy != 0) && AussenBearbeitung.AmpelVorschau(a, amp.Mitte.X + dx, amp.Mitte.Y + dy) is { } neu)
            {
                amp = neu with { Nr = amp.Nr };
            }
            liste.Add((s, amp));
        }
        return liste;
    }

    private List<(AussenStelle Stelle, Lernplattform.Core.Spiel.Probe.Ampel Ampel)> Ampeln(PyObjekt a, double dx, double dy) => AmpelnRoh(a, Auswahl, dx, dy);

    /// <summary>Umriss eines Zebrastreifens (ueber die ganze Fahrbahn, Breite laengs der Strasse).</summary>
    internal static List<System.Numerics.Vector2> ZebraUmriss(Lernplattform.Core.Spiel.Probe.Zebra z)
    {
        var l = z.Richtung * (Lernplattform.Core.Spiel.Probe.Zebrastreifen.Breite / 2);
        var q = z.Quer * (StadtSzene.StrassenBreite / 2);
        return [z.Mitte - l - q, z.Mitte + l - q, z.Mitte + l + q, z.Mitte - l + q];
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
            Flaeche(ctx, AussenBearbeitung.HausEcken(v.Stand, s), Pinsel(farbe, 0.55), new Pen(Pinsel("#111827"), 1.5, new DashStyle([4, 3], 0)));
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
        if (s != TuerFuer)
        {
            // S 0.28.0: ein anderes Stueck gewaehlt = Tuer-Setzen endet
            TuerSetzenBeenden();
        }
        Auswahl = s;
        _gruppe = [];
        AuswahlGeaendert?.Invoke();
        // Sammelliste 277: die Fassade fuer „Tür setzen“ schon jetzt im Hintergrund rechnen
        FassadeVorrechnen(s);
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
        else if (art == AussenArt.Zebrastreifen)
        {
            Gemeldet?.Invoke(Texte.ZebraKeineStrasse);
        }
        else if (art == AussenArt.Ampel)
        {
            Gemeldet?.Invoke(Texte.AmpelKeineKreuzung);
        }
        else if (art is AussenArt.Haltestelle or AussenArt.Parkplatz)
        {
            Gemeldet?.Invoke(Texte.HaltKeineStrasse);
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
        AussenArt.Zebrastreifen => Texte.Zebrastreifen,
        AussenArt.Notiz => Texte.Notiz,
        AussenArt.Ampel => Texte.Ampel,
        AussenArt.Haltestelle => Texte.Haltestelle,
        AussenArt.Parkplatz => Texte.Parkplatz,
        AussenArt.Huegel => Texte.Huegel,
        AussenArt.Hoehe => Texte.GemalteHoehen,
        _ => Texte.Paketplatz,
    };

    /// <summary>Ausgewaehltes Stueck um (dx, dy) verschieben (Verlauf, automatisch gespeichert).</summary>
    public void Verschieben(double dx, double dy)
    {
        if (GruppeAktiv)
        {
            GruppeVerschieben(dx, dy);
            return;
        }
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
        if (GruppeAktiv)
        {
            GruppeDrehen(rueckwaerts);
            return;
        }
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
        if (Stand is { } stand && AussenBearbeitung.DrehenNicht(stand, s) is { } grund)
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
            if (Stand is not { } a || _projekt is null || _projekt.NurLesen || SetzenArt is not null || TuerFuer is not null || Auswahl is not { } s
                || !AussenBearbeitung.Gueltig(a, s) || AussenBearbeitung.DrehenNicht(a, s) is not null || (AussenBearbeitung.MitEckpunkten(s.Art) && s.Punkt >= 0))
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
            var gt = (grad > 0 ? "+" : "") + grad.ToString(CultureInfo.InvariantCulture) + "°";
            var ft = new FormattedText(gt, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 12, Farbe("text"));
            Anzeigen.Zeichnen(ctx, ft, gt, new Point(gx + 16, gy - 8), default, null, null, 0, VerdecktFuer(this), Bounds.Size, _anzeigen);
        }
    }

    public void Kopieren()
    {
        if (GruppeAktiv)
        {
            GruppeKopieren();
            return;
        }
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
        if (GruppeAktiv)
        {
            GruppeLoeschen();
            return;
        }
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
            case Key.Escape when ReiheAbbrechen():
                return true;
            case Key.Escape when TuerFuer is not null:
                TuerSetzenBeenden();
                Gemeldet?.Invoke(Texte.TuerSetzenBeenden);
                return true;
            case Key.Escape when GelaendePinsel is not null:
                if (_pinselZug.Count > 0)
                {
                    _pinselZug.Clear();
                    InvalidateVisual();
                }
                else
                {
                    GelaendePinsel = null;
                }
                return true;
            case Key.Escape when MassbandAn:
                // S 0.27.0: erst die Messung leeren, dann das Massband beenden
                if (Massband.Anfang is not null)
                {
                    Massband.Leeren();
                    MassbandMelden();
                    InvalidateVisual();
                }
                else
                {
                    MassbandAn = false;
                }
                return true;
            case Key.Escape:
                if (SetzenArt is null && Gewaehlt.Count == 0)
                {
                    // S 0.30.0: nichts mehr gewaehlt, dann endet „Mehrere wählen“
                    MehrereWaehlen = false;
                }
                SetzenBeenden();
                Waehlen(null);
                return true;
            case Key.U when !strg:
                MehrereWaehlen = !MehrereWaehlen;
                return true;
            case Key.A when strg:
                AlleWaehlen();
                return true;
            case Key.Delete or Key.Back when Gewaehlt.Count > 0:
                Loeschen();
                return true;
            case Key.R when !strg && DrehbarBeimSetzen(SetzenArt):
                SetzenDrehen(mod.HasFlag(KeyModifiers.Shift));
                return true;
            case Key.R when !strg && Gewaehlt.Count > 0:
                Drehen(mod.HasFlag(KeyModifiers.Shift));
                return true;
            case Key.D when strg && Gewaehlt.Count > 0:
                Kopieren();
                return true;
            case Key.M when !strg && SetzenArt == AussenArt.Kurve:
                SetzenSpiegeln();
                return true;
            case Key.M when !strg && Auswahl is { } kurve && _projekt is not null && AussenBearbeitung.IstKurve(_projekt.AussenStand, kurve):
                Spiegeln();
                return true;
            case Key.Left when Gewaehlt.Count > 0:
                Verschieben(-schritt, 0);
                return true;
            case Key.Right when Gewaehlt.Count > 0:
                Verschieben(schritt, 0);
                return true;
            case Key.Up when Gewaehlt.Count > 0:
                Verschieben(0, -schritt);
                return true;
            case Key.Down when Gewaehlt.Count > 0:
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
            _reiheAnfangInDieserGeste = false;
            _mehrfachDruck = null;
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
        if (GelaendePinsel is not null)
        {
            // S 0.42.0: ein Finger malt einen Zug (wie ein Stueck ziehen), zwei Finger bleiben bei der Ansicht
            var malt = _projekt?.NurLesen == false;
            if (erster && malt)
            {
                PinselDruecken(p);
            }
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, malt));
            return;
        }
        if (QuestgeberSetzen is not null || TippWahl is not null)
        {
            // Welle Q1: Platz eines Questgebers draussen waehlen; Tippen setzt (beim Loslassen), Ziehen schiebt die Ansicht
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, false));
            return;
        }
        // S 0.28.0: Tuer setzen (Knopf) oder die Tuer des gewaehlten Hauses direkt greifen; ein Finger zieht sie an der Wand
        if (TuerFuer is not null || (erster && TuerDirektGreifen(p)))
        {
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, _projekt?.NurLesen == false));
            if (erster && TuerFuer is not null)
            {
                TuerVorschauAn(w.X, w.Y);
            }
            return;
        }
        var s = SetzenArt is null && !MassbandAn ? StueckAn(a, p, Math.Max(0.3, 10 / _zoom)) : null;
        if (erster && SetzenArt is null && !MassbandAn && MehrfachDrucken(e, s, p))
        {
            // S 0.30.0: Umschalt, „Mehrere wählen“ oder ein Stueck der Gruppe; ein Finger zieht Gruppe oder Rahmen
            Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, true));
            return;
        }
        // Welle Z: beim Ziehen zieht ein Finger vom Anfang zum Ende (wie ein Stueck), zwei Finger bleiben bei der Ansicht;
        // S 0.30.0: eine Reihe ebenso
        var zieht = (ZiehArtAktiv is not null || ReiheAktiv) && _projekt?.NurLesen == false;
        Anwenden(_geste.Druecken(e.Pointer.Id, p.X, p.Y, zieht || (s is not null && _projekt?.NurLesen == false)));
        // nur der erste Finger waehlt (S 0.5.1: ein zweiter Finger aendert die Auswahl nie); das Massband waehlt nie
        if (!erster || SetzenArt is not null || MassbandAn)
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
        EinfuegenSchwebenMaus(e, p);
        if (!_geste.Aktiv && MassbandAn && e.Pointer.Type == PointerType.Mouse && Stand is { } ma)
        {
            // S 0.27.0: Maus schwebt, das Ende des Massbands folgt
            var mw = WeltPunkt(p);
            Massband.Schweben(ma, mw.X, mw.Y, Massband.FangBildpunkte / _zoom, Raster, AusArten);
            InvalidateVisual();
        }
        if (!_geste.Aktiv && ReiheAktiv && ReiheAnfang is not null && e.Pointer.Type == PointerType.Mouse)
        {
            var rw = WeltPunkt(p);
            ReiheZielSetzen(rw.X, rw.Y);
        }
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
        if (TuerFuer is not null && TuerAnwenden(s))
        {
            return;
        }
        if (GelaendePinsel is not null && PinselAnwenden(s))
        {
            return;
        }
        if (ZiehArtAktiv is not null && ZiehenAnwenden(s))
        {
            return;
        }
        if (ReiheAktiv && ReiheAnwenden(s))
        {
            return;
        }
        if (_mehrfachDruck is not null && MehrfachAnwenden(s))
        {
            return;
        }
        switch (s.Art)
        {
            case Fingergeste.Art.Tippen when (QuestgeberSetzen ?? TippWahl) is { } qs && !_nurSchieben:
            {
                var qw = WeltPunkt(new Point(s.X, s.Y));
                qs(Math.Round(qw.X, 2), Math.Round(qw.Y, 2));
                break;
            }
            case Fingergeste.Art.Tippen when MassbandAn && !_nurSchieben:
            {
                var mw = WeltPunkt(new Point(s.X, s.Y));
                MassbandTippen(mw.X, mw.Y);
                break;
            }
            case Fingergeste.Art.Tippen when MassbandAn && _rechtsDruck:
                // Rechtsklick ohne Bewegung beendet die Messung wie Escape
                Taste(Key.Escape, KeyModifiers.None);
                break;
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
            if (StueckAn(a, bild, 6 / _zoom) is { } s && s.Art is AussenArt.Haus or AussenArt.Gebaeude or AussenArt.Katalog or AussenArt.Notiz)
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
