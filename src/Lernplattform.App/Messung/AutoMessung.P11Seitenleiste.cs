using Avalonia.Controls;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 1.15.0 (Weisungen P12-406/P12-407, Variante B; ersetzt die Gruppen von A 0.38.0): Seitenleiste wie das Entwurfsbild,
/// bedient wie mit der Maus. Prueft: Zeilen genau wie <see cref="Pruefungsteile.Seitenleiste"/> der gewaehlten Fachrichtung;
/// jeder Klick zeigt seine Seite und markiert genau diesen Eintrag; jeder Eintrag rollt mit dem Fokus (Tab) ganz ins Bild.
/// Dazu wie bisher die zuklappbaren Gruppen der Optionen (Weisung P11-37). Speichert nichts.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P11SeitenleisteAsync(MainView ansicht)
    {
        var funde = 0;
        void Fehler(string text)
        {
            Messwerte.Notiere("  FEHLER: " + text);
            funde++;
        }
        var leisten = ansicht.Leisten;
        async Task Klick(string id)
        {
            leisten.KlickFuerTests(id);
            await ansicht.BildFertigAsync();
        }
        var soll = Pruefungsteile.Seitenleiste(Core.Pruefung.Fachrichtung.Nach(Zustand.Sicht));
        var ist = leisten.ZeilenFuerTests;
        if (!ist.SequenceEqual(soll))
        {
            Fehler("Seitenleiste: Zeilen [" + string.Join(" | ", ist.Select(z => z.Titel)) + "] statt [" + string.Join(" | ", soll.Select(z => z.Titel)) + "]");
        }
        Messwerte.Notiere("Seitenleiste " + Zustand.Sicht + ": " + string.Join(" | ", ist.Select(z => z.Art + ":" + z.Titel)));
        foreach (var zeile in soll.Concat(Pruefungsteile.SeitenleisteUnten).Where(z => z.Id is not null))
        {
            await Klick(zeile.Id!);
            // A 1.17.0: „Prüfung“ zeigt die zuletzt gewaehlte Haelfte (hier AP1 oder AP2), alles andere seine Seite
            // A 1.19.0 (Weisung P12-440, Nico 4a): ein Bereich oeffnet AP2 › Üben bei diesem Bereich, markiert bleibt der Bereich
            var bereich = PruefungsteilSeite.Bereiche.Contains(zeile.Id!);
            var seite = zeile.Id == Pruefungsteile.Pruefung ? ansicht.LetzteHaelfte : bereich ? Pruefungsteile.Teil2 : zeile.Id!;
            if (ansicht.AktuelleSeite != seite)
            {
                Fehler("Seitenleiste: Klick auf " + zeile.Titel + " zeigt " + ansicht.AktuelleSeite);
            }
            var markiert = leisten.MarkiertFuerTests();
            var sollMarkiert = leisten.SollMarkiertFuerTests(bereich ? zeile.Id! : seite);
            var haupt = Pruefungsteile.Oberpunkt(bereich ? zeile.Id! : seite) ?? zeile.Id!;
            if (!markiert["Knopf"].SequenceEqual([haupt]) || sollMarkiert.Any(s => !s.Value.SequenceEqual(markiert[s.Key])))
            {
                Fehler("Seitenleiste: nach Klick auf " + zeile.Titel + " markiert " + string.Join("; ", markiert.Select(m => m.Key + " [" + string.Join(", ", m.Value) + "]")));
            }
        }
        // Hinweis Chat (Weisung P11-37): jeder Eintrag rollt mit dem Fokus (Tab) ganz ins Bild
        foreach (var zeile in soll.Where(z => z.Id is not null))
        {
            if (await leisten.FokusSichtbarFuerTestsAsync(zeile.Id!) is { } lage && (lage.Oben < -0.5 || lage.Unten > lage.Sichtbar + 0.5))
            {
                Fehler(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Seitenleiste: {zeile.Titel} mit Fokus nicht ganz sichtbar (oben {lage.Oben:F1}, unten {lage.Unten:F1}, sichtbar {lage.Sichtbar:F1})"));
            }
        }
        ansicht.Focus();
        await ansicht.ZeigeAsync(Seiten.Dashboard);

        // Optionen: beim Oeffnen alle Gruppen zu, Kopf mit Kartennamen, Sprung oeffnet die Gruppe, nur eine offen
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (ansicht.GebauteSeiten[Seiten.Optionen] is not OptionenSeite optionen)
        {
            Fehler("Optionen nicht gebaut");
            return funde;
        }
        var gruppen = optionen.GruppenKlappen;
        if (gruppen.Count != Optionen.Gruppen.Count || gruppen.Any(g => g.IsExpanded))
        {
            Fehler("Optionen: beim Oeffnen nicht alle " + Optionen.Gruppen.Count + " Gruppen zu");
        }
        foreach (var (gruppe, (titel, bereiche)) in gruppen.Zip(Optionen.Gruppieren(optionen.Angezeigt)))
        {
            var kurz = string.Join(" · ", bereiche.Select(b => b.Titel));
            var texte = gruppe.GetVisualDescendantsTexte();
            if (!texte.Contains(titel) || !texte.Contains(kurz))
            {
                Fehler("Optionen: Gruppe " + titel + " zeigt Titel oder Kartennamen nicht (" + kurz + ")");
            }
        }
        optionen.OeffneBereich(Optionen.Optik, false);
        await ansicht.BildFertigAsync();
        optionen.OeffneBereich(Optionen.Updates, false);
        await ansicht.BildFertigAsync();
        var offen = gruppen.Where(g => g.IsExpanded).Select(g => Avalonia.Automation.AutomationProperties.GetName(g)).ToList();
        Messwerte.Notiere("Optionen nach Sprung zu Aussehen, dann Updates: offen [" + string.Join(", ", offen) + "]");
        if (!offen.SequenceEqual([Optionen.GruppeProgramm]))
        {
            Fehler("Optionen: nach dem Sprung zu Updates nicht nur PROGRAMM offen: " + string.Join(", ", offen));
        }
        optionen.Klappe(Optionen.Optik)!.IsExpanded = false;
        optionen.Klappe(Optionen.Updates)!.IsExpanded = false;
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (gruppen.Any(g => g.IsExpanded))
        {
            Fehler("Optionen: Gruppe bleibt beim erneuten Oeffnen der Seite offen");
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        return funde;
    }

    private static List<string> GetVisualDescendantsTexte(this Control wurzel) =>
        [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(wurzel).OfType<TextBlock>().Select(t => t.Text ?? "")];
}
