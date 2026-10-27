using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 1, K5 (Sammelliste Nr. 5 und 6): prueft eine gezeichnete Seite auf
/// (1) Woerter, die mitten im Wort umbrechen, (2) Knoepfe, die aus ihrer Karte
/// ragen, (3) abgeschnittene einzeilige Beschriftungen und (4) Inhalte, die
/// ueber den rechten Fensterrand hinausgehen. Liefert je Fund eine Zeile.
/// A 0.15.0 (Weisung P8-10): (5) jede sichtbare Scrollleiste traegt den schmalen Stil aus App.axaml
/// (hoechstens 6 px dick, keine Pfeilknoepfe).
/// </summary>
public static class Layoutpruefung
{
    private const double Toleranz = 0.5;

    public static List<string> Pruefe(Control wurzel)
    {
        var funde = new List<string>();
        var breite = wurzel.Bounds.Width;
        foreach (var element in wurzel.GetVisualDescendants().OfType<Control>())
        {
            if (!element.IsEffectivelyVisible || element.Bounds.Width <= 0)
            {
                continue;
            }
            switch (element)
            {
                case TextBlock text:
                    PruefeText(text, funde);
                    break;
                case Button knopf:
                    PruefeKnopf(knopf, funde);
                    break;
                case ScrollBar leiste:
                    PruefeLeiste(leiste, funde);
                    break;
            }
            if (element is TextBlock or Button && element.TranslatePoint(new Point(element.Bounds.Width, 0), wurzel) is { } rechts
                && rechts.X > breite + Toleranz && !InWaagrechtVerschiebbar(element, wurzel))
            {
                funde.Add($"ragt ueber den Rand: {Name(element)} ({rechts.X:F0} > {breite:F0})");
            }
        }
        return funde;
    }

    private static void PruefeText(TextBlock text, List<string> funde)
    {
        var inhalt = text.Text ?? "";
        if (inhalt.Length == 0)
        {
            return;
        }
        if (text.TextWrapping == TextWrapping.NoWrap)
        {
            if (text.DesiredSize.Width > text.Bounds.Width + Toleranz && text.TextTrimming == TextTrimming.None)
            {
                funde.Add($"abgeschnitten: \"{Kurz(inhalt)}\" ({text.DesiredSize.Width:F0} > {text.Bounds.Width:F0})");
            }
            return;
        }
        var zeilen = text.TextLayout.TextLines;
        for (var i = 0; i < zeilen.Count - 1; i++)
        {
            var ende = zeilen[i].FirstTextSourceIndex + zeilen[i].Length;
            if (ende <= 0 || ende >= inhalt.Length)
            {
                continue;
            }
            if (char.IsLetterOrDigit(inhalt[ende - 1]) && char.IsLetterOrDigit(inhalt[ende]))
            {
                funde.Add($"Wort getrennt: \"{Wort(inhalt, ende)}\" in \"{Kurz(inhalt)}\"");
            }
        }
    }

    /// <summary>Weisung P8-10: Scrollleiste schmal und ohne Pfeilknoepfe (Stil in App.axaml).</summary>
    private static void PruefeLeiste(ScrollBar leiste, List<string> funde)
    {
        var dicke = leiste.Orientation == Avalonia.Layout.Orientation.Vertical ? leiste.Bounds.Width : leiste.Bounds.Height;
        if (dicke > LeisteDicke + Toleranz)
        {
            funde.Add($"Scrollleiste nicht schmal: {dicke:F0} > {LeisteDicke:F0}");
        }
        if (leiste.GetVisualDescendants().OfType<Control>().Any(c => c.Name is "PART_LineUpButton" or "PART_LineDownButton"))
        {
            funde.Add("Scrollleiste mit Pfeilknoepfen");
        }
    }

    /// <summary>Dicke der Scrollleisten in App.axaml (Weisung P8-10).</summary>
    public const double LeisteDicke = 6;

    private static void PruefeKnopf(Button knopf, List<string> funde)
    {
        var karte = knopf.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("karte"));
        if (karte is null || knopf.TranslatePoint(default, karte) is not { } links)
        {
            return;
        }
        var rechts = links.X + knopf.Bounds.Width;
        if (links.X < -Toleranz || rechts > karte.Bounds.Width + Toleranz)
        {
            funde.Add($"Knopf ragt aus der Karte: {Name(knopf)} ({links.X:F0}..{rechts:F0} von 0..{karte.Bounds.Width:F0})");
        }
    }

    private static bool InWaagrechtVerschiebbar(Control element, Control wurzel) =>
        element.GetVisualAncestors().TakeWhile(a => a != wurzel).OfType<ScrollViewer>()
            .Any(s => s.HorizontalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);

    private static string Name(Control element) => element switch
    {
        TextBlock t => "\"" + Kurz(t.Text ?? "") + "\"",
        Button { Content: TextBlock t } => "\"" + Kurz(t.Text ?? "") + "\"",
        Button { Content: string s } => "\"" + Kurz(s) + "\"",
        _ => element.GetType().Name,
    };

    private static string Wort(string text, int stelle)
    {
        var von = stelle;
        while (von > 0 && char.IsLetterOrDigit(text[von - 1]))
        {
            von--;
        }
        var bis = stelle;
        while (bis < text.Length && char.IsLetterOrDigit(text[bis]))
        {
            bis++;
        }
        return text[von..stelle] + "|" + text[stelle..bis];
    }

    private static string Kurz(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
