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
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            Child = inhalt,
        };
        karte.Classes.Add("karte");
        return karte.Farbe(Border.BackgroundProperty, flaeche).Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    /// <summary>Knopf mit Text, der umbrechen darf (ragt bei grosser Schrift nicht aus der Karte).</summary>
    public static Button Knopf(string text, bool betont = false)
    {
        var beschriftung = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }.Groesse();
        var knopf = new Button
        {
            Content = beschriftung,
            Padding = new Thickness(14, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 8),
        };
        knopf.Farbe(Button.BackgroundProperty, betont ? "Verlauf.primary" : "Pinsel.card_alt")
            .Farbe(Button.ForegroundProperty, betont ? "Pinsel.on_accent" : "Pinsel.text")
            .Farbe(Button.BorderBrushProperty, "Pinsel.border");
        beschriftung.Farbe(TextBlock.ForegroundProperty, betont ? "Pinsel.on_accent" : "Pinsel.text");
        return knopf;
    }

    /// <summary>Phase 5 BE4: Knopf fuer unwiderrufliche Schritte (NeoButton kind="danger"): Verlauf "danger", helle Schrift.</summary>
    public static Button Gefahrknopf(string text)
    {
        var knopf = Knopf(text);
        knopf.Farbe(Button.BackgroundProperty, "Verlauf.danger").Farbe(Button.ForegroundProperty, "Pinsel.on_accent");
        ((TextBlock)knopf.Content!).Farbe(TextBlock.ForegroundProperty, "Pinsel.on_accent");
        return knopf;
    }
}
