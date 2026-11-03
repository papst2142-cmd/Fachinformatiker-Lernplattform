using Avalonia.Automation;
using Avalonia.Controls;

namespace Lernplattform.Views;

/// <summary>
/// Phase 11 W2 (Weisung P11-20): Meldungen werden vom Screenreader vorgelesen (NVDA am PC, TalkBack am Handy).
/// Avalonia meldet eine "Live-Region" dann, wenn sich der Text eines TextBlocks mit LiveSetting aendert
/// (UIA LiveRegionChanged unter Windows, LiveRegion des Knotens unter Android). Zwei Wege:
/// <list type="bullet">
/// <item><see cref="Lebend{T}"/> fuer eine Zeile, deren Text sich an Ort und Stelle aendert (Rueckmeldung im Quiz,
/// im Rechen-Trainer, Ergebnis eines Rechners);</item>
/// <item><see cref="Sagen"/> fuer etwas, das neu erscheint (Kurzhinweis, Fenster): ein unsichtbarer Ansagetext ohne
/// Flaeche im Hauptfenster bekommt den neuen Text.</item>
/// </list>
/// Nichts davon ist sichtbar, die Optik bleibt gleich.
/// </summary>
public static class Ansage
{
    private static TextBlock? _ziel;

    /// <summary>Zuletzt angesagter Text (Start-Test).</summary>
    public static string Zuletzt { get; private set; } = "";

    /// <summary>Wird von MainView gesetzt: der unsichtbare Ansagetext (Breite und Hoehe 0).</summary>
    internal static TextBlock Ziel()
    {
        var ziel = new TextBlock { Width = 0, Height = 0, IsHitTestVisible = false };
        AutomationProperties.SetLiveSetting(ziel, AutomationLiveSetting.Assertive);
        _ziel = ziel;
        return ziel;
    }

    /// <summary>Zeile, deren neuer Text vorgelesen wird (hoeflich: nach dem, was gerade gesprochen wird).</summary>
    public static T Lebend<T>(T text) where T : TextBlock
    {
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
        return text;
    }

    /// <summary>Text sofort vorlesen. Gleicher Text zweimal hintereinander wird wieder gelesen.</summary>
    public static void Sagen(string text)
    {
        Zuletzt = text;
        if (_ziel is { } ziel)
        {
            ziel.Text = "";
            ziel.Text = text;
        }
    }
}
