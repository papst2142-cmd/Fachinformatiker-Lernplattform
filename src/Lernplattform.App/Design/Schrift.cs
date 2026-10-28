using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lernplattform.Design;

/// <summary>
/// Phase 1, K5: Schriftstufen (Normal 1,0 bis Maximal 2,0, wie fisi_theme.FONT_SIZES).
/// Alle Schriftgroessen kommen ueber Ressourcen "Schrift.&lt;name&gt;" (DynamicResource);
/// ein Wechsel tauscht nur diese Werte aus, die Oberflaeche wird nicht neu aufgebaut.
/// </summary>
public static class Schrift
{
    /// <summary>Grundgroessen bei Stufe Normal (Punkte).</summary>
    public static readonly IReadOnlyDictionary<string, double> Grundgroessen = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["Winzig"] = 11,
        ["Klein"] = 12,
        ["Normal"] = 14,
        ["Mittel"] = 16,
        ["Gross"] = 18,
        ["Titel"] = 22,
        ["Banner"] = 26,
        ["Zahl"] = 30,
    };

    /// <summary>
    /// Ueberschriften wachsen nur bis zu dieser Groesse mit (Sammelliste Nr. 5): sonst passt bei
    /// "Maximal" ein langes Wort wie "Abschlussprojekt" am Handy nicht mehr in eine Zeile.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double> Hoechstens = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["Gross"] = 28,
        ["Titel"] = 30,
        ["Banner"] = 32,
        ["Zahl"] = 36,
    };

    /// <summary>Beschriftung der unteren Leiste: waechst hoechstens bis 12 Punkte (wie NAV_LABEL_MAX am Handy).</summary>
    public const double LeisteHoechstens = 12;
    public const double LeisteGrund = 11;

    public static double Faktor { get; private set; } = 1.0;

    /// <summary>Nach jedem Wechsel der Schriftstufe (z. B. Aufbau breit/schmal neu waehlen).</summary>
    public static event Action? Geaendert;

    public static void Anwenden(double faktor)
    {
        var app = Application.Current ?? throw new InvalidOperationException("Keine Anwendung");
        var res = app.Resources;
        foreach (var (name, groesse) in Grundgroessen)
        {
            res["Schrift." + name] = Hoechstens.TryGetValue(name, out var grenze) ? Math.Max(groesse, Math.Min(grenze, groesse * faktor)) : groesse * faktor;
        }
        res["Schrift.Leiste"] = Math.Min(LeisteHoechstens, LeisteGrund * faktor);
        // Fluent-Steuerelemente (Knoepfe, Eingabefelder, Klappbereiche) nehmen diese Groesse
        res["ControlContentThemeFontSize"] = Grundgroessen["Normal"] * faktor;
        Faktor = faktor;
        Geaendert?.Invoke();
    }

    /// <summary>Bindet die Schriftgroesse an eine Stufe (wie DynamicResource in XAML).</summary>
    public static T Groesse<T>(this T element, string name = "Normal") where T : Control
    {
        element.Bind(TextElement.FontSizeProperty, element.GetResourceObservable("Schrift." + name));
        return element;
    }

    /// <summary>Feste Breite bei Stufe Normal, waechst mit der Schrift mit (Kacheln, Ringfelder).</summary>
    public static T Breite<T>(this T element, double basis) where T : Control
    {
        element.Bind(Layoutable.WidthProperty, element.GetResourceObservable("Schrift.Normal",
            v => v is double d ? basis * d / Grundgroessen["Normal"] : basis));
        return element;
    }

    /// <summary>Feste Hoehe bei Stufe Normal, waechst mit der Schrift mit.</summary>
    public static T HoeheMitSchrift<T>(this T element, double basis) where T : Control
    {
        element.Bind(Layoutable.HeightProperty, element.GetResourceObservable("Schrift.Normal",
            v => v is double d ? basis * d / Grundgroessen["Normal"] : basis));
        return element;
    }

    /// <summary>Text mit Umbruch nur an Wortgrenzen (Woerter werden nie mitten getrennt, solange sie in die Zeile passen).</summary>
    public static TextBlock Text(string text, string groesse = "Normal", FontWeight gewicht = FontWeight.Normal, string farbe = "Pinsel.text") =>
        new TextBlock { Text = text, FontWeight = gewicht, TextWrapping = TextWrapping.Wrap }
            .Groesse(groesse)
            .Farbe(TextBlock.ForegroundProperty, farbe);

    /// <summary>Karte wie in der Python-Linie: Flaeche "card", Rahmen "border", runde Ecken.</summary>
    public static Border Karte(Control inhalt, string flaeche = "Pinsel.card")
    {
        var karte = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Child = inhalt,
        };
        karte.Classes.Add("karte");
        return karte.Farbe(Border.BackgroundProperty, flaeche).Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    /// <summary>
    /// Knopf mit Text, der umbrechen darf (ragt bei grosser Schrift nicht aus der Karte).
    /// A 0.12.2 wie GradientButton am Handy: Pille (Hoehe 46, waechst mit der Schrift), Text 14 fett,
    /// betont mit Verlauf von links nach rechts, sonst dezent (card_alt, Rand border_hi).
    /// </summary>
    public static Button Knopf(string text, bool betont = false)
    {
        var beschriftung = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontWeight = FontWeight.Bold }.Groesse();
        var knopf = new Button
        {
            Content = beschriftung,
            Padding = new Thickness(20, 6),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 8),
            CornerRadius = new CornerRadius(999),
            BorderThickness = new Thickness(betont ? 0 : 1),
        };
        knopf.Bind(Layoutable.MinHeightProperty, knopf.GetResourceObservable("Schrift.Normal", v => v is double d ? KnopfHoehe * d / Grundgroessen["Normal"] : KnopfHoehe));
        knopf.Farbe(Button.BackgroundProperty, betont ? "VerlaufW.primary" : "Pinsel.card_alt")
            .Farbe(Button.ForegroundProperty, betont ? "Pinsel.on_accent" : "Pinsel.text")
            .Farbe(Button.BorderBrushProperty, "Pinsel.border_hi");
        beschriftung.Farbe(TextBlock.ForegroundProperty, betont ? "Pinsel.on_accent" : "Pinsel.text");
        return knopf;
    }

    /// <summary>Hoehe eines Knopfs bei Stufe Normal (GradientButton height=46).</summary>
    public const double KnopfHoehe = 46;

    /// <summary>
    /// A 0.12.2, Karte wie ui.Card am Handy: Strich 4 x 14 in der Akzentfarbe, Titel in Grossbuchstaben
    /// (11 fett, Akzentfarbe), Unterschrift rechts daneben (11 muted, rechtsbuendig, Aufteilung 3:2),
    /// darunter der Inhalt mit 12 Abstand.
    /// </summary>
    public static Border KopfKarte(string titel, string? unter, Control inhalt, string akzent = "Pinsel.accent", Control? aktion = null, bool oben = false)
    {
        var kopf = KartenKopf(titel, unter, akzent, out _, aktion, oben);
        return Karte(new StackPanel { Spacing = 12, Children = { kopf, inhalt } });
    }

    /// <summary>Ab diesem Schriftfaktor steht die Unterschrift unter dem Titel (Card.STACK_FACTOR am Handy).</summary>
    public const double UntereinanderAb = 1.3;

    /// <summary>
    /// Kopfzeile einer <see cref="KopfKarte"/> (auch fuer Karten, die ihren Inhalt selbst bauen). Ab
    /// <see cref="UntereinanderAb"/> steht die Unterschrift linksbuendig unter dem Titel, der Strich auf
    /// Hoehe der ersten Titelzeile; ein Wechsel der Schriftstufe ordnet neu an.
    /// </summary>
    public static Grid KartenKopf(string titel, string? unter, string akzent, out TextBlock unterText, Control? aktion = null, bool oben = false)
    {
        unterText = Text(unter ?? "", "Winzig", FontWeight.Normal, "Pinsel.muted");
        return KartenKopf(titel, unterText, akzent, aktion, oben);
    }

    /// <summary>
    /// Wie oben mit einer eigenen Unterschrift-Zeile, deren Text sich spaeter aendern darf (set_subtitle).
    /// <paramref name="oben"/> (Phase 8 W10): Titel, Strich und Unterschrift oben buendig, auch wenn die Unterschrift
    /// nebeneinander umbricht (Kacheln einer Reihe haben ihre Ueberschrift dann auf einer Hoehe).
    /// </summary>
    public static Grid KartenKopf(string titel, TextBlock unterBlock, string akzent, Control? aktion = null, bool oben = false)
    {
        var strich = new Border { Width = 4, Height = 14, CornerRadius = new CornerRadius(2) }
            .Farbe(Border.BackgroundProperty, akzent);
        var titelText = Text(Lernplattform.Core.Texte.Beschriftungen.Gross(titel), "Winzig", FontWeight.Bold, akzent);
        titelText.VerticalAlignment = VerticalAlignment.Center;
        unterBlock.VerticalAlignment = VerticalAlignment.Center;
        // Abstand 8 (spacing=8) als Rand vor jedem Teil statt ColumnSpacing: das zaehlte auch leere Spalten
        // mit und nahm dem Titel 16 Punkte
        titelText.Margin = unterBlock.Margin = new Thickness(KopfAbstand, 0, 0, 0);
        var kopf = new Grid { RowSpacing = 2 };
        kopf.Children.Add(strich);
        kopf.Children.Add(titelText);
        kopf.Children.Add(unterBlock);
        if (aktion is not null)
        {
            aktion.VerticalAlignment = VerticalAlignment.Center;
            aktion.Margin = new Thickness(KopfAbstand, 0, 0, 0);
            Grid.SetColumn(aktion, 3);
            Grid.SetRowSpan(aktion, 2);
            kopf.Children.Add(aktion);
        }
        // Nebeneinander passt ein Wort nicht in seine Spalte (z. B. "Grundlagenaufgaben" in 2/5 der Breite):
        // dann wie ab "Sehr gross" untereinander, damit kein Wort mitten getrennt wird
        string? spaltenJetzt = null;
        bool WortZuLang()
        {
            var breite = kopf.Bounds.Width - strich.Width - (aktion?.DesiredSize.Width ?? 0);
            return breite > 0
                && (LaengstesWort(titelText) > breite * 3 / 5 - KopfAbstand + 0.5
                    || LaengstesWort(unterBlock) > breite * 2 / 5 - KopfAbstand + 0.5);
        }
        void Anordnen()
        {
            var mitUnter = !string.IsNullOrEmpty(unterBlock.Text);
            var untereinander = Faktor >= UntereinanderAb || (mitUnter && WortZuLang());
            unterBlock.IsVisible = mitUnter;
            var spalten = untereinander || !mitUnter ? "Auto,*,0,Auto" : "Auto,3*,2*,Auto";
            if (spaltenJetzt != spalten)
            {
                spaltenJetzt = spalten;
                kopf.ColumnDefinitions = new ColumnDefinitions(spalten);
            }
            if (kopf.RowDefinitions.Count != (untereinander ? 2 : 1))
            {
                kopf.RowDefinitions = new RowDefinitions(untereinander ? "Auto,Auto" : "Auto");
            }
            Grid.SetColumn(titelText, 1);
            Grid.SetColumn(unterBlock, untereinander ? 1 : 2);
            Grid.SetRow(unterBlock, untereinander ? 1 : 0);
            unterBlock.TextAlignment = untereinander ? TextAlignment.Left : TextAlignment.Right;
            // Strich mittig zur ersten Titelzeile (tick_top), sonst mittig zur Zeile
            var obenBuendig = untereinander || oben;
            strich.VerticalAlignment = obenBuendig ? VerticalAlignment.Top : VerticalAlignment.Center;
            strich.Margin = new Thickness(0, obenBuendig ? Math.Max(0, Math.Round((Grundgroessen["Winzig"] * Faktor * 1.17 - 14) / 2, 1)) : 0, 0, 0);
            if (oben)
            {
                titelText.VerticalAlignment = unterBlock.VerticalAlignment = VerticalAlignment.Top;
            }
        }
        unterBlock.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
            {
                Anordnen();
            }
        };
        kopf.SizeChanged += (_, e) =>
        {
            if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5)
            {
                Anordnen();
            }
        };
        BeiStufe(kopf, Anordnen);
        return kopf;
    }

    /// <summary>Abstand zwischen Strich, Titel, Unterschrift und Aktion einer Kartenkopfzeile (Card spacing=8).</summary>
    public const double KopfAbstand = 8;

    /// <summary>Ab diesem Schriftfaktor stehen Bausteine untereinander (ui.LARGE_FACTOR = 1,6 am Handy).</summary>
    public const double GrossAb = 1.6;

    /// <summary>Ist eine der Stufen ab 160 % gewaehlt (ui.large())?</summary>
    public static bool Gross => Faktor >= GrossAb;

    /// <summary>
    /// Fuehrt <paramref name="aktion"/> sofort, beim Einhaengen und nach jedem Wechsel der Schriftstufe aus,
    /// solange das Element angezeigt wird (fuer Bausteine, die sich je Stufe anders anordnen).
    /// </summary>
    public static void BeiStufe(Control element, Action aktion)
    {
        element.AttachedToVisualTree += (_, _) => { Geaendert -= aktion; Geaendert += aktion; aktion(); };
        element.DetachedFromVisualTree += (_, _) => Geaendert -= aktion;
        aktion();
    }

    /// <summary>Breite des laengsten Worts eines Texts in seiner Schrift (Trennstellen Leerzeichen, "-", "/", U+200B; das geschuetzte Leerzeichen trennt nicht).</summary>
    public static double LaengstesWort(TextBlock text)
    {
        if (string.IsNullOrEmpty(text.Text) || text.FontSize <= 0)
        {
            return 0;
        }
        var schrift = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight);
        var breite = 0.0;
        foreach (var wort in text.Text.Split([' ', '-', '/', '\u200B'], StringSplitOptions.RemoveEmptyEntries))
        {
            using var layout = new Avalonia.Media.TextFormatting.TextLayout(wort, schrift, text.FontSize, null);
            breite = Math.Max(breite, layout.Width);
        }
        return breite;
    }

    /// <summary>Text in einer festen Groesse (Python-Wert bei Normal), waechst mit der Schrift wie fs().</summary>
    public static TextBlock Pt(string text, double groesse, FontWeight gewicht = FontWeight.Normal, string farbe = "Pinsel.text")
    {
        var block = new TextBlock { Text = text, FontWeight = gewicht, TextWrapping = TextWrapping.Wrap }.Farbe(TextBlock.ForegroundProperty, farbe);
        block.Bind(TextElement.FontSizeProperty, block.GetResourceObservable("Schrift.Normal", v => v is double d ? groesse * d / Grundgroessen["Normal"] : groesse));
        return block;
    }

    /// <summary>Phase 5 BE4: Knopf fuer unwiderrufliche Schritte (NeoButton kind="danger"): Verlauf "danger", helle Schrift.</summary>
    public static Button Gefahrknopf(string text)
    {
        var knopf = Knopf(text);
        knopf.BorderThickness = new Thickness(0);
        knopf.Farbe(Button.BackgroundProperty, "VerlaufW.danger").Farbe(Button.ForegroundProperty, "Pinsel.on_accent");
        ((TextBlock)knopf.Content!).Farbe(TextBlock.ForegroundProperty, "Pinsel.on_accent");
        return knopf;
    }
}
