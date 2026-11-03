using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;

namespace Lernplattform.Views;

/// <summary>
/// Phase 11 W2 (Weisung P11-20): Namen fuer den Screenreader (NVDA am PC, TalkBack am Handy). Ein Name ist kein
/// sichtbarer Text, die Optik bleibt gleich. Die Pruefung im Start-Test steht in Messung/Namenspruefung.cs.
/// </summary>
public static class Vorlesen
{
    /// <summary>Name, den der Screenreader vorliest (statt des sichtbaren Inhalts).</summary>
    public static T Vorlese<T>(this T element, string name) where T : Control
    {
        AutomationProperties.SetName(element, name);
        return element;
    }

    /// <summary>Feld oder Auswahl mit sichtbarer Beschriftung daneben oder darueber: der Screenreader liest sie als Namen.</summary>
    public static T Beschriftet<T>(this T element, Control beschriftung) where T : Control
    {
        AutomationProperties.SetLabeledBy(element, beschriftung);
        return element;
    }

    /// <summary>Beschriftung, die als Name fuer das Feld oder die Auswahl darunter gilt (gibt die Beschriftung zurueck).</summary>
    public static T Fuer<T>(this T beschriftung, Control feld) where T : Control
    {
        AutomationProperties.SetLabeledBy(feld, beschriftung);
        return beschriftung;
    }

    /// <summary>Alle Texte einer Flaeche (ohne Symbole und unsichtbare Teile), mit Punkt getrennt.</summary>
    public static string Text(Control flaeche)
    {
        IEnumerable<TextBlock> bloecke = flaeche is TextBlock t ? [t] : flaeche.GetLogicalDescendants().OfType<TextBlock>();
        return string.Join(". ", bloecke.Where(b => b.IsVisible && b.FontFamily != Symbole.Familie)
            .Select(b => (b.Inlines?.Text ?? b.Text ?? "").Replace("\u200B", "", StringComparison.Ordinal).Trim())
            .Where(x => x.Length > 0));
    }

    /// <summary>
    /// Einmal beim Start: der Kopf eines Expanders mit der Vorlage von Fluent (Hilfe, Fortschritt, Lizenzen, Vorlagen der
    /// Farben) bekommt den Namen des Expanders. Ohne das liest Avalonia den Typ des Kopfinhalts vor
    /// ("Avalonia.Controls.DockPanel").
    /// </summary>
    internal static void Einrichten()
    {
        Expander.TemplateAppliedEvent.AddClassHandler<Expander>((klappe, e) =>
        {
            if (e.NameScope.Find<ToggleButton>("ExpanderHeader") is { } kopf)
            {
                kopf[!AutomationProperties.NameProperty] = klappe[!AutomationProperties.NameProperty];
            }
        });
    }
}

/// <summary>
/// Phase 11 W2: antippbare Flaeche (Zeile einer Liste, Karte im Spiel), die sich als Schaltflaeche meldet. Der
/// Screenreader liest ihren eigenen Namen oder, ohne Namen, alle Texte darin, und kann sie ausloesen (Invoke: NVDA
/// Eingabe, TalkBack Doppeltippen) wie ein Klick.
/// </summary>
public class Klickflaeche : Border
{
    /// <summary>Was ein Klick, Eingabe/Leertaste oder der Screenreader ausloest.</summary>
    public Action? Klick { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(Klickflaeche flaeche) : ControlAutomationPeer(flaeche), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Vorlesen.Text(flaeche);

        public void Invoke() => flaeche.Klick?.Invoke();
    }
}
