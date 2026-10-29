using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W2b (Weisung P8-4, Sammelliste 114-116): Seitenleiste in Gruppen mit Symbolen und LERNSTATUS,
/// Optionen in Gruppen mit zugeklappten Karten. Prueft: jeder Eintrag hat ein Symbol, der Lernstatus zeigt
/// "x / y", alle Optionen-Karten sind nach dem Bauen zu, die Suche (OeffneBereich) klappt jede auf, die
/// Kurzzeile "Updates" zeigt eine gefundene Version. Messpunkte (wiederholbar, Grenze 400 ms): Karteikarten
/// und Spiel in der Seitenleiste aufklappen. Fuer die Layoutpruefung: Seitenleiste mit beiden Eintraegen offen
/// und Optionen ganz oben (alle Karten zu). Speichert nichts.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Anzahl der Ansichten (fuer den Bericht).</summary>
    public const int P8W2bAnsichtenAnzahl = 2;

    /// <summary>Gleich nach dem ersten Oeffnen aller Seiten (die Optionen sind gebaut, noch nichts aufgeklappt).</summary>
    private static async Task<int> P8W2bMesspunkteAsync(MainView ansicht)
    {
        var funde = 0;
        void Fehler(string text)
        {
            Messwerte.Notiere("  FEHLER: " + text);
            funde++;
        }
        var leisten = ansicht.Leisten;
        foreach (var (id, symbol) in leisten.SeitenSymbole)
        {
            if (symbol.Length == 0)
            {
                Fehler("Seitenleiste: Eintrag " + id + " ohne Symbol");
            }
        }
        // Weisung P8-56 (Nico, Wahl b): "Programm beenden" unten in der Seitenleiste (PC, auch ohne Vollbild); Esc
        // verlaesst das Vollbild (gedrueckt wird nur Esc, beendet wird hier nichts)
        if (!OperatingSystem.IsAndroid() && (leisten.BeendenKnopf is not { } beenden || !beenden.IsEffectivelyVisible
            || Avalonia.Automation.AutomationProperties.GetName(beenden) != Navigation.ProgrammBeenden))
        {
            Fehler("Seitenleiste: Knopf \"" + Navigation.ProgrammBeenden + "\" fehlt");
        }
        if (TopLevel.GetTopLevel(ansicht) is { } top)
        {
            Lernplattform.Spiel.Vollbild.DauerhaftSetzen(true);
            await Task.Delay(300);
            Taste(top, Avalonia.Input.Key.Escape);
            await Task.Delay(300);
            Messwerte.Notiere("Vollbild nach Esc: " + (Lernplattform.Spiel.Vollbild.Dauerhaft ? "an" : "aus"));
            if (Lernplattform.Spiel.Vollbild.Dauerhaft)
            {
                Fehler("Vollbild: Esc verlaesst das Vollbild nicht");
                Lernplattform.Spiel.Vollbild.DauerhaftSetzen(false);
            }
        }
        // Lernstatus: wird nach jedem Seitenwechsel im Hintergrund gelesen
        for (var i = 0; i < 100 && leisten.LernstatusText.Length == 0; i++)
        {
            await Task.Delay(50);
        }
        Messwerte.Notiere("Seitenleiste Lernstatus: " + leisten.LernstatusText);
        if (!System.Text.RegularExpressions.Regex.IsMatch(leisten.LernstatusText, @"^[\d.]+ / [\d.]+$"))
        {
            Fehler("Seitenleiste: Lernstatus ohne \"x / y\"");
        }
        foreach (var id in (string[])["cards", "game"])
        {
            var name = leisten.Titel(id);
            var vorher = leisten.Offen(id);
            funde += Ergebnis("Seitenleiste: " + name + " auf-/zuklappen (bis zum Bild)",
                await BildMessenAsync(ansicht, () => leisten.Umschalten(id), () => leisten.Umschalten(id), grenze: Geraetegrenze));
            if (leisten.Offen(id) == vorher)
            {
                Fehler("Seitenleiste: " + name + " klappt nicht um");
            }
            leisten.Umschalten(id);
        }
        if (ansicht.GebauteSeiten.TryGetValue(Seiten.Optionen, out var o) && o is OptionenSeite optionen)
        {
            var bereiche = optionen.Angezeigt;
            foreach (var bereich in bereiche)
            {
                if (optionen.Klappe(bereich.Id) is not { } klappe)
                {
                    Fehler("Optionen: " + bereich.Titel + " ohne Klappkarte");
                }
                else if (klappe.IsExpanded)
                {
                    Fehler("Optionen: " + bereich.Titel + " nach dem Bauen aufgeklappt");
                }
            }
            await ansicht.ZeigeAsync(Seiten.Optionen);
            // Weisung P8-5b: Titel jeder Karte in der Farbe ihrer Gruppe, "Loeschen und zuruecksetzen" rot
            foreach (var bereich in bereiche)
            {
                var titel = optionen.Klappe(bereich.Id)?.GetVisualDescendants().OfType<TextBlock>()
                    .FirstOrDefault(t => t.Text == Beschriftungen.Gross(bereich.Titel));
                var soll = optionen.TryFindResource("Pinsel." + Optionen.Farbe(bereich.Id), out var r) ? r as ISolidColorBrush : null;
                if (titel?.Foreground is not ISolidColorBrush ist || soll is null || ist.Color != soll.Color)
                {
                    Fehler("Optionen: " + bereich.Titel + " nicht in der Farbe " + Optionen.Farbe(bereich.Id));
                }
            }
            foreach (var bereich in bereiche)
            {
                // wie die Suche (open_area): jede Karte aufklappen und wieder zu
                optionen.OeffneBereich(bereich.Id, false);
                await ansicht.BildFertigAsync();
                if (optionen.Klappe(bereich.Id) is { IsExpanded: false })
                {
                    Fehler("Optionen: Suche oeffnet " + bereich.Titel + " nicht");
                }
                optionen.Klappe(bereich.Id)!.IsExpanded = false;
            }
            var probe = optionen.UpdateKurzProbe("A 9.9.9");
            Messwerte.Notiere("Optionen Updates Kurzzeile mit neuer Version: " + probe);
            if (probe != Beschriftungen.Einsetzen(UpdateTexte.Verfuegbar, "A 9.9.9"))
            {
                Fehler("Optionen: Kurzzeile Updates zeigt die neue Version nicht");
            }
            optionen.UpdateKurzProbe(null);
            await ansicht.BildFertigAsync();
        }
        else
        {
            Fehler("Optionen nicht gebaut");
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        return funde;
    }

    private static async IAsyncEnumerable<string> P8W2bAnsichten(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        var leisten = ansicht.Leisten;
        var zu = ((string[])["cards", "game"]).Where(id => !leisten.Offen(id)).ToList();
        foreach (var id in zu)
        {
            leisten.Umschalten(id);
        }
        await ansicht.BildFertigAsync();
        yield return "seitenleiste/karteikarten-und-spiel-offen";
        foreach (var id in zu)
        {
            leisten.Umschalten(id);
        }
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (ansicht.GebauteSeiten[Seiten.Optionen] is OptionenSeite optionen)
        {
            foreach (var bereich in optionen.Angezeigt)
            {
                if (optionen.Klappe(bereich.Id) is { } klappe)
                {
                    klappe.IsExpanded = false;
                }
            }
            if (optionen.Content is ScrollViewer rollen)
            {
                rollen.Offset = default;
            }
        }
        await ansicht.BildFertigAsync();
        yield return "optionen/gruppen-zu";
    }
}
