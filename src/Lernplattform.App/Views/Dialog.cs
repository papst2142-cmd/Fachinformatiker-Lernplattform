using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2: Hinweis- und Rueckfragefenster (wie messagebox in Python) als Ebene ueber der
/// Seite, gleich auf PC und Handy. Titel, Text oder eigener Inhalt, Knoepfe; Rueckgabe ist
/// die Nummer des gewaehlten Knopfs, -1 bei Schliessen mit Escape bzw. ohne Ebene.
/// </summary>
public static class Dialog
{
    public const string Ok = "OK";

    /// <summary>FW6 (Sammelliste 61/62): Abstand rechts in Fenstern mit Bildlauf (Dialog, Rundgang), breiter als die Scrollleiste (6).</summary>
    public const double RandFuerLeiste = 12;

    private static Panel? _ebene;

    /// <summary>Gerade offenes Fenster (fuer die Layoutpruefung im Start ohne Bedienung).</summary>
    public static Control? Offen { get; private set; }

    /// <summary>Wird von MainView gesetzt: die Ebene ueber allen Seiten.</summary>
    internal static void Ebene(Panel ebene) => _ebene = ebene;

    public static Task<int> Zeigen(string titel, string text, params string[] knoepfe) =>
        Zeigen(titel, Schrift.Text(text), knoepfe);

    public static Task<int> Zeigen(string titel, Control inhalt, params string[] knoepfe) => Zeigen(titel, inhalt, null, knoepfe);

    /// <summary>W4: wie oben; vorbereiten bekommt die Knoepfe (z. B. "Speichern" erst nach einer Wahl freigeben).</summary>
    public static Task<int> Zeigen(string titel, Control inhalt, Action<IReadOnlyList<Button>>? vorbereiten, params string[] knoepfe) =>
        Zeigen(titel, inhalt, vorbereiten, -1, knoepfe);

    /// <summary>
    /// Phase 5 BE10 (UpdateDialog): wie oben, aber ein Knopf, fuer den bleibtOffen(nummer) true liefert,
    /// schliesst das Fenster nicht (z. B. "Jetzt aktualisieren" zeigt danach den Fortschritt im selben Fenster);
    /// bleibtOffen(-1) entscheidet ueber Escape (waehrend des Updates bleibt das Fenster offen).
    /// </summary>
    public static Task<int> Zeigen(string titel, Control inhalt, Action<IReadOnlyList<Button>>? vorbereiten, Func<int, bool> bleibtOffen,
        params string[] knoepfe) => Zeigen(titel, inhalt, vorbereiten, -1, knoepfe, bleibtOffen);

    /// <summary>
    /// Phase 5: Rueckfrage mit Vorgabe (askyesno(default="no")): Eingabe/Leertaste waehlt den Knopf
    /// mit der Nummer vorgabe, nicht den ersten.
    /// </summary>
    public static Task<int> Rueckfrage(string titel, string text, int vorgabe, params string[] knoepfe) =>
        Zeigen(titel, Schrift.Text(text), null, vorgabe, knoepfe);

    /// <summary>
    /// Welle A 0.81.0 (Weisung P12-271, Sammelliste 348, Nico: „Handy schließen über ein X oben rechts, dass man nicht erst
    /// nach unten scrollen muss“): wie <see cref="Zeigen(string, Control, string[])"/>, aber Titel und ein X oben rechts
    /// stehen fest UEBER dem Bereich mit Bildlauf (immer zu sehen, bei jeder Schriftstufe und Groesse). Das X schliesst wie
    /// Escape (Rueckgabe -1). <paramref name="kreuzName"/> ist der Name fuer den Screenreader. Ohne <paramref name="knoepfe"/>
    /// (Spiel-Handy, Weisung P12-272: Nico „unten der Schließen-Button kann entfernt werden“) schliesst nur das X (und Escape).
    /// </summary>
    public static Task<int> ZeigenMitKreuz(string titel, string kreuzName, Control inhalt, params string[] knoepfe) =>
        Zeigen(titel, inhalt, null, -1, knoepfe, null, kreuzName);

    /// <summary>Welle A 0.81.0: X und Knopfreihe je offenem Fenster (fuer <see cref="KreuzVon"/> und <see cref="AlleSchliessen"/>).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, Button> Kreuze = new();

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, WrapPanel> Reihen = new();

    /// <summary>Start-Test: das X oben rechts eines offenen Fensters (null = Fenster ohne X).</summary>
    internal static Button? KreuzVon(Control? fenster) => fenster is not null && Kreuze.TryGetValue(fenster, out var k) ? k : null;

    private static Task<int> Zeigen(string titel, Control inhalt, Action<IReadOnlyList<Button>>? vorbereiten, int vorgabe, string[] knoepfe,
        Func<int, bool>? bleibtOffen = null, string? kreuzName = null)
    {
        var fertig = new TaskCompletionSource<int>();
        if (_ebene is not { } ebene)
        {
            fertig.SetResult(-1);
            return fertig.Task;
        }
        var reihe = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var fenster = new Border();
        void Schliessen(int wahl)
        {
            ebene.Children.Remove(fenster);
            ebene.IsVisible = ebene.Children.Count > 0;
            Offen = ebene.Children.Count > 0 ? (Control)ebene.Children[^1] : null;
            fertig.TrySetResult(wahl);
        }
        for (var i = 0; i < knoepfe.Length; i++)
        {
            var nummer = i;
            var knopf = Schrift.Knopf(knoepfe[i], i == 0);
            knopf.Click += (_, _) =>
            {
                if (bleibtOffen?.Invoke(nummer) != true)
                {
                    Schliessen(nummer);
                }
            };
            reihe.Children.Add(knopf);
        }
        vorbereiten?.Invoke([.. reihe.Children.OfType<Button>()]);
        var titelText = Schrift.Text(titel, "Gross", FontWeight.Bold);
        // Welle S 0.57.0 (Weisung P12-297 W3-1): ein Fenster, das nach einem Fehler mit demselben Inhalt neu oeffnet
        // („Neues Projekt“, Blueprint speichern/umbenennen), loest den Inhalt erst vom alten Fenster; sonst wirft Avalonia
        // („hat schon einen Eltern-Knoten“), das Fenster bleibt zu und der Grund erscheint nie
        (inhalt.Parent as Panel)?.Children.Remove(inhalt);
        var koerper = new StackPanel { Spacing = 10 };
        if (kreuzName is null)
        {
            koerper.Children.Add(titelText);
        }
        koerper.Children.Add(inhalt);
        if (knoepfe.Length > 0)
        {
            koerper.Children.Add(reihe);
        }
        // FW6 (Sammelliste 61/62): rechts Platz fuer die Scrollleiste, sonst liegt sie ueber dem Text
        koerper.Margin = new Thickness(0, 0, RandFuerLeiste, 0);
        var rollen = new ScrollViewer { Content = koerper };
        Control innen = rollen;
        Button? kreuz = null;
        if (kreuzName is not null)
        {
            // Welle A 0.81.0 (Sammelliste 348): Kopf mit Titel und X fest ueber dem Bildlauf
            kreuz = KreuzKnopf(kreuzName);
            kreuz.Click += (_, _) => Schliessen(-1);
            titelText.VerticalAlignment = VerticalAlignment.Center;
            var kopf = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 10) };
            kopf.Children.Add(titelText);
            Grid.SetColumn(kreuz, 1);
            kopf.Children.Add(kreuz);
            var gitter = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            gitter.Children.Add(kopf);
            Grid.SetRow(rollen, 1);
            gitter.Children.Add(rollen);
            innen = gitter;
        }
        var karte = Schrift.Karte(innen);
        karte.MaxWidth = 560;
        karte.Margin = new Thickness(16);
        karte.HorizontalAlignment = HorizontalAlignment.Stretch;
        karte.VerticalAlignment = VerticalAlignment.Center;
        Avalonia.Automation.AutomationProperties.SetName(karte, titel);
        fenster.Child = karte;
        Reihen.AddOrUpdate(fenster, reihe);
        if (kreuz is not null)
        {
            Kreuze.AddOrUpdate(fenster, kreuz);
        }
        fenster.Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0));
        fenster.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (bleibtOffen?.Invoke(-1) != true)
                {
                    Schliessen(-1);
                }
            }
        };
        // Phase 11 W2 (Weisung P11-20): Tab bleibt im Fenster (springt vom letzten zum ersten Knopf, nie auf die Seite
        // darunter), der Screenreader liest Titel und Text vor
        KeyboardNavigation.SetTabNavigation(fenster, KeyboardNavigationMode.Cycle);
        ebene.Children.Add(fenster);
        ebene.IsVisible = true;
        Offen = fenster;
        Ansage.Sagen(Vorlesetext(titel, inhalt));
        // Fokus auf den ersten Knopf (Eingabe/Leertaste bestaetigt), W4: den ersten freigegebenen, Phase 5: die Vorgabe;
        // FW6 (Sammelliste 62): das Fenster beginnt trotzdem oben beim Titel
        // Welle A 0.81.0 (P12-272): ein Fenster nur mit X (ohne Knoepfe unten) gibt dem X den Fokus
        FokusOhneRollen(rollen, () =>
            vorgabe >= 0 && vorgabe < reihe.Children.Count ? reihe.Children[vorgabe] : reihe.Children.FirstOrDefault(k => k.IsEnabled) ?? kreuz);
        return fertig.Task;
    }

    /// <summary>
    /// Welle A 0.81.0: X oben rechts im Kopf eines Fensters, gleich dem X oben rechts am Handy (A 0.37.0, MainView
    /// <c>Symbolknopf</c>): 48 x 48, Symbol 24 in <c>text_dim</c>, ohne Hintergrund; Name fuer Screenreader und Tipp.
    /// </summary>
    private static Button KreuzKnopf(string name)
    {
        var knopf = new Button
        {
            Content = Symbole.Block("CLOSE_ROUNDED", 24, "Pinsel.text_dim"),
            Padding = new Thickness(12),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8, 0, 0, 0),
        };
        ToolTip.SetTip(knopf, name);
        Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        return knopf;
    }

    /// <summary>
    /// Start-Test (Welle A 0.81.0, Sammelliste 348): ist das X oben rechts im offenen Fenster da und ohne Rollen zu sehen?
    /// Prueft Name, Lage im Fenster und in der Karte (rechts oben, nicht im Bildlauf) und rollt den Inhalt dafuer ganz
    /// nach unten. null = in Ordnung, sonst der Fund. Ein Fenster ohne X liefert „kein X“ (Gegenprobe).
    /// </summary>
    internal static string? KreuzFund(Control? fenster, string name)
    {
        if (fenster is not Border { Child: Border karte } || KreuzVon(fenster) is not { } kreuz)
        {
            return "kein X oben rechts";
        }
        if (Avalonia.Automation.AutomationProperties.GetName(kreuz) != name)
        {
            return "X ohne Namen \"" + name + "\"";
        }
        if (kreuz.FindLogicalAncestorOfType<ScrollViewer>() is { } r && karte.IsLogicalAncestorOf(r))
        {
            return "X liegt im Bildlauf (rollt weg)";
        }
        foreach (var rollen in karte.GetLogicalDescendants().OfType<ScrollViewer>().Take(1))
        {
            rollen.Offset = new Vector(0, Math.Max(0, rollen.Extent.Height - rollen.Viewport.Height));
        }
        if (!kreuz.IsEffectivelyVisible || kreuz.Bounds.Width < 1 || kreuz.TranslatePoint(default, fenster) is not { } p
            || karte.TranslatePoint(default, fenster) is not { } k)
        {
            return "X nicht zu sehen";
        }
        var x = new Rect(p, kreuz.Bounds.Size);
        var kr = new Rect(k, karte.Bounds.Size);
        if (x.X < -0.5 || x.Y < -0.5 || x.Right > fenster.Bounds.Width + 0.5 || x.Bottom > fenster.Bounds.Height + 0.5)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"X ragt aus dem Fenster ({x.X:0.#},{x.Y:0.#} {x.Width:0.#}x{x.Height:0.#} von {fenster.Bounds.Width:0.#}x{fenster.Bounds.Height:0.#})");
        }
        if (!kr.Contains(x) || x.Y - kr.Y > kr.Height / 3 || kr.Right - x.Right > 40)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"X nicht oben rechts in der Karte (X {x.X:0.#},{x.Y:0.#}, Karte {kr.X:0.#},{kr.Y:0.#} {kr.Width:0.#}x{kr.Height:0.#})");
        }
        return null;
    }

    /// <summary>Phase 11 W2: Titel und alle Texte des Inhalts (ohne Knoepfe und Felder) zum Vorlesen beim Oeffnen.</summary>
    internal static string Vorlesetext(string titel, Control inhalt)
    {
        IEnumerable<TextBlock> bloecke = inhalt is TextBlock t ? [t] : inhalt.GetLogicalDescendants().OfType<TextBlock>();
        var texte = bloecke.Where(b => b.FindLogicalAncestorOfType<Button>() is null && b.FindLogicalAncestorOfType<TextBox>() is null)
            .Select(b => b.Inlines?.Text ?? b.Text).Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(". ", texte.Prepend(titel)).Replace("\u200B", "", StringComparison.Ordinal);
    }

    /// <summary>
    /// FW6 (Sammelliste 62, Weisung P11-12): Fokus auf ein Element setzen, ohne dass der Bereich dorthin rollt. Sonst
    /// steht ein langes Fenster (Handy, grosse Schrift) beim Oeffnen am Textende, weil der Knopf mit dem Fokus unten
    /// liegt, und der Titel ist nicht zu sehen. Danach rollt der Bereich wieder mit dem Fokus (Tab).
    /// </summary>
    internal static void FokusOhneRollen(ScrollViewer rollen, Func<Control?> ziel, NavigationMethod art = NavigationMethod.Unspecified)
    {
        rollen.SetValue(ScrollViewer.BringIntoViewOnFocusChangeProperty, false);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ziel()?.Focus(art);
            rollen.Offset = default;
            rollen.ClearValue(ScrollViewer.BringIntoViewOnFocusChangeProperty);
        });
    }

    /// <summary>
    /// Phase 5 BE3 (show_wait, app_gui): „Bitte warten“ als Ebene ueber dem ganzen Fenster, ohne Knopf;
    /// Maus und Tastatur erreichen die Seite darunter nicht, Escape schliesst sie nicht. Dispose nimmt sie weg.
    /// </summary>
    public static IDisposable Warten(string text)
    {
        if (_ebene is not { } ebene)
        {
            return new Weg(() => { });
        }
        var karte = Schrift.Karte(Schrift.Text(text, "Mittel", FontWeight.Bold));
        karte.MaxWidth = 560;
        karte.Margin = new Thickness(16);
        karte.HorizontalAlignment = HorizontalAlignment.Center;
        karte.VerticalAlignment = VerticalAlignment.Center;
        Avalonia.Automation.AutomationProperties.SetName(karte, text);
        var fenster = new Border { Child = karte, Focusable = true, Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)) };
        Avalonia.Automation.AutomationProperties.SetName(fenster, text);
        // Jede Taste bleibt hier (auch Tab, Escape, Strg+F): nichts darunter laesst sich bedienen
        fenster.AddHandler(InputElement.KeyDownEvent, (_, e) => e.Handled = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ebene.Children.Add(fenster);
        ebene.IsVisible = true;
        Offen = fenster;
        Ansage.Sagen(text);
        fenster.Focus();
        return new Weg(() =>
        {
            ebene.Children.Remove(fenster);
            ebene.IsVisible = ebene.Children.Count > 0;
            Offen = ebene.Children.Count > 0 ? (Control)ebene.Children[^1] : null;
        });
    }

    private sealed class Weg(Action weg) : IDisposable
    {
        private Action? _weg = weg;

        public void Dispose()
        {
            _weg?.Invoke();
            _weg = null;
        }
    }

    /// <summary>Alle offenen Fenster schliessen (Start ohne Bedienung).</summary>
    public static void AlleSchliessen()
    {
        if (_ebene is { } ebene)
        {
            foreach (var fenster in ebene.Children.OfType<Border>().ToList())
            {
                // Welle A 0.81.0 (P12-272): ohne Knoepfe unten (Spiel-Handy) schliesst das X
                if (((Reihen.TryGetValue(fenster, out var reihe) ? reihe.Children.LastOrDefault() as Button : null) ?? KreuzVon(fenster)) is { } knopf)
                {
                    knopf.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                }
            }
        }
    }
}
