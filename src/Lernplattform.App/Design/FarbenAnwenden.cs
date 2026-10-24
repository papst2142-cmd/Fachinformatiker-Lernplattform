using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Lernplattform.Core.Design;

namespace Lernplattform.Design;

/// <summary>
/// Setzt eine berechnete Farbwelt als Ressourcen der Anwendung. Alle Flaechen
/// und Schriften holen ihre Farbe ueber "Pinsel.&lt;name&gt;" bzw. "Verlauf.&lt;name&gt;"
/// (DynamicResource); ein Wechsel tauscht nur diese Werte aus, die Oberflaeche
/// wird nicht neu aufgebaut.
/// </summary>
public static class FarbenAnwenden
{
    public static Palette Aktuell { get; private set; } =
        Farbwelt.Berechne(Farbwelt.Dunkel, Farbwelt.StandardGrundfarbe, Farbwelt.StandardHintergrund);

    public static void Anwenden(Palette palette)
    {
        var app = Application.Current ?? throw new InvalidOperationException("Keine Anwendung");
        var res = app.Resources;
        foreach (var (name, hex) in palette.Farben)
        {
            res["Pinsel." + name] = new SolidColorBrush(Color.Parse(hex));
        }
        foreach (var (name, (von, bis)) in palette.Verlaeufe)
        {
            res["Verlauf." + name] = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse(von), 0), new GradientStop(Color.Parse(bis), 1) },
            };
        }
        // A 0.12.2: Verlaeufe von links nach rechts wie gradient() am Handy (Knoepfe), ausser "hero" (Banner schraeg)
        foreach (var (name, (von, bis)) in palette.Verlaeufe)
        {
            res["VerlaufW." + name] = Waagerecht(Color.Parse(von), Color.Parse(bis));
        }
        Aktuell = palette;
        foreach (var (a, b) in Paare)
        {
            res[PaarSchluessel(a, b)] = Waagerecht(Color.Parse(palette.Farben[a]), Color.Parse(palette.Farben[b]));
        }
        // A 0.12.2: Schalter wie _switch am Handy (an: Spur violet, Knopf knob; aus: Spur card_alt, Knopf muted)
        foreach (var (schluessel, farbe) in (ReadOnlySpan<(string, string)>)[("ToggleSwitchFillOn", "violet"), ("ToggleSwitchFillOnPointerOver", "violet"),
            ("ToggleSwitchFillOnPressed", "violet"), ("ToggleSwitchKnobFillOn", "knob"), ("ToggleSwitchKnobFillOnPointerOver", "knob"),
            ("ToggleSwitchKnobFillOnPressed", "knob"), ("ToggleSwitchFillOff", "card_alt"), ("ToggleSwitchFillOffPointerOver", "card_alt"),
            ("ToggleSwitchFillOffPressed", "card_alt"), ("ToggleSwitchKnobFillOff", "muted"), ("ToggleSwitchKnobFillOffPointerOver", "muted"),
            ("ToggleSwitchKnobFillOffPressed", "muted"), ("ToggleSwitchStrokeOff", "muted"), ("ToggleSwitchStrokeOffPointerOver", "muted"),
            ("ToggleSwitchStrokeOffPressed", "muted")])
        {
            res[schluessel] = new SolidColorBrush(Color.Parse(palette.Farben[farbe]));
        }
        foreach (var (a, b, t) in Mischungen)
        {
            res[MischSchluessel(a, b, t)] = new SolidColorBrush(Color.Parse(Farbwelt.Mischen(palette.Farben[a], palette.Farben[b], t)));
        }
        app.RequestedThemeVariant = palette.Hell ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private static readonly HashSet<(string, string)> Paare = [];

    /// <summary>
    /// A 0.12.2: Verlauf von links nach rechts zwischen zwei Farben der Farbwelt (z. B. accent, purple wie
    /// GradientBar am Handy). Liefert den Ressourcen-Schluessel; jeder Farbwechsel rechnet ihn neu.
    /// </summary>
    public static string Paar(string a, string b)
    {
        if (Paare.Add((a, b)) && Application.Current is { } app)
        {
            app.Resources[PaarSchluessel(a, b)] = Waagerecht(Color.Parse(Aktuell.Farben[a]), Color.Parse(Aktuell.Farben[b]));
        }
        return PaarSchluessel(a, b);
    }

    private static readonly HashSet<(string, string, double)> Mischungen = [];

    /// <summary>A 0.12.2: Mischfarbe mix(a, b, t) wie am Handy (z. B. Unterzeile im Banner); liefert den Ressourcen-Schluessel.</summary>
    public static string Mischung(string a, string b, double t)
    {
        if (Mischungen.Add((a, b, t)) && Application.Current is { } app)
        {
            app.Resources[MischSchluessel(a, b, t)] = new SolidColorBrush(Color.Parse(Farbwelt.Mischen(Aktuell.Farben[a], Aktuell.Farben[b], t)));
        }
        return MischSchluessel(a, b, t);
    }

    /// <summary>Name einer Mischfarbe fuer Zeichnungen, die Farben selbst aufloesen (<see cref="Lernplattform.Views.VerlaufRing"/>).</summary>
    public static string MischName(string a, string b, double t) => a + "|" + b + "|" + t.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Farbe der aktuellen Farbwelt zu einem Namen ("accent") oder einer Mischfarbe aus <see cref="MischName"/>.</summary>
    public static string Hex(string name)
    {
        var teile = name.Split('|');
        if (teile.Length == 3)
        {
            return Farbwelt.Mischen(Hex(teile[0]), Hex(teile[1]), double.Parse(teile[2], System.Globalization.CultureInfo.InvariantCulture));
        }
        return Aktuell.Farben.TryGetValue(name, out var hex) ? hex : "#808080";
    }

    private static string MischSchluessel(string a, string b, double t) =>
        "Mischung." + a + "." + b + "." + t.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string PaarSchluessel(string a, string b) => "Paar." + a + "." + b;

    private static LinearGradientBrush Waagerecht(Color von, Color bis) => new()
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops = { new GradientStop(von, 0), new GradientStop(bis, 1) },
    };

    /// <summary>Bindet eine Eigenschaft an eine Farb-Ressource (wie DynamicResource in XAML).</summary>
    public static T Farbe<T>(this T element, AvaloniaProperty eigenschaft, string schluessel) where T : Control
    {
        element.Bind(eigenschaft, element.GetResourceObservable(schluessel));
        return element;
    }
}
