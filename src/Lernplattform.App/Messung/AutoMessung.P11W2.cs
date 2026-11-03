using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 11 W2 (Weisung P11-20): Screenreader und Tastatur im Start-Test.
/// (a) Gegenprobe der Namenspruefung (Layoutpruefung (11)): ein Knopf ohne Inhalt, ein Knopf nur mit einem Symbol und ein
/// Knopf, dessen Name Avalonia aus dem Typ ableiten wuerde ("Avalonia.Controls.StackPanel"), machen die Pruefung rot.
/// (b) Ein offenes Fenster haelt Tab fest (vom letzten Knopf zum ersten, nie auf die Seite darunter) und wird beim
/// Oeffnen angesagt (Titel und Text).
/// (c) Auf jeder Seite (PC-Groesse, alles aufgeklappt) erreicht Tab jedes bedienbare Element; die Reihenfolge ist die des
/// Aufbaus (keine TabIndex-Werte im Programm), also Seitenleiste, Kopf, Inhalt von oben nach unten.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P11W2TastaturAsync(MainView ansicht)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("Screenreader und Tastatur (W2): FEHLER, " + text);
            funde++;
        }

        // (a) Gegenprobe
        var ohne = new Button { Width = 60, Height = 30 };
        var symbol = new Button { Width = 60, Height = 30, Content = new TextBlock { Text = Symbole.Text("ARROW_RIGHT"), FontFamily = Symbole.Familie } };
        var typ = new Button { Width = 60, Height = 30, Content = new StackPanel() };
        var probe = new StackPanel { Children = { ohne, symbol, typ } };
        var offen = Dialog.Zeigen("Gegenprobe", probe, Dialog.Ok);
        await ansicht.BildFertigAsync();
        await ansicht.BildFertigAsync();
        var gefunden = new[] { ohne, symbol, typ }.Select(k => Namenspruefung.Fund(k)).ToList();
        var gesamt = Layoutpruefung.Pruefe(ansicht).Count(f => f.StartsWith(Namenspruefung.Kennung, StringComparison.Ordinal));
        if (gefunden.Any(f => f is null) || gesamt < 3)
        {
            Fund($"Gegenprobe: {gefunden.Count(f => f is not null)} von 3 Knoepfen ohne brauchbaren Namen erkannt, in der Layoutpruefung {gesamt}");
        }
        Dialog.AlleSchliessen();
        await offen;

        // (b) Fenster: Tab bleibt drin, Ansage beim Oeffnen
        var fenster = Dialog.Zeigen("Probe Tastatur", "Text zum Vorlesen", "Abbrechen", "Weiter");
        await ansicht.BildFertigAsync();
        await ansicht.BildFertigAsync();
        if (!Ansage.Zuletzt.StartsWith("Probe Tastatur. Text zum Vorlesen", StringComparison.Ordinal))
        {
            Fund($"Fenster nicht angesagt (zuletzt \"{Ansage.Zuletzt}\")");
        }
        if (Dialog.Offen is { } d && d.GetVisualDescendants().OfType<Button>().FirstOrDefault() is { } erster)
        {
            erster.Focus(NavigationMethod.Tab);
            IInputElement? jetzt = erster;
            for (var i = 0; i < 6 && jetzt is not null; i++)
            {
                jetzt = Naechstes(jetzt);
                if (jetzt is not Avalonia.Visual v || !v.GetVisualAncestors().Contains(d))
                {
                    Fund($"Tab verlaesst das offene Fenster nach {i + 1} Schritten ({jetzt?.GetType().Name ?? "nichts"})");
                    break;
                }
            }
        }
        else
        {
            Fund("Probefenster ohne Knopf");
        }
        Dialog.AlleSchliessen();
        await fenster;

        // (c) jede Seite: Tab erreicht jedes bedienbare Element
        var seiten = 0;
        foreach (var id in ansicht.GebauteSeiten.Keys.Where(id => id != Seiten.Spiel).ToList())
        {
            await ansicht.ZeigeAsync(id);
            if (ansicht.GebauteSeiten[id] is IAufklappbar klappbar)
            {
                klappbar.AllesAufklappen();
            }
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            var alle = ansicht.GetVisualDescendants().OfType<Control>().Where(Tabziel).ToList();
            if (alle.Count == 0)
            {
                continue;
            }
            var erreicht = new HashSet<Control>();
            IInputElement? schritt = alle[0];
            for (var i = 0; i < 5000 && schritt is Control c && erreicht.Add(c); i++)
            {
                schritt = Naechstes(c);
            }
            var fehlt = alle.Where(c => !erreicht.Contains(c)).ToList();
            foreach (var c in fehlt.Take(5))
            {
                Fund($"{id}: mit Tab nicht erreichbar: {c.GetType().Name} \"{Kurz(Vorlesen.Text(c))}\"");
            }
            if (fehlt.Count > 5)
            {
                Fund($"{id}: {fehlt.Count - 5} weitere nicht erreichbar");
            }
            seiten++;
        }
        if (funde == 0)
        {
            Messwerte.Notiere($"Screenreader und Tastatur (W2): Gegenprobe Namen 3/3 rot, Fenster haelt Tab und wird angesagt, "
                + $"Tab erreicht jedes bedienbare Element auf {seiten} Seiten: in Ordnung");
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        return funde;
    }

    /// <summary>Mit Tab erreichbar sein muss: bedienbar (Namenspruefung) und ein Tab-Halt. Gruppen mit
    /// TabNavigation "Once" (Auswahlknoepfe) erreicht man mit Tab einmal und dann mit den Pfeiltasten.</summary>
    private static bool Tabziel(Control c) =>
        Namenspruefung.Bedienbar(c) && KeyboardNavigation.GetIsTabStop(c)
        && c.TemplatedParent is not Control { Focusable: true }
        && !c.GetVisualAncestors().OfType<InputElement>().Any(v => KeyboardNavigation.GetTabNavigation(v) is KeyboardNavigationMode.Once or KeyboardNavigationMode.None);

    /// <summary>Wohin die Tab-Taste vom Element aus springt (der Fokus wandert dabei mit).</summary>
    private static IInputElement? Naechstes(IInputElement von)
    {
        if (von is not Control c || TopLevel.GetTopLevel(c)?.FocusManager is not { } fokus)
        {
            return null;
        }
        // wie ein echter Tastendruck: Tab an das Element, die Tastaturfuehrung des Fensters setzt den Fokus weiter
        c.Focus(NavigationMethod.Tab);
        c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Tab, Source = c });
        return fokus.GetFocusedElement();
    }

    private static string Kurz(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
