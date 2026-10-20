using System.Globalization;

namespace Lernplattform.Core.Design;

/// <summary>Eine fertig berechnete Farbwelt: alle Farben (Hex "#RRGGBB") und Verlaeufe.</summary>
public sealed record Palette(
    bool Hell,
    IReadOnlyDictionary<string, string> Farben,
    IReadOnlyDictionary<string, (string Von, string Bis)> Verlaeufe);

/// <summary>
/// Die 72 Farbwelten (6 Grundfarben x 6 Hintergruende x Dunkel/Hell), berechnet
/// wie in der Python-Linie (fisi_theme.py, farbwelt_palette ohne eigene Farben).
/// Im Hellen werden Schriftfarben so weit abgedunkelt, dass sie auf allen
/// Flaechen 4,6:1 erreichen; Rahmen und Fokus 3:1. Ergebnis muss exakt den
/// Python-Hexwerten entsprechen (Vergleichswerte G6, Auflage A2).
/// </summary>
public static class Farbwelt
{
    public const string Dunkel = "dunkel";
    public const string Hell = "hell";
    public const string StandardGrundfarbe = "cyan_pink";
    public const string StandardHintergrund = "violett";

    public const double MindestKontrast = 4.6;
    public const double BedienKontrast = 3.0;

    public sealed record Grundfarbe(string Id, string Name, string Akzent, string Akzent2,
        (string, string) Primaer, (string, string) Verlauf, (string, string) Banner);

    public static readonly IReadOnlyList<Grundfarbe> Grundfarben =
    [
        new("cyan_pink", "Cyan/Pink", "#22D3EE", "#F472B6", ("#7C3AED", "#DB2777"), ("#0E7490", "#4F46E5"), ("#4C1D95", "#9D174D")),
        new("lila_magenta", "Lila/Magenta", "#C084FC", "#F0ABFC", ("#7C3AED", "#C026D3"), ("#9333EA", "#DB2777"), ("#4C1D95", "#86198F")),
        new("blau_tuerkis", "Blau/Türkis", "#60A5FA", "#2DD4BF", ("#2563EB", "#0F766E"), ("#1D4ED8", "#0E7490"), ("#1E3A8A", "#115E59")),
        new("gruen_lime", "Grün/Lime", "#34D399", "#A3E635", ("#047857", "#4D7C0F"), ("#065F46", "#3F6212"), ("#064E3B", "#3F6212")),
        new("orange_gelb", "Orange/Gelb", "#FDBA74", "#FDE047", ("#C2410C", "#A16207"), ("#9A3412", "#854D0E"), ("#7C2D12", "#713F12")),
        new("rot_pink", "Rot/Pink", "#FB7185", "#F9A8D4", ("#E11D48", "#DB2777"), ("#BE123C", "#C026D3"), ("#7F1D1D", "#831843")),
    ];

    // Reihenfolge der Werte: bg, sidebar, card, card_alt, card_hi, border, border_hi, ring_bg, text_dim, muted
    private static readonly string[] HintergrundFelder =
        ["bg", "sidebar", "card", "card_alt", "card_hi", "border", "border_hi", "ring_bg", "text_dim", "muted"];

    public sealed record Hintergrund(string Id, string NameDunkel, string NameHell, string[] Dunkel, string[] Hell);

    public static readonly IReadOnlyList<Hintergrund> Hintergruende =
    [
        new("violett", "Violett", "Flieder",
            ["#120A24", "#0C0619", "#1B1031", "#241541", "#301C57", "#2F1D52", "#553289", "#2C1A4D", "#A794C6", "#9986B8"],
            ["#F4F0FB", "#EAE3F7", "#FFFFFF", "#F3EEFB", "#E8DFF7", "#DCD1EF", "#B7A3DD", "#E3D9F3", "#5A4979", "#685889"]),
        new("nachtblau", "Nachtblau", "Hellblau",
            ["#0B1224", "#070C19", "#111B33", "#172441", "#1E2F57", "#1F2E52", "#33518A", "#1A2848", "#94A3C6", "#8998BA"],
            ["#EFF3FA", "#E2E9F5", "#FFFFFF", "#EEF2FA", "#DFE7F5", "#D0DAEC", "#9FB2D6", "#DAE3F2", "#45557A", "#56658A"]),
        new("tannengruen", "Tannengrün", "Mintgrün",
            ["#0A1814", "#06100D", "#10231D", "#152D26", "#1C3931", "#1D3A31", "#2F5F50", "#183229", "#93BBAE", "#7EA498"],
            ["#EFF6F3", "#E1EEE8", "#FFFFFF", "#EDF5F1", "#DCEBE4", "#CCE0D7", "#93BBAA", "#D6E8DF", "#3E5E53", "#4D6C61"]),
        new("aubergine", "Aubergine", "Rosé",
            ["#1A0A14", "#11060D", "#26101D", "#311527", "#401C33", "#3D1D31", "#6A3255", "#3A1A2E", "#C294B0", "#AE829D"],
            ["#FAF0F5", "#F3E2EC", "#FFFFFF", "#F9EEF4", "#F0DCE7", "#E6CCDA", "#CE9BB7", "#EDD6E3", "#6E4560", "#7B536D"]),
        new("anthrazit", "Anthrazit", "Hellgrau",
            ["#141416", "#0D0D0F", "#1C1C20", "#242429", "#2E2E35", "#2C2C33", "#4A4A55", "#2A2A31", "#A3A3B0", "#9595A2"],
            ["#F3F3F5", "#E7E7EB", "#FFFFFF", "#F1F1F4", "#E4E4E9", "#D6D6DD", "#ABABB7", "#DFDFE5", "#4E4E5B", "#5C5C69"]),
        new("schwarz", "Schwarz", "Weiß",
            ["#050507", "#000000", "#111114", "#18181C", "#222228", "#25252B", "#3E3E48", "#1F1F25", "#A3A3B0", "#888895"],
            ["#FFFFFF", "#F2F2F4", "#FFFFFF", "#F5F5F7", "#EBEBEF", "#DDDDE3", "#B0B0BC", "#E6E6EB", "#4A4A55", "#585864"]),
    ];

    // Feste dunkle Grundwerte (Python: _DARK_FIXED), danach on_accent
    private static readonly (string, string)[] DunkelFest =
    [
        ("text", "#ECE6F8"), ("text_soft", "#CFC6E0"), ("knob", "#ECE6F8"), ("scrollbar", "#2A2A2F"),
        ("scrollbar_hi", "#40404A"), ("cyan", "#22D3EE"), ("pink", "#F472B6"), ("purple", "#A78BFA"),
        ("violet", "#7C3AED"), ("green", "#34D399"), ("yellow", "#FBBF24"), ("orange", "#FB923C"),
        ("blue", "#60A5FA"), ("red", "#F87171"),
    ];

    private static readonly (string, string)[] HellText =
    [
        ("text", "#1B1628"), ("text_soft", "#2F2940"), ("knob", "#FFFFFF"),
        ("scrollbar", "#C9C9D1"), ("scrollbar_hi", "#A9A9B5"),
    ];

    private static readonly string[] HellAbgedunkelt =
        ["cyan", "pink", "purple", "violet", "green", "yellow", "orange", "blue", "red", "accent", "accent2"];

    private static readonly string[] Flaechen = ["bg", "sidebar", "card", "card_alt", "card_hi"];

    /// <summary>Berechnet eine Farbwelt. Unbekannte Kennungen fallen wie in Python auf den Standard.</summary>
    public static Palette Berechne(string darstellung, string grundfarbe, string hintergrund)
    {
        bool hell = darstellung == Hell;
        var g = Grundfarben.FirstOrDefault(x => x.Id == grundfarbe) ?? Grundfarben[0];
        var h = Hintergruende.FirstOrDefault(x => x.Id == hintergrund) ?? Hintergruende[0];
        var c = new Dictionary<string, string>(StringComparer.Ordinal);
        var werte = hell ? h.Hell : h.Dunkel;
        for (int i = 0; i < HintergrundFelder.Length; i++)
        {
            c[HintergrundFelder[i]] = werte[i];
        }
        foreach (var (k, v) in DunkelFest)
        {
            c[k] = v;
        }
        c["on_accent"] = "#FFFFFF";
        c["accent"] = g.Akzent;
        c["accent2"] = g.Akzent2;
        if (hell)
        {
            foreach (var (k, v) in HellText)
            {
                c[k] = v;
            }
            var flaechen = Flaechen.Select(k => c[k]).ToArray();
            foreach (var k in HellAbgedunkelt)
            {
                c[k] = LesbarAuf(c[k], flaechen, MindestKontrast);
            }
            foreach (var k in (string[])["text_dim", "muted"])
            {
                c[k] = LesbarAuf(c[k], flaechen, MindestKontrast);
            }
        }
        var bedien = Flaechen.Select(k => c[k]).ToArray();
        var ziel = hell ? "#000000" : "#FFFFFF";
        c["field_border"] = AbtoenenBis(c["border_hi"], ziel, bedien, BedienKontrast);
        c["focus"] = AbtoenenBis(c["accent"], ziel, bedien, BedienKontrast);

        var verlaeufe = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["primary"] = g.Primaer,
            ["accent"] = g.Verlauf,
            ["hero"] = g.Banner,
            ["danger"] = ("#DC2626", "#BF5811"),
            ["success"] = ("#05875F", "#158293"),
        };
        return new Palette(hell, c, verlaeufe);
    }

    // ---------------------------------------------------------------- Farbhilfen

    public static (int R, int G, int B) HexZuRgb(string hex)
    {
        var h = hex.TrimStart('#');
        return (int.Parse(h.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(h.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(h.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    // Python: max(0, min(255, int(round(v)))); round = auf gerade Zahl runden wie Math.Round
    private static int Kanal(double v) => Math.Max(0, Math.Min(255, (int)Math.Round(v, MidpointRounding.ToEven)));

    public static string RgbZuHex(double r, double g, double b) =>
        string.Create(CultureInfo.InvariantCulture, $"#{Kanal(r):X2}{Kanal(g):X2}{Kanal(b):X2}");

    /// <summary>Mischt zwei Farben; t=0 liefert a, t=1 liefert b.</summary>
    public static string Mischen(string a, string b, double t)
    {
        t = Math.Max(0.0, Math.Min(1.0, t));
        var (ra, ga, ba) = HexZuRgb(a);
        var (rb, gb, bb) = HexZuRgb(b);
        return RgbZuHex(ra + (rb - ra) * t, ga + (gb - ga) * t, ba + (bb - ba) * t);
    }

    public static double Leuchtdichte(string farbe)
    {
        static double Teil(double v)
        {
            v /= 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        var (r, g, b) = HexZuRgb(farbe);
        return 0.2126 * Teil(r) + 0.7152 * Teil(g) + 0.0722 * Teil(b);
    }

    public static double Kontrast(string a, string b)
    {
        double la = Leuchtdichte(a), lb = Leuchtdichte(b);
        double hoch = Math.Max(la, lb), tief = Math.Min(la, lb);
        return (hoch + 0.05) / (tief + 0.05);
    }

    /// <summary>Python: readable_on - dunkelt in 2,5-%-Schritten ab, bis die Farbe auf allen Flaechen lesbar ist.</summary>
    public static string LesbarAuf(string farbe, string[] flaechen, double minimum)
    {
        var ton = farbe;
        for (int schritt = 1; schritt < 41; schritt++)
        {
            if (flaechen.All(f => Kontrast(ton, f) >= minimum))
            {
                return ton;
            }
            ton = Mischen(farbe, "#000000", schritt * 0.025);
        }
        return ton;
    }

    /// <summary>Python: shade_until - mischt in 0,25-%-Schritten zu ziel, bis der Mindestkontrast erreicht ist.</summary>
    public static string AbtoenenBis(string farbe, string ziel, string[] flaechen, double minimum)
    {
        var ton = farbe;
        for (int schritt = 1; schritt < 401; schritt++)
        {
            if (flaechen.All(f => Kontrast(ton, f) >= minimum))
            {
                return ton;
            }
            ton = Mischen(farbe, ziel, schritt * 0.0025);
        }
        return ton;
    }
}
