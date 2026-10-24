using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Symbole der Oberflaeche. A 0.12.2 (Weisung P6-16, Frage 1 A): Material-Symbole aus der
/// Teilschrift Assets/Fonts/fisi_symbole.otf (Material Icons, Apache-2.0, erzeugt von
/// werkzeuge/symbole/erzeuge_symbole.py, gleiche Liste wie dort), wie die Handy-App der
/// Python-Linie (ft.Icons) und die PC-Seitenleiste (fisi_symbole.otf). Dazu die eigenen
/// Umrisse Lupe und Fragezeichen von frueher.
/// </summary>
public static class Symbole
{
    public static readonly FontFamily Familie = new("avares://Lernplattform.App/Assets/Fonts#Material Icons");

    /// <summary>Name (Flutter/Flet, klein) -> Zeichencode; gleiche Liste wie erzeuge_symbole.py.</summary>
    public static readonly IReadOnlyDictionary<string, int> Zeichen = new Dictionary<string, int>
    {
        ["business_rounded"] = 0xF5F8, ["calculate_rounded"] = 0xF5FD, ["dashboard_rounded"] = 0xF68E,
        ["dns_rounded"] = 0xF6C1, ["euro_rounded"] = 0xF716, ["flag_rounded"] = 0xF768,
        ["help_outline_rounded"] = 0xF7E4, ["home_rounded"] = 0xF7F5, ["insights_rounded"] = 0xF820,
        ["layers_rounded"] = 0xF847, ["security_rounded"] = 0xF013E, ["settings_rounded"] = 0xF0164,
        ["sports_esports_rounded"] = 0xF01BC, ["storage_rounded"] = 0xF01DE, ["storefront_rounded"] = 0xF01E1,
        ["style_rounded"] = 0xF01E8, ["track_changes_rounded"] = 0xF0248, ["work_rounded"] = 0xF02C7,
        ["diamond_rounded"] = 0xF0306, ["edit_note_rounded"] = 0xF030F, ["lan_rounded"] = 0xF034B,
        ["route_rounded"] = 0xF0377,
        ["workspace_premium_rounded"] = 0xF03C0, ["dashboard_outlined"] = 0xEFA1, ["school_outlined"] = 0xF33C,
        ["school_rounded"] = 0xF012E, ["calculate_outlined"] = 0xEF10, ["sports_esports_outlined"] = 0xF3CA,
        ["insights_outlined"] = 0xF132, ["settings_outlined"] = 0xF36E, ["search_rounded"] = 0xF013D,
        ["chevron_right"] = 0xE15F, ["chevron_left"] = 0xE15E, ["arrow_right"] = 0xE09E, ["arrow_drop_down"] = 0xE098,
    };

    /// <summary>
    /// Zeichen zu einem Symbolnamen der Navigation: Handy-Namen wie "DASHBOARD_OUTLINED" direkt,
    /// PC-Namen wie "style" als gerundete Variante ("style_rounded", wie fisi_symbole.otf).
    /// </summary>
    public static string Text(string name)
    {
        var klein = Lernplattform.Core.Suche.PythonKlein.Klein(name);
        if (!Zeichen.TryGetValue(klein, out var code) && !Zeichen.TryGetValue(klein + "_rounded", out code))
        {
            throw new ArgumentException("Symbol fehlt in fisi_symbole.otf: " + name, nameof(name));
        }
        return char.ConvertFromUtf32(code);
    }

    /// <summary>Symbol als Text in der Symbolschrift in fester Groesse (wie ft.Icon, waechst nicht mit der Schriftstufe).</summary>
    public static TextBlock Block(string name, double groesse, string farbe)
    {
        var block = new TextBlock
        {
            Text = Text(name),
            FontFamily = Familie,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            LineHeight = double.NaN,
            // Feste Groesse wie ft.Icon am Handy: Symbole wachsen nicht mit der Schrift (nur ft.Text)
            FontSize = groesse,
        };
        block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable(farbe));
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(block, Avalonia.Automation.AccessibilityView.Raw);
        return block;
    }

    public static readonly Geometry Suche = Geometry.Parse(
        "M10,2 A8,8 0 1 0 15.2,16.6 L21,22.4 L22.4,21 L16.6,15.2 A8,8 0 0 0 10,2 Z M10,4 A6,6 0 1 1 10,16 A6,6 0 1 1 10,4 Z");

    public static readonly Geometry Hilfe = Geometry.Parse(
        "M12,1 A11,11 0 1 0 12,23 A11,11 0 1 0 12,1 Z M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 Z "
        + "M12,5.5 C9.8,5.5 8.2,7 8.2,9.2 L10.2,9.2 C10.2,8.1 11,7.4 12,7.4 C13,7.4 13.8,8.1 13.8,9 C13.8,10.9 11,10.7 11,13.8 L13,13.8 "
        + "C13,11.7 15.8,11.4 15.8,9 C15.8,7 14.2,5.5 12,5.5 Z M11,15.5 L13,15.5 L13,17.5 L11,17.5 Z");
}
