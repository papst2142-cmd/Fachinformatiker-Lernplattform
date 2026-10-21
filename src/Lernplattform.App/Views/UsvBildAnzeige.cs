using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 3, W2 (Paket R6): Bild des USV-Rechners wie Python (PC fisi_widgets.UpsDiagram, Handy
/// ui.UpsPicture): Last -> USV -> Akku als Kaesten (nebeneinander, wenn sie passen, sonst
/// untereinander), in der USV die Auslastung mit Strich bei der Grenze, darunter die Laufzeit
/// als Balken mit Namen und Minuten, unten die Empfehlung. Daten: Core Usv.Berechnen().Bild;
/// fuer Bildschirmleser der Vorlesetext ups_picture_summary. Ohne Bild leer (nach Fehleingabe).
/// </summary>
public sealed class UsvBildAnzeige : StackPanel
{
    /// <summary>UPS_BAR_COLORS (fisi_widgets 2005, ui.py 1371).</summary>
    private static readonly IReadOnlyDictionary<string, string> Balkenfarben = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ziel"] = "accent", ["neu"] = "purple", ["ok"] = "green", ["knapp"] = "red",
    };

    public UsvBildAnzeige()
    {
        Spacing = 6;
    }

    /// <summary>Zuletzt gezeigtes Bild (null = leer).</summary>
    public UsvBild? Bild { get; private set; }

    public void Setzen(UsvBild? bild)
    {
        Bild = bild;
        Children.Clear();
        Avalonia.Automation.AutomationProperties.SetName(this, bild is null ? "" : Usv.Vorlesetext(bild));
        if (bild is null)
        {
            return;
        }
        var reihe = new KastenReihe();
        var kaesten = new (string Titel, string Wert, string Detail, string Farbe, double? Anteil)[]
        {
            (RechnerTexte.BildLast, bild.Last, bild.LastDetail, "accent", null),
            (RechnerTexte.BildUsv, bild.Usv, bild.UsvDetail, "purple", bild.Auslastung),
            (RechnerTexte.BildAkku, bild.Akku, bild.AkkuDetail, "green", null),
        };
        for (var i = 0; i < kaesten.Length; i++)
        {
            var (titel, wert, detail, farbe, anteil) = kaesten[i];
            var zeilen = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    Schrift.Text(titel, "Klein", FontWeight.Bold, "Pinsel.muted"),
                    new WortText(wert, "Gross", FontWeight.Bold, "Pinsel.text"),
                    Schrift.Text(detail, "Klein", FontWeight.Normal, "Pinsel.text_dim"),
                },
            };
            if (anteil is { } a)
            {
                zeilen.Children.Add(Auslastung(a, bild.Grenze));
            }
            var strich = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 12, 0) }
                .Farbe(Border.BackgroundProperty, "Pinsel." + farbe);
            var innen = new DockPanel { Children = { strich, zeilen } };
            DockPanel.SetDock(strich, Dock.Left);
            reihe.Children.Add(new Border
            {
                Child = innen,
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
            }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border"));
            if (i < kaesten.Length - 1)
            {
                reihe.Children.Add(new Pfeil());
            }
        }
        Children.Add(reihe);
        var balken = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var b in bild.Balken)
        {
            var kopf = new TitelUndWert(120);
            kopf.Children.Add(new WortText(b.Beschriftung, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
            kopf.Children.Add(Schrift.Text(b.Text, "Klein", FontWeight.Normal, "Pinsel.text"));
            balken.Children.Add(kopf);
            balken.Children.Add(Balken(b.Anteil, "Pinsel." + Balkenfarben[b.Art], 12, b.Art == "knapp"));
        }
        Children.Add(balken);
        if (bild.Urteil is { } urteil)
        {
            Children.Add(Schrift.Text(Beschriftungen.Einsetzen(RechnerTexte.BildEmpfehlung, urteil), "Mittel", FontWeight.Bold,
                urteil == "passend" ? "Pinsel.green" : "Pinsel.red"));
        }
    }

    /// <summary>Balken wie ui.UpsPicture._bar: unendlich = voll, NaN = leer; "knapp" zusaetzlich blass (wie die Schraffur am PC).</summary>
    private static Control Balken(double anteil, string farbe, double hoehe, bool knapp = false)
    {
        if (double.IsNaN(anteil))
        {
            anteil = 0;
        }
        else if (double.IsInfinity(anteil))
        {
            anteil = anteil > 0 ? 1 : 0;
        }
        var voll = Math.Clamp((int)Math.Round(anteil * 1000, MidpointRounding.ToEven), 0, 1000);
        var raster = new Grid { Height = hoehe, ClipToBounds = true };
        raster.ColumnDefinitions.Add(new ColumnDefinition(voll, GridUnitType.Star));
        raster.ColumnDefinitions.Add(new ColumnDefinition(1000 - voll, GridUnitType.Star));
        if (voll > 0)
        {
            raster.Children.Add(new Border { Opacity = knapp ? 0.55 : 1 }.Farbe(Border.BackgroundProperty, farbe));
        }
        if (voll < 1000)
        {
            var rest = new Border().Farbe(Border.BackgroundProperty, "Pinsel.ring_bg");
            Grid.SetColumn(rest, 1);
            raster.Children.Add(rest);
        }
        return new Border { Child = raster, CornerRadius = new CornerRadius(hoehe / 2), ClipToBounds = true };
    }

    /// <summary>Auslastung der USV mit Strich bei der Grenze (80 %).</summary>
    private static Control Auslastung(double anteil, int grenze)
    {
        var gut = anteil <= grenze;
        var balken = Balken(Math.Min(anteil, 100) / 100.0, gut ? "Pinsel.green" : "Pinsel.red", 6);
        balken.Margin = new Thickness(0, 3, 0, 3);
        var marke = new Grid();
        marke.ColumnDefinitions.Add(new ColumnDefinition(grenze, GridUnitType.Star));
        marke.ColumnDefinitions.Add(new ColumnDefinition(2, GridUnitType.Pixel));
        marke.ColumnDefinitions.Add(new ColumnDefinition(100 - grenze, GridUnitType.Star));
        var strich = new Border { Height = 12 }.Farbe(Border.BackgroundProperty, "Pinsel.text");
        Grid.SetColumn(strich, 1);
        marke.Children.Add(strich);
        return new Panel { Height = 12, Margin = new Thickness(0, 4, 0, 0), Children = { balken, marke } };
    }

    /// <summary>Pfeil zwischen zwei Kaesten: nach rechts (nebeneinander) oder nach unten (untereinander).</summary>
    private sealed class Pfeil : Panel
    {
        private readonly Avalonia.Controls.Shapes.Path _pfad = new() { StrokeThickness = 2, Stretch = Stretch.None };

        public Pfeil()
        {
            _pfad.Farbe(Shape.StrokeProperty, "Pinsel.muted");
            Children.Add(_pfad);
        }

        public bool Waagrecht
        {
            set => _pfad.Data = Geometry.Parse(value ? "M 0,9 L 20,9 M 13,3 L 20,9 L 13,15" : "M 9,0 L 9,16 M 3,9 L 9,16 L 15,9");
        }
    }

    /// <summary>Kaesten nebeneinander, solange jeder mindestens 170 x Schriftfaktor breit ist, sonst untereinander.</summary>
    private sealed class KastenReihe : Panel
    {
        private const double PfeilBreite = 34;
        private const double PfeilHoehe = 26;
        private bool _waagrecht;

        private IEnumerable<Control> Kaesten => Children.Where(k => k is not Pfeil);

        protected override Size MeasureOverride(Size verfuegbar)
        {
            var anzahl = Kaesten.Count();
            _waagrecht = !double.IsInfinity(verfuegbar.Width)
                && (verfuegbar.Width - PfeilBreite * (anzahl - 1)) / anzahl >= 170 * Schrift.Faktor;
            foreach (var pfeil in Children.OfType<Pfeil>())
            {
                pfeil.Waagrecht = _waagrecht;
                pfeil.Measure(new Size(PfeilBreite, PfeilHoehe));
            }
            if (_waagrecht)
            {
                var breite = (verfuegbar.Width - PfeilBreite * (anzahl - 1)) / anzahl;
                double hoehe = 0;
                foreach (var k in Kaesten)
                {
                    k.Measure(new Size(breite, double.PositiveInfinity));
                    hoehe = Math.Max(hoehe, k.DesiredSize.Height);
                }
                return new Size(verfuegbar.Width, hoehe);
            }
            double gesamt = 0;
            foreach (var k in Kaesten)
            {
                k.Measure(new Size(verfuegbar.Width, double.PositiveInfinity));
                gesamt += k.DesiredSize.Height;
            }
            return new Size(double.IsInfinity(verfuegbar.Width) ? 0 : verfuegbar.Width, gesamt + PfeilHoehe * (anzahl - 1));
        }

        protected override Size ArrangeOverride(Size endgroesse)
        {
            var anzahl = Kaesten.Count();
            if (_waagrecht)
            {
                var breite = (endgroesse.Width - PfeilBreite * (anzahl - 1)) / anzahl;
                var hoehe = endgroesse.Height;
                double x = 0;
                foreach (var kind in Children)
                {
                    if (kind is Pfeil)
                    {
                        kind.Arrange(new Rect(x + 7, (hoehe - 18) / 2, PfeilBreite - 7, 18));
                        x += PfeilBreite;
                        continue;
                    }
                    kind.Arrange(new Rect(x, 0, breite, hoehe));
                    x += breite;
                }
                return endgroesse;
            }
            double y = 0;
            foreach (var kind in Children)
            {
                if (kind is Pfeil)
                {
                    kind.Arrange(new Rect((endgroesse.Width - 18) / 2, y + 5, 18, PfeilHoehe - 5));
                    y += PfeilHoehe;
                    continue;
                }
                kind.Arrange(new Rect(0, y, endgroesse.Width, kind.DesiredSize.Height));
                y += kind.DesiredSize.Height;
            }
            return endgroesse;
        }
    }
}
