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
/// FW6 (Weisung P11-12): (6) nichts Sichtbares liegt unter der senkrechten Scrollleiste eines Fensters (Sammelliste 61),
/// (7) ein Fenster (Dialog) beginnt oben beim Titel (Sammelliste 62), (8) Tabellen brechen nie um und zeigen
/// "→ wischen" genau dann, wenn sie breiter als ihr Bereich sind (Sammelliste 177 Teil 2).
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
                    PruefeUnterLeiste(leiste, wurzel, funde);
                    break;
                case Views.Tabellenkasten tabelle:
                    PruefeTabelle(tabelle, funde);
                    break;
            }
            if (element is TextBlock or Button && element.TranslatePoint(new Point(element.Bounds.Width, 0), wurzel) is { } rechts
                && rechts.X > breite + Toleranz && !InWaagrechtVerschiebbar(element, wurzel))
            {
                funde.Add($"ragt ueber den Rand: {Name(element)} ({rechts.X:F0} > {breite:F0})");
            }
        }
        if (Views.Dialog.Offen is { IsEffectivelyVisible: true } fenster && fenster.GetVisualAncestors().Contains(wurzel)
            && fenster.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } rollen && rollen.Offset.Y > Toleranz)
        {
            funde.Add($"Fenster beginnt nicht oben: {Name(fenster)} (gerollt um {rollen.Offset.Y:F0})");
        }
        return funde;
    }

    /// <summary>FW6 (Sammelliste 61): Text, Knopf, Feld oder Form unter einer sichtbaren senkrechten Scrollleiste.</summary>
    private static void PruefeUnterLeiste(ScrollBar leiste, Control wurzel, List<string> funde)
    {
        // nur in Fenstern ueber der Seite (Rundgang, Dialog: Karte mit Bildlauf direkt im Fenster); auf den Seiten
        // selbst reicht der Inhalt bis an den Rand (Sammelliste 192)
        if (leiste.Orientation != Avalonia.Layout.Orientation.Vertical || leiste.Bounds.Height <= 0
            || leiste.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is not { Content: Control inhalt } rollen
            || rollen.Parent is not Border { Parent: Border } karte || !karte.Classes.Contains("karte")
            || rollen.Extent.Height <= rollen.Viewport.Height + Toleranz
            || leiste.TranslatePoint(default, wurzel) is not { } oben)
        {
            return;
        }
        var flaeche = new Rect(oben, leiste.Bounds.Size);
        var sichtbar = new Rect(rollen.TranslatePoint(default, wurzel) ?? default, rollen.Bounds.Size);
        foreach (var element in inhalt.GetSelfAndVisualDescendants().OfType<Control>())
        {
            if (element is not (TextBlock or Button or TextBox or Avalonia.Controls.Shapes.Shape) || !element.IsEffectivelyVisible
                || element.Bounds.Width <= 0 || element.TranslatePoint(default, wurzel) is not { } p)
            {
                continue;
            }
            var stelle = new Rect(p, element.Bounds.Size).Intersect(sichtbar);
            var schnitt = stelle.Intersect(flaeche);
            if (schnitt.Width > Toleranz && schnitt.Height > Toleranz)
            {
                funde.Add($"liegt unter der Scrollleiste: {Name(element)} ({stelle.Right:F0} > {flaeche.X:F0})");
            }
        }
    }

    /// <summary>FW6 (Sammelliste 177 Teil 2): Tabelle ohne Umbruch, Hinweis "→ wischen" genau wenn noetig.</summary>
    private static void PruefeTabelle(Views.Tabellenkasten tabelle, List<string> funde)
    {
        var zeilen = (tabelle.Zeilen.Text ?? "").Split('\n').Length;
        if (tabelle.Zeilen.TextLayout.TextLines.Count != zeilen)
        {
            funde.Add($"Tabelle umgebrochen: \"{Kurz(tabelle.Zeilen.Text ?? "")}\" ({tabelle.Zeilen.TextLayout.TextLines.Count} statt {zeilen} Zeilen)");
        }
        // die waagrechte Scrollleiste liegt unter der letzten Zeile, nicht darueber
        if (tabelle.Rollen.GetVisualDescendants().OfType<ScrollBar>().FirstOrDefault(l => l.Orientation == Avalonia.Layout.Orientation.Horizontal
                && l.IsEffectivelyVisible && l.Bounds.Height > 0) is { } leiste
            && leiste.TranslatePoint(default, tabelle.Rollen) is { } oben
            && tabelle.Zeilen.TranslatePoint(default, tabelle.Rollen) is { } text
            && text.Y + tabelle.Zeilen.Padding.Top + tabelle.Zeilen.TextLayout.Height > oben.Y + Toleranz)
        {
            funde.Add($"Scrollleiste ueber der Tabelle: \"{Kurz(tabelle.Zeilen.Text ?? "")}\" ({text.Y + tabelle.Zeilen.TextLayout.Height:F0} > {oben.Y:F0})");
        }
        var breiter = tabelle.Rollen.Extent.Width > tabelle.Rollen.Viewport.Width + Toleranz;
        if (breiter != tabelle.WischHinweis)
        {
            funde.Add($"Tabelle {(breiter ? "ohne" : "mit")} Hinweis wischen: \"{Kurz(tabelle.Zeilen.Text ?? "")}\" ({tabelle.Rollen.Extent.Width:F0} / {tabelle.Rollen.Viewport.Width:F0})");
        }
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
