using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 11 W2 (Weisung P11-20, Plan Phase 11 Abschnitt 3): jedes bedienbare Element einer gezeichneten Seite
/// hat einen Namen, den ein Screenreader vorliest (NVDA am PC, TalkBack am Handy). Bedienbar heisst: sichtbar,
/// eingeschaltet und mit der Tastatur erreichbar (Focusable). Der Name ist genau der, den der Screenreader
/// bekommt: <see cref="AutomationPeer.GetName"/> des Automation-Peers (eigener AutomationProperties.Name oder
/// der Text, den Avalonia aus dem Inhalt ableitet). Nicht geprueft wird nur die 3D-Stadt des Spiels
/// (Nico 02.11.2026, Antwort 2a). Wird von <see cref="Layoutpruefung.Pruefe"/> als Pruefung (11) aufgerufen,
/// gilt also in allen Ansichten, Groessen und Schriftstufen des Start-Tests.
/// </summary>
public static partial class Namenspruefung
{
    public const string Kennung = "ohne Namen fuer den Screenreader: ";

    public static void Pruefe(Control wurzel, List<string> funde)
    {
        foreach (var element in wurzel.GetVisualDescendants().OfType<Control>())
        {
            if (Fund(element) is { } fund)
            {
                funde.Add(fund);
            }
        }
    }

    /// <summary>Fundzeile fuer ein bedienbares Element ohne Namen, sonst null.</summary>
    public static string? Fund(Control element)
    {
        if (!Bedienbar(element) || Ausgenommen(element))
        {
            return null;
        }
        var name = ControlAutomationPeer.CreatePeerForElement(element).GetName();
        return Brauchbar(name) ? null : Kennung + Beschreibung(element) + (string.IsNullOrWhiteSpace(name) ? "" : $" (liest \"{name}\")");
    }

    /// <summary>
    /// Ein Name taugt nur, wenn er Buchstaben oder Ziffern ausserhalb der Symbolschrift hat (ein Symbol aus
    /// fisi_symbole.otf liegt im privaten Bereich und klingt im Screenreader nach nichts) und kein Typname ist:
    /// Avalonia faellt bei einem Knopf mit Bild und Text (StackPanel) auf Content.ToString() zurueck, der
    /// Screenreader laese dann "Avalonia.Controls.StackPanel".
    /// </summary>
    public static bool Brauchbar(string? name) =>
        !string.IsNullOrWhiteSpace(name) && !Typname().IsMatch(name)
        && name.EnumerateRunes().Any(r => Rune.IsLetterOrDigit(r) && !Privat(r.Value));

    [GeneratedRegex(@"^(Avalonia|Lernplattform|System)(\.[A-Za-z_][A-Za-z0-9_`]*)+$")]
    private static partial Regex Typname();

    private static bool Privat(int zeichen) => zeichen is >= 0xE000 and <= 0xF8FF or >= 0xF0000;

    public static bool Bedienbar(Control element) =>
        element is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true }
        && element.Bounds.Width > 0 && element.Bounds.Height > 0;

    /// <summary>Antwort 2a: die 3D-Stadt bekommt keinen Namen; Teile in einer Vorlage (Template) eines
    /// geprueften Elements zaehlen als dessen Teil.</summary>
    private static bool Ausgenommen(Control element) =>
        element is Spiel.StadtFlaeche || element.TemplatedParent is Control { Focusable: true } eltern && eltern != element;

    /// <summary>Wo das Element steht: Typ, dann der naechste sichtbare Text darueber (zum Finden im Bild).</summary>
    private static string Beschreibung(Control element)
    {
        var text = element.GetVisualAncestors().OfType<Control>()
            .Select(v => v.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text))?.Text)
            .FirstOrDefault(t => t is not null);
        var kurz = text is null ? "" : text.Length <= 40 ? text : text[..40] + "…";
        return $"{element.GetType().Name} bei \"{kurz}\"";
    }
}
