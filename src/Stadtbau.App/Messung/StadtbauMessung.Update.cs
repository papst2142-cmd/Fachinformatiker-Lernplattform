using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Messung;
using Lernplattform.Views;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Messung;

/// <summary>
/// Patch S 0.5.1 Nachbesserung (Weisung P12-42 Punkt 1): Knopf "Nach Updates suchen" im Startbild mit einer Testquelle.
/// Jedes Ergebnis bekommt ein Fenster mit der richtigen Antwort (neu, neueste Version, nicht installiert, Fehler); beim
/// Start fragt das Werkzeug nur bei einer neuen Version (Gegenprobe: die Testquelle sagt beim Start "aktuell", es kam
/// kein Fenster).
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Antwort der Testquelle.</summary>
    private static UpdateErgebnis UpdateTestErgebnis { get; set; } = new(UpdateArt.Aktuell);

    private static List<string> DialogTexte() =>
        Dialog.Offen is { } d ? [.. Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(d).OfType<TextBlock>().Select(t => t.Text ?? "")] : [];

    private static async Task DialogKnopfAsync(string text)
    {
        if (Dialog.Offen is { } d)
        {
            foreach (var k in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(d).OfType<Button>().Where(k => k.Content is TextBlock { Text: var t } && t == text).ToList())
            {
                k.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
        await Task.Delay(200);
    }

    private static async Task UpdateKnopfAsync(Hauptansicht a, Window? f)
    {
        if (Dialog.Offen is not null)
        {
            Fund("Update: beim Start kam ein Fenster, obwohl die Testquelle \"aktuell\" sagt: " + string.Join(" ", DialogTexte()));
            await DialogKnopfAsync(Texte.Ok);
        }
        a.StartZeigenFuerTests();
        await BildAsync(a);
        var knopf = a.KnoepfeMit(Texte.UpdateSuchen).FirstOrDefault(k => k.IsVisible);
        if (knopf is null)
        {
            Fund("Update: Knopf \"" + Texte.UpdateSuchen + "\" fehlt im Startbild");
            return;
        }
        var faelle = new (UpdateErgebnis Ergebnis, string Erwartet, string Schliessen)[]
        {
            (new UpdateErgebnis(UpdateArt.Aktuell), Texte.UpdateAktuell(Werkzeug.Anzeige), Texte.Ok),
            (new UpdateErgebnis(UpdateArt.Neu, "9.9.9", () => Task.CompletedTask), Texte.UpdateNeuText("9.9.9"), Texte.UpdateSpaeter),
            (new UpdateErgebnis(UpdateArt.NichtInstalliert), Texte.UpdateNichtInstalliert, Texte.Ok),
            (new UpdateErgebnis(UpdateArt.Fehler, Grund: "Test"), Texte.UpdateFehler, Texte.Ok),
        };
        foreach (var (ergebnis, erwartet, schliessen) in faelle)
        {
            UpdateTestErgebnis = ergebnis;
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 50 && !DialogTexte().Contains(erwartet); i++)
            {
                await Task.Delay(50);
            }
            if (!DialogTexte().Contains(erwartet))
            {
                Fund("Update (" + ergebnis.Art + "): Fenster zeigt \"" + string.Join(" ", DialogTexte()) + "\" statt \"" + erwartet + "\"");
            }
            else
            {
                Messwerte.Notiere("Update (" + ergebnis.Art + "): " + erwartet);
            }
            if (ergebnis.Art == UpdateArt.Aktuell)
            {
                await AlleGroessenAsync(a, f, "Update aktuell");
            }
            await DialogKnopfAsync(schliessen);
        }
        UpdateTestErgebnis = new UpdateErgebnis(UpdateArt.Aktuell);
        // Gegenprobe: beim Start nur bei einer neuen Version ein Fenster
        if (UpdateSuche.BeimStartZeigen(new UpdateErgebnis(UpdateArt.Aktuell)) || !UpdateSuche.BeimStartZeigen(new UpdateErgebnis(UpdateArt.Neu)))
        {
            Fund("Update: Regel fuer das Fenster beim Start falsch");
        }
    }
}
