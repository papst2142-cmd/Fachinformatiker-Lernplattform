using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.27.0 (Weisung P12-118 Teil B, Kurzplan Meldung P12-79 Wuensche 3, 4, 5): Massband, Ebenen ein/aus und
/// Notizen in der Draufsicht.
/// </summary>
public sealed partial class KartenFlaeche
{
    // ---- Ebenen ----------------------------------------------------------------------------------

    private IReadOnlySet<Ebene> _ebenenAus = new HashSet<Ebene>();
    private IReadOnlySet<AussenArt> _ausArten = new HashSet<AussenArt>();

    /// <summary>Ausgeblendete Ebenen (nur Draufsicht). Ein ausgeblendetes gewaehltes Stueck wird abgewaehlt.</summary>
    public IReadOnlySet<Ebene> EbenenAus
    {
        get => _ebenenAus;
        set
        {
            _ebenenAus = value.ToHashSet();
            _ausArten = Ebenen.Arten(_ebenenAus);
            if (Auswahl is { } s && _ausArten.Contains(s.Art))
            {
                Waehlen(null);
            }
            InvalidateVisual();
        }
    }

    /// <summary>Stuecke, die gerade nicht anwaehlbar sind (ausgeblendete Ebenen).</summary>
    public IReadOnlySet<AussenArt> AusArten => _ausArten;

    /// <summary>Ist die Ebene zu sehen?</summary>
    public bool Zeigt(Ebene e) => !_ebenenAus.Contains(e);

    // ---- Massband --------------------------------------------------------------------------------

    /// <summary>Die Messung (gilt fuer die Draufsicht und die 3D-Ansicht).</summary>
    public Massband Massband { get; } = new();

    private bool _massbandAn;

    /// <summary>Massband an: Tippen setzt Anfang und Ende, waehlt und setzt nichts. Kein Schritt im Verlauf.</summary>
    public bool MassbandAn
    {
        get => _massbandAn;
        set
        {
            if (_massbandAn == value)
            {
                return;
            }
            _massbandAn = value;
            Massband.Leeren();
            if (value)
            {
                GelaendePinsel = null;
                SetzenBeenden();
                Waehlen(null);
            }
            MassbandGeaendert?.Invoke();
            Gemeldet?.Invoke(value ? Texte.MassbandAnfang : Texte.MassbandAus);
            InvalidateVisual();
        }
    }

    /// <summary>Massband an/aus oder ein neuer Punkt (Knopf markieren, 3D neu zeichnen).</summary>
    public event Action? MassbandGeaendert;

    /// <summary>Gemessene Laenge als Text („3,5 Felder“), null ohne fertige Messung.</summary>
    public string? MassbandText => Massband.Ende is not null && Massband.Laenge is { } l ? Massband.Text(l) : null;

    /// <summary>Ein Tipp mit dem Massband an der Weltstelle (x, y); Fangweite aus dem Massstab der Ansicht.</summary>
    /// <param name="fang">Fangweite in Karten-Einheiten (null = aus dem Massstab der Draufsicht).</param>
    /// <param name="dreiD">aus der 3D-Ansicht: dort gelten die Ebenen der Draufsicht nicht, nur Notizen fangen nie.</param>
    public void MassbandTippen(double x, double y, double? fang = null, bool dreiD = false)
    {
        if (Stand is not { } a || !MassbandAn)
        {
            return;
        }
        Massband.Tippen(a, x, y, fang ?? (Massband.FangBildpunkte / _zoom), Raster, dreiD ? AussenBearbeitung.NurDraufsicht : AusArten);
        MassbandMelden();
        MassbandGeaendert?.Invoke();
        InvalidateVisual();
    }

    /// <summary>3D: Vorschau des Endes, waehrend die Maus schwebt.</summary>
    public void MassbandSchweben3D(double x, double y, double fang)
    {
        if (Stand is { } a && MassbandAn)
        {
            Massband.Schweben(a, x, y, fang, Raster, AussenBearbeitung.NurDraufsicht);
        }
    }

    private void MassbandMelden() =>
        Gemeldet?.Invoke(MassbandText is { } t ? Texte.MassbandErgebnis(t) : Massband.Anfang is null ? Texte.MassbandAnfang : Texte.MassbandEnde);

    /// <summary>Bildlage der Laengen-Beschriftung beim letzten Zeichnen (Start-Test).</summary>
    public Rect MassbandSchild { get; private set; }

    private static readonly IBrush MassbandFarbe = new SolidColorBrush(Color.Parse("#E08A00"));
    private static readonly IBrush MassbandSchrift = new SolidColorBrush(Color.Parse("#3B2A00"));
    private static readonly IBrush MassbandGrund = new SolidColorBrush(Color.Parse("#FFF8E1"));

    private void MassbandZeichnen(DrawingContext ctx)
    {
        MassbandSchild = default;
        if (!MassbandAn || Massband.Anfang is not { } a)
        {
            return;
        }
        MassbandSchild = MassbandLinie(ctx, P(a.X, a.Y), (Massband.Ende ?? Massband.Schwebe) is { } e ? P(e.X, e.Y) : null, Massband.Laenge, Bounds, VerdecktFuer(this), _anzeigen);
    }

    /// <summary>
    /// Massband zeichnen (Draufsicht und 3D): orange Linie mit weissem Rand und Querstrichen an den Enden, Schild mit der
    /// Laenge ueber der Mitte. Nur ein Anfang: ein Querstrich. Rueckgabe: Lage des Schilds (leer ohne Ende).
    /// </summary>
    internal static Rect MassbandLinie(DrawingContext ctx, Point anfang, Point? ende, double? laenge, Rect flaeche,
        IReadOnlyList<Rect> verdeckt, List<(string Text, Rect Rahmen)> merken)
    {
        var rand = new Pen(Brushes.White, 6, lineCap: PenLineCap.Round);
        var linie = new Pen(MassbandFarbe, 3, lineCap: PenLineCap.Round);
        var (qx, qy) = (0.0, 9.0);
        if (ende is { } e && (e - anfang) is var d && Math.Sqrt((d.X * d.X) + (d.Y * d.Y)) is var l && l > 0.5)
        {
            (qx, qy) = (-d.Y / l * 9, d.X / l * 9);
        }
        void Quer(Point p)
        {
            ctx.DrawLine(rand, new Point(p.X - qx, p.Y - qy), new Point(p.X + qx, p.Y + qy));
            ctx.DrawLine(linie, new Point(p.X - qx, p.Y - qy), new Point(p.X + qx, p.Y + qy));
        }
        if (ende is not { } en)
        {
            Quer(anfang);
            return default;
        }
        ctx.DrawLine(rand, anfang, en);
        Quer(anfang);
        Quer(en);
        ctx.DrawLine(linie, anfang, en);
        return MassbandSchildZeichnen(ctx, new Point((anfang.X + en.X) / 2, (anfang.Y + en.Y) / 2), Massband.Text(laenge ?? 0), flaeche, verdeckt, merken);
    }

    /// <summary>Schild mit der Laenge, ganz in der Flaeche.</summary>
    private static Rect MassbandSchildZeichnen(DrawingContext ctx, Point mitte, string text, Rect flaeche,
        IReadOnlyList<Rect> verdeckt, List<(string Text, Rect Rahmen)> merken)
    {
        var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 15, MassbandSchrift);
        var (b, h) = (ft.Width + 18, ft.Height + 8);
        var x = Math.Clamp(mitte.X - (b / 2), 4, Math.Max(4, flaeche.Width - b - 4));
        var y = Math.Clamp(mitte.Y - h - 10, 4, Math.Max(4, flaeche.Height - h - 4));
        return Anzeigen.Zeichnen(ctx, ft, text, new Point(x, y), new Size(9, 4), MassbandGrund, new Pen(MassbandFarbe, 1.5), 7, verdeckt, flaeche.Size, merken);
    }

    // ---- Notizen ---------------------------------------------------------------------------------

    /// <summary>Gezeichnete Notizen (Bildflaeche) von unten nach oben, fuer Tippen und Start-Test.</summary>
    public IReadOnlyList<(AussenStelle Stelle, Rect Flaeche, string Text)> NotizFlaechen { get; private set; } = [];

    /// <summary>Hoechstens so breit (Bildpunkte) ist der Text einer Notiz; laengerer bricht um.</summary>
    public const double NotizBreite = 170;

    private static readonly IBrush NotizGelb = new SolidColorBrush(Color.Parse("#FCEB8C"));
    private static readonly IBrush NotizRand = new SolidColorBrush(Color.Parse("#B08900"));

    private FormattedText NotizTextForm(string text) =>
        new(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 13, Brushes.Black)
        {
            MaxTextWidth = NotizBreite,
            MaxLineCount = 4,
            Trimming = TextTrimming.CharacterEllipsis,
        };

    /// <summary>Rechteck einer Notiz im Bild (Mitte = Lage der Notiz, waehrend des Ziehens verschoben).</summary>
    private Rect NotizRechteck(PyObjekt a, AussenStelle s, double dx, double dy)
    {
        var n = Objekt(AussenBearbeitung.NotizListe(a)[s.Nummer]);
        var ft = NotizTextForm(n["text"] as string ?? "");
        var m = P(Py.F(n["x"]) + dx, Py.F(n["y"]) + dy);
        var (b, h) = (Math.Max(28, ft.Width + 14), ft.Height + 10);
        return new Rect(m.X - (b / 2), m.Y - (h / 2), b, h);
    }

    private void NotizenZeichnen(DrawingContext ctx, PyObjekt a, Typeface schrift, double dx, double dy)
    {
        var flaechen = new List<(AussenStelle, Rect, string)>();
        var liste = AussenBearbeitung.NotizListe(a);
        for (var i = 0; i < liste.Count; i++)
        {
            var s = new AussenStelle(AussenArt.Notiz, i);
            var text = AussenBearbeitung.NotizText(a, s);
            var r = NotizRechteck(a, s, s == Auswahl ? dx : 0, s == Auswahl ? dy : 0);
            // Zettel mit kleinem Schatten und Ecke
            ctx.DrawRectangle(new SolidColorBrush(Colors.Black, 0.18), null, r.Translate(new Vector(2, 2)), 3, 3);
            ctx.DrawRectangle(NotizGelb, new Pen(NotizRand, 1.2), r, 3, 3);
            ctx.DrawText(NotizTextForm(text), r.TopLeft + new Point(7, 5));
            flaechen.Add((s, r, text));
        }
        NotizFlaechen = flaechen;
    }

    /// <summary>Notiz unter dem Bildpunkt (die zuletzt gezeichnete oben), sonst null; ausgeblendet: null.</summary>
    private AussenStelle? NotizAn(Point bild) =>
        Zeigt(Ebene.Notizen) ? NotizFlaechen.LastOrDefault(n => n.Flaeche.Contains(bild)).Stelle : null;

    /// <summary>Stueck unter dem Bildpunkt: zuerst eine gezeichnete Notiz, sonst wie <see cref="AussenBearbeitung.StueckBei"/> ohne Ausgeblendetes.</summary>
    private AussenStelle? StueckAn(PyObjekt a, Point bild, double greifen)
    {
        if (NotizAn(bild) is { } n)
        {
            return n;
        }
        var w = WeltPunkt(bild);
        return AussenBearbeitung.StueckBei(a, w.X, w.Y, greifen, AusArten);
    }
}
